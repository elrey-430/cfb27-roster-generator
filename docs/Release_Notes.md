# Release notes

## Unreleased — the memory-card save, end to end

`export-legacy` and `import` both take a PS2 **memory-card save** (`.psu`) now,
not only the bare roster file, and give back the same kind they were given. The
database editor drops out of the loop at both ends: a save goes in, a save comes
out, and it goes straight back on the card.

This cost much less than expected. A `.psu` is a plain archive — a 512-byte
entry header per file, contents padded to 1024 bytes, no compression and no
encryption anywhere in it — so the EA roster database sits inside one exactly as
it sits on disk. The file extracted from a save and the same file inside it are
byte for byte identical, so there was nothing to reverse-engineer, only a
container to read.

Every entry's header is carried across verbatim and the only field that ever
changes is the length of the file whose contents changed, so the icon,
`icon.sys` and anything else sharing the save come through untouched — verified
on a real 2005 roster inside a three-file save, where only the roster differs
and what sits inside the save equals the loose `.db` exactly. Timestamps are
left alone on purpose: the card browser shows them and they are the user's
record of their own save.

Confirmed against a real memory-card save, not just one built to the spec: a
2025 community roster exported by a save manager, carrying the roster alongside
`view.ico` and `icon.sys`. It opens, round-trips byte-identical, and a
whole-league write changes the roster and nothing else — every other entry's
contents and all 512 bytes of every header come through untouched. That file
also settled a design question: it names the save directory and the roster
inside it **identically** (`BASLUS-20991R2025`), so finding the roster by
matching its name would have had to guess between the two. It is found by being
an EA database instead.

A bare roster file in still means a bare roster file out. Making a save out of
one would mean inventing its directory, icon and timestamps, which is authoring
rather than editing. The reverse is free, so `--db-out` — and a checkbox on the
export tab — writes the roster on its own as well, for checking the result in an
editor first.


## v0.9.4-alpha — Your CFB27 teams, back on the PS2

The import has always gone one way. `export-legacy` goes the other: a CFB27
dynasty written into a PS2-era NCAA Football roster file, over the squads
already in it, to be played on the console the file came from. In the app it is
a new **Export to PS2** tab, which shares the dynasty with the generator and
asks for nothing else — no roster CSV, no season, none of the four options that
govern how players are rated.

Name a school with `--team`, or leave it off and every school both games have is
written in one pass: **118 teams, 6,756 written and 3,274 cut, which is 10,030
slots exactly — 118 × 85**. One read, one save. The file you point at is opened
read-only and writing over it is refused.

**The depth chart decides who comes.** A PS2 squad holds about 69 players
against CFB27's 85, so a sixth of every roster has to go, and overall is the
obvious answer and the wrong one — a coach who starts a 78 over an 81 has said
something. The dynasty's own chart is read and honoured, position by position.
Only slots named after one of the game's 21 positions count: a chart also
carries jobs rather than positions — kick returner, third-down back, long
snapper — and keeping those would have put a receiver at the head of the running
backs. Players the chart says nothing about, it being three deep at quarterback
where a squad carries five, queue behind the ones it names. A dynasty carrying
no chart falls back to overall and the report says so.

Nobody changes position on the way across, so a slot your team has nobody for
keeps the player it had and is named. Ratings go through the five-bit scale
measured for this release, so one can move by half a step: an 84 stays an 84 and
a 77 becomes a 76. A surname past 13 characters is truncated and named —
`Kaahaaina-Torres` goes in as `Kaahaaina-Tor` — rather than discovered in the
game.

**The bug worth naming.** `--team all` first wrote 14,557 players into a
10,030-slot league. CFB27 has no Idaho, so `TeamMappings.json` points it at a
generic FCS squad to be *generated into* — and every one of those shares team
index 255 with the game's 4,527 recruits. Reading *out* of 255 handed the file
the entire recruiting pool under Idaho's name. Guarded now in all three places
the index is resolved, with the school skipped and told why.

Also fixed, and it has been wrong since v0.9.0: **character code 53 is a hyphen,
not a full stop**. Every hyphenated surname imported from a PS2 roster since
then came through as `El.Amin`.

