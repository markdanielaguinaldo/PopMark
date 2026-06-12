using PopMark.Helpers;
using PopMark.Models;
using PopMark.Services;
using Spectre.Console;

internal static class Program
{
    private const int ArrowSeekBaseSeconds = 10;
    private const int VolumeStepPercent = 10;

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
        var shutdownGuard = new PlaybackShutdownGuard(
            player,
            () =>
            {
                ConsoleHelper.ResetCursorStyle();
                ConsoleHelper.LeaveInteractiveScreen();
            });
        var dependencies = new DependencyInstaller();
        var history = new List<string>();
        var notice = "Ready. Add a YouTube video or playlist URL to start.";
        var showHelp = false;
        var showControls = false;
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

                if (!dependencies.ArePlaybackDependenciesAvailable())
                {
                    player.LastMessage = await dependencies.EnsurePlaybackDependenciesAsync(
                        promptToInstall: true,
                        confirmInstall: true);
                    notice = player.LastMessage;
                }

                var cachedQueue = QueueCacheStore.Load();
                if (cachedQueue.Current is not null ||
                    cachedQueue.Pending.Count > 0 ||
                    cachedQueue.Previous.Count > 0)
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
                ConsoleHelper.DrawCommandCenter(player.CreateSnapshot(), player.LastMessage, showHelp, queueScrollOffset, showControls);
                ConsoleHelper.UseBarCursor();

                var input = ConsoleHelper.ReadReactiveInput(
                    ref lastWidth,
                    ref lastHeight,
                    history,
                    () => player.CreateSnapshot(),
                    () => player.LastMessage,
                    null,
                    () => showHelp,
                    () => queueScrollOffset,
                    () => showControls);
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
                    if (TryResolvePlaylistClickCommand(parsedArgs[0], player.CreateSnapshot(), out var trackIndex))
                    {
                        await player.PlayAtQueueIndexAsync(trackIndex);
                        notice = player.LastMessage;
                        (lastWidth, lastHeight) = ConsoleHelper.GetWindowSize();
                        continue;
                    }

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

                    if (IsSeekCommand(parsedArgs[0]))
                    {
                        await player.SeekRelativeAsync(ResolveArrowSeekSeconds(parsedArgs[0]));
                        notice = player.LastMessage;
                        (lastWidth, lastHeight) = ConsoleHelper.GetWindowSize();
                        continue;
                    }

                    if (IsVolumeCommand(parsedArgs[0]))
                    {
                        await player.AdjustVolumeAsync(ResolveVolumeStepPercent(parsedArgs[0]));
                        notice = player.LastMessage;
                        (lastWidth, lastHeight) = ConsoleHelper.GetWindowSize();
                        continue;
                    }

                    if (IsInternalCommand(parsedArgs[0]))
                    {
                        (lastWidth, lastHeight) = ConsoleHelper.GetWindowSize();
                        continue;
                    }

                    if (IsShuffleCommand(parsedArgs[0]))
                    {
                        player.ShufflePlaylist();
                        var snapshot = player.CreateSnapshot();
                        queueScrollOffset = snapshot.Current is null ? 0 : snapshot.Previous.Count;
                        notice = player.LastMessage;
                        (lastWidth, lastHeight) = ConsoleHelper.GetWindowSize();
                        continue;
                    }

                    if (TryResolveGoToCommand(parsedArgs, out var goToTarget, out var goToMessage))
                    {
                        if (string.IsNullOrWhiteSpace(goToTarget))
                        {
                            player.LastMessage = goToMessage;
                        }
                        else if (player.ResolvePlaylistIndex(goToTarget) is { } targetIndex)
                        {
                            queueScrollOffset = targetIndex;
                            player.LastMessage = $"Playlist moved to song {targetIndex + 1}.";
                        }
                        else
                        {
                            player.LastMessage = $"No playlist song matches: {goToTarget}";
                        }

                        notice = player.LastMessage;
                        (lastWidth, lastHeight) = ConsoleHelper.GetWindowSize();
                        continue;
                    }

                    showHelp = parsedArgs[0].Equals("help", StringComparison.OrdinalIgnoreCase) ||
                               parsedArgs[0].Equals("h", StringComparison.OrdinalIgnoreCase) ||
                               parsedArgs[0].Equals("?", StringComparison.OrdinalIgnoreCase);
                    showControls = parsedArgs[0].Equals("controls", StringComparison.OrdinalIgnoreCase) ||
                                   parsedArgs[0].Equals("control", StringComparison.OrdinalIgnoreCase);
                    if (showControls)
                        showHelp = false;

