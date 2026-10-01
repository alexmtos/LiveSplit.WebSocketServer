using LiveSplit.Model;
using LiveSplit.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LiveSplit.WsServer.State;

/// <summary>
///     Builds <see cref="StateSnapshot"/>s. Must be used on LiveSplit's UI thread.
/// </summary>
public sealed class StateSnapshotBuilder
{
    private readonly IconCache icons = new();

    public StateSnapshot Build(LiveSplitState state, SnapshotOptions options)
    {
        IRun run = state.Run;
        HotkeyProfile hotkeyProfile = CurrentHotkeyProfile(state);

        return new StateSnapshot
        {
            Run = BuildRun(state, run, options),
            TimerState = state.CurrentPhase.ToString(),
            CurrentComparison = state.CurrentComparison,
            CurrentTimingMethod = state.CurrentTimingMethod.ToString(),
            CurrentTime = TimeDto.From(state.CurrentTime),
            LoadingTimes = TimeDto.Milliseconds(state.LoadingTimes),
            IsGameTimeInitialized = state.IsGameTimeInitialized,
            IsGameTimePaused = state.IsGameTimePaused,
            GameTimePauseTime = TimeDto.Milliseconds(state.GameTimePauseTime),
            AttemptStarted = TimeDto.Date(state.AttemptStarted),
            AttemptEnded = TimeDto.Date(state.AttemptEnded),
            PauseTime = TimeDto.Milliseconds(state.PauseTime),
            CurrentAttemptDuration = TimeDto.Milliseconds(state.CurrentAttemptDuration),
            CurrentSplitIndex = state.CurrentSplitIndex,
            CurrentDelta = TimeDto.Milliseconds(Safe(() => TimerCalculations.Delta(state, state.CurrentComparison))),
            PredictedTime = TimeDto.Milliseconds(Safe(() => TimerCalculations.PredictedTime(state, state.CurrentComparison))),
            BestPossibleTime = TimeDto.Milliseconds(Safe(() => TimerCalculations.BestPossibleTime(state))),
            CurrentHotkeyProfile = state.CurrentHotkeyProfile,
            HotkeyProfiles = state.Settings?.HotkeyProfiles?.Keys.ToList(),
            GlobalHotkeysEnabled = hotkeyProfile?.GlobalHotkeysEnabled,
            LayoutPath = NullIfEmpty(state.Layout?.FilePath),
        };
    }

    private RunSnapshot BuildRun(LiveSplitState state, IRun run, SnapshotOptions options)
    {
        return new RunSnapshot
        {
            GameIcon = options.IncludeIcons ? icons.GetDataUri(run.GameIcon) : null,
            GameName = run.GameName,
            CategoryName = run.CategoryName,
            StartingOffset = TimeDto.Milliseconds(run.Offset),
            AttemptCount = run.AttemptCount,
            FinishedCount = TimerCalculations.FinishedCount(run),
            Comparisons = run.Comparisons.ToList(),
            CustomComparisons = run.CustomComparisons.ToList(),
            AdvancedSumOfBest = TimeDto.Milliseconds(Safe(() => SumOfBest.CalculateSumOfBest(run, false, true, state.CurrentTimingMethod))),
            TotalPlaytime = TimeDto.Milliseconds(TimerCalculations.TotalPlaytime(run)),
            Segments = run.Select(segment => BuildSegment(segment, options)).ToList(),
            Metadata = BuildMetadata(run.Metadata),
            FilePath = NullIfEmpty(run.FilePath),
            HasChanged = run.HasChanged,
            AutoSplitter = BuildAutoSplitter(run.AutoSplitter),
            AttemptHistory = options.IncludeHistory
                ? run.AttemptHistory.Select(attempt => new AttemptSnapshot
                {
                    Index = attempt.Index,
                    Time = TimeDto.From(attempt.Time),
                    Started = TimeDto.Date(attempt.Started),
                    Ended = TimeDto.Date(attempt.Ended),
                    PauseTime = TimeDto.Milliseconds(attempt.PauseTime),
                    Duration = TimeDto.Milliseconds(attempt.Duration),
                }).ToList()
                : null,
        };
    }

    private SegmentSnapshot BuildSegment(ISegment segment, SnapshotOptions options)
    {
        return new SegmentSnapshot
        {
            Icon = options.IncludeIcons ? icons.GetDataUri(segment.Icon) : null,
            Name = segment.Name,
            SplitTime = TimeDto.From(segment.SplitTime),
            PersonalBest = TimeDto.From(segment.PersonalBestSplitTime),
            BestSegment = TimeDto.From(segment.BestSegmentTime),
            Comparisons = segment.Comparisons.ToDictionary(x => x.Key, x => TimeDto.From(x.Value)),
            CustomVariables = segment.CustomVariableValues != null
                ? new Dictionary<string, string>(segment.CustomVariableValues)
                : new Dictionary<string, string>(),
            SegmentHistory = options.IncludeHistory && segment.SegmentHistory != null
                ? segment.SegmentHistory
                    .OrderBy(x => x.Key)
                    .Select(x => new SegmentHistoryEntry { AttemptIndex = x.Key, Time = TimeDto.From(x.Value) })
                    .ToList()
                : null,
        };
    }

