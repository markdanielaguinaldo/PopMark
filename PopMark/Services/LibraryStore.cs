using PopMark.Helpers;
using PopMark.Models;
using System.Text.Json;

namespace PopMark.Services;

/// <summary>
/// Named queue and favorite storage. Lives beside queue.json so the library
/// survives restarts, and writes through AtomicFile like the rest of the state.
/// </summary>
public static class LibraryStore
{
    private const int CurrentSchemaVersion = 1;
    private const int MaxQueues = 200;
    private const int MaxFavorites = 1000;
    private const int MaxNameLength = 80;
    private const long MaxImportBytes = 5 * 1024 * 1024;

    private static readonly object Sync = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string FilePath => PopMarkState.LibraryFile;

    public static string ExportDirectory => PopMarkState.ExportsDirectory;

    public static LibrarySnapshot Load()
    {
        lock (Sync)
        {
            var file = ReadFile();
            return new LibrarySnapshot(
                file.Queues.Select(ToInfo).ToList(),
                file.Favorites.ToList());
        }
    }

    public static string SaveQueue(string? name, IReadOnlyList<Track> tracks)
    {
        var queueName = NormalizeName(name);
        var snapshot = tracks
            .Where(track => !string.IsNullOrWhiteSpace(track.Url))
            .Select(CloneTrack)
            .ToList();

        lock (Sync)
        {
            var file = ReadFile();
            file.Queues.RemoveAll(queue => SameName(queue.Name, queueName));
            file.Queues.Insert(0, new SavedQueue(queueName, DateTimeOffset.UtcNow, snapshot));
            if (file.Queues.Count > MaxQueues)
                file.Queues.RemoveRange(MaxQueues, file.Queues.Count - MaxQueues);

            WriteFile(file);
            return queueName;
        }
    }

    public static bool DeleteQueue(string name)
    {
        lock (Sync)
        {
            var file = ReadFile();
            var removed = file.Queues.RemoveAll(queue => SameName(queue.Name, name)) > 0;
            if (removed)
                WriteFile(file);

            return removed;
        }
    }

    public static IReadOnlyList<Track>? GetQueue(string name)
    {
        lock (Sync)
        {
            var queue = ReadFile().Queues.FirstOrDefault(candidate => SameName(candidate.Name, name));
            return queue?.Tracks.Select(CloneTrack).ToList();
        }
    }

    public static bool ToggleFavorite(Track track, out bool added)
    {
        lock (Sync)
        {
            var file = ReadFile();
            var key = TrackIdentity.Key(track.Url);
            var existing = file.Favorites.FindIndex(candidate => TrackIdentity.Key(candidate.Url) == key);

            if (existing >= 0)
            {
                file.Favorites.RemoveAt(existing);
                added = false;
            }
            else
            {
                file.Favorites.Insert(0, CloneTrack(track));
                if (file.Favorites.Count > MaxFavorites)
                    file.Favorites.RemoveRange(MaxFavorites, file.Favorites.Count - MaxFavorites);
                added = true;
            }

            WriteFile(file);
            return true;
        }
    }

    public static bool IsFavorite(string url)
    {
        lock (Sync)
        {
            var key = TrackIdentity.Key(url);
            return ReadFile().Favorites.Any(track => TrackIdentity.Key(track.Url) == key);
        }
    }

    public static string Export(IReadOnlyList<Track> tracks, string? name, string? path)
    {
        var target = ResolveExportPath(name, path);
        var directory = Path.GetDirectoryName(target);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        AtomicFile.WriteAllText(target, M3uPlaylist.Serialize(tracks));
        return target;
    }

    public static IReadOnlyList<Track> Import(string path)
    {
        var resolved = ResolveImportPath(path);
        var info = new FileInfo(resolved);
        if (!info.Exists)
            throw new InvalidOperationException($"No playlist file at {resolved}.");

        if (info.Length > MaxImportBytes)
            throw new InvalidOperationException("That playlist is larger than 5 MB.");

        var tracks = M3uPlaylist.Parse(File.ReadAllText(resolved));
        if (tracks.Count == 0)
            throw new InvalidOperationException("No http(s) track URLs were found in that playlist.");

        return tracks.Take(5000).ToList();
    }

