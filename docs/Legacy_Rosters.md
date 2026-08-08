# Reading PS2-era NCAA Football rosters

The tool can read a roster file from the PS2-era NCAA Football games and write
it out as one of its own. Community "named" rosters from those games carry real
players with real numbers and measurables for well over a hundred teams, which
is the part of building a historical roster that is pure typing.

```
RosterGenerator.Cli import --legacy <roster file> --season 2004 --output MyRoster.csv
```

`--team` writes one school; leaving it off writes every team the file carries.
**`--season` is required** — the file records no year of its own, and inferring
one from the players on it would be a research result presented as something
the file said. In the app the same thing is **Import old roster**.

## The container

The files are EA's `DB` format, a bit-packed table container. `RosterGenerator`
reads three of its tables: `PLAY` (players), `TDYN` (teams) and `DCHT` (the
depth chart).

```
header      'DB', u16 version, u32 0, u32 dataSize, u32 0, u32 tableCount, u32 checksum
directory   tableCount * (char[4] name, u32 offset relative to the end of the directory)
table       48-byte header; [+8] record length in BYTES, [+28] column count,
            [+44] the bit at which the first named column starts
columns     (char[4] name, u32 bits, u32 type, u32 endBitOffset), 16 bytes each --
            EXCEPT the last, which is truncated to (name, bits) and takes 8
records     fixed length, bit-packed, little-endian; bit n is bit n%8 of byte n/8
```

A column starts at its stored *end* offset minus its width. The column array is
sorted by name read as a little-endian integer, so it is a lookup table rather
than a layout.

Three things the format does not say out loud:

- **The row count is at table header +20**, as an allocated count then a used
  one, both `u16`. This was missed on the first pass and stood in for by
  scanning back from the end for the last row with a non-zero key — which
  agrees with the header on all six tables of both files (8893/7350/119 and
  4471/3995/83), but is a guess where the header is a fact. The reader now
  takes the header and keeps the scan as a fallback.
- **A column definition's fourth word is the NEXT column's start**, not this
  column's end. The two are the same number whenever columns run consecutively,
  which is nearly always — so reading it as an end and subtracting the width is
  right almost everywhere and silently wrong wherever a record has a gap or
  lists its columns out of order. That off-by-one produced twenty-two columns
  across three files that looked individually corrupt and needed a correction
  table; reading the word correctly dissolves every one of them.
- **`PFSH`, `PMSH` and `PSSH` are signed**, in two's complement. Nothing marks
  them: their declared type is the same 3 every other column carries.

Verified against community exports of both files, cell for cell, **with no
corrections of any kind: 1,018,590 of 1,018,590 (100%)**.

Names are stored a character at a time, ten fields for a first name and
thirteen for a last: 1–26 lower case, 27–52 upper, and four punctuation codes.

## Which team a player plays for

The player table records no team at all. Squads occupy consecutive runs of
player id, and `TDYN` names two captains per side, which places each team in a
run. Where two neighbouring squads' runs touch there is no gap to cut on, and a
boundary in the wrong place silently moves a dozen players to the wrong school.

The depth chart settles it. The chart is written in several passes over the
league, and within a pass a team lists each `(position, depth)` slot at most
once. So for a candidate boundary: split the chart rows either side of it,
group them into runs of consecutive table rows, and count slots used twice
inside one run. A boundary in the wrong place drags a player onto a chart that
already has somebody in his slot.

On both files this was built against every boundary resolves to a unique answer
with **no collisions at all**, and every team ends up holding its own captains
— 119 of 119 on one file, 83 of 83 on the other.

`data/LegacyTeamIds.json` maps team ids to schools. **Every entry was read off
the roster it belongs to**, and each names a player who identifies it, because
the ids are not reliably alphabetical: USC and Utah sit next to each other, and
both Louisiana schools appear under names they had stopped using. One entry
(Army) rests on the ordering alone and says so by having no player against it.

## What comes across, and what does not

| Roster CSV column | Source |
|---|---|
| FirstName, LastName | the per-character name fields |
| Position | `PPOS`, 21 values |
| Number | `PJEN` |
| HeightInches | `PHGT`, plain inches |
| Weight | `PWGT` + 160 — the same encoding CFB27 uses |
| Class | `PYER` |
| SkinTone | `PSKI`, counted from 1 rather than 0 |
| Role | the depth chart |
| LegacyRank, Legacy\* | see below |

Skin tone is carried rather than dropped. It is a value somebody chose
deliberately when the roster was made — a record, not the tool guessing what a
real person looked like, which it still never does.

Hometown, home state, previous school and redshirt status have no counterpart
in the format. Stats, awards, combine numbers and draft position are empty for
the same reason they are empty on an export: a roster file records what a
player *is*, never what he *did*.

