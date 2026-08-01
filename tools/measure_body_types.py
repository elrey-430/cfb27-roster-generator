#!/usr/bin/env python3
"""
Measures how the game itself assigns body builds, and scores data/BodyTypeRules.json
against that.

    python3 tools/measure_body_types.py <export-dir-with-0152_Player.csv> [rules.json]

The Player table's CharacterBodyType column holds one of five strings.
'Freshman' is the stored name for the build the game's editor calls Lean --
confirmed by a save in which five named Florida State players were each given a
different build in-game and read back out.

Two numbers matter and the script prints both:

  agreement  how often the rules reproduce the game's own choice
  ceiling    how often the BEST POSSIBLE rule reading only position, height and
             weight could reproduce it -- the modal build of each cell. The gap
             between the ceiling and 100% is the game's own variation, which no
             deterministic rule can recover.

Reporting agreement without the ceiling would make an 83% rule look like a
failure when it is in fact at the limit of what the inputs allow.
"""
import collections
import csv
import json
import sys

csv.field_size_limit(10 ** 9)

POSITIONS = ['QB', 'HB', 'FB', 'WR', 'TE', 'LT', 'LG', 'C', 'RG', 'RT',
             'LE', 'RE', 'DT', 'LOLB', 'MLB', 'ROLB', 'CB', 'FS', 'SS', 'K', 'P']


def load_players(path):
    rows = []
    for row in csv.DictReader(open(path, encoding='utf-8')):
        if row.get('_isEmpty') == 'true':
            continue
        height = int(row['Height'])
        weight = int(row['Weight']) + 160          # the save stores pounds - 160
        if height < 57:                            # engine placeholder junk
            continue
        rows.append((row['Position'], height, weight, row['CharacterBodyType']))
    return rows


class Rules:
    def __init__(self, blob):
        self.builder = {int(h): b for h, b in blob['builder'].items() if h.isdigit()}
        self.positions = blob['positions']
        self.default = blob['defaultPrefer']
        self.light = set(blob['lightBuilds'])
        self.above = blob['aboveTheTable']
        self.shortest, self.tallest = min(self.builder), max(self.builder)

    def permitted(self, height, weight):
        for band in self.builder[min(max(height, self.shortest), self.tallest)]:
            if weight <= band['to']:
                return band['allow']
        return None

    def choose(self, position, height, weight):
        rule = self.positions.get(position, {'prefer': self.default})
        if 'always' in rule:
            return rule['always']
        allowed = self.permitted(height, weight)
        if allowed is None:
            return self.above
        light = [a for a in allowed if a in self.light]
        if not light:
            return self.above if self.above in allowed else allowed[0]
        for wanted in rule.get('prefer', self.default):
            if wanted in light:
                return wanted
        return light[0]


def main():
    players = load_players(f"{sys.argv[1].rstrip('/')}/0152_Player.csv")
    rules = Rules(json.load(open(sys.argv[2] if len(sys.argv) > 2 else 'data/BodyTypeRules.json',
                                 encoding='utf-8')))

    shares = collections.Counter(b for _, _, _, b in players)
    print(f"{len(players)} live players\n")
    print("  build shares")
    for build, n in shares.most_common():
        print(f"    {build:10s} {n:6d}  {100 * n / len(players):5.1f}%")

    by_position = collections.defaultdict(collections.Counter)
    for position, _, _, build in players:
        by_position[position][build] += 1

    hit = collections.Counter()
    total = collections.Counter()
    for position, height, weight, build in players:
        total[position] += 1
        if rules.choose(position, height, weight) == build:
            hit[position] += 1

    # The ceiling: best achievable by the modal build of each (position, height,
    # 5 lb band) cell. Any rule reading these three fields is bounded by it.
    cells = collections.defaultdict(collections.Counter)
    for position, height, weight, build in players:
        cells[(position, height, weight // 5)][build] += 1
    ceiling = sum(c.most_common(1)[0][1] for c in cells.values())

    print(f"\n  agreement {sum(hit.values())}/{len(players)} = "
          f"{100 * sum(hit.values()) / len(players):.1f}%")
    print(f"  ceiling   {ceiling}/{len(players)} = {100 * ceiling / len(players):.1f}%"
          "   (best any position+height+weight rule can do)\n")

    print(f"  {'POS':5s} {'agree':>7s} {'own-modal':>10s} {'n':>6s}   modal build")
    for position in POSITIONS:
        if not total[position]:
            continue
        modal, count = by_position[position].most_common(1)[0]
        print(f"  {position:5s} {100 * hit[position] / total[position]:6.1f}% "
              f"{100 * count / total[position]:9.1f}% {total[position]:6d}   {modal}")

    print("\n  where the rules and the game differ most")
    misses = collections.Counter()
    for position, height, weight, build in players:
        got = rules.choose(position, height, weight)
        if got != build:
            misses[(position, build, got)] += 1
    for (position, game, model), n in misses.most_common(10):
        print(f"    {position:5s} game={game:9s} rules={model:9s} {n}")


if __name__ == '__main__':
    main()
