# CFB27 Player Table CSV Schema

This document is the ground truth for what the Historical CFB27 Roster
Generator knows about the `Player.csv` table exported from a CFB27 dynasty
save (table index 152, `0152_Player.csv` in a full export). It mirrors the
constants in `src/RosterGenerator.Core/Schema/` — **change them together**.

## Provenance and evidence standard

Everything marked *confirmed* below comes from a controlled diff of three
real save exports against a common baseline:

1. `DYNASTY-JUL24-BASE` — the baseline save.
2. `DYNASTY-2023FSU` — the same save with Florida State's roster fully
   replaced with the real 2023 FSU roster (names, jerseys, attributes,
   height, class, redshirt status, weight).
3. `DYNASTY-JUL24-MultipleTeamMultiplePlayerEdit` — the baseline with a
   small targeted edit: one player traded between two teams, two players
   renamed.

Re-running the base-vs-multi-edit diff during Milestone 1 development
reproduced the published findings exactly: the trade touched only
`TeamIndex`, `PrevTeamIndex`, `PLYR_PREVTEAMID`, `BaseNILValue`,
`CurrentNILCompensation`; the renames touched only `FirstName`/`LastName`
(plus a `PLYR_COMMENT` change on one of the two renames — since explained: the
game rewrites it to the commentary index of the new surname, see below).

Anything **not** covered by those diffs is labeled *unconfirmed
observation* — statistically profiled from 16,257 live rows of the base
export, but never verified by a controlled edit. Do not treat unconfirmed
entries as safe to write.

## File format

| Property | Value |
|---|---|
| Encoding | ASCII (no BOM observed) |
| Line endings | CRLF (`\r\n`), including after the last row |
| Quoting | None observed in any cell; the exporter only quotes when a value contains `,` `"` or a newline |
| Columns | 286 |
| Rows (base save) | 16,500 = 16,257 live players + 243 empty pool slots |

### Export bookkeeping columns

| Column | Type | Purpose |
|---|---|---|
| `_tableIndex` | int | Source table index in the save (always `152`) |
| `_tableName` | text | Source table name (always `Player`) |
| `_row` | int | **Primary key.** Unique and stable within one export; use it to address players |
| `_isEmpty` | bool | `true` marks an unused pool slot — every other column is blank on such rows |

> **Known data quirk (confirmed by inspection):** the genuine base save
> contains two *live* rows (`_row` 3082 and 7817) with empty names, team 255
> and zeroed attributes — engine placeholder junk. The validation layer
> therefore treats anomalies already present in the loaded file as
> *warnings* and only blocks anomalies *introduced by an edit*.

---

## Group 1 — Core identity fields (confirmed, safe to write)

Confirmed by the FSU full-roster replacement and the rename diff. A pure
rename does **not** require touching any other field.

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `FirstName` | text | non-empty for real players | Plain text, safe to edit in isolation |
| `LastName` | text | non-empty for real players | Plain text, safe to edit in isolation |
| `JerseyNum` | int | 0–99 (full range observed) | |
| `Height` | int | Raw inches. 69–78 observed for FBS players; league-wide observed range 57–82 (plus 0 on junk rows) | No conversion needed |
| `SchoolYear` | enum | `Freshman` `Sophomore` `Junior` `Senior` | |
| `RedshirtStatus` | enum | `Eligible` `Previous` `Ineligible` | |
| `Position` | enum | `QB` `HB` `FB` `WR` `TE` `LT` `LG` `C` `RG` `RT` `LE` `RE` `DT` `LOLB` `MLB` `ROLB` `CB` `FS` `SS` `K` `P` | 21 values observed |
| `OverallRating` | int | 0–99 | **Derived**: EA computes it as a linear function of the attributes, keyed by position and archetype (`data/OverallFormulas.json`; 99.33% exact over a full export). Never set it independently of the attributes |
| Ratings block (57 columns) | int | 0–99 (both bounds observed in real data) | Full list below |

### The 57 numeric rating columns

`AccelerationRating` `AgilityRating` `AwarenessRating` `BCVisionRating`
`BlockSheddingRating` `BreakSackRating` `BreakTackleRating` `CarryingRating`
`CatchInTrafficRating` `CatchingRating` `ChangeOfDirectionRating`
`ConfidenceRating` `DeepRouteRunningRating` `FinesseMovesRating`
`HitPowerRating` `ImpactBlockingRating` `InjuryRating` `JukeMoveRating`
`JumpingRating` `KickAccuracyRating` `KickPowerRating` `KickReturnRating`
`LeadBlockRating` `LongSnapRating` `ManCoverageRating`
`MediumRouteRunningRating` `OverallRating` `PassBlockFinesseRating`
`PassBlockPowerRating` `PassBlockRating` `PlayActionRating`
`PlayRecognitionRating` `PowerMovesRating` `PressRating` `PursuitRating`
`ReleaseRating` `RunBlockFinesseRating` `RunBlockPowerRating`
`RunBlockRating` `ShortRouteRunningRating` `SpectacularCatchRating`
`SpeedRating` `SpinMoveRating` `StaminaRating` `StiffArmRating`
`StrengthRating` `TackleRating` `ThrowAccuracyDeepRating`
`ThrowAccuracyMidRating` `ThrowAccuracyRating` `ThrowAccuracyShortRating`
`ThrowOnTheRunRating` `ThrowPowerRating` `ThrowUnderPressureRating`
`ToughnessRating` `TruckingRating` `ZoneCoverageRating`

> ⚠️ Two columns end in `Rating` but are **not** numeric ratings:
> `RunningStyleRating` (animation enum, e.g. `LongStrideLoose`) and
> `ProspectStarRating` (recruiting stars enum, e.g. `THREE_STAR`). They are
> deliberately excluded from the numeric list and the 0–99 validation rule.

---

## Group 2 — Offset-encoded columns (encoding RESOLVED)

| Column | Encoding |
|---|---|
| `Weight` | **Stored value = real pounds − 160.** Valid stored range 0–240 = 160–400 lb (both bounds observed league-wide). Write via `Player.WeightPounds` / `RosterEditSession.SetWeightPounds`; the `WeightRange` validation rule enforces the stored range. The earlier weight-curve/spline hypothesis is refuted — it is a plain fixed offset (the same convention EA franchise files use). |

**Evidence (2026-07, replacing the earlier "unresolved" status):**

1. Correlating the manually edited `DYNASTY-2023FSU` save against real
   listed 2023 weights across 47 matched players: residuals of
   `stored − (pounds − 160)` center on 0 (mean 1.1, median 1), with exact
   zero residuals wherever the editor's input value is known precisely
   (e.g. Jordan Travis 212 lb → 52; Jared Verse 260 lb → 100; Keon Coleman
   215 lb → 55). Non-zero residuals track discrepancies between roster
   listings, not encoding noise.
