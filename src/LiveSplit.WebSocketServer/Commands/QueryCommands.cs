using LiveSplit.Model;
using LiveSplit.WsServer.Protocol;
using LiveSplit.WsServer.State;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace LiveSplit.WsServer.Commands;

/// <summary>
///     Read only actions. They mirror the "get" commands of LiveSplit's built-in server, but return
///     times as milliseconds and missing values as null instead of formatted strings and "-".
/// </summary>
public static class QueryCommands
{
    public static void Register(CommandDispatcher d)
    {
        // Timer
        d.Register("gettimerphase", CommandAccess.Read, "The timer phase: NotRunning, Running, Paused or Ended.", (c, a) => c.State.CurrentPhase.ToString(), "getcurrenttimerphase");
        d.Register("getcurrenttime", CommandAccess.Read, "The current time with the current timing method (real time while game time is not initialized).",
            (c, a) => Ms(TimerCalculations.CurrentTime(c.State, TimerCalculations.EffectiveTimingMethod(c.State))));
        d.Register("getcurrentrealtime", CommandAccess.Read, "The current real time.", (c, a) => Ms(TimerCalculations.CurrentTime(c.State, TimingMethod.RealTime)));
        d.Register("getcurrentgametime", CommandAccess.Read, "The current game time (real time while game time is not initialized).",
            (c, a) => Ms(TimerCalculations.CurrentTime(c.State, c.State.IsGameTimeInitialized ? TimingMethod.GameTime : TimingMethod.RealTime)));
        d.Register("getdelta", CommandAccess.Read, "The delta of the last split. Args: { comparison? }.", (c, a) => Ms(TimerCalculations.Delta(c.State, Comparison(c, a))));
        d.Register("getpredictedtime", CommandAccess.Read, "The predicted final time. Args: { comparison? }.", (c, a) => Ms(TimerCalculations.PredictedTime(c.State, Comparison(c, a))));
        d.Register("getbestpossibletime", CommandAccess.Read, "The best possible final time.", (c, a) => Ms(TimerCalculations.BestPossibleTime(c.State)));
        d.Register("getfinaltime", CommandAccess.Read, "The final time of the run, or of the comparison while the run is not finished. Args: { comparison? }.",
            (c, a) => Ms(TimerCalculations.FinalTime(c.State, Comparison(c, a))), "getfinalsplittime");
        d.Register("getpausedrealtime", CommandAccess.Read, "The total pause time of the current attempt.", (c, a) => Ms(c.State.PauseTime), "getpausetime");
        d.Register("getpausedgametime", CommandAccess.Read, "The game time at which game time was paused.", (c, a) => Ms(c.State.GameTimePauseTime));
        d.Register("getloadingtimes", CommandAccess.Read, "The loading times (real time minus game time).", (c, a) => Ms(c.State.LoadingTimes));
        d.Register("isgametimepaused", CommandAccess.Read, "Whether game time is paused.", (c, a) => c.State.IsGameTimePaused);
        d.Register("isgametimeinitialized", CommandAccess.Read, "Whether game time is initialized.", (c, a) => c.State.IsGameTimeInitialized);
        d.Register("getoffset", CommandAccess.Read, "The start offset of the run.", (c, a) => Ms(c.State.Run.Offset));
        d.Register("gettimingmethod", CommandAccess.Read, "The current timing method: RealTime or GameTime.", (c, a) => c.State.CurrentTimingMethod.ToString());
        d.Register("getcomparisonname", CommandAccess.Read, "The current comparison.", (c, a) => c.State.CurrentComparison, "getcomparison");
        d.Register("getcomparisons", CommandAccess.Read, "All comparisons.", (c, a) => c.State.Run.Comparisons.ToList());

        // Splits
        d.Register("getsplitindex", CommandAccess.Read, "The index of the current split (-1 while not running).", (c, a) => c.State.CurrentSplitIndex);
        d.Register("getsplitcount", CommandAccess.Read, "The number of splits.", (c, a) => c.State.Run.Count);
        d.Register("getsplitname", CommandAccess.Read, "The name of a split. Args: { index } (negative indices count from the end).",
            (c, a) => SplitName(c, a.GetInt("index") ?? c.State.CurrentSplitIndex));
        d.Register("getcurrentsplitname", CommandAccess.Read, "The name of the current split.", (c, a) => c.State.CurrentSplit?.Name);
        d.Register("getprevioussplitname", CommandAccess.Read, "The name of the previous split.",
            (c, a) => c.State.CurrentSplitIndex > 0 ? c.State.Run[c.State.CurrentSplitIndex - 1].Name : null, "getlastsplitname");
        d.Register("getnextsplitname", CommandAccess.Read, "The name of the next split.",
            (c, a) => c.State.CurrentSplitIndex < c.State.Run.Count - 1 ? c.State.Run[c.State.CurrentSplitIndex + 1].Name : null, "getupcomingsplitname");
        d.Register("getprevioussplittime", CommandAccess.Read, "The split time of the previous split.",
            (c, a) => c.State.CurrentSplitIndex > 0 ? Ms(c.State.Run[c.State.CurrentSplitIndex - 1].SplitTime[c.State.CurrentTimingMethod]) : null,
            "getlastsplittime");
        d.Register("getcomparisonsplittime", CommandAccess.Read, "The comparison time of the current split. Args: { comparison? }.",
            (c, a) => c.State.CurrentSplit != null ? Ms(c.State.CurrentSplit.Comparisons[Comparison(c, a)][c.State.CurrentTimingMethod]) : null,
            "getcurrentsplittime");
        d.Register("getsegment", CommandAccess.Read, "Everything about one split. Args: { index }.", GetSegment);

        // Run
        d.Register("getgamename", CommandAccess.Read, "The game name.", (c, a) => c.State.Run.GameName);
        d.Register("getcategoryname", CommandAccess.Read, "The category name.", (c, a) => c.State.Run.CategoryName);
        d.Register("getcategoryvariables", CommandAccess.Read, "Region, platform, emulator and speedrun.com variables of the run.", GetCategoryVariables);
        d.Register("getattemptcount", CommandAccess.Read, "The number of attempts.", (c, a) => c.State.Run.AttemptCount);
        d.Register("getcompletedcount", CommandAccess.Read, "The number of finished attempts.", (c, a) => TimerCalculations.FinishedCount(c.State.Run));
        d.Register("getcustomvariablevalue", CommandAccess.Read, "The value of a custom variable. Args: { name }.",
            (c, a) => c.State.Run.Metadata.CustomVariableValue(a.RequireString("name")));
        d.Register("getcustomvariables", CommandAccess.Read, "All custom variables.",
            (c, a) => c.State.Run.Metadata.CustomVariables.ToDictionary(x => x.Key, x => x.Value.Value));
        d.Register("getsplitspath", CommandAccess.Read, "The path of the splits file.", (c, a) => NullIfEmpty(c.State.Run.FilePath));
        d.Register("getlayoutpath", CommandAccess.Read, "The path of the layout file.", (c, a) => NullIfEmpty(c.State.Layout?.FilePath));
        d.Register("getautosplitterpath", CommandAccess.Read, "The path of the auto splitter of the run.", GetAutoSplitterPath);
        d.Register("autosplitteractivated", CommandAccess.Read, "Whether the auto splitter of the run is activated.",
            (c, a) => c.State.Run.AutoSplitter != null && c.State.Run.AutoSplitter.IsActivated);

        // Hotkeys
        d.Register("gethotkeyprofile", CommandAccess.Read, "The current hotkey profile.", (c, a) => c.State.CurrentHotkeyProfile);
        d.Register("gethotkeyprofiles", CommandAccess.Read, "All hotkey profiles.", (c, a) => c.State.Settings.HotkeyProfiles.Keys.ToList());
        d.Register("globalhotkeysenabled", CommandAccess.Read, "Whether global hotkeys are enabled.",
            (c, a) => StateSnapshotBuilder.CurrentHotkeyProfile(c.State)?.GlobalHotkeysEnabled);

        // LiveSplit
        d.Register("getlivesplitversion", CommandAccess.Read, "The LiveSplit version.", (c, a) => c.Runtime.LiveSplitVersion);
        d.Register("getlivesplitpath", CommandAccess.Read, "The path of LiveSplit.exe.", (c, a) => Assembly.GetEntryAssembly()?.Location);
        d.Register("getcomponentversion", CommandAccess.Read, "The version of this component.", (c, a) => c.Runtime.ComponentVersion);
        d.Register("getservertype", CommandAccess.Read, "Always \"WebSocketJson\" (the built-in server replies TCP or Websocket).", (c, a) => "WebSocketJson");
    }

