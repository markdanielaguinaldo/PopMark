using PopMark.Models;
using Spectre.Console;
using System.Text;

namespace PopMark.Helpers.Terminal;

internal static class ReactiveInputReader
{
    public static string Read(
        ref int lastWidth,
        ref int lastHeight,
        List<string> commandHistory,
        Func<PlayerSnapshot>? snapshotProvider = null,
        Func<string>? noticeProvider = null,
        Func<bool>? miniModeProvider = null,
        Func<bool>? helpModeProvider = null)
    {
        if (snapshotProvider is null || noticeProvider is null)
        {
            AnsiConsole.Markup($"[bold {TerminalStyles.Accent}]popmark[/][{TerminalStyles.Muted}] >[/] ");
            return Console.ReadLine()?.Trim() ?? string.Empty;
        }

        var buffer = new StringBuilder();
        var historyIndex = commandHistory.Count;
        var browsingHistory = false;
        var draftInput = string.Empty;
        var animationFrame = 0;
        var lastRefresh = DateTimeOffset.MinValue;
        var lastScreenSignature = string.Empty;
        var trackedWidth = lastWidth;
        var trackedHeight = lastHeight;

        bool IsInputActive() => buffer.Length > 0 || browsingHistory;

        void RefreshScreen(bool advanceAnimation = false)
        {
            if (advanceAnimation)
                animationFrame++;

            var (width, height) = TerminalHost.GetWindowSize();
            trackedWidth = width;
            trackedHeight = height;
            var context = new RenderContext(
                width,
                height,
                snapshotProvider(),
                noticeProvider(),
                buffer.ToString(),
                helpModeProvider?.Invoke() == true,
                miniModeProvider?.Invoke() == true,
                animationFrame);
            TerminalFrameRenderer.Render(context);
            lastRefresh = DateTimeOffset.UtcNow;
            lastScreenSignature = BuildScreenSignature(
                snapshotProvider,
                noticeProvider,
                miniModeProvider,
                helpModeProvider,
                buffer.ToString(),
                includeElapsed: !IsInputActive()) ?? string.Empty;
        }

        RefreshScreen();

        while (true)
        {
            var (width, height) = TerminalHost.GetWindowSize();
            if (width > 0 && height > 0 && (width != trackedWidth || height != trackedHeight))
            {
                RefreshScreen();
                continue;
            }

            if (!Console.KeyAvailable)
            {
                var now = DateTimeOffset.UtcNow;
                var snapshot = snapshotProvider();
                var inputActive = IsInputActive();
                var isAnimating = !inputActive && snapshot.Status is PlaybackStatus.Playing or PlaybackStatus.Loading;
                var refreshMilliseconds = isAnimating ? 110 : 250;
                if ((now - lastRefresh).TotalMilliseconds >= refreshMilliseconds)
                {
                    var screenSignature = BuildScreenSignature(
                        snapshotProvider,
                        noticeProvider,
                        miniModeProvider,
                        helpModeProvider,
                        buffer.ToString(),
                        includeElapsed: !inputActive) ?? string.Empty;
                    if (isAnimating || !string.Equals(screenSignature, lastScreenSignature, StringComparison.Ordinal))
                    {
                        RefreshScreen(advanceAnimation: isAnimating);
                    }
                }

                Thread.Sleep(isAnimating ? 24 : 40);
                continue;
            }

            var key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.L && key.Modifiers.HasFlag(ConsoleModifiers.Control))
                return ReturnWithSize("cls", ref lastWidth, ref lastHeight, trackedWidth, trackedHeight);

            if (buffer.Length == 0 && !browsingHistory)
            {
                switch (key.Key)
                {
                    case ConsoleKey.Spacebar:
                        return ReturnWithSize("play", ref lastWidth, ref lastHeight, trackedWidth, trackedHeight);
                    case ConsoleKey.N:
                        return ReturnWithSize("next", ref lastWidth, ref lastHeight, trackedWidth, trackedHeight);
                    case ConsoleKey.M:
                        return ReturnWithSize("mini", ref lastWidth, ref lastHeight, trackedWidth, trackedHeight);
                    case ConsoleKey.Q:
                        return ReturnWithSize("q", ref lastWidth, ref lastHeight, trackedWidth, trackedHeight);
                    case ConsoleKey.Oem2 when key.KeyChar == '?':
                        return ReturnWithSize("help", ref lastWidth, ref lastHeight, trackedWidth, trackedHeight);
                }
            }

            if (key.Key == ConsoleKey.Enter)
                return ReturnWithSize(buffer.ToString().Trim(), ref lastWidth, ref lastHeight, trackedWidth, trackedHeight);

            if (key.Key == ConsoleKey.Escape)
                return ReturnWithSize(string.Empty, ref lastWidth, ref lastHeight, trackedWidth, trackedHeight);

            if (key.Key == ConsoleKey.UpArrow)
            {
                if (commandHistory.Count == 0)
                    continue;

                if (!browsingHistory)
                {
                    draftInput = buffer.ToString();
                    browsingHistory = true;
                    historyIndex = commandHistory.Count;
                }

                if (historyIndex > 0)
                {
                    historyIndex--;
                    buffer.Clear();
                    buffer.Append(commandHistory[historyIndex]);
                    RefreshScreen();
                }

                continue;
            }

            if (key.Key == ConsoleKey.DownArrow)
            {
                if (!browsingHistory)
                    continue;

                if (historyIndex < commandHistory.Count - 1)
                {
                    historyIndex++;
                    buffer.Clear();
                    buffer.Append(commandHistory[historyIndex]);
                }
                else
                {
                    historyIndex = commandHistory.Count;
                    browsingHistory = false;
                    buffer.Clear();
                    buffer.Append(draftInput);
                }

                RefreshScreen();
                continue;
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (buffer.Length > 0)
                {
                    buffer.Length--;
                    RefreshScreen();
                }

                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                buffer.Append(key.KeyChar);
                RefreshScreen();
            }
        }

    }

    private static string ReturnWithSize(string value, ref int lastWidth, ref int lastHeight, int trackedWidth, int trackedHeight)
    {
        lastWidth = trackedWidth;
        lastHeight = trackedHeight;
        return value;
    }

    private static string? BuildScreenSignature(
        Func<PlayerSnapshot>? snapshotProvider,
        Func<string>? noticeProvider,
        Func<bool>? miniModeProvider,
        Func<bool>? helpModeProvider,
        string input,
        bool includeElapsed)
    {
        if (snapshotProvider is null || noticeProvider is null)
            return null;

        var snapshot = snapshotProvider();
        var builder = new StringBuilder()
            .Append(miniModeProvider?.Invoke() == true ? "mini" : "full")
            .Append('|')
            .Append(helpModeProvider?.Invoke() == true ? "help" : "normal")
            .Append('|')
            .Append(snapshot.Status)
            .Append('|')
            .Append(noticeProvider())
            .Append('|')
            .Append(input)
            .Append('|')
            .Append(includeElapsed ? snapshot.Elapsed?.TotalSeconds.ToString("0") : "static-input")
            .Append('|');

        AppendTrack(builder, snapshot.Current);

        foreach (var track in snapshot.Pending)
            AppendTrack(builder.Append('|'), track);

        return builder.ToString();
    }

    private static void AppendTrack(StringBuilder builder, Track? track)
    {
        if (track is null)
        {
            builder.Append("<none>");
            return;
        }

        builder
            .Append(track.Title)
            .Append('\u001f')
            .Append(track.Url)
            .Append('\u001f')
            .Append(track.DisplaySource)
            .Append('\u001f')
            .Append(track.Duration);
    }
}
