using LiveSplit.WsServer.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LiveSplit.WsServer.Commands;

/// <summary>
///     Maps action names (and aliases) to their handlers and enforces the access rules.
/// </summary>
public sealed class CommandDispatcher
{
    private readonly Dictionary<string, CommandDefinition> commands = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<CommandDefinition> definitions = [];

    public IReadOnlyList<CommandDefinition> Definitions => definitions;

    public void Register(CommandDefinition definition)
    {
        foreach (string name in new[] { definition.Name }.Concat(definition.Aliases))
        {
            if (commands.ContainsKey(name))
            {
                throw new InvalidOperationException($"The action '{name}' is registered twice.");
            }

            commands.Add(name, definition);
        }

        definitions.Add(definition);
    }

    public void Register(string name, CommandAccess access, string description, CommandHandler handler, params string[] aliases)
    {
        Register(new CommandDefinition(name, access, description, handler, aliases));
    }

    public bool TryGet(string action, out CommandDefinition definition)
    {
        return commands.TryGetValue(action ?? "", out definition);
    }

    /// <summary>
    ///     Runs the request. Must be called on LiveSplit's UI thread.
    /// </summary>
    /// <exception cref="CommandException">The request is not allowed or failed in an expected way.</exception>
    public object Execute(CommandContext context, Request request)
    {
        if (!TryGet(request.Action, out CommandDefinition definition))
        {
            throw new CommandException(ErrorCodes.UnknownAction, $"Unknown action '{request.Action}'. Send 'help' for the list of actions.");
        }

        CheckAccess(context.Options, definition);
        return definition.Handler(context, request.Args);
    }

    public static void CheckAccess(IServerOptions options, CommandDefinition definition)
    {
        if ((definition.Access is CommandAccess.Control or CommandAccess.File) && options.ReadOnly)
        {
            throw new CommandException(ErrorCodes.ReadOnly, $"'{definition.Name}' is not allowed because the server is in read only mode.");
        }

        if (definition.Access == CommandAccess.File && !options.AllowFileCommands)
        {
            throw new CommandException(ErrorCodes.Forbidden, $"'{definition.Name}' is not allowed because file commands are disabled in the component settings.");
        }
    }

    /// <summary>
    ///     Creates a dispatcher with every built-in action.
    /// </summary>
    public static CommandDispatcher CreateDefault()
    {
        var dispatcher = new CommandDispatcher();
        SessionCommands.Register(dispatcher);
        TimerCommands.Register(dispatcher);
        RunCommands.Register(dispatcher);
        HotkeyCommands.Register(dispatcher);
        FileCommands.Register(dispatcher);
        QueryCommands.Register(dispatcher);
        return dispatcher;
    }
}
