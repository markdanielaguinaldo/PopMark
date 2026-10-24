namespace PopMark.Helpers.Terminal;

/// <summary>Computed geometry for the playback screen.</summary>
internal readonly record struct MainLayoutMetrics(
    int ContentWidth,
    int LeftMargin,
    int MainHeight,
    int HeaderHeight,
    int NowPlayingHeight,
    int QueueHeight,
    int PlaybackHeight,
    int FooterHeight,
    int VisibleQueueRows);

/// <summary>
/// Single source of truth for the main screen geometry. Components, hitboxes and
/// queue scrolling all use these values so they cannot drift apart.
/// </summary>
internal static class LayoutMetrics
{
    private const int PreferredMinimumWidth = 80;
    private const int PreferredMinimumHeight = 24;

    public static int ContentWidth(int terminalWidth)
    {
        var width = terminalWidth > 0 ? terminalWidth : PreferredMinimumWidth;
        return Math.Max(1, width - 4);
    }

    public static int LeftMargin(int terminalWidth, int contentWidth) =>
        Math.Max(0, ((terminalWidth > 0 ? terminalWidth : contentWidth) - contentWidth) / 2);

    public static MainLayoutMetrics ComputeMain(int terminalWidth, int terminalHeight)
    {
        var width = terminalWidth > 0 ? terminalWidth : PreferredMinimumWidth;
        var height = terminalHeight > 0 ? terminalHeight : PreferredMinimumHeight;
        var contentWidth = ContentWidth(width);
        var leftMargin = LeftMargin(width, contentWidth);
        var mainHeight = Math.Max(1, height - 1);

        var headerHeight = Math.Min(1, mainHeight);
        var footerHeight = mainHeight >= 2 ? 1 : 0;
        var available = Math.Max(0, mainHeight - headerHeight - footerHeight);

        var nowPlayingHeight = available >= 13 ? 6 : available >= 9 ? 4 : available >= 4 ? 3 : available;
        var remaining = Math.Max(0, available - nowPlayingHeight);
        var playbackHeight = remaining >= 7 ? 4 : remaining >= 3 ? 2 : 0;
        var queueHeight = Math.Max(0, remaining - playbackHeight);

        return new MainLayoutMetrics(
            contentWidth,
            leftMargin,
            mainHeight,
            headerHeight,
            nowPlayingHeight,
            queueHeight,
            playbackHeight,
            footerHeight,
            Math.Max(1, queueHeight - 2));
    }

    public static int VisibleQueueRows(int terminalHeight) =>
        ComputeMain(PreferredMinimumWidth, terminalHeight).VisibleQueueRows;
}
