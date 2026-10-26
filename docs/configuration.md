# Configuration

PopMark keeps its settings in a small number of files and environment variables. There is
no settings screen and no configuration file to edit by hand.

## Playback tools

PopMark needs two executables:

| Tool | Used for |
| --- | --- |
| `yt-dlp` | Expanding a video, playlist, or search into playable track URLs, and reading titles and durations. |
| `mpv` | Playing the audio. PopMark drives it over its IPC socket. |

Both must actually run: PopMark validates the resolved executable by starting it with
`--version` and requiring exit code `0`, so a blocked or half-written copy counts as
missing.

### Discovery order

For each tool, PopMark looks in this order:

1. A path pinned in `%LOCALAPPDATA%\PopMark\tools.json`.
2. `POPMARK_YTDLP_PATH` / `POPMARK_MPV_PATH` (or `POPMARK_YTDLP` / `POPMARK_MPV`).
3. PopMark's own copy under `%LOCALAPPDATA%\PopMark\tools\<tool>\`.
4. `PATH`, plus the usual WinGet, Scoop, Chocolatey, and Program Files tool folders.
5. On Windows, `where.exe`, then a bounded scan of WinGet, Scoop, Chocolatey, Program
   Files, and `%LOCALAPPDATA%\Programs` package folders.

Step 5 covers installs that never landed on `PATH`, such as a WinGet package folder like
`%LOCALAPPDATA%\Microsoft\WinGet\Packages\yt-dlp.yt-dlp_Microsoft.Winget.Source_8wekyb3d8bbwe\yt-dlp.exe`.

A `.exe` is preferred over a `.com` beside it. `mpv` ships a small `.com` shim that
relaunches the real binary, and starting the shim would leave PopMark holding the wrapper
instead of the player.

### Inspecting and changing paths

Run `tools` inside the UI, or `PopMark.exe tools` from a shell, to see what was resolved:

```text
yt-dlp: C:\Users\<you>\AppData\Local\PopMark\tools\yt-dlp\yt-dlp.exe [ok] (pinned)
mpv: C:\Program Files\mpv\mpv.exe [ok]
```

| Command | Action |
| --- | --- |
| `tools` | Show the resolved path, whether it runs, and whether it is pinned. |
| `tools install [yt-dlp, mpv, or all]` | Download a private copy into `%LOCALAPPDATA%\PopMark\tools` and pin it. |
| `tools set <tool> <path>` | Pin an executable you already have. The path must exist and run. |
| `tools clear <tool>` | Forget a pin and fall back to discovery. |

`tools install` behaves as a repair: it downloads the current release and pins it, so a
broken or out-of-date system copy cannot keep winning.

### Why `mpv` needs to know where `yt-dlp` is

`mpv` resolves YouTube pages by running `yt-dlp` itself, but it only looks for it by name
on `PATH`. A `yt-dlp` that PopMark found inside a WinGet package folder would therefore
be invisible to `mpv`, which reports:

```text
youtube-dl failed: not found or not enough permissions
```

PopMark avoids that by passing the resolved executable with
`--script-opts=ytdl_hook-ytdl_path=...` and prepending its folder to the `PATH` inherited
by the child process.

## State files

Everything PopMark remembers lives under `%LOCALAPPDATA%\PopMark`:

| File | Contents |
| --- | --- |
| `queue.json` | Current track, previous tracks, upcoming tracks, and volume. |
| `tools.json` | Pinned `yt-dlp` and `mpv` paths. |
| `sessions.json` | Live `mpv` process ids and IPC paths, used for crash cleanup. |
| `theme.json` | Active OpenTUI color theme. |
| `tools\<tool>\` | Tools installed by PopMark. |

Delete a file to reset just that part, or delete the folder to reset everything.

### Queue restore

The queue is written whenever playback state changes. On the next interactive launch,
PopMark restores:

- the track that was current, first in line to play,
- the tracks before and after it,
- the volume.

Restored playback starts **stopped** rather than auto-playing. Press <kbd>Space</kbd> to
resume. Starting PopMark with a URL skips the restore and adds the URL instead, so a
scheduled launch does not pick up yesterday's queue.

`quit` stops playback but keeps the queue for next time. `stop` clears it.

### Stale sessions

If PopMark exits without cleaning up, `mpv` can be left running. On startup PopMark reads
`sessions.json` and terminates anything it recorded, then scans for `mpv.exe` processes
still carrying a `--input-ipc-server=*popmark-*` argument. The message
`Cleared N previous PopMark playback session(s).` means that happened.

Only `mpv` processes launched by PopMark are targeted; a normal `mpv` window is left
alone.

## Volume

Volume runs from `0` to `130`. The extra 30% is a boost for quiet tracks; `mpv`'s own
maximum is raised to match. Each <kbd>-</kbd> or <kbd>=</kbd> press changes it by 10, and
clicking the volume slider sets it directly. The value is saved with the queue.

## Command-line options

| Invocation | Effect |
| --- | --- |
| `PopMark.exe` | Interactive UI, restoring the previous queue. |
| `PopMark.exe "<url>"` | Interactive UI, adding the URL instead of restoring. |
| `PopMark.exe --version` | Print the version and exit. |
| `PopMark.exe tools ...` | Run a tool command without entering the UI. |
| `PopMark.exe play-test "<url>" [--seconds N]` | Non-interactive playback test. |

See the [User Guide](user-guide.md) for the in-app commands and
[Troubleshooting](troubleshooting.md) for the `play-test` exit codes.
