using PopMark.Models;
using PopMark.Services;
using System.Text;
using Xunit;

namespace PopMark.Tests;

public sealed class MpvPositionIntegrationTests
{
    [Fact]
    public async Task MpvReportsLivePositionAndDuration()
    {
        // Opt-in: needs a real mpv install and audio output. CI skips it.
        if (!string.Equals(
                Environment.GetEnvironmentVariable("POPMARK_MPV_INTEGRATION"),
                "1",
                StringComparison.Ordinal))
        {
            return;
        }

        if (ToolLocator.ResolveRunnableExecutable("mpv") is null)
            return;

        var mediaPath = Path.Combine(Path.GetTempPath(), $"popmark-silence-{Guid.NewGuid():N}.wav");
        WriteSilentWav(mediaPath, seconds: 4);

        var player = new MpvPlayer();
        try
        {
            await player.PlayAsync(
                new Track("silence", mediaPath, "local", TimeSpan.FromSeconds(4)),
                volumePercent: 0);

            double? first = null;
            for (var attempt = 0; attempt < 30; attempt++)
            {
                await Task.Delay(150);
                first = await player.TryGetNumberPropertyAsync("time-pos");
                if (first is > 0.05)
                    break;
            }

            Assert.NotNull(first);
            Assert.True(first > 0, $"Expected a live position, got {first}.");

            await Task.Delay(700);
            var second = await player.TryGetNumberPropertyAsync("time-pos");
            Assert.NotNull(second);
            Assert.True(second > first, $"Expected position to advance from {first} to > {first}, got {second}.");

            var duration = await player.TryGetNumberPropertyAsync("duration");
            Assert.NotNull(duration);
            Assert.InRange(duration!.Value, 3.5, 4.5);
        }
        finally
        {
            await player.StopAsync();
            try
            {
                File.Delete(mediaPath);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public async Task PlaybackQueueUsesMpvPositionAndFillsMissingDuration()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("POPMARK_MPV_INTEGRATION"),
                "1",
                StringComparison.Ordinal))
        {
            return;
        }

        if (ToolLocator.ResolveRunnableExecutable("mpv") is null)
            return;

        var mediaPath = Path.Combine(Path.GetTempPath(), $"popmark-queue-{Guid.NewGuid():N}.wav");
        WriteSilentWav(mediaPath, seconds: 4);

        var player = new MpvPlayer();
        var queue = new PlaybackQueue(new YtDlpService(), player);
        try
        {
            queue.RestoreQueue(new PlayerSnapshot(
                PlaybackStatus.Stopped,
                null,
                new[] { new Track("silence", mediaPath, "local") },
                Array.Empty<Track>(),
                VolumePercent: 0));

            await queue.NextAsync();
            await Task.Delay(900);
            await queue.RefreshPositionAsync();

            var snapshot = queue.CreateSnapshot();
            Assert.NotNull(snapshot.Current);
            Assert.True(snapshot.Elapsed > TimeSpan.FromSeconds(0.1), $"Elapsed was {snapshot.Elapsed}.");
            Assert.NotNull(snapshot.Duration);
            Assert.InRange(snapshot.Duration!.Value.TotalSeconds, 3.5, 4.5);

            await queue.PauseAsync();
            await Task.Delay(300);
            await queue.RefreshPositionAsync();
            var paused = queue.CreateSnapshot();
            await Task.Delay(600);
            await queue.RefreshPositionAsync();
            var stillPaused = queue.CreateSnapshot();
            Assert.InRange(
                (stillPaused.Elapsed.GetValueOrDefault() - paused.Elapsed.GetValueOrDefault()).Duration(),
                TimeSpan.Zero,
                TimeSpan.FromMilliseconds(250));

            await queue.ResumeAsync();
            await Task.Delay(700);
            await queue.RefreshPositionAsync();
            var resumed = queue.CreateSnapshot();
            Assert.True(
                resumed.Elapsed.GetValueOrDefault() > stillPaused.Elapsed.GetValueOrDefault() + TimeSpan.FromMilliseconds(200),
                $"Expected playback to resume from {stillPaused.Elapsed}, got {resumed.Elapsed}.");

            await queue.SeekAbsoluteAsync(TimeSpan.FromSeconds(2));
            await Task.Delay(300);
            await queue.RefreshPositionAsync();
            var sought = queue.CreateSnapshot();
            Assert.InRange(
                sought.Elapsed.GetValueOrDefault().TotalSeconds,
                1.6,
                3.2);
        }
        finally
        {
            await queue.StopAsync(clearQueue: true);
            try
            {
                File.Delete(mediaPath);
            }
            catch
            {
            }
        }
    }

    private static void WriteSilentWav(string path, int seconds)
    {
        const int sampleRate = 8000;
        const short channels = 1;
        const short bitsPerSample = 16;
        var dataSize = sampleRate * seconds * channels * (bitsPerSample / 8);
        var byteRate = sampleRate * channels * (bitsPerSample / 8);

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: false);

        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataSize);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write((short)(channels * bitsPerSample / 8));
        writer.Write(bitsPerSample);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataSize);
        writer.Write(new byte[dataSize]);
    }
}
