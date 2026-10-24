# Development

This page covers the project layout, the pieces that are easy to break, and how a release
is built.

## Building and running

PopMark targets `net9.0` and depends on `Spectre.Console` for the UI and `SharpCompress`
for extracting the `mpv` archive.

```powershell
dotnet run --project .\PopMark\PopMark.csproj
dotnet build .\PopMark.sln --configuration Release
```

Run a URL without entering the UI:

```powershell
dotnet run --project .\PopMark\PopMark.csproj -- play-test "https://www.youtube.com/watch?v=..." --seconds 15
```

## Project layout

```text
PopMark/Program.cs                      entry point, command loop, key dispatch
PopMark/Services/PlaybackQueue.cs       queue state, next/previous/shuffle, restore
PopMark/Services/MpvPlayer.cs           mpv process, IPC commands, error capture
PopMark/Services/YtDlpService.cs        playlist and search expansion
PopMark/Services/ToolLocator.cs         tool discovery, pins, runnable checks
PopMark/Services/DependencyInstaller.cs downloads yt-dlp and mpv
PopMark/Services/QueueCacheStore.cs     queue.json read/write
PopMark/Services/PlaybackSessionStore.cs stale mpv cleanup
PopMark/Services/PlaybackShutdownGuard.cs stops playback on exit or console close
PopMark/Helpers/Terminal/               frame renderer, input reader, splash, hitboxes
PopMark/Models/                         Track, PlayerSnapshot, PlaybackStatus
```

The terminal layer is split between `TerminalFrameRenderer`, which turns a
`RenderContext` into lines, and `ReactiveInputReader`, which owns the loop that reads keys,
refreshes the screen, and resolves mouse events.

## How playback works

1. `PlaybackQueue.AddUrlAsync` asks `YtDlpService` to expand the URL.
2. `yt-dlp` runs with `--dump-single-json --flat-playlist --ignore-errors --no-warnings`.
   Flat metadata is why a large playlist appears quickly.
3. Tracks are queued. If nothing is playing, the first one starts.
4. `MpvPlayer` launches `mpv` with `--no-video --force-window=no --volume-max=130` and a
   per-process `--input-ipc-server`.
5. Later commands are JSON lines written to that IPC socket: `cycle pause`, `seek`,
   `add volume`, `loadfile ... append-play`, `quit`.
6. When the `mpv` process exits, `PlaybackQueue` advances to the next track.

On Windows the IPC endpoint is a named pipe; elsewhere it is a Unix socket. Commands retry
a short connection race twelve times, 120 ms apart, because `mpv` may not have created the
socket yet.

### Error capture

`mpv` writes diagnostics to **stdout**, not stderr. Both streams are drained concurrently
and their last non-`[cplayer]` lines are kept. Reading only stderr was the 1.1.0 bug that
made failures invisible: the queue treated a dead track as a finished one and skipped the
whole playlist in seconds.

A track that dies within ten seconds is treated as a failed start, not a finished song.
The queue stops and shows the captured reason. The window is ten seconds because `mpv`
has to reach the network through `yt-dlp` before it can fail.

## Tool resolution

`ToolLocator` caches resolutions in-process and invalidates on a pin change. Pinned paths
(`tools.json`), environment variables, and PopMark's managed folder are tried before
`PATH` and the package-manager folders.

Two details are load-bearing:

- **Prefer `.exe` over `.com`.** `mpv` ships a `.com` shim that relaunches the real
  binary. Starting the shim leaves PopMark holding the wrapper, not the player, so the
  process it thinks it owns exits immediately.
- **Hand `yt-dlp` to `mpv` explicitly.** `mpv` resolves YouTube pages by running `yt-dlp`
  by name. PopMark passes `--script-opts=ytdl_hook-ytdl_path=<path>` and prepends the
  folder to the child `PATH`, or a WinGet-only install is invisible to `mpv`.

A candidate must be a non-empty file and must exit `0` for `--version`. A resolved path
that fails that check is dropped from the cache so `tools install` can take over.

## State and restore

`QueueCacheStore` writes `queue.json` on every snapshot change. Restoring puts the current
track at the front of the pending queue and starts stopped, so launching the app does not
immediately make noise. Volume is restored with the queue.

`PlaybackSessionStore` records each launched `mpv` process. On startup it kills recorded
processes that still exist, clears the file, then runs a PowerShell CIM query for
`mpv.exe` processes with a `--input-ipc-server=*popmark-*` argument. That second pass
covers a crash between spawning `mpv` and writing the session file.

`PlaybackShutdownGuard` hooks `ProcessExit` and, on Windows, `SetConsoleCtrlHandler` so
closing the window still stops `mpv` and saves the queue.

## Terminal loop

- The screen refreshes every 110 ms while playing or loading.
- A state signature is checked every 250 ms, and a change triggers an immediate repaint.
- Each frame repaints its lines in place; a full clear only happens on the first paint,
  after a resize, or on an explicit redraw.
- Mouse hitboxes for the queue, progress bar, and volume slider are cached from the last
  render and matched against click coordinates.
- Space, volume, track-skip, and scroll keys only act when the command buffer is empty.
  Do not change that without checking that `-`, `=`, `[`, and `]` still type normally.

## Adding a command

1. Add the case to `ProcessCommandAsync` in `Program.cs`, or to the pre-dispatch handling
   if it needs to run before normal command parsing.
2. Add an alias if the existing commands have one.
3. Add it to `HelpLine` and the README/user guide tables.
4. If it needs a key, map it in `ReactiveInputReader` and keep the empty-buffer rule.

## Tests and CI

There is no automated test project. CI builds the solution in Release on
`windows-latest` with the .NET 9 SDK for every push and pull request. Behavioural checks
are manual:

- `play-test` exercises the whole yt-dlp → mpv path and returns a meaningful exit code.
- `tools` proves each binary resolves and runs.
- `--version` confirms the assembly metadata.

## Versioning and release

The version lives in four places in `PopMark/PopMark.csproj` and must move together:

```xml
<Version>1.1.2</Version>
<AssemblyVersion>1.1.2.0</AssemblyVersion>
<FileVersion>1.1.2.0</FileVersion>
<InformationalVersion>1.1.2</InformationalVersion>
```

A release is a self-contained single-file Windows x64 zip named
`PopMark-v<version>-win-x64.zip`, attached to a GitHub release tagged `v<version>`.
Published versions are currently `v1.1.0`, `v1.1.1`, and `v1.1.2`.

Order matters when publishing: build the zip, create the release with the asset, and only
then point users at it. Users extract over the previous folder; the queue and tools live
outside it under `%LOCALAPPDATA%\PopMark`, so they survive an upgrade.

## Commit messages

The repository installs a `commit-msg` hook from `.githooks/` and expects:

```text
[PopMark] Brief summary of the change
```

`.gitattributes` forces LF on the hook so it remains runnable by `sh`. Use
`git commit --no-verify` only when the hook itself is the thing being changed.
