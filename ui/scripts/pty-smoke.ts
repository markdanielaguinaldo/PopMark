/**
 * Headless PTY smoke test for the real OpenTUI entry point.
 * Run from ui/: bun --conditions=browser scripts/pty-smoke.ts
 */
const decoder = new TextDecoder();
let output = "";
let exited = false;
const startedAt = Date.now();
let onlineAt = 0;

const proc = Bun.spawn({
  cmd: [process.execPath, "--conditions=browser", "src/index.tsx"],
  cwd: import.meta.dir.replace(/[\\/]scripts$/, ""),
  terminal: {
    cols: Number(process.env.POPMARK_SMOKE_COLS ?? 120),
    rows: Number(process.env.POPMARK_SMOKE_ROWS ?? 36),
    data(_terminal, data) {
      output += decoder.decode(data, { stream: true });
      if (!onlineAt && output.includes("ONLINE")) onlineAt = Date.now();
      if (output.length > 400_000) output = output.slice(-200_000);
    },
  },
});

const closed = proc.exited.then((code) => {
  exited = true;
  return code;
});

async function waitFor(predicate: () => boolean, timeoutMs = 10_000) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    if (predicate()) return true;
    await Bun.sleep(200);
  }
  return predicate();
}

const connected = await waitFor(
  () =>
    output.includes("backend connected") ||
    output.includes("ONLINE") ||
    output.includes("v1.1.2"),
);
const startup = output;
console.log(`online-after=${onlineAt ? onlineAt - startedAt : -1}ms`);

let helpDialog = false;
let toolsDialog = false;
let splashView = false;
let logsView = false;

if (connected) {
  proc.terminal?.write("\u001bOS");
  logsView = await waitFor(() => output.includes("DIAGNOSTICS"), 3_000);
  proc.terminal?.write("\u001bOS");
  await Bun.sleep(200);

  proc.terminal?.write("\u001bOP");
  helpDialog = await waitFor(() => output.includes("COMMAND REFERENCE"), 3_000);
  proc.terminal?.write("\u001bOP");
  await Bun.sleep(200);

  proc.terminal?.write("\u001bOQ");
  toolsDialog = await waitFor(() => output.includes("PLAYBACK TOOLS"), 3_000);
  proc.terminal?.write("\u001bOQ");
  await Bun.sleep(200);

  proc.terminal?.write("\u001bOR");
  splashView = await waitFor(() => output.includes("A TERMINAL JUKEBOX"), 3_000);
  for (const char of "qzzz") {
    proc.terminal?.write(char);
    await Bun.sleep(40);
  }
  proc.terminal?.write("\u001bOR");
  await Bun.sleep(300);

  for (const char of "shuffle") {
    proc.terminal?.write(char);
    await Bun.sleep(40);
  }
  proc.terminal?.write("\r");
  await waitFor(() => /ylist is empty|Shuffled|track\(s\)|No other tracks|only has one track/.test(output), 3_000);
}

const roundTrip = /ylist is empty|Shuffled|track\(s\)|No other tracks|only has one track/.test(output);

const startupChecks = [
  ["alternate screen", startup.includes("\u001b[?1049h") || startup.includes("\u001b[?47h")],
  ["brand", startup.includes("POPMARK")],
  ["now playing", startup.includes("NOW PLAYING")],
  ["queue", startup.includes("QUEUE")],
  ["command input", startup.includes("add <url or search>")],
];

for (const [label, ok] of startupChecks) {
  console.log(`${ok ? "ok" : "MISSING"}  ${label}`);
}

console.log(`${connected ? "ok" : "MISSING"}  backend handshake`);
console.log(`${helpDialog ? "ok" : "MISSING"}  help dialog`);
console.log(`${toolsDialog ? "ok" : "MISSING"}  tools dialog`);
console.log(`${splashView ? "ok" : "MISSING"}  focus view`);
console.log(`${logsView ? "ok" : "MISSING"}  diagnostics view`);
console.log(`${roundTrip ? "ok" : "MISSING"}  command round-trip`);

proc.terminal?.write("\u0011"); // ctrl+q
const finished = await Promise.race([closed, Bun.sleep(5000).then(() => null)]);

if (!exited) proc.kill();

const failed = startupChecks.filter(([, ok]) => !ok);
if (failed.length > 0 || !connected || !roundTrip || !helpDialog || !toolsDialog || !splashView || !logsView) {
  console.log("---- output tail ----");
  console.log(output.slice(-4000));
  process.exitCode = 1;
}

console.log(`exit=${finished ?? "killed"}`);
