"""Alien hulls: REEF-like grown ships that live in the DRYDOCK and NOCTURNE worlds as enemies.

They sit on the same 0.5 m grid as everything else. Each cell stores a small signed distance (one byte in game)
instead of a block shape, and a surface-nets mesher turns the grid into a smooth shell in one pass: no per-voxel
smoothing step, no subdivision. Damage removes cells and the crater stays organic for free. Spines, sacs, eyes and
tendrils are the alien "blocks" placed on top."""

import math

import numpy as np
from mathutils import Vector

from ld_geom import CELL, Acc, surface_nets_grid, sphere, cyl, lathe, hash01


def _ell(P, c, r):
    q = (P - np.array(c)) / np.array(r)
    return (np.linalg.norm(q, axis=-1) - 1.0) * min(r)


def _cone(P, a, b, ra, rb):
    a, b = np.array(a, float), np.array(b, float)
    ba = b - a
    t = np.clip(((P - a) @ ba) / (ba @ ba), 0.0, 1.0)
    proj = a + t[..., None] * ba
    return np.linalg.norm(P - proj, axis=-1) - (ra + (rb - ra) * t)


def _smin(a, b, k):
    h = np.clip(0.5 + 0.5 * (b - a) / k, 0.0, 1.0)
    return b * (1 - h) + a * h - k * h * (1 - h)


