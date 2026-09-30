using LiveSplit.Model;
using LiveSplit.Options;
using LiveSplit.WsServer.Commands;
using LiveSplit.WsServer.Infrastructure;
using LiveSplit.WsServer.Server;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

namespace LiveSplit.UI.Components;

public class ServerComponent : IComponent
{
    private const string StartMenuText = "Start WebSocket Server (JSON)";
    private const string StopMenuText = "Stop WebSocket Server (JSON)";

    public Settings Settings { get; set; }
    public WebSocketHost Host { get; }

    protected System.Timers.Timer Timer { get; set; }
    protected IUiDispatcher Dispatcher { get; set; }

    protected LiveSplitState State { get; set; }
    protected TimerModel Model { get; set; }

    public float PaddingTop => 0;
    public float PaddingBottom => 0;
    public float PaddingLeft => 0;
    public float PaddingRight => 0;

    public string ComponentName => $"LiveSplit WebSocket Server ({Settings.Port})";

    public IDictionary<string, Action> ContextMenuControls { get; protected set; }

    private bool disposed;

    public ServerComponent(LiveSplitState state)
    {
        Settings = new Settings();
        Model = new TimerModel();

        ContextMenuControls = new Dictionary<string, Action>();
        UpdateContextMenu();

        State = state;
        Dispatcher = state.Form != null ? new FormUiDispatcher(state.Form) : new InlineUiDispatcher();

        Model.CurrentState = State;

        var runtime = new ServerRuntime(State, Model, Dispatcher, null, Settings, CommandDispatcher.CreateDefault(),
            typeof(ServerComponent).Assembly.GetName().Version.ToString(3));
        Host = new WebSocketHost(runtime);

        State.OnSplit += State_OnSplit;
        State.OnUndoSplit += State_OnUndoSplit;
        State.OnSkipSplit += State_OnSkipSplit;
        State.OnStart += State_OnStart;
        State.OnReset += State_OnReset;
        State.OnPause += State_OnPause;
        State.OnUndoAllPauses += State_OnUndoAllPauses;
        State.OnResume += State_OnResume;
        State.OnScrollUp += State_OnScrollUp;
        State.OnScrollDown += State_OnScrollDown;
        State.OnSwitchComparisonPrevious += State_OnSwitchComparisonPrevious;
        State.OnSwitchComparisonNext += State_OnSwitchComparisonNext;
        State.RunManuallyModified += State_RunManuallyModified;
        State.ComparisonRenamed += State_ComparisonRenamed;
    }

    public void Start()
    {
        StartServer(showErrors: true);
    }

