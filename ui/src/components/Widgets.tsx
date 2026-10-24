import { TextAttributes } from "@opentui/core";
import type { BoxProps, JSX } from "@opentui/solid";
import { Show, createSignal, onCleanup, type Accessor } from "solid-js";
import { singleLine } from "../lib/format";
import { theme } from "../theme";

export function Panel(
  props: Omit<BoxProps, "title" | "children"> & {
    title?: string;
    subtitle?: string;
    children?: JSX.Element;
  },
) {
  const { title, subtitle, children, ...box } = props;

  return (
    <box
      flexDirection="column"
      backgroundColor={theme.surface}
      border
      borderStyle="rounded"
      borderColor={theme.border}
      title={title ? ` ${title} ` : undefined}
      titleColor={theme.muted}
      padding={1}
      gap={0}
      {...box}
    >
      <Show when={subtitle} fallback={<box height={0} />}>
        <text fg={theme.dim} attributes={TextAttributes.DIM} selectable={false}>
          {subtitle}
        </text>
      </Show>
      {children}
    </box>
  );
}

export function ActionButton(props: {
  label: string;
  onPress: () => void;
  tone?: "accent" | "muted" | "danger" | "highlight";
  active?: boolean;
  disabled?: boolean;
  grow?: number;
  variant?: "solid" | "ghost";
}) {
  const [hovered, setHovered] = createSignal(false);
  const tone = () => props.tone ?? "muted";

  const background = () => {
    if (props.disabled) return theme.surface;
    if (props.active) return theme.accent;
    if (hovered()) return theme.surfaceHover;
    return props.variant === "ghost" ? theme.surface : theme.surfaceRaised;
  };

  const foreground = () => {
    if (props.disabled) return theme.faint;
    if (props.active) return theme.accentInk;
    if (tone() === "accent") return theme.accent;
    if (tone() === "highlight") return theme.highlight;
    if (tone() === "danger") return theme.danger;
    return theme.textSoft;
  };

  return (
    <box
      height={1}
      flexGrow={props.grow ?? 0}
      flexShrink={0}
      paddingLeft={1}
      paddingRight={1}
      alignItems="center"
      justifyContent="center"
      backgroundColor={background()}
      onMouseOver={() => setHovered(true)}
      onMouseOut={() => setHovered(false)}
      onMouseDown={(event) => {
        event.stopPropagation();
        if (!props.disabled) props.onPress();
      }}
    >
      <text fg={foreground()} selectable={false} wrapMode="none" truncate>
        {props.label}
      </text>
    </box>
  );
}

export function StatusChip(props: { status: string }) {
  const tone = () => {
    switch (props.status) {
      case "Playing":
        return { bg: theme.accent, fg: theme.accentInk, label: "PLAYING" };
      case "Paused":
        return { bg: "#4a3c14", fg: theme.warning, label: "PAUSED" };
      case "Loading":
        return { bg: "#143352", fg: theme.info, label: "LOADING" };
      case "Detached":
        return { bg: "#4a1d38", fg: theme.highlight, label: "DETACHED" };
      default:
        return { bg: theme.surfaceHover, fg: theme.muted, label: "STOPPED" };
    }
  };

  return (
    <box backgroundColor={tone().bg} paddingLeft={1} paddingRight={1} height={1} flexShrink={0}>
      <text fg={tone().fg} attributes={TextAttributes.BOLD} selectable={false} wrapMode="none">
        {tone().label}
      </text>
    </box>
  );
}

const SPINNER = ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"];

export function Spinner(props: { active: Accessor<boolean>; label?: string }) {
  const [frame, setFrame] = createSignal(0);

  const timer = setInterval(() => {
    if (props.active()) setFrame((value) => (value + 1) % SPINNER.length);
  }, 90);

  onCleanup(() => clearInterval(timer));

  return (
    <Show when={props.active()} fallback={<box height={0} />}>
      <text fg={theme.info} selectable={false} wrapMode="none">
        {`${SPINNER[frame()]} ${singleLine(props.label ?? "Working...", 100)}`}
      </text>
    </Show>
  );
}

const BARS = ["▁", "▂", "▃", "▄", "▅", "▆", "▇", "█"];

export function Equalizer(props: { active: Accessor<boolean>; bars?: number; color?: string }) {
  const count = props.bars ?? 7;
  const [tick, setTick] = createSignal(0);

  const timer = setInterval(() => {
    if (props.active()) setTick((value) => value + 1);
  }, 110);

  onCleanup(() => clearInterval(timer));

  const glyphs = () => {
    const now = tick();
    return Array.from({ length: count }, (_, index) => {
      const phase = now * 0.55 + index * 0.9;
      const level = props.active() ? (Math.sin(phase) + 1) / 2 : 0.12;
      return BARS[Math.min(BARS.length - 1, Math.max(0, Math.round(level * (BARS.length - 1))))];
    }).join("");
  };

  return (
    <text fg={props.color ?? theme.accent} selectable={false} wrapMode="none">
      {glyphs()}
    </text>
  );
}

