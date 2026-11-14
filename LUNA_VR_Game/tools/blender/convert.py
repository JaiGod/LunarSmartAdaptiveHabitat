"""LUNA VR -> Blender: turn the recorded Godot build (every primitive, label, screen, light) into a compact
scene description for build_luna_vr_blend.py. Coordinates are converted from Godot (Y up, -Z forward)
to Blender (Z up): (x, y, z) -> (x, -z, y). Objects are grouped by the model they belong to and named
after the comment in the game source that explains them, so the Blender outliner reads like the design.
"""
import json, re, math, sys, os, collections

SRC = os.environ.get("LUNA_SRC", os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "scripts"))
DUMP = sys.argv[1] if len(sys.argv) > 1 else "/tmp/luna_dump"
OUT = sys.argv[2] if len(sys.argv) > 2 else "/tmp/luna_blender_data.json"

d = json.load(open(os.path.join(DUMP, "luna_vr_scene.json")))
SRC_LINES = {f: open(os.path.join(SRC, f)).read().split("\n") for f in ["models.gd", "habitat.gd", "game.gd"]}

# ---------------------------------------------------------------------------------------------------
# source helpers
# ---------------------------------------------------------------------------------------------------
def code_and_comment(t):
    """split a GDScript line into code and trailing comment (ignores # inside strings)"""
    q = None
    for i, ch in enumerate(t):
        if q:
            if ch == q: q = None
        elif ch in "\"'": q = ch
        elif ch == "#": return t[:i], t[i + 1:].strip()
    return t, ""

def func_starts(lines):
    out = []
    for i, t in enumerate(lines):
        m = re.match(r"^(?:static )?func (\w+)", t)
        if m: out.append((i + 1, m.group(1)))
    return out

FUNCS = {f: func_starts(L) for f, L in SRC_LINES.items()}

def func_start(file, name):
    for ln, fn in FUNCS[file]:
        if fn == name: return ln
    return 1

def section_comment(file, fname, line):
    """nearest full-line comment above `line` inside the function (the 'what this block is' note)"""
    L = SRC_LINES[file]; start = func_start(file, fname)
    for i in range(line - 2, start - 1, -1):
        s = L[i].strip()
        if s.startswith("#"): return s.lstrip("#").strip()
        if s.startswith("func "): break
    return ""

def trailing_comment(file, line):
    return code_and_comment(SRC_LINES[file][line - 1])[1]

def headers(file):
    """the '# ====' purpose block above each builder"""
    L = SRC_LINES[file]; out = {}
    for ln, fn in FUNCS[file]:
        block = []; i = ln - 2
        while i >= 0 and L[i].startswith("#"):
            block.insert(0, L[i]); i -= 1
        text = [b.lstrip("#").strip() for b in block if not set(b.strip()) <= set("#= ")]
        if not text: continue
        joined = " ".join(text)
        if "NASA need" not in joined: continue
        keys = ["NASA need", "Gameplay", "Interaction", "Why needed"]
        pos = sorted((joined.find(k + ":"), k) for k in keys if joined.find(k + ":") >= 0)
        info = {"title": joined[:pos[0][0]].strip(" ·-") if pos else ""}
        for j, (p, k) in enumerate(pos):
            end = pos[j + 1][0] if j + 1 < len(pos) else len(joined)
            info[k] = joined[p + len(k) + 1:end].strip()
        out[fn] = info
    return out

HEAD = headers("models.gd")

STOP = {"a", "an", "the", "of", "in", "on", "at", "to", "for", "with", "and", "or", "around", "under", "from", "by", "into", "as"}
def short(text, n=46):
    t = text.strip().rstrip(".")
    t = re.sub(r"\s+", " ", t)
    for sep in [": ", " — ", " - ", "; "]:
        if sep in t and len(t.split(sep)[0]) >= 6: t = t.split(sep)[0]; break
    cuts = [t.find(c) for c in [" along the ", " on the ", " in the ", " at the ", " docked at ", " around ", " facing the ", " lining the "]]
    cuts = [i for i in cuts if i >= 8]
    if cuts: t = t[:min(cuts)]
    if " (" in t and len(t) > 34 and len(t.split(" (")[0]) >= 6: t = t.split(" (")[0]
    if len(t) > n:
        t = t[:n].rsplit(" ", 1)[0]
    if t.count("(") > t.count(")"): t = t[:t.rfind("(")]
    t = t.strip(" ,:+")
    words = t.split(" ")
    while len(words) > 1 and words[-1].lower() in STOP: words.pop()
    t = " ".join(words)
    return t[:1].upper() + t[1:]

