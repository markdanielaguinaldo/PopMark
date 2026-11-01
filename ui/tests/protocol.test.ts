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

  test("rejects known message types with the wrong shape", () => {
    expect(
      parseBackendMessage(
        JSON.stringify({
          type: "state",
          revision: "three",
          status: "Playing",
          elapsedSeconds: 1,
          volumePercent: 50,
          message: "x",
          busy: false,
        }),
      ),
    ).toBeNull();

    expect(
      parseBackendMessage(
        JSON.stringify({
          type: "queue",
          revision: 1,
          currentIndex: 0,
          previous: [],
          pending: [{ title: "Track", url: 7, displaySource: "YouTube" }],
        }),
      ),
    ).toBeNull();

    expect(parseBackendMessage(JSON.stringify({ type: "ack", ok: "yes" }))).toBeNull();
  });

  test("drops unknown fields and keeps ack ids", () => {
    const ack = parseBackendMessage(
      JSON.stringify({ type: "ack", id: "req-1", ok: true, extra: "ignored" }),
    );

    expect(ack).toEqual({ type: "ack", id: "req-1", ok: true });
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
