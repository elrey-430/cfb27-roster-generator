# Rating Model

How the generator turns historical player information into a complete CFB27
rating set. Nothing here is a black box: every number lives in a data file
you can edit, and every generated player can explain itself.

- **`data/OverallFormulas.json`** — EA's own overall formulas (supplied, not
  invented). Authoritative for the overall rating.
- **`data/RatingModels.json`** — the tunable *shape* model: position
  baselines, talent sensitivity, physical/combine curves, production curves,
  sanity caps.

## The overall rating is EA's formula, not ours

CFB27 computes overall as a plain linear function of the attributes, keyed
by **position and player archetype**:

```
overall = intercept + Σ (attributeValue × coefficient)
rounded to the nearest integer, with exact .5 rounding DOWN, clamped 12–99
```

79 formulas cover all 21 positions and all 59 archetypes.

**Independent verification.** Applying these formulas to the 16,257 real
players in a base dynasty export reproduces the game's own stored
`OverallRating`:

| Metric | Result |
|---|---|
| Exact match | **99.33%** (16,148 / 16,257) |
| Within ±1 | **99.90%** |

Known weak spots, carried as-is:

- **FS + `S_RunSupport`** — 78 of 404 players come out exactly +1 high, while
  SS with the same archetype is 100% exact. FS and SS evidently have
  slightly different formulas that the source file merges into one entry.
- **`KP_Power`** kickers/punters — a handful of larger misses (worst −13).
- The file's `originalScale` block disagrees with `coefficients` for
  `S_RunSupport` and `S_Zone`; **`coefficients` is authoritative** and is
  what this tool uses.

Using the real formula has a large practical payoff: the overall this tool
writes is exactly what the game will display for the attributes it wrote,
so the two can never disagree.

## Pipeline

```
historical evidence
   │  1. TalentScorer      → target overall + confidence + reasons
   │  1b. secondary role    production the position's own curves cannot see
   │  2. class-year cap     (low-confidence freshmen cannot look like veterans)
   │  3. attribute shape    MEASURED archetype profile at that overall
   │                        → production emphasis → physique
   │                        → verified measurements → experience
   │  4. calibration        solve EA's formula backwards for the target
   │  5. sanity caps        position / class / global, then settle integers
   ▼
56 attributes + overall + confidence + explanation
```

### 1. Evidence → target overall

Every signal answers one question — *what overall does this fact imply?* —
so the tables are directly reviewable. The target is the weighted mean of
whichever signals had data.

