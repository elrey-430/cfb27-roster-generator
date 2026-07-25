# 2023 Florida State — CFB27 Conversion Report

- **Team ID:** 27
- **Players in historical dataset:** 75
- **Players generated:** 75
- **Players skipped:** 0
- **Donor slots left unreplaced:** 0
- **Dataset source:** Simple historical CSV: 2023_FSU_Input.csv

## Global assumptions

- Ratings are generated from each player's historical evidence and calibrated so EA's own overall formula reproduces the intended overall; see Ratings/Rating_Model.md.
- Weight is written using the confirmed encoding (stored value = pounds − 160, representable range 160–400 lb); weights outside that range or missing from the dataset inherit the donor slot's weight.
- Identity asset fields (PLYR_ASSETNAME, GenericHeadAssetName, PLYR_PORTRAIT) keep the donor slot's values, so in-game portraits/head models belong to the replaced fictional players. Face mapping is a later milestone.
- Hometown is written: PLYR_HOME_TOWN takes the town as free text and PLYR_HOME_STATE the matching state from the save's 51-value enum (NonUS for anything not a US state).
- Player archetype (PlayerType) is chosen from each player's historical profile and the overall rating is recomputed with that archetype's EA formula, so the two always agree.
- PreviousSchool is written to PLYR_PREVTEAMID as that school's TEAM_ORIGID, and cleared to 0 for players who did not transfer. A school your dynasty does not carry is recorded as 1009, the value real FCS transfers carry.
- Slot assignment prefers a donor slot at the same position (or an interchangeable one, e.g. LE/RE); players placed in an unrelated slot get an explicit position change.
- The team's existing roster rates 5 point(s) above a typical program, so players you supplied little evidence for are rated as members of this team rather than of an average one. Players with a draft slot, awards or a stat line are unaffected.
- 10 roster slot(s) had no historical player, so they were re-rated as end-of-roster depth using the overall a real save carries at those roster ranks (data/RosterDepth.json), each held below the weakest historical player at its position. Their names, jersey numbers and portraits are unchanged.

## Players with missing information, defaults, or warnings

### Tate Rodemaker

Warnings:
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 69 -> 74: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.

### Brock Glenn

Warnings:
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 69 -> 74: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.
- Target overall reduced 74 -> 68: Freshman with Low confidence evidence.

### Trey Benson

Warnings:
- Archetype HB_ElusivePower -> HB_PowerBack: HB_PowerBack chosen because WeightPounds 216 is at least 215
- Physique: 216 lb vs 197 lb typical for HB (stronger, slightly slower).

### Lawrance Toafili

Warnings:
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 69 -> 74: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.

### Rodney Hill

Warnings:
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 69 -> 74: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.
- Target overall reduced 74 -> 68: Freshman with Low confidence evidence.

### Caziah Holmes

Warnings:
- Archetype HB_ElusiveBack -> HB_ElusivePower: HB_ElusivePower chosen because WeightPounds 205 is at least 205
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 69 -> 74: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.

### Samuel Singleton

Warnings:
- No HB-compatible slot was free; converted a RE slot, so the slot's inherited ratings fit the old position.
- Archetype DE_SmallerSpeedRusher -> HB_ElusiveBack: HB_ElusiveBack used as the HB default (no archetype rule matched the available data)
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 69 -> 74: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.
- Target overall reduced 74 -> 68: Freshman with Low confidence evidence.

### Keon Coleman

Warnings:
- Archetype WR_ShiftyRouteRunner -> WR_Physical: WR_Physical chosen because WeightPounds 215 is at least 210
- Physique: 215 lb vs 186 lb typical for WR (stronger, slightly slower).

### Johnny Wilson

Warnings:
- Archetype WR_ShiftyRouteRunner -> WR_Physical: WR_Physical chosen because WeightPounds 235 is at least 210
- Target overall moved 78 -> 80: the program rates 5 point(s) above a typical one, and this player's own record is Medium confidence.
- Physique: 235 lb vs 186 lb typical for WR (stronger, slightly slower).

### Ja'Khi Douglas

Warnings:
- Archetype WR_DeepThreat -> WR_ShiftyRouteRunner: WR_ShiftyRouteRunner used as the WR default (no archetype rule matched the available data)
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 76 -> 81: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.
- Target overall reduced 81 -> 78: Junior with Low confidence evidence.