## Ratings are not imported

Eighteen of CFB27's fifty-seven rating columns have any counterpart in the
older games:

```
Speed  Acceleration  Agility  Strength  Awareness  Catching  Carrying
BreakTackle  Jumping  Tackle  PassBlock  RunBlock  ThrowPower
ThrowAccuracy  KickPower  KickAccuracy  Stamina  Injury
```

Running each of EA's 79 overall formulas and summing the coefficient weight
those eighteen can supply:

| Position | Weight covered |
|---|---|
| K, P | 100.0% |
| FS / SS | 61.3% |
| DE | 60.9% |
| LB | 56.8% |
| DT | 56.0% |
| CB | 55.4% |
| HB | 54.2% |
| WR | 49.9% |
| OL | 46.2% |
| QB | 41.9% |
| TE | 40.7% |
| **mean** | **54.3%** |

So writing the old ratings across would leave about 46% of every overall — and
nearer 60% at quarterback — to be invented and then presented as history. On
top of that the stored numbers are five or six bits wide on a scale nobody has
anchored: a speed of 28 out of 31 means nothing outside the game it was written
in.

**What crosses over is the order**, which survives the trip in a way a rating
does not.

- `LegacyRank` is where a player stood on his own squad, 0 for the best man on
  it and 100 for the last. It becomes a talent signal, scored through
  `legacyRankToOverall` — a curve measured over EA's own rosters, 138 squads of
  exactly 85 and 11,730 players, by sorting each squad and averaging the
  overall at each place in the order. It is weighted below draft position,
  awards and production, because those are facts about what a player did and
  this is somebody's recollection of who was good.
- `LegacySpeed`, `LegacyStrength` and the rest are where he stood among others
  **at his own position on his own squad**, on the same 0–100 scale. Each nudges
  its matching attribute by up to `legacyShapeMaxShift` points, and each is
  offered to archetype selection as a rule field.

That second part is what stops two backs of the same standing coming out as the
same player. On the 2004 USC roster it separates them by itself:

```
HB Reggie Bush    OVR 84  HB_ElusiveBack  spd 95  str 67
HB LenDale White  OVR 83  HB_PowerBack    spd 78  str 88
```

A verified measurement always wins: a 40-yard dash in the roster file fixes
speed outright and the ranking is not allowed to move it afterwards.

## Limits worth knowing

- An older squad is about 62 players against CFB27's 85, so filler synthesis
  still runs to fill the bottom of the roster.
- Heights and weights are a roster editor's values, not a media guide's.
- A roster with no evidence beyond the import is rated on the ordering alone,
  and reports Low confidence accordingly. Supplying stats, awards or a draft
  pick for the players you know about is what turns it into your roster.
- Players the source never named are dropped. Whole divisions shipped unnamed
  in those games, and a roster row with nobody on it is not a player.

## Schools the game no longer carries

Of the 119 teams on a 2004 I-A roster, exactly one is not in CFB27: **Idaho**.

CFB27 ships five generic FCS teams — East, Midwest, Northwest, Southeast and
West — each with a real 85-man roster. Idaho is written onto **FCS East**, and
`data/TeamMappings.json` records that as an alias plus a `standInTeam`:

```json
{ "teamId": 255, "names": ["FCS East", "FCSE", "Idaho"], "standInTeam": "FCS East" }
```

**The redirect cannot go by `TeamIndex`.** All five FCS teams carry index 255,
and so do the 4,527 players in the recruiting pool, so asking the player table
for "team 255" hands back the lot. `Player` has no other team column:
`PrevTeamIndex` is 255 for all of them too, and 3,875 of the 4,527 are freshmen.

The teams know, though. Every team row — FBS and FCS alike — has a `Roster`
reference into one shared table whose rows hold exactly 85 player references,
the same 32-bit encoding and player tag the depth chart uses:

```
FCS East  Team.Roster -> row 33  -> 85 refs, first player rows 6373, 5875, 11651
USC       Team.Roster -> row 129 -> 85 refs, first player rows 2, 12223, 298
```

`TeamRosterTable` follows it, so a stand-in school writes to exactly the right
eighty-five slots. Verified on a real dynasty: generating 2004 Idaho changed 85
player rows, **all 85 of them FCS East's own, none outside it**.

Two consequences worth knowing:

- The dynasty's team list deliberately drops rows carrying the no-team
  sentinel, which is why the FCS teams do not appear in `list-teams`. An
  overlay entry naming a stand-in is admitted anyway — it is resolved by team
  name, never by index, so it cannot conjure a team the dynasty lacks. If the
  named team really is absent the conversion says so and writes nothing.
