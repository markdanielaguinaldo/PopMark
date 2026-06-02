using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
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

    public async Task<string> EnsurePlaybackDependenciesAsync(
        bool promptToInstall,
        bool confirmInstall = true,
        CancellationToken cancellationToken = default)
    {
        var missing = GetMissingPlaybackDependencies();
        if (missing.Count == 0)
            return "Playback dependencies are ready.";

        var missingNames = string.Join(", ", missing.Select(dependency => dependency.DisplayName));
        if (!promptToInstall)
            return $"Missing playback dependencies: {missingNames}. Start PopMark interactively to install them locally.";

        if (confirmInstall && !AnsiConsole.Confirm($"[yellow]Install missing playback tool(s) locally: {Markup.Escape(missingNames)}?[/]"))
            return $"Missing playback dependencies: {missingNames}.";

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

        ToolLocator.RefreshPathFromEnvironment();
        var stillMissing = GetMissingPlaybackDependencies();
        if (stillMissing.Count == 0)
            return $"Installed playback dependencies locally under {ToolLocator.ToolRoot}.";

        return $"Install finished, but still not found: {string.Join(", ", stillMissing.Select(dependency => dependency.DisplayName))}.";
    }

    private static IReadOnlyList<PlaybackDependency> GetMissingPlaybackDependencies() =>
        PlaybackDependencies
            .Where(dependency => ToolLocator.ResolveExecutable(dependency.CommandName) is null)
            .ToList();

    private static async Task InstallYtDlpAsync(CancellationToken cancellationToken)
    {
        var installDirectory = Path.Combine(ToolLocator.ToolRoot, "yt-dlp");
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
        var installDirectory = Path.Combine(ToolLocator.ToolRoot, "mpv");
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
}
