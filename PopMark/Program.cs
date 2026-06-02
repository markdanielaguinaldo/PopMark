using PopMark.Helpers;
using PopMark.Models;
using PopMark.Services;
using Spectre.Console;

internal static class Program
{
    private const int ArrowSeekBaseSeconds = 10;

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
        var dependencies = new DependencyInstaller();
        var history = new List<string>();
        var notice = "Ready. Add a YouTube video or playlist URL to start.";
        var miniMode = false;
        var showHelp = false;
        var queueScrollOffset = 0;
        player.LastMessage = notice;
        player.SnapshotChanged += QueueCacheStore.Save;

        try
        {
            var clearedSessions = PlaybackSessionStore.CleanupStaleSessions();
            if (clearedSessions > 0)
            {
                notice = $"Cleared {clearedSessions} previous PopMark playback session(s).";
                player.LastMessage = notice;
            }

            if (args.Length > 0 && args[0].Equals("deps", StringComparison.OrdinalIgnoreCase))
                return await RunDependencyInstallAsync(dependencies);

            if (TryParseNonInteractiveOptions(args, out var options))
                return await RunNonInteractiveAsync(player, dependencies, options);

            var hasStartupUrl = args.Length > 0 && IsLikelyUrl(args[0]);

            if (hasStartupUrl)
            {
                try
                {
                    await AddUrlAsync(player, dependencies, args[0], promptToInstallDependencies: true, confirmInstallDependencies: true, showStatus: true);
                    notice = player.LastMessage;
                }
                catch (Exception ex)
                {
                    notice = ex.Message;
                    player.LastMessage = ex.Message;
                }
            }
            else
            {
                ConsoleHelper.ShowStartupSplash();

                var cachedQueue = QueueCacheStore.Load();
                if (cachedQueue.Current is not null || cachedQueue.Pending.Count > 0)
                {
                    player.RestoreQueue(cachedQueue);
                    notice = player.LastMessage;
                }
            }

            var (lastWidth, lastHeight) = ConsoleHelper.GetWindowSize();
            var keepRunning = true;
            ConsoleHelper.EnterInteractiveScreen();

            while (keepRunning)
            {
                if (miniMode)
                    ConsoleHelper.DrawMiniPlayer(player.CreateSnapshot(), player.LastMessage);
                else
                    ConsoleHelper.DrawCommandCenter(player.CreateSnapshot(), player.LastMessage, showHelp, queueScrollOffset);
                ConsoleHelper.UseBarCursor();

                var input = ConsoleHelper.ReadReactiveInput(
                    ref lastWidth,
                    ref lastHeight,
                    history,
                    () => player.CreateSnapshot(),
                    () => player.LastMessage,
                    () => miniMode,
                    () => showHelp,
                    () => queueScrollOffset);
                ConsoleHelper.ShowCursor();

                if (string.IsNullOrWhiteSpace(input))
                {
                    notice = "Type help to list commands.";
                    player.LastMessage = notice;
                    continue;
                }

                if (!IsInternalCommand(input))
                    history.Add(input);
                var parsedArgs = ConsoleHelper.SplitArgs(input);
                if (parsedArgs.Length == 0)
                    continue;

                try
                {
                    if (TryResolveProgressClickCommand(parsedArgs[0], player.CreateSnapshot(), out var timestamp))
                    {
                        await player.SeekAbsoluteAsync(timestamp);
                        notice = player.LastMessage;
                        (lastWidth, lastHeight) = ConsoleHelper.GetWindowSize();
                        continue;
                    }

                    if (TryProcessQueueScrollCommand(parsedArgs[0], player.CreateSnapshot(), ref queueScrollOffset, out var scrollMessage))
                    {
                        player.LastMessage = scrollMessage;
                        notice = scrollMessage;
                        (lastWidth, lastHeight) = ConsoleHelper.GetWindowSize();
                        continue;
                    }

                    if (IsInternalCommand(parsedArgs[0]))
                    {
                        (lastWidth, lastHeight) = ConsoleHelper.GetWindowSize();
                        continue;
                    }

                    showHelp = parsedArgs[0].Equals("help", StringComparison.OrdinalIgnoreCase) ||
                               parsedArgs[0].Equals("h", StringComparison.OrdinalIgnoreCase) ||
                               parsedArgs[0].Equals("?", StringComparison.OrdinalIgnoreCase);
                    if (showHelp && miniMode)
                    {
                        miniMode = false;
                    }

                    keepRunning = !await ProcessCommandAsync(
                        player,
                        dependencies,
                        parsedArgs,
                        input,
                        () =>
                        {
                            miniMode = !miniMode;
                            player.LastMessage = miniMode ? "Compact view enabled." : "Full player view enabled.";
                        });
                    notice = player.LastMessage;
                    (lastWidth, lastHeight) = ConsoleHelper.GetWindowSize();
                }
                catch (Exception ex)
                {
                    player.LastMessage = ex.Message;
                    notice = ex.Message;
                }
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
            ConsoleHelper.LeaveInteractiveScreen();
        }
    }

