import { TextAttributes } from "@opentui/core";
import type { ScrollBoxRenderable } from "@opentui/core";
import { useTerminalDimensions } from "@opentui/solid";
import { For, Show, createEffect, createSignal, onCleanup } from "solid-js";
import type { PlaylistEntry } from "../lib/playlist";
import { formatTime, pluralize } from "../lib/format";
import { theme } from "../theme";
import { ActionButton, Chip, Panel, Tint } from "./Widgets";

type QueueSection = {
  kind: PlaylistEntry["kind"];
  label: string;
  entries: PlaylistEntry[];
};

function sectionsOf(entries: PlaylistEntry[]): QueueSection[] {
  const sections: QueueSection[] = [];
  for (const entry of entries) {
    const last = sections[sections.length - 1];
    if (!last || last.kind !== entry.kind) {
      sections.push({
        kind: entry.kind,
        label: entry.kind === "previous" ? "HISTORY" : entry.kind === "current" ? "NOW" : "UP NEXT",
        entries: [],
      });
    }
    sections[sections.length - 1].entries.push(entry);
  }
  return sections;
}

function sectionColor(kind: PlaylistEntry["kind"]): string {
  if (kind === "current") return theme.accent;
  if (kind === "pending") return theme.highlight;
  return theme.dim;
}

type RowMouseEvent = { x: number; y: number };

function QueueRow(props: {
  entry: PlaylistEntry;
  selected: boolean;
  onMouseDown: (index: number, kind: PlaylistEntry["kind"], event: RowMouseEvent) => void;
  onMouseDrag: (index: number, kind: PlaylistEntry["kind"], event: RowMouseEvent) => void;
  onMouseUp: (index: number, kind: PlaylistEntry["kind"]) => void;
  onMouseDrop: (index: number, kind: PlaylistEntry["kind"]) => void;
}) {
  const [hovered, setHovered] = createSignal(false);

  const background = () => {
    if (props.entry.kind === "current") return theme.currentRow;
    if (props.selected) return theme.selectedRow;
    if (hovered()) return theme.surfaceHover;
    if (props.entry.kind === "previous") return theme.previousRow;
    return theme.surface;
  };

  const marker = () =>
    props.entry.kind === "current"
      ? "▶"
      : props.selected
        ? "›"
        : props.entry.kind === "previous"
          ? "✓"
          : "·";

  const markerColor = () =>
    props.entry.kind === "current"
      ? theme.accent
      : props.selected
        ? theme.highlight
        : props.entry.kind === "previous"
          ? theme.faint
          : theme.dim;

  const rail = () =>
    props.entry.kind === "current"
      ? "▌"
      : props.selected
        ? "▌"
        : props.entry.kind === "previous"
          ? "│"
          : "│";

  const railColor = () =>
    props.entry.kind === "current"
      ? theme.accent
      : props.selected
        ? theme.highlight
        : props.entry.kind === "previous"
          ? theme.faint
          : theme.border;

  const titleColor = () =>
    props.entry.kind === "current"
      ? theme.accent
      : props.entry.kind === "previous"
        ? theme.dim
        : theme.textSoft;

  return (
    <box
      id={`queue-row-${props.entry.index}`}
      height={1}
      flexShrink={0}
      flexDirection="row"
      gap={1}
      paddingLeft={1}
      paddingRight={1}
      backgroundColor={background()}
      onMouseOver={() => setHovered(true)}
      onMouseOut={() => setHovered(false)}
      onMouseDown={(event) => {
        event.stopPropagation();
        event.preventDefault();
        props.onMouseDown(props.entry.index, props.entry.kind, event);
      }}
      onMouseDrag={(event) => props.onMouseDrag(props.entry.index, props.entry.kind, event)}
      onMouseUp={() => props.onMouseUp(props.entry.index, props.entry.kind)}
      onMouseDrop={() => props.onMouseDrop(props.entry.index, props.entry.kind)}
    >
      <text fg={railColor()} selectable={false} wrapMode="none" width={1}>
        {rail()}
      </text>
      <text fg={markerColor()} selectable={false} wrapMode="none" width={1}>
        {marker()}
      </text>
      <text fg={theme.faint} selectable={false} wrapMode="none" width={4} textAlign="right">
        {String(props.entry.index + 1).padStart(3, " ")}
      </text>
      <text
        fg={titleColor()}
        attributes={
          props.entry.kind === "current" || props.selected
            ? TextAttributes.BOLD
            : undefined
        }
        selectable={false}
        wrapMode="none"
        truncate
        flexGrow={1}
        minWidth={0}
      >
        {props.entry.track.title}
      </text>
      <text fg={theme.faint} selectable={false} wrapMode="none" width={7} textAlign="right">
        {formatTime(props.entry.track.durationSeconds)}
      </text>
    </box>
  );
}

