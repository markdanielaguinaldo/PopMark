import { TextAttributes } from "@opentui/core";
import type { ScrollBoxRenderable } from "@opentui/core";
import { useTerminalDimensions } from "@opentui/solid";
import { For, Show, createEffect, createSignal } from "solid-js";
import type { PlaylistEntry } from "../lib/playlist";
import { formatTime, pluralize } from "../lib/format";
import { theme } from "../theme";
import { ActionButton, Panel, SectionLabel, Tint } from "./Widgets";

function QueueRow(props: {
  entry: PlaylistEntry;
  selected: boolean;
  onPlay: (index: number) => void;
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
    props.entry.kind === "current" || props.selected
      ? theme.accent
      : props.entry.kind === "previous"
        ? theme.faint
        : theme.dim;

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
        props.onPlay(props.entry.index);
      }}
    >
      <text fg={markerColor()} selectable={false} wrapMode="none" width={1}>
        {marker()}
      </text>
      <text fg={theme.faint} selectable={false} wrapMode="none" width={4} textAlign="right">
        {String(props.entry.index + 1).padStart(3, " ")}
      </text>
      <text
        fg={titleColor()}
        attributes={props.entry.kind === "current" ? TextAttributes.BOLD : undefined}
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
  onPlay: (index: number) => void;
  onShuffle: () => void;
  onClear: () => void;
  onFocusInput: () => void;
}) {
  let scroll: ScrollBoxRenderable | undefined;
  const dimensions = useTerminalDimensions();

  createEffect(() => {
    dimensions();
    const index = props.selectedIndex;
    if (index < 0) return;
    scroll?.scrollChildIntoView(`queue-row-${index}`);
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
      borderColor={theme.border}
    >
      <Show when={!props.tiny} fallback={<box height={0} />}>
        <box
        flexDirection="row"
        alignItems="center"
        justifyContent="space-between"
        paddingLeft={1}
        paddingRight={1}
        height={1}
        flexShrink={0}
      >
        <text selectable={false} wrapMode="none">
          <Tint color={theme.muted}>{pluralize(props.entries.length, "track")}</Tint>
          <Show when={!props.compact} fallback={<Tint color={theme.faint}> </Tint>}>
            <Tint color={theme.faint}> · </Tint>
            <Tint color={theme.dim}>{counts().previous} played</Tint>
            <Tint color={theme.faint}> · </Tint>
            <Tint color={theme.dim}>{counts().pending} up next</Tint>
          </Show>
        </text>
        <box flexDirection="row" gap={1} flexShrink={0}>
          <ActionButton
            label={props.compact ? "SHUF" : "SHUFFLE"}
            onPress={props.onShuffle}
            tone="highlight"
          />
          <ActionButton
            label={props.compact ? "CLR" : "CLEAR"}
            onPress={props.onClear}
            tone="danger"
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
              The queue is empty.
            </text>
            <Show when={!props.compact} fallback={<box height={0} />}>
              <text fg={theme.faint} selectable={false}>
                Press / and paste a YouTube URL or playlist.
              </text>
              <SectionLabel>ADD SOMETHING WORTH HEARING</SectionLabel>
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
          <For each={props.entries}>
            {(entry) => (
              <QueueRow
                entry={entry}
                selected={entry.index === props.selectedIndex}
                onPlay={props.onPlay}
              />
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
            <Tint color={theme.dim}> play selection</Tint>
            <Tint color={theme.faint}> ↑↓ move · click a row to play</Tint>
          </text>
        </box>
      </Show>
    </Panel>
  );
}
