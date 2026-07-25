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
    private readonly RosterFiller? _rosterFiller;
    private readonly RosterDepthModel? _depth;
    private readonly TeamMappingSet? _previousSchools;

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
    public HistoricalTeamConverter(
        TeamMappingSet teamMappings,
        PositionMappingSet positionMappings,
        RatingEngine? ratingEngine = null,
        ArchetypeSelector? archetypeSelector = null,
        RosterFiller? rosterFiller = null,
        RosterDepthModel? rosterDepth = null,
        TeamMappingSet? previousSchoolMappings = null)
    {
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
        report.GlobalAssumptions.Add(
            "Identity asset fields (PLYR_ASSETNAME, GenericHeadAssetName, PLYR_PORTRAIT) keep the donor " +
            "slot's values, so in-game portraits/head models belong to the replaced fictional players. " +
            "Face mapping is a later milestone.");
        report.GlobalAssumptions.Add(
            "Hometown is written: PLYR_HOME_TOWN takes the town as free text and PLYR_HOME_STATE the " +
            "matching state from the save's 51-value enum (NonUS for anything not a US state).");
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

        report.FilledSlots.AddRange(
            _rosterFiller!.Fill(session, freeSlots, weakestByPosition, placements.Count));

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

    private void ApplyPlayer(
        RosterEditSession session,
        Player slot,
        HistoricalPlayer historicalPlayer,
        string targetPosition,
        PlayerConversionEntry entry)
    {
        // Identity: replace-with-real-player semantics. The donor slot's
        // asset values are passed back unchanged (a deliberate, reported
        // assumption — see GlobalAssumptions).
        session.ReplacePlayerIdentity(
            slot,
            historicalPlayer.FirstName,
            historicalPlayer.LastName,
            assetName: slot.GetRaw(PlayerColumns.AssetName),
            genericHeadAssetName: slot.GetRaw(PlayerColumns.GenericHeadAssetName),
            portrait: slot.GetRaw(PlayerColumns.Portrait));

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

        if (historicalPlayer.JerseyNumber is int jersey)
        {
            session.SetJerseyNumber(slot, jersey);
        }
        else
        {
            entry.MissingFields.Add("Jersey number");
            entry.DefaultsUsed.Add($"Jersey number: {slot.JerseyNumber} (inherited from donor slot)");
        }

        if (historicalPlayer.HeightInches is int height)
        {
            session.SetHeight(slot, height);
        }
        else
        {
            entry.MissingFields.Add("Height");
            entry.DefaultsUsed.Add($"Height: {slot.HeightInches}\" (inherited from donor slot)");
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

        if (historicalPlayer.WeightPounds is int weight)
        {
            if (weight is >= PlayerSchema.WeightPoundsMin and <= PlayerSchema.WeightPoundsMax)
            {
                session.SetWeightPounds(slot, weight);
            }
            else
            {
                entry.Warnings.Add(
                    $"Weight {weight} lb is outside the representable {PlayerSchema.WeightPoundsMin}–" +
                    $"{PlayerSchema.WeightPoundsMax} lb range.");
                entry.DefaultsUsed.Add($"Weight: {slot.WeightPounds} lb (inherited from donor slot)");
            }
        }
        else
        {
            entry.MissingFields.Add("Weight");
            entry.DefaultsUsed.Add($"Weight: {slot.WeightPounds} lb (inherited from donor slot)");
        }

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
