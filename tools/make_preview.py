# Workshop preview for AutoExplore Extended: original pixel art drawn in code (no game assets).
# A compass rose (exploring) with the four tools this mod teaches AutoExplore around it: crowbar,
# lockpick, shears and shovel. Art is drawn on a 128x128 pixel grid (8x scale to 1024), each object
# on its own layer with an automatic dark outline and a light top-left edge, then a serif title banner.
import math, random, sys
from PIL import Image, ImageDraw, ImageFont

S, G, BANNER = 8, 128, 26
random.seed(11)

BG_TOP, BG_BOT = (22, 17, 13), (38, 28, 20)
OUTLINE = (12, 9, 7)
STEEL = [(84, 92, 104), (132, 142, 156), (186, 194, 206), (232, 238, 244)]
WOOD = [(70, 44, 26), (108, 70, 40), (146, 98, 56), (182, 132, 80)]
BRASS = [(110, 74, 30), (168, 122, 52), (214, 170, 84), (246, 214, 138)]
GOLD = [(132, 92, 40), (192, 144, 64), (230, 184, 96), (250, 222, 150)]
PARCH = (234, 216, 180)
RING = (92, 70, 48)

class Layer:
    def __init__(self, pivot=None, deg=0):  # shapes are drawn upright and rotated about pivot before rasterizing
        self.p = {}; self.pivot = pivot; self.a = math.radians(deg)
    def T(self, x, y):
        if not self.pivot or not self.a: return x, y
        px_, py_ = self.pivot; dx, dy = x - px_, y - py_
        return px_ + dx * math.cos(self.a) - dy * math.sin(self.a), py_ + dx * math.sin(self.a) + dy * math.cos(self.a)
    def put(self, x, y, c, raw=False):
        if not raw: x, y = (round(v) for v in self.T(x, y))
        if 0 <= x < G and 0 <= y < G - BANNER: self.p[(x, y)] = c
    def disc(self, cx, cy, r, c):
        cx, cy = self.T(cx, cy)
        for y in range(int(cy - r) - 1, int(cy + r) + 2):
            for x in range(int(cx - r) - 1, int(cx + r) + 2):
                if (x - cx) ** 2 + (y - cy) ** 2 <= r * r: self.put(x, y, c, raw=True)
    def line(self, x0, y0, x1, y1, w, c):
        (x0, y0), (x1, y1) = self.T(x0, y0), self.T(x1, y1)
        saved, self.a = self.a, 0
        self._line(x0, y0, x1, y1, w, c); self.a = saved
    def _line(self, x0, y0, x1, y1, w, c):
        n = int(max(abs(x1 - x0), abs(y1 - y0)) * 2) + 1
        for i in range(n + 1):
            t = i / n
            self.disc(x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, w / 2, c)
    def poly(self, pts, c):
        pts = [self.T(*q) for q in pts]
        ys = [p[1] for p in pts]
        for y in range(int(min(ys)), int(max(ys)) + 1):
            xs = []
            for i in range(len(pts)):
                (x1, y1), (x2, y2) = pts[i], pts[(i + 1) % len(pts)]
                if (y1 <= y + .5 < y2) or (y2 <= y + .5 < y1):
                    xs.append(x1 + (y + .5 - y1) * (x2 - x1) / (y2 - y1))
            xs.sort()
            for a, b in zip(xs[::2], xs[1::2]):
                for x in range(int(round(a)), int(round(b))): self.put(x, y, c, raw=True)
    def shade(self, ramp):  # light from the top-left
        out = dict(self.p)
        for (x, y), c in self.p.items():
            if c not in ramp: continue
            i = ramp.index(c)
            if (x - 1, y) not in self.p or (x, y - 1) not in self.p: out[(x, y)] = ramp[min(i + 1, len(ramp) - 1)]
            elif (x + 1, y) not in self.p or (x, y + 1) not in self.p: out[(x, y)] = ramp[max(i - 1, 0)]
        self.p = out
    def outline(self):
        edge = {}
        for (x, y) in self.p:
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                q = (x + dx, y + dy)
                if q not in self.p: edge[q] = OUTLINE
        self.p.update(edge)

