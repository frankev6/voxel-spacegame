"""Shared geometry, NOCTURNE colours and drawing primitives for the racing design sheets (RACE-002, RACE-003)."""
import math
import os
import random
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
FONTS = os.path.join(ROOT, "Assets", "Game", "UI", "Theme", "Fonts")
F_TITLE = os.path.join(FONTS, "Michroma", "FNT_Michroma-Regular.ttf")
F_BODY = os.path.join(FONTS, "Inter", "FNT_Inter-Regular.ttf")
F_BOLD = os.path.join(FONTS, "Inter", "FNT_Inter-SemiBold.ttf")
F_MONO = os.path.join(FONTS, "JetBrainsMono", "FNT_JetBrainsMono-Regular.ttf")
SS = 2  # supersampling factor

# NOCTURNE colours (DESIGN-GUIDE)
BG = (2, 3, 3)
PANEL = (13, 14, 12)
PANEL_LINE = (52, 54, 47)
TRACK = (27, 29, 25)
TRACK_EDGE = (52, 54, 47)
INTERIOR = (16, 13, 11)
CENTRE = (88, 92, 82)
FIELD = (159, 224, 200)
VOXEL = (163, 164, 156)
HULL_DARK = (74, 76, 70)
BOOST = (255, 179, 71)
POWER = (176, 138, 46)
SLOW = (110, 59, 255)
MINE = (255, 36, 16)
EMP = (191, 214, 255)
REPULSE = (159, 224, 200)
GRAVITY = (168, 204, 255)
ROCK = (85, 83, 76)
BARRICADE = (178, 58, 42)
GATE = (255, 241, 214)
BRANCH = (124, 255, 154)
WASH = (191, 214, 255)
FLARE = (255, 106, 42)
TURRET = (255, 85, 48)
TEXT = (226, 228, 220)
DIM = (150, 154, 144)


def font(path, size):
    return ImageFont.truetype(path, int(size * SS))


# ---------------------------------------------------------------- geometry
def catmull(points, closed, step=4.0):
    """Centripetal Catmull-Rom through points, sampled about every `step` metres."""
    pts = list(points)
    n = len(pts)
    if closed:
        ctrl = [pts[-1]] + pts + [pts[0], pts[1]]
        segs = n
    else:
        ctrl = [(2 * pts[0][0] - pts[1][0], 2 * pts[0][1] - pts[1][1])] + pts + \
               [(2 * pts[-1][0] - pts[-2][0], 2 * pts[-1][1] - pts[-2][1])]
        segs = n - 1
    out = []
    for i in range(segs):
        p0, p1, p2, p3 = ctrl[i], ctrl[i + 1], ctrl[i + 2], ctrl[i + 3]

        def tj(ti, a, b):
            return ti + max(1e-6, math.hypot(b[0] - a[0], b[1] - a[1])) ** 0.5
        t0 = 0.0; t1 = tj(t0, p0, p1); t2 = tj(t1, p1, p2); t3 = tj(t2, p2, p3)
        k = max(2, int(math.dist(p1, p2) / step))
        for j in range(k):
            t = t1 + (t2 - t1) * j / k

            def lerp(a, b, ta, tb):
                if tb - ta < 1e-9:
                    return a
                return ((tb - t) / (tb - ta) * a[0] + (t - ta) / (tb - ta) * b[0],
                        (tb - t) / (tb - ta) * a[1] + (t - ta) / (tb - ta) * b[1])
            a1 = lerp(p0, p1, t0, t1); a2 = lerp(p1, p2, t1, t2); a3 = lerp(p2, p3, t2, t3)
            b1 = lerp(a1, a2, t0, t2); b2 = lerp(a2, a3, t1, t3)
            out.append(lerp(b1, b2, t1, t2))
    if not closed:
        out.append(pts[-1])
    return out


def cumlen(poly, closed):
    s = [0.0]
    for i in range(1, len(poly)):
        s.append(s[-1] + math.dist(poly[i - 1], poly[i]))
    return s, s[-1] + (math.dist(poly[-1], poly[0]) if closed else 0)


def make_path(points, closed=False, **kw):
    poly = catmull(points, closed)
    s, total = cumlen(poly, closed)
    return dict(kw, points=points, closed=closed, poly=poly, s=s, len=total)


def at(path, f):
    """Point and unit tangent at arc fraction f of a path."""
    poly, s, total = path["poly"], path["s"], path["len"]
    d = (f % 1.0 if path["closed"] else max(0.0, min(1.0, f))) * total
    lo, hi = 0, len(s) - 1
    while lo < hi:
        mid = (lo + hi + 1) // 2
        if s[mid] <= d:
            lo = mid
        else:
            hi = mid - 1
    i = lo
    a = poly[i]; b = poly[(i + 1) % len(poly)]
    seg = max(1e-6, math.dist(a, b)); u = max(0.0, min(1.0, (d - s[i]) / seg))
    return (a[0] + (b[0] - a[0]) * u, a[1] + (b[1] - a[1]) * u), ((b[0] - a[0]) / seg, (b[1] - a[1]) / seg)


