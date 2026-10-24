# User Guide

## Adding music

Type `add` followed by a YouTube URL, or by anything you would type into YouTube search:

```text
add https://www.youtube.com/watch?v=...
add https://www.youtube.com/playlist?list=...
add https://music.youtube.com/watch?v=...
add daft punk instant crush
```

A URL can also be typed on its own; PopMark treats it as `add`. Searches become a
single-result YouTube search, so `add lofi hip hop` queues the top match rather than a
list of results.

Playlists are expanded into individual tracks. PopMark asks `yt-dlp` for flat playlist
metadata, which is fast but sometimes returns an entry with a placeholder title or no
duration; playing that track fills it in.

You can also pass a URL at launch:

```powershell
PopMark.exe "https://www.youtube.com/watch?v=..."
```

## The two views

| View | What it shows |
| --- | --- |
| **Playlist** | Now playing, progress, volume, and the full queue. This is the default. |
| **Splash** | The PopMark splash art, the current title, progress, and volume, with the queue hidden. |

<kbd>Tab</kbd> switches between them, as do the `playlist` and `splash` commands. The
splash view also has a clickable progress bar and volume slider.

Starting without a URL shows the animated splash once; press any key to continue. Pass a
URL at launch to skip it.

## Commands

Type `help` (or <kbd>?</kbd> with an empty input) to show the short list, and `controls`
for the keyboard reference.

| Command | Action |
| --- | --- |
| `add <url or search>` | Add a video, playlist, or search result. |
| `play` / `pause` | Toggle playback. `r` is an alias. |
| `next` / `previous` | Move one track forward or back. Aliases: `n`, `]`, `skip` / `prev`, `back`, `[`. |
| `goto <# or title>` | Scroll the queue to a track, by number or partial title. Aliases: `jump`, `go to`, `song`. |
| `shuffle` | Shuffle the upcoming tracks. The current track stays put. |
| `stop` | Stop playback and clear the queue. Alias: `s`. |
| `clear playlist` | Stop playback and empty the queue. Alias: `clear-playlist`. |
| `clear` / `cls` | Redraw the screen without touching the queue. |
| `splash` | Switch to the splash view. Aliases: `art`, `visual`, `visualizer`. |
| `playlist` | Switch to the queue view. Aliases: `queue`, `menu`. |
| `tools` | Show, install, pin, or clear `yt-dlp` and `mpv`. Alias: `doctor`. |
| `version` | Show the app version. |
| `help` / `controls` | Show commands or keyboard controls. |
| `quit` | Stop playback, keep the queue, and exit. Aliases: `exit`, `q`. |

`goto` only moves the visible queue; it does not start playback. Use the number shown in
the queue, or part of a title.

## Controls

| Control | Action |
| --- | --- |
| <kbd>Space</kbd> | Play or pause when the command field is empty. |
| <kbd>-</kbd> / <kbd>=</kbd> | Volume down or up by 10%. |
| <kbd>Left</kbd> / <kbd>Right</kbd> | Seek backward or forward 10 seconds. |
| <kbd>[</kbd> / <kbd>]</kbd> | Previous or next track. |
| <kbd>Up</kbd> / <kbd>Down</kbd> | Scroll the queue one row. |
| <kbd>PageUp</kbd> / <kbd>PageDown</kbd> | Scroll the queue five rows. |
| <kbd>Home</kbd> / <kbd>End</kbd> | Jump to the top or bottom of the queue. |
| <kbd>Tab</kbd> | Switch between the playlist and splash views. |
| <kbd>Ctrl</kbd>+<kbd>L</kbd> | Clear and redraw the screen. |
| <kbd>Esc</kbd> | Clear whatever is typed in the command field. |
| <kbd>Enter</kbd> | Run the typed command. |

Space, volume, track-skip, and scroll keys only act while the command field is empty.
Typing `-`, `=`, `[`, or `]` into a command never changes the volume or skips a track.

### Mouse

When the terminal supports mouse reporting:

- Clicking a queue row starts that track.
- Clicking the progress bar seeks to that point.
- Clicking the volume slider sets the volume.
- In the queue view the wheel scrolls three rows, unless the pointer is over the volume
  slider, where it adjusts the volume. In the splash view the wheel adjusts the volume.

Mouse support depends on the terminal. Windows Terminal, Windows Console Host, and most
modern terminals forward the events; some minimal consoles do not.

## Playback behaviour

- A track that ends advances to the next one automatically.
- A track that fails within ten seconds of starting **stops** the queue and prints the
  reason instead of skipping through every remaining song. That window exists because
  `mpv` has to reach the network through `yt-dlp` before it can fail.
- Volume is applied to the running player immediately and is remembered for the next
  launch.
- Seeking is relative to the current position, and the displayed progress is clamped to
  the track's duration.

## Leaving and coming back

`quit` stops playback but saves the queue. On the next launch, the current track and the
rest of the queue are restored, stopped and ready to play. `stop` and `clear playlist`
empty it instead.

For what is stored and how restore behaves when a URL is passed at launch, see
[Configuration](configuration.md#queue-restore).

## When something goes wrong

The status line under the queue carries the last message, including the reason a track
would not play. For a message such as an HTTP 403, run the non-interactive test:

```powershell
PopMark.exe play-test "https://www.youtube.com/watch?v=..." --seconds 10
```

It prints whatever `mpv` reported and exits with a meaningful code. See
[Troubleshooting](troubleshooting.md#play-test-exit-codes).
