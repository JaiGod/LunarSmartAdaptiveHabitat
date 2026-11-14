"""LUNA-X Adaptive Wellness Glove - shape math (pure numpy, no bpy).

Coordinate frame (right glove, rest pose):
  origin = wrist centre, +Y = toward fingertips, +Z = back of hand, thumb on -X.
Units: metres.
"""
import numpy as np
from math import radians, sin, cos

# ----------------------------------------------------------------------------
# small vector helpers
# ----------------------------------------------------------------------------
def nrm(v):
    v = np.asarray(v, dtype=float)
    return v / np.linalg.norm(v)


def rot(axis, ang):
    """Rodrigues rotation matrix (right-hand rule, radians)."""
    x, y, z = nrm(axis)
    c, s = cos(ang), sin(ang)
    C = 1.0 - c
    return np.array([
        [c + x * x * C, x * y * C - z * s, x * z * C + y * s],
        [y * x * C + z * s, c + y * y * C, y * z * C - x * s],
        [z * x * C - y * s, z * y * C + x * s, c + z * z * C],
    ])


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


# ----------------------------------------------------------------------------
# skeleton (pressurised EVA-glove proportions: a bit bigger than a bare hand)
# ----------------------------------------------------------------------------
FINGER_SPECS = {
    #          MCP joint centre            segment lengths         radii at MCP, PIP, DIP, tip     splay  flex(deg)
    "index":  dict(mcp=(-0.0378, 0.0945, 0.0035), L=(0.0420, 0.0250, 0.0220), R=(0.0114, 0.0108, 0.0101, 0.0095), splay=6.5,  flex=(5.0, 9.0, 6.0)),
    "middle": dict(mcp=(-0.0126, 0.0990, 0.0045), L=(0.0460, 0.0290, 0.0230), R=(0.0117, 0.0111, 0.0104, 0.0098), splay=1.5,  flex=(5.0, 9.0, 6.0)),
    "ring":   dict(mcp=(0.0124, 0.0960, 0.0035),  L=(0.0430, 0.0270, 0.0220), R=(0.0112, 0.0106, 0.0099, 0.0093), splay=-4.5, flex=(6.0, 10.0, 7.0)),
    "pinky":  dict(mcp=(0.0362, 0.0875, 0.0015),  L=(0.0340, 0.0210, 0.0190), R=(0.0101, 0.0095, 0.0090, 0.0085), splay=-12.0, flex=(8.0, 12.0, 8.0)),
}
THUMB_SPEC = dict(cmc=(-0.0255, 0.0230, -0.0035), L=(0.0400, 0.0310, 0.0260),
                  R=(0.0158, 0.0130, 0.0122, 0.0112), yaw=47.0, pitch=7.0,
                  flex=(6.0, 14.0, 12.0), dorsal_guess=(-0.66, 0.05, 0.75))
FINGER_ORDER = ["index", "middle", "ring", "pinky"]
WRIST_PIVOT = np.array([0.0, 0.004, 0.0])


def build_chain(start, d0, up0, lengths, flex_deg):
    """Return joints (4), per-segment dirs (3) and per-segment dorsal 'up' vectors (3)."""
    joints = [np.asarray(start, float)]
    dirs, ups = [], []
    d, up = nrm(d0), nrm(up0)
    up = nrm(up - d * np.dot(up, d))
    for L, fl in zip(lengths, flex_deg):
        side = nrm(np.cross(d, up))
        R = rot(side, -radians(fl))       # negative = curl toward palm side
        d = R @ d
        up = R @ up
        dirs.append(d.copy())
        ups.append(up.copy())
        joints.append(joints[-1] + d * L)
    return joints, dirs, ups


def skeleton():
    sk = {}
    for name in FINGER_ORDER:
        s = FINGER_SPECS[name]
        d0 = rot((0, 0, 1), radians(s["splay"])) @ np.array([0.0, 1.0, 0.0])
        j, d, u = build_chain(s["mcp"], d0, (0, 0, 1), s["L"], s["flex"])
        sk[name] = dict(joints=j, dirs=d, ups=u, R=s["R"])
    t = THUMB_SPEC
    yaw, pitch = radians(t["yaw"]), radians(t["pitch"])
    d0 = np.array([-sin(yaw) * cos(pitch), cos(yaw) * cos(pitch), -sin(pitch)])
    j, d, u = build_chain(t["cmc"], d0, t["dorsal_guess"], t["L"], t["flex"])
    sk["thumb"] = dict(joints=j, dirs=d, ups=u, R=t["R"])
    return sk


SK = skeleton()

# ----------------------------------------------------------------------------
# SDF primitives (vectorised over P with shape (N,3))
# ----------------------------------------------------------------------------
def smin(a, b, k):
    if k <= 0:
        return np.minimum(a, b)
    h = np.clip(0.5 + 0.5 * (b - a) / k, 0.0, 1.0)
    return b * (1.0 - h) + a * h - k * h * (1.0 - h)


