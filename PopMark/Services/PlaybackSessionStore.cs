using PopMark.Helpers;
using PopMark.Models;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace PopMark.Services;

public static class PlaybackSessionStore
{
    private const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static string StateDirectory => PopMarkState.Root;

    private static string SessionFilePath => PopMarkState.SessionsFile;

    public static int CleanupStaleSessions()
    {
        var sessions = LoadSessions();
        var cleaned = 0;

        foreach (var session in sessions)
        {
            if (!IsSameProcess(session))
                continue;

            try
            {
                using var process = Process.GetProcessById(session.ProcessId);
                if (process.HasExited)
                    continue;

                process.Kill(entireProcessTree: true);
                cleaned++;
            }
            catch
            {
            }
        }

        SaveSessions(Array.Empty<PlaybackSession>());
        return cleaned + CleanupDiscoveredPopMarkMpvProcesses();
    }

    public static void Register(int processId, string ipcServerPath, Track track)
    {
        var startedAtTicks = ProcessStartedAtTicks(processId);
        var sessions = LoadSessions()
            .Where(session => session.ProcessId != processId)
            .ToList();

        sessions.Add(new PlaybackSession(
            processId,
            ipcServerPath,
            track.Title,
            DateTimeOffset.UtcNow,
            startedAtTicks));

        SaveSessions(sessions);
    }

    public static void Unregister(int processId)
    {
        var sessions = LoadSessions()
            .Where(session => session.ProcessId != processId)
            .ToList();

        SaveSessions(sessions);
    }

    private static bool IsSameProcess(PlaybackSession session)
    {
        // Sessions written before start times were recorded cannot be verified,
        // so leave them alone rather than risk killing a recycled PID.
        if (session.ProcessStartedAtUtcTicks <= 0)
            return false;

        try
        {
            using var process = Process.GetProcessById(session.ProcessId);
            if (process.HasExited)
                return false;

            var actual = process.StartTime.ToUniversalTime().Ticks;
            return Math.Abs(actual - session.ProcessStartedAtUtcTicks) <= TimeSpan.FromSeconds(2).Ticks;
        }
        catch
        {
            return false;
        }
    }

    private static long ProcessStartedAtTicks(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return process.StartTime.ToUniversalTime().Ticks;
        }
        catch
        {
            return DateTimeOffset.UtcNow.UtcDateTime.Ticks;
        }
    }

    private static List<PlaybackSession> LoadSessions()
    {
        var json = AtomicFile.ReadAllText(SessionFilePath);
        var sessions = ParseSessions(json);
        if (sessions is not null)
            return sessions;

        var backup = AtomicFile.ReadBackup(SessionFilePath);
        return ParseSessions(backup) ?? new List<PlaybackSession>();
    }

    private static List<PlaybackSession>? ParseSessions(string? json)
    {
        if (json is null)
            return null;

        try
        {
            var file = JsonSerializer.Deserialize<SessionFile>(json, JsonOptions);
            if (file is not null)
            {
                return file.SchemaVersion > CurrentSchemaVersion
                    ? new List<PlaybackSession>()
                    : file.Sessions ?? new List<PlaybackSession>();
            }
        }
        catch (JsonException)
        {
        }

        try
        {
            // Older builds wrote a bare array.
            return JsonSerializer.Deserialize<List<PlaybackSession>>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static void SaveSessions(IReadOnlyList<PlaybackSession> sessions)
    {
        try
        {
            Directory.CreateDirectory(StateDirectory);
            var file = new SessionFile(CurrentSchemaVersion, sessions.ToList());
            AtomicFile.WriteAllText(SessionFilePath, JsonSerializer.Serialize(file, JsonOptions));
        }
        catch
        {
        }
    }

    private static int CleanupDiscoveredPopMarkMpvProcesses()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return CleanupWindowsMpvProcesses();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return CleanupLinuxMpvProcesses();

        return 0;
    }

    private static int CleanupWindowsMpvProcesses()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-Command");
            startInfo.ArgumentList.Add("""
                Get-CimInstance Win32_Process -Filter "Name = 'mpv.exe'" |
                    Where-Object { $_.CommandLine -like '*--input-ipc-server=*popmark-*' } |
                    ForEach-Object {
                        Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue
                        $_.ProcessId
                    }
                """);

            using var process = Process.Start(startInfo);
            if (process is null)
                return 0;

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);

            return output
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Count(line => int.TryParse(line, out _));
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Linux (including Termux proot) has no WMI. Walk /proc and kill only mpv processes
    /// carrying PopMark's IPC argument, so stale players cannot outlive a crashed app.
    /// </summary>
    private static int CleanupLinuxMpvProcesses()
    {
        if (!Directory.Exists("/proc"))
            return 0;

        var cleaned = 0;
        IEnumerable<string> processDirectories;
        try
        {
            processDirectories = Directory.EnumerateDirectories("/proc");
        }
        catch
        {
            return 0;
        }

        foreach (var directory in processDirectories)
        {
            if (!int.TryParse(Path.GetFileName(directory), out var processId) ||
                processId == Environment.ProcessId)
            {
                continue;
            }

            try
            {
                var args = ReadLinuxCommandLine(directory);
                if (args.Length == 0)
                    continue;

                var isMpv = Path.GetFileName(args[0]).StartsWith("mpv", StringComparison.OrdinalIgnoreCase);
                var isPopMarkSession = args.Any(arg =>
                    arg.StartsWith("--input-ipc-server=", StringComparison.Ordinal) &&
                    arg.Contains("popmark-", StringComparison.Ordinal));

                if (!isMpv || !isPopMarkSession)
                    continue;

                using var process = Process.GetProcessById(processId);
                if (process.HasExited)
                    continue;

                process.Kill(entireProcessTree: true);
                cleaned++;
            }
            catch
            {
            }
        }

        return cleaned;
    }

    private static string[] ReadLinuxCommandLine(string processDirectory)
    {
        try
        {
            var cmdline = File.ReadAllText(Path.Combine(processDirectory, "cmdline"));
            return cmdline
                .Split('\0', StringSplitOptions.RemoveEmptyEntries)
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private sealed record PlaybackSession(
        int ProcessId,
        string IpcServerPath,
        string Title,
        DateTimeOffset StartedAtUtc,
        long ProcessStartedAtUtcTicks);

    private sealed record SessionFile(
        int SchemaVersion,
        List<PlaybackSession>? Sessions);
}
