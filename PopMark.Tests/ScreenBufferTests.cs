using PopMark.Helpers.Terminal.Components;
using PopMark.Helpers.Terminal.Rendering;
using System.Text;
using Xunit;

namespace PopMark.Tests;

public sealed class ScreenBufferTests
{
    [Fact]
    public void WriteText_PlacesCharactersAndClipsToMaxWidth()
    {
        var buffer = new ScreenBuffer(10, 2);

        var written = buffer.WriteText(1, 1, "hello", CellStyle.Default, maxWidth: 3);

        Assert.Equal(3, written);
        Assert.Equal(" hel", buffer.ToLines()[1]);
    }

    [Fact]
    public void Set_InheritsExistingBackgroundWhenStyleHasNone()
    {
        var background = CellStyle.FromBackground("#0b0f14");
        var buffer = new ScreenBuffer(4, 1, background);

        buffer.Set(0, 0, new Rune('x'), CellStyle.FromColor("#ffffff"));

        var cell = buffer.Get(0, 0);
        Assert.Equal('x', cell.Rune.ToString()[0]);
        Assert.Equal(background.Background, cell.Style.Background);
    }

    [Fact]
    public void WideRune_OccupiesContinuationCell_AndIsClearedWhenOverwritten()
    {
        var buffer = new ScreenBuffer(6, 1);

        buffer.Set(1, 0, new Rune('界'), CellStyle.Default);

        Assert.True(buffer.Get(1, 0).IsWide);
        Assert.True(buffer.Get(2, 0).IsContinuation);

        buffer.Set(2, 0, new Rune('a'), CellStyle.Default);

        Assert.False(buffer.Get(1, 0).IsContinuation);
        Assert.Equal(" ", buffer.Get(1, 0).Rune.ToString());
        Assert.Equal("a", buffer.Get(2, 0).Rune.ToString());
    }

    [Fact]
    public void ClipWideRuneAtRightEdge_ReplacesWithSpace()
    {
        var buffer = new ScreenBuffer(3, 1);

        buffer.Set(2, 0, new Rune('界'), CellStyle.Default);

        Assert.Equal(" ", buffer.Get(2, 0).Rune.ToString());
        Assert.False(buffer.Get(2, 0).IsWide);
    }

    [Fact]
    public void DrawBox_DrawsRoundedBorderAndTitle()
    {
        var buffer = new ScreenBuffer(20, 5);

        buffer.DrawBox(new UiRect(0, 0, 20, 5), UiBorderStyle.Rounded, CellStyle.Default, "Queue");

        var lines = buffer.ToLines(trimEnd: false);
        Assert.StartsWith("╭", lines[0]);
        Assert.Contains("Queue", lines[0]);
        Assert.EndsWith("╮", lines[0].TrimEnd());
        Assert.StartsWith("│", lines[1]);
        Assert.StartsWith("╰", lines[4]);
    }

    [Fact]
    public void ExtractAndApplyRow_RoundTripsCells()
    {
        var buffer = new ScreenBuffer(8, 3);
        buffer.WriteText(0, 1, "abcdef", CellStyle.Default);

        var row = buffer.ExtractRow(1);
        buffer.WriteText(0, 1, "ZZZZZZ", CellStyle.Default);
        buffer.ApplyRow(row, 1);

        Assert.Equal("abcdef", buffer.ToLines()[1]);
        Assert.Equal("abcdef", row.ToLines()[0]);
    }

    [Fact]
    public void Fill_ClipsToBufferBounds()
    {
        var buffer = new ScreenBuffer(4, 2);

        buffer.Fill(new UiRect(-2, -2, 10, 10), new Rune('.'), CellStyle.Default);

        Assert.Equal(new[] { "....", "...." }, buffer.ToLines());
    }

    [Fact]
    public void Visible_TextAndStack_MeasureRespectConstraints()
    {
        var text = new TextComponent { Text = "abcdefgh", PreferredWidth = 4, MinWidth = 2 };

        var size = text.Measure(new UiSize(100, 10));

        Assert.Equal(4, size.Width);
        Assert.Equal(1, size.Height);
    }
}
