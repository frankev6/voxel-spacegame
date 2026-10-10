"""Alien hulls: REEF-like grown ships that live in the DRYDOCK and NOCTURNE worlds as enemies.

They sit on the same 0.5 m grid as everything else. Each cell stores a small signed distance (one byte in game)
instead of a block shape, and a surface-nets mesher turns the grid into a smooth shell in one pass: no per-voxel
smoothing step, no subdivision. Damage removes cells and the crater stays organic for free. Spines, sacs, eyes and
tendrils are the alien "blocks" placed on top."""

import math

import numpy as np
from mathutils import Vector

from ld_geom import CELL, Acc, surface_nets, sphere, cyl, lathe, hash01


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
    def __init__(self, name, seed=1, colourway="bone"):
        self.name = name
        self.seed = seed
        self.colourway = colourway
        self.prims = []         # (op, kind, args, k)
        self.craters = []
        self.parts = []         # (kind, args): spines, sacs, eyes, tendrils, jets
        self.plumes = []
        self.field = None

    def add(self, kind, *args, k=1.5):
        self.prims.append(("add", kind, args, k))

    def sub(self, kind, *args, k=0.8):
        self.prims.append(("sub", kind, args, k))

    def part(self, kind, *args):
        self.parts.append((kind, args))

    def _bbox(self):
        lo = np.array([1e9] * 3)
        hi = np.array([-1e9] * 3)
        for op, kind, args, k in self.prims:
            if op != "add":
                continue
            if kind == "ell":
                c, r = np.array(args[0]), np.array(args[1])
                lo, hi = np.minimum(lo, c - r), np.maximum(hi, c + r)
            else:
                a, b, ra, rb = args
                m = max(ra, rb)
                lo = np.minimum(lo, np.minimum(a, b) - m)
                hi = np.maximum(hi, np.maximum(a, b) + m)
        return np.floor(lo - 3).astype(int), np.ceil(hi + 3).astype(int)

    def build_field(self):
        lo, hi = self._bbox()
        xs, ys, zs = [np.arange(lo[i], hi[i]) + 0.5 for i in range(3)]
        P = np.stack(np.meshgrid(xs, ys, zs, indexing="ij"), axis=-1)
        d = np.full(P.shape[:3], 1e3)
        for op, kind, args, k in self.prims:
            e = _ell(P, *args) if kind == "ell" else _cone(P, *args)
            if op == "add":
                d = _smin(d, e, k)
            else:
                d = -_smin(-d, e, k)        # smooth subtraction: smooth max(d, -e)
        for c, r in self.craters:
            q = np.linalg.norm(P - np.array(c), axis=-1)
            jag = (np.sin(P[..., 0] * 1.7 + self.seed) * np.sin(P[..., 1] * 1.3) * np.sin(P[..., 2] * 2.1)) * 0.9
            d = np.maximum(d, r + jag - q)
        idx = np.argwhere(d < 1.6)
        self.field = {(int(lo[0] + i), int(lo[1] + j), int(lo[2] + k)): float(d[i, j, k]) for i, j, k in idx}
        return self.field

    def carve(self, centre_cells, radius_cells, seed=0):
        self.craters.append((tuple(centre_cells), radius_cells))
        self.field = None
        return []

    def solid(self):
        if self.field is None:
            self.build_field()
        return [c for c, v in self.field.items() if v < 0]

    def bounds(self):
        cells = self.solid()
        a = np.array(cells)
        return Vector(a.min(axis=0)) * CELL, Vector(a.max(axis=0) + 1) * CELL

    # --------------------------------------------------------------------------------------------------------------
    def realize(self, S, acc, group, plumes=True):
        cw = self.colourway
        if self.field is None:
            self.build_field()
        verts, quads = surface_nets(self.field)
        verts_m = [(x * CELL, y * CELL, z * CELL) for x, y, z in verts]
        shell, flesh = [], []
        cr = [(Vector(c) * CELL, r * CELL) for c, r in self.craters]
        for q in quads:
            cen = sum((Vector(verts_m[i]) for i in q), Vector()) / 4
            (flesh if any((cen - c).length < r + 0.9 for c, r in cr) else shell).append(q)
        acc.add(group, f"a_shell@{cw}", (verts_m, shell), None, True)
        if flesh:
            acc.add(group, f"a_flesh@{cw}", (verts_m, flesh), None, True)
        for kind, args in self.parts:
            PARTS[kind](self, acc, group, cw, *args)
        if plumes:
            from ld_ships import plume
            for p, d, r, L in self.plumes:
                plume(acc, group, Vector(p) * CELL, Vector(d).normalized(), r * CELL, L * CELL, S)
        return []


# alien "blocks" ------------------------------------------------------------------------------------------------------

def _spine(al, acc, group, cw, base, tip, r):
    a, b = Vector(base) * CELL, Vector(tip) * CELL
    acc.add(group, f"a_spine@{cw}", cyl(tuple(a), tuple(b), r * CELL, 10, r1=0.02), None, True)
    acc.add(group, f"a_spine@{cw}", sphere(tuple(a), r * CELL * 1.25, 10, 5), None, True)


def _sac(al, acc, group, cw, c, r):
    c = Vector(c) * CELL
    acc.add(group, f"a_glow@{cw}", sphere(tuple(c), r * CELL, 16, 8, (1, 1, 0.8)), None, True)
    acc.add(group, f"a_spine@{cw}", sphere(tuple(c), r * CELL * 1.12, 16, 8, (1.05, 1.05, 0.55)), None, True)


def _eye(al, acc, group, cw, c, r):
    c = Vector(c) * CELL
    acc.add(group, f"a_glow@{cw}", sphere(tuple(c), r * CELL, 16, 8), None, True)
    acc.add(group, f"a_spine@{cw}", lathe([(r * CELL * 1.5, 0), (r * CELL * 1.15, r * CELL * 0.5), (r * CELL * 0.9, r * CELL * 0.8)],
                                         tuple(c - Vector((0, -r * CELL * 0.2, 0))), (0, 1, 0), 16, False, False), None, True)


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
