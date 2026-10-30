import { TextAttributes } from "@opentui/core";
import type { InputRenderable } from "@opentui/core";
import {
  useKeyboard,
  useRenderer,
  useTerminalDimensions,
} from "@opentui/solid";
import {
  Show,
  createMemo,
  createSignal,
  onCleanup,
  onMount,
  type JSX,
} from "solid-js";
import { createStore, reconcile } from "solid-js/store";
import { createBridgeClient } from "./bridge/client";
import type { BridgeClient, BridgeEvents } from "./bridge/client";
import type {
  BackendMessage,
  PlaybackStatus,
  ToolInfo,
  Track,
} from "./bridge/protocol";
import { Footer } from "./components/Footer";
import { Header } from "./components/Header";
import { HelpDialog, LogsDialog, ToolsDialog } from "./components/Dialogs";
import { NowPlaying } from "./components/NowPlaying";
import { QueuePanel } from "./components/QueuePanel";
import { Sidebar } from "./components/Sidebar";
import { Splash } from "./components/Splash";
import { ActionButton, Tint } from "./components/Widgets";
import { clamp } from "./lib/format";
import { appendLog, makeLogEntry, type LogEntry } from "./lib/logs";
import { liveElapsedSeconds, safeDuration } from "./lib/playback";
import {
  browseHistory,
  buildPlaylistEntries,
  followCurrentSelection,
  initialHistory,
  moveSelection,
  rememberCommand,
  type HistoryState,
} from "./lib/playlist";
import { theme, cycleTheme, themeLabel } from "./theme";

type Dialog = "help" | "tools" | "logs" | null;
type InputMode = "command" | "navigation" | "focus" | "modal";

type AppStore = {
  connection: "connecting" | "ready" | "closed" | "failed";
  backendVersion?: string;
  backendPath: string;
  error?: string;
  logs: LogEntry[];
  queue: {
    current?: Track;
    previous: Track[];
    pending: Track[];
    revision: number;
    currentIndex: number;
  };
  playback: {
    status: PlaybackStatus;
    elapsed: number;
    duration?: number;
    volume: number;
    message: string;
    busy: boolean;
    busyLabel?: string;
  };
  tools: ToolInfo[];
  toolRoot?: string;
  overrideFile?: string;
};

const VIEW_COMMANDS = new Set(["splash", "art", "visual", "visualizer"]);
const QUEUE_VIEW_COMMANDS = new Set(["playlist", "queue", "menu"]);

