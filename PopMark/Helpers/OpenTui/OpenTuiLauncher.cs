using System.Diagnostics;
using System.Reflection;

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
            UseShellExecute = false
        };

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

            process.WaitForExit();
            exitCode = process.ExitCode;
            return true;
        }
        catch
        {
            return false;
        }
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

        // Portable packages ship a Bun runtime beside the executable.
        candidates.Add(Path.Combine(baseDirectory, "bun.exe"));
        candidates.Add(Path.Combine(baseDirectory, "runtime", "bun.exe"));

        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        candidates.AddRange(path
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(directory => Path.Combine(directory, "bun.exe")));

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        candidates.Add(Path.Combine(home, ".bun", "bin", "bun.exe"));
        candidates.Add(Path.Combine(appData, "npm", "node_modules", "bun", "bin", "bun.exe"));

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                return BunCommand(candidate);
        }

        var shim = Path.Combine(appData, "npm", "bun.cmd");
        return File.Exists(shim) ? BunCommand(shim) : null;
    }

    private static (string FileName, string? BatchPath) BunCommand(string path)
    {
        return path.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".bat", StringComparison.OrdinalIgnoreCase)
            ? ("cmd.exe", path)
            : (path, null);
    }

    private static string QuoteArgument(string argument) =>
        argument.Contains(' ') || argument.Contains('"')
            ? $"\"{argument.Replace("\"", "\\\"")}\""
            : argument;

    private static string? BackendPath()
    {
        var assemblyPath = Assembly.GetEntryAssembly()?.Location;
        if (!string.IsNullOrWhiteSpace(assemblyPath) &&
            assemblyPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) &&
            File.Exists(assemblyPath))
        {
            return assemblyPath;
        }

        var processPath = Environment.ProcessPath;
        return !string.IsNullOrWhiteSpace(processPath) && File.Exists(processPath)
            ? processPath
            : null;
    }

    private static bool IsLikelyUrl(string value) =>
        Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) &&
        (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
         uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));
}