### Destyn Hill

Warnings:
- Archetype WR_Physical -> WR_ShiftyRouteRunner: WR_ShiftyRouteRunner used as the WR default (no archetype rule matched the available data)
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 69 -> 74: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.
- Target overall reduced 74 -> 68: Freshman with Low confidence evidence.

### Hykeem Williams

Warnings:
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 69 -> 74: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.
- Target overall reduced 74 -> 68: Freshman with Low confidence evidence.
- Physique: 210 lb vs 186 lb typical for WR (stronger, slightly slower).

### Deuce Spann

Warnings:
- Archetype WR_Physical -> WR_PhysicalRouteRunner: WR_PhysicalRouteRunner chosen because HeightInches 76 is at least 76
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 69 -> 74: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.

### Kentron Poitier

Missing:
- Jersey number

Default used:
- Jersey number: 7 (inherited from donor slot)

Warnings:
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 64 -> 69: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.
- Physique: 205 lb vs 186 lb typical for WR (stronger, slightly slower).

### Vandrevius Jacobs

Warnings:
- Archetype WR_Physical -> WR_ShiftyRouteRunner: WR_ShiftyRouteRunner used as the WR default (no archetype rule matched the available data)
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 64 -> 69: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.
- Target overall reduced 69 -> 68: Freshman with Low confidence evidence.

### Joshua Burrell

Missing:
- Jersey number

Default used:
- Jersey number: 7 (inherited from donor slot)

Warnings:
- No WR-compatible slot was free; converted a SS slot, so the slot's inherited ratings fit the old position.
- Archetype S_RunSupport -> WR_ShiftyRouteRunner: WR_ShiftyRouteRunner used as the WR default (no archetype rule matched the available data)
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 64 -> 69: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.
- Target overall reduced 69 -> 68: Freshman with Low confidence evidence.
- Physique: 205 lb vs 186 lb typical for WR (stronger, slightly slower).

### Darion Williamson

Missing:
- Jersey number

Default used:
- Jersey number: 37 (inherited from donor slot)

Warnings:
- No WR-compatible slot was free; converted a SS slot, so the slot's inherited ratings fit the old position.
- Archetype S_Hybrid -> WR_ShiftyRouteRunner: WR_ShiftyRouteRunner used as the WR default (no archetype rule matched the available data)
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 64 -> 69: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.

### Kyle Morlock

Warnings:
- Previous school 'Shorter' is not a team in your dynasty, so it is recorded as a school the game does not model (the value real FCS transfers carry).
- Archetype TE_PhysicalRouteRunner -> TE_Possession: TE_Possession used as the TE default (no archetype rule matched the available data)
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 69 -> 74: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.

### Markeston Douglas

Missing:
- Jersey number

Default used:
- Jersey number: 48 (inherited from donor slot)

Warnings:
- Archetype TE_Blocking -> TE_PhysicalRouteRunner: TE_PhysicalRouteRunner chosen because WeightPounds 260 is at least 250
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 69 -> 74: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.
- Physique: 260 lb vs 236 lb typical for TE (stronger, slightly slower).

### Brian Courtney

Missing:
- Jersey number

Default used:
- Jersey number: 89 (inherited from donor slot)

Warnings:
- Archetype TE_PhysicalRouteRunner -> TE_Possession: TE_Possession used as the TE default (no archetype rule matched the available data)
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 64 -> 69: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.

### Preston Daniel

Missing:
- Jersey number

Default used:
- Jersey number: 23 (inherited from donor slot)

Warnings:
- Archetype TE_PhysicalRouteRunner -> TE_Possession: TE_Possession used as the TE default (no archetype rule matched the available data)
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 64 -> 69: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.

### Jerrale Powers

Missing:
- Jersey number

Default used:
- Jersey number: 60 (inherited from donor slot)

Warnings:
- No TE-compatible slot was free; converted a LG slot, so the slot's inherited ratings fit the old position.
- Archetype G_Agile -> TE_Possession: TE_Possession used as the TE default (no archetype rule matched the available data)
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 64 -> 69: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.
- Target overall reduced 69 -> 68: Freshman with Low confidence evidence.

### Robert Scott

Warnings:
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 76 -> 81: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.
- Target overall reduced 81 -> 78: Junior with Low confidence evidence.
- Physique: 320 lb vs 305 lb typical for OL (stronger, slightly slower).

