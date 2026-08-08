using RosterGenerator.Core.Historical;
using RosterGenerator.Core.Legacy;
using RosterGenerator.Core.Rating;
using Xunit;

namespace RosterGenerator.Core.Tests;

/// <summary>
/// Importing a roster that recorded real ratings.
///
/// <para>A PS2-era file gives an order and nothing more, so what crosses over
/// is a rank. NCAA 14 gives forty-two of CFB27's fifty-seven rating columns as
/// numbers on the same 0-99 scale, and the rules change completely: those
/// numbers are copied, locked, and left exactly where whoever built that
/// roster put them. The tests here are about the seam — what happens to the
/// fifteen columns the older game had no answer for, and to the two it
/// answered once where CFB27 asks three times.</para>
/// </summary>
public sealed class SourceRatingTests
{
    private static readonly RatingEngine Engine = TestFixtures.RatingEngine();

    private static readonly ArchetypeSelector Selector = ArchetypeSelector.Load(
        TestFixtures.DataPath("ArchetypeRules.json"), Engine.Profiles,
        Engine.Model.SourceRatingSplits);

    private static readonly string[] Accuracies =
    {
        "ThrowAccuracyShortRating", "ThrowAccuracyMidRating", "ThrowAccuracyDeepRating",
    };

    private static HistoricalPlayer Quarterback() => new()
    {
        FirstName = "Source",
        LastName = "Quarterback",
        Position = "QB",
        HeightInches = 75,
        WeightPounds = 220,
        ClassYear = "Junior",
    };

    /// <summary>
    /// The source line an NCAA 14 file would hold for the game's own average
    /// player of an archetype at an overall: every column it really has, with
    /// the one throw accuracy and one route running it stores standing in for
    /// CFB27's three.
    /// </summary>
    private static Dictionary<string, double> SourceLine(string archetype, int overall)
    {
        var profile = Engine.Profiles!.Find(archetype)!;
        var line = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var column in LegacySchema.SourceRatingColumns)
        {
            if (Engine.Model.SourceRatingSplits.TryGetValue(column, out var across))
            {
                var parts = new List<double>();
                foreach (var part in across)
                {
                    if (profile.TryExpected(part, overall, out var value))
                    {
                        parts.Add(value);
                    }
                }

                if (parts.Count > 0)
                {
                    line[column] = Math.Round(parts.Average());
                }

                continue;
            }

            if (profile.TryExpected(column, overall, out var expected))
            {
                line[column] = Math.Round(expected);
            }
        }

