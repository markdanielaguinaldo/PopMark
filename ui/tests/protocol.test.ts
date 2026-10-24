import { describe, expect, test } from "bun:test";
import { parseBackendMessage } from "../src/bridge/protocol";

describe("parseBackendMessage", () => {
  test("parses state and queue messages", () => {
    const state = parseBackendMessage(
      JSON.stringify({
        type: "state",
        revision: 3,
        status: "Playing",
        elapsedSeconds: 12.5,
        volumePercent: 80,
        message: "Now playing: Test",
        busy: false,
      }),
    );

    expect(state?.type).toBe("state");

    const queue = parseBackendMessage(
      JSON.stringify({
        type: "queue",
        revision: 2,
        currentIndex: 0,
        previous: [],
        pending: [{ title: "Track", url: "https://example.com", displaySource: "YouTube" }],
      }),
    );

    expect(queue?.type).toBe("queue");
  });

  test("rejects malformed JSON and unknown messages", () => {
    expect(parseBackendMessage("{not json")).toBeNull();
    expect(parseBackendMessage(JSON.stringify({ type: "mystery" }))).toBeNull();
    expect(parseBackendMessage(JSON.stringify({}))).toBeNull();
    expect(parseBackendMessage("null")).toBeNull();
  });

  test("parses diagnostic log messages", () => {
    const log = parseBackendMessage(
      JSON.stringify({
        type: "log",
        level: "warn",
        source: "tools",
        line: "mpv: not found",
      }),
    );

    expect(log?.type).toBe("log");
    if (log?.type === "log") {
      expect(log.level).toBe("warn");
      expect(log.source).toBe("tools");
      expect(log.line).toBe("mpv: not found");
    }
  });
});
