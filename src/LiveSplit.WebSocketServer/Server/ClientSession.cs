using LiveSplit.Model;
using LiveSplit.UI.Components;
using LiveSplit.Web;
using LiveSplit.WsServer.Infrastructure;
using System;
using System.Threading.Tasks;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace LiveSplit.WsServer.Server;

/// <summary>
///     One connected WebSocket client. Messages arrive on a websocket-sharp
///     worker thread and are executed on LiveSplit's UI thread.
/// </summary>
internal sealed class ClientSession : WebSocketBehavior
{
    private readonly LiveSplitState state;
    private readonly ITimerModel model;
    private readonly Settings settings;
    private readonly IUiDispatcher dispatcher;

    public ClientSession(LiveSplitState state, ITimerModel model, Settings settings, IUiDispatcher dispatcher)
    {
        this.state = state;
        this.model = model;
        this.settings = settings;
        this.dispatcher = dispatcher;
    }

    protected override void OnOpen()
    {
        Reply(dispatcher.InvokeAsync(() =>
        {
            dynamic jsonData = new DynamicJsonObject();
            jsonData.open = new DynamicJsonObject();
            jsonData.open.response = "success";
            jsonData.state = JsonState.Create(state);
            return (string)jsonData.ToString();
        }));
    }

    protected override void OnMessage(MessageEventArgs e)
    {
        string action = ParseAction(e.Data);
        Reply(dispatcher.InvokeAsync(() => Execute(action)));
    }

    private static string ParseAction(string message)
    {
        try
        {
            dynamic messageData = JSON.FromString(message);
            if (messageData is DynamicJsonObject)
            {
                return messageData.action as string;
            }
        }
        catch (Exception)
        {
            // Not JSON: the whole message is the action.
        }

        return message?.Trim();
    }

    private string Execute(string action)
    {
        dynamic jsonData = new DynamicJsonObject();
        jsonData.response = new DynamicJsonObject();
        jsonData.response.response = action;

        switch (action)
        {
            case "hi":
                return jsonData.ToString();
            case "state":
                jsonData.state = JsonState.Create(state);
                return jsonData.ToString();
        }

        if (settings.ReadOnly)
        {
            return null;
        }

        switch (action)
        {
            case "startorsplit":
                if (state.CurrentPhase == TimerPhase.Running)
                {
                    model.Split();
                }
                else
                {
                    model.Start();
                }

                break;
            case "split":
                model.Split();
                break;
            case "unsplit":
                model.UndoSplit();
                break;
            case "skipsplit":
                model.SkipSplit();
                break;
            case "pause":
                if (state.CurrentPhase != TimerPhase.Paused)
                {
                    model.Pause();
                }

                break;
            case "resume":
                if (state.CurrentPhase == TimerPhase.Paused)
                {
                    model.Pause();
                }

                break;
            case "reset":
                model.Reset();
                break;
            case "starttimer":
                model.Start();
                break;
            case "pausegametime":
                state.IsGameTimePaused = true;
                break;
            case "unpausegametime":
                state.IsGameTimePaused = false;
                break;
        }

        return null;
    }

    private void Reply(Task<string> response)
    {
        response.ContinueWith(task =>
        {
            if (task.Status == TaskStatus.RanToCompletion && task.Result != null && State == WebSocketState.Open)
            {
                SendAsync(task.Result, null);
            }
        }, TaskScheduler.Default);
    }
}
