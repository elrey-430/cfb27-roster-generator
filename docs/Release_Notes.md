# Release notes

## v0.7.0-alpha — Play the season you recreated

Build a 1985 roster and the game says 1985. Until now a recreated roster was
played in whatever year the save started in, and it was the one thing about a
historical recreation that could not be fixed afterwards in a roster editor —
the year is not in the roster at all.

```
generate --dynasty DYNASTY-BASE1 --roster 1985_Roster.csv \
         --save-out DYNASTY-1985 --dynasty-year roster
```

The year lives in `SeasonInfo`, a one-row table inside the save:
`CurrentSeasonYear` and `BaseCalendarYear`, the anchor the dynasty counts
forward from. Both are written, because every base save has `CurrentYear: 0`
and so cannot distinguish which one the UI reads. Confirmed by loading the
game: a save with both set to 2023, and nothing else changed, displays 2023.

**Opt-in, and only the year moves.** Two fields plus the current-season row
each team keeps — 141 bytes of a 30 MB database — and the Player, Team and
CharacterVisuals tables re-extract byte-identical. The record book keeps its
real dates. It requires `--save-out`, because the year lives in a table the
export tool does not write; asking on a CSV-only run is reported rather than
dropped.

### Also in this release

- **All-time rosters wear their own decades.** `Season` is read per row, so
  each player gets their own year's equipment instead of the whole squad
  taking whichever year was typed first.
- **A second pass on the archetype rules, measured against the game.** Twelve
  position defaults were archetypes the game barely uses (`C_WellRounded` on 0
  of 403 centres); the offensive-line weight rules were shown to be worse than
  guessing and removed; the power-blocker rules were kept because the same
  measurement supports them. 20 of 85 players on the 2023 FSU roster changed
  archetype, with every overall identical.
- **The app warns about teams that were not in the FBS yet**, which the
  command line has done since v0.6.0 and the app never did.

### Known limitations

- The season year is verified on the display, not across a simulated dynasty.
- `--dynasty-year roster` on an all-time file takes the first year in it.

## v0.6.2-alpha — The announcers say the right name

An oversight, reported and fixed: your recreated players were being called by
somebody else's name.

The game stores a **commentary index** per player, choosing which recorded
name the announcers use. The tool never set it, so a recreated player kept
whatever the roster slot already had — your Jordan Travis was announced as the
player he replaced, every game, for the whole dynasty. Nothing in the game
tells you, which is why it went unnoticed.

**Now it follows the surname you already typed.** No new column, nothing to
fill in, no option to tick. The `LastName` in your roster CSV sets the index.

A surname the commentary has no recording of gets **0** — the announcers
simply do not say the name, rather than saying the wrong one. That is the
game's own value: a fifth of the players in an untouched save have it. On the
2023 Florida State roster, 61 of 85 players are named and 24 are not, and the
generation report gives you those counts.

### Measured, not guessed

The mapping covers **5,918 surnames**, read out of **146,295 player rows across
nine dynasty saves the game generated itself** — where the game assigned both
the name and the index.

Hand-edited saves were deliberately left out of that measurement. A roster
editor can leave the index pointing at a slot's previous occupant, and one such
save was visibly wrong: All-Time USC names — Bush, Palmer, Allen, Leinart —
each disagreed with all nine untouched saves. Including it would have taught
the tool names the announcers cannot actually say.

**The game agrees with the rule.** Renaming two players in-game and exporting
again shows the game rewriting the index itself, to exactly the values this
mapping gives for the new surnames — including 0 for a surname it has no audio
for.

### What this does not change

Ratings, equipment, faces, hometowns and every output file are identical to
v0.6.1. Existing roster CSVs work unchanged. Re-generating a roster you already
made will change the commentary index on those players, and nothing else.

If `data/CommentaryIds.json` is missing, the field is left exactly as it was
rather than zeroed — "we know nothing" is not the same as "the name cannot be
said", and the report tells you which happened.

## v0.6.1-alpha — Opening a dynasty save no longer looks like a hang

A patch for one problem reported against v0.6.0: **opening a dynasty save
looked like nothing was happening.** If Generate stayed grey, or the window
went unresponsive, or the command line printed nothing — that was this.
Nothing was broken. The tool was working, silently, for longer than anyone
would reasonably wait.

Reading a save unpacks 30 MB of compressed, bit-packed tables and writes the
ones the generator needs back out: about fifteen seconds on a fast local disk,
noticeably longer if Documents is redirected to OneDrive, which is where the
game keeps saves for most people. The tool said nothing while it did that. The
command line printed its first line only *after* the unpacking finished, and
the desktop app was worse — it did the work on the thread that draws the
window, so Windows rendered the whole app as *"Not Responding"*.

And if the dynasty had genuinely failed to open, choosing a roster afterwards
**overwrote the error** with "Ready — N players, nothing to fix": a reassuring
sentence above a button that would not work, with no trace of what went wrong.

### What now happens

- **It says what it is doing, before it does it.** *"Reading DYNASTY-BASE1 — a
  dynasty save takes twenty seconds or so to open."*
- **The window stays responsive** while it reads, and the dynasty buttons are
  disabled during the load so a second click cannot race the first.
- **Generate always explains itself.** Whenever it is unavailable, a line
  underneath names the missing step, or the dynasty's own error. That line
  cannot be overwritten by anything else.
- **The roster check stops saying "Ready".** It reads the roster file and
  nothing else, so it cannot know whether the run is ready. It says **"Roster
  is fine"**, which is what it actually established.

Same features, same output, same guarantees as v0.6.0.

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