    private MetadataSnapshot BuildMetadata(RunMetadata metadata)
    {
        var snapshot = new MetadataSnapshot
        {
            Emulator = metadata.UsesEmulator,
            RegionName = NullIfEmpty(metadata.RegionName),
            PlatformName = NullIfEmpty(metadata.PlatformName),
            RunId = metadata.RunID,
            VariableNames = metadata.VariableValueNames != null
                ? new Dictionary<string, string>(metadata.VariableValueNames)
                : new Dictionary<string, string>(),
            CustomVariables = metadata.CustomVariables.ToDictionary(
                x => x.Key,
                x => new CustomVariableSnapshot { Value = x.Value.Value, IsPermanent = x.Value.IsPermanent }),
            Variables = new Dictionary<string, string>(),
        };

        // Reading Game or Category blocks until speedrun.com answers, which must never
        // happen on the UI thread. Only use them once LiveSplit has loaded them.
        try
        {
            if (metadata.GameAvailable)
            {
                snapshot.GameId = metadata.Game?.ID;
                snapshot.RegionId = metadata.Region?.ID;
                snapshot.PlatformId = metadata.Platform?.ID;

                if (metadata.CategoryAvailable)
                {
                    snapshot.CategoryId = metadata.Category?.ID;
                    snapshot.Variables = SpeedrunComVariables(metadata);
                }
            }
        }
        catch (Exception e)
        {
            Log.Error(e);
        }

        return snapshot;
    }

    private readonly object variablesLock = new();
    private string variablesKey;
    private Dictionary<string, string> variables = [];
    private bool variablesLoading;

    /// <summary>
    ///     speedrun.com variable ids and values. Resolving them may download the game's variables
    ///     from speedrun.com, so it happens in the background; until then the result is empty.
    /// </summary>
    private Dictionary<string, string> SpeedrunComVariables(RunMetadata metadata)
    {
        string key = string.Join("\n", new[] { metadata.LiveSplitRun.GameName, metadata.LiveSplitRun.CategoryName }
            .Concat((metadata.VariableValueNames ?? new Dictionary<string, string>()).OrderBy(x => x.Key).Select(x => x.Key + "=" + x.Value)));

        lock (variablesLock)
        {
            if (key == variablesKey)
            {
                return new Dictionary<string, string>(variables);
            }

            if (!variablesLoading)
            {
                variablesLoading = true;
                Task.Run(() => LoadSpeedrunComVariables(metadata, key));
            }

            return [];
        }
    }

    private void LoadSpeedrunComVariables(RunMetadata metadata, string key)
    {
        var result = new Dictionary<string, string>();
        try
        {
            foreach (KeyValuePair<SpeedrunComSharp.Variable, SpeedrunComSharp.VariableValue> variable in metadata.VariableValues)
            {
                if (variable.Key != null && variable.Value != null)
                {
                    result[variable.Key.ID] = variable.Value.Value;
                }
            }
        }
        catch (Exception)
        {
            // speedrun.com is unreachable; try again when the run changes.
        }

        lock (variablesLock)
        {
            variablesKey = key;
            variables = result;
            variablesLoading = false;
        }
    }

    private static AutoSplitterSnapshot BuildAutoSplitter(AutoSplitter autoSplitter)
    {
        if (autoSplitter == null)
        {
            return null;
        }

        return new AutoSplitterSnapshot
        {
            Description = autoSplitter.Description,
            Type = autoSplitter.Type.ToString(),
            Activated = autoSplitter.IsActivated,
            LocalPath = Safe(() => autoSplitter.LocalPath),
            Website = autoSplitter.Website,
        };
    }

    public static HotkeyProfile CurrentHotkeyProfile(LiveSplitState state)
    {
        IDictionary<string, HotkeyProfile> profiles = state.Settings?.HotkeyProfiles;
        if (profiles == null || state.CurrentHotkeyProfile == null)
        {
            return null;
        }

        return profiles.TryGetValue(state.CurrentHotkeyProfile, out HotkeyProfile profile) ? profile : null;
    }

    private static string NullIfEmpty(string value)
    {
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static T Safe<T>(Func<T> func)
    {
        try
        {
            return func();
        }
        catch (Exception)
        {
            return default;
        }
    }
}