2. Decoding the full 16,257-player base save with +160 yields textbook
   position averages — QB 203 lb, WR 187 lb, OL 299–307 lb, DT 296 lb,
   CB 185 lb, K 189 lb — and a plausible league range of 160–400 lb.

---

## Group 2b — Archetype and hometown (confirmed writable, Milestone 5)

| Column | Type | Status |
|---|---|---|
| `PlayerType` | enum (59 values) | **Writable, with a required companion recompute.** Selects which of EA's overall formulas applies, so `OverallRating` must be recomputed whenever it changes |
| `PLYR_HOME_TOWN` | text | **Writable.** Free text; 3,031 distinct values in the base save |
| `PLYR_HOME_STATE` | enum (51 values) | **Writable.** The 50 US states in PascalCase (`NewYork`, `WestVirginia`) plus `NonUS` |

**Evidence.** A manually edited save (2023 FSU) was compared against an
untouched base export:

- All 85 edited players carry valid archetype strings, and hometowns were
  set to real values (Travis → West Palm Beach/Florida, Benson →
  Greenville/Mississippi, Coleman → Opelousas/Louisiana), so both fields
  clearly accept writes.
- Every `PLYR_HOME_STATE` value used stays inside the base save's 51-value
  domain; two new towns appear that the base save never had, confirming the
  town field is free text.

**The archetype companion dependency.** Checking each player's stored
overall against their archetype's formula:

| Save | Overall agrees with its own archetype |
|---|---|
| Untouched base (16,255 players) | **99.3%** |
| Manually edited 2023 FSU (85) | **56%** — and 35 of 85 match a *different* archetype at the same position |

That is the signature of a **stale overall**: the editor wrote the new
`PlayerType` but never recomputed `OverallRating`, leaving the value that
belonged to the pre-edit archetype. This tool always recomputes with the new
archetype's formula, and the `ArchetypeConsistency` validation rule reports
the defect in any file exhibiting it.

**Archetype must also suit the position.** The same save contains an LOLB
carrying `MLB_PassCoverage`, which is not an LOLB archetype — the editor
does not enforce compatibility. The validation rule does.

*Unconfirmed observation:* the ~94 `PT_*` boolean columns correlate with
archetype but are **not** a strict mirror (only 2 of 42 varying columns map
1:1; the game itself leaves ~20% of `HB_ElusiveBack` players without their
flag). They are treated as player traits and left untouched.

---

## Group 3 — Identity-derived fields (conditional on edit type)

Confirmed behavior: these change as a *side effect* of identity generation,
not of name text. A controlled in-game rename left all of them untouched.

| Column | Behavior | Tool policy |
|---|---|---|
| `PLYR_ASSETNAME` | Auto-derived from name at *generation* time (e.g. `HowardJamari_7025`); a plain rename does NOT regenerate it | Untouched on **Rename**; must be supplied by the caller on **ReplaceIdentity** |
| `GenericHeadAssetName` | Same derivation family (e.g. `Generic_0877_P_T0042_H_6_3`) | Same as above |
| `PLYR_PORTRAIT` | Portrait id tied to identity, not name text | Same as above |
| `PLYR_COMMENT` | Index into the recorded commentary audio — which name the announcers say. 0 = never said, held by 20% of an untouched save | **Written from the surname.** The game does the same on a rename: two in-game renames re-exported showed it rewritten to exactly the index for the new surname, 2 of 2, including 0 for an unrecorded name. Mapping measured from 146,295 rows across nine game-generated saves (`data/CommentaryIds.json`); validated by `CommentaryConsistencyRule`, which rejects an index belonging to a different name |

Because "cosmetic rename" and "replace with a different real player" are
both valid operations with different correct outcomes, the tool makes the
distinction an **explicit mode** (`EditIntent.Rename` vs
`EditIntent.ReplaceIdentity` recorded by `RosterEditSession`) rather than
inferring it. The `IdentityChangeConsistency` validation rule enforces the
declared intent.

---

## Group 4 — Team-change companion fields (required together)

Confirmed by both observed transfer edits. Changing `TeamIndex` without the
companions leaves stale team history and stale NIL money in the save.

| Column | Behavior on a team change | Sentinel |
|---|---|---|
| `TeamIndex` | Set to the new team | `255` = no team |
| `PrevTeamIndex` | Set to the OLD team index | `255` = no previous team — do not leave a stale `255` after a change |
| `PLYR_PREVTEAMID` | Set to the OLD team index, mirroring `PrevTeamIndex` | `0` = never transferred — **note the two fields use different sentinels** |
| `BaseNILValue` | Reset to `0` (observed in both transfer cases) | |
| `CurrentNILCompensation` | Reset to `0` (observed in both transfer cases) | |

### `PLYR_PREVTEAMID` native domain — RESOLVED (Milestone 6)

The field holds the **school** a transfer came from, as that school's
`TEAM_ORIGID` from the Team table — a presentation-level id spanning more
schools than the dynasty's own team list, and **not** a `TeamIndex`. This
explains the out-of-range values (1009–1235) noted as an open question
through Milestone 5.

Evidence, all from the untouched base save:

- 133 of the 135 distinct non-zero values are a `TEAM_ORIGID` in the same
  save. `TEAM_ORIGID` runs 1100–1504 over 143 teams and is unique per team.
- Resolving Florida State's 20 non-zero players gives real, plausible
  schools: Auburn, Notre Dame, Texas A&M, Duke, Troy, Texas, Purdue…
- The two values below the Team table's range are dominated by **`1009`**
  (363 players league-wide) — a school the dynasty does not model, which is
  what an FCS transfer carries.

**The two previous-team fields do not move together.** `PrevTeamIndex` reads
`255` for *every* player in an untouched save, including those 20 Florida
State transfers. Only `PLYR_PREVTEAMID` records where a player came from at
save creation; `PrevTeamIndex` appears to track in-dynasty movement.

`RosterEditSession.SetPreviousSchool` writes it and deliberately leaves
`PrevTeamIndex` alone. The converter writes it for every player, including
`0` for those who never transferred — otherwise a player inherits the donor
slot's transfer history. Schools the dynasty does not carry are written as
`1009` and reported.

`RosterEditSession.TransferPlayer` applies all five updates atomically, and
the **`TeamChangeConsistency`** validation rule (a distinct, named rule)
flags any `TeamIndex` change whose companions were not updated.

### Valid team indices

From the save's main `Team` table (`2225_Team.csv` in the base export):
FBS teams occupy `TeamIndex` 0–137; the five generic FCS squads and the
"no team" sentinel share `255`. The validator checks the 0–255 range
always, and exact membership when the caller supplies the save's team list.

---

## Group 5 — Derived/computed league-wide arrays (out of scope, do not hand-edit)

The export contains ~200 `Player[]` array tables (`0249_Player[].csv` etc.)
holding `Player0`…`Player84`-style slot references per team. A change to
just two players' `TeamIndex` reshuffled these arrays across dozens of
teams — strong evidence they are league-wide sorted/indexed lists (rating-
or ID-sorted roster/scouting orders) that the game recomputes on load.

