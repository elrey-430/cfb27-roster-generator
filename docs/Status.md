# Project Status

_Last updated: 2026-08-08 — NCAA 14's own ratings are carried across._

## Current status

**A roster from NCAA Football 14 brings its ratings with it.** The PS3-era save
holds the same EA `DB` container big-endian, and unlike the PS2 files it records
its ratings on a real 0-99 scale — the same scale CFB27 uses.

```
RosterGenerator.Cli import --legacy <roster file> --season 2013 --output MyRoster.csv
```

**Forty-two of CFB27's fifty-seven rating columns are copied and locked.**
Nothing afterwards moves them: not the class-year experience shift, not the
position or class caps, not the calibration solve. A junior's awareness is
normally held to 95 because that is where the game's own juniors stop; that is a
statement about what the game does, and it yields to a number somebody actually
recorded. The one thing that still outranks a source rating is a verified
measurement — a stopwatch is evidence about the person, a rating is somebody's
reading of him.

**The fifteen CFB27 asks for that NCAA 14 never had** — throw under pressure,
break sack, play action, the deep route runs — come from the archetype's
measured profile at that player's overall. Real numbers where they exist, and
where they do not, what the game itself gives this kind of player at this level.

**One number becomes three where it has to.** NCAA 14 stores one throw accuracy
and one route running; CFB27 stores a short, a medium and a deep of each. The
archetype's profile decides the shape and the source's number decides the level,
so a 95 accuracy on a field general comes out 97/95/93 and steeper on a pure
scrambler — and the three still average what the source said.

**The archetype comes out of the ratings**, scored in each attribute's own
measured scatter rather than from a stat line an imported player does not have.
Fed the game's own average quarterback of each QB archetype at 70/78/85/92/99,
it recovers the archetype **20 times out of 20**.

**And the overall follows the ratings, not the source's own number.** The two
came from different formulas over different columns, and chasing the source's
would pay for the difference out of the handful of attributes nobody recorded.
The report says what the ratings came to.

Checked against CFB27's own players: a quarterback of every QB archetype at five
overalls, compared attribute by attribute against what the game gives that
archetype at the same overall. **Nothing lands more than two of the game's own
standard deviations off.**

## The PS2 generation

**A twenty-year-old roster file is also a source of players.** Community "named"
rosters for the PS2-era games carry real people with real numbers and
measurables across more than a hundred teams, which is exactly the part of
building a historical roster that is pure typing.

The container is fully decoded — **1,018,590 of 1,018,590 cells** against
community exports of three different files. The row count is stated at table
header +20, as an allocated count then a used one; three body-shape fields are
signed with nothing to mark them; and the fourth word of a column definition is
the *next* column's start rather than this column's end, which is the single
misreading that made twenty-two columns look like they carried stale offsets.
They never did.

**Which team a player plays for is not recorded at all** in that generation.
Squads run in blocks of consecutive player id and the team table names two
captains per side, which is enough until two squads' blocks touch — then there
is no gap to cut on and a boundary in the wrong place moves a dozen players to
the wrong school. The depth chart settles it: within a pass a team lists each
`(position, depth)` slot once, so the boundary that makes the fewest players
collide with somebody already in their slot is the one the game is describing.
Every boundary in both files resolves with **no collisions**. NCAA 14 simply
records the team on the player, and names its own schools in plain text.

**PS2 ratings are deliberately not imported.** Eighteen of CFB27's fifty-seven
rating columns have any counterpart in the 2004 game, and those eighteen carry a
mean of **54.3%** of the weight in EA's own overall formulas — 41.9% at
quarterback — on a five- or six-bit scale nobody has anchored. NCAA 14's
forty-two carry **89.2%**, on a scale that needs no anchoring, which is what
makes the difference in treatment defensible.

**What crosses over from a PS2 file is the order.** `LegacyRank` — where a
player stood on his own squad — becomes a talent signal, scored through a curve
measured over EA's own rosters (138 squads of exactly 85, 11,730 players). The
eighteen `Legacy*` columns hold where he stood among others at his own position,
and each nudges its matching attribute and is offered to archetype selection.
That is what stops two backs of the same standing coming out identical:

```
HB Reggie Bush    OVR 84  HB_ElusiveBack  spd 95  str 67
HB LenDale White  OVR 83  HB_PowerBack    spd 78  str 88
```

A verified measurement still wins outright, and an import counts against
nothing — a roster written by hand keeps exactly the confidence it always had.

See [Legacy_Rosters.md](Legacy_Rosters.md) for both formats, the benchmark
tables, and the team id map, every entry of which was read off the roster it
belongs to rather than assumed from the ordering.

## Previously

**The tool could read a roster file and not write one.** That asymmetry has been
the top of the backlog since Milestone 15 and cost users twice: correcting one
player in ten thousand meant retyping the roster or editing the result in a
third-party editor, where the correction was invisible here and lost on the next
run — and every new project started from a blank template rather than from what
the dynasty already had.

**`RosterCsvExporter` writes a dynasty out as a roster file**, in the template's
own shape and column order. `export` on the command line, **Export roster file**
in the app. Omit the team and it writes every team the dynasty carries — a whole
season in one file, which the generator reads straight back because the `Team`
column decides where players go.

**The round trip is lossless for identity.** Exporting Florida State out of a
base save and feeding the file straight back in, compared player by player:

```
position 85/85   jersey 85/85   height 85/85   weight 85/85
class    85/85   redshirt 85/85  town 85/85    state 85/85
prev school 85/85
```

Compared *by player*, not by row: a recreated player takes whichever donor slot
fits his position, so what has to survive is the man, not his seat.

**Two things the first attempt got wrong, both caught by measuring:**

- Four players came back as having never transferred. Their `PLYR_PREVTEAMID`
  was 1009 — "a school the dynasty does not model" — which has an id but no
  name, and blank read back as "never transferred", a different and untrue
  thing. `PlayerSchema.PreviousSchoolNotInDynasty` (`Unlisted`) is now written
  on export and read back silently.
- Roles are read off the dynasty's own depth chart, which only became possible
  last release. Heading a slot named for a real position makes a starter; the
  specialist slots (`3DRB`, `KR`, `SLWR`) describe a package, not a starting
  job, so leading one does not promote a third receiver.

**The evidence columns are deliberately empty** — statistics, awards, combine
numbers, draft slots. A save records what a player *is*, never what he *did*, so
exporting cannot invent a stat line, and pretending otherwise would put made-up
numbers in somebody's file. An exported roster therefore reproduces identity
exactly and rates from scratch.

Tests: 556/556.

## Previously

**A recreated roster took the field in the donor's order.** Reported: depth
charts "way out of alignment" after generating. The cause is exact — a depth
chart points at player *rows*, in the order the donor's players ranked, and the
tool replaced who lives in each row while leaving the chart alone. The slot the
game believed was the starting quarterback held whoever landed there. Nothing in
the game corrects it, which is also what proves the game honours a stored chart
rather than re-sorting on load.

**Decoded.** Three tables: `Team.DepthChart` names a chart row, the chart's 35
slots each name a `Player[]` row, and that row lists up to six players in order.
Every cell is the 32-bit reference the CharacterVisuals link uses; the player
tag is 8496. Team row order is *not* team index — Florida State is Team row 38
and team 27 — so the link is followed rather than assumed. Only `Player[]` is
ever rewritten, so the structure cannot be broken by a rebuild.

**Fifteen of the 35 slots are not positions**, and none of it is guessable:
`GAD` is 59% halfbacks and 40% receivers, `LS` is 78% tight ends, `SLCB` draws
on corners and both safeties, and depth is 6 at receiver, 5 at corner, 4 at
halfback, 3 nearly everywhere else. All measured by
`tools/measure_depth_charts.py`.

**The mirrored pairs are one assignment, not two picks.** `LT`/`RT`, `LG`/`RG`,
`LE`/`RE`, `LOLB`/`ROLB` each list both sides. The same player never heads both
— 0 of 143 teams, all four pairs — and the better of the two is on the left 87%
to 92% of the time. So the pool is sorted once and dealt alternately, left
first.

**Verified end to end on a real save.** Generating 2023 Florida State and
reading the save back:

```
before   QB   Daniels(77), Willow(76), Sperry(74)
after    QB   Travis(92), Glenn(77), Rodemaker(70)
before   WR   Robinson(92), Danzy(84), Lopez(79), ...
after    WR   Coleman(93), Wilson(88), Douglas(80), ...
after    LT   Byers(LT 84), Armella(RT 70)
after    RT   Scott(LT 73), Sapp(RT 65)
```

The tackles show the deal working: the pool sorted 84, 73, 70, 65 and went left,
right, left, right.

**Nothing is asked of the user.** A dynasty with no depth chart — ordinary for a
folder from the community export tool — is skipped in silence, and
`LockedEntries`, which points at entries a user pinned, is never rewritten.

Tests: 547/547.

**`DraftRound` was in the template and unread.** Reported: a player entered as
the 33rd pick — round 2, pick 1 — came out at 97 or better. The tool read
`DraftPick` as an overall number and used `DraftRound` only when the pick was
missing, so *round 2, pick 1* was the first selection of the entire draft.

**Both spellings now work**, and which one the user meant is decided by
arithmetic rather than by a setting:

| Written | Read as |
|---|---|
| round 2, pick 1 | 33rd overall |
| pick 33, no round | 33rd overall |
| round 2, pick 45 | 45th overall — the 13th pick of round two |
| round 7, pick 20 | 212th overall |
| round 2, no pick | the middle of round two |

The rule: **a pick larger than a round holds cannot be a position inside one**,
so it is an overall number. Below that, a round makes the pick a position
within it. Round one needs no decision because the two readings agree there.

A round and a pick that flatly contradict each other — round 2, pick 200 — is
reported, and the pick is believed as the more specific of the two. A pick one
round past where the arithmetic puts it is *not* reported: rounds run past 32
selections when compensatory picks are awarded, and round 7, pick 240 is
ordinary.

**The reading is always stated** in the player's reasons — "Drafted #33 overall
(round 2, pick 1)" — because silently reinterpreting somebody's number would be
worse than the bug.

Verified end to end on the reported case: round 2 pick 1 and a bare pick 33 now
produce the same player, and both sit below a genuine first-round pick.

Tests: 535/535.

**The cap on a player the file says little about now reads role first and class
second.** It used to read class alone, which conflated "young" with "unknown" —
and measuring says class is much the weaker of the two.

The 90th percentile of overall by role and class, across 11,730 players on 138
teams:

| | Freshman | Sophomore | Junior | Senior |
|---|---|---|---|---|
| Starter | 82 | 84 | 87 | 87 |
| Backup | 78 | 77 | 77 | 77 |
| Reserve | 73 | 73 | 73 | 73 |
| Walk-on | 68 | 68 | 68 | 67 |

