using RosterGenerator.Core.Csv;

namespace RosterGenerator.Core.Historical;

/// <summary>The outcome of reading a user-facing historical roster CSV.</summary>
/// <param name="Roster">The parsed roster (rows with fatal problems excluded).</param>
/// <param name="Warnings">
/// Values that could NOT be used as written, phrased for end users — a
/// dropped row, an unreadable number, a misaligned row.
/// </param>
/// <param name="Corrections">
/// Values that WERE used after being cleaned up ("#13" read as 13). Kept
/// apart from <paramref name="Warnings"/> because a fixed value is not a
/// problem, and burying it among real ones trains people to ignore both.
/// </param>
public sealed record HistoricalCsvResult(
    HistoricalRoster Roster,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Corrections)
{
    /// <summary>Creates a result with no corrections.</summary>
    public HistoricalCsvResult(HistoricalRoster roster, IReadOnlyList<string> warnings)
        : this(roster, warnings, Array.Empty<string>())
    {
    }

    /// <summary>
    /// Every team the file describes, in the order they first appear.
    ///
    /// <para>One file can carry a whole season: a blank template written by
    /// <c>template --season</c> has 85 rows for each of that year's teams, and
    /// a filled one comes back the same shape. A file naming one team gives a
    /// list of one, so nothing that worked before behaves differently.</para>
    /// </summary>
    public IReadOnlyList<HistoricalRoster> Rosters { get; init; } = Array.Empty<HistoricalRoster>();

    /// <summary>True when the file describes more than one team.</summary>
    public bool IsMultiTeam => Rosters.Count > 1;
}

