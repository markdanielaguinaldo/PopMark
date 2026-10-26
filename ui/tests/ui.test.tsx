import { expect, test } from "bun:test";
import type { InputRenderable } from "@opentui/core";
import { testRender } from "@opentui/solid";
import { App } from "../src/app";
import type { BridgeClient, BridgeEvents } from "../src/bridge/client";
import type { BackendMessage, OutboundMessage } from "../src/bridge/protocol";
import { HelpDialog, LogsDialog, ToolsDialog } from "../src/components/Dialogs";
import { NowPlaying } from "../src/components/NowPlaying";
import { Splash } from "../src/components/Splash";
import type { LogEntry } from "../src/lib/logs";
import { applyTheme, themeId } from "../src/theme";

function seed(emit: (message: BackendMessage) => void) {
  emit({ type: "hello", protocol: 1, app: "PopMark", version: "1.1.2" });
  emit({
    type: "tools",
    tools: [
      { name: "yt-dlp", displayName: "yt-dlp", path: "C:\\tools\\yt-dlp.exe", runnable: true },
      { name: "mpv", displayName: "mpv", path: "C:\\tools\\mpv.exe", runnable: true },
    ],
    toolRoot: "C:\\tools",
    overrideFile: "C:\\tools.json",
  });
  emit({
    type: "queue",
    revision: 2,
    current: {
      title: "Test Track",
      url: "https://example.com/watch?v=test",
      displaySource: "YouTube",
      durationSeconds: 180,
    },
    currentIndex: 1,
    previous: [
      {
        title: "Earlier Track",
        url: "https://example.com/watch?v=earlier",
        displaySource: "YouTube",
        durationSeconds: 120,
      },
    ],
    pending: [
      {
        title: "Upcoming Track",
        url: "https://example.com/watch?v=upcoming",
        displaySource: "YouTube",
        durationSeconds: 240,
      },
    ],
  });
  emit({
    type: "state",
    revision: 2,
    status: "Playing",
    elapsedSeconds: 42,
    durationSeconds: 180,
    volumePercent: 70,
    message: "Now playing: Test Track",
    busy: false,
  });
  emit({
    type: "log",
    level: "info",
    source: "tools",
    line: "yt-dlp and mpv resolved successfully.",
  });
}

function fakeBackend() {
  const sent: OutboundMessage[] = [];
  let events: BridgeEvents | undefined;

  const client: BridgeClient = {
    backendPath: () => "fake-backend.exe",
    start: async () => undefined,
    send: (message) => {
      sent.push(message);
    },
    request: async (message) => {
      sent.push(message);
      return undefined;
    },
    close: async () => undefined,
  };

  return {
    sent,
    client,
    factory: (bridgeEvents: BridgeEvents) => {
      events = bridgeEvents;
      seed(bridgeEvents.onMessage);
      return client;
    },
    emit: (message: BackendMessage) => events?.onMessage(message),
  };
}

