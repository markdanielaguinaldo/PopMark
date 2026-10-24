using PopMark.Helpers.Terminal.Components;
using PopMark.Helpers.Terminal.Rendering;
using PopMark.Models;
using System.Reflection;

namespace PopMark.Helpers.Terminal;

internal static class SplashScreen
{
    public static string CurrentVersion =>
        typeof(SplashScreen).Assembly
            .GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), inherit: false)
            .OfType<AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()
            ?.InformationalVersion ??
        typeof(SplashScreen).Assembly.GetName().Version?.ToString() ??
        "unknown";
}

/// <summary>Full-screen splash view wrapped in the existing accent border.</summary>
internal sealed class SplashViewComponent : UiComponent
{
    private readonly RenderContext _app;
    private readonly PlayerSnapshot? _snapshot;

    public SplashViewComponent(RenderContext app, bool playbackMode = true)
    {
        _app = app;
        _snapshot = playbackMode ? app.Snapshot : null;
    }

    public override UiSize Measure(UiSize available) => available;

    public override void Render(ScreenBuffer screen, UiRect bounds, UiRenderContext context)
    {
        if (bounds.IsEmpty)
            return;

        screen.Fill(bounds, context.Theme.SplashBackground);

        if (bounds.Width < 2 || bounds.Height < 2)
            return;

        screen.DrawBox(bounds, UiBorderStyle.Rounded, context.Theme.BorderAccent);

        var content = bounds.Inset(1, 1, 1, 1);
        if (content.IsEmpty)
            return;

        var cells = SplashCanvas.RenderCells(
            _app.AnimationFrame,
            SplashScreen.CurrentVersion,
            content.Width,
            content.Height,
            _snapshot,
            context.Theme);

        for (var row = 0; row < content.Height; row++)
        {
            for (var column = 0; column < content.Width; column++)
            {
                var cell = cells[row, column];
                screen.Set(content.X + column, content.Y + row, new System.Text.Rune(cell.Character), cell.Style);
            }
        }

        if (_snapshot is null)
            return;

        var hitboxes = SplashCanvas.ResolveHitboxes(content.Width, content.Height, _snapshot);
        if (hitboxes.Progress is { } progress)
            context.Hits.Add(HitKind.Progress, new UiRect(content.X + progress.X, content.Y + progress.Y, progress.Width, 1));
        if (hitboxes.Volume is { } volume)
            context.Hits.Add(HitKind.Volume, new UiRect(content.X + volume.X, content.Y + volume.Y, volume.Width, 1));
    }
}

internal static class SplashCanvas
{
    private const int VolumeSliderColumn = 3;
    private const int VolumeSliderTrackOffset = 4;
    private const int VolumeSliderTrackWidth = 13;

    public static IReadOnlyList<string> RenderRows(
        int frame,
        string version,
        int width,
        int height,
        PlayerSnapshot? snapshot,
        UiTheme theme) =>
        Enumerable.Range(0, height)
            .Select(row => BuildPlainRow(RenderCells(frame, version, width, height, snapshot, theme), row))
            .ToArray();

    public static SplashCell[,] RenderCells(
        int frame,
        string version,
        int width,
        int height,
        PlayerSnapshot? snapshot,
        UiTheme theme)
    {
        var cells = CreateCells(width, height, theme);
        AddAmbientFrame(cells, frame, version, snapshot, theme);
        AddHero(cells, frame, version, snapshot, theme);
        return cells;
    }

    public static SplashHitboxes ResolveHitboxes(int width, int height, PlayerSnapshot snapshot)
    {
        if (width < 3 || height < 3)
            return new SplashHitboxes(null, null);

        var layout = ResolveHeroLayout(frame: 0, width, height, playbackMode: true, snapshot);
        var progressLine = layout.Art
            .Select((line, row) => new { Line = line, Row = row })
            .FirstOrDefault(candidate => AudioBeaconLogo.TryResolveProgressRange(candidate.Line, out _, out _));
        SplashHitbox? progress = null;

        if (progressLine is not null &&
            AudioBeaconLogo.TryResolveProgressRange(progressLine.Line, out var start, out var progressWidth))
        {
            progress = new SplashHitbox(
                X: layout.Column + start,
                Y: layout.Top + progressLine.Row,
                Width: progressWidth);
        }

        var volume = height >= 18
            ? new SplashHitbox(
                X: VolumeSliderColumn + VolumeSliderTrackOffset,
                Y: height - 2,
                Width: VolumeSliderTrackWidth)
            : null;

        return new SplashHitboxes(progress, volume);
    }

