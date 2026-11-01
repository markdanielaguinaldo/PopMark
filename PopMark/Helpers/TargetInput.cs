namespace PopMark.Helpers;

/// <summary>
/// Normalizes what the user typed before it reaches yt-dlp. Rejects option-like
/// strings and non-HTTP schemes, and turns plain text into a yt-dlp search.
/// </summary>
internal static class TargetInput
{
    private const int MaxLength = 2048;

    public static string Normalize(string value)
    {
        var target = value.Trim();

        if (target.Length == 0)
            throw new InvalidOperationException("Enter a YouTube URL or a search.");

        if (target.Length > MaxLength)
            throw new InvalidOperationException("That URL or search is too long.");

        if (target[0] == '-')
            throw new InvalidOperationException("URLs and searches cannot start with '-'.");

        foreach (var character in target)
        {
            if (char.IsControl(character))
                throw new InvalidOperationException("That URL or search contains control characters.");
        }

        if (target.StartsWith("ytsearch", StringComparison.OrdinalIgnoreCase))
            return target;

        if (Uri.TryCreate(target, UriKind.Absolute, out var uri))
        {
            if (!uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Only http and https URLs are supported.");
            }

            return target;
        }

        return $"ytsearch1:{target}";
    }
}
