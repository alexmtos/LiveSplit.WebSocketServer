# Protocol

The server accepts WebSocket connections on `ws://<host>:<port>/` (port `15721` by default).

There are two protocol versions:

- **Version 1** is the original protocol of this component. It is the default, so existing clients keep working unchanged.
- **Version 2** adds request ids, a response to every request with error codes, subscriptions and ticks. New clients should use it.

A client chooses version 2 by connecting to `ws://<host>:<port>/?protocol=2`, or at any time by sending:

```json
{ "action": "hello", "args": { "protocol": 2 } }
```

When the component settings define a token, it must be part of the URL: `ws://<host>:<port>/?protocol=2&token=<token>`.

## Requests

Requests are the same in both versions. They are either JSON:

```json
{ "id": 1, "action": "setcomparison", "args": { "comparison": "Best Segments" } }
```

- `action` (required): the action name, case insensitive. See [Actions](#actions).
- `id` (optional): a string or a number, copied into the response.
- `args` (optional): an object with named arguments, or an array with the arguments in order. `data` is accepted as a synonym of `args`.

or plain text, like the commands of LiveSplit's built-in server:

```
setcomparison Best Segments
setgametime 1:23.456
```

Everything after the first space is the argument. Times can be given as milliseconds (`83456`) in JSON, or as LiveSplit time strings (`1:23.456`, `1:00:00`, `12.5`) in both forms; `-` means "no time".

All actions run on LiveSplit's UI thread, one at a time.

## Protocol version 2

Every message sent by the server has a `type`.

### `hello`

Sent when a client connects with `?protocol=2`, and as the `data` of the response to the `hello` action.

```json
{
  "type": "hello",
  "protocolVersion": 2,
  "componentVersion": "2.0.0",
  "liveSplitVersion": "1.8.34",
  "readOnly": false,
  "fileCommandsAllowed": false,
  "state": { ... }
}
```

### `response`

Every request gets exactly one response.

```json
{ "type": "response", "id": 1, "action": "split", "ok": true, "data": { "timerState": "Running", "currentSplitIndex": 1 } }
```

```json
{ "type": "response", "id": 2, "action": "split", "ok": false, "error": { "code": "invalid_phase", "message": "'split' is not possible while the timer is NotRunning." } }
```

`data` is omitted when the action has nothing to return. `id` is omitted when the request had none.

| Error code | Meaning |
|---|---|
| `invalid_request` | The message is not valid JSON, has no `action`, or the action needs protocol version 2. |
| `unknown_action` | The action does not exist. |
| `invalid_args` | An argument is missing or invalid (unknown comparison, split index out of range...). |
| `invalid_phase` | Not possible in the current timer phase (for example `split` while the timer is not running). |
| `read_only` | The server is in read only mode. |
| `forbidden` | File actions are disabled in the component settings. |
| `unsupported` | The running LiveSplit version does not let components do this. |
| `unauthorized` | The token is missing or wrong. Sent right before the server closes the connection. |
| `unavailable` | LiveSplit is closing. |
| `internal` | The action failed unexpectedly (see LiveSplit's event log). |

### `event`

Sent when something happens in LiveSplit.

```json
{ "type": "event", "event": "split", "data": null, "state": { ... } }
```

| Event | `data` | Cause |
|---|---|---|
| `start`, `split`, `undo-split`, `skip-split`, `pause`, `resume`, `reset`, `undo-all-pauses` | `null` | The timer, from any source (hotkeys, auto splitters, other clients...). |
| `scroll` | `"up"` or `"down"` | The splits were scrolled. |
| `switch-comparison` | `"previous"` or `"next"` | The comparison was switched with a hotkey or `switchcomparison*`. |
| `run-manually-modified` | `null` | The run was edited. |
| `comparison-renamed` | `null` | A comparison was renamed. |
| `refresh` | `null` | Periodic resend of the state (every 15 s by default, configurable). |
| `run-changed` | `{ path, gameName, categoryName }` | Other splits were opened, or the splits were saved to another file. |
| `run-saved` | `{ path }` | The splits were saved. |
| `layout-changed` | `{ path }` | Another layout was opened, or the layout was saved to another file. |
| `comparison-changed` | `{ comparison }` | The current comparison changed. |
| `timing-method-changed` | `{ timingMethod }` | Real time / game time was switched. |
| `game-time-paused`, `game-time-resumed` | `null` | Game time was paused or resumed. |
| `game-time-initialized` | `{ initialized }` | Game time was initialized (or reset). |
| `hotkey-profile-changed` | `{ profile }` | The hotkey profile changed. |
| `global-hotkeys-changed` | `{ enabled }` | Global hotkeys were enabled or disabled. |
| `custom-variable-changed` | `{ name, value }` | A custom variable of the run changed (`value` is `null` when it was removed). |

By default a version 2 client receives every event with the state, without icons. `subscribe` changes that:

```json
{ "action": "subscribe", "args": { "events": ["split", "reset", "comparison-changed"], "includeState": false } }
```

### `tick`

A small message for clients that draw a running timer. It is sent at most every `tickMs` milliseconds (at least 50) after:

```json
{ "action": "subscribe", "args": { "tickMs": 100 } }
```

```json
{
  "type": "tick",
  "timerState": "Running",
  "currentTime": { "realTime": 83456, "gameTime": 80012 },
  "currentSplitIndex": 3,
  "currentDelta": -1520,
  "isGameTimePaused": false,
  "loadingTimes": 3444
}
```

Ticks follow LiveSplit's refresh rate, so they cannot be more frequent than LiveSplit redraws.

### Delivery

Messages to a client are sent one at a time, in order: the greeting first, then events and responses in the order they happened (an action's response comes after the events it caused). Ticks are skipped while a client has more than a few messages waiting, and a client that stops reading is disconnected once 1000 messages are waiting.

## Protocol version 1

Version 1 behaves exactly as before version 2.0 of this component:

- On connection: `{ "open": { "response": "success" }, "state": { ... } }`.
- `hi` replies `{ "response": { "response": "hi" } }` and `state` replies `{ "response": { "response": "state" }, "state": { ... } }`.
- The other original actions (`startorsplit`, `split`, `unsplit`, `skipsplit`, `pause`, `resume`, `reset`, `starttimer`, `pausegametime`, `unpausegametime`) get no reply, even when they fail.
- Events are sent as `{ "action": { "action": "split", "data": null }, "state": { ... } }`, only for the events LiveSplit raises and `refresh` (not the ones added in version 2), and the state always includes icons.
- Actions added in version 2 can be used from a version 1 session too; they are answered with version 2 `response` messages.

Differences with earlier versions of the component, even in version 1:

- `pause` no longer starts the timer when it is not running.
- In read only mode, control actions are refused as before, but queries added in version 2 still work.

## State

The state is sent with events, in the `hello` message and by the `state` action. Times are milliseconds (`null` when there is no time), dates are ISO 8601 in UTC, a `Time` is `{ "realTime": ms | null, "gameTime": ms | null }`.

```json
{
  "run": {
    "gameIcon": "data:image/png;base64,..." ,
    "gameName": "Super Mario 64",
    "categoryName": "16 Star",
    "startingOffset": 0,
    "attemptCount": 120,
    "finishedCount": 45,
    "comparisons": ["Personal Best", "Best Segments", "Average Segments"],
    "customComparisons": ["Personal Best"],
    "advancedSumOfBest": 1012345,
    "totalPlaytime": 123456789,
    "segments": [
      {
        "icon": null,
        "name": "Bob-omb Battlefield",
        "splitTime": { "realTime": null, "gameTime": null },
        "personalBest": { "realTime": 60000, "gameTime": null },
        "bestSegment": { "realTime": 58000, "gameTime": null },
        "comparisons": { "Personal Best": { "realTime": 60000, "gameTime": null } },
        "customVariables": {},
        "segmentHistory": [ { "attemptIndex": 1, "time": { "realTime": 61000, "gameTime": null } } ]
      }
    ],
    "metadata": {
      "gameId": "o1y9wo6q", "categoryId": "7dgrrxk4", "regionId": null, "platformId": "w89rwelk",
      "emulator": false,
      "variables": { "variableId": "Value" },
      "regionName": null, "platformName": "Nintendo 64", "runId": null,
      "variableNames": { "Variable": "Value" },
      "customVariables": { "deaths": { "value": "3", "isPermanent": false } }
    },
    "filePath": "C:\\Splits\\sm64.lss",
    "hasChanged": false,
    "autoSplitter": { "description": "...", "type": "Script", "activated": true, "localPath": "...", "website": null },
    "attemptHistory": [ { "index": 1, "time": { "realTime": 1000000, "gameTime": null }, "started": "2026-01-01T12:00:00.000Z", "ended": "2026-01-01T12:20:00.000Z", "pauseTime": null, "duration": 1200000 } ]
  },
  "timerState": "Running",
  "currentComparison": "Personal Best",
  "currentTimingMethod": "RealTime",
  "currentTime": { "realTime": 83456, "gameTime": 80012 },
  "loadingTimes": 3444,
  "isGameTimeInitialized": true,
  "isGameTimePaused": false,
  "gameTimePauseTime": null,
  "attemptStarted": "2026-01-01T12:00:00.000Z",
  "attemptEnded": "0001-01-01T00:00:00.000Z",
  "pauseTime": null,
  "currentAttemptDuration": 84000,
  "currentSplitIndex": 3,
  "currentDelta": -1520,
  "predictedTime": 1010825,
  "bestPossibleTime": 998000,
  "currentHotkeyProfile": "Default",
  "hotkeyProfiles": ["Default"],
  "globalHotkeysEnabled": true,
  "layoutPath": "C:\\Layouts\\main.lsl"
}
```

- Icons (`gameIcon`, `segments[].icon`) are only filled when requested (`includeIcons`), and always for version 1 clients.
- `attemptHistory` and `segments[].segmentHistory` are only present when requested (`includeHistory`).
- speedrun.com ids (`gameId`, `categoryId`, `regionId`, `platformId`, `variables`) are `null`/empty until LiveSplit has loaded them from speedrun.com; `regionName`, `platformName` and `variableNames` are always available.

## Actions

`help` returns this list with the access level of each action. Actions of LiveSplit's built-in server exist under the same names; the differences are that times are returned as milliseconds instead of formatted strings, and missing values as `null` instead of `-`.

### Session

Always allowed. `subscribe`, `unsubscribe` and `getsubscription` need protocol version 2.

| Action | Aliases | Description |
|---|---|---|
| `hi` |  | Replies without doing anything (protocol version 1). |
| `ping` |  | Replies with "pong". |
| `hello` |  | Switches the session to another protocol version. Args: { protocol: 2 }. Replies with the server information and the state. |
| `help` | `getcommands` | Lists every action. |
| `subscribe` |  | Chooses what the session receives (protocol 2). Args: { events?: string[] \| "all", includeState?: bool, includeIcons?: bool, includeHistory?: bool, tickMs?: number (0 turns ticks off) }. Omitted args keep their value. |
| `unsubscribe` |  | Stops receiving some events, or every event and tick when no events are given (protocol 2). Args: { events?: string[] }. |
| `getsubscription` |  | The current subscription of the session. |

### Control

Change the timer, the run or LiveSplit's settings. Refused with `read_only` in read only mode.

| Action | Aliases | Description |
|---|---|---|
| `start` | `starttimer` | Starts the timer. |
| `split` |  | Splits. |
| `startorsplit` |  | Starts the timer if it is not running, splits otherwise. |
| `undosplit` | `unsplit` | Undoes the last split. |
| `skipsplit` |  | Skips the current split. |
| `pause` |  | Pauses the timer. Does nothing if it is already paused. |
| `resume` |  | Resumes the timer. Does nothing if it is already running. |
| `togglepause` |  | Pauses the timer if it is running, resumes it if it is paused. |
| `undoallpauses` |  | Removes all pause time from the current attempt. |
| `reset` |  | Resets the timer. Args: { save?: bool } - with save: false the times of this attempt are discarded (default true). |
| `resetandsetattemptaspb` |  | Resets the timer and saves the attempt as the personal best. |
| `initgametime` |  | Initializes game time. |
| `setgametime` |  | Sets game time. Args: { time } (milliseconds or "1:23.456"). |
| `setloadingtimes` |  | Sets the loading times. Args: { time }. |
| `addloadingtimes` |  | Adds to the loading times. Args: { time }. |
| `pausegametime` |  | Pauses game time. |
| `unpausegametime` |  | Resumes game time (and cancels 'alwayspausegametime'). |
| `alwayspausegametime` |  | Pauses game time now and every time the timer starts, until 'unpausegametime'. |
| `setcomparison` |  | Changes the current comparison. Args: { comparison }. |
| `switchcomparisonnext` | `nextcomparison` | Switches to the next comparison. |
| `switchcomparisonprevious` | `previouscomparison` | Switches to the previous comparison. |
| `settimingmethod` | `switchto` | Changes the timing method. Args: { method: "realtime" \| "gametime" }. |
| `scrollup` |  | Scrolls the splits up. |
| `scrolldown` |  | Scrolls the splits down. |
| `setsplitname` |  | Renames a split. Args: { index, name } (negative indices count from the end). Plain text: "setsplitname <index> <name>". |
| `setcurrentsplitname` |  | Renames the current split. Args: { name }. |
| `setcustomvariable` |  | Sets a custom variable of the run. Args: { name, value }. Plain text: 'setcustomvariable ["name", "value"]'. |
| `setgamename` |  | Changes the game name. Args: { name }. |
| `setcategoryname` |  | Changes the category name. Args: { name }. |
| `setoffset` |  | Changes the start offset of the run. Only while the timer is not running. Args: { time }. |
| `enableglobalhotkeys` |  | Enables global hotkeys for the current hotkey profile. |
| `disableglobalhotkeys` |  | Disables global hotkeys for the current hotkey profile. |
| `setglobalhotkeys` |  | Enables or disables global hotkeys. Args: { enabled: bool }. |
| `switchhotkeyprofile` | `sethotkeyprofile` | Switches to another hotkey profile. Args: { profile }. |

### Files

Save or open files. Refused with `read_only` in read only mode and with `forbidden` unless *Allow clients to save and open splits, layouts and screenshots* is checked. Paths are local to the computer running LiveSplit and must be absolute.

| Action | Aliases | Description |
|---|---|---|
| `savesplits` |  | Saves the splits to their file (a new file next to LiveSplit if they were never saved). |
| `savesplitsas` |  | Saves the splits to another file. Args: { path } (.lss). |
| `savelayout` |  | Saves the layout to its file. |
| `savelayoutas` |  | Saves the layout to another file. Args: { path } (.lsl). |
| `switchsplits` | `opensplits` | Opens a splits file. Resets the timer without saving. Args: { path }. |
| `switchlayout` | `openlayout` | Opens a layout file. Args: { path }. |
| `savesplitsscreenshot` |  | Saves a PNG screenshot of the timer. Args: { path }. |

### Queries

Always allowed. Times are milliseconds, missing values are `null`.

| Action | Aliases | Description |
|---|---|---|
| `state` | `getstate` | Replies with the full state. Args: { includeIcons?: bool, includeHistory?: bool }. |
| `getsplitsscreenshot` | `screenshot` | Returns a screenshot of the timer as a PNG data URI. |
| `gettimerphase` | `getcurrenttimerphase` | The timer phase: NotRunning, Running, Paused or Ended. |
| `getcurrenttime` |  | The current time with the current timing method (real time while game time is not initialized). |
| `getcurrentrealtime` |  | The current real time. |
| `getcurrentgametime` |  | The current game time (real time while game time is not initialized). |
| `getdelta` |  | The delta of the last split. Args: { comparison? }. |
| `getpredictedtime` |  | The predicted final time. Args: { comparison? }. |
| `getbestpossibletime` |  | The best possible final time. |
| `getfinaltime` | `getfinalsplittime` | The final time of the run, or of the comparison while the run is not finished. Args: { comparison? }. |
| `getpausedrealtime` | `getpausetime` | The total pause time of the current attempt. |
| `getpausedgametime` |  | The game time at which game time was paused. |
| `getloadingtimes` |  | The loading times (real time minus game time). |
| `isgametimepaused` |  | Whether game time is paused. |
| `isgametimeinitialized` |  | Whether game time is initialized. |
| `getoffset` |  | The start offset of the run. |
| `gettimingmethod` |  | The current timing method: RealTime or GameTime. |
| `getcomparisonname` | `getcomparison` | The current comparison. |
| `getcomparisons` |  | All comparisons. |
| `getsplitindex` |  | The index of the current split (-1 while not running). |
| `getsplitcount` |  | The number of splits. |
| `getsplitname` |  | The name of a split. Args: { index? } (negative indices count from the end; the current split by default). |
| `getcurrentsplitname` |  | The name of the current split. |
| `getprevioussplitname` | `getlastsplitname` | The name of the previous split. |
| `getnextsplitname` | `getupcomingsplitname` | The name of the next split. |
| `getprevioussplittime` | `getlastsplittime` | The split time of the previous split. |
| `getcomparisonsplittime` | `getcurrentsplittime` | The comparison time of the current split. Args: { comparison? }. |
| `getsegment` |  | Everything about one split. Args: { index? } (the current split by default). |
| `getgamename` |  | The game name. |
| `getcategoryname` |  | The category name. |
| `getcategoryvariables` |  | Region, platform, emulator and speedrun.com variables of the run. |
| `getattemptcount` |  | The number of attempts. |
| `getcompletedcount` |  | The number of finished attempts. |
| `getcustomvariablevalue` |  | The value of a custom variable. Args: { name }. |
| `getcustomvariables` |  | All custom variables. |
| `getsplitspath` |  | The path of the splits file. |
| `getlayoutpath` |  | The path of the layout file. |
| `getautosplitterpath` |  | The path of the auto splitter of the run. |
| `autosplitteractivated` |  | Whether the auto splitter of the run is activated. |
| `gethotkeyprofile` |  | The current hotkey profile. |
| `gethotkeyprofiles` |  | All hotkey profiles. |
| `globalhotkeysenabled` |  | Whether global hotkeys are enabled. |
| `getlivesplitversion` |  | The LiveSplit version. |
| `getlivesplitpath` |  | The path of LiveSplit.exe. |
| `getcomponentversion` |  | The version of this component. |
| `getservertype` |  | Always "WebSocketJson" (the built-in server replies TCP or Websocket). |
## Example (JavaScript)

```js
const socket = new WebSocket("ws://127.0.0.1:15721/?protocol=2");
let nextId = 1;
const pending = new Map();

function send(action, args) {
  const id = nextId++;
  socket.send(JSON.stringify({ id, action, args }));
  return new Promise((resolve, reject) => pending.set(id, { resolve, reject }));
}

socket.onmessage = (message) => {
  const msg = JSON.parse(message.data);
  switch (msg.type) {
    case "hello":
      console.log(`LiveSplit ${msg.liveSplitVersion}, ${msg.state.run.gameName}`);
      send("subscribe", { events: ["split", "reset", "start"], includeState: true, tickMs: 100 });
      break;
    case "response": {
      const request = pending.get(msg.id);
      pending.delete(msg.id);
      if (msg.ok) request?.resolve(msg.data);
      else request?.reject(new Error(`${msg.error.code}: ${msg.error.message}`));
      break;
    }
    case "event":
      console.log(msg.event, msg.state?.currentSplitIndex);
      break;
    case "tick":
      console.log(msg.currentTime.realTime);
      break;
  }
};

// Later:
// await send("startorsplit");
// await send("setcomparison", { comparison: "Best Segments" });
// const delta = await send("getdelta");
```