    private static SplashCell[,] CreateCells(int width, int height, UiTheme theme)
    {
        var cells = new SplashCell[height, width];
        for (var row = 0; row < height; row++)
        {
            for (var column = 0; column < width; column++)
                cells[row, column] = new SplashCell(' ', theme.SplashBackground);
        }

        return cells;
    }

    private static void AddAmbientFrame(
        SplashCell[,] cells,
        int frame,
        string version,
        PlayerSnapshot? snapshot,
        UiTheme theme)
    {
        var height = cells.GetLength(0);
        var width = cells.GetLength(1);
        var pulse = frame % 4 < 2 ? theme.SplashPulseDim : theme.SplashPulseBright;

        WriteRight(cells, 1, 3, $"v{version}", theme.SplashMuted);
        WriteRight(cells, 2, 3, "YT Music", theme.SplashPulseBright);

        if (height >= 18)
        {
            AddVolumeSlider(cells, height - 2, VolumeSliderColumn, snapshot, theme);
            AddBottomRightDots(cells, frame, theme);
            AddControlLegend(cells, height - 2, theme);
        }

        AddMiddleDecorations(cells, frame, snapshot, theme);
        AddParticles(cells, frame, pulse);
    }

    private static void AddMiddleDecorations(SplashCell[,] cells, int frame, PlayerSnapshot? snapshot, UiTheme theme)
    {
        var height = cells.GetLength(0);
        var width = cells.GetLength(1);
        if (width < 56 || height < 16)
            return;

        AddRightOwl(cells, Math.Max(4, height / 2 - 2), Math.Max(0, width - 12), frame, snapshot, theme);
    }

    private static void AddRightOwl(
        SplashCell[,] cells,
        int startRow,
        int column,
        int frame,
        PlayerSnapshot? snapshot,
        UiTheme theme)
    {
        var eyes = SplashOwlEyesForFrame(frame, snapshot);
        var art = new[]
        {
            " ^...^ ",
            $"/ {eyes} \\",
            "|):::(|",
            "--w-w--"
        };

        for (var row = 0; row < art.Length; row++)
        {
            for (var index = 0; index < art[row].Length; index++)
                PutAnimated(cells, startRow + row, column + index, art[row][index], SplashOwlStyle(row, art[row][index], theme));
        }
    }

    private static CellStyle SplashOwlStyle(int row, char character, UiTheme theme) =>
        row switch
        {
            1 when character is 'o' or '-' or ',' => theme.SplashWhite,
            2 when character is ')' or '(' or ':' => theme.SplashPulseBright,
            3 when character is 'w' or '-' => theme.SplashPulseBright,
            _ => theme.SplashMuted
        };

    private static string SplashOwlEyesForFrame(int frame, PlayerSnapshot? snapshot)
    {
        if (snapshot?.Status != PlaybackStatus.Playing)
            return "-,-";

        var cycle = frame % 120;
        if (cycle is >= 88 and <= 112)
            return "z,z";

        if (cycle is 21 or 22 or 45 or 46 or 69 or 70)
            return "-,-";

        return "o,o";
    }

    private static void AddBottomRightDots(SplashCell[,] cells, int frame, UiTheme theme)
    {
        var height = cells.GetLength(0);
        var width = cells.GetLength(1);
        if (width < 48)
            return;

        var dots = (frame % 4) switch
        {
            0 => " ·   · ",
            1 => "  · ·  ",
            2 => "   ·   ",
            _ => "  · ·  "
        };

        WriteRight(cells, height - 2, 5, dots, theme.Accent);
    }

    private static void AddParticles(SplashCell[,] cells, int frame, CellStyle pulse)
    {
        var height = cells.GetLength(0);
        var width = cells.GetLength(1);
        if (width < 48 || height < 16)
            return;

        var particles = new (int X, int Y, char[] Marks)[]
        {
            (20, 25, new[] { '.', '·', ' ' }),
            (78, 24, new[] { '*', '.', ' ' }),
            (13, 60, new[] { '·', ' ', '.' }),
            (84, 64, new[] { '.', '*', ' ' }),
            (32, 77, new[] { '·', '.', ' ' }),
            (68, 78, new[] { '*', ' ', '.' })
        };

        foreach (var (x, y, marks) in particles)
        {
            var row = Math.Clamp(height * y / 100, 3, Math.Max(3, height - 4));
            var column = Math.Clamp(width * x / 100, 1, Math.Max(1, width - 2));
            var mark = marks[(frame / 2 + row + column) % marks.Length];
            if (mark != ' ')
                PutAnimated(cells, row, column, mark, pulse);
        }
    }

