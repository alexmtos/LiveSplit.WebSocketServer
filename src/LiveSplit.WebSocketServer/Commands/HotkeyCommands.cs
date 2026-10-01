using LiveSplit.Options;
using LiveSplit.WsServer.Protocol;
using LiveSplit.WsServer.State;
using System;
using System.Linq;

namespace LiveSplit.WsServer.Commands;

/// <summary>
///     Actions about LiveSplit's hotkeys.
/// </summary>
public static class HotkeyCommands
{
    public static void Register(CommandDispatcher d)
    {
        d.Register("enableglobalhotkeys", CommandAccess.Control, "Enables global hotkeys for the current hotkey profile.", (c, a) => SetGlobalHotkeys(c, true));
        d.Register("disableglobalhotkeys", CommandAccess.Control, "Disables global hotkeys for the current hotkey profile.", (c, a) => SetGlobalHotkeys(c, false));
        d.Register("setglobalhotkeys", CommandAccess.Control, "Enables or disables global hotkeys. Args: { enabled: bool }.",
            (c, a) => SetGlobalHotkeys(c, a.GetBool("enabled") ?? throw CommandException.InvalidArgs("Missing argument 'enabled'.")));
        d.Register("switchhotkeyprofile", CommandAccess.Control, "Switches to another hotkey profile. Args: { profile }.", SwitchHotkeyProfile, "sethotkeyprofile");
    }

    private static HotkeyProfile RequireProfile(CommandContext c)
    {
        return StateSnapshotBuilder.CurrentHotkeyProfile(c.State)
            ?? throw CommandException.Unsupported("There is no current hotkey profile.");
    }

    private static object SetGlobalHotkeys(CommandContext c, bool enabled)
    {
        RequireProfile(c).GlobalHotkeysEnabled = enabled;
        return new { profile = c.State.CurrentHotkeyProfile, globalHotkeysEnabled = enabled };
    }

    private static object SwitchHotkeyProfile(CommandContext c, CommandArgs a)
    {
        string requested = a.RequireString("profile");
        string profile = c.State.Settings.HotkeyProfiles.Keys.FirstOrDefault(x => x == requested)
            ?? c.State.Settings.HotkeyProfiles.Keys.FirstOrDefault(x => string.Equals(x, requested, StringComparison.OrdinalIgnoreCase))
            ?? throw CommandException.InvalidArgs($"Unknown hotkey profile '{requested}'. Available: {string.Join(", ", c.State.Settings.HotkeyProfiles.Keys)}.");

        if (c.Form == null || !c.Form.CanRefreshHotkeys)
        {
            // Without re-registering the hooks the new profile would only be half active.
            throw CommandException.Unsupported("This LiveSplit version does not allow switching hotkey profiles from a component.");
        }

        c.State.CurrentHotkeyProfile = profile;
        c.Form.RefreshHotkeys();
        return new { profile };
    }
}