export function App(props: {
  createClient?: (events: BridgeEvents) => BridgeClient;
}) {
  const renderer = useRenderer();
  const dimensions = useTerminalDimensions();
  const narrow = () => dimensions().width < 100;
  const compact = () => dimensions().width < 88;
  // Phone-sized terminals: Termux portrait is often ~45x24, so the regular
  // layout clips its toolbar and transport row before `narrow` would help.
  const dense = () => dimensions().width < 60;
  const short = () => dimensions().height < 30;
  const tiny = () => dimensions().height < 22;

  const [store, setStore] = createStore<AppStore>({
    connection: "connecting",
    backendPath: "",
    logs: [],
    queue: {
      previous: [],
      pending: [],
      revision: 0,
      currentIndex: -1,
    },
    playback: {
      status: "Stopped",
      elapsed: 0,
      volume: 100,
      message: "Starting PopMark...",
      busy: false,
    },
    tools: [],
  });

  const [dialog, setDialog] = createSignal<Dialog>(null);
  const [splash, setSplash] = createSignal(false);
  const [mode, setMode] = createSignal<InputMode>("command");
  const [selectedIndex, setSelectedIndex] = createSignal(-1);
  const [clock, setClock] = createSignal(Date.now());
  const [themeToast, setThemeToast] = createSignal<string>();

  let inputRef: InputRenderable | undefined;
  let history: HistoryState = initialHistory();
  let positionReceivedAt = Date.now();
  let pendingSeek: { target: number; at: number } | null = null;
  let lastSeekSentAt = 0;
  let lastVolumeSentAt = 0;
  let toolOperationRunning = false;
  let quitting = false;
  let modeBeforeOverlay: InputMode = "command";
  let modeBeforeFocus: InputMode = "command";
  let dialogBeforeOverlay: Dialog = null;
  let lastFollowedCurrentIndex = -2;
  let themeToastTimer: ReturnType<typeof setTimeout> | undefined;

  const editing = () => mode() === "command";

  const client = (props.createClient ?? createBridgeClient)({
    onMessage(message) {
      handleMessage(message);
    },
    onLog(line) {
      setStore("logs", (logs) => appendLog(logs, makeLogEntry("backend", line)));
    },
    onExit(info) {
      if (quitting) return;
      setStore("connection", "closed");
      setStore("error", info.error ?? `The PopMark backend exited (code ${info.code ?? "?"}).`);
    },
  });

  function handleMessage(message: BackendMessage) {
    switch (message.type) {
      case "hello":
        setStore("connection", "ready");
        setStore("backendVersion", message.version);
        setStore("error", undefined);
        break;

      case "queue":
        pendingSeek = null;
        {
          const previousUrl = store.queue.current?.url;
          const nextUrl = message.current?.url;
          setStore("queue", {
            current: message.current,
            previous: message.previous,
            pending: message.pending,
            revision: message.revision,
            currentIndex: message.currentIndex,
          });

          if (previousUrl !== nextUrl) {
            setStore("playback", "elapsed", 0);
            setStore("playback", "duration", safeDuration(message.current?.durationSeconds));
            positionReceivedAt = Date.now();
          }
        }
        setSelectedIndex((current) => {
          const length =
            message.previous.length + (message.current ? 1 : 0) + message.pending.length;
          const selected = followCurrentSelection(
            message.currentIndex,
            current,
            length,
            lastFollowedCurrentIndex,
          );
          lastFollowedCurrentIndex = message.currentIndex;
          return selected;
        });
        break;

      case "state":
        positionReceivedAt = Date.now();

        if (pendingSeek) {
          const age = positionReceivedAt - pendingSeek.at;
          const arrived =
            Number.isFinite(message.elapsedSeconds) &&
            Math.abs(message.elapsedSeconds - pendingSeek.target) <= 1.5;
          if (arrived || age > 1500) {
            pendingSeek = null;
          } else {
            // A tick emitted just before the seek can arrive just after the
            // optimistic update. Keep the target position until the backend
            // catches up instead of snapping the bar backwards.
            setStore("playback", {
              status: message.status,
              duration: safeDuration(message.durationSeconds),
              volume: clamp(message.volumePercent, 0, 130),
              message: message.message,
              busy: message.busy,
            });
            break;
          }
        }

        setStore("playback", {
          status: message.status,
          elapsed:
            Number.isFinite(message.elapsedSeconds) && message.elapsedSeconds > 0
              ? message.elapsedSeconds
              : 0,
          duration: safeDuration(message.durationSeconds),
          volume: clamp(message.volumePercent, 0, 130),
          message: message.message,
          busy: message.busy,
        });
        break;

      case "tools":
        setStore("tools", reconcile(message.tools));
        setStore("toolRoot", message.toolRoot);
        setStore("overrideFile", message.overrideFile);
        setStore("error", undefined);
        break;

      case "busy":
        setStore("playback", "busy", message.busy);
        setStore("playback", "busyLabel", message.label);
        break;

      case "log":
        setStore("logs", (logs) =>
          appendLog(logs, makeLogEntry(message.source, message.line, message.level)),
        );
        break;

      case "error":
        setStore("playback", "message", message.message);
        setStore("error", message.message);
        break;

      case "ack":
        if (!message.ok) {
          setStore("playback", "message", message.message ?? "Command failed.");
          setStore("error", message.message ?? "Command failed.");
        } else {
          setStore("error", undefined);
        }
        break;

      case "bye":
        if (!quitting) setStore("connection", "closed");
        break;
    }
  }

  const playlist = createMemo(() =>
    buildPlaylistEntries({
      current: store.queue.current,
      previous: store.queue.previous,
      pending: store.queue.pending,
    }),
  );

  const duration = createMemo(
    () =>
      safeDuration(store.playback.duration) ??
      safeDuration(store.queue.current?.durationSeconds),
  );

  const displayElapsed = createMemo(() =>
    liveElapsedSeconds(
      store.playback.elapsed,
      store.playback.status,
      positionReceivedAt,
      clock(),
      duration(),
    ),
  );

  const toolsReady = createMemo(() => store.tools.filter((tool) => tool.runnable).length);

  function focusInput() {
    setMode("command");
  }

  function enterNavigation() {
    setMode("navigation");
  }

  function openDialog(next: Exclude<Dialog, null>) {
    if (mode() !== "modal") {
      modeBeforeOverlay = mode();
      dialogBeforeOverlay = null;
    } else {
      dialogBeforeOverlay = dialog();
    }

    setDialog(next);
    setMode("modal");
  }

  function openHelp() {
    openDialog("help");
  }

  function openTools() {
    openDialog("tools");
    void refreshTools();
  }

  function openLogs() {
    openDialog("logs");
  }

  function closeDialog() {
    if (dialogBeforeOverlay) {
      const previous = dialogBeforeOverlay;
      dialogBeforeOverlay = null;
      setDialog(previous);
      setMode("modal");
      return;
    }

    setDialog(null);
    const restore = modeBeforeOverlay;
    modeBeforeOverlay = "command";
    setMode(restore === "modal" ? "command" : restore);
  }

  function openFocus() {
    if (mode() === "focus") return;
    if (mode() !== "modal") modeBeforeFocus = mode();
    setSplash(true);
    setMode("focus");
  }

  function changeTheme(direction: 1 | -1) {
    const next = cycleTheme(direction);
    setThemeToast(`${themeLabel()} · ${next.description}`);
    if (themeToastTimer) clearTimeout(themeToastTimer);
    themeToastTimer = setTimeout(() => setThemeToast(undefined), 2200);
  }

  function closeFocus() {
    setSplash(false);
    const restore = modeBeforeFocus;
    modeBeforeFocus = "command";
    setMode(restore === "navigation" ? "navigation" : "command");
  }

  function onInputRef(input: InputRenderable) {
    inputRef = input;
    input.on("focused", () => {
      if (mode() === "navigation") setMode("command");
    });
    input.on("blurred", () => {
      if (mode() === "command") setMode("navigation");
    });
  }

  function sendCommand(name: string) {
    client.send({ type: "command", name });
  }

  function togglePlayback() {
    sendCommand("play");
  }

  function nextTrack() {
    sendCommand("next");
  }

  function previousTrack() {
    sendCommand("previous");
  }

  function stopPlayback() {
    sendCommand("stop");
  }

  function shuffle() {
    sendCommand("shuffle");
  }

  function clearQueue() {
    sendCommand("clear-playlist");
  }

  function seekBy(seconds: number) {
    const total = duration();
    const target = Math.max(
      0,
      total ? Math.min(total, displayElapsed() + seconds) : displayElapsed() + seconds,
    );
    setStore("playback", "elapsed", target);
    positionReceivedAt = Date.now();
    pendingSeek = { target, at: positionReceivedAt };
    client.send({ type: "seek", delta: seconds });
  }

  function seekToRatio(ratio: number, final = true) {
    const total = duration();
    if (!total || total <= 0) return;
    const seconds = clamp(ratio, 0, 1) * total;
    setStore("playback", "elapsed", seconds);
    positionReceivedAt = Date.now();
    pendingSeek = { target: seconds, at: positionReceivedAt };

    const now = Date.now();
    if (!final && now - lastSeekSentAt < 80) return;
    lastSeekSentAt = now;
    client.send({ type: "seek", seconds });
  }

  function setVolume(percent: number, final = true) {
    const value = clamp(Math.round(percent), 0, 130);
    setStore("playback", "volume", value);

    const now = Date.now();
    if (!final && now - lastVolumeSentAt < 80) return;
    lastVolumeSentAt = now;
    client.send({ type: "volume", percent: value });
  }

  function adjustVolume(delta: number) {
    setVolume(store.playback.volume + delta);
  }

  function playIndex(index: number) {
    client.send({ type: "play-index", index });
  }

  function submit(rawValue: string) {
    const value = rawValue.trim();
    if (!value) return;

    history = rememberCommand(history, value);
    const [command, ...rest] = value.split(/\s+/);
    const lowered = command.toLowerCase();

    if (rest.length === 0 && ["help", "h", "?", "controls", "control"].includes(lowered)) {
      openHelp();
      return;
    }

    if (rest.length === 0 && ["tools", "tool", "doctor"].includes(lowered)) {
      openTools();
      return;
    }

    if (rest.length === 0 && ["logs", "diagnostics", "diag"].includes(lowered)) {
      openLogs();
      return;
    }

    if (rest.length === 0 && VIEW_COMMANDS.has(lowered)) {
      openFocus();
      return;
    }

    if (rest.length === 0 && QUEUE_VIEW_COMMANDS.has(lowered)) {
      if (mode() === "focus") closeFocus();
      else {
        setSplash(false);
        focusInput();
      }
      return;
    }

    void client
      .request({ type: "input", text: value })
      .catch((error: unknown) => {
        setStore("playback", "message", error instanceof Error ? error.message : String(error));
      });
  }

  async function refreshTools() {
    try {
      await client.request({ type: "tools", action: "status" });
    } catch (error) {
      const text = error instanceof Error ? error.message : String(error);
      setStore("playback", "message", text);
      setStore("error", text);
    }
  }

  async function installTools(tool?: string) {
    if (toolOperationRunning) return;
    toolOperationRunning = true;
    setStore("playback", "busy", true);
    setStore("playback", "busyLabel", tool ? `Installing ${tool}...` : "Installing playback tools...");
    try {
      await client.request({ type: "tools", action: "install", tool });
      await refreshTools();
    } catch (error) {
      const text = error instanceof Error ? error.message : String(error);
      setStore("playback", "message", text);
      setStore("error", text);
    } finally {
      toolOperationRunning = false;
      setStore("playback", "busy", false);
      setStore("playback", "busyLabel", undefined);
    }
  }

  function browse(delta: -1 | 1) {
    const next = browseHistory(history, delta, inputRef?.value ?? "");
    history = next.state;
    if (inputRef) inputRef.value = next.value;
  }

  async function quit() {
    if (quitting) return;
    quitting = true;
    await client.close();
    renderer.destroy();
    await renderer.closed.catch(() => undefined);
    process.exit(0);
  }

  useKeyboard((key) => {
    if (key.eventType === "release") return;

    if (key.ctrl && key.name === "q") {
      key.preventDefault();
      void quit();
      return;
    }

    if (key.ctrl && key.name === "c") {
      key.preventDefault();
      if (editing() && inputRef?.value) {
        inputRef.value = "";
        return;
      }
      void quit();
      return;
    }

    if (key.name === "f4") {
      key.preventDefault();
      if (mode() === "modal" && dialog() === "logs") closeDialog();
      else openLogs();
      return;
    }

    if (key.name === "f5") {
      key.preventDefault();
      changeTheme(key.shift ? -1 : 1);
      return;
    }

    if (mode() === "modal") {
      if (key.name === "escape" || key.name === "return" ||
          (key.name === "f1" && dialog() === "help") ||
          (key.name === "f2" && dialog() === "tools"))
      {
        key.preventDefault();
        closeDialog();
      }
      return;
    }

    if (key.name === "f1") {
      key.preventDefault();
      openHelp();
      return;
    }

    if (key.name === "f2") {
      key.preventDefault();
      openTools();
      return;
    }

    if (key.name === "f3") {
      key.preventDefault();
      if (mode() === "focus") closeFocus();
      else openFocus();
      return;
    }

    if (handlePlaybackShortcuts(key)) return;

    if (mode() === "focus") {
      if (key.name === "escape" || key.name === "tab") {
        key.preventDefault();
        closeFocus();
        return;
      }

      handleNavigationKey(key);
      return;
    }

    if (mode() === "navigation") {
      if (key.name === "escape") {
        key.preventDefault();
        focusInput();
        return;
      }

      if (key.name === "tab") {
        key.preventDefault();
        openFocus();
        return;
      }

      if (key.name === "/" || key.name === "i") {
        key.preventDefault();
        focusInput();
        return;
      }

      if (key.name === "a") {
        key.preventDefault();
        if (inputRef) inputRef.value = "add ";
        focusInput();
        return;
      }

      handleNavigationKey(key);
      return;
    }

    // Command mode: printable keys belong to the input. Only mode keys,
    // shortcuts, and history are handled globally.
    if (key.name === "escape") {
      key.preventDefault();
      enterNavigation();
      return;
    }

    if (key.name === "tab") {
      key.preventDefault();
      enterNavigation();
      return;
    }

    if (key.name === "up" || key.name === "down") {
      key.preventDefault();
      browse(key.name === "up" ? -1 : 1);
    }
  });

  function handlePlaybackShortcuts(key: import("@opentui/core").KeyEvent): boolean {
    if (key.ctrl) {
      switch (key.name) {
        case "p":
          key.preventDefault();
          togglePlayback();
          return true;
        case "n":
          key.preventDefault();
          nextTrack();
          return true;
        case "b":
          key.preventDefault();
          previousTrack();
          return true;
        case "s":
          key.preventDefault();
          shuffle();
          return true;
        case "left":
          key.preventDefault();
          seekBy(-10);
          return true;
        case "right":
          key.preventDefault();
          seekBy(10);
          return true;
        case "up":
          key.preventDefault();
          adjustVolume(10);
          return true;
        case "down":
          key.preventDefault();
          adjustVolume(-10);
          return true;
      }
    }

    if (key.meta || key.option) {
      if (key.name === "left" || key.name === "right") {
        key.preventDefault();
        seekBy(key.name === "left" ? -10 : 10);
        return true;
      }

      if (key.name === "up" || key.name === "down") {
        key.preventDefault();
        adjustVolume(key.name === "up" ? 10 : -10);
        return true;
      }
    }

    return false;
  }

  function handleNavigationKey(key: import("@opentui/core").KeyEvent) {
    const entries = playlist();

    switch (key.name) {
      case "space":
      case "return":
        key.preventDefault();
        if (key.name === "return" && selectedIndex() >= 0) playIndex(selectedIndex());
        else togglePlayback();
        return;
      case "n":
        key.preventDefault();
        nextTrack();
        return;
      case "p":
        key.preventDefault();
        previousTrack();
        return;
      case "s":
        key.preventDefault();
        shuffle();
        return;
      case "?":
        key.preventDefault();
        openHelp();
        return;
      case "t":
        key.preventDefault();
        openTools();
        return;
      case "l":
        key.preventDefault();
        openLogs();
        return;
      case "up":
      case "k":
        key.preventDefault();
        setSelectedIndex((index) => moveSelection(index, -1, entries.length));
        return;
      case "down":
      case "j":
        key.preventDefault();
        setSelectedIndex((index) => moveSelection(index, 1, entries.length));
        return;
      case "home":
        key.preventDefault();
        setSelectedIndex(entries.length > 0 ? 0 : -1);
        return;
      case "g":
      case "G":
        key.preventDefault();
        setSelectedIndex(
          key.shift || key.name === "G"
            ? entries.length > 0
              ? entries.length - 1
              : -1
            : entries.length > 0
              ? 0
              : -1,
        );
        return;
      case "end":
        key.preventDefault();
        setSelectedIndex(entries.length > 0 ? entries.length - 1 : -1);
        return;
      case "left":
        key.preventDefault();
        seekBy(-10);
        return;
      case "right":
        key.preventDefault();
        seekBy(10);
        return;
    }
  }

  onMount(() => {
    const startupUrl = process.env.POPMARK_START_URL?.trim();

    client
      .start()
      .then(() => {
        setStore("backendPath", client.backendPath());
        if (startupUrl) client.send({ type: "add", target: startupUrl });
      })
      .catch((error: unknown) => {
        setStore("connection", "failed");
        setStore("error", error instanceof Error ? error.message : String(error));
      });
  });

  const timers = [
    setInterval(() => setClock(Date.now()), 500),
  ];

  onCleanup(() => {
    for (const timer of timers) clearInterval(timer);
    if (themeToastTimer) clearTimeout(themeToastTimer);
    void client.close();
  });

  const errorPanel = () => (
    <box flexGrow={1} justifyContent="center" alignItems="center" flexDirection="column" gap={1}>
      <text fg={theme.danger} attributes={TextAttributes.BOLD} selectable={false}>
        {store.connection === "failed" ? "Could not start the PopMark backend" : "Backend disconnected"}
      </text>
      <text fg={theme.muted} selectable={false} wrapMode="word" width="80%">
        {store.error ?? "The backend stopped unexpectedly."}
      </text>
      <Show when={store.logs.length > 0} fallback={<box height={0} />}>
        <text fg={theme.faint} selectable={false} wrapMode="word" width="80%">
          {store.logs
            .slice(-4)
            .map((entry) => `${entry.source}: ${entry.line}`)
            .join("  ·  ")}
        </text>
      </Show>
      <box flexDirection="row" gap={1}>
        <ActionButton label="LOGS" onPress={openLogs} tone="highlight" />
        <ActionButton label="QUIT" onPress={() => void quit()} tone="danger" />
      </box>
    </box>
  );

  return (
    <box width="100%" height="100%" flexDirection="column" backgroundColor={theme.bg}>
      <Header
        status={store.playback.status}
        backendVersion={store.backendVersion}
        tools={store.tools}
        narrow={narrow}
        compact={compact}
        dense={dense}
        onOpenTools={openTools}
      />

      <box flexGrow={1} flexDirection="row" gap={1} padding={dense() ? 0 : 1} minHeight={0}>
        <Show
          when={store.connection === "ready" || store.connection === "connecting"}
          fallback={<PanelShell>{errorPanel()}</PanelShell>}
        >
          <box flexDirection="column" flexGrow={1} minWidth={0} gap={1}>
            <NowPlaying
              compact={short()}
              tiny={tiny()}
              dense={dense()}
              current={store.queue.current}
              status={store.playback.status}
              elapsed={displayElapsed()}
              duration={duration()}
              volume={store.playback.volume}
              message={store.playback.message}
              busy={store.playback.busy}
              busyLabel={store.playback.busyLabel}
              columns={dimensions().width - (dense() ? 4 : narrow() ? 6 : 39)}
              onToggle={togglePlayback}
              onNext={nextTrack}
              onPrevious={previousTrack}
              onStop={stopPlayback}
              onShuffle={shuffle}
              onSeek={seekToRatio}
              onVolume={setVolume}
            />

            <QueuePanel
              entries={playlist()}
              selectedIndex={selectedIndex()}
              compact={short() || compact()}
              tiny={tiny()}
              dense={dense()}
              onPlay={playIndex}
              onShuffle={shuffle}
              onClear={clearQueue}
              onFocusInput={focusInput}
            />
          </box>

          <Show when={!narrow()} fallback={<box height={0} />}>
            <Sidebar
              tools={store.tools}
              compact={short()}
              onManageTools={openTools}
              onHelp={openHelp}
              onSplash={openFocus}
              onLogs={openLogs}
            />
          </Show>
        </Show>
      </box>

      <Footer
        mode={mode}
        narrow={narrow()}
        dense={dense()}
        connected={store.connection === "ready"}
        onSubmit={submit}
        onRef={onInputRef}
        onFocusInput={focusInput}
        onEnterNavigation={enterNavigation}
        onOpenHelp={openHelp}
        onOpenTools={openTools}
        onOpenFocus={openFocus}
        onOpenLogs={openLogs}
        onCycleTheme={() => changeTheme(1)}
        onQuit={() => void quit()}
      />

      <Show when={splash()} fallback={<box height={0} />}>
        <Splash
          current={store.queue.current}
          status={store.playback.status}
          elapsed={displayElapsed()}
          duration={duration()}
          volume={store.playback.volume}
          message={store.playback.message}
          toolsReady={toolsReady()}
          toolsTotal={store.tools.length}
          width={dimensions().width}
          height={dimensions().height}
          onSeek={seekToRatio}
          onClose={closeFocus}
          onTogglePlayback={togglePlayback}
          onCycleTheme={changeTheme}
        />
      </Show>

      <Show when={dialog() === "help"} fallback={<box height={0} />}>
        <HelpDialog onClose={closeDialog} narrow={narrow()} />
      </Show>

      <Show when={dialog() === "tools"} fallback={<box height={0} />}>
        <ToolsDialog
          tools={store.tools}
          toolRoot={store.toolRoot}
          overrideFile={store.overrideFile}
          message={store.playback.message}
          busy={store.playback.busy}
          busyLabel={store.playback.busyLabel}
          failure={store.error}
          narrow={narrow()}
          onClose={closeDialog}
          onInstall={(tool) => void installTools(tool)}
          onRefresh={() => void refreshTools()}
          onOpenLogs={openLogs}
        />
      </Show>

      <Show when={dialog() === "logs"} fallback={<box height={0} />}>
        <LogsDialog logs={store.logs} onClose={closeDialog} narrow={narrow()} />
      </Show>

      <Show when={themeToast() !== undefined} fallback={<box height={0} />}>
        <box
          id="theme-toast"
          position="absolute"
          bottom={4}
          right={2}
          zIndex={70}
          paddingLeft={1}
          paddingRight={1}
          backgroundColor={theme.surfaceRaised}
          border
          borderStyle="rounded"
          borderColor={theme.borderStrong}
        >
          <text selectable={false} wrapMode="none">
            <Tint color={theme.accent} bold>
              THEME
            </Tint>
            <Tint color={theme.faint}> · </Tint>
            <Tint color={theme.text}>{themeToast()}</Tint>
          </text>
        </box>
      </Show>
    </box>
  );
}

function PanelShell(props: { children: JSX.Element }) {
  return (
    <box flexGrow={1} flexDirection="column" border borderStyle="rounded" borderColor={theme.border}>
      {props.children}
    </box>
  );
}