    public static string ResolveExportPath(string? name, string? path)
    {
        var directory = PopMarkState.ExportsDirectory;

        if (!string.IsNullOrWhiteSpace(path))
        {
            var value = path.Trim().Trim('"');
            var candidate = Path.IsPathRooted(value)
                ? value
                : Path.Combine(directory, value);

            if (string.IsNullOrWhiteSpace(Path.GetExtension(candidate)))
                candidate += ".m3u";

            return Path.GetFullPath(candidate);
        }

        var file = SanitizeFileName(string.IsNullOrWhiteSpace(name) ? "queue" : name);
        return Path.Combine(directory, $"{file}.m3u");
    }

    private static string ResolveImportPath(string path)
    {
        var value = path.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("Usage: import <path to .m3u>.");

        if (Path.IsPathRooted(value))
            return Path.GetFullPath(value);

        var underExports = Path.GetFullPath(Path.Combine(PopMarkState.ExportsDirectory, value));
        if (File.Exists(underExports))
            return underExports;

        return Path.GetFullPath(value);
    }

    private static string NormalizeName(string? name)
    {
        var value = SanitizeName(name);
        return string.IsNullOrWhiteSpace(value)
            ? $"Queue {DateTimeOffset.Now:yyyy-MM-dd HH:mm}"
            : value;
    }

    private static string SanitizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var trimmed = name.Trim().Trim('"');
        var builder = new System.Text.StringBuilder(Math.Min(trimmed.Length, MaxNameLength));
        foreach (var character in trimmed)
        {
            if (builder.Length >= MaxNameLength)
                break;

            if (!char.IsControl(character))
                builder.Append(character);
        }

        return builder.ToString().Trim();
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Select(character => invalid.Contains(character) ? '-' : character).ToArray());
        cleaned = cleaned.Trim().TrimEnd('.');
        return string.IsNullOrWhiteSpace(cleaned) ? "queue" : cleaned;
    }

    private static bool SameName(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static SavedQueueInfo ToInfo(SavedQueue queue) =>
        new(queue.Name, queue.Tracks.Count, queue.SavedAtUtc);

    private static Track CloneTrack(Track track) =>
        new(track.Title, track.Url, track.DisplaySource, track.Duration);

    private static LibraryFile ReadFile()
    {
        var json = AtomicFile.ReadAllText(FilePath);
        var parsed = TryParse(json);
        if (parsed is not null)
            return parsed;

        return TryParse(AtomicFile.ReadBackup(FilePath)) ?? EmptyFile();
    }

    private static LibraryFile? TryParse(string? json)
    {
        if (json is null)
            return null;

        try
        {
            var file = JsonSerializer.Deserialize<LibraryFile>(json, JsonOptions);
            if (file is null || file.SchemaVersion > CurrentSchemaVersion)
                return null;

            var queues = (file.Queues ?? new List<SavedQueue>())
                .Where(queue => !string.IsNullOrWhiteSpace(queue.Name))
                .Select(queue => new SavedQueue(
                    SanitizeName(queue.Name),
                    queue.SavedAtUtc,
                    (queue.Tracks ?? new List<Track>())
                        .Where(track => !string.IsNullOrWhiteSpace(track.Url))
                        .Select(CloneTrack)
                        .ToList()))
                .Where(queue => queue.Tracks.Count > 0)
                .Take(MaxQueues)
                .ToList();

            var favorites = (file.Favorites ?? new List<Track>())
                .Where(track => !string.IsNullOrWhiteSpace(track.Url))
                .Select(CloneTrack)
                .Take(MaxFavorites)
                .ToList();

            return new LibraryFile(CurrentSchemaVersion, queues, favorites);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void WriteFile(LibraryFile file)
    {
        Directory.CreateDirectory(PopMarkState.Root);
        AtomicFile.WriteAllText(FilePath, JsonSerializer.Serialize(file, JsonOptions));
    }

    private static LibraryFile EmptyFile() =>
        new(CurrentSchemaVersion, new List<SavedQueue>(), new List<Track>());

    public sealed record SavedQueueInfo(string Name, int TrackCount, DateTimeOffset SavedAtUtc);

    public sealed record LibrarySnapshot(IReadOnlyList<SavedQueueInfo> Queues, IReadOnlyList<Track> Favorites);

    private sealed record SavedQueue(string Name, DateTimeOffset SavedAtUtc, List<Track> Tracks);

    private sealed record LibraryFile(
        int SchemaVersion,
        List<SavedQueue> Queues,
        List<Track> Favorites);
}
