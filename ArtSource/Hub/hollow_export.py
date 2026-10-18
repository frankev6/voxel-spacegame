#!/usr/bin/env python3
"""DEV-023 option E, THE HOLLOW: the design (hub_option_e.py) exported as game-ready voxel data.

Run:  python3 ArtSource/Hub/hollow_export.py
Out:  ArtSource/Hub/hollow/hollow_layout.json, check_top.png, check_section.png

Game conventions: x east, y up, z north (game (x, z) = design (x, y)), metres, 2 m cells, cubes of side 1-4 cells,
every piece's origin at y = 0 (heights live in the j index). The rock is compressed vertically for the 55-degree
game camera (tunnels and caverns between -24 and +24, the top +40..+124, cliffs down to about -72), stored as a shell
only and split at +24 into "low" and "high" pieces (the high ones hide in the cutaway). See docs in the JSON summary.

JSON (version 1): cell, spawn, bounds, forts (x, z, rate, yaw), guns (all at y = 0), lights, neon, signs, stations,
moored, traffic, gunships, inside (16 m cutaway mask), beacons, boulders (the approach field's rocks, x z r: not voxelised,
for the cell budget), pieces (id, name, layer low/high/struct/fort, origin [x, 0, z], motion, fort, blocks [i, j, k, s, c, m]).
The rock, the forts, the Den hulk and most structures sit on a 4 m lattice so they pack into 2- and 4-cell blocks; 2 m cells
are used for gun ledges, gun clear zones, embrasures and the docks' arms. Every structure piece is split at +24: the part
above is a "high" piece (it hides with the rock's high pieces in the cutaway).

Three stages, because the system python3 has Pillow but no numpy, and Blender's bundled python has numpy but no Pillow
(and the design modules import Pillow):
  1. design   (this python: imports hub_option_e, runs option_e(), dumps the design and the rock's inputs)
  2. compute  (a python with numpy: this one if it has it, else Blender's bundled one, re-executed automatically;
               set HOLLOW_NUMPY_PY to choose another): voxelises, packs, checks, writes the JSON
  3. draw     (this python, Pillow): the two check images
"""
import glob
import json
import math
import os
import random
import shutil
import subprocess
import sys
import tempfile
import time

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "hollow")
OUT_JSON = os.path.join(OUT, "hollow_layout.json")

CELL = 2.0
CC = 4.0                   # the coarse lattice: 2 x 2 x 2 cells
TILE = 128                 # cells per rock tile side (256 m)
SPLIT = 12                 # cell index of y = +24: the cutaway plane
MAX_INST = 30000           # engine limit: blocks with s >= 2 per piece
np = None                  # numpy, bound in the compute stage


# ======================================================================== stage 1: the design
def stage_design(path):
    sys.path.insert(0, HERE)
    import hub_option_e as HE
    cap = {}
    orig = HE.build_rock

    def spy(tunnels, caverns, craters):            # option_e() keeps its tunnels and caverns local: catch them here
        cap.update(tunnels=tunnels, caverns=caverns, craters=craters)
        return orig(tunnels, caverns, craters)
    HE.build_rock = spy
    try:
        opt = HE.option_e()
    finally:
        HE.build_rock = orig
    rng = random.Random(3)
    samples = []
    for _ in range(400):
        x, y = rng.uniform(-900, 900), rng.uniform(-620, 640)
        samples.append((x, y, HE.field(x, y), HE.surface(x, y)))
    d = dict(opt={k: v for k, v in opt.items() if k != "rock"}, tunnels=cap["tunnels"], caverns=cap["caverns"],
             craters=cap["craters"], lobes=HE.LOBES, samples=samples, rock_cols=len(opt["rock"]["cols"]))
    with open(path, "w") as f:
        json.dump(d, f)


# ======================================================================== stage 2: compute (numpy)
# ---------------------------------------------------------------- design geometry (bearing: degrees clockwise from north)
def polar(b, r, c=(0.0, 0.0)):
    a = math.radians(b)
    return (c[0] + r * math.sin(a), c[1] + r * math.cos(a))


def box(c, heading, length, width):
    f, r = polar(heading, length / 2), polar(heading + 90, width / 2)
    return [(c[0] + f[0] + r[0], c[1] + f[1] + r[1]), (c[0] + f[0] - r[0], c[1] + f[1] - r[1]),
            (c[0] - f[0] - r[0], c[1] - f[1] - r[1]), (c[0] - f[0] + r[0], c[1] - f[1] + r[1])]


def ngon(c, r, n, rot0=0.0):
    return [polar(rot0 + k * 360 / n, r, c) for k in range(n)]


def centroid(pts):
    return (sum(p[0] for p in pts) / len(pts), sum(p[1] for p in pts) / len(pts))


def seg_dist(p, a, b):
    ax, ay = b[0] - a[0], b[1] - a[1]
    L2 = ax * ax + ay * ay
    t = 0.0 if L2 == 0 else max(0.0, min(1.0, ((p[0] - a[0]) * ax + (p[1] - a[1]) * ay) / L2))
    return math.dist(p, (a[0] + ax * t, a[1] + ay * t))


def poly_dist(p, pts):
    return min(seg_dist(p, a, b) for a, b in zip(pts, pts[1:]))


def poly_len(pts):
    return sum(math.dist(a, b) for a, b in zip(pts, pts[1:]))


def resample(pts, step=60.0):
    out = [tuple(pts[0])]
    for a, b in zip(pts, pts[1:]):
        n = max(1, int(math.ceil(math.dist(a, b) / step)))
        for k in range(1, n + 1):
            out.append((a[0] + (b[0] - a[0]) * k / n, a[1] + (b[1] - a[1]) * k / n))
    return [(round(x, 1), round(z, 1)) for x, z in out]


def offset_line(pts, d, closed=False):
    """The polyline moved d m to its right (mitred corners)."""
    P = list(pts[:-1]) if closed and pts[0] == pts[-1] else list(pts)
    n = len(P)
    out = []
    for i in range(n):
        segs = []
        if closed or i > 0:
            segs.append((P[i - 1], P[i]))
        if closed or i < n - 1:
            segs.append((P[i], P[(i + 1) % n]))
        nx = ny = 0.0
        for a, b in segs:
            L = math.dist(a, b) or 1
            nx += (b[1] - a[1]) / L; ny += -(b[0] - a[0]) / L      # right normal
        L = math.hypot(nx, ny) or 1
        nx, ny = nx / L, ny / L
        cosh = 1.0
        if len(segs) == 2:
            (a, b), (_, c) = segs
            u = ((b[0] - a[0]) / (math.dist(a, b) or 1), (b[1] - a[1]) / (math.dist(a, b) or 1))
            cosh = max(0.5, abs(u[1] * nx - u[0] * ny))
        out.append((P[i][0] + nx * d / cosh, P[i][1] + ny * d / cosh))
    if closed:
        out.append(out[0])
    return out


# ---------------------------------------------------------------- the rock field (numpy, the design's formulas)
def noise(x, y, seed):
    v = 0.0
    for k, (fx, fy, a) in enumerate(((1 / 170, 1 / 230, 0.5), (1 / 83, 1 / 61, 0.3), (1 / 37, 1 / 43, 0.2))):
        v = v + a * np.sin(x * fx + seed * 1.7 + k) * np.cos(y * fy - seed * 0.9 + 2 * k)
    return v * 1.6


class Rockfield:
    def __init__(self, lobes, craters):
        self.lobes, self.craters = lobes, craters

    def field(self, x, y):
        f = None
        for c, rx, ry, h in self.lobes:
            g = h * (1 - ((x - c[0]) / rx) ** 2 - ((y - c[1]) / ry) ** 2)
            f = g if f is None else np.maximum(f, g)
        return f + 0.10 * noise(x, y, 1)

    def surface(self, x, y):
        s = np.sqrt(np.maximum(self.field(x, y), 0.0))
        return 430 * s * (0.8 + 0.3 * noise(x, y, 2)) + 60 * np.maximum(0.0, noise(x * 2.3, y * 2.3, 7)) * s + 10

    def top(self, x, y):
        """Design top including crater dents (the design has none today, but stays shallow dents if it gets some)."""
        t = self.surface(x, y)
        for c, r, depth in self.craters:
            d = np.hypot(x - c[0], y - c[1])
            t = t + np.where(d < r * 1.25, depth * 0.25 * np.maximum(0.0, 1 - np.abs(d - r) / (r * 0.25))
                             - np.where(d < r, depth * (1 - (d / r) ** 2), 0.0), 0.0)
        return t


def ytop_of(design_top):
    """Game height of the rock's top: +40..+124, the design's shape kept."""
    return 24.0 + np.clip(0.22 * (design_top - 24.0), 16.0, 100.0)


def soft_up(dz):
    """Heights above a base: true up to 24 m, compressed beyond."""
    return dz if dz <= 24 else 24 + (dz - 24) * 0.35


def soft(z):
    """Design z -> game y for things standing off the rock in open space."""
    if z >= 0:
        return min(soft_up(z), 110.0)
    return z if z >= -24 else max(-24 + (z + 24) * 0.35, -90.0)


# ---------------------------------------------------------------- voxel helpers
def pip(X, Z, pts):
    """Even-odd point in polygon, vectorised."""
    inside = np.zeros(np.broadcast(X, Z).shape, bool)
    n = len(pts)
    for a in range(n):
        x1, z1 = pts[a]; x2, z2 = pts[(a + 1) % n]
        if z1 == z2:
            continue
        cond = ((z1 > Z) != (z2 > Z)) & (X < (x2 - x1) * (Z - z1) / (z2 - z1) + x1)
        inside ^= cond
    return inside


def cell_range(lo, hi):
    a = int(math.floor(lo / CELL)); b = int(math.ceil(hi / CELL))
    return a, max(b, a + 1)


def jrange(y0, y1):
    a = int(round(y0 / CELL)); b = int(round(y1 / CELL))
    return a, max(b, a + 1)


def prism(pts, y0, y1):
    """Polygon footprint (game x, z) extruded over [y0, y1): (bool array [i, j, k], global cell origin)."""
    i0, i1 = cell_range(min(p[0] for p in pts), max(p[0] for p in pts))
    k0, k1 = cell_range(min(p[1] for p in pts), max(p[1] for p in pts))
    j0, j1 = jrange(y0, y1)
    cx = (np.arange(i0, i1) + 0.5) * CELL; cz = (np.arange(k0, k1) + 0.5) * CELL
    m = pip(cx[:, None], cz[None, :], pts)
    if not m.any():
        c = centroid(pts)
        m[min(max(int(c[0] / CELL) - i0, 0), m.shape[0] - 1), min(max(int(c[1] / CELL) - k0, 0), m.shape[1] - 1)] = True
    return np.repeat(m[:, None, :], j1 - j0, axis=1), (i0, j0, k0)


