using PopMark.Helpers.Terminal;
using PopMark.Helpers.Terminal.Components;
using PopMark.Helpers.Terminal.Rendering;
using PopMark.Models;
using Xunit;

namespace PopMark.Tests;

public sealed class ComponentTests
{
    [Fact]
    public void Spinner_FrameAdvancesWithTimeAndWraps()
    {
        var spinner = new SpinnerComponent { IntervalMilliseconds = 80 };

        Assert.Equal(0, spinner.FrameAt(TimeSpan.Zero));
        Assert.Equal(2, spinner.FrameAt(TimeSpan.FromMilliseconds(160)));
        Assert.Equal(0, spinner.FrameAt(TimeSpan.FromMilliseconds(80 * SpinnerComponent.DefaultFrames.Length)));
    }

    [Fact]
    public void Spinner_StopFreezesFirstFrameAndCancelAliasesStop()
    {
        var spinner = new SpinnerComponent { FrameOverride = 4 };

        spinner.Stop();
        Assert.False(spinner.IsRunning);

        var (screen, _) = TestUi.Render(spinner, TestUi.Context(TestUi.Snapshot()), width: 4, height: 1);
        Assert.Equal(SpinnerComponent.DefaultFrames[0], screen.Get(0, 0).Rune.ToString());

        spinner.Cancel();
        Assert.False(spinner.IsRunning);
    }

    [Fact]
    public void ProgressBar_FillsProportionallyAndCompletes()
    {
        var context = TestUi.Context(TestUi.Snapshot());

        var half = new ProgressBarComponent
        {
            Value = 5,
            Maximum = 10,
            BarWidth = 10,
            FilledGlyph = "#",
            EmptyGlyph = "-"
        };
        var (halfScreen, _) = TestUi.Render(half, context, width: 10, height: 1);
        Assert.Equal("#####-----", halfScreen.ToLines()[0]);

        var complete = new ProgressBarComponent
        {
            Value = 0,
            Maximum = 10,
            BarWidth = 10,
            Completed = true,
            FilledGlyph = "#",
            EmptyGlyph = "-"
        };
        var (completeScreen, _) = TestUi.Render(complete, context, width: 10, height: 1);
        Assert.Equal("##########", completeScreen.ToLines()[0]);
    }

    [Fact]
    public void ProgressBar_RegistersHitboxWithRenderedWidth()
    {
        var context = TestUi.Context(TestUi.Snapshot());
        var progress = new ProgressBarComponent
        {
            Value = 0.25,
            Maximum = 1,
            BarWidth = 20,
            RegistersProgressHitbox = true
        };

        var (_, ui) = TestUi.Render(progress, context, width: 20, height: 1);

        var hit = Assert.Single(ui.Hits.Regions);
        Assert.Equal(HitKind.Progress, hit.Kind);
        Assert.Equal(new UiRect(0, 0, 20, 1), hit.Bounds);
    }

    [Fact]
    public void PlaybackProgress_FormatsDurationAndRatio()
    {
        var track = TestUi.Track("song", durationSeconds: 100);
        var snapshot = TestUi.Snapshot(current: track, elapsed: TimeSpan.FromSeconds(25));

        Assert.Equal(0.25, PlaybackProgressComponent.ProgressRatio(snapshot), 3);
        Assert.Equal("1:40", PlaybackProgressComponent.FormatDuration(track.Duration));
        Assert.Equal("--:--", PlaybackProgressComponent.FormatDuration(null));
        Assert.Equal("1:01:01", PlaybackProgressComponent.FormatDuration(TimeSpan.FromSeconds(3661)));
    }

    [Fact]
    public void VolumeSlider_RendersAndRegistersTrack()
    {
        var context = TestUi.Context(TestUi.Snapshot(volume: 50));
        var slider = new VolumeSliderComponent { Value = 50, TrackWidth = 9 };
        var (screen, ui) = TestUi.Render(slider, context, width: 18, height: 1);

        var hit = Assert.Single(ui.Hits.Regions);
        Assert.Equal(HitKind.Volume, hit.Kind);
        Assert.Equal(9, hit.Bounds.Width);
        Assert.Contains("VOL", screen.ToLines()[0]);
        Assert.Contains("50%", screen.ToLines()[0]);
    }

