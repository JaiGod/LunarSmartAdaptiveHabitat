"""LUNA-X Adaptive Wellness Glove - build script (runs in a background Blender).

Builds the glove from scratch into its OWN new file and never touches any other .blend.
Usage: blender --background --factory-startup --python 01_build.py -- <glove_dir> [render]
"""
import bpy, bmesh, sys, os, json, math, time, traceback
import numpy as np
from mathutils import Vector, Matrix

ARGS = sys.argv[sys.argv.index("--") + 1:]
GLOVE_DIR = ARGS[0]
DO_RENDER = "render" in ARGS
sys.path.insert(0, os.path.join(GLOVE_DIR, "scripts"))
import lx_sdf as S

TARGET = os.path.join(GLOVE_DIR, "LUNA-X_Wellness_Glove.blend")
assert os.path.basename(TARGET) == "LUNA-X_Wellness_Glove.blend"
RENDER_DIR = os.path.join(GLOVE_DIR, "renders")
STATUS = {"steps": []}


def log(msg):
    STATUS["steps"].append(f"{time.strftime('%H:%M:%S')} {msg}")
    print("[LX]", msg, flush=True)


# ----------------------------------------------------------------------------
# scene / collections
# ----------------------------------------------------------------------------
bpy.ops.wm.read_factory_settings(use_empty=True)
SC = bpy.context.scene
SC.name = "LUNA-X Glove"
SC.render.fps = 30
SC.frame_start, SC.frame_end = 1, 810
SC.unit_settings.system = 'METRIC'


def coll(name, parent=None):
    c = bpy.data.collections.new(name)
    (parent or SC.collection).children.link(c)
    return c


C_ROOT = coll("LUNAX_Glove")
C_BODY = coll("Glove_Body", C_ROOT)
C_HW = coll("Glove_Hardware", C_ROOT)
C_LED = coll("Glove_Lights_LED", C_ROOT)
C_RIG = coll("Glove_Rig", C_ROOT)
C_STAGE = coll("Studio")


def new_obj(name, me, c):
    o = bpy.data.objects.new(name, me)
    c.objects.link(o)
    return o


def mesh_from(name, verts, faces):
    me = bpy.data.meshes.new(name)
    me.from_pydata([tuple(v) for v in verts], [], [tuple(f) for f in faces])
    me.validate()
    me.update()
    return me


def shade(me, angle=35):
    me.shade_smooth()
    if hasattr(me, "set_sharp_from_angle"):
        me.set_sharp_from_angle(angle=math.radians(angle))


# ----------------------------------------------------------------------------
# materials
# ----------------------------------------------------------------------------
def new_mat(name):
    m = bpy.data.materials.new(name)
    if m.node_tree is None:
        m.use_nodes = True
    return m


def bsdf(m):
    return next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')


def setp(b, **kw):
    alias = {"base": "Base Color", "metal": "Metallic", "rough": "Roughness", "coat": "Coat Weight",
             "coat_rough": "Coat Roughness", "sheen": "Sheen Weight", "sheen_tint": "Sheen Tint",
             "aniso": "Anisotropic", "spec": "Specular IOR Level", "trans": "Transmission Weight",
             "ior": "IOR", "alpha": "Alpha", "emit": "Emission Color", "emit_s": "Emission Strength"}
    for k, v in kw.items():
        s = b.inputs.get(alias.get(k, k))
        if s is None:
            continue
        if isinstance(v, (tuple, list)) and len(v) == 3:
            v = (*v, 1.0)
        s.default_value = v


def simple_mat(name, **kw):
    m = new_mat(name)
    setp(bsdf(m), **kw)
    return m


class G:
    """Tiny node-graph builder."""
    def __init__(self, nt):
        self.nt, self.N, self.L = nt, nt.nodes, nt.links

    def node(self, t, **props):
        n = self.N.new(t)
        for k, v in props.items():
            setattr(n, k, v)
        return n

    def link(self, a, b):
        self.L.new(a, b)

    def put(self, sock, v):
        if hasattr(v, "is_output"):
            self.link(v, sock)
        else:
            sock.default_value = v

    def math(self, op, a, b=0.0, c=None, clamp=False):
        n = self.node("ShaderNodeMath", operation=op, use_clamp=clamp)
        self.put(n.inputs[0], a)
        self.put(n.inputs[1], b)
        if c is not None:
            self.put(n.inputs[2], c)
        return n.outputs[0]

    def mix(self, fac, a, b):
        n = self.node("ShaderNodeMix", data_type='RGBA')
        self.put(n.inputs["Factor"], fac)
        ins = [s for s in n.inputs if s.type == 'RGBA']
        self.put(ins[0], a if hasattr(a, "is_output") else (*a, 1.0))
        self.put(ins[1], b if hasattr(b, "is_output") else (*b, 1.0))
        return next(s for s in n.outputs if s.type == 'RGBA')

    def attr(self, name):
        n = self.node("ShaderNodeAttribute", attribute_name=name)
        return n.outputs["Fac"]

    def tex_coord(self, which="Object"):
        n = self.node("ShaderNodeTexCoord")
        return n.outputs[which]