class Alien:
    """An alien hull: smooth signed-distance primitives (ellipsoids, round cones, tori) unioned or carved with soft
    blends, sampled on the cell grid (step = cells per sample; 1 for ships, more for a behemoth seen from far)."""

    def __init__(self, name, seed=1, colourway="bone", step=1, skin=1.0):
        self.name = name
        self.seed = seed
        self.colourway = colourway
        self.step = step
        self.skin = skin        # carapace pattern scale (bigger aliens get bigger plates)
        self.prims = []         # (op, kind, args, k)
        self.craters = []
        self.parts = []         # (kind, args): spines, sacs, eyes, tendrils
        self.plumes = []
        self.lumps = None       # (amplitude, wavelength) organic surface noise, in cells
        self.grid = None

    def add(self, kind, *args, k=1.5):
        self.prims.append(("add", kind, args, k))

    def sub(self, kind, *args, k=0.8):
        self.prims.append(("sub", kind, args, k))

    def part(self, kind, *args):
        self.parts.append((kind, args))

    @staticmethod
    def _pbox(kind, args):
        if kind == "ell":
            c, r = np.array(args[0], float), np.array(args[1], float)
            return c - r, c + r
        if kind == "torus":
            c, n, R, r = args
            c = np.array(c, float)
            e = R + r
            return c - e, c + e
        a, b, ra, rb = args
        a, b = np.array(a, float), np.array(b, float)
        m = max(ra, rb)
        return np.minimum(a, b) - m, np.maximum(a, b) + m

    def _bbox(self):
        lo = np.array([1e9] * 3)
        hi = np.array([-1e9] * 3)
        for op, kind, args, k in self.prims:
            if op == "add":
                a, b = self._pbox(kind, args)
                lo, hi = np.minimum(lo, a), np.maximum(hi, b)
        pad = 3 * self.step + (self.lumps[0] if self.lumps else 0)
        return np.floor(lo - pad), np.ceil(hi + pad)

    def build_field(self):
        st = self.step
        lo, hi = self._bbox()
        n = np.maximum(((hi - lo) / st).astype(int) + 1, 2)
        d = np.full(tuple(n), 1e3, dtype=np.float32)
        axes = [lo[i] + (np.arange(n[i]) + 0.5) * st for i in range(3)]
        for op, kind, args, k in self.prims:
            a, b = self._pbox(kind, args)
            m = k + 2.0 * st + 2.0
            i0 = [max(0, int((a[i] - m - lo[i]) / st)) for i in range(3)]
            i1 = [min(n[i], int((b[i] + m - lo[i]) / st) + 2) for i in range(3)]
            if any(i1[i] <= i0[i] for i in range(3)):
                continue
            P = np.stack(np.meshgrid(axes[0][i0[0]:i1[0]], axes[1][i0[1]:i1[1]], axes[2][i0[2]:i1[2]], indexing="ij"), axis=-1)
            e = PRIM_SDF[kind](P, *args)
            sl = (slice(i0[0], i1[0]), slice(i0[1], i1[1]), slice(i0[2], i1[2]))
            if op == "add":
                d[sl] = _smin(d[sl], e, k)
            else:
                d[sl] = -_smin(-d[sl], e, k)        # smooth subtraction: smooth max(d, -e)
        if self.lumps:
            amp, wl = self.lumps
            P = np.stack(np.meshgrid(*axes, indexing="ij"), axis=-1)
            w = 2 * math.pi / wl
            nz = (np.sin(P[..., 0] * w * 1.0 + self.seed) * np.sin(P[..., 1] * w * 0.83 + 1.7) * np.sin(P[..., 2] * w * 1.21 + 0.4)
                  + 0.5 * np.sin(P[..., 0] * w * 2.3 + 2.0) * np.sin(P[..., 1] * w * 2.1) * np.sin(P[..., 2] * w * 1.9 + 1.1))
            d += (amp * nz * (np.abs(d) < amp * 4 + 2 * st)).astype(np.float32)
            del P
        for c, r in self.craters:
            P = np.stack(np.meshgrid(*axes, indexing="ij"), axis=-1)
            q = np.linalg.norm(P - np.array(c), axis=-1)
            jag = (np.sin(P[..., 0] * 1.7 + self.seed) * np.sin(P[..., 1] * 1.3) * np.sin(P[..., 2] * 2.1)) * 0.9
            d = np.maximum(d, r + jag - q).astype(np.float32)
            del P
        # grid values in cells (the mesher interpolates in grid units, so divide by the step)
        self.grid = d / st
        self.lo = lo
        return self.grid

    def carve(self, centre_cells, radius_cells, seed=0):
        self.craters.append((tuple(centre_cells), radius_cells))
        self.grid = None
        return []

    def bounds(self):
        if self.grid is None:
            self.build_field()
        idx = np.argwhere(self.grid < 0)
        a = self.lo + idx.min(axis=0) * self.step
        b = self.lo + (idx.max(axis=0) + 1) * self.step
        return Vector(a) * CELL, Vector(b) * CELL

    def solid_count(self):
        if self.grid is None:
            self.build_field()
        return int((self.grid < 0).sum()) * self.step ** 3

    # --------------------------------------------------------------------------------------------------------------
    def realize(self, S, acc, group, plumes=True):
        cw = self.colourway
        if self.grid is None:
            self.build_field()
        verts, quads = surface_nets_grid(self.grid, tuple(self.lo), self.step)
        verts_m = [(x * CELL, y * CELL, z * CELL) for x, y, z in verts]
        shell, flesh = [], []
        cr = [(Vector(c) * CELL, r * CELL) for c, r in self.craters]
        if cr:
            for q in quads:
                cen = sum((Vector(verts_m[i]) for i in q), Vector()) / 4
                (flesh if any((cen - c).length < r + 0.9 for c, r in cr) else shell).append(q)
        else:
            shell = quads
        sk = f"#{self.skin:g}" if self.skin != 1.0 else ""
        acc.add(group, f"a_shell@{cw}{sk}", (verts_m, shell), None, True)
        if flesh:
            acc.add(group, f"a_flesh@{cw}", (verts_m, flesh), None, True)
        for kind, args in self.parts:
            PARTS[kind](self, acc, group, cw, *args)
        if plumes:
            from ld_ships import plume
            for p, d, r, L in self.plumes:
                plume(acc, group, Vector(p) * CELL, Vector(d).normalized(), r * CELL, L * CELL, S)
        return []


def _torus(P, c, nrm, R, r):
    c = np.array(c, float)
    n_ = np.array(nrm, float)
    n_ /= np.linalg.norm(n_)
    q = P - c
    h = q @ n_
    rad = np.linalg.norm(q - h[..., None] * n_, axis=-1)
    return np.sqrt((rad - R) ** 2 + h ** 2) - r


PRIM_SDF = {"ell": _ell, "cone": _cone, "torus": _torus}


# alien "blocks" ------------------------------------------------------------------------------------------------------

