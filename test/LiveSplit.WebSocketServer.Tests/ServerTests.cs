using LiveSplit.Model;
using LiveSplit.WsServer.Server;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using WebSocketSharp;
using Xunit;

namespace LiveSplit.WsServer.Tests;

/// <summary>
///     End to end tests over a real WebSocket connection. Commands run inline on the
///     websocket-sharp thread, which is fine without a window.
/// </summary>
public sealed class ServerTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly TestLiveSplit ls = new(segmentCount: 2);
    private readonly WebSocketHost host;
    private readonly int port;

    public ServerTests()
    {
        port = FreePort();
        host = new WebSocketHost(ls.Runtime);
        host.Start(IPAddress.Loopback, port);
    }

    public void Dispose()
    {
        host.Dispose();
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int free = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return free;
    }

    private sealed class Client : IDisposable
    {
        private readonly WebSocket socket;
        private readonly BlockingCollection<string> messages = [];

        public Client(int port, string query = "")
        {
            socket = new WebSocket($"ws://127.0.0.1:{port}/{query}");
            socket.OnMessage += (s, e) => messages.Add(e.Data);
            socket.Connect();
        }

        public bool IsOpen => socket.ReadyState == WebSocketState.Open;

        public void Send(string message)
        {
            socket.Send(message);
        }

        public JsonElement Receive()
        {
            Assert.True(messages.TryTake(out string message, Timeout), "No message received.");
            return JsonDocument.Parse(message).RootElement;
        }

        /// <summary>
        ///     Skips messages until one matches.
        /// </summary>
        public JsonElement ReceiveWhere(Func<JsonElement, bool> predicate)
        {
            for (int i = 0; i < 50; i++)
            {
                JsonElement message = Receive();
                if (predicate(message))
                {
                    return message;
                }
            }

            throw new InvalidOperationException("No matching message received.");
        }

        public bool TryReceive(TimeSpan wait, out JsonElement message)
        {
            message = default;
            if (!messages.TryTake(out string text, wait))
            {
                return false;
            }

            message = JsonDocument.Parse(text).RootElement;
            return true;
        }

        public bool WaitUntilClosed(TimeSpan wait)
        {
            DateTime end = DateTime.UtcNow + wait;
            while (DateTime.UtcNow < end)
            {
                if (socket.ReadyState is WebSocketState.Closed or WebSocketState.Closing)
                {
                    return true;
                }

                System.Threading.Thread.Sleep(20);
            }

            return false;
        }

        public bool NothingReceived(TimeSpan wait)
        {
            return !messages.TryTake(out _, wait);
        }

        public void Dispose()
        {
            socket.Close();
        }
    }

    private static string Type(JsonElement message)
    {
        return message.TryGetProperty("type", out JsonElement type) ? type.GetString() : null;
    }

    [Fact]
    public void Protocol1KeepsItsOriginalMessages()
    {
        using var client = new Client(port);

        JsonElement open = client.Receive();
        Assert.Equal("success", open.GetProperty("open").GetProperty("response").GetString());
        Assert.Equal("Test Game", open.GetProperty("state").GetProperty("run").GetProperty("gameName").GetString());

        client.Send("hi");
        Assert.Equal("hi", client.Receive().GetProperty("response").GetProperty("response").GetString());

        client.Send("{\"action\": \"state\"}");
        JsonElement state = client.Receive();
        Assert.Equal("state", state.GetProperty("response").GetProperty("response").GetString());
        Assert.Equal("NotRunning", state.GetProperty("state").GetProperty("timerState").GetString());

        // Control actions are not answered in protocol 1, and failures are silent.
        client.Send("split");
        client.Send("starttimer");
        Assert.True(client.NothingReceived(TimeSpan.FromMilliseconds(300)));
        Assert.Equal(TimerPhase.Running, ls.State.CurrentPhase);
    }

    [Fact]
    public void Protocol1ReceivesLegacyBroadcastsOnly()
    {
        using var client = new Client(port);
        client.Receive();

        host.Broadcast(ServerEvents.Start, null);
        host.Broadcast(ServerEvents.ComparisonChanged, new { comparison = "x" });
        host.Broadcast(ServerEvents.Scroll, "up");

        JsonElement start = client.Receive();
        Assert.Equal("start", start.GetProperty("action").GetProperty("action").GetString());
        Assert.True(start.TryGetProperty("state", out _));

        JsonElement scroll = client.Receive();
        Assert.Equal("scroll", scroll.GetProperty("action").GetProperty("action").GetString());
        Assert.Equal("up", scroll.GetProperty("action").GetProperty("data").GetString());
    }

    [Fact]
    public void Protocol2RepliesToEveryRequest()
    {
        using var client = new Client(port, "?protocol=2");

        JsonElement hello = client.Receive();
        Assert.Equal("hello", Type(hello));
        Assert.Equal(2, hello.GetProperty("protocolVersion").GetInt32());

        client.Send("{\"id\": 1, \"action\": \"start\"}");
        JsonElement started = client.ReceiveWhere(x => Type(x) == "response");
        Assert.Equal(1, started.GetProperty("id").GetInt32());
        Assert.True(started.GetProperty("ok").GetBoolean());
        Assert.Equal("Running", started.GetProperty("data").GetProperty("timerState").GetString());

        client.Send("{\"id\": \"two\", \"action\": \"start\"}");
        JsonElement failed = client.ReceiveWhere(x => Type(x) == "response");
        Assert.Equal("two", failed.GetProperty("id").GetString());
        Assert.False(failed.GetProperty("ok").GetBoolean());
        Assert.Equal("invalid_phase", failed.GetProperty("error").GetProperty("code").GetString());

        client.Send("not a command");
        Assert.Equal("unknown_action", client.ReceiveWhere(x => Type(x) == "response").GetProperty("error").GetProperty("code").GetString());

        client.Send("{broken");
        Assert.Equal("invalid_request", client.ReceiveWhere(x => Type(x) == "response").GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public void HelloUpgradesAProtocol1Session()
    {
        using var client = new Client(port);
        client.Receive();

        client.Send("{\"action\": \"hello\", \"args\": {\"protocol\": 2}}");
        JsonElement response = client.Receive();
        Assert.Equal("response", Type(response));
        Assert.Equal(2, response.GetProperty("data").GetProperty("protocolVersion").GetInt32());

        client.Send("split");
        Assert.Equal("invalid_phase", client.Receive().GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public void Protocol2EventsFollowTheSubscription()
    {
        using var client = new Client(port, "?protocol=2");
        client.Receive();

        client.Send("{\"action\": \"subscribe\", \"args\": {\"events\": [\"comparison-changed\"], \"includeState\": false}}");
        client.ReceiveWhere(x => Type(x) == "response");

        host.Broadcast(ServerEvents.Start, null);
        host.Broadcast(ServerEvents.ComparisonChanged, new { comparison = "Best Segments" });

        JsonElement message = client.Receive();
        Assert.Equal("event", Type(message));
        Assert.Equal("comparison-changed", message.GetProperty("event").GetString());
        Assert.Equal("Best Segments", message.GetProperty("data").GetProperty("comparison").GetString());
        Assert.False(message.TryGetProperty("state", out _));
    }

    [Fact]
    public void MessagesArriveInOrder()
    {
        using var client = new Client(port, "?protocol=2");
        client.Receive();
        client.Send("{\"action\": \"subscribe\", \"args\": {\"includeState\": false}}");
        client.ReceiveWhere(x => Type(x) == "response");

        for (int i = 0; i < 200; i++)
        {
            host.Broadcast(ServerEvents.Scroll, i);
        }

        for (int i = 0; i < 200; i++)
        {
            Assert.Equal(i, client.Receive().GetProperty("data").GetInt32());
        }
    }

    [Fact]
    public void Ticks()
    {
        using var client = new Client(port, "?protocol=2");
        client.Receive();

        client.Send("{\"action\": \"subscribe\", \"args\": {\"tickMs\": 100}}");
        client.ReceiveWhere(x => Type(x) == "response");

        host.SendTicks(DateTime.UtcNow);
        JsonElement tick = client.Receive();
        Assert.Equal("tick", Type(tick));
        Assert.Equal("NotRunning", tick.GetProperty("timerState").GetString());

        // Not due yet.
        host.SendTicks(DateTime.UtcNow);
        Assert.True(client.NothingReceived(TimeSpan.FromMilliseconds(200)));
    }

    [Fact]
    public void ReadOnlyIsReported()
    {
        ls.Options.ReadOnly = true;
        using var client = new Client(port, "?protocol=2");
        Assert.True(client.Receive().GetProperty("readOnly").GetBoolean());

        client.Send("start");
        Assert.Equal("read_only", client.Receive().GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(TimerPhase.NotRunning, ls.State.CurrentPhase);
    }

    [Fact]
    public void TokenIsRequiredWhenSet()
    {
        ls.Options.AuthToken = "secret";

        using (var rejected = new Client(port, "?protocol=2&token=wrong"))
        {
            // The server sends the error and closes the connection. websocket-sharp clients drop
            // messages that are still queued when the close arrives, so the error is optional here.
            if (rejected.TryReceive(TimeSpan.FromSeconds(1), out JsonElement error))
            {
                Assert.Equal("unauthorized", error.GetProperty("error").GetProperty("code").GetString());
            }

            Assert.True(rejected.WaitUntilClosed(Timeout));
            Assert.Empty(host.Sessions);
            Assert.Equal(TimerPhase.NotRunning, ls.State.CurrentPhase);
        }

        using var accepted = new Client(port, "?protocol=2&token=secret");
        Assert.Equal("hello", Type(accepted.Receive()));
    }

    [Theory]
    [InlineData("", "https://evil.example", true)]
    [InlineData("https://overlay.example", null, true)]
    [InlineData("https://overlay.example", "https://overlay.example/", true)]
    [InlineData("https://overlay.example", "https://evil.example", false)]
    [InlineData("*", "https://evil.example", true)]
    public void Origins(string allowed, string origin, bool expected)
    {
        string[] list = allowed.Length == 0 ? [] : [allowed];

        Assert.Equal(expected, ClientSession.IsOriginAllowed(list, origin));
    }

    [Fact]
    public void TokenComparison()
    {
        Assert.True(ClientSession.IsTokenValid("", null));
        Assert.True(ClientSession.IsTokenValid("abc", "abc"));
        Assert.False(ClientSession.IsTokenValid("abc", "abd"));
        Assert.False(ClientSession.IsTokenValid("abc", null));
        Assert.False(ClientSession.IsTokenValid("abc", "abcd"));
    }
}
