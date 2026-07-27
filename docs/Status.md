# Project Status

_Last updated: 2026-07-27 — end of Milestone 14._

## Current status

**Milestone 14 (Native dynasty saves) is complete.**

- **A dynasty goes in as a save and comes back as a save.**
  `generate --dynasty DYNASTY-BASE1 --roster 2023_FSU.csv --save-out DYNASTY-2023FSU`
  is the whole workflow. No PocketScout export, no third-party importer — the
  two worst steps of the user's process are gone. Confirmed by the user: the
  written save loads in the game with the edit intact.
- **Nothing between the two ends had to change.** `extract.mjs` writes CSVs
  that are **byte-identical to PocketScout's own export** — verified on the
  Player, Team and CharacterVisuals tables — so the entire pipeline from
  Milestone 3 onward reads a save without knowing one was involved, and the
  2023 FSU regression test pins the same bytes either way.
- **Only differing cells are written.** The real 2023 FSU roster into a real
  save wrote **5,461 fields**, left **243 empty roster slots untouched**,
  changed **85 rows on team 27 and 0 rows anywhere else**, and produced a
  Player table matching the generated CSV exactly. The empty-record rule
  matters: a save pre-allocates slots holding no player, and writing the
  export's blanks back into them would be writing a blank name over a slot the
  game expects to find in a particular state.
- **The save that came in is never modified.** The output is always a new
  file, writing over the source is refused, and the originals' hashes were
  checked unchanged after every run.
- **The format work is borrowed, not rebuilt.** `madden-franchise` (MIT) ships
  the C27 schema and the zstd dictionaries. A C# reimplementation would mean
  owning a bit-packer and a 3,498-entry schema table, re-verified against every
  game patch, in exchange for nothing a user can see. `NativeSave` is the whole
  boundary: two process calls and a magic-byte check.
- **Nothing to install.** The release bundles the Node runtime itself
  (v22.23.1 LTS, MIT, checksum-verified against nodejs.org at build time,
  +33 MB zipped) alongside the vendored library, so the user installs nothing
  and runs no package manager. The bundled copy is private to the app and
  cannot be broken by another Node version on the machine. `NativeSave`
  prefers it and falls back to PATH for source checkouts. Without either, the
  tool names what is missing and the CSV workflow is untouched.
- **The desktop app does this too.** A "Save file…" browse button opening at
  the game's saves folder, a "write a new dynasty save" option that appears
  only when the input is a save, and the runtime problem reported inline
  rather than at Generate. Its step-1 copy used to say "This tool does not
  read your save file", which the smoke test pinned — both are now inverted.
- **Two leaks closed on the way.** `OpenDynasty` returned an export while
  dropping the package that owned its scratch folder, so every archive or save
  selection left a copy of the dynasty's tables in the temp folder; it now
  returns the package and callers dispose it. And `generate` opened the
  dynasty eagerly for a team prompt it usually never showed, extracting a save
  twice per run.
- **The guard that matters.** The schema is pinned at `C27_468_2`; a mismatch
  refuses to write rather than guessing, because a field written at the wrong
  offset corrupts a dynasty silently.
- Tests: 353/353. The end-to-end save test runs when `CFB27_TEST_SAVE` points
  at a real save; a green suite says nothing about that path unless it was set.

**Milestone 13 (A whole season at a time) is complete.**

- **The tool now writes the blank file, not the user.** `template --season
  2010` produces one row per roster slot for every team that played that year,
  with `Team`, `Season` and `Position` filled in. Against a real base save:
  **119 teams × 85 = 10,115 rows**. By hand that means knowing which schools
  existed that year and typing 10,000 rows before a single player is
  researched.
