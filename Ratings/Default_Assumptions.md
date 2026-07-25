# Default Assumptions

What the generator does when information is missing, and the guardrails that
stop it producing nonsense. Every assumption listed here is also reported
per player in `Output/Generation_Report.txt`.

## Missing information

| Missing | What happens |
|---|---|
| Jersey number | Keeps the number of the player being replaced |
| Height | Keeps the replaced player's height |
| Weight | Keeps the replaced player's weight |
| Class year | Keeps the replaced player's class and redshirt status |
| All performance evidence | Target overall falls back to **75** (an average FBS starter), then the class-year low-confidence cap applies. Confidence is reported as **Low** |
| Some evidence | Available signals are re-weighted to sum to 1 — a player with only stats is rated on stats, not penalised for the absent draft slot |
| Position not recognised | Player is skipped and listed in the report; add the label to `data/PositionMappings.json` |
| Combine numbers | Physical attributes are estimated from position baseline, talent and physique instead |

Nothing is silently invented: every substituted default appears in the
report under the player's name.

## Guardrails

### Position caps

Hard bounds per position group, applied after every calibration pass
(full list in `Position_Formulas.md`). The cases the milestone called out:

- **Offensive linemen cannot be fast.** OL speed is capped at **72**,
  acceleration 76, agility 72. A 99-overall left tackle still runs like a
  left tackle. (Real-game LTs: speed median 62–66, max 77.)
- **Kickers and punters cannot tackle.** K/P tackling is capped at **45**,
  hit power 40, strength 65, block shedding 35. An All-American kicker
  generates as an elite *kicker* — high kick power and accuracy, ordinary
  everything else.
- Quarterbacks cap at 55 tackling, receivers and backs at 40–55 run
  blocking, and every non-kicker caps low on kick power.

### Class-year caps

A true freshman must not read like a fifth-year starter, regardless of how
good the evidence is:

| Class | Awareness cap | Play recognition cap | Low-confidence overall cap |
|---|---|---|---|
| Freshman | 78 | 80 | 68 |
| Sophomore | 88 | 90 | 74 |
| Junior | 95 | 96 | 78 |
| Senior | 99 | 99 | 80 |

Experience-driven attributes (awareness, play recognition, throwing under
pressure, block shedding, coverage, play action) also shift by class
(−8 freshman … +4 senior), with **+3** for a player who has already used a
redshirt.

*Worked example:* 2013 Jameis Winston — a Heisman winner and future #1
overall pick — generates at **97 overall** but his awareness is held to
**78** because he was a redshirt freshman. 2005 Vince Young, a junior with
comparable evidence, generates at 96 with awareness **95**.

### Draft slot sets a floor

First-round picks are elite college players, so the draft signal sets a
minimum the weighted blend cannot pull below: the whole first round floors
at **91+**, and top-10 picks at 94–97. A major award floors 6 points below
its own implied overall. Both are reported in the player's reasons whenever
they bind. Later rounds step down normally (pick 40 → 88, pick 100 → 82).

### Depth-chart consistency

A player marked `Backup`, `Reserve` or `Walk-on` may not out-rate the best
`Starter` in the same position group. Violators are regenerated one point
below the starter and the change is reported.

The exception is deliberately narrow: a backup **is** allowed to exceed the
starter when they have **High** confidence backed by a draft slot or a major
award — the real case of a future first-round pick sitting behind a senior.

### Global bounds

Every attribute is finally clamped to **10–99**, and EA's formula clamps
overall to **12–99**.

## Reported, not hidden

If sanity caps prevent reaching the intended overall, the report says so
explicitly for that player:

```
Overall settled at 71 rather than 74: sanity caps for OL prevented the
remaining adjustment.
```

Likewise every locked measurement (`SpeedRating fixed at 92 by a verified
40-yard dash (4.49s)`), every physique adjustment, every class-year
reduction and every depth-chart cap is written into the report.

## Things this model deliberately does not do

- **Choose archetypes.** A player inherits the archetype (`PlayerType`) of
  the roster slot they replace.
- **Vary by season.** Draft position and career awards are retrospective; if
  you supply them for a player's freshman year, that year will look like
  their peak. Supply per-season evidence for per-season accuracy.
- **Generate equipment, portraits, faces or traits.**
- **Model injuries, morale or development traits.**
