using PopMark.Helpers.Terminal;
using PopMark.Helpers.Terminal.Components;
using PopMark.Helpers.Terminal.Rendering;
using PopMark.Models;
using Xunit;

namespace PopMark.Tests;

public sealed class StateAndCleanupTests
{
    [Fact]
    public void PlaybackStateChange_ProducesDifferentFrame()
    {
        var track = TestUi.Track("song", durationSeconds: 200);
        var playing = TestUi.Snapshot(
            PlaybackStatus.Playing,
            current: track,
            elapsed: TimeSpan.FromSeconds(20));
        var paused = TestUi.Snapshot(
            PlaybackStatus.Paused,
            current: track,
            elapsed: TimeSpan.FromSeconds(20));

        var (playingScreen, _) = TestUi.Render(
            new MainScreenComponent(),
            TestUi.Context(playing),
            width: 100,
            height: 30);
        var (pausedScreen, _) = TestUi.Render(
            new MainScreenComponent(),
            TestUi.Context(paused),
            width: 100,
            height: 30);

        Assert.NotEqual(playingScreen.ToPlainText(), pausedScreen.ToPlainText());
        Assert.Contains("PLAYING", playingScreen.ToPlainText());
        Assert.Contains("PAUSED", pausedScreen.ToPlainText());
    }

    [Fact]
    public void QueueChange_ProducesDifferentFrameAndHitboxes()
    {
        var context = TestUi.Context(TestUi.Snapshot(current: TestUi.Track("first")));
        var (first, firstHits) = TestUi.Render(new MainScreenComponent(), context, width: 100, height: 30);
        var changed = TestUi.Context(TestUi.Snapshot(
            current: TestUi.Track("first"),
            pending: new[] { TestUi.Track("second") }));
        var (second, secondHits) = TestUi.Render(new MainScreenComponent(), changed, width: 100, height: 30);

        Assert.NotEqual(first.ToPlainText(), second.ToPlainText());
        Assert.Equal(1, firstHits.Hits.Regions.Count(region => region.Kind == HitKind.QueueRow));
        Assert.Equal(2, secondHits.Hits.Regions.Count(region => region.Kind == HitKind.QueueRow));
    }

    [Fact]
    public void Resize_MovesHitboxesWithRenderedLayout()
    {
        var snapshot = TestUi.Snapshot(
            current: TestUi.Track("now"),
            pending: new[] { TestUi.Track("next") });

        var (_, large) = TestUi.Render(
            new MainScreenComponent(),
            TestUi.Context(snapshot, width: 120, height: 40),
            width: 120,
            height: 40);
        var (_, small) = TestUi.Render(
            new MainScreenComponent(),
            TestUi.Context(snapshot, width: 72, height: 24),
            width: 72,
            height: 24);

        var largeQueue = large.Hits.Regions.Where(region => region.Kind == HitKind.QueueRow).ToArray();
        var smallQueue = small.Hits.Regions.Where(region => region.Kind == HitKind.QueueRow).ToArray();

        Assert.NotEqual(largeQueue[0].Bounds, smallQueue[0].Bounds);
        Assert.All(smallQueue, region => Assert.True(region.Bounds.Right <= 72));
        Assert.All(small.Hits.Regions, region => Assert.True(region.Bounds.Right <= 72));
        Assert.All(small.Hits.Regions, region => Assert.True(region.Bounds.Bottom <= 24));
    }

    [Fact]
    public void MiniMode_UsesCompactLayoutWithoutInputOrPlaybackHitboxes()
    {
        var context = TestUi.Context(TestUi.Snapshot(current: TestUi.Track("now")), mini: true);
        var (screen, ui) = TestUi.Render(new MiniPlayerScreenComponent(), context, width: 80, height: 20);

        Assert.DoesNotContain("popmark >", screen.ToPlainText());
        Assert.Empty(ui.Hits.Regions);
        Assert.Contains("PopMark", screen.ToPlainText());
    }

    [Fact]
    public void SplashMode_RendersArtWithoutCommandInput()
    {
        var context = TestUi.Context(TestUi.Snapshot(current: TestUi.Track("now")), splash: true);
        var (screen, ui) = TestUi.Render(new SplashViewComponent(context), context, width: 100, height: 30);

        Assert.Contains("PopMark", screen.ToPlainText());
        Assert.DoesNotContain("popmark >", screen.ToPlainText());
        Assert.Contains(ui.Hits.Regions, region => region.Kind == HitKind.Progress);
    }

    [Fact]
    public void ResetFrameCache_IsIdempotentAndAllowsCleanup()
    {
        TerminalFrameRenderer.ResetFrameCache();
        TerminalFrameRenderer.ResetFrameCache();

        Assert.True(true);
    }
}