    [Fact]
    public void Stacks_SkipInvisibleChildrenAndHonorSpacing()
    {
        var hidden = new TextComponent { Text = "HIDDEN", Visible = false };
        var visible = new TextComponent { Text = "VISIBLE" };
        var stack = new VerticalStackComponent { Spacing = 1 };
        stack.Add(visible);
        stack.Add(hidden);
        stack.Add(new TextComponent { Text = "SECOND" });

        var (screen, _) = TestUi.Render(stack, TestUi.Context(TestUi.Snapshot()), width: 20, height: 4);

        Assert.Equal(new[] { "VISIBLE", string.Empty, "SECOND", string.Empty }, screen.ToLines());
    }

    [Fact]
    public void QueueList_BuildsRowsAndHitboxesForEveryTrack()
    {
        var previous = new[] { TestUi.Track("old") };
        var current = TestUi.Track("now");
        var pending = new[] { TestUi.Track("next 1"), TestUi.Track("next 2") };
        var snapshot = TestUi.Snapshot(current: current, previous: previous, pending: pending);
        var context = TestUi.Context(snapshot);
        var queue = new QueueListComponent();

        var (screen, ui) = TestUi.Render(queue, context, width: 40, height: 4);

        Assert.Contains("old", screen.ToLines()[0]);
        Assert.Contains("now", screen.ToLines()[1]);
        Assert.Contains("next 1", screen.ToLines()[2]);
        Assert.Contains("next 2", screen.ToLines()[3]);
        Assert.Equal(4, ui.Hits.Regions.Count(region => region.Kind == HitKind.QueueRow));
        Assert.Equal(new[] { 0, 1, 2, 3 }, ui.Hits.Regions.Select(region => region.Index));
    }

    [Fact]
    public void QueueList_ScrollingKeepsTrackIndexesAligned()
    {
        var pending = Enumerable.Range(1, 10).Select(index => TestUi.Track($"track {index}")).ToArray();
        var snapshot = TestUi.Snapshot(current: TestUi.Track("now"), pending: pending);
        var context = TestUi.Context(snapshot);
        var queue = new QueueListComponent { ScrollOffset = 4 };

        var (screen, ui) = TestUi.Render(queue, context, width: 40, height: 4);

        Assert.Contains("track 4", screen.ToLines()[0]);
        Assert.Equal(new[] { 4, 5, 6 }, ui.Hits.Regions.Select(region => region.Index));
    }

    [Fact]
    public void InputField_ShowsCaretAndScrollsLongText()
    {
        var text = "abcdefghijklmnop";
        var context = TestUi.Context(TestUi.Snapshot(), input: text, caret: text.Length);
        var field = new InputFieldComponent { Text = text, CaretIndex = text.Length };

        var (screen, ui) = TestUi.Render(field, context, width: 20, height: 1);

        Assert.NotNull(ui.Caret);
        Assert.Contains("popmark >", screen.ToLines()[0]);
        Assert.Contains("klmnop", screen.ToLines()[0]);
    }

    [Fact]
    public void Dialog_And_Menu_RenderInsideBounds()
    {
        var context = TestUi.Context(TestUi.Snapshot());
        var dialog = new DialogComponent { Title = "Confirm", Message = "Remove queue?" };

        var (screen, _) = TestUi.Render(dialog, context, width: 60, height: 10);

        Assert.Contains("Confirm", screen.ToLines()[1]);
        Assert.Contains("Remove queue?", screen.ToLines()[3]);

        var menu = new MenuComponent
        {
            Items = new[] { new MenuItem("play", "resume"), new MenuItem("add", "load a url") },
            SelectedIndex = 1
        };
        var (menuScreen, _) = TestUi.Render(menu, context, width: 30, height: 2);
        Assert.Contains("play", menuScreen.ToLines()[0]);
        Assert.Contains("add", menuScreen.ToLines()[1]);
    }

    [Fact]
    public void MainScreen_ExposesAllHitboxesAfterRender()
    {
        var current = TestUi.Track("now", durationSeconds: 200);
        var pending = new[] { TestUi.Track("next") };
        var snapshot = TestUi.Snapshot(current: current, pending: pending, elapsed: TimeSpan.FromSeconds(50));
        var context = TestUi.Context(snapshot);

        var (screen, ui) = TestUi.Render(new MainScreenComponent(), context);

        Assert.Contains(ui.Hits.Regions, region => region.Kind == HitKind.Progress);
        Assert.Contains(ui.Hits.Regions, region => region.Kind == HitKind.Volume);
        Assert.Equal(2, ui.Hits.Regions.Count(region => region.Kind == HitKind.QueueRow));
        Assert.Contains("PopMark", screen.ToLines()[0]);
        Assert.Contains("popmark >", screen.ToLines()[^1]);
    }
}
