"""Round 4, step 1, batch 3: the owner's factions as Starsector-style ship lines, with tech tiers, refits and asymmetry.

Factions and their lore come from the owner's legacy content (Import/Legacy/content/Factions) and the faction colours in
docs/design/DESIGN-SYSTEM.md (one livery per art direction). Ship names, house names and stories are proposals.
Imported by silhouettes.py, which writes the page. Units are metres; x right (starboard), y forward (bow at the top)."""

import math
from contextlib import contextmanager

from silhouettes import Sp, arcpts, F


# ======================================================================================================================
# Drawing helpers
# ======================================================================================================================

def rot(pts, ang, cx=0.0, cy=0.0):
    a = math.radians(ang)
    ca, sa = math.cos(a), math.sin(a)
    return [(cx + (x - cx) * ca - (y - cy) * sa, cy + (x - cx) * sa + (y - cy) * ca) for x, y in pts]


class S3(Sp):
    """Sp with a faction scope (CSS variables come from the f-<faction> class), sub-scopes and an optional rigid transform."""

    def __init__(self, fac):
        super().__init__()
        self.fac = fac
        self.xf = None

    def _tp(self, x, y):
        if not self.xf:
            return x, y
        px, py, ang, dx, dy = self.xf
        (x, y), = rot([(x, y)], ang, px, py)
        return x + dx, y + dy

    @contextmanager
    def scope(self, fac):
        self.els.append(f'<g class="f-{fac}">')
        yield
        self.els.append("</g>")

    @contextmanager
    def moved(self, px, py, ang, dx=0.0, dy=0.0):
        old, self.xf = self.xf, (px, py, ang, dx, dy)
        yield
        self.xf = old

    def poly(self, pts, c="base", mirror=False):
        self.els.append(f'<path class="{c}" d="{self._d([self._tp(x, y) for x, y in pts])}"/>')
        if mirror:
            self.poly([(-x, y) for x, y in pts][::-1], c)

    def circ(self, x, y, r, c="base", mirror=False):
        X, Y = self._tp(x, y)
        self.pts += [(X - r, Y - r), (X + r, Y + r)]
        self.els.append(f'<circle class="{c}" cx="{F(X)}" cy="{F(-Y)}" r="{F(r)}"/>')
        if mirror and x:
            self.circ(-x, y, r, c)

    def ring(self, x, y, r0, r1, c="base"):
        X, Y = self._tp(x, y)
        Sp.ring(self, X, Y, r0, r1, c)

    def line(self, pts, c="seam", mirror=False):
        self.els.append(f'<polyline class="{c}" points="' + " ".join(f"{F(X)},{F(-Y)}" for X, Y in (self._tp(x, y) for x, y in pts)) + '"/>')
        if mirror:
            self.line([(-x, y) for x, y in pts], c)

    # --- vocabulary ---------------------------------------------------------------------------------------------------
    def slab(self, pts, c="plate"):
        self.poly(pts, c)
        self.line(pts + [pts[0]], "seam")

    def ticks(self, x, y, n, step=1.1, h=1.6, ang=0.0):
        for k in range(n):
            p0 = (x + k * step * math.cos(math.radians(ang)), y + k * step * math.sin(math.radians(ang)))
            self.line([p0, (p0[0] - h * math.sin(math.radians(ang)), p0[1] + h * math.cos(math.radians(ang)))], "tick")

    def engine2(self, x, y, w, l, ang=270.0, c="frame"):
        """Nozzle at (x, y) whose exhaust points along `ang` degrees (270 = aft)."""
        body = [(x - w / 2, y), (x + w / 2, y), (x + w / 2, y + l), (x - w / 2, y + l)]
        flame = [(x - w * 0.42, y), (x + w * 0.42, y), (x + w * 0.3, y - w * 0.55), (x - w * 0.3, y - w * 0.55)]
        self.poly(rot(body, ang - 270, x, y), c)
        self.poly(rot(flame, ang - 270, x, y), "glow")

    def slot(self, x0, x1, y, depth=1.4):
        """A flat high-tech slot engine across x0..x1 with its exhaust face at y."""
        self.poly([(x0, y), (x1, y), (x1 - depth * 0.3, y - depth), (x0 + depth * 0.3, y - depth)], "glow")

    def containers(self, x0, y0, x1, y1, nx, ny, seed=0, skip=(), gap=0.35):
        dx, dy = (x1 - x0) / nx, (y1 - y0) / ny
        for i in range(nx):
            for j in range(ny):
                if (i, j) in skip:
                    continue
                k = (i * 7 + j * 13 + seed * 5 + (i * j) % 3) % 4
                self.poly([(x0 + i * dx + gap, y0 + j * dy + gap), (x0 + (i + 1) * dx - gap, y0 + j * dy + gap),
                           (x0 + (i + 1) * dx - gap, y0 + (j + 1) * dy - gap), (x0 + i * dx + gap, y0 + (j + 1) * dy - gap)], f"c{k + 1}")

    def truss(self, p0, p1, w, n=None):
        dx, dy = p1[0] - p0[0], p1[1] - p0[1]
        ln = math.hypot(dx, dy) or 1.0
        nx, ny = -dy / ln * w / 2, dx / ln * w / 2
        for sgn in (-1, 1):
            self.taper([(p0[0] + sgn * nx, p0[1] + sgn * ny), (p1[0] + sgn * nx, p1[1] + sgn * ny)], w * 0.16, "frame")
        n = n or max(2, int(ln / w))
        zz = [(p0[0] + dx * t / n + (nx if t % 2 else -nx), p0[1] + dy * t / n + (ny if t % 2 else -ny)) for t in range(n + 1)]
        self.line(zz, "truss")

    def ghost(self, x, y, ang, k=1.0):
        """A docked CORSAC-sized ship, drawn pale."""
        pts = [(-2.4, -4.5), (2.4, -4.5), (3, 2), (1.6, 5.6), (-0.6, 6.2), (-3.4, 3), (-3.6, -2)]
        self.poly(rot([(x + px * k, y + py * k) for px, py in pts], ang - 90, x, y), "ghost")

    def chevron(self, x, y, w, h, t, c="accent"):
        self.poly([(x - w / 2, y - h), (x, y), (x + w / 2, y - h), (x + w / 2, y - h + t), (x, y + t), (x - w / 2, y - h + t)], c)

    def embed(self, other, x, y, ang):
        """Another ship's drawing placed at (x, y), turned `ang` degrees anticlockwise, in its own faction scope."""
        self.els.append(f'<g class="f-{other.fac}" transform="translate({F(x)},{F(-y)}) rotate({F(-ang)})">')
        self.els += other.els
        self.els.append("</g>")
        x0, y0, x1, y1 = other.bbox()
        self.pts += [(px + x, py + y) for px, py in rot([(x0, y0), (x1, y0), (x1, y1), (x0, y1)], ang)]

    def wrapped(self):
        return [f'<g class="f-{self.fac}">'] + self.els + ["</g>"]


def S(fac):
    return S3(fac)


# ======================================================================================================================
# ARK EXPEDITION: the humans, strangers in this galaxy. Earth patterns printed by the ARK's fabricators, built to be
# repaired by their own crews with whatever they find. Nav lights: red to port, green to starboard
# ======================================================================================================================

def nav(s, xp, yp, xs, ys):
    s.circ(xp, yp, 0.7, "navr")
    s.circ(xs, ys, 0.7, "navg")


def ark_petrel(v="base"):
    s = S("ark")
    if v == "m12":
        s.taper([(0, 0), (0, 8), (0, 30), (0, 40)], [8, 9.4, 9.4, 4], "base")
        for y in (10, 20, 30):
            s.line([(-4.7, y), (4.7, y)], "seam")
        s.circ(0, 36, 3.6, "cab")
        s.taper([(-4.6, 24), (-13, 33)], [1.2, 1.0], "frame")
        s.circ(-14, 34.4, 2.6, "frame")
        s.oct(4.6, 9, 8, 20, 1.6, "plate")
        s.engine(0, 0, 4.6, 3)
        with s.scope("silence"):
            pts = [(5.2, 20), (9.6, 24), (8.6, 31), (3.6, 36), (1.8, 30), (3.2, 25)]
            s.poly(pts, "vglow")
            s.poly(pts, "void")
        s.line([(-4.6, 4), (-4.6, 18)], "pipe")
        return s, dict(name="PETREL · MISSION 12", desig="Earlier Ark pattern, found adrift", tier="stranger",
                       story="The same job from an earlier Ark: round modules, one engine, no turret. Its logs are older than they should be, "
                             "and a bite out of its starboard bow is simply gone, not broken. Salvageable, if you dare.",
                       plus=["Older pattern parts the ARK can no longer print", "Survey logs from Mission 12"],
                       minus=["No guns", "Silence damage: that part of the hull is gone, not broken"],
                       stats=(1, 1, 0, 2, 0, 1, 0))
    s.oct(-3.6, 0, 3.6, 33, 1.0, "base")
    for y in (8, 15, 22):
        s.line([(-3.6, y), (3.6, y)], "seam")
    s.oct(-3.4, 30, 3.4, 41, 1.8, "cab")
    s.oct(-3.4, 35.4, 3.4, 37, 0, "frame")
    s.windows(0, 32, 33, 1.3)
    s.truss((-3.6, 26), (-15, 38), 1.8)
    s.circ(-16.2, 39.2, 2.6, "frame")
    s.circ(-16.2, 39.2, 1.9, "cab")
    s.line([(-17.8, 39.2), (-14.6, 39.2)], "seam")
    s.slab([(-3.6, 4), (-11.4, 7), (-11.4, 13), (-3.6, 13)], "rad")
    for y in (8.4, 10.4):
        s.line([(-10.8, y), (-4, y)], "seam")
    for (y0, y1) in ((8, 17), (18.4, 27.4)):
        s.oct(3.6, y0, 8.4, y1, 2, "accent")
        s.hazard(3.8, y1 - 1.6, 8.2, y1 - 0.4)
    s.turret(0, 18.6, 1.5, 2)
    s.engine(0, 0, 4.4, 3)
    s.engine(-2.9, 2, 1.6, 1.2)
    s.engine(2.9, 2, 1.6, 1.2)
    nav(s, -3.6, 30.6, 3.6, 30.6)
    if v == "native":
        with s.scope("vestari"):
            s.poly([(-16.2, 44), (-14.2, 41.4), (-16.2, 38.8), (-18.2, 41.4)], "plate")
            s.circ(-16.2, 41.4, 0.7, "laser")
        with s.scope("khuchad"):
            s.slab([(-3.6, 14), (-8.6, 15), (-9, 27), (-3.6, 29)])
            s.ticks(-8.2, 17, 2, 1.2, 1.6, 90)
        with s.scope("alliance"):
            s.oct(3.6, 18.4, 9, 27.4, 0.6, "plate")
            s.containers(4, 18.8, 8.6, 27, 2, 2, seed=3)
        s.line([(-3.6, 14), (-3.6, 29)], "weld")
        s.line([(3.6, 18.4), (3.6, 27.4)], "weld")
        return s, dict(name="PETREL · GONE NATIVE", desig="Field refit with clan parts", tier="stranger",
                       story="The expedition can't make shields like the Vestari or armour like the Khuchad, so it takes them. A Vestari emitter on the sensor boom, "
                             "a Khuchad slab bolted to port, an Alliance cargo module where a fuel drum was. It flies like a PETREL and confuses everyone who scans it.",
                       plus=["A Vestari shield emitter: a real shield at last", "Khuchad armour slab on the port side", "An Alliance cargo module"],
                       minus=["One fuel drum gone: shorter range", "Nobody's spare parts fit: repairs are slow", "Every clan reads it as a thief"],
                       stats=(2, 3, 3, 3, 2, 2, 0))
    return s, dict(
        name="PETREL", cls="Frigate", desig="PETREL-class pathfinder · ARK pattern 7", tier="stranger",
        story="First into every unknown system. A strangers' ship has no charts, so the PETREL makes them: a magnetometer boom to port, "
              "drop tanks of fuel to starboard, one turret, and a radiator wing so it can sit silent for days and listen. "
              "Lanes open through what you know, and this is the ship that finds out.",
        fit="1 twin autocannon turret · survey boom · 2 drop tanks",
        system="Listen: goes cold (engines and radiator off) and reads everything in range",
        built="Printed pattern (the ARK can rebuild it from scratch) · Crew-repairable · No shield emitter (the expedition can't make one yet)",
        asym="Survey boom and radiator to port, fuel drums to starboard.",
        weak="Any warship. It survives by not being seen.",
        variants="PETREL · Mission 12 (older Ark pattern, found adrift) · PETREL · Gone native (clan parts bolted on): both in the refit bay",
        stats=(2, 1, 0, 4, 1, 2, 0))


def ark_gardener():
    s = S("ark")
    s.oct(-2.4, -4, 2.4, 96, 0.6, "frame")
    s.oct(-7, 72, 7, 98, 4, "cab")
    s.hazard(-7, 86, 7, 87.6)
    s.windows(0, 76, 84, 1.8)
    for sx in (-1, 1):
        s.taper([(sx * 5, 96), (sx * 9, 104), (sx * 6, 108)], [1.2, 1.0, 0.8], "frame")
    s.oct(-11, 38, 11, 68, 2.4, "plate")
    s.oct(-8, 46, 8, 49, 0, "accent")
    s.oct(-8, 56, 8, 59, 0, "accent")
    for x in (-6, 0, 6):
        s.oct(x - 1, 40, x + 1, 44, 0.3, "win")
    for y in (8, 20, 32):
        s.oct(2.4, y, 15, y + 10.4, 1, "frame")
        s.oct(3.4, y + 1, 14, y + 9.4, 0.8, "ore")
        s.circ(8.6, y + 5.2, 2.2, "crust")
    s.slab([(-2.4, 6), (-20, 4), (-21, 32), (-2.4, 34)], "rad")
    for y in range(9, 32, 4):
        s.line([(-20, y), (-3, y)], "seam")
    s.oct(-12, 64, -2.4, 74, 1.6, "base")
    s.oct(-11, 68, -4, 73, 1, "cab")
    s.turret(-7, 58, 1.6, 2, ang=180)
    s.turret(0, 92, 1.4, 1)
    s.oct(-8, -10, 8, 4, 2.4, "base")
    s.engine(-3.6, -10, 5, 3.4)
    s.engine(3.6, -10, 5, 3.4)
    nav(s, -7, 96, 7, 96)
    return s, dict(
        name="GARDENER", cls="Cargo ship", desig="GARDENER-class fabricator tender · ARK pattern 2", tier="stranger",
        story="Ore in, blocks out. The GARDENER takes raw rock in its hoppers and prints hull blocks for the fleet, which is how a few strangers keep ships "
              "flying a lifetime from home. The bow vault holds the expedition's seed library. Its name comes from an old Ark AI that still asks if it is safe to plant yet.",
        fit="2 light turrets · fabricator (ore to blocks) · seed vault · 2 manipulator arms",
        system="Print: rebuilds destroyed blocks on a nearby ship from its ore stock",
        built="Printed pattern · Crew-repairable · Civilian frame",
        asym="Ore hoppers to starboard, radiator wing and bridge to port.",
        weak="Slow and soft. Losing it costs the expedition more than any warship.",
        variants="GARDENER · Harvest: hoppers swapped for a mining laser",
        stats=(3, 1, 0, 1, 1, 4, 1))


def ark_icebreaker():
    s = S("ark")
    s.poly([(-9, 2), (9, 2), (11, 14), (11, 40), (15, 52), (17, 74), (15, 90), (-15, 90), (-17, 74), (-15, 52), (-11, 40), (-11, 14)], "base")
    for (x, y) in ((11, 24), (15, 52), (17, 74)):
        s.line([(-x, y), (x, y)], "seam")
    for sx in (-1, 1):
        s.line([(sx * 11, 40), (sx * 15, 52)], "seam")
    s.oct(-2.2, 2, 2.2, 124, 0.6, "frame")
    prow = [[(-15, 88), (15, 88), (12, 98), (0, 112), (-12, 98)], [(-12, 98), (0, 112), (12, 98), (8, 108), (0, 120), (-8, 108)],
            [(-8, 108), (0, 120), (8, 108), (4, 118), (0, 127), (-4, 118)]]
    for i, pr in enumerate(prow):
        s.slab(pr, "plate")
    s.poly([(-12.4, 97), (0, 111), (0, 113.4), (-12.4, 99.4)], "haz")
    s.poly([(12.4, 97), (0, 111), (0, 113.4), (12.4, 99.4)], "haz")
    for (y0, y1) in ((16, 28), (29, 40)):
        s.slab([(-11, y0), (-15.6, y0 + 1), (-15.6, y1 - 1), (-11, y1)], "plate")
    s.oct(15, 54, 20, 70, 1.2, "frame")
    s.taper([(17.6, 69), (17.6, 78)], [1.2, 0.8], "gun")
    s.poly([(16.6, 78), (18.6, 78), (17.6, 81)], "accent")
    s.oct(-6, 10, 6, 22, 2, "cab")
    s.windows(0, 12, 20, 1.6)
    s.turret(-9, 70, 2.4, 2, ang=180)
    s.turret(9, 34, 2.4, 2, ang=0)
    s.turret(0, 52, 2.8, 3)
    for x in (-9, -3, 3, 9):
        s.engine(x, 0, 5, 3.6)
    s.engine2(-16.4, 80, 3, 2.4, 90)
    s.engine2(13.6, 60, 3, 2.4, 90)
    nav(s, -15, 87, 15, 87)
    return s, dict(
        name="ICEBREAKER", cls="Cruiser", desig="ICEBREAKER-class ram cruiser · ARK pattern 11", tier="stranger",
        story="Built to break ice rings for water, it taught the expedition its first lesson here: when your guns are worse than everyone else's, use mass. "
              "Three replaceable prow plates on a keel beam that runs the full length, the bridge as far aft as it goes, turrets set back from the bow, and reverse thrusters to pull free after a hit.",
        fit="1 dorsal triple · 2 twin turrets set back from the bow · harpoon winch (starboard) · 3 replaceable prow plates",
        system="Full burn: +80% thrust for 4 s, then the reverse thrusters fire to pull it out",
        built="Printed pattern · Keel beam (impacts travel down the spine, not through the hull) · Spare prow plates carried on the port flank",
        asym="Spare prow plates to port, harpoon winch to starboard.",
        weak="Anything that turns faster or keeps its distance. Between rams it is an average cruiser with poor guns.",
        variants="ICEBREAKER · Polar: harpoon and winch for towing ice instead of fighting",
        stats=(4, 4, 1, 3, 2, 1, 5))


