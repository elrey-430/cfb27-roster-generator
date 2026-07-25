# Tests — fixture files

**Nothing in this folder is importable into the roster editor.** These are
generator *inputs* and expected *outputs* used by the automated tests.

The two CSV shapes in this project are opposite ends of the pipeline and are
easy to mix up:

```
simple roster CSV        →  [generator]  →  CFB27 Player table CSV  →  roster editor
(12–46 friendly columns)                    (286 columns, 16,500 rows)
Ratings_test.csv                            Output/Generated_Roster.csv
2023_FSU_Input.csv
templates/HistoricalRosterTemplate.csv
```

If the roster editor reports **"CSV file is missing required column
`_tableIndex`"**, a simple roster CSV was handed to it by mistake. Run it
through the generator first — the importable file is the one written to
`Output/Generated_Roster.csv`.

| File | Kind | Purpose |
|---|---|---|
| `Ratings_test.csv` | **input** (simple format) | Standalone rating-engine case: 2015 Dalvin Cook plus six contrast players (a QB, an All-American kicker, a first-round corner, a tackle, a backup and a no-evidence walk-on) that exercise each sanity guardrail |
| `2023_FSU_Input.csv` | **input** (simple format) | The full 2023 Florida State roster used by the byte-stability regression test |
| `DonorDynasty/` | **input** (CFB27 tables) | A trimmed dynasty export — Florida State's 85 players plus the Team table. Not a full save; it exists so tests run without a 27 MB fixture |
| `2023_FSU_Expected_Output.csv` | **expected output** | What generating `2023_FSU_Input.csv` against `DonorDynasty/` must produce, byte for byte. Because it is built from the trimmed donor it is **not** a complete save either |

## Trying these yourself

```
RosterGenerator.Cli generate --dynasty <your dynasty export folder> \
                             --roster Tests/Ratings_test.csv
```

Point `--dynasty` at **your own full export**, not at `DonorDynasty/` — the
output is only as complete as the dynasty it was generated against, and the
roster editor expects a full player table.
