"""Shared geometry for the NASA HUNCH print kits: profiles swept around circles or pill (stadium) outlines,
half-revolutions for split modules, watertight checks, binary STL, specs and layout guides."""
import math, os, struct, json
from collections import Counter
import numpy as np

WALL, CLEAR, LIP_W, LIP_H = 1.6, 0.35, 1.2, 2.4
SKIRT = LIP_H + 0.6
SEGS, ARC = 128, 32
MAG_D, MAG_H = 6.3, 3.2          # pocket for a 6 × 3 mm neodymium disc magnet (+ clearance)
PLA = 1.24                       # g/cm³


# ------------------------------------------------------------------ outlines
def circle_loop(n=SEGS):
    th = np.linspace(0, 2 * math.pi, n, endpoint=False)
    N = np.stack([np.cos(th), np.sin(th)], 1)
    return np.zeros_like(N), N


def stadium_loop(a, n=SEGS):
    """center line from (−a,0) to (a,0); returns per-sample centre point and outward unit normal, CCW"""
    narc = n // 4; nstr = n // 4
    C, N = [], []
    for t in np.linspace(-math.pi / 2, math.pi / 2, narc, endpoint=False):      # right cap around (a,0)
        C.append((a, 0)); N.append((math.cos(t), math.sin(t)))
    for s in np.linspace(a, -a, nstr, endpoint=False):                           # top side, going −x
        C.append((s, 0)); N.append((0, 1))
    for t in np.linspace(math.pi / 2, 3 * math.pi / 2, narc, endpoint=False):    # left cap around (−a,0)
        C.append((-a, 0)); N.append((math.cos(t), math.sin(t)))
    for s in np.linspace(-a, a, nstr, endpoint=False):                           # bottom side, going +x
        C.append((s, 0)); N.append((0, -1))
    return np.array(C, float), np.array(N, float)


def poly_area(p):
    return 0.5 * sum(p[i][0] * p[(i + 1) % len(p)][1] - p[(i + 1) % len(p)][0] * p[i][1] for i in range(len(p)))


def sweep(profile, loop=None):
    """profile (r, z), r >= 0, swept around a closed outline (default: circle). Z up. Outward-facing triangles."""
    if poly_area(profile) < 0: profile = profile[::-1]
    C, N = loop if loop is not None else circle_loop()
    K = len(C)
    verts, index = [], {}
    def vid(p):
        key = (round(p[0], 6), round(p[1], 6), round(p[2], 6))
        if key not in index: index[key] = len(verts); verts.append(p)
        return index[key]
    rings = [[vid((C[k, 0] + r * N[k, 0], C[k, 1] + r * N[k, 1], z)) for k in range(K)] for r, z in profile]
    faces = []
    n = len(profile)
    for i in range(n):
        j = (i + 1) % n
        if profile[i][0] <= 1e-9 and profile[j][0] <= 1e-9: continue     # edge on the centre line: no surface
        for k in range(K):
            k2 = (k + 1) % K
            A, B, D, Cc = rings[i][k], rings[i][k2], rings[j][k], rings[j][k2]
            for t in ((A, B, D), (B, Cc, D)):
                if len(set(t)) == 3: faces.append(t)
    return np.array(verts, float), np.array(faces, np.int64)


def ear_clip(poly):
    idx = list(range(len(poly))); tris = []
    def a2(a, b, c): return (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])
    def inside(p, a, b, c): return a2(a, b, p) >= -1e-12 and a2(b, c, p) >= -1e-12 and a2(c, a, p) >= -1e-12
    while len(idx) > 3:
        n = len(idx)
        for k in range(n):
            i0, i1, i2 = idx[(k - 1) % n], idx[k], idx[(k + 1) % n]
            a, b, c = poly[i0], poly[i1], poly[i2]
            if a2(a, b, c) <= 1e-12: continue
            if any(inside(poly[j], a, b, c) for j in idx if j not in (i0, i1, i2)): continue
            tris.append((i0, i1, i2)); idx.pop(k); break
        else: raise RuntimeError("ear clipping failed")
    tris.append(tuple(idx)); return tris


def half_tube(profile, segs=SEGS):
    """profile (r, s) revolved 180° about the X axis; lies flat on its cut face (z = 0), curved side up."""
    if poly_area(profile) < 0: profile = profile[::-1]
    U, V, W = np.array([0., 1, 0]), np.array([0., 0, 1]), np.array([1., 0, 0])
    nth = segs // 2 + 1; th = np.linspace(0, math.pi, nth)
    verts, rings = [], []
    for r, s in profile:
        if r <= 1e-9: verts.append(s * W); rings.append([len(verts) - 1] * nth)
        else:
            ring = []
            for t in th: verts.append(r * (math.cos(t) * U + math.sin(t) * V) + s * W); ring.append(len(verts) - 1)
            rings.append(ring)
    faces = []; n = len(profile)
    for i in range(n):
        j = (i + 1) % n
        if profile[i][0] <= 1e-9 and profile[j][0] <= 1e-9: continue
        for k in range(nth - 1):
            A, B, D, Cc = rings[i][k], rings[i][k + 1], rings[j][k], rings[j][k + 1]
            for t in ((A, B, D), (B, Cc, D)):
                if len(set(t)) == 3: faces.append(t)
    for a, b, c in ear_clip(profile): faces.append((rings[a][0], rings[b][0], rings[c][0]))
    for a, b, c in ear_clip(profile): faces.append((rings[a][-1], rings[c][-1], rings[b][-1]))
    return np.array(verts), np.array(faces, np.int64)