def prism_c(pts, y0, y1):
    """A prism on the 4 m lattice: (bool array [i, j, k] at 4 m, coarse origin)."""
    i0, i1 = int(math.floor(min(p[0] for p in pts) / CC)), int(math.ceil(max(p[0] for p in pts) / CC))
    k0, k1 = int(math.floor(min(p[1] for p in pts) / CC)), int(math.ceil(max(p[1] for p in pts) / CC))
    i1, k1 = max(i1, i0 + 1), max(k1, k0 + 1)
    j0 = int(round(y0 / CC)); j1 = max(int(round(y1 / CC)), j0 + 1)
    cx = (np.arange(i0, i1) + 0.5) * CC; cz = (np.arange(k0, k1) + 0.5) * CC
    m = pip(cx[:, None], cz[None, :], pts)
    if not m.any():
        c = centroid(pts)
        m[min(max(int(c[0] // CC) - i0, 0), m.shape[0] - 1), min(max(int(c[1] // CC) - k0, 0), m.shape[1] - 1)] = True
    return np.repeat(m[:, None, :], j1 - j0, axis=1), (i0, j0, k0)


def vshift(a, d):
    """a shifted by d cells along y (+ up), outside empty."""
    out = np.zeros_like(a)
    if d > 0:
        out[:, d:, :] = a[:, :-d, :]
    elif d < 0:
        out[:, :d, :] = a[:, -d:, :]
    else:
        out[:] = a
    return out


def around_void(v):
    """Cells round a cut: 4 m beside it, 6 m below it (a floor or a ledge), 4 m above it."""
    near = np.zeros_like(v)
    for jj in np.where(v.any(axis=(0, 2)))[0]:
        near[:, jj, :] = dilate2d(v[:, jj, :], 2.0, pad=False)
    for d in (-3, -2, -1, 1, 2):
        near |= vshift(v, d)
    return near


def up2(a):
    return a.repeat(2, 0).repeat(2, 1).repeat(2, 2)


def shift_and(a, axis, d):
    """a & a shifted by d along axis (outside counts as empty)."""
    out = np.zeros_like(a)
    src = [slice(None)] * 3; dst = [slice(None)] * 3
    if d > 0:
        src[axis] = slice(d, None); dst[axis] = slice(0, -d)
    else:
        src[axis] = slice(0, d); dst[axis] = slice(-d, None)
    out[tuple(dst)] = a[tuple(src)]
    return a & out


def erode(a, t, axes=(0, 1, 2)):
    b = a
    for ax in axes:
        for _ in range(t):
            b = shift_and(shift_and(b, ax, 1), ax, -1)
    return b


def shell(fill, t=2):
    """Hollow a solid larger than 8 m in every axis: keep a t-cell skin."""
    if min(fill.shape) <= 4:
        return fill.copy()
    return fill & ~erode(fill, t)


def dilate2d(a, r, pad=True):
    """2D dilation by a disk of radius r cells (outside the array counts as `pad`)."""
    R = int(math.ceil(r))
    p = np.pad(a, R, constant_values=pad)
    out = np.zeros_like(a)
    n0, n1 = a.shape
    for dx in range(-R, R + 1):
        for dz in range(-R, R + 1):
            if dx * dx + dz * dz <= r * r + 0.5:
                out |= p[R + dx:R + dx + n0, R + dz:R + dz + n1]
    return out


# packing schedule: (side, minimum required cells in the cube); all offsets are tried for each
OFFS = {s: [(a, b, c) for a in range(s) for b in range(s) for c in range(s)] for s in (2, 3, 4)}
EVEN = [(a, b, c) for a in (0, 2) for b in (0, 2) for c in (0, 2)]
SCHEDULE = ((4, 64, OFFS[4]), (4, 40, OFFS[4]), (3, 27, OFFS[3]), (3, 18, OFFS[3]), (2, 8, OFFS[2]), (2, 4, OFFS[2]), (2, 1, OFFS[2]))
# the rock (and anything on the 4 m lattice) packs on even offsets first, so it stays in 2- and 4-cell blocks
COARSE_SCHEDULE = ((4, 64, EVEN), (2, 8, [(0, 0, 0)]), (2, 1, [(0, 0, 0)]), (2, 1, OFFS[2]))


def pack(allowed, required, covered=None, schedule=SCHEDULE):
    """Greedy cube packing: covers every required cell; cubes stay inside allowed (required plus solid interior) and
    never overlap. Returns an int array of rows (i, j, k, s) local to the arrays."""
    if covered is None:
        covered = np.zeros(allowed.shape, bool)
    shape = allowed.shape
    out = []
    for s, t, offs in schedule:
        if (required & ~covered).sum() == 0:
            break
        for o in offs:
            n = [(shape[a] - o[a]) // s for a in range(3)]
            if min(n) <= 0:
                continue
            sl = tuple(slice(o[a], o[a] + n[a] * s) for a in range(3))
            free = allowed[sl] & ~covered[sl]
            ok = free.reshape(n[0], s, n[1], s, n[2], s).all(axis=(1, 3, 5))
            if not ok.any():
                continue
            rq = (required[sl] & free).reshape(n[0], s, n[1], s, n[2], s).sum(axis=(1, 3, 5))
            place = ok & (rq >= t)
            if not place.any():
                continue
            covered[sl] |= place.repeat(s, 0).repeat(s, 1).repeat(s, 2)
            idx = np.argwhere(place) * s + np.array(o)
            out.append(np.column_stack([idx, np.full(len(idx), s)]))
    rem = np.argwhere(required & ~covered)
    covered[required] = True
    out.append(np.column_stack([rem, np.ones(len(rem), int)]))
    return np.concatenate(out).astype(np.int64)


# ---------------------------------------------------------------- palette
HULL = dict(graphite=1, soot=11, steel=13, metal=14, brown=10, olive=18)
LIT = dict(red=21, amber=22, green=23, blue=20, pink=48, cyan=49, neon_amber=50)
SECTION = 46


class Solid:
    __slots__ = ("group", "col", "fill", "req", "lit", "litcol", "o", "name")

    def __init__(self, group, col, fill, o, req=None, lit=None, litcol=None, name=""):
        self.group, self.col, self.fill, self.o, self.name = group, col, fill, o, name
        self.req = fill.copy() if req is None else req
        self.lit, self.litcol = lit, litcol


class Exporter:
    def __init__(self, design):
        self.D = design
        self.opt = design["opt"]
        self.rf = Rockfield([(tuple(c), rx, ry, h) for c, rx, ry, h in design["lobes"]], design["craters"])
        self.log = []
        for x, y, f, s in design["samples"]:             # the numpy port must match the design's own formulas
            assert abs(float(self.rf.field(x, y)) - f) < 1e-9 and abs(float(self.rf.surface(x, y)) - s) < 1e-6, (x, y)
        self.read_design()

    # ------------------------------------------------------------ what the design placed
    def read_design(self):
        D, opt = self.D, self.opt
        self.tunnels = [([tuple(p) for p in pts], w) for pts, w in D["tunnels"]]
        self.caverns = [(tuple(c), r, fl, ce) for c, r, fl, ce in D["caverns"]]
        holo = {h["text"]: h for h in opt["holos"]}
        dist = {d["key"]: d for d in opt["districts"]}
        self.mouth = tuple(dist["maw"]["centre"])
        near = lambda p: min(range(len(self.caverns)), key=lambda i: math.dist(self.caverns[i][0], p))
        self.market_i, self.docks_i = near(holo["THE MARKET"]["at"]), near(holo["THE DOCKS"]["at"])
        self.market, self.market_r = self.caverns[self.market_i][0], self.caverns[self.market_i][1]
        self.docks, self.docks_r = self.caverns[self.docks_i][0], self.caverns[self.docks_i][1]
        self.dens = []
        for i, (c, r, fl, ce) in enumerate(self.caverns):
            if i in (self.market_i, self.docks_i):
                continue
            h = min((h for h in opt["holos"] if h["district"] == "fence"), key=lambda h: math.dist(h["at"], c))
            self.dens.append(dict(i=i, c=c, r=r, name=h["text"]))
        self.ring, self.ring_w = next((pts, w) for pts, w in self.tunnels if pts[0] == pts[-1])
        self.maw_t = min(self.tunnels, key=lambda t: min(p[1] for p in t[0]))[0]
        self.back = max(self.tunnels, key=lambda t: t[0][-1][0])[0]
        self.hulk = next(f for f in opt["fx"] if f["type"] == "hulk")
        self.forts = [f for f in opt["fx"] if f["type"] == "boulder" and "spin" in f]
        self.field_rocks = [f for f in opt["fx"] if f["type"] == "boulder" and "spin" not in f]
        self.station_c = (100.0, -10.0)                  # the centre the design's forts and outer works are laid round
        self.fort_b = [round(math.degrees(math.atan2(f["c"][0] - self.station_c[0], f["c"][1] - self.station_c[1])) % 360)
                       for f in self.forts]

    # ------------------------------------------------------------ the rock: classification at 2 m
    def classify(self):
        rf = self.rf
        # the rock is classified on a 4 m lattice (2 x 2 x 2 cells), so it packs into 2- and 4-cell blocks exactly
        X0, Z0, NX, NZ = -904.0, -624.0, 452, 317            # the design's rock range with a margin
        xs = X0 + CC * np.arange(NX) + CC / 2; zs = Z0 + CC * np.arange(NZ) + CC / 2
        GX, GZ = np.meshgrid(xs, zs, indexing="ij")
        foot = rf.field(GX, GZ) > 0
        ii = np.where(foot.any(1))[0]; kk = np.where(foot.any(0))[0]
        m = 3
        a0, a1 = max(ii[0] - m, 0), min(ii[-1] + m + 1, NX); b0, b1 = max(kk[0] - m, 0), min(kk[-1] + m + 1, NZ)
        self.rx0, self.rz0 = X0 + CC * a0, Z0 + CC * b0         # the rock grid's min corner (metres, a 4 m multiple)
        self.ri0, self.rk0 = int(round(self.rx0 / CELL)), int(round(self.rz0 / CELL))
        GX, GZ, foot = GX[a0:a1, b0:b1], GZ[a0:a1, b0:b1], foot[a0:a1, b0:b1]
        self.GX, self.GZ, self.foot = GX, GZ, foot
        kind = np.where(foot, 1, 0).astype(np.int8)          # 0 outside, 1 rock, 2 tunnel, 3 cavern
        cav = np.full(foot.shape, -1, np.int16)
        fj = np.full(foot.shape, -6, np.int16)               # floor, coarse index (y = -24)
        for idx, (c, r, fl, ce) in enumerate(self.caverns):
            mm = foot & (np.hypot(GX - c[0], GZ - c[1]) < r)
            if ce is None:
                continue                                      # a crater: a dent in the top only, never a hole
            kind[mm] = 3; cav[mm] = idx
            fj[mm] = -8 if idx == self.market_i else -6        # the Market's floor at -32
        self.tdist = []
        for pts, w in self.tunnels:
            d = np.full(foot.shape, np.inf)
            for (ax, ay), (bx, by) in zip(pts, pts[1:]):
                dx, dy = bx - ax, by - ay
                L2 = dx * dx + dy * dy
                t = np.clip(((GX - ax) * dx + (GZ - ay) * dy) / L2, 0, 1) if L2 > 0 else 0.0
                d = np.minimum(d, np.hypot(GX - (ax + dx * t), GZ - (ay + dy * t)))
            self.tdist.append(d)
            corr = d < w / 2 if not hasattr(self, "_corr") else (self._corr | (d < w / 2))
            self._corr = corr
            mm = foot & (kind == 1) & (d < w / 2)
            kind[mm] = 2
        # the Den: the rammed warship's hangar is cut into the rock where its stern lies in it
        hl = self.hulk_frame(GX, GZ)
        mm = foot & (np.abs(hl[1]) < self.hulk["width"] / 2 - 4) & (hl[0] > -self.hulk_body - 4) & (hl[0] < 4) & (kind == 1)
        kind[mm] = 3; cav[mm] = -2
        self.kind, self.cav, self.fj = kind, cav, fj
        self.inner = kind >= 2
        dtop = rf.top(GX, GZ)
        self.ytop = np.where(foot, ytop_of(dtop), 0.0)
        self.jt = np.round(self.ytop / CC).astype(np.int16)                   # coarse
        ybot = -72.0 + np.clip(7.5 * noise(GX * 1.3, GZ * 1.3, 5), -12, 12)
        self.jb = np.round(ybot / CC).astype(np.int16)
        # top shell 4 m, 8 m in patches (chunks)
        self.th = np.ones(foot.shape, np.int16)                                # top shell: 4 m
        self.jc0 = int(self.jb[foot].min()); self.jc1 = int(self.jt[foot].max())
        self.rj0, self.rj1 = 2 * self.jc0, 2 * self.jc1

    def hulk_frame(self, X, Z):
        """Axial (from the body's front face, + toward the bow) and lateral (+ right) coordinates of the Den hulk."""
        h = self.hulk
        L, W, hd = h["length"], h["width"], h["heading"]
        self.hulk_body = L * (0.78 if h.get("bow") else 1.0)
        bc = polar(hd, -(L - self.hulk_body) / 2, h["c"])
        front = polar(hd, self.hulk_body / 2, bc)
        u = (math.sin(math.radians(hd)), math.cos(math.radians(hd)))
        v = (u[1], -u[0])
        dx, dz = X - front[0], Z - front[1]
        return dx * u[0] + dz * u[1], dx * v[0] + dz * v[1]

    def col_kind(self, x, z):
        i = int(math.floor((x - self.rx0) / CC)); k = int(math.floor((z - self.rz0) / CC))
        if 0 <= i < self.kind.shape[0] and 0 <= k < self.kind.shape[1]:
            return int(self.kind[i, k]), int(self.cav[i, k])
        return 0, -1

    def ytop_at(self, x, z):
        return float(ytop_of(self.rf.top(x, z))) if float(self.rf.field(x, z)) > 0 else 40.0

    def on_surface(self, z, x, y):
        """A design height on the rock's top -> game y (sits on y_top)."""
        dz = z - float(self.rf.surface(x, y))
        return self.ytop_at(x, y) + (soft_up(dz) if dz >= 0 else max(dz * 0.22, -8.0))

    def edge_out(self, p, b, inside=40.0):
        """Where the rock ends along bearing b, searching outward from `inside` m behind p."""
        r = -inside
        q = polar(b, r, p)
        while float(self.rf.field(*q)) <= 0 and r > -200:      # start inside the rock
            r -= 4; q = polar(b, r, p)
        while float(self.rf.field(*q)) > 0 and r < 400:
            r += 1; q = polar(b, r, p)
        return q

    def reanchor_maw(self):
        """The design's Maw mouth is edge_point(180, (0, -420)), which starts outside the rock and so returns (0, -420)
        itself: the real cliff where the Maw tunnel leaves the rock is further north (the notch between the lobes). The
        throat's furniture is moved onto the real mouth: the field gate 20 m in, the blast doors 30 m in (clear of the lip
        guns on the notch's cliffs; the throat is short), the inner guns behind them."""
        opt, my = self.opt, self.mouth[1]
        e = self.edge_out((self.maw_t[-1][0] * 0.5, -200.0), 180, inside=0)
        self.real_mouth = (round(e[0], 1), round(e[1], 1))
        dz = self.real_mouth[1] - my
        ddoor = self.real_mouth[1] + 30 - (my + 16)
        self.log.append(f"the Maw: design mouth z {my:.0f}, real cliff z {self.real_mouth[1]:.0f}; doors, sign, towers moved {dz:.0f} m north")
        for b in opt["buildings"]:
            c = centroid([tuple(p) for p in b["pts"]])
            door = b["kind"] == "bastion" and b["h"] == 130 and b["d"] == -130
            tower = b["kind"] == "bastion" and b["h"] > 100 and b["d"] > 0 and abs(c[1] - (my + 110)) < 5
            if door or tower:
                b["pts"] = [(x, z + (ddoor if door else dz)) for x, z in b["pts"]]
        for f in opt["fx"]:
            if f["type"] == "neon" and math.dist(f["c"], (0, my - 6)) < 2:
                f["c"] = (self.real_mouth[0], self.real_mouth[1] - 4); f["z"] = 52.0
        for h in opt["holos"]:
            if h["text"] == "THE MAW":
                h["at"] = (self.real_mouth[0], self.real_mouth[1] - 64)
            elif h["text"] == "BLAST DOORS":
                h["at"] = (self.real_mouth[0], self.real_mouth[1] + 30)
        self.gate_z = self.real_mouth[1] + 20

    # ------------------------------------------------------------ the guns (amended: all at the flight plane)
    def place_guns(self):
        """Every gun at y = 0 on a solid ledge (-8..-2) with its footprint clear from -2 to +12."""
        opt = self.opt
        spheres = [b for b in opt["buildings"] if b.get("shape") == "sphere" and b["kind"] == "turret"]
        my = self.mouth[1]
        self.guns, self.ledges, self.voids, self.casemates = [], [], [], []
        mawz = lambda p: abs(p[0]) < 420 and p[1] < -150
        cases = {}
        for f in opt["fx"]:
            if f["type"] != "turret":
                continue
            c = tuple(f["c"]); hd = f["facing"]
            sp = min(spheres, key=lambda s: math.dist(s["c"], c))
            z, big = sp["z"], f["reach"] >= 900
            if min(math.dist(c, fo["c"]) for fo in self.forts) < 150:
                continue                                      # placed with its fort
            if mawz(c) and big and z > 100:
                continue                                      # the gun towers above the Maw: decorative only
            if mawz(c) and big:                               # a heavy battery in a casemate cut into the cliff
                side = 1 if c[0] > 0 else -1
                cases.setdefault(side, []).append((c, hd))
                continue
            if mawz(c) and z == 0:                            # inner guns in the throat, against the tunnel wall
                t = self.maw_t
                seg = min(zip(t, t[1:]), key=lambda s: seg_dist(c, *s))
                a, b = seg
                tf = max(0.0, min(1.0, ((c[1] - a[1]) * (b[1] - a[1]) + (c[0] - a[0]) * (b[0] - a[0])) / math.dist(a, b) ** 2))
                cl = (a[0] + (b[0] - a[0]) * tf, a[1] + (b[1] - a[1]) * tf)
                side = 1 if c[0] > cl[0] else -1
                w = cl
                while self.col_kind(*w)[0] >= 2:
                    w = (w[0] + side * 1.0, w[1])
                g = (w[0] - side * 9.0, c[1])
                self.add_gun(g, hd, False, "inner", ledge=box((g[0] + side * 4, g[1]), 90, 24, 14))
                continue
            if mawz(c):                                       # flak and the emplacements on the Maw's lip
                e = self.edge_out((c[0], my + 60), 180, inside=0)
                g = polar(180, 9, e)
                self.add_gun(g, hd, False, "maw", ledge=box(polar(180, 1, e), 180, 30, 14))
                continue
            p_design = polar(hd, -30, c)                      # the light guns round the rock, on cliff ledges
            e = self.edge_out(p_design, hd)
            g = polar(hd, 9, e)
            self.add_gun(g, hd, False, "edge", ledge=box(polar(hd, 1, e), hd, 30, 14))
        # the twin heavy batteries: casemates (y -40..+40) cut into the cliffs either side of the Maw, embrasure facing out
        for side, gs in cases.items():
            bast = [b for b in opt["buildings"] if b["kind"] == "bastion" and b["h"] == 40 and b["d"] == -90
                    and (centroid(b["pts"])[0] > 0) == (side > 0)][0]
            bc = centroid(bast["pts"])
            hd = gs[0][1]
            p = polar(hd, 10, bc)                             # the design put the bastion 10 m inside the cliff edge p
            self.casemates.append(dict(side=side, pts=bast["pts"], p=p, hd=hd))
            self.voids.append((box(polar(hd, 9, p), hd, 38, 92), -2.0, 16.0))
            for c, h in gs:
                self.add_gun(c, h, True, "maw")

    def add_gun(self, g, hd, big, group, ledge=None, fort=-1):
        a, b = (18, 18) if big else (14, 14)
        self.guns.append(dict(x=round(g[0], 2), y=0.0, z=round(g[1], 2), yaw=round(math.radians(hd % 360), 4), big=big,
                              fort=fort, group=group))
        if fort < 0:
            self.voids.append((box(g, hd, a, b), -2.0, 12.0))
            if ledge:
                self.ledges.append((group, ledge))

    # ------------------------------------------------------------ the rock in 3D, shell only
    def rock_volume(self):
        NX, NZ = self.foot.shape
        j0, j1 = self.jc0, self.jc1
        NY = j1 - j0
        SC = SPLIT // 2
        J = (j0 + np.arange(NY))[None, :, None]
        foot, inner = self.foot[:, None, :], self.inner[:, None, :]
        jt, jb, fj = self.jt[:, None, :], self.jb[:, None, :], self.fj[:, None, :]
        solid = foot & (J >= jb) & (J < jt)
        solid &= ~(inner & (J >= fj) & (J < SC))
        top = solid & (J >= jt - self.th[:, None, :])                       # (a) the top shell
        section = solid & (self.kind == 1)[:, None, :] & (J >= SC - 2) & (J < SC)   # (b) the cut layer y 16..24
        floor = solid & inner & (J >= fj - 1) & (J < fj)                    # (d) tunnel and cavern floors
        req = top | section | floor
        parts = dict(top=int(top.sum()), section=int(section.sum()), floor=int(floor.sum()))
        del top, section, floor
        f2, in2 = self.foot, self.inner
        wall_only = 0
        for jj in range(NY):                                                # (c) walls: within 6 m of air at that height
            Ja = j0 + jj
            air = ~f2 | (in2 & (self.fj <= Ja) & (Ja < SC)) | (f2 & (Ja >= self.jt))
            air &= ~(f2 & (Ja < self.jb))                                   # below the bottom is no face (no bottom shell)
            w_ = solid[:, jj, :] & dilate2d(air, 1.5)
            wall_only += int((w_ & ~req[:, jj, :]).sum())
            req[:, jj, :] |= w_
        parts["walls_extra"] = wall_only
        self.log.append("rock shell by rule (fine cells): " + ", ".join(f"{k} {8 * v}" for k, v in parts.items()))
        up = lambda a: a.repeat(2, 0).repeat(2, 1).repeat(2, 2)
        solid, req = up(solid), up(req)
        # the gun clear zones and the casemates' embrasures are cut at 2 m, with 6 m of wall round them
        void = np.zeros(solid.shape, bool)
        org = (self.ri0, self.rj0, self.rk0)
        for pts, y0, y1 in self.voids:
            self.paint(void, org, *prism(pts, y0, y1))
        solid &= ~void; req &= ~void
        for jj in np.where(void.any(axis=(0, 2)))[0]:
            req[:, jj, :] |= solid[:, jj, :] & dilate2d(void[:, jj, :], 3.0, pad=False)
        for d in (-3, -2, -1, 1, 2):
            req |= solid & vshift(void, d)
        self.rsolid, self.rreq, self.rreq0 = solid, req, req.copy()
        self.log.append(f"rock: {int(self.foot.sum())} 4 m columns, solid {int(solid.sum())} cells, shell {int(req.sum())} cells")

    @staticmethod
    def paint(target, torg, m, morg, value=True):
        """OR a local mask (origin morg, global cells) into target (origin torg)."""
        sl_t, sl_m = [], []
        for a in range(3):
            lo = max(morg[a], torg[a]); hi = min(morg[a] + m.shape[a], torg[a] + target.shape[a])
            if hi <= lo:
                return
            sl_t.append(slice(lo - torg[a], hi - torg[a])); sl_m.append(slice(lo - morg[a], hi - morg[a]))
        if value:
            target[tuple(sl_t)] |= m[tuple(sl_m)]
        else:
            target[tuple(sl_t)] &= ~m[tuple(sl_m)]

    def void_mask(self, shape, org):
        v = np.zeros(shape, bool)
        for pts, y0, y1 in self.voids:
            self.paint(v, org, *prism(pts, y0, y1))
        return v

    # ------------------------------------------------------------ structures
    def solid(self, group, pts, y0, y1, col, hollow=True, band=None, litcol=None, cap=True, name="", fine=False):
        """A structure solid. On the 4 m lattice unless fine (then 2 m): hollowed to a 4 m skin when it is larger than
        8 m in every axis, a cut face at +24 when it crosses it, an optional lit band (band = cell index of its layer)."""
        if fine:
            fill, o = prism(pts, y0, y1)
            req = shell(fill) if hollow else fill.copy()
            Jc = o[1] + np.arange(fill.shape[1])
            capl = (Jc >= SPLIT - 2) & (Jc < SPLIT)
            bl = None if band is None else int(band) - o[1]
        else:
            fill, o = prism_c(pts, y0, y1)
            req = shell(fill, 1) if hollow and min(fill.shape) > 2 else fill.copy()
            Jc = o[1] + np.arange(fill.shape[1])
            capl = Jc == SPLIT // 2 - 1
            bl = None if band is None else int(band) // 2 - o[1]
        if cap and Jc[0] < (SPLIT if fine else SPLIT // 2) <= Jc[-1]:      # a cut face at +24 for the cutaway
            req[:, capl, :] |= fill[:, capl, :]
        lit = None
        if bl is not None and 0 <= bl < fill.shape[1]:
            lay = fill[:, bl, :]
            ring = lay & ~erode(lay[:, None, :], 1, axes=(0, 2))[:, 0, :]
            lit = np.zeros_like(fill); lit[:, bl, :] = ring
            req |= lit
        if not fine:
            fill, req = up2(fill), up2(req)
            lit = None if lit is None else up2(lit)
            o = (2 * o[0], 2 * o[1], 2 * o[2])
        v = self.void_mask(fill.shape, o)
        if v.any():                                           # a gun's clear zone or an embrasure cuts it; close the cut
            fill &= ~v
            req = (req | (fill & around_void(v))) & fill
            if lit is not None:
                lit &= fill
        if group.startswith("outer_"):                       # bolted to the outside: never standing in a tunnel or cavern
            X = (o[0] + np.arange(fill.shape[0]) + 0.5) * CELL; Z = (o[2] + np.arange(fill.shape[2]) + 0.5) * CELL
            ci = np.floor((X - self.rx0) / CC).astype(int); ck = np.floor((Z - self.rz0) / CC).astype(int)
            okx = (ci >= 0) & (ci < self.kind.shape[0]); okz = (ck >= 0) & (ck < self.kind.shape[1])
            inn = np.zeros((fill.shape[0], fill.shape[2]), bool)
            inn[np.ix_(okx, okz)] = (self.inner | self._corr)[np.ix_(ci[okx], ck[okz])]
            low = (o[1] + np.arange(fill.shape[1])) < SPLIT
            bad = inn[:, None, :] & low[None, :, None] & fill
            if bad.any():
                self.log.append(f"{group}: {int(bad.sum())} cells of a {name or 'structure'} at "
                                f"({(o[0] + fill.shape[0] / 2) * CELL:.0f}, {(o[2] + fill.shape[2] / 2) * CELL:.0f}) clipped out of a tunnel")
                fill &= ~bad
                near = np.zeros_like(bad)
                for jj in np.where(bad.any(axis=(0, 2)))[0]:
                    near[:, jj, :] = dilate2d(bad[:, jj, :], 2.0, pad=False)
                req = (req | (fill & near)) & fill
                if lit is not None:
                    lit &= fill
        if not fill.any():
            return None
        s = Solid(group, col, fill, o, req, lit, litcol, "fine" if fine else name)
        self.solids.append(s)
        return s

    def sector(self, p):
        b = math.degrees(math.atan2(p[0] - self.station_c[0], p[1] - self.station_c[1])) % 360
        return "outer_" + ["north", "northeast", "east", "southeast", "south", "southwest", "west", "northwest"][int(((b + 22.5) % 360) // 45)]

    def build_structures(self):
        opt, rng = self.opt, random.Random(23)
        self.solids = []
        my = self.mouth[1]
        mk = lambda z: float(np.clip(-32 + (z + 80) * 22 / 90, -32, -10))      # Market: floor -80 -> -32, stalls below -10
        dn = lambda z: float(np.clip(-24 + (z + 50) * 14 / 58, -24, -10))      # dens: floor -50 -> -24
        den_of = lambda p: min(self.dens, key=lambda d: math.dist(d["c"], p))
        fort_near = lambda p: min(math.dist(p, f["c"]) for f in self.forts) < 150
        for b in opt["buildings"]:
            k, dist, pts = b["kind"], b["district"], [tuple(p) for p in b["pts"]]
            c = tuple(b["c"]) if b.get("c") else centroid(pts)
            sh, p_ = b.get("shape"), b.get("p", {})
            if k in ("turret", "barrel"):
                continue                                      # the guns are their own grids ("guns")
            if dist == "bazaar" and k == "stall":
                band = None; lc = None
                if b["lit"] >= 0.8:
                    lc = {"khu": LIT["neon_amber"], "scav": LIT["amber"], "vest": LIT["cyan"]}.get(b["roof"], LIT["pink"])
                    band = jrange(mk(b["d"]), mk(b["h"]))[1] - 1
                self.solid("market", pts, mk(b["d"]), mk(b["h"]), rng.choice([HULL["metal"], HULL["brown"]]), band=band, litcol=lc)
            elif dist == "bazaar" and k == "cargo":
                self.solid("market", pts, mk(b["d"]), mk(b["h"]), rng.choice([HULL["brown"], HULL["olive"]]))
            elif dist == "fence" and k == "cargo":
                g = "den_" + den_of(c)["name"].lower()
                self.solid(g, pts, dn(b["d"]), dn(b["h"]), rng.choice([HULL["brown"], HULL["olive"], HULL["metal"]]))
            elif dist == "chop" and k == "arm":
                apts = box(c, p_["heading"], p_["length"], 4)
                tip = polar(p_["heading"], p_["length"] / 2 + 2, c)
                if self.col_kind(*tip)[0] == 2:              # its outer end stands in a tunnel: it would block the lane
                    self.log.append(f"docks arm at bearing {p_['heading']:.0f} dropped: it stands across a tunnel mouth")
                    self.dropped_arms.append(c)
                    continue
                self.solid("docks", apts, -2, 2, HULL["metal"], hollow=False, fine=True)
            elif k == "bastion" and fort_near(c):
                continue                                      # with its fort
            elif k == "bastion" and b["h"] == 40 and b["d"] == -90:
                continue                                      # the casemates, below
            elif k == "bastion" and b["h"] == 14 and b["d"] == -20:
                self.solid("maw", pts, -20, -8, HULL["soot"], hollow=False)   # the casemate's sill, under the embrasure
            elif k == "bastion" and b["h"] > 100 and b["d"] > 0:              # gun towers above the Maw (decorative)
                y0 = self.on_surface(b["d"], *c); y1 = self.on_surface(b["h"], *c)
                self.solid("maw", pts, y0, y1, HULL["graphite"], band=jrange(y0, y1)[1] - 2, litcol=LIT["red"])
            elif k == "bastion":                                              # blast doors, in the walls of the throat
                xs = [p[0] for p in pts]
                side = 1 if c[0] > 0 else -1
                inner_x = min(xs, key=abs)
                wall = self.maw_wall(c[1], side)
                L = 32.0                                      # a slab 30 m into the wall, its edge 2 m proud of it
                dpts = box((side * (abs(wall - side * 2) + L / 2), c[1]), 90, L, max(p[1] for p in pts) - min(p[1] for p in pts))
                y1 = min(soft(b["h"]), self.ytop_at(*c) + 4)
                self.solid("maw", dpts, soft(b["d"]), y1, HULL["soot"], band=10, litcol=LIT["red"])
            elif k == "silo":
                self.solid("maw", pts, self.on_surface(b["d"], *c), self.on_surface(b["h"], *c), HULL["graphite"])
            elif dist == "outer" and k == "hab":
                surf = float(self.rf.surface(*c))
                y1 = self.ytop_at(*c) + soft_up(max(b["h"] - surf, 6.0))
                y0 = soft(b["d"])
                band = jrange(y0, y1)[0] + int(0.62 * (jrange(y0, y1)[1] - jrange(y0, y1)[0])) if b["lit"] > 0.5 else None
                self.solid(self.sector(c), pts, y0, y1, rng.choice([HULL["steel"], HULL["graphite"]]), band=band,
                           litcol=rng.choice([LIT["amber"], LIT["blue"]]), name="cliff module")
            elif dist == "outer" and k == "gantry":
                gp = box(c, p_["heading"], p_["length"], max(p_["width"], 4))
                if b["z"] == 70:                                           # the girder belt round the waist
                    y = soft(70.0); w = p_["width"]
                    self.solid(self.sector(c), gp, y - w / 2, y + w / 2, HULL["metal"], hollow=False, cap=False)
                else:                                                      # scaffolding over the flanks
                    y = self.on_surface(b["z"], *c)
                    self.solid(self.sector(c), gp, y - 2, y + 2, HULL["metal"], hollow=False, cap=False)
            elif dist == "outer" and k == "radiator":
                yb = self.on_surface(b["d"], *c); yt = self.on_surface(b["h"], *c)
                rp = box(c, p_["heading"], p_["length"], 4)
                self.solid(self.sector(c), rp, yb, yt, HULL["brown"], hollow=False, band=jrange(yb, yt)[1] - 1, litcol=LIT["red"], cap=False)
            elif dist == "outer" and k == "mast":
                yb = self.ytop_at(*c) - 4; yt = self.ytop_at(*c) + soft_up(p_["height"])
                self.solid(self.sector(c), box(c, 0, 6, 6), yb, yt, HULL["metal"], hollow=False, band=jrange(yb, yt)[1] - 1,
                           litcol=LIT["red"], cap=False)
            elif dist == "outer" and k == "rail":
                self.solid(self.sector(c), box(c, p_["heading"], p_["length"], p_["width"]), 14, 24, HULL["olive"])
            elif dist == "outer" and k == "spar":
                self.solid(self.sector(c), box(c, p_["heading"], p_["length"], p_["width"]), -4, 4, HULL["steel"], hollow=False,
                           name="docking spar")
            else:
                self.log.append(f"NOT EXPORTED: building {k}/{dist} at {c}")
        # the casemates for the twin heavy batteries
        for cm in self.casemates:
            self.solid("maw", cm["pts"], -40, 40, HULL["graphite"], band=8, litcol=LIT["red"])
        # ledges under the guns
        for group, lp in self.ledges:
            g = self.sector(centroid(lp)) if group == "edge" else "maw"
            self.solid(g, lp, -8, -2, HULL["graphite"], hollow=False, cap=False, fine=True)
        # the Quartermaster's console at the Market's centre (the design's "lab" ring)
        lab = next(f for f in opt["fx"] if f["type"] == "lab")
        self.solid("market", ngon(tuple(lab["c"]), 10, 6), -32, -12, HULL["graphite"], band=-8, litcol=LIT["neon_amber"])
        self.build_hulk()

    def maw_wall(self, z, side):
        """x of the Maw tunnel's wall at design y = z on one side (first rock column at the plane)."""
        t = self.maw_t
        x = min((p for p in t), key=lambda p: abs(p[1] - z))[0]
        for a, b in zip(t, t[1:]):
            if min(a[1], b[1]) <= z <= max(a[1], b[1]) and a[1] != b[1]:
                x = a[0] + (b[0] - a[0]) * (z - a[1]) / (b[1] - a[1])
        while self.col_kind(x, z)[0] >= 2 or (self.col_kind(x, z)[0] == 0 and abs(x) < 60):
            x += side
        return x

    def build_hulk(self):
        """The Den: a warship rammed into the rock, its hull a hangar open at the stern (into the tunnels) and the bow."""
        h = self.hulk
        L, W, hd = h["length"], h["width"], h["heading"]
        body = L * (0.78 if h.get("bow") else 1.0)
        bow = L - body
        pts = box(h["c"], hd, L + 20, W + 20)
        i0, i1 = int(math.floor(min(p[0] for p in pts) / CC)), int(math.ceil(max(p[0] for p in pts) / CC))
        k0, k1 = int(math.floor(min(p[1] for p in pts) / CC)), int(math.ceil(max(p[1] for p in pts) / CC))
        j0, j1 = -7, 8
        X = (np.arange(i0, i1) + 0.5)[:, None] * CC; Z = (np.arange(k0, k1) + 0.5)[None, :] * CC
        a, l = self.hulk_frame(X, Z)
        Y = ((np.arange(j0, j1) + 0.5) * CC)[None, :, None]
        a3, l3 = a[:, None, :], np.abs(l)[:, None, :]
        hw = np.where(a3 > 0, W / 2 - (W / 2 - 32) * np.clip(a3 / bow, 0, 1), W / 2)      # the bow tapers to a 64 m door
        top = 30.0                                       # the roof stays above +24, so the cutaway opens the whole hangar
        outer = (a3 > -body) & (a3 < bow) & (l3 < hw) & (Y >= -28) & (Y < top)
        cavity = (l3 < hw - 4) & (Y >= -24) & (Y < top - 4)                              # open at both ends
        fill = up2(outer & ~cavity)
        i0, j0, k0 = 2 * i0, 2 * j0, 2 * k0
        X = (np.arange(i0, i0 + fill.shape[0]) + 0.5)[:, None] * CELL; Z = (np.arange(k0, k0 + fill.shape[2]) + 0.5)[None, :] * CELL
        a, l = self.hulk_frame(X, Z)
        # its stern stands across the Ring: cut it back where it would block the highway
        ri = self.tunnels.index(next(t for t in self.tunnels if t[0] is self.ring))
        rd = np.full(a.shape, np.inf)
        for (ax, ay), (bx, by) in zip(self.ring, self.ring[1:]):
            dx, dy = bx - ax, by - ay
            t = np.clip(((X - ax) * dx + (Z - ay) * dy) / (dx * dx + dy * dy), 0, 1)
            rd = np.minimum(rd, np.hypot(X - (ax + dx * t), Z - (ay + dy * t)))
        fill &= ~(rd < self.ring_w / 2 + 2)[:, None, :]
        o = (i0, j0, k0)
        s = Solid("den_hulk", HULL["olive"], fill, o, name="hulk")
        self.solids.append(s)
        # deckhouses on its back and rust plates on its flanks: the same draw as the Blender render (hub_arrival.py)
        hr = random.Random(int(h["c"][0] * 13 + h["c"][1] * 7))
        bc = polar(hd, -(L - body) / 2, h["c"])
        for k in range(hr.randint(2, 5)):
            lat = hr.uniform(-0.3, 0.3) * W
            c = polar(hd + 90, lat, polar(hd, hr.uniform(-0.4, 0.4) * body, bc))
            ln, wd = hr.uniform(0.08, 0.2) * L, hr.uniform(0.2, 0.5) * W
            col = HULL["olive"] if hr.choice([0, 1]) == 0 else HULL["brown"]
            self.solid("den_hulk", box(c, hd, ln, wd), 28, 40, col)
        for k in range(hr.randint(2, 4)):
            side = hr.choice([-1, 1])
            c = polar(hd + 90, side * (W * 0.5 + 1), polar(hd, hr.uniform(-0.45, 0.45) * body, bc))
            ln = hr.uniform(0.1, 0.25) * L
            pl = self.solid("den_hulk", box(c, hd, ln, 4), -24, 24, HULL["brown"], hollow=False, cap=False)
            if pl is not None:                                 # plates never stand in the Ring either
                X2 = (pl.o[0] + np.arange(pl.fill.shape[0]) + 0.5)[:, None] * CELL
                Z2 = (pl.o[2] + np.arange(pl.fill.shape[2]) + 0.5)[None, :] * CELL
                bad = np.zeros((pl.fill.shape[0], pl.fill.shape[2]), bool)
                for (ax, ay), (bx, by) in zip(self.ring, self.ring[1:]):
                    dx, dy = bx - ax, by - ay
                    t = np.clip(((X2 - ax) * dx + (Z2 - ay) * dy) / (dx * dx + dy * dy), 0, 1)
                    bad |= np.hypot(X2 - (ax + dx * t), Z2 - (ay + dy * t)) < self.ring_w / 2 + 2
                pl.fill &= ~bad[:, None, :]; pl.req &= ~bad[:, None, :]

    def lumpy(self, X, Y, Z, R, V, seed, amp=0.12):
        """A lumpy ellipsoid (horizontal radius R, vertical V) centred at 0."""
        rr = np.sqrt(X * X + Z * Z) + 1e-9
        th = np.arctan2(X, Z); ph = np.arctan2(Y / V * R, rr)
        lump = (0.5 * np.sin(3 * th + seed) * np.cos(2 * ph + seed * 0.7) + 0.3 * np.sin(5 * th - seed * 1.3 + 2 * ph)
                + 0.2 * np.cos(7 * th + 3 * ph + seed * 2.1))
        return (X / R) ** 2 + (Y / V) ** 2 + (Z / R) ** 2 < (1 + amp * lump) ** 2

    def build_field(self):
        """The ordinary rock field on the approach: lumpy boulders in the flight plane, rock blocks."""
        for n, f in enumerate(self.field_rocks):
            c, r = tuple(f["c"]), f["r"]
            V = 0.6 * r
            pts = box(c, 0, 2 * r * 1.15 + 4, 2 * r * 1.15 + 4)
            i0, i1 = cell_range(min(p[0] for p in pts), max(p[0] for p in pts))
            k0, k1 = cell_range(min(p[1] for p in pts), max(p[1] for p in pts))
            j0, j1 = jrange(-V * 1.15 - 2, V * 1.15 + 2)
            X = ((np.arange(i0, i1) + 0.5) * CELL - c[0])[:, None, None]
            Y = ((np.arange(j0, j1) + 0.5) * CELL)[None, :, None]
            Z = ((np.arange(k0, k1) + 0.5) * CELL - c[1])[None, None, :]
            fill = self.lumpy(X, Y, Z, r, V, n)
            if not fill.any():
                continue
            s = Solid("field_" + ("west" if c[0] < 0 else "east"), 40 + n % 6, fill, (i0, j0, k0), req=shell(fill), name="boulder")
            self.solids.append(s)

    # ------------------------------------------------------------ pieces
    def assemble(self):
        groups = {}
        for s in self.solids:
            groups.setdefault(s.group, []).append(s)
        self.struct_pieces = []
        rorg = (self.ri0, self.rj0, self.rk0)
        for g, ss in groups.items():
            lo = [min(s.o[a] for s in ss) for a in range(3)]
            hi = [max(s.o[a] + s.fill.shape[a] for s in ss) for a in range(3)]
            shape = tuple(hi[a] - lo[a] for a in range(3))
            label = np.full(shape, -1, np.int16)
            req = np.zeros(shape, bool)
            classes = []

            def cls(c, m):
                if (c, m) not in classes:
                    classes.append((c, m))
                return classes.index((c, m))
            rock_like = g.startswith("field")
            for s in ss:
                sl = tuple(slice(s.o[a] - lo[a], s.o[a] - lo[a] + s.fill.shape[a]) for a in range(3))
                ci = cls(s.col, 0 if rock_like else 1)
                label[sl][s.fill] = ci
                req[sl] |= s.req
                if s.lit is not None and s.lit.any():
                    li = cls(s.litcol, 2)
                    label[sl][s.lit] = li
            req &= label >= 0
            # meet the rock: a structure replaces the rock's shell where it stands in it; whatever of it is buried in
            # the rock's interior is dropped (it would never be seen)
            sl_p, sl_r = [], []
            ok = True
            for a in range(3):
                a0 = max(lo[a], rorg[a]); a1 = min(hi[a], rorg[a] + self.rsolid.shape[a])
                if a1 <= a0:
                    ok = False; break
                sl_p.append(slice(a0 - lo[a], a1 - lo[a])); sl_r.append(slice(a0 - rorg[a], a1 - rorg[a]))
            if ok and not rock_like:
                sl_p, sl_r = tuple(sl_p), tuple(sl_r)
                lab = label[sl_p] >= 0
                rs, rr = self.rsolid[sl_r], self.rreq0[sl_r]
                carve = lab & rs & rr
                drop = lab & rs & ~rr
                self.rsolid[sl_r] &= ~carve
                self.rreq[sl_r] &= ~carve
                label[sl_p][drop] = -1
                req[sl_p] &= ~drop
            self.struct_pieces.append(dict(group=g, lo=lo, label=label, req=req, classes=classes, solids=ss))

    def pack_structures(self):
        out = []
        for P in self.struct_pieces:
            g, lo, label, req, classes = P["group"], P["lo"], P["label"], P["req"], P["classes"]
            covered = np.zeros(label.shape, bool)
            split = not (g.startswith("field"))
            blocks = {"low": [], "high": []}
            for s in P["solids"]:
                base = [s.o[a] - lo[a] for a in range(3)]
                full = [slice(base[a], base[a] + s.fill.shape[a]) for a in range(3)]
                parts = [("low", full)]
                if split:
                    jlo, jhi = base[1] + lo[1], base[1] + lo[1] + s.fill.shape[1]
                    parts = []
                    if jlo < SPLIT:
                        parts.append(("low", [full[0], slice(base[1], min(base[1] + s.fill.shape[1], SPLIT - lo[1])), full[2]]))
                    if jhi > SPLIT:
                        parts.append(("high", [full[0], slice(max(base[1], SPLIT - lo[1]), base[1] + s.fill.shape[1]), full[2]]))
                for layer, sl in parts:
                    sl = tuple(sl)
                    lab = label[sl]
                    for ci in np.unique(lab[lab >= 0]):
                        al = lab == ci
                        b = pack(al, req[sl] & al, covered[sl], schedule=SCHEDULE if s.name == "fine" else COARSE_SCHEDULE)
                        if len(b):
                            b[:, 0] += sl[0].start + lo[0]; b[:, 1] += sl[1].start + lo[1]; b[:, 2] += sl[2].start + lo[2]
                            c, m = classes[ci]
                            blocks[layer].append(np.column_stack([b, np.full(len(b), c), np.full(len(b), m)]))
            for layer, bl in blocks.items():
                if not bl:
                    continue
                B = np.concatenate(bl)
                out.append(dict(group=g, layer=("struct" if layer == "low" else "high"), blocks=B))
        return out

    def pack_rock(self):
        rs, rr = self.rsolid, self.rreq
        NX, NY, NZ = rs.shape
        j0 = self.rj0
        near_in = dilate2d(self.inner, 4) & ~dilate2d(~self.foot, 4)
        pieces = []
        for ti in range(int(math.ceil(NX / TILE))):
            for tk in range(int(math.ceil(NZ / TILE))):
                si, sk = slice(ti * TILE, min((ti + 1) * TILE, NX)), slice(tk * TILE, min((tk + 1) * TILE, NZ))
                for layer, sj in (("low", slice(0, SPLIT - j0)), ("high", slice(SPLIT - j0, NY))):
                    req = rr[si, sj, sk]
                    if not req.any():
                        continue
                    al = rs[si, sj, sk]
                    b = pack(al, req, schedule=COARSE_SCHEDULE)
                    i, j, k, s = b[:, 0], b[:, 1] + sj.start + j0, b[:, 2], b[:, 3]
                    # tones: chunky by noise per block, darker in the tunnels, the cut face at +24 its own colour
                    gx = self.rx0 + (ti * TILE + i + s / 2) * CELL; gz = self.rz0 + (tk * TILE + k + s / 2) * CELL
                    gy = (j + s / 2) * CELL
                    rnd = np.random.default_rng(ti * 97 + tk * 13 + (layer == "high")).uniform(-0.9, 0.9, len(b))
                    n = noise(gx * 1.9, gz * 1.9 + gy * 0.7, 13) / 1.6 + rnd
                    tone = np.clip(np.round(1.5 + n * 1.4), 0, 3).astype(int)
                    if layer == "high":
                        hk = np.clip((gy - 40) / 70, 0, 1)
                        c = 42 + np.clip(tone + np.round(hk), 0, 3)
                    else:
                        ci = np.clip((ti * TILE + i + s // 2) // 2, 0, near_in.shape[0] - 1)
                        ck = np.clip((tk * TILE + k + s // 2) // 2, 0, near_in.shape[1] - 1)
                        dark = near_in[ci, ck]
                        c = np.where(dark, 40 + np.clip(tone, 0, 2), 41 + tone)
                        c = np.where(j + s == SPLIT, SECTION, c)
                    B = np.column_stack([i, j, k, s, c, np.zeros(len(b), int)]).astype(np.int64)
                    pieces.append(dict(id=f"ship_hollow_rock_{layer}_{ti}_{tk}", name=f"Hollow Rock {layer.title()} {ti} {tk}",
                                       layer=layer, origin=[self.rx0 + ti * TILE * CELL, 0.0, self.rz0 + tk * TILE * CELL],
                                       motion="static", fort=-1, blocks=B))
        return pieces

    # ------------------------------------------------------------ forts
    def build_forts(self):
        pieces, self.fort_out = [], []
        for n, f in enumerate(self.forts):
            c = tuple(f["c"]); b = self.fort_b[n]
            cs = (round(c[0] / CELL) * CELL, round(c[1] / CELL) * CELL)
            R, V = float(f["r"]), 40.0
            H = int(R * 1.2 / CELL) + 14
            # the boulder and its deck on the 4 m lattice (centred on the fort's centre, a cell corner)
            Ic = np.arange(-H // 2, H // 2); Jc = np.arange(-12, 12)
            Xc = ((Ic + 0.5) * CC)[:, None, None]; Yc = ((Jc + 0.5) * CC)[None, :, None]; Zc = ((Ic + 0.5) * CC)[None, None, :]
            rock_c = self.lumpy(Xc, Yc, Zc, R, V, 11 + n, amp=0.10)
            # the bastion: a hex deck cut into the boulder's top, y 12..20
            hexp = [(x - cs[0], z - cs[1]) for x, z in ngon(polar(b, -10, c), 56, 6, 30)]
            hx = pip(Xc[:, 0, :], Zc[0, :, :], hexp)[:, None, :]
            deck_c = hx & (Yc >= 12) & (Yc < 20)
            dtop = deck_c & (Yc >= 16)
            rim_c = dtop & ~erode(dtop, 1, axes=(0, 2))                     # the deck's rim lit red
            rock_c &= ~(hx & (Yc >= 12))
            rreq_c = rock_c & ~erode(rock_c, 1)
            rock, rreq, deck, rim = up2(rock_c), up2(rreq_c), up2(deck_c & ~rim_c), up2(rim_c)
            I = np.arange(-H, H); Jv = np.arange(-24, 24)
            X = ((I + 0.5) * CELL)[:, None, None]; Y = ((Jv + 0.5) * CELL)[None, :, None]; Z = ((I + 0.5) * CELL)[None, None, :]
            # two heavy guns on ledges on the outward side, at the flight plane
            ledge = np.zeros(rock.shape, bool); void = np.zeros(rock.shape, bool)
            for k, db in enumerate((-15, 15)):
                bb = b + db
                rr = 0.0
                while self.lumpy(np.array(polar(bb, rr)[0]), np.array(0.0), np.array(polar(bb, rr)[1]), R, V, 11 + n, amp=0.10) and rr < 120:
                    rr += 1
                g = polar(bb, rr + 9, (c[0] - cs[0], c[1] - cs[1]))
                lp = box(polar(b, -8, g), b, 34, 18)
                ledge |= pip(X[:, 0, :], Z[0, :, :], lp)[:, None, :] & (Y >= -8) & (Y < -2)
                void |= pip(X[:, 0, :], Z[0, :, :], box(g, b, 18, 18))[:, None, :] & (Y >= -2) & (Y < 12)
                self.guns.append(dict(x=round(g[0] + cs[0], 2), y=0.0, z=round(g[1] + cs[1], 2), yaw=round(math.radians(b), 4),
                                      big=True, fort=n, group="fort"))
            cut = rock & (ledge | void)
            rock &= ~(ledge | void); rreq &= rock
            for jj in np.where(cut.any(axis=(0, 2)))[0]:
                rreq[:, jj, :] |= rock[:, jj, :] & dilate2d((ledge | void)[:, jj, :], 3.0, pad=False)
            B = []
            covered = np.zeros(rock.shape, bool)
            for al, rq, col, m in ((rim, rim, LIT["red"], 2), (deck, deck, HULL["graphite"], 1),
                                   (ledge, ledge, HULL["graphite"], 1), (rock, rreq, None, 0)):
                b_ = pack(al, rq, covered, schedule=SCHEDULE if al is ledge else COARSE_SCHEDULE)
                if col is None:
                    gx = (b_[:, 0] + b_[:, 3] / 2); gz = (b_[:, 2] + b_[:, 3] / 2)
                    tone = np.clip(np.round(2.5 + noise(gx * 7.0, gz * 7.0, 17 + n) / 1.6 * 2.2), 0, 4).astype(int)
                    cc = 41 + tone
                else:
                    cc = np.full(len(b_), col)
                B.append(np.column_stack([b_[:, 0] - H, b_[:, 1] - 24, b_[:, 2] - H, b_[:, 3], cc, np.full(len(b_), m)]))
            B = np.concatenate(B).astype(np.int64)
            pieces.append(dict(id=f"ship_hollow_fort_{n}", name=f"Hollow Fort {n}", layer="fort",
                               origin=[float(cs[0]), 0.0, float(cs[1])], motion="rotate", fort=n, blocks=B))
            self.fort_out.append(dict(x=float(cs[0]), z=float(cs[1]), rate=f["spin"], yaw=round(math.radians(b), 4)))
        return pieces

    # ------------------------------------------------------------ lists
    def light_y(self, x, z, zd):
        k, ci = self.col_kind(x, z)
        if k == 3 and ci >= 0:
            fl, ce = self.caverns[ci][2], self.caverns[ci][3]
            return float(np.clip(zd * 24 / ce if zd > 0 else zd * (-24 if ci != self.market_i else -32) / fl, -22, 20))
        if k == 3 or (k >= 1 and zd < 40):
            return float(np.clip(zd * 0.75, -22, 20))
        if k == 1:
            return self.on_surface(zd, x, z) + 1
        return soft(zd)

    def lists(self):
        opt, my = self.opt, self.mouth[1]
        L = {}
        sp = opt["spawn"]
        L["spawn"] = dict(x=float(sp["c"][0]), z=float(sp["c"][1]), yaw=round(math.radians(sp["heading"]), 4))
        L["forts"] = self.fort_out
        L["guns"] = self.guns
        lights, beacons = [], []
        for f in opt["fx"]:
            if f["type"] != "flash":
                continue
            x, z = f["c"]
            col = f["col"].upper()
            fo = [n for n, fo in enumerate(self.forts) if math.dist(fo["c"], f["c"]) < 5]
            if fo:
                y, kind = 26.0, "flash"
            else:
                y = self.light_y(x, z, f["z"])
                kind = "flash" if col == "#FF3C28" else ("beacon" if f["z"] == 0 and col in ("#FFB347", "#FFF0D0") else "lamp")
            if kind == "beacon" and z < my - 100:
                beacons.append(dict(x=round(x, 1), z=round(z, 1), col=col))
            lights.append(dict(x=round(x, 1), y=round(y, 1), z=round(z, 1), col=col, r=round(f["r"] * 10, 1), kind=kind))
        if len(lights) > 600:
            step = len(lights) / 600
            lights = [lights[int(i * step)] for i in range(600)]
        L["lights"], L["beacons"] = lights, beacons
        neon = []
        for f in opt["fx"]:
            if f["type"] == "neon_line":
                y = self.light_y(*f["pts"][0], f["z"])
                for a, b in zip(f["pts"], f["pts"][1:]):
                    if math.dist(a, b) > 0.5:
                        neon.append(dict(ax=round(a[0], 1), az=round(a[1], 1), bx=round(b[0], 1), bz=round(b[1], 1), y=round(y, 1),
                                         col=f["col"].upper()))
            elif f["type"] == "neon":
                a = polar(f["heading"], -f["length"] / 2, f["c"]); b = polar(f["heading"], f["length"] / 2, f["c"])
                y = self.light_y(*f["c"], f["z"])
                neon.append(dict(ax=round(a[0], 1), az=round(a[1], 1), bx=round(b[0], 1), bz=round(b[1], 1), y=round(y, 1),
                                 col=f["col"].upper()))
            elif f["type"] == "field_wall":                   # the field gate across the Maw's throat, wall to wall
                xa, xb = self.maw_wall(self.gate_z, -1), self.maw_wall(self.gate_z, 1)
                neon.append(dict(ax=round(xa + 1, 1), az=self.gate_z, bx=round(xb - 1, 1), bz=self.gate_z, y=0.0, col="#9FE0C8"))
        L["neon"] = neon
        # signs: the design's holo labels; a den's (and the Maw's) sits on its lit bar over the door
        signs, used = [], set()
        bars = [f for f in opt["fx"] if f["type"] == "neon"]
        for h in opt["holos"]:
            at = tuple(h["at"])
            cand = [i for i, f in enumerate(bars) if i not in used and math.dist(f["c"], at) < 80]
            if cand:
                i = min(cand, key=lambda i: math.dist(bars[i]["c"], at)); used.add(i)
                f = bars[i]
                signs.append(dict(text=h["text"], x=round(f["c"][0], 1), z=round(f["c"][1], 1),
                                  y=round(self.light_y(*f["c"], f["z"]) + 4, 1), yaw=round(math.radians(f["heading"]), 4),
                                  col=f["col"].upper(), size=14.0 if h["district"] == "maw" else 10.0))
            else:
                k, _ = self.col_kind(*at)
                y = 32.0 if h["district"] == "den" else (12.0 if k >= 2 else 20.0)
                col = "#%02X%02X%02X" % tuple(DISTRICT_COL.get(h["district"], (255, 241, 214)))
                signs.append(dict(text=h["text"], x=round(at[0], 1), z=round(at[1], 1), y=y, yaw=round(math.pi / 2, 4),
                                  col=col, size=12.0 if h["text"] in ("THE MARKET", "THE DOCKS", "THE DEN") else 10.0))
        L["signs"] = signs
        # moored ships: everything the design docks
        moored = []
        for s in opt["ships"]:
            if s["state"] != "docked":
                continue
            moored.append(dict(blueprint=blueprint(s["cls"], s["livery"]), x=s["c"][0], z=s["c"][1],
                               yaw=round(math.radians(s["heading"]), 4), cls=s["cls"]))
        moored = moored[:40]
        L["moored"] = moored
        # gunships: the Clan's guard off the Maw and the wraiths cruising out of the Den
        launch = next(ln for ln in opt["lanes"] if ln["kind"] == "launch")["pts"]
        gs = []
        for s in opt["ships"]:
            if s["state"] == "cruise" and s["livery"] == "khu" and s["cls"] == "wraith":
                if s["c"][1] < my - 200:
                    gs.append(dict(blueprint="ship_nocturne_wraith", x=s["c"][0], z=s["c"][1], radius=140.0))
                elif poly_dist(tuple(s["c"]), [tuple(p) for p in launch] + [polar(55, 700, self.hulk["c"])]) < 40:
                    gs.append(dict(blueprint="ship_nocturne_wraith", x=s["c"][0], z=s["c"][1], radius=110.0))
        L["gunships"] = gs
        L["stations"] = self.stations()
        L["traffic"] = self.routes()
        L["bounds"] = [float(v) for v in opt["bounds"]]
        L["boulders"] = [dict(x=f["c"][0], z=f["c"][1], r=f["r"]) for f in self.field_rocks]
        return L

    def stations(self):
        opt = self.opt
        st = []
        stalls = [b for b in opt["buildings"] if b["kind"] == "stall"]
        stall = min(stalls, key=lambda b: abs((math.degrees(math.atan2(centroid(b["pts"])[0] - self.market[0],
                                                                       centroid(b["pts"])[1] - self.market[1])) - 120 + 180) % 360 - 180))
        q = centroid(stall["pts"])
        a = math.degrees(math.atan2(q[0] - self.market[0], q[1] - self.market[1]))
        p = polar(a, -30, q)
        st.append(dict(id="market", kind="merchant", x=p[0], z=p[1], yaw=math.radians(a % 360), title="THE MARKET",
                       merchant="merchant_station_market"))
        lab = next(f for f in opt["fx"] if f["type"] == "lab")
        p = polar(180, 22, lab["c"])
        st.append(dict(id="quartermaster", kind="reset", x=p[0], z=p[1], yaw=0.0, title="QUARTERMASTER", merchant=""))
        for d in self.dens:
            ship = min((s for s in opt["ships"] if s["state"] == "docked"), key=lambda s: math.dist(s["c"], d["c"]))
            bs = math.degrees(math.atan2(ship["c"][0] - d["c"][0], ship["c"][1] - d["c"][1]))
            p = polar(bs + 180, d["r"] * 0.4, d["c"])
            st.append(dict(id="den_" + d["name"].lower(), kind="merchant", x=p[0], z=p[1], yaw=math.radians((bs + 180) % 360),
                           title=d["name"], merchant="merchant_shady_trader"))
        can = [f for f in opt["fx"] if f["type"] == "canister" and math.dist(f["c"], self.docks) < self.docks_r]
        p = tuple(can[0]["c"]) if can else self.docks
        st.append(dict(id="docks", kind="rearm", x=p[0], z=p[1], yaw=0.0, title="THE DOCKS", merchant=""))
        for s in st:
            s["x"], s["z"], s["yaw"] = round(s["x"], 1), round(s["z"], 1), round(s["yaw"], 4)
        return st

    def routes(self):
        my = self.mouth[1]
        mk = self.market
        R = 100.0                                       # the Market's inner lane (design r = 125 runs into moored haulers)
        maw = self.maw_t
        mi = offset_line([maw[1], maw[2]], 8)          # the Maw's throat, keep right: in on the east, out on the west
        mo = offset_line([maw[2], maw[1]], 8)
        maw_in = [(30.0, -1700.0), (30.0, my - 300), (mi[0][0], my - 60)] + mi
        maw_out = mo + [(mo[-1][0], my - 60), (-30.0, my - 300), (-30.0, -1700.0)]
        P = self.ring[:-1]
        n = len(P)
        idx = lambda p: min(range(n), key=lambda i: math.dist(P[i], p))

        def ring_path(a, b):
            """The shorter way round the Ring from point a to point b."""
            fw, bw = [P[a]], [P[a]]
            i = a
            while i != b:
                i = (i + 1) % n; fw.append(P[i])
            i = a
            while i != b:
                i = (i - 1) % n; bw.append(P[i])
            return fw if poly_len(fw) <= poly_len(bw) else bw

        def nearest_seg(c):
            s_ = min(range(n), key=lambda i: seg_dist(c, P[i], P[(i + 1) % n]))
            a_, b_ = P[s_], P[(s_ + 1) % n]
            t = ((c[0] - a_[0]) * (b_[0] - a_[0]) + (c[1] - a_[1]) * (b_[1] - a_[1])) / math.dist(a_, b_) ** 2
            t = max(0.0, min(1.0, t))
            return s_, (a_[0] + (b_[0] - a_[0]) * t, a_[1] + (b_[1] - a_[1]) * t)
        jm = idx(maw[-1])                               # where the Maw meets the Ring
        sm, join = nearest_seg(mk)                      # where the Ring runs into the Market
        jb0 = math.degrees(math.atan2(join[0] - mk[0], join[1] - mk[1])) % 360
        docked = [tuple(sh["c"]) for sh in self.opt["ships"] if sh["state"] == "docked"]
        jb = max((jb0 + o_ for o_ in (0, -10, 10, -20, 20, -30, 30)),
                 key=lambda b_: min(seg_dist(q, join, polar(b_, R, mk)) for q in docked) - abs(b_ - jb0) * 0.05)
        lane = lambda b0, b1, st=10: [polar(b, R, mk) for b in np.arange(b0, b1 + 0.1, st)]
        to_m = min((ring_path(jm, sm), ring_path(jm, (sm + 1) % n)), key=lambda q: poly_len(q) + math.dist(q[-1], join))
        r1 = maw_in + to_m + [join] + lane(jb, jb + 200)
        r2 = offset_line(self.ring, 10, closed=True)
        r3 = offset_line(self.ring[::-1], 10, closed=True)
        spur = min((pts for pts, w in self.tunnels if w <= 50), key=lambda t: math.dist(t[-1], self.docks))
        r4 = maw_in + ring_path(jm, idx(spur[0])) + [spur[-1], self.docks]
        back = self.back
        sd, jd = nearest_seg(self.docks)
        r5 = [self.docks, jd] + min((ring_path(sd, idx(back[0])), ring_path((sd + 1) % n, idx(back[0]))),
                                    key=lambda q: poly_len(q) + math.dist(q[0], jd)) + list(back[1:])
        ext = polar(math.degrees(math.atan2(back[-1][0] - back[-2][0], back[-1][1] - back[-2][1])), 200, back[-1])
        r5 = r5 + [ext]
        r6 = lane(0, 360)
        r7 = lane(jb + 160, jb + 360) + [join] + list(reversed(to_m)) + maw_out
        hc, hd = self.hulk["c"], self.hulk["heading"]
        r8 = [polar(hd, 40, hc), polar(hd, 160, hc), polar(hd, 700, hc)]
        routes = [("maw_to_market", r1, False, "ship_nocturne_vesper"), ("ring_clockwise", r2, True, "ship_nocturne_wraith"),
                  ("ring_anticlockwise", r3, True, "ship_nocturne_vesper"), ("maw_to_docks", r4, False, "ship_nocturne_wraith"),
                  ("docks_to_back_door", r5, False, "ship_nocturne_wraith"), ("market_inner_lane", r6, True, "ship_nocturne_wraith"),
                  ("market_to_maw", r7, False, "ship_nocturne_vesper"), ("den_launch", r8, False, "ship_nocturne_wraith")]
        out = []
        for name, pts, loop, bp in routes:
            pts = [tuple(map(float, p)) for p in pts]
            clean = [pts[0]]
            for p in pts[1:]:
                if math.dist(p, clean[-1]) > 1.0:
                    clean.append(p)
            if loop and math.dist(clean[0], clean[-1]) < 1.0:
                clean = clean[:-1]
            rs = resample(clean + ([clean[0]] if loop else []), 60.0)
            if loop:
                rs = rs[:-1]
            out.append(dict(name=name, blueprint=bp, route=[list(p) for p in rs], loop=loop))
        return out


def blueprint(cls, livery):
    if cls in ("cruiser", "freighter", "liner", "barge", "hauler"):
        return "ship_nocturne_vesper"
    return "ship_nocturne_wraith"


# ---------------------------------------------------------------- checks
def piece_check(p):
    B = p["blocks"]
    i, j, k, s = B[:, 0], B[:, 1], B[:, 2], B[:, 3]
    assert set(np.unique(s)) <= {1, 2, 3, 4}, p["id"]
    lo = [i.min(), j.min(), k.min()]
    hi = [(i + s).max(), (j + s).max(), (k + s).max()]
    occ = np.zeros((hi[0] - lo[0], hi[1] - lo[1], hi[2] - lo[2]), np.uint8)
    for sz in (1, 2, 3, 4):
        sel = s == sz
        if not sel.any():
            continue
        for a in range(sz):
            for b in range(sz):
                for c in range(sz):
                    np.add.at(occ, (i[sel] - lo[0] + a, j[sel] - lo[1] + b, k[sel] - lo[2] + c), 1)
    assert occ.max() <= 1, f"overlapping blocks in {p['id']}"
    assert int((s >= 2).sum()) <= MAX_INST, f"{p['id']} has {(s >= 2).sum()} multi-cell blocks"
    if p["id"].startswith("ship_hollow_rock_"):
        assert (i >= 0).all() and (i + s <= TILE).all() and (k >= 0).all() and (k + s <= TILE).all(), f"tile border crossed in {p['id']}"
        if p["layer"] == "low":
            assert (j + s <= SPLIT).all(), f"block straddles +24 in {p['id']}"
        else:
            assert (j >= SPLIT).all(), f"block straddles +24 in {p['id']}"
    if p["layer"] == "struct" and not p["id"].startswith("ship_hollow_field"):
        assert (j + s <= SPLIT).all()
    if p["layer"] == "high":
        assert (j >= SPLIT).all()
    assert p["origin"][1] == 0.0


def split_big(pieces):
    """Split any piece over the engine's multi-cell block limit (by halves along its longer axis)."""
    out = []
    for p in pieces:
        B = p["blocks"]
        if int((B[:, 3] >= 2).sum()) <= MAX_INST:
            out.append(p); continue
        ax = 0 if np.ptp(B[:, 0]) >= np.ptp(B[:, 2]) else 2
        cut = np.median(B[:, ax])
        for suf, sel in (("a", B[:, ax] < cut), ("b", B[:, ax] >= cut)):
            q = dict(p); q["id"] = p["id"] + "_" + suf; q["name"] = p["name"] + " " + suf.upper(); q["blocks"] = B[sel]
            out.extend(split_big([q]))
    return out


def world_cells(p, band=None):
    """Global 2 m cell columns (gi, gk) of a static piece's blocks, optionally only blocks touching a y band (cells)."""
    B = p["blocks"]
    oi, ok_ = int(round(p["origin"][0] / CELL)), int(round(p["origin"][2] / CELL))
    if band is not None:
        sel = (B[:, 1] < band[1]) & (B[:, 1] + B[:, 3] > band[0])
        B = B[sel]
    return B, oi, ok_


def stage_compute(design_path, work):
    global np
    import numpy
    np = numpy
    t0 = time.time()
    with open(design_path) as f:
        D = json.load(f)
    global DISTRICT_COL
    DISTRICT_COL = {"maw": (255, 85, 48), "bazaar": (255, 46, 136), "chop": (34, 230, 255), "fence": (255, 176, 0),
                    "den": (255, 85, 48), "outer": (190, 186, 172), "defence": (255, 85, 48)}
    E = Exporter(D)
    E.dropped_arms = []
    E.classify()
    E.reanchor_maw()
    E.place_guns()
    E.rock_volume()
    E.build_structures()
    E.assemble()
    rock = E.pack_rock()
    sp = E.pack_structures()
    struct = []
    names = {}
    for p in sp:
        g = p["group"]
        nm = "Hollow " + " ".join(w.title() for w in g.split("_"))
        hid = "_high" if p["layer"] == "high" else ""
        struct.append(dict(id="ship_hollow_" + g + hid, name=nm + (" (high)" if hid else ""), layer=p["layer"], origin=[0.0, 0.0, 0.0],
                           motion="static", fort=-1, blocks=p["blocks"]))
    # re-base each structure piece on its own min corner (x, z); heights stay absolute (origin y = 0)
    for p in struct:
        B = p["blocks"]
        mi, mk = int(B[:, 0].min()), int(B[:, 2].min())
        B[:, 0] -= mi; B[:, 2] -= mk
        p["origin"] = [mi * CELL, 0.0, mk * CELL]
    forts = E.build_forts()
    pieces = split_big(rock + struct + forts)
    for p in pieces:
        piece_check(p)
    L = E.lists()

    # ---- global occupancy on the 2 m lattice (static pieces): top heights, band [-8, 8), the cutaway mask
    statics = [p for p in pieces if p["motion"] == "static"]
    allB = []
    for p in pieces:
        B, oi, ok_ = world_cells(p)
        if p["motion"] == "rotate":
            oi, ok_ = int(round(p["origin"][0] / CELL)), int(round(p["origin"][2] / CELL))
        allB.append((p, B, oi, ok_))
    gx0 = min(oi + int(B[:, 0].min()) for _, B, oi, _ in allB) - 2
    gz0 = min(ok_ + int(B[:, 2].min()) for _, B, _, ok_ in allB) - 2
    gx1 = max(oi + int((B[:, 0] + B[:, 3]).max()) for _, B, oi, _ in allB) + 2
    gz1 = max(ok_ + int((B[:, 2] + B[:, 3]).max()) for _, B, _, ok_ in allB) + 2
    W, H = gx1 - gx0, gz1 - gz0
    top = np.full((W, H), -999, np.int16); pid = np.full((W, H), -1, np.int32); band = np.zeros((W, H), bool)
    bpid = np.full((W, H), -1, np.int32)
    high = np.zeros((W, H), bool)
    for n, (p, B, oi, ok_) in enumerate(allB):
        for sz in (1, 2, 3, 4):
            Bs = B[B[:, 3] == sz]
            if not len(Bs):
                continue
            for a in range(sz):
                for c in range(sz):
                    ii = Bs[:, 0] + oi - gx0 + a; kk = Bs[:, 2] + ok_ - gz0 + c
                    tp = (Bs[:, 1] + sz).astype(np.int16)
                    order = np.argsort(tp)
                    ii, kk, tp = ii[order], kk[order], tp[order]
                    better = tp > top[ii, kk]
                    top[ii[better], kk[better]] = tp[better]; pid[ii[better], kk[better]] = n
                    if p["motion"] == "static":
                        bsel = (Bs[order, 1] < 4) & (Bs[order, 1] + sz > -4)
                        band[ii[bsel], kk[bsel]] = True
                        bpid[ii[bsel], kk[bsel]] = n
                    if p["layer"] == "high":
                        high[ii, kk] = True
    # guns' grids stand in the band too
    for g in L["guns"]:
        if g["fort"] >= 0:
            continue
        a = 9 if g["big"] else 7
        i0, i1 = int((g["x"] - a) / CELL) - gx0, int((g["x"] + a) / CELL) - gx0
        k0, k1 = int((g["z"] - a) / CELL) - gz0, int((g["z"] + a) / CELL) - gz0
        band[max(i0, 0):i1 + 1, max(k0, 0):k1 + 1] = True
        bpid[max(i0, 0):i1 + 1, max(k0, 0):k1 + 1] = -100 - L["guns"].index(g)
    # the cutaway mask at 16 m
    ic0, kc0 = int(math.floor(gx0 * CELL / 16)), int(math.floor(gz0 * CELL / 16))
    ic1, kc1 = int(math.ceil(gx1 * CELL / 16)), int(math.ceil(gz1 * CELL / 16))
    hx, hz = np.nonzero(high)
    ci = ((hx + gx0) * CELL // 16).astype(int) - ic0; ck = ((hz + gz0) * CELL // 16).astype(int) - kc0
    ins = np.zeros((ic1 - ic0, kc1 - kc0), bool); ins[ci, ck] = True
    # crop to where it is set
    xi = np.where(ins.any(1))[0]; zk = np.where(ins.any(0))[0]
    ins = ins[xi[0]:xi[-1] + 1, zk[0]:zk[-1] + 1]
    L["inside"] = dict(cell=16, x0=float((ic0 + xi[0]) * 16), z0=float((kc0 + zk[0]) * 16), w=int(ins.shape[0]), h=int(ins.shape[1]),
                       rows=["".join("1" if ins[c, r] else "0" for c in range(ins.shape[0])) for r in range(ins.shape[1])])

    # ---- traffic clearance: >= 20 m from any rock / structure / gun cell in the y band [-8, 8]
    viol = []
    def blame(p):
        ci_, ck_ = int(p[0] / CELL) - gx0, int(p[1] / CELL) - gz0
        sub = bpid[max(ci_ - 10, 0):ci_ + 11, max(ck_ - 10, 0):ck_ + 11]
        out = set()
        for v in np.unique(sub[sub != -1]):
            out.add(allB[v][0]["id"] if v >= 0 else "gun %d (%s)" % (-100 - v, L["guns"][-100 - v]["group"]))
        return sorted(out)

    def clearance(p):
        ci_, ck_ = (p[0] / CELL) - gx0, (p[1] / CELL) - gz0
        r = 12
        i0, i1 = max(int(ci_) - r, 0), min(int(ci_) + r + 1, W); k0, k1 = max(int(ck_) - r, 0), min(int(ck_) + r + 1, H)
        sub = band[i0:i1, k0:k1]
        if not sub.any():
            return 99.0
        ii, kk = np.nonzero(sub)
        dx = np.maximum(np.abs(ii + i0 + 0.5 - ci_) - 0.5, 0); dz = np.maximum(np.abs(kk + k0 + 0.5 - ck_) - 0.5, 0)
        return float(np.hypot(dx, dz).min() * CELL)
    LEN = {"shuttle": (14, 6), "courier": (22, 8), "wraith": (28, 12), "tug": (32, 14), "cruiser": (52, 24), "hauler": (64, 20)}
    lanes = [[tuple(p) for p in r["route"]] + ([tuple(r["route"][0])] if r["loop"] else []) for r in L["traffic"]]

    def lane_gap(q):
        best = (1e9, None, None)
        for pts in lanes:
            for a, b in zip(pts, pts[1:]):
                d = seg_dist(q, a, b)
                if d < best[0]:
                    best = (d, a, b)
        return best
    for m in L["moored"]:
        Lm, Bm = LEN.get(m["cls"], (40, 16))
        need = Lm / 2 + 12
        d, a, b = lane_gap((m["x"], m["z"]))
        if d >= need:
            continue
        # away from the lane until clear (perpendicular first, then any direction); it must stay in open air, on the same
        # side of the rock's edge, and off the other ships
        ux, uz = b[0] - a[0], b[1] - a[1]; ul = math.hypot(ux, uz) or 1
        nx, nz = -uz / ul, ux / ul
        side = 1 if (m["x"] - a[0]) * nx + (m["z"] - a[1]) * nz >= 0 else -1
        dirs = [(side * nx, side * nz), (-side * nx, -side * nz)] + [(math.sin(math.radians(t)), math.cos(math.radians(t))) for t in range(0, 360, 30)]
        moved = False
        was = E.col_kind(m["x"], m["z"])[0]
        for step in range(2, 90, 2):
            for dx_, dz_ in dirs:
                q = (m["x"] + dx_ * step, m["z"] + dz_ * step)
                if lane_gap(q)[0] < need or clearance(q) < Bm / 2 + 4:
                    continue
                if any(o is not m and math.dist(q, (o["x"], o["z"])) < (Lm + LEN.get(o["cls"], (40, 16))[0]) / 2 + 4 for o in L["moored"]):
                    continue
                if (E.col_kind(*q)[0] == 0) != (was == 0):
                    continue
                E.log.append(f"moored {m['cls']} at ({m['x']:.0f}, {m['z']:.0f}) moved {step} m off the lane")
                m["x"], m["z"] = round(q[0], 1), round(q[1], 1)
                moved = True
                break
            if moved:
                break
        if not moved:
            E.log.append(f"moored {m['cls']} at ({m['x']:.0f}, {m['z']:.0f}) dropped: {d:.0f} m from a lane and no clear berth near")
            m["drop"] = True
    L["moored"] = [m for m in L["moored"] if not m.get("drop")]
    route_report = []
    for r in L["traffic"]:
        pts = r["route"] + ([r["route"][0]] if r["loop"] else [])
        mn = 99.0
        for a, b in zip(pts, pts[1:]):
            n = max(1, int(math.dist(a, b) / 2))
            for t in range(n + 1):
                q = (a[0] + (b[0] - a[0]) * t / n, a[1] + (b[1] - a[1]) * t / n)
                c = clearance(q)
                mn = min(mn, c)
                if c < 20:
                    viol.append((r["name"], round(q[0], 1), round(q[1], 1), round(c, 1)))
        near = []
        for m in L["moored"]:
            Lm = {"shuttle": 14, "courier": 22, "wraith": 28, "tug": 32, "cruiser": 52, "hauler": 64}.get(m["cls"], 40)
            d = poly_dist((m["x"], m["z"]), [tuple(p) for p in pts])
            if d < Lm / 2 + 12:
                near.append((m["cls"], m["x"], m["z"], round(d, 1)))
        route_report.append((r["name"], len(r["route"]), round(mn, 1), near))
    # stations in open air at y = 0
    st_report = []
    for s in L["stations"]:
        k = E.col_kind(s["x"], s["z"])[0]
        assert k != 1, f"station {s['id']} is inside the rock"
        st_report.append((s["id"], round(clearance((s["x"], s["z"])), 1)))
    # guns: a ledge under each (-8..-2) and its footprint clear from -2 to +12, of every piece
    occ = {}
    for p, B, oi, ok_ in allB:
        for sz in (1, 2, 3, 4):
            Bs = B[B[:, 3] == sz]
            for a in range(sz):
                for b_ in range(sz):
                    for c in range(sz):
                        for t in zip(Bs[:, 0] + oi + a, Bs[:, 1] + b_, Bs[:, 2] + ok_ + c):
                            if -4 <= t[1] < 6:
                                occ.setdefault(p["motion"] if p["motion"] == "static" else p["id"], set()).add(t)
    gun_bad = []
    for gi_, g in enumerate(L["guns"]):
        key = "static" if g["fort"] < 0 else f"ship_hollow_fort_{g['fort']}"
        o = occ.get(key, set())
        a, b_ = (9, 9) if g["big"] else (7, 7)
        hd = g["yaw"]
        ux, uz = math.sin(hd), math.cos(hd)
        under = clear = tot = 0
        for u in np.linspace(-a + 1.5, a - 1.5, 7):
            for v in np.linspace(-b_ + 1.5, b_ - 1.5, 7):
                x = g["x"] + u * ux + v * uz; z = g["z"] + u * uz - v * ux
                ci_, ck_ = int(math.floor(x / CELL)), int(math.floor(z / CELL))
                tot += 1
                under += (ci_, -2, ck_) in o
                clear += all((ci_, j, ck_) not in o for j in range(-1, 6))
        if under < 0.6 * tot or clear < 0.95 * tot:
            gun_bad.append((gi_, g["group"], g["x"], g["z"], f"ledge {under}/{tot}", f"clear {clear}/{tot}"))

    # ---- summary
    def cells(B):
        return int((B[:, 3].astype(np.int64) ** 3).sum())
    tot_cells = sum(cells(p["blocks"]) for p in pieces)
    tot_blocks = sum(len(p["blocks"]) for p in pieces)
    rockp = [p for p in pieces if p["layer"] in ("low", "high")]
    rock_cells = sum(cells(p["blocks"]) for p in rockp)
    rock_big = sum(cells(p["blocks"][p["blocks"][:, 3] >= 2]) for p in rockp)
    by_size = {s: [0, 0] for s in (1, 2, 3, 4)}
    for p in pieces:
        for s in (1, 2, 3, 4):
            n = int((p["blocks"][:, 3] == s).sum())
            by_size[s][0] += n; by_size[s][1] += n * s ** 3
    lines = []
    lines.append(f"{'piece':44s} {'layer':6s} {'blocks':>8s} {'cells':>9s} {'s1':>7s} {'s2':>7s} {'s3':>6s} {'s4':>6s}")
    for p in pieces:
        B = p["blocks"]
        lines.append(f"{p['id']:44s} {p['layer']:6s} {len(B):8d} {cells(B):9d} " +
                     " ".join(f"{int((B[:, 3] == s).sum()):{w}d}" for s, w in ((1, 7), (2, 7), (3, 6), (4, 6))))
    for lay in ("low", "high", "struct", "fort"):
        ps = [p for p in pieces if p["layer"] == lay]
        lines.append(f"  {lay:6s}: {len(ps):3d} pieces, {sum(len(p['blocks']) for p in ps):8d} blocks, {sum(cells(p['blocks']) for p in ps):9d} cells")
    lines.append(f"TOTAL: {len(pieces)} pieces, {tot_blocks} blocks, {tot_cells} cells (budget 2,500,000: "
                 f"{'OK' if tot_cells <= 2_500_000 else 'OVER'})")
    for s in (1, 2, 3, 4):
        lines.append(f"  s={s}: {by_size[s][0]:8d} blocks, {by_size[s][1]:9d} cells")
    lines.append(f"rock: {rock_cells} cells, {100 * rock_big / max(rock_cells, 1):.2f}% in s>=2 blocks (need >= 95%)")
    lines.append(f"guns {len(L['guns'])}, lights {len(L['lights'])}, neon {len(L['neon'])}, signs {len(L['signs'])}, "
                 f"stations {len(L['stations'])}, moored {len(L['moored'])}, traffic {len(L['traffic'])}, gunships {len(L['gunships'])}, "
                 f"beacons {len(L['beacons'])}, inside {L['inside']['w']}x{L['inside']['h']}")
    lines.append("traffic clearance (>= 20 m from any cell or gun in y -8..+8):")
    for name, n, mn, near in route_report:
        lines.append(f"  {name:22s} {n:3d} points, min clearance {mn:5.1f} m" + (f"; passes moored {near}" if near else ""))
    lines.append(f"  violations: {len(viol)}")
    seen = set()
    for v in viol:
        key = (v[0], round(v[1] / 40), round(v[2] / 40))
        if key in seen:
            continue
        seen.add(key)
        lines.append(f"    {v[0]} at ({v[1]}, {v[2]}) {v[3]} m: {blame((v[1], v[2]))}")
    lines.append(f"guns on a ledge with a clear footprint: {len(L['guns']) - len(gun_bad)}/{len(L['guns'])}" +
                 ("" if not gun_bad else " FAILING " + str(gun_bad)))
    lines.append("stations, clearance at y = 0: " + ", ".join(f"{a} {b} m" for a, b in st_report))
    lines += E.log
    print("\n".join(lines))
    assert tot_cells <= 2_500_000, "over the cell budget"
    assert rock_big / rock_cells >= 0.95, "rock not built from big blocks"

    # ---- the JSON
    os.makedirs(OUT, exist_ok=True)
    doc = dict(version=1, cell=CELL, spawn=L["spawn"], bounds=L["bounds"], forts=L["forts"], guns=L["guns"], lights=L["lights"],
               neon=L["neon"], signs=L["signs"], stations=L["stations"],
               moored=[{k: v for k, v in m.items() if k != "cls"} for m in L["moored"]],
               traffic=[{k: v for k, v in r.items() if k != "name"} for r in L["traffic"]], gunships=L["gunships"],
               inside=L["inside"], beacons=L["beacons"], boulders=L["boulders"],
               pieces=[dict(id=p["id"], name=p["name"], layer=p["layer"], origin=[round(float(v), 2) for v in p["origin"]],
                            motion=p["motion"], fort=int(p["fort"]), blocks=p["blocks"].tolist()) for p in pieces])
    with open(OUT_JSON, "w") as f:
        json.dump(doc, f, separators=(",", ":"))
    lines.append(f"wrote {OUT_JSON} ({os.path.getsize(OUT_JSON) / 1e6:.1f} MB) in {time.time() - t0:.0f} s")

    # ---- rasters for the check images (raw RGB, drawn on by stage 3)
    PAL = {1: (58, 63, 72), 11: (14, 16, 20), 13: (51, 80, 110), 14: (138, 122, 94), 10: (77, 52, 40), 18: (52, 54, 47),
           21: (255, 85, 48), 22: (255, 179, 71), 23: (159, 224, 200), 20: (191, 214, 255), 48: (255, 46, 136), 49: (34, 230, 255),
           50: (255, 176, 0), 46: (70, 44, 32)}
    for t_, v in zip(range(40, 46), (60, 78, 96, 116, 136, 156)):
        PAL[t_] = (v, int(v * 0.93), int(v * 0.84))
    hue = {}
    rngc = random.Random(5)
    for n, (p, B, oi, ok_) in enumerate(allB):
        if p["layer"] in ("low", "high"):
            hue[n] = None
        else:
            hue[n] = (rngc.randint(60, 255), rngc.randint(60, 255), rngc.randint(60, 255))
    lut = np.zeros((64, 3), float)
    for kk_, v in PAL.items():
        lut[kk_] = v

    def topview(skip_high):
        """Top block per column: its tone (rock: its palette colour; structures: one colour per piece), shaded by height."""
        tp = np.full((W, H), -999, np.int32); col = np.zeros((W, H, 3), float)
        for n, (p, B, oi, ok_) in enumerate(allB):
            if skip_high and p["layer"] == "high":
                continue
            for sz in (1, 2, 3, 4):
                Bs = B[B[:, 3] == sz]
                if not len(Bs):
                    continue
                Bs = Bs[np.argsort(Bs[:, 1] + sz)]
                c = lut[Bs[:, 4]] if hue[n] is None else np.repeat(np.array(hue[n], float)[None, :], len(Bs), 0)
                if hue[n] is not None:
                    c = 0.55 * c + 0.45 * lut[Bs[:, 4]]
                for a in range(sz):
                    for cc in range(sz):
                        ii = Bs[:, 0] + oi - gx0 + a; kk = Bs[:, 2] + ok_ - gz0 + cc
                        t_ = Bs[:, 1] + sz
                        better = t_ > tp[ii, kk]
                        tp[ii[better], kk[better]] = t_[better]; col[ii[better], kk[better]] = c[better]
        sh = np.clip(0.45 + 0.55 * (tp * CELL + 40) / 170, 0.3, 1.25)[:, :, None]
        im = np.clip(col * sh * 1.25, 0, 255).astype(np.uint8)
        im[tp == -999] = (8, 10, 12)
        return im.transpose(1, 0, 2)[::-1].copy()                 # rows north to south
    img = np.concatenate([topview(False), np.zeros((H, 12, 3), np.uint8), topview(True)], axis=1)
    # sections: x = 0 (the Maw's axis) and z = 120 (through the Market)
    secs = []
    for axis, val, lo_, hi_ in (("x", 0.0, -800, 700), ("z", E.market[1], -950, 1050)):
        sw, sh = int((hi_ - lo_)), 260
        S = np.zeros((sh, sw, 3), np.uint8); S[:] = (8, 10, 12)
        for p, B, oi, ok_ in allB:
            if p["motion"] != "static":
                continue
            if axis == "x":
                gi = int(math.floor(val / CELL)) - oi
                sel = (B[:, 0] <= gi) & (B[:, 0] + B[:, 3] > gi)
                h0 = (B[sel, 2] + ok_) * CELL
            else:
                gk = int(math.floor(val / CELL)) - ok_
                sel = (B[:, 2] <= gk) & (B[:, 2] + B[:, 3] > gk)
                h0 = (B[sel, 0] + oi) * CELL
            Bs = B[sel]
            for (hh, jj, ss, cc) in zip(h0, Bs[:, 1], Bs[:, 3], Bs[:, 4]):
                x0 = int(hh - lo_); x1 = int(hh + ss * CELL - lo_)
                y1 = int(150 - jj * CELL); y0 = int(150 - (jj + ss) * CELL)
                if x1 <= 0 or x0 >= sw:
                    continue
                col = PAL.get(int(cc), (200, 0, 200))
                S[max(y0, 0):min(y1, sh), max(x0, 0):min(x1, sw)] = col
                if ss >= 2:                                   # block outlines, so the big blocks read
                    S[max(y0, 0), max(x0, 0):min(x1, sw)] = (np.array(col) * 0.6).astype(np.uint8)
                    S[max(y0, 0):min(y1, sh), max(x0, 0)] = (np.array(col) * 0.6).astype(np.uint8)
        secs.append(dict(axis=axis, val=val, lo=lo_, hi=hi_, w=sw, h=sh))
        with open(os.path.join(work, f"sec_{axis}.rgb"), "wb") as f:
            f.write(S.tobytes())
    with open(os.path.join(work, "top.rgb"), "wb") as f:
        f.write(img.tobytes())
    meta = dict(top=dict(w=int(2 * W + 12), h=int(H), pw=int(W), x0=float(gx0 * CELL), z0=float(gz0 * CELL)), secs=secs, lists={k: L[k] for k in
                ("guns", "stations", "traffic", "moored", "signs", "gunships", "spawn", "forts")}, summary=lines,
                pieces=[dict(id=p["id"], layer=p["layer"], origin=p["origin"],
                             bbox=[int(p["blocks"][:, 0].min()), int(p["blocks"][:, 2].min()),
                                   int((p["blocks"][:, 0] + p["blocks"][:, 3]).max()), int((p["blocks"][:, 2] + p["blocks"][:, 3]).max())])
                        for p in pieces])
    with open(os.path.join(work, "meta.json"), "w") as f:
        json.dump(meta, f)


# ======================================================================== stage 3: the check images (Pillow)
def stage_draw(work):
    from PIL import Image, ImageDraw, ImageFont
    with open(os.path.join(work, "meta.json")) as f:
        M = json.load(f)
    try:
        fnt = ImageFont.truetype(os.path.join(HERE, "..", "..", "Assets", "Game", "UI", "Theme", "Fonts", "Inter", "FNT_Inter-SemiBold.ttf"), 16)
    except Exception:
        fnt = ImageFont.load_default()
    T = M["top"]
    img = Image.frombytes("RGB", (T["w"], T["h"]), open(os.path.join(work, "top.rgb"), "rb").read())
    d = ImageDraw.Draw(img, "RGBA")
    for panel in (0, 1):
        dx_ = panel * (T["pw"] + 12)
        P = lambda x, z: ((x - T["x0"]) / CELL + dx_, T["h"] - (z - T["z0"]) / CELL)
        for pc in M["pieces"]:                                    # rock tiles outlined
            if pc["layer"] == "low":
                x0, z0 = pc["origin"][0] + pc["bbox"][0] * CELL, pc["origin"][2] + pc["bbox"][1] * CELL
                x1, z1 = pc["origin"][0] + pc["bbox"][2] * CELL, pc["origin"][2] + pc["bbox"][3] * CELL
                a, b = P(x0, z1), P(x1, z0)
                d.rectangle([a, b], outline=(255, 255, 255, 40))
        L = M["lists"]
        cols = [(124, 255, 154), (255, 179, 71), (159, 224, 200), (255, 226, 176), (120, 220, 255), (255, 46, 136), (242, 183, 5), (255, 255, 255)]
        for n, r in enumerate(L["traffic"]):
            pts = [P(*p) for p in r["route"]] + ([P(*r["route"][0])] if r["loop"] else [])
            d.line(pts, fill=cols[n % len(cols)] + (220,), width=2)
            for q in pts:
                d.ellipse([q[0] - 2, q[1] - 2, q[0] + 2, q[1] + 2], fill=cols[n % len(cols)] + (255,))
        for m in L["moored"]:
            q = P(m["x"], m["z"]); d.rectangle([q[0] - 3, q[1] - 3, q[0] + 3, q[1] + 3], outline=(255, 226, 120, 255))
        for g in L["guns"]:
            q = P(g["x"], g["z"]); r = 6 if g["big"] else 4
            d.ellipse([q[0] - r, q[1] - r, q[0] + r, q[1] + r], fill=(255, 60, 40, 255), outline=(0, 0, 0, 255))
            e = (q[0] + math.sin(g["yaw"]) * 14, q[1] - math.cos(g["yaw"]) * 14)
            d.line([q, e], fill=(255, 60, 40, 255), width=2)
        for s in L["stations"]:
            q = P(s["x"], s["z"]); d.ellipse([q[0] - 6, q[1] - 6, q[0] + 6, q[1] + 6], outline=(124, 255, 154, 255), width=3)
            d.text((q[0] + 8, q[1] - 8), s["title"], font=fnt, fill=(124, 255, 154, 255))
        for s in L["signs"]:
            q = P(s["x"], s["z"]); d.text((q[0], q[1] + 6), s["text"], font=fnt, fill=(255, 255, 255, 200), anchor="mt")
        for g in L["gunships"]:
            q = P(g["x"], g["z"]); r = g["radius"] / CELL
            d.ellipse([q[0] - r, q[1] - r, q[0] + r, q[1] + r], outline=(255, 122, 47, 160), width=1)
        sp = L["spawn"]; q = P(sp["x"], sp["z"])
        d.ellipse([q[0] - 10, q[1] - 10, q[0] + 10, q[1] + 10], outline=(255, 255, 255, 255), width=2)
        d.text((10 + dx_, 10), ("THE HOLLOW, all pieces" if panel == 0 else "CUTAWAY: the high pieces (y >= +24) hidden") +
               ": top view, 2 m/px, north up", font=fnt, fill=(255, 255, 255, 255))
        d.text((10 + dx_, 30), "rock in its tones, shaded by height; structures tinted per piece; guns red,", font=fnt, fill=(200, 200, 200, 255))
        d.text((10 + dx_, 50), "stations green, routes coloured, moored ships yellow, gunship patrols orange", font=fnt, fill=(200, 200, 200, 255))

    img.save(os.path.join(OUT, "check_top.png"), optimize=True)
    # sections, vertical scale 1 px = 1 m (x 2 of the plan's scale)
    panels = []
    for s in M["secs"]:
        im = Image.frombytes("RGB", (s["w"], s["h"]), open(os.path.join(work, f"sec_{s['axis']}.rgb"), "rb").read())
        dd = ImageDraw.Draw(im, "RGBA")
        for y, lab, col in ((0, "y 0 flight plane", (124, 255, 154)), (24, "+24 cut", (255, 179, 71)), (-24, "-24 floor", (255, 179, 71)),
                            (-72, "-72", (120, 120, 120))):
            yy = 150 - y
            for x in range(0, s["w"], 12):
                dd.line([(x, yy), (x + 6, yy)], fill=col + (150,))
            dd.text((4, yy - 16), lab, font=fnt, fill=col + (220,))
        title = ("section x = 0 (the Maw's axis), south left, north right" if s["axis"] == "x"
                 else f"section z = {s['val']:.0f} (through the Market), west left, east right")
        dd.text((4, 4), title + f"; {s['lo']} .. {s['hi']} m", font=fnt, fill=(255, 255, 255, 255))
        for g in L["guns"]:
            h = g["z"] if s["axis"] == "x" else g["x"]
            o = g["x"] if s["axis"] == "x" else g["z"]
            if abs(o - s["val"]) < 12:
                dd.ellipse([h - s["lo"] - 5, 145, h - s["lo"] + 5, 155], fill=(255, 60, 40, 255))
        panels.append(im)
    W = max(p.width for p in panels); Hh = sum(p.height for p in panels) + 10 * (len(panels) - 1)
    out = Image.new("RGB", (W, Hh), (0, 0, 0))
    y = 0
    for p in panels:
        out.paste(p, (0, y)); y += p.height + 10
    out.save(os.path.join(OUT, "check_section.png"), optimize=True)


# ======================================================================== driver
def numpy_python():
    try:
        import numpy  # noqa: F401
        return sys.executable
    except ImportError:
        pass
    env = os.environ.get("HOLLOW_NUMPY_PY")
    cands = ([env] if env else []) + sorted(glob.glob("/Applications/Blender.app/Contents/Resources/*/python/bin/python3*"), reverse=True)
    for c in cands:
        if c and os.path.exists(c):
            r = subprocess.run([c, "-c", "import numpy"], capture_output=True)
            if r.returncode == 0:
                return c
    sys.exit("hollow_export: no python with numpy found (set HOLLOW_NUMPY_PY)")


def main():
    if len(sys.argv) >= 4 and sys.argv[1] == "--compute":
        stage_compute(sys.argv[2], sys.argv[3])
        return
    work = tempfile.mkdtemp(prefix="hollow_")
    try:
        dpath = os.path.join(work, "design.json")
        stage_design(dpath)
        py = numpy_python()
        if py == sys.executable:
            stage_compute(dpath, work)
        else:
            r = subprocess.run([py, os.path.abspath(__file__), "--compute", dpath, work])
            if r.returncode != 0:
                sys.exit(r.returncode)
        stage_draw(work)
        print(f"wrote {OUT_JSON} ({os.path.getsize(OUT_JSON) / 1e6:.2f} MB),", os.path.join(OUT, "check_top.png"), "and",
              os.path.join(OUT, "check_section.png"))
    finally:
        shutil.rmtree(work, ignore_errors=True)


if __name__ == "__main__":
    main()
