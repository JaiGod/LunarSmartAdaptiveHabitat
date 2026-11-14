"""Original pixel art for LUNA (all drawn here from templates; no outside assets).
Run: python3 tools/make_art.py art/"""
import sys, os, random
from PIL import Image

OUT = sys.argv[1]
os.makedirs(OUT, exist_ok=True)

def hexc(h, a=255):
    h = h.lstrip("#"); return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)

P = {  # shared palette
    'k': "#10131f", 'd': "#262d40", 'm': "#38415a", 'g': "#525d78", 'l': "#76839e", 'L': "#a6b1c6",
    'w': "#d3dae6", 'W': "#f3f5fa", 'a': "#f2a541", 'o': "#e0752f", 't': "#3fc1c9", 'T': "#257f8a",
    'G': "#4caf50", 'F': "#2b7a3d", 'y': "#8bc34a", 'Y': "#c3eb7f", 'r': "#e5484d", 'R': "#9b2c33",
    'b': "#4f8ef7", 'B': "#2c4f9e", 'p': "#a970ff", 'P': "#6a3fc0", 'n': "#8d5a3b", 'N': "#5a3a26",
    'c': "#f3e3c3", 's': "#9be7ff", 'e': "#3a7bd5", 'u': "#6b4a2f", 'U': "#4a3220", 'z': "#2f6fd6",
    'v': "#1b2133", 'x': "#c9ced8", 'q': "#ffd36e",
}
PAL = {k: hexc(v) for k, v in P.items()}

def from_ascii(rows, extra=None, outline=False):
    pal = dict(PAL); pal.update(extra or {})
    h = len(rows); w = max(len(r) for r in rows)
    im = Image.new("RGBA", (w, h), (0, 0, 0, 0)); px = im.load()
    for y, r in enumerate(rows):
        for x, ch in enumerate(r):
            if ch != '.' and ch != ' ': px[x, y] = pal[ch]
    if outline: add_outline(im)
    return im

def add_outline(im, col=None):
    col = col or PAL['k']; w, h = im.size; px = im.load(); src = im.copy().load()
    for y in range(h):
        for x in range(w):
            if src[x, y][3] == 0:
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    xx, yy = x + dx, y + dy
                    if 0 <= xx < w and 0 <= yy < h and src[xx, yy][3] > 0 and src[xx, yy][:3] != col[:3]:
                        px[x, y] = col; break

def save(im, name, scale=1):
    if scale != 1: im = im.resize((im.width * scale, im.height * scale), Image.NEAREST)
    im.save(os.path.join(OUT, name))

def rect(im, x, y, w, h, c):
    px = im.load()
    for yy in range(y, y + h):
        for xx in range(x, x + w):
            if 0 <= xx < im.width and 0 <= yy < im.height: px[xx, yy] = PAL[c] if isinstance(c, str) else c

def frame(im, x, y, w, h, c):
    rect(im, x, y, w, 1, c); rect(im, x, y + h - 1, w, 1, c); rect(im, x, y, 1, h, c); rect(im, x + w - 1, y, 1, h, c)

def dot(im, x, y, c):
    rect(im, x, y, 1, 1, c)

# =====================================================================================================
#  CREW — 12x16 frames: rows = down, up, side(right); cols = stand, walkA, walkB
# =====================================================================================================
HEAD_DOWN = ["............", "....HHHH....", "...HHHHHH...", "..HHSSSSHH..", "..HSSSSSSH..", "..HSESSESH..", "...SSSSSS...", "....SSSS...."]
HEAD_UP   = ["............", "....HHHH....", "...HHHHHH...", "..HHHHHHHH..", "..HHHHHHHH..", "..HHHHHHHH..", "...SHHHHS...", "....SSSS...."]
HEAD_SIDE = ["............", "....HHHH....", "...HHHHHH...", "..HHHHSSS...", "..HHHSSESS..", "..HHSSSSSS..", "...HSSSSS...", "....SSSS...."]
BODY_DOWN = ["...AJJJJA...", "..JJJAAJJJ..", "..SJJJJJJS..", "..SjJJJJjS..", "...JJJJJJ..."]
BODY_UP   = ["...AJJJJA...", "..JJJJJJJJ..", "..SJJJJJJS..", "..SjJJJJjS..", "...JJJJJJ..."]
BODY_SIDE = ["....AJJJ....", "...JJJAJJ...", "...JJSJJ....", "...JjSJj....", "....JJJJ...."]
LEGS = {
    "down": [["...JJ..JJ...", "...JJ..JJ...", "...BB..BB..."], ["...JJ..JJ...", "...BB..JJ...", ".......BB..."], ["...JJ..JJ...", "...JJ..BB...", "...BB......."]],
    "up":   [["...JJ..JJ...", "...JJ..JJ...", "...BB..BB..."], ["...JJ..JJ...", "...JJ..BB...", "...BB......."], ["...JJ..JJ...", "...BB..JJ...", ".......BB..."]],
    "side": [["....JJJ.....", "....JJJ.....", "....BBBB...."], ["...JJ.JJ....", "..JJ...JJ...", "..BB...BB..."], ["....JJJ.....", "....JJJ.....", "....BBBB...."]],
}
CREW = [  # name, role, hair, skin, accent
    ("Maya Okafor", "Commander", "#1d1a24", "#7a4b32", "#4f8ef7"),
    ("Leo Marin", "Medical officer", "#5b3b25", "#e2b48d", "#e5484d"),
    ("Ana Sato", "Engineer", "#22263a", "#f0cfae", "#f2a541"),
    ("Sam Reyes", "Botanist", "#9a4a2a", "#c48a5e", "#4caf50"),
]
def crew_extra(i):
    _, _, hair, skin, acc = CREW[i]
    return {'H': hexc(hair), 'S': hexc(skin), 'E': hexc("#141826"), 'J': hexc("#dfe5ee"), 'j': hexc("#b9c2d2"),
            'A': hexc(acc), 'B': hexc("#3a4157")}

