param(
    [string]$CommandDir = (Join-Path $env:USERPROFILE ".local\bin")
)

$ErrorActionPreference = "Stop"

$repo = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path

function Find-Build([string]$Path, [string]$Label) {
    if (Test-Path (Join-Path $Path "PopMark.exe")) {
        return (Resolve-Path $Path).Path
    }

    Write-Warning "$Label build not found at $Path"
    return $null
}

$portable = Get-ChildItem (Join-Path $repo "releases") -Directory -Filter "PopMark-*-OpenTUI-win-x64-portable" -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

$builds = @(
    @{ Name = "popmark"; Label = "Portable Release"; Path = if ($portable) { $portable.FullName } else { $null } }
    @{ Name = "popmark-release"; Label = "Release"; Path = (Join-Path $repo "builds\Release") }
    @{ Name = "popmark-debug"; Label = "Debug"; Path = (Join-Path $repo "builds\Debug") }
)

New-Item -ItemType Directory -Force $CommandDir | Out-Null

$installed = @()
foreach ($build in $builds) {
    $target = if ($build.Path) { Find-Build $build.Path $build.Label } else { $null }
    if (-not $target) {
        continue
    }

    $content = @"
@echo off
setlocal
set "POPMARK_HOME=$target"
if not exist "%POPMARK_HOME%\PopMark.exe" (
  echo PopMark $($build.Label) build not found at "%POPMARK_HOME%". 1>&2
  exit /b 1
)
pushd "%POPMARK_HOME%" >nul
"%POPMARK_HOME%\PopMark.exe" %*
set "POPMARK_EXIT=%ERRORLEVEL%"
popd >nul
exit /b %POPMARK_EXIT%
"@

    $path = Join-Path $CommandDir "$($build.Name).cmd"
    Set-Content -Path $path -Value $content -Encoding ascii -NoNewline
    $installed += $path
}

if ($installed.Count -eq 0) {
    throw "No PopMark builds found. Run .\scripts\build-portable.ps1 or build the solution first."
}

$userPath = [Environment]::GetEnvironmentVariable("Path", "User")
if ($null -eq $userPath) { $userPath = "" }
$parts = $userPath.Split(";", [StringSplitOptions]::RemoveEmptyEntries)
if ($parts -notcontains $CommandDir) {
    $newPath = (@($parts) + $CommandDir) -join ";"
    [Environment]::SetEnvironmentVariable("Path", $newPath, "User")
    Write-Host "Added $CommandDir to the user PATH. Open a new terminal to pick it up."
}

if (($env:PATH -split ";") -notcontains $CommandDir) {
    $env:PATH = "$CommandDir;$env:PATH"
}

Write-Host "Installed commands in ${CommandDir}:"
$installed | ForEach-Object { Write-Host "  $([IO.Path]::GetFileNameWithoutExtension($_))" }
Write-Host ""
Write-Host "Try: popmark --version"
