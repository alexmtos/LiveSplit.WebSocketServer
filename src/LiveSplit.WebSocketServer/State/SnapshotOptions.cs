namespace LiveSplit.WsServer.State;

/// <summary>
///     What a state snapshot contains besides the always present fields.
/// </summary>
/// <param name="IncludeIcons">Include the game and segment icons as PNG data URIs.</param>
/// <param name="IncludeHistory">Include the attempt history and the history of every segment.</param>
public readonly record struct SnapshotOptions(bool IncludeIcons, bool IncludeHistory)
{
    /// <summary>What protocol version 1 always sent.</summary>
    public static SnapshotOptions Legacy => new(IncludeIcons: true, IncludeHistory: false);

    /// <summary>Default for protocol version 2 clients.</summary>
    public static SnapshotOptions Default => new(IncludeIcons: false, IncludeHistory: false);
}