def _spine(al, acc, group, cw, base, tip, r):
    a, b = Vector(base) * CELL, Vector(tip) * CELL
    acc.add(group, f"a_spine@{cw}", cyl(tuple(a), tuple(b), r * CELL, 10, r1=0.02), None, True)
    acc.add(group, f"a_spine@{cw}", sphere(tuple(a), r * CELL * 1.25, 10, 5), None, True)


def _sac(al, acc, group, cw, c, r):
    c = Vector(c) * CELL
    acc.add(group, f"a_glow@{cw}", sphere(tuple(c), r * CELL, 16, 8, (1, 1, 0.8)), None, True)
    acc.add(group, f"a_spine@{cw}", sphere(tuple(c), r * CELL * 1.12, 16, 8, (1.05, 1.05, 0.55)), None, True)


def _eye(al, acc, group, cw, c, r, d=(0, 1, 0)):
    c = Vector(c) * CELL
    dv = Vector(d).normalized()
    acc.add(group, f"a_glow@{cw}", sphere(tuple(c), r * CELL, 16, 8), None, True)
    acc.add(group, f"a_spine@{cw}", lathe([(r * CELL * 1.5, 0), (r * CELL * 1.15, r * CELL * 0.5), (r * CELL * 0.9, r * CELL * 0.8)],
                                         tuple(c - dv * (r * CELL * 0.2)), tuple(dv), 16, False, False), None, True)


def _tendril(al, acc, group, cw, pts, r0, r1):
    pts = [Vector(p) * CELL for p in pts]
    n = len(pts) - 1
    for i in range(n):
        ra = (r0 + (r1 - r0) * i / n) * CELL
        rb = (r0 + (r1 - r0) * (i + 1) / n) * CELL
        acc.add(group, f"a_spine@{cw}", cyl(tuple(pts[i]), tuple(pts[i + 1]), ra, 8, r1=rb), None, True)
        acc.add(group, f"a_spine@{cw}", sphere(tuple(pts[i + 1]), rb, 8, 4), None, True)
    acc.add(group, f"a_glow@{cw}", sphere(tuple(pts[-1]), r1 * CELL * 1.6, 8, 4), None, True)


PARTS = {"spine": _spine, "sac": _sac, "eye": _eye, "tendril": _tendril}


# designs --------------------------------------------------------------------------------------------------------------

def alien_light(colourway="bone"):
    """HUSK: a manta-like skimmer with cephalic horns, a ridged back, sac rows on the wings and a whip tail."""
    a = Alien("HUSK", 71, colourway)
    a.add("ell", (0, 14, 3), (15, 13, 2.6), k=2.0)
    a.add("ell", (0, 13, 5), (5, 10, 3.2), k=2.5)
    for sx in (-1, 1):
        a.add("cone", (sx * 4.5, 22, 3.2), (sx * 6.5, 33, 3.8), 2.0, 0.5, k=1.5)
        a.add("cone", (sx * 3.5, 12, 4.5), (sx * 13.5, 7, 3.2), 1.3, 0.5, k=1.2)
        a.add("cone", (sx * 3.5, 18, 4.2), (sx * 12.5, 18, 3.0), 1.1, 0.4, k=1.2)
    a.add("cone", (0, 3, 3.5), (0, -14, 4.5), 2.2, 0.35, k=2.0)
    a.sub("ell", (0, 27, 3.2), (2.6, 2.2, 1.8))
    for i in range(5):
        y = 6 + i * 3.5
        a.part("spine", (0, y, 7.6 - abs(i - 2) * 0.5), (0, y - 3.5, 11.5 - abs(i - 2) * 0.8), 0.45)
    for sx in (-1, 1):
        for i in range(4):
            a.part("sac", (sx * (6.5 + i * 2.0), 15 - i * 2.2, 3.4), 0.75)
        a.part("eye", (sx * 2.2, 26.5, 4.6), 0.6)
        a.part("tendril", [(sx * 1.5, 26, 1.5), (sx * 2.2, 29, -1.5), (sx * 1.5, 32, -3.5), (sx * 2.5, 35, -4.5)], 0.45, 0.12)
    a.part("sac", (0, 27, 2.6), 1.4)
    a.plumes = [((sx * 3.5, 1.0, 2.4), (sx * 0.15, -1, 0), 1.0, 7.0) for sx in (-1, 1)]
    return a


