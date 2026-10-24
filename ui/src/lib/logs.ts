import type { LogLevel } from "../bridge/protocol";
import { singleLine } from "./format";

export type LogEntry = {
  id: number;
  at: number;
  source: string;
  level: LogLevel;
  line: string;
};

let nextId = 1;

export function inferLogLevel(line: string): LogLevel {
  const lower = line.toLowerCase();
  if (lower.includes("error") || lower.includes("failed") || lower.includes("exception")) {
    return "error";
  }
  if (
    lower.includes("warn") ||
    lower.includes("missing") ||
    lower.includes("not found") ||
    lower.includes("will not run")
  ) {
    return "warn";
  }
  return "info";
}

export function makeLogEntry(
  source: string,
  line: string,
  level: LogLevel = inferLogLevel(line),
): LogEntry {
  return {
    id: nextId++,
    at: Date.now(),
    source,
    level,
    line: singleLine(line, 1000),
  };
}

export function appendLog(entries: LogEntry[], entry: LogEntry, limit = 300): LogEntry[] {
  return [...entries.slice(-(limit - 1)), entry];
}
