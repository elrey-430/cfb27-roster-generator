namespace RosterGenerator.Core.Legacy;

/// <summary>
/// What the legacy roster format's fields mean, and the handful of places
/// where the file's own column table lies about where they are.
/// </summary>
public static class LegacySchema
{
    /// <summary>The player table.</summary>
    public const string PlayerTable = "PLAY";

    /// <summary>The team table.</summary>
    public const string TeamTable = "TDYN";

    /// <summary>The depth chart table.</summary>
    public const string DepthChartTable = "DCHT";

    /// <summary>Player id, unique within the file and the key every other table points with.</summary>
    public const string PlayerId = "PGID";

    /// <summary>Team id.</summary>
    public const string TeamId = "TOID";

    /// <summary>Position, 0-20 — see <see cref="Positions"/>.</summary>
    public const string Position = "PPOS";

    /// <summary>Depth within a depth-chart position, 0 = starter.</summary>
    public const string DepthOrder = "ddep";

    /// <summary>Jersey number.</summary>
    public const string Jersey = "PJEN";

    /// <summary>Height in inches. Stored plainly, no offset.</summary>
    public const string Height = "PHGT";

    /// <summary>Weight, as pounds over <see cref="WeightOffsetPounds"/>.</summary>
    public const string Weight = "PWGT";

    /// <summary>Class year, 0-3.</summary>
    public const string ClassYear = "PYER";

    /// <summary>Skin tone, 0-7.</summary>
    public const string SkinTone = "PSKI";

    /// <summary>The game's own overall rating, compressed — see the remarks on <see cref="Overall"/>.</summary>
    public const string Overall = "POVR";

    /// <summary>Defensive captain's player id.</summary>
    public const string DefensiveCaptain = "DCAP";

    /// <summary>Offensive captain's player id.</summary>
    public const string OffensiveCaptain = "OCAP";

    /// <summary>
    /// What to add to a stored weight to get pounds. The same encoding CFB27
    /// uses twenty years later.
    /// </summary>
    public const int WeightOffsetPounds = 160;

    /// <summary>
    /// Positions in file order. Confirmed from the players themselves rather
    /// than from a community field list: tackles average 6'5"/299 lb against
    /// guards at 6'3"/301, corners 5'10"/182, and every one of the 21 slots
    /// lands where its measurables say it should.
    /// </summary>
    public static readonly IReadOnlyList<string> Positions = new[]
    {
        "QB", "HB", "FB", "WR", "TE", "LT", "LG", "C", "RG", "RT", "LE",
        "RE", "DT", "LOLB", "MLB", "ROLB", "CB", "FS", "SS", "K", "P",
    };

    /// <summary>Class years in file order.</summary>
    public static readonly IReadOnlyList<string> ClassYears = new[]
    {
        "Freshman", "Sophomore", "Junior", "Senior",
    };

    /// <summary>
    /// Name fields, in order: ten characters of first name, thirteen of last.
    /// </summary>
    public static readonly IReadOnlyList<string> FirstNameFields =
        Enumerable.Range(1, 10).Select(i => $"PF{i:00}").ToList();

    /// <summary>Last-name character fields.</summary>
    public static readonly IReadOnlyList<string> LastNameFields =
        Enumerable.Range(1, 13).Select(i => $"PL{i:00}").ToList();

    /// <summary>
    /// The legacy attributes that survive into CFB27, mapped to the rating
    /// column each one becomes. Eighteen of CFB27's fifty-seven; everything
    /// about route running, coverage, block shedding and the split throw
    /// accuracies simply did not exist as a separate number in 2004.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> AttributeMap =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PSPD"] = "SpeedRating",
            ["PACC"] = "AccelerationRating",
            ["PAGI"] = "AgilityRating",
            ["PSTR"] = "StrengthRating",
            ["PAWR"] = "AwarenessRating",
            ["PCTH"] = "CatchingRating",
            ["PCAR"] = "CarryingRating",
            ["PBTK"] = "BreakTackleRating",
            ["PJMP"] = "JumpingRating",
            ["PTAK"] = "TackleRating",
            ["PPBK"] = "PassBlockRating",
            ["PRBK"] = "RunBlockRating",
            ["PTHP"] = "ThrowPowerRating",
            ["PTHA"] = "ThrowAccuracyRating",
            ["PKPR"] = "KickPowerRating",
            ["PKAC"] = "KickAccuracyRating",
            ["PSTA"] = "StaminaRating",
            ["PINJ"] = "InjuryRating",
        };

    /// <summary>
    /// Body-shape fields that store negative numbers in two's complement.
    /// Nothing in the column table marks them: the declared type is the same
    /// 3 every other field carries, and they were found by a value of 15 in a
    /// four-bit field reading back as -1 in the community export.
    /// </summary>
    public static readonly IReadOnlySet<string> SignedFields =
        new HashSet<string>(StringComparer.Ordinal) { "PFSH", "PMSH", "PSSH" };

    /// <summary>
    /// Columns whose stored end offset does not point at their data, and where
    /// their bits really are.
    ///
    /// <para>The container's column table is otherwise exact — it tiles each
    /// record with no gap — but nine columns across two tables carry offsets
    /// that belong to a different column. They were recovered by searching
    /// every bit position for the one that reproduces a community CSV export,
    /// and the same corrections then read a second, unrelated roster file
    /// correctly, which is what makes them a property of the format rather
    /// than damage to one file.</para>
    ///
    /// <para>Four of them are fields this tool depends on completely: player
    /// id, height, weight, and team id.</para>
    /// </summary>
    private static readonly IReadOnlyDictionary<string, int> CorrectedStarts =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["PLAY.RCHD"] = 0,
            ["PLAY.PGID"] = 16,
            ["PLAY.PWGT"] = 32,
            ["PLAY.PHED"] = 206,
            ["PLAY.PHGT"] = 390,
            ["TDYN.DCAP"] = 0,
            ["TDYN.OCAP"] = 16,
            ["TDYN.TRRB"] = 56,
            ["TDYN.TROL"] = 88,
            ["TDYN.TOID"] = 120,
        };

    /// <summary>
    /// Returns a column at the offset it is really stored at.
    /// </summary>
    internal static LegacyField Correct(string table, string field, int startBit, int bits) =>
        new(field,
            CorrectedStarts.TryGetValue($"{table}.{field}", out var corrected) ? corrected : startBit,
            bits);

    /// <summary>
    /// Decodes one of the character codes a name is stored as: 1-26 are
    /// lower case, 27-52 upper, and four codes carry punctuation. 0 ends the
    /// name.
    /// </summary>
    public static char? DecodeNameCharacter(int code) => code switch
    {
        >= 1 and <= 26 => (char)('a' + code - 1),
        >= 27 and <= 52 => (char)('A' + code - 27),
        53 => '.',
        54 => '\'',
        55 => '.',
        56 => ' ',
        _ => null,
    };

    /// <summary>Reads a name out of its per-character fields.</summary>
    public static string DecodeName(LegacyTable table, int record, IReadOnlyList<string> fields)
    {
        var text = new System.Text.StringBuilder(fields.Count);
        foreach (var field in fields)
        {
            if (!table.Has(field))
            {
                break;
            }

            var code = table.Read(record, field);
            if (code == 0)
            {
                break;
            }

            if (DecodeNameCharacter(code) is char character)
            {
                text.Append(character);
            }
        }

        return text.ToString().Trim();
    }
}
