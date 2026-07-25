# Historical CFB27 Roster Generator

Recreate real historical college football rosters inside a CFB27 dynasty
save. You provide your own dynasty export and a simple spreadsheet-style
roster CSV; the generator replaces the chosen team's players and produces
an import-ready `Player.csv` plus a plain-English report of everything it
did — no programming knowledge, CFB27 schema knowledge, or database ids
required.

- **`docs/Historical_CSV_Format.md`** — the simple roster CSV you fill in
  (template: `templates/HistoricalRosterTemplate.csv`).
- **`docs/Architecture.md`** — project structure, pipelines, and design
  rationale.
- **`docs/Schema.md`** — column-level ground truth for the CFB27 player
  table (what is confirmed safe to write and why).
- **`docs/Status.md`** — current status, known unknowns, next milestone.
- **`Ratings/`** — how ratings are generated: `Rating_Model.md` (the
  pipeline and its verification), `Position_Formulas.md` (every position's
  priorities, baselines and caps), `Default_Assumptions.md` (what happens
  when data is missing, and the sanity guardrails), and
  `Player_Test_Results.csv` (generated ratings for known historical
  players).

## End-user workflow

1. **Export your dynasty** with the community save-export tool (it writes a
   folder of CSVs, one per table).
2. **Fill in a roster CSV** — copy
   `templates/HistoricalRosterTemplate.csv`, one row per player:
   `FirstName,LastName,Position,Number,Height,Weight,Class,Team,Season`
   (only the first three are required; real-world values like `Tailback`,
   `6-2`, `RS Junior` are expected).
3. **Run the generator:**

   ```
   RosterGenerator.Cli generate --dynasty <your export folder> --roster MyRoster.csv
   ```

   If your CSV has no `Team` column, the generator lists your dynasty's
   teams and asks you to pick one (`list-teams` shows them any time).
   Ratings are generated automatically from whatever performance data your
   CSV contains — stats, awards, draft position, combine numbers — using
   **EA's own overall formulas**, so the overall the tool writes is exactly
   what the game will show. Pass `--ratings inherit` to keep the ratings of
   the players being replaced instead.
4. **Collect the output** from `Output/`:
   - `Generated_Roster.csv` — **this is the file you import.** It is the
     full 286-column player table with your team replaced. (Your own roster
     CSV from step 2 is an input and is *not* importable; if the editor
     says *"missing required column `_tableIndex`"*, that input file was
     handed to it by mistake.)
   - `Generation_Report.txt` — players processed/mapped, missing fields,
     defaults used, and warnings.

The generator works with **any compatible dynasty export** — teams, ids
and roster structure are discovered from your own file, never hard-coded.
Position names are normalized via the editable
`data/PositionMappings.json`; extra school aliases (e.g. "FSU") can be
added to `data/TeamMappings.json`.

## Example

```
RosterGenerator.Cli list-teams --dynasty MyDynastyExport/
RosterGenerator.Cli generate --dynasty MyDynastyExport/ --roster 2013_FSU.csv --team "Florida State" --season 2013
RosterGenerator.Cli compare --left Output/Generated_Roster.csv --right OtherExport/Player.csv --team "Florida State" --dynasty MyDynastyExport/
```

## Distribution (Windows 10/11)

One self-contained executable — no Python, Node, WSL or .NET runtime
needed on the target machine:

```
dotnet publish src/RosterGenerator.Cli -c Release -r win-x64 \
  --self-contained true -p:PublishSingleFile=true
```

The publish folder contains `RosterGenerator.Cli.exe` plus the editable
`data/` mapping files and the roster template.

## Build & test (developers)

Requires the .NET 8 SDK (developers only — end users need nothing):

```
dotnet test    # 86 tests: round-trip fidelity, validation, pipeline, ratings, 2023 FSU regression
```

The 2023 Florida State recreation (Milestone 2) is preserved as a
byte-stable regression test — `Tests/2023_FSU_Input.csv` +
`Tests/DonorDynasty/` must keep producing `Tests/2023_FSU_Expected_Output.csv`
exactly.

## What is deliberately not implemented yet

Equipment/face recreation, archetype selection, GUI polish,
automatic historical data gathering, multi-season bulk generation, dynasty
editing, and the derived `Player[]` array recompute — see `docs/Status.md`.
