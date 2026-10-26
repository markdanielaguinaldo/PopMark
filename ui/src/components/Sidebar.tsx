import { TextAttributes } from "@opentui/core";
import { For, Show, type JSX } from "solid-js";
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
      <box width={10} height={1} flexShrink={0} backgroundColor={theme.surface}>
        <text fg={theme.accent} selectable={false} wrapMode="none" width={10}>
          {props.keys.padEnd(10, " ")}
        </text>
      </box>
      <text fg={theme.textSoft} selectable={false} wrapMode="none" truncate flexGrow={1} minWidth={0}>
        {props.label}
      </text>
    </box>
  );
}

function ShortcutGroup(props: {
  label: string;
  tone: string;
  children: JSX.Element;
}) {
  return (
    <box flexDirection="column" gap={0}>
      <text
        fg={props.tone}
        attributes={TextAttributes.BOLD}
        selectable={false}
        wrapMode="none"
      >
        {props.label}
      </text>
      {props.children}
    </box>
  );
}

export function Sidebar(props: {
  tools: ToolInfo[];
  compact?: boolean;
  onManageTools: () => void;
  onHelp: () => void;
  onSplash: () => void;
  onLogs: () => void;
}) {
  const ready = () => props.tools.filter((tool) => tool.runnable).length;

  return (
    <box flexDirection="column" width={32} flexShrink={0} gap={1}>
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
        <box height={1} />
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

      <Panel title="SHORTCUTS" flexGrow={1} minHeight={12} padding={1}>
        <box flexDirection="column" gap={0}>
          <ShortcutGroup label="PLAYBACK" tone={theme.accent}>
            <ShortcutRow keys="[ctrl+p]" label="play / pause" />
            <ShortcutRow keys="[ctrl+→]" label="next track" />
            <ShortcutRow keys="[ctrl+←]" label="previous" />
            <ShortcutRow keys="[ctrl+↑↓]" label="volume" />
            <ShortcutRow keys="[alt+←→]" label="seek ±10s" />
          </ShortcutGroup>

          <box height={1} />

          <ShortcutGroup label="VIEWS" tone={theme.highlight}>
            <ShortcutRow keys="[F1]" label="command help" onPress={props.onHelp} />
            <ShortcutRow keys="[F3]" label="focus view" onPress={props.onSplash} />
            <ShortcutRow keys="[F4]" label="diagnostics" onPress={props.onLogs} />
            <ShortcutRow keys="[F5]" label="cycle theme" />
          </ShortcutGroup>

          <Show when={!props.compact} fallback={<box height={0} />}>
            <box flexGrow={1} minHeight={0} />
            <box flexDirection="column" gap={0}>
              <text fg={theme.border} selectable={false} wrapMode="none">
                ───────────────────────────
              </text>
              <box height={1} />
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
                <Tint color={theme.faint}> move · </Tint>
                <Tint color={theme.muted}>↵</Tint>
                <Tint color={theme.faint}> play · </Tint>
                <Tint color={theme.accent} bold>
                  a
                </Tint>
                <Tint color={theme.faint}> add</Tint>
              </text>
            </box>
          </Show>
        </box>
      </Panel>
    </box>
  );
}
