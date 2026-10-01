namespace LiveSplit.WsServer.Protocol;

public static class ProtocolVersion
{
    /// <summary>
    ///     The original protocol: legacy action names, <c>{ action: { action, data }, state }</c>
    ///     broadcasts and no replies to control actions. Default for backwards compatibility.
    /// </summary>
    public const int Legacy = 1;

    /// <summary>
    ///     Request ids, typed responses with error codes, subscriptions and ticks.
    /// </summary>
    public const int Current = 2;

    public static bool IsSupported(int version)
    {
        return version is Legacy or Current;
    }
}
