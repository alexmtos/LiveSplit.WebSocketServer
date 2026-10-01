using LiveSplit.WsServer.Protocol;
using System;
using System.Text.Json;
using Xunit;

namespace LiveSplit.WsServer.Tests;

public class RequestTests
{
    [Fact]
    public void PlainTextWithoutArguments()
    {
        Request request = Request.Parse("  Split ");

        Assert.Equal("split", request.Action);
        Assert.Null(request.Id);
        Assert.False(request.Args.IsText);
    }

    [Fact]
    public void PlainTextKeepsEverythingAfterTheActionAsText()
    {
        Request request = Request.Parse("setcomparison Personal Best");

        Assert.Equal("setcomparison", request.Action);
        Assert.Equal("Personal Best", request.Args.Text);
        Assert.Equal("Personal Best", request.Args.GetString("comparison"));
    }

    [Fact]
    public void JsonWithIdAndArgs()
    {
        Request request = Request.Parse("{\"id\": 7, \"action\": \"SETGAMETIME\", \"args\": {\"time\": 1500}}");

        Assert.Equal("setgametime", request.Action);
        Assert.Equal(7, request.Id.Value.GetInt32());
        Assert.Equal(TimeSpan.FromMilliseconds(1500), request.Args.GetTime("time"));
    }

    [Fact]
    public void JsonStringId()
    {
        Request request = Request.Parse("{\"id\": \"abc\", \"action\": \"ping\"}");

        Assert.Equal("abc", request.Id.Value.GetString());
    }

    [Fact]
    public void LegacyDataFieldIsAcceptedAsArgs()
    {
        Request request = Request.Parse("{\"action\": \"setcomparison\", \"data\": {\"comparison\": \"Best Segments\"}}");

        Assert.Equal("Best Segments", request.Args.GetString("comparison"));
    }

    [Fact]
    public void StringArgsAreText()
    {
        Request request = Request.Parse("{\"action\": \"setcomparison\", \"args\": \"Best Segments\"}");

        Assert.Equal("Best Segments", request.Args.Text);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{not json")]
    [InlineData("{\"id\": 1}")]
    [InlineData("{\"action\": 5}")]
    [InlineData("{\"action\": \"split\", \"id\": {}}")]
    public void InvalidMessagesAreRejected(string message)
    {
        Assert.Throws<RequestParseException>(() => Request.Parse(message));
    }

    [Fact]
    public void ParseErrorsKeepTheIdWhenKnown()
    {
        RequestParseException error = Assert.Throws<RequestParseException>(() => Request.Parse("{\"id\": 3}"));

        Assert.Equal(3, error.Id.Value.GetInt32());
    }
}

public class CommandArgsTests
{
    private static CommandArgs Json(string json)
    {
        return CommandArgs.FromJson(JsonDocument.Parse(json).RootElement);
    }

    [Theory]
    [InlineData("1:23.456", 83456)]
    [InlineData("1:00:00", 3600000)]
    [InlineData("12.5", 12500)]
    public void TimesAsText(string text, long milliseconds)
    {
        Assert.Equal(TimeSpan.FromMilliseconds(milliseconds), CommandArgs.FromText(text).GetTime("time"));
    }

    [Fact]
    public void TimesAsMilliseconds()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(2500), Json("{\"time\": 2500}").GetTime("time"));
    }

    [Fact]
    public void DashMeansNoTime()
    {
        Assert.Null(CommandArgs.FromText("-").GetTime("time"));
    }

    [Fact]
    public void InvalidTimeIsAnArgumentError()
    {
        CommandException error = Assert.Throws<CommandException>(() => CommandArgs.FromText("soon").GetTime("time"));

        Assert.Equal(ErrorCodes.InvalidArgs, error.Code);
    }

    [Fact]
    public void NamesAreCaseInsensitive()
    {
        Assert.Equal("x", Json("{\"Name\": \"x\"}").GetString("name"));
    }

    [Fact]
    public void ArraysArePositional()
    {
        CommandArgs args = Json("[\"a\", 2, true]");

        Assert.Equal("a", args.GetString("first", 0));
        Assert.Equal(2, args.GetInt("second", 1));
        Assert.True(args.GetBool("third", 2));
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("off", false)]
    [InlineData("1", true)]
    public void BooleansAsText(string text, bool expected)
    {
        Assert.Equal(expected, CommandArgs.FromText(text).GetBool("value"));
    }

    [Fact]
    public void StringLists()
    {
        Assert.Equal(["split", "reset"], Json("{\"events\": [\"split\", \"reset\"]}").GetStringList("events"));
        Assert.Equal(["split", "reset"], CommandArgs.FromText("split, reset").GetStringList("events"));
    }

    [Fact]
    public void RequireStringFailsWhenMissing()
    {
        CommandException error = Assert.Throws<CommandException>(() => CommandArgs.Empty.RequireString("name"));

        Assert.Equal(ErrorCodes.InvalidArgs, error.Code);
    }
}