**Tool policy:** these tables are not loaded, not edited and not exported by
this library. The roster import path relies on the import tool / game to
regenerate them. Treat any future need as a *full-table recompute problem*,
not a diff-and-patch problem. Open research item; explicitly out of
Milestone 1 scope.

---

## Group 5b — Head assets (confirmed by census)

`GenericHeadAssetName` holds all three of the game's head systems despite its
name. Across the 16,257 live players of a base save:

| Kind | Players | Shape |
|---|---|---|
| Real person's scan | **9,011** | `Unique_AbasiriJide_653` |
| Generated face | **7,244** | `Generic_3759_P_T0178_D_2_4` |
| Create-a-face | 1 | `Custom_Head_CAF` |
| Other | 1 | `3_MorphHead` |

Two companion columns move with it:

- **`PLYR_PORTRAIT`** is the number embedded in a generated name — they agree
  on 7,243 of 7,244 rows, so the pair must be written together.
- **`PLYR_ASSETNAME`** is set on **all 9,011** scanned players and matches the
  scan's own name (`Unique_<PLYR_ASSETNAME>`) on 8,681 of them. It is blank on
  4,100 generated players and set on the other 3,144, so either state is
  attested for a generated face.

The generated name decomposes as
`Generic_<portrait>_P_T<texture>_<family>_<skinTone>_<variant>`.

**The sixth segment is EA's skin tone — confirmed.** The `CharacterVisuals`
blob carries a bare `skinTone` integer beside the gear, and decoding every
player's visuals reference in a base save gives 3,144 generic-headed players
where both the segment and the field are readable. The two **agree 3,144 times
and disagree zero times.**

Stronger still: **a given generated head is only ever used at one tone.** 1,607
distinct heads appear in a base save and not one of them is used at two
different tones. Head and tone are a single choice, not two — which is why the
tool can set a player's appearance by choosing their face and never has to
write the visuals table. `CharacterVisualsTable` exposes `GetSkinTone` and
deliberately has no setter.

Tone is `1` (lightest) to `8` (darkest), distributed across a base save as
1,939 / 1,612 / 518 / 760 / 1,068 / 1,966 / 2,006 / 2,287. Generated faces
cover tones 1–7 with 349 / 294 / 77 / 104 / 246 / 272 / 265 distinct heads.

The remaining segments are **not** confirmed. The family letter
(`D`/`H`/`M`/`T`) is not the tone and does not track height, weight, position
or `bodyType`; the `T` segment has 59 distinct values; the trailing digit runs
1–4 and is not `bodyType` either. Nothing depends on any of them.

**Tool policy.** A replaced player used to inherit the slot's head, and 71 of
the 85 slots on a typical team carry a scan — so most of a recreated roster
wore the recognisable faces of present-day players under other people's names.
Those slots are now given a generated face **taken from the same export**, so
no asset name is ever invented, and `PLYR_ASSETNAME` is cleared with it. Slots
already carrying a generated face are left alone, as are slots no historical
player took over: a leftover player is still themselves.

Because the head carries the tone, the replacement is chosen **at the tone the
slot already had**, so swapping a scan for a generated face does not silently
change how a player looks. The roster CSV's optional `SkinTone` column
overrides that per player. A tone is never inferred from a name, a hometown or
a position — a blank cell means "keep what the slot had", not "work it out".

The create-a-face path carries semantic, human-readable slots inside
`CharacterVisuals` — `Head_hair_ShortCurly`, `Head_FacialHair_Animal`,
`Head_Face_Tone03_Set05`, plus eyes, nose, jaw, chin, cheek, ears and body
type. Exactly one character in the observed save uses it, so whether players
can use that path is **an open question**, and it is the obvious route to
era-appropriate hair if they can.

---

## Group 6 — Equipment lives in another table (CharacterVisuals, confirmed)

Uniform and equipment are **not** in the Player table. The evidence is a
controlled experiment: one dynasty exported twice, identical except that
eight Florida State cornerbacks had their helmets changed in the community
roster editor. Of **2,273 files, exactly one differed** —
`0130_CharacterVisuals.csv`. The Player table was untouched.

### The table

| Column | Meaning |
|---|---|
| `_tableIndex`, `_tableName`, `_row`, `_isEmpty` | The usual bookkeeping. `_row` equals the row's position in every export seen |
| `Overflow` | 32 zero bits on every populated row; unused in practice |
| `RawData` | A JSON document, 479–3,367 characters, holding everything the character wears |

Of 16,000 rows, 12,586 are populated; 3,414 are `_isEmpty=true` with an
empty `RawData`.

### Linking a player to their visuals

The Player table's `CharacterVisuals` column is a 32-character string of
ASCII `0`/`1` — the exporter's spelling of a 32-bit value:

```
00100001000001000000100000010010
└─── tag 8452 ───┘└─── row 2066 ───┘
```

- **Low 16 bits** — the `_row` id in the CharacterVisuals table.
- **High 16 bits** — a constant `8452` tag naming the table pointed at.

Confirmed by decoding the column for all 16,500 players and intersecting
with the eight changed visuals rows: they resolved to exactly the eight
edited cornerbacks, with no other matches.

### Inside `RawData`

Three loadouts: `Head`/`Head`, `Base`/`None`, and `None`/`PlayerOnField`.
The uniform is in **`PlayerOnField`**, which carries 32 elements — one per
slot, covering helmet, face mask, visor, gloves, shoes, sleeves, spats,
backplate, shoulder pads, towel, mouthpiece, neck pad, plus pants and jersey
style. Only two are decoded so far:

| Slot | `slotType` | Example |
|---|---|---|
| Helmet | `HeadWear` | `GearHelmet_RevolutionSpeed` |
| Face mask | `FaceMask` | `GearFaceMask_revospeed2bar` |

The face mask is element 0 in 12,126 of 12,156 rows, and 16 rows omit its
`slotType` key entirely — so match on the `GearFaceMask_` prefix, not on
position.

### Tool policy: patch, never re-serialize

`GearHelmet_` and `GearFaceMask_` each occur **exactly once** in every
populated row — verified across all 12,156 rows carrying a helmet. Both
values are therefore replaced by targeted string substitution, leaving every
other byte of the blob alone. Most of the JSON's meaning is unknown, and
re-serializing it would risk changing far more than was asked.

**Helmet and face mask are written together.** Masks are moulded to a
particular shell, and the mask changed alongside the helmet in all eight
demonstrated edits.

### Two things observed but deliberately not imitated

- **The editor fills in Head-loadout defaults.** Five of the eight rows
  gained `Head_SkinDetails_None` and `NeckTattoo_None` because their `Head`
  loadout had no `loadoutElements` key at all. That is the community
  editor's round-trip, not part of a helmet change; this tool leaves it.
