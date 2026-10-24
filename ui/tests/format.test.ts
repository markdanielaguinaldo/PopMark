import { describe, expect, test } from "bun:test";
import { formatDurationLong, formatTime, progressRatio, truncate, singleLine, stripTerminalControl } from "../src/lib/format";

describe("formatTime", () => {
  test("formats short durations as m:ss", () => {
    expect(formatTime(0)).toBe("0:00");
    expect(formatTime(65)).toBe("1:05");
    expect(formatTime(59.9)).toBe("0:59");
  });

  test("formats long durations with hours", () => {
    expect(formatTime(3600)).toBe("1:00:00");
    expect(formatTime(3725)).toBe("1:02:05");
  });

  test("handles missing durations", () => {
    expect(formatTime(undefined)).toBe("--:--");
    expect(formatTime(null)).toBe("--:--");
    expect(formatTime(Number.NaN)).toBe("--:--");
  });
});

describe("progressRatio", () => {
  test("clamps to a 0..1 range", () => {
    expect(progressRatio(30, 60)).toBe(0.5);
    expect(progressRatio(-5, 60)).toBe(0);
    expect(progressRatio(90, 60)).toBe(1);
  });

  test("returns zero without a duration", () => {
    expect(progressRatio(30, undefined)).toBe(0);
    expect(progressRatio(30, 0)).toBe(0);
  });
});

describe("formatDurationLong", () => {
  test("describes seconds, minutes, and hours", () => {
    expect(formatDurationLong(30)).toBe("30s");
    expect(formatDurationLong(150)).toBe("2m 30s");
    expect(formatDurationLong(3660)).toBe("1h 1m");
    expect(formatDurationLong(undefined)).toBe("unknown");
  });
});

describe("truncate", () => {
  test("keeps short text and shortens long text", () => {
    expect(truncate("PopMark", 10)).toBe("PopMark");
    expect(truncate("PopMark Player", 10)).toBe("PopMark...");
    expect(truncate("PopMark", 0)).toBe("");
  });
});

describe("terminal text safety", () => {
  test("strips ANSI and OSC sequences", () => {
    expect(stripTerminalControl("\u001B[31mred\u001B[0m")).toBe("red");
    expect(stripTerminalControl("\u001B]0;title\u0007text")).toBe("text");
    expect(stripTerminalControl("a\u0007b")).toBe("ab");
  });

  test("collapses multi-line process output to one UI line", () => {
    expect(singleLine("line one\r\nline two\tvalue")).toBe("line one line two value");
    expect(singleLine("  \u001B[1mfailed\u001B[0m  ")).toBe("failed");
    expect(singleLine(undefined)).toBe("");
  });
});
