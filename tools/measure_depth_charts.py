#!/usr/bin/env python3
"""
Measures how the game fills its own depth charts, and writes
data/DepthChartSlots.json.

    python3 tools/measure_depth_charts.py <full-export-dir> [out.json]

Needs a FULL export (every table), not the three the generator normally reads.

WHAT A DEPTH CHART IS, in the save:

    Team.DepthChart  ->  a DepthChart row, one per team, 35 position slots
                     ->  each slot points at a Player[] row
                     ->  which holds up to 6 player references, in depth order

Every reference is a 32-bit cell: the high half tags the table, the low half is
the row. The same encoding the CharacterVisuals link uses.

TWO THINGS ARE MEASURED, because both are needed to rebuild a chart and neither
is guessable:

  depth  how many players the game actually lists in that slot. It is not the
         same everywhere -- 6 at WR, 5 at CB, 4 at HB, 3 almost everywhere else.

  from   which positions the game draws on. Most slots take their own position,
         but the specialist ones do not exist as positions at all: GAD is 59% HB
         and 40% WR, LS is 78% TE, SLCB is CB/FS/SS, and the tackle, guard and
         end slots each list BOTH sides of the line.

Ordering is by overall descending within a slot -- true of 2,634 of the 2,731
slots on a base save (96.4%).
"""
import collections
import csv
import json
import os
import sys

csv.field_size_limit(10 ** 9)

# A reference's high half tags the table it points into.
def decode(cell):
    if not cell or not cell.strip() or set(cell) - {'0', '1'}:
        return None, None
    value = int(cell, 2)
    return value >> 16, value & 0xFFFF


def load(directory, suffix):
    for name in sorted(os.listdir(directory)):
        if name.endswith(suffix):
            rows = list(csv.DictReader(open(os.path.join(directory, name), encoding='utf-8')))
            if rows:
                yield name, rows


def biggest(directory, suffix):
    """The table of that name with the most rows — the real one, not a sentinel."""
    return max(load(directory, suffix), key=lambda pair: len(pair[1]))


def main():
    directory = sys.argv[1].rstrip('/')
    out = sys.argv[2] if len(sys.argv) > 2 else 'data/DepthChartSlots.json'

    _, teams = biggest(directory, '_Team.csv')
    _, charts = biggest(directory, '_DepthChart.csv')
    _, arrays = biggest(directory, '_Player[].csv')
    _, players = biggest(directory, '_Player.csv')
    by_row = {int(p['_row']): p for p in players}

    entry_columns = [c for c in arrays[0] if not c.startswith('_')]
    slots = [c for c in charts[0] if not c.startswith('_') and c != 'LockedEntries']

    filled = collections.defaultdict(collections.Counter)
    depths = collections.defaultdict(list)
    ordered = same = 0
    player_tag = None

    for team in teams:
        tag, chart_row = decode(team.get('DepthChart', ''))
        if chart_row is None or chart_row >= len(charts):
            continue
        chart = charts[chart_row]
        for slot in slots:
            _, array_row = decode(chart.get(slot, ''))
            if array_row is None or array_row >= len(arrays):
                continue
            entry = arrays[array_row]
            listed = []
            for column in entry_columns:
                tag, row = decode(entry[column])
                if not tag or row is None or row not in by_row:
                    continue
                player_tag = tag
                listed.append(by_row[row])
            if not listed:
                continue
            depths[slot].append(len(listed))
            for player in listed:
                filled[slot][player['Position']] += 1
            own = [int(p['OverallRating']) for p in listed if p['Position'] == slot]
            if own:
                same += 1
                ordered += own == sorted(own, reverse=True)

    model = {
        '_comment': [
            'How the game fills its own depth charts, measured from a base save.',
            '',
            'Team.DepthChart -> a DepthChart row -> one Player[] row per slot ->',
            'up to six player references in depth order.',
            '',
            "depth  how many players the game lists there. Not uniform: 6 at WR,",
            '       5 at CB, 4 at HB, 3 almost everywhere else.',
            'from   which positions it draws on, most-used first. The specialist',
            '       slots are not positions at all — GAD is HB and WR, LS is',
            '       mostly TE, and the tackle, guard and end slots each list both',
            '       sides of the line.',
            '',
            'Within a slot the order is by overall, descending.',
            'Re-derive with tools/measure_depth_charts.py.',
        ],
        'playerTableTag': player_tag,
        'orderedByOverall': round(ordered / same, 3) if same else None,
        'slots': {},
    }

    for slot in slots:
        if not filled[slot]:
            continue
        total = sum(filled[slot].values())
        model['slots'][slot] = {
            'depth': round(sum(depths[slot]) / len(depths[slot])),
            'from': [p for p, n in filled[slot].most_common() if n / total >= 0.02],
        }

    with open(out, 'w', encoding='utf-8') as handle:
        json.dump(model, handle, indent=2)
        handle.write('\n')

    print(f"{len(model['slots'])} slots, player table tag {player_tag}, "
          f"{100 * ordered / same:.1f}% of slots ordered by overall -> {out}")
    for slot, spec in model['slots'].items():
        print(f"  {slot:7s} depth {spec['depth']}  from {', '.join(spec['from'])}")


if __name__ == '__main__':
    main()
