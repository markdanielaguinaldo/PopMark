# PopMark

A small terminal music queue for YouTube videos and playlists.

PopMark expands links with `yt-dlp`, streams audio through `mpv`, and keeps your queue available between launches.

![PopMark cover art](PopMark/Files/Popmark%20Cover.png)



## Features

- Add YouTube videos or playlists from the terminal
- Stream through `mpv` without downloading music files
- Restore the previous queue on launch
- Toggle full and mini player views
- Seek, skip, pause, resume, and clear the playlist
- Install local playback tools without admin access

## Demo

![PopMark demo](PopMark/Files/Popmark.gif)

## Requirements

- .NET 9 SDK
- `yt-dlp`
- `mpv`

PopMark can install `yt-dlp` and portable `mpv` locally:

```powershell
dotnet run --project .\PopMark\PopMark.csproj -- deps
```

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

## Commands

| Command | Action |
| --- | --- |
| `add <url>` | Add a YouTube video or playlist |
| `play` / `pause` | Toggle playback |
| `next` | Skip to the next track |
| `p <seconds>` | Seek relative to the current position, for example `p 30` or `p -30` |
| `clear playlist` | Stop playback and empty the queue |
| `mini` | Toggle compact player mode |
| `cls` / `clear` | Redraw the terminal UI |
| `quit` | Stop playback and exit |