# ---------------------------------------------------------------------------------------------------
# collections (one per model) with the four questions answered
# ---------------------------------------------------------------------------------------------------
COLL = collections.OrderedDict()
def coll(key, name, info=None, parent=None, hidden=False):
    if key not in COLL:
        COLL[key] = {"key": key, "name": name, "info": info or {}, "parent": parent, "hidden": hidden}
    return key

def hinfo(fn, title=None):
    h = HEAD.get(fn, {})
    return {"title": title or h.get("title", fn), "nasa": h.get("NASA need", ""), "gameplay": h.get("Gameplay", ""),
            "interaction": h.get("Interaction", ""), "why": h.get("Why needed", "")}

POD_NAMES = ["You", "Leo", "Ana", "Sam"]
MODEL_COLL = {
    "airlock": ("10", "Dust Decontamination Airlock"),
    "eva_prep": ("11", "EVA Prep Bay"),
    "pods": ("20", "Crew Pod"),
    "kiosk": ("30", "Mood Check-in Kiosk"),
    "hub": ("31", "Social Hub"),
    "earth_corner": ("32", "Earth Connection Corner"),
    "greenhouse": ("40", "Greenhouse Therapy Racks"),
    "medical": ("50", "Medical Bay · Water-Wall Storm Shelter"),
    "eclss": ("51", "Life Support Core (ECLSS) · Lighting Calibration"),
    "robot_bay": ("52", "Maintenance Robot Bay · Stores"),
    "ops": ("60", "AI Command · Twin Table · Forecast Wall · Console"),
    "wellness_center": ("61", "Wellness Operations Center · Live 3D Twin · Planning"),
    "exercise": ("70", "Exercise Bay"),
    "wellness_dock": ("71", "Wellness Dock"),
    "hygiene": ("80", "Hygiene"),
}
HAB = coll("hab", "LUNA VR Habitat (game models)", {"title": "LUNA VR habitat", "nasa": "", "gameplay": "", "interaction": "", "why": ""})
SHELL = coll("shell", "00 Habitat shell", {
    "title": "Habitat shell modular kit",
    "nasa": "Real surface habitats are assembled from modules: floors, wall panels, hatches, cable trays, handrails, sensors.",
    "gameplay": "Every wall, floor, ceiling, door, cable tray, handrail and sensor in the game.",
    "interaction": "Doors slide open when someone is near; ceiling light panels follow LUNA's lighting simulation (colour temperature and lux).",
    "why": "One kit reused everywhere, the way professional games are built; it also holds the room lights LUNA controls."}, HAB)
S_FLOOR = coll("s_floor", "Floors", None, SHELL)
S_CUT = coll("s_cut", "Walls · cutaway (1.2 m, to see inside)", None, SHELL)
S_FULL = coll("s_full", "Walls · full height (hidden)", None, SHELL, hidden=True)
S_KIT = coll("s_kit", "Handrails · wall kit · signs", None, SHELL)
S_OVER = coll("s_over", "Ceilings · light panels · cable trays (hidden)", None, SHELL, hidden=True)
S_LIGHT = coll("s_light", "Room lights (LUNA lighting state at 08:34)", None, SHELL)

