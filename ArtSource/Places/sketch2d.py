"""Low-res 2D place sketches: a station, den or mission site as a cell grid (one cell = one 8 m block), drawn as a side
section and a plan, with routes, points of interest and notes. For agreeing on a layout in minutes before any 3D.

Owner (2026-11-10): "just give me 2d low res options for the dens, and the heist station because you spend 30 minutes
completely missaligned otherwise".

A sketch is a Sketch with two Views ("section": x across, z up; "plan": x across, y north). Each view is a grid of
materials painted with shapes (later shapes win), plus overlays: the core, tethers, shutters, fans, turrets,
searchlights, charges, lamps, ships, growth slots, labels, numbered points and routes. Coordinates are in cells, origin
bottom-left, y up.

Run:  python3 ArtSource/Places/sketch2d.py [key ...]        (sketches are listed in sketches.py)
Out:  docs/design/places/sketch/<key>.png and overview.png
"""
import math
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "Racing"))
from racing_common import ROOT, F_TITLE, F_BODY, F_BOLD, wrap  # noqa: E402
from PIL import Image, ImageDraw, ImageFont  # noqa: E402

_FONTS = {}


def font(path, size):
    k = (path, size)
    if k not in _FONTS:
        _FONTS[k] = ImageFont.truetype(path, int(size))
    return _FONTS[k]

OUT = os.path.join(ROOT, "docs", "design", "places", "sketch")
CELL_M = 8.0

# materials: colour, outline (None = no cell edge). Space is the background.
MAT = {
    "space": None,
    "air": ((22, 26, 30), None),            # inside a structure: flyable
    "armor": ((86, 88, 82), (58, 60, 55)),  # thick armour: only explosives cut it
    "hull": ((128, 130, 122), (98, 100, 93)),  # plate: tools and guns cut it, slowly and loudly
    "deck": ((104, 100, 88), (80, 77, 68)),
    "rock": ((92, 86, 76), (70, 65, 57)),
    "rock2": ((110, 100, 84), (84, 76, 64)),
    "wreck": ((120, 84, 60), (90, 62, 44)),  # broken, twisted: passable only where carved
    "glass": ((40, 70, 80), (30, 52, 60)),
    "ivory": ((196, 190, 172), (150, 145, 130)),   # Alliance ivory
    "jade": ((40, 120, 86), (28, 86, 62)),          # Alliance green
    "shanty": ((132, 96, 66), (100, 72, 50)),       # patched, hand-built
    "shanty2": ((100, 94, 88), (76, 72, 67)),
    "truss": ((74, 80, 86), None),                  # scaffolding, girders, cranes
    "tank": ((176, 140, 60), (130, 102, 44)),       # fuel
    "ore": ((170, 132, 70), (128, 98, 52)),
}
MAT_TEXT = {"armor": "armour: only explosives cut it", "hull": "plate: cut slowly, loudly", "air": "inside: flyable", "rock": "rock: dig it",
            "wreck": "wreckage", "ivory": "Alliance buildings", "shanty": "hand-built town", "truss": "girders, scaffolds", "tank": "fuel",
            "ore": "ore vein", "glass": "glass"}
COL = {"in": (184, 156, 255), "out": (255, 174, 74), "loud": (255, 70, 60), "eject": (157, 255, 60), "patrol": (255, 70, 60),
       "home": (255, 241, 214), "tow": (255, 106, 42), "core": (90, 240, 255), "danger": (255, 70, 60), "door": (255, 179, 71),
       "shutter": (255, 106, 42), "lamp": (255, 174, 74), "text": (226, 228, 220), "dim": (150, 154, 144), "slot": (157, 255, 60),
       "power": (90, 240, 255), "spin": (220, 235, 255), "lane": (124, 255, 154), "friend": (46, 196, 182), "fly": (184, 156, 255), "cyan": (46, 196, 182), "pink": (255, 46, 136), "white": (220, 235, 255)}


