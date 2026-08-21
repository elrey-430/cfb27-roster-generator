using RosterGenerator.Core.Model;
using RosterGenerator.Core.Schema;

namespace RosterGenerator.Core.Legacy;

/// <summary>One CFB27 player written into one PS2 roster slot.</summary>
/// <param name="Name">The player, as CFB27 has him.</param>
/// <param name="Position">The position slot he took over.</param>
/// <param name="Cfb27Overall">His overall in CFB27, 0-99.</param>
/// <param name="WrittenOverall">The overall the PS2 game will show, after the five-bit round trip.</param>
public sealed record LegacyExportedPlayer(
    string Name, string Position, int Cfb27Overall, int WrittenOverall);

/// <summary>One school paired up across the two games.</summary>
/// <param name="School">The school, as both maps name it.</param>
/// <param name="Cfb27TeamIndex">Its CFB27 team index.</param>
/// <param name="LegacyTeamId">Its PS2 team id.</param>
public sealed record LegacyExportTeam(string School, int Cfb27TeamIndex, int LegacyTeamId);

/// <summary>What an export did to one team.</summary>
/// <param name="Team">The PS2 team that was overwritten.</param>
/// <param name="Written">Every player written, in slot order.</param>
/// <param name="Cut">CFB27 players the PS2 squad had no room for, deepest first.</param>
/// <param name="Unfilled">PS2 slots left exactly as they were.</param>
/// <param name="Notes">Names that would not fit, and anything else lost on the way.</param>
/// <param name="DepthChartDecided">
/// True when the dynasty's own depth chart ordered this squad. False means the
/// cut fell back to overall, which is a weaker answer and worth saying out
/// loud rather than leaving the user to assume the coach's chart was read.
/// </param>
public sealed record LegacyExportedTeam(
    string Team,
    IReadOnlyList<LegacyExportedPlayer> Written,
    IReadOnlyList<string> Cut,
    IReadOnlyList<string> Unfilled,
    IReadOnlyList<string> Notes,
    bool DepthChartDecided = false);

/// <summary>What an export did.</summary>
/// <param name="Path">Where the new roster file went.</param>
/// <param name="Team">The PS2 team that was overwritten.</param>
/// <param name="Written">Every player written, in slot order.</param>
/// <param name="Cut">
/// CFB27 players the PS2 squad had no room for, deepest on the depth chart
/// first — the order they were cut in.
/// </param>
/// <param name="Unfilled">
/// PS2 slots left exactly as they were because CFB27 had nobody for that
/// position. Left alone rather than filled with somebody from elsewhere: a
/// punter in a guard's slot is worse than the guard who was already there.
/// </param>
/// <param name="Notes">Names that would not fit, and anything else lost on the way.</param>
public sealed record LegacyExportResult(
    string Path,
    string Team,
    IReadOnlyList<LegacyExportedPlayer> Written,
    IReadOnlyList<string> Cut,
    IReadOnlyList<string> Unfilled,
    IReadOnlyList<string> Notes)
{
    /// <summary>
    /// Every team the run wrote, in the order it wrote them. A one-team export
    /// has a list of one, so a caller that only ever does one team needs no
    /// changes.
    /// </summary>
    public IReadOnlyList<LegacyExportedTeam> Teams { get; init; } = Array.Empty<LegacyExportedTeam>();

    /// <summary>Teams asked for that the file had no squad for, and why.</summary>
    public IReadOnlyList<string> Skipped { get; init; } = Array.Empty<string>();

    /// <summary>
    /// True when what was written is a PS2 memory-card save rather than a bare
    /// roster file — which is to say, something that goes straight onto a
    /// memory card with no database editor in between.
    /// </summary>
    public bool WroteSave { get; init; }

    /// <summary>What the source was, in a sentence, for the report.</summary>
    public string SourceDescription { get; init; } = "";

    /// <summary>
    /// Where the roster was also written on its own, when that was asked for.
    /// </summary>
    public string? DatabasePath { get; init; }
}

