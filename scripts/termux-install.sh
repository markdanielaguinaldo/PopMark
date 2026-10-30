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
PROOT_BASE="$PREFIX/var/lib/proot-distro"
TARBALL="${1:-}"

# Let the caller write ~/storage/... instead of the full expanded path.
case "$TARBALL" in
    "~/"*) TARBALL="$HOME/${TARBALL#"~/"}" ;;
esac

# A URL downloads straight from a GitHub release, so nothing has to be copied
# over from a computer first.
case "$TARBALL" in
    http://* | https://*)
        URL="$TARBALL"
        command -v curl >/dev/null 2>&1 || pkg install -y curl
        TARBALL="$HOME/PopMark-download.tar.gz"
        say "Downloading $URL..."
        curl -fL --progress-bar -o "$TARBALL" "$URL" || die "Download failed: $URL"
        ;;
esac

# Without an argument, look for the newest archive. ~/storage/downloads is a
# symlink, so find needs -H (follow a symlink starting point) or it descends
# nowhere and reports no match.
is_package() {
    tar -tzf "$1" 2>/dev/null | grep -qE '^(\./)?PopMark$'
}

SEARCHED=""
CANDIDATES=""
SEEN=""
if [ -z "$TARBALL" ]; then
    for directory in "$HOME/storage/downloads" "$HOME/storage/shared/Download" "$HOME"; do
        [ -d "$directory" ] || continue
        SEARCHED="$SEARCHED $directory"
        CANDIDATES="$CANDIDATES$(find -H "$directory" -maxdepth 1 -type f -name 'PopMark-*.tar.gz' 2>/dev/null)
"
    done

    # Newest first, but skip anything that is not a portable package, such as a
    # GitHub source snapshot that happens to share the name.
    while IFS= read -r candidate; do
        [ -n "$candidate" ] || continue
        if is_package "$candidate"; then
            TARBALL="$candidate"
            say "Found $TARBALL"
            break
        fi
        SEEN="$SEEN  $candidate
"
    done <<EOF
$(printf '%s' "$CANDIDATES" | sort -r)
EOF
fi

if [ -n "$TARBALL" ] && [ ! -f "$TARBALL" ]; then
    die "No such file: $TARBALL\n\
Check the exact name:\n\
  ls -l ~/storage/downloads/*.tar.gz"
fi

if [ -z "$TARBALL" ]; then
    if [ -n "$SEEN" ]; then
        die "Found archives, but none is a PopMark portable package:\n${SEEN}\
Pass the package explicitly, for example:\n\
  bash ~/PopMark/scripts/termux-install.sh ~/PopMark-download.tar.gz"
    fi
    die "No PopMark .tar.gz found (searched:${SEARCHED:- nothing}).\n\
Pass the path yourself, for example:\n\
  bash ~/PopMark/scripts/termux-install.sh ~/storage/downloads/PopMark-*.tar.gz\n\
Or locate a copy anywhere on the device:\n\
  find -H /sdcard -iname 'PopMark-*.tar.gz' 2>/dev/null"
fi

say "Installing Termux packages (proot-distro, audio, wake lock)..."
# `pkg` wraps apt, and apt warns "does not have a stable CLI interface" whenever
# it runs without a terminal. Hide that noise and report our own failure instead.
pkg update -y >/dev/null 2>&1 || say "Package index update failed; continuing with the current index."
pkg install -y proot-distro pulseaudio tar termux-tools >/dev/null 2>&1 || die \
    "Could not install proot-distro, pulseaudio, tar, and termux-tools.\nRun 'pkg install -y proot-distro pulseaudio' manually, then retry."

# proot-distro 5.x stores containers under containers/<name>/rootfs; older
# releases used installed-rootfs/<name>. Resolve whichever actually holds a
# rootfs, and never treat a folder this script created as an installed distro.
resolve_rootfs() {
    for candidate in "$PROOT_BASE/containers/$DISTRO/rootfs" "$PROOT_BASE/installed-rootfs/$DISTRO"; do
        if [ -d "$candidate/etc" ]; then
            ROOTFS="$candidate"
            return 0
        fi
    done
    return 1
}

if ! resolve_rootfs; then
    say "Creating the Debian proot distro (this downloads about 100 MB)..."
    proot-distro install "$DISTRO"
    resolve_rootfs || die "proot-distro did not create a usable Debian rootfs. Run 'proot-distro install debian' and retry."
else
    say "Debian proot distro already exists."
fi
say "Rootfs: $ROOTFS"

# Earlier versions of this installer wrote into the legacy path and created that
# folder themselves. Remove the leftover copy so it cannot be mistaken for an
# install or waste ~800 MB.
STRAY="$PROOT_BASE/installed-rootfs/$DISTRO"
if [ "$ROOTFS" != "$STRAY" ] && [ -d "$STRAY" ] && [ ! -d "$STRAY/etc" ]; then
    say "Removing a stray legacy install at $STRAY"
    rm -rf "$STRAY"
fi

INSTALL_DIR="$ROOTFS/opt/popmark"

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

# Some archives keep everything under one top-level folder; accept either shape.
ROOT="$STAGE"
if [ ! -f "$ROOT/PopMark" ]; then
    nested=$(find "$STAGE" -maxdepth 4 -type f -name PopMark 2>/dev/null | head -n 1 || true)
    if [ -n "$nested" ]; then
        ROOT="$(dirname "$nested")"
        say "Using $ROOT as the application root."
    fi
fi

