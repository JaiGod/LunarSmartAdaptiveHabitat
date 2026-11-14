#!/usr/bin/env python3
"""Textures for LUNA VR. Plain, NASA-style surfaces (panel seams, fasteners, grip floor, fabric,
regolith) plus the sky, Earth and the three recovery-chair scenes. No decorative greebles."""
import math, random
from PIL import Image, ImageDraw, ImageFilter

OUT = "assets/tex/"
R = random.Random(7)

def noise(img, amt, seed=1):
    r = random.Random(seed)
    px = img.load(); w, h = img.size
    for y in range(h):
        for x in range(w):
            p = px[x, y]; n = r.randint(-amt, amt)
            px[x, y] = tuple(max(0, min(255, c + n)) for c in p[:3]) + ((p[3],) if len(p) == 4 else ())
    return img

# ---- wall panel: 1.25 m tile = one panel, with seam, 4 fasteners, a faint label strip --------------------
def wall():
    s = 512; im = Image.new("RGB", (s, s), (226, 229, 232)); d = ImageDraw.Draw(im)
    d.rectangle((0, 0, s - 1, 5), fill=(170, 175, 182)); d.rectangle((0, 0, 5, s - 1), fill=(170, 175, 182))
    d.rectangle((6, 6, s - 1, 9), fill=(240, 242, 244)); d.rectangle((6, 6, 9, s - 1), fill=(240, 242, 244))
    for (x, y) in [(30, 30), (s - 30, 30), (30, s - 30), (s - 30, s - 30)]:
        d.ellipse((x - 6, y - 6, x + 6, y + 6), fill=(150, 155, 162)); d.ellipse((x - 3, y - 3, x + 3, y + 3), fill=(205, 208, 212))
    d.rectangle((60, s // 2 - 2, s - 60, s // 2 + 2), fill=(214, 217, 221))   # stiffener line
    noise(im, 4).filter(ImageFilter.GaussianBlur(0.6)).save(OUT + "wall.png")

# ---- floor: grey rubber grip tiles ------------------------------------------------------------------
def floor():
    s = 512; im = Image.new("RGB", (s, s), (92, 97, 106)); d = ImageDraw.Draw(im)
    d.rectangle((0, 0, s - 1, 3), fill=(60, 64, 70)); d.rectangle((0, 0, 3, s - 1), fill=(60, 64, 70))
    for y in range(18, s, 26):
        for x in range(18 + (13 if (y // 26) % 2 else 0), s, 26):
            d.ellipse((x - 5, y - 5, x + 5, y + 5), fill=(104, 110, 119))
    noise(im, 6, 2).filter(ImageFilter.GaussianBlur(0.7)).save(OUT + "floor.png")

# ---- grating (airlock: dust falls through to the vacuum plenum) ----------------------------------------
def grate():
    s = 256; im = Image.new("RGBA", (s, s), (30, 32, 36, 255)); d = ImageDraw.Draw(im)
    for x in range(0, s, 16): d.rectangle((x, 0, x + 4, s), fill=(150, 156, 164, 255))
    for y in range(0, s, 64): d.rectangle((0, y, s, y + 5), fill=(130, 136, 144, 255))
    noise(im, 6, 3).save(OUT + "grate.png")

# ---- ceiling: acoustic panels ------------------------------------------------------------------------
def ceiling():
    s = 256; im = Image.new("RGB", (s, s), (205, 208, 212)); d = ImageDraw.Draw(im)
    d.rectangle((0, 0, s - 1, 2), fill=(150, 154, 160)); d.rectangle((0, 0, 2, s - 1), fill=(150, 154, 160))
    for y in range(10, s, 9):
        for x in range(10, s, 9): d.point((x, y), fill=(175, 178, 184))
    noise(im, 3, 4).save(OUT + "ceiling.png")

# ---- Nomex fabric (seats, cargo transfer bags) -------------------------------------------------------
def fabric():
    s = 256; im = Image.new("RGB", (s, s), (206, 196, 172)); d = ImageDraw.Draw(im)
    for y in range(0, s, 3): d.line((0, y, s, y), fill=(196, 186, 162))
    for x in range(0, s, 3): d.line((x, 0, x, s), fill=(200, 190, 166))
    noise(im, 5, 5).save(OUT + "fabric.png")

# ---- regolith -----------------------------------------------------------------------------------------
def regolith():
    s = 1024; im = Image.new("RGB", (s, s), (118, 116, 112)); px = im.load()
    r = random.Random(9)
    base = Image.new("L", (64, 64)); bp = base.load()
    for y in range(64):
        for x in range(64): bp[x, y] = r.randint(80, 150)
    base = base.resize((s, s), Image.BICUBIC).filter(ImageFilter.GaussianBlur(6))
    fine = Image.effect_noise((s, s), 40)
    for y in range(s):
        for x in range(s):
            v = int(base.getpixel((x, y)) * 0.75 + fine.getpixel((x, y)) * 0.25)
            px[x, y] = (v, v - 2, v - 6)
    d = ImageDraw.Draw(im)
    for _ in range(260):
        x, y, rr = r.randint(0, s), r.randint(0, s), r.choice([2, 2, 3, 4, 6])
        d.ellipse((x - rr, y - rr, x + rr, y + rr), fill=(150, 148, 142)); d.ellipse((x - rr + 1, y, x + rr, y + rr + 1), fill=(70, 69, 66))
    im.filter(ImageFilter.GaussianBlur(0.5)).save(OUT + "regolith.png")

# ---- regolith bag shielding on the outer shell ----------------------------------------------------------
def bags():
    s = 512; im = Image.new("RGB", (s, s), (120, 117, 110)); d = ImageDraw.Draw(im)
    for row in range(8):
        off = 32 if row % 2 else 0
        for col in range(-1, 5):
            x = col * 128 + off; y = row * 64
            d.rounded_rectangle((x + 3, y + 3, x + 125, y + 61), radius=22, fill=(132, 129, 121), outline=(92, 90, 85), width=3)
    noise(im, 8, 6).filter(ImageFilter.GaussianBlur(1.0)).save(OUT + "bags.png")

# ---- solar cell -----------------------------------------------------------------------------------------
def solar():
    s = 256; im = Image.new("RGB", (s, s), (18, 34, 78)); d = ImageDraw.Draw(im)
    for i in range(0, s, 32):
        d.line((i, 0, i, s), fill=(150, 160, 180), width=2); d.line((0, i, s, i), fill=(150, 160, 180), width=2)
    for i in range(0, s, 8): d.line((0, i, s, i), fill=(26, 44, 92))
    im.save(OUT + "solar.png")

# ---- stars: equirectangular sky ---------------------------------------------------------------------------
def stars():
    w, h = 2048, 1024; im = Image.new("RGB", (w, h), (0, 0, 0)); d = ImageDraw.Draw(im); r = random.Random(11)
    for _ in range(4500):
        x, y = r.randint(0, w - 1), r.randint(0, h - 1); b = int(255 * r.random() ** 3)
        c = r.choice([(b, b, b), (b, int(b * 0.9), int(b * 0.8)), (int(b * 0.85), int(b * 0.9), b)])
        d.point((x, y), fill=c)
        if b > 200: d.point((x + 1, y), fill=c); d.point((x, y + 1), fill=c)
    # Milky Way band
    band = Image.new("L", (w, h)); bp = band.load()
    for y in range(h):
        for x in range(0, w, 2):
            t = (y - h * 0.5 - 150 * math.sin(x / w * math.tau)) / 85.0
            v = int(28 * math.exp(-t * t))
            bp[x, y] = v; bp[min(w - 1, x + 1), y] = v
    band = band.filter(ImageFilter.GaussianBlur(7))
    im = Image.merge("RGB", [Image.composite(Image.new("L", (w, h), 255), c, band.point(lambda v: 0)) for c in im.split()])
    mw = Image.merge("RGB", (band, band.point(lambda v: int(v * 0.95)), band.point(lambda v: int(v * 1.1))))
    from PIL import ImageChops
    ImageChops.add(im, mw).save(OUT + "stars.png")

# ---- Earth -------------------------------------------------------------------------------------------------
def earth():
    w, h = 1024, 512; im = Image.new("RGB", (w, h), (18, 60, 140)); d = ImageDraw.Draw(im); r = random.Random(13)
    for _ in range(40):
        x, y = r.randint(0, w), r.randint(int(h * 0.15), int(h * 0.85)); rw, rh = r.randint(30, 140), r.randint(20, 90)
        d.ellipse((x - rw, y - rh, x + rw, y + rh), fill=(64, 112, 58))
    land = im.filter(ImageFilter.GaussianBlur(10))
    cl = Image.new("L", (w, h)); dc = ImageDraw.Draw(cl)
    for _ in range(70):
        x, y = r.randint(0, w), r.randint(0, h); rw = r.randint(20, 90)
        dc.ellipse((x - rw, y - rw // 4, x + rw, y + rw // 4), fill=r.randint(120, 230))
    cl = cl.filter(ImageFilter.GaussianBlur(7))
    d2 = ImageDraw.Draw(land); d2.rectangle((0, 0, w, 30), fill=(230, 235, 240)); d2.rectangle((0, h - 30, w, h), fill=(230, 235, 240))
    out = Image.composite(Image.new("RGB", (w, h), (245, 247, 250)), land, cl)
    out.save(OUT + "earth.png")

# ---- recovery chair scenes (equirectangular, soft and simple) -----------------------------------------------
def scene(name, sky_top, sky_bot, ground, extra):
    w, h = 2048, 1024; im = Image.new("RGB", (w, h)); d = ImageDraw.Draw(im)
    for y in range(h // 2):
        t = y / (h / 2); c = tuple(int(sky_top[k] * (1 - t) + sky_bot[k] * t) for k in range(3)); d.line((0, y, w, y), fill=c)
    for y in range(h // 2, h):
        t = (y - h / 2) / (h / 2); c = tuple(int(ground[k] * (1 - t * 0.5)) for k in range(3)); d.line((0, y, w, y), fill=c)
    extra(d, w, h, random.Random(len(name)))
    im.filter(ImageFilter.GaussianBlur(1.2)).save(OUT + "scene_%s.png" % name)

def forest_extra(d, w, h, r):
    for layer, col in [(0, (70, 110, 80)), (1, (46, 86, 58)), (2, (30, 64, 42))]:
        for _ in range(140):
            x = r.randint(0, w); base = h // 2 + 10 + layer * 18; th = r.randint(60, 150) - layer * 10
            d.polygon([(x, base - th), (x - 22, base), (x + 22, base)], fill=col)
def ocean_extra(d, w, h, r):
    d.ellipse((w * 0.25 - 40, h * 0.42 - 40, w * 0.25 + 40, h * 0.42 + 40), fill=(255, 236, 190))
    for y in range(h // 2, h, 6):
        d.line((0, y, w, y), fill=(40, 110, 150) if (y // 6) % 2 else (60, 140, 175))
def earth_extra(d, w, h, r):
    for _ in range(1500):
        d.point((r.randint(0, w), r.randint(0, h // 2)), fill=(230, 230, 240))
    d.ellipse((w * 0.5 - 260, h * 0.45 - 260, w * 0.5 + 260, h * 0.45 + 260), fill=(40, 90, 170))
    d.ellipse((w * 0.5 - 150, h * 0.45 - 120, w * 0.5 + 40, h * 0.45 + 60), fill=(70, 120, 70))
    d.ellipse((w * 0.5 - 60, h * 0.45 + 40, w * 0.5 + 180, h * 0.45 + 180), fill=(220, 225, 235))

def icon():
    im = Image.new("RGBA", (256, 256), (5, 8, 22, 255)); d = ImageDraw.Draw(im)
    d.ellipse((24, 24, 232, 232), fill=(196, 202, 214)); d.ellipse((92, 92, 164, 164), fill=(56, 189, 248))
    d.ellipse((60, 50, 100, 90), fill=(160, 166, 178)); d.ellipse((150, 160, 190, 200), fill=(160, 166, 178))
    im.save(OUT + "icon.png")

if __name__ == "__main__":
    for f in (wall, floor, grate, ceiling, fabric, regolith, bags, solar, stars, earth, icon): f(); print(f.__name__)
    scene("forest", (120, 170, 220), (210, 230, 240), (60, 90, 60), forest_extra)
    scene("ocean", (90, 150, 210), (250, 210, 170), (30, 90, 130), ocean_extra)
    scene("earth", (2, 4, 12), (6, 10, 24), (120, 118, 112), earth_extra)
    print("scenes")
