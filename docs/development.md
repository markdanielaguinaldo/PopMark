# PopMark development notes

## Architecture

PopMark is now split at a process boundary:

```text
PopMark.exe / dotnet PopMark.dll
│
├── (default, interactive) OpenTuiLauncher
│   └── bun ui/src/index.tsx
│       ├── OpenTUI + SolidJS renderer
│       └── spawns PopMark --bridge
│
└── PopMark --bridge
    ├── PlaybackQueue / MpvPlayer / YtDlpService
    ├── QueueCacheStore / PlaybackSessionStore
    ├── DependencyInstaller / ToolLocator
    └── JSON state -> stdout, JSON commands <- stdin
```

The C# application remains the single owner of playback state. The frontend does not
manage mpv, spawn mpv, parse YouTube metadata, or keep a second queue. It renders the
snapshots it receives from the bridge and sends intent back.

`OpenTuiLauncher` starts the frontend by default when:

- stdio is an interactive terminal,
- Bun is available, and
- `ui/src/index.tsx` (development) or `ui/dist/index.js` (bundled) can be found.

Use `--legacy-ui` only to inspect the retired C# renderer, or run `PopMark --bridge`
yourself when
working on the protocol. Environment overrides:

| Variable | Purpose |
| --- | --- |
| `POPMARK_BACKEND` | Full path to `PopMark.exe` or `PopMark.dll` for the frontend |
| `POPMARK_UI_ENTRY` | Frontend entry file used by `OpenTuiLauncher` |
| `POPMARK_BUN` | Bun executable used by `OpenTuiLauncher` |
| `POPMARK_START_URL` | URL the frontend adds after connecting |

## Prerequisites

- .NET 9 SDK
- Bun 1.3.14 or newer
- `yt-dlp` and `mpv` (PopMark can install both into
  `%LOCALAPPDATA%\PopMark\tools`)

## Build and run

Backend:

```powershell
dotnet build .\PopMark.sln --configuration Release
dotnet test .\PopMark.sln --configuration Release
```

Frontend:

```powershell
cd ui
bun install
bun run typecheck
bun run test
bun run build
bun run start
```

`bun run start` runs the source with `--conditions=browser`. This is required so
`solid-js` resolves to the reactive browser build rather than its server build under
Bun's default `node` condition. The `test` and `build` scripts do the same.

The backend is discovered in this order:

1. `POPMARK_BACKEND`
2. The newest `PopMark.dll` under `PopMark/bin/{Release,Debug}/net9.0` (launched with
   `dotnet`, which is more reliable on Windows than apphost `.exe` files)
3. A `PopMark.exe` in the same build folders, or beside the UI
4. `PopMark.exe` / `PopMark` in the current directory tree

If no backend is found, the UI shows the build command instead of failing silently.

## Frontend layout

```text
ui/
├── src/
│   ├── index.tsx             renderer lifecycle and entry point
│   ├── app.tsx               state, bridge wiring, keyboard dispatch, layout
│   ├── theme.ts              reactive palettes, persistence, brand gradient
│   ├── bridge/
│   │   ├── protocol.ts       message types and line parser
│   │   ├── backend.ts        backend discovery and spawning
│   │   └── client.ts         stdin/stdout pumps, request/ack tracking
│   ├── components/
│   │   ├── Header.tsx
│   │   ├── NowPlaying.tsx
│   │   ├── QueuePanel.tsx
│   │   ├── Sidebar.tsx
│   │   ├── Footer.tsx
│   │   ├── Dialogs.tsx
│   │   ├── Splash.tsx
│   │   └── Widgets.tsx       panels, buttons, bars, spinner, marquee, equalizer
│   └── lib/
│       ├── format.ts         time/progress/text helpers
│       └── playlist.ts       queue sections, selection, command history
├── tests/                    bun:test + OpenTUI testRender
├── scripts/pty-smoke.ts      real PTY startup/round-trip smoke test
├── build.ts                  Bun.build with the Solid plugin
└── bunfig.toml               @opentui/solid preload
```

OpenTUI notes that shaped the implementation:

- Dynamic expressions (`Show` and friends) must have a real fallback under layout
  nodes; an empty slot text node is rejected by the reconciler.
- `<Show>` children must use the callback/keyed form to stay reactive.
- Solid's `content={t`...`}` prop path stringifies `StyledText` in 0.6.8. Inline color
  is done with `<Tint>` (a styled `<span>`) instead.
- `ScrollBox` options that only exist in its constructor should not be passed from
  JSX; the CommonJS/JSX wrapper constructs components with only `{ id }` first.

## Bridge protocol

