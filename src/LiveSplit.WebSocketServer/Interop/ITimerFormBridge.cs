using System.Drawing;

namespace LiveSplit.WsServer.Interop;

/// <summary>
///     Operations that only LiveSplit's main window can perform (saving and opening files,
///     taking screenshots, re-registering hotkeys). Must be used on the UI thread.
/// </summary>
public interface ITimerFormBridge
{
    bool CanSaveSplits { get; }
    bool CanSaveLayout { get; }
    bool CanOpenSplits { get; }
    bool CanOpenLayout { get; }
    bool CanTakeScreenshot { get; }
    bool CanRefreshHotkeys { get; }

    bool SaveSplits();
    bool SaveLayout();
    bool OpenSplits(string path);
    bool OpenLayout(string path);
    Image TakeScreenshot();
    void RefreshHotkeys();
}