### Jeremiah Byers

Warnings:
- Archetype OT_Agile -> OT_Power: OT_Power chosen because WeightPounds 315 is at least 315
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 76 -> 81: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.
- Target overall reduced 81 -> 80: Senior with Low confidence evidence.

### Casey Roddick

Warnings:
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 76 -> 81: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.
- Target overall reduced 81 -> 80: Senior with Low confidence evidence.
- Physique: 320 lb vs 305 lb typical for OL (stronger, slightly slower).

### D'Mitri Emmanuel

Warnings:
- Archetype G_Power -> G_Agile: G_Agile chosen because WeightPounds 305 is at most 305
- Target overall moved 84 -> 86: the program rates 5 point(s) above a typical one, and this player's own record is Medium confidence.

### Maurice Smith

Warnings:
- Archetype C_Power -> C_Agile: C_Agile chosen because WeightPounds 300 is at most 300
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 76 -> 81: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.
- Target overall reduced 81 -> 80: Senior with Low confidence evidence.

### Darius Washington

Warnings:
- Target overall moved 84 -> 86: the program rates 5 point(s) above a typical one, and this player's own record is Medium confidence.

### Keiondre Jones

Missing:
- Jersey number

Default used:
- Jersey number: 64 (inherited from donor slot)

Warnings:
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 69 -> 74: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.
- Physique: 325 lb vs 305 lb typical for OL (stronger, slightly slower).

### Bless Harris

Warnings:
- Archetype G_Power -> G_WellRounded: G_WellRounded used as the RG default (no archetype rule matched the available data)
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 69 -> 74: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.

### Julian Armella

Missing:
- Jersey number

Default used:
- Jersey number: 79 (inherited from donor slot)

Warnings:
- Archetype OT_Agile -> OT_Power: OT_Power chosen because WeightPounds 315 is at least 315
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 64 -> 69: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.
- Target overall reduced 69 -> 68: Freshman with Low confidence evidence.

### Jaylen Early

Missing:
- Jersey number

Default used:
- Jersey number: 65 (inherited from donor slot)

Warnings:
- Archetype G_Agile -> G_WellRounded: G_WellRounded used as the RG default (no archetype rule matched the available data)
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 64 -> 69: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.
- Target overall reduced 69 -> 68: Freshman with Low confidence evidence.

### Qae'shon Sapp

Missing:
- Jersey number

Default used:
- Jersey number: 71 (inherited from donor slot)

Warnings:
- Archetype OT_Agile -> OT_Power: OT_Power chosen because WeightPounds 320 is at least 315
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 64 -> 69: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.
- Target overall reduced 69 -> 68: Freshman with Low confidence evidence.
- Physique: 320 lb vs 305 lb typical for OL (stronger, slightly slower).

### Lloyd Willis

Missing:
- Jersey number

Default used:
- Jersey number: 52 (inherited from donor slot)

Warnings:
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 64 -> 69: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.
- Target overall reduced 69 -> 68: Freshman with Low confidence evidence.

### Antavious Woody

Missing:
- Jersey number

Default used:
- Jersey number: 78 (inherited from donor slot)

Warnings:
- Archetype G_Power -> G_Agile: G_Agile chosen because WeightPounds 305 is at most 305
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 64 -> 69: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.
- Target overall reduced 69 -> 68: Freshman with Low confidence evidence.

### Andre Otto

Missing:
- Jersey number
- Hometown

Default used:
- Jersey number: 48 (inherited from donor slot)
- Hometown: Jacksonville, Florida (inherited from donor slot)

Warnings:
- No LG-compatible slot was free; converted a P slot, so the slot's inherited ratings fit the old position.
- Archetype KP_Power -> G_WellRounded: G_WellRounded used as the LG default (no archetype rule matched the available data)
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 64 -> 69: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.

### Jared Verse

Warnings:
- Previous school 'Albany' is not a team in your dynasty, so it is recorded as a school the game does not model (the value real FCS transfers carry).
- Archetype DE_PurePower -> DE_SmallerSpeedRusher: DE_SmallerSpeedRusher chosen because WeightPounds 260 is at most 260

### Patrick Payton

