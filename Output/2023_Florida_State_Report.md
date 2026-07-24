# 2023 Florida State — CFB27 Conversion Report

- **Team ID:** 27
- **Players in historical dataset:** 75
- **Players generated:** 75
- **Players skipped:** 0
- **Donor slots left unreplaced:** 10
- **Dataset source:** Compiled independently from publicly available 2023 roster information (seminoles.com 2023 roster, Tomahawk Nation roster releases, ESPN player pages, Sports Illustrated/247Sports position previews). Spot-verified via web search July 2026. NOT derived from any CFB27 dynasty export.

## Global assumptions

- Ratings are inherited from the donor slot each player replaces — automatic rating generation is out of scope for this milestone.
- Weight is written using the confirmed encoding (stored value = pounds − 160, representable range 160–400 lb); weights outside that range or missing from the dataset inherit the donor slot's weight.
- Identity asset fields (PLYR_ASSETNAME, GenericHeadAssetName, PLYR_PORTRAIT) keep the donor slot's values, so in-game portraits/head models belong to the replaced fictional players. Face mapping is a later milestone.
- Hometown/previous-school data is carried in the dataset but not exported — the candidate columns (PLYR_HOME_TOWN, PLYR_HOME_STATE) are not yet empirically confirmed as safe to write.
- Slot assignment prefers a donor slot at the same position (or an interchangeable one, e.g. LE/RE); players placed in an unrelated slot get an explicit position change.

## Warnings

- 10 donor slot(s) were not replaced; the original fictional players remain on the roster (listed below). Remove or edit them manually if unwanted.

## Players with missing information, defaults, or warnings

### Samuel Singleton

Warnings:
- No HB-compatible slot was free; converted a RE slot, so the slot's inherited ratings fit the old position.

### Kentron Poitier

Missing:
- Jersey number

Default used:
- Jersey number: 7 (inherited from donor slot)

### Joshua Burrell

Missing:
- Jersey number

Default used:
- Jersey number: 7 (inherited from donor slot)

Warnings:
- No WR-compatible slot was free; converted a SS slot, so the slot's inherited ratings fit the old position.

### Darion Williamson

Missing:
- Jersey number

Default used:
- Jersey number: 37 (inherited from donor slot)

Warnings:
- No WR-compatible slot was free; converted a SS slot, so the slot's inherited ratings fit the old position.

### Markeston Douglas

Missing:
- Jersey number

Default used:
- Jersey number: 48 (inherited from donor slot)

### Brian Courtney

Missing:
- Jersey number

Default used:
- Jersey number: 89 (inherited from donor slot)

### Preston Daniel

Missing:
- Jersey number

Default used:
- Jersey number: 23 (inherited from donor slot)

### Jerrale Powers

Missing:
- Jersey number

Default used:
- Jersey number: 60 (inherited from donor slot)

Warnings:
- No TE-compatible slot was free; converted a LG slot, so the slot's inherited ratings fit the old position.

### Keiondre Jones

Missing:
- Jersey number

Default used:
- Jersey number: 64 (inherited from donor slot)

### Julian Armella

Missing:
- Jersey number

Default used:
- Jersey number: 79 (inherited from donor slot)

### Jaylen Early

Missing:
- Jersey number

Default used:
- Jersey number: 65 (inherited from donor slot)

### Qae'shon Sapp

Missing:
- Jersey number

Default used:
- Jersey number: 71 (inherited from donor slot)

### Lloyd Willis

Missing:
- Jersey number

Default used:
- Jersey number: 52 (inherited from donor slot)

### Antavious Woody

Missing:
- Jersey number

Default used:
- Jersey number: 78 (inherited from donor slot)

### Andre Otto

Missing:
- Jersey number

Default used:
- Jersey number: 48 (inherited from donor slot)

Warnings:
- No LG-compatible slot was free; converted a P slot, so the slot's inherited ratings fit the old position.

### Gilber Edmond

Missing:
- Jersey number

Default used:
- Jersey number: 13 (inherited from donor slot)

### Byron Turner

Missing:
- Jersey number

Default used:
- Jersey number: 92 (inherited from donor slot)

### Darrell Jackson

Missing:
- Jersey number

Default used:
- Jersey number: 98 (inherited from donor slot)

### KJ Sampson

Missing:
- Jersey number

Default used:
- Jersey number: 94 (inherited from donor slot)

### Ayobami Tifase

Missing:
- Jersey number

Default used:
- Jersey number: 12 (inherited from donor slot)

Warnings:
- No DT-compatible slot was free; converted a CB slot, so the slot's inherited ratings fit the old position.

### Daniel Lyons

Missing:
- Jersey number

Default used:
- Jersey number: 64 (inherited from donor slot)

Warnings:
- No DT-compatible slot was free; converted a K slot, so the slot's inherited ratings fit the old position.

### Omar Graham

Missing:
- Jersey number

Default used:
- Jersey number: 45 (inherited from donor slot)

### Justin Cryer

Missing:
- Jersey number

Default used:
- Jersey number: 42 (inherited from donor slot)

### Jayion McCluster

Missing:
- Jersey number

Default used:
- Jersey number: 28 (inherited from donor slot)

### Greedy Vance

Missing:
- Jersey number

Default used:
- Jersey number: 33 (inherited from donor slot)

### Omarion Cooper

Missing:
- Jersey number

Default used:
- Jersey number: 18 (inherited from donor slot)

### Quindarrius Jones

Missing:
- Jersey number

Default used:
- Jersey number: 26 (inherited from donor slot)

### Edwin Joseph

Warnings:
- No CB-compatible slot was free; converted a FS slot, so the slot's inherited ratings fit the old position.

### Ashlynd Barker

Warnings:
- No CB-compatible slot was free; converted a P slot, so the slot's inherited ratings fit the old position.

### Kenton Kirkland

Missing:
- Jersey number

Default used:
- Jersey number: 15 (inherited from donor slot)

Warnings:
- No CB-compatible slot was free; converted a RE slot, so the slot's inherited ratings fit the old position.

### Tyler Keltner

Missing:
- Jersey number

Default used:
- Jersey number: 91 (inherited from donor slot)

Warnings:
- No K-compatible slot was free; converted a ROLB slot, so the slot's inherited ratings fit the old position.

### James Rosenberry

Missing:
- Jersey number

Default used:
- Jersey number: 49 (inherited from donor slot)

Warnings:
- No TE-compatible slot was free; converted a SS slot, so the slot's inherited ratings fit the old position.

## Donor slots left unreplaced

These original (fictional) players remain on the team because the
historical dataset had fewer players than the donor roster:

- Blake Nichelson (_row=7910) — ROLB, OVR 76
- Jake Stanton (_row=10153) — RE, OVR 72
- Jelani Washington (_row=11309) — QB, OVR 70
- Shane Willow (_row=11808) — QB, OVR 76
- Jarvis Boatwright Jr. (_row=12430) — FS, OVR 68
- Daylen Green (_row=13265) — ROLB, OVR 65
- Caleb LaVallee (_row=13822) — ROLB, OVR 67
- Jaemin Pinckney (_row=14435) — RE, OVR 72
- Max Redmon (_row=14521) — SS, OVR 68
- Izayia Williams (_row=15346) — LOLB, OVR 69

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
