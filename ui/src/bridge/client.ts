import { resolveBackend, spawnBackend, type BackendProcess } from "./backend";
import {
  parseBackendMessage,
  type BackendMessage,
  type OutboundMessage,
} from "./protocol";

export type BridgeEvents = {
  onMessage: (message: BackendMessage) => void;
  onLog: (line: string) => void;
  onExit: (info: { code: number | null; error?: string }) => void;
};

export type BridgeClient = {
  readonly backendPath: () => string;
  start: () => Promise<void>;
  send: (message: OutboundMessage) => void;
  request: (message: OutboundMessage, timeoutMs?: number) => Promise<string | undefined>;
  close: () => Promise<void>;
};

type PendingRequest = {
  resolve: (message: string | undefined) => void;
  reject: (error: Error) => void;
  timer: ReturnType<typeof setTimeout>;
};

const DEFAULT_TIMEOUT_MS = 20_000;
const LONG_TIMEOUT_MS = 15 * 60_000;

function isLongRunning(message: OutboundMessage): boolean {
  if (message.type === "tools" && ["install", "repair", "fix"].includes(message.action ?? "")) {
    return true;
  }

  if (message.type === "add") return true;

  if (message.type === "input") {
    const text = message.text.trim();
    return (
      /^add\b/i.test(text) ||
      /^https?:\/\//i.test(text) ||
      /^(tools|tool|doctor)\b.*\b(install|repair|fix)\b/i.test(text)
    );
  }

  return false;
}

export function createBridgeClient(events: BridgeEvents): BridgeClient {
  let child: BackendProcess | null = null;
  let closed = false;
  let backendPath = "";
  const pending = new Map<string, PendingRequest>();

  function settle(id: string | undefined, error?: Error, message?: string) {
    if (!id) return;
    const entry = pending.get(id);
    if (!entry) return;
    pending.delete(id);
    clearTimeout(entry.timer);
    if (error) entry.reject(error);
    else entry.resolve(message);
  }

  function rejectAll(error: Error) {
    for (const [id, entry] of pending) {
      clearTimeout(entry.timer);
      entry.reject(error);
      pending.delete(id);
    }
  }

  async function pumpLines(
    stream: ReadableStream<Uint8Array>,
    onLine: (line: string) => void,
  ) {
    const reader = stream.getReader();
    const decoder = new TextDecoder();
    let buffer = "";

    try {
      for (;;) {
        const { done, value } = await reader.read();
        if (done) break;
        buffer += decoder.decode(value, { stream: true });

        if (buffer.length > 1_000_000) {
          events.onLog("Discarded an oversized stream chunk without a newline.");
          buffer = "";
        }

        for (;;) {
          const newline = buffer.indexOf("\n");
          if (newline < 0) break;
          const line = buffer.slice(0, newline).replace(/\r$/, "");
          buffer = buffer.slice(newline + 1);
          if (line.trim()) onLine(line);
        }
      }
    } finally {
      buffer += decoder.decode();
      if (buffer.trim()) onLine(buffer.replace(/\r$/, ""));
      reader.releaseLock();
    }
  }

  function write(message: OutboundMessage): boolean {
    if (closed || !child) return false;

    try {
      const stdin = child.stdin;
      stdin.write(`${JSON.stringify(message)}\n`);
      const flushed = stdin.flush();
      if (flushed instanceof Promise) void flushed.catch(() => undefined);
      return true;
    } catch {
      return false;
    }
  }

  async function start() {
    const launch = resolveBackend();
    backendPath = launch.description;
    child = spawnBackend(launch);

    void pumpLines(child.stdout, (line) => {
      const message = parseBackendMessage(line);
      if (!message) {
        events.onLog(`[protocol] ignored: ${line}`);
        return;
      }

      if (message.type === "ack") {
        if (message.ok) settle(message.id, undefined, message.message);
        else settle(message.id, new Error(message.message ?? "Command failed."));
      } else if (message.type === "error") {
        settle(message.id, new Error(message.message));
      }

      events.onMessage(message);
    }).catch((error: unknown) => {
      events.onLog(`stdout reader stopped: ${String(error)}`);
    });

    void pumpLines(child.stderr, (line) => {
      events.onLog(line);
    }).catch((error: unknown) => {
      events.onLog(`stderr reader stopped: ${String(error)}`);
    });

    void child.exited.then((code) => {
      if (closed) return;
      closed = true;
      rejectAll(new Error("The PopMark backend stopped."));
      events.onExit({ code });
    }).catch((error: unknown) => {
      if (closed) return;
      closed = true;
      rejectAll(new Error(String(error)));
      events.onExit({ code: null, error: String(error) });
    });
  }

  function send(message: OutboundMessage) {
    write(message);
  }

  function request(
    message: OutboundMessage,
    timeoutMs = isLongRunning(message) ? LONG_TIMEOUT_MS : DEFAULT_TIMEOUT_MS,
  ): Promise<string | undefined> {
    const id = message.id ?? crypto.randomUUID();

    return new Promise<string | undefined>((resolve, reject) => {
      const timer = setTimeout(() => {
        pending.delete(id);
        reject(new Error(`Timed out waiting for: ${message.type}`));
      }, timeoutMs);

      pending.set(id, { resolve, reject, timer });

      if (!write({ ...message, id } as OutboundMessage)) {
        clearTimeout(timer);
        pending.delete(id);
        reject(new Error("The PopMark backend is not running."));
      }
    });
  }

  async function close() {
    if (closed) return;
    closed = true;

    if (child) {
      try {
        child.stdin.write(`${JSON.stringify({ type: "quit" })}\n`);
        await child.stdin.flush();
      } catch {
      }

      const exited = await Promise.race([
        child.exited.then(() => true),
        new Promise<boolean>((resolve) => setTimeout(() => resolve(false), 1200)),
      ]);

      if (!exited) {
        try {
          child.kill();
        } catch {
        }
      }
    }

    rejectAll(new Error("PopMark closed."));
  }

  return {
    backendPath: () => backendPath,
    start,
    send,
    request,
    close,
  };
}
