using RosterGenerator.Core.Editing;
using RosterGenerator.Core.Schema;
using RosterGenerator.Core.Validation;
using Xunit;

namespace RosterGenerator.Core.Tests;

/// <summary>
/// Exercises every validation rule, with emphasis on the two confirmed
/// multi-field dependencies: team changes (Group 4) and identity changes
/// (Group 3).
/// </summary>
public sealed class ValidationRuleTests
{
    private static ValidationReport Validate(
        Model.PlayerRoster roster,
        RosterEditSession? session = null,
        IReadOnlySet<int>? knownTeams = null) =>
        new RosterValidator().Validate(new RosterValidationContext(roster, session, knownTeams));

    [Fact]
    public void CleanRosterPassesAllRules()
    {
        var report = Validate(TestFixtures.LoadSampleRoster());

        Assert.True(report.IsValid);
        Assert.Empty(report.Issues);
    }

    // -- Single-field rules --------------------------------------------------

    [Fact]
    public void PreExistingAnomalyInSourceFileIsDowngradedToWarning()
    {
        // Real EA exports contain anomalies of their own (the observed base
        // save has two live rows with blank names). Simulate one by blanking
        // a name BEFORE the roster is loaded: it must warn, not block export.
        var doctored = Csv.CsvDocument.Parse(TestFixtures.PlayerSampleText);
        doctored.SetCell(0, PlayerColumns.FirstName, "");
        var roster = Model.PlayerRoster.Parse(doctored.ToCsvText());

        var report = Validate(roster);

        Assert.True(report.IsValid);
        Assert.Contains(report.Warnings, i => i.RuleName == "RequiredFields" &&
                                              i.Column == PlayerColumns.FirstName &&
                                              i.Message.Contains("Pre-existing"));
    }

    [Fact]
    public void MissingFirstNameIsAnError()
    {
        var roster = TestFixtures.LoadSampleRoster();
        roster.Players.First().SetRaw(PlayerColumns.FirstName, "");

        var report = Validate(roster);

        Assert.Contains(report.Errors, i => i.RuleName == "RequiredFields" && i.Column == PlayerColumns.FirstName);
    }

    [Fact]
    public void DuplicateRowKeyIsAnError()
    {
        var roster = TestFixtures.LoadSampleRoster();
        var players = roster.Players.Take(2).ToList();
        players[1].SetRaw(PlayerColumns.Row, players[0].GetRaw(PlayerColumns.Row));

        var report = Validate(roster);

        Assert.Contains(report.Errors, i => i.RuleName == "DuplicateRowKey");
    }

    [Fact]
    public void RatingOutsideZeroTo99IsAnError()
    {
        var roster = TestFixtures.LoadSampleRoster();
        roster.Players.First().SetRating("SpeedRating", 150);

        var report = Validate(roster);

        Assert.Contains(report.Errors, i => i.RuleName == "RatingRange" && i.Column == "SpeedRating");
    }

    [Fact]
    public void UnknownPositionIsAnError()
    {
        var roster = TestFixtures.LoadSampleRoster();
        roster.Players.First().SetRaw(PlayerColumns.Position, "XX");

        var report = Validate(roster);

        Assert.Contains(report.Errors, i => i.RuleName == "EnumFields" && i.Column == PlayerColumns.Position);
    }

    [Fact]
    public void TeamIndexOutsideRangeIsAnError()
    {
        var roster = TestFixtures.LoadSampleRoster();
        var player = roster.Players.First();
        new RosterEditSession(roster).TransferPlayer(player, 254);
        player.SetRaw(PlayerColumns.TeamIndex, "300");

        var report = Validate(roster);

        Assert.Contains(report.Errors, i => i.RuleName == "TeamAssignment");
    }

    [Fact]
    public void TeamIndexNotInKnownTeamsIsAnError()
    {
        var roster = TestFixtures.LoadSampleRoster();
        var session = new RosterEditSession(roster);
        session.TransferPlayer(roster.Players.First(), 133);

        var report = Validate(roster, session, knownTeams: new HashSet<int> { 2, 27 });

        Assert.Contains(report.Errors, i => i.RuleName == "TeamAssignment" && i.Message.Contains("133"));
    }

    // -- Group 4: team change multi-field dependency -------------------------

    [Fact]
    public void TeamIndexChangeWithoutCompanionUpdatesIsANamedError()
    {
        var roster = TestFixtures.LoadSampleRoster();
        // Bypass TransferPlayer to simulate a naive single-field edit.
        roster.Players.First().SetRaw(PlayerColumns.TeamIndex, "5");

        var report = Validate(roster);

        var issues = report.Errors.Where(i => i.RuleName == "TeamChangeConsistency").ToList();
        // Stale PrevTeamIndex (still the 255 sentinel) and stale PLYR_PREVTEAMID.
        Assert.Contains(issues, i => i.Column == PlayerColumns.PrevTeamIndex);
        Assert.Contains(issues, i => i.Column == PlayerColumns.PrevTeamId);
    }

    [Fact]
    public void TeamChangeWithStaleNilValuesIsANamedError()
    {
        var roster = TestFixtures.LoadSampleRoster();
        // Fixture row 330 (Tanner Applewhite) has nonzero NIL values.
        var player = roster.FindByRowKey(330)!;
        var oldTeam = player.TeamIndex;
        player.SetRaw(PlayerColumns.TeamIndex, "5");
        player.SetRaw(PlayerColumns.PrevTeamIndex, oldTeam.ToString());
        player.SetRaw(PlayerColumns.PrevTeamId, oldTeam.ToString());
        // NIL fields left stale on purpose.

        var report = Validate(roster);

        var issues = report.Errors.Where(i => i.RuleName == "TeamChangeConsistency").ToList();
        Assert.Contains(issues, i => i.Column == PlayerColumns.BaseNilValue);
        Assert.Contains(issues, i => i.Column == PlayerColumns.CurrentNilCompensation);
    }