def crew_frame(i, d, f):
    head = {"down": HEAD_DOWN, "up": HEAD_UP, "side": HEAD_SIDE}[d]
    body = {"down": BODY_DOWN, "up": BODY_UP, "side": BODY_SIDE}[d]
    rows = head + body + LEGS[d][f]
    im = Image.new("RGBA", (12, 17), (0, 0, 0, 0))
    im.paste(from_ascii(rows, crew_extra(i)), (0, 0))
    add_outline(im)
    return im

for i in range(4):
    sheet = Image.new("RGBA", (12 * 3, 17 * 3), (0, 0, 0, 0))
    for r, d in enumerate(["down", "up", "side"]):
        for f in range(3):
            sheet.paste(crew_frame(i, d, f), (f * 12, r * 17))
    save(sheet, "crew%d.png" % i)

# portraits 24x24
PORTRAIT = [
    "........................",
    "........HHHHHHHH........",
    "......HHHHHHHHHHHH......",
    ".....HHHHHHHHHHHHHH.....",
    ".....HHHHHHHHHHHHHH.....",
    "....HHHSSSSSSSSSSHHH....",
    "....HHSSSSSSSSSSSSHH....",
    "....HSSSSSSSSSSSSSSH....",
    "....HSSEESSSSSSEESSH....",
    "....HSSEESSSSSSEESSH....",
    "....SSSSSSSSSSSSSSSS....",
    ".....SSSSSSSSSSSSSS.....",
    ".....SSSSSMMMMSSSSS.....",
    "......SSSSSSSSSSSS......",
    ".......SSSSSSSSSS.......",
    "..........SSSS..........",
    ".......AAJJJJJJAA.......",
    ".....JJJJJAAAAJJJJJ.....",
    "...JJJJJJJJJJJJJJJJJJ...",
    "..JJJJJJJJJJJJJJJJJJJJ..",
    "..JJJJAAJJJJJJJJjJJJJJ..",
    "..JJJJAAJJJJJJJJjJJJJJ..",
    "..JJJJJJJJJJJJJJjJJJJJ..",
    "........................",
]
HAIR_VARIANTS = {  # small per-crew silhouettes
    0: {1: "......HHHHHHHHHH........", 2: ".....HHHHHHHHHHHHHH.....", 3: "....HHHHHHHHHHHHHHHH...."},
    2: {5: "....HHHHHSSSSSSHHHHH....", 6: "....HHHSSSSSSSSSSHHH....", 7: "....HHSSSSSSSSSSSSHH....", 8: "....HHSEESSSSSSEESHH....", 9: "....HHSEESSSSSSEESHH....", 10: "....HHSSSSSSSSSSSSHH....", 11: "....HH.SSSSSSSSSS.HH...."},
}
for i in range(4):
    rows = list(PORTRAIT)
    for r, s in HAIR_VARIANTS.get(i, {}).items(): rows[r] = s
    ex = crew_extra(i); ex['M'] = hexc("#7a3a3a")
    im = from_ascii(rows, ex, outline=True)
    save(im, "portrait%d.png" % i)

