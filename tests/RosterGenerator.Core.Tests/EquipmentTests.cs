using RosterGenerator.Core.Csv;
using RosterGenerator.Core.Equipment;
using RosterGenerator.Core.Model;
using RosterGenerator.Core.Schema;
using Xunit;

namespace RosterGenerator.Core.Tests;

/// <summary>
/// Equipment is decoded from a controlled experiment: one dynasty exported
/// twice, identical but for eight Florida State cornerbacks whose helmets were
/// changed in the community roster editor. Of 2,273 files, exactly one
/// differed — the CharacterVisuals table.
///
/// <para>These tests hold the implementation to that evidence. The strongest
/// of them requires our output to be <b>byte-identical</b> to what the real
/// editor produced, which is a higher bar than "the helmet field looks
/// right".</para>
/// </summary>
public sealed class EquipmentTests
{
    private static string Fixture(string relative) =>
        Path.Combine(AppContext.BaseDirectory, "Tests", relative);

    private static CharacterVisualsTable Before() =>
        CharacterVisualsTable.Load(Fixture(Path.Combine("EquipmentDynasty", "0130_CharacterVisuals.csv")));

    private static CharacterVisualsTable After() =>
        CharacterVisualsTable.Load(
            Fixture(Path.Combine("EquipmentDynasty_Expected", "0130_CharacterVisuals.csv")));

    private static PlayerRoster Players() =>
        PlayerRoster.Load(Fixture(Path.Combine("EquipmentDynasty", "0152_Player.csv")));

