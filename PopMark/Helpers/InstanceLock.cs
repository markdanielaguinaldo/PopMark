using System.Text;

namespace PopMark.Helpers;

/// <summary>
/// A per-state-folder lock so two PopMark windows cannot write the same queue,
/// theme, and session files at once. The handle is released by the OS if the
/// process dies, so a crash never leaves a permanent lock.
/// </summary>
internal sealed class InstanceLock : IDisposable
{
    private readonly FileStream _stream;

    private InstanceLock(FileStream stream) => _stream = stream;

    public static InstanceLock? TryAcquire(out string lockPath)
    {
        lockPath = Path.Combine(PopMarkState.Root, "popmark.lock");

        try
        {
            Directory.CreateDirectory(PopMarkState.Root);
            var stream = new FileStream(
                lockPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 1,
                FileOptions.None);

            stream.SetLength(0);
            var owner = Encoding.UTF8.GetBytes($"{Environment.ProcessId}\n");
            stream.Write(owner);
            stream.Flush(flushToDisk: true);

            return new InstanceLock(stream);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        try
        {
            _stream.Dispose();
        }
        catch
        {
        }
    }
}