# =====================================================================================================
#  FLOOR TILES 16x16 (per zone) + regolith
# =====================================================================================================
def plate(base, seam, hi, rivet=True, seed=0):
    rnd = random.Random(seed); im = Image.new("RGBA", (16, 16), hexc(base)); px = im.load()
    for x in range(16): px[x, 15] = hexc(seam); px[x, 0] = hexc(hi)
    for y in range(16): px[15, y] = hexc(seam); px[0, y] = hexc(hi)
    if rivet:
        for (x, y) in ((2, 2), (13, 2), (2, 13), (13, 13)): px[x, y] = hexc(seam)
    for _ in range(6):
        x, y = rnd.randrange(1, 15), rnd.randrange(1, 15); r, g, b, a = px[x, y]; px[x, y] = (min(255, r + 6), min(255, g + 6), min(255, b + 6), 255)
    return im

FLOORS = {
    "floor_metal": ("#3b4459", "#2d3446", "#46506a"),
    "floor_quarters": ("#41405a", "#323147", "#4d4c69"),
    "floor_galley": ("#4d4437", "#3a3329", "#5b5141"),
    "floor_work": ("#37455a", "#2a3546", "#42536b"),
    "floor_medical": ("#56627a", "#465066", "#66738c"),
    "floor_exercise": ("#3e3a55", "#2e2b41", "#4a4564"),
    "floor_hygiene": ("#3f5560", "#30434c", "#4a6470"),
    "floor_storage": ("#45413a", "#35322c", "#524d44"),
    "floor_eclss": ("#34404f", "#27303c", "#3e4b5d"),
    "floor_airlock": ("#4a4a4a", "#363636", "#575757"),
}
for name, (b, s, h) in FLOORS.items():
    save(plate(b, s, h, seed=hash(name) & 255), name + ".png")
# greenhouse grate
im = Image.new("RGBA", (16, 16), hexc("#2c3a30"))
for y in range(16):
    for x in range(16):
        if x % 4 == 0 or y % 4 == 0: dot(im, x, y, hexc("#3d5243"))
save(im, "floor_greenhouse.png")
# corridor with hazard-free guide stripe
im = plate("#353c4f", "#282e3d", "#3f4760", rivet=False, seed=3)
for x in range(16):
    if x % 4 < 2: dot(im, x, 7, hexc("#4b5574")); dot(im, x, 8, hexc("#4b5574"))
save(im, "floor_corridor.png")
# regolith (3 variants) + crater
for v in range(3):
    rnd = random.Random(40 + v); im = Image.new("RGBA", (16, 16), hexc("#5d5d63")); px = im.load()
    for _ in range(40):
        x, y = rnd.randrange(16), rnd.randrange(16); c = rnd.choice(["#545459", "#67676d", "#6f6f75", "#4d4d52"]); px[x, y] = hexc(c)
    save(im, "regolith%d.png" % v)
CRATER = ["..mmmmmm..", ".mggggggm.", "mglllllggm", "mgllLllggm", "mglllllggm", ".mggggggm.", "..mmmmmm.."]
im = from_ascii(CRATER, {'m': hexc("#4a4a50"), 'g': hexc("#525258"), 'l': hexc("#646469"), 'L': hexc("#7a7a80")})
save(im, "crater.png")

# =====================================================================================================
#  FURNITURE (top-down 3/4)
# =====================================================================================================
def bed(blanket, blanket_dark):
    im = Image.new("RGBA", (14, 26), (0, 0, 0, 0))
    rect(im, 1, 1, 12, 24, 'g'); rect(im, 2, 2, 10, 22, 'L')
    rect(im, 3, 3, 8, 4, 'W'); rect(im, 3, 6, 8, 1, 'w')
    rect(im, 2, 9, 10, 15, hexc(blanket)); rect(im, 2, 9, 10, 1, hexc(blanket_dark)); rect(im, 2, 16, 10, 1, hexc(blanket_dark))
    rect(im, 1, 24, 12, 1, 'm'); add_outline(im); return im
for i in range(4):
    acc = CREW[i][4]
    r, g, b, _ = hexc(acc); dark = "#%02x%02x%02x" % (int(r * .65), int(g * .65), int(b * .65))
    save(bed(acc, dark), "bed%d.png" % i)
    # occupied bed: head on pillow + blanket bump
    im = bed(acc, dark); ex = crew_extra(i)
    rect(im, 5, 3, 4, 3, ex['H']); rect(im, 5, 5, 4, 2, ex['S']); rect(im, 3, 10, 8, 10, hexc(dark)); rect(im, 4, 11, 6, 8, hexc(acc))
    save(im, "bed%d_sleep.png" % i)

def build(name, w, h, fn):
    im = Image.new("RGBA", (w, h), (0, 0, 0, 0)); fn(im); add_outline(im); save(im, name + ".png")

