"""
Lunar Smart Adaptive Habitat (project 3) — 3D-print kit generator.

Builds simple, watertight STL parts from the real layout (layout_export.json, metres, Unity axes) at 1:150 so the whole
base fits on a 2 ft (610 mm) board. Every part is a solid of revolution (a 2-D profile turned on a "lathe"), which gives
small, clean, manifold files that slice without repair.

  closed/  sealed parts: solid domes, core, modules, tunnels, hallways
  open/    the same base with removable roofs:
             • domes + core  → hollow BASE (with a locating lip) + lift-off ROOF
             • modules       → two hollow HALVES (print the file twice; one is the lid)
             • tunnels, hallways stay solid (too small to fit anything inside)

Usage:  python3 make_print_kit.py layout_export.json OUT_DIR [scale_denominator]
"""
import json, math, os, sys, struct
import numpy as np

# ------------------------------------------------------------------ settings (print millimetres)
SCALE_DEN = 150.0
WALL = 1.6        # shell thickness
CLEAR = 0.35      # fit clearance between lid and lip
LIP_W = 1.2       # locating-lip width
LIP_H = 2.4       # locating-lip height
SEGS = 128        # smoothness around the circle
ARC = 32          # smoothness of dome curves


def mm(v_m):
    return v_m * 1000.0 / SCALE_DEN


# ------------------------------------------------------------------ geometry: lathe (revolve a profile)
def ear_clip(poly):
    """triangulate a simple CCW polygon (list of (x, y)); returns index triples"""
    idx = list(range(len(poly)))
    tris = []

    def area2(a, b, c):
        return (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])

    def inside(p, a, b, c):
        return area2(a, b, p) >= -1e-12 and area2(b, c, p) >= -1e-12 and area2(c, a, p) >= -1e-12

    guard = 0
    while len(idx) > 3 and guard < 100000:
        guard += 1
        n = len(idx)
        for k in range(n):
            i0, i1, i2 = idx[(k - 1) % n], idx[k], idx[(k + 1) % n]
            a, b, c = poly[i0], poly[i1], poly[i2]
            if area2(a, b, c) <= 1e-12:
                continue
            if any(inside(poly[j], a, b, c) for j in idx if j not in (i0, i1, i2)):
                continue
            tris.append((i0, i1, i2))
            idx.pop(k)
            break
        else:
            raise RuntimeError("ear clipping failed")
    tris.append(tuple(idx))
    return tris


def poly_area(poly):
    return 0.5 * sum(poly[i][0] * poly[(i + 1) % len(poly)][1] - poly[(i + 1) % len(poly)][0] * poly[i][1] for i in range(len(poly)))


def revolve(profile, U, V, W, origin=(0, 0, 0), half=False, segs=SEGS):
    """profile: list of (r, s), r >= 0. Revolved about axis W through origin; e_r = cosθ U + sinθ V (U × V = W).
    full: θ ∈ [0, 2π) · half: θ ∈ [0, π] with the two flat cut faces capped. Returns (verts, faces) outward-facing."""
    if poly_area(profile) < 0:
        profile = profile[::-1]
    U, V, W, O = (np.array(x, float) for x in (U, V, W, origin))
    nth = segs // 2 + 1 if half else segs
    th = np.linspace(0, math.pi, nth) if half else np.linspace(0, 2 * math.pi, segs, endpoint=False)
    verts, rings = [], []
    for r, s in profile:
        if r <= 1e-9:
            verts.append(O + s * W)
            rings.append([len(verts) - 1] * nth)
        else:
            ring = []
            for t in th:
                verts.append(O + r * (math.cos(t) * U + math.sin(t) * V) + s * W)
                ring.append(len(verts) - 1)
            rings.append(ring)
    faces = []
    n = len(profile)
    kmax = nth - 1 if half else nth
    for i in range(n):
        j = (i + 1) % n
        ri, rj = profile[i][0] <= 1e-9, profile[j][0] <= 1e-9
        if ri and rj:
            continue
        for k in range(kmax):
            k2 = (k + 1) % nth
            A, B = rings[i][k], rings[i][k2]
            D, C = rings[j][k], rings[j][k2]
            if ri:
                faces.append((A, C, D))
            elif rj:
                faces.append((A, B, D))
            else:
                faces.append((A, B, D)); faces.append((B, C, D))
    if half:
        tris = ear_clip(profile)
        for a, b, c in tris:            # θ = 0 cap: CCW in (r, s) → normal −e_θ(0) (outward)
            faces.append((rings[a][0], rings[b][0], rings[c][0]))
        for a, b, c in tris:            # θ = π cap: reversed
            faces.append((rings[a][-1], rings[c][-1], rings[b][-1]))
    return np.array(verts), np.array(faces, dtype=np.int64)


