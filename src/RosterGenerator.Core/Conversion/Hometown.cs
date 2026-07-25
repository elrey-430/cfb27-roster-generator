using RosterGenerator.Core.Schema;

namespace RosterGenerator.Core.Conversion;

/// <summary>A hometown split into the two fields the save stores.</summary>
/// <param name="Town">Value for <c>PLYR_HOME_TOWN</c> (free text).</param>
/// <param name="State">Value for <c>PLYR_HOME_STATE</c> (51-value enum).</param>
/// <param name="Note">Explanation when the input needed interpreting.</param>
public sealed record HometownValue(string Town, string State, string? Note = null);

/// <summary>
/// Parses user-supplied hometowns ("Tampa, FL", "Melbourne, Australia") into
/// the save's two fields.
///
/// <c>PLYR_HOME_TOWN</c> is free text, but <c>PLYR_HOME_STATE</c> is a strict
/// 51-value enum — the 50 states in PascalCase plus <c>NonUS</c> — so
/// anything unrecognized becomes <c>NonUS</c> rather than an invalid value
/// the game might reject.
/// </summary>
public static class Hometown
{
    private static readonly Dictionary<string, string> ByAbbreviation = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AL"] = "Alabama", ["AK"] = "Alaska", ["AZ"] = "Arizona", ["AR"] = "Arkansas",
        ["CA"] = "California", ["CO"] = "Colorado", ["CT"] = "Connecticut", ["DE"] = "Delaware",
        ["FL"] = "Florida", ["GA"] = "Georgia", ["HI"] = "Hawaii", ["ID"] = "Idaho",
        ["IL"] = "Illinois", ["IN"] = "Indiana", ["IA"] = "Iowa", ["KS"] = "Kansas",
        ["KY"] = "Kentucky", ["LA"] = "Louisiana", ["ME"] = "Maine", ["MD"] = "Maryland",
        ["MA"] = "Massachusetts", ["MI"] = "Michigan", ["MN"] = "Minnesota", ["MS"] = "Mississippi",
        ["MO"] = "Missouri", ["MT"] = "Montana", ["NE"] = "Nebraska", ["NV"] = "Nevada",
        ["NH"] = "NewHampshire", ["NJ"] = "NewJersey", ["NM"] = "NewMexico", ["NY"] = "NewYork",
        ["NC"] = "NorthCarolina", ["ND"] = "NorthDakota", ["OH"] = "Ohio", ["OK"] = "Oklahoma",
        ["OR"] = "Oregon", ["PA"] = "Pennsylvania", ["RI"] = "RhodeIsland", ["SC"] = "SouthCarolina",
        ["SD"] = "SouthDakota", ["TN"] = "Tennessee", ["TX"] = "Texas", ["UT"] = "Utah",
        ["VT"] = "Vermont", ["VA"] = "Virginia", ["WA"] = "Washington", ["WV"] = "WestVirginia",
        ["WI"] = "Wisconsin", ["WY"] = "Wyoming",
        // Washington DC has no enum value of its own in the save.
        ["DC"] = PlayerSchema.NonUsHomeState,
    };

    /// <summary>
    /// Parses "City, State" into the two stored fields. Returns null when the
    /// input is blank.
    /// </summary>
    public static HometownValue? Parse(string? hometown)
    {
        if (string.IsNullOrWhiteSpace(hometown))
        {
            return null;
        }

        var parts = hometown.Split(',', 2, StringSplitOptions.TrimEntries);
        var town = parts[0];
        if (parts.Length == 1)
        {
            return new HometownValue(town, PlayerSchema.NonUsHomeState,
                $"Hometown '{hometown}' has no state; PLYR_HOME_STATE set to NonUS.");
        }

        var stateText = parts[1];
        if (TryResolveState(stateText, out var state))
        {
            // Resolving to NonUS is still a substitution (Washington DC has no
            // enum value of its own), so say so rather than doing it quietly.
            var note = state == PlayerSchema.NonUsHomeState
                ? $"'{stateText}' has no state value in the save; PLYR_HOME_STATE set to NonUS."
                : null;
            return new HometownValue(town, state, note);
        }

        return new HometownValue(town, PlayerSchema.NonUsHomeState,
            $"'{stateText}' is not a US state; PLYR_HOME_STATE set to NonUS.");
    }

    /// <summary>
    /// Resolves a state written as an abbreviation ("FL"), a full name
    /// ("Florida", "West Virginia") or the save's own PascalCase spelling.
    /// </summary>
    public static bool TryResolveState(string text, out string state)
    {
        var trimmed = text.Trim();
        if (ByAbbreviation.TryGetValue(trimmed, out var byAbbreviation))
        {
            state = byAbbreviation;
            return true;
        }

        // "West Virginia", "west virginia" and "WestVirginia" all collapse to
        // the save's spelling once spaces and case are removed.
        var collapsed = new string(trimmed.Where(char.IsLetter).ToArray());
        var match = PlayerSchema.HomeStates
            .FirstOrDefault(s => string.Equals(s, collapsed, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            state = match;
            return true;
        }

        state = PlayerSchema.NonUsHomeState;
        return false;
    }
}
