using LiveSplit.WsServer.Protocol;
using LiveSplit.WsServer.State;
using System.Linq;

namespace LiveSplit.WsServer.Commands;

/// <summary>
///     Actions about the connection itself.
/// </summary>
public static class SessionCommands
{
    public static void Register(CommandDispatcher d)
    {
        d.Register("hi", CommandAccess.Session, "Replies without doing anything (protocol version 1).", (c, a) => null);

        d.Register("ping", CommandAccess.Session, "Replies with \"pong\".", (c, a) => "pong");

        d.Register("hello", CommandAccess.Session,
            "Switches the session to another protocol version. Args: { protocol: 2 }. Replies with the server information and the state.",
            Hello);

        d.Register("state", CommandAccess.Read,
            "Replies with the full state. Args: { includeIcons?: bool, includeHistory?: bool }.",
            (c, a) => c.Snapshots.Build(c.State, ReadSnapshotOptions(c, a)),
            "getstate");

        d.Register("help", CommandAccess.Session, "Lists every action.", Help, "getcommands");
    }

    public static HelloMessage CreateHello(CommandContext c, bool includeState)
    {
        int protocol = c.Session?.ProtocolVersion ?? ProtocolVersion.Current;
        return new HelloMessage
        {
            ProtocolVersion = protocol,
            ComponentVersion = c.Runtime.ComponentVersion,
            LiveSplitVersion = c.Runtime.LiveSplitVersion,
            ReadOnly = c.Options.ReadOnly,
            FileCommandsAllowed = c.Options.AllowFileCommands && !c.Options.ReadOnly,
            State = includeState
                ? c.Snapshots.Build(c.State, c.Session?.Subscription.StateOptions ?? SnapshotOptions.Default)
                : null,
        };
    }

    private static object Hello(CommandContext c, CommandArgs a)
    {
        int protocol = a.GetInt("protocol") ?? ProtocolVersion.Current;
        if (!ProtocolVersion.IsSupported(protocol))
        {
            throw CommandException.InvalidArgs($"Protocol version {protocol} is not supported. Supported versions: {ProtocolVersion.Legacy}, {ProtocolVersion.Current}.");
        }

        if (c.Session != null)
        {
            c.Session.ProtocolVersion = protocol;
        }

        return CreateHello(c, includeState: true);
    }

    private static SnapshotOptions ReadSnapshotOptions(CommandContext c, CommandArgs a)
    {
        SnapshotOptions defaults = c.Session?.ProtocolVersion == ProtocolVersion.Legacy
            ? SnapshotOptions.Legacy
            : c.Session?.Subscription.StateOptions ?? SnapshotOptions.Default;

        return new SnapshotOptions(
            IncludeIcons: a.GetBool("includeIcons") ?? defaults.IncludeIcons,
            IncludeHistory: a.GetBool("includeHistory") ?? defaults.IncludeHistory);
    }

    private static object Help(CommandContext c, CommandArgs a)
    {
        return c.Runtime.Commands.Definitions.Select(x => new
        {
            action = x.Name,
            aliases = x.Aliases,
            access = x.Access.ToString().ToLowerInvariant(),
            description = x.Description,
        }).ToList();
    }
}