def check(verts, faces, name):
    """watertight + consistently oriented: every directed edge must have exactly one opposite"""
    from collections import Counter
    e = Counter()
    for a, b, c in faces:
        for x, y in ((a, b), (b, c), (c, a)):
            e[(x, y)] += 1
    bad = sum(1 for (x, y), k in e.items() if k != 1 or e.get((y, x), 0) != 1)
    v = verts[faces]
    vol = np.einsum('ij,ij->i', v[:, 0], np.cross(v[:, 1], v[:, 2])).sum() / 6.0
    if bad or vol <= 0:
        raise RuntimeError(f"{name}: not watertight ({bad} bad edges) or inverted (vol {vol:.1f})")
    return vol


def write_stl(path, verts, faces, name):
    v = verts[faces]
    n = np.cross(v[:, 1] - v[:, 0], v[:, 2] - v[:, 0])
    ln = np.linalg.norm(n, axis=1, keepdims=True); ln[ln == 0] = 1; n /= ln
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, 'wb') as f:
        f.write(name.encode()[:80].ljust(80, b' '))
        f.write(struct.pack('<I', len(faces)))
        rec = np.zeros(len(faces), dtype=[('n', '<f4', 3), ('v', '<f4', (3, 3)), ('a', '<u2')])
        rec['n'] = n; rec['v'] = v
        f.write(rec.tobytes())


def ellipse_arc(R, H, z0, n=ARC, reverse=False):
    """quarter ellipse from (R, z0) up to (0, z0 + H)"""
    pts = [(R * math.cos(t), z0 + H * math.sin(t)) for t in np.linspace(0, math.pi / 2, n)]
    pts[-1] = (0.0, z0 + H)
    return pts[::-1] if reverse else pts


# ------------------------------------------------------------------ part profiles (mm)
def dome_closed(R, hw, hd, R_top=None):
    """solid: cylinder wall (radius R, height hw) + elliptical roof (radius R_top, height hd)"""
    Rt = R_top or R
    prof = [(0.0, 0.0), (R, 0.0), (R, hw)]
    if Rt > R + 1e-6:
        prof.append((Rt, hw))
    prof += ellipse_arc(Rt, hd, hw)[1:]
    return prof


SKIRT = LIP_H + 0.6   # straight bottom band of every roof; it slides over the lip


def dome_base_open(R, hw):
    """hollow cup (height hw − SKIRT) with a locating lip that rises above the rim"""
    ri = R - WALL; h = hw - SKIRT
    return [(0.0, 0.0), (R, 0.0), (R, h), (ri, h), (ri, h + LIP_H), (ri - LIP_W, h + LIP_H), (ri - LIP_W, WALL), (0.0, WALL)]


def dome_roof_open(R, hw, hd, R_top=None):
    """lift-off roof: straight skirt (clears the lip) + elliptical shell; prints flat side down, no supports"""
    Rt = R_top or R
    ri = R - WALL + CLEAR                     # inside radius of the skirt — clears the base's lip
    outer = [(Rt, 0.0)] + ellipse_arc(Rt, hd, SKIRT)                          # (Rt,0) (Rt,SK) … (0,SK+hd)
    inner = ellipse_arc(ri, hd - WALL, SKIRT, reverse=True) + [(ri, 0.0)]    # (0,SK+hd−W) … (ri,SK) (ri,0)
    return outer + inner


