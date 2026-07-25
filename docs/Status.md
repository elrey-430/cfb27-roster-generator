# Project Status

_Last updated: 2026-07-25 — end of Milestone 5; Milestone 6 planned._

## Current status

**Milestone 5 (Archetypes, hometowns, roster size) is complete.** Two more
fields were investigated and cleared for writing, closing the largest
remaining realism gaps.

- **`PlayerType` (archetype) — writable, with a companion dependency.** A
  manually edited save proved the write takes, but also exposed the trap:
  the archetype selects which of EA's overall formulas applies, and the
  community editor does not recompute the overall afterwards. Only **56%**
  of that save's 85 edited players have an overall matching their own
  archetype (35 match a *different* one) against **99.3%** in an untouched
  base save. This tool selects an archetype from each player's profile
  (`data/ArchetypeRules.json`) and always recomputes with the new formula;
  the `ArchetypeConsistency` rule reports the defect in any file with it.
  The same save also carries an LOLB with `MLB_PassCoverage` — an archetype
  invalid for the position — which the rule now catches too.
- **`PLYR_HOME_TOWN` / `PLYR_HOME_STATE` — writable.** Town is free text;
  state is a strict **51-value enum** (50 states in PascalCase plus
  `NonUS`). The `Hometown` column now writes both, accepting `FL`,
  `Florida` or `West Virginia`, and mapping anything non-US to `NonUS`
  with a note.
- **Roster size** is reported more usefully: the warning now says how many
  leftover original players rate 75+ and could out-rank the historical
  roster on the depth chart.
- Tests: 118/118. The FSU regression fixture was deliberately regenerated
  once (the diff was confined to the two hometown columns and nothing else).

**Milestone 4 (Automated ratings & attribute generation) is complete.** A
user can supply a name, position, height, weight and whatever historical
performance data they have, and receive a complete CFB27 player — all 56
attributes plus an overall — with no manual editing.

- **The overall rating is EA's own formula, not an invention.**
  `data/OverallFormulas.json` holds 79 formulas covering all 21 positions
  and all 59 archetypes. Verified independently against a full dynasty
  export: **99.33% exact** (16,148/16,257 players), 99.90% within one
  point. Because the formula is linear, the engine solves it *backwards* to
  hit an intended overall exactly — so the overall written always agrees
  with the attributes written.
- **Transparent evidence model** (`data/RatingModels.json`) — draft slot
  (0.34), awards (0.26), production (0.22), recruiting stars (0.10),
  depth-chart role (0.08), each expressed directly on the overall scale.
- **Confidence + reasons** on every player (High/Medium/Low with the
  signals that fired), surfaced in `Generation_Report.txt`.
- **Verified measurements win and stay put:** a timed 40 sets speed exactly
  (4.30→99, 4.40→96, 4.50→92) and calibration may never move it.
- **Guardrails:** OL speed capped at 72, K/P tackling at 45, class-year
  awareness caps (a Heisman-winning redshirt freshman still tops out at 78
  awareness), low-confidence freshman overall cap, and a depth-chart rule
  keeping backups below starters unless a draft slot or major award
  justifies it.
- Deliverables: `Ratings/Rating_Model.md`, `Ratings/Position_Formulas.md`,
  `Ratings/Default_Assumptions.md`, `Ratings/Player_Test_Results.csv`, and
  `Tests/Ratings_test.csv` (standalone 2015 Dalvin Cook case).
- Tests: 86/86 passing. The Milestone 3 FSU regression remains byte-stable
  (it pins `--ratings inherit`, testing the conversion layer).

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

### Milestone 5 — archetypes and hometowns

- **`ArchetypeSelector`** (`data/ArchetypeRules.json`) — per-position rules
  keyed to the real archetype names (LT/RT use `OT_*`, LG/RG `G_*`, …),
  with thresholds derived from each archetype's attribute medians in a real
  export. First matching rule wins; a condition whose field is missing never
  matches, so a player with no data falls through to the position default.
- **`Hometown`** parser — "City, ST" → free-text town plus the state enum.
- **`ArchetypeConsistencyRule`** — flags archetypes invalid for the position
  and archetype changes made without recomputing the overall. Keyed to
  `PlayerType` so an edit is an error while a defect already present in a
  loaded file stays a warning.