    private void StartServer(bool showErrors)
    {
        CloseAllConnections();

        try
        {
            Host.Start(IPAddress.Any, Settings.Port);
        }
        catch (Exception e)
        {
            Log.Error(e);
            Log.Error($"[WebSocket Server] Could not start the server on port {Settings.Port}.");
            Host.Stop();

            if (showErrors)
            {
                MessageBox.Show(State.Form,
                    $"Could not start the WebSocket server on port {Settings.Port}.\n\n{e.Message}",
                    "LiveSplit WebSocket Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            UpdateContextMenu();
            return;
        }

        Timer = new System.Timers.Timer(15000)
        {
            AutoReset = true
        };
        Timer.Elapsed += Timer_Elapsed;
        Timer.Start();

        UpdateContextMenu();
    }

    public void Stop()
    {
        CloseAllConnections();
        UpdateContextMenu();
    }

    protected void CloseAllConnections()
    {
        // Stop the timer first so that no refresh gets queued while the server shuts down.
        if (Timer != null)
        {
            Timer.Elapsed -= Timer_Elapsed;
            Timer.Stop();
            Timer.Dispose();
            Timer = null;
        }

        Host.Stop();
    }

    private void UpdateContextMenu()
    {
        ContextMenuControls.Clear();
        if (Host?.IsRunning != true)
        {
            ContextMenuControls.Add(StartMenuText, Start);
        }
        else
        {
            ContextMenuControls.Add(StopMenuText, Stop);
        }
    }

    public void DrawVertical(Graphics g, LiveSplitState state, float width, Region clipRegion)
    {
    }

    public void DrawHorizontal(Graphics g, LiveSplitState state, float height, Region clipRegion)
    {
    }

    public float VerticalHeight => 0;

    public float MinimumWidth => 0;

    public float HorizontalWidth => 0;

    public float MinimumHeight => 0;

    public XmlNode GetSettings(XmlDocument document)
    {
        return Settings.GetSettings(document);
    }

    public Control GetSettingsControl(LayoutMode mode)
    {
        return Settings;
    }

    public void SetSettings(XmlNode settings)
    {
        Settings.SetSettings(settings);
        if (!Host.IsRunning && Settings.AutoStart)
        {
            // The layout is loaded before the main form is fully shown, so start a little later.
            Task.Delay(500).ContinueWith(_ => Dispatcher.Post(() =>
            {
                if (!disposed && !Host.IsRunning)
                {
                    StartServer(showErrors: false);
                }
            }));
        }
    }

    public void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
    {
    }

    private void SendState(string action, object data)
    {
        // Timer events may be raised from other threads (for example by auto splitters),
        // so the state is always read on the UI thread.
        Dispatcher.Post(() =>
        {
            if (Host.IsRunning)
            {
                Host.Broadcast(action, data);
            }
        });
    }

    private void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
    {
        SendState("refresh", null);
    }

    private void State_OnSplit(object sender, EventArgs e)
    {
        SendState("split", null);
    }

    private void State_OnUndoSplit(object sender, EventArgs e)
    {
        SendState("undo-split", null);
    }

    private void State_OnSkipSplit(object sender, EventArgs e)
    {
        SendState("skip-split", null);
    }

    private void State_OnStart(object sender, EventArgs e)
    {
        SendState("start", null);
    }

    private void State_OnReset(object sender, TimerPhase value)
    {
        SendState("reset", null);
    }

    private void State_OnPause(object sender, EventArgs e)
    {
        SendState("pause", null);
    }

    private void State_OnUndoAllPauses(object sender, EventArgs e)
    {
        SendState("undo-all-pauses", null);
    }

    private void State_OnResume(object sender, EventArgs e)
    {
        SendState("resume", null);
    }

    private void State_OnScrollUp(object sender, EventArgs e)
    {
        SendState("scroll", "up");
    }

    private void State_OnScrollDown(object sender, EventArgs e)
    {
        SendState("scroll", "down");
    }

    private void State_OnSwitchComparisonPrevious(object sender, EventArgs e)
    {
        SendState("switch-comparison", "previous");
    }

    private void State_OnSwitchComparisonNext(object sender, EventArgs e)
    {
        SendState("switch-comparison", "next");
    }

    private void State_RunManuallyModified(object sender, EventArgs e)
    {
        SendState("run-manually-modified", null);
    }

    private void State_ComparisonRenamed(object sender, EventArgs e)
    {
        SendState("comparison-renamed", null);
    }

    public void Dispose()
    {
        disposed = true;

        State.OnSplit -= State_OnSplit;
        State.OnUndoSplit -= State_OnUndoSplit;
        State.OnSkipSplit -= State_OnSkipSplit;
        State.OnStart -= State_OnStart;
        State.OnReset -= State_OnReset;
        State.OnPause -= State_OnPause;
        State.OnUndoAllPauses -= State_OnUndoAllPauses;
        State.OnResume -= State_OnResume;
        State.OnScrollUp -= State_OnScrollUp;
        State.OnScrollDown -= State_OnScrollDown;
        State.OnSwitchComparisonPrevious -= State_OnSwitchComparisonPrevious;
        State.OnSwitchComparisonNext -= State_OnSwitchComparisonNext;
        State.RunManuallyModified -= State_RunManuallyModified;
        State.ComparisonRenamed -= State_ComparisonRenamed;

        CloseAllConnections();
    }

    public int GetSettingsHashCode()
    {
        return Settings.GetSettingsHashCode();
    }
}