def ark_holdfast():
    s = S("ark")
    s.poly([(-40, 0), (2, 0), (2, 238), (-12, 238), (-20, 230), (-46, 230), (-54, 214), (-54, 188), (-62, 182), (-62, 136), (-54, 130),
            (-54, 92), (-62, 86), (-62, 40), (-54, 34), (-50, 12)], "base")
    for y in range(30, 230, 30):
        s.line([(-54, y), (2, y)], "seam")
    s.line([(-28, 4), (-28, 228)], "seam")
    s.oct(-40, 232, -8, 254, 6, "cab")
    s.windows(-24, 236, 250, 2.8)
    s.oct(-54, 236, -40, 240, 0, "accent")
    for (y0, y1) in ((40, 90), (130, 180)):
        s.slab([(-62, y0), (-84, y0 + 6), (-84, y1 - 6), (-62, y1)], "rad")
        for y in range(y0 + 8, y1 - 4, 6):
            s.line([(-82, y), (-64, y)], "seam")
    s.oct(-44, 96, -10, 126, 4, "plate")
    s.oct(-38, 106, -16, 116, 2, "accent")
    for sx_x in (8, 74):
        s.truss((sx_x, 4), (sx_x, 246), 7)
    for y in (6, 82, 160, 242):
        s.truss((2, y), (78, y), 5)
    s.oct(2, 118, 78, 124, 0, "frame")
    s.hazard(2, 120, 78, 122)
    s.oct(36, 116, 44, 126, 1, "plate")
    with s.scope("ark"):
        hull = [(33, 30), (51, 30), (53, 100), (57, 150), (55, 186), (42, 216), (29, 186), (27, 150), (31, 100)]
        s.poly(hull, "ghost")
        for y in (60, 90, 150, 180):
            s.line([(30, y), (56, y)], "truss")
    for (x, y) in ((-40, 210), (-14, 210), (-40, 30), (-14, 60), (78, 246), (8, 246), (78, 6), (-30, 150)):
        s.turret(x, y, 3.4, 2)
    s.oct(-50, -10, -2, 6, 3, "plate")
    for x in (-42, -30, -18, -8):
        s.engine(x, -10, 6.6, 4)
    for x in (8, 74):
        s.engine(x, -2, 6, 4)
    nav(s, -84, 174, 80, 246)
    return s, dict(
        name="HOLDFAST", cls="Dreadnought", desig="HOLDFAST-class mobile drydock · ARK pattern 1", tier="stranger",
        story="The expedition's only capital ship is a shipyard. Half of it is hull (habitat, fabricators, reactor, radiators), "
              "half is an open cradle where a new ship is printed block by block, here an ICEBREAKER half-built. Strangers can't count on anyone else's yards, so they brought their own.",
        fit="8 twin turrets on the hull and cradle corners · gantry crane · a ship-sized build cradle",
        system="Rush build: finishes the ship in the cradle, or repairs any ship docked in it",
        built="Printed pattern · Crew-repairable · Yard (builds and repairs ships between fights)",
        asym="Solid hull to port, open build cradle to starboard.",
        weak="The cradle side is open frame, and anything in it is unfinished. Hit it while it's building.",
        variants="Every HOLDFAST is different, depending on what is in the cradle",
        stats=(5, 3, 1, 1, 3, 4, 2))


# ======================================================================================================================
# ARK WAR LINE: the bulk of the expedition's fighting force. Four printed patterns built to fight as one team: a HALO marks
# a target and every linked gun fires on it at once. Shared build rule: heat to port, ammunition to starboard
# ======================================================================================================================

def aw_squall():
    s = S("arkw")
    s.poly([(-5, 0), (5, 0), (6, 6), (6, 28), (4.6, 33), (-4.6, 33), (-6, 28), (-6, 6)], "base")
    for (y0, y1, w0, w1) in ((26, 31, 6.6, 6), (31, 36, 6, 4.6), (36, 40, 4.6, 3)):
        s.slab([(-w0, y0), (w0, y0), (w1, y1), (-w1, y1)], "plate")
    s.slab([(-6, 7), (-15, 10), (-15, 21), (-6, 24)], "rad")
    for y in (12, 15, 18):
        s.line([(-14.4, y), (-6.6, y + 0.6)], "seam")
    s.oct(6, 8, 11, 24, 1.6, "accent")
    s.hazard(6.3, 21.4, 10.7, 22.8)
    s.line([(6, 15), (11, 15)], "seam")
    s.oct(-6, 10.4, 6, 11.8, 0, "band")
    s.oct(-5.4, 15, -2, 23, 1, "cab")
    s.windows(-3.7, 16.4, 21.6, 1.3)
    s.oct(-1.3, 6, 1.3, 58, 0.3, "frame")
    for y in (40, 44.5, 49, 53.5):
        s.oct(-2.2, y, 2.2, y + 1.4, 0.2, "trim")
    s.turret(3, 4, 1.3, 1, ang=300)
    s.engine(-2.8, 0, 3.6, 2.6)
    s.engine(2.8, 0, 3.6, 2.6)
    s.engine(8.5, 8, 2.4, 1.8)
    nav(s, -15, 20, 11, 23.6)
    return s, dict(
        name="SQUALL", cls="Frigate", desig="SQUALL-class strike frigate · ARK war pattern 21", tier="stranger",
        story="A coilgun longer than the ship, with a frigate built around its breech. The gun is fixed forward, so the whole ship aims it. "
              "SQUALLs fly in pairs: one fires while the other recharges, and when a HALO marks a target both fire with the rest of the team. "
              "The slugs are printed from ore in the magazine on its starboard side, so a SQUALL never runs out while the GARDENERs are working.",
        fit="1 spinal coilgun (fixed forward) · 1 point-defence turret · prow of 3 armour blocks",
        system="Volley: holds its shot until the team fires, then all linked guns hit the marked target at once",
        built="Printed pattern · Fire link (shares targets with every ARK warship in range) · Standard blocks (armour swaps with any ARK hull)",
        asym="Coilgun radiator to port, slug magazine to starboard: the war line's rule, heat to port and ammunition to starboard.",
        weak="Anything on its flank. The gun only points where the ship does, so a fast frigate that stays beside it is safe.",
        variants="SQUALL · Lancer: the coilgun swapped for a torpedo rail",
        stats=(2, 3, 0, 4, 4, 0, 1))


def aw_halo():
    s = S("arkw")
    s.poly([(-7, 0), (7, 0), (8.6, 6), (8.6, 36), (6.4, 40), (6.4, 52), (-6.4, 52), (-6.4, 40), (-8.6, 36), (-8.6, 6)], "base")
    for y in (20, 34):
        s.line([(-8.6, y), (8.6, y)], "seam")
    s.poly([(-12.6, 52), (12.6, 52), (12.6, 60), (8, 64), (-8, 64), (-12.6, 60)], "base")
    s.poly([(-12, 0), (12, 0), (12, 6), (8.6, 9), (-8.6, 9), (-12, 6)], "base")
    s.slab([(-8, 64), (8, 64), (5.6, 72), (-5.6, 72)], "plate")
    s.slab([(-5.6, 72), (5.6, 72), (2.4, 78), (-2.4, 78)], "plate")
    s.slab([(-8.6, 9), (-16, 12), (-16, 30), (-8.6, 33)], "rad")
    for y in (15, 19, 23, 27):
        s.line([(-15.4, y), (-9.2, y)], "seam")
    s.truss((-6.4, 44), (-19, 56), 2.4)
    s.circ(-21.4, 58.4, 6.6, "frame")
    s.circ(-21.4, 58.4, 5.2, "cab")
    s.line([(-25.6, 58.4), (-17.2, 58.4)], "seam")
    s.circ(-21.4, 58.4, 1.1, "frame")
    s.oct(8.6, 12, 14.4, 40, 1.8, "accent")
    s.hazard(8.9, 37, 14.1, 38.6)
    s.oct(-8.6, 35.6, 8.6, 37.2, 0, "band")
    s.oct(-4.4, 38, 4.4, 50, 1.6, "cab")
    s.windows(0, 40, 48, 1.6)
    s.turret(0, 67, 2.4, 2)
    for (x, y, a) in ((-10.4, 57, 150), (10.4, 57, 30), (-4.4, 46, 180), (4.6, 30, 0), (-9.6, 3, 220), (9.6, 3, 320)):
        s.turret(x, y, 1.4, 1, ang=a)
    s.turret(11.5, 24, 2, 2, ang=0)
    s.engine(-3.6, 0, 4.4, 3)
    s.engine(3.6, 0, 4.4, 3)
    nav(s, -16, 28, 14.4, 34)
    return s, dict(
        name="HALO", cls="Destroyer", desig="HALO-class fire-control destroyer · ARK war pattern 24", tier="stranger",
        story="The expedition can't make shields, so the HALO is the team's shield: six point-defence turrets and a flak battery that shoot down missiles and fighters "
              "aimed at anyone nearby. The fire-control dish on its port boom marks one target, and every linked ARK gun in range fires on it together. "
              "Its own guns are light. It punches by making everyone else hit.",
        fit="1 twin coilgun turret · 6 point-defence turrets · 1 flak battery · fire-control dish",
        system="Mark: paints one target; linked ships' volleys on it hit together and land harder",
        built="Printed pattern · Fire link (the hub: the team's targets come from here) · Standard blocks",
        asym="Fire-control dish on a boom to port, flak magazine to starboard.",
        weak="Losing it. Kill the HALO first and the team fires one ship at a time, with no point defence.",
        variants="HALO · Lamp: the dish swapped for a long-range survey array, for escorting PETRELs",
        stats=(3, 2, 0, 3, 2, 0, 0))


def aw_tempest():
    s = S("arkw")
    s.poly([(-11, 0), (11, 0), (12, 10), (12, 46), (15, 50), (15, 68), (20, 74), (20, 100), (13, 108), (-13, 108), (-20, 100), (-20, 74), (-15, 68), (-15, 50), (-12, 46), (-12, 10)], "base")
    for y in (24, 46, 68):
        s.line([(-15, y), (15, y)], "seam")
    for sx in (-1, 1):
        s.line([(sx * 15, 74), (sx * 15, 100)], "seam")
    swapped = {(0, 1), (2, 0)}
    for r, (y0, y1, w0, w1, n) in enumerate(((108, 116, 13, 10.6, 4), (116, 123, 10.6, 7.4, 3), (123, 129, 7.4, 4, 2), (129, 133, 4, 1.6, 1))):
        for i in range(n):
            a0, a1 = -w0 + 2 * w0 * i / n, -w0 + 2 * w0 * (i + 1) / n
            b0, b1 = -w1 + 2 * w1 * i / n, -w1 + 2 * w1 * (i + 1) / n
            s.slab([(a0, y0), (a1, y0), (b1, y1), (b0, y1)], "trim" if (r, i) in swapped else "plate")
    s.slab([(-12, 12), (-26, 16), (-26, 40), (-12, 44)], "rad")
    s.slab([(-15, 50), (-23, 53), (-23, 64), (-15, 67)], "rad")
    for y in (20, 25, 30, 35):
        s.line([(-25.4, y), (-12.6, y)], "seam")
    for y in (56, 61):
        s.line([(-22.4, y), (-15.6, y)], "seam")
    s.oct(12, 14, 19, 62, 2, "accent")
    for y in (30, 46):
        s.line([(12, y), (19, y)], "seam")
    s.hazard(12.3, 59, 18.7, 60.6)
    s.oct(-15, 69, 15, 71, 0, "band")
    s.oct(-6, 26, 6, 42, 2, "cab")
    s.windows(0, 28, 40, 1.6)
    s.turret(0, 97, 4.4, 3)
    s.turret(0, 80, 4.4, 3)
    s.turret(-15, 87, 2.6, 2, ang=125)
    s.turret(15, 87, 2.6, 2, ang=55)
    s.turret(-9, 18, 1.4, 1, ang=200)
    s.turret(9, 68, 1.4, 1, ang=0)
    for x in (-9, -3, 3, 9):
        s.engine(x, 0, 5, 3.6)
    nav(s, -26, 36, 19, 58)
    return s, dict(
        name="TEMPEST", cls="Cruiser", desig="TEMPEST-class line cruiser · ARK war pattern 22", tier="stranger",
        story="The bulk of the fleet, printed in more numbers than anything else. Every gun sits forward of the bridge, so the TEMPEST puts its whole weight on whatever is ahead of it: "
              "two heavy triple coilgun turrets, one firing over the other, and a twin on each shoulder. The prow is printed in standard armour blocks; "
              "two of these are fresh replacements, and any GARDENER can print more.",
        fit="2 heavy triple turrets (superfiring) · 2 twin turrets · 2 point-defence turrets · prow of 10 standard armour blocks",
        system="Hold the line: the prow blocks lock and harden for 5 s; it can't turn while they're locked",
        built="Printed pattern · Fire link · Standard blocks (damaged blocks swap out between fights)",
        asym="Radiators to port, magazine and slug printer to starboard. Two replacement prow blocks still unpainted.",
        weak="Its stern. Every gun faces forward, so a ship that gets behind it faces two point-defence turrets.",
        variants="TEMPEST · Bastion: the shoulder twins swapped for missile racks",
        stats=(4, 4, 0, 2, 4, 0, 1))


def aw_thunderhead():
    s = S("arkw")
    s.poly([(-20, 0), (20, 0), (24, 12), (24, 40), (28, 46), (28, 150), (-28, 150), (-28, 46), (-24, 40), (-24, 12)], "base")
    for y in (40, 70, 100, 128):
        s.line([(-28, y), (28, y)], "seam")
    front = [(-66, 178), (-34, 206), (0, 226), (28, 212), (46, 186)]
    back = [(40, 176), (28, 150), (-28, 150), (-58, 162)]
    s.poly(front + back, "base")
    inner = [(-48, 176), (-26, 196), (0, 210), (20, 200), (32, 182)]
    s.poly(front + inner[::-1], "plate")
    for (a, b) in zip(front, inner):
        s.line([a, b], "seam")
    for f0, f1, i0, i1 in zip(front, front[1:], inner, inner[1:]):
        s.line([((f0[0] + i0[0]) / 2, (f0[1] + i0[1]) / 2), ((f1[0] + i1[0]) / 2, (f1[1] + i1[1]) / 2)], "seam")
    s.poly([(-66, 178), (-34, 206), (-33.2, 203.6), (-64.2, 176.6)], "haz")
    s.slab([(-28, 48), (-56, 56), (-56, 140), (-28, 146)], "rad")
    for y in range(62, 140, 8):
        s.line([(-55, y), (-29, y)], "seam")
    s.oct(28, 50, 40, 146, 4, "accent")
    for y in (82, 114):
        s.line([(28, y), (40, y)], "seam")
    s.hazard(28.5, 142, 39.5, 144)
    s.oct(24, 18, 34, 40, 2, "ore")
    s.oct(-28, 120, 28, 123, 0, "band")
    s.oct(-26, 92, -10, 116, 3, "cab")
    s.windows(-18, 95, 113, 2)
    s.truss((-18, 92), (-18, 80), 2.4)
    s.circ(-18, 76, 4, "frame")
    s.circ(-18, 76, 3, "cab")
    for (y, a) in ((182, 90), (160, 90), (64, 270), (40, 270)):
        s.turret(4, y, 7, 3, ang=a)
    for (x, y, a) in ((-20, 140, 150), (21, 140, 30), (-21, 30, 200), (19, 88, 0)):
        s.turret(x, y, 3, 2, ang=a)
    for (x, y, a) in ((-44, 176, 150), (30, 182, 30), (-10, 8, 240), (12, 8, 300), (34, 46, 0), (-52, 146, 180)):
        s.turret(x, y, 1.6, 1, ang=a)
    for x in (-18, -11, -4, 4, 11, 18):
        s.engine(x, 0, 6, 4)
    nav(s, -66, 179, 46, 187)
    return s, dict(
        name="THUNDERHEAD", cls="Dreadnought", desig="THUNDERHEAD-class battleship · ARK war pattern 20", tier="stranger",
        story="A pattern the clans had never seen: an Earth battleship. Four heavy triple turrets on the centreline, two firing forward and two aft, and in place of the shield "
              "it can't make, a flared armour mantlet 110 m wide across its bow. The mantlet reaches further to port, over the radiators, and the line forms in its shadow: "
              "TEMPESTs at its shoulders, HALOs behind the gaps, SQUALLs on the flanks. It is the anvil the rest of the team hits against.",
        fit="4 heavy triple turrets · 4 twin turrets · 6 point-defence turrets · bow mantlet · slug foundry",
        system="Shelter: the mantlet hardens and the battleship holds course, so ships behind it take no fire from ahead",
        built="Printed pattern · Fire link (the flagship: commands the team's volleys) · Standard blocks · Slug foundry (prints ammunition for the whole team)",
        asym="Mantlet wider to port, over the radiators. Bridge and fire-control tower to port, slug foundry and magazines to starboard.",
        weak="It turns like a station. Anything that comes from the side, or from below the mantlet's edge, faces turrets, not armour.",
        variants="THUNDERHEAD · Breakwater: no aft turrets; a second mantlet for holding a lane open",
        stats=(5, 5, 0, 1, 5, 1, 2))


def aw_team():
    """The fire team at true scale, bow up, enemy ahead. Not a ship card: a doctrine plate."""
    s = S("arkw")
    placed = [(aw_thunderhead, 0, 0), (aw_tempest, -102, 14), (aw_tempest, 94, 14), (aw_halo, -64, -86), (aw_halo, 62, -86),
              (aw_squall, -196, 168), (aw_squall, -172, 150), (aw_squall, 172, 150), (aw_squall, 196, 168)]
    tgt = (0, 470)
    for fn, x, y in placed:
        sh, m = fn()
        if m["name"] == "HALO":
            s.line([(x - 21.4, y + 58.4), (tgt[0], tgt[1] - 74)], "markline")
        else:
            s.line([(x, y + (226 if m["name"] == "THUNDERHEAD" else 132 if m["name"] == "TEMPEST" else 58)), tgt], "fireline")
    for fn, x, y in placed:
        sh, m = fn()
        s.embed(sh, x, y, 0)
    s.embed(kh_argali()[0], tgt[0], tgt[1] + 63, 180)
    s.ring(tgt[0], tgt[1], 74, 78, "accent")
    return s


# ======================================================================================================================
# THE ARK: the expedition's mothership. Station-sized; it crossed the dark between stars and is now home port, shipyard and capital
# ======================================================================================================================

