using System;

namespace LiveSplit.WsServer.Protocol;

/// <summary>
///     An expected failure that is reported to the client as <c>{ ok: false, error: { code, message } }</c>.
/// </summary>
public sealed class CommandException : Exception
{
    public string Code { get; }

    public CommandException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public static CommandException InvalidArgs(string message)
    {
        return new CommandException(ErrorCodes.InvalidArgs, message);
    }

    public static CommandException InvalidPhase(string message)
    {
        return new CommandException(ErrorCodes.InvalidPhase, message);
    }

    public static CommandException Unsupported(string message)
    {
        return new CommandException(ErrorCodes.Unsupported, message);
    }
}