def alien_heavy(colourway="bone"):
    """BROODMOTHER: an armoured isopod the size of a frigate. Overlapping carapace plates with swept side spines,
    a broad head shield with sweeping horns, a split tail, sac rows, glowing eyes and legs hanging underneath."""
    a = Alien("BROODMOTHER", 72, colourway)
    n = 9
    segs = []
    for i in range(n):
        t = i / (n - 1)
        y = 6 + i * 9.0
        r = 6.5 + 5.5 * math.sin(math.pi * (0.15 + 0.75 * t))
        segs.append((y, r))
    for y, r in segs:
        a.add("ell", (0, y, 8), (r * 0.8, 6.0, r * 0.55), k=1.2)                    # soft body
        a.add("ell", (0, y + 1.5, 8 + r * 0.32), (r * 1.18, 5.6, 2.1), k=0.5)       # carapace plate, overlapping backward
        for sx in (-1, 1):
            a.add("cone", (sx * r * 0.95, y + 1.5, 8 + r * 0.2), (sx * (r * 1.18 + 4.0), y - 3.5, 7.5), 1.6, 0.25, k=0.6)
    # head shield and horns
    a.add("ell", (0, 89, 9.5), (13.0, 9.0, 3.2), k=1.2)
    a.add("ell", (0, 92, 7.5), (6.5, 7.5, 4.5), k=1.5)
    for sx in (-1, 1):
        a.add("cone", (sx * 11.5, 86, 9.5), (sx * 17.0, 66, 9.0), 2.4, 0.35, k=0.8)
        a.add("cone", (sx * 4.0, 96, 9.5), (sx * 2.2, 107, 11.5), 1.8, 0.3, k=1.0)
    a.sub("ell", (0, 98.5, 5.5), (3.4, 3.0, 2.0))
    # tail plate with two long cerci
    a.add("ell", (0, -2, 8.5), (7.5, 5.5, 1.8), k=1.2)
    for sx in (-1, 1):
        a.add("cone", (sx * 3.0, -4, 8.5), (sx * 7.5, -22, 9.5), 1.5, 0.25, k=0.8)
    for i, (y, r) in enumerate(segs):
        for sx in (-1, 1):
            a.part("sac", (sx * r * 0.62, y + 1, 8 - r * 0.42), 0.8 + 0.25 * (i % 2))
            if 0 < i < n - 1:
                a.part("tendril", [(sx * r * 0.45, y, 8 - r * 0.45), (sx * (r * 0.75), y + 1, 2.5), (sx * (r * 0.8), y + 3.5, -1.5),
                                   (sx * (r * 0.7), y + 5.5, -3.5)], 0.55, 0.14)
        if 1 <= i <= n - 2:
            a.part("spine", (0, y + 1.5, 8 + r * 0.32 + 1.8), (0, y - 3.5, 8 + r * 0.32 + 6.0 - abs(i - 4) * 0.6), 0.55)
    for sx in (-1, 1):
        for j in range(3):
            a.part("eye", (sx * (3.0 + j * 1.6), 95.5 - j * 1.2, 11.6 - j * 0.3), 0.55 - j * 0.1)
    a.part("sac", (0, 98.5, 5.6), 2.2)
    a.plumes = [((sx * 2.5, -6, 7), (sx * 0.1, -1, -0.05), 1.4, 11.0) for sx in (-1, 1)]
    return a


def _arm_path(a, prof, K=1.0, phase=0.0):
    """Points of a limb that leaves the centre at azimuth a (radians) along a (radial, z) profile."""
    ca, sa = math.cos(a), math.sin(a)
    return [(ca * r * K, sa * r * K, z * K) for r, z in prof]


