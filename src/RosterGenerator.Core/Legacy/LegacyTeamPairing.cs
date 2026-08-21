using System.Text.Json;
using RosterGenerator.Core.Mapping;
using RosterGenerator.Core.Schema;

namespace RosterGenerator.Core.Legacy;

/// <summary>
/// Pairs a school across the two games, so a user names a school and never a
/// number.
///
/// <para>Both halves are already in the project and both were built by hand
/// against real files: <c>data/LegacyTeamIds.json</c> says which PS2 team id
/// each school is, and <c>data/TeamMappings.json</c> says which CFB27 team
/// index it is, aliases included. This joins them on the school name, which is
/// the only thing the two have in common.</para>
///
/// <para>A school in one and not the other is reported rather than guessed at.
/// The PS2 files carry 119 schools and CFB27 ships 136, so they genuinely
/// disagree — Idaho left the FBS, and half a dozen schools joined it after
/// 2004.</para>
/// </summary>
public static class LegacyTeamPairing
{
    /// <summary>
    /// Every school both maps know, in PS2 team id order.
    /// </summary>
    /// <param name="legacyTeamIdsPath">Path to <c>LegacyTeamIds.json</c>.</param>
    /// <param name="teams">The CFB27 team mappings.</param>
    /// <param name="unpaired">Schools one map has and the other does not.</param>
    public static IReadOnlyList<LegacyExportTeam> Pair(
        string legacyTeamIdsPath, TeamMappingSet teams, out IReadOnlyList<string> unpaired)
    {
        var paired = new List<LegacyExportTeam>();
        var missing = new List<string>();
        foreach (var (legacyTeamId, school) in ReadLegacyTeams(legacyTeamIdsPath))
        {
            if (!teams.TryResolve(school, out var teamIndex))
            {
                missing.Add(
                    $"{school} (PS2 team {legacyTeamId}) has no CFB27 team in TeamMappings.json, so it " +
                    "keeps the squad it had.");
                continue;
            }

            if (teamIndex == PlayerSchema.NoTeamSentinel)
            {
                // A school CFB27 does not carry is mapped onto a stand-in at
                // the no-team index so it can be GENERATED INTO. Reading OUT
                // of that index is the opposite thing: it is where the game
                // parks 4,527 recruits and five generic FCS squads, and none
                // of them are this school's players. Idaho is the live case.
                missing.Add(
                    $"{school} (PS2 team {legacyTeamId}) is not a team CFB27 carries — it only has a " +
                    "stand-in to be generated into — so there is nobody to take out of it. It keeps the " +
                    "squad it had.");
                continue;
            }

            paired.Add(new LegacyExportTeam(school, teamIndex, legacyTeamId));
        }

        unpaired = missing;
        return paired;
    }

    /// <summary>
    /// The pairing for one named school, or null when either map lacks it.
    /// </summary>
    public static LegacyExportTeam? Find(
        string legacyTeamIdsPath, TeamMappingSet teams, string school)
    {
        var wanted = Normalize(school);
        foreach (var (legacyTeamId, name) in ReadLegacyTeams(legacyTeamIdsPath))
        {
            if (Normalize(name) == wanted && teams.TryResolve(name, out var teamIndex) &&
                teamIndex != PlayerSchema.NoTeamSentinel)
            {
                return new LegacyExportTeam(name, teamIndex, legacyTeamId);
            }
        }

        // The school may be named differently in the two files — "Miami (FL)"
        // against "Miami" — so fall back to whatever CFB27 calls it and look
        // the PS2 side up through the same alias list.
        if (!teams.TryResolve(school, out var resolved) || resolved == PlayerSchema.NoTeamSentinel)
        {
            return null;
        }

        foreach (var (legacyTeamId, name) in ReadLegacyTeams(legacyTeamIdsPath))
        {
            if (teams.TryResolve(name, out var other) && other == resolved)
            {
                return new LegacyExportTeam(name, resolved, legacyTeamId);
            }
        }

        return null;
    }

    /// <summary>School names the PS2 map knows, for a picker.</summary>
    public static IReadOnlyList<string> Schools(string legacyTeamIdsPath) =>
        ReadLegacyTeams(legacyTeamIdsPath).Select(t => t.School)
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();

    private static string Normalize(string name) =>
        new(name.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static IReadOnlyList<(int TeamId, string School)> ReadLegacyTeams(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty("teams", out var teams))
        {
            throw new InvalidDataException($"'{path}' has no 'teams' array.");
        }

        return teams.EnumerateArray()
            .Where(t => t.TryGetProperty("teamId", out _) && t.TryGetProperty("school", out _))
            .Select(t => (t.GetProperty("teamId").GetInt32(), t.GetProperty("school").GetString() ?? ""))
            .Where(t => t.Item2.Length > 0)
            .OrderBy(t => t.Item1)
            .ToList();
    }
}
