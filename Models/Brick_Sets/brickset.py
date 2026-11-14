# brickset.py - turns a Blender model into a brick-built set.
# Run:  blender --background --factory-startup --python brickset.py -- <config.json> <stage>
# stage: extract | build | all
# The source model is COPIED to a temp folder and only the copy is opened (read-only use).
# The set itself is built in a brand-new blank file (read_factory_settings(use_empty=True)).
import bpy, sys, os, json, math, time, shutil, re, traceback, random
import numpy as np
from mathutils import Vector, Matrix, Euler

ARGS = sys.argv[sys.argv.index('--') + 1:]
CFG = json.load(open(ARGS[0], encoding='utf-8'))
STAGE = ARGS[1] if len(ARGS) > 1 else 'all'
WORK = CFG['work']
OUT = CFG['out_dir']
os.makedirs(WORK, exist_ok=True)
os.makedirs(OUT, exist_ok=True)
LOGF = open(os.path.join(WORK, 'log.txt'), 'a', encoding='utf-8')


def log(*a):
    s = time.strftime('%H:%M:%S ') + ' '.join(str(x) for x in a)
    LOGF.write(s + '\n')
    LOGF.flush()


# ----------------------------------------------------------------------------------------------
# Palette (generic brick colour names with typical sRGB values)
# ----------------------------------------------------------------------------------------------
PALETTE = [
    ("White", (242, 243, 242), 's'), ("Light Bluish Gray", (160, 165, 169), 's'),
    ("Dark Bluish Gray", (108, 110, 104), 's'), ("Black", (33, 33, 33), 's'),
    ("Red", (196, 40, 27), 's'), ("Dark Red", (123, 46, 47), 's'),
    ("Orange", (254, 138, 24), 's'), ("Bright Light Orange", (248, 187, 61), 's'),
    ("Yellow", (245, 205, 47), 's'), ("Bright Light Yellow", (255, 236, 108), 's'),
    ("Tan", (228, 205, 158), 's'), ("Dark Tan", (149, 138, 115), 's'),
    ("Medium Nougat", (170, 125, 85), 's'), ("Reddish Brown", (105, 64, 39), 's'),
    ("Dark Brown", (53, 33, 0), 's'), ("Green", (40, 127, 70), 's'),
    ("Bright Green", (75, 151, 74), 's'), ("Lime", (187, 233, 11), 's'),
    ("Dark Green", (24, 70, 50), 's'), ("Olive Green", (155, 154, 90), 's'),
    ("Sand Green", (160, 188, 172), 's'), ("Blue", (13, 105, 171), 's'),
    ("Dark Blue", (10, 52, 99), 's'), ("Medium Blue", (90, 147, 219), 's'),
    ("Bright Light Blue", (159, 195, 233), 's'), ("Medium Azure", (54, 174, 191), 's'),
    ("Dark Azure", (7, 139, 201), 's'), ("Sand Blue", (96, 116, 161), 's'),
    ("Dark Turquoise", (0, 143, 155), 's'), ("Magenta", (146, 57, 120), 's'),
    ("Dark Pink", (200, 112, 160), 's'), ("Bright Pink", (228, 173, 200), 's'),
    ("Dark Purple", (63, 54, 145), 's'), ("Medium Lavender", (172, 120, 186), 's'),
    ("Lavender", (225, 213, 237), 's'),
    ("Pearl Gold", (170, 127, 46), 'm'), ("Flat Silver", (137, 135, 136), 'm'),
    ("Trans-Clear", (236, 240, 242), 't'), ("Trans-Light Blue", (174, 233, 239), 't'),
    ("Trans-Medium Blue", (85, 154, 183), 't'), ("Trans-Dark Blue", (0, 32, 160), 't'),
    ("Trans-Red", (201, 26, 9), 't'), ("Trans-Orange", (240, 143, 28), 't'),
    ("Trans-Yellow", (245, 205, 47), 't'), ("Trans-Green", (35, 120, 65), 't'),
    ("Trans-Bright Green", (86, 190, 86), 't'), ("Trans-Neon Green", (248, 241, 132), 't'),
    ("Trans-Purple", (165, 165, 203), 't'), ("Trans-Dark Pink", (223, 102, 149), 't'),
    ("Trans-Black", (99, 95, 82), 't'),
]
PNAME = [p[0] for p in PALETTE]
PRGB = np.array([p[1] for p in PALETTE], np.float32) / 255.0
PKIND = [p[2] for p in PALETTE]
NP_ = len(PALETTE)
PIDX = {n: i for i, n in enumerate(PNAME)}


def lin2srgb(c):
    c = np.clip(np.asarray(c, np.float32), 0, 1)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * np.power(c, 1 / 2.4) - 0.055)


def srgb2lin(c):
    c = np.clip(np.asarray(c, np.float32), 0, 1)
    return np.where(c <= 0.04045, c / 12.92, np.power((c + 0.055) / 1.055, 2.4))


def srgb_to_lab(rgb):
    c = srgb2lin(rgb)
    M = np.array([[0.4124, 0.3576, 0.1805], [0.2126, 0.7152, 0.0722], [0.0193, 0.1192, 0.9505]], np.float32)
    xyz = c @ M.T / np.array([0.95047, 1.0, 1.08883], np.float32)
    f = np.where(xyz > 0.008856, np.cbrt(np.maximum(xyz, 0)), 7.787 * xyz + 16 / 116)
    L = 116 * f[..., 1] - 16
    a = 500 * (f[..., 0] - f[..., 1])
    b = 200 * (f[..., 1] - f[..., 2])
    return np.stack([L, a, b], -1)


PLAB = srgb_to_lab(PRGB)
SOLID_IDX = np.array([i for i, k in enumerate(PKIND) if k == 's'])
METAL_IDX = np.array([i for i, k in enumerate(PKIND) if k == 'm'])
TRANS_IDX = np.array([i for i, k in enumerate(PKIND) if k == 't'])


def nearest(lab, cand):
    d = ((lab[:, None, :] - PLAB[cand][None, :, :]) ** 2)
    d[..., 0] *= 0.8   # lightness slightly less important than hue
    return cand[np.argmin(d.sum(-1), axis=1)]


# ----------------------------------------------------------------------------------------------
# STAGE 1: extract triangles + colours from a COPY of the source model
# ----------------------------------------------------------------------------------------------
IMG_CACHE = {}


def img_array(img):
    if img is None:
        return None
    key = img.name
    if key in IMG_CACHE:
        return IMG_CACHE[key]
    arr = None
    try:
        w, h = img.size
        if w > 0 and h > 0:
            im = img.copy()
            tw, th = min(w, 384), min(h, 384)
            if (tw, th) != (w, h):
                im.scale(tw, th)
            px = np.empty(tw * th * 4, np.float32)
            im.pixels.foreach_get(px)
            arr = px.reshape(th, tw, 4)[:, :, :3].copy()
            cs = img.colorspace_settings.name.lower()
            if img.is_float or 'linear' in cs or 'non-color' in cs or 'raw' in cs:
                arr = lin2srgb(arr)
            bpy.data.images.remove(im)
    except Exception as e:
        log('img fail', img.name, repr(e)[:120])
        arr = None
    IMG_CACHE[key] = arr
    return arr


def find_input(node, names):
    for nm in names:
        s = node.inputs.get(nm)
        if s is not None:
            return s
    return None


def eval_color(sock, depth=0):
    """Rough evaluation of a colour socket. Returns ('C', srgb array) | ('IMG', node) | ('VCOL', name) | None"""
    if sock is None or depth > 7:
        return None
    if not sock.is_linked:
        v = sock.default_value
        try:
            return ('C', lin2srgb(np.array(v[:3], np.float32)))
        except TypeError:
            return ('C', lin2srgb(np.array([v, v, v], np.float32)))
    lk = sock.links[0]
    n = lk.from_node
    t = n.type
    if t == 'RGB':
        return ('C', lin2srgb(np.array(lk.from_socket.default_value[:3], np.float32)))
    if t == 'TEX_IMAGE':
        return ('IMG', n)
    if t == 'VALTORGB':
        els = n.color_ramp.elements
        return ('C', lin2srgb(np.mean([e.color[:3] for e in els], axis=0)))
    if t in ('VERTEX_COLOR', 'ATTRIBUTE'):
        nm = getattr(n, 'layer_name', '') or getattr(n, 'attribute_name', '')
        return ('VCOL', nm)
    if t in ('MIX_RGB', 'MIX'):
        if t == 'MIX':
            a = next((s for s in n.inputs if s.identifier == 'A_Color'), None)
            b = next((s for s in n.inputs if s.identifier == 'B_Color'), None)
            f = next((s for s in n.inputs if s.identifier == 'Factor_Float'), None)
        else:
            a, b, f = n.inputs.get('Color1'), n.inputs.get('Color2'), n.inputs.get('Fac')
        fac = 0.5 if (f is None or f.is_linked) else float(f.default_value)
        ra, rb = eval_color(a, depth + 1), eval_color(b, depth + 1)
        if ra and ra[0] == 'IMG' and fac < 0.7:
            return ra
        if rb and rb[0] == 'IMG' and fac >= 0.3:
            return rb
        if ra and rb and ra[0] == 'C' and rb[0] == 'C':
            blend = getattr(n, 'blend_type', 'MIX')
            if blend == 'MULTIPLY':
                return ('C', ra[1] * (1 - fac) + ra[1] * rb[1] * fac)
            return ('C', ra[1] * (1 - fac) + rb[1] * fac)
        return ra if ra and ra[0] != 'IMG' else (rb if rb and rb[0] != 'IMG' else (ra or rb))
    if t in ('HUE_SAT', 'BRIGHTCONTRAST', 'GAMMA', 'CURVE_RGB', 'INVERT', 'SEPARATE_COLOR', 'COMBINE_COLOR'):
        s = find_input(n, ['Color', 'Image'])
        r = eval_color(s, depth + 1)
        if r and r[0] == 'C' and t == 'INVERT':
            return ('C', 1 - r[1])
        return r
    if t == 'GROUP':
        s = find_input(n, ['Base Color', 'Color', 'Albedo', 'Colour'])
        return eval_color(s, depth + 1) if s is not None else None
    return None