/// <summary>
/// Writes a CFB27 team into a PS2-era roster file, over the squad already
/// there.
///
/// <para><b>Over, never into.</b> A PS2 roster records no team on a player —
/// a squad IS its block of player ids — so there is no way to add a team, and
/// no need to: every slot that gets overwritten keeps its id, which keeps the
/// depth chart, the captains and the team's own structure valid. The file that
/// comes out differs from the one that went in only in the fields of one
/// team's players.</para>
///
/// <para><b>The depth chart decides who survives.</b> CFB27 carries 85 players
/// and a PS2 squad holds around 69, so a sixth of the roster cannot come. The
/// cut is made position by position: the PS2 team's own slots say how many
/// quarterbacks and how many guards it has room for, and CFB27's depth chart
/// says which ones. Nobody changes position on the way across, so the squad
/// that arrives is playable — cutting on overall alone would fill a team with
/// the best 69 players and leave it without a punter.</para>
///
/// <para><b>Ratings go through the measured scale.</b> That generation holds a
/// rating in five bits; see <see cref="LegacyRatingScale"/>. Eighteen of
/// CFB27's fifty-seven have any counterpart, and the rest simply do not exist
/// there.</para>
/// </summary>
public static class LegacyRosterExporter
{
    /// <summary>CFB27 rating column to the PS2 field it becomes.</summary>
    private static readonly IReadOnlyList<(string Field, string Column)> Ratings =
        LegacySchema.AttributeMap.Select(kv => (kv.Key, kv.Value)).ToList();

    /// <summary>
    /// Writes one CFB27 team over one PS2 team.
    /// </summary>
    /// <param name="sourcePath">The PS2 roster file to start from. Never modified.</param>
    /// <param name="outputPath">Where the new roster file goes.</param>
    /// <param name="roster">The CFB27 player table.</param>
    /// <param name="teamIndex">The CFB27 team to take players from.</param>
    /// <param name="legacyTeamId">The PS2 team id to write over.</param>
    /// <param name="teamName">What to call that team in the report.</param>
    /// <param name="scale">The measured five-bit rating scale.</param>
    /// <param name="depthChart">
    /// The dynasty's own depth chart, asked for a team at a time. See the
    /// multi-team overload.
    /// </param>
    public static LegacyExportResult Export(
        string sourcePath,
        string outputPath,
        PlayerRoster roster,
        int teamIndex,
        int legacyTeamId,
        string teamName,
        LegacyRatingScale scale,
        Func<int, IReadOnlyDictionary<string, IReadOnlyList<int>>?>? depthChart = null) =>
        Export(sourcePath, outputPath, roster,
            new[] { new LegacyExportTeam(teamName, teamIndex, legacyTeamId) }, scale, depthChart);

    /// <summary>
    /// Writes any number of CFB27 teams into one PS2 roster file.
    ///
    /// <para>Every team goes into the same file and the file is saved once, so
    /// a whole-league export costs one read and one write rather than a
    /// hundred. A team the file has no squad for is skipped and named instead
    /// of failing the run — one unmapped school should not cost somebody the
    /// other hundred and eighteen.</para>
    /// </summary>
    /// <param name="sourcePath">The PS2 roster file to start from. Never modified.</param>
    /// <param name="outputPath">Where the new roster file goes.</param>
    /// <param name="roster">The CFB27 player table.</param>
    /// <param name="teams">The schools to write, paired across the two games.</param>
    /// <param name="scale">The measured five-bit rating scale.</param>
    /// <param name="depthChart">
    /// The dynasty's own depth chart, asked for one CFB27 team index at a time:
    /// position → the player row keys listed there, starter first. This is what
    /// decides who comes, so it is worth supplying — without it the cut falls
    /// back to overall, which is a different answer whenever a coach starts
    /// somebody the numbers do not. Positions it does not mention, and teams it
    /// has no chart for, fall back to overall.
    /// </param>
    public static LegacyExportResult Export(
        string sourcePath,
        string outputPath,
        PlayerRoster roster,
        IReadOnlyList<LegacyExportTeam> teams,
        LegacyRatingScale scale,
        Func<int, IReadOnlyDictionary<string, IReadOnlyList<int>>?>? depthChart = null,
        string? databaseOutputPath = null)
    {
        // Either a bare roster file or the memory-card save it lives inside;
        // whichever it is, that is what comes back out.
        var source = LegacyRosterSource.Open(sourcePath);
        var file = source.Database;
        if (file.ByteOrder != LegacyByteOrder.Little)
        {
            throw new InvalidDataException(
                "Only a PS2-era roster can be written to. This file is from the PS3 generation, whose " +
                "checksums have not been worked out.");
        }

        if (teams.Count == 0)
        {
            throw new ArgumentException("No teams to write.", nameof(teams));
        }

        var play = file.Tables[LegacySchema.PlayerTable];

        // Materialised once: PlayerRoster.Players filters empty slots on
        // every enumeration, and the indexes below have to mean one thing.
        var players = roster.Players.ToList();

        // Read the squads once too. Working out who plays for whom on a PS2
        // file means id runs and depth-chart collisions, and doing it per team
        // would repeat that hundred-team job a hundred times.
        var squads = SquadsOf(file);
        var done = new List<LegacyExportedTeam>();
        var skipped = new List<string>();

        foreach (var team in teams)
        {
            if (!squads.TryGetValue(team.LegacyTeamId, out var slots) || slots.Count == 0)
            {
                skipped.Add(
                    $"{team.School}: team {team.LegacyTeamId} has no players in " +
                    $"'{Path.GetFileName(sourcePath)}', so there was nothing to write over.");
                continue;
            }

            done.Add(WriteTeam(
                play, team, slots, players, scale, depthChart?.Invoke(team.Cfb27TeamIndex)));
        }

        if (done.Count == 0)
        {
            throw new InvalidDataException(
                $"None of the {teams.Count} team(s) asked for could be written. " + string.Join(" ", skipped));
        }

        source.Save(outputPath, readFrom: sourcePath);

        // The roster on its own as well, when asked for: somebody who wants to
        // look the result over in a database editor before putting a memory
        // card near it should not have to run the export twice.
        if (databaseOutputPath is { Length: > 0 })
        {
            source.SaveDatabaseOnly(databaseOutputPath, readFrom: sourcePath);
        }

        var first = done[0];
        return new LegacyExportResult(
            outputPath, first.Team, first.Written, first.Cut, first.Unfilled, first.Notes)
        {
            Teams = done,
            Skipped = skipped,
            WroteSave = source.InSave,
            SourceDescription = source.Describe(),
            DatabasePath = databaseOutputPath is { Length: > 0 } ? databaseOutputPath : null,
        };
    }

