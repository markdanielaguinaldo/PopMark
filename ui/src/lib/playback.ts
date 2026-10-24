import type { PlaybackStatus } from "../bridge/protocol";
import { clamp } from "./format";

/**
 * Projects the last authoritative position forward between backend updates.
 * Both timestamps must come from the same clock: mixing performance.now() with
 * Date.now() makes the position look hours ahead and pins the bar at 100%.
 */
export function liveElapsedSeconds(
  baseSeconds: number,
  status: PlaybackStatus,
  receivedAtMs: number,
  nowMs: number,
  durationSeconds?: number,
): number {
  const safeBase =
    Number.isFinite(baseSeconds) && baseSeconds > 0 ? baseSeconds : 0;

  let value = safeBase;
  if (status === "Playing") {
    const driftMs =
      Number.isFinite(receivedAtMs) && Number.isFinite(nowMs)
        ? Math.max(0, nowMs - receivedAtMs)
        : 0;
    value += driftMs / 1000;
  }

  if (!Number.isFinite(value) || value < 0) value = 0;

  if (
    durationSeconds !== undefined &&
    Number.isFinite(durationSeconds) &&
    durationSeconds > 0
  ) {
    value = Math.min(value, durationSeconds);
  }

  return value;
}

/** Rounded for display without letting NaN/Infinity leak into the UI. */
export function safeDuration(durationSeconds?: number): number | undefined {
  return durationSeconds !== undefined &&
    Number.isFinite(durationSeconds) &&
    durationSeconds > 0
    ? durationSeconds
    : undefined;
}

export function positionRatio(elapsedSeconds: number, durationSeconds?: number): number {
  const duration = safeDuration(durationSeconds);
  if (duration === undefined) return 0;
  return clamp(elapsedSeconds / duration, 0, 1);
}
