"""Minimal binary-FBX reader: objects, transforms, mesh bounding boxes (world space)."""
import struct, zlib, sys, math, json
import numpy as np


def parse(path):
    data = open(path, 'rb').read()
    assert data[:20] == b'Kaydara FBX Binary  ', 'not a binary FBX'
    ver = struct.unpack_from('<I', data, 23)[0]
    wide = ver >= 7500

    def prop(o):
        t = chr(data[o]); o += 1
        if t == 'Y': return struct.unpack_from('<h', data, o)[0], o + 2
        if t == 'C': return data[o] != 0, o + 1
        if t == 'I': return struct.unpack_from('<i', data, o)[0], o + 4
        if t == 'F': return struct.unpack_from('<f', data, o)[0], o + 4
        if t == 'D': return struct.unpack_from('<d', data, o)[0], o + 8
        if t == 'L': return struct.unpack_from('<q', data, o)[0], o + 8
        if t in 'SR':
            n = struct.unpack_from('<I', data, o)[0]; o += 4
            raw = data[o:o + n]; o += n
            return (raw.decode('utf-8', 'replace') if t == 'S' else raw), o
        if t in 'fdlibc':
            n, enc, clen = struct.unpack_from('<III', data, o); o += 12
            raw = data[o:o + clen]; o += clen
            if enc == 1: raw = zlib.decompress(raw)
            dt = {'f': '<f4', 'd': '<f8', 'l': '<i8', 'i': '<i4', 'b': 'u1', 'c': 'u1'}[t]
            return np.frombuffer(raw, dtype=dt, count=n), o
        raise ValueError('bad prop type ' + t)

    def node(o):
        if wide:
            end, nprops, plen = struct.unpack_from('<QQQ', data, o); o += 24
        else:
            end, nprops, plen = struct.unpack_from('<III', data, o); o += 12
        nl = data[o]; o += 1
        name = data[o:o + nl].decode('ascii', 'replace'); o += nl
        if end == 0: return None, o
        props = []
        for _ in range(nprops):
            v, o = prop(o); props.append(v)
        kids = []
        while o < end:
            k, o2 = node(o)
            if k is None: o = o2; break
            kids.append(k); o = o2
        return (name, props, kids), end

    o = 27; top = []
    while o < len(data) - 200:
        n, o2 = node(o)
        if n is None: break
        top.append(n); o = o2
    return top


def child(n, name):
    for k in n[2]:
        if k[0] == name: return k
    return None


def props70(n):
    out = {}
    p = child(n, 'Properties70')
    if p:
        for k in p[2]:
            if k[0] == 'P': out[k[1][0]] = k[1][4:]
    return out


def euler_xyz(r):
    rx, ry, rz = [math.radians(v) for v in r]
    cx, sx, cy, sy, cz, sz = math.cos(rx), math.sin(rx), math.cos(ry), math.sin(ry), math.cos(rz), math.sin(rz)
    Rx = np.array([[1, 0, 0], [0, cx, -sx], [0, sx, cx]]); Ry = np.array([[cy, 0, sy], [0, 1, 0], [-sy, 0, cy]]); Rz = np.array([[cz, -sz, 0], [sz, cz, 0], [0, 0, 1]])
    return Rz @ Ry @ Rx


def scene(path):
    top = parse(path)
    objs = child([n for n in top if n[0] == 'Objects'][0], None) if False else [n for n in top if n[0] == 'Objects'][0]
    conns = [n for n in top if n[0] == 'Connections'][0]
    gs = [n for n in top if n[0] == 'GlobalSettings']
    unit = 1.0
    if gs:
        p = props70(gs[0]); unit = float(p.get('UnitScaleFactor', [1.0])[0]) if 'UnitScaleFactor' in p else 1.0
    models, geoms = {}, {}
    for n in objs[2]:
        uid, nm = n[1][0], n[1][1].split('\x00')[0]
        if n[0] == 'Model':
            p = props70(n)
            g = lambda k, d: np.array(p[k][:3], float) if k in p else np.array(d, float)
            M = np.eye(4)
            R = euler_xyz(g('PreRotation', (0, 0, 0))) @ euler_xyz(g('Lcl Rotation', (0, 0, 0)))
            M[:3, :3] = R * g('Lcl Scaling', (1, 1, 1)); M[:3, 3] = g('Lcl Translation', (0, 0, 0))
            G = np.eye(4); G[:3, :3] = euler_xyz(g('GeometricRotation', (0, 0, 0))) * g('GeometricScaling', (1, 1, 1)); G[:3, 3] = g('GeometricTranslation', (0, 0, 0))
            models[uid] = {'name': nm, 'type': n[1][2], 'M': M, 'G': G, 'parent': None, 'geoms': []}
        elif n[0] == 'Geometry':
            v = child(n, 'Vertices')
            if v is not None:
                geoms[uid] = np.asarray(v[1][0], float).reshape(-1, 3)
                pv = child(n, 'PolygonVertexIndex'); geoms[uid] = (geoms[uid], np.asarray(pv[1][0]) if pv else None)
    for c in conns[2]:
        if c[0] != 'C' or c[1][0] != 'OO': continue
        a, b = c[1][1], c[1][2]
        if a in geoms and b in models: models[b]['geoms'].append(a)
        elif a in models and b in models: models[a]['parent'] = b
    def world(uid):
        m = models[uid]; W = m['M']
        while m['parent'] in models:
            m = models[m['parent']]; W = m['M'] @ W
        return W
    out = []
    for uid, m in models.items():
        for gid in m['geoms']:
            V, P = geoms[gid]
            W = world(uid) @ m['G']
            Vw = (V @ W[:3, :3].T + W[:3, 3]) / 100.0 * unit if unit != 100 else (V @ W[:3, :3].T + W[:3, 3])
            out.append({'name': m['name'], 'verts': Vw, 'poly': P})
    return out, unit


if __name__ == '__main__':
    objs, unit = scene(sys.argv[1])
    print('unit', unit, 'meshes', len(objs))
    for o in objs:
        v = o['verts']; mn, mx = v.min(0), v.max(0)
        print(f"{o['name'][:44]:44s} n={len(v):7d} min={np.round(mn,1)} max={np.round(mx,1)}")
