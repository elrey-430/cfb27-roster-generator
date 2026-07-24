using RosterGenerator.Core.Editing;
using RosterGenerator.Core.Historical;
using RosterGenerator.Core.Mapping;
using RosterGenerator.Core.Model;
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
/// Confirmed-safe fields (name, jersey, height, class, redshirt, position)
/// are overwritten with historical values; ratings are inherited from the
/// slot (rating generation is a later milestone); Weight is never written
/// while its encoding is unresolved. Every default and assumption is
/// recorded in the <see cref="ConversionReport"/>.
/// </summary>
public sealed class HistoricalTeamConverter
{
    private readonly TeamMappingSet _teamMappings;
    private readonly PositionMappingSet _positionMappings;

    /// <summary>Creates a converter over the two external mapping sets.</summary>
    public HistoricalTeamConverter(TeamMappingSet teamMappings, PositionMappingSet positionMappings)
    {
        _teamMappings = teamMappings;
        _positionMappings = positionMappings;
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

        report.GlobalAssumptions.Add(
            "Ratings are inherited from the donor slot each player replaces — automatic rating " +
            "generation is out of scope for this milestone.");
        report.GlobalAssumptions.Add(
            "Weight is NOT written: the save's Weight encoding is unresolved (values are not pounds). " +
            "Historical weights are preserved in the dataset for when the encoding is decoded.");
        report.GlobalAssumptions.Add(
            "Identity asset fields (PLYR_ASSETNAME, GenericHeadAssetName, PLYR_PORTRAIT) keep the donor " +
            "slot's values, so in-game portraits/head models belong to the replaced fictional players. " +
            "Face mapping is a later milestone.");
        report.GlobalAssumptions.Add(
            "Hometown/previous-school data is carried in the dataset but not exported — the candidate " +
            "columns (PLYR_HOME_TOWN, PLYR_HOME_STATE) are not yet empirically confirmed as safe to write.");
        report.GlobalAssumptions.Add(
            "Slot assignment prefers a donor slot at the same position (or an interchangeable one, e.g. " +
            "LE/RE); players placed in an unrelated slot get an explicit position change.");

        // Donor slots for this team, position-preferred assignment.
        var slots = roster.Players
            .Where(p => p.TeamIndex == teamId)
            .OrderBy(p => p.RowKey)
            .ToList();
        var freeSlots = new List<Player>(slots);

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
        }

        foreach (var slot in freeSlots)
        {
            report.LeftoverDonorSlots.Add($"{slot} — {slot.Position}, OVR {slot.OverallRating}");
        }

        if (freeSlots.Count > 0)
        {
            report.GlobalWarnings.Add(
                $"{freeSlots.Count} donor slot(s) were not replaced; the original fictional players " +
                "remain on the roster (listed below). Remove or edit them manually if unwanted.");
        }

        return report;
    }

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

        if (historicalPlayer.WeightPounds is null)
        {
            entry.MissingFields.Add("Weight");
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
