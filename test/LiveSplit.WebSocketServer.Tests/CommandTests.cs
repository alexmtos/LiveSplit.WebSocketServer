using LiveSplit.Model;
using LiveSplit.WsServer.Commands;
using LiveSplit.WsServer.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace LiveSplit.WsServer.Tests;

public class DispatcherTests
{
    [Fact]
    public void UnknownActionsAreReported()
    {
        var ls = new TestLiveSplit();

        Assert.Equal(ErrorCodes.UnknownAction, ls.ExecuteFailing("fly").Code);
    }

    [Fact]
    public void ReadOnlyBlocksControlActionsButNotQueries()
    {
        var ls = new TestLiveSplit();
        ls.Options.ReadOnly = true;

        Assert.Equal(ErrorCodes.ReadOnly, ls.ExecuteFailing("start").Code);
        Assert.Equal(TimerPhase.NotRunning, ls.State.CurrentPhase);
        Assert.Equal("NotRunning", ls.Execute("gettimerphase"));
        Assert.Equal("pong", ls.Execute("ping"));
    }

    [Fact]
    public void FileActionsNeedToBeAllowed()
    {
        var ls = new TestLiveSplit();

        Assert.Equal(ErrorCodes.Forbidden, ls.ExecuteFailing("savesplits").Code);

        ls.Options.ReadOnly = true;
        ls.Options.AllowFileCommands = true;
        Assert.Equal(ErrorCodes.ReadOnly, ls.ExecuteFailing("savesplits").Code);
    }

    [Fact]
    public void FileActionsWithoutTheMainWindowAreUnsupported()
    {
        var ls = new TestLiveSplit();
        ls.Options.AllowFileCommands = true;

        Assert.Equal(ErrorCodes.Unsupported, ls.ExecuteFailing("savesplits").Code);
    }

    [Fact]
    public void BuiltInServerAliasesWork()
    {
        var ls = new TestLiveSplit();

        ls.Execute("starttimer");
        Assert.Equal(TimerPhase.Running, ls.State.CurrentPhase);

        ls.Execute("switchto gametime");
        Assert.Equal(TimingMethod.GameTime, ls.State.CurrentTimingMethod);
    }

    [Fact]
    public void EveryActionAndAliasIsUnique()
    {
        CommandDispatcher dispatcher = CommandDispatcher.CreateDefault();
        var names = dispatcher.Definitions.SelectMany(x => new[] { x.Name }.Concat(x.Aliases)).ToList();

        Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(dispatcher.Definitions, x => Assert.False(string.IsNullOrWhiteSpace(x.Description)));
    }

    [Fact]
    public void EveryCommandOfTheBuiltInServerExists()
    {
        CommandDispatcher dispatcher = CommandDispatcher.CreateDefault();
        string[] builtIn =
        [
            "startorsplit", "split", "undosplit", "unsplit", "skipsplit", "pause", "undoallpauses", "resume", "reset",
            "start", "starttimer", "setgametime", "setloadingtimes", "addloadingtimes", "pausegametime", "unpausegametime",
            "alwayspausegametime", "getgamename", "getcategoryname", "getcategoryvariables", "getdelta", "getsplitindex",
            "getsplitcount", "getsplitname", "getcurrentsplitname", "getlastsplitname", "getprevioussplitname",
            "getnextsplitname", "getupcomingsplitname", "getlastsplittime", "getprevioussplittime", "getcurrentsplittime",
            "getcomparisonsplittime", "getcurrentrealtime", "getcurrentgametime", "getcurrenttime", "getfinaltime",
            "getfinalsplittime", "getbestpossibletime", "getpredictedtime", "getpausedrealtime", "getpausedgametime",
            "getoffset", "gettimerphase", "getcurrenttimerphase", "getcomparisonname", "setcomparison", "switchto",
            "gettimingmethod", "setsplitname", "setcurrentsplitname", "getcustomvariablevalue", "setcustomvariable",
            "globalhotkeysenabled", "enableglobalhotkeys", "disableglobalhotkeys", "switchhotkeyprofile", "ping",
            "getlayoutpath", "savelayout", "savelayoutas", "getsplitspath", "savesplits", "savesplitsas", "switchlayout",
            "switchsplits", "getsplitsscreenshot", "savesplitsscreenshot", "getattemptcount", "getcompletedcount",
            "getautosplitterpath", "autosplitteractivated", "gethotkeyprofile", "getlivesplitversion", "getlivesplitpath",
            "getservertype",
        ];

        Assert.All(builtIn, name => Assert.True(dispatcher.TryGet(name, out _), name));
    }
}

