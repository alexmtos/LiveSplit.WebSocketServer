# LiveSplit WebSocket Server

A LiveSplit component that lets other programs and other computers read and control LiveSplit over a WebSocket connection, with JSON messages.

## Why use this instead of LiveSplit's built-in server?

LiveSplit has its own TCP and WebSocket server (Control → Start TCP/WebSocket Server). It answers text commands one at a time. This component:

- **Pushes events**: clients are told when the timer starts, splits, pauses, resets, when the splits or layout change, when the comparison or timing method changes, and more, from any source (hotkeys, auto splitters, other clients).
- **Sends the whole state as JSON**: run, segments, comparisons, metadata, custom variables, deltas, predictions... with every event, on request, and every 15 seconds.
- **Answers every request** with a typed response and an error code (protocol version 2), and matches responses to requests with ids.
- **Supports the same commands** as the built-in server (same names, also as plain text), plus more, for complete control of LiveSplit.
- Lets clients subscribe only to what they need, and receive a lightweight tick to draw a running timer.
- Can be restricted to this computer, protected with a token, limited to some web origins, or made read only.

## Install

This version targets the current LiveSplit (.NET Framework 4.8.1).

1. Download `LiveSplit.WebSocketServer.dll` from the releases of this repository (or from the artifact of the latest *Build* workflow run).
2. Put it in the `Components` folder of LiveSplit. `websocket-sharp.dll` already ships with LiveSplit; you no longer need to copy it.

## Setup

Add the component to the layout: *Edit Layout → + → Control → LiveSplit WebSocket Server*. In *Layout Settings*:

- **Start the server automatically** when the layout is loaded.
- **Port** (15721 by default).
- **Accept connections from**: *This computer only* (default for new components) or *Other devices on the network too*. Layouts made with earlier versions keep accepting connections from the network.
- **Token**: when set, clients must add `&token=...` to the URL. *Generate* creates a random one.
- **Allowed web origins**: when set, browsers can only connect from these origins (for example `https://my-overlay.example`), so other web pages cannot control LiveSplit. Programs that are not browsers are not affected.
- **Resend state every**: interval of the `refresh` broadcast (0 disables it).
- **Read only**: clients can read the state, but not control LiveSplit.
- **Allow clients to save and open splits, layouts and screenshots**: off by default.
- **Connect with**: the URL to use in your client.

Port and network changes apply the next time the server starts.

To start or stop the server by hand: right click LiveSplit → *Control* → *Start WebSocket Server (JSON)*.

## Protocol

Connect to `ws://127.0.0.1:15721/?protocol=2` and send JSON requests:

```json
{ "id": 1, "action": "startorsplit" }
{ "id": 2, "action": "setcomparison", "args": { "comparison": "Best Segments" } }
{ "id": 3, "action": "subscribe", "args": { "events": ["split", "reset"], "tickMs": 100 } }
```

Every request gets a response (`{ "type": "response", "id": 1, "ok": true, "data": ... }`), and events arrive as `{ "type": "event", "event": "split", "state": { ... } }`.

Clients written for earlier versions of this component (protocol version 1, without `?protocol=2`) keep working unchanged.

See [docs/PROTOCOL.md](docs/PROTOCOL.md) for the complete protocol, the state format, all events and all actions.

## Building

The project is built like LiveSplit's own components. Either:

- Clone this repository into `components/LiveSplit.WebSocketServer` of a LiveSplit checkout and run `dotnet build LiveSplit.WebSocketServer.slnx` there, or
- Build from anywhere by pointing to a LiveSplit checkout:

  ```
  dotnet build LiveSplit.WebSocketServer.slnx -p:LsSrcPath=<LiveSplit>/src -p:LsLibPath=<LiveSplit>/lib
  ```

The LiveSplit checkout needs the `lib/SpeedrunComSharp` submodule. `dotnet test` runs the tests.

## Testing against a running LiveSplit

`tools/test_server.py` checks every feature end to end over a real connection: both protocol versions, the state, every query, subscriptions and ticks, the token, origins and read only mode, and, when enabled, the timer, run editing, hotkeys and file actions.

```
pip install websocket-client
python tools/test_server.py                    # safe, read only checks
python tools/test_server.py --all              # also changes LiveSplit, then restores it
python tools/test_server.py --all --token XYZ --host 192.168.0.10
```

By default nothing in LiveSplit is changed. `--control` (timer; it must not be running), `--edit-run`, `--hotkeys` and `--files` enable the tests that change LiveSplit; they restore what they change, but the attempt count grows by one and the splits are left marked as modified. Run `python tools/test_server.py --help` for details. The script exits with 1 when a check fails.

## Credits

Originally created by [MeGotsThis](https://github.com/MeGotsThis/LiveSplit.WebSocketServer).