**Class barely registers below the starting eleven.** Backups run 78/77/77/77
and reserves 73/73/73/73 whatever their year. Only starters show a class
effect, and even there it is five points across four years.

So the old cap — 68, 74, 78, 82 by class — was wrong in **both directions at
once**: it held a freshman backup ten points under where the game puts one, and
let a senior reserve nine points over. It falls back to the old per-class value
when a roster file names no role at all.

**This is the change that did what widening the role curve could not.**

| | Biggest pile | Distinct overalls | MAD vs EA |
|---|---|---|---|
| Before the role work | 25 | 20 | 2.69 |
| Role curve + spread | 15 | 27 | 2.74 |
| **Plus this cap** | **8** | **32** | **2.68** |
| EA's own roster | 9 | 25 | — |

Better on every measure at once, including the curve fit that the previous two
changes had each cost a little. The pile of fifteen freshmen stacked on 68 was
this cap all along, not the spread. The low 80s are now spread across 80, 81,
82 and 84 rather than stacked on 80.

What is still unlike the game is the bottom: 28 players under 70 against EA's
16, and 25 in the 70–74 band against 32. That is the filler and reserve
population, and it is the next thing worth measuring.

Tests: 522/522.

**A generated roster was coming out in spikes where the game's is a curve.**
Asked to widen the role and production curves. Measuring first showed the
production curves were not the problem — they already span 56 to 96, and a
starter with no draft slot and no award runs 73 to 86 across a plausible range
of seasons. The role scores were, in two separate ways.

**They were three to five points low.** `roleScores` is now the median overall
the game itself carries at the roster ranks each role occupies, read straight
out of `medianOverallByRank`: starter 78 (was 76), backup 73 (69), reserve 68
(64), walk-on 64 (61). Against EA's own Florida State the 75–79 band held 3
players where the game holds 21; it now holds 18.

**And a single score cannot do the job at all.** 64 of the 2023 Florida State
file's 75 rows carry no stats, no award and no draft slot — eleven of them the
identical "Reserve, redshirt freshman". Every one of those blended to the same
number: **18 players on exactly 78, 25 on exactly 68.**

The game spreads 14 points inside its starters (73 at the 10th percentile, 87
at the 90th) and 8 to 9 inside every other role. Class year does not explain
it: across 11,730 players on 138 teams, class moves the median within a role by
one point, four for starters. It is variation the roster file gives no evidence
about.

**So `RoleSpread` reproduces the distribution without claiming to know which
player is which.** Within a role, players whose entire record is that role are
ordered by what evidence they do have — blended overall, then class seniority,
then name — and laid along the measured percentile curve. This is what
`RosterFiller` has always done for empty slots, and for the same reason. One
stat, award or draft slot and a player keeps their own number; a single player
in a role is left alone; the ordering never uses chance, so the same file
always produces the same roster.

| | Biggest pile | Distinct overalls | MAD vs EA |
|---|---|---|---|
| Before | 25 | 20 | 2.69 |
| After | 15 | 27 | 2.74 |
| EA's own | 9 | 25 | — |

The 15 remaining are freshmen on the Low-confidence class cap of 68, which is a
different rule doing its job. Mean absolute deviation moves 2.69 → 2.74 against
a 3.00 bar: the curve fit is a hair worse, the shape a great deal better, and
shape was the ask.

**One engine change was needed.** `Generate` gained `overallOverride`, because
a roster-level pass can see something scoring players one at a time cannot. It
lands after the program and secondary adjustments — the measured curve is
already the answer, and applying those on top would move it back off — and
before every cap, which are about what the game can hold.

Tests: 514/514.

**The draft curve now spans the whole drafted band.** `draftScores` runs 99 at
pick 1 down to 85 at pick 256, so a pick number is a rough statement of overall
on its own — 93 at the end of round one, 90 at the end of round two, 88 through
round three, high 80s after. Measured on a receiver, whose position cap is 99:

```
pick    1    5   10   20   32   64  100  160  256
OVR    99   97   96   95   93   90   88   86   85
```

A halfback tops out at 96 rather than 99, because 96 is the best halfback the
game itself carries. Position caps were measured from the game and this does
not overrule them.

**A draft slot is a floor and never a ceiling.** `signalFloors.draft` is 0, so
a pick floors at exactly what it implies and better evidence lifts from there.
The case that was asked for:

| | Heisman season | Ordinary season |
|---|---|---|
| Taken 45th | **96** | 92 |
| Taken 240th | **96** | 85 |

Getting there needed the *award* tolerance tightened from 6 to 2. At 6 a
Heisman floored at 92 — exactly what pick 45 implies — so Derrick Henry came
out 92 either way and his draft slot had quietly become the verdict on his
season, which is the thing the change exists to prevent.

**Undrafted players are capped at 85**, where the drafted band begins, so the
two meet rather than overlap. This applies to an explicit `UDFA` only, never to
a blank draft column: "undrafted" is a statement about the player, an empty
column is a gap in the record, and most all-time rosters carry no draft data at
all.

**The low-80s diversity did not follow, and it is worth saying so.** On the
2023 Florida State roster the 80–84 band holds 8 players before and after,
because 64 of its 75 rows carry no draft data and exactly one says `UDFA` — so
neither the floor nor the cap reaches them. The undrafted cap can only spread a
band the file actually populates. Raising `undraftedFreeAgentScore` was tried
and makes it worse, not better: at 67 the undrafted profiles spread 69–85, at
80 they collapse onto 80–85, because the signal becomes a floor of its own.

**Cost against the game's own roster:** mean absolute deviation from EA's
Florida State curve moves from 2.29 to 2.69, against a 3.00 bar and the 3.02
the manual human recreation scores. Eight players moved, all drafted, all
upward — Verse 92→95, Fiske 88→92, Green 86→91.

Tests: 508/508.

**Every drafted player is now rated at least 85 overall.** Requested, and the
numbers show why. Draft is one signal of five in the weighted blend, and the
existing per-signal floor is proportional — it tracks the draft curve down, so
a late pick floored at a late-pick number. A seventh-round pick generated at
**77**; a sixth-rounder at **80**, which is exactly what a player whose roster
row says nothing about the draft also gets.

That is the wrong shape for the fact. About 250 players are drafted out of some
ten thousand in FBS, and no weighting of a single season's evidence can express
that.

`draftedOverallFloor` (85) is applied after the program and secondary
adjustments and before every ceiling, so a position cap still wins. Above the
floor the draft slot does all its usual work and the order stays strict — picks
1, 10 and 32 come out 97, 94, 91. Below it the order flattens on purpose: a
third-rounder and a seventh-rounder meet at 85.

**Undrafted is not unknown, and neither gets the floor.** `UDFA` in the
`DraftPick` column is a statement about the player; an empty column is a gap in
the record. Both stay where they were.

**One rule had to give way.** The depth-consistency pass pulls a backup rated
above the starter back under them, unless the evidence is High confidence *and*
carries a draft or award signal. A player whose row holds only a draft pick
reaches Medium — so the rule would have caught precisely the player its own
description names, "a future first-round pick genuinely can sit behind a senior
starter", and undone the floor a moment after the engine applied it. A draft
slot now justifies it on its own.

**The cost is measured.** Mean absolute deviation from EA's own Florida State
curve moves from 2.12 to 2.29, against a 3.00 bar and the 3.02 that the manual
human recreation of that roster scores.

Tests: 504/504.

**Body build is chosen from position, height and weight.** Requested, with no
new input from the user — and the tool already has all three.

**The field is `CharacterBodyType` on the Player table, and `Freshman` is the
stored name for the build the game's editor calls Lean.** Nothing in the schema
says so; it was read out of a save in which five named Florida State players
were each given a different build in-game. The `CharacterVisuals` blob also
carries a `bodyType` integer and it is *not* this field — only one of the five
had the key, and its value did not track the build that was set.

**Two sources decide it, and they answer different questions.** EA's own player
builder says which builds a given height and weight can carry, which is what
stops a 175 lb receiver being written as Muscular. The base save's census says
what the game puts on each position. Where a position's build is not in
question — ends and tackles Muscular at 81–97%, interior line and defensive
tackle Heavy at 76–90% — the position decides outright; everyone else chooses
among the light builds the builder permits.

**One deliberate departure from the builder: the Lean cutoff.** EA's climbs
from 175 lb at 5'9" to 210 lb at 6'5" before Standard is available; the game's
own rosters run Standard down to 160 at every height. It is set to 170 lb at
6'0" and below, then +5 lb per inch — 195 by 6'5".

The floor and the slope come from different places. **170 is the project
owner's call**, worth six points of agreement over EA's table. **The slope is
the game's own**: among skill players a 6'2"–6'3" man at 170–179 lb is Lean
46–55% of the time where a 5'10"–5'11" man at the same weight is Lean 19–25%,
so a tall light player really does read as lanky in the data. It is free —
82.5% agreement with or without it — and takes the count of Lean players
written from 437 to 730, against the 1,007 the game itself writes.

**82.5% agreement across 16,257 players, against a ceiling of 86.8%** — the
best any rule reading those three fields could do, since the game's own choice
varies within a cell. Every position that takes its build outright sits exactly
on its ceiling. What is left is mostly irreducible: halfbacks split 53/47
between Standard and Muscular at every weight, defensive tackles 76/23 between
Heavy and Muscular.

**Confirmed end to end on a real save.** Generating 2023 FSU into the supplied
dynasty and reading the result back moved 26 builds, all on Florida State and
none anywhere else — including a 310 lb guard who had been *Thin*, a 290 lb
defensive tackle who had been *Thin*, and a 305 lb defensive tackle who had
been *Standard*.

Tests: 489/489.

**A generated player no longer inherits the slot's `IsNIL` flag.** Requested:
everyone the tool writes should default to false.

**What the flag means** (corrected — the first pass through this read it as a
compensation field, which it is not): `IsNIL` marks the slot as holding a
**real person**, an athlete who signed an NIL agreement to appear under their
own name and likeness, and **the game will not let such a player be edited.**

That makes the inheritance wrong twice over. A recreated player is not the
licensed athlete whose slot they took, so the flag asserts something untrue
about them — and it leaves them locked against editing in the game, which is
the opposite of what a recreation is for. The census says how much of a roster
that covers: 1.7% of players in the 40s are flagged, 42.4% in the 60s, 78.0%
in the 70s, and **100% of the 114 players at 90 and above**. A recreated
roster is built on the best slots the save has, so it was the whole starting
eleven arriving locked.