- Anything that re-asks `TeamIndex` after a conversion has the same problem.
  Equipment did, and put an era's helmets on all 4,527 players in the pool.
  Conversions now record the slots they claimed and equipment follows those.

To add another departed school, give its name to whichever FCS team should hold
it. There are five, so five can be recreated in one dynasty before they start
overwriting each other.


## The PS3 generation — NCAA Football 14

`USR-DATA` inside a PS3 save folder is **the same container, big-endian**.
Because the four-character table and column codes are stored as integers rather
than text, their bytes arrive reversed: what reads as `THCD` is `DCHT`. The
reader detects which way round a file is by parsing the header both ways and
keeping the one whose declared size matches the file on disk.

An NCAA 14 roster carries ten tables against a PS2 roster's three, and the
player table is a different proposition:

```
PLAY  8631/9100 rows, 112 bytes, 133 columns      TEAM   141/146
DCHT 11068/11464                                  COCH   403/409
CSKL   378/409                                    STAD   197/200
CONF    25/26   DIVI 22/22   INJY 0/0   TUNI 0/0
```

- **Names are plain text** — `PFNA` 11 bytes, `PLNA` 13 — not a column per letter.
- **Ratings are real.** Seven bits each, overall running 58–99 across 8,631
  players with a mean of 74.8. The PS2 generation's five-bit bucket index, and
  the scale problem that came with it, are simply absent.
- **`TGID` is on the player**, so none of the id-run and depth-chart machinery
  above is needed: the file says who plays for whom.
- **Forty attributes**, against eighteen on PS2.

Reading the 2013 roster gives the season it should:

```
RE   #7  Jadeveon Clowney   OVR 99      WR #2  Sammy Watkins       OVR 97
LT  #75  Jake Matthews      OVR 98      QB #5  Teddy Bridgewater   OVR 97
ROLB #11 Anthony Barr       OVR 98      LT #73 Greg Robinson       OVR 97
```

### Teams name themselves

This generation's `TEAM` table carries `TDNA` ("Alabama"), `TLNA`, `TSNA`
("Bama") and `TMNA` as plain text, keyed by the same `TGID` the players hold.
So there is nothing to identify by hand: `data/LegacyTeamIds.json` exists only
because the PS2 team table has an id and no name at all.

All **126 team names carrying players already resolve** against the tool's own
school list, with no aliases added.

The file's own name always wins over `LegacyTeamIds.json`. The two generations
number different leagues — NCAA 14 reaches TGID 235 where the PS2 map stops at
230 — so letting the older map speak here would quietly refile teams wherever
they disagree, and it agrees at the start, which is exactly what makes that
failure hard to notice.

A team the file lists but never fields is **left out** rather than written as a
squad of nobody. In this roster 141 teams are listed and 126 fielded.

### What is read

Everything except skin tone: names, positions, jersey numbers, heights,
weights, class years, redshirt status, depth-chart roles and all 42 ratings.

Skin tone is not read from this generation. Its player table carries face and
head assets rather than a tone, and reading one off those would be inference
about a real person's appearance.

The columns that looked misplaced here — the player id, weight, class year and
the rest — were the same off-by-one described above, not damage.

### The attribute mapping

Twenty-four attributes beyond the PS2 eighteen. Each was read from its
four-character code, checked against the position that ought to lead it, and
then confirmed against the field dictionary in a community editor's own
configuration — which caught one the position test could not:

| Field | Reading | Leads | Trails | Gap |
|---|---|---|---|---|
| `PMCV` | ManCoverage | CB 81 | C 34 | +47 |
| `PZCV` | ZoneCoverage | FS 77 | C 34 | +43 |
| `SPCT` | SpectacularCatch | WR 68 | C 19 | +48 |
| `PPRS` | Pursuit | MLB 77 | QB 33 | +44 |
| `PBSH` | BlockShedding | DT 75 | WR 37 | +37 |
| `PPBS` | PassBlockPower | LT 78 | WR 41 | +37 |

**Twenty-one of twenty-one came out in the predicted direction by at least 28
points** — but a position signature cannot separate two attributes with the same
profile, and one was wrong for exactly that reason: `PPRC` is **Play
Recognition**, not Press, and defensive backs lead both. The editor's dictionary
also named `PYRS` as Press (it had been taken for a class year) and `PBFW` as
Run Blocking Footwork.

Against EA's 79 overall formulas those forty-four attributes carry **89.2% of
the coefficient weight**, against 54.3% for the PS2 eighteen — 100% at kicker and
punter, 84–96% everywhere else, and **56.6% at quarterback**, where NCAA 14 had
one throw-accuracy number to CFB27's three and none of `ThrowUnderPressure`,
`ThrowOnTheRun`, `BreakSack` or `PlayAction`.
