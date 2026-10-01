# CLAUDE.md

Guidance for Claude Code (and other contributors) working in this repository.

## What this is

A LiveSplit **component** (a .NET Framework 4.8.1 WinForms DLL loaded by LiveSplit from its `Components` folder) that runs a WebSocket server. Clients read the full timer state as JSON, receive pushed events, and control everything LiveSplit can do. It is independent from LiveSplit's built-in TCP/WebSocket server (`CommandServer`), but accepts the same command names.

- User documentation: [README.md](README.md)
- Wire protocol (both versions, every action, event and state field): [docs/PROTOCOL.md](docs/PROTOCOL.md)
- History: [CHANGELOG.md](CHANGELOG.md)

## Repository layout

```
Directory.Build.props            Imports LiveSplit's props when this repo is a submodule (components/…), LangVersion 14
LiveSplit.WebSocketServer.slnx   Solution: component + tests
.editorconfig                    Copied from LiveSplit (file-scoped namespaces, braces always, var only when obvious)
src/LiveSplit.WebSocketServer/
  UI/Components/                 LiveSplit integration: ServerFactory (IComponentFactory), ServerComponent (IComponent), Settings (UserControl + Designer)
  Infrastructure/                IUiDispatcher: FormUiDispatcher (real UI thread), InlineUiDispatcher (tests)
  Server/                        WebSocketHost (websocket-sharp server, broadcast, ticks), ClientSession (one connection),
                                 ServerRuntime (shared state), SessionSubscription, ServerEvents (event names)
  Protocol/                      Request parsing, CommandArgs, messages (hello/response/event/tick), ErrorCodes, Json options
  Commands/                      CommandDispatcher + one static class per area: Session, Timer, Run, Hotkey, File, Query
  State/                         StateSnapshotBuilder (+ DTOs), TimerCalculations, StateChangeDetector, IconCache
  Interop/                       TimerFormBridge: reflection on LiveSplit's TimerForm for save/open/screenshot/hotkeys
test/LiveSplit.WebSocketServer.Tests/   xUnit tests with a real LiveSplitState/TimerModel and real WebSocket connections
tools/test_server.py             End-to-end test script against a running LiveSplit (websocket-client)
.github/workflows/build.yml      CI: build + test on Windows against the latest LiveSplit release and LiveSplit master
```

