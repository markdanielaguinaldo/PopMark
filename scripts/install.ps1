[CmdletBinding()]
param(
    [string]$CommandDir = (Join-Path $env:USERPROFILE ".local\bin"),
    [switch]$SkipPlaybackTools
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

function Write-Step([string]$Text) {
    Write-Host ""
    Write-Host "== $Text ==" -ForegroundColor Cyan
}

function Add-ToPath([string]$Directory) {
    $userPath = [Environment]::GetEnvironmentVariable("Path", "User")
    if ($null -eq $userPath) { $userPath = "" }

    $parts = @($userPath.Split(";", [StringSplitOptions]::RemoveEmptyEntries))
    if ($parts -notcontains $Directory) {
        $newPath = (@($parts) + $Directory) -join ";"
        [Environment]::SetEnvironmentVariable("Path", $newPath, "User")
        Write-Host "Added to user PATH: $Directory"
    }

    if (($env:PATH.Split(";", [StringSplitOptions]::RemoveEmptyEntries)) -notcontains $Directory) {
        $env:PATH = "$Directory;$env:PATH"
    }
}

function Get-DotNetSdkMajor {
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $dotnet) { return 0 }

    $sdks = & $dotnet.Source --list-sdks 2>$null
    $majors = @()
    foreach ($line in $sdks) {
        if ($line -match '^(\d+)\.') {
            $majors += [int]$Matches[1]
        }
    }

    if ($majors.Count -eq 0) { return 0 }
    return ($majors | Measure-Object -Maximum).Maximum
}

function Install-DotNetSdk {
    $installDir = Join-Path $env:LOCALAPPDATA "Microsoft\dotnet"
    $installer = Join-Path $env:TEMP "dotnet-install.ps1"

    Write-Host "Installing the .NET 9 SDK into $installDir..."
    Invoke-WebRequest "https://dot.net/v1/dotnet-install.ps1" -UseBasicParsing -OutFile $installer

    & powershell -NoProfile -ExecutionPolicy Bypass -File $installer -Channel "9.0" -InstallDir $installDir
    if ($LASTEXITCODE -ne 0) {
        throw "The .NET SDK installer failed with code $LASTEXITCODE."
    }

    Add-ToPath $installDir
    $env:DOTNET_ROOT = $installDir
    [Environment]::SetEnvironmentVariable("DOTNET_ROOT", $installDir, "User")

    $dotnet = Join-Path $installDir "dotnet.exe"
    if (-not (Test-Path $dotnet)) {
        throw "The .NET SDK was installed but $dotnet was not found."
    }
}

function Get-BunPath {
    $candidates = @(
        $env:POPMARK_BUN,
        (Join-Path $env:USERPROFILE ".bun\bin\bun.exe"),
        (Join-Path $env:APPDATA "npm\node_modules\bun\bin\bun.exe")
    )

    $command = Get-Command bun.exe -ErrorAction SilentlyContinue
    if ($command) { $candidates += $command.Source }

    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path $candidate)) {
            return (Resolve-Path $candidate).Path
        }
    }

    return $null
}

function Get-BunVersion([string]$Path) {
    try {
        $value = (& $Path --version 2>$null | Select-Object -First 1).Trim()
        return [version]$value
    } catch {
        return $null
    }
}

function Install-Bun {
    $installer = Join-Path $env:TEMP "bun-install.ps1"
    Write-Host "Installing Bun..."
    Invoke-WebRequest "https://bun.sh/install.ps1" -UseBasicParsing -OutFile $installer

    & powershell -NoProfile -ExecutionPolicy Bypass -File $installer
    if ($LASTEXITCODE -ne 0) {
        throw "The Bun installer failed with code $LASTEXITCODE."
    }

    $bunDir = Join-Path $env:USERPROFILE ".bun\bin"
    if (Test-Path (Join-Path $bunDir "bun.exe")) {
        Add-ToPath $bunDir
    }
}

$repo = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
if (-not (Test-Path (Join-Path $repo "PopMark.sln"))) {
    throw "Could not find PopMark.sln next to $repo. Run this script from the repository's scripts folder."
}

Write-Host "PopMark bootstrap"
Write-Host "Repository: $repo"

Write-Step "Checking the .NET SDK"
$sdkMajor = Get-DotNetSdkMajor
if ($sdkMajor -lt 9) {
    Install-DotNetSdk
} else {
    Write-Host "Found .NET SDK major version $sdkMajor."
}

Write-Step "Checking Bun"
$bun = Get-BunPath
if (-not $bun) {
    Install-Bun
    $bun = Get-BunPath
} else {
    $bunVersion = Get-BunVersion $bun
    if ($null -eq $bunVersion -or $bunVersion -lt [version]"1.3.14") {
        Write-Host "Found Bun $bunVersion; installing a current version."
        Install-Bun
        $bun = Get-BunPath
    } else {
        Write-Host "Found Bun $bunVersion at $bun."
    }
}

if (-not $bun) {
    throw "Bun was not found after installation. Install it from https://bun.sh and rerun this script."
}

Write-Step "Installing frontend dependencies"
Push-Location (Join-Path $repo "ui")
try {
    & $bun install --frozen-lockfile
    if ($LASTEXITCODE -ne 0) {
        throw "bun install failed. Check the network connection and the ui\bun.lock file."
    }
} finally {
    Pop-Location
}

Write-Step "Building the portable release"
& (Join-Path $repo "scripts\build-portable.ps1")
if ($LASTEXITCODE -ne 0) {
    throw "The portable build failed."
}

Write-Step "Installing the popmark command"
& (Join-Path $repo "scripts\install-commands.ps1") -CommandDir $CommandDir
if ($LASTEXITCODE -ne 0) {
    throw "Installing the popmark command failed."
}

if (-not $SkipPlaybackTools) {
    Write-Step "Checking yt-dlp and mpv"
    $managedRoot = Join-Path $env:LOCALAPPDATA "PopMark\tools"
    $managedYtDlp = Join-Path $managedRoot "yt-dlp\yt-dlp.exe"
    $managedMpv = Join-Path $managedRoot "mpv\mpv.exe"

    if ((Test-Path $managedYtDlp) -and (Test-Path $managedMpv)) {
        Write-Host "Managed yt-dlp and mpv are already installed."
    } else {
        $portable = Get-ChildItem (Join-Path $repo "releases") -Directory -Filter "PopMark-*-OpenTUI-win-x64-portable" -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1

        if ($portable) {
            $exe = Join-Path $portable.FullName "PopMark.exe"
            & $exe tools install all
            if ($LASTEXITCODE -ne 0) {
                Write-Warning "Playback tool installation returned code $LASTEXITCODE. Open PopMark and press F2 to repair them."
            }
        }
    }
}

$portable = Get-ChildItem (Join-Path $repo "releases") -Directory -Filter "PopMark-*-OpenTUI-win-x64-portable" -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

Write-Host ""
Write-Host "PopMark is ready." -ForegroundColor Green
if ($portable) {
    Write-Host "Portable build: $(Join-Path $portable.FullName 'PopMark.exe')"
}
Write-Host "Command: popmark"
Write-Host "If the command is not visible in this shell, open a new terminal."
exit 0
