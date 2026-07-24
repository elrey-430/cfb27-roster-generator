namespace RosterGenerator.Core.Conversion;

/// <summary>
/// Parses historical class-year labels ("Redshirt Junior", "RS Fr",
/// "Graduate") into the CFB27 <c>SchoolYear</c> + <c>RedshirtStatus</c>
/// pair. A redshirt prefix maps to <c>RedshirtStatus = Previous</c>;
/// graduate students are represented as Seniors (CFB27 has no grad year).
/// </summary>
public static class ClassYear
{
    /// <summary>
    /// Tries to parse a class-year label.
    /// </summary>
    /// <param name="label">The historical label, e.g. "Redshirt Sophomore".</param>
    /// <param name="schoolYear">CFB27 SchoolYear enum value.</param>
    /// <param name="redshirtStatus">CFB27 RedshirtStatus enum value.</param>
    /// <returns>False when the label is unrecognized.</returns>
    public static bool TryParse(string label, out string schoolYear, out string redshirtStatus)
    {
        schoolYear = "";
        redshirtStatus = "Eligible";

        var text = label.Trim().ToLowerInvariant().Replace(".", "").Replace("-", " ");
        var isRedshirt = false;
        foreach (var prefix in new[] { "redshirt ", "rs ", "rs" })
        {
            if (text.StartsWith(prefix, StringComparison.Ordinal) && text.Length > prefix.Length)
            {
                text = text[prefix.Length..].TrimStart();
                isRedshirt = true;
                break;
            }
        }

        schoolYear = text switch
        {
            "freshman" or "fr" or "frosh" => "Freshman",
            "sophomore" or "so" or "soph" => "Sophomore",
            "junior" or "jr" => "Junior",
            "senior" or "sr" => "Senior",
            "graduate" or "grad" or "gr" or "graduate student" => "Senior",
            _ => "",
        };

        if (schoolYear.Length == 0)
        {
            return false;
        }

        // A player cannot be a redshirt-eligible freshman and have already
        // used the redshirt; "Redshirt X" always means the redshirt was used.
        redshirtStatus = isRedshirt ? "Previous" : "Eligible";
        return true;
    }
}
