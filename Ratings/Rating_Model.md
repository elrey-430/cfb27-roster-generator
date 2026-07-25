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
   │  2. class-year cap     (low-confidence freshmen cannot look like veterans)
   │  3. attribute shape    position baseline → talent sensitivity
   │                        → physique → verified measurements → experience
   │  4. calibration        solve EA's formula backwards for the target
   │  5. sanity caps        position / class / global, then recompute overall
   ▼
56 attributes + overall + confidence + explanation
```

### 1. Evidence → target overall

Every signal answers one question — *what overall does this fact imply?* —
so the tables are directly reviewable. The target is the weighted mean of
whichever signals had data.

| Signal | Weight | Source |
|---|---|---|
| Draft slot | 0.34 | `draftScores` (#1 → 99, #32 → 89, #100 → 80, #256 → 71) |
| Awards | 0.26 | `awardScores` (Heisman 98, consensus All-American 93, first-team all-conference 86) |
| Production | 0.22 | `production` curves per position group |
| Recruiting stars | 0.10 | `recruitingStarScores` (5★ 86 … 1★ 62) |
| Depth-chart role | 0.08 | `roleScores` (starter 76, backup 69, reserve 64) |

Only the **best** award counts; extras are noted but never stacked, so a
long honours list cannot inflate a player past its ceiling. Partial stat
lines scale their signal's weight down proportionally. With no evidence at
all the target falls back to `referenceTalent` (75 — an average FBS
starter).

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

### 3–4. Attribute shape, then calibration

Each attribute starts at its position baseline (the value for a player at
overall 75) and moves by `talentSensitivity × (talent − 75)`, so an elite
quarterback gains far more throw accuracy than speed. Physique nudges and
**verified measurements** are applied next; a measured 40 time, bench,
vertical, shuttle or three-cone *replaces* the estimate and is then
**locked** — calibration may never move it.

Calibration then solves EA's formula backwards. Because the formula is
linear, the required correction is closed-form rather than searched, and it
is distributed **in proportion to each attribute's talent sensitivity** so
quality-defining attributes absorb it first and near-constant physical
traits stay put.

### 5. Sanity caps

Position caps, class-year caps and a global 10–99 floor/ceiling are
re-applied after every calibration pass. If caps prevent reaching the
target, the shortfall is reported rather than hidden. See
`Default_Assumptions.md`.

## Believability check against the real game

Generated players were compared with the real distributions in a base
dynasty export (median and full range for each overall bucket):

| Case | Generated | Real-game bucket (median, range) | Verdict |
|---|---|---|---|
| Reggie Bush, HB 94 | speed 98, carrying 92 | HB 90–99: speed 91 (88–95), carrying 89 (78–94) | at the top, as expected for a 4.33 Heisman winner |
| Dalvin Cook, HB 88 | speed 92, carrying 93 | HB 80–89: speed 90 (85–96), carrying 83 (68–96) | in range |
| Jalen Ramsey, CB 89 | speed 96, man cover 91 | CB 80–89: speed 91 (87–99), man 82 (73–91) | in range, high end |
| Roderick Johnson, LT 80 | speed 61, strength 94 | LT 80–89: speed 66 (53–77), strength 89 (83–96) | in range |
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