    private static EquipmentEraSet Eras() =>
        EquipmentEraSet.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "EquipmentEras.json"));

    private static readonly HeadGear RevolutionSpeed =
        new("GearHelmet_RevolutionSpeed", "GearFaceMask_revospeed2bar");

    // The three players the editor changed without also filling in a default
    // Head loadout, so its output and ours should agree to the byte. The other
    // five gained "Head_SkinDetails_None"/"NeckTattoo_None" because their Head
    // loadout had no loadoutElements key at all — an artifact of the editor's
    // round-trip, not part of a helmet change, and not something to imitate.
    private static readonly int[] CleanlyEditedRows = { 2066, 9120, 13938 };

    [Fact]
    public void TheDecodedReferenceFindsTheRightPlayers()
    {
        var players = Players();
        var byRow = players.Players
            .Where(p => p.HasColumn(PlayerColumns.CharacterVisuals))
            .Select(p => (Row: CharacterVisualsReference.RowId(p.GetRaw(PlayerColumns.CharacterVisuals)),
                          Name: $"{p.FirstName} {p.LastName}"))
            .Where(x => x.Row is not null)
            .ToDictionary(x => x.Row!.Value, x => x.Name);

        Assert.Equal("Nehemiah Chandler", byRow[2066]);
        Assert.Equal("Ja'Bril Rawls", byRow[9120]);
        Assert.Equal("Jamari Howard", byRow[13938]);
        Assert.Equal("Zae Thomas", byRow[15462]);
    }

    [Fact]
    public void ARejectedReferenceIsNullRatherThanAWrongRow()
    {
        Assert.Null(CharacterVisualsReference.RowId(null));
        Assert.Null(CharacterVisualsReference.RowId(""));
        Assert.Null(CharacterVisualsReference.RowId("not binary"));

        // Correctly formed, but tagged for a different table.
        Assert.Null(CharacterVisualsReference.RowId("00000000000000010000000000000001"));

        // The real thing: tag 8452 in the high half, row 2066 in the low.
        Assert.Equal(2066, CharacterVisualsReference.RowId("00100001000001000000100000010010"));
    }

    [Fact]
    public void ReadsTheHelmetAndMaskTheEditorShows()
    {
        var gear = Before().GetHeadGear(2066);
        Assert.NotNull(gear);
        Assert.Equal("GearHelmet_Axiom", gear!.Value.Helmet);
        Assert.Equal("GearFaceMask_Axiom2barsingle", gear.Value.FaceMask);
    }

    [Fact]
    public void WritingAHelmetMatchesTheRealEditorByteForByte()
    {
        // The whole milestone in one assertion: reproduce what the community
        // editor wrote, exactly, for the rows it edited cleanly.
        var ours = Before();
        var theirs = After();

        foreach (var row in CleanlyEditedRows)
        {
            Assert.True(ours.SetHeadGear(row, RevolutionSpeed), $"row {row} had no helmet to set");
        }

        foreach (var row in CleanlyEditedRows)
        {
            Assert.Equal(
                Raw(theirs, row),
                Raw(ours, row));
        }
    }

    [Fact]
    public void RowsWeDidNotTouchAreLeftExactlyAsTheyWere()
    {
        var original = File.ReadAllText(
            Fixture(Path.Combine("EquipmentDynasty", "0130_CharacterVisuals.csv")));
        var table = CharacterVisualsTable.Load(
            Fixture(Path.Combine("EquipmentDynasty", "0130_CharacterVisuals.csv")));

        table.SetHeadGear(2066, RevolutionSpeed);

        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".csv");
        try
        {
            table.Save(path);
            var written = File.ReadAllText(path);

            // Exactly one row differs, and the file is otherwise unchanged --
            // the same guarantee the player table gives.
            var originalLines = original.Split("\r\n");
            var writtenLines = written.Split("\r\n");
            Assert.Equal(originalLines.Length, writtenLines.Length);
            var differing = originalLines.Where((l, i) => l != writtenLines[i]).Count();
            Assert.Equal(1, differing);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AnUnchangedTableRoundTripsByteIdentically()
    {
        var source = Fixture(Path.Combine("EquipmentDynasty", "0130_CharacterVisuals.csv"));
        var table = CharacterVisualsTable.Load(source);
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".csv");
        try
        {
            table.Save(path);
            Assert.Equal(File.ReadAllText(source), File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AnEraCoversEveryPlayerOnTheTeam()
    {
        var visuals = Before();
        var report = new EquipmentApplier(Eras()).Apply(Players(), visuals, teamIndex: 27, season: 2014);

        Assert.NotNull(report.Era);
        Assert.Equal("2010-2016", report.Era!.Name);

        // Everyone on the roster is accounted for, whether they were supplied
        // by the user or filled in as depth.
        Assert.Equal(85, report.Changed.Count + report.AlreadyCorrect + report.Unresolved.Count);
        Assert.Empty(report.Unresolved);
    }

    [Fact]
    public void ARiddellWearerStaysRiddellAndASchuttWearerStaysSchutt()
    {
        // The rule that keeps a squad looking mixed: brand carries over, the
        // model changes. Collapsing 85 players into one helmet would be a
        // worse likeness than leaving them alone.
        var visuals = Before();
        new EquipmentApplier(Eras()).Apply(Players(), visuals, teamIndex: 27, season: 2014);

        // Nehemiah Chandler wore a Riddell Axiom; Quindarrius Jones a Schutt F7.
        Assert.Equal(RevolutionSpeed, visuals.GetHeadGear(2066));
        Assert.Equal(
            new HeadGear("GearHelmet_AirXP", "GearFaceMask_2Bar"),
            visuals.GetHeadGear(5930));
    }

    [Fact]
    public void ABrandThatDidNotExistYetTakesTheEraFallback()
    {
        // Vicis shipped nothing until 2016, so a Vicis wearer has no
        // same-brand model to move to in 2014.
        var visuals = Before();
        var eras = Eras();
        Assert.Equal("Vicis", eras.BrandOf("GearHelmet_VicisZero1"));

        new EquipmentApplier(eras).Apply(Players(), visuals, teamIndex: 27, season: 2014);

        Assert.Equal(RevolutionSpeed, visuals.GetHeadGear(5098));   // Karson Hobbs
        Assert.Equal(RevolutionSpeed, visuals.GetHeadGear(15462));  // Zae Thomas
    }

    [Fact]
    public void TheDemonstratedEditsAreReproducedWhereTheyFollowTheRule()
    {
        // Six of the eight demonstrated changes follow brand lineage. The two
        // that do not are recorded in docs/Schema.md rather than fitted to:
        // Jamari Howard (Schutt -> Riddell) and Charles Lester (Light ->
        // Revolution, a 2000s shell), both left for a follow-up demonstration.
        var visuals = Before();
        new EquipmentApplier(Eras()).Apply(Players(), visuals, teamIndex: 27, season: 2014);

        var editor = After();
        int[] followTheRule = { 2066, 5098, 5921, 5930, 9120, 15462 };
        foreach (var row in followTheRule)
        {
            Assert.Equal(editor.GetHeadGear(row), visuals.GetHeadGear(row));
        }
    }

    [Fact]
    public void MasksFollowThePlayersPosition()
    {
        // The game's own rosters put a kicker cage on 92-98% of kickers and a
        // cage or heavy bar on linemen. An era whose per-role masks are known
        // should do the same rather than giving everyone one mask.
        var eras = EquipmentEraSet.Load(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "EquipmentEras_Roles.json"));

        Assert.Equal(MaskRoles.Kicker, eras.RoleOf("P"));
        Assert.Equal(MaskRoles.OffensiveLine, eras.RoleOf("C"));
        Assert.Equal(MaskRoles.DefensiveLine, eras.RoleOf("DT"));
        Assert.Equal(MaskRoles.Quarterback, eras.RoleOf("QB"));
        Assert.Equal(MaskRoles.Skill, eras.RoleOf("WR"));

        var era = eras.ForSeason(2014)!;
        var option = era.Fallback;
        Assert.Equal("GearFaceMask_SpeedFlexKicker", option.ForRole(MaskRoles.Kicker, 0).FaceMask);
        Assert.Equal("GearFaceMask_Speedflex2Bar", option.ForRole(MaskRoles.Quarterback, 0).FaceMask);
    }

    [Fact]
    public void AnUnknownPositionIsTreatedAsASkillPlayer()
    {
        Assert.Equal(MaskRoles.Skill, Eras().RoleOf("LS"));
        Assert.Equal(MaskRoles.Skill, Eras().RoleOf(""));
    }

    [Fact]
    public void AShellWithNoPerRoleMasksGivesEveryoneItsDefault()
    {
        // The Schutt Air XP has only had its two-bar demonstrated, so it must
        // keep giving everyone that rather than inventing a kicker cage.
        var option = Eras().ForSeason(2014)!.ByBrand["Schutt"];
        Assert.Empty(option.MasksByRole);
        Assert.Equal(option.ForRole(MaskRoles.Skill, 0), option.ForRole(MaskRoles.Kicker, 7));
    }

    [Fact]
    public void TheLinemanMaskPoolIsVariedButDeterministic()
    {
        var option = new HelmetOption
        {
            Helmet = "GearHelmet_Speed_Flex",
            FaceMask = "GearFaceMask_Speedflex2Bar",
            LinemanMaskPool = new List<string>
            {
                "GearFaceMask_SpeedflexFullcage",
                "GearFaceMask_SpeedflexRobotRB",
                "GearFaceMask_Speedflex3BarRB",
            },
        };

        // Same player, same mask, every run -- a roster that shuffled itself
        // between runs would make the output impossible to check.
        Assert.Equal(option.ForRole(MaskRoles.OffensiveLine, 41), option.ForRole(MaskRoles.OffensiveLine, 41));

        // And the line is not all in one mask.
        var spread = Enumerable.Range(0, 12)
            .Select(i => option.ForRole(MaskRoles.OffensiveLine, i).FaceMask)
            .Distinct()
            .Count();
        Assert.Equal(3, spread);

        // Only linemen draw from the pool.
        Assert.Equal("GearFaceMask_Speedflex2Bar", option.ForRole(MaskRoles.Skill, 1).FaceMask);
    }

    [Fact]
    public void AnEraCanSetSleevesAndShoulderPads()
    {
        var visuals = Before();
        var report = new EquipmentApplier(Eras()).Apply(Players(), visuals, teamIndex: 27, season: 2005);

        // The 2000s wore looser jerseys and bigger pads than 2027 does.
        Assert.Equal("Gear_JerseyStyle_SleeveStandard", visuals.GetJerseyStyle(2066));
        Assert.Equal("Medium_Pads", visuals.GetShoulderPads(2066));
        Assert.True(report.SleevesChanged > 0);
        Assert.True(report.ShoulderPadsChanged > 0);
        Assert.Contains("Shoulder pads", report.Describe());
    }

    [Fact]
    public void RiddellsLineSplitsByModelInTheTwoThousands()
    {
        // A SpeedFlex wearer belongs in a Revolution. The Axiom -> VSR-4 half
        // of this rule is pending the VSR-4's asset name, so an Axiom wearer
        // currently falls through to the Revolution too.
        var era = Eras().ForSeason(2005)!;
        Assert.Equal(
            "GearHelmet_Revolution",
            era.For("GearHelmet_Speed_Flex", "Riddell").Helmet);
    }

    [Fact]
    public void TheTwoThousandsEraUsesTheOlderRiddellShells()
    {
        var visuals = Before();
        new EquipmentApplier(Eras()).Apply(Players(), visuals, teamIndex: 27, season: 2005);

        // Nehemiah Chandler wore an Axiom, so he belongs in a VSR-4 rather
        // than the Revolution a SpeedFlex wearer gets.
        Assert.Equal(
            new HeadGear("GearHelmet_standardBrady", "GearFaceMask_2Bar"),
            visuals.GetHeadGear(2066));

        // Ja'Bril Rawls wore a SpeedFlex, and is a cornerback.
        Assert.Equal(
            new HeadGear("GearHelmet_Revolution", "GearFaceMask_RevoRobot2"),
            visuals.GetHeadGear(9120));
    }

    [Fact]
    public void ASeasonNoEraCoversLeavesEquipmentAlone()
    {
        // The same rule every optional feature follows: without evidence, do
        // nothing. Writing an asset name the game may not carry would risk a
        // broken helmet.
        var visuals = Before();
        var before = visuals.GetHeadGear(2066);

        var report = new EquipmentApplier(Eras()).Apply(Players(), visuals, teamIndex: 27, season: 2020);

        Assert.Null(report.Era);
        Assert.False(report.Applied);
        Assert.Empty(report.Changed);
        Assert.Equal(before, visuals.GetHeadGear(2066));
        Assert.Contains("2020", report.Describe());
    }

    [Fact]
    public void PlayersOnOtherTeamsAreNotTouched()
    {
        var visuals = Before();
        var before = visuals.GetHeadGear(2066);

        var report = new EquipmentApplier(Eras()).Apply(Players(), visuals, teamIndex: 999, season: 2014);

        Assert.Empty(report.Changed);
        Assert.Equal(before, visuals.GetHeadGear(2066));
    }

    [Fact]
    public void AnEraMissingItsFaceMaskIsRefused()
    {
        // A helmet without the mask moulded to it leaves a mismatched pair in
        // the save, so the data file is not allowed to describe one.
        var error = LoadEras("""
            { "eras": [ { "name": "broken", "fromSeason": 2010, "toSeason": 2016,
                          "fallback": { "helmet": "GearHelmet_RevolutionSpeed" } } ] }
            """);
        Assert.Contains("face mask", error.Message);
    }

    [Fact]
    public void AnEraWithNoFallbackIsRefused()
    {
        // Without one, a Vicis wearer in 2014 has nothing to be given.
        var error = LoadEras("""
            { "eras": [ { "name": "no fallback", "fromSeason": 2010, "toSeason": 2016,
                          "byBrand": { "Riddell": { "helmet": "GearHelmet_RevolutionSpeed",
                                                    "faceMask": "GearFaceMask_revospeed2bar" } } } ] }
            """);
        Assert.Contains("fallback", error.Message);
    }

    private static InvalidDataException LoadEras(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
        File.WriteAllText(path, json);
        try
        {
            return Assert.Throws<InvalidDataException>(() => EquipmentEraSet.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ATableWithoutTheBlobColumnIsRejected()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".csv");
        File.WriteAllText(path, "_tableIndex,_tableName,_row\r\n130,CharacterVisuals,0\r\n");
        try
        {
            var error = Assert.Throws<CsvSchemaException>(() => CharacterVisualsTable.Load(path));
            Assert.Contains("CharacterVisuals table", error.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    // Every asset name below was read out of a real export after being set in
    // the community roster editor. These assertions are the guard against a
    // typo in the data file quietly putting a player in a helmet the game does
    // not have — the failure mode that would reach a user as a broken model.
    [InlineData(2014, "MLB", "GearHelmet_Speed_Flex", "GearHelmet_RevolutionSpeed", "GearFaceMask_revospeed3BarLBStraight")]
    [InlineData(2014, "C", "GearHelmet_Speed_Flex", "GearHelmet_RevolutionSpeed", "GearFaceMask_revoSpeedFullCage2")]
    [InlineData(2014, "DT", "GearHelmet_Speed_Flex", "GearHelmet_RevolutionSpeed", "GearFaceMask_revoSpeedFullCage")]
    [InlineData(2014, "K", "GearHelmet_Speed_Flex", "GearHelmet_RevolutionSpeed", "GearFaceMask_revospeedKicker")]
    [InlineData(2005, "MLB", "GearHelmet_Speed_Flex", "GearHelmet_Revolution", "GearFaceMask_REVO3BarLb")]
    [InlineData(2005, "C", "GearHelmet_Speed_Flex", "GearHelmet_Revolution", "GearFaceMask_revofullcage")]
    [InlineData(2005, "LE", "GearHelmet_Speed_Flex", "GearHelmet_Revolution", "GearFaceMask_RevoRobot")]
    [InlineData(2005, "K", "GearHelmet_Speed_Flex", "GearHelmet_Revolution", "GearFaceMask_revoKicker")]
    // An Axiom wearer in the 2000s belongs in a VSR-4, not a Revolution.
    [InlineData(2005, "QB", "GearHelmet_Axiom", "GearHelmet_standardBrady", "GearFaceMask_2Bar")]
    // Schutt keeps its own line: the Air Advantage, a different asset from the
    // Air XP Pro VTD despite the editor labelling both "Air XP".
    [InlineData(2005, "QB", "GearHelmet_SchuttF7", "GearHelmet_Schutt", "GearFaceMask_2Bar")]
    [InlineData(1995, "QB", "GearHelmet_Speed_Flex", "GearHelmet_standardBrady", "GearFaceMask_2Bar")]
    [InlineData(1985, "HB", "GearHelmet_Speed_Flex", "GearHelmet_RiddellTK", "GearFaceMask_VintageStandard")]
    [InlineData(1975, "LT", "GearHelmet_Speed_Flex", "GearHelmet_RiddellTK", "GearFaceMask_VintageTwoBar")]
    [InlineData(1975, "QB", "GearHelmet_Speed_Flex", "GearHelmet_RiddellTK", "GearFaceMask_VintageTwoBar")]
    public void EveryEraWritesTheDemonstratedAssets(
        int season, string position, string wearing, string expectedHelmet, string expectedMask)
    {
        var eras = Eras();
        var era = eras.ForSeason(season);
        Assert.NotNull(era);

        var gear = era!.For(wearing, eras.BrandOf(wearing)).ForRole(eras.RoleOf(position), seed: 0);

        Assert.Equal(expectedHelmet, gear.Helmet);
        Assert.Equal(expectedMask, gear.FaceMask);
    }

    [Fact]
    public void TheEightiesLineIsSpreadAcrossTheVintageMasks()
    {
        // Linemen draw from a pool rather than all wearing one mask; everyone
        // else takes the Vintage Standard.
        var eras = Eras();
        var era = eras.ForSeason(1985)!;
        var option = era.Fallback;

        var line = Enumerable.Range(0, 15)
            .Select(i => option.ForRole(MaskRoles.OffensiveLine, i).FaceMask)
            .Distinct()
            .ToList();
        Assert.Equal(3, line.Count);
        Assert.All(line, m => Assert.Contains("Vintage", m));

        Assert.Equal("GearFaceMask_VintageStandard", option.ForRole(MaskRoles.Skill, 3).FaceMask);
    }

    [Theory]
    [InlineData(2014, "Gear_JerseyStyle_SleeveTight", "Small_Pads")]
    [InlineData(2005, "Gear_JerseyStyle_SleeveStandard", "Medium_Pads")]
    [InlineData(1995, "Gear_JerseyStyle_SleeveLong", "Large_Pads")]
    [InlineData(1985, "Gear_JerseyStyle_SleeveLong", "XLarge_Pads")]
    [InlineData(1975, "Gear_JerseyStyle_SleeveLong", "XLarge_Pads")]
    public void SleevesAndPadsMatchTheEra(int season, string sleeves, string pads)
    {
        var era = Eras().ForSeason(season);
        Assert.NotNull(era);
        Assert.Equal(sleeves, era!.Sleeves);
        Assert.Equal(pads, era.ShoulderPads);
    }

    [Fact]
    public void EveryAssetTheDataFileNamesWasActuallyDemonstrated()
    {
        // The catalogue of names read out of the two demonstration exports.
        // Anything the data file mentions that is not in here would be a guess.
        var confirmed = new HashSet<string>(StringComparer.Ordinal)
        {
            "GearHelmet_RevolutionSpeed", "GearHelmet_Revolution", "GearHelmet_AirXP",
            "GearHelmet_standardBrady", "GearHelmet_RiddellTK", "GearHelmet_Schutt",
            "GearFaceMask_revospeed2bar", "GearFaceMask_revospeed3BarLBStraight",
            "GearFaceMask_revoSpeedFullCage", "GearFaceMask_revoSpeedFullCage2",
            "GearFaceMask_revospeedKicker", "GearFaceMask_RevoRobot", "GearFaceMask_RevoRobot2",
            "GearFaceMask_REVO3BarLb", "GearFaceMask_revofullcage", "GearFaceMask_revoKicker",
            "GearFaceMask_RevoNormal", "GearFaceMask_2Bar",
            "GearFaceMask_VintageStandard", "GearFaceMask_VintageTwoBar",
            "GearFaceMask_VintageLong", "GearFaceMask_VintageHalfCage",
            "Gear_JerseyStyle_SleeveTight", "Gear_JerseyStyle_SleeveStandard",
            "Gear_JerseyStyle_SleeveLong",
            "Small_Pads", "Medium_Pads", "Large_Pads", "XLarge_Pads",
        };

        foreach (var era in Eras().Eras)
        {
            foreach (var option in era.ByBrand.Values.Concat(era.ByModel.Values).Append(era.Fallback))
            {
                Assert.Contains(option.Helmet, confirmed);
                Assert.Contains(option.FaceMask, confirmed);
                Assert.All(option.MasksByRole.Values, m => Assert.Contains(m, confirmed));
                Assert.All(option.LinemanMaskPool, m => Assert.Contains(m, confirmed));
            }

            if (era.Sleeves is not null)
            {
                Assert.Contains(era.Sleeves, confirmed);
            }

            if (era.ShoulderPads is not null)
            {
                Assert.Contains(era.ShoulderPads, confirmed);
            }
        }
    }

    private static string Raw(CharacterVisualsTable table, int rowId)
    {
        var gear = table.GetHeadGear(rowId);
        Assert.NotNull(gear);
        return RawBlob(table, rowId);
    }

    private static string RawBlob(CharacterVisualsTable table, int rowId)
    {
        for (var i = 0; i < table.Document.RowCount; i++)
        {
            if (table.Document.GetCell(i, "_row") == rowId.ToString())
            {
                return table.Document.GetCell(i, "RawData");
            }
        }

        throw new InvalidOperationException($"row {rowId} not in the fixture");
    }
}
