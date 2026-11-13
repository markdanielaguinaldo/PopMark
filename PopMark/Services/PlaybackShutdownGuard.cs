using System.Runtime.InteropServices;

namespace PopMark.Services;

public sealed class PlaybackShutdownGuard
{
    private readonly PlaybackQueue _player;
    private readonly Action? _afterCleanup;
    private readonly ConsoleCtrlHandler? _consoleCtrlHandler;
    private int _cleanupStarted;

    public PlaybackShutdownGuard(PlaybackQueue player, Action? afterCleanup = null)
    {
        _player = player;
        _afterCleanup = afterCleanup;
        AppDomain.CurrentDomain.ProcessExit += HandleProcessExit;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _consoleCtrlHandler = HandleConsoleCtrl;
            _ = SetConsoleCtrlHandler(_consoleCtrlHandler, add: true);
        }
    }

    public void Cleanup()
    {
        if (Interlocked.Exchange(ref _cleanupStarted, 1) == 1)
            return;

        try
        {
            // aislop-ignore-next-line ai-slop/csharp-sync-over-async -- a process-exit handler has nowhere to await
            _player.StopPlaybackAndKeepQueueAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException
                                       or ObjectDisposedException or AggregateException)
        {
            // Playback is already gone; cleanup still reaches the after-cleanup callback.
        }

        try
        {
            _afterCleanup?.Invoke();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // A user callback must never break shutdown.
        }
    }

    private void HandleProcessExit(object? sender, EventArgs eventArgs) =>
        Cleanup();

    private bool HandleConsoleCtrl(ConsoleCtrlType ctrlType)
    {
        if (ctrlType is ConsoleCtrlType.Close or ConsoleCtrlType.Logoff or ConsoleCtrlType.Shutdown)
            Cleanup();

        return false;
    }

    private delegate bool ConsoleCtrlHandler(ConsoleCtrlType ctrlType);

    private enum ConsoleCtrlType
    {
        CtrlC = 0,
        CtrlBreak = 1,
        Close = 2,
        Logoff = 5,
        Shutdown = 6
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleCtrlHandler(ConsoleCtrlHandler handlerRoutine, bool add);
}
