"""Block kit for the look-development scenes. Each block is built in local metres: footprint box from (0,0,0) to
(sx,sy,sz)*CELL, +Y = front (barrels), -Y = exhaust, +Z = top. Every direction (style "shape") builds its own form
inside the same footprint, the way an enchanting table fills one Minecraft block with detail."""

import math
from mathutils import Matrix, Vector

from ld_geom import (CELL, box, cbox, cyl, lathe, tube, torus, sphere, ico, prism, fin, merge, xf, T, R, S as SC,
                     hash01, mesh_cells)

KIT = {
    # structure
    "hull_cube": (1, 1, 1), "hull_wedge": (1, 1, 1), "hull_corner": (1, 1, 1), "hull_inner": (1, 1, 1),
    "hull_slope2": (1, 2, 1), "hull_round": (1, 1, 1),
    "hull_window": (1, 1, 1), "hull_hatch": (1, 1, 1), "hull_vent": (1, 1, 1), "hull_pipes": (1, 1, 1),
    "hull_armor": (1, 1, 1), "girder": (1, 1, 1),
    # control
    "cockpit": (4, 4, 3),
    # weapons
    "autocannon": (2, 2, 2), "railgun": (2, 6, 2), "missile": (3, 3, 1), "beam": (2, 3, 2), "flak": (3, 3, 3),
    # modules
    "thruster_s": (2, 3, 2), "thruster_l": (4, 6, 4), "reactor": (3, 3, 3), "shield": (2, 2, 2), "capacitor": (2, 2, 2),
    "cargo": (2, 4, 2), "radiator": (1, 6, 4), "sensor": (2, 2, 3), "solar": (1, 8, 4),
    # cores
    "core_1": (1, 1, 1), "core_2": (2, 2, 2), "core_3": (3, 3, 3), "core_35": (3, 5, 3),
}

LABELS = {
    "hull_cube": "Hull", "hull_wedge": "Hull slope", "hull_corner": "Hull corner", "hull_inner": "Hull inner corner",
    "hull_slope2": "Long slope 1×2", "hull_round": "Rounded edge", "hull_window": "Hull · windows",
    "hull_hatch": "Hull · hatch & latches", "hull_vent": "Hull · vents", "hull_pipes": "Hull · pipes & conduit",
    "hull_armor": "Heavy armour", "girder": "Girder frame", "cockpit": "Cockpit 4×4×3",
    "autocannon": "Autocannon 2×2×2", "railgun": "Railgun 2×6×2", "missile": "Missile pod 3×3×1",
    "beam": "Beam lance 2×3×2", "flak": "Flak turret 3×3×3", "thruster_s": "Thruster 2×3×2",
    "thruster_l": "Main engine 4×6×4", "reactor": "Reactor 3×3×3", "shield": "Shield emitter 2×2×2",
    "capacitor": "Capacitor bank 2×2×2", "cargo": "Cargo pod 2×4×2", "radiator": "Radiator 1×6×4",
    "sensor": "Sensor mast 2×2×3", "solar": "Solar wing 1×8×4", "core_1": "Core 1×1×1", "core_2": "Core 2×2×2",
    "core_3": "Core 3×3×3", "core_35": "Core 3×5×3",
}

CATEGORY = {
    "Structure": ["hull_cube", "hull_wedge", "hull_corner", "hull_inner", "hull_slope2", "hull_round", "hull_armor", "girder"],
    "Procedural hull variants": ["hull_window", "hull_hatch", "hull_vent", "hull_pipes"],
    "Weapons": ["autocannon", "railgun", "missile", "beam", "flak"],
    "Modules": ["cockpit", "thruster_s", "thruster_l", "reactor", "shield", "capacitor", "cargo", "radiator", "sensor", "solar"],
    "Cores": ["core_1", "core_2", "core_3", "core_35"],
}


class B:
    def __init__(self, S, acc, group, M, size):
        self.S, self.acc, self.group, self.M = S, acc, group, M
        self.shape = S["shape"]
        self.seg = S["seg"]
        self.size = size
        self.W, self.L, self.H = size[0] * CELL, size[1] * CELL, size[2] * CELL
        self.round = self.shape not in ("lowpoly",)

    def put(self, tag, vf, smooth=None, fade=None):
        if smooth is None:
            smooth = self.round
        self.acc.add(self.group, tag, vf, self.M, smooth)

    def plume(self, p0, direction, r, length, tag="plume"):
        """Engine plume (cone, fade 1 at the nozzle -> 0 at the tip), stored with a fade attribute."""
        d = Vector(direction).normalized()
        a = Vector(p0)
        prof = [(r * 0.95, 0.0), (r * 0.8, length * 0.25), (r * 0.45, length * 0.7), (r * 0.08, length)]
        v, f = lathe(prof, tuple(a), tuple(d), max(self.seg, 12), False, False)
        n = max(self.seg, 12)
        fade = []
        for j in range(len(prof)):
            fade += [1.0 - prof[j][1] / length] * n
        self.acc.add_fade(self.group, tag, (v, f), self.M, fade)


# ======================================================================================================================
# helpers
# ======================================================================================================================

def ring_posts(b, cx, cy, half, z0, z1, r, tag="trim", n=4, phase=45):
    for i in range(n):
        a = math.radians(phase + 360 * i / n)
        x, y = cx + math.cos(a) * half, cy + math.sin(a) * half
        b.put(tag, cyl((x, y, z0), (x, y, z1), r, 6 if b.shape == "lowpoly" else 8))


def corner_posts(b, x0, y0, x1, y1, z0, z1, t, tag="trim"):
    for x in (x0, x1 - t):
        for y in (y0, y1 - t):
            b.put(tag, box((x, y, z0), (x + t, y + t, z1)), smooth=False)


def bell(b, p0, axis, r_throat, r_exit, length, tag="metal", inner="emit_t", ribs=0):
    """Rocket nozzle bell from p0 along axis."""
    seg = max(b.seg, 8)
    prof = [(r_throat, 0.0), (r_throat * 1.05, length * 0.15), (r_exit * 0.7, length * 0.55), (r_exit, length)]
    v, f = lathe(prof, p0, axis, seg, False, False)
    b.put(tag, (v, f))
    # inner glow disc near the throat
    a = Vector(p0) + Vector(axis).normalized() * (length * 0.2)
    b.put(inner, cyl(tuple(a), tuple(a + Vector(axis).normalized() * 0.01), r_throat * 1.2, seg))
    # inner liner (darker) so the bell reads hollow
    prof2 = [(r_throat * 0.92, 0.01), (r_exit * 0.66, length * 0.55), (r_exit * 0.95, length * 0.99)]
    v2, f2 = lathe(prof2, p0, axis, seg, False, False)
    f2 = [tuple(reversed(face)) for face in f2]
    b.put("dark", (v2, f2))
    for i in range(ribs):
        t = 0.25 + 0.6 * i / max(1, ribs - 1)
        rr = r_throat + (r_exit - r_throat) * t * t + 0.012
        c = Vector(p0) + Vector(axis).normalized() * (length * t)
        b.put("trim", torus(tuple(c), axis, rr, 0.012, seg, 4))


