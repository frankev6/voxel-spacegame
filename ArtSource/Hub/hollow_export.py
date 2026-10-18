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


def chaikin(pts, closed=False, iters=3):
    """Corner cutting: a polyline's corners rounded (the ends of an open one kept)."""
    P = list(pts[:-1]) if closed and pts[0] == pts[-1] else list(pts)
    for _ in range(iters):
        Q = [] if closed else [P[0]]
        n = len(P)
        for i in range(n if closed else n - 1):
            a, b = P[i], P[(i + 1) % n]
            Q += [(0.75 * a[0] + 0.25 * b[0], 0.75 * a[1] + 0.25 * b[1]), (0.25 * a[0] + 0.75 * b[0], 0.25 * a[1] + 0.75 * b[1])]
        P = Q if closed else Q + [P[-1]]
    return P + [P[0]] if closed else P


def arclen(pts):
    s = [0.0]
    for a, b in zip(pts, pts[1:]):
        s.append(s[-1] + math.dist(a, b))
    return s


def normals(pts, closed=False):
    """Unit right-hand normals of a dense polyline."""
    n = len(pts)
    out = []
    for i in range(n):
        a = pts[i - 1] if (closed or i > 0) else pts[i]
        b = pts[(i + 1) % n] if (closed or i < n - 1) else pts[i]
        dx, dz = b[0] - a[0], b[1] - a[1]
        L = math.hypot(dx, dz) or 1.0
        out.append((dz / L, -dx / L))
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

    # ------------------------------------------------------------ natural tunnels (owner: "the tunnels should be tunneled less
    # linear and more naturally, also ... there is a tunnel that doesn't reach its destination")
    def naturalise(self):
        """Every tunnel bored through the rock, not extruded. The Ring's corners rounded and its line meandering in gentle
        S-curves (no straight run over ~80 m), still meeting the Maw's throat head on; each side tunnel meandering between its
        ends, starting on the new Ring and running on into its cavern when it stopped short of it (the Market's south tunnel
        stopped 3.5 m short of the Market's wall: the owner's dead end); each one's width breathing along it and flaring where
        it opens. The Maw's throat stays straight (its doors, field gate and guns are laid along it); its walls get the noise."""
        smooth = lambda t: t * t * (3 - 2 * t)
        new, self.tprof, self.tgoal = [], [], []       # tgoal: where each side tunnel leads (a cavern's index, "den", "out")
        ring0, maw0 = self.ring, self.maw_t
        # the Ring: a whole number of waves round it, so it closes; held still where the Maw comes in
        P = densify(chaikin(densify(ring0, 40.0), closed=True, iters=3), 8.0)     # corners rounded locally, ~15 m in
        P[-1] = P[0]
        s = arclen(P); tot = s[-1]
        N = normals(P[:-1], closed=True); N.append(N[0])
        k1 = max(3, round(tot / 240)); k2 = max(k1 + 2, round(tot / 105))
        ring, hw = [], []
        for p, si, n in zip(P, s, N):
            ph = 2 * math.pi * si / tot
            hold = smooth(min(math.dist(p, maw0[-1]) / 110, 1.0))
            off = hold * (10.0 * math.sin(k1 * ph + 0.7) + 4.0 * math.sin(k2 * ph + 2.1))
            ring.append((p[0] + n[0] * off, p[1] + n[1] * off))
            hw.append(41.5 + 2.5 * math.sin((k1 + 1) * ph + 1.3) + 1.5 * math.sin(k2 * ph - 0.4))   # 75..91 m wide
        ring[-1] = ring[0]
        on_ring = lambda q: min(((seg_dist(q, a, b), a, b) for a, b in zip(ring, ring[1:])), key=lambda t: t[0])
        for pts, w in self.tunnels:
            if pts is ring0:
                new.append((ring, w)); self.tprof.append(dict(kind="ring", s=np.array(arclen(ring)), hw=np.array(hw), base=min(hw) - 2))
                self.tgoal.append(None)
                continue
            if pts is maw0:
                new.append((maw0, w)); self.tprof.append(dict(kind="maw", s=np.array(arclen(maw0)), hw=np.full(len(maw0), w / 2), base=w / 2))
                self.tgoal.append(None)
                continue
            traffic = pts is self.back                          # the back door: one-way traffic flies out through it
            pts = list(pts)
            if poly_dist(pts[0], ring0) < 5:
                _, a, b = on_ring(pts[0])                       # it starts on the Ring: on the new one
                ax, az = b[0] - a[0], b[1] - a[1]
                t = max(0.0, min(1.0, ((pts[0][0] - a[0]) * ax + (pts[0][1] - a[1]) * az) / (ax * ax + az * az or 1)))
                pts[0] = (a[0] + ax * t, a[1] + az * t)
            c, r = min(((cv[0], cv[1]) for cv in self.caverns if cv[3] is not None), key=lambda cv: math.dist(cv[0], pts[-1]) - cv[1])
            gap = math.dist(c, pts[-1]) - w / 2 - r
            self.tgoal.append(next(i for i, cv in enumerate(self.caverns) if cv[0] == c) if gap < 40 else
                              "out" if float(self.rf.field(*pts[-1])) <= 0 else "den")
            if -w / 2 < gap < 40:                               # it ends at a cavern: run it well into it
                u = ((pts[-1][0] - c[0]) / math.dist(c, pts[-1]), (pts[-1][1] - c[1]) / math.dist(c, pts[-1]))
                pts.append((c[0] + u[0] * r * 0.7, c[1] + u[1] * r * 0.7))
                if gap > 0:
                    self.log.append(f"dead end fixed: the tunnel from ({pts[0][0]:.0f}, {pts[0][1]:.0f}) stopped {gap:.1f} m short "
                                    f"of the cavern at ({c[0]:.0f}, {c[1]:.0f}); it now runs {r * 0.3 + gap + w / 2:.0f} m on into it")
            D = densify(chaikin(densify(pts, 30.0), iters=3), 8.0)
            s = arclen(D); tot = s[-1]
            N = normals(D)
            amp = 6.0 if tot < 160 else 11.0
            wl = max(110.0, min(200.0, tot / 1.5))
            base = (max(w, 76.0) if traffic else w) / 2
            seed = (pts[0][0] * 0.013 + pts[0][1] * 0.007) % 6.283
            out, hws = [], []
            for p, si, n in zip(D, s, N):
                hold = smooth(min(si / 45, (tot - si) / 45, 1.0))
                off = hold * amp * (math.sin(2 * math.pi * si / wl + seed) + 0.35 * math.sin(2 * math.pi * si / (wl * 0.45) + 2 * seed))
                out.append((p[0] + n[0] * off, p[1] + n[1] * off))
                flare = 0.4 * math.exp(-si / 30) + (0.0 if traffic else 0.4 * math.exp(-(tot - si) / 30))
                hws.append(max(base * (1 + 0.22 * math.sin(2 * math.pi * si / (wl * 0.7) + seed * 1.7)), 38.0 if traffic else 23.0) * (1 + flare))
            new.append((out, w))
            self.tprof.append(dict(kind="side", s=np.array(arclen(out)), hw=np.array(hws),
                                   base=36.0 if traffic else min(0.8 * w / 2, min(hws) - 2)))    # the back door is meant to open
            if traffic:
                self.back = out
        self.tunnels = new
        self.ring = ring
        self.ring_w = 2 * (max(hw) + 6)

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
        rng = random.Random(43)
        edge = edt(~foot, 12) * CC                            # m from the rock's outside: nothing natural breaks through it
        wn = 2.2 * noise(GX * 1.9, GZ * 1.9, 31) + 1.0 * noise(GX * 4.3, GZ * 4.3, 37)     # the walls' noise, about +-5 m
        # caverns: an outline that bulges and wanders (outward only, so nothing built in them is buried)
        for idx, (c, r, fl, ce) in enumerate(self.caverns):
            if ce is None:
                continue                                      # a crater: a dent in the top only, never a hole
            dd = np.hypot(GX - c[0], GZ - c[1])
            th = np.arctan2(GX - c[0], GZ - c[1])
            h = 0.6 * np.sin(3 * th + idx * 1.7) + 0.4 * np.sin(5 * th - idx * 2.9)
            reff = r * (1 + 0.09 * (0.5 + 0.5 * h)) + np.abs(wn) * 0.8
            mm = foot & ((dd < r) | ((dd < reff) & (edge >= 12)))
            kind[mm] = 3; cav[mm] = idx
            fj[mm] = -8 if idx == self.market_i else -6        # the Market's floor at -32
        # tunnels: each one's width (its profile along it) plus the walls' noise; beyond its design width only away from the
        # rock's outside, so a wall never breaks through to space (the Maw's throat and the back door are meant to)
        self.tdist = []
        corr = np.zeros(foot.shape, bool)
        tunnel_at = np.zeros(foot.shape, bool)
        for (pts, w), pr in zip(self.tunnels, self.tprof):
            d = np.full(foot.shape, np.inf); sn = np.zeros(foot.shape)
            S = pr["s"]
            for i, ((ax, ay), (bx, by)) in enumerate(zip(pts, pts[1:])):
                dx, dy = bx - ax, by - ay
                L2 = dx * dx + dy * dy
                t = np.clip(((GX - ax) * dx + (GZ - ay) * dy) / L2, 0, 1) if L2 > 0 else 0.0
                dd = np.hypot(GX - (ax + dx * t), GZ - (ay + dy * t))
                better = dd < d
                d = np.where(better, dd, d); sn = np.where(better, S[i] + t * math.sqrt(L2), sn)
            self.tdist.append(d)
            if pr["kind"] == "maw":
                cut = d < w / 2 + np.maximum(wn, 0) * 0.6      # the throat: bulges only (its doors and guns stand on its walls)
            else:
                cut = (d < pr["base"]) | ((d < np.interp(sn, S, pr["hw"]) + wn) & (edge >= 12))
            corr |= cut
            mm = foot & (kind == 1) & cut
            kind[mm] = 2
            if pr["kind"] != "maw":
                tunnel_at |= mm
        self._corr = corr
        # alcoves and side pockets, 10-30 m deep, beside the lanes (half of them a pit, floor -28)
        self.pockets = 0
        for (pts, w), pr in zip(self.tunnels, self.tprof):
            if pr["kind"] == "maw":
                continue
            S, HW = pr["s"], pr["hw"]
            Nn = normals(pts, closed=pr["kind"] == "ring")
            sp = rng.uniform(40, 120)
            while sp < S[-1] - 40:
                i = int(np.searchsorted(S, sp))
                side = rng.choice((-1, 1)); depth = rng.uniform(10, 30)
                cc = (pts[i][0] + Nn[i][0] * side * (HW[i] + depth * 0.35), pts[i][1] + Nn[i][1] * side * (HW[i] + depth * 0.35))
                rad = depth * 0.55 + 6
                mm = foot & (kind == 1) & (edge >= 12) & (np.hypot(GX - cc[0], GZ - cc[1]) < rad * (1 + 0.12 * noise(GX * 3, GZ * 3, 47)))
                if mm.any():
                    kind[mm] = 2; tunnel_at |= mm; self.pockets += 1
                    if rng.random() < 0.5:
                        fj[mm] = -7
                sp += rng.uniform(110, 190)
        # the tunnels' floors wander a little: raised shelves at -20, dips to -28 (the Maw's throat stays level)
        nf = noise(GX * 1.3 + 50, GZ * 1.3, 23) / 1.6
        fj = np.where(tunnel_at & (nf > 0.55), -5, np.where(tunnel_at & (nf < -0.6), -7, fj)).astype(np.int16)
        # the Den: the rammed warship's hangar is cut into the rock where its stern lies in it
        hl = self.hulk_frame(GX, GZ)
        mm = foot & (np.abs(hl[1]) < self.hulk["width"] / 2 - 4) & (hl[0] > -self.hulk_body - 4) & (hl[0] < 4) & (kind == 1)
        kind[mm] = 3; cav[mm] = -2
        # pillars and rock spurs left standing in the wide places, well off every route, station and building
        lanes = self.pre_lanes = [[tuple(p) for p in r["route"]] + ([tuple(r["route"][0])] if r["loop"] else []) for r in self.routes()]
        spots = [(s_["x"], s_["z"]) for s_ in self.stations()] + [centroid([tuple(q) for q in b["pts"]]) for b in self.opt["buildings"]]
        wall = edt(kind < 2, 10) * CC
        hulk_zone = (np.abs(hl[1]) < self.hulk["width"] / 2 + 10) & (hl[0] > -self.hulk_body - 20) & (hl[0] < 200)
        cand = np.argwhere((kind >= 2) & (cav != -2) & ~hulk_zone)
        self.pillars = []
        for n in rng.sample(range(len(cand)), min(len(cand), 4000)):
            if len(self.pillars) >= 18:
                break
            i, k = cand[n]
            p = (float(GX[i, k]), float(GZ[i, k]))
            R = rng.uniform(6, 12)
            wd = float(wall[i, k])
            if not (wd < 0.8 * R or R + 12 < wd < R + 40):   # a spur off a wall, or a pillar with room to fly round it
                continue
            if min(poly_dist(p, L_) for L_ in lanes) < R + 40 or min(math.dist(p, q) for q in spots) < R + 40:
                continue
            if any(math.dist(p, q) < 90 for q in self.pillars):
                continue
            mm = foot & (np.hypot(GX - p[0], GZ - p[1]) < R * (1 + 0.18 * noise(GX * 2.6, GZ * 2.6, 53)))
            kind[mm] = 1; cav[mm] = -1
            self.pillars.append(p)
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
                if self.col_kind(*tip)[0] == 2 or min(poly_dist(q, L_) for q in (tip, c) for L_ in self.pre_lanes) < 30:
                    # its outer end stands in a tunnel, or it reaches into a route: it would block the lane
                    self.log.append(f"docks arm at bearing {p_['heading']:.0f} dropped: it stands across a tunnel mouth or a route")
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
                # the design's tunnel-edge lines ran straight along the old axes, across cavern floors, rock and out past the
                # Maw: the tunnels' edges are lit by the light strips along the walls' feet instead (Furnisher.wall_pipes), in
                # short segments that bend with the wall and stop at each cavern's mouth
                continue
            elif f["type"] == "neon":
                a = polar(f["heading"], -f["length"] / 2, f["c"]); b = polar(f["heading"], f["length"] / 2, f["c"])
                y = self.light_y(*f["c"], f["z"])
                if any(self.col_kind(*q)[0] == 1 and y < self.ytop_at(*q) for q in (a, b, f["c"])):
                    self.log.append(f"neon bar at ({f['c'][0]:.0f}, {f['c'][1]:.0f}) dropped: the natural walls put it in the rock")
                    continue
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
                    # beside the launch lane, alternately left and right of it, not on it (the Den's traffic flies out there)
                    side = 1 if len(gs) % 2 else -1
                    q = polar(self.hulk["heading"] + 90 * side, 45, tuple(s["c"]))
                    gs.append(dict(blueprint="ship_nocturne_wraith", x=round(q[0], 1), z=round(q[1], 1), radius=110.0))
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
        # keep right on the Ring and its connectors (owner: "most ships are just colliding everywhere"): every route on the Ring
        # flies the loops' lane for its direction, 10 m right of the centreline, so no two routes meet head on
        kr = lambda pts: offset_line(pts, 10)
        to_m = min((ring_path(jm, sm), ring_path(jm, (sm + 1) % n)), key=lambda q: poly_len(q) + math.dist(q[-1], join))
        r1 = maw_in + kr(to_m + [join, polar(jb, R, mk)]) + lane(jb, jb + 200)
        r2 = offset_line(self.ring, 10, closed=True)
        r3 = offset_line(self.ring[::-1], 10, closed=True)
        back = self.back
        sd, jd = nearest_seg(self.docks)
        # to the Docks through their wide mouth on the Ring (the 50 m spur to their south leaves a ship 23 m from its walls)
        to_d = min((ring_path(jm, sd), ring_path(jm, (sd + 1) % n)), key=lambda q: poly_len(q) + math.dist(q[-1], jd))
        r4 = maw_in + kr(to_d + [jd, self.docks])
        r5 = kr([self.docks, jd] + min((ring_path(sd, idx(back[0])), ring_path((sd + 1) % n, idx(back[0]))),
                                       key=lambda q: poly_len(q) + math.dist(q[0], jd))) + list(back[1:])
        ext = polar(math.degrees(math.atan2(back[-1][0] - back[-2][0], back[-1][1] - back[-2][1])), 200, back[-1])
        r5 = r5 + [ext]
        r6 = lane(0, 360)
        r7 = lane(jb + 160, jb + 360) + kr([polar(jb + 360, R, mk), join] + list(reversed(to_m))) + maw_out
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

    # ------------------------------------------------------------ berths (owner: "most ships are just colliding everywhere")
    def berth_ok(self, q, m, placed, L, occ, routes):
        """Whether a moored ship (blueprint length l) may lie at q: in open air beyond its half-length + 15 m from rock and
        structure cells at the flight plane, 10 m clear of the other berths, 40 m from every route, off the stations' circles,
        the gunship posts and the field's boulders, on the same side of the rock's edge as the design put it."""
        h = ship_len(m["blueprint"]) / 2
        b = L["bounds"]
        if not (b[0] + h < q[0] < b[2] - h and b[1] + h < q[1] < b[3] - h):
            return False
        k = self.col_kind(*q)[0]
        if k == 1 or (k == 0) != m["_out"]:
            return False
        if any(math.dist(q, (o["x"], o["z"])) < h + ship_len(o["blueprint"]) / 2 + BERTH_GAP for o in placed):
            return False
        if any(math.dist(q, (s["x"], s["z"])) < STATION_R + h + 2 for s in L["stations"]):
            return False
        if any(math.dist(q, (g["x"], g["z"])) < h + 30 for g in L["gunships"]):
            return False
        if any(math.dist(q, (bo["x"], bo["z"])) < boulder_reach(bo["r"]) + h + BERTH_GAP for bo in L["boulders"]):
            return False
        if any(poly_dist(q, pts) < max(BERTH_LANE, h + ROUTE_CLEAR) for pts in routes):
            return False
        return occ.clear_at_least(q, h + BERTH_ROCK)

    def berth_moored(self, L, occ):
        """Every moored ship where the design docked it if that berth is clear; else the nearest clear berth within 160 m; else
        dropped (never overlapped)."""
        routes = [route_pts(r) for r in L["traffic"]]
        ships = L["moored"]
        for m in ships:
            m["_out"] = self.col_kind(m["x"], m["z"])[0] == 0
        placed, left = [], []
        for m in ships:
            (placed if self.berth_ok((m["x"], m["z"]), m, placed, L, occ, routes) else left).append(m)
        moved = dropped = 0
        for n, m in enumerate(left):
            spot = None
            for r in range(4, 161, 4):
                for a in range(0, 360, 10):
                    q = polar(a + n * 7, r, (m["x"], m["z"]))
                    if self.berth_ok(q, m, placed, L, occ, routes):
                        spot = q; break
                if spot:
                    break
            if spot:
                self.log.append(f"moored {m['blueprint'][14:]} at ({m['x']:.0f}, {m['z']:.0f}) moved {r} m to a clear berth")
                m["x"], m["z"] = round(spot[0], 1), round(spot[1], 1)
                placed.append(m); moved += 1
            else:
                self.log.append(f"moored {m['blueprint'][14:]} at ({m['x']:.0f}, {m['z']:.0f}) dropped: no clear berth within 160 m")
                dropped += 1
        order = {id(m): i for i, m in enumerate(ships)}
        L["moored"] = sorted(placed, key=lambda m: order[id(m)])
        self.berths = dict(design=len(ships), kept=len(ships) - len(left), moved=moved, dropped=dropped)

    def recheck_berths(self, L, occ):
        """After the routes moved: drop any berth a route now passes too close to (and say so)."""
        routes = [route_pts(r) for r in L["traffic"]]
        keep = []
        for m in L["moored"]:
            if self.berth_ok((m["x"], m["z"]), m, keep, L, occ, routes):
                keep.append(m)
            else:
                self.log.append(f"moored {m['blueprint'][14:]} at ({m['x']:.0f}, {m['z']:.0f}) dropped after the routes moved")
                self.berths["dropped"] += 1
        L["moored"] = keep


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


