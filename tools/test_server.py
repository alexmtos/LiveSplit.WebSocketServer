#!/usr/bin/env python3
"""End-to-end test of the LiveSplit WebSocket Server component against a running LiveSplit.

By default only safe, read-only checks run. Checks that change LiveSplit must be enabled
explicitly and restore what they change when they finish:

    --control   drive the timer (start, split, pause, game time, comparisons...) and reset it
                without saving. The timer must not be running. The attempt count grows by one.
    --edit-run  rename splits, game and category, change the offset and set a custom variable,
                then restore them. The run is left marked as modified, and the custom variable
                "ws-test" stays in the run until LiveSplit is restarted.
    --hotkeys   toggle global hotkeys and switch to the current hotkey profile.
    --files     save the splits and the layout to their own files, reopen them, and take
                screenshots. Needs "Allow clients to save and open splits, layouts and screenshots".
    --all       everything above.

Requirements: Python 3.8+ and `pip install websocket-client`.

Examples:
    python tools/test_server.py
    python tools/test_server.py --all --token my-token
    python tools/test_server.py --host 192.168.0.10 --port 15721 --control -v
"""

import argparse
import json
import os
import sys
import tempfile
import threading
import time
import traceback

try:
    import websocket  # websocket-client
except ImportError:
    sys.exit("This script needs websocket-client: pip install websocket-client")


LEGACY_STATE_FIELDS = [
    "run", "timerState", "currentComparison", "currentTimingMethod", "currentTime", "loadingTimes",
    "isGameTimeInitialized", "isGameTimePaused", "attemptStarted", "attemptEnded", "pauseTime",
    "currentAttemptDuration", "currentSplitIndex",
]
V2_STATE_FIELDS = [
    "gameTimePauseTime", "currentDelta", "predictedTime", "bestPossibleTime", "currentHotkeyProfile",
    "hotkeyProfiles", "globalHotkeysEnabled", "layoutPath",
]
LEGACY_RUN_FIELDS = [
    "gameIcon", "gameName", "categoryName", "startingOffset", "attemptCount", "finishedCount", "comparisons",
    "advancedSumOfBest", "totalPlaytime", "segments", "metadata",
]
V2_RUN_FIELDS = ["customComparisons", "filePath", "hasChanged", "autoSplitter"]
SEGMENT_FIELDS = ["icon", "name", "splitTime", "personalBest", "bestSegment", "comparisons", "customVariables"]
METADATA_FIELDS = [
    "gameId", "categoryId", "regionId", "platformId", "emulator", "variables", "regionName", "platformName",
    "runId", "variableNames", "customVariables",
]
ALL_EVENTS = {
    "refresh", "split", "undo-split", "skip-split", "start", "reset", "pause", "undo-all-pauses", "resume",
    "scroll", "switch-comparison", "run-manually-modified", "comparison-renamed", "run-changed", "run-saved",
    "layout-changed", "comparison-changed", "timing-method-changed", "game-time-paused", "game-time-resumed",
    "game-time-initialized", "hotkey-profile-changed", "global-hotkeys-changed", "custom-variable-changed",
}
# Commands of LiveSplit's built-in server, which must all exist under the same name.
BUILT_IN_COMMANDS = [
    "startorsplit", "split", "undosplit", "unsplit", "skipsplit", "pause", "undoallpauses", "resume", "reset",
    "start", "starttimer", "setgametime", "setloadingtimes", "addloadingtimes", "pausegametime",
    "unpausegametime", "alwayspausegametime", "getgamename", "getcategoryname", "getcategoryvariables",
    "getdelta", "getsplitindex", "getsplitcount", "getsplitname", "getcurrentsplitname", "getlastsplitname",
    "getprevioussplitname", "getnextsplitname", "getupcomingsplitname", "getlastsplittime",
    "getprevioussplittime", "getcurrentsplittime", "getcomparisonsplittime", "getcurrentrealtime",
    "getcurrentgametime", "getcurrenttime", "getfinaltime", "getfinalsplittime", "getbestpossibletime",
    "getpredictedtime", "getpausedrealtime", "getpausedgametime", "getoffset", "gettimerphase",
    "getcurrenttimerphase", "getcomparisonname", "setcomparison", "switchto", "gettimingmethod",
    "setsplitname", "setcurrentsplitname", "getcustomvariablevalue", "setcustomvariable",
    "globalhotkeysenabled", "enableglobalhotkeys", "disableglobalhotkeys", "switchhotkeyprofile", "ping",
    "getlayoutpath", "savelayout", "savelayoutas", "getsplitspath", "savesplits", "savesplitsas",
    "switchlayout", "switchsplits", "getsplitsscreenshot", "savesplitsscreenshot", "getattemptcount",
    "getcompletedcount", "getautosplitterpath", "autosplitteractivated", "gethotkeyprofile",
    "getlivesplitversion", "getlivesplitpath", "getservertype",
]
TEST_VARIABLE = "ws-test"


# --------------------------------------------------------------------------------------------
# Client
# --------------------------------------------------------------------------------------------

class Closed(Exception):
    pass


