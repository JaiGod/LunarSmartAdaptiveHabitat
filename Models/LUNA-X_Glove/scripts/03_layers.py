"""LUNA-X glove - exploded layer view scene (inside the glove's OWN file).

Sensor liner (IVA mode) -> pressure bladder -> restraint -> insulation -> outer layer + hardware.
Usage: blender --background --factory-startup <glove.blend> --python 03_layers.py -- <glove_dir> [render]
"""
import bpy, bmesh, sys, os, math
import numpy as np
from mathutils import Vector, Matrix

ARGS = sys.argv[sys.argv.index("--") + 1:]
GLOVE_DIR = ARGS[0]
DO_RENDER = "render" in ARGS
TARGET = os.path.join(GLOVE_DIR, "LUNA-X_Wellness_Glove.blend")
assert os.path.normcase(bpy.data.filepath) == os.path.normcase(TARGET), "refusing to touch any other file"
sys.path.insert(0, os.path.join(GLOVE_DIR, "scripts"))
import lx_sdf as S

MAIN = bpy.data.scenes["LUNA-X Glove"]
MAIN.frame_set(1)                       # frame 1 = rest pose
dg = bpy.context.evaluated_depsgraph_get()
MAT = bpy.data.materials


def log(m):
    print("[LX]", m, flush=True)


if "LUNA-X Layers" in bpy.data.scenes:
    bpy.data.scenes.remove(bpy.data.scenes["LUNA-X Layers"])
SC = bpy.data.scenes.new("LUNA-X Layers")
SC.world = MAIN.world
SC.render.engine = MAIN.render.engine
SC.render.fps = 30
SC.frame_start, SC.frame_end = 1, 300
SC.render.resolution_x, SC.render.resolution_y = 1920, 1080
SC.view_settings.view_transform = MAIN.view_settings.view_transform
SC.view_settings.look = MAIN.view_settings.look
for attr in ("use_raytracing", "taa_render_samples"):
    if hasattr(SC.eevee, attr):
        setattr(SC.eevee, attr, getattr(MAIN.eevee, attr))
ROOT = bpy.data.collections.new("Exploded_Layers")
SC.collection.children.link(ROOT)


def coll(name):
    c = bpy.data.collections.new(name)
    ROOT.children.link(c)
    return c


# ----------------------------------------------------------------------------
# materials for inner layers
# ----------------------------------------------------------------------------
def new_mat(name):
    m = MAT.new(name)
    if m.node_tree is None:
        m.use_nodes = True
    return m


def bsdf(m):
    return next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')


def setp(b, d):
    for k, v in d.items():
        s = b.inputs.get(k)
        if s is not None:
            s.default_value = (*v, 1.0) if isinstance(v, tuple) and len(v) == 3 else v


def knit():
    m = new_mat("LX_Liner_Knit")
    nt = m.node_tree; b = bsdf(m)
    setp(b, {"Base Color": (0.16, 0.18, 0.22), "Roughness": 0.85, "Sheen Weight": 0.8})
    w = nt.nodes.new("ShaderNodeTexWave"); w.wave_type = 'BANDS'; w.bands_direction = 'Y'
    w.inputs["Scale"].default_value = 1400.0
    tc = nt.nodes.new("ShaderNodeTexCoord"); nt.links.new(tc.outputs["Object"], w.inputs["Vector"])
    bp = nt.nodes.new("ShaderNodeBump"); bp.inputs["Strength"].default_value = 0.35
    nt.links.new(w.outputs["Fac"], bp.inputs["Height"]); nt.links.new(bp.outputs["Normal"], b.inputs["Normal"])
    return m


def bladder():
    m = new_mat("LX_Bladder_Urethane")
    b = bsdf(m)
    setp(b, {"Base Color": (0.95, 0.55, 0.12), "Roughness": 0.25, "Alpha": 0.62, "Coat Weight": 0.6})
    if hasattr(m, "surface_render_method"):
        m.surface_render_method = 'BLENDED'
    return m


def restraint():
    m = new_mat("LX_Restraint_Dacron")
    nt = m.node_tree; b = bsdf(m)
    setp(b, {"Base Color": (0.80, 0.76, 0.64), "Roughness": 0.8})
    tc = nt.nodes.new("ShaderNodeTexCoord")
    ck = nt.nodes.new("ShaderNodeTexChecker"); ck.inputs["Scale"].default_value = 500.0
    nt.links.new(tc.outputs["Object"], ck.inputs["Vector"])
    bp = nt.nodes.new("ShaderNodeBump"); bp.inputs["Strength"].default_value = 0.5
    nt.links.new(ck.outputs["Fac"], bp.inputs["Height"]); nt.links.new(bp.outputs["Normal"], b.inputs["Normal"])
    return m


