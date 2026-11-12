# PROJECT ARES · NASA HUNCH — Crew Wellness & Smart-Habitat asset pack, modelled procedurally in Blender.
# 15 assets that tie straight to the rubric (AI psychological monitoring, biomedical health, mental-health countermeasures,
# ECLSS, autonomous maintenance, VR/AR, crew well-being). Real-world scale in metres, front of every asset faces -Y.
# Exports: GLB per asset (website 3D library) + FBX per asset (Unity, realistic game & Lunar Habitat Commander).
# Material naming contract for the game: EMIT_* = emissive light, GLASS_* = transparent, everything else = opaque PBR.
# © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
import bpy, bmesh, math, os, json
from mathutils import Vector, Matrix
BASE = r"C:\Users\jaine\Downloads\Nasa\Moon_Base_Final"
OUT_GLB = BASE + r"\HunchBridge\site\models\wellness"
OUT_FBX = BASE + r"\Assets\Unity3\Resources\Wellness"
for d in (OUT_GLB, OUT_FBX): os.makedirs(d, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
rep = {}
R90 = math.pi / 2

def mat(name, col, metal=0.0, rough=0.5, emit=None, strength=4.0, alpha=1.0):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True; b = m.node_tree.nodes.get('Principled BSDF')
    b.inputs['Base Color'].default_value = (*col, 1); b.inputs['Metallic'].default_value = metal; b.inputs['Roughness'].default_value = rough
    if emit:
        b.inputs['Emission Color'].default_value = (*emit, 1); b.inputs['Emission Strength'].default_value = strength
    if alpha < 1:
        b.inputs['Alpha'].default_value = alpha
        for attr, val in (('surface_render_method', 'BLENDED'), ('blend_method', 'BLEND')):
            try: setattr(m, attr, val)
            except Exception: pass
    return m
def emat(name, col, strength=5.0): return mat('EMIT_' + name, col, emit=col, strength=strength)
WHITE = mat('ShellWhite', (.88, .9, .93), rough=.35); GREY = mat('Graphite', (.09, .1, .12), rough=.45)
ALU = mat('Aluminium', (.75, .77, .8), metal=1, rough=.28); NAVY = mat('Strap_Navy', (.05, .16, .32), rough=.8)
SCREEN = mat('ScreenGlass', (.01, .02, .04), rough=.1); GOLD = mat('SensorGold', (1, .76, .34), metal=1, rough=.25)
FELT = mat('AcousticFelt', (.16, .2, .28), rough=.95); FOAM = mat('Mattress', (.55, .6, .7), rough=.9)
SOIL = mat('Soil', (.12, .08, .05), rough=1); LEAF = mat('Leaf', (.12, .5, .16), rough=.6); LEAF2 = mat('Leaf_Light', (.35, .72, .22), rough=.6)
WOOD = mat('BambooComposite', (.55, .4, .24), rough=.6); FABRIC = mat('Upholstery', (.26, .34, .44), rough=.9); RUG = mat('Rug', (.18, .26, .3), rough=1)
RUBBER = mat('Rubber', (.03, .03, .03), rough=.9)
GLASS = mat('GLASS_Clear', (.7, .85, 1), rough=.05, alpha=.18); WATER = mat('GLASS_Water', (.15, .45, 1), rough=.05, alpha=.45)
HOLO = mat('GLASS_EMIT_Hologram', (.3, .85, 1), emit=(.25, .8, 1), strength=3, alpha=.35)
CYAN = emat('Cyan', (.22, .74, .97), 6); GREEN = emat('Green', (.15, 1, .45), 6); RED = emat('Red', (1, .18, .2), 6)
AMBER = emat('Amber', (1, .62, .18), 5); BLUE = emat('CalmBlue', (.25, .45, 1), 4); MAGENTA = emat('GrowLight', (.9, .25, 1), 5)
WARM = emat('Sunrise', (1, .55, .2), 6); WHITE_L = emat('WhiteLight', (1, .97, .9), 5); PURPLE = emat('Aurora', (.55, .3, 1), 4)

def setmat(o, m): o.data.materials.clear(); o.data.materials.append(m); return o
def bevel(o, w=.002, seg=3):
    md = o.modifiers.new('bev', 'BEVEL'); md.width = w; md.segments = seg; md.limit_method = 'ANGLE'; return o
def box(sx, sy, sz, loc, m, bv=None, name='box', rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc); o = bpy.context.object; o.scale = (sx, sy, sz); o.name = name
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True); o.rotation_euler = rot; setmat(o, m)
    if bv: bevel(o, bv)
    return o
def cyl(r, d, loc, m, rot=(0, 0, 0), v=48, name='cyl', bv=None):
    bpy.ops.mesh.primitive_cylinder_add(radius=r, depth=d, location=loc, rotation=rot, vertices=v); o = bpy.context.object; o.name = name; setmat(o, m)
    if bv: bevel(o, bv)
    return o
def cone(r1, r2, d, loc, m, rot=(0, 0, 0), v=32, name='cone'):
    bpy.ops.mesh.primitive_cone_add(radius1=r1, radius2=r2, depth=d, location=loc, rotation=rot, vertices=v); o = bpy.context.object; o.name = name; setmat(o, m); return o
def sph(r, loc, m, name='sph', scale=(1, 1, 1), seg=24):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=r, location=loc, segments=seg, ring_count=seg // 2); o = bpy.context.object; o.name = name; o.scale = scale; setmat(o, m); return o
def torus(R, r, loc, m, rot=(0, 0, 0), name='torus', scale=(1, 1, 1), seg=64):
    bpy.ops.mesh.primitive_torus_add(major_radius=R, minor_radius=r, location=loc, rotation=rot, major_segments=seg, minor_segments=12); o = bpy.context.object; o.name = name; o.scale = scale; setmat(o, m); return o
def text(s, size, loc, m, rot=(R90, 0, 0), extrude=.0004, name='label', align='CENTER'):
    bpy.ops.object.text_add(location=loc, rotation=rot); o = bpy.context.object; o.data.body = s; o.data.size = size; o.data.extrude = extrude
    o.data.align_x = align; o.data.align_y = 'CENTER'; bpy.ops.object.convert(target='MESH'); o.name = name; setmat(o, m); return o
def ecg(width, height, loc, m, rot=(R90, 0, 0), beats=3, thick=.0012, name='ECG_trace'):
    pts = []; n = 50 * beats
    for i in range(n + 1):
        x = i / n; ph = (x * beats) % 1; y = 0
        if .10 < ph < .16: y = .12 * math.sin((ph - .10) / .06 * math.pi)
        elif .22 < ph < .24: y = -.15
        elif .24 <= ph < .27: y = 1.0 - abs(ph - .255) / .015 * 1.1
        elif .27 <= ph < .29: y = -.25
        elif .40 < ph < .52: y = .25 * math.sin((ph - .40) / .12 * math.pi)
        pts.append(((x - .5) * width, y * height * .5, 0))
    cd = bpy.data.curves.new(name, 'CURVE'); cd.dimensions = '3D'; cd.bevel_depth = thick; cd.bevel_resolution = 1
    sp = cd.splines.new('POLY'); sp.points.add(len(pts) - 1)
    for p, c in zip(sp.points, pts): p.co = (*c, 1)
    o = bpy.data.objects.new(name, cd); bpy.context.scene.collection.objects.link(o); o.location = loc; o.rotation_euler = rot
    bpy.ops.object.select_all(action='DESELECT'); bpy.context.view_layer.objects.active = o; o.select_set(True); bpy.ops.object.convert(target='MESH'); setmat(o, m); return o
def heart(size, loc, m, rot=(R90, 0, 0), name='heart_icon'):
    pts = [(16 * math.sin(t) ** 3 / 17, (13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t)) / 17) for t in [i / 40 * 2 * math.pi for i in range(40)]]
    me = bpy.data.meshes.new(name); bm = bmesh.new(); vs = [bm.verts.new((x * size, y * size, 0)) for x, y in pts]; bm.faces.new(vs)
    bm.to_mesh(me); bm.free(); o = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(o)
    o.location = loc; o.rotation_euler = rot; sol = o.modifiers.new('s', 'SOLIDIFY'); sol.thickness = size * .15; setmat(o, m); return o
def bar(val, w, h, loc, m, name='bar'):
    """horizontal gauge: dark track + lit fill (val 0..1), faces -Y"""
    x, y, z = loc; box(w, .004, h, (x, y, z), GREY, name=name + '_track')
    box(w * val, .004, h * .8, (x - w * (1 - val) / 2, y - .003, z), m, name=name + '_fill')
def screen(w, h, loc, lines, bezel=WHITE, depth=.03, name='Screen'):
    """wall/desk screen facing -Y. lines = [(text, size, material, dz, dx)]"""
    x, y, z = loc
    box(w + .04, depth, h + .04, (x, y, z), bezel, bv=.006, name=name + '_Bezel')
    box(w, .004, h, (x, y - depth / 2 - .001, z), SCREEN, name=name + '_Panel')
    fy = y - depth / 2 - .004
    for ln in lines:
        s, size, m, dz = ln[:4]; dx = ln[4] if len(ln) > 4 else 0
        text(s, size, (x + dx, fy, z + dz), m, name=name + '_Text')
    return fy
def plant(loc, s=1.0, m=LEAF, n=5, seg=12):
    x, y, z = loc
    for k in range(n):
        a = k / n * 2 * math.pi
        sph(.05 * s, (x + math.cos(a) * .035 * s, y + math.sin(a) * .035 * s, z + .03 * s + (k % 2) * .02 * s), m if k % 2 else LEAF2, name='Leaf', scale=(1.3, .8, .55), seg=seg)
def flower(loc, col_m, h=.3):
    x, y, z = loc; cyl(.006, h, (x, y, z + h / 2), LEAF, v=8, name='Stem')
    for k in range(5):
        a = k / 5 * 2 * math.pi; sph(.022, (x + math.cos(a) * .025, y + math.sin(a) * .025, z + h), col_m, name='Petal', scale=(1, 1, .4), seg=10)
    sph(.014, (x, y, z + h + .005), AMBER, name='Flower_Centre', seg=10)