def desk(im):
    rect(im, 1, 6, 14, 8, 'l'); rect(im, 1, 6, 14, 1, 'L'); rect(im, 1, 13, 14, 1, 'g')
    rect(im, 4, 1, 8, 6, 'd'); rect(im, 5, 2, 6, 4, 's'); dot(im, 6, 3, 'W'); rect(im, 7, 7, 2, 1, 'd'); rect(im, 3, 9, 6, 2, 'm')
build("desk", 16, 15, desk)
def desk_off(im):
    desk(im); rect(im, 5, 2, 6, 4, 'v')
build("desk_off", 16, 15, desk_off)
def console(im):
    rect(im, 1, 5, 30, 9, 'g'); rect(im, 1, 5, 30, 1, 'l'); rect(im, 1, 13, 30, 1, 'm')
    for k, c in enumerate(['t', 's', 'a', 's']):
        rect(im, 2 + k * 7, 1, 6, 5, 'd'); rect(im, 3 + k * 7, 2, 4, 3, c)
    for k in range(10): dot(im, 3 + k * 3, 9, 'a' if k % 3 == 0 else 't')
build("console", 32, 15, console)
def table(im):
    rect(im, 2, 2, 28, 16, 'n'); rect(im, 2, 2, 28, 1, hexc("#a87553")); rect(im, 2, 17, 28, 2, 'N')
    rect(im, 6, 6, 4, 3, 'W'); rect(im, 22, 6, 4, 3, 'W'); rect(im, 14, 10, 4, 3, 'W'); dot(im, 15, 11, 'G'); dot(im, 7, 7, 'o')
build("table", 32, 20, table)
def stool(im):
    rect(im, 1, 1, 6, 5, 'm'); rect(im, 1, 1, 6, 1, 'g'); rect(im, 2, 6, 4, 1, 'd')
build("stool", 8, 8, stool)
def galley(im):
    rect(im, 1, 3, 30, 11, 'L'); rect(im, 1, 3, 30, 1, 'W'); rect(im, 1, 13, 30, 1, 'g')
    rect(im, 3, 5, 8, 6, 'd'); rect(im, 4, 6, 6, 4, 'v'); dot(im, 5, 7, 'a'); rect(im, 14, 5, 5, 4, 'l'); rect(im, 15, 6, 3, 2, 's')
    rect(im, 22, 5, 7, 6, 'w'); rect(im, 23, 6, 2, 2, 'o'); rect(im, 26, 7, 2, 2, 'r'); dot(im, 24, 9, 'G')
build("galley", 32, 15, galley)
def sofa(im):
    rect(im, 1, 1, 30, 5, hexc("#6b4f8a")); rect(im, 1, 6, 30, 6, hexc("#7d5fa0")); rect(im, 1, 6, 30, 1, hexc("#9479b8"))
    rect(im, 1, 1, 3, 11, hexc("#5b4277")); rect(im, 28, 1, 3, 11, hexc("#5b4277")); rect(im, 11, 7, 1, 4, hexc("#6b4f8a")); rect(im, 20, 7, 1, 4, hexc("#6b4f8a"))
build("sofa", 32, 13, sofa)
def screen(im):
    rect(im, 1, 1, 30, 9, 'd'); rect(im, 2, 2, 28, 7, 'v')
    for k in range(12): dot(im, 3 + k * 2, 3 + (k * 7) % 5, 'W')
    rect(im, 22, 3, 5, 5, 'e'); rect(im, 23, 4, 2, 2, 'G'); dot(im, 25, 6, 'W')
build("screen", 32, 11, screen)
def treadmill(im):
    rect(im, 2, 3, 12, 24, 'g'); rect(im, 3, 6, 10, 20, 'd')
    for y in range(7, 26, 3): rect(im, 3, y, 10, 1, 'm')
    rect(im, 1, 1, 14, 4, 'l'); rect(im, 4, 1, 8, 2, 's'); rect(im, 1, 3, 1, 8, 'L'); rect(im, 14, 3, 1, 8, 'L')
build("treadmill", 16, 28, treadmill)
def bike(im):
    rect(im, 6, 2, 4, 3, 'd'); rect(im, 7, 5, 2, 9, 'l'); rect(im, 3, 6, 10, 2, 'L'); rect(im, 4, 13, 8, 3, 'g'); rect(im, 5, 4, 6, 1, 'a')
build("bike", 16, 18, bike)
def ared(im):
    rect(im, 1, 2, 2, 14, 'L'); rect(im, 13, 2, 2, 14, 'L'); rect(im, 1, 5, 14, 2, 'l'); rect(im, 0, 4, 2, 4, 'd'); rect(im, 14, 4, 2, 4, 'd')
    rect(im, 4, 10, 8, 5, 'm'); rect(im, 4, 10, 8, 1, 'g'); rect(im, 6, 15, 4, 1, 'd')
