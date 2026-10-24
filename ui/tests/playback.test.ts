import { describe, expect, test } from "bun:test";
import { liveElapsedSeconds, positionRatio, safeDuration } from "../src/lib/playback";

describe("positionRatio", () => {
  test("maps position onto the 0..1 progress range", () => {
    expect(positionRatio(0, 232)).toBe(0);
    expect(positionRatio(116, 232)).toBe(0.5);
    expect(positionRatio(232, 232)).toBe(1);
  });

  test("clamps negative and out-of-range positions", () => {
    expect(positionRatio(-20, 232)).toBe(0);
    expect(positionRatio(300, 232)).toBe(1);
    expect(positionRatio(Number.NaN, 232)).toBe(0);
  });

  test("never reports full progress without a usable duration", () => {
    expect(positionRatio(50, undefined)).toBe(0);
    expect(positionRatio(50, 0)).toBe(0);
    expect(positionRatio(50, Number.NaN)).toBe(0);
    expect(positionRatio(50, Number.POSITIVE_INFINITY)).toBe(0);
  });
});

describe("liveElapsedSeconds", () => {
  test("advances only while playing", () => {
    expect(liveElapsedSeconds(10, "Playing", 1_000, 3_500, 232)).toBe(12.5);
    expect(liveElapsedSeconds(10, "Paused", 1_000, 3_500, 232)).toBe(10);
    expect(liveElapsedSeconds(10, "Stopped", 1_000, 3_500, 232)).toBe(10);
  });

  test("never runs past the known duration", () => {
    expect(liveElapsedSeconds(19, "Playing", 1_000, 6_000, 20)).toBe(20);
  });

  test("handles invalid timestamps, values, and durations", () => {
    expect(liveElapsedSeconds(Number.NaN, "Paused", 1_000, 2_000, 232)).toBe(0);
    expect(liveElapsedSeconds(-4, "Paused", 1_000, 2_000, 232)).toBe(0);
    expect(liveElapsedSeconds(10, "Playing", Number.NaN, 2_000, 232)).toBe(10);
    expect(liveElapsedSeconds(10, "Playing", 5_000, 1_000, 232)).toBe(10);
    expect(liveElapsedSeconds(10, "Playing", 1_000, 2_000, Number.NaN)).toBe(11);
    expect(liveElapsedSeconds(10, "Playing", 1_000, 2_000, 0)).toBe(11);
  });

  test("tracks seek and resume baselines", () => {
    const receivedAt = 10_000;
    expect(liveElapsedSeconds(100, "Paused", receivedAt, receivedAt + 2_000, 232)).toBe(100);
    expect(liveElapsedSeconds(100, "Playing", receivedAt, receivedAt + 500, 232)).toBe(100.5);
  });
});

describe("safeDuration", () => {
  test("accepts only positive finite durations", () => {
    expect(safeDuration(232)).toBe(232);
    expect(safeDuration(0)).toBeUndefined();
    expect(safeDuration(-1)).toBeUndefined();
    expect(safeDuration(Number.NaN)).toBeUndefined();
    expect(safeDuration(undefined)).toBeUndefined();
  });
});
