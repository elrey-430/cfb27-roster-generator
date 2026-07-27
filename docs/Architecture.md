# Historical CFB27 Roster Generator — Architecture

The system is built in three layers, delivered across three milestones:

1. **Foundation (Milestone 1)** — byte-faithful CFB27 `Player.csv` I/O,
   a typed player model, intent-recording edits, named validation rules,
   and a validating exporter.
2. **Historical pipeline (Milestone 2)** — a platform-independent
   historical roster model, external team/position mapping systems, and a
   converter that replaces one team's players inside a donor save.
3. **Generalized end-user workflow (Milestone 3)** — dynasty-export
   discovery (any user's save), a simple spreadsheet-style input CSV,
   team/season selection, and standard `Output/` generation, with the 2023
   FSU recreation preserved as a byte-stable regression test.
4. **Rating generation (Milestone 4)** — historical evidence becomes a
   complete attribute set, with the overall computed by EA's own formulas
   (solved backwards to hit an intended overall), confidence scores, and
   sanity guardrails. See `Ratings/`.

## The three pipelines (Milestone 3 view)

### Input pipeline

```
user dynasty export folder            simple historical CSV
      │ DynastyExport.Open                  │ HistoricalCsv.Read
      │  – finds the Player table           │  – case-insensitive headers
      │    by content (_tableName)          │  – heights "6-2" or inches
      │  – finds the main Team table        │  – per-row user-facing warnings
      │  – lists available teams            │  – Team/Season from file or
      ▼                                     ▼    caller (interactive prompt)
PlayerRoster (donor)                  HistoricalRoster
```

Teams and ids always come from the **user's own dynasty**; `data/TeamMappings.json`
is only an optional alias overlay (e.g. "FSU"), filtered to teams that
actually exist in the loaded save. Nothing is keyed to any particular
dynasty file.

### Conversion pipeline

```
HistoricalRoster + PlayerRoster
      │ HistoricalTeamConverter.Convert (via RosterEditSession)
      │  – school → TeamIndex through the dynasty-derived mappings
      │  – position normalized through data/PositionMappings.json
      │  – slot assignment: same position → interchangeable group → any
      │  – class → SchoolYear + RedshirtStatus; weight → pounds − 160
      │  – missing values inherit the replaced player's values (reported)
      │  RatingEngine (when enabled)
      │  – evidence → target overall (+ confidence and reasons)
      │  – position baseline → talent sensitivity → physique
      │    → verified measurements (locked) → experience
      │  – calibrate against EA's own overall formula, then sanity caps
      │  – roster pass: backups held below starters
      ▼
edited PlayerRoster + ConversionReport
```

### Output pipeline

```
edited PlayerRoster + edit session
      │ RosterValidator (9 named rules; errors block the export)
      │ RosterExporter  (byte-faithful write; per-row change accounting)
      ▼
Output/Generated_Roster.csv  +  Output/Generation_Report.txt
```

---

# Milestone 1 foundation (reference)

## Purpose of this milestone

Prove the foundation: load a CFB27 `Player.csv` export, modify one player in
a controlled manner, validate the result (including the confirmed
multi-field dependencies), and export a CSV the existing community roster
import tool will accept. Historical roster *generation* is deliberately not
built yet — but every module boundary below was drawn so that Milestone 2
can add it without reworking this layer.

## Technology choices

| Decision | Rationale |
|---|---|
| C# / .NET 8 | Requested target; first-class Windows 10/11 support |
| Self-contained single-file publish | End users need no Python/Node/Docker/WSL/.NET install — `dotnet publish -r win-x64 --self-contained -p:PublishSingleFile=true` produces one `.exe` (verified working, ~67 MB) |
| **Zero external dependencies** in `RosterGenerator.Core` | The CSV dialect is trivial (no quoting, CRLF) but the *round-trip guarantee* is critical; a third-party CSV library that normalizes quoting/line endings would silently break byte-fidelity. Test projects use xunit (dev-time only) |
| No GUI in this milestone | Core is a class library; the PoC is a console app. A future GUI (or the existing Electron suite) sits on top of the same library |

## Project structure

```
cfb27-roster-generator/
├── RosterGenerator.sln
├── docs/                        ← Architecture, Schema, Status, Historical_CSV_Format
├── data/                        ← editable data files (shipped next to the exe)
│   ├── TeamMappings.json            optional school-alias overlay
│   ├── PositionMappings.json        position aliases + interchangeability groups
│   ├── OverallFormulas.json         EA's 79 overall formulas (authoritative)
│   ├── RatingModels.json            evidence curves, caps, production emphasis
│   ├── ArchetypeProfiles.json       GENERATED: what the game gives each of the
│   │                                59 archetypes, per attribute, per overall
│   ├── ArchetypeRules.json          which archetype a player's profile implies
│   ├── RosterDepth.json             roster shape and program standing
│   ├── EquipmentEras.json           period-correct helmets, masks, sleeves, pads
│   ├── FbsMembership.json           when each school reached the FBS (advisory)
│   └── RosterSkeleton.json          MEASURED: the league-mean layout of a
│                                    team's 85 slots, by position
├── Ratings/                     ← rating model documentation + test results
├── tools/                       ← measurement scripts that generate data/ files
│   └── build_archetype_profiles.py  fits every archetype's every attribute
│                                    against overall across a real export
├── templates/
│   └── HistoricalRosterTemplate.csv ← the user-facing input template
├── HistoricalData/2023/FloridaState.json  ← curated example dataset (JSON form)
├── Tests/                       ← 2023 FSU regression fixtures (input, donor, expected)
├── Output/                      ← generated deliverables
├── src/
│   ├── RosterGenerator.Core/    ← the reusable library (no GUI, no I/O policy)
│   │   ├── Csv/                 ← byte-preserving CSV parse/serialize
│   │   ├── Schema/              ← empirical knowledge as code (columns, enums, bounds)
│   │   ├── Model/               ← Player / PlayerRoster typed views + change tracking
│   │   ├── Editing/             ← intent-recording mutations (rename/replace/transfer)
│   │   ├── Validation/          ← 9 named rules (State vs ChangeDriven)
│   │   ├── Export/              ← validate-then-write with per-row change proof
│   │   ├── Historical/          ← HistoricalPlayer/Roster model + simple-CSV reader,
│   │   │                          RosterCsvValidator, FbsMembership,
│   │   │                          SeasonTemplateWriter (blank whole-season file)
│   │   ├── Mapping/             ← TeamMappingSet / PositionMappingSet (external files)
│   │   ├── Dynasty/             ← DynastyExport: discover tables/teams in any export
│   │   ├── Conversion/          ← HistoricalTeamConverter + ConversionReport (+ClassYear)
│   │   └── Comparison/          ← RosterComparer (generated vs benchmark)
│   ├── RosterGenerator.Cli/     ← end-user commands: generate / template /
│   │                              validate / list-teams / compare
│   └── RosterGenerator.Poc/     ← Milestone 1 proof-of-concept (rename + jersey)
└── tests/
    └── RosterGenerator.Core.Tests/  335 xunit tests + real-data fixtures
```

Parsing (`Csv/`), business logic (`Editing/`), validation (`Validation/`)
and export (`Export/`) are independent components, as required: each folder
depends only on the layers above it in the data flow below, and none of
them knows about a UI.

## Data flow

```
input Player.csv
      │  CsvDocument.Load            (raw string cells, byte-faithful)
      ▼
PlayerRoster                          (typed Player views + load-time snapshot)
      │  RosterEditSession            (RenamePlayer / TransferPlayer / ... ,
      │                               records EditIntent per row)
      ▼
RosterValidationContext               (roster + edit intents + known teams)
      │  RosterValidator              (8 named rules; errors block, warnings inform)
      ▼
RosterExporter.Export
      │  writes CsvDocument back      (only deliberately edited cells differ)
      ▼
output Player.csv  +  ExportResult    (per-row list of changed columns = proof)
```

## Key design decisions and why

### 1. Byte-preserving raw-cell model (the load-bearing decision)

Only ~30 of the 286 player columns have empirically confirmed semantics.
Any representation that parses every column into typed values must also
re-serialize the 250+ unknown columns correctly — a huge, unverifiable
surface. Instead, `CsvDocument` stores every cell as the exact string from
the file, and `Player` is a *view* that parses on read and formats on
write. Consequences:

- An unedited file re-exports **byte-identical** (asserted by tests and
  verified against the full 16,500-row real export).
- The exported diff can only contain deliberately edited cells — exactly
  the compatibility contract the community import tool expects.
- Unknown columns survive untouched no matter what future game patches add.

### 2. Load-time snapshot → change tracking → change-aware validation

`PlayerRoster` snapshots all cells at load. This enables:

- `GetChangedColumns(row)` — the exporter's proof of what an edit touched.
- Validation of *transitions*, not just states: `TeamChangeConsistency`
  fires on a `TeamIndex` change whose companion fields are stale — a check
  that is impossible with only the final value in hand.

### 3. Explicit edit intent instead of inference

The empirical findings show "rename" and "replace with a different real
player" are both legitimate but require opposite handling of
`PLYR_ASSETNAME` / `GenericHeadAssetName` / `PLYR_PORTRAIT`. The tool
cannot guess which one the user means, so `RosterEditSession` records an
`EditIntent` per operation and the `IdentityChangeConsistency` rule
cross-checks the actual cell changes against the declared intent. Editing
cells directly (bypassing the session) still works but is flagged by
validation as an undeclared identity change — safe by default, never
silently wrong.

### 4. Multi-field operations are single methods

`TransferPlayer(player, newTeam)` applies all five confirmed companion
updates atomically (`TeamIndex`, `PrevTeamIndex`, `PLYR_PREVTEAMID`, both
NIL fields zeroed). Users of the library cannot forget half of a transfer;
the validation rule exists to catch edits made outside the session.

### 5. State rules vs change-driven rules ("validate the delta strictly, the baseline leniently")

Running the validator against the untouched real base save surfaced two
live rows with blank names that EA's own engine wrote. A file the game
itself produced must always remain exportable, so rules are classified:

- **State rules** (required fields, rating range, enums, team range,
  duplicate keys): findings whose cells are unchanged since load are
  downgraded to warnings, annotated "pre-existing in the source file".
- **Change-driven rules** (team-change consistency, identity-change
  consistency, opaque-field guard): fire only on deltas and are never
  downgraded — for them, an *unchanged* cell is often exactly the bug.

### 6. Unresolved encodings are locked, not guessed

`Weight` (not pounds; suspected spline index) and `PLYR_COMMENT` (internal
pool index) have no setter on `Player`, and the `OpaqueFieldGuard` rule
blocks any change to them. The derived `Player[]` league-wide array tables
are simply never loaded. All three are documented open research items in
`Schema.md` — flagged, not solved, per the milestone's non-goals.

### 7. Errors are messages, not silence

Every failure path — unparseable file, ragged row, missing column, blocked
export — throws a typed exception (`CsvSchemaException`,
`RosterExportException`) carrying a human-readable explanation, and
`RosterExportException` carries the full `ValidationReport` so a caller can
show every issue at once. `RosterExporter` writes nothing when validation
fails.

## Major classes (public API surface)

| Class | Responsibility |
|---|---|
| `CsvDocument` | Parse/serialize one CSV table; raw cell access by (row, column name) |
| `PlayerRoster` | Player-table semantics: required columns, live vs empty rows, `_row` lookup, snapshot/change tracking |
| `Player` | Typed accessors for confirmed fields; raw access for the rest |
| `RosterEditSession` | The supported mutation API; records `EditIntent` per operation |
| `RosterValidator` / `IValidationRule` | Named, independent rules; pre-existing-anomaly downgrade |
| `RosterExporter` | Validate → write → report exactly which cells changed |

## Extension points for later milestones

- **New roster sources** (the "historical" part): a generator produces
  `RosterEditSession` operations against a loaded roster — the session/
  validation/export pipeline is already source-agnostic.
- **New tables**: `CsvDocument` is table-agnostic; a `Team` or `Recruit`
  model class can wrap it the same way `PlayerRoster` does.
- **New validation rules**: implement `IValidationRule`, add to the rule
  list; rules are independent and composable.
- **Weight decoding** (Milestone 2 research): once resolved, replace the
  `OpaqueFieldGuard` lock with a typed accessor + spline lookup component.
- **GUI**: any front end (WPF/WinUI/console/Electron bridge) can consume
  `RosterGenerator.Core` — the library performs no console I/O.

## Verification status (what was actually proven)

- 335 xunit tests pass, covering round-trip fidelity, every validation rule,
  both multi-field dependency rules, the archetype floors, the equipment and
  appearance passes, whole-season templating and conversion, and the full
  PoC pipeline.
- The PoC ran against the real 16,500-row `DYNASTY-JUL24-BASE` export:
  renamed `_row=5098` (Bray Hubbard → Charlie Ward) and set jersey 18→17.
  An independent `diff`/`cmp` of input vs output confirmed **exactly one
  line changed, in exactly the `FirstName`, `LastName`, `JerseyNum` cells**;
  `PLYR_ASSETNAME` et al. correctly retained the original identity values,
  matching observed real-tool rename behavior.
- Re-diffing the provided base vs multi-edit exports reproduced every
  empirical finding the schema is built on (see `Schema.md`, Provenance).
