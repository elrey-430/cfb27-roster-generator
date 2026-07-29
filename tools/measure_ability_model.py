#!/usr/bin/env python3
"""Measure how the game hands out abilities, so this tool can do the same.

CFB27 stores the two kinds of ability very differently, and the difference
decides what can be written at all:

* **Physical abilities.** ``PhysicalAbility1..5`` hold a *tier only* --
  None/Bronze/Silver/Gold/Platinum -- and nothing on the player says which
  ability a slot is. That mapping lives in the game's own data, referenced by
  ``PositionSignatureAbility`` and friends, which the save does not carry. So a
  slot cannot be pointed at a different ability; what can be set is **how good
  a player is in the slots their archetype already gives them**.

* **Mental abilities.** ``MentalAbility1..3`` name the ability outright, from a
  20-value enum, with their own ranks. They are rare and elite -- a player has
  all three or none -- and each position's pool is measured rather than ruled.

Three things are measured, all against a base save where the game assigned
everything itself:

1. **How many** slots a player has, against their overall.
2. **Which** slots, per archetype -- because slot 4 on a nose tackle and slot 4
   on a receiver are different abilities, and only the archetype knows.
3. **What tier** those slots hold, against overall.

    python3 tools/measure_ability_model.py <export-dir> [<export-dir> ...] \
        --out data/AbilityModel.json

Each <export-dir> holds a ``0152_Player.csv`` (or any Player table export).
"""
import argparse
import collections
import csv
import json
import pathlib
import sys

csv.field_size_limit(10**9)

RECRUIT_POOL_TEAM = 255
PHYSICAL_SLOTS = range(1, 6)
MENTAL_SLOTS = range(1, 4)
TIERS = ["Bronze", "Silver", "Gold", "Platinum"]

# Overall is bucketed because the sample thins out at the top: 5-point bands
# keep every bucket big enough to mean something.
BAND = 5


