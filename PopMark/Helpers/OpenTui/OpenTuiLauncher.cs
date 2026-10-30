using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace PopMark.Helpers.OpenTui;

/// <summary>
/// Launches the OpenTUI frontend as the default interactive experience.
/// The C# process stays the playback backend; the frontend starts
/// <c>PopMark --bridge</c> as its own child.
/// </summary>
internal static class OpenTuiLauncher
{
    public static bool TryLaunch(string[] args, out int exitCode)
    {
        exitCode = 0;

        if (args.Any(arg => arg.Equals("--legacy-ui", StringComparison.OrdinalIgnoreCase)))
            return false;

        if (Console.IsInputRedirected || Console.IsOutputRedirected)
            return false;

        if (!CanLaunchFor(args, out var startupUrl))
            return false;

        var ui = FindUiEntry();
        var bun = FindBun();

        if (ui is null || bun is null)
            return false;

        // Run from ui/ so Bun finds tsconfig.json, bunfig.toml, and node_modules.
        var entryDirectory = Path.GetDirectoryName(ui.Value.EntryPath)!;
        var workingDirectory = Directory.GetParent(entryDirectory)?.FullName ?? entryDirectory;

        var startInfo = new ProcessStartInfo
        {
            FileName = bun.Value.FileName,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardError = true
        };

        var uiLogPath = UiLogPath();
        if (uiLogPath is not null)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(uiLogPath)!);
                File.Delete(uiLogPath);
            }
            catch
            {
            }
        }

        var uiLog = new StringBuilder();

        var arguments = new List<string>();
        if (ui.Value.IsSource)
            arguments.Add("--conditions=browser");

        arguments.Add(ui.Value.EntryPath);

        if (bun.Value.BatchPath is null)
        {
            foreach (var argument in arguments)
                startInfo.ArgumentList.Add(argument);
        }
        else
        {
            // A .cmd shim has to run through cmd.exe. /s /c expects one command
            // string, so quote the shim and every argument ourselves.
            var commandLine = $"\"{bun.Value.BatchPath}\" {string.Join(' ', arguments.Select(QuoteArgument))}";
            startInfo.Arguments = $"/d /s /c \"{commandLine}\"";
        }

        var backend = BackendPath();
        if (backend is not null)
            startInfo.Environment["POPMARK_BACKEND"] = backend;

        if (!string.IsNullOrWhiteSpace(startupUrl))
            startInfo.Environment["POPMARK_START_URL"] = startupUrl;

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
                return false;

            process.ErrorDataReceived += (_, args) =>
            {
                if (args.Data is null)
                    return;

                lock (uiLog)
                    uiLog.AppendLine(args.Data);
            };
            process.BeginErrorReadLine();

            // Keep the terminal usable even if the OpenTUI process dies without
            // restoring raw mode, the alternate screen, or mouse reporting.
            try
            {
                process.WaitForExit();
            }
            finally
            {
                RestoreTerminal();
            }

            exitCode = process.ExitCode;

            if (exitCode != 0)
                ReportUiFailure(exitCode, uiLog, uiLogPath);

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Best-effort reset for terminals left in OpenTUI's raw/alternate-screen mode.
    /// Writing these on a clean exit is harmless and keeps a crashed session from
    /// spilling mouse reports into the shell prompt.
    /// </summary>
    private static void RestoreTerminal()
    {
        try
        {
            Console.Out.Write(
                "\u001b[0m" +
                "\u001b[?1000l\u001b[?1002l\u001b[?1003l\u001b[?1006l\u001b[?1015l" +
                "\u001b[?1004l\u001b[?2004l\u001b[?2026l" +
                "\u001b[0 q\u001b[?25h\u001b[?1049l");
            Console.Out.Flush();
        }
        catch
        {
        }
    }

    private static string? UiLogPath()
    {
        try
        {
            return Path.Combine(PopMarkState.LogsDirectory, "opentui.log");
        }
        catch
        {
            return null;
        }
    }

    private static void ReportUiFailure(int exitCode, StringBuilder log, string? logPath)
    {
        string? details;
        lock (log)
            details = log.ToString().Trim();

        if (logPath is not null)
        {
            try
            {
                File.WriteAllText(logPath, details);
            }
            catch
            {
            }
        }

        Console.Error.WriteLine();
        Console.Error.WriteLine($"[PopMark] The OpenTUI interface stopped unexpectedly (exit code {exitCode}).");
        if (!string.IsNullOrWhiteSpace(details))
        {
            var tail = details.Length > 4000 ? details[^4000..] : details;
            Console.Error.WriteLine(tail);
        }

        if (logPath is not null)
            Console.Error.WriteLine($"[PopMark] Details saved to {logPath}");
    }

    private static bool CanLaunchFor(string[] args, out string? startupUrl)
    {
        startupUrl = null;

        if (args.Length == 0)
            return true;

        if (args.Length == 1 && IsLikelyUrl(args[0]))
        {
            startupUrl = args[0];
            return true;
        }

        if (args.Length == 2 &&
            args[0].Equals("add", StringComparison.OrdinalIgnoreCase) &&
            IsLikelyUrl(args[1]))
        {
            startupUrl = args[1];
            return true;
        }

        // Playback tests, tools, bridge, versions, and other flag-driven modes
        // stay on the C# side.
        return false;
    }

    private static (string EntryPath, bool IsSource)? FindUiEntry()
    {
        var overridePath = Environment.GetEnvironmentVariable("POPMARK_UI_ENTRY");
        if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath))
        {
            return (
                Path.GetFullPath(overridePath),
                overridePath.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase) ||
                overridePath.EndsWith(".ts", StringComparison.OrdinalIgnoreCase));
        }

        var candidates = new List<string>();
        var baseDirectory = AppContext.BaseDirectory;

        for (var directory = new DirectoryInfo(baseDirectory); directory is not null; directory = directory.Parent)
        {
            candidates.Add(Path.Combine(directory.FullName, "ui", "src", "index.tsx"));
            candidates.Add(Path.Combine(directory.FullName, "ui", "dist", "index.js"));

            if (directory.FullName.Equals(directory.Root.FullName, StringComparison.OrdinalIgnoreCase))
                break;
        }

        var current = new DirectoryInfo(Environment.CurrentDirectory);
        for (var directory = current; directory is not null; directory = directory.Parent)
        {
            candidates.Add(Path.Combine(directory.FullName, "ui", "src", "index.tsx"));
            candidates.Add(Path.Combine(directory.FullName, "ui", "dist", "index.js"));

            if (directory.FullName.Equals(directory.Root.FullName, StringComparison.OrdinalIgnoreCase))
                break;
        }

        foreach (var candidate in candidates)
        {
            if (!File.Exists(candidate))
                continue;

            return (
                Path.GetFullPath(candidate),
                candidate.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }

    private static (string FileName, string? BatchPath)? FindBun()
    {
        var overridePath = Environment.GetEnvironmentVariable("POPMARK_BUN");
        if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath))
            return BunCommand(overridePath);

        var candidates = new List<string>();
        var baseDirectory = AppContext.BaseDirectory;
        var isWindows = OperatingSystem.IsWindows();
        var bunName = isWindows ? "bun.exe" : "bun";

        // Portable packages ship a Bun runtime beside the executable.
        candidates.Add(Path.Combine(baseDirectory, bunName));
        candidates.Add(Path.Combine(baseDirectory, "runtime", bunName));

        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        candidates.AddRange(path
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(directory => Path.Combine(directory, bunName)));

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (isWindows)
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            candidates.Add(Path.Combine(home, ".bun", "bin", "bun.exe"));
            candidates.Add(Path.Combine(appData, "npm", "node_modules", "bun", "bin", "bun.exe"));
        }
        else
        {
            candidates.Add(Path.Combine(home, ".bun", "bin", "bun"));

            // Termux keeps its packages in $PREFIX/bin; a proot distro has its own bun.
            var prefix = Environment.GetEnvironmentVariable("PREFIX");
            if (!string.IsNullOrWhiteSpace(prefix))
                candidates.Add(Path.Combine(prefix, "bin", "bun"));

            candidates.Add("/data/data/com.termux/files/usr/bin/bun");
            candidates.Add("/usr/local/bin/bun");
            candidates.Add("/usr/bin/bun");
        }

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                return BunCommand(candidate);
        }

        if (!isWindows)
            return null;

        var shim = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "npm",
            "bun.cmd");
        return File.Exists(shim) ? BunCommand(shim) : null;
    }

    private static (string FileName, string? BatchPath) BunCommand(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return path.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith(".bat", StringComparison.OrdinalIgnoreCase)
                ? ("cmd.exe", path)
                : (path, null);
        }

        return (path, null);
    }

    private static string QuoteArgument(string argument) =>
        argument.Contains(' ') || argument.Contains('"')
            ? $"\"{argument.Replace("\"", "\\\"")}\""
            : argument;

    private static string? BackendPath()
    {
        var processPath = Environment.ProcessPath;

        // A single-file publish runs from PopMark.exe itself, so start the
        // bridge from the same executable.
        if (!string.IsNullOrWhiteSpace(processPath) &&
            !Path.GetFileNameWithoutExtension(processPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase) &&
            File.Exists(processPath))
        {
            return processPath;
        }

        // Framework-dependent runs through `dotnet PopMark.dll`: the assembly
        // path is the backend, while the process path is the dotnet host.
#pragma warning disable IL3000 // Assembly.Location is empty in single-file builds.
        var assemblyPath = Assembly.GetEntryAssembly()?.Location;
#pragma warning restore IL3000
        if (!string.IsNullOrWhiteSpace(assemblyPath) && File.Exists(assemblyPath))
            return assemblyPath;

        return !string.IsNullOrWhiteSpace(processPath) && File.Exists(processPath)
            ? processPath
            : null;
    }

    private static bool IsLikelyUrl(string value) =>
        Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) &&
        (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
         uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));
}