def tube_closed(R, L):
    return [(0.0, 0.0), (R, 0.0), (R, L), (0.0, L)]


def tube_half_open(R, L):
    """hollow half-tube with closed ends; revolve 180° → a trough that lies flat on its cut face"""
    ri = R - WALL
    return [(0.0, 0.0), (R, 0.0), (R, L), (0.0, L), (0.0, L - WALL), (ri, L - WALL), (ri, WALL), (0.0, WALL)]


# ------------------------------------------------------------------ the habitat
NODE_NAMES = {"N": "Node_Command", "E": "Node_Maintenance", "H": "Node_Medical_StormShelter", "R": "Node_Recreation_Commons",
              "G": "Node_CrewSupport_Hygiene2", "K": "Node_Quarters_Vestibule",
              "SQ1": "SleepDome_Quarters1", "SQ2": "SleepDome_Quarters2", "SQ3": "SleepDome_Quarters3", "SQ4": "SleepDome_Quarters4"}
MODULES = {"A": "Module_Airlock_EVA", "B": "Module_Lab", "C": "Module_Greenhouse", "D": "Module_Wellness"}
HALLS = [("N01", "B", "N"), ("H01", "D", "H"), ("E01", "C", "E"), ("L1", "N", "R"), ("L2", "R", "E"), ("L3", "E", "G"),
         ("L4", "G", "H"), ("K", "H", "K"), ("SQ1", "K", "SQ1"), ("SQ2", "K", "SQ2"), ("SQ3", "K", "SQ3"), ("SQ4", "K", "SQ4")]