class View:
    def __init__(self, name, w, h, caption=""):
        self.name, self.w, self.h, self.caption = name, w, h, caption
        self.g = [["space"] * w for _ in range(h)]
        self.over = []          # overlays (kind, data)
        self.routes = []
        self.tints = []         # (cells set, colour) e.g. what the first core powers

    # ------------------------------------------------------------------ painting
    def set(self, x, y, m):
        if 0 <= x < self.w and 0 <= y < self.h:
            self.g[y][x] = m

    def rect(self, x0, y0, x1, y1, m):
        for y in range(int(y0), int(y1)):
            for x in range(int(x0), int(x1)):
                self.set(x, y, m)

    def shell(self, x0, y0, x1, y1, t, m="armor", inside="air"):
        self.rect(x0, y0, x1, y1, m)
        self.rect(x0 + t, y0 + t, x1 - t, y1 - t, inside)

    def disc(self, cx, cy, r, m, noise=0.0, seed=0, sy=1.0):
        rng = random.Random(seed)
        ph = [rng.uniform(0, 6.3) for _ in range(4)]
        for y in range(int(cy - r * sy - 3), int(cy + r * sy + 4)):
            for x in range(int(cx - r - 3), int(cx + r + 4)):
                dx, dy = x + 0.5 - cx, (y + 0.5 - cy) / sy
                a = math.atan2(dy, dx)
                rr = r * (1 + noise * (0.5 * math.sin(3 * a + ph[0]) + 0.3 * math.sin(5 * a + ph[1]) + 0.2 * math.sin(9 * a + ph[2])))
                if dx * dx + dy * dy <= rr * rr:
                    self.set(x, y, m)

    def ring(self, cx, cy, r0, r1, m):
        for y in range(int(cy - r1 - 1), int(cy + r1 + 2)):
            for x in range(int(cx - r1 - 1), int(cx + r1 + 2)):
                d = math.hypot(x + 0.5 - cx, y + 0.5 - cy)
                if r0 <= d <= r1:
                    self.set(x, y, m)

    def line(self, pts, w, m):
        """A thick polyline (a tunnel when m is air or space)."""
        for (ax, ay), (bx, by) in zip(pts, pts[1:]):
            L = max(1, int(math.hypot(bx - ax, by - ay) * 3))
            for k in range(L + 1):
                t = k / L
                cx, cy = ax + (bx - ax) * t, ay + (by - ay) * t
                for y in range(int(cy - w), int(cy + w) + 1):
                    for x in range(int(cx - w), int(cx + w) + 1):
                        if math.hypot(x + 0.5 - cx, y + 0.5 - cy) <= w / 2:
                            self.set(x, y, m)

    def poly(self, pts, m):
        xs, ys = [p[0] for p in pts], [p[1] for p in pts]
        for y in range(int(min(ys)), int(max(ys)) + 1):
            for x in range(int(min(xs)), int(max(xs)) + 1):
                if inside((x + 0.5, y + 0.5), pts):
                    self.set(x, y, m)

    def scatter(self, x0, y0, x1, y1, m, frac, seed=0, only=None):
        rng = random.Random(seed)
        for y in range(int(y0), int(y1)):
            for x in range(int(x0), int(x1)):
                if 0 <= x < self.w and 0 <= y < self.h and (only is None or self.g[y][x] in only) and rng.random() < frac:
                    self.g[y][x] = m

    def tint(self, x0, y0, x1, y1, colour=COL["power"]):
        self.tints.append(((x0, y0, x1, y1), colour))

    # ------------------------------------------------------------------ overlays
    def core(self, c, r=2.5, label="core", dead=False):
        self.over.append(("core", dict(c=c, r=r, label=label, dead=dead)))

    def tether(self, a, b, colour=(200, 230, 240)):
        self.over.append(("tether", dict(a=a, b=b, colour=colour)))

    def shutter(self, a, b, label=""):
        """A blast shutter: open now, slams shut on the alarm."""
        self.over.append(("shutter", dict(a=a, b=b, label=label)))

    def door(self, a, b, label=""):
        self.over.append(("door", dict(a=a, b=b, label=label)))

    def fan(self, c, r=1.5):
        self.over.append(("fan", dict(c=c, r=r)))

    def turret(self, c, facing=0):
        self.over.append(("turret", dict(c=c, facing=facing)))

    def eye(self, c, facing, reach=10, arc=50, colour=(255, 230, 160)):
        """A searchlight, sensor cone or gun arc (degrees, 0 = +x, counter-clockwise)."""
        self.over.append(("eye", dict(c=c, facing=facing, reach=reach, arc=arc, colour=colour)))

    def dot(self, c, colour=COL["lamp"], r=2):
        """A small light: a stall, a window, a mine."""
        self.over.append(("dot", dict(c=c, colour=colour, r=r)))

    def bar(self, a, b, colour, label="", width=3):
        self.over.append(("bar", dict(a=a, b=b, colour=colour, label=label, width=width)))

    def ghost(self, x0, y0, x1, y1, label="place"):
        """Editor preview: blocks about to be placed."""
        self.over.append(("ghost", dict(r=(x0, y0, x1, y1), label=label)))

    def dig(self, x0, y0, x1, y1, label="dig"):
        """Editor preview: cells marked to dig out."""
        self.over.append(("dig", dict(r=(x0, y0, x1, y1), label=label)))

    def charge(self, c, label="charge"):
        self.over.append(("charge", dict(c=c, label=label)))

    def lamp(self, c, colour=COL["lamp"]):
        self.over.append(("lamp", dict(c=c, colour=colour)))

    def ship(self, c, heading=0, colour=(200, 200, 190), size=3.0):
        self.over.append(("ship", dict(c=c, heading=heading, colour=colour, size=size)))

    def slot(self, x0, y0, x1, y1, label=""):
        """A growth slot: where the crew can build next (dashed)."""
        self.over.append(("slot", dict(r=(x0, y0, x1, y1), label=label)))

    def label(self, c, text, colour=COL["dim"], anchor="mm", size=13):
        self.over.append(("label", dict(c=c, text=text, colour=colour, anchor=anchor, size=size)))

    def poi(self, n, c, colour=(255, 255, 255)):
        self.over.append(("poi", dict(n=n, c=c, colour=colour)))

    def nope(self, c, text=""):
        """A red cross: the core does not fit here."""
        self.over.append(("nope", dict(c=c, text=text)))

    def route(self, pts, kind="in", label=""):
        self.routes.append(dict(pts=pts, kind=kind, label=label))