EXT = coll("ext", "90 Exterior", {"title": "Outside the habitat", "nasa": "", "gameplay": "", "interaction": "", "why": ""}, HAB)
EXT_INFO = {
    "terrain": ("Terrain · regolith, rocks, crater rims", {
        "nasa": "Shackleton-rim style polar terrain: the base sits low among crater rims, where sunlight is near-horizontal.",
        "gameplay": "The ground you walk on during EVA; dust from it is what the airlock fights.",
        "interaction": "Walk on it during EVA.",
        "why": "Scale and context: everything outside is built for this ground."}),
    "berms": ("Regolith-bag berms (radiation shielding)", {
        "nasa": "Regolith shielding against galactic cosmic rays and micrometeoroids (bags filled on site, as NASA surface-habitat studies propose).",
        "gameplay": "Why the habitat is safe outside storms; the medical bay's water wall is the extra shelter for solar flares.",
        "interaction": "Seen on EVA.",
        "why": "Radiation is the top long-duration risk after psychology; shielding is not optional."}),
    "roof": ("Regolith roof + greenhouse light-pipe dome (hidden)", {
        "nasa": "Overhead regolith shielding; the dome over the greenhouse brings daylight through a light pipe.",
        "gameplay": "Hidden here so you can see into the rooms. Unhide it to see the habitat as built.",
        "interaction": "None (structure).",
        "why": "Same shielding as the berms, over the top."}),
    "porch": ("Suitport porch · docked EVA suits", {
        "nasa": "Suitports keep dusty suits outside (NASA Z-series / rover suitport concept).",
        "gameplay": "You climb into these suits from the airlock to go on EVA.",
        "interaction": "Enter through the suitport from inside.",
        "why": "The suits never come inside, so neither does most of the dust."}),
    "solar": ("Vertical solar array towers (VSAT)", {
        "nasa": "NASA's Vertical Solar Array Technology for the south pole: tall masts catch the low polar Sun.",
        "gameplay": "Power. Dust storms coat the panels and power drops until they are cleaned (robot or EVA).",
        "interaction": "Clean the panels on EVA, or send the robot.",
        "why": "Power runs life support; dust on the arrays is a real failure mode."}),
    "athlete": ("Six-legged repair robot (after NASA JPL's ATHLETE)", {
        "nasa": "Based on NASA JPL's six-limbed ATHLETE robot; dust mitigation on the solar arrays.",
        "gameplay": "Dust storms coat the arrays and power drops; the robot cleans them.",
        "interaction": "Dispatch it from the robot bay screen, or do the EVA yourself.",
        "why": "Ties power, dust and EVA risk together."}),
    "rover": ("Pressurised rover", {
        "nasa": "Long surface traverses with suitport entry (NASA pressurised rover concepts).",
        "gameplay": "Parked asset (future traverse missions).",
        "interaction": "Seen on EVA.",
        "why": "Shows suitports on a vehicle as well as on the habitat."}),
    "dish": ("Comm dish pointed at Earth", {
        "nasa": "Direct-to-Earth S-band / Ka-band link (Earth is low on the horizon at the pole).",
        "gameplay": "The family messages and the Earth window's live view arrive through it.",
        "interaction": "Seen on EVA.",
        "why": "Contact with home is part of the wellbeing system (the Earth Connection corner)."}),
}
for i, (k, (nm, inf)) in enumerate(EXT_INFO.items()):
    inf = dict(inf); inf["title"] = nm
    coll("ext_" + k, "%02d %s" % (91 + i, nm), inf, EXT, hidden=(k == "roof"))

def model_coll(builder, model):
    num, name = MODEL_COLL[builder]
    if builder == "pods":
        i = int(model.split("_")[-1]) - 1
        key = "pod%d" % i
        return coll(key, "%s %s %d · %s" % (num, name, i + 1, POD_NAMES[i]), hinfo("pods", "Crew Pod %d (%s)" % (i + 1, POD_NAMES[i])), HAB)
    return coll(builder, "%s %s" % (num, name), hinfo(builder, name), HAB)

# ---------------------------------------------------------------------------------------------------
# coordinate conversion
# ---------------------------------------------------------------------------------------------------
def to_blender(xf):
    # Godot basis columns x, y, z and origin -> Blender 3x4 (rows), A = P*B, t = P*o, P=(x,-z,y)
    bx, by, bz, o = xf[0:3], xf[3:6], xf[6:9], xf[9:12]
    B = [[bx[0], by[0], bz[0]], [bx[1], by[1], bz[1]], [bx[2], by[2], bz[2]]]   # rows of the Godot matrix
    A = [B[0], [-B[2][0], -B[2][1], -B[2][2]], B[1]]
    t = [o[0], -o[2], o[1]]
    return [round(v, 5) for v in A[0] + [t[0]] + A[1] + [t[1]] + A[2] + [t[2]]]