    [Fact]
    public void TransferPlayerOperationPassesTeamChangeConsistency()
    {
        var roster = TestFixtures.LoadSampleRoster();
        var player = roster.FindByRowKey(330)!;
        var oldTeam = player.TeamIndex;
        var session = new RosterEditSession(roster);

        session.TransferPlayer(player, 5);

        Assert.Equal(5, player.TeamIndex);
        Assert.Equal(oldTeam, player.PrevTeamIndex);
        Assert.Equal(oldTeam.ToString(), player.GetRaw(PlayerColumns.PrevTeamId));
        Assert.Equal("0", player.GetRaw(PlayerColumns.BaseNilValue));
        Assert.Equal("0", player.GetRaw(PlayerColumns.CurrentNilCompensation));
        Assert.True(Validate(roster, session).IsValid);
    }

    // -- Group 3: identity change vs rename intent ---------------------------

    [Fact]
    public void NameChangeWithoutDeclaredIntentIsANamedError()
    {
        var roster = TestFixtures.LoadSampleRoster();
        roster.Players.First().FirstName = "Someone";

        var report = Validate(roster);

        Assert.Contains(report.Errors, i => i.RuleName == "IdentityChangeConsistency" &&
                                            i.Message.Contains("declared intent"));
    }

    [Fact]
    public void RenameThatAlsoTouchesIdentityAssetsIsANamedError()
    {
        var roster = TestFixtures.LoadSampleRoster();
        var player = roster.Players.First();
        var session = new RosterEditSession(roster);
        session.RenamePlayer(player, "Charlie", "Ward");
        player.SetRaw(PlayerColumns.AssetName, "WardCharlie_9999");

        var report = Validate(roster, session);

        Assert.Contains(report.Errors, i => i.RuleName == "IdentityChangeConsistency" &&
                                            i.Message.Contains(PlayerColumns.AssetName));
    }

    [Fact]
    public void PlainRenameViaSessionIsValid()
    {
        var roster = TestFixtures.LoadSampleRoster();
        var session = new RosterEditSession(roster);
        session.RenamePlayer(roster.Players.First(), "Charlie", "Ward");

        Assert.True(Validate(roster, session).IsValid);
    }

    [Fact]
    public void ReplaceIdentityUpdatingAllAssetsIsValid()
    {
        var roster = TestFixtures.LoadSampleRoster();
        var session = new RosterEditSession(roster);

        session.ReplacePlayerIdentity(
            roster.Players.First(),
            "Jamari", "Howard",
            assetName: "HowardJamari_7025",
            genericHeadAssetName: "Generic_1234_P_T0042_H_6_3",
            portrait: "7025");

        var report = Validate(roster, session);
        Assert.True(report.IsValid);
        Assert.Empty(report.Warnings);
    }

    [Fact]
    public void ReplaceIdentityLeavingAssetsStaleWarns()
    {
        var roster = TestFixtures.LoadSampleRoster();
        var player = roster.Players.First();
        var session = new RosterEditSession(roster);
        // Caller passes back the existing asset values — technically declared
        // a replace, but the assets still belong to the old identity.
        session.ReplacePlayerIdentity(
            player,
            "Jamari", "Howard",
            assetName: player.GetRaw(PlayerColumns.AssetName),
            genericHeadAssetName: player.GetRaw(PlayerColumns.GenericHeadAssetName),
            portrait: player.GetRaw(PlayerColumns.Portrait));

        var report = Validate(roster, session);

        Assert.True(report.IsValid);
        Assert.Contains(report.Warnings, i => i.RuleName == "IdentityChangeConsistency");
    }

    // -- Group 2: weight encoding (stored = pounds − 160) --------------------

    [Fact]
    public void WeightPoundsAppliesTheConfirmedOffsetBothWays()
    {
        var roster = TestFixtures.LoadSampleRoster();
        var player = roster.Players.First();

        player.WeightPounds = 215;

        Assert.Equal("55", player.GetRaw(PlayerColumns.Weight));
        Assert.Equal(215, player.WeightPounds);
        Assert.True(Validate(roster).IsValid);
    }

    [Theory]
    [InlineData(159)]
    [InlineData(401)]
    public void WeightOutsideRepresentableRangeIsRejected(int pounds)
    {
        var roster = TestFixtures.LoadSampleRoster();

        Assert.Throws<ArgumentOutOfRangeException>(() => roster.Players.First().WeightPounds = pounds);
    }

    [Fact]
    public void StoredWeightOutsideRangeIsAValidationError()
    {
        var roster = TestFixtures.LoadSampleRoster();
        roster.Players.First().SetRaw(PlayerColumns.Weight, "241");

        var report = Validate(roster);

        Assert.Contains(report.Errors, i => i.RuleName == "WeightRange" && i.Column == PlayerColumns.Weight);
    }

    [Fact]
    public void CommentChangeIsBlocked()
    {
        var roster = TestFixtures.LoadSampleRoster();
        roster.Players.First().SetRaw(PlayerColumns.Comment, "1234");

        var report = Validate(roster);

        Assert.Contains(report.Errors, i => i.RuleName == "OpaqueFieldGuard" && i.Column == PlayerColumns.Comment);
    }
}