    private static LegacyExportedTeam WriteTeam(
        LegacyTable play,
        LegacyExportTeam team,
        IReadOnlyDictionary<string, List<int>> slots,
        IReadOnlyList<Player> players,
        LegacyRatingScale scale,
        IReadOnlyDictionary<string, IReadOnlyList<int>>? depthOrder)
    {
        var byPosition = Candidates(players, team.Cfb27TeamIndex, depthOrder);
        var written = new List<LegacyExportedPlayer>();
        var unfilled = new List<string>();
        var notes = new List<string>();
        var taken = new HashSet<int>();

        // Slot order within a position is the PS2 team's own depth: its
        // starter takes CFB27's starter.
        foreach (var (position, positionSlots) in slots.OrderBy(s => s.Key, StringComparer.Ordinal))
        {
            var candidates = byPosition.GetValueOrDefault(position, Array.Empty<int>());
            for (var i = 0; i < positionSlots.Count; i++)
            {
                if (i >= candidates.Count)
                {
                    unfilled.Add($"{position} #{i + 1}");
                    continue;
                }

                var player = players[candidates[i]];
                taken.Add(candidates[i]);
                WritePlayer(play, positionSlots[i], player, scale, notes);
                var overall = Number(player, PlayerColumns.OverallRating);
                written.Add(new LegacyExportedPlayer(
                    $"{player.GetRaw(PlayerColumns.FirstName)} {player.GetRaw(PlayerColumns.LastName)}".Trim(),
                    position, overall, scale.ToDisplayed(scale.ToStored(overall))));
            }
        }

        // Everyone the squad had no room for, deepest first — the order they
        // were cut in, which is what somebody checking the result wants.
        var cut = byPosition
            .SelectMany(p => p.Value.Select((row, depth) => (Row: row, p.Key, Depth: depth)))
            .Where(x => !taken.Contains(x.Row))
            .OrderByDescending(x => x.Depth)
            .ThenBy(x => x.Key, StringComparer.Ordinal)
            .Select(x =>
            {
                var player = players[x.Row];
                return $"{player.GetRaw(PlayerColumns.FirstName)} {player.GetRaw(PlayerColumns.LastName)} " +
                       $"({x.Key}, {Number(player, PlayerColumns.OverallRating)} OVR, {Ordinal(x.Depth + 1)} on the chart)";
            })
            .ToList();

        return new LegacyExportedTeam(
            team.School, written, cut, unfilled, notes, depthOrder is { Count: > 0 });
    }

    private static string Ordinal(int n) => n switch
    {
        1 => "1st", 2 => "2nd", 3 => "3rd", _ => $"{n}th",
    };

    /// <summary>
    /// A column read as a number, or 0 when the export does not carry it.
    ///
    /// <para>Tolerant on purpose: an export written by a different version of
    /// the community tool may be missing a column, and losing a jersey number
    /// is not a reason to refuse somebody's whole roster.</para>
    /// </summary>
    private static int Number(Player player, string column) =>
        player.HasColumn(column) && int.TryParse(player.GetRaw(column), out var value) ? value : 0;