    private static void AddHero(
        SplashCell[,] cells,
        int frame,
        string version,
        PlayerSnapshot? snapshot,
        UiTheme theme)
    {
        var height = cells.GetLength(0);
        var width = cells.GetLength(1);
        var playbackMode = snapshot is not null;
        var layout = ResolveHeroLayout(frame, width, height, playbackMode, snapshot);
        var art = layout.Art;
        var compact = height < 18;

        for (var index = 0; index < art.Count && layout.Top + index < height; index++)
        {
            var line = art[index].PadRight(layout.ArtWidth);
            WriteTape(cells, layout.Top + index, layout.Column, line, frame, index, theme);
        }

        var titleRow = Math.Min(height - 1, layout.Top + art.Count + 1);
        var subtitleRow = Math.Min(height - 1, titleRow + 1);
        var readyRow = Math.Min(height - 1, subtitleRow + (compact ? 1 : 2));
        var pulse = frame % 4 < 2 ? theme.SplashWhite : theme.SplashPulseBright;

        WriteCentered(cells, titleRow, "PopMark", theme.SplashPulseBright);
        if (playbackMode)
        {
            var track = snapshot!.Current ?? snapshot.Pending.FirstOrDefault();
            var trackText = TerminalText.TrimForWidget(track?.Title ?? "Queue is empty", Math.Max(12, width - 8));
            var waveWidth = Math.Clamp(width - 12, 12, 54);
            var wave = SplashWave(snapshot.Status, frame, waveWidth);
            var detail = PlaybackDetail(snapshot, track);

            WriteCentered(cells, subtitleRow, trackText, theme.SplashWhite);
            WriteCentered(cells, Math.Min(height - 1, subtitleRow + 1), wave, theme.SplashPulseBright);
            WriteCentered(cells, Math.Min(height - 1, subtitleRow + 2), detail, theme.SplashMuted);
            return;
        }

        if (!compact)
            WriteCentered(cells, subtitleRow, $"Terminal Music Player v{version}", theme.SplashMuted);
        WriteCentered(cells, readyRow, "Ready • Press any key", pulse);
    }

    private static SplashHeroLayout ResolveHeroLayout(
        int frame,
        int width,
        int height,
        bool playbackMode,
        PlayerSnapshot? snapshot)
    {
        var art = AudioBeaconLogo.Lines(frame, playbackMode ? snapshot : null, width);
        var artWidth = art.Max(line => line.Length);
        var compact = height < 18;
        var groupHeight = art.Count + (playbackMode ? 5 : compact ? 2 : 4);
        var top = Math.Max(1, (height - groupHeight) / 2);
        var column = Math.Max(0, (width - artWidth) / 2);
        return new SplashHeroLayout(art, artWidth, top, column);
    }

    private static string SplashWave(PlaybackStatus status, int frame, int width)
    {
        string[] glyphs = ["⣀", "⣄", "⣆", "⣇", "⣧", "⣷", "⣿"];
        if (status is not (PlaybackStatus.Playing or PlaybackStatus.Loading))
            return new string('⣀', width);

        return string.Concat(Enumerable.Range(0, width).Select(index =>
        {
            var phase = (frame + index * 2) % 14;
            var level = phase <= 7 ? phase - 1 : 13 - phase;
            return glyphs[Math.Clamp(level, 0, glyphs.Length - 1)];
        }));
    }

    private static string PlaybackDetail(PlayerSnapshot snapshot, Track? track)
    {
        var time = $"{FormatDuration(snapshot.Elapsed)} / {FormatDuration(track?.Duration)}";
        var status = snapshot.Status is PlaybackStatus.Playing ? "Playing" : snapshot.Status.ToString();
        return $"{status} • {time} • Tab playlist";
    }