Transport: one JSON object per line, UTF-8, on stdout/stdin. Nothing else may be
written to stdout. The bridge redirects `Console.Out` to stderr, so logs, Spectre
output, and .NET diagnostics cannot corrupt the protocol.

The bridge reads the **raw** `Console.OpenStandardInput()` stream. Using `Console.In`
is unsafe when the backend is spawned by a process that already owns a console: .NET
can bind `Console.In` to that console instead of the redirected pipe.

### Backend to frontend

All messages are camelCase JSON.

```json
{"type":"hello","protocol":1,"app":"PopMark","version":"1.1.2"}
```

```json
{"type":"queue","revision":3,"currentIndex":1,
 "current":{"title":"Song","url":"https://...","displaySource":"YouTube","durationSeconds":180},
 "previous":[{"title":"Earlier","url":"https://...","displaySource":"YouTube","durationSeconds":120}],
 "pending":[{"title":"Next","url":"https://...","displaySource":"YouTube"}]}
```

`currentIndex` is the index of the current row in the combined
`previous + current + pending` list, or `-1` when nothing is current. `revision`
increases whenever the list changes.

```json
{"type":"state","revision":3,"status":"Playing","elapsedSeconds":42.5,
 "durationSeconds":180,"volumePercent":70,"message":"Now playing: Song","busy":false}
```

`status` is one of `Stopped`, `Loading`, `Playing`, `Paused`, `Detached`. State is
emitted on change and every 500 ms while the UI is open. `elapsedSeconds` is the
authoritative mpv `time-pos`, refreshed through IPC (`get_property`) by the bridge
tick. If the track metadata had no duration, the same poll fills `durationSeconds`
from mpv. `message` is the concise human-readable status used by the UI; diagnostics
never go into this field.

```json
{"type":"tools","tools":[{"name":"yt-dlp","displayName":"yt-dlp","path":"C:\\...","runnable":true}],
 "toolRoot":"C:\\Users\\...\\PopMark\\tools","overrideFile":"C:\\Users\\...\\tools.json"}
```

```json
{"type":"busy","busy":true,"label":"Adding to queue..."}
{"type":"log","level":"info","source":"tools","line":"mpv: C:\\...\\mpv.exe [ok]"}
{"type":"ack","id":"7f3...","ok":true}
{"type":"ack","id":"7f3...","ok":false,"message":"Unknown command: x"}
{"type":"error","id":"7f3...","message":"..."}
{"type":"bye"}
```

`busy` is advisory UI state for long operations such as dependency installation.
`log` carries tool reports and backend diagnostics into the `F4` diagnostics view;
the renderer never prints them into the live screen. `ack` resolves a request by
`id`; an unsuccessful ack is also surfaced in the state message.

### Frontend to backend

| Message | Meaning |
| --- | --- |
| `{"type":"ping","id":"..."}` | Liveness check; replies with `ack` |
| `{"type":"command","name":"play"}` | Toggle playback |
| `{"type":"command","name":"next"}` | Next track |
| `{"type":"command","name":"previous"}` | Previous track |
| `{"type":"command","name":"stop"}` | Stop and clear |
| `{"type":"command","name":"clear-playlist"}` | Stop and empty the queue |
| `{"type":"command","name":"shuffle"}` | Shuffle upcoming |
| `{"type":"input","text":"add <url>"}` | Any legacy command line or URL |
| `{"type":"add","target":"..."}` | Add a URL/search without shell parsing |
| `{"type":"play-index","index":2}` | Play a queue row |
| `{"type":"seek","seconds":42}` | Absolute seek |
| `{"type":"seek","delta":10}` | Relative seek |
| `{"type":"volume","percent":80}` | Set volume (0–130) |
| `{"type":"volume","delta":-10}` | Adjust volume |
| `{"type":"tools","action":"status"}` | Emit a fresh tools snapshot |
| `{"type":"tools","action":"install","tool":"mpv"}` | Install/repair a tool |
| `{"type":"tools","action":"set","tool":"mpv","path":"C:\\mpv.exe"}` | Pin a tool |
| `{"type":"quit","id":"..."}` | Stop playback, save the queue, exit |

The bridge processes requests serially. Playback methods are awaited in that order,
while state notifications can be written from playback callbacks. Writes are guarded
by a lock. If stdout breaks (frontend died), the bridge shuts down; if stdin reaches
EOF, the bridge stops playback, saves the queue, and exits.

## Live playback position

`PlaybackQueue` owns the position used by both UIs. Its estimator (`_positionOffset`
plus time since `_positionStartedAt`) is only a short-lived fallback. Every bridge tick
(500 ms) while playing calls `MpvPlayer.TryGetNumberPropertyAsync("time-pos")` over the
mpv IPC channel. Pause and resume also refresh from mpv, and a seek updates the queue
optimistically until the next poll confirms it.