def mylar():
    m = new_mat("LX_Insulation_Al_Mylar")
    nt = m.node_tree; b = bsdf(m)
    setp(b, {"Base Color": (0.85, 0.85, 0.88), "Metallic": 1.0, "Roughness": 0.18})
    tc = nt.nodes.new("ShaderNodeTexCoord")
    nz = nt.nodes.new("ShaderNodeTexVoronoi"); nz.inputs["Scale"].default_value = 160.0
    nt.links.new(tc.outputs["Object"], nz.inputs["Vector"])
    bp = nt.nodes.new("ShaderNodeBump"); bp.inputs["Strength"].default_value = 0.6
    nt.links.new(nz.outputs["Distance"], bp.inputs["Height"]); nt.links.new(bp.outputs["Normal"], b.inputs["Normal"])
    return m


def coolant():
    m = new_mat("LX_Coolant_Tube")
    b = bsdf(m)
    setp(b, {"Base Color": (0.05, 0.35, 1.0), "Roughness": 0.15, "Coat Weight": 1.0,
             "Emission Color": (0.0, 0.35, 1.0), "Emission Strength": 1.5})
    return m


def label_mat():
    m = new_mat("LX_Label_White")
    setp(bsdf(m), {"Base Color": (0.9, 0.92, 0.95), "Emission Color": (0.85, 0.9, 1.0), "Emission Strength": 2.5})
    return m


def accent_mat():
    m = new_mat("LX_Label_Accent")
    setp(bsdf(m), {"Base Color": (0.0, 0.6, 1.0), "Emission Color": (0.0, 0.7, 1.0), "Emission Strength": 4.0})
    return m


MK = {"knit": knit(), "bladder": bladder(), "restraint": restraint(), "mylar": mylar(), "cool": coolant(),
      "label": label_mat(), "accent": accent_mat()}

# ----------------------------------------------------------------------------
# layer shells from the same shape math, offset inward
# ----------------------------------------------------------------------------
body = bpy.data.objects["LX_Glove_Body"]
src = body.data


def shell(name, depth, mat, c):
    me = src.copy()
    me.name = name
    V = np.empty(len(me.vertices) * 3); me.vertices.foreach_get("co", V); V = V.reshape(-1, 3)
    if depth > 0:
        V = S.project(V, level=-depth, iters=8)
    me.vertices.foreach_set("co", V.ravel())
    me.materials.clear(); me.materials.append(mat)
    bm = bmesh.new(); bm.from_mesh(me)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.y < -0.010], context='VERTS')
    bm.to_mesh(me); bm.free()
    me.update()
    o = bpy.data.objects.new(name, me)
    c.objects.link(o)
    return o


LAYERS = []   # (parent empty, title, subtitle)


def layer_root(name):
    e = bpy.data.objects.new(name, None)
    e.empty_display_size = 0.02
    ROOT.objects.link(e)
    return e


# 1 sensor liner ----------------------------------------------------------------
c1 = coll("L1_Sensor_Liner")
r1 = layer_root("LAYER_1_Sensor_Liner")
liner = shell("L1_Liner_Knit", 0.0045, MK["knit"], c1)
liner.parent = r1
gold = MAT["LX_Gold_Polished"]; blk = MAT["LX_Black_Anodized"]; led = MAT["LX_LED_Status"]


def small(name, kind, loc, normal, mat, c, parent, **kw):
    bm = bmesh.new()
    if kind == 'cyl':
        bmesh.ops.create_cone(bm, cap_ends=True, segments=24, radius1=kw["r"], radius2=kw["r"], depth=kw["h"])
    else:
        bmesh.ops.create_cube(bm, size=1.0)
        for v in bm.verts:
            v.co = Vector((v.co.x * kw["x"], v.co.y * kw["y"], v.co.z * kw["z"]))
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    me.materials.append(mat)
    o = bpy.data.objects.new(name, me); c.objects.link(o)
    z = Vector(normal).normalized(); y = Vector(kw.get("yaxis", (0, 1, 0)))
    y = (y - z * y.dot(z)).normalized(); x = y.cross(z)
    M = Matrix((x, y, z)).transposed().to_4x4(); M.translation = Vector(loc)
    o.matrix_world = M
    o.parent = parent
    if mat == led:
        o["lx_gain"] = 1.0
    return o


def on_liner(p_guess):
    p, n = S.surface_point(np.asarray(p_guess, float), level=-0.0045)
    return p, n


def curve(name, pts, radius, mat, c, parent):
    cu = bpy.data.curves.new(name, 'CURVE'); cu.dimensions = '3D'
    cu.bevel_depth = radius; cu.bevel_resolution = 3
    sp = cu.splines.new('POLY'); sp.points.add(len(pts) - 1)
    for i, p in enumerate(pts):
        sp.points[i].co = (*p, 1.0)
    cu.materials.append(mat)
    o = bpy.data.objects.new(name, cu); c.objects.link(o); o.parent = parent
    return o


