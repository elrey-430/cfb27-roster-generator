# Historical CFB27 Roster Generator

A .NET 8 class library and CLI that reliably loads a CFB27 dynasty save
`Player.csv` export, edits players in a controlled way, validates the
result — including the confirmed multi-field dependencies — and exports a
CSV compatible with the existing roster import tool. As of Milestone 2 it
can independently recreate a historical team roster (the 2023 Florida
State Seminoles) from a public-information dataset.

- **`docs/Architecture.md`** — project structure, data flow, and why each
  design decision was made.
- **`docs/Schema.md`** — column-level ground truth for the 286-column
  player table: what is confirmed safe to write, what has an unresolved
  encoding, and what must never be hand-edited.
- **`docs/Status.md`** — current status, completed features, known
  unknowns, next milestone.

## Build & test (developers)

Requires the .NET 8 SDK (developers only — end users need nothing):

```
dotnet test          # round-trip fidelity, validation rules, historical pipeline
dotnet run --project src/RosterGenerator.Poc -- <input Player.csv> <output.csv> [_row]
```

The PoC loads a roster, renames one player and changes their jersey number,
validates, exports, and prints an independent cell-by-cell diff proving
only `FirstName`, `LastName` and `JerseyNum` changed.

## Historical roster pipeline (Milestone 2)

Generate a CFB27-importable `Player.csv` with one team's roster replaced by
a historical dataset, plus a validation report:

```
dotnet run --project src/RosterGenerator.Cli -- generate \
  --base <base save Player.csv> \
  --historical HistoricalData/2023/FloridaState.json \
  --output Output/2023_Florida_State_CFB27.csv \
  --report Output/2023_Florida_State_Report.md
```

Compare one team's roster between two Player CSVs (e.g. generated vs a
manual benchmark export):

```
dotnet run --project src/RosterGenerator.Cli -- compare \
  --left Output/2023_Florida_State_CFB27.csv --right <benchmark Player.csv> \
  --team "Florida State"
```

Team ids and position names are never hard-coded — they come from the
editable `data/TeamMappings.json` (generated from the save's own Team
table) and `data/PositionMappings.json` (Tailback→HB, Cornerback→CB, ...).
Datasets live under `HistoricalData/<season>/<School>.json`; the model
tolerates missing values, and every substituted default is listed in the
generated report.

## Distribution (end users, Windows 10/11)

Publish a single self-contained executable — no Python, Node, WSL or .NET
runtime required on the target machine:

```
dotnet publish src/RosterGenerator.Cli -c Release -r win-x64 \
  --self-contained true -p:PublishSingleFile=true
```

## What is deliberately not implemented yet

Ratings generation, equipment/face recreation, GUI, web scraping, multiple
seasons, dynasty editing, and the two open reverse-engineering items
(`Weight` encoding, derived `Player[]` array tables) — see `docs/Status.md`
for the recommended next milestone.
