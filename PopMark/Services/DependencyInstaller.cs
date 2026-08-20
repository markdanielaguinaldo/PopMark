using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using PopMark.Helpers;
using Spectre.Console;
using System.Diagnostics;
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

        // The pin makes the fresh copy win over whatever was found earlier, so a broken
        // system install can never keep taking priority after a repair.
        ToolLocator.SetOverride(dependency.CommandName, ToolLocator.ManagedExecutablePath(dependency.CommandName));
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
        var hint = $"Type 'tools install' to put a private copy under {ToolLocator.ToolRoot}, " +
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

        var destination = Path.Combine(installDirectory, "yt-dlp.exe");
        await DownloadFileAsync(
            "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe",
            destination,
            cancellationToken);
    }

    private static async Task InstallMpvAsync(CancellationToken cancellationToken)
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

    public sealed record ToolStatus(string DisplayName, string? Path, bool Runnable, string? PinnedPath);
}