def sd_capsule(P, a, b, ra, rb):
    a = np.asarray(a, float)
    b = np.asarray(b, float)
    ba = b - a
    t = np.clip(((P - a) @ ba) / np.dot(ba, ba), 0.0, 1.0)
    C = a + t[:, None] * ba
    return np.linalg.norm(P - C, axis=1) - (ra + (rb - ra) * t)


def sd_ellipsoid(P, c, axes, radii):
    q = (P - np.asarray(c, float)) @ np.asarray(axes, float).T   # local coords
    r = np.asarray(radii, float)
    k0 = np.linalg.norm(q / r, axis=1)
    k1 = np.linalg.norm(q / (r * r), axis=1) + 1e-12
    return k0 * (k0 - 1.0) / k1


def sd_round_box(q, b, r):
    d = np.abs(q) - (np.asarray(b) - r)
    out = np.linalg.norm(np.maximum(d, 0.0), axis=1)
    ins = np.minimum(np.max(d, axis=1), 0.0)
    return out + ins - r


def sd_round_cyl_y(P, yc, half_h, R, rr):
    qr = np.sqrt(P[:, 0] ** 2 + P[:, 2] ** 2) - R + rr
    qy = np.abs(P[:, 1] - yc) - half_h + rr
    return (np.minimum(np.maximum(qr, qy), 0.0)
            + np.sqrt(np.maximum(qr, 0.0) ** 2 + np.maximum(qy, 0.0) ** 2) - rr)


# ----------------------------------------------------------------------------
# glove components
# ----------------------------------------------------------------------------
PALM_C = np.array([0.0, 0.0470, 0.0005])
PALM_B = np.array([0.0490, 0.0500, 0.0172])
PALM_R = 0.0165
GROOVE_DEPTH = 0.00085
GROOVE_W = 0.00105
GROOVE_OFF = 0.0026


def sd_palm(P):
    q = P - PALM_C
    y = P[:, 1]
    taper = 0.84 + 0.16 * np.clip(y / 0.080, 0.0, 1.0)
    x = q[:, 0] / taper
    z = q[:, 2] + 2.6 * q[:, 0] ** 2          # slight dome on top / cup underneath
    d = sd_round_box(np.stack([x, q[:, 1], z], axis=1), PALM_B, PALM_R)
    hyp = sd_ellipsoid(P, (0.0290, 0.0350, -0.0085), np.eye(3), (0.0150, 0.0300, 0.0110))
    d = smin(d, hyp, 0.008)
    # wrist (slightly elliptical) and the round collar that fills the titanium ring
    Pw = P.copy()
    Pw[:, 0] /= 1.25
    wrist = sd_capsule(Pw, (0.0, -0.022, 0.0), (0.0, 0.020, 0.0010), 0.0300, 0.0290)
    d = smin(d, wrist, 0.018)
    collar = sd_round_cyl_y(P, -0.0095, 0.0080, 0.0398, 0.0030)
    d = smin(d, collar, 0.006)
    return d


def joint_grooves(P, joint, axis):
    s = (P - joint) @ axis
    g = (np.exp(-((s - GROOVE_OFF) / GROOVE_W) ** 2) + np.exp(-((s + GROOVE_OFF) / GROOVE_W) ** 2))
    return g


def sd_chain(P, ch, grooves_at=(1, 2), k=0.0015):
    J, R = ch["joints"], ch["R"]
    d = None
    for i in range(3):
        di = sd_capsule(P, J[i], J[i + 1], R[i], R[i + 1])
        d = di if d is None else smin(d, di, k)
    g = np.zeros(len(P))
    for gi in grooves_at:
        ax = nrm(ch["dirs"][gi - 1] + ch["dirs"][gi])
        g += joint_grooves(P, J[gi], ax)
    return d + GROOVE_DEPTH * g, g


def components(P):
    """Return dict of component SDFs (+ groove fields)."""
    out = {"palm": sd_palm(P)}
    grooves = np.zeros(len(P))
    th, gth = sd_chain(P, SK["thumb"], grooves_at=(1, 2), k=0.003)
    tch = SK["thumb"]
    d0, up0 = tch["dirs"][0], tch["ups"][0]
    side0 = nrm(np.cross(d0, up0))
    thenar_c = tch["joints"][0] + d0 * 0.011 + np.array([0.011, -0.001, -0.004])
    thenar = sd_ellipsoid(P, thenar_c, np.stack([d0, side0, up0]), (0.0250, 0.0175, 0.0135))
    th = smin(th, thenar, 0.006)
    out["thumb"] = th
    for name in FINGER_ORDER:
        d, g = sd_chain(P, SK[name])
        out[name] = d
        grooves = np.maximum(grooves, g * (d < 0.004))
    grooves = np.maximum(grooves, gth * (th < 0.004))
    out["_grooves"] = grooves
    return out


def glove_sdf(P, comps=None):
    c = comps if comps is not None else components(P)
    palm = c["palm"]
    full = smin(palm, c["thumb"], 0.016)
    for name in FINGER_ORDER:
        full = np.minimum(full, smin(palm, c[name], 0.0085))
    return full