- **430 populated rows carry no helmet.** They are skipped rather than
  guessed at.

### What the base save does and does not contain

A base save carries **10 helmet models, all modern**:

| Helmet | Players |
|---|---|
| `GearHelmet_Speed_Flex` | 8,761 |
| `GearHelmet_SchuttF7` | 1,531 |
| `GearHelmet_Axiom` | 920 |
| `GearHelmet_VicisZero2` | 406 |
| `GearHelmet_SchuttF7Pro` | 312 |
| `GearHelmet_VicisZero2Trench` | 113 |
| `GearHelmet_AirXP` | 82 |
| `GearHelmet_LightGladiator` | 27 |
| `GearHelmet_LightLS2` | 3 |
| `GearHelmet_VicisZero1` | 1 |

…and 88 face mask models, family-scoped by name.

### The era rule: brand carries over, the model changes

A player's current helmet names a manufacturer, and the era moves them to
that manufacturer's model of the day. Applying one helmet to a whole team
would flatten a squad that was never uniform, and the demonstration
deliberately included players in several shells.

| Wearing now | Brand | 2010–2016 | 2000–2009 |
|---|---|---|---|
| Axiom, SpeedFlex | Riddell | Revolution Speed | Revolution |
| F7, F7 Pro | Schutt | Air XP PRO VTD | *not yet demonstrated* |
| Zero1, Zero2, Zero2 Trench | Vicis | *fallback* | *fallback* |
| Gladiator, LS2 | Light | *fallback* | *fallback* |

Vicis shipped nothing until 2016 and Light little earlier, so those players
have no same-brand model to move to and take the era's fallback shell.

**Six of the eight demonstrated edits follow this rule exactly.** Two do not,
and are recorded rather than fitted to:

- **Jamari Howard**, Schutt F7 Pro → Riddell Revolution Speed. The stated
  rule sends any Schutt to the Air XP.
- **Charles Lester III**, Light Gladiator → Riddell Revolution. The other
  five brand-less players went to the Revolution *Speed*; the plain
  Revolution is the 2000s shell, so this reads as a vocabulary
  demonstration rather than a 2014 assignment.

Both are open questions for the next demonstration, not assumptions in the
data file.

**Retro helmets are not among them.** `GearHelmet_RevolutionSpeed` and
`GearHelmet_Revolution` appear on **zero** of 12,586 players, as do the masks
`revospeed2bar`, `RevoNormal` and `2Bar`. The assets exist and the editor can
select them, but nothing wears them by default — so the period catalogue
**cannot be mined from a save**. Each era's helmets must be demonstrated in
the editor and read out of the diff. That is why `data/EquipmentEras.json`
lists only confirmed names, and why a season no era covers changes nothing:
writing an asset name the game may not carry is a guess with a broken helmet
at the end of it.

### Confirmed period vocabulary

| Helmet | Face mask | Era |
|---|---|---|
| `GearHelmet_RevolutionSpeed` | `GearFaceMask_revospeed2bar` | 2010s |
| `GearHelmet_Revolution` | `GearFaceMask_RevoNormal` | 2000s |
| `GearHelmet_AirXP` | `GearFaceMask_2Bar` | 2010s |

Known gaps, in rough order of value:

1. **A Schutt shell for the 2000s** (Air Advantage or DNA) — without it, a
   Schutt wearer in 2005 falls back to a Riddell.
2. **Position-specific masks.** Every player in an era currently gets the
   same 2-bar, so a kicker and a nose tackle end up identical. The base save
   shows the game distinguishes them (`SpeedFlexKicker`, `F7Kicker`,
   `Speedflex3BarLB`), so the retro shells very likely have equivalents.
3. **Anything before 2000.**

---

## Group 7 — The season year lives in SeasonInfo (confirmed in-game)

Not a Player column at all, and not present in an export: `SeasonInfo` is a
**one-row table** inside the save, and it holds the year the game puts on
screen while the dynasty is played.

| Column | Base save | Range | Meaning |
|---|---|---|---|
| `CurrentSeasonYear` | 2026 | 0–4095 | The season being played |
| `BaseCalendarYear` | 2026 | 0–4096 | The anchor the dynasty counts forward from |
| `CurrentYear` | 0 | 0–100 | Offset into the dynasty, **not** a calendar year |
| `MaxYears` | 30 | 0–100 | How far the dynasty runs |

**Confirmed by loading the game.** A save built from a real dynasty with
`CurrentSeasonYear` and `BaseCalendarYear` set to 2023 — and nothing else
changed — loads and displays 2023. That is the whole edit: **141 bytes of a
30,005,935-byte database**, and the extracted Player, Team and
CharacterVisuals tables come back byte-identical to the original's.

All eight base saves available carry `CurrentYear: 0`, so which of the two
fields the UI reads cannot be told apart from data alone — `CurrentSeasonYear`
and `BaseCalendarYear + CurrentYear` give the same answer. **Both are
written**, so they agree however the game derives what it shows.

### What else carries a year, and why it is left alone

Sweeping all 2,272 tables for integer fields able to hold a calendar year
returns 18. Only one of them tracks the current season:

- **`TeamHistoricSeriesYear.Year`** — 138 live rows, one per team, all stamped
  with the save's year. Written too, or it would disagree with the rest of the
  dynasty.
- **`PlayerStatRecord.calendarYear`** — 4,023 live rows across 36 tables. This
  is the **record book**: Sam Hartman's 2023 passing touchdowns, Philip
  Rivers' 2003 passing yards, Rashad Greene's 2014 receiving yards. Real dated
  achievements that belong to the years they happened in, whatever year the
  dynasty is set to. Never written.
- **`Team.YearSchoolEstablished`, `Team.YearStartOfFootballProgram`** (150 live
  rows, 1890–1893 and similar) and **`Stadium.STADIUM_CALENDAR_YEARBUILT`** —
  historical facts. Never written.
- **`Rivalry.FirstYearPlayed`** — named like a year and defaulted to 1890, but
  the 233 live rows hold 23, 24, 25, 30. Not a calendar year in practice.
- **`ContractYearSummary.ContractYear`** and
  `DraftPickTransactionHistoryEntry.Year` — zero live rows in a base save.
- **`DraftClassInfo.DraftClassYear`** — range 0–30, so it is relative to the
  dynasty rather than absolute, and follows the anchor by itself.

### The trap

**`madden-franchise` does not enforce its own schema.** Setting
`CurrentSeasonYear` to 5000, or to −1, is accepted in silence and writes a
number the game was never built to read. The bound is this project's to hold —
checked in `NativeSave.Apply` before the save is opened, and again in
`apply.mjs` so the sidecar is safe to run on its own. The floor is **1869**,
the first college football game ever played; anything below it is a typo, not
a season somebody is recreating.

---

## Group 8 — Abilities: a tier is stored, the ability is not

