param(
    [string]$Version = "1.1.2",
    [ValidateSet("linux-arm64", "linux-x64")]
    [string]$Runtime = "linux-arm64",
    [switch]$SkipBun,
    [switch]$SkipYtDlp
)

# Builds a self-contained PopMark package for Linux. The linux-arm64 package is what
# runs on an Android phone inside Termux + proot-distro. Cross-publishing from Windows
# avoids installing the .NET SDK and Bun on the device.

$ErrorActionPreference = "Stop"

$repo = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$ui = Join-Path $repo "ui"
$releases = Join-Path $repo "releases"
$name = "PopMark-$Version-OpenTUI-$Runtime-portable"
$out = Join-Path $releases $name
$tar = Join-Path $releases "$name.tar.gz"
$temp = Join-Path ([System.IO.Path]::GetTempPath()) "popmark-$Runtime-build"

$bunCandidates = @(
    $env:POPMARK_BUN,
    (Join-Path $env:USERPROFILE ".bun\bin\bun.exe"),
    (Join-Path $env:APPDATA "npm\node_modules\bun\bin\bun.exe")
)

$bunCommand = Get-Command bun.exe -ErrorAction SilentlyContinue
if ($bunCommand) { $bunCandidates += $bunCommand.Source }

$bun = $bunCandidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if (-not $bun) {
    throw "Bun was not found. Run .\scripts\install.ps1, or install Bun from https://bun.sh."
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "The .NET SDK was not found. Run .\scripts\install.ps1 to install it."
}

if (-not (Get-Command tar -ErrorAction SilentlyContinue)) {
    throw "tar.exe was not found. Windows 10 1803 or newer ships it; otherwise install bsdtar."
}

if (Test-Path $out) { Remove-Item $out -Recurse -Force }
if (Test-Path $tar) { Remove-Item $tar -Force }
if (Test-Path $temp) { Remove-Item $temp -Recurse -Force }
New-Item -ItemType Directory -Force $out | Out-Null
New-Item -ItemType Directory -Force (Join-Path $out "ui\dist") | Out-Null
New-Item -ItemType Directory -Force $temp | Out-Null

Write-Host "Installing frontend dependencies..."
Push-Location $ui
try {
    & $bun install --frozen-lockfile
    if ($LASTEXITCODE -ne 0) { throw "Frontend dependency installation failed." }

    Write-Host "Building OpenTUI bundle..."
    & $bun run build
    if ($LASTEXITCODE -ne 0) { throw "Frontend build failed." }
} finally {
    Pop-Location
}

Write-Host "Publishing self-contained backend for $Runtime..."
dotnet publish (Join-Path $repo "PopMark\PopMark.csproj") `
    --configuration Release `
    --runtime $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:InvariantGlobalization=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $out
if ($LASTEXITCODE -ne 0) { throw "Backend publish failed." }

Write-Host "Copying UI assets and OpenTUI native library..."
Copy-Item (Join-Path $ui "dist\*") (Join-Path $out "ui\dist") -Recurse -Force

$coreVersion = (Get-Content (Join-Path $ui "package.json") -Raw | ConvertFrom-Json).dependencies."@opentui/core"
$nativePackage = "core-$Runtime"
$nativeDir = Join-Path $out "ui\node_modules\@opentui\$nativePackage"
$nativeTarball = Join-Path $temp "$nativePackage.tgz"
$nativeExtract = Join-Path $temp $nativePackage

Write-Host "Downloading @opentui/$nativePackage $coreVersion..."
& curl.exe -fsSL -o $nativeTarball "https://registry.npmjs.org/@opentui/$nativePackage/-/$nativePackage-$coreVersion.tgz"
if ($LASTEXITCODE -ne 0) { throw "Could not download @opentui/$nativePackage." }

New-Item -ItemType Directory -Force $nativeExtract | Out-Null
& tar -xzf $nativeTarball -C $nativeExtract
if ($LASTEXITCODE -ne 0) { throw "Could not extract @opentui/$nativePackage." }

New-Item -ItemType Directory -Force $nativeDir | Out-Null
Copy-Item (Join-Path $nativeExtract "package\*") $nativeDir -Recurse -Force

if (-not $SkipBun) {
    Write-Host "Downloading Bun for $Runtime..."
    $bunArchiveName = if ($Runtime -eq "linux-arm64") { "bun-linux-aarch64" } else { "bun-linux-x64" }
    $bunArchive = Join-Path $temp "$bunArchiveName.zip"
    & curl.exe -fsSL -o $bunArchive "https://github.com/oven-sh/bun/releases/latest/download/$bunArchiveName.zip"
    if ($LASTEXITCODE -ne 0) { throw "Could not download the Bun runtime." }

    Expand-Archive -Path $bunArchive -DestinationPath $temp -Force
    $bunBinary = Join-Path $temp "$bunArchiveName\bun"
    if (-not (Test-Path $bunBinary)) { throw "The Bun archive did not contain a bun executable." }
    Copy-Item $bunBinary (Join-Path $out "bun") -Force
}

if (-not $SkipYtDlp) {
    Write-Host "Downloading yt-dlp..."
    $ytDlpAsset = if ($Runtime -eq "linux-arm64") { "yt-dlp_linux_aarch64" } else { "yt-dlp_linux" }
    $ytDlpDir = Join-Path $out "tools\yt-dlp"
    New-Item -ItemType Directory -Force $ytDlpDir | Out-Null
    & curl.exe -fsSL -o (Join-Path $ytDlpDir "yt-dlp") "https://github.com/yt-dlp/yt-dlp/releases/latest/download/$ytDlpAsset"
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Could not download $ytDlpAsset. Run 'tools install yt-dlp' on the device instead."
        Remove-Item $ytDlpDir -Recurse -Force
    }
}

Write-Host "Copying documentation..."
Copy-Item (Join-Path $repo "docs") (Join-Path $out "docs") -Recurse -Force
Copy-Item (Join-Path $repo "README.md") (Join-Path $out "README.md") -Force

@"
PopMark $Version - OpenTUI portable build ($Runtime)
====================================================

Linux desktop:
  1. Keep PopMark, bun, and the ui folder together.
  2. chmod +x PopMark bun tools/yt-dlp/yt-dlp
  3. Install mpv with your package manager (yt-dlp is bundled).
  4. Run ./PopMark

Android (Termux):
  Use scripts/termux-install.sh from the repository, or follow docs/termux.md.
  Termux cannot run this build directly: it runs inside a Debian proot distro,
  because .NET and Bun need glibc.

Queue, tool pins, and managed tools live under:
  ${'$'}XDG_DATA_HOME/PopMark  (usually ~/.local/share/PopMark)
Set POPMARK_HOME to move that folder.

Controls: F1 help, F2 tools, F3 focus view, F4 diagnostics, Ctrl+Q quit.
On narrow (phone) screens the footer also exposes HELP / TOOLS / FOCUS / LOGS /
THEME / QUIT buttons; tap queue rows, the progress bar, and the volume slider.
"@ | Set-Content (Join-Path $out "README.txt") -Encoding utf8

Write-Host "Creating archive..."
& tar -czf $tar -C $out .
if ($LASTEXITCODE -ne 0) { throw "Could not create the archive." }

Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "Linux package: $out"
Write-Host "Archive: $tar"
