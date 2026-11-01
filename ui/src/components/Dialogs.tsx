import { TextAttributes } from "@opentui/core";
import type { JSX } from "@opentui/solid";
import { For, Show, createSignal } from "solid-js";
import type { ToolInfo, Track } from "../bridge/protocol";
import { basename, formatTime, singleLine } from "../lib/format";
import type { LibraryEntry } from "../lib/library";
import { savedDate } from "../lib/library";
import type { LogEntry } from "../lib/logs";
import { theme } from "../theme";
import { ActionButton, Spinner, Tint } from "./Widgets";

export function Overlay(props: {
  title: string;
  width: number | `${number}%`;
  onClose: () => void;
  children: JSX.Element;
}) {
  return (
    <box
      position="absolute"
      top={0}
      left={0}
      width="100%"
      height="100%"
      zIndex={50}
      backgroundColor="#03060bcc"
      justifyContent="center"
      alignItems="center"
      onMouseDown={() => props.onClose()}
    >
      <box
        width={props.width}
        maxHeight="84%"
        flexDirection="column"
        border
        borderStyle="double"
        borderColor={theme.borderStrong}
        backgroundColor={theme.surface}
        title={` ${props.title} `}
        titleColor={theme.accent}
        padding={1}
        gap={1}
        onMouseDown={(event) => event.stopPropagation()}
      >
        {props.children}
      </box>
    </box>
  );
}

function Line(props: { children: JSX.Element }) {
  return (
    <text fg={theme.textSoft} selectable={false} wrapMode="none">
      {props.children}
    </text>
  );
}

function Command(props: { name: string; args?: string; description: string }) {
  return (
    <Line>
      <Tint color={theme.text} bold>
        {props.name}
      </Tint>
      <Show when={props.args} fallback={<Tint color={theme.dim}> </Tint>}>
        <Tint color={theme.dim}> {props.args} </Tint>
      </Show>
      <Tint color={theme.faint}>  {props.description}</Tint>
    </Line>
  );
}

