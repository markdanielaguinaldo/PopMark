param(
    [string]$Version = "1.1.2"
)

$ErrorActionPreference = "Stop"

$repo = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$ui = Join-Path $repo "ui"
$releases = Join-Path $repo "releases"
$name = "PopMark-$Version-OpenTUI-win-x64-portable"
$out = Join-Path $releases $name
$zip = Join-Path $releases "$name.zip"

$bunCandidates = @(
    $env:POPMARK_BUN,
    (Join-Path $env:APPDATA "npm\node_modules\bun\bin\bun.exe"),
    (Join-Path $env:USERPROFILE ".bun\bin\bun.exe")
) | Where-Object { $_ -and (Test-Path $_) }

$bun = $bunCandidates | Select-Object -First 1
if (-not $bun) {
    throw "Bun executable not found. Set POPMARK_BUN or install Bun."
}

if (Test-Path $out) { Remove-Item $out -Recurse -Force }
if (Test-Path $zip) { Remove-Item $zip -Force }
New-Item -ItemType Directory -Force $out | Out-Null
New-Item -ItemType Directory -Force (Join-Path $out "ui\dist") | Out-Null

Write-Host "Building OpenTUI bundle..."
Push-Location $ui
try {
    & $bun run build
    if ($LASTEXITCODE -ne 0) { throw "Frontend build failed." }
} finally {
    Pop-Location
}

Write-Host "Publishing self-contained backend..."
dotnet publish (Join-Path $repo "PopMark\PopMark.csproj") `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $out
if ($LASTEXITCODE -ne 0) { throw "Backend publish failed." }

Write-Host "Copying UI assets and runtime..."
Copy-Item (Join-Path $ui "dist\*") (Join-Path $out "ui\dist") -Recurse -Force

$nativeSource = Join-Path $ui "node_modules\@opentui\core-win32-x64"
$nativeTarget = Join-Path $out "ui\node_modules\@opentui\core-win32-x64"
New-Item -ItemType Directory -Force $nativeTarget | Out-Null
Copy-Item (Join-Path $nativeSource "*") $nativeTarget -Recurse -Force

Copy-Item $bun (Join-Path $out "bun.exe") -Force

@"
PopMark $Version - OpenTUI portable build
=========================================

1. Keep PopMark.exe, bun.exe, and the ui folder together.
2. Run PopMark.exe.
3. Use F2 to install or inspect yt-dlp and mpv.

No .NET or Bun installation is required for this portable build.
Queue, tool pins, and managed tools live under %LOCALAPPDATA%\PopMark.

Controls: F1 help, F2 tools, F3 focus view, F4 diagnostics, Ctrl+Q quit.
"@ | Set-Content (Join-Path $out "README.txt") -Encoding utf8

Copy-Item (Join-Path $repo "README.md") (Join-Path $out "README.md") -Force

Write-Host "Verifying backend..."
& (Join-Path $out "PopMark.exe") --version
if ($LASTEXITCODE -ne 0) { throw "Portable backend did not run." }

Write-Host "Creating archive..."
Compress-Archive -Path (Join-Path $out "*") -DestinationPath $zip -CompressionLevel Optimal

Write-Host "Portable build: $out"
Write-Host "Archive: $zip"
