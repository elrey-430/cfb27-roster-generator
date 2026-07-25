# Position Formulas

Per-position attribute priorities, baselines and caps. All values come from
`data/RatingModels.json` and can be edited without rebuilding.

Reading the tables:

- **Baseline** — the attribute's value for a player at overall 75 (an average FBS starter).
- **Sensitivity** — points the attribute moves per point of overall above/below 75.
  High sensitivity = the attribute defines this position's quality.
- **Cap** — hard sanity bounds for the position (see `Default_Assumptions.md`).

## Position groups

| Group | CFB27 positions | Typical size (from real save data) |
|---|---|---|
| QB | QB | 6'2", 203 lb |
| HB | HB | 5'11", 197 lb |
| FB | FB | 6'0", 202 lb |
| WR | WR | 6'1", 186 lb |
| TE | TE | 6'4", 236 lb |
| OL | LT, LG, C, RG, RT | 6'4", 305 lb |
| DL | LE, RE, DT | 6'3", 265 lb |
| LB | LOLB, MLB, ROLB | 6'1", 225 lb |
| CB | CB | 6'0", 185 lb |
| S | FS, SS | 6'0", 195 lb |
| K | K | 5'11", 188 lb |
| P | P | 6'2", 200 lb |

## QB

**Attribute priorities** (highest sensitivity first): `Awareness`, `PlayRecognition`, `ThrowAccuracyDeep`, `ThrowUnderPressure`, `ThrowAccuracy`, `ThrowAccuracyMid`, `ThrowOnTheRun`, `ThrowAccuracyShort`

| Attribute | Baseline | Sensitivity | Cap |
|---|---|---|---|
| `Awareness` | 70 | 0.85 | — |
| `PlayRecognition` | 70 | 0.8 | — |
| `ThrowAccuracyDeep` | 73 | 0.6 | — |
| `ThrowUnderPressure` | 72 | 0.6 | — |
| `ThrowAccuracyMid` | 78 | 0.55 | — |
| `ThrowAccuracy` | 78 | 0.55 | — |
| `ThrowOnTheRun` | 74 | 0.55 | — |
| `BreakSack` | 66 | 0.5 | — |
| `ThrowAccuracyShort` | 82 | 0.5 | — |
| `ThrowPower` | 84 | 0.45 | — |
| `PlayAction` | 78 | 0.4 | — |
| `Acceleration` | 78 | 0.3 | — |
| `Agility` | 76 | 0.3 | — |
| `BCVision` | 62 | 0.3 | — |
| `Speed` | 76 | 0.3 | 55–95 |
| `Carrying` | 58 | 0.2 | — |
| `HitPower` | 30 | — | 10–50 |
| `ManCoverage` | 30 | — | 10–35 |
| `RunBlock` | 30 | — | 10–45 |
| `Strength` | 62 | — | 45–80 |
| `Tackle` | 45 | — | 10–55 |

**Production signal**

| Stat | Weight | Curve (value → implied overall) |
|---|---|---|
| PassYards | 0.45 | 4200 → 97, 3500 → 91, 2800 → 84, 2000 → 76, 1200 → 68, 400 → 60 |
| PassTD | 0.3 | 40 → 97, 30 → 91, 22 → 84, 14 → 76, 7 → 68, 2 → 60 |
| CompletionPct | 0.25 | 70 → 95, 65 → 89, 60 → 82, 55 → 74, 50 → 66, 45 → 58 |

## HB

**Attribute priorities** (highest sensitivity first): `Awareness`, `BreakTackle`, `BCVision`, `Trucking`, `JukeMove`, `Catching`, `Carrying`, `SpinMove`

