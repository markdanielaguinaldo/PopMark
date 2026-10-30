#!/data/data/com.termux/files/usr/bin/bash
#
# Installs a PopMark linux-arm64 portable build on Android.
#
# Run this inside Termux (not inside the proot distro). It creates a Debian
# proot-distro, because .NET and Bun need glibc and will not start on Android's
# Bionic libc directly.
#
# Usage:
#   scripts/termux-install.sh /path/to/PopMark-1.1.2-OpenTUI-linux-arm64-portable.tar.gz
#
# With no argument, the newest matching tarball in ~/storage/downloads or ~ is used.

set -euo pipefail

say() { printf '\033[1;36m[PopMark]\033[0m %s\n' "$*"; }
die() { printf '\033[1;31m[PopMark]\033[0m %b\n' "$*" >&2; exit 1; }

[ -n "${PREFIX:-}" ] || die "Run this inside Termux. See docs/termux.md."
[ -d /data/data/com.termux/files/usr ] || die "This does not look like Termux."

case "$(uname -m)" in
    aarch64 | arm64) ;;
    *) die "This package is for arm64 phones; $(uname -m) is not supported." ;;
esac

DISTRO="debian"
ROOTFS="$PREFIX/var/lib/proot-distro/installed-rootfs/$DISTRO"
INSTALL_DIR="$ROOTFS/opt/popmark"
TARBALL="${1:-}"

# Let the caller write ~/storage/... instead of the full expanded path.
case "$TARBALL" in
    "~/"*) TARBALL="$HOME/${TARBALL#"~/"}" ;;
esac

# Without an argument, look for the newest archive. ~/storage/downloads is a
# symlink, so find needs -H (follow a symlink starting point) or it descends
# nowhere and reports no match.
SEARCHED=""
if [ -z "$TARBALL" ]; then
    for directory in "$HOME/storage/downloads" "$HOME/storage/shared/Download" "$HOME"; do
        [ -d "$directory" ] || continue
        SEARCHED="$SEARCHED $directory"
        candidate=$(find -H "$directory" -maxdepth 1 -type f -name 'PopMark-*.tar.gz' 2>/dev/null | sort | tail -n 1 || true)
        if [ -n "$candidate" ]; then
            TARBALL="$candidate"
            say "Found $TARBALL"
            break
        fi
    done
fi

if [ -z "$TARBALL" ] || [ ! -f "$TARBALL" ]; then
    die "No PopMark .tar.gz found (looked in:${SEARCHED:- nowhere}).\n\
Pass the path yourself, for example:\n\
  bash ~/PopMark/scripts/termux-install.sh ~/storage/downloads/PopMark-*-linux-arm64-portable.tar.gz\n\
Or locate a copy anywhere on the device:\n\
  find -H /sdcard -iname 'PopMark-*.tar.gz' 2>/dev/null"
fi

say "Installing Termux packages (proot-distro, audio, wake lock)..."
pkg update -y >/dev/null
pkg install -y proot-distro pulseaudio tar termux-tools >/dev/null

if [ ! -d "$ROOTFS" ]; then
    say "Creating the Debian proot distro (this downloads about 100 MB)..."
    proot-distro install "$DISTRO"
else
    say "Debian proot distro already exists."
fi

say "Installing mpv inside Debian..."
proot-distro login "$DISTRO" -- bash -lc '
    set -e
    export DEBIAN_FRONTEND=noninteractive
    if ! command -v mpv >/dev/null 2>&1; then
        apt-get update
        apt-get install -y --no-install-recommends mpv ca-certificates
    fi
'

say "Unpacking PopMark into $INSTALL_DIR..."
STAGE="$ROOTFS/opt/popmark.new"
rm -rf "$STAGE"
mkdir -p "$STAGE"
tar -xzf "$TARBALL" -C "$STAGE" || die "Could not unpack $TARBALL"
[ -f "$STAGE/PopMark" ] || die "The archive does not contain a PopMark binary."
[ -d "$STAGE/ui/dist" ] || die "The archive does not contain ui/dist."

# Byte 18-19 of an ELF header: 0xB700 is little-endian aarch64, 0x3E00 is x86-64.
# Checking beats trusting the file name, especially after a rename.
MACHINE="$(od -An -tx1 -j18 -N2 "$STAGE/PopMark" 2>/dev/null | tr -d ' \n')"
[ "$MACHINE" = "b700" ] || die \
    "This is not an arm64 build (ELF machine 0x${MACHINE:-unknown}). Build it with scripts/build-linux.ps1 -Runtime linux-arm64."

chmod +x "$STAGE/PopMark" 2>/dev/null || true
chmod +x "$STAGE/bun" 2>/dev/null || true
chmod +x "$STAGE/tools/yt-dlp/yt-dlp" 2>/dev/null || true

rm -rf "$INSTALL_DIR"
mv "$STAGE" "$INSTALL_DIR"

say "Writing the in-distro launcher..."
mkdir -p "$ROOTFS/usr/local/bin"
cat > "$ROOTFS/usr/local/bin/popmark" <<'LAUNCHER'
#!/bin/bash
# PopMark runs inside proot. Audio is carried over TCP to the PulseAudio server
# started by the Termux-side popmark wrapper.
export PULSE_SERVER="${PULSE_SERVER:-tcp:127.0.0.1:4713}"
export POPMARK_MPV_AO="${POPMARK_MPV_AO:-pulse}"
export POPMARK_HOME="${POPMARK_HOME:-$HOME/.local/share/PopMark}"
cd /opt/popmark
exec /opt/popmark/PopMark "$@"
LAUNCHER
chmod +x "$ROOTFS/usr/local/bin/popmark"

say "Writing the Termux launcher..."
cat > "$PREFIX/bin/popmark" <<'LAUNCHER'
#!/data/data/com.termux/files/usr/bin/bash
set -u

# Start (or reuse) the Termux PulseAudio server that Debian's mpv talks to.
pulseaudio --start --exit-idle-time=-1 >/dev/null 2>&1 || true
pactl load-module module-native-protocol-tcp auth-anonymous=1 port=4713 >/dev/null 2>&1 || true

termux-wake-lock >/dev/null 2>&1 || true
trap 'termux-wake-unlock >/dev/null 2>&1 || true' EXIT

proot-distro login debian -- /usr/local/bin/popmark "$@"
LAUNCHER
chmod +x "$PREFIX/bin/popmark"

say "Done."
cat <<'EOF'

Run PopMark from Termux with:

    popmark

First run tips:
  * Keep the Termux notification visible so Android does not freeze playback.
  * Disable battery optimisation for Termux if tracks stop in the background.
  * Tap the prompt to type; on narrow screens the footer has HELP/TOOLS/FOCUS/
    LOGS/THEME/QUIT buttons, so the F-keys are optional.
  * yt-dlp is bundled. Update it with: popmark tools install yt-dlp

EOF
