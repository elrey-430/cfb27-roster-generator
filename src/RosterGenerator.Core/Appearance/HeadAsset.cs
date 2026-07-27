using System.Text.RegularExpressions;

namespace RosterGenerator.Core.Appearance;

/// <summary>Which of the game's three head systems a player is using.</summary>
public enum HeadAssetKind
{
    /// <summary>Not recognised, or absent. Left alone.</summary>
    Unknown,

    /// <summary>
    /// A real person's head scan, <c>Unique_&lt;Name&gt;_&lt;id&gt;</c>. These are the
    /// game's NIL likenesses.
    /// </summary>
    RealPersonScan,

    /// <summary>
    /// A generated face,
    /// <c>Generic_&lt;portrait&gt;_P_T&lt;texture&gt;_&lt;family&gt;_&lt;skinTone&gt;_&lt;variant&gt;</c>.
    /// </summary>
    Generic,

    /// <summary>A create-a-face head assembled from parts.</summary>
    Custom,
}

/// <summary>
/// A parsed <c>GenericHeadAssetName</c>.
///
/// <para>The column holds all three systems despite its name. In a base save
/// 9,011 of 16,257 live players carry a <c>Unique_</c> scan and 7,244 a
/// <c>Generic_</c> face; exactly one uses create-a-face.</para>
///
/// <para><b>The sixth segment is the skin tone.</b> Measured by decoding every
/// player's <c>CharacterVisuals</c> reference in a base save and reading the
/// <c>skinTone</c> field out of the visuals blob: across 3,144 generic-headed
/// players the segment and the field agree <b>3,144 times and disagree zero
/// times</b>. Better still, a given generated head is only ever used at one
/// tone — 1,607 distinct heads, none used at two — so the head and the tone
/// are one choice, not two, and choosing a face is enough to set a player's
/// appearance without writing the visuals table at all.</para>
/// </summary>
/// <param name="Kind">Which head system this name belongs to.</param>
/// <param name="AssetName">The raw name, written back verbatim.</param>
/// <param name="Portrait">Portrait id, repeated inside a generic name.</param>
/// <param name="SkinTone">
/// EA's skin tone, 1 (lightest) to 8 (darkest). Zero when the name is not a
/// generated face, since no other head system spells it out.
/// </param>
public readonly record struct HeadAsset(
    HeadAssetKind Kind, string AssetName, int Portrait, int SkinTone = 0)
{
    private static readonly Regex GenericPattern =
        new(@"^Generic_(\d+)_P_T\d+_[A-Za-z]+_(\d+)_\d+$", RegexOptions.Compiled);

    /// <summary>Lightest tone the game uses.</summary>
    public const int MinimumSkinTone = 1;

    /// <summary>Darkest tone the game uses.</summary>
    public const int MaximumSkinTone = 8;

    /// <summary>True when this is a real person's likeness.</summary>
    public bool IsRealPerson => Kind == HeadAssetKind.RealPersonScan;

    /// <summary>True when a usable tone was read off the name.</summary>
    public bool HasSkinTone => SkinTone is >= MinimumSkinTone and <= MaximumSkinTone;

    /// <summary>True when the value is one of EA's tones.</summary>
    public static bool IsValidSkinTone(int tone) =>
        tone is >= MinimumSkinTone and <= MaximumSkinTone;

    /// <summary>Classifies a raw head asset name.</summary>
    public static HeadAsset Parse(string? assetName)
    {
        if (string.IsNullOrWhiteSpace(assetName))
        {
            return new HeadAsset(HeadAssetKind.Unknown, "", 0);
        }

        var generic = GenericPattern.Match(assetName);
        if (generic.Success)
        {
            // The portrait id is repeated inside the name; a base save agrees
            // with PLYR_PORTRAIT on 7,243 of 7,244 rows, so the two must be
            // written together to stay consistent.
            return new HeadAsset(
                HeadAssetKind.Generic,
                assetName,
                int.Parse(generic.Groups[1].Value),
                int.Parse(generic.Groups[2].Value));
        }

        if (assetName.StartsWith("Unique_", StringComparison.Ordinal))
        {
            return new HeadAsset(HeadAssetKind.RealPersonScan, assetName, 0);
        }

        return assetName.Contains("CAF", StringComparison.OrdinalIgnoreCase)
            ? new HeadAsset(HeadAssetKind.Custom, assetName, 0)
            : new HeadAsset(HeadAssetKind.Unknown, assetName, 0);
    }
}
