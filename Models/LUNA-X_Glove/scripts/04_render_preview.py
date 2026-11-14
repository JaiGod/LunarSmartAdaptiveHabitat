"""LUNA-X glove - low-settings preview MP4s (does NOT save the .blend).
Usage: blender --background --factory-startup <glove.blend> --python 04_render_preview.py -- <glove_dir>
"""
import bpy, sys, os

ARGS = sys.argv[sys.argv.index("--") + 1:]
GLOVE_DIR = ARGS[0]
OUT = os.path.join(GLOVE_DIR, "renders")


def low(sc, name):
    r = sc.render
    r.resolution_x, r.resolution_y, r.resolution_percentage = 960, 540, 100
    ee = sc.eevee
    for attr, val in (("taa_render_samples", 8), ("use_raytracing", False), ("use_shadows", True)):
        if hasattr(ee, attr):
            setattr(ee, attr, val)
    ims = r.image_settings
    if hasattr(ims, "media_type"):
        ims.media_type = 'VIDEO'
    ims.file_format = 'FFMPEG'
    r.ffmpeg.format = 'MPEG4'
    r.ffmpeg.codec = 'H264'
    r.ffmpeg.constant_rate_factor = 'MEDIUM'
    r.ffmpeg.ffmpeg_preset = 'REALTIME'
    r.filepath = os.path.join(OUT, name + "_")
    r.use_file_extension = True


jobs = [("LUNA-X Glove", "LUNA-X_Glove_Showcase_PREVIEW"), ("LUNA-X Layers", "LUNA-X_Glove_Layers_PREVIEW")]
for scene_name, out_name in jobs:
    sc = bpy.data.scenes[scene_name]
    low(sc, out_name)
    vl = sc.view_layers[0]
    with bpy.context.temp_override(scene=sc, view_layer=vl):
        sc.frame_set(sc.frame_start)
        bpy.ops.render.render(animation=True, scene=sc.name)
    print("[LX] rendered", scene_name, flush=True)
print("[LX] PREVIEW DONE", flush=True)
