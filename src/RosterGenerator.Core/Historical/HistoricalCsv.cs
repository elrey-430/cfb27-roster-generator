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
    /// School override. When null, the file's Team column must supply one
    /// consistent value.
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
            if (school is null && teamValue.Length > 0)
            {
                if (fileSchool is null)
                {
                    fileSchool = teamValue;
                }
                else if (Normalize(fileSchool) != Normalize(teamValue))
                {
                    warnings.Add($"{rowLabel}: Team '{teamValue}' differs from '{fileSchool}' used by earlier " +
                                 "rows; one file describes one team's roster. The first value wins.");
                }
            }

            var seasonValue = Cell(row, "season");
            if (season is null && seasonValue.Length > 0 && int.TryParse(seasonValue, out var parsedSeason))
            {
                fileSeason = parsedSeason;
            }

            players.Add(new HistoricalPlayer
            {
                FirstName = firstName,
                LastName = lastName,
                Position = position,
                JerseyNumber = ParseInt(Cell(row, "number"), rowLabel, "Number", warnings, corrections),
                HeightInches = ParseHeight(Cell(row, "height"), rowLabel, warnings),
                WeightPounds = ParseInt(Cell(row, "weight"), rowLabel, "Weight", warnings, corrections),
                ClassYear = NullIfEmpty(Cell(row, "class")),
                Hometown = NullIfEmpty(Cell(row, "hometown")),
                PreviousSchool = NullIfEmpty(Cell(row, "previousschool")),
                Notes = NullIfEmpty(Cell(row, "notes")),
                Evidence = ReadEvidence(Cell, row, rowLabel, warnings, corrections),
            });
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

        var roster = new HistoricalRoster
        {
            Season = season ?? fileSeason,
            School = resolvedSchool,
            Source = $"Simple historical CSV: {Path.GetFileName(path)}",
            Players = players,
        };
        return new HistoricalCsvResult(roster, warnings, corrections);
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

        return new RatingEvidence
        {
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
    /// Parses a height value: plain inches ("74") or feet-inches notation
    /// ("6-2", "6'2", "6 2", "6ft2"). Returns null (with a warning) when
    /// the value is present but unparseable.
    /// </summary>
    internal static int? ParseHeight(string value, string rowLabel, List<string> warnings)
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

            warnings.Add($"{rowLabel}: Height '{value}' is not a plausible height in inches — ignored.");
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
                return feet * 12 + remainder;
            }
        }

        warnings.Add($"{rowLabel}: Height '{value}' is not recognized (use inches like 74, or feet-inches " +
                     "like 6-2) — ignored.");
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