def shader_list(mat):
    out = []
    nt = mat.node_tree
    o = next((n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL' and n.is_active_output), None)
    if o is None:
        o = next((n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL'), None)
    if o is None:
        return out

    def walk(sock, w, d):
        if d > 10 or sock is None or not sock.is_linked:
            return
        n = sock.links[0].from_node
        if n.type == 'MIX_SHADER':
            fs = n.inputs[0]
            fac = 0.5 if fs.is_linked else float(fs.default_value)
            walk(n.inputs[1], w * (1 - fac), d + 1)
            walk(n.inputs[2], w * fac, d + 1)
        elif n.type == 'ADD_SHADER':
            walk(n.inputs[0], w, d + 1)
            walk(n.inputs[1], w, d + 1)
        else:
            out.append((n, w))
    walk(o.inputs.get('Surface'), 1.0, 0)
    return out


GLASS_RE = re.compile(r'(glass|window|visor|lens|acrylic|transparent|canopy|pane\b|windscreen|porthole_glass|dome_glass|cupola_glass)', re.I)
NOTGLASS_RE = re.compile(r'(frame|rim|trim|seal|gasket|mullion|strut|rib)', re.I)


def material_info(mat, obname=''):
    inf = {'col': np.array([0.7, 0.7, 0.7], np.float32), 'kind': 0, 'img': None, 'vcol': None, 'uv': None}
    if mat is None:
        if GLASS_RE.search(obname) and not NOTGLASS_RE.search(obname):
            inf['kind'] = 1
        return inf
    nm = mat.name
    col = None
    glass_w = 0.0
    emit_s = 0.0
    emit_col = None
    metallic = 0.0
    alpha = 1.0
    if mat.use_nodes and mat.node_tree:
        shs = shader_list(mat)
        best = None
        for n, w in shs:
            t = n.type
            if t == 'BSDF_PRINCIPLED':
                r = eval_color(n.inputs.get('Base Color'))
                a = n.inputs.get('Alpha')
                alpha = float(a.default_value) if (a is not None and not a.is_linked) else 1.0
                tw = n.inputs.get('Transmission Weight') or n.inputs.get('Transmission')
                tr = float(tw.default_value) if (tw is not None and not tw.is_linked) else 0.0
                mt = n.inputs.get('Metallic')
                metallic = float(mt.default_value) if (mt is not None and not mt.is_linked) else 0.0
                es = n.inputs.get('Emission Strength')
                ec = n.inputs.get('Emission Color') or n.inputs.get('Emission')
                s = float(es.default_value) if (es is not None and not es.is_linked) else 0.0
                ce = eval_color(ec) if ec is not None else None
                if ce and ce[0] == 'C':
                    lum = float(np.dot(ce[1], [0.2126, 0.7152, 0.0722]))
                    if s * lum > 0.35:
                        emit_s += w * s
                        emit_col = ce[1]
                elif ce and ce[0] == 'IMG' and s > 0.5:
                    emit_s += w * s
                if tr > 0.45 or alpha < 0.6:
                    glass_w += w
                if best is None or w > best[1]:
                    best = (r, w)
            elif t in ('BSDF_DIFFUSE', 'BSDF_GLOSSY', 'BSDF_ANISOTROPIC', 'SUBSURFACE_SCATTERING', 'BSDF_TOON', 'BSDF_SHEEN', 'BSDF_METALLIC'):
                r = eval_color(n.inputs.get('Color') or n.inputs.get('Base Color'))
                if t in ('BSDF_GLOSSY', 'BSDF_ANISOTROPIC', 'BSDF_METALLIC'):
                    metallic = max(metallic, 0.7 * w)
                if best is None or w > best[1]:
                    best = (r, w)
            elif t in ('BSDF_GLASS', 'BSDF_REFRACTION', 'BSDF_TRANSPARENT'):
                glass_w += w
                r = eval_color(n.inputs.get('Color'))
                if best is None or w > best[1]:
                    best = (r, w)
            elif t == 'EMISSION':
                ce = eval_color(n.inputs.get('Color'))
                st = n.inputs.get('Strength')
                s = float(st.default_value) if (st is not None and not st.is_linked) else 1.0
                emit_s += w * s
                if ce and ce[0] == 'C':
                    emit_col = ce[1]
                if best is None or w > best[1]:
                    best = (ce, w * 0.9)
            elif t == 'GROUP':
                r = eval_color(find_input(n, ['Base Color', 'Color', 'Albedo']))
                if best is None or w > best[1]:
                    best = (r, w)
        if best is not None:
            col = best[0]
    if col is None or col[0] == 'C' and col is None:
        col = ('C', lin2srgb(np.array(mat.diffuse_color[:3], np.float32)))
    if col[0] == 'C':
        inf['col'] = np.asarray(col[1], np.float32)
    elif col[0] == 'IMG':
        n = col[1]
        arr = img_array(n.image)
        if arr is not None:
            inf['img'] = arr
            inf['col'] = arr.reshape(-1, 3).mean(0)
            vec = n.inputs.get('Vector')
            if vec is not None and vec.is_linked and vec.links[0].from_node.type == 'UVMAP':
                inf['uv'] = vec.links[0].from_node.uv_map
        else:
            inf['col'] = lin2srgb(np.array(mat.diffuse_color[:3], np.float32))
    elif col[0] == 'VCOL':
        inf['vcol'] = col[1] or ''
        inf['col'] = lin2srgb(np.array(mat.diffuse_color[:3], np.float32))
    if not mat.use_nodes:
        inf['col'] = lin2srgb(np.array(mat.diffuse_color[:3], np.float32))
        if mat.diffuse_color[3] < 0.6:
            glass_w = 1.0
    nl = nm.lower() + ' ' + obname.lower()
    if glass_w >= 0.45 or (GLASS_RE.search(nl) and not NOTGLASS_RE.search(nl)):
        inf['kind'] = 1
    elif emit_s >= 1.0:
        inf['kind'] = 2
        if emit_col is not None and float(np.max(emit_col)) > 0.05:
            inf['col'] = np.asarray(emit_col, np.float32)
    elif metallic >= 0.6:
        inf['kind'] = 3
    return inf


def chain_of(collection_parents, ob):
    chains = []
    for c in ob.users_collection:
        ch = [c.name]
        p = collection_parents.get(c.name)
        while p is not None:
            ch.append(p)
            p = collection_parents.get(p)
        chains.append(list(reversed(ch)))
    return chains[0] if chains else []


def rx_any(pats, s):
    return any(re.search(p, s, re.I) for p in pats)


def stage_extract():
    t0 = time.time()
    src = CFG['src']
    m0 = os.path.getmtime(src)
    ext = os.path.splitext(src)[1].lower()
    cp = os.path.join(WORK, 'SRC_COPY' + ext)
    shutil.copy2(src, cp)
    if ext == '.blend':
        bpy.ops.wm.open_mainfile(filepath=cp, load_ui=False)
    else:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        if ext == '.fbx':
            bpy.ops.import_scene.fbx(filepath=cp)
        elif ext in ('.glb', '.gltf'):
            bpy.ops.import_scene.gltf(filepath=cp)
        elif ext == '.obj':
            bpy.ops.wm.obj_import(filepath=cp)
    log('loaded', src, round(time.time() - t0, 1), 's')
    sc = bpy.context.scene
    parents = {}

    def walkc(c):
        for ch in c.children:
            parents[ch.name] = c.name if c != sc.collection else None
            walkc(ch)
    walkc(sc.collection)
    R = np.array(Euler([math.radians(a) for a in CFG.get('rotate', [0, 0, 0])]).to_matrix(), np.float32)
    exc_c = CFG.get('exclude_cols', [])
    exc_o = CFG.get('exclude_objs', [])
    inc_c = CFG.get('include_cols', [])
    inc_o = CFG.get('include_objs', [])
    grp_rx = CFG.get('group_regex', [])
    glevel = CFG.get('group_level', 0)
    force = CFG.get('force', [])  # [ [regex, palette name] ]
    crop = CFG.get('crop')  # [x0,x1,y0,y1,z0,z1] after rotation
    dg = bpy.context.evaluated_depsgraph_get()
    Ts, Cs, Ks, Gs, Os = [], [], [], [], []
    groups, gidx = [], {}
    objnames, oidx = [], {}
    mcache = {}
    meshcache = {}
    nobj = 0
    for inst in dg.object_instances:
        ob = inst.object
        if ob.type not in ('MESH', 'CURVE', 'SURFACE', 'META', 'FONT'):
            continue
        if ob.type == 'FONT' and not CFG.get('keep_text', False):
            continue
        orig = ob.original
        holder = inst.parent.original if (inst.is_instance and inst.parent is not None) else orig
        if orig.hide_render or holder.hide_render:
            continue
        name = orig.name
        ch = chain_of(parents, holder)
        chs = ' / '.join(ch)
        if exc_c and any(rx_any(exc_c, c) for c in ch):
            continue
        if exc_o and (rx_any(exc_o, name) or rx_any(exc_o, holder.name)):
            continue
        if inc_c and not any(rx_any(inc_c, c) for c in ch):
            continue
        if inc_o and not (rx_any(inc_o, name) or rx_any(inc_o, holder.name)):
            continue
        # group
        g = None
        for pat, gname in grp_rx:
            if re.search(pat, name, re.I) or re.search(pat, holder.name, re.I) or re.search(pat, chs, re.I):
                g = gname
                break
        if g is None:
            g = ch[min(glevel, len(ch) - 1)] if ch else 'Model'
        if g not in gidx:
            gidx[g] = len(groups)
            groups.append(g)
        key = (orig.name, ob.data.name if ob.data else '')
        if key in meshcache:
            co, tv, tl, tm, uvs, vcols = meshcache[key]
        else:
            try:
                me = ob.to_mesh()
            except Exception:
                continue
            if me is None or len(me.polygons) == 0:
                try:
                    ob.to_mesh_clear()
                except Exception:
                    pass
                continue
            me.calc_loop_triangles()
            nv = len(me.vertices)
            co = np.empty(nv * 3, np.float32)
            me.vertices.foreach_get('co', co)
            co = co.reshape(-1, 3)
            nt = len(me.loop_triangles)
            tv = np.empty(nt * 3, np.int32)
            me.loop_triangles.foreach_get('vertices', tv)
            tv = tv.reshape(-1, 3)
            tl = np.empty(nt * 3, np.int32)
            me.loop_triangles.foreach_get('loops', tl)
            tl = tl.reshape(-1, 3)
            tm = np.empty(nt, np.int32)
            me.loop_triangles.foreach_get('material_index', tm)
            uvs = {}
            for uvl in me.uv_layers:
                a = np.empty(len(me.loops) * 2, np.float32)
                uvl.data.foreach_get('uv', a)
                uvs[uvl.name] = (a.reshape(-1, 2), uvl.active_render)
            vcols = {}
            for ca in me.color_attributes:
                try:
                    a = np.empty(len(ca.data) * 4, np.float32)
                    ca.data.foreach_get('color_srgb', a)
                    vcols[ca.name] = (a.reshape(-1, 4)[:, :3], ca.domain)
                except Exception:
                    pass
            ob.to_mesh_clear()
            meshcache[key] = (co, tv, tl, tm, uvs, vcols)
        if len(tv) == 0:
            continue
        M = np.array(inst.matrix_world, np.float32)
        cw = co @ M[:3, :3].T + M[:3, 3]
        cw = cw @ R.T
        tri = cw[tv]
        if crop:
            cen = tri.mean(1)
            keep = (cen[:, 0] >= crop[0]) & (cen[:, 0] <= crop[1]) & (cen[:, 1] >= crop[2]) & (cen[:, 1] <= crop[3]) & (cen[:, 2] >= crop[4]) & (cen[:, 2] <= crop[5])
            if not keep.any():
                continue
        else:
            keep = np.ones(len(tri), bool)
        # colours per triangle
        nt = len(tri)
        col = np.zeros((nt, 3), np.float32)
        kind = np.zeros(nt, np.int8)
        slots = [s.material for s in ob.material_slots]
        mi_set = np.unique(tm)
        for mi in mi_set:
            sel = tm == mi
            mat = slots[mi] if (0 <= mi < len(slots)) else None
            mk = (mat.name if mat else '<none>') + '|' + name
            if mk not in mcache:
                mcache[mk] = material_info(mat, name)
            inf = mcache[mk]
            kind[sel] = inf['kind']
            if inf['img'] is not None and uvs:
                uvname = inf['uv'] if inf['uv'] in uvs else next((k for k, v in uvs.items() if v[1]), next(iter(uvs)))
                uva = uvs[uvname][0]
                c = uva[tl[sel]].mean(1)
                arr = inf['img']
                hh, ww = arr.shape[:2]
                px = np.clip((np.mod(c[:, 0], 1.0) * ww).astype(np.int32), 0, ww - 1)
                py = np.clip((np.mod(c[:, 1], 1.0) * hh).astype(np.int32), 0, hh - 1)
                col[sel] = arr[py, px]
            elif inf['vcol'] is not None and vcols:
                vn = inf['vcol'] if inf['vcol'] in vcols else next(iter(vcols))
                va, dom = vcols[vn]
                if dom == 'CORNER':
                    col[sel] = va[tl[sel]].mean(1)
                else:
                    col[sel] = va[tv[sel]].mean(1)
            else:
                col[sel] = inf['col']
        for pat, pname in force:
            if re.search(pat, name, re.I) or re.search(pat, chs, re.I):
                p = PIDX[pname]
                col[:] = PRGB[p]
                kind[:] = {'s': 0, 't': 1, 'm': 3}[PKIND[p]]
                if pname.startswith('Trans') and CFG.get('force_glow', False):
                    kind[:] = 2
                break
        if name not in oidx:
            oidx[name] = len(objnames)
            objnames.append(name)
        Ts.append(tri[keep])
        Cs.append(col[keep])
        Ks.append(kind[keep])
        Gs.append(np.full(int(keep.sum()), gidx[g], np.int16))
        Os.append(np.full(int(keep.sum()), oidx[name], np.int32))
        nobj += 1
    T = np.concatenate(Ts).astype(np.float32)
    C = np.concatenate(Cs).astype(np.float32)
    K = np.concatenate(Ks).astype(np.int8)
    G = np.concatenate(Gs).astype(np.int16)
    O = np.concatenate(Os).astype(np.int32)
    np.savez(os.path.join(WORK, 'tris.npz'), T=T, C=C, K=K, G=G, O=O)
    meta = {'groups': groups, 'objects': objnames, 'n_obj': nobj, 'n_tris': int(len(T)),
            'bbox': [T.reshape(-1, 3).min(0).tolist(), T.reshape(-1, 3).max(0).tolist()],
            'src': src, 'src_mtime': m0, 'src_mtime_same': os.path.getmtime(src) == m0}
    json.dump(meta, open(os.path.join(WORK, 'extract.json'), 'w'), indent=1)
    try:
        os.remove(cp)
    except Exception:
        pass
    log('extract done', nobj, 'objects', len(T), 'tris', round(time.time() - t0, 1), 's', 'groups', groups)


# ----------------------------------------------------------------------------------------------
# STAGE 2: voxelise -> bricks
# ----------------------------------------------------------------------------------------------
BRICKS = [(2, 8), (2, 6), (2, 4), (2, 3), (2, 2), (1, 8), (1, 6), (1, 4), (1, 3), (1, 2), (1, 1)]
PLATES = [(6, 8), (4, 8), (6, 6), (4, 6), (4, 4), (2, 12), (2, 10), (2, 8), (2, 6), (2, 4), (2, 3), (2, 2),
          (1, 12), (1, 10), (1, 8), (1, 6), (1, 4), (1, 3), (1, 2), (1, 1)]
TRANS_BRICKS = [(2, 4), (2, 2), (1, 6), (1, 4), (1, 2), (1, 1)]
TRANS_PLATES = [(2, 4), (2, 2), (1, 6), (1, 4), (1, 3), (1, 2), (1, 1)]
PARTNO = {('B', 1, 1): '3005', ('B', 1, 2): '3004', ('B', 1, 3): '3622', ('B', 1, 4): '3010', ('B', 1, 6): '3009',
          ('B', 1, 8): '3008', ('B', 2, 2): '3003', ('B', 2, 3): '3002', ('B', 2, 4): '3001', ('B', 2, 6): '2456',
          ('B', 2, 8): '3007',
          ('P', 1, 1): '3024', ('P', 1, 2): '3023', ('P', 1, 3): '3623', ('P', 1, 4): '3710', ('P', 1, 6): '3666',
          ('P', 1, 8): '3460', ('P', 1, 10): '4477', ('P', 1, 12): '60479', ('P', 2, 2): '3022', ('P', 2, 3): '3021',
          ('P', 2, 4): '3020', ('P', 2, 6): '3795', ('P', 2, 8): '3034', ('P', 2, 10): '3832', ('P', 2, 12): '2445',
          ('P', 4, 4): '3031', ('P', 4, 6): '3032', ('P', 4, 8): '3035', ('P', 6, 6): '3958', ('P', 6, 8): '3036',
          ('BP', 32, 32): '3811', ('BP', 16, 16): '91405'}


def voxelise(T, C, K, G, S, origin, dims):
    """Surface voxelisation by dense sampling. Returns pal grid, grp grid (int16, -1 empty)."""
    NX, NY, NZ = dims
    sc = np.array([S, S, 0.4 * S], np.float32)
    U = (T - origin) / sc
    lab = srgb_to_lab(C)
    pal = np.empty(len(C), np.int32)
    for k, cand in ((0, SOLID_IDX), (1, TRANS_IDX), (2, TRANS_IDX), (3, np.concatenate([METAL_IDX, SOLID_IDX]))):
        sel = K == k
        if sel.any():
            # chunked nearest (memory)
            idx = np.nonzero(sel)[0]
            for i in range(0, len(idx), 200000):
                j = idx[i:i + 200000]
                pal[j] = nearest(lab[j], cand) + (NP_ if k == 2 else 0)
    e1 = U[:, 1] - U[:, 0]
    e2 = U[:, 2] - U[:, 0]
    e3 = U[:, 2] - U[:, 1]
    L = np.sqrt(np.maximum(np.maximum((e1 ** 2).sum(1), (e2 ** 2).sum(1)), (e3 ** 2).sum(1)))
    ns = np.maximum(1, np.ceil(L / 0.33)).astype(np.int32)
    ns = np.minimum(ns, 3000)
    NG = int(G.max()) + 1
    acc_c, acc_g = [], []

    def add(pts, pidx, gidx):
        ix = np.floor(pts[:, 0]).astype(np.int64)
        iy = np.floor(pts[:, 1]).astype(np.int64)
        iz = np.floor(pts[:, 2]).astype(np.int64)
        ok = (ix >= 0) & (ix < NX) & (iy >= 0) & (iy < NY) & (iz >= 0) & (iz < NZ)
        lin = (ix * NY + iy) * NZ + iz
        lin = lin[ok]
        k1 = lin * NP_ + pidx[ok]
        u, c = np.unique(k1, return_counts=True)
        acc_c.append((u, c))
        k2 = lin * NG + gidx[ok]
        u, c = np.unique(k2, return_counts=True)
        acc_g.append((u, c))

    for n in np.unique(ns):
        idx = np.nonzero(ns == n)[0]
        I, J = np.meshgrid(np.arange(n + 1), np.arange(n + 1), indexing='ij')
        m = (I + J) <= n
        bi = (I[m] / n).astype(np.float32)
        bj = (J[m] / n).astype(np.float32)
        kpts = len(bi)
        step = max(1, int(6_000_000 // max(kpts, 1)))
        for s in range(0, len(idx), step):
            j = idx[s:s + step]
            A = U[j, 0][:, None, :]
            P = A + bi[None, :, None] * e1[j][:, None, :] + bj[None, :, None] * e2[j][:, None, :]
            P = P.reshape(-1, 3)
            add(P, np.repeat(pal[j], kpts), np.repeat(G[j].astype(np.int64), kpts))
    u = np.concatenate([a[0] for a in acc_c])
    c = np.concatenate([a[1] for a in acc_c])
    uu, inv = np.unique(u, return_inverse=True)
    cc = np.bincount(inv, weights=c)
    lin = uu // NP_
    pp = uu % NP_
    order = np.lexsort((cc, lin))
    lin_s = lin[order]
    last = np.r_[lin_s[1:] != lin_s[:-1], True]
    vlin = lin_s[last]
    vpal = pp[order][last]
    u = np.concatenate([a[0] for a in acc_g])
    c = np.concatenate([a[1] for a in acc_g])
    uu, inv = np.unique(u, return_inverse=True)
    cc = np.bincount(inv, weights=c)
    lin = uu // NG
    gg = uu % NG
    order = np.lexsort((cc, lin))
    lin_s = lin[order]
    last = np.r_[lin_s[1:] != lin_s[:-1], True]
    glin = lin_s[last]
    ggrp = gg[order][last]
    PAL = np.full(NX * NY * NZ, -1, np.int16)
    GRP = np.full(NX * NY * NZ, -1, np.int16)
    PAL[vlin] = vpal
    GRP[glin] = ggrp
    PAL = PAL.reshape(NX, NY, NZ)
    GRP = GRP.reshape(NX, NY, NZ)
    GRP[PAL < 0] = -1
    GRP[(PAL >= 0) & (GRP < 0)] = 0
    return PAL, GRP


def close_diagonals(PAL, GRP):
    """Fill one cell of every diagonal-only 2x2 pattern so walls have no see-through pinholes."""
    filled = 0
    for ax in ((0, 1), (0, 2), (1, 2)):
        for _ in range(2):
            occ = PAL >= 0
            sl = [slice(None)] * 3

            def S(a0, a1):
                s = [slice(None)] * 3
                s[ax[0]] = slice(0, -1) if a0 == 0 else slice(1, None)
                s[ax[1]] = slice(0, -1) if a1 == 0 else slice(1, None)
                return tuple(s)
            a, b, c, d = occ[S(0, 0)], occ[S(1, 1)], occ[S(1, 0)], occ[S(0, 1)]
            m1 = a & b & ~c & ~d
            m2 = c & d & ~a & ~b
            if not (m1.any() or m2.any()):
                break
            # fill cell 'c' (1,0) from 'a' for m1 ; fill cell 'a' (0,0) from 'c' for m2
            Pc = PAL[S(1, 0)]
            Gc = GRP[S(1, 0)]
            Pa = PAL[S(0, 0)]
            Ga = GRP[S(0, 0)]
            Pc[m1] = Pa[m1]
            Gc[m1] = Ga[m1]
            Pa2 = PAL[S(0, 0)]
            Ga2 = GRP[S(0, 0)]
            Pa2[m2] = PAL[S(1, 0)][m2]
            Ga2[m2] = GRP[S(1, 0)][m2]
            filled += int(m1.sum() + m2.sum())
    return filled


def pack_layer(keymap, sizes_fn, flip):
    NX, NY = keymap.shape
    used = np.zeros(keymap.shape, bool)
    out = []
    xs, ys = np.nonzero(keymap >= 0)
    if len(xs) == 0:
        return out
    if flip:
        order = np.lexsort((xs, ys))[::-1]
    else:
        order = np.lexsort((ys, xs))
    km = keymap
    for i in order:
        x = int(xs[i])
        y = int(ys[i])
        if used[x, y]:
            continue
        k = km[x, y]
        placed = False
        for (a, b) in sizes_fn(k):
            oris = ((a, b), (b, a)) if a != b else ((a, b),)
            if flip:
                oris = oris[::-1]
            for (w, l) in oris:
                if flip:
                    x0, y0 = x - w + 1, y - l + 1
                else:
                    x0, y0 = x, y
                if x0 < 0 or y0 < 0 or x0 + w > NX or y0 + l > NY:
                    continue
                if km[x0 + w - 1, y0 + l - 1] != k or km[x0, y0 + l - 1] != k or km[x0 + w - 1, y0] != k:
                    continue
                if (km[x0:x0 + w, y0:y0 + l] != k).any() or used[x0:x0 + w, y0:y0 + l].any():
                    continue
                used[x0:x0 + w, y0:y0 + l] = True
                out.append((x0, y0, w, l, int(k)))
                placed = True
                break
            if placed:
                break
        if not placed:
            used[x, y] = True
            out.append((x, y, 1, 1, int(k)))
    return out


def make_bricks(PAL, GRP):
    NX, NY, NZ = PAL.shape
    occ = PAL >= 0
    KEY = np.where(occ, PAL.astype(np.int32) + 1000 * GRP.astype(np.int32), -1)
    elems = []   # x,y,z,w,l,h,pal,grp
    EID = np.full(PAL.shape, -1, np.int32)
    trans = set(int(i) for i in TRANS_IDX)

    def bsz(k):
        return TRANS_BRICKS if ((k % 1000) % NP_) in trans else BRICKS

    def psz(k):
        return TRANS_PLATES if ((k % 1000) % NP_) in trans else PLATES
    for c in range((NZ + 2) // 3):
        z0 = 3 * c
        flip = (c % 2 == 1)
        if z0 + 2 < NZ:
            k0, k1, k2 = KEY[:, :, z0], KEY[:, :, z0 + 1], KEY[:, :, z0 + 2]
            bm = (k0 >= 0) & (k0 == k1) & (k0 == k2)
        else:
            bm = np.zeros((NX, NY), bool)
        if bm.any():
            km = np.where(bm, KEY[:, :, z0], -1)
            for (x, y, w, l, k) in pack_layer(km, bsz, flip):
                EID[x:x + w, y:y + l, z0:z0 + 3] = len(elems)
                elems.append((x, y, z0, w, l, 3, k % 1000, k // 1000))
        for dz in range(3):
            z = z0 + dz
            if z >= NZ:
                break
            km = np.where(bm, -1, KEY[:, :, z])
            if (km >= 0).any():
                for (x, y, w, l, k) in pack_layer(km, psz, flip ^ (dz % 2 == 1)):
                    EID[x:x + w, y:y + l, z] = len(elems)
                    elems.append((x, y, z, w, l, 1, k % 1000, k // 1000))
    E = np.array(elems, np.int32).reshape(-1, 8)
    return E, EID


# ----------------------------------------------------------------------------------------------
# Geometry helpers (real brick dimensions, metres)
# ----------------------------------------------------------------------------------------------
P = 0.008
PH = 0.0032
GAP = 0.0001
GZ = 0.00004
SR = 0.0024
SH = 0.0017
SEG = 12
_ang = np.linspace(0, 2 * math.pi, SEG, endpoint=False)
_cs = np.cos(_ang)
_sn = np.sin(_ang)
BOX_F = np.array([[0, 3, 2, 1], [4, 5, 6, 7], [0, 1, 5, 4], [2, 3, 7, 6], [3, 0, 4, 7], [1, 2, 6, 5]], np.int32)


def boxes_geom(x0, y0, z0, x1, y1, z1):
    n = len(x0)
    V = np.empty((n, 8, 3), np.float32)
    V[:, 0] = np.stack([x0, y0, z0], 1)
    V[:, 1] = np.stack([x1, y0, z0], 1)
    V[:, 2] = np.stack([x1, y1, z0], 1)
    V[:, 3] = np.stack([x0, y1, z0], 1)
    V[:, 4] = np.stack([x0, y0, z1], 1)
    V[:, 5] = np.stack([x1, y0, z1], 1)
    V[:, 6] = np.stack([x1, y1, z1], 1)
    V[:, 7] = np.stack([x0, y1, z1], 1)
    F = (BOX_F[None, :, :] + (np.arange(n) * 8)[:, None, None]).reshape(-1)
    return V.reshape(-1, 3), F, np.full(n * 6, 4, np.int32)


def studs_geom(cx, cy, z):
    n = len(cx)
    V = np.empty((n, 2 * SEG, 3), np.float32)
    V[:, :SEG, 0] = cx[:, None] + SR * _cs[None, :]
    V[:, :SEG, 1] = cy[:, None] + SR * _sn[None, :]
    V[:, :SEG, 2] = z[:, None]
    V[:, SEG:, 0] = V[:, :SEG, 0]
    V[:, SEG:, 1] = V[:, :SEG, 1]
    V[:, SEG:, 2] = z[:, None] + SH
    i = np.arange(SEG)
    side = np.stack([i, (i + 1) % SEG, SEG + (i + 1) % SEG, SEG + i], 1)  # SEG x 4
    top = SEG + i  # one n-gon
    per = np.concatenate([side.reshape(-1), top])
    F = (per[None, :] + (np.arange(n) * 2 * SEG)[:, None]).reshape(-1)
    sizes = np.tile(np.r_[np.full(SEG, 4), [SEG]], n).astype(np.int32)
    smooth = np.tile(np.r_[np.ones(SEG, bool), [False]], n)
    return V.reshape(-1, 3), F, sizes, smooth


def build_mesh(name, V, F, sizes, mat_idx, step, smooth=None):
    me = bpy.data.meshes.new(name)
    me.vertices.add(len(V))
    me.vertices.foreach_set('co', V.astype(np.float32).ravel())
    me.loops.add(len(F))
    me.loops.foreach_set('vertex_index', F.astype(np.int32))
    me.polygons.add(len(sizes))
    starts = np.zeros(len(sizes), np.int32)
    if len(sizes) > 1:
        starts[1:] = np.cumsum(sizes)[:-1]
    me.polygons.foreach_set('loop_start', starts)
    try:
        me.polygons.foreach_set('loop_total', sizes.astype(np.int32))
    except Exception:
        pass
    me.update(calc_edges=True)
    me.polygons.foreach_set('material_index', mat_idx.astype(np.int32))
    if smooth is not None:
        me.polygons.foreach_set('use_smooth', smooth.astype(bool))
    at = me.attributes.new('step', 'INT', 'FACE')
    at.data.foreach_set('value', step.astype(np.int32))
    me.update()
    return me


# ----------------------------------------------------------------------------------------------
# STAGE 2: build the set in a brand-new blank file
# ----------------------------------------------------------------------------------------------
def clean_group_name(g):
    names = CFG.get('group_names', {})
    for pat, nice in names.items():
        if re.search(pat, g, re.I):
            return nice
    s = re.sub(r'^(BAKED_|LUNAX_|LXH_|LX_|TYPE_\d+_|\d+[A-Z]?_)', '', g)
    s = re.sub(r'^(\d+_)', '', s)
    s = s.replace('_', ' ').strip()
    return s[:1].upper() + s[1:] if s else 'Model'


def voxel_grid_for(npz_path, studs=None, plates=None, S=None, extra_rot=None):
    d = np.load(npz_path)
    T, C, K, G = d['T'], d['C'], d['K'], d['G']
    mn = T.reshape(-1, 3).min(0)
    mx = T.reshape(-1, 3).max(0)
    ext = mx - mn
    if S is None:
        if plates:
            S = float(ext[2]) / (plates * 0.4)
        else:
            S = float(max(ext[0], ext[1])) / studs
    dims = (int(math.ceil(ext[0] / S)) + 1, int(math.ceil(ext[1] / S)) + 1, int(math.ceil(ext[2] / (0.4 * S))) + 1)
    origin = mn - np.array([0.5 * (dims[0] * S - ext[0]), 0.5 * (dims[1] * S - ext[1]), 0.0], np.float32)
    # emissive triangles keep a +NP_ code so they can glow
    PAL, GRP = voxelise(T, C, K, G, S, origin, dims)
    return PAL, GRP, S, origin, dims, K


def stage_build():
    t0 = time.time()
    meta = json.load(open(os.path.join(WORK, 'extract.json')))
    groups = list(meta['groups'])
    PAL, GRP, S, origin, dims, _ = voxel_grid_for(os.path.join(WORK, 'tris.npz'), studs=CFG.get('studs'), plates=CFG.get('plates'))
    log('voxelised', dims, 'S=%.4f' % S, 'filled', int((PAL >= 0).sum()), round(time.time() - t0, 1), 's')
    nf = close_diagonals(PAL, GRP)
    log('closed diagonals', nf)
    # ---- extras (other models placed on the base at their own scale)
    for ex in CFG.get('extras', []):
        try:
            p2, g2, S2, o2, d2, _ = voxel_grid_for(os.path.join(ex['work'], 'tris.npz'), studs=ex.get('studs'), plates=ex.get('plates'))
            close_diagonals(p2, g2)
            gname = ex['group']
            if gname not in groups:
                groups.append(gname)
            gi = groups.index(gname)
            ox, oy, oz = ex['at']  # voxel offset of the extra's min corner (x,y in studs from grid min, z in plates)
            if ex.get('center'):
                ox = int(ox - d2[0] // 2)
                oy = int(oy - d2[1] // 2)
            nx, ny, nz = PAL.shape
            need = (max(nx, ox + d2[0]), max(ny, oy + d2[1]), max(nz, oz + d2[2]))
            if need != PAL.shape:
                P2 = np.full(need, -1, np.int16)
                G2 = np.full(need, -1, np.int16)
                P2[:nx, :ny, :nz] = PAL
                G2[:nx, :ny, :nz] = GRP
                PAL, GRP = P2, G2
            sub = PAL[ox:ox + d2[0], oy:oy + d2[1], oz:oz + d2[2]]
            m = (p2 >= 0) & (sub < 0)
            sub[m] = p2[m]
            GRP[ox:ox + d2[0], oy:oy + d2[1], oz:oz + d2[2]][m] = gi
            # optional clear support column under the extra (a stand made of trans-clear 1x1 round-style bricks)
            if ex.get('stand'):
                cx, cy = ox + d2[0] // 2, oy + d2[1] // 2
                if 'Display stand' not in groups:
                    groups.append('Display stand')
                si = groups.index('Display stand')
                for zz in range(0, oz):
                    for dx in (0, 1):
                        for dy in (0, 1):
                            if PAL[cx + dx, cy + dy, zz] < 0:
                                PAL[cx + dx, cy + dy, zz] = PIDX['Trans-Clear']
                                GRP[cx + dx, cy + dy, zz] = si
            log('extra placed', gname, d2)
        except Exception as e:
            log('extra failed', ex.get('group'), repr(e))
    base = CFG.get('base', 'moon')
    # ---- pad for baseplate / stand
    if base == 'moon':
        m = 4
        NX, NY, NZ = PAL.shape
        BX = int(math.ceil((NX + 2 * m) / 32.0) * 32) if NX + 2 * m > 24 else 32
        BY = int(math.ceil((NY + 2 * m) / 32.0) * 32) if NY + 2 * m > 24 else 32
        if NX + 2 * m <= 16 and NY + 2 * m <= 16:
            BX = BY = 16
        px, py = (BX - NX) // 2, (BY - NY) // 2
        P2 = np.full((BX, BY, NZ), -1, np.int16)
        G2 = np.full((BX, BY, NZ), -1, np.int16)
        P2[px:px + NX, py:py + NY] = PAL
        G2[px:px + NX, py:py + NY] = GRP
        PAL, GRP = P2, G2
        # lunar surface details on the free baseplate area
        if 'Moon surface' not in groups:
            groups.append('Moon surface')
        gi = groups.index('Moon surface')
        rng = random.Random(7)
        foot = (PAL >= 0).any(2)
        near = foot.copy()
        for dx in (-2, -1, 0, 1, 2):
            for dy in (-2, -1, 0, 1, 2):
                near |= np.roll(np.roll(foot, dx, 0), dy, 1)
        free = ~near
        n_crater = max(2, (BX * BY) // 900)
        for _ in range(n_crater):
            for _try in range(30):
                cx, cy = rng.randrange(4, BX - 4), rng.randrange(4, BY - 4)
                r = rng.choice([2.5, 3.0, 3.5, 4.5])
                ok = True
                cells = []
                for xx in range(int(cx - r - 1), int(cx + r + 2)):
                    for yy in range(int(cy - r - 1), int(cy + r + 2)):
                        dd = math.hypot(xx + 0.5 - cx, yy + 0.5 - cy)
                        if abs(dd - r) < 0.55:
                            if not (0 <= xx < BX and 0 <= yy < BY) or not free[xx, yy]:
                                ok = False
                            cells.append((xx, yy))
                if ok:
                    for (xx, yy) in cells:
                        if rng.random() < 0.88:
                            PAL[xx, yy, 0] = PIDX['Dark Bluish Gray']
                            GRP[xx, yy, 0] = gi
                            free[xx, yy] = False
                    break
        n_rock = (BX * BY) // 160
        for _ in range(n_rock):
            xx, yy = rng.randrange(1, BX - 2), rng.randrange(1, BY - 2)
            w, l = rng.choice([(1, 1), (1, 1), (1, 2), (2, 2), (1, 3)])
            if rng.random() < 0.5:
                w, l = l, w
            if free[xx:xx + w, yy:yy + l].all():
                c = rng.choice(['Dark Bluish Gray', 'Dark Bluish Gray', 'Dark Tan', 'Light Bluish Gray', 'Tan'])
                if c == 'Light Bluish Gray':
                    c = 'Dark Bluish Gray'
                PAL[xx:xx + w, yy:yy + l, 0] = PIDX[c]
                GRP[xx:xx + w, yy:yy + l, 0] = gi
                free[xx:xx + w, yy:yy + l] = False
                if rng.random() < 0.25 and w * l >= 2:
                    PAL[xx, yy, 1] = PIDX[c]
                    GRP[xx, yy, 1] = gi
    elif base == 'stand':
        m = 2
        NX, NY, NZ = PAL.shape
        foot = (PAL >= 0).any(2)
        xs, ys = np.nonzero(foot)
        P2 = np.full((NX + 2 * m, NY + 2 * m, NZ + 2), -1, np.int16)
        G2 = np.full((NX + 2 * m, NY + 2 * m, NZ + 2), -1, np.int16)
        P2[m:m + NX, m:m + NY, 2:] = PAL
        G2[m:m + NX, m:m + NY, 2:] = GRP
        PAL, GRP = P2, G2
        if 'Display stand' not in groups:
            groups.append('Display stand')
        gi = groups.index('Display stand')
        x0, x1 = xs.min(), xs.max() + 2 * m + 1
        y0, y1 = ys.min(), ys.max() + 2 * m + 1
        sc_ = CFG.get('stand_color', 'Black')
        PAL[x0:x1, y0:y1, 0:2] = PIDX[sc_]
        GRP[x0:x1, y0:y1, 0:2] = gi
    NX, NY, NZ = PAL.shape
    # ---- bricks
    E, EID = make_bricks(PAL, GRP)
    nE = len(E)
    log('bricks', nE, round(time.time() - t0, 1), 's')
    # ---- build order (bags) and steps
    gids = sorted(set(int(g) for g in E[:, 7]))
    cen = np.array([NX / 2.0, NY / 2.0])
    info = []
    for g in gids:
        sel = E[:, 7] == g
        ex_ = E[sel]
        c = np.array([(ex_[:, 0] + ex_[:, 3] / 2.0).mean(), (ex_[:, 1] + ex_[:, 4] / 2.0).mean()])
        glass = np.isin(ex_[:, 6] % NP_, TRANS_IDX).mean()
        info.append((g, float(np.linalg.norm(c - cen)), float(glass), int(sel.sum()), float(ex_[:, 2].mean())))
    pri = CFG.get('group_order', [])

    def order_key(t):
        g, dist, glass, n, zm = t
        nm = groups[g]
        if nm in ('Moon surface', 'Display stand'):
            return (-1, 0, 0)
        for i, pat in enumerate(pri):
            if re.search(pat, nm, re.I):
                return (0, i, 0)
        return (1, 1 if glass > 0.5 else 0, dist)
    info.sort(key=order_key)
    # merge tiny groups into the following bag
    order = [t[0] for t in info]
    steps_total = int(np.clip(round(nE / 40.0), 8, CFG.get('max_steps', 72)))
    per = max(8, nE / steps_total)
    step = np.zeros(nE, np.int32)
    steps = []   # dicts
    sno = 0
    bag = 0
    for g in order:
        idx = np.nonzero(E[:, 7] == g)[0]
        if len(idx) == 0:
            continue
        bag += 1
        zs = E[idx, 2]
        cur = []
        layers = np.unique(zs)
        for li, z in enumerate(layers):
            mem = idx[zs == z]
            if cur and (len(cur) + len(mem) > per * 1.35):
                sno += 1
                step[cur] = sno
                steps.append({'step': sno, 'bag': bag, 'group': int(g), 'n': len(cur)})
                cur = []
            cur.extend(mem.tolist())
        if cur:
            sno += 1
            step[cur] = sno
            steps.append({'step': sno, 'bag': bag, 'group': int(g), 'n': len(cur)})
    nsteps = sno
    log('steps', nsteps, 'bags', bag)
    # ---- studs
    occ = PAL >= 0
    above = np.zeros_like(occ)
    above[:, :, :-1] = occ[:, :, 1:]
    sx, sy, sz = np.nonzero(occ & ~above)
    s_eid = EID[sx, sy, sz]
    okm = s_eid >= 0
    sx, sy, sz, s_eid = sx[okm], sy[okm], sz[okm], s_eid[okm]
    # ---- materials list (used codes)
    codes = sorted(set(int(c) for c in E[:, 6]))
    # glow codes: emissive cells were stored as pal + NP_? (voxelise maps emissive to trans; glow flag per config)
    code_slot = {c: i for i, c in enumerate(codes)}
    NS = len(codes)
    # ---- reset to blank file
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.name = CFG['set_no']
    sc.unit_settings.system = 'METRIC'
    sc.unit_settings.length_unit = 'CENTIMETERS'
    mats = []
    for c in codes:
        name, rgb, kind = PALETTE[c % NP_]
        mats.append(make_material(('Brick · ' if c < NP_ else 'Brick (lit) · ') + name, np.array(rgb) / 255.0, kind, glow=(c >= NP_)))
    for c in codes:
        name, rgb, kind = PALETTE[c % NP_]
        mats.append(make_material(('Faded · ' if c < NP_ else 'Faded (lit) · ') + name, np.array(rgb) / 255.0, kind, faded=True))
    ng = make_build_nodegroup()
    root = bpy.data.collections.new('Set %s · %s' % (CFG['set_no'], CFG['name']))
    sc.collection.children.link(root)
    sc['brick_step'] = 1000000
    sc['brick_fade'] = 0
    objs = []
    gname_of = {}
    bag_of_group = {}
    for st in steps:
        bag_of_group.setdefault(st['group'], st['bag'])
    for g in order:
        sel = np.nonzero(E[:, 7] == g)[0]
        if len(sel) == 0:
            continue
        ex_ = E[sel]
        x0 = ex_[:, 0] * P + GAP
        y0 = ex_[:, 1] * P + GAP
        z0 = ex_[:, 2] * PH
        x1 = (ex_[:, 0] + ex_[:, 3]) * P - GAP
        y1 = (ex_[:, 1] + ex_[:, 4]) * P - GAP
        z1 = (ex_[:, 2] + ex_[:, 5]) * PH - GZ
        V1, F1, S1 = boxes_geom(x0, y0, z0, x1, y1, z1)
        mi1 = np.repeat(np.array([code_slot[int(c)] for c in ex_[:, 6]], np.int32), 6)
        st1 = np.repeat(step[sel], 6)
        sm1 = np.zeros(len(S1), bool)
        ssel = np.isin(s_eid, sel)
        if ssel.any():
            cx = (sx[ssel] + 0.5) * P
            cy = (sy[ssel] + 0.5) * P
            cz = (sz[ssel] + 1) * PH - GZ
            V2, F2, S2, sm2 = studs_geom(cx.astype(np.float32), cy.astype(np.float32), cz.astype(np.float32))
            codes2 = PAL[sx[ssel], sy[ssel], sz[ssel]]
            mi2 = np.repeat(np.array([code_slot[int(c)] for c in codes2], np.int32), SEG + 1)
            st2 = np.repeat(step[s_eid[ssel]], SEG + 1)
            F2 = F2 + len(V1)
            V = np.concatenate([V1, V2])
            F = np.concatenate([F1, F2])
            Sz = np.concatenate([S1, S2])
            MI = np.concatenate([mi1, mi2])
            ST = np.concatenate([st1, st2])
            SM = np.concatenate([sm1, sm2])
        else:
            V, F, Sz, MI, ST, SM = V1, F1, S1, mi1, st1, sm1
        nice = clean_group_name(groups[g])
        gname_of[g] = nice
        bagn = bag_of_group.get(g, 0)
        col = bpy.data.collections.new('Bag %02d · %s' % (bagn, nice))
        root.children.link(col)
        me = build_mesh(nice, V, F, Sz, MI, ST, SM)
        for m_ in mats:
            me.materials.append(m_)
        ob = bpy.data.objects.new(nice, me)
        col.objects.link(ob)
        add_build_modifier(ob, ng, NS, sc)
        objs.append(ob)
    # ---- baseplate (not a voxel element)
    bp_count = 0
    if base == 'moon':
        bp = make_baseplate(NX, NY, occ[:, :, 0])
        col = bpy.data.collections.new('Baseplates')
        root.children.link(col)
        col.objects.link(bp)
        bp_count = (NX // 32) * (NY // 32) if NX >= 32 else 1
    # ---- parts bookkeeping
    parts = {}
    for (x, y, z, w, l, h, c, g), s in zip(E.tolist(), step.tolist()):
        a, b = min(w, l), max(w, l)
        kind = 'B' if h == 3 else 'P'
        k = (kind, a, b, c % NP_)
        parts[k] = parts.get(k, 0) + 1
    step_parts = {}
    for (x, y, z, w, l, h, c, g), s in zip(E.tolist(), step.tolist()):
        a, b = min(w, l), max(w, l)
        k = ('B' if h == 3 else 'P', a, b, c % NP_)
        d = step_parts.setdefault(s, {})
        d[k] = d.get(k, 0) + 1
    if base == 'moon':
        if NX >= 32:
            parts[('BP', 32, 32, PIDX['Light Bluish Gray'])] = bp_count
        else:
            parts[('BP', 16, 16, PIDX['Light Bluish Gray'])] = 1
    n_pieces = int(sum(parts.values()))
    scale_den = S / P if CFG.get('units_m', True) else None
    height_cm = (int(np.nonzero(occ.any((0, 1)))[0].max()) + 1) * PH * 100
    summary = {
        'set_no': CFG['set_no'], 'name': CFG['name'], 'subtitle': CFG.get('subtitle', ''), 'series': CFG.get('series', ''),
        'pieces': n_pieces, 'elements': int(nE), 'studs_visible': int(len(sx)), 'steps': int(nsteps), 'bags': int(bag),
        'width_cm': round(NX * P * 100, 1), 'depth_cm': round(NY * P * 100, 1), 'height_cm': round(height_cm, 1),
        'scale': ('1:%d' % round(scale_den)) if scale_den else '', 'grid': [int(NX), int(NY), int(NZ)],
        'colors': len(set(k[3] for k in parts)), 'unique_parts': len(parts),
        'bag_names': {str(bag_of_group[g]): gname_of[g] for g in gname_of if g in bag_of_group},
        'src': CFG['src'], 'src_mtime_same': meta.get('src_mtime_same'),
    }
    # ---- lights, world, cameras
    setup_render_world(sc)
    pts = sample_points(E, NX, NY, base)
    cam_front = add_camera(sc, 'Cam · Box art', pts, CFG.get('az', 38), CFG.get('el', 27), 4 / 3.0, CFG.get('margin', 0.06))
    cam_back = add_camera(sc, 'Cam · Back of box', pts, CFG.get('az', 38) + 155, CFG.get('el', 27) + 5, 4 / 3.0, 0.07)
    sc.camera = cam_front
    # text note in the file
    tx = bpy.data.texts.new('README')
    tx.write('%s  %s\n%s\n\n%d pieces, %d steps in %d bags.\nBuilt from: %s (original not modified)\n\n'
             'Build animation: press Space - the scene property brick_step drives every bag.\n'
             'Instruction view: set brick_fade = 1 to see older steps faded.\n' %
             (CFG['set_no'], CFG['name'], CFG.get('subtitle', ''), n_pieces, nsteps, bag, CFG['src']))
    blend_path = os.path.join(OUT, CFG['file_stem'] + '.blend')
    for s_ in CFG.get('all_sources', []) + [CFG['src']]:
        if os.path.normcase(os.path.abspath(s_)) == os.path.normcase(os.path.abspath(blend_path)):
            raise RuntimeError('refusing to write over a source file')
    bpy.ops.wm.save_as_mainfile(filepath=blend_path, compress=True)
    log('saved', blend_path, round(time.time() - t0, 1), 's')
    # ---- renders
    renders = {}
    try:
        renders = do_renders(sc, objs, E, step, steps, NX, NY, base, cam_front, cam_back, nsteps, NS)
    except Exception:
        log('render error', traceback.format_exc())
    # ---- build animation keyframes (after renders) then save again
    sc['brick_step'] = 0
    fps_step = 4 if nsteps > 40 else 6
    sc.frame_start = 1
    sc.frame_end = int(nsteps * fps_step + 48)
    for k in range(0, nsteps + 1):
        sc['brick_step'] = k
        sc.keyframe_insert('["brick_step"]', frame=1 + k * fps_step)
    try:
        for fc in sc.animation_data.action.fcurves:
            for kp in fc.keyframe_points:
                kp.interpolation = 'CONSTANT'
    except Exception:
        pass
    sc['brick_fade'] = 0
    sc.frame_set(sc.frame_end)
    bpy.ops.wm.save_as_mainfile(filepath=blend_path, compress=True)
    summary['blend'] = blend_path
    summary['renders'] = renders
    summary['parts'] = [[k[0], k[1], k[2], PNAME[k[3]], v] for k, v in sorted(parts.items(), key=lambda kv: (kv[0][3], kv[0][0], kv[0][1], kv[0][2]))]
    summary['step_parts'] = {str(s): [[k[0], k[1], k[2], PNAME[k[3]], v] for k, v in d.items()] for s, d in step_parts.items()}
    summary['steps_meta'] = steps
    summary['group_names'] = {str(g): gname_of.get(g, '') for g in gname_of}
    summary['src_mtime_same_after'] = (os.path.getmtime(CFG['src']) == meta.get('src_mtime'))
    json.dump(summary, open(os.path.join(WORK, 'summary.json'), 'w'), indent=1)
    log('build done', n_pieces, 'pieces', round(time.time() - t0, 1), 's')
    return summary


def make_material(name, rgb, kind, faded=False, glow=False):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    bs = nt.nodes.get('Principled BSDF')
    lin = srgb2lin(rgb)
    if faded:
        fr = 0.62
        f = rgb * (1 - fr) + np.array([1, 1, 1]) * fr
        m.diffuse_color = (*srgb2lin(f), 1)
        bs.inputs['Base Color'].default_value = (*srgb2lin(f), 1)
        return m
    m.diffuse_color = (*lin, 1) if kind != 't' else (*srgb2lin(rgb * 0.75 + 0.25), 1)
    bs.inputs['Base Color'].default_value = (*lin, 1)
    bs.inputs['Roughness'].default_value = 0.22
    try:
        bs.inputs['Specular IOR Level'].default_value = 0.5
    except Exception:
        pass
    bv = nt.nodes.new('ShaderNodeBevel')
    bv.inputs['Radius'].default_value = 0.00030
    try:
        bv.samples = 6
    except Exception:
        pass
    nt.links.new(bv.outputs['Normal'], bs.inputs['Normal'])
    if kind == 't':
        tw = bs.inputs.get('Transmission Weight') or bs.inputs.get('Transmission')
        tw.default_value = 1.0
        bs.inputs['Roughness'].default_value = 0.04
        bs.inputs['IOR'].default_value = 1.585
        c2 = srgb2lin(np.clip(rgb * 1.05, 0, 1))
        bs.inputs['Base Color'].default_value = (*c2, 1)
        if glow:
            ec = bs.inputs.get('Emission Color') or bs.inputs.get('Emission')
            ec.default_value = (*lin, 1)
            bs.inputs['Emission Strength'].default_value = 2.5
    elif kind == 'm':
        bs.inputs['Metallic'].default_value = 1.0
        bs.inputs['Roughness'].default_value = 0.3
    return m


def make_build_nodegroup():
    ng = bpy.data.node_groups.new('Brick Build Steps', 'GeometryNodeTree')
    it = ng.interface
    it.new_socket('Geometry', in_out='INPUT', socket_type='NodeSocketGeometry')
    s1 = it.new_socket('Step', in_out='INPUT', socket_type='NodeSocketInt')
    s1.default_value = 1000000
    it.new_socket('Fade', in_out='INPUT', socket_type='NodeSocketBool')
    it.new_socket('Fade Offset', in_out='INPUT', socket_type='NodeSocketInt')
    it.new_socket('Geometry', in_out='OUTPUT', socket_type='NodeSocketGeometry')
    N, L = ng.nodes, ng.links
    gi = N.new('NodeGroupInput')
    go = N.new('NodeGroupOutput')
    na = N.new('GeometryNodeInputNamedAttribute')
    na.data_type = 'INT'
    na.inputs['Name'].default_value = 'step'
    attr = na.outputs.get('Attribute') or na.outputs[0]
    cg = N.new('FunctionNodeCompare')
    cg.data_type = 'INT'
    cg.operation = 'GREATER_THAN'
    L.new(attr, cg.inputs[2])
    L.new(gi.outputs['Step'], cg.inputs[3])
    dl = N.new('GeometryNodeDeleteGeometry')
    dl.domain = 'FACE'
    L.new(gi.outputs['Geometry'], dl.inputs['Geometry'])
    L.new(cg.outputs['Result'], dl.inputs['Selection'])
    cl = N.new('FunctionNodeCompare')
    cl.data_type = 'INT'
    cl.operation = 'LESS_THAN'
    L.new(attr, cl.inputs[2])
    L.new(gi.outputs['Step'], cl.inputs[3])
    an = N.new('FunctionNodeBooleanMath')
    an.operation = 'AND'
    L.new(cl.outputs['Result'], an.inputs[0])
    L.new(gi.outputs['Fade'], an.inputs[1])
    mi = N.new('GeometryNodeInputMaterialIndex')
    ad = N.new('ShaderNodeMath')
    ad.operation = 'ADD'
    L.new(mi.outputs[0], ad.inputs[0])
    L.new(gi.outputs['Fade Offset'], ad.inputs[1])
    sm = N.new('GeometryNodeSetMaterialIndex')
    L.new(dl.outputs['Geometry'], sm.inputs['Geometry'])
    L.new(an.outputs[0], sm.inputs['Selection'])
    L.new(ad.outputs[0], sm.inputs['Material Index'])
    L.new(sm.outputs['Geometry'], go.inputs[0])
    return ng


def sock_ids(ng):
    ids = {}
    for item in ng.interface.items_tree:
        if getattr(item, 'in_out', '') == 'INPUT' and getattr(item, 'socket_type', '') != 'NodeSocketGeometry':
            ids[item.name] = item.identifier
    return ids


def add_build_modifier(ob, ng, NS, sc):
    md = ob.modifiers.new('Build steps', 'NODES')
    md.node_group = ng
    ids = sock_ids(ng)
    md[ids['Step']] = 1000000
    md[ids['Fade Offset']] = NS
    for nm, prop in (('Step', 'brick_step'), ('Fade', 'brick_fade')):
        try:
            fc = md.driver_add('["%s"]' % ids[nm])
            dr = fc.driver
            dr.type = 'AVERAGE'
            v = dr.variables.new()
            v.type = 'SINGLE_PROP'
            v.targets[0].id_type = 'SCENE'
            v.targets[0].id = sc
            v.targets[0].data_path = '["%s"]' % prop
        except Exception as e:
            log('driver fail', repr(e))


def make_baseplate(NX, NY, covered):
    x0 = np.array([GAP], np.float32)
    V1, F1, S1 = boxes_geom(np.array([0.0], np.float32) + GAP, np.array([0.0], np.float32) + GAP, np.array([-0.0016], np.float32),
                            np.array([NX * P - GAP], np.float32), np.array([NY * P - GAP], np.float32), np.array([0.0], np.float32))
    xs, ys = np.nonzero(~covered)
    V2, F2, S2, sm2 = studs_geom(((xs + 0.5) * P).astype(np.float32), ((ys + 0.5) * P).astype(np.float32), np.zeros(len(xs), np.float32))
    V = np.concatenate([V1, V2])
    F = np.concatenate([F1, F2 + len(V1)])
    Sz = np.concatenate([S1, S2])
    MI = np.zeros(len(Sz), np.int32)
    ST = np.zeros(len(Sz), np.int32)
    SM = np.concatenate([np.zeros(len(S1), bool), sm2])
    me = build_mesh('Baseplates', V, F, Sz, MI, ST, SM)
    me.materials.append(make_material('Brick · Light Bluish Gray (baseplate)', np.array(PALETTE[PIDX['Light Bluish Gray']][1]) / 255.0, 's'))
    # seam lines between 32x32 baseplates are drawn as thin dark boxes
    ob = bpy.data.objects.new('Baseplates', me)
    return ob


def sample_points(E, NX, NY, base):
    rng = np.random.default_rng(1)
    idx = rng.choice(len(E), size=min(len(E), 4000), replace=False)
    ex_ = E[idx]
    pts = []
    for dx in (0, 1):
        for dy in (0, 1):
            for dz in (0, 1):
                pts.append(np.stack([(ex_[:, 0] + dx * ex_[:, 3]) * P, (ex_[:, 1] + dy * ex_[:, 4]) * P, (ex_[:, 2] + dz * ex_[:, 5]) * PH], 1))
    pts = np.concatenate(pts)
    if base == 'moon':
        corners = np.array([[0, 0, 0], [NX * P, 0, 0], [0, NY * P, 0], [NX * P, NY * P, 0]], np.float32)
        pts = np.concatenate([pts, corners])
    return pts.astype(np.float32)


def fit_camera(cam, pts, az, el, aspect, margin):
    d = np.array([math.cos(math.radians(el)) * math.cos(math.radians(az)), math.cos(math.radians(el)) * math.sin(math.radians(az)), math.sin(math.radians(el))])
    lo_c = pts.min(0)
    hi_c = pts.max(0)
    center = (lo_c + hi_c) / 2
    q = Vector((-d).tolist()).to_track_quat('-Z', 'Y')
    cam.rotation_euler = q.to_euler()
    Rm = np.array(q.to_matrix())
    tanx = math.tan(cam.data.angle_x / 2) if aspect >= 1 else math.tan(cam.data.angle_y / 2) * aspect
    tany = tanx / aspect
    sx_ = 0.0
    sy_ = 0.0
    for it in range(3):
        lo, hi = 0.001, 100.0
        for _ in range(50):
            mid = (lo + hi) / 2
            loc = center + d * mid
            pc = (pts - loc) @ Rm
            zz = -pc[:, 2]
            if (zz <= 1e-4).any():
                lo = mid
                continue
            u = pc[:, 0] / zz / tanx / 2 + 0.5 - sx_
            v = pc[:, 1] / zz / tany / 2 + 0.5 - sy_ * aspect
            if u.min() < margin or u.max() > 1 - margin or v.min() < margin or v.max() > 1 - margin:
                lo = mid
            else:
                hi = mid
        loc = center + d * hi
        pc = (pts - loc) @ Rm
        zz = -pc[:, 2]
        u = pc[:, 0] / zz / tanx / 2 + 0.5
        v = pc[:, 1] / zz / tany / 2 + 0.5
        sx_ = (u.min() + u.max()) / 2 - 0.5
        sy_ = ((v.min() + v.max()) / 2 - 0.5) / aspect
    cam.location = loc.tolist()
    cam.data.shift_x = sx_
    cam.data.shift_y = sy_
    return cam


def add_camera(sc, name, pts, az, el, aspect, margin):
    cd = bpy.data.cameras.new(name)
    cd.lens = 50
    cd.clip_start = 0.002
    cd.clip_end = 100
    cam = bpy.data.objects.new(name, cd)
    sc.collection.objects.link(cam)
    fit_camera(cam, pts, az, el, aspect, margin)
    return cam


def setup_render_world(sc):
    w = bpy.data.worlds.new('Studio')
    sc.world = w
    w.use_nodes = True
    nt = w.node_tree
    bg = nt.nodes.get('Background')
    tc = nt.nodes.new('ShaderNodeTexCoord')
    sep = nt.nodes.new('ShaderNodeSeparateXYZ')
    ramp = nt.nodes.new('ShaderNodeValToRGB')
    nt.links.new(tc.outputs['Generated'], sep.inputs[0])
    nt.links.new(sep.outputs['Z'], ramp.inputs['Fac'])
    ramp.color_ramp.elements[0].position = 0.45
    ramp.color_ramp.elements[0].color = (0.010, 0.012, 0.020, 1)
    ramp.color_ramp.elements[1].position = 0.85
    ramp.color_ramp.elements[1].color = (0.55, 0.60, 0.70, 1)
    nt.links.new(ramp.outputs['Color'], bg.inputs['Color'])
    bg.inputs['Strength'].default_value = 0.9
    az = math.radians(CFG.get('az', 38))
    key = bpy.data.lights.new('Key sun', 'SUN')
    key.energy = 3.4
    key.angle = math.radians(3.0)
    kob = bpy.data.objects.new('Key sun', key)
    kob.rotation_euler = Euler((math.radians(42), 0, az + math.radians(55) + math.pi / 2))
    sc.collection.objects.link(kob)
    rim = bpy.data.lights.new('Rim sun', 'SUN')
    rim.energy = 1.6
    rim.angle = math.radians(8)
    rim.color = (0.75, 0.85, 1.0)
    rob = bpy.data.objects.new('Rim sun', rim)
    rob.rotation_euler = Euler((math.radians(60), 0, az + math.radians(200) + math.pi / 2))
    sc.collection.objects.link(rob)


def set_step(sc, objs, k, fade):
    sc['brick_step'] = int(k)
    sc['brick_fade'] = int(fade)
    for ob in objs:
        md = ob.modifiers.get('Build steps')
        ids = sock_ids(md.node_group)
        md[ids['Step']] = int(k)
        md[ids['Fade']] = bool(fade)
    sc.frame_set(sc.frame_current)
    bpy.context.view_layer.update()


def setup_cycles(sc, w, h, samples):
    sc.render.engine = 'CYCLES'
    try:
        cp = bpy.context.preferences.addons['cycles'].preferences
        dev = None
        for dt in ('OPTIX', 'CUDA'):
            try:
                cp.compute_device_type = dt
                cp.get_devices()
                if any(d.type == dt for d in cp.devices):
                    for d in cp.devices:
                        d.use = (d.type == dt)
                    dev = dt
                    break
            except Exception:
                pass
        sc.cycles.device = 'GPU' if dev else 'CPU'
    except Exception:
        sc.cycles.device = 'CPU'
    sc.cycles.samples = samples
    sc.cycles.use_adaptive_sampling = True
    sc.cycles.adaptive_threshold = 0.02
    sc.cycles.use_denoising = True
    try:
        sc.cycles.denoiser = 'OPTIX'
    except Exception:
        pass
    sc.cycles.max_bounces = 8
    sc.cycles.transmission_bounces = 8
    sc.cycles.transparent_max_bounces = 8
    try:
        sc.cycles.time_limit = 150
    except Exception:
        pass
    sc.render.film_transparent = True
    sc.render.resolution_x = w
    sc.render.resolution_y = h
    sc.render.resolution_percentage = 100
    sc.render.image_settings.file_format = 'PNG'
    sc.render.image_settings.color_mode = 'RGBA'
    sc.view_settings.view_transform = 'AgX'
    for lk in ('AgX - Punchy', 'Punchy', 'AgX - Medium High Contrast', 'Medium High Contrast'):
        try:
            sc.view_settings.look = lk
            break
        except Exception:
            pass


def setup_workbench(sc, w, h):
    sc.render.engine = 'BLENDER_WORKBENCH'
    sh = sc.display.shading
    sh.light = 'STUDIO'
    sh.color_type = 'MATERIAL'
    sh.show_cavity = True
    try:
        sh.cavity_type = 'BOTH'
        sh.cavity_ridge_factor = 1.0
        sh.cavity_valley_factor = 1.0
    except Exception:
        pass
    sh.show_object_outline = True
    sh.object_outline_color = (0.05, 0.05, 0.05)
    sh.show_shadows = True
    sh.shadow_intensity = 0.35
    try:
        sc.display.render_aa = '16'
    except Exception:
        pass
    sc.render.film_transparent = True
    sc.render.resolution_x = w
    sc.render.resolution_y = h
    sc.render.resolution_percentage = 100
    sc.render.image_settings.file_format = 'PNG'
    sc.render.image_settings.color_mode = 'RGBA'
    sc.view_settings.view_transform = 'Standard'
    sc.view_settings.look = 'None'


def do_renders(sc, objs, E, step, steps, NX, NY, base, cam_front, cam_back, nsteps, NS):
    R = {}
    rd = os.path.join(WORK, 'renders')
    os.makedirs(rd, exist_ok=True)
    t0 = time.time()
    set_step(sc, objs, 1000000, 0)
    setup_cycles(sc, 2000, 1500, CFG.get('samples', 128))
    sc.camera = cam_front
    sc.render.filepath = os.path.join(rd, 'box_front.png')
    bpy.ops.render.render(write_still=True)
    R['front'] = sc.render.filepath
    log('front render', round(time.time() - t0, 1), 's')
    setup_cycles(sc, 1600, 1200, max(64, CFG.get('samples', 128) // 2))
    sc.camera = cam_back
    sc.render.filepath = os.path.join(rd, 'box_back.png')
    bpy.ops.render.render(write_still=True)
    R['back'] = sc.render.filepath
    log('back render', round(time.time() - t0, 1), 's')
    # instruction steps (workbench)
    setup_workbench(sc, 1400, 1050)
    cd = bpy.data.cameras.new('Cam · Instructions')
    cd.lens = 50
    cd.clip_start = 0.002
    cd.clip_end = 100
    ci = bpy.data.objects.new('Cam · Instructions', cd)
    sc.collection.objects.link(ci)
    sc.camera = ci
    cenxy = np.array([NX * P / 2, NY * P / 2])
    R['steps'] = {}
    by_bag = {}
    for st in steps:
        by_bag.setdefault(st['bag'], []).append(st)
    for bagn, sts in by_bag.items():
        g = sts[0]['group']
        selg = E[:, 7] == g
        bc = np.array([((E[selg, 0] + E[selg, 3] / 2.0) * P).mean(), ((E[selg, 1] + E[selg, 4] / 2.0) * P).mean()])
        v = bc - cenxy
        if np.linalg.norm(v) > 0.15 * max(NX, NY) * P:
            az = math.degrees(math.atan2(v[1], v[0]))
        else:
            az = CFG.get('az', 38)
        for st in sts:
            k = st['step']
            sel = selg & (step <= k)
            ex_ = E[sel]
            pts = []
            for dx in (0, 1):
                for dy in (0, 1):
                    for dz in (0, 1):
                        pts.append(np.stack([(ex_[:, 0] + dx * ex_[:, 3]) * P, (ex_[:, 1] + dy * ex_[:, 4]) * P, (ex_[:, 2] + dz * ex_[:, 5]) * PH], 1))
            pts = np.concatenate(pts).astype(np.float32)
            if len(pts) > 30000:
                pts = pts[np.random.default_rng(k).choice(len(pts), 30000, replace=False)]
            fit_camera(ci, pts, az, CFG.get('el_steps', 38), 4 / 3.0, 0.10)
            set_step(sc, objs, k, 1)
            fp = os.path.join(rd, 'step_%03d.png' % k)
            sc.render.filepath = fp
            bpy.ops.render.render(write_still=True)
            R['steps'][str(k)] = fp
    # final full model (instruction style, no fade)
    set_step(sc, objs, 1000000, 0)
    fit_camera(ci, sample_points(E, NX, NY, base), CFG.get('az', 38), 30, 4 / 3.0, 0.06)
    sc.render.filepath = os.path.join(rd, 'final_wb.png')
    bpy.ops.render.render(write_still=True)
    R['final_wb'] = sc.render.filepath
    log('step renders', len(R['steps']), round(time.time() - t0, 1), 's')
    return R


# ----------------------------------------------------------------------------------------------
# STAGE 3: box art, parts list and instruction booklet (PIL, bundled with Blender)
# ----------------------------------------------------------------------------------------------
def stage_pages(summary=None):
    from PIL import Image, ImageDraw, ImageFont, ImageFilter
    if summary is None:
        summary = json.load(open(os.path.join(WORK, 'summary.json')))
    FD = r'C:\Windows\Fonts'

    def font(names, size):
        for n in names:
            p = os.path.join(FD, n)
            if os.path.exists(p):
                try:
                    return ImageFont.truetype(p, size)
                except Exception:
                    pass
        return ImageFont.load_default()
    BLACK = ['seguibl.ttf', 'ariblk.ttf', 'arialbd.ttf']
    BOLD = ['segoeuib.ttf', 'arialbd.ttf']
    SEMI = ['seguisb.ttf', 'segoeuib.ttf', 'arial.ttf']
    REG = ['segoeui.ttf', 'arial.ttf']
    th = CFG.get('theme', '#1FA2FF').lstrip('#')
    THEME = tuple(int(th[i:i + 2], 16) for i in (0, 2, 4))
    rd = os.path.join(WORK, 'renders')
    rng = random.Random(11)

    def space_bg(W, H, glow_at=(0.58, 0.5)):
        y = np.linspace(0, 1, H)[:, None, None]
        top = np.array([5, 8, 20], np.float32)
        bot = np.array([16, 24, 50], np.float32)
        arr = top * (1 - y) + bot * y
        arr = np.repeat(arr, W, axis=1)
        xx, yy = np.meshgrid(np.linspace(0, 1, W), np.linspace(0, 1, H))
        d = np.sqrt(((xx - glow_at[0]) * W / H) ** 2 + (yy - glow_at[1]) ** 2)
        g = np.clip(1 - d / 0.75, 0, 1) ** 2
        arr = arr + g[:, :, None] * np.array(THEME, np.float32)[None, None, :] * 0.22
        img = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), 'RGB')
        dr = ImageDraw.Draw(img, 'RGBA')
        for _ in range(int(W * H / 2600)):
            x, y_ = rng.random() * W, rng.random() * H
            a = rng.randint(40, 200)
            r = rng.choice([0.6, 0.8, 1.0, 1.0, 1.4])
            dr.ellipse([x - r, y_ - r, x + r, y_ + r], fill=(255, 255, 255, a))
        for _ in range(int(W * H / 160000)):
            x, y_ = rng.random() * W, rng.random() * H * 0.7
            L = rng.randint(10, 26)
            for k in range(L, 0, -1):
                a = int(150 * (1 - k / L))
                dr.line([x - k, y_, x + k, y_], fill=(255, 255, 255, max(0, a // 3)), width=1)
                dr.line([x, y_ - k, x, y_ + k], fill=(255, 255, 255, max(0, a // 3)), width=1)
            dr.ellipse([x - 2.2, y_ - 2.2, x + 2.2, y_ + 2.2], fill=(255, 255, 255, 235))
        return img

    def fit_text(text, names, max_w, start, min_size=30):
        s = start
        while s > min_size:
            f = font(names, s)
            if f.getlength(text) <= max_w:
                return f
            s -= 4
        return font(names, min_size)

    def paste_render(canvas, path, box, anchor=(0.5, 0.5)):
        if not path or not os.path.exists(path):
            return
        im = Image.open(path).convert('RGBA')
        bb = im.getbbox()
        if bb:
            im = im.crop(bb)
        bw, bh = box[2] - box[0], box[3] - box[1]
        k = min(bw / im.width, bh / im.height)
        im = im.resize((max(1, int(im.width * k)), max(1, int(im.height * k))), Image.LANCZOS)
        x = int(box[0] + (bw - im.width) * anchor[0])
        y = int(box[1] + (bh - im.height) * anchor[1])
        sh = Image.new('RGBA', im.size, (0, 0, 0, 0))
        sh.putalpha(im.split()[3].point(lambda a: int(a * 0.45)))
        sh = sh.filter(ImageFilter.GaussianBlur(18))
        canvas.paste((0, 0, 0), (x + 10, y + 22), sh)
        canvas.paste(im, (x, y), im)

    def rrect(dr, box, r, fill=None, outline=None, width=1):
        dr.rounded_rectangle(box, r, fill=fill, outline=outline, width=width)

    pieces = summary['pieces']
    age = CFG.get('age', '16+' if pieces > 3000 else ('14+' if pieces > 1200 else '12+'))
    out_files = {}
    # ---------------- box front
    W, H = 2400, 1800
    img = space_bg(W, H)
    dr = ImageDraw.Draw(img, 'RGBA')
    dr.ellipse([-0.25 * W, 0.80 * H, 1.25 * W, 2.3 * H], fill=(52, 56, 66, 255))
    dr.ellipse([-0.25 * W, 0.80 * H, 1.25 * W, 2.3 * H], outline=(120, 128, 142, 120), width=4)
    paste_render(img, summary['renders'].get('front'), (int(0.05 * W), int(0.13 * H), int(0.97 * W), int(0.86 * H)), (0.6, 0.55))
    dr = ImageDraw.Draw(img, 'RGBA')
    f_small = font(SEMI, 30)
    dr.text((70, 54), 'PROJECT ARES', font=f_small, fill=(205, 215, 235))
    fb = font(BLACK, 58)
    tw_ = fb.getlength('BRICK SERIES')
    rrect(dr, [64, 96, 64 + tw_ + 56, 186], 22, fill=THEME + (255,))
    dr.text((92, 104), 'BRICK SERIES', font=fb, fill=(255, 255, 255))
    fa = font(BLACK, 66)
    rrect(dr, [W - 250, 60, W - 70, 170], 20, fill=(255, 255, 255, 255))
    aw = fa.getlength(age)
    dr.text((W - 160 - aw / 2, 70), age, font=fa, fill=(20, 24, 34))
    fn = font(BOLD, 46)
    nw = fn.getlength(summary['set_no'])
    dr.text((W - 160 - nw / 2, 182), summary['set_no'], font=fn, fill=(255, 255, 255))
    title = summary['name'].upper()
    ft = fit_text(title, BLACK, 1500, 132)
    dr.text((74, H - 330), title, font=ft, fill=(0, 0, 0, 120))
    dr.text((70, H - 336), title, font=ft, fill=(255, 255, 255))
    fs = fit_text(summary.get('subtitle', ''), SEMI, 1450, 44)
    dr.text((72, H - 186), summary.get('subtitle', ''), font=fs, fill=(205, 215, 235))
    pcs = '{:,} pcs'.format(pieces)
    fp_ = font(BLACK, 46)
    pw = fp_.getlength(pcs)
    rrect(dr, [70, H - 120, 70 + pw + 50, H - 52], 34, fill=THEME + (255,))
    dr.text((95, H - 118), pcs, font=fp_, fill=(255, 255, 255))
    wcm = summary['width_cm']
    dim = ('Over %d cm wide' % int(wcm)) if wcm < 100 else ('Over %.1f m wide' % (int(wcm / 10) / 10.0))
    fdim = font(SEMI, 36)
    lw = fdim.getlength(dim)
    dr.line([W - 90 - lw, H - 140, W - 90, H - 140], fill=(220, 228, 240), width=3)
    dr.line([W - 90 - lw, H - 152, W - 90 - lw, H - 128], fill=(220, 228, 240), width=3)
    dr.line([W - 90, H - 152, W - 90, H - 128], fill=(220, 228, 240), width=3)
    dr.text((W - 90 - lw, H - 124), dim, font=fdim, fill=(230, 236, 245))
    if summary.get('scale'):
        sc_t = 'Scale ' + summary['scale']
        f2 = font(REG, 30)
        dr.text((W - 90 - f2.getlength(sc_t), H - 76), sc_t, font=f2, fill=(170, 180, 200))
    dr.rectangle([0, H - 16, W, H], fill=THEME)
    front_path = os.path.join(OUT, CFG['file_stem'] + ' - Box front.png')
    img.save(front_path)
    out_files['box_front'] = front_path
    # ---------------- box back
    img = space_bg(W, H, glow_at=(0.35, 0.5))
    dr = ImageDraw.Draw(img, 'RGBA')
    paste_render(img, summary['renders'].get('back'), (40, 120, int(0.66 * W), H - 80), (0.5, 0.5))
    dr = ImageDraw.Draw(img, 'RGBA')
    x0 = int(0.68 * W)
    rrect(dr, [x0, 110, W - 60, H - 80], 30, fill=(10, 14, 30, 210), outline=THEME + (255,), width=3)
    fh = font(BLACK, 54)
    dr.text((x0 + 44, 150), "WHAT'S INSIDE", font=fh, fill=(255, 255, 255))
    fr = font(SEMI, 33)
    fr2 = font(REG, 29)
    y = 240
    bag_names = summary.get('bag_names', {})
    for bn in sorted(bag_names, key=lambda s: int(s))[:14]:
        rrect(dr, [x0 + 44, y + 4, x0 + 112, y + 46], 10, fill=THEME + (255,))
        t = str(bn)
        dr.text((x0 + 78 - fr.getlength(t) / 2, y + 4), t, font=fr, fill=(255, 255, 255))
        nm = bag_names[bn]
        while fr.getlength(nm) > (W - 60) - (x0 + 130) - 30 and len(nm) > 4:
            nm = nm[:-2]
        dr.text((x0 + 130, y + 4), nm, font=fr, fill=(230, 236, 245))
        y += 62
    if len(bag_names) > 14:
        dr.text((x0 + 130, y + 4), '+ %d more bags' % (len(bag_names) - 14), font=fr2, fill=(170, 180, 200))
        y += 56
    y = max(y + 30, H - 560)
    facts = ['{:,} pieces in {} colours'.format(pieces, summary['colors']),
             '{} building steps, {} bags'.format(summary['steps'], summary['bags']),
             '{} × {} × {} cm'.format(int(round(summary['width_cm'])), int(round(summary['depth_cm'])), int(round(summary['height_cm'])))]
    if summary.get('scale'):
        facts.append('Scale ' + summary['scale'])
    facts.append('Built from: ' + os.path.basename(summary['src']))
    for ftxt in facts:
        while fr2.getlength(ftxt) > (W - 60) - (x0 + 44) - 30 and len(ftxt) > 6:
            ftxt = ftxt[:-2]
        dr.text((x0 + 44, y), ftxt, font=fr2, fill=(200, 210, 228))
        y += 52
    dr.rectangle([0, H - 16, W, H], fill=THEME)
    back_path = os.path.join(OUT, CFG['file_stem'] + ' - Box back.png')
    img.save(back_path)
    out_files['box_back'] = back_path
    # ---------------- parts list CSV
    csv_path = os.path.join(OUT, CFG['file_stem'] + ' - Parts list.csv')
    with open(csv_path, 'w', encoding='utf-8') as f:
        f.write('Part number,Part,Colour,Quantity\n')
        for kind, a, b, cname, qty in summary['parts']:
            pn = PARTNO.get((kind, a, b), '')
            pname = {'B': 'Brick', 'P': 'Plate', 'BP': 'Baseplate'}[kind] + ' %d x %d' % (a, b)
            f.write('%s,%s,%s,%d\n' % (pn, pname, cname, qty))
    out_files['csv'] = csv_path

    # ---------------- part icons
    icon_cache = {}

    def part_icon(kind, a, b, cname):
        key = (kind, a, b, cname)
        if key in icon_cache:
            return icon_cache[key]
        rgb = np.array(PALETTE[PIDX[cname]][1], np.float32)
        trans = PKIND[PIDX[cname]] == 't'
        if kind == 'BP':
            a, b = 4, 4
        w, l = a, b
        h = 1.2 if kind == 'B' else (0.4 if kind == 'P' else 0.2)
        s = 26.0 * 3
        c30 = 0.8660254

        def pr(x, y, z):
            return (x - y) * c30 * s, (x + y) * 0.5 * s - z * s
        pts = [pr(x, y, z) for x in (0, w) for y in (0, l) for z in (0, h + 0.22)]
        xs = [p[0] for p in pts]
        ys = [p[1] for p in pts]
        ox, oy = -min(xs) + 8, -min(ys) + 8
        Wc, Hc = int(max(xs) - min(xs) + 16), int(max(ys) - min(ys) + 16)
        im = Image.new('RGBA', (Wc, Hc), (0, 0, 0, 0))
        d = ImageDraw.Draw(im, 'RGBA')

        def P_(x, y, z):
            p = pr(x, y, z)
            return (p[0] + ox, p[1] + oy)
        alpha = 170 if trans else 255
        topc = tuple(int(v) for v in np.clip(rgb * 1.08 + 10, 0, 255)) + (alpha,)
        lc = tuple(int(v) for v in np.clip(rgb * 0.82, 0, 255)) + (alpha,)
        rc = tuple(int(v) for v in np.clip(rgb * 0.64, 0, 255)) + (alpha,)
        line = tuple(int(v) for v in np.clip(rgb * 0.35, 0, 255)) + (255,)
        lw_ = 3
        d.polygon([P_(w, 0, 0), P_(w, l, 0), P_(w, l, h), P_(w, 0, h)], fill=rc, outline=line, width=lw_)
        d.polygon([P_(0, l, 0), P_(w, l, 0), P_(w, l, h), P_(0, l, h)], fill=lc, outline=line, width=lw_)
        d.polygon([P_(0, 0, h), P_(w, 0, h), P_(w, l, h), P_(0, l, h)], fill=topc, outline=line, width=lw_)
        r = 0.3
        A = 1.2247 * r * s
        B = 0.7071 * r * s
        hs = 0.21 * s
        for ssum in range(0, w + l):
            for i in range(w):
                j = ssum - i
                if j < 0 or j >= l:
                    continue
                cx, cy = P_(i + 0.5, j + 0.5, h)
                d.rectangle([cx - A, cy - hs, cx + A, cy], fill=lc)
                d.ellipse([cx - A, cy - B, cx + A, cy + B], fill=lc, outline=line, width=2)
                d.ellipse([cx - A, cy - B - hs, cx + A, cy + B - hs], fill=topc, outline=line, width=2)
                d.line([cx - A, cy - hs, cx - A, cy], fill=line, width=2)
                d.line([cx + A, cy - hs, cx + A, cy], fill=line, width=2)
        im = im.resize((max(1, Wc // 3), max(1, Hc // 3)), Image.LANCZOS)
        icon_cache[key] = im
        return im

    def paste_fit(canvas, im, box):
        bw, bh = box[2] - box[0], box[3] - box[1]
        k = min(bw / im.width, bh / im.height, 1.6)
        im2 = im.resize((max(1, int(im.width * k)), max(1, int(im.height * k))), Image.LANCZOS)
        canvas.paste(im2, (int(box[0] + (bw - im2.width) / 2), int(box[1] + (bh - im2.height) / 2)), im2)

    # ---------------- booklet pages
    PW, PH_ = 2339, 1654
    pages = []
    fhead = font(SEMI, 28)

    def new_page(num=None, dark=False):
        pg = Image.new('RGB', (PW, PH_), (12, 16, 30) if dark else (255, 255, 255))
        d = ImageDraw.Draw(pg, 'RGBA')
        if not dark:
            d.rectangle([0, 0, PW, 10], fill=THEME)
            d.text((60, 30), '%s  %s' % (summary['set_no'], summary['name']), font=fhead, fill=(120, 128, 140))
            if num is not None:
                t = str(num)
                d.text((PW - 60 - fhead.getlength(t), PH_ - 70), t, font=fhead, fill=(120, 128, 140))
        return pg, d
    # cover
    pg, d = new_page(dark=True)
    cov = Image.open(front_path)
    k = min(PW / cov.width, PH_ / cov.height)
    cov = cov.resize((int(cov.width * k), int(cov.height * k)), Image.LANCZOS)
    pg.paste(cov, ((PW - cov.width) // 2, (PH_ - cov.height) // 2))
    pages.append(pg)
    # contents
    pnum = 2
    pg, d = new_page(pnum)
    d.text((80, 90), 'In this set', font=font(BLACK, 72), fill=(20, 24, 34))
    y = 220
    by_bag = {}
    for st in summary['steps_meta']:
        by_bag.setdefault(st['bag'], []).append(st['step'])
    fr = font(SEMI, 38)
    for bn in sorted(by_bag):
        nm = summary['bag_names'].get(str(bn), '')
        rrect(d, [80, y, 160, y + 62], 14, fill=THEME)
        t = str(bn)
        d.text((120 - fr.getlength(t) / 2, y + 6), t, font=fr, fill=(255, 255, 255))
        d.text((190, y + 6), nm, font=fr, fill=(30, 34, 44))
        rng_t = 'Steps %d-%d' % (min(by_bag[bn]), max(by_bag[bn])) if len(by_bag[bn]) > 1 else 'Step %d' % by_bag[bn][0]
        d.text((900, y + 10), rng_t, font=font(REG, 32), fill=(110, 118, 130))
        y += 82
        if y > PH_ - 200:
            break
    stats = ['{:,} pieces'.format(pieces), '{} colours'.format(summary['colors']), '{} steps'.format(summary['steps']),
             '{} × {} × {} cm'.format(int(round(summary['width_cm'])), int(round(summary['depth_cm'])), int(round(summary['height_cm'])))]
    if summary.get('scale'):
        stats.append('Scale ' + summary['scale'])
    yy = 240
    for s_ in stats:
        d.text((1300, yy), s_, font=font(BOLD, 44), fill=(30, 34, 44))
        yy += 70
    paste_fit(pg, Image.open(summary['renders']['final_wb']).convert('RGBA') if summary['renders'].get('final_wb') else Image.new('RGBA', (2, 2)), (1260, 640, PW - 80, PH_ - 120))
    pages.append(pg)
    # inventory
    items = sorted(summary['parts'], key=lambda p: (p[3], p[0], p[1], p[2]))
    cols, rows = 10, 6
    cw, ch_ = (PW - 160) // cols, (PH_ - 260) // rows
    fq = font(BLACK, 34)
    fsml = font(REG, 20)
    for i0 in range(0, len(items), cols * rows):
        pnum += 1
        pg, d = new_page(pnum)
        d.text((80, 80), 'Parts', font=font(BLACK, 60), fill=(20, 24, 34))
        for j, (kind, a, b, cname, qty) in enumerate(items[i0:i0 + cols * rows]):
            cx = 80 + (j % cols) * cw
            cy = 190 + (j // cols) * ch_
            rrect(d, [cx + 6, cy + 6, cx + cw - 6, cy + ch_ - 6], 14, fill=(240, 244, 249))
            paste_fit(pg, part_icon(kind, a, b, cname), (cx + 20, cy + 16, cx + cw - 20, cy + ch_ - 92))
            d.text((cx + 20, cy + ch_ - 88), '%dx' % qty, font=fq, fill=(20, 24, 34))
            pn = PARTNO.get((kind, a, b), '')
            d.text((cx + 20, cy + ch_ - 44), ('%s · ' % pn if pn else '') + cname, font=fsml, fill=(100, 108, 120))
        pages.append(pg)
    # steps (2 x 2 per page)
    steps_meta = summary['steps_meta']
    fstep = font(BLACK, 70)
    fcount = font(BOLD, 26)
    cellw, cellh = (PW - 140) // 2, (PH_ - 170) // 2
    for i0 in range(0, len(steps_meta), 4):
        pnum += 1
        pg, d = new_page(pnum)
        for j, st in enumerate(steps_meta[i0:i0 + 4]):
            cx = 60 + (j % 2) * (cellw + 20)
            cy = 90 + (j // 2) * (cellh + 10)
            k = st['step']
            rp = summary['renders'].get('steps', {}).get(str(k))
            # render
            if rp and os.path.exists(rp):
                paste_fit(pg, Image.open(rp).convert('RGBA'), (cx + 10, cy + 200, cx + cellw - 10, cy + cellh - 10))
            d.text((cx + 10, cy + 8), str(k), font=fstep, fill=(20, 24, 34))
            plist = sorted(summary['step_parts'].get(str(k), []), key=lambda p: -p[4])
            show = plist[:11] if len(plist) > 12 else plist
            bx0 = cx + 140
            n_ = len(show) + (1 if len(plist) > 12 else 0)
            boxw = min(cellw - 150, 34 + n_ * 92)
            rrect(d, [bx0, cy + 14, bx0 + boxw, cy + 186], 18, fill=(228, 238, 248), outline=(150, 175, 205), width=3)
            for q, (kind, a, b, cname, qty) in enumerate(show):
                ix = bx0 + 18 + q * 92
                paste_fit(pg, part_icon(kind, a, b, cname), (ix, cy + 26, ix + 84, cy + 136))
                d.text((ix + 6, cy + 140), '%dx' % qty, font=fcount, fill=(20, 24, 34))
            if len(plist) > 12:
                ix = bx0 + 18 + len(show) * 92
                more = sum(p[4] for p in plist[11:])
                d.text((ix, cy + 70), '+%d' % more, font=fcount, fill=(60, 70, 90))
            if j == 0 and (i0 == 0 or steps_meta[i0 - 1]['bag'] != st['bag']) or (j > 0 and steps_meta[i0 + j - 1]['bag'] != st['bag']):
                tag = 'BAG %d · %s' % (st['bag'], summary['bag_names'].get(str(st['bag']), ''))
                ft_ = font(BOLD, 28)
                tw2 = ft_.getlength(tag)
                rrect(d, [cx + cellw - tw2 - 60, cy + cellh - 66, cx + cellw - 16, cy + cellh - 16], 14, fill=THEME)
                d.text((cx + cellw - tw2 - 38, cy + cellh - 62), tag, font=ft_, fill=(255, 255, 255))
        pages.append(pg)
    # back cover
    pg, d = new_page(dark=True)
    bk = Image.open(back_path)
    k = min(PW / bk.width, PH_ / bk.height)
    bk = bk.resize((int(bk.width * k), int(bk.height * k)), Image.LANCZOS)
    pg.paste(bk, ((PW - bk.width) // 2, (PH_ - bk.height) // 2))
    pages.append(pg)
    pdf_path = os.path.join(OUT, CFG['file_stem'] + ' - Instructions.pdf')
    pages[0].save(pdf_path, save_all=True, append_images=pages[1:], resolution=200, quality=82)
    out_files['pdf'] = pdf_path
    out_files['pages'] = len(pages)
    summary['files'] = out_files
    json.dump(summary, open(os.path.join(WORK, 'summary.json'), 'w'), indent=1)
    log('pages done', len(pages), 'pages')
    return summary


# ----------------------------------------------------------------------------------------------
if __name__ == '__main__':
    st = {'stage': STAGE, 'ok': False}
    try:
        if STAGE in ('extract', 'all'):
            stage_extract()
        sm = None
        if STAGE in ('build', 'all', 'build_pages'):
            sm = stage_build()
        if STAGE in ('pages', 'all', 'build_pages'):
            stage_pages(sm)
        st['ok'] = True
    except Exception:
        st['error'] = traceback.format_exc()
        log('FAILED', st['error'])
    json.dump(st, open(os.path.join(WORK, 'status_%s.json' % STAGE), 'w'))
