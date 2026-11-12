# PROJECT ARES · NASA HUNCH — crew health-monitoring hardware, modelled procedurally in Blender.
# Matches the D1 sensor list: chest beacon (2-lead ECG -> heart rate/HRV, skin response, UWB), night wristband
# (PPG + accelerometer -> sleep), cortisol microneedle patch, zone sensor pod (paired NDIR CO2, mic array,
# camera, UWB anchor), Crew Health Station (vitals kiosk) and the smart sleep bunk. Exports one GLB per model.
# © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
import bpy, bmesh, math, os, json
from mathutils import Vector
OUT = r"C:\Users\jaine\Downloads\Nasa\Moon_Base_Final\HunchBridge\site\models\health"
os.makedirs(OUT, exist_ok=True)
# FBX copies for the two Unity games (Crew Health Rounds side mission)
OUT_FBX = [r"C:\Users\jaine\Downloads\Nasa\Moon_Base_Final\Assets\Unity3\Resources\Wellness",
           r"C:\Users\jaine\Downloads\Nasa\official projects for nasa\unity2\Nasa_Project\Assets\HUNCH\Resources\Health"]
for d in OUT_FBX: os.makedirs(d, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
rep = {}

def mat(name, col, metal=0.0, rough=0.5, emit=None, strength=4.0, alpha=1.0):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True; b = m.node_tree.nodes.get('Principled BSDF')
    b.inputs['Base Color'].default_value = (*col, 1); b.inputs['Metallic'].default_value = metal; b.inputs['Roughness'].default_value = rough
    if emit:
        b.inputs['Emission Color'].default_value = (*emit, 1); b.inputs['Emission Strength'].default_value = strength
    if alpha < 1:
        b.inputs['Alpha'].default_value = alpha
    return m
WHITE = mat('ShellWhite', (.88, .9, .93), rough=.35); GREY = mat('Graphite', (.09, .1, .12), rough=.45)
ALU = mat('Aluminium', (.75, .77, .8), metal=1, rough=.28); STRAP = mat('Strap', (.05, .16, .32), rough=.8)
SCREEN = mat('ScreenGlass', (.01, .02, .04), rough=.1); GLASS = mat('DarkGlass', (.02, .02, .03), rough=.05, metal=.2)
ECG = mat('ECG_Green', (.1, 1, .45), emit=(.15, 1, .45), strength=6); CYAN = mat('LED_Cyan', (.2, .8, 1), emit=(.22, .74, .97), strength=6)
RED = mat('LED_Red', (1, .2, .2), emit=(1, .18, .2), strength=6); AMBER = mat('LED_Amber', (1, .7, .2), emit=(1, .62, .18), strength=5)
BLUE = mat('Mood_Blue', (.2, .4, 1), emit=(.25, .45, 1), strength=4); SKIN = mat('PatchSkin', (.93, .85, .75), rough=.7)
FOAM = mat('Mattress', (.55, .6, .7), rough=.9); GEL = mat('ElectrodeGel', (.75, .8, .85), rough=.2)

def setmat(o, m):
    o.data.materials.clear(); o.data.materials.append(m); return o
def bevel(o, w=.002, seg=3):
    md = o.modifiers.new('bev', 'BEVEL'); md.width = w; md.segments = seg; md.limit_method = 'ANGLE'; return o
def box(sx, sy, sz, loc, m, bv=None, name='box'):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc); o = bpy.context.object; o.scale = (sx, sy, sz); o.name = name
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True); setmat(o, m)
    if bv: bevel(o, bv)
    return o
def cyl(r, d, loc, m, rot=(0, 0, 0), v=48, name='cyl', bv=None):
    bpy.ops.mesh.primitive_cylinder_add(radius=r, depth=d, location=loc, rotation=rot, vertices=v); o = bpy.context.object; o.name = name; setmat(o, m)
    if bv: bevel(o, bv)
    return o
def sph(r, loc, m, name='sph', scale=(1, 1, 1)):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=r, location=loc, segments=32, ring_count=16); o = bpy.context.object; o.name = name; o.scale = scale; setmat(o, m); return o
def torus(R, r, loc, m, rot=(0, 0, 0), name='torus', scale=(1, 1, 1)):
    bpy.ops.mesh.primitive_torus_add(major_radius=R, minor_radius=r, location=loc, rotation=rot, major_segments=64, minor_segments=16); o = bpy.context.object; o.name = name; o.scale = scale; setmat(o, m); return o