def gpos(xf): return (xf[9], xf[10], xf[11])

def room_of(x, z):
    for r, info in d["rooms"].items():
        rx, rz, w, h = info["rect"]
        if rx - 0.05 <= x <= rx + w + 0.05 and rz - 0.05 <= z <= rz + h + 0.05: return r
    return None

# ---------------------------------------------------------------------------------------------------
# materials
# ---------------------------------------------------------------------------------------------------
def s2l(c):
    return [((v + 0.055) / 1.055) ** 2.4 if v > 0.04045 else v / 12.92 for v in c[:3]]

MATS = {}
def mat_entry(k):
    if not k: k = "default"
    if k in MATS: return k
    m = d["mats"].get(k, {"albedo": [0.7, 0.7, 0.7, 1], "rough": 0.6, "metal": 0.0})
    e = {"albedo": [round(v, 4) for v in s2l(m["albedo"])], "alpha": round(m["albedo"][3], 3) if m.get("transp") else 1.0,
         "rough": round(m.get("rough", 0.6), 3), "metal": round(m.get("metal", 0.0), 3),
         "emit": [round(v, 4) for v in s2l(m.get("emit_col", [0, 0, 0, 1]))] if m.get("emit") else None,
         "emit_e": round(m.get("emit_e", 1.0), 3), "unshaded": bool(m.get("unshaded")),
         "tex": m.get("tex_file", ""), "tri": bool(m.get("tri")), "scale": round(m.get("uv1_scale", [1, 1, 1])[0], 4)}
    # glow() materials store the colour in emission; give the viewport the same colour
    if e["emit"] and max(e["albedo"]) < 0.02: e["albedo"] = e["emit"]
    MATS[k] = e
    return k

# readable material names for the Blender material list
def nice_mat(k):
    if k.startswith("mat_-"): return None
    return k

# ---------------------------------------------------------------------------------------------------
# classify every primitive into (collection, object name, purpose)
# ---------------------------------------------------------------------------------------------------
OBJ = collections.OrderedDict()   # (coll, name) -> {"purpose", "prims": []}
def put(ck, name, purpose, prim):
    key = (ck, name)
    if key not in OBJ: OBJ[key] = {"coll": ck, "name": name, "purpose": purpose, "prims": []}
    elif purpose and purpose not in OBJ[key]["purpose"] and len(OBJ[key]["purpose"]) < 400:
        OBJ[key]["purpose"] += " / " + purpose
    OBJ[key]["prims"].append(prim)

ROOM_NAMES = {r: v["name"] for r, v in d["rooms"].items()}
POD_ROOM = {"pod0": "Pod 1 · You", "pod1": "Pod 2 · Leo", "pod2": "Pod 3 · Ana", "pod3": "Pod 4 · Sam"}

def prim_record(p):
    k = p["k"]
    r = {"k": k, "m": to_blender(p["xf"]), "mat": mat_entry(p.get("mat", ""))}
    if k == "box": r["s"] = [round(v, 4) for v in p["s"]]
    elif k == "cyl": r.update(r1=round(p["r"], 4), r2=round(p["rt"], 4), h=round(p["h"], 4), n=p["n"], caps=p.get("caps", [True, True]))
    elif k == "sph": r.update(r=round(p["r"], 4), h=round(p["h"], 4), n=p["n"], rings=p["rings"], hemi=p["hemi"])
    elif k == "cap": r.update(r=round(p["r"], 4), h=round(p["h"], 4))
    elif k in ("quad", "plane"): r.update(s=[round(v, 4) for v in p["s"]], orient=p.get("orient", 2 if k == "quad" else 1))
    elif k == "torus": r.update(ri=round(p["ri"], 4), ro=round(p["ro"], 4), rings=p.get("rings", 24), segs=p.get("segs", 8))
    elif k == "prism": r.update(s=[round(v, 4) for v in p["s"]], ltr=p["ltr"])
    elif k == "tris": r["v"] = p["v"]
    elif k == "phole": r = phole_tris(p, r)
    return r

