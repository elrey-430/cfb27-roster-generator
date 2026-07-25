# Historical Roster CSV Format

> **This file is an *input*, not something you import into the roster
> editor.** You fill it in, the generator reads it alongside the CSVs
> exported from your dynasty, and writes the importable file to
> `Output/Generated_Roster.csv`. Handing this format straight to the roster
> editor produces *"CSV file is missing required column `_tableIndex`"* —
> that means the generator step was skipped.
>
> ```
> your roster CSV      ┐
>                      ├→ [generator] → Output/Generated_Roster.csv → roster editor
> exported dynasty CSVs┘
> ```

This is the **user-facing** input format for the roster generator. You fill
in real-world roster information — the application handles every CFB27
internal detail (team ids, position enums, redshirt flags, the weight
encoding).

## Start here: you only need the basics

Old rosters are badly documented and you will not find a full record for
every player. **You are not expected to.** This works:

```csv
FirstName,LastName,Position,Number,Class,Role,Team,Season
Jordan,Travis,QB,13,RS Senior,Starter,Florida State,2023
Trey,Benson,Tailback,3,RS Junior,Starter,Florida State,2023
Samuel,Singleton,Tailback,28,Freshman,,Florida State,2023
```

That is `templates/HistoricalRosterTemplate_Basics.csv` — start from it.
Strictly, even **Number**, **Class** and **Role** are optional: a row needs
only a first name, a last name and a position.

**`Role` is the single most valuable column for the effort.** Without it,
players you supply nothing else for all come out within a couple of points
of each other, because class year is the only thing separating them. One
word per player fixes that:

| Role | Effect on a player with no other data |
|---|---|
| `Starter` | ~80 |
| `Backup` | ~74 |
| `Reserve` | ~69 |
| *(blank)* | ~78 — the same as if the column did not exist |

Leaving it blank is not a penalty and not an error; it generates exactly
what the tool produced before the column existed. Fill it in for the
players you are sure about and leave the rest empty (the third row above).

Everything you leave out is filled in for you — height, weight, hometown,
all 56 attributes, the archetype — and **every single substitution is listed
in `Generation_Report.txt`**, so you always know what the tool decided
rather than what you told it. The rest of the 85-man roster is filled in
too, so you never have to research a team's walk-ons.

Use the full `templates/HistoricalRosterTemplate.csv` when you *do* have
statistics, draft slots or awards. More detail buys better ratings; it is
never required.

## Rules

- One file describes **one team's roster for one season**.
- The first line is the header. Column order does not matter, header names
  are case-insensitive, and spaces are ignored (`First Name` works).
  Columns the tool does not recognize are left alone.
- Every value is optional except **FirstName**, **LastName** and
  **Position** — rows missing one of those are skipped (with a warning in
  the generation report). Anything else you leave blank is filled with a
  sensible default and listed in the report.
- **A mistake in one cell never costs you the file.** A jersey number,
  height or weight outside what the game accepts is reported and that
  player keeps the replaced player's value; the other 84 are unaffected.
- Saving from Excel is fine — the byte-order mark it adds and the empty
  rows it leaves behind are both ignored.
- **Untidy numbers are read, not thrown away.** `#13`, `13.0`, `212 lbs`,
  `4.49s` and `1,250` are all understood, and the report says what each was
  read as. Something with no number in it at all (`twelve`) is still
  reported and skipped rather than guessed at.
- If a value contains a comma (e.g. `Tampa, FL`), wrap it in double quotes
  — any spreadsheet program does this automatically when saving as CSV.

Every correction and every substitution is listed in
`Generation_Report.txt` — the tool never changes your data silently.

### Mistakes it catches for you

| What you did | What happens |
|---|---|
| Left a row short, or added a stray comma | Padded or trimmed, and the row number is reported |
| Repeated a column heading | The first is used, and it says so |
| Misspelled a class (`Sinior`) | Reported; that player keeps the replaced player's class |
| Misspelled a role (`Startr`) | Reported, with the list of roles that work |
| Used a position it does not know | Only that player is skipped, and it names the file to add the alias to |
| Wrote a hometown that is not a US state | Stored as `NonUS` and reported |
| Listed more players than the team has slots | The first 85 are used and the rest are named |
| Supplied a header with no player rows | Refused outright, rather than replacing the team with 85 strangers |