    private static string FormatDuration(TimeSpan? duration)
    {
        if (duration is null)
            return "--:--";

        return duration.Value.TotalHours >= 1
            ? duration.Value.ToString(@"h\:mm\:ss")
            : duration.Value.ToString(@"m\:ss");
    }

    private static void WriteTape(
        SplashCell[,] cells,
        int row,
        int column,
        string text,
        int frame,
        int tapeRow,
        UiTheme theme)
    {
        for (var i = 0; i < text.Length; i++)
        {
            var character = text[i];
            var style = AudioBeaconLogo.StyleFor(frame, tapeRow, i, character, text, theme);
            PutAnimated(cells, row, column + i, character, style);
        }
    }

    private static void AddControlLegend(SplashCell[,] cells, int row, UiTheme theme)
    {
        var width = cells.GetLength(1);
        var volumeEndColumn = VolumeSliderColumn + "VOL ".Length + VolumeSliderTrackWidth + 1 + 4;
        var minimumColumn = Math.Min(width, volumeEndColumn + 3);
        string[] variants =
        [
            "SPACE Play/Pause   [/] prev/next   TAB Playlist   Q Quit",
            "SPACE Play   [/] prev/next   TAB View   Q Quit",
            "SPACE Play   TAB View   Q Quit"
        ];

        var controls = variants.FirstOrDefault(candidate =>
        {
            var length = TerminalText.VisibleLength(candidate);
            return length <= width &&
                   Math.Max(0, (width - length) / 2) >= minimumColumn;
        }) ?? variants[^1];

        var column = Math.Max(minimumColumn, (width - TerminalText.VisibleLength(controls)) / 2);
        if (column + TerminalText.VisibleLength(controls) > width)
            column = Math.Max(0, width - TerminalText.VisibleLength(controls));

        WriteControlLegend(cells, row, column, controls, theme);
    }

    private static void WriteControlLegend(
        SplashCell[,] cells,
        int row,
        int column,
        string controls,
        UiTheme theme)
    {
        for (var index = 0; index < controls.Length; index++)
            Put(cells, row, column + index, controls[index], ControlLegendStyle(controls, index, theme));
    }

    private static CellStyle ControlLegendStyle(string controls, int index, UiTheme theme)
    {
        if (char.IsWhiteSpace(controls[index]))
            return theme.SplashBackground;

        if (TokenCoversIndex(controls, index, "SPACE") ||
            TokenCoversIndex(controls, index, "TAB") ||
            TokenCoversIndex(controls, index, "Q"))
        {
            return theme.SplashPulseBright;
        }

        if (controls[index] is '[' or '/' or ']')
            return theme.SplashPulseBright;

        return theme.SplashWhite;
    }

    private static bool TokenCoversIndex(string text, int index, string token)
    {
        var firstPossibleStart = Math.Max(0, index - token.Length + 1);
        var lastPossibleStart = Math.Min(index, text.Length - token.Length);

        for (var start = firstPossibleStart; start <= lastPossibleStart; start++)
        {
            var startsAtTokenBoundary = start == 0 || char.IsWhiteSpace(text[start - 1]);
            var endsAtTokenBoundary = start + token.Length == text.Length || char.IsWhiteSpace(text[start + token.Length]);
            if (startsAtTokenBoundary &&
                endsAtTokenBoundary &&
                text.AsSpan(start, token.Length).SequenceEqual(token))
            {
                return true;
            }
        }

        return false;
    }

    private static void AddVolumeSlider(
        SplashCell[,] cells,
        int row,
        int column,
        PlayerSnapshot? snapshot,
        UiTheme theme)
    {
        var volume = Math.Clamp(snapshot?.VolumePercent ?? 100, 0, 200);
        const int trackWidth = VolumeSliderTrackWidth;
        var knobPosition = Math.Clamp((int)Math.Round(Math.Min(volume, 100) / 100d * (trackWidth - 1)), 0, trackWidth - 1);
        var activeStyle = volume > 100 ? theme.Boost : theme.SplashPulseBright;
        const string label = "VOL ";
        Write(cells, row, column, label, theme.SplashMuted);

        for (var index = 0; index < trackWidth; index++)
        {
            var character = index == knobPosition ? '●' : '━';
            var style = index <= knobPosition ? activeStyle : theme.SplashFaint;
            Put(cells, row, column + label.Length + index, character, style);
        }

        Write(cells, row, column + label.Length + trackWidth + 1, $"{volume}%", activeStyle);
    }

