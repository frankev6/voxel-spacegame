"""Round 4, step 1 (v2): plan-view sprites for the second ship lines, for the owner to approve before any 3D build.

Every ship has a job, and its outline comes from that job (armour where it takes hits, guns where they bear, hangars,
tanks, radiators, engines). Each line shares a doctrine, a build grammar and a paint pattern. Each card shows the
sprite in the line's colours plus a black silhouette thumbnail.

Writes docs/design/lookbook/silhouettes.html. Run with system Python:  python3 ArtSource/LookDev/silhouettes.py
Units are metres; x right, y forward (bow at the top). v1 (rejected: novelty shapes) is kept as silhouettes-v1.html."""

import math
import os

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "docs", "design", "lookbook", "silhouettes.html"))


def F(v):
    return f"{v:.2f}"


class Sp:
    """A plan-view sprite: SVG elements in metres (y up), with layer classes styled per line."""

    def __init__(self):
        self.els = []
        self.pts = []
        self.side = None

    def _d(self, pts):
        self.pts += pts
        return "M" + " L".join(f"{F(x)},{F(-y)}" for x, y in pts) + " Z"

    def poly(self, pts, c="base", mirror=False):
        self.els.append(f'<path class="{c}" d="{self._d(pts)}"/>')
        if mirror:
            self.poly([(-x, y) for x, y in pts][::-1], c)

    def hull(self, st, c="base", cx=0.0):
        """Symmetric hull from stations [(y, half-width)], stern first."""
        R = [(cx + w, y) for y, w in st]
        L = [(cx - w, y) for y, w in st][::-1]
        self.poly(R + L, c)

    def oct(self, x0, y0, x1, y1, ch=1.0, c="base", mirror=False):
        ch = min(ch, (x1 - x0) / 2.01, (y1 - y0) / 2.01)
        self.poly([(x0 + ch, y0), (x1 - ch, y0), (x1, y0 + ch), (x1, y1 - ch), (x1 - ch, y1), (x0 + ch, y1), (x0, y1 - ch), (x0, y0 + ch)], c, mirror)

    def circ(self, x, y, r, c="base", mirror=False):
        self.pts += [(x - r, y - r), (x + r, y + r)]
        self.els.append(f'<circle class="{c}" cx="{F(x)}" cy="{F(-y)}" r="{F(r)}"/>')
        if mirror and x:
            self.circ(-x, y, r, c)

    def ring(self, x, y, r0, r1, c="base"):
        self.pts += [(x - r1, y - r1), (x + r1, y + r1)]
        d = (f"M{F(x + r1)},{F(-y)} a{F(r1)},{F(r1)} 0 1,0 {F(-2 * r1)},0 a{F(r1)},{F(r1)} 0 1,0 {F(2 * r1)},0 Z "
             f"M{F(x + r0)},{F(-y)} a{F(r0)},{F(r0)} 0 1,0 {F(-2 * r0)},0 a{F(r0)},{F(r0)} 0 1,0 {F(2 * r0)},0 Z")
        self.els.append(f'<path class="{c}" fill-rule="evenodd" d="{d}"/>')

    def taper(self, pts, widths, c="base", mirror=False):
        if not isinstance(widths, (list, tuple)):
            widths = [widths] * len(pts)
        L, R = [], []
        n = len(pts)
        for i, (p, w) in enumerate(zip(pts, widths)):
            a, b = pts[max(0, i - 1)], pts[min(n - 1, i + 1)]
            dx, dy = b[0] - a[0], b[1] - a[1]
            ln = math.hypot(dx, dy) or 1.0
            nx, ny = -dy / ln, dx / ln
            L.append((p[0] + nx * w / 2, p[1] + ny * w / 2))
            R.append((p[0] - nx * w / 2, p[1] - ny * w / 2))
        self.poly(L + R[::-1], c, mirror)

    def arch(self, p0, p1, bulge, w, c="frame", mirror=False, n=10):
        """A curved strut (flying buttress) from p0 to p1, bowed sideways by `bulge`; w a width or (w0, w1)."""
        dx, dy = p1[0] - p0[0], p1[1] - p0[1]
        ln = math.hypot(dx, dy) or 1.0
        nx, ny = -dy / ln, dx / ln
        pts = [(p0[0] + dx * t + nx * bulge * math.sin(math.pi * t), p0[1] + dy * t + ny * bulge * math.sin(math.pi * t))
               for t in [i / n for i in range(n + 1)]]
        if isinstance(w, (list, tuple)):
            ws = [w[0] + (w[1] - w[0]) * i / n for i in range(n + 1)]
        else:
            ws = [w] * (n + 1)
        self.taper(pts, ws, c, mirror)

    def line(self, pts, c="seam", mirror=False):
        self.els.append(f'<polyline class="{c}" points="' + " ".join(f"{F(x)},{F(-y)}" for x, y in pts) + '"/>')
        if mirror:
            self.line([(-x, y) for x, y in pts], c)

    def turret(self, x, y, r, barrels=2, ang=90.0, blen=None, mirror=False):
        blen = blen if blen is not None else r * 1.6
        a = math.radians(ang)
        for k in range(barrels):
            off = (k - (barrels - 1) / 2) * r * 0.42
            ox, oy = -math.sin(a) * off, math.cos(a) * off
            p0 = (x + ox, y + oy)
            p1 = (x + ox + math.cos(a) * (r + blen), y + oy + math.sin(a) * (r + blen))
            self.taper([p0, p1], [r * 0.24, r * 0.2], "gun")
        self.circ(x, y, r, "tur")
        self.circ(x, y, r * 0.55, "turi")
        if mirror and x:
            self.turret(-x, y, r, barrels, 180 - ang, blen)

    def engine(self, x, y, w, l, bell=False, mirror=False):
        """Nozzle with its exhaust face at y (pointing -y)."""
        if bell:
            self.poly([(x - w * 0.32, y + l), (x + w * 0.32, y + l), (x + w * 0.5, y), (x - w * 0.5, y)], "frame")
        else:
            self.oct(x - w / 2, y, x + w / 2, y + l, w * 0.18, "frame")
        self.poly([(x - w * 0.42, y), (x + w * 0.42, y), (x + w * 0.3, y - w * 0.55), (x - w * 0.3, y - w * 0.55)], "glow")
        if mirror and x:
            self.engine(-x, y, w, l, bell)

    def hazard(self, x0, y0, x1, y1, mirror=False):
        self.oct(x0, y0, x1, y1, 0.0, "haz", mirror)

    def windows(self, x, y0, y1, step=1.6, mirror=False):
        y = y0
        while y <= y1:
            self.circ(x, y, 0.28, "win", mirror)
            y += step

    def spire(self, x, y, r, mirror=False):
        self.poly([(x, y + r), (x + r * 0.7, y), (x, y - r), (x - r * 0.7, y)], "plate", mirror)
        self.circ(x, y, r * 0.25, "trim", mirror)

    def rose(self, x, y, r):
        self.circ(x, y, r, "trim")
        self.circ(x, y, r * 0.82, "frame")
        for k in range(8):
            a = math.pi * k / 8
            self.line([(x + math.cos(a) * r * 0.8, y + math.sin(a) * r * 0.8), (x - math.cos(a) * r * 0.8, y - math.sin(a) * r * 0.8)], "tline")
        self.circ(x, y, r * 0.3, "trim")

    def bbox(self):
        xs = [p[0] for p in self.pts]
        ys = [p[1] for p in self.pts]
        return min(xs), min(ys), max(xs), max(ys)


def arcpts(cx, cy, r, a0, a1, n=24):
    return [(cx + r * math.cos(math.radians(a0 + (a1 - a0) * i / n)), cy + r * math.sin(math.radians(a0 + (a1 - a0) * i / n))) for i in range(n + 1)]


# ======================================================================================================================
# DRYDOCK line B: Caisson Heavy Industries, a deep-space mining and salvage consortium
# ======================================================================================================================