def phole_tris(p, r):
    rx, rz, w, h = p["rect"]; y = p["y"]; hc = p["hc"]; hr = p["hr"]
    def ray_rect(cx, cz, a):
        dx, dz = math.cos(a), math.sin(a); best = 1e9
        for t in [((rx - cx) / dx) if abs(dx) > 1e-6 else 1e9, ((rx + w - cx) / dx) if abs(dx) > 1e-6 else 1e9,
                  ((rz - cz) / dz) if abs(dz) > 1e-6 else 1e9, ((rz + h - cz) / dz) if abs(dz) > 1e-6 else 1e9]:
            if t > 0:
                px, pz = cx + dx * t, cz + dz * t
                if rx - 0.001 <= px <= rx + w + 0.001 and rz - 0.001 <= pz <= rz + h + 0.001: best = min(best, t)
        return (cx + dx * best, cz + dz * best)
    angs = [2 * math.pi * k / 64 for k in range(64)]
    for cx_, cz_ in [(rx, rz), (rx + w, rz), (rx + w, rz + h), (rx, rz + h)]:
        angs.append(math.atan2(cz_ - hc[1], cx_ - hc[0]) % (2 * math.pi))
    angs.sort(); flat = []
    for k in range(len(angs)):
        a0 = angs[k]; a1 = angs[(k + 1) % len(angs)]
        if a1 < a0: a1 += 2 * math.pi
        i0 = (hc[0] + math.cos(a0) * hr, hc[1] + math.sin(a0) * hr); i1 = (hc[0] + math.cos(a1) * hr, hc[1] + math.sin(a1) * hr)
        o0 = ray_rect(hc[0], hc[1], a0); o1 = ray_rect(hc[0], hc[1], a1)
        for t in ([i0, o0, o1], [i0, o1, i1]):
            pts = t if not p["down"] else [t[0], t[2], t[1]]
            for q in pts: flat += [round(q[0], 4), round(y, 4), round(q[1], 4)]
    r["k"] = "tris"; r["v"] = [flat]
    return r

HELPER_NAMES = {"_chair": "chair", "rack_plants": "plants", "_rack": "rack", "parts_boxes": "parts boxes"}

def classify_model(p):
    st = p["st"]
    builder = None; anchor = None
    for f, fn, ln in st:
        if f == "models.gd" and fn in MODEL_COLL:
            builder, anchor = fn, ln; break
    if builder is None:
        return coll("misc", "99 Other", None, HAB), "Part", ""
    ck = model_coll(builder, p["model"])
    tc = trailing_comment("models.gd", anchor)
    sc = section_comment("models.gd", builder, anchor)
    leaf = st[0][1]
    if tc:
        name = short(tc); purpose = (sc + " — " + tc) if sc else tc
        if " " not in tc.strip() and sc and short(sc, 32).lower() not in name.lower():
            name = short(sc, 32) + " · " + name[:1].lower() + name[1:]
    elif sc:
        name = short(sc); purpose = sc
    else:
        name = HEAD.get(builder, {}).get("title", builder).title() or builder; purpose = ""
        name = MODEL_COLL[builder][1]
    if leaf in HELPER_NAMES and leaf != builder and HELPER_NAMES[leaf] not in name.lower():
        name = name + " · " + HELPER_NAMES[leaf]
    return ck, name, purpose