export function HelpDialog(props: { onClose: () => void; narrow: boolean }) {
  return (
    <Overlay
      title="COMMAND REFERENCE"
      width={props.narrow ? "92%" : 86}
      onClose={props.onClose}
    >
      <scrollbox flexGrow={1} minHeight={8} maxHeight={26} focusable={false}>
        <box flexDirection="column" gap={1}>
          <box flexDirection="column">
            <text fg={theme.accent} attributes={TextAttributes.BOLD} selectable={false}>
              PLAYBACK
            </text>
            <Command name="add" args="<url | search>" description="queue a video, playlist, or pick from search results" />
            <Command name="search" args="<query>" description="show the top matches before queueing" />
            <Command name="play / pause" description="toggle the current track" />
            <Command name="next / previous" description="move through the queue" />
            <Command name="goto" args="<# | title>" description="focus a row" />
            <Command name="shuffle" description="reshuffle upcoming tracks" />
            <Command name="undo" description="undo the last queue change" />
            <Command name="stop" description="stop and clear the queue" />
            <Command name="clear playlist" description="stop but keep the window open" />
          </box>

          <box flexDirection="column">
            <text fg={theme.info} attributes={TextAttributes.BOLD} selectable={false}>
              LIBRARY
            </text>
            <Line>
              <Tint color={theme.accent}>library</Tint>
              <Tint color={theme.dim}> saved queues  ·  </Tint>
              <Tint color={theme.accent}>save</Tint>
              <Tint color={theme.dim}> &lt;name&gt;  ·  </Tint>
              <Tint color={theme.accent}>load</Tint>
              <Tint color={theme.dim}> &lt;name&gt;</Tint>
            </Line>
            <Line>
              <Tint color={theme.accent}>delete-queue</Tint>
              <Tint color={theme.dim}> &lt;name&gt;  ·  </Tint>
              <Tint color={theme.accent}>export</Tint>
              <Tint color={theme.dim}> [file.m3u]  ·  </Tint>
              <Tint color={theme.accent}>import</Tint>
              <Tint color={theme.dim}> &lt;file.m3u&gt;  ·  </Tint>
              <Tint color={theme.accent}>favorite</Tint>
            </Line>
          </box>

          <box flexDirection="column">
            <text fg={theme.highlight} attributes={TextAttributes.BOLD} selectable={false}>
              TOOLS
            </text>
            <Command name="tools" description="show yt-dlp and mpv resolution" />
            <Command name="tools install" args="[yt-dlp|mpv|all]" description="download into the managed folder" />
            <Command name="tools set" args="<tool> <path>" description="pin an existing executable" />
            <Command name="tools clear" args="<tool>" description="remove a pinned path" />
            <Command name="logs" description="open diagnostics and subprocess reports" />
            <Command name="version" description="print the backend version" />
          </box>

          <box flexDirection="column">
            <text fg={theme.info} attributes={TextAttributes.BOLD} selectable={false}>
              KEYBOARD
            </text>
            <Line>
              <Tint color={theme.accent}>ctrl+p</Tint>
              <Tint color={theme.dim}> play/pause  </Tint>
              <Tint color={theme.accent}>ctrl+n</Tint>
              <Tint color={theme.dim}> next  </Tint>
              <Tint color={theme.accent}>ctrl+b</Tint>
              <Tint color={theme.dim}> previous</Tint>
            </Line>
            <Line>
              <Tint color={theme.accent}>ctrl+←/→</Tint>
              <Tint color={theme.dim}> seek ±10s  </Tint>
              <Tint color={theme.accent}>ctrl+↑/↓</Tint>
              <Tint color={theme.dim}> volume</Tint>
            </Line>
            <Line>
              <Tint color={theme.accent}>alt+←/→</Tint>
              <Tint color={theme.dim}> seek  </Tint>
              <Tint color={theme.accent}>F1</Tint>
              <Tint color={theme.dim}> help  </Tint>
              <Tint color={theme.accent}>F2</Tint>
              <Tint color={theme.dim}> tools  </Tint>
              <Tint color={theme.accent}>F3</Tint>
              <Tint color={theme.dim}> focus view</Tint>
            </Line>
            <Line>
              <Tint color={theme.accent}>F4</Tint>
              <Tint color={theme.dim}> diagnostics  </Tint>
              <Tint color={theme.accent}>F5</Tint>
              <Tint color={theme.dim}> theme  </Tint>
              <Tint color={theme.accent}>F6</Tint>
              <Tint color={theme.dim}> library</Tint>
            </Line>
            <Line>
              <Tint color={theme.accent}>shift+↑↓</Tint>
              <Tint color={theme.dim}> reorder upcoming  </Tint>
              <Tint color={theme.accent}>shift+n</Tint>
              <Tint color={theme.dim}> play next</Tint>
            </Line>
            <Line>
              <Tint color={theme.accent}>d</Tint>
              <Tint color={theme.dim}> remove selected  </Tint>
              <Tint color={theme.accent}>u</Tint>
              <Tint color={theme.dim}> undo  </Tint>
              <Tint color={theme.accent}>f</Tint>
              <Tint color={theme.dim}> favorite</Tint>
            </Line>
            <Line>
              <Tint color={theme.accent}>tab</Tint>
              <Tint color={theme.dim}> command → navigation → focus  </Tint>
              <Tint color={theme.accent}>esc</Tint>
              <Tint color={theme.dim}> back / close</Tint>
            </Line>
            <Line>
              <Tint color={theme.accent}>esc</Tint>
              <Tint color={theme.dim}> close dialog / leave input  </Tint>
              <Tint color={theme.accent}>ctrl+q</Tint>
              <Tint color={theme.dim}> quit</Tint>
            </Line>
          </box>

            <Line>
              <Tint color={theme.dim}>Navigation/Focus: </Tint>
              <Tint color={theme.accent}>space</Tint>
              <Tint color={theme.dim}> play, </Tint>
              <Tint color={theme.accent}>n/p</Tint>
              <Tint color={theme.dim}> skip, </Tint>
              <Tint color={theme.accent}>↑↓</Tint>
              <Tint color={theme.dim}> select, </Tint>
              <Tint color={theme.accent}>↵</Tint>
              <Tint color={theme.dim}> play row, </Tint>
              <Tint color={theme.accent}>t</Tint>
              <Tint color={theme.dim}> tools, </Tint>
              <Tint color={theme.accent}>l</Tint>
              <Tint color={theme.dim}> logs, </Tint>
              <Tint color={theme.accent}>a</Tint>
              <Tint color={theme.dim}> add, </Tint>
              <Tint color={theme.accent}>/</Tint>
              <Tint color={theme.dim}> type.</Tint>
            </Line>
            <Line>
              <Tint color={theme.dim}>Queue drag: </Tint>
              <Tint color={theme.accent}>drag rows</Tint>
              <Tint color={theme.dim}> to reorder upcoming tracks.</Tint>
            </Line>
        </box>
      </scrollbox>

      <box flexDirection="row" justifyContent="space-between" alignItems="center">
        <text fg={theme.faint} selectable={false}>
          Click outside or press esc to close.
        </text>
        <ActionButton label="CLOSE" onPress={props.onClose} tone="accent" />
      </box>
    </Overlay>
  );
}

