using RosterGenerator.Core.Csv;

namespace RosterGenerator.Core.Historical;

/// <summary>The outcome of reading a user-facing historical roster CSV.</summary>
/// <param name="Roster">The parsed roster (rows with fatal problems excluded).</param>
/// <param name="Warnings">Per-row problems, phrased for end users.</param>
public sealed record HistoricalCsvResult(HistoricalRoster Roster, IReadOnlyList<string> Warnings);

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
        var document = CsvDocument.Parse(File.ReadAllText(path));
        var warnings = new List<string>();

        var columns = document.Header
            .Select((name, index) => (Key: Normalize(name), Index: index))
            .Where(c => c.Key.Length > 0)
            .GroupBy(c => c.Key)
            .ToDictionary(g => g.Key, g => g.First().Index, StringComparer.Ordinal);

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
                JerseyNumber = ParseInt(Cell(row, "number"), rowLabel, "Number", warnings),
                HeightInches = ParseHeight(Cell(row, "height"), rowLabel, warnings),
                WeightPounds = ParseInt(Cell(row, "weight"), rowLabel, "Weight", warnings),
                ClassYear = NullIfEmpty(Cell(row, "class")),
                Hometown = NullIfEmpty(Cell(row, "hometown")),
                PreviousSchool = NullIfEmpty(Cell(row, "previousschool")),
                Notes = NullIfEmpty(Cell(row, "notes")),
            });
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
        return new HistoricalCsvResult(roster, warnings);
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

    private static int? ParseInt(string value, string rowLabel, string field, List<string> warnings)
    {
        if (value.Length == 0)
        {
            return null;
        }

        if (int.TryParse(value, out var parsed))
        {
            return parsed;
        }

        warnings.Add($"{rowLabel}: {field} '{value}' is not a number — ignored.");
        return null;
    }

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;

    private static string Normalize(string name) =>
        new(name.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
}