def mat_glove_tmg():
    m = new_mat("LX_Glove_TMG_Fabric")
    g = G(m.node_tree)
    b = bsdf(m)
    grip, seam, groove = g.attr("grip"), g.attr("seam"), g.attr("groove")
    co = g.tex_coord("Object")
    # fabric weave
    w1 = g.node("ShaderNodeTexWave", wave_type='BANDS', bands_direction='X')
    w1.inputs["Scale"].default_value = 900.0
    w2 = g.node("ShaderNodeTexWave", wave_type='BANDS', bands_direction='Y')
    w2.inputs["Scale"].default_value = 900.0
    g.link(co, w1.inputs["Vector"]); g.link(co, w2.inputs["Vector"])
    weave = g.math('MULTIPLY', w1.outputs["Fac"], w2.outputs["Fac"])
    # grip hex dots
    vor = g.node("ShaderNodeTexVoronoi", feature='DISTANCE_TO_EDGE')
    vor.inputs["Scale"].default_value = 520.0
    g.link(co, vor.inputs["Vector"])
    hexh = g.math('MULTIPLY', vor.outputs["Distance"], 3.0, clamp=True)
    # big soft wrinkles
    nz = g.node("ShaderNodeTexNoise")
    nz.inputs["Scale"].default_value = 45.0
    nz.inputs["Detail"].default_value = 4.0
    g.link(co, nz.inputs["Vector"])
    height = g.math('ADD', g.math('MULTIPLY', weave, g.math('SUBTRACT', 1.0, grip)),
                    g.math('MULTIPLY', hexh, grip))
    height = g.math('ADD', height, g.math('MULTIPLY', nz.outputs["Fac"], 0.6))
    bump = g.node("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 0.22
    bump.inputs["Distance"].default_value = 0.0004
    g.link(height, bump.inputs["Height"])
    g.link(bump.outputs["Normal"], b.inputs["Normal"])
    # colour
    var = g.math('MULTIPLY', nz.outputs["Fac"], 0.08)
    fabric = g.mix(var, (0.70, 0.705, 0.69), (0.60, 0.605, 0.59))
    c1 = g.mix(g.math('MULTIPLY', seam, 0.85, clamp=True), fabric, (0.42, 0.44, 0.47))
    c2 = g.mix(g.math('MULTIPLY', groove, 0.55, clamp=True), c1, (0.30, 0.31, 0.33))
    c3 = g.mix(grip, c2, (0.055, 0.058, 0.064))
    g.link(c3, b.inputs["Base Color"])
    g.link(g.math('ADD', 0.80, g.math('MULTIPLY', grip, -0.28)), b.inputs["Roughness"])
    g.link(g.math('MULTIPLY', g.math('SUBTRACT', 1.0, grip), 0.6), b.inputs["Sheen Weight"])
    return m


def mat_carbon():
    m = new_mat("LX_CarbonFiber")
    g = G(m.node_tree)
    b = bsdf(m)
    co = g.tex_coord("Object")
    mp = g.node("ShaderNodeMapping")
    mp.inputs["Scale"].default_value = (700, 700, 700)
    g.link(co, mp.inputs["Vector"])
    chk = g.node("ShaderNodeTexChecker")
    chk.inputs["Scale"].default_value = 1.0
    g.link(mp.outputs[0], chk.inputs["Vector"])
    wx = g.node("ShaderNodeTexWave", wave_type='BANDS', bands_direction='X')
    wy = g.node("ShaderNodeTexWave", wave_type='BANDS', bands_direction='Y')
    for w in (wx, wy):
        w.inputs["Scale"].default_value = 6.0
        g.link(mp.outputs[0], w.inputs["Vector"])
    f = g.mix(chk.outputs["Fac"], wx.outputs["Color"], wy.outputs["Color"])
    sep = g.node("ShaderNodeSeparateColor")
    g.link(f, sep.inputs[0])
    tone = g.math('MULTIPLY', sep.outputs[0], 0.05)
    col = g.mix(tone, (0.006, 0.006, 0.007), (0.045, 0.047, 0.052))
    g.link(col, b.inputs["Base Color"])
    setp(b, rough=0.42, coat=0.6, coat_rough=0.08, metal=0.0, spec=0.3)
    bump = g.node("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 0.08
    g.link(sep.outputs[0], bump.inputs["Height"])
    g.link(bump.outputs["Normal"], b.inputs["Normal"])
    return m


def mat_brushed(name, base, rough, aniso=0.6):
    m = new_mat(name)
    g = G(m.node_tree)
    b = bsdf(m)
    setp(b, base=base, metal=1.0, aniso=aniso)
    nz = g.node("ShaderNodeTexNoise")
    nz.inputs["Scale"].default_value = 900.0
    nz.inputs["Detail"].default_value = 2.0
    co = g.tex_coord("Object")
    mp = g.node("ShaderNodeMapping")
    mp.inputs["Scale"].default_value = (1, 1, 60)
    g.link(co, mp.inputs[0]); g.link(mp.outputs[0], nz.inputs["Vector"])
    g.link(g.math('ADD', rough - 0.05, g.math('MULTIPLY', nz.outputs["Fac"], 0.1)), b.inputs["Roughness"])
    return m


# LED protocol -----------------------------------------------------------------
LED_STATES = [  # name, colour, meaning
    ("NOMINAL", (0.10, 1.00, 0.32), "steady glow - all systems normal"),
    ("LUNA_AI", (0.00, 0.78, 1.00), "breathing pulse - LUNA processing / scanning"),
    ("NAV", (1.00, 1.00, 1.00), "rotating chase - navigation guidance"),
    ("CREW_WELLNESS", (0.62, 0.22, 1.00), "double pulse - crew call / wellness check-in"),
    ("CAUTION", (1.00, 0.50, 0.00), "slow blink 1 Hz - caution"),
    ("WARNING", (1.00, 0.03, 0.02), "fast strobe 4 Hz - warning / emergency"),
]


def led_group():
    ng = bpy.data.node_groups.new("LX_LED_Protocol", "ShaderNodeTree")
    ng.interface.new_socket("Phase", in_out='INPUT', socket_type='NodeSocketFloat')
    ng.interface.new_socket("Color", in_out='OUTPUT', socket_type='NodeSocketColor')
    ng.interface.new_socket("Strength", in_out='OUTPUT', socket_type='NodeSocketFloat')
    g = G(ng)
    gi = g.node("NodeGroupInput")
    go = g.node("NodeGroupOutput")
    st = g.node("ShaderNodeValue"); st.name = st.label = "LED_State"
    fr = g.node("ShaderNodeValue"); fr.name = fr.label = "Frame"
    t = g.math('DIVIDE', fr.outputs[0], 30.0)
    s = st.outputs[0]
    TWO_PI = 6.283185
    p0 = g.math('ADD', 0.85, g.math('MULTIPLY', 0.15, g.math('SINE', g.math('MULTIPLY', t, TWO_PI * 0.5))))
    p1 = g.math('ADD', 0.55, g.math('MULTIPLY', 0.45, g.math('SINE', g.math('MULTIPLY', t, TWO_PI * 1.2))))
    ch = g.math('COSINE', g.math('MULTIPLY', g.math('SUBTRACT', gi.outputs["Phase"], g.math('MULTIPLY', t, 0.8)), TWO_PI))
    p2 = g.math('ADD', 0.08, g.math('MULTIPLY', 0.92, g.math('POWER', g.math('ADD', 0.5, g.math('MULTIPLY', ch, 0.5)), 8.0)))
    u = g.math('FRACT', g.math('DIVIDE', t, 1.2))
    a = g.math('LESS_THAN', u, 0.12)
    bb = g.math('MULTIPLY', g.math('GREATER_THAN', u, 0.22), g.math('LESS_THAN', u, 0.34))
    p3 = g.math('ADD', 0.1, g.math('MULTIPLY', 0.9, g.math('MAXIMUM', a, bb)))
    p4 = g.math('ADD', 0.15, g.math('MULTIPLY', 0.85, g.math('LESS_THAN', g.math('FRACT', t), 0.5)))
    p5 = g.math('ADD', 0.04, g.math('MULTIPLY', 0.96, g.math('LESS_THAN', g.math('FRACT', g.math('MULTIPLY', t, 4.0)), 0.5)))
    pats = [p0, p1, p2, p3, p4, p5]
    strength = None
    color = None
    for i, (nm, rgb, _) in enumerate(LED_STATES):
        sel = g.math('COMPARE', s, float(i), 0.5)
        term = g.math('MULTIPLY', sel, pats[i])
        strength = term if strength is None else g.math('ADD', strength, term)
        cn = g.node("ShaderNodeVectorMath", operation='SCALE')
        cn.inputs[0].default_value = rgb
        g.link(sel, cn.inputs["Scale"])
        color = cn.outputs[0] if color is None else _vadd(g, color, cn.outputs[0])
    g.link(color, go.inputs["Color"])
    g.link(strength, go.inputs["Strength"])
    return ng, st, fr


def _vadd(g, a, b):
    n = g.node("ShaderNodeVectorMath", operation='ADD')
    g.link(a, n.inputs[0]); g.link(b, n.inputs[1])
    return n.outputs[0]


def mat_led(name, grp, gain=30.0, chase=False):
    m = new_mat(name)
    g = G(m.node_tree)
    b = bsdf(m)
    setp(b, base=(0.02, 0.02, 0.02), rough=0.2, coat=1.0)
    gn = g.node("ShaderNodeGroup"); gn.node_tree = grp
    if chase:  # angle around the object's local Y axis -> 0..1
        sepn = g.node("ShaderNodeSeparateXYZ")
        g.link(g.tex_coord("Object"), sepn.inputs[0])
        ang = g.math('ARCTAN2', sepn.outputs["X"], sepn.outputs["Z"])
        g.link(g.math('ADD', g.math('DIVIDE', ang, 6.283185), 0.5), gn.inputs["Phase"])
    ga = g.node("ShaderNodeAttribute", attribute_type='OBJECT', attribute_name="lx_gain")
    g.link(gn.outputs["Color"], b.inputs["Emission Color"])
    g.link(g.math('MULTIPLY', g.math('MULTIPLY', gn.outputs["Strength"], gain), ga.outputs["Fac"]), b.inputs["Emission Strength"])
    return m


def mat_display(grp):
    """LUNA core display: animated rings in the state colour."""
    m = new_mat("LX_LUNA_Core_Display")
    g = G(m.node_tree)
    b = bsdf(m)
    setp(b, base=(0.0, 0.0, 0.0), rough=0.3)
    gn = g.node("ShaderNodeGroup"); gn.node_tree = grp
    co = g.tex_coord("Object")
    sep = g.node("ShaderNodeSeparateXYZ"); g.link(co, sep.inputs[0])
    r = g.math('SQRT', g.math('ADD', g.math('MULTIPLY', sep.outputs["X"], sep.outputs["X"]),
                              g.math('MULTIPLY', sep.outputs["Y"], sep.outputs["Y"])))
    rings = g.math('SINE', g.math('MULTIPLY', r, 1400.0))
    rings = g.math('GREATER_THAN', rings, 0.55)
    ang = g.math('ARCTAN2', sep.outputs["X"], sep.outputs["Y"])
    seg = g.math('GREATER_THAN', g.math('SINE', g.math('MULTIPLY', ang, 12.0)), -0.6)
    core = g.math('LESS_THAN', r, 0.0032)
    pat = g.math('MAXIMUM', g.math('MULTIPLY', rings, seg), core)
    g.link(gn.outputs["Color"], b.inputs["Emission Color"])
    ga = g.node("ShaderNodeAttribute", attribute_type='OBJECT', attribute_name="lx_gain")
    g.link(g.math('MULTIPLY', g.math('MULTIPLY', g.math('MULTIPLY', pat, gn.outputs["Strength"]), 18.0), ga.outputs["Fac"]), b.inputs["Emission Strength"])
    return m


def mat_fake_glass(name, tint=(0.85, 0.95, 1.0), opacity=0.18):
    m = new_mat(name)
    nt = m.node_tree
    for n in list(nt.nodes):
        if n.type == 'BSDF_PRINCIPLED':
            nt.nodes.remove(n)
    out = next(n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL')
    g = G(nt)
    tr = g.node("ShaderNodeBsdfTransparent")
    tr.inputs[0].default_value = (*tint, 1)
    gl = g.node("ShaderNodeBsdfPrincipled")
    setp(gl, base=(0.02, 0.02, 0.025), rough=0.03, coat=1.0, spec=1.0)
    fr = g.node("ShaderNodeFresnel"); fr.inputs[0].default_value = 1.5
    mx = g.node("ShaderNodeMixShader")
    g.link(g.math('ADD', opacity, g.math('MULTIPLY', fr.outputs[0], 0.7), clamp=True), mx.inputs[0])
    g.link(tr.outputs[0], mx.inputs[1]); g.link(gl.outputs[0], mx.inputs[2])
    g.link(mx.outputs[0], out.inputs["Surface"])
    if hasattr(m, "surface_render_method"):
        m.surface_render_method = 'BLENDED'
    return m


log("materials")
LEDG, LED_STATE_NODE, LED_FRAME_NODE = led_group()
M = {
    "tmg": mat_glove_tmg(),
    "carbon": mat_carbon(),
    "titanium": mat_brushed("LX_Titanium_Brushed", (0.60, 0.585, 0.56), 0.24),
    "gold": simple_mat("LX_Gold_Polished", base=(1.0, 0.72, 0.30), metal=1.0, rough=0.14),
    "gold_satin": simple_mat("LX_Gold_Satin", base=(0.93, 0.70, 0.38), metal=1.0, rough=0.30),
    "chrome": simple_mat("LX_Chrome", base=(0.95, 0.95, 0.96), metal=1.0, rough=0.06),
    "black_ano": simple_mat("LX_Black_Anodized", base=(0.022, 0.023, 0.028), metal=1.0, rough=0.30),
    "rubber": simple_mat("LX_Rubber_Black", base=(0.03, 0.03, 0.032), rough=0.62),
    "orange": simple_mat("LX_NASA_Orange", base=(1.0, 0.10, 0.0), rough=0.48, sheen=0.3),
    "cuff_fabric": None,
    "led": mat_led("LX_LED_Status", LEDG),
    "led_chase": mat_led("LX_LED_WristRing_Chase", LEDG, gain=35.0, chase=True),
    "display": mat_display(LEDG),
    "glass": mat_fake_glass("LX_Glass_Dome"),
    "sensor_glass": simple_mat("LX_Sensor_Window", base=(0.005, 0.006, 0.01), rough=0.05, coat=1.0, spec=1.0),
    "ir": simple_mat("LX_IR_Scanner", base=(0.02, 0.0, 0.0), rough=0.1, emit=(1.0, 0.1, 0.05), emit_s=0.0, coat=1.0),
}
_ir = M["ir"]; _g = G(_ir.node_tree)
_ga = _g.node("ShaderNodeAttribute", attribute_type='OBJECT', attribute_name="lx_gain")
_g.link(_g.math('MULTIPLY', _ga.outputs["Fac"], 40.0), bsdf(_ir).inputs["Emission Strength"])
M["cuff_fabric"] = M["tmg"]

# ----------------------------------------------------------------------------
# glove body: voxel-union of primitives, then projected onto the smooth SDF
# ----------------------------------------------------------------------------
log("body: primitives")
bm = bmesh.new()


def bm_sphere(center, radii, axes=None):
    r = bmesh.ops.create_uvsphere(bm, u_segments=20, v_segments=12, radius=1.0)
    A = np.eye(3) if axes is None else np.asarray(axes)
    for v in r["verts"]:
        q = np.array(v.co) * np.asarray(radii)
        v.co = Vector(center + q @ A)


for name in S.FINGER_ORDER + ["thumb"]:
    ch = S.SK[name]
    for i in range(3):
        a, b = ch["joints"][i], ch["joints"][i + 1]
        for t in np.linspace(0, 1, 6):
            rr = (ch["R"][i] + (ch["R"][i + 1] - ch["R"][i]) * t) * 0.97
            bm_sphere(a + (b - a) * t, (rr, rr, rr))
# palm block
for x in np.linspace(-0.032, 0.032, 5):
    for y in np.linspace(0.008, 0.088, 6):
        bm_sphere(np.array([x, y, 0.0005]), (0.017, 0.017, 0.0155))
for y in np.linspace(-0.022, 0.012, 5):
    bm_sphere(np.array([0.0, y, 0.0]), (0.036, 0.016, 0.029))
th = S.SK["thumb"]
bm_sphere(th["joints"][0] + th["dirs"][0] * 0.011 + np.array([0.011, -0.001, -0.004]), (0.022, 0.016, 0.012),
          np.stack([th["dirs"][0], S.nrm(np.cross(th["dirs"][0], th["ups"][0])), th["ups"][0]]))
me_crude = bpy.data.meshes.new("crude")
bm.to_mesh(me_crude)
bm.free()
crude = new_obj("crude", me_crude, C_BODY)
rm = crude.modifiers.new("vox", 'REMESH')
rm.mode = 'VOXEL'
rm.voxel_size = 0.0011
rm.adaptivity = 0.0
dg = bpy.context.evaluated_depsgraph_get()
me_body = bpy.data.meshes.new_from_object(crude.evaluated_get(dg))
bpy.data.objects.remove(crude)
bpy.data.meshes.remove(me_crude)
log(f"body: voxel mesh {len(me_body.vertices)} verts")

V = np.empty(len(me_body.vertices) * 3)
me_body.vertices.foreach_get("co", V)
V = V.reshape(-1, 3)
E = np.empty(len(me_body.edges) * 2, dtype=np.int64)
me_body.edges.foreach_get("vertices", E)
E = E.reshape(-1, 2)


def laplacian(X, E, lam=0.5):
    acc = np.zeros_like(X)
    cnt = np.zeros(len(X))
    np.add.at(acc, E[:, 0], X[E[:, 1]]); np.add.at(acc, E[:, 1], X[E[:, 0]])
    np.add.at(cnt, E[:, 0], 1); np.add.at(cnt, E[:, 1], 1)
    avg = acc / np.maximum(cnt, 1)[:, None]
    return X + lam * (avg - X)


V = S.project(V, iters=12)
for _ in range(3):
    V = laplacian(V, E, 0.5)
    V = S.project(V, iters=4)
me_body.vertices.foreach_set("co", V.ravel())
me_body.update()
# cut the body behind the collar (hidden inside the gauntlet) for a cleaner mesh
bmb = bmesh.new()
bmb.from_mesh(me_body)
dead = [v for v in bmb.verts if v.co.y < -0.021]
bmesh.ops.delete(bmb, geom=dead, context='VERTS')
bmb.to_mesh(me_body)
bmb.free()
me_body.name = "LX_Glove_Body"
shade(me_body, 180)
body = new_obj("LX_Glove_Body", me_body, C_BODY)
body.data.materials.append(M["tmg"])

# per-vertex attributes for the shader + weights later
V = np.empty(len(me_body.vertices) * 3); me_body.vertices.foreach_get("co", V); V = V.reshape(-1, 3)
comps = S.components(V)
N = S.normals(V)
masks = S.surface_masks(V, N, comps)
for key in ("grip", "seam", "groove"):
    a = me_body.attributes.new(key, 'FLOAT', 'POINT')
    a.data.foreach_set("value", masks[key].astype(np.float32))
log(f"body: projected {len(V)} verts")

# ----------------------------------------------------------------------------
# hardware helpers
# ----------------------------------------------------------------------------
def frame_at(p_guess, tangent):
    p, n = S.surface_point(np.asarray(p_guess, float))
    t = np.asarray(tangent, float)
    t = S.nrm(t - n * np.dot(t, n))
    return p, n, t, np.cross(n, t)


def patch(name, guess, tangent, su, sv, lift, thick, mat, c=C_HW, nu=14, nv=12, expo=4.0, bevel=0.0005):
    """Conformal plate that hugs the glove surface."""
    p, n, tu, tv = frame_at(guess, tangent)
    verts, faces = [], []
    for j in range(nv):
        for i in range(nu):
            u = -1 + 2 * i / (nu - 1)
            v = -1 + 2 * j / (nv - 1)
            s = max(abs(u), abs(v))
            if s > 1e-9:
                ang = math.atan2(v, u)
                rho = (abs(math.cos(ang)) ** expo + abs(math.sin(ang)) ** expo) ** (-1 / expo)
                rsq = 1 / max(abs(math.cos(ang)), abs(math.sin(ang)))
                u, v = u * rho / rsq, v * rho / rsq
            verts.append(p + tu * u * su / 2 + tv * v * sv / 2 + n * 0.012)
    P = S.project(np.array(verts), iters=10)
    Nn = S.normals(P)
    P = P + Nn * lift
    for j in range(nv - 1):
        for i in range(nu - 1):
            a = j * nu + i
            faces.append((a, a + 1, a + nu + 1, a + nu))
    me = mesh_from(name, P, faces)
    o = new_obj(name, me, c)
    sol = o.modifiers.new("thick", 'SOLIDIFY')
    sol.thickness = thick
    sol.offset = 1.0
    sol.use_even_offset = True
    if bevel > 0:
        bv = o.modifiers.new("bevel", 'BEVEL')
        bv.width = bevel
        bv.segments = 3
        bv.limit_method = 'ANGLE'
    shade(me, 40)
    me.materials.append(mat)
    return o, p, n


def place(o, loc, z_axis, y_axis=None):
    z = Vector(z_axis).normalized()
    y = Vector(y_axis if y_axis is not None else (0, 1, 0))
    y = (y - z * y.dot(z)).normalized()
    x = y.cross(z)
    Mx = Matrix((x, y, z)).transposed().to_4x4()
    Mx.translation = Vector(loc)
    o.matrix_world = Mx


def prim(name, kind, mat, c=C_HW, **kw):
    bmx = bmesh.new()
    if kind == 'cyl':
        bmesh.ops.create_cone(bmx, cap_ends=True, segments=kw.get("seg", 32), radius1=kw["r"], radius2=kw.get("r2", kw["r"]), depth=kw["h"])
    elif kind == 'torus':
        R, r, su, sv = kw["R"], kw["r"], kw.get("su", 48), kw.get("sv", 12)
        vs = []
        for i in range(su):
            a = 2 * math.pi * i / su
            for j in range(sv):
                b = 2 * math.pi * j / sv
                rr = R + r * math.cos(b)
                vs.append(bmx.verts.new((rr * math.cos(a), rr * math.sin(a), r * math.sin(b))))
        for i in range(su):
            for j in range(sv):
                a0 = i * sv + j; a1 = ((i + 1) % su) * sv + j
                b0 = i * sv + (j + 1) % sv; b1 = ((i + 1) % su) * sv + (j + 1) % sv
                bmx.faces.new((vs[a0], vs[a1], vs[b1], vs[b0]))
    elif kind == 'box':
        bmesh.ops.create_cube(bmx, size=1.0)
        for v in bmx.verts:
            v.co = Vector((v.co.x * kw["x"], v.co.y * kw["y"], v.co.z * kw["z"]))
    elif kind == 'sphere':
        bmesh.ops.create_uvsphere(bmx, u_segments=kw.get("seg", 24), v_segments=kw.get("ring", 12), radius=kw["r"])
        for v in bmx.verts:
            v.co.z *= kw.get("zs", 1.0)
    elif kind == 'hex':
        bmesh.ops.create_cone(bmx, cap_ends=True, segments=6, radius1=kw["r"], radius2=kw.get("r2", kw["r"]), depth=kw["h"])
    me = bpy.data.meshes.new(name)
    bmx.to_mesh(me); bmx.free()
    o = new_obj(name, me, c)
    me.materials.append(mat)
    if kw.get("bevel"):
        bv = o.modifiers.new("bevel", 'BEVEL'); bv.width = kw["bevel"]; bv.segments = 3; bv.limit_method = 'ANGLE'
    shade(me, kw.get("smooth", 35))
    return o


def lathe(name, profile, mat, c=C_HW, seg=128):
    """profile = [(radius, y), ...] closed loop, revolved around +Y."""
    verts, faces = [], []
    n = len(profile)
    for i in range(seg):
        a = 2 * math.pi * i / seg
        for r, y in profile:
            verts.append((r * math.sin(a), y, r * math.cos(a)))
    for i in range(seg):
        for j in range(n):
            a0 = i * n + j; a1 = ((i + 1) % seg) * n + j
            b0 = i * n + (j + 1) % n; b1 = ((i + 1) % seg) * n + (j + 1) % n
            faces.append((a0, b0, b1, a1))
    me = mesh_from(name, verts, faces)
    shade(me, 30)
    o = new_obj(name, me, c)
    me.materials.append(mat)
    return o


PARTS = {}   # object name -> bone name (for rig parenting)


def bone_of(o, b):
    PARTS[o.name] = b
    return o


# ----------------------------------------------------------------------------
# wrist disconnect ring (titanium + gold bearing + status LED band)
# ----------------------------------------------------------------------------
log("hardware: wrist ring")
ring = lathe("LX_Wrist_Ring_Titanium", [(0.0402, -0.0170), (0.0478, -0.0170), (0.0508, -0.0146), (0.0512, -0.0112),
                                        (0.0498, -0.0098), (0.0498, -0.0082), (0.0512, -0.0068), (0.0510, -0.0036),
                                        (0.0482, -0.0012), (0.0402, -0.0012)], M["titanium"])
bone_of(ring, "forearm")
race = prim("LX_Wrist_Bearing_Gold", 'torus', M["gold"], R=0.0499, r=0.0011, su=128, sv=12)
race.rotation_euler = (math.radians(90), 0, 0); race.location = (0, -0.0090, 0)
bone_of(race, "forearm")
band = lathe("LX_Wrist_Status_LED", [(0.0430, -0.0009), (0.0470, -0.0009), (0.0470, -0.0004), (0.0430, -0.0004)], M["led_chase"], C_LED)
bone_of(band, "forearm")
for i in range(12):
    a = 2 * math.pi * (i + 0.5) / 12
    if abs(math.sin(a)) > 0.97:
        continue
    b = prim(f"LX_Wrist_Bolt_{i:02d}", 'hex', M["chrome"], r=0.0017, h=0.0012, bevel=0.0002)
    nrm_ = (math.sin(a), 0, math.cos(a))
    place(b, (0.0514 * math.sin(a), -0.0136, 0.0514 * math.cos(a)), nrm_, (0, 1, 0))
    bone_of(b, "forearm")
for sgn in (-1, 1):  # locking buttons on both sides, NASA orange caps
    hous = prim(f"LX_Wrist_Lock_{'L' if sgn < 0 else 'R'}", 'box', M["titanium"], x=0.006, y=0.012, z=0.010, bevel=0.0012)
    hous.location = (sgn * 0.0528, -0.0092, 0.0)
    cap = prim(f"LX_Wrist_LockCap_{'L' if sgn < 0 else 'R'}", 'box', M["orange"], x=0.0025, y=0.008, z=0.006, bevel=0.0008)
    cap.location = (sgn * 0.0565, -0.0092, 0.0)
    bone_of(hous, "forearm"); bone_of(cap, "forearm")

# ----------------------------------------------------------------------------
# gauntlet (cuff) with fabric folds, orange band, arm interface ring
# ----------------------------------------------------------------------------
log("hardware: gauntlet")
ys = np.linspace(-0.0150, -0.1520, 70)
segs = 96
gv, gf = [], []
for k, y in enumerate(ys):
    tt = (-y - 0.015) / 0.137
    rx = 0.0438 + 0.017 * tt ** 1.3
    rz = 0.0438 + 0.011 * tt ** 1.3
    fold = 0.0012 * math.cos((-y) * 2 * math.pi / 0.021) * (1 - tt) ** 0.7
    for i in range(segs):
        a = 2 * math.pi * i / segs
        wob = 0.0007 * math.sin(7 * a + y * 90) * tt
        gv.append(((rx + fold + wob) * math.sin(a), y, (rz + fold + wob) * math.cos(a)))
for k in range(len(ys) - 1):
    for i in range(segs):
        a0 = k * segs + i; a1 = k * segs + (i + 1) % segs
        gf.append((a0, a0 + segs, a1 + segs, a1))
gme = mesh_from("LX_Gauntlet", gv, gf)
shade(gme, 180)
gaunt = new_obj("LX_Gauntlet", gme, C_BODY)
gme.materials.append(M["tmg"])
sol = gaunt.modifiers.new("thick", 'SOLIDIFY'); sol.thickness = 0.0025; sol.offset = -1
bone_of(gaunt, "forearm")
ob = lathe("LX_Gauntlet_Band_Orange", [(0.0590, -0.118), (0.0605, -0.1185), (0.0607, -0.1290), (0.0592, -0.1295)], M["orange"])
ob.scale = (1.0, 1.0, 0.905)
bone_of(ob, "forearm")
strap = lathe("LX_Gauntlet_Strap", [(0.0528, -0.060), (0.0545, -0.0605), (0.0548, -0.0735), (0.0530, -0.074)], M["rubber"])
strap.scale = (1.0, 1.0, 0.92)
bone_of(strap, "forearm")
buckle = prim("LX_Gauntlet_Buckle", 'box', M["titanium"], x=0.016, y=0.018, z=0.004, bevel=0.0012)
place(buckle, (0.0, -0.0672, 0.0518), (0, 0, 1))
bone_of(buckle, "forearm")
armring = lathe("LX_Arm_Interface_Ring", [(0.0612, -0.150), (0.0660, -0.150), (0.0672, -0.153), (0.0672, -0.161),
                                          (0.0650, -0.164), (0.0612, -0.164)], M["titanium"])
armring.scale = (1.0, 1.0, 0.91)
bone_of(armring, "forearm")
armgold = prim("LX_Arm_Bearing_Gold", 'torus', M["gold"], R=0.0673, r=0.0009, su=128, sv=10)
armgold.rotation_euler = (math.radians(90), 0, 0); armgold.location = (0, -0.157, 0); armgold.scale = (1, 0.91, 1)
bone_of(armgold, "forearm")
# cuff status display strip (curved OLED on the back of the cuff)
dv, df = [], []
NU, NV = 24, 5
for j in range(NV):
    y = -0.034 - 0.012 * j / (NV - 1)
    tt = (-y - 0.015) / 0.137
    rx = 0.0438 + 0.017 * tt ** 1.3 + 0.0012
    rz = 0.0438 + 0.011 * tt ** 1.3 + 0.0012
    for i in range(NU):
        ang = -0.42 + 0.84 * i / (NU - 1)
        dv.append((rx * math.sin(ang), y, rz * math.cos(ang)))
for j in range(NV - 1):
    for i in range(NU - 1):
        a0 = j * NU + i
        df.append((a0, a0 + NU, a0 + NU + 1, a0 + 1))
disp = new_obj("LX_Cuff_Status_Display", mesh_from("LX_Cuff_Status_Display", dv, df), C_LED)
disp.data.materials.append(M["led"])
sol = disp.modifiers.new("thick", 'SOLIDIFY'); sol.thickness = 0.0010; sol.offset = 1.0
bone_of(disp, "forearm")

# ----------------------------------------------------------------------------
# carbon knuckle guard, phalanx plates, LUNA core, sensors, LEDs
# ----------------------------------------------------------------------------
log("hardware: knuckle guard")
for name in S.FINGER_ORDER:
    ch = S.SK[name]
    J, D, U = ch["joints"], ch["dirs"], ch["ups"]
    side = S.nrm(np.cross(D[0], U[0]))
    w = 2.0 * ch["R"][0] * 0.86
    o, _, _ = patch(f"LX_Knuckle_Plate_{name}", J[0] + U[0] * 0.03 - D[0] * 0.002, side, w, 0.0165, 0.0010, 0.0034, M["carbon"])
    bone_of(o, "hand")
    o, _, _ = patch(f"LX_Phalanx_Plate_{name}", J[0] + D[0] * 0.56 * np.linalg.norm(J[1] - J[0]) + U[0] * 0.03,
                    side, 2.0 * ch["R"][1] * 0.78, np.linalg.norm(J[1] - J[0]) * 0.42, 0.0008, 0.0026, M["carbon"])
    bone_of(o, f"{name}_01")
    # hinge pin between knuckle plate and bridge
    pin = prim(f"LX_Knuckle_Hinge_{name}", 'cyl', M["titanium"], r=0.0013, h=w * 0.7, seg=16)
    p, n = S.surface_point(J[0] - D[0] * 0.0115 + U[0] * 0.03)
    place(pin, p + n * 0.0030, side, n)
    bone_of(pin, "hand")
bridge, _, _ = patch("LX_Knuckle_Bridge", (0.0, 0.0805, 0.04), (1, 0, 0), 0.084, 0.0085, 0.0010, 0.0030, M["carbon"], nu=30, nv=6, expo=5)
bone_of(bridge, "hand")

log("hardware: LUNA wellness processor")
cp, cn, ct, cb = frame_at((0.003, 0.043, 0.04), (0, 1, 0))
base, _, _ = patch("LX_Core_BasePlate", (0.003, 0.043, 0.04), (0, 1, 0), 0.050, 0.046, 0.0008, 0.0020, M["black_ano"], nu=20, nv=20, expo=2.6)
bone_of(base, "hand")
top = cp + cn * 0.0030
hous = prim("LX_Core_Housing", 'hex', M["titanium"], r=0.0205, r2=0.0185, h=0.0062, bevel=0.0009)
place(hous, top + cn * 0.0031, cn, ct)
bezel = prim("LX_Core_Bezel_Gold", 'torus', M["gold"], R=0.0152, r=0.0013, su=96, sv=12)
place(bezel, top + cn * 0.0064, cn, ct)
disp_core = prim("LX_Core_Display", 'cyl', M["display"], r=0.0146, h=0.0006, seg=64)
place(disp_core, top + cn * 0.0063, cn, ct)
dome = prim("LX_Core_Glass_Dome", 'sphere', M["glass"], r=0.0147, zs=0.32, seg=48, ring=16)
place(dome, top + cn * 0.0064, cn, ct)
for o in (hous, bezel, disp_core, dome):
    bone_of(o, "hand")
for i in range(6):
    a = 2 * math.pi * (i + 0.5) / 6
    sc_ = prim(f"LX_Core_Screw_{i}", 'cyl', M["gold_satin"], r=0.0013, h=0.0008, seg=16, bevel=0.0002)
    loc = top + cn * 0.0063 + (ct * math.cos(a) + cb * math.sin(a)) * 0.0178
    place(sc_, loc, cn, ct)
    bone_of(sc_, "hand")
# LUNA text on the display
fc = bpy.data.curves.new("LX_Core_Text", 'FONT')
fc.body = "LUNA"
fc.size = 0.0042
fc.align_x = 'CENTER'; fc.align_y = 'CENTER'
fc.extrude = 0.00008
txt = new_obj("LX_Core_Text", fc, C_LED)
txt.data.materials.append(M["display"])
place(txt, top + cn * 0.0067 - ct * 0.0072, cn, ct)
bone_of(txt, "hand")
# rotating sensor ring around the core
sring = prim("LX_Core_Sensor_Ring", 'torus', M["titanium"], R=0.0245, r=0.0014, su=96, sv=12)
place(sring, cp + cn * 0.0045, cn, ct)
bone_of(sring, "hand")
for i in range(12):
    a = 2 * math.pi * i / 12
    em = prim(f"LX_Core_Ring_Emitter_{i:02d}", 'sphere', M["led"], C_LED, r=0.00105, seg=12, ring=8)
    em.parent = sring
    em.location = (0.0245 * math.cos(a), 0.0245 * math.sin(a), 0.0012)
data_port = prim("LX_Core_DataPort", 'box', M["black_ano"], x=0.007, y=0.003, z=0.0025, bevel=0.0004)
place(data_port, top - ct * 0.0205 + cn * 0.0015, cn, ct)
bone_of(data_port, "hand")

log("hardware: finger systems")
for name in S.FINGER_ORDER + ["thumb"]:
    ch = S.SK[name]
    J, D, U, R = ch["joints"], ch["dirs"], ch["ups"], ch["R"]
    # LED ring around the base of the proximal segment
    t0 = 0.30 if name != "thumb" else 0.35
    pos = J[0] + (J[1] - J[0]) * t0
    rr = R[0] + (R[1] - R[0]) * t0
    lr = prim(f"LX_Finger_LED_Ring_{name}", 'torus', M["led"], C_LED, R=rr + 0.0006, r=0.00075, su=48, sv=8)
    place(lr, pos, D[0], U[0])
    bone_of(lr, f"{name}_01")
    # nail status light on the distal segment
    o, _, _ = patch(f"LX_Tip_Status_LED_{name}", J[2] + D[2] * 0.55 * np.linalg.norm(J[3] - J[2]) + U[2] * 0.03, S.nrm(np.cross(D[2], U[2])),
                    0.0042, 0.0075, 0.0004, 0.0006, M["led"], C_LED, nu=8, nv=8, expo=3, bevel=0.0)
    bone_of(o, f"{name}_03")
    # haptic pod on the middle segment
    p, n = S.surface_point(J[1] + D[1] * 0.5 * np.linalg.norm(J[2] - J[1]) + U[1] * 0.03)
    pod = prim(f"LX_Haptic_Pod_{name}", 'cyl', M["black_ano"], r=0.0026, h=0.0014, seg=24, bevel=0.0003)
    place(pod, p + n * 0.0005, n, D[1])
    rim = prim(f"LX_Haptic_Rim_{name}", 'torus', M["gold"], R=0.0026, r=0.00035, su=32, sv=6)
    place(rim, p + n * 0.0012, n, D[1])
    bone_of(pod, f"{name}_02"); bone_of(rim, f"{name}_02")
    # gold sensor pads on the palmar side of the fingertips (thumb, index, middle)
    if name in ("thumb", "index", "middle"):
        o, pp, pn = patch(f"LX_Tip_Sensor_Gold_{name}", J[2] + D[2] * 0.55 * np.linalg.norm(J[3] - J[2]) - U[2] * 0.03,
                          S.nrm(np.cross(D[2], U[2])), 0.0095, 0.0110, 0.0004, 0.0006, M["gold"], nu=10, nv=10, expo=2.2, bevel=0.0)
        bone_of(o, f"{name}_03")
        o, _, _ = patch(f"LX_Tip_Sensor_Window_{name}", J[2] + D[2] * 0.55 * np.linalg.norm(J[3] - J[2]) - U[2] * 0.03,
                        S.nrm(np.cross(D[2], U[2])), 0.0045, 0.0060, 0.0011, 0.0004, M["sensor_glass"], nu=8, nv=8, expo=2.2, bevel=0.0)
        bone_of(o, f"{name}_03")
# non-contact IR / spectral scanner in the index fingertip
ch = S.SK["index"]
p, n = S.surface_point(ch["joints"][3] + ch["dirs"][2] * 0.02)
lens = prim("LX_Index_IR_Scanner", 'cyl', M["ir"], r=0.0024, h=0.0012, seg=32, bevel=0.0002)
place(lens, p + n * 0.0003, n, ch["ups"][2])
lring = prim("LX_Index_IR_Ring", 'torus', M["led"], C_LED, R=0.0030, r=0.00045, su=32, sv=8)
place(lring, p + n * 0.0006, n, ch["ups"][2])
bone_of(lens, "index_03"); bone_of(lring, "index_03")

# ----------------------------------------------------------------------------
# armature + skin weights
# ----------------------------------------------------------------------------
log("rig")
arm_d = bpy.data.armatures.new("LX_Glove_Rig")
arm = new_obj("LX_Glove_Rig", arm_d, C_RIG)
arm.show_in_front = True
arm_d.display_type = 'OCTAHEDRAL'
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='EDIT')
eb = arm_d.edit_bones
root = eb.new("root"); root.head = (0, -0.20, 0); root.tail = (0, -0.17, 0)
fa = eb.new("forearm"); fa.head = (0, -0.16, 0); fa.tail = tuple(S.WRIST_PIVOT); fa.parent = root
hd = eb.new("hand"); hd.head = tuple(S.WRIST_PIVOT); hd.tail = (0, 0.072, 0.002); hd.parent = fa; hd.use_connect = True
for e in (root, fa, hd):
    e.align_roll(Vector((0, 0, 1)))
for name in S.FINGER_ORDER + ["thumb"]:
    ch = S.SK[name]
    prev = hd
    for i in range(3):
        b = eb.new(f"{name}_0{i + 1}")
        b.head = tuple(ch["joints"][i]); b.tail = tuple(ch["joints"][i + 1])
        b.align_roll(Vector(ch["ups"][i]))
        b.parent = prev
        b.use_connect = i > 0
        prev = b
bpy.ops.object.mode_set(mode='OBJECT')

Vb = np.empty(len(me_body.vertices) * 3); me_body.vertices.foreach_get("co", Vb); Vb = Vb.reshape(-1, 3)
W = S.skin_weights(Vb)
# smooth weights on the surface to remove any seams
Eb = np.empty(len(me_body.edges) * 2, dtype=np.int64); me_body.edges.foreach_get("vertices", Eb); Eb = Eb.reshape(-1, 2)
Wm = np.stack([W[b] for b in S.BONES], axis=1)
for _ in range(6):
    Wm = laplacian(Wm, Eb, 0.5)
Wm /= np.maximum(Wm.sum(axis=1, keepdims=True), 1e-9)
for k, bname in enumerate(S.BONES):
    vg = body.vertex_groups.new(name=bname)
    w = Wm[:, k]
    idx = np.nonzero(w > 0.002)[0]
    for wv in np.unique(np.round(w[idx], 3)):
        sel = idx[np.round(w[idx], 3) == wv]
        vg.add(sel.tolist(), float(wv), 'REPLACE')
amod = body.modifiers.new("Armature", 'ARMATURE'); amod.object = arm; amod.use_deform_preserve_volume = True
body.parent = arm

bpy.context.view_layer.update()
for oname, bname in PARTS.items():
    o = bpy.data.objects[oname]
    mw = o.matrix_world.copy()
    bone = arm_d.bones[bname]
    o.parent = arm
    o.parent_type = 'BONE'
    o.parent_bone = bname
    pm = arm.matrix_world @ bone.matrix_local @ Matrix.Translation((0, bone.length, 0))
    o.matrix_parent_inverse = pm.inverted()
    o.matrix_basis = mw
for pb in arm.pose.bones:
    pb.rotation_mode = 'XYZ'

# controller (drives the LED protocol)
ctrl = bpy.data.objects.new("LUNAX_Controller", None)
C_RIG.objects.link(ctrl)
ctrl.empty_display_type = 'SPHERE'; ctrl.empty_display_size = 0.012
ctrl.location = (0.09, -0.06, 0.06)
ctrl["led_state"] = 0
ui = ctrl.id_properties_ui("led_state")
ui.update(min=0, max=5, description="0 NOMINAL green, 1 LUNA cyan, 2 NAV white, 3 CREW/WELLNESS purple, 4 CAUTION amber, 5 WARNING red")
fcv = LED_STATE_NODE.outputs[0].driver_add("default_value")
v = fcv.driver.variables.new(); v.name = "s"; v.type = 'SINGLE_PROP'
v.targets[0].id_type = 'OBJECT'; v.targets[0].id = ctrl; v.targets[0].data_path = '["led_state"]'
fcv.driver.expression = "s"
fcf = LED_FRAME_NODE.outputs[0].driver_add("default_value")
fcf.driver.expression = "frame"
STATUS["drivers_simple"] = [fcv.driver.is_simple_expression, fcf.driver.is_simple_expression]

# ----------------------------------------------------------------------------
# studio: world, lights, floor, cameras
# ----------------------------------------------------------------------------
log("studio")
world = bpy.data.worlds.new("LX_Studio_World")
SC.world = world
if world.node_tree is None:
    world.use_nodes = True
wn = world.node_tree
g = G(wn)
bgn = next(n for n in wn.nodes if n.type == 'BACKGROUND')
hdri = None
wdir = bpy.utils.system_resource('DATAFILES', path="studiolights/world")
for cand in ("interior.exr", "studio.exr", "city.exr", "forest.exr"):
    if os.path.exists(os.path.join(wdir, cand)):
        hdri = os.path.join(wdir, cand)
        break
if hdri:
    env = g.node("ShaderNodeTexEnvironment")
    env.image = bpy.data.images.load(hdri)
    g.link(env.outputs["Color"], bgn.inputs["Color"])
bgn.inputs["Strength"].default_value = 0.30
lp = g.node("ShaderNodeLightPath")
dark = g.node("ShaderNodeBackground"); dark.inputs["Color"].default_value = (0.004, 0.006, 0.012, 1)
mx = g.node("ShaderNodeMixShader")
out = next(n for n in wn.nodes if n.type == 'OUTPUT_WORLD')
g.link(lp.outputs["Is Camera Ray"], mx.inputs[0])
g.link(bgn.outputs[0], mx.inputs[1]); g.link(dark.outputs[0], mx.inputs[2])
g.link(mx.outputs[0], out.inputs["Surface"])
STATUS["hdri"] = hdri


def light(name, kind, loc, target, energy, size, color):
    ld = bpy.data.lights.new(name, kind)
    ld.energy = energy
    ld.color = color
    if kind == 'AREA':
        ld.size = size
        ld.shape = 'DISK'
    o = new_obj(name, ld, C_STAGE)
    o.location = loc
    d = Vector(target) - Vector(loc)
    o.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    return o


T = (0.0, 0.03, 0.0)
light("Key_Light", 'AREA', (-0.45, -0.25, 0.55), T, 16.0, 0.35, (1.0, 0.96, 0.9))
light("Fill_Light", 'AREA', (0.55, -0.15, 0.25), T, 5.0, 0.5, (0.78, 0.86, 1.0))
light("Rim_Light", 'AREA', (0.10, 0.60, 0.35), T, 28.0, 0.25, (0.85, 0.92, 1.0))
light("Under_Bounce", 'AREA', (0.0, 0.05, -0.40), T, 2.0, 0.6, (1.0, 0.9, 0.85))
fl = bpy.data.meshes.new("Floor")
fl.from_pydata([(-2, -2, 0), (2, -2, 0), (2, 2, 0), (-2, 2, 0)], [], [(0, 1, 2, 3)])
floor = new_obj("Studio_Floor", fl, C_STAGE)
floor.location = (0, 0, -0.075)
fl.materials.append(simple_mat("LX_Studio_Floor", base=(0.006, 0.007, 0.009), rough=0.45, spec=0.3))


def camera(name, loc, target, lens=85, focus=None, fstop=4.0):
    cd = bpy.data.cameras.new(name)
    cd.lens = lens
    cd.clip_start = 0.01
    cd.dof.use_dof = True
    cd.dof.aperture_fstop = fstop
    cd.dof.focus_distance = (Vector(focus or target) - Vector(loc)).length
    o = new_obj(name, cd, C_STAGE)
    o.location = loc
    o.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    return o


cams = {
    "hero": camera("CAM_Hero", (-0.46, -0.30, 0.40), (0.0, 0.05, 0.0), 58, fstop=7.0),
    "back": camera("CAM_Top_Back", (0.0, 0.03, 0.62), (0.0, 0.03, 0.0), 60, fstop=8),
    "palm": camera("CAM_Palm", (0.12, 0.10, -0.50), (0.0, 0.05, 0.0), 60, fstop=8),
    "core": camera("CAM_Core_Closeup", (-0.10, -0.06, 0.20), (0.003, 0.043, 0.02), 100, fstop=4),
    "side": camera("CAM_Thumb_Side", (-0.60, 0.06, 0.10), (0.0, 0.0, 0.0), 60, fstop=8),
}
SC.camera = cams["hero"]

# render settings
r = SC.render
r.engine = 'BLENDER_EEVEE'
r.resolution_x, r.resolution_y = 1600, 900
r.film_transparent = False
try:
    SC.view_settings.view_transform = 'AgX'
    SC.view_settings.look = 'AgX - Medium High Contrast'
except Exception:
    pass
ee = SC.eevee
for attr, val in (("use_raytracing", True), ("taa_render_samples", 64), ("use_shadows", True), ("use_bloom", True)):
    if hasattr(ee, attr):
        try:
            setattr(ee, attr, val)
        except Exception:
            pass

# every emissive object gets an animatable brightness ("lx_gain", 0..1)
EMISSIVE = {M["led"], M["led_chase"], M["display"], M["ir"]}
for o in bpy.data.objects:
    if o.type in ('MESH', 'FONT') and any(ms.material in EMISSIVE for ms in o.material_slots):
        o["lx_gain"] = 0.0 if o.name == "LX_Index_IR_Scanner" else 1.0

# finished file -----------------------------------------------------------------
SC.frame_set(1)
STATUS["objects"] = len(bpy.data.objects)
STATUS["body_verts"] = len(me_body.vertices)
bpy.ops.wm.save_as_mainfile(filepath=TARGET, check_existing=False)
log("saved " + TARGET)

if DO_RENDER:
    for key, cam in cams.items():
        SC.camera = cam
        r.filepath = os.path.join(RENDER_DIR, f"glove_{key}.png")
        t0 = time.time()
        bpy.ops.render.render(write_still=True)
        log(f"render {key} {time.time() - t0:.1f}s")
    SC.camera = cams["hero"]
