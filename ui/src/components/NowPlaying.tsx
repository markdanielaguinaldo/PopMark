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
  dense?: boolean;
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
  const dense = () => props.dense === true;
  // Below ~44 columns the five transport buttons plus the volume slider do not
  // fit on one row; stack them so nothing is clipped on a phone.
  const stackedTransport = () => dense() && props.columns < 44;
  const message = () => singleLine(props.message, 180);
  const duration = () => safeDuration(props.duration) ?? safeDuration(props.current?.durationSeconds);
  const remaining = () => {
    const total = duration();
    return total === undefined ? undefined : Math.max(0, total - props.elapsed);
  };
  const statusTone = () => {
    if (props.status === "Playing") return "accent" as const;
    if (props.status === "Paused") return "highlight" as const;
    if (props.status === "Loading") return "info" as const;
    return "muted" as const;
  };
  const volumeBarWidth = () =>
    stackedTransport()
      ? Math.max(6, Math.min(16, props.columns - 22))
      : dense()
        ? Math.max(8, Math.min(20, props.columns - 12))
        : Math.max(8, Math.min(24, props.columns - 66));

  return (
    <Panel
      title="NOW PLAYING"
      flexGrow={0}
      flexBasis="auto"
      flexShrink={0}
      minHeight={dense() ? 6 : props.tiny ? 5 : props.compact ? 7 : 9}
      padding={dense() ? 0 : 1}
      paddingLeft={dense() ? 1 : undefined}
      paddingRight={dense() ? 1 : undefined}
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
          <Show when={!props.compact && !dense()} fallback={<box height={0} />}>
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
          <Show when={!dense()} fallback={<box height={0} />}>
            <Equalizer active={playing} bars={10} color={playing() ? theme.accent : theme.faint} />
          </Show>
        </box>
      </box>

      <Show
        when={props.current && !props.tiny && !dense()}
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
        id="transport-row"
        flexDirection="column"
        gap={0}
        flexShrink={0}
      >
        <box flexDirection="row" gap={1} height={1} alignItems="center" flexShrink={0}>
          <ActionButton
            label={dense() ? "PREV" : "◀ PREV"}
            onPress={props.onPrevious}
            tone="accent"
            variant="ghost"
          />
          <ActionButton
            label={
              playing()
                ? dense()
                  ? "PAUSE"
                  : "❚❚ PAUSE"
                : dense()
                  ? "PLAY"
                  : "▶ PLAY"
            }
            onPress={props.onToggle}
            active
          />
          <ActionButton
            label={dense() ? "NEXT" : "NEXT ▶"}
            onPress={props.onNext}
            tone="accent"
            variant="ghost"
          />
          <Show when={!stackedTransport()} fallback={<box height={0} />}>
            <ActionButton
              label={dense() ? "STOP" : "■ STOP"}
              onPress={props.onStop}
              tone="danger"
              variant="ghost"
            />
            <ActionButton
              label={dense() ? "SHUF" : "⇄ SHUFFLE"}
              onPress={props.onShuffle}
              tone="highlight"
              variant="ghost"
            />
          </Show>

          <Show when={!dense()} fallback={<box height={0} />}>
            <box flexGrow={1} />
            <Show when={!props.compact} fallback={<box height={0} />}>
              <box flexDirection="row" gap={1} alignItems="center" height={1} flexShrink={0}>
                <text fg={theme.dim} selectable={false} wrapMode="none" width={3}>
                  VOL
                </text>
                <SliderBar
                  id="volume-bar"
                  width={volumeBarWidth()}
                  ratio={props.volume / MAX_VOLUME}
                  onSelect={(ratio, final) =>
                    props.onVolume(Math.round(ratio * MAX_VOLUME), final)
                  }
                  filledColor={theme.highlight}
                  trackColor={theme.surfaceHover}
                  thumbColor={theme.highlight}
                />
                <text
                  fg={theme.highlight}
                  selectable={false}
                  wrapMode="none"
                  width={5}
                  textAlign="right"
                >
                  {`${props.volume}%`}
                </text>
              </box>
            </Show>
          </Show>
        </box>

        <Show when={stackedTransport()} fallback={<box height={0} />}>
          <box flexDirection="row" gap={1} height={1} alignItems="center" flexShrink={0}>
            <ActionButton label="STOP" onPress={props.onStop} tone="danger" variant="ghost" />
            <ActionButton label="SHUF" onPress={props.onShuffle} tone="highlight" variant="ghost" />
            <SliderBar
              id="volume-bar"
              width={volumeBarWidth()}
              ratio={props.volume / MAX_VOLUME}
              onSelect={(ratio, final) => props.onVolume(Math.round(ratio * MAX_VOLUME), final)}
              filledColor={theme.highlight}
              trackColor={theme.surfaceHover}
              thumbColor={theme.highlight}
            />
            <text
              fg={theme.highlight}
              selectable={false}
              wrapMode="none"
              width={5}
              textAlign="right"
            >
              {`${props.volume}%`}
            </text>
          </box>
        </Show>

        <Show when={dense() && !stackedTransport()} fallback={<box height={0} />}>
          <box flexDirection="row" gap={1} height={1} alignItems="center" flexShrink={0}>
            <text fg={theme.dim} selectable={false} wrapMode="none" width={3}>
              VOL
            </text>
            <SliderBar
              id="volume-bar"
              width={volumeBarWidth()}
              ratio={props.volume / MAX_VOLUME}
              onSelect={(ratio, final) => props.onVolume(Math.round(ratio * MAX_VOLUME), final)}
              filledColor={theme.highlight}
              trackColor={theme.surfaceHover}
              thumbColor={theme.highlight}
            />
            <text fg={theme.highlight} selectable={false} wrapMode="none" width={5} textAlign="right">
              {`${props.volume}%`}
            </text>
          </box>
        </Show>
      </box>

      <Show when={!props.tiny && !dense()} fallback={<box height={0} />}>
        <box height={1} flexShrink={0} />
      </Show>

      <Show when={props.compact && !props.tiny && !dense()} fallback={<box height={0} />}>
        <box flexDirection="row" gap={1} alignItems="center" height={1} flexShrink={0} minWidth={0}>
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
