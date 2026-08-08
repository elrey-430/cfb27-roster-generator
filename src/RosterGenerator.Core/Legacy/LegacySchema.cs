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

    /// <summary>The PS3-era team table, which names its teams in plain text.</summary>
    public const string ModernTeamTable = "TEAM";

    /// <summary>Team name, e.g. "Arizona State".</summary>
    public const string TeamName = "TDNA";

    /// <summary>Team short name, e.g. "ASU". Used as a fallback alias.</summary>
    public const string TeamShortName = "TSNA";

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

    /// <summary>PS3-era first name, held as plain text rather than a column per letter.</summary>
    public const string FirstNameText = "PFNA";

    /// <summary>PS3-era last name, held as plain text.</summary>
    public const string LastNameText = "PLNA";

    /// <summary>PS3-era team id, carried on the player rather than inferred.</summary>
    public const string PlayerTeamId = "TGID";

    /// <summary>PS3-era class year, 0-3. Not to be confused with PYRS, which is Press.</summary>
    public const string ModernClassYear = "PYEA";

    /// <summary>PS3-era redshirt marker.</summary>
    public const string Redshirt = "PRSD";

    /// <summary>
    /// The attributes a PS3-era file adds over a PS2-era one, mapped to the
    /// CFB27 rating each becomes.
    ///
    /// <para>Every one of these was checked against the position that ought to
    /// lead it: man coverage runs corners 81 to a centre's 34, block shedding
    /// tackles 75 to a receiver's 37, spectacular catch receivers 68 to a
    /// centre's 19. Twenty-one of twenty-one came out in the predicted
    /// direction by at least 28 points, which is what turns a reading of the
    /// four-character code into a mapping worth relying on.</para>
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> ModernAttributeMap =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PBCV"] = "BCVisionRating",
            ["PYRS"] = "PressRating",
            ["PBFW"] = "RunBlockFinesseRating",
            ["PBSH"] = "BlockSheddingRating",
            ["PPRC"] = "PlayRecognitionRating",
            ["PMCV"] = "ManCoverageRating",
            ["PZCV"] = "ZoneCoverageRating",
            ["PFMV"] = "FinesseMovesRating",
            ["PPMV"] = "PowerMovesRating",
            ["PJMV"] = "JukeMoveRating",
            ["PSMV"] = "SpinMoveRating",
            ["PTRK"] = "TruckingRating",
            ["PSAR"] = "StiffArmRating",
            ["PHIT"] = "HitPowerRating",
            ["PIBL"] = "ImpactBlockingRating",
            ["PPRS"] = "PursuitRating",
            ["RELS"] = "ReleaseRating",
            ["PPBF"] = "PassBlockFinesseRating",
            ["PPBS"] = "PassBlockPowerRating",
            ["PRBS"] = "RunBlockPowerRating",
            ["SPCT"] = "SpectacularCatchRating",
            ["TRAF"] = "CatchInTrafficRating",
            ["PRTR"] = "RouteRunningRating",
            ["PKRT"] = "KickReturnRating",
        };

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
    /// Every CFB27 rating column a PS3-era roster can supply a real 0-99 value
    /// for, in write order. Forty-two of CFB27's fifty-seven.
    ///
    /// <para>Two of these are not CFB27 columns at all — the later game holds
    /// one throw accuracy and one route running where CFB27 holds three of
    /// each. They are carried under their own names and spread across the
    /// three when a player is generated, because a number somebody typed once
    /// is evidence about all three together and about none of them
    /// separately.</para>
    /// </summary>
    public static readonly IReadOnlyList<string> SourceRatingColumns =
        AttributeMap.Values.Concat(ModernAttributeMap.Values)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(v => v, StringComparer.Ordinal)
            .ToList();

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