public class TimerCommandTests
{
    [Fact]
    public void StartSplitAndFinish()
    {
        var ls = new TestLiveSplit(segmentCount: 2);

        ls.Execute("start");
        System.Threading.Thread.Sleep(20);
        ls.Execute("split");
        Assert.Equal(1, ls.State.CurrentSplitIndex);

        System.Threading.Thread.Sleep(20);
        ls.Execute("{\"action\": \"split\"}");
        Assert.Equal(TimerPhase.Ended, ls.State.CurrentPhase);
    }

    [Fact]
    public void SplitWhileNotRunningIsAPhaseError()
    {
        var ls = new TestLiveSplit();

        Assert.Equal(ErrorCodes.InvalidPhase, ls.ExecuteFailing("split").Code);
        Assert.Equal(ErrorCodes.InvalidPhase, ls.ExecuteFailing("reset").Code);
        Assert.Equal(ErrorCodes.InvalidPhase, ls.ExecuteFailing("undosplit").Code);
    }

    [Fact]
    public void PauseResumeAndToggle()
    {
        var ls = new TestLiveSplit();
        ls.StartTimer();

        ls.Execute("pause");
        Assert.Equal(TimerPhase.Paused, ls.State.CurrentPhase);
        ls.Execute("pause");
        Assert.Equal(TimerPhase.Paused, ls.State.CurrentPhase);

        ls.Execute("resume");
        Assert.Equal(TimerPhase.Running, ls.State.CurrentPhase);

        ls.Execute("togglepause");
        Assert.Equal(TimerPhase.Paused, ls.State.CurrentPhase);
    }

    [Fact]
    public void PauseDoesNotStartTheTimer()
    {
        var ls = new TestLiveSplit();

        Assert.Equal(ErrorCodes.InvalidPhase, ls.ExecuteFailing("pause").Code);
        Assert.Equal(TimerPhase.NotRunning, ls.State.CurrentPhase);
    }

    [Fact]
    public void ResetWithoutSavingKeepsThePersonalBest()
    {
        var ls = new TestLiveSplit(segmentCount: 1);
        TimeSpan? personalBest = ls.State.Run[0].PersonalBestSplitTime.RealTime;
        ls.StartTimer();
        ls.Execute("split");

        ls.Execute("{\"action\": \"reset\", \"args\": {\"save\": false}}");

        Assert.Equal(TimerPhase.NotRunning, ls.State.CurrentPhase);
        Assert.Equal(personalBest, ls.State.Run[0].PersonalBestSplitTime.RealTime);
    }

    [Fact]
    public void SkipAndUndo()
    {
        var ls = new TestLiveSplit(segmentCount: 3);
        ls.StartTimer();

        ls.Execute("skipsplit");
        Assert.Equal(1, ls.State.CurrentSplitIndex);

        ls.Execute("unsplit");
        Assert.Equal(0, ls.State.CurrentSplitIndex);
    }

    [Fact]
    public void GameTime()
    {
        var ls = new TestLiveSplit();
        ls.StartTimer();

        ls.Execute("initgametime");
        Assert.True(ls.State.IsGameTimeInitialized);

        ls.Execute("pausegametime");
        Assert.True(ls.State.IsGameTimePaused);

        ls.Execute("setgametime 0:05");
        Assert.Equal(TimeSpan.FromSeconds(5), ls.State.CurrentTime.GameTime);

        ls.Execute("unpausegametime");
        Assert.False(ls.State.IsGameTimePaused);
    }

    [Fact]
    public void AlwaysPauseGameTimePausesOnEveryStart()
    {
        var ls = new TestLiveSplit();

        ls.Execute("alwayspausegametime");

        Assert.True(ls.Runtime.AlwaysPauseGameTime);
        Assert.True(ls.State.IsGameTimePaused);

        ls.Execute("unpausegametime");
        Assert.False(ls.Runtime.AlwaysPauseGameTime);
    }

