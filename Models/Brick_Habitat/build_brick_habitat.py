# Lunar Smart Adaptive Habitat - brick-built engineering prototype. Builds from a BLANK file.
import bpy, math, os, json, time, random, traceback, bmesh
from mathutils import Vector
TMP = r"C:\Users\jaine\AppData\Local\Temp\lunax_helmet_build"
LOGF = os.path.join(TMP, "lego_progress.txt"); open(LOGF, "w").close()
def log(s):
    with open(LOGF, "a") as f: f.write(time.strftime("%H:%M:%S ") + str(s) + "\n")
OUT = r"C:\Users\jaine\Downloads\Nasa\official projects for nasa\LEGO Habitat"
BLEND = os.path.join(OUT, "Lunar_Habitat_Brick_Engineering.blend")
os.makedirs(OUT, exist_ok=True)
if os.path.exists(BLEND): raise SystemExit("exists - refusing to overwrite")
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene; sc.name = "Brick_Habitat"
P, PH = 0.08, 0.032          # stud pitch, plate height (metres, 10x real brick scale)
BH = 3                        # brick = 3 plates
G = 0.0012                    # visible seam gap
TAU = 2 * math.pi
random.seed(7)

# ------------------------------------------------------------------ materials
def lin(h):
    h = h.lstrip('#'); c = [int(h[i:i+2], 16) / 255 for i in (0, 2, 4)]
    return tuple((x / 12.92) if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c)
MATS = {}
def mat(name, hexc, trans=False, emit=0.0, rough=0.28, metal=0.0, alpha=0.42):
    m = bpy.data.materials.new("BRK_" + name); m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]; col = lin(hexc)
    b.inputs["Base Color"].default_value = (*col, 1); b.inputs["Roughness"].default_value = 0.06 if trans else rough
    b.inputs["Metallic"].default_value = metal
    try: b.inputs["Coat Weight"].default_value = 0.35
    except Exception: pass
    if trans:
        b.inputs["Alpha"].default_value = alpha
        try: m.surface_render_method = 'BLENDED'
        except Exception: pass
    if emit:
        b.inputs["Emission Color"].default_value = (*col, 1); b.inputs["Emission Strength"].default_value = emit
    MATS[name] = m; return m
for n, h in [("white", "F4F4F4"), ("lgrey", "A0A5A9"), ("dgrey", "6C6E68"), ("black", "1B2A34"), ("blue", "0055BF"),
             ("azure", "36AEBF"), ("orange", "FE8A18"), ("yellow", "F2CD37"), ("red", "C91A09"), ("green", "237841"),
             ("bgreen", "4B9F4A"), ("tan", "E4CD9E"), ("dtan", "958A73"), ("brown", "583927"), ("ground", "5A5D5A"),
             ("regolith", "7A7466")]:
    mat(n, h)
mat("t_clear", "EEEEEE", trans=True, alpha=0.28); mat("t_lblue", "AEEFEC", trans=True, alpha=0.5)
mat("t_blue", "0A5CC8", trans=True, alpha=0.55); mat("t_red", "E02020", trans=True, emit=3.0, alpha=0.7)
mat("t_green", "4BD45B", trans=True, emit=2.0, alpha=0.7); mat("t_yellow", "F7D117", trans=True, emit=2.0, alpha=0.7)
mat("screen", "3CC8FF", emit=2.5); mat("grow", "D060C0", emit=0.0); mat("water", "4FB8FF", trans=True, emit=0.6, alpha=0.75)
mat("earth_blue", "1E5BB8", emit=0.35); mat("earth_green", "3C8C3C", emit=0.3); mat("earth_white", "F2F2F2", emit=0.4)

# ------------------------------------------------------------------ mesh builder
class MB:
    def __init__(self): self.v = []; self.f = []
    def add(self, vs, fs):
        o = len(self.v); self.v += vs; self.f += [[i + o for i in fc] for fc in fs]
    def box(self, cx, cy, z0, sx, sy, sz, ang=0.0):
        c, s = math.cos(ang), math.sin(ang); hx, hy = sx / 2, sy / 2
        pts = [(-hx, -hy), (hx, -hy), (hx, hy), (-hx, hy)]
        vs = [(cx + c * x - s * y, cy + s * x + c * y, z) for z in (z0, z0 + sz) for (x, y) in pts]
        self.add(vs, [[0, 3, 2, 1], [4, 5, 6, 7], [0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7]])
    def cyl(self, cx, cy, z0, h, r, seg=12, bottom=True):
        vs = [(cx + r * math.cos(TAU * i / seg), cy + r * math.sin(TAU * i / seg), z) for z in (z0, z0 + h) for i in range(seg)]
        fs = [[i, (i + 1) % seg, seg + (i + 1) % seg, seg + i] for i in range(seg)] + [list(range(seg, 2 * seg))]
        if bottom: fs.append(list(range(seg - 1, -1, -1)))
        self.add(vs, fs)
    def studs(self, cx, cy, ztop, w, d, ang=0.0, seg=8):
        c, s = math.cos(ang), math.sin(ang)
        for i in range(w):
            for j in range(d):
                lx, ly = (i - (w - 1) / 2) * P, (j - (d - 1) / 2) * P
                self.cyl(cx + c * lx - s * ly, cy + s * lx + c * ly, ztop, 0.017, 0.024, seg, bottom=False)
    def brick(self, xs, ys, zp, w, d, hp=3, ang=0.0, studs=True):
        """xs,ys = centre in studs, zp = bottom in plates, w x d studs (w along local x), hp plates tall"""
        cx, cy, z0 = xs * P, ys * P, zp * PH
        self.box(cx, cy, z0, w * P - G, d * P - G, hp * PH - G * 0.5, ang)
        if studs: self.studs(cx, cy, z0 + hp * PH - G * 0.5, w, d, ang)
    def round(self, xs, ys, zp, dia, hp=3, studs=True, seg=14):
        cx, cy, z0 = xs * P, ys * P, zp * PH
        self.cyl(cx, cy, z0, hp * PH - G * 0.5, dia * P / 2 - G, seg)
        if studs:
            if dia <= 1: self.cyl(cx, cy, z0 + hp * PH - G * 0.5, 0.017, 0.024, 8, bottom=False)
            else: self.studs(cx, cy, z0 + hp * PH - G * 0.5, int(dia), int(dia))
    def tube(self, p0, p1, r, seg=12):
        a, b = Vector(p0), Vector(p1); ax = (b - a); L = ax.length; ax.normalize()
        up = Vector((0, 0, 1)) if abs(ax.z) < 0.9 else Vector((1, 0, 0))
        u = ax.cross(up).normalized(); v = ax.cross(u).normalized()
        vs = []
        for base in (a, b):
            for i in range(seg):
                t = TAU * i / seg; q = base + (u * math.cos(t) + v * math.sin(t)) * r; vs.append(tuple(q))
        fs = [[i, (i + 1) % seg, seg + (i + 1) % seg, seg + i] for i in range(seg)] + [list(range(seg, 2 * seg)), list(range(seg - 1, -1, -1))]
        self.add(vs, fs)

COLLS = {}
def coll(name, parent=None):
    if name in COLLS: return COLLS[name]
    c = bpy.data.collections.new(name); (parent or sc.collection).children.link(c); COLLS[name] = c; return c