## Required columns

| Column | Example | Notes |
|---|---|---|
| `FirstName` | `Jordan` | |
| `LastName` | `Travis` | |
| `Position` | `QB`, `Tailback`, `Cornerback` | Real-world position names are fine — they are normalized via `data/PositionMappings.json` (e.g. Tailback → HB, Edge → LE). Add your own aliases to that file if needed |

## Recommended columns

| Column | Example | Notes |
|---|---|---|
| `Number` | `13` | Jersey number 0–99. Blank = keeps the replaced player's number |
| `Height` | `6-2`, `6'2"`, or `74` | Feet-inches or plain inches |
| `Weight` | `212` | Pounds (160–400). Blank = keeps the replaced player's weight |
| `Class` | `Freshman`, `RS Junior`, `Redshirt Senior`, `Graduate` | "RS"/"Redshirt" prefixes set the in-game redshirt flag; Graduate becomes Senior |
| `Team` | `Florida State` | The school this roster belongs to. May instead be chosen when running the generator; must match a team in **your** dynasty (see `list-teams`) |
| `Season` | `2013` | The historical season, used for labeling and reports |
| `Role` | `Starter` | `Starter` / `Backup` / `Reserve` / `Walk-on`. The cheapest way to make a roster look right — see above. Blank behaves exactly as if the column were absent; a word the tool does not recognize is ignored and reported |

## Optional columns — performance evidence (drives rating generation)

Fill in whatever you have. Each one improves the generated ratings and the
confidence score; leaving them all blank still works (the player is rated
from position and class defaults and reported as Low confidence).

| Column | Example | Notes |
|---|---|---|
| `StarRating` | `5` | Recruiting stars, 1–5 |
| `Forty` | `4.49` | Verified 40-yard dash. **Sets speed exactly** (4.30→99, 4.40→96, 4.50→92) and is never overridden |
| `Bench` | `21` | 225 lb reps → strength |
| `Vertical` | `38` | Inches → jumping |
| `Shuttle` | `4.15` | 20-yard shuttle → agility |
| `ThreeCone` | `6.95` | Three-cone → change of direction |
| `DraftRound` / `DraftPick` | `2` / `41` | NFL draft. `DraftPick` is the **overall** pick and the strongest single signal — but see below. Put `UDFA` in `DraftPick` for undrafted free agents |
| `Awards` | `Heisman; Consensus All-American` | **Semicolon-separated.** Only the best award counts |
| `AwardContender` | `Heisman` | Awards the player was **in contention for** without winning — a finalist, a semifinalist, someone in the conversation. Same names as `Awards`, scored a few points lower. Worth more than winning a smaller award: a Heisman finalist out-rates an all-conference pick. Often the only evidence that survives when an injury ends a season early |

### Statistics

Season stats for the player's position. Supply the raw counting stats you
have — percentages and per-carry averages are derived automatically.

`PassYards` `PassTD` `PassInt` `Completions` `Attempts` `RushYards` `RushTD`
`RushAttempts` `RecYards` `RecTD` `Receptions` `Tackles` `Sacks`
`TacklesForLoss` `Interceptions` `PassesDefended` `ForcedFumbles`
`FieldGoalsMade` `FieldGoalsAttempted` `LongFieldGoal` `PuntAverage`
`GamesPlayed` `GamesStarted`

## Optional columns

| Column | Example | Notes |
|---|---|---|
| `Hometown` | `"Tampa, FL"` | **Written to the generated roster.** Accepts `FL`, `Florida` or `West Virginia`; anything that is not a US state (e.g. `Melbourne, Australia`) is stored as `NonUS` and reported |
| `PreviousSchool` | `Oregon` | **Written to the generated roster.** A school your dynasty does not carry (an FCS school such as `Albany`) is recorded the way real FCS transfers are, and reported. Leave blank for a player who never transferred |
| `Notes` | `Team captain` | Free text for your own bookkeeping; appears in reports |

