# PopMark

PopMark is a lightweight terminal music queue for YouTube links. Paste a video or playlist URL, let `yt-dlp` expand it, and stream audio through `mpv` without downloading music files locally.

## Requirements

- .NET 9 SDK
- `yt-dlp` available on `PATH`
- `mpv` available on `PATH`

## Run

```powershell
dotnet run --project .\PopMark\PopMark.csproj
```

You can also start by passing a URL:

```powershell
dotnet run --project .\PopMark\PopMark.csproj -- "https://www.youtube.com/watch?v=..."
```

## Commands

- `add <url>` or `a <url>`: load a YouTube video or playlist into the queue
- `pause` / `resume`: control playback
- `next`: skip to the next track
- `stop`: stop playback and clear the queue
- `detach`: exit the TUI while handing the pending queue to mpv
- `quit`: stop playback and exit