build("ared", 16, 17, ared)
def rack(stage):
    def f(im):
        rect(im, 1, 8, 14, 7, 'g'); rect(im, 1, 8, 14, 1, 'l'); rect(im, 2, 9, 12, 4, 'u'); rect(im, 2, 9, 12, 1, 'U')
        rect(im, 1, 0, 14, 2, 'p'); rect(im, 2, 1, 12, 1, hexc("#ff7ad9"))  # red/blue LED bar
        if stage >= 1:
            for x in (3, 7, 11): rect(im, x, 7, 1, 2, 'y')
        if stage >= 2:
            for x in (2, 6, 10): rect(im, x, 5, 3, 3, 'G'); dot(im, x + 1, 5, 'Y')
        if stage >= 3:
            for x in (2, 6, 10): rect(im, x, 3, 3, 5, 'G'); rect(im, x - 1 if x > 2 else x, 5, 1, 2, 'F'); dot(im, x + 1, 3, 'Y')
        if stage >= 4:
            for x in (3, 7, 11): dot(im, x, 5, 'r'); dot(im, x - 1, 6, 'r')
        if stage == 5:  # wilted / blight
            for x in (2, 6, 10): rect(im, x, 4, 3, 4, hexc("#8a7d3a")); dot(im, x + 1, 4, hexc("#5a5226"))
    return f
for s in range(6): build("rack%d" % s, 16, 16, rack(s))
def medbed(im):
    rect(im, 1, 1, 12, 26, 'L'); rect(im, 2, 2, 10, 24, 'W'); rect(im, 3, 3, 8, 4, 'w'); rect(im, 1, 26, 12, 1, 'g')
    rect(im, 2, 12, 10, 1, 'x'); rect(im, 6, 15, 2, 6, 'r'); rect(im, 4, 17, 6, 2, 'r')
build("medbed", 14, 28, medbed)
def medcab(im):
    rect(im, 1, 1, 14, 14, 'w'); rect(im, 1, 1, 14, 1, 'W'); rect(im, 1, 14, 14, 1, 'L'); rect(im, 7, 4, 2, 7, 'r'); rect(im, 4, 6, 8, 2, 'r')
build("medcab", 16, 16, medcab)
def monitor(im):
    rect(im, 1, 1, 10, 8, 'd'); rect(im, 2, 2, 8, 6, 'v')
    pts = [5, 5, 5, 3, 7, 5, 5, 5]
    for x, y in enumerate(pts): dot(im, 2 + x, y, 'G')
    rect(im, 5, 9, 2, 4, 'l')
build("monitor", 12, 14, monitor)
def crate(im):
    rect(im, 1, 3, 14, 12, hexc("#8b7a55")); rect(im, 1, 3, 14, 1, hexc("#b19d70")); rect(im, 1, 14, 14, 1, hexc("#5e5239"))
    rect(im, 1, 8, 14, 1, hexc("#6e6045")); rect(im, 7, 3, 2, 12, hexc("#6e6045"))
build("crate", 16, 16, crate)
def shelf(im):
    rect(im, 1, 1, 14, 14, 'm'); rect(im, 2, 2, 12, 12, 'd')
    for y in (5, 9, 13): rect(im, 2, y, 12, 1, 'g')
    for x, c in ((3, 'a'), (6, 't'), (10, 'r'), (4, 'w'), (9, 'o')): rect(im, x, 2 + (x % 3) * 4, 2, 3, c)
build("shelf", 16, 16, shelf)
def eclss(frame_i):
    def f(im):
        rect(im, 1, 1, 14, 22, 'g'); rect(im, 1, 1, 14, 1, 'l'); rect(im, 2, 3, 12, 8, 'd'); rect(im, 2, 13, 12, 8, 'd')
        for k in range(4): rect(im, 3, 4 + k * 2, 10, 1, 'm')
        for k in range(3): dot(im, 3 + k * 4, 15, 't' if (k + frame_i) % 2 == 0 else 'T'); dot(im, 4 + k * 4, 18, 'G' if (k + frame_i) % 2 else 'F')
        rect(im, 1, 22, 14, 1, 'm')
    return f
for k in range(2): build("eclss%d" % k, 16, 24, eclss(k))
def eclss_broken(im):
    eclss(0)(im)
    for k in range(3): dot(im, 3 + k * 4, 15, 'r'); dot(im, 4 + k * 4, 18, 'R')
