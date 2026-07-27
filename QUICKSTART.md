# Historical CFB27 Roster Generator — Quick start

Recreate a historical college football roster inside your CFB27 dynasty.

**Point it at your dynasty save and get a dynasty save back.**

```
your dynasty save  →  [this tool]  →  new dynasty save
```

Your save is never changed: the result is always a new file. This route needs
Node.js 22.19+ (https://nodejs.org) — it is the one thing not included here.

Without Node, or if you prefer it, the original route still works in full: you
export your dynasty to CSVs with the community export tool, this program writes
a new CSV, and the community roster editor imports it back.

```
your dynasty  →  [export tool]  →  CSV files  →  [this tool]  →  new CSV  →  [roster editor]
```

Both programs in this folder are self-contained: they run on a clean Windows 10
or 11 machine with no .NET runtime, no Python, and no setup.

```
RosterGenerator.Gui.exe    the app — start here
RosterGenerator.Cli.exe    the same thing from a command prompt
data\                      editable team, position, rating and archetype files
templates\                 roster files to copy and fill in
```

Keep the `data` and `templates` folders next to the executables.

---

## 1. Point at your dynasty

**The easy way:** your save file itself, in
`Documents\EA SPORTS College Football 27\saves\` — a file with no extension,
named for your dynasty. Nothing to export.

**Or:** run the community export tool on your dynasty. It writes a **folder of
CSV files, one per table** — `Player`, `Team`, and dozens more. That folder is
what you point this tool at.

Either way you do not need to know which file is which: the Player and Team
tables are found for you, and the rest are ignored. (If you only have the
Player CSV, that works too — you just have to name the team yourself.)

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

1. **Browse** to your dynasty save, or the folder of exported CSVs, from
   step 1.
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

`--dynasty` takes your dynasty save file, the folder of exported CSVs, or the
Player CSV itself.

### The short way: save in, save out

```
RosterGenerator.Cli.exe generate --roster MyRoster.csv ^
    --dynasty "%USERPROFILE%\Documents\EA SPORTS College Football 27\saves\DYNASTY-BASE1" ^
    --save-out "%USERPROFILE%\Documents\EA SPORTS College Football 27\saves\DYNASTY-RECREATED"
```

Then load it in the game. No export step and no separate roster importer.

Only the fields that actually change are written, and the empty roster slots
the game keeps in reserve are left exactly as they were. **Your original save
is never modified** — the output is always a new file, and writing over the
one you supplied is refused.

This route needs **Node.js 22.19 or newer** installed (https://nodejs.org).
Without it you get a message saying so, and the export route below still
works.

```
RosterGenerator.Cli.exe validate --roster MyRoster.csv --dynasty C:\path\to\exported-csvs
RosterGenerator.Cli.exe generate --roster MyRoster.csv --dynasty C:\path\to\exported-csvs
RosterGenerator.Cli.exe list-teams --dynasty C:\path\to\exported-csvs
```

`validate` checks your file and writes nothing. It exits non-zero only when
something would stop generation.

### Doing a whole season

```
RosterGenerator.Cli.exe template --dynasty C:\path\to\exported-csvs --season 2010 --output 2010_Season.csv
```

That writes a blank roster file for the *whole year* — one row per roster
slot for every team that played, with `Team`, `Season` and `Position` already
filled in (about 10,000 rows). Fill it in, then `validate` and `generate` it
exactly as you would for one team; they all end up in the single file you
import.

Teams that had not reached the FBS that season are left out and named, so a
2010 file does not quietly include Sacramento State. The dates are in
`data\FbsMembership.json` if you disagree with any of them.

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

**"No Player table found under …"** — the folder you chose is not the one the
export tool wrote. It should contain many CSV files, one of which is the
Player table. If you meant to use your save file, pick the save itself, not
the folder it lives in.

**"Node.js 22.19 or newer is needed …"** — reading a save directly needs Node
installed (https://nodejs.org). Install it, or export your dynasty to CSVs and
point `--dynasty` at that folder instead.

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