## Example

```csv
FirstName,LastName,Position,Number,Height,Weight,Class,Team,Season,Hometown,PreviousSchool,Notes
Jordan,Travis,QB,13,6-1,212,RS Senior,Florida State,2023,"West Palm Beach, FL",Louisville,Starter
Trey,Benson,Tailback,3,6-1,216,RS Junior,Florida State,2023,"Greenville, MS",Oregon,
Jared,Verse,Defensive End,5,6-4,260,RS Senior,Florida State,2023,"Dade City, FL",Albany,
Ryan,Fitzgerald,K,88,6-1,190,RS Junior,Florida State,2023,"Colquitt, GA",,
```

## What happens to your data

| You provide | Written to the generated roster as |
|---|---|
| Names | `FirstName` / `LastName` (replace-identity edit) |
| Position | Normalized CFB27 position; players are placed into matching roster slots where possible (a generic DE may take an LE or RE slot) |
| Number | `JerseyNum` |
| Height | `Height` (inches) |
| Weight | `Weight` using the confirmed encoding (stored = pounds − 160) |
| Class | `SchoolYear` + `RedshirtStatus` |
| Role / stats / awards / draft / combine | Generated ratings — all 56 attributes plus the overall, computed with EA's own overall formula |
| Hometown | `PLYR_HOME_TOWN` (town) + `PLYR_HOME_STATE` (state enum) |
| Position, weight, height and stats | The player's **archetype** (`PlayerType`), e.g. a 225 lb back becomes `HB_PowerBack`. The overall is then recomputed with that archetype's formula |
| PreviousSchool | `PLYR_PREVTEAMID` (the school's id in your dynasty) |

Portraits and equipment are inherited from the players being replaced; every inherited default is listed in
`Generation_Report.txt`. Rating generation can be turned off with
`--ratings inherit`. See `Ratings/Rating_Model.md` for how ratings are
derived and `Ratings/Default_Assumptions.md` for the guardrails.

## Roster size — the slots you do not fill

A CFB27 team always carries **85 players**. Every slot your file does not
supply keeps its original fictional player, and because the game builds its
depth chart from ratings alone, a leftover 82-overall quarterback will start
ahead of yours.

By default the generator re-rates those slots as end-of-roster depth, using
the overall a real save carries at each roster rank and holding every one of
them below your weakest player at that position. Their names, jersey numbers
and portraits are unchanged, and each one is listed in the report. Pass
`--fill leave` to keep them exactly as they are.

So there is no need to research a team's walk-ons: supply the players you
know about and the rest of the roster is filled in for you.

## Team strength

Ratings also account for the program. A backup at a playoff team and a
backup at the worst team in the country are not the same player, so the
generator measures the team you selected against a typical one and adjusts
players you gave little evidence for. Players with a draft slot, awards or a
stat line are rated on their own record and are unaffected. Nothing extra is
required from you — the adjustment comes from the dynasty you loaded.

## A note on draft position

Draft position is the heaviest signal the generator uses, and the only one
that looks *backwards* from the season you are recreating. It records where
the NFL took a player months later, which is a different question from how
they played.

Sometimes those answers disagree badly. Jordan Travis was the 2023 ACC
Player of the Year and went in the fifth round, because he broke his leg in
November. Taken at face value, his draft slot rated him seven points below
his own season.

So when a draft slot sits well below what a player's awards and statistics
say, the generator trusts the record of the season more and says so in the
report:

```
Draft position counted for less: Drafted #171 overall sits 14 points below
this player's awards (conference player of the year). A draft slot records
where the NFL took someone months later, not how they played in this season.
```

The draft slot is not ignored — a late pick is still information — it just
stops outvoting the season itself. **This is the case `AwardContender` is
for:** if a player's season ended early, what they were in contention for
before it ended is often the truest thing left in the record.
