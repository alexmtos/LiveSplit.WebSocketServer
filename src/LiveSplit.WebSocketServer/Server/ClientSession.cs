using LsLog = LiveSplit.Options.Log;
using LiveSplit.WsServer.Commands;
using LiveSplit.WsServer.Infrastructure;
using LiveSplit.WsServer.Protocol;
using LiveSplit.WsServer.State;
using Versions = LiveSplit.WsServer.Protocol.ProtocolVersion;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading.Tasks;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace LiveSplit.WsServer.Server;

/// <summary>
///     One connected WebSocket client. Messages arrive on a websocket-sharp worker
///     thread and are executed on LiveSplit's UI thread.
/// </summary>
public sealed class ClientSession : WebSocketBehavior, ISessionControl
{
    /// <summary>
    ///     Actions of protocol version 1. Version 1 clients keep receiving exactly what they used to
    ///     for them: a reply to "hi" and "state" only.
    /// </summary>
    private static readonly HashSet<string> LegacyActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "hi", "state", "startorsplit", "split", "unsplit", "skipsplit", "pause", "resume",
        "reset", "starttimer", "pausegametime", "unpausegametime",
    };

    private readonly ServerRuntime runtime;

    // Written on the UI thread, read on websocket-sharp threads.
    private volatile int protocolVersion = Versions.Legacy;

    public int ProtocolVersion
    {
        get => protocolVersion;
        set => protocolVersion = value;
    }

    public SessionSubscription Subscription { get; } = new();

    /// <summary>
    ///     When the last tick was sent. Only used on the UI thread.
    /// </summary>
    internal DateTime LastTick { get; set; }

    // Set once in OnOpen, before any message is processed.
    private volatile bool authorized;

    public ClientSession(ServerRuntime runtime)
    {
        this.runtime = runtime;

        // Browsers send the page's origin; other clients usually send none.
        OriginValidator = origin => IsOriginAllowed(runtime.Options.AllowedOriginList, origin);
    }

    public static bool IsOriginAllowed(IReadOnlyCollection<string> allowedOrigins, string origin)
    {
        if (allowedOrigins == null || allowedOrigins.Count == 0 || string.IsNullOrEmpty(origin))
        {
            return true;
        }

        string normalized = origin.Trim().TrimEnd('/');
        foreach (string allowed in allowedOrigins)
        {
            if (allowed == "*" || string.Equals(allowed, normalized, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsTokenValid(string expected, string provided)
    {
        if (string.IsNullOrEmpty(expected))
        {
            return true;
        }

        if (provided == null || provided.Length != expected.Length)
        {
            return false;
        }

        // Constant time comparison.
        int difference = 0;
        for (int i = 0; i < expected.Length; i++)
        {
            difference |= expected[i] ^ provided[i];
        }

        return difference == 0;
    }

    public bool IsOpen => authorized && State == WebSocketState.Open;

    protected override void OnOpen()
    {
        if (!IsTokenValid(runtime.Options.AuthToken, Context.QueryString?["token"]))
        {
            string error = Json.Serialize(ResponseMessage.Failure(null, null, ErrorCodes.Unauthorized,
                "A valid token is required. Connect with ?token=... (see the component settings)."));
            try
            {
                Send(error);
                Context.WebSocket.Close(CloseStatusCode.PolicyViolation, "Unauthorized");
            }
            catch (Exception)
            {
                // The client is already gone.
            }

            return;
        }

        authorized = true;

        string requested = Context.QueryString?["protocol"];
        if (requested != null
            && int.TryParse(requested, NumberStyles.Integer, CultureInfo.InvariantCulture, out int version)
            && Versions.IsSupported(version))
        {
            ProtocolVersion = version;
        }

        Reply(runtime.Dispatcher.InvokeAsync(() =>
        {
            if (ProtocolVersion == Versions.Legacy)
            {
                return Json.Serialize(new
                {
                    open = new { response = "success" },
                    state = runtime.Snapshots.Build(runtime.State, SnapshotOptions.Legacy),
                });
            }

            return Json.Serialize(SessionCommands.CreateHello(new CommandContext(runtime, this), includeState: true));
        }));
    }

    protected override void OnMessage(MessageEventArgs e)
    {
        if (!authorized || !e.IsText)
        {
            return;
        }

        Request request;
        try
        {
            request = Request.Parse(e.Data);
        }
        catch (RequestParseException parseError)
        {
            if (ProtocolVersion != Versions.Legacy)
            {
                SendText(Json.Serialize(ResponseMessage.Failure(parseError.Id, null, ErrorCodes.InvalidRequest, parseError.Message)));
            }

            return;
        }

        Reply(runtime.Dispatcher.InvokeAsync(() => Execute(request)).ContinueWith(task =>
        {
            if (task.Status == TaskStatus.RanToCompletion)
            {
                return task.Result;
            }

            // The UI thread was not reachable.
            Exception error = task.Exception?.GetBaseException();
            return ProtocolVersion == Versions.Legacy && LegacyActions.Contains(request.Action)
                ? null
                : Json.Serialize(ResponseMessage.Failure(request.Id, request.Action, ErrorCodes.Unavailable, error?.Message ?? "LiveSplit is not available."));
        }, TaskScheduler.Default));
    }

    /// <summary>
    ///     Runs the request on the UI thread and returns the message to send back, if any.
    /// </summary>
    private string Execute(Request request)
    {
        // Captured before running: "hello" changes the protocol of the session.
        bool legacy = ProtocolVersion == Versions.Legacy && LegacyActions.Contains(request.Action);

        object data;
        try
        {
            data = runtime.Commands.Execute(new CommandContext(runtime, this), request);
        }
        catch (CommandException error)
        {
            return legacy ? null : Json.Serialize(ResponseMessage.Failure(request.Id, request.Action, error.Code, error.Message));
        }
        catch (Exception error)
        {
            LsLog.Error(error);
            return legacy ? null : Json.Serialize(ResponseMessage.Failure(request.Id, request.Action, ErrorCodes.Internal, error.Message));
        }

        if (!legacy)
        {
            return Json.Serialize(ResponseMessage.Success(request, data));
        }

        switch (request.Action)
        {
            case "hi":
                return Json.Serialize(new { response = new { response = "hi" } });
            case "state":
                return Json.Serialize(new { response = new { response = "state" }, state = data });
            default:
                // Protocol version 1 never replied to control actions.
                return null;
        }
    }

    private void Reply(Task<string> message)
    {
        message.ContinueWith(task =>
        {
            if (task.Status == TaskStatus.RanToCompletion && task.Result != null)
            {
                SendText(task.Result);
            }
            else if (task.IsFaulted)
            {
                LsLog.Error(task.Exception.GetBaseException());
            }
        }, TaskScheduler.Default);
    }

    /// <summary>
    ///     Queues a text message without blocking the caller. Safe to call from any thread.
    /// </summary>
    internal void SendText(string message)
    {
        if (!IsOpen)
        {
            return;
        }

        try
        {
            SendAsync(message, null);
        }
        catch (InvalidOperationException)
        {
            // The connection closed in the meantime.
        }
    }
}
