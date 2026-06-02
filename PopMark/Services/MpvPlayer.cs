using PopMark.Models;
using System.Diagnostics;
using System.IO.Pipes;
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
    private const int MinVolumePercent = 0;
    private const int MaxVolumePercent = 130;

    public event Func<Task>? PlaybackExited;

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

        var startInfo = new ProcessStartInfo
        {
            FileName = ToolLocator.ResolveExecutable("mpv") ?? "mpv",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("--no-video");
        startInfo.ArgumentList.Add("--force-window=no");
        startInfo.ArgumentList.Add("--really-quiet");
        startInfo.ArgumentList.Add($"--volume-max={MaxVolumePercent}");
        startInfo.ArgumentList.Add($"--volume={Math.Clamp(volumePercent, MinVolumePercent, MaxVolumePercent)}");
        startInfo.ArgumentList.Add($"--input-ipc-server={_ipcServerPath}");
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
            }

            PlaybackSessionStore.Register(process.Id, _ipcServerPath, track);

            _ = Task.Run(async () =>
            {
                try
                {
                    await process.StandardOutput.ReadToEndAsync(cancellationToken);
                    await process.StandardError.ReadToEndAsync(cancellationToken);
                }
                catch
                {
                }
            }, cancellationToken);
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new InvalidOperationException("mpv was not found. Install it and make sure it is available on PATH.", ex);
        }
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

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    await using var pipe = new NamedPipeClientStream(".", ipcName, PipeDirection.Out, PipeOptions.Asynchronous);
                    await pipe.ConnectAsync(350, cancellationToken);
                    var bytes = Encoding.UTF8.GetBytes(payload);
                    await pipe.WriteAsync(bytes, cancellationToken);
                    await pipe.FlushAsync(cancellationToken);
                    return;
                }

                await using var stream = File.OpenWrite(ipcServerPath);
                var unixBytes = Encoding.UTF8.GetBytes(payload);
                await stream.WriteAsync(unixBytes, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                return;
            }
            catch when (attempt < attempts)
            {
                await Task.Delay(120, cancellationToken);
            }
        }
    }

    private async Task OnProcessExitedAsync(Process exitedProcess)
    {
        var shouldNotify = false;
        lock (_syncRoot)
        {
            if (!ReferenceEquals(_process, exitedProcess))
                return;

            shouldNotify = !_stopRequested;
            _process = null;
            _ipcName = null;
            _ipcServerPath = null;
            _registeredProcessId = null;
        }

        PlaybackSessionStore.Unregister(exitedProcess.Id);

        if (shouldNotify && PlaybackExited is not null)
            await PlaybackExited.Invoke();
    }
}
