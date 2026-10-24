export type Track = {
  title: string;
  url: string;
  displaySource: string;
  durationSeconds?: number;
};

export type PlaybackStatus = "Stopped" | "Loading" | "Playing" | "Paused" | "Detached";

export type ToolInfo = {
  name: string;
  displayName: string;
  path?: string;
  runnable: boolean;
  pinnedPath?: string;
};

export type QueueMessage = {
  type: "queue";
  revision: number;
  current?: Track;
  currentIndex: number;
  previous: Track[];
  pending: Track[];
};

export type StateMessage = {
  type: "state";
  revision: number;
  status: PlaybackStatus;
  elapsedSeconds: number;
  durationSeconds?: number;
  volumePercent: number;
  message: string;
  busy: boolean;
};

export type HelloMessage = {
  type: "hello";
  protocol: number;
  app: string;
  version: string;
};

export type ToolsMessage = {
  type: "tools";
  tools: ToolInfo[];
  toolRoot: string;
  overrideFile: string;
};

export type BusyMessage = {
  type: "busy";
  busy: boolean;
  label?: string;
};

export type LogLevel = "info" | "warn" | "error";

export type LogMessage = {
  type: "log";
  level: LogLevel;
  source: string;
  line: string;
};

export type AckMessage = {
  type: "ack";
  id?: string;
  ok: boolean;
  message?: string;
};

export type ErrorMessage = {
  type: "error";
  id?: string;
  message: string;
};

export type ByeMessage = {
  type: "bye";
};

export type BackendMessage =
  | HelloMessage
  | QueueMessage
  | StateMessage
  | ToolsMessage
  | BusyMessage
  | LogMessage
  | AckMessage
  | ErrorMessage
  | ByeMessage;

export type OutboundMessage =
  | { type: "ping"; id?: string }
  | { type: "command"; id?: string; name: string }
  | { type: "input"; id?: string; text: string }
  | { type: "add"; id?: string; target: string }
  | { type: "seek"; id?: string; seconds?: number; delta?: number }
  | { type: "volume"; id?: string; percent?: number; delta?: number }
  | { type: "play-index"; id?: string; index: number }
  | { type: "tools"; id?: string; action?: string; tool?: string; path?: string }
  | { type: "quit"; id?: string };

const messageTypes = new Set([
  "hello",
  "queue",
  "state",
  "tools",
  "busy",
  "log",
  "ack",
  "error",
  "bye",
]);

/** Parse one protocol line. Returns null for malformed or unknown messages. */
export function parseBackendMessage(line: string): BackendMessage | null {
  let value: unknown;
  try {
    value = JSON.parse(line);
  } catch {
    return null;
  }

  if (typeof value !== "object" || value === null) return null;

  const type = (value as { type?: unknown }).type;
  if (typeof type !== "string" || !messageTypes.has(type)) return null;

  return value as BackendMessage;
}
