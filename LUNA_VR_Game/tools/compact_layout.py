"""Compact-layout tool: reads furniture footprints (--r3occ dump) and works out which empty strips of each
room can be removed, so the habitat shrinks without moving anything relative to its own room walls."""
import json, sys, math
C = 1.25
OLD = {  # current rects (cells): x, y, w, h
 "airlock": [1, 1, 3, 7], "hygiene": [4, 1, 3, 7], "quarters": [7, 1, 8, 7], "communal": [15, 1, 8, 7], "greenhouse": [23, 1, 5, 7],
 "corridor": [1, 8, 27, 3], "medical": [1, 11, 5, 7], "eclss": [6, 11, 5, 7], "storage": [11, 11, 3, 7], "work": [14, 11, 8, 7], "exercise": [22, 11, 6, 7]}
KEEP = 0.9   # keep at least this much clear floor in every gap (a walkway)

def intervals(room, axis, prims):
    x, y, w, h = OLD[room]
    lo = (x if axis == 0 else y) * C; hi = lo + (w if axis == 0 else h) * C
    olo = (y if axis == 0 else x) * C; ohi = olo + (h if axis == 0 else w) * C
    iv = []
    for p in prims:
        a0, a1 = (p[1], p[4]) if axis == 0 else (p[3], p[6])
        b0, b1 = (p[3], p[6]) if axis == 0 else (p[1], p[4])
        if p[5] < 0.02 or p[2] > 3.4: continue                     # floor/ceiling slabs
        if b1 <= olo + 0.01 or b0 >= ohi - 0.01 or a1 <= lo + 0.01 or a0 >= hi - 0.01: continue
        if a1 - a0 > (hi - lo) * 0.9: continue                        # spans the room (rails, trays): ignore
        iv.append([max(a0, lo), min(a1, hi)])
    iv.sort(); merged = []
    for a, b in iv:
        if merged and a <= merged[-1][1] + 0.02: merged[-1][1] = max(merged[-1][1], b)
        else: merged.append([a, b])
    gaps = []; cur = lo
    for a, b in merged:
        if a > cur: gaps.append([cur, a])
        cur = max(cur, b)
    if cur < hi: gaps.append([cur, hi])
    return lo, hi, merged, gaps

if __name__ == "__main__":
    d = json.load(open(sys.argv[1]))
    prims = d["prims"] + [["inter", p[1] - 0.2, p[2] - 0.2, p[3] - 0.2, p[1] + 0.2, p[2] + 0.2, p[3] + 0.2] for p in d["inters"]] \
          + [["scr", p[1] - 0.15, p[2] - 0.2, p[3] - 0.15, p[1] + 0.15, p[2] + 0.2, p[3] + 0.15] for p in d["screens"]]
    for room in OLD:
        for axis in (0, 1):
            lo, hi, occ, gaps = intervals(room, axis, prims)
            can = sum(max(0.0, (b - a) - KEEP) for a, b in gaps)
            print("%-10s %s len %.2f  occupied %.2f  deletable %.2f m = %d cells  gaps %s" % (room, "xz"[axis], hi - lo,
                  sum(b - a for a, b in occ), can, int(can / C), [round(b - a, 2) for a, b in gaps]))
