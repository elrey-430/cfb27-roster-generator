# Output

By default `generate` writes its two deliverables here:

- `Generated_Roster.csv` — the full CFB27 player table with your selected
  team's roster replaced. This is the file you import with the roster
  editing tool.
- `Generation_Report.txt` — players processed and mapped, missing fields,
  defaults used, and warnings.

Override either path with `--output` / `--report`.

The `2023_Florida_State_*` files committed here are the Milestone 2
worked example: a complete 2023 Florida State roster generated from
`HistoricalData/2023/FloridaState.json` against a base dynasty export, kept
as a reference deliverable. The same generation is protected by an
automated byte-stability test — see `Tests/` and `FsuRegressionTests`.
