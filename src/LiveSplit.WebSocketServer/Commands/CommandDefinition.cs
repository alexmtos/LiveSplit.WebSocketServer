using LiveSplit.WsServer.Protocol;
using System;
using System.Collections.Generic;

namespace LiveSplit.WsServer.Commands;

/// <summary>
///     Handles an action and returns the response's <c>data</c> (or <see langword="null"/>).
///     Runs on LiveSplit's UI thread and reports expected failures with <see cref="CommandException"/>.
/// </summary>
public delegate object CommandHandler(CommandContext context, CommandArgs args);

public sealed class CommandDefinition
{
    public string Name { get; }
    public IReadOnlyList<string> Aliases { get; }
    public CommandAccess Access { get; }
    public string Description { get; }
    public CommandHandler Handler { get; }

    public CommandDefinition(string name, CommandAccess access, string description, CommandHandler handler, params string[] aliases)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Access = access;
        Description = description;
        Handler = handler ?? throw new ArgumentNullException(nameof(handler));
        Aliases = aliases ?? [];
    }
}
