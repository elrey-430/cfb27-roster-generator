using RosterGenerator.Core.Editing;
using RosterGenerator.Core.Historical;
using RosterGenerator.Core.Mapping;
using RosterGenerator.Core.Model;
using RosterGenerator.Core.Rating;
using RosterGenerator.Core.Schema;

namespace RosterGenerator.Core.Conversion;

/// <summary>
/// Converts a historical roster into CFB27 form by replacing one team's
/// players inside a loaded donor roster (a real save's Player table).
///
/// Replacement — rather than row creation — is deliberate: the Player table
/// has ~250 columns with unconfirmed semantics that only a real save can
/// supply, so each historical player takes over an existing roster slot via
/// the replace-identity operation and inherits the slot's unknown fields.
/// Confirmed-safe fields (name, jersey, height, weight, class, redshirt,
/// position, hometown) are overwritten with historical values; ratings and
/// archetype are generated when an engine is supplied and otherwise inherited
/// from the slot. Slots no historical player fills are handed to the
/// <see cref="RosterFiller"/>, without which the original fictional players
/// stay and some of them start. Every default and assumption is recorded in
/// the <see cref="ConversionReport"/>.
/// </summary>
public sealed class HistoricalTeamConverter
{
    private readonly TeamMappingSet _teamMappings;
    private readonly PositionMappingSet _positionMappings;
    private readonly RatingEngine? _ratingEngine;
    private readonly ArchetypeSelector? _archetypeSelector;
    private readonly AbilityModel? _abilities;
    private readonly Appearance.BodyTypeModel? _bodyTypes;
    private readonly RosterFiller? _rosterFiller;
    private readonly bool _replaceRealPersonFaces;
    private readonly Equipment.CharacterVisualsTable? _characterVisuals;
    private Appearance.HeadAssetPool? _faces;
    private readonly RosterDepthModel? _depth;
    private readonly TeamMappingSet? _previousSchools;
    private readonly CommentaryIdSet _commentaryIds;

    /// <summary>
    /// Creates a converter.
    /// </summary>
    /// <param name="teamMappings">School name → team index lookup.</param>
    /// <param name="positionMappings">Historical → CFB27 position lookup.</param>
    /// <param name="ratingEngine">
    /// Rating engine to generate attributes with. When null, each player
    /// inherits the ratings of the roster slot they replace.
    /// </param>
    /// <param name="archetypeSelector">
    /// Chooses each player's archetype from their historical profile. When
    /// null the roster slot's existing archetype is kept.
    /// </param>
    /// <param name="rosterFiller">
    /// Re-rates the slots the historical roster does not fill so the original
    /// fictional players cannot out-rate it. When null they are left alone and
    /// only reported. Requires <paramref name="ratingEngine"/>.
    /// </param>
    /// <param name="rosterDepth">
    /// Measured roster shape. Supplied, players the user gave little evidence
    /// for are rated as members of this program rather than of an average one.
    /// </param>
    /// <param name="previousSchoolMappings">
    /// School name → <c>TEAM_ORIGID</c> lookup
    /// (<see cref="Dynasty.DynastyExport.BuildPreviousSchoolMappings"/>). When
    /// null, transfers' previous schools are not written.
    /// </param>
    /// <param name="replaceRealPersonFaces">
    /// When true, a donor slot carrying a real person's head scan is given a
    /// generated face instead, so a recreated player never wears the likeness
    /// of somebody who is not them.
    /// </param>
    /// <param name="characterVisuals">
    /// The export's CharacterVisuals table, read only, so a face swap can keep
    /// the skin tone the roster slot already had. A real person's scan does
    /// not spell its tone out in its name the way a generated head does, and
    /// this is the only place to read it. Null simply means no preference.
    /// </param>
    /// <param name="commentaryIds">
    /// Surname → commentary index, so the announcers say the player's own name
    /// rather than the name of whoever held the slot. Null or empty leaves the
    /// field exactly as it was: "we know nothing" is not the same as "the name
    /// cannot be said", and zeroing it would silence a roster over a missing
    /// data file.
    /// </param>
    /// <param name="abilities">
    /// How good a player is in the ability slots their archetype gives them,
    /// measured from a base save. Null leaves every slot exactly as the
    /// replaced player left it — which means a recreated walk-on can inherit
    /// somebody else's gold, so the pipeline supplies this whenever ratings are
    /// generated.
    /// </param>
    /// <param name="bodyTypes">
    /// Chooses each player's body build from their position, height and weight.
    /// Null leaves the build the replaced player had, which on a slot swap is
    /// somebody else's body.
    /// </param>
    public HistoricalTeamConverter(
        TeamMappingSet teamMappings,
        PositionMappingSet positionMappings,
        RatingEngine? ratingEngine = null,
        ArchetypeSelector? archetypeSelector = null,
        RosterFiller? rosterFiller = null,
        RosterDepthModel? rosterDepth = null,
        TeamMappingSet? previousSchoolMappings = null,
        bool replaceRealPersonFaces = true,
        Equipment.CharacterVisualsTable? characterVisuals = null,
        CommentaryIdSet? commentaryIds = null,
        AbilityModel? abilities = null,
        Appearance.BodyTypeModel? bodyTypes = null)
    {
        _bodyTypes = bodyTypes;
        _abilities = abilities;
        _commentaryIds = commentaryIds ?? CommentaryIdSet.Empty;
        _replaceRealPersonFaces = replaceRealPersonFaces;
        _characterVisuals = characterVisuals;
        _teamMappings = teamMappings;
        _positionMappings = positionMappings;
        _ratingEngine = ratingEngine;
        _archetypeSelector = archetypeSelector;
        _rosterFiller = rosterFiller;
        _depth = rosterDepth;
        _previousSchools = previousSchoolMappings;
    }