    [Fact]
    public void Comparisons()
    {
        var ls = new TestLiveSplit();

        ls.Execute("setcomparison best segments");
        Assert.Equal("Best Segments", ls.State.CurrentComparison);

        Assert.Equal(ErrorCodes.InvalidArgs, ls.ExecuteFailing("setcomparison Nope").Code);

        ls.Execute("switchcomparisonnext");
        Assert.NotEqual("Best Segments", ls.State.CurrentComparison);
    }

    [Fact]
    public void TimingMethod()
    {
        var ls = new TestLiveSplit();

        ls.Execute("{\"action\": \"settimingmethod\", \"args\": {\"method\": \"gametime\"}}");
        Assert.Equal(Model.TimingMethod.GameTime, ls.State.CurrentTimingMethod);

        Assert.Equal(ErrorCodes.InvalidArgs, ls.ExecuteFailing("settimingmethod sundial").Code);
    }
}

public class RunCommandTests
{
    [Fact]
    public void RenameSplitsWithTextJsonAndNegativeIndices()
    {
        var ls = new TestLiveSplit(segmentCount: 3);

        ls.Execute("setsplitname 0 First");
        ls.Execute("{\"action\": \"setsplitname\", \"args\": {\"index\": -1, \"name\": \"Last\"}}");
        ls.Execute("{\"action\": \"setsplitname\", \"args\": [1, \"Middle\"]}");

        Assert.Equal(["First", "Middle", "Last"], ls.State.Run.Select(x => x.Name));
        Assert.True(ls.State.Run.HasChanged);
        Assert.Equal(ErrorCodes.InvalidArgs, ls.ExecuteFailing("setsplitname 5 Nope").Code);
    }

    [Fact]
    public void CustomVariablesInBothFormats()
    {
        var ls = new TestLiveSplit();

        ls.Execute("setcustomvariable [\"deaths\", \"3\"]");
        ls.Execute("{\"action\": \"setcustomvariable\", \"args\": {\"name\": \"route\", \"value\": \"glitchless\"}}");

        Assert.Equal("3", ls.Execute("getcustomvariablevalue deaths"));
        Assert.Equal("glitchless", ls.State.Run.Metadata.CustomVariableValue("route"));
    }

    [Fact]
    public void OffsetOnlyWhileNotRunning()
    {
        var ls = new TestLiveSplit();

        ls.Execute("setoffset -1.5");
        Assert.Equal(TimeSpan.FromSeconds(-1.5), ls.State.Run.Offset);

        ls.StartTimer();
        Assert.Equal(ErrorCodes.InvalidPhase, ls.ExecuteFailing("setoffset 0").Code);
    }

    [Fact]
    public void GameAndCategoryNames()
    {
        var ls = new TestLiveSplit();

        ls.Execute("setgamename Another Game");
        ls.Execute("setcategoryname 100%");

        Assert.Equal("Another Game", ls.Execute("getgamename"));
        Assert.Equal("100%", ls.Execute("getcategoryname"));
    }
}

public class QueryCommandTests
{
    [Fact]
    public void SplitQueries()
    {
        var ls = new TestLiveSplit(segmentCount: 3);

        Assert.Equal(3, ls.Execute("getsplitcount"));
        Assert.Equal(-1, ls.Execute("getsplitindex"));
        Assert.Equal("Split 3", ls.Execute("getsplitname -1"));
        Assert.Null(ls.Execute("getcurrentsplitname"));

        ls.StartTimer();
        Assert.Equal("Split 1", ls.Execute("getcurrentsplitname"));
        Assert.Equal("Split 2", ls.Execute("getnextsplitname"));
        Assert.Null(ls.Execute("getprevioussplitname"));
    }

    [Fact]
    public void ReadingAnUnknownCustomVariableDoesNotCreateIt()
    {
        var ls = new TestLiveSplit();
        ls.Options.ReadOnly = true;

        Assert.Null(ls.Execute("getcustomvariablevalue nope"));
        Assert.False(ls.State.Run.Metadata.CustomVariables.ContainsKey("nope"));
    }

