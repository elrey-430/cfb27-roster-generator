namespace RosterGenerator.Core.Legacy;

/// <summary>
/// Reads a PS2-era NCAA Football roster file into teams and players.
///
/// <para>The hard part is not the bits, it is deciding who plays for whom.
/// The player table records no team at all. Squads occupy consecutive runs of
/// player id, and the team table names two captains per side, which places
/// each team in a run — but where two neighbouring squads' runs touch there is
/// no gap to cut on, and a wrongly placed boundary silently moves a dozen
/// players to the wrong school.</para>
///
/// <para>The depth chart settles it. The chart is written in several passes
/// over the league, and within a pass a team lists each (position, depth) slot
/// at most once. So for a candidate boundary: split the chart rows either side
/// of it, group them into runs of consecutive table rows, and count slots used
/// twice inside one run. A boundary in the wrong place drags a player onto a
/// chart that already has somebody in his slot. The boundary with the fewest
/// collisions is the one the game itself is describing — and on both roster
/// files this tool was built against, every boundary resolves to a unique
/// answer with no collisions at all.</para>
/// </summary>
public static class LegacyRosterReader
{
    /// <summary>
    /// Reads a legacy roster file.
    /// </summary>
    /// <param name="path">The roster file.</param>
    /// <param name="schools">Team id to school name, or null to leave schools unresolved.</param>
    public static LegacyRosterFile Read(string path, IReadOnlyDictionary<int, string>? schools = null) =>
        Read(EaDbFile.Read(path), schools);

    /// <summary>Reads a legacy roster file already parsed into tables.</summary>
    public static LegacyRosterFile Read(EaDbFile file, IReadOnlyDictionary<int, string>? schools = null)
    {
        // A PS3-era file records each player's team on the player, so none of
        // the id-run and depth-chart machinery below is needed for it. It is
        // also the richer file by a distance: names as text, ratings on a real
        // 0-99 scale, and twenty-two more attributes.
        if (file.ByteOrder == LegacyByteOrder.Big)
        {
            return ReadModern(file, schools);
        }

        foreach (var required in new[]
                 { LegacySchema.PlayerTable, LegacySchema.TeamTable, LegacySchema.DepthChartTable })
        {
            if (!file.Tables.ContainsKey(required))
            {
                throw new InvalidDataException(
                    $"This file has no '{required}' table, so it is not a roster the tool can read. " +
                    "A roster file carries PLAY, TDYN and DCHT.");
            }
        }

        var play = file.Tables[LegacySchema.PlayerTable];
        var tdyn = file.Tables[LegacySchema.TeamTable];
        var dcht = file.Tables[LegacySchema.DepthChartTable];
        var notes = new List<string>();

        // Every record in use is a player and holds an id, whether or not
        // anyone typed a name on him: NCAA shipped its I-AA squads nameless,
        // and a nameless player still occupies a slot in the id runs the team
        // split depends on. They are dropped later, when a roster is written.
        var playerCount = play.CountUsed(LegacySchema.PlayerId);
        var rowById = new Dictionary<int, int>();
        for (var row = 0; row < playerCount; row++)
        {
            var id = play.Read(row, LegacySchema.PlayerId);
            if (id != 0)
            {
                rowById[id] = row;
            }
        }

        if (rowById.Count == 0)
        {
            throw new InvalidDataException("This roster file holds no players.");
        }

        var ids = rowById.Keys.OrderBy(i => i).ToList();
        var roles = ReadRoles(dcht);
        var teams = ReadTeams(tdyn, rowById.Keys.ToHashSet());
        var runs = FindRuns(ids);
        var assignment = AssignTeams(runs, teams, ids, dcht, notes);

        var result = new List<LegacyTeam>();
        foreach (var (teamId, memberIds) in assignment.OrderBy(a => a.Key))
        {
            var players = memberIds
                .Select(id => ReadPlayer(play, rowById[id], id, roles))
                .ToList();
            schools?.TryGetValue(teamId, out var school);
            var name = schools is not null && schools.TryGetValue(teamId, out var found) ? found : null;
            if (name is null && schools is not null)
            {
                notes.Add($"Team {teamId} has no school in the team id map; it was written as 'Team {teamId}'.");
            }

            result.Add(new LegacyTeam(teamId, name, players));
        }

        return new LegacyRosterFile(result, notes);
    }