test("renders the queue and sends transport shortcuts", async () => {
  applyTheme("midnight", false);
  const fake = fakeBackend();
  const setup = await testRender(() => <App createClient={fake.factory} />, {
    width: 120,
    height: 40,
  });

  try {
    await setup.renderOnce();
    await setup.flush();

    const frame = setup.captureCharFrame();
    expect(frame).toContain("NOW PLAYING");
    expect(frame).toContain("Test Track");
    expect(frame).toContain("Upcoming Track");
    expect(frame).toContain("PLAYING");
    expect(frame).toContain("v1.1.2");
    expect(frame).toContain("connected");
    expect(frame).toContain("0:42");
    expect(frame).toContain("3:00");

    const progressTrack = setup.renderer.root.findDescendantById("progress-bar");
    expect(progressTrack).toBeDefined();
    const trackText = progressTrack!.getChildren()[0] as { plainText?: string };
    const trackGlyphs = trackText.plainText ?? "";
    const filledGlyphs = [...trackGlyphs].filter((glyph) => glyph === "━").length;
    const ratio = filledGlyphs / progressTrack!.width;
    expect(ratio).toBeGreaterThan(0.15);
    expect(ratio).toBeLessThan(0.35);

    setup.mockInput.pressKey("n", { ctrl: true });
    await setup.renderOnce();
    expect(fake.sent).toContainEqual({ type: "command", name: "next" });

    setup.mockInput.pressArrow("right", { ctrl: true });
    await setup.renderOnce();
    expect(fake.sent).toContainEqual({ type: "seek", delta: 10 });

    setup.mockInput.pressArrow("up", { ctrl: true });
    await setup.renderOnce();
    expect(fake.sent).toContainEqual({ type: "volume", percent: 80 });

    setup.mockInput.pressKey("F5");
    await setup.renderOnce();
    expect(themeId()).toBe("ember");

    setup.mockInput.pressKey("F5", { shift: true });
    await setup.renderOnce();
    expect(themeId()).toBe("midnight");
    applyTheme("midnight", false);

    const queueRow = setup.renderer.root.findDescendantById("queue-row-2");
    expect(queueRow).toBeDefined();
    await setup.mockMouse.click(queueRow!.x + 2, queueRow!.y);
    await setup.renderOnce();
    expect(fake.sent).toContainEqual({ type: "play-index", index: 2 });

    const progressBar = setup.renderer.root.findDescendantById("progress-bar");
    expect(progressBar).toBeDefined();
    await setup.mockMouse.click(
      progressBar!.x + Math.floor(progressBar!.width / 2),
      progressBar!.y,
    );
    await setup.renderOnce();
    const seek = fake.sent.find((message) => message.type === "seek" && "seconds" in message);
    expect(seek).toBeDefined();
    expect((seek as { seconds: number }).seconds).toBeGreaterThan(70);
    expect((seek as { seconds: number }).seconds).toBeLessThan(110);

    const volumeBar = setup.renderer.root.findDescendantById("volume-bar");
    expect(volumeBar).toBeDefined();
    await setup.mockMouse.click(
      volumeBar!.x + Math.floor(volumeBar!.width / 2),
      volumeBar!.y,
    );
    await setup.renderOnce();
    const volume = fake.sent
      .filter((message) => message.type === "volume" && "percent" in message)
      .at(-1);
    expect(volume).toBeDefined();
    expect((volume as { percent: number }).percent).toBeGreaterThan(55);
    expect((volume as { percent: number }).percent).toBeLessThan(75);

    await setup.mockMouse.drag(
      volumeBar!.x + 1,
      volumeBar!.y,
      volumeBar!.x + volumeBar!.width - 2,
      volumeBar!.y,
    );
    await setup.renderOnce();
    const draggedVolume = fake.sent
      .filter((message) => message.type === "volume" && "percent" in message)
      .at(-1);
    expect((draggedVolume as { percent: number }).percent).toBeGreaterThan(80);

    await setup.mockMouse.drag(
      progressBar!.x + 1,
      progressBar!.y,
      progressBar!.x + Math.floor(progressBar!.width * 0.75),
      progressBar!.y,
    );
    await setup.renderOnce();
    const draggedSeek = fake.sent
      .filter((message) => message.type === "seek" && "seconds" in message)
      .at(-1);
    expect((draggedSeek as { seconds: number }).seconds).toBeGreaterThan(110);
  } finally {
    await setup.dispose();
  }
});

test("submits commands and supports navigation mode", async () => {
  const fake = fakeBackend();
  const setup = await testRender(() => <App createClient={fake.factory} />, {
    width: 110,
    height: 34,
  });

  try {
    await setup.renderOnce();
    await setup.flush();

    await setup.mockInput.typeText("shuffle");
    setup.mockInput.pressEnter();
    await setup.renderOnce();
    expect(fake.sent).toContainEqual({ type: "input", text: "shuffle" });

    setup.mockInput.pressEscape();
    await new Promise((resolve) => setTimeout(resolve, 150));
    await setup.renderOnce();

    setup.mockInput.pressKey("n");
    await setup.renderOnce();
    expect(fake.sent).toContainEqual({ type: "command", name: "next" });

    setup.mockInput.pressKey("/");
    await setup.renderOnce();
    await setup.mockInput.typeText("add lo-fi beats");
    setup.mockInput.pressEnter();
    await setup.renderOnce();
    expect(fake.sent).toContainEqual({ type: "input", text: "add lo-fi beats" });
  } finally {
    await setup.dispose();
  }
});

