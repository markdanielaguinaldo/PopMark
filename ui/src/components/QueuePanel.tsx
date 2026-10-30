import { TextAttributes } from "@opentui/core";
import type { ScrollBoxRenderable } from "@opentui/core";
import { useTerminalDimensions } from "@opentui/solid";
import { For, Show, createEffect, createSignal } from "solid-js";
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
        props.onPlay(props.entry.index);
      }}
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
  onPlay: (index: number) => void;
  onShuffle: () => void;
  onClear: () => void;
  onFocusInput: () => void;
}) {
  let scroll: ScrollBoxRenderable | undefined;
  const dimensions = useTerminalDimensions();

  // On a phone the queue header costs a whole track row; drop it when the panel
  // is too short for both. Shuffle and clear remain available as commands.
  const hideHeader = () => props.tiny === true || (props.dense === true && dimensions().height < 24);

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
                      onPlay={props.onPlay}
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
            <Tint color={theme.dim}> play selection</Tint>
            <Tint color={theme.faint}>  ·  </Tint>
            <Tint color={theme.muted}>↑↓</Tint>
            <Tint color={theme.faint}> move  ·  </Tint>
            <Tint color={theme.muted}>space</Tint>
            <Tint color={theme.faint}> pause  ·  click a row to play</Tint>
          </text>
        </box>
      </Show>
    </Panel>
  );
}
