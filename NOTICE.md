# Notice

## What the MIT licence covers

The source code, the documentation, and the derived data files in `data/` —
the rating model, position and team name mappings, archetype rules and the
measured roster-depth curve.

## What it does not cover

**Game data.** This project reads and writes files exported from a
copyrighted video game. Two small fixtures containing game data are
committed so the tests can run:

| File | What it is |
|---|---|
| `Tests/DonorDynasty/0152_Player.csv` | 85 players from one team |
| `Tests/DonorDynasty/2225_Team.csv` | the save's team list |
| `Tests/2023_FSU_Expected_Output.csv` | the same 85 rows after generation |

They are the minimum needed to prove the tool produces a byte-identical
result, and they are not licensed by this project. The full base-save player
table is deliberately **not** committed for the same reason — see
`Output/README.md`.

**Player names, statistics and awards** in `Tests/2023_FSU_Input.csv` and
`HistoricalData/` are public information compiled from published sources
(university athletics sites, conference award announcements, draft results
and contemporary reporting).

## Affiliation

This is an unofficial fan project. It is not affiliated with, endorsed by, or
connected to EA Sports, the ACC, Florida State University, or any other
school or organisation named in it. All trademarks belong to their owners.

It also depends on the community's save-export and roster-import tools, which
are separate projects with their own authors and terms; this project neither
includes nor replaces them.