for f in ["thumb"] + S.FINGER_ORDER:
    ch = S.SK[f]
    J, D, U = ch["joints"], ch["dirs"], ch["ups"]
    # IMU on the middle segment (dorsal)
    p, n = on_liner(J[1] + D[1] * 0.01 + U[1] * 0.03)
    small(f"L1_IMU_{f}", 'box', p + n * 0.0008, n, blk, c1, r1, x=0.0050, y=0.0060, z=0.0012, yaxis=D[1])
    # force-sensing pad on the fingertip (palmar)
    p, n = on_liner(J[2] + D[2] * 0.012 - U[2] * 0.03)
    small(f"L1_FSR_{f}", 'cyl', p + n * 0.0003, n, gold, c1, r1, r=0.0040, h=0.0005)
    # haptic motor at the base of the finger (palmar)
    p, n = on_liner(J[0] + D[0] * 0.012 - U[0] * 0.03)
    small(f"L1_Haptic_Motor_{f}", 'cyl', p + n * 0.0012, n, MAT["LX_Titanium_Brushed"], c1, r1, r=0.0032, h=0.0026)
    # gold sensor trace + coolant line along the back of the finger to the wrist
    trace, tube = [], []
    for t in np.linspace(0, 1, 9):
        q = J[3] * (1 - t) + J[0] * t
        k = min(int((1 - t) * 3), 2)
        tp, tn = on_liner(q + U[k] * 0.03)
        trace.append(tuple(tp + tn * 0.0004))
        tube.append(tuple(tp + tn * 0.0016 + S.nrm(np.cross(D[k], U[k])) * 0.0028))
    for y in np.linspace(0.075, 0.008, 6):
        x0 = J[0][0] * (0.35 + 0.65 * (y - 0.008) / 0.067)
        tp, tn = on_liner((x0, y, 0.04))
        trace.append(tuple(tp + tn * 0.0004))
        tube.append(tuple(tp + tn * 0.0016 + np.array([0.0028, 0, 0])))
    curve(f"L1_Trace_{f}", trace, 0.00045, gold, c1, r1)
    if f != "thumb":
        curve(f"L1_Coolant_{f}", tube, 0.0011, MK["cool"], c1, r1)

# palm EDA electrodes, wrist PPG optical sensor, micro pumps, wrist connector
for i, x in enumerate((-0.012, 0.014)):
    p, n = on_liner((x, 0.050, -0.04))
    small(f"L1_EDA_Electrode_{i}", 'cyl', p + n * 0.0003, n, gold, c1, r1, r=0.0055, h=0.0005)
p, n = on_liner((0.0, 0.010, -0.05))
small("L1_PPG_Sensor", 'box', p + n * 0.0010, n, blk, c1, r1, x=0.012, y=0.008, z=0.0018)
small("L1_PPG_LED", 'cyl', p + n * 0.0021, n, led, c1, r1, r=0.0016, h=0.0004)
for i, x in enumerate((-0.016, 0.018)):
    p, n = on_liner((x, 0.030, 0.04))
    small(f"L1_Micro_Pump_{i}", 'cyl', p + n * 0.0030, n, MAT["LX_Titanium_Brushed"], c1, r1, r=0.0050, h=0.0055)
    small(f"L1_Micro_Pump_Cap_{i}", 'cyl', p + n * 0.0060, n, MAT["LX_Gold_Satin"], c1, r1, r=0.0035, h=0.0008)
p, n = on_liner((0.0, 0.004, 0.05))
small("L1_Wrist_Connector", 'box', p + n * 0.0020, n, gold, c1, r1, x=0.020, y=0.007, z=0.0035)
small("L1_Liner_Processor", 'box', on_liner((0.0, 0.045, 0.04))[0] + on_liner((0.0, 0.045, 0.04))[1] * 0.0012,
      on_liner((0.0, 0.045, 0.04))[1], blk, c1, r1, x=0.018, y=0.018, z=0.0020)
LAYERS.append((r1, "1  LUNA SENSOR LINER", "worn alone inside the habitat (IVA mode)\nIMUs, grip-force pads, PPG + EDA, haptics, coolant"))

# 2-4 shells ------------------------------------------------------------------
for idx, (nm, depth, mat, title, sub) in enumerate([
        ("L2_Pressure_Bladder", 0.0030, MK["bladder"], "2  PRESSURE BLADDER", "urethane - holds suit pressure"),
        ("L3_Restraint_Layer", 0.0018, MK["restraint"], "3  RESTRAINT LAYER", "polyester - keeps the bladder's shape"),
        ("L4_Insulation", 0.0008, MK["mylar"], "4  INSULATION", "aluminized Mylar - thermal protection")], start=2):
    c = coll(nm)
    r = layer_root(f"LAYER_{idx}_{nm[3:]}")
    o = shell(nm, depth, mat, c)
    o.parent = r
    LAYERS.append((r, title, sub))