    private static void WriteCentered(SplashCell[,] cells, int row, string text, CellStyle style) =>
        Write(cells, row, Math.Max(0, (cells.GetLength(1) - TerminalText.VisibleLength(text)) / 2), text, style);

    private static void WriteRight(SplashCell[,] cells, int row, int margin, string text, CellStyle style) =>
        Write(cells, row, Math.Max(0, cells.GetLength(1) - margin - TerminalText.VisibleLength(text)), text, style);

    private static void Write(SplashCell[,] cells, int row, int column, string text, CellStyle style)
    {
        for (var index = 0; index < text.Length; index++)
            Put(cells, row, column + index, text[index], style);
    }

    private static void Put(SplashCell[,] cells, int row, int column, char character, CellStyle style)
    {
        if (row < 0 ||
            row >= cells.GetLength(0) ||
            column < 0 ||
            column >= cells.GetLength(1))
        {
            return;
        }

        var background = cells[row, column].Style.Background;
        cells[row, column] = new SplashCell(character, style with { Background = style.Background ?? background });
    }

    private static void PutAnimated(SplashCell[,] cells, int row, int column, char character, CellStyle style)
    {
        if (!IsInsideContentArea(cells, row, column))
            return;

        Put(cells, row, column, character, style);
    }

    private static bool IsInsideContentArea(SplashCell[,] cells, int row, int column) =>
        row >= 0 &&
        row < cells.GetLength(0) &&
        column >= 0 &&
        column < cells.GetLength(1);

    private static string BuildPlainRow(SplashCell[,] cells, int row)
    {
        var width = cells.GetLength(1);
        var builder = new System.Text.StringBuilder(width);
        for (var column = 0; column < width; column++)
            builder.Append(cells[row, column].Character);
        return builder.ToString();
    }

    internal sealed record SplashCell(char Character, CellStyle Style);

    private sealed record SplashHeroLayout(IReadOnlyList<string> Art, int ArtWidth, int Top, int Column);
}

internal sealed record SplashHitboxes(SplashHitbox? Progress, SplashHitbox? Volume);

internal sealed record SplashHitbox(int X, int Y, int Width);

internal static class AudioBeaconLogo
{
    private static readonly string[] LargeTapeDeck =
    [
        "         _____________ _I-I__ __",
        "        /       ____  \"-|_|-.\\  \\",
        "       /  __,--'    `--._ _  \\\\  \\",
        "      /,-'      ,-\"-.      `-.\\\\  \\",
        "     /(        (  ^  )       ) \\\\  \\",
        "    /  `m.__    `-.-'   __.m'  _))  \\",
        "   / ____`\"\"mm.______,mm\"\"'   /_/-'  \\",
        "  / /___/ === `\"\"\"\"\"\"'    II  `w      \\",
        " /_____________________________________\\",
        "|_______________________________________|"
    ];

    private static readonly string[] CompactTapeDeck =
    [
        "      __________ _I-I_",
        "     /     ___  \"|_|.\\",
        "    / __,-'   `-._ \\\\",
        "   /,'    ,-\"-.   `\\\\",
        "  /(     ( ^ )    )\\",
        " / `m.__  `-' __.m' _))\\",
        " /___ === `\"\"\"\"'  II `w \\",
        "/________________________\\",
        "|________________________|"
    ];

    private static readonly string[] NarrowTapeDeck =
    [
        "   _________",
        "  /  _I-I_  \\",
        " /  ( ^ )  \\",
        "|  === II  |",
        "|___________|"
    ];

    public static IReadOnlyList<string> Lines(int frame, PlayerSnapshot? snapshot, int availableWidth)
    {
        var large = BuildArt(NormalizeArt(LargeTapeDeck), frame, snapshot);
        if (availableWidth >= large[0].Length)
            return large;

        var compact = BuildArt(NormalizeArt(CompactTapeDeck), frame, snapshot);
        if (availableWidth >= compact[0].Length)
            return compact;

        return BuildArt(NormalizeArt(NarrowTapeDeck), frame, snapshot);
    }

    public static bool TryResolveProgressRange(string line, out int start, out int width)
    {
        start = 0;
        width = 0;
        if (!line.StartsWith('|') || line.LastIndexOf('|') <= 0)
            return false;

        start = 1;
        width = line.LastIndexOf('|') - start;
        return width > 0;
    }

