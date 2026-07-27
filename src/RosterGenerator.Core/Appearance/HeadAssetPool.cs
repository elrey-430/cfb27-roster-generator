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
    private readonly Dictionary<int, IReadOnlyList<HeadAsset>> _byTone;

    private HeadAssetPool(IReadOnlyList<HeadAsset> faces)
    {
        _faces = faces;
        _byTone = faces
            .Where(f => f.HasSkinTone)
            .GroupBy(f => f.SkinTone)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<HeadAsset>)g.ToList());
    }

    /// <summary>How many distinct generated faces are available.</summary>
    public int Count => _faces.Count;

    /// <summary>True when there is nothing to draw from.</summary>
    public bool IsEmpty => _faces.Count == 0;

    /// <summary>Skin tones this export actually carries generated faces for.</summary>
    public IReadOnlyCollection<int> AvailableSkinTones => _byTone.Keys;

    /// <summary>How many distinct faces this export has at one skin tone.</summary>
    public int CountAtSkinTone(int tone) =>
        _byTone.TryGetValue(tone, out var faces) ? faces.Count : 0;

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
    public HeadAsset? Draw(int seed) => Draw(seed, preferredSkinTone: null);

    /// <summary>
    /// A face at a particular skin tone, falling back sensibly when the user's
    /// export cannot supply one.
    ///
    /// <para>Because a generated head is only ever used at one tone, choosing
    /// the face IS choosing the tone — nothing in the visuals table has to be
    /// written for this to take effect.</para>
    ///
    /// <para>The fallback ladder is deliberate. An exact tone is used when the
    /// export has faces at it. Otherwise the <b>nearest</b> tone is used, since
    /// EA numbers them lightest (1) to darkest (8) and an adjacent tone is the
    /// smallest possible miss; ties go to the darker of the two only because a
    /// tie has to break somewhere, and the report says what happened either
    /// way. If the export has no tone information at all, the whole pool is
    /// drawn from, which is exactly the behaviour before tones were understood.
    /// </para>
    /// </summary>
    /// <param name="seed">The player's row key, for a reproducible choice.</param>
    /// <param name="preferredSkinTone">
    /// EA's 1–8, or null to draw from the whole pool.
    /// </param>
    public HeadAsset? Draw(int seed, int? preferredSkinTone)
    {
        if (_faces.Count == 0)
        {
            return null;
        }

        if (preferredSkinTone is not int wanted || _byTone.Count == 0)
        {
            return _faces[(int)((uint)Mix(seed) % (uint)_faces.Count)];
        }

        var tone = _byTone.ContainsKey(wanted)
            ? wanted
            : _byTone.Keys.OrderBy(t => Math.Abs(t - wanted)).ThenByDescending(t => t).First();
        var candidates = _byTone[tone];
        return candidates[(int)((uint)Mix(seed) % (uint)candidates.Count)];
    }

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