# ---------------------------------------------------------------- the static pieces on one grid
def edt(occ, R):
    """Distance in cells from each cell's centre to the nearest edge of an occupied cell: exact, capped at R cells."""
    W, H = occ.shape
    g = np.zeros((W, H))
    last = np.full(H, -1e9)
    for i in range(W):
        last = np.where(occ[i], i, last); g[i] = i - last
    last = np.full(H, 1e9)
    for i in range(W - 1, -1, -1):
        last = np.where(occ[i], i, last); g[i] = np.minimum(g[i], last - i)
    g = np.minimum(np.maximum(g - 0.5, 0.0), R) ** 2
    d2 = g.copy()
    for dz in range(1, R + 1):
        e = (dz - 0.5) ** 2
        d2[:, dz:] = np.minimum(d2[:, dz:], g[:, :-dz] + e)
        d2[:, :-dz] = np.minimum(d2[:, :-dz], g[:, dz:] + e)
    return np.minimum(np.sqrt(d2), R)


class Occupancy:
    """Every piece's blocks on one global 2 m grid: the flight band (blocks touching y -8..+8, and the guns' grids), the columns
    of the high layer (the cutaway mask), the floor band y -36..-8 in 3D (where the floor clutter goes) and the band's
    distance field (for the berths and the routes)."""
    J0, J1 = -18, -4                                     # the floor band, cells: y -36 .. -8

    def __init__(self, pieces, guns, field=True):
        allB = []
        for p in pieces:
            B, oi, ok_ = world_cells(p)
            allB.append((p, B, oi, ok_))
        self.allB = allB
        gx0 = min(oi + int(B[:, 0].min()) for _, B, oi, _ in allB) - 2
        gz0 = min(ok_ + int(B[:, 2].min()) for _, B, _, ok_ in allB) - 2
        gx1 = max(oi + int((B[:, 0] + B[:, 3]).max()) for _, B, oi, _ in allB) + 2
        gz1 = max(ok_ + int((B[:, 2] + B[:, 3]).max()) for _, B, _, ok_ in allB) + 2
        self.gx0, self.gz0, self.gx1, self.gz1 = gx0, gz0, gx1, gz1
        W, H = self.W, self.H = gx1 - gx0, gz1 - gz0
        band = np.zeros((W, H), bool); bpid = np.full((W, H), -1, np.int32); high = np.zeros((W, H), bool)
        low = np.zeros((W, self.J1 - self.J0, H), bool)
        for n, (p, B, oi, ok_) in enumerate(allB):
            for sz in (1, 2, 3, 4):
                Bs = B[B[:, 3] == sz]
                if not len(Bs):
                    continue
                for a in range(sz):
                    for c in range(sz):
                        ii = Bs[:, 0] + oi - gx0 + a; kk = Bs[:, 2] + ok_ - gz0 + c
                        if p["motion"] == "static":
                            bsel = (Bs[:, 1] < 4) & (Bs[:, 1] + sz > -4)
                            band[ii[bsel], kk[bsel]] = True
                            bpid[ii[bsel], kk[bsel]] = n
                            for b_ in range(sz):
                                jj = Bs[:, 1] + b_ - self.J0
                                ls = (jj >= 0) & (jj < low.shape[1])
                                low[ii[ls], jj[ls], kk[ls]] = True
                        if p["layer"] == "high":
                            high[ii, kk] = True
        # guns' grids stand in the band too
        for gi_, g in enumerate(guns):
            if g["fort"] >= 0:
                continue
            a = 9 if g["big"] else 7
            i0, i1 = int((g["x"] - a) / CELL) - gx0, int((g["x"] + a) / CELL) - gx0
            k0, k1 = int((g["z"] - a) / CELL) - gz0, int((g["z"] + a) / CELL) - gz0
            band[max(i0, 0):i1 + 1, max(k0, 0):k1 + 1] = True
            bpid[max(i0, 0):i1 + 1, max(k0, 0):k1 + 1] = -100 - gi_
        self.band, self.bpid, self.high, self.low = band, bpid, high, low
        if field:
            self.df = edt(band, 40) * CELL
            self.dgx, self.dgz = np.gradient(self.df)

    def cell(self, x, z):
        return (np.clip(np.floor(np.asarray(x) / CELL).astype(int) - self.gx0, 0, self.W - 1),
                np.clip(np.floor(np.asarray(z) / CELL).astype(int) - self.gz0, 0, self.H - 1))

    def clear(self, p, reach=60.0):
        """Exact distance from p to the nearest cell in the flight band (capped at reach)."""
        ci_, ck_ = p[0] / CELL - self.gx0, p[1] / CELL - self.gz0
        r = int(reach / CELL) + 1
        i0, i1 = max(int(ci_) - r, 0), min(int(ci_) + r + 1, self.W); k0, k1 = max(int(ck_) - r, 0), min(int(ck_) + r + 1, self.H)
        if i1 <= i0 or k1 <= k0:
            return reach
        sub = self.band[i0:i1, k0:k1]
        if not sub.any():
            return reach
        ii, kk = np.nonzero(sub)
        dx = np.maximum(np.abs(ii + i0 + 0.5 - ci_) - 0.5, 0); dz = np.maximum(np.abs(kk + k0 + 0.5 - ck_) - 0.5, 0)
        return float(min(np.hypot(dx, dz).min() * CELL, reach))

    def clear_at_least(self, p, need):
        """Whether p is at least `need` m from the band (the field first, exact only near the threshold)."""
        i, k = self.cell(p[0], p[1])
        v = float(self.df[i, k])
        if v >= need + 1.6 and v < 79:
            return True
        if v < need - 1.6:
            return False
        return self.clear(p, need + 4) >= need

    def blame(self, p):
        ci_, ck_ = int(p[0] / CELL) - self.gx0, int(p[1] / CELL) - self.gz0
        sub = self.bpid[max(ci_ - 14, 0):ci_ + 15, max(ck_ - 14, 0):ck_ + 15]
        return sorted({self.allB[v][0]["id"] if v >= 0 else "gun %d" % (-100 - v) for v in np.unique(sub[sub != -1])})


