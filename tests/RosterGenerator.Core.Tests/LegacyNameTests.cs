using RosterGenerator.Core.Legacy;
using Xunit;

namespace RosterGenerator.Core.Tests;

/// <summary>
/// Putting a CFB27 name into a PS2 roster's per-character name columns.
///
/// <para>Ten characters for a first name and thirteen for a last, in an
/// alphabet of 52 letters and four punctuation marks. Plenty of real names do
/// not fit, and the rule throughout is that what cannot be carried is said
/// out loud rather than quietly mangled — a subtly wrong name on a real person
/// is worse than a reported one.</para>
/// </summary>
public sealed class LegacyNameTests
{
    private static LegacyTable Play() =>
        EaDbFile.Parse(LegacyRosterTests.LittleEndianFixture().ToArray()).Tables["PLAY"];

    [Theory]
    [InlineData("Reggie")]
    [InlineData("LenDale")]
    [InlineData("D'Angelo")]
    [InlineData("A.J.")]
    [InlineData("Van Dyke")]
    public void ANameTheFormatCanHoldGoesInAndComesBackOut(string name)
    {
        var play = Play();
        Assert.Null(LegacySchema.EncodeName(play, 0, LegacySchema.LastNameFields, name));
        Assert.Equal(name, LegacySchema.DecodeName(play, 0, LegacySchema.LastNameFields));
    }

    [Fact]
    public void ALongerNameIsCutAndSaidSoOutLoud()
    {
        var play = Play();
        var note = LegacySchema.EncodeName(
            play, 0, LegacySchema.LastNameFields, "Vanderjagtenberg");

        Assert.NotNull(note);
        Assert.Contains("13 characters", note);
        Assert.Equal("Vanderjagtenb", LegacySchema.DecodeName(play, 0, LegacySchema.LastNameFields));
    }

    [Fact]
    public void ACharacterTheFormatHasNoCodeForIsNamed()
    {
        var play = Play();
        var note = LegacySchema.EncodeName(play, 0, LegacySchema.LastNameFields, "Nuñez3");

        Assert.NotNull(note);
        Assert.Contains("'ñ'", note);
        Assert.Contains("'3'", note);
        Assert.Equal("Nuez", LegacySchema.DecodeName(play, 0, LegacySchema.LastNameFields));
    }

    [Fact]
    public void AShorterNameLeavesNoneOfTheOldOneBehind()
    {
        // The tail of the name being replaced is another player's name.
        var play = Play();
        LegacySchema.EncodeName(play, 0, LegacySchema.LastNameFields, "Winterbottom");
        LegacySchema.EncodeName(play, 0, LegacySchema.LastNameFields, "Fox");

        Assert.Equal("Fox", LegacySchema.DecodeName(play, 0, LegacySchema.LastNameFields));
        foreach (var field in LegacySchema.LastNameFields.Skip(3))
        {
            Assert.Equal(0, play.Read(0, field));
        }
    }

    [Fact]
    public void EveryCodeTheReaderKnowsIsOneTheWriterCanProduce()
    {
        // The two halves have to agree, or a name reads back as something the
        // writer never meant to put there.
        for (var code = 1; code <= 56; code++)
        {
            if (LegacySchema.DecodeNameCharacter(code) is not char character)
            {
                continue;
            }

            // 55 also decodes to '.', so the writer is allowed to prefer 53.
            var encoded = LegacySchema.EncodeNameCharacter(character);
            Assert.NotNull(encoded);
            Assert.Equal(character, LegacySchema.DecodeNameCharacter(encoded!.Value));
        }
    }
}
