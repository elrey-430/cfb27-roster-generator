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
    /// <c>Generic_&lt;portrait&gt;_P_T&lt;tone&gt;_&lt;family&gt;_&lt;a&gt;_&lt;b&gt;</c>.
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
/// </summary>
public readonly record struct HeadAsset(HeadAssetKind Kind, string AssetName, int Portrait)
{
    private static readonly Regex GenericPattern =
        new(@"^Generic_(\d+)_P_T\d+_[A-Za-z]+_\d+_\d+$", RegexOptions.Compiled);

    /// <summary>True when this is a real person's likeness.</summary>
    public bool IsRealPerson => Kind == HeadAssetKind.RealPersonScan;

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
                HeadAssetKind.Generic, assetName, int.Parse(generic.Groups[1].Value));
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