**The NIL money fields are separate and deliberately left alone.** They do not
move with the flag: 3,473 of the 7,246 players a base save marks `false` still
hold a non-zero `BaseNILValue`. Where the game does zero them is a team change,
which `TransferPlayer` has handled since Milestone 1.

Filled depth slots are cleared too, for the same reason their abilities are:
the slot has just been re-rated as a 63-overall walk-on, so what was true of it
at 88 is not true of it any more. A slot the tool did *not* generate into is
left exactly as it was.

The donor fixture holds `false` throughout, so a test asserting the output is
`false` would pass with the feature deleted. The new tests flip every slot to
`true` first; two of the seven fail against the old code.

Tests: 456/456.

**Writing a dynasty save from the app has never worked in a shipped build.**
Reported against v0.7.2-alpha, generating a full FBS roster:

```
Error: ENOENT: no such file or directory, open
  '...\CFB27-Roster-Generator-0.7.2-alpha-win-x64\tools\native-save\Output\Generated_Roster.csv'
```

The roster was written, and written correctly — to `Output\Generated_Roster.csv`
beside the executable, which is the shipped default and a *relative* path. The
sidecar runs with its working directory set to `tools\native-save`, because
that is where its scripts and its `node_modules` are, so the same relative path
named a file that has never existed. Nothing to do with the size of the roster:
the step after generating it was simply being reached.

**Every path handed to the sidecar is now resolved against the caller's
directory first**, in `NativeSave.ApplyArguments` and `ExtractArguments`, the
only two places a path crosses that boundary.

Why 442 tests missed it is worth writing down: **they all wrote to a temporary
directory**, which is to say they all passed absolute paths, so every one of
them exercised the one case that worked. The new tests pass relative paths on
purpose, and three of them fail against the old code — including one that runs
the real sidecar.

The sidecar also stops answering with a stack trace. A table it cannot find is
now named, with "nothing was written and your save was not touched", and a save
that is not there says so rather than complaining about a missing FBCHUNKS
header. The C# layer surfaces stderr, so that is what the user reads.

Tests: 449/449.

**A whole-season roster could not actually be generated.** Reported: importing
a roster for all teams was limited by team selection. It was worse than a
limit — the desktop app sent the team it had detected on *every* run, and
`HistoricalCsv` gave an explicit team priority over each row's own, so a
119-team file was silently written onto whichever school appeared first.
10,115 players, one team, nothing reported. Reproduced before changing
anything: a three-team file with `--team "Florida State"` put all six players,
Alabama's and Michigan's included, onto Florida State.

**The Team column now decides.** A row that names its team goes to that team;
the caller's team is a fallback for rows that leave the cell blank, and the
season override is untouched because a season really is roster-wide. Verified
end to end on a filled 2010 season against the full base save, with `--team`
deliberately set: **10,115 players across 119 teams, 85 each, zero misplaced.**

**The app stops asking a question the file already answers.** The picker
disables itself when the file names teams and says which case applies — "Your
file covers 119 teams and each player goes to the one their Team cell names" —
rather than leaving the user to infer it from a player count. It stays for the
one case it exists for: a file with no Team column at all.

One existing test asserted the old contract outright
(`SimpleCsvCallerTeamOverridesFileTeamColumn`). It was the bug, written down as
intent, which is why nothing caught this — it is rewritten to state the new
rule and to keep pinning the season override, which did not change.

Tests: 442/442.

**Recreated players get the abilities their rating earns.** Requested after
the ability research, and the research is what shaped it — the two families are
stored so differently that only one of them can be chosen at all.

**Physical abilities cannot be chosen, and the tool does not pretend
otherwise.** `PhysicalAbility1..5` are typed `AbilitiesRank` — the same type as
the mental *ranks* — and hold only None/Bronze/Silver/Gold/Platinum. Nothing on
the player names the ability. That mapping is game data:
`PositionSignatureAbility[]`, `PositionAbilityTable[]` and
`PositionalAbilitySplines[]` are each one row of references, none of which
resolves to a table inside the save. It is position- and archetype-dependent —
600 of the 696 single-ability `DT_PurePower` players use slot 4, every
single-ability `KP_Power` uses slot 3 — so slot 4 on a nose tackle is not slot 4
on a kicker. What the tool sets is **how many** of a player's slots are filled,
**which** of them, and **at what tier**; the archetype it already chooses does
the rest.

**Measured, not authored.** `tools/measure_ability_model.py` reads
`data/AbilityModel.json` out of a base save: the share of players with an
ability rises from 3.6% at OVR 50–54 to 99.1% at 90–94, tiers go from
Bronze-heavy at the bottom to 52% Platinum at 95+, and each archetype has its
own slot order. Six tests assert the tool reproduces that share to within 4
points at six overalls — the model is checked against the game, not against
itself.

**Mental abilities are the opposite and are treated so.** They name the ability
outright, and they are rare and elite: 248 of 11,730 players carry any, 244 of
those carry all three, essentially none below OVR 80. A player is only ever
given one the game has been **observed** giving their position. An earlier cut
tried to classify abilities as "position-locked" or "general" by counting
positions; that over-fit — `FieldGeneral` (QB) and `OLRally` (the line) really
are locked, but `Headstrong` looked locked to four positions purely because
only 32 players carry it. No rule is inferred from a 22-player sample; the pool
is the observation.

**A defect the roster diff caught, not the tests.** Abilities were applied to
converted players but not to the slots the filler re-rates as depth, so a
63-overall walk-on kept two Silvers from the player before him — the exact
thing the feature exists to prevent, and invisible without diffing the output
against the save it came from. Filled slots are now decided too, and a test
fails if any fringe player's slots match the donor's.

Nothing new is asked of the user: the tiers come from the overall the rating
engine already produced and the archetype already selected, so awards, draft
slot and production reach abilities through the rating they earned. With
`--ratings inherit` there is no generated overall, so nothing is written rather
than invented.

Tests: 431/431.

**You can play the season you recreated.** Reported as the last gap before a
release: a recreated 1985 roster was still played in whatever year the save
started in, and that is the one thing about a historical recreation a user
cannot edit around afterwards.

**Found, then confirmed in the game.** The year lives in `SeasonInfo`, a
one-row table: `CurrentSeasonYear` and `BaseCalendarYear`, the anchor the
dynasty counts forward from (`CurrentYear` is the offset into it, 0 in a fresh
save). A probe save built from a real dynasty with both set to 2023 — and
nothing else changed — was loaded by the user and **displays 2023**. All eight
base saves carry `CurrentYear: 0`, so which field the UI reads could not be
told apart from data; both are written, so they agree either way.

The edit is **141 bytes of a 30,005,935-byte database**: two `SeasonInfo`
fields plus the current-season row each team keeps in
`TeamHistoricSeriesYear`, which would otherwise disagree with the rest of the
dynasty. Re-extracting the written save returns Player, Team and
CharacterVisuals byte-identical to the original's.

**Every other year-bearing field was checked and deliberately left alone.**
Sweeping all 2,272 tables returns 18 fields wide enough to hold a calendar
year. `Team.YearStartOfFootballProgram` and `Stadium.STADIUM_CALENDAR_YEARBUILT`
are historical facts; `Rivalry.FirstYearPlayed` is named like a year but holds
23, 24, 25, 30; `DraftClassInfo.DraftClassYear` is 0–30, relative, and follows
the anchor by itself. The interesting one is `PlayerStatRecord.calendarYear` —
4,023 live rows that turn out to be the **record book**, real dated
achievements like Philip Rivers' 2003 passing yards. Those belong to the years
they happened in whatever year the dynasty is set to.

**Opt-in, not automatic.** `--dynasty-year <year>|roster` on the command line,
and in the app a checkbox that names the year — "Play it in 2023" — appearing
only once there is both a save to write into and a season to write. Recreating
an old roster inside a present-day dynasty is a perfectly reasonable thing to
want, and rewinding somebody's calendar uninvited is not this tool's call.
Asking for a year on a CSV-only run is reported rather than dropped, because
the year lives in a table the export tool does not write.

**A trap worth recording**: `madden-franchise` does not enforce its own schema.
Setting `CurrentSeasonYear` to 5000, or to −1, is accepted in silence and
writes a number the game cannot read. The bound is ours — checked in
`NativeSave.Apply` before the save is opened, and again in `apply.mjs` so the
sidecar is safe standalone. The floor is 1869, the first college football game
ever played.

Tests: 413/413, and the end-to-end path verified against a real save: the 2023
Florida State roster and the year land together, 5,524 fields written, and the
source save's hash is unchanged.

**All-time rosters wear their own decades.** An all-time roster is one team
holding fifty years of players, and `Season` was read once per file — whichever
year happened to be typed first. The All-Time USC file that reported this took
1980, so Reggie Bush played in a Riddell TK. `HistoricalPlayer` now carries its
own optional season, `HistoricalCsv` reads it per row, and `EquipmentApplier`
gained an overload keyed on roster slot rather than team index, so the era is
chosen per player.

Verified on a real all-time Florida State file spanning 1988–2015: Deion
Sanders in a Riddell TK with a vintage mask, Charlie Ward in a VSR-4, Jalen
Ramsey in a Revolution Speed — three eras in one run, and the report names the
span rather than a single year. The roster keeps one season for the things that
genuinely need one: the report heading, the FBS membership check, and the depth
slots filled in for the user, which belong to no year of their own. An explicit
`--season` still overrides every row, because "treat this file as 1999" has to
mean all of it.

One defect the real run caught that the tests had not: `EquipmentReport.Merge`
sums its parts' team counts, which is right when the parts are disjoint schools
in a whole-season run and wrong when they are seasons of the same school — it
reported one team as seven. Merging now takes the real count.

**A second pass on the archetype rules, measured rather than re-argued.**
`tools/measure_archetype_usage.py` asks a base save what the game itself does,
and it refutes more than the two calls that prompted the review.

- **Twelve position defaults were archetypes the game barely uses.**
  `C_WellRounded` is on 0 of 403 centres, `G_WellRounded` on 1 of 944 guards,
  `TE_Possession` on 8 of 756 tight ends, `KP_Accurate` on 5% of punters. The
  default is what a player with no usable evidence gets, which on a researched
  historical roster is most of the squad — so most of a recreated team was
  being put in an archetype that does not occur, with nothing in the game to
  say so. Each default is now the archetype the game most often uses at that
  position, which is the maximum-likelihood answer and needs no threshold to
  argue about.
