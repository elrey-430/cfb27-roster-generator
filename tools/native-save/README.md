# Native dynasty saves — read/write probe

Everything this project has worked on so far is **PocketScout's CSV export**
of a dynasty. This folder is the evidence that the tool can skip that step and
read and write the **save file itself**.

Nothing here is wired into the application yet. It is a measurement rig, kept
because the findings below cost real saves to establish and must not have to
be rediscovered.

## What a CFB27 save is

A single extensionless file in
`Documents\EA SPORTS College Football 27\saves\`, e.g. `DYNASTY-BASE1`.

| | |
|---|---|
| Magic | `FBCHUNKS` (`46 42 43 48 55 4e 4b 53`) |
| Packed size | 9,646,981 bytes — **fixed**, identical across all five saves measured |
| Unpacked size | 30,005,935 bytes (30,006,689 with a custom coach) |
| Compression | zstd with a **trained dictionary** — this is why a save does not simply gunzip |
| Header bytes 22+ | little-endian save timestamp (year, month, day, hour, minute) |
| Tables | 2,272 — **2,274 with a custom coach** |
| Schema | `C27_468_2` (major 468, minor 2, gameYear 27), game type `college` |

The format is EA's franchise database, the same lineage as Madden. It is
handled by [`madden-franchise`](https://github.com/bep713/madden-franchise)
(MIT, v4.3.5+), which ships the C27 schema and the CFB zstd dictionaries and
auto-detects the game type. Node >= 22.19.

## What was proven, against five real saves

Five fresh dynasties, each with the PocketScout CSV export of the *same* save
to check against. Originals were copied and never modified; their hashes were
verified unchanged afterwards.

1. **It opens.** All five: gameYear 27, type `college`, schema 468.2, correct
   table count. No override needed.
2. **The read is exact.** The native Player table was diffed against
   PocketScout's `0152_Player.csv` of the same save, field by field:
   **4,584,474 comparisons over 16,257 live players, zero mismatches** — on
   `DYNASTY-BASE1` and again on `DYNASTY-BASE5` (custom coach + live roster).
   Both sides also agree on exactly which 243 records are empty.
3. **The round trip is lossless.** Unpack → repack with no edits, on all five
   saves: the 30 MB unpacked database is **byte-identical**, same SHA-256.
   The packed file is *not* byte-identical (~57% of bytes differ) because
   zstd will not reproduce EA's exact stream — but the packed size is
   unchanged, and the decompressed content is perfect. **Byte equality of the
   packed file is the wrong test; equality of the unpacked database is the
   right one.**
4. **A single edit stays single.** One jersey number changed (row 266, 32→99):
   exactly **1 byte** differs in the 30 MB database and exactly **1 cell** in
   the Player table.

Two corroborations of earlier findings fell out for free: the 282 fields the
library exposes match PocketScout's `fieldCount` exactly, and the custom-coach
saves carry **+2 tables**, which is what shifts the `Team` table from index
2225 to 2227.

## The season year (confirmed in-game)

The year the game displays lives in `SeasonInfo`, a **one-row table**:
`CurrentSeasonYear` and `BaseCalendarYear`, the anchor the dynasty counts
forward from. `CurrentYear` beside them is the offset into the dynasty, not a
calendar year, and is 0 in every fresh save.

A save built from a real dynasty with both year fields set to 2023 — nothing
else changed — was loaded in the game and **displays 2023**. `apply.mjs
--year` does exactly that, plus the current-season row each team keeps in
`TeamHistoricSeriesYear`: **141 bytes of the 30,005,935-byte database**, with
the Player, Team and CharacterVisuals tables re-extracting byte-identical.

All available base saves carry `CurrentYear: 0`, so `CurrentSeasonYear` and
`BaseCalendarYear + CurrentYear` cannot be told apart from data. Both are
written so they agree either way.

Every other year-bearing field was swept and left alone — most notably
`PlayerStatRecord.calendarYear`, whose 4,023 rows are the record book (Philip
Rivers' 2003 passing yards) rather than dynasty state. See Schema.md Group 7.

**The library does not enforce its own schema.** `CurrentSeasonYear` is
documented 0–4095; setting 5000, or −1, is accepted in silence and writes a
number the game cannot read. The bound is checked in `apply.mjs` and again in
`NativeSave.Apply`, floored at 1869 — the first college football game ever
played.

## What is still unknown

~~**Whether the game loads a repacked save.**~~ **Confirmed by the user**: a
repacked save loads with the roster edit intact, and a repacked save with the
season year changed loads and shows the new year.

Still unresolved: the schema is pinned at `C27_468_2`, and a game patch will
move it. Any production use must refuse to write an unrecognised schema
version rather than guess.

Also untested: what a changed year does over a long dynasty — whether
recruiting classes, eligibility or the coach carousel derive anything from the
anchor beyond the display. The derived fields that were checked are offsets
rather than absolute years (`DraftClassInfo.DraftClassYear` is 0–30), which is
encouraging but is not the same as playing a season through.

## Running it

```
npm install
node gate1.mjs   <save>                 # open and identify
node gate2b.mjs  <save> <repacked>      # compare unpacked databases
node gate4.mjs   <save> <out>           # single-field edit
node dump.mjs    <save> Player out.tsv  # dump a table for diffing
node sweep.mjs                          # gates 1-2 over saves/

node extract.mjs <save> <outDir>                    # save -> the pipeline's CSVs
node apply.mjs   <save> <out> <csv...> [--year YYYY] # CSVs + year -> a NEW save
```

Work on copies. The originals are somebody's dynasty.
