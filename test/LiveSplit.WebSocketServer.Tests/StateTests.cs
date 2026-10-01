using LiveSplit.Model;
using LiveSplit.WsServer.Protocol;
using LiveSplit.WsServer.Server;
using LiveSplit.WsServer.State;
using System.Drawing;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace LiveSplit.WsServer.Tests;

public class SnapshotTests
{
    private static JsonElement Serialize(StateSnapshot snapshot)
    {
        return JsonDocument.Parse(Json.Serialize(snapshot)).RootElement;
    }

    [Fact]
    public void KeepsTheFieldsOfProtocolVersion1()
    {
        var ls = new TestLiveSplit(segmentCount: 2);
        JsonElement json = Serialize(ls.Runtime.Snapshots.Build(ls.State, SnapshotOptions.Legacy));

        string[] stateFields =
        [
            "run", "timerState", "currentComparison", "currentTimingMethod", "currentTime", "loadingTimes",
            "isGameTimeInitialized", "isGameTimePaused", "attemptStarted", "attemptEnded", "pauseTime",
            "currentAttemptDuration", "currentSplitIndex",
        ];
        Assert.All(stateFields, field => Assert.True(json.TryGetProperty(field, out _), field));

        JsonElement run = json.GetProperty("run");
        string[] runFields =
        [
            "gameIcon", "gameName", "categoryName", "startingOffset", "attemptCount", "finishedCount", "comparisons",
            "advancedSumOfBest", "totalPlaytime", "segments", "metadata",
        ];
        Assert.All(runFields, field => Assert.True(run.TryGetProperty(field, out _), field));

        JsonElement segment = run.GetProperty("segments")[0];
        Assert.Equal("Split 1", segment.GetProperty("name").GetString());
        Assert.Equal(10000, segment.GetProperty("personalBest").GetProperty("realTime").GetInt64());
        Assert.Equal(9000, segment.GetProperty("personalBest").GetProperty("gameTime").GetInt64());
        Assert.Equal(10000, segment.GetProperty("comparisons").GetProperty(Run.PersonalBestComparisonName).GetProperty("realTime").GetInt64());

        string[] metadataFields = ["gameId", "categoryId", "regionId", "platformId", "emulator", "variables"];
        Assert.All(metadataFields, field => Assert.True(run.GetProperty("metadata").TryGetProperty(field, out _), field));

        Assert.Equal("NotRunning", json.GetProperty("timerState").GetString());
        Assert.Equal("Test Game", run.GetProperty("gameName").GetString());
    }

    [Fact]
    public void IconsOnlyWhenRequested()
    {
        var ls = new TestLiveSplit(segmentCount: 1);
        ls.State.Run[0].Icon = new Bitmap(2, 2);

        Assert.Null(ls.Runtime.Snapshots.Build(ls.State, SnapshotOptions.Default).Run.Segments[0].Icon);
        string icon = ls.Runtime.Snapshots.Build(ls.State, SnapshotOptions.Legacy).Run.Segments[0].Icon;
        Assert.StartsWith("data:image/png;base64,", icon);
    }

    [Fact]
    public void HistoryOnlyWhenRequested()
    {
        var ls = new TestLiveSplit(segmentCount: 1);
        ls.StartTimer();
        ls.Model.Split();
        ls.Model.Reset();

        Assert.Null(ls.Runtime.Snapshots.Build(ls.State, SnapshotOptions.Default).Run.AttemptHistory);
        StateSnapshot withHistory = ls.Runtime.Snapshots.Build(ls.State, new SnapshotOptions(IncludeIcons: false, IncludeHistory: true));
        Assert.Single(withHistory.Run.AttemptHistory);
        Assert.NotEmpty(withHistory.Run.Segments[0].SegmentHistory);

        string json = Json.Serialize(ls.Runtime.Snapshots.Build(ls.State, SnapshotOptions.Default));
        Assert.DoesNotContain("attemptHistory", json);
    }

    [Fact]
    public void CustomVariablesAndDerivedTimes()
    {
        var ls = new TestLiveSplit(segmentCount: 2);
        ls.State.Run.Metadata.SetCustomVariable("deaths", "4");

        StateSnapshot snapshot = ls.Runtime.Snapshots.Build(ls.State, SnapshotOptions.Default);

        Assert.Equal("4", snapshot.Run.Metadata.CustomVariables["deaths"].Value);
        Assert.Equal(20000, snapshot.PredictedTime);
        Assert.Equal(Options.HotkeyProfile.DefaultHotkeyProfileName, snapshot.CurrentHotkeyProfile);
        Assert.Contains(Options.HotkeyProfile.DefaultHotkeyProfileName, snapshot.HotkeyProfiles);
    }
}

public class StateChangeDetectorTests
{
    [Fact]
    public void FirstCallOnlyRecords()
    {
        var ls = new TestLiveSplit();

        Assert.Empty(new StateChangeDetector().Detect(ls.State));
    }

    [Fact]
    public void DetectsChangesWithoutLiveSplitEvents()
    {
        var ls = new TestLiveSplit();
        var detector = new StateChangeDetector();
        detector.Detect(ls.State);

        ls.State.CurrentComparison = "Best Segments";
        ls.State.CurrentTimingMethod = TimingMethod.GameTime;
        ls.State.Run.Metadata.SetCustomVariable("deaths", "1");
        ls.State.IsGameTimePaused = true;

        string[] events = detector.Detect(ls.State).Select(x => x.Event).ToArray();

        Assert.Contains(ServerEvents.ComparisonChanged, events);
        Assert.Contains(ServerEvents.TimingMethodChanged, events);
        Assert.Contains(ServerEvents.CustomVariableChanged, events);
        Assert.Contains(ServerEvents.GameTimePaused, events);
        Assert.Empty(detector.Detect(ls.State));
    }

    [Fact]
    public void DetectsANewRunAndSaves()
    {
        var ls = new TestLiveSplit();
        var detector = new StateChangeDetector();
        ls.State.Run.HasChanged = true;
        detector.Detect(ls.State);

        ls.State.Run.HasChanged = false;
        Assert.Equal([ServerEvents.RunSaved], detector.Detect(ls.State).Select(x => x.Event));

        ls.State.Run = new TestLiveSplit().State.Run;
        Assert.Equal([ServerEvents.RunChanged], detector.Detect(ls.State).Select(x => x.Event));
    }
}
