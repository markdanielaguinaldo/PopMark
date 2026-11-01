# User Guide

## Adding music

Type `add` followed by a YouTube URL, or by anything you would type into YouTube search:

```text
add https://www.youtube.com/watch?v=...
add https://www.youtube.com/playlist?list=...
add https://music.youtube.com/watch?v=...
add daft punk instant crush
```

A URL can also be typed on its own; PopMark treats it as `add` and queues it directly.
Search words open a picker with the top matches instead of assuming the first result is
the one you wanted. `search <query>` opens the same picker without going through `add`.

Playlists are expanded into individual tracks. PopMark asks `yt-dlp` for flat playlist
metadata, which is fast but sometimes returns an entry with a placeholder title or no
duration; playing that track fills it in from `mpv`.

## Search results

`add lofi hip hop` and `search lofi hip hop` both ask YouTube for the top eight matches
(up to ten). The picker shows each title, channel, and duration. Use
<kbd>↑</kbd>/<kbd>↓</kbd> or <kbd>j</kbd>/<kbd>k</kbd> to choose a row, then press
<kbd>Enter</kbd> to add it. <kbd>Esc</kbd> closes the picker without changing the queue.
Tracks already in the queue are skipped when the selection is added.

## Queue editing

Only upcoming tracks move. The current track and history stay fixed.

| Action | How |
| --- | --- |
| Select a row | <kbd>↑</kbd>/<kbd>↓</kbd> or click |
| Reorder | Drag an upcoming row, or <kbd>Shift</kbd>+<kbd>↑</kbd>/<kbd>↓</kbd> |
| Play next | <kbd>Shift</kbd>+<kbd>n</kbd> or the `NEXT` button |
| Remove | <kbd>d</kbd>/<kbd>Delete</kbd> or the `REMOVE` button |
| Undo | <kbd>u</kbd> or the `UNDO` button |

Removing the current track stops playback and leaves the rest of the queue intact.
Undo restores the previous queue; if the current track changed, playback restarts
stopped so you can press play when ready. Duplicate YouTube URLs are detected using
canonical video IDs, so short links and `watch?v=` links count as the same track.

## Saved library

Press <kbd>F6</kbd>, or run `library` / `queues` / `favorites`, to browse saved queues and
favorites. A saved queue loads and starts playing; a favorite is added to the current
queue. The library dialog also exports a selected queue as `.m3u` and deletes saved
queues.

| Command | Action |
| --- | --- |
| `save <name>` | Save the current queue under a name (replacing an existing name). |
| `load <name>` | Replace the queue from a saved queue and start playing. |
| `delete-queue <name>` | Delete a saved queue. |
| `favorite` | Toggle the current track in favorites. Press <kbd>f</kbd> to favorite the selected row. |
| `export [file.m3u]` | Export the current queue. |
| `import <file.m3u>` | Replace the queue from an M3U file and start playing. |

Exports default to `%LOCALAPPDATA%\PopMark\exports` (`$XDG_DATA_HOME/PopMark/exports` on
Linux). A relative export path is resolved under that folder; an absolute path is used
as-is. Imports accept YouTube and other `http(s)` track URLs and ignore local file paths.
Saved queues and favorites live in `library.json` beside `queue.json`.

You can also pass a URL at launch:

```powershell
PopMark.exe "https://www.youtube.com/watch?v=..."
```

## Views and input modes

| View / mode | What it does |
| --- | --- |
| **Command Center** | Now playing, transport, volume, queue, tools, and shortcuts. |
| **Focus View** | Large logo, dim twinkling starfield, waveform, current track, progress, and playback controls. |
| **Command** | The bottom input owns printable text, Enter, history, and shortcuts. |
| **Navigation** | The input blurs; printable keys are ignored and navigation keys work. |
| **Modal** | Help, tools, diagnostics, search results, and the library own the keyboard until closed. |

<kbd>F3</kbd> opens Focus View. <kbd>Tab</kbd> cycles command → navigation → focus.
<kbd>Esc</kbd> returns one level. Only <kbd>Ctrl</kbd>+<kbd>Q</kbd> quits.

Focus View is not a command input: typing ordinary characters there does nothing. It
supports playback and navigation shortcuts, and its progress bar is draggable. A sparse
starfield shimmers behind the content, with denser light toward the edges of the screen.

Its controls are also mouse- and touch-friendly: `RETURN`, `PLAY`/`PAUSE`, and `THEME`
(which cycles the theme). Below 60 columns the whole application switches to a phone
layout, so those buttons remain reachable without an F-key row.

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
| `add <url or search>` | Add a video or playlist, or open the search picker. |
| `search <query>` | Open the search picker for a query. |
| `play` / `pause` | Toggle playback. |
| `next` / `previous` | Move one track forward or back. |
| `goto <# or title>` | Focus a queue row by number or partial title. |
| `shuffle` | Shuffle the upcoming tracks. The current track stays put. |
| `undo` | Undo the last queue change. |
| `library` / `queues` / `favorites` | Open saved queues and favorites. |
| `save <name>` | Save the current queue. |
| `load <name>` | Replace the queue with a saved queue and play it. |
| `delete-queue <name>` | Delete a saved queue. |
| `export [file.m3u]` | Export the current queue as an M3U playlist. |
| `import <file.m3u>` | Replace the queue from an M3U playlist and play it. |
| `favorite` | Favorite or unfavorite the current track. |
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
| <kbd>Shift</kbd>+<kbd>↑</kbd> / <kbd>↓</kbd> | Reorder the selected upcoming track |
| <kbd>Shift</kbd>+<kbd>n</kbd> | Move the selected upcoming track to play next |
| <kbd>d</kbd> / <kbd>Delete</kbd> | Remove the selected queue row |
| <kbd>u</kbd> | Undo the last queue change |
| <kbd>f</kbd> | Favorite or unfavorite the selected/current track |
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
| <kbd>F6</kbd> | Saved queues and favorites |
| <kbd>Ctrl</kbd>+<kbd>Q</kbd> | Quit |

### Mouse

When the terminal supports mouse reporting:

- Clicking a queue row starts that track.
- Dragging an upcoming queue row reorders it.
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
