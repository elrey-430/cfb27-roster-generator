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
| Draft slot | 0.34 | `draftScores` (#1 → 99, #32 → 93, #100 → 84, #256 → 75) |
| Awards | 0.26 | `awardScores` (Heisman 98, consensus All-American 93, first-team all-conference 86) |
| Production | 0.22 | `production` curves per position group |
| Recruiting stars | 0.10 | `recruitingStarScores` (5★ 86 … 1★ 62) |
| Depth-chart role | 0.08 | `roleScores` (starter 76, backup 69, reserve 64) |

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
(`signalFloors`): the target may not fall more than 2 points below what the
draft slot implies, or 6 below a major award. The draft curve and that
tolerance are tuned together so the entire first round floors at 91+:

| Pick | Implied | Floor | Pick | Implied | Floor |
|---|---|---|---|---|---|
| 1 | 99 | 97 | 32 | 93 | **91** |
| 5 | 97 | 95 | 40 | 90 | 88 |
| 10 | 96 | 94 | 64 | 87 | 85 |
| 16 | 95 | 93 | 100 | 84 | 82 |
| 24 | 94 | 92 | 200 | 78 | 76 |

When a floor is applied it is stated in the player's reasons — e.g.
*"Raised to 95 (floor from draft: Drafted #5 overall) — the weighted blend
of 90 understated a player with this record."*

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