    [Fact]
    public void SplitQueriesDefaultToTheCurrentSplit()
    {
        var ls = new TestLiveSplit(segmentCount: 3);

        // -1 while not running must not be read as "the last split".
        Assert.Null(ls.Execute("getsplitname"));
        Assert.Equal(ErrorCodes.InvalidArgs, ls.ExecuteFailing("getsegment").Code);

        ls.StartTimer();
        Assert.Equal("Split 1", ls.Execute("getsplitname"));
    }

    [Fact]
    public void TimesAreMilliseconds()
    {
        var ls = new TestLiveSplit(segmentCount: 3);

        Assert.Equal(0L, ls.Execute("getcurrenttime"));
        Assert.Equal(30000L, ls.Execute("getfinaltime"));
        Assert.Equal(30000L, ls.Execute("getpredictedtime"));
        Assert.Null(ls.Execute("getdelta"));
    }

    [Fact]
    public void HotkeyQueriesAndProfiles()
    {
        var ls = new TestLiveSplit();

        Assert.Equal(Options.HotkeyProfile.DefaultHotkeyProfileName, ls.Execute("gethotkeyprofile"));
        Assert.Contains(Options.HotkeyProfile.DefaultHotkeyProfileName, (IEnumerable<string>)ls.Execute("gethotkeyprofiles"));

        ls.Execute("enableglobalhotkeys");
        Assert.Equal(true, ls.Execute("globalhotkeysenabled"));

        // Switching profiles needs the main window to re-register the hotkeys.
        Assert.Equal(ErrorCodes.Unsupported, ls.ExecuteFailing($"switchhotkeyprofile {Options.HotkeyProfile.DefaultHotkeyProfileName}").Code);
        Assert.Equal(ErrorCodes.InvalidArgs, ls.ExecuteFailing("switchhotkeyprofile Nope").Code);
    }
}

public class SessionCommandTests
{
    [Fact]
    public void HelloSwitchesTheProtocol()
    {
        var ls = new TestLiveSplit();
        ls.Session.ProtocolVersion = ProtocolVersion.Legacy;

        var hello = (HelloMessage)ls.Execute("{\"action\": \"hello\", \"args\": {\"protocol\": 2}}");

        Assert.Equal(ProtocolVersion.Current, ls.Session.ProtocolVersion);
        Assert.Equal(ProtocolVersion.Current, hello.ProtocolVersion);
        Assert.NotNull(hello.State);
        Assert.Equal(ErrorCodes.InvalidArgs, ls.ExecuteFailing("{\"action\": \"hello\", \"args\": {\"protocol\": 9}}").Code);
    }

    [Fact]
    public void SubscriptionsNeedProtocol2()
    {
        var ls = new TestLiveSplit();
        ls.Session.ProtocolVersion = ProtocolVersion.Legacy;

        Assert.Equal(ErrorCodes.InvalidRequest, ls.ExecuteFailing("subscribe").Code);
    }

    [Fact]
    public void Subscribe()
    {
        var ls = new TestLiveSplit();

        ls.Execute("{\"action\": \"subscribe\", \"args\": {\"events\": [\"split\", \"reset\"], \"includeState\": false, \"tickMs\": 10}}");

        Assert.True(ls.Session.Subscription.Wants("split"));
        Assert.False(ls.Session.Subscription.Wants("pause"));
        Assert.False(ls.Session.Subscription.IncludeState);
        Assert.Equal(TimeSpan.FromMilliseconds(SessionCommands.MinimumTickMs), ls.Session.Subscription.TickInterval);

        ls.Execute("{\"action\": \"unsubscribe\", \"args\": {\"events\": [\"split\"]}}");
        Assert.False(ls.Session.Subscription.Wants("split"));
        Assert.True(ls.Session.Subscription.Wants("reset"));

        ls.Execute("{\"action\": \"subscribe\", \"args\": {\"events\": \"all\", \"tickMs\": 0}}");
        Assert.True(ls.Session.Subscription.Wants("pause"));
        Assert.Null(ls.Session.Subscription.TickInterval);

        Assert.Equal(ErrorCodes.InvalidArgs, ls.ExecuteFailing("{\"action\": \"subscribe\", \"args\": {\"events\": [\"nope\"]}}").Code);
    }
}