if [ ! -f "$ROOT/PopMark" ] || [ ! -d "$ROOT/ui/dist" ]; then
    printf '\n--- contents of %s (first 20 entries) ---\n' "$TARBALL" >&2
    tar -tzf "$TARBALL" 2>/dev/null | head -n 20 >&2 || true
    printf -- '---\n' >&2
    die "This is not a PopMark portable package: it has no PopMark binary and ui/dist.\n\
Copy the archive built by scripts/build-linux.ps1 -Runtime linux-arm64, or download it with:\n\
  curl -L -o ~/storage/downloads/PopMark-arm64.tar.gz <release-url>"
fi

# Byte 18-19 of an ELF header: 0xB700 is little-endian aarch64, 0x3E00 is x86-64.
# Checking beats trusting the file name, especially after a rename.
MACHINE="$(od -An -tx1 -j18 -N2 "$ROOT/PopMark" 2>/dev/null | tr -d ' \n')"
[ "$MACHINE" = "b700" ] || die \
    "This is not an arm64 build (ELF machine 0x${MACHINE:-unknown}). Build it with scripts/build-linux.ps1 -Runtime linux-arm64."

chmod +x "$ROOT/PopMark" 2>/dev/null || true
chmod +x "$ROOT/bun" 2>/dev/null || true
chmod +x "$ROOT/tools/yt-dlp/yt-dlp" 2>/dev/null || true

rm -rf "$INSTALL_DIR"
mv "$ROOT" "$INSTALL_DIR"
if [ "$ROOT" != "$STAGE" ]; then
    rm -rf "$STAGE"
fi

say "Writing the in-distro launcher..."
mkdir -p "$ROOTFS/usr/local/bin"
cat > "$ROOTFS/usr/local/bin/popmark" <<'LAUNCHER'
#!/bin/bash
# PopMark runs inside proot. Audio is carried over TCP to the PulseAudio server
# started by the Termux-side popmark wrapper.
export PULSE_SERVER="${PULSE_SERVER:-tcp:127.0.0.1:4713}"
export POPMARK_MPV_AO="${POPMARK_MPV_AO:-pulse}"
export POPMARK_HOME="${POPMARK_HOME:-$HOME/.local/share/PopMark}"
# Under proot the GC cannot reserve its default virtual range and CoreCLR aborts
# with "GC heap initialization failed (0x8007000E)". A hard limit avoids the
# reservation; 256 MB is far more than this app needs.
export DOTNET_GCHeapHardLimit="${DOTNET_GCHeapHardLimit:-0x10000000}"
cd /opt/popmark
exec /opt/popmark/PopMark "$@"
LAUNCHER
chmod +x "$ROOTFS/usr/local/bin/popmark"
[ -x "$ROOTFS/usr/local/bin/popmark" ] || die "Could not write the in-distro launcher."

say "Writing the Termux launcher..."
cat > "$PREFIX/bin/popmark" <<'LAUNCHER'
#!/data/data/com.termux/files/usr/bin/bash
set -u

# Start (or reuse) the Termux PulseAudio server that Debian's mpv talks to.
pulseaudio --start --exit-idle-time=-1 >/dev/null 2>&1 || true
pactl load-module module-native-protocol-tcp auth-anonymous=1 port=4713 >/dev/null 2>&1 || true

termux-wake-lock >/dev/null 2>&1 || true
trap 'termux-wake-unlock >/dev/null 2>&1 || true' EXIT

# Prefer the in-distro launcher, but do not depend on it: if it is missing or not
# executable, run the packaged binary directly with the same environment.
proot-distro login debian -- /bin/sh -c '
    if [ -x /usr/local/bin/popmark ]; then
        exec /usr/local/bin/popmark "$@"
    fi
    if [ ! -x /opt/popmark/PopMark ]; then
        echo "PopMark is not installed in Debian. Rerun scripts/termux-install.sh." >&2
        exit 1
    fi
    PULSE_SERVER="${PULSE_SERVER:-tcp:127.0.0.1:4713}"
    POPMARK_MPV_AO="${POPMARK_MPV_AO:-pulse}"
    POPMARK_HOME="${POPMARK_HOME:-$HOME/.local/share/PopMark}"
    DOTNET_GCHeapHardLimit="${DOTNET_GCHeapHardLimit:-0x10000000}"
    export PULSE_SERVER POPMARK_MPV_AO POPMARK_HOME DOTNET_GCHeapHardLimit
    cd /opt/popmark || exit 1
    exec ./PopMark "$@"
' sh "$@"
LAUNCHER
chmod +x "$PREFIX/bin/popmark"

say "Adding always-visible Termux keys (F1-F5, arrows, space)..."
TERMUX_PROPS="$HOME/.termux/termux.properties"
if [ -f "$TERMUX_PROPS" ] && grep -q '^[[:space:]]*extra-keys' "$TERMUX_PROPS"; then
    say "Keeping your existing extra-keys setting."
else
    mkdir -p "$HOME/.termux"
    if [ -f "$TERMUX_PROPS" ]; then
        cp "$TERMUX_PROPS" "$TERMUX_PROPS.popmark-backup" 2>/dev/null || true
    fi
    cat >> "$TERMUX_PROPS" <<'KEYS'

# Added by PopMark: keys above the keyboard, usable without opening it.
extra-keys = [['ESC','TAB','CTRL','ALT','LEFT','UP','RIGHT'],['F1','F2','F3','F4','F5','SPACE','ENTER']]
KEYS
fi
termux-reload-settings >/dev/null 2>&1 || true

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
