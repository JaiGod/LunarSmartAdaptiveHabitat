"""LUNA-X glove - animation pack (runs in a background Blender on the glove's OWN file).

Adds: gesture poses + clip actions, the full showcase timeline (startup, calibration, plant scan,
material analysis, wellness wave, crew call, sleep, nav, emergency + fist ack, gestures,
LUNA greenhouse recommendation), micro-mechanics (AI ring, sensor pods, cooling vents).
Usage: blender --background --factory-startup <glove.blend> --python 02_animate.py -- <glove_dir> [render]
"""
import bpy, sys, os, math, random, time
import numpy as np
from math import radians
from mathutils import Vector, Matrix

ARGS = sys.argv[sys.argv.index("--") + 1:]
GLOVE_DIR = ARGS[0]
DO_RENDER = "render" in ARGS
TARGET = os.path.join(GLOVE_DIR, "LUNA-X_Wellness_Glove.blend")
assert os.path.normcase(bpy.data.filepath) == os.path.normcase(TARGET), "refusing to touch any other file"
sys.path.insert(0, os.path.join(GLOVE_DIR, "scripts"))
import lx_sdf as S

SC = bpy.context.scene
arm = bpy.data.objects["LX_Glove_Rig"]
ctrl = bpy.data.objects["LUNAX_Controller"]
OBJ = bpy.data.objects
FINGERS = S.FINGER_ORDER
ALLF = ["thumb"] + FINGERS
FB = [f"{f}_0{i}" for f in ALLF for i in (1, 2, 3)]
POSE_BONES = FB + ["hand", "forearm"]
random.seed(7)


def log(m):
    print("[LX]", m, flush=True)


# ----------------------------------------------------------------------------
# helpers
# ----------------------------------------------------------------------------
def fcurves_of(idb):
    ad = idb.animation_data
    if not ad or not ad.action:
        return []
    act = ad.action
    try:
        return list(act.fcurves)
    except AttributeError:
        from bpy_extras import anim_utils
        cb = anim_utils.action_get_channelbag_for_slot(act, ad.action_slot)
        return list(cb.fcurves) if cb else []


def set_interp(idb, kind='CONSTANT', path_filter=None):
    for fc in fcurves_of(idb):
        if path_filter and path_filter not in fc.data_path:
            continue
        for k in fc.keyframe_points:
            k.interpolation = kind


def key_prop(o, prop, frame, value):
    o[prop] = value
    o.keyframe_insert(f'["{prop}"]', frame=frame)


def gain(name, frame, value):
    key_prop(OBJ[name], "lx_gain", frame, value)


def state(frame, s):
    key_prop(ctrl, "led_state", frame, s)


def pose(p):
    """p: dict bone -> (x, y, z) degrees. Missing finger bones -> 0."""
    for b in POSE_BONES:
        v = p.get(b, (0, 0, 0))
        arm.pose.bones[b].rotation_euler = tuple(radians(a) for a in v)


def key_pose(frame, p, root_loc=(0, 0, 0), root_rot=(0, 0, 0)):
    pose(p)
    for b in POSE_BONES:
        arm.pose.bones[b].keyframe_insert("rotation_euler", frame=frame)
    rb = arm.pose.bones["root"]
    rb.location = root_loc
    rb.rotation_euler = tuple(radians(a) for a in root_rot)
    rb.keyframe_insert("location", frame=frame)
    rb.keyframe_insert("rotation_euler", frame=frame)


def finger(f, a, b, c, splay=0.0):
    return {f"{f}_01": (-a, 0, splay), f"{f}_02": (-b, 0, 0), f"{f}_03": (-c, 0, 0)}


def merge(*ds):
    out = {}
    for d in ds:
        out.update(d)
    return out


for pb in arm.pose.bones:
    pb.rotation_mode = 'XYZ'

# ----------------------------------------------------------------------------
# poses
# ----------------------------------------------------------------------------
RELAX = merge(finger("index", 8, 12, 8), finger("middle", 10, 15, 9), finger("ring", 12, 17, 10),
              finger("pinky", 14, 19, 11), finger("thumb", 4, 8, 8))