## v0.9.3-alpha — NCAA 14's rosters bring their ratings

The PS3-era save holds forty-two of CFB27's fifty-seven rating columns on the
same 0-99 scale, so unlike the PS2 files the numbers themselves cross over, not
just the order. Read against a real 2013 community roster: 8,631 players across
126 teams, no warnings, Clowney a 99 and Bridgewater a 97.

Those forty-two are copied and then left alone — no cap, class shift or solver
moves them. The fifteen NCAA 14 never had are filled from what CFB27 gives that
kind of player at that level. Its single throw accuracy and single route
running are split three ways, shaped by the archetype and centred so they still
average what was written. The archetype itself comes out of the ratings, scored
in each attribute's own measured scatter: 20 recoveries out of 20 on the game's
own average quarterbacks.

**The correction that took longest to find.** Carrying the ratings verbatim was
wrong, and only a real roster showed it. CFB27's formula returned an overall 6.8
points below what NCAA 14 stated at outside linebacker and 2.5 points above it
at cornerback — a 9.6-point spread tracking nothing but how much of each
position's formula the older game covered. That would have made corners the best
players on every imported team. Every carried rating now moves by the same
amount, solved so the game's formula returns the stated overall; one shift
rather than one per attribute, so every difference between them — the player's
shape — survives untouched. Position bias 0.0, within-position rank correlation
1.000 where it was 0.80-0.99.

Also: `import` reached the CLI help text, which it had never been in; and both
front ends stopped telling every user "Ratings are NOT imported" after an import
that had just imported them.

## v0.9.2-alpha — Schools the game no longer carries

Of the 119 teams on a 2004 I-A roster exactly one is missing from CFB27: Idaho.
It had nowhere to go, so the whole roster was refused. The game ships five
generic FCS teams with real 85-man rosters, and a departed school is now written
onto one of them — Idaho onto FCS East, recorded in `TeamMappings.json` as an
alias plus a `standInTeam`.

**The redirect cannot go by `TeamIndex`.** All five FCS teams carry index 255,
and so do the 4,527 players in the recruiting pool, so asking the player table
for "team 255" hands back the lot. `Player` has no other team column either:
`PrevTeamIndex` is 255 for all of them and 3,875 of the 4,527 are freshmen.

The teams know. Every team row, FBS and FCS alike, has a `Roster` reference into
one shared table whose rows hold exactly 85 player references — the same 32-bit
encoding and player tag the depth chart uses.

```
FCS East  Team.Roster -> row 33  -> 85 refs, player rows 6373, 5875, 11651
USC       Team.Roster -> row 129 -> 85 refs, player rows 2, 12223, 298
```

`TeamRosterTable` follows it. Verified on a real dynasty: generating 2004 Idaho
changed 85 player rows, all 85 of them FCS East's own and none outside it.

Two things had to give way:

- The dynasty's team list drops rows carrying the no-team sentinel, which is why
  the FCS teams never appear in it. An overlay entry naming a stand-in is
  admitted anyway — it resolves by team name, never by index, so it cannot
  conjure a team the dynasty lacks; if the named team really is absent the
  conversion says so and writes nothing.
- Anything that re-asks `TeamIndex` after a conversion has the same problem.
  Equipment did, and put 2004 helmets on all 4,527 players in the pool.
  Conversions now record the slots they claimed and equipment follows those,
  which is identical for an ordinary team and correct for this one.

Five FCS teams means five departed schools per dynasty before they overwrite
each other.

Tests: 573, five new.

## v0.9.0-alpha — PS2-era rosters are a source of players

`import` reads a roster file from the PS2-era NCAA Football games. Community
"named" rosters carry real people with real numbers and measurables across more
than a hundred teams, which is exactly the part of building a historical roster
that is pure typing. **Import old roster** in the app, which adopts the file it
writes so the user can go straight to Check.

The container is EA's `DB` format and it is fully decoded — **660,445 of 660,445
cells** against community exports of two different files. Three things had to be
worked out that the format never states:

- It stores no row count anywhere. The record area is pre-allocated and the tail
  left blank, so the last row with a non-zero key ends the table. Exact on all
  six tables across both files: 8893/7350/119 and 4471/3995/83.
- Nine columns carry stale end offsets pointing at another column's bits — four
  of them player id, height, weight and team id. Recovered by searching every
  bit position for the one that reproduces an export; the same corrections then
  read a second unrelated file, which makes them a property of the format.
- `PFSH`/`PMSH`/`PSSH` are signed in two's complement, with nothing marking them.

**Which team a player plays for is not recorded.** Squads run in blocks of
consecutive player id and `TDYN` names two captains per side, which suffices
until two blocks touch — then there is no gap to cut on and a boundary in the
wrong place moves a dozen players to the wrong school. The depth chart settles
it: within a pass a team lists each `(position, depth)` slot at most once, so the
boundary that makes the fewest players collide with somebody already in their
slot is the one the game is describing. Every boundary in both files resolves
with no collisions, and every team holds its own captains — 119/119 and 83/83.

**Ratings are deliberately not imported.** Eighteen of CFB27's fifty-seven rating
columns have any counterpart in the older games, and those eighteen carry a mean
of 54.3% of the weight in EA's own overall formulas — 41.9% at quarterback.
Writing them across would leave nearly half of every overall to be invented and
then presented as history, and the stored numbers are five or six bits wide on a
scale nobody has anchored.

What crosses over is the order. `LegacyRank` becomes a talent signal (weight
0.20, below draft, awards and production) scored through a curve measured over
EA's own rosters — 138 squads of exactly 85, 11,730 players. The eighteen
`Legacy*` columns hold where a player stood among others at his own position;
each nudges its matching attribute by up to six points and is offered to
archetype selection as a rule field. That is what stops two backs of the same
standing coming out identical:

```
HB Reggie Bush    OVR 84  HB_ElusiveBack  spd 95  str 67
HB LenDale White  OVR 83  HB_PowerBack    spd 78  str 88
```

A verified measurement still wins outright. And the legacy signal only joins the
weight total when there is one to weigh, so a hand-written roster keeps exactly
the confidence it always had rather than being diluted by an import nobody made.

`data/LegacyTeamIds.json` maps 119 team ids to schools, 118 of them naming a
player from that team's own roster. The ids are not reliably alphabetical — USC
and Utah sit next to each other, and both Louisiana schools appear under names
they had stopped using.

Tests: 568, twelve new. The fixture is built rather than committed: a real
roster file is somebody's editing work and not ours to redistribute, and a
written one states the awkward parts of the format outright.

## v0.8.4-alpha — A dynasty writes out as a roster file

The tool could read a roster file and not write one — top of the backlog since
Milestone 15. `RosterCsvExporter` writes a dynasty out in the template's own
shape and column order: `export` on the command line, **Export roster file** in
the app. Omit the team and it writes every team the dynasty carries, which the
generator reads straight back because the roster file's `Team` column decides
where each player goes.

Identity is lossless. Florida State exported out of a base save and generated
straight back in, compared **by player rather than by row** — a recreated player
takes whichever donor slot fits his position, so what has to survive is the man,
not his seat — is 85/85 on position, jersey, height, weight, class, redshirt,
town, state and previous school.

Two things the first attempt got wrong, both caught by measuring the round trip:

- Four players came back as never having transferred. `PLYR_PREVTEAMID` 1009 is
  a school the dynasty does not model — an id with no name — and blank read back
  as "never transferred", a different and untrue thing.
  `PlayerSchema.PreviousSchoolNotInDynasty` (`Unlisted`) is written on export and
  read back silently, without the warning an unknown school name would raise.
- Roles are read off the dynasty's own depth chart, which only became possible
  in v0.8.3. Heading a slot named for a real position makes a starter; the
  specialist slots describe a package, not a starting job, so leading 3DRB or KR
  does not promote a third receiver. An 85-man roster yields 21 starters.

The evidence columns — stats, awards, combine, draft — are deliberately empty. A
save records what a player *is*, never what he *did*; exporting cannot invent a
stat line, and pretending otherwise would put made-up numbers in somebody's file.
An exported roster reproduces identity exactly and rates from scratch.