The two ability families are stored completely differently, and the difference
decides what a save can change at all.

### Physical abilities — `PhysicalAbility1..5`

Typed **`AbilitiesRank`** in the schema — the same type as the mental ability
*ranks* — with values `None`, `Bronze`, `Silver`, `Gold`, `Platinum`.

**Nothing on the player says which ability a slot is.** That mapping lives in
the game's own data. `PositionSignatureAbility[]`, `PositionAbilityTable[]` and
`PositionalAbilitySplines[]` are each a *single row of references*, and none of
those references resolves to a table inside the save. It depends on position
and archetype: in a base save 600 of the 696 `DT_PurePower` players with
exactly one ability have it in **slot 4**, while every single-ability
`KP_Power` has **slot 3**. Slot 4 on a nose tackle is not slot 4 on a kicker.

So a slot **cannot be pointed at another archetype's ability** by editing a
save. What changes a player's ability *set* is changing their archetype.

What the save does decide, and what this tool writes:

| | Driven by | Measured |
|---|---|---|
| How many slots are filled | Overall | 3.6% at OVR 50–54 rising to 99.1% at 90–94 |
| Which slots | Archetype | per-archetype slot order |
| What tier each holds | Overall | Bronze-heavy at the bottom, 52% Platinum at 95+ |

### Mental abilities — `MentalAbility1..3` + `MentalAbilityRank1..3`

These **do** name the ability, from a 20-value enum (`MentalAbilities`):
RoadFanFavorite, Toughness, FieldGeneral, ClutchKicker, Captain, TeamPlayer,
ClearHeaded, Headstrong, Adrenaline, HomeFanFavorite, WinningTime, TheNatural,
Rhythm, BestFriend, OLRally, DLRally, DBRally, BellCow, Instinct, HotHead.

Rare and elite: **248 of 11,730** players in a base save carry any, and **244
of those 248 carry all three**. Essentially none below OVR 80; 32.6% at 85–89
and 67.0% at 90–94.

Eight never appear on any player: Adrenaline, BellCow, Captain, HotHead,
Instinct, Rhythm, TeamPlayer, Toughness.

Five are position-confined in a base save with zero exceptions —
`FieldGeneral` (QB only, 9 players), `ClutchKicker` (K, 3), `OLRally` (the
line, 23), `DLRally` (D-line, 10), `DBRally` (DBs, 16). This project does not
infer an eligibility *rule* from that: `data/AbilityModel.json` records the
positions each ability was **observed** on and a player may only be given one
seen at their position. With 22–32 carriers there is no way to tell a rule
from a small sample, and the observation cannot be wrong in the direction that
matters.

### Related

`TraitDevelopment` is the development trait: `Normal` (7,595),
`College_Impact` (3,340), `College_Star` (770), `College_Elite` (25). The
schema also lists `Star`, `Superstar`, `XFactor` and `Hidden`, which no CFB
player carries — Madden-side values in a shared schema.

---

## Group 9 — `IsNIL` marks a real person, and locks them (confirmed by census)

**`IsNIL` is not a compensation field.** It marks a slot as holding a real
athlete — someone who signed an NIL agreement to appear under their own name
and likeness — and the game gates editing on it: **a player flagged `true`
cannot be edited in-game.**

A bool, and both values are ordinary in a base save: **7,246 `false`,
9,011 `true`** across the 16,257 live rows. Writing `false` therefore writes a
value the game itself holds for 7,246 of its own players — reassignment, not
invention.

**The census fits that meaning.** The flag climbs with overall the way a
real-athlete marker should: the stars are real people, and the end of a roster
is filler the game generated.

| Overall | Carry a deal | Of |
|---|---|---|
| 40–49 | 1.7% | 176 |
| 50–59 | 6.2% | 2,030 |
| 60–69 | 42.4% | 6,567 |
| 70–79 | 78.0% | 6,017 |
| 80–89 | 95.7% | 1,350 |
| 90–99 | **100%** | 114 |

That shape is why the flag cannot be left to the donor slot, and it cuts two
ways. A recreated player is **not** the licensed athlete whose slot they took,
so carrying the flag asserts something untrue about them. And because a
recreated roster is built on the *best* slots the save has — every slot at 90
and above is flagged — it is the entire starting eleven that would arrive
locked against editing in the game.

**The money fields are separate and are deliberately left alone.** They do not
move with the flag in the game's own data: **3,473 of the 7,246 `false`
players still hold a non-zero `BaseNILValue`** (max 165; the `true` side
reaches 430). Zeroing them alongside it would be inventing a rule the save does
not follow. Where the game *does* zero them is a team change — see Group 4,
which is a different edit.

The tool writes `false` for every player it generates, filled depth slots
included, and does not touch a slot it did not generate.

---

## Group 10 — `CharacterBodyType` is the body build (confirmed by named edit)

**`Freshman` is the stored name for the build the game's own editor calls
Lean.** Nothing in the schema hints at that, and a tool writing `"Lean"` would
be writing a value no player has ever held.

Confirmed by a save in which five named Florida State players were each given a
different build in-game and read back out:

| Player | Set in-game to | Stored as |
|---|---|---|
| Ashton Daniels | Lean | `Freshman` |
| Shane Willow | Thin | `Thin` |
| Kevin Sperry | Standard | `Standard` |
| Tanner Applewhite | Muscular | `Muscular` |
| Jelani Washington | Heavy | `Heavy` |

Base-save shares across 16,257 live players: Muscular 38.8%, Standard 35.5%,
Heavy 16.1%, Freshman 6.1%, Thin 3.5%.

> The `CharacterVisuals` blob also carries a `bodyType` integer, and it is
> **not** this field. Only one of the five edited players had the key at all,
> and its value did not track the build that was set. The Player column is what
> the game reads.

### What decides it

Two sources, answering different questions.

**EA's player builder** says which builds a given height and weight can carry.
That is what stops a 6'0" 240 lb receiver being written as Standard, and it is
transcribed into `data/BodyTypeRules.json`.

One deliberate departure: **the Lean cutoff is a flat 170 lb**. The builder's
own rises with height — 175 lb at 5'9" through 210 lb at 6'5" before Standard
becomes available — while the game's rosters carry Standard players down to
160 lb at every height. 170 splits the difference, and it is a choice rather
than a measurement.

**The base save's own census** says what the game puts on each position:

| Position | Build | Share |
|---|---|---|
| LE / RE | Muscular | 95.0% / 97.3% |
| LT / RT | Muscular | 81.2% / 81.5% |
| LOLB / MLB / ROLB | Muscular | 94.5% / 97.4% / 95.4% |
| FB / TE | Muscular | 98.9% / 79.2% |
| DT | Heavy | 76.1% |
| LG / RG / C | Heavy | 87.3% / 86.8% / 90.2% |
| QB / WR / CB / FS / SS / HB | Standard | 79.8% / 77.0% / 86.0% / 93.8% / 94.7% / 53.0% |
| K / P | Thin | 68.6% / 72.5% |