img = Image.new("RGB", (G * S, G * S))
d = ImageDraw.Draw(img)
for y in range(G):
    t = y / (G - 1)
    d.rectangle([0, y * S, G * S, (y + 1) * S], fill=tuple(int(BG_TOP[i] + (BG_BOT[i] - BG_TOP[i]) * t) for i in range(3)))

def blit(layer):
    for (x, y), c in layer.p.items():
        if 0 <= x < G and 0 <= y < G - BANNER: d.rectangle([x * S, y * S, x * S + S - 1, y * S + S - 1], fill=c)

bg = Layer()
for _ in range(70): bg.put(random.randrange(G), random.randrange(G - BANNER), (58, 46, 36))
cx, cy, R = 64, 51, 37
for i in range(0, 180, 3):
    a = 2 * math.pi * i / 180
    bg.put(round(cx + R * math.cos(a)), round(cy + R * math.sin(a)), RING)
blit(bg)

rose = Layer()
def point(angle, length, width, light, dark):
    tip = (cx + length * math.cos(angle), cy + length * math.sin(angle))
    l = (cx + width * math.cos(angle + math.pi / 2), cy + width * math.sin(angle + math.pi / 2))
    r = (cx + width * math.cos(angle - math.pi / 2), cy + width * math.sin(angle - math.pi / 2))
    rose.poly([tip, l, (cx, cy)], light); rose.poly([tip, r, (cx, cy)], dark)
for k in range(4): point(math.pi / 4 + k * math.pi / 2, 11, 4, GOLD[1], GOLD[0])
for k in range(4): point(-math.pi / 2 + k * math.pi / 2, 19, 5, PARCH, GOLD[2])
rose.disc(cx, cy, 3.2, GOLD[1]); rose.disc(cx, cy, 1.6, PARCH)
rose.outline()

def at(angle): return cx + R * math.cos(angle), cy + R * math.sin(angle)

def shovel(ox, oy, deg=0):
    L = Layer((ox, oy), deg)
    L.line(ox - 4, oy - 15, ox + 4, oy - 15, 2.6, WOOD[2])
    L.line(ox, oy - 15, ox, oy + 2, 2.6, WOOD[1])
    L.line(ox, oy + 1, ox, oy + 4, 3.2, STEEL[0])
    L.poly([(ox - 6, oy + 4), (ox + 6, oy + 4), (ox + 6, oy + 11), (ox, oy + 16), (ox - 6, oy + 11)], STEEL[1])
    L.line(ox, oy + 6, ox, oy + 12, 1, STEEL[0])
    L.shade(WOOD); L.shade(STEEL); L.outline(); return L

def crowbar(ox, oy, deg=0):
    L = Layer()
    L.line(ox - 11, oy + 12, ox + 5, oy - 5, 2.8, STEEL[1])          # bar
    pts = [(ox + 8 + 3.6 * math.cos(math.radians(a)), oy - 6 + 3.6 * math.sin(math.radians(a))) for a in range(150, 400, 15)]
    for (x0, y0), (x1, y1) in zip(pts, pts[1:]): L.line(x0, y0, x1, y1, 2.8, STEEL[1])  # J hook curling over
    L.poly([(ox - 13, oy + 11), (ox - 9, oy + 15), (ox - 14, oy + 16)], STEEL[1])        # flat chisel end
    L.shade(STEEL); L.outline()
    L.p[(round(pts[-1][0]), round(pts[-1][1]) + 1)] = OUTLINE                            # split claw notch
    return L

