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
                return new PlayerSnapshot(PlaybackStatus.Stopped, null, Array.Empty<Track>(), Array.Empty<Track>());

            var json = File.ReadAllText(CacheFilePath);
            var cache = JsonSerializer.Deserialize<QueueCache>(json, JsonOptions);
            if (cache is null)
                return new PlayerSnapshot(PlaybackStatus.Stopped, null, Array.Empty<Track>(), Array.Empty<Track>());

            return new PlayerSnapshot(
                PlaybackStatus.Stopped,
                cache.Current,
                cache.Pending ?? new List<Track>(),
                cache.Previous ?? new List<Track>(),
                VolumePercent: Math.Clamp(cache.VolumePercent ?? 100, 0, 130));
        }
        catch
        {
            return new PlayerSnapshot(PlaybackStatus.Stopped, null, Array.Empty<Track>(), Array.Empty<Track>());
        }
    }

    public static void Save(PlayerSnapshot snapshot)
    {
        try
        {
            Directory.CreateDirectory(StateDirectory);
            var cache = new QueueCache(
                snapshot.Current,
                snapshot.Pending.ToList(),
                snapshot.Previous.ToList(),
                snapshot.VolumePercent);
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

    private sealed record QueueCache(
        Track? Current,
        List<Track>? Pending,
        List<Track>? Previous,
        int? VolumePercent);
}