# ----------------------------------------------------------------------------
# projection helpers
# ----------------------------------------------------------------------------
def gradient(P, f=glove_sdf, h=2e-5):
    g = np.zeros_like(P)
    for i in range(3):
        e = np.zeros(3)
        e[i] = h
        g[:, i] = (f(P + e) - f(P - e)) / (2 * h)
    return g


def project(P, level=0.0, iters=10, f=glove_sdf, max_step=0.003):
    P = np.array(P, dtype=float, copy=True)
    for _ in range(iters):
        d = f(P) - level
        g = gradient(P, f)
        g2 = np.sum(g * g, axis=1) + 1e-12
        step = np.clip(d, -max_step, max_step)
        P -= (step / g2)[:, None] * g
    return P


def normals(P, f=glove_sdf):
    g = gradient(P, f)
    return g / (np.linalg.norm(g, axis=1, keepdims=True) + 1e-12)


def surface_point(guess, along=None, level=0.0):
    """Project one point; returns (point, normal)."""
    p = project(np.array([guess], float), level=level)
    n = normals(p)[0]
    return p[0], n


# ----------------------------------------------------------------------------
# skinning weights (cascade along each chain, then smoothed on the mesh)
# ----------------------------------------------------------------------------
BONES = (["hand", "forearm"]
         + [f"{f}_0{i}" for f in FINGER_ORDER + ["thumb"] for i in (1, 2, 3)])


def chain_weights(P, ch, h=(0.0060, 0.0042, 0.0038)):
    J, D = ch["joints"], ch["dirs"]
    a = smoothstep(-h[0], h[0], (P - J[0]) @ D[0])
    b = smoothstep(-h[1], h[1], (P - J[1]) @ nrm(D[0] + D[1]))
    c = smoothstep(-h[2], h[2], (P - J[2]) @ nrm(D[1] + D[2]))
    return {"hand": 1 - a, "_01": a * (1 - b), "_02": a * b * (1 - c), "_03": a * b * c}


def skin_weights(P, comps=None, tau=0.0007):
    c = comps if comps is not None else components(P)
    keys = ["palm", "thumb"] + FINGER_ORDER
    stack = np.stack([c[k] for k in keys], axis=1)
    m = np.exp(-(stack - stack.min(axis=1, keepdims=True)) / tau)
    m /= m.sum(axis=1, keepdims=True)
    W = {b: np.zeros(len(P)) for b in BONES}
    # wrist blend for anything that is "hand"
    yrel = P[:, 1] - WRIST_PIVOT[1]
    hand_frac = smoothstep(-0.004, 0.018, yrel)
    # palm membership
    W["hand"] += m[:, 0] * hand_frac
    W["forearm"] += m[:, 0] * (1 - hand_frac)
    for col, name in enumerate(["thumb"] + FINGER_ORDER, start=1):
        hh = (0.0110, 0.0050, 0.0042) if name == "thumb" else (0.0060, 0.0042, 0.0038)
        cw = chain_weights(P, SK[name], hh)
        W["hand"] += m[:, col] * cw["hand"] * hand_frac
        W["forearm"] += m[:, col] * cw["hand"] * (1 - hand_frac)
        for i in (1, 2, 3):
            W[f"{name}_0{i}"] += m[:, col] * cw[f"_0{i}"]
    return W


# ----------------------------------------------------------------------------
# surface masks for the shader (grip pads, seams, convolute grooves)
# ----------------------------------------------------------------------------
def surface_masks(P, N, comps=None):
    c = comps if comps is not None else components(P)
    keys = ["palm", "thumb"] + FINGER_ORDER
    stack = np.stack([c[k] for k in keys], axis=1)
    member = np.argmin(stack, axis=1)
    palmar = -N[:, 2].copy()                      # palm: facing -Z
    side = np.abs(N[:, 2]).copy()
    for col, name in enumerate(["thumb"] + FINGER_ORDER, start=1):
        ch = SK[name]
        sel = member == col
        if not np.any(sel):
            continue
        # pick the closest segment's 'up' vector
        Ps = P[sel]
        best = np.full(len(Ps), 1e9)
        upv = np.zeros((len(Ps), 3))
        for i in range(3):
            dseg = sd_capsule(Ps, ch["joints"][i], ch["joints"][i + 1], ch["R"][i], ch["R"][i + 1])
            better = dseg < best
            best = np.where(better, dseg, best)
            upv[better] = ch["ups"][i]
        dots = np.sum(N[sel] * upv, axis=1)
        palmar[sel] = -dots
        side[sel] = np.abs(dots)
    grip = smoothstep(0.30, 0.70, palmar)
    # keep the palm grip pad off the wrist and the extreme edges
    palm_sel = member == 0
    grip[palm_sel] *= smoothstep(0.012, 0.030, P[palm_sel, 1])
    grooves = np.clip(c["_grooves"], 0, 1)
    grip *= 1.0 - smoothstep(0.25, 0.75, grooves)
    seam = np.exp(-(side / 0.055) ** 2) * (1 - smoothstep(0.2, 0.6, grooves))
    seam[palm_sel & (P[:, 1] < 0.004)] = 0.0
    return {"grip": grip, "seam": seam, "groove": grooves, "member": member}
