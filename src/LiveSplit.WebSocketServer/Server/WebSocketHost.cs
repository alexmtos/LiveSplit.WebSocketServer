using LiveSplit.Model;
using LiveSplit.Options;
using LiveSplit.WsServer.Protocol;
using LiveSplit.WsServer.State;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using WebSocketSharp.Server;
using Versions = LiveSplit.WsServer.Protocol.ProtocolVersion;

namespace LiveSplit.WsServer.Server;

/// <summary>
///     Owns the websocket-sharp server and sends events to the connected clients.
/// </summary>
public sealed class WebSocketHost : IDisposable
{
    public const string ServicePath = "/";

    private readonly ServerRuntime runtime;
    private WebSocketServer server;

    public WebSocketHost(ServerRuntime runtime)
    {
        this.runtime = runtime;
    }

    public bool IsRunning => server != null;

    public ServerRuntime Runtime => runtime;

    /// <exception cref="Exception">The server could not be started, for example because the port is in use.</exception>
    public void Start(IPAddress address, int port)
    {
        Stop();

        var newServer = new WebSocketServer(address, port);
        newServer.AddWebSocketService(ServicePath, () => new ClientSession(runtime));
        newServer.Start();
        server = newServer;
    }

    public void Stop()
    {
        WebSocketServer oldServer = server;
        server = null;
        if (oldServer == null)
        {
            return;
        }

        try
        {
            oldServer.Stop();
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
    }

    public IReadOnlyList<ClientSession> Sessions
    {
        get
        {
            WebSocketServer current = server;
            if (current == null || !current.WebSocketServices.TryGetServiceHost(ServicePath, out WebSocketServiceHost host))
            {
                return [];
            }

            try
            {
                return host.Sessions.Sessions.OfType<ClientSession>().Where(x => x.IsOpen).ToList();
            }
            catch (InvalidOperationException)
            {
                // The server is stopping.
                return [];
            }
        }
    }

    /// <summary>
    ///     Sends an event to every interested client. Must be called on the UI thread.
    /// </summary>
    public void Broadcast(string eventName, object data)
    {
        IReadOnlyList<ClientSession> sessions = Sessions;
        if (sessions.Count == 0)
        {
            return;
        }

        var snapshots = new Dictionary<SnapshotOptions, StateSnapshot>();
        StateSnapshot Snapshot(SnapshotOptions options)
        {
            if (!snapshots.TryGetValue(options, out StateSnapshot snapshot))
            {
                snapshot = runtime.Snapshots.Build(runtime.State, options);
                snapshots.Add(options, snapshot);
            }

            return snapshot;
        }

        string legacyMessage = null;
        // Keyed by what the message contains: with or without state, and which state.
        var messages = new Dictionary<(bool IncludeState, SnapshotOptions Options), string>();

        foreach (ClientSession session in sessions)
        {
            string message;
            if (session.ProtocolVersion == Versions.Legacy)
            {
                if (!ServerEvents.Legacy.Contains(eventName))
                {
                    continue;
                }

                message = legacyMessage ??= Json.Serialize(new
                {
                    action = new { action = eventName, data },
                    state = Snapshot(SnapshotOptions.Legacy),
                });
            }
            else
            {
                SessionSubscription subscription = session.Subscription;
                if (!subscription.Wants(eventName))
                {
                    continue;
                }

                (bool IncludeState, SnapshotOptions Options) key = subscription.IncludeState
                    ? (true, subscription.StateOptions)
                    : (false, default);
                if (!messages.TryGetValue(key, out message))
                {
                    message = Json.Serialize(new EventMessage
                    {
                        Event = eventName,
                        Data = data,
                        State = key.IncludeState ? Snapshot(key.Options) : null,
                    });
                    messages.Add(key, message);
                }
            }

            session.SendText(message);
        }
    }

    /// <summary>
    ///     Sends a tick to every client whose tick interval has elapsed. Called every frame on the UI thread.
    /// </summary>
    public void SendTicks(DateTime now)
    {
        string message = null;
        foreach (ClientSession session in Sessions)
        {
            if (session.ProtocolVersion == Versions.Legacy || session.Subscription.TickInterval is not TimeSpan interval)
            {
                continue;
            }

            if (now - session.LastTick < interval)
            {
                continue;
            }

            session.LastTick = now;
            message ??= Json.Serialize(BuildTick());
            session.SendText(message, droppable: true);
        }
    }

    private TickMessage BuildTick()
    {
        LiveSplitState state = runtime.State;
        TimeSpan? delta = null;
        try
        {
            delta = TimerCalculations.Delta(state, state.CurrentComparison);
        }
        catch (Exception)
        {
            // The delta is optional.
        }

        return new TickMessage
        {
            TimerState = state.CurrentPhase.ToString(),
            CurrentTime = TimeDto.From(state.CurrentTime),
            CurrentSplitIndex = state.CurrentSplitIndex,
            CurrentDelta = TimeDto.Milliseconds(delta),
            IsGameTimePaused = state.IsGameTimePaused,
            LoadingTimes = TimeDto.Milliseconds(state.LoadingTimes),
        };
    }

    public void Dispose()
    {
        Stop();
    }
}
