# Historical CFB27 Roster Generator

Recreate real historical college football rosters inside a CFB27 dynasty.

The tool works on **CSV files, not save files**: you export your dynasty to
CSVs with the community export tool, point this at that folder along with a
simple spreadsheet-style roster CSV you fill in yourself, and it writes an
import-ready player table plus a plain-English report of everything it did.
No programming knowledge, CFB27 schema knowledge, or database ids required.

```
your dynasty → [export tool] → CSV files → [this tool] → new CSV → [roster editor]
```

- **`docs/Historical_CSV_Format.md`** — the simple roster CSV you fill in
  (start from `templates/HistoricalRosterTemplate_Basics.csv`).
- **`docs/Architecture.md`** — project structure, pipelines, and design
  rationale.
- **`docs/Schema.md`** — column-level ground truth for the CFB27 player
  table (what is confirmed safe to write and why).
- **`QUICKSTART.md`** — the page that ships with the release: install-free
  setup, the four steps, and what to do when something goes wrong.
- **`docs/Status.md`** — current status, known unknowns, next milestone.
- **`Ratings/`** — how ratings are generated: `Rating_Model.md` (the
  pipeline and its verification), `Position_Formulas.md` (every position's
  priorities, baselines and caps), `Default_Assumptions.md` (what happens
  when data is missing, and the sanity guardrails), and
  `Player_Test_Results.csv` (generated ratings for known historical
  players).

## End-user workflow

1. **Export your dynasty to CSVs** with the community export tool. It writes
   a folder of CSV files, one per table; that folder is what you point the
   generator at. The Player and Team tables are discovered inside it, so you
   never have to identify a particular file (the Player CSV on its own also
   works, with the team named explicitly).
2. **Fill in a roster CSV** — copy
   `templates/HistoricalRosterTemplate_Basics.csv`, one row per player:

   ```csv
   FirstName,LastName,Position,Number,Class,Role,Team,Season
   Jordan,Travis,QB,13,RS Senior,Starter,Florida State,2023
   ```

   `Role` (`Starter` / `Backup` / `Reserve` / `Walk-on`) is worth the most
   for the least effort — one word per player is what separates a starter
   from a third-stringer when you have nothing else. Leave it blank where
   you are unsure; a blank generates exactly what the tool would have
   without the column.

   Old rosters are badly documented and you are **not** expected to find a
   full record for every player. Only the name and position are required;
   real-world values like `Tailback`, `6-2` and `RS Junior` are what the
   tool expects. Anything you leave out is filled in for you and listed in
   the report, and a mistake in one cell never costs you the file. Use the
   fuller `templates/HistoricalRosterTemplate.csv` when you *do* have
   statistics, draft positions or awards — more detail buys better ratings,
   but it is never required.
3. **Run the generator.** Open `RosterGenerator.Gui.exe` and follow the four
   steps, or from a command prompt:

   ```
   RosterGenerator.Cli validate --roster MyRoster.csv --dynasty <folder of exported CSVs>
   RosterGenerator.Cli generate --roster MyRoster.csv --dynasty <folder of exported CSVs>
   ```

   `validate` checks your roster file and writes nothing, so a mistake shows
   up in a few lines instead of inside a 27 MB file's report. The desktop app
   runs the same check the moment you choose a file.

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

The generator works with **any compatible export** — teams, ids and roster
structure are discovered from your own CSVs, never hard-coded.
Position names are normalized via the editable
`data/PositionMappings.json`; extra school aliases (e.g. "FSU") can be
added to `data/TeamMappings.json`.

## Example

```
RosterGenerator.Cli list-teams --dynasty MyExportedCsvs/
RosterGenerator.Cli generate --dynasty MyExportedCsvs/ --roster 2013_FSU.csv --team "Florida State" --season 2013
RosterGenerator.Cli compare --left Output/Generated_Roster.csv --right OtherExport/Player.csv --team "Florida State" --dynasty MyExportedCsvs/
```

## Distribution (Windows 10/11)

```
./build-release.sh 7.0.0
```

Produces `dist/CFB27-Roster-Generator-7.0.0-win-x64/` and a zip of it: the
desktop app and the command-line tool as self-contained executables that run
on a clean Windows 10/11 machine with no .NET runtime, Python, Node or WSL,
alongside the editable `data/` files, the roster `templates/` and
`QUICKSTART.md`.

## Build & test (developers)

Requires the .NET 8 SDK (developers only — end users need nothing):

```
dotnet test    # 221 tests: round-trip fidelity, validation, pipeline, ratings,
               # roster fill, sparse input, validate integrity, GUI smoke,
               # and the 2023 FSU byte-stability regression
```

The 2023 Florida State recreation (Milestone 2) is preserved as a
byte-stable regression test — `Tests/2023_FSU_Input.csv` +
`Tests/DonorDynasty/` must keep producing `Tests/2023_FSU_Expected_Output.csv`
exactly.

## What is deliberately not implemented yet

Equipment and face recreation, automatic historical data gathering,
multi-season bulk generation, dynasty editing, and the derived `Player[]`
array recompute — see `docs/Status.md`.

## Licence

MIT — see [LICENSE](LICENSE).

The licence covers this project's code, documentation and derived data. It
does not cover the game data in the test fixtures, nor the community
save-export and roster-import tools this depends on. See [NOTICE.md](NOTICE.md).