export function ToolsDialog(props: {
  tools: ToolInfo[];
  toolRoot?: string;
  overrideFile?: string;
  message: string;
  busy: boolean;
  busyLabel?: string;
  failure?: string;
  narrow: boolean;
  onClose: () => void;
  onInstall: (tool?: string) => void;
  onRefresh: () => void;
  onOpenLogs: () => void;
}) {
  return (
    <Overlay title="PLAYBACK TOOLS" width={props.narrow ? "94%" : 78} onClose={props.onClose}>
      <text fg={theme.dim} selectable={false} wrapMode="word">
        {singleLine(props.message, 220)}
      </text>

      <Show when={props.failure} fallback={<box height={0} />}>
        <text fg={theme.danger} selectable={false} wrapMode="word">
          {singleLine(props.failure, 220)}
        </text>
      </Show>

      <box flexDirection="column" gap={1}>
        <For each={props.tools}>
          {(tool) => {
            const label = () => (props.busyLabel ?? "").toLowerCase();
            const updating = () =>
              props.busy &&
              (label().includes(tool.displayName.toLowerCase()) ||
                label().includes("installing playback tools"));
            const checking = () => props.busy && label().includes("checking");
            const status = () =>
              updating()
                ? "updating"
                : checking()
                  ? "checking"
                  : tool.runnable
                    ? "ready"
                    : "missing";

            return (
              <box
                flexDirection="column"
                border={["bottom"]}
                borderColor={theme.border}
                paddingBottom={0}
              >
                <box flexDirection="row" alignItems="center" gap={1}>
                  <text
                    fg={tool.runnable ? theme.accent : theme.danger}
                    selectable={false}
                    wrapMode="none"
                    width={1}
                  >
                    {tool.runnable ? "●" : "○"}
                  </text>
                  <text fg={theme.text} attributes={TextAttributes.BOLD} selectable={false} width={9}>
                    {tool.displayName}
                  </text>
                  <text
                    fg={
                      updating() || checking()
                        ? theme.info
                        : tool.runnable
                          ? theme.accent
                          : theme.warning
                    }
                    selectable={false}
                    wrapMode="none"
                    width={9}
                  >
                    {status()}
                  </text>
                  <text
                    fg={theme.dim}
                    selectable={false}
                    wrapMode="none"
                    truncate
                    flexGrow={1}
                    minWidth={0}
                  >
                    {tool.path ?? "not found"}
                  </text>
                </box>
                <Show when={tool.pinnedPath} fallback={<box height={0} />}>
                  <text fg={theme.faint} selectable={false} wrapMode="none">
                    {`pinned: ${basename(tool.pinnedPath)}`}
                  </text>
                </Show>
              </box>
            );
          }}
        </For>
      </box>

      <box flexDirection="column">
        <text selectable={false} wrapMode="none">
          <Tint color={theme.dim}>managed folder </Tint>
          <Tint color={theme.faint}>{props.toolRoot ?? "unknown"}</Tint>
        </text>
        <text selectable={false} wrapMode="none">
          <Tint color={theme.dim}>saved paths </Tint>
          <Tint color={theme.faint}>{props.overrideFile ?? "unknown"}</Tint>
        </text>
      </box>

      <Show when={props.busy} fallback={<box height={0} />}>
        <Spinner active={() => props.busy} label="Tools are updating..." />
      </Show>

      <box flexDirection="row" gap={1} flexWrap="wrap">
        <ActionButton
          label="REPAIR ALL"
          onPress={() => props.onInstall()}
          tone="accent"
          disabled={props.busy}
        />
        <ActionButton
          label="YT-DLP"
          onPress={() => props.onInstall("yt-dlp")}
          disabled={props.busy}
        />
        <ActionButton label="MPV" onPress={() => props.onInstall("mpv")} disabled={props.busy} />
        <ActionButton
          label="REFRESH"
          onPress={props.onRefresh}
          tone="highlight"
          disabled={props.busy}
        />
        <ActionButton label="LOGS" onPress={props.onOpenLogs} />
        <box flexGrow={1} />
        <ActionButton label="CLOSE" onPress={props.onClose} />
      </box>

      <text fg={theme.faint} selectable={false} wrapMode="word">
        For a custom binary, type: tools set yt-dlp &lt;full path to yt-dlp&gt;
      </text>
    </Overlay>
  );
}

