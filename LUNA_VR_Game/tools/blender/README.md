# LUNA VR → Blender

Builds every model of LUNA VR as a native Blender scene, **straight from the game's own model code**, so the Blender file and the game always match. That covers each part, label, live screen and room light. Each part is named after the comment in `scripts/models.gd` that explains it ("HEPA vacuum", "Water wall", "Live 3D twin"). Each model becomes one collection carrying the four answers from the [model manifest](../../../Docs/LUNA_VR/MODEL_MANIFEST.md) as custom properties: NASA requirement, gameplay purpose, how astronauts interact, and why it's necessary.

1. **Record the build.** Copy `LUNA_VR_Game` to a scratch folder whose name contains `luna_rec`, then run `python patch_rec.py <that folder>`. This only instruments the copy.
2. **Run it once in record mode:**
   `godot --path <luna_rec> -d -- --blender=<dump folder>`
   `-d` turns on call stacks, which is how every part finds its name in the code. The game starts, captures the 15 LUNA screens at 08:34 on day 1, writes `luna_vr_scene.json` and quits.
3. **Convert:** `python convert.py <dump folder> <kit>/luna_vr_blender_data.json`. Then copy the textures (`assets/tex/*.png`), the `screen_*.png` files from the dump and `assets/fonts/DejaVuSans.ttf` into `<kit>`.
4. **Build in Blender** (background, never touches an open Blender window):
   `blender --background --factory-startup --python build_luna_vr_blend.py -- <kit> <out>.blend`

Walls are cut at 1.2 m so you can see into every room. The full-height walls, ceilings and the regolith roof are in hidden collections.
