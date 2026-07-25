# Equipment demonstration spec

Everything the generator writes into a save has to be an asset name confirmed
to exist in the game. Retro equipment **cannot be discovered from a dynasty**:
a base save carries 528 distinct asset names across 12,586 characters, and not
one of them is a VSR-4, a TK, or a vintage mask. The only way to learn a name
is to select it in the community roster editor, export, and read it out of the
diff.

This file lists exactly what to change, and why each one is worth the effort.

## How to run a demonstration

1. Start from a dynasty export you already have (the "before").
2. In the roster editor, change **one player per asset** you want confirmed.
3. Export again to a differently named folder (the "after").
4. Send both. The diff names the assets; nothing else is needed.

Changing one player per asset is enough — the generator needs the *name*, not
a sample. Use players on one team so the diff stays small, and note who you
changed. (An unnoted eighth change in the first round cost a round-trip.)

---

## Round 2 — what would unblock the most

### A. Per-position masks for the retro shells (highest value)

Right now every player in a 2010s roster gets the same two-bar, so a punter
and a nose tackle come out identical. The game clearly distinguishes them: over
a full base save it puts a kicker cage on **92–98%** of kickers and punters, a
cage or heavy bar on linemen, and an open two-bar on quarterbacks.

For **`GearHelmet_RevolutionSpeed`** and **`GearHelmet_Revolution`**, one
player each:

| Player position | Mask to fit |
|---|---|
| K or P | the kicker cage |
| C, G or T | a full cage |
| DT or DE | a heavy cage/robot |
| MLB | a linebacker bar |
| WR or CB | a skill-position mask |

That is 5 players per shell, 10 total, and it fills in `masksByRole` for both
Riddell shells. The same again for `GearHelmet_AirXP` if you want Schutt
covered.

### B. The Riddell VSR-4

Needed for two things you have already specified: the 2000s Axiom lineage
(Axiom → VSR-4) and the whole of 1990–1999. Without it, an Axiom wearer in
2005 falls through to a Revolution, which is wrong for the early part of the
decade.

**One player** in a VSR-4, plus its default mask.

### C. The Riddell TK and the vintage masks

Needed for everything before 1990.

| Asset | Used by |
|---|---|
| Riddell TK helmet | 1980s and pre-1980 |
| "Vintage Standard" mask | 1980s non-linemen |
| "Vintage Two Bar" mask | all pre-1980 positions |
| 2–3 further vintage masks | the 1980s lineman pool |

**Four to six players.**

### D. Jersey cut and pad size

Confirmed already: `Gear_JerseyStyle_SleeveTight`, `_SleeveStandard`,
`_RolledLow`, and `Small_Pads`, `Medium_Pads`, `Large_Pads`.

Still needed:

| What you asked for | Status |
|---|---|
| "loose" sleeves | Possibly `_SleeveStandard` — **confirm which the editor calls "loose"** |
| "long" sleeves | Not in any base save. **One player.** |
| X-Large pads | Not in any base save. **One player.** |

`Large_Pads` appears on exactly **one** player in the whole base save, so the
larger sizes are real but almost unused — which is why they have to be
demonstrated rather than mined.

### E. A Schutt shell for the 2000s

Schutt's period models were the Air Advantage and the DNA; the Air XP is a
late-2000s helmet, so it is defensible from roughly 2008 but not for 2000–2007.
Until one is confirmed, a Schutt wearer in the early 2000s falls back to a
Riddell — the one place the model knowingly gets the manufacturer wrong.

**One player**, if either shell exists in the game.

---

## What the research settled

- **Riddell Revolution** — introduced 2002, worn by **83% of NFL players by
  2008**; over 2 million sold 2002–2008. It is the dominant shell of the late
  2000s, which is why the 2000s era leads with it.
- **Riddell VSR-4** — the previous-generation standard and the silhouette most
  1980s/90s throwbacks reproduce. Still in college use alongside the
  Revolution through 2010, which is why the 2000s split is by *model lineage*
  rather than by year.
- **Schutt Air XP** — late-2000s; the Air XP Pro VTD line arrives in the
  2010s.

Sources: [Revolution helmets (Wikipedia)](https://en.wikipedia.org/wiki/Revolution_helmets),
[Riddell helmet history](https://www.riddel.com/helmet-history),
[Evolution of Riddell football helmets](https://ultimateautographs.com/blogs/news/the-evolution-of-riddell-football-helmets-from-innovation-to-memorabilia),
[Schutt Sports](https://schuttsports.com/collections/helmets).

---

## Eras defined once each round lands

| Era | Needs | Status |
|---|---|---|
| 2010–2016 | — | **Live**, masks pending (A) |
| 2000–2009 | VSR-4 (B), Schutt shell (E) | **Live**, partially |
| 1990–1999 | VSR-4 (B), "long" sleeves, `Large_Pads` | Blocked on B and D |
| 1980–1989 | TK + vintage masks (C), X-Large pads (D) | Blocked on C and D |
| pre-1980 | TK + Vintage Two Bar (C), X-Large pads (D) | Blocked on C and D |

A round covering **A + B + C + D** — roughly 20 players — would define every
era in the table.