build("eclss_broken", 16, 24, eclss_broken)
def tank(im):
    rect(im, 3, 1, 10, 22, 'b'); rect(im, 2, 3, 12, 18, 'b'); rect(im, 4, 2, 2, 19, hexc("#7fb0ff")); rect(im, 2, 20, 12, 2, 'B'); rect(im, 3, 22, 10, 1, 'B')
    rect(im, 2, 8, 12, 1, 'B'); rect(im, 2, 15, 12, 1, 'B')
build("tank", 16, 24, tank)
def hatch(im):
    rect(im, 2, 1, 12, 14, 'l'); rect(im, 1, 3, 14, 10, 'l'); rect(im, 3, 3, 10, 10, 'g'); rect(im, 5, 5, 6, 6, 'd'); rect(im, 6, 6, 4, 4, 'v'); dot(im, 7, 7, 's')
    rect(im, 2, 7, 3, 2, 'a'); rect(im, 11, 7, 3, 2, 'a')
build("hatch", 16, 16, hatch)
def suit(im):
    rect(im, 1, 1, 14, 2, 'g')
    rect(im, 5, 3, 6, 5, 'W'); rect(im, 6, 4, 4, 3, hexc("#c2913a")); dot(im, 7, 4, 'q')
    rect(im, 3, 8, 10, 8, 'W'); rect(im, 2, 9, 2, 6, 'w'); rect(im, 12, 9, 2, 6, 'w'); rect(im, 6, 10, 4, 2, 'b'); dot(im, 7, 13, 'r')
    rect(im, 4, 16, 3, 6, 'w'); rect(im, 9, 16, 3, 6, 'w'); rect(im, 4, 21, 3, 2, 'l'); rect(im, 9, 21, 3, 2, 'l')
build("suit", 16, 24, suit)
def shower(im):
    rect(im, 1, 1, 14, 14, hexc("#7fb6c9")); rect(im, 2, 2, 12, 12, hexc("#a8d7e6")); rect(im, 3, 3, 10, 10, hexc("#c9ecf5"))
    rect(im, 7, 7, 2, 2, 'g'); rect(im, 1, 1, 14, 1, 'W')
build("shower", 16, 16, shower)
def toilet(im):
    rect(im, 3, 1, 10, 4, 'L'); rect(im, 4, 5, 8, 9, 'W'); rect(im, 5, 6, 6, 6, 'x'); rect(im, 6, 7, 4, 4, 'L'); rect(im, 3, 1, 10, 1, 'W')
build("toilet", 16, 15, toilet)
def window(im):
    rect(im, 0, 0, 32, 12, 'g'); rect(im, 2, 2, 28, 8, 'v')
    for (x, y) in ((4, 3), (9, 6), (14, 4), (19, 8), (25, 3), (12, 8), (28, 6)): dot(im, x, y, 'W')
    rect(im, 21, 4, 4, 4, 'e'); rect(im, 22, 4, 2, 2, 'G'); dot(im, 24, 7, 'W')
    rect(im, 2, 9, 28, 1, hexc("#4a4a50")); rect(im, 0, 0, 32, 1, 'l')
build("window", 32, 12, window)
def partition(im):
    rect(im, 1, 1, 6, 46, 'L'); rect(im, 2, 2, 4, 44, 'w')
    for y in range(6, 46, 8): rect(im, 2, y, 4, 1, 'L')
    rect(im, 3, 2, 2, 44, hexc("#e9edf4"))
build("partition", 8, 48, partition)
def robot(im):
    rect(im, 2, 3, 8, 6, 'L'); rect(im, 3, 4, 6, 3, 'd'); dot(im, 4, 5, 't'); dot(im, 7, 5, 't'); rect(im, 5, 1, 2, 2, 'g'); dot(im, 5, 0, 'a')
    rect(im, 1, 9, 10, 2, 'g'); rect(im, 2, 11, 2, 1, 'd'); rect(im, 8, 11, 2, 1, 'd')
build("robot", 12, 12, robot)
def plant_pot(im):
    rect(im, 3, 7, 6, 5, 'o'); rect(im, 3, 7, 6, 1, hexc("#f09a5a")); rect(im, 2, 2, 3, 5, 'G'); rect(im, 6, 1, 3, 6, 'G'); rect(im, 4, 3, 3, 4, 'F'); dot(im, 7, 2, 'Y'); dot(im, 3, 3, 'Y')
build("plant_pot", 12, 13, plant_pot)
def lamp(im):
    rect(im, 1, 1, 10, 4, 'q'); rect(im, 2, 2, 8, 2, 'W'); rect(im, 5, 5, 2, 6, 'l'); rect(im, 3, 11, 6, 1, 'g')
build("lamp", 12, 13, lamp)
def solar(im):
    for k in range(3):
        rect(im, 1 + k * 10, 1, 9, 14, 'B'); rect(im, 2 + k * 10, 2, 7, 12, 'z')
        for y in range(4, 14, 3): rect(im, 2 + k * 10, y, 7, 1, 'B')
    rect(im, 1, 15, 30, 2, 'l')
