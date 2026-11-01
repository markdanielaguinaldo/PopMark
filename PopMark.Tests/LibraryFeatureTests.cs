using PopMark.Helpers;
using PopMark.Helpers.Bridge;
using PopMark.Models;
using PopMark.Services;
using System.Text.Json;
using Xunit;

namespace PopMark.Tests;

[Collection("StateDirectory")]
public sealed class LibraryFeatureTests
{
    private static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, BridgeProtocol.JsonOptions);

    [Fact]
    public void TrackIdentity_CanonicalizesYouTubeLinkShapes()
    {
        var watch = TrackIdentity.Key(
            "https://www.youtube.com/watch?v=FGBhQbmPwH8&list=PL123&index=4");
        var shortLink = TrackIdentity.Key("https://youtu.be/FGBhQbmPwH8?t=30");
        var shorts = TrackIdentity.Key("https://www.youtube.com/shorts/FGBhQbmPwH8");
        var embed = TrackIdentity.Key("https://www.youtube.com/embed/FGBhQbmPwH8");
        var live = TrackIdentity.Key("https://www.youtube.com/live/FGBhQbmPwH8?feature=share");

        Assert.Equal(watch, shortLink);
        Assert.Equal(watch, shorts);
        Assert.Equal(watch, embed);
        Assert.Equal(watch, live);
        Assert.True(TrackIdentity.Same(
            "https://www.youtube.com/watch?v=FGBhQbmPwH8",
            "https://youtu.be/FGBhQbmPwH8"));

        Assert.NotEqual(
            watch,
            TrackIdentity.Key("https://www.youtube.com/watch?v=a5uQMwRMHcs"));
    }

    [Fact]
    public void M3uPlaylist_RoundTripsTitlesDurationsAndSources()
    {
        var text = M3uPlaylist.Serialize(new[]
        {
            new Track("Song One", "https://example.test/one", "YouTube", TimeSpan.FromSeconds(185)),
            new Track("Live Set", "https://example.test/two", "YouTube")
        });

        Assert.StartsWith("#EXTM3U", text);
        Assert.Contains("#EXTINF:185,Song One", text);
        Assert.Contains("#EXTINF:-1,Live Set", text);

        var parsed = M3uPlaylist.Parse(
            "#EXTM3U\n#EXTGRP:Radio\n#EXTINF:90,First, with comma\nhttps://example.test/first\n" +
            "/local/file.mp3\nhttps://example.test/second\n");

        Assert.Equal(2, parsed.Count);
        Assert.Equal("First, with comma", parsed[0].Title);
        Assert.Equal("Radio", parsed[0].DisplaySource);
        Assert.Equal(90, parsed[0].Duration?.TotalSeconds);
        Assert.Null(parsed[1].Duration);
        Assert.Equal("Imported", parsed[1].DisplaySource);
    }

    [Fact]
    public void LibraryStore_SavesLoadsDeletesAndFavorites()
    {
        WithStateRoot(_ =>
        {
            var tracks = new[]
            {
                new Track("One", "https://www.youtube.com/watch?v=one", "YouTube", TimeSpan.FromSeconds(60)),
                new Track("Two", "https://www.youtube.com/watch?v=two", "YouTube")
            };

            Assert.Equal("Road Trip", LibraryStore.SaveQueue("Road Trip", tracks));

            var snapshot = LibraryStore.Load();
            var queue = Assert.Single(snapshot.Queues);
            Assert.Equal("Road Trip", queue.Name);
            Assert.Equal(2, queue.TrackCount);

            var loaded = LibraryStore.GetQueue("road trip");
            Assert.NotNull(loaded);
            Assert.Equal(2, loaded!.Count);
            Assert.Equal(60, loaded[0].Duration?.TotalSeconds);

            LibraryStore.SaveQueue("road trip", new[]
            {
                new Track("Three", "https://www.youtube.com/watch?v=three", "YouTube")
            });

            snapshot = LibraryStore.Load();
            queue = Assert.Single(snapshot.Queues);
            Assert.Equal(1, queue.TrackCount);

            Assert.True(LibraryStore.ToggleFavorite(tracks[0], out var added));
            Assert.True(added);
            Assert.True(LibraryStore.IsFavorite("https://youtu.be/one"));
            Assert.Single(LibraryStore.Load().Favorites);

            LibraryStore.ToggleFavorite(tracks[0], out added);
            Assert.False(added);
            Assert.False(LibraryStore.IsFavorite(tracks[0].Url));
            Assert.Empty(LibraryStore.Load().Favorites);

            Assert.True(LibraryStore.DeleteQueue("ROAD TRIP"));
            Assert.Null(LibraryStore.GetQueue("Road Trip"));
            Assert.Empty(LibraryStore.Load().Queues);
        });
    }

    [Fact]
    public void LibraryStore_ExportsAndImportsM3u()
    {
        WithStateRoot(_ =>
        {
            var tracks = new[]
            {
                new Track("One", "https://example.test/one", "YouTube", TimeSpan.FromSeconds(125)),
                new Track("Two", "https://example.test/two", "YouTube")
            };

            var path = LibraryStore.Export(tracks, "mix tape", path: null);

            Assert.True(File.Exists(path));
            Assert.StartsWith(PopMarkState.ExportsDirectory, path);
            Assert.EndsWith(".m3u", path, StringComparison.OrdinalIgnoreCase);

            var imported = LibraryStore.Import("mix tape.m3u");
            Assert.Equal(2, imported.Count);
            Assert.Equal("One", imported[0].Title);
            Assert.Equal(125, imported[0].Duration?.TotalSeconds);

            var explicitPath = Path.Combine(PopMarkState.Root, "custom.m3u");
            Assert.Equal(
                Path.GetFullPath(explicitPath),
                LibraryStore.Export(tracks, "ignored", explicitPath));
            Assert.True(File.Exists(explicitPath));
        });
    }

    [Fact]
    public void LibraryStore_RejectsPlaylistWithoutHttpUrls()
    {
        WithStateRoot(_ =>
        {
            var path = Path.Combine(PopMarkState.Root, "local.m3u");
            Directory.CreateDirectory(PopMarkState.Root);
            File.WriteAllText(path, "#EXTM3U\n/local/song.mp3\n");

            var error = Assert.Throws<InvalidOperationException>(() => LibraryStore.Import(path));
            Assert.Contains("http", error.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void YtDlp_ParseSearchResultsKeepsChannelDurationAndUrl()
    {
        const string json = """
            {
              "entries": [
                {
                  "id": "FGBhQbmPwH8",
                  "title": "Daft Punk - One More Time",
                  "channel": "Daft Punk",
                  "duration": 322,
                  "url": "https://www.youtube.com/watch?v=FGBhQbmPwH8"
                },
                {
                  "id": "a5uQMwRMHcs",
                  "title": "Daft Punk - Instant Crush",
                  "uploader": "Daft Punk",
                  "url": "a5uQMwRMHcs"
                },
                null
              ]
            }
            """;

        var tracks = YtDlpService.ParseSearchResults(json);

        Assert.Equal(2, tracks.Count);
        Assert.Equal("Daft Punk", tracks[0].DisplaySource);
        Assert.Equal(322, tracks[0].Duration?.TotalSeconds);
        Assert.Equal("https://www.youtube.com/watch?v=FGBhQbmPwH8", tracks[0].Url);
        Assert.Equal("Daft Punk", tracks[1].DisplaySource);
        Assert.Equal("https://www.youtube.com/watch?v=a5uQMwRMHcs", tracks[1].Url);
    }

    [Fact]
    public void BridgeSearchAndLibraryMessagesSerializeForTheUi()
    {
        var track = new Track("Song", "https://example.test/song", "Channel", TimeSpan.FromSeconds(75));

        var search = BridgeSearchResultsDto.Create("search-1", "example", new[] { track });
        var searchJson = Serialize(search);

        Assert.Equal("search-results", search.Type);
        Assert.Equal("search-1", search.Id);
        Assert.Contains("\"query\":\"example\"", searchJson);
        Assert.Contains("\"displaySource\":\"Channel\"", searchJson);
        Assert.Contains("\"durationSeconds\":75", searchJson);

        var library = BridgeLibraryDto.From(new LibraryStore.LibrarySnapshot(
            new[]
            {
                new LibraryStore.SavedQueueInfo("Road Trip", 2, DateTimeOffset.UtcNow)
            },
            new[] { track }));
        var libraryJson = Serialize(library);

        Assert.Equal("library", library.Type);
        Assert.Single(library.Queues);
        Assert.Single(library.Favorites);
        Assert.Contains("\"trackCount\":2", libraryJson);
        Assert.Contains("\"favorites\":[", libraryJson);
    }

    [Fact]
    public void BridgeRequestParsesSearchTrackAndQueueEdits()
    {
        Assert.True(BridgeRequest.TryParse(
            """{"type":"search","id":"s1","text":"daft punk","limit":8}""",
            out var search,
            out _));
        Assert.Equal("daft punk", search!.Text);
        Assert.Equal(8, search.Limit);

        Assert.True(BridgeRequest.TryParse(
            """{"type":"add-track","track":{"title":"Song","url":"https://example.test/song","displaySource":"Channel","durationSeconds":75.5}}""",
            out var add,
            out _));
        Assert.Equal("Song", add!.Track?.Title);
        Assert.Equal(75.5, add.Track?.DurationSeconds);

        Assert.True(BridgeRequest.TryParse(
            """{"type":"move-track","index":3,"to":1}""",
            out var move,
            out _));
        Assert.Equal(3, move!.Index);
        Assert.Equal(1, move.To);

        Assert.True(BridgeRequest.TryParse(
            """{"type":"library","action":"load","name":"Road Trip","play":true}""",
            out var library,
            out _));
        Assert.Equal("load", library!.Action);
        Assert.Equal("Road Trip", library.Name);
        Assert.True(library.Play);
    }

    private static void WithStateRoot(Action<string> action)
    {
        var previous = Environment.GetEnvironmentVariable("POPMARK_HOME");
        var root = Path.Combine(Path.GetTempPath(), $"popmark-library-{Guid.NewGuid():N}");

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
