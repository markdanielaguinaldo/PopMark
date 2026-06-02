using PopMark.Models;

namespace PopMark.Services;

public sealed class PlaybackQueue
{
    private readonly YtDlpService _ytDlp;
    private readonly MpvPlayer _mpv;
    private readonly Queue<Track> _pending = new();
    private readonly Stack<Track> _previous = new();
    private readonly object _syncRoot = new();
    private Track? _current;
    private PlaybackStatus _status = PlaybackStatus.Stopped;
    private TimeSpan _positionOffset = TimeSpan.Zero;
    private DateTimeOffset? _positionStartedAt;

    public event Action<PlayerSnapshot>? SnapshotChanged;

    public PlaybackQueue(YtDlpService ytDlp, MpvPlayer mpv)
    {
        _ytDlp = ytDlp;
        _mpv = mpv;
        _mpv.PlaybackExited += AdvanceAfterTrackExitAsync;
    }

    public string LastMessage { get; set; } = "Ready.";

    public async Task AddUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        SetStatus(PlaybackStatus.Loading);

        IReadOnlyList<Track> tracks;
        try
        {
            tracks = await _ytDlp.LoadTracksAsync(url, cancellationToken);
        }
        catch
        {
            SetStatus(_current is null ? PlaybackStatus.Stopped : PlaybackStatus.Playing);
            throw;
        }

        lock (_syncRoot)
        {
            foreach (var track in tracks)
                _pending.Enqueue(track);
        }

        LastMessage = tracks.Count == 1
            ? $"Added: {tracks[0].Title}"
            : $"Added {tracks.Count} tracks.";

