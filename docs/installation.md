# Installation

PopMark is a single console application. It has no installer, no service, and no
admin-only steps.

## Requirements

- **Windows x64** for the released build. The zip is self-contained, so the .NET runtime
  does not need to be installed.
- **Network access to YouTube** for playback, and to `github.com` / `api.github.com` when
  PopMark installs a tool.
- **.NET 9 SDK** only if you build from source.
- **`yt-dlp` and `mpv`** unless you let PopMark install them. It offers to do this on
  first launch.

The source has fallbacks for Unix-style `PATH` and tool directories, but the managed
installer downloads Windows x64 packages. Treat the released build as Windows-only.

## Released build

1. Open [Releases](https://github.com/markdanielaguinaldo/PopMark/releases) and download
   `PopMark-v<version>-win-x64.zip`.
2. Extract it anywhere you can write, such as `%LOCALAPPDATA%\Programs\PopMark`.
3. Run `PopMark.exe`.

The release is a self-contained single-file build; there is nothing else to install. It
is not code-signed, so Windows SmartScreen may ask for confirmation the first time.

Because nothing is written next to the executable, you can move or delete the extracted
folder at any time. Your queue and tools live under `%LOCALAPPDATA%\PopMark` and are not
affected.

## Building from source

Install the [.NET 9 SDK](https://dotnet.microsoft.com/download), then:

```powershell
git clone https://github.com/markdanielaguinaldo/PopMark.git
cd PopMark
dotnet run --project .\PopMark\PopMark.csproj
```

For a release build:

```powershell
dotnet build .\PopMark.sln --configuration Release
```

The build output lands under `PopMark\bin\Release\net9.0\`.

## First run

When a playback tool is missing, PopMark asks before installing anything — on first
launch, or the first time you add something:

```text
Install missing playback tool(s) into C:\Users\<you>\AppData\Local\PopMark\tools: yt-dlp, mpv?
```

Accepting downloads:

- `yt-dlp.exe` from the latest [yt-dlp release](https://github.com/yt-dlp/yt-dlp/releases).
- A portable Windows x64 build of `mpv` from
  [shinchiro/mpv-winbuild-cmake](https://github.com/shinchiro/mpv-winbuild-cmake/releases),
  extracted automatically.

Both go under `%LOCALAPPDATA%\PopMark\tools`, and PopMark pins them so a broken system
install cannot take priority later. No administrator rights are used.

If you decline, PopMark still starts. Install the tools yourself and point PopMark at
them with `tools set`, or use `tools install` later. See
[Configuration](configuration.md#playback-tools).

## Upgrading

Replace the extracted program folder with the new release. The queue, pinned tool paths,
and locally installed tools are unaffected because they live under
`%LOCALAPPDATA%\PopMark`.

A newer PopMark can read a queue written by an older one; the state file is plain JSON.
If a future release changes the format, the old file is ignored rather than crashing the
app.

## Uninstalling

1. Delete the folder you extracted PopMark into.
2. Optionally delete `%LOCALAPPDATA%\PopMark` to remove the saved queue, pinned paths, and
   locally installed tools.

No registry entries, services, or startup tasks are created.

## Next steps

- [Configuration](configuration.md) — tool discovery, state files, volume.
- [User Guide](user-guide.md) — commands and controls.
- [Troubleshooting](troubleshooting.md) — when a tool install or a track fails.
