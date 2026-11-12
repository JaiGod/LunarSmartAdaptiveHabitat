"""Realistic habitat — the ring bases, hallways and lunar regolith protection as SEPARATE pieces laid out on one
plate (one STL). Nothing inside (solid outer shapes), no sleep domes, no core/modules.
usage: python3 realistic_parts_plate.py <habitat.fbx> <out_dir>
© 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
"""
import sys, os, re, json
import numpy as np
import fbxread, kitlib as K
from realistic_shell_stl import tris, weld

P3 = os.path.join(os.path.dirname(__file__), '..', 'Project3_Moon_Base_Final_unity3', 'closed')
BASES = ["06_Node_Command", "07_Node_Maintenance", "08_Node_Medical_StormShelter", "09_Node_Recreation_Commons", "10_Node_CrewSupport_Hygiene2", "11_Node_Quarters_Vestibule"]
HALLS = ["16_Hallway_N01_B_to_N", "17_Hallway_H01_D_to_H", "18_Hallway_E01_C_to_E", "19_Hallway_L1_N_to_R", "20_Hallway_L2_R_to_E", "21_Hallway_L3_E_to_G", "22_Hallway_L4_G_to_H", "23_Hallway_K_H_to_K"]
SCALE = 150.0; GAP = 6.0

def read_stl(p):
    d = np.fromfile(p, dtype=[('n', '<f4', 3), ('v', '<f4', (3, 3)), ('a', '<u2')], offset=84)
    v = d['v'].reshape(-1, 3).astype(float); F = np.arange(len(v)).reshape(-1, 3)
    return weld(v, F, 1e-4)

def main(fbx, out):
    pieces = []
    for n in BASES: pieces.append(("base", n, *read_stl(os.path.join(P3, n + ".stl"))))
    for n in HALLS: pieces.append(("hallway", n, *read_stl(os.path.join(P3, n + ".stl"))))
    objs, _ = fbxread.scene(fbx)
    for o in sorted(objs, key=lambda o: o['name']):
        if 'PrintedRegolith' not in o['name']: continue
        V, F = weld(o['verts'].copy(), tris(o['poly']))
        V = np.c_[V[:, 0], -V[:, 2], V[:, 1]] * (1000.0 / SCALE)
        pieces.append(("regolith shield", o['name'], V, F))
    # shelf packing, parts sorted by group then height of footprint
    placed, x, y, row_h, W = [], 0.0, 0.0, 0.0, 300.0
    for g, n, V, F in pieces:
        V = V - V.min(0); sx, sy = V[:, 0].max(), V[:, 1].max()
        if sx > sy and sx > W * 0.5: pass
        if x + sx > W: x = 0.0; y += row_h + GAP; row_h = 0.0
        placed.append((g, n, V + np.array([x, y, 0.0]), F)); x += sx + GAP; row_h = max(row_h, sy)
    V, F = K.merge(*[(v, f) for _, _, v, f in placed])
    os.makedirs(out, exist_ok=True); path = os.path.join(out, "Lunar_Habitat_Realistic_Bases_Hallways_RegolithShield_SEPARATE_PARTS_1-150.stl")
    K.write_stl(path, V, F, "Lunar habitat - 6 bases, 8 hallways, regolith shield - separate parts 1:150")
    size = V.max(0) - V.min(0)
    info = {"file": os.path.basename(path), "scale": "1:150", "plate_mm": [round(float(s), 1) for s in size],
            "parts": [{"group": g, "name": n, "size_mm": [round(float(s), 1) for s in (v.max(0) - v.min(0))]} for g, n, v, _ in placed]}
    json.dump(info, open(os.path.join(out, "parts.json"), "w"), indent=1)
    print(info["file"], info["plate_mm"], len(placed), "parts")

if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2])