class Client:
    """A WebSocket connection that collects every message in the background."""

    def __init__(self, url, origin=None, timeout=10):
        self.url = url
        self.ws = websocket.create_connection(url, timeout=timeout, origin=origin, suppress_origin=origin is None)
        self.ws.settimeout(None)
        self.messages = []
        self.closed = False
        self.condition = threading.Condition()
        self.next_id = 1
        self.thread = threading.Thread(target=self._read, daemon=True)
        self.thread.start()

    def _read(self):
        try:
            while True:
                text = self.ws.recv()
                if text is None or text == "":
                    break
                try:
                    message = json.loads(text)
                except ValueError:
                    message = {"_raw": text}
                with self.condition:
                    self.messages.append(message)
                    self.condition.notify_all()
        except Exception:
            pass
        with self.condition:
            self.closed = True
            self.condition.notify_all()

    def send(self, message):
        self.ws.send(message if isinstance(message, str) else json.dumps(message))

    def wait_for(self, predicate, timeout=5.0, description="message"):
        """Returns (and removes) the first received message matching predicate."""
        deadline = time.monotonic() + timeout
        with self.condition:
            while True:
                for i, message in enumerate(self.messages):
                    if predicate(message):
                        return self.messages.pop(i)
                remaining = deadline - time.monotonic()
                if self.closed:
                    raise Closed("connection closed while waiting for %s" % description)
                if remaining <= 0:
                    raise AssertionError("timed out waiting for %s" % description)
                self.condition.wait(remaining)

    def take_all(self, predicate):
        with self.condition:
            taken = [m for m in self.messages if predicate(m)]
            self.messages = [m for m in self.messages if not predicate(m)]
            return taken

    def clear(self):
        with self.condition:
            self.messages = []

    def wait_closed(self, timeout=5.0):
        deadline = time.monotonic() + timeout
        with self.condition:
            while not self.closed:
                remaining = deadline - time.monotonic()
                if remaining <= 0:
                    return False
                self.condition.wait(remaining)
            return True

    # Protocol version 2 helpers.

    def request(self, action, args=None, timeout=10.0):
        """Sends a JSON request and returns its response message."""
        request_id = self.next_id
        self.next_id += 1
        message = {"id": request_id, "action": action}
        if args is not None:
            message["args"] = args
        self.send(message)
        return self.wait_for(lambda m: m.get("type") == "response" and m.get("id") == request_id,
                             timeout, "the response to '%s'" % action)

    def call(self, action, args=None):
        """Sends a request that must succeed and returns its data."""
        response = self.request(action, args)
        if not response.get("ok"):
            error = response.get("error") or {}
            raise AssertionError("'%s' failed: %s: %s" % (action, error.get("code"), error.get("message")))
        return response.get("data")

    def text(self, command, timeout=10.0):
        """Sends a plain text request and returns the next response message."""
        self.send(command)
        return self.wait_for(lambda m: m.get("type") == "response", timeout, "the response to '%s'" % command)

    def expect_error(self, action, args, code):
        response = self.request(action, args)
        assert not response.get("ok"), "'%s' should fail with %s but succeeded" % (action, code)
        actual = response["error"]["code"]
        assert actual == code, "'%s' failed with %s instead of %s (%s)" % (
            action, actual, code, response["error"].get("message"))
        return response["error"]

    def event(self, name, timeout=5.0):
        return self.wait_for(lambda m: m.get("type") == "event" and m.get("event") == name,
                             timeout, "the '%s' event" % name)

    def close(self):
        try:
            self.ws.close()
        except Exception:
            pass


# --------------------------------------------------------------------------------------------
# Test runner
# --------------------------------------------------------------------------------------------

class Skip(Exception):
    pass


class Runner:
    def __init__(self, verbose):
        self.verbose = verbose
        self.results = []
        self.group = ""

    def section(self, title):
        self.group = title
        print("\n== %s ==" % title)

    def run(self, name, function):
        start = time.monotonic()
        try:
            detail = function()
            status = "PASS"
        except Skip as e:
            status, detail = "SKIP", str(e)
        except (AssertionError, Closed) as e:
            status, detail = "FAIL", str(e) or "assertion failed"
            if self.verbose:
                traceback.print_exc()
        except Exception as e:
            status, detail = "ERROR", "%s: %s" % (type(e).__name__, e)
            if self.verbose:
                traceback.print_exc()
        elapsed = (time.monotonic() - start) * 1000
        self.results.append((self.group, name, status, detail))
        line = "  [%s] %s" % (status, name)
        if detail and (status != "PASS" or self.verbose):
            line += " - %s" % detail
        if self.verbose:
            line += " (%.0f ms)" % elapsed
        print(line)

    def summary(self):
        counts = {}
        for _, _, status, _ in self.results:
            counts[status] = counts.get(status, 0) + 1
        print("\n== Summary ==")
        print("  " + ", ".join("%s: %d" % (s, counts.get(s, 0)) for s in ("PASS", "FAIL", "ERROR", "SKIP")))
        failed = [r for r in self.results if r[2] in ("FAIL", "ERROR")]
        for group, name, status, detail in failed:
            print("  %s: %s / %s - %s" % (status, group, name, detail))
        return 1 if failed else 0


def is_int(value):
    return isinstance(value, int) and not isinstance(value, bool)


def is_time(value):
    return value is None or is_int(value)


def is_time_dto(value):
    return isinstance(value, dict) and is_time(value.get("realTime")) and is_time(value.get("gameTime"))


def assert_fields(obj, fields, what):
    missing = [f for f in fields if f not in obj]
    assert not missing, "%s is missing %s" % (what, ", ".join(missing))


# --------------------------------------------------------------------------------------------
# Tests
# --------------------------------------------------------------------------------------------