| Attribute | Baseline | Sensitivity | Cap |
|---|---|---|---|
| `Awareness` | 68 | 0.75 | — |
| `BreakTackle` | 76 | 0.6 | — |
| `BCVision` | 80 | 0.55 | — |
| `Catching` | 66 | 0.5 | — |
| `JukeMove` | 80 | 0.5 | — |
| `Trucking` | 70 | 0.5 | — |
| `Carrying` | 82 | 0.45 | — |
| `CatchInTraffic` | 60 | 0.45 | — |
| `SpinMove` | 76 | 0.45 | — |
| `StiffArm` | 68 | 0.45 | — |
| `KickReturn` | 70 | 0.4 | — |
| `PassBlock` | 50 | 0.4 | — |
| `Agility` | 86 | 0.35 | — |
| `ChangeOfDirection` | 84 | 0.35 | — |
| `Speed` | 88 | 0.35 | 70–99 |
| `Strength` | 66 | 0.35 | 50–88 |
| `Acceleration` | 88 | 0.32 | — |
| `Jumping` | 80 | — | — |
| `ManCoverage` | 30 | — | 10–40 |
| `RunBlock` | 30 | — | 10–55 |
| `ShortRouteRunning` | 62 | — | — |
| `Stamina` | 84 | — | — |
| `ThrowPower` | 30 | — | 10–50 |

**Production signal**

| Stat | Weight | Curve (value → implied overall) |
|---|---|---|
| RushYards | 0.55 | 1700 → 96, 1300 → 90, 950 → 83, 600 → 75, 300 → 67, 80 → 59 |
| RushTD | 0.25 | 20 → 96, 15 → 90, 10 → 83, 6 → 75, 3 → 67, 1 → 60 |
| YardsPerCarry | 0.2 | 7.5 → 96, 6.5 → 91, 5.5 → 84, 4.5 → 76, 3.5 → 66, 2.5 → 56 |

## FB

**Attribute priorities** (highest sensitivity first): `Awareness`, `LeadBlock`, `ImpactBlocking`, `RunBlock`, `RunBlockPower`, `BreakTackle`, `Trucking`, `Catching`

| Attribute | Baseline | Sensitivity | Cap |
|---|---|---|---|
| `Awareness` | 68 | 0.7 | — |
| `LeadBlock` | 80 | 0.6 | — |
| `ImpactBlocking` | 78 | 0.55 | — |
| `RunBlock` | 72 | 0.55 | — |
| `BreakTackle` | 70 | 0.5 | — |
| `Catching` | 62 | 0.5 | — |
| `RunBlockPower` | 74 | 0.5 | — |
| `Trucking` | 76 | 0.5 | — |
| `Carrying` | 72 | 0.4 | — |
| `Strength` | 80 | 0.4 | — |
| `Acceleration` | 76 | 0.25 | — |
| `Speed` | 74 | 0.25 | 55–88 |
| `Agility` | 66 | — | — |
| `BCVision` | 64 | — | — |
| `ManCoverage` | 30 | — | 10–35 |
| `PassBlock` | 62 | — | — |
| `RunBlockFinesse` | 62 | — | — |
| `ThrowPower` | 30 | — | 10–50 |

**Production signal**

| Stat | Weight | Curve (value → implied overall) |
|---|---|---|
| RushYards | 0.5 | 500 → 92, 300 → 86, 150 → 79, 50 → 72, 10 → 66 |
| RushTD | 0.5 | 8 → 92, 5 → 86, 3 → 79, 1 → 72, 0 → 66 |

## WR

**Attribute priorities** (highest sensitivity first): `Awareness`, `CatchInTraffic`, `ShortRouteRunning`, `MediumRouteRunning`, `DeepRouteRunning`, `Catching`, `SpectacularCatch`, `Release`

| Attribute | Baseline | Sensitivity | Cap |
|---|---|---|---|
| `Awareness` | 66 | 0.7 | — |
| `CatchInTraffic` | 70 | 0.55 | — |
| `DeepRouteRunning` | 74 | 0.55 | — |
| `MediumRouteRunning` | 76 | 0.55 | — |
| `ShortRouteRunning` | 78 | 0.55 | — |
| `Catching` | 80 | 0.5 | — |
| `Release` | 70 | 0.5 | — |
| `SpectacularCatch` | 72 | 0.5 | — |
| `BCVision` | 66 | 0.4 | — |
| `KickReturn` | 66 | 0.4 | — |
| `Carrying` | 62 | 0.35 | — |
| `Agility` | 85 | 0.32 | — |
| `ChangeOfDirection` | 84 | 0.32 | — |
| `Speed` | 89 | 0.32 | 72–99 |
| `Acceleration` | 88 | 0.3 | — |
| `Jumping` | 84 | 0.3 | — |
| `Strength` | 58 | 0.3 | 45–82 |
| `BreakTackle` | 54 | — | — |
| `RunBlock` | 48 | — | 10–62 |
| `Stamina` | 84 | — | — |
| `Tackle` | 45 | — | 10–50 |
| `ThrowPower` | 30 | — | 10–55 |