    public static CellStyle StyleFor(
        int frame,
        int row,
        int column,
        char character,
        string line,
        UiTheme theme) =>
        TapeAccentStyle(frame, row, column, character, line, theme) ?? BaseTapeStyle(row, theme);

    private static CellStyle BaseTapeStyle(int row, UiTheme theme) =>
        row switch
        {
            < 3 => theme.SplashPulseBright,
            < 8 => theme.Accent,
            _ => theme.SplashFaint
        };

    private static CellStyle? TapeAccentStyle(
        int frame,
        int row,
        int column,
        char character,
        string line,
        UiTheme theme)
    {
        if (character is '◐' or '◓' or '◑' or '◒')
            return theme.SplashWhite;

        if (line.StartsWith('|') &&
            line.LastIndexOf('|') > 0 &&
            character is '=' or '◉')
            return theme.Accent;

        if (line.StartsWith('|') &&
            line.LastIndexOf('|') > 0 &&
            character == '-')
            return theme.SplashFaint;

        if (character is '=' or 'I' or '|')
            return frame % 2 == 0 ? theme.Accent : theme.SplashPulseBright;

        if (IsBaseLineScan(column, character, line, frame))
            return theme.SplashWhite;

        if (character is '_' or '/' or '\\')
            return (row + column) % 5 == 0 ? theme.SplashPulseBright : null;

        return null;
    }

    private static bool IsBaseLineScan(int column, char character, string line, int frame)
    {
        if (character != '_' ||
            !line.StartsWith('|') ||
            line.LastIndexOf('|') <= 0)
        {
            return false;
        }

        var start = line.IndexOf('_');
        var end = line.LastIndexOf('_');
        if (start < 0 || end < start)
            return false;

        const int segmentWidth = 4;
        var availableWidth = end - start + 1;
        var maxPosition = Math.Max(0, availableWidth - segmentWidth);
        var progressPosition = maxPosition == 0 ? 0 : frame % (maxPosition + 1);
        var scanStart = start + progressPosition;
        var scanEnd = Math.Min(end, scanStart + segmentWidth - 1);
        return column >= scanStart && column <= scanEnd;
    }

    private static string[] BuildArt(IReadOnlyList<string> art, int frame, PlayerSnapshot? snapshot)
    {
        var animated = AnimateReel(art, frame);
        return snapshot is null
            ? animated
            : ApplyProgress(animated, snapshot);
    }

    private static string[] AnimateReel(IReadOnlyList<string> art, int frame)
    {
        var reel = (frame % 4) switch
        {
            0 => '◐',
            1 => '◓',
            2 => '◑',
            _ => '◒'
        };

        return art.Select(line => line.Replace('^', reel)).ToArray();
    }

    private static string[] ApplyProgress(IReadOnlyList<string> art, PlayerSnapshot snapshot)
    {
        var hasProgress = snapshot.Status is PlaybackStatus.Playing or PlaybackStatus.Paused or PlaybackStatus.Loading &&
                          snapshot.Current?.Duration is { TotalSeconds: > 0 } &&
                          snapshot.Elapsed is not null;
        var ratio = hasProgress ? ProgressRatio(snapshot) : 0;
        return art.Select(line =>
        {
            if (!TryResolveProgressRange(line, out var start, out var width))
                return line;

            var markerPosition = Math.Clamp((int)Math.Round(ratio * Math.Max(0, width - 1)), 0, Math.Max(0, width - 1));
            var filled = Math.Clamp((int)Math.Round(ratio * width), 0, width);
            var chars = line.ToCharArray();
            for (var index = 0; index < width; index++)
                chars[start + index] = index < filled ? '=' : '-';

            if (hasProgress && width > 0)
                chars[start + markerPosition] = '◉';

            return new string(chars);
        }).ToArray();
    }

    private static double ProgressRatio(PlayerSnapshot snapshot)
    {
        if (snapshot.Current?.Duration is not { TotalSeconds: > 0 } duration ||
            snapshot.Elapsed is not { } elapsed)
        {
            return 0;
        }

        return Math.Clamp(elapsed.TotalSeconds / duration.TotalSeconds, 0, 1);
    }

    private static string[] NormalizeArt(IReadOnlyList<string> art)
    {
        var width = art.Max(line => line.Length);
        return art.Select(line => line.PadRight(width)).ToArray();
    }
}
