using System.Buffers.Binary;
using System.Text;
using RosterGenerator.Core.Legacy;
using Xunit;

namespace RosterGenerator.Core.Tests;

/// <summary>
/// Reading and writing a PS2 memory-card save in the <c>.psu</c> form.
///
/// <para>The guarantees that matter are all about restraint. A memory card
/// holds more than a roster, and a tool that rewrites the parts it did not come
/// for is worse than one that refuses to open the file at all — so the tests
/// below are mostly about what does <em>not</em> change.</para>
/// </summary>
public sealed class Ps2SaveTests
{
    private const uint DirectoryMode = 0x8427;
    private const uint FileMode = 0x8497;

    /// <summary>
    /// Builds a .psu by hand, to the published layout, so the reader is tested
    /// against the format rather than against the writer.
    /// </summary>
    private static byte[] BuildSave(string saveName, params (string Name, byte[] Data)[] files)
    {
        var stream = new MemoryStream();

        void Entry(uint mode, int length, string name)
        {
            var header = new byte[512];
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0x00), mode);
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0x04), (uint)length);
            WriteStamp(header.AsSpan(0x08));
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0x10), 7);   // cluster
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0x14), 3);   // dir entry
            WriteStamp(header.AsSpan(0x18));
            Encoding.ASCII.GetBytes(name).CopyTo(header.AsSpan(0x40));
            stream.Write(header);
        }

        Entry(DirectoryMode, files.Length + 2, saveName);
        Entry(DirectoryMode, 0, ".");
        Entry(DirectoryMode, 0, "..");
        foreach (var (name, data) in files)
        {
            Entry(FileMode, data.Length, name);
            stream.Write(data);
            stream.Write(new byte[Padded(data.Length) - data.Length]);
        }

        return stream.ToArray();
    }

    /// <summary>A sceMcStDateTime: reserved, second, minute, hour, day, month, year.</summary>
    private static void WriteStamp(Span<byte> span)
    {
        span[0] = 0;
        span[1] = 30;
        span[2] = 15;
        span[3] = 9;
        span[4] = 15;
        span[5] = 8;
        BinaryPrimitives.WriteUInt16LittleEndian(span[6..], 2004);
    }

    private static int Padded(int length) => (length + 1023) / 1024 * 1024;

    private static byte[] Roster() => LegacyRosterTests.LittleEndianSquadFixture();

    private static byte[] Icon() => Enumerable.Range(0, 1500).Select(i => (byte)(i % 251)).ToArray();

    [Fact]
    public void ASaveNobodyTouchedComesBackByteForByte()
    {
        // The whole design in one assertion: opening a save and writing it
        // straight back out must be the identity.
        var original = BuildSave("BASLUS-20991", ("icon.sys", Icon()), ("BASLUS-20991RNCAA1a", Roster()));
        var save = Ps2MemoryCardSave.Parse(original, "test.psu");

        Assert.Equal(original, save.ToBytes());
    }

    [Fact]
    public void TheSaveKnowsItsOwnNameAndItsFiles()
    {
        var save = Ps2MemoryCardSave.Parse(
            BuildSave("BASLUS-20991", ("icon.sys", Icon()), ("BASLUS-20991RNCAA1a", Roster())),
            "test.psu");

        Assert.Equal("BASLUS-20991", save.SaveName);
        Assert.Equal(
            new[] { "icon.sys", "BASLUS-20991RNCAA1a" },
            save.Files.Select(f => f.Name));

        // The directory and its two markers are entries too, and they are what
        // makes the file a save rather than a heap of blobs.
        Assert.Equal(
            new[] { "BASLUS-20991", ".", "..", "icon.sys", "BASLUS-20991RNCAA1a" },
            save.Entries.Select(e => e.Name));
    }

    [Fact]
    public void TheRosterIsFoundByWhatItIsRatherThanByItsName()
    {
        // The file is named differently between the roster and dynasty saves
        // and between games, so the name is not evidence. The DB header is.
        var save = Ps2MemoryCardSave.Parse(
            BuildSave("BASLUS-20991", ("icon.sys", Icon()), ("SOMETHING-UNSEEN", Roster())),
            "test.psu");

        Assert.NotNull(save.RosterFile);
        Assert.Equal("SOMETHING-UNSEEN", save.RosterFile!.Name);
    }

    [Fact]
    public void ASaveWithNoRosterInItSaysSoRatherThanGuessing()
    {
        var save = Ps2MemoryCardSave.Parse(BuildSave("BASLUS-20991", ("icon.sys", Icon())), "test.psu");
        Assert.Null(save.RosterFile);
    }

    [Theory]
    [InlineData(64)]        // very much shorter
    [InlineData(1024)]      // exactly on the padding boundary
    [InlineData(1025)]      // one byte over it
    public void ReplacingTheRosterLeavesEveryOtherFileExactlyAsItWas(int newLength)
    {
        var icon = Icon();
        var save = Ps2MemoryCardSave.Parse(
            BuildSave("BASLUS-20991", ("icon.sys", icon), ("BASLUS-20991RNCAA1a", Roster())),
            "test.psu");

        var replacement = Enumerable.Range(0, newLength).Select(i => (byte)(i % 97)).ToArray();
        save.Replace(save.RosterFile!, replacement);

        var reread = Ps2MemoryCardSave.Parse(save.ToBytes(), "written.psu");
        Assert.Equal(icon, reread.Files.Single(f => f.Name == "icon.sys").Data);
        Assert.Equal(replacement, reread.Files.Single(f => f.Name == "BASLUS-20991RNCAA1a").Data);
        Assert.Equal(newLength, reread.Files.Single(f => f.Name == "BASLUS-20991RNCAA1a").Length);
    }

    [Fact]
    public void AReplacedFileKeepsEveryHeaderFieldButItsLength()
    {
        var save = Ps2MemoryCardSave.Parse(
            BuildSave("BASLUS-20991", ("BASLUS-20991RNCAA1a", Roster())), "test.psu");
        var roster = save.RosterFile!;
        var mode = roster.Mode;
        var created = roster.Created;
        var modified = roster.Modified;

        save.Replace(roster, new byte[77]);

        // Timestamps are deliberately left alone: the card browser shows them,
        // and they are the user's record of their own save rather than ours.
        Assert.Equal(mode, roster.Mode);
        Assert.Equal(created, roster.Created);
        Assert.Equal(modified, roster.Modified);
        Assert.Equal("BASLUS-20991RNCAA1a", roster.Name);
        Assert.Equal(77, roster.Length);
    }

    [Fact]
    public void TheTimestampIsReadTheWayThePs2WritesIt()
    {
        var save = Ps2MemoryCardSave.Parse(
            BuildSave("BASLUS-20991", ("BASLUS-20991RNCAA1a", Roster())), "test.psu");

        Assert.Equal(new DateTime(2004, 8, 15, 9, 15, 30), save.RosterFile!.Created);
    }

    [Fact]
    public void TheLayoutIsTheOneThePs2Uses()
    {
        // Pinned deliberately: a 512-byte header per entry, and each file's
        // contents padded out to a multiple of 1024. Getting either wrong
        // produces a file the console will not read, and nothing else in the
        // suite would notice.
        var roster = Roster();
        var bytes = Ps2MemoryCardSave
            .Parse(BuildSave("BASLUS-20991", ("BASLUS-20991RNCAA1a", roster)), "test.psu")
            .ToBytes();

        Assert.Equal(512 * 4 + Padded(roster.Length), bytes.Length);
        Assert.Equal(0, bytes.Length % 512);

        // The roster's contents begin immediately after the fourth header.
        Assert.Equal(roster, bytes[(512 * 4)..(512 * 4 + roster.Length)]);
    }

    [Fact]
    public void ABareRosterFileIsNotMistakenForASave()
    {
        // The two are handled differently and the tool has to tell them apart
        // on its own, because a .psu and a bare roster file both arrive as a
        // file with no extension worth trusting.
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            File.WriteAllBytes(path, Roster());
            Assert.False(Ps2MemoryCardSave.LooksLikeSave(path));
            Assert.True(EaDbFile.LooksLikeLegacyFile(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ASaveIsNotMistakenForABareRosterFile()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            File.WriteAllBytes(path, BuildSave("BASLUS-20991", ("BASLUS-20991RNCAA1a", Roster())));
            Assert.True(Ps2MemoryCardSave.LooksLikeSave(path));
            Assert.False(EaDbFile.LooksLikeLegacyFile(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TrailingBytesTheReaderCannotAccountForAreRefused()
    {
        // Rather than read what it understands and quietly drop the rest,
        // which is how a tool truncates somebody's memory card.
        var bytes = BuildSave("BASLUS-20991", ("BASLUS-20991RNCAA1a", Roster()))
            .Concat(new byte[512]).ToArray();

        var error = Assert.Throws<InvalidDataException>(() => Ps2MemoryCardSave.Parse(bytes, "odd.psu"));
        Assert.Contains("after its last entry", error.Message);
    }

    [Fact]
    public void AFileRunningOffTheEndIsRefused()
    {
        var bytes = BuildSave("BASLUS-20991", ("BASLUS-20991RNCAA1a", Roster()));
        // Claim the roster is far larger than the file can hold.
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(512 * 3 + 4), 0x0F00_0000);

        var error = Assert.Throws<InvalidDataException>(() => Ps2MemoryCardSave.Parse(bytes, "short.psu"));
        Assert.Contains("runs off the end", error.Message);
    }

    [Fact]
    public void WritingOverTheSaveThatWasReadIsRefused()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            File.WriteAllBytes(path, BuildSave("BASLUS-20991", ("BASLUS-20991RNCAA1a", Roster())));
            var save = Ps2MemoryCardSave.Read(path);
            Assert.Throws<InvalidOperationException>(() => save.Save(path, readFrom: path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ExportingIntoASaveGivesBackASaveWithEverythingElseIntact()
    {
        // The point of the whole container: a save goes in, a save comes out,
        // the teams inside it change and nothing else in the memory card does.
        var icon = Icon();
        var source = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var output = source + ".out";
        try
        {
            File.WriteAllBytes(source, BuildSave(
                "BASLUS-20991", ("icon.sys", icon), ("BASLUS-20991RNCAA1a", Roster())));

            var result = LegacyRosterExporter.Export(
                source, output, TestFixtures.LoadSampleRoster(), teamIndex: 27, legacyTeamId: 9,
                teamName: "Florida State",
                LegacyRatingScale.Load(TestFixtures.DataPath("LegacyRatingScale.json")));

            Assert.True(result.WroteSave);
            Assert.Contains("memory-card save", result.SourceDescription);

            var written = Ps2MemoryCardSave.Read(output);
            Assert.Equal("BASLUS-20991", written.SaveName);
            Assert.Equal(icon, written.Files.Single(f => f.Name == "icon.sys").Data);

            // And the roster inside it really is the edited one.
            var squad = LegacyRosterReader.Read(EaDbFile.Parse(written.RosterFile!.Data))
                .Teams.Single(t => t.TeamId == 9);
            Assert.Contains(squad.Players, p => p.LastName == "Applewhite");
        }
        finally
        {
            File.Delete(source);
            File.Delete(output);
        }
    }

    [Fact]
    public void ABareRosterFileStillGivesBackABareRosterFile()
    {
        // You get back the kind you gave it. Turning a bare file into a save
        // would mean inventing a save directory and an icon the user never had.
        var source = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var output = source + ".out";
        try
        {
            File.WriteAllBytes(source, Roster());
            var result = LegacyRosterExporter.Export(
                source, output, TestFixtures.LoadSampleRoster(), 27, 9, "Florida State",
                LegacyRatingScale.Load(TestFixtures.DataPath("LegacyRatingScale.json")));

            Assert.False(result.WroteSave);
            Assert.True(EaDbFile.LooksLikeLegacyFile(output));
        }
        finally
        {
            File.Delete(source);
            File.Delete(output);
        }
    }

    [Fact]
    public void TheRosterCanAlsoBeWrittenOnItsOwnAlongsideTheSave()
    {
        var source = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var output = source + ".out";
        var database = source + ".db";
        try
        {
            File.WriteAllBytes(source, BuildSave("BASLUS-20991", ("BASLUS-20991RNCAA1a", Roster())));
            var result = LegacyRosterExporter.Export(
                source, output, TestFixtures.LoadSampleRoster(),
                new[] { new LegacyExportTeam("Florida State", 27, 9) },
                LegacyRatingScale.Load(TestFixtures.DataPath("LegacyRatingScale.json")),
                depthChart: null, databaseOutputPath: database);

            Assert.Equal(database, result.DatabasePath);

            // The save, and the same roster loose — byte for byte the same
            // tables, one with the memory-card wrapper and one without.
            Assert.True(Ps2MemoryCardSave.LooksLikeSave(output));
            Assert.True(EaDbFile.LooksLikeLegacyFile(database));
            Assert.Equal(
                Ps2MemoryCardSave.Read(output).RosterFile!.Data,
                File.ReadAllBytes(database));
        }
        finally
        {
            File.Delete(source);
            File.Delete(output);
            File.Delete(database);
        }
    }

    [Fact]
    public void ImportingReadsASaveJustAsHappilyAsABareFile()
    {
        var save = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var bare = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            File.WriteAllBytes(save, BuildSave("BASLUS-20991", ("BASLUS-20991RNCAA1a", Roster())));
            File.WriteAllBytes(bare, Roster());

            var fromSave = LegacyRosterReader.Read(save);
            var fromBare = LegacyRosterReader.Read(bare);

            Assert.Equal(
                fromBare.Teams.Select(t => t.Players.Count),
                fromSave.Teams.Select(t => t.Players.Count));
        }
        finally
        {
            File.Delete(save);
            File.Delete(bare);
        }
    }

    [Fact]
    public void ASaveThatHoldsNoRosterSaysSoPlainly()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            File.WriteAllBytes(path, BuildSave("BASLUS-20991", ("icon.sys", Icon())));
            var error = Assert.Throws<InvalidDataException>(() => LegacyRosterSource.Open(path));
            Assert.Contains("none of the", error.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TheRosterIsFoundEvenWhenItSharesTheSaveFoldersName()
    {
        // Not hypothetical: a real NCAA Football 2005 roster save names its
        // directory and the roster inside it identically —
        // BASLUS-20991R2025/BASLUS-20991R2025 — alongside view.ico and
        // icon.sys. Matching on the name would have to guess between the two.
        var save = Ps2MemoryCardSave.Parse(
            BuildSave(
                "BASLUS-20991R2025",
                ("BASLUS-20991R2025", Roster()),
                ("view.ico", Icon()),
                ("icon.sys", Icon())),
            "real.psu");

        Assert.Equal("BASLUS-20991R2025", save.SaveName);
        Assert.NotNull(save.RosterFile);
        Assert.Equal("BASLUS-20991R2025", save.RosterFile!.Name);
        Assert.Equal(Roster(), save.RosterFile.Data);
    }

    [Fact]
    public void ADirectoryEntryCannotBeGivenFileContents()
    {
        var save = Ps2MemoryCardSave.Parse(
            BuildSave("BASLUS-20991", ("BASLUS-20991RNCAA1a", Roster())), "test.psu");

        Assert.Throws<ArgumentException>(() => save.Replace(save.Entries[0], new byte[16]));
    }
}