**Production signal**

| Stat | Weight | Curve (value → implied overall) |
|---|---|---|
| RecYards | 0.5 | 1300 → 95, 1000 → 89, 700 → 82, 450 → 75, 200 → 67, 60 → 59 |
| Receptions | 0.25 | 85 → 95, 65 → 89, 45 → 82, 28 → 74, 12 → 66, 4 → 58 |
| RecTD | 0.25 | 13 → 95, 9 → 89, 6 → 82, 3 → 74, 1 → 66, 0 → 60 |

## TE

**Attribute priorities** (highest sensitivity first): `Awareness`, `RunBlock`, `Catching`, `CatchInTraffic`, `ShortRouteRunning`, `MediumRouteRunning`, `RunBlockPower`, `PassBlock`

| Attribute | Baseline | Sensitivity | Cap |
|---|---|---|---|
| `Awareness` | 66 | 0.7 | — |
| `RunBlock` | 68 | 0.55 | — |
| `CatchInTraffic` | 72 | 0.5 | — |
| `Catching` | 76 | 0.5 | — |
| `ImpactBlocking` | 68 | 0.5 | — |
| `MediumRouteRunning` | 70 | 0.5 | — |
| `PassBlock` | 62 | 0.5 | — |
| `RunBlockPower` | 68 | 0.5 | — |
| `ShortRouteRunning` | 74 | 0.5 | — |
| `DeepRouteRunning` | 60 | 0.45 | — |
| `LeadBlock` | 66 | 0.45 | — |
| `Release` | 62 | 0.45 | — |
| `SpectacularCatch` | 64 | 0.45 | — |
| `Strength` | 76 | 0.4 | — |
| `Speed` | 78 | 0.35 | 58–94 |
| `Acceleration` | 78 | 0.32 | — |
| `Agility` | 72 | 0.3 | — |
| `BCVision` | 58 | — | — |
| `Carrying` | 60 | — | — |
| `Jumping` | 78 | — | — |
| `LongSnap` | 40 | — | — |
| `ManCoverage` | 30 | — | 10–40 |
| `RunBlockFinesse` | 62 | — | — |
| `ThrowPower` | 30 | — | 10–50 |

**Production signal**

| Stat | Weight | Curve (value → implied overall) |
|---|---|---|
| RecYards | 0.5 | 800 → 94, 550 → 87, 350 → 80, 180 → 73, 60 → 66, 15 → 60 |
| Receptions | 0.25 | 60 → 94, 45 → 88, 30 → 81, 18 → 74, 8 → 66, 2 → 59 |
| RecTD | 0.25 | 9 → 94, 6 → 88, 4 → 81, 2 → 74, 1 → 67, 0 → 62 |

## OL

**Attribute priorities** (highest sensitivity first): `Awareness`, `RunBlock`, `PassBlock`, `PlayRecognition`, `RunBlockPower`, `RunBlockFinesse`, `PassBlockPower`, `PassBlockFinesse`

| Attribute | Baseline | Sensitivity | Cap |
|---|---|---|---|
| `Awareness` | 70 | 0.8 | — |
| `PassBlock` | 76 | 0.6 | — |
| `PlayRecognition` | 66 | 0.6 | — |
| `RunBlock` | 76 | 0.6 | — |
| `ImpactBlocking` | 74 | 0.55 | — |
| `PassBlockFinesse` | 72 | 0.55 | — |
| `PassBlockPower` | 74 | 0.55 | — |
| `RunBlockFinesse` | 70 | 0.55 | — |
| `RunBlockPower` | 76 | 0.55 | — |
| `LeadBlock` | 70 | 0.5 | — |
| `Strength` | 86 | 0.4 | — |
| `Acceleration` | 62 | 0.2 | 50–76 |
| `Agility` | 56 | 0.2 | 45–72 |
| `Speed` | 58 | 0.2 | 45–72 |
| `Carrying` | 40 | — | 10–55 |
| `Catching` | 40 | — | 10–45 |
| `ChangeOfDirection` | 54 | — | — |
| `Jumping` | 52 | — | 30–70 |
| `KickReturn` | 35 | — | 10–30 |
| `LongSnap` | 45 | — | — |
| `ManCoverage` | 30 | — | 10–25 |
| `Stamina` | 82 | — | — |
| `ThrowPower` | 30 | — | 10–45 |
| `Toughness` | 82 | — | — |
| `ZoneCoverage` | 30 | — | 10–25 |

