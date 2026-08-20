using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace PopMark.Services;

public static class ToolLocator
{
    private static readonly string[] WindowsExecutableExtensions = { ".exe", ".cmd", ".bat", ".com" };
    private static readonly ConcurrentDictionary<string, string?> ResolvedPaths = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, bool> RunnablePaths = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object OverrideSync = new();

    public static string ToolRoot =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PopMark",
            "tools");

    public static string OverrideFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PopMark",
            "tools.json");

    /// <summary>The enforced install location PopMark manages itself.</summary>
    public static string ManagedDirectory(string commandName) => Path.Combine(ToolRoot, commandName);

    public static string ManagedExecutablePath(string commandName) =>
        Path.Combine(ManagedDirectory(commandName), RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? commandName + ".exe" : commandName);

    public static string? ResolveExecutable(string commandName)
    {
        if (ResolvedPaths.TryGetValue(commandName, out var cached) && (cached is null || File.Exists(cached)))
            return cached;

        var resolved = Locate(commandName);
        if (resolved is not null)
            PrependToProcessPath(Path.GetDirectoryName(resolved));

        ResolvedPaths[commandName] = resolved;
        return resolved;
    }

    /// <summary>Resolves and then proves the tool actually runs, so a blocked or half-written copy counts as missing.</summary>
    public static string? ResolveRunnableExecutable(string commandName)
    {
        var path = ResolveExecutable(commandName);
        if (path is null)
            return null;

        if (IsRunnable(path))
            return path;

        // A resolved-but-dead copy must not pin the cache, or the repair flow can never take over.
        ResolvedPaths.TryRemove(commandName, out _);
        return null;
    }

    public static bool IsRunnable(string executablePath)
    {
        return RunnablePaths.GetOrAdd(executablePath, static path =>
        {
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists || info.Length == 0)
                    return false;

                var startInfo = new ProcessStartInfo
                {
                    FileName = path,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                startInfo.ArgumentList.Add("--version");

                using var process = Process.Start(startInfo);
                if (process is null)
                    return false;

                if (!process.WaitForExit(15000))
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    catch
                    {
                    }

                    return false;
                }

                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        });
    }

    public static void Invalidate()
    {
        ResolvedPaths.Clear();
        RunnablePaths.Clear();
    }

    public static void RefreshPathFromEnvironment()
    {
        var paths = new[]
            {
                Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process),
                ReadEnvironmentVariableSafely("PATH", EnvironmentVariableTarget.User),
                ReadEnvironmentVariableSafely("PATH", EnvironmentVariableTarget.Machine)
            }
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .SelectMany(path => path!.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        var merged = string.Join(Path.PathSeparator, paths.Concat(GetFallbackPathDirectories()).Distinct(StringComparer.OrdinalIgnoreCase));
        Environment.SetEnvironmentVariable("PATH", merged, EnvironmentVariableTarget.Process);
    }

    /// <summary>
    /// Puts a tool's own directory on the process PATH so child processes inherit it.
    /// mpv shells out to yt-dlp by name, so a yt-dlp that only exists inside a WinGet
    /// package folder is invisible to mpv unless that folder is on the PATH we hand down.
    /// </summary>
    public static void PrependToProcessPath(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return;

        var current = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process) ?? string.Empty;
        var entries = current.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (entries.Contains(directory, StringComparer.OrdinalIgnoreCase))
            return;

        Environment.SetEnvironmentVariable(
            "PATH",
            string.Join(Path.PathSeparator, new[] { directory }.Concat(entries)),
            EnvironmentVariableTarget.Process);
    }

    public static IReadOnlyDictionary<string, string> LoadOverrides()
    {
        lock (OverrideSync)
        {
            try
            {
                if (!File.Exists(OverrideFilePath))
                    return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(OverrideFilePath));
                return parsed is null
                    ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>(parsed, StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    public static void SetOverride(string commandName, string? executablePath)
    {
        lock (OverrideSync)
        {
            var overrides = new Dictionary<string, string>(LoadOverrides(), StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(executablePath))
                overrides.Remove(commandName);
            else
                overrides[commandName] = executablePath;

            Directory.CreateDirectory(Path.GetDirectoryName(OverrideFilePath)!);
            File.WriteAllText(OverrideFilePath, JsonSerializer.Serialize(overrides, new JsonSerializerOptions { WriteIndented = true }));
        }

        Invalidate();
    }

    private static string? Locate(string commandName)
    {
        RefreshPathFromEnvironment();

        foreach (var candidate in GetPinnedCandidates(commandName))
        {
            if (IsUsableFile(candidate))
                return Path.GetFullPath(candidate);
        }

        foreach (var directory in GetSearchDirectories())
        {
            foreach (var fileName in GetExecutableCandidates(commandName))
            {
                var fullPath = Path.Combine(directory, fileName);
                if (IsUsableFile(fullPath))
                    return fullPath;
            }
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return null;

        return LocateWithWhere(commandName) ?? LocateRecursively(commandName);
    }

    /// <summary>Explicit pins beat discovery: the saved tools.json, then environment, then PopMark's own install.</summary>
    private static IEnumerable<string> GetPinnedCandidates(string commandName)
    {
        if (LoadOverrides().TryGetValue(commandName, out var configured) && !string.IsNullOrWhiteSpace(configured))
            yield return Environment.ExpandEnvironmentVariables(configured);

        foreach (var variable in GetOverrideVariableNames(commandName))
        {
            var value = Environment.GetEnvironmentVariable(variable);
            if (!string.IsNullOrWhiteSpace(value))
                yield return Environment.ExpandEnvironmentVariables(value.Trim().Trim('"'));
        }

        yield return ManagedExecutablePath(commandName);
    }

    private static IEnumerable<string> GetOverrideVariableNames(string commandName)
    {
        var normalized = new string(commandName.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        yield return $"POPMARK_{normalized}_PATH";
        yield return $"POPMARK_{normalized}";
    }

    private static string? LocateWithWhere(string commandName)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "where.exe",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add(commandName);

            using var process = Process.Start(startInfo);
            if (process is null)
                return null;

            var output = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(5000))
                return null;

            return output
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault(IsUsableFile);
        }
        catch
        {
            return null;
        }
    }

    private static string? LocateRecursively(string commandName)
    {
        var fileNames = GetExecutableCandidates(commandName).ToArray();

        foreach (var (root, maxDepth) in GetRecursiveSearchRoots())
        {
            if (!Directory.Exists(root))
                continue;

            var matches = new List<string>();
            foreach (var fileName in fileNames)
                matches.AddRange(FindFiles(root, fileName, maxDepth));

            var best = matches
                .Where(IsUsableFile)
                .OrderByDescending(ExtractVersion)
                .ThenByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();

            if (best is not null)
                return best;
        }

        return null;
    }

    private static IEnumerable<string> GetSearchDirectories()
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(path))
        {
            foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                yield return directory;
        }

        foreach (var directory in GetFallbackPathDirectories())
            yield return directory;
    }

    private static IEnumerable<string> GetFallbackPathDirectories()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            yield break;

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var commonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        yield return Path.Combine(localAppData, "Microsoft", "WindowsApps");
        yield return Path.Combine(localAppData, "Microsoft", "WinGet", "Links");
        yield return Path.Combine(ToolRoot, "yt-dlp");
        yield return Path.Combine(ToolRoot, "mpv");
        yield return Path.Combine(userProfile, "scoop", "shims");
        yield return Path.Combine(commonAppData, "scoop", "shims");
        yield return Path.Combine(commonAppData, "chocolatey", "bin");
        yield return Path.Combine(programFiles, "mpv");
        yield return Path.Combine(programFilesX86, "mpv");
    }

    private static IEnumerable<(string Root, int MaxDepth)> GetRecursiveSearchRoots()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var commonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        yield return (ToolRoot, 4);
        yield return (Path.Combine(localAppData, "Microsoft", "WinGet", "Packages"), 4);
        yield return (Path.Combine(programFiles, "WinGet", "Packages"), 4);
        yield return (Path.Combine(userProfile, "scoop", "apps"), 4);
        yield return (Path.Combine(commonAppData, "chocolatey", "lib"), 4);
        yield return (Path.Combine(localAppData, "Programs"), 4);
        yield return (programFiles, 3);
        yield return (programFilesX86, 3);
    }

    private static IEnumerable<string> FindFiles(string root, string fileName, int maxDepth)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            MaxRecursionDepth = maxDepth,
            AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.System,
            MatchCasing = MatchCasing.CaseInsensitive
        };

        // Enumerate behind a guard: one unreadable subtree must not abandon the whole scan.
        using var enumerator = SafeEnumerate(root, fileName, options).GetEnumerator();
        while (true)
        {
            string current;
            try
            {
                if (!enumerator.MoveNext())
                    yield break;

                current = enumerator.Current;
            }
            catch
            {
                yield break;
            }

            yield return current;
        }
    }

    private static IEnumerable<string> SafeEnumerate(string root, string fileName, EnumerationOptions options)
    {
        try
        {
            return Directory.EnumerateFiles(root, fileName, options);
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>WinGet keeps the version in the package folder name, so the newest copy wins.</summary>
    private static Version ExtractVersion(string path)
    {
        var best = new Version(0, 0);
        var directory = Path.GetDirectoryName(path);

        while (!string.IsNullOrEmpty(directory))
        {
            foreach (var part in Path.GetFileName(directory).Split('_', '-', ' ', '+'))
            {
                if (Version.TryParse(part, out var version) && version > best)
                    best = version;
            }

            directory = Path.GetDirectoryName(directory);
        }

        return best;
    }

    private static bool IsUsableFile(string path)
    {
        try
        {
            // Windows app-execution aliases are zero-length reparse stubs that fail to launch.
            var info = new FileInfo(path);
            return info.Exists && info.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static string? ReadEnvironmentVariableSafely(string name, EnvironmentVariableTarget target)
    {
        try
        {
            return Environment.GetEnvironmentVariable(name, target);
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<string> GetExecutableCandidates(string commandName)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || Path.HasExtension(commandName))
            return new[] { commandName };

        var extensions = Environment.GetEnvironmentVariable("PATHEXT")?
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ?? WindowsExecutableExtensions;

        return extensions.Select(extension => commandName + extension);
    }
}
