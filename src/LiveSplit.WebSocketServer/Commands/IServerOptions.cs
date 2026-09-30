namespace LiveSplit.WsServer.Commands;

/// <summary>
///     The settings that decide which commands a client may use.
/// </summary>
public interface IServerOptions
{
    bool ReadOnly { get; }

    bool AllowFileCommands { get; }
}