OBJS = {}
def make(mb, name, matname, cname, smooth=False, parent_coll=None):
    if not mb.v: return None
    me = bpy.data.meshes.new(name); me.from_pydata(mb.v, [], mb.f); me.validate(clean_customdata=False)
    bm = bmesh.new(); bm.from_mesh(me); bmesh.ops.recalc_face_normals(bm, faces=bm.faces); bm.to_mesh(me); bm.free()
    if smooth:
        me.polygons.foreach_set("use_smooth", [True] * len(me.polygons))
        try: me.set_sharp_from_angle(angle=math.radians(40))
        except Exception: pass
    me.materials.append(MATS[matname])
    ob = bpy.data.objects.new(name, me); coll(cname, parent_coll).objects.link(ob); OBJS[name] = ob
    return ob

# ------------------------------------------------------------------ construction helpers
ASSEMBLY = []   # (object, module_index, z_order)
CONNECT = []    # (object, dx, dy) corridors that slide in
MOD_INDEX = {}
def M(name):
    if name not in MOD_INDEX: MOD_INDEX[name] = len(MOD_INDEX)
    return MOD_INDEX[name]

def ring_wall(mod, cname, cx, cy, R, courses, colors, skip=None, z0=1, trans_rows=()):
    n = max(10, int(round(TAU * R / 2)))
    for k in range(courses):
        mbs = {}
        for i in range(n):
            a = TAU * (i + (0.5 if k % 2 else 0)) / n
            if skip and skip(a, k): continue
            col = "t_clear" if k in trans_rows else colors[k % len(colors)]
            mbs.setdefault(col, MB()).brick(cx + R * math.cos(a), cy + R * math.sin(a), z0 + k * BH, 2, 1, BH, a + math.pi / 2, studs=(k == courses - 1))
        for col, mb in mbs.items():
            ob = make(mb, f"{mod}_Wall_C{k}_{col}", col, cname, smooth=False)
            if ob: ASSEMBLY.append((ob, M(mod), k))

def rect_wall(mod, cname, cx, cy, W, D, courses, colors, skip=None, z0=1, trans_rows=()):
    for k in range(courses):
        mbs = {}; off = 2 if k % 2 else 0
        segs = []
        x = -W / 2; first = True
        while x < W / 2 - 1e-6:
            L = min(2 if (off and first) else 4, W / 2 - x); first = False
            for y in (-D / 2 + 0.5, D / 2 - 0.5): segs.append((cx + x + L / 2, cy + y, L, 1, 0.0, 'x', x + L / 2, y))
            x += L
        y = -D / 2 + 1; first = True
        while y < D / 2 - 1 - 1e-6:
            L = min(2 if (off and first) else 4, D / 2 - 1 - y); first = False
            for xx in (-W / 2 + 0.5, W / 2 - 0.5): segs.append((cx + xx, cy + y + L / 2, L, 1, math.pi / 2, 'y', xx, y + L / 2))
            y += L
        for (px, py, L, dd, ang, axis, lx, ly) in segs:
            if skip and skip(lx, ly, k): continue
            col = "t_clear" if k in trans_rows else colors[k % len(colors)]
            mbs.setdefault(col, MB()).brick(px, py, z0 + k * BH, int(round(L)), 1, BH, ang, studs=(k == courses - 1))
        for col, mb in mbs.items():
            ob = make(mb, f"{mod}_Wall_C{k}_{col}", col, cname)
            if ob: ASSEMBLY.append((ob, M(mod), k))

def disc_floor(mod, cname, cx, cy, R, z0=0, col="lgrey"):
    mb = MB(); r = int(R)
    for i in range(-r, r, 2):
        for j in range(-r, r, 2):
            if (i + 1) ** 2 + (j + 1) ** 2 <= (R - 1.2) ** 2: mb.brick(cx + i + 1, cy + j + 1, z0, 2, 2, 1, studs=False)
    ob = make(mb, f"{mod}_Floor", col, cname); ASSEMBLY.append((ob, M(mod), -1))
def rect_floor(mod, cname, cx, cy, W, D, z0=0, col="lgrey"):
    mb = MB()
    for i in range(int(-W / 2), int(W / 2), 4):
        for j in range(int(-D / 2), int(D / 2), 4):
            w = min(4, W / 2 - i); d = min(4, D / 2 - j)
            mb.brick(cx + i + w / 2, cy + j + d / 2, z0, int(w), int(d), 1, studs=False)
    ob = make(mb, f"{mod}_Floor", col, cname); ASSEMBLY.append((ob, M(mod), -1))

def stepped_roof(mod, cname, cx, cy, R, z0, steps=5, col="white", sky=True):
    mb = MB(); mbs = MB()
    for s in range(steps):
        rr = R * (1 - s / (steps + 1)); n = max(8, int(round(TAU * rr / 2)))
        for i in range(n):
            a = TAU * (i + 0.5 * (s % 2)) / n
            mb.brick(cx + rr * math.cos(a), cy + rr * math.sin(a), z0 + s, 2, 2, 1, a + math.pi / 2, studs=True)
        rin = rr - 2
        if s == steps - 1 and sky:
            for i in range(int(-rin), int(rin) + 1, 2):
                for j in range(int(-rin), int(rin) + 1, 2):
                    if i * i + j * j <= rin * rin: mbs.brick(cx + i, cy + j, z0 + s, 2, 2, 1, studs=False)
    ob = make(mb, f"{mod}_Roof", col, cname); ASSEMBLY.append((ob, M(mod), 50))
    ob2 = make(mbs, f"{mod}_Roof_Skylight", "t_clear", cname)
    if ob2: ASSEMBLY.append((ob2, M(mod), 51))
    # inner roof fill (so the roof is closed apart from the skylight)
    mbf = MB(); rin0 = R - 1
    for i in range(int(-rin0), int(rin0) + 1, 2):
        for j in range(int(-rin0), int(rin0) + 1, 2):
            d = math.hypot(i, j)
            if d <= rin0 and (not sky or d > R * (1 - (steps - 1) / (steps + 1)) - 2): mbf.brick(cx + i, cy + j, z0, 2, 2, 1, studs=False)
    ob3 = make(mbf, f"{mod}_Roof_Deck", "lgrey", cname); ASSEMBLY.append((ob3, M(mod), 49))

def flat_roof(mod, cname, cx, cy, W, D, z0, col="white", skylights=()):
    mb = MB(); mbs = MB()
    for i in range(int(-W / 2), int(W / 2), 2):
        for j in range(int(-D / 2), int(D / 2), 2):
            sky = any(abs(cx + i + 1 - sx) < sw and abs(cy + j + 1 - sy) < sd for sx, sy, sw, sd in skylights)
            (mbs if sky else mb).brick(cx + i + 1, cy + j + 1, z0, 2, 2, 1, studs=not sky)
    ob = make(mb, f"{mod}_Roof", col, cname); ASSEMBLY.append((ob, M(mod), 50))
    ob2 = make(mbs, f"{mod}_Roof_Skylight", "t_clear", cname)
    if ob2: ASSEMBLY.append((ob2, M(mod), 51))

