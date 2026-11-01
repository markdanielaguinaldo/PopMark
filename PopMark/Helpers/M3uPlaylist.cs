using PopMark.Models;
using System.Text;

namespace PopMark.Helpers;

/// <summary>
/// Reads and writes the M3U subset PopMark needs: EXTINF title/duration lines
/// followed by one http(s) URL. Local file paths are ignored because playback
/// goes through yt-dlp and mpv URLs.
/// </summary>
public static class M3uPlaylist
{
    public static string Serialize(IEnumerable<Track> tracks)
    {
        var builder = new StringBuilder();
        builder.AppendLine("#EXTM3U");

        foreach (var track in tracks)
        {
            var seconds = track.Duration is { } duration && duration > TimeSpan.Zero
                ? (long)Math.Round(duration.TotalSeconds)
                : -1;

            builder.Append("#EXTINF:")
                .Append(seconds)
                .Append(',')
                .Append(SingleLine(track.Title))
                .AppendLine();
            builder.AppendLine(track.Url);
        }

        return builder.ToString();
    }

    public static IReadOnlyList<Track> Parse(string contents)
    {
        var tracks = new List<Track>();
        string? title = null;
        string? source = null;
        TimeSpan? duration = null;

        foreach (var rawLine in contents.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim().TrimStart('\uFEFF');
            if (line.Length == 0)
                continue;

            if (line.StartsWith("#EXTINF:", StringComparison.OrdinalIgnoreCase))
            {
                ParseExtInf(line["#EXTINF:".Length..], out duration, out title);
                continue;
            }

            if (line.StartsWith("#EXTGRP:", StringComparison.OrdinalIgnoreCase))
            {
                source = Clean(line["#EXTGRP:".Length..], 120);
                continue;
            }

            if (line.StartsWith('#'))
                continue;

            if (!IsHttpUrl(line))
            {
                title = null;
                duration = null;
                source = null;
                continue;
            }

            tracks.Add(new Track(
                title ?? FallbackTitle(line),
                line,
                source ?? "Imported",
                duration));

            title = null;
            duration = null;
            source = null;
        }

        return tracks;
    }

    private static void ParseExtInf(string value, out TimeSpan? duration, out string? title)
    {
        duration = null;
        title = null;

        var comma = value.IndexOf(',');
        var durationText = comma >= 0 ? value[..comma] : value;
        if (long.TryParse(durationText.Trim(), out var seconds) && seconds > 0)
            duration = TimeSpan.FromSeconds(seconds);

        if (comma < 0)
            return;

        title = Clean(value[(comma + 1)..], 300);
        if (string.IsNullOrWhiteSpace(title))
            title = null;
    }

    private static string FallbackTitle(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? uri.Host
            : "Imported track";

    private static bool IsHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
         uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));

    private static string SingleLine(string value) =>
        Clean(value, 300).Replace(',', ' ');

    private static string Clean(string value, int maxLength)
    {
        var builder = new StringBuilder(Math.Min(value.Length, maxLength));
        foreach (var character in value)
        {
            if (builder.Length >= maxLength)
                break;

            if (!char.IsControl(character))
                builder.Append(character);
        }

        return builder.ToString().Trim();
    }
}