mark_set = set()
def mark(): global mark_set; mark_set = set(bpy.data.objects.keys())
def group(name, loc=(0, 0, 0), rot=(0, 0, 0)):
    """parent everything created since mark() to a new empty and move/rotate it as one piece"""
    new = [bpy.data.objects[k] for k in bpy.data.objects.keys() if k not in mark_set]
    e = bpy.data.objects.new(name, None); bpy.context.scene.collection.objects.link(e)
    for o in new: o.parent = e
    e.location = loc; e.rotation_euler = rot; bpy.context.view_layer.update(); return e

def capsule(L, R, W, loc, m, part='full', thick=.03, name='Capsule'):
    """capsule lying along X: length L between the cap centres, radius R (height), width W; part = full/top/bottom"""
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=False, cap_tris=False, segments=48, radius1=R, radius2=R, depth=L, matrix=Matrix.Rotation(R90, 4, 'Y'))
    for sx in (-1, 1): bmesh.ops.create_uvsphere(bm, u_segments=48, v_segments=24, radius=R, matrix=Matrix.Translation((sx * L / 2, 0, 0)))
    inside = [v for v in bm.verts if abs(v.co.x) < L / 2 - 1e-4 and (v.co.y ** 2 + v.co.z ** 2) < (R * R) * .999]
    bmesh.ops.delete(bm, geom=inside, context='VERTS')
    if part != 'full':
        g = bm.verts[:] + bm.edges[:] + bm.faces[:]
        bmesh.ops.bisect_plane(bm, geom=g, dist=1e-5, plane_co=(0, 0, 0), plane_no=(0, 0, 1), clear_outer=(part == 'bottom'), clear_inner=(part == 'top'))
    for v in bm.verts: v.co.y *= W / (2 * R)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(o); o.location = loc; setmat(o, m)
    for p in me.polygons: p.use_smooth = True
    if thick: s = o.modifiers.new('shell', 'SOLIDIFY'); s.thickness = thick
    return o