    /// <summary>
    /// Applies the historical roster to the donor roster's matching team.
    /// Edits are made through <paramref name="session"/> so validation can
    /// verify them; the caller exports afterwards.
    /// </summary>
    public ConversionReport Convert(RosterEditSession session, HistoricalRoster historical)
    {
        var roster = session.Roster;
        var teamId = _teamMappings.Resolve(historical.School);
        var report = new ConversionReport(historical, teamId);

        report.GlobalAssumptions.Add(_ratingEngine is null
            ? "Ratings are inherited from the roster slot each player replaces (rating generation disabled)."
            : "Ratings are generated from each player's historical evidence and calibrated so EA's own overall " +
              "formula reproduces the intended overall; see Ratings/Rating_Model.md.");
        report.GlobalAssumptions.Add(
            "Weight is written using the confirmed encoding (stored value = pounds − 160, representable " +
            "range 160–400 lb); weights outside that range or missing from the dataset inherit the donor " +
            "slot's weight.");
        report.GlobalAssumptions.Add(_replaceRealPersonFaces
            ? "A roster slot carrying a real person's head scan is given a generated face taken from this " +
              "same export, so no recreated player wears a living player's likeness under another name. " +
              "The replacement keeps the slot's own skin tone unless the roster CSV's optional SkinTone " +
              "column asked for a different one; a tone is never inferred from a name or a hometown."
            : "Identity asset fields (PLYR_ASSETNAME, GenericHeadAssetName, PLYR_PORTRAIT) keep the donor " +
              "slot's values, so recreated players wear the faces of the players they replaced.");
        report.GlobalAssumptions.Add(
            "Hometown is written: PLYR_HOME_TOWN takes the town as free text and PLYR_HOME_STATE the " +
            "matching state from the save's 51-value enum (NonUS for anything not a US state).");
        report.GlobalAssumptions.Add(_abilities is null
            ? "Ability slots keep whatever the replaced player had, so a recreated player may carry the " +
              "previous occupant's abilities."
            : "Ability tiers are set from each player's overall and their archetype's own slots, measured " +
              "from a base save. The save stores a tier per slot and never names the ability — which slot " +
              "is which ability is decided by position and archetype in the game's own data — so this " +
              "sets how good a player is in the slots they already have, not which abilities they get.");
        report.GlobalAssumptions.Add(_archetypeSelector is null
            ? "Player archetype (PlayerType) is inherited from the roster slot each player replaces."
            : "Player archetype (PlayerType) is chosen from each player's historical profile and the " +
              "overall rating is recomputed with that archetype's EA formula, so the two always agree.");
        report.GlobalAssumptions.Add(_previousSchools is null
            ? "Transfers' previous schools are not written; each player keeps the donor slot's value."
            : "PreviousSchool is written to PLYR_PREVTEAMID as that school's TEAM_ORIGID, and cleared to 0 " +
              "for players who did not transfer. A school your dynasty does not carry is recorded as " +
              $"{PlayerSchema.PrevTeamIdNotInDynasty}, the value real FCS transfers carry.");
        report.GlobalAssumptions.Add(
            "Slot assignment prefers a donor slot at the same position (or an interchangeable one, e.g. " +
            "LE/RE); players placed in an unrelated slot get an explicit position change.");
        report.GlobalAssumptions.Add(_bodyTypes is null
            ? "Body build (CharacterBodyType) keeps whatever the replaced player had, so a recreated " +
              "player can be standing in somebody else's body."
            : "Body build (CharacterBodyType) is chosen from position, height and weight — no input is " +
              "asked of you. Positions whose build is not in question take it outright (ends and tackles " +
              "Muscular, interior line and defensive tackle Heavy, measured from a base save); the rest " +
              "choose among the builds EA's own player builder allows at that height and weight. The " +
              "build the game calls Lean is stored as 'Freshman'.");
        report.GlobalAssumptions.Add(
            "Every generated player is written with IsNIL = false. That flag marks a real person who " +
            "signed an NIL agreement to appear under their own name, and the game will not let such a " +
            "player be edited — so a recreated player must not inherit it, both because they are not that " +
            "person and because it would leave them locked. The separate NIL money fields are not " +
            "touched; they do not move with the flag in the game's own data.");

        // Donor slots for this team, position-preferred assignment.
        var slots = roster.Players
            .Where(p => p.TeamIndex == teamId)
            .OrderBy(p => p.RowKey)
            .ToList();
        var freeSlots = new List<Player>(slots);
        var placements = new List<(Player Slot, HistoricalPlayer Historical, PlayerConversionEntry Entry)>();

        foreach (var historicalPlayer in historical.Players)
        {
            var entry = new PlayerConversionEntry(historicalPlayer);
            report.Entries.Add(entry);

            if (!_positionMappings.TryResolve(historicalPlayer.Position, out var targetPosition))
            {
                entry.Warnings.Add(
                    $"Position '{historicalPlayer.Position}' has no mapping — player skipped. " +
                    "Add it to PositionMappings.json.");
                continue;
            }

            var slot = freeSlots.FirstOrDefault(s => s.Position == targetPosition)
                       ?? freeSlots.FirstOrDefault(s => _positionMappings.AreInterchangeable(s.Position, targetPosition))
                       ?? freeSlots.FirstOrDefault();
            if (slot is null)
            {
                entry.Warnings.Add("No donor roster slot left on the team — player skipped.");
                continue;
            }

            freeSlots.Remove(slot);
            entry.AssignedRowKey = slot.RowKey;
            ApplyPlayer(session, slot, historicalPlayer, targetPosition, entry);
            placements.Add((slot, historicalPlayer, entry));
        }

        if (_ratingEngine is not null)
        {
            GenerateRatings(session, report, placements);
        }

        if (freeSlots.Count > 0)
        {
            if (_rosterFiller is not null)
            {
                FillRemainingSlots(session, report, freeSlots, placements);
            }
            else
            {
                ReportUnfilledSlots(report, freeSlots);
            }
        }

        // Reported once for the roster rather than once per player: the
        // commentary has no recording of roughly a third of surnames, so a
        // per-player note would bury everything else in the report.
        var named = report.Converted.Count(e => e.CommentaryId != CommentaryIdSet.None);
        var unnamed = report.Converted.Count() - named;
        report.GlobalAssumptions.Add(_commentaryIds.Count == 0
            ? "Commentary was left as the replaced players had it, so the announcers will use their " +
              "names. data/CommentaryIds.json is missing."
            : $"Commentary follows each player's surname, as the game does it on a rename: " +
              $"{named} of {named + unnamed} will be named by the announcers. The other {unnamed} " +
              "have a surname the commentary has no recording of and are left unnamed rather than " +
              "called by the name of whoever held their slot.");

        return report;
    }