class Tests:
    def __init__(self, args, runner):
        self.args = args
        self.r = runner
        query = []
        if args.token:
            query.append("token=" + args.token)
        self.base = "ws://%s:%d/" % (args.host, args.port)
        self.token_query = "&".join(query)
        self.hello = None
        self.state = None
        self.client = None

    def url(self, protocol=None, token=True):
        parts = []
        if protocol:
            parts.append("protocol=%d" % protocol)
        if token and self.token_query:
            parts.append(self.token_query)
        return self.base + ("?" + "&".join(parts) if parts else "")

    def connect(self, protocol=2, subscribe=None):
        client = Client(self.url(protocol))
        greeting = client.wait_for(lambda m: True, 10, "the greeting")
        if protocol == 2:
            assert greeting.get("type") == "hello", "expected a hello message, got %r" % greeting
            if subscribe is not None:
                client.call("subscribe", subscribe)
        return client, greeting

    @property
    def read_only(self):
        return bool(self.hello and self.hello.get("readOnly"))

    def require_writable(self):
        if self.read_only:
            raise Skip("the server is in read only mode")

    def run(self):
        self.r.section("Connection")
        self.r.run("connect with protocol 2 and receive hello", self.t_hello)
        if self.client is None:
            print("\nCould not connect to %s. Is LiveSplit running with the server started?" % self.base)
            return
        try:
            self.run_groups()
        finally:
            self.client.close()

    def run_groups(self):
        r = self.r
        r.run("ping (JSON and plain text)", self.t_ping)
        r.run("request ids are echoed (number and string)", self.t_ids)
        r.run("invalid requests are reported", self.t_invalid_requests)
        r.run("help lists every action", self.t_help)
        r.run("every command of the built-in server exists", self.t_built_in_commands)

        r.section("Protocol version 1")
        r.run("greeting, hi and state", self.t_v1_basic)
        r.run("actions added in version 2 answer with version 2 responses", self.t_v1_new_actions)
        r.run("hello upgrades a version 1 session", self.t_v1_hello_upgrade)

        r.section("State")
        r.run("all fields are present with the right types", self.t_state_fields)
        r.run("icons and history only on request", self.t_state_options)

        r.section("Queries")
        r.run("timer queries", self.t_timer_queries)
        r.run("split queries", self.t_split_queries)
        r.run("run queries", self.t_run_queries)
        r.run("reading an unknown custom variable does not create it", self.t_custom_variable_read)
        r.run("hotkey queries", self.t_hotkey_queries)
        r.run("LiveSplit queries", self.t_livesplit_queries)
        r.run("screenshot", self.t_screenshot)

        r.section("Subscriptions")
        r.run("default subscription", self.t_default_subscription)
        r.run("subscribe, unsubscribe and validation", self.t_subscribe)
        r.run("subscriptions need protocol version 2", self.t_subscribe_v1)
        r.run("ticks", self.t_ticks)
        r.run("refresh event", self.t_refresh)

        r.section("Security")
        r.run("token is enforced", self.t_token)
        r.run("origin check", self.t_origin)
        r.run("read only mode refuses control actions", self.t_read_only)
        r.run("file actions are refused when disabled", self.t_files_forbidden)

        # Before the timer and run tests, which leave the splits modified.
        r.section("Files (--files)")
        if self.args.files:
            self.run_files()
        else:
            r.run("files", lambda: self.skip_flag("--files"))

        r.section("Timer control (--control)")
        if self.args.control:
            self.run_control()
        else:
            r.run("timer control", lambda: self.skip_flag("--control"))

        r.section("Run editing (--edit-run)")
        if self.args.edit_run:
            self.run_edit_run()
        else:
            r.run("run editing", lambda: self.skip_flag("--edit-run"))

        r.section("Hotkeys (--hotkeys)")
        if self.args.hotkeys:
            r.run("toggle global hotkeys", self.t_global_hotkeys)
            r.run("switch hotkey profile", self.t_hotkey_profile)
        else:
            r.run("hotkeys", lambda: self.skip_flag("--hotkeys"))


    @staticmethod
    def skip_flag(flag):
        raise Skip("changes LiveSplit; enable with %s or --all" % flag)

    # Connection -------------------------------------------------------------------------------

    def t_hello(self):
        try:
            self.client, self.hello = self.connect(2)
        except AssertionError as e:
            if "unauthorized" in str(e):
                raise AssertionError("the server requires a token: pass it with --token")
            raise
        assert self.hello["protocolVersion"] == 2
        for field in ("componentVersion", "liveSplitVersion", "readOnly", "fileCommandsAllowed", "state"):
            assert field in self.hello, "hello is missing %s" % field
        self.state = self.hello["state"]
        return "component %s, LiveSplit %s, read only: %s, file commands: %s" % (
            self.hello["componentVersion"], self.hello["liveSplitVersion"], self.hello["readOnly"],
            self.hello["fileCommandsAllowed"])

    def t_ping(self):
        assert self.client.call("ping") == "pong"
        response = self.client.text("ping")
        assert response["ok"] and response["data"] == "pong", response

    def t_ids(self):
        response = self.client.request("ping")
        assert is_int(response["id"])
        self.client.send({"id": "abc", "action": "ping"})
        response = self.client.wait_for(lambda m: m.get("type") == "response" and m.get("id") == "abc")
        assert response["data"] == "pong"

    def t_invalid_requests(self):
        c = self.client
        c.send("{broken json")
        assert c.wait_for(lambda m: m.get("type") == "response")["error"]["code"] == "invalid_request"
        c.send({"id": 99001})
        response = c.wait_for(lambda m: m.get("type") == "response" and m.get("id") == 99001)
        assert response["error"]["code"] == "invalid_request"
        c.expect_error("definitely-not-an-action", None, "unknown_action")
        assert c.text("definitely-not-an-action")["error"]["code"] == "unknown_action"

    def t_help(self):
        actions = self.client.call("help")
        names = {a["action"] for a in actions}
        for a in actions:
            assert a["access"] in ("read", "session", "control", "file"), a
            assert a["description"], a
        assert {"state", "split", "subscribe", "savesplits", "getdelta"} <= names
        return "%d actions" % len(actions)

    def t_built_in_commands(self):
        actions = self.client.call("help")
        names = set()
        for a in actions:
            names.add(a["action"])
            names.update(a["aliases"])
        missing = [c for c in BUILT_IN_COMMANDS if c not in names]
        assert not missing, "missing: " + ", ".join(missing)

    # Protocol version 1 -----------------------------------------------------------------------

    def t_v1_basic(self):
        client = Client(self.url(None))
        try:
            opened = client.wait_for(lambda m: True, 10, "the greeting")
            assert opened.get("open", {}).get("response") == "success", opened
            assert_fields(opened["state"], LEGACY_STATE_FIELDS, "state")
            client.send("hi")
            assert client.wait_for(lambda m: "response" in m)["response"] == {"response": "hi"}
            client.send({"action": "state"})
            response = client.wait_for(lambda m: "response" in m)
            assert response["response"]["response"] == "state"
            assert_fields(response["state"], LEGACY_STATE_FIELDS, "state")
            # Version 1 clients always get icons (as data URIs or null).
            icon = response["state"]["run"]["gameIcon"]
            assert icon is None or icon.startswith("data:image/png;base64,")
        finally:
            client.close()

    def t_v1_new_actions(self):
        client = Client(self.url(None))
        try:
            client.wait_for(lambda m: True, 10, "the greeting")
            client.send({"id": 5, "action": "getsplitcount"})
            response = client.wait_for(lambda m: m.get("type") == "response")
            assert response["ok"] and response["id"] == 5 and is_int(response["data"])
        finally:
            client.close()

    def t_v1_hello_upgrade(self):
        client = Client(self.url(None))
        try:
            client.wait_for(lambda m: True, 10, "the greeting")
            data = client.call("hello", {"protocol": 2})
            assert data["protocolVersion"] == 2 and data["type"] == "hello"
            client.expect_error("hello", {"protocol": 9}, "invalid_args")
            client.call("getsubscription")
        finally:
            client.close()

    # State ------------------------------------------------------------------------------------

    def t_state_fields(self):
        state = self.client.call("state")
        assert_fields(state, LEGACY_STATE_FIELDS + V2_STATE_FIELDS, "state")
        run = state["run"]
        assert_fields(run, LEGACY_RUN_FIELDS + V2_RUN_FIELDS, "run")
        assert_fields(run["metadata"], METADATA_FIELDS, "run.metadata")
        assert state["timerState"] in ("NotRunning", "Running", "Paused", "Ended")
        assert state["currentTimingMethod"] in ("RealTime", "GameTime")
        assert is_time_dto(state["currentTime"])
        assert state["currentComparison"] in run["comparisons"]
        assert is_int(state["currentSplitIndex"])
        for name in ("currentDelta", "predictedTime", "bestPossibleTime", "loadingTimes", "pauseTime"):
            assert is_time(state[name]), "%s is not a time: %r" % (name, state[name])
        for i, segment in enumerate(run["segments"]):
            assert_fields(segment, SEGMENT_FIELDS, "segment %d" % i)
            for field in ("splitTime", "personalBest", "bestSegment"):
                assert is_time_dto(segment[field]), "segment %d %s" % (i, field)
            for comparison, value in segment["comparisons"].items():
                assert is_time_dto(value), "segment %d comparison %s" % (i, comparison)
        self.state = state
        return "%s - %s, %d splits, %s" % (run["gameName"], run["categoryName"], len(run["segments"]),
                                           state["timerState"])

    def t_state_options(self):
        plain = self.client.call("state")
        assert plain["run"]["gameIcon"] is None and all(s["icon"] is None for s in plain["run"]["segments"])
        assert "attemptHistory" not in plain["run"]
        full = self.client.call("state", {"includeIcons": True, "includeHistory": True})
        assert isinstance(full["run"]["attemptHistory"], list)
        assert all(isinstance(s["segmentHistory"], list) for s in full["run"]["segments"])
        icons = [s["icon"] for s in full["run"]["segments"] if s["icon"]] + [full["run"]["gameIcon"] or ""]
        assert all(i == "" or i.startswith("data:image/png;base64,") for i in icons)
        return "%d attempts in history" % len(full["run"]["attemptHistory"])

    # Queries ----------------------------------------------------------------------------------

    def t_timer_queries(self):
        c = self.client
        assert c.call("gettimerphase") in ("NotRunning", "Running", "Paused", "Ended")
        assert c.call("getcurrenttimerphase") == c.call("gettimerphase")
        for action in ("getcurrenttime", "getcurrentrealtime", "getcurrentgametime", "getdelta", "getpredictedtime",
                       "getbestpossibletime", "getfinaltime", "getfinalsplittime", "getpausedrealtime",
                       "getpausedgametime", "getloadingtimes", "getoffset", "getprevioussplittime",
                       "getcomparisonsplittime"):
            value = c.call(action)
            assert is_time(value), "%s returned %r" % (action, value)
        assert isinstance(c.call("isgametimepaused"), bool)
        assert isinstance(c.call("isgametimeinitialized"), bool)
        assert c.call("gettimingmethod") in ("RealTime", "GameTime")
        comparisons = c.call("getcomparisons")
        assert c.call("getcomparisonname") in comparisons
        assert c.call("getcomparison") == c.call("getcomparisonname")
        # A comparison argument is accepted.
        assert is_time(c.call("getpredictedtime", {"comparison": comparisons[0]}))
        response = c.text("getfinaltime " + comparisons[0])
        assert response["ok"] and is_time(response["data"])

    def t_split_queries(self):
        c = self.client
        segments = self.state["run"]["segments"]
        count = c.call("getsplitcount")
        assert count == len(segments)
        index = c.call("getsplitindex")
        assert is_int(index)
        if count == 0:
            raise Skip("the run has no splits")
        assert c.call("getsplitname", {"index": 0}) == segments[0]["name"]
        assert c.call("getsplitname", {"index": -1}) == segments[-1]["name"]
        assert c.text("getsplitname 0")["data"] == segments[0]["name"]
        c.expect_error("getsplitname", {"index": count + 100}, "invalid_args")
        segment = c.call("getsegment", {"index": 0})
        assert segment["name"] == segments[0]["name"] and is_time_dto(segment["personalBest"])
        current = c.call("getcurrentsplitname")
        if index < 0:
            assert current is None and c.call("getsplitname") is None
            c.expect_error("getsegment", None, "invalid_args")
        else:
            assert current == segments[index]["name"]
        for action in ("getprevioussplitname", "getlastsplitname", "getnextsplitname", "getupcomingsplitname"):
            value = c.call(action)
            assert value is None or isinstance(value, str), "%s returned %r" % (action, value)

    def t_run_queries(self):
        c = self.client
        run = self.state["run"]
        assert c.call("getgamename") == run["gameName"]
        assert c.call("getcategoryname") == run["categoryName"]
        assert c.call("getattemptcount") == run["attemptCount"]
        assert c.call("getcompletedcount") == run["finishedCount"]
        variables = c.call("getcategoryvariables")
        assert_fields(variables, ("region", "platform", "usesEmulator", "variables"), "getcategoryvariables")
        assert isinstance(c.call("getcustomvariables"), dict)
        for action in ("getsplitspath", "getlayoutpath", "getautosplitterpath"):
            value = c.call(action)
            assert value is None or isinstance(value, str), "%s returned %r" % (action, value)
        assert isinstance(c.call("autosplitteractivated"), bool)

    def t_custom_variable_read(self):
        c = self.client
        name = "ws-test-unknown-%d" % int(time.time())
        assert c.call("getcustomvariablevalue", {"name": name}) is None
        assert name not in c.call("getcustomvariables")
        c.expect_error("getcustomvariablevalue", None, "invalid_args")

    def t_hotkey_queries(self):
        c = self.client
        profiles = c.call("gethotkeyprofiles")
        assert c.call("gethotkeyprofile") in profiles
        assert isinstance(c.call("globalhotkeysenabled"), bool)
        return "profiles: %s" % ", ".join(profiles)

    def t_livesplit_queries(self):
        c = self.client
        assert isinstance(c.call("getlivesplitversion"), str)
        assert c.call("getcomponentversion") == self.hello["componentVersion"]
        assert c.call("getservertype") == "WebSocketJson"
        path = c.call("getlivesplitpath")
        assert path is None or path.lower().endswith(".exe"), path
        return "LiveSplit at %s" % path

    def t_screenshot(self):
        response = self.client.request("getsplitsscreenshot")
        if not response["ok"] and response["error"]["code"] == "unsupported":
            raise Skip("unsupported by this LiveSplit version")
        assert response["ok"], response
        assert response["data"].startswith("data:image/png;base64,")
        return "%d KB" % (len(response["data"]) * 3 // 4 // 1024)

    # Subscriptions ----------------------------------------------------------------------------

    def t_default_subscription(self):
        subscription = self.client.call("getsubscription")
        assert set(subscription["events"]) == ALL_EVENTS, "events differ: %s" % (
            set(subscription["events"]) ^ ALL_EVENTS)
        assert subscription["includeState"] is True
        assert subscription["includeIcons"] is False and subscription["includeHistory"] is False
        assert subscription["tickMs"] is None

    def t_subscribe(self):
        client, _ = self.connect(2)
        try:
            data = client.call("subscribe", {"events": ["split", "reset"], "includeState": False, "tickMs": 10})
            assert sorted(data["events"]) == ["reset", "split"]
            assert data["includeState"] is False and data["tickMs"] == 50  # minimum
            data = client.call("unsubscribe", {"events": ["split"]})
            assert data["events"] == ["reset"]
            data = client.call("subscribe", {"events": "all", "tickMs": 0})
            assert set(data["events"]) == ALL_EVENTS and data["tickMs"] is None
            data = client.call("unsubscribe")
            assert data["events"] == []
            client.expect_error("subscribe", {"events": ["not-an-event"]}, "invalid_args")
        finally:
            client.close()

    def t_subscribe_v1(self):
        client = Client(self.url(None))
        try:
            client.wait_for(lambda m: True, 10, "the greeting")
            client.expect_error("subscribe", {"events": ["split"]}, "invalid_request")
        finally:
            client.close()

    def t_ticks(self):
        client, _ = self.connect(2, {"events": [], "tickMs": 100})
        try:
            time.sleep(1.5)
            ticks = client.take_all(lambda m: m.get("type") == "tick")
            if not ticks:
                raise AssertionError("no tick in 1.5 s (ticks follow LiveSplit's redraws; is its window updating?)")
            for tick in ticks:
                assert_fields(tick, ("timerState", "currentTime", "currentSplitIndex", "currentDelta",
                                     "isGameTimePaused", "loadingTimes"), "tick")
            assert len(ticks) <= 17, "%d ticks in 1.5 s for 100 ms" % len(ticks)
            client.call("subscribe", {"tickMs": 0})
            time.sleep(0.3)
            client.clear()
            time.sleep(0.5)
            assert not client.take_all(lambda m: m.get("type") == "tick"), "ticks continued after tickMs: 0"
            return "%d ticks in 1.5 s" % len(ticks)
        finally:
            client.close()

    def t_refresh(self):
        if not self.args.wait_refresh:
            raise Skip("waits for the periodic refresh; enable with --wait-refresh")
        client, _ = self.connect(2, {"events": ["refresh"], "includeState": True})
        try:
            event = client.event("refresh", timeout=self.args.wait_refresh)
            assert_fields(event["state"], LEGACY_STATE_FIELDS, "refresh state")
        finally:
            client.close()

    # Security ---------------------------------------------------------------------------------

    def t_token(self):
        if not self.args.token:
            client = Client(self.url(2, token=False))
            try:
                greeting = client.wait_for(lambda m: True, 10, "the greeting")
            finally:
                client.close()
            if greeting.get("type") == "hello":
                raise Skip("no token configured (pass --token if the server has one)")
            raise AssertionError("the server requires a token; pass it with --token")
        for url in (self.url(2, token=False), self.url(2, token=False) + "&token=wrong-" + self.args.token):
            client = Client(url)
            try:
                try:
                    message = client.wait_for(lambda m: True, 5, "the error")
                    assert message.get("error", {}).get("code") == "unauthorized", message
                except Closed:
                    pass  # The error may be dropped when the close arrives first.
                assert client.wait_closed(5), "the connection stayed open without a valid token"
            finally:
                client.close()

    def t_origin(self):
        origin = "https://ws-test.invalid"
        try:
            client = Client(self.url(2), origin=origin)
        except websocket.WebSocketBadStatusException as e:
            return "a browser at %s is refused (HTTP %s): allowed origins are configured" % (origin, e.status_code)
        try:
            client.wait_for(lambda m: m.get("type") == "hello", 10, "hello")
        finally:
            client.close()
        return "a browser at %s is accepted: no allowed origins configured" % origin

    def t_read_only(self):
        if not self.read_only:
            raise Skip("the server is not in read only mode")
        for action in ("start", "split", "reset", "setcomparison", "setcustomvariable", "enableglobalhotkeys"):
            self.client.expect_error(action, None, "read_only")
        self.client.expect_error("savesplits", None, "read_only")

    def t_files_forbidden(self):
        if self.read_only:
            raise Skip("the server is in read only mode")
        if self.hello["fileCommandsAllowed"]:
            raise Skip("file commands are allowed in the settings")
        for action in ("savesplits", "savelayout", "switchsplits", "switchlayout", "savesplitsscreenshot"):
            self.client.expect_error(action, {"path": "C:\\nowhere.lss"}, "forbidden")

    # Timer control ----------------------------------------------------------------------------

    def run_control(self):
        r = self.r
        r.run("preconditions", self.control_preconditions)
        if not getattr(self, "control_ready", False):
            return
        listener, _ = self.connect(2, {"includeState": False})
        legacy = Client(self.url(None))
        legacy.wait_for(lambda m: True, 10, "the greeting")
        self.listener, self.legacy = listener, legacy
        original_comparison = self.client.call("getcomparisonname")
        original_method = self.client.call("gettimingmethod")
        attempts = self.client.call("getattemptcount")
        try:
            r.run("actions are refused while the timer is not running", self.t_not_running)
            r.run("start (events for version 1 and 2 clients)", self.t_start)
            r.run("split, undo and skip", self.t_split)
            r.run("queries while running", self.t_running_queries)
            r.run("pause, resume, toggle and undo pauses", self.t_pause)
            r.run("game time", self.t_game_time)
            r.run("comparisons", self.t_comparisons)
            r.run("timing method", self.t_timing_method)
            r.run("scroll", self.t_scroll)
            r.run("startorsplit", self.t_start_or_split)
            r.run("reset without saving", lambda: self.t_reset(attempts))
        finally:
            # Leave LiveSplit as it was, even after a failure.
            try:
                if self.client.call("gettimerphase") != "NotRunning":
                    self.client.call("reset", {"save": False})
                self.client.call("unpausegametime")
                self.client.call("setcomparison", {"comparison": original_comparison})
                self.client.call("settimingmethod", {"method": original_method})
            except Exception as e:
                print("  !! could not restore the timer: %s" % e)
            listener.close()
            legacy.close()

    def control_preconditions(self):
        self.require_writable()
        if self.client.call("gettimerphase") != "NotRunning":
            raise Skip("the timer is running; reset it first")
        if self.client.call("getsplitcount") < 2:
            raise Skip("the run needs at least 2 splits")
        self.control_ready = True

    def t_not_running(self):
        c = self.client
        for action in ("split", "pause", "resume", "undosplit", "skipsplit", "reset", "togglepause",
                       "setcurrentsplitname"):
            c.expect_error(action, {"name": "x"} if action == "setcurrentsplitname" else None, "invalid_phase")

    def t_start(self):
        data = self.client.call("start")
        assert data == {"timerState": "Running", "currentSplitIndex": 0}, data
        self.listener.event("start")
        legacy = self.legacy.wait_for(lambda m: m.get("action", {}).get("action") == "start", 5, "the v1 start")
        assert_fields(legacy["state"], LEGACY_STATE_FIELDS, "v1 event state")
        self.client.expect_error("start", None, "invalid_phase")
        time.sleep(0.3)

    def t_split(self):
        c = self.client
        assert c.call("split")["currentSplitIndex"] == 1
        self.listener.event("split")
        assert c.call("undosplit")["currentSplitIndex"] == 0
        self.listener.event("undo-split")
        assert c.call("skipsplit")["currentSplitIndex"] == 1
        self.listener.event("skip-split")
        assert c.text("unsplit")["data"]["currentSplitIndex"] == 0
        self.listener.event("undo-split")

    def t_running_queries(self):
        c = self.client
        assert c.call("getcurrentsplitname") == self.state["run"]["segments"][0]["name"]
        assert c.call("getnextsplitname") == self.state["run"]["segments"][1]["name"]
        assert c.call("getcurrenttime") > 0
        assert c.call("getsegment")["index"] == 0
        state = c.call("state")
        assert state["timerState"] == "Running" and state["currentSplitIndex"] == 0

    def t_pause(self):
        c = self.client
        assert c.call("pause")["timerState"] == "Paused"
        self.listener.event("pause")
        assert c.call("pause")["timerState"] == "Paused"  # already paused: no-op
        assert c.call("togglepause")["timerState"] == "Running"
        self.listener.event("resume")
        assert c.call("resume")["timerState"] == "Running"  # already running: no-op
        c.call("pause")
        c.call("resume")
        assert is_int(c.call("getpausedrealtime"))
        c.call("undoallpauses")
        self.listener.event("undo-all-pauses")
        assert c.call("getpausedrealtime") in (None, 0)

    def t_game_time(self):
        c = self.client
        data = c.call("initgametime")
        assert data["isGameTimeInitialized"] is True
        self.listener.event("game-time-initialized")
        c.call("pausegametime")
        self.listener.event("game-time-paused")
        c.call("setgametime", {"time": 5000})
        assert c.call("getcurrentgametime") == 5000
        response = c.text("setgametime 0:07.5")
        assert response["ok"] and c.call("getcurrentgametime") == 7500
        c.expect_error("setgametime", {"time": "soon"}, "invalid_args")
        c.call("setloadingtimes", {"time": 1000})
        c.call("addloadingtimes", {"time": 500})
        assert c.call("getloadingtimes") == 1500
        c.call("unpausegametime")
        self.listener.event("game-time-resumed")
        c.call("alwayspausegametime")
        assert c.call("isgametimepaused") is True
        c.call("unpausegametime")
        assert c.call("isgametimepaused") is False

    def t_comparisons(self):
        c = self.client
        comparisons = c.call("getcomparisons")
        original = c.call("getcomparisonname")
        other = next((x for x in comparisons if x != original), None)
        if other is None:
            raise Skip("only one comparison")
        assert c.call("setcomparison", {"comparison": other}) == {"comparison": other}
        assert self.listener.event("comparison-changed")["data"]["comparison"] == other
        assert c.text("setcomparison " + original.lower())["data"]["comparison"] == original
        c.expect_error("setcomparison", {"comparison": "No Such Comparison"}, "invalid_args")
        c.call("switchcomparisonnext")
        assert self.listener.event("switch-comparison")["data"] == "next"
        c.call("switchcomparisonprevious")
        assert self.listener.event("switch-comparison")["data"] == "previous"
        assert c.call("getcomparisonname") == original

    def t_timing_method(self):
        c = self.client
        original = c.call("gettimingmethod")
        other = "gametime" if original == "RealTime" else "realtime"
        c.call("settimingmethod", {"method": other})
        self.listener.event("timing-method-changed")
        assert c.text("switchto " + original.lower())["ok"]
        assert c.call("gettimingmethod") == original
        c.expect_error("settimingmethod", {"method": "sundial"}, "invalid_args")

    def t_scroll(self):
        self.client.call("scrollup")
        assert self.listener.event("scroll")["data"] == "up"
        self.client.call("scrolldown")
        assert self.listener.event("scroll")["data"] == "down"

    def t_start_or_split(self):
        assert self.client.call("startorsplit")["currentSplitIndex"] == 1
        self.listener.event("split")

    def t_reset(self, attempts_before):
        c = self.client
        pbs = [s["personalBest"] for s in c.call("state")["run"]["segments"]]
        data = c.call("reset", {"save": False})
        assert data["timerState"] == "NotRunning", data
        self.listener.event("reset")
        assert c.call("getattemptcount") == attempts_before + 1
        assert [s["personalBest"] for s in c.call("state")["run"]["segments"]] == pbs, "the personal best changed"

    # Run editing ------------------------------------------------------------------------------

    def run_edit_run(self):
        r = self.r
        r.run("preconditions", self.require_writable)
        if self.read_only:
            return
        listener, _ = self.connect(2, {"includeState": False})
        self.listener = listener
        c = self.client
        original = c.call("state")["run"]
        try:
            r.run("rename splits", lambda: self.t_rename(original))
            r.run("custom variables", self.t_custom_variables)
            r.run("game and category names", lambda: self.t_names(original))
            r.run("offset", lambda: self.t_offset(original))
        finally:
            try:
                for i, segment in enumerate(original["segments"]):
                    c.call("setsplitname", {"index": i, "name": segment["name"]})
                c.call("setgamename", {"name": original["gameName"]})
                c.call("setcategoryname", {"name": original["categoryName"]})
                if c.call("gettimerphase") == "NotRunning":
                    c.call("setoffset", {"time": original["startingOffset"]})
            except Exception as e:
                print("  !! could not restore the run: %s" % e)
            listener.close()

    def t_rename(self, original):
        c = self.client
        if not original["segments"]:
            raise Skip("the run has no splits")
        assert c.call("setsplitname", {"index": 0, "name": "ws-test A"}) == {"index": 0, "name": "ws-test A"}
        self.listener.event("run-manually-modified")
        assert c.call("getsplitname", {"index": 0}) == "ws-test A"
        assert c.text("setsplitname -1 ws-test B")["ok"]
        assert c.call("getsplitname", {"index": -1}) == "ws-test B"
        # Positional arguments as a JSON array.
        assert c.call("setsplitname", [0, "ws-test C"])["name"] == "ws-test C"
        c.expect_error("setsplitname", {"index": 10000, "name": "x"}, "invalid_args")
        assert c.call("state")["run"]["hasChanged"] is True

    def t_custom_variables(self):
        c = self.client
        value = "value-%d" % int(time.time())
        c.call("setcustomvariable", {"name": TEST_VARIABLE, "value": value})
        event = self.listener.event("custom-variable-changed")
        assert event["data"] == {"name": TEST_VARIABLE, "value": value}, event
        assert c.call("getcustomvariablevalue", {"name": TEST_VARIABLE}) == value
        assert c.text('setcustomvariable ["%s", "array form"]' % TEST_VARIABLE)["ok"]
        assert c.call("getcustomvariables")[TEST_VARIABLE] == "array form"
        state = c.call("state")
        assert state["run"]["metadata"]["customVariables"][TEST_VARIABLE]["value"] == "array form"
        c.expect_error("setcustomvariable", {"value": "no name"}, "invalid_args")

    def t_names(self, original):
        c = self.client
        c.call("setgamename", {"name": "ws-test game"})
        c.call("setcategoryname", {"name": "ws-test category"})
        assert c.call("getgamename") == "ws-test game"
        assert c.call("getcategoryname") == "ws-test category"
        c.call("setgamename", {"name": original["gameName"]})
        c.call("setcategoryname", {"name": original["categoryName"]})

    def t_offset(self, original):
        c = self.client
        if c.call("gettimerphase") != "NotRunning":
            c.expect_error("setoffset", {"time": 0}, "invalid_phase")
            raise Skip("the timer is running")
        c.call("setoffset", {"time": -1500})
        assert c.call("getoffset") == -1500
        assert c.text("setoffset 0:02")["ok"] and c.call("getoffset") == 2000
        c.call("setoffset", {"time": original["startingOffset"]})

    # Hotkeys ----------------------------------------------------------------------------------

    def t_global_hotkeys(self):
        self.require_writable()
        c = self.client
        listener, _ = self.connect(2, {"events": ["global-hotkeys-changed"], "includeState": False})
        original = c.call("globalhotkeysenabled")
        try:
            c.call("disableglobalhotkeys" if original else "enableglobalhotkeys")
            assert c.call("globalhotkeysenabled") is (not original)
            assert listener.event("global-hotkeys-changed")["data"]["enabled"] is (not original)
            c.call("setglobalhotkeys", {"enabled": original})
            assert c.call("globalhotkeysenabled") is original
        finally:
            c.call("setglobalhotkeys", {"enabled": original})
            listener.close()

    def t_hotkey_profile(self):
        self.require_writable()
        c = self.client
        c.expect_error("switchhotkeyprofile", {"profile": "No Such Profile"}, "invalid_args")
        response = c.request("switchhotkeyprofile", {"profile": c.call("gethotkeyprofile")})
        if not response["ok"] and response["error"]["code"] == "unsupported":
            raise Skip("unsupported by this LiveSplit version")
        assert response["ok"], response

    # Files ------------------------------------------------------------------------------------

    def run_files(self):
        r = self.r
        r.run("preconditions", self.files_preconditions)
        if not getattr(self, "files_ready", False):
            return
        listener, _ = self.connect(2, {"includeState": False})
        self.listener = listener
        try:
            r.run("path validation", self.t_path_validation)
            r.run("save a screenshot", self.t_save_screenshot)
            r.run("save and reopen the layout", self.t_layout)
            r.run("save and reopen the splits", self.t_splits)
        finally:
            listener.close()

    def files_preconditions(self):
        self.require_writable()
        if not self.hello["fileCommandsAllowed"]:
            raise Skip("enable 'Allow clients to save and open splits, layouts and screenshots'")
        self.files_ready = True

    def t_path_validation(self):
        c = self.client
        c.expect_error("savesplitsas", {"path": "relative.lss"}, "invalid_args")
        c.expect_error("savesplitsas", {"path": "C:\\ws-test\\wrong.txt"}, "invalid_args")
        c.expect_error("savelayoutas", {"path": "C:\\ws-test\\wrong.lss"}, "invalid_args")
        c.expect_error("switchsplits", {"path": "C:\\ws-test\\does-not-exist.lss"}, "invalid_args")
        c.expect_error("switchlayout", {"path": "C:\\ws-test\\does-not-exist.lsl"}, "invalid_args")
        c.expect_error("savesplitsscreenshot", {"path": "C:\\ws-test\\shot.jpg"}, "invalid_args")

    def livesplit_is_local(self):
        return self.args.host in ("127.0.0.1", "localhost", "::1") and os.name == "nt"

    def t_save_screenshot(self):
        splits = self.client.call("getsplitspath")
        folder = self.args.remote_temp or (tempfile.gettempdir() if self.livesplit_is_local() else None)
        if folder is None and splits:
            folder = os.path.dirname(splits) if "\\" not in splits else splits.rsplit("\\", 1)[0]
        if folder is None:
            raise Skip("pass --remote-temp with a folder on the LiveSplit computer")
        separator = "\\" if "\\" in folder or ":" in folder else "/"
        path = folder.rstrip("\\/") + separator + "ws-test-screenshot.png"
        response = self.client.request("savesplitsscreenshot", {"path": path})
        if not response["ok"] and response["error"]["code"] == "unsupported":
            raise Skip("unsupported by this LiveSplit version")
        assert response["ok"], response
        if self.livesplit_is_local():
            assert os.path.getsize(path) > 0
            os.remove(path)
            return "saved and removed %s" % path
        return "saved %s (on the LiveSplit computer; delete it)" % path

    def t_layout(self):
        c = self.client
        path = c.call("getlayoutpath")
        if not path:
            raise Skip("the layout was never saved")
        response = c.request("savelayout")
        if not response["ok"] and response["error"]["code"] == "unsupported":
            raise Skip("unsupported by this LiveSplit version")
        assert response["ok"] and response["data"]["path"] == path, response
        assert c.call("switchlayout", {"path": path})["path"] == path
        self.listener.event("layout-changed")
        return path

    def t_splits(self):
        c = self.client
        state = c.call("state")
        path = state["run"]["filePath"]
        if not path:
            raise Skip("the splits were never saved")
        if state["run"]["hasChanged"]:
            raise Skip("the splits have unsaved changes; save them in LiveSplit first")
        if state["timerState"] != "NotRunning":
            raise Skip("the timer is running (reopening the splits would reset it)")
        response = c.request("savesplits")
        if not response["ok"] and response["error"]["code"] == "unsupported":
            raise Skip("unsupported by this LiveSplit version")
        assert response["ok"] and response["data"]["path"] == path, response
        assert c.call("switchsplits", {"path": path})["path"] == path
        self.listener.event("run-changed")
        assert c.call("getsplitspath") == path
        return path


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--host", default="127.0.0.1", help="LiveSplit computer (default 127.0.0.1)")
    parser.add_argument("--port", type=int, default=15721, help="server port (default 15721)")
    parser.add_argument("--token", help="token set in the component settings")
    parser.add_argument("--control", action="store_true", help="drive the timer")
    parser.add_argument("--edit-run", action="store_true", help="edit and restore the run")
    parser.add_argument("--hotkeys", action="store_true", help="toggle global hotkeys and profiles")
    parser.add_argument("--files", action="store_true", help="save and reopen splits and layout, screenshots")
    parser.add_argument("--all", action="store_true", help="enable every test")
    parser.add_argument("--wait-refresh", type=float, metavar="SECONDS",
                        help="also wait up to SECONDS for the periodic refresh event")
    parser.add_argument("--remote-temp", metavar="FOLDER",
                        help="folder on the LiveSplit computer for the screenshot test")
    parser.add_argument("-v", "--verbose", action="store_true", help="show details and tracebacks")
    args = parser.parse_args()
    if args.all:
        args.control = args.edit_run = args.hotkeys = args.files = True

    runner = Runner(args.verbose)
    print("Testing the LiveSplit WebSocket Server at ws://%s:%d/" % (args.host, args.port))
    try:
        Tests(args, runner).run()
    except KeyboardInterrupt:
        print("\nInterrupted.")
    sys.exit(runner.summary())


if __name__ == "__main__":
    main()