Where a position's build is not in question the position decides outright and
the builder is not consulted — the table describes the *light* builds, and its
"Standard/Thin only" band below 220 lb would make a 215 lb linebacker Standard,
which the game itself never does. Measured: gating those positions costs six
points of agreement and buys nothing. Everyone else chooses among the light
builds the builder permits at their height and weight.

### How well it reproduces the game

**82.6% agreement across 16,255 players**, against a **ceiling of 86.8%** — the
best any rule reading only position, height and weight could achieve, because
the game's own assignment varies within a cell. Every position that takes its
build outright sits exactly on its own ceiling.

Moving the Lean cutoff to a flat 170 lb is worth 6.1 points of that on its own
(76.5% → 82.6%). What is left is mostly irreducible: halfbacks split 53/47
between Standard and Muscular at every weight, and defensive tackles 76/23
between Heavy and Muscular, so no rule reading these three fields can do
better.

Re-measure with `python3 tools/measure_body_types.py <export-dir>`.

---

## Appendix — remaining columns (unconfirmed observations)

Statistical profile of every other column across the 16,257 live rows of
the base export. **These are observations, not confirmed semantics — none
of them are validated or written by the Milestone 1 tool.** Types:

- `int a–b` — every observed value parses as an integer in that range
- `bool` — only `false`/`true` observed
- `enum` — 25 or fewer distinct string values (all listed, up to 22)
- `text (n distinct)` — free text with n distinct values
- `binary32` — 32-character 0/1 string; looks like an encoded table
  reference/bitfield (same shape the save uses for cross-table references)

