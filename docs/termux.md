# PopMark on Android (Termux)

PopMark plays audio only, so a phone is a natural fit. Android cannot run the
released build directly, though: Termux uses Android's Bionic C library while the
.NET runtime and Bun (the OpenTUI frontend) require glibc.

The supported setup therefore runs PopMark inside a small **Debian proot distro**
managed by Termux. Termux stays the terminal and the audio server; everything else
runs in Debian. No root access is required.

```text
Termux
├── PulseAudio  (TCP 127.0.0.1:4713)
└── proot-distro Debian
    └── PopMark  (.NET backend + Bun/OpenTUI + bundled yt-dlp + mpv)
```

## What you need

- An **arm64** Android device (most phones from 2018 onward), Android 8 or newer.
- **Termux from [F-Droid](https://f-droid.org/packages/com.termux/) or
  [GitHub](https://github.com/termux/termux-app/releases)**. The Play Store build
  is unmaintained and lacks `proot-distro`.
- About **2 GB of free storage** for Debian, .NET, Bun, and mpv.
- Wi-Fi for the first install.
- A desktop machine is recommended for the build, but you can also build inside
  the proot distro (see [Building on the phone](#building-on-the-phone)).

## 1. Get the ARM64 package

### From GitHub Releases (no desktop needed)

Every tagged or manual run of the **Release for Android (Termux)** workflow
attaches the package to a release. Download it on the phone and hand the URL to
the installer in one step:

```bash
cd ~/PopMark
bash scripts/termux-install.sh \
  https://github.com/markdanielaguinaldo/PopMark/releases/download/termux-1.1.2/PopMark-1.1.2-OpenTUI-linux-arm64-portable.tar.gz
```

The installer downloads the archive itself, so nothing needs to be copied from a
computer. Skip to [step 3](#3-install-from-termux); the download *is* the archive.
To trigger a build, run the workflow from the repository's **Actions** tab or push
a tag:

```bash
git tag termux-1.1.2
git push origin termux-1.1.2
```

### Build it yourself

On a machine with the .NET 9 SDK and Bun:

```powershell
cd PopMark
.\scripts\build-linux.ps1 -Runtime linux-arm64
```

The script cross-publishes the backend and produces:

```text
releases/PopMark-1.1.2-OpenTUI-linux-arm64-portable.tar.gz
```

The archive already contains the .NET backend, the Bun runtime, the ARM64 OpenTUI
native library, and `yt-dlp`. `mpv` is installed from Debian's package repository
during setup, because there is no trustworthy portable ARM64 build to unpack.

On Linux or inside proot, the equivalent is:

```bash
./scripts/build-linux.sh 1.1.2 linux-arm64
```

## 2. Copy the archive to the phone

Skip this if you passed a release URL to the installer above; it downloads the
archive directly.

Pick whichever is easiest:

- **USB / MTP:** copy the `.tar.gz` into the phone's `Download` folder.
- **adb:** `adb push PopMark-1.1.2-OpenTUI-linux-arm64-portable.tar.gz /sdcard/Download/`
- **Direct download:** open the release URL in the phone browser.

Then grant Termux access to shared storage:

```bash
termux-setup-storage
```

## 3. Install from Termux

```bash
pkg update
pkg install -y proot-distro pulseaudio
```

Get the repository scripts onto the phone (clone the repo, or copy
`scripts/termux-install.sh` into your Termux home), then run:

```bash
bash scripts/termux-install.sh ~/storage/downloads/PopMark-1.1.2-OpenTUI-linux-arm64-portable.tar.gz
```

With no path argument the script looks for the newest matching archive in
`~/storage/downloads` and `~`.

The installer:

1. installs `proot-distro`, `pulseaudio`, and `tar`;
2. creates the Debian distro if it does not exist;
3. installs `mpv` inside Debian;
4. unpacks PopMark into `/opt/popmark` inside the distro;
5. writes `popmark` into `$PREFIX/bin`, plus an in-distro launcher that points
   mpv at Termux's PulseAudio server.

## 4. Run it

```bash
popmark
```

The wrapper starts PulseAudio, takes a wake lock, and enters Debian. First launch
takes a few seconds; later launches are faster because the proot image is warm.

Optional arguments work the same as on desktop:

```bash
popmark --version
popmark "https://www.youtube.com/watch?v=..."
popmark tools
```

## Controls on a phone

Termux sends touch events as mouse clicks, so the whole interface is usable
without a keyboard:

- **Tap the prompt** (`›`) at the bottom to type a URL, a search, or a command.
- **Tap a queue row** to play that track.
- **Tap or drag the progress bar** to seek, and the **volume slider** to set volume.
- **Tap the footer buttons** on narrow screens: `HELP`, `TOOLS`, `FOCUS`, `LOGS`,
  `THEME`, and `QUIT`. The F-keys exist for hardware keyboards but are not needed.
- **In Focus View**, tap `RETURN`, `PLAY`/`PAUSE`, `THEME` to leave, toggle
  playback, or cycle the theme without touching the keyboard.

The transport row (`PREV`, `PLAY`/`PAUSE`, `NEXT`, `STOP`, `SHUFFLE`) is always
touchable. Below 60 columns the layout switches to a phone mode: the footer splits
into two button rows, the transport and volume stack, and decorative rows are
dropped so nothing is clipped.

### Termux shortcut keys

The installer adds a two-row key strip above the keyboard, so these keys are one
tap away even with the soft keyboard closed:

```properties
extra-keys = [['ESC','TAB','CTRL','ALT','LEFT','UP','RIGHT'],['F1','F2','F3','F4','F5','SPACE','ENTER']]
```

It appends the setting to `~/.termux/termux.properties` only when no `extra-keys`
line exists (an existing one is left alone, and a backup is written first). Use
`termux-reload-settings` to apply it without restarting.

`F3` leaves Focus View, `F5` cycles themes, and the arrow/space keys drive
playback and seeking — all without opening the keyboard. Delete the
`extra-keys` line to remove the strip.

## Audio

`mpv` runs inside Debian and has no direct access to Android's audio device. The
Termux-side wrapper starts PulseAudio and publishes it on
`tcp:127.0.0.1:4713`; the in-distro launcher sets `PULSE_SERVER` and
`POPMARK_MPV_AO=pulse`.

Check the bridge if there is no sound:

```bash
pactl info
pactl list short sinks
```

Start it manually if needed:

```bash
pulseaudio --kill
pulseaudio --start --exit-idle-time=-1
pactl load-module module-native-protocol-tcp auth-anonymous=1 port=4713
```

Volume still works normally inside PopMark (0–130%) and with the phone's
hardware keys.

## Keeping playback alive

Android aggressively freezes background processes. For reliable playback:

1. Keep the Termux notification visible. The wrapper holds a wake lock while
   PopMark runs.
2. Disable battery optimisation for Termux in **Settings → Apps → Termux →
   Battery → Unrestricted**.
3. On Android 12+, enable **Developer options → Disable child process
   restrictions** if playback stops as soon as the screen locks.

## Updating

- **PopMark:** build a newer ARM64 archive and rerun
  `scripts/termux-install.sh`. The queue, theme, and tool pins live under
  `/root/.local/share/PopMark` inside the distro and are preserved.
- **yt-dlp:** run `popmark`, then type `tools install yt-dlp`. The bundled copy is
  replaced and pinned.
- **mpv:** `proot-distro login debian -- apt-get update && proot-distro login debian -- apt-get install -y mpv`.

## Uninstalling

```bash
rm "$PREFIX/bin/popmark"
proot-distro remove debian
pkg uninstall pulseaudio proot-distro
```

Deleting the distro removes the queue and settings too.

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| `popmark: command not found` | `pkg install termux-tools`, then open a new Termux session; `$PREFIX/bin` must be on `PATH`. |
| `Could not find the PopMark backend` | The archive was extracted without its `ui` folder or `PopMark` binary. Rerun the installer with a fresh archive. |
| `mpv: NOT FOUND` | `proot-distro login debian -- apt-get install -y mpv` |
| No audio, but the track advances | Check `pactl info`; restart PulseAudio as shown above. |
| `GC heap initialization failed (0x8007000E)` | Proot cannot satisfy the GC's default virtual reservation. The launcher already sets `DOTNET_GCHeapHardLimit=0x10000000`; if it still fails, raise it (`0x20000000`) or free RAM. |
| Playback stops when the screen locks | Keep Termux awake, disable battery optimisation, enable the child-process setting above. |
| `Failed to resolve host` / 403 from YouTube | Update yt-dlp with `tools install yt-dlp`. |
| Interface looks torn or oversized | Use Termux at a smaller font size, or turn the phone to landscape for the full sidebar. |
| Taps in Focus View do nothing | Confirm the installed bundle is current: `grep -c focus-return "$PREFIX/var/lib/proot-distro/containers/debian/rootfs/opt/popmark/ui/dist/index.js"` must print at least 1. Otherwise use the `F3`/`F5` keys from the Termux key strip. |
| Very slow startup | proot has overhead. The first launch after a reboot is slowest; later ones reuse the page cache. |

## Building on the phone

If you do not have a desktop machine, build inside the proot distro:

```bash
proot-distro login debian
apt-get update
apt-get install -y curl unzip git mpv ca-certificates
curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
bash /tmp/dotnet-install.sh --channel 9.0
export PATH="$HOME/.dotnet:$PATH"
curl -fsSL https://bun.sh/install | bash
export PATH="$HOME/.bun/bin:$PATH"
git clone https://github.com/markdanielaguinaldo/PopMark.git ~/PopMark
cd ~/PopMark
./scripts/build-linux.sh 1.1.2 linux-arm64
```

The build needs roughly 1.5 GB of extra space while it runs. Once the archive is
created, follow [step 3](#3-install-from-termux) or simply unpack it into
`/opt/popmark` and use the distro launcher.

## Why not native Termux?

- The .NET runtime has no Android/Bionic build. `proot-distro` supplies a glibc
  userspace, so the official `linux-arm64` runtime works unmodified.
- Bun has no Android/Bionic build either, and OpenTUI's native core ships
  glibc/musl `linux-arm64` binaries only.
- Rewriting the backend and UI to run on Bionic would be a different project.

proot adds a little CPU overhead, but PopMark is audio-only, so playback and the
UI stay smooth on ordinary phones.