/// <summary>
/// Reads the simple, user-facing historical roster CSV format
/// (documented in <c>docs/Historical_CSV_Format.md</c>):
///
/// <code>
/// FirstName,LastName,Position,Number,Height,Weight,Class,Team,Season[,Hometown,PreviousSchool,Notes]
/// </code>
///
/// Users supply real-world values — positions like "Tailback", heights like
/// "6-2" or "74", weights in pounds, classes like "RS Junior" — and never
/// need to know CFB27 internals. Headers are case-insensitive and column
/// order does not matter; only FirstName, LastName and Position are
/// required per row. Team/Season may come from the file or be supplied by
/// the caller (the caller's choice wins).
/// </summary>
public static class HistoricalCsv
{
    /// <summary>
    /// Reads a simple historical roster CSV.
    /// </summary>
    /// <param name="path">The CSV file path.</param>
    /// <param name="school">
    /// The school to use for rows whose <c>Team</c> cell is empty — a fallback,
    /// not an override. A row that names its own team always goes to that team,
    /// so a file covering a whole season is read as a whole season no matter
    /// what the caller passes.
    /// </param>
    /// <param name="season">
    /// Season override. When null, the file's Season column is used when
    /// present (defaulting to 0 = unknown).
    /// </param>
    public static HistoricalCsvResult Read(string path, string? school = null, int? season = null)
    {
        // A person's roster CSV is not a machine-written table: rows that stop
        // early are ordinary, and must not be treated as a corrupt file.
        var document = CsvDocument.Parse(File.ReadAllText(path), CsvDocument.RaggedRows.Pad);
        var warnings = new List<string>();
        var corrections = new List<string>();

        // A row of the wrong width usually means a stray or missing comma, and
        // the silent fix would hide a header the user has misaligned.
        foreach (var (row, fields) in document.RaggedRowsAdjusted)
        {
            warnings.Add(fields < document.Header.Count
                ? $"Row {row + 1} has only {fields} of {document.Header.Count} columns; the rest were " +
                  "treated as blank. Check for a missing comma if that is not what you meant."
                : $"Row {row + 1} has {fields} columns but the header has {document.Header.Count}; the " +
                  "extra values were ignored. Check for a stray comma.");
        }

        var byKey = document.Header
            .Select((name, index) => (Key: Normalize(name), Name: name, Index: index))
            .Where(c => c.Key.Length > 0)
            .GroupBy(c => c.Key)
            .ToList();

        foreach (var duplicate in byKey.Where(g => g.Count() > 1))
        {
            warnings.Add(
                $"The column '{duplicate.First().Name}' appears {duplicate.Count()} times; only the first " +
                "is used.");
        }

        var columns = byKey.ToDictionary(g => g.Key, g => g.First().Index, StringComparer.Ordinal);

        // Column names the template used to use. Renaming a column in the
        // template must never break a file somebody has already filled in, so
        // the old name keeps reading the same cell for good.
        foreach (var (current, legacy) in new[] { ("heightinches", "height") })
        {
            if (!columns.ContainsKey(current) && columns.TryGetValue(legacy, out var index))
            {
                columns[current] = index;
            }
        }

        foreach (var required in new[] { "firstname", "lastname", "position" })
        {
            if (!columns.ContainsKey(required))
            {
                throw new CsvSchemaException(
                    $"The historical roster CSV is missing the required '{required}' column. " +
                    "Required columns: FirstName, LastName, Position. See docs/Historical_CSV_Format.md.");
            }
        }

        string Cell(int row, string key) =>
            columns.TryGetValue(key, out var index) ? document.GetCell(row, document.Header[index]).Trim() : "";

        // Grouped by team so one file can carry a whole season. Insertion
        // order is kept, so the report reads in the order the user typed.
        var byTeam = new Dictionary<string, List<HistoricalPlayer>>(StringComparer.OrdinalIgnoreCase);
        var teamOrder = new List<string>();
        var seasonByTeam = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var players = new List<HistoricalPlayer>();
        string? fileSchool = null;
        var fileSeason = 0;
        for (var row = 0; row < document.RowCount; row++)
        {
            var rowLabel = $"row {row + 2}"; // 1-based file line, after the header
            var firstName = Cell(row, "firstname");
            var lastName = Cell(row, "lastname");
            var position = Cell(row, "position");
            if (firstName.Length == 0 && lastName.Length == 0 && position.Length == 0)
            {
                continue; // fully blank line
            }

            if (firstName.Length == 0 || lastName.Length == 0 || position.Length == 0)
            {
                warnings.Add($"{rowLabel}: skipped — FirstName, LastName and Position are required.");
                continue;
            }

            var teamValue = Cell(row, "team");
            if (school is null && teamValue.Length > 0 && fileSchool is null)
            {
                fileSchool = teamValue;
            }

            // A row's own Season is kept on the player as well as being folded
            // into the roster's. They are different questions on an all-time
            // file: the roster has one season and the player has theirs, and
            // it is the player's that decides what they are wearing.
            var seasonValue = Cell(row, "season");
            int? playerSeason = int.TryParse(seasonValue, out var parsedSeason) && parsedSeason > 0
                ? parsedSeason
                : null;
            if (season is null && playerSeason is int fromRow)
            {
                fileSeason = fromRow;
            }

            var player = new HistoricalPlayer
            {
                // An explicit override means "treat the whole file as this
                // season", so it wins over anything a row says rather than
                // leaving half the roster following the column.
                Season = season is null ? playerSeason : null,
                FirstName = firstName,
                LastName = lastName,
                Position = position,
                JerseyNumber = ParseInt(Cell(row, "number"), rowLabel, "Number", warnings, corrections),
                HeightInches = ParseHeight(Cell(row, "heightinches"), rowLabel, warnings, corrections),
                WeightPounds = ParseInt(Cell(row, "weight"), rowLabel, "Weight", warnings, corrections),
                ClassYear = NullIfEmpty(Cell(row, "class")),
                Hometown = NullIfEmpty(Cell(row, "hometown")),
                PreviousSchool = NullIfEmpty(Cell(row, "previousschool")),
                Notes = NullIfEmpty(Cell(row, "notes")),
                SkinTone = ReadSkinTone(Cell(row, "skintone"), rowLabel, warnings),
                Evidence = ReadEvidence(Cell, row, rowLabel, warnings, corrections),
            };
            players.Add(player);

            // A row's own Team decides where that player goes. The caller's
            // team, then the file's first Team, only stand in for a row that
            // does not say.
            //
            // This used to be the other way round — an explicit team won over
            // every row — which quietly collapsed a whole-season file onto one
            // school. The desktop app sends the team it detected on every run,
            // so a 119-team file generated 10,115 players onto whichever team
            // happened to be listed first, with nothing reported.
            var owner = (teamValue.Length > 0 ? teamValue : null) ?? school ?? fileSchool;
            if (owner is null)
            {
                continue;
            }

            if (!byTeam.TryGetValue(owner, out var roster))
            {
                roster = new List<HistoricalPlayer>();
                byTeam[owner] = roster;
                teamOrder.Add(owner);
            }

            roster.Add(player);
            if (playerSeason is int rowSeason && !seasonByTeam.ContainsKey(owner))
            {
                seasonByTeam[owner] = rowSeason;
            }
        }

        // Generating from an empty roster silently produces a team of 85
        // replacements and none of the user's players — a file that looks
        // right and contains nothing they typed.
        if (players.Count == 0)
        {
            throw new CsvSchemaException(
                $"'{Path.GetFileName(path)}' has a header but no usable player rows. Every row needs at " +
                "least a FirstName, a LastName and a Position. See docs/Historical_CSV_Format.md.");
        }

        var resolvedSchool = school ?? fileSchool
            ?? throw new CsvSchemaException(
                "No team was selected and the CSV has no Team column — add a Team column or pass the team " +
                "explicitly.");

        var source = $"Simple historical CSV: {Path.GetFileName(path)}";
        var rosters = teamOrder
            .Select(team => new HistoricalRoster
            {
                Season = season ?? (seasonByTeam.TryGetValue(team, out var own) ? own : fileSeason),
                School = team,
                Source = source,
                Players = byTeam[team],
            })
            .ToList();

        // The single-roster view keeps every existing caller working. When the
        // file names one team it is that team; when the caller asked for one it
        // is theirs; only a multi-team file makes the distinction matter, and
        // callers that care read Rosters instead.
        var primary = rosters.FirstOrDefault(r => Normalize(r.School) == Normalize(resolvedSchool))
            ?? rosters.FirstOrDefault()
            ?? new HistoricalRoster
            {
                Season = season ?? fileSeason,
                School = resolvedSchool,
                Source = source,
                Players = players,
            };

        return new HistoricalCsvResult(primary, warnings, corrections) { Rosters = rosters };
    }

