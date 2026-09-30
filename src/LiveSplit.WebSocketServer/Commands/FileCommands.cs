using LiveSplit.WsServer.Interop;
using LiveSplit.WsServer.Protocol;
using LiveSplit.WsServer.State;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace LiveSplit.WsServer.Commands;

/// <summary>
///     Actions that save or open splits and layouts, and screenshots of the timer.
///     Paths are local to the computer running LiveSplit.
/// </summary>
public static class FileCommands
{
    public static void Register(CommandDispatcher d)
    {
        d.Register("savesplits", CommandAccess.File, "Saves the splits to their file (a new file next to LiveSplit if they were never saved).", SaveSplits);
        d.Register("savesplitsas", CommandAccess.File, "Saves the splits to another file. Args: { path } (.lss).", SaveSplitsAs);
        d.Register("savelayout", CommandAccess.File, "Saves the layout to its file.", SaveLayout);
        d.Register("savelayoutas", CommandAccess.File, "Saves the layout to another file. Args: { path } (.lsl).", SaveLayoutAs);
        d.Register("switchsplits", CommandAccess.File, "Opens a splits file. Resets the timer without saving. Args: { path }.", SwitchSplits, "opensplits");
        d.Register("switchlayout", CommandAccess.File, "Opens a layout file. Args: { path }.", SwitchLayout, "openlayout");
        d.Register("savesplitsscreenshot", CommandAccess.File, "Saves a PNG screenshot of the timer. Args: { path }.", SaveScreenshot);

        // Only reads the window, so it is allowed like any other read action.
        d.Register("getsplitsscreenshot", CommandAccess.Read, "Returns a screenshot of the timer as a PNG data URI.", GetScreenshot, "screenshot");
    }

    private static ITimerFormBridge RequireForm(CommandContext c, Func<ITimerFormBridge, bool> capability)
    {
        ITimerFormBridge form = c.Form;
        if (form == null || !capability(form))
        {
            throw CommandException.Unsupported("This LiveSplit version does not allow this action from a component.");
        }

        return form;
    }

    private static string RequirePath(CommandArgs a, string extension)
    {
        string path = a.RequireString("path").Trim();
        if (extension != null && !path.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
        {
            throw CommandException.InvalidArgs($"The path must end with {extension}.");
        }

        if (!Path.IsPathRooted(path))
        {
            throw CommandException.InvalidArgs("The path must be absolute.");
        }

        return path;
    }

    private static object FileResult(bool success, string path, string what)
    {
        if (!success)
        {
            throw new CommandException(ErrorCodes.Internal, $"Could not {what}. See LiveSplit's error log for details.");
        }

        return new { path };
    }

    private static object SaveSplits(CommandContext c, CommandArgs a)
    {
        ITimerFormBridge form = RequireForm(c, x => x.CanSaveSplits);
        return FileResult(form.SaveSplits(), c.State.Run.FilePath, "save the splits");
    }

    private static object SaveSplitsAs(CommandContext c, CommandArgs a)
    {
        ITimerFormBridge form = RequireForm(c, x => x.CanSaveSplits);
        string path = RequirePath(a, ".lss");

        string previous = c.State.Run.FilePath;
        c.State.Run.FilePath = path;
        bool success = form.SaveSplits();
        if (!success)
        {
            c.State.Run.FilePath = previous;
        }

        return FileResult(success, path, "save the splits");
    }

    private static object SaveLayout(CommandContext c, CommandArgs a)
    {
        ITimerFormBridge form = RequireForm(c, x => x.CanSaveLayout);
        if (string.IsNullOrEmpty(c.State.Layout?.FilePath))
        {
            throw CommandException.InvalidArgs("The layout was never saved. Use 'savelayoutas' with a path.");
        }

        return FileResult(form.SaveLayout(), c.State.Layout.FilePath, "save the layout");
    }

    private static object SaveLayoutAs(CommandContext c, CommandArgs a)
    {
        ITimerFormBridge form = RequireForm(c, x => x.CanSaveLayout);
        string path = RequirePath(a, ".lsl");

        string previous = c.State.Layout.FilePath;
        c.State.Layout.FilePath = path;
        bool success = form.SaveLayout();
        if (!success)
        {
            c.State.Layout.FilePath = previous;
        }

        return FileResult(success, path, "save the layout");
    }

    private static object SwitchSplits(CommandContext c, CommandArgs a)
    {
        ITimerFormBridge form = RequireForm(c, x => x.CanOpenSplits);
        string path = RequirePath(a, null);
        if (!File.Exists(path))
        {
            throw CommandException.InvalidArgs($"The file does not exist: {path}");
        }

        return FileResult(form.OpenSplits(path), path, "open the splits");
    }

    private static object SwitchLayout(CommandContext c, CommandArgs a)
    {
        ITimerFormBridge form = RequireForm(c, x => x.CanOpenLayout);
        string path = RequirePath(a, null);
        if (!File.Exists(path))
        {
            throw CommandException.InvalidArgs($"The file does not exist: {path}");
        }

        return FileResult(form.OpenLayout(path), path, "open the layout");
    }

    private static object GetScreenshot(CommandContext c, CommandArgs a)
    {
        ITimerFormBridge form = RequireForm(c, x => x.CanTakeScreenshot);
        using Image image = form.TakeScreenshot();
        return IconCache.Encode(image) ?? throw new CommandException(ErrorCodes.Internal, "Could not encode the screenshot.");
    }

    private static object SaveScreenshot(CommandContext c, CommandArgs a)
    {
        ITimerFormBridge form = RequireForm(c, x => x.CanTakeScreenshot);
        string path = RequirePath(a, ".png");
        using Image image = form.TakeScreenshot();
        image.Save(path, ImageFormat.Png);
        return new { path };
    }
}