Warnings:
- Archetype DE_PowerRusher -> DE_SmallerSpeedRusher: DE_SmallerSpeedRusher chosen because WeightPounds 250 is at most 260
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 76 -> 81: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.
- Target overall reduced 81 -> 74: Sophomore with Low confidence evidence.
- Physique: 250 lb vs 265 lb typical for DL (faster, slightly weaker).

### Gilber Edmond

Missing:
- Jersey number

Default used:
- Jersey number: 13 (inherited from donor slot)

Warnings:
- Archetype DE_RunStopper -> DE_SmallerSpeedRusher: DE_SmallerSpeedRusher chosen because WeightPounds 250 is at most 260
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 69 -> 74: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.
- Physique: 250 lb vs 265 lb typical for DL (faster, slightly weaker).

### Byron Turner

Missing:
- Jersey number

Default used:
- Jersey number: 92 (inherited from donor slot)

Warnings:
- Archetype DE_PurePower -> DE_PowerRusher: DE_PowerRusher used as the LE default (no archetype rule matched the available data)
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 69 -> 74: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.

### Fabien Lovett

Warnings:
- Archetype DT_PurePower -> DT_NoseTackle: DT_NoseTackle chosen because WeightPounds 315 is at least 315
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 76 -> 81: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.
- Target overall reduced 81 -> 80: Senior with Low confidence evidence.
- Physique: 315 lb vs 265 lb typical for DL (stronger, slightly slower).

### Braden Fiske

Warnings:
- Archetype DT_PowerRusher -> DT_SpeedRusher: DT_SpeedRusher chosen because Sacks 6 is at least 6
- Physique: 295 lb vs 265 lb typical for DL (stronger, slightly slower).

### Joshua Farmer

Warnings:
- Archetype DT_PurePower -> DT_PowerRusher: DT_PowerRusher used as the DT default (no archetype rule matched the available data)
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 69 -> 74: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.
- Physique: 305 lb vs 265 lb typical for DL (stronger, slightly slower).

### Malcolm Ray

Warnings:
- Archetype DT_PurePower -> DT_PowerRusher: DT_PowerRusher used as the DT default (no archetype rule matched the available data)
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 69 -> 74: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.
- Physique: 300 lb vs 265 lb typical for DL (stronger, slightly slower).

### Darrell Jackson

Missing:
- Jersey number

Default used:
- Jersey number: 98 (inherited from donor slot)

Warnings:
- Archetype DT_PurePower -> DT_NoseTackle: DT_NoseTackle chosen because WeightPounds 330 is at least 315
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 69 -> 74: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.
- Physique: 330 lb vs 265 lb typical for DL (stronger, slightly slower).

### KJ Sampson

Missing:
- Jersey number

Default used:
- Jersey number: 94 (inherited from donor slot)

Warnings:
- Archetype DT_SpeedRusher -> DT_PowerRusher: DT_PowerRusher used as the DT default (no archetype rule matched the available data)
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 64 -> 69: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.
- Target overall reduced 69 -> 68: Freshman with Low confidence evidence.
- Physique: 290 lb vs 265 lb typical for DL (stronger, slightly slower).

### Ayobami Tifase

Missing:
- Jersey number

Default used:
- Jersey number: 12 (inherited from donor slot)

Warnings:
- No DT-compatible slot was free; converted a CB slot, so the slot's inherited ratings fit the old position.
- Archetype CB_MantoMan -> DT_PowerRusher: DT_PowerRusher used as the DT default (no archetype rule matched the available data)
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 64 -> 69: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.
- Target overall reduced 69 -> 68: Freshman with Low confidence evidence.
- Physique: 305 lb vs 265 lb typical for DL (stronger, slightly slower).

### Daniel Lyons

Missing:
- Jersey number

Default used:
- Jersey number: 64 (inherited from donor slot)

Warnings:
- No DT-compatible slot was free; converted a K slot, so the slot's inherited ratings fit the old position.
- Archetype KP_Power -> DT_PowerRusher: DT_PowerRusher used as the DT default (no archetype rule matched the available data)
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 64 -> 69: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.
- Target overall reduced 69 -> 68: Freshman with Low confidence evidence.
- Physique: 290 lb vs 265 lb typical for DL (stronger, slightly slower).

### Tatum Bethune

Warnings:
- Target overall moved 75 -> 77: the program rates 5 point(s) above a typical one, and this player's own record is Medium confidence.

### DJ Lundy