    private static async Task<bool> ProcessCommandAsync(
        PlaybackQueue player,
        DependencyInstaller dependencies,
        string[] args,
        string rawInput,
        Action toggleMiniMode)
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
                        new TextPrompt<string>("[bold deepskyblue1]YouTube URL[/]:")
                            .Validate(value => IsLikelyUrl(value)
                                ? ValidationResult.Success()
                                : ValidationResult.Error("[cyan1]Enter a valid URL.[/]")));
                }

                await AddUrlAsync(player, dependencies, url, promptToInstallDependencies: true, confirmInstallDependencies: true, showStatus: true);
                return false;

            case "deps":
            case "dependencies":
            case "install-deps":
                player.LastMessage = await dependencies.EnsurePlaybackDependenciesAsync(promptToInstall: true, confirmInstall: true);
                return false;

            case "play":
            case "pause":
            case "r":
                await player.TogglePauseAsync();
                return false;

            case "next":
            case "skip":
            case "n":
                await player.NextAsync();
                return false;

            case "previous":
            case "prev":
            case "back":
                await player.PreviousAsync();
                return false;

            case "__seek-forward":
            case "__seek-back":
                await player.SeekRelativeAsync(ResolveArrowSeekSeconds(command));
                return false;

            case "stop":
            case "s":
                await player.StopAsync(clearQueue: true);
                return false;

            case "clear-playlist":
            case "clearplaylist":
            case "clear-queue":
            case "clearqueue":
                await player.ClearPlaylistAsync();
                return false;

            case "queue":
            case "ls":
            case "status":
                if (args.Length > 1 && args[1].Equals("clear", StringComparison.OrdinalIgnoreCase))
                {
                    await player.ClearPlaylistAsync();
                    return false;
                }

                player.LastMessage = player.CreateSnapshot().Current is null
                    ? "Nothing is playing."
                    : $"Current track: {player.CreateSnapshot().Current!.Title}";
                return false;

            case "cls":
            case "clear":
                if (args.Length > 1 &&
                    (args[1].Equals("playlist", StringComparison.OrdinalIgnoreCase) ||
                     args[1].Equals("queue", StringComparison.OrdinalIgnoreCase)))
                {
                    await player.ClearPlaylistAsync();
                    return false;
                }

                player.LastMessage = "Screen refreshed.";
                return false;

            case "mini":
            case "m":
                toggleMiniMode();
                return false;

            case "quit":
            case "exit":
            case "q":
                await player.StopPlaybackAndKeepQueueAsync();
                return true;

            default:
                if (IsLikelyUrl(rawInput))
                {
                    await AddUrlAsync(player, dependencies, rawInput, promptToInstallDependencies: true, confirmInstallDependencies: true, showStatus: true);
                    return false;
                }

                player.LastMessage = $"Unknown command: {command}. Type help to list commands.";
                return false;
        }
    }

    private static async Task<int> RunNonInteractiveAsync(
        PlaybackQueue player,
        DependencyInstaller dependencies,
        NonInteractiveOptions options)
    {
        try
        {
            ConsoleHelper.Info($"Loading URL: {options.Url}");
            await AddUrlAsync(
                player,
                dependencies,
                options.Url,
                promptToInstallDependencies: options.InstallDependencies,
                confirmInstallDependencies: false,
                showStatus: false);

            if (!dependencies.ArePlaybackDependenciesAvailable())
            {
                ConsoleHelper.Error(player.LastMessage);
                return 2;
            }

            var snapshot = player.CreateSnapshot();
            if (snapshot.Current is null)
            {
                ConsoleHelper.Error(player.LastMessage);
                return 1;
            }

            ConsoleHelper.Success($"Now playing: {snapshot.Current.Title}");
            ConsoleHelper.Info($"Keeping playback alive for {options.Seconds} second(s).");
            await Task.Delay(TimeSpan.FromSeconds(options.Seconds));

            await player.StopAsync(clearQueue: true);
            ConsoleHelper.Success("Playback test finished.");
            return 0;
        }
        catch (Exception ex)
        {
            ConsoleHelper.Error(ex.Message);
            return 1;
        }
    }

    private static async Task AddUrlAsync(
        PlaybackQueue player,
        DependencyInstaller dependencies,
        string url,
        bool promptToInstallDependencies,
        bool confirmInstallDependencies,
        bool showStatus)
    {
        if (showStatus && dependencies.ArePlaybackDependenciesAvailable())
        {
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.SquareCorners)
                .SpinnerStyle(Style.Parse("deepskyblue1"))
                .StartAsync("Adding URL to queue...", async ctx =>
                {
                    ctx.Status("Checking playback tools...");
                    var dependencyMessage = await dependencies.EnsurePlaybackDependenciesAsync(
                        promptToInstall: promptToInstallDependencies,
                        confirmInstall: confirmInstallDependencies);
                    if (!dependencies.ArePlaybackDependenciesAvailable())
                    {
                        player.LastMessage = dependencyMessage;
                        return;
                    }

                    ctx.Status("Loading YouTube metadata with yt-dlp...");
                    await player.AddUrlAsync(url);
                });
            return;
        }

        var dependencyMessage = await dependencies.EnsurePlaybackDependenciesAsync(
            promptToInstall: promptToInstallDependencies,
            confirmInstall: confirmInstallDependencies);
        if (!dependencies.ArePlaybackDependenciesAvailable())
        {
            player.LastMessage = dependencyMessage;
            return;
        }

        if (showStatus)
        {
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.SquareCorners)
                .SpinnerStyle(Style.Parse("deepskyblue1"))
                .StartAsync("Loading YouTube metadata with yt-dlp...", async ctx =>
                {
                    ctx.Status("Adding URL to queue...");
                    await player.AddUrlAsync(url);
                });
            return;
        }

        await player.AddUrlAsync(url);
    }

    private static async Task<int> RunDependencyInstallAsync(DependencyInstaller dependencies)
    {
        try
        {
            var message = await dependencies.EnsurePlaybackDependenciesAsync(promptToInstall: true, confirmInstall: false);
            if (!dependencies.ArePlaybackDependenciesAvailable())
            {
                ConsoleHelper.Error(message);
                return 2;
            }

            ConsoleHelper.Success(message);
            return 0;
        }
        catch (Exception ex)
        {
            ConsoleHelper.Error(ex.Message);
            return 1;
        }
    }

    private static bool TryParseNonInteractiveOptions(string[] args, out NonInteractiveOptions options)
    {
        options = new NonInteractiveOptions(string.Empty, Seconds: 15, InstallDependencies: false);
        if (args.Length == 0)
            return false;

        var command = args[0].ToLowerInvariant();
        var nonInteractive = args.Any(arg => arg.Equals("--non-interactive", StringComparison.OrdinalIgnoreCase) ||
                                            arg.Equals("--no-tui", StringComparison.OrdinalIgnoreCase));

        if (command is not ("play-test" or "test-add") && !(command is "add" or "a" or "load" && nonInteractive))
            return false;

        var url = args
            .Skip(1)
            .FirstOrDefault(arg => !arg.StartsWith("-", StringComparison.Ordinal) && IsLikelyUrl(arg));

        if (string.IsNullOrWhiteSpace(url))
        {
            ConsoleHelper.Error("Usage: play-test <url> [--seconds 15] [--install-deps]");
            options = options with { Url = string.Empty };
            return true;
        }

        options = new NonInteractiveOptions(
            url,
            Seconds: ResolveSeconds(args),
            InstallDependencies: args.Any(arg => arg.Equals("--install-deps", StringComparison.OrdinalIgnoreCase)));
        return true;
    }

    private static int ResolveSeconds(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].Equals("--seconds", StringComparison.OrdinalIgnoreCase) &&
                !args[i].Equals("-s", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (i + 1 < args.Length && int.TryParse(args[i + 1], out var seconds))
                return Math.Clamp(seconds, 1, 3600);
        }

        return 15;
    }

    private static string? ResolveUrlArgument(string[] args, string rawInput)
    {
        if (args.Length >= 2)
            return string.Join(' ', args.Skip(1)).Trim();

        var firstSpace = rawInput.IndexOf(' ');
        return firstSpace >= 0 ? rawInput[(firstSpace + 1)..].Trim() : null;
    }

    private static int ResolveArrowSeekSeconds(string command)
    {
        var direction = command.Equals("__seek-forward", StringComparison.OrdinalIgnoreCase) ? 1 : -1;
        return direction * ArrowSeekBaseSeconds;
    }

    private static bool IsInternalCommand(string input) =>
        input.StartsWith("__", StringComparison.Ordinal);

    private static bool TryProcessQueueScrollCommand(
        string command,
        PlayerSnapshot snapshot,
        ref int queueScrollOffset,
        out string message)
    {
        const int scrollStep = 5;
        var totalTracks = snapshot.Previous.Count + snapshot.Pending.Count + (snapshot.Current is null ? 0 : 1);
        var maxOffset = Math.Max(0, totalTracks - 1);

        switch (command.ToLowerInvariant())
        {
            case "__queue-up":
                queueScrollOffset = Math.Max(0, queueScrollOffset - 1);
                message = "Playlist scrolled up.";
                return true;
            case "__queue-down":
                queueScrollOffset = Math.Min(maxOffset, queueScrollOffset + 1);
                message = "Playlist scrolled down.";
                return true;
            case "__queue-page-up":
                queueScrollOffset = Math.Max(0, queueScrollOffset - scrollStep);
                message = "Playlist scrolled up.";
                return true;
            case "__queue-page-down":
                queueScrollOffset = Math.Min(maxOffset, queueScrollOffset + scrollStep);
                message = "Playlist scrolled down.";
                return true;
            case "__queue-home":
                queueScrollOffset = 0;
                message = "Playlist scrolled to the top.";
                return true;
            case "__queue-end":
                queueScrollOffset = maxOffset;
                message = "Playlist scrolled to the bottom.";
                return true;
            default:
                message = string.Empty;
                return false;
        }
    }

    private static bool TryResolveProgressClickCommand(string command, PlayerSnapshot snapshot, out TimeSpan timestamp)
    {
        timestamp = TimeSpan.Zero;
        if (!command.StartsWith("__progress-click:", StringComparison.Ordinal))
            return false;

        var parts = command.Split(':');
        return parts.Length == 3 &&
               int.TryParse(parts[1], out var x) &&
               int.TryParse(parts[2], out var y) &&
               ConsoleHelper.TryResolveProgressClick(x, y, snapshot, out timestamp);
    }

    private static bool IsLikelyUrl(string value) =>
        Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) &&
        (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
         uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));

    private sealed record NonInteractiveOptions(string Url, int Seconds, bool InstallDependencies);
}