def export(key, title, facts):
    objs = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    fm = bpy.data.objects.new('FRONT_MARKER', None); bpy.context.scene.collection.objects.link(fm); fm.location = (0, -1, 0)   # the game reads this to know which way the asset faces
    bpy.ops.object.select_all(action='DESELECT')
    for o in bpy.context.scene.objects: o.select_set(True)
    glb = os.path.join(OUT_GLB, key + '.glb')
    bpy.ops.export_scene.gltf(filepath=glb, export_format='GLB', use_selection=True, export_apply=True, export_cameras=False, export_lights=False)
    fbx = os.path.join(OUT_FBX, key + '.fbx')
    bpy.ops.export_scene.fbx(filepath=fbx, use_selection=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', use_mesh_modifiers=True, mesh_smooth_type='FACE', bake_space_transform=True, object_types={'EMPTY', 'MESH'})
    tris = 0; dg = bpy.context.evaluated_depsgraph_get()
    mn = Vector((1e9,) * 3); mx = -mn
    for o in objs:
        me = o.evaluated_get(dg).to_mesh(); tris += sum(len(p.vertices) - 2 for p in me.polygons)
        for c in o.bound_box:
            p = o.matrix_world @ Vector(c); mn = Vector(map(min, mn, p)); mx = Vector(map(max, mx, p))
    sz = mx - mn
    rep[key] = dict(title=title, kb=round(os.path.getsize(glb) / 1024), tris=tris, objects=len(objs), size_m=[round(sz.x, 2), round(sz.y, 2), round(sz.z, 2)], **facts)
    for o in list(bpy.context.scene.objects): bpy.data.objects.remove(o, do_unlink=True)
    for me in list(bpy.data.meshes):
        if me.users == 0: bpy.data.meshes.remove(me)

# ───────────────────────── 1) AI PSYCHOLOGICAL MONITORING WRISTBAND ─────────────────────────
torus(.032, .007, (0, 0, 0), NAVY, rot=(R90, 0, 0), name='Band', scale=(1, 1, 1.25))
box(.048, .034, .015, (0, 0, .041), WHITE, bv=.004, name='Pod')
box(.038, .026, .0012, (0, 0, .0488), SCREEN, name='Screen')
torus(.0215, .0012, (0, 0, .0492), BLUE, name='Mood_Ring', scale=(1.12, .78, 1))
heart(.0035, (-.011, .0045, .0497), RED, rot=(0, 0, 0), name='HR_Icon')
text('64', .0055, (-.0035, .0045, .0497), GREEN, rot=(0, 0, 0), name='HR_Value', align='LEFT')
ecg(.03, .004, (0, -.0015, .0497), GREEN, rot=(0, 0, 0), thick=.00025, beats=2, name='HR_Trace')
text('STRESS LOW · SLEEP 91', .0021, (0, -.0075, .0497), CYAN, rot=(0, 0, 0), name='Stress_Text')
sph(.0022, (.026, 0, .043), RED, name='Emergency_LED', scale=(.6, 1, 1))
cyl(.003, .004, (-.026, 0, .043), ALU, rot=(0, R90, 0), name='SOS_Button', v=16)
for dx in (-.005, .005): sph(.0017, (dx, 0, .0328), GREEN, name='PPG_LED', scale=(1, 1, .5), seg=10)
for dx in (-.012, .012): cyl(.0025, .001, (dx, 0, .033), GOLD, name='EDA_Contact', v=16)
cyl(.0035, .001, (0, .009, .033), ALU, name='Skin_Temp_Sensor', v=16)
box(.008, .016, .004, (0, -.0, -.0395), ALU, bv=.001, name='Clasp')
for k in range(5): cyl(.0012, .006, (.0, 0, -.0395), GREY, name='Strap_Hole', v=8).location.x += (k - 2) * .005
for dz in (.0, .006): box(.0012, .006, .003, (.0245, .008, .04 + dz), ALU, name='Side_Button')
for dx in (-.003, .003): cyl(.0012, .001, (dx, -.012, .033), GOLD, name='Charge_Pin', v=8)
export('ai_wristband', 'AI Psychological Monitoring Wristband', dict(cat='psych', priority=1))

# ───────────────────────── 2) PERSONAL SANCTUARY RELAXATION POD ─────────────────────────
cyl(1.25, .12, (0, 0, .06), WHITE, v=64, name='Platform', bv=.02)
torus(1.22, .015, (0, 0, .125), BLUE, name='Floor_Glow')
for k in range(15):   # noise-cancelling acoustic wall: 15 felt panels around 250°, open at the front
    a = math.radians(-35 + k * 250 / 14)   # gap of 110° centred on -Y (the front)
    x, y = math.cos(a) * 1.13, math.sin(a) * 1.13
    p = box(.44, .08, 1.95, (x, y, 1.1), FELT, bv=.02, name='Acoustic_Panel'); p.rotation_euler = (0, 0, a + R90)
    f = box(.46, .04, .06, (x * 1.02, y * 1.02, .15), ALU, name='Panel_Foot'); f.rotation_euler = (0, 0, a + R90)
cyl(1.25, .08, (0, 0, 2.12), WHITE, v=64, name='Roof', bv=.02)
torus(.9, .02, (0, 0, 2.07), BLUE, name='Breathing_Light')     # slow 6-breaths-a-minute pulse in the game
box(.62, .7, .14, (0, .15, .42), FABRIC, bv=.05, name='Recliner_Seat'); box(.66, .74, .3, (0, .15, .25), WHITE, bv=.03, name='Recliner_Base')
b = box(.62, .14, .85, (0, .55, .85), FABRIC, bv=.05, name='Recliner_Back'); b.rotation_euler = (math.radians(-28), 0, 0)
box(.4, .14, .18, (0, .78, 1.27), FABRIC, bv=.06, name='Headrest', rot=(math.radians(-28), 0, 0))
for sx in (-.36, .36): box(.1, .62, .08, (sx, .2, .62), WHITE, bv=.03, name='Armrest')
box(.44, .3, .12, (0, -.38, .28), FABRIC, bv=.04, name='Footrest', rot=(math.radians(20), 0, 0))
cyl(.16, .03, (.72, -.15, .62), WHITE, name='Side_Table', bv=.008); cyl(.03, .5, (.72, -.15, .36), ALU, name='Side_Post')
box(.16, .09, .07, (.72, -.15, .68), GREY, bv=.025, name='VR_Headset'); torus(.07, .008, (.72, -.12, .68), NAVY, rot=(0, 0, 0), name='VR_Strap', scale=(1, 1.2, .5))
cyl(.08, .012, (.72, -.15, .64), CYAN, name='VR_Dock_Charger')
for i, (px, py) in enumerate(((-.75, .25), (-.65, .65), (.62, .62))):   # interactive holo-plants
    cyl(.1, .25, (px, py, .25), WHITE, name='HoloPlanter', bv=.01)
    for j in range(3): sph(.12 - j * .03, (px, py, .45 + j * .14), HOLO, name='HoloPlant_Leaf', scale=(1, 1, .7), seg=16)
    cone(.12, .0, .5, (px, py, .7), HOLO, name='HoloPlant_Frond')
screen(.42, .26, (-.95, -.62, 1.25), [('SANCTUARY', .045, CYAN, .07), ('CALM 82%', .06, GREEN, -.01), ('NOISE -38 dB · 6 BREATHS/MIN', .022, WHITE_L, -.08)], name='Sanctuary_Status')
export('sanctuary_pod', 'Personal Sanctuary Relaxation Pod', dict(cat='psych', priority=2))

# ───────────────────────── 3) MODULAR LUNAR GREENHOUSE ─────────────────────────
box(3.2, 2.2, .1, (0, 0, .05), ALU, bv=.01, name='Floor_Plate')
for sx in (-1.55, 1.55):
    for sy in (-1.05, 1.05): box(.08, .08, 2.4, (sx, sy, 1.25), WHITE, bv=.01, name='Frame_Post')
for sy in (-1.05, 1.05): box(3.18, .08, .08, (0, sy, 2.45), WHITE, name='Top_Beam')
for sx in (-1.55, 1.55): box(.08, 2.18, .08, (sx, 0, 2.45), WHITE, name='Side_Beam')
box(3.1, .02, 2.3, (0, 1.06, 1.25), GLASS, name='Back_Glass'); box(.02, 2.1, 2.3, (-1.56, 0, 1.25), GLASS, name='Side_Glass_L'); box(.02, 2.1, 2.3, (1.56, 0, 1.25), GLASS, name='Side_Glass_R')
for i, tx in enumerate((-1.05, -.35, .35, 1.05)):   # 4 hydroponic towers
    cyl(.13, 1.9, (tx, .55, 1.05), WHITE, name='Hydro_Tower', bv=.01); cyl(.17, .1, (tx, .55, .15), GREY, name='Tower_Base')
    for r in range(6):
        for k in range(4):
            a = k / 4 * 2 * math.pi + r * .4; z = .45 + r * .26
            cyl(.035, .06, (tx + math.cos(a) * .14, .55 + math.sin(a) * .14, z), GREY, rot=(0, R90, a), name='Grow_Cup', v=12)
            plant((tx + math.cos(a) * .19, .55 + math.sin(a) * .19, z - .02), .7, n=3, seg=8)
    box(.06, .06, 1.6, (tx, .2, 1.4), MAGENTA, name='Grow_Light_Bar')
for z in (.35, .55): cyl(.035, 3.0, (0, .9, z), WATER, rot=(0, R90, 0), name='Water_Return_Tube', v=16)
cyl(.28, 1.0, (-1.3, -.7, .6), WATER, name='Nutrient_Tank', bv=.01); torus(.28, .02, (-1.3, -.7, 1.1), ALU, name='Tank_Ring'); torus(.28, .02, (-1.3, -.7, .1), ALU, name='Tank_Ring')
box(.5, .4, .9, (1.25, -.75, .55), WHITE, bv=.02, name='O2_Module')
for k in range(6): box(.4, .02, .03, (1.25, -.96, .35 + k * .08), GREY, name='O2_Grille')
text('O2 +2.1 kg/day', .045, (1.25, -.962, .85), GREEN, name='O2_Readout')
for r in range(3):   # salad trays at the front
    box(2.0, .3, .12, (0, -.2 - r * .3, .16), WHITE, bv=.01, name='Grow_Tray')
    for k in range(9): plant((-.9 + k * .225, -.2 - r * .3, .2), .9, n=4, seg=8)
screen(.6, .38, (.0, -1.0, 1.55), [('AI GROWTH MONITOR', .04, CYAN, .13), ('GROWTH 94%', .07, GREEN, .03), ('pH 6.0   EC 1.8   22°C', .035, WHITE_L, -.06), ('MOOD SESSIONS TODAY: 3', .03, AMBER, -.13)], name='Growth_Monitor')
export('greenhouse', 'Modular Lunar Greenhouse', dict(cat='life', priority=3))

# ───────────────────────── 4) SMART MEDICAL SCANNER BAY (realistic CMO station) ─────────────────────────
mark()
box(2.6, 1.6, .06, (0, 0, .03), mat('AntiStaticFloor', (.32, .36, .42), rough=.6), bv=.01, name='Base_Plate')
for sy in (-.62, .62): box(2.4, .07, .04, (0, sy, .08), ALU, name='Arch_Rail'); box(2.4, .02, .01, (0, sy, .105), GREY, name='Rail_Groove')
# hydraulic chair: column, bellows, seat, backrest, headrest, leg rest, armrests with control pads, restraint straps
cyl(.2, .05, (0, 0, .085), GREY, v=48, name='Column_Foot', bv=.01)
for k in range(5): torus(.1, .018, (0, 0, .14 + k * .045), RUBBER, name='Column_Bellows')
cyl(.08, .2, (0, 0, .42), ALU, name='Column_Ram')
box(.66, .7, .08, (0, 0, .55), WHITE, bv=.02, name='Seat_Pan')
box(.62, .32, .1, (0, -.17, .63), FABRIC, bv=.04, name='Seat_Cushion_L'); box(.62, .32, .1, (0, .17, .63), FABRIC, bv=.04, name='Seat_Cushion_R')
box(.9, .66, .06, (-.55, 0, .9), WHITE, bv=.02, name='Backrest_Shell', rot=(0, math.radians(-50), 0))
box(.84, .6, .08, (-.53, 0, .95), FABRIC, bv=.035, name='Backrest_Cushion', rot=(0, math.radians(-50), 0))
box(.22, .34, .1, (-.93, 0, 1.33), FABRIC, bv=.045, name='Headrest', rot=(0, math.radians(-50), 0))
for sy in (-.12, .12): sph(.012, (-.95, sy, 1.39), GOLD, name='Headrest_EEG_Pad', seg=10)
box(.62, .56, .07, (.6, 0, .48), FABRIC, bv=.03, name='Legrest', rot=(0, math.radians(28), 0)); box(.08, .56, .26, (.92, 0, .26), WHITE, bv=.02, name='Footplate', rot=(0, math.radians(-10), 0))
for sy in (-.42, .42):
    box(.64, .11, .06, (-.02, sy, .78), FABRIC, bv=.025, name='Armrest'); cyl(.03, .2, (-.02, sy, .67), ALU, name='Armrest_Post')
    box(.16, .08, .012, (.18, sy, .815), SCREEN, name='Armrest_Control_Pad')
    for j in range(3): cyl(.008, .006, (.13 + j * .045, sy, .823), (GREEN, AMBER, RED)[j], v=12, name='Pad_Button')
    box(.05, .1, .03, (-.02, sy * 1.12, .78), NAVY, name='Wrist_Restraint')
box(.08, .72, .012, (.05, 0, .69), NAVY, name='Lap_Strap'); box(.06, .04, .02, (.05, 0, .70), ALU, name='Strap_Buckle')
box(.07, .64, .012, (-.5, 0, 1.0), NAVY, name='Chest_Strap', rot=(0, math.radians(-50), 0))
# smooth scanning arch on the rails: outer shell, inner emitter line, 8 sensor windows, carriage feet
def half_torus(R, r, loc, m, name):
    bpy.ops.mesh.primitive_torus_add(major_radius=R, minor_radius=r, major_segments=72, minor_segments=16); o = bpy.context.object; o.name = name
    bm = bmesh.new(); bm.from_mesh(o.data); g = bm.verts[:] + bm.edges[:] + bm.faces[:]
    bmesh.ops.bisect_plane(bm, geom=g, dist=1e-5, plane_co=(0, 0, 0), plane_no=(0, 1, 0), clear_inner=True); bm.to_mesh(o.data); bm.free()
    o.location = loc; o.rotation_euler = (R90, 0, R90); setmat(o, m)
    for pl in o.data.polygons: pl.use_smooth = True
    return o
half_torus(.66, .085, (-.15, 0, .12), WHITE, 'Scanner_Arch')
half_torus(.575, .012, (-.15, 0, .12), CYAN, 'Scan_Emitter_Line')
for k in range(8):
    a = math.pi * (k + .5) / 8; y = math.cos(a) * .6; z = .12 + math.sin(a) * .6
    box(.05, .06, .06, (-.15, y, z), SCREEN, name='Sensor_Window', rot=(a, 0, 0))
for sy in (-.62, .62): box(.28, .14, .1, (-.15, sy, .14), GREY, bv=.015, name='Arch_Carriage')
text('BIOMEDICAL SCAN · mmWAVE + THERMAL + PPG', .028, (-.06, -.0, 1.42), CYAN, rot=(R90, 0, R90), name='Arch_Label')
# articulated monitor arm with the live vitals screen
cyl(.035, 1.1, (.95, -.7, .6), ALU, name='Monitor_Post'); sph(.045, (.95, -.7, 1.17), GREY, name='Arm_Joint')
box(.5, .05, .05, (.75, -.78, 1.2), ALU, name='Arm_Link_1', rot=(0, 0, math.radians(20))); sph(.04, (.53, -.86, 1.2), GREY, name='Arm_Joint')
mark()
screen(.5, .34, (0, 0, 0), [('PATIENT VITALS', .03, CYAN, .13), ('HR 66', .05, GREEN, .06, -.12), ('SpO2 98%', .05, CYAN, .06, .11), ('BP 118/76', .035, WHITE_L, -.1, -.12), ('37.0 °C', .035, AMBER, -.1, .12), ('RESP 14', .025, WHITE_L, -.145)], name='Vitals_Monitor')
ecg(.44, .06, (0, -.022, -.025), GREEN, beats=4, thick=.0016)
group('Vitals_Grp', loc=(.42, -.92, 1.35), rot=(math.radians(-8), 0, math.radians(-25)))
mark()
# overhead exam light
cyl(.03, .5, (-.9, .7, 1.95), ALU, name='Light_Post_Top'); cyl(.025, 1.9, (-.9, .7, .95), ALU, name='Light_Post')
box(.6, .04, .04, (-.62, .45, 2.18), ALU, name='Light_Arm', rot=(0, 0, math.radians(-40)))
cyl(.2, .06, (-.35, .2, 2.12), WHITE, v=48, name='Exam_Light_Head', bv=.01); cyl(.17, .01, (-.35, .2, 2.085), WHITE_L, v=48, name='Exam_Light_LEDs')
# medical cart: drawers, AED, ultrasound, glove box, sharps bin, blood analyser, portable O2
CART = (-1.05, -.55)
box(.55, .45, .8, (CART[0], CART[1], .5), WHITE, bv=.02, name='Med_Cart')
for k, (lab, col) in enumerate((('AIRWAY', BLUE), ('MEDS', GREEN), ('TRAUMA', RED), ('IV', AMBER))):
    z = .8 - k * .17; box(.5, .02, .14, (CART[0], CART[1] - .23, z), WHITE, bv=.006, name='Cart_Drawer'); box(.18, .025, .02, (CART[0], CART[1] - .245, z + .03), ALU, name='Drawer_Handle')
    text(lab, .03, (CART[0], CART[1] - .246, z - .035), col, name='Drawer_Label')
for x, y in ((-.27, -.22), (.27, -.22), (-.27, .22), (.27, .22)): sph(.035, (CART[0] + x, CART[1] + y, .06), RUBBER, name='Caster', seg=10)
box(.24, .18, .1, (CART[0] - .1, CART[1] + .05, .95), mat('AED_Yellow', (1, .8, .1), rough=.4), bv=.02, name='AED_Defibrillator'); heart(.03, (CART[0] - .1, CART[1] - .041, .955), RED, name='AED_Heart'); text('AED', .03, (CART[0] - .1, CART[1] - .041, .92), GREY, name='AED_Label')
box(.2, .14, .015, (CART[0] + .14, CART[1], .91), GREY, bv=.004, name='Ultrasound_Base'); box(.2, .015, .13, (CART[0] + .14, CART[1] + .07, .98), SCREEN, name='Ultrasound_Screen', rot=(math.radians(-15), 0, 0))
cyl(.012, .1, (CART[0] + .25, CART[1] - .05, .96), WHITE, name='Ultrasound_Probe')
box(.12, .08, .06, (CART[0] + .14, CART[1] - .14, .93), mat('Glove_Box', (.4, .55, .9)), name='Glove_Dispenser')
cyl(.05, .12, (CART[0] - .2, CART[1] - .14, .96), mat('Sharps_Red', (.85, .1, .1), rough=.4), name='Sharps_Container')
box(.07, .03, .13, (CART[0] - .32, CART[1] - .1, .96), GREY, bv=.01, name='Blood_Analyser'); box(.05, .005, .04, (CART[0] - .32, CART[1] - .116, .99), SCREEN, name='Analyser_Screen')
O2G = mat('O2_Green', (.1, .55, .25), rough=.35)
cyl(.06, .55, (CART[0] + .33, CART[1] + .1, .55), O2G, name='Portable_O2_Cylinder'); cyl(.025, .06, (CART[0] + .33, CART[1] + .1, .85), ALU, name='O2_Regulator')
torus(.08, .006, (CART[0] + .33, CART[1] - .02, .75), mat('Tube_Clear', (.8, .9, 1), rough=.1), rot=(R90, 0, 0), name='O2_Tubing'); sph(.04, (CART[0] + .33, CART[1] - .1, .7), GLASS, name='O2_Mask', scale=(1, .7, 1.2))
# IV pole + infusion pump, red-cross bay sign, emergency AI display
cyl(.015, 1.8, (1.05, .65, .95), ALU, name='IV_Pole'); torus(.08, .008, (1.05, .65, 1.85), ALU, name='IV_Hook')
box(.14, .04, .2, (1.05, .65, 1.66), GLASS, name='IV_Bag'); cyl(.003, .5, (1.05, .63, 1.35), mat('Tube_Clear', (.8, .9, 1), rough=.1), name='IV_Line')
box(.14, .1, .18, (1.05, .58, 1.15), WHITE, bv=.015, name='Infusion_Pump'); box(.09, .005, .05, (1.05, .528, 1.19), SCREEN, name='Pump_Screen'); text('25 ml/h', .015, (1.05, .524, 1.19), GREEN, name='Pump_Rate')
for x, y in ((-.15, 0), (.15, 0), (0, -.15), (0, .15)): box(.3 if y == 0 else .04, .04 if y == 0 else .3, .02, (1.05 + x / 2, .65 + y / 2, .07), ALU, name='IV_Base_Leg')
box(.5, .03, .5, (0, .8, 2.0), WHITE, bv=.01, name='Bay_Sign'); box(.3, .035, .08, (0, .79, 2.03), RED, name='Cross_H'); box(.08, .035, .3, (0, .79, 2.03), RED, name='Cross_V')
text('CMO · MEDICAL', .04, (0, .776, 1.82), GREY, name='Bay_Label')
cyl(.02, .3, (0, .8, 2.3), ALU, name='Sign_Hanger')
group('Bay_Equipment')
mark()
screen(.36, .24, (0, 0, 0), [('AI MED', .04, RED, .06), ('EMERGENCY READY', .03, WHITE_L, -.01), ('AED · O2 · CMO LINK', .022, AMBER, -.07)], bezel=RED, name='Emergency_Display')
group('Emerg_Grp', loc=(-1.05, -.55, 1.45), rot=(0, 0, math.radians(10)))
export('medical_scanner', 'Smart Medical Scanner Bay', dict(cat='medical', priority=4))

# ───────────────────────── 5) AI COMPANION HOLOGRAM PROJECTOR ─────────────────────────
mark()
cyl(.5, .05, (0, 0, .72), WHITE, v=64, name='Table_Top', bv=.012); cyl(.06, .68, (0, 0, .36), ALU, name='Table_Pedestal'); cyl(.32, .04, (0, 0, .02), ALU, v=48, name='Table_Foot')
cyl(.16, .03, (0, 0, .76), GREY, v=48, name='Projector_Base'); torus(.15, .01, (0, 0, .78), CYAN, name='Projector_Ring'); cyl(.05, .015, (0, 0, .78), CYAN, name='Projector_Lens')
cone(.15, .02, .8, (0, 0, 1.18), HOLO, name='Light_Cone', v=48)
sph(.09, (0, 0, 1.55), HOLO, name='Counsellor_Head')
cone(.17, .06, .45, (0, 0, 1.25), HOLO, name='Counsellor_Torso'); sph(.07, (0, 0, 1.47), HOLO, name='Counsellor_Neck', scale=(2.2, 1, .6))
for z, R in ((1.0, .22), (1.7, .2), (1.85, .14)): torus(R, .006, (0, 0, z), CYAN, name='Emotion_Ring')
heart(.035, (-.32, -.05, 1.55), RED, name='Empathy_Heart')
box(.36, .005, .16, (.34, -.1, 1.52), HOLO, name='Holo_Card', rot=(0, 0, math.radians(-15)))
text('HOW ARE YOU', .03, (.34, -.106, 1.56), WHITE_L, rot=(R90, 0, math.radians(-15)), name='Holo_Q1'); text('FEELING TODAY?', .03, (.34, -.106, 1.51), WHITE_L, rot=(R90, 0, math.radians(-15)), name='Holo_Q2')
text('CALM 4 / 5', .025, (.34, -.106, 1.46), GREEN, rot=(R90, 0, math.radians(-15)), name='Holo_Calm')
for a in (math.radians(-120), math.radians(-60)):
    cyl(.2, .06, (math.cos(a) * .85, math.sin(a) * .85, .45), FABRIC, name='Stool_Seat', bv=.02); cyl(.04, .42, (math.cos(a) * .85, math.sin(a) * .85, .21), ALU, name='Stool_Post')
export('hologram_companion', 'AI Companion Hologram Projector', dict(cat='psych', priority=5))

# ───────────────────────── 6) ADAPTIVE CIRCADIAN LIGHTING SYSTEM ─────────────────────────
box(.22, .18, .05, (0, 0, .025), WHITE, bv=.015, name='Lamp_Base')
cyl(.012, .32, (0, .03, .2), ALU, name='Lamp_Stem', rot=(math.radians(-10), 0, 0))
cyl(.14, .03, (0, 0, .4), WHITE, rot=(math.radians(75), 0, 0), name='Lamp_Disc', bv=.006)
for R, m in ((.04, WHITE_L), (.08, WARM), (.115, AMBER)): torus(R, .012 if R > .05 else .03, (0, -.018, .4), m, rot=(math.radians(75), 0, 0), name='Sunrise_Ring')
torus(.135, .004, (0, -.016, .4), BLUE, rot=(math.radians(75), 0, 0), name='Melatonin_Safe_Ring')
box(.12, .005, .035, (0, -.091, .03), SCREEN, name='Clock_Panel'); text('06:30 SUNRISE', .012, (0, -.094, .03), AMBER, name='Clock_Text')
box(.6, .04, .03, (0, .3, .015), WARM, name='Under_Bunk_Strip')
export('circadian_light', 'Adaptive Circadian Lighting System', dict(cat='psych', priority=6))

# ───────────────────────── 7) EMOTIONAL SUPPORT GARDEN MODULE ─────────────────────────
for bx in (-.7, .7):
    box(1.2, .6, .5, (bx, .2, .25), WOOD, bv=.02, name='Raised_Bed'); box(1.12, .52, .05, (bx, .2, .48), SOIL, name='Bed_Soil')
cols = [mat('Petal_Pink', (1, .45, .65), rough=.6), mat('Petal_Blue', (.35, .55, 1), rough=.6), mat('Petal_Yellow', (1, .85, .25), rough=.6), mat('Petal_Lilac', (.7, .5, 1), rough=.6)]
for i in range(16):
    bx = -1.2 + (i % 8) * .34; by = .05 + (i // 8) * .3; flower((bx + (.02 if i % 2 else -.02), by, .5), cols[i % 4], .22 + (i % 3) * .06)
for k in range(5): plant((-1.2 + k * .6, .42, .5), 1.4)
box(1.4, .45, .06, (0, -.75, .45), WOOD, bv=.015, name='Bench_Seat'); box(1.4, .06, .35, (0, -.95, .7), WOOD, bv=.015, name='Bench_Back')
for bx in (-.6, .6): box(.06, .4, .42, (bx, -.75, .21), ALU, name='Bench_Leg')
box(2.6, .05, .05, (0, .55, 1.3), ALU, name='Arm_Rail'); box(.12, .1, .1, (.3, .55, 1.25), WHITE, name='Arm_Carriage')
cyl(.02, .5, (.3, .45, 1.0), ALU, name='Planting_Arm', rot=(math.radians(25), 0, 0)); cone(.03, .005, .08, (.3, .34, .74), CYAN, rot=(math.radians(205), 0, 0), name='Seed_Nozzle')
for bx in (-1.3, 1.3): box(.05, .05, 1.3, (bx, .55, .65), ALU, name='Rail_Post')
cyl(.15, .7, (1.55, -.3, .35), WHITE, name='Water_Station', bv=.01); cyl(.012, .25, (1.55, -.42, .62), ALU, rot=(R90, 0, 0), name='Tap')
screen(.4, .2, (0, .56, 1.6), [('GARDEN · MOOD +12', .035, GREEN, .03), ('BLOOMS 16 · WATER 1.2 L', .022, WHITE_L, -.04)], name='Garden_Sign')
export('emotional_garden', 'Emotional Support Garden Module', dict(cat='psych', priority=7))

# ───────────────────────── 8) ECLSS CORE LIFE SUPPORT MACHINE ─────────────────────────
box(2.0, .9, 2.1, (0, .2, 1.05), WHITE, bv=.02, name='ECLSS_Cabinet')
for k, x in enumerate((-.65, -.35, -.05)):   # CO2 scrubber beds
    cyl(.12, 1.3, (x, -.32, .95), ALU, name='CO2_Scrubber'); torus(.12, .012, (x, -.32, 1.62), GREEN, name='Scrubber_Status')
    cyl(.13, .05, (x, -.32, .28), GREY, name='Scrubber_Cap')
box(.45, .2, .9, (.45, -.33, 1.2), GREY, bv=.02, name='O2_Generator')
for k in range(5): box(.36, .02, .05, (.45, -.44, .9 + k * .12), CYAN, name='OGA_Cell_Light')
for k, x in enumerate((.3, .62)): cyl(.14, .6, (x, -.4, .4), WATER, name='Water_Tank'); torus(.14, .01, (x, -.4, .7), ALU, name='Tank_Band')
for z in (1.75, 1.85): cyl(.03, 1.9, (0, -.3, z), ALU, rot=(0, R90, 0), name='Manifold_Pipe', v=16)
for x in (-.75, .75): cyl(.03, .4, (x, -.3, 1.6), ALU, name='Riser_Pipe', v=16)
for x in (-.6, .6): cyl(.16, .02, (x, -.26, 2.0), GREY, rot=(R90, 0, 0), name='Fan_Grille'); torus(.16, .01, (x, -.27, 2.0), ALU, rot=(R90, 0, 0), name='Fan_Ring')
screen(.5, .3, (.0, -.27, 2.0), [('ECLSS', .04, CYAN, .1), ('O2 21.0%  CO2 0.3%', .035, GREEN, .02), ('H2O RECOVERY 92%', .03, WHITE_L, -.05), ('FILTER CHANGE IN 14 d', .025, AMBER, -.11)], name='ECLSS_Display')
export('eclss_core', 'ECLSS Core Life Support Machine', dict(cat='life', priority=8))

# ───────────────────────── 9) AUTONOMOUS MAINTENANCE DRONE ─────────────────────────
box(.42, .42, .14, (0, 0, .5), WHITE, bv=.05, name='Drone_Body')
for sx in (-1, 1):
    for sy in (-1, 1):
        x, y = sx * .3, sy * .3; box(.18, .04, .03, (x / 2, y / 2, .5), GREY, name='Fan_Arm', rot=(0, 0, math.atan2(y, x)))
        torus(.11, .015, (x, y, .52), WHITE, name='Duct'); cyl(.012, .03, (x, y, .52), GREY, name='Hub'); box(.2, .025, .004, (x, y, .53), GREY, name='Rotor', rot=(0, 0, .4))
sph(.05, (0, -.21, .47), SCREEN, name='Camera_Dome', scale=(1, 1, .8)); torus(.05, .005, (0, -.21, .47), CYAN, rot=(R90, 0, 0), name='Camera_Ring')
for sx in (-.12, .12):
    cyl(.012, .22, (sx, -.12, .35), ALU, rot=(math.radians(30), 0, 0), name='Tool_Arm')
    cyl(.02, .05, (sx, -.18, .24), GREY, name='Tool_Head')
cyl(.035, .2, (-.12, -.19, .2), RUBBER, rot=(0, R90, 0), name='Dust_Brush', v=24)
cone(.02, .004, .05, (.12, -.19, .19), AMBER, rot=(math.pi, 0, 0), name='Repair_Probe')
sph(.012, (0, .21, .55), GREEN, name='Status_LED', seg=10)
box(.6, .6, .08, (0, 0, .04), GREY, bv=.01, name='Dock_Pad'); torus(.25, .01, (0, 0, .085), CYAN, name='Dock_Guide'); box(.12, .12, .02, (0, 0, .09), GOLD, name='Charge_Contacts')
text('DRONE BAY', .05, (0, -.301, .04), CYAN, name='Dock_Label')
export('maintenance_drone', 'Autonomous Maintenance Drone', dict(cat='research', priority=9))

# ───────────────────────── 10) SMART HABITAT CONTROL CONSOLE ─────────────────────────
box(1.8, .75, .06, (0, 0, .76), WHITE, bv=.015, name='Desk_Top'); box(1.7, .65, .7, (0, .03, .38), GREY, bv=.02, name='Desk_Body')
mark()
screen(.52, .34, (0, 0, 0), [('CREW MOOD', .035, CYAN, .12), ('78', .12, GREEN, 0), ('UP 4 THIS WEEK', .028, WHITE_L, -.12)], name='Mood_Screen'); group('S1', (-.6, .12, 1.15), (math.radians(-10), 0, math.radians(18)))
mark()
screen(.6, .38, (0, 0, 0), [('HABITAT HEALTH', .035, CYAN, .14), ('O2 98%   PWR 86%', .035, GREEN, .07), ('RAD 0.4 mSv/day', .035, AMBER, .01), ('CREW WELLNESS', .028, WHITE_L, -.06)], name='Habitat_Screen')
for i, v in enumerate((.82, .74, .9, .66)): bar(v, .44, .022, (0, -.022, -.1 - i * .028), GREEN if v > .7 else AMBER, name='Wellness_Bar')
group('S2', (0, .18, 1.2), (math.radians(-10), 0, 0))
mark()
screen(.52, .34, (0, 0, 0), [('TRENDS 7 d', .035, CYAN, .12), ('SLEEP 7.4 h', .04, GREEN, .04), ('STRESS DOWN 12%', .04, GREEN, -.03), ('NO ALERTS', .03, WHITE_L, -.11)], name='Trend_Screen'); group('S3', (.6, .12, 1.15), (math.radians(-10), 0, math.radians(-18)))
mark()
box(.5, .18, .02, (0, -.18, .8), GREY, bv=.005, name='Keyboard'); cyl(.05, .015, (.35, -.2, .8), CYAN, name='Trackball')
cyl(.25, .08, (0, -.75, .48), FABRIC, name='Chair_Seat', bv=.03); cyl(.04, .42, (0, -.75, .23), ALU, name='Chair_Post'); box(.45, .08, .5, (0, -.98, .8), FABRIC, bv=.03, name='Chair_Back')
export('habitat_console', 'Smart Habitat Control Console', dict(cat='research', priority=10))

# ───────────────────────── 11) EMERGENCY MENTAL HEALTH STATION ─────────────────────────
box(1.2, .12, 2.0, (0, .1, 1.0), WHITE, bv=.02, name='Wall_Panel')
box(1.24, .14, .06, (0, .1, 2.0), BLUE, name='Calm_Header_Light')
cyl(.12, .06, (0, -.0, 1.05), RED, rot=(R90, 0, 0), name='Talk_Now_Button'); torus(.14, .012, (0, -.02, 1.05), WHITE, rot=(R90, 0, 0), name='Button_Ring')
text('TALK NOW', .045, (0, -.04, .86), RED, name='Button_Label')
torus(.16, .02, (0, -.0, 1.5), BLUE, rot=(R90, 0, 0), name='Breathing_Guide'); text('BREATHE IN · 4 s', .03, (0, -.04, 1.5), WHITE_L, name='Breathe_Text')
fy = screen(.5, .22, (0, .02, 1.8), [('CRISIS SUPPORT · 24/7', .03, CYAN, .05), ('PRIVATE · CREW-CONTROLLED', .025, WHITE_L, -.03)], name='Support_Screen')
box(.1, .05, .2, (-.45, -.0, 1.2), GREY, bv=.02, name='Handset'); box(.03, .03, .05, (-.45, -.03, 1.33), ALU, name='Handset_Hook')
torus(.08, .012, (.45, -.02, 1.25), GREY, rot=(R90, 0, 0), name='Headset_Band', scale=(1, 1, 1)); sph(.04, (.37, -.03, 1.2), GREY, name='Ear_Cup', scale=(.5, 1, 1)); sph(.04, (.53, -.03, 1.2), GREY, name='Ear_Cup', scale=(.5, 1, 1))
box(.4, .25, .05, (0, -.12, .6), WHITE, bv=.01, name='Kit_Shelf'); box(.2, .15, .12, (-.1, -.12, .68), mat('Kit_Blue', (.2, .35, .7)), bv=.01, name='Comfort_Kit'); box(.1, .1, .1, (.12, -.12, .68), WHITE, bv=.02, name='Weighted_Blanket')
box(.03, 1.0, .03, (-.75, -.45, 2.1), ALU, name='Curtain_Rail')
for k in range(6): box(.02, .1, 1.9, (-.75 + (k % 2) * .03, -.08 - k * .07, 1.1), mat('Curtain', (.55, .65, .8), rough=.95), name='Privacy_Curtain')
export('emergency_mh_station', 'Emergency Mental Health Station', dict(cat='emergency', priority=11))

# ───────────────────────── 12) BIOMETRICS SENSOR WALL ARRAY ─────────────────────────
box(2.4, .1, 1.3, (0, .05, .95), WHITE, bv=.015, name='Array_Backplate')
kinds = ['CAM', 'MIC', 'CO2', 'TEMP', 'UWB', 'RAD']
for r in range(3):
    for c in range(6):
        x = -1.0 + c * .4; z = 1.35 - r * .38; k = kinds[c]
        box(.34, .04, .32, (x, -.02, z), GREY, bv=.01, name=f'Tile_{k}')
        if k == 'CAM': sph(.06, (x, -.04, z + .03), SCREEN, name='Lens', scale=(1, .6, 1))
        elif k == 'MIC':
            for j in range(4): box(.2, .01, .015, (x, -.045, z + .07 - j * .035), RUBBER, name='Mic_Slot')
        elif k == 'CO2': cyl(.07, .02, (x, -.05, z + .03), ALU, rot=(R90, 0, 0), name='NDIR_Cell')
        elif k == 'TEMP': cyl(.025, .2, (x, -.05, z + .02), ALU, name='Thermistor')
        elif k == 'UWB': box(.14, .02, .14, (x, -.05, z + .03), GOLD, name='UWB_Antenna')
        else: cyl(.06, .03, (x, -.05, z + .03), AMBER, rot=(R90, 0, 0), name='Dosimeter')
        sph(.012, (x + .13, -.045, z - .12), GREEN if (r + c) % 5 else AMBER, name='Tile_LED', seg=10)
        if r == 2: text(k, .035, (x, -.042, z - .21), CYAN, name='Tile_Label')
text('BIOMETRICS & ENVIRONMENT SENSOR ARRAY', .045, (0, -.002, 1.65), CYAN, name='Array_Title')
export('sensor_wall', 'Biometrics Sensor Wall Array', dict(cat='life', priority=12))

# ───────────────────────── 13) VIRTUAL EARTH WINDOW SYSTEM ─────────────────────────
box(1.9, .14, 1.2, (0, .07, 1.5), WHITE, bv=.03, name='Window_Frame'); box(1.76, .02, 1.06, (0, -.01, 1.5), SCREEN, name='OLED_Panel')
OCEAN = emat('Earth_Ocean', (.1, .35, .9), 2.5); LAND = emat('Earth_Land', (.2, .6, .25), 2.2); CLOUD = emat('Earth_Cloud', (.95, .97, 1), 2.5)
cyl(.38, .01, (.15, -.025, 1.45), OCEAN, rot=(R90, 0, 0), name='Earth_Disc', v=64)
for (dx, dz, s) in ((-.1, .12, .12), (.12, -.05, .14), (.05, .2, .07), (-.15, -.15, .08)): sph(s, (.15 + dx, -.03, 1.45 + dz), LAND, name='Continent', scale=(1.3, .05, .9))
for (dx, dz) in ((.0, .28), (-.22, .02), (.2, .12)): sph(.07, (.15 + dx, -.035, 1.45 + dz), CLOUD, name='Cloud', scale=(2.0, .04, .5))
torus(.385, .012, (.15, -.03, 1.45), CYAN, rot=(R90, 0, 0), name='Atmosphere_Glow')
for k in range(24): sph(.006, (-.85 + (k * 37 % 160) / 100, -.025, 1.05 + (k * 53 % 90) / 100), WHITE_L, name='Star', seg=6)
text('VIRTUAL WINDOW · EARTH LIVE', .035, (-.48, -.03, 1.95), CYAN, name='Window_Label')
box(1.9, .25, .06, (0, -.05, .88), WHITE, bv=.015, name='Sill'); plant((.7, -.08, .91), 1.2)
export('earth_window', 'Virtual Earth Window System', dict(cat='psych', priority=13))

# ───────────────────────── 14) CREW ADAPTIVE HEALTH & RECOVERY SLEEP POD (3 AI states) ─────────────────────────
STATES = {'healthy': ((.15, 1, .45), 45, 'HEALTHY', 'SLEEP 91% · HR 58', .0),
          'fatigue': ((1, .78, .15), 18, 'FATIGUE DETECTED', 'CIRCADIAN RESET 21:30', .25),
          'stress': ((.25, .5, 1), 0, 'STRESS · CALM PROTOCOL', 'EARTH VIEW · COUNSELLOR ON', .55)}
def recovery_pod(state):
    col, opn, line1, line2, tint = STATES[state]
    S = emat('State_' + state, col, 6)
    CAN = mat('GLASS_Electrochromic_' + state, (.55 - tint * .45, .75 - tint * .5, 1 - tint * .4), rough=.05, alpha=.2 + tint * .5)
    box(2.3, 1.0, .45, (0, 0, .225), WHITE, bv=.02, name='Pod_Base')
    for sy in (-.45, .45): box(2.6, .08, .05, (0, sy, .025), ALU, name='Mounting_Rail')
    for k, x in enumerate((-.7, 0, .7)):   # retractable storage drawers
        box(.6, .02, .3, (x, -.505, .24), GREY, bv=.005, name='Storage_Drawer'); box(.2, .03, .025, (x, -.52, .33), ALU, name='Drawer_Handle')
    for x in (-1.0, 1.0):
        for k in range(4): box(.12, .02, .02, (x, -.505, .1 + k * .04), RUBBER, name='Thermal_Vent')
    box(2.2, .04, .03, (0, -.52, .43), PURPLE, name='Aurora_Strip')
    capsule(1.7, .55, 1.1, (0, 0, 1.0), WHITE, 'bottom', .035, name='Pod_Shell')
    hinge = bpy.data.objects.new('Canopy_Hinge', None); bpy.context.scene.collection.objects.link(hinge); hinge.location = (0, .55, 1.0)
    can = capsule(1.7, .55, 1.1, (0, -.55, 0), CAN, 'top', .015, name='Smart_Canopy'); can.parent = hinge
    hinge.rotation_euler = (-math.radians(opn), 0, 0)
    box(1.95, .85, .14, (0, 0, .72), FOAM, bv=.05, name='Mattress')
    box(.3, .7, .16, (-.9, 0, .86), FABRIC, bv=.07, name='EEG_Headrest')
    for k in range(7):
        a = k / 6 * math.pi; sph(.014, (-.88 + math.sin(a) * .05, -.25 + k * .083, .95), GOLD, name='EEG_Electrode', seg=10)
    box(1.2, .6, .006, (.1, 0, .795), CYAN, name='Sleep_Sensor_Mat')   # BCG heart-rate/breathing + sleep stages
    for sy in (-.52, .52): box(1.9, .03, .03, (0, sy, 1.0), S, name='Circadian_Strip')
    for x in (-.6, .6): cyl(.03, .02, (x, .5, .9), GREY, rot=(R90, 0, 0), name='Nature_Speaker', v=16)
    for x in (-1.45, 1.45): sph(.035, (x * .98, 0, 1.0), RED, name='Emergency_Light', seg=12)
    for sy in (-1, 1): box(.08, .6, 1.0, (-1.55, sy * .5, 1.0), WHITE, bv=.02, name='Privacy_Wing', rot=(0, 0, sy * math.radians(25)))
    for sx in (-.95, -.6): box(.04, .04, .95, (sx, -.62, .5), ALU, name='Ladder_Rail')
    for k in range(3): cyl(.015, .35, (-.775, -.63, .25 + k * .28), ALU, rot=(0, R90, 0), name='Ladder_Rung', v=12)
    for x in (-1.35, 1.35): box(.08, .08, 2.1, (x, .62, 1.05), ALU, name='Gantry_Post')
    box(2.78, .1, .08, (0, .62, 2.1), ALU, name='Gantry_Beam'); box(.18, .16, .12, (-.3, .62, 2.02), WHITE, bv=.02, name='Scanner_Carriage')
    cyl(.02, .3 if opn else .25, (-.3, .5, 1.85), ALU, rot=(math.radians(-35), 0, 0), name='Scanner_Arm'); cyl(.07, .04, (-.3, .4, 1.72), WHITE, name='Scanner_Head'); torus(.07, .008, (-.3, .4, 1.7), CYAN, name='Scan_Ring')
    cyl(.07, .03, (.92, 0, .81), GREY, name='Holo_Projector'); torus(.06, .006, (.92, 0, .83), CYAN, name='Projector_Ring')
    if state == 'stress':
        sph(.05, (.92, 0, 1.15), HOLO, name='Counsellor_Head'); cone(.09, .03, .25, (.92, 0, .98), HOLO, name='Counsellor_Body')
        for k in range(3): torus(.18 + k * .06, .006, (0, 0, 1.25 + k * .05), BLUE, name='Calm_Halo')
    mark()
    screen(.44, .26, (0, 0, 0), [('EARTH VIEW' if state == 'stress' else 'SLEEP DATA', .03, CYAN, .09), ('EARTH LIVE' if state == 'stress' else 'DEEP 22%  REM 21%', .045 if state == 'stress' else .03, (emat('Earth_Ocean', (.1, .35, .9), 2.5) if state == 'stress' else WHITE_L), -.01)], bezel=WHITE, name='Foot_Screen')
    group('FootScreen', (1.08, 0, 1.05), (0, 0, math.radians(-90)))
    mark()
    screen(.62, .2, (0, 0, 0), [(line1, .04, S, .04), (line2, .028, WHITE_L, -.04)], bezel=GREY, name='Status_Display')
    group('Status', (.62, -.51, .27))
    if state == 'fatigue':
        mark()
        screen(.5, .24, (0, 0, 0), [('AI RECOMMENDS', .03, AMBER, .08), ('+ LIGHT THERAPY 20 min', .025, WHITE_L, .02), ('+ GREENHOUSE SESSION', .025, WHITE_L, -.03), ('+18 WELLNESS', .03, GREEN, -.08)], bezel=GREY, name='AI_Recommend')
        group('Recommend', (-.2, -.9, 1.5))
    export('recovery_pod_' + state, 'Adaptive Recovery Sleep Pod · ' + state, dict(cat='psych', priority=0, state=state))
for st in ('healthy', 'fatigue', 'stress'): recovery_pod(st)

# ───────────────────────── 15) STRESS RECOVERY LOUNGE ─────────────────────────
cyl(1.6, .02, (0, 0, .01), RUG, v=64, name='Round_Rug')
for k in range(9):   # curved sofa
    a = math.radians(200 + k * 140 / 8); x = math.cos(a) * 1.25
    box(.48, .7, .38, (x, math.sin(a) * 1.25, .19), FABRIC, bv=.05, name='Sofa_Seat', rot=(0, 0, a + R90))
for k in range(9):
    a = math.radians(200 + k * 140 / 8); x, y = math.cos(a) * 1.6, math.sin(a) * 1.6
    box(.5, .16, .75, (x, y, .5), FABRIC, bv=.05, name='Sofa_Back', rot=(0, 0, a + R90))
for k in range(5):
    a = math.radians(215 + k * 27); sph(.13, (math.cos(a) * 1.3, math.sin(a) * 1.3, .5), mat('Cushion', (.85, .6, .35), rough=.9), name='Cushion', scale=(1, .5, 1))
cyl(.45, .05, (0, .1, .4), WOOD, v=48, name='Low_Table', bv=.01); cyl(.08, .38, (0, .1, .19), ALU, name='Table_Post')
cyl(.08, .1, (.15, .1, .48), WHITE, name='Tea_Mug'); sph(.08, (-.15, .2, .5), BLUE, name='Calm_Orb')
cyl(1.62, .03, (0, 0, 2.3), WHITE, v=64, name='Ceiling_Ring'); torus(1.55, .02, (0, 0, 2.27), WARM, name='Ambient_Ring_Light')
cyl(.2, .5, (1.3, .7, .25), WHITE, name='Plant_Pot'); plant((1.3, .7, .5), 3)
mark()
screen(1.0, .6, (0, 0, 0), [('NATURE MODE · FOREST', .045, GREEN, .22)], name='Lounge_Screen')
cyl(.2, .01, (0, -.025, -.03), emat('Earth_Ocean', (.1, .35, .9), 2.5), rot=(R90, 0, 0), name='Screen_Earth', v=48)
group('LoungeScreen', (0, 1.75, 1.3))
export('stress_lounge', 'Stress Recovery Lounge', dict(cat='psych', priority=15))


# ═════════════════════════ HEALTH & LIFE-SUPPORT HARDWARE (realistic game) ═════════════════════════
def poly(pts, loc, m, rot=(R90, 0, 0), thick=.002, name='Trace'):
    cd = bpy.data.curves.new(name, 'CURVE'); cd.dimensions = '3D'; cd.bevel_depth = thick; cd.bevel_resolution = 1
    sp = cd.splines.new('POLY'); sp.points.add(len(pts) - 1)
    for p, (x, y) in zip(sp.points, pts): p.co = (x, y, 0, 1)
    o = bpy.data.objects.new(name, cd); bpy.context.scene.collection.objects.link(o); o.location = loc; o.rotation_euler = rot
    bpy.ops.object.select_all(action='DESELECT'); bpy.context.view_layer.objects.active = o; o.select_set(True); bpy.ops.object.convert(target='MESH'); setmat(o, m); return o
O2G = mat('O2_Green', (.1, .55, .25), rough=.35); YEL = mat('Warning_Yellow', (1, .8, .1), rough=.5); TUBE = mat('Tube_Clear', (.8, .9, 1), rough=.1)

# 16) OXYGEN TANK RACK — high-pressure O2 reserve (ECLSS backup + medical O2)
box(1.5, .55, .08, (0, 0, .04), GREY, bv=.01, name='Rack_Base'); box(1.5, .06, 1.8, (0, .26, .95), WHITE, bv=.015, name='Rack_Back')
for k in range(4):
    x = -.54 + k * .36
    cyl(.13, 1.3, (x, .05, .76), O2G, name='O2_Cylinder', v=40); sph(.13, (x, .05, 1.41), O2G, name='Cylinder_Dome', scale=(1, 1, .55))
    cyl(.135, .04, (x, .05, .12), RUBBER, name='Cylinder_Boot')
    for z in (.5, 1.1): torus(.14, .012, (x, .05, z), NAVY, name='Rack_Strap')
    box(.14, .01, .12, (x, -.085, .8), WHITE, name='Cylinder_Label'); text('O2', .05, (x, -.091, .82), O2G, name='O2_Text'); text('6.8 kg', .022, (x, -.091, .77), GREY, name='Mass_Text')
    cyl(.03, .1, (x, .05, 1.53), ALU, name='Cylinder_Valve'); torus(.04, .008, (x, .05, 1.58), mat('Valve_Black', (.05, .05, .05)), name='Valve_Handwheel')
    box(.07, .07, .08, (x, .0, 1.66), ALU, bv=.01, name='Regulator'); cyl(.035, .015, (x, -.04, 1.67), WHITE, rot=(R90, 0, 0), name='Gauge_Face')
    box(.004, .002, .028, (x + .006, -.049, 1.672), RED, name='Gauge_Needle', rot=(0, math.radians(35 - k * 15), 0))
cyl(.02, 1.3, (0, .04, 1.76), ALU, rot=(0, R90, 0), name='O2_Manifold', v=16)
for k in range(4): cyl(.012, .1, (-.54 + k * .36, .02, 1.71), ALU, name='Pigtail_Hose', v=12)
mark()
screen(.32, .3, (0, 0, 0), [('O2 SUPPLY', .03, GREEN, .11), ('2200 psi', .045, WHITE_L, .05), ('4 x 6.8 kg', .025, CYAN, -.0)], name='O2_Panel')
for k, v in enumerate((.95, .9, .82, .4)): bar(v, .24, .016, (0, -.02, -.05 - k * .025), GREEN if v > .5 else AMBER, name='Tank_Level')
group('O2_Panel_Grp', (.95, .1, 1.3), (0, 0, math.radians(-30)))
mark()
sph(.035, (.95, .02, 1.6), AMBER, name='Low_Pressure_Alarm', seg=12)
box(.5, .01, .1, (0, -.235, .2), YEL, name='Oxidizer_Warning'); text('OXIDIZER · NO FLAME', .028, (0, -.242, .2), GREY, name='Warning_Text')
export('oxygen_tanks', 'Oxygen Tank Rack', dict(cat='life', priority=16))

# 17) AI HEALTH DETECTION SYSTEM — on-board edge computer + sensor-fusion dashboard (6 conditions)
box(.7, .8, 2.0, (-.75, .1, 1.0), GREY, bv=.015, name='Compute_Cabinet'); box(.62, .02, 1.85, (-.75, -.31, 1.0), GLASS, name='Cabinet_Glass_Door')
for k in range(10):
    z = .25 + k * .17; box(.58, .7, .12, (-.75, .1, z), mat('Server_Black', (.04, .045, .05), rough=.4), bv=.004, name='Compute_Unit')
    for j in range(6): sph(.006, (-.98 + j * .03, -.255, z), GREEN if (j + k) % 7 else AMBER, name='Unit_LED', seg=8)
    box(.2, .005, .05, (-.6, -.256, z), SCREEN, name='Unit_Display')
text('ARES AI · ON-BOARD · NO CLOUD', .03, (-.75, -.33, 2.06), CYAN, name='Cabinet_Label')
cyl(.04, 1.0, (.45, .1, .5), ALU, name='Dash_Stand'); box(.5, .4, .04, (.45, .1, .02), GREY, bv=.01, name='Dash_Foot')
mark()
screen(1.1, .7, (0, 0, 0), [('AI HEALTH DETECTION', .045, CYAN, .3), ('TIER 0 · ALL 4 CREW OK', .035, GREEN, .23), ('SENSOR FUSION 9 / 9 · CONFIDENCE 0.94', .022, WHITE_L, -.31)], name='Detection_Dash')
for k, (lab, v) in enumerate((('ANXIETY', .22), ('DEPRESSION', .18), ('CONFLICT', .12), ('FATIGUE', .41), ('AUTONOMY', .15), ('MONOTONY', .33))):
    z = .15 - k * .075; text(lab, .026, (-.38, -.02, z), WHITE_L, name='Cond_Label'); bar(v, .55, .03, (.18, -.02, z), AMBER if v > .35 else GREEN, name='Cond_Bar')
group('Dash_Grp', (.45, .1, 1.35))
export('ai_health_hub', 'AI Health Detection System', dict(cat='medical', priority=17))

# 18) MOOD HEALTH TRACKER — voluntary 2-tap mood check-in wall panel + trend + docked tablet
box(.7, .08, 1.0, (0, .04, 1.4), WHITE, bv=.02, name='Mood_Panel')
box(.62, .01, .2, (0, -.005, 1.78), SCREEN, name='Question_Screen'); text('HOW ARE YOU FEELING?', .032, (0, -.012, 1.8), WHITE_L, name='Question'); text('PRIVATE · ONLY YOU SEE THIS', .018, (0, -.012, 1.73), CYAN, name='Privacy_Note')
faces = [('GREAT', (.2, .9, .4)), ('GOOD', (.6, .95, .3)), ('OK', (1, .85, .25)), ('LOW', (1, .55, .2)), ('BAD', (.95, .3, .3))]
for k, (lab, col) in enumerate(faces):
    x = -.24 + k * .12; fm = emat('Mood_' + lab, col, 3)
    cyl(.045, .025, (x, -.01, 1.58), fm, rot=(R90, 0, 0), name='Mood_Button'); text(lab, .018, (x, -.012, 1.51), GREY, name='Mood_Label')
    for ex in (-.015, .015): sph(.006, (x + ex, -.025, 1.595), GREY, name='Eye', seg=8)
    curve = .012 * (1 - k / 2); poly([(-.018, 0), (0, -curve), (.018, 0)], (x, -.026, 1.565), GREY, thick=.002, name='Mouth')
box(.62, .01, .3, (0, -.005, 1.22), SCREEN, name='Trend_Screen'); text('MOOD · LAST 14 DAYS', .02, (0, -.012, 1.34), CYAN, name='Trend_Title')
vals = [.5, .55, .48, .6, .62, .58, .7, .66, .72, .68, .75, .78, .74, .8]
poly([(-.28 + i * .043, (v - .6) * .5) for i, v in enumerate(vals)], (0, -.012, 1.2), GREEN, thick=.003, name='Mood_Trend')
box(.26, .06, .03, (0, -.03, .98), ALU, name='Tablet_Dock'); box(.24, .012, .17, (0, -.04, 1.08), GREY, bv=.008, name='Crew_Tablet', rot=(math.radians(-12), 0, 0)); box(.21, .005, .14, (0, -.047, 1.08), SCREEN, name='Tablet_Screen', rot=(math.radians(-12), 0, 0))
export('mood_tracker', 'Mood Health Tracker', dict(cat='psych', priority=18))

# 19) HEART-RATE MONITOR — bedside vitals monitor on a rolling stand + chest strap & finger clip sensors
for k in range(5):
    a = k / 5 * 2 * math.pi; box(.3, .04, .03, (math.cos(a) * .15, math.sin(a) * .15, .08), GREY, name='Stand_Leg', rot=(0, 0, a)); sph(.03, (math.cos(a) * .3, math.sin(a) * .3, .035), RUBBER, name='Caster', seg=10)
cyl(.025, 1.15, (0, 0, .67), ALU, name='Stand_Pole'); box(.3, .15, .02, (0, -.02, .9), WHITE, name='Accessory_Basket')
box(.44, .14, .34, (0, 0, 1.38), WHITE, bv=.03, name='Monitor_Housing'); box(.36, .004, .26, (0, -.072, 1.4), SCREEN, name='Monitor_Screen')
ecg(.22, .06, (-.05, -.076, 1.47), GREEN, beats=3, thick=.0012, name='ECG_Lead_II')
poly([(-.11 + i * .011, .015 * math.sin(i * .9)) for i in range(21)], (-.05, -.076, 1.39), CYAN, thick=.001, name='Pleth_Wave')
poly([(-.11 + i * .011, .012 * math.sin(i * .35)) for i in range(21)], (-.05, -.076, 1.32), AMBER, thick=.001, name='Resp_Wave')
text('72', .06, (.12, -.077, 1.46), GREEN, name='HR_Big'); heart(.012, (.165, -.077, 1.49), RED, name='HR_Heart')
text('SpO2 98', .022, (.12, -.077, 1.39), CYAN, name='SpO2'); text('HRV 54ms', .02, (.12, -.077, 1.34), WHITE_L, name='HRV')
text('RR 14', .02, (.12, -.077, 1.3), AMBER, name='Resp_Rate')
box(.2, .04, .03, (0, 0, 1.57), WHITE, name='Monitor_Handle'); sph(.012, (.19, -.07, 1.54), GREEN, name='Alarm_Light', seg=10)
torus(.12, .012, (-.05, -.03, .98), NAVY, rot=(R90, 0, 0), name='Chest_Strap', scale=(1, 1, .5)); box(.06, .02, .035, (-.05, -.045, .96), WHITE, bv=.008, name='Strap_Transmitter')
box(.035, .025, .02, (.08, -.03, .935), mat('Clip_Blue', (.2, .45, .9)), bv=.006, name='Finger_Clip')
poly([(0, 0), (.04, .12), (.02, .3), (0, .42)], (.08, -.06, .94), GREY, rot=(R90, 0, 0), thick=.003, name='Sensor_Cable')
export('heart_rate_monitor', 'Heart-Rate & Vitals Monitor', dict(cat='medical', priority=19))

# 20) BLOOD-PRESSURE TRACKER — wall-mounted automatic BP unit with cuff, hose and arm rest
box(.42, .1, .5, (0, .05, 1.35), WHITE, bv=.025, name='BP_Unit'); box(.32, .005, .2, (0, -.002, 1.45), SCREEN, name='BP_Screen')
text('SYS 118', .03, (-.07, -.006, 1.5), GREEN, name='SYS'); text('DIA 76', .03, (-.07, -.006, 1.455), CYAN, name='DIA'); text('PULSE 68', .022, (-.07, -.006, 1.41), WHITE_L, name='PULSE')
heart(.015, (.1, -.006, 1.47), RED, name='BP_Heart'); text('mmHg', .016, (.1, -.006, 1.42), GREY, name='Units')
cyl(.035, .02, (-.1, -.01, 1.24), GREEN, rot=(R90, 0, 0), name='Start_Button'); text('START', .016, (-.1, -.012, 1.19), GREY, name='Start_Label')
cyl(.035, .02, (.1, -.01, 1.24), RED, rot=(R90, 0, 0), name='Stop_Button'); text('STOP', .016, (.1, -.012, 1.19), GREY, name='Stop_Label')
box(.32, .14, .1, (0, -.05, 1.0), WHITE, bv=.015, name='Cuff_Holder')
cyl(.07, .22, (0, -.07, 1.05), NAVY, rot=(0, R90, 0), name='BP_Cuff'); box(.2, .002, .05, (0, -.142, 1.05), mat('Velcro', (.6, .62, .65), rough=1), name='Cuff_Velcro')
for k in range(6): torus(.035, .005, (.2, -.04, 1.15 - k * .025), mat('Hose_Grey', (.5, .52, .55), rough=.6), name='Coiled_Hose')
box(.5, .25, .05, (0, -.2, .82), FABRIC, bv=.02, name='Arm_Rest'); box(.05, .2, .05, (-.2, -.08, .8), ALU, name='Rest_Bracket'); box(.05, .2, .05, (.2, -.08, .8), ALU, name='Rest_Bracket')
text('BLOOD PRESSURE · AUTO LOG TO CMO (WITH CONSENT)', .018, (0, -.001, 1.63), CYAN, name='BP_Label')
export('bp_monitor', 'Blood-Pressure Tracker', dict(cat='medical', priority=20))

# 21) AI SLEEP-DETECTION BED — realistic crew bed + under-mattress sensing + sleep software on the headboard
box(2.1, 1.0, .32, (0, 0, .2), WHITE, bv=.02, name='Bed_Frame')
for x in (-.65, 0, .65): box(.6, .02, .22, (x, -.505, .2), GREY, bv=.005, name='Underbed_Drawer'); box(.16, .02, .02, (x, -.52, .27), ALU, name='Drawer_Pull')
box(2.0, .92, .04, (0, 0, .38), CYAN, name='Sleep_Sensor_Mat')   # ballistocardiography: HR, breathing, movement
box(2.0, .92, .2, (0, 0, .5), FOAM, bv=.05, name='Mattress'); box(2.02, .94, .02, (0, 0, .6), mat('Sheet', (.9, .92, .95), rough=.9), bv=.01, name='Fitted_Sheet')
box(.4, .7, .12, (-.78, 0, .67), mat('Pillow', (.95, .96, .98), rough=.9), bv=.05, name='Pillow')
box(1.2, .96, .06, (.35, 0, .64), NAVY, bv=.03, name='Thermal_Blanket'); box(.25, .96, .08, (.95, 0, .66), NAVY, bv=.03, name='Blanket_Fold')
box(.06, 1.0, 1.0, (-1.06, 0, .85), WHITE, bv=.02, name='Headboard')
box(.06, .9, .1, (1.06, 0, .45), WHITE, bv=.02, name='Footboard')
box(1.0, .03, .2, (.1, .52, .62), ALU, name='Side_Rail'); 
cyl(.012, .4, (-1.0, .4, 1.3), ALU, rot=(0, math.radians(30), 0), name='Reading_Light_Arm'); cone(.05, .02, .07, (-.88, .4, 1.45), WHITE, rot=(0, math.radians(120), 0), name='Reading_Light'); cyl(.04, .005, (-.85, .4, 1.43), WARM, rot=(0, math.radians(120), 0), name='Reading_LED')
mark()
screen(.7, .42, (0, 0, 0), [('AI SLEEP ANALYSIS', .03, CYAN, .17), ('SLEEP SCORE 86', .04, GREEN, .11, -.12), ('7 h 38 m', .03, WHITE_L, .11, .2), ('HR 54 · RESP 13 · 19.5 °C', .022, WHITE_L, -.18)], name='Sleep_Software')
lv = [0, 1, 2, 3, 3, 2, 1, 4, 1, 2, 3, 2, 1, 4, 4, 1, 2, 1, 4, 0]   # 0 awake, 1 light, 2 deep... 4 REM (hypnogram)
hy = []
for i, l in enumerate(lv): x = -.3 + i * .03; z = {0: .06, 1: .02, 2: -.02, 3: -.06, 4: .04}[l]; hy += [(x, z), (x + .03, z)]
poly(hy, (0, -.022, -.02), PURPLE, thick=.0018, name='Hypnogram')
for k, lab in enumerate(('AWAKE', 'REM', 'LIGHT', 'DEEP')): text(lab, .012, (-.33, -.022, .06 - k * .04 + (0 if k != 1 else -.02) * 0), WHITE_L, name='Stage_Label', align='RIGHT')
group('Sleep_SW', (-1.02, 0, 1.12), (0, 0, math.radians(-90)))
mark()
cyl(.025, .02, (-1.03, -.42, .75), AMBER, rot=(0, R90, 0), name='Temp_Knob'); text('19.5°', .02, (-1.03, -.42, .69), WHITE_L, rot=(R90, 0, math.radians(-90)), name='Temp_Readout')
box(.02, 1.0, .02, (1.1, -.55, 2.1), ALU, name='Curtain_Rail')
for k in range(5): box(.08, .02, 1.6, (1.1, -.55 + k * .06, 1.28), mat('Curtain', (.55, .65, .8), rough=.95), name='Privacy_Curtain')
export('smart_bed', 'AI Sleep-Detection Bed', dict(cat='psych', priority=21))

json.dump(rep, open(os.path.join(OUT_GLB, 'wellness_report.json'), 'w'), indent=1)
result = rep
print('WELLNESS DONE', len(rep))