def ark_mothership():
    s = S("ark")
    # keel, running the full length
    s.oct(-36, 140, 36, 1370, 10, "base")
    for y in range(200, 1360, 60):
        s.line([(-36, y), (36, y)], "seam")
    s.oct(-6, 140, 6, 1370, 2, "frame")
    # drive: seven fusion engines on a thrust frame, propellant tanks ahead of it
    s.oct(-112, 20, 112, 150, 14, "plate")
    for y in (60, 100):
        s.line([(-112, y), (112, y)], "seam")
    for x in range(-90, 91, 30):
        s.engine(x, 20, 24, 22)
    for (x0, x1) in ((-106, -62), (-56, -14), (14, 56), (62, 106)):
        s.oct(x0, 160, x1, 300, 16, "accent")
        s.oct(x0, 280, x1, 286, 0, "frame")
    # reactor and radiator wings: port wing whole; starboard wing cut down to print the first fleet, its frame left bare
    s.oct(-62, 300, 62, 384, 10, "plate")
    s.circ(0, 342, 24, "frame")
    s.circ(0, 342, 14, "glow")
    s.slab([(-62, 306), (-330, 330), (-330, 500), (-62, 520)], "rad")
    for x in range(-312, -70, 22):
        s.line([(x, 330 + (x + 330) * -0.09 + 4), (x, 500 + (x + 330) * 0.08 - 4)], "seam")
    s.slab([(62, 306), (170, 316), (170, 424), (62, 438)], "rad")
    for x in range(80, 170, 22):
        s.line([(x, 312), (x, 434)], "seam")
    s.line([(170, 316), (170, 424)], "weld")
    s.truss((170, 330), (330, 340), 10)
    s.truss((170, 410), (330, 400), 10)
    s.truss((330, 330), (330, 410), 10)
    # hangar deck between the reactor and the yards: the PETRELs launch from here
    s.oct(-58, 392, 58, 548, 10, "plate")
    for y in (410, 444, 478, 512):
        s.oct(-50, y, -10, y + 24, 2, "frame")
        s.oct(10, y, 50, y + 24, 2, "frame")
    # foundry to port, fed by a captured asteroid clamped to its flank
    s.oct(-150, 560, -14, 800, 14, "base")
    for y in (620, 680, 740):
        s.line([(-150, y), (-14, y)], "seam")
    s.oct(-120, 650, -60, 710, 8, "glow")
    s.oct(-112, 658, -68, 702, 6, "frame")
    rock = []
    for i in range(30):
        a = 2 * math.pi * i / 30
        r = 112 + 14 * math.sin(3 * a + 0.4) + 9 * math.sin(5 * a + 1.7) + 5 * math.sin(9 * a)
        rock.append((-268 + math.cos(a) * r * 0.86, 690 + math.sin(a) * r))
    s.poly(rock, "ore")
    for (x, y, r) in ((-300, 740, 22), (-250, 620, 14), (-320, 650, 10), (-230, 770, 9), (-290, 700, 7)):
        s.circ(x, y, r, "crust")
    s.poly([(-176, 640), (-210, 600), (-200, 590), (-166, 632)], "frame")
    for y in (650, 672, 694, 716, 738):
        s.line([(-160, y), (-196, y + 6)], "seam")
    s.truss((-150, 600), (-196, 610), 10)
    s.truss((-150, 780), (-200, 768), 10)
    s.oct(-160, 590, -150, 790, 2, "frame")
    # shipyards to starboard: a gantry cradle with a HOLDFAST in it, plus a GARDENER and an ICEBREAKER on pylons
    for x in (100, 300):
        s.truss((x, 530), (x, 860), 12)
    for y in (530, 860):
        s.truss((14, y), (300, y), 12)
    s.embed(ark_holdfast()[0], 202, 562, 0)
    s.truss((300, 640), (318, 640), 6)
    s.embed(ark_gardener()[0], 338, 600, 0)
    s.truss((300, 800), (316, 800), 6)
    s.embed(ark_icebreaker()[0], 338, 740, 0)
    # two habitat drums spinning for gravity, the forward one the seed vault
    for (y0, y1, c) in ((880, 1040, "base"), (1070, 1220, "cab")):
        s.oct(-150, y0, 150, y1, 30, c)
        for x in range(-130, 131, 26):
            s.line([(x, y0 + 4), (x, y1 - 4)], "seam")
        s.oct(-124, (y0 + y1) / 2 - 3, 124, (y0 + y1) / 2 + 3, 0, "frame")
    s.windows(-144, 896, 1024, 8)
    s.windows(144, 896, 1024, 8)
    s.oct(-40, 1040, 40, 1070, 6, "plate")
    s.hazard(-40, 1052, 40, 1058)
    # command block and long-range dishes (the big one to port, aimed back the way they came)
    s.oct(-52, 1240, 52, 1340, 12, "cab")
    s.windows(0, 1252, 1328, 6)
    s.oct(-52, 1286, 52, 1292, 0, "accent")
    s.truss((-52, 1300), (-100, 1300), 10)
    s.circ(-130, 1300, 32, "frame")
    s.circ(-130, 1300, 26, "plate")
    s.circ(-130, 1300, 5, "frame")
    s.truss((52, 1320), (80, 1326), 6)
    s.circ(94, 1328, 15, "frame")
    s.circ(94, 1328, 11, "cab")
    # bow shield: a plug of ice and rock that took the crossing's impacts, worn further on the port side
    s.oct(-30, 1340, 30, 1368, 4, "frame")
    bow = [(-150, 1366), (148, 1366), (160, 1400), (154, 1446), (112, 1484), (40, 1506), (-30, 1502), (-88, 1482), (-124, 1452), (-156, 1404)]
    s.poly(bow, "ore")
    for (x, y, r) in ((-110, 1440, 16), (-60, 1478, 9), (20, 1470, 12), (90, 1450, 8), (-140, 1400, 7), (120, 1410, 10), (-20, 1420, 6)):
        s.circ(x, y, r, "crust")
    s.line([(-150, 1372), (148, 1372)], "seam")
    # point defence and heavy batteries
    for (x, y, a) in ((-150, 960, 180), (150, 960, 0), (-150, 1150, 180), (150, 1150, 0), (-100, 160, 220), (100, 160, 320),
                      (-150, 1380, 140), (150, 1380, 40), (-14, 560, 180), (14, 880, 0), (-62, 330, 180), (62, 330, 0)):
        s.turret(x, y, 6, 1, ang=a)
    for (x, y, a) in ((-44, 1252, 120), (44, 1252, 60), (-140, 800, 200)):
        s.turret(x, y, 9, 3, ang=a)
    s.circ(-330, 415, 6, "navr")
    s.circ(356, 720, 6, "navg")
    return s, dict(
        name="THE ARK", cls="Mothership", desig="A.R.K. · expedition mothership · the only one of its pattern", tier="stranger",
        story="The ship that carried the expedition across the dark between stars, and now its home port, shipyard and capital. It was never meant to fight or land. "
              "From the bow: a plug of ice and rock that took the impacts of the crossing, worn unevenly; the command block and long-range dishes, the largest aimed back the way they came; "
              "two habitat drums that spin for gravity, the forward one holding the seed vault; then the yards. To port the foundry eats a captured asteroid clamped to its flank. "
              "To starboard the gantries print the fleet: a HOLDFAST, a GARDENER and an ICEBREAKER are docked here at true scale. Aft, the reactor and radiators, "
              "and seven fusion engines that will never be lit again at full power. The starboard radiator wing was cut down to print the first PETRELs and has never been replaced.",
        fit="12 point-defence turrets · 3 heavy batteries · foundry · hangar deck · 2 habitat drums · seed vault · shipyard gantries",
        system="Home port: any ARK ship docked here is repaired and rearmed, and the foundry prints new hulls from ore",
        built="The original pattern library (every ARK ship is printed from it) · Shipyard · Foundry · Habitat (the expedition's people live here)",
        asym="Foundry and captured asteroid to port, shipyards to starboard. Full radiator wing to port, the starboard one cut down to bare frame.",
        weak="Everything, if it's caught alone. It is the whole expedition in one hull; the war line exists so that never happens.",
        variants="None. There were other Arks: Mission 12's PETREL is one of the few pieces ever found.",
        stats=(5, 4, 0, 0, 3, 5, 0))


# ======================================================================================================================
# RAMMERS: ramming is a game system (collision impulse = mass x speed, and both ships take the hit)
# ======================================================================================================================

def kh_tarpan():
    s = S("khuchad")
    s.poly([(-5, 0), (5, 0), (6.4, 6), (6, 18), (4, 24), (-4.6, 24), (-6, 16), (-6, 4)], "base")
    plates = [[(-7, 20), (6, 18.6), (8.4, 26), (2.6, 34), (-6, 30)], [(-6, 30), (2.6, 34), (8.4, 26), (9.6, 32), (4.6, 40), (-3, 38)],
              [(-3, 38), (4.6, 40), (9.6, 32), (8.6, 40), (5, 46.4), (0.6, 45)]]
    for p in plates:
        s.slab(p)
    s.line([(9.6, 32), (5, 46.4)], "tick")
    s.ticks(-5, 31, 4, 1.2, 1.6, 30)
    s.oct(-2.6, 8, 2.2, 15, 1, "cab")
    s.turret(0, 3.6, 1.4, 2, ang=270)
    for x in (-3.4, 0.4, 4):
        s.engine(x, 0, 3.4, 2.6)
    s.engine(-7.6, 3, 2.6, 2)
    s.engine(7.6, 3, 2.6, 2)
    return s, dict(
        name="TARPAN", cls="Frigate", desig="TARPAN-class ram frigate · Khuchad Clan", tier="low",
        story="The pack's battering ram: five engines behind three layers of soot plate, with the prow canted to starboard like every Khuchad lance. "
              "It hits first to stagger a target and crack its armour, and the CORSACs pour through the hole. Few TARPAN crews ram twice; the ones who do get painted on the ORDU.",
        fit="1 rear-facing autocannon · triple-layer ram prow",
        system="Charge: +100% thrust for 3 s; the prow hardens for the impact",
        built="Trophy plating · Pack link",
        asym="Prow canted to starboard; armour layers sweep from the port side.",
        weak="Any ship that sidesteps it. It barely turns, and after a ram it is a slow brick with one gun.",
        variants="TARPAN Bannerman: a chief's TARPAN with the clan's colours on the prow",
        stats=(2, 4, 0, 4, 1, 0, 5))


def sc_fireship():
    s = S("alliance")
    s.oct(-2.6, -2, 2.6, 94, 0.6, "frame")
    s.hull([(86, 10), (94, 9), (100, 5.4), (104.4, 1.6)], "base")
    with s.scope("scav"):
        for i, y0 in enumerate(range(24, 84, 12)):
            for sx in (-1, 1):
                x0, x1 = (-14, -2.6) if sx < 0 else (2.6, 14)
                s.oct(x0, y0, x1, y0 + 11.4, 0.4, "plate" if (i + (sx > 0)) % 2 else "base")
                s.line([(x0 + 1, y0 + 2), (x1 - 1, y0 + 9)], "seam")
        s.slab([(-12, 92), (12, 92), (8, 108), (0, 116), (-8, 108)], "plate")
        s.slab([(-8, 108), (0, 116), (8, 108), (4, 116), (0, 121), (-4, 116)], "base")
        s.poly([(-11.6, 93.4), (11.6, 93.4), (11, 96), (-11, 96)], "accent")
        for (x, y) in ((-18, 4), (18, 4), (-10, -10), (10, -10), (0, -12)):
            s.engine2(x, y, 5.4, 4)
        s.truss((-12, 8), (-18, 6), 3)
        s.truss((12, 8), (18, 6), 3)
        s.oct(-12, -6, 12, 8, 2.4, "base")
        s.oct(-3, 70, 3, 78, 1.2, "accent")
        s.circ(0, 74, 2, "cab")
        s.line([(-14, 24), (-14, 84)], "weld")
        s.line([(14, 24), (14, 84)], "weld")
    s.oct(-17, 6, -6, 22, 2.2, "base")
    s.oct(-15.6, 14, -7.4, 21, 1.4, "cab")
    with s.scope("pochteca"):
        s.oct(-8.6, 88, 8.6, 90.6, 0, "band")
    return s, dict(
        name="TALLY (S) FIRESHIP", desig="Scavenger refit · one-way", tier="outlaw",
        story="A TALLY with its containers filled with scrap and slag for mass, three more engines, a welded ram on the bow and a lifeboat pod on top. "
              "The crew aims it, locks the throttle and bails out. Scavengers use them to break blockades and station gates.",
        plus=["Containers packed with scrap: twice the mass, and ram damage is mass × speed", "5 salvaged engines run past their limit", "A crude welded ram"],
        minus=["No guns", "It doesn't come back", "Hull −50%: it only has to last one hit"],
        stats=(1, 2, 0, 3, 0, 0, 5))


# ======================================================================================================================
# KHUCHAD CLAN: low tech, bulky, pack and chief. Armour to port (the shield arm), ram or gun to starboard (the lance arm)
# ======================================================================================================================

def kh_corsac():
    s = S("khuchad")
    s.poly([(-5, 0), (5.4, 0), (7.4, 5), (7, 20), (4.6, 30), (1.2, 37), (-2.4, 36), (-5, 30)], "base")
    for p in ([(-5, 2), (-10.6, 4.4), (-12, 14), (-5, 14.4)], [(-5, 14.4), (-12, 14), (-11.4, 24), (-5, 25)],
              [(-5, 25), (-11.4, 24), (-7.6, 33.6), (-2.6, 37.6)]):
        s.slab(p)
    s.ticks(-10.6, 5.6, 4, 1.15, 1.6, 90)
    s.ticks(-11, 16.4, 3, 1.15, 1.6, 90)
    with s.scope("alliance"):
        s.slab([(2.4, 2.2), (6.8, 3.4), (7.2, 10.6), (2.8, 10)], "plate")
    s.oct(5, 13, 7.8, 28, 0.6, "plate")
    s.line([(5, 18), (7.8, 18)], "seam")
    s.taper([(6.4, 27), (6.4, 41)], [1.1, 0.9], "gun")
    s.oct(5.5, 39.4, 7.3, 42, 0.3, "frame")
    s.poly([(-2.6, 34.4), (3.4, 32), (2, 35.6), (-1.2, 37.2)], "accent")
    s.line([(-5, 8), (5.4, 8)], "seam")
    s.line([(-5, 19), (6.4, 19)], "seam")
    s.oct(-3.2, 21, 1.6, 28, 1.0, "cab")
    s.windows(-0.8, 22.4, 26.4, 1.3)
    s.turret(-0.2, 13, 1.8, 2)
    s.engine(-2.2, 0, 3.4, 2.8)
    s.engine(2.8, 0, 3.4, 2.8)
    s.engine(-8.6, 3.2, 2.8, 2.2)
    return s, dict(
        name="CORSAC", cls="Frigate", desig="CORSAC-class pack raider", tier="low",
        story="The ship every Khuchad warrior earns first. Alone it is nothing: a slab of armour, one heavy cannon and three engines. "
              "In a pack of eight it chews a cruiser's armour off from the shield side while the chief closes in.",
        fit="1 heavy cannon (fixed hardpoint, starboard) · 1 twin autocannon turret",
        system="War-burn: +50% thrust for 6 s to close the distance",
        built="Trophy plating · Pack link",
        asym="Lamellar shield slab on port; cannon offset to starboard; an Alliance plate welded on aft as a trophy.",
        weak="Beams and flak at long range. Anything that reaches its starboard side.",
        variants="CORSAC Bannerman: pack leader, a relay mast in place of the trophy plate · CORSAC (S): Scavenger copy, slab cut off, faster and flimsier",
        stats=(2, 3, 1, 4, 2, 0))


def kh_kulan():
    s = S("khuchad")
    # the tow train: whatever the clan seized, linked by cable
    s.line([(0, 63), (0.6, 57)], "cable")
    s.line([(0.4, 43.6), (1.4, 41)], "cable")
    s.line([(1.6, 27.4), (-0.4, 25.6)], "cable")
    s.line([(-0.6, 12.6), (0.2, 9.4)], "cable")
    with s.scope("alliance"):
        s.oct(-6.6, 43.6, 7.4, 57, 0.6, "frame")
        s.containers(-6.2, 44, 7, 56.6, 3, 2, seed=2)
    s.circ(1.6, 34.4, 7, "frame")
    s.circ(1.6, 34.4, 6.2, "ore")
    for (x, y, r) in ((-0.6, 36.4, 1.8), (3.6, 32, 1.4), (2.8, 37.8, 1.0)):
        s.circ(x, y, r, "crust")
    with s.scope("vestari"):
        s.poly([(-6.6, 13), (3.6, 12.2), (6.4, 18.2), (3.4, 25.4), (-1.2, 26.4), (-7.2, 21)], "base")
        s.poly([(-2.2, 13), (0.4, 12.8), (1, 25.8), (-1.4, 26.1)], "plate")
        s.line([(3.2, 13.4), (5.4, 18.4), (2.8, 24.4)], "lline")
    s.line([(-6.8, 14), (-3, 18.6), (-7.0, 20.6)], "seam")
    s.oct(-4.6, -1, 4.6, 9.4, 2.2, "cab")
    for y in (1.6, 4.2, 6.8):
        s.line([(-4.4, y), (4.4, y)], "seam")
    # the head: a brute tug with canted engines so the exhaust clears the train
    s.oct(-8.5, 63, 8.5, 95, 2.6, "base")
    s.slab([(-8.5, 66), (-17, 63.4), (-18.6, 75), (-15, 83), (-8.5, 84)])
    s.ticks(-16.6, 69, 4, 1.1, 1.6, 90)
    s.poly([(8.5, 70), (14, 68), (15.4, 76.6), (8.5, 79)], "base")
    s.engine2(-15.6, 64.2, 4.4, 3, 235)
    s.engine2(-9.8, 63.2, 3.4, 2.6, 255)
    s.engine2(13.2, 68.6, 3.6, 2.6, 300)
    s.oct(-2.2, 59.6, 2.2, 63.4, 0.5, "frame")
    s.slab([(-7, 95), (7, 95), (4.6, 102.6), (-4.6, 102.6)])
    s.poly([(-4.6, 101.6), (4.6, 101.6), (4.2, 102.8), (-4.2, 102.8)], "accent")
    s.oct(-3.2, 80, 3.2, 88.6, 1.2, "cab")
    s.windows(0, 82, 87, 1.4)
    s.turret(0, 70.4, 1.9, 2, ang=270)
    s.turret(5, 90.4, 1.5, 2)
    return s, dict(
        name="KULAN", cls="Cargo ship", desig="KULAN-class plunder barge", tier="low",
        story="The Khuchad don't build cargo holds; they take them. A KULAN is a brute tug that drags home whatever the clan seized "
              "(containers, ore pods, chunks of hull) in a cable train behind it. Its engines are canted outward so the exhaust clears the train.",
        fit="2 twin autocannon turrets, one facing aft to guard the train · ram bow",
        system="Cut loose: drops the whole train and doubles its speed",
        built="Tow rig (its cargo is whatever it caught) · Trophy plating",
        asym="Armoured port engine pod, bare starboard pod, and a train that is different every trip.",
        weak="Slow and clumsy while towing. Cut the cable and it has nothing to show.",
        variants="KULAN Ram-barge: train dropped, a second ram welded on, used to breach stations",
        stats=(3, 3, 1, 1, 1, 5))


