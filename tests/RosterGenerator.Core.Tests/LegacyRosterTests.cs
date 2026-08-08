using RosterGenerator.Core.Historical;
using RosterGenerator.Core.Legacy;
using RosterGenerator.Core.Rating;

namespace RosterGenerator.Core.Tests;

/// <summary>
/// Reading PS2-era NCAA Football roster files.
///
/// <para>The fixture is built here rather than committed, for two reasons. A
/// real roster file is somebody's editing work and not ours to redistribute;
/// and a written fixture can state the awkward parts of the format outright —
/// the stale column offsets, the truncated last column, two squads sharing one
/// block of player ids — instead of hoping a sample happens to contain
/// them.</para>
/// </summary>
public class LegacyRosterTests
{
    // ---- the fixture -------------------------------------------------------
    //
    // PLAY records are 52 bytes. Fields sit where the real format puts them,
    // because the reader's correction table names absolute bit positions:
    //
    //   RCHD  0-16   PGID 16-32   PWGT 32-40
    //   PF01..PF10  40-100        PL01..PL13 100-178   (6 bits each)
    //   PPOS 178-183 PJEN 183-190 PYER 190-192  PSKI 192-195
    //   POVR 195-200 PHGT 390-397 PSPD 200-205  PSTR 205-210
    //
    // PGID, PWGT and PHGT are written with the WRONG end offsets on purpose —
    // the same wrong ones two real files carry — so the corrections are what
    // makes the fixture readable at all.

    private const int PlayerRecordBytes = 52;
    private const int TeamRecordBytes = 20;
    private const int ChartRecordBytes = 4;

    private sealed record Column(string Name, int Bits, int Start, int? DeclaredEnd = null);

    private static readonly List<Column> PlayerColumns = BuildPlayerColumns();

    private static List<Column> BuildPlayerColumns()
    {
        var columns = new List<Column>
        {
            new("RCHD", 16, 0),
            new("PGID", 16, 16, DeclaredEnd: 210),
            new("PWGT", 8, 32, DeclaredEnd: 397),
        };

        var bit = 40;
        for (var i = 1; i <= 10; i++, bit += 6)
        {
            columns.Add(new Column($"PF{i:00}", 6, bit));
        }

        for (var i = 1; i <= 13; i++, bit += 6)
        {
            columns.Add(new Column($"PL{i:00}", 6, bit));
        }

        columns.Add(new Column("PPOS", 5, 178));
        columns.Add(new Column("PJEN", 7, 183));
        columns.Add(new Column("PYER", 2, 190));
        columns.Add(new Column("PSKI", 3, 192));
        columns.Add(new Column("POVR", 5, 195));
        columns.Add(new Column("PHGT", 7, 390, DeclaredEnd: 32));
        columns.Add(new Column("PSPD", 5, 200));
        columns.Add(new Column("PSTR", 5, 205));
        return columns;
    }

    private static readonly List<Column> TeamColumns = new()
    {
        new Column("DCAP", 16, 0),
        new Column("OCAP", 16, 16, DeclaredEnd: 96),
        new Column("TROV", 8, 32),
        new Column("TOID", 9, 120, DeclaredEnd: 64),
    };

    private static readonly List<Column> ChartColumns = new()
    {
        new Column("PGID", 16, 0),
        new Column("PPOS", 5, 16),
        new Column("ddep", 3, 21),
    };

    private sealed record FixturePlayer(
        int Id, string First, string Last, int Position, int Jersey, int Height, int Weight,
        int ClassYear, int SkinTone, int Overall, int Speed, int Strength);

