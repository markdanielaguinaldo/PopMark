import { createCliRenderer } from "@opentui/core";
import { render } from "@opentui/solid";
import { App } from "./app";
import { theme } from "./theme";

if (!process.stdout.isTTY) {
  console.error("PopMark's OpenTUI frontend needs an interactive terminal.");
  console.error("Run it from Windows Terminal, or use the C# TUI: PopMark.exe");
  process.exit(1);
}

const renderer = await createCliRenderer({
  exitOnCtrlC: false,
  useMouse: true,
  autoFocus: false,
  targetFps: 60,
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

process.on("exit", destroyRenderer);
process.on("SIGINT", () => {
  destroyRenderer();
  process.exit(0);
});
process.on("SIGTERM", () => {
  destroyRenderer();
  process.exit(0);
});

try {
  renderer.setTerminalTitle("PopMark");
  await render(() => <App />, renderer);
} catch (error) {
  destroyRenderer();
  console.error(error);
  process.exit(1);
}