class Sketch:
    def __init__(self, key, group, letter, title, line):
        self.key, self.group, self.letter, self.title, self.line = key, group, letter, title, line
        self.views = []
        self.notes = []          # (heading, [lines])
        self.key_items = []      # (colour, text)
        self.recommended = False

    def view(self, name, w, h, caption=""):
        v = View(name, w, h, caption)
        self.views.append(v)
        return v

    def note(self, heading, *lines):
        self.notes.append((heading, list(lines)))


def inside(p, poly):
    x, y = p
    c = False
    for (ax, ay), (bx, by) in zip(poly, poly[1:] + poly[:1]):
        if (ay > y) != (by > y) and x < (bx - ax) * (y - ay) / (by - ay + 1e-9) + ax:
            c = not c
    return c


# ====================================================================================================================
def render_view(v, cs):
    """Draw one view at cs pixels per cell. Returns an RGB image."""
    W, H = v.w * cs, v.h * cs
    img = Image.new("RGB", (W, H), (6, 7, 9))
    d = ImageDraw.Draw(img, "RGBA")
    rng = random.Random(5)
    for _ in range(int(v.w * v.h * 0.03)):
        x, y = rng.uniform(0, W), rng.uniform(0, H)
        b = rng.randint(40, 110)
        d.point((x, y), fill=(b, b, b + 10))

    def P(x, y):
        return (x * cs, H - y * cs)

    for (x0, y0, x1, y1), colour in v.tints:
        d.rectangle([P(x0, y1), P(x1, y0)], fill=colour + (26,))
    rng = random.Random(9)
    for y in range(v.h):
        for x in range(v.w):
            m = v.g[y][x]
            if m == "space":
                continue
            fill, edge = MAT[m]
            if m in ("rock", "rock2", "wreck"):
                k = rng.uniform(0.86, 1.1)
                fill = tuple(min(255, int(c * k)) for c in fill)
            x0, y0 = x * cs, H - (y + 1) * cs
            d.rectangle([x0, y0, x0 + cs - 1, y0 + cs - 1], fill=fill, outline=edge)
    for (x0, y0, x1, y1), colour in v.tints:
        d.rectangle([P(x0, y1), P(x1, y0)], outline=colour + (120,), width=1)

    f_s, f_b = font(F_BODY, 12), font(F_BOLD, 13)
    for kind, o in v.over:
        if kind == "tether":
            d.line([P(*o["a"]), P(*o["b"])], fill=tuple(o["colour"]) + (210,), width=2)
    for kind, o in v.over:
        if kind == "core":
            cx, cy = P(*o["c"])
            r = o["r"] * cs
            if o["dead"]:
                d.ellipse([cx - r, cy - r, cx + r, cy + r], outline=(120, 130, 135), width=2)
                d.line([cx - r * 0.6, cy - r * 0.6, cx + r * 0.6, cy + r * 0.6], fill=(120, 130, 135), width=2)
            else:
                for k in range(4, 0, -1):
                    rr = r * (1 + k * 0.35)
                    d.ellipse([cx - rr, cy - rr, cx + rr, cy + rr], fill=COL["core"] + (18,))
                d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(30, 120, 140), outline=COL["core"], width=2)
                d.ellipse([cx - r * 0.45, cy - r * 0.45, cx + r * 0.45, cy + r * 0.45], fill=(220, 255, 255))
            if o["label"]:
                d.text((cx, cy + r + 4), o["label"], fill=COL["core"], font=f_b, anchor="mt")
        elif kind in ("shutter", "door"):
            col = COL["shutter"] if kind == "shutter" else COL["door"]
            a, b = P(*o["a"]), P(*o["b"])
            d.line([a, b], fill=col, width=max(3, cs // 3))
            if kind == "shutter":
                n = int(math.dist(a, b) / 6)
                for k in range(n + 1):
                    t = k / max(1, n)
                    q = (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)
                    d.ellipse([q[0] - 1.5, q[1] - 1.5, q[0] + 1.5, q[1] + 1.5], fill=(30, 20, 10))
            if o["label"]:
                m = ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2)
                d.text((m[0] + 6, m[1]), o["label"], fill=col, font=f_s, anchor="lm")
        elif kind == "fan":
            cx, cy = P(*o["c"])
            r = o["r"] * cs
            for a in (20, 80, 140):
                ra = math.radians(a)
                d.line([cx - r * math.cos(ra), cy - r * math.sin(ra), cx + r * math.cos(ra), cy + r * math.sin(ra)], fill=(230, 210, 120), width=2)
            d.ellipse([cx - r, cy - r, cx + r, cy + r], outline=(230, 210, 120, 160), width=1)
        elif kind == "eye":
            cx, cy = P(*o["c"])
            r = o["reach"] * cs
            a0, a1 = -o["facing"] - o["arc"] / 2, -o["facing"] + o["arc"] / 2
            d.pieslice([cx - r, cy - r, cx + r, cy + r], a0, a1, fill=o["colour"] + (30,), outline=o["colour"] + (80,))
            d.ellipse([cx - 3, cy - 3, cx + 3, cy + 3], fill=o["colour"])
        elif kind == "dot":
            cx, cy = P(*o["c"])
            r = o["r"]
            d.ellipse([cx - r * 2.2, cy - r * 2.2, cx + r * 2.2, cy + r * 2.2], fill=o["colour"] + (46,))
            d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=o["colour"])
        elif kind == "bar":
            a, b = P(*o["a"]), P(*o["b"])
            dash(d, [a, b], o["colour"], o["width"], 7, 4)
            if o["label"]:
                d.text(((a[0] + b[0]) / 2, min(a[1], b[1]) - 4), o["label"], fill=o["colour"], font=f_s, anchor="mb")
        elif kind in ("ghost", "dig"):
            x0, y0, x1, y1 = o["r"]
            a, b = P(x0, y1), P(x1, y0)
            col = (124, 255, 154) if kind == "ghost" else (255, 90, 70)
            d.rectangle([a, b], fill=col + (70,), outline=col, width=2)
            for gx in range(int(x0) + 1, int(x1)):
                d.line([P(gx, y0), P(gx, y1)], fill=col + (120,), width=1)
            for gy in range(int(y0) + 1, int(y1)):
                d.line([P(x0, gy), P(x1, gy)], fill=col + (120,), width=1)
            d.text(((a[0] + b[0]) / 2, a[1] - 3), o["label"], fill=col, font=f_b, anchor="mb")
        elif kind == "turret":
            cx, cy = P(*o["c"])
            a = math.radians(o["facing"])
            d.line([cx, cy, cx + math.cos(a) * cs * 1.6, cy - math.sin(a) * cs * 1.6], fill=COL["danger"], width=3)
            d.rectangle([cx - cs * 0.6, cy - cs * 0.6, cx + cs * 0.6, cy + cs * 0.6], fill=(70, 30, 26), outline=COL["danger"])
        elif kind == "charge":
            cx, cy = P(*o["c"])
            pts = []
            for k in range(16):
                rr = cs * (1.3 if k % 2 == 0 else 0.6)
                a = k * math.pi / 8
                pts.append((cx + rr * math.cos(a), cy + rr * math.sin(a)))
            d.polygon(pts, fill=(255, 70, 60, 210), outline=(255, 220, 200))
            if o["label"]:
                d.text((cx, cy - cs * 1.6), o["label"], fill=COL["loud"], font=f_b, anchor="mb")
        elif kind == "lamp":
            cx, cy = P(*o["c"])
            for k in (3, 2, 1):
                rr = cs * 0.4 * k
                d.ellipse([cx - rr, cy - rr, cx + rr, cy + rr], fill=o["colour"] + (40,))
            d.ellipse([cx - 2, cy - 2, cx + 2, cy + 2], fill=o["colour"])
        elif kind == "ship":
            cx, cy = P(*o["c"])
            s = o["size"] * cs / 2
            a = math.radians(o["heading"])
            fx, fy = math.cos(a), -math.sin(a)
            rx, ry = -fy, fx
            pts = [(cx + fx * s, cy + fy * s), (cx - fx * s + rx * s * 0.55, cy - fy * s + ry * s * 0.55),
                   (cx - fx * s * 0.6, cy - fy * s * 0.6), (cx - fx * s - rx * s * 0.55, cy - fy * s - ry * s * 0.55)]
            d.polygon(pts, fill=o["colour"], outline=(20, 20, 20))
        elif kind == "slot":
            x0, y0, x1, y1 = o["r"]
            a, b = P(x0, y1), P(x1, y0)
            dash(d, [a, (b[0], a[1]), b, (a[0], b[1]), a], COL["slot"], 2, 6)
            if o["label"]:
                d.text(((a[0] + b[0]) / 2, (a[1] + b[1]) / 2), o["label"], fill=COL["slot"], font=f_s, anchor="mm")
        elif kind == "nope":
            cx, cy = P(*o["c"])
            r = cs * 1.2
            d.line([cx - r, cy - r, cx + r, cy + r], fill=COL["loud"], width=4)
            d.line([cx - r, cy + r, cx + r, cy - r], fill=COL["loud"], width=4)
            if o["text"]:
                d.text((cx + r + 4, cy), o["text"], fill=COL["loud"], font=f_s, anchor="lm")

    for rt in v.routes:
        pts = [P(*p) for p in rt["pts"]]
        col = COL[rt["kind"]]
        if rt["kind"] == "patrol":
            dash(d, pts, col, 2, 4, gap=5)
        elif rt["kind"] in ("out", "loud", "eject", "tow"):
            dash(d, pts, col, 4, 12, gap=6)
        else:
            d.line(pts, fill=col, width=4, joint="curve")
        if len(pts) > 1:
            arrow(d, pts[-2], pts[-1], col)
        if rt["label"]:
            right = pts[0][0] > W - 160
            top = pts[0][1] < 24
            d.text((pts[0][0] + (-6 if right else 6), pts[0][1] + (8 if top else -6)), rt["label"], fill=col, font=f_b,
                   anchor=("r" if right else "l") + ("t" if top else "d"))
    for kind, o in v.over:
        if kind == "label":
            cx, cy = P(*o["c"])
            d.text((cx, cy), o["text"], fill=o["colour"], font=font(F_BODY, o["size"]), anchor=o["anchor"])
        elif kind == "poi":
            cx, cy = P(*o["c"])
            d.ellipse([cx - 10, cy - 10, cx + 10, cy + 10], fill=(10, 10, 10), outline=o["colour"], width=2)
            d.text((cx, cy + 1), str(o["n"]), fill=o["colour"], font=f_b, anchor="mm")
    # scale bar: 100 m
    n = 100 / CELL_M * cs
    d.line([12, H - 14, 12 + n, H - 14], fill=COL["text"], width=2)
    d.line([12, H - 19, 12, H - 9], fill=COL["text"], width=2)
    d.line([12 + n, H - 19, 12 + n, H - 9], fill=COL["text"], width=2)
    d.text((18 + n, H - 14), "100 m", fill=COL["text"], font=f_s, anchor="lm")
    return img


