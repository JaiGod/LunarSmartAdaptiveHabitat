"""Realistic Earth textures for LUNA VR (no external images).

earth.png        1024x512 equirectangular map: oceans, continents with biome colours, ice caps, clouds.
scene_earth.png  2048x1024 'live south-pole camera' view: black sky, Earth low over a jagged crater rim
                 (from the lunar south pole Earth always sits near the horizon), long grazing-sun shadows.
Run: python3 tools/make_earth.py
"""
import numpy as np
from PIL import Image, ImageFilter

rng = np.random.default_rng(7)
OUT = "assets/tex/"


def fbm(shape, octaves=6, base=4, seed=0, wrap_x=True, pers=0.5):
    """fractal value noise in [0,1]"""
    r = np.random.default_rng(seed)
    h, w = shape
    acc = np.zeros(shape); amp = 1.0; tot = 0.0
    for o in range(octaves):
        n = base * 2 ** o
        gh, gw = max(2, n // 2 + 1), n + 1
        g = r.random((gh, gw))
        if wrap_x: g[:, -1] = g[:, 0]
        img = Image.fromarray((g * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC)
        acc += amp * (np.asarray(img, float) / 255.0); tot += amp; amp *= pers
    return acc / tot


def warp(n, dx, dy, k):
    h, w = n.shape
    yy, xx = np.mgrid[0:h, 0:w]
    return n[np.clip((yy + dy * k).astype(int), 0, h - 1), ((xx + dx * k).astype(int)) % w]


def earth_map(w=1024, h=512):
    lat = np.linspace(90, -90, h)[:, None] * np.ones((1, w))
    a = np.abs(lat)
    ln = fbm((h, w), 8, 4, seed=11, pers=0.6)
    ln = warp(ln, fbm((h, w), 5, 4, seed=12) - 0.5, fbm((h, w), 5, 4, seed=13) - 0.5, 160)   # coastlines with bays and peninsulas
    ln = ln + 0.06 * fbm((h, w), 6, 16, seed=14)
    thr = np.percentile(ln, 71)                                      # ~29 % land, like Earth
    land = ln > thr
    hgt = np.clip((ln - thr) / (ln.max() - thr), 0, 1)
    green = np.array([58, 96, 48]); forest = np.array([34, 66, 36]); desert = np.array([190, 162, 116]); tundra = np.array([122, 114, 98]); mountain = np.array([110, 98, 84])
    dry = fbm((h, w), 6, 5, seed=5)
    desert_w = (np.clip(1 - np.abs(a - 24) / 11, 0, 1) * np.clip((dry - 0.42) / 0.12, 0, 1))[..., None]
    trop = np.clip(1 - a / 18, 0, 1)[..., None]
    col = green * (1 - trop) + forest * trop
    col = col * (1 - desert_w) + desert * desert_w
    mw = np.clip((hgt - 0.55) / 0.3, 0, 1)[..., None]
    col = col * (1 - mw) + mountain * mw
    tw = np.clip((a - 55) / 15, 0, 1)[..., None]
    col = col * (1 - tw) + tundra * tw
    shelf = np.clip((ln - (thr - 0.03)) / 0.03, 0, 1)[..., None]
    ocean = np.array([8, 30, 78]) * (1 - shelf) + np.array([24, 78, 132]) * shelf
    img = np.where(land[..., None], col, ocean)
    ice = (a > 78 + 3 * fbm((h, w), 4, 6, seed=3)) | ((lat < -64) & (fbm((h, w), 4, 6, seed=4) > 0.35))
    img = np.where(ice[..., None], np.array([236, 240, 245]), img)
    # clouds: zonal streaks + swirled storm systems, about half the planet covered
    c = fbm((h, w), 7, 10, seed=21, pers=0.62)
    c = warp(c, fbm((h, w), 4, 3, seed=22) - 0.5, (fbm((h, w), 4, 3, seed=23) - 0.5) * 0.4, 90)
    band = 0.5 + 0.5 * np.cos(np.radians(a) * 6)
    c = c * (0.8 + 0.3 * band)
    ct = np.percentile(c, 52)
    cl = np.clip((c - ct) / 0.07, 0, 1) ** 1.3
    img = img * (1 - cl[..., None] * 0.9) + np.array([246, 248, 252]) * cl[..., None] * 0.9
    return np.clip(img, 0, 255).astype(np.uint8)


def sphere(tex, d, sun=(-0.75, 0.25, 0.6), rot=0.0):
    """orthographic render of a textured sphere, d pixels across; returns RGBA"""
    h, w, _ = tex.shape
    y, x = np.mgrid[-1:1:d * 1j, -1:1:d * 1j]
    rr = x ** 2 + y ** 2
    inside = rr <= 1
    z = np.sqrt(np.clip(1 - rr, 0, 1))
    lat = np.arcsin(-y)
    lon = np.arctan2(x, z) + rot
    u = ((lon / (2 * np.pi) + 0.5) % 1.0) * (w - 1)
    v = (0.5 - lat / np.pi) * (h - 1)
    col = tex[v.astype(int), u.astype(int)].astype(float)
    s = np.array(sun, float); s /= np.linalg.norm(s)
    lam = np.clip(x * s[0] - y * s[1] + z * s[2], 0, 1)
    shade = 0.03 + 0.97 * lam ** 0.8
    col = col * shade[..., None]
    rim = np.clip((rr - 0.82) / 0.18, 0, 1) ** 2 * np.clip(lam * 3, 0, 1)
    col = col * (1 - rim[..., None] * 0.5) + np.array([120, 170, 255]) * rim[..., None] * 0.5   # thin blue atmosphere
    a = np.clip((1 - rr) * d * 0.5, 0, 1) * 255                                                 # anti-aliased edge
    out = np.dstack([np.clip(col, 0, 255), a]).astype(np.uint8)
    out[~inside] = 0
    return out


def scene(tex, W=2048, H=1024):
    sky = np.zeros((H, W, 3))
    stars = rng.random((H, W))
    sky[stars > 0.9993] = 150             # the camera exposes for the bright Earth: only a few faint stars
    img = Image.fromarray(sky.astype(np.uint8)).convert("RGBA")
    # Earth: ~2 deg in the sky; the camera is a telephoto, so it fills about a fifth of the frame
    d = 300
    e = Image.fromarray(sphere(tex, d, sun=(-0.8, 0.2, 0.55), rot=1.2), "RGBA")
    horizon_y = int(H * 0.62)
    img.alpha_composite(e, (int(W * 0.58), horizon_y - int(d * 0.92)))
    # crater-rim horizon: jagged ridges, lit from a sun only ~1.5 deg above the horizon (grazing light)
    xs = np.arange(W)
    def ridge_line(seed, amps):
        r = np.random.default_rng(seed); out = np.zeros(W)
        for amp, per in amps:
            ph = r.random() * 2 * np.pi
            out += amp * np.sin(xs / per * 2 * np.pi + ph) + amp * 0.4 * np.sin(xs / (per * 0.41) * 2 * np.pi + ph * 1.7)
        return out
    far = horizon_y - 30 + ridge_line(1, [(40, 1100), (16, 380), (6, 150)]) * 0.6
    near = horizon_y + 25 + ridge_line(2, [(55, 800), (22, 260), (8, 90)]) * 0.7
    yy = np.arange(H)[:, None]
    out = np.asarray(img)[..., :3].astype(float)
    grain = fbm((H, W), 6, 8, seed=31)
    for rl, lit, dark in [(far, 120, 34), (near, 175, 14)]:
        slope = np.convolve(np.gradient(rl), np.ones(25) / 25, mode="same")
        below = yy - rl[None, :]
        crest = np.clip(0.35 - slope[None, :] * 0.9, 0, 1) * np.exp(-np.clip(below, 0, None) / 16.0)   # grazing sun lights only the crest
        g = dark * (0.6 + 0.8 * grain) + lit * crest
        mask = below >= 0
        out = np.where(mask[..., None], np.dstack([g, g * 0.97, g * 0.93]), out)
    # timestamp bar like a real camera feed (plain, no decoration)
    pil = Image.fromarray(np.clip(out, 0, 255).astype(np.uint8))
    return pil.filter(ImageFilter.GaussianBlur(0.6))


if __name__ == "__main__":
    em = earth_map()
    Image.fromarray(em).save(OUT + "earth.png")
    scene(em).save(OUT + "scene_earth.png")
    print("wrote earth.png, scene_earth.png")


def forest(W=2048, H=1024):
    """calm forest edge at golden hour: layered tree lines fading into haze, meadow in front"""
    yy = np.arange(H)[:, None] / H
    sky = np.dstack([110 + 100 * yy, 160 + 70 * yy, 225 + 10 * yy]) * np.ones((1, W, 1))
    out = sky.copy()
    r = np.random.default_rng(4)
    layers = [(0.50, (120, 150, 140), 0.07), (0.55, (80, 115, 95), 0.09), (0.60, (48, 82, 58), 0.12), (0.66, (28, 56, 36), 0.15)]
    xs = np.arange(W)
    for base, col, ht in layers:
        top = np.full(W, base * H)
        x = 0
        while x < W:                                   # individual conifers of varying height and width
            tw = r.integers(30, 72); th = ht * H * (0.6 + 0.6 * r.random())
            cx = x + tw / 2
            prof = np.clip(1 - np.abs(xs - cx) / (tw * 0.6), 0, 1)
            top = np.minimum(top, base * H - th * prof)
            x += int(tw * (0.5 + 0.4 * r.random()))
        mask = np.arange(H)[:, None] >= top[None, :]
        out = np.where(mask[..., None], np.array(col, float), out)
    g = fbm((H, W), 6, 6, seed=9)
    meadow_top = 0.68 * H
    m = np.arange(H)[:, None] >= meadow_top
    mead = np.dstack([70 + 50 * g, 110 + 50 * g, 50 + 20 * g])
    shade = np.clip(1.15 - (np.arange(H)[:, None] - meadow_top) / H, 0.6, 1.1)[..., None]
    out = np.where(m[..., None], mead * shade, out)
    return Image.fromarray(np.clip(out, 0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.8))


def ocean(W=2048, H=1024):
    """sunset over a calm sea: gradient sky, soft sun, glitter path on gentle swell"""
    hz = int(H * 0.5)
    yy = np.arange(H)[:, None]
    t = np.clip(yy / hz, 0, 1)
    sky = np.dstack([90 + 160 * t ** 2, 140 + 85 * t ** 2, 215 - 40 * t ** 2]) * np.ones((1, W, 1))
    sx, sy = int(W * 0.25), hz - 60
    xx = np.arange(W)[None, :]
    dsun = np.sqrt((xx - sx) ** 2 + (yy - sy) ** 2)
    sky += np.dstack([255 * np.exp(-dsun / 90), 190 * np.exp(-dsun / 90), 110 * np.exp(-dsun / 90)]) * 0.45
    sky = np.where((dsun < 34)[..., None], np.array([255, 244, 214], float), sky)
    d = np.clip((yy - hz) / (H - hz), 0, 1)
    # swell: thin horizontal wave crests that get wider and further apart toward the viewer (perspective)
    r = np.random.default_rng(17)
    rows = np.arange(H)[:, None]
    per = 6 + 60 * d                                            # crest spacing in px, per row
    ph = r.random((H, 1)) * 2 * np.pi
    wob = np.sin(xx / (40 + 400 * d) + ph) * 0.5 + np.sin(xx / (13 + 90 * d) + ph * 3.1) * 0.5
    crest = 0.5 + 0.5 * np.sin(rows / np.maximum(per, 1) * 2 * np.pi + wob * 2.2)
    sea = np.dstack([18 + 34 * (1 - d), 66 + 44 * (1 - d), 104 + 36 * (1 - d)]) * (0.88 + 0.22 * crest[..., None])
    path = np.exp(-((xx - sx) / (20 + 240 * d)) ** 2) * np.clip((crest - 0.82) / 0.1, 0, 1)
    sea += np.dstack([255 * path, 225 * path, 170 * path]) * 0.85
    out = np.where((yy >= hz)[..., None], sea, sky)
    return Image.fromarray(np.clip(out, 0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.7))


if __name__ == "__main__":
    forest().save(OUT + "scene_forest.png")
    ocean().save(OUT + "scene_ocean.png")
    print("wrote scene_forest.png, scene_ocean.png")