def lockpick(ox, oy, deg=0):  # a pick pouch: picks standing parallel out of a small leather bag
    L = Layer((ox, oy), deg)
    top = oy - 2                                                     # pouch mouth
    for dx, h, tip in [(-4, 12, "hook"), (-1, 15, "rake"), (2, 11, "diamond"), (5, 13, "half")]:
        x = ox + dx
        L.line(x, top, x, top - h, 1.2, STEEL[2])
        y = top - h
        if tip == "hook": L.put(x + 1, y, STEEL[2]); L.put(x + 1, y + 1, STEEL[2])
        elif tip == "rake":
            for k in range(3): L.put(x + 1, y + 1 + k * 2, STEEL[2])
        elif tip == "diamond": L.put(x - 1, y + 1, STEEL[2]); L.put(x + 1, y + 1, STEEL[2])
        else: L.put(x + 1, y + 1, STEEL[2]); L.put(x + 1, y + 2, STEEL[2])
    # pouch body: rounded leather bag, a little wider at the bottom
    L.poly([(ox - 8, top), (ox + 8, top), (ox + 9, top + 12), (ox + 7, top + 15), (ox - 7, top + 15), (ox - 9, top + 12)], WOOD[1])
    L.line(ox - 8, top + 2, ox + 8, top + 2, 1.6, WOOD[0])           # gathered neck
    L.line(ox - 7, top + 3, ox + 7, top + 3, 1, BRASS[2])            # drawstring
    L.disc(ox + 6, top + 5, 1.1, BRASS[2])                           # string knot
    L.line(ox - 4, top + 8, ox - 4, top + 12, 1, WOOD[0])            # stitched seam hint
    L.shade(WOOD); L.shade(BRASS); L.outline(); return L

def shears(ox, oy, deg=0):  # sheep shears: two broad blades, two handles, a spring bow joining them
    L = Layer((ox, oy), deg)
    top = oy - 3
    for sx in (-1, 1):
        for i in range(31):                                          # blade, opening slightly toward the tip
            t = i / 30
            L.disc(ox + sx * (2.6 + 1.6 * t), top - 13 * t, 2.0 * (1 - t) ** 0.7 + 0.5, STEEL[1])
        L.line(ox + sx * 2.6, top + 1, ox + sx * 2.6, top + 2, 2.6, BRASS[1])   # ferrule
        L.line(ox + sx * 2.8, top + 3, ox + sx * 4.2, top + 13, 2.4, WOOD[2])   # handle
    for i in range(13):                                              # spring bow joining the handle ends
        a = i * math.pi / 12
        L.disc(ox + 4.2 * math.cos(a), top + 14 + 3.5 * math.sin(a), 1.1, STEEL[1])
    L.shade(STEEL); L.shade(BRASS); L.shade(WOOD); L.outline(); return L

for make, angle, lean in [(crowbar, -math.pi / 4, 0), (lockpick, math.pi / 4, 35), (shears, 3 * math.pi / 4, -35), (shovel, -3 * math.pi / 4, -40)]:
    x, y = at(angle)
    blit(make(round(x), round(y), lean))
blit(rose)

top = (G - BANNER) * S
d.rectangle([0, top, G * S, G * S], fill=(18, 13, 10))
d.rectangle([0, top, G * S, top + 4], fill=(120, 92, 60))
def font(names, size):
    for n in names:
        try: return ImageFont.truetype(n, size)
        except OSError: pass
    return ImageFont.load_default()
size = 82
while True:
    title = font(["C:/Windows/Fonts/georgiab.ttf", "C:/Windows/Fonts/timesbd.ttf"], size)
    if d.textlength("AUTOEXPLORE EXTENDED", font=title) <= G * S - 90 or size <= 40: break
    size -= 2
sub = font(["C:/Windows/Fonts/georgia.ttf", "C:/Windows/Fonts/times.ttf"], 38)
def center(text, f, y, c, shadow=True):
    x = (G * S - d.textlength(text, font=f)) / 2
    if shadow: d.text((x + 4, y + 4), text, font=f, fill=(0, 0, 0))
    d.text((x, y), text, font=f, fill=c)
center("AUTOEXPLORE EXTENDED", title, top + 26, PARCH)
center("crowbar · lockpick · shears · shovel", sub, top + 130, GOLD[2], shadow=False)

out = sys.argv[1] if len(sys.argv) > 1 else "preview.png"
img.save(out)
print(out)
