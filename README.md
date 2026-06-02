# PopMark

PopMark is a lightweight terminal music queue for YouTube links. Paste a video or playlist URL, let `yt-dlp` expand it, and stream audio through `mpv` without downloading music files locally.

## Requirements

- .NET 9 SDK
- `yt-dlp` and `mpv`

PopMark can install `yt-dlp` and portable `mpv` locally without admin access:

```powershell
dotnet run --project .\PopMark\PopMark.csproj -- deps
```

Local tools are stored under `%LOCALAPPDATA%\PopMark\tools`.
Queue state is saved under `%LOCALAPPDATA%\PopMark\queue.json` so PopMark can restore your last queue on the next launch.

## Run

```powershell
dotnet run --project .\PopMark\PopMark.csproj
```

You can also start by passing a URL:

```powershell
dotnet run --project .\PopMark\PopMark.csproj -- "https://www.youtube.com/watch?v=..."
```

Run a non-interactive playback test:

```powershell
dotnet run --project .\PopMark\PopMark.csproj -- play-test "https://www.youtube.com/watch?v=..." --seconds 15
```

## Commands

- `add <url>`: add a YouTube video or playlist
- `play` or `pause`: toggle playback
- `next`: skip to the next track
- `seek <seconds>`: fast forward or rewind, for example `seek 30` or `seek -30`
- `clear playlist`: stop playback and empty the queue
- `cls` or `clear`: clear and redraw the command center
- `mini`: toggle compact player mode
- `quit`: stop playback and exit