def kh_argali():
    s = S("khuchad")
    s.hull([(0, 13), (10, 15), (70, 15), (90, 11.5), (104, 6.5), (110, 2)], "base", cx=2)
    xo = lambda y: -26 + 10.5 * ((y - 54) / 50) ** 2
    bands = [(3, 19), (19, 35), (35, 51), (51, 67), (67, 83), (83, 99)]
    for i, (y0, y1) in enumerate(bands):
        s.slab([(-12.4, y0), (xo(y0), y0 + 1.4), (xo(y1), y1), (-12.4, y1 + 1.4)])
        s.ticks(xo(y0) + 1.2, y0 + 3.6, 2 + i % 3, 1.2, 1.7, 90 - 6)
    s.slab([(-12.4, 99), (-15.6, 100.2), (-10.6, 111), (-4.4, 113.6), (-2.6, 106)])
    s.slab([(6, 96), (15.4, 97.6), (19.6, 111), (13.6, 126), (7.4, 119), (4, 105)])
    s.line([(19.6, 111), (13.6, 126)], "tick")
    s.circ(9.6, 104.6, 1.9, "cab")
    s.circ(12.6, 111.2, 1.6, "cab")
    for y in (28, 50, 72):
        s.oct(15, y - 6.5, 23, y + 6.5, 2, "base")
        s.turret(19.6, y, 3.1, 2, ang=60)
    s.turret(2, 86, 3.4, 3)
    s.turret(2, 62, 3.4, 3)
    s.oct(-3.6, 38, 7.4, 49, 1.6, "cab")
    s.windows(2, 40, 47, 1.6)
    s.poly([(-2.4, 96), (9, 96), (8, 99), (-1.6, 99.4)], "accent")
    for x in (-8, -2, 4, 10):
        s.engine(x, 0, 4.6, 3.4)
    s.engine(-20.2, 5.6, 3.8, 2.6)
    return s, dict(
        name="ARGALI", cls="Cruiser", desig="ARGALI-class breaching cruiser", tier="low",
        story="Leads every Khuchad charge. The ram sits off-centre to starboard so the boarding tubes behind it meet an enemy's flank, "
              "and the whole port side is a lamellar wall of armour slabs. It rams, clamps on, sends its boarders across and tows the prize home.",
        fit="3 heavy twin cannons on starboard sponsons · 2 dorsal triple turrets · boarding ram with 2 tubes",
        system="Brace: armour hardens for 5 s before an impact",
        built="Trophy plating · Pack link · Forward ward (narrow shield, bow only)",
        asym="Armour wall to port; ram and gun sponsons to starboard. The open side is its weakness by design.",
        weak="Its open starboard side: a frigate pack that flanks right will gut it. Slow to turn.",
        variants="ARGALI Chieftain: banner mast, more boarders · the Scavenger King's welded-on bow came from one",
        stats=(4, 5, 1, 2, 3, 1))


def kh_ordu():
    s = S("khuchad")
    # the body: a fortress with a forward "jaw" where the pack docks
    body = [(-40, 0), (-46, 24), (-60, 30), (-62, 70), (-74, 80), (-74, 150), (-66, 196), (-56, 236), (-40, 252), (-22, 244),
            (-26, 172), (26, 172), (22, 246), (40, 258), (58, 240), (68, 196), (74, 150), (74, 80), (62, 70), (60, 30), (46, 24), (40, 0)]
    s.poly(body, "base")
    for (x0, x1, y) in ((-60, 60, 30), (-62, 62, 70), (-74, 74, 110), (-74, 74, 150)):
        s.line([(x0, y), (x1, y)], "seam")
    for x in (-34, 34):
        s.line([(x, 4), (x, 168)], "seam")
    for sx in (-1, 1):
        s.slab([(sx * 60, 34), (sx * 66, 38), (sx * 66, 62), (sx * 60, 66)], "plate")
    s.oct(-42, -8, 42, 14, 5, "plate")
    for x in range(-35, 36, 10):
        s.engine(x, -8, 7.4, 4.6)
    s.engine(-54, 26, 6, 4)
    s.engine(54, 26, 6, 4)
    # the pack in its cradles along the jaw
    for sx in (-1, 1):
        for y in (184, 214):
            s.oct(sx * 26 - (5 if sx > 0 else 0), y - 5, sx * 26 + (5 if sx < 0 else 0), y + 5, 1.2, "frame")
        s.ghost(sx * 12.5, 192, 90, 3.0)
    s.ghost(0, 226, 90, 3.0)
    # port: lamellar trophy wall, with the whole bow of an Alliance cruiser welded on
    for i, (y0, y1) in enumerate(((80, 104), (104, 128))):
        s.slab([(-74, y0), (-86, y0 + 3), (-88, y1), (-75.6, y1 + 2)])
        s.ticks(-85, y0 + 5, 3 + i % 3, 1.8, 2.6, 90)
    with s.scope("alliance"):
        s.poly([(-74, 130), (-90, 144), (-96, 176), (-92, 206), (-82, 226), (-70, 230)], "base")
        s.poly([(-86, 154), (-93.6, 160), (-94, 194), (-87, 198)], "plate")
        s.windows(-80, 146, 214, 4)
        with s.scope("pochteca"):
            s.oct(-90, 138, -76, 140.6, 0, "band")
    s.line([(-74, 130), (-70, 230)], "weld")
    # starboard: the spear arm, twin heavy cannons down the outside of the jaw
    s.oct(68, 120, 82, 232, 2, "frame")
    for x in (71.6, 78.4):
        s.taper([(x, 230), (x, 262)], [2.4, 2.0], "gun")
    s.oct(70, 256, 80, 263, 1.2, "frame")
    s.poly([(68, 116), (82, 116), (80, 120), (68, 120)], "accent")
    # chief's hall, banner stripe and batteries
    s.oct(-18, 84, 18, 132, 5, "cab")
    for y in (92, 102, 112, 122):
        s.line([(-15, y), (15, y)], "seam")
    s.windows(-11, 88, 128, 3.4)
    s.windows(11, 88, 128, 3.4)
    s.poly([(-26, 168), (26, 168), (24, 172), (-24, 172)], "accent")
    for (x, y, b) in ((0, 148, 3), (0, 52, 3), (-44, 196, 2), (-40, 226, 2), (46, 196, 2), (40, 230, 2), (-48, 48, 2), (48, 48, 2), (52, 130, 2)):
        s.turret(x, y, 4.2, b)
    for x in (-34, 34):
        s.turret(x, 26, 3, 2, ang=270)
    return s, dict(
        name="ORDU", cls="Dreadnought", desig="ORDU-class chief-ship", tier="low",
        story="The clan's capital, forge and chief's hall. The forward jaw is a dock where three CORSACs at a time rearm while the rest of the pack fights. "
              "The port side is armoured with trophies; the biggest is the whole bow of an Alliance cruiser, welded on after the clan took it.",
        fit="2 spinal heavy cannons down the starboard arm · 9 heavy turrets · aft flak · 3 CORSAC cradles in the jaw",
        system="Rally: CORSACs in range repair and rearm twice as fast",
        built="Trophy plating · Pack chief (the pack fights harder while it lives) · Forward ward",
        asym="Trophy wall to port, spear-arm battery to starboard.",
        weak="Kill the chief and the pack breaks: every CORSAC withdraws when the ORDU falls. The inside of the jaw is lightly armoured.",
        variants="No two are alike: an ORDU's trophies record which clans and fleets it has beaten",
        stats=(5, 5, 2, 1, 4, 3))


# ======================================================================================================================
# TRIPLE ALLIANCE (Tlaxcala): midline, hybrid, standard spines with swappable modules; a house band marks the builder
# ======================================================================================================================

def al_auditor(v="base"):
    s = S("alliance")
    s.hull([(0, 4), (4, 5.6), (26, 5.6), (34, 4), (40, 1.6)], "base")
    s.oct(-1, 28, 1, 45, 0.3, "frame")
    s.oct(-0.4, 30, 0.4, 44.6, 0.1, "laser")
    s.oct(-8.4, 13, -4.6, 25, 0.8, "plate")
    if v == "s":
        with s.scope("scav"):
            s.truss((-6.4, 20), (-13, 26), 2.2)
            s.turret(-13.6, 27, 1.9, 3)
            s.slab([(-5.6, 6), (5.6, 6), (5.6, 11), (-5.6, 10)], "base")
            s.poly([(-4, 30), (3, 31), (2.4, 33), (-3.6, 32)], "plate")
    else:
        s.taper([(-7, 22.6), (-12.6, 30), (-15.4, 35.4)], [1.4, 1.1, 0.9], "frame")
        s.circ(-16, 36.6, 2.4, "frame")
        s.circ(-16, 36.6, 1.8, "cab")
        s.line([(-17.4, 36.6), (-14.6, 36.6)], "seam")
    s.oct(4.6, 9, 8.6, 24, 1, "plate")
    s.taper([(6.6, 23), (6.6, 32)], [1.0, 0.6], "gun")
    s.poly([(5.6, 32), (7.6, 32), (6.6, 34.4)], "accent")
    with s.scope("itztli"):
        s.oct(-5.6, 6.4, 5.6, 8.2, 0, "band")
    s.oct(-2.6, 22, 2.6, 30, 1.2, "cab")
    s.windows(0, 23.5, 28.5, 1.4)
    s.turret(0, 14, 1.4, 1)
    s.line([(-5.4, 12), (-5.4, 26)], "lline")
    s.engine(-2.4, 0, 2.9, 2.6)
    s.engine(2.4, 0, 2.9, 2.6)
    return s, dict(
        name="AUDITOR", cls="Frigate", desig="AUDITOR-class contract enforcer · Itztli Arms", tier="mid",
        story="The Alliance's toll collector. The scanner boom reads a cargo hold through two metres of steel, and the harpoon on the other side stops anyone who runs. "
              "Most captains pay. The railgun is for the ones who don't.",
        fit="1 railgun (fixed, centreline) · 1 harpoon (starboard) · 1 light turret · cargo scanner",
        system="Audit lock: reveals the target's cargo and slows it while harpooned",
        built="Standard bays · Corporate insurance",
        asym="Scanner boom to port, harpoon to starboard.",
        weak="Real warships: thin armour, and the boom is easy to shoot off.",
        variants="AUDITOR (S): Scavengers rip out the boom and put a gun on a truss in its place",
        stats=(2, 2, 3, 4, 2, 1))


def al_tally(v="base"):
    s = S("alliance")
    s.oct(-2.6, -2, 2.6, 94, 0.6, "frame")
    s.hull([(86, 10), (94, 9), (100, 5.4), (104.4, 1.6)], "base")
    bays = [24 + 12 * i for i in range(5)]
    for i, y0 in enumerate(bays):
        for sx in (-1, 1):
            x0, x1 = (-14, -2.6) if sx < 0 else (2.6, 14)
            gutted = v == "s" and i in (1, 2, 3)
            s.oct(x0, y0, x1, y0 + 11.4, 0.4, "frame" if gutted else "rad")
            if gutted:
                s.line([(x0, y0), (x1, y0 + 11.4), (x0, y0 + 11.4), (x1, y0)], "pipe")
    gunslots = {(1, -1), (3, 1), (2, -1)}
    for i, y0 in enumerate(bays):
        for sx in (-1, 1):
            x0, x1 = (-14, -3) if sx < 0 else (3, 14)
            if v == "base" and (i, sx) == (3, 1):
                continue
            if v == "s" and i in (1, 2, 3):
                continue
            s.containers(x0, y0 + 0.2, x1, y0 + 11.2, 2, 3, seed=i * 2 + (sx > 0))
            if v == "q" and (i, sx) in gunslots:
                s.oct(x0 + 0.6, y0 + 1, x1 - 0.6, y0 + 10.4, 0.4, "frame")
                s.line([(x0 + 0.6, y0 + 1), (x0 - 1.6 if sx < 0 else x1 + 1.6, y0 + 3.4)], "deckl")
                s.line([(x0 + 0.6, y0 + 10.4), (x0 - 1.6 if sx < 0 else x1 + 1.6, y0 + 8)], "deckl")
                with s.scope("itztli"):
                    s.turret((x0 + x1) / 2, y0 + 5.7, 2.6, 2, ang=180 if sx < 0 else 0)
    if v == "s":
        with s.scope("scav"):
            for i in (1, 2, 3):
                y0 = bays[i]
                s.truss((-3, y0 + 5.7), (-16 + i, y0 + 5.7), 3.6)
                s.truss((3, y0 + 5.7), (15 - i * 0.6, y0 + 6.6), 3.6)
            s.turret(-14.4, bays[1] + 5.7, 2.6, 2, ang=150)
            s.turret(-13.4, bays[3] + 5.7, 2.6, 2, ang=200)
            s.turret(13.4, bays[2] + 6.6, 2.6, 2, ang=20)
            for (x, y) in ((12, bays[1] + 2), (12.6, bays[3] + 2)):
                s.oct(x, y, x + 4.6, y + 7.6, 0.6, "plate")
                for k in range(3):
                    s.circ(x + 2.3, y + 1.6 + k * 2.2, 0.7, "accent")
            s.truss((-10, 4), (-21, 0), 3)
            s.engine2(-22, -2.6, 5.2, 4)
            s.truss((10, 6), (18, 4), 2.4)
            s.engine2(18.6, 1.6, 3.4, 3)
            s.slab([(-16, 8), (-6, 8.6), (-6, 20), (-16, 21)], "base")
            s.poly([(-5, 88), (6, 89.4), (5, 91.6), (-5.6, 90.4)], "plate")
            s.slab([(2.6, 92), (8.6, 91), (9, 97), (3, 98)], "accent")
    s.taper([(3, 50), (17, 52.4)], [1.4, 1.2], "frame")
    s.oct(15.6, 47.6, 19.8, 56.6, 0.6, "plate" if v != "s" else "rad")
    s.hazard(16, 55.4, 19.4, 56.4)
    s.oct(-17, 6, -6, 22, 2.2, "base")
    s.oct(-15.6, 14, -7.4, 21, 1.4, "cab")
    s.windows(-11.5, 15.5, 19.5, 1.3)
    s.oct(-12, -8, 12, 6, 2.4, "base")
    for x in (-6.5, 0, 6.5):
        s.engine(x, -8, 4.0, 3)
    with s.scope("pochteca"):
        s.oct(-8.6, 88, 8.6, 90.6, 0, "band")
    s.turret(0, 97, 1.4, 1)
    s.turret(0, 12, 1.3, 1, ang=270)
    meta = dict(
        name="TALLY", cls="Cargo ship", desig="TALLY-class container freighter · Pochteca Freight", tier="mid",
        story="The most common hull in Alliance space: an M-spine with five container bays, a crane for loading in open space, and the bridge "
              "on the port quarter where it can see the whole deck. Also the hull most often stolen and refitted.",
        fit="2 point-defence turrets",
        system="None",
        built="Standard bays · Corporate insurance · Civilian frame",
        asym="Bridge on the port quarter, crane to starboard, and whichever bays happen to be empty.",
        weak="Everything. It needs an escort.",
        variants="TALLY (I) Q-ship · TALLY (S) Gunbarge: both in the refit bay below",
        stats=(3, 2, 2, 2, 1, 5))
    if v == "q":
        meta.update(name="TALLY (I) Q-SHIP", desig="Itztli security refit",
                    story="Looks exactly like a TALLY, down to the Pochteca paint and the crane. Three of its containers are guns behind false doors. "
                          "Itztli sends them down pirate routes and waits.",
                    plus=["3 heavy twin turrets in containers, behind false doors", "Same outline and paint: it reads as a freighter until it fires"],
                    minus=["Cargo −40%", "Once the doors open, every pirate in the sector learns its name"],
                    stats=(3, 2, 2, 2, 4, 3))
    if v == "s":
        meta.update(name="TALLY (S) GUNBARGE", desig="Scavenger refit", tier="outlaw",
                    story="Containers ripped out, trusses welded in, a gun or a missile rack on every truss, and two salvaged engines bolted on. "
                          "Plenty of guns and not much hull.",
                    plus=["3 turrets and 2 missile racks on open trusses", "2 salvaged engines: +25% speed"],
                    minus=["Hull −40%: the bays were the structure", "No cargo", "Unequal engines: it pulls to port"],
                    stats=(2, 1, 1, 3, 5, 0))
    return s, meta


def al_escrow():
    s = S("alliance")
    s.oct(-3, 0, 3, 114, 0.8, "frame")
    s.poly([(3, 4), (26, 4), (28.4, 8), (28.4, 104), (22, 120), (3, 120)], "plate")
    s.line([(15.6, 10), (15.6, 114)], "deckl")
    for x in (8, 23):
        s.line([(x, 70), (x, 116)], "deckl")
    for y in (12, 16, 20):
        s.line([(10, y), (15.6, y + 3), (21.2, y)], "deckl")
    for (x, y) in ((8.6, 34), (8.6, 44), (22.6, 34), (22.6, 44), (8.6, 54)):
        s.poly([(x - 2.4, y - 2.4), (x + 2.4, y - 2.4), (x, y + 3)], "ghost")
    s.oct(-19, 18, -3, 90, 1.6, "base")
    s.containers(-18, 24, -4, 86, 3, 8, seed=5)
    s.hull([(90, 8), (100, 6.6), (112, 2)], "base", cx=-9.6)
    s.oct(-16, 2, -4, 18, 2, "base")
    s.oct(-14.4, 8, -5.6, 16, 1.4, "cab")
    s.windows(-10, 9.4, 14.6, 1.3)
    with s.scope("pochteca"):
        s.oct(-19, 88, -3, 90.2, 0, "band")
    with s.scope("itztli"):
        s.oct(3, 104, 28.4, 106.2, 0, "band")
    for (x, y) in ((-19.6, 32), (-19.6, 72), (28.8, 24), (28.8, 92)):
        s.turret(x, y, 2.2, 3, ang=180 if x < 0 else 0)
    for x in (18, 23):
        s.oct(x - 2, 108, x + 2, 114, 0.5, "frame")
        s.circ(x, 112, 0.8, "accent")
    for x in (8, 15.6, 23.2):
        s.engine(x, 4, 4, 3)
    s.engine(-10, 2, 4, 3)
    return s, dict(
        name="ESCROW", cls="Cruiser", desig="ESCROW-class convoy carrier · Pochteca and Itztli", tier="mid",
        story="Half freighter, half carrier on one L-spine: containers to port, a flight deck to starboard. It escorts its own convoy, "
              "so Pochteca saves on escorts and Itztli takes a cut. The bridge sits to port so the deck stays clear.",
        fit="2 drone wings · 4 flak turrets · 2 missile racks at the end of the deck",
        system="Scramble: launches every drone at once",
        built="Standard bays · Corporate insurance",
        asym="Flight deck to starboard; cargo, bow and bridge to port.",
        weak="Once its drones are down it is a slow freighter, and the deck is a big flat target.",
        variants="ESCROW (S): the deck plated over and turned into a gun platform",
        stats=(3, 2, 3, 2, 3, 3))