    private static long? Ms(TimeSpan? time)
    {
        return TimeDto.Milliseconds(time);
    }

    private static string NullIfEmpty(string value)
    {
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static string Comparison(CommandContext c, CommandArgs a)
    {
        return a.GetString("comparison") ?? c.State.CurrentComparison;
    }

    private static int RequireSplitIndex(CommandContext c, int index)
    {
        int resolved = TimerCalculations.ResolveSplitIndex(c.State, index);
        if (resolved < 0 || resolved >= c.State.Run.Count)
        {
            throw CommandException.InvalidArgs($"Split index {index} is out of range (the run has {c.State.Run.Count} splits).");
        }

        return resolved;
    }

    private static object SplitName(CommandContext c, int index)
    {
        return c.State.Run[RequireSplitIndex(c, index)].Name;
    }

    private static object GetSegment(CommandContext c, CommandArgs a)
    {
        int index = RequireSplitIndex(c, a.GetInt("index") ?? c.State.CurrentSplitIndex);
        ISegment segment = c.State.Run[index];
        return new
        {
            index,
            name = segment.Name,
            splitTime = TimeDto.From(segment.SplitTime),
            personalBest = TimeDto.From(segment.PersonalBestSplitTime),
            bestSegment = TimeDto.From(segment.BestSegmentTime),
            comparisons = segment.Comparisons.ToDictionary(x => x.Key, x => TimeDto.From(x.Value)),
            customVariables = segment.CustomVariableValues,
        };
    }

    private static object GetCategoryVariables(CommandContext c, CommandArgs a)
    {
        RunMetadata metadata = c.State.Run.Metadata;
        return new
        {
            region = NullIfEmpty(metadata.RegionName),
            platform = NullIfEmpty(metadata.PlatformName),
            usesEmulator = metadata.UsesEmulator,
            variables = metadata.VariableValueNames != null
                ? new Dictionary<string, string>(metadata.VariableValueNames)
                : new Dictionary<string, string>(),
        };
    }

    private static object GetAutoSplitterPath(CommandContext c, CommandArgs a)
    {
        try
        {
            return NullIfEmpty(c.State.Run.AutoSplitter?.LocalPath);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