Tests: 556, nine new.

## v0.8.3-alpha — Depth charts are rebuilt

A generated roster took the field in the donor's order. A depth chart points at
player rows, in the order the dynasty's original players ranked; the tool
replaced who lives in each row and left the chart alone, so the slot the game
believed was the starting quarterback held whoever landed there. The game does
not correct it — which is what proves it honours a stored chart rather than
re-sorting on load.

Three tables carry it: `Team.DepthChart` names a chart row, the chart's 35 slots
each name a `Player[]` row, and that row lists up to six players in order. The
player reference tag is 8496. Team row order is *not* team index — Florida State
is Team row 38 and team 27 — so the link is followed rather than assumed. Only
`Player[]` is rewritten, so a rebuild cannot break the structure.

Fifteen of the 35 slots are not positions, and the model for them is measured
(`tools/measure_depth_charts.py`): GAD is 59% HB and 40% WR, LS is 78% TE, SLCB
draws on CB, FS and SS; depth is 6 at WR, 5 at CB, 4 at HB, 3 nearly everywhere
else. Ordering within a slot is by overall descending — 2,634 of 2,731 slots.

The mirrored pairs are one assignment: LT/RT, LG/RG, LE/RE and LOLB/ROLB each
list both sides, the same player never heads both (0 of 143 teams, all four
pairs), and the better of the two is on the left 87–92% of the time. So the pool
is sorted once and dealt alternately, left first.

Verified end to end on a real save — 2023 Florida State read back out of the
written file:

```
before  QB  Daniels(77), Willow(76), Sperry(74)
after   QB  Travis(92), Glenn(77), Rodemaker(70)
after   LT  Byers(LT 84), Armella(RT 70)
after   RT  Scott(LT 73), Sapp(RT 65)
```

No user input. A dynasty carrying no depth chart is skipped in silence, and
`LockedEntries` — the entries a user pinned — is never rewritten. Extraction
gains two tables and about 13 seconds.

Twelve new tests, 547 total.

## v0.8.2-alpha — Read both draft columns

`DraftRound` has been in the template since Milestone 6 and was never read.
`DraftPick` was taken as an overall pick number always, and the round used only
when the pick was missing — so a player entered as *round 2, pick 1* was rated
as the first selection of the entire draft, coming out in the high nineties
instead of the low nineties.

Both spellings now work, and `DraftSlot` tells them apart by arithmetic rather
than by a setting:

| Written | Read as |
|---|---|
| round 2, pick 1 | 33rd overall |
| pick 33, no round | 33rd overall |
| round 2, pick 45 | 45th overall — the 13th pick of round two |
| round 7, pick 20 | 212th overall |
| round 2, no pick | the middle of round two |

A pick larger than a round holds cannot be a position inside one, so it is an
overall number; below that a round makes the pick a position within it. Round
one needs no decision because the readings agree there.

A flat contradiction (round 2, pick 200) is reported and the pick believed, as
the more specific of the two. A pick one round long is not reported —
compensatory selections push real rounds past 32, so round 7 pick 240 is
ordinary.

The reading is always stated in the player's reasons, "Drafted #33 overall
(round 2, pick 1)", because silently reinterpreting a user's number would be
worse than the bug.

Twelve new tests, 535 total.

## v0.8.1-alpha — Cap by role first, class second

The Low-confidence overall cap read class year alone, conflating "young" with
"unknown". Measured across 11,730 players on 138 teams, role dominates and
class barely registers below the starting eleven — the 90th percentile by role
and class:

| | Freshman | Sophomore | Junior | Senior |
|---|---|---|---|---|
| Starter | 82 | 84 | 87 | 87 |
| Backup | 78 | 77 | 77 | 77 |
| Reserve | 73 | 73 | 73 | 73 |
| Walk-on | 68 | 68 | 68 | 67 |

One number per class (68/74/78/82) was wrong in both directions at once: a
freshman backup held ten points under where the game puts one, a senior reserve
let nine points over. `lowConfidenceCapByRole` replaces it and falls back to the
per-class value when a file names no role.

