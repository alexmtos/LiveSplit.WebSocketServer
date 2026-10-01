namespace LiveSplit.WsServer.Server;

/// <summary>
///     The parts of a client session that commands can change.
/// </summary>
public interface ISessionControl
{
    int ProtocolVersion { get; set; }

    SessionSubscription Subscription { get; }
}
