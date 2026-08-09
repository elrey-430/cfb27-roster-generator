using RosterGenerator.Core.Legacy;
using RosterGenerator.Core.Pipeline;
using Xunit;

namespace RosterGenerator.Core.Tests;

/// <summary>
/// An imported roster driven through the whole pipeline, not just the rating
/// engine.
///
/// <para>The engine is tested directly elsewhere. What this pins is the seam
/// either side of it: that the <c>Source*</c> columns an import writes survive
/// the roster reader, the converter and the exporter, and that the overall
/// written into the player table is the one the source stated. That seam is
/// where a feature like this quietly stops working — the engine keeps passing
/// its own tests while nothing reaches it.</para>
/// </summary>
public sealed class SourceRatingPipelineTests
{
    private static string TestsPath(params string[] parts) =>
        Path.Combine(new[] { AppContext.BaseDirectory, "Tests" }.Concat(parts).ToArray());

    /// <summary>Overalls to give the four quarterbacks the roster carries.</summary>
    private static readonly int[] Stated = { 91, 84, 77, 69 };

    /// <summary>
    /// Writes the roster an NCAA 14 import would have produced: identity, a
    /// SourceOverall, and every Source* column the later game records, filled
    /// from what CFB27 itself gives that kind of player.
    /// </summary>
    private static string WriteImportedRoster(string path)
    {
        var engine = TestFixtures.RatingEngine();
        var profile = engine.Profiles!.Find("QB_FieldGeneral")!;
        var splits = engine.Model.SourceRatingSplits;

        var header = new List<string>
        {
            "FirstName", "LastName", "Position", "Number", "HeightInches", "Weight", "Class",
            "Team", "Season", "Role", "SourceOverall",
        };
        header.AddRange(LegacyRosterImporter.SourceColumns);

        var lines = new List<string> { string.Join(",", header) };
        for (var i = 0; i < Stated.Length; i++)
        {
            var overall = Stated[i];
            var row = new List<string>
            {
                "Import", $"Quarterback{i}", "QB", (10 + i).ToString(), "75", "220", "Junior",
                "Florida State", "2013", i == 0 ? "Starter" : "Backup", overall.ToString(),
            };

            foreach (var column in LegacySchema.SourceRatingColumns)
            {
                if (splits.TryGetValue(column, out var across))
                {
                    var parts = across
                        .Where(a => profile.TryExpected(a, overall, out _))
                        .Select(a => { profile.TryExpected(a, overall, out var v); return v; })
                        .ToList();
                    row.Add(parts.Count > 0 ? Math.Round(parts.Average()).ToString() : "");
                    continue;
                }

                row.Add(profile.TryExpected(column, overall, out var value)
                    ? Math.Round(value).ToString()
                    : "");
            }

            lines.Add(string.Join(",", row));
        }

        File.WriteAllLines(path, lines);
        return path;
    }

    [Fact]
    public void AnImportedRosterKeepsItsStatedOverallsThroughTheWholePipeline()
    {
        var roster = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".csv");
        var outputCsv = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".csv");
        var report = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".txt");
        try
        {
            WriteImportedRoster(roster);
            var result = new RosterGenerationService().Run(new RosterGenerationRequest
            {
                DynastyPath = TestsPath("DonorDynasty"),
                RosterPath = roster,
                DataDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures"),
                OutputPath = outputCsv,
                ReportPath = report,
                FillRoster = false,
            });

            Assert.Equal(Stated.Length, result.Converted);
            Assert.Equal(0, result.Skipped);

            // Read the overalls back out of the player table that was actually
            // written, rather than trusting the in-memory result — the export
            // is the thing a user imports.
            var table = Model.PlayerRoster.Load(outputCsv);
            var written = table.Players
                .Where(p => p.GetRaw("LastName").StartsWith("Quarterback", StringComparison.Ordinal))
                .ToDictionary(
                    p => p.GetRaw("LastName"),
                    p => int.Parse(p.GetRaw("OverallRating")),
                    StringComparer.Ordinal);

            Assert.Equal(Stated.Length, written.Count);
            for (var i = 0; i < Stated.Length; i++)
            {
                Assert.Equal(Stated[i], written[$"Quarterback{i}"]);
            }
        }
        finally
        {
            foreach (var file in new[] { roster, outputCsv, report })
            {
                File.Delete(file);
            }
        }
    }
}