        await StartNextIfIdleAsync(cancellationToken);
        NotifySnapshotChanged();
    }

    public void RestoreQueue(PlayerSnapshot snapshot)
    {
        lock (_syncRoot)
        {
            _pending.Clear();
            _previous.Clear();
            _current = snapshot.Current;
            _status = PlaybackStatus.Stopped;

            if (_current is not null)
                _pending.Enqueue(_current);

            foreach (var track in snapshot.Pending)
                _pending.Enqueue(track);

            _current = null;
            ResetPositionLocked(startRunning: false);
        }

        LastMessage = snapshot.Current is null && snapshot.Pending.Count == 0
            ? "Ready."
            : $"Restored {snapshot.Pending.Count + (snapshot.Current is null ? 0 : 1)} cached track(s).";
        NotifySnapshotChanged();
    }

    public async Task PauseAsync(CancellationToken cancellationToken = default)
    {
        if (!HasCurrentTrack())
        {
            LastMessage = "Nothing is playing.";
            return;
        }

        await _mpv.PauseAsync(cancellationToken);
        lock (_syncRoot)
        {
            _positionOffset = ResolveElapsedLocked();
            _positionStartedAt = null;
            _status = PlaybackStatus.Paused;
        }
        LastMessage = "Playback paused.";
        NotifySnapshotChanged();
    }

    public async Task ResumeAsync(CancellationToken cancellationToken = default)
    {
        if (!HasCurrentTrack())
        {
            LastMessage = "Nothing is playing.";
            return;
        }

        await _mpv.ResumeAsync(cancellationToken);
        lock (_syncRoot)
        {
            _positionStartedAt = DateTimeOffset.UtcNow;
            _status = PlaybackStatus.Playing;
        }
        LastMessage = "Playback resumed.";
        NotifySnapshotChanged();
    }

    public async Task TogglePauseAsync(CancellationToken cancellationToken = default)
    {
        var shouldStart = false;
        lock (_syncRoot)
        {
            shouldStart = _current is null && _pending.Count > 0;
        }

        if (shouldStart)
        {
            await NextAsync(cancellationToken);
            return;
        }

        if (!HasCurrentTrack())
        {
            LastMessage = "Nothing is playing.";
            return;
        }

        await _mpv.TogglePauseAsync(cancellationToken);
        lock (_syncRoot)
        {
            if (_status == PlaybackStatus.Paused)
            {
                _status = PlaybackStatus.Playing;
                _positionStartedAt = DateTimeOffset.UtcNow;
            }
            else
            {
                _positionOffset = ResolveElapsedLocked();
                _positionStartedAt = null;
                _status = PlaybackStatus.Paused;
            }

            LastMessage = _status == PlaybackStatus.Paused ? "Playback paused." : "Playback resumed.";
        }
        NotifySnapshotChanged();
    }

    public async Task NextAsync(CancellationToken cancellationToken = default)
    {
        Track? next;
        lock (_syncRoot)
        {
            if (_current is not null)
                _previous.Push(_current);

            next = _pending.Count > 0 ? _pending.Dequeue() : null;
            if (next is null)
            {
                _current = null;
                _status = PlaybackStatus.Stopped;
                ResetPositionLocked(startRunning: false);
            }
            else
            {
                _current = next;
                _status = PlaybackStatus.Playing;
                ResetPositionLocked(startRunning: true);
            }
        }

        if (next is null)
        {
            await _mpv.StopAsync(cancellationToken);
            LastMessage = "End of queue.";
            return;
        }

        await _mpv.PlayAsync(next, cancellationToken);
        LastMessage = $"Now playing: {next.Title}";
        NotifySnapshotChanged();
    }

    public async Task PreviousAsync(CancellationToken cancellationToken = default)
    {
        Track? previous;
        lock (_syncRoot)
        {
            previous = _previous.Count > 0 ? _previous.Pop() : null;
            if (previous is null)
            {
                LastMessage = "No previous track.";
                return;
            }

            if (_current is not null)
                PrependPendingLocked(_current);

            _current = previous;
            _status = PlaybackStatus.Playing;
            ResetPositionLocked(startRunning: true);
        }

        await _mpv.PlayAsync(previous, cancellationToken);
        LastMessage = $"Returned to: {previous.Title}";
        NotifySnapshotChanged();
    }

    public async Task SeekRelativeAsync(int seconds, CancellationToken cancellationToken = default)
    {
        if (!HasCurrentTrack())
        {
            LastMessage = "Nothing is playing.";
            return;
        }

        await _mpv.SeekRelativeAsync(seconds, cancellationToken);
        lock (_syncRoot)
        {
            var elapsed = ResolveElapsedLocked().Add(TimeSpan.FromSeconds(seconds));
            _positionOffset = ClampElapsedLocked(elapsed);
            _positionStartedAt = _status == PlaybackStatus.Playing ? DateTimeOffset.UtcNow : null;
        }

        LastMessage = seconds >= 0
            ? $"Skipped forward {seconds} second(s)."
            : $"Rewound {Math.Abs(seconds)} second(s).";
        NotifySnapshotChanged();
    }

    public async Task StopAsync(bool clearQueue, CancellationToken cancellationToken = default)
    {
        lock (_syncRoot)
        {
            _current = null;
            _status = PlaybackStatus.Stopped;
            ResetPositionLocked(startRunning: false);
            if (clearQueue)
            {
                _pending.Clear();
                _previous.Clear();
            }
        }

        await _mpv.StopAsync(cancellationToken);
        LastMessage = clearQueue ? "Stopped playback and cleared queue." : "Stopped playback.";
        NotifySnapshotChanged();
    }

    public async Task ClearPlaylistAsync(CancellationToken cancellationToken = default)
    {
        lock (_syncRoot)
        {
            _current = null;
            _pending.Clear();
            _previous.Clear();
            _status = PlaybackStatus.Stopped;
            ResetPositionLocked(startRunning: false);
        }

        await _mpv.StopAsync(cancellationToken);
        LastMessage = "Playlist cleared.";
        NotifySnapshotChanged();
    }

    public async Task StopPlaybackAndKeepQueueAsync(CancellationToken cancellationToken = default)
    {
        lock (_syncRoot)
        {
            _positionOffset = ResolveElapsedLocked();
            _positionStartedAt = null;
            _status = PlaybackStatus.Stopped;
        }

        await _mpv.StopAsync(cancellationToken);
        LastMessage = "Stopped playback. Queue saved for next launch.";
        NotifySnapshotChanged();
    }

    public async Task DetachAsync(CancellationToken cancellationToken = default)
    {
        List<Track> tracksToAppend;
        lock (_syncRoot)
        {
            tracksToAppend = _pending.ToList();
        }

        foreach (var track in tracksToAppend)
            await _mpv.AppendAsync(track, cancellationToken);

        lock (_syncRoot)
        {
            _pending.Clear();
            _positionOffset = ResolveElapsedLocked();
            _positionStartedAt = null;
            _status = PlaybackStatus.Detached;
        }

        _mpv.Detach();
        LastMessage = tracksToAppend.Count == 0
            ? "Detached. mpv will continue the current track."
            : $"Detached. mpv owns the remaining {tracksToAppend.Count} queued track(s).";
        NotifySnapshotChanged();
    }

    public PlayerSnapshot CreateSnapshot()
    {
        lock (_syncRoot)
        {
            return new PlayerSnapshot(_status, _current, _pending.ToList(), ResolveElapsedLocked());
        }
    }

    private async Task StartNextIfIdleAsync(CancellationToken cancellationToken)
    {
        Track? next = null;

        lock (_syncRoot)
        {
            if (_current is not null || _pending.Count == 0)
                return;

            next = _pending.Dequeue();
            _current = next;
            _status = PlaybackStatus.Playing;
            ResetPositionLocked(startRunning: true);
        }

        await _mpv.PlayAsync(next, cancellationToken);
        LastMessage = $"Now playing: {next.Title}";
    }

    private async Task AdvanceAfterTrackExitAsync()
    {
        Track? next = null;
        lock (_syncRoot)
        {
            if (_pending.Count > 0)
            {
                if (_current is not null)
                    _previous.Push(_current);

                next = _pending.Dequeue();
                _current = next;
                _status = PlaybackStatus.Playing;
                ResetPositionLocked(startRunning: true);
            }
            else
            {
                if (_current is not null)
                    _previous.Push(_current);

                _current = null;
                _status = PlaybackStatus.Stopped;
                ResetPositionLocked(startRunning: false);
                LastMessage = "Queue finished.";
            }
        }
        NotifySnapshotChanged();

        if (next is null)
            return;

        try
        {
            await _mpv.PlayAsync(next);
            LastMessage = $"Now playing: {next.Title}";
            NotifySnapshotChanged();
        }
        catch (Exception ex)
        {
            LastMessage = $"Failed to start next track: {ex.Message}";
            SetStatus(PlaybackStatus.Stopped);
        }
    }

    private bool HasCurrentTrack()
    {
        lock (_syncRoot)
        {
            return _current is not null && _status is PlaybackStatus.Playing or PlaybackStatus.Paused;
        }
    }

    private void ResetPositionLocked(bool startRunning)
    {
        _positionOffset = TimeSpan.Zero;
        _positionStartedAt = startRunning ? DateTimeOffset.UtcNow : null;
    }

    private void PrependPendingLocked(Track track)
    {
        var existing = _pending.ToList();
        _pending.Clear();
        _pending.Enqueue(track);

        foreach (var pendingTrack in existing)
            _pending.Enqueue(pendingTrack);
    }

    private TimeSpan ResolveElapsedLocked()
    {
        var elapsed = _positionOffset;
        if (_status == PlaybackStatus.Playing && _positionStartedAt is not null)
            elapsed += DateTimeOffset.UtcNow - _positionStartedAt.Value;

        return ClampElapsedLocked(elapsed);
    }

    private TimeSpan ClampElapsedLocked(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero)
            return TimeSpan.Zero;

        if (_current?.Duration is { } duration && elapsed > duration)
            return duration;

        return elapsed;
    }

    private void SetStatus(PlaybackStatus status)
    {
        lock (_syncRoot)
        {
            _status = status;
        }
        NotifySnapshotChanged();
    }

    private void NotifySnapshotChanged() => SnapshotChanged?.Invoke(CreateSnapshot());
}