**Production signal:** none — this group is rated from draft slot, awards, recruiting rating and depth-chart role.

## DL

**Attribute priorities** (highest sensitivity first): `Awareness`, `PlayRecognition`, `PowerMoves`, `FinesseMoves`, `BlockShedding`, `Tackle`, `Pursuit`, `HitPower`

| Attribute | Baseline | Sensitivity | Cap |
|---|---|---|---|
| `Awareness` | 68 | 0.75 | — |
| `PlayRecognition` | 70 | 0.7 | — |
| `BlockShedding` | 74 | 0.6 | — |
| `FinesseMoves` | 72 | 0.6 | — |
| `PowerMoves` | 76 | 0.6 | — |
| `Pursuit` | 74 | 0.5 | — |
| `Tackle` | 74 | 0.5 | — |
| `HitPower` | 74 | 0.45 | — |
| `Strength` | 84 | 0.4 | — |
| `Acceleration` | 78 | 0.35 | — |
| `Speed` | 74 | 0.35 | 55–90 |
| `Agility` | 68 | 0.3 | — |
| `Catching` | 40 | — | 10–50 |
| `ChangeOfDirection` | 64 | — | — |
| `Jumping` | 70 | — | — |
| `ManCoverage` | 30 | — | 10–45 |
| `RunBlock` | 30 | — | 10–55 |
| `Stamina` | 82 | — | — |
| `ThrowPower` | 30 | — | 10–45 |
| `Toughness` | 82 | — | — |

**Production signal**

| Stat | Weight | Curve (value → implied overall) |
|---|---|---|
| Sacks | 0.5 | 12 → 96, 9 → 91, 6 → 85, 3.5 → 78, 1.5 → 71, 0 → 63 |
| TacklesForLoss | 0.3 | 20 → 95, 15 → 90, 10 → 84, 6 → 77, 3 → 70, 0 → 62 |
| Tackles | 0.2 | 70 → 92, 55 → 87, 40 → 81, 25 → 74, 12 → 67, 3 → 60 |

## LB

**Attribute priorities** (highest sensitivity first): `Awareness`, `PlayRecognition`, `ZoneCoverage`, `ManCoverage`, `BlockShedding`, `Tackle`, `Pursuit`, `PowerMoves`

| Attribute | Baseline | Sensitivity | Cap |
|---|---|---|---|
| `Awareness` | 70 | 0.8 | — |
| `PlayRecognition` | 72 | 0.75 | — |
| `ZoneCoverage` | 66 | 0.65 | — |
| `ManCoverage` | 62 | 0.6 | — |
| `BlockShedding` | 70 | 0.55 | — |
| `FinesseMoves` | 58 | 0.5 | — |
| `PowerMoves` | 60 | 0.5 | — |
| `Pursuit` | 80 | 0.5 | — |
| `Tackle` | 80 | 0.5 | — |
| `HitPower` | 78 | 0.45 | — |
| `Catching` | 50 | 0.4 | — |
| `Agility` | 76 | 0.35 | — |
| `Speed` | 82 | 0.35 | 62–94 |
| `Strength` | 74 | 0.35 | — |
| `Acceleration` | 83 | 0.32 | — |
| `ChangeOfDirection` | 74 | — | — |
| `Jumping` | 78 | — | — |
| `RunBlock` | 30 | — | 10–50 |
| `Stamina` | 84 | — | — |
| `ThrowPower` | 30 | — | 10–45 |
| `Toughness` | 82 | — | — |

**Production signal**

| Stat | Weight | Curve (value → implied overall) |
|---|---|---|
| Tackles | 0.5 | 130 → 94, 105 → 89, 80 → 83, 55 → 76, 30 → 69, 10 → 61 |
| TacklesForLoss | 0.25 | 18 → 94, 13 → 89, 9 → 83, 5 → 76, 2 → 69, 0 → 62 |
| Sacks | 0.25 | 9 → 94, 6 → 89, 4 → 83, 2 → 76, 1 → 70, 0 → 64 |

