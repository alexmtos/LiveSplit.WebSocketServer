using System.Collections.Generic;

namespace LiveSplit.WsServer.Server;

/// <summary>
///     Names of the events sent to clients.
/// </summary>
public static class ServerEvents
{
    // Raised by LiveSplit (also sent to protocol version 1 clients).
    public const string Refresh = "refresh";
    public const string Split = "split";
    public const string UndoSplit = "undo-split";
    public const string SkipSplit = "skip-split";
    public const string Start = "start";
    public const string Reset = "reset";
    public const string Pause = "pause";
    public const string UndoAllPauses = "undo-all-pauses";
    public const string Resume = "resume";
    public const string Scroll = "scroll";
    public const string SwitchComparison = "switch-comparison";
    public const string RunManuallyModified = "run-manually-modified";
    public const string ComparisonRenamed = "comparison-renamed";

    // Detected by comparing the state between frames (protocol version 2 only).
    public const string RunChanged = "run-changed";
    public const string RunSaved = "run-saved";
    public const string LayoutChanged = "layout-changed";
    public const string ComparisonChanged = "comparison-changed";
    public const string TimingMethodChanged = "timing-method-changed";
    public const string GameTimePaused = "game-time-paused";
    public const string GameTimeResumed = "game-time-resumed";
    public const string GameTimeInitialized = "game-time-initialized";
    public const string HotkeyProfileChanged = "hotkey-profile-changed";
    public const string GlobalHotkeysChanged = "global-hotkeys-changed";
    public const string CustomVariableChanged = "custom-variable-changed";

    /// <summary>
    ///     Events that protocol version 1 clients receive, as <c>{ action: { action, data }, state }</c>.
    /// </summary>
    public static readonly ISet<string> Legacy = new HashSet<string>
    {
        Refresh, Split, UndoSplit, SkipSplit, Start, Reset, Pause, UndoAllPauses,
        Resume, Scroll, SwitchComparison, RunManuallyModified, ComparisonRenamed,
    };

    public static readonly ISet<string> All = new HashSet<string>(Legacy)
    {
        RunChanged, RunSaved, LayoutChanged, ComparisonChanged, TimingMethodChanged, GameTimePaused,
        GameTimeResumed, GameTimeInitialized, HotkeyProfileChanged, GlobalHotkeysChanged, CustomVariableChanged,
    };
}
