"""Build the NASA HUNCH 3-D print kits for project 1, project 2 and project 3 (+ display extras).

python3 build_all.py OUT p1.pkl p2.pkl layout_export.json
"""
import sys, os, json, math, pickle, shutil
import numpy as np
import kitlib as K

OUT = sys.argv[1]
P1_PKL, P2_PKL, P3_LAYOUT = sys.argv[2], sys.argv[3], sys.argv[4]


class Kit:
    def __init__(self, folder, title, scale_den, note=""):
        self.folder, self.title, self.den, self.note = folder, title, scale_den, note
        self.parts = []      # dicts
        self.plan = []       # layout items (metres)

    def mm(self, m): return m * 1000.0 / self.den

    def add(self, sets, fname, mesh, what, where, count=1, group=""):
        v, f = mesh
        vol, area = K.check(v, f, fname)
        g, h = K.spec(vol, area)
        ext = v.max(0) - v.min(0)
        for st in sets:
            self.parts.append(dict(set=st, file=fname, v=v, f=f, what=what, where=where, count=count, group=group,
                                   size=[round(float(x), 1) for x in ext], grams=g, hours=h, tris=len(f)))

    def write(self):
        root = os.path.join(OUT, self.folder)
        for p in self.parts:
            K.write_stl(os.path.join(root, p['set'], p['file']), p['v'], p['f'], p['file'][:-4])
        json.dump([{k: v for k, v in p.items() if k not in ('v', 'f')} for p in self.parts], open(os.path.join(root, 'parts.json'), 'w'), indent=1)
        if self.plan: layout_svg(self, os.path.join(root, '00_LAYOUT_GUIDE_print_at_100_percent.svg'))
        return root


# ------------------------------------------------------------------ layout guide
def layout_svg(kit, path):
    xs, zs = [], []
    for it in kit.plan:
        t = it[0]
        if t == 'circle': xs += [it[1] - it[3], it[1] + it[3]]; zs += [it[2] - it[3], it[2] + it[3]]
        elif t == 'pill': xs += [it[1] - it[3] - it[4], it[1] + it[3] + it[4]]; zs += [it[2] - it[4], it[2] + it[4]]
        elif t == 'rect': xs += [it[1] - it[3], it[1] + it[3]]; zs += [it[2] - it[4], it[2] + it[4]]
        else: xs += [it[1][0], it[2][0]]; zs += [it[1][1], it[2][1]]
    pad = 3.0; x0, x1, z0, z1 = min(xs) - pad, max(xs) + pad, min(zs) - pad, max(zs) + pad
    m = kit.mm; W, H = m(x1 - x0), m(z1 - z0) + 16
    P = lambda x, z: (m(x - x0), m(z1 - z) + 16)
    o = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{W:.1f}mm" height="{H:.1f}mm" viewBox="0 0 {W:.2f} {H:.2f}" font-family="Arial">',
         f'<rect width="{W:.2f}" height="{H:.2f}" fill="white"/>',
         f'<text x="4" y="7" font-size="5" font-weight="bold">{kit.title} — where every part goes</text>',
         f'<text x="4" y="12.5" font-size="3">Scale 1:{int(kit.den)} · print at 100 % (actual size) · grid 10 mm = {10 * kit.den / 1000:.1f} m · footprint {W:.0f} × {H - 16:.0f} mm</text>']
    for gx in np.arange(0, W, 10): o.append(f'<line x1="{gx:.1f}" y1="16" x2="{gx:.1f}" y2="{H:.1f}" stroke="#e3e3e3" stroke-width="0.2"/>')
    for gy in np.arange(16, H, 10): o.append(f'<line x1="0" y1="{gy:.1f}" x2="{W:.1f}" y2="{gy:.1f}" stroke="#e3e3e3" stroke-width="0.2"/>')
    for it in kit.plan:
        if it[0] == 'hall':
            (ax, ay), (bx, by) = P(*it[1]), P(*it[2])
            o.append(f'<line x1="{ax:.2f}" y1="{ay:.2f}" x2="{bx:.2f}" y2="{by:.2f}" stroke="#8fb3dd" stroke-width="{m(2 * it[3]):.2f}"/>')
            if it[4]: o.append(f'<text x="{(ax + bx) / 2:.2f}" y="{(ay + by) / 2 + 1:.2f}" font-size="2.4" text-anchor="middle" fill="#1d3f6e">{it[4]}</text>')
    for it in kit.plan:
        if it[0] == 'circle':
            cx, cy = P(it[1], it[2])
            o.append(f'<circle cx="{cx:.2f}" cy="{cy:.2f}" r="{m(it[3]):.2f}" fill="#f3efe4" stroke="#333" stroke-width="0.4"/>')
            o.append(f'<text x="{cx:.2f}" y="{cy + 1.2:.2f}" font-size="{3.4 if m(it[3]) > 12 else 2.4}" text-anchor="middle" font-weight="bold">{it[4]}</text>')
        elif it[0] == 'pill':
            cx, cy = P(it[1], it[2]); a, r = m(it[3]), m(it[4])
            o.append(f'<rect x="{cx - a - r:.2f}" y="{cy - r:.2f}" width="{2 * (a + r):.2f}" height="{2 * r:.2f}" rx="{r:.2f}" fill="#f3efe4" stroke="#333" stroke-width="0.4"/>')
            o.append(f'<text x="{cx:.2f}" y="{cy + 1.2:.2f}" font-size="3" text-anchor="middle" font-weight="bold">{it[5]}</text>')
        elif it[0] == 'rect':
            cx, cy = P(it[1], it[2]); hx, hz = m(it[3]), m(it[4])
            o.append(f'<rect x="{cx - hx:.2f}" y="{cy - hz:.2f}" width="{2 * hx:.2f}" height="{2 * hz:.2f}" rx="{min(hx, hz):.2f}" fill="#e9eff7" stroke="#333" stroke-width="0.4"/>')
            if it[5]: o.append(f'<text x="{cx:.2f}" y="{cy + 1:.2f}" font-size="2.6" text-anchor="middle">{it[5]}</text>')
    o.append('</svg>'); open(path, 'w').write('\n'.join(o))