def sub_poly(path, f0, f1):
    d0, d1 = f0 * path["len"], f1 * path["len"]
    return [q for i, q in enumerate(path["poly"]) if d0 <= path["s"][i] <= d1]


def offset(poly, closed, dist):
    out, n = [], len(poly)
    for i in range(n):
        a = poly[i - 1] if (i > 0 or closed) else poly[i]
        b = poly[(i + 1) % n] if (i < n - 1 or closed) else poly[i]
        tx, ty = b[0] - a[0], b[1] - a[1]
        L = max(1e-6, math.hypot(tx, ty))
        out.append((poly[i][0] - ty / L * dist, poly[i][1] + tx / L * dist))
    return out


def dist_to_poly(p, poly):
    best = 1e18
    for i in range(len(poly) - 1):
        a, b = poly[i], poly[i + 1]
        abx, aby = b[0] - a[0], b[1] - a[1]; L2 = abx * abx + aby * aby
        u = 0 if L2 == 0 else max(0, min(1, ((p[0] - a[0]) * abx + (p[1] - a[1]) * aby) / L2))
        best = min(best, math.hypot(p[0] - a[0] - abx * u, p[1] - a[1] - aby * u))
    return best


def curvature_radius(poly, closed, window=6):
    n = len(poly); r = []
    for i in range(n):
        ia, ib = i - window, i + window
        if not closed:
            ia, ib = max(0, ia), min(n - 1, ib)
        a, b, c = poly[ia % n], poly[i], poly[ib % n]
        area2 = abs((b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0]))
        r.append(1e9 if area2 < 1e-6 else math.dist(a, b) * math.dist(b, c) * math.dist(c, a) / (2 * area2))
    return r


def rot(p, deg, c=(0, 0)):
    a = math.radians(deg); x, y = p[0] - c[0], p[1] - c[1]
    return (c[0] + x * math.cos(a) - y * math.sin(a), c[1] + x * math.sin(a) + y * math.cos(a))


def point_in_poly(p, poly):
    x, y, inside = p[0], p[1], False
    for i in range(len(poly)):
        a, b = poly[i], poly[i - 1]
        if (a[1] > y) != (b[1] > y) and x < (b[0] - a[0]) * (y - a[1]) / (b[1] - a[1] + 1e-12) + a[0]:
            inside = not inside
    return inside


# ---------------------------------------------------------------- view
class View:
    """Maps world metres (+x east, +y north) into an image rectangle (pixels, supersampled)."""
    def __init__(self, img, rect, bounds):
        self.img = img
        self.d = ImageDraw.Draw(img, "RGBA")
        x0, y0, x1, y1 = rect
        bx0, by0, bx1, by1 = bounds
        self.k = min((x1 - x0) / (bx1 - bx0), (y1 - y0) / (by1 - by0))
        self.ox = x0 + ((x1 - x0) - (bx1 - bx0) * self.k) / 2 - bx0 * self.k
        self.oy = y0 + ((y1 - y0) - (by1 - by0) * self.k) / 2 + by1 * self.k

    def P(self, p):
        return (self.ox + p[0] * self.k, self.oy - p[1] * self.k)

    def m(self, metres):
        return metres * self.k

    def px(self, n):
        return n * SS

    def line(self, pts, color, width, closed=False, dash=None):
        xy = [self.P(p) for p in pts]
        if closed and xy:
            xy.append(xy[0])
        if len(xy) < 2:
            return
        if not dash:
            self.d.line(xy, fill=color, width=max(1, int(width)), joint="curve")
            return
        on, off = dash
        acc, draw_on = 0.0, True
        for i in range(len(xy) - 1):
            a, b = xy[i], xy[i + 1]
            if draw_on:
                self.d.line([a, b], fill=color, width=max(1, int(width)))
            acc += math.dist(a, b)
            if acc > (on if draw_on else off):
                acc, draw_on = 0.0, not draw_on

    def circle(self, c, r_m, fill=None, outline=None, width=1):
        x, y = self.P(c); r = self.m(r_m)
        self.d.ellipse((x - r, y - r, x + r, y + r), fill=fill, outline=outline, width=max(1, int(width)))

    def dot(self, c, r_px, fill):
        x, y = self.P(c); r = r_px * SS
        self.d.ellipse((x - r, y - r, x + r, y + r), fill=fill)

    def poly(self, pts, fill=None, outline=None, width=1):
        self.d.polygon([self.P(p) for p in pts], fill=fill, outline=outline, width=max(1, int(width)))

    def text(self, p, s, fnt, fill, anchor="mm"):
        self.d.text(self.P(p), s, font=fnt, fill=fill, anchor=anchor)

    def arrow(self, a, b, color, width, head=None):
        head = head if head is not None else self.px(10)
        A, B = self.P(a), self.P(b)
        self.d.line([A, B], fill=color, width=int(width))
        ang = math.atan2(B[1] - A[1], B[0] - A[0])
        for s in (-1, 1):
            self.d.line([B, (B[0] - head * math.cos(ang + s * 0.5), B[1] - head * math.sin(ang + s * 0.5))],
                        fill=color, width=int(width))


