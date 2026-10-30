using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using PopMark.Helpers;
using Spectre.Console;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace PopMark.Services;

public sealed class DependencyInstaller
{
    private static readonly HttpClient HttpClient = CreateHttpClient();

    private static readonly PlaybackDependency[] PlaybackDependencies =
    [
        new("yt-dlp", "yt-dlp", InstallYtDlpAsync),
        new("mpv", "mpv", InstallMpvAsync)
    ];

    public bool ArePlaybackDependenciesAvailable() => GetMissingPlaybackDependencies().Count == 0;

    public IReadOnlyList<string> DependencyNames =>
        PlaybackDependencies.Select(dependency => dependency.CommandName).ToList();

    /// <summary>Where each tool resolved from right now, for the 'tools' diagnostic.</summary>
    public IReadOnlyList<ToolStatus> DescribeTools() =>
        PlaybackDependencies
            .Select(dependency =>
            {
                var path = ToolLocator.ResolveExecutable(dependency.CommandName);
                return new ToolStatus(
                    dependency.DisplayName,
                    path,
                    path is not null && ToolLocator.IsRunnable(path),
                    ToolLocator.LoadOverrides().TryGetValue(dependency.CommandName, out var pinned) ? pinned : null);
            })
            .ToList();

    /// <summary>Installs one tool into the enforced PopMark path even if a copy exists elsewhere.</summary>
    public async Task<string> InstallToManagedPathAsync(string commandName, CancellationToken cancellationToken = default)
    {
        var dependency = PlaybackDependencies.FirstOrDefault(candidate =>
            candidate.CommandName.Equals(commandName, StringComparison.OrdinalIgnoreCase));

        if (dependency is null)
            return $"Unknown tool: {commandName}. Known tools: {string.Join(", ", DependencyNames)}.";

        Directory.CreateDirectory(ToolLocator.ToolRoot);
        await dependency.InstallAsync(cancellationToken);

        // Pin only a copy PopMark actually owns. On Linux, mpv comes from the distro
        // package manager, so there is no managed executable to point at.
        var managed = ToolLocator.ManagedExecutablePath(dependency.CommandName);
        if (File.Exists(managed))
            ToolLocator.SetOverride(dependency.CommandName, managed);

        ToolLocator.Invalidate();
        ToolLocator.RefreshPathFromEnvironment();

        var resolved = ToolLocator.ResolveRunnableExecutable(dependency.CommandName);
        return resolved is null
            ? $"Installed {dependency.DisplayName} under {ToolLocator.ToolRoot}, but it still does not run."
            : $"{dependency.DisplayName} is now installed at {resolved}.";
    }

    public async Task<string> EnsurePlaybackDependenciesAsync(
        bool promptToInstall,
        bool confirmInstall = true,
        CancellationToken cancellationToken = default)
    {
        var missing = GetMissingPlaybackDependencies();
        if (missing.Count == 0)
            return "Playback dependencies are ready.";

        var missingNames = string.Join(", ", missing.Select(dependency => dependency.DisplayName));
        var hint = PopMarkState.IsWindows
            ? $"Type 'tools install' to put a private copy under {ToolLocator.ToolRoot}, " +
              "or 'tools set <tool> <path>' if you already have one."
            : $"Type 'tools install' to download yt-dlp and install mpv from your package manager, " +
              "or 'tools set <tool> <path>' if you already have one.";

        if (!promptToInstall)
            return $"Missing playback tool(s): {missingNames}. {hint}";

        if (confirmInstall && !ConsoleHelper.RunWithStandardInput(() =>
                AnsiConsole.Confirm($"[yellow]Install missing playback tool(s) into {Markup.Escape(ToolLocator.ToolRoot)}: {Markup.Escape(missingNames)}?[/]")))
        {
            return $"Missing playback tool(s): {missingNames}. {hint}";
        }

        Directory.CreateDirectory(ToolLocator.ToolRoot);

        foreach (var dependency in missing)
        {
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("pink1"))
                .StartAsync($"Installing {dependency.DisplayName} locally...", async _ =>
                {
                    await dependency.InstallAsync(cancellationToken);
                });
        }

        ToolLocator.Invalidate();
        ToolLocator.RefreshPathFromEnvironment();
        var stillMissing = GetMissingPlaybackDependencies();
        if (stillMissing.Count == 0)
            return $"Installed playback dependencies locally under {ToolLocator.ToolRoot}.";