def dc_cutter():
    s = Sp()
    s.hull([(-1, 6), (2, 8.5), (20, 9), (27, 8), (31, 6)], "base")
    for sx in (-1, 1):
        s.taper([(sx * 5.2, 26), (sx * 5.4, 38), (sx * 4.6, 46)], [4.4, 3.8, 3.0], "base")
        s.oct(sx * 4.8 - 2.6, 42, sx * 4.8 + 2.6, 49, 1.0, "plate")
        s.circ(sx * 3.6, 48.6, 0.8, "laser")
    s.oct(6, 6, 12.5, 24, 1.5, "plate", mirror=True)
    s.hazard(6.4, 21.5, 12.1, 23.6, mirror=True)
    s.oct(-3.2, 14, 3.2, 22, 1.2, "cab")
    s.line([(0, 22), (0, 30)], "seam")
    s.poly([(-2.2, 30), (2.2, 30), (1.5, 33), (-1.5, 33)], "frame")
    s.turret(9.2, 14, 1.4, 1, mirror=True)
    s.engine(3.4, -1, 3.0, 3.5, mirror=True)
    s.oct(7.5, 0, 11, 5, 0.8, "frame", mirror=True)
    s.windows(0, 16, 20, 1.3)
    return s, dict(name="CUTTER", role="Frigate · armour stripper",
                   job="A mining cutter with its safety locks off. Two cutting heads focus their beams between the prongs and strip a hull's armour in seconds at knife range, while yellow ore-plate shoulders soak return fire.",
                   weak="Anything that keeps its distance. Exposed from behind.")


def dc_surveyor():
    s = Sp()
    for sx in (-1, 1):
        s.taper([(sx * 3.5, 24), (sx * 12, 10), (sx * 17.5, 1)], [3.2, 3.0, 2.6], "rad")
        s.taper([(sx * 3.2, 14), (sx * 9, 4), (sx * 12, -1)], [2.4, 2.2, 2.0], "rad")
    s.hull([(-3, 4.2), (2, 4.6), (30, 4), (44, 2.6), (50, 0.8)], "base")
    s.circ(0, 38, 9, "frame")
    s.ring(0, 38, 7.4, 9, "plate")
    s.circ(0, 38, 6.6, "cab")
    for k in range(6):
        a = math.pi * k / 6
        s.line([(math.cos(a) * 6.4, 38 + math.sin(a) * 6.4), (-math.cos(a) * 6.4, 38 - math.sin(a) * 6.4)], "seam")
    s.circ(0, 38, 1.5, "plate")
    s.hazard(-3.2, 46, 3.2, 47.5)
    s.oct(-2.4, 16, 2.4, 24, 1, "cab")
    s.turret(0, 10, 1.2, 1)
    s.engine(2.5, -3, 4.2, 5, mirror=True)
    return s, dict(name="SURVEYOR", role="Frigate · spotter",
                   job="A prospecting ship. Its dorsal array maps ore bodies from 50 km out, and in a fight it paints targets for the fleet's long guns, extending their range. Oversized engines and radiator vanes let it sprint and keep sprinting.",
                   weak="Barely armed. Dies to anything that closes.")


def dc_tug():
    s = Sp()
    for sx in (-1, 1):
        for y0 in (0, 22):
            s.taper([(sx * 7, y0 + 6), (sx * 13, y0 + 6)], [3.2, 3.2], "frame")
            s.oct(sx * 15.5 - 3.4, y0, sx * 15.5 + 3.4, y0 + 12, 1.2, "plate")
            s.hazard(sx * 15.5 - 3.0, y0 + 10, sx * 15.5 + 3.0, y0 + 11.6)
            s.engine(sx * 15.5, y0, 4.4, 2.0)
    s.oct(-7.5, 2, 7.5, 34, 2.2, "base")
    s.poly(arcpts(0, 41, 9, 160, 380, 24) + arcpts(0, 41, 5.4, 380, 160, 24), "plate")
    s.circ(0, 41, 2.2, "laser")
    s.oct(-3.4, 18, 3.4, 26, 1.2, "cab")
    s.turret(0, 9, 1.5, 2)
    s.line([(-7, 14), (7, 14)], "seam")
    s.line([(-7, 30), (7, 30)], "seam")
    return s, dict(name="TUG", role="Frigate · control",
                   job="A deep-space tug: four engine pods at the corners for raw thrust, and a graviton clamp at the bow. It pins an enemy ship, or hauls it out of formation and into your guns.",
                   weak="Lightly armed on its own. Its job ends once the clamp breaks.")


def dc_tanker():
    s = Sp()
    s.oct(-2.2, -6, 2.2, 74, 0.6, "frame")
    for y in (14, 34, 54):
        s.oct(-17, y - 8.6, 17, y + 8.6, 2, "frame")
        for sx in (-1, 1):
            s.circ(sx * 8.3, y, 7.6, "plate")
            s.circ(sx * 8.3, y, 6.8, "cab")
            s.line([(sx * 8.3 - 6.8, y), (sx * 8.3 + 6.8, y)], "pline")
            s.circ(sx * 8.3, y, 1.0, "frame")
    s.hull([(66, 7), (80, 7), (88, 4.5), (92, 1)], "base")
    s.oct(-4, 72, 4, 82, 1.5, "cab")
    s.hazard(-6.5, 84.5, 6.5, 86.5)
    s.oct(-9.5, -16, 9.5, 2, 2.5, "base")
    s.oct(-11.5, -10, 11.5, -4, 1, "rad")
    s.engine(0, -16, 3.6, 3)
    s.engine(5.4, -16, 3.6, 3, mirror=True)
    s.turret(0, 24, 1.3, 1)
    s.turret(0, 44, 1.3, 1)
    return s, dict(name="TANKER", role="Cargo · volatiles and fuel",
                   job="Six insulated spheres of fuel and volatiles in a braced frame. It keeps a fleet flying far from any port. The frame carries no armour: if a sphere is hit it is jettisoned, not repaired.",
                   weak="Anything. It needs an escort.")


def dc_orehauler():
    s = Sp()
    s.oct(-3, -8, 3, 80, 0.8, "frame")
    for i, y in enumerate((8, 22, 36, 50, 64)):
        for sx, dy in ((-1, 0), (1, 7)):
            if sx > 0 and i == 4:
                continue
            cx, cy = sx * 9.5, y + dy
            s.poly([(cx - 5.5, cy - 3), (cx - 5.5, cy + 3), (cx, cy + 6.4), (cx + 5.5, cy + 3), (cx + 5.5, cy - 3), (cx, cy - 6.4)], "plate")
            s.poly([(cx - 4, cy - 2.2), (cx - 4, cy + 2.2), (cx, cy + 4.6), (cx + 4, cy + 2.2), (cx + 4, cy - 2.2), (cx, cy - 4.6)], "ore")
    s.poly([(-6, 78), (6, 78), (15, 94), (-15, 94)], "base")
    s.poly([(-11, 91), (11, 91), (5, 82), (-5, 82)], "frame")
    for x in range(-13, 14, 4):
        s.poly([(x - 1, 94), (x + 1, 94), (x, 97)], "plate")
    s.oct(-17, 2, -12, 12, 1, "cab")
    s.oct(-8, -14, 8, 0, 2, "base")
    for x in (-5.4, -1.8, 1.8, 5.4):
        s.engine(x, -14, 3, 2.6)
    s.turret(0, 40, 1.2, 1)
    return s, dict(name="ORE HAULER", role="Cargo · bulk ore and salvage",
                   job="A scoop at the bow swallows ore and scrap into hexagonal bins clamped down a bare spine. Bins are swapped, not unloaded. The staggered bins are its signature outline.",
                   weak="Slow and wide. The scoop is its only armour.")


