param(
    [string]$Version = "1.1.2",
    [string]$Ref = "v1.1.2"
)

$ErrorActionPreference = "Stop"

$repo = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$releases = Join-Path $repo "releases"
$work = Join-Path $releases "_legacy-worktree"
$name = "PopMark-$Version-legacy-win-x64"
$out = Join-Path $releases $name
$zip = Join-Path $releases "$name.zip"

if (-not (git -C $repo rev-parse --verify "$Ref^{commit}" 2>$null)) {
    throw "Git ref not found: $Ref"
}

if (Test-Path $work) {
    git -C $repo worktree remove --force $work 2>$null
    if (Test-Path $work) { Remove-Item $work -Recurse -Force }
}
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
if (Test-Path $zip) { Remove-Item $zip -Force }

Write-Host "Checking out legacy source at $Ref..."
git -C $repo worktree add --detach $work $Ref
if ($LASTEXITCODE -ne 0) { throw "Could not create the legacy worktree." }

try {
    Write-Host "Publishing legacy C# UI..."
    dotnet publish (Join-Path $work "PopMark\PopMark.csproj") `
        --configuration Release `
        --runtime win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -o $out
    if ($LASTEXITCODE -ne 0) { throw "Legacy publish failed." }

    @"
PopMark $Version - legacy C# terminal UI
========================================

This is a backup build of the original Spectre.Console interface, taken from
tag $Ref before the OpenTUI rewrite.

1. Run PopMark.exe.
2. Use the in-app tools screen to install or inspect yt-dlp and mpv.

No .NET installation is required. Queue, tool pins, and managed tools live
under %LOCALAPPDATA%\PopMark, shared with the OpenTUI build.

The current OpenTUI build also starts this UI when launched with --legacy-ui.
"@ | Set-Content (Join-Path $out "README.txt") -Encoding utf8

    if (Test-Path (Join-Path $work "README.md")) {
        Copy-Item (Join-Path $work "README.md") (Join-Path $out "README.md") -Force
    }

    Write-Host "Verifying legacy backend..."
    & (Join-Path $out "PopMark.exe") --version
    if ($LASTEXITCODE -ne 0) { throw "Legacy backend did not run." }

    Write-Host "Creating archive..."
    # A self-contained single-file app can hold its own image briefly after
    # --version exits; retry until Windows releases the handle.
    for ($attempt = 1; ; $attempt++) {
        try {
            Compress-Archive -Path (Join-Path $out "*") -DestinationPath $zip -CompressionLevel Optimal
            break
        } catch {
            if ($attempt -ge 10) { throw }
            if (Test-Path $zip) { Remove-Item $zip -Force }
            Start-Sleep -Seconds 2
        }
    }
} finally {
    git -C $repo worktree remove --force $work 2>$null
}

Write-Host "Legacy build: $out"
Write-Host "Archive: $zip"
