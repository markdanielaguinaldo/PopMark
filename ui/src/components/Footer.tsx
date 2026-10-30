import { TextAttributes } from "@opentui/core";
import type { InputRenderable } from "@opentui/core";
import { Show, createEffect, createSignal } from "solid-js";
import { theme } from "../theme";
import { ActionButton, Tint } from "./Widgets";

export type InputMode = "command" | "navigation" | "focus" | "modal";

export function Footer(props: {
  mode: () => InputMode;
  narrow: boolean;
  dense: boolean;
  connected: boolean;
  onSubmit: (value: string) => void;
  onRef: (input: InputRenderable) => void;
  onFocusInput: () => void;
  onEnterNavigation: () => void;
  onOpenHelp: () => void;
  onOpenTools: () => void;
  onOpenFocus: () => void;
  onOpenLogs: () => void;
  onCycleTheme: () => void;
  onQuit: () => void;
}) {
  let inputRef: InputRenderable | undefined;
  const [inputNode, setInputNode] = createSignal<InputRenderable>();

  createEffect(() => {
    const input = inputNode();
    if (!input) return;
    if (props.mode() === "command") input.focus();
    else input.blur();
  });

  const modeLabel = () => {
    switch (props.mode()) {
      case "command":
        return "CMD";
      case "navigation":
        return "NAV";
      case "focus":
        return "FOCUS";
      default:
        return "DIALOG";
    }
  };

  const modeBackground = () => {
    switch (props.mode()) {
      case "command":
        return theme.accent;
      case "focus":
        return theme.highlight;
      case "modal":
        return theme.info;
      default:
        return theme.surfaceRaised;
    }
  };

  const modeForeground = () =>
    props.mode() === "navigation" ? theme.muted : theme.accentInk;

  const onlineLabel = () => (props.connected ? "ONLINE" : "OFFLINE");
  const onlineColor = () => (props.connected ? theme.accent : theme.danger);

  return (
    <box
      height={props.dense ? 4 : 3}
      flexShrink={0}
      flexDirection="column"
      border={["top"]}
      borderColor={props.mode() === "command" ? theme.accentSoft : theme.border}
      backgroundColor={theme.bg}
      paddingLeft={1}
      paddingRight={1}
    >
      <box flexDirection="row" gap={1} alignItems="center" height={1}>
        <text
          fg={theme.accent}
          attributes={TextAttributes.BOLD}
          selectable={false}
          wrapMode="none"
        >
          ›
        </text>
        <input
          id="command-input"
          ref={(input: InputRenderable) => {
            inputRef = input;
            setInputNode(input);
            props.onRef(input);
          }}
          onMouseDown={(event) => {
            event.stopPropagation();
            props.onFocusInput();
          }}
          flexGrow={1}
          placeholder="add <url or search>   ·   play   ·   next   ·   shuffle   ·   tools"
          placeholderColor={theme.dim}
          backgroundColor={theme.bg}
          focusedBackgroundColor={theme.bg}
          textColor={theme.text}
          focusedTextColor={theme.text}
          onSubmit={() => {
            const value = inputRef?.value ?? "";
            if (!value.trim()) return;
            if (inputRef) inputRef.value = "";
            props.onSubmit(value);
          }}
        />
        <Show when={!props.narrow} fallback={<box height={0} />}>
          <text fg={theme.faint} selectable={false} wrapMode="none">
            enter
          </text>
        </Show>
        <box
          flexShrink={0}
          height={1}
          paddingLeft={1}
          paddingRight={1}
          backgroundColor={modeBackground()}
          onMouseDown={(event) => {
            event.stopPropagation();
            if (props.mode() === "command") props.onEnterNavigation();
            else if (props.mode() === "navigation") props.onFocusInput();
          }}
        >
          <text
            fg={modeForeground()}
            attributes={TextAttributes.BOLD}
            selectable={false}
            wrapMode="none"
          >
            {modeLabel()}
          </text>
        </box>
      </box>

      <Show
        when={props.dense}
        fallback={
          <box flexDirection="row" gap={1} height={1} alignItems="center">
            <Show
              when={!props.narrow}
              fallback={
                // Termux and other touch terminals rarely have F-keys. Every view is
                // reachable from these one-cell buttons instead.
                <box flexDirection="row" gap={1} alignItems="center" flexShrink={0}>
                  <ActionButton label="HELP" onPress={props.onOpenHelp} variant="ghost" />
                  <ActionButton label="TOOLS" onPress={props.onOpenTools} variant="ghost" />
                  <ActionButton label="FOCUS" onPress={props.onOpenFocus} variant="ghost" />
                  <ActionButton label="LOGS" onPress={props.onOpenLogs} variant="ghost" />
                  <ActionButton
                    label="THEME"
                    onPress={props.onCycleTheme}
                    tone="highlight"
                    variant="ghost"
                  />
                  <ActionButton label="QUIT" onPress={props.onQuit} tone="danger" variant="ghost" />
                </box>
              }
            >
              <text selectable={false} wrapMode="none">
                <Tint color={theme.accent}>[F1]</Tint>
                <Tint color={theme.muted}> help </Tint>
                <Tint color={theme.accent}>[F2]</Tint>
                <Tint color={theme.muted}> tools </Tint>
                <Tint color={theme.accent}>[F3]</Tint>
                <Tint color={theme.muted}> focus </Tint>
                <Tint color={theme.accent}>[F4]</Tint>
                <Tint color={theme.muted}> logs </Tint>
                <Tint color={theme.accent}>[F5]</Tint>
                <Tint color={theme.muted}> theme </Tint>
                <Tint color={theme.faint}> · </Tint>
                <Tint color={theme.accent}>[ctrl+q]</Tint>
                <Tint color={theme.muted}> quit</Tint>
              </text>
            </Show>
            <box flexGrow={1} />
            <text fg={onlineColor()} selectable={false} wrapMode="none">
              {props.narrow ? onlineLabel() : props.connected ? "● backend connected" : "○ backend offline"}
            </text>
          </box>
        }
      >
        {/* Phone layout: two rows of tap targets, because six buttons plus the
            connection label do not fit beside each other below ~60 columns. */}
        <box flexDirection="row" gap={1} height={1} alignItems="center" flexShrink={0}>
          <ActionButton label="HELP" onPress={props.onOpenHelp} variant="ghost" />
          <ActionButton label="TOOLS" onPress={props.onOpenTools} variant="ghost" />
          <ActionButton label="FOCUS" onPress={props.onOpenFocus} variant="ghost" />
          <ActionButton label="LOGS" onPress={props.onOpenLogs} variant="ghost" />
        </box>
        <box flexDirection="row" gap={1} height={1} alignItems="center" flexShrink={0}>
          <ActionButton
            label="THEME"
            onPress={props.onCycleTheme}
            tone="highlight"
            variant="ghost"
          />
          <ActionButton label="QUIT" onPress={props.onQuit} tone="danger" variant="ghost" />
          <box flexGrow={1} />
          <text fg={onlineColor()} selectable={false} wrapMode="none">
            {onlineLabel()}
          </text>
        </box>
      </Show>
    </box>
  );
}