def tube_between(kit, A, B, rA, rB, R_m, name, label, sets=("closed", "open"), where=""):
    """hallway / tunnel from wall of A to wall of B (A, B = (x, z) metres)"""
    d = np.array([B[0] - A[0], B[1] - A[1]], float); dist = np.linalg.norm(d); d /= dist
    L = dist - rA - rB
    kit.add(list(sets), name, K.sweep(K.tube_closed(kit.mm(R_m), kit.mm(L))), f"{label} ({L:.1f} m real) — print standing on one end; magnet pocket in each end",
            where or label, group="Hallways & tunnels")
    p0 = np.array(A) + d * rA; p1 = np.array(B) - d * rB
    kit.plan.append(('hall', tuple(p0), tuple(p1), R_m, ""))


# ------------------------------------------------------------------ projects 1 & 2 (same design; project 2 is 1.5× bigger)
def build_p12(pkl, folder, title, den, note):
    objs = pickle.load(open(pkl, 'rb'))
    O = {o['name'][6:]: o['verts'] for o in objs if o['name'].startswith('BAKED_')}
    bb = lambda n: (O[n].min(0), O[n].max(0))
    kit = Kit(folder, title, den, note); m = kit.mm
    # --- central hub: regolith berm + 4 identical stackable tower floors + dome roof
    smn, smx = bb('HUB_Shell'); tmn, tmx = bb('HUB_Tower_Shell'); rmn, rmx = bb('HUB_TowerRoof')
    Rb, R, hb, top, hd = smx[0], tmx[0], smx[1], tmx[1], rmx[1] - tmx[1]
    prof = [(0.0, 0.0), (m(Rb), 0.0), (m(R), m(hb)), (m(R), m(top))] + K.ellipse_arc(m(R), m(hd), m(top))[1:]
    kit.add(["closed"], "01_Hub_Tower.stl", K.sweep(prof), "Central hub + 5-floor tower (one solid piece)", "centre of the board", group="Hub")
    ri = m(R) - K.WALL; lip_o = ri - K.CLEAR; lo = lip_o - K.LIP_W
    base = [(0.0, 0.0), (m(Rb), 0.0), (m(R), m(hb)), (lip_o, m(hb)), (lip_o, m(hb) + K.LIP_H), (lo, m(hb) + K.LIP_H), (lo, K.WALL), (0.0, K.WALL)]
    kit.add(["open"], "01a_Hub_BASE_ground_floor.stl", K.sweep(base), "Hub ground floor with regolith berm — the tower floors stack on its lip", "centre of the board", group="Hub")
    hr = (m(top - hb) - K.SKIRT) / 4
    kit.add(["open"], "01b_Hub_TOWER_FLOOR_print_x4.stl", K.sweep(K.stack_ring(m(R), hr)),
            "Tower floor (mezzanine / command / emergency medical / ICU) — 4 IDENTICAL rings: stack them in any order", "on top of the hub base", count=4, group="Hub")
    kit.add(["open"], "01c_Hub_ROOF.stl", K.sweep(K.roof_open(m(R), m(hd))), "Tower dome roof — sits on the top floor", "top of the tower", group="Hub")
    kit.plan.append(('circle', 0, 0, Rb, 'HUB'))
    # --- crew core: pill-shaped two-deck habitat
    cmn, cmx = bb('CORE_Roof'); umn, umx = bb('CORE_UpperShell'); shmn, shmx = bb('CORE_Shell')
    L, Wd = cmx[0] - cmn[0], cmx[2] - cmn[2]; r = Wd / 2; a = (L - Wd) / 2; ccx, ccz = (cmx[0] + cmn[0]) / 2, (cmx[2] + cmn[2]) / 2
    hw, hdc, rb = umx[1], cmx[1] - umx[1], shmx[2]
    loop = K.stadium_loop(m(a))
    kit.add(["closed"], "02_Crew_Core.stl", K.sweep(K.dome_closed(m(r), m(hw), m(hdc), R_bottom=m(rb)), loop), "Crew habitat core (pill shape, two decks)", "west of the hub", group="Buildings")
    kit.add(["open"], "02a_Crew_Core_BASE.stl", K.sweep(K.base_open(m(r), m(hw), R_bottom=m(rb)), loop), "Crew core — hollow base with lip", "west of the hub", group="Buildings")
    kit.add(["open"], "02b_Crew_Core_ROOF.stl", K.sweep(K.roof_open(m(r), m(hdc)), loop), "Crew core — lift-off roof", "west of the hub", group="Buildings")
    kit.plan.append(('pill', ccx, ccz, a, r, 'CREW CORE'))
    # --- five domes with regolith berms
    names = {'AGRI': 'Agriculture', 'UTIL': 'Utility', 'MED': 'Medical', 'SCI': 'Science', 'LOG': 'Logistics'}
    where = {'AGRI': 'north-west', 'UTIL': 'north-east', 'MED': 'south-west', 'SCI': 'south-east', 'LOG': 'east of the hub'}
    centers = {'HUB': (0.0, 0.0, Rb * 0.97), 'CORE': (ccx, ccz, None, (a + r, r))}
    k = 3
    for code, nm in names.items():
        rmn, rmx = bb(code + '_Roof'); smn, smx = bb(code + '_Shell')
        Rd = (rmx[0] - rmn[0]) / 2; Rb2 = (smx[0] - smn[0]) / 2; hw = smx[1]; hd = rmx[1] - hw
        cx, cz = (rmx[0] + rmn[0]) / 2, (rmx[2] + rmn[2]) / 2
        fn = f"{k:02d}_{nm}_Dome"
        kit.add(["closed"], fn + ".stl", K.sweep(K.dome_closed(m(Rd), m(hw), m(hd), R_bottom=m(Rb2))), f"{nm} dome with regolith berm", where[code], group="Buildings")
        kit.add(["open"], fn + "_BASE.stl", K.sweep(K.base_open(m(Rd), m(hw), R_bottom=m(Rb2))), f"{nm} dome — hollow base with lip", where[code], group="Buildings")
        kit.add(["open"], fn + "_ROOF.stl", K.sweep(K.roof_open(m(Rd), m(hd))), f"{nm} dome — lift-off roof", where[code], group="Buildings")
        kit.plan.append(('circle', cx, cz, Rb2, nm.upper())); centers[code] = (cx, cz, Rb2 * 0.97); k += 1
    # --- six junction nodes (all identical → interchangeable)
    v = O['NODES_Shell']; rv = O['NODES_Roof']
    hw_n = v[:, 1].max(); hd_n = rv[:, 1].max() - hw_n
    ang = [0, 60, 120, 180, 240, 300]; dist_n = 20.0 * (1.5 if '1.5' in note else 1.0)
    nodes = []
    for t in ang:
        p = (dist_n * math.cos(math.radians(t)), dist_n * math.sin(math.radians(t)))
        sel = (np.hypot(v[:, 0] - p[0], v[:, 2] - p[1]) < dist_n * 0.25)
        if sel.sum() == 0: continue
        q = v[sel]; c = ((q[:, 0].max() + q[:, 0].min()) / 2, (q[:, 2].max() + q[:, 2].min()) / 2); Rn = (q[:, 0].max() - q[:, 0].min()) / 2
        nodes.append((c, Rn))
    Rn = float(np.mean([n[1] for n in nodes]))
    kit.add(["closed"], f"{k:02d}_Junction_Node_print_x{len(nodes)}.stl", K.sweep(K.dome_closed(m(Rn), m(hw_n), m(hd_n))), f"Junction node — {len(nodes)} identical, any one fits any spot", "the 6 small circles around the hub", count=len(nodes), group="Nodes")
    kit.add(["open"], f"{k:02d}_Junction_Node_BASE_print_x{len(nodes)}.stl", K.sweep(K.base_open(m(Rn), m(hw_n))), "Junction node — hollow base", "the 6 small circles around the hub", count=len(nodes), group="Nodes")
    kit.add(["open"], f"{k:02d}_Junction_Node_ROOF_print_x{len(nodes)}.stl", K.sweep(K.roof_open(m(Rn), m(hd_n))), "Junction node — roof", "on each node base", count=len(nodes), group="Nodes")
    for i, (c, _) in enumerate(nodes): kit.plan.append(('circle', c[0], c[1], Rn, 'N' + str(i + 1)))
    k += 1
    # --- tunnels: hub ↔ each node, node ↔ nearest building
    tmn, tmx = bb('TUNNELS'); Rt = (tmx[1] - tmn[1]) / 2
    for i, (c, _) in enumerate(nodes):
        tube_between(kit, (0, 0), c, centers['HUB'][2], Rn * 0.97, Rt, f"{k:02d}_Tunnel_Hub_to_N{i + 1}.stl", f"tunnel hub → node N{i + 1}"); k += 1
    for code in ['AGRI', 'UTIL', 'MED', 'SCI', 'LOG', 'CORE']:
        cx, cz = centers[code][0], centers[code][1]
        j = int(np.argmin([math.hypot(c[0] - cx, c[1] - cz) for c, _ in nodes])); c = nodes[j][0]
        rr = centers[code][2] if centers[code][2] else centers[code][3][0] * 0.97
        tube_between(kit, c, (cx, cz), Rn * 0.97, rr, Rt, f"{k:02d}_Tunnel_N{j + 1}_to_{names.get(code, 'CrewCore')}.stl", f"tunnel node N{j + 1} → {names.get(code, 'crew core')}"); k += 1
    return kit, O


