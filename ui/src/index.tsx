import { createCliRenderer } from "@opentui/core";
import { render } from "@opentui/solid";
import { App } from "./app";
import { theme } from "./theme";

if (!process.stdout.isTTY) {
  console.error("PopMark's OpenTUI frontend needs an interactive terminal.");
  console.error("Run it from Windows Terminal, Termux, or any ANSI terminal. PopMark.exe");
  console.error("without Bun falls back to the C# interface.");
  process.exit(1);
}

const renderer = await createCliRenderer({
  exitOnCtrlC: false,
  useMouse: true,
  // Only report motion while a button is held. Hover is unused, and this avoids
  // a flood of native hit-test work whenever the pointer crosses the window.
  enableMouseMovement: false,
  autoFocus: false,
  targetFps: 30,
  screenMode: "alternate-screen",
  clearOnShutdown: true,
  backgroundColor: theme.bg,
});

let destroyed = false;

function destroyRenderer() {
  if (destroyed) return;
  destroyed = true;
  try {
    renderer.destroy();
  } catch {
  }
}

async function shutdown(code: number) {
  destroyRenderer();
  // destroy() writes the terminal restore sequences asynchronously. Exiting
  // before they flush leaves the shell in mouse-reporting/alternate-screen mode.
  await renderer.closed.catch(() => undefined);
  process.exit(code);
}

process.on("exit", destroyRenderer);
process.on("SIGINT", () => {
  void shutdown(0);
});
process.on("SIGTERM", () => {
  void shutdown(0);
});
process.on("uncaughtException", (error) => {
  console.error(error);
  void shutdown(1);
});
process.on("unhandledRejection", (error) => {
  console.error(error);
  void shutdown(1);
});

try {
  renderer.setTerminalTitle("PopMark");
  await render(() => <App />, renderer);
} catch (error) {
  console.error(error);
  await shutdown(1);
}
