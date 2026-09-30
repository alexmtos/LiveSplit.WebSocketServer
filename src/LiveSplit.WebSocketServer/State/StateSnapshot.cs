using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LiveSplit.WsServer.State;

// The JSON representation of LiveSplit's state. All times are in milliseconds and all
// dates are ISO 8601 in UTC. The fields of protocol version 1 keep their names and meaning.

public sealed class StateSnapshot
{
    public RunSnapshot Run { get; set; }
    public string TimerState { get; set; }
    public string CurrentComparison { get; set; }
    public string CurrentTimingMethod { get; set; }
    public TimeDto CurrentTime { get; set; }
    public long? LoadingTimes { get; set; }
    public bool IsGameTimeInitialized { get; set; }
    public bool IsGameTimePaused { get; set; }
    public long? GameTimePauseTime { get; set; }
    public string AttemptStarted { get; set; }
    public string AttemptEnded { get; set; }
    public long? PauseTime { get; set; }
    public long? CurrentAttemptDuration { get; set; }
    public int CurrentSplitIndex { get; set; }

    /// <summary>Delta of the last split against the current comparison.</summary>
    public long? CurrentDelta { get; set; }

    /// <summary>Predicted final time against the current comparison.</summary>
    public long? PredictedTime { get; set; }

    public long? BestPossibleTime { get; set; }
    public string CurrentHotkeyProfile { get; set; }
    public IList<string> HotkeyProfiles { get; set; }
    public bool? GlobalHotkeysEnabled { get; set; }
    public string LayoutPath { get; set; }
}

public sealed class RunSnapshot
{
    public string GameIcon { get; set; }
    public string GameName { get; set; }
    public string CategoryName { get; set; }
    public long? StartingOffset { get; set; }
    public int AttemptCount { get; set; }
    public int FinishedCount { get; set; }
    public IList<string> Comparisons { get; set; }
    public IList<string> CustomComparisons { get; set; }
    public long? AdvancedSumOfBest { get; set; }
    public long? TotalPlaytime { get; set; }
    public IList<SegmentSnapshot> Segments { get; set; }
    public MetadataSnapshot Metadata { get; set; }
    public string FilePath { get; set; }
    public bool HasChanged { get; set; }
    public AutoSplitterSnapshot AutoSplitter { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IList<AttemptSnapshot> AttemptHistory { get; set; }
}

public sealed class SegmentSnapshot
{
    public string Icon { get; set; }
    public string Name { get; set; }
    public TimeDto SplitTime { get; set; }
    public TimeDto PersonalBest { get; set; }
    public TimeDto BestSegment { get; set; }
    public IDictionary<string, TimeDto> Comparisons { get; set; }
    public IDictionary<string, string> CustomVariables { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IList<SegmentHistoryEntry> SegmentHistory { get; set; }
}

public sealed class SegmentHistoryEntry
{
    public int AttemptIndex { get; set; }
    public TimeDto Time { get; set; }
}

public sealed class AttemptSnapshot
{
    public int Index { get; set; }
    public TimeDto Time { get; set; }
    public string Started { get; set; }
    public string Ended { get; set; }
    public long? PauseTime { get; set; }
    public long? Duration { get; set; }
}

public sealed class MetadataSnapshot
{
    // speedrun.com ids. They are only filled once speedrun.com data has been loaded by LiveSplit.
    public string GameId { get; set; }
    public string CategoryId { get; set; }
    public string RegionId { get; set; }
    public string PlatformId { get; set; }
    public bool Emulator { get; set; }

    /// <summary>speedrun.com variable id to value name.</summary>
    public IDictionary<string, string> Variables { get; set; }

    public string RegionName { get; set; }
    public string PlatformName { get; set; }
    public string RunId { get; set; }

    /// <summary>Variable name to value name, as stored in the splits file.</summary>
    public IDictionary<string, string> VariableNames { get; set; }

    public IDictionary<string, CustomVariableSnapshot> CustomVariables { get; set; }
}

public sealed class CustomVariableSnapshot
{
    public string Value { get; set; }
    public bool IsPermanent { get; set; }
}

public sealed class AutoSplitterSnapshot
{
    public string Description { get; set; }
    public string Type { get; set; }
    public bool Activated { get; set; }
    public string LocalPath { get; set; }
    public string Website { get; set; }
}
