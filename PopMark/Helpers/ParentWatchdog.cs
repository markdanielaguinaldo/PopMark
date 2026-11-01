using System.Diagnostics;

namespace PopMark.Helpers;

/// <summary>
/// Watches the UI process that spawned the bridge. If it disappears without
/// closing stdin first, the bridge shuts down instead of playing on forever.
/// </summary>
internal sealed class ParentWatchdog : IDisposable
{
    private readonly CancellationTokenSource _cancellation = new();

    private ParentWatchdog(int processId, Action onParentLost)
    {
        _ = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            try
            {
                while (await timer.WaitForNextTickAsync(_cancellation.Token))
                {
                    if (IsRunning(processId))
                        continue;

                    onParentLost();
                    return;
                }
            }
            catch (OperationCanceledException)
            {
            }
        });
    }

    public static ParentWatchdog? Start(Action onParentLost)
    {
        return int.TryParse(Environment.GetEnvironmentVariable("POPMARK_UI_PID"), out var processId) &&
               processId > 0
            ? new ParentWatchdog(processId, onParentLost)
            : null;
    }

    private static bool IsRunning(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose() => _cancellation.Cancel();
}
