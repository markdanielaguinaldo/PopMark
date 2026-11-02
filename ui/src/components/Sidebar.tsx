import { TextAttributes } from "@opentui/core";
import { useTerminalDimensions } from "@opentui/solid";
import { For, Show } from "solid-js";
import type { ToolInfo } from "../bridge/protocol";
import { basename } from "../lib/format";
import { theme } from "../theme";
import { Panel, Tint } from "./Widgets";

function ShortcutRow(props: { keys: string; label: string; onPress?: () => void }) {
  return (
    <box
      flexDirection="row"
      gap={1}
      height={1}
      onMouseDown={
        props.onPress
          ? (event) => {
              event.stopPropagation();
              props.onPress?.();
            }
          : undefined
      }
    >
      <box width={9} height={1} flexShrink={0} backgroundColor={theme.surface}>
        <text fg={theme.accent} selectable={false} wrapMode="none" width={9}>
          {props.keys.padEnd(9, " ")}
        </text>
      </box>
      <text fg={theme.textSoft} selectable={false} wrapMode="none" truncate flexGrow={1} minWidth={0}>
        {props.label}
      </text>
    </box>
  );
}

export function Sidebar(props: {
  tools: ToolInfo[];
  compact?: boolean;
  onManageTools: () => void;
  onHelp: () => void;
  onLibrary: () => void;
  onSplash: () => void;
  onLogs: () => void;
}) {
  const ready = () => props.tools.filter((tool) => tool.runnable).length;
  const dimensions = useTerminalDimensions();
  const tight = () => props.compact || dimensions().height < 32;
  const condensedTools = () => dimensions().height < 36;
  const roomy = () => !props.compact && dimensions().height >= 40;

  return (
    <box flexDirection="column" width={34} flexShrink={0} gap={1}>
      <Panel title="TOOLS" flexShrink={0}>
        <box flexDirection="row" justifyContent="space-between" height={1}>
          <text
            fg={ready() === props.tools.length && props.tools.length > 0 ? theme.accent : theme.warning}
            selectable={false}
            wrapMode="none"
          >
            {`${ready()}/${props.tools.length} READY`}
          </text>
          <text fg={theme.faint} selectable={false} wrapMode="none">
            F2
          </text>
        </box>
        <Show when={!condensedTools()} fallback={<box height={0} />}>
          <box height={1} />
        </Show>
        <For each={props.tools}>
          {(tool) => (
            <box
              flexDirection="row"
              gap={1}
              height={1}
              onMouseDown={(event) => {
                event.stopPropagation();
                props.onManageTools();
              }}
            >
              <text
                fg={tool.runnable ? theme.accent : theme.danger}
                selectable={false}
                wrapMode="none"
                width={1}
              >
                {tool.runnable ? "●" : "○"}
              </text>
              <text fg={theme.textSoft} selectable={false} wrapMode="none" width={7}>
                {tool.displayName}
              </text>
              <text
                fg={tool.runnable ? theme.dim : theme.warning}
                selectable={false}
                wrapMode="none"
                truncate
                flexGrow={1}
                minWidth={0}
              >
                {tool.runnable ? basename(tool.path) : "missing"}
              </text>
            </box>
          )}
        </For>
        <Show when={props.tools.length === 0} fallback={<box height={0} />}>
          <text fg={theme.faint} selectable={false} wrapMode="none">
            No playback tools found.
          </text>
        </Show>
        <box height={1} />
        <box
          flexDirection="row"
          justifyContent="space-between"
          onMouseDown={(event) => {
            event.stopPropagation();
            props.onManageTools();
          }}
        >
          <text selectable={false} wrapMode="none">
            <Tint color={theme.accent} bold>
              manage tools
            </Tint>
            <Tint color={theme.faint}>  ›</Tint>
          </text>
          <text fg={theme.faint} selectable={false} wrapMode="none">
            [F2]
          </text>
        </box>
      </Panel>

      <Panel
        title="SHORTCUTS"
        flexGrow={1}
        minHeight={12}
        padding={1}
        paddingTop={dimensions().height < 36 ? 0 : undefined}
        paddingBottom={dimensions().height < 36 ? 0 : undefined}
      >
        <box flexDirection="column" gap={0}>
          <ShortcutRow keys="[ctrl+p]" label="play / pause" />
          <ShortcutRow keys="[ctrl+→]" label="next track" />
          <ShortcutRow keys="[ctrl+←]" label="previous" />
          <Show when={!tight()} fallback={<box height={0} />}>
            <ShortcutRow keys="[ctrl+↑↓]" label="volume" />
            <ShortcutRow keys="[alt+←→]" label="seek ±10s" />
          </Show>
          <Show when={!props.compact} fallback={<box height={0} />}>
            <ShortcutRow keys="[ctrl+-=]" label="text size" />
          </Show>

          <box height={1} />

          <ShortcutRow keys="[F1]" label="commands" onPress={props.onHelp} />
          <ShortcutRow keys="[F3]" label="focus view" onPress={props.onSplash} />
          <ShortcutRow keys="[F4]" label="diagnostics" onPress={props.onLogs} />
          <ShortcutRow keys="[F5]" label="cycle theme" />
          <ShortcutRow keys="[F6]" label="saved library" onPress={props.onLibrary} />

          <Show when={roomy()} fallback={<box height={0} />}>
            <box flexDirection="column" gap={0} flexShrink={0}>
              <text
                fg={theme.dim}
                attributes={TextAttributes.BOLD}
                selectable={false}
                wrapMode="none"
              >
                NAVIGATION
              </text>
              <text selectable={false} wrapMode="none">
                <Tint color={theme.accent} bold>
                  space
                </Tint>
                <Tint color={theme.dim}> play · </Tint>
                <Tint color={theme.accent} bold>
                  n/p
                </Tint>
                <Tint color={theme.dim}> skip</Tint>
              </text>
              <text selectable={false} wrapMode="none">
                <Tint color={theme.muted}>↑↓</Tint>
                <Tint color={theme.faint}> select · </Tint>
                <Tint color={theme.muted}>⇧↑↓</Tint>
                <Tint color={theme.faint}> reorder</Tint>
              </text>
              <text selectable={false} wrapMode="none">
                <Tint color={theme.muted}>↵</Tint>
                <Tint color={theme.faint}> play · </Tint>
                <Tint color={theme.muted}>d</Tint>
                <Tint color={theme.faint}> remove</Tint>
              </text>
              <text selectable={false} wrapMode="none">
                <Tint color={theme.muted}>u</Tint>
                <Tint color={theme.faint}> undo · </Tint>
                <Tint color={theme.muted}>f</Tint>
                <Tint color={theme.faint}> favorite · </Tint>
                <Tint color={theme.muted}>a</Tint>
                <Tint color={theme.faint}> add</Tint>
              </text>
            </box>
          </Show>
        </box>
      </Panel>
    </box>
  );
}
