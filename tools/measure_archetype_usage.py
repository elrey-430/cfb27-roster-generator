#!/usr/bin/env python3
"""Measure what the game itself does with archetypes, to check our rules.

``data/ArchetypeRules.json`` decides which archetype a recreated player gets,
which in turn decides which of EA's overall formulas rates them. Its rules and
its per-position defaults were written by hand, from reasoning about what the
archetype names ought to mean. This script asks the game instead.

Two questions, both answerable from a base save's Player table, where the game
assigned every archetype itself:

1. **Is the position default the archetype the game would have picked?** The
   default is what a player with no usable evidence gets, which on a
   researched historical roster is most of the squad. A default the game
   almost never uses puts most of a recreated team in an archetype that does
   not occur.

2. **Is weight evidence for an offensive lineman's archetype?** Every OL rule
   in the file is a weight threshold. This reports, for each rule, how often
   it is right — against the base rate, which is what it has to beat to be
   worth having — plus the probability that a randomly chosen player of the
   archetype really is heavier (or lighter) than a randomly chosen player who
   is not. That probability is 0.5 when weight tells you nothing at all.

    python3 tools/measure_archetype_usage.py <export-dir> [<export-dir> ...]

Each <export-dir> holds a ``0152_Player.csv`` (or any Player table export).
Nothing is written: this reports, and a human decides what the data file
should say.
"""
import argparse
import collections
import csv
import itertools
import json
import pathlib
import sys

csv.field_size_limit(10**9)

# The recruit pool lives at 255 and is randomly generated per save, so it says
# nothing about how the game builds a real roster.
RECRUIT_POOL_TEAM = 255

# Stored weight is pounds - 160 (Schema.md, Group 2).
WEIGHT_OFFSET = 160


def read_players(directory):
    """Every live player on a real team, across one export."""
    for path in sorted(pathlib.Path(directory).rglob("*Player.csv")):
        with path.open(newline="", encoding="utf-8-sig") as handle:
            reader = csv.DictReader(handle)
            if reader.fieldnames is None or "PlayerType" not in reader.fieldnames:
                continue

            for row in reader:
                if not row.get("FirstName", "").strip():
                    continue  # a pre-allocated slot holding no player

                try:
                    team = int(row["TeamIndex"])
                except (KeyError, ValueError):
                    continue

                if team < RECRUIT_POOL_TEAM:
                    yield row

        return  # one Player table per export


def separation(with_trait, without_trait, heavier):
    """P(a random member of the archetype outweighs a random non-member).

    0.5 means weight carries no information. Computed exactly rather than
    sampled, so the number does not move between runs.
    """
    if not with_trait or not without_trait:
        return 0.5

    wins = ties = 0
    for a, b in itertools.product(with_trait, without_trait):
        if a == b:
            ties += 1
        elif (a > b) if heavier else (a < b):
            wins += 1

    return (wins + ties / 2) / (len(with_trait) * len(without_trait))


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("exports", nargs="+", help="dynasty export folder(s)")
    parser.add_argument("--rules", default="data/ArchetypeRules.json")
    args = parser.parse_args(argv)

    players = [row for export in args.exports for row in read_players(export)]
    if not players:
        print("No player rows found.", file=sys.stderr)
        return 1

    rules = json.loads(pathlib.Path(args.rules).read_text())["positions"]
    used = collections.defaultdict(collections.Counter)
    weights = collections.defaultdict(list)
    for row in players:
        position = row["Position"]
        used[position][row["PlayerType"]] += 1
        try:
            weights[position].append((row["PlayerType"], int(row["Weight"]) + WEIGHT_OFFSET))
        except ValueError:
            pass

    print(f"{len(players):,} players across {len(args.exports)} export(s)\n")

    print("POSITION DEFAULTS — what a player with no usable evidence gets")
    print(f"{'pos':5s} {'our default':24s} {'share':>7s}  {'the game usually picks':30s}")
    for position in sorted(rules):
        counts = used.get(position)
        if not counts:
            continue

        total = sum(counts.values())
        default = rules[position]["default"]
        share = counts.get(default, 0) / total
        modal, modal_count = counts.most_common(1)[0]
        flag = "  <-- rarely used" if share < 0.20 else ""
        print(f"{position:5s} {default:24s} {share:6.1%}  "
              f"{modal + f' ({modal_count / total:.0%})':30s}{flag}")

    print("\nUNUSED — archetypes we list as available that no player has")
    for position in sorted(rules):
        counts = used.get(position, {})
        missing = [a for a in rules[position]["available"] if not counts.get(a)]
        if missing:
            print(f"  {position:5s} {', '.join(missing)}")

    print("\nWEIGHT RULES — does weight actually predict the archetype?")
    print(f"{'pos':5s} {'archetype':22s} {'rule':>12s} {'fires on':>9s} {'right':>6s} "
          f"{'precision':>10s} {'base rate':>10s} {'separation':>11s}")
    for position in sorted(rules):
        pool = weights.get(position)
        if not pool:
            continue

        for rule in rules[position]["rules"]:
            conditions = [c for c in rule["all"] if c["field"] == "WeightPounds"]
            if len(conditions) != 1 or len(rule["all"]) != 1:
                continue

            condition = conditions[0]
            target = rule["archetype"]
            heavier = "min" in condition and condition["min"] is not None
            bound = condition["min"] if heavier else condition["max"]

            fires = [t for t, w in pool if (w >= bound if heavier else w <= bound)]
            right = sum(1 for t in fires if t == target)
            base = sum(1 for t, _ in pool if t == target) / len(pool)
            score = separation(
                [w for t, w in pool if t == target],
                [w for t, w in pool if t != target],
                heavier)

            verdict = "" if right / len(fires) > base * 1.25 else "  <-- no better than guessing"
            print(f"{position:5s} {target:22s} "
                  f"{('>= ' if heavier else '<= ') + str(bound) + ' lb':>12s} "
                  f"{len(fires):9d} {right:6d} {right / len(fires) if fires else 0:9.0%} "
                  f"{base:10.0%} {score:11.3f}{verdict}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