export function LogsDialog(props: {
  logs: LogEntry[];
  onClose: () => void;
  narrow: boolean;
}) {  const levelColor = (level: LogEntry["level"]) =>
    level === "error" ? theme.danger : level === "warn" ? theme.warning : theme.dim;

  return (
    <Overlay
      title="DIAGNOSTICS"
      width={props.narrow ? "94%" : 100}
      onClose={props.onClose}
    >
      <text fg={theme.dim} selectable={false} wrapMode="word">
        Tool reports and backend stderr are collected here. The renderer never prints
        subprocess output directly, so this view is the only place it appears.
      </text>

      <Show
        when={props.logs.length > 0}
        fallback={
          <box flexGrow={1} minHeight={6} justifyContent="center" alignItems="center">
            <text fg={theme.faint} selectable={false}>
              No diagnostics yet.
            </text>
          </box>
        }
      >
        <scrollbox
          flexGrow={1}
          minHeight={8}
          maxHeight={24}
          focusable={false}
          stickyScroll
          stickyStart="bottom"
          backgroundColor={theme.bg}
        >
          <box flexDirection="column">
            <For each={props.logs}>
              {(entry) => (
                <box flexDirection="row" gap={1} alignItems="flex-start">
                  <text fg={theme.faint} selectable={false} wrapMode="none" width={8}>
                    {new Date(entry.at).toLocaleTimeString([], {
                      hour: "2-digit",
                      minute: "2-digit",
                      second: "2-digit",
                    })}
                  </text>
                  <text
                    fg={levelColor(entry.level)}
                    selectable={false}
                    wrapMode="none"
                    width={6}
                  >
                    {entry.level.toUpperCase()}
                  </text>
                  <text fg={theme.dim} selectable={false} wrapMode="none" width={8} truncate>
                    {entry.source}
                  </text>
                  <text fg={theme.textSoft} selectable={false} wrapMode="word" flexGrow={1} minWidth={0}>
                    {singleLine(entry.line, 1000)}
                  </text>
                </box>
              )}
            </For>
          </box>
        </scrollbox>
      </Show>

      <box flexDirection="row" justifyContent="space-between" alignItems="center">
        <text fg={theme.faint} selectable={false}>
          F4 toggles this view. Esc closes it.
        </text>
        <ActionButton label="CLOSE" onPress={props.onClose} tone="accent" />
      </box>
    </Overlay>
  );
}

function SearchResultRow(props: {
  track: Track;
  index: number;
  selected: boolean;
  onSelect: (index: number) => void;
}) {
  const [hovered, setHovered] = createSignal(false);

  return (
    <box
      height={1}
      flexShrink={0}
      flexDirection="row"
      gap={1}
      paddingLeft={1}
      paddingRight={1}
      backgroundColor={
        props.selected ? theme.selectedRow : hovered() ? theme.surfaceHover : theme.surface
      }
      onMouseOver={() => setHovered(true)}
      onMouseOut={() => setHovered(false)}
      onMouseDown={(event) => {
        event.stopPropagation();
        event.preventDefault();
        props.onSelect(props.index);
      }}
    >
      <text fg={props.selected ? theme.highlight : theme.dim} selectable={false} width={1}>
        {props.selected ? "›" : "·"}
      </text>
      <text fg={theme.faint} selectable={false} wrapMode="none" width={4} textAlign="right">
        {String(props.index + 1).padStart(3, " ")}
      </text>
      <text
        fg={props.selected ? theme.text : theme.textSoft}
        attributes={props.selected ? TextAttributes.BOLD : undefined}
        selectable={false}
        wrapMode="none"
        truncate
        flexGrow={1}
        minWidth={0}
      >
        {props.track.title}
      </text>
      <text
        fg={theme.dim}
        selectable={false}
        wrapMode="none"
        width={18}
        truncate
        flexShrink={0}
      >
        {singleLine(props.track.displaySource, 18)}
      </text>
      <text fg={theme.faint} selectable={false} wrapMode="none" width={7} textAlign="right">
        {formatTime(props.track.durationSeconds)}
      </text>
    </box>
  );
}

