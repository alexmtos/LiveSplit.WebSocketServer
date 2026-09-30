using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using System;
using System.Linq;

namespace LiveSplit.WsServer.State;

/// <summary>
///     Derived timer values. The formulas match LiveSplit's built-in server so both report the same numbers.
/// </summary>
public static class TimerCalculations
{
    /// <summary>
    ///     The timing method to report for "current" times: game time falls back to real time
    ///     while game time is not initialized.
    /// </summary>
    public static TimingMethod EffectiveTimingMethod(LiveSplitState state)
    {
        return state.CurrentTimingMethod == TimingMethod.GameTime && !state.IsGameTimeInitialized
            ? TimingMethod.RealTime
            : state.CurrentTimingMethod;
    }

    /// <summary>
    ///     The current time, or the run's offset while the timer is not running.
    /// </summary>
    public static TimeSpan? CurrentTime(LiveSplitState state, TimingMethod method)
    {
        return state.CurrentPhase == TimerPhase.NotRunning ? state.Run.Offset : state.CurrentTime[method];
    }

    /// <summary>
    ///     The delta of the last split against <paramref name="comparison"/>.
    /// </summary>
    public static TimeSpan? Delta(LiveSplitState state, string comparison)
    {
        if (state.Run.Count == 0)
        {
            return null;
        }

        if (state.CurrentPhase is TimerPhase.Running or TimerPhase.Paused)
        {
            return LiveSplitStateHelper.GetLastDelta(state, state.CurrentSplitIndex, comparison, state.CurrentTimingMethod);
        }

        if (state.CurrentPhase == TimerPhase.Ended)
        {
            return state.Run[^1].SplitTime[state.CurrentTimingMethod] - state.Run[^1].Comparisons[comparison][state.CurrentTimingMethod];
        }

        return null;
    }

    /// <summary>
    ///     The predicted final time against <paramref name="comparison"/>.
    /// </summary>
    public static TimeSpan? PredictedTime(LiveSplitState state, string comparison)
    {
        if (state.Run.Count == 0)
        {
            return null;
        }

        TimingMethod method = state.CurrentTimingMethod;
        if (state.CurrentPhase is TimerPhase.Running or TimerPhase.Paused)
        {
            TimeSpan? delta = LiveSplitStateHelper.GetLastDelta(state, state.CurrentSplitIndex, comparison, method) ?? TimeSpan.Zero;
            TimeSpan? liveDelta = state.CurrentTime[method] - state.CurrentSplit.Comparisons[comparison][method];
            if (liveDelta > delta)
            {
                delta = liveDelta;
            }

            return delta + state.Run[^1].Comparisons[comparison][method];
        }

        if (state.CurrentPhase == TimerPhase.Ended)
        {
            return state.Run[^1].SplitTime[method];
        }

        return state.Run[^1].Comparisons[comparison][method];
    }

    /// <summary>
    ///     The best possible final time (the prediction against the sum of best segments).
    /// </summary>
    public static TimeSpan? BestPossibleTime(LiveSplitState state)
    {
        return PredictedTime(state, BestSegmentsComparisonGenerator.ComparisonName);
    }

    /// <summary>
    ///     The final time of the run if it ended, otherwise the final time of <paramref name="comparison"/>.
    /// </summary>
    public static TimeSpan? FinalTime(LiveSplitState state, string comparison)
    {
        if (state.Run.Count == 0)
        {
            return null;
        }

        return state.CurrentPhase == TimerPhase.Ended
            ? state.CurrentTime[state.CurrentTimingMethod]
            : state.Run[^1].Comparisons[comparison][state.CurrentTimingMethod];
    }

    public static int FinishedCount(IRun run)
    {
        return run.AttemptHistory.Count(x => x.Time.RealTime != null);
    }

    public static TimeSpan TotalPlaytime(IRun run)
    {
        TimeSpan totalPlaytime = TimeSpan.Zero;

        foreach (Attempt attempt in run.AttemptHistory)
        {
            TimeSpan? duration = attempt.Duration;

            if (duration.HasValue)
            {
                // Either >= 1.6.0 or a finished run.
                totalPlaytime += duration.Value - (attempt.PauseTime ?? TimeSpan.Zero);
            }
            else
            {
                // Must be < 1.6.0 and a reset: sum the segments of that attempt.
                foreach (ISegment segment in run)
                {
                    if (segment.SegmentHistory.TryGetValue(attempt.Index, out Time segmentTime) && segmentTime.RealTime.HasValue)
                    {
                        totalPlaytime += segmentTime.RealTime.Value;
                    }
                }
            }
        }

        return totalPlaytime;
    }

    /// <summary>
    ///     Resolves negative split indices from the end of the run (-1 is the last split).
    /// </summary>
    public static int ResolveSplitIndex(LiveSplitState state, int index)
    {
        return index < 0 ? state.Run.Count + index : index;
    }
}
