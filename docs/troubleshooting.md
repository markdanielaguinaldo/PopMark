# Troubleshooting

Start with `tools`. It reports where each playback tool was found, whether it actually
runs, and whether the path was pinned:

```text
yt-dlp: C:\Users\<you>\AppData\Local\PopMark\tools\yt-dlp\yt-dlp.exe [ok] (pinned)
mpv: C:\Program Files\mpv\mpv.exe [ok]
```

A tool marked `NOT FOUND` or `found but will not run` is the cause of most playback
problems.

## Missing or broken tools

Run `tools install` to download both tools into `%LOCALAPPDATA%\PopMark\tools` and pin
them. This is also the repair path: it does not reuse an existing copy.

If the download fails:

- Confirm the machine can reach `github.com` and `api.github.com`. A corporate proxy or
  firewall can block the release download even when YouTube works.
- Install `yt-dlp` and `mpv` yourself, then pin them:

  ```text
  tools set yt-dlp "C:\path\to\yt-dlp.exe"
  tools set mpv "C:\path\to\mpv.exe"
  ```

- Point at the executable, not its folder. `tools set` refuses a path that does not
  exist or does not run, so an unfinished download is rejected.

## `youtube-dl failed: not found or not enough permissions`

This message comes from `mpv`, not PopMark. `mpv` runs `yt-dlp` by name on `PATH` and
could not find it. PopMark normally prevents this by passing the resolved path and adding
its folder to the child `PATH`; seeing the message means the resolution failed or the
configured path is unreadable.

1. Run `tools` and confirm `yt-dlp` is `ok`.
2. If it is pinned, clear the pin and reinstall:

   ```text
   tools clear yt-dlp
   tools install yt-dlp
   ```

3. Retry playback. If it still appears, run `play-test` and read the full error.

## HTTP 403, `Signature extraction failed`, or `nsig extraction failed`

YouTube rejected the request because the installed `yt-dlp` is out of date. Update it:

```text
tools install yt-dlp
```

That fetches the current release and pins it. Restart playback afterwards. A 403 that
survives the update is usually a temporary YouTube-side change; waiting for the next
`yt-dlp` release is the fix.

## Playback stops immediately

PopMark deliberately stops instead of skipping when a track dies within ten seconds. The
status line reads:

```text
Playback failed: <the reason>. Run 'tools' to check yt-dlp and mpv.
```

Reproduce it outside the UI for the full message:

```powershell
PopMark.exe play-test "https://www.youtube.com/watch?v=..." --seconds 10
```

If that reports a 403 or a signature error, update `yt-dlp`. If it reports that a tool is
missing, run `tools install`.

## `mpv could not be started from ...`

The resolved `mpv` path exists but Windows refused to launch it. Common causes:

- A pinned path that has since been moved, deleted, or quarantined. Run `tools clear mpv`,
  then `tools install mpv`.
- A zero-length WinGet execution alias picked up by a manual pin. Point at the real
  `mpv.exe` instead.
- Antivirus blocking the real binary at launch. Check the quarantined-items list, then
  reinstall.

## There is no sound

- Check the volume in PopMark with <kbd>=</kbd>, and in the Windows volume mixer.
- PopMark allows up to 130%; values above 100% are a boost, not a separate output.
- Confirm another `mpv` or browser tab is not holding an exclusive audio device.
- `mpv` runs with video disabled. That is expected, not a broken install.

## The queue was not restored

- PopMark restores the queue only when it starts **without** a URL. Passing a URL
  replaces the restore for that launch.
- `quit` keeps the queue; `stop` and `clear playlist` do not.
- If `%LOCALAPPDATA%\PopMark\queue.json` is missing or unreadable, PopMark starts with an
  empty queue rather than failing. Check that the file exists and contains valid JSON.
- A restored queue starts stopped. Press <kbd>Space</kbd> to resume.

## A previous session left `mpv` running

On startup PopMark terminates `mpv` processes recorded in `sessions.json`, then scans for
any `mpv.exe` carrying a `--input-ipc-server=*popmark-*` argument. If an orphan survives:

1. Close PopMark.
2. End the stray `mpv.exe` in Task Manager.
3. Delete `%LOCALAPPDATA%\PopMark\sessions.json`.
4. Start PopMark again.

A normal `mpv` you launched yourself is not matched by that scan and is left alone.

## The screen is garbled or flickers

PopMark draws a full-screen terminal UI with cursor positioning and mouse reporting.
Use a terminal with ANSI support — Windows Terminal is the safest choice — and avoid
very small window sizes.

- <kbd>Ctrl</kbd>+<kbd>L</kbd> or `cls` redraws the screen.
- Resizing the window triggers a redraw.
- If the UI stays broken after a resize, quit and restart. The saved queue is not lost.

Running inside an IDE's embedded console, a redirected pipe, or a terminal that strips
escape sequences will produce a poor result; use a real console window.

## Mouse clicks do nothing

Mouse reporting depends on the terminal. Confirm the terminal forwards mouse events, and
that the app is not running under a multiplexer that swallows them. Keyboard controls
always work.

## `play-test` exit codes

| Code | Meaning |
| --- | --- |
| `0` | Playback was still healthy when the test ended. |
| `1` | The URL could not be loaded, no track was produced, or an unexpected error occurred. |
| `2` | A playback tool was still missing after the add attempt. |
| `3` | The track stopped, including when it simply ended before the test did. The printed message says why. |

The default test length is 15 seconds; `--seconds N` accepts 1 to 3600. Add
`--install-deps` to let the test install missing tools without prompting.

## Build failures

- Use the .NET **9** SDK. A newer SDK can usually build the project, but the released
  runtime target is `net9.0`.
- If restore fails, check NuGet access; the project depends on `Spectre.Console` and
  `SharpCompress`.
- CI builds the solution in Release on `windows-latest`; a local
  `dotnet build .\PopMark.sln --configuration Release` should reproduce it.

## Known limitations

- Audio only. PopMark passes `--no-video` to `mpv`.
- YouTube only. Other sites that `yt-dlp` supports may work, but are not tested.
- No downloads and no offline library; playback needs a connection.
- Playlists are read with flat metadata, so titles and durations can be incomplete until
  a track starts.
- There is no per-track retry command. Re-add the URL, or use `play-test` to diagnose it.
- Released builds are Windows x64 only.