    /// <summary>
    /// Turns the slots the historical roster did not fill into end-of-roster
    /// depth. The ceiling handed to the filler is the weakest historical
    /// player at each position, which is what actually keeps a leftover
    /// fictional player off the depth chart.
    /// </summary>
    private void FillRemainingSlots(
        RosterEditSession session,
        ConversionReport report,
        IReadOnlyList<Player> freeSlots,
        IReadOnlyList<(Player Slot, HistoricalPlayer Historical, PlayerConversionEntry Entry)> placements)
    {
        var weakestByPosition = placements
            .GroupBy(p => p.Slot.Position, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Min(p => p.Slot.OverallRating), StringComparer.Ordinal);

        var filled = _rosterFiller!.Fill(session, freeSlots, weakestByPosition, placements.Count);
        report.FilledSlots.AddRange(filled);

        // A filled slot is re-rated as depth, so everything that was true of it
        // because of the rating it used to have has to be re-decided too.
        //
        // Abilities are the case this was first found on: leaving them let the
        // previous occupant's gold survive on a walk-on the filler had just
        // rated at 63, invisible unless somebody diffed the slot against the
        // save it came from. IsNIL is the same shape of mistake — a re-rated
        // walk-on still marked as the real person who used to hold the slot,
        // and locked against editing with them — and it is cleared whether or
        // not an ability model was loaded.
        var byRowKey = freeSlots.ToDictionary(s => s.RowKey);
        foreach (var slot in filled)
        {
            if (!byRowKey.TryGetValue(slot.RowKey, out var player))
            {
                continue;
            }

            session.SetNilStatus(player, false);
            ApplyBodyType(session, player, entry: null);
            if (_abilities is not null)
            {
                session.SetAbilities(player, _abilities.For(
                    player.GetRaw(PlayerTypeColumn), player.Position, slot.Overall, slot.RowKey));
            }
        }

        report.GlobalAssumptions.Add(
            $"{freeSlots.Count} roster slot(s) had no historical player, so they were re-rated as " +
            "end-of-roster depth using the overall a real save carries at those roster ranks " +
            "(data/RosterDepth.json), each held below the weakest historical player at its position. " +
            "Their names, jersey numbers and portraits are unchanged.");
    }