def classify_shell(p):
    fns = [f[1] for f in p["st"]]
    x, y, z = gpos(p["xf"])
    if p["k"] == "phole": return S_OVER, "Ceiling · Greenhouse (around the light-pipe dome)", "ceiling panel with the hole for the light-pipe dome"
    if fns[0] == "_floors_and_ceilings":
        r = room_of(x, z) or "?"
        nm = POD_ROOM.get(r, ROOM_NAMES.get(r, r))
        if y < 0.5: return S_FLOOR, "Floor · " + nm, "floor panels (grip texture; tint marks the room)"
        return S_OVER, "Ceiling · " + nm, "ceiling panels"
    if fns[0] == "_light_panels":
        return S_OVER, "Ceiling light panels", "tunable LED panels: LUNA sets their colour temperature and brightness"
    if "_door" in fns[:2] or fns[0] == "_hatch_wall":
        return S_FULL, "Door frames · hatch", "door frames (doors slide when someone is near)"
    if fns[0] in ("_wall_run", "_walls_and_doors"):
        return S_FULL, "Walls", "wall panels"
    # kit details: handrails, sensors, cable trays
    anchor = next((ln for f, fn, ln in p["st"] if f == "habitat.gd" and fn == "_kit_details"), None)
    sc = section_comment("habitat.gd", "_kit_details", anchor) if anchor else ""
    tc = trailing_comment("habitat.gd", anchor) if anchor else ""
    nm = short(tc or sc or "Kit")
    if fns[0] == "_cable_tray": nm = "Cable trays"
    if y > 1.95: return S_OVER, nm, (sc + (" — " + tc if tc else "")).strip(" —")
    return S_KIT, nm, (sc + (" — " + tc if tc else "")).strip(" —")

def classify_ext(p):
    fns = [f[1] for f in p["st"]]
    x, y, z = gpos(p["xf"])
    if "_athlete" in fns: return "ext_athlete", "ATHLETE repair robot", "six limbs with wheels on the feet; an arm with an electrostatic dust brush"
    if "_rover" in fns: return "ext_rover", "Pressurised rover", "suitports at the back, rear window, six wheels"
    if "_suit" in fns:
        tc = trailing_comment("habitat.gd", next(ln for f, fn, ln in p["st"] if fn == "_suit"))
        return "ext_porch", "EVA suits (docked)", "two suits docked to the suitports" + (" — " + tc if tc else "")
    ln = next((ln for f, fn, ln in p["st"] if f == "habitat.gd" and fn == "_exterior"), None)
    sc = section_comment("habitat.gd", "_exterior", ln) if ln else ""
    if p["k"] == "prism": return "ext_berms", "Regolith-bag berms", sc
    if "roof and berms" in sc: return "ext_roof", "Regolith-bag roof" if p["k"] == "box" else "Light-pipe dome (greenhouse)", sc
    if "porch" in sc: return "ext_porch", "Suitport porch deck", sc
    if "solar" in sc: return "ext_solar", "Solar towers", sc
    if "dish" in sc: return "ext_dish", "Comm dish", sc
    tc = trailing_comment("habitat.gd", ln) if ln else ""
    if p["k"] == "box" and abs(p["s"][0]) > 100: return "ext_terrain", "Ground (regolith)", "ground just under the habitat floor"
    if p["k"] == "torus": return "ext_terrain", "Small craters (raised rims)", "small craters: raised rim"
    if p["k"] == "sph" and p["r"] > 20: return "ext_terrain", "Far crater rims", "far crater rims"
    return "ext_terrain", "Rocks", "scattered rocks"

CREW = coll("crew", "85 Crew at 08:34 (Leo, Ana, Sam)", {
    "title": "Crew avatars",
    "nasa": "Four-person crew (the brief's maximum); you are the fourth.",
    "gameplay": "LUNA's simulated crewmates: where they are and what they do comes from the simulation (shown here at 08:34 on day 1).",
    "interaction": "Talk to them, eat with them, resolve conflict; LUNA reads their wellbeing the same way it reads yours.",
    "why": "The wellbeing system is about a crew, not one person; they also give every room human scale."}, HAB)
CREW_NAMES = {"crew_1": "Leo", "crew_2": "Ana", "crew_3": "Sam"}