def main(layout_path, out_dir):
    objs = {o['n']: o for o in json.load(open(layout_path))['objects']}
    parts = []      # (set, filename, verts, faces, note, count)
    plan = []       # footprint items for the layout guide

    def add(sets, fname, vf, note, count=1):
        v, f = vf
        vol = check(v, f, fname)
        for st in sets:
            parts.append((st, fname, v, f, note, count, vol))

    Z = (0, 0, 1); X = (1, 0, 0); Y = (0, 1, 0)
    # ---- core: rigid lower deck + inflatable upper deck
    lo = objs["CORE_C01_LowerDeck_RigidHull_Aluminum"]; up = objs["CORE_C10_UpperDeck_Inflatable_BetaCloth_Outer"]
    R1, hw, R2, H2 = mm(lo['sx'] / 2), mm(lo['sy']), mm(up['sx'] / 2), mm(up['sy'])
    add(["closed"], "01_Core.stl", revolve(dome_closed(R1, hw, H2, R2), X, Y, Z), "core habitat (rigid lower deck + inflatable upper deck)")
    add(["open"], "01_Core_BASE.stl", revolve(dome_base_open(R1, hw), X, Y, Z), "core lower deck, hollow, with locating lip")
    add(["open"], "01_Core_ROOF.stl", revolve(dome_roof_open(R1, hw, H2, R2), X, Y, Z), "core inflatable upper deck, lift-off roof")
    plan.append(("circle", lo['cx'], lo['cz'], lo['sx'] / 2, "CORE"))

    # ---- modules (horizontal cylinders) + their transition tunnels
    k = 2
    for code, nm in MODULES.items():
        h = objs[f"{code}10_PressureHull"]; t = objs[f"{code}01_TransitionTunnel_Hull"]
        along_x = h['sx'] > h['sz']
        L, R = mm(max(h['sx'], h['sz'])), mm(h['sy'] / 2)
        fn = f"{k:02d}_{nm}"
        add(["closed"], fn + ".stl", revolve(tube_closed(R, L), X, Y, Z), f"{nm.replace('_', ' ')} — print standing on one end")
        add(["open"], fn + "_HALF_print_x2.stl", revolve(tube_half_open(R, L), Y, Z, X, half=True), f"{nm.replace('_', ' ')} — hollow half; print TWO (one is the lid)", 2)
        plan.append(("rect", h['cx'], h['cz'], (h['sx'] / 2, h['sz'] / 2), code + " " + nm.split('_', 1)[1].replace('_', ' ')))
        tl, tr = mm(max(t['sx'], t['sz'])), mm(t['sy'] / 2)
        add(["closed", "open"], f"{k:02d}b_Tunnel_{code}.stl", revolve(tube_closed(tr, tl), X, Y, Z), f"tunnel core ↔ module {code} (solid)")
        plan.append(("rect", t['cx'], t['cz'], (t['sx'] / 2, t['sz'] / 2), ""))
        k += 1

    # ---- domes: nodes + sleep domes
    centers = {}
    for code, nm in NODE_NAMES.items():
        pre = f"{code}_10_" if code.startswith("SQ") else f"{code}10_"
        hull = objs[pre + "Node_RigidHull"]; roof = objs[pre.replace("10_", "12_") + "Node_DomeRoof"]
        R, hw, hd = mm(hull['sx'] / 2), mm(hull['sy']), mm(roof['sy'])
        fn = f"{k:02d}_{nm}"
        add(["closed"], fn + ".stl", revolve(dome_closed(R, hw, hd), X, Y, Z), nm.replace('_', ' '))
        add(["open"], fn + "_BASE.stl", revolve(dome_base_open(R, hw), X, Y, Z), nm.replace('_', ' ') + " — hollow base with lip")
        add(["open"], fn + "_ROOF.stl", revolve(dome_roof_open(R, hw, hd), X, Y, Z), nm.replace('_', ' ') + " — lift-off dome roof")
        centers[code] = (hull['cx'], hull['cz'], hull['sx'] / 2, None)
        plan.append(("circle", hull['cx'], hull['cz'], hull['sx'] / 2, code))
        k += 1
    for code in MODULES:
        h = objs[f"{code}10_PressureHull"]
        centers[code] = (h['cx'], h['cz'], None, (h['sx'] / 2, h['sz'] / 2))

    # ---- hallways: tubes from wall to wall
    def exit_dist(c, d):
        cx, cz, r, half = c
        if r:
            return r * 0.97
        tx = half[0] / abs(d[0]) if abs(d[0]) > 1e-6 else 1e9
        tz = half[1] / abs(d[1]) if abs(d[1]) > 1e-6 else 1e9
        return min(tx, tz)
    hall_files = []
    for i, (hn, a, b) in enumerate(HALLS):
        A, B = centers[a], centers[b]
        d = np.array([B[0] - A[0], B[1] - A[1]]); dist = np.linalg.norm(d); d /= dist
        L = dist - exit_dist(A, d) - exit_dist(B, -d)
        R = mm(2.62 / 2)
        fn = f"{k:02d}_Hallway_{hn}_{a}_to_{b}.stl"
        add(["closed", "open"], fn, revolve(tube_closed(R, mm(L)), X, Y, Z), f"hallway {a} → {b} ({L:.1f} m real) — print standing on one end")
        p0 = np.array(A[:2]) + d * exit_dist(A, d); p1 = np.array(B[:2]) - d * exit_dist(B, -d)
        plan.append(("hall", p0, p1, 1.31, ""))
        k += 1

    # ---- write
    for st, fn, v, f, note, count, vol in parts:
        write_stl(os.path.join(out_dir, st, fn), v, f, fn[:-4])
    return parts, plan


