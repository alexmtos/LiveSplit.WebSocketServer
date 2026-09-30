using LiveSplit.Model;
using LiveSplit.WsServer.Protocol;

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
        d.Register("reset", CommandAccess.Control, "Resets the timer.", Reset);
        d.Register("pausegametime", CommandAccess.Control, "Pauses game time.", PauseGameTime);
        d.Register("unpausegametime", CommandAccess.Control, "Resumes game time (and cancels 'alwayspausegametime').", UnpauseGameTime);
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

    private static object Reset(CommandContext c, CommandArgs a)
    {
        RequirePhase(c.State, "reset", TimerPhase.Running, TimerPhase.Paused, TimerPhase.Ended);
        c.Model.Reset();
        return TimerResult(c.State);
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
}
