namespace PopMark.Models;

public sealed record PlayerSnapshot(
    PlaybackStatus Status,
    Track? Current,
    IReadOnlyList<Track> Pending,
    TimeSpan? Elapsed = null);
