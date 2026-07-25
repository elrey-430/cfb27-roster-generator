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
    public void AnEraPutsItsHelmetOnEveryPlayerOnTheTeam()
    {
        var visuals = Before();
        var report = new EquipmentApplier(Eras()).Apply(Players(), visuals, teamIndex: 27, season: 2014);

        Assert.NotNull(report.Era);
        Assert.Equal("2010-2016", report.Era!.Name);

        // Everyone on the roster ends up in the era helmet, whether they were
        // supplied by the user or filled in as depth.
        Assert.Equal(85, report.Changed.Count + report.AlreadyCorrect + report.Unresolved.Count);
        Assert.Empty(report.Unresolved);
        Assert.All(report.Changed, c => Assert.Equal(RevolutionSpeed, c.After));
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
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
        File.WriteAllText(path, """
            { "eras": [ { "name": "broken", "fromSeason": 2010, "toSeason": 2016,
                          "helmet": { "helmet": "GearHelmet_RevolutionSpeed" } } ] }
            """);
        try
        {
            var error = Assert.Throws<InvalidDataException>(() => EquipmentEraSet.Load(path));
            Assert.Contains("face mask", error.Message);
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
