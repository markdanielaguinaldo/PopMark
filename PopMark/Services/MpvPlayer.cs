using PopMark.Models;
using System.Diagnostics;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace PopMark.Services;

public sealed class MpvPlayer
{
    private readonly object _syncRoot = new();
    private Process? _process;
    private string? _ipcName;
    private string? _ipcServerPath;
    private int? _registeredProcessId;
    private bool _stopRequested;
    private DateTimeOffset _startedAt;
    private Task? _errorTask;
    private readonly SemaphoreSlim _ipcLock = new(1, 1);
    private int _requestId;
    private const int MinVolumePercent = 0;
    private const int MaxVolumePercent = 130;

    public event Func<Task>? PlaybackExited;

    /// <summary>Last diagnostic mpv wrote to stderr, used to explain a track that died on start.</summary>
    public string? LastError { get; private set; }

    /// <summary>True when the last mpv process gave up almost immediately, which means it never really played.</summary>
    public bool LastRunFailedImmediately { get; private set; }

    public bool IsRunning
    {
        get
        {
            lock (_syncRoot)
            {
                return _process is { HasExited: false };
            }
        }
    }

    public Task PlayAsync(Track track, CancellationToken cancellationToken = default) =>
        PlayAsync(track, 100, cancellationToken);

    public async Task PlayAsync(Track track, int volumePercent, CancellationToken cancellationToken = default)
    {
        await StopAsync(cancellationToken);

        var ipcId = $"popmark-{Environment.ProcessId}-{Guid.NewGuid():N}";
        _ipcName = ipcId;
        _ipcServerPath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? $@"\\.\pipe\{ipcId}"
            : Path.Combine(Path.GetTempPath(), ipcId);

        var mpvPath = ToolLocator.ResolveExecutable("mpv") ?? "mpv";
        var ytDlpPath = ToolLocator.ResolveExecutable("yt-dlp");

        var startInfo = new ProcessStartInfo
        {
            FileName = mpvPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("--no-video");
        startInfo.ArgumentList.Add("--force-window=no");
        startInfo.ArgumentList.Add("--terminal=no");
        startInfo.ArgumentList.Add("--msg-level=all=error");
        startInfo.ArgumentList.Add($"--volume-max={MaxVolumePercent}");
        startInfo.ArgumentList.Add($"--volume={Math.Clamp(volumePercent, MinVolumePercent, MaxVolumePercent)}");
        startInfo.ArgumentList.Add($"--input-ipc-server={_ipcServerPath}");

        // mpv resolves YouTube pages by shelling out to yt-dlp, and it only looks for it by
        // name on PATH. Hand it the exact executable we resolved so an install outside PATH
        // (a WinGet package folder, for example) still plays.
        if (ytDlpPath is not null)
        {
            startInfo.ArgumentList.Add($"--script-opts=ytdl_hook-ytdl_path={ytDlpPath}");
            startInfo.Environment["PATH"] = BuildChildPath(Path.GetDirectoryName(ytDlpPath));
        }

        startInfo.ArgumentList.Add(track.Url);

        try
        {
            var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Unable to start mpv.");

            process.EnableRaisingEvents = true;
            process.Exited += (_, _) => _ = OnProcessExitedAsync(process);

            lock (_syncRoot)
            {
                _stopRequested = false;
                _process = process;
                _registeredProcessId = process.Id;
                _startedAt = DateTimeOffset.UtcNow;
                LastError = null;
                LastRunFailedImmediately = false;
            }

            PlaybackSessionStore.Register(process.Id, _ipcServerPath, track);

            // mpv writes its diagnostics to stdout, not stderr, so both streams feed the
            // summary. Drain them concurrently or a full pipe buffer would stall playback.
            var stdoutTask = ReadAllSafelyAsync(process.StandardOutput, cancellationToken);
            var stderrTask = ReadAllSafelyAsync(process.StandardError, cancellationToken);

            var errorTask = Task.Run(async () =>
            {
                var captured = await Task.WhenAll(stdoutTask, stderrTask);
                var combined = string.Join(
                    "\n",
                    captured.Where(text => !string.IsNullOrWhiteSpace(text)));

                if (!string.IsNullOrWhiteSpace(combined))
                    LastError = SummarizeError(combined);
            }, cancellationToken);

            lock (_syncRoot)
            {
                _errorTask = errorTask;
            }
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new InvalidOperationException(
                $"mpv could not be started from '{mpvPath}'. Run 'tools' to see what PopMark resolved, or 'tools install mpv' to install a private copy.",
                ex);
        }
    }

    private static Task<string> ReadAllSafelyAsync(StreamReader reader, CancellationToken cancellationToken) =>
        Task.Run(async () =>
        {
            try
            {
                return await reader.ReadToEndAsync(cancellationToken);
            }
            catch
            {
                return string.Empty;
            }
        }, cancellationToken);

    private static string BuildChildPath(string? extraDirectory)
    {
        var current = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(extraDirectory))
            return current;

        var entries = current.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return entries.Contains(extraDirectory, StringComparer.OrdinalIgnoreCase)
            ? current
            : string.Join(Path.PathSeparator, new[] { extraDirectory }.Concat(entries));
    }

