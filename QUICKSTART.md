# Historical CFB27 Roster Generator — Quick start

Recreate a historical college football roster inside your CFB27 dynasty save.

Nothing to install. Both programs in this folder are self-contained: they run
on a clean Windows 10 or 11 machine with no .NET runtime, no Python, and no
setup.

```
RosterGenerator.Gui.exe    the app — start here
RosterGenerator.Cli.exe    the same thing from a command prompt
data\                      editable team, position, rating and archetype files
templates\                 roster files to copy and fill in
```

Keep the `data` and `templates` folders next to the executables.

---

## 1. Export your dynasty

Use the community save-export tool. It writes a folder of CSV files, one per
table. Point the generator at that folder — it finds what it needs itself.

## 2. Fill in a roster

Copy `templates\HistoricalRosterTemplate_Basics.csv` and open it in Excel,
Google Sheets or Notepad.

```csv
FirstName,LastName,Position,Number,Class,Role,Team,Season
Jordan,Travis,QB,13,RS Senior,Starter,Florida State,2023
Trey,Benson,Tailback,3,RS Junior,Starter,Florida State,2023
Samuel,Singleton,Tailback,28,Freshman,,Florida State,2023
```

**Only `FirstName`, `LastName` and `Position` are required.** Old rosters are
badly documented and you are not expected to find a complete record for every
player. Everything you leave out is filled in for you and listed in the
report.

Two things are worth the effort if you can manage them:

- **`Role`** — `Starter`, `Backup`, `Reserve` or `Walk-on`. One word per
  player is what separates a starter from a third-stringer when you have
  nothing else. Leave it blank where you are unsure; a blank changes nothing.
- **Statistics, draft position and awards**, in the fuller
  `HistoricalRosterTemplate.csv`. These make the ratings genuinely accurate.
  None of them are required.

You do **not** need to research a team's walk-ons. A CFB27 team always carries
85 players, and every slot you do not supply is filled in as believable
end-of-roster depth.

## 3. Generate

Open `RosterGenerator.Gui.exe`:

1. **Browse** to your dynasty export folder.
2. **Browse** to your roster CSV. It is checked immediately and tells you
   about anything wrong — before anything is written.
3. Confirm the **team** and **season**.
4. Click **Generate**.

You get two files in `Output\`:

- `Generated_Roster.csv` — import this with your roster editing tool.
- `Generation_Report.txt` — every value that was filled in, corrected or
  could not be used, player by player. **Worth reading.**

---

## Command line

Same engine, same results.

```
RosterGenerator.Cli.exe validate --roster MyRoster.csv --dynasty C:\path\to\export
RosterGenerator.Cli.exe generate --roster MyRoster.csv --dynasty C:\path\to\export
RosterGenerator.Cli.exe list-teams --dynasty C:\path\to\export
```

`validate` checks your file and writes nothing. It exits non-zero only when
something would stop generation.

Useful options for `generate`:

| Option | Meaning |
|---|---|
| `--team "Florida State"` | Team, if your CSV has no `Team` column |
| `--season 2023` | Season, for labelling |
| `--output <path>` | Where to write the roster (default `Output\Generated_Roster.csv`) |
| `--ratings inherit` | Keep the ratings of the players being replaced |
| `--fill leave` | Leave unsupplied slots alone instead of filling them |

Run it with no arguments for the full list.

---

## If something goes wrong

**"Could not find the data file …"** — the `data` folder must sit next to the
executable. Unzip the whole folder, not just the .exe.

**"… is missing the required 'firstname' column"** — the first line of your
CSV must be the header. Check you did not delete it.

**"… has a header but no usable player rows"** — every row needs a first name,
a last name and a position.

**"CSV file is missing required column `_tableIndex`"** — this comes from the
*roster editor*, and means you handed it your input file. Import
`Output\Generated_Roster.csv` instead.

**A player did not turn out how you expected** — open
`Generation_Report.txt` and search for their name. Every decision the tool made
about them is listed there.

---

Full documentation is in the project repository: the roster CSV format, how
ratings are derived, and what is known about the save file's columns.