def starfield(img, rect, seed, density=1.0):
    rng = random.Random(seed)
    x0, y0, x1, y1 = rect
    glow = Image.new("RGB", img.size, BG)
    gd = ImageDraw.Draw(glow)
    for _ in range(6):
        cx, cy = rng.uniform(x0, x1), rng.uniform(y0, y1)
        rr = rng.uniform(300, 800) * SS
        col = rng.choice([(42, 58, 48), (74, 36, 104), (34, 40, 94), (74, 58, 34), (60, 30, 22)])
        gd.ellipse((cx - rr, cy - rr, cx + rr, cy + rr), fill=tuple(int(c * 0.55) for c in col))
    glow = glow.filter(ImageFilter.GaussianBlur(240 * SS))
    img.paste(glow.crop(rect), rect[:2])
    d = ImageDraw.Draw(img, "RGBA")
    for _ in range(int(1600 * density)):
        x, y = rng.uniform(x0, x1), rng.uniform(y0, y1)
        b = int(rng.uniform(60, 210)); s = rng.choice([1, 1, 1, 2]) * SS
        d.ellipse((x, y, x + s, y + s), fill=(b, b, min(255, b + 20), 255))


def wrap(text, width):
    words, lines, cur = text.split(), [], ""
    for w in words:
        if len(cur) + len(w) + 1 > width and cur:
            lines.append(cur); cur = w
        else:
            cur = (cur + " " + w).strip()
    if cur:
        lines.append(cur)
    return lines


# ---------------------------------------------------------------- set-piece primitives (world metres)
def structure(v, pts, seed=1, plate=26, label=None, fnt=None, base=HULL_DARK):
    """A voxel structure seen from above: hull fill, plate mosaic, bright rim."""
    rng = random.Random(seed)
    v.poly(pts, fill=base + (255,))
    xs, ys = [p[0] for p in pts], [p[1] for p in pts]
    tones = [(163, 164, 156), (174, 173, 164), (148, 150, 142), (166, 158, 134), (138, 142, 126)]
    y = min(ys)
    while y < max(ys):
        x = min(xs)
        while x < max(xs):
            c = (x + plate / 2, y + plate / 2)
            if point_in_poly(c, pts) and rng.random() < 0.82:
                t = rng.choice(tones); k = rng.uniform(0.45, 0.7)
                col = tuple(int(ch * k) for ch in t)
                w = plate * rng.choice([1, 1, 2]) - 3
                v.poly([(x + 1.5, y + 1.5), (x + w, y + 1.5), (x + w, y + plate - 1.5), (x + 1.5, y + plate - 1.5)], fill=col + (255,))
            x += plate
        y += plate
    v.line(pts, VOXEL + (255,), v.px(2.5), closed=True)
    if label and fnt:
        cx, cy = sum(xs) / len(xs), sum(ys) / len(ys)
        v.text((cx, cy), label, fnt, TEXT + (230,))


def rock(v, c, r, rng, fill=ROCK):
    pts = [(c[0] + math.cos(t) * r * rng.uniform(0.7, 1.1), c[1] + math.sin(t) * r * rng.uniform(0.7, 1.1))
           for t in [i * math.tau / 8 for i in range(8)]]
    v.poly(pts, fill=fill + (255,), outline=(124, 120, 108, 255))


def rock_field(v, c, rad, n, seed, smin=8, smax=20):
    rng = random.Random(seed)
    for _ in range(n):
        a, rr = rng.uniform(0, math.tau), rad * math.sqrt(rng.random())
        rock(v, (c[0] + math.cos(a) * rr, c[1] + math.sin(a) * rr), rng.uniform(smin, smax), rng)


def gravity_well(v, c, body_r, infl_r, seed=3):
    for i in range(10, 0, -1):
        rr = body_r + (infl_r - body_r) * i / 10
        v.circle(c, rr, fill=GRAVITY + (int(5 + (10 - i) * 2.2),))
    for i in range(1, 4):
        v.circle(c, body_r + (infl_r - body_r) * i / 3.2, outline=GRAVITY + (70,), width=v.px(1))
    for k in range(12):
        a = k * math.tau / 12 + 0.13
        p0 = (c[0] + math.cos(a) * infl_r * 0.93, c[1] + math.sin(a) * infl_r * 0.93)
        p1 = (c[0] + math.cos(a) * infl_r * 0.78, c[1] + math.sin(a) * infl_r * 0.78)
        v.arrow(p0, p1, GRAVITY + (140,), v.px(2), head=v.px(7))
    # the body: cratered rock (its surface is a voxel wall you can crash into)
    rng = random.Random(seed)
    v.circle(c, body_r, fill=(58, 58, 54, 255), outline=(124, 120, 108, 255), width=v.px(2))
    for _ in range(int(body_r / 8)):
        a, rr = rng.uniform(0, math.tau), body_r * math.sqrt(rng.random()) * 0.9
        p = (c[0] + math.cos(a) * rr, c[1] + math.sin(a) * rr)
        cr = rng.uniform(6, body_r / 6)
        v.circle(p, cr, fill=(44, 44, 41, 255), outline=(82, 80, 74, 255), width=v.px(1))
    v.circle((c[0] - body_r * 0.25, c[1] + body_r * 0.3), body_r * 0.55, fill=(255, 255, 255, 10))


