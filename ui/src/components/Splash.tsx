import { TextAttributes } from "@opentui/core";
import { Show, createSignal, onCleanup } from "solid-js";
import type { Track } from "../bridge/protocol";
import { formatTime, singleLine } from "../lib/format";
import { positionRatio, safeDuration } from "../lib/playback";
import { brandGradient, theme, themeLabel } from "../theme";
import { Equalizer, SliderBar, Tint } from "./Widgets";

const WAVE = ["▁", "▂", "▃", "▄", "▅", "▆", "▇", "█"];

export function Splash(props: {
  current?: Track;
  status: string;
  elapsed: number;
  duration?: number;
  volume: number;
  message: string;
  toolsReady: number;
  toolsTotal: number;
  width: number;
  onSeek: (ratio: number, final?: boolean) => void;
}) {
  const [tick, setTick] = createSignal(0);
  const playing = () => props.status === "Playing";

  const timer = setInterval(() => {
    if (playing()) setTick((value) => value + 1);
  }, 140);

  onCleanup(() => clearInterval(timer));

  const wave = () => {
    const columns = Math.max(12, Math.min(120, props.width - 12));
    const offset = tick();
    return Array.from({ length: columns }, (_, index) => {
      const phase = index * 0.32 + offset * 0.45;
      const envelope = Math.sin((index / columns) * Math.PI);
      const value = playing() ? (Math.sin(phase) + 1) / 2 * (0.3 + envelope * 0.7) : 0.12;
      return WAVE[Math.max(0, Math.min(WAVE.length - 1, Math.floor(value * WAVE.length)))];
    }).join("");
  };

  const font = () => (props.width >= 92 ? "block" : props.width >= 58 ? "tiny" : "tiny");

  return (
    <box
      position="absolute"
      top={0}
      left={0}
      width="100%"
      height="100%"
      zIndex={40}
      backgroundColor={theme.bg}
      flexDirection="column"
      alignItems="center"
      justifyContent="center"
      gap={1}
    >
      <ascii_font text="POPMARK" font={font()} color={[...brandGradient()]} selectable={false} />

      <text selectable={false} wrapMode="none">
        <Tint color={theme.dim}>A TERMINAL JUKEBOX</Tint>
        <Tint color={theme.faint}>   /   </Tint>
        <Tint color={theme.muted}>C# PLAYBACK</Tint>
        <Tint color={theme.faint}> + </Tint>
        <Tint color={theme.muted}>OPENTUI</Tint>
      </text>

      <box height={1} />

      <text fg={theme.faint} selectable={false} wrapMode="none">
        {wave()}
      </text>

      <box height={1} />

      <Show
        when={props.current}
        keyed
        fallback={
          <box flexDirection="column" alignItems="center" gap={0}>
            <text fg={theme.dim} selectable={false}>
              Nothing is playing.
            </text>
            <text fg={theme.faint} selectable={false}>
              Press F3 to return and add a URL.
            </text>
          </box>
        }
      >
        {(current: Track) => (
          <box flexDirection="column" alignItems="center" gap={1} width="70%" minWidth={40}>
            <box flexDirection="row" gap={1} alignItems="center">
              <Equalizer active={playing} bars={5} />
              <text
                fg={theme.text}
                attributes={TextAttributes.BOLD}
                selectable={false}
                wrapMode="none"
                truncate
              >
                {current.title}
              </text>
              <Equalizer active={playing} bars={5} phase={1.6} reverse />
            </box>
            <text selectable={false} wrapMode="none">
              <Tint color={theme.muted}>{current.displaySource}</Tint>
              <Tint color={theme.faint}>  ·  </Tint>
              <Tint color={theme.dim}>{formatTime(props.elapsed)}</Tint>
              <Tint color={theme.faint}> / </Tint>
              <Tint color={theme.dim}>{formatTime(safeDuration(props.duration) ?? safeDuration(current.durationSeconds))}</Tint>
            </text>
            <box flexDirection="row" width="100%" gap={1} alignItems="center">
              <text fg={theme.faint} selectable={false} wrapMode="none" width={6}>
                {formatTime(props.elapsed)}
              </text>
              <SliderBar
                id="focus-progress"
                width={Math.max(12, Math.round(props.width * 0.7) - 16)}
                ratio={positionRatio(
                  props.elapsed,
                  safeDuration(props.duration) ?? safeDuration(current.durationSeconds),
                )}
                onSelect={(ratio, final) => props.onSeek(ratio, final)}
                filledColor={theme.accent}
                trackColor={theme.faint}
                thumb={false}
              />
              <text fg={theme.faint} selectable={false} wrapMode="none" width={6} textAlign="right">
                {formatTime(safeDuration(props.duration) ?? safeDuration(current.durationSeconds))}
              </text>
            </box>
          </box>
        )}
      </Show>

      <box height={1} />

      <box flexDirection="row" gap={1} alignItems="center">
        <text selectable={false} wrapMode="none">
          <Tint color={theme.accent} bold>
            F3
          </Tint>
          <Tint color={theme.dim}> return · </Tint>
          <Tint color={theme.accent} bold>
            space
          </Tint>
          <Tint color={theme.dim}> play/pause · </Tint>
          <Tint color={theme.accent} bold>
            ←/→
          </Tint>
          <Tint color={theme.dim}> seek</Tint>
        </text>
      </box>

      <text fg={theme.faint} selectable={false} wrapMode="none">
        {singleLine(props.message, 160)}
      </text>

      <text selectable={false} wrapMode="none">
        <Tint
          color={
            props.toolsTotal > 0 && props.toolsReady === props.toolsTotal
              ? theme.accent
              : theme.warning
          }
        >
          ●
        </Tint>
        <Tint color={theme.dim}> tools {props.toolsReady}/{props.toolsTotal}</Tint>
        <Tint color={theme.faint}> · volume </Tint>
        <Tint color={theme.dim}>{props.volume}%</Tint>
        <Tint color={theme.faint}> · </Tint>
        <Tint color={theme.dim}>v{props.status}</Tint>
        <Tint color={theme.faint}> · </Tint>
        <Tint color={theme.muted}>{themeLabel()}</Tint>
      </text>
    </box>
  );
}
