using PopMark.Helpers;
using PopMark.Models;
using System.Text.Json;

namespace PopMark.Services;

public static class QueueCacheStore
{
    private const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static string StateDirectory => PopMarkState.Root;

    private static string CacheFilePath => PopMarkState.QueueFile;

    public static PlayerSnapshot Load()
    {
        var json = AtomicFile.ReadAllText(CacheFilePath);
        var snapshot = json is null ? null : TryParse(json);
        if (snapshot is not null)
            return snapshot;

        var backup = AtomicFile.ReadBackup(CacheFilePath);
        return backup is null ? EmptySnapshot() : TryParse(backup) ?? EmptySnapshot();
    }

    public static void Save(PlayerSnapshot snapshot)
    {
        try
        {
            Directory.CreateDirectory(StateDirectory);
            var cache = new QueueCache(
                CurrentSchemaVersion,
                snapshot.Current,
                snapshot.Pending.ToList(),
                snapshot.Previous.ToList(),
                snapshot.VolumePercent);

            AtomicFile.WriteAllText(CacheFilePath, JsonSerializer.Serialize(cache, JsonOptions));
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

    private static PlayerSnapshot? TryParse(string json)
    {
        try
        {
            var cache = JsonSerializer.Deserialize<QueueCache>(json, JsonOptions);
            if (cache is null || cache.SchemaVersion > CurrentSchemaVersion)
                return null;

            return new PlayerSnapshot(
                PlaybackStatus.Stopped,
                cache.Current,
                cache.Pending ?? new List<Track>(),
                cache.Previous ?? new List<Track>(),
                VolumePercent: Math.Clamp(cache.VolumePercent ?? 100, 0, 130));
        }
        catch
        {
            return null;
        }
    }

    private static PlayerSnapshot EmptySnapshot() =>
        new(PlaybackStatus.Stopped, null, Array.Empty<Track>(), Array.Empty<Track>());

    private sealed record QueueCache(
        int SchemaVersion,
        Track? Current,
        List<Track>? Pending,
        List<Track>? Previous,
        int? VolumePercent);
}
