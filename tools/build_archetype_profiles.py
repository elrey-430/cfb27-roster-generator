#!/usr/bin/env python3
"""Measure what the game itself gives each archetype, and write it out as
data/ArchetypeProfiles.json.

For every archetype and every rating attribute, this fits value = a + b*overall
by least squares across the real players in a dynasty export. That gives a
starting point for a generated player that is EA's own, not anybody's opinion,
and it covers all 59 archetypes and 57 attributes without a hand-written line.

Usage:  python3 tools/build_archetype_profiles.py <export dir> [out.json]
"""
import csv, json, sys, os, glob, collections

csv.field_size_limit(sys.maxsize)

RATINGS = """AccelerationRating AgilityRating AwarenessRating BCVisionRating
BlockSheddingRating BreakSackRating BreakTackleRating CarryingRating
CatchInTrafficRating CatchingRating ChangeOfDirectionRating ConfidenceRating
DeepRouteRunningRating FinesseMovesRating HitPowerRating ImpactBlockingRating
InjuryRating JukeMoveRating JumpingRating KickAccuracyRating KickPowerRating
KickReturnRating LeadBlockRating LongSnapRating ManCoverageRating
MediumRouteRunningRating PassBlockFinesseRating PassBlockPowerRating
PassBlockRating PlayActionRating PlayRecognitionRating PowerMovesRating
PressRating PursuitRating ReleaseRating RunBlockFinesseRating
RunBlockPowerRating RunBlockRating ShortRouteRunningRating
SpectacularCatchRating SpeedRating SpinMoveRating StaminaRating
StiffArmRating StrengthRating TackleRating ThrowAccuracyDeepRating
ThrowAccuracyMidRating ThrowAccuracyRating ThrowAccuracyShortRating
ThrowOnTheRunRating ThrowPowerRating ThrowUnderPressureRating ToughnessRating
TruckingRating ZoneCoverageRating""".split()

MIN_SAMPLE = 12


def find_player_table(root):
    for path in glob.glob(os.path.join(root, "**", "*.csv"), recursive=True):
        with open(path, newline="", encoding="utf-8-sig", errors="ignore") as f:
            head = f.readline()
            if "_tableName" not in head:
                continue
            row = f.readline()
            cols = head.rstrip("\r\n").split(",")
            if "Player" in row.split(",")[cols.index("_tableName")] and "OverallRating" in cols:
                return path
    raise SystemExit(f"no Player table under {root}")


def fit(points):
    """Least-squares a + b*x plus the spread left over.

    Returns (intercept, slope, residualSd). The third number is what the game
    itself varies this attribute by among players of the same archetype at the
    same overall — the honest size of a "this one was better at that" nudge, so
    the engine never has to invent a magnitude.
    """
    n = len(points)
    sx = sum(x for x, _ in points)
    sy = sum(y for _, y in points)
    sxx = sum(x * x for x, _ in points)
    sxy = sum(x * y for x, y in points)
    denominator = n * sxx - sx * sx
    if denominator == 0:
        a, b = sy / n, 0.0
    else:
        b = (n * sxy - sx * sy) / denominator
        a = (sy - b * sx) / n

    residuals = sum((y - (a + b * x)) ** 2 for x, y in points)
    sd = (residuals / (n - 2)) ** 0.5 if n > 2 else 0.0
    return round(a, 3), round(b, 4), round(sd, 3)


def main():
    root = sys.argv[1]
    out = sys.argv[2] if len(sys.argv) > 2 else "data/ArchetypeProfiles.json"
    path = find_player_table(root)

    rows = list(csv.reader(open(path, newline="", encoding="utf-8-sig")))
    header = rows[0]
    idx = {c: n for n, c in enumerate(header)}

    samples = collections.defaultdict(lambda: collections.defaultdict(list))
    counts = collections.Counter()
    for row in rows[1:]:
        if row[idx["_isEmpty"]] == "true":
            continue
        archetype = row[idx["PlayerType"]]
        overall_raw = row[idx["OverallRating"]]
        if not archetype or not overall_raw.isdigit():
            continue
        overall = int(overall_raw)
        # Overall 0 marks engine placeholder rows, not players.
        if overall <= 0:
            continue
        counts[archetype] += 1
        for attribute in RATINGS:
            value = row[idx[attribute]]
            if value.isdigit():
                samples[archetype][attribute].append((overall, int(value)))

    profiles = {}
    for archetype, attributes in sorted(samples.items()):
        if counts[archetype] < MIN_SAMPLE:
            continue
        fitted = {}
        for attribute, points in attributes.items():
            if len(points) >= MIN_SAMPLE:
                fitted[attribute] = list(fit(points))
        profiles[archetype] = {"sampleSize": counts[archetype], "fit": fitted}

    document = {
        "//": [
            "MEASURED, NOT AUTHORED. For each archetype and attribute this holds",
            "[intercept, slope, residualSd] fitting value = intercept + slope *",
            "overall across the real players of a dynasty export -- so a generated",
            "player starts from what the game itself gives that archetype at that",
            "overall, and any nudge away from it is sized by the spread the game",
            "itself shows among those same players (residualSd).",
            "",
            "Regenerate with: python3 tools/build_archetype_profiles.py <export dir>",
            f"Source table: {os.path.basename(path)}",
            f"Archetypes: {len(profiles)}; attributes per archetype: up to {len(RATINGS)}",
        ],
        "minimumSample": MIN_SAMPLE,
        "archetypes": profiles,
    }
    with open(out, "w") as f:
        json.dump(document, f, indent=1)
        f.write("\n")
    print(f"wrote {out}: {len(profiles)} archetypes from {sum(counts.values())} players")


if __name__ == "__main__":
    main()