def sweeper(v, hub, length, arms, phase_deg, hub_r=60, label_font=None):
    v.circle(hub, length, outline=MINE + (110,), width=v.px(2))
    v.circle(hub, length, fill=MINE + (14,))
    for k in range(arms):
        a = phase_deg + k * 360 / arms
        tip = rot((hub[0] + length, hub[1]), a, hub)
        nrm = rot((hub[0] + length, hub[1] + 14), a, hub)
        nrm2 = rot((hub[0] + length, hub[1] - 14), a, hub)
        base1 = rot((hub[0], hub[1] + 22), a, hub); base2 = rot((hub[0], hub[1] - 22), a, hub)
        v.poly([base1, nrm, tip, nrm2, base2], fill=VOXEL + (255,), outline=(87, 90, 82, 255))
        # motion arc behind the arm
        trail = [rot((hub[0] + length * 0.92, hub[1]), a - t, hub) for t in range(0, 40, 4)]
        v.line(trail, VOXEL + (90,), v.px(5))
    structure(v, [rot((hub[0] + hub_r * math.cos(t), hub[1] + hub_r * math.sin(t)), 0) for t in
                  [i * math.tau / 10 for i in range(10)]], seed=int(hub[0]) & 255, plate=18)
    # rotation arrow
    arc = [rot((hub[0] + length + 40, hub[1]), phase_deg + 20 + t, hub) for t in range(0, 50, 5)]
    v.line(arc, TEXT + (200,), v.px(2))
    v.arrow(arc[-2], arc[-1], TEXT + (200,), v.px(2))


def blast_door(v, path, f, switch_pos, open_=False):
    p, t = at(path, f)
    nx, ny = -t[1], t[0]; hw = path["width"] / 2 + 6
    a = (p[0] + nx * hw, p[1] + ny * hw); c = (p[0] - nx * hw, p[1] - ny * hw)
    mid1 = (p[0] + nx * 4, p[1] + ny * 4); mid2 = (p[0] - nx * 4, p[1] - ny * 4)
    v.line([a, mid1], (232, 174, 74, 255), v.m(10))
    v.line([mid2, c], (232, 174, 74, 255), v.m(10))
    for q in (a, c):
        v.dot(q, 5, (232, 174, 74, 255))
    # hazard stripes
    for j in range(8):
        u = (j + 0.5) / 8; q = (a[0] + (c[0] - a[0]) * u, a[1] + (c[1] - a[1]) * u)
        x, y = v.P(q)
        v.d.line([(x - v.m(3), y - v.m(4)), (x + v.m(3), y + v.m(4))], fill=(13, 14, 12, 255), width=v.px(2))
    if switch_pos:
        v.line([p, switch_pos], (232, 174, 74, 120), v.px(2), dash=(v.px(6), v.px(6)))
        x, y = v.P(switch_pos); s = v.px(9)
        v.d.rectangle((x - s, y - s, x + s, y + s), fill=(232, 174, 74, 255), outline=(13, 14, 12, 255), width=v.px(2))
        v.d.ellipse((x - s / 2, y - s / 2, x + s / 2, y + s / 2), fill=(13, 14, 12, 255))


def engine_wash(v, origin, dir_deg, length, width, engine=True):
    d = (math.cos(math.radians(dir_deg)), math.sin(math.radians(dir_deg)))
    n = (-d[1], d[0])
    for i in range(8, 0, -1):
        L = length * i / 8; W = width * (0.35 + 0.65 * i / 8)
        a = (origin[0] + n[0] * width * 0.18, origin[1] + n[1] * width * 0.18)
        b = (origin[0] - n[0] * width * 0.18, origin[1] - n[1] * width * 0.18)
        e1 = (origin[0] + d[0] * L + n[0] * W / 2, origin[1] + d[1] * L + n[1] * W / 2)
        e2 = (origin[0] + d[0] * L - n[0] * W / 2, origin[1] + d[1] * L - n[1] * W / 2)
        v.poly([a, e1, e2, b], fill=WASH + (int(14 + 3 * (8 - i)),))
    for k in (-1, 0, 1):
        s = (origin[0] + n[0] * width * 0.12 * k + d[0] * 20, origin[1] + n[1] * width * 0.12 * k + d[1] * 20)
        e = (s[0] + d[0] * length * 0.75, s[1] + d[1] * length * 0.75)
        v.line([s, e], WASH + (120,), v.px(2), dash=(v.px(10), v.px(10)))
    if engine:
        bw = width * 0.22
        box = [(origin[0] - d[0] * 40 + n[0] * bw, origin[1] - d[1] * 40 + n[1] * bw),
               (origin[0] + n[0] * bw, origin[1] + n[1] * bw), (origin[0] - n[0] * bw, origin[1] - n[1] * bw),
               (origin[0] - d[0] * 40 - n[0] * bw, origin[1] - d[1] * 40 - n[1] * bw)]
        v.poly(box, fill=(78, 82, 77, 255), outline=VOXEL + (255,), width=v.px(2))
        v.line([(origin[0] + n[0] * bw * 0.8, origin[1] + n[1] * bw * 0.8), (origin[0] - n[0] * bw * 0.8, origin[1] - n[1] * bw * 0.8)],
               WASH + (255,), v.px(4))