| | Biggest pile | Distinct overalls | MAD vs EA |
|---|---|---|---|
| Before the role work | 25 | 20 | 2.69 |
| v0.8.0 (role curve + spread) | 15 | 27 | 2.74 |
| **v0.8.1 (plus this cap)** | **8** | **32** | **2.68** |
| EA's own roster | 9 | 25 | — |

Better on every measure at once, including the curve fit the previous two
changes had each cost a little. The pile of fifteen freshmen on 68 was this cap,
not the spread.

Still unlike the game at the bottom: 28 players under 70 against EA's 16, and 25
in the 70–74 band against 32.

## v0.8.0-alpha — Recreated players get a body, and a roster gets a curve

Ratings move for every user in this release. Seven commits, two themes.

### Body build (new)

Every generated player gets a `CharacterBodyType` from position, height and
weight, with no new input. **`Freshman` is the stored name for the build the
game's editor calls Lean** — read out of a save in which five named Florida
State players were each given a different build in-game. The
`CharacterVisuals` blob also carries a `bodyType` integer and it is *not* this
field.

EA's player builder gates the light builds; the base save's census decides the
positions whose build is not in question (ends and tackles Muscular at 81–97%,
interior line and defensive tackle Heavy at 76–90%). 82.5% agreement against an
86.8% ceiling. The Lean cutoff is 170 lb at 6'0" and below, then +5 lb per inch
— the floor is a project decision, the slope is the game's own.

Confirmed end to end: 26 builds changed on a real save, all on the target team,
including a 310 lb guard who had been Thin.

### The draft band

`draftScores` spans 99 at pick 1 to 85 at pick 256, and `signalFloors.draft` is
0, so a pick floors at exactly what it implies. `draftedOverallFloor` (85) is
the backstop under it. `undraftedOverallCeiling` (85) caps an explicit `UDFA`
and never a blank column.

The award tolerance went 6 → 2 so a season can outrun a draft slot: a Heisman
winner is 96 whether taken 45th or 240th, against 92 and 85 for an ordinary
player at those picks. At 6 a Heisman floored at 92 — exactly what pick 45
implies — so the draft slot had quietly become the verdict on the season.

### Roster shape

`roleScores` are now the median the game carries at each role's roster ranks
(78/73/68/64, up from 76/69/64/61), which took the 75–79 band from 3 players to
18 against EA's 21.

`RoleSpread` lays players whose entire record is a role along the measured
percentile curve for that role, because the game spreads 14 points inside its
starters and 8–9 inside every other role, and class year explains one point of
it. Biggest stack 25 → 15, distinct ratings 20 → 27, against EA's 9 and 25.
`Generate` gained `overallOverride` for it.

MAD against EA's own Florida State curve: 2.29 → 2.74, bar 3.00.

### IsNIL

Corrected: it marks a real, NIL-signed person and the game gates editing on it
— not a compensation field. Every generated player is written with it off, so a
recreated roster is editable in-game. The NIL money fields are separate and are
left alone.

## v0.7.3-alpha — Writing a save actually writes a save

**"Write a new dynasty save" has never worked in a released build.** Turning it
on ended the run with a Node `ENOENT` and no save, whatever roster was being
generated.

The roster was written, correctly, to `Output\Generated_Roster.csv` beside the
executable — the shipped default, and a relative path. The sidecar runs with its
working directory set to `tools\native-save`, where its scripts and
`node_modules` live, so the same relative path named a file that has never
existed. Nothing to do with the size of the roster; a full FBS run is simply the
first time that step gets reached.

Every path is now resolved against the caller's directory in
`NativeSave.ApplyArguments` and `ExtractArguments` — the only two places a path
crosses that boundary.

The sidecar also stops answering with a stack trace. A table it cannot find is
named, with *"nothing was written and your save was not touched"*, and a save
that is not there says so rather than complaining about a missing FBCHUNKS
header.

### Why nothing caught it

All 442 tests wrote to a temporary directory — which is to say they all passed
absolute paths, so every one of them exercised the one case that worked. The
seven new tests pass relative paths on purpose; three fail against the old code,
including one that runs the real sidecar end to end.

