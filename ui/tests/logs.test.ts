import { describe, expect, test } from "bun:test";
import { appendLog, inferLogLevel, makeLogEntry } from "../src/lib/logs";

describe("diagnostic logs", () => {
  test("infers useful levels from process text", () => {
    expect(inferLogLevel("mpv is now installed")).toBe("info");
    expect(inferLogLevel("mpv: not found")).toBe("warn");
    expect(inferLogLevel("tool install failed: network error")).toBe("error");
  });

  test("sanitizes entries before they reach the UI", () => {
    const entry = makeLogEntry("backend", "\u001B[31mline one\r\nline two\u001B[0m");
    expect(entry.line).toBe("line one line two");
  });

  test("keeps a bounded ring buffer", () => {
    let logs = [] as ReturnType<typeof makeLogEntry>[];
    for (let index = 0; index < 10; index++) {
      logs = appendLog(logs, makeLogEntry("backend", `line ${index}`), 5);
    }

    expect(logs).toHaveLength(5);
    expect(logs[0].line).toBe("line 5");
    expect(logs.at(-1)?.line).toBe("line 9");
  });
});