| Signal | Weight | Source |
|---|---|---|
| Draft slot | 0.34 | `draftScores` (#1 → 99, #32 → 93, #100 → 88, #256 → 85) |
| Awards | 0.26 | `awardScores` (Heisman 98, consensus All-American 93, first-team all-conference 86) |
| Production | 0.22 | `production` curves per position group |
| Recruiting stars | 0.10 | `recruitingStarScores` (5★ 86 … 1★ 62) |
| Depth-chart role | 0.08 | `roleScores` (starter 78, backup 73, reserve 68, walk-on 64) |

Only the **best** award counts; extras are noted but never stacked, so a
long honours list cannot inflate a player past its ceiling. Partial stat
lines scale their signal's weight down proportionally. With no evidence at
all the target falls back to `referenceTalent` (75 — an average FBS
starter).

#### Signal floors — strong evidence is not averaged away

A plain weighted mean understates elite players: Jalen Ramsey, the #5
overall pick, blended to **89** because his draft slot (96) was averaged
against an ordinary "first-team all-conference" and his recruiting rating.
Real rosters put first-round picks at **91 or above**, high picks higher
still.

So the strongest retrospective signals also set a **floor** on the target
(`signalFloors`): a draft slot floors at exactly what it implies (tolerance
**0**), and a major award within 2 points of what it implies.

**The draft curve spans the whole drafted band, 85 to 99**, so a pick number
is a rough statement of overall on its own:

| Pick | Implied | Pick | Implied | Pick | Implied |
|---|---|---|---|---|---|
| 1 | **99** | 32 | 93 | 130 | 87 |
| 5 | 97 | 50 | 91 | 160 | 86 |
| 10 | 96 | 64 | 90 | 200 | 86 |
| 20 | 95 | 100 | 88 | 256 | **85** |

Position caps still bind: a receiver taken first overall generates at 99, a
halfback at 96, because 96 is the best halfback the game itself carries.

When a floor is applied it is stated in the player's reasons — e.g.
*"Raised to 95 (floor from draft: Drafted #5 overall) — the weighted blend
of 90 understated a player with this record."*

#### A draft slot is a floor and never a ceiling

Because the draft only ever floors, a season can outrun it. Derrick Henry won
the Heisman and went 45th; the pick implies 92, the Heisman implies 98, and
the higher floor wins:

| | Heisman season | Ordinary season |
|---|---|---|
| Taken 45th | **96** | 92 |
| Taken 240th | **96** | 85 |

The award tolerance is what makes that work. At the old value of 6 a Heisman
floored at 92 — the same number pick 45 implies — so Henry came out 92 either
way and his draft slot had effectively become the verdict on his year. At 2 he
clears it, and where he was taken no longer changes what he was.

#### The drafted floor — being drafted at all is the fact

The per-signal floor above is proportional: it tracks the draft curve down,
so a late pick floors at a late-pick number. That still let the bottom of the
draft fall through. A seventh-round pick generated at **77**, and a
sixth-rounder at **80** — the same 80 as a player whose roster row said
nothing about the draft whatsoever.

That is the wrong shape for the fact. Roughly 250 players are drafted out of
some ten thousand in FBS, and a weighted mean cannot express it, because the
draft is one signal of five and the other four are all about a single season.

So `draftedOverallFloor` (**85**) is a flat minimum for anyone with a pick or
a round, applied after the program and secondary-production adjustments and
before every ceiling — a position cap still wins.

`draftedOverallFloor` (**85**) is now a backstop rather than the working rule:
the draft curve bottoms out at 85, so the per-pick floor already holds every
drafted player there. It stays because it states the invariant independently of
the curve — a later edit to `draftScores` cannot quietly drop drafted players
below the band.

#### The undrafted ceiling — the other side of the same line

`undraftedOverallCeiling` (**85**) caps a player marked `UDFA`. The two rules
meet at 85 rather than overlapping, so where a player sits says what happened
to them: drafted from 85 up, undrafted from 85 down.

It applies to an explicit `UDFA` only, **never to a blank draft column.**
"Undrafted" is a statement about the player; an empty column is a gap in the
record, and most all-time rosters carry no draft data at all — capping those
would assert something nobody said.

**Undrafted is not the same as unknown, and neither gets the floor.** `UDFA`
in the `DraftPick` column is a statement about the player (scored 67); an
empty column is a gap in the record. Both stay where they were.

The cost is measured: mean absolute deviation from EA's own Florida State
curve moves from **2.12 to 2.29**, against a 3.00 bar and the 3.02 the manual
human recreation of that roster scores.

#### Role scores are the game's own roster, by rank

`roleScores` is not a judgement about what a backup is worth. Each value is the
**median overall the game itself carries at the roster ranks that role
occupies**, taken from `medianOverallByRank` in `data/RosterDepth.json`:

| Role | Roster ranks | Median | Was |
|---|---|---|---|
| Starter | 1–22 | **78** | 76 |
| Backup | 23–44 | **73** | 69 |
| Reserve | 45–70 | **68** | 64 |
| Walk-on | 71–85 | **64** | 61 |

The old values were three to five points low across the board, and it showed in
the middle of a generated roster: against EA's own Florida State, the 75–79
band held 3 players where the game holds 21.

#### The role spread — a curve, not a number

Even with the right median, every player whose file gives *only* a role came
out on that one number. On the 2023 Florida State file — 64 of whose 75 rows
carry no stats, no award and no draft slot — that meant **18 players on exactly
78 and 25 on exactly 68**, where EA's own roster puts three to nine players on
each value from 69 to 84.

No single score can fix that, because the game spreads a long way *inside* each
role (`roleSpread`, measured across 11,730 players on 138 teams):

| Role | p10 | p25 | median | p75 | p90 | spread |
|---|---|---|---|---|---|---|
| Starter | 73 | 75 | 79 | 83 | 87 | 14 |
| Backup | 69 | 70 | 73 | 75 | 77 | 8 |
| Reserve | 65 | 66 | 68 | 71 | 73 | 8 |
| Walk-on | 59 | 61 | 64 | 66 | 68 | 9 |

Class year does not explain that spread either — measured on the same 11,730
players it moves the median within a role by one point, four for starters.

So `RoleSpread` reproduces the *distribution* without claiming to know which
player is which: within one role, the players whose whole record is that role
are ordered by what evidence they do have — blended overall, then class
seniority, then name — and laid along the measured percentile curve. This is
the same thing `RosterFiller` has always done for slots no historical player
fills, and for the same reason: a roster's shape is measurable even when its
individuals are not.

**It only ever moves a player the file says nothing about.** One stat, one
award or one draft slot and the player keeps their own number. A single player
in a role is not a pile and is left alone. The order is decided by evidence and
then by name, never by chance, so the same file always produces the same
roster.

Result on the 2023 Florida State roster:

| | Biggest pile | Distinct overalls |
|---|---|---|
| Before | 25 players | 20 |
| After | 15 players | 27 |
| EA's own | 9 players | 25 |

The 15 that remain are freshmen sitting on the Low-confidence class cap of 68,
which is a different rule doing its job.

#### The Low-confidence cap reads role first, class second

A player the file says little about is held to what the game carries for that
*kind* of player. That cap used to read class year alone, which conflated
"young" with "unknown" — and the measurement says class is the weaker of the
two by a long way. The 90th percentile of overall, by role and class, across
11,730 players on 138 teams (`lowConfidenceCapByRole`):

| | Freshman | Sophomore | Junior | Senior |
|---|---|---|---|---|
| Starter | 82 | 84 | 87 | 87 |
| Backup | 78 | 77 | 77 | 77 |
| Reserve | 73 | 73 | 73 | 73 |
| Walk-on | 68 | 68 | 68 | 67 |

**Class barely registers below the starting eleven.** Backups run 78/77/77/77
and reserves 73/73/73/73 whatever their year; only starters show a real class
effect, and even there it is five points across four years.

The old per-class cap — 68, 74, 78, 82 — was therefore wrong in both directions
at once. It held a freshman backup **ten points under** where the game puts
one, and let a senior reserve **nine points over**. It falls back to the old
value when the roster file names no role.

Fixing it did what widening the role curve alone could not:

| | Biggest pile | Distinct overalls | MAD vs EA |
|---|---|---|---|
| Before the role work | 25 | 20 | 2.69 |
| Role curve + spread | 15 | 27 | 2.74 |
| **Plus this cap** | **8** | **32** | **2.68** |
| EA's own roster | 9 | 25 | — |

The pile of freshmen stacked on 68 was this cap, not the spread.

### 2. Confidence

Confidence is the fraction of total signal weight that had data:

| Coverage | Confidence | Typical meaning |
|---|---|---|
| ≥ 0.60 | **High** | Draft slot and/or a major award, plus production |
| ≥ 0.30 | **Medium** | Production or recruiting profile only |
| < 0.30 | **Low** | Little more than a position and a class year |

Low confidence also triggers a class-year ceiling
(`lowConfidenceOverallCap`: freshman 68, sophomore 74, junior 78, senior 80)
so an unknown freshman cannot be handed a star's overall.

### 1b. Secondary production

A position's talent score asks one question. `HB` asks how well someone ran;
a back who caught 37 passes answered a second one it never asked, and came
out identical to a back who caught none.

`productionEmphasis.groups` names, per position group, the roles the group is
chiefly rated on (`primary`) and the ones it can also fill (`secondary`). A
secondary role scored above `threshold` raises the target overall, capped at
`secondaryOverallBonusMax` and taking the best role rather than summing them,
so the roster's shape still comes from the primary role.

### 3. Attribute shape — measured, not written down

Each attribute starts at **what the game itself gives that archetype at that
overall**. `data/ArchetypeProfiles.json` is generated by
`tools/build_archetype_profiles.py` from a real dynasty export: for all 59
archetypes and all 56 attributes it fits `value = intercept + slope × overall`
across the export's 16,256 players and records the residual spread as well.

This is not a stylistic preference. The alternative is somebody's opinion of
what a receiving back should be good at, and opinions do not scale to 59
archetypes × 56 attributes — the hand-written position baselines named only a
subset, so everything they omitted fell to a global default of 30. That is how
a back with 34 catches got 30 in all three route-running attributes and a
receiver got 30 trucking: the archetype was chosen correctly and then ignored.

The seed is self-consistent. Feeding an archetype's own line back through EA's
overall formula returns the overall it was built for to within 0.3 points for
56 of the 59 archetypes (worst case 2.5), so calibration makes a small
correction rather than hauling a wrong shape into place.

Where an archetype was not measured — fewer than `minimumSample` players in
the export — the hand-written position baseline still fills in, moved by
`talentSensitivity × (talent − 75)` as before. The engine runs without the
profile file at all.

A position sanity cap that contradicts the measurement **yields to it**: those
caps were written before the game's own players were counted, and several of
them are simply wrong. The cap is widened to admit the measured value and goes
on bounding everything else.

**Production emphasis** is applied next. Each role a player produced in raises
the attributes that role is performed with, by
`(roleScore − threshold) / scorePerSigma` standard deviations **of the spread
the game itself shows** among players of that archetype. Nothing invents a
magnitude, and where the spread was not measurable the nudge is zero. The
nudge only ever raises: historical rosters arrive with whatever box scores
survived, and a 1968 receiver must not be marked down for numbers nobody kept.
Shaping downward is the archetype's job.

Physique nudges and **verified measurements** follow; a measured 40 time,
bench, vertical, shuttle or three-cone *replaces* the estimate and is then
**locked** — calibration and emphasis may never move it.

### 4. Calibration

Calibration then solves EA's formula backwards. Because the formula is
linear, the required correction is closed-form rather than searched, and it
is distributed **in proportion to each attribute's talent sensitivity** so
quality-defining attributes absorb it first and near-constant physical
traits stay put.

### 5. Sanity caps and integer settling

After calibration the attributes are frozen to whole numbers. Rounding 56
attributes moves the formula's total by up to half a point each, which can
drop the overall below the value the double-precision solve reached, so a
final pass nudges integer attributes — largest coefficients first, caps
respected — until the overall computed from **the values actually written**
equals the target.

Position caps, class-year caps and a global 10–99 floor/ceiling are
re-applied after every calibration pass. If caps prevent reaching the
target, the shortfall is reported rather than hidden. See
`Default_Assumptions.md`.

## Believability check against the real game

Generated players were compared with the real distributions in a base
dynasty export (median and full range for each overall bucket):

| Case | Generated | Real-game bucket (median, range) | Verdict |
|---|---|---|---|
| Reggie Bush, HB 97 | speed 98, awareness 89 | HB 90–99: speed 91 (88–95), carrying 89 (78–94) | at the top, as expected for a 4.33 Heisman winner and #2 pick |
| Jalen Ramsey, CB 95 | speed 96, man cover 91+ | CB 90–99: speed 92 (89–96), man 89 (82–98) | in range for a top-5 pick |
| Dalvin Cook, HB 89 | speed 92, awareness 79 | HB 80–89: speed 90 (85–96), carrying 83 (68–96) | in range |
| Roderick Johnson, LT 81 | speed 61, strength 94 | LT 80–89: speed 66 (53–77), strength 89 (83–96) | in range |
| No-evidence walk-on, WR 64 | speed 93, awareness 65 | WR 60–69: speed 86 (77–98), awareness 60 (35–81) | in range, above median |

Full results: `Player_Test_Results.csv`.

## Known characteristics and limits

- **Archetype is inherited, not chosen.** A player takes the archetype of
  the roster slot they replace, so a power back who lands in an
  `HB_ElusiveBack` slot is rated against the elusive formula. Choosing the
  archetype from the historical profile is a future improvement — writing
  `PlayerType` is not yet on the confirmed-safe column list.
- **Calibration pushes the highest-coefficient attributes hardest.** Elite
  quarterbacks reach 99 throw power before their accuracy saturates.
- **Retrospective signals.** Draft position and awards describe a career,
  not a single season; a player's freshman year will look like their peak
  unless you vary the per-season evidence you supply.
- **No equipment, portraits, faces or traits** — out of scope here.
