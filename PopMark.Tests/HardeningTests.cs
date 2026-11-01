using PopMark.Helpers;
using PopMark.Models;
using PopMark.Services;
using System.Diagnostics;
using Xunit;

namespace PopMark.Tests;

[Collection("StateDirectory")]
public sealed class HardeningTests
{
    [Fact]
    public void QueueCacheStore_RoundTripsAndFallsBackToBackup()
    {
        WithStateRoot(root =>
        {
            QueueCacheStore.Save(Snapshot("First"));
            QueueCacheStore.Save(Snapshot("Second"));

            Assert.True(File.Exists(PopMarkState.QueueFile));
            Assert.True(File.Exists($"{PopMarkState.QueueFile}.bak"));
            Assert.Equal("Second", QueueCacheStore.Load().Current?.Title);

            File.WriteAllText(PopMarkState.QueueFile, "{not json");

            Assert.Equal("First", QueueCacheStore.Load().Current?.Title);
        });
    }

    [Fact]
    public void QueueCacheStore_IgnoresNewerSchema()
    {
        WithStateRoot(_ =>
        {
            Directory.CreateDirectory(PopMarkState.Root);
            File.WriteAllText(
                PopMarkState.QueueFile,
                """{"SchemaVersion":99,"Current":{"Title":"Future","Url":"https://example.test","DisplaySource":"YouTube"}}""");

            Assert.Null(QueueCacheStore.Load().Current);
        });
    }

    [Fact]
    public void AtomicFile_KeepsPreviousCopy()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"popmark-atomic-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "state.json");

        try
        {
            AtomicFile.WriteAllText(path, "first");
            AtomicFile.WriteAllText(path, "second");

            Assert.Equal("second", AtomicFile.ReadAllText(path));
            Assert.Equal("first", AtomicFile.ReadBackup(path));
        }
        finally
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public async Task BoundedLineReader_ReadsLinesAndRejectsOversized()
    {
        var reader = new BoundedLineReader(new StringReader("first\r\nsecond\n"), 32);
        var first = await reader.ReadLineAsync(default);
        var second = await reader.ReadLineAsync(default);

        Assert.Equal("first", first.Line);
        Assert.Equal("second", second.Line);
        Assert.False(first.TooLong);

        var oversized = new BoundedLineReader(new StringReader("12345\nok\n"), 4);
        var rejected = await oversized.ReadLineAsync(default);
        var afterRejection = await oversized.ReadLineAsync(default);

        Assert.True(rejected.TooLong);
        Assert.Null(rejected.Line);
        Assert.Equal("ok", afterRejection.Line);
    }

    [Fact]
    public void TargetInput_RejectsOptionsAndNonHttpSchemes()
    {
        Assert.Equal("https://example.test/watch?v=1", TargetInput.Normalize("https://example.test/watch?v=1"));
        Assert.Equal("ytsearch1:daft punk", TargetInput.Normalize("daft punk"));
        Assert.Equal("ytsearch1:already", TargetInput.Normalize("ytsearch1:already"));

        Assert.Throws<InvalidOperationException>(() => TargetInput.Normalize("--exec=calc"));
        Assert.Throws<InvalidOperationException>(() => TargetInput.Normalize("file:///etc/passwd"));
    }

    [Fact]
    public void ProcessJob_KillsChildWhenHandleCloses()
    {
        if (!OperatingSystem.IsWindows())
            return;

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c ping -n 30 127.0.0.1 > nul",
            UseShellExecute = false,
            CreateNoWindow = true
        });

        Assert.NotNull(process);

        var job = ProcessJob.TryCreateFor(process!);
        Assert.NotNull(job);

        job!.Dispose();

        Assert.True(process!.WaitForExit(5000), "mpv-style child should die with the job handle.");
    }

    private static PlayerSnapshot Snapshot(string title) =>
        new(
            PlaybackStatus.Stopped,
            new Track(title, "https://example.test/video", "YouTube"),
            Array.Empty<Track>(),
            Array.Empty<Track>());

    private static void WithStateRoot(Action<string> action)
    {
        var previous = Environment.GetEnvironmentVariable("POPMARK_HOME");
        var root = Path.Combine(Path.GetTempPath(), $"popmark-state-{Guid.NewGuid():N}");

        try
        {
            Environment.SetEnvironmentVariable("POPMARK_HOME", root);
            action(root);
        }
        finally
        {
            Environment.SetEnvironmentVariable("POPMARK_HOME", previous);
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
            }
        }
    }
}
