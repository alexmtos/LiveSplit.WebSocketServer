namespace LiveSplit.WsServer.Commands;

public enum CommandAccess
{
    /// <summary>Only reads LiveSplit. Always allowed.</summary>
    Read,

    /// <summary>Only affects the client's own session (protocol, subscriptions). Always allowed.</summary>
    Session,

    /// <summary>Changes the timer, the run or LiveSplit's settings. Blocked in read only mode.</summary>
    Control,

    /// <summary>Reads or writes files, or replaces the splits or layout. Blocked in read only mode and unless file commands are allowed.</summary>
    File,
}
