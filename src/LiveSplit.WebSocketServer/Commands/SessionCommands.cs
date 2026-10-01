using LiveSplit.WsServer.Protocol;
using LiveSplit.WsServer.Server;
using LiveSplit.WsServer.State;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

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

        d.Register("subscribe", CommandAccess.Session,
            "Chooses what the session receives (protocol 2). Args: { events?: string[] | \"all\", includeState?: bool, "
            + "includeIcons?: bool, includeHistory?: bool, tickMs?: number (0 turns ticks off) }. Omitted args keep their value.",
            Subscribe);
        d.Register("unsubscribe", CommandAccess.Session,
            "Stops receiving some events, or every event and tick when no events are given (protocol 2). Args: { events?: string[] }.",
            Unsubscribe);
        d.Register("getsubscription", CommandAccess.Session, "The current subscription of the session.", (c, a) => DescribeSubscription(RequireV2(c)));
    }

    /// <summary>The smallest accepted tick interval.</summary>
    public const int MinimumTickMs = 50;

    private static SessionSubscription RequireV2(CommandContext c)
    {
        if (c.Session == null || c.Session.ProtocolVersion < ProtocolVersion.Current)
        {
            throw new CommandException(ErrorCodes.InvalidRequest,
                "Subscriptions need protocol version 2. Connect with ?protocol=2 or send { \"action\": \"hello\", \"args\": { \"protocol\": 2 } } first.");
        }

        return c.Session.Subscription;
    }

    private static ISet<string> ReadEvents(CommandArgs a)
    {
        IReadOnlyList<string> events = a.GetStringList("events");
        if (events == null)
        {
            return null;
        }

        string[] unknown = events.Where(x => !ServerEvents.All.Contains(x)).ToArray();
        if (unknown.Length > 0)
        {
            throw CommandException.InvalidArgs($"Unknown events: {string.Join(", ", unknown)}. Known events: {string.Join(", ", ServerEvents.All)}.");
        }

        return new HashSet<string>(events);
    }

    private static object Subscribe(CommandContext c, CommandArgs a)
    {
        SessionSubscription subscription = RequireV2(c);

        if (a.TryGet("events", 0, out JsonElement events) && events.ValueKind == JsonValueKind.String
            && string.Equals(events.GetString(), "all", StringComparison.OrdinalIgnoreCase))
        {
            subscription.Events = null;
        }
        else if (ReadEvents(a) is ISet<string> set)
        {
            subscription.Events = set;
        }

        subscription.IncludeState = a.GetBool("includeState") ?? subscription.IncludeState;
        subscription.StateOptions = new SnapshotOptions(
            IncludeIcons: a.GetBool("includeIcons") ?? subscription.StateOptions.IncludeIcons,
            IncludeHistory: a.GetBool("includeHistory") ?? subscription.StateOptions.IncludeHistory);

        int? tickMs = a.GetInt("tickMs");
        if (tickMs.HasValue)
        {
            subscription.TickInterval = tickMs.Value > 0
                ? TimeSpan.FromMilliseconds(Math.Max(tickMs.Value, MinimumTickMs))
                : null;
        }

        return DescribeSubscription(subscription);
    }

    private static object Unsubscribe(CommandContext c, CommandArgs a)
    {
        SessionSubscription subscription = RequireV2(c);
        ISet<string> events = ReadEvents(a);

        if (events == null)
        {
            subscription.Events = new HashSet<string>();
            subscription.TickInterval = null;
        }
        else
        {
            var remaining = new HashSet<string>(subscription.Events ?? ServerEvents.All);
            remaining.ExceptWith(events);
            subscription.Events = remaining;
        }

        return DescribeSubscription(subscription);
    }

    private static object DescribeSubscription(SessionSubscription subscription)
    {
        return new
        {
            events = (subscription.Events ?? ServerEvents.All).OrderBy(x => x).ToList(),
            includeState = subscription.IncludeState,
            includeIcons = subscription.StateOptions.IncludeIcons,
            includeHistory = subscription.StateOptions.IncludeHistory,
            tickMs = subscription.TickInterval.HasValue ? (int?)subscription.TickInterval.Value.TotalMilliseconds : null,
        };
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
