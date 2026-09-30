using LiveSplit.Model;
using LiveSplit.WsServer.Interop;
using LiveSplit.WsServer.Server;
using LiveSplit.WsServer.State;

namespace LiveSplit.WsServer.Commands;

/// <summary>
///     Everything a command handler may use.
/// </summary>
public sealed class CommandContext
{
    public ServerRuntime Runtime { get; }

    /// <summary>
    ///     The session that sent the request, or <see langword="null"/> when there is none.
    /// </summary>
    public ISessionControl Session { get; }

    public LiveSplitState State => Runtime.State;
    public ITimerModel Model => Runtime.Model;
    public ITimerFormBridge Form => Runtime.Form;
    public IServerOptions Options => Runtime.Options;
    public StateSnapshotBuilder Snapshots => Runtime.Snapshots;

    public CommandContext(ServerRuntime runtime, ISessionControl session)
    {
        Runtime = runtime;
        Session = session;
    }
}