The frontend keeps the state position separate from track metadata. `liveElapsedSeconds`
projects the last authoritative value forward using `Date.now()` on both ends, clamps
it to a valid duration, and freezes it while paused. `positionRatio` returns 0 when the
duration is zero, missing, or invalid. The old implementation mixed `performance.now()`
with `Date.now()`, which made the projected position billions of seconds ahead and
pinned both timestamps to the track duration.

Metadata and queue updates do not reset live position; only a track change, stop, or
restore does. If IPC fails, the last known position is kept and no 100% jump is
invented.

## Input ownership and output isolation

The UI has four explicit modes: `command`, `navigation`, `focus`, and `modal`.

- `command` owns the input renderable and accepts printable text, Enter, history, and
  Ctrl shortcuts.
- `navigation` and `focus` ignore printable characters; only documented shortcuts and
  navigation keys are handled. Focus View blurs the input and cannot submit stale text.
- `modal` routes keys to the dialog; background playback and queue state updates never
  change focus.

`Footer` owns focus through an effect driven by the mode, and the input only becomes
focused in command mode. Opening a dialog or Focus View records the previous mode and
restores it on close. Escape, Tab, Enter, and F1-F5 have fixed transitions; see the
help dialog for the current map.

## Themes

`theme.ts` exposes a Solid mutable store. Every component reads `theme.accent` or
another palette key inside JSX, so `applyTheme` retints the whole interface without
remounting it. `cycleTheme` wraps through five palettes: Midnight, Ember, Sakura,
Evergreen, and Mono. F5 cycles forward, Shift+F5 backward, and the toast names the new
theme. The selection is saved to `%LOCALAPPDATA%\PopMark\theme.json`; `POPMARK_THEME`
overrides the startup choice without rewriting the file.

All C# child processes used for tools, mpv, yt-dlp, `where.exe`, and session discovery
set `UseShellExecute=false`, redirect stdout/stderr, and generally `CreateNoWindow`.
mpv additionally runs with `--terminal=no`. The bridge reserves stdout for JSON and
redirects `Console.Out` to stderr. Tool reports are split into `log` messages, while the
state message gets a one-line summary. The frontend stores backend stderr and those
logs in a bounded ring buffer and renders them only in the `F4` diagnostics dialog;
inline status areas run text through `singleLine()` to strip ANSI and collapse newlines.



1. User presses `Ctrl+Q` or the backend exits unexpectedly.
2. The frontend calls `renderer.destroy()` and awaits `renderer.closed` so the restore
   sequences flush before `process.exit()`.
3. `client.close()` sends `quit`, waits for the backend, then kills it if needed.
4. The bridge calls `PlaybackQueue.StopPlaybackAndKeepQueueAsync()`, which fires
   `SnapshotChanged` and saves the queue.
5. `PlaybackShutdownGuard` runs as a final safety net.
6. `renderer.destroy()` restores the alternate screen and mouse modes. If the Bun
   process dies before that, `OpenTuiLauncher` writes the same reset sequences and
   reports the exit code.

Mouse reporting runs in drag mode (`enableMouseMovement: false`): motion is only sent
while a button is held, which keeps hover from generating native work on every pointer
move. The renderer targets 30 FPS.

## Tests

```powershell
# C#: layout, rendering, input, protocol DTOs
dotnet test .\PopMark.sln --configuration Debug

# Optional real-mpv IPC/position/pause/resume/seek integration tests
$env:POPMARK_MPV_INTEGRATION = "1"
dotnet test .\PopMark.sln --configuration Debug --filter FullyQualifiedName~MpvPositionIntegrationTests
Remove-Item Env:POPMARK_MPV_INTEGRATION

# Frontend: formatting, playback math, playlist/history helpers, protocol parser, rendered UI
cd ui
bun run typecheck
bun run test
```

The UI tests use `testRender` from `@opentui/solid` and a fake `BridgeClient`. They
cover static rendering, keyboard dispatch, command submission, and dialog/splash
layouts without spawning the real backend.

`bun run smoke` is an optional real-PTY check. It starts `src/index.tsx` under a Bun
PTY, waits for the backend handshake, runs `shuffle`, verifies the message appears,
then quits with `Ctrl+Q`. It is intentionally not part of the normal test run because
it needs a terminal.

## Packaging notes

The default launcher requires Bun on the machine. The frontend bundle in `ui/dist`
can be produced with `bun run build`; it still runs under Bun and still spawns the C#
bridge. The OpenTUI native package (`@opentui/core-win32-x64` on Windows) and the
tree-sitter assets are resolved at runtime, so ship `ui/node_modules` with `ui/dist`.
A future installer can ship both the .NET publish output and a Bun runtime.