    private static string SummarizeError(string error)
    {
        var lines = error
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !line.StartsWith("[cplayer]", StringComparison.OrdinalIgnoreCase))
            .TakeLast(3);

        return string.Join(" ", lines);
    }

    public Task PauseAsync(CancellationToken cancellationToken = default) =>
        SendCommandAsync(["set_property", "pause", true], cancellationToken);

    public Task ResumeAsync(CancellationToken cancellationToken = default) =>
        SendCommandAsync(["set_property", "pause", false], cancellationToken);

    public Task TogglePauseAsync(CancellationToken cancellationToken = default) =>
        SendCommandAsync(["cycle", "pause"], cancellationToken);

    public Task AppendAsync(Track track, CancellationToken cancellationToken = default) =>
        SendCommandAsync(["loadfile", track.Url, "append-play"], cancellationToken);

    public Task SeekRelativeAsync(int seconds, CancellationToken cancellationToken = default) =>
        SendCommandAsync(["seek", seconds, "relative"], cancellationToken);

    public Task SeekAbsoluteAsync(TimeSpan timestamp, CancellationToken cancellationToken = default) =>
        SendCommandAsync(["seek", timestamp.TotalSeconds, "absolute"], cancellationToken);

    public Task AdjustVolumeAsync(int percentDelta, CancellationToken cancellationToken = default) =>
        SendCommandAsync(["add", "volume", percentDelta], cancellationToken);

    public Task SetVolumeAsync(int percent, CancellationToken cancellationToken = default) =>
        SendCommandAsync(["set_property", "volume", Math.Clamp(percent, MinVolumePercent, MaxVolumePercent)], cancellationToken);

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Process? process;
        int? registeredProcessId;
        lock (_syncRoot)
        {
            _stopRequested = true;
            process = _process;
            registeredProcessId = _registeredProcessId;
        }

        if (process is null || process.HasExited)
        {
            if (registeredProcessId.HasValue)
                PlaybackSessionStore.Unregister(registeredProcessId.Value);
            return;
        }

        try
        {
            await SendCommandAsync(["quit"], cancellationToken, retry: false);
            await process.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
        }
        catch
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch
            {
            }
        }

        if (registeredProcessId.HasValue)
            PlaybackSessionStore.Unregister(registeredProcessId.Value);
    }

    public void Detach()
    {
        lock (_syncRoot)
        {
            _stopRequested = true;
            _process = null;
            _ipcName = null;
            _ipcServerPath = null;
            _registeredProcessId = null;
        }
    }

    /// <summary>
    /// Reads a numeric mpv property (for example time-pos or duration) through
    /// the same IPC socket used for playback commands. Returns null when mpv is
    /// not running, the property is unavailable, or the request times out.
    /// </summary>
    public async Task<double?> TryGetNumberPropertyAsync(
        string property,
        CancellationToken cancellationToken = default)
    {
        string? ipcName;
        string? ipcServerPath;
        lock (_syncRoot)
        {
            if (_process is null || _process.HasExited)
                return null;

            ipcName = _ipcName;
            ipcServerPath = _ipcServerPath;
        }

        if (string.IsNullOrWhiteSpace(ipcName) || string.IsNullOrWhiteSpace(ipcServerPath))
            return null;

        var requestId = Interlocked.Increment(ref _requestId);
        var payload = JsonSerializer.Serialize(new
        {
            command = new object[] { "get_property", property },
            request_id = requestId
        }) + "\n";

        var acquired = false;
        try
        {
            await _ipcLock.WaitAsync(cancellationToken);
            acquired = true;

            await using var stream = await ConnectIpcAsync(ipcName, ipcServerPath, cancellationToken);
            await WriteIpcAsync(stream, payload, cancellationToken);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMilliseconds(700));

            using var reader = new StreamReader(stream, Encoding.UTF8, false, 1024, leaveOpen: true);
            while (!timeout.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(timeout.Token);
                if (line is null)
                    return null;

                try
                {
                    using var document = JsonDocument.Parse(line);
                    var root = document.RootElement;

                    if (!root.TryGetProperty("request_id", out var idElement) ||
                        !idElement.TryGetInt32(out var id) ||
                        id != requestId)
                    {
                        continue;
                    }

                    if (root.TryGetProperty("error", out var errorElement) &&
                        !string.Equals(errorElement.GetString(), "success", StringComparison.OrdinalIgnoreCase))
                    {
                        return null;
                    }

                    if (!root.TryGetProperty("data", out var dataElement) ||
                        dataElement.ValueKind != JsonValueKind.Number ||
                        !dataElement.TryGetDouble(out var value) ||
                        !double.IsFinite(value))
                    {
                        return null;
                    }

                    return value;
                }
                catch (JsonException)
                {
                    // mpv can emit non-response event lines; keep reading until
                    // the matching request id or the timeout.
                }
            }
        }
        catch
        {
            // Position polling must never take playback down. The caller keeps
            // the last known position and tries again on the next tick.
        }
        finally
        {
            if (acquired)
                _ipcLock.Release();
        }

        return null;
    }

    private async Task SendCommandAsync(object[] command, CancellationToken cancellationToken, bool retry = true)
    {
        string? ipcName;
        string? ipcServerPath;
        lock (_syncRoot)
        {
            if (_process is null || _process.HasExited)
                throw new InvalidOperationException("mpv is not running.");

            ipcName = _ipcName;
            ipcServerPath = _ipcServerPath;
        }

        if (string.IsNullOrWhiteSpace(ipcName) || string.IsNullOrWhiteSpace(ipcServerPath))
            throw new InvalidOperationException("mpv IPC is not ready.");

        var payload = JsonSerializer.Serialize(new { command }) + "\n";
        var attempts = retry ? 12 : 1;

        var acquired = false;
        try
        {
            await _ipcLock.WaitAsync(cancellationToken);
            acquired = true;

            for (var attempt = 1; attempt <= attempts; attempt++)
            {
                try
                {
                    await using var stream = await ConnectIpcAsync(ipcName, ipcServerPath, cancellationToken);
                    await WriteIpcAsync(stream, payload, cancellationToken);
                    return;
                }
                catch when (attempt < attempts)
                {
                    await Task.Delay(120, cancellationToken);
                }
            }

            throw new InvalidOperationException("mpv did not accept the IPC command.");
        }
        finally
        {
            if (acquired)
                _ipcLock.Release();
        }
    }

    private static async Task<Stream> ConnectIpcAsync(
        string ipcName,
        string ipcServerPath,
        CancellationToken cancellationToken)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var pipe = new NamedPipeClientStream(".", ipcName, PipeDirection.InOut, PipeOptions.Asynchronous);
            try
            {
                await pipe.ConnectAsync(350, cancellationToken);
                return pipe;
            }
            catch
            {
                pipe.Dispose();
                throw;
            }
        }

        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            await socket.ConnectAsync(new UnixDomainSocketEndPoint(ipcServerPath), cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    private static async Task WriteIpcAsync(Stream stream, string payload, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(payload);
        await stream.WriteAsync(bytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private async Task OnProcessExitedAsync(Process exitedProcess)
    {
        var shouldNotify = false;
        Task? errorTask;
        lock (_syncRoot)
        {
            if (!ReferenceEquals(_process, exitedProcess))
                return;

            errorTask = _errorTask;

            shouldNotify = !_stopRequested;
            LastRunFailedImmediately = shouldNotify &&
                DateTimeOffset.UtcNow - _startedAt < TimeSpan.FromSeconds(10);
            _process = null;
            _ipcName = null;
            _ipcServerPath = null;
            _registeredProcessId = null;
        }

        // Give the stderr reader a moment so LastError is populated before anyone reads it.
        if (errorTask is not null)
            await Task.WhenAny(errorTask, Task.Delay(500));

        PlaybackSessionStore.Unregister(exitedProcess.Id);

        if (shouldNotify && PlaybackExited is not null)
            await PlaybackExited.Invoke();
    }
}
