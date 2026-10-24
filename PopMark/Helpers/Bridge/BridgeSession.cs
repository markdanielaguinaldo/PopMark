using PopMark.Helpers;
using PopMark.Models;
using PopMark.Services;
using System.Text.Json;
using System.Threading.Channels;

namespace PopMark.Helpers.Bridge;

/// <summary>
/// Owns the backend half of the OpenTUI bridge: JSON state out on stdout,
/// structured JSON commands in on stdin. Playback itself stays in <see cref="PlaybackQueue"/>.
/// </summary>
internal sealed class BridgeSession
{
    private readonly PlaybackQueue _player;
    private readonly DependencyInstaller _dependencies;
    private readonly TextWriter _writer;
    private readonly string _version;
    private readonly Channel<BridgeRequest> _requests = Channel.CreateUnbounded<BridgeRequest>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
    private readonly CancellationTokenSource _shutdown = new();
    private readonly object _writeLock = new();
    private readonly object _emitLock = new();
    private long _queueRevision;
    private string _lastQueueSignature = string.Empty;
    private string _lastStateSignature = string.Empty;
    private bool _busy;
    private bool _pipeClosed;

    public BridgeSession(
        PlaybackQueue player,
        DependencyInstaller dependencies,
        TextWriter writer,
        string version)
    {
        _player = player;
        _dependencies = dependencies;
        _writer = writer;
        _version = version;
    }

    public async Task<int> RunAsync()
    {
        _player.SnapshotChanged += HandleSnapshotChanged;

        try
        {
            Write(BridgeHelloDto.Create(_version));

            // Give the UI queue/state immediately; tool probing can be slower
            // (it runs each executable with --version) and must not block the
            // first usable frame.
            EmitQueue(force: true);
            EmitState(force: true);

            var dependencyMessage = await _dependencies.EnsurePlaybackDependenciesAsync(
                promptToInstall: false);
            if (!_dependencies.ArePlaybackDependenciesAvailable())
                _player.LastMessage = dependencyMessage;

            EmitTools();
            EmitState(force: true);

            var stdinTask = ReadStandardInputAsync();
            var tickTask = TickAsync();

            await foreach (var request in _requests.Reader.ReadAllAsync())
            {
                if (!await HandleRequestAsync(request))
                    break;
            }

            _shutdown.Cancel();
            await _player.StopPlaybackAndKeepQueueAsync(CancellationToken.None);
            Write(new BridgeByeDto("bye"));

            // The stdin reader may be parked in a blocking console read; it is a
            // background thread pool task, so let it finish on its own.
            _ = stdinTask;
            _ = tickTask;
            return 0;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        finally
        {
            _player.SnapshotChanged -= HandleSnapshotChanged;
        }
    }

    /// <summary>Cancels the session from outside (console control events, pipe loss).</summary>
    public void RequestShutdown()
    {
        try
        {
            _shutdown.Cancel();
        }
        catch
        {
        }

        _requests.Writer.TryComplete();
    }

    private async Task ReadStandardInputAsync()
    {
        // Do not use Console.In here: when the bridge is spawned from a process
        // that still owns a console (for example inside a terminal UI), .NET can
        // bind Console.In to that console instead of the redirected pipe. The
        // raw standard-input stream is always the pipe the parent gave us.
        using var input = new StreamReader(
            Console.OpenStandardInput(),
            new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 4096,
            leaveOpen: true);

        try
        {
            while (!_shutdown.IsCancellationRequested)
            {
                var line = await input.ReadLineAsync(_shutdown.Token).ConfigureAwait(false);
                if (line is null)
                    break;

                if (!BridgeRequest.TryParse(line, out var request, out var error))
                {
                    Write(BridgeErrorDto.Create(null, error ?? "Invalid bridge request."));
                    continue;
                }

                _requests.Writer.TryWrite(request!);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Write(BridgeErrorDto.Create(null, $"stdin reader failed: {ex.Message}"));
        }
        finally
        {
            _requests.Writer.TryComplete();
        }
    }

    private async Task TickAsync()
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));
            while (await timer.WaitForNextTickAsync(_shutdown.Token).ConfigureAwait(false))
            {
                // time-pos comes from mpv itself; the estimator inside
                // PlaybackQueue is only a fallback while IPC is catching up.
                var snapshot = _player.CreateSnapshot();
                if (snapshot.Status == PlaybackStatus.Playing)
                    await _player.RefreshPositionAsync(_shutdown.Token);

                EmitState(force: false);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task<bool> HandleRequestAsync(BridgeRequest request)
    {
        try
        {
            switch (request.Type.ToLowerInvariant())
            {
                case "ping":
                    Write(BridgeAckDto.Success(request.Id, "pong"));
                    return true;

                case "command":
                    return await HandleCommandRequestAsync(request);

                case "input":
                    return await HandleInputAsync(request.Text ?? request.Value ?? string.Empty, request.Id);

                case "add":
                    await RunBusyAsync("Adding to queue...", () =>
                        AddTargetAsync(request.Target ?? request.Value ?? request.Text ?? string.Empty));
                    return true;

                case "seek":
                    await HandleSeekAsync(request);
                    return true;

                case "volume":
                    await HandleVolumeAsync(request);
                    return true;

                case "play-index":
                    await HandlePlayIndexAsync(request);
                    return true;

                case "tools":
                    await HandleToolsAsync(request);
                    return true;

                case "quit":
                case "shutdown":
                    Write(BridgeAckDto.Success(request.Id, "Shutting down."));
                    return false;

                default:
                    Write(BridgeAckDto.Failure(request.Id, $"Unknown bridge request type: {request.Type}"));
                    return true;
            }
        }
        catch (Exception ex)
        {
            _player.LastMessage = ex.Message;
            Write(BridgeAckDto.Failure(request.Id, ex.Message));
            EmitState(force: true);
            return true;
        }
    }

    private async Task<bool> HandleCommandRequestAsync(BridgeRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Text))
            return await HandleInputAsync(request.Text, request.Id);