export function Marquee(props: { text: string; columns: number; active?: boolean }) {
  const [offset, setOffset] = createSignal(0);
  const separator = "   •   ";

  const timer = setInterval(() => {
    if (props.active === false) return;
    if (props.text.length <= props.columns) return;
    setOffset((value) => (value + 1) % Math.max(1, props.text.length + separator.length));
  }, 240);

  onCleanup(() => clearInterval(timer));

  const content = () => {
    if (props.text.length <= props.columns) return props.text;
    const loop = props.text + separator;
    const start = offset() % loop.length;
    const doubled = loop + loop;
    return doubled.slice(start, start + Math.max(8, props.columns));
  };

  return (
    <text fg={theme.text} attributes={TextAttributes.BOLD} wrapMode="none" selectable={false}>
      {content()}
    </text>
  );
}

export function Bar(props: {
  ratio: number;
  onSelect?: (ratio: number) => void;
  filledColor?: string;
  emptyColor?: string;
  height?: number;
  id?: string;
}) {
  const ratio = () => Math.min(1, Math.max(0, props.ratio));
  const percent = () => `${(ratio() * 100).toFixed(3)}%` as `${number}%`;

  return (
    <box
      id={props.id}
      flexDirection="row"
      height={props.height ?? 1}
      width="100%"
      backgroundColor={props.emptyColor ?? theme.faint}
      onMouseDown={(event) => {
        if (!props.onSelect) return;
        event.stopPropagation();
        const target = event.currentTarget;
        if (!target) return;
        const width = Math.max(1, target.width);
        const localX = Math.max(0, Math.min(width - 1, event.x - target.x));
        props.onSelect(width <= 1 ? 1 : localX / (width - 1));
      }}
    >
      <Show when={ratio() > 0} fallback={<box height={0} />}>
        <box width={percent()} backgroundColor={props.filledColor ?? theme.accent} />
      </Show>
    </box>
  );
}

/** One-cell-high line slider with an optional knob. */
export function SliderBar(props: {
  width: number;
  ratio: number;
  onSelect?: (ratio: number, final: boolean) => void;
  filledColor?: string;
  trackColor?: string;
  thumbColor?: string;
  thumb?: boolean;
  id?: string;
}) {
  const width = () => Math.max(2, Math.floor(props.width));
  const ratio = () => Math.min(1, Math.max(0, props.ratio));
  const filled = () => Math.round(ratio() * (width() - 1));
  const empty = () => Math.max(0, width() - 1 - filled());

  const select = (event: { currentTarget: { x: number; width: number } | null; x: number }, final: boolean) => {
    if (!props.onSelect) return;
    const target = event.currentTarget;
    if (!target) return;
    const localX = Math.max(0, Math.min(target.width - 1, event.x - target.x));
    props.onSelect(target.width <= 1 ? 1 : localX / (target.width - 1), final);
  };

  return (
    <box
      id={props.id}
      height={1}
      width={width()}
      flexShrink={0}
      onMouseDown={(event) => {
        event.preventDefault();
        event.stopPropagation();
        select(event, false);
      }}
      onMouseMove={(event) => {
        if (event.button !== 0) return;
        if (!event.isDragging) return;
        event.preventDefault();
        select(event, false);
      }}
      onMouseDrag={(event) => {
        event.preventDefault();
        select(event, false);
      }}
      onMouseUp={(event) => {
        event.preventDefault();
        select(event, true);
      }}
      onMouseDragEnd={(event) => {
        event.preventDefault();
        select(event, true);
      }}
    >
      <text selectable={false} wrapMode="none">
        <Tint color={props.filledColor ?? theme.accent}>
          {"━".repeat(filled())}
        </Tint>
        <Tint color={props.thumb === false ? (props.filledColor ?? theme.accent) : (props.thumbColor ?? theme.highlight)}>
          {props.thumb === false ? "━" : "●"}
        </Tint>
        <Tint color={props.trackColor ?? theme.faint}>{"─".repeat(empty())}</Tint>
      </text>
    </box>
  );
}

export function KeyHint(props: { shortcut: string; label: string; onPress?: () => void }) {
  return (
    <text
      selectable={false}
      wrapMode="none"
      onMouseDown={
        props.onPress
          ? (event) => {
              event.stopPropagation();
              props.onPress?.();
            }
          : undefined
      }
    >
      <Tint color={theme.accent} bold>{`[${props.shortcut}]`}</Tint>
      <Tint color={theme.dim}>{` ${props.label}`}</Tint>
    </text>
  );
}

export function SectionLabel(props: { children: JSX.Element }) {
  return (
    <text fg={theme.muted} attributes={TextAttributes.BOLD} selectable={false} wrapMode="none">
      {props.children}
    </text>
  );
}

/** Inline colored text. OpenTUI's text-node style path is the Solid-safe way to mix colors. */
export function Tint(props: { color: string; bold?: boolean; children: JSX.Element }) {
  return (
    <span
      style={{
        fg: props.color,
        attributes: props.bold ? TextAttributes.BOLD : 0,
      }}
    >
      {props.children}
    </span>
  );
}
