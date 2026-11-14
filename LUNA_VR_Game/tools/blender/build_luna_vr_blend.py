"""LUNA VR habitat -> native Blender scene.

Builds every model of the LUNA VR game (Godot 4) as real Blender objects from the recorded build
(luna_vr_blender_data.json), grouped in one collection per model. Each model collection carries the
four answers the project requires (NASA requirement / gameplay purpose / how astronauts interact /
why it is necessary) as custom properties, on a "WHY IT EXISTS" empty, and in the LUNA_VR_README text.

Run (does not touch any open Blender window):
  blender --background --factory-startup --python build_luna_vr_blend.py -- <kit folder> <output .blend>
"""
import bpy, bmesh, json, math, os, sys, traceback
from mathutils import Matrix, Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
KIT = argv[0] if argv else os.path.dirname(os.path.abspath(__file__))
OUT = argv[1] if len(argv) > 1 else os.path.join(KIT, "LUNA_VR_Habitat_Models.blend")
STATUS = os.path.join(KIT, "build_status.txt")
CUT_Z = 1.2          # cutaway wall height (m), so you can look into every room from above


def log(msg):
    print("[LUNA]", msg)
    with open(os.path.join(KIT, "build_log_steps.txt"), "a", encoding="utf-8") as f:
        f.write(msg + "\n")


# ------------------------------------------------------------------------------------------------
# helpers
# ------------------------------------------------------------------------------------------------
def mat4(m):
    return Matrix(((m[0], m[1], m[2], m[3]), (m[4], m[5], m[6], m[7]), (m[8], m[9], m[10], m[11]), (0, 0, 0, 1)))


def find_layer_collection(lc, col):
    if lc.collection == col:
        return lc
    for ch in lc.children:
        r = find_layer_collection(ch, col)
        if r:
            return r
    return None


def set_props(idb, info):
    keys = [("1 NASA requirement", "nasa"), ("2 Gameplay purpose", "gameplay"), ("3 How astronauts interact", "interaction"),
            ("4 Why it's necessary", "why")]
    for label, k in keys:
        if info.get(k):
            idb[label] = info[k]


# ---- primitive generators (local, Godot convention: Y is the axis/up; faces are fixed afterwards) ----
def g_box(s):
    hx, hy, hz = s[0] / 2, s[1] / 2, s[2] / 2
    v = [(-hx, -hy, -hz), (hx, -hy, -hz), (hx, hy, -hz), (-hx, hy, -hz), (-hx, -hy, hz), (hx, -hy, hz), (hx, hy, hz), (-hx, hy, hz)]
    f = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (3, 7, 6, 2), (0, 4, 7, 3), (1, 2, 6, 5)]
    return v, f, True


def ring(r, y, n):
    if r < 1e-6:
        return [(0.0, y, 0.0)]
    return [(math.sin(2 * math.pi * i / n) * r, y, math.cos(2 * math.pi * i / n) * r) for i in range(n)]


def stack_rings(rings, caps_top=True, caps_bottom=True):
    """rings listed top -> bottom; each ring is a list of points (1 point = pole). Returns verts, faces."""
    verts, faces, idx = [], [], []
    for rg in rings:
        idx.append(list(range(len(verts), len(verts) + len(rg))))
        verts += rg
    for a, b in zip(idx[:-1], idx[1:]):          # a = upper, b = lower
        if len(a) == 1 and len(b) == 1:
            continue
        if len(a) == 1:
            n = len(b)
            for i in range(n):
                faces.append((b[i], b[(i + 1) % n], a[0]))
        elif len(b) == 1:
            n = len(a)
            for i in range(n):
                faces.append((b[0], a[(i + 1) % n], a[i]))
        else:
            n = len(a)
            for i in range(n):
                faces.append((b[i], b[(i + 1) % n], a[(i + 1) % n], a[i]))
    if caps_top and len(idx[0]) > 2:
        faces.append(tuple(idx[0]))
    if caps_bottom and len(idx[-1]) > 2:
        faces.append(tuple(reversed(idx[-1])))
    return verts, faces


def g_cyl(r1, r2, h, n, caps):
    n = max(3, int(n))
    v, f = stack_rings([ring(r2, h / 2, n), ring(r1, -h / 2, n)], caps[0], caps[1])
    return v, f, caps[0] and caps[1]


