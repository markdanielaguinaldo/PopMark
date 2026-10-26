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
duration; playing that track fills it in from `mpv`.

You can also pass a URL at launch:

```powershell
PopMark.exe "https://www.youtube.com/watch?v=..."
```

## Views and input modes

| View / mode | What it does |
| --- | --- |
| **Command Center** | Now playing, transport, volume, queue, tools, and shortcuts. |
| **Focus View** | Large logo, waveform, current track, progress, and playback controls. |
| **Command** | The bottom input owns printable text, Enter, history, and shortcuts. |
| **Navigation** | The input blurs; printable keys are ignored and navigation keys work. |
| **Modal** | Help, tools, and diagnostics own the keyboard until closed. |

<kbd>F3</kbd> opens Focus View. <kbd>Tab</kbd> cycles command → navigation → focus.
<kbd>Esc</kbd> returns one level. Only <kbd>Ctrl</kbd>+<kbd>Q</kbd> quits.

Focus View is not a command input: typing ordinary characters there does nothing. It
supports playback and navigation shortcuts, and its progress bar is draggable.

## Themes

Press <kbd>F5</kbd> to cycle color schemes and <kbd>Shift</kbd>+<kbd>F5</kbd> to go
back. A small toast names the new theme, and the choice is saved for the next launch.

| Theme | Palette |
| --- | --- |
| **Midnight** | teal, pink, deep navy |
| **Ember** | amber, coral, warm charcoal |
| **Sakura** | blossom pink, violet, plum |
| **Evergreen** | moss, mint, forest black |
| **Mono** | silver on graphite |
| **Ruby** | scarlet, rose, inked crimson |
| **Cascade** | gold, honey, dusky amber |
| **Random** | picks one of the palettes above each time it is selected |

The waveform, borders, queue selection, and transport controls all follow the active
theme. Set `POPMARK_THEME` to one of the theme IDs to choose the startup theme without
changing the saved value. `random` picks a fresh palette on every launch.

## Commands

Type `help` or press <kbd>F1</kbd> for the in-app command reference.

| Command | Action |
| --- | --- |
| `add <url or search>` | Add a video, playlist, or search result. |
| `play` / `pause` | Toggle playback. |
| `next` / `previous` | Move one track forward or back. |
| `goto <# or title>` | Focus a queue row by number or partial title. |
| `shuffle` | Shuffle the upcoming tracks. The current track stays put. |
| `stop` | Stop playback and clear the queue. |
| `clear playlist` | Stop playback and empty the queue. |
| `tools` | Open the tool management dialog. |
| `tools install [yt-dlp\|mpv\|all]` | Install or repair a managed copy. |
| `tools set <tool> <path>` | Pin an existing executable. |
| `tools clear <tool>` | Remove a pinned path. |
| `logs` | Open diagnostics. |
| `version` | Show the app version. |
| `quit` | Stop playback, keep the queue, and exit. |

## Controls

| Control | Action |
| --- | --- |
| `/` or click the input | Focus the command field |
| <kbd>Tab</kbd> | Cycle command → navigation → focus view |
| <kbd>Esc</kbd> | Leave input, close dialog, or return from Focus View |
| <kbd>Space</kbd> | Play/pause outside command entry |
| <kbd>n</kbd> / <kbd>p</kbd> | Next / previous track |
| <kbd>↑</kbd> / <kbd>↓</kbd> | Move the queue selection |
| <kbd>Enter</kbd> | Play the selected row outside command entry |
| <kbd>Ctrl</kbd>+<kbd>P</kbd> | Toggle playback from anywhere |
| <kbd>Ctrl</kbd>+<kbd>N</kbd> / <kbd>Ctrl</kbd>+<kbd>B</kbd> | Next / previous track |
| <kbd>Ctrl</kbd>+<kbd>←</kbd> / <kbd>Ctrl</kbd>+<kbd>→</kbd> | Seek ±10 seconds |
| <kbd>Ctrl</kbd>+<kbd>↑</kbd> / <kbd>Ctrl</kbd>+<kbd>↓</kbd> | Volume ±10% |
| <kbd>Alt</kbd>+<kbd>←</kbd> / <kbd>Alt</kbd>+<kbd>→</kbd> | Seek ±10 seconds |
| <kbd>F1</kbd> | Command reference |
| <kbd>F2</kbd> | Playback tools |
| <kbd>F3</kbd> | Focus View |
| <kbd>F4</kbd> | Diagnostics |
| <kbd>F5</kbd> | Next color theme |
| <kbd>Shift</kbd>+<kbd>F5</kbd> | Previous color theme |
| <kbd>Ctrl</kbd>+<kbd>Q</kbd> | Quit |

### Mouse

When the terminal supports mouse reporting:

- Clicking a queue row starts that track.
- Press and drag the progress bar to seek; the timestamp updates while dragging.
- Press and drag the volume slider to change volume.
- The mouse wheel scrolls the queue.
- Queue, tool, and dialog buttons are clickable.

Mouse support depends on the terminal. Windows Terminal, Windows Console Host, and most
modern terminals forward the events; some minimal consoles do not.

## Playback behaviour

- A track that ends advances to the next one automatically.
- The displayed position is read from `mpv` while playing; pause freezes it and resume
  continues from the same point.
- Seeking updates the bar immediately and commits to `mpv` during/after the drag.
- A track that fails within ten seconds of starting **stops** the queue and reports the
  reason instead of skipping through every remaining song.
- Volume is applied to the running player immediately and is remembered for the next
  launch.

## Leaving and coming back

`quit` stops playback but saves the queue. On the next launch, the current track and the
rest of the queue are restored, stopped and ready to play. `stop` and `clear playlist`
empty it instead.

For what is stored and how restore behaves when a URL is passed at launch, see
[Configuration](configuration.md#queue-restore).

## When something goes wrong

The status line in Now Playing carries the last message, including the reason a track
would not play. Press <kbd>F4</kbd> to inspect tool reports and backend stderr.

For a message such as an HTTP 403, run the non-interactive test:

```powershell
PopMark.exe play-test "https://www.youtube.com/watch?v=..." --seconds 10
```

It prints whatever `mpv` reported and exits with a meaningful code. See
[Troubleshooting](troubleshooting.md#play-test-exit-codes).
