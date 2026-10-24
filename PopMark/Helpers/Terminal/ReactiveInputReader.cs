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
        Func<bool>? controlsModeProvider = null,
        Func<bool>? splashModeProvider = null)
    {
        if (snapshotProvider is null || noticeProvider is null)
        {
            AnsiConsole.Markup($"[bold {TerminalStyles.Accent}]popmark[/][{TerminalStyles.Muted}] >[/] ");
            return Console.ReadLine()?.Trim() ?? string.Empty;
        }

        var editor = new CommandLineEditor(commandHistory);
        var animationFrame = 0;
        var lastMainRefresh = DateTimeOffset.MinValue;
        var lastStateCheck = DateTimeOffset.MinValue;
        var lastScreenSignature = string.Empty;
        var trackedWidth = lastWidth;
        var trackedHeight = lastHeight;
        const int PlaybackRefreshMilliseconds = 110;
        const int StateCheckMilliseconds = 250;

        void RefreshMain(bool advanceAnimation = false, bool renderInput = false, bool forceFullPaint = false)
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
                editor.Text,
                helpModeProvider?.Invoke() == true,
                controlsModeProvider?.Invoke() == true,
                miniModeProvider?.Invoke() == true,
                animationFrame,
                queueScrollOffsetProvider?.Invoke() ?? 0,
                splashModeProvider?.Invoke() == true,
                InputCaret: editor.Caret);
            TerminalFrameRenderer.Render(context, forceFullPaint, renderInput);
            lastMainRefresh = DateTimeOffset.UtcNow;
            lastStateCheck = lastMainRefresh;
            lastScreenSignature = BuildScreenSignature(
                snapshotProvider,
                noticeProvider,
                miniModeProvider,
                helpModeProvider,
                controlsModeProvider,
                queueScrollOffsetProvider,
                splashModeProvider,
                includeElapsed: true) ?? string.Empty;
        }

        void RefreshInput()
        {
            if (splashModeProvider?.Invoke() == true)
                return;

            var (width, height) = TerminalHost.GetWindowSize();
            trackedWidth = width;
            trackedHeight = height;
            TerminalFrameRenderer.RenderInputLine(width, height, editor.Text, editor.Caret);
        }

        bool HandleEditCommand(string command)
        {
            var changed = command switch
            {
                "__edit-left" => editor.MoveLeft(),
                "__edit-right" => editor.MoveRight(),
                "__edit-home" => editor.MoveHome(),
                "__edit-end" => editor.MoveEnd(),
                "__edit-delete" => editor.Delete(),
                "__history-up" => editor.HistoryPrevious(),
                "__history-down" => editor.HistoryNext(),
                _ => false
            };

            if (command.StartsWith("__edit-", StringComparison.Ordinal) ||
                command.StartsWith("__history-", StringComparison.Ordinal))
            {
                if (changed)
                    RefreshInput();

                return true;
            }

            return false;
        }

        RefreshMain(renderInput: true, forceFullPaint: true);

        while (true)
        {
            var (width, height) = TerminalHost.GetWindowSize();
            if (width > 0 && height > 0 && (width != trackedWidth || height != trackedHeight))
            {
                RefreshMain(renderInput: true, forceFullPaint: true);
                continue;
            }

            if (!HasInputKeyAvailable())
            {
                var now = DateTimeOffset.UtcNow;
                var snapshot = snapshotProvider();
                var playbackCanRefresh = snapshot.Status is PlaybackStatus.Playing or PlaybackStatus.Loading;
                var refreshDue = playbackCanRefresh &&
                                 (now - lastMainRefresh).TotalMilliseconds >= PlaybackRefreshMilliseconds;
                var stateChanged = false;
                if ((now - lastStateCheck).TotalMilliseconds >= StateCheckMilliseconds)
                {
                    lastStateCheck = now;
                    var screenSignature = BuildScreenSignature(
                        snapshotProvider,
                        noticeProvider,
                        miniModeProvider,
                        helpModeProvider,
                        controlsModeProvider,
                        queueScrollOffsetProvider,
                        splashModeProvider,
                        includeElapsed: true) ?? string.Empty;
                    stateChanged = !string.Equals(screenSignature, lastScreenSignature, StringComparison.Ordinal);
                }

                if (refreshDue || stateChanged)
                {
                    RefreshMain(advanceAnimation: playbackCanRefresh, renderInput: false);
                }

                Thread.Sleep(20);
                continue;
            }

            var key = ReadInputKey();

            if (TryResolveTerminalCommand(key, editor.IsEmpty, out var terminalCommand))
            {
                if (HandleEditCommand(terminalCommand))
                    continue;

                if (string.IsNullOrWhiteSpace(terminalCommand))
                    continue;

                return ReturnWithSize(terminalCommand, ref lastWidth, ref lastHeight, trackedWidth, trackedHeight);
            }

            if (IsClearScreenKey(key))
                return ReturnWithSize("cls", ref lastWidth, ref lastHeight, trackedWidth, trackedHeight);

            if (IsTabKey(key))
                return ReturnWithSize("__toggle-view", ref lastWidth, ref lastHeight, trackedWidth, trackedHeight);

            if (editor.IsEmpty && ResolveEmptyBufferShortcut(key) is { } shortcut)
                return ReturnWithSize(shortcut, ref lastWidth, ref lastHeight, trackedWidth, trackedHeight);

            if (IsEnterKey(key))
                return ReturnWithSize(editor.Text.Trim(), ref lastWidth, ref lastHeight, trackedWidth, trackedHeight);

            if (IsEscapeKey(key))
                return ReturnWithSize(string.Empty, ref lastWidth, ref lastHeight, trackedWidth, trackedHeight);

            if (IsBackspaceKey(key))
            {
                if (editor.Backspace())
                    RefreshInput();

                continue;
            }

            if (!editor.IsEmpty)
            {
                var moved = key.Key switch
                {
                    ConsoleKey.LeftArrow => editor.MoveLeft(),
                    ConsoleKey.RightArrow => editor.MoveRight(),
                    ConsoleKey.Home => editor.MoveHome(),
                    ConsoleKey.End => editor.MoveEnd(),
                    ConsoleKey.Delete => editor.Delete(),
                    ConsoleKey.UpArrow => editor.HistoryPrevious(),
                    ConsoleKey.DownArrow => editor.HistoryNext(),
                    _ => false
                };

                if (moved)
                {
                    RefreshInput();
                    continue;
                }

                if (key.Key is ConsoleKey.LeftArrow or ConsoleKey.RightArrow or
                    ConsoleKey.Home or ConsoleKey.End or ConsoleKey.Delete or
                    ConsoleKey.UpArrow or ConsoleKey.DownArrow)
                {
                    continue;
                }
            }

            if (!char.IsControl(key.KeyChar))
            {
                editor.Insert(key.KeyChar);
                RefreshInput();
            }
        }
    }

    private static string ReturnWithSize(string value, ref int lastWidth, ref int lastHeight, int trackedWidth, int trackedHeight)
    {
        lastWidth = trackedWidth;
        lastHeight = trackedHeight;
        return value;
    }

    /// <summary>
    /// Maps a key to a playback/view command when the command buffer is empty.
    /// Returns <see langword="null"/> for keys that should be inserted as text.
    /// </summary>
    internal static string? ResolveEmptyBufferShortcut(ConsoleKeyInfo key)
    {
        if (IsSpaceKey(key))
            return "play";

        if (key.KeyChar == '-')
            return "__volume-down";

        if (key.KeyChar == '=')
            return "__volume-up";

        if (key.KeyChar == '[')
            return "__previous-track";

        if (key.KeyChar == ']')
            return "__next-track";

        return key.Key switch
        {
            ConsoleKey.RightArrow => "__seek-forward",
            ConsoleKey.LeftArrow => "__seek-back",
            ConsoleKey.PageUp => "__queue-page-up",
            ConsoleKey.PageDown => "__queue-page-down",
            ConsoleKey.UpArrow => "__queue-up",
            ConsoleKey.DownArrow => "__queue-down",
            ConsoleKey.Home => "__queue-home",
            ConsoleKey.End => "__queue-end",
            ConsoleKey.Oem2 when key.KeyChar == '?' => "help",
            _ => null
        };
    }

    /// <summary>Test seam for the escape-sequence router.</summary>
    internal static string ResolveTerminalSequence(string sequence, bool shortcutsEnabled)
    {
        _ = TryResolveCsiShortcutCommand(sequence, shortcutsEnabled, out var command);
        return command;
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
        {
            command = sequence switch
            {
                "\u001b[A" => "__history-up",
                "\u001b[B" => "__history-down",
                "\u001b[C" or "\u001b[1;2C" => "__edit-right",
                "\u001b[D" or "\u001b[1;2D" => "__edit-left",
                "\u001b[H" or "\u001b[1~" or "\u001bOH" => "__edit-home",
                "\u001b[F" or "\u001b[4~" or "\u001bOF" => "__edit-end",
                "\u001b[3~" => "__edit-delete",
                _ => string.Empty
            };

            return true;
        }

        command = sequence switch
        {
            "\u001b[A" => "__queue-up",
            "\u001b[B" => "__queue-down",
            "\u001b[C" => "__seek-forward",
            "\u001b[D" => "__seek-back",
            "\u001b[1;2C" => "__seek-forward",
            "\u001b[1;2D" => "__seek-back",
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
            return (button & 1) == 0
                ? $"__mouse-wheel-up:{x}:{y}"
                : $"__mouse-wheel-down:{x}:{y}";

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

    private static bool IsTabKey(ConsoleKeyInfo key) =>
        key.Key == ConsoleKey.Tab ||
        key.KeyChar == '\t';

    private static string? BuildScreenSignature(
        Func<PlayerSnapshot>? snapshotProvider,
        Func<string>? noticeProvider,
        Func<bool>? miniModeProvider,
        Func<bool>? helpModeProvider,
        Func<bool>? controlsModeProvider,
        Func<int>? queueScrollOffsetProvider,
        Func<bool>? splashModeProvider,
        bool includeElapsed)
    {
        if (snapshotProvider is null || noticeProvider is null)
            return null;

        var snapshot = snapshotProvider();
        var builder = new StringBuilder()
            .Append(miniModeProvider?.Invoke() == true ? "mini" : "full")
            .Append('|')
            .Append(splashModeProvider?.Invoke() == true ? "splash" : "playlist")
            .Append('|')
            .Append(helpModeProvider?.Invoke() == true ? "help" : "normal")
            .Append('|')
            .Append(controlsModeProvider?.Invoke() == true ? "controls" : "normal")
            .Append('|')
            .Append(snapshot.Status)
            .Append('|')
            .Append(snapshot.VolumePercent)
            .Append('|')
            .Append(noticeProvider())
            .Append('|')
            .Append(queueScrollOffsetProvider?.Invoke() ?? 0)
            .Append('|')
            .Append(includeElapsed && snapshot.Elapsed is { } elapsed ? ((long)(elapsed.TotalMilliseconds / 250)).ToString() : "static")
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