def dash(d, pts, col, width, seg, gap=None):
    gap = gap or seg
    for a, b in zip(pts, pts[1:]):
        L = math.dist(a, b)
        t = 0.0
        while t < L:
            t1 = min(L, t + seg)
            p = (a[0] + (b[0] - a[0]) * t / L, a[1] + (b[1] - a[1]) * t / L)
            q = (a[0] + (b[0] - a[0]) * t1 / L, a[1] + (b[1] - a[1]) * t1 / L)
            d.line([p, q], fill=col, width=width)
            t = t1 + gap


def arrow(d, a, b, col):
    ang = math.atan2(b[1] - a[1], b[0] - a[0])
    s = 12
    p1 = (b[0] - s * math.cos(ang - 0.45), b[1] - s * math.sin(ang - 0.45))
    p2 = (b[0] - s * math.cos(ang + 0.45), b[1] - s * math.sin(ang + 0.45))
    d.polygon([b, p1, p2], fill=col)


# ====================================================================================================================
LEGEND = [("armor", "armour: only explosives cut it"), ("hull", "plate: cut slowly, loudly"), ("air", "inside: flyable"),
          ("rock", "rock"), ("wreck", "wreckage")]
LEGEND_O = [("core", "station core"), ("shutter", "blast shutter: slams on the alarm"), ("door", "door"),
            ("fan", "fan / crusher (timed)"), ("eye", "searchlight / sensor"), ("turret", "turret"),
            ("charge", "charge point"), ("slot", "growth slot"), ("ghost", "place blocks (den editor)"), ("dig", "dig (den editor)")]
