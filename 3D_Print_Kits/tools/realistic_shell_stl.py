"""Realistic habitat — outer shell only, one STL: every module, node, sleep dome, tunnel and hallway hull
(no interior), plus the lunar regolith protection (3D-printed regolith dome segments and arches, MMOD shields),
on a ground plate so it prints as one piece. Built from the real game model (LunarHabitat_Unity3_Habitat.fbx).

usage: python3 realistic_shell_stl.py <habitat.fbx> <out_dir>
© 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
"""
import re, sys, os, math, json
from collections import defaultdict
import numpy as np
import fbxread, kitlib as K

SCALE = 320.0                 # 1:320 → 1 m real = 3.125 mm
WALL = 0.32                   # real metres → 1.0 mm printed wall
KEEP = re.compile(r'(MMOD_WhiteShield|Bulkhead_Outer_Hatch|TransitionTunnel_Hull|Hallway_Hull|Hallway_Support|Node_RigidHull|Node_DomeRoof|'
                  r'CORE_C01_LowerDeck_RigidHull|CORE_C10_UpperDeck_Inflatable_BetaCloth_Outer|PrintedRegolith)')
SHIELD = re.compile(r'(PrintedRegolith|MMOD)')


def tris(poly):
    """FBX polygon index list (negative = last index of a polygon, stored as ~i) → triangle fan"""
    out, cur = [], []
    for i in poly:
        if i < 0: cur.append(~i); out += [(cur[0], cur[k], cur[k + 1]) for k in range(1, len(cur) - 1)]; cur = []
        else: cur.append(i)
    return np.array(out, np.int64)


def weld(V, F, tol=1e-4):
    key = np.round(V / tol).astype(np.int64); _, first, inv = np.unique(key, axis=0, return_index=True, return_inverse=True)
    F = inv.reshape(-1)[F]; F = F[(F[:, 0] != F[:, 1]) & (F[:, 1] != F[:, 2]) & (F[:, 0] != F[:, 2])]
    return V[first], F


def solidify(V, F, t):
    """surface → watertight shell of thickness t: offset copy along −normal, reversed, boundary edges stitched"""
    fn = np.cross(V[F[:, 1]] - V[F[:, 0]], V[F[:, 2]] - V[F[:, 0]])
    vn = np.zeros_like(V)
    for k in range(3): np.add.at(vn, F[:, k], fn)
    ln = np.linalg.norm(vn, axis=1, keepdims=True); ln[ln == 0] = 1; vn /= ln
    n = len(V); V2 = np.vstack([V, V - vn * t]); F2 = [F, F[:, ::-1] + n]
    ec = defaultdict(int)
    for a, b, c in F:
        for x, y in ((a, b), (b, c), (c, a)): ec[(x, y)] += 1
    side = [(x, y) for (x, y), k in ec.items() if (y, x) not in ec]          # boundary edges (one face only)
    if side:
        s = np.array(side); F2.append(np.c_[s[:, 1], s[:, 0], s[:, 0] + n]); F2.append(np.c_[s[:, 1], s[:, 0] + n, s[:, 1] + n])
    return V2, np.vstack(F2)


def hull2d(P):
    P = sorted(set(map(tuple, np.round(P, 3))))
    def cross(o, a, b): return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])
    lo, hi = [], []
    for p in P:
        while len(lo) >= 2 and cross(lo[-2], lo[-1], p) <= 0: lo.pop()
        lo.append(p)
    for p in reversed(P):
        while len(hi) >= 2 and cross(hi[-2], hi[-1], p) <= 0: hi.pop()
        hi.append(p)
    return np.array(lo[:-1] + hi[:-1])


def main(fbx, out):
    objs, _ = fbxread.scene(fbx)
    parts, names, foot = [], [], []
    for o in objs:
        if not KEEP.search(o['name']) or o['poly'] is None: continue
        v = o['verts']
        if np.abs(v).max() > 200: continue                                    # far-away duplicates (not part of the base)
        F = tris(o['poly']); V, F = weld(v.copy(), F)
        if len(F) == 0: continue
        V, F = solidify(V, F, WALL * (0.6 if SHIELD.search(o['name']) else 1.0))
        P = np.c_[V[:, 0], -V[:, 2], V[:, 1]] * (1000.0 / SCALE)               # Unity Y-up → print Z-up, metres → mm
        parts.append((P, F)); names.append(o['name']); foot.append(P[:, :2])
    allP = np.vstack([p for p, _ in parts]); zmin = allP[:, 2].min()
    # ground plate: the lunar surface the base sits on (joins everything into one printable piece and closes the hull bottoms)
    top = 0.62 * 1000.0 / SCALE                                                # just under the raised hallway floors (0.62 m real)
    h = hull2d(np.vstack(foot)); c = h.mean(0); h = c + (h - c) * 1.03
    ring = len(h); pv = np.vstack([np.c_[h, np.full(ring, zmin - 0.4)], np.c_[h, np.full(ring, top)]])
    pf = [(0, k + 1, k) for k in range(1, ring - 1)] + [(ring, ring + k, ring + k + 1) for k in range(1, ring - 1)]
    for k in range(ring): j = (k + 1) % ring; pf += [(k, j, ring + j), (k, ring + j, ring + k)]
    parts.append((pv, np.array(pf, np.int64))); names.append("ground plate")
    V, F = K.merge(*parts); V[:, 2] -= V[:, 2].min()
    os.makedirs(out, exist_ok=True); path = os.path.join(out, "Lunar_Habitat_Realistic_Shell_Hallways_RegolithShield_1-320.stl")
    K.write_stl(path, V, F, "Lunar Smart Adaptive Habitat - shell + hallways + regolith shield 1:320")
    size = V.max(0) - V.min(0)
    info = {"file": os.path.basename(path), "scale": "1:320", "size_mm": [round(float(x), 1) for x in size], "triangles": int(len(F)), "parts": names}
    json.dump(info, open(os.path.join(out, "parts.json"), "w"), indent=1)
    print(json.dumps({k: info[k] for k in ("file", "size_mm", "triangles")}), len(names), "parts")
    return V, F


if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2])