def band_of(overall):
    return (overall // BAND) * BAND


def read_players(directory):
    for path in sorted(pathlib.Path(directory).rglob("*Player.csv")):
        with path.open(newline="", encoding="utf-8-sig") as handle:
            reader = csv.DictReader(handle)
            if reader.fieldnames is None or "PhysicalAbility1" not in reader.fieldnames:
                continue

            for row in reader:
                if not row.get("FirstName", "").strip():
                    continue
                try:
                    if int(row["TeamIndex"]) >= RECRUIT_POOL_TEAM:
                        continue
                    int(row["OverallRating"])
                except (KeyError, ValueError):
                    continue

                yield row
        return


def physical_slots(row):
    return [n for n in PHYSICAL_SLOTS if row[f"PhysicalAbility{n}"] != "None"]


def mental_names(row):
    return [row[f"MentalAbility{n}"] for n in MENTAL_SLOTS if row[f"MentalAbility{n}"] != "None"]


def distribution(counter):
    """A Counter as fractions, keyed by string, summing to 1."""
    total = sum(counter.values())
    return {str(k): round(v / total, 5) for k, v in sorted(counter.items())} if total else {}


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("exports", nargs="+")
    parser.add_argument("--out", default="data/AbilityModel.json")
    args = parser.parse_args(argv)

    players = [r for export in args.exports for r in read_players(export)]
    if not players:
        print("No player rows found.", file=sys.stderr)
        return 1

    # 1. How many physical slots, by overall band.
    count_by_band = collections.defaultdict(collections.Counter)
    # 3. What tier they hold, by overall band.
    tier_by_band = collections.defaultdict(collections.Counter)
    # 2. Which slots, per archetype -- weighted over every filled slot, so an
    #    archetype's ordering reflects the whole population and not just the
    #    players who happen to have exactly one.
    slots_by_archetype = collections.defaultdict(collections.Counter)
    archetype_totals = collections.Counter()

    mental_by_band = collections.defaultdict(lambda: [0, 0])
    mental_names_seen = collections.Counter()
    mental_positions = collections.defaultdict(collections.Counter)
    mental_rank = collections.Counter()
    mental_slot_counts = collections.Counter()
    position_totals = collections.Counter()

    for row in players:
        overall = int(row["OverallRating"])
        band = band_of(overall)
        filled = physical_slots(row)

        count_by_band[band][len(filled)] += 1
        archetype_totals[row["PlayerType"]] += 1
        for n in filled:
            tier_by_band[band][row[f"PhysicalAbility{n}"]] += 1
            slots_by_archetype[row["PlayerType"]][n] += 1

        names = mental_names(row)
        mental_by_band[band][0] += 1
        position_totals[row["Position"]] += 1
        mental_slot_counts[len(names)] += 1
        if names:
            mental_by_band[band][1] += 1
            for n in MENTAL_SLOTS:
                if row[f"MentalAbility{n}"] != "None":
                    mental_names_seen[row[f"MentalAbility{n}"]] += 1
                    mental_positions[row[f"MentalAbility{n}"]][row["Position"]] += 1
                    mental_rank[row[f"MentalAbilityRank{n}"]] += 1

    # Which abilities a position may be given: exactly the ones the game has
    # been seen giving to that position, and nothing else.
    #
    # The first cut of this tried to sort abilities into "position-locked" and
    # "general" by counting how many positions carried each. That over-fits:
    # FieldGeneral (QB) and OLRally (the line) really are locked and are named
    # after their group, but Headstrong appeared on four positions and
    # BestFriend on two purely because only 32 and 22 players carry them. There
    # is no way to tell a rule from a small sample at that size, so no rule is
    # inferred -- the pool is the observation itself, which cannot be wrong in
    # the direction that matters.
    by_position = {
        position: sorted(name for name, positions in mental_positions.items() if position in positions)
        for position in sorted(position_totals)
    }

    model = {
        "//": (
            "MEASURED, not authored. How the game itself distributes abilities, from "
            "tools/measure_ability_model.py. PhysicalAbility1..5 hold a TIER ONLY -- the "
            "ability a slot represents lives in the game's data, not the save -- so what is "
            "modelled here is how many slots a player has, which of their archetype's slots "
            "they are, and what tier each holds. Overall drives all three."
        ),
        "sourcePlayers": len(players),
        "sourceCount": len(args.exports),
        "overallBand": BAND,
        "physical": {
            "slotCountByOverall": {str(b): distribution(c) for b, c in sorted(count_by_band.items())},
            "tierByOverall": {
                str(b): {t: round(c[t] / sum(c.values()), 5) for t in TIERS}
                for b, c in sorted(tier_by_band.items()) if sum(c.values())
            },
            "slotOrderByArchetype": {
                archetype: [n for n, _ in counter.most_common()]
                for archetype, counter in sorted(slots_by_archetype.items())
            },
        },
        "mental": {
            "//": (
                "Rare and elite: 2.1% of players, and of those, 244 of 248 carry all three. "
                "byPosition is what the game was SEEN giving each position -- an observation, "
                "not an inferred eligibility rule, so a position with no observations gets none."
            ),
            "shareByOverall": {
                str(b): round(has / n, 5) for b, (n, has) in sorted(mental_by_band.items()) if n
            },
            "slotCountObserved": distribution(mental_slot_counts),
            "rankMix": distribution(mental_rank),
            "byPosition": by_position,
            "observedPositions": {k: sorted(v) for k, v in sorted(mental_positions.items())},
            "neverObserved": sorted(
                {"RoadFanFavorite", "Toughness", "FieldGeneral", "ClutchKicker", "Captain",
                 "TeamPlayer", "ClearHeaded", "Headstrong", "Adrenaline", "HomeFanFavorite",
                 "WinningTime", "TheNatural", "Rhythm", "BestFriend", "OLRally", "DLRally",
                 "DBRally", "BellCow", "Instinct", "HotHead"} - set(mental_names_seen)),
        },
    }

    out = pathlib.Path(args.out)
    out.write_text(json.dumps(model, indent=2) + "\n")

    print(f"{len(players):,} players across {len(args.exports)} export(s) -> {out}")
    print(f"  physical: {sum(1 for r in players if physical_slots(r)):,} have at least one slot")
    print(f"  mental:   {sum(1 for r in players if mental_names(r)):,} have any "
          f"({distribution(mental_slot_counts)})")
    thin = [p for p, a in by_position.items() if len(a) < 3]
    print(f"  mental pools: {len(by_position)} positions, "
          f"{min(len(a) for a in by_position.values())}-{max(len(a) for a in by_position.values())} abilities each"
          + (f"; thin at {', '.join(thin)}" if thin else ""))
    if model["mental"]["neverObserved"]:
        print(f"  never observed: {', '.join(model['mental']['neverObserved'])}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