# ------------------------------------------------------------------ project 3 (the VR simulator base)
def build_p3(layout, den=150.0):
    objs = {o['n']: o for o in json.load(open(layout))['objects']}
    kit = Kit("Project3_Moon_Base_Final_unity3", "Project 3 — Lunar Smart Adaptive Habitat (VR simulator)", den); m = kit.mm
    lo = objs["CORE_C01_LowerDeck_RigidHull_Aluminum"]; up = objs["CORE_C10_UpperDeck_Inflatable_BetaCloth_Outer"]
    R1, hw, R2, H2 = m(lo['sx'] / 2), m(lo['sy']), m(up['sx'] / 2), m(up['sy'])
    kit.add(["closed"], "01_Core.stl", K.sweep(K.dome_closed(R1, hw, H2, R_top=R2)), "Core habitat (rigid lower deck + inflatable upper deck)", "centre", group="Core & modules")
    kit.add(["open"], "01_Core_BASE.stl", K.sweep(K.base_open(R1, hw)), "Core lower deck — hollow base with lip", "centre", group="Core & modules")
    kit.add(["open"], "01_Core_ROOF.stl", K.sweep(K.roof_open(R1, H2, R_top=R2)), "Core inflatable upper deck — lift-off roof", "centre", group="Core & modules")
    kit.plan.append(('circle', lo['cx'], lo['cz'], lo['sx'] / 2, 'CORE'))
    MODS = {"A": ("Module_Airlock_EVA", "east"), "B": ("Module_Lab", "north"), "C": ("Module_Greenhouse", "west"), "D": ("Module_Wellness", "south")}
    k = 2; centers = {}
    for code, (nm, wh) in MODS.items():
        h = objs[f"{code}10_PressureHull"]; t = objs[f"{code}01_TransitionTunnel_Hull"]
        L, R = m(max(h['sx'], h['sz'])), m(h['sy'] / 2)
        fn = f"{k:02d}_{nm}"
        kit.add(["closed"], fn + ".stl", K.sweep(K.tube_closed(R, L)), f"{nm.replace('_', ' ')} — stands on one end to print", f"{wh} of the core", group="Core & modules")
        kit.add(["open"], fn + "_HALF_print_x2.stl", K.half_tube(K.tube_half_open(R, L)), f"{nm.replace('_', ' ')} — hollow half; print 2 (one is the lid)", f"{wh} of the core", count=2, group="Core & modules")
        kit.plan.append(('rect', h['cx'], h['cz'], h['sx'] / 2, h['sz'] / 2, nm.split('_', 1)[1].replace('_', ' ')))
        kit.add(["closed", "open"], f"{k:02d}b_Tunnel_{code}.stl", K.sweep(K.tube_closed(m(t['sy'] / 2), m(max(t['sx'], t['sz'])))), f"short tunnel core ↔ {nm.split('_', 1)[1].lower()}", f"between core and {nm.split('_', 1)[1].lower()}", group="Hallways & tunnels")
        kit.plan.append(('rect', t['cx'], t['cz'], t['sx'] / 2, t['sz'] / 2, ''))
        centers[code] = (h['cx'], h['cz'], None, (h['sx'] / 2, h['sz'] / 2)); k += 1
    NODES = {"N": "Node_Command", "E": "Node_Maintenance", "H": "Node_Medical_StormShelter", "R": "Node_Recreation_Commons", "G": "Node_CrewSupport_Hygiene2",
             "K": "Node_Quarters_Vestibule", "SQ1": "SleepDome_Quarters1", "SQ2": "SleepDome_Quarters2", "SQ3": "SleepDome_Quarters3", "SQ4": "SleepDome_Quarters4"}
    for code, nm in NODES.items():
        pre = f"{code}_10_" if code.startswith("SQ") else f"{code}10_"
        hull = objs[pre + "Node_RigidHull"]; roof = objs[pre.replace("10_", "12_") + "Node_DomeRoof"]
        R, hwn, hd = m(hull['sx'] / 2), m(hull['sy']), m(roof['sy'])
        fn = f"{k:02d}_{nm}"; grp = "Sleep domes" if code.startswith("SQ") else "Nodes"
        kit.add(["closed"], fn + ".stl", K.sweep(K.dome_closed(R, hwn, hd)), nm.replace('_', ' '), f"circle {code} on the guide", group=grp)
        kit.add(["open"], fn + "_BASE.stl", K.sweep(K.base_open(R, hwn)), nm.replace('_', ' ') + " — hollow base with lip", f"circle {code} on the guide", group=grp)
        kit.add(["open"], fn + "_ROOF.stl", K.sweep(K.roof_open(R, hd)), nm.replace('_', ' ') + " — lift-off roof", f"on base {code}", group=grp)
        centers[code] = (hull['cx'], hull['cz'], hull['sx'] / 2 * 0.97)
        kit.plan.append(('circle', hull['cx'], hull['cz'], hull['sx'] / 2, code)); k += 1
    def exitd(c, d):
        if c[2]: return c[2]
        hx, hz = c[3]; return min(hx / abs(d[0]) if abs(d[0]) > 1e-6 else 1e9, hz / abs(d[1]) if abs(d[1]) > 1e-6 else 1e9)
    HALLS = [("N01", "B", "N"), ("H01", "D", "H"), ("E01", "C", "E"), ("L1", "N", "R"), ("L2", "R", "E"), ("L3", "E", "G"), ("L4", "G", "H"),
             ("K", "H", "K"), ("SQ1", "K", "SQ1"), ("SQ2", "K", "SQ2"), ("SQ3", "K", "SQ3"), ("SQ4", "K", "SQ4")]
    for hn, a, b in HALLS:
        A, B = centers[a], centers[b]; d = np.array([B[0] - A[0], B[1] - A[1]]); d /= np.linalg.norm(d)
        tube_between(kit, A[:2], B[:2], exitd(A, d), exitd(B, -d), 1.31, f"{k:02d}_Hallway_{hn}_{a}_to_{b}.stl", f"hallway {a} → {b}"); k += 1
    # --- exterior extras at the same scale (things the VR base has outside)
    kit.add(["closed", "open"], f"{k:02d}_Landing_Pad.stl", K.sweep([(0.0, 0.0), (m(15), 0.0), (m(15), 1.2), (m(13.5), 2.0), (0.0, 2.0)]), "Cargo landing pad (30 m wide, sintered regolith)", "300 m away in real life — put it at the board edge", group="Exterior extras"); k += 1
    body = [(0.0, 0.0), (m(1.3), 0.0), (m(2.1), m(1.8)), (m(2.1), m(5.0))] + K.ellipse_arc(m(2.1), m(1.4), m(5.0))[1:]   # descent stage cone + crew/cargo body
    lander = K.merge(K.sweep(body), *[K.sweep(K.tube_closed(m(0.3), m(2.6), magnets=False)) for _ in range(4)],
                     offsets=[(0, 0, 0)] + [(m(2.15) * math.cos(t), m(2.15) * math.sin(t), 0) for t in (0.785, 2.356, 3.927, 5.498)])
    kit.add(["closed", "open"], f"{k:02d}_Cargo_Lander.stl", lander, "Cargo lander (body on 4 legs)", "on the landing pad", group="Exterior extras"); k += 1
    mast = K.merge(K.sweep(K.tube_closed(m(0.35), m(14), magnets=False)), K.sweep([(0.0, 0.0), (m(1.6), 0.0), (m(1.6), 1.6), (0.0, 1.6)]))
    kit.add(["closed", "open"], f"{k:02d}_Solar_Tower_print_x3.stl", mast, "Solar-tower mast on a foot (glue a 10 × 4 mm card 'panel' on top)", "around the base", count=3, group="Exterior extras"); k += 1
    kit.add(["closed", "open"], f"{k:02d}_Comms_Antenna.stl", K.merge(K.sweep(K.tube_closed(m(0.3), m(18), magnets=False)), K.sweep([(0.0, 0.0), (m(1.4), 0.0), (m(1.4), 1.6), (0.0, 1.6)]),
                     K.sweep([(0.0, 0.0), (m(2.2), m(2.2)), (m(2.2), m(2.5)), (0.0, m(0.3))]), offsets=[(0, 0, 0), (0, 0, 0), (0, 0, m(18))]), "Comms mast with dish", "next to the command node", group="Exterior extras"); k += 1
    return kit