    private static byte[] BuildFile(
        IReadOnlyList<FixturePlayer> players,
        IReadOnlyList<(int TeamId, int Defensive, int Offensive)> teams,
        IReadOnlyList<(int PlayerId, int Position, int Depth)> chart)
    {
        var play = BuildTable(PlayerColumns, PlayerRecordBytes, players.Count + 3, (write, row) =>
        {
            if (row >= players.Count)
            {
                return;
            }

            var p = players[row];
            write("RCHD", row + 1);
            write("PGID", p.Id);
            write("PWGT", p.Weight - LegacySchema.WeightOffsetPounds);
            WriteName(write, "PF", p.First, 10);
            WriteName(write, "PL", p.Last, 13);
            write("PPOS", p.Position);
            write("PJEN", p.Jersey);
            write("PYER", p.ClassYear);
            write("PSKI", p.SkinTone);
            write("POVR", p.Overall);
            write("PHGT", p.Height);
            write("PSPD", p.Speed);
            write("PSTR", p.Strength);
        }, used: players.Count);

        var tdyn = BuildTable(TeamColumns, TeamRecordBytes, teams.Count + 2, (write, row) =>
        {
            if (row >= teams.Count)
            {
                return;
            }

            write("TOID", teams[row].TeamId);
            write("DCAP", teams[row].Defensive);
            write("OCAP", teams[row].Offensive);
            write("TROV", 70);
        }, used: teams.Count);

        var dcht = BuildTable(ChartColumns, ChartRecordBytes, chart.Count + 2, (write, row) =>
        {
            if (row >= chart.Count)
            {
                return;
            }

            write("PGID", chart[row].PlayerId);
            write("PPOS", chart[row].Position);
            write("ddep", chart[row].Depth);
        }, used: chart.Count);

        var tables = new (string Name, byte[] Bytes)[]
        {
            ("DCHT", dcht), ("PLAY", play), ("TDYN", tdyn),
        };

        var header = new List<byte>();
        header.AddRange("DB"u8.ToArray());
        header.AddRange(BitConverter.GetBytes((ushort)0x0800));
        header.AddRange(BitConverter.GetBytes(0));
        header.AddRange(BitConverter.GetBytes(tables.Sum(t => t.Bytes.Length)));
        header.AddRange(BitConverter.GetBytes(0));
        header.AddRange(BitConverter.GetBytes(tables.Length));
        header.AddRange(BitConverter.GetBytes(0));

        var offset = 0;
        foreach (var (name, bytes) in tables)
        {
            header.AddRange(System.Text.Encoding.ASCII.GetBytes(name));
            header.AddRange(BitConverter.GetBytes(offset));
            offset += bytes.Length;
        }

        return header.Concat(tables.SelectMany(t => t.Bytes)).ToArray();
    }

    private static void WriteName(Action<string, int> write, string prefix, string value, int slots)
    {
        for (var i = 0; i < slots; i++)
        {
            var code = 0;
            if (i < value.Length)
            {
                var c = value[i];
                code = c switch
                {
                    >= 'a' and <= 'z' => c - 'a' + 1,
                    >= 'A' and <= 'Z' => c - 'A' + 27,
                    '\'' => 54,
                    ' ' => 56,
                    _ => 0,
                };
            }

            write($"{prefix}{i + 1:00}", code);
        }
    }

    private static byte[] BuildTable(
        IReadOnlyList<Column> columns, int recordBytes, int records,
        Action<Action<string, int>, int> fill, int? used = null)
    {
        // 48-byte table header, then 16 bytes per column except the last,
        // which the format truncates to name and width. +20 holds the record
        // counts, allocated then used.
        var head = new byte[48];
        BitConverter.GetBytes(recordBytes).CopyTo(head, 8);
        BitConverter.GetBytes((ushort)records).CopyTo(head, 20);
        BitConverter.GetBytes((ushort)(used ?? records)).CopyTo(head, 22);
        BitConverter.GetBytes(columns.Count).CopyTo(head, 28);
        BitConverter.GetBytes(columns[0].Start).CopyTo(head, 44);

        var defs = new List<byte>();
        for (var i = 0; i < columns.Count; i++)
        {
            var column = columns[i];
            defs.AddRange(System.Text.Encoding.ASCII.GetBytes(column.Name));
            defs.AddRange(BitConverter.GetBytes(column.Bits));
            if (i == columns.Count - 1)
            {
                continue;
            }

            defs.AddRange(BitConverter.GetBytes(3));
            defs.AddRange(BitConverter.GetBytes(column.DeclaredEnd ?? column.Start + column.Bits));
        }

        var data = new byte[recordBytes * records];
        var byName = columns.ToDictionary(c => c.Name);
        for (var row = 0; row < records; row++)
        {
            var start = row * recordBytes * 8;
            fill((name, value) =>
            {
                var column = byName[name];
                for (var bit = 0; bit < column.Bits; bit++)
                {
                    if (((value >> bit) & 1) == 0)
                    {
                        continue;
                    }

                    var position = start + column.Start + bit;
                    data[position >> 3] |= (byte)(1 << (position & 7));
                }
            }, row);
        }

        return head.Concat(defs).Concat(data).ToArray();
    }