## CB

**Attribute priorities** (highest sensitivity first): `Awareness`, `PlayRecognition`, `ManCoverage`, `ZoneCoverage`, `Press`, `Catching`, `Pursuit`, `Tackle`

| Attribute | Baseline | Sensitivity | Cap |
|---|---|---|---|
| `Awareness` | 68 | 0.8 | — |
| `PlayRecognition` | 70 | 0.75 | — |
| `ManCoverage` | 78 | 0.65 | — |
| `ZoneCoverage` | 76 | 0.6 | — |
| `Press` | 70 | 0.55 | — |
| `Catching` | 58 | 0.5 | — |
| `Pursuit` | 74 | 0.45 | — |
| `KickReturn` | 60 | 0.4 | — |
| `Tackle` | 58 | 0.4 | 35–80 |
| `Agility` | 87 | 0.3 | — |
| `ChangeOfDirection` | 86 | 0.3 | — |
| `Jumping` | 84 | 0.3 | — |
| `Speed` | 90 | 0.3 | 74–99 |
| `Strength` | 58 | 0.3 | 40–78 |
| `Acceleration` | 89 | 0.28 | — |
| `CatchInTraffic` | 48 | — | — |
| `HitPower` | 56 | — | — |
| `RunBlock` | 30 | — | 10–45 |
| `Stamina` | 84 | — | — |
| `ThrowPower` | 30 | — | 10–45 |

**Production signal**

| Stat | Weight | Curve (value → implied overall) |
|---|---|---|
| Interceptions | 0.4 | 6 → 94, 4 → 89, 2 → 82, 1 → 76, 0 → 68 |
| PassesDefended | 0.35 | 16 → 94, 12 → 89, 8 → 83, 5 → 76, 2 → 69, 0 → 62 |
| Tackles | 0.25 | 65 → 90, 50 → 85, 38 → 80, 25 → 74, 12 → 67, 3 → 60 |

## S

**Attribute priorities** (highest sensitivity first): `Awareness`, `PlayRecognition`, `ZoneCoverage`, `ManCoverage`, `Catching`, `Press`, `Tackle`, `HitPower`

| Attribute | Baseline | Sensitivity | Cap |
|---|---|---|---|
| `Awareness` | 70 | 0.8 | — |
| `PlayRecognition` | 72 | 0.75 | — |
| `ZoneCoverage` | 78 | 0.62 | — |
| `ManCoverage` | 70 | 0.6 | — |
| `Catching` | 58 | 0.5 | — |
| `HitPower` | 74 | 0.45 | — |
| `Press` | 60 | 0.45 | — |
| `Pursuit` | 80 | 0.45 | — |
| `Tackle` | 74 | 0.45 | — |
| `Agility` | 82 | 0.3 | — |
| `ChangeOfDirection` | 82 | 0.3 | — |
| `Jumping` | 82 | 0.3 | — |
| `Speed` | 87 | 0.3 | 70–98 |
| `Strength` | 64 | 0.3 | 42–82 |
| `Acceleration` | 87 | 0.28 | — |
| `BlockShedding` | 56 | — | — |
| `KickReturn` | 52 | — | — |
| `RunBlock` | 30 | — | 10–48 |
| `Stamina` | 84 | — | — |
| `ThrowPower` | 30 | — | 10–45 |

**Production signal**

| Stat | Weight | Curve (value → implied overall) |
|---|---|---|
| Tackles | 0.45 | 100 → 93, 80 → 88, 60 → 82, 40 → 75, 22 → 68, 6 → 60 |
| Interceptions | 0.3 | 6 → 94, 4 → 89, 2 → 82, 1 → 76, 0 → 69 |
| PassesDefended | 0.25 | 14 → 93, 10 → 88, 7 → 82, 4 → 75, 1 → 68, 0 → 62 |

## K

**Attribute priorities** (highest sensitivity first): `KickAccuracy`, `KickPower`, `Awareness`, `Speed`, `Acceleration`