def dc_tender():
    s = Sp()
    s.hull([(0, 12), (58, 12), (68, 9), (76, 4), (79, 0.5)], "base")
    s.oct(-10, 5, 10, 56, 1, "frame")
    for y in range(9, 55, 6):
        s.line([(-10, y), (10, y)], "seam")
    for (x, y, c) in ((-7, 10, "plate"), (-3, 10, "plate"), (2, 12, "cab"), (6, 10, "plate"), (-6, 28, "cab"), (4, 30, "plate"), (-2, 44, "plate"), (5, 46, "cab")):
        s.oct(x - 1.6, y - 1.6, x + 1.6, y + 1.6, 0.3, c)
    for y in (20, 40):
        s.oct(-15, y - 1.4, 15, y + 1.4, 0.4, "plate")
        s.oct(-2.5, y - 2.4, 2.5, y + 2.4, 0.6, "frame")
    s.oct(9, 56, 15.5, 70, 1.5, "cab")
    s.windows(12.2, 59, 68, 1.5)
    s.hazard(-12, 72, 12, 74)
    s.hull([(10, 4), (22, 5), (34, 4), (40, 1)], "ghost", cx=-25)
    for y in (14, 30):
        s.oct(-21, y - 1, -12, y + 1, 0.3, "frame")
    for x in (-5, 0, 5):
        s.engine(x, 0, 3.4, 2.6)
    s.turret(-7, 62, 1.4, 2)
    return s, dict(name="TENDER", role="Cargo · field repair",
                   job="A flatbed of spare hull blocks under a travelling gantry crane. It clamps a damaged frigate (shown pale) to its side and rebuilds it during the battle. The island bridge sits to starboard to keep the deck clear.",
                   weak="A priority target. Lose it and the fleet stops healing.")


def dc_gyre():
    s = Sp()
    s.oct(12, 4, 27, 8, 0.5, "rad", mirror=True)
    s.oct(12, 11, 24, 15, 0.5, "rad", mirror=True)
    s.hull([(-2, 9), (10, 12), (44, 12), (50, 10), (70, 10), (92, 9), (112, 5), (124, 1.5)], "base")
    s.oct(-49, 51, 49, 59, 2, "cab")
    for x in range(-44, 45, 8):
        s.line([(x, 51), (x, 59)], "seam")
    s.oct(42, 51, 49, 59, 1.5, "plate", mirror=True)
    s.circ(0, 55, 11, "frame")
    s.circ(0, 55, 7, "plate")
    s.poly([(-9, 100), (9, 100), (6, 112), (-6, 112)], "plate")
    s.hazard(-5.5, 110, 5.5, 112)
    s.turret(0, 92, 3.0, 3)
    s.turret(0, 78, 3.0, 3)
    s.turret(0, 34, 2.6, 2, ang=270)
    s.turret(45.5, 55, 1.6, 1, mirror=True)
    s.oct(-3.5, 64, 3.5, 72, 1.2, "cab")
    for x in (-7.5, -2.5, 2.5, 7.5):
        s.engine(x, -2, 4.2, 3.4)
    side = Sp()
    side.ring(55, 0, 40, 47, "cab")
    for k in range(6):
        a = math.radians(30 * k)
        side.taper([(55 - math.cos(a) * 40, -math.sin(a) * 40), (55 + math.cos(a) * 40, math.sin(a) * 40)], [2.4, 2.4], "frame")
    side.taper([(0, 0), (44, 0), (90, 0), (124, 0)], [11, 14, 10, 2], "base")
    side.circ(55, 0, 10, "plate")
    s.side = side
    return s, dict(name="GYRE", role="Cruiser · long-range patrol and command",
                   job="A 95 m centrifuge ring gives the crew gravity on patrols that last years. Seen from above it is a cross, from the side a wheel. It carries the fleet's command staff, three triple turrets and gun blisters on the rim.",
                   weak="The ring is a huge target. Lose it and the patrol is over.")


def dc_breaker():
    s = Sp()
    s.hull([(0, 18), (28, 24), (58, 22), (84, 15), (104, 7), (116, 2)], "base")
    plates = [[(-24, 26), (-17, 32), (-14, 52), (-23, 50)], [(17, 32), (24, 26), (23, 50), (14, 52)],
              [(-23, 54), (-14, 56), (-11, 78), (-17, 76)], [(14, 56), (23, 54), (17, 76), (11, 78)],
              [(-15, 80), (-8, 84), (-5, 100), (-9, 98)], [(8, 84), (15, 80), (9, 98), (5, 100)],
              [(-25, 4), (-18, 6), (-18, 24), (-26, 22)], [(18, 6), (25, 4), (26, 22), (18, 24)]]
    for p in plates:
        q = [(x * 1.04, y) for x, y in p]
        s.poly(q, "plate")
        s.line(q + [q[0]], "seam")
    for x in (-5.5, 0, 5.5):
        s.oct(x - 1.2, 96, x + 1.2, 124 - abs(x) * 0.8, 0.4, "frame")
        s.circ(x, 124 - abs(x) * 0.8, 1.3, "laser")
    s.hazard(-4, 102, 4, 104)
    s.turret(11, 64, 3.2, 2, mirror=True)
    s.turret(11, 38, 3.2, 2, mirror=True)
    s.turret(0, 80, 2.4, 1)
    s.oct(-5, 18, 5, 30, 2, "cab")
    for x in (-12, -6, 0, 6, 12):
        s.engine(x, 0, 4.4, 3)
    return s, dict(name="BREAKER", role="Cruiser · line brawler",
                   job="An ore-breaker cruiser armoured in raw asteroid slabs bolted over the hull, which is why its outline is so jagged. Three mining lances at the bow. It wedges into enemy formations and takes the hits the fleet can't.",
                   weak="Slow to turn. Its lances only fire forward.")


def dc_hive():
    s = Sp()
    for y in (18, 50, 82):
        s.oct(-15, y - 2.4, 15, y + 2.4, 0.5, "frame")
    for sx in (-1, 1):
        cx = sx * 20.5
        s.poly([(cx - 6, 2), (cx + 6, 2), (cx + 6, 90), (cx + 2, 100), (cx - 2, 100), (cx - 6, 90)], "base")
        s.oct(cx - 3, 8, cx + 3, 94, 0.5, "frame")
        for y in range(12, 92, 8):
            s.line([(cx, y), (cx, y + 4)], "hline")
        s.poly([(cx - 2.6, 92), (cx + 2.6, 92), (cx, 97)], "haz")
        s.engine(cx - 3, 2, 3.2, 2.6)
        s.engine(cx + 3, 2, 3.2, 2.6)
        for y in (24, 48, 72):
            s.oct(cx + sx * 4.2 - 1, y - 4, cx + sx * 4.2 + 1, y + 4, 0.3, "plate")
    s.hull([(0, 7), (100, 7), (112, 4), (122, 1)], "base")
    s.oct(-4.5, 30, 4.5, 70, 1, "plate")
    s.oct(22, 56, 29, 70, 1.5, "cab")
    s.turret(0, 92, 2.4, 2)
    s.turret(0, 20, 2.4, 2, ang=270)
    s.engine(-2.5, 0, 3.4, 3)
    s.engine(2.5, 0, 3.4, 3)
    return s, dict(name="HIVE", role="Cruiser · drone carrier",
                   job="A converted ore carrier. The two side hulls are flight decks for mining drones that now carry warheads, joined to the centre hull by bridges with open space between. The island stands on the starboard deck.",
                   weak="Thin-skinned. Falls fast once its drones are spent.")


