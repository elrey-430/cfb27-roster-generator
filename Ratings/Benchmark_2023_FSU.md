# Fidelity benchmark — generated vs. manual 2023 Florida State

The generator's own 2023 Florida State roster, scored field by field against
a Florida State dynasty a person built by hand in the roster editor, and
against the roster **the game itself ships** for Florida State.

Two reference points matter, and they are not the same thing:

| Reference | What it is | What it proves |
|---|---|---|
| The manual export | One person's recreation of the same team | Where two independent attempts disagree |
| EA's own Florida State | The untouched base save's team 27 | What a real roster at this program looks like |

The manual export is a peer, not an authority. Where the two disagree, the
game breaks the tie.

## Method

- 85 players per team in both files; matched by name after stripping
  punctuation and generational suffixes, then a narrow fuzzy pass for typos
  (`Mastromanno`/`Mastromonno`, `Toafili` first-name spelling).
- The 10 slots the generator fills as depth are excluded — they are
  deliberately synthetic and have no counterpart to compare against.
- **57 historical players matched.** 17 appear only in the generated roster
  and 27 only in the manual one; the manual creator kept more of EA's
  original players and included some the dataset does not.

## What it found

### 1. The flagship deliverable had never had generated ratings

`Output/2023_Florida_State_CFB27.csv` was produced with `--ratings inherit`,
and neither `Tests/2023_FSU_Input.csv` nor
`HistoricalData/2023/FloridaState.json` carried a single evidence column.
Every player in the shipped example was wearing the ratings of the fictional
player they replaced — Jared Verse was carrying some EA-generated end's
attributes. Rating generation worked; nothing was exercising it.

Running the documented path with no evidence rates **every player 75**, the
model's reference talent, which is the correct answer to "I don't know" but
makes for a useless worked example.

**Fixed.** The 2023 Florida State input now carries researched evidence:
all ten 2024 NFL Draft selections with their overall pick numbers, the
undrafted signing, All-ACC and All-America honours, season statistics for
the fifteen players they are documented for, and a depth-chart role for all
75.

### 2. Roles derived from free-text notes mislabelled starters

The first pass inferred each player's role from the dataset's `Notes`
column. A note about a jersey number is not a note about a depth chart, so
Akeem Dent (starting safety) and Fentrell Cypress (starting cornerback) both
came out as `Reserve` and were generated twenty points light.

**Fixed.** Roles now come from the 2023 two-deep: 27 starters, 25 backups,
23 reserves.

### 3. Award scores are calibrated for position players, not specialists

Award and draft scores sit on one shared scale, but the game's positions do
not share a range. Across the base save:

| Position | n | median | p95 | max |
|---|---|---|---|---|
| WR | 2064 | 70 | 83 | **99** |
| CB | 1375 | 70 | 84 | **97** |
| K | 360 | 71 | 81 | **90** |
| P | 326 | 70 | 81 | **86** |

Alex Mastromanno led the nation in punting and made first-team All-American,
and the generator rated him **91 — better than any punter in the game**.

**Fixed.** `positionOverallCaps` in `RatingModels.json` caps each position
group at the highest overall the game itself carries there. It is the
observed maximum, not a haircut: Ryan Fitzgerald, a first-team All-American
kicker, still generates at 89 against a kicker ceiling of 90.

### 4. The middle of the roster collapsed — no notion of program standing

The headline finding. Role, awards and statistics record what a player did;
none of them record *where*. A backup cornerback at a playoff program and a
backup cornerback at the worst team in the country were rated identically,
from the same league-average role score. Because a role score is a single
flat number, the generated roster also clumped — 69, 69, 68, 64, 64 — where
a real roster descends smoothly.

Overall by roster rank:

| Rank | EA's own FSU | Manual | Generated (before) | Generated (after) |
|---|---|---|---|---|
| 1 | 92 | 95 | 92 | 92 |
| 10 | 82 | 87 | 83 | 85 |
| 20 | 79 | 84 | 76 | 80 |
| **30** | **76** | 82 | **69** | **74** |
| **40** | **74** | 76 | **69** | **74** |
| 60 | 72 | 72 | 64 | 68 |
| 85 | 64 | 64 | 60 | 60 |
| **mean** | **74.5** | 77.0 | 70.4 | **73.2** |

**Fixed.** `RosterDepthModel.ProgramAdjustment` measures the donor team's
median overall against the league median (69) and shifts thinly evidenced
players by the difference, capped at ±8. Florida State scores +5. The shift
fades as evidence strengthens — full at Low confidence, half at Medium, none
at High — so a first-round pick is rated on their own record. **No new input
is required from the user:** the donor roster already encodes the program's
tier.

## Score

Mean absolute deviation from EA's own Florida State roster, rank by rank:

| | deviation |
|---|---|
| Generated, before this milestone | 4.48 |
| **The manual human recreation** | **3.02** |
| Generated, after | **2.01** |

The generated roster now tracks the shape of the game's own roster more
closely than the hand-built one does — the manual export is inflated at the
top (33 players at 80+ against the game's 16) and converges below rank 40.

`RosterFidelityTests` pins this: the deviation must stay at or under 3.00,
inside the human benchmark's score.

Where both sides carried real evidence, they agree closely — Keon Coleman
−1, Darius Washington +1, D'Mitri Emmanuel +1, Tate Rodemaker −2.

## Documented gaps

Differences that are **not** defects on this side:

- **Hometowns.** Every manual-export hometown that can be checked is still
  the donor player's value — the manual creator never set the field. The
  generator writes real hometowns, so a mismatch here favours the generator.
- **Archetypes.** 35 of the manual export's 85 players carry an overall that
  matches a *different* archetype, the signature of an archetype changed
  without recomputing the overall (Milestone 5). Disagreement is expected.
- **Weights and jersey numbers.** The two datasets disagree on roughly 40%
  of jersey numbers. Both sides are unverified in places; the dataset still
  carries "verify jersey" notes on about 25 players. Neither side is
  authoritative and this is left open.

Still open as a modelling question:

- **Draft slot as a proxy for college quality.** Jordan Travis generates at
  83 against the manual export's 90. He was a fifth-round pick because of a
  November leg injury, not because of how he played in 2023. Draft position
  is the strongest single signal in the model and it is measuring the wrong
  season for injured players. There is no fix in this milestone; the
  `Notes` column records the injury but nothing reads it.

## Reproducing

```
dotnet run --project src/RosterGenerator.Cli -- generate \
    --dynasty <your base export> --roster Tests/2023_FSU_Input.csv \
    --output Output/2023_Florida_State_CFB27.csv \
    --report Output/2023_Florida_State_Report.md
```

The shape score is asserted automatically by `RosterFidelityTests` against
the committed donor fixture, which is EA's untouched Florida State roster.