# ------------------------------------------------------------------ display extras (1:600) from the project 1 world
def build_extras(O, den=700.0):
    kit = Kit("Extras_Display_1to700", "Display extras (project 1 & 2 world) — 1:700", den); m = kit.mm
    bb = lambda n: (O[n].min(0), O[n].max(0))
    mn, mx = bb('ROCKET'); R = (mx[0] - mn[0]) / 2; H = mx[1] - mn[1]
    rocket = K.merge(K.sweep(K.cone_tip(m(R), m(H * 0.82), m(H * 0.18))),
                     *[K.sweep(K.tube_closed(m(R * 0.35), m(H * 0.12), magnets=False)) for _ in range(3)],
                     offsets=[(0, 0, 0)] + [(m(R * 1.05) * math.cos(t), m(R * 1.05) * math.sin(t), 0) for t in (0, 2.094, 4.189)])
    kit.add(["display"], "01_Rocket.stl", rocket, f"Spaceport rocket ({H:.0f} m tall real)", "spaceport (north-east)", group="Display")
    kit.add(["display"], "02_Landing_Pad.stl", K.sweep([(0.0, 0.0), (m(22), 0.0), (m(22), 1.0), (m(20), 1.6), (0.0, 1.6)]), "Spaceport landing pad", "under the rocket", group="Display")
    mn, mx = bb('KEEP_01'); Rk = (mx[0] - mn[0]) / 2; Hk = mx[1] - mn[1]
    kit.add(["display"], "03_Satellite_Tower_print_x7.stl", K.sweep([(0.0, 0.0), (m(Rk), 0.0), (m(Rk * 0.62), m(Hk * 0.2)), (m(Rk * 0.55), m(Hk * 0.78))] + K.ellipse_arc(m(Rk * 0.55), m(Hk * 0.22), m(Hk * 0.78))[1:]),
            "Satellite 'keep' tower — 7 identical", "ring around the kingdom", count=7, group="Display")
    mn, mx = bb('KINGDOM_Dome'); Rg = (mx[0] - mn[0]) / 2; Hg = mx[1]
    kit.add(["display"], "04_Kingdom_Glass_Dome_COVER.stl", K.sweep(K.roof_open(m(Rg), m(Hg) - K.SKIRT)),
            f"Kingdom glass dome ({2 * Rg:.0f} m wide real) — print in CLEAR PETG/PLA, place over the mini base", "over the mini base", group="Display")
    return kit


