using RosterGenerator.Core.Csv;
using Xunit;

namespace RosterGenerator.Core.Tests;

/// <summary>
/// The foundation guarantee: loading and re-serializing a CFB27 export
/// without edits reproduces the input byte-for-byte. Everything else in the
/// library leans on this.
/// </summary>
public sealed class CsvRoundTripTests
{
    [Fact]
    public void UneditedDocumentRoundTripsByteForByte()
    {
        var original = TestFixtures.PlayerSampleText;

        var document = CsvDocument.Parse(original);

        Assert.Equal(original, document.ToCsvText());
    }

    [Fact]
    public void ParserReportsRaggedRows()
    {
        var ex = Assert.Throws<CsvSchemaException>(() => CsvDocument.Parse("a,b,c\r\n1,2\r\n"));

        Assert.Contains("2 fields", ex.Message);
    }

    [Fact]
    public void ParserReportsEmptyFile()
    {
        Assert.Throws<CsvSchemaException>(() => CsvDocument.Parse(""));
    }

    [Fact]
    public void MissingColumnGetsDescriptiveError()
    {
        var document = CsvDocument.Parse("a,b\r\n1,2\r\n");

        var ex = Assert.Throws<CsvSchemaException>(() => document.GetCell(0, "TeamIndex"));

        Assert.Contains("TeamIndex", ex.Message);
    }
}
