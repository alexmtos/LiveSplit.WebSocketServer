using LiveSplit.Options;
using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace LiveSplit.WsServer.Interop;

/// <summary>
///     Calls the file and hotkey operations of LiveSplit's main window (TimerForm).
///     They are private in LiveSplit and only handed to its built-in server, so they are
///     found by reflection. When a LiveSplit version renames them, the matching capability
///     turns off and the commands report "unsupported" instead of failing.
/// </summary>
public sealed class TimerFormBridge : ITimerFormBridge
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private readonly object form;
    private readonly MethodInfo saveSplits;
    private readonly MethodInfo saveLayout;
    private readonly MethodInfo openRun;
    private readonly MethodInfo openLayout;
    private readonly MethodInfo makeScreenShot;
    private readonly MethodInfo refreshHotkeyHooks;

    public TimerFormBridge(object form)
    {
        this.form = form;
        if (form == null)
        {
            return;
        }

        // bool SaveSplits(bool promptPBMessage, bool suppressPrompts)
        saveSplits = Find(typeof(bool), "SaveSplits", typeof(bool), typeof(bool));
        // bool SaveLayout(bool suppressPrompts)
        saveLayout = Find(typeof(bool), "SaveLayout", typeof(bool));
        // bool OpenRunFromFile(string filePath, bool suppressPrompts)
        openRun = Find(typeof(bool), "OpenRunFromFile", typeof(string), typeof(bool));
        // bool OpenLayoutFromFile(string filePath, bool suppressPrompts)
        openLayout = Find(typeof(bool), "OpenLayoutFromFile", typeof(string), typeof(bool));
        // Image MakeScreenShot()
        makeScreenShot = Find(typeof(Image), "MakeScreenShot");
        // void RefreshHotkeyHooks()
        refreshHotkeyHooks = Find(typeof(void), "RefreshHotkeyHooks");
    }

    public bool CanSaveSplits => saveSplits != null;
    public bool CanSaveLayout => saveLayout != null;
    public bool CanOpenSplits => openRun != null;
    public bool CanOpenLayout => openLayout != null;
    public bool CanTakeScreenshot => makeScreenShot != null;
    public bool CanRefreshHotkeys => refreshHotkeyHooks != null;

    public bool SaveSplits()
    {
        return (bool)Invoke(saveSplits, false, true);
    }

    public bool SaveLayout()
    {
        return (bool)Invoke(saveLayout, true);
    }

    public bool OpenSplits(string path)
    {
        return (bool)Invoke(openRun, path, true);
    }

    public bool OpenLayout(string path)
    {
        return (bool)Invoke(openLayout, path, true);
    }

    public Image TakeScreenshot()
    {
        return (Image)Invoke(makeScreenShot);
    }

    public void RefreshHotkeys()
    {
        Invoke(refreshHotkeyHooks);
    }

    private MethodInfo Find(Type returnType, string name, params Type[] parameters)
    {
        MethodInfo method = form.GetType()
            .GetMethods(Flags)
            .FirstOrDefault(m => m.Name == name
                && m.ReturnType == returnType
                && m.GetParameters().Select(p => p.ParameterType).SequenceEqual(parameters));

        if (method == null)
        {
            Log.Warning($"[WebSocket Server] {form.GetType().Name}.{name} was not found; the related commands are unavailable.");
        }

        return method;
    }

    private object Invoke(MethodInfo method, params object[] args)
    {
        if (method == null)
        {
            throw new NotSupportedException("This LiveSplit version does not support this operation.");
        }

        try
        {
            return method.Invoke(form, args);
        }
        catch (TargetInvocationException e) when (e.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(e.InnerException).Throw();
            throw;
        }
    }
}