# ---------------------------------------------------------------- ships and routes (owner: "most ships are just colliding everywhere")
SHIP_LEN = {"ship_nocturne_wraith": 20.0, "ship_nocturne_vesper": 45.0}    # about a third as wide as long; any other: 50 m
ROUTE_CLEAR = 25.0     # a route: from rock / structure / gun cells in y -8..+8, gunship posts, moored hulls and boulders
BERTH_ROCK = 15.0      # a moored ship: beyond its half-length from rock / structure cells in y -8..+8
BERTH_GAP = 10.0       # two moored ships: beyond the sum of their half-lengths, centre to centre
BERTH_LANE = 40.0      # a moored ship's centre from every route
STATION_R = 18.0       # a station's circle (PlaygroundHollow.StationRadius)
CLUTTER_LANE = 30.0    # floor clutter from every route
BOULDER_R = (10, 20, 30, 44)


def ship_len(bp):
    return SHIP_LEN.get(bp, 50.0)


def boulder_reach(r):
    """How far a field boulder's lumps reach: the game's rock of the nearest radius, bumps up to 30 % (HollowBuilder.Rock)."""
    return 1.3 * min(BOULDER_R, key=lambda b: abs(b - r))


def route_pts(r):
    return [tuple(p) for p in r["route"]] + ([tuple(r["route"][0])] if r["loop"] else [])


def densify(pts, step):
    out = [tuple(pts[0])]
    for a, b in zip(pts, pts[1:]):
        n = max(1, int(math.ceil(math.dist(a, b) / step)))
        out += [(a[0] + (b[0] - a[0]) * k / n, a[1] + (b[1] - a[1]) * k / n) for k in range(1, n + 1)]
    return out


def rdp(pts, tol):
    """Douglas-Peucker: the fewest points within tol of the line."""
    if len(pts) < 3:
        return list(pts)
    d = [seg_dist(p, pts[0], pts[-1]) for p in pts[1:-1]]
    i = max(range(len(d)), key=d.__getitem__)
    if d[i] <= tol:
        return [pts[0], pts[-1]]
    return rdp(pts[:i + 2], tol)[:-1] + rdp(pts[i + 1:], tol)


def route_obstacles(L):
    """Point obstacles for the routes: (x, z, radius, need from the centre)."""
    obs = [(g["x"], g["z"], 0.0, ROUTE_CLEAR, "gunship post") for g in L["gunships"]]
    for m in L["moored"]:
        h = ship_len(m["blueprint"]) / 2
        obs.append((m["x"], m["z"], h, max(BERTH_LANE, h + ROUTE_CLEAR), "moored " + m["blueprint"][14:]))
    for b in L["boulders"]:
        r = boulder_reach(b["r"])
        obs.append((b["x"], b["z"], r, r + ROUTE_CLEAR, "boulder"))
    return obs


def route_clearance(occ, r, obs, step=5.0):
    """A route sampled every `step` m: (minimum clearance, where, what): to the band's cells, gunship posts, moored hulls (their
    centre less half their length) and boulders (their centre less their lumps' reach)."""
    best = (1e9, None, "")
    for q in densify(route_pts(r), step):
        c = occ.clear(q, 40.0)
        if c < best[0]:
            best = (c, q, "rock/structure")
        for x, z, rad, need, what in obs:
            d = math.dist(q, (x, z)) - rad
            if d < best[0]:
                best = (d, q, what)
    return best


def nudge(occ, pts, loop, obs, target=ROUTE_CLEAR + 2.5, iters=120):
    """Push a route off whatever it passes too close to: the dense route's displacement is pushed by the band's distance field and
    the point obstacles, then smoothed along the route, so only the offending stretches move and the rest keeps its exact shape."""
    P0 = np.array(densify(pts, 5.0))
    if loop:
        P0 = P0[:-1]
    D = np.zeros_like(P0)
    O = np.array([(x, z) for x, z, _, _, _ in obs]) if obs else np.zeros((0, 2))
    need = np.array([n for _, _, _, n, _ in obs]) + 2.5 if obs else np.zeros(0)
    for _ in range(iters):
        P = P0 + D
        i, k = occ.cell(P[:, 0], P[:, 1])
        c = occ.df[i, k]
        gx, gz = occ.dgx[i, k], occ.dgz[i, k]
        gl = np.hypot(gx, gz) + 1e-9
        push = np.clip(target - c, 0, 1.5)
        D[:, 0] += gx / gl * push; D[:, 1] += gz / gl * push
        if len(O):
            V = P[:, None, :] - O[None, :, :]
            T = np.gradient(P0, axis=0)                 # a route right over an obstacle steps aside, to its right
            Nr = np.column_stack([T[:, 1], -T[:, 0]]) / (np.hypot(T[:, 0], T[:, 1])[:, None] + 1e-9)
            V = V + Nr[:, None, :] * 0.05
            dist = np.hypot(V[..., 0], V[..., 1]) + 1e-9
            amt = np.clip(need[None, :] - dist, 0, 1.5)
            D += (V / dist[..., None] * amt[..., None]).sum(axis=1)
        for _ in range(2):                              # smooth the displacement along the route
            if loop:
                D = D + 0.35 * ((np.roll(D, 1, 0) + np.roll(D, -1, 0)) / 2 - D)
            else:
                D[1:-1] = D[1:-1] + 0.35 * ((D[:-2] + D[2:]) / 2 - D[1:-1])
    P = P0 + D
    out = [tuple(map(float, p)) for p in P]
    if loop:
        out.append(out[0])
    out = rdp(out, 0.5)
    out = resample(out, 60.0)
    if loop:
        out = out[:-1]
    return out


# ---------------------------------------------------------------- wayfinding: reachability, junctions, the stations' paths
class Wayfinder:
    """Shortest paths through the flight band on an 8 m grid, over cells at least `clear` m from rock, structure and guns at
    y -8..+8 (keeping to the middle where it can): what is reachable from the Maw, every tunnel's connection, the junctions'
    signposts and each station's path from the Maw."""
    C = 8.0
    N8 = [(1, 0, 1.0), (-1, 0, 1.0), (0, 1, 1.0), (0, -1, 1.0), (1, 1, 1.414), (1, -1, 1.414), (-1, 1, 1.414), (-1, -1, 1.414)]

    def __init__(self, occ, clear=8.0):
        f = int(self.C / CELL)
        W, H = occ.W // f, occ.H // f
        df = occ.df[:W * f, :H * f].reshape(W, f, H, f).min(axis=(1, 3))
        self.W, self.H = W, H
        self.free = (df >= clear).ravel()
        self.pen = (1 + 2 * np.clip(30 - df, 0, 30) / 30).ravel()
        self.x0, self.z0 = occ.gx0 * CELL, occ.gz0 * CELL
        self.runs = {}

    def node(self, p):
        i = int((p[0] - self.x0) / self.C); k = int((p[1] - self.z0) / self.C)
        best = None
        for r in range(0, 4):                           # the nearest free node within 3 nodes
            for di in range(-r, r + 1):
                for dk in range(-r, r + 1):
                    a, b = i + di, k + dk
                    if 0 <= a < self.W and 0 <= b < self.H and self.free[a * self.H + b]:
                        d = di * di + dk * dk
                        if best is None or d < best[0]:
                            best = (d, a * self.H + b)
            if best:
                return best[1]
        return None

    def at(self, n):
        i, k = divmod(n, self.H)
        return (self.x0 + (i + 0.5) * self.C, self.z0 + (k + 0.5) * self.C)

    def run(self, key, p):
        """Dijkstra from p: (cost to every node, the next node toward p)."""
        import heapq
        W, H, free, pen = self.W, self.H, self.free, self.pen
        dist = np.full(W * H, np.inf); nxt = np.full(W * H, -1, np.int64)
        src = self.node(p)
        dist[src] = 0.0
        heap = [(0.0, src)]
        while heap:
            d, n = heapq.heappop(heap)
            if d > dist[n]:
                continue
            i, k = divmod(n, H)
            for di, dk, ln in self.N8:
                a, b = i + di, k + dk
                if not (0 <= a < W and 0 <= b < H):
                    continue
                m = a * H + b
                if not free[m] or (di and dk and not (free[a * H + k] and free[i * H + b])):
                    continue
                nd = d + ln * 0.5 * (pen[n] + pen[m])
                if nd < dist[m]:
                    dist[m] = nd; nxt[m] = n
                    heapq.heappush(heap, (nd, m))
        self.runs[key] = (dist, nxt, src)
        return self.runs[key]

    def reach(self, key, p):
        n = self.node(p)
        return n is not None and np.isfinite(self.runs[key][0][n])

    def chain(self, key, p):
        """The nodes from p to the run's source."""
        dist, nxt, src = self.runs[key]
        n = self.node(p)
        if n is None or not np.isfinite(dist[n]):
            return None
        out = [n]
        while out[-1] != src:
            out.append(int(nxt[out[-1]]))
        return out

    def heading(self, key, p, ahead=40.0):
        """The yaw of the first leg of the shortest path from p toward the run's source."""
        c = self.chain(key, p)
        if not c or len(c) < 2:
            return None
        q = self.at(c[min(len(c) - 1, int(ahead / self.C))])
        return round(math.atan2(q[0] - p[0], q[1] - p[1]) % (2 * math.pi), 4)