def check(verts, faces, name):
    e = Counter()
    for a, b, c in faces:
        for x, y in ((a, b), (b, c), (c, a)): e[(x, y)] += 1
    bad = sum(1 for (x, y), k in e.items() if k != 1 or e.get((y, x), 0) != 1)
    v = verts[faces]
    vol = np.einsum('ij,ij->i', v[:, 0], np.cross(v[:, 1], v[:, 2])).sum() / 6.0
    if bad or vol <= 0: raise RuntimeError(f"{name}: not watertight ({bad} bad edges) or inverted (vol {vol:.1f})")
    area = 0.5 * np.linalg.norm(np.cross(v[:, 1] - v[:, 0], v[:, 2] - v[:, 0]), axis=1).sum()
    return vol, area


def merge(*meshes, offsets=None):
    V, F, base = [], [], 0
    for i, (v, f) in enumerate(meshes):
        v = v + (np.array(offsets[i]) if offsets else 0)
        V.append(v); F.append(f + base); base += len(v)
    return np.vstack(V), np.vstack(F)


def write_stl(path, verts, faces, name):
    v = verts[faces]
    n = np.cross(v[:, 1] - v[:, 0], v[:, 2] - v[:, 0]); ln = np.linalg.norm(n, axis=1, keepdims=True); ln[ln == 0] = 1; n /= ln
    os.makedirs(os.path.dirname(path), exist_ok=True)
    rec = np.zeros(len(faces), dtype=[('n', '<f4', 3), ('v', '<f4', (3, 3)), ('a', '<u2')]); rec['n'] = n; rec['v'] = v
    with open(path, 'wb') as f:
        f.write(name.encode()[:80].ljust(80, b' ')); f.write(struct.pack('<I', len(faces))); f.write(rec.tobytes())


def ellipse_arc(R, H, z0, n=ARC, reverse=False):
    pts = [(R * math.cos(t), z0 + H * math.sin(t)) for t in np.linspace(0, math.pi / 2, n)]
    pts[-1] = (0.0, z0 + H)
    return pts[::-1] if reverse else pts


# ------------------------------------------------------------------ profiles (mm)
def dome_closed(R, hw, hd, R_bottom=None, R_top=None):
    """solid building: wall from R_bottom (sloped regolith berm if larger) up to R at height hw, then a dome"""
    Rb = R_bottom or R; Rt = R_top or R
    prof = [(0.0, 0.0), (Rb, 0.0), (R, hw)]
    if Rt > R + 1e-6: prof.append((Rt, hw))
    return prof + ellipse_arc(Rt, hd, hw)[1:]


def base_open(R, hw, R_bottom=None):
    """hollow base (height hw − SKIRT) with a locating lip; the roof's skirt slides over the lip"""
    Rb = R_bottom or R; h = hw - SKIRT; ri = R - WALL
    Rh = Rb + (R - Rb) * (h / hw)                        # outer radius at the rim (follows a sloped berm)
    return [(0.0, 0.0), (Rb, 0.0), (Rh, h), (ri, h), (ri, h + LIP_H), (ri - LIP_W, h + LIP_H), (ri - LIP_W, WALL), (0.0, WALL)]


def roof_open(R, hd, R_top=None):
    """lift-off roof: straight skirt (clears the lip) + dome shell; prints flat side down, no supports"""
    Rt = R_top or R; ri = R - WALL + CLEAR
    return [(Rt, 0.0)] + ellipse_arc(Rt, hd, SKIRT) + ellipse_arc(ri, max(hd - WALL, 1.0), SKIRT, reverse=True) + [(ri, 0.0)]


def stack_ring(R, h):
    """open-ended tower section with the same lip on top as a base, so sections stack in ANY order (interchangeable).
    Inside, a 45° ledge carries the lip (prints without supports)."""
    ri = R - WALL; lip_o = ri - CLEAR; lo = lip_o - LIP_W; d = ri - lo
    return [(ri, 0.0), (R, 0.0), (R, h), (lip_o, h), (lip_o, h + LIP_H), (lo, h + LIP_H), (lo, h - 0.5), (ri, h - 0.5 - d)]


def tube_closed(R, L, magnets=True):
    """solid tube standing on one end; a magnet pocket in each end makes every tube plug into any building"""
    if magnets and R > MAG_D / 2 + 1.5 and L > 2 * MAG_H + 3:
        p = MAG_D / 2
        return [(0.0, MAG_H), (p, MAG_H), (p, 0.0), (R, 0.0), (R, L), (p, L), (p, L - MAG_H), (0.0, L - MAG_H)]
    return [(0.0, 0.0), (R, 0.0), (R, L), (0.0, L)]


def tube_half_open(R, L):
    ri = R - WALL
    return [(0.0, 0.0), (R, 0.0), (R, L), (0.0, L), (0.0, L - WALL), (ri, L - WALL), (ri, WALL), (0.0, WALL)]


def cone_tip(R, L_body, L_nose):
    """rocket: body + nose cone"""
    return [(0.0, 0.0), (R, 0.0), (R, L_body)] + [(R * math.cos(t), L_body + L_nose * math.sin(t)) for t in np.linspace(0, math.pi / 2, 24)][1:-1] + [(0.0, L_body + L_nose)]


# ------------------------------------------------------------------ specs
def spec(vol, area):
    """grams + hours for PLA, 0.2 mm layers, 3 walls (≈1.2 mm), 15 % infill"""
    shell = min(vol, area * 1.2); plastic = shell + 0.15 * max(0.0, vol - shell)
    g = plastic / 1000 * PLA
    return round(g, 1), round(max(0.2, g / 11.0), 1)      # ≈11 g/h on a typical FDM printer