        var name = (request.Name ?? request.Value ?? string.Empty).Trim().ToLowerInvariant();

        switch (name)
        {
            case "play":
            case "pause":
            case "resume":
            case "toggle":
                await _player.TogglePauseAsync();
                return Acknowledge(request.Id);

            case "next":
            case "skip":
                await _player.NextAsync();
                return Acknowledge(request.Id);

            case "previous":
            case "prev":
            case "back":
                await _player.PreviousAsync();
                return Acknowledge(request.Id);

            case "stop":
                await _player.StopAsync(clearQueue: true);
                return Acknowledge(request.Id);

            case "clear-playlist":
            case "clearplaylist":
                await _player.ClearPlaylistAsync();
                return Acknowledge(request.Id);

            case "shuffle":
                _player.ShufflePlaylist();
                return Acknowledge(request.Id);

            case "seek-forward":
            case "seek-back":
            {
                var seconds = request.Seconds ?? request.Delta ?? 10;
                var direction = name == "seek-forward" ? 1 : -1;
                await _player.SeekRelativeAsync((int)Math.Round(direction * Math.Abs(seconds)));
                EmitQueue(force: true);
                return Acknowledge(request.Id);
            }

            case "volume-up":
            case "volume-down":
            {
                var amount = request.Percent ?? request.Delta ?? 10;
                var direction = name == "volume-up" ? 1 : -1;
                await _player.AdjustVolumeAsync((int)Math.Round(direction * Math.Abs(amount)));
                return Acknowledge(request.Id);
            }

            case "version":
                _player.LastMessage = Program.VersionDisplay();
                return Acknowledge(request.Id, _player.LastMessage);

            case "tools":
                await HandleToolsAsync(new BridgeRequest("tools", request.Id, Action: request.Action));
                return true;

            case "quit":
            case "exit":
                Write(BridgeAckDto.Success(request.Id, "Shutting down."));
                return false;

            case "help":
            case "controls":
                Write(BridgeAckDto.Success(request.Id, "Commands are available in the command palette."));
                return true;

            case "add":
                await RunBusyAsync("Adding to queue...", () =>
                    AddTargetAsync(request.Target ?? request.Value ?? string.Empty));
                return Acknowledge(request.Id);

            default:
                Write(BridgeAckDto.Failure(request.Id, Program.UnknownCommandMessage(name)));
                return true;
        }
    }

    private async Task<bool> HandleInputAsync(string input, string? id)
    {
        var args = ConsoleHelper.SplitArgs(input);
        if (args.Length == 0)
        {
            Write(BridgeAckDto.Success(id));
            return true;
        }

        var command = args[0].ToLowerInvariant();

        switch (command)
        {
            case "add":
                if (!Program.TryResolveAddTarget(args, out var target))
                {
                    Write(BridgeAckDto.Failure(id, Program.AddUsageMessage()));
                    return true;
                }

                await RunBusyAsync("Adding to queue...", () => AddTargetAsync(target));
                return Acknowledge(id);

            case "play":
            case "pause":
            case "r":
            case "resume":
                await _player.TogglePauseAsync();
                return Acknowledge(id);

            case "next":
            case "skip":
            case "n":
            case "]":
                await _player.NextAsync();
                return Acknowledge(id);

            case "previous":
            case "prev":
            case "back":
            case "[":
                await _player.PreviousAsync();
                return Acknowledge(id);

            case "stop":
            case "s":
                await _player.StopAsync(clearQueue: true);
                return Acknowledge(id);

            case "clear-playlist":
            case "clearplaylist":
                await _player.ClearPlaylistAsync();
                return Acknowledge(id);

            case "clear":
            {
                if (args.Length > 1 && args[1].Equals("playlist", StringComparison.OrdinalIgnoreCase))
                {
                    await _player.ClearPlaylistAsync();
                    return Acknowledge(id);
                }

                Write(BridgeAckDto.Failure(id, Program.UnknownCommandMessage("clear")));
                return true;
            }

            case "cls":
                _player.LastMessage = "The OpenTUI client owns the screen.";
                return Acknowledge(id);

            case "shuffle":
            case "shuf":
                _player.ShufflePlaylist();
                return Acknowledge(id);

            case "goto":
            case "go-to":
            case "jump":
            case "song":
            case "go":
            {
                var targetStart = command == "go" ? 2 : 1;
                var targetText = args.Length > targetStart
                    ? string.Join(' ', args.Skip(targetStart)).Trim()
                    : string.Empty;

                var resolved = _player.ResolvePlaylistIndex(targetText);
                _player.LastMessage = string.IsNullOrWhiteSpace(targetText)
                    ? "Usage: goto <playlist number or song title>."
                    : resolved is null
                        ? $"No playlist song matches: {targetText}"
                        : $"Playlist moved to song {resolved + 1}.";
                return Acknowledge(id, _player.LastMessage);
            }

            case "tools":
            case "tool":
            case "doctor":
                await RunBusyAsync("Checking playback tools...", async () =>
                {
                    var action = args.Length > 1 ? args[1] : "status";
                    var report = await Program.HandleToolsCommandAsync(_dependencies, args, interactive: false);
                    EmitToolReport(report);
                    EmitTools();
                    _player.LastMessage = SummarizeTools(action, report);
                });
                return Acknowledge(id, _player.LastMessage);

            case "quit":
            case "exit":
            case "q":
                Write(BridgeAckDto.Success(id, "Shutting down."));
                return false;

            case "version":
            case "ver":
                _player.LastMessage = Program.VersionDisplay();
                return Acknowledge(id, _player.LastMessage);

            case "help":
            case "h":
            case "?":
            case "controls":
            case "control":
            case "splash":
            case "art":
            case "visual":
            case "visualizer":
            case "playlist":
            case "queue":
            case "menu":
                Write(BridgeAckDto.Success(id, "The OpenTUI client renders views directly."));
                return true;

            default:
                if (Program.IsLikelyUrl(input))
                {
                    await RunBusyAsync("Adding to queue...", () => AddTargetAsync(input));
                    return Acknowledge(id);
                }

                Write(BridgeAckDto.Failure(id, Program.UnknownCommandMessage(command)));
                return true;
        }
    }

    private async Task AddTargetAsync(string target)
    {
        target = target.Trim();
        if (string.IsNullOrWhiteSpace(target))
        {
            _player.LastMessage = Program.AddUsageMessage();
            return;
        }

        var dependencyMessage = await _dependencies.EnsurePlaybackDependenciesAsync(
            promptToInstall: false);
        if (!_dependencies.ArePlaybackDependenciesAvailable())
        {
            _player.LastMessage = dependencyMessage;
            EmitTools();
            return;
        }

        await _player.AddUrlAsync(target);
    }

    private async Task HandleSeekAsync(BridgeRequest request)
    {
        if (request.Seconds is { } seconds)
        {
            await _player.SeekAbsoluteAsync(TimeSpan.FromSeconds(seconds));
        }
        else if (request.Delta is { } delta)
        {
            await _player.SeekRelativeAsync((int)Math.Round(delta));
        }

        EmitState(force: true);
    }

    private async Task HandleVolumeAsync(BridgeRequest request)
    {
        if (request.Percent is { } percent)
        {
            var snapshot = _player.CreateSnapshot();
            await _player.AdjustVolumeAsync(percent - snapshot.VolumePercent);
        }
        else if (request.Delta is { } delta)
        {
            await _player.AdjustVolumeAsync((int)Math.Round(delta));
        }

        EmitState(force: true);
    }

    private async Task HandlePlayIndexAsync(BridgeRequest request)
    {
        if (request.Index is not { } index)
        {
            Write(BridgeAckDto.Failure(request.Id, "play-index requires an index."));
            return;
        }

        await _player.PlayAtQueueIndexAsync(index);
        EmitQueue(force: true);
        EmitState(force: true);
        Write(BridgeAckDto.Success(request.Id));
    }

    private async Task HandleToolsAsync(BridgeRequest request)
    {
        var action = (request.Action ?? request.Name ?? "status").Trim().ToLowerInvariant();
        var args = new List<string> { "tools" };

        args.Add(action);

        if (!string.IsNullOrWhiteSpace(request.Tool))
            args.Add(request.Tool);

        if (!string.IsNullOrWhiteSpace(request.Path))
            args.Add(request.Path);

        var label = action is "install" or "repair" or "fix"
            ? "Installing playback tools..."
            : "Checking playback tools...";

        await RunBusyAsync(label, async () =>
        {
            var report = await Program.HandleToolsCommandAsync(
                _dependencies,
                args.ToArray(),
                interactive: false);

            EmitToolReport(report);
            EmitTools();
            _player.LastMessage = SummarizeTools(action, report);
        });

        Write(BridgeAckDto.Success(request.Id, _player.LastMessage));
    }

    private async Task RunBusyAsync(string label, Func<Task> action)
    {
        SetBusy(true, label);
        try
        {
            await action();
        }
        finally
        {
            SetBusy(false, null);
        }
    }

    private void SetBusy(bool busy, string? label)
    {
        _busy = busy;
        Write(busy ? BridgeBusyDto.Start(label ?? string.Empty) : BridgeBusyDto.Stop());
        EmitState(force: true);
    }

    private bool Acknowledge(string? id, string? message = null)
    {
        Write(BridgeAckDto.Success(id, message));
        EmitQueue(force: false);
        EmitState(force: true);
        return true;
    }

    private void HandleSnapshotChanged(PlayerSnapshot snapshot)
    {
        EmitQueue(force: false);
        EmitState(force: false);
    }

    private void EmitQueue(bool force)
    {
        var snapshot = _player.CreateSnapshot();
        lock (_emitLock)
        {
            var signature = BuildQueueSignature(snapshot);
            if (!force && signature == _lastQueueSignature)
                return;

            _lastQueueSignature = signature;
            _queueRevision++;
            Write(BridgeQueueDto.From(snapshot, _queueRevision));
        }
    }

    private void EmitState(bool force)
    {
        var snapshot = _player.CreateSnapshot();
        lock (_emitLock)
        {
            var elapsed = Math.Max(0, snapshot.Elapsed?.TotalSeconds ?? 0);
            var signature = string.Join(
                '|',
                snapshot.Status,
                elapsed.ToString("0.0"),
                snapshot.VolumePercent,
                _player.LastMessage,
                _queueRevision,
                _busy);

            if (!force && signature == _lastStateSignature)
                return;

            _lastStateSignature = signature;
            Write(BridgeStateDto.From(snapshot, _queueRevision, _busy, _player.LastMessage));
        }
    }

    private void EmitToolReport(string report)
    {
        foreach (var line in report.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var level = line.Contains("fail", StringComparison.OrdinalIgnoreCase) ||
                        line.Contains("missing", StringComparison.OrdinalIgnoreCase) ||
                        line.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                        line.Contains("unknown", StringComparison.OrdinalIgnoreCase)
                ? "error"
                : line.Contains("will not run", StringComparison.OrdinalIgnoreCase)
                    ? "warn"
                    : "info";

            Write(BridgeLogDto.Create("tools", level, line));
        }
    }

    private string SummarizeTools(string action, string report)
    {
        if (action is "install" or "repair" or "fix")
        {
            return report
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault() ?? "Tool operation finished.";
        }

        var statuses = _dependencies.DescribeTools();
        var ready = statuses.Count(status => status.Runnable);
        return $"Playback tools: {ready}/{statuses.Count} ready.";
    }

    private void EmitTools()
    {
        var tools = _dependencies.DescribeTools()
            .Select(status => (
                status.DisplayName,
                status.DisplayName,
                status.Path,
                status.Runnable,
                status.PinnedPath))
            .ToList();

        Write(BridgeToolsDto.From(tools));
    }

    private static string BuildQueueSignature(PlayerSnapshot snapshot)
    {
        static string Describe(Track track) =>
            $"{track.Title}\u001f{track.Url}\u001f{track.DisplaySource}\u001f{track.Duration?.TotalSeconds}";

        var parts = new List<string>(snapshot.Previous.Count + snapshot.Pending.Count + 1);
        parts.Add(snapshot.Current is null ? "-" : Describe(snapshot.Current));
        parts.AddRange(snapshot.Previous.Select(Describe));
        parts.Add("--");
        parts.AddRange(snapshot.Pending.Select(Describe));
        return string.Join('\u001e', parts);
    }

    private void Write<T>(T message)
    {
        if (_pipeClosed)
            return;

        string json;
        try
        {
            json = JsonSerializer.Serialize(message, BridgeProtocol.JsonOptions);
        }
        catch (Exception)
        {
            return;
        }

        try
        {
            lock (_writeLock)
            {
                _writer.WriteLine(json);
                _writer.Flush();
            }
        }
        catch (IOException)
        {
            ClosePipe();
        }
        catch (ObjectDisposedException)
        {
            ClosePipe();
        }
    }

    private void ClosePipe()
    {
        if (_pipeClosed)
            return;

        _pipeClosed = true;
        RequestShutdown();
    }
}