def path_points(wf, occ, key, p, step=20.0):
    """The shortest flight-band path from the run's source to p, smoothed where that stays clear, a point every ~20 m."""
    c = wf.chain(key, p)
    if not c:
        return None
    raw = [wf.at(n) for n in reversed(c)]
    sm = []
    for i in range(len(raw)):
        w = raw[max(0, i - 3):i + 4]
        q = (sum(a for a, _ in w) / len(w), sum(b for _, b in w) / len(w))
        sm.append(q if occ.clear(q, 12.0) >= 6.0 else raw[i])
    sm[0], sm[-1] = raw[0], tuple(p)
    out, acc = [sm[0]], 0.0
    for a, b in zip(sm, sm[1:]):
        acc += math.dist(a, b)
        if acc >= step:
            out.append(b); acc = 0.0
    if out[-1] != sm[-1]:
        out.append(sm[-1])
    return [[round(x, 1), round(z, 1)] for x, z in out]


def wayfind(E, occ, L):
    """Reachability from the Maw (every station, den, dock, berth and tunnel end), each side tunnel's connection to where it
    leads, the junctions' signposts and the stations' paths. Returns report lines."""
    lines = []
    wf = Wayfinder(occ)
    entrance = (E.real_mouth[0], E.real_mouth[1] - 50)
    wf.run("maw", entrance)
    st = {s["id"]: (s["x"], s["z"]) for s in L["stations"]}
    for sid, p in st.items():
        wf.run(sid, p)
    h = E.hulk
    hangar = polar(h["heading"], -E.hulk_body * 0.4, polar(h["heading"], -(h["length"] - E.hulk_body) / 2, h["c"]))
    wf.run("den", hangar)
    # 1. reachable from the Maw
    checks = [("station " + k, p) for k, p in st.items()] + [("den " + d["name"].lower(), d["c"]) for d in E.dens]
    checks += [("the Docks", E.docks), ("the Market", E.market), ("the Den's hangar", hangar)]
    checks += [(f"berth {m['blueprint'][14:]} ({m['x']:.0f}, {m['z']:.0f})", (m["x"], m["z"])) for m in L["moored"]]
    for pts, w in E.tunnels:
        if pts[0] != pts[-1]:
            checks += [(f"tunnel end ({q[0]:.0f}, {q[1]:.0f})", q) for q in (pts[0], pts[-1]) if E.col_kind(*q)[0] >= 2]
    bad = [name for name, p in checks if not wf.reach("maw", p)]
    lines.append(f"reachable from the Maw (8 m grid, >= 8 m from rock / structure in y -8..+8): {len(checks) - len(bad)}/{len(checks)}"
                 + ("" if not bad else " UNREACHABLE: " + "; ".join(bad)))
    # 2. every side tunnel reaches where it leads: its far end is near its cavern by a path, not only back round the Ring
    dead, ratios = [], []
    for (pts, w), goal in zip(E.tunnels, E.tgoal):
        if goal is None or goal == "out":
            continue                                     # the Ring, the Maw's throat, the back door (it leaves the rock)
        end = pts[-1]
        key = "den" if goal == "den" else next(s["id"] for s in L["stations"] if E.col_kind(s["x"], s["z"])[1] == goal)
        target = st.get(key, hangar)
        c_ = wf.chain(key, end)
        ln = sum(math.dist(wf.at(a), wf.at(b)) for a, b in zip(c_, c_[1:])) if c_ else np.inf
        ratio = ln / max(math.dist(end, target), 1.0)
        ratios.append(f"{key} {ratio:.1f}")
        if not ratio < 2.0:
            dead.append(f"({pts[0][0]:.0f}, {pts[0][1]:.0f}) -> ({end[0]:.0f}, {end[1]:.0f}) to {key}: path {ratio:.1f}x the straight line")
    lines.append("side tunnels reaching their destination (path from the tunnel's end / straight line): " + ", ".join(ratios)
                 + ("; all connected" if not dead else "; DEAD ENDS " + "; ".join(dead)))
    # 3. the junctions and mouths where a ship chooses, and the Maw's entrance: the heading toward each station
    cands = [entrance, E.maw_t[-1]]
    for pts, w in E.tunnels:
        if pts[0] == pts[-1] or pts is E.maw_t:
            continue
        cands.append(pts[0])
        c, r = min(((cv[0], cv[1]) for cv in E.caverns if cv[3] is not None), key=lambda cv: math.dist(cv[0], pts[-1]) - cv[1])
        cands.append(next((q for q in pts if math.dist(q, c) < r), pts[-1]))
    for c in (E.market, E.docks):                       # where a cavern opens straight onto the Ring
        cands.append(min(E.ring, key=lambda q: math.dist(q, c)))
    wps = []
    for p in cands:
        if any(math.dist(p, (w_["x"], w_["z"])) < 40 for w_ in wps) or not wf.reach("maw", p):
            continue
        to = {}
        for sid in st:
            y = wf.heading(sid, p)
            if y is not None:
                to[sid] = y
        wps.append(dict(x=round(p[0], 1), z=round(p[1], 1), to=to))
    L["waypoints"] = wps
    L["paths"] = {sid: path_points(wf, occ, "maw", p) for sid, p in st.items() if wf.reach("maw", p)}
    lines.append(f"waypoints {len(wps)}; paths from the Maw: " + ", ".join(f"{k} {len(v)} pts" for k, v in L["paths"].items()))
    return lines


# ---------------------------------------------------------------- the floors (owner: "empty open spaces with nothing")
# A well-kept station built into the rock, not a cave (owner: "its in the future and this feels like an ugly dark cave"): tidy
# industry on the floors, lit panels and light bars on it, light strips along the walls' bases and round the stations and pads,
# cool white light with neon accents. Salvage mess only in the salvage den; rubble only in the rough side tunnels. Every piece of
# it below y = -10, so the flight band (-8..+8) stays as it was; none of it within 30 m of a route, in a station's circle or
# under a berth. It ships in the cavern's structure piece, or in the rock tile's low piece in the tunnels.
CRATE = (4, 6, 3, 19, 5, 2, 13, 1, 8)                # container and crate colours: contrast with the light grey rock
METAL = (1, 13, 11)                                  # tanks, machinery, frames
PANEL = (20, 20, 20, 49, 48)                         # lit panels and light bars: mostly cool white, some cyan and pink
COOL = ("#DCEBFF", "#DCEBFF", "#DCEBFF", "#BFF4FF", "#BFF4FF", "#22E6FF", "#FF2E88")
CONTAINERS = ((6, 2, 2), (4, 2, 2), (3, 1, 1), (2, 1, 1))     # cells: 12x4x4 m to 4x2x2 m
MIX = {
    "market": (("containers", 3), ("crates", 2), ("rack", 2), ("tanks", 1), ("machinery", 1.5), ("htank", 1), ("cable", 0.3)),
    "docks": (("containers", 3), ("tanks", 3), ("htank", 2), ("machinery", 2), ("rack", 1.5), ("crates", 1), ("cable", 0.4)),
    "den": (("containers", 2), ("crates", 2), ("rack", 2), ("machinery", 1.5), ("tanks", 1), ("girders", 0.5), ("cable", 0.3)),
    "den_salvage": (("wreck", 3), ("pile", 3), ("girders", 2), ("containers", 1), ("crates", 1)),
    "den_hulk": (("containers", 3), ("machinery", 2), ("rack", 2), ("tanks", 1.5), ("htank", 1), ("crates", 1), ("cable", 0.3)),
    "side": (("rubble", 3), ("boulder", 1.5), ("crates", 1.5), ("girders", 1), ("containers", 1), ("rack", 0.5)),
    "ring": (("crates", 3), ("rack", 1), ("cable", 0.3)),
}
DENSITY = {"market": 70, "docks": 60, "den": 45, "den_salvage": 35, "den_hulk": 55, "side": 130, "ring": 90}   # floor cells per cluster