    /// <summary>
    /// Reads a PS3-era roster, where the player table says who each player
    /// plays for and the names are plain text.
    /// </summary>
    private static LegacyRosterFile ReadModern(
        EaDbFile file, IReadOnlyDictionary<int, string>? schools)
    {
        if (!file.Tables.TryGetValue(LegacySchema.PlayerTable, out var play))
        {
            throw new InvalidDataException(
                "This file has no 'PLAY' table, so it is not a roster the tool can read.");
        }

        var notes = new List<string>();

        var chart = file.Tables.GetValueOrDefault(LegacySchema.DepthChartTable);
        var roles = chart is null ? new Dictionary<int, string>() : ReadRoles(chart);

        var byTeam = new Dictionary<int, List<LegacyPlayer>>();
        var count = play.CountUsed(LegacySchema.PlayerId);
        for (var row = 0; row < count; row++)
        {
            var id = play.Read(row, LegacySchema.PlayerId);
            var team = play.Has(LegacySchema.PlayerTeamId)
                ? play.Read(row, LegacySchema.PlayerTeamId)
                : 0;
            var attributes = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var map in new[] { LegacySchema.AttributeMap, LegacySchema.ModernAttributeMap })
            {
                foreach (var (field, column) in map)
                {
                    if (play.Has(field))
                    {
                        attributes[column] = play.Read(row, field);
                    }
                }
            }

            var position = play.Read(row, LegacySchema.Position);
            var height = play.Has(LegacySchema.Height) ? play.Read(row, LegacySchema.Height) : 0;
            var jersey = play.Has(LegacySchema.Jersey) ? play.Read(row, LegacySchema.Jersey) : 0;

            byTeam.TryAdd(team, new List<LegacyPlayer>());
            byTeam[team].Add(new LegacyPlayer
            {
                PlayerId = id,
                FirstName = play.Has(LegacySchema.FirstNameText)
                    ? play.ReadText(row, LegacySchema.FirstNameText) : "",
                LastName = play.Has(LegacySchema.LastNameText)
                    ? play.ReadText(row, LegacySchema.LastNameText) : "",
                Position = position >= 0 && position < LegacySchema.Positions.Count
                    ? LegacySchema.Positions[position]
                    : "ATH",
                JerseyNumber = jersey > 0 ? jersey : null,
                HeightInches = height > 0 ? height : null,
                WeightPounds = height > 0
                    ? play.Read(row, LegacySchema.Weight) + LegacySchema.WeightOffsetPounds
                    : null,
                ClassYear = play.Has(LegacySchema.ModernClassYear) &&
                            play.Read(row, LegacySchema.ModernClassYear) is var y &&
                            y >= 0 && y < LegacySchema.ClassYears.Count
                    ? LegacySchema.ClassYears[y]
                    : null,
                // Skin tone is not in this generation's player table -- the
                // face and head assets are, and reading a tone off those would
                // be inference about a real person's appearance.
                SkinTone = null,
                RawOverall = play.Has(LegacySchema.Overall) ? play.Read(row, LegacySchema.Overall) : 0,
                RawAttributes = attributes,
                Role = roles.GetValueOrDefault(id),
            });
        }

        notes.Add(
            $"Read as a PS3-era roster: {byTeam.Values.Sum(v => v.Count)} player(s) across " +
            $"{byTeam.Count} team(s), with each player's team taken from the player table.");
        notes.Add(
            "Skin tone is not read from this generation: its player table carries face and head " +
            "assets rather than a tone, and reading one off those would be inference about a real " +
            "person's appearance.");

