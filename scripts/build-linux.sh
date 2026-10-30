#!/usr/bin/env bash
#
# Builds the same Linux portable package as scripts/build-linux.ps1, but on Linux.
# Useful inside the Termux proot distro when there is no desktop machine to
# cross-publish from.
#
# Usage: scripts/build-linux.sh [version] [runtime]
#   runtime: linux-arm64 (default) or linux-x64

set -euo pipefail

VERSION="${1:-1.1.2}"
RUNTIME="${2:-linux-arm64}"

case "$RUNTIME" in
    linux-arm64) BUN_ASSET="bun-linux-aarch64"; NATIVE="core-linux-arm64"; YTDLP_ASSET="yt-dlp_linux_aarch64" ;;
    linux-x64)   BUN_ASSET="bun-linux-x64";     NATIVE="core-linux-x64";   YTDLP_ASSET="yt-dlp_linux" ;;
    *) echo "Unsupported runtime: $RUNTIME" >&2; exit 1 ;;
esac

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UI="$REPO/ui"
RELEASES="$REPO/releases"
NAME="PopMark-$VERSION-OpenTUI-$RUNTIME-portable"
OUT="$RELEASES/$NAME"
ARCHIVE="$RELEASES/$NAME.tar.gz"
TEMP="$(mktemp -d)"
trap 'rm -rf "$TEMP"' EXIT

for command in bun dotnet curl tar; do
    command -v "$command" >/dev/null 2>&1 || { echo "Missing required command: $command" >&2; exit 1; }
done

rm -rf "$OUT" "$ARCHIVE"
mkdir -p "$OUT/ui/dist"

echo "Installing frontend dependencies..."
(
    cd "$UI"
    bun install --frozen-lockfile
    bun run build
)

echo "Publishing self-contained backend for $RUNTIME..."
dotnet publish "$REPO/PopMark/PopMark.csproj" \
    --configuration Release \
    --runtime "$RUNTIME" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:InvariantGlobalization=true \
    -p:DebugType=None \
    -p:DebugSymbols=false \
    -o "$OUT"

echo "Copying UI assets and OpenTUI native library..."
cp -R "$UI/dist/." "$OUT/ui/dist/"
CORE_VERSION="$(sed -n 's/.*"@opentui\/core": *"\([^"]*\)".*/\1/p' "$UI/package.json" | head -n1)"
[ -n "$CORE_VERSION" ] || { echo "Could not read the @opentui/core version." >&2; exit 1; }
curl -fsSL -o "$TEMP/native.tgz" \
    "https://registry.npmjs.org/@opentui/$NATIVE/-/$NATIVE-$CORE_VERSION.tgz"
mkdir -p "$TEMP/native"
tar -xzf "$TEMP/native.tgz" -C "$TEMP/native"
mkdir -p "$OUT/ui/node_modules/@opentui/$NATIVE"
cp -R "$TEMP/native/package/." "$OUT/ui/node_modules/@opentui/$NATIVE/"

echo "Downloading Bun..."
curl -fsSL -o "$TEMP/bun.zip" \
    "https://github.com/oven-sh/bun/releases/latest/download/$BUN_ASSET.zip"
if command -v unzip >/dev/null 2>&1; then
    unzip -q "$TEMP/bun.zip" -d "$TEMP"
else
    python3 -c "import zipfile,sys; zipfile.ZipFile(sys.argv[1]).extractall(sys.argv[2])" \
        "$TEMP/bun.zip" "$TEMP"
fi
cp "$TEMP/$BUN_ASSET/bun" "$OUT/bun"
chmod +x "$OUT/bun"

echo "Downloading yt-dlp..."
mkdir -p "$OUT/tools/yt-dlp"
curl -fsSL -o "$OUT/tools/yt-dlp/yt-dlp" \
    "https://github.com/yt-dlp/yt-dlp/releases/latest/download/$YTDLP_ASSET"
chmod +x "$OUT/tools/yt-dlp/yt-dlp"

chmod +x "$OUT/PopMark"
cp -R "$REPO/docs" "$OUT/docs"
cp "$REPO/README.md" "$OUT/README.md"

echo "Creating archive..."
tar -czf "$ARCHIVE" -C "$OUT" .

echo "Linux package: $OUT"
echo "Archive: $ARCHIVE"
