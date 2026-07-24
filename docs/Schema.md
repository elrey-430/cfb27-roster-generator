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
(plus one spontaneous `PLYR_COMMENT` change on one of the two renames).

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

## Group 2 — Confirmed columns with an unresolved encoding (do NOT write)

| Column | Status |
|---|---|
| `Weight` | **NOT raw pounds.** Observed values (league-wide 0–240, with FSU-diff examples 20, 35, 55, 100, 166) do not correspond to realistic player weights. Very likely an index/offset into a weight curve or spline — `Spline.csv` / `PositionSplineTable` tables exist in the export and are candidate lookup targets. **Open research item for Milestone 2**; until resolved, the library exposes `Weight` read-only (`Player.WeightRaw`) and the `OpaqueFieldGuard` validation rule blocks any change to it. |

*Assumption to verify:* the weight-curve/spline hypothesis is plausible but
unproven. Reverse-engineering it is explicitly out of scope for Milestone 1.

---

## Group 3 — Identity-derived fields (conditional on edit type)

Confirmed behavior: these change as a *side effect* of identity generation,
not of name text. A controlled in-game rename left all of them untouched.

| Column | Behavior | Tool policy |
|---|---|---|
| `PLYR_ASSETNAME` | Auto-derived from name at *generation* time (e.g. `HowardJamari_7025`); a plain rename does NOT regenerate it | Untouched on **Rename**; must be supplied by the caller on **ReplaceIdentity** |
| `GenericHeadAssetName` | Same derivation family (e.g. `Generic_0877_P_T0042_H_6_3`) | Same as above |
| `PLYR_PORTRAIT` | Portrait id tied to identity, not name text | Same as above |
| `PLYR_COMMENT` | Internal flavor-text/comment-pool index; changed on one observed rename with **no clear trigger** | **Leave alone, never set.** Any change is blocked by `OpaqueFieldGuard` |

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
| `PLYR_PREVTEAMID` | Set to the OLD team index, mirroring `PrevTeamIndex` | `0` observed as its "none" value — **note the two fields use different sentinels** |
| `BaseNILValue` | Reset to `0` (observed in both transfer cases) | |
| `CurrentNILCompensation` | Reset to `0` (observed in both transfer cases) | |

*Unconfirmed observation:* in the untouched base save, some players carry
`PLYR_PREVTEAMID` values (e.g. 1009–1164) that are far outside the
`TeamIndex` range (0–137), suggesting the field's native domain is a
different ID space (possibly recruiting/high-school or a prior-save team
id). The confirmed fact is only what the real tool *writes on a transfer*:
the old `TeamIndex` value, kept in sync with `PrevTeamIndex`.

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
| `CharacterBodyType` | enum | `Freshman`, `Heavy`, `Muscular`, `Standard`, `Thin` |
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
| `IsNIL` | bool |  |
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