| Attribute | Baseline | Sensitivity | Cap |
|---|---|---|---|
| `KickAccuracy` | 78 | 0.8 | — |
| `KickPower` | 80 | 0.75 | — |
| `Awareness` | 66 | 0.6 | — |
| `Acceleration` | 64 | 0.15 | — |
| `Speed` | 62 | 0.15 | 45–80 |
| `Agility` | 58 | — | — |
| `BlockShedding` | 30 | — | 10–35 |
| `Catching` | 40 | — | 10–50 |
| `HitPower` | 30 | — | 10–40 |
| `Jumping` | 56 | — | — |
| `LongSnap` | 30 | — | — |
| `ManCoverage` | 30 | — | 10–30 |
| `Pursuit` | 55 | — | 10–50 |
| `RunBlock` | 30 | — | 10–35 |
| `Stamina` | 80 | — | — |
| `Strength` | 48 | — | 25–65 |
| `Tackle` | 45 | — | 10–45 |
| `ThrowPower` | 30 | — | 10–55 |
| `Toughness` | 58 | — | — |

**Production signal**

| Stat | Weight | Curve (value → implied overall) |
|---|---|---|
| FieldGoalsMade | 0.45 | 26 → 95, 21 → 90, 16 → 84, 11 → 77, 6 → 70, 1 → 62 |
| FieldGoalPct | 0.4 | 92 → 96, 85 → 91, 78 → 85, 70 → 78, 60 → 70, 50 → 61 |
| LongFieldGoal | 0.15 | 56 → 95, 52 → 90, 48 → 84, 44 → 77, 40 → 70, 35 → 62 |

## P

**Attribute priorities** (highest sensitivity first): `KickPower`, `KickAccuracy`, `Awareness`, `Speed`, `Acceleration`

| Attribute | Baseline | Sensitivity | Cap |
|---|---|---|---|
| `KickPower` | 82 | 0.8 | — |
| `KickAccuracy` | 76 | 0.75 | — |
| `Awareness` | 64 | 0.6 | — |
| `Acceleration` | 64 | 0.15 | — |
| `Speed` | 62 | 0.15 | 45–80 |
| `Agility` | 58 | — | — |
| `BlockShedding` | 30 | — | 10–35 |
| `Catching` | 40 | — | 10–50 |
| `HitPower` | 30 | — | 10–40 |
| `Jumping` | 56 | — | — |
| `LongSnap` | 35 | — | — |
| `ManCoverage` | 30 | — | 10–30 |
| `Pursuit` | 55 | — | 10–50 |
| `RunBlock` | 30 | — | 10–35 |
| `Stamina` | 80 | — | — |
| `Strength` | 50 | — | 25–65 |
| `Tackle` | 45 | — | 10–45 |
| `ThrowPower` | 55 | — | 10–60 |
| `Toughness` | 58 | — | — |

**Production signal**

| Stat | Weight | Curve (value → implied overall) |
|---|---|---|
| PuntAverage | 1.0 | 47 → 95, 45 → 90, 43 → 84, 41 → 77, 39 → 69, 36 → 60 |

## Physical measurement curves

A verified measurement replaces the estimate and is then locked; calibration
may never move it.

- **40-yard dash → Speed (Acceleration uses the same curve at +0.02s):** 4.2s → 99, 4.3s → 99, 4.4s → 96, 4.5s → 92, 4.6s → 87, 4.7s → 82, 4.8s → 76, 4.9s → 70, 5.0s → 64, 5.2s → 52, 5.4s → 42
- **Bench press → Strength:** 40 reps → 99, 35 reps → 95, 30 reps → 90, 25 reps → 85, 20 reps → 79, 15 reps → 72, 10 reps → 64, 5 reps → 55
- **Vertical jump → Jumping:** 42" → 99, 39" → 95, 36" → 90, 33" → 84, 30" → 77, 27" → 70, 24" → 62
- **20-yard shuttle → Agility:** 3.95s → 99, 4.1s → 95, 4.25s → 90, 4.4s → 84, 4.55s → 77, 4.7s → 69, 4.9s → 58
- **Three-cone drill → Change of Direction:** 6.6s → 99, 6.8s → 95, 7.0s → 90, 7.2s → 84, 7.4s → 77, 7.6s → 69, 7.9s → 58

Values between listed points are interpolated linearly; values outside the
range clamp to the nearest endpoint.