- **The offensive-line weight rules were noise and are gone.** The file said
  "at most 295 lb means pass protector". The save says `OT_PassProtector`'s
  median weight is **309 lb — above `OT_Agile`'s 305**, and P(a pass protector
  is lighter than another tackle) is **0.476**, i.e. very slightly the wrong
  way. The rule caught 13 of 138 real pass protectors while mislabelling 86
  other tackles; at RT its precision (7%) was *below* the base rate (14%).
  Same for the centre and guard variants and for `DT_NoseTackle` (0.494). The
  Munoz case was the visible symptom: a 278 lb tackle is a normal tackle in
  1979 and weight cannot tell finesse from anything.
- **The power-blocker rules are kept**, because the same measurement supports
  them: separation 0.68–0.71 and precision above the base rate at every OL
  position. Retuning was not the answer either way — one direction is evidence
  and the other is not.
- **The Groza kicker was classified correctly for the wrong reason.**
  KP_Power is 74% of the game's kickers and 18 of its top 20, so an
  award-winning kicker belongs there; what was wrong was reaching it through
  "a 52-yard field goal" while the default was the rarer archetype. KP_Power
  is now the default, and `KP_Accurate` is selected by what it actually means
  in the game — accuracy above leg strength (KickAccuracy 79 vs KickPower 72,
  against +11 the other way for KP_Power) — so it takes both a good percentage
  and no long.
- **Stat thresholds were left alone and the reason is recorded**: the game's
  own players carry no season statistics, so those rules cannot be checked
  this way. The review says what it measured and what it did not.

On the 2023 Florida State roster: **20 of 85 players changed archetype, every
overall identical, 1,017 attributes reshaped.** Fidelity is unmoved at 2.12,
which is the expected result rather than a null one — the engine calibrates
attributes to a target overall, so an archetype change moves shape, not
strength.

**The app says when a school was not yet in the FBS.** The command line's
`validate` has reported this since Milestone 13; the desktop app never asked
the question at all, which is the wrong way round, because the app is where
the team and season are actually chosen. A note now sits under the team and
season boxes and follows both as they change — a season is something a user
edits after the roster has already been checked. `RosterCsvValidator.Check`
also gets the membership file from the app now, so the finding appears in the
output pane like every other one. Advisory, never a gate.

A detail the tests caught: the note uses the problem's full `Reason`, not
`Detail`. `Detail` strips the leading school name for callers that print the
school in a column of their own, and this note is a lone sentence with nothing
else naming the team — it read "did not field an FBS team until 2026."

Tests: 399/399.

**The announcers now say the right name (v0.6.2).** Reported as an oversight:
the Player table's `PLYR_COMMENT` selects which recorded name the commentary
uses, and a recreated player kept whatever the slot already had — so a
generated Jordan Travis was called by the replaced player's name for the whole
dynasty, with nothing in the game to reveal it.

Each player's surname now sets the index, with **0** for a surname the
commentary has no recording of. No new column and no user input: it comes from
the `LastName` already supplied.

**Measured, not invented.** `data/CommentaryIds.json` holds 5,918 surnames,
built by `tools/build_commentary_ids.py` from **146,295 player rows across nine
game-generated saves**. Hand-edited saves are deliberately excluded — a roster
editor can leave `PLYR_COMMENT` pointing at a slot's previous occupant, and
pooling one such save was visibly poisoning the mapping (All-Time USC names —
Bush, Palmer, Allen, Leinart — each disagreeing with all nine base saves).
Across the base saves only 2 surnames of 9,070 are ambiguous.

**The game itself confirms the rule.** Renaming two players in-game and
re-exporting shows the game rewriting `PLYR_COMMENT` to exactly the values this
mapping gives for the new surnames, 2 of 2 — including 0 for a surname with no
recording. That also resolves an old note in `Schema.md` calling this field
"changed spontaneously on one observed rename with no clear trigger": the
trigger was the rename.

**A lock became a check.** `OpaqueFieldGuard` forbade any write to
`PLYR_COMMENT` because the field was not understood, and it correctly blocked
this work. It is replaced by `CommentaryConsistencyRule`, which permits the
write but rejects an index belonging to a different name — the precise defect
being fixed. With no mapping file present the converter does not touch the
field at all, since "we know nothing" is not the same as "the name cannot be
said".

Verified on a real save: 85 of 85 Florida State players carry the index for
their own surname. Tests: 368/368.

**Bug: opening a dynasty save looked like a hang.** Reported against v0.6.0
alongside the above, and the more serious of the two. Reading a save unpacks
30 MB of bit-packed tables and writes the ones the generator needs back out —
fifteen seconds on a fast local disk, longer from a OneDrive-redirected
Documents folder. Nothing said so. The command line printed its first line only
*after* that finished, so a working program looked like a dead one; the desktop
app was worse, because `LoadDynasty` ran inline and froze the entire window for
the duration.

The load now runs off the UI thread, announces itself before it starts, and
locks the dynasty pickers while it works so a second choice cannot race the
first. `RosterGenerationService` gained an optional `Progress` callback, which
the CLI sends to the console and the app marshals back to the UI thread.

The first attempt at this deadlocked: a `LoadDynasty()` shim kept for the tests
did `GetAwaiter().GetResult()` on the UI thread, and the continuation needed
the very thread the blocking call was holding. The suite went from 5 seconds to
a timeout, which is exactly what it is for. The shim is gone and the tests pump
the dispatcher while awaiting instead. Tests: 357/357.

**Bug: a greyed-out Generate button that would not say why.** Reported against
the v0.6.0 build — the status line read "Ready — 75 players, nothing to fix"
and Generate stayed dead. The status was telling the truth about the roster and
nothing at all about the dynasty: `CheckAsync` reads only the roster file, and
its message overwrote whatever the dynasty had failed with. Whatever had gone
wrong in step 1 was erased by step 2, leaving a reassuring sentence above a
button that would not work.

Two changes. The window now carries a **persistent line naming what is
missing** — the step that has not been done, or the dynasty's own error
message, kept in a field so a later roster check cannot overwrite it. And the
roster check no longer says "Ready", which was a claim about the whole run it
had no way to make; it says "Roster is fine", which is all it actually knows.

Four headless tests pin it, including the exact sequence reported: fail the
dynasty, then choose a roster, and the explanation must survive. Setting them
up exposed that Avalonia's `SetupWithoutStarting` is process-global, so the GUI
tests now share one UI thread (`HeadlessGui`) instead of each starting their
own. Tests: 357/357.

**Milestone 14 (Native dynasty saves) is complete.**

- **A dynasty goes in as a save and comes back as a save.**
  `generate --dynasty DYNASTY-BASE1 --roster 2023_FSU.csv --save-out DYNASTY-2023FSU`
  is the whole workflow. No PocketScout export, no third-party importer — the
  two worst steps of the user's process are gone. Confirmed by the user: the
  written save loads in the game with the edit intact.
- **Nothing between the two ends had to change.** `extract.mjs` writes CSVs
  that are **byte-identical to PocketScout's own export** — verified on the
  Player, Team and CharacterVisuals tables — so the entire pipeline from
  Milestone 3 onward reads a save without knowing one was involved, and the
  2023 FSU regression test pins the same bytes either way.
- **Only differing cells are written.** The real 2023 FSU roster into a real
  save wrote **5,461 fields**, left **243 empty roster slots untouched**,
  changed **85 rows on team 27 and 0 rows anywhere else**, and produced a
  Player table matching the generated CSV exactly. The empty-record rule
  matters: a save pre-allocates slots holding no player, and writing the
  export's blanks back into them would be writing a blank name over a slot the
  game expects to find in a particular state.
- **The save that came in is never modified.** The output is always a new
  file, writing over the source is refused, and the originals' hashes were
  checked unchanged after every run.
- **The format work is borrowed, not rebuilt.** `madden-franchise` (MIT) ships
  the C27 schema and the zstd dictionaries. A C# reimplementation would mean
  owning a bit-packer and a 3,498-entry schema table, re-verified against every
  game patch, in exchange for nothing a user can see. `NativeSave` is the whole
  boundary: two process calls and a magic-byte check.
- **Nothing to install.** The release bundles the Node runtime itself
  (v22.23.1 LTS, MIT, checksum-verified against nodejs.org at build time)
  alongside the vendored library — 68 MB to 122 MB zipped — so the user installs nothing
  and runs no package manager. The bundled copy is private to the app and
  cannot be broken by another Node version on the machine. `NativeSave`
  prefers it and falls back to PATH for source checkouts. Without either, the
  tool names what is missing and the CSV workflow is untouched.
- **The desktop app does this too.** A "Save file…" browse button opening at
  the game's saves folder, a "write a new dynasty save" option that appears
  only when the input is a save, and the runtime problem reported inline
  rather than at Generate. Its step-1 copy used to say "This tool does not
  read your save file", which the smoke test pinned — both are now inverted.
- **Two leaks closed on the way.** `OpenDynasty` returned an export while
  dropping the package that owned its scratch folder, so every archive or save
  selection left a copy of the dynasty's tables in the temp folder; it now
  returns the package and callers dispose it. And `generate` opened the
  dynasty eagerly for a team prompt it usually never showed, extracting a save
  twice per run.
- **The guard that matters.** The schema is pinned at `C27_468_2`; a mismatch
  refuses to write rather than guessing, because a field written at the wrong
  offset corrupts a dynasty silently.
- Tests: 353/353. The end-to-end save test runs when `CFB27_TEST_SAVE` points
  at a real save; a green suite says nothing about that path unless it was set.

**Milestone 13 (A whole season at a time) is complete.**

- **The tool now writes the blank file, not the user.** `template --season
  2010` produces one row per roster slot for every team that played that year,
  with `Team`, `Season` and `Position` filled in. Against a real base save:
  **119 teams × 85 = 10,115 rows**. By hand that means knowing which schools
  existed that year and typing 10,000 rows before a single player is
  researched.