export function QueuePanel(props: {
  entries: PlaylistEntry[];
  selectedIndex: number;
  compact?: boolean;
  tiny?: boolean;
  dense?: boolean;
  canRemove: boolean;
  canPlayNext: boolean;
  onPlay: (index: number) => void;
  onSelect: (index: number) => void;
  onMove: (from: number, to: number) => void;
  onPlayNext: () => void;
  onRemove: () => void;
  onUndo: () => void;
  onShuffle: () => void;
  onClear: () => void;
  onFocusInput: () => void;
}) {
  let scroll: ScrollBoxRenderable | undefined;
  let pressedIndex: number | null = null;
  let pressedKind: PlaylistEntry["kind"] | null = null;
  let pressX = 0;
  let pressY = 0;
  let dragMoved = false;
  let clickTimer: ReturnType<typeof setTimeout> | undefined;
  let anchorIndex: number | null = null;
  let anchorTimer: ReturnType<typeof setTimeout> | undefined;
  let anchorRetries = 0;
  const dimensions = useTerminalDimensions();

  // Park the current track a third of the way down the viewport once the list
  // has a real layout, so "UP NEXT" is visible on short terminals instead of
  // the current row hugging the bottom edge.
  function applyAnchor(): boolean {
    const node = scroll;
    const index = anchorIndex;
    if (!node || index === null) return false;
    if (node.scrollHeight <= node.height) return false;
    const row = node.findDescendantById(`queue-row-${index}`);
    if (!row) return false;
    const offset = row.y - node.y + node.scrollTop;
    if (offset < 0) return false;
    const anchor = Math.max(1, Math.floor(node.height / 3));
    node.scrollTop = Math.max(0, offset - anchor);
    anchorIndex = null;
    return true;
  }

  // Row geometry is only valid after the layout pass that follows a queue
  // update, so retry for a few frames when the first attempt sees a stale tree.
  function scheduleAnchor() {
    if (anchorIndex === null) return;
    if (applyAnchor()) return;
    if (anchorRetries >= 20) {
      anchorIndex = null;
      return;
    }
    anchorRetries += 1;
    if (anchorTimer) clearTimeout(anchorTimer);
    anchorTimer = setTimeout(scheduleAnchor, 16);
  }

  onCleanup(() => {
    if (anchorTimer) clearTimeout(anchorTimer);
    if (clickTimer) clearTimeout(clickTimer);
  });

  function resetPress() {
    pressedIndex = null;
    pressedKind = null;
    dragMoved = false;
  }

  function handleRowDown(index: number, kind: PlaylistEntry["kind"], event: RowMouseEvent) {
    // A previous click may still be waiting for its drop/up handshake.
    if (clickTimer) {
      clearTimeout(clickTimer);
      clickTimer = undefined;
    }
    pressedIndex = index;
    pressedKind = kind;
    pressX = event.x;
    pressY = event.y;
    dragMoved = false;
    props.onSelect(index);
  }

  function handleRowDrag(_index: number, _kind: PlaylistEntry["kind"], event: RowMouseEvent) {
    if (pressedIndex === null || dragMoved) return;
    // A row is one cell tall, so any vertical travel means the pointer left the
    // row. Horizontal jitter of a cell or two is still a click.
    dragMoved = Math.abs(event.y - pressY) >= 1 || Math.abs(event.x - pressX) >= 3;
  }

  function handleRowUp(index: number) {
    const source = pressedIndex;
    if (source === null) return;

    // The renderer follows a captured mouse-up with a drop event for the row
    // under the pointer. Defer the click by a turn so a completed drag can
    // cancel it instead of starting playback.
    if (clickTimer) clearTimeout(clickTimer);
    clickTimer = setTimeout(() => {
      clickTimer = undefined;
      if (pressedIndex !== source) return;
      const wasMoved = dragMoved;
      resetPress();
      // Releasing on a different row means the pointer travelled between rows
      // even if the terminal never reported a drag event.
      if (!wasMoved && index === source) props.onPlay(source);
    }, 0);
  }

  function handleRowDrop(index: number, kind: PlaylistEntry["kind"]) {
    const source = pressedIndex;
    if (source === null) return;

    const sourceKind = pressedKind;
    const wasMoved = dragMoved;
    // Resolve the gesture before the renderer's second (target row) mouse-up
    // arrives, so it is not mistaken for a click.
    resetPress();

    if (sourceKind !== "pending" || kind !== "pending") return;
    if (source !== index) {
      props.onMove(source, index);
    } else if (!wasMoved) {
      props.onPlay(index);
    }
  }

  // On a phone the queue header costs a whole track row; drop it when the panel
  // is too short for both. Shuffle and clear remain available as commands.
  const hideHeader = () => props.tiny === true || (props.dense === true && dimensions().height < 24);

  createEffect(() => {
    dimensions();
    const index = props.selectedIndex;
    if (index < 0) {
      anchorIndex = null;
      return;
    }
    scroll?.scrollChildIntoView(`queue-row-${index}`);
    anchorIndex = props.entries[index]?.kind === "current" ? index : null;
    anchorRetries = 0;
    scheduleAnchor();
  });

  const counts = () => {
    const previous = props.entries.filter((entry) => entry.kind === "previous").length;
    const pending = props.entries.filter((entry) => entry.kind === "pending").length;
    return { previous, pending };
  };

  return (
    <Panel
      title="QUEUE"
      flexGrow={1}
      flexBasis={0}
      minWidth={0}
      minHeight={props.tiny ? 3 : props.compact ? 4 : 6}
      padding={0}
      gap={props.tiny ? 0 : 1}
    >
      <Show when={!hideHeader()} fallback={<box height={0} />}>
        <box
          flexDirection="row"
          alignItems="center"
          justifyContent="space-between"
          paddingLeft={1}
          paddingRight={1}
          height={1}
          flexShrink={0}
        >
          <box flexDirection="row" alignItems="center" gap={1} minWidth={0}>
            <text
              fg={theme.textSoft}
              attributes={TextAttributes.BOLD}
              selectable={false}
              wrapMode="none"
            >
              {pluralize(props.entries.length, "track").toUpperCase()}
            </text>
            <Show when={!props.compact} fallback={<box height={0} />}>
              <Chip label={`${counts().previous} PLAYED`} tone="muted" />
              <Chip label={`${counts().pending} UP NEXT`} tone="highlight" />
            </Show>
          </box>
          <box flexDirection="row" gap={1} flexShrink={0}>
            <Show when={!props.compact} fallback={<box height={0} />}>
              <ActionButton
                label="NEXT"
                onPress={props.onPlayNext}
                tone="highlight"
                variant="ghost"
                disabled={!props.canPlayNext}
              />
              <ActionButton
                label="REMOVE"
                onPress={props.onRemove}
                tone="danger"
                variant="ghost"
                disabled={!props.canRemove}
              />
              <ActionButton
                label="UNDO"
                onPress={props.onUndo}
                tone="info"
                variant="ghost"
              />
            </Show>
            <ActionButton
              label={props.compact ? "SHUF" : "SHUFFLE"}
              onPress={props.onShuffle}
              tone="highlight"
              variant="ghost"
            />
            <ActionButton
              label={props.compact ? "CLR" : "CLEAR"}
              onPress={props.onClear}
              tone="danger"
              variant="ghost"
            />
          </box>
        </box>
      </Show>

      <Show
        when={props.entries.length > 0}
        fallback={
          <box
            flexGrow={1}
            justifyContent="center"
            alignItems="center"
            flexDirection="column"
            gap={props.compact ? 0 : 1}
            onMouseDown={() => props.onFocusInput()}
          >
            <text fg={theme.dim} selectable={false}>
              ♫  The queue is empty.
            </text>
            <Show when={!props.compact} fallback={<box height={0} />}>
              <text fg={theme.faint} selectable={false}>
                Press / and paste a YouTube URL or playlist.
              </text>
              <text selectable={false} wrapMode="none">
                <Tint color={theme.accent} bold>
                  add {"<url or search>"}
                </Tint>
                <Tint color={theme.faint}> starts the first track</Tint>
              </text>
            </Show>
          </box>
        }
      >
        <scrollbox
          ref={(node: ScrollBoxRenderable) => {
            scroll = node;
          }}
          flexGrow={1}
          minHeight={1}
          backgroundColor={theme.surface}
          focusable={false}
          paddingRight={1}
        >
          <For each={sectionsOf(props.entries)}>
            {(section) => (
              <>
                <Show when={!props.compact && !props.tiny} fallback={<box height={0} />}>
                  <box
                    height={1}
                    flexShrink={0}
                    flexDirection="row"
                    alignItems="center"
                    gap={1}
                    paddingLeft={2}
                    paddingRight={1}
                  >
                    <text
                      fg={sectionColor(section.kind)}
                      attributes={TextAttributes.BOLD}
                      selectable={false}
                      wrapMode="none"
                    >
                      {section.label}
                    </text>
                    <box
                      flexGrow={1}
                      height={1}
                      border={["bottom"]}
                      borderColor={theme.border}
                    />
                  </box>
                </Show>
                <For each={section.entries}>
                  {(entry) => (
                    <QueueRow
                      entry={entry}
                      selected={entry.index === props.selectedIndex}
                      onMouseDown={handleRowDown}
                      onMouseDrag={handleRowDrag}
                      onMouseUp={handleRowUp}
                      onMouseDrop={handleRowDrop}
                    />
                  )}
                </For>
              </>
            )}
          </For>
        </scrollbox>
      </Show>

      <Show when={!props.compact} fallback={<box height={0} />}>
        <box height={1} flexShrink={0} paddingLeft={1}>
          <text selectable={false} wrapMode="none">
            <Tint color={theme.accent} bold>
              ↵
            </Tint>
            <Tint color={theme.dim}> play  ·  </Tint>
            <Tint color={theme.muted}>↑↓</Tint>
            <Tint color={theme.faint}> move  ·  </Tint>
            <Tint color={theme.muted}>shift+↑↓</Tint>
            <Tint color={theme.faint}> reorder  ·  </Tint>
            <Tint color={theme.muted}>d</Tint>
            <Tint color={theme.faint}> remove  ·  </Tint>
            <Tint color={theme.muted}>u</Tint>
            <Tint color={theme.faint}> undo  ·  drag rows</Tint>
          </text>
        </box>
      </Show>
    </Panel>
  );
}
