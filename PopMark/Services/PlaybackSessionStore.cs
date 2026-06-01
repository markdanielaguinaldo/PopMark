using PopMark.Models;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace PopMark.Services;

public static class PlaybackSessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static string StateDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PopMark");

    private static string SessionFilePath => Path.Combine(StateDirectory, "sessions.json");

    public static int CleanupStaleSessions()
    {
        var sessions = LoadSessions();
        var cleaned = 0;

        foreach (var session in sessions)
        {
            try
            {
                var process = Process.GetProcessById(session.ProcessId);
                if (process.HasExited)
                    continue;

                process.Kill(entireProcessTree: true);
                cleaned++;
            }
            catch
            {
            }
        }

        SaveSessions([]);
        return cleaned + CleanupDiscoveredPopMarkMpvProcesses();
    }

    public static void Register(int processId, string ipcServerPath, Track track)
    {
        var sessions = LoadSessions()
            .Where(session => session.ProcessId != processId)
            .ToList();

        sessions.Add(new PlaybackSession(
            processId,
            ipcServerPath,
            track.Title,
            DateTimeOffset.UtcNow));

        SaveSessions(sessions);
    }

    public static void Unregister(int processId)
    {
        var sessions = LoadSessions()
            .Where(session => session.ProcessId != processId)
            .ToList();

        SaveSessions(sessions);
    }

    private static List<PlaybackSession> LoadSessions()
    {
        try
        {
            if (!File.Exists(SessionFilePath))
                return [];

            var json = File.ReadAllText(SessionFilePath);
            return JsonSerializer.Deserialize<List<PlaybackSession>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static void SaveSessions(IReadOnlyList<PlaybackSession> sessions)
    {
        try
        {
            Directory.CreateDirectory(StateDirectory);
            File.WriteAllText(SessionFilePath, JsonSerializer.Serialize(sessions, JsonOptions));
        }
        catch
        {
        }
    }

    private static int CleanupDiscoveredPopMarkMpvProcesses()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return 0;

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

    private sealed record PlaybackSession(
        int ProcessId,
        string IpcServerPath,
        string Title,
        DateTimeOffset StartedAtUtc);
}