test("focus view owns input and does not accept command typing", async () => {
  const fake = fakeBackend();
  const setup = await testRender(() => <App createClient={fake.factory} />, {
    width: 110,
    height: 34,
  });

  const commandInput = () =>
    setup.renderer.root.findDescendantById("command-input") as InputRenderable | undefined;

  try {
    await setup.renderOnce();
    await setup.flush();
    expect(commandInput()?.value ?? "").toBe("");

    setup.mockInput.pressKey("F3");
    await setup.renderOnce();
    await setup.flush();
    expect(setup.renderer.currentFocusedRenderable).not.toBe(commandInput()!);

    await setup.mockInput.typeText("zzz");
    await setup.renderOnce();
    expect(commandInput()?.value ?? "").toBe("");
    expect(fake.sent.some((message) => message.type === "input")).toBe(false);

    setup.mockInput.pressKey("n");
    await setup.renderOnce();
    expect(fake.sent).toContainEqual({ type: "command", name: "next" });

    setup.mockInput.pressEscape();
    await new Promise((resolve) => setTimeout(resolve, 150));
    await setup.renderOnce();
    await setup.flush();
    expect(setup.renderer.currentFocusedRenderable).toBe(commandInput()!);

    await setup.mockInput.typeText("abc");
    await setup.renderOnce();
    expect(commandInput()?.value).toBe("abc");

    setup.mockInput.pressTab();
    await setup.renderOnce();
    expect(setup.renderer.currentFocusedRenderable).not.toBe(commandInput()!);

    setup.mockInput.pressTab();
    await setup.renderOnce();
    await setup.flush();
    expect(setup.renderer.currentFocusedRenderable).not.toBe(commandInput()!);

    setup.mockInput.pressKey("F1");
    await setup.renderOnce();
    await setup.flush();
    await setup.mockInput.typeText("xyz");
    await setup.renderOnce();
    expect(commandInput()?.value).toBe("abc");

    setup.mockInput.pressEscape();
    await new Promise((resolve) => setTimeout(resolve, 150));
    await setup.renderOnce();
    await setup.flush();
    expect(setup.renderer.currentFocusedRenderable).not.toBe(commandInput()!);

    setup.mockInput.pressEscape();
    await new Promise((resolve) => setTimeout(resolve, 150));
    await setup.renderOnce();
    await setup.flush();
    expect(setup.renderer.currentFocusedRenderable).not.toBe(commandInput()!);

    setup.mockInput.pressKey("/");
    await setup.renderOnce();
    await setup.flush();
    expect(setup.renderer.currentFocusedRenderable).toBe(commandInput()!);

    await setup.mockInput.typeText("def");
    await setup.renderOnce();
    expect(commandInput()?.value).toBe("abcdef");
  } finally {
    await setup.dispose();
  }
});

test("keeps diagnostic text out of playback panels and inside the log view", async () => {
  const nowPlaying = await testRender(
    () => (
      <NowPlaying
        current={{
          title: "Track",
          url: "https://example.com",
          displaySource: "YouTube",
          durationSeconds: 200,
        }}
        status="Playing"
        elapsed={10}
        duration={200}
        volume={70}
        message={"tool report line one\r\ntool report line two"}
        busy={false}
        columns={80}
        onToggle={() => undefined}
        onNext={() => undefined}
        onPrevious={() => undefined}
        onStop={() => undefined}
        onShuffle={() => undefined}
        onSeek={() => undefined}
        onVolume={() => undefined}
      />
    ),
    { width: 110, height: 20 },
  );

  try {
    await nowPlaying.renderOnce();
    const frame = nowPlaying.captureCharFrame();
    expect(frame).toContain("tool report line one tool report line two");
    expect(frame).toContain("0:10");
    expect(frame).toContain("3:20");
  } finally {
    await nowPlaying.dispose();
  }

  const logs: LogEntry[] = [
    {
      id: 1,
      at: Date.now(),
      source: "tools",
      level: "info",
      line: "mpv is now installed at C:\\tools\\mpv.exe.",
    },
    {
      id: 2,
      at: Date.now(),
      source: "backend",
      level: "error",
      line: "\u001B[31minstall failed\u001B[0m - check LOGS",
    },
  ];

  const logView = await testRender(
    () => <LogsDialog logs={logs} onClose={() => undefined} narrow={false} />,
    { width: 110, height: 28 },
  );

  try {
    await logView.renderOnce();
    const frame = logView.captureCharFrame();
    expect(frame).toContain("DIAGNOSTICS");
    expect(frame).toContain("mpv is now installed");
    expect(frame).toContain("install failed");
    expect(frame).not.toContain("\u001B[31m");
  } finally {
    await logView.dispose();
  }
});

