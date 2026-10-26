import { TextAttributes } from "@opentui/core";
import { Show } from "solid-js";
import type { Track } from "../bridge/protocol";
import { formatTime, singleLine } from "../lib/format";
import { positionRatio, safeDuration } from "../lib/playback";
import { theme } from "../theme";
import {
  ActionButton,
  Chip,
  Equalizer,
  Marquee,
  Panel,
  SliderBar,
  Spinner,
  Tint,
} from "./Widgets";

const MAX_VOLUME = 130;

function messageColor(message: string, busy: boolean): string {
  if (busy) return theme.info;
  const lower = message.toLowerCase();
  if (lower.includes("failed") || lower.includes("missing") || lower.includes("unknown")) {
    return theme.danger;
  }
  if (lower.includes("restored") || lower.includes("added") || lower.includes("now playing")) {
    return theme.accent;
  }
  return theme.muted;
}

export function NowPlaying(props: {
  current?: Track;
  status: string;
  elapsed: number;
  duration?: number;
  volume: number;
  message: string;
  busy: boolean;
  busyLabel?: string;
  compact?: boolean;
  tiny?: boolean;
  columns: number;
  onToggle: () => void;
  onNext: () => void;
  onPrevious: () => void;
  onStop: () => void;
  onShuffle: () => void;
  onSeek: (ratio: number, final?: boolean) => void;
  onVolume: (percent: number, final?: boolean) => void;
}) {
  const playing = () => props.status === "Playing";
  const message = () => singleLine(props.message, 180);
  const duration = () => safeDuration(props.duration) ?? safeDuration(props.current?.durationSeconds);
  const remaining = () => {
    const total = duration();
    return total === undefined ? undefined : Math.max(0, total - props.elapsed);
  };
  const wide = () => !props.compact && props.columns >= 110;
  const statusTone = () => {
    if (props.status === "Playing") return "accent" as const;
    if (props.status === "Paused") return "highlight" as const;
    if (props.status === "Loading") return "info" as const;
    return "muted" as const;
  };

  return (
    <Panel
      title="NOW PLAYING"
      flexGrow={0}
      flexBasis="auto"
      flexShrink={0}
      minHeight={props.tiny ? 6 : props.compact ? 8 : 10}
      gap={0}
    >
      <box flexDirection="row" alignItems="flex-start" gap={1} flexShrink={0}>
        <box flexDirection="row" alignItems="center" gap={1} flexGrow={1} minWidth={0}>
          <text
            fg={playing() ? theme.accent : theme.faint}
            attributes={TextAttributes.BOLD}
            selectable={false}
            wrapMode="none"
          >
            {playing() ? "▶" : "❚❚"}
          </text>
          <Show
            when={props.current}
            keyed
            fallback={
              <text selectable={false} wrapMode="none">
                <Tint color={theme.dim}>Nothing queued yet.</Tint>
                <Tint color={theme.muted}> Press / and paste a YouTube URL.</Tint>
              </text>
            }
          >
            {(current: Track) => (
              <Marquee
                text={current.title}
                columns={Math.max(16, props.columns - 18)}
                active={playing()}
              />
            )}
          </Show>
        </box>
        <box flexShrink={0} flexDirection="row" alignItems="center" gap={1}>
          <Show when={!props.compact} fallback={<box height={0} />}>
            <Chip
              label={
                playing()
                  ? "LIVE"
                  : props.status === "Paused"
                    ? "HOLD"
                    : props.status.toUpperCase()
              }
              tone={statusTone()}
              filled={playing()}
            />
          </Show>
          <Equalizer active={playing} bars={10} color={playing() ? theme.accent : theme.faint} />
        </box>
      </box>

      <Show
        when={props.current && !props.tiny}
        fallback={<box height={0} />}
      >
        <text selectable={false} wrapMode="none" truncate>
          <Tint color={theme.faint}>FROM  </Tint>
          <Tint color={theme.muted}>{props.current?.displaySource}</Tint>
        </text>
      </Show>

      <Show when={!props.compact} fallback={<box height={0} />}>
        <box height={1} flexShrink={0} />
      </Show>

      <box flexDirection="row" gap={1} alignItems="center" height={1} flexShrink={0}>
        <text
          fg={playing() ? theme.accent : theme.textSoft}
          attributes={TextAttributes.BOLD}
          selectable={false}
          wrapMode="none"
          width={7}
        >
          {formatTime(props.elapsed)}
        </text>
        <SliderBar
          id="progress-bar"
          width={Math.max(10, props.columns - (props.compact ? 18 : 30))}
          ratio={positionRatio(props.elapsed, duration())}
          onSelect={(ratio, final) => {
            if (duration()) props.onSeek(ratio, final);
          }}
          filledColor={playing() ? theme.accent : theme.borderStrong}
          trackColor={theme.surfaceHover}
        />
        <text fg={theme.muted} selectable={false} wrapMode="none" width={7} textAlign="right">
          {formatTime(duration())}
        </text>
        <Show when={!props.compact} fallback={<box height={0} />}>
          <text fg={theme.faint} selectable={false} wrapMode="none" width={7} textAlign="right">
            {remaining() === undefined ? "" : `-${formatTime(remaining())}`}
          </text>
        </Show>
      </box>

      <Show when={!props.compact} fallback={<box height={0} />}>
        <box height={1} flexShrink={0} />
      </Show>

      <box
        flexDirection="row"
        gap={1}
        height={1}
        flexShrink={0}
        justifyContent="space-between"
        alignItems="center"
      >
        <ActionButton
          label="◀ PREV"
          hint={wide() ? "ctrl+b" : undefined}
          onPress={props.onPrevious}
          grow={1}
          tone="accent"
          variant="ghost"
        />
        <ActionButton
          label={playing() ? "❚❚ PAUSE" : "▶ PLAY"}
          hint={wide() ? "space" : undefined}
          onPress={props.onToggle}
          grow={2}
          active
        />
        <ActionButton
          label="NEXT ▶"
          hint={wide() ? "ctrl+n" : undefined}
          onPress={props.onNext}
          grow={1}
          tone="accent"
          variant="ghost"
        />
        <ActionButton
          label="■ STOP"
          onPress={props.onStop}
          grow={1}
          tone="danger"
          variant="ghost"
        />
        <Show when={!props.compact} fallback={<box height={0} />}>
          <ActionButton
            label="SHUFFLE"
            hint={wide() ? "ctrl+s" : undefined}
            onPress={props.onShuffle}
            grow={1}
            tone="highlight"
            variant="ghost"
          />
        </Show>
      </box>

      <Show when={!props.compact} fallback={<box height={0} />}>
        <box flexDirection="row" gap={1} alignItems="center" height={1} flexShrink={0}>
          <text fg={theme.dim} selectable={false} wrapMode="none" width={4}>
            VOL
          </text>
          <SliderBar
            id="volume-bar"
            width={Math.max(10, props.columns - 14)}
            ratio={props.volume / MAX_VOLUME}
            onSelect={(ratio, final) =>
              props.onVolume(Math.round(ratio * MAX_VOLUME), final)
            }
            filledColor={theme.highlight}
            trackColor={theme.surfaceHover}
            thumbColor={theme.highlight}
          />
          <text fg={theme.highlight} selectable={false} wrapMode="none" width={5} textAlign="right">
            {`${props.volume}%`}
          </text>
        </box>
      </Show>

      <Show when={props.compact && !props.tiny} fallback={<box height={0} />}>
        <box flexDirection="row" gap={1} alignItems="center" height={1} flexShrink={0} minWidth={0}>
          <ActionButton label="SHUFFLE" onPress={props.onShuffle} tone="highlight" />
          <text
            fg={messageColor(message(), props.busy)}
            attributes={TextAttributes.DIM}
            selectable={false}
            wrapMode="none"
            truncate
            flexGrow={1}
            minWidth={0}
          >
            {props.busy ? singleLine(props.busyLabel ?? "Working...", 80) : message()}
          </text>
        </box>
      </Show>

      <Show when={!props.compact} fallback={<box height={0} />}>
        <box flexDirection="row" gap={1} alignItems="center" height={1} flexShrink={0} minWidth={0}>
          <Show
            when={props.busy}
            fallback={
              <text
                fg={messageColor(message(), false)}
                selectable={false}
                wrapMode="none"
                truncate
                flexGrow={1}
                minWidth={0}
              >
                <Tint color={messageColor(message(), false)} bold>› </Tint>
                {message()}
              </text>
            }
          >
            <Spinner active={() => props.busy} label={props.busyLabel ?? "Working..."} />
          </Show>
        </box>
      </Show>
    </Panel>
  );
}