def dc_crucible():
    s = Sp()
    s.oct(-30, 6, 30, 58, 6, "base")
    for sx in (-1, 1):
        s.taper([(sx * 30, 28), (sx * 62, 22)], [14, 9], "rad")
        for t in range(5):
            s.line([(sx * (34 + t * 6), 21 + t * 0.2), (sx * (34 + t * 6), 34 - t * 1.2)], "seam")
        s.oct(sx * 52 - 13, 88, sx * 52 + 13, 124, 4, "plate")
        s.turret(sx * 54, 106, 5.5, 3)
    s.hull([(140, 20), (172, 20), (200, 14), (222, 6), (230, 2)], "base")
    s.oct(-4, 150, 4, 224, 1.5, "frame")
    s.oct(-1.4, 152, 1.4, 222, 0.5, "melt")
    s.hazard(-9, 222, 9, 226)
    s.circ(0, 105, 54, "frame")
    s.ring(0, 105, 47, 54, "plate")
    s.circ(0, 105, 42, "melt")
    for (x, y, r) in ((-14, 112, 9), (12, 96, 7), (20, 120, 6), (-22, 90, 5), (2, 128, 5), (-4, 84, 6)):
        s.circ(x, y, r, "crust")
    for a in range(0, 360, 30):
        s.line([(math.cos(math.radians(a)) * 47.5, 105 + math.sin(math.radians(a)) * 47.5),
                (math.cos(math.radians(a)) * 53.5, 105 + math.sin(math.radians(a)) * 53.5)], "seam")
    s.turret(-12, 176, 3.4, 2)
    s.turret(12, 176, 3.4, 2)
    s.oct(-8, 40, 8, 54, 2, "cab")
    for x in range(-25, 26, 10):
        s.engine(x, 6, 6, 4)
    return s, dict(name="CRUCIBLE", role="Dreadnought · foundry and siege",
                   job="A floating foundry. The open crucible melts wrecks and asteroids into new hull blocks for the fleet. In battle it pours that melt down the spinal spout at the bow as a slag cannon. Its batteries sit on the trunnion sponsons that hold the crucible.",
                   weak="Takes minutes to bring the melt to firing heat. Rush it before then.")


def dc_railhead():
    s = Sp()
    for y in (78, 118, 158, 198):
        s.taper([(8, y), (30, y - 8), (50, y - 14)], [7, 6.5, 6], "rad", mirror=True)
        for t in range(6):
            x = 12 + t * 6.4
            s.line([(x, y - (x - 8) * 0.33 + 3), (x, y - (x - 8) * 0.33 - 3.2)], "seam", mirror=True)
    s.hull([(-12, 16), (24, 20), (40, 17), (52, 9)], "base")
    s.oct(-7, 44, 7, 258, 1.5, "frame")
    for y in range(58, 252, 14):
        s.oct(-10, y - 1.8, 10, y + 1.8, 0.6, "plate")
        s.oct(-2.4, y - 1.0, 2.4, y + 1.0, 0.2, "coil")
    s.circ(0, 26, 15, "frame")
    s.circ(0, 26, 12, "cab")
    s.circ(0, 26, 4.5, "coil")
    s.oct(-14, 254, 14, 266, 3, "plate")
    s.hazard(-12, 263.5, 12, 266)
    s.oct(-5, 44, 5, 52, 1.5, "cab")
    s.turret(14, 32, 2.2, 2, mirror=True)
    s.turret(0, 236, 1.6, 1)
    for x in (-10, -3.4, 3.4, 10):
        s.engine(x, -12, 5, 3.6)
    return s, dict(name="RAILHEAD", role="Dreadnought · siege artillery",
                   job="A 260 m ore launcher pressed into war. The whole ship is the gun: a coil-banded barrel with racks of radiator vanes down its length, and a reactor and crew section at the breech. It hits from beyond everyone else's range and turns asteroids into ammunition.",
                   weak="Nearly helpless up close, and it can't turn quickly. It lives or dies by its escorts.")


def dc_homestead():
    s = Sp()
    s.oct(14, 18, 34, 62, 3, "plate", mirror=True)
    for y in (24, 36, 48):
        s.oct(18, y, 32, y + 7, 0.5, "frame", mirror=True)
    s.ring(0, 112, 60, 74, "cab")
    for k in range(24):
        a = math.radians(15 * k)
        s.line([(math.cos(a) * 60, 112 + math.sin(a) * 60), (math.cos(a) * 74, 112 + math.sin(a) * 74)], "seam")
    for k in range(6):
        a = math.radians(30 + 60 * k)
        s.taper([(math.cos(a) * 18, 112 + math.sin(a) * 18), (math.cos(a) * 61, 112 + math.sin(a) * 61)], [4, 4], "frame")
    s.hull([(0, 14), (180, 14), (206, 10), (232, 2)], "base")
    s.circ(0, 112, 19, "plate")
    s.circ(0, 112, 12, "frame")
    for sx in (-1, 1):
        s.taper([(sx * 12, 192), (sx * 30, 192)], [3, 3], "frame")
        s.hull([(178, 4), (196, 5), (208, 3), (212, 0.5)], "ghost", cx=sx * 36)
    s.oct(-6, 150, 6, 170, 2, "cab")
    s.turret(0, 214, 3.2, 3)
    s.turret(0, 86, 3.2, 3, ang=270)
    for x in range(-11, 12, 5):
        s.engine(x, 0, 4.2, 3.6)
    return s, dict(name="HOMESTEAD", role="Dreadnought · mothership",
                   job="The consortium's moving town. A 150 m habitat ring turns flat around the spine, home to thousands; frigates ride the docking pylons at the bow (shown pale), and hangar wings sit aft. It is the fleet's port, shipyard and capital.",
                   weak="The ring is armoured like a city, not a warship. It must not be allowed to fight alone.")


# ======================================================================================================================
# NOCTURNE line B: the Knell choir, a militant mortuary order
# ======================================================================================================================

def nk_penitent():
    s = Sp()
    s.arch((4.5, 24), (12.5, 12), 3.0, (2.2, 1.6), "frame", mirror=True)
    s.oct(9.5, 7, 15.5, 16, 1.6, "plate", mirror=True)
    s.turret(12.5, 11.5, 1.6, 1, mirror=True)
    s.hull([(0, 6), (8, 8), (28, 8), (32, 7)], "base")
    s.poly([(-8, 30), (8, 30), (6.5, 38), (2.5, 45), (0, 48), (-2.5, 45), (-6.5, 38)], "plate")
    s.poly([(-1.2, 40), (1.2, 40), (0, 52)], "frame")
    s.line([(-6, 33), (6, 33)], "tline")
    s.line([(-4.5, 38), (4.5, 38)], "tline")
    s.oct(-3.5, 2, 3.5, 9, 1.2, "accent")
    s.rose(0, 5.5, 2.2)
    s.spire(0, 20, 2.2)
    s.windows(5.4, 12, 26, 2.2, mirror=True)
    s.engine(0, 0, 7, 3, bell=True)
    return s, dict(name="PENITENT", role="Frigate · ram and boarder",
                   job="An ogive ram with a boarding drill. It punches into a hull and empties its crew into it; the order takes ships intact, dead crews and all. Arc projectors on buttress pods clear the breach.",
                   weak="Must close to touch. Flak and point defence shred it on the way in.")


def nk_acolyte():
    s = Sp()
    for (px, py) in ((13, 33), (13, 7)):
        s.arch((2.5, 20 + (py - 20) * 0.2), (px, py), 2.2 if py > 20 else -2.2, (2.0, 1.5), "frame", mirror=True)
        s.circ(px, py, 3.6, "plate", mirror=True)
        s.turret(px, py, 2.0, 3, ang=90 if py > 20 else 270, blen=1.2, mirror=True)
    s.hull([(0, 4), (34, 5), (40, 3), (44, 0.8)], "base")
    s.circ(0, 20, 4.6, "plate")
    s.spire(0, 20, 3.2)
    s.oct(-2.4, 30, 2.4, 36, 1, "accent")
    s.line([(0, 3), (0, 15)], "tline")
    s.engine(2.2, 0, 3.2, 2.6, bell=True, mirror=True)
    return s, dict(name="ACOLYTE", role="Frigate · point-defence escort",
                   job="Four buttress arms hold rotary point-defence pods around a spire mast. It sits beside a capital ship and blots out missiles and drones.",
                   weak="Its guns are tiny. A real warship ignores the pods and kills the hub.")