    /// <summary>
    /// Reads the optional rating-evidence columns. All are additive to the
    /// golden-standard template: a file with none of them still parses, and
    /// any stat named in <see cref="StatKeys"/> is picked up automatically,
    /// so new statistics need no code change here.
    /// </summary>
    private static RatingEvidence ReadEvidence(
        Func<int, string, string> cell, int row, string rowLabel, List<string> warnings,
        List<string> corrections)
    {
        var stats = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in StatColumnNames)
        {
            var raw = cell(row, Normalize(key));
            if (raw.Length == 0)
            {
                continue;
            }

            if (double.TryParse(raw, out var value))
            {
                stats[key] = value;
            }
            else
            {
                warnings.Add($"{rowLabel}: {key} '{raw}' is not a number — ignored.");
            }
        }

        var awards = cell(row, "awards")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        // Accepted under two spellings because both read naturally in a
        // spreadsheet header and neither is obviously the right one.
        var contenderCell = cell(row, "awardcontender");
        if (contenderCell.Length == 0)
        {
            contenderCell = cell(row, "awardfinalist");
        }

        var awardContender = contenderCell
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        var draftPickText = cell(row, "draftpick");
        var undrafted = draftPickText.Equals("UDFA", StringComparison.OrdinalIgnoreCase) ||
                        draftPickText.Equals("Undrafted", StringComparison.OrdinalIgnoreCase);