Warnings:
- Archetype MLB_PassCoverage -> MLB_RunStopper: MLB_RunStopper used as the MLB default (no archetype rule matched the available data)
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 69 -> 74: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.
- Physique: 245 lb vs 225 lb typical for LB (stronger, slightly slower).

### Blake Nichelson

Warnings:
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 69 -> 74: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.
- Target overall reduced 74 -> 68: Freshman with Low confidence evidence.

### Omar Graham

Missing:
- Jersey number

Default used:
- Jersey number: 45 (inherited from donor slot)

Warnings:
- Archetype MLB_FieldGeneral -> MLB_RunStopper: MLB_RunStopper used as the MLB default (no archetype rule matched the available data)
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 64 -> 69: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.
- Target overall reduced 69 -> 68: Freshman with Low confidence evidence.

### Justin Cryer

Missing:
- Jersey number

Default used:
- Jersey number: 42 (inherited from donor slot)

Warnings:
- Archetype MLB_PassCoverage -> MLB_RunStopper: MLB_RunStopper used as the MLB default (no archetype rule matched the available data)
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 64 -> 69: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.
- Target overall reduced 69 -> 68: Freshman with Low confidence evidence.

### Jayion McCluster

Missing:
- Jersey number

Default used:
- Jersey number: 28 (inherited from donor slot)

Warnings:
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 64 -> 69: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.
- Target overall reduced 69 -> 68: Freshman with Low confidence evidence.

### Fentrell Cypress

Warnings:
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 76 -> 81: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.
- Target overall reduced 81 -> 80: Senior with Low confidence evidence.

### Jarrian Jones

Warnings:
- Target overall moved 83 -> 85: the program rates 5 point(s) above a typical one, and this player's own record is Medium confidence.

### Renardo Green

Warnings:
- Archetype CB_MantoMan -> CB_HybridCorner: CB_HybridCorner chosen because PassesDefended 13 is at least 8

### Azareye'h Thomas

Warnings:
- Archetype CB_Slot -> CB_MantoMan: CB_MantoMan used as the CB default (no archetype rule matched the available data)
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 69 -> 74: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.

### Kevin Knowles

Warnings:
- Archetype CB_HybridCorner -> CB_Slot: CB_Slot chosen because HeightInches 70 is at most 70
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 76 -> 81: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.
- Target overall reduced 81 -> 78: Junior with Low confidence evidence.

### Greedy Vance

Missing:
- Jersey number

Default used:
- Jersey number: 33 (inherited from donor slot)

Warnings:
- Archetype CB_HybridCorner -> CB_MantoMan: CB_MantoMan used as the CB default (no archetype rule matched the available data)
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 69 -> 74: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.

### Omarion Cooper

Missing:
- Jersey number

Default used:
- Jersey number: 18 (inherited from donor slot)

Warnings:
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 69 -> 74: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.

### Quindarrius Jones

Missing:
- Jersey number

Default used:
- Jersey number: 26 (inherited from donor slot)

Warnings:
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 64 -> 69: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.
- Target overall reduced 69 -> 68: Freshman with Low confidence evidence.

### Conrad Hussey

Warnings:
- Archetype CB_HybridCorner -> CB_MantoMan: CB_MantoMan used as the CB default (no archetype rule matched the available data)
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 69 -> 74: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.
- Target overall reduced 74 -> 68: Freshman with Low confidence evidence.

### Edwin Joseph

Warnings:
- No CB-compatible slot was free; converted a FS slot, so the slot's inherited ratings fit the old position.
- Archetype S_Hybrid -> CB_MantoMan: CB_MantoMan used as the CB default (no archetype rule matched the available data)
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 64 -> 69: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.
- Target overall reduced 69 -> 68: Freshman with Low confidence evidence.

### Akeem Dent

Warnings:
- Archetype S_RunSupport -> S_Hybrid: S_Hybrid used as the FS default (no archetype rule matched the available data)
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 76 -> 81: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.
- Target overall reduced 81 -> 80: Senior with Low confidence evidence.

### Shyheim Brown

Warnings:
- Archetype S_RunSupport -> S_Hybrid: S_Hybrid used as the FS default (no archetype rule matched the available data)
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 76 -> 81: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.
- Target overall reduced 81 -> 74: Sophomore with Low confidence evidence.

### Ashlynd Barker