FIST = merge(finger("index", 82, 98, 62), finger("middle", 85, 100, 62), finger("ring", 85, 100, 60),
             finger("pinky", 85, 98, 58), {"thumb_01": (-30, 0, -18), "thumb_02": (-35, 0, 0), "thumb_03": (-45, 0, 0)})
POINT = merge(finger("index", 2, 3, 2), finger("middle", 70, 95, 55), finger("ring", 75, 98, 55),
              finger("pinky", 78, 96, 52), {"thumb_01": (-22, 0, -12), "thumb_02": (-30, 0, 0), "thumb_03": (-25, 0, 0)})
OPEN = merge(finger("index", -6, 0, 0, 9), finger("middle", -6, 0, 0, 2), finger("ring", -6, 0, 0, -6),
             finger("pinky", -6, 0, 0, -13), {"thumb_01": (6, 0, 14), "thumb_02": (4, 0, 0), "thumb_03": (2, 0, 0)})
TOUCH = merge(RELAX, finger("index", 18, 22, 10))     # fingertip pad down onto a surface


def solve_pinch():
    """Small search so the thumb tip actually meets the index tip."""
    def cost(x):
        p = merge(RELAX, finger("index", x[0], x[1], x[2]),
                  {"thumb_01": (x[3], 0, x[4]), "thumb_02": (x[5], 0, 0), "thumb_03": (x[6], 0, 0)})
        pose(p)
        bpy.context.view_layer.update()
        ti = arm.matrix_world @ arm.pose.bones["index_03"].tail
        tt = arm.matrix_world @ arm.pose.bones["thumb_03"].tail
        return abs((ti - tt).length - 0.016), p
    best = [30, 40, 25, -20, -10, -20, -20]
    bc, bp = cost(best)
    step = [10, 10, 10, 10, 10, 10, 10]
    lo = [5, 5, 0, -60, -50, -60, -60]
    hi = [70, 90, 70, 20, 40, 10, 10]
    for it in range(8):
        for k in range(len(best)):
            for sgn in (-1, 1):
                cand = list(best)
                cand[k] = min(hi[k], max(lo[k], cand[k] + sgn * step[k]))
                c, p = cost(cand)
                if c < bc:
                    bc, bp, best = c, p, cand
        step = [s * 0.6 for s in step]
    log(f"pinch solved: tip gap error {bc * 1000:.1f} mm, params {[round(v, 1) for v in best]}")
    return bp, bc


PINCH, PINCH_ERR = solve_pinch()

# ----------------------------------------------------------------------------
# micro-mechanics: AI ring pivot, core press pivot, cooling vents, sensor pods
# ----------------------------------------------------------------------------
def make_pivot(name, src_obj, bone):
    pv = bpy.data.objects.new(name, None)
    src_obj.users_collection[0].objects.link(pv)
    pv.empty_display_size = 0.01
    pv.matrix_world = src_obj.matrix_world.copy()
    mw = pv.matrix_world.copy()
    b = arm.data.bones[bone]
    pv.parent = arm; pv.parent_type = 'BONE'; pv.parent_bone = bone
    pm = arm.matrix_world @ b.matrix_local @ Matrix.Translation((0, b.length, 0))
    pv.matrix_parent_inverse = pm.inverted()
    pv.matrix_basis = mw
    child = bpy.data.objects.new(name + "_Anim", None)
    src_obj.users_collection[0].objects.link(child)
    child.empty_display_size = 0.006
    child.parent = pv
    return pv, child


def reparent_keep(o, new_parent):
    mw = o.matrix_world.copy()
    o.parent = new_parent
    o.parent_type = 'OBJECT'
    o.matrix_parent_inverse = new_parent.matrix_world.inverted()
    o.matrix_world = mw


pose({})
bpy.context.view_layer.update()
ring_pv, ring_spin = make_pivot("LX_Core_Ring_Pivot", OBJ["LX_Core_Sensor_Ring"], "hand")
bpy.context.view_layer.update()
reparent_keep(OBJ["LX_Core_Sensor_Ring"], ring_spin)
core_pv, core_press = make_pivot("LX_Core_Press_Pivot", OBJ["LX_Core_Housing"], "hand")
bpy.context.view_layer.update()
for n in ["LX_Core_Housing", "LX_Core_Bezel_Gold", "LX_Core_Display", "LX_Core_Glass_Dome", "LX_Core_Text"] + \
         [f"LX_Core_Screw_{i}" for i in range(6)]:
    reparent_keep(OBJ[n], core_press)