def al_consortium():
    s = S("alliance")
    s.oct(-9, -2, 9, 246, 2.4, "base")
    s.oct(-2.4, 6, 2.4, 238, 0.8, "frame")
    s.hull([(236, 12), (250, 9), (264, 3)], "base")
    for y in (20, 60, 100, 140, 180, 220):
        s.line([(-9, y), (9, y)], "seam")
    with s.scope("caisson"):
        s.oct(9, 14, 58, 90, 6, "base")
        for (x, y, r) in ((28, 34, 11.5), (28, 66, 11), (47, 50, 7)):
            s.circ(x, y, r + 1.2, "frame")
            s.circ(x, y, r, "cab")
            s.line([(x - r, y), (x + r, y)], "pline")
        s.taper([(52, 76), (64, 86), (68, 96)], [3, 2.4, 2], "frame")
        s.hazard(30, 87.6, 58, 90)
        s.hazard(30, 14, 58, 16.4)
        s.turret(50, 22, 2.4, 2, ang=0)
    with s.scope("pochteca"):
        s.oct(-66, 90, -9, 168, 8, "base")
        for y in (100, 116, 132, 148):
            s.oct(-63, y, -11, y + 2.4, 0, "band")
        for x in (-56, -46, -36, -26, -17):
            s.windows(x, 104, 160, 4)
        for y in (104, 129, 154):
            s.oct(-71, y - 3.4, -66, y + 3.4, 1, "frame")
        s.poly([(-90, 112), (-74, 112), (-71, 128), (-74, 146), (-90, 146)], "ghost")
        s.turret(-56, 94, 2.2, 2, ang=200)
    with s.scope("itztli"):
        s.poly([(9, 168), (48, 172), (56, 192), (52, 228), (22, 244), (9, 244)], "base")
        s.line([(13, 172), (48, 175.6), (52.6, 192), (48.6, 226), (22, 240.6)], "lline")
        s.turret(28, 188, 5.4, 3)
        s.turret(34, 212, 5.4, 3)
        s.turret(22, 232, 4.4, 2)
        s.oct(9, 168, 46, 170.6, 0, "band")
    s.oct(-14, 168, 4, 184, 2.6, "cab")
    s.windows(-9, 171, 181, 2.6)
    s.windows(-1, 171, 181, 2.6)
    for (x, y) in ((0, 200), (0, 218), (-14, 64), (-14, 34), (0, 108)):
        s.turret(x, y, 2.2, 1)
    s.oct(-30, -10, 30, 12, 5, "base")
    for x in (-21, -10.5, 0, 10.5, 21):
        s.engine(x, -10, 7.6, 4.6)
    return s, dict(
        name="CONSORTIUM", cls="Dreadnought", desig="CONSORTIUM-class board flagship · all three houses", tier="mid",
        story="Three modules from three houses on one spine. Itztli owns the gun deck, Pochteca the trade hall and Caisson the refinery, "
              "and each paints its third in its own colours. The board meets aboard, which is why the ship only moves when all three agree.",
        fit="Itztli: 3 heavy triple turrets · Pochteca: docking for 4 freighters, drone hangars · Caisson: a refinery that rebuilds blocks mid-battle",
        system="Board vote: one house system at a time (overcharge guns, launch all, or emergency repair)",
        built="Standard bays · Three bridges (each module keeps working if another is lost)",
        asym="Gun deck forward to starboard, trade hall amidships to port, refinery aft to starboard: modules zigzag along a straight spine.",
        weak="Slow to decide and slow to turn. Kill one module and the ship limps with a dead third.",
        variants="The board commissions one per decade; each house refits its own third to taste",
        stats=(5, 3, 4, 1, 4, 4))