def alien_lamprey(colourway="bone"):
    """LAMPREY: a 100 m eel-shaped boarder. No eyes; the whole front is a round funnel mouth lined with rings of teeth
    that latches onto a hull and bores in. Seven glowing gill pores along each flank."""
    a = Alien("LAMPREY", 73, colourway)
    a.lumps = (0.35, 9.0)
    ys = list(range(-92, 101, 8))

    def xc(y):
        return 7.0 * math.sin(y * 0.032 + 0.6) * (1.0 - max(0.0, y - 40) / 80.0)

    def rad(y):
        if y > 70:
            return 11.5
        if y > 20:
            return 12.5
        return max(1.6, 12.5 - (20 - y) * 0.095)

    for y0, y1 in zip(ys, ys[1:]):
        a.add("cone", (xc(y0), y0, 0), (xc(y1), y1, 0), rad(y0), rad(y1), k=3.0)
    # dorsal and ventral fin ridges, tail fin
    for y in range(-80, 40, 6):
        a.add("ell", (xc(y), y, rad(y) * 0.92), (0.7, 5.0, 2.2 + 0.03 * (40 - y) * 0.4), k=1.4)
    for y in range(-84, -30, 6):
        a.add("ell", (xc(y), y, -rad(y) * 0.92), (0.7, 5.0, 2.6), k=1.4)
    a.add("ell", (xc(-92), -96, 0), (0.8, 12.0, 7.5), k=2.0)
    # the funnel mouth: a flared disc, hollowed into a cone, with a thick lip
    a.add("cone", (0, 96, 0), (0, 108, 0), 11.5, 15.5, k=2.0)
    a.sub("cone", (0, 113, 0), (0, 95, 0), 13.8, 2.6, k=0.8)
    a.add("torus", (0, 108.2, 0), (0, 1, 0), 14.0, 1.7, k=1.0)

    def r_in(y):
        return 2.6 + (13.8 - 2.6) * (y - 95) / 18.0

    for ring_i, (y, n) in enumerate(((107.0, 22), (104.0, 18), (101.0, 14), (98.5, 10))):
        rr = r_in(y)
        L = 2.2 - ring_i * 0.35
        for i in range(n):
            t = 2 * math.pi * (i + 0.5 * (ring_i % 2)) / n
            radial = Vector((math.cos(t), 0, math.sin(t)))
            base = Vector((0, y, 0)) + radial * (rr + 0.25)
            tip = base - radial * L + Vector((0, -0.45 * L, 0))
            a.part("spine", tuple(base), tuple(tip), 0.42 - ring_i * 0.05)
    a.part("sac", (0, 96.5, 0), 2.6)
    # pits around the lip where eyes should be, glowing faintly
    for i in range(8):
        t = 2 * math.pi * (i + 0.5) / 8
        p = (math.cos(t) * 15.2, 106.0, math.sin(t) * 15.2)
        a.sub("ell", p, (1.0, 0.9, 1.0), k=0.4)
        a.part("eye", (p[0] * 0.97, p[1] - 0.2, p[2] * 0.97), 0.42)
    # seven gill pores per side
    for sx in (-1, 1):
        for i in range(7):
            y = 76 - i * 4.6
            th = math.radians(12)
            r = rad(y)
            p = (xc(y) + sx * r * math.cos(th), y, r * math.sin(th))
            a.sub("ell", p, (1.7, 1.25, 1.7), k=0.5)
            a.part("sac", (xc(y) + sx * (r - 0.9) * math.cos(th), y, (r - 0.9) * math.sin(th)), 0.95)
    # barbs along the dorsal ridge
    for y in range(-70, 40, 10):
        z = rad(y) * 0.92 + 2.0
        a.part("spine", (xc(y), y, z), (xc(y), y - 4.5, z + 3.5), 0.4)
    a.plumes = [((xc(-96), -104, 0), (0, -1, 0), 2.2, 18.0)]
    return a


