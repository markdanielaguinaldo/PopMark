using PopMark.Helpers.Terminal.Components;
using PopMark.Helpers.Terminal.Rendering;

namespace PopMark.Helpers.Terminal;

/// <summary>Builds the component tree for the active view.</summary>
internal static class AppLayout
{
    public static UiComponent Create(RenderContext context)
    {
        if (context.MiniMode)
            return new MiniPlayerScreenComponent();

        if (context.SplashMode)
            return new SplashViewComponent(context);

        return new MainScreenComponent();
    }
}

/// <summary>
/// Playback screen: header, now playing, queue, playback and footer, followed by
/// the command input row.
/// </summary>
internal sealed class MainScreenComponent : UiComponent
{
    public override UiSize Measure(UiSize available) => available;

    public override void Render(ScreenBuffer screen, UiRect bounds, UiRenderContext context)
    {
        if (bounds.IsEmpty)
            return;

        screen.Fill(bounds, context.Theme.Page);

        var metrics = LayoutMetrics.ComputeMain(bounds.Width, bounds.Height);
        var mainHeight = Math.Max(1, bounds.Height - 1);
        var content = new VerticalStackComponent
        {
            PreferredWidth = metrics.ContentWidth,
            PreferredHeight = mainHeight
        };

        content.Add(new HeaderComponent(), metrics.HeaderHeight);
        content.Add(new NowPlayingComponent(), metrics.NowPlayingHeight);
        content.AddFlex(new QueuePanelComponent(), 1);
        content.Add(new PlaybackPanelComponent(), metrics.PlaybackHeight);
        content.Add(new FooterComponent(), metrics.FooterHeight);

        new AlignComponent(content)
        {
            Horizontal = UiHorizontalAlignment.Center,
            Vertical = UiVerticalAlignment.Top
        }.Render(screen, new UiRect(bounds.X, bounds.Y, bounds.Width, mainHeight), context);

        if (bounds.Height >= 1)
        {
            var input = new InputFieldComponent
            {
                Text = context.App.Input,
                CaretIndex = context.App.InputCaret,
                IsFocused = true,
                PreferredWidth = metrics.ContentWidth
            };

            new AlignComponent(input)
            {
                Horizontal = UiHorizontalAlignment.Center,
                Vertical = UiVerticalAlignment.Top
            }.Render(screen, new UiRect(bounds.X, bounds.Bottom - 1, bounds.Width, 1), context);
        }
    }
}
