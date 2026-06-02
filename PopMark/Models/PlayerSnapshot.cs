namespace PopMark.Models;

public sealed record PlayerSnapshot(
    PlaybackStatus Status,
    Track? Current,
    IReadOnlyList<Track> Pending,
    IReadOnlyList<Track> Previous,
    TimeSpan? Elapsed = null);
