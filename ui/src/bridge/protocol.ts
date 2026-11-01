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

const playbackStatuses = new Set<PlaybackStatus>([
  "Stopped",
  "Loading",
  "Playing",
  "Paused",
  "Detached",
]);

const logLevels = new Set<LogLevel>(["info", "warn", "error"]);

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function isFiniteNumber(value: unknown): value is number {
  return typeof value === "number" && Number.isFinite(value);
}

function stringField(value: Record<string, unknown>, key: string): string | null {
  const field = value[key];
  return typeof field === "string" ? field : null;
}

function parseTrack(value: unknown): Track | null {
  if (!isRecord(value)) return null;

  const title = stringField(value, "title");
  const url = stringField(value, "url");
  const displaySource = stringField(value, "displaySource");
  if (title === null || url === null || displaySource === null) return null;

  const duration = value.durationSeconds;
  if (duration !== undefined && !isFiniteNumber(duration)) return null;

  return duration === undefined
    ? { title, url, displaySource }
    : { title, url, displaySource, durationSeconds: duration };
}

function parseTrackList(value: unknown): Track[] | null {
  if (!Array.isArray(value)) return null;

  const tracks: Track[] = [];
  for (const entry of value) {
    const track = parseTrack(entry);
    if (!track) return null;
    tracks.push(track);
  }

  return tracks;
}

function parseTool(value: unknown): ToolInfo | null {
  if (!isRecord(value)) return null;

  const name = stringField(value, "name");
  const displayName = stringField(value, "displayName");
  if (name === null || displayName === null || typeof value.runnable !== "boolean") return null;

  const path = value.path === null ? undefined : value.path;
  const pinnedPath = value.pinnedPath === null ? undefined : value.pinnedPath;
  if (path !== undefined && typeof path !== "string") return null;
  if (pinnedPath !== undefined && typeof pinnedPath !== "string") return null;

  return {
    name,
    displayName,
    runnable: value.runnable,
    ...(path === undefined ? {} : { path }),
    ...(pinnedPath === undefined ? {} : { pinnedPath }),
  };
}

function parseMessage(value: Record<string, unknown>): BackendMessage | null {
  switch (value.type) {
    case "hello": {
      const app = stringField(value, "app");
      const version = stringField(value, "version");
      if (app === null || version === null || !isFiniteNumber(value.protocol)) return null;
      return { type: "hello", protocol: value.protocol, app, version };
    }

    case "queue": {
      const current = value.current === null || value.current === undefined
        ? undefined
        : parseTrack(value.current);
      if (value.current !== null && value.current !== undefined && !current) return null;

      const previous = parseTrackList(value.previous);
      const pending = parseTrackList(value.pending);
      if (!previous || !pending || !isFiniteNumber(value.revision) || !isFiniteNumber(value.currentIndex)) {
        return null;
      }

      return {
        type: "queue",
        revision: value.revision,
        currentIndex: value.currentIndex,
        previous,
        pending,
        ...(current ? { current } : {}),
      };
    }

    case "state": {
      const message = stringField(value, "message");
      if (
        message === null ||
        !isFiniteNumber(value.revision) ||
        !isFiniteNumber(value.elapsedSeconds) ||
        !isFiniteNumber(value.volumePercent) ||
        typeof value.busy !== "boolean" ||
        typeof value.status !== "string" ||
        !playbackStatuses.has(value.status as PlaybackStatus)
      ) {
        return null;
      }

      if (value.durationSeconds !== undefined && !isFiniteNumber(value.durationSeconds)) return null;

      return {
        type: "state",
        revision: value.revision,
        status: value.status as PlaybackStatus,
        elapsedSeconds: value.elapsedSeconds,
        volumePercent: value.volumePercent,
        message,
        busy: value.busy,
        ...(value.durationSeconds === undefined ? {} : { durationSeconds: value.durationSeconds }),
      };
    }

    case "tools": {
      if (!Array.isArray(value.tools)) return null;

      const tools: ToolInfo[] = [];
      for (const entry of value.tools) {
        const tool = parseTool(entry);
        if (!tool) return null;
        tools.push(tool);
      }

      const toolRoot = stringField(value, "toolRoot");
      const overrideFile = stringField(value, "overrideFile");
      if (toolRoot === null || overrideFile === null) return null;

      return { type: "tools", tools, toolRoot, overrideFile };
    }

    case "busy": {
      if (typeof value.busy !== "boolean") return null;
      if (value.label !== undefined && typeof value.label !== "string") return null;
      return {
        type: "busy",
        busy: value.busy,
        ...(value.label === undefined ? {} : { label: value.label }),
      };
    }

    case "log": {
      const source = stringField(value, "source");
      const line = stringField(value, "line");
      if (source === null || line === null || typeof value.level !== "string" || !logLevels.has(value.level as LogLevel)) {
        return null;
      }

      return { type: "log", level: value.level as LogLevel, source, line };
    }

    case "ack": {
      if (typeof value.ok !== "boolean") return null;
      if (value.id !== undefined && typeof value.id !== "string") return null;
      if (value.message !== undefined && typeof value.message !== "string") return null;
      return {
        type: "ack",
        ok: value.ok,
        ...(value.id === undefined ? {} : { id: value.id }),
        ...(value.message === undefined ? {} : { message: value.message }),
      };
    }

    case "error": {
      const message = stringField(value, "message");
      if (message === null) return null;
      if (value.id !== undefined && typeof value.id !== "string") return null;
      return { type: "error", message, ...(value.id === undefined ? {} : { id: value.id }) };
    }

    case "bye":
      return { type: "bye" };

    default:
      return null;
  }
}

/** Parse one protocol line. Returns null for malformed or unknown messages. */
export function parseBackendMessage(line: string): BackendMessage | null {
  let value: unknown;
  try {
    value = JSON.parse(line);
  } catch {
    return null;
  }

  return isRecord(value) ? parseMessage(value) : null;
}