    /// <summary>
    /// The PS2 team's slots, grouped by the position each one currently holds.
    ///
    /// <para>Grouped by what the slot IS rather than reassigned: keeping every
    /// player at the position his slot already played is what makes the result
    /// a football team rather than a list of the best available men.</para>
    /// </summary>
    private static Dictionary<int, Dictionary<string, List<int>>> SquadsOf(EaDbFile file)
    {
        var roster = LegacyRosterReader.Read(file);
        var play = file.Tables[LegacySchema.PlayerTable];
        var rowById = new Dictionary<int, int>();
        for (var row = 0; row < play.CountUsed(LegacySchema.PlayerId); row++)
        {
            rowById[play.Read(row, LegacySchema.PlayerId)] = row;
        }

        var squads = new Dictionary<int, Dictionary<string, List<int>>>();
        foreach (var team in roster.Teams)
        {
            var slots = new Dictionary<string, List<int>>(StringComparer.Ordinal);

            // Best first inside a position, so the PS2 starter is overwritten
            // by the CFB27 starter rather than by whoever is listed first.
            foreach (var player in team.Players.OrderByDescending(p => p.RawOverall))
            {
                if (rowById.TryGetValue(player.PlayerId, out var row))
                {
                    slots.TryAdd(player.Position, new List<int>());
                    slots[player.Position].Add(row);
                }
            }

            squads[team.TeamId] = slots;
        }

        return squads;
    }

    /// <summary>
    /// CFB27's players for this team, per position, best first.
    ///
    /// <para>The depth chart decides the order where the dynasty carries one,
    /// because that is the coach's own answer to who plays. Overall is the
    /// fallback and the tie-break.</para>
    /// </summary>
    private static Dictionary<string, IReadOnlyList<int>> Candidates(
        IReadOnlyList<Player> players, int teamIndex,
        IReadOnlyDictionary<string, IReadOnlyList<int>>? depthOrder)
    {
        // The chart names players by row key, which is the only identifier the
        // two tables share; the indexes below are into the filtered list.
        var depthByRowKey = depthOrder?.ToDictionary(
            p => p.Key,
            p => p.Value.Select((rowKey, depth) => (rowKey, depth))
                .GroupBy(x => x.rowKey)
                .ToDictionary(g => g.Key, g => g.Min(x => x.depth)),
            StringComparer.Ordinal);

        var byPosition = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        for (var row = 0; row < players.Count; row++)
        {
            var player = players[row];
            if (Number(player, PlayerColumns.TeamIndex) != teamIndex)
            {
                continue;
            }

            var position = player.GetRaw(PlayerColumns.Position);
            if (position.Length == 0)
            {
                continue;
            }

            byPosition.TryAdd(position, new List<int>());
            byPosition[position].Add(row);
        }

        return byPosition.ToDictionary(
            p => p.Key,
            p =>
            {
                var chart = depthByRowKey?.GetValueOrDefault(p.Key);
                var ranked = p.Value
                    .OrderBy(row => chart is not null &&
                                    chart.TryGetValue(players[row].RowKey, out var depth)
                        ? depth
                        : int.MaxValue)
                    .ThenByDescending(row => Number(players[row], PlayerColumns.OverallRating))
                    .ToList();
                return (IReadOnlyList<int>)ranked;
            },
            StringComparer.Ordinal);
    }

    private static void WritePlayer(
        LegacyTable play, int row, Player player, LegacyRatingScale scale, List<string> notes)
    {
        var who = $"{player.GetRaw(PlayerColumns.FirstName)} {player.GetRaw(PlayerColumns.LastName)}".Trim();

        if (LegacySchema.EncodeName(play, row, LegacySchema.FirstNameFields, player.GetRaw(PlayerColumns.FirstName))
            is string first)
        {
            notes.Add($"{who}: {first}");
        }

        if (LegacySchema.EncodeName(play, row, LegacySchema.LastNameFields, player.GetRaw(PlayerColumns.LastName))
            is string last)
        {
            notes.Add($"{who}: {last}");
        }

        void Put(string field, int value)
        {
            if (play.Has(field))
            {
                play.Write(row, field, Math.Clamp(value, 0, (1 << play.ByName[field].Bits) - 1));
            }
        }

        Put(LegacySchema.Jersey, Number(player, PlayerColumns.JerseyNum));
        Put(LegacySchema.Height, Number(player, PlayerColumns.Height));
        // CFB27 stores weight the same way this format does -- pounds over 160 --
        // so it crosses straight over rather than being converted twice.
        Put(LegacySchema.Weight, Number(player, PlayerColumns.Weight));

        var year = LegacySchema.ClassYears.ToList().FindIndex(
            y => player.GetRaw(PlayerColumns.SchoolYear).Contains(y, StringComparison.OrdinalIgnoreCase));
        if (year >= 0)
        {
            Put(LegacySchema.ClassYear, year);
        }

        Put(LegacySchema.Overall, scale.ToStored(Number(player, PlayerColumns.OverallRating)));
        foreach (var (field, column) in Ratings)
        {
            Put(field, scale.ToStored(Number(player, column)));
        }
    }
}
