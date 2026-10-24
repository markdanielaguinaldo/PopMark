using PopMark.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PopMark.Helpers.Bridge;

/// <summary>
/// Wire format for the JSON-lines protocol spoken between the OpenTUI frontend
/// and the PopMark backend. Every message is a single line of UTF-8 JSON.
/// </summary>
internal static class BridgeProtocol
{
    public const int Version = 1;
    public const string BridgeFlag = "--bridge";

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

internal sealed record BridgeTrackDto(
    string Title,
    string Url,
    string DisplaySource,
    double? DurationSeconds)
{
    public static BridgeTrackDto From(Track track) =>
        new(track.Title, track.Url, track.DisplaySource, track.Duration?.TotalSeconds);
}

internal sealed record BridgeHelloDto(
    string Type,
    int Protocol,
    string App,
    string Version)
{
    public static BridgeHelloDto Create(string version) =>
        new("hello", BridgeProtocol.Version, "PopMark", version);
}

internal sealed record BridgeQueueDto(
    string Type,
    long Revision,
    BridgeTrackDto? Current,
    int CurrentIndex,
    IReadOnlyList<BridgeTrackDto> Previous,
    IReadOnlyList<BridgeTrackDto> Pending)
{
    public static BridgeQueueDto From(PlayerSnapshot snapshot, long revision) =>
        new(
            "queue",
            revision,
            snapshot.Current is null ? null : BridgeTrackDto.From(snapshot.Current),
            snapshot.Current is null ? -1 : snapshot.Previous.Count,
            snapshot.Previous.Select(BridgeTrackDto.From).ToList(),
            snapshot.Pending.Select(BridgeTrackDto.From).ToList());
}

internal sealed record BridgeStateDto(
    string Type,
    long Revision,
    string Status,
    double ElapsedSeconds,
    double? DurationSeconds,
    int VolumePercent,
    string Message,
    bool Busy)
{
    public static BridgeStateDto From(PlayerSnapshot snapshot, long revision, bool busy, string message)
    {
        var duration = snapshot.Duration ??
                       (snapshot.Current?.Duration is { } trackDuration && trackDuration > TimeSpan.Zero
                           ? trackDuration
                           : null);

        return new(
            "state",
            revision,
            snapshot.Status.ToString(),
            Math.Max(0, snapshot.Elapsed?.TotalSeconds ?? 0),
            duration?.TotalSeconds,
            snapshot.VolumePercent,
            message,
            busy);
    }
}

internal sealed record BridgeToolDto(
    string Name,
    string DisplayName,
    string? Path,
    bool Runnable,
    string? PinnedPath);

internal sealed record BridgeToolsDto(
    string Type,
    IReadOnlyList<BridgeToolDto> Tools,
    string ToolRoot,
    string OverrideFile)
{
    public static BridgeToolsDto From(IReadOnlyList<(string Name, string DisplayName, string? Path, bool Runnable, string? PinnedPath)> tools) =>
        new(
            "tools",
            tools.Select(tool => new BridgeToolDto(
                tool.Name,
                tool.DisplayName,
                tool.Path,
                tool.Runnable,
                tool.PinnedPath)).ToList(),
            Services.ToolLocator.ToolRoot,
            Services.ToolLocator.OverrideFilePath);
}

internal sealed record BridgeBusyDto(string Type, bool Busy, string? Label)
{
    public static BridgeBusyDto Start(string label) => new("busy", true, label);
    public static BridgeBusyDto Stop() => new("busy", false, null);
}

internal sealed record BridgeLogDto(string Type, string Level, string Source, string Line)
{
    public static BridgeLogDto Create(string source, string level, string line) =>
        new("log", level, source, line);
}

internal sealed record BridgeAckDto(string Type, string? Id, bool Ok, string? Message)
{
    public static BridgeAckDto Success(string? id, string? message = null) =>
        new("ack", id, true, string.IsNullOrWhiteSpace(message) ? null : message);

    public static BridgeAckDto Failure(string? id, string message) =>
        new("ack", id, false, message);
}

internal sealed record BridgeErrorDto(string Type, string? Id, string Message)
{
    public static BridgeErrorDto Create(string? id, string message) => new("error", id, message);
}

internal sealed record BridgeByeDto(string Type);

internal sealed record BridgeRequest(
    string Type,
    string? Id = null,
    string? Name = null,
    string? Text = null,
    string? Value = null,
    string? Target = null,
    string? Action = null,
    string? Tool = null,
    string? Path = null,
    double? Seconds = null,
    double? Delta = null,
    int? Percent = null,
    int? Index = null)
{
    public static bool TryParse(string line, out BridgeRequest? request, out string? error)
    {
        request = null;
        error = null;

        if (string.IsNullOrWhiteSpace(line))
        {
            error = "Empty request.";
            return false;
        }

        try
        {
            request = JsonSerializer.Deserialize<BridgeRequest>(line, BridgeProtocol.JsonOptions);
        }
        catch (JsonException ex)
        {
            error = $"Invalid bridge request: {ex.Message}";
            return false;
        }

        if (request is null || string.IsNullOrWhiteSpace(request.Type))
        {
            error = "Bridge request is missing a type.";
            return false;
        }

        return true;
    }
}