def grapple_pylon(v, c, reach):
    v.circle(c, reach, outline=BRANCH + (110,), width=v.px(2))
    arc = [rot((c[0] + reach, c[1]), t, c) for t in range(200, 330, 5)]
    v.line(arc, BRANCH + (220,), v.px(3), dash=(v.px(8), v.px(6)))
    v.arrow(arc[-2], arc[-1], BRANCH + (220,), v.px(3))
    s = v.m(14)
    x, y = v.P(c)
    v.d.polygon([(x, y - s), (x + s, y), (x, y + s), (x - s, y)], fill=VOXEL + (255,), outline=BRANCH + (255,))
    v.d.ellipse((x - s / 3, y - s / 3, x + s / 3, y + s / 3), fill=BRANCH + (255,))


def mine(v, c, kind="prox", scale=1.0):
    x, y = v.P(c); r = max(v.px(3), v.m(6 * scale))
    if kind == "prox":
        v.d.ellipse((x - r * 2.8, y - r * 2.8, x + r * 2.8, y + r * 2.8), outline=MINE + (80,), width=v.px(1))
        v.d.ellipse((x - r, y - r, x + r, y + r), fill=MINE + (255,))
    elif kind == "repulse":
        v.d.ellipse((x - r * 1.2, y - r * 1.2, x + r * 1.2, y + r * 1.2), outline=REPULSE + (255,), width=v.px(2))
        for k in range(6):
            a = k * math.tau / 6
            v.d.line([(x + math.cos(a) * r * 1.6, y + math.sin(a) * r * 1.6), (x + math.cos(a) * r * 2.8, y + math.sin(a) * r * 2.8)],
                     fill=REPULSE + (200,), width=v.px(2))
    elif kind == "emp":
        v.d.rectangle((x - r, y - r, x + r, y + r), fill=(20, 30, 50, 255), outline=EMP + (255,), width=v.px(2))
        v.d.line([(x - r * 0.4, y - r * 0.8), (x + r * 0.2, y - r * 0.05), (x - r * 0.2, y + r * 0.05), (x + r * 0.4, y + r * 0.8)],
                 fill=EMP + (255,), width=v.px(2))
        v.d.ellipse((x - r * 3.2, y - r * 3.2, x + r * 3.2, y + r * 3.2), outline=EMP + (60,), width=v.px(1))
    elif kind == "seeker":
        v.d.polygon([(x, y - r * 1.4), (x + r * 1.2, y + r), (x - r * 1.2, y + r)], fill=MINE + (255,))
        v.d.ellipse((x - r * 3.5, y - r * 3.5, x + r * 3.5, y + r * 3.5), outline=MINE + (50,), width=v.px(1))
    elif kind == "gravity":
        v.d.ellipse((x - r, y - r, x + r, y + r), fill=(30, 20, 60, 255), outline=GRAVITY + (255,), width=v.px(2))
        for k in range(1, 3):
            v.d.ellipse((x - r * (1 + k), y - r * (1 + k), x + r * (1 + k), y + r * (1 + k)), outline=GRAVITY + (90 - 30 * k,), width=v.px(1))


def mine_field(v, c, rad, n, kind, seed):
    rng = random.Random(seed)
    for _ in range(n):
        a, rr = rng.uniform(0, math.tau), rad * math.sqrt(rng.random())
        mine(v, (c[0] + math.cos(a) * rr, c[1] + math.sin(a) * rr), kind)


def turret(v, c, facing_deg, reach, arc_deg):
    x, y = v.P(c); R = v.m(reach)
    v.d.pieslice((x - R, y - R, x + R, y + R), -facing_deg - arc_deg / 2, -facing_deg + arc_deg / 2, fill=TURRET + (28,),
                 outline=TURRET + (90,))
    s = v.m(12)
    v.d.rectangle((x - s, y - s, x + s, y + s), fill=(122, 46, 36, 255), outline=TURRET + (255,), width=v.px(2))
    tip = rot((c[0] + 34, c[1]), facing_deg, c)
    v.line([c, tip], TURRET + (255,), v.px(4))