def nk_vesper():
    s = Sp()
    s.arch((3.5, 16), (13, 1), 2.4, (2.2, 1.0), "frame", mirror=True)
    s.hull([(0, 4.2), (40, 4.6), (50, 2.8), (58, 0.6)], "base")
    for k in range(4):
        for sx in (-1, 1):
            x = sx * (5.6 + k * 1.6)
            top = 46 - k * 6
            s.oct(x - 0.75, 16, x + 0.75, top, 0.3, "plate")
            s.circ(x, top, 0.75, "frame")
            s.line([(x - 0.75, top - 4), (x + 0.75, top - 4)], "tline")
    s.oct(-2.4, 6, 2.4, 12, 1, "accent")
    s.spire(0, 26, 1.8)
    s.spire(0, 36, 1.5)
    s.engine(2.2, 0, 3.2, 3, bell=True, mirror=True)
    return s, dict(name="VESPER", role="Frigate · torpedo strike",
                   job="Two banks of torpedo tubes stepped down its flanks like organ pipes. It closes, fires one salvo heavy enough to cripple a cruiser, and runs.",
                   weak="Very little after the salvo. Catch it while it reloads.")


def nk_reliquary():
    s = Sp()
    C = (0.0, 44.0)
    octo = [(C[0] + 24 * math.cos(math.radians(22.5 + 45 * k)), C[1] + 24 * math.sin(math.radians(22.5 + 45 * k))) for k in range(8)]
    s.hull([(0, 10), (12, 12), (24, 11)], "base")
    s.hull([(62, 11), (74, 9), (84, 4), (90, 0.6)], "base")
    for k in (0, 1, 2, 3):
        a = math.radians(45 + 90 * k)
        x, y = C[0] + 23 * math.cos(a), C[1] + 23 * math.sin(a)
        s.oct(x - 4, y - 4, x + 4, y + 4, 1.2, "base")
        s.spire(x, y, 2.6)
        s.turret(x + 3.4 * (1 if x > 0 else -1), y, 1.3, 2, ang=0 if x > 0 else 180, blen=1.0)
    s.poly(octo, "plate")
    s.poly([(C[0] + 17 * math.cos(math.radians(22.5 + 45 * k)), C[1] + 17 * math.sin(math.radians(22.5 + 45 * k))) for k in range(8)], "frame")
    for k in range(8):
        a = math.radians(45 * k)
        s.line([(C[0] + 5 * math.cos(a), C[1] + 5 * math.sin(a)), (C[0] + 16 * math.cos(a), C[1] + 16 * math.sin(a))], "tline")
    s.circ(C[0], C[1], 6.5, "accent")
    s.rose(C[0], C[1], 5)
    s.oct(-2.6, 64, 2.6, 80, 0.6, "accent")
    s.rose(0, 80, 3)
    s.windows(8.6, 64, 74, 2.4, mirror=True)
    for x in (-6, 0, 6):
        s.engine(x, 0, 4.4, 3, bell=True)
    return s, dict(name="RELIQUARY", role="Cargo · armoured vault transport",
                   job="An octagonal vault, the strongest shape for armour, with walls a metre of bronze thick, carries what the order values most: relics, captured cores, the honoured dead. Four corner towers carry the point defence, and only a short nave and engine house are bolted on.",
                   weak="Little firepower, and it can only run slowly. Its armour is the plan.")


def nk_pilgrim():
    s = Sp()
    for k in range(4):
        y = 14 + k * 17
        for sx in (-1, 1):
            s.arch((sx * 8, y + 2), (sx * 13.5, y + 2), 1.6 * sx, 1.6, "frame")
            cx = sx * 15.5
            s.poly([(cx - 3, y - 3), (cx + 3, y - 3), (cx + 3.2, y + 6), (cx, y + 10), (cx - 3.2, y + 6)], "accent")
            s.poly([(cx - 1.8, y - 2), (cx + 1.8, y - 2), (cx + 1.8, y + 5), (cx, y + 7.5), (cx - 1.8, y + 5)], "plate")
    s.hull([(0, 8.5), (82, 8.5), (94, 5.5), (100, 1)], "base")
    s.windows(6.6, 4, 86, 1.5, mirror=True)
    s.windows(3.6, 8, 80, 1.5, mirror=True)
    s.rose(0, 88, 3.4)
    s.spire(0, 46, 2.4)
    for x in (-5.6, -1.9, 1.9, 5.6):
        s.engine(x, 0, 3.4, 3, bell=True)
    return s, dict(name="PILGRIM", role="Cargo · troopship",
                   job="Carries a thousand of the faithful and eight assault landers docked nose-out in buttress cradles down its flanks. Its lit window rows are visible from a long way off.",
                   weak="Unarmoured, and crowded. It is the target.")


def nk_hospice():
    s = Sp()
    s.hull([(0, 12), (36, 12), (46, 8), (56, 9), (68, 14), (74, 13)], "base")
    s.oct(-7, 60, 7, 74.5, 1, "void")
    for sx in (-1, 1):
        s.taper([(sx * 12, 66), (sx * 15, 78), (sx * 12, 88), (sx * 6.5, 92)], [4.2, 3.6, 3.0, 2.0], "plate")
        s.poly([(sx * 6.5, 92), (sx * 4, 90), (sx * 6, 95.5)], "frame")
        s.circ(sx * 14.8, 77, 1.6, "frame")
    s.oct(-3, 4, 3, 40, 0.6, "accent")
    s.spire(0, 10, 2.2)
    for y in (20, 30, 50):
        s.circ(9.5, y, 1.1, "trim", mirror=True)
    s.windows(10.5, 4, 34, 2.2, mirror=True)
    for x in (-6, 0, 6):
        s.engine(x, 0, 4.4, 3, bell=True)
    return s, dict(name="HOSPICE", role="Cargo · recovery and salvage",
                   job="Gothic recovery arms lift wrecks and the dead into the open mortuary bay at its bow. The order counts the dead after every battle, theirs and yours. Salvaged blocks go to the fleet.",
                   weak="No guns to speak of. It comes after the battle, not during it.")


def nk_choir():
    s = Sp()
    s.hull([(4, 11), (88, 11), (104, 7), (120, 1)], "base")
    s.oct(-32, 55, 32, 71, 3, "base")
    for sx in (-1, 1):
        s.circ(sx * 32, 63, 9, "plate")
        s.ring(sx * 32, 63, 4.8, 6.6, "coil")
        s.circ(sx * 32, 63, 3, "frame")
        s.spire(sx * 18, 63, 2.4)
    s.circ(0, 6, 12, "base")
    for k in range(5):
        a = math.radians(210 + 30 * k)
        x, y = 13 * math.cos(a), 6 + 13 * math.sin(a)
        s.circ(x, y, 4, "plate")
        s.engine(x, y - 2, 3.4, 2.4, bell=True)
    s.circ(0, 63, 7.5, "trim")
    s.rose(0, 63, 6.5)
    s.oct(-3, 16, 3, 50, 0.6, "accent")
    s.oct(-3, 76, 3, 92, 0.6, "accent")
    s.turret(0, 84, 3.0, 3)
    s.turret(0, 36, 3.0, 3, ang=270)
    s.windows(9, 18, 50, 2.6, mirror=True)
    s.windows(9, 76, 90, 2.6, mirror=True)
    s.rose(0, 104, 3)
    return s, dict(name="CHOIR", role="Cruiser · anti-swarm storm ship",
                   job="A cruciform storm ship. The transept arms carry two storm-coil towers that chain lightning through packs of small ships, and the shield emitter sits in the crossing's rose window. The apse at the stern holds five engines, one in each radiating chapel.",
                   weak="The coils are short-ranged. A lone heavy ship at distance outguns it.")


