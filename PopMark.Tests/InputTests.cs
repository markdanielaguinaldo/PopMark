using PopMark.Helpers.Terminal;
using Xunit;

namespace PopMark.Tests;

public sealed class InputTests
{
    [Fact]
    public void Editor_InsertsAtCaretAndMoves()
    {
        var editor = new CommandLineEditor(new List<string>());

        editor.Insert('a');
        editor.Insert('c');
        editor.MoveLeft();
        editor.Insert('b');

        Assert.Equal("abc", editor.Text);
        Assert.Equal(2, editor.Caret);

        editor.MoveHome();
        Assert.Equal(0, editor.Caret);
        editor.MoveEnd();
        Assert.Equal(3, editor.Caret);
    }

    [Fact]
    public void Editor_BackspaceAndDeleteRespectCaret()
    {
        var editor = new CommandLineEditor(new List<string>());
        foreach (var character in "abcd")
            editor.Insert(character);

        editor.MoveLeft();
        editor.MoveLeft();
        Assert.True(editor.Backspace());
        Assert.Equal("acd", editor.Text);
        Assert.Equal(1, editor.Caret);

        Assert.True(editor.Delete());
        Assert.Equal("ad", editor.Text);
        Assert.Equal(1, editor.Caret);
        Assert.True(editor.Delete());
        Assert.Equal("a", editor.Text);
        Assert.False(editor.Delete());
    }

    [Fact]
    public void Editor_HistoryOnlyWorksWithNonEmptyBuffer()
    {
        var editor = new CommandLineEditor(new List<string> { "first", "second" });

        Assert.False(editor.HistoryPrevious());
        Assert.False(editor.HistoryNext());

        editor.Insert('x');
        Assert.True(editor.HistoryPrevious());
        Assert.Equal("second", editor.Text);
        Assert.True(editor.HistoryPrevious());
        Assert.Equal("first", editor.Text);
        Assert.True(editor.HistoryNext());
        Assert.Equal("second", editor.Text);
        Assert.True(editor.HistoryNext());
        Assert.Equal("x", editor.Text);
    }

    [Fact]
    public void Editor_EditsResetHistoryCursor()
    {
        var editor = new CommandLineEditor(new List<string> { "older", "newer" });
        editor.Insert('z');
        editor.HistoryPrevious();
        Assert.Equal("newer", editor.Text);

        editor.Insert('!');

        Assert.Equal("newer!", editor.Text);
        Assert.True(editor.HistoryPrevious());
        Assert.Equal("newer", editor.Text);
    }

    [Theory]
    [InlineData('a', ConsoleKey.A, null)]
    [InlineData(' ', ConsoleKey.Spacebar, "play")]
    [InlineData('-', ConsoleKey.OemMinus, "__volume-down")]
    [InlineData('=', ConsoleKey.OemPlus, "__volume-up")]
    [InlineData('[', ConsoleKey.Oem4, "__previous-track")]
    [InlineData(']', ConsoleKey.Oem6, "__next-track")]
    public void EmptyBuffer_CharacterShortcutsMatchExistingBindings(char character, ConsoleKey key, string? expected)
    {
        var info = new ConsoleKeyInfo(character, key, false, false, false);

        Assert.Equal(expected, ReactiveInputReader.ResolveEmptyBufferShortcut(info));
    }

    [Theory]
    [InlineData(ConsoleKey.UpArrow, "__queue-up")]
    [InlineData(ConsoleKey.DownArrow, "__queue-down")]
    [InlineData(ConsoleKey.LeftArrow, "__seek-back")]
    [InlineData(ConsoleKey.RightArrow, "__seek-forward")]
    [InlineData(ConsoleKey.PageUp, "__queue-page-up")]
    [InlineData(ConsoleKey.PageDown, "__queue-page-down")]
    [InlineData(ConsoleKey.Home, "__queue-home")]
    [InlineData(ConsoleKey.End, "__queue-end")]
    public void EmptyBuffer_ArrowShortcutsMatchExistingBindings(ConsoleKey key, string expected)
    {
        var info = new ConsoleKeyInfo('\0', key, false, false, false);

        Assert.Equal(expected, ReactiveInputReader.ResolveEmptyBufferShortcut(info));
    }

    [Fact]
    public void EmptyBuffer_QuestionMarkOpensHelp()
    {
        var info = new ConsoleKeyInfo('?', ConsoleKey.Oem2, true, false, false);

        Assert.Equal("help", ReactiveInputReader.ResolveEmptyBufferShortcut(info));
    }

    [Theory]
    [InlineData("\u001b[A", "__queue-up")]
    [InlineData("\u001b[B", "__queue-down")]
    [InlineData("\u001b[C", "__seek-forward")]
    [InlineData("\u001b[3~", "")]
    public void EscapeSequence_WithEmptyBuffer_KeepsPlaybackRouting(string sequence, string expected)
    {
        Assert.Equal(expected, ReactiveInputReader.ResolveTerminalSequence(sequence, shortcutsEnabled: true));
    }

    [Theory]
    [InlineData("\u001b[A", "__history-up")]
    [InlineData("\u001b[B", "__history-down")]
    [InlineData("\u001b[C", "__edit-right")]
    [InlineData("\u001b[D", "__edit-left")]
    [InlineData("\u001b[H", "__edit-home")]
    [InlineData("\u001b[F", "__edit-end")]
    [InlineData("\u001b[3~", "__edit-delete")]
    public void EscapeSequence_WithText_RoutesToEditing(string sequence, string expected)
    {
        Assert.Equal(expected, ReactiveInputReader.ResolveTerminalSequence(sequence, shortcutsEnabled: false));
    }

    [Fact]
    public void TypedCharacters_AreInsertedInsteadOfResolvedWhenBufferHasText()
    {
        var editor = new CommandLineEditor(new List<string>());

        editor.Insert('a');
        editor.Insert('-');
        editor.Insert('=');
        editor.Insert('[');
        editor.Insert(']');

        Assert.Equal("a-=[]", editor.Text);
        Assert.Null(ReactiveInputReader.ResolveEmptyBufferShortcut(
            new ConsoleKeyInfo('a', ConsoleKey.A, false, false, false)));
    }

    [Theory]
    [InlineData("short", 5, 20, "short", 5)]
    [InlineData("abcdefghij", 10, 4, "ghij", 4)]
    [InlineData("abcdefghij", 0, 4, "abcd", 0)]
    public void VisibleWindow_KeepsCaretInView(string text, int caret, int width, string expected, int expectedCaret)
    {
        var (visible, caretOffset) = PopMark.Helpers.Terminal.Components.InputFieldComponent
            .VisibleWindow(text, caret, width);

        Assert.Equal(expected, visible);
        Assert.Equal(expectedCaret, caretOffset);
    }
}