Warnings:
- No CB-compatible slot was free; converted a P slot, so the slot's inherited ratings fit the old position.
- Archetype KP_Power -> CB_MantoMan: CB_MantoMan used as the CB default (no archetype rule matched the available data)
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 64 -> 69: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.
- Target overall reduced 69 -> 68: Freshman with Low confidence evidence.
- Physique: 200 lb vs 185 lb typical for CB (stronger, slightly slower).

### Kenton Kirkland

Missing:
- Jersey number

Default used:
- Jersey number: 15 (inherited from donor slot)

Warnings:
- No CB-compatible slot was free; converted a RE slot, so the slot's inherited ratings fit the old position.
- Archetype DE_SmallerSpeedRusher -> CB_MantoMan: CB_MantoMan used as the CB default (no archetype rule matched the available data)
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 64 -> 69: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.
- Target overall reduced 69 -> 68: Freshman with Low confidence evidence.

### Ryan Fitzgerald

Warnings:
- Archetype KP_Power -> KP_Accurate: KP_Accurate used as the K default (no archetype rule matched the available data)
- Target overall moved 89 -> 91: the program rates 5 point(s) above a typical one, and this player's own record is Medium confidence.
- Target overall reduced 91 -> 90: the highest K the game itself carries is 90.

### Tyler Keltner

Missing:
- Jersey number

Default used:
- Jersey number: 91 (inherited from donor slot)

Warnings:
- No K-compatible slot was free; converted a ROLB slot, so the slot's inherited ratings fit the old position.
- Previous school 'East Tennessee State' is not a team in your dynasty, so it is recorded as a school the game does not model (the value real FCS transfers carry).
- Archetype OLB_PassCoverage -> KP_Accurate: KP_Accurate used as the K default (no archetype rule matched the available data)
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 69 -> 74: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.

### Alex Mastromanno

Warnings:
- 'Australia' is not a US state; PLYR_HOME_STATE set to NonUS.
- Target overall moved 91 -> 93: the program rates 5 point(s) above a typical one, and this player's own record is Medium confidence.
- Target overall reduced 93 -> 86: the highest P the game itself carries is 86.
- Physique: 220 lb vs 200 lb typical for P (stronger, slightly slower).

### James Rosenberry

Missing:
- Jersey number

Default used:
- Jersey number: 49 (inherited from donor slot)

Warnings:
- No TE-compatible slot was free; converted a SS slot, so the slot's inherited ratings fit the old position.
- Archetype S_Hybrid -> TE_Possession: TE_Possession used as the TE default (no archetype rule matched the available data)
- Ratings generated with Low confidence — supply stats, awards, a draft slot or a recruiting rating for a better estimate.
- Target overall moved 76 -> 81: the program rates 5 point(s) above a typical one, and this player's own record is Low confidence.
- Target overall reduced 81 -> 80: Senior with Low confidence evidence.

## Converted players

