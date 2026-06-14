# PopMark

A small terminal music queue for YouTube videos and playlists.

PopMark expands links with `yt-dlp`, streams audio through `mpv`, and keeps your queue available between launches.

![PopMark cover art](PopMark/Files/Popmark%20Cover.png)



## Features

- Add YouTube videos or playlists from the terminal
- Stream through `mpv` without downloading music files
- Restore the previous queue on launch
- Arrow-key seeking, clickable playlist navigation, pause, resume, and clear the playlist
- Install local playback tools without admin access

## Demo

![PopMark demo](PopMark/Files/Popmark.gif)

## Requirements

- .NET 9 SDK
- `yt-dlp`
- `mpv`

PopMark asks to install missing playback tools locally on first interactive run.

Local tools are stored in `%LOCALAPPDATA%\PopMark\tools`.
Queue state is stored in `%LOCALAPPDATA%\PopMark\queue.json`.

## Run

```powershell
dotnet run --project .\PopMark\PopMark.csproj
```

Start with a URL:

```powershell
dotnet run --project .\PopMark\PopMark.csproj -- "https://www.youtube.com/watch?v=..."
```

Run a short non-interactive playback test:

```powershell
dotnet run --project .\PopMark\PopMark.csproj -- play-test "https://www.youtube.com/watch?v=..." --seconds 15
```

Check the app version:

```powershell
dotnet run --project .\PopMark\PopMark.csproj -- --version
```

## Versioning

Published builds use semantic versioning from `PopMark/PopMark.csproj`.
Update `Version`, `AssemblyVersion`, `FileVersion`, and `InformationalVersion` together before publishing a release.

## Stack

  - Language: C#
  - Runtime / Framework: .NET 9
  - Project type: Console app / terminal UI
  - Terminal UI library: Spectre.Console
  - Archive extraction dependency: SharpCompress
  - Playback engine: mpv
  - YouTube metadata / playlist expansion: yt-dlp


## Commands

| Command | Action |
| --- | --- |
| `add <url or search>` | Add a YouTube video, playlist, or search result |
| `play` / `pause` | Toggle playback |
| `goto <#\|title>` | Scroll to a playlist song |
| `shuffle` | Randomize the playlist |
| `version` | Show the current app version |
| `clear playlist` | Stop playback and empty the queue |
| `clear` / `cls` | Redraw the screen |
| `help` | Show typed commands |
| `controls` | Show keyboard and mouse controls |
| `quit` | Stop playback and exit |

## Controls

| Control | Action |
| --- | --- |
| `Space` | Toggle playback when the command field is empty |
| `-` / `=` | Decrease or increase volume by 10% |
| `Left Arrow` / `Right Arrow` | Seek backward or forward by 10 seconds |
| `Up Arrow` / `Down Arrow` / mouse wheel | Scroll the playlist panel by one row |
| `PageUp` / `PageDown` | Scroll the playlist panel faster |
| `Home` / `End` | Jump to the top or bottom of the playlist |
| Click playlist song | Play that song directly |
| Click progress bar | Jump to that timestamp when the terminal supports mouse input |