# cooling vents: two titanium flaps on the base plate, hinged on their inner edge
VENTS = []
hb = OBJ["LX_Core_Housing"].matrix_world
for side in (-1, 1):
    me = bpy.data.meshes.new(f"LX_Cooling_Vent_{side}")
    import bmesh
    bmx = bmesh.new()
    bmesh.ops.create_cube(bmx, size=1.0)
    for v in bmx.verts:
        v.co = Vector(((v.co.x + 0.5) * 0.0070, v.co.y * 0.0110, v.co.z * 0.0009))
    bmx.to_mesh(me); bmx.free()
    vo = bpy.data.objects.new(f"LX_Cooling_Vent_{'L' if side < 0 else 'R'}", me)
    OBJ["LX_Core_Housing"].users_collection[0].objects.link(vo)
    me.materials.append(bpy.data.materials["LX_Titanium_Brushed"])
    vo.parent = core_pv
    # local frame of core pivot: Z = surface normal, Y = toward fingers
    vo.location = (side * 0.0215, 0.0, -0.0010)
    vo.rotation_euler = (0, 0, 0 if side > 0 else math.pi)
    vo.rotation_mode = 'XYZ'
    VENTS.append(vo)
    # glowing grille under the flap
    gm = bpy.data.meshes.new(f"LX_Vent_Grille_{side}")
    bmx = bmesh.new()
    bmesh.ops.create_cube(bmx, size=1.0)
    for v in bmx.verts:
        v.co = Vector(((v.co.x + 0.5) * 0.0062, v.co.y * 0.0095, v.co.z * 0.0004 - 0.0008))
    bmx.to_mesh(gm); bmx.free()
    go = bpy.data.objects.new(f"LX_Vent_Grille_{'L' if side < 0 else 'R'}", gm)
    OBJ["LX_Core_Housing"].users_collection[0].objects.link(go)
    gm.materials.append(bpy.data.materials["LX_LED_Status"])
    go.parent = core_pv
    go.location = (side * 0.0215, 0.0, -0.0010)
    go.rotation_euler = (0, 0, 0 if side > 0 else math.pi)
    go["lx_gain"] = 0.0

# sensor pods: tip sensor windows extend along their normal
PODS = {}
for f in ("thumb", "index", "middle"):
    o = OBJ[f"LX_Tip_Sensor_Window_{f}"]
    me = o.data
    nsum = Vector()
    for v in me.vertices:
        nsum += v.normal
    PODS[f] = (o, o.location.copy(), (o.matrix_basis.to_3x3() @ nsum).normalized() if nsum.length > 0 else Vector((0, 0, -1)))

# cuff message (LUNA recommendation) - text baked to mesh and wrapped onto the cuff display
MSG = []
lines = [("LUNA RECOMMENDATION", 0.0026), ("GREENHOUSE ACTIVITY", 0.0030), ("20 MIN", 0.0040)]
for i, (txt, size) in enumerate(lines):
    cu = bpy.data.curves.new(f"LX_Cuff_Msg_{i}", 'FONT')
    cu.body = txt; cu.size = size; cu.align_x = 'CENTER'; cu.align_y = 'CENTER'; cu.extrude = 0.00005
    t = bpy.data.objects.new(f"LX_Cuff_Msg_{i}_tmp", cu)
    SC.collection.objects.link(t)
    t.location = (0.0, -0.0365 - 0.0045 * i, 0.060)
    bpy.context.view_layer.update()
    me = bpy.data.meshes.new_from_object(t.evaluated_get(bpy.context.evaluated_depsgraph_get()))
    bpy.data.objects.remove(t)
    mo = bpy.data.objects.new(f"LX_Cuff_Msg_{i}", me)
    OBJ["LX_Cuff_Status_Display"].users_collection[0].objects.link(mo)
    mo.location = (0.0, -0.0365 - 0.0045 * i, 0.060)
    sw = mo.modifiers.new("wrap", 'SHRINKWRAP')
    sw.target = OBJ["LX_Cuff_Status_Display"]
    sw.wrap_method = 'PROJECT'; sw.use_project_z = True; sw.use_negative_direction = True
    sw.offset = 0.0016
    me.materials.append(bpy.data.materials["LX_LED_Status"])
    mo["lx_gain"] = 0.0
    bpy.context.view_layer.update()
    reparent_keep(mo, OBJ["LX_Cuff_Status_Display"])
    MSG.append(mo)

