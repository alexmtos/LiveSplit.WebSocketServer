using LiveSplit.Model;
using LiveSplit.WsServer.Protocol;
using LiveSplit.WsServer.State;
using System;
using System.Linq;

namespace LiveSplit.WsServer.Commands;

/// <summary>
///     Actions that drive the timer.
/// </summary>
public static class TimerCommands
{
    public static void Register(CommandDispatcher d)
    {
        d.Register("start", CommandAccess.Control, "Starts the timer.", Start, "starttimer");
        d.Register("split", CommandAccess.Control, "Splits.", Split);
        d.Register("startorsplit", CommandAccess.Control, "Starts the timer if it is not running, splits otherwise.", StartOrSplit);
        d.Register("undosplit", CommandAccess.Control, "Undoes the last split.", UndoSplit, "unsplit");
        d.Register("skipsplit", CommandAccess.Control, "Skips the current split.", SkipSplit);
        d.Register("pause", CommandAccess.Control, "Pauses the timer. Does nothing if it is already paused.", Pause);
        d.Register("resume", CommandAccess.Control, "Resumes the timer. Does nothing if it is already running.", Resume);
        d.Register("togglepause", CommandAccess.Control, "Pauses the timer if it is running, resumes it if it is paused.", TogglePause);
        d.Register("undoallpauses", CommandAccess.Control, "Removes all pause time from the current attempt.", UndoAllPauses);
        d.Register("reset", CommandAccess.Control,
            "Resets the timer. Args: { save?: bool } - with save: false the times of this attempt are discarded (default true).", Reset);
        d.Register("resetandsetattemptaspb", CommandAccess.Control, "Resets the timer and saves the attempt as the personal best.", ResetAndSetAttemptAsPB);

        d.Register("initgametime", CommandAccess.Control, "Initializes game time.", InitGameTime);
        d.Register("setgametime", CommandAccess.Control, "Sets game time. Args: { time } (milliseconds or \"1:23.456\").", SetGameTime);
        d.Register("setloadingtimes", CommandAccess.Control, "Sets the loading times. Args: { time }.", SetLoadingTimes);
        d.Register("addloadingtimes", CommandAccess.Control, "Adds to the loading times. Args: { time }.", AddLoadingTimes);
        d.Register("pausegametime", CommandAccess.Control, "Pauses game time.", PauseGameTime);
        d.Register("unpausegametime", CommandAccess.Control, "Resumes game time (and cancels 'alwayspausegametime').", UnpauseGameTime);
        d.Register("alwayspausegametime", CommandAccess.Control, "Pauses game time now and every time the timer starts, until 'unpausegametime'.", AlwaysPauseGameTime);

        d.Register("setcomparison", CommandAccess.Control, "Changes the current comparison. Args: { comparison }.", SetComparison);
        d.Register("switchcomparisonnext", CommandAccess.Control, "Switches to the next comparison.", (c, a) =>
        {
            c.Model.SwitchComparisonNext();
            return new { comparison = c.State.CurrentComparison };
        }, "nextcomparison");
        d.Register("switchcomparisonprevious", CommandAccess.Control, "Switches to the previous comparison.", (c, a) =>
        {
            c.Model.SwitchComparisonPrevious();
            return new { comparison = c.State.CurrentComparison };
        }, "previouscomparison");
        d.Register("settimingmethod", CommandAccess.Control, "Changes the timing method. Args: { method: \"realtime\" | \"gametime\" }.", SetTimingMethod, "switchto");

        d.Register("scrollup", CommandAccess.Control, "Scrolls the splits up.", (c, a) =>
        {
            c.Model.ScrollUp();
            return null;
        });
        d.Register("scrolldown", CommandAccess.Control, "Scrolls the splits down.", (c, a) =>
        {
            c.Model.ScrollDown();
            return null;
        });
    }

    /// <summary>
    ///     The data returned by the actions that change the timer.
    /// </summary>
    public static object TimerResult(LiveSplitState state)
    {
        return new
        {
            timerState = state.CurrentPhase.ToString(),
            currentSplitIndex = state.CurrentSplitIndex,
        };
    }

    private static void RequirePhase(LiveSplitState state, string action, params TimerPhase[] phases)
    {
        foreach (TimerPhase phase in phases)
        {
            if (state.CurrentPhase == phase)
            {
                return;
            }
        }

        throw CommandException.InvalidPhase($"'{action}' is not possible while the timer is {state.CurrentPhase}.");
    }

    private static object Start(CommandContext c, CommandArgs a)
    {
        RequirePhase(c.State, "start", TimerPhase.NotRunning);
        c.Model.Start();
        return TimerResult(c.State);
    }

    private static object Split(CommandContext c, CommandArgs a)
    {
        RequirePhase(c.State, "split", TimerPhase.Running);
        c.Model.Split();
        return TimerResult(c.State);
    }

    private static object StartOrSplit(CommandContext c, CommandArgs a)
    {
        if (c.State.CurrentPhase == TimerPhase.Running)
        {
            c.Model.Split();
        }
        else if (c.State.CurrentPhase == TimerPhase.NotRunning)
        {
            c.Model.Start();
        }
        else
        {
            throw CommandException.InvalidPhase($"'startorsplit' is not possible while the timer is {c.State.CurrentPhase}.");
        }

        return TimerResult(c.State);
    }

    private static object UndoSplit(CommandContext c, CommandArgs a)
    {
        if (c.State.CurrentPhase == TimerPhase.NotRunning || c.State.CurrentSplitIndex <= 0)
        {
            throw CommandException.InvalidPhase("There is no split to undo.");
        }