def al_parcel(v="base"):
    s = S("alliance")
    s.oct(-1.6, -1, 1.6, 34, 0.4, "frame")
    s.oct(-5.4, 32, 4.6, 41, 2, "base")
    s.oct(-4.8, 34.6, 0.4, 40, 1.2, "cab")
    s.windows(-2.2, 35.6, 38.8, 1.2)
    s.oct(-6, -3, 6, 6, 1.6, "base")
    if v == "base":
        for sx in (-1, 1):
            s.containers(sx * 1.8 - (7.6 if sx < 0 else 0), 7, sx * 1.8 + (7.6 if sx > 0 else 0), 30, 2, 4, seed=sx + 3)
        s.turret(2.6, 36.6, 1.1, 1)
        with s.scope("pochteca"):
            s.oct(-6, 6, 6, 7.4, 0, "band")
    elif v == "s":
        for sx in (-1, 1):
            x0, x1 = (-9.4, -1.6) if sx < 0 else (1.6, 9.4)
            s.oct(x0, 7, x1, 30, 0.4, "frame")
            for y in (7, 14.6, 22.2):
                s.line([(x0, y), (x1, y + 7.6), (x0, y + 7.6), (x1, y)], "pipe")
        s.containers(1.8, 7, 9.4, 18.6, 2, 2, seed=4)
        with s.scope("scav"):
            s.slab([(1.8, 12), (9.4, 11), (9.4, 18.6), (1.8, 18.6)], "base")
        with s.scope("scav"):
            s.truss((-1.6, 21), (-9, 21), 2.6)
            s.turret(-10.2, 21, 2.0, 3)
            s.oct(2.6, 19, 8.8, 29, 0.8, "plate")
            for k in range(4):
                s.circ(4.2 + (k % 2) * 3, 21.6 + (k // 2) * 4.4, 1.0, "accent")
            s.truss((-5.4, 1), (-12, 2), 2.4)
            s.engine2(-12.4, -2.4, 4.6, 4.2)
            s.truss((5.4, 3), (9.6, 4), 2.0)
            s.engine2(10, 1.4, 3.0, 2.8)
            s.slab([(-5.4, 32), (1.4, 32), (1.4, 37), (-5.4, 36)], "base")
            s.poly([(-2, 38.6), (4.6, 39.6), (4, 41.2), (-2.4, 40.4)], "plate")
            s.slab([(2, -3), (6, -3), (6, 3), (2, 2)], "accent")
    elif v == "i":
        with s.scope("itztli"):
            for sx in (-1, 1):
                s.slab([(sx * 1.6, 7), (sx * 9.6, 8), (sx * 9.6, 29), (sx * 1.6, 30)], "base")
            s.oct(-9.6, 17.8, 9.6, 19.4, 0, "band")
            s.turret(0, 18.6, 2.3, 3)
            s.oct(4.6, 32, 7.6, 40, 1, "plate")
            s.taper([(6.1, 39), (6.1, 45)], [0.9, 0.5], "gun")
    for x in (-2.6, 2.6):
        s.engine(x, -3, 3.0, 2.6)
    meta = dict(
        name="PARCEL", cls="Frigate", desig="PARCEL-class light freighter · Pochteca Freight", tier="mid",
        story="A short-haul freighter: two container bays, a bridge and an engine block. Cheap and everywhere, and the first ship most captains own.",
        stats=(2, 1, 2, 3, 1, 3))
    if v == "s":
        meta.update(name="PARCEL (S) JACKRABBIT", desig="Scavenger raider refit", tier="outlaw",
                    story="Stripped for speed: one bay gutted for a rocket rack, a gatling on a truss where the other was, and two salvaged engines on outriggers.",
                    plus=["2 salvaged engines on outriggers: +40% speed", "Gatling on a port truss, rocket rack in the starboard bay"],
                    minus=["Holds gutted: cargo −70%", "Bay armour cut away: hull −30%", "Unequal engines: it pulls to port at full burn"],
                    stats=(1, 1, 1, 5, 3, 1))
    if v == "i":
        meta.update(name="PARCEL (I) ESCORT", desig="Itztli security refit",
                    story="The same hull brought up to Itztli's standard: armour plates over the bays, a flak turret between them and a harpoon at the bow. Escorts its own sister ships.",
                    plus=["Armour plates over both bays", "Triple flak turret, harpoon at the bow"],
                    minus=["Cargo −60%", "Speed −20%"],
                    stats=(3, 3, 2, 2, 2, 1))
    return s, meta


def al_wellhead(v="base"):
    s = S("alliance")
    s.oct(-4, 8, 4, 186, 1, "frame")
    s.hull([(170, 22), (184, 18), (198, 8), (206, 2)], "base", cx=-2)
    s.oct(-11, 178, 7, 192, 2.4, "cab")
    s.windows(-2, 181, 189, 1.6)
    s.oct(-32, -6, 32, 14, 4, "base")
    for x in range(-24, 25, 8):
        s.engine(x, -6, 6, 4)
    if v == "base":
        s.oct(-36, 24, -6, 172, 14, "cab")
        for y in range(40, 165, 20):
            s.oct(-36, y, -6, y + 2.4, 0, "frame")
        for (x, y, r) in ((20, 52, 16), (22, 90, 14.5), (19, 126, 15.5)):
            s.circ(x, y, r + 1.4, "frame")
            s.circ(x, y, r, "cab")
            s.line([(x - r, y), (x + r, y)], "pline")
        for y in (70, 108, 144):
            s.line([(4, y), (14, y), (14, y + 6)], "pipe")
        s.line([(-6, 60), (4, 60)], "pipe")
        s.line([(-6, 150), (4, 152), (10, 160)], "pipe")
        s.taper([(28, 140), (44, 160), (52, 178)], [3.2, 2.6, 2.0], "frame")
        s.oct(49, 176, 55, 182, 1, "plate")
        with s.scope("pochteca"):
            s.oct(-36, 166, -6, 168.6, 0, "band")
        s.hazard(-36, 172.6, -6, 174.4)
        s.turret(0, 196, 2, 1)
        meta = dict(
            name="WELLHEAD", cls="Dreadnought", desig="WELLHEAD-class tanker · Pochteca Freight, Caisson-built", tier="mid",
            story="Fuel and volatiles for a whole fleet: one long main tank to port, three spheres to starboard, and a loading arm for transfers in open space. "
                  "Most of its 200 m is fuel.",
            stats=(4, 2, 2, 1, 1, 5))
    else:
        with s.scope("khuchad"):
            s.oct(-36, 24, -6, 172, 14, "base")
            for y in range(40, 165, 20):
                s.oct(-36, y, -6, y + 2.4, 0, "frame")
            s.oct(-32, 30, -10, 166, 9, "frame")
            for y in (52, 98, 144):
                s.ghost(-21, y, 90, 2.9)
            for (x, y, r) in ((20, 52, 16), (22, 90, 14.5), (19, 126, 15.5)):
                s.circ(x, y, r + 1.4, "frame")
                s.circ(x, y, r, "plate")
                for k in range(6):
                    a = math.radians(60 * k + 30)
                    s.circ(x + math.cos(a) * r * 0.55, y + math.sin(a) * r * 0.55, r * 0.17, "frame")
                    s.circ(x + math.cos(a) * r * 0.55, y + math.sin(a) * r * 0.55, r * 0.09, "accent")
            for y0, y1 in ((22, 52), (52, 82), (82, 112), (112, 142), (142, 172)):
                s.slab([(-36, y0), (-46, y0 + 3), (-47, y1), (-36, y1 + 2)])
                s.ticks(-44.6, y0 + 6, 3, 1.4, 2.2, 90)
            s.oct(28, 138, 34, 146, 1, "frame")
            s.line([(28, 140), (34, 145)], "weld")
            for (x, y) in ((-41, 40), (-41, 100), (-41, 160), (40, 70), (40, 108), (0, 196)):
                s.turret(x, y, 3.2, 2, ang=180 if x < 0 else (0 if x > 0 else 90))
            s.poly([(-11, 192), (7, 192), (5, 195), (-9, 195)], "accent")
        meta = dict(
            name="WELLHEAD (K) PRIZE", cls="Dreadnought", desig="Khuchad prize conversion", tier="low",
            story="When the Khuchad take a WELLHEAD they core the main tank into a hangar for three CORSACs, turn the spheres into missile silos "
                  "and weld soot slabs down the port side. It becomes a slow, heavily armoured battle-carrier that still smells of fuel.",
            plus=["Main tank cored into a hangar: 3 CORSACs ride inside", "Spheres become missile silos (6 tubes each)", "Soot armour slabs and trophy tallies down the port side"],
            minus=["Fuel −70%", "Loading arm cut off", "Hits in the old tank start fires"],
            stats=(4, 4, 1, 1, 4, 1))
    return s, meta


# ======================================================================================================================
# VESTARI: high tech, slick, few hands and long lives. Every hull carries one offset fin: the speaker's station
# ======================================================================================================================

def ve_edict():
    s = S("vestari")
    s.poly([(0.6, 46), (4.4, 31), (13.6, 8), (12.4, 1.6), (4, 0), (-4, 0), (-7.4, 6.4), (-5.4, 25), (-2, 36)], "base")
    s.poly([(0.6, 44), (2, 30), (1.2, 3), (-1.2, 3), (-1.4, 30)], "plate")
    s.taper([(-6.8, 7), (-5, 25.4), (-1.8, 37)], [1.4, 1.2, 0.9], "frame")
    s.taper([(-6.8, 7.4), (-5, 25.4), (-1.9, 36.6)], [0.5, 0.45, 0.35], "laser")
    s.line([(4.4, 30), (12, 8)], "lline")
    s.line([(2.4, 22), (8.6, 4)], "lline")
    s.line([(-4.6, 2), (-3.8, 20)], "lline")
    s.poly([(4.2, 12), (9.4, 10), (9, 14), (4.6, 15)], "cab")
    s.slot(-3.6, 5, 0)
    s.slot(9.2, 12.4, 1.6, 1.0)
    return s, dict(
        name="EDICT", cls="Frigate", desig="EDICT-class border picket", tier="high",
        story="Stops intruders at the edge of Vestari space. Fast enough to choose every engagement, and the beam lance along its port edge "
              "cuts a frigate open in one pass. Edicts don't warn twice.",
        fit="1 beam lance (fixed, along the port edge) · strong forward shield",
        system="Glide: a burst of speed with the shield held up",
        built="Heirloom frame · Few hands",
        asym="Swept starboard wing with the speaker's cell in it; lance on the port edge.",
        weak="Sustained kinetic fire overloads its shield, and there is almost no armour behind it.",
        variants="None. The Vestari don't refit",
        stats=(1, 1, 5, 5, 3, 0))


def ve_cradle(v="base"):
    s = S("vestari")
    s.taper([(0, 0), (0, 16), (0, 74), (0, 94)], [10, 13, 13, 2], "base")
    s.oct(-1.2, 2, 1.2, 90, 0.4, "plate")
    s.taper([(-5.4, 74), (-14.6, 80)], [1.1, 0.8], "frame")
    s.oct(-16.4, 79, -13, 82.6, 0.8, "frame")
    s.line([(-5.6, 6), (-6.2, 70)], "lline")
    s.line([(5.6, 6), (6.2, 70)], "lline")
    if v == "base":
        s.oct(-7.4, 27, 7.4, 63, 4.4, "cab")
        s.oct(-4.6, 31, 4.6, 59, 3, "laser")
        for y in (36, 45, 54):
            s.line([(-4.4, y), (4.4, y)], "seam")
        em = [(-10.6, 30), (10.6, 30), (-11.8, 45), (11.8, 45), (-10.6, 60), (10.6, 60)]
    else:
        with s.scope("scav"):
            s.oct(-7.4, 27, 7.4, 63, 3, "base")
            s.circ(0, 45, 4.4, "frame")
            s.circ(0, 45, 2.6, "glow")
            s.turret(0, 34, 2.8, 2)
            s.turret(0, 56, 2.8, 2)
            s.slab([(3, 66), (6.4, 66), (6.4, 76), (3, 75)], "plate")
            s.slab([(-6.4, 14), (-2, 14.6), (-2, 22), (-6.4, 22.6)], "base")
            s.slab([(1.4, 80), (5, 80.6), (3.6, 86), (1.2, 85)], "accent")
            s.truss((5.6, 8), (12, 6), 2.2)
            s.engine2(12.6, 3.4, 4.2, 3.4)
            s.line([(-7.4, 27), (-7.4, 63)], "weld")
            s.line([(7.4, 27), (7.4, 63)], "weld")
            for (x, y) in ((10.6, 30), (-11.8, 45)):
                s.oct(x - 1, y - 1, x + 1, y + 1, 0.3, "frame")
        em = [(-10.6, 30), (11.8, 45), (-10.6, 60), (10.6, 60)]
    for (x, y) in em:
        s.poly([(x, y + 2.6), (x + 1.8, y), (x, y - 2.6), (x - 1.8, y)], "plate")
        s.circ(x, y, 0.6, "laser")
    if v == "base":
        pts = em[0::2] + em[1::2][::-1]
        s.line(pts + [pts[0]], "lline")
    s.slot(-4.2, -0.6, 0)
    s.slot(0.6, 4.2, 0)
    meta = dict(
        name="CRADLE", cls="Cargo ship", desig="CRADLE-class gestation ship", tier="high",
        story="Carries the next generation: the vault holds a world's children, a century in the making. Six emitters cage the vault in overlapping shields. "
              "Every Vestari fleet in range answers its call, and no Vestari ship will fire while a CRADLE is in the line of fire.",
        fit="No weapons · 6 shield emitters · an escort spar to port",
        system="Sanctuary: the six emitters merge into one shield for 10 s",
        built="Heirloom frame · Few hands · Sacred hull (never refitted)",
        asym="Escort docking spar to port.",
        weak="It has no guns. Its shields hold until they don't.",
        variants="The Stolen CRADLE (S): see the refit bay",
        stats=(2, 1, 5, 3, 0, 4))
    if v == "s":
        meta.update(name="THE STOLEN CRADLE (S)", desig="Scavenger refit · the Scavenger King's prize", tier="outlaw",
                    story="The only refitted CRADLE. The vault was ripped out for a gun deck, two emitters were sold, and the pearl hull is patched in primer. "
                          "The Scavengers say it was empty when they took it. The Vestari don't believe them, and will cross any border to destroy it.",
                    plus=["Gun deck in the old vault: 2 heavy turrets", "Salvaged engine bolted to starboard", "Still has four Vestari emitters"],
                    minus=["Two emitters gone: gaps in its shield", "Every Vestari fleet attacks it on sight"],
                    stats=(2, 1, 3, 4, 3, 1))
    return s, meta


def ve_mandate():
    s = S("vestari")
    s.poly([(0, 128), (8, 98), (14, 42), (12.4, 8), (6, 0), (-6, 0), (-9.4, 10), (-9.4, 82), (-4, 114)], "base")
    s.poly([(0, 124), (2.4, 98), (2, 6), (-2, 6), (-2.6, 98)], "plate")
    s.oct(-15, 12, -9, 126, 1.2, "frame")
    s.oct(-13, 14, -11, 140, 0.4, "laser")
    s.poly([(-14.4, 136), (-9.6, 136), (-10.6, 142), (-13.4, 142)], "plate")
    s.poly([(12.6, 50), (31, 60), (32.6, 70), (13.6, 80)], "plate")
    s.circ(26.6, 65.6, 3.4, "cab")
    s.circ(26.6, 65.6, 1.6, "laser")
    for (a, b) in (((6, 104), (12, 46)), ((4, 70), (10, 8)), ((-6, 96), (-7, 14)), ((14, 52), (30, 61))):
        s.line([a, b], "lline")
    s.turret(4.4, 104, 1.9, 1)
    s.turret(7, 28, 1.9, 1, ang=270)
    s.slot(-5.6, -1, 0, 1.8)
    s.slot(0, 5, 0, 1.8)
    s.slot(6.4, 10.6, 3, 1.4)
    return s, dict(
        name="MANDATE", cls="Cruiser", desig="MANDATE-class lance cruiser", tier="high",
        story="A long faceted blade with its spinal lance down the port side rather than the centre, so the reactor and the speaker's fin "
              "can sit on the keel. It fights at the edge of its range, fires, and is gone before anyone can close.",
        fit="1 spinal beam lance (port) · 2 light energy turrets",
        system="Charge: the next lance shot does double damage",
        built="Heirloom frame · Few hands",
        asym="Lance to port, speaker's fin to starboard.",
        weak="Get inside its turn: the lance only fires forward, and it has little armour.",
        variants="None",
        stats=(3, 2, 4, 4, 4, 0))


def ve_quorum():
    s = S("vestari")
    cy = 140
    outer = arcpts(0, cy, 130, 198, 346, 48)
    inner = arcpts(0, cy + 6, 104, 342, 202, 48)
    tip_s = (132, 118)
    tip_p = (-118, 108)
    s.poly(outer + [tip_s] + inner + [tip_p], "base")
    band = arcpts(0, cy + 6, 106, 340, 204, 40) + arcpts(0, cy + 6, 112, 204, 340, 40)
    s.poly(band, "plate")
    s.line(arcpts(0, cy, 124, 206, 338, 40), "lline")
    for a in range(222, 330, 21):
        x, y = math.cos(math.radians(a)) * 117, cy + 2 + math.sin(math.radians(a)) * 117
        s.turret(x, y, 3.8, 2, ang=a + 180)
    for (x, y) in ((-108, 98), (-96, 82), (118, 106), (110, 92), (128, 116), (100, 78)):
        s.poly([(x, y + 3), (x + 2, y), (x, y - 3), (x - 2, y)], "plate")
        s.circ(x, y, 0.7, "laser")
    s.poly([(-44, 30), (-36, 2), (-20, -10), (20, -10), (36, 2), (44, 30)], "base")
    s.poly([(-3, -8), (3, -8), (4, 34), (-4, 34)], "plate")
    s.circ(16, 10, 8, "plate")
    s.circ(16, 10, 6.2, "cab")
    s.circ(16, 10, 2.4, "laser")
    s.line([(-34, 4), (-40, 28)], "lline")
    s.line([(34, 4), (40, 28)], "lline")
    s.slot(-30, -8, -10, 2.4)
    s.slot(-6, 6, -10, 2.0)
    s.slot(8, 30, -10, 2.4)
    s.circ(122, 106, 3.4, "cab")
    for a in (226, 240, 300, 314):
        x, y = math.cos(math.radians(a)) * 131, cy + math.sin(math.radians(a)) * 131
        s.poly(rot([(x - 3, y), (x + 3, y), (x + 2, y - 2.4), (x - 2, y - 2.4)], a - 270, x, y), "glow")
    return s, dict(
        name="QUORUM", cls="Dreadnought", desig="QUORUM-class parliament ship", tier="high",
        story="The Vestari Quorum, their elected assembly, sits aboard. The crescent throws one huge shield arc ahead of the fleet, and the main battery "
              "lines its inner edge. By law the battery fires only on a recorded vote, which takes about ten seconds.",
        fit="5 heavy beam turrets on the inner edge · 6 shield emitters on the horns",
        system="Assembly: projects a shield arc in front of the whole fleet",
        built="Heirloom frame · Few hands · Recorded vote (main battery has a 10 s authorisation)",
        asym="The starboard horn is longer and carries the speaker's chamber.",
        weak="The shield arc faces forward only and the convex back is bare. Strike before the vote is recorded.",
        variants="There is only one QUORUM",
        stats=(4, 2, 5, 2, 4, 1))


# ======================================================================================================================
# SCAVENGERS: no yards, only refits. The Scavenger King's own ship is three wrecks welded together
# ======================================================================================================================

def sc_kingscourt():
    s = S("scav")
    with s.scope("vestari"):
        s.poly([(-12, 0), (32, 0), (40, 12), (37, 44), (-12, 50)], "base")
        s.poly([(10, 2), (15, 2), (15, 46), (10, 47)], "plate")
        s.line([(28, 4), (35, 40)], "lline")
        s.line([(-8, 4), (-8, 46)], "lline")
        s.slot(-10, 8, 0, 2.4)
        s.slot(17, 34, 0, 2.4)
    s.line([(-12, 50), (37, 44)], "weld")
    with s.scope("alliance"):
        s.oct(-26, 48, 26, 192, 2, "rad")
        s.oct(-4, 46, 4, 194, 1, "frame")
        for i, y0 in enumerate(range(52, 186, 14)):
            for sx in (-1, 1):
                x0, x1 = (-25, -5) if sx < 0 else (5, 25)
                if (i + (sx > 0)) % 4 == 2:
                    s.oct(x0, y0, x1, y0 + 13, 0.4, "frame")
                    s.line([(x0, y0), (x1, y0 + 13)], "pipe")
                    s.line([(x0, y0 + 13), (x1, y0)], "pipe")
                else:
                    s.containers(x0, y0, x1, y0 + 13, 3, 3, seed=i * 3 + sx)
    s.slab([(-24, 56), (-8, 57), (-8, 76), (-24, 74)], "base")
    s.slab([(8, 122), (24, 121), (24, 140), (8, 141)], "accent")
    s.slab([(-24, 150), (-6, 151), (-6, 166), (-24, 164)], "plate")
    s.slab([(10, 66), (25, 64), (24, 78), (10, 79)], "base")
    for y in (82, 128, 172):
        s.truss((-26, y), (-52, y + 8), 4.6)
        s.turret(-53, y + 8.6, 3.6, 2, ang=165)
    s.truss((26, 100), (48, 96), 4)
    s.turret(49, 95.6, 3.6, 3, ang=15)
    s.oct(-15, 100, 15, 120, 4, "accent")
    s.oct(-10, 104, 10, 116, 3, "cab")
    s.windows(-4, 106, 114, 1.8)
    s.windows(4, 106, 114, 1.8)
    s.truss((-12, 30), (-34, 18), 3.6)
    s.engine2(-36, 11, 7, 6)
    s.engine2(-20, 36, 5, 4, 245)
    with s.scope("khuchad"):
        with s.moved(0, 192, -6):
            s.hull([(190, 24), (224, 23), (242, 15), (256, 5)], "base")
            for (y0, y1) in ((192, 210), (210, 228), (228, 244)):
                s.slab([(-22, y0), (-36, y0 + 1.4), (-33, y1), (-21, y1 + 1.4)])
            s.slab([(10, 236), (22, 238), (27, 252), (19, 266), (11, 259), (7, 246)])
            s.turret(2, 222, 3.8, 3)
            s.turret(20, 210, 3.2, 2, ang=60)
            s.ticks(-33, 196, 4, 1.6, 2.4, 90)
            s.poly([(-14, 244), (14, 244), (12, 248), (-12, 248)], "accent")
    s.line([(-26, 192), (26, 189)], "weld")
    s.line([(-26, 194.6), (26, 191.6)], "weld")
    return s, dict(
        name="KING'S COURT", cls="Dreadnought", desig="The Scavenger King's throne ship", tier="outlaw",
        story="Three wrecks welded into one: the spine of an Alliance TALLY, the bow of a Khuchad ARGALI, and the engines of a Vestari ship nobody admits to having seen. "
              "The bow went on six degrees off true and nobody has dared to fix it. Every few years another piece is added.",
        fit="ARGALI ram and dorsal triple · 5 turrets on open trusses · stolen Vestari engines",
        system="Stolen shield: a Vestari shield on a pirate reactor. Short, and it overheats",
        built="Gutted holds · Hot engines · Mismatched parts",
        asym="All of it.",
        weak="The welds. Hit the joins and it comes apart into three ships again, which is exactly how voxel damage works in this game.",
        variants="It changes every few years",
        stats=(4, 3, 3, 2, 4, 2))


# ======================================================================================================================
# REDACTED: the Vanguard (precursor derelicts) and the Silence. They break every human rule
# ======================================================================================================================

def vg_warden():
    s = S("vanguard")
    for a in (90, 210, 330):
        arm = [(-14, 12), (-5, 56), (0, 64), (5, 56), (14, 12), (0, 24)]
        s.poly(rot(arm, a - 90), "base")
        for k in (-2, -1, 0, 1, 2):
            s.line(rot([(k * 2.2, 22 + abs(k) * 1.6), (k * 1.0, 56 - abs(k) * 2)], a - 90), "flute")
        s.poly(rot([(-9, 20), (0, 30), (9, 20), (9, 23), (0, 34), (-9, 23)], a - 90), "accent")
        s.poly(rot([(-1.4, 58), (0, 66), (1.4, 58)], a - 90), "accent")
    s.circ(0, 0, 19, "base")
    s.ring(0, 0, 15.6, 17.4, "accent")
    s.circ(0, 0, 14.6, "plate")
    s.circ(0, 0, 4, "accent")
    for a in range(0, 360, 60):
        s.circ(math.cos(math.radians(a + 30)) * 9.4, math.sin(math.radians(a + 30)) * 9.4, 1.2, "accent")
    return s, dict(
        name="WARDEN", cls="Cruiser", desig="VANGUARD OBJECT · cruiser-sized", tier="redacted",
        story="Found holding station over dead worlds, turning slowly. It has no engines and no crew spaces, and it wakes when a ship comes within 20 km. "
              "<i class=rd>████████</i> Vestari ships will not fire on it. <i class=rd>██████</i>",
        fit="<i class=rd>████</i> · gold lances from the arm tips",
        system="<i class=rd>██████</i>",
        built="Never weathers · Cracks glow gold instead of burning",
        asym="None. Three-fold symmetry, which no human yard uses.",
        weak="Unknown. Its core brightens just before it fires.",
        variants="<i class=rd>███</i>",
        stats=None)


def vg_spindle():
    s = S("vanguard")
    L = 262
    w = lambda t: 34 * math.sin(math.pi * t) ** 0.75
    n = 40
    right = [(w(i / n), i / n * L) for i in range(n + 1)]
    s.poly(right + [(-x, y) for x, y in right[::-1]], "base")
    for k in (-3, -2, -1, 1, 2, 3):
        s.line([(k / 4 * w(i / n) * 0.95, i / n * L) for i in range(2, n - 1)], "flute")
    core = [(6 * math.sin(math.pi * i / n) ** 0.6, i / n * L) for i in range(n + 1)]
    s.poly(core + [(-x, y) for x, y in core[::-1]], "plate")
    for y in range(36, 240, 26):
        s.circ(0, y, 2.2, "accent")
    for y in (92, 172):
        s.poly([(26, y - 6), (70, y - 30), (64, y - 18), (26, y + 6)], "base", mirror=True)
        s.poly([(30, y - 4), (62, y - 22), (60, y - 18), (30, y)], "accent", mirror=True)
    s.ring(0, 132, 74, 78, "accent")
    return s, dict(
        name="SPINDLE", cls="Dreadnought", desig="VANGUARD OBJECT · 262 m", tier="redacted",
        story="A fluted ceramic spindle inside a gold halo. Three have been seen and none has been boarded. The halo spins faster as ships approach. "
              "<i class=rd>██████████</i> Aksharan <i class=rd>████████████</i>",
        fit="<i class=rd>██████████</i>",
        system="<i class=rd>████</i>",
        built="Never weathers · No engines · No seams a human yard would cut",
        asym="None. Mirror and radial symmetry only.",
        weak="<i class=rd>████████</i>",
        variants="<i class=rd>███</i>",
        stats=None)


def si_silence():
    s = S("ark")
    s.oct(-7, 0, 7, 46, 1.6, "base")
    s.oct(-12, 6, 12, 20, 1.6, "plate")
    s.oct(-3.4, 22, 3.4, 30, 1, "cab")
    s.hazard(-6.4, 4, 6.4, 5.6)
    s.engine(-3.4, 0, 3.8, 2.8)
    s.engine(3.4, 0, 3.8, 2.8)
    s.turret(0, 13, 1.8, 2)
    for y in (32, 38):
        s.line([(-7, y), (7, y)], "seam")
    with s.scope("silence"):
        pts = []
        for i in range(40):
            a = 2 * math.pi * i / 40
            r = 17 + 4 * math.sin(3 * a + 0.6) + 2.4 * math.sin(7 * a + 1.3) + 1.6 * math.sin(11 * a)
            pts.append((3 + math.cos(a) * r, 52 + math.sin(a) * r * 0.9))
        s.poly(pts, "vglow")
        s.poly(pts, "void")
        for (x, y, r) in ((-15, 40, 1.2), (20, 38, 0.9), (-20, 52, 0.7), (23, 50, 1.1)):
            s.circ(x, y, r, "accent")
    with s.scope("ark"):
        for (x, y, k) in ((-12, 37, 1.5), (13, 35.6, 1.2), (-9.4, 33, 0.9), (9, 33.4, 0.8)):
            s.poly([(x - k, y - k), (x + k, y - k * 0.6), (x + k * 0.8, y + k), (x - k * 0.6, y + k * 0.8)], "base")
    return s, dict(
        name="THE SILENCE", cls="Cruiser", desig="CONTACT · size unknown · [DATA EXPUNGED]", tier="redacted",
        story="Not a ship. A matte shape that reflects nothing, fringed with violet light. Whatever it touches is not broken but gone: no debris, no wreck. "
              "This plate shows an ARK survey frigate at the moment of contact.",
        fit="None observed",
        system="None observed",
        built="Reflects nothing",
        asym="It has no fixed shape.",
        weak="Leave.",
        variants="<i class=rd>█</i>",
        stats=None)


# ======================================================================================================================
# Page content
# ======================================================================================================================

TIERS = {"stranger": "ARK TECH", "low": "LOW TECH", "mid": "MIDLINE", "high": "HIGH TECH", "outlaw": "OUTLAW", "redacted": "REDACTED"}

# Faction liveries per art direction, from DESIGN-SYSTEM.md (DRYDOCK, NOCTURNE); dark primaries lifted slightly for plan views.
PAL = {
    "khuchad": (dict(base="#A8471E", plate="#2B2522", frame="#1A1512", cab="#D9C7A8", accent="#FF7A2F", trim="#7A5A40", glow="#FF8A3A",
                     laser="#FF7A2F", win="#FFB060", tur="#17120F", rad="#4A3A30", band="#FF7A2F", ore="#6B5545", crust="#3B2A20", ghost="#C08868"),
                dict(base="#4A2C20", plate="#6E3420", frame="#120C0A", cab="#9C8A76", accent="#FF7A3D", trim="#5A4030", glow="#FF7A3D",
                     laser="#FF7A3D", win="#FF9A50", tur="#0E0A08", rad="#2E1E18", band="#FF7A3D", ore="#4A3A30", crust="#1E1410", ghost="#8A5A40")),
    "alliance": (dict(base="#E6E0CF", plate="#1F7A4D", frame="#1E2B25", cab="#F6F3EA", accent="#5BE3A1", trim="#9AA89E", glow="#7FF0C0",
                      laser="#5BE3A1", win="#FFE2B0", tur="#17201C", rad="#3A5A4C", band="#E3B341", c1="#1F7A4D", c2="#E3B341", c3="#3E6E9A",
                      c4="#C9C2AE", deck="#2A5E45", ghost="#A9C4B4"),
                 dict(base="#26463A", plate="#2F6B55", frame="#0B1712", cab="#A6BDB0", accent="#B8FF6A", trim="#4E6A5E", glow="#B8FF6A",
                      laser="#B8FF6A", win="#E8FFB0", tur="#0A120F", rad="#1A3028", band="#A8862F", c1="#2F6B55", c2="#A8862F", c3="#2E4A66",
                      c4="#5E6A60", deck="#16302A", ghost="#5E7A6C")),
    "pochteca": (dict(band="#E3B341", base="#E6E0CF"), dict(band="#B8923A", base="#26463A")),
    "itztli": (dict(base="#2A2D33", band="#121317", plate="#1F7A4D", accent="#5BE3A1", tur="#0E0F12", frame="#0E0F12"),
               dict(base="#15171C", band="#08090B", plate="#2F6B55", accent="#B8FF6A", tur="#060708", frame="#060708")),
    "caisson": (dict(base="#4A4F57", plate="#D9A12E", cab="#E6E0D2", frame="#25282D"),
                dict(base="#3A3E44", plate="#A8822A", cab="#A8A49A", frame="#16181B")),
    "vestari": (dict(base="#E4E9EE", plate="#2F7FD0", frame="#2A3442", cab="#F7FAFC", accent="#9FD8FF", trim="#B8C6D4", glow="#9FD8FF",
                     laser="#BFE8FF", win="#CFF0FF", tur="#1E2632", rad="#C9D3DD", band="#2F7FD0", ghost="#AFC4D8"),
                dict(base="#24304A", plate="#C9D6E3", frame="#0B0F18", cab="#DCE6F0", accent="#9FE8FF", trim="#6F8098", glow="#9FE8FF",
                     laser="#BFF2FF", win="#9FE8FF", tur="#0B0F18", rad="#1A2336", band="#C9D6E3", ghost="#5E6E88")),
    "scav": (dict(base="#5B5E5A", plate="#8E2B25", frame="#262724", cab="#B8B4A8", accent="#D9B23A", trim="#7A7A72", glow="#FFB347",
                  laser="#FFB347", win="#FFB347", tur="#1C1D1B", rad="#45463F", band="#D9B23A", ghost="#8A8A80"),
             dict(base="#3A3532", plate="#5E2A24", frame="#141210", cab="#7E776C", accent="#FFB347", trim="#55504A", glow="#FFB347",
                  laser="#FFB347", win="#FFB347", tur="#0E0D0C", rad="#2A2624", band="#FFB347", ghost="#5E5852")),
    "vanguard": (dict(base="#ECE7DC", plate="#101317", frame="#C9C2B2", cab="#F7F3EA", accent="#FFC86B", trim="#D8CFBD", glow="#FFC86B", laser="#FFD88A"),
                 dict(base="#DADDE2", plate="#0A0C10", frame="#AEB3BC", cab="#F2F4F7", accent="#FFC86B", trim="#C2C6CE", glow="#FFC86B", laser="#FFD88A")),
    "silence": (dict(void="#060508", accent="#6E3BFF", glow="#6E3BFF"), dict(void="#000000", accent="#6E3BFF", glow="#6E3BFF")),
    "arkw": (dict(base="#5A5E64", plate="#D8D1C0", frame="#1E2023", cab="#EFEBE2", accent="#F2B705", glow="#FFE2B0", win="#FFE2B0", tur="#17191B", trim="#A8AAA3",
                  rad="#33373C", band="#D2402F", ore="#6B6355", crust="#3E3A34", ghost="#9AA0A8", laser="#FFE2B0"),
             dict(base="#1E2229", plate="#636B78", frame="#454B55", cab="#B9BFC8", accent="#FFD27A", glow="#FFE9C7", win="#FFE9C7", tur="#08090B", trim="#3A4048",
                  rad="#14181D", band="#FF5A4A", ore="#4A4640", crust="#2A2724", ghost="#6B7380", laser="#FFE9C7")),
    "ark": (dict(base="#CFC8B8", plate="#3B3E42", frame="#26282B", cab="#EFEBE2", accent="#F2B705", glow="#FFE2B0", win="#FFE2B0", tur="#1E2023", trim="#9A9C9E", rad="#6C7075", ore="#6B6355", crust="#3E3A34", ghost="#9AA0A8", laser="#FFE2B0"),
            dict(base="#2C3139", plate="#6B7380", frame="#4A505A", cab="#C9CED6", accent="#FFE9C7", glow="#FFE9C7", win="#FFE9C7", tur="#0C0E10", trim="#8A919C", rad="#424954", ore="#4A4640", crust="#2A2724", ghost="#6B7380", laser="#FFE9C7")),
}

FACTIONS = [
    dict(key="ark", name="ARK expedition", tier="stranger", tag="The humans · strangers in this galaxy · the player's faction",
         lore="The A.R.K. expedition was supposed to save humanity, and a long time passed before it arrived. Earlier Arks went ahead and were lost; "
              "their wrecks and beacons turn up across the galaxy. Behind the expedition there is nothing: only the Silence.",
         doctrine="Nothing local and nothing wasted. Every ARK ship is an Earth pattern printed by the ARK's fabricators and built to be repaired by its own crew "
                  "from whatever they can find. They are few, far from home and outgunned by every clan: no native shields, no energy weapons worth the name. "
                  "So every hull has one job the expedition can't afford to lose (find the way, feed the fleet, break through, rebuild), and every hull has adapter "
                  "points for the alien parts its crew takes. They are built from the same standard blocks the player uses: the most legible, most modifiable hulls in the galaxy.",
         paint="Bone and graphite plates, safety-yellow drop tanks and fabricators, hazard stripes on every edge that can hit something, red and green navigation lights "
               "and stencilled pattern numbers. Next to the clans they look plain, practical and foreign.",
         builtin="Printed pattern (the ARK can rebuild any of them from scratch) · Crew-repairable · Adapter points (clan parts can be bolted on)",
         mods="Field refits with clan parts (gone native), and wrecks of earlier Ark patterns to recover. Both are in the refit bay.",
         ships=[lambda: ark_petrel("base"), ark_gardener, ark_icebreaker, ark_holdfast]),
    dict(key="arkw", name="ARK war line", tier="stranger", tag="The humans at war · the bulk of the expedition's fighting force · built to fight as one team",
         lore="The same expedition and the same printers. The ARK's civilian patterns keep the expedition alive; these four keep it from being taken.",
         doctrine="One ship of the ARK loses to one ship of any clan: no shields, no energy weapons, crews that are few and far from home. So the war line never fights alone. "
                  "Every hull is a heavy kinetic puncher (coilguns firing slugs printed from ore), and every one carries a fire link. A HALO marks a target and every linked gun fires on it in the same second; "
                  "one volley does what no single ARK ship can. The THUNDERHEAD's mantlet stands in for the shields they can't make, the HALOs' point defence covers the team, "
                  "TEMPESTs are the weight of the line, and SQUALL pairs dart in from the flanks. All four share one build rule, heat to port and ammunition to starboard, "
                  "so any crew can fight any ship and a damaged hull can be patched with blocks from another.",
         paint="The civilian palette turned around: graphite hulls instead of bone, bone armour blocks, the same safety-yellow on every magazine, and one red stripe across each hull "
               "that marks its fire team. Fresh replacement blocks stay unpainted until the next refit.",
         builtin="Printed pattern · Fire link (linked ships volley together on a marked target) · Standard blocks (armour swaps between any ARK hulls)",
         mods="Clan parts bolt on here too (see the PETREL in the refit bay); the war patterns also have their own swaps (SQUALL Lancer, TEMPEST Bastion, THUNDERHEAD Breakwater).",
         ships=[aw_squall, aw_halo, aw_tempest, aw_thunderhead], extra="team"),
    dict(key="khuchad", name="Khuchad Clan", tier="low", tag="Low tech · bulky · pack and chief",
         lore="A warfare tradition. Hegemonic and grabby: they take a world, strip what is easy and move on. Low-to-midline tech, in large numbers of small ships with one large ship leading the pack.",
         doctrine="Pack and chief. Cheap, thick-skinned frigates hunt in packs of six to twelve around one huge chief-ship that is also the clan's moving camp. "
                  "Every hull is built like a mounted warrior: heavy armour on the port side (the shield arm), the ram or main gun offset to starboard (the lance arm). "
                  "They circle anticlockwise to keep the armour toward the enemy, then turn in to strike. Ballistic guns, rams and boarders; weak shields; slow.",
         paint="Rust hulls, soot lamellar armour, ember kill-tallies on the shield side. Trophy plates cut from beaten enemies are welded on in the enemy's own colours.",
         builtin="Trophy plating (armour grows with victories) · Pack link (frigates fight harder near their chief) · Forward ward (a narrow bow shield only)",
         mods="Prize conversions (K): a captured freighter becomes a carrier or gun-barge by coring its holds and welding soot slabs over one side.",
         ships=[kh_corsac, kh_kulan, kh_argali, kh_ordu]),
    dict(key="alliance", name="Triple Alliance (Tlaxcala)", tier="mid", tag="Midline · hybrid · standard spines, swappable modules",
         lore="A conglomerate of three big players that behave almost like megacorporations, holding an oligopoly known as the Triple Alliance.",
         doctrine="Every hull is a spine in one of three sizes with standard module bays, so the same hull leaves the yard as a freighter, a carrier or a gunship depending on the contract. "
                  "Ships are hybrids by default: they carry cargo and enough guns to keep it. A ship's outline tells you its contract. "
                  "Mixed weapons, missiles and drones; average armour and shields; the best logistics in the sector. "
                  "The three houses (names proposed): POCHTECA FREIGHT (trade and logistics), ITZTLI ARMS (security and weapons) and CAISSON HEAVY INDUSTRIES (mining and salvage: the batch-2 line).",
         paint="Ivory spines and bridges, corporate-green module bays, mint running lights. A house band marks the builder: gold for Pochteca, obsidian for Itztli, hazard chevrons for Caisson.",
         builtin="Standard bays (modules swap at any Alliance port) · Corporate insurance (cheap to repair, expensive to lose)",
         mods="Itztli security refits (I): armour and guns on civilian hulls, including the Q-ship, a freighter that hides its guns in containers.",
         ships=[al_auditor, al_tally, al_escrow, al_consortium]),
    dict(key="vestari", name="Vestari", tier="high", tag="High tech · slick · few hands, long lives",
         lore="Technologically advanced, dogmatic and democratic. Xenophobic: peaceful as long as no foreigner enters their space. They believe they are the direct descendants of the Aksharans and that all other life is lesser. High-tech ships in small fleets; a century-long gestation means there are very few of them.",
         doctrine="With so few people, every ship is an heirloom built to last centuries and never to lose its crew: strong shields, energy weapons, speed, heavy automation and very small crews. "
                  "They don't trade blows; they choose where the fight happens and leave when it turns. Every hull carries one offset fin or spire, the speaker's station, where the elected captain stands.",
         paint="Pearl planes and azure keels with ice-blue light-lines in the seams. No weathering and no markings: everyone already knows whose ship it is.",
         builtin="Heirloom frame (repairs itself between fights) · Few hands (lose a few crew and the ship is crippled)",
         mods="None. The Vestari never refit a hull; the single exception is a scandal (the Stolen CRADLE, in the refit bay).",
         ships=[ve_edict, ve_cradle, ve_mandate, ve_quorum]),
    dict(key="scav", name="Scavengers", tier="outlaw", tag="Outlaw · no yards, only refits",
         lore="Pirates loyal to the Scavenger King (named in the design system's faction colours).",
         doctrine="Every Scavenger ship was someone else's first. They strip armour and cargo for speed, bolt on salvaged engines, put guns on open trusses where the holds were, and patch everything in primer. "
                  "Faster, weaker and never the same twice. Their refits are in the refit bay below; this is the King's own ship.",
         paint="Primer grey, oxide-red slashes, mismatched yellow panels, with the victim's old paint still showing through.",
         builtin="Gutted holds (+speed, −hull) · Hot engines (salvaged engines run past their limit) · Mismatched parts",
         mods="(S) refits of anything they can catch.",
         ships=[sc_kingscourt]),
    dict(key="vanguard", name="[REDACTED]: the Vanguard and the Silence", tier="redacted", tag="Precursor derelicts and something else",
         lore="From the design system: Vanguard ceramic never weathers and glows warm gold, with chevron and fluted plates, never the human square seam. "
              "The Silence is matte shapes that reflect nothing, fringed with violet black-light, that erase what they touch.",
         doctrine="They break every human rule on purpose: no engines, no square seams, no faction paint, no weathering, symmetry no human yard uses. "
                  "Codex entries are mostly blacked out. They are encounters and mysteries, not factions you trade with.",
         paint="Vanguard: ceramic and glass-black with precursor gold (prismatic seams in NOCTURNE). The Silence: void black with a violet fringe.",
         builtin="—",
         mods="—",
         ships=[vg_warden, vg_spindle, si_silence]),
]

REFITS = [
    dict(title="PETREL pathfinder (ARK)", cls="Frigate",
         note="The player's own path: an older Ark's wreck, and the same hull wearing clan parts.",
         ships=[lambda: ark_petrel("base"), lambda: ark_petrel("m12"), lambda: ark_petrel("native")]),
    dict(title="PARCEL light freighter", cls="Frigate",
         note="Same hull, opposite choices: Scavengers trade hull for speed, Itztli trade speed for armour.",
         ships=[lambda: al_parcel("base"), lambda: al_parcel("s"), lambda: al_parcel("i")]),
    dict(title="TALLY container freighter", cls="Cargo ship",
         note="One refit hides its guns, the other shows them all.",
         ships=[lambda: al_tally("base"), lambda: al_tally("q"), lambda: al_tally("s")]),
    dict(title="WELLHEAD tanker", cls="Dreadnought",
         note="A civilian capital becomes a battle-carrier. The outline survives; the job doesn't.",
         ships=[lambda: al_wellhead("base"), lambda: al_wellhead("k")]),
    dict(title="CRADLE gestation ship", cls="Cargo ship",
         note="The refit that started a feud.",
         ships=[lambda: ve_cradle("base"), lambda: ve_cradle("s")]),
]

RESEARCH = [
    ("Tech tiers are philosophies, not upgrades.",
     "Low tech means armour, ballistics and long fights at low speed. Midline mixes everything, leans on missiles and carriers, and has the best logistics. "
     "High tech means shields, energy weapons and speed, for short hit-and-run fights. Each tier wins some matchups and loses others."),
    ("Factions reuse hulls and make them their own.",
     "The Hound appears as a pirate, Luddic Path, Luddic Church and Hegemony auxiliary version. The outline barely changes; the paint, a few bolt-ons and the built-in mods do."),
    ("Civilian hulls go to war.",
     "The Colossus, Atlas, Buffalo and Prometheus all have war conversions: holds become hangars or gun trusses, and the price is cargo, hull strength and often reliability. "
     "The description always explains who did it and why."),
    ("Pirate refits trade hull for speed; military refits do the opposite.",
     "The pirate Falcon gets hacked engines (faster, higher burn) and gives up some defences and range. The XIV Battlegroup and Lion's Guard versions get more armour or better gear on the same hull, "
     "and the Hegemony's auxiliaries are civilian hulls brought up to military standard."),
    ("Asymmetry follows use.",
     "Civilian and low-tech hulls carry offset tanks, cranes, bridges and flight decks (Tarsus, Prometheus, Mora, Condor, Astral), and high-tech hulls like the Odyssey and Aurora are asymmetric too. "
     "Warships are mostly symmetric with small offsets. Refits add asymmetry because they are bolted on."),
    ("Every hull has a niche and a counter.",
     "Frigates harass, finish and capture; carriers fight from range; phase ships ambush; a capital without escorts is a target. Bigger is not better, just different."),
    ("Redacted ships break the rules.",
     "Remnant drones, Omega's crystalline shards, the Threat's ribbed shells, the Dweller's red void and the Domain's derelicts each have their own geometry, unique weapons and sparse codex text. "
     "They are encounters and mysteries, not factions you trade with."),
    ("Codex depth.",
     "Every hull has a designation, a maker, a short in-world story, built-in hull mods, a ship system and known variants. Each card below uses that format."),
]

SOURCES = [("Ships", "https://starsector.wiki.gg/wiki/Ships"), ("Factions", "https://starsector.wiki.gg/wiki/Factions"),
           ("Falcon (P)", "https://starsector.wiki.gg/wiki/Falcon_(P)"), ("Colossus Mk.III", "https://starsector.wiki.gg/wiki/Colossus_Mk.III"),
           ("Prometheus Mk.II", "https://starsector.wiki.gg/wiki/Prometheus_Mk.II"), ("Atlas Mk.II", "https://starsector.wiki.gg/wiki/Atlas_Mk.II"),
           ("Buffalo Mk.II", "https://starsector.wiki.gg/wiki/Buffalo_Mk.II"), ("Hound (A)", "https://starsector.wiki.gg/wiki/Hound_(A)"),
           ("Onslaught Mk.I", "https://starsector.wiki.gg/wiki/Onslaught_Mk.I"), ("Ziggurat", "https://starsector.wiki.gg/wiki/Ziggurat"),
           ("Invictus", "https://starsector.wiki.gg/wiki/Invictus"), ("Lion's Guard", "https://starsector.wiki.gg/wiki/Lion%27s_Guard"),
           ("Luddic Path", "https://starsector.wiki.gg/wiki/Luddic_Path"), ("Remnant", "https://starsector.wiki.gg/wiki/Remnant"),
           ("Omega", "https://starsector.wiki.gg/wiki/Omega"), ("Threat", "https://starsector.wiki.gg/wiki/Threat"),
           ("Weapons", "https://starsector.wiki.gg/wiki/Weapons"), ("Hullmods", "https://starsector.wiki.gg/wiki/Hullmods")]


def palette_css():
    out = []
    for key, (dd, nc) in PAL.items():
        for mode, pal in (("pdd", dd), ("pnc", nc)):
            out.append(f".{mode} .f-{key}{{" + ";".join(f"--{k}:{v}" for k, v in pal.items()) + "}")
    return "\n".join(out)


STAT_NAMES = ("Hull", "Armour", "Shield", "Speed", "Firepower", "Cargo", "Ram")

RAM = {"CORSAC": 1, "KULAN": 2, "ARGALI": 4, "ORDU": 2, "CONSORTIUM": 1, "KING'S COURT": 3, "PARCEL (I) ESCORT": 1,
       "WELLHEAD": 1, "WELLHEAD (K) PRIZE": 2}


def full(m):
    st = m["stats"]
    if st is None or len(st) == 7:
        return st
    return tuple(st) + (RAM.get(m["name"], 0),)


def stats_html(st, base=None):
    rows = []
    for i, nm in enumerate(STAT_NAMES):
        if st is None:
            pips = '<i class="unk">?????</i>'
        else:
            pips = "".join(f'<i class="{"on" if k < st[i] else ""}"></i>' for k in range(5))
            if base is not None and st[i] != base[i]:
                d = st[i] - base[i]
                pips += f'<em class="{"up" if d > 0 else "dn"}">{"+" if d > 0 else "−"}{abs(d)}</em>'
        rows.append(f"<span>{nm}</span><b>{pips}</b>")
    return '<div class="stats">' + "".join(rows) + "</div>"


# ======================================================================================================================
# Rendering
# ======================================================================================================================

SCALES3 = {"Frigate": 64, "Destroyer": 100, "Cargo ship": 128, "Cruiser": 150, "Dreadnought": 280}
BARS3 = {"Frigate": 10, "Destroyer": 10, "Cargo ship": 20, "Cruiser": 20, "Dreadnought": 50}


def sprite_h(s, bar=None, mono=False, pad=0.03, cls="spr"):
    """A plan view turned bow-right (port at the top), for ships and plates too long for a square card."""
    x0, y0, x1, y1 = s.bbox()
    L, W = y1 - y0, x1 - x0
    p = max(L, W) * pad
    vx, vy, vw, vh = y0 - p, x0 - p, L + 2 * p, W + 2 * p
    sw = vw / 1000
    out = [f'<svg class="{"mono" if mono else cls} wide" viewBox="{F(vx)} {F(vy)} {F(vw)} {F(vh)}" xmlns="http://www.w3.org/2000/svg" style="--sw:{sw:.3f}">'
           '<g transform="rotate(90)">']
    out += s.wrapped()
    out.append("</g>")
    if bar and not mono:
        bx, by = vx + p, vy + vh - p * 0.4
        out.append(f'<g class="bar"><line x1="{F(bx)}" y1="{F(by)}" x2="{F(bx + bar)}" y2="{F(by)}" stroke-width="{F(sw * 2.5)}"/>'
                   f'<text x="{F(bx)}" y="{F(by - sw * 8)}" font-size="{F(sw * 16)}">{bar} m</text></g>')
    out.append("</svg>")
    return "".join(out)


def hero_card(s, m):
    codex = "".join(f"<dt>{k}</dt><dd>{m[f]}</dd>" for k, f in (("Fit", "fit"), ("System", "system"), ("Built in", "built"),
                                                                   ("Asymmetry", "asym"), ("Variants", "variants")) if m.get(f))
    return (f'<button class="card card3 hero" data-key="{m["name"]}" data-name="{m["name"]} (ARK expedition)">'
            f'<div class="thumb">{sprite_h(s, mono=True)}</div><span class="tier t-{m["tier"]}">{TIERS[m["tier"]]}</span>'
            f'{sprite_h(s, 100)}'
            f'<div class="nm">{m["name"]}</div><div class="role">{m["desig"]} · {m["cls"].lower()} · {size_txt(s)}</div>'
            f'<div class="hcols"><div>{stats_html(full(m))}<p>{m["story"]}</p></div><div><dl class="codex">{codex}</dl>'
            f'<p class="weak"><b>Weak to:</b> {m["weak"]}</p></div></div></button>')


def team_plate():
    s = aw_team()
    return ('<div class="team"><div class="plate">' + sprite_h(s, 100, pad=0.04, cls="spr tp") + '</div>'
            '<div class="tnote"><b>The fire team, at true scale.</b> Enemy ahead (to the right): a Khuchad ARGALI. '
            '<ul><li><b>THUNDERHEAD</b> in the centre; its mantlet takes the fire from ahead.</li>'
            '<li><b>TEMPESTs</b> at its shoulders, just behind the mantlet\'s wings, every gun facing forward.</li>'
            '<li><b>HALOs</b> behind the gaps: point defence over everyone, and the mark (yellow).</li>'
            '<li><b>SQUALL pairs</b> out on the flanks, ready to dart in.</li></ul>'
            'When the HALO marks the target, every linked gun fires in the same second (red). One volley does what no single ARK ship can. '
            'Break the team (kill the HALOs, get behind the TEMPESTs, come in under the mantlet\'s edge) and it fights one ship at a time.</div></div>')


def sprite3(s, scale_m, bar=None, mono=False):
    x0, y0, x1, y1 = s.bbox()
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    half = scale_m / 2
    sw = scale_m / 400
    out = [f'<svg class="{"mono" if mono else "spr"}" viewBox="{F(cx - half)} {F(-cy - half)} {F(2 * half)} {F(2 * half)}" '
           f'xmlns="http://www.w3.org/2000/svg" style="--sw:{sw:.3f}">']
    out += s.wrapped()
    if bar and not mono:
        bx, by = cx - half + scale_m * 0.05, -cy + half - scale_m * 0.04
        out.append(f'<g class="bar"><line x1="{F(bx)}" y1="{F(by)}" x2="{F(bx + bar)}" y2="{F(by)}" stroke-width="{F(sw * 2.5)}"/>'
                   f'<text x="{F(bx)}" y="{F(by - sw * 8)}" font-size="{F(sw * 16)}">{bar} m</text></g>')
    out.append("</svg>")
    return "".join(out)


def size_txt(s):
    x0, y0, x1, y1 = s.bbox()
    return f"{y1 - y0:.0f} m long, {x1 - x0:.0f} m wide"


def ship_card(s, m, faction):
    cls = m["cls"]
    codex = "".join(f"<dt>{k}</dt><dd>{m[f]}</dd>" for k, f in (("Fit", "fit"), ("System", "system"), ("Built in", "built"),
                                                                   ("Asymmetry", "asym"), ("Variants", "variants")) if m.get(f))
    return (f'<button class="card card3" data-key="{m["name"]}" data-name="{m["name"]} ({faction})">'
            f'<div class="thumb">{sprite3(s, SCALES3[cls], mono=True)}</div><span class="tier t-{m["tier"]}">{TIERS[m["tier"]]}</span>'
            f'{sprite3(s, SCALES3[cls], BARS3[cls])}'
            f'<div class="nm">{m["name"]}</div><div class="role">{m["desig"]} · {cls.lower()} · {size_txt(s)}</div>'
            f'{stats_html(full(m))}<p>{m["story"]}</p><dl class="codex">{codex}</dl>'
            f'<p class="weak"><b>Weak to:</b> {m["weak"]}</p></button>')


def refit_card(s, m, base_stats, cls, is_base):
    diff = ""
    if not is_base:
        diff = ('<ul class="diff">' + "".join(f'<li class="p">{t}</li>' for t in m.get("plus", []))
                + "".join(f'<li class="m">{t}</li>' for t in m.get("minus", [])) + "</ul>")
    return (f'<button class="card card3" data-key="{m["name"]}" data-name="{m["name"]}">'
            f'<div class="thumb">{sprite3(s, SCALES3[cls], mono=True)}</div><span class="tier t-{m["tier"]}">{"BASE HULL" if is_base else TIERS[m["tier"]]}</span>'
            f'{sprite3(s, SCALES3[cls], BARS3[cls])}'
            f'<div class="nm">{m["name"]}</div><div class="role">{m["desig"]} · {size_txt(s)}</div>'
            f'{stats_html(full(m), None if is_base else base_stats)}<p>{m["story"]}</p>{diff}</button>')


def render():
    secs = []
    res = "".join(f"<li><b>{h}</b> {t}</li>" for h, t in RESEARCH)
    src = " · ".join(f'<a href="{u}">{n}</a>' for n, u in SOURCES)
    secs.append(f"""<section class="research"><details open><summary><h2>What we took from Starsector</h2></summary>
<p class="blurb">Notes from the wiki's ship, faction, refit and hull-mod pages and a study of the ship sprites (looked at, not stored). Our own words throughout.</p>
<ol>{res}</ol>
<h3>How it maps onto our universe</h3>
<p class="blurb">The factions already exist in your content: the lore below each faction name is from <code>Import/Legacy/content/Factions</code>, and the colours
are the faction liveries in <code>DESIGN-SYSTEM.md</code>; the ARK lore is from the legacy narrative, one per art direction. House names, ship names and stories are mine and yours to change.</p>
<table class="map"><tr><th>Starsector</th><th>Ours</th><th>Shape language</th></tr>
<tr><td>Low tech (Hegemony)</td><td><b>Khuchad Clan</b></td><td>Bulky, lamellar armour to port, ram or gun to starboard, packs around a chief-ship</td></tr>
<tr><td>Midline (Persean League)</td><td><b>Triple Alliance</b> (Tlaxcala)</td><td>Hybrid: standard spines with module bays; cargo plus guns; house colour bands</td></tr>
<tr><td>High tech (Tri-Tachyon)</td><td><b>Vestari</b></td><td>Slick: long faceted blades and crescents, light-lines, one offset speaker's fin</td></tr>
<tr><td>Pirates, Luddic Path refits</td><td><b>Scavengers</b> (S), Khuchad prizes (K)</td><td>Refits of other people's hulls: trusses, bolted engines, primer and mismatched paint</td></tr>
<tr><td>Elite and auxiliary refits (XIV, LG, (A))</td><td><b>Itztli security refits</b> (I)</td><td>Same hull, more armour and guns, obsidian band</td></tr>
<tr><td>Remnant, Omega, Threat, Dweller, Domain derelicts</td><td><b>Vanguard</b>, <b>the Silence</b> (and the xeno aliens already built)</td><td>Rule-breaking: three-fold symmetry, no engines, flutes and chevrons, void</td></tr>
<tr><td>(none: here the humans are the strangers)</td><td><b>ARK expedition</b>, the player</td><td>Earth patterns from the ARK printers: legible standard blocks, hazard stripes, nav lights, adapter points for clan parts. The war line is the same patterns in graphite, built to fight as a team</td></tr></table>
<p class="blurb"><b>Strangers and natives.</b> The Khuchad, the Alliance and the Vestari are the galaxy's own clans; the humans of the ARK are the newcomers. So the human ships are deliberately the odd ones out: plainer, foreign, and the only ones built to take other people's parts.</p>
<p class="blurb"><b>Matchups.</b> Khuchad beat the Alliance up close (armour and rams against average shields). The Alliance beats the Vestari with numbers and missiles
(shields overload and there are too few Vestari to replace losses). The Vestari beat the Khuchad by never letting them close. Frigates matter everywhere:
a CORSAC pack on an ARGALI's open side, an AUDITOR's harpoon on a fleeing EDICT.</p>
<p class="src">Sources: {src}</p></details></section>""")
    for fa in FACTIONS:
        if fa["key"] == "ark":
            s, m = ark_mothership()
            secs.append('<section class="fac f-ark mother"><h2><span class="tier t-stranger">ARK TECH</span> The ARK<small>The expedition mothership · station-sized · '
                        'home port, shipyard and capital</small></h2>'
                        '<p class="blurb">Turned bow-right to fit the page (port side at the top). Everything docked to it is at true scale: '
                        'the HOLDFAST in the starboard gantry is 254 m long.</p>'
                        f'<div class="grid4">{hero_card(s, m)}</div></section>')
        cards = []
        for fn in fa["ships"]:
            s, m = fn()
            cards.append(ship_card(s, m, fa["name"]))
        dd, nc = PAL.get(fa["key"], PAL["vanguard"])
        sw = lambda p: "".join(f'<i style="background:{p[k]}"></i>' for k in ("base", "plate", "accent") if k in p)
        secs.append(f'<section class="fac f-{fa["key"]}"><h2><span class="tier t-{fa["tier"]}">{TIERS[fa["tier"]]}</span> {fa["name"]}'
                    f'<small>{fa["tag"]}</small><span class="sw"><span class="dd">{sw(dd)}</span><span class="nc">{sw(nc)}</span></span></h2>'
                    f'<p class="blurb lore"><b>Your lore.</b> {fa["lore"]}</p><p class="blurb"><b>Doctrine.</b> {fa["doctrine"]}</p>'
                    f'<p class="blurb"><b>Paint.</b> {fa["paint"]}</p>'
                    f'<p class="blurb"><b>Built into every hull.</b> {fa["builtin"]} &nbsp; <b>Mods they make.</b> {fa["mods"]}</p>'
                    f'<div class="grid4">{"".join(cards)}</div>{team_plate() if fa.get("extra") == "team" else ""}</section>')
    rc = []
    for fn, fac in ((kh_tarpan, "Khuchad Clan"), (ark_icebreaker, "ARK expedition")):
        s, m = fn()
        rc.append(ship_card(s, m, fac))
    s, m = sc_fireship()
    rc.append(refit_card(s, m, full(al_tally("base")[1]), "Cargo ship", False))
    secs.append("<section class=\"fac rammers\"><h2>Rammers</h2>"
                "<p class=\"blurb\">Ramming is a game system: ram damage comes from collision impulse (mass × speed), and with voxel destruction both ships take the hit. "
                "So a rammer needs its own build: mass up front in a solid, replaceable prow; a spine that carries the shock so the rammer doesn't snap; "
                "lots of thrust, plus reverse thrust to pull free; and nothing fragile near the bow. Its counter is anything that turns faster or keeps its distance. "
                "Every card now has a <b>Ram</b> bar.</p>"
                "<p class=\"blurb\">Who rams: the <b>Khuchad</b> live for it (TARPAN, the ARGALI's breaching ram, the KULAN's bow). The <b>ARK</b> learned it out of necessity (ICEBREAKER). "
                "<b>Scavengers</b> do it once per ship (the FIRESHIP). The <b>Alliance</b> would rather harpoon you and tow you in. The <b>Vestari</b> never ram: with so few of them, "
                "trading a hull and its crew for one hit is unthinkable. Batch 2 also has rammers: PENITENT (NOCTURNE frigate) and the MAUSOLEUM's ram.</p>"
                f"<div class=\"grid4\">{''.join(rc)}</div></section>")
    rows = []
    for rf in REFITS:
        built = [fn() for fn in rf["ships"]]
        base_stats = full(built[0][1])
        cards = [refit_card(s, m, base_stats, rf["cls"], i == 0) for i, (s, m) in enumerate(built)]
        rows.append(f'<div class="rrow"><div class="cls"><b>{rf["title"]}</b><span>{rf["note"]}</span><span class="sc">same scale across the row</span></div>'
                    + "".join(cards) + "</div>")
    secs.append('<section class="fac refits"><h2>Refit bay: cargo hulls re-specced for war</h2>'
                '<p class="blurb">The same hull refitted by different people for different reasons, like a Falcon and its pirate version. Each refit keeps the base outline '
                'so you can still recognise the hull, then changes it where the new job needs it. The stat bars show what the refit gained (+) and lost (−).</p>'
                '<p class="blurb">In our game a refit is just a blueprint edit (cargo blocks out, gun blocks and thrusters on trusses in), so these double as examples '
                'of what players can do to hulls they capture.</p>' + "".join(rows) + "</section>")
    return "".join(secs)


CSS3 = """
.tabs{display:flex;gap:8px;flex-wrap:wrap;margin:14px 0 0}
.tabs button,.paint button{font:inherit;font-size:14px;padding:7px 14px;border-radius:999px;border:1px solid var(--line);background:transparent;color:var(--ink);cursor:pointer}
.tabs button.on,.paint button.on{background:var(--ink);color:var(--page);border-color:var(--ink)}
.paint{display:flex;gap:8px;align-items:center;margin:10px 0 0;color:var(--muted);font-size:13px;flex-wrap:wrap}
.pane{display:none}.pane.on{display:block}
.research ol{margin:6px 0 10px;padding-left:22px;max-width:1000px;color:#CFD3DC}.research li{margin:5px 0}.research li b{color:var(--ink)}
.research summary{cursor:pointer;list-style:none}.research summary h2{display:inline}
.research h3{font-size:16px;margin:16px 0 4px}
.map{border-collapse:collapse;font-size:13px;margin:8px 0;max-width:1000px;width:100%}
.map th,.map td{border-bottom:1px solid var(--line);padding:6px 8px;text-align:left;vertical-align:top}.map th{color:var(--muted);font-weight:600}
.src{font-size:12px;color:var(--muted)}.src a{color:#8FB4FF}
.fac h2{display:flex;align-items:center;gap:10px;flex-wrap:wrap}.fac h2 small{font-size:13px;color:var(--muted);font-weight:500}
.sw{display:inline-flex;gap:3px;margin-left:auto}.sw i{display:inline-block;width:16px;height:16px;border-radius:3px;border:1px solid rgba(255,255,255,.2)}
.pdd .sw .nc,.pnc .sw .dd{display:none}
.tier{font-size:10px;font-weight:800;letter-spacing:.12em;padding:3px 7px;border-radius:4px;color:#fff;white-space:nowrap}
.t-stranger{background:#F2B705;color:#16181B}.t-low{background:#A8471E}.t-mid{background:#1F7A4D}.t-high{background:#2F7FD0}.t-outlaw{background:#8E2B25}.t-redacted{background:#000;color:#FFC86B;border:1px solid #FFC86B}
.card .tier{position:absolute;top:12px;right:12px}
.grid4{display:grid;grid-template-columns:repeat(auto-fill,minmax(290px,1fr));gap:12px;margin:12px 0}
.rrow{display:grid;grid-template-columns:150px repeat(3,minmax(0,1fr));gap:12px;margin:16px 0}
.stats{display:grid;grid-template-columns:auto 1fr auto 1fr;gap:1px 8px;font-size:11px;color:var(--muted);margin:6px 0 2px;align-items:center}
.stats b{display:flex;gap:2px;align-items:center;font-weight:400}.stats i{display:inline-block;width:9px;height:7px;border-radius:1px;background:#26304A}
.stats i.on{background:#D8D4CA}.stats i.unk{width:auto;height:auto;background:none;font-style:normal;color:#FFC86B;letter-spacing:.1em}
.stats em{font-style:normal;font-size:10px;margin-left:4px;font-weight:700}.stats em.up{color:#7BE39A}.stats em.dn{color:#F08A6A}
.codex{display:grid;grid-template-columns:auto 1fr;gap:3px 8px;margin:8px 0 0;font-size:12px;line-height:1.35}
.codex dt{color:var(--muted);font-weight:600}.codex dd{margin:0;color:#CFD3DC}
.diff{list-style:none;padding:0;margin:8px 0 12px;font-size:12px}.diff li{margin:2px 0;padding-left:16px;position:relative;color:#CFD3DC}
.diff li.p::before{content:"+";position:absolute;left:2px;color:#7BE39A;font-weight:700}.diff li.m::before{content:"−";position:absolute;left:2px;color:#F08A6A;font-weight:700}
.rd{font-style:normal;color:#E9E6DF;letter-spacing:-.05em}
.lore b{color:#FFC86B}
.spr .navr{fill:#FF3B30}.spr .navg{fill:#34E36B}.spr .band{fill:var(--band)}.spr .c1{fill:var(--c1)}.spr .c2{fill:var(--c2)}.spr .c3{fill:var(--c3)}.spr .c4{fill:var(--c4)}.spr .deck{fill:var(--deck)}
.spr .vglow{fill:var(--accent);filter:url(#vblur);opacity:.9}
.spr .tick{fill:none;stroke:var(--accent);stroke-width:calc(var(--sw)*1.8)}
.spr .lline{fill:none;stroke:var(--accent);stroke-width:calc(var(--sw)*1.1);opacity:.85}
.spr .truss{fill:none;stroke:var(--frame);stroke-width:calc(var(--sw)*1.3)}
.spr .weld{fill:none;stroke:var(--glow);stroke-width:calc(var(--sw)*2.2);stroke-dasharray:calc(var(--sw)*4) calc(var(--sw)*2.5)}
.spr .flute{fill:none;stroke:var(--frame);stroke-width:var(--sw)}
.spr .deckl{fill:none;stroke:var(--cab);stroke-width:calc(var(--sw)*1.2);stroke-dasharray:calc(var(--sw)*5) calc(var(--sw)*3);opacity:.75}
.spr .cable{fill:none;stroke:var(--frame);stroke-width:calc(var(--sw)*2.6)}
.spr .pipe{fill:none;stroke:var(--trim);stroke-width:calc(var(--sw)*2.6)}
.mono .navr,.mono .navg,.mono .tick,.mono .lline,.mono .truss,.mono .weld,.mono .flute,.mono .deckl,.mono .pipe,.mono .vglow{display:none}
.mono .cable{fill:none !important;stroke:#111 !important;stroke-width:calc(var(--sw)*3) !important}
.pnc .card3 .spr .base,.pnc .card3 .spr .plate{stroke:var(--accent);stroke-opacity:.35;stroke-width:calc(var(--sw)*.8)}
@keyframes prism{0%{fill:#FF7B7B}20%{fill:#FFD86B}40%{fill:#7BFFA8}60%{fill:#6BD8FF}80%{fill:#B88BFF}100%{fill:#FF7B7B}}
.pnc .f-vanguard .accent{animation:prism 6s linear infinite}
.card.hero{grid-column:1/-1}
.card svg.wide{aspect-ratio:auto;height:auto;width:100%;display:block}
.hero .thumb{width:15%}
.hcols{display:grid;grid-template-columns:1.1fr 1fr;gap:18px}
.team{display:grid;grid-template-columns:1.6fr 1fr;gap:16px;align-items:center;background:var(--card);border:2px solid var(--line);border-radius:10px;padding:12px;margin:4px 0 12px}
.tnote{font-size:13px;line-height:1.45;color:#CFD3DC}.tnote b{color:var(--ink)}.tnote ul{margin:6px 0;padding-left:18px}
.spr .fireline{fill:none;stroke:var(--band);stroke-width:calc(var(--sw)*3.5);stroke-dasharray:calc(var(--sw)*14) calc(var(--sw)*8);opacity:.85}
.spr .markline{fill:none;stroke:var(--accent);stroke-width:calc(var(--sw)*3.5);stroke-dasharray:calc(var(--sw)*5) calc(var(--sw)*6)}
@media (max-width:900px){.rrow{grid-template-columns:1fr}.hcols,.team{grid-template-columns:1fr}}
"""
