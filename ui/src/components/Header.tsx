import { For, Show, createSignal, onCleanup } from "solid-js";
import { theme } from "../theme";
import { StatusChip, Tint } from "./Widgets";
import type { ToolInfo } from "../bridge/protocol";

function clockText(date: Date): string {
  return date.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
}

export function Header(props: {
  status: string;
  backendVersion?: string;
  tools: ToolInfo[];
  narrow: () => boolean;
  compact: () => boolean;
  onOpenTools: () => void;
}) {
  const [now, setNow] = createSignal(new Date());
  const timer = setInterval(() => setNow(new Date()), 30_000);
  onCleanup(() => clearInterval(timer));

  const toolLabel = (tool: ToolInfo) =>
    tool.displayName.toLowerCase() === "mpv" ? "MPV" : tool.displayName;
  const orderedTools = () =>
    [...props.tools].sort(
      (a, b) =>
        (a.name.toLowerCase() === "mpv" ? 0 : 1) - (b.name.toLowerCase() === "mpv" ? 0 : 1),
    );

  return (
    <box
      height={3}
      flexShrink={0}
      flexDirection="row"
      alignItems="center"
      justifyContent="space-between"
      paddingLeft={1}
      paddingRight={1}
      border={["bottom"]}
      borderColor={theme.border}
      backgroundColor={theme.bg}
    >
      <box flexDirection="row" alignItems="center" gap={1} minWidth={0}>
        <text selectable={false} wrapMode="none">
          <Tint color={theme.accent} bold>
            POPMARK
          </Tint>
          <Tint color={theme.faint}> │ </Tint>
          <Tint color={theme.muted}>terminal jukebox</Tint>
          <Tint color={theme.faint}> · </Tint>
          <Tint color={theme.dim}>
            {props.backendVersion ? `v${props.backendVersion}` : "connecting…"}
          </Tint>
        </text>
      </box>

      <box flexDirection="row" alignItems="center" gap={1} flexShrink={0}>
        <For each={orderedTools()}>
          {(tool, index) => (
            <>
              <box
                flexDirection="row"
                alignItems="center"
                gap={1}
                onMouseDown={(event) => {
                  event.stopPropagation();
                  props.onOpenTools();
                }}
              >
                <Show when={!props.narrow()} fallback={<box height={0} />}>
                  <text fg={theme.muted} selectable={false} wrapMode="none">
                    {toolLabel(tool)}
                  </text>
                </Show>
                <text
                  fg={tool.runnable ? theme.accent : theme.danger}
                  selectable={false}
                  wrapMode="none"
                >
                  {tool.runnable ? "●" : "○"}
                </text>
              </box>
              <Show when={index() < props.tools.length - 1} fallback={<box height={0} />}>
                <text fg={theme.faint} selectable={false} wrapMode="none">
                  │
                </text>
              </Show>
            </>
          )}
        </For>
        <Show when={props.tools.length > 0} fallback={<box height={0} />}>
          <text fg={theme.faint} selectable={false} wrapMode="none">
            │
          </text>
        </Show>
        <StatusChip status={props.status} />
        <Show when={!props.compact()} fallback={<box height={0} />}>
          <text fg={theme.dim} selectable={false} wrapMode="none">
            {clockText(now())}
          </text>
        </Show>
      </box>
    </box>
  );
}
