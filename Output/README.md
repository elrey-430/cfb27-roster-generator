# Output

By default `generate` writes its two deliverables here:

- `Generated_Roster.csv` — the full CFB27 player table with your selected
  team's roster replaced. This is the file you import with the roster
  editing tool.
- `Generation_Report.txt` — players processed and mapped, missing fields,
  defaults used, and warnings.

Override either path with `--output` / `--report`.

`2023_Florida_State_Report.md` is the worked example's report, kept as a
reference for what the tool tells you about its own decisions.

The player table that went with it is **not** committed. It is a build
output: 26 MB, regenerable in seconds, and a copy of the whole base-save
player table — the game's data rather than this project's. Produce it with:

```
dotnet run --project src/RosterGenerator.Cli -- generate \
    --dynasty <your export> --roster Tests/2023_FSU_Input.csv \
    --output Output/2023_Florida_State_CFB27.csv \
    --report Output/2023_Florida_State_Report.md
```

That generation is protected by an automated byte-stability test — see
`Tests/` and `FsuRegressionTests`.
