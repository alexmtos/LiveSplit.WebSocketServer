namespace LiveSplit.WsServer.Protocol;

/// <summary>
///     Machine readable error codes sent in <c>error.code</c>.
/// </summary>
public static class ErrorCodes
{
    /// <summary>The message could not be understood (for example invalid JSON or a missing action).</summary>
    public const string InvalidRequest = "invalid_request";

    /// <summary>The action does not exist.</summary>
    public const string UnknownAction = "unknown_action";

    /// <summary>An argument is missing or invalid.</summary>
    public const string InvalidArgs = "invalid_args";

    /// <summary>The server is in read only mode and the action would change LiveSplit.</summary>
    public const string ReadOnly = "read_only";

    /// <summary>The action is not possible in the current timer phase.</summary>
    public const string InvalidPhase = "invalid_phase";

    /// <summary>The action is disabled in the component settings.</summary>
    public const string Forbidden = "forbidden";

    /// <summary>The running LiveSplit version does not support the action.</summary>
    public const string Unsupported = "unsupported";

    /// <summary>The client did not provide a valid token.</summary>
    public const string Unauthorized = "unauthorized";

    /// <summary>LiveSplit is not available (for example while closing).</summary>
    public const string Unavailable = "unavailable";

    /// <summary>The action failed unexpectedly.</summary>
    public const string Internal = "internal";
}