def classify_runtime(p):
    files = [f for f, fn, ln in p["st"]]; fns = [fn for f, fn, ln in p["st"]]
    if "player.gd" in files: return "skip", "", ""
    if "avatar.gd" in files:
        who = next((v for k, v in CREW_NAMES.items() if "/" + k + "/" in p["node"] + "/"), "Crew")
        return CREW, who, "crewmate avatar (suit colour = their pod colour)"
    if "rack_plants" in fns:
        return model_coll("greenhouse", ""), "Plants (growth stage on day 1)", "lettuce, herbs, dwarf tomatoes and strawberries; the engine grows them and counts the food"
    if "parts_boxes" in fns:
        return model_coll("robot_bay", ""), "Spare parts boxes (one per spare)", "the boxes on the shelf are the real spare-part count; repairs use them up"
    return None

def stowed(p):
    """the hub partition lives in a ceiling pocket until Floor plan B: keep it with the (hidden) ceiling parts"""
    if not any(fn == "hub" for f, fn, ln in p["st"]): return False
    if "/partition/" in p.get("node", "") + "/": return True
    return p["k"] == "box" and p["xf"][10] - p["s"][1] / 2 > 2.8      # the ceiling pocket it slides into

for p in d["prims"]:
    if p["model"] != "Shell" and stowed(p):
        put(S_OVER, "Hub partition (stowed in its ceiling pocket)", "retractable partition (Floor plan B): lowers to split the social hub in two when two crew are in conflict", prim_record(p))
        continue
    rt = classify_runtime(p)
    if rt:
        if rt[0] != "skip": put(rt[0], rt[1], rt[2], prim_record(p))
        continue
    if p["model"] == "Shell": ck, name, purpose = classify_shell(p)
    elif p["model"] == "Exterior": ck, name, purpose = classify_ext(p)
    else: ck, name, purpose = classify_model(p)
    put(ck, name, purpose, prim_record(p))

# prim centres per collection (to place labels, screens and loose meshes with the model they belong to)
CENTERS = []
for (ck, name), o in OBJ.items():
    if ck.startswith("s_") or ck.startswith("ext") or ck == "crew": continue
    for pr in o["prims"]:
        m = pr["m"]; CENTERS.append(((m[3], m[7], m[11]), ck, name))

def nearest_coll(bp, maxd=6.0):
    best = None; bd = 1e9
    for c, ck, nm in CENTERS:
        dd = (c[0] - bp[0]) ** 2 + (c[1] - bp[1]) ** 2 + (c[2] - bp[2]) ** 2
        if dd < bd: bd, best = dd, (ck, nm)
    return best if best and bd < maxd * maxd else None

def bpos(xf): return (xf[9], -xf[11], xf[10])

# loose meshes: sliding doors, screen bezels, the live twin, wall displays
SCREEN_COLL = {}
for s in d["screens"]:
    nc = nearest_coll(bpos(s["xf"]))
    SCREEN_COLL[s["id"]] = nc[0] if nc else "misc"
for o in d["others"]:
    own = o["owner"]
    if own.startswith("door_") or own.startswith("@Node3D"):
        nm = "Sliding doors" if o["mat"] == "door" else "Door windows"
        put(S_FULL, nm, "sliding door panels (open when someone is near)", prim_record(o)); continue
    if own.startswith("screen_"):
        sid = own[len("screen_"):]
        put(SCREEN_COLL.get(sid, "misc"), "Screen bezels", "screen frames", prim_record(o)); continue
    nc = nearest_coll(bpos(o["xf"]))
    ck = nc[0] if nc else "misc"
    if "twin3d" in o["path"]:
        nm = "Live 3D twin (1:30) · rooms" if o["k"] == "box" else "Live 3D twin · crew pucks"
        put(ck, nm, "holographic 1:30 twin: rooms glow with their real lighting, crew pucks move with the location beacons", prim_record(o))
    else:
        nm = (nc[1] + " · picture") if nc else "Display picture"
        put(ck, nm, "the picture on this display (a live Earth view / scene in the game)", prim_record(o))

if "misc" in [o["coll"] for o in OBJ.values()]:
    coll("misc", "99 Other", None, HAB)