| Column | Observed type | Observed values / notes |
|---|---|---|
| `CharacterGameplay` | binary32 | 32-char 0/1 string; looks like a table reference/bitfield |
| `GetAbilityValue` | binary32 | 32-char 0/1 string; looks like a table reference/bitfield |
| `CareerStats` | binary32 | 32-char 0/1 string; looks like a table reference/bitfield |
| `SeasonStats` | binary32 | 32-char 0/1 string; looks like a table reference/bitfield |
| `GameStats` | binary32 | 32-char 0/1 string; looks like a table reference/bitfield |
| `CharacterVisuals` | binary32 | 32-char 0/1 string; looks like a table reference/bitfield |
| `PLYR_HOME_TOWN` | text (3032 distinct) |  |
| `PT_HBELUSIVEBACK` | bool |  |
| `PresentationId` | int 0–1037653 |  |
| `PT_HBELUSIVEPOWER` | bool |  |
| `ExperiencePoints` | int 0–0 |  |
| `PLYR_BIRTHDATE` | int 0–65517 |  |
| `PT_HBPOWERBACK` | bool |  |
| `LegacyScore` | int 0–9300 |  |
| `DeepRouteRunningRating` | int 0–97 |  |
| `SeasonHealthPool` | int 450–1000 |  |
| `SkillPoints` | int 0–10 |  |
| `TraitDevelopment` | enum | `College_Elite`, `College_Impact`, `College_Star`, `Normal` |
| `PlayerVisMoveType` | enum | `Agile`, `AgileSmall`, `AgileTall`, `Bruiser`, `BruiserHeavy`, `BruiserQuick`, `Default` |
| `SeasonHealthPoolMax` | int 450–1000 |  |
| `MaxInjuryDuration` | int 0–0 |  |
| `PLYR_GENERICHEAD` | text (297 distinct) |  |
| `PLYR_DRAFTPICK` | int 0–511 |  |
| `PLYR_STYLE` | enum | `Normal` |
| `PLYR_QBSTYLE` | text (40 distinct) |  |
| `PLYR_PERFORMLEVEL` | int 0–100 |  |
| `PLYR_HANDEDNESS` | enum | `Left`, `Right` |
| `CaptainsPatch` | enum | `FiveYearGold`, `None`, `OneYear`, `TwoYear` |
| `PortraitSwappableLibraryPath` | enum | `library_icons_brt` |
| `PLYR_TENDENCY` | int 0–2 |  |
| `AbsoluteTransferChance` | int -1–-1 |  |
| `AbsoluteGoProChance` | int -1–-1 |  |
| `InjurySeverity` | enum | `Invalid_` |
| `TEAM_TYPE` | enum | `Current` |
| `WearAndTear_LLeg` | int 10–10 |  |
| `AgilityRating` | int 0–99 |  |
| `PlayActionRating` | int 0–97 |  |
| `AccelerationRating` | int 0–99 |  |
| `PlayerType` | text (59 distinct) |  |
| `WearAndTear_LShoulder` | int 10–10 |  |
| `PassBlockPowerRating` | int 0–97 |  |
| `ConfidenceRating` | int 0–59 |  |
| `AwarenessRating` | int 0–99 |  |
| `PassBlockRating` | int 0–96 |  |
| `WearAndTear_LKnee` | int 10–10 |  |
| `Fatigue` | int 0–0 |  |
| `PassBlockFinesseRating` | int 0–98 |  |
| `BCVisionRating` | int 0–98 |  |
| `WearAndTear_RAnkle` | int 10–10 |  |
| `BreakTackleRating` | int 0–97 |  |
| `FinesseMovesRating` | int 0–98 |  |
| `BreakSackRating` | int 0–97 |  |
| `BlockSheddingRating` | int 0–96 |  |
| `WearAndTear_Back` | int 10–10 |  |
| `ManCoverageRating` | int 0–98 |  |
| `MediumRouteRunningRating` | int 0–97 |  |
| `ChangeOfDirectionRating` | int 0–99 |  |
| `MinInjuryDuration` | int 0–0 |  |
| `WearAndTear_LAnkle` | int 10–10 |  |
| `CatchingRating` | int 0–97 |  |
| `LongSnapRating` | int 0–99 |  |
| `CatchInTrafficRating` | int 0–97 |  |
| `KickReturnRating` | int 0–99 |  |
| `HitPowerRating` | int 0–97 |  |
| `CarryingRating` | int 0–96 |  |
| `LeadBlockRating` | int 0–97 |  |
| `Personality` | enum | `Entertainer`, `Intense`, `Leader`, `TeamPlayer`, `Unpredictable` |
| `JukeMoveRating` | int 0–99 |  |
| `JumpingRating` | int 0–99 |  |
| `KickAccuracyRating` | int 0–98 |  |
| `KickPowerRating` | int 0–99 |  |
| `WearAndTear_LHip` | int 10–10 |  |
| `InjuryRating` | int 0–99 |  |
| `ImpactBlockingRating` | int 0–97 |  |
| `InjuryType` | enum | `Invalid_` |
| `WearAndTear_RArm` | int 10–10 |  |
| `ThrowAccuracyDeepRating` | int 0–95 |  |
| `ThrowAccuracyMidRating` | int 0–96 |  |
| `ThrowAccuracyRating` | int 0–95 |  |
| `ThrowAccuracyShortRating` | int 0–98 |  |
| `WearAndTear_RElbow` | int 10–10 |  |
| `ThrowOnTheRunRating` | int 0–97 |  |
| `StiffArmRating` | int 0–93 |  |
| `StrengthRating` | int 0–97 |  |
| `TackleRating` | int 0–97 |  |
| `WearAndTear_LHand` | int 10–10 |  |
| `SpectacularCatchRating` | int 0–99 |  |
| `SpeedRating` | int 0–99 |  |
| `SpinMoveRating` | int 0–94 |  |
| `StaminaRating` | int 0–99 |  |
| `RecruitingDealbreaker` | enum | `BrandExposure`, `ChampionshipContender`, `CoachPrestige`, `ConferencePrestige`, `Invalid`, `PlayingStyle`, `PlayingTime`, `ProPotential`, `ProximityToHome` |
| `ToughnessRating` | int 0–99 |  |
| `ThrowUnderPressureRating` | int 0–97 |  |
| `ThrowPowerRating` | int 0–99 |  |
| `ShortRouteRunningRating` | int 0–97 |  |
| `WearAndTear_RFoot` | int 10–10 |  |
| `RunBlockFinesseRating` | int 0–98 |  |
| `RunBlockPowerRating` | int 0–96 |  |
| `RunBlockRating` | int 0–95 |  |
| `TruckingRating` | int 0–95 |  |
| `WearAndTear_LArm` | int 10–10 |  |
| `PowerMovesRating` | int 0–98 |  |
| `PressRating` | int 0–97 |  |
| `PursuitRating` | int 0–99 |  |
| `ReleaseRating` | int 0–96 |  |
| `WearAndTear_LElbow` | int 10–10 |  |
| `PLYR_STANCE` | enum | `Generic` |
| `PLYR_CELEBRATION` | int 0–99 |  |
| `PlayRecognitionRating` | int 0–98 |  |
| `ZoneCoverageRating` | int 0–97 |  |
| `PT_HBPOWERBLOCKING` | bool |  |
| `PLYR_HOME_STATE` | text (51 distinct) |  |
| `PLYR_DRAFTROUND` | int 0–63 |  |
| `IronManPosition` | enum | `Invalid_` |
| `PLYR_SLEEVETEMPERATURE` | int 0–95 |  |
| `PhysicalAbility3` | enum | `Bronze`, `Gold`, `None`, `Platinum`, `Silver` |
| `PLYR_CONSECYEARSWITHTEAM` | int 0–10 |  |
| `HomePipeline` | text (44 distinct) |  |
| `Age` | int 0–51 |  |
| `TotalInjuryDuration` | int 0–0 |  |
| `NumPrideStickers` | int 0–0 |  |
| `LatestInjuryStage` | enum | `PreSeason` |
| `SkillGroupCap3` | int 0–20 |  |
| `SkillGroupCap4` | int 0–20 |  |
| `SkillGroupCap5` | int 0–20 |  |
| `SkillGroupCap6` | int 0–20 |  |
| `CurrentYearSeasonEndingInjuryWeek` | int 0–30 |  |
| `YearlyAwardCount` | int 0–2 |  |
| `MentalAbility3` | enum | `Adrenaline`, `BellCow`, `BestFriend`, `ClearHeaded`, `DBRally`, `DLRally`, `FieldGeneral`, `Headstrong`, `HomeFanFavorite`, `HotHead`, `None`, `OLRally`, `RoadFanFavorite`, `TeamPlayer`, `TheNatural`, `WinningTime` |
| `RunningStyleRating` | enum | `Default`, `DefaultStrideAwkward`, `DefaultStrideBreadLoaf`, `DefaultStrideHighandTight`, `DefaultStrideLoose`, `LongStrideAwkward`, `LongStrideBreadLoaf`, `LongStrideDefault`, `LongStrideHighandTight`, `LongStrideLoose`, `ShortStrideAwkward`, `ShortStrideDefault`, `ShortStrideHighandTight`, `ShortStrideLoose` |
| `Scheme` | enum | `DEF_3_3_5`, `DEF_BASE4_3`, `OFF_AIR_RAID`, `OFF_MULTIPLE_OFFENSE`, `OFF_OPTION`, `OFF_PISTOL`, `OFF_POWER_SPREAD`, `OFF_PRO_STYLE`, `OFF_RUN_AND_SHOOT`, `OFF_SPREAD`, `OFF_SPREAD_OPTION`, `OFF_VEER_AND_SHOOT`, `OFF_WEST_COAST_ZONE_RUN` |
| `IdealRecruitingPitch` | enum | `Aspirational`, `CampusPersonality`, `CoachsFavorite`, `CollegeExperience`, `ConferenceSpotlight`, `FootballInfluencer`, `Grassroots`, `HometownHero`, `Invalid`, `ItsGameTime`, `Prestigious`, `ProveYourself`, `Starter`, `StudentOfTheGame`, `SundayBound`, `TVTime`, `TeamPlayer`, `TheClutch`, `TimeToGetToWork`, `ToTheHouse`, `WorkHorse` |
| `SkillGroupCap1` | int 0–20 |  |
| `SkillGroupCap2` | int 0–20 |  |
| `InjuryStatus` | enum | `Uninjured` |
| `LastYearSeasonEndingInjuryWeek` | int 0–0 |  |
| `LatestInjuryWeek` | int 0–0 |  |
| `LatestInjuryYear` | int 0–0 |  |
| `Role` | enum | `NoRole` |
| `MentalAbility1` | enum | `Adrenaline`, `BellCow`, `BestFriend`, `ClearHeaded`, `ClutchKicker`, `DBRally`, `DLRally`, `FieldGeneral`, `Headstrong`, `HomeFanFavorite`, `HotHead`, `None`, `OLRally`, `RoadFanFavorite`, `TeamPlayer`, `TheNatural`, `WinningTime` |
| `MentalAbility2` | enum | `Adrenaline`, `BellCow`, `BestFriend`, `ClearHeaded`, `ClutchKicker`, `DBRally`, `DLRally`, `FieldGeneral`, `Headstrong`, `HomeFanFavorite`, `HotHead`, `None`, `OLRally`, `RoadFanFavorite`, `TeamPlayer`, `TheNatural`, `WinningTime` |
| `Motivation2` | int 0–0 |  |
| `WearAndTear_RLeg` | int 10–10 |  |
| `WearAndTear_RKnee` | int 10–10 |  |
| `WearAndTear_Rib` | int 10–10 |  |
| `WearAndTear_LFoot` | int 10–10 |  |
| `WearAndTear_RHip` | int 10–10 |  |
| `WearAndTear_RHand` | int 10–10 |  |
| `Motivation1` | int 0–0 |  |
| `MentalAbilityRank2` | enum | `Bronze`, `Gold`, `None`, `Platinum`, `Silver` |
| `MentalAbilityRank3` | enum | `Bronze`, `Gold`, `None`, `Platinum`, `Silver` |
| `PhysicalAbility1` | enum | `Bronze`, `Gold`, `None`, `Platinum`, `Silver` |
| `PhysicalAbility4` | enum | `Bronze`, `Gold`, `None`, `Platinum`, `Silver` |
| `PhysicalAbility5` | enum | `Bronze`, `Gold`, `None`, `Platinum`, `Silver` |
| `PlayoffRoundReached` | enum | `None` |
| `PhysicalAbility2` | enum | `Bronze`, `Gold`, `None`, `Platinum`, `Silver` |
| `WearAndTear_RShoulder` | int 10–10 |  |
| `Motivation3` | int 0–0 |  |
| `PT_POWERRUSHER` | bool |  |
| `PT_PLAYBALL` | bool |  |
| `PT_PUNCHITOUT` | bool |  |
| `PT_QBBACKFIELDCREATOR` | bool |  |
| `PT_QBCOMMIT` | bool |  |
| `PT_PARANOID` | bool |  |
| `PT_PANICBUTTON` | bool |  |
| `PT_OBLIVIOUS` | bool |  |
| `PT_QBDUALTHREAT` | bool |  |
| `PT_QBDYNAMICPERFORMANCEBACKFIELDCREATOR` | bool |  |
| `PT_NOSEDIVE` | bool |  |
| `PT_LOOKFORSTARS` | bool |  |
| `PT_LONGARMOFTHELAW` | bool |  |
| `PT_LBRUNSUPPORT` | bool |  |
| `PT_LBPASSCOVERAGE` | bool |  |
| `PT_LBFIELDGENERAL` | bool |  |
| `PT_INVINCIBLE` | bool |  |
| `PT_HIGHLIGHTREEL` | bool |  |
| `PT_HEROBALL` | bool |  |
| `PT_HBRECEIVINGBACK` | bool |  |
| `PT_HBPOWERRECEIVING` | bool |  |
| `StartingHotCold` | enum | `Neutral` |
| `ProspectStarRating` | enum | `FIVE_STAR`, `FOUR_STAR`, `Invalid`, `ONE_STAR`, `THREE_STAR`, `TWO_STAR` |
| `PracticePlan` | enum | `Count_` |
| `MentalAbilityRank1` | enum | `Bronze`, `Gold`, `None`, `Platinum`, `Silver` |
| `IsInjuredReserve` | bool |  |
| `IsImpactPlayer` | bool |  |
| `IsCreated` | bool |  |
| `IsUserControlled` | bool |  |
| `PT_28_3` | bool |  |
| `PT_AGGRESSIVERECEIVER` | bool |  |
| `PT_ANCHORED` | bool |  |
| `PT_BIGHITTER` | bool |  |
| `PT_BOUNCER` | bool |  |
| `PT_BULLISH` | bool |  |
| `PT_CANNON` | bool |  |
| `PT_CANNONBALL` | bool |  |
| `PT_CONSERVATIVE` | bool |  |
| `PT_COVERBALL` | bool |  |
| `PT_DISCIPLINED` | bool |  |
| `PT_DLPOWERRUSHER` | bool |  |
| `PT_DLPUREPOWER` | bool |  |
| `PT_DLRUNSTOPPER` | bool |  |
| `PT_DLSPEEDRUSHER` | bool |  |
| `PT_DOUBLEBACK` | bool |  |
| `PT_ELUSIVEINSTINCT` | bool |  |
| `PT_EYESUP` | bool |  |
| `PT_FINESSERUSHER` | bool |  |
| `PT_FLYSWATTER` | bool |  |
| `PT_FORTIFIER` | bool |  |
| `PT_FREEPLAYFINDER` | bool |  |
| `PT_FREESTYLER` | bool |  |
| `PT_FROZENSOLID` | bool |  |
| `PT_GASGUZZLER` | bool |  |
| `PT_HAPPYFEET` | bool |  |
| `PT_PLAYRECEIVER` | bool |  |
| `PT_POSSESSIONRECEIVER` | bool |  |
| `PT_REDZONEJAMMER` | bool |  |
| `PT_RACRECEIVER` | bool |  |
| `PT_QUICKTRIGGER` | bool |  |
| `PT_QUICKCLOCK` | bool |  |
| `PT_QBPURERUNNER` | bool |  |
| `PT_QBPOCKETPASSER` | bool |  |
| `PT_QBDYNAMICPERFORMANCEPURERUNNER` | bool |  |
| `PT_QBDYNAMICPERFORMANCEPOCKETPASSER` | bool |  |
| `PT_QBDYNAMICPERFORMANCEDUALTHREAT` | bool |  |
| `PT_THROWITUP` | bool |  |
| `PT_TRAVELINGSHOWMEN` | bool |  |
| `PT_TWISTER` | bool |  |
| `PT_UNDERCUT` | bool |  |
| `PT_UNDISCIPLINED` | bool |  |
| `PT_UNUSEDTRAIT1` | bool |  |
| `PT_UNUSEDTRAIT2` | bool |  |
| `PT_UPANDOVER` | bool |  |
| `PT_WHIRLWIND` | bool |  |
| `PT_WRELUSIVEROUTERUNNER` | bool |  |
| `PT_WRGADGET` | bool |  |
| `PT_WRPHYSICALBLOCKER` | bool |  |
| `PT_WRPHYSICALRECEIVER` | bool |  |
| `PT_WRPHYSICALROUTERUNNER` | bool |  |
| `PT_WRPLAYMAKER` | bool |  |
| `PT_WRPOWERBLOCKING` | bool |  |
| `WasPreviouslyInjured` | bool |  |
| `PLYR_ICON` | bool |  |
| `PLYR_ISCAPTAIN` | bool |  |
| `NarrativeLock` | bool |  |
| `PortraitForceSilhouette` | bool |  |
| `IsLegend` | bool |  |
| `PT_THROWAWAY` | bool |  |
| `PT_TEVERTICALTHREAT` | bool |  |
| `PT_TEPHYSICALROUTERUNNER` | bool |  |
| `PT_TEPHYSICALBLOCKER` | bool |  |
| `PT_TEBLOCKING` | bool |  |
| `PT_STRONGARM` | bool |  |
| `PT_STEERINGCLEAR` | bool |  |
| `PT_SRUNSUPPORT` | bool |  |
| `PT_SNOWBALL` | bool |  |
| `PT_SNAPMISCHIEF` | bool |  |
| `PT_SHOWBOAT` | bool |  |
| `PT_SEEINGGHOSTS` | bool |  |
| `PT_SAFETACKLER` | bool |  |
| `PT_RUNOVER` | bool |  |
| `PT_RISKTAKER` | bool |  |
| `PT_RIPCORD` | bool |  |

