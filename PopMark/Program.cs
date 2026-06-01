using PopMark.Helpers;
using PopMark.Services;
using Spectre.Console;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        ConsoleHelper.InitializeTerminalCapabilities();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
        };

        var ytDlp = new YtDlpService();
        var mpv = new MpvPlayer();
        var player = new PlaybackQueue(ytDlp, mpv);
        var history = new List<string>();
        var notice = "Ready. Add a YouTube video or playlist URL to start.";

        try
        {
            if (args.Length > 0 && IsLikelyUrl(args[0]))
            {
                await AddUrlAsync(player, args[0]);
                notice = $"Loaded startup URL: {args[0]}";
            }
            else
            {
                ConsoleHelper.ShowStartupSplash();
            }

            var (lastWidth, lastHeight) = ConsoleHelper.GetWindowSize();
            var keepRunning = true;

            while (keepRunning)
            {
                ConsoleHelper.DrawCommandCenter(player.CreateSnapshot(), notice);
                ConsoleHelper.UseBarCursor();

                var input = ConsoleHelper.ReadReactiveInput(ref lastWidth, ref lastHeight, history);
                if (string.IsNullOrWhiteSpace(input))
                {
                    notice = "Type help to list commands.";
                    continue;
                }

                history.Add(input);
                var parsedArgs = ConsoleHelper.SplitArgs(input);
                if (parsedArgs.Length == 0)
                    continue;

                keepRunning = !await ProcessCommandAsync(player, parsedArgs, input);
                notice = player.LastMessage;
            }

            return 0;
        }
        catch (Exception ex)
        {
            ConsoleHelper.Error($"Unexpected error: {ex.Message}");
            return 1;
        }
        finally
        {
            ConsoleHelper.ResetCursorStyle();
        }
    }

    private static async Task<bool> ProcessCommandAsync(PlaybackQueue player, string[] args, string rawInput)
    {
        var command = args[0].ToLowerInvariant();

        switch (command)
        {
            case "help":
            case "h":
            case "?":
                player.LastMessage = "Help is visible in the command center.";
                return false;

            case "add":
            case "a":
            case "load":
                var url = ResolveUrlArgument(args, rawInput);
                if (string.IsNullOrWhiteSpace(url))
                {
                    url = AnsiConsole.Prompt(
                        new TextPrompt<string>("[bold pink1]YouTube URL[/]:")
                            .Validate(value => IsLikelyUrl(value)
                                ? ValidationResult.Success()
                                : ValidationResult.Error("[red]Enter a valid URL.[/]")));
                }

                await AddUrlAsync(player, url);
                return false;

            case "pause":
            case "p":
                await player.PauseAsync();
                return false;

            case "resume":
            case "play":
            case "r":
                await player.ResumeAsync();
                return false;

            case "toggle":
            case "t":
                await player.TogglePauseAsync();
                return false;

            case "next":
            case "skip":
            case "n":
                await player.NextAsync();
                return false;

            case "stop":
            case "s":
                await player.StopAsync(clearQueue: true);
                return false;

            case "queue":
            case "ls":
            case "status":
                player.LastMessage = player.CreateSnapshot().Current is null
                    ? "Nothing is playing."
                    : $"Current track: {player.CreateSnapshot().Current!.Title}";
                return false;

            case "cls":
            case "clear":
                player.LastMessage = "Screen refreshed.";
                return false;

            case "detach":
            case "d":
                await player.DetachAsync();
                return true;

            case "quit":
            case "exit":
            case "q":
                await player.StopAsync(clearQueue: true);
                player.LastMessage = "Stopped playback and exited.";
                return true;

            default:
                if (IsLikelyUrl(rawInput))
                {
                    await AddUrlAsync(player, rawInput);
                    return false;
                }

                player.LastMessage = $"Unknown command: {command}. Type help to list commands.";
                return false;
        }
    }

    private static async Task AddUrlAsync(PlaybackQueue player, string url)
    {
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("pink1"))
            .StartAsync("Loading YouTube metadata with yt-dlp...", async _ =>
            {
                await player.AddUrlAsync(url);
            });
    }

    private static string? ResolveUrlArgument(string[] args, string rawInput)
    {
        if (args.Length >= 2)
            return string.Join(' ', args.Skip(1)).Trim();

        var firstSpace = rawInput.IndexOf(' ');
        return firstSpace >= 0 ? rawInput[(firstSpace + 1)..].Trim() : null;
    }

    private static bool IsLikelyUrl(string value) =>
        Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) &&
        (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
         uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));
}