def text(s, size, loc, m, rot=(0, 0, 0), extrude=.0004, name='label'):
    bpy.ops.object.text_add(location=loc, rotation=rot); o = bpy.context.object; o.data.body = s; o.data.size = size; o.data.extrude = extrude
    o.data.align_x = 'CENTER'; o.data.align_y = 'CENTER'; bpy.ops.object.convert(target='MESH'); o.name = name; setmat(o, m); return o
def ecg_line(width, height, loc, m, rot=(0, 0, 0), beats=3, thick=.0012, name='ECG_trace'):
    pts = []; n = 60 * beats
    for i in range(n + 1):
        x = i / n; ph = (x * beats) % 1
        y = 0
        if .10 < ph < .16: y = .12 * math.sin((ph - .10) / .06 * math.pi)          # P wave
        elif .22 < ph < .24: y = -.15                                              # Q
        elif .24 <= ph < .27: y = 1.0 - abs(ph - .255) / .015 * 1.1                # R spike
        elif .27 <= ph < .29: y = -.25                                             # S
        elif .40 < ph < .52: y = .25 * math.sin((ph - .40) / .12 * math.pi)        # T wave
        pts.append(((x - .5) * width, y * height * .5, 0))
    cd = bpy.data.curves.new(name, 'CURVE'); cd.dimensions = '3D'; cd.bevel_depth = thick; cd.bevel_resolution = 2
    sp = cd.splines.new('POLY'); sp.points.add(len(pts) - 1)
    for p, c in zip(sp.points, pts): p.co = (*c, 1)
    o = bpy.data.objects.new(name, cd); bpy.context.scene.collection.objects.link(o); o.location = loc; o.rotation_euler = rot
    bpy.context.view_layer.objects.active = o; o.select_set(True); bpy.ops.object.convert(target='MESH'); setmat(o, m); return o
def heart(size, loc, m, rot=(0, 0, 0), name='heart_icon'):
    pts = [(16 * math.sin(t) ** 3 / 17, (13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t)) / 17) for t in [i / 48 * 2 * math.pi for i in range(48)]]
    me = bpy.data.meshes.new(name); bm = bmesh.new(); vs = [bm.verts.new((x * size, y * size, 0)) for x, y in pts]; f = bm.faces.new(vs)
    bmesh.ops.extrude_face_region(bm, geom=[f]); [setattr(v, 'co', v.co + Vector((0, 0, size * .15))) for v in bm.verts if v.co.z == 0 and False]
    bm.to_mesh(me); bm.free(); o = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(o)
    o.location = loc; o.rotation_euler = rot; sol = o.modifiers.new('s', 'SOLIDIFY'); sol.thickness = size * .15; setmat(o, m); return o

