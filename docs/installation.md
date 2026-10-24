# Installation

PopMark has no installer, no service, and no admin-only steps.

## Requirements

- **Windows x64** for the released build. The portable zip includes the .NET backend,
  Bun, and the compiled OpenTUI assets, so neither .NET nor Bun needs to be installed.
- **Network access to YouTube** for playback, and to `github.com` / `api.github.com` when
  PopMark installs a tool.
- **.NET 9 SDK and Bun 1.3.14+** only if you build from source.
- **`yt-dlp` and `mpv`** unless you let PopMark install them from the Tools panel.

The source has fallbacks for Unix-style `PATH` and tool directories, but the managed
installer downloads Windows x64 packages. Treat the released build as Windows-only.

## Portable build

1. Open [Releases](https://github.com/markdanielaguinaldo/PopMark/releases) and download
   `PopMark-1.1.2-OpenTUI-win-x64-portable.zip`.
2. Extract it anywhere you can write, such as `%LOCALAPPDATA%\Programs\PopMark`.
3. Run `PopMark.exe`.

Keep `PopMark.exe`, `bun.exe`, and the `ui` folder together. The release is not
code-signed, so Windows SmartScreen may ask for confirmation the first time.

Because nothing is written next to the executable, you can move or delete the extracted
folder at any time. Your queue and tools live under `%LOCALAPPDATA%\PopMark` and are not
affected.

## Building from source

On a fresh Windows x64 machine, run the bootstrap:

```powershell
git clone https://github.com/markdanielaguinaldo/PopMark.git
cd PopMark
.\install.cmd
```

It installs the .NET 9 SDK and Bun if needed, runs `bun install`, builds the portable
package, installs managed `yt-dlp` and `mpv` when missing, and installs the `popmark`
command. Use `.\install.cmd /nopause -SkipPlaybackTools` for unattended setup.

If you already have the .NET 9 SDK and Bun:

```powershell
git clone https://github.com/markdanielaguinaldo/PopMark.git
cd PopMark
cd ui
bun install
cd ..
dotnet run --project .\PopMark\PopMark.csproj
```

For a release build:

```powershell
.\scripts\build-portable.ps1
```

`build-portable.ps1` also runs `bun install` before building, so a fresh clone does not
need a separate frontend dependency step.

The backend build output lands under `PopMark\bin\Release\net9.0\`; the frontend bundle
lands under `ui\dist\`. The default launcher starts the OpenTUI frontend automatically.
Use `--legacy-ui` only to inspect the retired C# interface; the OpenTUI frontend is the
default and supported UI.

## First run

When a playback tool is missing, the Tools panel shows `missing`. Open it with
<kbd>F2</kbd> and choose `REPAIR ALL`, or run `tools install` in the command field.
Downloads:

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