build("solar", 32, 18, solar)
def antenna(im):
    rect(im, 7, 6, 2, 10, 'l'); rect(im, 2, 1, 12, 6, 'L'); rect(im, 3, 2, 10, 4, 'w'); dot(im, 8, 0, 'r'); rect(im, 4, 15, 8, 2, 'g')
build("antenna", 16, 18, antenna)
def rover(im):
    rect(im, 2, 3, 20, 8, 'w'); rect(im, 3, 4, 18, 6, 'L'); rect(im, 15, 4, 5, 3, 's'); rect(im, 1, 10, 22, 2, 'g')
    for x in (2, 9, 17): rect(im, x, 11, 5, 4, 'd'); rect(im, x + 1, 12, 3, 2, 'm')
build("rover", 24, 16, rover)

# =====================================================================================================
#  ICONS 8x8 — sensors and conditions
# =====================================================================================================
ICONS = {
    # sensors
    "s_hrv": ["........", ".rr.rr..", "rrrrrrr.", "rWrrrrr.", ".rrrrr..", "..rrr...", "...r....", "........"],
    "s_gsr": ["...t....", "...t....", "..ttt...", ".ttttt..", ".tsttt..", ".ttttt..", "..ttt...", "........"],
    "s_sleep": ["..aaa...", ".aa.....", "aa....q.", "aa......", "aa...q..", ".aa...a.", "..aaaa..", "........"],
    "s_voice": ["....t...", "..t.t...", "..t.tt.t", "t.tttt.t", "..t.tt.t", "..t.t...", "....t...", "........"],
    "s_facs": [".wwwww..", "w.....w.", "w.k.k.w.", "w.....w.", "w.k..kw.", "w..kk.w.", ".wwwww..", "........"],
    "s_motion": [".yy.....", ".yy.....", "..y..yy.", ".....yy.", ".yy...y.", ".yy.....", "..y..yy.", ".....yy."],
    "s_cortisol": ["........", ".pppppp.", ".pWWWWp.", ".pWppWp.", ".pWppWp.", ".pWWWWp.", ".pppppp.", "........"],
    "s_co2": ["........", "...LL...", ".LLLLLL.", "LLwwLLLL", "LwLLwLLL", ".LLLLLL.", "........", "........"],
    # conditions
    "c_anxiety": ["........", "r.....r.", ".r...r..", "..r.r...", "r..r..r.", ".r...r..", "..r.r...", "...r...."],
    "c_depression": ["..bbb...", ".bbbbbb.", "bbbbbbbb", ".bbbbbb.", "........", ".b..b...", "...b..b.", ".b......"],
    "c_conflict": ["....oo..", "...oo...", "..oo....", ".oooooo.", "....oo..", "...oo...", "..oo....", ".o......"],
    "c_fatigue": [".aaaaaa.", "a......a", "a.aa...a", "a.aa...a", "a......a", ".aaaaaa.", "........", "........"],
    "c_autonomy": ["..ppp...", ".p...p..", ".p...p..", "ppppppp.", "pp.P.pp.", "pp.P.pp.", "ppppppp.", "........"],
    "c_monotony": ["gggggggg", "g.g.g.g.", "gggggggg", ".g.g.g.g", "gggggggg", "g.g.g.g.", "gggggggg", "........"],
    # misc UI
    "i_power": ["....a...", "...aa...", "..aa....", ".aaaaa..", "...aa...", "..aa....", ".aa.....", "........"],
    "i_o2": ["........", ".bbb....", "b...b...", "b...b...", "b...b...", ".bbb.bb.", ".......b", "......bb"],
    "i_water": ["...b....", "..bbb...", ".bbbbb..", "bbbbbbb.", "bbsbbbb.", ".bbbbb..", "..bbb...", "........"],
    "i_food": ["....G...", "...G....", ".rrrrr..", "rrWrrrr.", "rrrrrrr.", "rrrrrrr.", ".rrrrr..", "........"],
    "i_trust": [".tt.tt..", "tttttttt", "tttttttt", ".tttttt.", "..tttt..", "...tt...", "........", "........"],
    "i_science": ["..wwww..", "...ww...", "...ww...", "..wwww..", ".wtttww.", "wttttttw", "wwwwwwww", "........"],
    "i_lock": ["..LLL...", ".L...L..", ".L...L..", "LLLLLLL.", "LLLdLLL.", "LLLdLLL.", "LLLLLLL.", "........"],
    "i_eye": ["........", "..www...", ".w.k.w..", "w.kkk.w.", ".w.k.w..", "..www...", "........", "........"],
    "i_bell": ["...a....", "..aaa...", ".aaaaa..", ".aaaaa..", ".aaaaa..", "aaaaaaa.", "...a....", "........"],
    "i_check": ["........", "......G.", ".....GG.", "G...GG..", "GG.GG...", ".GGG....", "..G.....", "........"],
    "i_cross": ["........", "r.....r.", ".r...r..", "..r.r...", "...r....", "..r.r...", ".r...r..", "r.....r."],
}
for name, rows in ICONS.items():
    save(from_ascii(rows), name + ".png")