def nk_cloister():
    s = Sp()
    s.hull([(68, 9), (96, 9), (110, 5), (122, 1)], "base")
    s.poly([(-27, 18), (27, 18), (27, 72), (-27, 72)], "base")
    s.oct(-17, 28, 17, 62, 1, "void")
    for y in range(31, 62, 6):
        s.line([(-14, y), (14, y)], "fline")
    s.poly([(-4, 56), (4, 56), (0, 61)], "haz")
    for sx in (-1, 1):
        for y in (18, 72):
            s.oct(sx * 27 - 6, y - 6, sx * 27 + 6, y + 6, 1.5, "plate")
            s.spire(sx * 27, y, 3)
            s.turret(sx * 27 + sx * 4.5, y + (4.5 if y > 40 else -4.5), 1.4, 2, ang=90 if y > 40 else 270, blen=1.0)
    for y in (30, 45, 60):
        s.oct(26, y - 2, 31, y + 2, 0.4, "frame", mirror=True)
    s.poly([(11 * math.cos(math.radians(22.5 + 45 * k)), 9 + 11 * math.sin(math.radians(22.5 + 45 * k))) for k in range(8)], "base")
    s.oct(-3, 74, 3, 96, 0.6, "accent")
    s.oct(-3, 20, 3, 26, 0.6, "accent")
    s.rose(0, 102, 3)
    s.turret(0, 86, 2.4, 2)
    for x in (-4.5, 0, 4.5):
        s.engine(x, -1, 3.6, 3, bell=True)
    return s, dict(name="CLOISTER", role="Cruiser · carrier",
                   job="Its drones launch from the open cloister court, walled in on four sides by hangar ranges with gun towers at the corners, so the launch is protected. A short nave carries the bridge forward, and an octagonal engine house sits aft.",
                   weak="Its own guns are few, and the open court is a weak spot from above.")


def nk_sexton():
    s = Sp()
    for sx in (-1, 1):
        s.arch((sx * 12, 40), (sx * 26, 50), -4 * sx, 2.6, "frame")
        s.arch((sx * 12, 62), (sx * 26, 54), 4 * sx, 2.6, "frame")
        s.oct(sx * 26 - 5.5, 45, sx * 26 + 5.5, 59, 2, "plate")
        s.turret(sx * 26, 52, 3.4, 3)
        s.taper([(sx * 9, 86), (sx * 9.5, 110), (sx * 7, 132)], [6, 5, 2], "plate")
    s.hull([(0, 12), (60, 14), (88, 12)], "base")
    s.oct(-2.4, 70, 2.4, 128, 0.6, "frame")
    for y in range(92, 128, 6):
        s.oct(-3.6, y, 3.6, y + 1.2, 0.3, "coil")
    s.oct(-3, 6, 3, 64, 0.6, "accent")
    s.spire(0, 24, 2.6)
    s.spire(0, 46, 2.6)
    s.windows(10.6, 8, 60, 2.6, mirror=True)
    for x in (-7, 0, 7):
        s.engine(x, 0, 5.2, 3.6, bell=True)
    return s, dict(name="SEXTON", role="Cruiser · capital killer",
                   job="A spinal particle lance runs the length of the nave and fires between twin armoured prongs. Batteries on buttress pods guard the flanks. It opens a dreadnought from 40 km.",
                   weak="Fixed forward gun: frigates that get under or around it are safe.")


def nk_basilica():
    s = Sp()
    s.hull([(14, 34), (186, 34), (196, 28)], "plate")
    for y in range(26, 190, 16):
        s.oct(33, y, 38, y + 7, 1, "frame", mirror=True)
    s.hull([(8, 26), (196, 26), (206, 20)], "base")
    s.hull([(0, 16), (204, 16), (222, 12), (236, 5), (242, 1)], "base")
    s.oct(-48, 128, 48, 152, 4, "base")
    for sx in (-1, 1):
        s.oct(sx * 29 - 11, 0, sx * 29 + 11, 26, 3, "base")
        s.spire(sx * 29, 13, 6)
        s.oct(sx * 48 - 8, 132, sx * 48 + 8, 148, 2, "plate")
        s.turret(sx * 48, 140, 5, 3)
        for y in (44, 76, 108, 168):
            s.turret(sx * 29.5, y, 4, 3)
    s.oct(-14, 126, 14, 154, 3, "plate")
    s.spire(0, 140, 9)
    s.oct(-3.5, 30, 3.5, 120, 0.8, "accent")
    s.oct(-3.5, 160, 3.5, 204, 0.8, "accent")
    for y in (60, 92, 178):
        s.spire(0, y, 4)
    s.rose(0, 214, 5)
    s.windows(19.5, 30, 196, 3, mirror=True)
    for x in (-6, 0, 6):
        s.engine(x, 2, 5.2, 4, bell=True)
    return s, dict(name="BASILICA", role="Dreadnought · broadside battleship",
                   job="A battleship laid out like a basilica. The stepped aisles are gun decks, with ten triple batteries down the flanks and two more at the transept ends. A crossing tower is the command keep, and the twin west towers are armoured citadels over the engines.",
                   weak="Its bow and stern are thin. It must turn to fight, and that takes time.")


def nk_mausoleum():
    s = Sp()
    for k in range(8):
        a = math.radians(22.5 + 45 * k)
        x, y = math.cos(a) * 68, 112 + math.sin(a) * 68
        s.arch((math.cos(a) * 50, 112 + math.sin(a) * 50), (x, y), 4, 3.4, "frame")
        s.circ(x, y, 8, "plate")
        s.turret(x, y, 3.0, 4, ang=math.degrees(a), blen=2.0)
    s.poly([(-24, 160), (24, 160), (18, 190), (8, 212), (0, 220), (-8, 212), (-18, 190)], "plate")
    s.line([(-20, 172), (20, 172)], "tline")
    s.line([(-15, 190), (15, 190)], "tline")
    s.oct(-26, 22, 26, 62, 6, "base")
    s.circ(0, 112, 55, "base")
    for k in range(16):
        a = math.radians(11.25 + 22.5 * k)
        s.line([(math.cos(a) * 14, 112 + math.sin(a) * 14), (math.cos(a) * 53, 112 + math.sin(a) * 53)], "fline")
    s.ring(0, 112, 30, 33, "trim")
    s.circ(0, 112, 13, "accent")
    s.rose(0, 112, 9)
    for x in (-18, -9, 0, 9, 18):
        s.engine(x, 22, 6, 4, bell=True)
    return s, dict(name="MAUSOLEUM", role="Dreadnought · shield fortress",
                   job="The dome is a shield generator, and eight buttress towers of point defence ring it. It anchors the line: the fleet fights inside its bubble while its ram does the rest.",
                   weak="Slow, and few long guns. Starve the shield with sustained fire, or ignore it and hit the fleet's flanks.")


def nk_gravekeeper():
    s = Sp()
    hulk = [(-20, 146), (-9, 138), (3, 144), (16, 136), (21, 156), (17, 174), (22, 190), (15, 208), (19, 224), (8, 242),
            (-2, 234), (-12, 244), (-19, 224), (-15, 206), (-21, 186), (-16, 166)]
    s.poly(hulk, "dead")
    for p in ((-10, 170), (7, 198), (-4, 224), (11, 158), (-12, 200)):
        s.circ(p[0], p[1], 3.4, "deadi")
    for y in (24, 60, 96, 128):
        s.arch((-22, y), (22, y), 6, 4, "frame")
        s.arch((-22, y + 8), (22, y + 8), -6, 3, "frame")
    for sx in (-1, 1):
        cx = sx * 34
        s.poly([(cx - 13, 4), (cx + 13, 4), (cx + 13, 196), (cx + 6, 214), (cx, 228), (cx - 6, 214), (cx - 13, 196)], "base")
        s.oct(cx + sx * 9 - 4, 20, cx + sx * 9 + 4, 190, 1, "plate")
        s.oct(cx - 2.6, 12, cx + 2.6, 190, 0.6, "accent")
        for y in (40, 90, 140, 190):
            s.spire(cx - sx * 4, y, 3)
        for y in (60, 110, 160):
            s.turret(cx + sx * 9, y, 3.6, 3)
        for x in (cx - 6, cx, cx + 6):
            s.engine(x, 4, 4.6, 3.6, bell=True)
        for y in (152, 184, 214):
            s.taper([(sx * 22, y), (sx * 16, y + 2)], [3.4, 2.6], "plate")
    return s, dict(name="GRAVEKEEPER", role="Dreadnought · twin-hull assault",
                   job="Two armoured naves clamp a recovered dead hulk between their bows and drive it ahead as an ablative shield (the dead protect the living). When the hulk is shot away, it docks a fresh one from the field.",
                   weak="Wide and clumsy. The naves are exposed from the sides.")


