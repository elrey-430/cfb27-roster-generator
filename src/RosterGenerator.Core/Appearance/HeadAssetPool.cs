using RosterGenerator.Core.Model;
using RosterGenerator.Core.Schema;

namespace RosterGenerator.Core.Appearance;

/// <summary>
/// The generated faces already present in the user's own dynasty, used to
/// replace a real person's likeness when a historical player takes over their
/// roster slot.
///
/// <para><b>Why this exists.</b> Replacing a player keeps the slot's head, and
/// 9,011 of 16,257 players in a base save wear a <c>Unique_</c> scan of a real
/// person — 71 of the 85 slots on a typical team. Left alone, most of a
/// recreated 1985 roster ends up wearing the recognisable faces of real
/// present-day players under other people's names. A generated face is not a
/// perfect likeness of the historical player either, but it is not a specific
/// living person being mislabelled.</para>
///
/// <para><b>Nothing is invented.</b> The replacements are faces already in the
/// same save, so every value written is one the game demonstrably carries —
/// the same rule the equipment layer follows.</para>
/// </summary>
public sealed class HeadAssetPool
{
    private readonly IReadOnlyList<HeadAsset> _faces;

    private HeadAssetPool(IReadOnlyList<HeadAsset> faces)
    {
        _faces = faces;
    }

    /// <summary>How many distinct generated faces are available.</summary>
    public int Count => _faces.Count;

    /// <summary>True when there is nothing to draw from.</summary>
    public bool IsEmpty => _faces.Count == 0;

    /// <summary>
    /// Collects every distinct generated face in the roster, in a stable
    /// order. Scans and create-a-face heads are skipped: the point is to have
    /// faces that belong to nobody in particular.
    /// </summary>
    public static HeadAssetPool Build(PlayerRoster roster)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var faces = new List<HeadAsset>();

        foreach (var player in roster.Players)
        {
            if (!player.HasColumn(PlayerColumns.GenericHeadAssetName))
            {
                break;
            }

            var head = HeadAsset.Parse(player.GetRaw(PlayerColumns.GenericHeadAssetName));
            if (head.Kind == HeadAssetKind.Generic && seen.Add(head.AssetName))
            {
                faces.Add(head);
            }
        }

        // Ordered by name so the pool does not depend on row order, which
        // keeps a run reproducible across differently sorted exports.
        faces.Sort((a, b) => string.CompareOrdinal(a.AssetName, b.AssetName));
        return new HeadAssetPool(faces);
    }

    /// <summary>
    /// A face for the player in <paramref name="seed"/> — their row key — so
    /// the same roster always produces the same faces, and two players on one
    /// team rarely share one.
    /// </summary>
    public HeadAsset? Draw(int seed) =>
        _faces.Count == 0 ? null : _faces[(int)((uint)Mix(seed) % (uint)_faces.Count)];

    // A row key is a small ascending integer, so using it directly would hand
    // adjacent slots adjacent faces. Mixing spreads them across the pool
    // without giving up determinism.
    private static int Mix(int value)
    {
        unchecked
        {
            var x = (uint)value * 2654435761u;
            x ^= x >> 15;
            x *= 2246822519u;
            x ^= x >> 13;
            return (int)(x & 0x7FFFFFFF);
        }
    }
}
