# Changelog

## 2.0.0

### Compatibility

- Builds against the current LiveSplit (SDK-style project, .NET Framework 4.8.1). `websocket-sharp.dll` is no longer shipped with the component, LiveSplit already includes it.
- Clients of version 1.x keep working: without `?protocol=2`, the server behaves as before (protocol version 1).

### Added

- Protocol version 2 (`?protocol=2` or the `hello` action): request ids, a response to every request with error codes, `hello`, `event` and `tick` messages.
- Plain text requests in the format of LiveSplit's built-in server (`setgametime 1:23.456`).
- Every command of LiveSplit's built-in server, plus `togglepause`, `reset { save: false }`, `resetandsetattemptaspb`, `initgametime`, `settimingmethod`, `switchcomparisonnext/previous`, `scrollup/down`, `setgamename`, `setcategoryname`, `setoffset`, `setglobalhotkeys`, `getsegment`, `getcomparisons`, `getcustomvariables`, `gethotkeyprofiles`, `help` and more. See [docs/PROTOCOL.md](docs/PROTOCOL.md).
- Saving and opening splits and layouts, and screenshots, when allowed in the settings.
- Events for changes LiveSplit raises no event for: `run-changed`, `run-saved`, `layout-changed`, `comparison-changed`, `timing-method-changed`, `game-time-paused/resumed`, `game-time-initialized`, `hotkey-profile-changed`, `global-hotkeys-changed`, `custom-variable-changed`.
- `subscribe`/`unsubscribe`: choose events, whether they carry the state, icons and history, and periodic ticks.
- New state fields: `currentDelta`, `predictedTime`, `bestPossibleTime`, `gameTimePauseTime`, hotkey profiles, `layoutPath`, and in `run`: `customComparisons`, `filePath`, `hasChanged`, `autoSplitter`, custom variables, region/platform names, and on request the attempt and segment history.
- Settings: network access (this computer only / network), token, allowed web origins, resend interval, file commands, and the URL to connect with.
- Tests and a GitHub Actions build against the latest LiveSplit.

### Changed

- New components only accept connections from this computer. Existing layouts keep accepting connections from the network.
- Commands and state reads run on LiveSplit's UI thread instead of the connection's thread.
- Each client has an ordered outgoing queue: messages always arrive in order, ticks are skipped for slow clients and clients that stop reading are disconnected.
- Icons are encoded once and only sent to clients that want them (always to version 1 clients).
- `pause` no longer starts the timer when it is not running.
- The menu entries are now *Start/Stop WebSocket Server (JSON)*, to tell them apart from LiveSplit's built-in server.

### Fixed

- Building the state no longer waits for speedrun.com on the UI thread, and no longer fails when a speedrun.com variable has no value.
- Starting the server on a port in use no longer crashes; the error is shown.
- An invalid port in the settings, or a computer without IPv4, no longer throws.
- The periodic refresh could block LiveSplit while the server was stopping.