export function SearchDialog(props: {
  query: string;
  results: Track[];
  selectedIndex: number;
  narrow: boolean;
  onSelect: (index: number) => void;
  onClose: () => void;
}) {
  return (
    <Overlay
      title="SEARCH RESULTS"
      width={props.narrow ? "94%" : 92}
      onClose={props.onClose}
    >
      <text fg={theme.dim} selectable={false} wrapMode="word">
        {`Top matches for "${singleLine(props.query, 120)}". Pick one to add it to the queue.`}
      </text>

      <Show
        when={props.results.length > 0}
        fallback={
          <box flexGrow={1} minHeight={5} justifyContent="center" alignItems="center">
            <text fg={theme.faint} selectable={false}>
              No results. Try a different search.
            </text>
          </box>
        }
      >
        <scrollbox flexGrow={1} minHeight={5} maxHeight={20} focusable={false}>
          <For each={props.results}>
            {(track, index) => (
              <SearchResultRow
                track={track}
                index={index()}
                selected={index() === props.selectedIndex}
                onSelect={props.onSelect}
              />
            )}
          </For>
        </scrollbox>
      </Show>

      <box flexDirection="row" justifyContent="space-between" alignItems="center">
        <text fg={theme.faint} selectable={false} wrapMode="none">
          <Tint color={theme.muted}>↑↓</Tint>
          <Tint color={theme.faint}> select  ·  </Tint>
          <Tint color={theme.accent}>↵</Tint>
          <Tint color={theme.faint}> add to queue  ·  </Tint>
          <Tint color={theme.muted}>esc</Tint>
          <Tint color={theme.faint}> close</Tint>
        </text>
        <ActionButton label="CLOSE" onPress={props.onClose} />
      </box>
    </Overlay>
  );
}

function LibraryQueueRow(props: {
  entry: Extract<LibraryEntry, { kind: "queue" }>;
  selected: boolean;
  onActivate: (index: number) => void;
}) {
  const [hovered, setHovered] = createSignal(false);

  return (
    <box
      height={1}
      flexShrink={0}
      flexDirection="row"
      gap={1}
      paddingLeft={1}
      paddingRight={1}
      backgroundColor={
        props.selected ? theme.selectedRow : hovered() ? theme.surfaceHover : theme.surface
      }
      onMouseOver={() => setHovered(true)}
      onMouseOut={() => setHovered(false)}
      onMouseDown={(event) => {
        event.stopPropagation();
        event.preventDefault();
        props.onActivate(props.entry.index);
      }}
    >
      <text fg={props.selected ? theme.highlight : theme.dim} selectable={false} width={1}>
        {props.selected ? "›" : "▤"}
      </text>
      <text
        fg={props.selected ? theme.text : theme.textSoft}
        attributes={props.selected ? TextAttributes.BOLD : undefined}
        selectable={false}
        wrapMode="none"
        truncate
        flexGrow={1}
        minWidth={0}
      >
        {singleLine(props.entry.name, 120)}
      </text>
      <text fg={theme.faint} selectable={false} wrapMode="none" width={12} textAlign="right">
        {props.entry.trackCount} tracks
      </text>
      <text fg={theme.faint} selectable={false} wrapMode="none" width={7} textAlign="right">
        {savedDate(props.entry.savedAtUtc)}
      </text>
    </box>
  );
}