class Furnisher:
    TOP = -6                                          # the highest cell clutter may fill: its top at y = -10

    def __init__(self, E, occ, L, budget, seed=31):
        self.E, self.occ, self.L, self.budget = E, occ, L, budget
        self.rng = random.Random(seed)
        W, H, J0 = occ.W, occ.H, occ.J0
        self.NJ = occ.low.shape[1]
        self.topi = self.TOP - J0
        X = (np.arange(W) + occ.gx0 + 0.5) * CELL; Z = (np.arange(H) + occ.gz0 + 0.5) * CELL
        self.X, self.Z = X, Z
        ci = np.floor((X - E.rx0) / CC).astype(int); ck = np.floor((Z - E.rz0) / CC).astype(int)
        vx = (ci >= 0) & (ci < E.kind.shape[0]); vz = (ck >= 0) & (ck < E.kind.shape[1])
        sub = np.ix_(ci[vx], ck[vz]); at = np.ix_(vx, vz)
        kind = np.zeros((W, H), np.int8); kind[at] = E.kind[sub]
        cav = np.full((W, H), -1, np.int16); cav[at] = E.cav[sub]
        fj = np.full((W, H), -6, np.int16); fj[at] = E.fj[sub]
        tun = np.full((W, H), -1, np.int16); tun[at] = np.argmin(np.stack(E.tdist), axis=0)[sub]
        # the Den's hangar: the warship's cavity, in the rock or out of it
        a, l = E.hulk_frame(X[:, None], Z[None, :])
        hk = E.hulk; bow = hk["length"] - E.hulk_body
        hw = np.where(a > 0, hk["width"] / 2 - (hk["width"] / 2 - 32) * np.clip(a / max(bow, 1), 0, 1), hk["width"] / 2)
        hangar = (np.abs(l) < hw - 4) & (a > -E.hulk_body) & (a < bow)
        self.inner = (kind >= 2) | hangar
        self.fmap = (np.where(hangar & (kind < 2), -12, 2 * fj) - J0).astype(int)
        fm = np.clip(self.fmap, 1, self.NJ - 1)
        I, K = np.meshgrid(np.arange(W), np.arange(H), indexing="ij")
        low = occ.low
        above = np.cumsum(low[:, ::-1, :], axis=1)[:, ::-1, :]          # occupied cells at or above each j
        self.floor_ok = self.inner & low[I, fm - 1, K] & (above[I, fm, K] == 0)
        # regions
        names = ["market", "docks"] + ["den_" + d["name"].lower() for d in E.dens] + ["den_hulk", "ring", "side"]
        self.names = names
        rid = np.full((W, H), -1, np.int16)
        rid[(kind == 3) & (cav == E.market_i)] = 0
        rid[(kind == 3) & (cav == E.docks_i)] = 1
        for n, d in enumerate(E.dens):
            rid[(kind == 3) & (cav == d["i"])] = 2 + n
        rid[hangar | ((kind == 3) & (cav == -2))] = names.index("den_hulk")
        ring_i = next(i for i, t in enumerate(E.tunnels) if t[0] is E.ring)
        rid[(kind == 2) & (tun == ring_i) & (rid < 0)] = names.index("ring")
        rid[(kind == 2) & (rid < 0)] = names.index("side")
        rid[~self.inner] = -1
        self.rid = rid
        self.wall_d = edt(~self.inner, 12) * CELL                       # m from the rock's walls
        # where nothing goes: within 30 m of a route, in a station's circle, under a berth
        excl = np.zeros((W, H), bool)
        for r in L["traffic"]:
            for p, q in zip(route_pts(r), route_pts(r)[1:]):
                self.mark_near_seg(excl, p, q, CLUTTER_LANE + 1.5)
        for s in L["stations"]:
            self.mark_near_seg(excl, (s["x"], s["z"]), (s["x"], s["z"]), STATION_R + 1.5)
        for m in L["moored"]:
            self.mark_near_seg(excl, (m["x"], m["z"]), (m["x"], m["z"]), ship_len(m["blueprint"]) / 2 + 4)
        self.excl = excl
        self.keep = self.floor_ok & ~excl & (rid >= 0)
        self.lab = np.full(low.shape, -1, np.int16)
        self.classes = []
        self.count = {}
        self.cells = 0
        self.lights, self.neon = [], []

    def mark_near_seg(self, mask, a, b, r):
        occ = self.occ
        x0, x1 = min(a[0], b[0]) - r, max(a[0], b[0]) + r; z0, z1 = min(a[1], b[1]) - r, max(a[1], b[1]) + r
        i0, i1 = max(int(x0 / CELL) - occ.gx0, 0), min(int(x1 / CELL) - occ.gx0 + 2, occ.W)
        k0, k1 = max(int(z0 / CELL) - occ.gz0, 0), min(int(z1 / CELL) - occ.gz0 + 2, occ.H)
        if i1 <= i0 or k1 <= k0:
            return
        X = self.X[i0:i1, None]; Z = self.Z[None, k0:k1]
        dx, dz = b[0] - a[0], b[1] - a[1]
        L2 = dx * dx + dz * dz
        t = np.clip(((X - a[0]) * dx + (Z - a[1]) * dz) / L2, 0, 1) if L2 > 0 else 0.0
        mask[i0:i1, k0:k1] |= np.hypot(X - (a[0] + dx * t), Z - (a[1] + dz * t)) < r

    def cls(self, c, m):
        if (c, m) not in self.classes:
            self.classes.append((c, m))
        return self.classes.index((c, m))

    # ------------------------------------------------------------ placing
    def place(self, boxes, kind, f, clip=False, min_cells=1):
        """Boxes (i0, i1, j0, j1, k0, k1, colour, material), grid cells, j in the floor band. Whole, or (clip) only the cells
        that are free, over this floor and allowed. Returns the cells placed."""
        occ, todo, n = self.occ, [], 0
        if self.cells > self.budget:
            return 0
        for i0, i1, j0, j1, k0, k1, c, m in boxes:
            a0, a1, b0, b1 = max(i0, 0), min(i1, occ.W), max(k0, 0), min(k1, occ.H)
            c0, c1 = max(j0, 0), min(j1, self.topi + 1)
            if (a0, a1, b0, b1, c0, c1) != (i0, i1, k0, k1, j0, j1):
                if not clip:
                    return 0
                if a1 <= a0 or b1 <= b0 or c1 <= c0:
                    continue
            ok = (~occ.low[a0:a1, c0:c1, b0:b1] & (self.lab[a0:a1, c0:c1, b0:b1] < 0)
                  & (self.keep[a0:a1, b0:b1] & (self.fmap[a0:a1, b0:b1] == f))[:, None, :])
            if not clip and not ok.all():
                return 0
            todo.append((a0, a1, c0, c1, b0, b1, ok, self.cls(c, m)))
            n += int(ok.sum())
        if n < min_cells:
            return 0
        for a0, a1, c0, c1, b0, b1, ok, ci in todo:
            self.lab[a0:a1, c0:c1, b0:b1][ok] = ci
        self.cells += n
        self.count[kind] = self.count.get(kind, 0) + 1
        return n

    def light(self, i, k, f, col, r=8.0, dy=3.0, prio=1):
        self.lights.append(dict(x=round(float(self.X[i]), 1), y=round((f + self.occ.J0) * CELL + dy, 1), z=round(float(self.Z[k]), 1),
                                col=col, r=r, kind="lamp", _p=prio))

    def thin_lights(self, cap):
        """At most `cap` lamps (they are real point lights): the stations', pads' and strips' first, the clutter's evenly."""
        first = [l for l in self.lights if l["_p"] == 0]
        rest = [l for l in self.lights if l["_p"] != 0]
        room = max(cap - len(first), 0)
        if len(rest) > room:
            rest = [rest[int(i * len(rest) / room)] for i in range(room)] if room else []
        self.lights = [{k: v for k, v in l.items() if k != "_p"} for l in first + rest]

    def strip(self, pts, y, col):
        pts = densify(pts, 16.0)                          # short segments, so a strip bends with its wall
        for a, b in zip(pts, pts[1:]):
            if math.dist(a, b) > 0.5:
                self.neon.append(dict(ax=round(a[0], 1), az=round(a[1], 1), bx=round(b[0], 1), bz=round(b[1], 1), y=round(y, 1), col=col))

    # ------------------------------------------------------------ the pieces of clutter (anchor i, k on floor f; ax: long axis)
    @staticmethod
    def R(i, k, ax, a0, a1, b0, b1, j0, j1, c, m):
        return (i + a0, i + a1, j0, j1, k + b0, k + b1, c, m) if ax == 0 else (i + b0, i + b1, j0, j1, k + a0, k + a1, c, m)

    def containers(self, i, k, f, ax):
        """A row of container stacks, one size, 1-3 high, doors at one end; light bars on some."""
        rng, R = self.rng, self.R
        lc, wc, hc = rng.choice(CONTAINERS)
        out, b = [], 0
        for _ in range(rng.randint(1, 4)):
            for lev in range(rng.choice((1, 1, 2, 2, 3)) if hc == 2 else rng.choice((1, 2))):
                j0 = f + lev * hc
                if j0 + hc - 1 > self.topi:
                    break
                c = rng.choice(CRATE)
                out.append(R(i, k, ax, 0, lc - 1, b, b + wc, j0, j0 + hc, c, 1))
                out.append(R(i, k, ax, lc - 1, lc, b, b + wc, j0, j0 + hc, 11 if c != 11 else 1, 1))
                if lc >= 4 and rng.random() < 0.5:                     # a light bar along its top edge
                    out[-2] = R(i, k, ax, 0, lc - 1, b, b + wc, j0, j0 + hc - 1, c, 1)
                    out.append(R(i, k, ax, 0, lc - 1, b, b + wc - 1, j0 + hc - 1, j0 + hc, c, 1))
                    out.append(R(i, k, ax, 0, lc - 1, b + wc - 1, b + wc, j0 + hc - 1, j0 + hc, rng.choice(PANEL), 2))
            b += wc + rng.choice((0, 0, 1))
        return out

    def crates(self, i, k, f, ax):
        rng, out = self.rng, []
        for _ in range(rng.randint(3, 7)):
            a, b = rng.randint(0, 4), rng.randint(0, 4)
            ln = rng.choice((1, 1, 2))
            c = rng.choice(CRATE)
            out.append(self.R(i, k, ax, a, a + ln, b, b + 1, f, f + 1, c, 1))
            if rng.random() < 0.35:
                out.append(self.R(i, k, ax, a, a + 1, b, b + 1, f + 1, f + 2, rng.choice(CRATE), 1))
        return out

    def rack(self, i, k, f, ax):
        """A cargo rack: frame posts and shelves, crates on them, a lit strip along the top."""
        rng, R = self.rng, self.R
        ln, h = rng.randint(4, 7), rng.randint(3, 5)
        fc = rng.choice(METAL)
        out = []
        for a in range(0, ln, 2):
            out.append(R(i, k, ax, a, a + 1, 0, 2, f, f + h, fc, 2))
        for a in range(ln):
            if a % 2:
                for lev in range(0, h - 1, 2):
                    if rng.random() < 0.8:
                        out.append(R(i, k, ax, a, a + 1, 0, 2, f + lev, f + lev + 1, rng.choice(CRATE), 1))
            out.append(R(i, k, ax, a, a + 1, 0, 2, f + h - 1, f + h, rng.choice(PANEL) if a % 2 else fc, 2))
        return out

    def tanks(self, i, k, f, ax):
        """A tank farm: 1-3 upright tanks in a row, a lit band round each one's top, a pipe between them."""
        rng = self.rng
        rc = rng.choice((1.5, 2.0, 2.5)); h = rng.randint(3, min(6, self.topi - f + 1))
        n = math.ceil(rc)
        col = rng.choice((1, 13, 4, 11))
        band = rng.choice((20, 20, 49, 22))
        out = []
        for t in range(rng.randint(1, 3)):
            ca = t * (2 * n + 1)
            for a in range(-n, n):
                for b in range(-n, n):
                    d = math.hypot(a + 0.5, b + 0.5)
                    if d > rc:
                        continue
                    out.append(self.R(i, k, ax, ca + a, ca + a + 1, b, b + 1, f, f + h - 1, col, 1))
                    out.append(self.R(i, k, ax, ca + a, ca + a + 1, b, b + 1, f + h - 1, f + h, band if d > rc - 1 else col, 2 if d > rc - 1 else 1))
            if t:
                out.append(self.R(i, k, ax, ca - n - 1, ca - n + 1, 0, 1, f + 1, f + 2, 13, 1))
        return out

    def htank(self, i, k, f, ax):
        """A horizontal gas tank on saddles, its end caps and a lit stripe."""
        rng = self.rng
        ln = rng.randint(5, 8)
        body = rng.choice((13, 4, 3, 1))
        out = []
        for b, u in ((1, 0), (0, 1), (1, 1), (2, 1), (1, 2)):
            out.append(self.R(i, k, ax, 1, ln - 1, b, b + 1, f + u, f + u + 1, body if (b, u) != (1, 2) else rng.choice(PANEL),
                              1 if (b, u) != (1, 2) else 2))
            out.append(self.R(i, k, ax, 0, 1, b, b + 1, f + u, f + u + 1, 13, 1))
            out.append(self.R(i, k, ax, ln - 1, ln, b, b + 1, f + u, f + u + 1, 13, 1))
        for a in (1, ln - 2):
            for b in (0, 2):
                out.append(self.R(i, k, ax, a, a + 1, b, b + 1, f, f + 1, 1, 1))
        return out

    def machinery(self, i, k, f, ax):
        """A machine: a base, a housing, a lit panel and an exhaust stack."""
        rng, R = self.rng, self.R
        la, lb = rng.choice(((3, 3), (4, 3), (4, 4)))
        out = [R(i, k, ax, 0, la, 0, lb, f, f + 2, rng.choice(METAL), 1),
               R(i, k, ax, 1, la - 1, 1, lb - 1, f + 2, f + 3, rng.choice((1, 13, 4)), 1),
               R(i, k, ax, 0, 1, 1, lb - 1, f + 1, f + 2, rng.choice(PANEL + (22,)), 2),
               R(i, k, ax, la - 1, la, lb - 1, lb, f + 2, f + 5, 16, 1)]
        out[0] = R(i, k, ax, 1, la, 0, lb, f, f + 2, out[0][6], 1)
        out.append(R(i, k, ax, 0, 1, 0, 1, f, f + 2, 1, 1)); out.append(R(i, k, ax, 0, 1, lb - 1, lb, f, f + 2, 1, 1))
        out.append(R(i, k, ax, 0, 1, 1, lb - 1, f, f + 1, 1, 1))
        return out

    def girders(self, i, k, f, ax):
        rng = self.rng
        out = []
        ln = rng.randint(6, 10)
        n = rng.randint(2, 3)
        for b in range(n):
            out.append(self.R(i, k, ax, 0, ln, b, b + 1, f, f + 1, rng.choice((1, 13, 6)), rng.choice((1, 2))))
        if rng.random() < 0.6:
            out.append(self.R(i, k, ax, 1, ln - 1, 0, n, f + 1, f + 2, rng.choice((1, 13)), 2))
        return out

    def cable(self, i, k, f, ax):
        rng = self.rng
        a1, b1 = rng.randint(6, 14), rng.randint(3, 8)
        return [self.R(i, k, ax, 0, a1, 0, 1, f, f + 1, 11, 1), self.R(i, k, ax, a1 - 1, a1, 1, b1, f, f + 1, 11, 1)]

    def pile(self, i, k, f, ax):
        """A salvage pile: a heap of scrap, hull plates and frames."""
        rng = self.rng
        R_, hm = rng.uniform(2.0, 4.0), rng.randint(2, 4)
        out = []
        n = math.ceil(R_)
        for a in range(-n, n + 1):
            for b in range(-n, n + 1):
                d = math.hypot(a, b)
                if d > R_:
                    continue
                h = int(round(hm * (1 - d / R_) + rng.uniform(-0.6, 0.8)))
                for u in range(max(h, 0)):
                    out.append(self.R(i, k, ax, a, a + 1, b, b + 1, f + u, f + u + 1, rng.choice((11, 10, 14, 13, 18, 1, 6)),
                                      2 if rng.random() < 0.15 else 1))
        return out

    def wreck(self, i, k, f, ax):
        """A wrecked hull section: half a hull's shell lying on the floor, ribs inside, holes and a torn end."""
        rng = self.rng
        ln, rc = rng.randint(5, 9), rng.choice((3.0, 3.5, 4.0))
        col = rng.choice((18, 10, 11, 13))
        n = math.ceil(rc)
        out = []
        for b in range(-n, n):
            for u in range(n):
                d = math.hypot(b + 0.5, u + 0.5)
                end = ln - rng.randint(0, 2)
                for a in range(end):
                    if rc - 1.2 <= d <= rc and rng.random() > 0.12:
                        out.append(self.R(i, k, ax, a, a + 1, b, b + 1, f + u, f + u + 1, 10 if rng.random() < 0.25 else col, 1))
                    elif a % 3 == 1 and rc - 2.2 <= d < rc - 1.2:
                        out.append(self.R(i, k, ax, a, a + 1, b, b + 1, f + u, f + u + 1, 1, 2))
        return out

    def rubble(self, i, k, f, ax, R_=None):
        """Fallen rock against a wall (or a boulder on the floor): a lumpy heap on the 4 m lattice, rock tones."""
        rng = self.rng
        R_ = R_ or rng.uniform(5.0, 11.0)
        V = 0.6 * R_
        n = int(math.ceil(R_ * 1.2 / CC))
        out = []
        seed = rng.uniform(0, 50)
        i, k = i - (i + self.occ.gx0) % 2, k - (k + self.occ.gz0) % 2       # on the rock's 4 m lattice
        for a in range(-n, n):
            for b in range(-n, n):
                for u in range(int(math.ceil(V * 1.2 / CC))):
                    x, y, z = (a + 0.5) * CC, (u + 0.5) * CC - 0.25 * V, (b + 0.5) * CC
                    if self.E.lumpy(np.array(x), np.array(y), np.array(z), R_, V, seed):
                        out.append((i + 2 * a, i + 2 * a + 2, f + 2 * u, f + 2 * u + 2, k + 2 * b, k + 2 * b + 2, 40 + rng.randint(0, 3), 0))
        return out

    def scaffold(self, i, k, f, ax, side):
        """Scaffolding against a wall: frame posts and rails, a deck at half height, lit panels on the rail."""
        rng, R = self.rng, self.R
        ln, h = rng.randint(4, 8), rng.randint(4, min(6, self.topi - f + 1))
        fc = rng.choice((1, 13))
        b0, b1 = (0, 2) if side > 0 else (-1, 1)
        out = []
        for a in range(ln):
            if a % 2 == 0:
                out.append(R(i, k, ax, a, a + 1, b0, b1, f, f + h - 1, fc, 2))
            else:
                out.append(R(i, k, ax, a, a + 1, b0, b1, f + h // 2, f + h // 2 + 1, 13, 1))
            out.append(R(i, k, ax, a, a + 1, b0, b1, f + h - 1, f + h, rng.choice(PANEL) if a % 3 == 1 else fc, 2))
        return out

    def pad(self, i, k, f):
        """A landing pad: a 14 m floor plate, its edge and centre cross lit."""
        out = []
        for a in range(7):
            for b in range(7):
                edge = a in (0, 6) or b in (0, 6)
                cross = (a == 3 or b == 3) and 1 <= a <= 5 and 1 <= b <= 5
                out.append((i + a, i + a + 1, f, f + 1, k + b, k + b + 1, 20 if edge else (49 if cross else 1), 2 if edge or cross else 1))
        return out

    # ------------------------------------------------------------ signs (owner: the text was floating, huge, one mirrored)
    def place_signs(self):
        """Every sign flat on the floor (0.5 m over it), letters 4-6 m (THE MAW 8), turned to the heading of the main approach to
        it (the flight camera turns with the ship) and laid on that approach just before its station: outside the station's
        circle, never under a berth."""
        E, L, occ = self.E, self.L, self.occ
        st = {s["id"]: (s["x"], s["z"]) for s in L["stations"]}
        routes = {r["name"]: route_pts(r) for r in L["traffic"]}
        hd = lambda a, b: math.degrees(math.atan2(b[0] - a[0], b[1] - a[1])) % 360

        def route_mouth(name, c, r):                    # where a route first enters a cavern
            return next((p for p in densify(routes[name], 4.0) if math.dist(p, c) < r - 4), c)

        def tunnel_mouth(c, r):                         # where the side tunnel into a cavern opens into it
            ends = [pts for pts, w in E.tunnels if pts[0] != pts[-1] and math.dist(pts[-1], c) < r + 45]
            if not ends:
                return None
            pts = min(ends, key=lambda t: math.dist(t[-1], c))
            return next((q for q in pts if math.dist(q, c) < r), pts[-1])

        a, b = E.maw_t[1], E.maw_t[2]
        throat = lambda z: (a[0] + (b[0] - a[0]) * (z - a[1]) / (b[1] - a[1]), z)
        plan = {"THE MAW": (None, hd(a, b), throat(E.real_mouth[1] + 12)),
                "BLAST DOORS": (None, hd(a, b), throat(E.real_mouth[1] + 36))}
        m = route_mouth("maw_to_market", E.market, E.market_r)
        plan["THE MARKET"] = (st["market"], hd(m, st["market"]), None)
        m = route_mouth("maw_to_docks", E.docks, E.docks_r)
        plan["THE DOCKS"] = (st["docks"], hd(m, st["docks"]), None)
        for d in E.dens:
            s = st["den_" + d["name"].lower()]
            m = tunnel_mouth(d["c"], d["r"]) or d["c"]
            plan[d["name"]] = (s, hd(m, s), None)
        h = E.hulk
        bc = polar(h["heading"], -(h["length"] - E.hulk_body) / 2, h["c"])
        plan["THE DEN"] = (None, h["heading"] % 360, polar(h["heading"], -E.hulk_body * 0.25, bc))   # in from the stern
        self.signmask = np.zeros(self.keep.shape, bool)
        out = []
        for sg in L["signs"]:
            size = {"THE MAW": 8.0, "BLAST DOORS": 4.0, "THE MARKET": 6.0, "THE DOCKS": 6.0, "THE DEN": 6.0}.get(sg["text"], 5.0)
            w = (len(sg["text"]) * 6 - 1) / 7 * size
            stn, heading, at = plan[sg["text"]]
            u = (math.sin(math.radians(heading)), math.cos(math.radians(heading)))
            if stn is not None:                          # on the approach, just before the station's circle (else just past it)
                back = STATION_R + size / 2 + 6
                cands = [(stn[0] - u[0] * (back + t) + u[1] * s_, stn[1] - u[1] * (back + t) - u[0] * s_)
                         for t in range(0, 41, 4) for s_ in (0, 8, -8, 16, -16, 24, -24)]
                cands += [(stn[0] + u[0] * (back + t) + u[1] * s_, stn[1] + u[1] * (back + t) - u[0] * s_)
                          for t in range(0, 41, 4) for s_ in (0, 8, -8, 16, -16)]
            else:
                cands = [(at[0] + u[0] * t + u[1] * s_, at[1] + u[1] * t - u[0] * s_)
                         for t in (0, 4, -4, 8, -8, 12, -12, 16, -16, 24, -24, 32, 40, 48) for s_ in (0, 6, -6, 12, -12)]
            spot = None
            for p in cands:
                cells = self.sign_cells(p, u, w, size, 1.0)
                if cells is not None:
                    spot = p; break
            if spot is None:
                E.log.append(f"sign {sg['text']}: no clear floor near its station, left at ({sg['x']}, {sg['z']})")
                out.append(sg); continue
            i, k = occ.cell(*spot)
            f = int(self.fmap[i, k])
            ii, kk = self.sign_cells(spot, u, w, size, 3.0, check=False)
            self.signmask[ii, kk] = True
            out.append(dict(text=sg["text"], x=round(spot[0], 1), y=round((f + occ.J0) * CELL + 0.5, 1), z=round(spot[1], 1),
                            yaw=round(math.radians(heading), 4), col=sg["col"], size=size))
        L["signs"] = out
        self.excl |= self.signmask
        self.keep &= ~self.signmask

    def sign_cells(self, p, u, w, h, margin, check=True):
        """The floor cells under a sign's rectangle (w along its lines, h along u); None if any is not clear floor."""
        occ = self.occ
        r = math.hypot(w, h) / 2 + margin + 2
        i0, k0 = occ.cell(p[0] - r, p[1] - r); i1, k1 = occ.cell(p[0] + r, p[1] + r)
        I, K = np.meshgrid(np.arange(i0, i1 + 1), np.arange(k0, k1 + 1), indexing="ij")
        dx, dz = self.X[I] - p[0], self.Z[K] - p[1]
        along, across = dx * u[0] + dz * u[1], dx * u[1] - dz * u[0]
        m = (np.abs(along) <= h / 2 + margin) & (np.abs(across) <= w / 2 + margin)
        I, K = I[m], K[m]
        if not check:
            return I, K
        ci, ck = occ.cell(*p)
        f = self.fmap[ci, ck]
        if not (self.floor_ok[I, K] & (self.fmap[I, K] == f) & ~self.signmask[I, K]).all():
            return None
        X, Z = self.X[I], self.Z[K]
        for s in self.L["stations"]:
            if (np.hypot(X - s["x"], Z - s["z"]) < STATION_R + 1.5).any():
                return None
        for mo in self.L["moored"]:
            if (np.hypot(X - mo["x"], Z - mo["z"]) < ship_len(mo["blueprint"]) / 2 + 2).any():
                return None
        return I, K

    # ------------------------------------------------------------ the floors
    def furnish(self):
        occ, rng, E, L = self.occ, self.rng, self.E, self.L
        stations = [(s["x"], s["z"]) for s in L["stations"]]
        par = ((np.arange(occ.W) + occ.gx0) % 2 == 0)[:, None] & ((np.arange(occ.H) + occ.gz0) % 2 == 0)[None, :]
        wallside = self.keep & (self.wall_d < 2.5)
        # 1. pipes along the walls' feet (everywhere), with brackets and warning lights; light strips above them
        self.wall_pipes()
        # 2. landing pads by the stations
        for s in L["stations"]:
            for r in range(30, 64, 4):
                done = False
                for b in range(0, 360, 20):
                    p = polar(b, r, (s["x"], s["z"]))
                    i, k = occ.cell(*p)
                    i, k = int(i) - 3, int(k) - 3
                    f = int(self.fmap[i + 3, k + 3])
                    if self.place(self.pad(i, k, f), "landing pad", f):
                        y = (f + occ.J0) * CELL + 2.6
                        x0, z0 = (i + occ.gx0) * CELL - 1, (k + occ.gz0) * CELL - 1
                        self.strip([(x0, z0), (x0 + 16, z0), (x0 + 16, z0 + 16), (x0, z0 + 16), (x0, z0)], y, "#22E6FF")
                        for c in ((i, k), (i + 6, k + 6)):
                            self.light(c[0], c[1], f, "#DCEBFF", 8.0, 4.0, prio=0)
                        done = True; break
                if done:
                    break
        # 3. clusters, region by region: denser near the stations and in the dens, sparse along the highway
        for code, reg in enumerate(self.names):
            mix = MIX.get(reg, MIX["den"] if reg.startswith("den_") else None)
            m = self.keep & (self.rid == code)
            cand = np.argwhere(m & par)
            if not len(cand):
                continue
            dens_key = reg if reg in DENSITY else "den"
            target = max(4, int(m.sum() / DENSITY[dens_key]))
            px, pz = self.X[cand[:, 0]], self.Z[cand[:, 1]]
            dst = np.min([np.hypot(px - x, pz - z) for x, z in stations], axis=0)
            w = 1 + 4 * np.exp(-dst / 45.0)
            w = w / w.sum()
            kinds, wts = zip(*mix)
            nprng = np.random.default_rng(code + 7)
            placed = 0
            for _ in range(target * 10):
                if placed >= target:
                    break
                i, k = cand[nprng.choice(len(cand), p=w)]
                kind = rng.choices(kinds, wts)[0]
                f = int(self.fmap[i, k])
                ax = rng.randint(0, 1)
                if kind in ("rubble", "boulder"):
                    if kind == "rubble":
                        near = np.argwhere(wallside[max(i - 8, 0):i + 9, max(k - 8, 0):k + 9])
                        if not len(near):
                            continue
                        ii, kk = near[rng.randrange(len(near))]
                        i, k = max(i - 8, 0) + ii, max(k - 8, 0) + kk
                    n = self.place(self.rubble(i, k, f, ax, None if kind == "rubble" else rng.uniform(4.0, 7.0)), kind, f, clip=True, min_cells=16)
                elif kind in ("pile", "wreck", "cable"):
                    n = self.place(getattr(self, kind)(i, k, f, ax), kind, f, clip=True, min_cells=6)
                else:
                    n = self.place(getattr(self, kind)(i, k, f, ax), kind, f)
                if n:
                    placed += 1
                    if rng.random() < (0.5 if reg in ("ring", "side") else 0.8):
                        self.light_near(i, k, f, reg)
                    for _ in range(rng.randint(0, 1 if reg in ("ring", "side") else 3)):     # its neighbours: a yard, not a scatter
                        sk = rng.choices(kinds, wts)[0]
                        if sk in ("rubble", "boulder", "pile", "wreck"):
                            continue
                        a_, b_ = i + rng.randint(-9, 9), k + rng.randint(-9, 9)
                        a_, b_ = a_ - (a_ + occ.gx0) % 2, b_ - (b_ + occ.gz0) % 2
                        if 0 <= a_ < occ.W and 0 <= b_ < occ.H and self.keep[a_, b_] and self.fmap[a_, b_] == f:
                            self.place(getattr(self, sk)(a_, b_, f, ax if rng.random() < 0.7 else 1 - ax), sk, f,
                                       clip=sk == "cable", min_cells=4)
            # scaffolding against the walls of the caverns
            if reg not in ("ring", "side"):
                ws = np.argwhere(wallside & (self.rid == code) & par)
                for _ in range(min(len(ws), 40)):
                    if self.count.get("scaffold " + reg, 0) >= max(2, target // 6):
                        break
                    i, k = ws[rng.randrange(len(ws))]
                    f = int(self.fmap[i, k])
                    # the wall's side: along the axis the wall distance changes least
                    gx = self.wall_d[min(i + 1, occ.W - 1), k] - self.wall_d[max(i - 1, 0), k]
                    gz = self.wall_d[i, min(k + 1, occ.H - 1)] - self.wall_d[i, max(k - 1, 0)]
                    ax = 1 if abs(gx) > abs(gz) else 0
                    side = 1 if (gx if ax == 1 else gz) > 0 else -1
                    if self.place(self.scaffold(int(i), int(k), f, ax, side), "scaffold", f):
                        self.count["scaffold " + reg] = self.count.get("scaffold " + reg, 0) + 1
                        self.light(int(i), int(k), f, rng.choice(COOL[:5]), 8.0, 6.0)
        # 4. light rings on the floor under the stations' circles
        for s in L["stations"]:
            i, k = occ.cell(s["x"], s["z"])
            f = int(self.fmap[i, k]) if self.inner[i, k] else -12 - occ.J0
            y = (f + occ.J0) * CELL + 1.0
            ring = [polar(b, STATION_R + 2, (s["x"], s["z"])) for b in range(0, 361, 15)]
            self.strip(ring, y, "#22E6FF" if s["kind"] != "reset" else "#FF2E88")
            for b in range(0, 360, 90):
                q = polar(b + 45, STATION_R + 3, (s["x"], s["z"]))
                self.lights.append(dict(x=round(q[0], 1), y=round(y + 2, 1), z=round(q[1], 1), col="#DCEBFF", r=9.0, kind="lamp", _p=0))
        for s in [k for k in self.count if k.startswith("scaffold ")]:
            del self.count[s]

    def solid_at(self, x, y, z):
        """Whether the cell at (x, y, z) in the floor band holds rock, a structure or clutter."""
        occ = self.occ
        i, k = occ.cell(x, z)
        j = int(math.floor(y / CELL)) - occ.J0
        return 0 <= j < self.NJ and (occ.low[i, j, k] or self.lab[i, j, k] >= 0)

    def clean_lights(self):
        """No light strip passes through rock, a structure or the clutter (each is cut there, sampled every metre), and no lamp
        sits inside any of them."""
        out = []
        for s_ in self.neon:
            a, b, y = (s_["ax"], s_["az"]), (s_["bx"], s_["bz"]), s_["y"]
            n = max(2, int(math.dist(a, b)) + 1)
            run = []
            for t in list(np.linspace(0, 1, n)) + [None]:
                q = None if t is None else (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)
                if q is not None and not self.solid_at(q[0], y, q[1]):
                    run.append(q); continue
                if len(run) >= 2 and math.dist(run[0], run[-1]) >= 2.0:
                    out.append(dict(s_, ax=round(run[0][0], 1), az=round(run[0][1], 1), bx=round(run[-1][0], 1), bz=round(run[-1][1], 1)))
                run = []
        self.neon_cut = len(out) - len(self.neon)
        self.neon = out
        self.lights = [l for l in self.lights if not self.solid_at(l["x"], l["y"], l["z"])]

    def light_near(self, i, k, f, reg):
        """A floor lamp beside a piece of clutter: a free cell next to it."""
        occ, rng = self.occ, self.rng
        for _ in range(12):
            a, b = i + rng.randint(-4, 6), k + rng.randint(-4, 6)
            if 0 <= a < occ.W and 0 <= b < occ.H and self.keep[a, b] and self.lab[a, f, b] < 0 and not occ.low[a, f, b]:
                col = rng.choice(COOL) if reg not in ("docks", "den_salvage") or rng.random() < 0.6 else "#FFB347"
                self.light(a, b, f, col, 8.0, 3.0)
                return

    def wall_pipes(self):
        """Pipes along the walls' feet, a second run above in places, brackets, warning lamps; light strips along the walls."""
        occ = self.occ
        foot = self.keep & (self.wall_d < 2.5)
        I, K = np.nonzero(foot)
        gx, gz = (I + occ.gx0) * CELL, (K + occ.gz0) * CELL
        on = (np.sin(gx / 37.0) * np.cos(gz / 29.0) + 0.5 * np.sin((gx + gz) / 13.0)) > -0.45
        upper = (np.sin(gx / 23.0 + 1.3) * np.cos(gz / 31.0 - 0.4)) > 0.05
        n = 0
        for i, k, o, u, x, z in zip(I, K, on, upper, gx, gz):
            if not o:
                continue
            f = int(self.fmap[i, k])
            run = 13 if (int(x / 40) + int(z / 40)) % 3 else 16
            boxes = [(i, i + 1, f, f + 1, k, k + 1, run, 1)]
            if u:
                boxes.append((i, i + 1, f + 2, f + 3, k, k + 1, 1, 1))
                bracket = (int(x / CELL) + int(z / CELL)) % 5 == 0
                lamp = (int(x / CELL) * 7 + int(z / CELL) * 3) % 23 == 0
                if bracket or lamp:
                    boxes.append((i, i + 1, f + 1, f + 2, k, k + 1, 22 if lamp else 1, 2))
            n += self.place(boxes, "pipe cell", f, clip=True) > 0
        self.pipe_m = n * CELL
        del self.count["pipe cell"]
        # light strips: a cool white line along every wall's foot, 2 m up, where the floor runs along the wall
        lines = 0
        for reg_code, reg in enumerate(self.names):
            m = self.floor_ok & (self.rid == reg_code) & (self.wall_d < 2.5) & (self.wall_d > 0)
            col = {"market": "#FF2E88", "docks": "#22E6FF", "den_smuggler": "#FF2E88"}.get(reg, "#DCEBFF")
            pts = np.argwhere(m)
            # chain the wall's foot cells into runs, nearest neighbour first
            left = {(int(a), int(b)) for a, b in pts}
            while left:
                c = left.pop()
                run = [c]
                for end in (0, 1):
                    while True:
                        cur = run[-1] if end == 0 else run[0]
                        nxt = next(((cur[0] + da, cur[1] + db) for da, db in ((1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1))
                                    if (cur[0] + da, cur[1] + db) in left), None)
                        if nxt is None:
                            break
                        left.discard(nxt)
                        if end == 0:
                            run.append(nxt)
                        else:
                            run.insert(0, nxt)
                if len(run) < 6:
                    continue
                f = int(self.fmap[run[0]])
                P = [((a + occ.gx0 + 0.5) * CELL, (b + occ.gz0 + 0.5) * CELL) for a, b in run]
                P = rdp(P, 1.0)
                self.strip(P, (f + occ.J0) * CELL + 6.5, col)       # on the wall, over the pipes
                lines += 1
                for a, b in run[::40]:                      # a lamp every 80 m along the strip
                    self.light(a, b, f, col, 9.0, 5.0, prio=0)
        self.strip_runs = lines

    def emit(self, pieces):
        """The clutter's cells packed into blocks, into the cavern's structure piece or the rock tile's low piece."""
        occ, E = self.occ, self.E
        I, J, K = np.nonzero(self.lab >= 0)
        if not len(I):
            return {}
        gi, gk = I + occ.gx0, K + occ.gz0
        reg = self.rid[I, K]
        tunnel = (reg == self.names.index("ring")) | (reg == self.names.index("side")) | (reg < 0)
        ti, tk = (gi - E.ri0) // TILE, (gk - E.rk0) // TILE
        code = np.where(tunnel, 1000 + ti * 100 + tk, reg)
        byid = {p["id"]: p for p in pieces}
        added = {}
        for cd in np.unique(code):
            sel = code == cd
            i, j, k = I[sel], J[sel], K[sel]
            o = (i.min() - (i.min() + occ.gx0) % 2, j.min() - (j.min() + occ.J0) % 2, k.min() - (k.min() + occ.gz0) % 2)
            lab = np.full((i.max() - o[0] + 1, j.max() - o[1] + 1, k.max() - o[2] + 1), -1, np.int16)
            lab[i - o[0], j - o[1], k - o[2]] = self.lab[i, j, k]
            covered = np.zeros(lab.shape, bool)
            out = []
            for ci in np.unique(lab[lab >= 0]):
                al = lab == ci
                b = pack(al, al, covered, schedule=COARSE_SCHEDULE if self.classes[ci][1] == 0 else SCHEDULE)
                c, m = self.classes[ci]
                out.append(np.column_stack([b[:, 0] + o[0] + occ.gx0, b[:, 1] + o[1] + occ.J0, b[:, 2] + o[2] + occ.gz0,
                                            b[:, 3], np.full(len(b), c), np.full(len(b), m)]))
            B = np.concatenate(out).astype(np.int64)
            if cd >= 1000:
                t_i, t_k = (cd - 1000) // 100, (cd - 1000) % 100
                pid = f"ship_hollow_rock_low_{t_i}_{t_k}"
                B[:, 0] -= E.ri0 + t_i * TILE; B[:, 2] -= E.rk0 + t_k * TILE
                if pid not in byid:
                    byid[pid] = dict(id=pid, name=f"Hollow Rock Low {t_i} {t_k}", layer="low", origin=[E.rx0 + t_i * TILE * CELL, 0.0,
                                     E.rz0 + t_k * TILE * CELL], motion="static", fort=-1, blocks=np.zeros((0, 6), np.int64))
                    pieces.append(byid[pid])
            else:
                g = self.names[cd]
                pid = "ship_hollow_" + g
                if pid not in byid:
                    byid[pid] = dict(id=pid, name="Hollow " + " ".join(w.title() for w in g.split("_")), layer="struct",
                                     origin=[0.0, 0.0, 0.0], motion="static", fort=-1, blocks=np.zeros((0, 6), np.int64))
                    pieces.append(byid[pid])
            byid[pid]["blocks"] = np.concatenate([byid[pid]["blocks"], B])
            added[pid] = added.get(pid, 0) + len(B)
        return added


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
    E.naturalise()
    E.classify()
    E.reanchor_maw()
    E.place_guns()
    E.rock_volume()
    E.build_structures()
    E.assemble()
    rock = E.pack_rock()
    sp = E.pack_structures()
    struct = []
    for p in sp:
        g = p["group"]
        nm = "Hollow " + " ".join(w.title() for w in g.split("_"))
        hid = "_high" if p["layer"] == "high" else ""
        struct.append(dict(id="ship_hollow_" + g + hid, name=nm + (" (high)" if hid else ""), layer=p["layer"], origin=[0.0, 0.0, 0.0],
                           motion="static", fort=-1, blocks=p["blocks"]))
    forts = E.build_forts()
    pieces = rock + struct + forts                    # the structures still in global cells (origin 0), re-based below
    L = E.lists()

    # ---- berths and routes, against the flight band of what was built
    occ = Occupancy(pieces, L["guns"])
    obs0 = route_obstacles(L)
    before = {r["name"]: route_clearance(occ, r, obs0) for r in L["traffic"]}
    E.berth_moored(L, occ)
    obs = route_obstacles(L)
    for r in L["traffic"]:
        if route_clearance(occ, r, obs)[0] < ROUTE_CLEAR + 0.5:
            r["route"] = [list(p) for p in nudge(occ, route_pts(r), r["loop"], obs)]
    E.recheck_berths(L, occ)
    obs = route_obstacles(L)
    after = {r["name"]: route_clearance(occ, r, obs) for r in L["traffic"]}
    way_lines = wayfind(E, occ, L)

    # ---- the floors: signs, then the clutter and its lights (all below y = -10: the band and the checks above stand)
    budget = 2_480_000 - sum(int((p["blocks"][:, 3].astype(np.int64) ** 3).sum()) for p in pieces)
    F = Furnisher(E, occ, L, budget)
    F.place_signs()
    F.furnish()
    F.clean_lights()
    F.thin_lights(600 - len(L["lights"]))
    added = F.emit(pieces)
    L["lights"] += F.lights
    L["neon"] += F.neon

    # re-base each structure piece on its own min corner (x, z); heights stay absolute (origin y = 0)
    for p in pieces:
        if p["motion"] == "static" and p["origin"] == [0.0, 0.0, 0.0]:
            B = p["blocks"]
            mi, mk = int(B[:, 0].min()), int(B[:, 2].min())
            B[:, 0] -= mi; B[:, 2] -= mk
            p["origin"] = [mi * CELL, 0.0, mk * CELL]
    pieces = split_big(pieces)
    for p in pieces:
        piece_check(p)

    # ---- the final pieces on one grid: the cutaway mask, the checks, the images
    fin = Occupancy(pieces, L["guns"], field=False)
    allB, gx0, gz0, gx1, gz1, W, H, high = fin.allB, fin.gx0, fin.gz0, fin.gx1, fin.gz1, fin.W, fin.H, fin.high
    band_changed = int((fin.band != occ.band).sum())       # the floor clutter never reaches the flight band: 0
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
    final = {r["name"]: route_clearance(fin, r, obs) for r in L["traffic"]}
    # stations in open air at y = 0
    st_report = []
    for s in L["stations"]:
        k = E.col_kind(s["x"], s["z"])[0]
        assert k != 1, f"station {s['id']} is inside the rock"
        st_report.append((s["id"], round(occ.clear((s["x"], s["z"])), 1)))
    # guns: a ledge under each (-8..-2) and its footprint clear from -2 to +12, of every piece
    gocc = {}
    for p, B, oi, ok_ in allB:
        for sz in (1, 2, 3, 4):
            Bs = B[B[:, 3] == sz]
            for a in range(sz):
                for b_ in range(sz):
                    for c in range(sz):
                        for t in zip(Bs[:, 0] + oi + a, Bs[:, 1] + b_, Bs[:, 2] + ok_ + c):
                            if -4 <= t[1] < 6:
                                gocc.setdefault(p["motion"] if p["motion"] == "static" else p["id"], set()).add(t)
    gun_bad = []
    for gi_, g in enumerate(L["guns"]):
        key = "static" if g["fort"] < 0 else f"ship_hollow_fort_{g['fort']}"
        o = gocc.get(key, set())
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
    rock_cells = sum(cells(p["blocks"][p["blocks"][:, 5] == 0]) for p in rockp)       # the stone (the floor clutter in a tile aside)
    rock_big = sum(cells(p["blocks"][(p["blocks"][:, 3] >= 2) & (p["blocks"][:, 5] == 0)]) for p in rockp)
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
    lines.append(f"traffic clearance (sampled every 5 m; >= {ROUTE_CLEAR:.0f} m from rock / structure / gun cells in y -8..+8, "
                 "gunship posts, moored hulls, boulders), as laid -> after the nudges:")
    for r in L["traffic"]:
        b0, b1 = before[r["name"]], final[r["name"]]
        lines.append(f"  {r['name']:22s} {len(r['route']):3d} points, {b0[0]:5.1f} m ({b0[2]}) -> {b1[0]:5.1f} m ({b1[2]})"
                     + ("" if b1[0] >= ROUTE_CLEAR else f" SHORT at ({b1[1][0]:.0f}, {b1[1][1]:.0f}): {fin.blame(b1[1])}"))
    bt = E.berths
    lines.append(f"moored: {bt['design']} in the design, {bt['kept']} kept in place, {bt['moved']} moved, {bt['dropped']} dropped: "
                 f"{len(L['moored'])} berths")
    lines.append(f"floor clutter: {F.cells} cells; " + ", ".join(f"{k} {v}" for k, v in sorted(F.count.items()))
                 + f"; pipes along the walls {F.pipe_m:.0f} m (cells x 2 m); light strips {F.strip_runs}; lights +{len(F.lights)}, "
                 f"neon +{len(F.neon)}; band cells changed {band_changed}")
    lines.append("  clutter blocks per piece: " + ", ".join(f"{k[12:]} {v}" for k, v in sorted(added.items())))
    big = [(p["id"], len(p["blocks"])) for p in pieces if len(p["blocks"]) > 25000]
    lines.append("pieces over 25,000 blocks: " + (str(big) if big else "none"))
    lines.append(f"natural tunnels: {len(E.tunnels)} tunnels meandering (the Maw's throat straight), {E.pockets} alcoves and side "
                 f"pockets, {len(E.pillars)} pillars and rock spurs")
    lines += way_lines
    lines.append("signs: " + "; ".join(f"{s['text']} ({s['x']:.0f}, {s['y']:.1f}, {s['z']:.0f}) yaw {math.degrees(s['yaw']):.0f} size {s['size']:.0f}"
                                        for s in L["signs"]))
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
               moored=[{k: v for k, v in m.items() if k != "cls" and not k.startswith("_")} for m in L["moored"]],
               traffic=[{k: v for k, v in r.items() if k != "name"} for r in L["traffic"]], gunships=L["gunships"],
               inside=L["inside"], beacons=L["beacons"], boulders=L["boulders"], waypoints=L["waypoints"], paths=L["paths"],
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
                ("guns", "stations", "traffic", "moored", "signs", "gunships", "spawn", "forts", "waypoints", "paths")}, summary=lines,
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
        for m in L["moored"]:                                     # each berth its ship's size: length x a third of it
            ln = {"ship_nocturne_wraith": 20.0, "ship_nocturne_vesper": 45.0}.get(m["blueprint"], 50.0)
            u = (math.sin(m["yaw"]), math.cos(m["yaw"])); v = (u[1], -u[0])
            d.polygon([P(m["x"] + u[0] * a * ln / 2 + v[0] * b * ln / 6, m["z"] + u[1] * a * ln / 2 + v[1] * b * ln / 6)
                       for a, b in ((1, 1), (1, -1), (-1, -1), (-1, 1))], outline=(255, 226, 120, 255))
        for g in L["guns"]:
            q = P(g["x"], g["z"]); r = 6 if g["big"] else 4
            d.ellipse([q[0] - r, q[1] - r, q[0] + r, q[1] + r], fill=(255, 60, 40, 255), outline=(0, 0, 0, 255))
            e = (q[0] + math.sin(g["yaw"]) * 14, q[1] - math.cos(g["yaw"]) * 14)
            d.line([q, e], fill=(255, 60, 40, 255), width=2)
        for s in L["stations"]:
            q = P(s["x"], s["z"]); d.ellipse([q[0] - 9, q[1] - 9, q[0] + 9, q[1] + 9], outline=(124, 255, 154, 255), width=2)   # 18 m
            d.text((q[0] + 8, q[1] - 8), s["title"], font=fnt, fill=(124, 255, 154, 255))
        for s in L["signs"]:
            q = P(s["x"], s["z"]); d.text((q[0], q[1] + 6), s["text"], font=fnt, fill=(255, 255, 255, 200), anchor="mt")
            d.line([q, (q[0] + math.sin(s["yaw"]) * 10, q[1] - math.cos(s["yaw"]) * 10)], fill=(255, 255, 255, 220), width=2)
        for g in L["gunships"]:
            q = P(g["x"], g["z"]); r = g["radius"] / CELL
            d.ellipse([q[0] - r, q[1] - r, q[0] + r, q[1] + r], outline=(255, 122, 47, 160), width=1)
        if panel == 1:                                            # the stations' paths from the Maw, the signposted junctions
            for pts in L.get("paths", {}).values():
                d.line([P(*p) for p in pts], fill=(255, 255, 255, 90), width=1)
            for w_ in L.get("waypoints", []):
                q = P(w_["x"], w_["z"]); d.rectangle([q[0] - 4, q[1] - 4, q[0] + 4, q[1] + 4], outline=(255, 255, 255, 255), width=1)
        sp = L["spawn"]; q = P(sp["x"], sp["z"])
        d.ellipse([q[0] - 10, q[1] - 10, q[0] + 10, q[1] + 10], outline=(255, 255, 255, 255), width=2)
        d.text((10 + dx_, 10), ("THE HOLLOW, all pieces" if panel == 0 else "CUTAWAY: the high pieces (y >= +24) hidden") +
               ": top view, 2 m/px, north up", font=fnt, fill=(255, 255, 255, 255))
        d.text((10 + dx_, 30), "rock in its tones, shaded by height; structures tinted per piece; guns red,", font=fnt, fill=(200, 200, 200, 255))
        d.text((10 + dx_, 50), "stations green (18 m), routes coloured, berths yellow (ship size), signs white (tick: their yaw), gunship patrols orange", font=fnt, fill=(200, 200, 200, 255))

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
