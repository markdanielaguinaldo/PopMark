using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PopMark.Helpers;

/// <summary>
/// Ties a Windows child process tree to this process. When the job handle closes,
/// including when PopMark is killed, Windows terminates every process still in it.
/// That keeps mpv and the Bun frontend from outliving a closed terminal.
/// </summary>
internal sealed class ProcessJob : IDisposable
{
    private const uint JobObjectExtendedLimitInformationClass = 9;
    private const uint LimitKillOnJobClose = 0x2000;

    private IntPtr _handle;

    private ProcessJob(IntPtr handle) => _handle = handle;

    public static ProcessJob? TryCreateFor(Process process)
    {
        if (!OperatingSystem.IsWindows())
            return null;

        var handle = CreateJobObject(IntPtr.Zero, null);
        if (handle == IntPtr.Zero)
            return null;

        var info = new JobObjectExtendedLimitInformation
        {
            BasicLimitInformation = new JobObjectBasicLimitInformation
            {
                LimitFlags = LimitKillOnJobClose
            }
        };

        var size = Marshal.SizeOf<JobObjectExtendedLimitInformation>();
        var buffer = Marshal.AllocHGlobal(size);

        try
        {
            Marshal.StructureToPtr(info, buffer, fDeleteOld: false);

            var configured = SetInformationJobObject(
                handle,
                JobObjectExtendedLimitInformationClass,
                buffer,
                (uint)size);

            if (!configured || !AssignProcessToJobObject(handle, process.Handle))
            {
                CloseHandle(handle);
                return null;
            }

            return new ProcessJob(handle);
        }
        catch
        {
            CloseHandle(handle);
            return null;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public void Dispose()
    {
        var handle = Interlocked.Exchange(ref _handle, IntPtr.Zero);
        if (handle != IntPtr.Zero)
            CloseHandle(handle);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectBasicLimitInformation
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectExtendedLimitInformation
    {
        public JobObjectBasicLimitInformation BasicLimitInformation;
        public IoCounters IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateJobObject(IntPtr jobAttributes, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(
        IntPtr job,
        uint infoClass,
        IntPtr info,
        uint infoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}
