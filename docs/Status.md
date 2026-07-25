# Project Status

_Last updated: 2026-07-25 — end of Milestone 9._

## Current status

**Milestone 9 (Period-correct equipment) is complete.**

- **Where equipment lives.** Not in the Player table. A controlled experiment
  — one dynasty exported twice, differing only in eight Florida State
  cornerbacks' helmets — changed exactly **one file out of 2,273**:
  `0130_CharacterVisuals.csv`. Full decode in `docs/Schema.md`, Group 6.
- **The link.** The Player table's `CharacterVisuals` column is a packed
  32-bit reference: low 16 bits are the visuals row, high 16 a constant
  `8452` table tag. Decoding it for all 16,500 players recovered exactly the
  eight edited cornerbacks and nothing else.
- **The edit.** Helmet (`slotType: HeadWear`) and face mask
  (`slotType: FaceMask`) are replaced by targeted string substitution inside
  the JSON blob — both patterns occur exactly once per row across all 12,156
  rows carrying a helmet — so every other byte survives. The two are always
  written together, because a mask is moulded to a shell.
- **The user surface.** The season already being recreated picks the era. No
  new column, no new question. A second file, `Generated_Equipment.csv`, is
  written and must be imported alongside the roster.
- **The acceptance test.** Our output is **byte-identical** to what the
  community editor produced for the three rows it edited without also
  filling in unrelated Head-loadout defaults. That is a stronger bar than any
  previous milestone had.
- **Known limit — the catalogue.** Retro helmets appear on *zero* of 12,586
  players in a base save, so the period vocabulary cannot be mined and must
  be demonstrated in the editor one helmet at a time. Confirmed so far:
  Revolution Speed, Revolution, Air XP. Only 2010–2016 is defined, and a
  season no era covers changes nothing.
- **Brand carries over, the model changes.** A player's current helmet names
  a manufacturer and the era moves them to that manufacturer's model, so a
  squad stays mixed rather than collapsing into 85 identical helmets. Brands
  that did not exist yet (Vicis, Light) take the era's fallback. Six of the
  eight demonstrated edits follow this exactly; the two that do not
  (Howard, Schutt → Riddell; Lester, Light → the 2000s Revolution) are
  recorded in `docs/Schema.md` as open questions rather than fitted to.
- **Masks follow position.** Mined from the base save: the game puts a kicker
  cage on 92–98% of kickers and punters, a cage or heavy bar on linemen, an
  open two-bar on quarterbacks. The engine now selects by role, with a
  deterministic pool for spreading masks across a line. Retro shells have only
  had their two-bar demonstrated, so they still give everyone that one — the
  highest-value gap left.
- **Sleeves and shoulder pads** are era-wide slots alongside the helmet.
  Confirmed: `Gear_JerseyStyle_SleeveTight`/`_SleeveStandard`/`_RolledLow`
  and `Small_Pads`/`Medium_Pads`/`Large_Pads`.
- **A second key-order trap, caught before it shipped.** The exporter writes
  `itemAssetName` and `slotType` in *either* order — 12,570 rows one way, 16
  the other — so any pattern spanning both keys would have missed almost
  every row. All slots are matched on the value's own prefix instead.
- **Five eras live**: 2010–2016, 2000–2009, 1990–1999, 1980–1989 and
  pre-1980, every asset in them read out of a demonstration export. A second
  round of 17 player edits supplied the retro vocabulary that could not be
  mined: the VSR-4 (`GearHelmet_standardBrady`), the TK
  (`GearHelmet_RiddellTK`), the Schutt Air Advantage (`GearHelmet_Schutt`,
  distinct from `GearHelmet_AirXP`), four vintage masks, per-role Revolution
  and Revolution Speed masks, long sleeves and X-Large pads.
- **The 2000s split by model** on the research: the Revolution arrived in 2002
  and was on 83% of NFL players by 2008, while the VSR-4 it replaced stayed in
  college use through 2010 — so a SpeedFlex wearer takes a Revolution and an
  Axiom wearer a VSR-4.
- **Asset names are not UI labels**, which is a trap worth remembering: the
  VSR-4 is `standardBrady`, and the shell the editor calls "Schutt Air XP" is
  the real-world Air Advantage and a *different asset* from the Air XP Pro
  VTD. A test pins every name in the data file to the demonstrated set so a
  typo cannot reach a user as a broken helmet model.
- Tests: 272/272.

**Milestone 8 (Draft slot measures the wrong season) is complete.**

- **The defect.** Draft position is the heaviest signal in the model and the
  only backward-looking one: it records where the NFL took a player months
  later, which is a different question from how they played in the season
  being recreated. An injury, a position the league does not value or a bad
  combine all move it without moving anything that happened on the field.
- **The fix.** When a draft slot sits more than 6 points below the
  contemporaneous evidence (awards, production), its weight drops to 35% and
  the report says why. It is not discarded — a late pick is still
  information — it just stops outvoting the season itself. The rule is
  narrow: it fired on 3 of 75 Florida State players.
