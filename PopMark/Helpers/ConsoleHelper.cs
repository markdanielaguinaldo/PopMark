using PopMark.Helpers.Terminal;
using PopMark.Models;

namespace PopMark.Helpers;

public static class ConsoleHelper
{
    public static void InitializeTerminalCapabilities() =>
        TerminalHost.InitializeTerminalCapabilities();

    public static void ShowStartupSplash() =>
        StartupSplash.Show();

    public static void EnterInteractiveScreen() =>
        TerminalHost.EnterInteractiveScreen();

    public static void LeaveInteractiveScreen() =>
        TerminalHost.LeaveInteractiveScreen();

    public static void DrawCommandCenter(PlayerSnapshot snapshot, string notice, bool showHelp = false, int queueScrollOffset = 0) =>
        TerminalFrameRenderer.DrawCommandCenter(snapshot, notice, showHelp, queueScrollOffset);

    public static void DrawMiniPlayer(PlayerSnapshot snapshot, string notice) =>
        TerminalFrameRenderer.DrawMiniPlayer(snapshot, notice);

    public static bool TryResolveProgressClick(int x, int y, PlayerSnapshot snapshot, out TimeSpan timestamp) =>
        TerminalFrameRenderer.TryResolveProgressClick(x, y, snapshot, out timestamp);

    public static string ReadReactiveInput(
        ref int lastWidth,
        ref int lastHeight,
        List<string> commandHistory,
        Func<PlayerSnapshot>? snapshotProvider = null,
        Func<string>? noticeProvider = null,
        Func<bool>? miniModeProvider = null,
        Func<bool>? helpModeProvider = null,
        Func<int>? queueScrollOffsetProvider = null) =>
        ReactiveInputReader.Read(
            ref lastWidth,
            ref lastHeight,
            commandHistory,
            snapshotProvider,
            noticeProvider,
            miniModeProvider,
            helpModeProvider,
            queueScrollOffsetProvider);

    public static string[] SplitArgs(string commandLine) =>
        CommandLineParser.SplitArgs(commandLine);

    public static void UseBarCursor() =>
        TerminalHost.UseBarCursor();

    public static void ShowCursor() =>
        TerminalHost.ShowCursor();

    public static void ResetCursorStyle() =>
        TerminalHost.ResetCursorStyle();

    public static (int Width, int Height) GetWindowSize() =>
        TerminalHost.GetWindowSize();

    public static void Info(string message) =>
        TerminalHost.Info(message);

    public static void Warn(string message) =>
        TerminalHost.Warn(message);

    public static void Success(string message) =>
        TerminalHost.Success(message);

    public static void Error(string message) =>
        TerminalHost.Error(message);
}