# ----------------------------------------------------------------------------
# object lists for light sequences
# ----------------------------------------------------------------------------
RINGS = {f: f"LX_Finger_LED_Ring_{f}" for f in ALLF}
TIPS = {f: f"LX_Tip_Status_LED_{f}" for f in ALLF}
EMIT = [o.name for o in OBJ if o.name.startswith("LX_Core_Ring_Emitter_")]
CORE = ["LX_Core_Display", "LX_Core_Text"]
WRIST = "LX_Wrist_Status_LED"
CUFF = "LX_Cuff_Status_Display"
ALL_LED = list(RINGS.values()) + list(TIPS.values()) + EMIT + CORE + [WRIST, CUFF, "LX_Index_IR_Ring"]


def all_gain(frame, v, names=None):
    for n in (names or ALL_LED):
        gain(n, frame, v)


def wave(names, start, step, peak=1.0, base=0.15, width=6):
    """light travels along a list of objects."""
    for i, n in enumerate(names):
        f = start + i * step
        gain(n, f - 1, base)
        gain(n, f + width // 2, peak)
        gain(n, f + width, base)


# ----------------------------------------------------------------------------
# SHOWCASE timeline (30 fps)
# ----------------------------------------------------------------------------
log("showcase timeline")
SC.frame_start, SC.frame_end = 1, 1680
SEG = {}


def seg(name, a, b):
    SEG[name] = (a, b)
    m = SC.timeline_markers.new(name, frame=a)


# 1 STARTUP -------------------------------------------------------------------
seg("01 Power On + Finger Calibration", 1, 150)
all_gain(1, 0.0)
state(1, 1)
gain("LX_Index_IR_Scanner", 1, 0.0)
for n in CORE:
    gain(n, 8, 0.0); gain(n, 16, 1.0)
for i, n in enumerate(EMIT):                      # AI ring emitters come on around the ring
    gain(n, 16 + i * 2, 0.0); gain(n, 18 + i * 2, 1.0)
for i, f in enumerate(ALLF):                      # fingertips light individually
    gain(TIPS[f], 40 + i * 6, 0.0); gain(TIPS[f], 43 + i * 6, 1.0)
wave([TIPS[f] for f in ALLF] + [RINGS[f] for f in reversed(ALLF)], 72, 3, 1.0, 0.0, 6)   # status line across fingers
for f in ALLF:
    gain(RINGS[f], 96, 1.0)
gain(WRIST, 64, 0.0); gain(WRIST, 74, 1.0)
gain(CUFF, 74, 0.0); gain(CUFF, 84, 1.0)
key_pose(1, {})
key_pose(30, RELAX)
f0 = 60                                           # flex test thumb -> pinky
for i, f in enumerate(ALLF):
    a = f0 + i * 16
    key_pose(a, RELAX)
    key_pose(a + 7, merge(RELAX, finger(f, 35, 50, 35)))
    key_pose(a + 14, RELAX)
state(140, 0)                                     # MISSION READY
key_pose(150, RELAX)

# 2 IDLE (humans never hold perfectly still) ------------------------------------
seg("02 Idle Micro-adjust", 150, 300)
for f in range(170, 300, 22):
    jit = {}
    for b, v in RELAX.items():
        jit[b] = (v[0] + random.uniform(-4, 4), 0, v[2] + random.uniform(-1.5, 1.5))
    jit["hand"] = (random.uniform(-3, 3), 0, random.uniform(-2.5, 2.5))
    key_pose(f, jit)
key_pose(300, RELAX)

# 3 PLANT SCAN ------------------------------------------------------------------
seg("03 Greenhouse Plant Scan", 300, 480)
key_pose(325, TOUCH, root_loc=(0, 0, 0))
key_pose(340, TOUCH, root_loc=(0, 0, -0.012))     # fingertip pad touches the leaf
state(340, 1)
for f, (o, loc, n) in PODS.items():               # sensor pods extend
    o.location = loc; o.keyframe_insert("location", frame=336)
    o.location = loc + n * 0.0011; o.keyframe_insert("location", frame=346)
    o.location = loc + n * 0.0011; o.keyframe_insert("location", frame=430)
    o.location = loc; o.keyframe_insert("location", frame=442)
wave([TIPS["index"], RINGS["index"]] + EMIT[:6] + CORE + [WRIST, CUFF], 350, 4, 1.0, 0.2, 8)   # data -> wrist
wave([TIPS["index"], RINGS["index"]] + EMIT[:6] + CORE + [WRIST, CUFF], 395, 4, 1.0, 0.2, 8)
key_pose(430, TOUCH, root_loc=(0, 0, -0.012))
key_pose(450, RELAX, root_loc=(0, 0, 0))
state(450, 0)                                      # results OK
key_pose(480, RELAX)

# 4 MATERIAL ANALYSIS (non-contact IR / spectral) -------------------------------
seg("04 Material Analysis", 480, 600)
key_pose(505, POINT, root_rot=(0, 0, 0))
key_pose(520, POINT, root_rot=(8, 0, -6))
state(512, 1)
gain("LX_Index_IR_Scanner", 512, 0.0); gain("LX_Index_IR_Scanner", 518, 1.0)
gain("LX_Index_IR_Scanner", 575, 1.0); gain("LX_Index_IR_Scanner", 582, 0.0)
for k in range(4):                                  # blue scan pulses
    gain("LX_Index_IR_Ring", 520 + k * 14, 0.2); gain("LX_Index_IR_Ring", 526 + k * 14, 1.0); gain("LX_Index_IR_Ring", 532 + k * 14, 0.2)
key_pose(580, POINT, root_rot=(8, 0, -6))
key_pose(600, RELAX)
state(590, 0)
gain("LX_Index_IR_Ring", 590, 1.0)

# 5 WELLNESS CHECK - fatigue increasing: amber wave finger -> hand -> forearm --
seg("05 Wellness Check (fatigue)", 600, 720)
state(610, 4)
seqw = [TIPS["index"], TIPS["middle"], RINGS["index"], RINGS["middle"], RINGS["ring"], "LX_Core_Display", WRIST, CUFF]
wave(seqw, 615, 5, 1.0, 0.25, 10)
wave(seqw, 660, 5, 1.0, 0.25, 10)
for f in range(612, 700, 4):                         # subtle tremor in the fingers
    j = merge(RELAX, {"index_01": (-8 + random.uniform(-1.2, 1.2), 0, 0), "middle_01": (-10 + random.uniform(-1.2, 1.2), 0, 0)})
    key_pose(f, j)
key_pose(705, RELAX)
state(712, 0)

# 6 CREW MEETING - purple sweep pinky -> thumb ----------------------------------
seg("06 Crew Meeting Signal", 720, 840)
state(722, 3)
order = ["pinky", "ring", "middle", "index", "thumb"]
for rep in range(2):
    wave([n for f in order for n in (RINGS[f], TIPS[f])], 728 + rep * 45, 3, 1.0, 0.3, 8)
state(835, 0)

# 7 SLEEP MODE - slow 6-8 s breathing --------------------------------------------
seg("07 Sleep Mode", 840, 1020)
state(842, 3)
for n in ALL_LED:
    for k, f in enumerate(range(845, 1021, 105)):
        gain(n, f, 0.08); gain(n, f + 52, 0.55)
key_pose(845, RELAX)
key_pose(1000, merge(finger("index", 18, 25, 14), finger("middle", 20, 28, 15), finger("ring", 22, 30, 15),
                     finger("pinky", 24, 30, 15), finger("thumb", 6, 12, 10)))
all_gain(1020, 1.0)
state(1020, 0)

# 8 NAVIGATION - white chase + fingertip torches --------------------------------
seg("08 Navigation Mode", 1020, 1110)
state(1024, 2)
key_pose(1030, POINT, root_rot=(0, 0, 0))
key_pose(1095, POINT, root_rot=(0, 0, 12))
key_pose(1110, RELAX)
state(1108, 0)

# 9 EMERGENCY: fall detected -> red broadcast -> fist held 2 s = acknowledge -----
seg("09 Emergency + Fist Acknowledge", 1110, 1290)
key_pose(1120, RELAX, root_loc=(0, 0, 0), root_rot=(0, 0, 0))
key_pose(1128, OPEN, root_loc=(0.006, 0, -0.025), root_rot=(-14, 6, 8))     # sudden impact
key_pose(1136, OPEN, root_loc=(0.002, 0, -0.018), root_rot=(-8, 2, 4))
state(1128, 5)
key_pose(1170, RELAX, root_loc=(0, 0, 0))
key_pose(1185, FIST)
key_pose(1245, FIST)                                  # held for 2 seconds
for k in range(3):                                     # haptic confirm - triple pulse on rings
    for f in ALLF:
        gain(RINGS[f], 1246 + k * 6, 1.0); gain(RINGS[f], 1249 + k * 6, 0.2)
state(1246, 4)                                         # acknowledged -> caution
key_pose(1262, RELAX)
state(1285, 0)

# 10 GESTURES: pinch = select, open palm = interface, double tap ---------------
seg("10 Gestures: Pinch / Open Palm / Double Tap", 1290, 1440)
key_pose(1300, RELAX)
key_pose(1315, PINCH)
state(1315, 1)
key_pose(1330, PINCH)
key_pose(1345, RELAX)
state(1340, 0)
key_pose(1360, OPEN, root_rot=(0, 0, 0))
key_pose(1395, OPEN, root_rot=(0, 0, 0))
state(1362, 1)
key_pose(1410, RELAX)
for k, f in enumerate((1418, 1430)):                  # double tap
    key_pose(f, merge(RELAX, finger("index", 26, 18, 8)))
    key_pose(f + 6, merge(RELAX, finger("index", 4, 6, 4)))
state(1418, 3)
state(1440, 0)

# 11 SHOWSTOPPER: LUNA recommendation -----------------------------------------
seg("11 LUNA Recommendation: Greenhouse 20 min", 1440, 1680)
key_pose(1440, RELAX)
state(1450, 4)                                         # isolation trend -> amber pulse
gain(CUFF, 1470, 1.0); gain(CUFF, 1480, 0.18)
for i, m in enumerate(MSG):
    gain(m.name, 1478 + i * 8, 0.0); gain(m.name, 1486 + i * 8, 1.0)
key_pose(1500, RELAX, root_rot=(0, 0, 0))
key_pose(1530, RELAX, root_rot=(-12, 0, 0))            # astronaut raises / reads the wrist
# centre hand button press (core housing depresses) + vents open (heavy processing)
for f, z in ((1560, 0.0), (1566, -0.0012), (1574, 0.0)):
    core_press.location = (0, 0, z); core_press.keyframe_insert("location", frame=f)
state(1566, 3)                                          # purple confirmation pulse
for n in ALL_LED:
    gain(n, 1566, 1.0)
for i, m in enumerate(MSG):
    gain(m.name, 1600, 1.0); gain(m.name, 1612, 0.0)
gain(CUFF, 1612, 1.0)
key_pose(1640, RELAX, root_rot=(0, 0, 0))
state(1650, 0)
key_pose(1680, RELAX)

# vents: open during heavy processing (scan + material analysis + recommendation)
for vo in VENTS:
    for f, a in ((1, 0), (340, 0), (350, 38), (440, 38), (452, 0), (512, 0), (522, 38), (590, 38), (600, 0),
                 (1566, 0), (1576, 45), (1630, 45), (1642, 0)):
        vo.rotation_euler = (0, radians(-a), vo.rotation_euler.z)
        vo.keyframe_insert("rotation_euler", frame=f)
for go in [OBJ["LX_Vent_Grille_L"], OBJ["LX_Vent_Grille_R"]]:
    for f, v in ((1, 0), (346, 0), (352, 1), (440, 1), (452, 0), (518, 0), (524, 1), (590, 1), (600, 0), (1572, 0), (1578, 1), (1630, 1), (1642, 0)):
        gain(go.name, f, v)

# AI activity ring: slow idle spin, fast while LUNA processes
spin = 0.0
spin_keys = [(1, 0.0)]
speeds = [(1, 150, 2.0), (150, 340, 0.5), (340, 450, 3.0), (450, 512, 0.5), (512, 590, 2.5), (590, 1300, 0.5),
          (1300, 1560, 0.6), (1560, 1640, 3.0), (1640, 1680, 0.5)]
for a, b, rev_per_s in speeds:
    spin += 2 * math.pi * rev_per_s * (b - a) / 30.0
    spin_keys.append((b, spin))
for f, v in spin_keys:
    ring_spin.rotation_euler = (0, 0, v)
    ring_spin.keyframe_insert("rotation_euler", frame=f)
set_interp(ring_spin, 'LINEAR')

# controller state should snap, not fade
set_interp(ctrl, 'CONSTANT')
SHOW = arm.animation_data.action
SHOW.name = "LX_Showcase_Timeline"
SHOW.use_fake_user = True

# ----------------------------------------------------------------------------
# clip actions for Unity (each a short loop/gesture)
# ----------------------------------------------------------------------------
log("clip actions")
CLIPS = {
    "Gesture_Fist_Acknowledge": [(1, RELAX), (12, FIST), (72, FIST), (88, RELAX)],
    "Gesture_Pinch_Select": [(1, RELAX), (12, PINCH), (24, PINCH), (36, RELAX)],
    "Gesture_Open_Palm": [(1, RELAX), (14, OPEN), (50, OPEN), (64, RELAX)],
    "Gesture_Point_Scan": [(1, RELAX), (14, POINT), (60, POINT), (74, RELAX)],
    "Gesture_Double_Tap": [(1, RELAX), (6, merge(RELAX, finger("index", 26, 18, 8))), (12, RELAX),
                           (18, merge(RELAX, finger("index", 26, 18, 8))), (24, RELAX)],
    "Finger_Calibration": None,
    "Idle_Loop": None,
}
show_action = arm.animation_data.action
for cname, keys in CLIPS.items():
    act = bpy.data.actions.new(cname)
    act.use_fake_user = True
    arm.animation_data.action = act
    if cname == "Finger_Calibration":
        keys = [(1, RELAX)]
        for i, f in enumerate(ALLF):
            keys += [(1 + i * 16 + 7, merge(RELAX, finger(f, 35, 50, 35))), (1 + i * 16 + 14, RELAX)]
    if cname == "Idle_Loop":
        keys = [(1, RELAX)]
        for f in range(25, 120, 25):
            keys.append((f, {b: (v[0] + random.uniform(-4, 4), 0, v[2]) for b, v in RELAX.items()}))
        keys.append((121, RELAX))
    for f, p in keys:
        key_pose(f, p)
arm.animation_data.action = show_action

# ----------------------------------------------------------------------------
# camera: gentle push-in for the showcase
# ----------------------------------------------------------------------------
cam = OBJ["CAM_Hero"]
base = cam.location.copy()
cam.keyframe_insert("location", frame=1)
cam.location = base * 0.96
cam.keyframe_insert("location", frame=1680)
SC.camera = cam
SC.frame_set(1)

info = {"pinch_err_mm": PINCH_ERR * 1000, "segments": SEG}
with open(os.path.join(GLOVE_DIR, "scripts", "02_status.json"), "w") as f:
    import json
    json.dump(info, f, indent=1)
bpy.ops.wm.save_as_mainfile(filepath=TARGET, check_existing=False)
log("saved")

if DO_RENDER:
    SC.render.resolution_x, SC.render.resolution_y = 960, 540
    for fr in (1230, 1320, 1380, 1530):
        SC.frame_set(fr)
        SC.render.filepath = os.path.join(GLOVE_DIR, "renders", f"anim_check_{fr:04d}.png")
        bpy.ops.render.render(write_still=True)
    SC.render.resolution_x, SC.render.resolution_y = 1600, 900
    log("renders done")