LINES = {
    "drydock": dict(name="DRYDOCK · line B · Caisson Heavy Industries",
                    doctrine="A deep-space mining and salvage consortium. Its ships are built around one big industrial module each (a cutter head, a tank cluster, a ring, a crucible, a rail) and armed with mining gear turned on people. They live far from ports, so they repair, refuel and refine for themselves.",
                    paint="Graphite hulls; safety-yellow ore-armour plates on the leading edges and sponsons; black-and-yellow chevrons on every bow; white crew modules and tanks; amber work lights.",
                    cls=dict(base="#4A4F57", plate="#D9A12E", frame="#25282D", cab="#E6E0D2", accent="#E6E0D2", rad="#3A3E44",
                             trim="#D9A12E", glow="#FF9A3D", laser="#7FF6FF", ore="#6E5A44", melt="#FF7A1E", crust="#3B2A20",
                             coil="#4FE3D2", ghost="#8A9098", dead="#555555", deadi="#333333", void="#121418", win="#FFC680", tur="#1C1E22"),
                    classes=[("Frigate", [dc_cutter, dc_surveyor, dc_tug]),
                             ("Cargo ship", [dc_tanker, dc_orehauler, dc_tender]),
                             ("Cruiser", [dc_gyre, dc_breaker, dc_hive]),
                             ("Dreadnought", [dc_crucible, dc_railhead, dc_homestead])]),
    "nocturne": dict(name="NOCTURNE · line B · the Knell choir",
                     doctrine="A militant mortuary order that recovers the dead from every battlefield and wages holy war with what it salvages. It fights in armour, at close range and by boarding, with storm coils and relic weapons. Its ships are laid out like the buildings it worships in: naves, crossings, apses, cloisters, towers and octagonal chapter houses, each one doing a job (a gun deck, a launch court, a vault, an engine house), with flying buttresses carrying guns outboard.",
                     paint="Bone-ivory armour; blackened-bronze buttresses and frames; oxblood roof bands and banners; gilt trim and rose windows; candle-amber lancet windows; blue-white engine light.",
                     cls=dict(base="#CFC3A3", plate="#7A5A36", frame="#2E2720", cab="#E8DFC8", accent="#7A231C", rad="#3A322A",
                              trim="#C9A04E", glow="#BFD6FF", laser="#BFD6FF", ore="#555555", melt="#FF7A1E", crust="#333333",
                              coil="#9FD8FF", ghost="#8C8579", dead="#4B4943", deadi="#2A2925", void="#15120F", win="#FFB65A", tur="#211C17"),
                     classes=[("Frigate", [nk_penitent, nk_acolyte, nk_vesper]),
                              ("Cargo ship", [nk_reliquary, nk_pilgrim, nk_hospice]),
                              ("Cruiser", [nk_choir, nk_cloister, nk_sexton]),
                              ("Dreadnought", [nk_basilica, nk_mausoleum, nk_gravekeeper])]),
}

SCALES = {"Frigate": 64, "Cargo ship": 112, "Cruiser": 150, "Dreadnought": 280}
BARS = {"Frigate": 10, "Cargo ship": 20, "Cruiser": 20, "Dreadnought": 50}


def sprite_svg(s, scale_m, bar=None, mono=False):
    x0, y0, x1, y1 = s.bbox()
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    half = scale_m / 2
    sw = scale_m / 400
    out = [f'<svg class="{"mono" if mono else "spr"}" viewBox="{F(cx - half)} {F(-cy - half)} {F(2 * half)} {F(2 * half)}" '
           f'xmlns="http://www.w3.org/2000/svg" style="--sw:{sw:.3f}">']
    out += s.els
    if bar and not mono:
        bx, by = cx - half + scale_m * 0.05, -cy + half - scale_m * 0.04
        out.append(f'<g class="bar"><line x1="{F(bx)}" y1="{F(by)}" x2="{F(bx + bar)}" y2="{F(by)}" stroke-width="{F(sw * 2.5)}"/>'
                   f'<text x="{F(bx)}" y="{F(by - sw * 8)}" font-size="{F(sw * 16)}">{bar} m</text></g>')
    out.append("</svg>")
    return "".join(out)


def side_svg(t):
    x0, y0, x1, y1 = t.bbox()
    pad = (x1 - x0) * 0.06
    sw = (x1 - x0) / 200
    return (f'<div class="side"><svg class="spr" viewBox="{F(x0 - pad)} {F(-y1 - pad)} {F(x1 - x0 + 2 * pad)} {F(y1 - y0 + 2 * pad)}" '
            f'xmlns="http://www.w3.org/2000/svg" style="--sw:{sw:.3f}">' + "".join(t.els) + '</svg><span>side, bow →</span></div>')


def build():
    import sil_batch3 as B3
    secs = []
    for key, Ln in LINES.items():
        rows = []
        for cls, fns in Ln["classes"]:
            built = [fn() for fn in fns]
            lens = [s.bbox()[3] - s.bbox()[1] for s, _ in built]
            cards = []
            for i, (s, meta) in enumerate(built):
                x0, y0, x1, y1 = s.bbox()
                opt = "ABC"[i]
                side = side_svg(s.side) if s.side else ""
                cards.append(f'<button class="card" data-dir="{key}" data-cls="{cls}" data-opt="{opt}" data-name="{meta["name"]}">'
                             f'<div class="tag">{opt}</div>{side}'
                             f'<div class="thumb">{sprite_svg(s, SCALES[cls], mono=True)}</div>'
                             f'{sprite_svg(s, SCALES[cls], BARS[cls])}'
                             f'<div class="nm">{meta["name"]}</div><div class="role">{meta["role"]} · {y1 - y0:.0f} m long, {x1 - x0:.0f} m wide</div>'
                             f'<p>{meta["job"]}</p><p class="weak"><b>Weak to:</b> {meta["weak"]}</p></button>')
            rows.append(f'<div class="row"><div class="cls"><b>{cls}</b><span>{min(lens):.0f}–{max(lens):.0f} m</span>'
                        f'<span class="sc">same scale across the row</span></div>' + "".join(cards) + "</div>")
        vars_ = ";".join(f"--{k}:{v}" for k, v in Ln["cls"].items())
        secs.append(f'<section style="{vars_}"><h2>{Ln["name"]}</h2><p class="blurb"><b>Doctrine.</b> {Ln["doctrine"]}</p>'
                    f'<p class="blurb"><b>Paint pattern.</b> {Ln["paint"]}</p>' + "".join(rows) + "</section>")
    html = (PAGE.replace("{{SECTIONS}}", "".join(secs)).replace("{{BATCH3}}", B3.render())
            .replace("/*CSS3*/", B3.CSS3 + B3.palette_css()))
    with open(OUT, "w") as fh:
        fh.write(html)
    print("wrote", OUT)


