using PopMark.Helpers.Bridge;
using PopMark.Models;
using System.Text.Json;
using Xunit;

namespace PopMark.Tests;

public sealed class BridgeProtocolTests
{
    private static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, BridgeProtocol.JsonOptions);

    [Fact]
    public void TrackDtoUsesCamelCaseAndOmitsUnknownDuration()
    {
        var track = new Track("Song", "https://example.com", "YouTube");

        var json = Serialize(BridgeTrackDto.From(track));

        Assert.Contains("\"title\":\"Song\"", json);
        Assert.Contains("\"displaySource\":\"YouTube\"", json);
        Assert.DoesNotContain("durationSeconds", json);
    }

    [Fact]
    public void QueueDtoMarksCurrentIndexAfterPrevious()
    {
        var snapshot = new PlayerSnapshot(
            PlaybackStatus.Playing,
            new Track("Now", "https://example.com/now", "YouTube", TimeSpan.FromSeconds(90)),
            new[] { new Track("Next", "https://example.com/next", "YouTube") },
            new[] { new Track("Before", "https://example.com/before", "YouTube") },
            TimeSpan.FromSeconds(12),
            80);

        var queue = BridgeQueueDto.From(snapshot, revision: 7);
        var json = Serialize(queue);

        Assert.Equal(7, queue.Revision);
        Assert.Equal(1, queue.CurrentIndex);
        Assert.Equal("Now", queue.Current?.Title);
        Assert.Equal(90, queue.Current?.DurationSeconds);
        Assert.Contains("\"type\":\"queue\"", json);
        Assert.Contains("\"currentIndex\":1", json);
    }

    [Fact]
    public void StateDtoCarriesPlaybackFields()
    {
        var snapshot = new PlayerSnapshot(
            PlaybackStatus.Paused,
            new Track("Now", "https://example.com/now", "YouTube", TimeSpan.FromSeconds(120)),
            Array.Empty<Track>(),
            Array.Empty<Track>(),
            TimeSpan.FromSeconds(30),
            65);

        var state = BridgeStateDto.From(snapshot, revision: 3, busy: true, message: "Paused.");

        Assert.Equal("Paused", state.Status);
        Assert.Equal(30, state.ElapsedSeconds);
        Assert.Equal(120, state.DurationSeconds);
        Assert.Equal(65, state.VolumePercent);
        Assert.True(state.Busy);
        Assert.Equal("Paused.", state.Message);
    }

    [Fact]
    public void RequestParserAcceptsStructuredCommands()
    {
        Assert.True(BridgeRequest.TryParse(
            """{"type":"seek","id":"abc","seconds":42.5}""",
            out var request,
            out var error));

        Assert.Null(error);
        Assert.NotNull(request);
        Assert.Equal("seek", request!.Type);
        Assert.Equal("abc", request.Id);
        Assert.Equal(42.5, request.Seconds);

        Assert.True(BridgeRequest.TryParse(
            """{"type":"tools","action":"install","tool":"mpv"}""",
            out var tools,
            out _));
        Assert.Equal("install", tools!.Action);
        Assert.Equal("mpv", tools.Tool);
    }

    [Fact]
    public void RequestParserRejectsMalformedPayloads()
    {
        Assert.False(BridgeRequest.TryParse("{not json", out _, out var error));
        Assert.NotNull(error);

        Assert.False(BridgeRequest.TryParse("""{"id":"missing-type"}""", out _, out error));
        Assert.NotNull(error);

        Assert.False(BridgeRequest.TryParse("   ", out _, out _));
    }

    [Fact]
    public void HelloAndByeAreSingleLineMessages()
    {
        var hello = Serialize(BridgeHelloDto.Create("1.2.3"));
        var bye = Serialize(new BridgeByeDto("bye"));

        Assert.Contains("\"type\":\"hello\"", hello);
        Assert.Contains("\"protocol\":1", hello);
        Assert.Contains("\"version\":\"1.2.3\"", hello);
        Assert.Equal("""{"type":"bye"}""", bye);
    }

    [Fact]
    public void LogAndLiveDurationMessagesKeepDiagnosticsOutOfStateText()
    {
        var log = Serialize(BridgeLogDto.Create("tools", "warn", "mpv missing"));
        Assert.Contains("\"type\":\"log\"", log);
        Assert.Contains("\"level\":\"warn\"", log);
        Assert.Contains("\"source\":\"tools\"", log);

        var snapshot = new PlayerSnapshot(
            PlaybackStatus.Playing,
            new Track("Live", "https://example.com/live", "local"),
            Array.Empty<Track>(),
            Array.Empty<Track>(),
            TimeSpan.FromSeconds(10),
            50,
            TimeSpan.FromSeconds(240));

        var state = BridgeStateDto.From(snapshot, revision: 1, busy: false, message: "Playing.");
        Assert.Equal(240, state.DurationSeconds);
    }
}