        return $"Install finished, but still not usable: {string.Join(", ", stillMissing.Select(dependency => dependency.DisplayName))}. " +
               "Run 'tools' to see what PopMark resolved.";
    }

    private static IReadOnlyList<PlaybackDependency> GetMissingPlaybackDependencies() =>
        PlaybackDependencies
            .Where(dependency => ToolLocator.ResolveRunnableExecutable(dependency.CommandName) is null)
            .ToList();

    private static async Task InstallYtDlpAsync(CancellationToken cancellationToken)
    {
        var installDirectory = ToolLocator.ManagedDirectory("yt-dlp");
        Directory.CreateDirectory(installDirectory);

        var release = YtDlpReleaseForCurrentPlatform();
        var destination = Path.Combine(installDirectory, release.FileName);
        await DownloadFileAsync(
            $"https://github.com/yt-dlp/yt-dlp/releases/latest/download/{release.Asset}",
            destination,
            cancellationToken);

        ToolLocator.EnsureExecutable(destination);
    }

    /// <summary>
    /// yt-dlp publishes a self-contained binary per platform. Linux/Android get the
    /// matching build, so a phone never receives a Windows .exe.
    /// </summary>
    private static YtDlpRelease YtDlpReleaseForCurrentPlatform()
    {
        if (PopMarkState.IsWindows)
            return new YtDlpRelease("yt-dlp.exe", "yt-dlp.exe");

        if (OperatingSystem.IsMacOS())
            return new YtDlpRelease("yt-dlp_macos", "yt-dlp");

        // Native Termux runs on Bionic, where the PyInstaller builds will not start, and
        // the plain zipapp would need python. PopMark itself cannot run there either, so
        // this branch only matters for completeness. Inside the proot distro the glibc
        // builds below are the correct choice.
        if (OperatingSystem.IsAndroid())
            return new YtDlpRelease("yt-dlp", "yt-dlp");

        return RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => new YtDlpRelease("yt-dlp_linux_aarch64", "yt-dlp"),
            Architecture.Arm => new YtDlpRelease("yt-dlp_linux_armv7l", "yt-dlp"),
            Architecture.X86 => new YtDlpRelease("yt-dlp_linux_x86", "yt-dlp"),
            _ => new YtDlpRelease("yt-dlp_linux", "yt-dlp")
        };
    }

    private static async Task InstallMpvAsync(CancellationToken cancellationToken)
    {
        if (PopMarkState.IsWindows)
        {
            await InstallMpvForWindowsAsync(cancellationToken);
            return;
        }

        await InstallMpvFromPackageManagerAsync(cancellationToken);
    }

    private static async Task InstallMpvForWindowsAsync(CancellationToken cancellationToken)
    {
        var releaseAsset = await ResolveLatestMpvAssetAsync(cancellationToken);
        var archivePath = Path.Combine(ToolLocator.ToolRoot, "mpv.7z");
        var installDirectory = ToolLocator.ManagedDirectory("mpv");
        var tempDirectory = Path.Combine(ToolLocator.ToolRoot, "mpv-extract");

        await DownloadFileAsync(releaseAsset.DownloadUrl, archivePath, cancellationToken);

        if (Directory.Exists(tempDirectory))
            Directory.Delete(tempDirectory, recursive: true);

        Directory.CreateDirectory(tempDirectory);
        using (var archive = ArchiveFactory.OpenArchive(archivePath, new ReaderOptions()))
        {
            archive.WriteToDirectory(tempDirectory, new ExtractionOptions
            {
                ExtractFullPath = true,
                Overwrite = true
            });
        }

        var mpvExe = Directory
            .EnumerateFiles(tempDirectory, "mpv.exe", SearchOption.AllDirectories)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("The downloaded mpv archive did not contain mpv.exe.");

        if (Directory.Exists(installDirectory))
            Directory.Delete(installDirectory, recursive: true);

        Directory.Move(Path.GetDirectoryName(mpvExe)!, installDirectory);

        try
        {
            File.Delete(archivePath);
            if (Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, recursive: true);
        }
        catch
        {
        }
    }

    /// <summary>
    /// Linux and macOS do not have a portable mpv archive that is safe to unpack, so
    /// install through the system package manager. Inside Termux proot the user is root,
    /// which makes this work without extra prompts.
    /// </summary>
    private static async Task InstallMpvFromPackageManagerAsync(CancellationToken cancellationToken)
    {
        var packageManager = FindPackageManager();
        if (packageManager is null)
        {
            throw new InvalidOperationException(
                "mpv is not installed and no supported package manager was found. " +
                "Install mpv yourself (for example 'apt install mpv', 'pkg install mpv', or 'brew install mpv') " +
                "and run 'tools' again.");
        }

        if (packageManager.RequiresRoot && !IsRoot())
        {
            var sudo = FindOnPath("sudo");
            if (sudo is null)
                throw new InvalidOperationException(
                    $"Installing mpv with {packageManager.Name} needs root. Run '{packageManager.CommandLine}' " +
                    "in another terminal, then run 'tools' again.");

            packageManager = packageManager with { FileName = sudo, Arguments = ["-n", packageManager.FileName, .. packageManager.Arguments] };
        }

        var (exitCode, output) = await RunProcessAsync(packageManager.FileName, packageManager.Arguments, cancellationToken);
        if (exitCode != 0)
        {
            throw new InvalidOperationException(
                $"Installing mpv with {packageManager.Name} failed (exit code {exitCode}). {Summarize(output)} " +
                $"Run '{packageManager.CommandLine}' manually, then run 'tools' again.");
        }

        ToolLocator.Invalidate();
        ToolLocator.RefreshPathFromEnvironment();
    }

    private static PackageManager? FindPackageManager()
    {
        if (PopMarkState.IsTermux && FindOnPath("pkg") is not null)
            return new PackageManager("pkg", "pkg", ["install", "-y", "mpv"], RequiresRoot: false);

        if (OperatingSystem.IsMacOS())
            return FindOnPath("brew") is null
                ? null
                : new PackageManager("Homebrew", "brew", ["install", "mpv"], RequiresRoot: false);

        if (FindOnPath("apt-get") is not null)
            return new PackageManager("apt", "apt-get", ["install", "-y", "mpv"], RequiresRoot: true);

        if (FindOnPath("apk") is not null)
            return new PackageManager("apk", "apk", ["add", "--no-cache", "mpv"], RequiresRoot: true);

        if (FindOnPath("dnf") is not null)
            return new PackageManager("dnf", "dnf", ["install", "-y", "mpv"], RequiresRoot: true);

        if (FindOnPath("pacman") is not null)
            return new PackageManager("pacman", "pacman", ["-S", "--noconfirm", "mpv"], RequiresRoot: true);

        if (FindOnPath("zypper") is not null)
            return new PackageManager("zypper", "zypper", ["--non-interactive", "install", "mpv"], RequiresRoot: true);

        return null;
    }

    private static bool IsRoot()
    {
        var user = Environment.GetEnvironmentVariable("USER") ?? Environment.GetEnvironmentVariable("LOGNAME");
        return string.Equals(user, "root", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(Environment.UserName, "root", StringComparison.OrdinalIgnoreCase);
    }

    private static string? FindOnPath(string command)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
            return null;

        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var candidate = Path.Combine(directory, command);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static async Task<(int ExitCode, string Output)> RunProcessAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.Environment["DEBIAN_FRONTEND"] = "noninteractive";
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        try
        {
            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException($"Unable to start {fileName}.");

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            return (process.ExitCode, $"{await outputTask}\n{await errorTask}".Trim());
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            return (-1, ex.Message);
        }
    }

    private static string Summarize(string output)
    {
        var lastLine = output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault();

        return lastLine is null ? string.Empty : lastLine.Length > 200 ? lastLine[..200] : lastLine;
    }

    private static async Task<MpvReleaseAsset> ResolveLatestMpvAssetAsync(CancellationToken cancellationToken)
    {
        using var response = await HttpClient.GetAsync(
            "https://api.github.com/repos/shinchiro/mpv-winbuild-cmake/releases/latest",
            cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var assets = document.RootElement.GetProperty("assets").EnumerateArray()
            .Select(asset => new MpvReleaseAsset(
                asset.GetProperty("name").GetString() ?? string.Empty,
                asset.GetProperty("browser_download_url").GetString() ?? string.Empty))
            .Where(asset => asset.Name.EndsWith(".7z", StringComparison.OrdinalIgnoreCase) &&
                            asset.Name.Contains("mpv-x86_64", StringComparison.OrdinalIgnoreCase) &&
                            !asset.Name.Contains("debug", StringComparison.OrdinalIgnoreCase) &&
                            !asset.Name.Contains("dev", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var preferred = assets.FirstOrDefault(asset => !asset.Name.Contains("-v3-", StringComparison.OrdinalIgnoreCase))
            ?? assets.FirstOrDefault();

        return preferred is null || string.IsNullOrWhiteSpace(preferred.DownloadUrl)
            ? throw new InvalidOperationException("Could not find a portable Windows mpv release asset.")
            : preferred;
    }

    private static async Task DownloadFileAsync(string url, string destination, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var tempFile = destination + ".download";

        using var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using (var remote = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (var local = File.Create(tempFile))
        {
            await remote.CopyToAsync(local, cancellationToken);
        }

        if (File.Exists(destination))
            File.Delete(destination);

        File.Move(tempFile, destination);
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("PopMark/1.0");
        return client;
    }

    private sealed record PlaybackDependency(
        string DisplayName,
        string CommandName,
        Func<CancellationToken, Task> InstallAsync);

    private sealed record MpvReleaseAsset(string Name, string DownloadUrl);

    private sealed record YtDlpRelease(string Asset, string FileName);

    private sealed record PackageManager(
        string Name,
        string FileName,
        string[] Arguments,
        bool RequiresRoot)
    {
        public string CommandLine => string.Join(' ', new[] { FileName }.Concat(Arguments));
    }

    public sealed record ToolStatus(string DisplayName, string? Path, bool Runnable, string? PinnedPath);
}
