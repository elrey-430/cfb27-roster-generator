# Project Status

_Last updated: 2026-07-24 — end of Milestone 1._

## Current status

**Milestone 1 (Foundation & Proof of Concept) is complete and verified.**
The library can reliably load a CFB27 dynasty save `Player.csv` export,
represent its 16,500 rows × 286 columns internally, apply controlled edits,
validate the result (including the confirmed multi-field dependencies), and
export a CSV compatible with the existing roster import tool.

The decisive verification ran against a real save export
(`DYNASTY-JUL24-BASE`): a rename + jersey-number edit to one player
produced an output file that a byte-level `diff` showed differing from the
input in **exactly one line, in exactly the `FirstName`, `LastName` and
`JerseyNum` cells** — with the identity-asset fields correctly retaining
their original values, matching observed in-game rename behavior.

The project now lives standalone in this repository (extracted from the
`cfb27-aio-app` monorepo, history preserved). Historical roster generation
has **not** started; that is by design (see Non-goals in the Milestone 1
brief).

- Solution: `RosterGenerator.sln` — .NET 8, zero external dependencies in
  the core library
- Tests: 25/25 passing (`dotnet test`)
- Distribution path verified: `dotnet publish -r win-x64 --self-contained
  -p:PublishSingleFile=true` produces a single ~67 MB `.exe` that runs on a
  clean Windows 10/11 machine with no runtime installed

## Completed features

- **Byte-preserving CSV layer** (`Csv/`) — an unedited file round-trips
  byte-identically; exports can only differ in cells an edit deliberately
  changed. This is the compatibility guarantee for the community import
  tool.
- **Typed player model** (`Model/`) — `PlayerRoster`/`Player` views over
  the raw table, with a load-time snapshot enabling per-cell change
  tracking; unknown columns pass through untouched.
- **Intent-recording edit operations** (`Editing/`) —
  `RenamePlayer` (cosmetic; assets untouched), `ReplacePlayerIdentity`
  (different real person; caller must supply new asset values),
  `TransferPlayer` (applies all five confirmed companion updates
  atomically: `TeamIndex`, `PrevTeamIndex`, `PLYR_PREVTEAMID`, both NIL
  fields zeroed), plus attribute setters for jersey/height/class/redshirt/
  position/ratings.
- **Validation layer** (`Validation/`) — eight named rules:
  `RequiredFields`, `DuplicateRowKey`, `RatingRange` (0–99 over the 57
  numeric rating columns), `EnumFields` (position/class/redshirt/jersey),
  `TeamAssignment` (range + optional membership in the save's team list),
  **`TeamChangeConsistency`** (the Group 4 multi-field dependency),
  **`IdentityChangeConsistency`** (rename-vs-replace intent enforcement),
  and `OpaqueFieldGuard` (blocks writes to `Weight` / `PLYR_COMMENT`).
  Anomalies already present in the source file downgrade to warnings so
  genuine EA exports — which contain blank-name placeholder rows — always
  remain exportable; the same anomaly introduced by an edit is an error.
- **Validating exporter** (`Export/`) — refuses to write when validation
  fails (with the full report in the exception) and returns a per-row list
  of changed columns as proof of what the edit touched.
- **Proof-of-concept console app** (`RosterGenerator.Poc`) — runs the full
  load → rename+jersey → validate → export → independent-diff pipeline
  against any `Player.csv`.
- **Documentation** — `docs/Architecture.md` (structure, data flow, design
  rationale) and `docs/Schema.md` (column-level ground truth: five
  empirically confirmed field groups plus a profiled appendix of all 286
  columns with assumptions explicitly labeled).

## Known unknowns

Tracked open research items — all are locked or out of scope in code, not
guessed at (details in `docs/Schema.md`):

1. **`Weight` encoding (Group 2).** NOT raw pounds; observed values
   (20–240) don't match real weights. Hypothesis: index/offset into a
   weight curve/spline (`Spline.csv` / `PositionSplineTable` tables in the
   export are candidate lookup targets) — plausible but unproven. Until
   resolved, the library exposes it read-only and `OpaqueFieldGuard`
   blocks changes. **Blocks correct weights in generated rosters.**
2. **Derived league-wide `Player[]` array tables (Group 5).** ~200 array
   tables reshuffled across dozens of teams from a two-player team change —
   evidence of game-recomputed sorted/indexed lists. Never loaded or
   written by this tool; treat any future need as a full-table recompute
   problem, not diff-and-patch.
3. **`PLYR_COMMENT` semantics.** Internal flavor-text/comment-pool index;
   changed spontaneously on one observed rename with no clear trigger.
   Policy: leave alone, never set.
4. **`PLYR_PREVTEAMID` native domain.** On transfers the real tool writes
   the old `TeamIndex` into it, but untouched saves carry values
   (1009–1164) far outside the team-index range (0–137) — its native ID
   space is unidentified. Also note the sentinel mismatch: `PrevTeamIndex`
   uses `255` for "none", `PLYR_PREVTEAMID` uses `0`.
5. **~250 unconfirmed columns.** Statistically profiled (type/range/enum
   values) in the Schema.md appendix but never verified by a controlled
   edit. None are written by the tool.
6. **Asset regeneration rules.** `PLYR_ASSETNAME` /
   `GenericHeadAssetName` / `PLYR_PORTRAIT` formats are observed but the
   generation algorithm (and which values are safe to synthesize for a
   replacement player) is unknown — currently the caller must supply
   values on a replace.

## Next recommended milestone

**Milestone 2 — Full-team replacement engine (the 2023-FSU scenario as a
library operation).**

Rationale: the provided `DYNASTY-2023FSU` export is a complete worked
example of the target operation — one team's roster fully replaced with a
real historical roster. Rebuilding that transformation with this library,
then diffing the output against the real `DYNASTY-2023FSU` export as
ground truth, is the highest-value next step: it exercises replace-identity
at scale, surfaces every field the FSU edit touched that the tool doesn't
model yet, and produces a reusable "replace a whole team" operation that
historical generation (Milestone 3) can drive.

Suggested scope:

1. **Weight decoding research** (prerequisite for correct weights):
   correlate the FSU players' known real weights against their `Weight`
   values and the `Spline` tables; confirm or refute the spline hypothesis.
   Unlock the field in code only if confirmed.
2. **Asset-field study**: diff `PLYR_ASSETNAME` / `GenericHeadAssetName` /
   `PLYR_PORTRAIT` between base and FSU exports across all ~85 replaced
   players to learn the regeneration pattern well enough to synthesize
   safe values (or to confirm the import tool regenerates them).
3. **Team-roster replacement operation**: `ReplaceTeamRoster(teamIndex,
   IReadOnlyList<HistoricalPlayer>)` built on the existing edit session —
   slot matching, jersey/height/class/redshirt/ratings application, and
   validation of the whole team in one report.
4. **Roster-source abstraction**: define the `HistoricalPlayer` input model
   (CSV/JSON to start) so Milestone 3 can plug in real data sources without
   touching the engine.
5. **Acceptance test**: reproduce the 2023 FSU roster from the base save
   and diff against the real `DYNASTY-2023FSU` export; every unexplained
   difference becomes either a new schema fact or a documented gap.

Explicitly still deferred: rating *generation*, equipment/face mapping,
GUI, scraping, dynasty editing, and the derived-array recompute.