def layout_svg(plan, path, title):
    """true-scale top-view placement guide (print at 100 %)"""
    xs, zs = [], []
    for it in plan:
        if it[0] == "circle": xs += [it[1] - it[3], it[1] + it[3]]; zs += [it[2] - it[3], it[2] + it[3]]
        elif it[0] == "rect": xs += [it[1] - it[3][0], it[1] + it[3][0]]; zs += [it[2] - it[3][1], it[2] + it[3][1]]
        else: xs += [it[1][0], it[2][0]]; zs += [it[1][1], it[2][1]]
    pad = 3.0
    x0, x1, z0, z1 = min(xs) - pad, max(xs) + pad, min(zs) - pad, max(zs) + pad
    W, H = mm(x1 - x0), mm(z1 - z0) + 14
    def P(x, z): return mm(x - x0), mm(z1 - z) + 14
    o = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{W:.1f}mm" height="{H:.1f}mm" viewBox="0 0 {W:.2f} {H:.2f}" font-family="Arial">',
         f'<rect x="0" y="0" width="{W:.2f}" height="{H:.2f}" fill="white"/>',
         f'<text x="4" y="7" font-size="5" font-weight="bold">{title}</text>',
         f'<text x="4" y="12" font-size="3">Scale 1:{int(SCALE_DEN)} — print this sheet at 100 % (actual size). Grid = 10 mm = {10 * SCALE_DEN / 1000:.1f} m.</text>']
    for gx in np.arange(0, W, 10): o.append(f'<line x1="{gx:.1f}" y1="14" x2="{gx:.1f}" y2="{H:.1f}" stroke="#ddd" stroke-width="0.2"/>')
    for gy in np.arange(14, H, 10): o.append(f'<line x1="0" y1="{gy:.1f}" x2="{W:.1f}" y2="{gy:.1f}" stroke="#ddd" stroke-width="0.2"/>')
    for it in plan:
        if it[0] == "hall":
            (ax, ay), (bx, by) = P(*it[1]), P(*it[2])
            o.append(f'<line x1="{ax:.2f}" y1="{ay:.2f}" x2="{bx:.2f}" y2="{by:.2f}" stroke="#7aa6d6" stroke-width="{mm(2 * it[3]):.2f}" stroke-linecap="butt"/>')
    for it in plan:
        if it[0] == "circle":
            cx, cy = P(it[1], it[2]); r = mm(it[3])
            o.append(f'<circle cx="{cx:.2f}" cy="{cy:.2f}" r="{r:.2f}" fill="#f2efe6" stroke="#333" stroke-width="0.4"/>')
            o.append(f'<text x="{cx:.2f}" y="{cy + 1.5:.2f}" font-size="4" text-anchor="middle" font-weight="bold">{it[4]}</text>')
        elif it[0] == "rect":
            cx, cy = P(it[1], it[2]); hx, hz = mm(it[3][0]), mm(it[3][1])
            o.append(f'<rect x="{cx - hx:.2f}" y="{cy - hz:.2f}" width="{2 * hx:.2f}" height="{2 * hz:.2f}" fill="#e8eef5" stroke="#333" stroke-width="0.4"/>')
            if it[4]: o.append(f'<text x="{cx:.2f}" y="{cy + 1.2:.2f}" font-size="2.6" text-anchor="middle">{it[4]}</text>')
    o.append('</svg>')
    open(path, 'w').write("\n".join(o))
    return W, H


if __name__ == "__main__":
    lay, out = sys.argv[1], sys.argv[2]
    if len(sys.argv) > 3: SCALE_DEN = float(sys.argv[3])
    parts, plan = main(lay, out)
    W, H = layout_svg(plan, os.path.join(out, "00_LAYOUT_GUIDE_print_at_100_percent.svg"), f"Lunar Smart Adaptive Habitat — 3D print layout (1:{int(SCALE_DEN)})")
    rows = []
    for st, fn, v, f, note, count, vol in parts:
        ext = v.max(0) - v.min(0)
        rows.append((st, fn, len(f), ext, vol, note, count))
    json.dump([{"set": r[0], "file": r[1], "triangles": int(r[2]), "size_mm": [round(float(x), 1) for x in r[3]], "volume_cm3": round(r[4] / 1000, 2), "note": r[5], "print": r[6]} for r in rows],
              open(os.path.join(out, "parts.json"), "w"), indent=1)
    print(f"layout {W:.0f} x {H:.0f} mm; parts: {len(rows)}")
    for r in rows: print(f"{r[0]:6s} {r[1]:58s} {r[2]:6d} tris  {r[3][0]:6.1f}x{r[3][1]:6.1f}x{r[3][2]:6.1f} mm  {r[4]/1000:6.2f} cm3  x{r[6]}")
