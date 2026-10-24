import { TextAttributes } from "@opentui/core";
import { Show } from "solid-js";
import type { Track } from "../bridge/protocol";
import { formatTime, singleLine } from "../lib/format";
import { positionRatio, safeDuration } from "../lib/playback";
import { theme } from "../theme";
import { ActionButton, Equalizer, Marquee, Panel, SliderBar, Spinner, Tint } from "./Widgets";

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

  return (
    <Panel
      title="NOW PLAYING"
      flexGrow={0}
      flexBasis="auto"
      flexShrink={0}
      minHeight={props.tiny ? 7 : props.compact ? 8 : 10}
      gap={0}
    >
      <box flexDirection="row" alignItems="flex-start" gap={1} flexShrink={0}>
        <box flexDirection="column" flexGrow={1} minWidth={0}>
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
              <>
                <Marquee
                  text={current.title}
                  columns={Math.max(16, props.columns - 6)}
                  active={playing()}
                />
                <Show when={!props.tiny} fallback={<box height={0} />}>
                  <text selectable={false} wrapMode="none" truncate>
                    <Tint color={theme.faint}>◈ </Tint>
                    <Tint color={theme.muted}>{current.displaySource}</Tint>
                  </text>
                </Show>
              </>
            )}
          </Show>
        </box>
        <box flexShrink={0} flexDirection="column" alignItems="flex-end" gap={0}>
          <Equalizer active={playing} bars={10} color={playing() ? theme.accent : theme.faint} />
        </box>
      </box>

      <Show when={!props.compact} fallback={<box height={0} />}>
        <box height={1} flexShrink={0} />
      </Show>

      <box flexDirection="row" gap={1} alignItems="center" height={1} flexShrink={0}>
        <text fg={theme.textSoft} selectable={false} wrapMode="none" width={7}>
          {formatTime(props.elapsed)}
        </text>
        <SliderBar
          id="progress-bar"
          width={Math.max(10, props.columns - 22)}
          ratio={positionRatio(props.elapsed, duration())}
          thumb={false}
          onSelect={(ratio, final) => {
            if (duration()) props.onSeek(ratio, final);
          }}
          filledColor={playing() ? theme.accent : theme.borderStrong}
          trackColor={theme.surfaceHover}
        />
        <text fg={theme.muted} selectable={false} wrapMode="none" width={7} textAlign="right">
          {formatTime(duration())}
        </text>
      </box>

      <Show when={!props.tiny} fallback={<box height={0} />}>
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
        <ActionButton label="◀ PREV" onPress={props.onPrevious} grow={1} tone="accent" variant="ghost" />
        <ActionButton
          label={playing() ? "❚❚ PAUSE" : "▶ PLAY"}
          onPress={props.onToggle}
          grow={2}
          active
        />
        <ActionButton label="NEXT ▶" onPress={props.onNext} grow={1} tone="accent" variant="ghost" />
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
            onPress={props.onShuffle}
            grow={1}
            tone="highlight"
            variant="ghost"
          />
        </Show>
      </box>

      <Show when={!props.compact} fallback={<box height={0} />}>
        <box flexDirection="row" gap={1} alignItems="center" height={1} flexShrink={0}>
          <text fg={theme.dim} selectable={false} wrapMode="none" width={8}>
            {`VOL ${props.volume}%`}
          </text>
          <SliderBar
            id="volume-bar"
            width={Math.max(10, props.columns - 16)}
            ratio={props.volume / MAX_VOLUME}
            onSelect={(ratio, final) =>
              props.onVolume(Math.round(ratio * MAX_VOLUME), final)
            }
            filledColor={theme.highlight}
            trackColor={theme.surfaceHover}
            thumbColor={theme.highlight}
          />
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
                attributes={TextAttributes.DIM}
                selectable={false}
                wrapMode="none"
                truncate
                flexGrow={1}
                minWidth={0}
              >
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