- **CLI** `--archetypes select|inherit`; selecting requires
  `--ratings generate`, because the two must move together.

### Milestone 4 — rating generation

- **EA overall formulas** (`Rating/OverallFormulaSet.cs`) — loads the 79
  supplied formulas, computes overall with EA's half-down rounding, and
  inverts them in closed form to calibrate attributes to a target overall.
- **Evidence model** (`Historical/RatingEvidence.cs`) — role, star rating,
  combine numbers, draft slot, awards and 26 statistics, all optional and
  all additive to the golden-standard template CSV.
- **Talent scorer** (`Rating/TalentScorer.cs`) — weighted blend of the
  available signals with per-signal explanations; partial stat lines scale
  their own weight down; derived stats (completion %, YPC, FG%) computed
  automatically.
- **Rating engine** (`Rating/RatingEngine.cs`) — position baselines →
  talent sensitivity → physique (reference sizes derived from real save
  medians) → verified measurements (locked) → experience shift →
  sensitivity-weighted calibration → sanity caps.
- **Depth consistency** (`Rating/DepthConsistency.cs`) — roster-level pass
  holding backups below starters, with a narrow justification exception.
- **CLI** `--ratings generate|inherit` (generate is the default) and a
  ratings section in the generation report.

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

**Milestone 6 — Roster completion and fidelity benchmark.**

Everything through Milestone 5 makes each *individual* player right. What
remains is making the roster **as a whole** right, and proving it with a
measured number rather than an impression.

### 1. Roster-size policy — highest impact, and the only item felt in-game

When the historical roster has fewer players than the team's slots, the
leftover fictional players stay (10 in the FSU run). The player table has
no depth-chart column — `DepthChart` / `DepthChartConfig` are team-level,
and the game builds its two-deep from ratings. So a leftover 82-OVR
fictional quarterback simply takes the job from the historical starter,
and every rating decision made upstream (EA's own overall formula,
calibrated attributes, selected archetypes) is overridden at the position
that matters most.

Implement a policy instead of only reporting the count. In preference
order:

1. **Fill from the dataset** — take remaining slots from the historical
   roster's walk-ons and scout-team players. Best fidelity; requires the
   input CSV to carry them.
2. **Rate leftovers to a floor** so they cannot crack the two-deep. Safe,
   no schema risk, no new confirmed fields needed.
3. **Deactivate the slot** — highest fidelity, but depends on confirming
   the `_isEmpty` pool-slot mechanics first (243 such rows exist in the
   base save). That is genuine research, not a configuration change, and
   should not be attempted casually.

### 2. Benchmark scoring against the manual 2023 FSU export

`Comparison/RosterComparer.cs` was built for this and has never been used
for it. Run generated-vs-manual field by field and resolve every
difference into exactly one of three outcomes: a data fix, a newly
confirmed schema fact, or a documented gap.

This is the method that has already paid: independently recomputing
overalls caught a CLI wiring failure that had silently disabled the
rating engine, and an overall/archetype coherence test caught the
community editor's missing recompute. It also produces a single fidelity
percentage to track across future changes. Resolve the ~25 "verify"
jersey/status flags in `HistoricalData/2023/FloridaState.json` in the same
pass.

### 3. `PreviousSchool` target column — timeboxed

The field is collected, stored and reported, but never written, because no
target column is confirmed. The transfer-related columns
(`PLYR_PREVTEAMID`, `PrevTeamIndex`) are partly mapped already, so a
bounded investigation is reasonable. If it does not resolve quickly,
document the finding and move on — do not guess a column.

### Deliberately *not* next: the asset-field study

`PLYR_ASSETNAME` / `GenericHeadAssetName` / `PLYR_PORTRAIT` remain
unmapped (Known unknown 6), so replaced players still wear the donor
players' faces. That is worth fixing eventually but is the wrong trade
now: it is purely cosmetic, most inherited heads are generic to begin
with, and the payoff requires reverse-engineering a generation algorithm —
high effort, uncertain outcome, and adjacent to the reverse-engineering
scoped out in the Milestone 1 brief. Leftover starters are a functional
defect; borrowed faces are not.

Explicitly still deferred: GUI polish, automatic historical data
gathering, equipment/face generation, multi-season bulk generation,
dynasty editing, and the derived-array recompute.