                    keepRunning = !await ProcessCommandAsync(
                        player,
                        dependencies,
                        parsedArgs,
                        input);
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
            shutdownGuard.Cleanup();
            ConsoleHelper.ResetCursorStyle();
            ConsoleHelper.LeaveInteractiveScreen();
        }
    }

    private static async Task<bool> ProcessCommandAsync(
        PlaybackQueue player,
        DependencyInstaller dependencies,
        string[] args,
        string rawInput)
    {
        var command = args[0].ToLowerInvariant();

        switch (command)
        {
            case "help":
            case "h":
            case "?":
                player.LastMessage = "Commands are visible in the command center.";
                return false;

            case "controls":
            case "control":
                player.LastMessage = "Controls are visible in the command center.";
                return false;

            case "add":
            case "a":
            case "load":
                var url = ResolveUrlArgument(args, rawInput);
                if (string.IsNullOrWhiteSpace(url))
                {
                    url = ConsoleHelper.RunWithStandardInput(() =>
                        AnsiConsole.Prompt(
                            new TextPrompt<string>("[bold deepskyblue1]YouTube URL[/]:")
                                .Validate(value => IsLikelyUrl(value)
                                    ? ValidationResult.Success()
                                    : ValidationResult.Error("[cyan1]Enter a valid URL.[/]"))));
                }

                await AddUrlAsync(player, dependencies, url, promptToInstallDependencies: true, confirmInstallDependencies: true, showStatus: true);
                return false;

            case "play":
            case "pause":
            case "r":
                await player.TogglePauseAsync();
                return false;

            case "next":
            case "skip":
            case "n":
            case "]":
                player.LastMessage = DeprecatedTrackNavigationMessage("next");
                return false;

            case "previous":
            case "prev":
            case "back":
            case "[":
                player.LastMessage = DeprecatedTrackNavigationMessage("previous");
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
                await player.ClearPlaylistAsync();
                return false;

            case "clear":
                if (args.Length > 1 &&
                    args[1].Equals("playlist", StringComparison.OrdinalIgnoreCase))
                {
                    await player.ClearPlaylistAsync();
                    return false;
                }

                player.LastMessage = "Unknown command: clear. Type help to list commands.";
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

    private static string DeprecatedTrackNavigationMessage(string command) =>
        $"{command} is deprecated. Click a playlist song to play it directly, or use goto <#|title> to focus a song.";


    private static bool IsShuffleCommand(string command) =>
        command.Equals("shuffle", StringComparison.OrdinalIgnoreCase) ||
        command.Equals("shuf", StringComparison.OrdinalIgnoreCase);

    private static bool TryResolveGoToCommand(string[] args, out string target, out string message)
    {
        target = string.Empty;
        message = "Usage: goto <playlist number or song title>.";
        if (args.Length == 0)
            return false;

        var command = args[0].ToLowerInvariant();
        var targetStartIndex = command switch
        {
            "goto" or "go-to" or "jump" or "song" => 1,
            "go" when args.Length >= 2 && args[1].Equals("to", StringComparison.OrdinalIgnoreCase) => 2,
            _ => -1
        };

        if (targetStartIndex < 0)
            return false;

        if (args.Length <= targetStartIndex)
            return true;

        target = string.Join(' ', args.Skip(targetStartIndex)).Trim();
        return true;
    }

    private static int ResolveArrowSeekSeconds(string command)
    {
        var direction = command.Equals("__seek-forward", StringComparison.OrdinalIgnoreCase) ? 1 : -1;
        return direction * ArrowSeekBaseSeconds;
    }

    private static bool IsSeekCommand(string command) =>
        command.Equals("__seek-forward", StringComparison.OrdinalIgnoreCase) ||
        command.Equals("__seek-back", StringComparison.OrdinalIgnoreCase);

    private static int ResolveVolumeStepPercent(string command) =>
        command.Equals("__volume-up", StringComparison.OrdinalIgnoreCase)
            ? VolumeStepPercent
            : -VolumeStepPercent;

    private static bool IsVolumeCommand(string command) =>
        command.Equals("__volume-up", StringComparison.OrdinalIgnoreCase) ||
        command.Equals("__volume-down", StringComparison.OrdinalIgnoreCase);

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
        if (!TryResolveMousePointCommand(command, out var x, out var y))
            return false;

        return ConsoleHelper.TryResolveProgressClick(x, y, snapshot, out timestamp);
    }

    private static bool TryResolvePlaylistClickCommand(string command, PlayerSnapshot snapshot, out int trackIndex)
    {
        trackIndex = -1;
        if (!TryResolveMousePointCommand(command, out var x, out var y))
            return false;

        return ConsoleHelper.TryResolvePlaylistClick(x, y, snapshot, out trackIndex);
    }

    private static bool TryResolveMousePointCommand(string command, out int x, out int y)
    {
        x = 0;
        y = 0;
        if (!command.StartsWith("__mouse-click:", StringComparison.Ordinal) &&
            !command.StartsWith("__progress-click:", StringComparison.Ordinal))
        {
            return false;
        }

        var parts = command.Split(':');
        return parts.Length == 3 &&
               int.TryParse(parts[1], out x) &&
               int.TryParse(parts[2], out y);
    }

    private static bool IsLikelyUrl(string value) =>
        Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) &&
        (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
         uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));

    private sealed record NonInteractiveOptions(string Url, int Seconds, bool InstallDependencies);
}