def lens(b, center, r, tag="emit_w"):
    if b.shape == "lowpoly":
        b.put(tag, ico(center, r, 0), smooth=False)
    else:
        b.put(tag, sphere(center, r, max(b.seg, 10), max(b.seg // 2, 5)))


def hazard(b, mn, mx, n=4, axis=0):
    """Diagonal-ish hazard stripes as alternating thin plates (industrial accent)."""
    span = mx[axis] - mn[axis]
    for i in range(n):
        if i % 2:
            continue
        a = list(mn)
        c = list(mx)
        a[axis] = mn[axis] + span * i / n
        c[axis] = mn[axis] + span * (i + 1) / n
        b.put("accent", box(tuple(a), tuple(c)), smooth=False)


# ======================================================================================================================
# Hull pieces (single cells, with optional greeble faces)
# ======================================================================================================================

def hull_piece(b, kind):
    cells = {(0, 0, 0): "hull"}
    shapes = {}
    if kind == "hull_wedge":
        shapes[(0, 0, 0)] = ("slope", ((0, 1, 0), (0, 0, 1)))
    elif kind == "hull_corner":
        C = CELL
        v = [(0, 0, 0), (C, 0, 0), (0, C, 0), (0, 0, C)]
        b.put("hull", (v, [(0, 2, 1), (0, 1, 3), (0, 3, 2), (1, 2, 3)]), smooth=False)
        return
    elif kind == "hull_inner":
        # inverted corner: keep x+y+z <= 2
        verts, faces = [], []
        polys = _inner_corner_polys()
        for dvec, poly, full in polys:
            base = len(verts)
            verts += [tuple(c * CELL for c in p) for p in poly]
            faces.append(tuple(range(base, base + len(poly))))
        b.put("hull", (verts, faces), smooth=False)
        return
    elif kind == "hull_slope2":
        cells = {(0, 0, 0): "hull", (0, 1, 0): "hull"}
        v = [(0, 0, 0), (CELL, 0, 0), (CELL, 2 * CELL, 0), (0, 2 * CELL, 0), (0, 0, CELL), (CELL, 0, CELL)]
        f = [(0, 3, 2, 1), (0, 1, 5, 4), (4, 5, 2, 3), (0, 4, 3), (1, 2, 5)]
        b.put("hull", (v, f), smooth=False)
        return
    elif kind == "hull_round":
        n = 8 if b.shape != "lowpoly" else 3
        # quarter round: profile in YZ, extruded along X
        prof = [(0.0, 0.0)] + [(math.sin(math.pi / 2 * i / n) * CELL, math.cos(math.pi / 2 * i / n) * CELL) for i in range(n + 1)]
        v, f = fin(prof[::-1], CELL, CELL / 2)
        b.put("hull", (v, f), smooth=False)
        return
    tag = "armor" if kind == "hull_armor" else "hull"
    out, exposed = mesh_cells({k: tag for k in cells}, shapes)
    for t, vf in out.items():
        b.put(t, vf, smooth=False)
    if kind == "hull_armor":
        # thick bolted plate on top and front
        b.put("armor", box((0.03, 0.03, CELL), (CELL - 0.03, CELL - 0.03, CELL + 0.05)), smooth=False)
        b.put("armor", box((0.03, CELL, 0.03), (CELL - 0.03, CELL + 0.05, CELL - 0.03)), smooth=False)
        for x in (0.07, CELL - 0.07):
            for y in (0.07, CELL - 0.07):
                b.put("metal", cyl((x, y, CELL + 0.05), (x, y, CELL + 0.065), 0.018, 6))
    greeble_kind = {"hull_window": "window", "hull_hatch": "hatch", "hull_vent": "vent", "hull_pipes": "pipe"}.get(kind)
    if greeble_kind:
        faces = ((0, 1, 0), (1, 0, 0)) if greeble_kind == "window" else ((0, 0, 1), (0, 1, 0), (1, 0, 0))
        for face in faces:
            greeble_face(b.S, b.acc, b.group, b.M, (0, 0, 0), face, greeble_kind, 0.37, run=(0, 1))


def _inner_corner_polys():
    from ld_geom import CUBE_FACES, clip_poly, cap_poly
    n, d = (1, 1, 1), 2.0
    polys = []
    for dvec, quad in CUBE_FACES.items():
        cp = clip_poly(quad, n, d)
        if cp:
            polys.append((dvec, cp, False))
    cap = cap_poly(n, d)
    polys.append((None, cap, False))
    return polys


def girder(b):
    t = 0.05
    L = CELL
    for x in (0, L - t):
        for y in (0, L - t):
            b.put("trim", box((x, y, 0), (x + t, y + t, L)), smooth=False)
        for z in (0, L - t):
            b.put("trim", box((x, 0, z), (x + t, L, z + t)), smooth=False)
    for y in (0, L - t):
        for z in (0, L - t):
            b.put("trim", box((0, y, z), (L, y + t, z + t)), smooth=False)
    for i in range(4):
        z = (i + 0.5) * L / 4
        b.put("metal", cyl((t, L / 2, z), (L - t, L / 2, z + 0.06), 0.012, 6))


# ======================================================================================================================
# Greebles on an exposed cell face (procedural hull variety)
# ======================================================================================================================

def face_frame(cell, normal):
    """Centre, normal, tangent u (along ship length where possible) and v of a cell face, in group metres."""
    n = Vector(normal)
    c = (Vector(cell) + Vector((0.5, 0.5, 0.5)) + n * 0.5) * CELL
    if abs(n.y) < 0.5:
        u = Vector((0, 1, 0))
    else:
        u = Vector((1, 0, 0))
    v = n.cross(u)
    return c, n, u, v


def frame_matrix(c, n, u, v):
    m = Matrix.Identity(4)
    for i in range(3):
        m[i][0], m[i][1], m[i][2], m[i][3] = u[i], v[i], n[i], c[i]
    return m


def greeble_face(S, acc, group, M, cell, normal, kind, rnd, run=(0, 1)):
    """Add a greeble of `kind` on one exposed face. Local frame: x=u (length), y=v, z=out of the face."""
    c, n, u, v = face_frame(cell, normal)
    F = M @ frame_matrix(c, n, u, v)
    h = CELL / 2
    shape = S["shape"]
    seg = 6 if shape == "lowpoly" else 10

    def put(tag, vf, smooth=False):
        acc.add(group, tag, vf, F, smooth)

    if kind == "plate":
        put("hull2" if rnd < 0.5 else "armor", box((-h + 0.035, -h + 0.035, 0), (h - 0.035, h - 0.035, 0.025)))
    elif kind == "window":
        if shape == "retro":
            put("trim", tube((0, 0, 0), (0, 0, 0.03), 0.13, 0.10, 16), True)
            put("window", cyl((0, 0, 0.0), (0, 0, 0.015), 0.105, 16), True)
        elif shape == "organic":
            put("window", sphere((0, 0, 0), 0.09, 10, 5, (1, 1, 0.5)), True)
        else:
            # strip window spanning the face along u
            put("dark", box((-h, -0.07, -0.005), (h, 0.07, 0.012)))
            put("window", box((-h + 0.02, -0.05, 0.0), (h - 0.02, 0.05, 0.016)))
            if shape == "industrial":
                put("trim", box((-0.012, -0.075, 0.0), (0.012, 0.075, 0.022)))
    elif kind == "porthole":
        put("trim", tube((0, 0, 0), (0, 0, 0.03), 0.11, 0.08, 16), True)
        put("window", cyl((0, 0, 0.0), (0, 0, 0.012), 0.085, 16), True)
    elif kind == "hatch":
        put("trim", box((-0.16, -0.16, 0), (0.16, 0.16, 0.02)))
        put("hull2", box((-0.13, -0.13, 0.02), (0.13, 0.13, 0.03)))
        put("metal", box((-0.07, -0.015, 0.03), (0.07, 0.015, 0.05)))
        for sx in (-1, 1):
            for sy in (-1, 1):
                put("metal", box((sx * 0.14 - 0.02, sy * 0.14 - 0.02, 0.0), (sx * 0.14 + 0.02, sy * 0.14 + 0.02, 0.04)))
        if shape == "industrial":
            put("accent", box((-0.2, -0.2, 0), (-0.17, 0.2, 0.012)))
            put("accent", box((0.17, -0.2, 0), (0.2, 0.2, 0.012)))
    elif kind == "vent":
        put("dark", box((-0.17, -0.12, -0.002), (0.17, 0.12, 0.006)))
        for i in range(5):
            y = -0.1 + i * 0.05
            put("trim", box((-0.17, y - 0.01, 0.0), (0.17, y + 0.01, 0.03)))
    elif kind == "pipe":
        # 1-3 parallel pipes along u, with brackets
        k = 1 + int(rnd * 3)
        for i in range(k):
            off = (i - (k - 1) / 2) * 0.08
            put("metal" if i % 2 == 0 else "foil", cyl((-h, off, 0.045), (h, off, 0.045), 0.03, seg), True)
        if run[0] % 2 == 0:
            put("trim", box((-0.03, -0.05 - (k - 1) * 0.04, 0), (0.03, 0.05 + (k - 1) * 0.04, 0.09)))
    elif kind == "light":
        put("trim", cyl((0, 0, 0), (0, 0, 0.03), 0.04, 8), True)
        put("nav_w", sphere((0, 0, 0.04), 0.03, 8, 4), True)
    elif kind == "antenna":
        put("metal", cyl((0, 0, 0), (0, 0, 0.6 + rnd * 0.6), 0.012, 6))
        put("trim", cyl((0, 0, 0), (0, 0, 0.06), 0.05, 8))
        put("nav_r", sphere((0, 0, 0.62 + rnd * 0.6), 0.025, 6, 3), True)
    elif kind == "stripe":
        for i in range(4):
            x0 = -h + i * CELL / 4
            if i % 2 == 0:
                put("accent", box((x0, -h, 0), (x0 + CELL / 4, h, 0.008)))
    elif kind == "lightline":
        put("emit_s", box((-h, -0.012, 0), (h, 0.012, 0.01)))
    elif kind == "handrail":
        for x in (-0.18, 0.18):
            put("accent", cyl((x, 0.0, 0.0), (x, 0.0, 0.07), 0.01, 6))
        put("accent", cyl((-0.2, 0.0, 0.07), (0.2, 0.0, 0.07), 0.012, 6))
    elif kind == "foil":
        put("foil", box((-h + 0.02, -h + 0.02, 0), (h - 0.02, h - 0.02, 0.012)))
    elif kind == "pore":
        put("dark", sphere((0, 0, 0.0), 0.08, 10, 5, (1, 1, 0.3)), True)
        put("window", sphere((0, 0, -0.01), 0.05, 10, 5), True)
    elif kind == "vein":
        put("emit_p", cyl((-h, 0, 0.0), (h, 0, 0.0), 0.025, 6), True)
        put("hull2", cyl((-h, 0.06, 0.0), (h, 0.06, 0.0), 0.035, 6), True)
    elif kind == "spine":
        put("hull2", cyl((0, 0, 0), (0, 0, 0.35 + rnd * 0.3), 0.06, 8, r1=0.0), True)
    elif kind == "stud":
        put("hull2", prism([(math.cos(a) * 0.12, math.sin(a) * 0.12) for a in [i * math.pi / 3 for i in range(6)]], 0, 0.06))


# ======================================================================================================================
# Functional blocks
# ======================================================================================================================

def cockpit(b):
    W, L, H = b.W, b.L, b.H
    sh = b.shape
    if sh == "industrial":
        b.put("hull", box((0, 0, 0), (W, L, H * 0.5)), False)
        v = [(0.1, 0.1), (W - 0.1, 0.1), (W - 0.25, L - 0.15), (0.25, L - 0.15)]
        b.put("trim", prism(v, H * 0.5, H * 0.55), False)
        # faceted canopy
        cv = [(0.18, 0.2, H * 0.55), (W - 0.18, 0.2, H * 0.55), (W - 0.32, L - 0.25, H * 0.55), (0.32, L - 0.25, H * 0.55),
              (0.35, 0.45, H), (W - 0.35, 0.45, H), (W - 0.45, L - 0.55, H * 0.95), (0.45, L - 0.55, H * 0.95)]
        cf = [(0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7), (4, 5, 6, 7)]
        b.put("glass", (cv, cf), False)
        for t in (0.35, 0.6):
            y = 0.2 + (L - 0.45) * t
            b.put("trim", box((0.25, y - 0.02, H * 0.55), (W - 0.25, y + 0.02, H * 0.98)), False)
        b.put("trim", box((W / 2 - 0.025, 0.2, H * 0.55), (W / 2 + 0.025, L - 0.25, H * 1.0)), False)
        hazard(b, (0.0, -0.01, H * 0.3), (W, 0.0, H * 0.45), 8)
    elif sh == "sleek":
        b.put("hull", box((0, 0, 0), (W, L, H * 0.4)), False)
        b.put("glass", sphere((W / 2, L * 0.55, H * 0.4), 1.0, 24, 12, (W * 0.38, L * 0.45, H * 0.55)))
        b.put("emit_s", lathe([(W * 0.4, 0), (W * 0.4, 0.015)], (W / 2, L * 0.55, H * 0.4), (0, 0, 1), 32, False, False))
        b.put("trim", box((W / 2 - 0.02, L * 0.12, H * 0.4), (W / 2 + 0.02, L * 0.98, H * 0.95)), False)
    elif sh == "retro":
        b.put("hull", cyl((W / 2, L / 2, 0), (W / 2, L / 2, H * 0.45), min(W, L) * 0.48, b.seg))
        b.put("trim", torus((W / 2, L / 2, H * 0.45), (0, 0, 1), min(W, L) * 0.4, 0.06, b.seg, 8))
        b.put("glass", sphere((W / 2, L / 2, H * 0.45), min(W, L) * 0.4, b.seg, 10, (1, 1, 1.25)))
        b.put("armor", fin([(L * 0.1, H * 0.45), (L * 0.35, H * 0.45), (L * 0.15, H * 1.1)], 0.06, W / 2))
    elif sh == "nasa":
        b.put("hull", cyl((W / 2, 0.05, H / 2), (W / 2, L - 0.05, H / 2), min(W, H) * 0.48, b.seg))
        b.put("trim", lathe([(min(W, H) * 0.48, 0), (min(W, H) * 0.3, 0.25), (min(W, H) * 0.3, 0.32)],
                            (W / 2, L - 0.05, H / 2), (0, 1, 0), b.seg))
        b.put("metal", tube((W / 2, L + 0.2, H / 2), (W / 2, L + 0.28, H / 2), 0.36, 0.28, b.seg))
        for i in range(4):
            a = math.radians(90 * i + 45)
            x, z = W / 2 + math.cos(a) * min(W, H) * 0.49, H / 2 + math.sin(a) * min(W, H) * 0.49
            b.put("trim", tube((x, L * 0.65, z), (x, L * 0.65 + 0.01, z), 0.09, 0.0001, 12))
            b.put("glass", cyl((x, L * 0.65, z), (x, L * 0.65 + 0.01, z), 0.07, 12))
        b.put("foil", cyl((W / 2, 0.05, H / 2), (W / 2, 0.35, H / 2), min(W, H) * 0.5, b.seg))
        for z in (H * 0.15, H * 0.85):
            b.put("accent", cyl((0.02, L * 0.3, z), (0.02, L * 0.8, z), 0.015, 6))
    elif sh == "organic":
        b.put("hull", sphere((W / 2, L / 2, H * 0.35), 1.0, 16, 8, (W * 0.5, L * 0.5, H * 0.4)))
        b.put("glass", sphere((W / 2, L * 0.62, H * 0.62), 1.0, 16, 8, (W * 0.26, L * 0.3, H * 0.28)))
        b.put("hull2", sphere((W / 2, L * 0.55, H * 0.78), 1.0, 12, 6, (W * 0.3, L * 0.25, H * 0.08)))
    else:  # lowpoly
        b.put("hull", box((0, 0, 0), (W, L, H * 0.45)), False)
        cv = [(0.15, 0.1, H * 0.45), (W - 0.15, 0.1, H * 0.45), (W / 2, L - 0.05, H * 0.45), (W / 2, L * 0.4, H)]
        cf = [(0, 1, 3), (1, 2, 3), (2, 0, 3), (0, 2, 1)]
        b.put("glass", (cv, cf), False)
        b.put("hull2", prism([(0.0, 0.0), (W, 0.0), (W, 0.2), (0.0, 0.2)], 0, H * 0.6), False)


def autocannon(b):
    W, L, H = b.W, b.L, b.H
    cx, cy = W / 2, L / 2
    sh = b.shape
    b.put("trim", cyl((cx, cy, 0), (cx, cy, 0.14), W * 0.46, b.seg))
    b.put("dark", cyl((cx, cy, 0.14), (cx, cy, 0.17), W * 0.38, b.seg))
    if sh == "industrial":
        b.put("armor", box((cx - 0.34, cy - 0.32, 0.17), (cx + 0.34, cy + 0.26, 0.6)), False)
        b.put("armor", box((cx - 0.3, cy + 0.26, 0.2), (cx + 0.3, cy + 0.38, 0.55)), False)
        for s in (-1, 1):
            x = cx + s * 0.13
            b.put("metal", cyl((x, cy + 0.38, 0.4), (x, L + 0.35, 0.4), 0.045, 10))
            b.put("dark", box((x - 0.07, L + 0.18, 0.33), (x + 0.07, L + 0.35, 0.47)), False)
            b.put("trim", cyl((x, cy + 0.38, 0.4), (x, cy + 0.6, 0.4), 0.07, 10))
        b.put("hull2", box((cx + 0.34, cy - 0.25, 0.25), (cx + 0.46, cy + 0.15, 0.5)), False)
        b.put("emit_w", box((cx - 0.2, cy - 0.33, 0.45), (cx + 0.2, cy - 0.31, 0.5)), False)
        hazard(b, (cx - 0.34, cy - 0.33, 0.2), (cx + 0.34, cy - 0.32, 0.3), 6)
    elif sh == "sleek":
        v = [(cx - 0.35, cy - 0.35, 0.17), (cx + 0.35, cy - 0.35, 0.17), (cx + 0.2, cy + 0.4, 0.17), (cx - 0.2, cy + 0.4, 0.17),
             (cx - 0.25, cy - 0.3, 0.42), (cx + 0.25, cy - 0.3, 0.42), (cx + 0.08, cy + 0.3, 0.36), (cx - 0.08, cy + 0.3, 0.36)]
        f = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
        b.put("hull", (v, f), False)
        b.put("metal", cyl((cx, cy + 0.3, 0.3), (cx, L + 0.5, 0.3), 0.04, 16))
        for i in range(3):
            y = cy + 0.5 + i * 0.22
            b.put("emit_w", torus((cx, y, 0.3), (0, 1, 0), 0.05, 0.012, 16, 4))
        b.put("emit_s", box((cx - 0.26, cy - 0.31, 0.38), (cx + 0.26, cy - 0.29, 0.4)), False)
    elif sh == "retro":
        b.put("hull", sphere((cx, cy, 0.17), 0.36, b.seg, 10, (1, 1, 0.9)))
        for s in (-1, 1):
            x = cx + s * 0.15
            b.put("metal", cyl((x, cy + 0.2, 0.32), (x, L + 0.25, 0.32), 0.05, b.seg, r1=0.03))
            for i in range(3):
                b.put("armor", torus((x, cy + 0.45 + i * 0.16, 0.32), (0, 1, 0), 0.06, 0.018, b.seg, 6))
            b.put("emit_w", sphere((x, L + 0.27, 0.32), 0.04, 10, 5))
        b.put("glass", sphere((cx, cy - 0.05, 0.42), 0.12, 12, 6))
    elif sh == "nasa":
        b.put("foil", cyl((cx, cy, 0.17), (cx, cy, 0.55), 0.3, b.seg))
        b.put("hull", box((cx - 0.12, cy - 0.38, 0.25), (cx + 0.12, cy - 0.25, 0.5)), False)
        b.put("metal", cyl((cx, cy, 0.42), (cx, L + 0.4, 0.42), 0.035, 12))
        b.put("trim", cyl((cx, cy + 0.2, 0.42), (cx, cy + 0.55, 0.42), 0.07, 12))
        b.put("glass", cyl((cx + 0.22, cy + 0.1, 0.45), (cx + 0.22, cy + 0.3, 0.45), 0.05, 12))
    elif sh == "organic":
        b.put("hull", sphere((cx, cy, 0.3), 0.36, 12, 6, (1, 1.1, 0.75)))
        for s in (-1, 1):
            x = cx + s * 0.16
            b.put("hull2", cyl((x, cy + 0.1, 0.32), (x + s * 0.05, L + 0.3, 0.38), 0.07, 8, r1=0.025))
            b.put("emit_w", sphere((x + s * 0.05, L + 0.31, 0.38), 0.035, 8, 4))
    else:  # lowpoly
        b.put("hull", prism([(cx + math.cos(a) * 0.36, cy + math.sin(a) * 0.36) for a in [i * math.pi / 3 for i in range(6)]],
                            0.17, 0.5), False)
        b.put("hull2", box((cx - 0.3, cy - 0.1, 0.5), (cx + 0.3, cy + 0.25, 0.58)), False)
        for s in (-1, 1):
            x = cx + s * 0.14
            b.put("metal", box((x - 0.04, cy + 0.2, 0.3), (x + 0.04, L + 0.3, 0.38)), False)
            b.put("emit_w", box((x - 0.045, L + 0.3, 0.295), (x + 0.045, L + 0.34, 0.385)), False)


def railgun(b):
    W, L, H = b.W, b.L, b.H
    cx = W / 2
    sh = b.shape
    b.put("trim" if sh != "organic" else "hull", box((0.05, 0.0, 0.0), (W - 0.05, 0.9, H * 0.85)), sh == "organic")
    zc = H * 0.5
    rail_t = 0.07
    gap = 0.11
    for s in (-1, 1):
        x0 = cx + s * gap
        if sh == "lowpoly":
            b.put("metal", box((x0 - rail_t / 2, 0.5, zc - 0.12), (x0 + rail_t / 2, L + 0.3, zc + 0.12)), False)
        elif sh == "organic":
            b.put("hull2", cyl((x0, 0.5, zc), (x0, L + 0.3, zc), 0.06, 8, r1=0.03))
        else:
            b.put("metal", box((x0 - rail_t / 2, 0.5, zc - 0.13), (x0 + rail_t / 2, L + 0.35, zc + 0.13)), False)
            b.put("dark", box((x0 - rail_t / 2 - 0.005, 0.5, zc - 0.02), (x0 + rail_t / 2 + 0.005, L + 0.35, zc + 0.02)), False)
    # field coils along the rails
    n = 5 if sh != "retro" else 6
    for i in range(n):
        y = 1.0 + i * (L - 0.7) / n
        if sh == "lowpoly":
            b.put("hull2", tube((cx, y, zc), (cx, y + 0.1, zc), 0.26, 0.2, 4), False)
        elif sh == "sleek":
            b.put("emit_w", tube((cx, y, zc), (cx, y + 0.03, zc), 0.23, 0.2, 24))
        elif sh == "retro":
            b.put("armor", torus((cx, y, zc), (0, 1, 0), 0.24, 0.05, b.seg, 8))
        elif sh == "organic":
            b.put("hull", torus((cx, y, zc), (0, 1, 0), 0.2, 0.05, 12, 6))
        else:
            b.put("hull2", tube((cx, y, zc), (cx, y + 0.12, zc), 0.25, 0.2, b.seg))
            b.put("emit_w", tube((cx, y + 0.12, zc), (cx, y + 0.14, zc), 0.22, 0.2, b.seg))
    b.put("emit_w", box((cx - 0.03, 0.6, zc - 0.03), (cx + 0.03, L + 0.3, zc + 0.03)), False)
    if sh == "industrial":
        hazard(b, (0.05, -0.005, 0.1), (W - 0.05, 0.0, 0.3), 6)
        for s in (-1, 1):
            b.put("metal", cyl((cx + s * 0.38, 0.1, zc), (cx + s * 0.38, 0.8, zc), 0.07, 10))


def missile(b):
    W, L, H = b.W, b.L, b.H
    sh = b.shape
    b.put("hull" if sh != "industrial" else "armor", box((0, 0, 0), (W, L, H * 0.8)), False)
    b.put("trim", box((0.04, 0.04, H * 0.8), (W - 0.04, L - 0.04, H * 0.85)), False)
    for i in range(3):
        for j in range(3):
            x = 0.25 + i * 0.5
            y = 0.25 + j * 0.5
            z = H * 0.85
            if sh == "lowpoly":
                b.put("dark", prism([(x + math.cos(a) * 0.17, y + math.sin(a) * 0.17) for a in [k * math.pi / 3 for k in range(6)]],
                                    z - 0.01, z + 0.01), False)
            else:
                b.put("dark", cyl((x, y, z - 0.01), (x, y, z + 0.012), 0.17, b.seg))
            opened = hash01(i, j, 7) < 0.45
            if sh == "retro" and opened:
                b.put("armor", cyl((x, y, z), (x, y, z + 0.18), 0.12, b.seg, r1=0.0))
            elif sh == "organic":
                b.put("emit_w", sphere((x, y, z), 0.1, 10, 5, (1, 1, 0.6)))
            elif opened:
                b.put("emit_w", cyl((x, y, z - 0.005), (x, y, z + 0.015), 0.1, b.seg))
                b.put("hull2", box((x - 0.17, y + 0.17, z), (x + 0.17, y + 0.2, z + 0.3)), False)
            else:
                b.put("hull2", box((x - 0.18, y - 0.18, z + 0.01), (x + 0.18, y + 0.18, z + 0.04)), False)
                if sh == "industrial":
                    b.put("accent", box((x - 0.18, y - 0.03, z + 0.04), (x + 0.18, y + 0.03, z + 0.045)), False)


def beam(b):
    W, L, H = b.W, b.L, b.H
    cx, zc = W / 2, H * 0.5
    sh = b.shape
    if sh == "retro":
        b.put("hull", sphere((cx, 0.4, zc), 0.42, b.seg, 10))
        b.put("metal", lathe([(0.35, 0), (0.12, 0.3), (0.06, 0.9)], (cx, 0.6, zc), (0, 1, 0), b.seg))
        for i in range(3):
            b.put("armor", torus((cx, 0.85 + i * 0.25, zc), (0, 1, 0), 0.24 - i * 0.06, 0.025, b.seg, 6))
        lens(b, (cx, 1.55, zc), 0.08)
        for a in (0, 120, 240):
            r = math.radians(a)
            b.put("metal", cyl((cx + math.cos(r) * 0.2, 0.9, zc + math.sin(r) * 0.2), (cx + math.cos(r) * 0.05, 1.75, zc + math.sin(r) * 0.05), 0.015, 6))
        return
    if sh == "organic":
        b.put("hull", sphere((cx, 0.5, zc), 1.0, 12, 6, (0.45, 0.6, 0.42)))
        b.put("hull2", cyl((cx, 0.8, zc), (cx, 1.6, zc), 0.22, 10, r1=0.1))
        lens(b, (cx, 1.62, zc), 0.12)
        return
    body_tag = "hull" if sh in ("sleek", "nasa") else "armor"
    b.put(body_tag, box((0.05, 0.0, 0.05), (W - 0.05, 0.75, H - 0.1)), False)
    seg = b.seg
    b.put("trim", cyl((cx, 0.75, zc), (cx, 1.35, zc), 0.3, seg, r1=0.16))
    for i in range(3):
        y = 0.85 + i * 0.18
        if sh == "lowpoly":
            b.put("hull2", tube((cx, y, zc), (cx, y + 0.06, zc), 0.3 - i * 0.04, 0.12, 6), False)
        else:
            b.put("emit_w" if sh == "sleek" else "metal", torus((cx, y, zc), (0, 1, 0), 0.27 - i * 0.045, 0.02, seg, 6))
    b.put("metal", cyl((cx, 1.35, zc), (cx, 1.6, zc), 0.1, seg))
    lens(b, (cx, 1.62, zc), 0.1)
    if sh == "industrial":
        for k in range(6):
            y = 0.08 + k * 0.11
            b.put("trim", box((W - 0.05, y, 0.15), (W + 0.04, y + 0.05, H - 0.2)), False)
            b.put("trim", box((-0.04, y, 0.15), (0.05, y + 0.05, H - 0.2)), False)


def flak(b):
    W, L, H = b.W, b.L, b.H
    cx, cy = W / 2, L / 2
    sh = b.shape
    b.put("trim", cyl((cx, cy, 0), (cx, cy, 0.2), W * 0.48, b.seg))
    if sh in ("retro", "organic", "sleek"):
        b.put("armor" if sh != "sleek" else "hull", sphere((cx, cy, 0.2), 0.62, b.seg, 10, (1, 1, 0.75)))
    elif sh == "lowpoly":
        b.put("armor", prism([(cx + math.cos(a) * 0.65, cy + math.sin(a) * 0.65) for a in [i * math.pi / 4 + math.pi / 8 for i in range(8)]],
                             0.2, 0.85), False)
    else:
        v = [(cx - 0.65, cy - 0.65, 0.2), (cx + 0.65, cy - 0.65, 0.2), (cx + 0.65, cy + 0.45, 0.2), (cx - 0.65, cy + 0.45, 0.2),
             (cx - 0.55, cy - 0.55, 0.9), (cx + 0.55, cy - 0.55, 0.9), (cx + 0.5, cy + 0.25, 0.9), (cx - 0.5, cy + 0.25, 0.9)]
        f = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
        b.put("armor", (v, f), False)
        if sh == "nasa":
            b.put("foil", box((cx - 0.5, cy - 0.5, 0.9), (cx + 0.5, cy + 0.2, 0.95)), False)
    b.put("dark", box((cx - 0.42, cy + 0.3, 0.32), (cx + 0.42, cy + 0.62, 0.78)), False)
    for sx in (-1, 1):
        for sz in (-1, 1):
            x = cx + sx * 0.17
            z = 0.55 + sz * 0.12
            if sh == "lowpoly":
                b.put("metal", box((x - 0.05, cy + 0.5, z - 0.05), (x + 0.05, L + 0.55, z + 0.05)), False)
            else:
                b.put("metal", cyl((x, cy + 0.5, z), (x, L + 0.55, z), 0.05, 10))
                b.put("dark", cyl((x, L + 0.45, z), (x, L + 0.6, z), 0.065, 10))
    b.put("emit_w", box((cx - 0.3, cy - 0.66, 0.7), (cx + 0.3, cy - 0.64, 0.76)), False)
    if sh == "industrial":
        hazard(b, (cx - 0.65, cy - 0.66, 0.25), (cx + 0.65, cy - 0.65, 0.4), 10)
        b.put("glass", box((cx + 0.3, cy - 0.3, 0.9), (cx + 0.5, cy - 0.05, 0.98)), False)


def thruster(b, big=False):
    W, L, H = b.W, b.L, b.H
    cx, zc = W / 2, H / 2
    sh = b.shape
    r_exit = min(W, H) * (0.46 if not big else 0.44)
    nozzle_len = L * 0.42
    housing_y0 = nozzle_len
    if sh == "industrial":
        b.put("hull2", box((0.0, housing_y0, 0.0), (W, L, H)), False)
        b.put("trim", box((-0.02, housing_y0 - 0.06, -0.02), (W + 0.02, housing_y0 + 0.08, H + 0.02)), False)
        bell(b, (cx, housing_y0, zc), (0, -1, 0), r_exit * 0.45, r_exit, nozzle_len, "metal", ribs=3)
        for s in (-1, 1):
            b.put("metal", cyl((cx + s * r_exit * 0.8, housing_y0, zc + r_exit * 0.8), (cx + s * r_exit * 0.6, housing_y0 - nozzle_len * 0.6, zc + r_exit * 0.5), 0.025, 6))
        hazard(b, (0.0, L - 0.2, H), (W, L, H + 0.01), 8, axis=0)
        if big:
            for sx in (-1, 1):
                for sz in (-1, 1):
                    bell(b, (cx + sx * W * 0.38, housing_y0, zc + sz * H * 0.38), (0, -1, 0), 0.05, 0.11, 0.25, "metal")
    elif sh == "sleek":
        b.put("hull", box((0.0, housing_y0 * 0.5, 0.05), (W, L, H - 0.05)), False)
        b.put("dark", box((0.08, housing_y0 * 0.5 - 0.05, 0.15), (W - 0.08, housing_y0 * 0.5 + 0.01, H - 0.15)), False)
        b.put("emit_t", box((0.12, housing_y0 * 0.5 - 0.06, 0.2), (W - 0.12, housing_y0 * 0.5 - 0.04, H - 0.2)), False)
        b.put("emit_s", box((-0.005, housing_y0 * 0.5, H * 0.5 - 0.01), (W + 0.005, L, H * 0.5 + 0.01)), False)
    elif sh == "retro":
        b.put("hull", cyl((cx, housing_y0, zc), (cx, L, zc), r_exit * 0.95, b.seg, r1=r_exit * 0.7))
        bell(b, (cx, housing_y0, zc), (0, -1, 0), r_exit * 0.5, r_exit * 1.05, nozzle_len, "metal", ribs=2)
        b.put("armor", torus((cx, housing_y0 + 0.05, zc), (0, 1, 0), r_exit * 0.95, 0.05, b.seg, 8))
        for a in (0, 120, 240):
            r = math.radians(a + 90)
            dx, dz = math.cos(r), math.sin(r)
            pts = [(housing_y0 + 0.1, 0.0), (L - 0.1, 0.0), (housing_y0 - 0.2, r_exit * 0.9)]
            fv, ff = fin(pts, 0.04, 0.0)
            v = []
            for (x, y, z) in fv:
                # fin plane spans (y, radial)
                rad = r_exit * 0.8 + z
                v.append((cx + dx * rad + (-dz) * x, y, zc + dz * rad + dx * x))
            b.put("armor", (v, ff), False)
    elif sh == "nasa":
        b.put("foil", cyl((cx, housing_y0, zc), (cx, L, zc), r_exit * 0.8, b.seg))
        b.put("trim", box((0.05, L * 0.7, 0.05), (W - 0.05, L, H - 0.05)), False)
        bell(b, (cx, housing_y0, zc), (0, -1, 0), r_exit * 0.35, r_exit, nozzle_len, "metal", ribs=0)
        b.put("dark", torus((cx, housing_y0 - nozzle_len * 0.5, zc), (0, 1, 0), r_exit * 0.7, 0.012, b.seg, 4))
    elif sh == "organic":
        b.put("hull", sphere((cx, L * 0.62, zc), 1.0, 12, 6, (W * 0.5, L * 0.45, H * 0.5)))
        b.put("hull2", lathe([(r_exit * 0.3, 0), (r_exit * 0.9, nozzle_len * 0.5), (r_exit * 1.0, nozzle_len * 0.9)],
                             (cx, housing_y0 + 0.1, zc), (0, -1, 0), 12, False, False))
        b.put("emit_t", sphere((cx, housing_y0 - 0.05, zc), r_exit * 0.45, 12, 6))
    else:  # lowpoly
        b.put("hull2", box((0.0, housing_y0, 0.0), (W, L, H)), False)
        b.put("metal", lathe([(r_exit * 0.5, 0), (r_exit, nozzle_len)], (cx, housing_y0, zc), (0, -1, 0), 6, False, False, math.pi / 6), False)
        b.put("emit_t", lathe([(r_exit * 0.48, 0.02), (0.0, 0.03)], (cx, housing_y0, zc), (0, -1, 0), 6, False, False, math.pi / 6), False)
        b.put("accent", box((0.0, L - 0.15, H), (W, L, H + 0.04)), False)
    return (cx, 0.0, zc), r_exit


def reactor(b):
    W, L, H = b.W, b.L, b.H
    cx, cy = W / 2, L / 2
    sh = b.shape
    if sh == "industrial":
        b.put("trim", box((0, 0, 0), (W, L, 0.15)), False)
        corner_posts(b, 0, 0, W, L, 0.15, H, 0.1, "hull2")
        b.put("trim", box((0, 0, H - 0.12), (W, L, H)), False)
        b.put("emit_p", torus((cx, cy, H / 2), (0, 0, 1), 0.42, 0.11, 32, 10))
        b.put("metal", cyl((cx, cy, 0.15), (cx, cy, H - 0.12), 0.16, 16))
        for a in range(8):
            r = math.radians(a * 45)
            b.put("trim", box((cx + math.cos(r) * 0.42 - 0.05, cy + math.sin(r) * 0.42 - 0.05, H / 2 - 0.16),
                              (cx + math.cos(r) * 0.42 + 0.05, cy + math.sin(r) * 0.42 + 0.05, H / 2 + 0.16)), False)
        for s in (-1, 1):
            b.put("foil", cyl((cx + s * 0.62, 0.1, 0.4), (cx + s * 0.62, L - 0.1, 0.4), 0.06, 10))
        hazard(b, (0.0, -0.005, 0.15), (W, 0.0, 0.35), 10)
    elif sh == "sleek":
        b.put("hull", box((0, 0, 0), (W, L, 0.2)), False)
        b.put("hull", box((0, 0, H - 0.2), (W, L, H)), False)
        b.put("glass", cyl((cx, cy, 0.2), (cx, cy, H - 0.2), 0.5, 32))
        b.put("emit_p", cyl((cx, cy, 0.2), (cx, cy, H - 0.2), 0.12, 24))
        for i in range(4):
            b.put("emit_s", torus((cx, cy, 0.4 + i * 0.22), (0, 0, 1), 0.3, 0.012, 32, 4))
        corner_posts(b, 0, 0, W, L, 0.2, H - 0.2, 0.06, "trim")
    elif sh == "retro":
        b.put("trim", cyl((cx, cy, 0), (cx, cy, 0.3), 0.7, b.seg, r1=0.55))
        b.put("glass", sphere((cx, cy, 0.85), 0.55, b.seg, 12))
        b.put("emit_p", ico((cx, cy, 0.85), 0.2, 1, jitter=0.3, seed=3), False)
        for i in range(3):
            b.put("armor", torus((cx, cy, 0.5 + i * 0.35), (0, 0, 1), 0.58 - abs(i - 1) * 0.12, 0.03, b.seg, 6))
        b.put("metal", cyl((cx, cy, 1.35), (cx, cy, 1.6), 0.04, 8))
        b.put("emit_p", sphere((cx, cy, 1.62), 0.06, 10, 5))
    elif sh == "nasa":
        b.put("foil", cyl((cx, cy, 0.0), (cx, cy, H), 0.36, b.seg))
        for i in range(8):
            a = math.radians(i * 45)
            d = Vector((math.cos(a), math.sin(a), 0))
            v0 = Vector((cx, cy, 0.1)) + d * 0.34
            fv, ff = box((-0.015, 0, 0), (0.015, 0.38, H - 0.2))
            M = Matrix.Translation(v0) @ Matrix.Rotation(a - math.pi / 2, 4, "Z")
            b.put("hull", xf((fv, ff), M), False)
        b.put("dark", cyl((cx, cy, H), (cx, cy, H + 0.05), 0.2, b.seg))
        b.put("emit_p", cyl((cx, cy, H + 0.05), (cx, cy, H + 0.06), 0.12, b.seg))
    elif sh == "organic":
        b.put("emit_p", sphere((cx, cy, H * 0.48), 0.42, 16, 8, (1, 1, 1.15)))
        for i in range(6):
            a = math.radians(i * 60)
            x, y = cx + math.cos(a) * 0.6, cy + math.sin(a) * 0.6
            b.put("hull", cyl((x, y, 0.0), (cx + math.cos(a) * 0.2, cy + math.sin(a) * 0.2, H), 0.08, 8, r1=0.03))
        b.put("hull2", sphere((cx, cy, 0.1), 1.0, 12, 6, (0.7, 0.7, 0.18)))
    else:  # lowpoly
        b.put("trim", prism([(cx + math.cos(a) * 0.72, cy + math.sin(a) * 0.72) for a in [i * math.pi / 3 for i in range(6)]], 0, 0.2), False)
        b.put("trim", prism([(cx + math.cos(a) * 0.72, cy + math.sin(a) * 0.72) for a in [i * math.pi / 3 for i in range(6)]], H - 0.2, H), False)
        b.put("emit_p", ico((cx, cy, H / 2), 0.42, 0, scale=(1, 1, 1.3)), False)
        for i in range(6):
            a = i * math.pi / 3
            x, y = cx + math.cos(a) * 0.66, cy + math.sin(a) * 0.66
            b.put("hull2", box((x - 0.05, y - 0.05, 0.2), (x + 0.05, y + 0.05, H - 0.2)), False)


def shield(b):
    W, L, H = b.W, b.L, b.H
    cx, cy = W / 2, L / 2
    sh = b.shape
    if sh == "industrial":
        b.put("hull2", box((0.05, 0.05, 0), (W - 0.05, L - 0.05, 0.3)), False)
        b.put("metal", cyl((cx, cy, 0.3), (cx, cy, 0.55), 0.08, 12))
        b.put("trim", lathe([(0.05, 0), (0.25, 0.05), (0.42, 0.2), (0.45, 0.24)], (cx, cy, 0.5), (0, 0, 1), 20, True, False))
        b.put("emit_s", sphere((cx, cy, 0.62), 0.07, 12, 6))
        for a in (0, 120, 240):
            r = math.radians(a)
            b.put("metal", cyl((cx + math.cos(r) * 0.4, cy + math.sin(r) * 0.4, 0.72), (cx, cy, 0.9), 0.012, 6))
        b.put("emit_s", sphere((cx, cy, 0.9), 0.03, 8, 4))
    elif sh == "sleek":
        b.put("hull", box((0.1, 0.1, 0), (W - 0.1, L - 0.1, 0.18)), False)
        b.put("emit_s", torus((cx, cy, 0.55), (0, 0, 1), 0.38, 0.03, 32, 6))
        b.put("emit_s", torus((cx, cy, 0.55), (1, 0, 0), 0.3, 0.015, 32, 4))
        b.put("trim", cyl((cx, cy, 0.18), (cx, cy, 0.5), 0.04, 12))
        lens(b, (cx, cy, 0.55), 0.09, "emit_s")
    elif sh == "retro":
        b.put("trim", cyl((cx, cy, 0), (cx, cy, 0.2), 0.45, b.seg, r1=0.35))
        b.put("metal", cyl((cx, cy, 0.2), (cx, cy, 0.85), 0.05, 10))
        for i in range(3):
            b.put("armor", torus((cx, cy, 0.35 + i * 0.16), (0, 0, 1), 0.28 - i * 0.07, 0.025, b.seg, 6))
        b.put("emit_s", sphere((cx, cy, 0.92), 0.12, 14, 7))
    elif sh == "nasa":
        b.put("hull", box((0.05, 0.05, 0), (W - 0.05, L - 0.05, 0.12)), False)
        b.put("trim", box((cx - 0.03, cy - 0.03, 0.12), (cx + 0.03, cy + 0.03, 0.4)), False)
        v, f = box((-0.4, -0.02, -0.35), (0.4, 0.02, 0.35))
        M = Matrix.Translation((cx, cy, 0.6)) @ Matrix.Rotation(math.radians(-30), 4, "X")
        b.put("solar", xf((v, f), M), False)
        for i in range(4):
            for j in range(4):
                p = M @ Vector((-0.3 + i * 0.2, -0.025, -0.26 + j * 0.17))
                b.put("emit_s", cyl(tuple(p), tuple(p + (M.to_3x3() @ Vector((0, -0.01, 0)))), 0.03, 6))
    elif sh == "organic":
        b.put("hull", sphere((cx, cy, 0.15), 1.0, 12, 6, (0.45, 0.45, 0.2)))
        for i in range(7):
            a = math.radians(i * 360 / 7)
            tip = (cx + math.cos(a) * 0.32, cy + math.sin(a) * 0.32, 0.75 + 0.1 * hash01(i))
            b.put("hull2", cyl((cx + math.cos(a) * 0.15, cy + math.sin(a) * 0.15, 0.2), tip, 0.05, 6, r1=0.015))
            b.put("emit_s", sphere(tip, 0.04, 8, 4))
    else:
        b.put("hull2", box((0.05, 0.05, 0), (W - 0.05, L - 0.05, 0.2)), False)
        cv = [(0.15, 0.15, 0.2), (W - 0.15, 0.15, 0.2), (W - 0.15, L - 0.15, 0.2), (0.15, L - 0.15, 0.2), (cx, cy, 0.85)]
        b.put("hull", (cv, [(0, 1, 4), (1, 2, 4), (2, 3, 4), (3, 0, 4)]), False)
        b.put("emit_s", ico((cx, cy, 0.92), 0.1, 0), False)


def capacitor(b):
    W, L, H = b.W, b.L, b.H
    sh = b.shape
    b.put("trim", box((0, 0, 0), (W, L, 0.1)), False)
    for i in range(2):
        for j in range(2):
            x, y = 0.25 + i * 0.5, 0.25 + j * 0.5
            if sh == "lowpoly":
                b.put("hull2", prism([(x + math.cos(a) * 0.19, y + math.sin(a) * 0.19) for a in [k * math.pi / 3 for k in range(6)]], 0.1, H - 0.1), False)
                b.put("emit_p", prism([(x + math.cos(a) * 0.2, y + math.sin(a) * 0.2) for a in [k * math.pi / 3 for k in range(6)]], 0.5, 0.56), False)
            elif sh == "organic":
                b.put("hull", sphere((x, y, 0.5), 1.0, 10, 6, (0.2, 0.2, 0.38)))
                b.put("emit_p", sphere((x, y, 0.62), 0.1, 8, 4))
            else:
                b.put("hull2" if sh != "nasa" else "foil", cyl((x, y, 0.1), (x, y, H - 0.12), 0.19, b.seg))
                for z in (0.35, 0.6):
                    b.put("emit_p", cyl((x, y, z), (x, y, z + 0.04), 0.195, b.seg))
                b.put("metal", cyl((x, y, H - 0.12), (x, y, H - 0.02), 0.08, 10))
    if sh in ("industrial", "nasa"):
        b.put("metal", cyl((0.25, 0.25, H - 0.05), (0.75, 0.75, H - 0.05), 0.02, 6))
        b.put("metal", cyl((0.75, 0.25, H - 0.05), (0.25, 0.75, H - 0.05), 0.02, 6))


def cargo(b):
    W, L, H = b.W, b.L, b.H
    sh = b.shape
    if sh == "nasa":
        b.put("hull", cyl((W / 2, 0.1, H / 2), (W / 2, L - 0.1, H / 2), W * 0.47, b.seg))
        b.put("hull", sphere((W / 2, 0.1, H / 2), W * 0.47, b.seg, 8, (1, 0.4, 1)))
        b.put("hull", sphere((W / 2, L - 0.1, H / 2), W * 0.47, b.seg, 8, (1, 0.4, 1)))
        b.put("accent", cyl((W / 2, L * 0.3, H / 2), (W / 2, L * 0.33, H / 2), W * 0.48, b.seg))
        return
    if sh == "organic":
        b.put("hull2", sphere((W / 2, L / 2, H / 2), 1.0, 12, 6, (W * 0.48, L * 0.48, H * 0.46)))
        for i in range(3):
            b.put("hull", torus((W / 2, 0.4 + i * 0.6, H / 2), (0, 1, 0), W * 0.45, 0.04, 12, 6))
        return
    tag = "hull2" if sh in ("industrial", "lowpoly") else "accent"
    b.put(tag, box((0.02, 0.02, 0.02), (W - 0.02, L - 0.02, H - 0.02)), False)
    if sh != "lowpoly":
        n = 9
        for i in range(n):
            y = 0.1 + i * (L - 0.2) / (n - 1)
            for x0, x1 in ((-0.005, 0.03), (W - 0.03, W + 0.005)):
                b.put(tag, box((x0, y - 0.025, 0.06), (x1, y + 0.025, H - 0.06)), False)
    corner_posts(b, 0, 0, W, L, 0, H, 0.06, "trim")
    b.put("trim", box((0, 0, H - 0.06), (W, 0.06, H)), False)
    b.put("trim", box((0, L - 0.06, H - 0.06), (W, L, H)), False)
    if sh == "industrial":
        b.put("accent", box((0.1, -0.01, H * 0.4), (W - 0.1, 0.0, H * 0.6)), False)


def radiator(b):
    W, L, H = b.W, b.L, b.H
    sh = b.shape
    b.put("trim", box((0, 0, 0), (W, L, 0.12)), False)
    if sh == "organic":
        pts = [(0.1, 0.1), (L - 0.1, 0.1), (L * 0.75, H * 0.9), (L * 0.3, H)]
        b.put("hull2", fin(pts, 0.05, W / 2), True)
        for i in range(5):
            y = 0.4 + i * 0.5
            b.put("emit_p", cyl((W / 2, y, 0.12), (W / 2, y + 0.1, H * 0.85), 0.03, 6))
        return
    b.put("hull" if sh in ("nasa", "sleek") else "metal", box((W / 2 - 0.03, 0.05, 0.12), (W / 2 + 0.03, L - 0.05, H - 0.05)), False)
    n = 10
    for i in range(n):
        y = 0.12 + i * (L - 0.24) / (n - 1)
        b.put("trim" if sh != "nasa" else "dark", box((W / 2 - 0.05, y - 0.015, 0.12), (W / 2 + 0.05, y + 0.015, H - 0.05)), False)
    if sh in ("industrial", "retro"):
        b.put("ember", box((W / 2 - 0.035, 0.05, H - 0.08), (W / 2 + 0.035, L - 0.05, H - 0.05)), False)
    if sh == "sleek":
        b.put("emit_s", box((W / 2 - 0.035, 0.05, H - 0.07), (W / 2 + 0.035, L - 0.05, H - 0.05)), False)


def sensor(b):
    W, L, H = b.W, b.L, b.H
    cx, cy = W / 2, L / 2
    sh = b.shape
    b.put("trim", box((0.1, 0.1, 0), (W - 0.1, L - 0.1, 0.15)), False)
    if sh == "organic":
        b.put("hull", cyl((cx, cy, 0.1), (cx + 0.1, cy, H * 0.8), 0.1, 8, r1=0.05))
        b.put("glass", sphere((cx + 0.1, cy, H * 0.85), 0.16, 12, 6))
        b.put("emit_s", sphere((cx + 0.1, cy + 0.12, H * 0.85), 0.06, 8, 4))
        return
    b.put("metal", cyl((cx, cy, 0.15), (cx, cy, H * 0.65), 0.04, 8))
    seg = b.seg if sh != "lowpoly" else 6
    tilt = Matrix.Translation((cx, cy, H * 0.65)) @ Matrix.Rotation(math.radians(35), 4, "X")
    prof = [(0.0, 0.0), (0.18, 0.02), (0.36, 0.08), (0.45, 0.14)] if sh != "retro" else [(0.0, 0.0), (0.25, 0.02), (0.5, 0.1), (0.6, 0.18)]
    v, f = lathe(prof, (0, 0, 0), (0, 0, 1), seg, False, False)
    b.put("hull" if sh != "industrial" else "hull2", xf((v, f), tilt), sh != "lowpoly")
    v2, f2 = cyl((0, 0, 0), (0, 0, 0.32), 0.012, 6)
    b.put("metal", xf((v2, f2), tilt))
    b.put("emit_s", xf(sphere((0, 0, 0.33), 0.03, 8, 4), tilt))
    if sh in ("industrial", "nasa"):
        b.put("hull", box((0.12, 0.12, 0.15), (0.42, 0.42, 0.4)), False)
        b.put("nav_r", sphere((cx, cy, H * 0.66), 0.02, 6, 3))


def solar(b):
    W, L, H = b.W, b.L, b.H
    b.put("trim", box((0, 0, 0), (W, 0.4, H)), False)
    b.put("metal", cyl((W / 2, 0.4, H / 2), (W / 2, L, H / 2), 0.03, 8))
    b.put("solar", box((W / 2 - 0.015, 0.5, 0.05), (W / 2 + 0.015, L - 0.05, H - 0.05)), False)


# ---------------------------------------------------------------------------------------------------------------------
# Cores: intricate within their footprint
# ---------------------------------------------------------------------------------------------------------------------

def core(b):
    W, L, H = b.W, b.L, b.H
    cx, cy, cz = W / 2, L / 2, H / 2
    sh = b.shape
    s = min(W, H)  # scale of the "heart"
    long = L > W * 1.3
    seg = b.seg
    t = max(0.03, 0.06 * s)
    # base and frame
    if sh == "industrial":
        b.put("trim", box((0, 0, 0), (W, L, t * 1.5)), False)
        b.put("trim", box((0, 0, H - t), (W, L, H)), False)
        corner_posts(b, 0, 0, W, L, t * 1.5, H - t, t * 1.3, "hull2")
        for x in (0, W - t * 1.3):
            for y in (0, L - t * 1.3):
                b.put("accent", box((x - 0.002, y - 0.002, H * 0.3), (x + t * 1.3 + 0.002, y + t * 1.3 + 0.002, H * 0.36)), False)
    elif sh == "sleek":
        b.put("hull", box((0, 0, 0), (W, L, t)), False)
        b.put("hull", box((0, 0, H - t), (W, L, H)), False)
        corner_posts(b, 0, 0, W, L, t, H - t, t * 0.6, "trim")
        b.put("emit_c", box((t, t, t), (W - t, L - t, t + 0.01)), False)
    elif sh == "retro":
        b.put("trim", lathe([(s * 0.5, 0), (s * 0.42, t * 2), (s * 0.3, t * 2.5)], (cx, cy, 0), (0, 0, 1), seg))
        b.put("trim", lathe([(s * 0.3, 0), (s * 0.42, -t * 0.5), (s * 0.5, -t * 2)], (cx, cy, H), (0, 0, 1), seg))
    elif sh == "nasa":
        b.put("trim", box((0, 0, 0), (W, L, t)), False)
        corner_posts(b, 0, 0, W, L, t, H, t * 0.8, "metal")
        b.put("trim", box((0, 0, H - t * 0.8), (W, L, H)), False)
    elif sh == "organic":
        b.put("hull2", sphere((cx, cy, t), 1.0, 12, 6, (W * 0.48, L * 0.48, t * 2)))
    else:
        b.put("trim", prism([(0, 0), (W, 0), (W, L), (0, L)], 0, t * 1.5), False)
        b.put("trim", prism([(0, 0), (W, 0), (W, L), (0, L)], H - t * 1.5, H), False)
        corner_posts(b, 0, 0, W, L, t * 1.5, H - t * 1.5, t * 1.4, "hull2")

    hearts = []
    if long:
        n = int(round(L / W)) + 1
        for i in range(n):
            hearts.append((cx, W * 0.5 + (L - W) * i / (n - 1), cz, s * 0.32))
    else:
        hearts.append((cx, cy, cz, s * 0.36))

    for k, (hx, hy, hz, r) in enumerate(hearts):
        if sh == "industrial":
            b.put("emit_c", sphere((hx, hy, hz), r * 0.45, seg, seg // 2))
            b.put("metal", torus((hx, hy, hz), (0, 0, 1), r * 0.85, r * 0.06, seg * 2, 6))
            b.put("hull2", torus((hx, hy, hz), (1, 0, 0), r * 0.72, r * 0.07, seg * 2, 6))
            b.put("metal", torus((hx, hy, hz), (0, 1, 0), r * 0.6, r * 0.05, seg * 2, 6))
            for a in range(4):
                ang = math.radians(45 + 90 * a)
                b.put("foil", cyl((hx + math.cos(ang) * r * 1.3, hy + math.sin(ang) * r * 1.3, t * 1.5),
                                  (hx + math.cos(ang) * r * 0.5, hy + math.sin(ang) * r * 0.5, hz - r * 0.4), r * 0.05, 6))
        elif sh == "sleek":
            b.put("emit_c", ico((hx, hy, hz), r * 0.42, 0, scale=(0.7, 0.7, 1.4)), False)
            for i, ax in enumerate(((0, 0, 1), (1, 0, 0.3), (0.3, 1, 0))):
                b.put("emit_s" if i == 0 else "metal", torus((hx, hy, hz), ax, r * (0.95 - i * 0.12), r * 0.02, 48, 4))
            b.put("glass", sphere((hx, hy, hz), r * 0.75, 24, 12))
        elif sh == "retro":
            b.put("glass", sphere((hx, hy, hz), r * 0.9, seg, 10))
            b.put("emit_c", ico((hx, hy, hz), r * 0.35, 1, jitter=0.35, seed=k), False)
            b.put("metal", cyl((hx, hy, hz - r * 0.9), (hx, hy, hz - r * 0.3), r * 0.04, 6))
            for i in range(2):
                b.put("armor", torus((hx, hy, hz), (1, 0.4 * (1 - 2 * i), 0), r * 1.0, r * 0.05, seg, 6))
        elif sh == "nasa":
            # control-moment gyroscope: flywheel in a gimbal
            b.put("foil", cyl((hx, hy, hz - r * 0.18), (hx, hy, hz + r * 0.18), r * 0.7, seg))
            b.put("metal", torus((hx, hy, hz), (1, 0, 0), r * 0.95, r * 0.05, seg * 2, 6))
            b.put("hull", cyl((hx - r * 1.05, hy, hz), (hx + r * 1.05, hy, hz), r * 0.06, 8))
            b.put("emit_c", cyl((hx, hy, hz + r * 0.18), (hx, hy, hz + r * 0.2), r * 0.3, seg))
            for i in range(4):
                b.put("hull", box((t, hy - r * 0.6 + i * r * 0.32, t), (t + r * 0.25, hy - r * 0.6 + i * r * 0.32 + r * 0.22, H * 0.7)), False)
                b.put("emit_c", box((t + r * 0.25, hy - r * 0.55 + i * r * 0.32, H * 0.5), (t + r * 0.26, hy - r * 0.5 + i * r * 0.32, H * 0.52)), False)
        elif sh == "organic":
            for i in range(6):
                a = math.radians(i * 60 + k * 20)
                d = Vector((math.cos(a), math.sin(a), 0))
                c = Vector((hx, hy, hz - r * 0.2)) + d * r * 0.55
                M = Matrix.Translation(c) @ Matrix.Rotation(a, 4, "Z") @ Matrix.Rotation(math.radians(-25), 4, "Y")
                b.put("hull", xf(sphere((0, 0, 0), 1.0, 10, 6, (r * 0.18, r * 0.35, r * 0.9)), M))
            b.put("emit_c", sphere((hx, hy, hz), r * 0.4, 16, 8))
            b.put("glass", sphere((hx, hy, hz), r * 0.5, 16, 8))
        else:
            b.put("emit_c", ico((hx, hy, hz), r * 0.55, 0, scale=(0.8, 0.8, 1.25)), False)
            for i in range(5):
                a = math.radians(i * 72 + k * 30)
                p = (hx + math.cos(a) * r * 0.95, hy + math.sin(a) * r * 0.95, hz + (hash01(i, k) - 0.5) * r * 0.6)
                b.put("hull2", ico(p, r * 0.12, 0), False)
    if long and sh in ("industrial", "nasa", "lowpoly", "sleek"):
        b.put("metal", cyl((cx, t, cz), (cx, L - t, cz), s * 0.03, 8))


BUILDERS = {
    "cockpit": cockpit, "autocannon": autocannon, "railgun": railgun, "missile": missile, "beam": beam, "flak": flak,
    "thruster_s": lambda b: thruster(b, False), "thruster_l": lambda b: thruster(b, True), "reactor": reactor,
    "shield": shield, "capacitor": capacitor, "cargo": cargo, "radiator": radiator, "sensor": sensor, "solar": solar,
    "core_1": core, "core_2": core, "core_3": core, "core_35": core, "girder": girder,
}


def build_block(kind, S, acc, group, M, size=None):
    size = size or KIT[kind]
    b = B(S, acc, group, M, size)
    if kind.startswith("hull_"):
        hull_piece(b, kind)
        return b, None
    res = BUILDERS[kind](b)
    return b, res
