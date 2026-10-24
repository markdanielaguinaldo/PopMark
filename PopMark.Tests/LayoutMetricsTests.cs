using PopMark.Helpers.Terminal;
using Xunit;

namespace PopMark.Tests;

public sealed class LayoutMetricsTests
{
    [Fact]
    public void ContentWidth_KeepsFourColumnMargin()
    {
        Assert.Equal(76, LayoutMetrics.ContentWidth(80));
        Assert.Equal(2, LayoutMetrics.LeftMargin(80, 76));
        Assert.Equal(76, LayoutMetrics.ContentWidth(0));
    }

    [Theory]
    [InlineData(80, 24)]
    [InlineData(80, 40)]
    [InlineData(120, 60)]
    public void ComputeMain_AtNormalSizes_MatchesOriginalGeometry(int width, int height)
    {
        var metrics = LayoutMetrics.ComputeMain(width, height);

        Assert.Equal(width - 4, metrics.ContentWidth);
        Assert.Equal(height - 1, metrics.MainHeight);
        Assert.Equal(9, LayoutMetrics.VisibleQueueRows(24));
        Assert.Equal(25, LayoutMetrics.VisibleQueueRows(40));
        Assert.Equal(
            metrics.MainHeight,
            metrics.HeaderHeight + metrics.NowPlayingHeight + metrics.QueueHeight +
            metrics.PlaybackHeight + metrics.FooterHeight);
        Assert.Equal(Math.Max(1, metrics.QueueHeight - 2), metrics.VisibleQueueRows);
    }

    [Theory]
    [InlineData(40, 8)]
    [InlineData(60, 12)]
    [InlineData(100, 16)]
    [InlineData(20, 5)]
    public void ComputeMain_OnSmallTerminals_DegradesWithoutOverflow(int width, int height)
    {
        var metrics = LayoutMetrics.ComputeMain(width, height);

        Assert.True(metrics.ContentWidth >= 1);
        Assert.True(metrics.NowPlayingHeight >= 0);
        Assert.True(metrics.QueueHeight >= 0);
        Assert.True(metrics.PlaybackHeight >= 0);
        Assert.True(metrics.VisibleQueueRows >= 1);
        Assert.True(
            metrics.HeaderHeight + metrics.NowPlayingHeight + metrics.QueueHeight +
            metrics.PlaybackHeight + metrics.FooterHeight <= metrics.MainHeight);
    }

    [Fact]
    public void Resize_ChangesContentAndVisibleRows()
    {
        var wide = LayoutMetrics.ComputeMain(120, 40);
        var narrow = LayoutMetrics.ComputeMain(70, 24);

        Assert.NotEqual(wide.ContentWidth, narrow.ContentWidth);
        Assert.NotEqual(wide.VisibleQueueRows, narrow.VisibleQueueRows);
        Assert.Equal(70 - 4, narrow.ContentWidth);
        Assert.Equal(2, narrow.LeftMargin);
    }

    [Fact]
    public void VisibleQueueRows_DelegatesToMainLayout()
    {
        Assert.Equal(LayoutMetrics.ComputeMain(80, 33).VisibleQueueRows, LayoutMetrics.VisibleQueueRows(33));
        Assert.Equal(LayoutMetrics.ComputeMain(80, 24).VisibleQueueRows, LayoutMetrics.VisibleQueueRows(0));
    }
}
