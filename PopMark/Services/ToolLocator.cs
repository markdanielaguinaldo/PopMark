using System.Runtime.InteropServices;

namespace PopMark.Services;

public static class ToolLocator
{
    private static readonly string[] WindowsExecutableExtensions = [".exe", ".cmd", ".bat", ".com"];

    public static string ToolRoot =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PopMark",
            "tools");

    public static string? ResolveExecutable(string commandName)
    {
        RefreshPathFromEnvironment();

        foreach (var directory in GetSearchDirectories())
        {
            foreach (var candidate in GetExecutableCandidates(commandName))
            {
                var fullPath = Path.Combine(directory, candidate);
                if (File.Exists(fullPath))
                    return fullPath;
            }
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            foreach (var root in GetRecursiveSearchRoots().Where(Directory.Exists))
            {
                foreach (var candidate in GetExecutableCandidates(commandName))
                {
                    var match = FindFirstFile(root, candidate);
                    if (match is not null)
                        return match;
                }
            }
        }

        return null;
    }

    public static void RefreshPathFromEnvironment()
    {
        var paths = new[]
            {
                Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process),
                Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User),
                Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine)
            }
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .SelectMany(path => path!.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        var merged = string.Join(Path.PathSeparator, paths.Concat(GetFallbackPathDirectories()).Distinct(StringComparer.OrdinalIgnoreCase));
        Environment.SetEnvironmentVariable("PATH", merged, EnvironmentVariableTarget.Process);
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
        yield return Path.Combine(userProfile, "scoop", "shims");
        yield return Path.Combine(commonAppData, "scoop", "shims");
        yield return Path.Combine(commonAppData, "chocolatey", "bin");
        yield return Path.Combine(programFiles, "mpv");
        yield return Path.Combine(programFilesX86, "mpv");
    }

    private static IEnumerable<string> GetRecursiveSearchRoots()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

        yield return ToolRoot;
        yield return Path.Combine(localAppData, "Microsoft", "WinGet", "Packages");
        yield return Path.Combine(localAppData, "Programs");
        yield return Path.Combine(programFiles);
    }

    private static string? FindFirstFile(string root, string fileName)
    {
        try
        {
            return Directory
                .EnumerateFiles(root, fileName, SearchOption.AllDirectories)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<string> GetExecutableCandidates(string commandName)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || Path.HasExtension(commandName))
            return [commandName];

        var extensions = Environment.GetEnvironmentVariable("PATHEXT")?
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ?? WindowsExecutableExtensions;

        return extensions.Select(extension => commandName + extension);
    }
}
