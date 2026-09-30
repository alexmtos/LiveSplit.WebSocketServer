using LiveSplit.Model;
using LiveSplit.WsServer.Commands;
using LiveSplit.WsServer.Infrastructure;
using LiveSplit.WsServer.Interop;
using LiveSplit.WsServer.State;

namespace LiveSplit.WsServer.Server;

/// <summary>
///     State shared by all sessions of a server.
/// </summary>
public sealed class ServerRuntime
{
    public LiveSplitState State { get; }
    public ITimerModel Model { get; }
    public IUiDispatcher Dispatcher { get; }
    public ITimerFormBridge Form { get; }
    public IServerOptions Options { get; }
    public CommandDispatcher Commands { get; }
    public StateSnapshotBuilder Snapshots { get; } = new();

    public string ComponentVersion { get; }

    /// <summary>
    ///     Set by <c>alwayspausegametime</c>: game time is paused again whenever the timer starts.
    /// </summary>
    public bool AlwaysPauseGameTime { get; set; }

    public ServerRuntime(LiveSplitState state, ITimerModel model, IUiDispatcher dispatcher, ITimerFormBridge form, IServerOptions options, CommandDispatcher commands, string componentVersion)
    {
        State = state;
        Model = model;
        Dispatcher = dispatcher;
        Form = form;
        Options = options;
        Commands = commands;
        ComponentVersion = componentVersion;
    }

    public string LiveSplitVersion
    {
        get
        {
            try
            {
                return Updates.Git.Version ?? "Unknown";
            }
            catch (System.Exception)
            {
                return "Unknown";
            }
        }
    }
}
