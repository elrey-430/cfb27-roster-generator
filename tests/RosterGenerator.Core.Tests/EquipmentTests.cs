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
    public void TheTwoThousandsEraUsesTheOlderRiddellShell()
    {
        var visuals = Before();
        new EquipmentApplier(Eras()).Apply(Players(), visuals, teamIndex: 27, season: 2005);

        Assert.Equal(
            new HeadGear("GearHelmet_Revolution", "GearFaceMask_RevoNormal"),
            visuals.GetHeadGear(2066));
    }

    [Fact]
    public void ASeasonNoEraCoversLeavesEquipmentAlone()
    {
        // The same rule every optional feature follows: without evidence, do
        // nothing. Writing an asset name the game may not carry would risk a
        // broken helmet.
        var visuals = Before();
        var before = visuals.GetHeadGear(2066);

        var report = new EquipmentApplier(Eras()).Apply(Players(), visuals, teamIndex: 27, season: 1998);

        Assert.Null(report.Era);
        Assert.False(report.Applied);
        Assert.Empty(report.Changed);
        Assert.Equal(before, visuals.GetHeadGear(2066));
        Assert.Contains("1998", report.Describe());
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