def mini_base(kit_src, den_src, den=700.0):
    """the whole project-1 base as ONE small piece on a round plate (closed parts, scaled 1:700)"""
    f = den_src / den; Vs, Fs = [], []
    plate = K.sweep([(0.0, 0.0), (kit_src.mm(52) * f, 0.0), (kit_src.mm(52) * f, 1.6), (0.0, 1.6)])
    meshes = [plate]; offs = [(0, 0, 0)]
    pos = {}
    for it in kit_src.plan:
        if it[0] == 'circle': pos[it[4]] = (it[1], it[2])
        if it[0] == 'pill': pos['CREW CORE'] = (it[1], it[2])
    lookup = {'01_Hub_Tower.stl': 'HUB', '02_Crew_Core.stl': 'CREW CORE', '03_Agriculture_Dome.stl': 'AGRICULTURE', '04_Utility_Dome.stl': 'UTILITY',
              '05_Medical_Dome.stl': 'MEDICAL', '06_Science_Dome.stl': 'SCIENCE', '07_Logistics_Dome.stl': 'LOGISTICS'}
    for p in kit_src.parts:
        if p['set'] != 'closed': continue
        if p['file'] in lookup:
            x, z = pos[lookup[p['file']]]; meshes.append((p['v'] * f, p['f'])); offs.append((kit_src.mm(x) * f, kit_src.mm(z) * f, 1.5))
        elif 'Junction_Node' in p['file']:
            for nm, (x, z) in pos.items():
                if nm.startswith('N') and nm[1:].isdigit(): meshes.append((p['v'] * f, p['f'])); offs.append((kit_src.mm(x) * f, kit_src.mm(z) * f, 1.5))
    return K.merge(*meshes, offsets=offs)


if __name__ == "__main__":
    if os.path.exists(OUT): shutil.rmtree(OUT)
    p1, O1 = build_p12(P1_PKL, "Project1_HUNCH_Lunar_Base_unity1", "Project 1 — HUNCH Lunar Base (unity1)", 150.0, "")
    p2, O2 = build_p12(P2_PKL, "Project2_Lunar_Habitat_Commander_unity2", "Project 2 — Lunar Habitat Commander (unity2, base ×1.5)", 225.0, "x1.5")
    p3 = build_p3(P3_LAYOUT)
    ex = build_extras(O1)
    ex.add(["display"], "05_Mini_Project1_Base_on_plate.stl", mini_base(p1, 150.0), "The whole project-1 base as one small piece on a plate (sits under the glass dome)", "centre of the display", group="Display")
    kits = [p1, p2, p3, ex]
    for kt in kits:
        kt.write()
        print(f"{kt.folder}: {len(kt.parts)} files")
    json.dump({kt.folder: {"title": kt.title, "scale": kt.den, "parts": [{k: v for k, v in p.items() if k not in ('v', 'f')} for p in kt.parts]} for kt in kits},
              open(os.path.join(OUT, "all_parts.json"), "w"), indent=1)
