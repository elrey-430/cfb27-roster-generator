# Historical CFB27 Roster Generator

Recreate real historical college football rosters inside a CFB27 dynasty.

Point it at your dynasty and a simple spreadsheet-style roster CSV you fill in
yourself, and it writes the roster into the save, plus a plain-English report
of everything it did. No programming knowledge, CFB27 schema knowledge, or
database ids required.

```
your dynasty save → [this tool] → new dynasty save
```

It also reads the community export tool's CSVs and writes an import-ready
player table, which is how it worked before it could open a save and remains a
first-class route:

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
- **`docs/Release_Notes.md`** — what changed in each release, and why.
- **`docs/Status.md`** — current status, known unknowns, next milestone.
- **`Ratings/`** — how ratings are generated: `Rating_Model.md` (the
  pipeline and its verification), `Position_Formulas.md` (every position's
  priorities, baselines and caps), `Default_Assumptions.md` (what happens
  when data is missing, and the sanity guardrails), and
  `Player_Test_Results.csv` (generated ratings for known historical
  players).

## The short version

Point it at your dynasty save, hand it a roster, get a dynasty save back:

```
RosterGenerator.Cli generate --dynasty DYNASTY-BASE1 --roster 2023_FSU.csv --save-out DYNASTY-2023FSU
```

Copy the result into `Documents\EA SPORTS College Football 27\saves\` and load
it. No export step, no separate roster importer. Your original save is never
modified — the output is always a new file.

**Nothing to install.** The release bundles everything it needs to read a save,
including its own copy of the Node.js runtime (v22 LTS, MIT, checksum-verified
at build time). You do not install Node, you do not run a package manager, and
the bundled copy cannot be broken by any other version already on the machine.

If you are running from a source checkout rather than the release zip, that
copy is not there — install [Node.js](https://nodejs.org) 22.19+ or run
`build-release.sh`, which fetches it. Either way the export-based workflow below
needs none of this, and the tool names what is missing rather than failing
obscurely.

## End-user workflow

1. **Point at your dynasty.** Either the **save file itself** (recommended —
   see above), or a folder of CSVs from the community export tool. The Player
   and Team tables are discovered inside either, so you never have to identify
   a particular file (the Player CSV on its own also works, with the team named
   explicitly).
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
   real-world values like `Tailback` and `RS Junior` are what the tool
   expects. Anything you leave out is filled in for you and listed in the
   report, and a mistake in one cell never costs you the file.

   One column is strict on purpose: **`HeightInches` is inches** — write
   `74`, not `6-2`. Excel turns `6-2` into the 2nd of June the moment it
   opens the file, which quietly destroys the height, so the column name is
   the instruction. Feet-inches is still read and converted, and reported so
   you can fix it at the source.

   Use the fuller `templates/HistoricalRosterTemplate.csv` when you *do* have
   statistics, draft positions or awards — more detail buys better ratings,
   but it is never required.
3. **Run the generator.** Open `RosterGenerator.Gui.exe` and follow the four
   steps, or from a command prompt:

   ```
   RosterGenerator.Cli validate --roster MyRoster.csv --dynasty DYNASTY-BASE1
   RosterGenerator.Cli generate --roster MyRoster.csv --dynasty DYNASTY-BASE1 --save-out DYNASTY-NEW
   ```

   `--dynasty` takes your save file or an export folder; `--save-out` writes a
   save you can drop straight into the game.

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
4. **Collect the output.** With `--save-out` there is one file and you are
   done: copy it into your saves folder. Otherwise, from `Output/`:
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

## Recreating a whole season

One roster CSV can carry any number of teams — just fill in `Team` per row —
and they all convert into the single output table you import once. Ask the
tool for the blank file rather than typing 10,000 rows by hand:

```
RosterGenerator.Cli template --dynasty MyExportedCsvs/ --season 2010 --output 2010_Season.csv
```

That writes one row per roster slot for every team that played in 2010, with
`Team`, `Season` and `Position` already filled in, ready to hand to a
spreadsheet. Fill in the players, then `validate` and `generate` it exactly
as you would a single team. A full season is around 10,000 rows and
generates in about twenty seconds.

**Teams that were not in the FBS yet are left out, and named on the way
past.** CFB27 ships today's 138 teams, so a 2010 season built from that list
would silently include Sacramento State, James Madison, Liberty and a dozen
more schools that were still in the FCS — with nothing in the save to tell
you. The dates live in `data/FbsMembership.json`, and `validate` reports the
same thing as a note if a filled file names one. It is advisory in both
places, never a gate: correct the file if you know better, or ignore the
note, because the roster generates either way.

## Example

```
RosterGenerator.Cli list-teams --dynasty MyExportedCsvs/
RosterGenerator.Cli template  --dynasty MyExportedCsvs/ --season 2013 --output 2013_Season.csv
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
dotnet test    # 295 tests: round-trip fidelity, validation, pipeline, ratings,
               # archetype floors, equipment eras, faces, roster fill, sparse
               # input, validate integrity, GUI smoke, and the 2023 FSU
               # byte-stability regression
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