    /// <summary>
    /// Records slots that kept their original fictional players, including how
    /// many of them are good enough to take a starting job.
    /// </summary>
    private static void ReportUnfilledSlots(ConversionReport report, IReadOnlyList<Player> freeSlots)
    {
        foreach (var slot in freeSlots)
        {
            report.LeftoverDonorSlots.Add($"{slot} — {slot.Position}, OVR {slot.OverallRating}");
        }

        var starters = freeSlots.Count(p => p.OverallRating >= StarterOverallThreshold);
        report.GlobalWarnings.Add(
            $"{freeSlots.Count} roster slot(s) were not replaced, so that many original players remain " +
            $"on the team (listed below){(starters > 0 ? $"; {starters} of them rate {StarterOverallThreshold}+ " +
            "and may appear ahead of your historical players on the depth chart" : "")}. " +
            "Supply more players in the roster CSV, or enable the roster fill, to replace them.");
    }

    /// <summary>
    /// Generates and writes ratings for every placed player, then runs the
    /// roster-level depth-consistency pass and regenerates anyone it caps.
    /// </summary>
    private void GenerateRatings(
        RosterEditSession session,
        ConversionReport report,
        IReadOnlyList<(Player Slot, HistoricalPlayer Historical, PlayerConversionEntry Entry)> placements)
    {
        var engine = _ratingEngine!;
        var rated = new List<RatedPlayer>();
        var byPlayer = new Dictionary<HistoricalPlayer, (Player Slot, PlayerConversionEntry Entry)>();

        // The donor roster already encodes how strong the program is, so a
        // player the user gave no evidence for can still be rated as a member
        // of THIS team rather than of an average one.
        var programAdjustment = _depth?.ProgramAdjustment(
            session.Roster.Players.Where(p => p.TeamIndex == report.TeamId).Select(p => p.OverallRating)) ?? 0;
        if (programAdjustment != 0)
        {
            report.GlobalAssumptions.Add(
                $"The team's existing roster rates {Math.Abs(programAdjustment)} point(s) " +
                $"{(programAdjustment > 0 ? "above" : "below")} a typical program, so players you supplied " +
                "little evidence for are rated as members of this team rather than of an average one. " +
                "Players with a draft slot, awards or a stat line are unaffected.");
        }

        foreach (var (slot, historical, entry) in placements)
        {
            // A blank Role is fine and changes nothing, but a misspelled one
            // would be dropped in silence — looking exactly like leaving it
            // out, while the user believes they set it.
            if (historical.Evidence.Role is { Length: > 0 } role &&
                !string.IsNullOrWhiteSpace(role) &&
                !engine.IsKnownRole(role))
            {
                entry.Warnings.Add(
                    $"Role '{role.Trim()}' is not one the tool recognizes, so it was ignored. " +
                    $"Use one of: {string.Join(", ", engine.KnownRoles)}.");
            }

            var playerType = slot.GetRaw(PlayerTypeColumn);
            if (_archetypeSelector is not null)
            {
                var choice = _archetypeSelector.Select(slot.Position, historical, historical.Evidence);
                if (choice.Archetype != playerType)
                {
                    // Writing PlayerType changes which EA formula computes the
                    // overall, so the rating generated below MUST use the new
                    // archetype — that recompute is what keeps the record
                    // coherent.
                    session.SetPlayerType(slot, choice.Archetype);
                    entry.Warnings.Add($"Archetype {playerType} -> {choice.Archetype}: {choice.Reason}");
                    playerType = choice.Archetype;
                }

                entry.Archetype = choice;
            }

            var ratings = engine.Generate(
                slot.Position, playerType, historical, historical.Evidence,
                programAdjustment: programAdjustment);
            rated.Add(new RatedPlayer(historical, slot.Position, ratings.PlayerType, ratings));
            byPlayer[historical] = (slot, entry);
        }

        // Roster-level rule: players the file gives nothing but a role are
        // spread across the range the game itself carries for that role,
        // instead of every one of them landing on the same number. Runs before
        // the depth check so that check sees the ratings the roster will
        // actually carry.
        foreach (var (player, overall, reason) in RoleSpread.Plan(rated, engine.Model))
        {
            var (slot, entry) = byPlayer[player.Player];
            var regenerated = engine.Generate(
                slot.Position, slot.GetRaw(PlayerTypeColumn), player.Player, player.Player.Evidence,
                programAdjustment: programAdjustment, overallOverride: overall);
            var index = rated.FindIndex(r => ReferenceEquals(r.Player, player.Player));
            rated[index] = player with { Ratings = regenerated };
            entry.Warnings.Add(reason);
        }

        // Roster-level rule: a backup must not out-rate the starter without
        // strong individual evidence. Violators are regenerated under a cap.
        foreach (var (violator, ceiling, reason) in DepthConsistency.FindViolations(rated))
        {
            var (slot, entry) = byPlayer[violator.Player];
            var regenerated = engine.Generate(
                slot.Position, slot.GetRaw(PlayerTypeColumn), violator.Player, violator.Player.Evidence, ceiling,
                programAdjustment);
            var index = rated.FindIndex(r => ReferenceEquals(r.Player, violator.Player));
            rated[index] = violator with { Ratings = regenerated };
            entry.Warnings.Add(reason);
        }

        foreach (var player in rated)
        {
            var (slot, entry) = byPlayer[player.Player];
            session.SetGeneratedRatings(slot, player.Ratings.Attributes, player.Ratings.Overall);
            entry.Ratings = player.Ratings;

            // Abilities come last, because they are read off the overall this
            // loop has just written — including the one the depth-consistency
            // pass may have capped. Doing it earlier would rate a player on a
            // number that then changed.
            if (_abilities is not null)
            {
                var abilities = _abilities.For(
                    slot.GetRaw(PlayerTypeColumn), slot.Position, player.Ratings.Overall, slot.RowKey);
                session.SetAbilities(slot, abilities);
                entry.Abilities = abilities;
            }

            if (player.Ratings.Confidence == RatingConfidence.Low)
            {
                entry.Warnings.Add(
                    "Ratings generated with Low confidence — supply stats, awards, a draft slot or a " +
                    "recruiting rating for a better estimate.");
            }

            foreach (var adjustment in player.Ratings.Adjustments)
            {
                entry.Warnings.Add(adjustment);
            }
        }
    }

    /// <summary>Column holding the archetype whose EA overall formula applies.</summary>
    private const string PlayerTypeColumn = "PlayerType";

    /// <summary>
    /// Overall at which a leftover original player is likely to out-rate a
    /// generated one and show up on the depth chart, making the roster look
    /// wrong. Used only to sharpen the report's warning.
    /// </summary>
    private const int StarterOverallThreshold = 75;

    /// <summary>
    /// Returns a value only when it is present AND inside the range the game
    /// accepts; otherwise reports it and returns null so the caller falls back
    /// to the donor slot.
    ///
    /// Every field here is optional, and a user filling one in from a sparse
    /// source will sometimes get it wrong. A typo must degrade to exactly what
    /// a blank cell does — inherit and say so — because the alternative is
    /// writing a value the validator then rejects, which fails the whole
    /// export and produces no file at all. One mistyped jersey number is not a
    /// reason to hand back nothing for an 85-player roster.
    /// </summary>
    /// <summary>
    /// Decides which face the replaced player wears.
    ///
    /// <para>The donor slot's head is kept unless it is a real person's scan.
    /// 71 of the 85 slots on a typical team carry one, so leaving them puts
    /// most of a recreated roster in the recognisable faces of present-day
    /// players under other people's names. Those slots are given a generated
    /// face drawn from the same save — never an invented asset name — and the
    /// substitution is reported like every other.</para>
    ///
    /// <para>The scan's <c>PLYR_ASSETNAME</c> goes with it: the column is set
    /// on all 9,011 scanned players and blank on 4,100 generated ones, so
    /// clearing it is both attested and the thing that severs the last link to
    /// the real person.</para>
    ///
    /// <para><b>Skin tone rides along with the face.</b> A generated head is
    /// only ever used at one tone, so picking the face picks the tone and
    /// nothing in the visuals table has to be written. Which tone is wanted
    /// comes from two places, in this order: the optional <c>SkinTone</c>
    /// column, when the user supplied one; otherwise the tone the roster slot
    /// already had, so swapping a real person's scan for a generated face does
    /// not also change how the player looks. The tone is never inferred from a
    /// name, a hometown or a position.</para>
    /// </summary>
    private (string AssetName, string HeadAssetName, string Portrait) ChooseFace(
        RosterEditSession session, Player slot, HistoricalPlayer historicalPlayer,
        PlayerConversionEntry entry)
    {
        var inherited = (
            AssetName: slot.GetRaw(PlayerColumns.AssetName),
            HeadAssetName: slot.GetRaw(PlayerColumns.GenericHeadAssetName),
            Portrait: slot.GetRaw(PlayerColumns.Portrait));

        var slotHead = Appearance.HeadAsset.Parse(inherited.HeadAssetName);
        var requested = historicalPlayer.SkinTone;

        // Nothing to do when the slot's face is already acceptable and the
        // user did not ask for a particular appearance.
        if (requested is null && (!_replaceRealPersonFaces || !slotHead.IsRealPerson))
        {
            return inherited;
        }

        // A slot the user asked to leave alone stays alone, even with a tone
        // requested: --faces inherit means inherit.
        if (!_replaceRealPersonFaces)
        {
            if (requested is int ignored)
            {
                entry.Warnings.Add(
                    $"SkinTone {ignored} was not applied because faces are being inherited " +
                    "(--faces inherit).");
            }

            return inherited;
        }

        // Already the right tone: changing the face would be churn.
        if (requested is int want && slotHead.Kind == Appearance.HeadAssetKind.Generic &&
            slotHead.SkinTone == want)
        {
            return inherited;
        }

        var wanted = requested ?? SlotSkinTone(slot, slotHead);

        _faces ??= Appearance.HeadAssetPool.Build(session.Roster);
        var replacement = _faces.Draw(slot.RowKey, wanted);
        if (replacement is null)
        {
            entry.Warnings.Add(
                "kept the replaced player's head: it is a real person's likeness, but this export " +
                "carries no generated faces to use instead.");
            return inherited;
        }

        var got = replacement.Value.SkinTone;
        var toneNote = requested is int asked
            ? asked == got
                ? $", at the skin tone {asked} you asked for"
                : $", at skin tone {got} — this export carries no generated face at the {asked} " +
                  "you asked for, so the nearest available was used"
            : wanted is int kept && kept == got
                ? $", keeping the slot's own skin tone ({kept})"
                : "";

        if (slotHead.IsRealPerson)
        {
            entry.DefaultsUsed.Add(
                $"face: the slot carried a real player's likeness ({inherited.HeadAssetName}), " +
                $"replaced with a generated one ({replacement.Value.AssetName}){toneNote}.");
        }
        else
        {
            entry.DefaultsUsed.Add(
                $"face: changed to {replacement.Value.AssetName}{toneNote}.");
        }

        return ("", replacement.Value.AssetName, replacement.Value.Portrait.ToString());
    }

    /// <summary>
    /// Writes the body build for a slot that has already been given its final
    /// position, height and weight.
    ///
    /// <para>A build is never guessed from position alone: with no usable
    /// height or weight the slot keeps what it had, which is at least a build
    /// the game itself put on a player of that size.</para>
    /// </summary>
    private void ApplyBodyType(RosterEditSession session, Player slot, PlayerConversionEntry? entry)
    {
        if (_bodyTypes?.For(slot.Position, slot.HeightInches, slot.WeightPounds) is not { } build)
        {
            return;
        }

        if (!string.Equals(slot.GetRaw(PlayerColumns.CharacterBodyType), build, StringComparison.Ordinal))
        {
            session.SetBodyType(slot, build);
        }

        if (entry is not null)
        {
            entry.BodyType = build;
        }
    }

    /// <summary>
    /// The skin tone the roster slot already has, so a face swap does not
    /// change a player's appearance as a side effect.
    ///
    /// <para>A generated head spells its tone out in its own name. A real
    /// person's scan does not, so the tone is read from the slot's
    /// CharacterVisuals row when the export carries one. Null means "no
    /// preference", and the face is then drawn from the whole pool exactly as
    /// it was before tones were understood.</para>
    /// </summary>
    private int? SlotSkinTone(Player slot, Appearance.HeadAsset slotHead)
    {
        if (slotHead.HasSkinTone)
        {
            return slotHead.SkinTone;
        }

        if (_characterVisuals is null || !slot.HasColumn(PlayerColumns.CharacterVisuals))
        {
            return null;
        }

        var rowId = Equipment.CharacterVisualsReference.RowId(
            slot.GetRaw(PlayerColumns.CharacterVisuals));
        return rowId is int row ? _characterVisuals.GetSkinTone(row) : null;
    }

    private static int? Usable(
        int? value, int min, int max, string label, string inherited, PlayerConversionEntry entry)
    {
        if (value is not int number)
        {
            entry.MissingFields.Add(label);
            entry.DefaultsUsed.Add($"{label}: {inherited} (inherited from donor slot)");
            return null;
        }

        if (number < min || number > max)
        {
            entry.Warnings.Add(
                $"{label} {number} is outside the {min}–{max} the game accepts — check the roster CSV.");
            entry.DefaultsUsed.Add($"{label}: {inherited} (inherited from donor slot)");
            return null;
        }

        return number;
    }

    private void ApplyPlayer(
        RosterEditSession session,
        Player slot,
        HistoricalPlayer historicalPlayer,
        string targetPosition,
        PlayerConversionEntry entry)
    {
        // Identity: replace-with-real-player semantics. The donor slot's
        // asset values are passed back unchanged unless the slot carried a
        // real person's head scan — see ChooseFace.
        var face = ChooseFace(session, slot, historicalPlayer, entry);
        session.ReplacePlayerIdentity(
            slot,
            historicalPlayer.FirstName,
            historicalPlayer.LastName,
            assetName: face.AssetName,
            genericHeadAssetName: face.HeadAssetName,
            portrait: face.Portrait);

        // IsNIL marks a real, NIL-signed person, and the game will not let one
        // be edited. A recreated player is not that person, so inheriting the
        // slot's flag both claims they are and locks them — and it is the best
        // slots that carry it (100% at 90 overall and above), which is to say
        // the whole starting eleven of a recreated roster.
        session.SetNilStatus(slot, false);

        // Commentary follows the surname, exactly as the game does it on a
        // rename. Left alone, the announcers would keep calling this player by
        // the name of whoever held the slot. 0 is a real answer, not a
        // failure: it is what the game itself stores for a surname it has no
        // recording of, and it is what stops the wrong name being said.
        //
        // With no mapping loaded the field is not touched at all. An absent
        // mapping means this tool knows nothing about commentary, which is a
        // different thing from knowing the announcers cannot say the name —
        // writing 0 on that basis would silence a roster over a missing file.
        if (_commentaryIds.Count > 0)
        {
            var commentary = _commentaryIds.ForLastName(historicalPlayer.LastName);
            session.SetCommentaryId(slot, commentary);
            entry.CommentaryId = commentary;
        }

        // Position: keep the slot's position when interchangeable with the
        // mapped one (a generic DE keeps the slot's LE or RE), otherwise
        // change it explicitly.
        if (_positionMappings.AreInterchangeable(slot.Position, targetPosition))
        {
            entry.AssignedPosition = slot.Position;
        }
        else
        {
            var oldPosition = slot.Position;
            entry.AssignedPosition = targetPosition;
            session.SetPosition(slot, targetPosition);
            entry.Warnings.Add(
                $"No {targetPosition}-compatible slot was free; converted a {oldPosition} slot, " +
                "so the slot's inherited ratings fit the old position.");
        }

        if (Usable(historicalPlayer.JerseyNumber, PlayerSchema.JerseyNumMin, PlayerSchema.JerseyNumMax,
                "Jersey number", $"{slot.JerseyNumber}", entry) is int jersey)
        {
            session.SetJerseyNumber(slot, jersey);
        }

        if (Usable(historicalPlayer.HeightInches, PlayerSchema.HeightInchesMin, PlayerSchema.HeightInchesMax,
                "Height", $"{slot.HeightInches}\"", entry) is int height)
        {
            session.SetHeight(slot, height);
        }

        // Previous school. This is written even when the player has none:
        // otherwise a player who never transferred inherits the donor's
        // transfer history, and 20 of the 85 players on a real roster have one.
        if (_previousSchools is not null)
        {
            if (historicalPlayer.PreviousSchool is not { Length: > 0 } previousSchool)
            {
                session.SetPreviousSchool(slot, PlayerSchema.NoPrevTeamIdSentinel);
            }
            else if (_previousSchools.TryResolve(previousSchool, out var schoolId))
            {
                session.SetPreviousSchool(slot, schoolId);
            }
            else
            {
                session.SetPreviousSchool(slot, PlayerSchema.PrevTeamIdNotInDynasty);
                entry.Warnings.Add(
                    $"Previous school '{previousSchool}' is not a team in your dynasty, so it is recorded " +
                    "as a school the game does not model (the value real FCS transfers carry).");
            }
        }

        if (Hometown.Parse(historicalPlayer.Hometown) is HometownValue hometown)
        {
            session.SetHometown(slot, hometown.Town, hometown.State);
            if (hometown.Note is not null)
            {
                entry.Warnings.Add(hometown.Note);
            }
        }
        else if (historicalPlayer.Hometown is null)
        {
            entry.MissingFields.Add("Hometown");
            entry.DefaultsUsed.Add(
                $"Hometown: {slot.GetRaw(PlayerColumns.HomeTown)}, {slot.GetRaw(PlayerColumns.HomeState)} " +
                "(inherited from donor slot)");
        }

        if (Usable(historicalPlayer.WeightPounds, PlayerSchema.WeightPoundsMin, PlayerSchema.WeightPoundsMax,
                "Weight", $"{slot.WeightPounds} lb", entry) is int weight)
        {
            session.SetWeightPounds(slot, weight);
        }

        // Body build comes last of the physical fields, because it is read off
        // the height, weight and position this player has just been given
        // rather than the ones the donor slot arrived with. Reading the slot
        // would describe whoever used to stand there.
        ApplyBodyType(session, slot, entry);

        if (historicalPlayer.ClassYear is string classYear)
        {
            if (ClassYear.TryParse(classYear, out var schoolYear, out var redshirtStatus))
            {
                session.SetSchoolYear(slot, schoolYear);
                session.SetRedshirtStatus(slot, redshirtStatus);
            }
            else
            {
                entry.Warnings.Add($"Class year '{classYear}' is unrecognized.");
                entry.DefaultsUsed.Add(
                    $"Class year: {slot.SchoolYear}/{slot.RedshirtStatus} (inherited from donor slot)");
            }
        }
        else
        {
            entry.MissingFields.Add("Class year");
            entry.DefaultsUsed.Add(
                $"Class year: {slot.SchoolYear}/{slot.RedshirtStatus} (inherited from donor slot)");
        }
    }
}