# 5 outer layer + all hardware, baked from the rest pose -------------------------
c5 = coll("L5_Outer_And_Hardware")
r5 = layer_root("LAYER_5_Outer_Hardware")
src_cols = ["Glove_Body", "Glove_Hardware", "Glove_Lights_LED"]
count = 0
for cname in src_cols:
    for o in bpy.data.collections[cname].all_objects:
        if o.type not in ('MESH', 'FONT', 'CURVE'):
            continue
        if o.name.startswith("LX_Cuff_Msg"):
            continue
        ev = o.evaluated_get(dg)
        me = bpy.data.meshes.new_from_object(ev)
        me.transform(o.matrix_world)
        if o.name == "LX_Glove_Body":
            pass
        no = bpy.data.objects.new("L5_" + o.name[3:], me)
        c5.objects.link(no)
        no.parent = r5
        if "lx_gain" in o.keys():
            no["lx_gain"] = 1.0
        count += 1
LAYERS.append((r5, "5  OUTER LAYER + LUNA-X HARDWARE", "Ortho-fabric, RTV grip, carbon guard,\ntitanium wrist disconnect, LUNA core"))
log(f"outer hardware copies: {count}")

# ----------------------------------------------------------------------------
# labels + explode animation
# ----------------------------------------------------------------------------
SPACING = 0.165
CENTER = SPACING * 2
cl = coll("Labels")
for i, (r, title, sub) in enumerate(LAYERS):
    final_x = i * SPACING
    r.location = (CENTER, 0, 0)
    r.keyframe_insert("location", frame=1)
    r.keyframe_insert("location", frame=40 + (4 - i) * 6)
    r.location = (final_x, 0, 0)
    r.keyframe_insert("location", frame=110 + (4 - i) * 6)
    for j, (txt, size, mat, yoff) in enumerate([(title, 0.0105, MK["accent"], -0.215), (sub, 0.0072, MK["label"], -0.232)]):
        cu = bpy.data.curves.new(f"LBL_{i}_{j}", 'FONT')
        cu.body = txt; cu.size = size; cu.align_x = 'CENTER'
        cu.materials.append(mat)
        t = bpy.data.objects.new(f"LBL_{i}_{j}", cu); cl.objects.link(t)
        t.location = (final_x, yoff + 0.20, -0.002)
        t.location = (final_x, yoff, 0.0)
        t.scale = (0, 0, 0); t.keyframe_insert("scale", frame=120 + i * 8)
        t.scale = (1, 1, 1); t.keyframe_insert("scale", frame=132 + i * 8)
    # slow presentation turn of each layer
    r.rotation_euler = (0, 0, 0); r.keyframe_insert("rotation_euler", frame=150)
    r.rotation_euler = (0, math.radians(-25), 0); r.keyframe_insert("rotation_euler", frame=300)

# title
cu = bpy.data.curves.new("LBL_Title", 'FONT')
cu.body = "LUNA-X ADAPTIVE WELLNESS GLOVE  -  LAYER BREAKDOWN"
cu.size = 0.016; cu.align_x = 'CENTER'; cu.materials.append(MK["label"])
t = bpy.data.objects.new("LBL_Title", cu); cl.objects.link(t)
t.location = (CENTER, 0.235, 0.0)

# floor / lights from the main studio
SC.collection.children.link(bpy.data.collections["Studio"])
cd = bpy.data.cameras.new("CAM_Layers"); cd.lens = 32; cd.clip_start = 0.01
cam = bpy.data.objects.new("CAM_Layers", cd); ROOT.objects.link(cam)
cam.location = (CENTER, -0.42, 0.62)
cam.rotation_euler = (Vector((CENTER, 0.02, 0.0)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
SC.camera = cam
key = bpy.data.lights.new("Layers_Key", 'AREA'); key.energy = 60; key.size = 0.8; key.color = (1, 0.97, 0.93)
ko = bpy.data.objects.new("Layers_Key", key); ROOT.objects.link(ko)
ko.location = (CENTER - 0.2, -0.35, 0.6)
ko.rotation_euler = (Vector((CENTER, 0, 0)) - ko.location).to_track_quat('-Z', 'Y').to_euler()

bpy.ops.wm.save_as_mainfile(filepath=TARGET, check_existing=False)
log("saved layers scene")

if DO_RENDER:
    for fr in (1, 80, 160, 280):
        SC.frame_set(fr)
        SC.render.filepath = os.path.join(GLOVE_DIR, "renders", f"layers_{fr:04d}.png")
        with bpy.context.temp_override(scene=SC, view_layer=SC.view_layers[0]):
            SC.frame_set(fr)
            bpy.ops.render.render(write_still=True, scene=SC.name)
    log("layer renders done")
