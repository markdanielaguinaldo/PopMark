using PopMark.Models;
using System.Text.Json;

namespace PopMark.Services;

public static class QueueCacheStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static string StateDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PopMark");

    private static string CacheFilePath => Path.Combine(StateDirectory, "queue.json");

    public static PlayerSnapshot Load()
    {
        try
        {
            if (!File.Exists(CacheFilePath))
                return new PlayerSnapshot(PlaybackStatus.Stopped, null, [], []);

            var json = File.ReadAllText(CacheFilePath);
            var cache = JsonSerializer.Deserialize<QueueCache>(json, JsonOptions);
            if (cache is null)
                return new PlayerSnapshot(PlaybackStatus.Stopped, null, [], []);

            return new PlayerSnapshot(PlaybackStatus.Stopped, cache.Current, cache.Pending, []);
        }
        catch
        {
            return new PlayerSnapshot(PlaybackStatus.Stopped, null, [], []);
        }
    }

    public static void Save(PlayerSnapshot snapshot)
    {
        try
        {
            Directory.CreateDirectory(StateDirectory);
            var cache = new QueueCache(snapshot.Current, snapshot.Pending.ToList());
            File.WriteAllText(CacheFilePath, JsonSerializer.Serialize(cache, JsonOptions));
        }
        catch
        {
        }
    }

    public static void Clear()
    {
        try
        {
            if (File.Exists(CacheFilePath))
                File.Delete(CacheFilePath);
        }
        catch
        {
        }
    }

    private sealed record QueueCache(Track? Current, List<Track> Pending);
}