def g_sph(r, h, n, rings, hemi):
    n = max(3, int(n)); R = max(2, int(rings))
    rs = []
    if hemi:
        steps = max(2, (R + 1) // 2)
        for j in range(steps + 1):
            t = (math.pi / 2) * j / steps
            rs.append(ring(r * math.sin(t), h * math.cos(t), n))
        v, f = stack_rings(rs, False, True)
    else:
        for j in range(R + 2):
            t = math.pi * j / (R + 1)
            rs.append(ring(r * math.sin(t), (h / 2) * math.cos(t), n))
        v, f = stack_rings(rs, False, False)
    return v, f, True


def g_cap(r, h):
    n = 12; k = 4; mid = max(0.0, h / 2 - r); rs = []
    for j in range(k + 1):
        t = (math.pi / 2) * j / k
        rs.append(ring(r * math.sin(t), mid + r * math.cos(t), n))
    for j in range(k + 1):
        t = (math.pi / 2) * j / k
        rs.append(ring(r * math.cos(t), -mid - r * math.sin(t), n))
    v, f = stack_rings(rs, False, False)
    return v, f, True


def g_quad(s, orient):
    w, h = s[0] / 2, s[1] / 2
    if orient == 0:      # faces +X
        v = [(0, -h, w), (0, -h, -w), (0, h, -w), (0, h, w)]
    elif orient == 1:    # faces +Y
        v = [(-w, 0, h), (w, 0, h), (w, 0, -h), (-w, 0, -h)]
    else:                # faces +Z (Godot default for QuadMesh)
        v = [(-w, -h, 0), (w, -h, 0), (w, h, 0), (-w, h, 0)]
    return v, [(0, 1, 2, 3)], False


def g_torus(ri, ro, rings, segs):
    lo, hi = min(ri, ro), max(ri, ro)
    rt = (hi - lo) / 2; R = lo + rt
    rings = max(8, int(rings)); segs = max(4, int(segs))
    v = []
    for i in range(rings):
        p = 2 * math.pi * i / rings
        for j in range(segs):
            t = 2 * math.pi * j / segs
            k = R + rt * math.cos(t)
            v.append((k * math.sin(p), rt * math.sin(t), k * math.cos(p)))
    f = []
    for i in range(rings):
        for j in range(segs):
            a = i * segs + j; b = ((i + 1) % rings) * segs + j
            c = ((i + 1) % rings) * segs + (j + 1) % segs; d = i * segs + (j + 1) % segs
            f.append((a, b, c, d))
    return v, f, True


def g_prism(s, ltr):
    x, y, z = s[0] / 2, s[1] / 2, s[2] / 2
    ax = -x + s[0] * ltr
    v = [(-x, -y, -z), (x, -y, -z), (ax, y, -z), (-x, -y, z), (x, -y, z), (ax, y, z)]
    f = [(0, 2, 1), (3, 4, 5), (0, 1, 4, 3), (1, 2, 5, 4), (2, 0, 3, 5)]
    return v, f, True


def prim_geometry(p):
    k = p["k"]
    if k == "box": return g_box(p["s"])
    if k == "cyl": return g_cyl(p["r1"], p["r2"], p["h"], p["n"], p.get("caps", [True, True]))
    if k == "sph": return g_sph(p["r"], p["h"], p["n"], p["rings"], p["hemi"])
    if k == "cap": return g_cap(p["r"], p["h"])
    if k in ("quad", "plane"): return g_quad(p["s"], p.get("orient", 2))
    if k == "torus": return g_torus(p["ri"], p["ro"], p.get("rings", 24), p.get("segs", 8))
    if k == "prism": return g_prism(p["s"], p["ltr"])
    if k == "tris":
        v, f = [], []
        for flat in p["v"]:
            for t in range(0, len(flat) - 8, 9):
                base = len(v)
                v += [(flat[t], flat[t + 1], flat[t + 2]), (flat[t + 3], flat[t + 4], flat[t + 5]), (flat[t + 6], flat[t + 7], flat[t + 8])]
                f.append((base, base + 2, base + 1))          # Godot front faces are clockwise
        return v, f, False
    return [], [], False


# ------------------------------------------------------------------------------------------------
# materials
# ------------------------------------------------------------------------------------------------
IMG = {}
def load_image(name):
    if name in IMG:
        return IMG[name]
    p = os.path.join(KIT, name)
    img = bpy.data.images.load(p, check_existing=True) if os.path.exists(p) else None
    IMG[name] = img
    return img


def image_average(img):
    try:
        import numpy as np
        a = np.empty(len(img.pixels), dtype=np.float32)
        img.pixels.foreach_get(a)
        a = a.reshape(-1, 4)[:, :3].mean(axis=0)
        return [float(((c + 0.055) / 1.055) ** 2.4 if c > 0.04045 else c / 12.92) for c in a]
    except Exception:
        return [0.7, 0.7, 0.7]


def nodes_of(m):
    try:
        m.use_nodes = True
    except Exception:
        pass
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    return nt


MATS = {}
def make_material(key, e, idx):
    nm = ("LUNA " + key) if not key.startswith("mat_-") else ("LUNA live glow %02d" % idx)
    m = bpy.data.materials.new(nm)
    nt = nodes_of(m); N = nt.nodes; L = nt.links
    out = N.new("ShaderNodeOutputMaterial"); out.location = (400, 0)
    base = list(e["albedo"])
    view = list(base)
    tex = load_image(e["tex"]) if e.get("tex") else None
    if e.get("unshaded"):
        em = N.new("ShaderNodeEmission"); em.location = (150, 0)
        em.inputs["Strength"].default_value = 1.0
        em.inputs["Color"].default_value = (*base, 1.0)
        if tex:
            ti = N.new("ShaderNodeTexImage"); ti.image = tex; ti.location = (-300, 0)
            L.new(ti.outputs["Color"], em.inputs["Color"])
            view = [min(1.0, c * 1.2) for c in image_average(tex)]
        L.new(em.outputs["Emission"], out.inputs["Surface"])
    else:
        b = N.new("ShaderNodeBsdfPrincipled"); b.location = (100, 0)
        b.inputs["Base Color"].default_value = (*base, 1.0)
        b.inputs["Roughness"].default_value = e.get("rough", 0.6)
        b.inputs["Metallic"].default_value = e.get("metal", 0.0)
        if tex and e.get("tri"):
            geo = N.new("ShaderNodeNewGeometry"); geo.location = (-800, 0)
            sc = N.new("ShaderNodeVectorMath"); sc.operation = "SCALE"; sc.location = (-600, 0)
            sc.inputs["Scale"].default_value = e.get("scale", 1.0)
            ti = N.new("ShaderNodeTexImage"); ti.image = tex; ti.location = (-400, 0)
            ti.projection = "BOX"; ti.projection_blend = 0.25
            mul = N.new("ShaderNodeVectorMath"); mul.operation = "MULTIPLY"; mul.location = (-150, 0)
            mul.inputs[1].default_value = base
            L.new(geo.outputs["Position"], sc.inputs[0])
            L.new(sc.outputs["Vector"], ti.inputs["Vector"])
            L.new(ti.outputs["Color"], mul.inputs[0])
            L.new(mul.outputs["Vector"], b.inputs["Base Color"])
            avg = image_average(tex)
            view = [base[i] * avg[i] for i in range(3)]
        if e.get("emit"):
            b.inputs["Emission Color"].default_value = (*e["emit"], 1.0)
            b.inputs["Emission Strength"].default_value = max(0.5, e.get("emit_e", 1.0) * 3.0)
            if max(base) < 0.05:
                view = list(e["emit"])
        if e.get("alpha", 1.0) < 0.999:
            b.inputs["Alpha"].default_value = e["alpha"]
            for attr, val in (("surface_render_method", "DITHERED"), ("blend_method", "BLEND")):
                try:
                    setattr(m, attr, val); break
                except Exception:
                    pass
        L.new(b.outputs["BSDF"], out.inputs["Surface"])
    m.diffuse_color = (*[max(0.0, min(1.0, c)) for c in view], e.get("alpha", 1.0) if e.get("alpha", 1.0) < 0.999 else 1.0)
    m.roughness = e.get("rough", 0.6)
    m.metallic = e.get("metal", 0.0)
    return m


def get_mat(key):
    return MATS[key]


# ------------------------------------------------------------------------------------------------
# build
# ------------------------------------------------------------------------------------------------
def main():
    open(os.path.join(KIT, "build_log_steps.txt"), "w").close()
    D = json.load(open(os.path.join(KIT, "luna_vr_blender_data.json"), encoding="utf-8"))
    log("data: %d objects, %d labels, %d screens" % (len(D["objects"]), len(D["labels"]), len(D["screens"])))

    for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
    for c in list(bpy.data.collections): bpy.data.collections.remove(c)
    for blk in (bpy.data.meshes, bpy.data.materials, bpy.data.lights, bpy.data.cameras, bpy.data.curves):
        for x in list(blk): blk.remove(x)

    sc = bpy.context.scene
    sc.name = "LUNA VR Habitat"
    sc.unit_settings.system = "METRIC"

    # ---- world: lunar night sky, a little ambient so the rooms read in Rendered view ----
    w = bpy.data.worlds.new("Lunar south pole sky")
    sc.world = w
    try: w.use_nodes = True
    except Exception: pass
    bg = w.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.010, 0.013, 0.024, 1.0)
        bg.inputs[1].default_value = 1.0

    # ---- materials ----
    for i, (k, e) in enumerate(D["materials"].items()):
        MATS[k] = make_material(k, e, i)
    log("materials %d" % len(MATS))

    # ---- collections (one per model) ----
    COL = {}
    order = {}
    for c in D["collections"]:
        col = bpy.data.collections.new(c["name"])
        COL[c["key"]] = col
        set_props(col, c.get("info", {}))
    def prio(c):
        k = c["key"]
        if k == "hab": return (0, "")
        if k == "shell": return (1, "")
        if k.startswith("s_"):
            return (2, ["s_floor", "s_cut", "s_kit", "s_light", "s_over", "s_full"].index(k) if k in ["s_floor", "s_cut", "s_kit", "s_light", "s_over", "s_full"] else 9)
        return (3, c["name"])
    for c in sorted(D["collections"], key=prio):
        parent = COL[c["parent"]] if c.get("parent") else sc.collection
        parent.children.link(COL[c["key"]])

    # ---- objects ----
    used = set()
    def unique(name, ck):
        if name not in used:
            used.add(name); return name
        title = D_COLL[ck]["info"].get("title") or D_COLL[ck]["name"]
        short_t = title.split(" · ")[0].split(" (")[0]
        nm = "%s · %s" % (short_t, name)
        i = 2
        while nm in used:
            nm = "%s · %s %d" % (short_t, name, i); i += 1
        used.add(nm); return nm

    D_COLL = {c["key"]: c for c in D["collections"]}
    made = {}
    for ob in D["objects"]:
        bm = bmesh.new()
        uv = bm.loops.layers.uv.new("UVMap")
        slots = []; closed_faces = []
        for p in ob["prims"]:
            v, f, closed = prim_geometry(p)
            if not v: continue
            M = mat4(p["m"])
            flip = M.to_3x3().determinant() < 0
            bv = [bm.verts.new(M @ Vector(x)) for x in v]
            if p["mat"] not in slots: slots.append(p["mat"])
            mi = slots.index(p["mat"])
            for fi in f:
                if len(set(fi)) < 3: continue
                try:
                    face = bm.faces.new([bv[i] for i in fi])
                except ValueError:
                    continue
                face.material_index = mi
                face.smooth = True
                if closed:
                    closed_faces.append(face)
                elif flip:
                    face.normal_flip()
                if p["k"] in ("quad", "plane") and len(fi) == 4:
                    for loop, co in zip(face.loops, [(0, 0), (1, 0), (1, 1), (0, 1)]):
                        loop[uv].uv = co
        if closed_faces:
            bmesh.ops.recalc_face_normals(bm, faces=closed_faces)
        me = bpy.data.meshes.new(ob["name"])
        bm.to_mesh(me); bm.free()
        for k in slots:
            me.materials.append(get_mat(k))
        try:
            me.set_sharp_from_angle(angle=math.radians(40))
        except Exception:
            try: me.shade_smooth()
            except Exception: pass
        # origin at the middle of the part
        if me.vertices:
            lo = Vector((min(x.co.x for x in me.vertices), min(x.co.y for x in me.vertices), min(x.co.z for x in me.vertices)))
            hi = Vector((max(x.co.x for x in me.vertices), max(x.co.y for x in me.vertices), max(x.co.z for x in me.vertices)))
            c = (lo + hi) / 2
            if ob["coll"] != "ext_terrain":
                c.z = lo.z
            me.transform(Matrix.Translation(-c))
        else:
            c = Vector((0, 0, 0))
        o = bpy.data.objects.new(unique(ob["name"], ob["coll"]), me)
        o.location = c
        COL[ob["coll"]].objects.link(o)
        if ob.get("purpose"): o["purpose"] = ob["purpose"]
        o["model"] = D_COLL[ob["coll"]]["info"].get("title") or D_COLL[ob["coll"]]["name"]
        if ob.get("bevel", 0) > 0:
            bv_ = o.modifiers.new("Bevel", "BEVEL")
            bv_.width = ob["bevel"]; bv_.segments = 2
            bv_.limit_method = "ANGLE"; bv_.angle_limit = math.radians(30)
            try: bv_.use_clamp_overlap = True
            except Exception: pass
        made[(ob["coll"], ob["name"])] = o
        # cutaway copy of walls, door frames and doors
        if ob.get("cut"):
            bm = bmesh.new(); bm.from_mesh(me)
            bm.transform(Matrix.Translation(c))
            res = bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=(0, 0, CUT_Z),
                                         plane_no=(0, 0, 1), clear_outer=True)
            edges = [e for e in res["geom_cut"] if isinstance(e, bmesh.types.BMEdge)]
            if edges:
                try: bmesh.ops.holes_fill(bm, edges=edges, sides=0)
                except Exception: pass
            bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
            for fc in bm.faces: fc.smooth = False
            me2 = bpy.data.meshes.new(ob["name"] + " (cutaway)")
            bm.to_mesh(me2); bm.free()
            for k in slots: me2.materials.append(get_mat(k))
            o2 = bpy.data.objects.new(unique(ob["name"] + " · cutaway", "s_cut"), me2)
            COL["s_cut"].objects.link(o2)
            o2["purpose"] = "the same %s, cut at %.1f m so you can see into the rooms (full height: unhide 'Walls · full height')" % (ob["name"].lower(), CUT_Z)
    log("objects %d" % len(made))

    # ---- labels (the game's signs, in the game's font) ----
    font = None
    fp = os.path.join(KIT, "DejaVuSans.ttf")
    if os.path.exists(fp):
        font = bpy.data.fonts.load(fp, check_existing=True)
    inks = {}
    cam_target = Vector((18.0, -11.5, 0.0)); cam_pos = Vector((40.0, -44.0, 34.0))
    for lb in D["labels"]:
        txt = lb["text"]
        cu = bpy.data.curves.new("Label", "FONT")
        cu.body = txt
        cu.size = max(0.02, lb["size"]) * 1.15
        cu.align_x = {0: "LEFT", 1: "CENTER", 2: "RIGHT", 3: "JUSTIFY"}.get(lb.get("ha", 1), "CENTER")
        cu.align_y = {0: "TOP", 1: "CENTER", 2: "BOTTOM"}.get(lb.get("va", 1), "CENTER")
        if font: cu.font = font
        key = tuple(round(x, 2) for x in lb["col"])
        if key not in inks:
            m = bpy.data.materials.new("LUNA sign ink %d" % len(inks))
            nt = nodes_of(m)
            em = nt.nodes.new("ShaderNodeEmission"); em.inputs["Color"].default_value = (*lb["col"], 1.0); em.inputs["Strength"].default_value = 1.0
            out = nt.nodes.new("ShaderNodeOutputMaterial"); out.location = (300, 0)
            nt.links.new(em.outputs["Emission"], out.inputs["Surface"])
            m.diffuse_color = (*lb["col"], 1.0)
            inks[key] = m
        cu.materials.append(inks[key])
        first = txt.split("\n")[0][:40]
        o = bpy.data.objects.new(unique("Sign · " + first, lb["coll"]), cu)
        if lb.get("bb"):
            pos = mat4(lb["m"]).to_translation()
            d = cam_pos - pos; d.z = 0
            yaw = math.atan2(d.y, d.x) + math.pi / 2
            o.matrix_world = Matrix.Translation(pos) @ Matrix.Rotation(yaw, 4, "Z") @ Matrix.Rotation(math.pi / 2, 4, "X")
        else:
            o.matrix_world = mat4(lb["m"])
        o["text"] = txt
        COL[lb["coll"]].objects.link(o)
    log("labels %d" % len(D["labels"]))

    # ---- screens: the real LUNA interfaces, as the game draws them ----
    for s in D["screens"]:
        img = load_image(s["file"])
        me = bpy.data.meshes.new("Screen " + s["id"])
        w, h = s["w"] / 2, s["h"] / 2
        me.from_pydata([(-w, -h, 0), (w, -h, 0), (w, h, 0), (-w, h, 0)], [], [(0, 1, 2, 3)])
        uvl = me.uv_layers.new(name="UVMap")
        for i, co in enumerate([(0, 0), (1, 0), (1, 1), (0, 1)]):
            uvl.data[i].uv = co
        m = bpy.data.materials.new("LUNA screen · " + s["id"])
        nt = nodes_of(m)
        em = nt.nodes.new("ShaderNodeEmission"); em.inputs["Strength"].default_value = 1.0
        out = nt.nodes.new("ShaderNodeOutputMaterial"); out.location = (300, 0)
        if img:
            ti = nt.nodes.new("ShaderNodeTexImage"); ti.image = img; ti.location = (-300, 0)
            nt.links.new(ti.outputs["Color"], em.inputs["Color"])
        nt.links.new(em.outputs["Emission"], out.inputs["Surface"])
        m.diffuse_color = (0.04, 0.07, 0.16, 1.0)
        me.materials.append(m)
        o = bpy.data.objects.new(unique("Screen · " + s["id"], s["coll"]), me)
        o.matrix_world = mat4(s["m"])
        o["purpose"] = "live LUNA interface (captured from the game at %s)" % D.get("clock", "")
        COL[s["coll"]].objects.link(o)

    # ---- room lights in LUNA's state at the capture time ----
    for l in D["lights"]:
        ld = bpy.data.lights.new("Room light · " + l["room"], "POINT")
        ld.energy = 140.0 * l["e"]
        ld.color = l["col"]
        ld.shadow_soft_size = 0.25
        try:
            ld.use_custom_distance = True; ld.cutoff_distance = l["range"]
        except Exception:
            pass
        o = bpy.data.objects.new(unique("Room light · " + l["room"], "s_light"), ld)
        o.location = l["pos"]
        COL["s_light"].objects.link(o)

    # ---- the low polar Sun ----
    sd = bpy.data.lights.new("Sun (3° above the horizon)", "SUN")
    sd.energy = 3.0; sd.angle = math.radians(0.53)
    so = bpy.data.objects.new("Sun · low polar Sun", sd)
    so.rotation_euler = Vector(D["sun_dir"]).to_track_quat("-Z", "Y").to_euler()
    COL["ext"].objects.link(so)

    # ---- WHY IT EXISTS markers + README ----
    readme = ["LUNA VR HABITAT · BLENDER MODELS", "=" * 33, "",
              "Every model in the LUNA VR game (Godot 4), built here as real Blender objects at real scale (metres).",
              "Each collection is one model. Click its 'WHY IT EXISTS' empty and look at Object Properties >",
              "Custom Properties (or the N panel > Item) for the four answers: NASA requirement, gameplay purpose,",
              "how astronauts interact, and why it is necessary. Every part also has a 'purpose' property.",
              "", "Viewing tips:",
              "  Z > Material Preview  shows textures, glowing LEDs and the live LUNA screens.",
              "  Hidden collections (eye icon): full-height walls, ceilings/light panels, and the regolith roof.",
              "  Cameras: select one in the Outliner, then Ctrl+Numpad 0 to look through it.",
              "  Screens and lights show LUNA's state at %s on day 1 of the mission." % D.get("clock", ""), "",
              "Source: github.com/JaiGod/LunarSmartAdaptiveHabitat (LUNA_VR_Game). Built from the game's own",
              "model code by build_luna_vr_blend.py, so the Blender scene and the game always match.", ""]
    for c in sorted(D["collections"], key=prio):
        info = c.get("info", {})
        if not info.get("nasa"): continue
        col = COL[c["key"]]
        objs = [o for o in col.all_objects if o.type in ("MESH",)]
        if objs:
            cen = sum((o.matrix_world.translation for o in objs), Vector()) / len(objs)
            top = max(o.matrix_world.translation.z for o in objs)
        else:
            cen, top = Vector(), 0.0
        e = bpy.data.objects.new("WHY IT EXISTS · " + (info.get("title") or c["name"]), None)
        e.empty_display_type = "SPHERE"; e.empty_display_size = 0.12
        e.location = (cen.x, cen.y, max(2.9, top + 0.6) if not c["key"].startswith("ext") else top + 3.0)
        set_props(e, info)
        col.objects.link(e)
        readme += [c["name"].upper(), "  NASA requirement : " + info.get("nasa", ""), "  Gameplay purpose : " + info.get("gameplay", ""),
                   "  How they interact: " + info.get("interaction", ""), "  Why it's necessary: " + info.get("why", ""), ""]
    t = bpy.data.texts.new("LUNA_VR_README")
    t.write("\n".join(readme))

    # ---- cameras ----
    def add_cam(name, pos, target, lens=30):
        cd = bpy.data.cameras.new(name); cd.lens = lens; cd.clip_end = 3000
        o = bpy.data.objects.new(name, cd)
        o.location = pos
        o.rotation_euler = (Vector(target) - Vector(pos)).to_track_quat("-Z", "Y").to_euler()
        COL["hab"].objects.link(o)
        return o
    main_cam = add_cam("Camera · whole habitat (cutaway)", cam_pos, cam_target, 30)
    for r in D["rooms"]:
        if r["id"] in ("corridor", "qhall") or r["id"].startswith("pod"): continue
        x, y, wdt, hgt = r["rect"]
        cx, cy = x + wdt / 2, y + hgt / 2
        # high over the room's own floor, so the regolith berms never block the view
        add_cam("Camera · " + r["name"], (cx + wdt * 0.15, cy - hgt * 0.42, 7.2), (cx, cy + hgt * 0.08, 0.4), 22)
    add_cam("Camera · Crew pods", (13.75, -10.0, 7.5), (13.75, -4.2, 0.4), 22)
    sc.camera = main_cam

    # ---- render + viewport ----
    for eng in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
        try:
            sc.render.engine = eng; break
        except Exception:
            pass
    sc.render.resolution_x = 1920; sc.render.resolution_y = 1080
    for coll_key in [c["key"] for c in D["collections"] if c.get("hidden")]:
        lc = find_layer_collection(bpy.context.view_layer.layer_collection, COL[coll_key])
        if lc: lc.hide_viewport = True
        COL[coll_key].hide_render = True
    rot = (cam_target - cam_pos).to_track_quat("-Z", "Y")
    for scr in bpy.data.screens:
        for area in scr.areas:
            if area.type != "VIEW_3D": continue
            sp = area.spaces.active
            try:
                sp.clip_start = 0.05; sp.clip_end = 5000
                sp.shading.type = "SOLID"; sp.shading.color_type = "MATERIAL"
                sp.shading.show_cavity = True
                r3 = sp.region_3d
                r3.view_location = cam_target; r3.view_rotation = rot; r3.view_distance = (cam_pos - cam_target).length
                r3.view_perspective = "PERSP"
            except Exception as ex:
                log("viewport: %s" % ex)

    # ---- keep everything inside the .blend ----
    try:
        bpy.ops.file.pack_all()
    except Exception as ex:
        log("pack_all: %s" % ex)
        for img in bpy.data.images:
            try: img.pack()
            except Exception: pass
        if font:
            try: font.pack()
            except Exception: pass
    bpy.ops.wm.save_as_mainfile(filepath=OUT, compress=True)
    n_obj = len(bpy.data.objects)
    log("saved %s (%d objects)" % (OUT, n_obj))
    return n_obj


try:
    n = main()
    open(STATUS, "w", encoding="utf-8").write("ok %d" % n)
except Exception:
    open(STATUS, "w", encoding="utf-8").write("ERROR\n" + traceback.format_exc())
    raise
