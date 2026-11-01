using System.Diagnostics.CodeAnalysis;

namespace PopMark.Helpers;

/// <summary>
/// Stable comparison key for a track URL. YouTube links come in several shapes
/// (watch, youtu.be, shorts), so duplicate detection works on the video id.
/// </summary>
internal static class TrackIdentity
{
    public static string Key(string url)
    {
        var trimmed = url.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
            !(uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
              uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            return trimmed.ToLowerInvariant();
        }

        if (TryGetYouTubeId(uri, out var videoId))
            return $"youtube:{videoId.ToLowerInvariant()}";

        var builder = new UriBuilder(uri)
        {
            Fragment = string.Empty,
            Host = uri.Host.ToLowerInvariant()
        };

        return builder.Uri.AbsoluteUri.TrimEnd('/');
    }

    public static bool Same(string left, string right) =>
        string.Equals(Key(left), Key(right), StringComparison.OrdinalIgnoreCase);

    private static bool TryGetYouTubeId(Uri uri, [NotNullWhen(true)] out string? videoId)
    {
        videoId = null;
        var host = uri.Host.ToLowerInvariant();
        if (host is "youtu.be" or "www.youtu.be")
        {
            videoId = uri.AbsolutePath.Trim('/').Split('/', 2)[0];
            return !string.IsNullOrWhiteSpace(videoId);
        }

        if (host is not ("youtube.com" or "www.youtube.com" or "m.youtube.com" or "music.youtube.com"))
            return false;

        var path = uri.AbsolutePath.Trim('/');
        if (path.StartsWith("shorts/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("embed/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("live/", StringComparison.OrdinalIgnoreCase))
        {
            videoId = path.Split('/', 3)[1];
            return !string.IsNullOrWhiteSpace(videoId);
        }

        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 && parts[0].Equals("v", StringComparison.OrdinalIgnoreCase))
            {
                videoId = Uri.UnescapeDataString(parts[1]);
                return !string.IsNullOrWhiteSpace(videoId);
            }
        }

        return false;
    }
}