        // Columns an import writes and nobody has to fill in by hand. They
        // hold places in an order rather than ratings — see
        // LegacyRosterImporter — so they read as plain numbers.
        var legacyShape = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var rating in Legacy.LegacySchema.AttributeMap.Values)
        {
            var column = Normalize(Legacy.LegacyRosterImporter.ColumnFor(rating));
            if (ParseDouble(cell(row, column), rowLabel, column, warnings, corrections) is double place)
            {
                legacyShape[rating] = place;
            }
        }

        return new RatingEvidence
        {
            LegacyRankPercentile =
                ParseDouble(cell(row, "legacyrank"), rowLabel, "LegacyRank", warnings, corrections),
            LegacyRatingPercentiles = legacyShape,
            Role = NullIfEmpty(cell(row, "role")),
            StarRating = ParseInt(cell(row, "starrating"), rowLabel, "StarRating", warnings, corrections),
            FortyYardDash = ParseDouble(cell(row, "forty"), rowLabel, "Forty", warnings, corrections),
            BenchPressReps = ParseInt(cell(row, "bench"), rowLabel, "Bench", warnings, corrections),
            VerticalJumpInches = ParseDouble(cell(row, "vertical"), rowLabel, "Vertical", warnings, corrections),
            ShuttleSeconds = ParseDouble(cell(row, "shuttle"), rowLabel, "Shuttle", warnings, corrections),
            ThreeConeSeconds = ParseDouble(cell(row, "threecone"), rowLabel, "ThreeCone", warnings, corrections),
            DraftPickOverall = undrafted ? null : ParseInt(draftPickText, rowLabel, "DraftPick", warnings, corrections),
            DraftRound = ParseInt(cell(row, "draftround"), rowLabel, "DraftRound", warnings, corrections),
            UndraftedFreeAgent = undrafted,
            Awards = awards,
            AwardContender = awardContender,
            Stats = stats,
        };
    }

    /// <summary>Stat columns recognized in the input CSV.</summary>
    private static readonly string[] StatColumnNames =
    {
        StatKeys.PassYards, StatKeys.PassTD, StatKeys.PassInt, StatKeys.Completions, StatKeys.Attempts,
        StatKeys.CompletionPct, StatKeys.RushYards, StatKeys.RushTD, StatKeys.RushAttempts,
        StatKeys.YardsPerCarry, StatKeys.RecYards, StatKeys.RecTD, StatKeys.Receptions,
        StatKeys.Tackles, StatKeys.Sacks, StatKeys.TacklesForLoss, StatKeys.Interceptions,
        StatKeys.PassesDefended, StatKeys.ForcedFumbles, StatKeys.FieldGoalsMade,
        StatKeys.FieldGoalsAttempted, StatKeys.FieldGoalPct, StatKeys.LongFieldGoal,
        StatKeys.PuntAverage, StatKeys.GamesPlayed, StatKeys.GamesStarted,
    };

    /// <summary>
    /// Strips the decoration people and spreadsheets put around numbers —
    /// <c>#13</c>, <c>212 lbs</c>, <c>4.49s</c>, <c>1,250</c>, <c>21 reps</c>
    /// — leaving the digits. Copying a stat line off a web page produces these
    /// constantly, and rejecting them throws away data the user did supply.
    /// Returns null when nothing numeric is left, so a genuine mistake still
    /// gets reported rather than silently becoming a number.
    /// </summary>
    private static string? Digits(string value)
    {
        var cleaned = new string(value
            .Where(c => char.IsDigit(c) || c == '.' || c == '-' || c == '+')
            .ToArray())
            .Trim();

        // A lone sign or dot is not a number, and neither is "6.2.1".
        return cleaned.Count(char.IsDigit) == 0 || cleaned.Count(c => c == '.') > 1 ? null : cleaned;
    }

    private static double? ParseDouble(string value, string rowLabel, string field, List<string> warnings,
        List<string> corrections)
    {
        if (value.Length == 0)
        {
            return null;
        }

        if (double.TryParse(value, out var parsed))
        {
            return parsed;
        }

        if (Digits(value) is string digits && double.TryParse(digits, out var recovered))
        {
            corrections.Add($"{rowLabel}: {field} '{value}' read as {recovered:0.##}.");
            return recovered;
        }

        warnings.Add($"{rowLabel}: {field} '{value}' is not a number — ignored.");
        return null;
    }

    /// <summary>
    /// Anything a spreadsheet turns a height into once it has decided the cell
    /// is a date: <c>2-Jun</c>, <c>Jun-02</c>, <c>6/2/2026</c>, <c>2026-06-02</c>.
    /// </summary>
    private static readonly System.Text.RegularExpressions.Regex DateShapedHeight = new(
        @"^\s*(?:\d{1,4}[-/.]\d{1,2}[-/.]\d{1,4}"
        + @"|\d{1,2}[-/ ](?:jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec)[a-z]*"
        + @"|(?:jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec)[a-z]*[-/ ]\d{1,2})\s*$",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase
        | System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// Parses the <c>HeightInches</c> cell.
    ///
    /// <para><b>The column is inches, and its name says so.</b> A bare number
    /// is the only thing anyone — a person, a spreadsheet assistant — should
    /// ever have to put here, because feet-inches is what makes the cell
    /// ambiguous and it is ambiguous to software long before it is ambiguous
    /// to a reader. Excel decides <c>6-2</c> is the 2nd of June the moment it
    /// opens the file, and writes back <c>2-Jun</c> or the serial number
    /// behind that date; the height is gone and nothing says why.</para>
    ///
    /// <para>Feet-inches is still <b>read</b>, because refusing a value the
    /// tool plainly understands would cost the user data to make a point. It
    /// is converted and reported as a correction, so the file can be fixed at
    /// the source.</para>
    /// </summary>
    internal static int? ParseHeight(
        string value, string rowLabel, List<string> warnings, List<string> corrections)
    {
        if (value.Length == 0)
        {
            return null;
        }

        if (int.TryParse(value, out var inches))
        {
            if (inches is >= 48 and <= 96)
            {
                return inches;
            }

            // A spreadsheet date serial. Naming the cause matters more than
            // the number does: a user told "not plausible" checks the height,
            // and the height was right when they typed it.
            if (inches is >= 20000 and <= 80000)
            {
                warnings.Add(
                    $"{rowLabel}: HeightInches '{value}' is a spreadsheet date serial, not a height — " +
                    "ignored. A cell like 6-2 becomes a date the moment Excel opens the file. Write the " +
                    "height in inches (6-2 is 74), or format the column as Text first.");
                return null;
            }

            warnings.Add($"{rowLabel}: HeightInches '{value}' is not a plausible height in inches — ignored.");
            return null;
        }

        if (DateShapedHeight.IsMatch(value))
        {
            warnings.Add(
                $"{rowLabel}: HeightInches '{value}' is a date, not a height — ignored. A cell like 6-2 " +
                "becomes a date the moment Excel opens the file. Write the height in inches (6-2 is 74), " +
                "or format the column as Text first.");
            return null;
        }

        var match = System.Text.RegularExpressions.Regex.Match(
            value, @"^\s*(\d)\s*(?:'|ft|feet|-|\s)\s*(\d{1,2})\s*(?:""|in)?\s*$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (match.Success)
        {
            var feet = int.Parse(match.Groups[1].Value);
            var remainder = int.Parse(match.Groups[2].Value);
            if (feet is >= 4 and <= 7 && remainder < 12)
            {
                var total = feet * 12 + remainder;
                corrections.Add(
                    $"{rowLabel}: HeightInches '{value}' read as {total}. The column is inches — writing " +
                    $"{total} keeps a spreadsheet from turning it into a date.");
                return total;
            }
        }

        warnings.Add($"{rowLabel}: HeightInches '{value}' is not recognized — ignored. The column is " +
                     "inches: write 74, not 6-2.");
        return null;
    }

    /// <summary>
    /// Reads the optional SkinTone cell: EA's 1 (lightest) to 8 (darkest).
    ///
    /// A value outside that range is refused rather than clamped. Clamping
    /// would quietly turn a typed "10" into the darkest tone in the game and
    /// give the user a player they did not ask for, with nothing on screen to
    /// say so; ignoring it leaves the slot alone and says why.
    /// </summary>
    private static int? ReadSkinTone(string value, string rowLabel, List<string> warnings)
    {
        if (value.Length == 0)
        {
            return null;
        }

        if (int.TryParse(value.Trim(), out var tone) && Appearance.HeadAsset.IsValidSkinTone(tone))
        {
            return tone;
        }

        warnings.Add(
            $"{rowLabel}: SkinTone '{value}' is not one of {Appearance.HeadAsset.MinimumSkinTone}–" +
            $"{Appearance.HeadAsset.MaximumSkinTone} (lightest to darkest) — ignored, and this player " +
            "keeps the appearance of the roster slot they took over.");
        return null;
    }

    private static int? ParseInt(string value, string rowLabel, string field, List<string> warnings,
        List<string> corrections)
    {
        if (value.Length == 0)
        {
            return null;
        }

        if (int.TryParse(value, out var parsed))
        {
            return parsed;
        }

        // "#13" from a roster page, and "13.0" from a spreadsheet that decided
        // the column was a decimal, both mean 13.
        if (Digits(value) is string digits && double.TryParse(digits, out var recovered) &&
            Math.Abs(recovered - Math.Round(recovered)) < 1e-9)
        {
            var whole = (int)Math.Round(recovered);
            corrections.Add($"{rowLabel}: {field} '{value}' read as {whole}.");
            return whole;
        }

        warnings.Add($"{rowLabel}: {field} '{value}' is not a number — ignored.");
        return null;
    }

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;

    private static string Normalize(string name) =>
        new(name.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
}
