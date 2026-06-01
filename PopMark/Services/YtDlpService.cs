using PopMark.Models;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace PopMark.Services;

public sealed class YtDlpService
{
    public async Task<IReadOnlyList<Track>> LoadTracksAsync(string url, CancellationToken cancellationToken = default)
    {
        var result = await RunYtDlpAsync(url, cancellationToken);
        if (result.ExitCode != 0)
            throw new InvalidOperationException(BuildYtDlpError(result.Error));

        using var document = JsonDocument.Parse(result.Output);
        var root = document.RootElement;
        var tracks = new List<Track>();

        if (root.TryGetProperty("entries", out var entries) && entries.ValueKind == JsonValueKind.Array)
        {
            var playlistTitle = GetString(root, "title") ?? "YouTube playlist";
            foreach (var entry in entries.EnumerateArray())
            {
                if (entry.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                    continue;

                var trackUrl = ResolveTrackUrl(entry);
                if (string.IsNullOrWhiteSpace(trackUrl))
                    continue;

                tracks.Add(new Track(
                    GetString(entry, "title") ?? $"Track {tracks.Count + 1}",
                    trackUrl,
                    playlistTitle,
                    GetDuration(entry)));
            }
        }
        else
        {
            var trackUrl = ResolveTrackUrl(root) ?? url;
            tracks.Add(new Track(
                GetString(root, "title") ?? "YouTube track",
                trackUrl,
                GetString(root, "uploader") ?? "YouTube",
                GetDuration(root)));
        }

        return tracks.Count == 0
            ? throw new InvalidOperationException("yt-dlp did not return playable tracks for that URL.")
            : tracks;
    }

    private static async Task<ProcessResult> RunYtDlpAsync(string url, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ToolLocator.ResolveExecutable("yt-dlp") ?? "yt-dlp",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("--dump-single-json");
        startInfo.ArgumentList.Add("--flat-playlist");
        startInfo.ArgumentList.Add("--ignore-errors");
        startInfo.ArgumentList.Add("--no-warnings");
        startInfo.ArgumentList.Add(url);

        try
        {
            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Unable to start yt-dlp.");

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            return new ProcessResult(process.ExitCode, await outputTask, await errorTask);
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new InvalidOperationException("yt-dlp was not found. Install it and make sure it is available on PATH.", ex);
        }
    }

    private static string BuildYtDlpError(string error)
    {
        if (string.IsNullOrWhiteSpace(error))
            return "yt-dlp failed to load that URL.";

        return $"yt-dlp failed: {error.Trim()}";
    }

    private static string? ResolveTrackUrl(JsonElement element)
    {
        var url = GetString(element, "webpage_url")
            ?? GetString(element, "original_url")
            ?? GetString(element, "url");

        if (string.IsNullOrWhiteSpace(url))
            return null;

        if (Uri.TryCreate(url, UriKind.Absolute, out _))
            return url;

        var id = GetString(element, "id") ?? url;
        return string.IsNullOrWhiteSpace(id)
            ? null
            : $"https://www.youtube.com/watch?v={Uri.EscapeDataString(id)}";
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static TimeSpan? GetDuration(JsonElement element)
    {
        if (!element.TryGetProperty("duration", out var property))
            return null;

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetDouble(out var seconds) => TimeSpan.FromSeconds(seconds),
            _ => null
        };
    }

    private sealed record ProcessResult(int ExitCode, string Output, string Error);
}
