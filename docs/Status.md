# Project Status

_Last updated: 2026-07-24 — end of Milestone 3._

## Current status

**Milestone 3 (Generalized historical roster pipeline) is complete.** The
generator is now a general-purpose end-user tool: it works with any
compatible dynasty export, takes a simple spreadsheet-style roster CSV, and
needs no programming, schema, or database-id knowledge from the user.

- **No dynasty-specific dependency.** `DynastyExport.Open` discovers the
  Player table and the main Team table by content (`_tableName`) in the
  user's own export, and derives the available teams and their ids from
  that save. `data/TeamMappings.json` is now only an optional alias
  overlay, filtered to teams that actually exist in the loaded dynasty.
- **Simple user-facing input.** `FirstName,LastName,Position,Number,
  Height,Weight,Class,Team,Season` (+ optional `Hometown`,
  `PreviousSchool`, `Notes`); case-insensitive headers, any column order,
  heights as `6-2` or `74`, classes as `RS Junior`; only the first three
  fields are required. Template: `templates/HistoricalRosterTemplate.csv`.
- **Team/season selection.** `list-teams` enumerates the dynasty's teams;
  `generate` takes `--team`/`--season`, reads them from the CSV, or prompts
  interactively with a numbered list.
- **Standard output.** `Output/Generated_Roster.csv` +
  `Output/Generation_Report.txt` (plain text: processed/mapped counts,
  missing fields, defaults used, warnings).
- **Regression-protected.** The 2023 FSU recreation is now an automated
  byte-stability test over committed fixtures (`Tests/2023_FSU_Input.csv`,
  `Tests/DonorDynasty/`, `Tests/2023_FSU_Expected_Output.csv`).
- Tests: 65/65 passing. Standalone `win-x64` single-file publish verified,
  shipping `data/` and `templates/` alongside the executable.

**Milestone 2 (Historical Roster Pipeline & 2023 Florida State recreation)
is complete.** The pipeline independently generated a complete 2023 FSU
roster from a public-information dataset and exported it as a
CFB27-compatible full `Player.csv`:

- `HistoricalData/2023/FloridaState.json` — 75-player dataset compiled from
  public sources (seminoles.com, Tomahawk Nation, ESPN, SI/247Sports), not
  from any dynasty export
- `Output/2023_Florida_State_CFB27.csv` — the deliverable: the base save's
  player table with FSU's roster replaced (75 rows changed, all on team 27,
  only the seven confirmed-safe columns touched; byte-verified)
- `Output/2023_Florida_State_Report.md` — the generated validation report
  (counts, missing fields, defaults, assumptions, warnings)
- `dotnet run --project src/RosterGenerator.Cli -- generate|compare ...` —
  repeatable for any team/season given a dataset and the mapping files
- Tests: 50/50 passing

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

### Milestone 3 — generalized end-user pipeline

- **Dynasty import** (`Dynasty/DynastyExport.cs`) — opens an export folder
  (searched recursively) or a lone Player CSV; identifies the Player table
  by its required columns and the main Team table by row count (the export
  contains several decoy single-row Team tables); exposes the discovered
  teams and builds the school-name lookup from them.
- **Simple historical CSV reader** (`Historical/HistoricalCsv.cs`) —
  tolerant header matching, feet-inches or plain-inch heights, per-row
  user-facing warnings instead of hard failures, caller-supplied
  team/season overriding file columns.
- **Roster template** (`templates/HistoricalRosterTemplate.csv`).
- **Plain-text generation report** (`ConversionReport.ToText`) alongside
  the existing Markdown renderer.
- **CLI workflow** (`RosterGenerator.Cli`) — `generate` (with interactive
  team/season selection when not supplied), `list-teams`, `compare`;
  defaults to `Output/Generated_Roster.csv` +
  `Output/Generation_Report.txt`; friendly single-line error messages.
