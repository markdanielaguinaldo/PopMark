namespace PopMark.Models;

public sealed record Track(
    string Title,
    string Url,
    string DisplaySource,
    TimeSpan? Duration = null);
