import { TextAttributes } from "@opentui/core";
import { Show, createSignal, onCleanup } from "solid-js";
import type { Track } from "../bridge/protocol";
import { formatTime, singleLine } from "../lib/format";
import { positionRatio, safeDuration } from "../lib/playback";
import { brandGradient, theme, themeLabel } from "../theme";
import { Equalizer, SliderBar, Tint, ActionButton } from "./Widgets";
import { Starfield } from "./Starfield";

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
  height: number;
  onSeek: (ratio: number, final?: boolean) => void;
  onClose: () => void;
  onTogglePlayback: () => void;
  onCycleTheme: (direction: 1 | -1) => void;
}) {
  const [tick, setTick] = createSignal(0);
  const playing = () => props.status === "Playing";
  // A phone in portrait is often only ~24 rows tall; drop decorative rows rather
  // than letting the track controls be clipped off the bottom.
  const tight = () => props.height < 26;
  const cramped = () => props.height < 20;

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
      justifyContent={tight() ? "flex-start" : "center"}
      paddingTop={tight() ? 1 : 0}
      gap={tight() ? 0 : 1}
    >
      <Starfield width={props.width} height={props.height} />

      <ascii_font text="POPMARK" font={font()} color={[...brandGradient()]} selectable={false} />

      <Show when={!tight()} fallback={<box height={0} />}>
        <text selectable={false} wrapMode="none">
          <Tint color={theme.dim}>A TERMINAL JUKEBOX</Tint>
          <Tint color={theme.faint}>   /   </Tint>
          <Tint color={theme.muted}>C# PLAYBACK</Tint>
          <Tint color={theme.faint}> + </Tint>
          <Tint color={theme.muted}>OPENTUI</Tint>
        </text>
      </Show>

      <Show when={!tight()} fallback={<box height={0} />}>
        <box height={1} />

        <text fg={theme.faint} selectable={false} wrapMode="none">
          {wave()}
        </text>

        <box height={1} />
      </Show>

      <Show
        when={props.current}
        keyed
        fallback={
          <box flexDirection="column" alignItems="center" gap={0}>
            <text fg={theme.dim} selectable={false}>
              Nothing is playing.
            </text>
            <text fg={theme.faint} selectable={false}>
              Press F3 or tap RETURN to add a URL.
            </text>
          </box>
        }
      >
        {(current: Track) => (
          <box
            flexDirection="column"
            alignItems="center"
            gap={1}
            flexShrink={0}
            width={props.width < 60 ? "92%" : "70%"}
            minWidth={Math.max(20, Math.min(40, props.width - 6))}
          >
            <box flexDirection="row" gap={1} alignItems="center">
              <Show when={props.width >= 60} fallback={<box height={0} />}>
                <Equalizer active={playing} bars={5} id="focus-eq-left" />
              </Show>
              <text
                fg={theme.text}
                attributes={TextAttributes.BOLD}
                selectable={false}
                wrapMode="none"
                truncate
              >
                {current.title}
              </text>
              <Show when={props.width >= 60} fallback={<box height={0} />}>
                <Equalizer active={playing} bars={5} id="focus-eq-right" />
              </Show>
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

      <box flexDirection="column" gap={0} alignItems="center" flexShrink={0}>
        {/* Tap targets: phones rarely have F-keys, and the termux keyboard hides
            the input row. Every focus-view action is reachable here. */}
        <box flexDirection="row" gap={1} alignItems="center">
          <ActionButton
            id="focus-return"
            label="RETURN"
            onPress={props.onClose}
            tone="accent"
            variant="ghost"
            height={props.width < 60 ? 3 : 1}
          />
          <ActionButton
            id="focus-play"
            label={playing() ? "PAUSE" : "PLAY"}
            onPress={props.onTogglePlayback}
            tone="muted"
            variant="ghost"
            height={props.width < 60 ? 3 : 1}
          />
          <ActionButton
            id="focus-theme"
            label="THEME"
            onPress={() => props.onCycleTheme(1)}
            tone="highlight"
            variant="ghost"
            height={props.width < 60 ? 3 : 1}
          />
          <ActionButton
            id="focus-theme-back"
            label="◀"
            onPress={() => props.onCycleTheme(-1)}
            tone="muted"
            variant="ghost"
            height={props.width < 60 ? 3 : 1}
          />
        </box>
        <Show when={props.width >= 80} fallback={<box height={0} />}>
          <text fg={theme.faint} selectable={false} wrapMode="none">
            F3 return · space play/pause · ←/→ seek · F5 theme
          </text>
        </Show>
      </box>

      <Show when={!cramped()} fallback={<box height={0} />}>
        <text fg={theme.faint} selectable={false} wrapMode="none">
          {singleLine(props.message, 160)}
        </text>
      </Show>

      <Show when={!cramped()} fallback={<box height={0} />}>
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
      </Show>
    </box>
  );
}
