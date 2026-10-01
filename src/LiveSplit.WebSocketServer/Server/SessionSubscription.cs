using LiveSplit.WsServer.State;
using System;
using System.Collections.Generic;

namespace LiveSplit.WsServer.Server;

/// <summary>
///     What a protocol version 2 client wants to receive.
/// </summary>
public sealed class SessionSubscription
{
    /// <summary>
    ///     The events to receive, or <see langword="null"/> for all of them.
    /// </summary>
    public ISet<string> Events { get; set; }

    /// <summary>
    ///     Whether events carry the full state.
    /// </summary>
    public bool IncludeState { get; set; } = true;

    public SnapshotOptions StateOptions { get; set; } = SnapshotOptions.Default;

    /// <summary>
    ///     How often to send <c>tick</c> messages, or <see langword="null"/> for never.
    /// </summary>
    public TimeSpan? TickInterval { get; set; }

    public bool Wants(string eventName)
    {
        return Events == null || Events.Contains(eventName);
    }
}