## v0.7.2-alpha — The Team column decides the team

A correctness fix. **A roster covering more than one team only generated one of
them**, and said nothing about the rest.

The app sent its detected team on every run, and `HistoricalCsv` gave an
explicit team priority over each row's own — so a whole-season file was written
onto whichever school appeared first. 10,115 players, one team, silent.

Each player now goes to the team their own `Team` cell names. The caller's team
(`--team`, or the app's picker) is a fallback for rows that leave it blank.
`--season` is unchanged and is still a true override, because a season really
is roster-wide.

Verified on a filled 2010 season against a real base save with `--team`
deliberately set: **10,115 players across 119 teams, 85 each, zero misplaced.**

The app's picker now disables itself when the file names teams and says which
case applies, rather than leaving the user to infer it from a player count. It
remains for the one case it exists for: a file with no `Team` column.

**Re-run any multi-team roster made before this.** Single-team files are
unaffected.

### Why nothing caught it

An existing test asserted the old behaviour by name —
`SimpleCsvCallerTeamOverridesFileTeamColumn`. The bug was written down as
intent, so the suite stayed green while 118 teams were discarded. It now states
the new rule and still pins the season override.

## v0.7.1-alpha — Recreated players get abilities

A small update. A generated 92-overall edge rusher used to come out with
whatever ability tiers the player he replaced happened to have. The tiers now
follow the rating the player earned, from the overall the engine already
produces — so nothing new is asked of the user.

**It sets how good a player is in the slots they have; it does not choose which
abilities those are**, because the save does not store that.
`PhysicalAbility1..5` hold a tier only, and which ability a slot represents
lives in the game's own data keyed on position and archetype. See Schema.md
Group 8.

`data/AbilityModel.json` is measured from a base save: the share of players with
an ability rises from 3.6% at OVR 50–54 to 99.1% at 90–94, tiers run
Bronze-heavy at the bottom to 52% Platinum above 95, and each archetype fills
its own slots in the game's own order. Six tests hold the tool to those shares
within 4 points.

Mental abilities stay as rare as the game makes them — 248 of 11,730 players
carry any, 244 of those all three — and a player is only given one the game has
been observed giving their position.

### Also fixed

Slots filled as end-of-roster depth kept the previous player's abilities, so a
63-overall walk-on on the Florida State roster was holding two Silvers from his
predecessor. Every slot on the team is now decided fresh.

### Unchanged

Ratings, archetypes, equipment, faces, the commentary index and the season year
are identical to v0.7.0. `--ratings inherit` writes no abilities, because there
is no generated overall to read them from.

## v0.7.0-alpha — Play the season you recreated

Build a 1985 roster and the game says 1985. Until now a recreated roster was
played in whatever year the save started in, and it was the one thing about a
historical recreation that could not be fixed afterwards in a roster editor —
the year is not in the roster at all.

```
generate --dynasty DYNASTY-BASE1 --roster 1985_Roster.csv \
         --save-out DYNASTY-1985 --dynasty-year roster
```

The year lives in `SeasonInfo`, a one-row table inside the save:
`CurrentSeasonYear` and `BaseCalendarYear`, the anchor the dynasty counts
forward from. Both are written, because every base save has `CurrentYear: 0`
and so cannot distinguish which one the UI reads. Confirmed by loading the
game: a save with both set to 2023, and nothing else changed, displays 2023.

**Opt-in, and only the year moves.** Two fields plus the current-season row
each team keeps — 141 bytes of a 30 MB database — and the Player, Team and
CharacterVisuals tables re-extract byte-identical. The record book keeps its
real dates. It requires `--save-out`, because the year lives in a table the
export tool does not write; asking on a CSV-only run is reported rather than
dropped.

### Also in this release

- **All-time rosters wear their own decades.** `Season` is read per row, so
  each player gets their own year's equipment instead of the whole squad
  taking whichever year was typed first.
- **A second pass on the archetype rules, measured against the game.** Twelve
  position defaults were archetypes the game barely uses (`C_WellRounded` on 0
  of 403 centres); the offensive-line weight rules were shown to be worse than
  guessing and removed; the power-blocker rules were kept because the same
  measurement supports them. 20 of 85 players on the 2023 FSU roster changed
  archetype, with every overall identical.
- **The app warns about teams that were not in the FBS yet**, which the
  command line has done since v0.6.0 and the app never did.

### Known limitations

- The season year is verified on the display, not across a simulated dynasty.
- `--dynasty-year roster` on an all-time file takes the first year in it.

## v0.6.2-alpha — The announcers say the right name

An oversight, reported and fixed: your recreated players were being called by
somebody else's name.

The game stores a **commentary index** per player, choosing which recorded
name the announcers use. The tool never set it, so a recreated player kept
whatever the roster slot already had — your Jordan Travis was announced as the
player he replaced, every game, for the whole dynasty. Nothing in the game
tells you, which is why it went unnoticed.

**Now it follows the surname you already typed.** No new column, nothing to
fill in, no option to tick. The `LastName` in your roster CSV sets the index.

A surname the commentary has no recording of gets **0** — the announcers
simply do not say the name, rather than saying the wrong one. That is the
game's own value: a fifth of the players in an untouched save have it. On the
2023 Florida State roster, 61 of 85 players are named and 24 are not, and the
generation report gives you those counts.

### Measured, not guessed

The mapping covers **5,918 surnames**, read out of **146,295 player rows across
nine dynasty saves the game generated itself** — where the game assigned both
the name and the index.

Hand-edited saves were deliberately left out of that measurement. A roster
editor can leave the index pointing at a slot's previous occupant, and one such
save was visibly wrong: All-Time USC names — Bush, Palmer, Allen, Leinart —
each disagreed with all nine untouched saves. Including it would have taught
the tool names the announcers cannot actually say.

**The game agrees with the rule.** Renaming two players in-game and exporting
again shows the game rewriting the index itself, to exactly the values this
mapping gives for the new surnames — including 0 for a surname it has no audio
for.

### What this does not change

Ratings, equipment, faces, hometowns and every output file are identical to
v0.6.1. Existing roster CSVs work unchanged. Re-generating a roster you already
made will change the commentary index on those players, and nothing else.

If `data/CommentaryIds.json` is missing, the field is left exactly as it was
rather than zeroed — "we know nothing" is not the same as "the name cannot be
said", and the report tells you which happened.

## v0.6.1-alpha — Opening a dynasty save no longer looks like a hang

A patch for one problem reported against v0.6.0: **opening a dynasty save
looked like nothing was happening.** If Generate stayed grey, or the window
went unresponsive, or the command line printed nothing — that was this.
Nothing was broken. The tool was working, silently, for longer than anyone
would reasonably wait.

Reading a save unpacks 30 MB of compressed, bit-packed tables and writes the
ones the generator needs back out: about fifteen seconds on a fast local disk,
noticeably longer if Documents is redirected to OneDrive, which is where the
game keeps saves for most people. The tool said nothing while it did that. The
command line printed its first line only *after* the unpacking finished, and
the desktop app was worse — it did the work on the thread that draws the
window, so Windows rendered the whole app as *"Not Responding"*.

And if the dynasty had genuinely failed to open, choosing a roster afterwards
**overwrote the error** with "Ready — N players, nothing to fix": a reassuring
sentence above a button that would not work, with no trace of what went wrong.

### What now happens

- **It says what it is doing, before it does it.** *"Reading DYNASTY-BASE1 — a
  dynasty save takes twenty seconds or so to open."*
- **The window stays responsive** while it reads, and the dynasty buttons are
  disabled during the load so a second click cannot race the first.
- **Generate always explains itself.** Whenever it is unavailable, a line
  underneath names the missing step, or the dynasty's own error. That line
  cannot be overwritten by anything else.
- **The roster check stops saying "Ready".** It reads the roster file and
  nothing else, so it cannot know whether the run is ready. It says **"Roster
  is fine"**, which is what it actually established.

Same features, same output, same guarantees as v0.6.0.

## v0.6.0-alpha — Drop your dynasty in, get your dynasty back

**The headline: you no longer export anything, and you no longer import
anything.** Point the tool at your dynasty save, hand it a roster, and it
writes you a new save you load in the game.

```
RosterGenerator.Cli generate --dynasty DYNASTY-BASE1 --roster 2023_FSU.csv --save-out DYNASTY-2023FSU
```

Or in the desktop app: **Save file…** → your roster → Generate.

Before this release the workflow was: run PocketScout's export tool, run this
tool, then run a third-party roster importer. Two of those three steps are now
gone.

### Nothing to install

The download includes everything needed to read a save, **including its own
copy of the Node.js runtime** (v22.23.1 LTS, MIT licensed, checksum-verified
against nodejs.org at build time). You do not install Node. You do not run a
package manager. Unzip and run.

The bundled copy is also *private* to this application — whatever Node version
anything else on your machine wants, it cannot break this one, and this one
cannot break it.

The download grows from 68 MB to **122 MB** as a result — 33 MB for the
runtime and 21 MB for the save-format library, which carries the schema that
makes any of this possible. What it buys:

| Without it | With it |
|---|---|
| Export dynasty → run tool → import with a roster editor | Run tool |
| Three programs | One |
| A 27 MB CSV to place correctly | A save file you double-click in the game |
| Equipment changes need a second file imported separately | Written into the same save |

**If you would rather not use it, nothing is taken away.** The export-to-CSV
workflow still works exactly as it always has, is still first-class, and is
still what happens if the `tools\native-save` folder is missing. The tool tells
you what it needs rather than failing obscurely.

### What is guaranteed about writing a save

This is somebody's dynasty, so the rules are strict and they are tested:

- **Your save is never modified.** The output is always a new file, and
  writing over the file you supplied is refused outright.
- **Only fields that actually differ are written.** Every field is read back
  out of the save and compared first. Recreating the 2023 Florida State roster
  wrote 5,461 fields, changed 85 rows on Florida State and **zero rows
  anywhere else** in a 16,500-player table.
- **Empty roster slots are left alone.** A save pre-allocates slots holding no
  player; 243 of them were untouched in that run.
- **A game patch cannot corrupt your dynasty through this.** The save format
  version is checked, and a version this build does not recognise refuses to
  write rather than guessing at where fields live.

Verified before any of this shipped: reading a save reproduces PocketScout's
own export exactly — 4,584,474 field comparisons across 16,257 players, zero
mismatches — and unpacking then repacking a save with no edits returns a
byte-identical 30 MB database, on five different saves.

### Also in this release

- **A whole season at once.** `template --season 2010` writes a blank roster
  file for every team that played that year — 119 teams × 85 slots = 10,115
  rows — with Team, Season and Position filled in. One roster file can now
  carry any number of teams, and they all convert into one output.
- **Teams that were not in the FBS yet are left out, and named.** CFB27 ships
  today's 138 teams, so a 2010 season built from that list would silently
  include Sacramento State, James Madison, Liberty and a dozen more schools
  still in the FCS. Advisory, never a gate — the dates are in
  `data/FbsMembership.json` and are yours to correct.
- **The height column is now `HeightInches`, and it means inches.** Write
  `74`, not `6-2`. Excel turns `6-2` into the 2nd of June the moment it opens
  the file, which destroyed the height before the tool ever saw it — the
  commonest failure when filling the template with a spreadsheet assistant.
  Feet-inches is still read and converted, and reported so you can fix it at
  source. Files already filled in under the old `Height` name keep working.

### Upgrading

Nothing to migrate. Existing roster CSVs work unchanged, including ones using
the old `Height` column. Keep the whole unzipped folder together — `data`,
`templates` and `tools` all need to sit beside the executables.

### Known limitations

- Writing a save is verified on schema `C27_468_2`. A game patch may move it;
  the tool will say so and refuse rather than write.
- `data/FbsMembership.json` records schools that *joined* the FBS, not ones
  that left. A 2010 season template writes 119 teams where the real FBS had
  120 — the missing one is Idaho, which CFB27 does not carry.
- The desktop app writes the new save beside your original with `-Recreated`
  appended; there is no "choose where to put it" dialog yet.