        return line;
    }

    private static GeneratedRatings Generate(
        string archetype, Dictionary<string, double> source, int overall) =>
        Engine.Generate("QB", archetype, Quarterback(),
            new RatingEvidence { SourceOverall = overall, SourceRatings = source });

    // ---- the split ---------------------------------------------------------

    [Fact]
    public void OneThrowAccuracyBecomesThreeThatAverageBackToIt()
    {
        // The design case, worked through: a field general whose source roster
        // records a single 95. The archetype decides which of short, mid and
        // deep leads; the 95 decides the level.
        var source = SourceLine("QB_FieldGeneral", 85);
        source["ThrowAccuracyRating"] = 95;
        var ratings = Generate("QB_FieldGeneral", source, 85);

        var split = Accuracies.Select(a => ratings.Attributes[a]).ToArray();
        Assert.Equal(95, split.Average(), 0);
        Assert.True(split[0] > split[1] && split[1] > split[2],
            $"a field general throws better short than deep, got {string.Join("/", split)}");
    }

    [Fact]
    public void TheArchetypeDecidesTheShapeOfTheSplit()
    {
        // Same single number, two kinds of quarterback. The game's own pure
        // scramblers fall off with depth harder than its field generals do,
        // and the split has to carry that or every imported quarterback comes
        // out throwing the same ball.
        static Dictionary<string, double> Line(string archetype)
        {
            var source = SourceLine(archetype, 85);
            source["ThrowAccuracyRating"] = 88;
            return source;
        }

        var general = Generate("QB_FieldGeneral", Line("QB_FieldGeneral"), 85);
        var scrambler = Generate("QB_PureScrambler", Line("QB_PureScrambler"), 85);

        var generalDrop = general.Attributes["ThrowAccuracyShortRating"] -
                          general.Attributes["ThrowAccuracyDeepRating"];
        var scramblerDrop = scrambler.Attributes["ThrowAccuracyShortRating"] -
                            scrambler.Attributes["ThrowAccuracyDeepRating"];

        Assert.True(scramblerDrop > generalDrop,
            $"pure scrambler drops {scramblerDrop} short-to-deep, field general {generalDrop}");
        Assert.Equal(88, Accuracies.Average(a => (double)general.Attributes[a]), 0);
        Assert.Equal(88, Accuracies.Average(a => (double)scrambler.Attributes[a]), 0);
    }

    [Fact]
    public void ASplitAtTheCeilingHandsWhatItCannotUseToTheOthers()
    {
        // A 98 accuracy cannot come out 101/98/95, so the points the top one
        // cannot take go to the two that have room. Averaging is the promise;
        // clamping silently would quietly break it.
        var source = SourceLine("QB_FieldGeneral", 92);
        source["ThrowAccuracyRating"] = 98;
        var ratings = Generate("QB_FieldGeneral", source, 92);

        Assert.All(Accuracies, a => Assert.InRange(ratings.Attributes[a], 10, 99));
        Assert.Equal(98, Accuracies.Average(a => (double)ratings.Attributes[a]), 0);
        Assert.Equal(99, ratings.Attributes["ThrowAccuracyShortRating"]);
    }

    [Fact]
    public void TheGeneralThrowAccuracyColumnIsNotWrittenFromTheSource()
    {
        // CFB27 keeps a ThrowAccuracyRating no overall formula reads, and its
        // own improvisers carry about 34 there while throwing in the eighties.
        // Copying the source's number into it would make an imported player
        // the only one in the game whose vestigial column means anything.
        var source = SourceLine("QB_Improviser", 85);
        source["ThrowAccuracyRating"] = 95;
        var ratings = Generate("QB_Improviser", source, 85);

        Assert.True(ratings.Attributes["ThrowAccuracyRating"] < 60,
            $"expected the archetype's own value, got {ratings.Attributes["ThrowAccuracyRating"]}");
    }

    // ---- carrying and gap filling -----------------------------------------

    [Fact]
    public void EveryRatingTheSourceRecordedComesOutUnchanged()
    {
        var source = SourceLine("QB_Scrambler", 85);
        var ratings = Generate("QB_Scrambler", source, 85);

        foreach (var (attribute, value) in source)
        {
            if (Engine.Model.SourceRatingSplits.ContainsKey(attribute) ||
                !ratings.Attributes.ContainsKey(attribute))
            {
                continue;
            }

            Assert.Equal((int)value, ratings.Attributes[attribute]);
        }
    }

    [Fact]
    public void ASeniorsAwarenessIsNotLiftedWhenTheSourceRecordedIt()
    {
        // Class year raises awareness because a roster file normally says
        // nothing about it. Here something does.
        var source = SourceLine("QB_FieldGeneral", 85);
        source["AwarenessRating"] = 71;

        var senior = Engine.Generate("QB", "QB_FieldGeneral",
            Quarterback() with { ClassYear = "Senior" },
            new RatingEvidence { SourceOverall = 85, SourceRatings = source });

        Assert.Equal(71, senior.Attributes["AwarenessRating"]);
    }

    [Fact]
    public void AClassCapDoesNotOverruleARecordedRating()
    {
        // A junior's awareness tops out at 95 in the game's own rosters. That
        // is a statement about what the game does, and it has to yield to a
        // number somebody actually recorded.
        var source = SourceLine("QB_FieldGeneral", 92);
        source["AwarenessRating"] = 99;

        Assert.Equal(99, Generate("QB_FieldGeneral", source, 92).Attributes["AwarenessRating"]);
    }

    [Fact]
    public void TheColumnsTheOlderGameNeverHadComeFromTheArchetype()
    {
        // The whole point of the exercise: a complete CFB27 player out of an
        // incomplete source, with the difference made up from what the game
        // itself gives this kind of player at this level.
        var profile = Engine.Profiles!.Find("QB_PureScrambler")!;
        var ratings = Generate("QB_PureScrambler", SourceLine("QB_PureScrambler", 85), 85);

        foreach (var attribute in new[]
                 {
                     "ThrowUnderPressureRating", "BreakSackRating", "PlayActionRating",
                     "ThrowOnTheRunRating", "ChangeOfDirectionRating",
                 })
        {
            Assert.True(profile.TryExpected(attribute, 85, out var expected));
            var reach = 2 * Math.Max(profile.Spread(attribute), 1);
            Assert.InRange(ratings.Attributes[attribute], expected - reach, expected + reach);
        }
    }

    [Theory]
    [InlineData("QB_FieldGeneral")]
    [InlineData("QB_Improviser")]
    [InlineData("QB_Scrambler")]
    [InlineData("QB_PureScrambler")]
    public void NothingLandsOutsideWhatTheGameItselfSpreads(string archetype)
    {
        // Generated against CFB27's own players of the same archetype at the
        // same overall: nothing may sit further from them than twice the
        // scatter the game's own players show. The gap-filled attributes are
        // the ones this is really checking.
        var profile = Engine.Profiles!.Find(archetype)!;
        foreach (var overall in new[] { 70, 78, 85, 92, 99 })
        {
            var ratings = Generate(archetype, SourceLine(archetype, overall), overall);
            foreach (var attribute in profile.Attributes)
            {
                if (!ratings.Attributes.TryGetValue(attribute, out var generated) ||
                    !profile.TryExpected(attribute, overall, out var expected))
                {
                    continue;
                }

                var reach = 2 * Math.Max(profile.Spread(attribute), 1);
                Assert.True(Math.Abs(generated - expected) <= reach,
                    $"{archetype} at {overall}: {attribute} generated {generated}, " +
                    $"the game gives {expected:0.0} +/- {profile.Spread(attribute):0.0}");
            }
        }
    }

    // ---- the overall -------------------------------------------------------

    [Fact]
    public void TheOverallFollowsTheRatingsRatherThanTheSourcesOwnNumber()
    {
        // A quarterback whose source roster called him an 85 but gave him a
        // 99-overall's throwing. The two numbers came from different formulas
        // over different columns, and the ratings are what this game reads —
        // so the overall moves rather than the ratings being bent to reach it.
        var source = SourceLine("QB_FieldGeneral", 85);
        source["ThrowPowerRating"] = 99;
        source["AwarenessRating"] = 99;
        source["ThrowAccuracyRating"] = 99;
        var ratings = Generate("QB_FieldGeneral", source, 85);

        Assert.True(ratings.Overall > 85, $"expected better than the stated 85, got {ratings.Overall}");
        Assert.Equal(99, ratings.Attributes["ThrowPowerRating"]);
        Assert.Equal(99, ratings.Attributes["AwarenessRating"]);
        Assert.Contains(ratings.Adjustments, a => a.Contains("from the ratings themselves"));
    }

    [Fact]
    public void ASourceOverallIsNotHeldDownByThePositionCeiling()
    {
        // The ceiling is the highest the game's own shipped rosters go, not
        // the highest it can hold. Holding an imported 97 down to 95 would
        // leave him carrying a 97's ratings with a 95's overall.
        var ratings = Generate("QB_Improviser", SourceLine("QB_Improviser", 99), 99);

        Assert.True(ratings.Overall > 95, $"the QB ceiling is 95; got {ratings.Overall}");
    }

    [Fact]
    public void ASourceRatedPlayerIsNotSpreadAlongTheRoleCurve()
    {
        // RoleSpread lays out players the file says nothing about. Somebody
        // deciding this one was an 84 is the most individuating thing a player
        // can carry, so he must not be laid back out on an average curve.
        var roleOnlyPlayer = Quarterback() with { Evidence = new RatingEvidence { Role = "Starter" } };
        var ratedPlayer = Quarterback() with
        {
            Evidence = new RatingEvidence
            {
                Role = "Starter",
                SourceOverall = 84,
                SourceRatings = SourceLine("QB_Scrambler", 84),
            },
        };

        var rated = new RatedPlayer(ratedPlayer, "QB", "QB_Scrambler",
            Engine.Generate("QB", "QB_Scrambler", ratedPlayer, ratedPlayer.Evidence));
        var roleOnly = new RatedPlayer(roleOnlyPlayer, "QB", "QB_Scrambler",
            Engine.Generate("QB", "QB_Scrambler", roleOnlyPlayer, roleOnlyPlayer.Evidence));

        Assert.False(RoleSpread.IsUndifferentiated(rated));
        Assert.True(RoleSpread.IsUndifferentiated(roleOnly));
    }

    // ---- choosing the archetype -------------------------------------------

    [Theory]
    [InlineData("QB_FieldGeneral")]
    [InlineData("QB_Improviser")]
    [InlineData("QB_Scrambler")]
    [InlineData("QB_PureScrambler")]
    public void TheArchetypeIsRecoveredFromTheRatingsAlone(string archetype)
    {
        // No stat line, no weight rule — the numbers themselves say what kind
        // of quarterback this was, and they have to say it at every level.
        foreach (var overall in new[] { 70, 78, 85, 92, 99 })
        {
            var evidence = new RatingEvidence
            {
                SourceOverall = overall,
                SourceRatings = SourceLine(archetype, overall),
            };

            Assert.Equal(archetype, Selector.Select("QB", Quarterback(), evidence).Archetype);
        }
    }

    [Fact]
    public void AHandWrittenRosterStillGoesThroughTheOrdinaryRules()
    {
        // The measured match must not quietly take over the ordinary path: a
        // player with a stat line and no source ratings is chosen exactly as
        // he was before any of this existed.
        var evidence = new RatingEvidence
        {
            Stats = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                [StatKeys.RushYards] = 1200,
            },
        };

        var choice = Selector.Select("QB", Quarterback(), evidence);
        Assert.Equal("QB_PureScrambler", choice.Archetype);
        Assert.Contains("RushYards", choice.Reason);
    }
}