- **`AwardContender`.** A new optional column for awards a player was in
  contention for without winning, scored from the same vocabulary 5 points
  lower. A Heisman finalist therefore out-rates a first-team all-conference
  winner, which is the right ordering. It is often the only evidence left
  when a season ends early.
- **A data error the work exposed.** Jordan Travis *won* the 2023 ACC
  Player of the Year and Offensive Player of the Year, and finished fifth in
  Heisman voting; the dataset had him at first-team all-conference, two
  tiers low. Corrected. He now generates at **88, up from 83**, led by the
  award rather than his draft slot.
- **Fidelity: 2.07** (was 2.01), still inside the 3.00 bar and better than
  the human recreation's 3.02. The metric measures agreement with the shape
  of EA's *generic* Florida State roster, and 2023 was an unusually
  top-heavy team — ten NFL draft picks — so rating its best player higher
  moves away from the generic curve on purpose.
- Tests: 229/229. Shipped as
  [v0.2.0-alpha](https://github.com/elrey-430/cfb27-historical-rosters/releases)
  from the distribution repository, which builds and tests on Windows before
  packaging.

**Milestone 7 (Ship it) is complete.** The tool is no longer something only
this repository can run.

- **A desktop app.** `RosterGenerator.Gui.exe` — pick the dynasty folder, pick
  the roster CSV, confirm team and season, click Generate. The roster file is
  checked the moment it is chosen, so problems appear before anything is
  written. Built with Avalonia; WinForms and WPF cannot build on this Linux
  SDK, so choosing them would have meant shipping code that was never compiled
  or run here.
- **A `validate` command.** Checks a roster CSV on its own and writes nothing.
  Held to the standard that makes a validator worth having: over a corpus of
  fifteen real mistakes, "ready to generate" must mean generation succeeds,
  a blocking verdict must mean it fails, every player generation skips and
  every value it rejects must have been flagged first, and a clean file must
  produce no warnings at all. Building those tests found two defects in the
  validator itself.
- **One pipeline, two front-ends.** `RosterGenerationService` holds every
  decision that shapes a roster; the command line and the desktop app only ask
  questions and display answers. They cannot grow different behaviour.
- **A release.** `./build-release.sh` produces two self-contained
  `win-x64` executables (~67 MB and ~74 MB) with `data/` and `templates/`
  beside them and a quick start, zipped and ready to hand to someone. This is
  what the Milestone 1 brief asked for and no build had ever produced.
- Tests: 221/221, including the window building and showing under Avalonia's
  headless platform. The 2023 Florida State deliverable still regenerates
  byte-identically after the pipeline was extracted.

**Milestone 6 (Roster completion and fidelity benchmark) is complete.** The
generator now produces a whole believable roster rather than a believable
set of individuals, and there is a measured number saying so.

- **The 85th player problem is solved.** A CFB27 team always carries 85
  players; a researchable historical roster is the two-deep plus whoever
  else is documented. The leftover slots kept EA's fictional players, three
  of whom out-rated the historical Florida State roster. `RosterDepth.json`
  measures what end-of-roster depth actually looks like — the median overall
  at each of the 85 roster ranks and the class mix at each depth, across 138
  untouched FBS rosters — and the filler gives an unfilled slot what the game
  itself puts at that rank, held below the weakest historical player at the
  position. Names, jersey numbers and portraits are untouched.
- **`PLYR_PREVTEAMID` decoded.** It is a school's `TEAM_ORIGID`, not a team
  index — which is why its values (1009–1235) never matched the team range.
  133 of 135 distinct non-zero values resolve to a team in the same save.
  `PreviousSchool` is now written.
- **Two rating defects found by benchmarking and fixed.** The game's best
  punter is an 86 and its best receiver a 99, but both drew on one award
  scale, so a nation-leading All-American punter generated at 91 — better
  than any punter in the game. And role, awards and statistics record what a
  player did, never *where*, so an anonymous backup was rated identically at
  a playoff program and at the worst team in the country.
- **Fidelity score: 2.01.** Mean absolute deviation from the roster shape
  the game itself ships for Florida State, rank by rank — down from 4.48,
  and inside the 3.02 scored by the hand-built human recreation. Pinned by
  `RosterFidelityTests`. See `Ratings/Benchmark_2023_FSU.md`.
- Tests: 136/136.

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

### Milestone 7 — shipping

- **`RosterGenerationService`** (`Core/Pipeline/`) — the whole pipeline in one
  place: open the dynasty, read the roster, convert, validate, export, write
  the report. Both front-ends call it, so a fix reaches both and neither can
  quietly grow its own behaviour.
- **`RosterCsvValidator`** — a pre-flight check producing Blocking / Warning /
  Note findings. Runs the same reader, position mappings, bounds and role
  vocabulary generation uses rather than reimplementing them.
- **`RosterGenerator.Gui`** — Avalonia desktop app, window built in C# so the
  build stays a plain compile. Validates on file selection, preselects the
  team and season it finds, disables the options that cannot work, and runs
  generation off the UI thread.
- **CLI `validate`**, exiting non-zero only on blocking findings.
- **`build-release.sh`** — self-contained `win-x64` publish of both
  executables with `data/`, `templates/` and `QUICKSTART.md`, zipped.
- **`GuiSmokeTests`** — builds and shows the real window under Avalonia's
  headless platform, and asserts Generate is disabled until both files are
  chosen.


### Milestone 6 — roster completion and benchmarking

- **`RosterDepthModel`** (`data/RosterDepth.json`) — median overall by roster
  rank and class mix by depth band, measured across 138 base-save rosters.
  Also carries the league median (69) used to size the program adjustment.
- **`RosterFiller`** — turns unfilled slots into end-of-roster depth off the
  measured curve, ceilinged by the weakest historical player at the position
  and floored at 45. Fully deterministic (largest-remainder class quotas, no
  sampling) so byte-stable output is preserved.
- **Program adjustment** — the donor roster's median against the league
  median, shifted onto thinly evidenced players and fading to nothing as
  evidence strengthens (full at Low confidence, half at Medium, none at
  High). Needs no new input: the dynasty already encodes the tier.
- **`positionOverallCaps`** — each position group capped at the highest
  overall the game itself carries there. The observed maximum, not a
  haircut: a first-team All-American kicker still generates at 89 of 90.
- **`SetPreviousSchool`** + `DynastyExport.BuildPreviousSchoolMappings` —
  writes `PLYR_PREVTEAMID` as the school's `TEAM_ORIGID`, translating the
  shared alias overlay into that id space so "Mississippi State" finds the
  save's "Mississippi St".
- **`RosterFidelityTests`** — asserts the generated roster's shape stays
  within 3.00 mean absolute deviation of the game's own.
- **2023 Florida State dataset enriched** — all ten 2024 draft selections
  with overall pick numbers, the undrafted signing, All-ACC and All-America
  honours, season statistics, and a depth-chart role for all 75 players.
- **CLI** `--fill fill|leave`.

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
4. ~~**`PLYR_PREVTEAMID` native domain.**~~ **RESOLVED (Milestone 6):** it
   holds the school a transfer came from, as that school's `TEAM_ORIGID` —
   not a `TeamIndex`, which is why the values (1009–1235) never matched the
   team range. 133 of the 135 distinct non-zero values in a base save are a
   `TEAM_ORIGID` in that save, and Florida State's 20 non-zero players
   resolve to real schools. `1009` means a school the dynasty does not
   model. `PrevTeamIndex` reads `255` for every player in an untouched save
   including those transfers, so the two fields do **not** track the same
   thing; only `PLYR_PREVTEAMID` is written. See Schema.md Group 4.
5. **~250 unconfirmed columns.** Statistically profiled (type/range/enum
   values) in the Schema.md appendix but never verified by a controlled
   edit. None are written by the tool.
6. **Asset regeneration rules.** `PLYR_ASSETNAME` /
   `GenericHeadAssetName` / `PLYR_PORTRAIT` formats are observed but the
   generation algorithm (and which values are safe to synthesize for a
   replacement player) is unknown — currently the caller must supply
   values on a replace.

## Next recommended milestone

**Milestone 8 — Draft slot measures the wrong season.**

The one rating defect Milestone 6 found and neither it nor Milestone 7 fixed,
and now the most valuable thing left.

Draft position is the strongest single signal in the model, and it records
where a player was taken, not how they played. Jordan Travis generates at 83
against the manual export's 90: he was a fifth-round pick because of a
November leg injury, in a season where he was an ACC Player of the Year
candidate. It distorts exactly the marquee players a historical roster exists
for — the ones a user will look at first.

Options worth weighing, cheapest first:

1. An explicit `Injured` or `DraftStockNote` column. The `Notes` column
   already records the injury and nothing reads it.
2. Weight the draft signal down when strong production disagrees with it —
   the model already computes both.
3. Cap how far a draft slot may pull a player below their statistical
   evidence, the mirror of the signal floor that fixed first-round picks.

Option 2 needs no new input from the user, which makes it the one to try
first.

### Also worth doing

- **Roster CSV round-trip.** The generator can already read a roster; it
  cannot write one. Exporting a team's current roster as a roster CSV would
  give users a starting point to edit rather than a blank template, and would
  make "tweak one player and regenerate" a two-minute job.
- **Sign the executables.** Windows SmartScreen will warn on an unsigned
  download from an unknown publisher, which is a real barrier for the
  non-technical users this milestone was for.

### Deliberately *not* next

- **The asset-field study.** `PLYR_ASSETNAME` / `GenericHeadAssetName` /
  `PLYR_PORTRAIT` are still unmapped (Known unknown 6), so replaced players
  wear the donor players' faces. Still the wrong trade: purely cosmetic, most
  inherited heads are generic anyway, and the payoff needs a
  reverse-engineered generation algorithm.
- **Multi-team and multi-season generation.** Recreating a whole historical
  season means 138 teams' rosters — a data-gathering problem far larger than
  the tool.
- **Jersey numbers.** About 25 remain unverified in the FSU dataset and the
  two rosters disagree on roughly 40%. This needs sources, not engineering.

Explicitly still deferred: automatic historical data gathering,
equipment/face generation, dynasty editing, and the derived-array recompute.
