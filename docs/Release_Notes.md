# Release notes

## v0.6.0-alpha — Drop your dynasty in, get your dynasty back

**The headline: you no longer export anything, and you no longer import
anything.** Point the tool at your dynasty save, hand it a roster, and it
writes you a new save you load in the game.

```
RosterGenerator.Cli generate --dynasty DYNASTY-BASE1 --roster 2023_FSU.csv --save-out DYNASTY-2023FSU
```

Or in the desktop app: **Save file…** → your roster → Generate.

Before this release the workflow was: run PocketScout's export tool, run this
tool, then run a third-party roster importer. Two of those three steps are now
gone.

### Nothing to install

The download includes everything needed to read a save, **including its own
copy of the Node.js runtime** (v22.23.1 LTS, MIT licensed, checksum-verified
against nodejs.org at build time). You do not install Node. You do not run a
package manager. Unzip and run.

The bundled copy is also *private* to this application — whatever Node version
anything else on your machine wants, it cannot break this one, and this one
cannot break it.

The download grows from 68 MB to **122 MB** as a result — 33 MB for the
runtime and 21 MB for the save-format library, which carries the schema that
makes any of this possible. What it buys:

| Without it | With it |
|---|---|
| Export dynasty → run tool → import with a roster editor | Run tool |
| Three programs | One |
| A 27 MB CSV to place correctly | A save file you double-click in the game |
| Equipment changes need a second file imported separately | Written into the same save |

**If you would rather not use it, nothing is taken away.** The export-to-CSV
workflow still works exactly as it always has, is still first-class, and is
still what happens if the `tools\native-save` folder is missing. The tool tells
you what it needs rather than failing obscurely.

### What is guaranteed about writing a save

This is somebody's dynasty, so the rules are strict and they are tested:

- **Your save is never modified.** The output is always a new file, and
  writing over the file you supplied is refused outright.
- **Only fields that actually differ are written.** Every field is read back
  out of the save and compared first. Recreating the 2023 Florida State roster
  wrote 5,461 fields, changed 85 rows on Florida State and **zero rows
  anywhere else** in a 16,500-player table.
- **Empty roster slots are left alone.** A save pre-allocates slots holding no
  player; 243 of them were untouched in that run.
- **A game patch cannot corrupt your dynasty through this.** The save format
  version is checked, and a version this build does not recognise refuses to
  write rather than guessing at where fields live.

Verified before any of this shipped: reading a save reproduces PocketScout's
own export exactly — 4,584,474 field comparisons across 16,257 players, zero
mismatches — and unpacking then repacking a save with no edits returns a
byte-identical 30 MB database, on five different saves.

### Also in this release

- **A whole season at once.** `template --season 2010` writes a blank roster
  file for every team that played that year — 119 teams × 85 slots = 10,115
  rows — with Team, Season and Position filled in. One roster file can now
  carry any number of teams, and they all convert into one output.
- **Teams that were not in the FBS yet are left out, and named.** CFB27 ships
  today's 138 teams, so a 2010 season built from that list would silently
  include Sacramento State, James Madison, Liberty and a dozen more schools
  still in the FCS. Advisory, never a gate — the dates are in
  `data/FbsMembership.json` and are yours to correct.
- **The height column is now `HeightInches`, and it means inches.** Write
  `74`, not `6-2`. Excel turns `6-2` into the 2nd of June the moment it opens
  the file, which destroyed the height before the tool ever saw it — the
  commonest failure when filling the template with a spreadsheet assistant.
  Feet-inches is still read and converted, and reported so you can fix it at
  source. Files already filled in under the old `Height` name keep working.

### Upgrading

Nothing to migrate. Existing roster CSVs work unchanged, including ones using
the old `Height` column. Keep the whole unzipped folder together — `data`,
`templates` and `tools` all need to sit beside the executables.

### Known limitations

- Writing a save is verified on schema `C27_468_2`. A game patch may move it;
  the tool will say so and refuse rather than write.
- `data/FbsMembership.json` records schools that *joined* the FBS, not ones
  that left. A 2010 season template writes 119 teams where the real FBS had
  120 — the missing one is Idaho, which CFB27 does not carry.
- The desktop app writes the new save beside your original with `-Recreated`
  appended; there is no "choose where to put it" dialog yet.