ROUTE_KEY = [("in", "way in (quiet)"), ("out", "way out (quiet)"), ("loud", "way out (loud)"), ("eject", "way out (co-op)"),
             ("patrol", "patrol / response"), ("spin", "the station turns"), ("fly", "flying inside"), ("lane", "traffic lane (transponder on)"), ("home", "home run"), ("tow", "tow run")]


def render(sk, out_dir=OUT):
    os.makedirs(out_dir, exist_ok=True)
    main = sk.views[0]
    cs_main = max(8, min(12, 1200 // main.w))
    imgs = [render_view(main, cs_main)]
    for v in sk.views[1:]:
        imgs.append(render_view(v, max(6, min(10, 760 // v.w))))
    side_w = 760
    W = imgs[0].width + side_w + 72
    body_h = max(imgs[0].height + 40, sum(i.height + 40 for i in imgs[1:]) + 440)
    H = 130 + body_h + 2600
    sheet = Image.new("RGB", (W, H), (10, 11, 13))
    d = ImageDraw.Draw(sheet)
    tag = f"{sk.group} {sk.letter}" + ("  ·  recommended" if sk.recommended else "")
    d.text((24, 22), tag.upper(), fill=COL["out"], font=font(F_BOLD, 16))
    d.text((24, 46), sk.title.upper(), fill=COL["text"], font=font(F_TITLE, 34))
    d.text((24, 96), sk.line, fill=COL["dim"], font=font(F_BODY, 16))
    y = 130
    sheet.paste(imgs[0], (24, y))
    d.text((24, y + imgs[0].height + 8), main.caption, fill=COL["dim"], font=font(F_BODY, 13))
    x2 = 24 + imgs[0].width + 48
    yy = y
    for v, im in zip(sk.views[1:], imgs[1:]):
        sheet.paste(im, (x2, yy))
        d.text((x2, yy + im.height + 6), v.caption, fill=COL["dim"], font=font(F_BODY, 13))
        yy += im.height + 34
    # notes under the side views, or under the main view when there is no room
    tx, ty, tw = x2, yy + 6, side_w
    fh, fb = font(F_TITLE, 15), font(F_BODY, 14)
    for head, lines in sk.notes:
        d.text((tx, ty), head.upper(), fill=COL["out"], font=fh)
        ty += 24
        for ln in lines:
            for k, wl in enumerate(wrap(ln.replace("   ", "  ·  "), 100)):
                d.text((tx + (0 if k == 0 else 12), ty), ("· " if k == 0 else "") + wl, fill=COL["text"], font=fb)
                ty += 19
        ty += 8
    # legend along the bottom of the main view
    lx, ly = 24, y + imgs[0].height + 34
    f_s = font(F_BODY, 13)
    mats_used = {c for v in sk.views for row in v.g for c in row}
    for m, t in [(m, t) for m, t in MAT_TEXT.items() if m in mats_used]:
        d.rectangle([lx, ly, lx + 14, ly + 14], fill=MAT[m][0], outline=MAT[m][1])
        d.text((lx + 20, ly + 7), t, fill=COL["dim"], font=f_s, anchor="lm")
        lx += 26 + d.textlength(t, font=f_s) + 18
        if lx > imgs[0].width - 180:
            lx, ly = 24, ly + 24
    lx, ly = 24, ly + 26
    used = {k for v in sk.views for k, _ in v.over} | {"core"}
    for k, t in LEGEND_O:
        if k not in used:
            continue
        legend_icon(d, k, lx + 7, ly + 7)
        d.text((lx + 20, ly + 7), t, fill=COL["dim"], font=f_s, anchor="lm")
        lx += 26 + d.textlength(t, font=f_s) + 18
    lx, ly = 24, ly + 26
    kinds = []
    for v in sk.views:
        for r in v.routes:
            if r["kind"] not in kinds:
                kinds.append(r["kind"])
    for k, t in ROUTE_KEY:
        if k in kinds:
            d.line([lx, ly + 7, lx + 22, ly + 7], fill=COL[k], width=4)
            d.text((lx + 28, ly + 7), t, fill=COL["dim"], font=f_s, anchor="lm")
            lx += 34 + d.textlength(t, font=f_s) + 18
    H2 = max(ty, ly + 30, y + imgs[0].height + 120) + 20
    sheet = sheet.crop((0, 0, W, H2))
    path = os.path.join(out_dir, f"{sk.key}.png")
    sheet.save(path)
    return path


def render_grid(key, tag, title, line, panels, cols, cs, footers=(), out_dir=OUT, text_w=None):
    """A sheet of equal panels in a grid. panels: [(view, heading, [text lines], heading colour)]. footers: extra images
    (timelines) pasted full width under the grid."""
    os.makedirs(out_dir, exist_ok=True)
    ims = [render_view(v, cs) for v, _, _, _ in panels]
    pw = max(i.width for i in ims)
    ph = max(i.height for i in ims)
    fh, fb, fc = font(F_TITLE, 15), font(F_BODY, 14), font(F_BODY, 13)
    wrap_n = text_w or max(40, int(pw / 7.2))
    # text height per row
    def text_h(lines):
        return sum(19 * len(wrap(ln, wrap_n)) for ln in lines) + 8
    rows = (len(panels) + cols - 1) // cols
    row_h = []
    for r in range(rows):
        row_h.append(ph + 34 + max(text_h(p[2]) for p in panels[r * cols:(r + 1) * cols]) + 26)
    W = 24 + cols * (pw + 28) + 20
    for fim in footers:
        W = max(W, fim.width + 48)
    H = 130 + sum(row_h) + sum(fim.height + 24 for fim in footers) + 30
    sheet = Image.new("RGB", (W, H), (10, 11, 13))
    d = ImageDraw.Draw(sheet)
    d.text((24, 22), tag.upper(), fill=COL["out"], font=font(F_BOLD, 16))
    d.text((24, 46), title.upper(), fill=COL["text"], font=font(F_TITLE, 34))
    d.text((24, 96), line, fill=COL["dim"], font=font(F_BODY, 16))
    y = 130
    for r in range(rows):
        for c in range(cols):
            i = r * cols + c
            if i >= len(panels):
                break
            v, head, lines, hc = panels[i]
            x = 24 + c * (pw + 28)
            d.text((x, y), head.upper(), fill=hc or COL["out"], font=fh)
            sheet.paste(ims[i], (x, y + 24))
            ty = y + 24 + ims[i].height + 6
            if v.caption:
                d.text((x, ty), v.caption, fill=COL["dim"], font=fc)
                ty += 20
            for ln in lines:
                for k, wl in enumerate(wrap(ln, wrap_n)):
                    d.text((x + (0 if k == 0 else 12), ty), ("· " if k == 0 else "") + wl, fill=COL["text"], font=fb)
                    ty += 19
        y += row_h[r]
    for fim in footers:
        sheet.paste(fim, (24, y))
        y += fim.height + 24
    path = os.path.join(out_dir, f"{key}.png")
    sheet.save(path)
    return path


def timeline(title, span, unit, rows, width=1900, marks=None, note=""):
    """A strip chart: rows of (label, colour, [(start, end)] or [instant]) over 0..span."""
    fb, fs, fh = font(F_BODY, 14), font(F_BODY, 12), font(F_TITLE, 15)
    left, rh = 250, 26
    H = 44 + rh * len(rows) + 46
    im = Image.new("RGB", (width, H), (14, 16, 18))
    d = ImageDraw.Draw(im, "RGBA")
    d.text((12, 10), title.upper(), fill=COL["out"], font=fh)
    if note:
        d.text((12 + d.textlength(title.upper(), font=fh) + 20, 12), note, fill=COL["dim"], font=fs)
    x0, x1 = left, width - 24

    def X(t):
        return x0 + (x1 - x0) * t / span
    y = 40
    for m, lab in (marks or []):
        d.line([X(m), y, X(m), y + rh * len(rows)], fill=(70, 74, 70), width=1)
        d.text((X(m), y + rh * len(rows) + 6), lab, fill=COL["dim"], font=fs, anchor="mt")
    for label, colour, spans in rows:
        d.text((12, y + rh / 2), label, fill=COL["text"], font=fb, anchor="lm")
        d.line([x0, y + rh / 2, x1, y + rh / 2], fill=(34, 37, 36), width=1)
        for sp in spans:
            if isinstance(sp, tuple) and len(sp) == 3:
                a, b, txt = sp
            elif isinstance(sp, tuple):
                (a, b), txt = sp, ""
            else:
                a, b, txt = sp, None, ""
            if b is None:
                d.ellipse([X(a) - 4, y + rh / 2 - 4, X(a) + 4, y + rh / 2 + 4], fill=colour)
            else:
                d.rectangle([X(a), y + 5, X(b), y + rh - 5], fill=colour + (200,))
            if txt:
                tx = X(a if b is None else b) + 8
                tl = d.textlength(txt, font=fs)
                if tx + tl <= x1:
                    d.text((tx, y + rh / 2), txt, fill=colour, font=fs, anchor="lm")
                elif b is not None and X(b) - X(a) > tl + 12:
                    d.text((X(a) + 6, y + rh / 2), txt, fill=(12, 12, 12), font=fs, anchor="lm")
                else:
                    d.text((X(a) - 8, y + rh / 2), txt, fill=colour, font=fs, anchor="rm")
        y += rh
    d.text((x1, 12), unit, fill=COL["dim"], font=fs, anchor="rt")
    return im


def legend_icon(d, k, x, y):
    if k == "core":
        d.ellipse([x - 7, y - 7, x + 7, y + 7], fill=(30, 120, 140), outline=COL["core"], width=2)
    elif k == "shutter":
        d.line([x - 8, y, x + 8, y], fill=COL["shutter"], width=4)
    elif k == "door":
        d.line([x - 8, y, x + 8, y], fill=COL["door"], width=4)
    elif k == "fan":
        d.line([x - 7, y - 3, x + 7, y + 3], fill=(230, 210, 120), width=2)
        d.line([x - 2, y - 7, x + 2, y + 7], fill=(230, 210, 120), width=2)
    elif k == "eye":
        d.pieslice([x - 9, y - 9, x + 9, y + 9], -25, 25, fill=(255, 230, 160))
    elif k == "turret":
        d.rectangle([x - 5, y - 5, x + 5, y + 5], fill=(70, 30, 26), outline=COL["danger"])
    elif k == "charge":
        d.ellipse([x - 6, y - 6, x + 6, y + 6], fill=COL["loud"])
    elif k in ("ghost", "dig"):
        col = (124, 255, 154) if k == "ghost" else (255, 90, 70)
        d.rectangle([x - 7, y - 7, x + 7, y + 7], fill=tuple(c // 3 for c in col), outline=col)
    elif k == "slot":
        dash(d, [(x - 7, y - 7), (x + 7, y - 7), (x + 7, y + 7), (x - 7, y + 7), (x - 7, y - 7)], COL["slot"], 2, 4, 3)


def overview(paths, out):
    ims = [Image.open(p) for p in paths]
    w = 1100
    th = [int(im.height * w / im.width) for im in ims]
    cols = 2
    rows = (len(ims) + 1) // 2
    rh = [max(th[r * cols:(r + 1) * cols]) for r in range(rows)]
    sheet = Image.new("RGB", (cols * w + 3 * 20, sum(rh) + (rows + 1) * 20), (4, 5, 6))
    y = 20
    for r in range(rows):
        for c in range(cols):
            i = r * cols + c
            if i < len(ims):
                sheet.paste(ims[i].resize((w, th[i]), Image.LANCZOS), (20 + c * (w + 20), y))
        y += rh[r] + 20
    sheet.save(out)


if __name__ == "__main__":
    sys.path.insert(0, HERE)
    import sketches
    keys = sys.argv[1:]
    groups = {}
    for fn in sketches.ALL:
        sk = fn()
        if keys and sk.key not in keys:
            continue
        p = render(sk)
        groups.setdefault(sk.group, []).append(p)
        print("SKETCH", p, flush=True)
    for g, ps in groups.items():
        if not keys and len(ps) > 1:
            out = os.path.join(OUT, f"overview_{g.split()[0].lower()}.png")
            overview(ps, out)
            print("OVERVIEW", out)
