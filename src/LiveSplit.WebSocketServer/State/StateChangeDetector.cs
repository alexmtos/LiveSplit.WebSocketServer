using LiveSplit.Model;
using LiveSplit.UI;
using LiveSplit.WsServer.Server;
using System.Collections.Generic;
using System.Linq;

namespace LiveSplit.WsServer.State;

/// <summary>
///     Detects changes LiveSplit raises no event for (opening splits or a layout, switching
///     comparison or timing method, game time pauses, hotkey profile, custom variables...)
///     by comparing the state with the previous frame. Must be used on the UI thread.
/// </summary>
public sealed class StateChangeDetector
{
    public readonly record struct Change(string Event, object Data);

    private bool initialized;
    private IRun run;
    private string runPath;
    private bool runHasChanged;
    private ILayout layout;
    private string layoutPath;
    private string comparison;
    private TimingMethod timingMethod;
    private bool gameTimePaused;
    private bool gameTimeInitialized;
    private string hotkeyProfile;
    private bool? globalHotkeys;
    private Dictionary<string, string> customVariables = [];

    /// <summary>
    ///     Returns what changed since the previous call. The first call only records the state.
    /// </summary>
    public IReadOnlyList<Change> Detect(LiveSplitState state)
    {
        var changes = new List<Change>();

        IRun currentRun = state.Run;
        string currentRunPath = currentRun?.FilePath;
        bool currentRunHasChanged = currentRun?.HasChanged ?? false;
        ILayout currentLayout = state.Layout;
        string currentLayoutPath = currentLayout?.FilePath;
        bool? currentGlobalHotkeys = StateSnapshotBuilder.CurrentHotkeyProfile(state)?.GlobalHotkeysEnabled;
        Dictionary<string, string> currentCustomVariables = currentRun?.Metadata.CustomVariables.ToDictionary(x => x.Key, x => x.Value.Value)
            ?? [];

        if (initialized)
        {
            bool runReplaced = !ReferenceEquals(currentRun, run) || currentRunPath != runPath;
            if (runReplaced)
            {
                changes.Add(new Change(ServerEvents.RunChanged, new
                {
                    path = currentRunPath,
                    gameName = currentRun?.GameName,
                    categoryName = currentRun?.CategoryName,
                }));
            }
            else if (runHasChanged && !currentRunHasChanged)
            {
                changes.Add(new Change(ServerEvents.RunSaved, new { path = currentRunPath }));
            }

            if (!ReferenceEquals(currentLayout, layout) || currentLayoutPath != layoutPath)
            {
                changes.Add(new Change(ServerEvents.LayoutChanged, new { path = currentLayoutPath }));
            }

            if (state.CurrentComparison != comparison)
            {
                changes.Add(new Change(ServerEvents.ComparisonChanged, new { comparison = state.CurrentComparison }));
            }

            if (state.CurrentTimingMethod != timingMethod)
            {
                changes.Add(new Change(ServerEvents.TimingMethodChanged, new { timingMethod = state.CurrentTimingMethod.ToString() }));
            }

            if (state.IsGameTimeInitialized != gameTimeInitialized)
            {
                changes.Add(new Change(ServerEvents.GameTimeInitialized, new { initialized = state.IsGameTimeInitialized }));
            }

            if (state.IsGameTimePaused != gameTimePaused)
            {
                changes.Add(new Change(state.IsGameTimePaused ? ServerEvents.GameTimePaused : ServerEvents.GameTimeResumed, null));
            }

            if (state.CurrentHotkeyProfile != hotkeyProfile)
            {
                changes.Add(new Change(ServerEvents.HotkeyProfileChanged, new { profile = state.CurrentHotkeyProfile }));
            }

            if (currentGlobalHotkeys != globalHotkeys)
            {
                changes.Add(new Change(ServerEvents.GlobalHotkeysChanged, new { enabled = currentGlobalHotkeys }));
            }

            // A different run brings its own variables, which run-changed already covers.
            if (!runReplaced)
            {
                foreach (KeyValuePair<string, string> variable in currentCustomVariables)
                {
                    if (!customVariables.TryGetValue(variable.Key, out string previous) || previous != variable.Value)
                    {
                        changes.Add(new Change(ServerEvents.CustomVariableChanged, new { name = variable.Key, value = variable.Value }));
                    }
                }

                foreach (string removed in customVariables.Keys.Where(x => !currentCustomVariables.ContainsKey(x)))
                {
                    changes.Add(new Change(ServerEvents.CustomVariableChanged, new { name = removed, value = (string)null }));
                }
            }
        }

        initialized = true;
        run = currentRun;
        runPath = currentRunPath;
        runHasChanged = currentRunHasChanged;
        layout = currentLayout;
        layoutPath = currentLayoutPath;
        comparison = state.CurrentComparison;
        timingMethod = state.CurrentTimingMethod;
        gameTimePaused = state.IsGameTimePaused;
        gameTimeInitialized = state.IsGameTimeInitialized;
        hotkeyProfile = state.CurrentHotkeyProfile;
        globalHotkeys = currentGlobalHotkeys;
        customVariables = currentCustomVariables;

        return changes;
    }
}