def portal(v, c, facing_deg, width, label=None, fnt=None):
    n = rot((0, width / 2), facing_deg)
    a, b = (c[0] + n[0], c[1] + n[1]), (c[0] - n[0], c[1] - n[1])
    for w, al in ((22, 50), (12, 120), (5, 255)):
        v.line([a, b], GRAVITY + (al,), v.px(w))
    for q in (a, b):
        v.dot(q, 7, GRAVITY + (255,))
    if label and fnt:
        off = rot((44, 0), facing_deg)
        v.text((c[0] - off[0] * 1.5, c[1] - off[1] * 1.5), label, fnt, GRAVITY + (255,))


def canister(v, c, scale=1.0):
    x, y = v.P(c); r = max(v.px(4), v.m(7 * scale))
    pts = [(x + math.cos(a) * r, y + math.sin(a) * r) for a in [k * math.tau / 6 for k in range(6)]]
    v.d.polygon(pts, fill=POWER + (255,), outline=BOOST + (255,))
    v.d.ellipse((x - r * 2, y - r * 2, x + r * 2, y + r * 2), outline=BOOST + (60,), width=v.px(1))


def flare_zone(v, region, sun_dir_deg, shadows=()):
    v.poly(region, fill=FLARE + (26,))
    xs, ys = [p[0] for p in region], [p[1] for p in region]
    d = (math.cos(math.radians(sun_dir_deg)), math.sin(math.radians(sun_dir_deg)))
    n = (-d[1], d[0])
    cx, cy = (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2
    span = max(max(xs) - min(xs), max(ys) - min(ys))
    for k in range(-14, 15):
        o = (cx + n[0] * k * span / 26, cy + n[1] * k * span / 26)
        a = (o[0] - d[0] * span, o[1] - d[1] * span); b = (o[0] + d[0] * span, o[1] + d[1] * span)
        seg = clip_segment_to_poly(a, b, region)
        for s0, s1 in seg:
            v.line([s0, s1], FLARE + (40,), v.px(2))
    for sh in shadows:
        v.poly(sh, fill=(0, 0, 0, 120))
        v.line(sh, (40, 40, 40, 200), v.px(1), closed=True, dash=(v.px(5), v.px(5)))
    v.line(region, FLARE + (150,), v.px(2), closed=True, dash=(v.px(12), v.px(8)))


def clip_segment_to_poly(a, b, poly, steps=160):
    out, cur = [], None
    for i in range(steps + 1):
        u = i / steps; p = (a[0] + (b[0] - a[0]) * u, a[1] + (b[1] - a[1]) * u)
        if point_in_poly(p, poly):
            cur = (cur[0], p) if cur else (p, p)
        elif cur:
            out.append(cur); cur = None
    if cur:
        out.append(cur)
    return out


def rail(v, pts, rocks_at, seed, color=ROCK):
    v.line(pts, (124, 120, 108, 150), v.px(3), dash=(v.px(14), v.px(10)))
    path = make_path(pts, False)
    rng = random.Random(seed)
    for f in rocks_at:
        p, t = at(path, f)
        rock(v, p, rng.uniform(16, 26), rng, fill=color)
        v.arrow((p[0] + t[0] * 34, p[1] + t[1] * 34), (p[0] + t[0] * 80, p[1] + t[1] * 80), TEXT + (170,), v.px(2), head=v.px(7))


def orbit(v, c, r, n_rocks, seed, direction=1):
    pts = [(c[0] + math.cos(t) * r, c[1] + math.sin(t) * r) for t in [i * math.tau / 180 for i in range(181)]]
    v.line(pts, (124, 120, 108, 110), v.px(2), dash=(v.px(10), v.px(10)))
    rng = random.Random(seed)
    for k in range(n_rocks):
        a = k * math.tau / n_rocks + rng.uniform(-0.15, 0.15)
        p = (c[0] + math.cos(a) * (r + rng.uniform(-14, 14)), c[1] + math.sin(a) * (r + rng.uniform(-14, 14)))
        rock(v, p, rng.uniform(9, 18), rng)
        if k % 3 == 0:
            t = (-math.sin(a) * direction, math.cos(a) * direction)
            v.arrow((p[0] + t[0] * 26, p[1] + t[1] * 26), (p[0] + t[0] * 62, p[1] + t[1] * 62), TEXT + (150,), v.px(2), head=v.px(6))


def callout(v, p, n, fnt):
    x, y = v.P(p); r = v.px(15)
    v.d.ellipse((x - r, y - r, x + r, y + r), fill=(13, 14, 12, 235), outline=TEXT + (255,), width=v.px(2))
    v.d.text((x, y + v.px(1)), str(n), font=fnt, fill=TEXT + (255,), anchor="mm")


def reactor_pulse(v, c, r, shadows=()):
    """A dying reactor that pulses radiation outward; shadow polygons (behind wreckage) are safe."""
    for i in range(7, 0, -1):
        v.circle(c, r * i / 7, fill=FLARE + (int(10 + (7 - i) * 3),))
    for i in (0.45, 0.7, 0.95):
        v.circle(c, r * i, outline=FLARE + (150,), width=v.px(2))
    for sh in shadows:
        v.poly(sh, fill=(0, 0, 0, 150))
        v.line(sh, (60, 60, 60, 220), v.px(1), closed=True, dash=(v.px(5), v.px(5)))
    x, y = v.P(c); rr = v.px(11)
    v.d.ellipse((x - rr, y - rr, x + rr, y + rr), fill=(255, 220, 160, 255), outline=FLARE + (255,), width=v.px(3))


def wreck(a, b, s0, s1, width_fn, jag0=0.0, jag1=0.0, seed=1, step=0.02):
    """Hull outline along axis a→b from fraction s0 to s1, half-width width_fn(s); jagged (broken) ends where jag > 0 metres."""
    import random as _r
    rng = _r.Random(seed)
    L = math.dist(a, b); u = ((b[0] - a[0]) / L, (b[1] - a[1]) / L); n = (-u[1], u[0])
    def P(s, w): return (a[0] + u[0] * s * L + n[0] * w, a[1] + u[1] * s * L + n[1] * w)
    k = max(2, int((s1 - s0) / step))
    ss = [s0 + (s1 - s0) * i / k for i in range(k + 1)]
    left = [P(s, width_fn(s)) for s in ss]
    right = [P(s, -width_fn(s)) for s in reversed(ss)]
    def end(s, jag, w0, w1):
        if jag <= 0:
            return []
        pts, m = [], 7
        for i in range(1, m):
            w = w0 + (w1 - w0) * i / m
            pts.append(P(s + (rng.uniform(-1, 1) * jag) / L, w))
        return pts
    return left + end(s1, jag1, width_fn(s1), -width_fn(s1)) + right + end(s0, jag0, -width_fn(s0), width_fn(s0))


# ---------------------------------------------------------------- dead warships (battle graveyard)
WRECK = dict(base=(78, 76, 70), plate=(112, 108, 98), deck=(52, 52, 48), frame=(34, 34, 31), rust=(116, 72, 44),
             scorch=(24, 22, 20), ember=(255, 106, 42), dead=(60, 64, 66))


def ship_wreck(v, a, b, kind, seed=1, cut=None, width=None):
    """Draw a dead capital ship from axis a (stern) to b (bow) seen from above, in layered parts.
    kind: dreadnought, carrier, battleship, cruiser or frigate. cut = (s0, s1) keeps only that part of the hull,
    with torn edges. Returns the hull outline (world points) so tracks and interiors can be tested against it."""
    import random as _r
    rng = _r.Random(seed)
    L = math.dist(a, b); u = ((b[0] - a[0]) / L, (b[1] - a[1]) / L); n = (-u[1], u[0])
    W = width or {"dreadnought": 185, "carrier": 125, "battleship": 110, "cruiser": 70, "frigate": 34}[kind]

    def P(s, w):
        return (a[0] + u[0] * s * L + n[0] * w, a[1] + u[1] * s * L + n[1] * w)

    def half_width(s):
        if kind == "carrier":
            return W * (0.78 if s < 0.04 or s > 0.96 else 1.0)
        prow = {"dreadnought": 0.84, "battleship": 0.86, "cruiser": 0.8, "frigate": 0.72}[kind]
        w = W
        if s > prow:
            w = W * max(0.12, 1 - (s - prow) / (1 - prow) * 0.9)
        if s < 0.06:
            w = W * 0.82
        if kind == "dreadnought" and 0.30 < s < 0.42:
            w = W * 1.25           # sponsons under the bridge
        return w

    s0, s1 = cut or (0.0, 1.0)
    k = max(8, int((s1 - s0) * 90))
    ss = [s0 + (s1 - s0) * i / k for i in range(k + 1)]
    left = [P(s, half_width(s)) for s in ss]
    right = [P(s, -half_width(s)) for s in reversed(ss)]

    def torn(s, w0, w1):
        return [P(s + rng.uniform(-1, 1) * 40 / L, w0 + (w1 - w0) * i / 8) for i in range(1, 8)]
    outline = left + (torn(s1, half_width(s1), -half_width(s1)) if s1 < 1 else []) + right + \
              (torn(s0, -half_width(s0), half_width(s0)) if s0 > 0 else [])
    inside = lambda s: s0 <= s <= s1
    v.poly(outline, fill=WRECK["base"] + (255,), outline=WRECK["frame"] + (255,), width=v.px(2))

    # plates in rows along the hull, some missing (scorched) and some rusted
    rows = 3 if kind in ("frigate",) else 5
    for r in range(rows):
        w0 = -1 + 2 * r / rows; w1 = -1 + 2 * (r + 1) / rows
        s = s0
        while s < s1:
            ds = rng.uniform(0.025, 0.06) * (2 if kind == "frigate" else 1)
            e = min(s1, s + ds)
            hw = half_width((s + e) / 2) * 0.9
            col = WRECK["plate"] if rng.random() < 0.7 else (WRECK["rust"] if rng.random() < 0.5 else WRECK["scorch"])
            v.poly([P(s + 0.003, hw * (w0 + 0.04)), P(e - 0.003, hw * (w0 + 0.04)), P(e - 0.003, hw * (w1 - 0.04)), P(s + 0.003, hw * (w1 - 0.04))],
                   fill=col + (255,))
            s = e
    # centre spine and frames
    v.line([P(max(s0, 0.04), 0), P(min(s1, 0.95), 0)], WRECK["frame"] + (255,), v.m(W * 0.10))
    for s in [x / 20 for x in range(1, 20)]:
        if inside(s):
            v.line([P(s, half_width(s) * 0.95), P(s, -half_width(s) * 0.95)], WRECK["frame"] + (200,), v.px(1))

    if kind == "carrier":   # flight deck down the middle, island to one side
        v.poly([P(max(s0, 0.02), W * 0.42), P(min(s1, 0.98), W * 0.42), P(min(s1, 0.98), -W * 0.42), P(max(s0, 0.02), -W * 0.42)], fill=WRECK["deck"] + (255,))
        for s in [x / 40 for x in range(1, 40)]:
            if inside(s):
                v.line([P(s, 0), P(s + 0.012, 0)], (210, 190, 120, 200), v.px(2))
        for s in (0.15, 0.85):
            if inside(s):
                v.line([P(s, W * 0.38), P(s, -W * 0.38)], (210, 190, 120, 160), v.px(2))
        if inside(0.62):
            v.poly([P(0.55, -W * 1.0), P(0.70, -W * 1.0), P(0.70, -W * 1.45), P(0.55, -W * 1.45)], fill=WRECK["plate"] + (255,), outline=WRECK["frame"] + (255,))
    # turrets: barbettes on the centreline (capital ships) or in pairs (cruiser, frigate)
    turrets = {"dreadnought": [0.22, 0.48, 0.58, 0.70, 0.78], "battleship": [0.28, 0.5, 0.72], "cruiser": [0.6, 0.75], "frigate": [0.65],
               "carrier": []}[kind]
    for s in turrets:
        if not inside(s):
            continue
        c = P(s, 0); r = W * (0.32 if kind != "frigate" else 0.4)
        v.circle(c, r, fill=WRECK["dead"] + (255,), outline=WRECK["frame"] + (255,), width=v.px(2))
        ang = math.atan2(u[1], u[0]) + rng.uniform(-1.2, 1.2)     # guns frozen where they last pointed
        for off in (-0.25, 0, 0.25):
            p0 = (c[0] - math.sin(ang) * r * off, c[1] + math.cos(ang) * r * off)
            v.line([p0, (p0[0] + math.cos(ang) * r * 2.1, p0[1] + math.sin(ang) * r * 2.1)], WRECK["frame"] + (255,), v.m(W * 0.05))
    # bridge tower
    bs = {"dreadnought": 0.36, "battleship": 0.40, "cruiser": 0.45, "frigate": 0.4, "carrier": None}[kind]
    if bs and inside(bs):
        v.poly([P(bs - 0.04, W * 0.45), P(bs + 0.03, W * 0.4), P(bs + 0.03, -W * 0.4), P(bs - 0.04, -W * 0.45)], fill=WRECK["plate"] + (255,),
               outline=WRECK["frame"] + (255,), width=v.px(2))
        v.line([P(bs - 0.01, W * 0.3), P(bs - 0.01, -W * 0.3)], (255, 174, 74, 90), v.px(2))
    # dead engines at the stern
    if s0 <= 0.02:
        nb = {"dreadnought": 4, "battleship": 3, "carrier": 4, "cruiser": 2, "frigate": 2}[kind]
        for i in range(nb):
            w = W * 0.75 * (-1 + 2 * (i + 0.5) / nb)
            br = W * 0.75 / nb
            v.poly([P(0.0, w - br * 0.6), P(0.0, w + br * 0.6), P(-0.025 * 600 / L * 6, w + br), P(-0.025 * 600 / L * 6, w - br)],
                   fill=WRECK["frame"] + (255,))
    # hull breaches with embers
    for _ in range({"dreadnought": 5, "battleship": 4, "carrier": 3, "cruiser": 2, "frigate": 1}[kind]):
        s = rng.uniform(max(s0, 0.08), min(s1, 0.9)); w = rng.uniform(-0.7, 0.7) * half_width(s); r = W * rng.uniform(0.12, 0.25)
        c = P(s, w)
        v.circle(c, r * 1.5, fill=WRECK["scorch"] + (200,))
        v.circle(c, r, fill=(12, 10, 9, 255), outline=WRECK["ember"] + (200,), width=v.px(2))
        v.dot(c, 2.5, WRECK["ember"] + (230,))
    return outline