def alien_crinoid(colourway="bone"):
    """CRINOID: a sea lily the size of a frigate. No front and no back: five-fold symmetric, a cup-shaped body with a
    ring of eyes around a glowing mouth, ten feathered arms curling up like a closing hand, a jointed stalk trailing."""
    K = 1.5
    a = Alien("CRINOID", 74, colourway)
    a.lumps = (0.25, 6.0)
    a.add("ell", (0, 0, 0), (9 * K, 9 * K, 7 * K), k=2.0)
    a.add("ell", (0, 0, 5 * K), (7.2 * K, 7.2 * K, 4.2 * K), k=2.0)
    a.add("torus", (0, 0, 8.6 * K), (0, 0, 1), 4.2 * K, 1.1 * K, k=1.0)
    a.sub("ell", (0, 0, 9.6 * K), (3.6 * K, 3.6 * K, 2.4 * K), k=0.8)
    a.part("sac", (0, 0, 8.2 * K), 2.7 * K)
    for i in range(10):
        t = 2 * math.pi * (i + 0.5) / 10
        p = (math.cos(t) * 6.4 * K, math.sin(t) * 6.4 * K, 7.4 * K)
        a.part("eye", p, 0.55 * K, (math.cos(t) * 0.5, math.sin(t) * 0.5, 1.0))
    base = [(8, 2), (15, 3), (22, 5)]
    branch = [(29, 8), (36, 12), (41, 17), (43, 23), (41, 28), (37, 31), (33, 31.5)]
    for i in range(5):
        az = math.pi / 2 + i * 2 * math.pi / 5
        stem = _arm_path(az, base, K)
        rads = [3.4, 3.0, 2.7]
        for p, q, r0, r1 in zip(stem, stem[1:], rads, rads[1:]):
            a.add("cone", p, q, r0 * K * 0.5, r1 * K * 0.5, k=0.7)
            a.add("ell", q, (r1 * K * 0.62,) * 3, k=0.4)
        for side in (-1, 1):
            path = [stem[-1]] + _arm_path(az + side * math.radians(15), branch, K)
            br = [2.6 - 1.9 * j / (len(path) - 1) for j in range(len(path))]
            for j, (p, q) in enumerate(zip(path, path[1:])):
                a.add("cone", p, q, br[j] * K * 0.5, br[j + 1] * K * 0.5, k=0.5)
                a.add("ell", q, (br[j + 1] * K * 0.6,) * 3, k=0.35)
                # pinnules: a feather of thin spines on both sides of every segment
                P, Q = Vector(p), Vector(q)
                d = (Q - P).normalized()
                lat = d.cross(Vector((0, 0, 1)))
                if lat.length < 1e-3:
                    lat = Vector((1, 0, 0))
                lat.normalize()
                L = (5.5 - 3.5 * j / len(path)) * K * 0.7
                for s2 in (-1, 1):
                    for f in (0.3, 0.75):
                        b0 = P + (Q - P) * f + lat * s2 * br[j] * K * 0.4
                        tip = b0 + (lat * s2 * 0.8 + d * 0.55 + Vector((0, 0, 0.35))).normalized() * L
                        a.part("spine", tuple(b0), tuple(tip), 0.16 * K)
            tipp = path[-1]
            a.part("sac", tipp, 0.7 * K)
    # trailing stalk with whorls of cirri at the joints
    stalk = [(0, 0, -6), (0, -2, -15), (0, -6, -24), (0, -12, -32), (0, -20, -38), (0, -30, -42), (0, -42, -44), (0, -54, -44)]
    stalk = [tuple(v * K for v in p) for p in stalk]
    rs = [3.0, 2.6, 2.3, 2.0, 1.8, 1.6, 1.4, 1.2]
    for j, (p, q) in enumerate(zip(stalk, stalk[1:])):
        a.add("cone", p, q, rs[j] * K * 0.6, rs[j + 1] * K * 0.6, k=0.6)
        a.add("ell", q, (rs[j + 1] * K * 0.8,) * 3, k=0.4)
        if j >= 1:
            for m in range(5):
                t = 2 * math.pi * (m + 0.5 * (j % 2)) / 5
                Q = Vector(q)
                o = Vector((math.cos(t), 0.25, math.sin(t)))
                pts = [tuple(Q + o * rs[j] * K * 0.6), tuple(Q + o * 3.5 * K + Vector((0, -1, 0))), tuple(Q + o * 5.5 * K + Vector((0, -3.5, -1.5)))]
                a.part("tendril", pts, 0.38 * K, 0.1 * K)
    for i in range(10):
        t = 2 * math.pi * i / 10
        a.part("sac", (math.cos(t) * 7.0 * K, math.sin(t) * 7.0 * K, -3.5 * K), 0.9 * K)
    return a


