using RosterGenerator.Core.Legacy;
using Xunit;

namespace RosterGenerator.Core.Tests;

/// <summary>
/// Writing back into a legacy roster file.
///
/// <para>The reader and the writer walk the same bits in opposite directions,
/// and the only way to know they agree is to make them prove it: read every
/// field of every record, write each one straight back, and require the file
/// to come out byte for byte as it went in. A writer that disagrees with the
/// reader by one bit produces a file that still loads and is quietly wrong,
/// which is the worst outcome available here.</para>
/// </summary>
public sealed class LegacyWriteTests
{
    /// <summary>
    /// Numeric fields only. A name is held as 88 or 104 bits of plain text and
    /// has its own pair of accessors; putting it through the numeric ones
    /// would ask an int to hold eleven bytes.
    /// </summary>
    private static IEnumerable<LegacyField> Numeric(LegacyTable table) =>
        table.Fields.Where(f => f.Bits <= 32);

    /// <summary>Reads every numeric field of every record into a plain list.</summary>
    private static List<int> ReadAll(LegacyTable table) =>
        Enumerable.Range(0, table.Capacity)
            .SelectMany(r => Numeric(table).Select(f => table.Read(r, f.Name)))
            .ToList();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WritingEveryFieldBackUnchangedLeavesTheFileByteIdentical(bool bigEndian)
    {
        var original = bigEndian ? LegacyRosterTests.BigEndianFixture() : LegacyRosterTests.LittleEndianFixture();
        var file = EaDbFile.Parse(original.ToArray());

        foreach (var table in file.Tables.Values)
        {
            for (var record = 0; record < table.Capacity; record++)
            {
                foreach (var field in Numeric(table))
                {
                    table.Write(record, field.Name, table.Read(record, field.Name));
                }

                foreach (var field in table.Fields.Where(f => f.Bits > 32))
                {
                    table.WriteText(record, field.Name, table.ReadText(record, field.Name));
                }
            }
        }

        Assert.Equal(original, file.Bytes.ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AWrittenValueReadsBackAsItself(bool bigEndian)
    {
        // Every value a field can hold, not a sample: an off-by-one in the
        // mask only shows at the top of the range.
        var file = EaDbFile.Parse(
            (bigEndian ? LegacyRosterTests.BigEndianFixture() : LegacyRosterTests.LittleEndianFixture()).ToArray());
        var table = file.Tables["PLAY"];

        foreach (var field in table.Fields.Where(f => f.Bits is > 0 and <= 10))
        {
            var limit = (1 << field.Bits) - 1;
            foreach (var value in new[] { 0, 1, limit / 2, limit - 1, limit }.Distinct().Where(v => v >= 0))
            {
                table.Write(0, field.Name, value);
                Assert.Equal(value, table.Read(0, field.Name));
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WritingOneFieldDisturbsNoOther(bool bigEndian)
    {
        // Fields straddle byte boundaries, so a careless mask writes into the
        // neighbour — and the neighbour is another player's rating.
        var file = EaDbFile.Parse(
            (bigEndian ? LegacyRosterTests.BigEndianFixture() : LegacyRosterTests.LittleEndianFixture()).ToArray());
        var table = file.Tables["PLAY"];
        var before = ReadAll(table);

        var numeric = Numeric(table).ToList();
        var target = numeric.First(f => f.Bits is > 1 and <= 10);
        var index = numeric.IndexOf(target);
        table.Write(1, target.Name, (1 << target.Bits) - 1);

        var after = ReadAll(table);
        var moved = Enumerable.Range(0, before.Count).Where(i => before[i] != after[i]).ToList();
        Assert.Single(moved);
        Assert.Equal(numeric.Count + index, moved[0]);
    }

    [Fact]
    public void ANameWrittenOverALongerOneLeavesNoTailBehind()
    {
        // The tail of the name being replaced is somebody else's name. The
        // game would stop at the terminator and never show it; anyone
        // comparing two files would see it.
        // Stated without reaching inside: writing "Fox" over a long name must
        // leave the file byte for byte where writing "Fox" over a blank slot
        // does. If any of "Anderson" survived past the terminator the two
        // would differ, even though both read back as "Fox".
        var overLongName = EaDbFile.Parse(LegacyRosterTests.BigEndianFixture().ToArray());
        var overBlank = EaDbFile.Parse(LegacyRosterTests.BigEndianFixture().ToArray());

        Assert.Equal("Anderson", overLongName.Tables["PLAY"].ReadText(1, "PLNA"));
        overBlank.Tables["PLAY"].WriteText(1, "PLNA", "");
        Assert.NotEqual(overLongName.Bytes.ToArray(), overBlank.Bytes.ToArray());

        overLongName.Tables["PLAY"].WriteText(1, "PLNA", "Fox");
        overBlank.Tables["PLAY"].WriteText(1, "PLNA", "Fox");

        Assert.Equal("Fox", overLongName.Tables["PLAY"].ReadText(1, "PLNA"));
        Assert.Equal(overBlank.Bytes.ToArray(), overLongName.Bytes.ToArray());
    }

    [Fact]
    public void AValueTooWideForItsFieldIsRefused()
    {
        // Truncating silently would write a plausible-looking wrong number.
        var table = EaDbFile.Parse(LegacyRosterTests.LittleEndianFixture().ToArray()).Tables["PLAY"];
        var field = table.Fields.First(f => f.Bits is > 0 and <= 8);

        Assert.Throws<ArgumentOutOfRangeException>(() => table.Write(0, field.Name, 1 << field.Bits));
        Assert.Throws<ArgumentOutOfRangeException>(() => table.Write(0, field.Name, -1));
    }

    [Fact]
    public void SavingOverTheFileThatWasReadIsRefused()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            File.WriteAllBytes(path, LegacyRosterTests.LittleEndianFixture().ToArray());
            var file = EaDbFile.Read(path);

            Assert.Throws<InvalidOperationException>(() => file.Save(path, readFrom: path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
