using System.Collections.Generic;

namespace LiveSplit.WsServer.Commands;

/// <summary>
///     The settings that decide who may connect and which commands they may use.
/// </summary>
public interface IServerOptions
{
    bool ReadOnly { get; }

    bool AllowFileCommands { get; }

    /// <summary>
    ///     When not empty, clients must connect with <c>?token=...</c>.
    /// </summary>
    string AuthToken { get; }

    /// <summary>
    ///     Origins browsers may connect from. Empty allows every origin.
    /// </summary>
    IReadOnlyCollection<string> AllowedOriginList { get; }
}