PAGE = """<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>Ship Line Silhouettes</title>
<style>
:root{--ink:#E9E6DF;--muted:#A3A9B6;--page:#070B16;--line:#26304A;--pick:#FFB020;--card:#0D1428}
*{box-sizing:border-box}
body{margin:0;background:var(--page);color:var(--ink);font:15px/1.45 -apple-system,BlinkMacSystemFont,"Segoe UI",Inter,sans-serif}
header{padding:28px 16px 6px;max-width:1320px;margin:0 auto}
h1{font-size:26px;margin:0 0 6px}
header p{margin:4px 0;color:var(--muted);max-width:900px}
section{max-width:1320px;margin:18px auto 44px;padding:0 16px}
h2{font-size:20px;margin:28px 0 6px;letter-spacing:.02em}
.blurb{color:var(--muted);margin:0 0 8px;max-width:980px}.blurb b{color:var(--ink)}
.row{display:grid;grid-template-columns:130px repeat(3,1fr);gap:12px;margin:14px 0;align-items:stretch}
.cls{display:flex;flex-direction:column;gap:2px;padding-top:6px}
.cls b{font-size:16px}.cls span{color:var(--muted);font-size:13px}.cls .sc{font-size:12px;margin-top:6px}
.card{all:unset;cursor:pointer;position:relative;background:var(--card);border:2px solid var(--line);border-radius:10px;padding:10px 12px 12px;
  display:flex;flex-direction:column;transition:border-color .12s, transform .12s}
.card:hover{transform:translateY(-1px);border-color:#3A4870}
.card:focus-visible{outline:2px solid var(--pick);outline-offset:2px}
.card.on{border-color:var(--pick);box-shadow:0 0 0 3px rgba(255,176,32,.25)}
.card svg.spr{width:100%;aspect-ratio:1/1;display:block}
.thumb{position:absolute;top:10px;left:10px;width:24%;background:#E9E6DF;border-radius:6px;padding:3px}
.thumb svg{width:100%;aspect-ratio:1/1;display:block}
.side{position:absolute;top:10px;right:10px;width:36%;text-align:center;font-size:10px;color:var(--muted);background:rgba(255,255,255,.04);border-radius:6px;padding:3px}
.side svg{width:100%;height:auto;display:block}
.tag{position:absolute;bottom:10px;right:12px;font-weight:700;font-size:13px;color:var(--muted)}
.card.on .tag::before{content:"picked ✓  ";color:var(--pick)}
.card.card3.on::after{content:"marked ✓";position:absolute;bottom:10px;right:12px;font-size:12px;font-weight:700;color:var(--pick)}
.nm{font-weight:700;letter-spacing:.08em;margin-top:2px;font-size:15px}
.role{font-size:12px;color:var(--muted)}
.card p{margin:6px 0 0;font-size:13px;line-height:1.4;color:#CFD3DC}
.card p.weak{color:#A9AEBA;margin-bottom:14px}.card p.weak b{color:#E58A6A;font-weight:600}
.spr .base{fill:var(--base)}.spr .plate{fill:var(--plate)}.spr .frame{fill:var(--frame)}.spr .cab{fill:var(--cab)}
.spr .accent{fill:var(--accent)}.spr .rad{fill:var(--rad)}.spr .trim{fill:var(--trim)}.spr .glow{fill:var(--glow);opacity:.9}
.spr .laser{fill:var(--laser)}.spr .ore{fill:var(--ore)}.spr .melt{fill:var(--melt)}.spr .crust{fill:var(--crust);opacity:.8}
.spr .coil{fill:var(--coil)}.spr .ghost{fill:var(--ghost);opacity:.55}.spr .dead{fill:var(--dead)}.spr .deadi{fill:var(--deadi)}
.spr .void{fill:var(--void)}.spr .win{fill:var(--win)}.spr .tur{fill:var(--tur)}.spr .turi{fill:none;stroke:var(--trim);stroke-width:calc(var(--sw)*1.4)}
.spr .gun{fill:var(--frame)}
.spr .haz{fill:url(#haz)}
.spr .seam{fill:none;stroke:rgba(0,0,0,.45);stroke-width:var(--sw)}
.spr .pline{fill:none;stroke:var(--plate);stroke-width:calc(var(--sw)*3)}
.spr .tline{fill:none;stroke:var(--trim);stroke-width:calc(var(--sw)*1.6)}
.spr .fline{fill:none;stroke:var(--trim);stroke-width:calc(var(--sw)*1.2);opacity:.8}
.spr .hline{fill:none;stroke:var(--plate);stroke-width:calc(var(--sw)*2)}
.mono *{fill:#111 !important;stroke:none !important}
.mono .glow,.mono .win,.mono .ghost,.mono .seam,.mono .tline,.mono .fline,.mono .hline,.mono .pline,.mono .turi{display:none}
.bar line{stroke:#7C8496}.bar text{fill:#7C8496;font-family:inherit}
.picks{position:sticky;bottom:0;background:rgba(7,11,22,.92);backdrop-filter:blur(6px);border-top:1px solid var(--line);padding:10px 16px}
.picks div{max-width:1320px;margin:0 auto;display:flex;gap:12px;align-items:center;flex-wrap:wrap}
.picks code{font-size:13px;background:var(--line);padding:4px 8px;border-radius:6px;white-space:normal}
.picks button{font:inherit;padding:6px 12px;border-radius:8px;border:1px solid var(--line);background:var(--ink);color:var(--page);cursor:pointer}
@media (max-width:900px){.row{grid-template-columns:1fr}.cls{flex-direction:row;gap:10px;align-items:baseline}}
/*CSS3*/
</style></head><body class="pdd">
<svg width="0" height="0" style="position:absolute"><defs>
<pattern id="haz" patternUnits="userSpaceOnUse" width="2.4" height="2.4" patternTransform="rotate(45)">
<rect width="2.4" height="2.4" fill="#E2AA2E"/><rect width="1.2" height="2.4" fill="#15161A"/></pattern>
<filter id="vblur" x="-50%" y="-50%" width="200%" height="200%"><feGaussianBlur stdDeviation="2.4"/></filter></defs></svg>
<header><h1>Ship lines: silhouettes for approval</h1>
<p>Plan views from above, bow at the top. The thumbnail top-left of each card is the same ship as a black silhouette (the outline test).
Every card of the same class is drawn at the same scale. Pale shapes are other ships (docked, parked or carried). Click cards to mark them, then copy your picks below.</p>
<div class="tabs"><button class="on" data-pane="b3">Batch 3 · factions, tech tiers, refits (new)</button><button data-pane="b2">Batch 2 · line B per direction</button></div>
<div class="paint" id="paint"><span>Paint:</span><button class="on" data-p="pdd">DRYDOCK liveries</button><button data-p="pnc">NOCTURNE liveries</button>
<span>Shapes belong to the faction; the art direction changes the paint (and, in 3D, the surface detail).</span></div></header>
<div class="pane on" id="b3">{{BATCH3}}</div>
<div class="pane" id="b2"><section><h2>Batch 2: form follows function</h2><p class="blurb">Every ship has one job it is best at, and its outline comes from that job: armour where it takes hits, guns where they bear,
hangars, tanks, radiators and engines where the work needs them. Each line shares a doctrine, a build grammar and a paint pattern. Pick one per class.</p></section>
{{SECTIONS}}</div>
<div class="picks"><div><b>Your picks:</b> <code id="out">none yet</code><button id="copy">Copy</button></div></div>
<script>
const picks={},marks=new Set();
function show(){const a=Object.entries(picks).map(([k,v])=>k+": "+v);const b=[...marks];
  const t=[...(b.length?["Batch 3: "+b.join(", ")]:[]),...a].join(" · ");document.getElementById('out').textContent=t||'none yet';}
document.querySelectorAll('.card').forEach(c=>c.addEventListener('click',()=>{
  if(c.classList.contains('card3')){const v=c.dataset.name;if(marks.has(v)){marks.delete(v);c.classList.remove('on')}else{marks.add(v);c.classList.add('on')}show();return;}
  const k=(c.dataset.dir==='drydock'?'DRYDOCK ':'NOCTURNE ')+c.dataset.cls.toLowerCase();
  const v=c.dataset.opt+' '+c.dataset.name;
  document.querySelectorAll(`.card[data-dir="${c.dataset.dir}"][data-cls="${c.dataset.cls}"]`).forEach(x=>x.classList.remove('on'));
  if(picks[k]===v){delete picks[k];}else{picks[k]=v;c.classList.add('on');}
  show();}));
document.querySelectorAll('.tabs button').forEach(b=>b.addEventListener('click',()=>{
  document.querySelectorAll('.tabs button').forEach(x=>x.classList.toggle('on',x===b));
  document.querySelectorAll('.pane').forEach(p=>p.classList.toggle('on',p.id===b.dataset.pane));
  document.getElementById('paint').style.display=b.dataset.pane==='b3'?'':'none';}));
document.querySelectorAll('#paint button').forEach(b=>b.addEventListener('click',()=>{
  document.querySelectorAll('#paint button').forEach(x=>x.classList.toggle('on',x===b));
  document.body.classList.remove('pdd','pnc');document.body.classList.add(b.dataset.p);}));
document.getElementById('copy').addEventListener('click',()=>{try{navigator.clipboard.writeText(document.getElementById('out').textContent)}catch(e){}});
</script></body></html>
"""

if __name__ == "__main__":
    build()
