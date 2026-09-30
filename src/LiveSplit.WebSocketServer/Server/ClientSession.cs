using LiveSplit.WsServer.Commands;
using LiveSplit.WsServer.Infrastructure;
using LiveSplit.WsServer.Protocol;
using LiveSplit.WsServer.State;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using WebSocketSharp;
using WebSocketSharp.Server;
using LsLog = LiveSplit.Options.Log;
using Versions = LiveSplit.WsServer.Protocol.ProtocolVersion;

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

    // Set on the UI thread once the greeting is queued; broadcasts (also on the UI thread) wait for it.
    private volatile bool greeted;

    private const int MaxQueuedMessages = 1000;
    private const int MaxQueuedBeforeDroppingTicks = 8;
    private readonly Queue<string> outgoing = new();
    private bool draining;

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

    /// <summary>
    ///     Whether the session is authorized, greeted and connected, so it can receive broadcasts.
    ///     Only meaningful on the UI thread.
    /// </summary>
    public bool IsOpen => authorized && greeted && State == WebSocketState.Open;

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

        // The greeting is queued on the UI thread, like broadcasts, so no event can overtake it.
        CompleteOnFailure(runtime.Dispatcher.InvokeAsync(() =>
        {
            string greeting = ProtocolVersion == Versions.Legacy
                ? Json.Serialize(new
                {
                    open = new { response = "success" },
                    state = runtime.Snapshots.Build(runtime.State, SnapshotOptions.Legacy),
                })
                : Json.Serialize(SessionCommands.CreateHello(new CommandContext(runtime, this), includeState: true));

            SendText(greeting);
            greeted = true;
            return true;
        }), null);
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

        // The response is queued on the UI thread, right after any event the action raised.
        CompleteOnFailure(runtime.Dispatcher.InvokeAsync(() =>
        {
            string response = Execute(request);
            if (response != null)
            {
                SendText(response);
            }

            return true;
        }), request);
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

    /// <summary>
    ///     Reports that the UI thread could not run the work (LiveSplit is closing).
    /// </summary>
    private void CompleteOnFailure(Task<bool> work, Request request)
    {
        work.ContinueWith(task =>
        {
            Exception error = task.Exception?.GetBaseException();
            if (error is not LiveSplitUnavailableException)
            {
                LsLog.Error(error);
            }

            if (request != null && !(ProtocolVersion == Versions.Legacy && LegacyActions.Contains(request.Action)))
            {
                SendText(Json.Serialize(ResponseMessage.Failure(request.Id, request.Action, ErrorCodes.Unavailable, "LiveSplit is not available.")));
            }
        }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
    }

    /// <summary>
    ///     Queues a text message. Messages are sent one at a time, in the order they were
    ///     queued, by a single worker. Safe to call from any thread and never blocks.
    /// </summary>
    /// <param name="droppable">
    ///     The message may be skipped when the client is not keeping up (ticks).
    /// </param>
    internal void SendText(string message, bool droppable = false)
    {
        if (!authorized || State != WebSocketState.Open)
        {
            return;
        }

        lock (outgoing)
        {
            if (droppable && outgoing.Count >= MaxQueuedBeforeDroppingTicks)
            {
                return;
            }

            if (outgoing.Count >= MaxQueuedMessages)
            {
                // The client stopped reading; do not let its messages pile up forever.
                outgoing.Clear();
                try
                {
                    Context.WebSocket.CloseAsync(CloseStatusCode.PolicyViolation, "Too many pending messages");
                }
                catch (Exception)
                {
                    // Already closing.
                }

                return;
            }

            outgoing.Enqueue(message);
            if (draining)
            {
                return;
            }

            draining = true;
        }

        ThreadPool.QueueUserWorkItem(_ => Drain());
    }

    private void Drain()
    {
        while (true)
        {
            string message;
            lock (outgoing)
            {
                if (outgoing.Count == 0)
                {
                    draining = false;
                    return;
                }

                message = outgoing.Dequeue();
            }

            try
            {
                if (State == WebSocketState.Open)
                {
                    Send(message);
                }
            }
            catch (Exception)
            {
                // The connection closed in the meantime.
            }
        }
    }
}