# ---------------------------------------------------------------------------------------------------
# labels, screens, lights
# ---------------------------------------------------------------------------------------------------
labels = []
for l in d["labels"]:
    if not l["text"].strip(): continue
    bp = bpos(l["xf"])
    if l["owner"] == "Static": ck = S_KIT
    elif l["owner"] in ("Outside",):
        nc = None
        tl = l["text"].lower()
        ck = "ext_dish" if "dish" in tl else "ext_athlete" if "robot" in tl else "ext_rover" if "rover" in tl else "ext_terrain"
    else:
        nc = nearest_coll(bp); ck = nc[0] if nc else "misc"
    col = s2l(l["col"])
    labels.append({"coll": ck, "text": l["text"], "m": to_blender(l["xf"]), "size": round(l["fs"] * l["ps"], 4),
                   "col": [round(v, 4) for v in col], "bb": bool(l["bb"]), "ha": l["ha"], "va": l["va"]})

screens = []
for s in d["screens"]:
    screens.append({"coll": SCREEN_COLL.get(s["id"], "misc"), "id": s["id"], "m": to_blender(s["xf"]), "w": round(s["size"][0], 4),
                    "h": round(s["size"][1], 4), "file": s["file"]})

lights = []
for l in d["lights"]:
    x, y, z = gpos(l["xf"])
    r = room_of(x, z)
    lights.append({"pos": [round(x, 3), round(-z, 3), round(y, 3)], "col": [round(v, 4) for v in s2l(l["col"])], "e": round(l["e"], 3),
                   "range": round(l.get("range", 8.0), 2), "room": POD_ROOM.get(r, ROOM_NAMES.get(r, r or "?"))})

rooms = []
for r, v in d["rooms"].items():
    rx, rz, w, h = v["rect"]
    nm = POD_ROOM.get(r, ROOM_NAMES.get(r, r))
    rooms.append({"id": r, "name": nm, "rect": [rx, -(rz + h), w, h], "h": v["h"]})

# ---------------------------------------------------------------------------------------------------
# write
# ---------------------------------------------------------------------------------------------------
objects = []
used_colls = set()
for (ck, name), o in OBJ.items():
    kinds = collections.Counter(p["k"] for p in o["prims"])
    bevel = 0.0
    if ck not in (S_FLOOR, S_FULL, S_OVER) and not ck.startswith("ext_terrain") and kinds["box"] >= max(1, len(o["prims"]) // 3):
        bevel = 0.006
    if ck == "ext_berms" or ck == "ext_roof": bevel = 0.0
    objects.append({"coll": ck, "name": name, "purpose": o["purpose"], "bevel": bevel,
                    "cut": ck == S_FULL, "prims": o["prims"]})
    used_colls.add(ck)
for l in labels: used_colls.add(l["coll"])
for s in screens: used_colls.add(s["coll"])

# keep only collections in use (+ their parents)
keep = set()
for ck in used_colls | {S_CUT, S_LIGHT}:
    while ck:
        keep.add(ck); ck = COLL[ck]["parent"] if ck in COLL else None
colls = [c for c in COLL.values() if c["key"] in keep]
# order: shell, models by number, exterior
colls.sort(key=lambda c: (0 if c["key"] == "hab" else 1, c["name"]))

out = {"collections": colls, "materials": MATS, "objects": objects, "labels": labels, "screens": screens, "lights": lights,
       "rooms": rooms, "clock": d["clock"], "sun_dir": [-0.62, 0.78, -0.05], "earth_dir": [0.35, 0.93, 0.09]}
json.dump(out, open(OUT, "w"), separators=(",", ":"))
print("wrote", OUT, os.path.getsize(OUT) // 1024, "KB")
print("collections", len(colls), "objects", len(objects), "labels", len(labels), "screens", len(screens), "lights", len(lights), "materials", len(MATS))
if "--list" in sys.argv:
    by = collections.defaultdict(list)
    for o in objects: by[o["coll"]].append("%s (%d)" % (o["name"], len(o["prims"])))
    for c in colls:
        print("\n[%s] %s%s" % (c["key"], c["name"], "  (hidden)" if c["hidden"] else ""))
        if c["info"].get("nasa"): print("   NASA:", c["info"]["nasa"][:110])
        for n in by.get(c["key"], []): print("   -", n)