def alien_gravid(colourway="bone"):
    """GRAVID: the behemoth, a living carrier almost half a kilometre long. A swollen, banded brood body pitted with
    egg chambers, an armoured head with an eye cluster and sweeping horns, sail spines along the back, birthing
    orifices underneath and a forest of trailing tendrils. Sampled at 2 m (step 4) in this render."""
    a = Alien("GRAVID", 75, colourway, step=4, skin=6.0)
    a.lumps = (2.2, 34.0)
    segs = []
    n = 13
    for i in range(n):
        t = i / (n - 1)
        y = -380 + i * 56
        r = 60 + 92 * math.sin(math.pi * (0.08 + 0.84 * t)) ** 0.8
        segs.append((y, r))
    for y, r in segs:
        a.add("ell", (0, y, 0), (r * 1.08, 40, r * 0.82), k=18.0)
    for y, r in segs[1:-1]:
        a.add("torus", (0, y + 26, 0), (0, 1, 0), r * 0.98, 9.0, k=6.0)
    # brood pits on the flanks: a honeycomb of egg chambers
    for sx in (-1, 1):
        for i, (y, r) in enumerate(segs[2:-2]):
            for j, th in enumerate((-28, -8, 12, 32)):
                yy = y + (14 if j % 2 else -6)
                tt = math.radians(th)
                p = (sx * r * 1.06 * math.cos(tt), yy, r * 0.8 * math.sin(tt))
                a.sub("ell", p, (8.5, 8.5, 8.5), k=3.0)
                q = Vector(p) - Vector((sx * math.cos(tt), 0, math.sin(tt) * 0.8)).normalized() * 3.5
                a.part("sac", tuple(q), 4.6)
    # thorax and the armoured head
    a.add("ell", (0, 360, 18), (96, 74, 74), k=20.0)
    a.add("ell", (0, 424, 46), (112, 46, 30), k=10.0)
    a.add("ell", (0, 452, 10), (54, 40, 44), k=12.0)
    for sx in (-1, 1):
        a.add("cone", (sx * 70, 432, 56), (sx * 128, 560, 132), 16, 2.5, k=8.0)
        a.add("cone", (sx * 96, 410, 40), (sx * 170, 470, 70), 11, 2.0, k=6.0)
        a.add("cone", (sx * 26, 470, -6), (sx * 18, 560, -40), 11, 2.0, k=6.0)
    for row, (zz, cnt) in enumerate(((52, 5), (36, 4), (22, 3))):
        for k_ in range(cnt):
            x = (k_ - (cnt - 1) / 2) * 15
            a.part("eye", (x, 466 - row * 2, zz), 4.6 - row * 0.6)
    # birthing orifices underneath
    for sx in (-1, 1):
        for y in (-120, 60, 220):
            c = (sx * 58, y, -112)
            a.add("torus", c, (0, 0, 1), 20.0, 7.0, k=5.0)
            a.sub("ell", (c[0], c[1], c[2] + 4), (16, 16, 26), k=4.0)
            a.part("sac", (c[0], c[1], c[2] + 18), 11.0)
    # sail spines along the back
    for i, (y, r) in enumerate(segs[1:-1]):
        h = 60 + 70 * math.sin(math.pi * (i + 0.5) / (n - 2))
        a.part("spine", (0, y + 10, r * 0.78), (0, y - 40, r * 0.78 + h), 9.0)
        for sx in (-1, 1):
            a.part("spine", (sx * r * 0.7, y + 10, r * 0.55), (sx * (r * 0.7 + 40), y - 30, r * 0.55 + 46), 5.5)
    # trailing tendrils
    for i in range(26):
        y = -320 + i * 26
        sx = -1 if i % 2 else 1
        x = sx * (25 + (i * 37) % 70)
        pts = [(x, y, -88)]
        for k_ in range(1, 8):
            pts.append((x + sx * 6 * math.sin(k_ * 0.9 + i), y - k_ * 34, -88 - k_ * 30 - 8 * math.sin(k_ * 1.3 + i)))
        a.part("tendril", pts, 4.2, 0.6)
    a.plumes = []
    return a