function LibraryFavoriteRow(props: {
  entry: Extract<LibraryEntry, { kind: "favorite" }>;
  selected: boolean;
  onActivate: (index: number) => void;
}) {
  const [hovered, setHovered] = createSignal(false);

  return (
    <box
      height={1}
      flexShrink={0}
      flexDirection="row"
      gap={1}
      paddingLeft={1}
      paddingRight={1}
      backgroundColor={
        props.selected ? theme.selectedRow : hovered() ? theme.surfaceHover : theme.surface
      }
      onMouseOver={() => setHovered(true)}
      onMouseOut={() => setHovered(false)}
      onMouseDown={(event) => {
        event.stopPropagation();
        event.preventDefault();
        props.onActivate(props.entry.index);
      }}
    >
      <text fg={props.selected ? theme.highlight : theme.warning} selectable={false} width={1}>
        {props.selected ? "›" : "★"}
      </text>
      <text
        fg={props.selected ? theme.text : theme.textSoft}
        attributes={props.selected ? TextAttributes.BOLD : undefined}
        selectable={false}
        wrapMode="none"
        truncate
        flexGrow={1}
        minWidth={0}
      >
        {props.entry.track.title}
      </text>
      <text fg={theme.dim} selectable={false} wrapMode="none" width={18} truncate>
        {singleLine(props.entry.track.displaySource, 18)}
      </text>
      <text fg={theme.faint} selectable={false} wrapMode="none" width={7} textAlign="right">
        {formatTime(props.entry.track.durationSeconds)}
      </text>
    </box>
  );
}

export function LibraryDialog(props: {
  entries: LibraryEntry[];
  selectedIndex: number;
  narrow: boolean;
  onActivate: (index: number) => void;
  onDelete: () => void;
  onExport: () => void;
  onClose: () => void;
}) {
  const queues = () => props.entries.filter((entry) => entry.kind === "queue");
  const favorites = () => props.entries.filter((entry) => entry.kind === "favorite");
  const selected = () => props.entries.find((entry) => entry.index === props.selectedIndex);
  const queueSelected = () => selected()?.kind === "queue";

  return (
    <Overlay title="LIBRARY" width={props.narrow ? "94%" : 96} onClose={props.onClose}>
      <text fg={theme.dim} selectable={false} wrapMode="word">
        Save the current queue with <Tint color={theme.accent}>save &lt;name&gt;</Tint>, import
        with <Tint color={theme.accent}>import &lt;file.m3u&gt;</Tint>, or export with{" "}
        <Tint color={theme.accent}>export [file.m3u]</Tint>.
      </text>

      <Show
        when={props.entries.length > 0}
        fallback={
          <box flexGrow={1} minHeight={6} justifyContent="center" alignItems="center">
            <text fg={theme.faint} selectable={false}>
              No saved queues or favorites yet.
            </text>
          </box>
        }
      >
        <scrollbox flexGrow={1} minHeight={6} maxHeight={22} focusable={false}>
          <Show when={queues().length > 0} fallback={<box height={0} />}>
            <box height={1} flexDirection="row" gap={1} paddingLeft={2}>
              <text fg={theme.accent} attributes={TextAttributes.BOLD} selectable={false}>
                SAVED QUEUES
              </text>
            </box>
            <For each={queues()}>
              {(entry) => (
                <LibraryQueueRow
                  entry={entry as Extract<LibraryEntry, { kind: "queue" }>}
                  selected={entry.index === props.selectedIndex}
                  onActivate={props.onActivate}
                />
              )}
            </For>
          </Show>

          <Show when={favorites().length > 0} fallback={<box height={0} />}>
            <box height={1} flexDirection="row" gap={1} paddingLeft={2}>
              <text fg={theme.highlight} attributes={TextAttributes.BOLD} selectable={false}>
                FAVORITES
              </text>
            </box>
            <For each={favorites()}>
              {(entry) => (
                <LibraryFavoriteRow
                  entry={entry as Extract<LibraryEntry, { kind: "favorite" }>}
                  selected={entry.index === props.selectedIndex}
                  onActivate={props.onActivate}
                />
              )}
            </For>
          </Show>
        </scrollbox>
      </Show>

      <box flexDirection="row" gap={1} alignItems="center" flexWrap="wrap">
        <ActionButton
          label="DELETE"
          onPress={props.onDelete}
          tone="danger"
          disabled={!queueSelected()}
        />
        <ActionButton
          label="EXPORT"
          onPress={props.onExport}
          tone="highlight"
          disabled={!queueSelected()}
        />
        <text fg={theme.faint} selectable={false} wrapMode="none">
          <Tint color={theme.accent}>↵</Tint>
          <Tint color={theme.faint}> load / queue  ·  </Tint>
          <Tint color={theme.muted}>x</Tint>
          <Tint color={theme.faint}> delete  ·  </Tint>
          <Tint color={theme.muted}>e</Tint>
          <Tint color={theme.faint}> export</Tint>
        </text>
        <box flexGrow={1} />
        <ActionButton label="CLOSE" onPress={props.onClose} />
      </box>
    </Overlay>
  );
}
