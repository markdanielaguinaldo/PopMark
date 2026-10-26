import { TextAttributes } from "@opentui/core";
import type { InputRenderable } from "@opentui/core";
import { Show, createEffect, createSignal } from "solid-js";
import { theme } from "../theme";
import { Tint } from "./Widgets";

export type InputMode = "command" | "navigation" | "focus" | "modal";

export function Footer(props: {
  mode: () => InputMode;
  narrow: boolean;
  connected: boolean;
  onSubmit: (value: string) => void;
  onRef: (input: InputRenderable) => void;
  onFocusInput: () => void;
  onEnterNavigation: () => void;
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

  return (
    <box
      height={3}
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

      <box flexDirection="row" gap={1} height={1} alignItems="center">
        <Show
          when={!props.narrow}
          fallback={
            <text fg={theme.faint} selectable={false} wrapMode="none">
              F1 help · F2 tools · F3 focus · F4 logs · F5 theme
            </text>
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
        <text selectable={false} wrapMode="none">
          <Tint color={props.connected ? theme.accent : theme.danger}>
            {props.connected
              ? props.narrow
                ? "ONLINE"
                : "● backend connected"
              : props.narrow
                ? "OFFLINE"
                : "○ backend offline"}
          </Tint>
        </text>
      </box>
    </box>
  );
}