        c.Model.UndoSplit();
        return TimerResult(c.State);
    }

    private static object SkipSplit(CommandContext c, CommandArgs a)
    {
        RequirePhase(c.State, "skipsplit", TimerPhase.Running, TimerPhase.Paused);
        if (c.State.CurrentSplitIndex >= c.State.Run.Count - 1)
        {
            throw CommandException.InvalidPhase("The last split cannot be skipped.");
        }

        c.Model.SkipSplit();
        return TimerResult(c.State);
    }

    private static object Pause(CommandContext c, CommandArgs a)
    {
        RequirePhase(c.State, "pause", TimerPhase.Running, TimerPhase.Paused);
        if (c.State.CurrentPhase == TimerPhase.Running)
        {
            c.Model.Pause();
        }

        return TimerResult(c.State);
    }

    private static object Resume(CommandContext c, CommandArgs a)
    {
        RequirePhase(c.State, "resume", TimerPhase.Running, TimerPhase.Paused);
        if (c.State.CurrentPhase == TimerPhase.Paused)
        {
            // ITimerModel has no Resume: Pause toggles.
            c.Model.Pause();
        }

        return TimerResult(c.State);
    }

    private static object TogglePause(CommandContext c, CommandArgs a)
    {
        RequirePhase(c.State, "togglepause", TimerPhase.Running, TimerPhase.Paused);
        c.Model.Pause();
        return TimerResult(c.State);
    }

    private static object UndoAllPauses(CommandContext c, CommandArgs a)
    {
        RequirePhase(c.State, "undoallpauses", TimerPhase.Running, TimerPhase.Paused, TimerPhase.Ended);
        c.Model.UndoAllPauses();
        return TimerResult(c.State);
    }

    private static object Reset(CommandContext c, CommandArgs a)
    {
        RequirePhase(c.State, "reset", TimerPhase.Running, TimerPhase.Paused, TimerPhase.Ended);
        bool save = a.GetBool("save") ?? true;
        c.Model.Reset(save);
        return TimerResult(c.State);
    }

    private static object ResetAndSetAttemptAsPB(CommandContext c, CommandArgs a)
    {
        RequirePhase(c.State, "resetandsetattemptaspb", TimerPhase.Running, TimerPhase.Paused, TimerPhase.Ended);
        c.Model.ResetAndSetAttemptAsPB();
        return TimerResult(c.State);
    }

    private static object GameTimeResult(LiveSplitState state)
    {
        return new
        {
            gameTime = TimeDto.Milliseconds(state.CurrentTime.GameTime),
            loadingTimes = TimeDto.Milliseconds(state.LoadingTimes),
            isGameTimeInitialized = state.IsGameTimeInitialized,
            isGameTimePaused = state.IsGameTimePaused,
        };
    }

    private static object InitGameTime(CommandContext c, CommandArgs a)
    {
        c.Model.InitializeGameTime();
        return GameTimeResult(c.State);
    }

    private static object SetGameTime(CommandContext c, CommandArgs a)
    {
        TimeSpan time = a.RequireTime("time");
        if (c.State.CurrentPhase == TimerPhase.NotRunning)
        {
            throw CommandException.InvalidPhase("Game time can only be set while the timer is running.");
        }

        c.State.SetGameTime(time);
        return GameTimeResult(c.State);
    }

    private static object SetLoadingTimes(CommandContext c, CommandArgs a)
    {
        c.State.LoadingTimes = a.GetTime("time") ?? TimeSpan.Zero;
        return GameTimeResult(c.State);
    }

    private static object AddLoadingTimes(CommandContext c, CommandArgs a)
    {
        c.State.LoadingTimes += a.RequireTime("time");
        return GameTimeResult(c.State);
    }

    private static object PauseGameTime(CommandContext c, CommandArgs a)
    {
        c.State.IsGameTimePaused = true;
        return null;
    }

    private static object UnpauseGameTime(CommandContext c, CommandArgs a)
    {
        c.Runtime.AlwaysPauseGameTime = false;
        c.State.IsGameTimePaused = false;
        return null;
    }

    private static object AlwaysPauseGameTime(CommandContext c, CommandArgs a)
    {
        c.Runtime.AlwaysPauseGameTime = true;
        c.State.IsGameTimePaused = true;
        return null;
    }

    private static object SetComparison(CommandContext c, CommandArgs a)
    {
        string comparison = a.RequireString("comparison");
        string match = c.State.Run.Comparisons.FirstOrDefault(x => x == comparison)
            ?? c.State.Run.Comparisons.FirstOrDefault(x => string.Equals(x, comparison, StringComparison.OrdinalIgnoreCase))
            ?? throw CommandException.InvalidArgs($"Unknown comparison '{comparison}'. Available: {string.Join(", ", c.State.Run.Comparisons)}.");

        c.State.CurrentComparison = match;
        return new { comparison = match };
    }

    private static object SetTimingMethod(CommandContext c, CommandArgs a)
    {
        string method = a.RequireString("method").Trim().ToLowerInvariant();
        c.State.CurrentTimingMethod = method switch
        {
            "realtime" or "real" or "rta" => TimingMethod.RealTime,
            "gametime" or "game" or "igt" => TimingMethod.GameTime,
            _ => throw CommandException.InvalidArgs("'method' must be \"realtime\" or \"gametime\"."),
        };

        return new { timingMethod = c.State.CurrentTimingMethod.ToString() };
    }
}