- **The oversight it exists to close.** CFB27 ships the **138 teams of
  today**, so a 2010 season assembled from that list silently includes schools
  that were still in the FCS, and nothing in the save says so.
  `data/FbsMembership.json` records when each of 31 schools reached the FBS
  (plus UAB's 2015–16 gap), and the 2010 run correctly left out **19**:
  Sacramento State, NDSU, Delaware, Missouri State, Kennesaw St., Sam Houston,
  Jax State, James Madison, Liberty, Coastal Carolina, Charlotte, App St., Ga
  Southern, Old Dominion, Georgia State, UMass, Texas State, UTSA and South
  Alabama. (2010's FBS had 120 teams; the one CFB27 cannot supply is Idaho,
  which left after 2017.)
- **Advisory, never a gate.** `validate` reports the same thing as a note on a
  filled file, and generation proceeds regardless. The dates are this project's
  reading of the record in a plain JSON file the user can correct — refusing to
  build somebody's roster over a date this project got wrong would be the worse
  failure.
- **One roster file can now carry any number of teams.** Each team's 85 slots
  are disjoint, so they all convert into the single output table the user
  imports once. Verified end to end on the full 2010 file: **10,115 players
  across 119 teams in 21 seconds, 0 errors**, and a diff against the source
  table shows exactly 10,115 changed rows, in exactly 119 teams, of exactly 85
  — the other 21 team indexes, recruit pool included, untouched.
- **The reporting was wrong before it was right.** The first working
  multi-team run converted all three teams but reported only the first, and
  rehelmeted only the first. The result now carries every conversion, the
  tallies are over all of them, and equipment is applied per season across
  every converted team.
- **The 85-slot layout is measured, not invented.** `data/RosterSkeleton.json`
  is the league mean across a base save's 138 teams, apportioned to exactly 85
  by largest remainder: 9 WR, 8 CB, 6 each DT/HB/TE, 4 each
  FS/LE/LT/MLB/QB/RE/ROLB/RT/SS, 3 each C/LG/RG, 2 K, 2 P, 1 LOLB. A starting
  shape, not a rule.
- Tests: 335/335.

**Follow-up: the height column is inches, and its name says so.** Filling the
template with a spreadsheet assistant failed consistently on `Height`, and the
cause was not the tool: Excel decides `6-2` is the 2nd of June the moment it
opens the file and writes back `2-Jun` or the serial behind it, so the height
was destroyed before the generator ever saw it. The column is now
**`HeightInches`** — a bare number is the only thing that survives a
spreadsheet, and the column name is the instruction. Feet-inches is still read
and converted (refusing a value the tool understands would cost the user data
to make a point) but reported as a correction; a date or a date serial is
named as such rather than reported as an implausible height, so the user looks
at the right thing. `Height` keeps reading the same cell for good, so files
already filled in under the old name are unaffected. Tests: 343/343.

**Research: native dynasty saves can be read and written.** Five real save
files (not exports) were measured against the PocketScout CSV exports of the
same saves. A CFB27 save is an extensionless `FBCHUNKS` file, 9,646,981 bytes
packed and ~30 MB unpacked, zstd-compressed with a trained dictionary; it is
EA's franchise database, schema `C27_468_2`, handled by the MIT-licensed
`madden-franchise` library. Four things were established, all against real
data: it **opens** (all five, correct schema and table count, no override);
the **read is exact** (4,584,474 field comparisons over 16,257 live players
against PocketScout's own CSV, zero mismatches, on two different saves); the
**round trip is lossless** (unpack → repack on all five gives a byte-identical
30 MB database — the packed file differs because zstd will not reproduce EA's
stream, so packed-byte equality is the wrong test); and a **single edit stays
single** (one jersey number: 1 byte changed in 30 MB, 1 cell in the Player
table). The unknown that matters is whether the game loads a repacked save,
which needs the game and cannot be tested here. Details and the harness are in
`tools/native-save/`. **Since confirmed by the user — the game loads it — and
built out as Milestone 14 above.**

**Milestone 12 (One file in, one file out; and appearance) is complete.**

- **A dynasty goes in as one file and comes back as one file.** `--dynasty`
  takes a `.zip` wherever it took a folder, and `--package` writes the whole
  dynasty back out as a single archive. The property that matters is not that
  it round-trips — it is that **everything the tool did not generate comes back
  byte for byte**. Verified on five fresh saves: 2,271 of 2,273 files identical
  (2,273 of 2,275 for the two with a custom coach), the two that moved equal to
  the generated tables, and the result re-opens as a dynasty.
- **Five fresh saves settled what varies between dynasties.** Two independently
  created saves with the same team, coach and roster are **byte-identical on
  all 139 real teams**; the only differing rows are the 4,100 in `TeamIndex
  255`, the randomly generated recruit pool. The coach touches **no** player
  row. The live roster is not byte-stable between downloads, but only in
  flavour fields — hometown, ability bitfield, pipeline, 35 jersey numbers —
  and never a name, position, class, height, weight or rating.
- **A custom coach renumbers the database.** 245 of 1,299 shared tables shift
  `_tableIndex`, including the real `Team` table (2225 → 2227). `Player` and
  `CharacterVisuals` happen to stay put, but there are **nine tables named
  `Team`** in every save, so discovery by content rather than by number is what
  keeps this working — and is why a canonical Player table can never be shipped.
- **Skin tone is decoded, and it rides along with the face.** The
  `CharacterVisuals` blob carries a bare `skinTone` (1 lightest, 8 darkest),
  and the sixth segment of a generated head's own name is the same value —
  3,144 agreements, zero disagreements. A given generated head is only ever
  used at one tone (1,607 heads, none at two), so choosing the face chooses the
  tone and the visuals table never has to be written.
- **Faces now keep the slot's tone, and an optional `SkinTone` column
  overrides it.** On the 2014 Florida State roster: 6 of 6 requested tones
  honoured exactly, and **79 of 79 other players kept their tone through the
  face swap, none moved**. Out-of-range values are refused and reported rather
  than clamped.
- **The tone is supplied, never inferred.** The generator will not guess what a
  real person looked like from their name, hometown or position. A blank cell
  means "keep what the roster slot had".
- Tests: 317/317.

**Milestone 11 (Attributes that match the archetype) is complete.**

- **The defect, reported twice from opposite sides.** A user's Marcus Allen —
  a back who caught 34 passes, correctly classified `HB_PowerReceiving` — came
  out with **30 in all three route-running attributes**. Another user's
  Marqise Lee, a receiver, came out with **34 juke and 30 trucking**. Same
  bug: the archetype was chosen correctly and then ignored. The attribute
  shape was assembled from hand-written position baselines that named only a
  subset of the 56 attributes, and everything they omitted fell to a global
  default of 30.
- **The fix is measured, not authored.** `tools/build_archetype_profiles.py`
  reads a real dynasty export and fits `value = intercept + slope × overall`
  for **all 59 archetypes × all 56 attributes** across 16,256 players, and
  records the residual spread too. `data/ArchetypeProfiles.json` is that
  measurement; a generated player now starts from what the game itself gives
  their archetype at their overall. The seed is self-consistent: fed back
  through EA's own formula it returns the overall it was built for to within
  0.3 points for 56 of the 59 archetypes.
- **Production now moves the attributes it was earned with.** Each role a
  player produced in (passing, rushing, receiving, pass rush, run stop,
  coverage, kicking, punting) raises that role's attributes by a number of
  standard deviations **of the spread the game itself shows** for that
  archetype. Nothing invents a magnitude. It only ever raises — a 1968
  receiver must not be marked down for numbers nobody kept — because shaping
  downward is the archetype's job.
- **A second role now counts toward the overall.** `HB` asks how well someone
  ran; a back who caught 37 passes answered a question it never asked and used
  to tie with a back who caught none. Secondary-role production adds a bounded
  bonus to the target.
- **Sanity caps yield to measurements.** Several hand-written position caps
  would have held an archetype below where every player of it in the game
  actually sits. The cap is widened to admit the measured value and goes on
  bounding everything else.
- **The guardrail is general, not two special cases.** `ArchetypeFloorTests`
  asserts that no generated player sits below the floor the game's own players
  of that archetype occupy, in any attribute that archetype's overall formula
  weights heavily. It fails on the old engine with **48 breaches** across the
  Florida State fixture and passes on the new one.
- **Roster strength is untouched; only shape moved.** Regenerating the 2014
  Florida State roster old-vs-new: **all 85 overalls identical**, mean
  attribute movement 8.7 points, and 147 attributes moved 30+ points into the
  range the game actually uses.
- Tests: 295/295, with the whole suite now running the engine configured the
  way the shipped application configures it.
- **Verified on both reporters' own files before release.** The 2014 Florida
  State roster was rebuilt from public sources and reviewed by hand; the other
  user's All-Time USC template was run unmodified. On USC: all 85 overalls
  identical again, mean attribute movement 9.35, and 197 attributes moved 30+
  points — Marcus Allen's medium route 30 → 86, Reggie Bush's 30 → 94, Marqise
  Lee's juke 34 → 89. The secondary-role bonus fires on Bush and says so in
  the report.
- Shipped together with Milestone 10 as
  [v0.4.0-alpha](https://github.com/elrey-430/cfb27-historical-rosters/releases/tag/v0.4.0-alpha).

**Milestone 10 (Faces) is complete — the first tier. Shipped in v0.4.0-alpha.**

- **The defect.** A replaced player inherited the roster slot's head, and
  **9,011 of 16,257** players in a base save wear a `Unique_` scan of a real
  person — 71 of the 85 slots on a typical team. So most of a recreated 1985
  roster wore the recognisable faces of present-day players, under other
  people's names. On the Florida State fixture that was 71 slots; it is now 7,
  and those 7 are leftover slots still carrying their own player's identity.
- **The fix.** Those slots get a generated face **drawn from the user's own
  export** — never an invented asset name, the same rule the equipment layer
  follows — with `PLYR_PORTRAIT` written to match and `PLYR_ASSETNAME` cleared.
  Selection is seeded from the player's row key, so a roster regenerates
  identically. `--faces inherit` restores the old behaviour.
- **Deliberately narrow.** Slots that already carried a generated face are not
  churned, and slots no historical player took over keep their own likeness.
  Every substitution is listed in the report.
- **Not attempted: matching a historical player to a real scan.** The scans
  are present-day players, so the overlap with any historical season is
  almost nil — and inferring what a real person looked like from their name is
  not something this tool should do. A user who knows the right head can name
  it; that is their call, not an inference.
- Tests: 289/289. The 2023 Florida State golden fixture was regenerated; the
  only columns that moved are the three head columns.

**Milestone 9 (Period-correct equipment) is complete.**

- **Where equipment lives.** Not in the Player table. A controlled experiment
  — one dynasty exported twice, differing only in eight Florida State
  cornerbacks' helmets — changed exactly **one file out of 2,273**:
  `0130_CharacterVisuals.csv`. Full decode in `docs/Schema.md`, Group 6.
- **The link.** The Player table's `CharacterVisuals` column is a packed
  32-bit reference: low 16 bits are the visuals row, high 16 a constant
  `8452` table tag. Decoding it for all 16,500 players recovered exactly the
  eight edited cornerbacks and nothing else.
- **The edit.** Helmet (`slotType: HeadWear`) and face mask
  (`slotType: FaceMask`) are replaced by targeted string substitution inside
  the JSON blob — both patterns occur exactly once per row across all 12,156
  rows carrying a helmet — so every other byte survives. The two are always
  written together, because a mask is moulded to a shell.
- **The user surface.** The season already being recreated picks the era. No
  new column, no new question. A second file, `Generated_Equipment.csv`, is
  written and must be imported alongside the roster.
- **The acceptance test.** Our output is **byte-identical** to what the
  community editor produced for the three rows it edited without also
  filling in unrelated Head-loadout defaults. That is a stronger bar than any
  previous milestone had.
- **The catalogue cannot be mined.** Retro helmets appear on *zero* of 12,586
  players in a base save, so every period asset had to be demonstrated in the
  editor and read out of a diff. Two rounds, 25 player edits, covered it. A
  season no era covers still changes nothing.
- **Brand carries over, the model changes.** A player's current helmet names
  a manufacturer and the era moves them to that manufacturer's model, so a
  squad stays mixed rather than collapsing into 85 identical helmets. Brands
  that did not exist yet (Vicis, Light) take the era's fallback. Six of the
  eight demonstrated edits follow this exactly; the two that do not
  (Howard, Schutt → Riddell; Lester, Light → the 2000s Revolution) are
  recorded in `docs/Schema.md` as open questions rather than fitted to.
- **Masks follow position.** Mined from the base save: the game puts a kicker
  cage on 92–98% of kickers and punters, a cage or heavy bar on linemen, an
  open two-bar on quarterbacks. The engine now selects by role, with a
  deterministic pool for spreading masks across a line. The demonstration then
  showed something finer than the mined data did: offensive and defensive
  linemen differ, so a centre gets `revofullcage` where an edge rusher gets
  `RevoRobot`.
- **Sleeves and shoulder pads** are era-wide slots alongside the helmet:
  tight/small in the 2010s, loose/medium in the 2000s, long from the 1990s
  back with large then X-large pads. `SleeveStandard` turned out to be what
  the editor calls "loose".
- **A second key-order trap, caught before it shipped.** The exporter writes
  `itemAssetName` and `slotType` in *either* order — 12,570 rows one way, 16
  the other — so any pattern spanning both keys would have missed almost
  every row. All slots are matched on the value's own prefix instead.
- **Five eras live**: 2010–2016, 2000–2009, 1990–1999, 1980–1989 and
  pre-1980, every asset in them read out of a demonstration export. A second
  round of 17 player edits supplied the retro vocabulary that could not be
  mined: the VSR-4 (`GearHelmet_standardBrady`), the TK
  (`GearHelmet_RiddellTK`), the Schutt Air Advantage (`GearHelmet_Schutt`,
  distinct from `GearHelmet_AirXP`), four vintage masks, per-role Revolution
  and Revolution Speed masks, long sleeves and X-Large pads.
- **The 2000s split by model** on the research: the Revolution arrived in 2002
  and was on 83% of NFL players by 2008, while the VSR-4 it replaced stayed in
  college use through 2010 — so a SpeedFlex wearer takes a Revolution and an
  Axiom wearer a VSR-4.
- **Asset names are not UI labels**, which is a trap worth remembering: the
  VSR-4 is `standardBrady`, and the shell the editor calls "Schutt Air XP" is
  the real-world Air Advantage and a *different asset* from the Air XP Pro
  VTD. A test pins every name in the data file to the demonstrated set so a
  typo cannot reach a user as a broken helmet model.
- Tests: 272/272.

**Milestone 8 (Draft slot measures the wrong season) is complete.**

- **The defect.** Draft position is the heaviest signal in the model and the
  only backward-looking one: it records where the NFL took a player months
  later, which is a different question from how they played in the season
  being recreated. An injury, a position the league does not value or a bad
  combine all move it without moving anything that happened on the field.
- **The fix.** When a draft slot sits more than 6 points below the
  contemporaneous evidence (awards, production), its weight drops to 35% and
  the report says why. It is not discarded — a late pick is still
  information — it just stops outvoting the season itself. The rule is
  narrow: it fired on 3 of 75 Florida State players.
- **`AwardContender`.** A new optional column for awards a player was in
  contention for without winning, scored from the same vocabulary 5 points
  lower. A Heisman finalist therefore out-rates a first-team all-conference
  winner, which is the right ordering. It is often the only evidence left
  when a season ends early.
- **A data error the work exposed.** Jordan Travis *won* the 2023 ACC
  Player of the Year and Offensive Player of the Year, and finished fifth in
  Heisman voting; the dataset had him at first-team all-conference, two
  tiers low. Corrected. He now generates at **88, up from 83**, led by the
  award rather than his draft slot.
- **Fidelity: 2.07** (was 2.01), still inside the 3.00 bar and better than
  the human recreation's 3.02. The metric measures agreement with the shape
  of EA's *generic* Florida State roster, and 2023 was an unusually
  top-heavy team — ten NFL draft picks — so rating its best player higher
  moves away from the generic curve on purpose.
- Tests: 229/229. Shipped as
  [v0.2.0-alpha](https://github.com/elrey-430/cfb27-historical-rosters/releases)
  from the distribution repository, which builds and tests on Windows before
  packaging.

**Milestone 7 (Ship it) is complete.** The tool is no longer something only
this repository can run.

- **A desktop app.** `RosterGenerator.Gui.exe` — pick the dynasty folder, pick
  the roster CSV, confirm team and season, click Generate. The roster file is
  checked the moment it is chosen, so problems appear before anything is
  written. Built with Avalonia; WinForms and WPF cannot build on this Linux
  SDK, so choosing them would have meant shipping code that was never compiled
  or run here.
- **A `validate` command.** Checks a roster CSV on its own and writes nothing.
  Held to the standard that makes a validator worth having: over a corpus of
  fifteen real mistakes, "ready to generate" must mean generation succeeds,
  a blocking verdict must mean it fails, every player generation skips and
  every value it rejects must have been flagged first, and a clean file must
  produce no warnings at all. Building those tests found two defects in the
  validator itself.
- **One pipeline, two front-ends.** `RosterGenerationService` holds every
  decision that shapes a roster; the command line and the desktop app only ask
  questions and display answers. They cannot grow different behaviour.
- **A release.** `./build-release.sh` produces two self-contained
  `win-x64` executables (~67 MB and ~74 MB) with `data/` and `templates/`
  beside them and a quick start, zipped and ready to hand to someone. This is
  what the Milestone 1 brief asked for and no build had ever produced.
- Tests: 221/221, including the window building and showing under Avalonia's
  headless platform. The 2023 Florida State deliverable still regenerates
  byte-identically after the pipeline was extracted.

**Milestone 6 (Roster completion and fidelity benchmark) is complete.** The
generator now produces a whole believable roster rather than a believable
set of individuals, and there is a measured number saying so.

- **The 85th player problem is solved.** A CFB27 team always carries 85
  players; a researchable historical roster is the two-deep plus whoever
  else is documented. The leftover slots kept EA's fictional players, three
  of whom out-rated the historical Florida State roster. `RosterDepth.json`
  measures what end-of-roster depth actually looks like — the median overall
  at each of the 85 roster ranks and the class mix at each depth, across 138
  untouched FBS rosters — and the filler gives an unfilled slot what the game
  itself puts at that rank, held below the weakest historical player at the
  position. Names, jersey numbers and portraits are untouched.
- **`PLYR_PREVTEAMID` decoded.** It is a school's `TEAM_ORIGID`, not a team
  index — which is why its values (1009–1235) never matched the team range.
  133 of 135 distinct non-zero values resolve to a team in the same save.
  `PreviousSchool` is now written.
- **Two rating defects found by benchmarking and fixed.** The game's best
  punter is an 86 and its best receiver a 99, but both drew on one award
  scale, so a nation-leading All-American punter generated at 91 — better
  than any punter in the game. And role, awards and statistics record what a
  player did, never *where*, so an anonymous backup was rated identically at
  a playoff program and at the worst team in the country.
- **Fidelity score: 2.01.** Mean absolute deviation from the roster shape
  the game itself ships for Florida State, rank by rank — down from 4.48,
  and inside the 3.02 scored by the hand-built human recreation. Pinned by
  `RosterFidelityTests`. See `Ratings/Benchmark_2023_FSU.md`.
- Tests: 136/136.

**Milestone 5 (Archetypes, hometowns, roster size) is complete.** Two more
fields were investigated and cleared for writing, closing the largest
remaining realism gaps.

- **`PlayerType` (archetype) — writable, with a companion dependency.** A
  manually edited save proved the write takes, but also exposed the trap:
  the archetype selects which of EA's overall formulas applies, and the
  community editor does not recompute the overall afterwards. Only **56%**
  of that save's 85 edited players have an overall matching their own
  archetype (35 match a *different* one) against **99.3%** in an untouched
  base save. This tool selects an archetype from each player's profile
  (`data/ArchetypeRules.json`) and always recomputes with the new formula;
  the `ArchetypeConsistency` rule reports the defect in any file with it.
  The same save also carries an LOLB with `MLB_PassCoverage` — an archetype
  invalid for the position — which the rule now catches too.
- **`PLYR_HOME_TOWN` / `PLYR_HOME_STATE` — writable.** Town is free text;
  state is a strict **51-value enum** (50 states in PascalCase plus
  `NonUS`). The `Hometown` column now writes both, accepting `FL`,
  `Florida` or `West Virginia`, and mapping anything non-US to `NonUS`
  with a note.
- **Roster size** is reported more usefully: the warning now says how many
  leftover original players rate 75+ and could out-rank the historical
  roster on the depth chart.
- Tests: 118/118. The FSU regression fixture was deliberately regenerated
  once (the diff was confined to the two hometown columns and nothing else).

**Milestone 4 (Automated ratings & attribute generation) is complete.** A
user can supply a name, position, height, weight and whatever historical
performance data they have, and receive a complete CFB27 player — all 56
attributes plus an overall — with no manual editing.

- **The overall rating is EA's own formula, not an invention.**
  `data/OverallFormulas.json` holds 79 formulas covering all 21 positions
  and all 59 archetypes. Verified independently against a full dynasty
  export: **99.33% exact** (16,148/16,257 players), 99.90% within one
  point. Because the formula is linear, the engine solves it *backwards* to
  hit an intended overall exactly — so the overall written always agrees
  with the attributes written.
- **Transparent evidence model** (`data/RatingModels.json`) — draft slot
  (0.34), awards (0.26), production (0.22), recruiting stars (0.10),
  depth-chart role (0.08), each expressed directly on the overall scale.
- **Confidence + reasons** on every player (High/Medium/Low with the
  signals that fired), surfaced in `Generation_Report.txt`.
- **Verified measurements win and stay put:** a timed 40 sets speed exactly
  (4.30→99, 4.40→96, 4.50→92) and calibration may never move it.
- **Guardrails:** OL speed capped at 72, K/P tackling at 45, class-year
  awareness caps (a Heisman-winning redshirt freshman still tops out at 78
  awareness), low-confidence freshman overall cap, and a depth-chart rule
  keeping backups below starters unless a draft slot or major award
  justifies it.
- Deliverables: `Ratings/Rating_Model.md`, `Ratings/Position_Formulas.md`,
  `Ratings/Default_Assumptions.md`, `Ratings/Player_Test_Results.csv`, and
  `Tests/Ratings_test.csv` (standalone 2015 Dalvin Cook case).
- Tests: 86/86 passing. The Milestone 3 FSU regression remains byte-stable
  (it pins `--ratings inherit`, testing the conversion layer).

**Milestone 3 (Generalized historical roster pipeline) is complete.** The
generator is now a general-purpose end-user tool: it works with any
compatible dynasty export, takes a simple spreadsheet-style roster CSV, and
needs no programming, schema, or database-id knowledge from the user.

- **No dynasty-specific dependency.** `DynastyExport.Open` discovers the
  Player table and the main Team table by content (`_tableName`) in the
  user's own export, and derives the available teams and their ids from
  that save. `data/TeamMappings.json` is now only an optional alias
  overlay, filtered to teams that actually exist in the loaded dynasty.
- **Simple user-facing input.** `FirstName,LastName,Position,Number,
  Height,Weight,Class,Team,Season` (+ optional `Hometown`,
  `PreviousSchool`, `Notes`); case-insensitive headers, any column order,
  heights as `6-2` or `74`, classes as `RS Junior`; only the first three
  fields are required. Template: `templates/HistoricalRosterTemplate.csv`.
- **Team/season selection.** `list-teams` enumerates the dynasty's teams;
  `generate` takes `--team`/`--season`, reads them from the CSV, or prompts
  interactively with a numbered list.
- **Standard output.** `Output/Generated_Roster.csv` +
  `Output/Generation_Report.txt` (plain text: processed/mapped counts,
  missing fields, defaults used, warnings).
- **Regression-protected.** The 2023 FSU recreation is now an automated
  byte-stability test over committed fixtures (`Tests/2023_FSU_Input.csv`,
  `Tests/DonorDynasty/`, `Tests/2023_FSU_Expected_Output.csv`).
- Tests: 65/65 passing. Standalone `win-x64` single-file publish verified,
  shipping `data/` and `templates/` alongside the executable.

**Milestone 2 (Historical Roster Pipeline & 2023 Florida State recreation)
is complete.** The pipeline independently generated a complete 2023 FSU
roster from a public-information dataset and exported it as a
CFB27-compatible full `Player.csv`:

- `HistoricalData/2023/FloridaState.json` — 75-player dataset compiled from
  public sources (seminoles.com, Tomahawk Nation, ESPN, SI/247Sports), not
  from any dynasty export
- `Output/2023_Florida_State_CFB27.csv` — the deliverable: the base save's
  player table with FSU's roster replaced (75 rows changed, all on team 27,
  only the seven confirmed-safe columns touched; byte-verified)
- `Output/2023_Florida_State_Report.md` — the generated validation report
  (counts, missing fields, defaults, assumptions, warnings)
- `dotnet run --project src/RosterGenerator.Cli -- generate|compare ...` —
  repeatable for any team/season given a dataset and the mapping files
- Tests: 50/50 passing

**Milestone 1 (Foundation & Proof of Concept) is complete and verified.**
The library can reliably load a CFB27 dynasty save `Player.csv` export,
represent its 16,500 rows × 286 columns internally, apply controlled edits,
validate the result (including the confirmed multi-field dependencies), and
export a CSV compatible with the existing roster import tool.

The decisive verification ran against a real save export
(`DYNASTY-JUL24-BASE`): a rename + jersey-number edit to one player
produced an output file that a byte-level `diff` showed differing from the
input in **exactly one line, in exactly the `FirstName`, `LastName` and
`JerseyNum` cells** — with the identity-asset fields correctly retaining
their original values, matching observed in-game rename behavior.

The project now lives standalone in this repository (extracted from the
`cfb27-aio-app` monorepo, history preserved). Historical roster generation
has **not** started; that is by design (see Non-goals in the Milestone 1
brief).

- Solution: `RosterGenerator.sln` — .NET 8, zero external dependencies in
  the core library
- Tests: 25/25 passing (`dotnet test`)
- Distribution path verified: `dotnet publish -r win-x64 --self-contained
  -p:PublishSingleFile=true` produces a single ~67 MB `.exe` that runs on a
  clean Windows 10/11 machine with no runtime installed

## Completed features

### Milestone 13 — a whole season at a time

- **`SeasonTemplateWriter`** (`Core/Historical/`) — writes the blank
  whole-season roster CSV. The header is copied from the shipped template
  rather than restated, so the blank file and the documented format cannot
  drift apart.
- **`FbsMembership`** (`data/FbsMembership.json`) — per-school first FBS
  season plus skipped-season ranges. `Check` returns a problem or null;
  `EligibleIn` filters a season's teams. Advisory everywhere it is consulted.
- **`data/RosterSkeleton.json`** — the measured league-mean position layout of
  a team's 85 slots, apportioned by largest remainder.
- **Multi-team reading and conversion** — `HistoricalCsv` groups rows by team
  and exposes `Rosters` / `IsMultiTeam`; `RosterGenerationService.Run` converts
  every team into one session and one output table, and reports a team the
  dynasty does not carry rather than failing the other 130.
- **`EquipmentApplier.Apply(…, IReadOnlyCollection<int> teamIndexes, …)`** and
  `EquipmentReport.Merge` — a season's teams are rehelmeted per season in one
  pass over the roster, and the summary counts all of them.
- **`CsvDocument.FromRows`** — files this project writes are quoted and
  escaped by exactly the code that reads one back.
- **CLI `template`**, and `validate` extended to check every team in a season
  file and to note a school that had not reached the FBS.

### Milestone 7 — shipping

- **`RosterGenerationService`** (`Core/Pipeline/`) — the whole pipeline in one
  place: open the dynasty, read the roster, convert, validate, export, write
  the report. Both front-ends call it, so a fix reaches both and neither can
  quietly grow its own behaviour.
- **`RosterCsvValidator`** — a pre-flight check producing Blocking / Warning /
  Note findings. Runs the same reader, position mappings, bounds and role
  vocabulary generation uses rather than reimplementing them.
- **`RosterGenerator.Gui`** — Avalonia desktop app, window built in C# so the
  build stays a plain compile. Validates on file selection, preselects the
  team and season it finds, disables the options that cannot work, and runs
  generation off the UI thread.
- **CLI `validate`**, exiting non-zero only on blocking findings.
- **`build-release.sh`** — self-contained `win-x64` publish of both
  executables with `data/`, `templates/` and `QUICKSTART.md`, zipped.
- **`GuiSmokeTests`** — builds and shows the real window under Avalonia's
  headless platform, and asserts Generate is disabled until both files are
  chosen.


### Milestone 6 — roster completion and benchmarking

- **`RosterDepthModel`** (`data/RosterDepth.json`) — median overall by roster
  rank and class mix by depth band, measured across 138 base-save rosters.
  Also carries the league median (69) used to size the program adjustment.
- **`RosterFiller`** — turns unfilled slots into end-of-roster depth off the
  measured curve, ceilinged by the weakest historical player at the position
  and floored at 45. Fully deterministic (largest-remainder class quotas, no
  sampling) so byte-stable output is preserved.
- **Program adjustment** — the donor roster's median against the league
  median, shifted onto thinly evidenced players and fading to nothing as
  evidence strengthens (full at Low confidence, half at Medium, none at
  High). Needs no new input: the dynasty already encodes the tier.
- **`positionOverallCaps`** — each position group capped at the highest
  overall the game itself carries there. The observed maximum, not a
  haircut: a first-team All-American kicker still generates at 89 of 90.
- **`SetPreviousSchool`** + `DynastyExport.BuildPreviousSchoolMappings` —
  writes `PLYR_PREVTEAMID` as the school's `TEAM_ORIGID`, translating the
  shared alias overlay into that id space so "Mississippi State" finds the
  save's "Mississippi St".
- **`RosterFidelityTests`** — asserts the generated roster's shape stays
  within 3.00 mean absolute deviation of the game's own.
- **2023 Florida State dataset enriched** — all ten 2024 draft selections
  with overall pick numbers, the undrafted signing, All-ACC and All-America
  honours, season statistics, and a depth-chart role for all 75 players.
- **CLI** `--fill fill|leave`.

### Milestone 5 — archetypes and hometowns

- **`ArchetypeSelector`** (`data/ArchetypeRules.json`) — per-position rules
  keyed to the real archetype names (LT/RT use `OT_*`, LG/RG `G_*`, …),
  with thresholds derived from each archetype's attribute medians in a real
  export. First matching rule wins; a condition whose field is missing never
  matches, so a player with no data falls through to the position default.
- **`Hometown`** parser — "City, ST" → free-text town plus the state enum.
- **`ArchetypeConsistencyRule`** — flags archetypes invalid for the position
  and archetype changes made without recomputing the overall. Keyed to
  `PlayerType` so an edit is an error while a defect already present in a
  loaded file stays a warning.
- **CLI** `--archetypes select|inherit`; selecting requires
  `--ratings generate`, because the two must move together.

### Milestone 4 — rating generation

- **EA overall formulas** (`Rating/OverallFormulaSet.cs`) — loads the 79
  supplied formulas, computes overall with EA's half-down rounding, and
  inverts them in closed form to calibrate attributes to a target overall.
- **Evidence model** (`Historical/RatingEvidence.cs`) — role, star rating,
  combine numbers, draft slot, awards and 26 statistics, all optional and
  all additive to the golden-standard template CSV.
- **Talent scorer** (`Rating/TalentScorer.cs`) — weighted blend of the
  available signals with per-signal explanations; partial stat lines scale
  their own weight down; derived stats (completion %, YPC, FG%) computed
  automatically.
- **Rating engine** (`Rating/RatingEngine.cs`) — position baselines →
  talent sensitivity → physique (reference sizes derived from real save
  medians) → verified measurements (locked) → experience shift →
  sensitivity-weighted calibration → sanity caps.
- **Depth consistency** (`Rating/DepthConsistency.cs`) — roster-level pass
  holding backups below starters, with a narrow justification exception.
- **CLI** `--ratings generate|inherit` (generate is the default) and a
  ratings section in the generation report.

### Milestone 3 — generalized end-user pipeline

- **Dynasty import** (`Dynasty/DynastyExport.cs`) — opens an export folder
  (searched recursively) or a lone Player CSV; identifies the Player table
  by its required columns and the main Team table by row count (the export
  contains several decoy single-row Team tables); exposes the discovered
  teams and builds the school-name lookup from them.
- **Simple historical CSV reader** (`Historical/HistoricalCsv.cs`) —
  tolerant header matching, feet-inches or plain-inch heights, per-row
  user-facing warnings instead of hard failures, caller-supplied
  team/season overriding file columns.
- **Roster template** (`templates/HistoricalRosterTemplate.csv`).
- **Plain-text generation report** (`ConversionReport.ToText`) alongside
  the existing Markdown renderer.
- **CLI workflow** (`RosterGenerator.Cli`) — `generate` (with interactive
  team/season selection when not supplied), `list-teams`, `compare`;
  defaults to `Output/Generated_Roster.csv` +
  `Output/Generation_Report.txt`; friendly single-line error messages.
- **2023 FSU regression test** (`FsuRegressionTests`) — regenerates from
  the committed simple-CSV input and donor dynasty and asserts the output
  is byte-identical to the committed expected file.

### Milestone 2 — historical pipeline

- **Historical data model** (`Historical/`) — platform-independent
  `HistoricalPlayer`/`HistoricalRoster` JSON model (season, school, name,
  position, jersey, height, weight, class year + optional hometown,
  previous school, notes); every non-identity field may be missing.
- **Team mapping system** (`data/TeamMappings.json`) — external
  alias→TeamIndex file generated from the save's own Team table (138 teams);
  no team ids are hard-coded; lookups are case/punctuation-insensitive.
- **Position mapping system** (`data/PositionMappings.json`) — external
  alias→CFB27-position file (Tailback→HB, Cornerback→CB, ...) plus
  interchangeability groups (LT/LG/C/RG/RT, LE/RE, LOLB/MLB/ROLB, FS/SS).
- **Historical→CFB27 converter** (`Conversion/`) — replaces one team's
  players inside a donor save via replace-identity edits: writes the
  confirmed-safe fields, inherits donor values for anything missing or
  unresolved (ratings, Weight, identity assets), assigns slots
  position-first with group fallback, and records every default and
  assumption in a Markdown `ConversionReport`.
- **Class-year parser** — "Redshirt Junior"/"RS Fr"/"Graduate" →
  `SchoolYear` + `RedshirtStatus` pairs.
- **Comparison utility** (`Comparison/RosterComparer.cs`) — field-by-field
  team comparison between two Player tables (name-matched, configurable
  column set, Markdown report) ready for the generated-vs-manual FSU
  benchmark.
- **CLI** (`RosterGenerator.Cli`) — `generate` and `compare` commands.

### Milestone 1 — foundation

- **Byte-preserving CSV layer** (`Csv/`) — an unedited file round-trips
  byte-identically; exports can only differ in cells an edit deliberately
  changed. This is the compatibility guarantee for the community import
  tool.
- **Typed player model** (`Model/`) — `PlayerRoster`/`Player` views over
  the raw table, with a load-time snapshot enabling per-cell change
  tracking; unknown columns pass through untouched.
- **Intent-recording edit operations** (`Editing/`) —
  `RenamePlayer` (cosmetic; assets untouched), `ReplacePlayerIdentity`
  (different real person; caller must supply new asset values),
  `TransferPlayer` (applies all five confirmed companion updates
  atomically: `TeamIndex`, `PrevTeamIndex`, `PLYR_PREVTEAMID`, both NIL
  fields zeroed), plus attribute setters for jersey/height/class/redshirt/
  position/ratings.
- **Validation layer** (`Validation/`) — eight named rules:
  `RequiredFields`, `DuplicateRowKey`, `RatingRange` (0–99 over the 57
  numeric rating columns), `EnumFields` (position/class/redshirt/jersey),
  `TeamAssignment` (range + optional membership in the save's team list),
  **`TeamChangeConsistency`** (the Group 4 multi-field dependency),
  **`IdentityChangeConsistency`** (rename-vs-replace intent enforcement),
  and `OpaqueFieldGuard` (blocked writes to `PLYR_COMMENT`; it also locked
  `Weight` until the encoding was confirmed — now range-checked by
  `WeightRange` instead). Both locks have since been retired by the
  measurement that resolved the field they guarded: `OpaqueFieldGuard` was
  replaced in v0.6.2 by `CommentaryConsistencyRule`, which permits the
  commentary write and rejects an index belonging to a different name.
  Anomalies already present in the source file downgrade to warnings so
  genuine EA exports — which contain blank-name placeholder rows — always
  remain exportable; the same anomaly introduced by an edit is an error.
- **Validating exporter** (`Export/`) — refuses to write when validation
  fails (with the full report in the exception) and returns a per-row list
  of changed columns as proof of what the edit touched.
- **Proof-of-concept console app** (`RosterGenerator.Poc`) — runs the full
  load → rename+jersey → validate → export → independent-diff pipeline
  against any `Player.csv`.
- **Documentation** — `docs/Architecture.md` (structure, data flow, design
  rationale) and `docs/Schema.md` (column-level ground truth: five
  empirically confirmed field groups plus a profiled appendix of all 286
  columns with assumptions explicitly labeled).

## Known unknowns

Tracked open research items — all are locked or out of scope in code, not
guessed at (details in `docs/Schema.md`):

1. ~~**`Weight` encoding (Group 2).**~~ **RESOLVED (2026-07):** stored
   value = pounds − 160 (range 160–400 lb), confirmed by correlating the
   manually edited 2023 FSU save against real listed weights and by
   league-wide decoded position averages. The spline hypothesis is
   refuted. Writable via `Player.WeightPounds`; the converter now writes
   real weights. See Schema.md Group 2 for the evidence.
2. **Derived league-wide `Player[]` array tables (Group 5).** ~200 array
   tables reshuffled across dozens of teams from a two-player team change —
   evidence of game-recomputed sorted/indexed lists. Never loaded or
   written by this tool; treat any future need as a full-table recompute
   problem, not diff-and-patch.
3. ~~**`PLYR_COMMENT` semantics.**~~ **RESOLVED (v0.6.2):** it is an index
   into the recorded commentary audio — which name the announcers say — and
   `0` means the name is never said, held by 20.3% of an untouched save. The
   surname determines it: `data/CommentaryIds.json` maps 5,918 surnames,
   measured across 146,295 player rows in nine game-generated saves. The
   "changed spontaneously on one observed rename" note is explained by that —
   the trigger was the rename, and the game rewrote the index to the new
   surname's value, 2 of 2. Now written by the converter and checked by
   `CommentaryConsistencyRule`. See Schema.md Group 3.
4. ~~**`PLYR_PREVTEAMID` native domain.**~~ **RESOLVED (Milestone 6):** it
   holds the school a transfer came from, as that school's `TEAM_ORIGID` —
   not a `TeamIndex`, which is why the values (1009–1235) never matched the
   team range. 133 of the 135 distinct non-zero values in a base save are a
   `TEAM_ORIGID` in that save, and Florida State's 20 non-zero players
   resolve to real schools. `1009` means a school the dynasty does not
   model. `PrevTeamIndex` reads `255` for every player in an untouched save
   including those transfers, so the two fields do **not** track the same
   thing; only `PLYR_PREVTEAMID` is written. See Schema.md Group 4.
5. **~250 unconfirmed columns.** Statistically profiled (type/range/enum
   values) in the Schema.md appendix but never verified by a controlled
   edit. None are written by the tool.
6. **Asset regeneration rules.** ~~Unknown.~~ **WORKED AROUND (Milestone 10):**
   the generation algorithm behind `GenericHeadAssetName` / `PLYR_PORTRAIT` is
   still not decoded, and the segments inside a generated head's name are not
   understood — but nothing needs to synthesize one. A replaced player is given
   a head that already exists in the user's own export, `PLYR_PORTRAIT` written
   to match and `PLYR_ASSETNAME` cleared. Reassignment, never authoring. What
   remains unknown is only *which* generated head suits a given player, which
   is cosmetic.

## Next recommended milestone

**Milestone 15 — Roster CSV round-trip.**

The generator can read a roster file; it cannot write one. That asymmetry is
now the biggest thing standing between a user and a good result — and a
season's worth of teams has just made it larger, because a user who dislikes
one player in 10,115 still has no way to correct them and regenerate.

Every rating defect reported so far arrived the same way: a user generated a
roster, looked at one player, and disagreed. The answer to that is not another
rating rule — it is letting them fix it and regenerate without retyping 85
lines. Exporting a team's current roster *as a roster CSV* turns "tweak one
player" from an afternoon into two minutes, and it turns a blank template into
a filled starting point, which is the single most common complaint about the
input step.

It also removes the standing awkwardness that the only way to correct a
generated player is in the third-party editor, where the correction is invisible
to this tool and lost on the next run.

### Also worth doing

- **FBS membership only records arrivals.** `data/FbsMembership.json` covers
  the 31 schools that joined since 1978 and knows nothing about schools that
  *left*: Idaho played the 2010 season and dropped to the FCS after 2017, so
  CFB27 does not carry it and a 2010 template writes 119 teams where the real
  FBS had 120. The tool cannot supply a team the save does not have, but it
  should say so rather than leave the user to count. Seasons before 1978 are
  also outside what the file describes.
- **Sign the executables.** SmartScreen still warns on every download.
- **Opening a save is still slow.** Fifteen to thirty seconds, and v0.6.1 only
  made the wait honest rather than shorter. Most of it is `extract.mjs` writing
  every column of the Player and CharacterVisuals tables back out as CSV when
  the generator reads a fraction of them; a narrower extract is the obvious
  first thing to measure.
- **Two ambiguous surnames.** "Butts" and "David" carry two different
  commentary indexes across the base saves, and the more common one wins. The
  tie-break is a count, not evidence about which recording the game intends —
  worth resolving by a controlled in-game rename rather than more sampling.

### Deliberately *not* next

- **Position-cap and program-adjustment interaction.** On an all-time roster a
  receiver with Medium-confidence evidence can clear a Heisman-winning back,
  because the WR cap is 99 where HB is 96 and the program adjustment applies in
  full at Medium confidence. Both parts are measured and defensible on their
  own. Judged not worth changing: it is visible only on all-time rosters, and a
  user who disagrees can edit it.
- **Tier 2 faces (decoding the head segments).** Tier 1 stopped recreated
  players wearing real people's faces, which was the actual harm. Choosing
  *which* generated head is cosmetic.
- **Create-A-Face import.** Confirmed Road To Glory exclusive; the conversion
  path through a Dynasty save is unverified.
- **Jersey numbers.** About 25 remain unverified in the FSU dataset and the
  two rosters disagree on roughly 40%. This needs sources, not engineering.

Explicitly still deferred: automatic historical data gathering, dynasty
editing, and the derived-array recompute.