    /// <summary>
    /// Two squads whose player ids run straight into one another, which is the
    /// case no gap can separate. Ids 100-109 are one team and 110-119 the
    /// other, and only the depth chart says so.
    /// </summary>
    private static byte[] TouchingSquads()
    {
        var players = new List<FixturePlayer>();
        var chart = new List<(int, int, int)>();
        for (var team = 0; team < 2; team++)
        {
            for (var i = 0; i < 10; i++)
            {
                var id = 100 + team * 10 + i;
                players.Add(new FixturePlayer(
                    Id: id, First: "Player", Last: $"Number{(char)('a' + i)}",
                    Position: i, Jersey: 10 + i, Height: 70 + (i % 6), Weight: 180 + i * 10,
                    ClassYear: i % 4, SkinTone: i % 8, Overall: 20 - i, Speed: i, Strength: 20 - i));
                chart.Add((id, i, 0));
            }
        }

        return BuildFile(players, new[] { (7, 102, 104), (8, 112, 114) }, chart);
    }

    private static string WriteTemp(byte[] bytes)
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        File.WriteAllBytes(path, bytes);
        return path;
    }

    // ---- reader ------------------------------------------------------------

    [Fact]
    public void ReadsIdentityThroughTheStaleColumnOffsets()
    {
        var file = BuildFile(
            new[]
            {
                new FixturePlayer(41, "Reggie", "Bush", 1, 5, 72, 200, 1, 5, 30, 31, 12),
                new FixturePlayer(42, "LenDale", "White", 1, 21, 74, 240, 1, 6, 28, 10, 30),
            },
            new[] { (9, 41, 42) },
            new[] { (41, 1, 0), (42, 1, 1) });

        var roster = LegacyRosterReader.Read(EaDbFile.Parse(file));
        var players = roster.AllPlayers.ToList();

        Assert.Equal(2, players.Count);
        var bush = players[0];
        Assert.Equal("Reggie", bush.FirstName);
        Assert.Equal("Bush", bush.LastName);
        Assert.Equal("HB", bush.Position);
        Assert.Equal(5, bush.JerseyNumber);
        Assert.Equal(72, bush.HeightInches);
        Assert.Equal(200, bush.WeightPounds);
        Assert.Equal("Sophomore", bush.ClassYear);
        Assert.Equal("Starter", bush.Role);
        Assert.Equal("Backup", players[1].Role);
    }

    [Fact]
    public void CarriesSkinToneOntoEasScale()
    {
        // The file counts from zero and CFB27 from one. The value is carried
        // rather than dropped because somebody chose it deliberately when the
        // roster was made -- it is a record, not a guess about a real person.
        var file = BuildFile(
            new[] { new FixturePlayer(41, "A", "Player", 0, 1, 72, 200, 0, 0, 10, 10, 10) },
            new[] { (9, 41, 41) },
            new[] { (41, 0, 0) });

        Assert.Equal(1, LegacyRosterReader.Read(EaDbFile.Parse(file)).AllPlayers.Single().SkinTone);
    }

    [Fact]
    public void DecodesNamesIncludingAnApostrophe()
    {
        var file = BuildFile(
            new[] { new FixturePlayer(41, "Travis", "O'Neal", 2, 36, 71, 200, 0, 3, 10, 10, 10) },
            new[] { (9, 41, 41) },
            new[] { (41, 2, 0) });

        Assert.Equal("O'Neal", LegacyRosterReader.Read(EaDbFile.Parse(file)).AllPlayers.Single().LastName);
    }

    [Fact]
    public void SplitsTwoSquadsWhoseIdsTouch()
    {
        var roster = LegacyRosterReader.Read(EaDbFile.Parse(TouchingSquads()));

        Assert.Equal(2, roster.Teams.Count);
        Assert.All(roster.Teams, t => Assert.Equal(10, t.Players.Count));
        Assert.All(roster.Teams[0].Players, p => Assert.InRange(p.PlayerId, 100, 109));
        Assert.All(roster.Teams[1].Players, p => Assert.InRange(p.PlayerId, 110, 119));
        Assert.Contains(roster.Notes, n => n.Contains("0 slot collision"));
    }

    [Fact]
    public void RefusesAFileThatIsNotARoster()
    {
        var path = WriteTemp("not a roster at all, just some text"u8.ToArray());
        try
        {
            Assert.Throws<InvalidDataException>(() => EaDbFile.Read(path));
            Assert.False(EaDbFile.LooksLikeLegacyFile(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ---- import ------------------------------------------------------------

    private static string ImportTouchingSquads(out LegacyImportResult result)
    {
        var source = WriteTemp(TouchingSquads());
        var output = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".csv");
        try
        {
            result = LegacyRosterImporter.Import(
                source, output, new Dictionary<int, string> { [7] = "Texas", [8] = "USC" }, 2004);
        }
        finally
        {
            File.Delete(source);
        }

        return output;
    }

    [Fact]
    public void ImportWritesEveryTeamWithTheSeasonItWasGiven()
    {
        var output = ImportTouchingSquads(out var result);
        try
        {
            Assert.Equal(2, result.Teams);
            Assert.Equal(20, result.Players);

            var read = HistoricalCsv.Read(output);
            Assert.Empty(read.Warnings);
            Assert.Equal(2, read.Rosters.Count);
            Assert.Equal(new[] { "Texas", "USC" }, read.Rosters.Select(r => r.School));
            Assert.All(read.Rosters, r => Assert.Equal(2004, r.Season));
        }
        finally
        {
            File.Delete(output);
        }
    }

    [Fact]
    public void ImportLeavesTheEvidenceColumnsAlone()
    {
        // Stats, awards, combine numbers and a draft pick are what a player
        // DID. A roster file has never held any of them, and writing a number
        // there would be this tool inventing a career.
        var output = ImportTouchingSquads(out _);
        try
        {
            var header = File.ReadLines(output).First().Split(',');
            foreach (var absent in new[]
                     { "PassYards", "Awards", "DraftPick", "DraftRound", "Forty", "StarRating" })
            {
                Assert.DoesNotContain(absent, header);
            }

            var player = HistoricalCsv.Read(output).Roster.Players[0];
            Assert.Empty(player.Evidence.Awards);
            Assert.Empty(player.Evidence.Stats);
            Assert.Null(player.Evidence.DraftPickOverall);
        }
        finally
        {
            File.Delete(output);
        }
    }

    [Fact]
    public void ImportWritesPlacesInAnOrderRatherThanRatings()
    {
        var output = ImportTouchingSquads(out _);
        try
        {
            var players = HistoricalCsv.Read(output).Rosters[0].Players;
            var ranks = players.Select(p => p.Evidence.LegacyRankPercentile).ToList();

            // The best player on the squad is 0 and the last man 100.
            Assert.Equal(0, ranks.First());
            Assert.Equal(100, ranks.Last());
            Assert.All(ranks, r => Assert.InRange(r!.Value, 0, 100));

            // Every player is alone at his position in this fixture, and a
            // group of one has no order to speak of, so it must not pretend to.
            Assert.All(players, p =>
                Assert.All(p.Evidence.LegacyRatingPercentiles.Values, v => Assert.Equal(50, v)));
        }
        finally
        {
            File.Delete(output);
        }
    }

    // ---- rating ------------------------------------------------------------

    private static RatingEngine Engine() => RatingEngine.Load(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "RatingModels.json"),
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "OverallFormulas.json"),
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "ArchetypeProfiles.json"));

    private static HistoricalPlayer Back(string last) => new()
    {
        FirstName = "A", LastName = last, Position = "HB",
        HeightInches = 72, WeightPounds = 210, ClassYear = "Junior",
    };

    [Fact]
    public void RankNearTheTopOfASquadRatesAboveRankNearTheBottom()
    {
        var engine = Engine();
        var top = engine.Generate("HB", null, Back("Top"), new RatingEvidence { LegacyRankPercentile = 0 });
        var bottom = engine.Generate("HB", null, Back("Bottom"),
            new RatingEvidence { LegacyRankPercentile = 100 });

        Assert.True(top.TargetOverall > bottom.TargetOverall,
            $"top of the squad {top.TargetOverall} should beat the last man {bottom.TargetOverall}");
    }

    [Fact]
    public void PlaceAtAPositionShapesTheAttributeItBelongsTo()
    {
        // The point of the whole exercise: two backs of the same standing come
        // out as different players when the source roster said they were.
        var engine = Engine();
        var evidence = new RatingEvidence { LegacyRankPercentile = 20 };

        var quick = engine.Generate("HB", null, Back("Quick"), evidence with
        {
            LegacyRatingPercentiles = new Dictionary<string, double>
                { ["SpeedRating"] = 0, ["StrengthRating"] = 100 },
        });
        var strong = engine.Generate("HB", null, Back("Strong"), evidence with
        {
            LegacyRatingPercentiles = new Dictionary<string, double>
                { ["SpeedRating"] = 100, ["StrengthRating"] = 0 },
        });

        Assert.True(quick.Attributes["SpeedRating"] > strong.Attributes["SpeedRating"]);
        Assert.True(strong.Attributes["StrengthRating"] > quick.Attributes["StrengthRating"]);
    }

    [Fact]
    public void AVerifiedMeasurementOutranksTheSourceRosterSOpinion()
    {
        var engine = Engine();
        var measured = engine.Generate("HB", null, Back("Measured"), new RatingEvidence
        {
            FortyYardDash = 4.32,
            LegacyRatingPercentiles = new Dictionary<string, double> { ["SpeedRating"] = 100 },
        });
        var stopwatchOnly = engine.Generate("HB", null, Back("Stopwatch"),
            new RatingEvidence { FortyYardDash = 4.32 });

        Assert.Equal(stopwatchOnly.Attributes["SpeedRating"], measured.Attributes["SpeedRating"]);
    }

    [Fact]
    public void ARosterNobodyImportedKeepsTheConfidenceItAlwaysHad()
    {
        // The legacy signal must not count against a hand-written file: an
        // absent import is a source that does not apply, not a gap in what is
        // known about the player.
        var engine = Engine();
        var withEvidence = new RatingEvidence
        {
            Role = "Starter",
            DraftPickOverall = 40,
            Awards = new[] { "All-American" },
        };

        var rated = engine.Generate("HB", null, Back("Ordinary"), withEvidence);
        Assert.True(rated.Talent.Coverage > 0.65,
            $"coverage {rated.Talent.Coverage:0.00} should not be diluted by an import nobody made");
    }

    // ---- the PS3 generation -----------------------------------------------

    /// <summary>
    /// A minimal big-endian file. Everything is the same container written the
    /// other way round, including the four-character codes, which are stored as
    /// integers and so arrive with their bytes reversed.
    /// </summary>
    private static byte[] BigEndianFile()
    {
        // One table, PLAY, with a text name and a couple of 7-bit ratings.
        var columns = new (string Name, int Bits, int Start)[]
        {
            ("PFNA", 88, 0),      // 11 bytes of text
            ("PPOS", 5, 88),
            ("PSPD", 7, 93),
            ("POVR", 7, 100),
        };
        const int recordBytes = 14;
        const int records = 3;

        var head = new byte[48];
        void BeU32(byte[] b, int o, uint v)
        {
            b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16);
            b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v;
        }

        BeU32(head, 8, recordBytes);
        head[20] = 0; head[21] = records;       // allocated
        head[22] = 0; head[23] = records;       // used
        head[28] = (byte)columns.Length;
        BeU32(head, 44, 0);

        var defs = new List<byte>();
        for (var i = 0; i < columns.Length; i++)
        {
            var (name, bits, start) = columns[i];
            defs.AddRange(name.Reverse().Select(c => (byte)c));
            var four = new byte[4]; BeU32(four, 0, (uint)bits); defs.AddRange(four);
            if (i == columns.Length - 1)
            {
                continue;
            }

            var type = new byte[4]; BeU32(type, 0, 3); defs.AddRange(type);
            var end = new byte[4]; BeU32(end, 0, (uint)(start + bits)); defs.AddRange(end);
        }

        var data = new byte[recordBytes * records];
        var names = new[] { "Jadeveon", "Sammy", "Teddy" };
        var pos = new[] { 11, 3, 0 };
        var spd = new[] { 90, 92, 84 };
        var ovr = new[] { 99, 97, 97 };
        for (var r = 0; r < records; r++)
        {
            var at = r * recordBytes;
            for (var i = 0; i < names[r].Length; i++)
            {
                data[at + i] = (byte)names[r][i];
            }

            void Put(int start, int bits, int value)
            {
                for (var i = 0; i < bits; i++)
                {
                    if ((value >> (bits - 1 - i) & 1) == 0)
                    {
                        continue;
                    }

                    var p = r * recordBytes * 8 + start + i;
                    data[p >> 3] |= (byte)(1 << (7 - (p & 7)));
                }
            }

            Put(88, 5, pos[r]);
            Put(93, 7, spd[r]);
            Put(100, 7, ovr[r]);
        }

        var table = head.Concat(defs).Concat(data).ToArray();
        var file = new List<byte>();
        file.AddRange("DB"u8.ToArray());
        file.AddRange(new byte[] { 0x00, 0x08 });
        file.AddRange(new byte[] { 0x01, 0x00, 0x00, 0x00 });
        var size = new byte[4]; BeU32(size, 0, (uint)(24 + 8 + table.Length)); file.AddRange(size);
        file.AddRange(new byte[4]);
        var count = new byte[4]; BeU32(count, 0, 1); file.AddRange(count);
        file.AddRange(new byte[4]);
        file.AddRange("PLAY".Reverse().Select(c => (byte)c));
        file.AddRange(new byte[4]);
        return file.Concat(table).ToArray();
    }

    [Fact]
    public void ByteOrderIsDetectedFromTheFileItself()
    {
        // Read both ways round and keep whichever declares a size matching the
        // file on disk -- evidence rather than a flag byte taken on trust.
        Assert.Equal(LegacyByteOrder.Big, EaDbFile.Parse(BigEndianFile()).ByteOrder);

        var ps2 = EaDbFile.Parse(BuildFile(
            new[] { new FixturePlayer(41, "A", "Player", 0, 1, 72, 200, 0, 0, 10, 10, 10) },
            new[] { (9, 41, 41) }, new[] { (41, 0, 0) }));
        Assert.Equal(LegacyByteOrder.Little, ps2.ByteOrder);
    }

    [Fact]
    public void ABigEndianFileReadsTextNamesAndBigEndianBits()
    {
        var play = EaDbFile.Parse(BigEndianFile()).Tables["PLAY"];

        Assert.Equal(3, play.DeclaredUsed);
        Assert.Equal(new[] { "PFNA", "PPOS", "PSPD", "POVR" },
            play.Fields.Select(f => f.Name));
        Assert.Equal("Jadeveon", play.ReadText(0, "PFNA"));
        Assert.Equal("Teddy", play.ReadText(2, "PFNA"));
        Assert.Equal(99, play.Read(0, "POVR"));
        Assert.Equal(92, play.Read(1, "PSPD"));
    }

    [Fact]
    public void RowCountsComeFromTheHeaderRatherThanAScan()
    {
        // The count is stated at table header +20 as (allocated, used). It was
        // missed on the first pass and stood in for by scanning back for the
        // last non-zero key; the header is the fact and the scan the fallback.
        var file = BuildFile(
            new[]
            {
                new FixturePlayer(41, "A", "One", 0, 1, 72, 200, 0, 0, 10, 10, 10),
                new FixturePlayer(42, "B", "Two", 1, 2, 73, 210, 1, 1, 11, 11, 11),
            },
            new[] { (9, 41, 42) },
            new[] { (41, 0, 0), (42, 1, 0) });

        var play = EaDbFile.Parse(file).Tables["PLAY"];
        Assert.Equal(2, play.DeclaredUsed);
        Assert.Equal(2, play.CountUsed("PGID"));
        Assert.True(play.Capacity > play.DeclaredUsed, "the fixture leaves spare rows");
    }
}