        var teams = byTeam
            .OrderBy(t => t.Key)
            .Select(t => new LegacyTeam(
                t.Key,
                schools is not null && schools.TryGetValue(t.Key, out var s) ? s : null,
                t.Value))
            .ToList();
        return new LegacyRosterFile(teams, notes);
    }

    private static LegacyPlayer ReadPlayer(
        LegacyTable play, int row, int id, IReadOnlyDictionary<int, string> roles)
    {
        var positionIndex = play.Read(row, LegacySchema.Position);
        var classIndex = play.Read(row, LegacySchema.ClassYear);
        var attributes = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (field, column) in LegacySchema.AttributeMap)
        {
            if (play.Has(field))
            {
                attributes[column] = play.Read(row, field);
            }
        }

        var jersey = play.Has(LegacySchema.Jersey) ? play.Read(row, LegacySchema.Jersey) : 0;
        var height = play.Has(LegacySchema.Height) ? play.Read(row, LegacySchema.Height) : 0;
        var weight = play.Has(LegacySchema.Weight) ? play.Read(row, LegacySchema.Weight) : 0;

        return new LegacyPlayer
        {
            PlayerId = id,
            FirstName = LegacySchema.DecodeName(play, row, LegacySchema.FirstNameFields),
            LastName = LegacySchema.DecodeName(play, row, LegacySchema.LastNameFields),
            Position = positionIndex >= 0 && positionIndex < LegacySchema.Positions.Count
                ? LegacySchema.Positions[positionIndex]
                : "ATH",
            JerseyNumber = jersey > 0 ? jersey : null,
            HeightInches = height > 0 ? height : null,
            // Zero is a real stored weight and means the lightest the format
            // holds, so it is only missing when the height is missing too.
            WeightPounds = height > 0 ? weight + LegacySchema.WeightOffsetPounds : null,
            ClassYear = classIndex >= 0 && classIndex < LegacySchema.ClassYears.Count
                ? LegacySchema.ClassYears[classIndex]
                : null,
            // The file counts skin tone from zero and CFB27 from one. The tone
            // is carried across rather than left blank because a person chose
            // it deliberately when the roster was made, which is evidence, not
            // a guess at what somebody looked like.
            SkinTone = play.Has(LegacySchema.SkinTone) ? play.Read(row, LegacySchema.SkinTone) + 1 : null,
            RawOverall = play.Has(LegacySchema.Overall) ? play.Read(row, LegacySchema.Overall) : 0,
            RawAttributes = attributes,
            Role = roles.GetValueOrDefault(id),
        };
    }

    /// <summary>
    /// Depth-chart role per player: heading a slot makes a starter, second
    /// makes a backup, anything deeper a reserve. A player the chart never
    /// names is left without a role rather than called a walk-on, because the
    /// chart not mentioning him is an absence of evidence.
    /// </summary>
    private static Dictionary<int, string> ReadRoles(LegacyTable dcht)
    {
        var best = new Dictionary<int, int>();
        var used = dcht.CountUsed(LegacySchema.PlayerId);
        for (var row = 0; row < used; row++)
        {
            var id = dcht.Read(row, LegacySchema.PlayerId);
            if (id == 0)
            {
                continue;
            }

            var depth = dcht.Read(row, LegacySchema.DepthOrder);
            if (!best.TryGetValue(id, out var current) || depth < current)
            {
                best[id] = depth;
            }
        }

        return best.ToDictionary(
            kv => kv.Key,
            kv => kv.Value switch { 0 => "Starter", 1 => "Backup", _ => "Reserve" });
    }

    private sealed record TeamRow(int TeamId, int LowCaptain, int HighCaptain);

    private static List<TeamRow> ReadTeams(LegacyTable tdyn, HashSet<int> playerIds)
    {
        var teams = new List<TeamRow>();
        var used = tdyn.CountUsed(LegacySchema.TeamId);
        for (var row = 0; row < used; row++)
        {
            var id = tdyn.Read(row, LegacySchema.TeamId);
            var defensive = tdyn.Read(row, LegacySchema.DefensiveCaptain);
            var offensive = tdyn.Read(row, LegacySchema.OffensiveCaptain);
            var captains = new[] { defensive, offensive }.Where(playerIds.Contains).ToList();
            if (id == 0 || captains.Count == 0)
            {
                continue;
            }

            teams.Add(new TeamRow(id, captains.Min(), captains.Max()));
        }

        return teams;
    }

    private static List<(int Low, int High)> FindRuns(List<int> ids)
    {
        var runs = new List<(int, int)>();
        var start = ids[0];
        var previous = ids[0];
        foreach (var id in ids.Skip(1))
        {
            if (id - previous > 1)
            {
                runs.Add((start, previous));
                start = id;
            }

            previous = id;
        }

        runs.Add((start, previous));
        return runs;
    }

    private static Dictionary<int, List<int>> AssignTeams(
        List<(int Low, int High)> runs, List<TeamRow> teams, List<int> ids,
        LegacyTable dcht, List<string> notes)
    {
        var chartRows = new List<(int Row, int Id, int Slot)>();
        var used = dcht.CountUsed(LegacySchema.PlayerId);
        for (var row = 0; row < used; row++)
        {
            var id = dcht.Read(row, LegacySchema.PlayerId);
            if (id != 0)
            {
                chartRows.Add((row,
                    id,
                    dcht.Read(row, LegacySchema.Position) * 16 + dcht.Read(row, LegacySchema.DepthOrder)));
            }
        }

        var owners = new Dictionary<int, List<TeamRow>>();
        foreach (var team in teams)
        {
            var index = runs.FindIndex(r => r.Low <= team.LowCaptain && team.LowCaptain <= r.High);
            if (index >= 0)
            {
                owners.TryAdd(index, new List<TeamRow>());
                owners[index].Add(team);
            }
        }

        // A run holding no captain is a squad whose ids happen to be broken by
        // a gap. It belongs to whichever neighbouring run is closer.
        var bounds = runs.ToList();
        foreach (var (low, high) in runs.Select((r, i) => (r, i))
                     .Where(x => !owners.ContainsKey(x.i))
                     .Select(x => x.r)
                     .ToList())
        {
            var index = runs.FindIndex(r => r.Low == low && r.High == high);
            var before = owners.Keys.Where(k => k < index).DefaultIfEmpty(-1).Max();
            var after = owners.Keys.Where(k => k > index).DefaultIfEmpty(-1).Min();
            var pick = after < 0 ? before
                : before < 0 ? after
                : low - bounds[before].High <= bounds[after].Low - high ? before : after;
            if (pick < 0)
            {
                continue;
            }

            bounds[pick] = (Math.Min(bounds[pick].Low, low), Math.Max(bounds[pick].High, high));
        }

        var result = new Dictionary<int, List<int>>();
        foreach (var (index, group) in owners.OrderBy(o => o.Key))
        {
            var (low, high) = bounds[index];
            var span = ids.Where(i => low <= i && i <= high).ToList();
            if (group.Count == 1)
            {
                result[group[0].TeamId] = span;
                continue;
            }

            var ordered = group.OrderBy(t => t.LowCaptain).ToList();
            var edges = new List<int> { low };
            for (var i = 0; i + 1 < ordered.Count; i++)
            {
                var cut = BestCut(chartRows, low, high, ordered[i].HighCaptain, ordered[i + 1].LowCaptain,
                    out var collisions);
                notes.Add(
                    $"Teams {ordered[i].TeamId} and {ordered[i + 1].TeamId} share one block of player ids; " +
                    $"the depth chart puts the boundary at {cut} ({collisions} slot collision(s)).");
                edges.Add(cut);
            }

            edges.Add(high + 1);
            for (var i = 0; i < ordered.Count; i++)
            {
                var (from, to) = (edges[i], edges[i + 1]);
                result[ordered[i].TeamId] = span.Where(id => from <= id && id < to).ToList();
            }
        }

        return result;
    }

    /// <summary>
    /// The boundary between two teams sharing one id run: the one that makes
    /// the fewest players collide with somebody already in their depth-chart
    /// slot. Ties are broken by taking the middle of the tied range.
    /// </summary>
    private static int BestCut(
        IReadOnlyList<(int Row, int Id, int Slot)> chart, int low, int high,
        int lastOfFirst, int firstOfSecond, out int collisions)
    {
        var candidates = new List<int>();
        var best = int.MaxValue;
        for (var cut = lastOfFirst + 1; cut <= firstOfSecond; cut++)
        {
            var score =
                Collisions(chart.Where(r => low <= r.Id && r.Id < cut)) +
                Collisions(chart.Where(r => cut <= r.Id && r.Id <= high));
            if (score < best)
            {
                best = score;
                candidates.Clear();
                candidates.Add(cut);
            }
            else if (score == best)
            {
                candidates.Add(cut);
            }
        }

        collisions = best == int.MaxValue ? 0 : best;
        return candidates.Count > 0 ? candidates[candidates.Count / 2] : firstOfSecond;
    }

    private static int Collisions(IEnumerable<(int Row, int Id, int Slot)> rows)
    {
        var total = 0;
        var seen = new HashSet<int>();
        var previous = int.MinValue;
        foreach (var row in rows)
        {
            if (previous != int.MinValue && row.Row != previous + 1)
            {
                seen.Clear();
            }

            if (!seen.Add(row.Slot))
            {
                total++;
            }

            previous = row.Row;
        }

        return total;
    }
}