| # | Name | Pos | Class | Donor slot (_row) |
|---|---|---|---|---|
| 13 | Jordan Travis | QB | Redshirt Senior | 330 |
| 18 | Tate Rodemaker | QB | Redshirt Junior | 2635 |
| 11 | Brock Glenn | QB | Freshman | 10083 |
| 3 | Trey Benson | HB | Redshirt Junior | 6092 |
| 9 | Lawrance Toafili | HB | Redshirt Junior | 9429 |
| 21 | Rodney Hill | HB | Redshirt Freshman | 9807 |
| 24 | Caziah Holmes | HB | Redshirt Junior | 11926 |
| 28 | Samuel Singleton | HB | Freshman | 266 |
| 4 | Keon Coleman | WR | Junior | 1085 |
| 14 | Johnny Wilson | WR | Redshirt Junior | 1896 |
| 0 | Ja'Khi Douglas | WR | Redshirt Junior | 2650 |
| 7 | Destyn Hill | WR | Freshman | 3906 |
| 8 | Hykeem Williams | WR | Freshman | 9119 |
| 12 | Deuce Spann | WR | Redshirt Junior | 9798 |
| — | Kentron Poitier | WR | Redshirt Senior | 13901 |
| 16 | Vandrevius Jacobs | WR | Freshman | 13949 |
| — | Joshua Burrell | WR | Redshirt Freshman | 591 |
| — | Darion Williamson | WR | Redshirt Sophomore | 804 |
| 6 | Jaheim Bell | TE | Redshirt Junior | 9030 |
| 84 | Kyle Morlock | TE | Redshirt Senior | 10646 |
| — | Markeston Douglas | TE | Redshirt Junior | 12453 |
| — | Brian Courtney | TE | Redshirt Sophomore | 13796 |
| — | Preston Daniel | TE | Redshirt Sophomore | 13892 |
| — | Jerrale Powers | TE | Freshman | 831 |
| 74 | Robert Scott | LT | Redshirt Junior | 1992 |
| 63 | Jeremiah Byers | LT | Redshirt Senior | 2640 |
| 70 | Casey Roddick | LG | Redshirt Senior | 1212 |
| 71 | D'Mitri Emmanuel | LG | Redshirt Senior | 6725 |
| 54 | Maurice Smith | C | Redshirt Senior | 5649 |
| 76 | Darius Washington | LG | Redshirt Junior | 14452 |
| — | Keiondre Jones | RG | Redshirt Senior | 5205 |
| 75 | Bless Harris | RG | Redshirt Senior | 6615 |
| — | Julian Armella | RT | Redshirt Freshman | 8010 |
| — | Jaylen Early | RG | Redshirt Freshman | 8157 |
| — | Qae'shon Sapp | RT | Redshirt Freshman | 8194 |
| — | Lloyd Willis | C | Freshman | 10688 |
| — | Antavious Woody | RG | Redshirt Freshman | 10779 |
| — | Andre Otto | LG | Redshirt Sophomore | 835 |
| 5 | Jared Verse | LE | Redshirt Senior | 2848 |
| 11 | Patrick Payton | LE | Redshirt Sophomore | 2850 |
| — | Gilber Edmond | LE | Redshirt Junior | 2889 |
| — | Byron Turner | LE | Redshirt Sophomore | 2936 |
| 0 | Fabien Lovett | DT | Redshirt Senior | 6609 |
| 55 | Braden Fiske | DT | Redshirt Senior | 7034 |
| 47 | Joshua Farmer | DT | Redshirt Sophomore | 9416 |
| 99 | Malcolm Ray | DT | Redshirt Junior | 12062 |
| — | Darrell Jackson | DT | Junior | 12601 |
| — | KJ Sampson | DT | Freshman | 15310 |
| — | Ayobami Tifase | DT | Redshirt Freshman | 1984 |
| — | Daniel Lyons | DT | Redshirt Freshman | 3853 |
| 4 | Kalen DeLoach | MLB | Redshirt Senior | 4117 |
| 15 | Tatum Bethune | MLB | Redshirt Senior | 5674 |
| 10 | DJ Lundy | MLB | Redshirt Junior | 8675 |
| 18 | Blake Nichelson | MLB | Freshman | 12793 |
| — | Omar Graham | MLB | Freshman | 13823 |
| — | Justin Cryer | MLB | Redshirt Freshman | 15017 |
| — | Jayion McCluster | ROLB | Redshirt Freshman | 3899 |
| 23 | Fentrell Cypress | CB | Redshirt Senior | 4904 |
| 7 | Jarrian Jones | CB | Redshirt Senior | 5713 |
| 8 | Renardo Green | CB | Redshirt Senior | 5722 |
| 13 | Azareye'h Thomas | CB | Sophomore | 6334 |
| 3 | Kevin Knowles | CB | Junior | 8793 |
| — | Greedy Vance | CB | Redshirt Junior | 12807 |
| — | Omarion Cooper | CB | Junior | 13506 |
| — | Quindarrius Jones | CB | Redshirt Freshman | 13765 |
| 12 | Conrad Hussey | CB | Freshman | 15030 |
| 13 | Edwin Joseph | CB | Freshman | 4809 |
| 1 | Akeem Dent | FS | Redshirt Senior | 6001 |
| 38 | Shyheim Brown | FS | Redshirt Sophomore | 8965 |
| 27 | Ashlynd Barker | CB | Redshirt Freshman | 5122 |
| — | Kenton Kirkland | CB | Freshman | 5914 |
| 88 | Ryan Fitzgerald | K | Redshirt Junior | 14358 |
| — | Tyler Keltner | K | Redshirt Senior | 6468 |
| 29 | Alex Mastromanno | P | Senior | 13694 |
| — | James Rosenberry | TE | Redshirt Senior | 7359 |
