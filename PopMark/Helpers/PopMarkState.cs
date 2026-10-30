namespace PopMark.Helpers;

/// <summary>
/// Owns the folder that holds the queue, tool pins, sessions, theme, and managed tools.
/// Windows keeps using %LOCALAPPDATA%\PopMark. Linux, including a Termux proot
/// environment, follows the XDG base directory spec. POPMARK_HOME overrides both, which
/// is what the Termux launcher uses to keep state inside the Android home directory.
/// </summary>
public static class PopMarkState
{
    public const string FolderName = "PopMark";

    public static string Root => Path.GetFullPath(ResolveRoot());

    public static string ToolsDirectory => Path.Combine(Root, "tools");

    public static string ToolsFile => Path.Combine(Root, "tools.json");

    public static string QueueFile => Path.Combine(Root, "queue.json");

    public static string SessionsFile => Path.Combine(Root, "sessions.json");

    public static string ThemeFile => Path.Combine(Root, "theme.json");

    public static string LogsDirectory => Path.Combine(Root, "logs");

    public static bool IsWindows => OperatingSystem.IsWindows();

    /// <summary>True when running inside the Termux userspace instead of a proot distro.</summary>
    public static bool IsTermux =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TERMUX_VERSION")) ||
        Directory.Exists("/data/data/com.termux/files/usr");

    private static string ResolveRoot()
    {
        var overridePath = Environment.GetEnvironmentVariable("POPMARK_HOME");
        if (!string.IsNullOrWhiteSpace(overridePath))
            return ExpandHome(overridePath.Trim().Trim('"'));

        if (IsWindows)
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(localAppData))
                localAppData = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            return Path.Combine(localAppData, FolderName);
        }

        var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (!string.IsNullOrWhiteSpace(xdgDataHome) && Path.IsPathRooted(xdgDataHome))
            return Path.Combine(xdgDataHome, FolderName);

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home))
            home = Environment.GetEnvironmentVariable("HOME") ?? Directory.GetCurrentDirectory();

        return Path.Combine(home, ".local", "share", FolderName);
    }

    private static string ExpandHome(string path)
    {
        if (path == "~")
            path = "~/";

        if (!path.StartsWith("~/", StringComparison.Ordinal))
            return path;

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home))
            home = Environment.GetEnvironmentVariable("HOME") ?? string.Empty;

        return string.IsNullOrWhiteSpace(home)
            ? path[2..]
            : Path.Combine(home, path[2..]);
    }
}