Namespaces: new code lives in `LiveSplit.WsServer.*` (not `LiveSplit.WebSocketServer.*`, which would clash with websocket-sharp's `WebSocketServer` class). The LiveSplit-facing classes stay in `LiveSplit.UI.Components`, as LiveSplit expects.

## Building

The project references LiveSplit's sources (`LiveSplit.Core`, `UpdateManager`) through `$(LsSrcPath)`/`$(LsLibPath)`, like the official components. It needs a LiveSplit checkout with the `lib/SpeedrunComSharp` submodule.

```
git clone https://github.com/LiveSplit/LiveSplit.git
cd LiveSplit
git checkout <latest release tag, e.g. 1.8.37>      # NOT master, see below
git submodule update --init --depth 1 lib/SpeedrunComSharp

# Option A: as LiveSplit does it
git clone <this repo> components/LiveSplit.WebSocketServer
dotnet build components/LiveSplit.WebSocketServer/LiveSplit.WebSocketServer.slnx -c Release

# Option B: from anywhere
dotnet build LiveSplit.WebSocketServer.slnx -c Release -p:LsSrcPath=<LiveSplit>/src -p:LsLibPath=<LiveSplit>/lib
```

On Linux add `-p:EnableWindowsTargeting=true` (the .NET 10 SDK builds net4.8.1 WinForms code there; `apt install dotnet-sdk-10.0`).

**Build against a LiveSplit release tag, not master.** The component does not ship its dependencies: `websocket-sharp`, `System.Text.Json`, `SpeedrunComSharp` and `LiveSplit.Core` are referenced with `Private=false`/`ExcludeAssets=runtime` and loaded from LiveSplit's folder. A build made against LiveSplit `master` can reference newer versions (for example System.Text.Json 10 instead of 9) than the released LiveSplit has; it then loads, but every message fails. `ServerComponent.FindMissingDependency` detects this and refuses to start the server with an error dialog. A build works with the LiveSplit version it was built against and later ones.

## Testing

```
dotnet test LiveSplit.WebSocketServer.slnx -c Release          # Windows
```

On Linux, run the xUnit console runner under Mono (`apt install mono-complete`, runner from the `xunit.runner.console` NuGet package):

```
mono xunit.console.exe test/LiveSplit.WebSocketServer.Tests/bin/Release/net4.8.1/LiveSplit.WebSocketServer.Tests.dll -parallel none -noappdomain
```

Mono may not exit after the summary when outbound UDP is blocked: LiveSplit's `TimeStamp` starts an NTP query that never returns. Wrap the run in `timeout`. This does not happen on Windows/CI.

Tests build a real `LiveSplitState` without a window (`TestLiveSplit`). Do not use `StandardSettingsFactory` in tests: it needs LiveSplit's component discovery. WebSocket tests use websocket-sharp's client, which drops queued messages when the close frame arrives first; assert on the closed connection rather than on a final message, and use a `ping` as an ordering barrier instead of sleeps.

`tools/test_server.py` exercises every feature against a running LiveSplit (`--all` enables the tests that change LiveSplit and restore it). Keep it in sync when adding actions or events.

## Architecture rules

1. **Everything that touches LiveSplit runs on the UI thread.** websocket-sharp calls `OnOpen`/`OnMessage` on worker threads; `ClientSession` marshals through `IUiDispatcher.InvokeAsync`. Timer events may come from other threads (auto splitters), so `ServerComponent.SendState` posts too. `FormUiDispatcher` captures the UI thread's `WindowsFormsSynchronizationContext` at construction; do not go back to `Control.InvokeRequired`, which is false when the form has no handle.
2. **Never block the UI thread.** No synchronous sends (use `ClientSession.SendText`), no `SynchronizationContext.Send`, no network. `RunMetadata.Game`, `.Category` and `.VariableValues` can download from speedrun.com: only read them when `GameAvailable`/`CategoryAvailable`, and resolve variable ids in the background (see `StateSnapshotBuilder.SpeedrunComVariables`). `RunMetadata.CustomVariableValue` *creates* missing variables; read `CustomVariables` instead.
3. **Messages to a client are ordered.** Each `ClientSession` has one outgoing queue drained by one worker. The greeting and responses are queued on the UI thread, like broadcasts, so a response always follows the events its action raised. Never call websocket-sharp's `SendAsync`/`Broadcast` directly (they reorder). Ticks are `droppable`; 1000 pending messages disconnect the client.
4. **Protocol version 1 must not change.** Version 1 (default, no `?protocol=2`) clients get `{ open, state }`, replies only to `hi` and `state`, silence for the original control actions (even on failure), and `{ action: { action, data }, state }` broadcasts for `ServerEvents.Legacy` only, with icons. The legacy state field names are part of that contract (`SnapshotTests.KeepsTheFieldsOfProtocolVersion1`). New fields may be added.
5. **Access is enforced only in `CommandDispatcher.CheckAccess`**, from each command's `CommandAccess`: `Read`/`Session` always allowed, `Control` blocked in read only mode, `File` also needs `AllowFileCommands`.
6. **Stay compatible with LiveSplit's built-in server**: same action names (as name or alias) and the same plain-text argument formats (`setsplitname <index> <name>`, `setcustomvariable ["name","value"]`). `DispatcherTests.EveryCommandOfTheBuiltInServerExists` lists them. Results differ on purpose: milliseconds instead of formatted times, `null` instead of `-`.
7. **TimerForm operations go through `ITimerFormBridge`.** LiveSplit keeps `SaveSplits`, `OpenRunFromFile`, `MakeScreenShot`, `RefreshHotkeyHooks`... private; `TimerFormBridge` finds them by name and signature. If a LiveSplit update renames one, the capability turns off and the command answers `unsupported`; check the signatures in `src/LiveSplit.View/View/TimerForm.cs` of the target LiveSplit.

## How to…

**Add an action**: register it in the matching `Commands/*Commands.cs` with a name, `CommandAccess`, a description (shown by `help` and copied into docs/PROTOCOL.md) and optional aliases. Handlers run on the UI thread, read arguments through `CommandArgs` (`GetString/GetInt/GetBool/GetTime/RequireX`, which also accept the plain-text form), throw `CommandException` (`InvalidArgs`, `InvalidPhase`, `Unsupported`…) for expected failures and return the response `data` (or `null`). Add tests in `CommandTests.cs`, a row in docs/PROTOCOL.md, and a check in `tools/test_server.py`.

**Add an event**: add the name to `ServerEvents` (and to `All`). Raise it from a LiveSplit event in `ServerComponent`, or detect it in `StateChangeDetector` when LiveSplit has no event for it. Only add it to `ServerEvents.Legacy` if version 1 clients must receive it (they normally must not). Document it in docs/PROTOCOL.md and update `ALL_EVENTS` in `tools/test_server.py`.

**Add a state field**: add it to the DTOs in `State/StateSnapshot.cs` and fill it in `StateSnapshotBuilder` (UI thread, no blocking calls; wrap fragile calculations in `Safe`). Never rename or remove existing fields. Document it in docs/PROTOCOL.md.

**Add a setting**: property on `Settings`, a control in `Settings.Designer.cs`, `GetSettings`/`SetSettings`/`GetSettingsHashCode` in `Settings.cs`. Choose the default for missing XML so that existing layouts keep their old behavior (see `BindMode`). Expose it to commands through `IServerOptions` if needed.

## Conventions

- C# 14, file-scoped namespaces, braces on every block, explicit types unless the type is obvious, `Log` from `LiveSplit.Options` for errors (it writes to the Windows Event Viewer). Match the style of the surrounding code.
- JSON is camelCase through `Protocol.Json.Options` (`System.Text.Json`); times are integer milliseconds, dates ISO 8601 UTC.
- The component version is the `<Version>` in `src/LiveSplit.WebSocketServer/LiveSplit.WebSocketServer.csproj`; `ServerFactory` and the `hello` message read it from the assembly.
- Update CHANGELOG.md for user-visible changes, and docs/PROTOCOL.md for any protocol change.
- Commit messages: a short imperative subject and a body explaining why.