test("stays usable at narrow and short terminal sizes", async () => {
  const fake = fakeBackend();
  const setup = await testRender(() => <App createClient={fake.factory} />, {
    width: 70,
    height: 20,
  });

  try {
    await setup.renderOnce();
    await setup.flush();
    const frame = setup.captureCharFrame();
    expect(frame).toContain("NOW PLAYING");
    expect(frame).toContain("QUEUE");
    expect(frame).toContain("Test Track");
    expect(frame).toContain("PLAY");
    expect(frame).toContain("add <url or search>");
  } finally {
    await setup.dispose();
  }
});

test("renders help and tools dialogs and the focus view", async () => {
  const help = await testRender(() => <HelpDialog onClose={() => undefined} narrow={false} />, {
    width: 96,
    height: 40,
  });

  try {
    await help.renderOnce();
    const frame = help.captureCharFrame();
    expect(frame).toContain("COMMAND REFERENCE");
    expect(frame).toContain("PLAYBACK");
    expect(frame).toContain("tools install");
    expect(frame).toContain("ctrl+p");
  } finally {
    await help.dispose();
  }

  const tools = await testRender(
    () => (
      <ToolsDialog
        tools={[
          { name: "yt-dlp", displayName: "yt-dlp", path: "C:\\tools\\yt-dlp.exe", runnable: true },
          { name: "mpv", displayName: "mpv", path: "C:\\tools\\mpv.exe", runnable: false },
        ]}
        toolRoot="C:\\tools"
        overrideFile="C:\\tools.json"
        message="Missing playback tool(s): mpv."
        busy={false}
        narrow={false}
        onClose={() => undefined}
        onInstall={() => undefined}
        onRefresh={() => undefined}
        onOpenLogs={() => undefined}
      />
    ),
    { width: 90, height: 30 },
  );

  try {
    await tools.renderOnce();
    const frame = tools.captureCharFrame();
    expect(frame).toContain("PLAYBACK TOOLS");
    expect(frame).toContain("yt-dlp");
    expect(frame).toContain("missing");
    expect(frame).toContain("REPAIR ALL");
  } finally {
    await tools.dispose();
  }

  const focusSeeks: Array<{ ratio: number; final: boolean }> = [];
  const splash = await testRender(
    () => (
      <Splash
        current={{
          title: "Focus Track",
          url: "https://example.com/watch?v=focus",
          displaySource: "YouTube",
          durationSeconds: 200,
        }}
        status="Playing"
        elapsed={30}
        duration={200}
        volume={65}
        message="Now playing: Focus Track"
        toolsReady={2}
        toolsTotal={2}
        width={110}
        height={36}
        onSeek={(ratio, final) => focusSeeks.push({ ratio, final: final ?? true })}
      />
    ),
    { width: 110, height: 36 },
  );

  try {
    await splash.renderOnce();
    const frame = splash.captureCharFrame();
    expect(frame).toContain("A TERMINAL JUKEBOX");
    expect(frame).toContain("Focus Track");
    expect(frame).toContain("tools 2/2");
    expect(frame).toContain("hoot");
    expect(splash.renderer.root.findDescendantById("focus-owl")).toBeDefined();

    const focusProgress = splash.renderer.root.findDescendantById("focus-progress");
    expect(focusProgress).toBeDefined();
    await splash.mockMouse.drag(
      focusProgress!.x + 1,
      focusProgress!.y,
      focusProgress!.x + Math.floor(focusProgress!.width * 0.6),
      focusProgress!.y,
    );
    await splash.renderOnce();
    const lastSeek = focusSeeks.at(-1);
    expect(lastSeek).toBeDefined();
    expect(lastSeek!.ratio).toBeGreaterThan(0.4);
    expect(lastSeek!.final).toBe(true);
  } finally {
    await splash.dispose();
  }
});
