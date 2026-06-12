using PopMark.Models;
using Spectre.Console;
using System.Text;

namespace PopMark.Helpers.Terminal;

internal static class ReactiveInputReader
{
    private static readonly Queue<ConsoleKeyInfo> PendingKeys = new();

    public static string Read(
        ref int lastWidth,
        ref int lastHeight,
        List<string> commandHistory,
        Func<PlayerSnapshot>? snapshotProvider = null,
        Func<string>? noticeProvider = null,
        Func<bool>? miniModeProvider = null,
        Func<bool>? helpModeProvider = null,
        Func<int>? queueScrollOffsetProvider = null,
        Func<bool>? controlsModeProvider = null)
    {
        if (snapshotProvider is null || noticeProvider is null)
        {
            AnsiConsole.Markup($"[bold {TerminalStyles.Accent}]popmark[/][{TerminalStyles.Muted}] >[/] ");
            return Console.ReadLine()?.Trim() ?? string.Empty;
        }

        var buffer = new StringBuilder();
        var animationFrame = 0;
        var lastRefresh = DateTimeOffset.MinValue;
        var lastScreenSignature = string.Empty;
        var trackedWidth = lastWidth;
        var trackedHeight = lastHeight;

        bool IsInputActive() => buffer.Length > 0;

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
                controlsModeProvider?.Invoke() == true,
                miniModeProvider?.Invoke() == true,
                animationFrame,
                queueScrollOffsetProvider?.Invoke() ?? 0);
            TerminalFrameRenderer.Render(context);
            lastRefresh = DateTimeOffset.UtcNow;
            lastScreenSignature = BuildScreenSignature(
                snapshotProvider,
                noticeProvider,
                miniModeProvider,
                helpModeProvider,
                controlsModeProvider,
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

            if (!HasInputKeyAvailable())
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
                        controlsModeProvider,
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

            var key = ReadInputKey();

            if (TryResolveTerminalCommand(key, buffer.Length == 0, out var terminalCommand))
            {
                if (string.IsNullOrWhiteSpace(terminalCommand))
                    continue;

                return ReturnWithSize(terminalCommand, ref lastWidth, ref lastHeight, trackedWidth, trackedHeight);
            }

            if (IsClearScreenKey(key))
                return ReturnWithSize("cls", ref lastWidth, ref lastHeight, trackedWidth, trackedHeight);

            if (buffer.Length == 0)
            {
                if (IsSpaceKey(key))
                    return ReturnWithSize("play", ref lastWidth, ref lastHeight, trackedWidth, trackedHeight);

                if (key.KeyChar == '-')
                    return ReturnWithSize("__volume-down", ref lastWidth, ref lastHeight, trackedWidth, trackedHeight);

                if (key.KeyChar == '=')
                    return ReturnWithSize("__volume-up", ref lastWidth, ref lastHeight, trackedWidth, trackedHeight);

                switch (key.Key)
                {
                    case ConsoleKey.RightArrow:
                        return ReturnWithSize("__seek-forward", ref lastWidth, ref lastHeight, trackedWidth, trackedHeight);
                    case ConsoleKey.LeftArrow:
                        return ReturnWithSize("__seek-back", ref lastWidth, ref lastHeight, trackedWidth, trackedHeight);
                    case ConsoleKey.PageUp:
                        return ReturnWithSize("__queue-page-up", ref lastWidth, ref lastHeight, trackedWidth, trackedHeight);
                    case ConsoleKey.PageDown:
                        return ReturnWithSize("__queue-page-down", ref lastWidth, ref lastHeight, trackedWidth, trackedHeight);
                    case ConsoleKey.UpArrow:
                        return ReturnWithSize("__queue-up", ref lastWidth, ref lastHeight, trackedWidth, trackedHeight);
                    case ConsoleKey.DownArrow:
                        return ReturnWithSize("__queue-down", ref lastWidth, ref lastHeight, trackedWidth, trackedHeight);
                    case ConsoleKey.Home:
                        return ReturnWithSize("__queue-home", ref lastWidth, ref lastHeight, trackedWidth, trackedHeight);
                    case ConsoleKey.End:
                        return ReturnWithSize("__queue-end", ref lastWidth, ref lastHeight, trackedWidth, trackedHeight);
                    case ConsoleKey.Oem2 when key.KeyChar == '?':
                        return ReturnWithSize("help", ref lastWidth, ref lastHeight, trackedWidth, trackedHeight);
                }
            }

            if (IsEnterKey(key))
                return ReturnWithSize(buffer.ToString().Trim(), ref lastWidth, ref lastHeight, trackedWidth, trackedHeight);

            if (IsEscapeKey(key))
                return ReturnWithSize(string.Empty, ref lastWidth, ref lastHeight, trackedWidth, trackedHeight);

            if (IsBackspaceKey(key))
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

    private static bool HasInputKeyAvailable() =>
        PendingKeys.Count > 0 || Console.KeyAvailable;

    private static ConsoleKeyInfo ReadInputKey() =>
        PendingKeys.Count > 0
            ? PendingKeys.Dequeue()
            : Console.ReadKey(intercept: true);

    private static void PushBackInputKey(ConsoleKeyInfo key) =>
        PendingKeys.Enqueue(key);

    private static bool TryResolveTerminalCommand(ConsoleKeyInfo key, bool shortcutsEnabled, out string command)
    {
        command = string.Empty;
        if (!IsEscapeKey(key))
            return false;

        var sequence = new StringBuilder().Append('\u001b');
        var deadline = DateTimeOffset.UtcNow.AddMilliseconds(150);
        while (sequence.Length < 64 && DateTimeOffset.UtcNow <= deadline)
        {
            if (!HasInputKeyAvailable())
            {
                Thread.Sleep(1);
                continue;
            }

            var next = ReadInputKey();
            if (next.KeyChar == '\0')
                continue;

            sequence.Append(next.KeyChar);
            if (IsCompleteEscapeSequence(sequence))
                break;
        }

        var value = sequence.ToString();
        if (TryResolveSgrMouseCommand(value, out command) ||
            TryResolveLegacyMouseCommand(value, out command) ||
            TryResolveCsiShortcutCommand(value, shortcutsEnabled, out command))
        {
            return true;
        }

        if (value.StartsWith("\u001b[", StringComparison.Ordinal) ||
            value.StartsWith("\u001bO", StringComparison.Ordinal))
            return true;

        return false;
    }

    private static bool TryResolveSgrMouseCommand(string sequence, out string command)
    {
        command = string.Empty;
        if (!sequence.StartsWith("\u001b[<", StringComparison.Ordinal))
            return false;

        var final = sequence[^1];
        if (final != 'M')
            return true;

        var body = sequence[3..^1];
        var parts = body.Split(';');
        if (parts.Length != 3 ||
            !int.TryParse(parts[0], out var button) ||
            !int.TryParse(parts[1], out var x) ||
            !int.TryParse(parts[2], out var y))
        {
            return true;
        }

        command = ResolveMouseButtonCommand(button, x, y);
        return true;
    }

    private static bool TryResolveLegacyMouseCommand(string sequence, out string command)
    {
        command = string.Empty;
        if (!sequence.StartsWith("\u001b[M", StringComparison.Ordinal) || sequence.Length < 6)
            return false;

        var button = sequence[3] - 32;
        var x = sequence[4] - 32;
        var y = sequence[5] - 32;
        command = ResolveMouseButtonCommand(button, x, y);
        return true;
    }

    private static bool TryResolveCsiShortcutCommand(string sequence, bool shortcutsEnabled, out string command)
    {
        command = string.Empty;
        if (!shortcutsEnabled)
            return sequence.StartsWith("\u001b[", StringComparison.Ordinal) ||
                   sequence.StartsWith("\u001bO", StringComparison.Ordinal);

        command = sequence switch
        {
            "\u001b[A" => "__queue-up",
            "\u001b[B" => "__queue-down",
            "\u001b[C" => "__seek-forward",
            "\u001b[D" => "__seek-back",
            "\u001b[5~" => "__queue-page-up",
            "\u001b[6~" => "__queue-page-down",
            "\u001b[H" or "\u001b[1~" or "\u001bOH" => "__queue-home",
            "\u001b[F" or "\u001b[4~" or "\u001bOF" => "__queue-end",
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(command) &&
            TryResolveCsiFinalShortcut(sequence, out command))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(command) ||
               sequence.StartsWith("\u001b[", StringComparison.Ordinal) ||
               sequence.StartsWith("\u001bO", StringComparison.Ordinal);
    }

    private static bool TryResolveCsiFinalShortcut(string sequence, out string command)
    {
        command = string.Empty;
        if (!sequence.StartsWith("\u001b[", StringComparison.Ordinal) ||
            sequence.Length < 3)
        {
            return false;
        }

        command = sequence[^1] switch
        {
            'A' => "__queue-up",
            'B' => "__queue-down",
            'C' => "__seek-forward",
            'D' => "__seek-back",
            'H' => "__queue-home",
            'F' => "__queue-end",
            _ => string.Empty
        };

        return !string.IsNullOrWhiteSpace(command);
    }

    private static string ResolveMouseButtonCommand(int button, int x, int y)
    {
        if ((button & 64) == 64)
            return (button & 1) == 0 ? "__queue-up" : "__queue-down";

        return (button & 3) == 0
            ? $"__mouse-click:{x}:{y}"
            : string.Empty;
    }

    private static bool IsCompleteEscapeSequence(StringBuilder sequence)
    {
        var value = sequence.ToString();
        if (value.StartsWith("\u001b[M", StringComparison.Ordinal))
            return value.Length >= 6;

        if (value.StartsWith("\u001b[<", StringComparison.Ordinal))
            return value[^1] is 'M' or 'm';

        if (value.StartsWith("\u001bO", StringComparison.Ordinal))
            return value.Length >= 3;

        return value.Length >= 3 && value[^1] is >= '@' and <= '~';
    }

    private static bool IsClearScreenKey(ConsoleKeyInfo key) =>
        key.Key == ConsoleKey.L && key.Modifiers.HasFlag(ConsoleModifiers.Control) ||
        key.KeyChar == '\f';

    private static bool IsEnterKey(ConsoleKeyInfo key) =>
        key.Key == ConsoleKey.Enter ||
        key.KeyChar is '\r' or '\n';

    private static bool IsEscapeKey(ConsoleKeyInfo key) =>
        key.Key == ConsoleKey.Escape ||
        key.KeyChar == '\u001b';

    private static bool IsBackspaceKey(ConsoleKeyInfo key) =>
        key.Key == ConsoleKey.Backspace ||
        key.KeyChar is '\b' or '\u007f';

    private static bool IsSpaceKey(ConsoleKeyInfo key) =>
        key.Key == ConsoleKey.Spacebar ||
        key.KeyChar == ' ';

    private static string? BuildScreenSignature(
        Func<PlayerSnapshot>? snapshotProvider,
        Func<string>? noticeProvider,
        Func<bool>? miniModeProvider,
        Func<bool>? helpModeProvider,
        Func<bool>? controlsModeProvider,
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
            .Append(controlsModeProvider?.Invoke() == true ? "controls" : "normal")
            .Append('|')
            .Append(snapshot.Status)
            .Append('|')
            .Append(noticeProvider())
            .Append('|')
            .Append(input)
            .Append('|')
            .Append(includeElapsed ? snapshot.Elapsed?.TotalSeconds.ToString("0") : "static-input")
            .Append('|');

        foreach (var track in snapshot.Previous)
            AppendTrack(builder.Append('|'), track);

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
