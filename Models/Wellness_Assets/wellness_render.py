# Renders the wellness asset pack as product shots (one hero lineup + one per device). Never saves.
import bpy, os, math
from mathutils import Vector
SRC = r"C:\Users\jaine\Downloads\Nasa\Moon_Base_Final\HunchBridge\site\models\wellness"
OUT = r"C:\Users\jaine\Downloads\Nasa\Moon_Base_Final\HunchBridge\figma"
os.makedirs(OUT, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
try: sc.render.engine = 'BLENDER_EEVEE_NEXT'
except Exception: sc.render.engine = 'BLENDER_EEVEE'
sc.render.resolution_x, sc.render.resolution_y = 1600, 1200
sc.render.film_transparent = False
w = bpy.data.worlds.new('w'); sc.world = w; w.use_nodes = True
bg = w.node_tree.nodes['Background']; bg.inputs[0].default_value = (0.012, 0.018, 0.045, 1); bg.inputs[1].default_value = 1.0
try: sc.eevee.use_bloom = True
except Exception: pass
sc.view_settings.view_transform = 'AgX' if 'AgX' in [i.identifier for i in sc.view_settings.bl_rna.properties['view_transform'].enum_items] else 'Filmic'
def light(name, loc, energy, size=2, col=(1, 1, 1)):
    d = bpy.data.lights.new(name, 'AREA'); d.energy = energy; d.size = size; d.color = col
    o = bpy.data.objects.new(name, d); sc.collection.objects.link(o); o.location = loc
    o.rotation_euler = (Vector((0, 0, 0)) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler(); return o
cam_d = bpy.data.cameras.new('cam'); cam = bpy.data.objects.new('cam', cam_d); sc.collection.objects.link(cam); sc.camera = cam
items = ['recovery_pod_healthy','recovery_pod_fatigue','recovery_pod_stress','ai_wristband','sanctuary_pod','greenhouse','medical_scanner','hologram_companion','circadian_light','emotional_garden','eclss_core','maintenance_drone','habitat_console','emergency_mh_station','sensor_wall','earth_window','stress_lounge','oxygen_tanks','ai_health_hub','mood_tracker','heart_rate_monitor','bp_monitor','smart_bed']
done = []
for key in items:
    for o in list(sc.objects):
        if o.type not in ('CAMERA',): bpy.data.objects.remove(o, do_unlink=True)
    bpy.ops.import_scene.gltf(filepath=os.path.join(SRC, key + '.glb'))
    objs = [o for o in sc.objects if o.type == 'MESH']
    mn = Vector((1e9,) * 3); mx = -mn
    for o in objs:
        for c in o.bound_box:
            p = o.matrix_world @ Vector(c); mn = Vector(map(min, mn, p)); mx = Vector(map(max, mx, p))
    cen = (mn + mx) / 2; r = (mx - mn).length / 2
    # floor disc
    bpy.ops.mesh.primitive_cylinder_add(radius=r * 3, depth=r * .02, location=(cen.x, cen.y, mn.z - r * .011), vertices=96)
    fl = bpy.context.object; m = bpy.data.materials.new('floor'); m.use_nodes = True
    b = m.node_tree.nodes['Principled BSDF']; b.inputs['Base Color'].default_value = (.03, .05, .1, 1); b.inputs['Roughness'].default_value = .35; fl.data.materials.append(m)
    light('key', cen + Vector((r * 2.5, -r * 2.5, r * 3)), 420 * r * r, size=r * 2)
    light('rim', cen + Vector((-r * 3, r * 2.5, r * 2)), 300 * r * r, size=r * 2, col=(.45, .75, 1))
    light('fill', cen + Vector((-r * 2, -r * 3, r)), 110 * r * r, size=r * 3)
    cam_d.lens = 60
    cam.location = cen + Vector((r * 2.1, -r * 2.6, r * 1.7))
    cam.rotation_euler = (cen - cam.location).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = os.path.join(OUT, 'wellness_' + key + '.png'); bpy.ops.render.render(write_still=True); done.append(key)
print("RENDER DONE", done); result = {"done": done}