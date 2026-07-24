using System.Text.Json;
using System.Text.Json.Serialization;

namespace RosterGenerator.Core.Historical;

/// <summary>
/// A complete historical roster for one school and season, loaded from a
/// JSON dataset (e.g. <c>HistoricalData/2023/FloridaState.json</c>).
/// </summary>
public sealed record HistoricalRoster
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    /// <summary>Season year (e.g. 2023). Required.</summary>
    public required int Season { get; init; }

    /// <summary>School name as used in <c>TeamMappings.json</c>. Required.</summary>
    public required string School { get; init; }

    /// <summary>Where the data came from. Optional but strongly encouraged.</summary>
    public string? Source { get; init; }

    /// <summary>Dataset-wide caveats. Optional.</summary>
    public string? Notes { get; init; }

    /// <summary>All players on the roster.</summary>
    public required IReadOnlyList<HistoricalPlayer> Players { get; init; }

    /// <summary>Loads a roster dataset from a JSON file.</summary>
    public static HistoricalRoster Load(string path)
    {
        var roster = JsonSerializer.Deserialize<HistoricalRoster>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException($"'{path}' does not contain a historical roster.");
        if (roster.Players.Count == 0)
        {
            throw new InvalidDataException($"'{path}' contains no players.");
        }

        return roster;
    }

    /// <summary>Serializes the roster back to JSON (used by tooling/tests).</summary>
    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);
}