- **The oversight it exists to close.** CFB27 ships the **138 teams of
  today**, so a 2010 season assembled from that list silently includes schools
  that were still in the FCS, and nothing in the save says so.
  `data/FbsMembership.json` records when each of 31 schools reached the FBS
  (plus UAB's 2015–16 gap), and the 2010 run correctly left out **19**:
  Sacramento State, NDSU, Delaware, Missouri State, Kennesaw St., Sam Houston,
  Jax State, James Madison, Liberty, Coastal Carolina, Charlotte, App St., Ga
  Southern, Old Dominion, Georgia State, UMass, Texas State, UTSA and South
  Alabama. (2010's FBS had 120 teams; the one CFB27 cannot supply is Idaho,
  which left after 2017.)
- **Advisory, never a gate.** `validate` reports the same thing as a note on a
  filled file, and generation proceeds regardless. The dates are this project's
  reading of the record in a plain JSON file the user can correct — refusing to
  build somebody's roster over a date this project got wrong would be the worse
  failure.
- **One roster file can now carry any number of teams.** Each team's 85 slots
  are disjoint, so they all convert into the single output table the user
  imports once. Verified end to end on the full 2010 file: **10,115 players
  across 119 teams in 21 seconds, 0 errors**, and a diff against the source
  table shows exactly 10,115 changed rows, in exactly 119 teams, of exactly 85
  — the other 21 team indexes, recruit pool included, untouched.
- **The reporting was wrong before it was right.** The first working
  multi-team run converted all three teams but reported only the first, and
  rehelmeted only the first. The result now carries every conversion, the
  tallies are over all of them, and equipment is applied per season across
  every converted team.
- **The 85-slot layout is measured, not invented.** `data/RosterSkeleton.json`
  is the league mean across a base save's 138 teams, apportioned to exactly 85
  by largest remainder: 9 WR, 8 CB, 6 each DT/HB/TE, 4 each
  FS/LE/LT/MLB/QB/RE/ROLB/RT/SS, 3 each C/LG/RG, 2 K, 2 P, 1 LOLB. A starting
  shape, not a rule.
- Tests: 335/335.

**Follow-up: the height column is inches, and its name says so.** Filling the
template with a spreadsheet assistant failed consistently on `Height`, and the
cause was not the tool: Excel decides `6-2` is the 2nd of June the moment it
opens the file and writes back `2-Jun` or the serial behind it, so the height
was destroyed before the generator ever saw it. The column is now
**`HeightInches`** — a bare number is the only thing that survives a
spreadsheet, and the column name is the instruction. Feet-inches is still read
and converted (refusing a value the tool understands would cost the user data
to make a point) but reported as a correction; a date or a date serial is
named as such rather than reported as an implausible height, so the user looks
at the right thing. `Height` keeps reading the same cell for good, so files
already filled in under the old name are unaffected. Tests: 343/343.

**Research: native dynasty saves can be read and written.** Five real save
files (not exports) were measured against the PocketScout CSV exports of the
same saves. A CFB27 save is an extensionless `FBCHUNKS` file, 9,646,981 bytes
packed and ~30 MB unpacked, zstd-compressed with a trained dictionary; it is
EA's franchise database, schema `C27_468_2`, handled by the MIT-licensed
`madden-franchise` library. Four things were established, all against real
data: it **opens** (all five, correct schema and table count, no override);
the **read is exact** (4,584,474 field comparisons over 16,257 live players
against PocketScout's own CSV, zero mismatches, on two different saves); the
**round trip is lossless** (unpack → repack on all five gives a byte-identical
30 MB database — the packed file differs because zstd will not reproduce EA's
stream, so packed-byte equality is the wrong test); and a **single edit stays
single** (one jersey number: 1 byte changed in 30 MB, 1 cell in the Player
table). The unknown that matters is whether the game loads a repacked save,
which needs the game and cannot be tested here. Details and the harness are in
`tools/native-save/`. **Since confirmed by the user — the game loads it — and
built out as Milestone 14 above.**

**Milestone 12 (One file in, one file out; and appearance) is complete.**

- **A dynasty goes in as one file and comes back as one file.** `--dynasty`
  takes a `.zip` wherever it took a folder, and `--package` writes the whole
  dynasty back out as a single archive. The property that matters is not that
  it round-trips — it is that **everything the tool did not generate comes back
  byte for byte**. Verified on five fresh saves: 2,271 of 2,273 files identical
  (2,273 of 2,275 for the two with a custom coach), the two that moved equal to
  the generated tables, and the result re-opens as a dynasty.
- **Five fresh saves settled what varies between dynasties.** Two independently
  created saves with the same team, coach and roster are **byte-identical on
  all 139 real teams**; the only differing rows are the 4,100 in `TeamIndex
  255`, the randomly generated recruit pool. The coach touches **no** player
  row. The live roster is not byte-stable between downloads, but only in
  flavour fields — hometown, ability bitfield, pipeline, 35 jersey numbers —
  and never a name, position, class, height, weight or rating.
- **A custom coach renumbers the database.** 245 of 1,299 shared tables shift
  `_tableIndex`, including the real `Team` table (2225 → 2227). `Player` and
  `CharacterVisuals` happen to stay put, but there are **nine tables named
  `Team`** in every save, so discovery by content rather than by number is what
  keeps this working — and is why a canonical Player table can never be shipped.
- **Skin tone is decoded, and it rides along with the face.** The
  `CharacterVisuals` blob carries a bare `skinTone` (1 lightest, 8 darkest),
  and the sixth segment of a generated head's own name is the same value —
  3,144 agreements, zero disagreements. A given generated head is only ever
  used at one tone (1,607 heads, none at two), so choosing the face chooses the
  tone and the visuals table never has to be written.
- **Faces now keep the slot's tone, and an optional `SkinTone` column
  overrides it.** On the 2014 Florida State roster: 6 of 6 requested tones
  honoured exactly, and **79 of 79 other players kept their tone through the
  face swap, none moved**. Out-of-range values are refused and reported rather
  than clamped.
- **The tone is supplied, never inferred.** The generator will not guess what a
  real person looked like from their name, hometown or position. A blank cell
  means "keep what the roster slot had".
- Tests: 317/317.

**Milestone 11 (Attributes that match the archetype) is complete.**

- **The defect, reported twice from opposite sides.** A user's Marcus Allen —
  a back who caught 34 passes, correctly classified `HB_PowerReceiving` — came
  out with **30 in all three route-running attributes**. Another user's
  Marqise Lee, a receiver, came out with **34 juke and 30 trucking**. Same
  bug: the archetype was chosen correctly and then ignored. The attribute
  shape was assembled from hand-written position baselines that named only a
  subset of the 56 attributes, and everything they omitted fell to a global
  default of 30.
- **The fix is measured, not authored.** `tools/build_archetype_profiles.py`
  reads a real dynasty export and fits `value = intercept + slope × overall`
  for **all 59 archetypes × all 56 attributes** across 16,256 players, and
  records the residual spread too. `data/ArchetypeProfiles.json` is that
  measurement; a generated player now starts from what the game itself gives
  their archetype at their overall. The seed is self-consistent: fed back
  through EA's own formula it returns the overall it was built for to within
  0.3 points for 56 of the 59 archetypes.
- **Production now moves the attributes it was earned with.** Each role a
  player produced in (passing, rushing, receiving, pass rush, run stop,
  coverage, kicking, punting) raises that role's attributes by a number of
  standard deviations **of the spread the game itself shows** for that
  archetype. Nothing invents a magnitude. It only ever raises — a 1968
  receiver must not be marked down for numbers nobody kept — because shaping
  downward is the archetype's job.
- **A second role now counts toward the overall.** `HB` asks how well someone
  ran; a back who caught 37 passes answered a question it never asked and used
  to tie with a back who caught none. Secondary-role production adds a bounded
  bonus to the target.
- **Sanity caps yield to measurements.** Several hand-written position caps
  would have held an archetype below where every player of it in the game
  actually sits. The cap is widened to admit the measured value and goes on
  bounding everything else.
- **The guardrail is general, not two special cases.** `ArchetypeFloorTests`
  asserts that no generated player sits below the floor the game's own players
  of that archetype occupy, in any attribute that archetype's overall formula
  weights heavily. It fails on the old engine with **48 breaches** across the
  Florida State fixture and passes on the new one.
- **Roster strength is untouched; only shape moved.** Regenerating the 2014
  Florida State roster old-vs-new: **all 85 overalls identical**, mean
  attribute movement 8.7 points, and 147 attributes moved 30+ points into the
  range the game actually uses.
- Tests: 295/295, with the whole suite now running the engine configured the
  way the shipped application configures it.
- **Verified on both reporters' own files before release.** The 2014 Florida
  State roster was rebuilt from public sources and reviewed by hand; the other
  user's All-Time USC template was run unmodified. On USC: all 85 overalls
  identical again, mean attribute movement 9.35, and 197 attributes moved 30+
  points — Marcus Allen's medium route 30 → 86, Reggie Bush's 30 → 94, Marqise
  Lee's juke 34 → 89. The secondary-role bonus fires on Bush and says so in
  the report.
- Shipped together with Milestone 10 as
  [v0.4.0-alpha](https://github.com/elrey-430/cfb27-historical-rosters/releases/tag/v0.4.0-alpha).

**Milestone 10 (Faces) is complete — the first tier. Shipped in v0.4.0-alpha.**

- **The defect.** A replaced player inherited the roster slot's head, and
  **9,011 of 16,257** players in a base save wear a `Unique_` scan of a real
  person — 71 of the 85 slots on a typical team. So most of a recreated 1985
  roster wore the recognisable faces of present-day players, under other
  people's names. On the Florida State fixture that was 71 slots; it is now 7,
  and those 7 are leftover slots still carrying their own player's identity.
- **The fix.** Those slots get a generated face **drawn from the user's own
  export** — never an invented asset name, the same rule the equipment layer
  follows — with `PLYR_PORTRAIT` written to match and `PLYR_ASSETNAME` cleared.
  Selection is seeded from the player's row key, so a roster regenerates
  identically. `--faces inherit` restores the old behaviour.
- **Deliberately narrow.** Slots that already carried a generated face are not
  churned, and slots no historical player took over keep their own likeness.
  Every substitution is listed in the report.
- **Not attempted: matching a historical player to a real scan.** The scans
  are present-day players, so the overlap with any historical season is
  almost nil — and inferring what a real person looked like from their name is
  not something this tool should do. A user who knows the right head can name
  it; that is their call, not an inference.
- Tests: 289/289. The 2023 Florida State golden fixture was regenerated; the
  only columns that moved are the three head columns.

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
- **The catalogue cannot be mined.** Retro helmets appear on *zero* of 12,586
  players in a base save, so every period asset had to be demonstrated in the
  editor and read out of a diff. Two rounds, 25 player edits, covered it. A
  season no era covers still changes nothing.
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
  deterministic pool for spreading masks across a line. The demonstration then
  showed something finer than the mined data did: offensive and defensive
  linemen differ, so a centre gets `revofullcage` where an edge rusher gets
  `RevoRobot`.
- **Sleeves and shoulder pads** are era-wide slots alongside the helmet:
  tight/small in the 2010s, loose/medium in the 2000s, long from the 1990s
  back with large then X-large pads. `SleeveStandard` turned out to be what
  the editor calls "loose".
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

### Milestone 13 — a whole season at a time

- **`SeasonTemplateWriter`** (`Core/Historical/`) — writes the blank
  whole-season roster CSV. The header is copied from the shipped template
  rather than restated, so the blank file and the documented format cannot
  drift apart.
- **`FbsMembership`** (`data/FbsMembership.json`) — per-school first FBS
  season plus skipped-season ranges. `Check` returns a problem or null;
  `EligibleIn` filters a season's teams. Advisory everywhere it is consulted.
- **`data/RosterSkeleton.json`** — the measured league-mean position layout of
  a team's 85 slots, apportioned by largest remainder.
- **Multi-team reading and conversion** — `HistoricalCsv` groups rows by team
  and exposes `Rosters` / `IsMultiTeam`; `RosterGenerationService.Run` converts
  every team into one session and one output table, and reports a team the
  dynasty does not carry rather than failing the other 130.
- **`EquipmentApplier.Apply(…, IReadOnlyCollection<int> teamIndexes, …)`** and
  `EquipmentReport.Merge` — a season's teams are rehelmeted per season in one
  pass over the roster, and the summary counts all of them.
- **`CsvDocument.FromRows`** — files this project writes are quoted and
  escaped by exactly the code that reads one back.
- **CLI `template`**, and `validate` extended to check every team in a season
  file and to note a school that had not reached the FBS.

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
6. **Asset regeneration rules.** ~~Unknown.~~ **WORKED AROUND (Milestone 10):**
   the generation algorithm behind `GenericHeadAssetName` / `PLYR_PORTRAIT` is
   still not decoded, and the segments inside a generated head's name are not
   understood — but nothing needs to synthesize one. A replaced player is given
   a head that already exists in the user's own export, `PLYR_PORTRAIT` written
   to match and `PLYR_ASSETNAME` cleared. Reassignment, never authoring. What
   remains unknown is only *which* generated head suits a given player, which
   is cosmetic.

## Next recommended milestone

**Milestone 15 — Roster CSV round-trip.**

The generator can read a roster file; it cannot write one. That asymmetry is
now the biggest thing standing between a user and a good result — and a
season's worth of teams has just made it larger, because a user who dislikes
one player in 10,115 still has no way to correct them and regenerate.

Every rating defect reported so far arrived the same way: a user generated a
roster, looked at one player, and disagreed. The answer to that is not another
rating rule — it is letting them fix it and regenerate without retyping 85
lines. Exporting a team's current roster *as a roster CSV* turns "tweak one
player" from an afternoon into two minutes, and it turns a blank template into
a filled starting point, which is the single most common complaint about the
input step.

It also removes the standing awkwardness that the only way to correct a
generated player is in the third-party editor, where the correction is invisible
to this tool and lost on the next run.

### Also worth doing

- **A `Season` per row.** The All-Time USC file carried a different season on
  every player and the tool had to pick one (1980), which then put the whole
  squad in Riddell TKs. All-time rosters are clearly a thing users want; per
  player equipment eras would serve them properly.
- **FBS membership only records arrivals.** `data/FbsMembership.json` covers
  the 31 schools that joined since 1978 and knows nothing about schools that
  *left*: Idaho played the 2010 season and dropped to the FCS after 2017, so
  CFB27 does not carry it and a 2010 template writes 119 teams where the real
  FBS had 120. The tool cannot supply a team the save does not have, but it
  should say so rather than leave the user to count. Seasons before 1978 are
  also outside what the file describes.
- **Archetype rules deserve a second pass.** Two questionable calls surfaced
  in verification: a Groza-winning kicker classified `KP_Power` off a 53-yard
  long, and a 278 lb Anthony Munoz classified `OT_PassProtector` by a weight
  threshold, costing him run blocking. Both are one-line data edits; neither is
  obviously wrong; both are worth a deliberate review rather than a reaction.
- **Sign the executables.** SmartScreen still warns on every download.
- **Bundle Node, or drop the dependency.** Reading a save needs Node.js 22.19+,
  which is the only thing a user must install. Options, none yet judged worth
  it: ship a Node single-executable build of the two sidecars (~50 MB added to
  the release), or reimplement the format in C# (owns a bit-packer and a
  3,498-entry schema table forever). The current answer — name the missing
  dependency and keep the CSV route working — is honest and cheap, but it is a
  step between a user and the good workflow.

### Deliberately *not* next

- **Position-cap and program-adjustment interaction.** On an all-time roster a
  receiver with Medium-confidence evidence can clear a Heisman-winning back,
  because the WR cap is 99 where HB is 96 and the program adjustment applies in
  full at Medium confidence. Both parts are measured and defensible on their
  own. Judged not worth changing: it is visible only on all-time rosters, and a
  user who disagrees can edit it.
- **Tier 2 faces (decoding the head segments).** Tier 1 stopped recreated
  players wearing real people's faces, which was the actual harm. Choosing
  *which* generated head is cosmetic.
- **Create-A-Face import.** Confirmed Road To Glory exclusive; the conversion
  path through a Dynasty save is unverified.
- **Jersey numbers.** About 25 remain unverified in the FSU dataset and the
  two rosters disagree on roughly 40%. This needs sources, not engineering.

Explicitly still deferred: automatic historical data gathering, dynasty
editing, and the derived-array recompute.