# =====================================================================================================
#  TITLE ART: big pixel moon + habitat silhouette (160x90, scaled up in game)
# =====================================================================================================
rnd = random.Random(7)
im = Image.new("RGBA", (320, 180), hexc("#0b0e17"))
px = im.load()
for _ in range(160):
    x, y = rnd.randrange(320), rnd.randrange(120); px[x, y] = hexc(rnd.choice(["#ffffff", "#9fb3d9", "#d9c7a0", "#6e7fa8"]))
# earth
cx, cy, r = 255, 38, 15
for y in range(cy - r, cy + r + 1):
    for x in range(cx - r, cx + r + 1):
        dd = (x - cx) ** 2 + (y - cy) ** 2
        if dd <= r * r:
            c = "#2f6fd6"
            if (x - cx + 3) ** 2 + (y - cy + 2) ** 2 < 40 or (x - cx - 6) ** 2 + (y - cy - 5) ** 2 < 25 or (x - cx + 7) ** 2 + (y - cy - 8) ** 2 < 10: c = "#4caf50"
            if (x - cx) + (y - cy) > 9: c = "#1b3f80" if c == "#2f6fd6" else "#2b6a33"
            if y < cy - r + 3 or (y - cy + 4) ** 2 + (x - cx + 8) ** 2 < 6 or (y - cy - 2) ** 2 // 2 + (x - cx - 2) ** 2 < 3: c = "#e8f0ff"
            px[x, y] = hexc(c)
# moon surface horizon
for x in range(320):
    h = 128 + int(6 * __import__("math").sin(x / 23.0) + 3 * __import__("math").sin(x / 7.3))
    for y in range(h, 180):
        px[x, y] = hexc("#6a6a70" if y < h + 2 else ("#5d5d63" if (x * 7 + y * 3) % 13 else "#525258"))
for (cx, cy, rr) in ((40, 160, 9), (120, 170, 6), (210, 158, 11), (290, 168, 7)):
    for y in range(cy - rr // 2, cy + rr // 2 + 1):
        for x in range(cx - rr, cx + rr + 1):
            if ((x - cx) / rr) ** 2 + ((y - cy) / (rr / 2)) ** 2 <= 1 and 0 <= x < 320 and y < 180:
                px[x, y] = hexc("#4c4c52" if y < cy else "#77777d")
# habitat pill (cylinder) + greenhouse dome + solar + antenna
for y in range(112, 134):
    for x in range(96, 224):
        e = min(x - 96, 223 - x)
        if e < 8 and (y - 123) ** 2 > (e * 1.4) ** 2 + 20: continue
        c = "#c9ced8"
        if y < 116: c = "#e8ecf2"
        elif y > 129: c = "#8f97a8"
        if (x - 96) % 32 == 0: c = "#7b879e"
        px[x, y] = hexc(c)
for k, x in enumerate(range(108, 214, 14)):
    for yy in range(119, 124):
        for xx in range(x, x + 6): px[xx, yy] = hexc("#f2c26b" if k % 3 != 1 else "#9be7ff")
for y in range(100, 113):
    for x in range(228, 262):
        if ((x - 245) / 17) ** 2 + ((y - 113) / 13) ** 2 <= 1:
            px[x, y] = hexc("#5fbf6a" if (x + y) % 5 else "#b5e37a")
for y in range(113, 134):
    for x in range(226, 264): px[x, y] = hexc("#aab3c3" if y > 117 else "#d3dae6")
for k in range(3):
    for y in range(100, 116):
        for x in range(30 + k * 18, 44 + k * 18):
            px[x, y] = hexc("#2f6fd6" if (y + x) % 4 else "#2c4f9e")
    for y in range(116, 128): px[37 + k * 18, y] = hexc("#76839e")
for y in range(92, 128): px[80, y] = hexc("#a6b1c6")
for x in range(74, 87): px[x, 92] = hexc("#d3dae6")
px[80, 90] = hexc("#e5484d")
im.save(os.path.join(OUT, "title.png"))
print("art done:", len(os.listdir(OUT)), "files")
