using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.Options;
using LiveSplit.WsServer.Commands;
using LiveSplit.WsServer.Infrastructure;
using LiveSplit.WsServer.Interop;
using LiveSplit.WsServer.Protocol;
using LiveSplit.WsServer.Server;
using System;
using System.Collections.Generic;
using System.Threading;

namespace LiveSplit.WsServer.Tests;

internal sealed class TestOptions : IServerOptions
{
    public bool ReadOnly { get; set; }
    public bool AllowFileCommands { get; set; }
    public string AuthToken { get; set; } = "";
    public IReadOnlyCollection<string> AllowedOriginList { get; set; } = [];
}

internal sealed class TestSession : ISessionControl
{
    public int ProtocolVersion { get; set; } = Protocol.ProtocolVersion.Current;
    public SessionSubscription Subscription { get; } = new();
}

/// <summary>
///     A real LiveSplit state and timer model, without any window.
/// </summary>
internal sealed class TestLiveSplit
{
    public LiveSplitState State { get; }
    public TimerModel Model { get; }
    public TestOptions Options { get; } = new();
    public ServerRuntime Runtime { get; }
    public TestSession Session { get; } = new();

    public TestLiveSplit(int segmentCount = 3, ITimerFormBridge form = null)
    {
        var run = new Run(new StandardComparisonGeneratorsFactory())
        {
            GameName = "Test Game",
            CategoryName = "Any%",
        };

        for (int i = 0; i < segmentCount; i++)
        {
            run.AddSegment($"Split {i + 1}",
                pbSplitTime: new Time(TimeSpan.FromSeconds(10 * (i + 1)), TimeSpan.FromSeconds(9 * (i + 1))),
                bestSegmentTime: new Time(TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(7)));
        }

        // StandardSettingsFactory needs LiveSplit's component discovery, so only set what matters here.
        ISettings settings = new Options.Settings
        {
            HotkeyProfiles = new Dictionary<string, HotkeyProfile>
            {
                { HotkeyProfile.DefaultHotkeyProfileName, new HotkeyProfile() },
            },
            ComparisonGeneratorStates = new Dictionary<string, bool>(),
        };
        State = new LiveSplitState(run, null, null, null, settings)
        {
            CurrentComparison = Run.PersonalBestComparisonName,
            CurrentHotkeyProfile = HotkeyProfile.DefaultHotkeyProfileName,
        };

        Model = new TimerModel { CurrentState = State };
        Runtime = new ServerRuntime(State, Model, new InlineUiDispatcher(), form, Options, CommandDispatcher.CreateDefault(), "2.0.0");
    }

    public CommandContext Context => new(Runtime, Session);

    /// <summary>
    ///     Runs a request like a client would send it.
    /// </summary>
    public object Execute(string message)
    {
        return Runtime.Commands.Execute(Context, Request.Parse(message));
    }

    public CommandException ExecuteFailing(string message)
    {
        return Xunit.Assert.Throws<CommandException>(() => Execute(message));
    }

    /// <summary>
    ///     Starts the timer and waits a moment, since LiveSplit ignores splits at 0 ms.
    /// </summary>
    public void StartTimer()
    {
        Model.Start();
        Thread.Sleep(20);
    }
}
