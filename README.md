# Historical CFB27 Roster Generator — Milestone 1 foundation

A .NET 8 class library (plus console proof-of-concept) that reliably loads a
CFB27 dynasty save `Player.csv` export, edits players in a controlled way,
validates the result — including the confirmed multi-field dependencies —
and exports a CSV compatible with the existing roster import tool.

- **`docs/Architecture.md`** — project structure, data flow, and why each
  design decision was made.
- **`docs/Schema.md`** — column-level ground truth for the 286-column
  player table: what is confirmed safe to write, what has an unresolved
  encoding, and what must never be hand-edited.

## Build & test (developers)

Requires the .NET 8 SDK (developers only — end users need nothing):

```
dotnet test          # 25 tests: round-trip fidelity, validation rules, PoC pipeline
dotnet run --project src/RosterGenerator.Poc -- <input Player.csv> <output.csv> [_row]
```

The PoC loads a roster, renames one player and changes their jersey number,
validates, exports, and prints an independent cell-by-cell diff proving
only `FirstName`, `LastName` and `JerseyNum` changed.

## Distribution (end users, Windows 10/11)

Publish a single self-contained executable — no Python, Node, WSL or .NET
runtime required on the target machine:

```
dotnet publish src/RosterGenerator.Poc -c Release -r win-x64 \
  --self-contained true -p:PublishSingleFile=true
```

## What this milestone deliberately does not do

Historical roster generation, rating generation, equipment/face mapping,
GUI, scraping, dynasty editing, and the two open reverse-engineering items
(`Weight` encoding, derived `Player[]` array tables) are all later
milestones — see the non-goals in `docs/Schema.md`.