def export(key, title):
    objs = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    path = os.path.join(OUT, key + '.glb')
    bpy.ops.export_scene.gltf(filepath=path, export_format='GLB', use_selection=True, export_apply=True, export_cameras=False, export_lights=False)
    fm = bpy.data.objects.new('FRONT_MARKER', None); bpy.context.scene.collection.objects.link(fm); fm.location = (0, -1, 0); fm.select_set(True)
    for d in OUT_FBX:
        bpy.ops.export_scene.fbx(filepath=os.path.join(d, key + '.fbx'), use_selection=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', use_mesh_modifiers=True, mesh_smooth_type='FACE', bake_space_transform=True, object_types={'EMPTY', 'MESH'})
    tris = 0
    for o in objs:
        dg = bpy.context.evaluated_depsgraph_get(); me = o.evaluated_get(dg).to_mesh(); tris += sum(len(p.vertices) - 2 for p in me.polygons)
    rep[key] = {'title': title, 'kb': round(os.path.getsize(path) / 1024), 'tris': tris, 'objects': len(objs)}
    for o in objs: bpy.data.objects.remove(o, do_unlink=True)
    for o in [o for o in bpy.context.scene.objects]: bpy.data.objects.remove(o, do_unlink=True)

# 1) CREW BEACON — chest-worn: 2-lead ECG (heart rate + HRV), skin response, UWB location tag, comms
body = cyl(.032, .014, (0, 0, .007), WHITE, name='Beacon_Housing', bv=.003)
cyl(.026, .002, (0, 0, .0145), SCREEN, name='Beacon_Face')
torus(.0275, .0012, (0, 0, .0148), CYAN, name='Beacon_StatusRing')
heart(.006, (-.009, .004, .0156), RED, name='Beacon_Heart')
text('72', .009, (.008, .004, .0156), ECG, name='Beacon_BPM')
ecg_line(.036, .008, (0, -.008, .0156), ECG, name='Beacon_ECG', thick=.0004, beats=2)
for x in (-.05, .05):
    cyl(.012, .003, (x, 0, .0015), GEL, name='ECG_Electrode')                     # 2-lead ECG pads
    box(.04, .02, .002, (x * .55, 0, .002), STRAP, name='Strap_Link')
box(.36, .028, .0025, (0, 0, -.0005), STRAP, bv=.001, name='Chest_Strap')
cyl(.004, .002, (0, .022, .0145), GEL, name='GSR_Contact')
text('ARES', .006, (0, .016, .0152), CYAN, name='Beacon_Label')
export('crew_beacon', 'Crew beacon (chest)')

# 2) NIGHT WRISTBAND — PPG + accelerometer -> sleep stages, overnight heart rate
band = torus(.03, .007, (0, 0, 0), STRAP, rot=(math.pi / 2, 0, 0), name='Band', scale=(1, 1, 1.25))
box(.042, .03, .016, (0, 0, .04), WHITE, bv=.004, name='Pod')
box(.034, .022, .0012, (0, 0, .0482), SCREEN, name='Screen')
text('58 BPM', .0045, (0, .0055, .0492), ECG, name='HR_Text')
ecg_line(.028, .005, (0, -.0005, .0492), ECG, thick=.0003, beats=2)
text('SLEEP · DEEP', .0028, (0, -.0065, .0492), CYAN, name='Sleep_Text')
for dx in (-.004, .004): sph(.0018, (dx, 0, .0318), ECG, name='PPG_LED', scale=(1, 1, .5))
export('sleep_wristband', 'Night wristband')

# 3) CORTISOL PATCH — microneedle sweat patch, 24–48 h
box(.044, .044, .0016, (0, 0, .0008), SKIN, bv=.0006, name='Patch_Backing')
box(.024, .024, .0012, (0, 0, .002), WHITE, bv=.0004, name='Reader_Window')
cyl(.004, .0004, (0, 0, .0027), AMBER, name='Colour_Indicator', v=32)
for i in range(7):
    for j in range(7):
        bpy.ops.mesh.primitive_cone_add(radius1=.0006, depth=.0016, location=(-.009 + i * .003, -.009 + j * .003, -.0008), rotation=(math.pi, 0, 0), vertices=8)
        setmat(bpy.context.object, ALU)
text('CORTISOL', .0035, (0, -.016, .0018), GREY, name='Patch_Label')
export('cortisol_patch', 'Cortisol patch')

# 4) ZONE SENSOR POD — ceiling: paired NDIR CO2 + humidity, mic array, camera (shared zones), UWB anchor
cyl(.14, .045, (0, 0, 0), WHITE, name='Pod_Body', v=64, bv=.006)          # shown face-up; on the ceiling it faces down
cyl(.10, .004, (0, 0, .0235), GREY, name='Pod_Face', v=64)
sph(.028, (0, 0, .026), GLASS, name='Camera_Dome', scale=(1, 1, .7))
torus(.118, .003, (0, 0, .022), ECG, name='Status_Ring')
for k in range(6):
    a = k / 6 * 2 * math.pi; cyl(.004, .003, (math.cos(a) * .07, math.sin(a) * .07, .026), GLASS, name='Mic_Port', v=16)
for k, x in enumerate((-.06, .06)):
    yy = .045 * (1 if k else -1)
    box(.04, .026, .012, (x, yy, .031), ALU, bv=.002, name=f'CO2_Sensor_{"AB"[k]}')
    for t in range(4): box(.032, .002, .002, (x, yy - .0075 + t * .005, .0375), GREY, name='Grille_Slot')
box(.03, .03, .012, (0, .11, -.03), GREY, bv=.002, name='UWB_Anchor')
text('CO2 612 ppm', .009, (0, -.09, .0262), CYAN, name='CO2_Readout')
export('zone_sensor_pod', 'Zone sensor pod')

# 5) CREW HEALTH STATION — med-bay vitals kiosk: ECG, blood pressure, SpO2, temperature, private check-in
box(.5, .4, .06, (0, 0, .03), ALU, bv=.01, name='Base_Plate')
cyl(.05, 1.0, (0, 0, .55), WHITE, name='Column', bv=.01)
scr = box(.62, .04, .42, (0, 0, 1.2), WHITE, bv=.015, name='Screen_Bezel'); scr.rotation_euler = (math.radians(-12), 0, 0)
pan = box(.56, .004, .36, (0, -.022, 1.2), SCREEN, name='Screen_Panel'); pan.rotation_euler = (math.radians(-12), 0, 0)
rot = (math.radians(78), 0, 0)
ecg_line(.5, .12, (0, -.0135, 1.27), ECG, rot=rot, thick=.0025, beats=4, name='Live_ECG')
heart(.03, (-.22, -.0415, 1.14), RED, rot=rot, name='HR_Heart')
text('HR 72', .045, (-.1, -.0415, 1.14), ECG, rot=rot, name='HR_Value')
text('SpO2 98%', .04, (.12, -.0415, 1.14), CYAN, rot=rot, name='SpO2_Value')
text('BP 118/76   TEMP 36.8°C', .028, (0, -.056, 1.07), WHITE, rot=rot, name='BP_Temp')
text('CREW HEALTH STATION', .03, (0, -.007, 1.35), CYAN, rot=rot, name='Title')
cyl(.05, .16, (.36, -.06, .95), STRAP, rot=(0, math.pi / 2, 0), name='BP_Cuff')
torus(.05, .006, (.36, -.06, .95), ALU, rot=(0, math.pi / 2, 0), name='Cuff_Ring')
box(.06, .04, .03, (-.36, -.06, .98), WHITE, bv=.008, name='Pulse_Oximeter'); cyl(.012, .005, (-.36, -.083, .98), RED, rot=(math.pi / 2, 0, 0), name='Oximeter_LED')
box(.12, .08, .03, (0, -.16, .92), WHITE, bv=.01, name='Thermometer_Tray')
cyl(.18, .04, (0, -.55, .5), STRAP, name='Stool_Seat', bv=.01); cyl(.025, .48, (0, -.55, .25), ALU, name='Stool_Post'); cyl(.16, .02, (0, -.55, .01), ALU, name='Stool_Foot')
export('health_station', 'Crew Health Station')

# 6) SMART SLEEP BUNK — under-mattress sleep sensor strip, circadian mood light, private curtain rail
box(2.0, .9, .3, (0, 0, .15), WHITE, bv=.02, name='Bunk_Frame')
box(1.9, .82, .14, (0, 0, .37), FOAM, bv=.04, name='Mattress')
box(1.4, .5, .006, (0, 0, .303), CYAN, name='Sleep_Sensor_Strip')
box(.5, .7, .12, (-.7, 0, .5), WHITE, bv=.05, name='Pillow')
box(2.0, .04, .02, (0, .43, 1.4), BLUE, name='Mood_Light_Strip')
box(.04, .04, 1.4, (-.98, .43, .7), ALU, name='Post_L'); box(.04, .04, 1.4, (.98, .43, .7), ALU, name='Post_R')
box(2.0, .04, .04, (0, -.45, 1.4), ALU, name='Curtain_Rail'); box(.04, .04, 1.4, (-.98, -.45, .7), ALU, name='Post_FL'); box(.04, .04, 1.4, (.98, -.45, .7), ALU, name='Post_FR')
p = box(.3, .02, .2, (.85, .44, 1.0), SCREEN, name='Bunk_Panel'); text('SLEEP 7h 40m', .03, (.85, .428, 1.03), ECG, rot=(math.pi / 2, 0, 0), name='Sleep_Readout')
text('DEEP 22%  REM 21%', .022, (.85, .428, .97), CYAN, rot=(math.pi / 2, 0, 0), name='Stage_Readout')
export('sleep_bunk', 'Smart sleep bunk')

json.dump(rep, open(os.path.join(OUT, 'health_report.json'), 'w'), indent=1)
print('HEALTH DONE', rep)