def dome(mod, cname, cx, cy, R, z0, frame_col="white"):
    N = int(R * P / (BH * PH)) + 1
    for k in range(N):
        zz = k * BH * PH; ratio = min(0.999, zz / (R * P)); rr = R * math.cos(math.asin(ratio))
        if rr < 1.5: break
        n = max(6, int(round(TAU * rr / 2))); mbt = MB(); mbf = MB()
        for i in range(n):
            a = TAU * (i + 0.5 * (k % 2)) / n
            rib = (k % 3 == 0) or (i % max(1, n // 6) == 0)
            (mbf if rib else mbt).brick(cx + rr * math.cos(a), cy + rr * math.sin(a), z0 + k * BH, 2, 1, BH, a + math.pi / 2, studs=False)
        for mb, col, tag in ((mbt, "t_clear", "Glass"), (mbf, frame_col, "Frame")):
            ob = make(mb, f"{mod}_Dome{tag}_C{k}", col, cname)
            if ob: ASSEMBLY.append((ob, M(mod), 10 + k))
    cap = MB(); cap.round(cx, cy, z0 + (k + 1) * BH, max(4, int(2 * rr) + 1), 1, studs=False, seg=24); cap.round(cx, cy, z0 + (k + 1) * BH + 1, 2, 3, True)
    ob = make(cap, f"{mod}_Dome_Cap", frame_col, cname); ASSEMBLY.append((ob, M(mod), 40))

def corridor(name, cname, x0, y0, x1, y1, width=6, courses=4):
    FOOT.append(('s', x0, y0, x1, y1, width))
    dx, dy = x1 - x0, y1 - y0; L = math.hypot(dx, dy); ang = math.atan2(dy, dx); ux, uy = dx / L, dy / L; nx, ny = -uy, ux
    walls = MB(); glass = MB(); roof = MB(); floor = MB()
    nseg = max(1, int(L // 4))
    for k in range(courses):
        for s in range(nseg):
            t = (s + 0.5 + (0.5 if k % 2 else 0)) * L / nseg
            if t > L: continue
            for side in (-1, 1):
                px = x0 + ux * t + nx * side * (width / 2 - 0.5); py = y0 + uy * t + ny * side * (width / 2 - 0.5)
                (glass if k == 2 else walls).brick(px, py, 1 + k * BH, 4, 1, BH, ang, studs=False)
    for s in range(nseg):
        t = (s + 0.5) * L / nseg
        roof.brick(x0 + ux * t, y0 + uy * t, 1 + courses * BH, 4, width, 1, ang, studs=True)
        floor.brick(x0 + ux * t, y0 + uy * t, 0, 4, width - 2, 1, ang, studs=False)
    obs = [make(walls, f"{name}_Walls", "lgrey", cname), make(glass, f"{name}_Windows", "t_clear", cname),
           make(roof, f"{name}_Roof", "white", cname), make(floor, f"{name}_Floor", "dgrey", cname)]
    for ob in obs:
        if ob: CONNECT.append((ob, ux * 18, uy * 18))

def label(text, xs, ys, zp, size=1.6, rot=0.0, cname="Labels", col="black"):
    tile = MB(); w = max(4, int(len(text) * size * 0.62) + 2)
    tile.brick(xs, ys, zp, w, 3, 1, rot, studs=False)
    t = make(tile, f"Label_Tile_{text[:18]}", "white", cname)
    cu = bpy.data.curves.new("LabelText", 'FONT'); cu.body = text; cu.size = size * P; cu.align_x = 'CENTER'; cu.align_y = 'CENTER'; cu.extrude = 0.002
    ob = bpy.data.objects.new(f"Label_{text[:24]}", cu); cu.materials.append(MATS[col]); coll(cname).objects.link(ob)
    ob.location = (xs * P, ys * P, (zp + 1) * PH + 0.001); ob.rotation_euler = (0, 0, rot)
    return t, ob

FOOT = []
def blocked(x, y):
    for f in FOOT:
        if f[0] == 'c' and math.hypot(x - f[1], y - f[2]) < f[3] + 1.5: return True
        if f[0] == 'r' and abs(x - f[1]) < f[3] / 2 + 1.5 and abs(y - f[2]) < f[4] / 2 + 1.5: return True
        if f[0] == 's':
            x0, y0, x1, y1, w = f[1:]; dx, dy = x1 - x0, y1 - y0; L2 = dx * dx + dy * dy
            t = max(0, min(1, ((x - x0) * dx + (y - y0) * dy) / L2)); px, py = x0 + t * dx, y0 + t * dy
            if math.hypot(x - px, y - py) < w / 2 + 1.5: return True
    return False
LAB = []
try:
    log("start")
    # ============================================================ GROUND
    g = coll("00_Lunar_Ground"); gb = MB(); gs = MB()
    for i in range(-96, 96, 32):
        for j in range(-96, 112, 32):
            gb.box((i + 16) * P, (j + 16) * P, -PH, 32 * P - G, 32 * P - G, PH)
            if -96 <= j < 96: gs.studs((i + 16) * P, (j + 16) * P, 0, 32, 32, seg=6)
    make(gb, "Ground_Baseplates", "ground", "00_Lunar_Ground")
    make(gs, "Ground_Studs_RENDER_ONLY", "ground", "00_Lunar_Ground")
    # craters: stacked rings of plates
    cm = MB()
    for (cx, cy, r) in [(-80, -80, 7), (82, -84, 5), (-84, 70, 6), (60, 78, 4), (24, -92, 4), (-48, -88, 5), (88, 40, 5)]:
        for k in range(3):
            rr = r - k * 1.2; n = max(8, int(TAU * rr / 2))
            for i in range(n):
                a = TAU * i / n; cm.brick(cx + rr * math.cos(a), cy + rr * math.sin(a), k, 2, 1, 1, a + math.pi / 2, True)
    make(cm, "Ground_Craters", "regolith", "00_Lunar_Ground")
    # back ridge (hides Earth before it rises)
    rg = MB()
    for i in range(-110, 112, 4):
        hgt = int(6 + 5 * abs(math.sin(i * 0.07)) + 3 * abs(math.sin(i * 0.19)))
        for k in range(hgt): rg.brick(i, 100 + (k % 2), k * 3, 4, 4, 3, 0, studs=(k == hgt - 1))
    make(rg, "Ground_Horizon_Ridge", "regolith", "00_Lunar_Ground")
    log("ground")

    # ============================================================ 01 CENTRAL COMMAND MODULE (core)
    C = "01_Central_Command"; coll(C)
    disc_floor("Command", C, 0, 0, 16); FOOT.append(("c", 0, 0, 16))
    doors = [0, math.pi / 2, math.pi, 3 * math.pi / 2, math.pi / 4, 3 * math.pi / 4]
    def core_skip(a, k):
        return k < 4 and any(abs(((a - d + math.pi) % TAU) - math.pi) < 0.2 for d in doors)
    ring_wall("Command", C, 0, 0, 16, 8, ["white", "white", "lgrey"], skip=core_skip, trans_rows=(5,))
    stepped_roof("Command", C, 0, 0, 16, 1 + 8 * BH, steps=5)
    pm = MB(); sm = MB()
    for i in range(6):
        a = TAU * i / 6 + 0.5; x, y = 11 * math.cos(a), 11 * math.sin(a)
        pm.brick(x, y, 1, 4, 2, 3, a + math.pi / 2, studs=False); sm.brick(x, y, 4, 4, 1, 1, a + math.pi / 2, studs=False)
    pm.round(0, 0, 1, 6, 3, studs=False)
    make(pm, "Command_Consoles", "dgrey", C); make(sm, "Command_Console_Screens", "screen", C)
    holo = MB(); holo.round(0, 0, 4, 4, 2, studs=False); make(holo, "Command_Holotable_Map", "t_lblue", C)
    for o in ("Command_Consoles", "Command_Console_Screens", "Command_Holotable_Map"): ASSEMBLY.append((OBJS[o], M("Command"), 2))
    LAB.append(label("COMMAND", 0, -20, 0))
    log("command")

    # ============================================================ 02 CREW LIVING QUARTERS (N)
    C = "02_Crew_Quarters"; coll(C); cx, cy, W, D = 0, 48, 30, 22
    rect_floor("Crew", C, cx, cy, W, D); FOOT.append(("r", cx, cy, W, D))
    rect_wall("Crew", C, cx, cy, W, D, 6, ["white", "white", "azure"], skip=lambda lx, ly, k: (k < 4 and abs(lx) < 3 and ly < 0), trans_rows=(4,))
    flat_roof("Crew", C, cx, cy, W, D, 1 + 6 * BH, skylights=[(0, 48, 4, 6)])
    bm_ = MB(); bl = MB(); lk = MB()
    for i, (bx, by) in enumerate([(-10, 54), (-4, 54), (4, 54), (10, 54)]):
        bm_.brick(bx, by, 1, 4, 8, 3, 0, False); bl.brick(bx, by - 1, 4, 4, 6, 1, 0, False); lk.brick(bx, 41, 1, 4, 2, 6, 0, False)
    bm_.round(0, 46, 1, 4, 3, False)
    make(bm_, "Crew_Bunks_and_Table", "white", C); make(bl, "Crew_Bunk_Blankets", "blue", C); make(lk, "Crew_Lockers", "lgrey", C)
    for o in ("Crew_Bunks_and_Table", "Crew_Bunk_Blankets", "Crew_Lockers"): ASSEMBLY.append((OBJS[o], M("Crew"), 2))
    LAB.append(label("CREW QUARTERS", 0, 63, 0))
    corridor("Corridor_N", "09_Corridors", 0, 16, 0, 37)

    # ============================================================ 03 MEDICAL BAY (E)
    C = "03_Medical_Bay"; coll(C)
    disc_floor("Medical", C, 46, 0, 11); FOOT.append(("c", 46, 0, 11))
    ring_wall("Medical", C, 46, 0, 11, 7, ["white", "white", "red"], skip=lambda a, k: k < 4 and (abs(((a - math.pi + math.pi) % TAU) - math.pi) < 0.35 or abs(a % TAU) < 0.35), trans_rows=(4,))
    stepped_roof("Medical", C, 46, 0, 11, 1 + 7 * BH, steps=4)
    md = MB(); mr = MB()
    md.brick(46, 2, 1, 8, 4, 3, 0, False); md.brick(46, 2, 4, 8, 4, 1, 0, False)
    for s in (-1, 1): md.brick(46 + 5 * s, 2, 1, 1, 6, 9, 0, False)
    md.brick(46, 2, 10, 11, 6, 1, 0, False); md.brick(46, -6, 1, 10, 2, 6, 0, False)
    mr.brick(46, -6, 7, 2, 1, 1, 0, False); mr.brick(46, -6, 7, 1, 2, 1, 0, False)
    make(md, "Medical_Bed_Scanner_Cabinets", "white", C); make(mr, "Medical_Red_Cross", "red", C)
    for o in ("Medical_Bed_Scanner_Cabinets", "Medical_Red_Cross"): ASSEMBLY.append((OBJS[o], M("Medical"), 2))
    LAB.append(label("MEDICAL BAY", 46, -15, 0))
    corridor("Corridor_E", "09_Corridors", 16, 0, 35, 0)

    # ============================================================ 04 RADIATION STORM SHELTER + shield layers (E, far)
    C = "04_Radiation_Storm_Shelter"; coll(C); sx, sy = 72, 0
    rect_floor("Shelter", C, sx, sy, 12, 12); FOOT.append(("r", sx, sy, 24, 24))
    rect_wall("Shelter", C, sx, sy, 12, 12, 5, ["white", "lgrey"], skip=lambda lx, ly, k: k < 3 and lx < -5 and abs(ly) < 2)
    flat_roof("Shelter", C, sx, sy, 12, 12, 1 + 5 * BH)
    sb = MB(); sb.brick(sx + 2, sy - 3, 1, 6, 3, 3, 0, False); sb.brick(sx + 2, sy + 3, 1, 6, 3, 3, 0, False)
    make(sb, "Shelter_Bunks", "orange", C); ASSEMBLY.append((OBJS["Shelter_Bunks"], M("Shelter"), 2))
    SH = "05_Radiation_Shield_Layers"; coll(SH)
    def shield_box(name, W, D, courses, col, roofcol, z_roof):
        mb = MB()
        for k in range(courses):
            off = 2 if k % 2 else 0
            for (x0, x1, y, ang, horiz) in [(-W / 2, W / 2, -D / 2 + 1, 0, True), (-W / 2, W / 2, D / 2 - 1, 0, True)]:
                x = x0 + off
                while x < x1 - 1e-6:
                    L = min(4, x1 - x)
                    if not (k < 3 and x < -W / 2 + 4 and False): mb.brick(sx + x + L / 2, sy + y, 1 + k * BH, int(L), 2, BH, 0, studs=False)
                    x += L
            y = -D / 2 + 2 + off
            while y < D / 2 - 2 - 1e-6:
                L = min(4, D / 2 - 2 - y)
                for xx in (-W / 2 + 1, W / 2 - 1):
                    if xx < 0 and abs(y + L / 2) < 3 and k < 3: continue   # entrance gap facing the medical bay
                    mb.brick(sx + xx, sy + y + L / 2, 1 + k * BH, int(L), 2, BH, math.pi / 2, studs=False)
                y += L
        for i in range(int(-W / 2), int(W / 2), 2):
            for j in range(int(-D / 2), int(D / 2), 2): mb.brick(sx + i + 1, sy + j + 1, z_roof, 2, 2, 2, 0, studs=True)
        return make(mb, name, col, SH)
    WATER = shield_box("Shield_Water_Tank_Layer", 18, 18, 6, "t_blue", "t_blue", 1 + 6 * BH)
    REGO = shield_box("Shield_Regolith_Layer", 24, 24, 7, "regolith", "regolith", 1 + 7 * BH + 2)
    LAB.append(label("RADIATION STORM SHELTER", 72, -17, 0, size=1.3))
    corridor("Corridor_Shelter", "09_Corridors", 57, 0, 60, 0, width=6, courses=4)
    log("medical+shelter")

    # ============================================================ 06 ENGINEERING CONTROL ROOM (S)
    C = "06_Engineering_Control"; coll(C); ex, ey, EW, ED = 0, -46, 26, 20
    rect_floor("Engineering", C, ex, ey, EW, ED); FOOT.append(("r", ex, ey, EW, ED))
    rect_wall("Engineering", C, ex, ey, EW, ED, 6, ["lgrey", "lgrey", "yellow"], skip=lambda lx, ly, k: k < 4 and abs(lx) < 3, trans_rows=(4,))
    flat_roof("Engineering", C, ex, ey, EW, ED, 1 + 6 * BH, skylights=[(0, -46, 6, 4)])
    en = MB(); es = MB(); bt = MB()
    for i in range(-8, 10, 4): en.brick(i, -38, 1, 4, 2, 4, 0, False); es.brick(i, -38, 5, 4, 1, 1, 0, False)
    for i in range(-10, 12, 2): bt.brick(i, -53, 1, 2, 2, 9, 0, True)
    make(en, "Engineering_Consoles", "dgrey", C); make(es, "Engineering_Screens", "screen", C); make(bt, "Engineering_Battery_Banks", "yellow", C)
    for o in ("Engineering_Consoles", "Engineering_Screens", "Engineering_Battery_Banks"): ASSEMBLY.append((OBJS[o], M("Engineering"), 2))
    LAB.append(label("ENGINEERING CONTROL", -24, -46, 0, size=1.3, rot=math.pi / 2))
    corridor("Corridor_S", "09_Corridors", 0, -16, 0, -36)

    # ============================================================ 07 WATER PROCESSING CENTER (S far) - open roof, visible from above
    C = "07_Water_Processing"; coll(C); wx, wy, WW, WD = 0, -72, 30, 20
    rect_floor("Water", C, wx, wy, WW, WD, col="dgrey"); FOOT.append(("r", wx, wy, WW, WD))
    rect_wall("Water", C, wx, wy, WW, WD, 3, ["lgrey", "azure"])
    wt = MB(); wf = MB(); wp = MB(); wg = MB(); wtop = MB()
    for i, x in enumerate((-10, -4)):
        for k in range(5): wt.round(x, -66, 1 + k * BH, 4, BH, studs=(k == 4))
    for x in (4, 10):
        wf.brick(x, -66, 1, 4, 4, 9, 0, True); wtop.brick(x, -66, 10, 4, 4, 1, 0, False)
    for x in (-10, -4, 4): wp.round(x, -77, 1, 2, 6, True)
    for k in range(4): wg.round(10, -77, 1 + k * BH, 4, BH, studs=(k == 3))
    make(wt, "Water_Storage_Tanks", "t_lblue", C); make(wf, "Water_Filtration_Stations", "white", C)
    make(wtop, "Water_Filtration_Status", "t_green", C); make(wp, "Water_Main_Pumps", "dgrey", C); make(wg, "Water_Greywater_Recovery_Tank", "t_clear", C)
    for o in ("Water_Storage_Tanks", "Water_Filtration_Stations", "Water_Filtration_Status", "Water_Main_Pumps", "Water_Greywater_Recovery_Tank"): ASSEMBLY.append((OBJS[o], M("Water"), 2))
    LAB.append(label("WATER PROCESSING", 0, -86, 0, size=1.4))
    corridor("Corridor_Water", "09_Corridors", 0, -56, 0, -62, width=6, courses=3)
    log("engineering+water")

    # ============================================================ 08 AIRLOCK + ROVER GARAGE (W)
    C = "08_Airlock"; coll(C)
    disc_floor("Airlock", C, -40, 0, 6, col="dgrey"); FOOT.append(("c", -40, 0, 6))
    ring_wall("Airlock", C, -40, 0, 6, 6, ["orange", "white"], skip=lambda a, k: k < 4 and (abs(a % TAU) < 0.5 or abs(((a - math.pi + math.pi) % TAU) - math.pi) < 0.5))
    stepped_roof("Airlock", C, -40, 0, 6, 1 + 6 * BH, steps=3, sky=False)
    su = MB()
    for y in (-3, 3): su.brick(-40, y, 1, 2, 1, 9, 0, True); su.round(-40, y, 10, 2, 3, False)
    make(su, "Airlock_EVA_Suits", "white", C); ASSEMBLY.append((OBJS["Airlock_EVA_Suits"], M("Airlock"), 2))
    LAB.append(label("AIRLOCK", -40, -9, 0))
    corridor("Corridor_W", "09_Corridors", -16, 0, -34, 0)
    C = "10_Rover_Garage"; coll(C); gx, gy, GW, GD = -62, 0, 24, 22
    rect_floor("Garage", C, gx, gy, GW, GD, col="dgrey"); FOOT.append(("r", gx, gy, GW, GD))
    rect_wall("Garage", C, gx, gy, GW, GD, 6, ["lgrey", "lgrey", "orange"], skip=lambda lx, ly, k: (k < 5 and lx < -10 and abs(ly) < 6) or (k < 4 and lx > 10 and abs(ly) < 3))
    flat_roof("Garage", C, gx, gy, GW, GD, 1 + 6 * BH)
    rv = MB(); rw = MB(); rc = MB()
    rv.brick(-62, 0, 3, 10, 6, 1, 0, True); rv.brick(-60, 0, 4, 6, 6, 6, 0, True)
    for (x, y) in [(-66, -3.5), (-66, 3.5), (-58, -3.5), (-58, 3.5)]:
        rw.tube(((x) * P, (y - 0.6) * P, 3 * PH), ((x) * P, (y + 0.6) * P, 3 * PH), 1.4 * P, 16)
    rc.brick(-57, 0, 5, 1, 4, 3, 0, False)
    make(rv, "Garage_Pressurised_Rover", "white", C); make(rw, "Garage_Rover_Wheels", "black", C, smooth=True); make(rc, "Garage_Rover_Window", "t_clear", C)
    for o in ("Garage_Pressurised_Rover", "Garage_Rover_Wheels", "Garage_Rover_Window"): ASSEMBLY.append((OBJS[o], M("Garage"), 2))
    LAB.append(label("ROVER GARAGE", -62, -14, 0))
    corridor("Corridor_Garage", "09_Corridors", -46, 0, -50, 0, width=6, courses=4)
    log("airlock+garage")

    # ============================================================ 11/12 GREENHOUSES (hydroponics left/west, aquaponics right/east)
    GROW = []; PLANTS = []; FISH = []
    for (name, gxp, cname, aqua) in [("Hydroponics", -42, "11_Hydroponics_Greenhouse", False), ("Aquaponics", 42, "12_Aquaponics_Greenhouse", True)]:
        coll(cname); gy0 = 40
        disc_floor(name, cname, gxp, gy0, 14, col="tan"); FOOT.append(("c", gxp, gy0, 14))
        ring_wall(name, cname, gxp, gy0, 14, 2, ["white"], skip=lambda a, k, gx_=gxp: k < 2 and abs(((a - math.atan2(-40, -gx_) + math.pi) % TAU) - math.pi) < 0.3)
        dome(name, cname, gxp, gy0, 14, 1 + 2 * BH)
        beds = MB(); crops = MB(); lights = MB()
        rows = (-6, 0, 6)
        for r_ in rows:
            beds.brick(gxp + r_, gy0 + (2 if aqua else 0), 1 if not aqua else 7, 4, 14, 1, 0, True)
            for j in range(-6, 7, 2):
                crops.round(gxp + r_ - 1, gy0 + (2 if aqua else 0) + j, (2 if not aqua else 8), 1, 3, True); crops.round(gxp + r_ + 1, gy0 + (2 if aqua else 0) + j, (2 if not aqua else 8), 1, 3, True)
            lights.brick(gxp + r_, gy0 + (2 if aqua else 0), 22, 1, 12, 1, 0, False)
        make(beds, f"{name}_Crop_Beds", "brown", cname); P_ = make(crops, f"{name}_Crops", "bgreen", cname); L_ = make(lights, f"{name}_Grow_Lights", "grow", cname)
        for o in (f"{name}_Crop_Beds", f"{name}_Crops", f"{name}_Grow_Lights"): ASSEMBLY.append((OBJS[o], M(name), 2))
        PLANTS.append(P_); GROW.append(L_)
        if aqua:
            tk = MB(); fi = MB(); nf = MB()
            for r_ in rows:
                for k in range(2): tk.brick(gxp + r_, gy0 + 2, 1 + k * BH, 4, 14, BH, 0, studs=False)
                for j in (-4, 0, 4): fi.brick(gxp + r_ + (1 if j else -1), gy0 + 2 + j, 3, 1, 2, 1, 0, False)
            nf.brick(gxp, gy0 - 9, 1, 6, 2, 6, 0, True)
            make(tk, "Aquaponics_Fish_Tanks", "t_blue", cname); FISH.append(make(fi, "Aquaponics_Fish", "orange", cname)); make(nf, "Aquaponics_Nutrient_Biofilter", "white", cname)
            for o in ("Aquaponics_Fish_Tanks", "Aquaponics_Fish", "Aquaponics_Nutrient_Biofilter"): ASSEMBLY.append((OBJS[o], M(name), 2))
        LAB.append(label("HYDROPONICS ONLY" if not aqua else "AQUAPONICS: FISH + PLANTS", gxp, gy0 - 19, 0, size=1.2))
    corridor("Corridor_NW", "09_Corridors", -11.3, 11.3, -32.1, 30.0)
    corridor("Corridor_NE", "09_Corridors", 11.3, 11.3, 32.1, 30.0)
    log("greenhouses")

    # ============================================================ 13 FLUID DYNAMICS CENTER - exposed pipe network
    PC = "13_Fluid_Pipe_Network"; coll(PC)
    ZP = 18   # plates: pipe height (above the corridors)
    PATHS = {
        "Supply_Aquaponics": [(15, -70), (32, -70), (32, 26), (40, 26)],
        "Supply_Hydroponics": [(-15, -70), (-32, -70), (-32, 26), (-40, 26)],
        "Greywater_Return": [(-15, 44), (-26, 44), (-26, -60), (-12, -66)],
        "Shield_Water_Feed": [(32, -40), (72, -40), (72, -13)],
        "Aquaponics_Return": [(52, 27), (58.5, 22), (58.5, -24), (40, -24), (32, -30)],
    }
    pipe = MB(); joints = MB(); posts = MB(); arrows = MB(); press = MB()
    def W3(p): return (p[0] * P, p[1] * P, ZP * PH)
    for pname, pts in PATHS.items():
        for a, b in zip(pts, pts[1:]):
            pipe.tube(W3(a), W3(b), 0.42 * P, 12)
            L = math.hypot(b[0] - a[0], b[1] - a[1]); n = max(1, int(L // 10))
            for s in range(1, n + 1):
                t = s / (n + 1); x = a[0] + (b[0] - a[0]) * t; y = a[1] + (b[1] - a[1]) * t
                if not blocked(x, y):
                    for k in range(ZP // BH): posts.round(x, y, k * BH, 1, BH, studs=False, seg=10)
                press.round(x, y, ZP + 1, 1, 1, studs=False, seg=10)
            ang = math.atan2(b[1] - a[1], b[0] - a[0])
            for s in range(n + 1):
                t = (s + 0.5) / (n + 1); x = a[0] + (b[0] - a[0]) * t; y = a[1] + (b[1] - a[1]) * t
                tri = [(math.cos(ang) * 0.11, math.sin(ang) * 0.11), (math.cos(ang + 2.5) * 0.08, math.sin(ang + 2.5) * 0.08), (math.cos(ang - 2.5) * 0.08, math.sin(ang - 2.5) * 0.08)]
                z0_, z1_ = (ZP + 1.0) * PH, (ZP + 1.6) * PH
                arrows.add([(x * P + u, y * P + v, z0_) for u, v in tri] + [(x * P + u, y * P + v, z1_) for u, v in tri],
                           [[0, 2, 1], [3, 4, 5], [0, 1, 4, 3], [1, 2, 5, 4], [2, 0, 3, 5]])
        for p in pts: joints.cyl(p[0] * P, p[1] * P, ZP * PH - 0.045, 0.09, 0.6 * P, 12)
    PIPE_OB = make(pipe, "Pipes_Transparent_Blue", "t_lblue", PC, smooth=True); make(joints, "Pipe_Connectors", "dgrey", PC, smooth=True)
    make(posts, "Pipe_Support_Posts", "dgrey", PC, smooth=True); make(arrows, "Flow_Direction_Arrows", "yellow", PC); PRESS = make(press, "Pressure_Indicators", "t_green", PC)
    for o in ("Pipes_Transparent_Blue", "Pipe_Connectors", "Pipe_Support_Posts", "Flow_Direction_Arrows", "Pressure_Indicators"): ASSEMBLY.append((OBJS[o], M("Pipes"), 2))
    DROPS = []
    for pname, pts in PATHS.items():
        segs = []; tot = 0
        for a, b in zip(pts, pts[1:]): L = math.hypot(b[0] - a[0], b[1] - a[1]); segs.append((a, b, tot, L)); tot += L
        for d in range(int(tot // 6)):
            mb = MB(); mb.cyl(0, 0, -0.02, 0.04, 0.3 * P, 10); ob = make(mb, f"Water_Droplet_{pname}_{d}", "water", PC, smooth=True)
            ob["path"] = pname; DROPS.append((ob, segs, tot, d * 6.0))
    log("pipes %d droplets" % len(DROPS))

    # ============================================================ RADIATION RAYS
    RC = "14_Radiation_Visualisation"; coll(RC); RAYS = []
    for i in range(6):
        a = TAU * i / 6; top = Vector(((sx + 14 * math.cos(a)) * P, (sy + 14 * math.sin(a)) * P, 2.6))
        tgt = Vector(((sx + 2 * math.cos(a)) * P, (sy + 2 * math.sin(a)) * P, 5 * PH))
        mb = MB(); mb.tube((0, 0, 0), (0, 0, -1), 0.012, 8)
        ob = make(mb, f"Radiation_Ray_{i}", "t_red", RC)
        ob.location = top; ob.rotation_mode = 'QUATERNION'; ob.rotation_quaternion = (tgt - top).to_track_quat('-Z', 'Y')
        parts = []
        for q in range(3):
            pm2 = MB(); pm2.cyl(0, 0, -0.02, 0.04, 0.025, 8); po = make(pm2, f"Radiation_Particle_{i}_{q}", "t_red", RC); parts.append(po)
        RAYS.append((ob, top, tgt, parts))
    TXT = {}
    for key, s in [("none", "NO SHIELD"), ("water", "WATER SHIELD"), ("rego", "REGOLITH SHIELD"), ("combined", "COMBINED SHIELD")]:
        cu = bpy.data.curves.new("ShieldTxt", 'FONT'); cu.body = s; cu.size = 0.22; cu.align_x = 'CENTER'; cu.extrude = 0.015
        ob = bpy.data.objects.new(f"Radiation_State_{key}", cu); cu.materials.append(MATS["t_red"]); coll(RC).objects.link(ob)
        ob.location = (sx * P, sy * P - 0.2, 2.2); ob.rotation_euler = (math.radians(70), 0, 0); TXT[key] = ob
    log("radiation")

    # ============================================================ EARTH (brick voxel globe) + title + lights + world
    EC = "15_Earth_and_Title"; coll(EC)
    eb = MB(); eg = MB(); ew = MB(); R_E = 13
    for i in range(-R_E, R_E + 1):
        for j in range(-R_E, R_E + 1):
            for k in range(-int(R_E / 0.4), int(R_E / 0.4) + 1):
                zz = k * 0.4; d = math.sqrt(i * i + j * j + zz * zz)
                if R_E - 1.2 < d <= R_E:
                    lat = math.asin(max(-1, min(1, zz / R_E))); lon = math.atan2(j, i)
                    n = math.sin(lon * 3 + lat * 2) + math.cos(lon * 2 - lat * 3) * 0.8
                    tgtmb = ew if abs(lat) > 1.15 else (eg if n > 0.7 else eb)
                    tgtmb.brick(i, j, k, 1, 1, 1, 0, studs=False)
    for mb, col in ((eb, "earth_blue"), (eg, "earth_green"), (ew, "earth_white")): make(mb, f"Earth_Bricks_{col}", col, EC)
    EARTH = bpy.data.objects.new("Earth_Rig", None); coll(EC).objects.link(EARTH)
    for col in ("earth_blue", "earth_green", "earth_white"): OBJS[f"Earth_Bricks_{col}"].parent = EARTH
    EARTH.location = (0, 150 * P, -1.6)
    TITLE = []
    for i, (s, size) in enumerate([("LUNAR SMART ADAPTIVE HABITAT", 0.42), ("Built Brick By Brick For Humanity's Future", 0.2)]):
        cu = bpy.data.curves.new("Title", 'FONT'); cu.body = s; cu.size = size; cu.align_x = 'CENTER'; cu.extrude = 0.01
        ob = bpy.data.objects.new(f"Title_Line_{i}", cu); cu.materials.append(MATS["white" if i == 0 else "yellow"]); coll(EC).objects.link(ob)
        ob.location = (0, 128 * P, 4.3 - i * 0.62); ob.rotation_euler = (math.radians(90), 0, 0); TITLE.append(ob)
    sun = bpy.data.lights.new("Sun", 'SUN'); sun.energy = 4.0; sun.angle = math.radians(2); sun.color = (1, 0.97, 0.92)
    so = bpy.data.objects.new("Sun", sun); coll("16_Studio").objects.link(so); so.rotation_euler = (math.radians(52), 0, math.radians(-35))
    fl = bpy.data.lights.new("Fill", 'AREA'); fl.energy = 2500; fl.size = 12; fl.color = (0.7, 0.8, 1.0)
    fo = bpy.data.objects.new("Fill", fl); coll("16_Studio").objects.link(fo); fo.location = (-6, -10, 9); fo.rotation_euler = (math.radians(45), 0, math.radians(-30))
    w = bpy.data.worlds.new("Space"); sc.world = w; w.use_nodes = True; nt = w.node_tree; N = nt.nodes
    bg = N["Background"]; tc = N.new("ShaderNodeTexCoord"); vor = N.new("ShaderNodeTexVoronoi"); vor.inputs["Scale"].default_value = 260
    ramp = N.new("ShaderNodeValToRGB"); ramp.color_ramp.elements[0].position = 0.0; ramp.color_ramp.elements[0].color = (1, 1, 1, 1)
    ramp.color_ramp.elements[1].position = 0.035; ramp.color_ramp.elements[1].color = (0.004, 0.006, 0.014, 1)
    nt.links.new(tc.outputs["Generated"], vor.inputs["Vector"]); nt.links.new(vor.outputs["Distance"], ramp.inputs["Fac"]); nt.links.new(ramp.outputs["Color"], bg.inputs["Color"])
    bg.inputs["Strength"].default_value = 1.0
    log("earth+world")

    # ============================================================ CAMERAS (website showcase)
    TG = {}
    def cam(name, loc, tgt, lens=40):
        cd = bpy.data.cameras.new(name); cd.lens = lens; cd.clip_end = 400
        co = bpy.data.objects.new(name, cd); coll("16_Studio").objects.link(co); co.location = Vector(loc) * P
        t = bpy.data.objects.new(name + "_Target", None); coll("16_Studio").objects.link(t); t.location = Vector(tgt) * P
        c = co.constraints.new('TRACK_TO'); c.target = t; c.track_axis = 'TRACK_NEGATIVE_Z'; c.up_axis = 'UP_Y'; TG[name] = t
        return co
    CAMS = {"hero": cam("Cam_1_Hero_Homepage", (95, -125, 85), (2, -4, 0), 35),
            "rad": cam("Cam_2_Radiation_Closeup", (98, -34, 42), (72, 0, 6), 45),
            "water": cam("Cam_3_Water_Systems", (30, -108, 52), (0, -66, 6), 40),
            "green": cam("Cam_4_Greenhouse_Overview", (0, -8, 105), (0, 42, 0), 38),
            "cross": cam("Cam_5_Cross_Section", (-60, -110, 120), (0, -8, 0), 32),
            "explode": cam("Cam_6_Exploded_View", (120, -150, 150), (0, -4, 10), 30),
            "trailer": cam("Cam_7_Trailer_Cinematic", (0, -150, 30), (0, 0, 10), 32)}
    sc.camera = CAMS["hero"]
    log("cameras")

    # ============================================================ ANIMATION (24 fps, 900 frames)
    sc.render.fps = 24; sc.frame_start = 1; sc.frame_end = 900
    for nm, fr in [("ASSEMBLY", 1), ("MODULES_CONNECT", 260), ("PIPES_FILL", 320), ("GREENHOUSES_ON", 400), ("SHIELDS_DEPLOY", 470),
                   ("RADIATION_NO_SHIELD", 540), ("RADIATION_WATER", 580), ("RADIATION_REGOLITH", 620), ("RADIATION_COMBINED", 660),
                   ("EARTH_RISE", 700), ("TITLE", 800)]:
        sc.timeline_markers.new(nm, frame=fr)
    def K(ob, path, f, idx=-1): ob.keyframe_insert(path, index=idx, frame=f)
    # assembly: modules in order, courses bottom->top drop in
    order = ["Command", "Crew", "Medical", "Shelter", "Engineering", "Water", "Airlock", "Garage", "Hydroponics", "Aquaponics", "Pipes"]
    mod_start = {m: 4 + i * 20 for i, m in enumerate(order)}
    for ob, mi, z in ASSEMBLY:
        if ob is None: continue
        mname = [k for k, v in MOD_INDEX.items() if v == mi][0]
        st = mod_start.get(mname, 200) + max(0, min(z, 60)) * 1.6
        base = ob.location.copy()
        ob.location = base + Vector((0, 0, 3.0)); ob.scale = (0, 0, 0); K(ob, "location", 1); K(ob, "scale", 1)
        K(ob, "location", st); K(ob, "scale", st)
        ob.scale = (1, 1, 1); K(ob, "scale", st + 1)
        ob.location = base; K(ob, "location", st + 14)
    for ob, dx, dy in CONNECT:
        base = ob.location.copy(); ob.location = base + Vector((dx * P, dy * P, 0)); ob.scale = (0, 0, 0)
        K(ob, "location", 1); K(ob, "scale", 1); K(ob, "location", 258); K(ob, "scale", 258)
        ob.scale = (1, 1, 1); K(ob, "scale", 259); ob.location = base; K(ob, "location", 310)
    # water droplets flow along the pipes from frame 320
    def path_pt(segs, tot, s):
        s = s % tot
        for a, b, t0, L in segs:
            if t0 <= s <= t0 + L:
                u = (s - t0) / L; return Vector(((a[0] + (b[0] - a[0]) * u) * P, (a[1] + (b[1] - a[1]) * u) * P, ZP * PH))
        return Vector((segs[-1][1][0] * P, segs[-1][1][1] * P, ZP * PH))
    for ob, segs, tot, off in DROPS:
        ob.scale = (0, 0, 0); K(ob, "scale", 1)
        appear = 320 + int(off * 1.2); K(ob, "scale", appear); ob.scale = (1, 1, 1); K(ob, "scale", appear + 2)
        for f in range(appear, 901, 6):
            ob.location = path_pt(segs, tot, (f - 320) * 0.35 + 0.0 * off) if f >= appear else path_pt(segs, tot, 0)
            ob.location = path_pt(segs, tot, (f - appear) * 0.35); K(ob, "location", f)
        for fc in (ob.animation_data.action.fcurves if hasattr(ob.animation_data.action, "fcurves") else []):
            for kp in fc.keyframe_points: kp.interpolation = 'LINEAR'
    # pressure indicators blink, grow lights + plants + fish
    pb = MATS["t_green"].node_tree.nodes["Principled BSDF"].inputs["Emission Strength"]
    for f in range(1, 901, 6): pb.default_value = 0.2 if f < 320 else (4.0 if (f // 12) % 2 else 1.2); pb.keyframe_insert("default_value", frame=f)
    gr = MATS["grow"].node_tree.nodes["Principled BSDF"].inputs["Emission Strength"]
    for f, v in ((1, 0), (400, 0), (430, 8), (900, 8)): gr.default_value = v; gr.keyframe_insert("default_value", frame=f)
    for pl in PLANTS:
        pl.scale = (1, 1, 0.2); K(pl, "scale", 400, 2); pl.scale = (1, 1, 1); K(pl, "scale", 470, 2)
    # shields: start detached (raised), deploy 470-540, radiation demo 540-700
    for ob in (WATER, REGO): ob["home"] = 0.0
    def shield_z(ob, f):
        lift = 2.6 if ob is REGO else 1.8
        if f < 470: return lift
        if f < 540: return lift * (1 - min(1, (f - 470) / (60 if ob is REGO else 45)))
        if 540 <= f < 580: return lift
        if 580 <= f < 620: return 0 if ob is WATER else lift
        if 620 <= f < 660: return 0 if ob is REGO else lift
        return 0
    for f in list(range(1, 901, 4)) + [469, 470, 539, 540, 579, 580, 619, 620, 659, 660]:
        for ob in (WATER, REGO): ob.location = (0, 0, shield_z(ob, f)); K(ob, "location", f, 2)
    for ob in (WATER, REGO):
        for fc in (ob.animation_data.action.fcurves if hasattr(ob.animation_data.action, "fcurves") else []):
            for kp in fc.keyframe_points: kp.interpolation = 'CONSTANT' if False else 'BEZIER'
    def stop_frac(f):
        if f < 540: return 0.0
        if f < 580: return 1.0      # no shield: reaches crew
        if f < 620: return 0.80     # water absorbs part
        if f < 660: return 0.70     # regolith stops more
        if f < 700: return 0.58     # combined stops it outside
        return 0.0
    for ray, top, tgt, parts in RAYS:
        L = (tgt - top).length
        for f in range(1, 901, 4):
            fr = stop_frac(f); ray.scale = (1, 1, max(0.0001, L * fr)); K(ray, "scale", f)
            for qi, po in enumerate(parts):
                if fr == 0: po.scale = (0, 0, 0); po.location = top
                else:
                    u = ((f * 0.02 + qi / 3.0) % 1.0) * fr; po.location = top + (tgt - top) * u; po.scale = (1, 1, 1)
                K(po, "location", f); K(po, "scale", f)
    for key, (a, b) in {"none": (540, 580), "water": (580, 620), "rego": (620, 660), "combined": (660, 700)}.items():
        ob = TXT[key]
        for f, on in ((1, False), (a, True), (b, False)):
            ob.hide_render = not on; ob.hide_viewport = not on; K(ob, "hide_render", f); K(ob, "hide_viewport", f)
    # earth rise + title
    for f, z in ((1, -1.6), (700, -1.6), (790, 1.9), (900, 2.1)): EARTH.location.z = z; K(EARTH, "location", f, 2)
    for ob in TITLE:
        for f, s_ in ((1, 0), (800, 0), (830, 1), (900, 1)): ob.scale = (s_, s_, s_); K(ob, "scale", f)
    # trailer camera path
    tc_ = CAMS["trailer"]; tt = TG["Cam_7_Trailer_Cinematic"]
    keys = [(1, (0, -150, 95), (0, 0, 0)), (260, (110, -110, 75), (0, 0, 0)), (320, (60, -100, 45), (0, -66, 6)), (400, (-20, -10, 70), (0, 40, 4)),
            (470, (100, -40, 50), (72, 0, 6)), (700, (96, -30, 40), (72, 0, 6)), (720, (0, -140, 14), (0, 40, 12)), (900, (0, -130, 12), (0, 40, 20))]
    for f, loc, tgt in keys:
        tc_.location = Vector(loc) * P; tt.location = Vector(tgt) * P; K(tc_, "location", f); K(tt, "location", f)
    log("animation")

    # ============================================================ render settings + save
    sc.render.engine = 'BLENDER_EEVEE'
    try: sc.eevee.taa_render_samples = 48; sc.eevee.use_raytracing = True
    except Exception: pass
    sc.view_settings.view_transform = 'AgX'
    try: sc.view_settings.look = 'AgX - Medium High Contrast'
    except Exception: pass
    sc.render.resolution_x = 1600; sc.render.resolution_y = 900
    tx = bpy.data.texts.new("README_Brick_Habitat")
    tx.write("Lunar Smart Adaptive Habitat - brick-built engineering prototype (Jaineil R., NASA HUNCH Project ARES).\n"
             "Every collection is one real system. Object names: <Module>_Wall_C<course>, _Roof, _Floor, pipes, shields.\n"
             "Timeline markers: assembly, modules connect, pipes fill, greenhouses on, shields deploy, radiation demo (none/water/regolith/combined), Earth rise, title.\n"
             "Cameras Cam_1..Cam_7 are the website showcase views. Ground_Studs_RENDER_ONLY is left out of the web GLB.\n")
    nb = sum(1 for o in bpy.data.objects if o.type == 'MESH')
    tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in bpy.data.objects if o.type == 'MESH')
    sc.frame_set(900)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND, compress=True)
    log(f"SAVED objects={nb} tris={tris} assembly={len(ASSEMBLY)}")
except Exception:
    log("ERROR " + traceback.format_exc())
