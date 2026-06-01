using PopMark.Models;

namespace PopMark.Services;

public sealed class PlaybackQueue
{
    private readonly YtDlpService _ytDlp;
    private readonly MpvPlayer _mpv;
    private readonly Queue<Track> _pending = new();
    private readonly object _syncRoot = new();
    private Track? _current;
    private PlaybackStatus _status = PlaybackStatus.Stopped;

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
            _current = snapshot.Current;
            _status = PlaybackStatus.Stopped;

            if (_current is not null)
                _pending.Enqueue(_current);

            foreach (var track in snapshot.Pending)
                _pending.Enqueue(track);

            _current = null;
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
        SetStatus(PlaybackStatus.Paused);
        LastMessage = "Playback paused.";
    }

    public async Task ResumeAsync(CancellationToken cancellationToken = default)
    {
        if (!HasCurrentTrack())
        {
            LastMessage = "Nothing is playing.";
            return;
        }

        await _mpv.ResumeAsync(cancellationToken);
        SetStatus(PlaybackStatus.Playing);
        LastMessage = "Playback resumed.";
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
            _status = _status == PlaybackStatus.Paused ? PlaybackStatus.Playing : PlaybackStatus.Paused;
            LastMessage = _status == PlaybackStatus.Paused ? "Playback paused." : "Playback resumed.";
        }
        NotifySnapshotChanged();
    }

    public async Task NextAsync(CancellationToken cancellationToken = default)
    {
        Track? next;
        lock (_syncRoot)
        {
            next = _pending.Count > 0 ? _pending.Dequeue() : null;
            if (next is null)
            {
                _current = null;
                _status = PlaybackStatus.Stopped;
            }
            else
            {
                _current = next;
                _status = PlaybackStatus.Playing;
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

    public async Task StopAsync(bool clearQueue, CancellationToken cancellationToken = default)
    {
        lock (_syncRoot)
        {
            _current = null;
            _status = PlaybackStatus.Stopped;
            if (clearQueue)
                _pending.Clear();
        }

        await _mpv.StopAsync(cancellationToken);
        LastMessage = clearQueue ? "Stopped playback and cleared queue." : "Stopped playback.";
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
            return new PlayerSnapshot(_status, _current, _pending.ToList());
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
                next = _pending.Dequeue();
                _current = next;
                _status = PlaybackStatus.Playing;
            }
            else
            {
                _current = null;
                _status = PlaybackStatus.Stopped;
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
