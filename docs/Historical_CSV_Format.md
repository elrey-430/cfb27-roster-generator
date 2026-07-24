# Historical Roster CSV Format

This is the **user-facing** input format for the roster generator. You fill
in real-world roster information — the application handles every CFB27
internal detail (team ids, position enums, redshirt flags, the weight
encoding). A ready-to-fill template lives at
`templates/HistoricalRosterTemplate.csv`.

## Rules

- One file describes **one team's roster for one season**.
- The first line is the header. Column order does not matter and header
  names are case-insensitive.
- Every value is optional except **FirstName**, **LastName** and
  **Position** — rows missing one of those are skipped (with a warning in
  the generation report). Anything else you leave blank is filled with a
  sensible default and listed in the report.
- If a value contains a comma (e.g. `Tampa, FL`), wrap it in double quotes
  — any spreadsheet program does this automatically when saving as CSV.

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

## Optional columns

| Column | Example | Notes |
|---|---|---|
| `Hometown` | `"Tampa, FL"` | Stored in the dataset; not yet written into the save (the target columns are not confirmed safe) |
| `PreviousSchool` | `Oregon` | Same as above |
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

| You provide | Written to the save as |
|---|---|
| Names | `FirstName` / `LastName` (replace-identity edit) |
| Position | Normalized CFB27 position; players are placed into matching roster slots where possible (a generic DE may take an LE or RE slot) |
| Number | `JerseyNum` |
| Height | `Height` (inches) |
| Weight | `Weight` using the confirmed encoding (stored = pounds − 160) |
| Class | `SchoolYear` + `RedshirtStatus` |

Ratings, portraits and equipment are inherited from the players being
replaced (rating/appearance generation is a later milestone); every
inherited default is listed in `Generation_Report.txt`.