- **2023 FSU regression test** (`FsuRegressionTests`) — regenerates from
  the committed simple-CSV input and donor dynasty and asserts the output
  is byte-identical to the committed expected file.

### Milestone 2 — historical pipeline

- **Historical data model** (`Historical/`) — platform-independent
  `HistoricalPlayer`/`HistoricalRoster` JSON model (season, school, name,
  position, jersey, height, weight, class year + optional hometown,
  previous school, notes); every non-identity field may be missing.
- **Team mapping system** (`data/TeamMappings.json`) — external
  alias→TeamIndex file generated from the save's own Team table (138 teams);
  no team ids are hard-coded; lookups are case/punctuation-insensitive.
- **Position mapping system** (`data/PositionMappings.json`) — external
  alias→CFB27-position file (Tailback→HB, Cornerback→CB, ...) plus
  interchangeability groups (LT/LG/C/RG/RT, LE/RE, LOLB/MLB/ROLB, FS/SS).
- **Historical→CFB27 converter** (`Conversion/`) — replaces one team's
  players inside a donor save via replace-identity edits: writes the
  confirmed-safe fields, inherits donor values for anything missing or
  unresolved (ratings, Weight, identity assets), assigns slots
  position-first with group fallback, and records every default and
  assumption in a Markdown `ConversionReport`.
- **Class-year parser** — "Redshirt Junior"/"RS Fr"/"Graduate" →
  `SchoolYear` + `RedshirtStatus` pairs.
- **Comparison utility** (`Comparison/RosterComparer.cs`) — field-by-field
  team comparison between two Player tables (name-matched, configurable
  column set, Markdown report) ready for the generated-vs-manual FSU
  benchmark.
- **CLI** (`RosterGenerator.Cli`) — `generate` and `compare` commands.

### Milestone 1 — foundation

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
  and `OpaqueFieldGuard` (blocks writes to `PLYR_COMMENT`; it also locked
  `Weight` until the encoding was confirmed — now range-checked by
  `WeightRange` instead).
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

1. ~~**`Weight` encoding (Group 2).**~~ **RESOLVED (2026-07):** stored
   value = pounds − 160 (range 160–400 lb), confirmed by correlating the
   manually edited 2023 FSU save against real listed weights and by
   league-wide decoded position averages. The spline hypothesis is
   refuted. Writable via `Player.WeightPounds`; the converter now writes
   real weights. See Schema.md Group 2 for the evidence.
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

**Milestone 4 — Realism: ratings generation and identity assets.**

The pipeline is now general and reliable; the remaining gaps are about how
*convincing* a generated roster is once it is in the game.

1. **Ratings generation** — the biggest realism gap. Generated players
   currently inherit the ratings of whoever they replaced, so a walk-on
   can carry a star's ratings and vice versa. Drive ratings from the
   historical data the user already supplies (position, class, and
   optional star rating / usage tier / stats), with a documented,
   tunable model rather than a black box.
2. **Roster-size handling** — when the historical roster has fewer players
   than the team's slots, leftover fictional players remain (10 in the FSU
   run). Decide and implement a policy (leave, deactivate, or fill from
   the historical dataset's walk-ons) rather than only reporting it.
3. **Asset-field study** — learn how `PLYR_ASSETNAME` /
   `GenericHeadAssetName` / `PLYR_PORTRAIT` are generated so replaced
   players stop wearing the donor players' faces.
4. **Benchmark scoring** — run `compare` against the manually created 2023
   FSU dynasty export and turn each difference into a data fix, a new
   schema fact, or a documented gap. (Also resolve the ~25 "verify"
   jersey/status flags in `FloridaState.json`.)
5. **Hometown/previous-school export** — confirm whether
   `PLYR_HOME_TOWN` / `PLYR_HOME_STATE` are safe to write, so the optional
   CSV columns stop being decorative.

Explicitly still deferred: GUI polish, automatic historical data
gathering, equipment/face generation, multi-season bulk generation,
dynasty editing, and the derived-array recompute.
