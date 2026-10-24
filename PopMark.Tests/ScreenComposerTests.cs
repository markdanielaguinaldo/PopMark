using PopMark.Helpers.Terminal.Components;
using PopMark.Helpers.Terminal.Rendering;
using Xunit;

namespace PopMark.Tests;

public sealed class ScreenComposerTests
{
    private const string ClearScreen = "\u001b[2J";

    [Fact]
    public void FirstFrame_FullPaint_ContainsClearAndContent()
    {
        var current = new ScreenBuffer(8, 2);
        current.WriteText(0, 0, "hello", CellStyle.Default);

        var output = ScreenComposer.Compose(null, current, forceFullPaint: false, useColor: true);

        Assert.Contains(ClearScreen, output);
        Assert.Contains("hello", output);
    }

    [Fact]
    public void UnchangedFrame_DoesNotClearOrRewriteContent()
    {
        var buffer = new ScreenBuffer(8, 1);
        buffer.WriteText(0, 0, "hello", CellStyle.Default);

        var output = ScreenComposer.Compose(buffer, buffer, forceFullPaint: false, useColor: true);

        Assert.DoesNotContain(ClearScreen, output);
        Assert.DoesNotContain("hello", output);
    }

    [Fact]
    public void ChangedCell_EmitsOnlyChangedRun()
    {
        var previous = new ScreenBuffer(11, 1);
        previous.WriteText(0, 0, "hello world", CellStyle.Default);
        var current = new ScreenBuffer(11, 1);
        current.WriteText(0, 0, "hello WORLD", CellStyle.Default);

        var output = ScreenComposer.Compose(previous, current, forceFullPaint: false, useColor: true);

        Assert.Contains("WORLD", output);
        Assert.DoesNotContain("hello", output);
        Assert.DoesNotContain(ClearScreen, output);
    }

    [Fact]
    public void ResizedFrame_ForcesFullPaint()
    {
        var previous = new ScreenBuffer(6, 1);
        var current = new ScreenBuffer(8, 1);
        current.WriteText(0, 0, "resize", CellStyle.Default);

        var output = ScreenComposer.Compose(previous, current, forceFullPaint: false, useColor: true);

        Assert.Contains(ClearScreen, output);
        Assert.Contains("resize", output);
    }

    [Fact]
    public void ForceFullPaint_OnSameBuffer_ClearsAgain()
    {
        var buffer = new ScreenBuffer(4, 1);
        buffer.WriteText(0, 0, "same", CellStyle.Default);

        var output = ScreenComposer.Compose(buffer, buffer, forceFullPaint: true, useColor: true);

        Assert.Contains(ClearScreen, output);
        Assert.Contains("same", output);
    }

    [Fact]
    public void CursorPlacement_IsOneBasedAndShownOnlyOnRequest()
    {
        var buffer = new ScreenBuffer(6, 2);

        var hidden = ScreenComposer.Compose(null, buffer, true, true);
        var shown = ScreenComposer.Compose(null, buffer, true, true, cursorRow: 1, cursorColumn: 3, showCursor: true);

        Assert.DoesNotContain("\u001b[?25h", hidden);
        Assert.Contains("\u001b[?25h", shown);
        Assert.Contains("\u001b[2;4H", shown);
    }

    [Fact]
    public void ComposeRow_OnlyWritesChangedRow()
    {
        var previousRow = new ScreenBuffer(8, 1);
        previousRow.WriteText(0, 0, "abcdef", CellStyle.Default);
        var currentRow = new ScreenBuffer(8, 1);
        currentRow.WriteText(0, 0, "abcXef", CellStyle.Default);

        var output = ScreenComposer.ComposeRow(previousRow, currentRow, terminalRow: 7, useColor: true);

        Assert.Contains("X", output);
        Assert.DoesNotContain("abcdef", output);
        Assert.DoesNotContain(ClearScreen, output);
    }
}
