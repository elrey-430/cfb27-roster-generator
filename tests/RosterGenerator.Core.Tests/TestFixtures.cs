using RosterGenerator.Core.Model;

namespace RosterGenerator.Core.Tests;

/// <summary>
/// Access to the checked-in sample data. <c>PlayerSample.csv</c> is a slice
/// of a real CFB27 save export (the DYNASTY-JUL24-BASE baseline): the full
/// 286-column header, five real player rows (three FSU, two Alabama) and one
/// empty pool slot, byte-for-byte as the game exported them.
/// </summary>
internal static class TestFixtures
{
    /// <summary>Absolute path to the sample Player table CSV.</summary>
    public static string PlayerSamplePath =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlayerSample.csv");

    /// <summary>Reads the sample file's raw text.</summary>
    public static string PlayerSampleText => File.ReadAllText(PlayerSamplePath);

    /// <summary>Loads the sample roster.</summary>
    public static PlayerRoster LoadSampleRoster() => PlayerRoster.Load(PlayerSamplePath);
}
