"""Block kit for the look-development scenes. Each block is built in local metres: footprint box from (0,0,0) to
(sx,sy,sz)*CELL, +Y = front (barrels), -Y = exhaust, +Z = top. Each finalist builds its own form inside the same
footprint, the way an enchanting table fills one Minecraft block with detail:

  industrial  DRYDOCK   chamfered steel, bolts, hazard paint, pipes, work lamps
  gothic      NOCTURNE  ribbed, hunched, cathedral-industrial; hoses, slits of sodium light, red strobes
  opera       OPERA     sculpted capsules and spheres, two-tone paint, chrome and gold, jewel-like cores
"""

import math

import bmesh
from mathutils import Matrix, Vector

from ld_geom import (CELL, box, cbox, cyl, lathe, tube, torus, sphere, ico, prism, fin, merge, xf, T, R, S as SC,
                     hash01, mesh_cells, CUBE_FACES, clip_poly, cap_poly)

KIT = {
    # structure
    "hull_cube": (1, 1, 1), "hull_wedge": (1, 1, 1), "hull_corner": (1, 1, 1), "hull_inner": (1, 1, 1),
    "hull_slope2": (1, 2, 1), "hull_round": (1, 1, 1),
    "hull_window": (1, 1, 1), "hull_hatch": (1, 1, 1), "hull_vent": (1, 1, 1), "hull_pipes": (1, 1, 1),
    "hull_armor": (1, 1, 1), "girder": (1, 1, 1),
    # weapons
    "autocannon": (2, 2, 2), "gatling": (2, 3, 2), "railgun": (2, 6, 2), "heavy_cannon": (4, 6, 3), "mass_driver": (3, 12, 3),
    "beam": (2, 4, 2), "tesla": (2, 2, 3), "plasma": (3, 3, 3), "torpedo": (3, 6, 2), "missile": (3, 3, 1), "flak": (3, 3, 3),
    "harpoon": (2, 4, 2),
    # modules
    "cockpit": (4, 4, 3), "thruster_s": (2, 3, 2), "thruster_l": (4, 6, 4), "reactor": (3, 3, 3), "shield": (2, 2, 2),
    "capacitor": (2, 2, 2), "cargo": (2, 4, 2), "radiator": (1, 6, 4), "sensor": (2, 2, 3), "solar": (1, 8, 4),
    # cores
    "core_1": (1, 1, 1), "core_2": (2, 2, 2), "core_3": (3, 3, 3), "core_35": (3, 5, 3),
}

LABELS = {
    "hull_cube": "Hull", "hull_wedge": "Hull slope", "hull_corner": "Hull corner", "hull_inner": "Inner corner",
    "hull_slope2": "Long slope 1×2", "hull_round": "Rounded edge", "hull_window": "Hull · windows",
    "hull_hatch": "Hull · hatch & latches", "hull_vent": "Hull · vents", "hull_pipes": "Hull · pipes & conduit",
    "hull_armor": "Heavy armour", "girder": "Girder frame",
    "autocannon": "Autocannon", "gatling": "Rotary cannon", "railgun": "Railgun", "heavy_cannon": "Heavy twin cannon",
    "mass_driver": "Spinal mass driver", "beam": "Laser lance", "tesla": "Arc projector", "plasma": "Plasma mortar",
    "torpedo": "Torpedo launcher", "missile": "Missile cells", "flak": "Flak turret", "harpoon": "Salvage harpoon",
    "cockpit": "Cockpit", "thruster_s": "Thruster", "thruster_l": "Main engine", "reactor": "Reactor",
    "shield": "Shield projector", "capacitor": "Capacitor bank", "cargo": "Cargo pod", "radiator": "Radiator",
    "sensor": "Sensor mast", "solar": "Solar wing", "core_1": "Core", "core_2": "Core", "core_3": "Core", "core_35": "Core",
}

CATEGORY = {
    "Structure": ["hull_cube", "hull_wedge", "hull_corner", "hull_inner", "hull_slope2", "hull_round", "hull_armor", "girder"],
    "Procedural hull variants": ["hull_window", "hull_hatch", "hull_vent", "hull_pipes"],
    "Weapons": ["autocannon", "gatling", "railgun", "beam", "harpoon", "tesla", "missile", "flak", "plasma", "torpedo",
                "heavy_cannon", "mass_driver"],
    "Modules": ["cockpit", "thruster_s", "thruster_l", "reactor", "shield", "capacitor", "cargo", "radiator", "sensor", "solar"],
    "Cores": ["core_1", "core_2", "core_3", "core_35"],
}


class B:
    def __init__(self, S, acc, group, M, size):
        self.S, self.acc, self.group, self.M = S, acc, group, M
        self.shape = S["shape"]
        self.F = {"industrial": "ind", "gothic": "goth", "opera": "opera"}.get(self.shape, "ind")
        self.seg = S["seg"]
        self.size = size
        self.W, self.L, self.H = size[0] * CELL, size[1] * CELL, size[2] * CELL
        self.remap = {}

    def put(self, tag, vf, smooth=True):
        self.acc.add(self.group, self.remap.get(tag, tag), vf, self.M, smooth)

    def flat(self, tag, vf):
        self.acc.add(self.group, self.remap.get(tag, tag), vf, self.M, False)


# ======================================================================================================================
# geometry helpers
# ======================================================================================================================

def hull_pts(points):
    """Convex hull of points as (verts, faces) with coplanar triangles merged."""
    bm = bmesh.new()
    vs = [bm.verts.new(p) for p in points]
    res = bmesh.ops.convex_hull(bm, input=vs)
    loose = [v for v in bm.verts if not v.link_faces]
    if loose:
        bmesh.ops.delete(bm, geom=loose, context="VERTS")
    bmesh.ops.dissolve_limit(bm, angle_limit=math.radians(0.5), verts=bm.verts, edges=bm.edges)
    bm.verts.index_update()
    verts = [tuple(v.co) for v in bm.verts]
    faces = [tuple(v.index for v in f.verts) for f in bm.faces]
    bm.free()
    return verts, faces


def chbox(mn, mx, c=0.04, top=(0.0, 0.0), bot=(0.0, 0.0)):
    """Box with chamfered edges. top/bot = (inset x, inset y) of the top/bottom face for tapered housings."""
    x0, y0, z0 = mn
    x1, y1, z1 = mx
    pts = []
    for X, sx in ((x0, -1), (x1, 1)):
        for Y, sy in ((y0, -1), (y1, 1)):
            for Z, sz in ((z0, -1), (z1, 1)):
                ix, iy = top if sz > 0 else bot
                Xc, Yc = X - sx * ix, Y - sy * iy
                pts += [(Xc, Yc - sy * c, Z - sz * c), (Xc - sx * c, Yc, Z - sz * c), (Xc - sx * c, Yc - sy * c, Z)]
    return hull_pts(pts)


def capsule(p0, p1, r, seg=16, rings=6, r1=None):
    """Cylinder with hemispherical ends along p0->p1 (r1: radius at p1 end)."""
    a, b = Vector(p0), Vector(p1)
    L = (b - a).length
    r1 = r if r1 is None else r1
    prof = []
    for i in range(rings + 1):
        t = math.pi / 2 * i / rings
        prof.append((math.sin(t) * r, r - math.cos(t) * r))
    for i in range(rings + 1):
        t = math.pi / 2 * i / rings
        prof.append((math.cos(t) * r1, L - r1 + math.sin(t) * r1))
    prof[0] = (0.0, 0.0)
    prof[-1] = (0.0, L)
    return lathe(prof, tuple(a), tuple(b - a), seg, False, False)


def pipe_path(pts, r, seg=8):
    """Bent pipe through points: cylinders with ball joints."""
    out = []
    for i in range(len(pts) - 1):
        out.append(cyl(pts[i], pts[i + 1], r, seg))
    for p in pts[1:-1]:
        out.append(sphere(p, r, seg, max(3, seg // 2)))
    return merge(*out)


def hose(b, pts, r, tag="hose", rings_tag=None, step=0.06):
    """Corrugated hose (gothic): pipe plus rings every `step`."""
    b.put(tag, pipe_path(pts, r, 10))
    if rings_tag:
        for i in range(len(pts) - 1):
            a, c = Vector(pts[i]), Vector(pts[i + 1])
            n = max(1, int((c - a).length / step))
            for k in range(n):
                p = a + (c - a) * ((k + 0.5) / n)
                b.put(rings_tag, torus(tuple(p), tuple(c - a), r * 1.05, r * 0.28, 10, 4))


def bolts(b, pts, axis=(0, 0, 1), r=0.016, h=0.012, tag="metal"):
    for p in pts:
        q = Vector(p)
        b.put(tag, cyl(tuple(q), tuple(q + Vector(axis).normalized() * h), r, 6))


def ring_bolts(b, c, axis, R_, n, r=0.014, tag="metal"):
    from ld_geom import _frame
    u, v, w = _frame(axis)
    for i in range(n):
        a = 2 * math.pi * i / n
        p = Vector(c) + (u * math.cos(a) + v * math.sin(a)) * R_
        b.put(tag, cyl(tuple(p), tuple(p + w * 0.014), r, 6))


def hazard(b, mn, mx, n=6, axis=0, tag="accent"):
    span = mx[axis] - mn[axis]
    for i in range(0, n, 2):
        a, c = list(mn), list(mx)
        a[axis] = mn[axis] + span * i / n
        c[axis] = mn[axis] + span * (i + 1) / n
        b.flat(tag, box(tuple(a), tuple(c)))


def ribs(b, mn, mx, axis, n, depth, tag="trim", thick=0.03, inset=0.0):
    """n plates perpendicular to `axis` spread across the box, protruding by `depth` beyond it in the other two axes."""
    for i in range(n):
        t = (i + 0.5) / n
        a, c = list(mn), list(mx)
        pos = mn[axis] + (mx[axis] - mn[axis]) * t
        a[axis], c[axis] = pos - thick / 2, pos + thick / 2
        for k in range(3):
            if k != axis:
                a[k] -= depth if k != 2 else (0 if inset else depth)
                c[k] += depth
        b.flat(tag, box(tuple(a), tuple(c)))


def lamp(b, p, r=0.03, tag="nav_w", cage="metal"):
    b.put(tag, sphere(p, r, 10, 5))
    if cage:
        b.put(cage, torus(p, (0, 0, 1), r * 1.25, r * 0.2, 10, 4))


def arc(b, p0, p1, seed=0, n=7, jit=0.06, r=0.008, tag="emit_s"):
    """Jagged lightning bolt between two points."""
    a, c = Vector(p0), Vector(p1)
    pts = [a]
    for i in range(1, n):
        t = i / n
        q = a + (c - a) * t + Vector(((hash01(i, seed, 1) - 0.5), (hash01(i, seed, 2) - 0.5), (hash01(i, seed, 3) - 0.5))) * jit * 2
        pts.append(q)
    pts.append(c)
    for i in range(len(pts) - 1):
        b.put(tag, cyl(tuple(pts[i]), tuple(pts[i + 1]), r, 5))


def muzzle(b, p, axis, r, F, tag="metal"):
    """Flavour muzzle at the end of a barrel."""
    d = Vector(axis).normalized()
    p = Vector(p)
    if F == "ind":
        # boxy muzzle brake with side slots
        b.put(tag, cyl(tuple(p), tuple(p + d * r * 3.0), r * 1.6, 8))
        b.put("dark", cyl(tuple(p + d * r * 0.8), tuple(p + d * r * 1.4), r * 1.65, 8))
        b.put("dark", cyl(tuple(p + d * r * 1.9), tuple(p + d * r * 2.4), r * 1.65, 8))
        b.put("dark", cyl(tuple(p + d * r * 3.0), tuple(p + d * r * 3.02), r * 0.7, 8))
    elif F == "goth":
        b.put(tag, lathe([(r, 0), (r * 1.25, r * 1.2), (r * 1.8, r * 3.2), (r * 1.9, r * 3.5)], tuple(p), tuple(d), 12, False, False))
        b.put("dark", cyl(tuple(p + d * r * 3.4), tuple(p + d * r * 3.45), r * 1.5, 12))
    else:
        b.put("gold", torus(tuple(p), tuple(d), r * 1.25, r * 0.3, 16, 6))
        b.put(tag, lathe([(r, 0), (r * 1.15, r * 1.5), (r * 0.8, r * 2.6)], tuple(p), tuple(d), 16, False, True))


def barrel(b, p0, p1, r, F, tag="metal", jacket=True):
    a, c = Vector(p0), Vector(p1)
    d = (c - a).normalized()
    L = (c - a).length
    if F == "opera":
        b.put(tag, cyl(tuple(a), tuple(c), r, 16, r1=r * 0.85))
        for t in (0.35, 0.7):
            b.put("gold", torus(tuple(a + d * L * t), tuple(d), r * 1.05, r * 0.18, 16, 4))
    else:
        b.put(tag, cyl(tuple(a), tuple(c), r, 12))
        if jacket and F == "goth":
            # perforated cooling jacket: ribbed sleeve over the rear half
            n = int(L * 0.5 / 0.05)
            for k in range(n):
                q = a + d * (0.04 + k * 0.05)
                b.put("trim", torus(tuple(q), tuple(d), r * 1.35, r * 0.28, 12, 4))
        elif jacket:
            b.put("trim", cyl(tuple(a), tuple(a + d * L * 0.3), r * 1.35, 12))
    muzzle(b, c, d, r, F, tag)


def turret_base(b, cx, cy, R_, h, F):
    if F == "opera":
        b.put("trim", lathe([(R_, 0), (R_, h * 0.6), (R_ * 0.85, h)], (cx, cy, 0), (0, 0, 1), 32, False, True))
        b.put("gold", torus((cx, cy, h * 0.6), (0, 0, 1), R_ * 1.0, 0.012, 32, 4))
    else:
        b.put("trim", cyl((cx, cy, 0), (cx, cy, h), R_, 24 if F == "ind" else 16))
        b.put("dark", cyl((cx, cy, h), (cx, cy, h + 0.012), R_ * 0.9, 24))
        ring_bolts(b, (cx, cy, h), (0, 0, 1), R_ * 0.93, 12 if F == "ind" else 8)


def gunhouse(b, mn, mx, F, tag="paint_w"):
    """The armoured body of a turret."""
    x0, y0, z0 = mn
    x1, y1, z1 = mx
    w, l, h = x1 - x0, y1 - y0, z1 - z0
    if F == "ind":
        b.flat(tag, chbox(mn, mx, min(w, l, h) * 0.14, top=(w * 0.08, l * 0.12)))
        hazard(b, (x0 + w * 0.1, y0 - 0.004, z0 + h * 0.15), (x1 - w * 0.1, y0, z0 + h * 0.32), 8)
        bolts(b, [(x0 + w * 0.2 + i * w * 0.2, y1 - l * 0.25, z1) for i in range(4)])
    elif F == "goth":
        b.flat(tag, chbox(mn, mx, min(w, l, h) * 0.1, top=(w * 0.18, l * 0.05)))
        ribs(b, (x0 + w * 0.05, y0 + l * 0.08, z0), (x1 - w * 0.05, y1 - l * 0.2, z1), 1, max(3, int(l / 0.12)), 0.02, "trim", 0.025, inset=1)
        b.put("accent", box((x0 + w * 0.3, y0 - 0.005, z1 - h * 0.25), (x1 - w * 0.3, y0, z1 - h * 0.18)), False)
    else:
        # art-deco gunhouse: tapered chamfered body, a dorsal crest in the livery colour, chrome visor, gold pinstripe
        cx = (x0 + x1) / 2
        b.flat(tag, chbox(mn, mx, min(w, l, h) * 0.2, top=(w * 0.16, l * 0.2)))
        b.flat("trim", chbox((cx - w * 0.06, y0 + l * 0.08, z1 - 0.02), (cx + w * 0.06, y1 - l * 0.3, z1 + h * 0.12), 0.02, top=(0.0, l * 0.12)))
        b.flat("hull2", box((x0 + w * 0.12, y0 - 0.006, z0 + h * 0.36), (x1 - w * 0.12, y0 + 0.002, z0 + h * 0.46)))
        b.flat("chrome", box((x0 + w * 0.2, y1 - 0.012, z0 + h * 0.5), (x1 - w * 0.2, y1 + 0.004, z0 + h * 0.68)))
        zp = z0 + h * 0.28
        for a, c in (((x0 - 0.004, y0, zp), (x0 + 0.002, y1, zp + 0.018)), ((x1 - 0.002, y0, zp), (x1 + 0.004, y1, zp + 0.018)),
                     ((x0, y0 - 0.004, zp), (x1, y0 + 0.002, zp + 0.018)), ((x0, y1 - 0.002, zp), (x1, y1 + 0.004, zp + 0.018))):
            b.flat("gold", box(a, c))


# ======================================================================================================================
# Hull pieces (single cells, with optional greeble faces)
# ======================================================================================================================

def hull_piece(b, kind):
    cells = {(0, 0, 0): "hull"}
    shapes = {}
    if kind == "hull_wedge":
        shapes[(0, 0, 0)] = ("slope", ((0, 1, 0), (0, 0, 1)))
    elif kind == "hull_corner":
        # outer corner: where a slope along x meets a slope along y (the intersection of two wedges)
        shapes[(0, 0, 0)] = ("clip", (((0, 1, 1), 1.0), ((1, 0, 1), 1.0)))
    elif kind == "hull_inner":
        shapes[(0, 0, 0)] = ("clip", (((1, 1, 1), 2.0),))
    elif kind == "hull_slope2":
        cells = {(0, 0, 0): "hull", (0, 1, 0): "hull"}
        # plane z <= 1 - y/2 across both cells (cell 1 local: z <= 0.5 - y/2)
        shapes[(0, 0, 0)] = ("clip", (((0, 0.5, 1), 1.0),))
        shapes[(0, 1, 0)] = ("clip", (((0, 0.5, 1), 0.5),))
    elif kind == "hull_round":
        n = 10
        prof = [(0.0, 0.0)] + [(math.sin(math.pi / 2 * i / n) * CELL, math.cos(math.pi / 2 * i / n) * CELL) for i in range(n + 1)]
        v, f = fin(prof[::-1], CELL, CELL / 2)
        b.put("hull", (v, f), True)
        return
    tag = "armor" if kind == "hull_armor" else "hull"
    out, exposed = mesh_cells({k: tag for k in cells}, shapes)
    for t, vf in out.items():
        b.flat(t, vf)
    if kind == "hull_armor":
        F = b.F
        if F == "ind":
            b.flat("armor", chbox((0.025, 0.025, CELL), (CELL - 0.025, CELL - 0.025, CELL + 0.06), 0.02))
            b.flat("armor", chbox((0.025, CELL, 0.025), (CELL - 0.025, CELL + 0.06, CELL - 0.025), 0.02))
            bolts(b, [(x, y, CELL + 0.06) for x in (0.06, CELL - 0.06) for y in (0.06, CELL - 0.06)])
            hazard(b, (0.03, CELL + 0.06, CELL - 0.12), (CELL - 0.03, CELL + 0.065, CELL - 0.05), 6)
        elif F == "goth":
            for i in range(5):
                x = 0.05 + i * 0.1
                b.flat("armor", box((x - 0.02, 0.0, CELL), (x + 0.02, CELL, CELL + 0.07)))
            b.flat("trim", box((0.0, -0.01, CELL * 0.7), (CELL, CELL + 0.01, CELL * 0.75)))
        else:
            b.flat("armor", chbox((0.05, 0.05, CELL), (CELL - 0.05, CELL - 0.05, CELL + 0.06), 0.03, top=(0.04, 0.04)))
            b.put("gold", torus((CELL / 2, CELL / 2, CELL + 0.01), (0, 0, 1), CELL * 0.36, 0.012, 32, 4))
            b.put("gold", sphere((CELL / 2, CELL / 2, CELL + 0.07), 0.03, 12, 6))
    greeble_kind = {"hull_window": "window", "hull_hatch": "hatch", "hull_vent": "vent", "hull_pipes": "pipe"}.get(kind)
    if greeble_kind:
        faces = ((0, 1, 0), (1, 0, 0)) if greeble_kind == "window" else ((0, 0, 1), (0, 1, 0), (1, 0, 0))
        for face in faces:
            greeble_face(b.S, b.acc, b.group, b.M, (0, 0, 0), face, greeble_kind, 0.37, run=(0, 1))


def girder(b):
    L = CELL
    F = b.F
    if F == "opera":
        t = 0.03
        for x in (t, L - t):
            for z in (t, L - t):
                b.put("chrome", cyl((x, 0, z), (x, L, z), t, 10))
        for y in (t, L - t):
            b.put("chrome", torus((L / 2, y, L / 2), (0, 1, 0), L / 2 - t, t * 0.8, 16, 6))
        b.put("gold", cyl((t, t, t), (L - t, L - t, L - t), 0.012, 6))
        return
    t = 0.05 if F == "ind" else 0.045
    tag = "trim" if F == "ind" else "metal"
    for x in (0, L - t):
        for y in (0, L - t):
            b.flat(tag, box((x, y, 0), (x + t, y + t, L)))
        for z in (0, L - t):
            b.flat(tag, box((x, 0, z), (x + t, L, z + t)))
    for y in (0, L - t):
        for z in (0, L - t):
            b.flat(tag, box((0, y, z), (L, y + t, z + t)))
    if F == "ind":
        for (a, c) in (((t, t, t), (L - t, t, L - t)), ((t, L - t, L - t), (L - t, L - t, t))):
            b.put("accent", cyl(a, c, 0.014, 6))
            b.put("accent", cyl((a[0], L - t, a[2]), (c[0], L - t, c[2]), 0.014, 6))
    else:
        # pointed arches on the sides: the cathedral truss
        for x in (0.0, L - 0.02):
            pts = [(0.05 + 0.4 * math.sin(math.pi * i / 10) * 0 + (L - 0.1) * i / 10, 0.05 + (L * 0.75) * math.sin(math.pi * i / 10) ** 0.6) for i in range(11)]
            for i in range(10):
                (y0, z0), (y1, z1) = pts[i], pts[i + 1]
                b.put("metal", cyl((x + 0.01, y0, z0), (x + 0.01, y1, z1), 0.012, 6))


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
    Fm = M @ frame_matrix(c, n, u, v)
    h = CELL / 2
    F = {"industrial": "ind", "gothic": "goth", "opera": "opera"}.get(S["shape"], "ind")

    def put(tag, vf, smooth=False):
        acc.add(group, tag, vf, Fm, smooth)

    if kind == "plate":
        put("hull2" if rnd < 0.5 else "armor", box((-h + 0.035, -h + 0.035, 0), (h - 0.035, h - 0.035, 0.022)))
    elif kind == "window":
        if F == "opera":
            put("gold", tube((0, 0, 0), (0, 0, 0.025), 0.12, 0.095, 24), True)
            put("window", cyl((0, 0, -0.005), (0, 0, 0.01), 0.1, 24), True)
        elif F == "goth":
            for x in (-0.09, 0.09):
                put("trim", box((x - 0.05, -0.17, 0), (x + 0.05, 0.17, 0.03)))
                put("window", box((x - 0.022, -0.14, 0.0), (x + 0.022, 0.14, 0.032)))
        else:
            put("trim", box((-h, -0.085, 0), (h, 0.085, 0.014)))
            put("window", box((-h + 0.02, -0.055, 0.0), (h - 0.02, 0.055, 0.018)))
            put("trim", box((-0.012, -0.085, 0.0), (0.012, 0.085, 0.024)))
    elif kind == "hatch":
        if F == "goth":
            put("trim", cyl((0, 0, 0), (0, 0, 0.025), 0.18, 16), True)
            put("hull2", cyl((0, 0, 0.025), (0, 0, 0.035), 0.15, 16), True)
            put("metal", torus((0, 0, 0.06), (0, 0, 1), 0.07, 0.012, 16, 4), True)
            for i in range(4):
                a = math.radians(45 + 90 * i)
                put("metal", cyl((0, 0, 0.06), (math.cos(a) * 0.07, math.sin(a) * 0.07, 0.06), 0.008, 4))
            for i in range(8):
                a = math.radians(22.5 + 45 * i)
                put("metal", cyl((math.cos(a) * 0.165, math.sin(a) * 0.165, 0.025), (math.cos(a) * 0.165, math.sin(a) * 0.165, 0.04), 0.012, 6))
        elif F == "opera":
            v, f = sphere((0, 0, 0), 1.0, 24, 8)
            put("trim", ([(x * 0.2, y * 0.14, max(0, z) * 0.02) for x, y, z in v], f), True)
            put("chrome", cyl((-0.06, 0, 0.02), (0.06, 0, 0.02), 0.012, 8), True)
        else:
            put("trim", box((-0.17, -0.17, 0), (0.17, 0.17, 0.02)))
            put("hull2", box((-0.14, -0.14, 0.02), (0.14, 0.14, 0.03)))
            put("metal", box((-0.07, -0.015, 0.03), (0.07, 0.015, 0.05)))
            for sx in (-1, 1):
                for sy in (-1, 1):
                    put("metal", box((sx * 0.15 - 0.02, sy * 0.15 - 0.02, 0.0), (sx * 0.15 + 0.02, sy * 0.15 + 0.02, 0.045)))
            put("accent", box((-0.21, -0.21, 0), (-0.18, 0.21, 0.012)))
            put("accent", box((0.18, -0.21, 0), (0.21, 0.21, 0.012)))
    elif kind == "vent":
        if F == "goth":
            # deep grille: recessed frame with ribs and a dim ember glow at the back (reads as depth, not a hole)
            put("trim", box((-0.2, -0.15, 0), (0.2, -0.12, 0.05)))
            put("trim", box((-0.2, 0.12, 0), (0.2, 0.15, 0.05)))
            put("trim", box((-0.2, -0.15, 0), (-0.17, 0.15, 0.05)))
            put("trim", box((0.17, -0.15, 0), (0.2, 0.15, 0.05)))
            put("ember", box((-0.17, -0.12, 0.0), (0.17, 0.12, 0.004)))
            for i in range(7):
                x = -0.15 + i * 0.05
                put("metal", box((x - 0.008, -0.12, 0.0), (x + 0.008, 0.12, 0.045)))
        elif F == "opera":
            for i in range(3):
                y = -0.09 + i * 0.09
                put("dark", capsule((-0.15, y, 0.0), (0.15, y, 0.0), 0.022, 10, 3))
                put("chrome", torus((0, y, 0.0), (1, 0, 0), 0.03, 0.006, 8, 3))
        else:
            put("trim", box((-0.18, -0.13, 0), (0.18, 0.13, 0.01)))
            for i in range(5):
                y = -0.1 + i * 0.05
                put("metal", box((-0.16, y - 0.012, 0.0), (0.16, y + 0.012, 0.035)))
    elif kind == "pipe":
        k = 1 + int(rnd * 3)
        for i in range(k):
            off = (i - (k - 1) / 2) * 0.085
            if F == "goth":
                put("hose", cyl((-h, off, 0.045), (h, off, 0.045), 0.032, 10), True)
                for j in range(5):
                    x = -h + (j + 0.5) * CELL / 5
                    put("metal", torus((x, off, 0.045), (1, 0, 0), 0.034, 0.009, 10, 4), True)
            elif F == "opera":
                put("chrome", cyl((-h, off, 0.04), (h, off, 0.04), 0.026, 12), True)
            else:
                put("metal" if i % 2 == 0 else "foil", cyl((-h, off, 0.045), (h, off, 0.045), 0.03, 10), True)
        if run[0] % 2 == 0:
            put("trim" if F != "opera" else "gold", box((-0.03, -0.05 - (k - 1) * 0.045, 0), (0.03, 0.05 + (k - 1) * 0.045, 0.09)))
    elif kind == "light":
        put("trim", cyl((0, 0, 0), (0, 0, 0.03), 0.04, 8), True)
        put("nav_w", sphere((0, 0, 0.04), 0.03, 8, 4), True)
    elif kind == "strobe":
        put("trim", cyl((0, 0, 0), (0, 0, 0.05), 0.035, 8), True)
        put("nav_r", sphere((0, 0, 0.06), 0.03, 8, 4), True)
        put("metal", torus((0, 0, 0.06), (0, 0, 1), 0.035, 0.006, 8, 3), True)
    elif kind == "antenna":
        put("metal", cyl((0, 0, 0), (0, 0, 0.6 + rnd * 0.6), 0.012, 6))
        put("trim", cyl((0, 0, 0), (0, 0, 0.06), 0.05, 8))
        put("nav_r", sphere((0, 0, 0.62 + rnd * 0.6), 0.025, 6, 3), True)
    elif kind == "spike":
        hgt = 0.35 + rnd * 0.5
        put("metal", cyl((0, 0, 0), (0, 0, hgt), 0.035, 6, r1=0.004))
        put("trim", cyl((0, 0, 0), (0, 0, 0.05), 0.06, 6))
    elif kind == "stripe":
        for i in range(4):
            x0 = -h + i * CELL / 4
            if i % 2 == 0:
                put("accent", box((x0, -h, 0), (x0 + CELL / 4, h, 0.006)))
    elif kind == "rib":
        put("trim", box((-0.025, -h, 0), (0.025, h, 0.06)))
    elif kind == "handrail":
        for x in (-0.18, 0.18):
            put("accent", cyl((x, 0.0, 0.0), (x, 0.0, 0.07), 0.01, 6))
        put("accent", cyl((-0.2, 0.0, 0.07), (0.2, 0.0, 0.07), 0.012, 6))
    elif kind == "fairing":
        put("trim", capsule((-h + 0.06, 0, 0.0), (h - 0.06, 0, 0.0), 0.06, 12, 4))
        put("gold", torus((0, 0, 0.0), (1, 0, 0), 0.062, 0.008, 12, 3), True)


# ======================================================================================================================
# Weapons
# ======================================================================================================================

def autocannon(b):
    W, L, H, F = b.W, b.L, b.H, b.F
    cx, cy = W / 2, L / 2
    turret_base(b, cx, cy, W * 0.44, 0.12, F)
    if F == "opera":
        gunhouse(b, (cx - 0.38, cy - 0.38, 0.12), (cx + 0.38, cy + 0.32, 0.56), F)
        for s in (-1, 1):
            barrel(b, (cx + s * 0.13, cy + 0.3, 0.32), (cx + s * 0.13, L + 0.42, 0.32), 0.038, F, "chrome")
        return
    gunhouse(b, (cx - 0.36, cy - 0.36, 0.12), (cx + 0.36, cy + 0.28, 0.58), F)
    for s in (-1, 1):
        x = cx + s * 0.13
        b.put("trim", cyl((x, cy + 0.28, 0.36), (x, cy + 0.42, 0.36), 0.075, 12))
        barrel(b, (x, cy + 0.4, 0.36), (x, L + 0.3, 0.36), 0.04, F)
    if F == "ind":
        # ammo chute from the side magazine
        b.flat("paint_w", chbox((cx + 0.36, cy - 0.3, 0.16), (cx + 0.5, cy + 0.06, 0.44), 0.02))
        b.put("metal", pipe_path([(cx + 0.43, cy + 0.06, 0.4), (cx + 0.43, cy + 0.2, 0.46), (cx + 0.2, cy + 0.24, 0.5)], 0.03))
        lamp(b, (cx - 0.25, cy - 0.2, 0.6), 0.025, "nav_r")
    else:
        hose(b, [(cx - 0.36, cy - 0.2, 0.3), (cx - 0.46, cy - 0.05, 0.22), (cx - 0.44, cy + 0.25, 0.32), (cx - 0.13, cy + 0.36, 0.36)], 0.025, "hose", "metal")
        b.put("metal", cyl((cx, cy - 0.1, 0.58), (cx, cy - 0.1, 0.9), 0.012, 6, r1=0.002))


def gatling(b):
    W, L, H, F = b.W, b.L, b.H, b.F
    cx, zc = W / 2, H * 0.48
    turret_base(b, cx, 0.45, W * 0.42, 0.1, F)
    axis = (0, 1, 0)
    if F == "opera":
        gunhouse(b, (cx - 0.36, 0.0, 0.1), (cx + 0.36, 0.88, zc + 0.32), F)
    elif F == "goth":
        b.flat("paint_w", chbox((cx - 0.34, 0.0, 0.1), (cx + 0.34, 0.85, zc + 0.3), 0.06, top=(0.12, 0.05)))
        ribs(b, (cx - 0.34, 0.08, 0.12), (cx + 0.34, 0.8, zc + 0.3), 1, 6, 0.02, "trim", 0.025, inset=1)
    else:
        b.flat("paint_w", chbox((cx - 0.34, 0.0, 0.1), (cx + 0.34, 0.85, zc + 0.3), 0.06))
        # drum magazine on the side
        b.put("trim", cyl((cx + 0.34, 0.42, zc), (cx + 0.5, 0.42, zc), 0.26, 20))
        b.put("accent", cyl((cx + 0.5, 0.42, zc), (cx + 0.51, 0.42, zc), 0.18, 20))
        ring_bolts(b, (cx + 0.5, 0.42, zc), (1, 0, 0), 0.22, 8)
    # rotating barrel cluster
    y0, y1 = 0.85, L + 0.35
    b.put("metal", cyl((cx, 0.8, zc), (cx, 1.0, zc), 0.2, 16))
    for i in range(6):
        a = math.radians(i * 60 + 15)
        x, z = cx + math.cos(a) * 0.12, zc + math.sin(a) * 0.12
        b.put("chrome" if F == "opera" else "metal", cyl((x, y0, z), (x, y1, z), 0.032, 8))
        b.put("dark", cyl((x, y1, z), (x, y1 + 0.005, z), 0.02, 8))
    for t in (0.25, 0.65, 0.97):
        y = y0 + (y1 - y0) * t
        b.put("gold" if F == "opera" else "trim", cyl((cx, y, zc), (cx, y + 0.05, zc), 0.18, 16))
    if F == "goth":
        b.put("emit_w", torus((cx, y1 - 0.01, zc), axis, 0.12, 0.01, 16, 4))


def railgun(b):
    W, L, H, F = b.W, b.L, b.H, b.F
    cx, zc = W / 2, H * 0.5
    if F == "opera":
        gunhouse(b, (0.04, 0.0, 0.0), (W - 0.04, 1.2, H - 0.06), F)
        b.put("hull", cyl((cx, 1.1, zc), (cx, L + 0.4, zc), 0.2, 24, r1=0.13))
        for s in (-1, 1):
            b.put("chrome", cyl((cx + s * 0.2, 1.2, zc), (cx + s * 0.15, L + 0.2, zc), 0.025, 10))
        for i in range(7):
            y = 1.35 + i * (L - 1.0) / 7
            b.put("gold", torus((cx, y, zc), (0, 1, 0), 0.215 - i * 0.011, 0.025, 24, 6))
        b.put("emit_w", cyl((cx, L + 0.4, zc), (cx, L + 0.42, zc), 0.08, 16))
        return
    housing = "paint_w"
    b.flat(housing, chbox((0.06, 0.0, 0.0), (W - 0.06, 1.0, H - 0.08), 0.07))
    b.flat("trim", chbox((cx - 0.2, 1.0, 0.0), (cx + 0.2, L - 0.4, zc - 0.13), 0.03))
    rail_tag = "metal" if F == "ind" else "dark"
    for s in (-1, 1):
        x0 = cx + s * 0.12
        b.flat(rail_tag, box((x0 - 0.04, 0.6, zc - 0.13), (x0 + 0.04, L + 0.35, zc + 0.13)))
    b.flat("emit_w", box((cx - 0.02, 0.8, zc - 0.03), (cx + 0.02, L + 0.32, zc + 0.03)))
    n = 6
    for i in range(n):
        y = 1.15 + i * (L - 0.85) / n
        if F == "ind":
            b.flat("trim", chbox((cx - 0.26, y, zc - 0.24), (cx + 0.26, y + 0.12, zc + 0.24), 0.04))
            b.flat("accent", box((cx - 0.27, y + 0.03, zc + 0.16), (cx + 0.27, y + 0.09, zc + 0.245)))
        else:
            # vertebrae: pointed coil plates
            pts = []
            for k in range(7):
                a = math.radians(-90 + k * 60)
                r = 0.27 if k % 2 == 0 else 0.22
                pts.append((cx + math.cos(a) * r, zc + math.sin(a) * r * 1.1))
            pts.append((cx, zc + 0.42))
            v = [(x, y, z) for x, z in pts] + [(x, y + 0.07, z) for x, z in pts]
            b.flat("trim", hull_pts(v))
    if F == "ind":
        hazard(b, (0.06, -0.004, 0.12), (W - 0.06, 0.0, 0.35), 6)
        for s in (-1, 1):
            b.put("paint_p", cyl((cx + s * 0.38, 0.1, zc), (cx + s * 0.38, 0.9, zc), 0.08, 12))
    else:
        hose(b, [(cx - 0.3, 0.9, zc + 0.15), (cx - 0.38, 1.4, zc + 0.35), (cx - 0.2, 2.0, zc + 0.3)], 0.03, "hose", "metal")


def heavy_cannon(b):
    """Big naval twin turret: the silhouette-changing gun."""
    W, L, H, F = b.W, b.L, b.H, b.F
    cx, cy = W / 2, 1.2
    turret_base(b, cx, cy, W * 0.45, 0.16, F)
    zb = 0.85
    if F == "opera":
        gunhouse(b, (cx - 0.9, cy - 1.0, 0.16), (cx + 0.9, cy + 0.9, 1.15), F)
        b.put("glass", capsule((cx - 0.45, cy - 0.3, 1.05), (cx + 0.45, cy - 0.3, 1.05), 0.09, 16, 4))
        for s in (-1, 1):
            x = cx + s * 0.32
            b.put("hull", sphere((x, cy + 0.85, zb), 0.2, 20, 10))
            barrel(b, (x, cy + 0.9, zb), (x, L + 0.9, zb + 0.05), 0.085, F, "chrome")
        return
    gx0, gx1 = cx - 0.92, cx + 0.92
    gunhouse(b, (gx0, cy - 1.0, 0.16), (gx1, cy + 0.85, 1.3), F)
    for s in (-1, 1):
        x = cx + s * 0.33
        # blast bag / mantlet at the barrel root
        if F == "ind":
            b.put("hose", cyl((x, cy + 0.82, zb), (x, cy + 1.05, zb), 0.15, 12, r1=0.11))
        else:
            for k in range(4):
                b.put("hose", torus((x, cy + 0.86 + k * 0.05, zb), (0, 1, 0), 0.12 - k * 0.01, 0.03, 12, 5))
        barrel(b, (x, cy + 1.0, zb), (x, L + 0.75, zb), 0.075, F)
    if F == "ind":
        # rangefinder ears and ladder
        for s in (-1, 1):
            b.flat("trim", chbox((cx + s * 0.92 - 0.08, cy - 0.6, 0.95), (cx + s * 0.92 + 0.12 * s + 0.04, cy - 0.3, 1.1), 0.02))
            b.put("glass", cyl((cx + s * 1.0, cy - 0.3, 1.02), (cx + s * 1.0, cy - 0.29, 1.02), 0.05, 12))
        for k in range(6):
            b.put("accent", cyl((gx0 - 0.03, cy - 0.85, 0.3 + k * 0.15), (gx0 - 0.03, cy - 0.65, 0.3 + k * 0.15), 0.012, 6))
        b.flat("hull2", box((cx - 0.5, cy - 1.004, 0.6), (cx + 0.5, cy - 1.0, 0.95)))
        lamp(b, (cx + 0.6, cy - 0.7, 1.32), 0.035, "nav_r")
        b.put("metal", cyl((cx - 0.6, cy - 0.7, 1.3), (cx - 0.6, cy - 0.7, 2.0), 0.012, 6))
    else:
        # cathedral buttresses and spires
        for s in (-1, 1):
            for k in range(3):
                y = cy - 0.8 + k * 0.6
                pts = [(cx + s * 0.92, y, 0.2), (cx + s * 1.0, y, 0.2), (cx + s * 0.92, y, 1.25), (cx + s * 0.92, y + 0.08, 0.2),
                       (cx + s * 1.0, y + 0.08, 0.2), (cx + s * 0.92, y + 0.08, 1.25)]
                b.flat("trim", hull_pts(pts))
            b.put("metal", cyl((cx + s * 0.6, cy - 0.7, 1.3), (cx + s * 0.6, cy - 0.7, 2.1), 0.03, 6, r1=0.002))
        lamp(b, (cx, cy - 0.95, 1.2), 0.03, "nav_r")
        b.flat("window", box((cx - 0.4, cy - 1.003, 1.0), (cx + 0.4, cy - 1.0, 1.04)))


def mass_driver(b):
    """Spinal gun along the whole ship axis."""
    W, L, H, F = b.W, b.L, b.H, b.F
    cx, zc = W / 2, H / 2
    breech = 1.4
    if F == "opera":
        gunhouse(b, (0.0, 0.0, 0.0), (W, breech + 0.3, H), F)
        b.put("hull", cyl((cx, breech, zc), (cx, L, zc), 0.44, 32, r1=0.34))
        for s in (-1, 1):
            b.put("chrome", cyl((cx + s * 0.45, breech, zc), (cx + s * 0.36, L - 0.2, zc), 0.04, 10))
        n = 9
        for i in range(n):
            y = breech + 0.3 + i * (L - breech - 0.5) / n
            b.put("gold", torus((cx, y, zc), (0, 1, 0), 0.44 - i * 0.008, 0.05, 32, 8))
        b.put("hull", lathe([(0.34, 0), (0.5, 0.4), (0.58, 0.55)], (cx, L, zc), (0, 1, 0), 32, False, False))
        b.put("emit_w", cyl((cx, L + 0.2, zc), (cx, L + 0.22, zc), 0.25, 24))
        return
    # breech block
    b.flat("paint_w", chbox((0.0, 0.0, 0.0), (W, breech, H), 0.12))
    if F == "ind":
        hazard(b, (0.0, -0.004, 0.25), (W, 0.0, 0.55), 10)
        for s in (-1, 1):
            for k in range(3):
                b.put("paint_p", cyl((cx + s * (W / 2 + 0.06), 0.2 + k * 0.4, 0.3), (cx + s * (W / 2 + 0.06), 0.2 + k * 0.4, H - 0.3), 0.1, 12))
    # barrel: open truss (industrial) or vertebral cage (gothic)
    y0 = breech
    if F == "ind":
        for sx in (-1, 1):
            for sz in (-1, 1):
                b.flat("trim", box((cx + sx * 0.45 - 0.05, y0, zc + sz * 0.45 - 0.05), (cx + sx * 0.45 + 0.05, L, zc + sz * 0.45 + 0.05)))
        k = 0
        y = y0 + 0.2
        while y < L - 0.3:
            b.flat("accent", chbox((cx - 0.55, y, zc - 0.55), (cx + 0.55, y + 0.22, zc + 0.55), 0.06))
            b.flat("dark", box((cx - 0.4, y - 0.001, zc - 0.4), (cx + 0.4, y + 0.221, zc + 0.4)))
            if y + 0.5 < L - 0.3:
                b.put("trim", cyl((cx - 0.45, y + 0.22, zc + 0.45), (cx + 0.45, y + 0.72, zc + 0.45), 0.02, 6))
                b.put("trim", cyl((cx + 0.45, y + 0.22, zc - 0.45), (cx - 0.45, y + 0.72, zc - 0.45), 0.02, 6))
            y += 0.72
            k += 1
        b.flat("metal", box((cx - 0.06, y0, zc - 0.06), (cx + 0.06, L, zc + 0.06)))
        b.flat("emit_w", box((cx - 0.03, y0, zc + 0.06), (cx + 0.03, L, zc + 0.08)))
        b.flat("trim", chbox((cx - 0.6, L - 0.3, zc - 0.6), (cx + 0.6, L, zc + 0.6), 0.08))
        b.flat("dark", box((cx - 0.25, L - 0.001, zc - 0.25), (cx + 0.25, L + 0.002, zc + 0.25)))
    else:
        b.put("dark", cyl((cx, y0, zc), (cx, L, zc), 0.22, 16))
        b.put("emit_w", cyl((cx, y0, zc), (cx, L + 0.01, zc), 0.07, 12))
        y = y0 + 0.1
        while y < L - 0.2:
            pts = []
            for k in range(9):
                a = math.radians(-90 + k * 45)
                r = 0.62 if k % 2 == 0 else 0.5
                pts.append((cx + math.cos(a) * r, zc + math.sin(a) * r))
            pts.append((cx, zc + 0.95))
            v = [(x, y, z) for x, z in pts] + [(x, y + 0.12, z) for x, z in pts]
            b.flat("trim", hull_pts(v))
            y += 0.42
        for s in (-1, 1):
            hose(b, [(cx + s * 0.55, 0.6, H - 0.2), (cx + s * 0.62, y0 + 1.5, zc + 0.3), (cx + s * 0.4, y0 + 3.0, zc + 0.45)], 0.05, "hose", "metal")
        b.put("metal", lathe([(0.25, 0), (0.45, 0.25), (0.6, 0.5)], (cx, L - 0.3, zc), (0, 1, 0), 16, False, False))


def beam(b):
    """Laser lance."""
    W, L, H, F = b.W, b.L, b.H, b.F
    cx, zc = W / 2, H * 0.5
    if F == "opera":
        gunhouse(b, (0.04, 0.0, 0.0), (W - 0.04, 1.1, H - 0.06), F)
        b.put("chrome", cyl((cx, 1.0, zc), (cx, L - 0.15, zc), 0.11, 20, r1=0.07))
        for k in range(3):
            b.put("gold", torus((cx, 1.2 + k * 0.22, zc), (0, 1, 0), 0.115 - k * 0.012, 0.018, 20, 4))
        for i in range(4):
            a = math.radians(i * 90 + 45)
            p0 = (cx + math.cos(a) * 0.2, L - 0.5, zc + math.sin(a) * 0.2)
            b.put("gold", cyl(p0, (cx + math.cos(a) * 0.05, L + 0.1, zc + math.sin(a) * 0.05), 0.014, 6))
        b.flat("emit_w", hull_pts([(cx + math.cos(math.radians(k * 60)) * 0.08, L - 0.05, zc + math.sin(math.radians(k * 60)) * 0.08) for k in range(6)]
                               + [(cx, L + 0.18, zc), (cx, L - 0.2, zc)]))
        return
    b.flat("paint_w", chbox((0.05, 0.0, 0.0), (W - 0.05, 1.1, H - 0.08), 0.08))
    b.flat("trim", chbox((cx - 0.16, 1.1, 0.0), (cx + 0.16, L - 0.3, zc - 0.17), 0.03))
    # heat-sink fins
    for k in range(8):
        y = 0.1 + k * 0.12
        for s in (-1, 1):
            x = cx + s * (W / 2 - 0.05)
            b.flat("trim", box((min(x, x + s * 0.12), y, 0.15), (max(x, x + s * 0.12), y + 0.04, H - 0.2)))
    # telescoping focus tube
    steps = [(1.1, 0.3), (1.45, 0.24), (1.75, 0.18)]
    for i, (y, r) in enumerate(steps):
        y1 = steps[i + 1][0] if i + 1 < len(steps) else L - 0.12
        b.put("metal" if F == "ind" else "trim", cyl((cx, y, zc), (cx, y1, zc), r, 20))
        b.put("dark", torus((cx, y1, zc), (0, 1, 0), r * 0.85, 0.015, 20, 4))
    if F == "goth":
        for k in range(10):
            y = 1.15 + k * 0.08
            b.put("trim", torus((cx, y, zc), (0, 1, 0), 0.32, 0.018, 16, 4))
        b.put("dark", cyl((cx, L - 0.12, zc), (cx, L, zc), 0.2, 16))
        b.put("emit_w", cyl((cx, L - 0.06, zc), (cx, L - 0.05, zc), 0.12, 16))
    else:
        b.put("emit_w", sphere((cx, L - 0.06, zc), 0.11, 16, 8))
        b.put("trim", tube((cx, L - 0.14, zc), (cx, L - 0.05, zc), 0.19, 0.12, 20))
        hazard(b, (0.05, -0.004, 0.15), (W - 0.05, 0.0, 0.32), 6)
        for s in (-1, 1):
            b.put("paint_p", cyl((cx + s * 0.32, 0.9, zc + 0.36), (cx + s * 0.22, 1.7, zc + 0.24), 0.05, 10))


def tesla(b):
    """Arc projector."""
    W, L, H, F = b.W, b.L, b.H, b.F
    cx, cy = W / 2, L / 2
    if F == "opera":
        b.put("trim", lathe([(0.42, 0), (0.42, 0.08), (0.12, 0.25), (0.06, 0.3)], (cx, cy, 0), (0, 0, 1), 32, False, True))
        b.put("chrome", cyl((cx, cy, 0.25), (cx, cy, H - 0.45), 0.05, 16))
        for k in range(4):
            b.put("gold", torus((cx, cy, 0.45 + k * 0.18), (0, 0, 1), 0.2 - k * 0.03, 0.014, 24, 4))
        b.put("emit_s", sphere((cx, cy, H - 0.3), 0.2, 24, 12))
        b.put("glass", sphere((cx, cy, H - 0.3), 0.26, 24, 12))
        for k in range(4):
            a = math.radians(k * 90 + 30)
            arc(b, (cx, cy, H - 0.3), (cx + math.cos(a) * 0.48, cy + math.sin(a) * 0.48, H - 0.55 + 0.2 * hash01(k)), seed=k, jit=0.05)
        return
    base = "paint_w"
    b.flat(base, chbox((0.05, 0.05, 0.0), (W - 0.05, L - 0.05, 0.28), 0.06))
    # ceramic insulator stack
    n = 6 if F == "ind" else 8
    for k in range(n):
        z = 0.3 + k * 0.1
        b.put("ceramic", lathe([(0.09, 0), (0.2 - 0.01 * k, 0.03), (0.2 - 0.01 * k, 0.05), (0.09, 0.08)], (cx, cy, z), (0, 0, 1), 20, False, False))
    b.put("ceramic", cyl((cx, cy, 0.28), (cx, cy, 0.3 + n * 0.1), 0.09, 16))
    top = 0.32 + n * 0.1
    if F == "ind":
        b.put("metal", torus((cx, cy, top + 0.05), (0, 0, 1), 0.26, 0.08, 32, 12))
        b.put("metal", cyl((cx, cy, top - 0.02), (cx, cy, top + 0.08), 0.18, 20))
        for k in range(4):
            a = math.radians(k * 90 + 45)
            p = (cx + math.cos(a) * 0.45, cy + math.sin(a) * 0.45, 0.28)
            b.put("trim", cyl(p, (p[0], p[1], 0.75), 0.022, 6))
            b.put("metal", sphere((p[0], p[1], 0.77), 0.04, 10, 5))
            arc(b, (cx + math.cos(a) * 0.3, cy + math.sin(a) * 0.3, top + 0.05), (p[0], p[1], 0.77), seed=k + 3)
        hazard(b, (0.05, 0.046, 0.06), (W - 0.05, 0.05, 0.2), 8)
    else:
        # twisted crown of spikes
        for k in range(7):
            a = math.radians(k * 360 / 7)
            p0 = (cx + math.cos(a) * 0.12, cy + math.sin(a) * 0.12, top)
            p1 = (cx + math.cos(a + 0.5) * 0.3, cy + math.sin(a + 0.5) * 0.3, top + 0.35 + 0.1 * (k % 2))
            b.put("metal", cyl(p0, p1, 0.03, 6, r1=0.004))
            if k % 2 == 0:
                arc(b, p1, (cx + math.cos(a + 1.4) * 0.32, cy + math.sin(a + 1.4) * 0.32, top + 0.35), seed=k, jit=0.04, tag="emit_s")
        b.put("emit_s", sphere((cx, cy, top + 0.12), 0.07, 12, 6))


def plasma(b):
    """Plasma mortar: a containment sphere and a short fat barrel."""
    W, L, H, F = b.W, b.L, b.H, b.F
    cx, cy, cz = W / 2, L * 0.42, H * 0.5
    turret_base(b, cx, cy, W * 0.42, 0.12, F)
    R_ = 0.52
    if F == "opera":
        b.put("emit_w", sphere((cx, cy, cz), R_ * 0.75, 32, 16))
        b.put("glass", sphere((cx, cy, cz), R_ * 0.85, 32, 16))
        for ax in ((1, 0, 0), (0.5, 0, 0.86), (-0.5, 0, 0.86)):
            b.put("gold", torus((cx, cy, cz), ax, R_ * 0.98, 0.025, 48, 6))
        b.put("hull", lathe([(0.22, 0), (0.3, 0.3), (0.42, 0.6)], (cx, cy + R_ * 0.8, cz), (0, 1, 0), 24, False, False))
        b.put("paint_w", cyl((cx, cy, 0.12), (cx, cy, cz - R_ * 0.7), 0.12, 16))
        return
    b.put("paint_w", sphere((cx, cy, cz), R_, 24, 12))
    # glowing slits around the equator
    for k in range(8):
        a = math.radians(k * 45 + 22.5)
        p = Vector((cx + math.cos(a) * R_ * 0.99, cy + math.sin(a) * R_ * 0.99, cz))
        d = Vector((math.cos(a), math.sin(a), 0))
        side = Vector((-math.sin(a), math.cos(a), 0))
        v = [tuple(p + side * sx * 0.035 + Vector((0, 0, sz * 0.18)) + d * 0.005) for sx in (-1, 1) for sz in (-1, 1)]
        b.flat("emit_w", hull_pts(v + [tuple(Vector(q) - d * 0.03) for q in v]))
    b.put("trim", torus((cx, cy, cz + R_ * 0.55), (0, 0, 1), R_ * 0.82, 0.05, 32, 6))
    b.put("trim", torus((cx, cy, cz - R_ * 0.55), (0, 0, 1), R_ * 0.82, 0.05, 32, 6))
    ring_bolts(b, (cx, cy, cz + R_ * 0.6), (0, 0, 1), R_ * 0.78, 16)
    # short fat barrel with magnetic rings
    y0 = cy + R_ * 0.75
    b.put("metal", cyl((cx, y0, cz), (cx, L + 0.25, cz), 0.24, 20, r1=0.28))
    for k in range(3):
        b.put("accent" if F == "ind" else "trim", cyl((cx, y0 + 0.1 + k * 0.22, cz), (cx, y0 + 0.2 + k * 0.22, cz), 0.32, 20))
    b.put("dark", cyl((cx, L + 0.25, cz), (cx, L + 0.26, cz), 0.2, 20))
    b.put("emit_w", cyl((cx, L + 0.1, cz), (cx, L + 0.11, cz), 0.16, 20))
    if F == "goth":
        for k in range(10):
            a = math.radians(k * 36)
            b.put("trim", torus((cx, cy, cz), (math.cos(a), math.sin(a), 0), R_ * 1.02, 0.02, 32, 4))
        b.put("metal", cyl((cx, cy, cz + R_), (cx, cy, cz + R_ + 0.4), 0.04, 6, r1=0.002))
    else:
        for s in (-1, 1):
            b.put("paint_p", pipe_path([(cx + s * R_ * 0.9, cy - 0.2, cz - 0.1), (cx + s * 0.7, cy - 0.5, 0.3), (cx + s * 0.5, cy - 0.6, 0.12)], 0.05))


def torpedo(b):
    W, L, H, F = b.W, b.L, b.H, b.F
    cx, zc = W / 2, H * 0.5
    r = 0.3
    y_tip = L + 0.25

    def fish(y0, y1):
        b.put("hull2" if F != "goth" else "trim", cyl((cx, y0, zc), (cx, y1 - 0.5, zc), r, 24))
        b.put("accent", lathe([(r, 0), (r * 0.95, 0.15), (r * 0.6, 0.38), (0.0, 0.5)], (cx, y1 - 0.5, zc), (0, 1, 0), 24, False, False))
        if F == "ind":
            for k in range(4):
                b.put("dark", lathe([(r * 0.95 - k * 0.0, 0.0), (r * 0.95, 0.025)], (cx, y1 - 0.5 + k * 0.1, zc), (0, 1, 0), 24, False, False))
        for k in range(4):
            a = math.radians(45 + k * 90)
            b.flat("trim", hull_pts([(cx + math.cos(a) * r * 0.8, y0, zc + math.sin(a) * r * 0.8),
                                    (cx + math.cos(a) * r * 1.6, y0, zc + math.sin(a) * r * 1.6),
                                    (cx + math.cos(a) * r * 0.8, y0 + 0.4, zc + math.sin(a) * r * 0.8),
                                    (cx + math.cos(a) * r * 0.8 + 0.01, y0, zc + math.sin(a) * r * 0.8 + 0.01)]))

    if F == "opera":
        gunhouse(b, (0.0, 0.0, 0.0), (W, L * 0.58, H), F)
        b.put("dark", cyl((cx, L * 0.58 - 0.02, zc), (cx, L * 0.58 + 0.005, zc), 0.36, 32))
        b.put("gold", torus((cx, L * 0.58, zc), (0, 1, 0), 0.4, 0.035, 32, 6))
        fish(L * 0.45, y_tip)
        return
    if F == "ind":
        # open launch cradle: rails and frames, torpedo half loaded
        for s in (-1, 1):
            b.flat("trim", box((cx + s * 0.4 - 0.05, 0.0, 0.0), (cx + s * 0.4 + 0.05, L, 0.12)))
        for k in range(4):
            y = 0.2 + k * (L - 0.5) / 3
            b.flat("paint_w", chbox((cx - 0.55, y, 0.0), (cx + 0.55, y + 0.18, H * 0.85), 0.05))
            b.flat("dark", box((cx - 0.34, y - 0.001, zc - 0.34), (cx + 0.34, y + 0.181, zc + 0.34)))
        fish(0.15, y_tip)
        b.put("metal", pipe_path([(cx + 0.55, 0.3, H * 0.6), (cx + 0.7, 0.8, H * 0.9), (cx + 0.4, 1.3, H)], 0.035))
        hazard(b, (cx - 0.55, 0.196, 0.05), (cx + 0.55, 0.2, 0.25), 8)
    else:
        b.put("paint_w", cyl((cx, 0.0, zc), (cx, L - 0.6, zc), 0.46, 16))
        for k in range(12):
            b.put("trim", torus((cx, 0.15 + k * 0.2, zc), (0, 1, 0), 0.47, 0.03, 16, 4))
        b.put("metal", lathe([(0.46, 0), (0.55, 0.12), (0.58, 0.18)], (cx, L - 0.6, zc), (0, 1, 0), 16, False, False))
        fish(L - 1.4, y_tip)


def missile(b):
    W, L, H, F = b.W, b.L, b.H, b.F
    if F == "opera":
        b.flat("paint_w", chbox((0, 0, 0), (W, L, 0.45), 0.08, top=(0.06, 0.06)))
    else:
        b.flat("paint_w", chbox((0, 0, 0), (W, L, H * 0.85), 0.05))
    for i in range(3):
        for j in range(3):
            x = 0.25 + i * 0.5
            y = 0.25 + j * 0.5
            z = H * 0.85
            opened = hash01(i, j, 7) < 0.4
            if F == "opera":
                if (i - 1) ** 2 + (j - 1) ** 2 > 1:
                    continue
                b.put("gold", torus((x, y, 0.45), (0, 0, 1), 0.15, 0.02, 16, 4))
                b.put("dark", cyl((x, y, 0.44), (x, y, 0.46), 0.14, 16))
                b.put("accent", lathe([(0.1, 0), (0.1, 0.1), (0.0, 0.25)], (x, y, 0.45), (0, 0, 1), 16, False, False))
                continue
            b.put("dark", cyl((x, y, z - 0.01), (x, y, z + 0.012), 0.17, 16))
            if opened:
                b.put("accent", lathe([(0.11, 0), (0.11, 0.05), (0.0, 0.2)], (x, y, z), (0, 0, 1), 16, False, False))
                # lid hinged up
                lid = hull_pts([(x - 0.18, y + 0.17, z), (x + 0.18, y + 0.17, z), (x - 0.18, y + 0.2, z), (x + 0.18, y + 0.2, z),
                                (x - 0.18, y + 0.17, z + 0.34), (x + 0.18, y + 0.17, z + 0.34), (x - 0.18, y + 0.2, z + 0.34), (x + 0.18, y + 0.2, z + 0.34)])
                b.flat("trim", lid)
            else:
                b.flat("trim", box((x - 0.19, y - 0.19, z + 0.005), (x + 0.19, y + 0.19, z + 0.035)))
                if F == "ind":
                    b.flat("accent", box((x - 0.19, y - 0.03, z + 0.035), (x + 0.19, y + 0.03, z + 0.04)))
                else:
                    for k in range(3):
                        b.flat("metal", box((x - 0.16 + k * 0.13, y - 0.17, z + 0.035), (x - 0.13 + k * 0.13, y + 0.17, z + 0.06)))


def flak(b):
    W, L, H, F = b.W, b.L, b.H, b.F
    cx, cy = W / 2, L / 2
    turret_base(b, cx, cy, W * 0.46, 0.18, F)
    if F == "opera":
        gunhouse(b, (cx - 0.68, cy - 0.68, 0.18), (cx + 0.68, cy + 0.6, 0.95), F)
    else:
        gunhouse(b, (cx - 0.68, cy - 0.68, 0.18), (cx + 0.68, cy + 0.45, 1.0), F)
    for sx in (-1, 1):
        for sz in (-1, 1):
            x = cx + sx * 0.2
            z = 0.58 + sz * 0.13
            barrel(b, (x, cy + 0.4, z), (x, L + 0.45, z), 0.045, F, "chrome" if F == "opera" else "metal", jacket=False)
    if F == "ind":
        b.put("glass", box((cx + 0.3, cy - 0.35, 1.0), (cx + 0.55, cy - 0.05, 1.07)), False)
        lamp(b, (cx - 0.45, cy - 0.45, 1.02), 0.03, "nav_w")
    elif F == "goth":
        for s in (-1, 1):
            b.put("metal", cyl((cx + s * 0.45, cy - 0.4, 1.0), (cx + s * 0.5, cy - 0.45, 1.6), 0.03, 6, r1=0.003))
        b.flat("window", box((cx - 0.4, cy - 0.684, 0.82), (cx + 0.4, cy - 0.68, 0.86)))


def harpoon(b):
    """Salvage harpoon: barbed spear on a rail, cable drum behind."""
    W, L, H, F = b.W, b.L, b.H, b.F
    cx, zc = W / 2, H * 0.55
    if F == "opera":
        gunhouse(b, (0.06, 0.0, 0.0), (W - 0.06, 1.1, H - 0.1), F)
        b.flat("trim", chbox((cx - 0.08, 1.1, 0.0), (cx + 0.08, L - 0.4, zc - 0.04), 0.02))
        b.put("chrome", cyl((cx, 0.9, zc), (cx, L + 0.2, zc), 0.045, 12))
        tip = Vector((cx, L + 0.2, zc))
        for k in (-1, 0, 1):
            b.put("gold", cyl(tuple(tip + Vector((k * 0.14, -0.25, 0))), tuple(tip + Vector((k * 0.14, 0.15 if k == 0 else 0.05, 0))), 0.02, 8, r1=0.003))
        b.put("gold", cyl(tuple(tip + Vector((-0.15, -0.25, 0))), tuple(tip + Vector((0.15, -0.25, 0))), 0.02, 8))
        return
    # cable drum (winch)
    b.put("paint_w", cyl((0.12, 0.45, 0.42), (W - 0.12, 0.45, 0.42), 0.36, 20))
    b.put("hose", cyl((0.22, 0.45, 0.42), (W - 0.22, 0.45, 0.42), 0.38, 20))
    for x in (0.12, W - 0.12):
        b.flat("trim", chbox((x - 0.06, 0.0, 0.0), (x + 0.06, 0.9, 0.85), 0.03))
    # launch rail
    b.flat("trim", chbox((cx - 0.18, 0.9, zc - 0.2), (cx + 0.18, L - 0.6, zc - 0.05), 0.03))
    b.flat("trim", chbox((cx - 0.1, 1.0, 0.0), (cx + 0.1, L - 0.7, zc - 0.2), 0.02))
    b.put("metal", cyl((cx, 0.8, zc), (cx, L + 0.15, zc), 0.035, 10))
    b.put("hose", pipe_path([(cx, 0.45, 0.8), (cx, 0.75, zc + 0.05), (cx, 0.95, zc)], 0.012, 6))
    tip = Vector((cx, L + 0.15, zc))
    b.put("metal", cyl(tuple(tip), tuple(tip + Vector((0, 0.32, 0))), 0.07, 8, r1=0.0))
    for k in range(3):
        a = math.radians(90 + k * 120)
        d = Vector((math.cos(a), 0, math.sin(a)))
        b.put("metal", cyl(tuple(tip + Vector((0, 0.12, 0))), tuple(tip + d * 0.16 - Vector((0, 0.08, 0))), 0.02, 6, r1=0.004))
    if F == "ind":
        hazard(b, (cx - 0.18, 0.9, zc - 0.05), (cx + 0.18, L - 0.6, zc - 0.045), 10, axis=1)
    else:
        for k in range(3):
            a = math.radians(30 + k * 120)
            d = Vector((math.cos(a), 0, math.sin(a)))
            b.put("metal", cyl(tuple(tip + Vector((0, 0.2, 0))), tuple(tip + d * 0.22), 0.015, 6, r1=0.002))


# ======================================================================================================================
# Modules
# ======================================================================================================================

def cockpit(b):
    W, L, H, F = b.W, b.L, b.H, b.F
    if F == "ind":
        # heavy truck/excavator cab: base, cab with a raked window band, visor brow, roof lamp bar
        b.flat("hull", chbox((0, 0, 0), (W, L, H * 0.42), 0.06))
        cab = [(0.12, 0.1, H * 0.42), (W - 0.12, 0.1, H * 0.42), (W - 0.12, L - 0.1, H * 0.42), (0.12, L - 0.1, H * 0.42),
               (0.18, 0.2, H * 0.95), (W - 0.18, 0.2, H * 0.95), (W - 0.18, L - 0.75, H * 0.95), (0.18, L - 0.75, H * 0.95)]
        b.flat("hull2", hull_pts(cab))
        # window band: front rake + sides
        band = [(0.2, L - 0.12, H * 0.5), (W - 0.2, L - 0.12, H * 0.5), (0.25, L - 0.68, H * 0.86), (W - 0.25, L - 0.68, H * 0.86),
                (0.19, L - 0.13, H * 0.5), (W - 0.19, L - 0.13, H * 0.5)]
        b.flat("glass", hull_pts([(x, y + 0.012, z + 0.006) for x, y, z in band[:4]] + [(x, y - 0.02, z) for x, y, z in band[:4]]))
        for t in (0.33, 0.66):
            x = 0.2 + (W - 0.4) * t
            b.flat("trim", hull_pts([(x - 0.025, L - 0.1, H * 0.48), (x + 0.025, L - 0.1, H * 0.48), (x - 0.025, L - 0.7, H * 0.9), (x + 0.025, L - 0.7, H * 0.9),
                                     (x - 0.025, L - 0.15, H * 0.48), (x + 0.025, L - 0.15, H * 0.48)]))
        for s in (-1, 1):
            x = W / 2 + s * (W / 2 - 0.16)
            b.flat("glass", box((min(x, x + s * 0.01), 0.6, H * 0.6), (max(x, x + s * 0.01) + 0.0, L - 0.9, H * 0.82)))
        # visor brow
        b.flat("trim", chbox((0.1, L - 0.95, H * 0.93), (W - 0.1, L - 0.55, H * 1.0), 0.03))
        for k in range(5):
            lamp(b, (0.45 + k * (W - 0.9) / 4, L - 0.62, H * 1.02), 0.035, "window", None)
        hazard(b, (0.0, -0.005, H * 0.12), (W, 0.0, H * 0.32), 10)
        b.put("metal", cyl((0.3, 0.4, H * 0.95), (0.3, 0.4, H * 1.6), 0.012, 6))
        lamp(b, (0.3, 0.4, H * 1.6), 0.025, "nav_r", None)
        for x0 in (-0.02, W):
            b.flat("accent", box((x0, 0.3, 0.05), (x0 + 0.02, 1.2, 0.1)))
    elif F == "goth":
        # hunched bridge pod: heavy cowl with a ribbed carapace, a row of small square windows
        b.flat("hull", chbox((0, 0, 0), (W, L, H * 0.35), 0.08))
        pod = [(0.1, 0.05, H * 0.35), (W - 0.1, 0.05, H * 0.35), (W - 0.15, L - 0.05, H * 0.35), (0.15, L - 0.05, H * 0.35),
               (0.35, 0.3, H * 0.95), (W - 0.35, 0.3, H * 0.95), (W - 0.45, L - 0.55, H * 0.82), (0.45, L - 0.55, H * 0.82),
               (W / 2, L - 0.1, H * 0.55)]
        b.flat("hull2", hull_pts(pod))
        for k in range(6):
            x = 0.42 + k * (W - 0.84) / 5
            b.flat("trim", box((x - 0.06, L - 0.62, H * 0.62), (x + 0.06, L - 0.48, H * 0.72)))
            b.flat("window", box((x - 0.045, L - 0.47, H * 0.635), (x + 0.045, L - 0.46, H * 0.705)))
        for k in range(7):
            y = 0.25 + k * 0.25
            pts = [(0.3, y, H * 0.35), (W - 0.3, y, H * 0.35), (W / 2, y, H * 1.02), (0.3, y + 0.06, H * 0.35), (W - 0.3, y + 0.06, H * 0.35), (W / 2, y + 0.06, H * 1.02)]
            b.flat("trim", hull_pts(pts))
        for s in (-1, 1):
            b.put("metal", cyl((W / 2 + s * 0.4, 0.3, H * 0.85), (W / 2 + s * 0.55, 0.0, H * 1.6), 0.02, 6, r1=0.002))
        lamp(b, (W / 2, 0.15, H * 0.98), 0.035, "nav_r", None)
        hose(b, [(0.1, 0.2, H * 0.3), (-0.05, 0.8, H * 0.4), (0.05, 1.5, H * 0.3)], 0.03, "hose", "metal")
        hose(b, [(W - 0.1, 0.2, H * 0.3), (W + 0.05, 0.8, H * 0.4), (W - 0.05, 1.5, H * 0.3)], 0.03, "hose", "metal")
    else:
        # bubble cab: sculpted body in livery, wrap-around canopy split by a gold frame, chrome trims
        v, f = sphere((0, 0, 0), 1.0, 40, 20)
        body = [(W / 2 + x * W * 0.5, L * 0.5 + y * L * 0.52, max(z, -0.1) * H * 0.55 + H * 0.05) for x, y, z in v]
        b.put("hull", (body, f))
        b.put("hull2", torus((W / 2, L * 0.5, H * 0.2), (0, 0, 1), W * 0.49, 0.05, 48, 6))
        v2, f2 = sphere((0, 0, 0), 1.0, 40, 20)
        can = [(W / 2 + x * W * 0.36, L * 0.6 + y * L * 0.36, H * 0.42 + max(z, 0) * H * 0.58) for x, y, z in v2]
        b.put("glass", (can, f2))
        b.put("gold", torus((W / 2, L * 0.6, H * 0.43), (0, 0, 1), W * 0.36, 0.022, 48, 6))
        prof = []
        for i in range(17):
            t = math.pi * i / 16
            prof.append((W / 2, L * 0.6 + math.cos(t) * L * 0.37, H * 0.42 + math.sin(t) * H * 0.585))
        for i in range(16):
            b.put("gold", cyl(prof[i], prof[i + 1], 0.018, 8))
        b.put("chrome", torus((W / 2, L * 0.5, H * 0.08), (0, 0, 1), W * 0.5, 0.03, 48, 6))
        lamp(b, (W / 2, L * 0.02, H * 0.3), 0.04, "window", None)


def thruster(b, big=False):
    W, L, H, F = b.W, b.L, b.H, b.F
    cx, zc = W / 2, H / 2
    r_exit = min(W, H) * 0.46
    nl = L * 0.45
    y0 = nl
    if F == "ind":
        b.flat("paint_e", chbox((0.0, y0, 0.0), (W, L, H), min(W, H) * 0.08))
        b.flat("trim", box((-0.02, y0 - 0.05, -0.02), (W + 0.02, y0 + 0.07, H + 0.02)))
        bell(b, (cx, y0, zc), (0, -1, 0), r_exit * 0.45, r_exit, nl, ribs=3)
        for a in (45, 135, 225, 315):
            r = math.radians(a)
            p0 = (cx + math.cos(r) * r_exit * 0.95, y0, zc + math.sin(r) * r_exit * 0.95)
            p1 = (cx + math.cos(r) * r_exit * 0.55, y0 - nl * 0.45, zc + math.sin(r) * r_exit * 0.55)
            b.put("chrome", cyl(p0, p1, 0.03 * (2 if big else 1), 8))
            b.put("trim", cyl(p0, (p0[0] + (p1[0] - p0[0]) * 0.4, p0[1] + (p1[1] - p0[1]) * 0.4, p0[2] + (p1[2] - p0[2]) * 0.4), 0.045 * (2 if big else 1), 8))
        hazard(b, (0.0, L - 0.25, H), (W, L, H + 0.01), 8)
        b.put("metal", pipe_path([(0.1, L - 0.2, H), (0.1, y0 + 0.2, H + 0.08), (cx - 0.2, y0 + 0.1, H + 0.08)], 0.04 * (1.5 if big else 1)))
        if big:
            for sx in (-1, 1):
                for sz in (-1, 1):
                    bell(b, (cx + sx * W * 0.4, y0, zc + sz * H * 0.4), (0, -1, 0), 0.05, 0.11, 0.26)
    elif F == "goth":
        b.flat("paint_e", chbox((0.02, y0, 0.02), (W - 0.02, L, H - 0.02), min(W, H) * 0.18))
        ribs(b, (0.0, y0 + 0.1, 0.0), (W, L - 0.1, H), 1, int((L - y0) / 0.13), 0.015, "trim", 0.03)
        # main nozzle ringed by small ones
        bell(b, (cx, y0, zc), (0, -1, 0), r_exit * 0.35, r_exit * 0.72, nl, ribs=5)
        n = 6 if big else 4
        for k in range(n):
            a = math.radians(45 + k * 360 / n)
            p = (cx + math.cos(a) * r_exit * 0.86, y0, zc + math.sin(a) * r_exit * 0.86)
            bell(b, p, (0, -1, 0), r_exit * 0.06, r_exit * 0.16, nl * 0.5, ribs=0)
        for k in range(4):
            a = math.radians(k * 90)
            b.put("metal", cyl((cx + math.cos(a) * r_exit * 0.98, y0, zc + math.sin(a) * r_exit * 0.98),
                               (cx + math.cos(a) * r_exit * 0.74, y0 - nl * 0.95, zc + math.sin(a) * r_exit * 0.74), 0.018, 6))
        lamp(b, (cx, L - 0.2, H + 0.02), 0.03, "nav_r", None)
    else:
        b.flat("paint_e", chbox((0.02, y0 - 0.05, 0.02), (W - 0.02, L, H - 0.02), min(W, H) * 0.22, top=(0.0, 0.0)))
        b.flat("trim", chbox((cx - W * 0.1, y0 + 0.1, H - 0.03), (cx + W * 0.1, L - 0.1, H + H * 0.08), 0.03, top=(0.0, (L - y0) * 0.2)))
        b.flat("hull2", box((-0.006, L - 0.35, 0.1), (W + 0.006, L - 0.2, H - 0.1)))
        for z in (H * 0.3, H * 0.7):
            b.flat("gold", box((-0.004, y0 + 0.05, z - 0.01), (0.004, L - 0.05, z + 0.01)))
            b.flat("gold", box((W - 0.004, y0 + 0.05, z - 0.01), (W + 0.004, L - 0.05, z + 0.01)))
        for k in range(3):
            a = math.radians(90 + k * 120)
            d = Vector((math.cos(a), 0, math.sin(a)))
            rr = min(W, H) * 0.45
            base0 = Vector((cx, y0 + 0.1, zc)) + d * rr
            base1 = Vector((cx, L - 0.05, zc)) + d * rr * 0.8
            tip = Vector((cx, y0 + 0.05, zc)) + d * (rr + min(W, H) * 0.35)
            side = Vector((-math.sin(a), 0, math.cos(a))) * 0.02
            b.flat("trim", hull_pts([tuple(base0 + side), tuple(base1 + side), tuple(tip + side), tuple(base0 - side), tuple(base1 - side), tuple(tip - side)]))
        b.put("hull", lathe([(r_exit * 0.4, 0), (r_exit * 0.7, nl * 0.5), (r_exit * 0.98, nl * 0.95), (r_exit * 1.0, nl)],
                            (cx, y0 + 0.02, zc), (0, -1, 0), 32, False, False))
        prof2 = [(r_exit * 0.36, 0.01), (r_exit * 0.66, nl * 0.5), (r_exit * 0.94, nl * 0.98)]
        v2, f2 = lathe(prof2, (cx, y0 + 0.02, zc), (0, -1, 0), 32, False, False)
        b.put("gold", (v2, [tuple(reversed(f)) for f in f2]))
        b.put("emit_t", cyl((cx, y0 - nl * 0.15, zc), (cx, y0 - nl * 0.14, zc), r_exit * 0.45, 32))
        b.put("emit_t", torus((cx, y0 - nl + 0.01, zc), (0, 1, 0), r_exit * 0.99, 0.012, 48, 4))
        b.put("gold", torus((cx, y0 + (L - y0) * 0.3, zc), (0, 1, 0), min(W, H) * 0.49, 0.02, 48, 4))
    return (cx, 0.0, zc), r_exit


def bell(b, p0, axis, r_throat, r_exit, length, ribs=0):
    seg = 24
    prof = [(r_throat, 0.0), (r_throat * 1.05, length * 0.15), (r_exit * 0.7, length * 0.55), (r_exit, length)]
    b.put("heat", lathe(prof, p0, axis, seg, False, False))
    a = Vector(p0) + Vector(axis).normalized() * (length * 0.2)
    b.put("emit_t", cyl(tuple(a), tuple(a + Vector(axis).normalized() * 0.01), r_throat * 1.2, seg))
    prof2 = [(r_throat * 0.92, 0.01), (r_exit * 0.66, length * 0.55), (r_exit * 0.95, length * 0.99)]
    v2, f2 = lathe(prof2, p0, axis, seg, False, False)
    b.put("dark", (v2, [tuple(reversed(face)) for face in f2]))
    for i in range(ribs):
        t = 0.25 + 0.6 * i / max(1, ribs - 1)
        rr = r_throat + (r_exit - r_throat) * t * t + 0.012
        c = Vector(p0) + Vector(axis).normalized() * (length * t)
        b.put("trim", torus(tuple(c), axis, rr, 0.012, seg, 4))


def reactor(b):
    W, L, H, F = b.W, b.L, b.H, b.F
    cx, cy = W / 2, L / 2
    if F == "ind":
        # fission pile: pressure vessel with domed lid, bolted flange, control-rod drives, coolant loops
        b.flat("trim", chbox((0, 0, 0), (W, L, 0.14), 0.04))
        R_ = 0.52
        b.put("paint_p", cyl((cx, cy, 0.14), (cx, cy, H * 0.62), R_, 32))
        b.put("trim", cyl((cx, cy, H * 0.6), (cx, cy, H * 0.67), R_ + 0.06, 32))
        ring_bolts(b, (cx, cy, H * 0.67), (0, 0, 1), R_ + 0.02, 20)
        b.put("hull", lathe([(R_, 0), (R_ * 0.9, 0.12), (R_ * 0.6, 0.22), (0.0, 0.27)], (cx, cy, H * 0.67), (0, 0, 1), 32, False, False))
        for i in range(-1, 2):
            for j in range(-1, 2):
                if i * i + j * j == 2:
                    continue
                x, y = cx + i * 0.14, cy + j * 0.14
                b.put("metal", cyl((x, y, H * 0.85), (x, y, H - 0.05), 0.03, 8))
                b.put("accent" if (i + j) % 2 == 0 else "trim", cyl((x, y, H - 0.08), (x, y, H), 0.042, 8))
        # viewport with blue glow
        b.put("trim", tube((cx, cy - R_ - 0.005, H * 0.38), (cx, cy - R_ - 0.04, H * 0.38), 0.14, 0.09, 20))
        b.put("emit_p", cyl((cx, cy - R_ + 0.01, H * 0.38), (cx, cy - R_ - 0.01, H * 0.38), 0.095, 20))
        # coolant loops to side heat exchangers
        for s in (-1, 1):
            b.flat("paint_e", chbox((cx + s * 0.62 - 0.08, cy - 0.45, 0.14), (cx + s * 0.62 + 0.08, cy + 0.45, H * 0.55), 0.03))
            b.put("metal", pipe_path([(cx + s * R_ * 0.9, cy - 0.2, H * 0.5), (cx + s * 0.62, cy - 0.2, H * 0.62), (cx + s * 0.62, cy - 0.2, H * 0.55)], 0.04))
            b.put("metal", pipe_path([(cx + s * R_ * 0.9, cy + 0.2, H * 0.25), (cx + s * 0.62, cy + 0.2, H * 0.25)], 0.04))
        hazard(b, (0.0, -0.004, 0.02), (W, 0.0, 0.12), 12)
    elif F == "goth":
        R_ = 0.5
        b.put("paint_p", lathe([(0.65, 0), (0.62, 0.12), (R_, 0.25), (R_, H * 0.72), (0.3, H * 0.88), (0.12, H)], (cx, cy, 0), (0, 0, 1), 16, False, True))
        for k in range(8):
            a = math.radians(k * 45 + 22.5)
            # buttress
            pts = [(cx + math.cos(a) * R_, cy + math.sin(a) * R_, 0.25), (cx + math.cos(a) * 0.72, cy + math.sin(a) * 0.72, 0.0),
                   (cx + math.cos(a) * R_, cy + math.sin(a) * R_, H * 0.8)]
            side = Vector((-math.sin(a), math.cos(a), 0)) * 0.025
            b.flat("trim", hull_pts([tuple(Vector(p) + side) for p in pts] + [tuple(Vector(p) - side) for p in pts]))
            # slit of light between buttresses
            a2 = a + math.radians(22.5)
            p = Vector((cx + math.cos(a2) * R_ * 1.005, cy + math.sin(a2) * R_ * 1.005, 0))
            sd = Vector((-math.sin(a2), math.cos(a2), 0)) * 0.025
            b.flat("emit_p", hull_pts([tuple(p + sd + Vector((0, 0, H * 0.3))), tuple(p - sd + Vector((0, 0, H * 0.3))),
                                       tuple(p + sd + Vector((0, 0, H * 0.62))), tuple(p - sd + Vector((0, 0, H * 0.62))),
                                       tuple(p * 0.999 + Vector((cx, cy, 0)) * 0.001 + Vector((0, 0, H * 0.46)))]))
        b.put("metal", cyl((cx, cy, H), (cx, cy, H + 0.35), 0.04, 6, r1=0.004))
        hose(b, [(cx + 0.5, cy - 0.3, H * 0.6), (cx + 0.7, cy - 0.5, H * 0.3), (cx + 0.72, cy - 0.7, 0.05)], 0.04, "hose", "metal")
        hose(b, [(cx - 0.5, cy + 0.3, H * 0.65), (cx - 0.72, cy + 0.55, H * 0.35), (cx - 0.7, cy + 0.72, 0.05)], 0.04, "hose", "metal")
    else:
        # the pod: a polished sphere on a pedestal, gold rings and a glowing equator
        R_ = 0.6
        b.put("trim", lathe([(0.7, 0), (0.7, 0.06), (0.3, 0.2), (0.22, 0.4)], (cx, cy, 0), (0, 0, 1), 48, False, True))
        b.put("paint_p", sphere((cx, cy, H * 0.55), R_, 48, 24))
        b.put("emit_p", torus((cx, cy, H * 0.55), (0, 0, 1), R_ * 1.0, 0.03, 64, 8))
        for k, z in enumerate((0.3, 0.8)):
            rr = math.sqrt(max(0.0, R_ ** 2 - ((z - 0.55) * H) ** 2))
            b.put("gold", torus((cx, cy, H * z), (0, 0, 1), rr, 0.02, 48, 6))
        b.put("glass", sphere((cx, cy + R_ * 0.6, H * 0.62), 0.18, 24, 12, (1, 0.5, 1)))


def shield(b):
    W, L, H, F = b.W, b.L, b.H, b.F
    cx, cy = W / 2, L / 2
    if F == "ind":
        b.flat("paint_p", chbox((0.05, 0.05, 0), (W - 0.05, L - 0.05, 0.26), 0.05))
        for k in range(4):
            z = 0.28 + k * 0.08
            b.put("ceramic", lathe([(0.06, 0), (0.15, 0.025), (0.15, 0.04), (0.06, 0.06)], (cx, cy, z), (0, 0, 1), 16, False, False))
        b.put("ceramic", cyl((cx, cy, 0.26), (cx, cy, 0.62), 0.06, 12))
        b.put("emit_s", sphere((cx, cy, 0.7), 0.08, 16, 8))
        for k in range(4):
            a = math.radians(45 + k * 90)
            d = Vector((math.cos(a), math.sin(a), 0))
            p0 = Vector((cx, cy, 0.66)) + d * 0.08
            p1 = Vector((cx, cy, 0.86)) + d * 0.42
            b.put("metal", cyl(tuple(p0), tuple(p1), 0.022, 8, r1=0.012))
            b.put("emit_s", sphere(tuple(p1), 0.03, 8, 4))
        hazard(b, (0.05, 0.046, 0.04), (W - 0.05, 0.05, 0.2), 6)
    elif F == "goth":
        b.flat("paint_p", chbox((0.05, 0.05, 0), (W - 0.05, L - 0.05, 0.3), 0.08))
        for k in range(9):
            a = math.radians(k * 40)
            p0 = (cx + math.cos(a) * 0.32, cy + math.sin(a) * 0.32, 0.3)
            p1 = (cx + math.cos(a) * 0.4, cy + math.sin(a) * 0.4, 0.75 + 0.15 * (k % 2))
            b.put("metal", cyl(p0, p1, 0.03, 6, r1=0.003))
        b.put("emit_s", sphere((cx, cy, 0.5), 0.12, 16, 8))
        b.put("trim", torus((cx, cy, 0.5), (0, 0, 1), 0.2, 0.03, 24, 6))
    else:
        b.put("trim", lathe([(0.42, 0), (0.42, 0.06), (0.15, 0.2), (0.08, 0.35)], (cx, cy, 0), (0, 0, 1), 32, False, True))
        for k in range(12):
            a0, a1 = math.radians(-60 + k * 10), math.radians(-60 + (k + 1) * 10)
            b.put("gold", cyl((cx + math.sin(a0) * 0.38, cy, 0.55 + math.cos(a0) * 0.38 - 0.38 + 0.2),
                              (cx + math.sin(a1) * 0.38, cy, 0.55 + math.cos(a1) * 0.38 - 0.38 + 0.2), 0.02, 8))
        b.put("emit_s", torus((cx, cy, 0.62), (0, 1, 0.3), 0.2, 0.025, 48, 6))
        b.put("emit_s", sphere((cx, cy, 0.62), 0.06, 16, 8))


def capacitor(b):
    W, L, H, F = b.W, b.L, b.H, b.F
    if F == "ind":
        # rack of flat cell modules with bus bars and indicator lamps
        b.flat("trim", chbox((0, 0, 0), (W, L, 0.1), 0.03))
        for k in range(6):
            y = 0.08 + k * 0.145
            b.flat("paint_p", chbox((0.08, y, 0.1), (W - 0.08, y + 0.11, H - 0.15), 0.015))
            b.flat("dark", box((0.12, y - 0.002, H * 0.55), (0.3, y + 0.112, H * 0.62)))
            lamp(b, (W - 0.16, y + 0.055, H - 0.12), 0.018, "emit_p" if k % 3 else "nav_g", None)
        for x in (0.25, W - 0.25):
            b.flat("accent", box((x - 0.03, 0.05, H - 0.15), (x + 0.03, L - 0.05, H - 0.1)))
        corner = [(0.0, 0.0), (W - 0.06, 0.0), (0.0, L - 0.06), (W - 0.06, L - 0.06)]
        for x, y in corner:
            b.flat("trim", box((x, y, 0.1), (x + 0.06, y + 0.06, H - 0.08)))
        b.put("hose", pipe_path([(0.25, L - 0.05, H - 0.12), (0.25, L + 0.05, H * 0.6), (0.25, L + 0.02, 0.1)], 0.025))
    elif F == "goth":
        # laminated transformer core with heavy windings
        b.flat("trim", chbox((0, 0, 0), (W, L, 0.12), 0.04))
        for k in range(14):
            y = 0.15 + k * 0.05
            b.flat("metal" if k % 2 else "trim", box((0.15, y, 0.12), (W - 0.15, y + 0.04, H - 0.12)))
        for s in (-1, 1):
            x = W / 2 + s * 0.22
            b.put("paint_p", cyl((x, 0.18, H * 0.5), (x, L - 0.18, H * 0.5), 0.2, 16))
            for k in range(8):
                b.put("hose", torus((x, 0.22 + k * 0.08, H * 0.5), (0, 1, 0), 0.2, 0.012, 16, 4))
        b.flat("emit_p", box((W / 2 - 0.02, 0.15, H - 0.13), (W / 2 + 0.02, L - 0.15, H - 0.11)))
    else:
        # art-deco battery tower: stepped fins
        b.put("trim", lathe([(0.48, 0), (0.48, 0.08), (0.4, 0.12)], (W / 2, L / 2, 0), (0, 0, 1), 4, False, True, math.pi / 4))
        for k in range(5):
            z0 = 0.12 + k * 0.15
            r = 0.36 - k * 0.04
            b.put("paint_p" if k % 2 == 0 else "gold", lathe([(r, 0), (r, 0.12), (r * 0.9, 0.15)], (W / 2, L / 2, z0), (0, 0, 1), 4, False, True, math.pi / 4))
        b.put("emit_p", sphere((W / 2, L / 2, 0.92), 0.06, 16, 8))


def cargo(b):
    W, L, H, F = b.W, b.L, b.H, b.F
    if F == "opera":
        b.put("hull", capsule((W / 2, 0.05, H / 2), (W / 2, L - 0.05, H / 2), W * 0.47, 32, 8))
        for t in (0.3, 0.7):
            b.put("c_cargo", cyl((W / 2, L * t - 0.12, H / 2), (W / 2, L * t + 0.12, H / 2), W * 0.475, 32))
            b.put("gold", torus((W / 2, L * t - 0.13, H / 2), (0, 1, 0), W * 0.475, 0.012, 32, 4))
        return
    tag = "c_cargo"
    if F == "ind":
        b.flat(tag, box((0.02, 0.02, 0.02), (W - 0.02, L - 0.02, H - 0.02)))
        n = 13
        for i in range(n):
            y = 0.1 + i * (L - 0.2) / (n - 1)
            for x0, x1 in ((-0.005, 0.03), (W - 0.03, W + 0.005)):
                b.flat(tag, box((x0, y - 0.022, 0.06), (x1, y + 0.022, H - 0.06)))
            b.flat(tag, box((0.06, y - 0.022, H - 0.03), (W - 0.06, y + 0.022, H + 0.005)))
        for x in (0, W - 0.07):
            for y in (0, L - 0.07):
                for z in (0, H - 0.07):
                    b.flat("trim", box((x, y, z), (x + 0.07, y + 0.07, z + 0.07)))
        for x in (0.25, 0.4, W - 0.4, W - 0.25):
            b.put("metal", cyl((x, L + 0.01, 0.08), (x, L + 0.01, H - 0.08), 0.012, 6))
        b.flat("hull", box((0.1, -0.005, H * 0.6), (W - 0.1, 0.0, H * 0.8)))
    else:
        b.flat(tag, chbox((0.02, 0.02, 0.02), (W - 0.02, L - 0.02, H - 0.02), 0.08))
        for k in range(4):
            y = 0.2 + k * (L - 0.4) / 3
            b.flat("hose", box((-0.01, y - 0.03, -0.01), (W + 0.01, y + 0.03, H + 0.01)))
            b.flat("metal", box((W / 2 - 0.04, y - 0.04, H), (W / 2 + 0.04, y + 0.04, H + 0.02)))
        b.flat("accent", box((0.15, -0.005, H * 0.7), (W - 0.15, 0.0, H * 0.78)))


def radiator(b):
    W, L, H, F = b.W, b.L, b.H, b.F
    if F == "opera":
        pts = [(0.0, 0.05), (L - 0.05, 0.05), (L * 0.85, H * 0.7), (L * 0.35, H)]
        b.flat("hull2", fin(pts, 0.06, W / 2))
        for (y0, z0), (y1, z1) in zip(pts[1:], pts[2:] + pts[:1]):
            b.put("gold", cyl((W / 2, y0, z0), (W / 2, y1, z1), 0.035, 10))
        b.put("chrome", cyl((W / 2, 0.0, 0.05), (W / 2, L - 0.05, 0.05), 0.05, 12))
        return
    b.flat("trim", box((0, 0, 0), (W, L, 0.12)))
    if F == "ind":
        b.flat("trim", box((W / 2 - 0.04, 0.0, 0.12), (W / 2 + 0.04, 0.08, H)))
        b.flat("trim", box((W / 2 - 0.04, L - 0.08, 0.12), (W / 2 + 0.04, L, H)))
        b.flat("trim", box((W / 2 - 0.04, 0.0, H - 0.08), (W / 2 + 0.04, L, H)))
        n = 16
        for i in range(n):
            y = 0.12 + i * (L - 0.24) / (n - 1)
            b.flat("metal", box((W / 2 - 0.07, y - 0.012, 0.12), (W / 2 + 0.07, y + 0.012, H - 0.08)))
        b.flat("ember", box((W / 2 - 0.02, 0.08, H - 0.2), (W / 2 + 0.02, L - 0.08, H - 0.08)))
        b.put("metal", cyl((W / 2, 0.1, 0.06), (W / 2, L - 0.1, 0.06), 0.05, 10))
        hazard(b, (W / 2 - 0.041, 0.0, 0.12), (W / 2 + 0.041, 0.08, H), 8, axis=2)
    else:
        # dorsal fin of ribbed spines
        n = 12
        for i in range(n):
            y = 0.15 + i * (L - 0.3) / (n - 1)
            hgt = H * (0.45 + 0.55 * math.sin(math.pi * (i + 0.5) / n))
            b.flat("metal", hull_pts([(W / 2 - 0.03, y - 0.04, 0.12), (W / 2 + 0.03, y - 0.04, 0.12), (W / 2 - 0.03, y + 0.04, 0.12), (W / 2 + 0.03, y + 0.04, 0.12),
                                      (W / 2, y + 0.06, hgt)]))
        b.flat("ember", box((W / 2 - 0.015, 0.15, 0.12), (W / 2 + 0.015, L - 0.15, 0.16)))


def sensor(b):
    W, L, H, F = b.W, b.L, b.H, b.F
    cx, cy = W / 2, L / 2
    if F == "ind":
        b.flat("trim", chbox((0.1, 0.1, 0), (W - 0.1, L - 0.1, 0.15), 0.03))
        # lattice mast
        t = 0.025
        for sx in (-1, 1):
            for sy in (-1, 1):
                b.put("trim", cyl((cx + sx * 0.22, cy + sy * 0.22, 0.15), (cx + sx * 0.06, cy + sy * 0.06, H * 0.78), t, 6))
        for k in range(4):
            z = 0.25 + k * 0.25
            r = 0.22 - (z - 0.15) / (H * 0.78 - 0.15) * 0.16
            for sx, sy, ex, ey in ((-1, -1, 1, -1), (1, -1, 1, 1), (1, 1, -1, 1), (-1, 1, -1, -1)):
                b.put("trim", cyl((cx + sx * r, cy + sy * r, z), (cx + ex * r, cy + ey * r, z + 0.12), 0.012, 4))
        # rotating radar bar
        b.flat("paint_p", chbox((cx - 0.5, cy - 0.06, H * 0.8), (cx + 0.5, cy + 0.06, H * 0.92), 0.02))
        for k in range(9):
            x = cx - 0.44 + k * 0.11
            b.flat("dark", box((x - 0.03, cy + 0.06, H * 0.82), (x + 0.03, cy + 0.065, H * 0.9)))
        b.put("metal", cyl((cx, cy, H * 0.92), (cx, cy, H + 0.3), 0.012, 6))
        lamp(b, (cx, cy, H + 0.3), 0.025, "nav_r", None)
        b.put("metal", cyl((cx + 0.3, cy, 0.15), (cx + 0.35, cy, H * 0.6), 0.008, 4))
    elif F == "goth":
        b.flat("paint_p", chbox((0.15, 0.15, 0), (W - 0.15, L - 0.15, 0.25), 0.06))
        heights = [1.0, 0.75, 0.6, 0.85, 0.5]
        for k, hh in enumerate(heights):
            a = math.radians(k * 72)
            p = (cx + math.cos(a) * 0.12, cy + math.sin(a) * 0.12, 0.25)
            b.put("metal", cyl(p, (p[0] + math.cos(a) * 0.08, p[1] + math.sin(a) * 0.08, 0.25 + H * hh), 0.025, 6, r1=0.004))
            for j in range(3):
                z = 0.4 + j * 0.25
                if z < H * hh:
                    b.put("trim", cyl((p[0], p[1], z), (p[0] + math.cos(a) * 0.12, p[1] + math.sin(a) * 0.12, z + 0.03), 0.01, 4))
        lamp(b, (cx, cy, 0.3), 0.04, "nav_r", None)
        b.put("glass", sphere((cx, cy + 0.2, 0.2), 0.08, 12, 6))
    else:
        b.put("trim", lathe([(0.35, 0), (0.35, 0.05), (0.08, 0.2)], (cx, cy, 0), (0, 0, 1), 32, False, True))
        b.put("chrome", cyl((cx, cy, 0.2), (cx, cy, H * 0.75), 0.03, 12, r1=0.015))
        b.put("hull2", sphere((cx, cy, H * 0.55), 0.13, 24, 12))
        b.put("gold", torus((cx, cy, H * 0.55), (0.3, 0, 1), 0.22, 0.012, 32, 4))
        b.put("gold", torus((cx, cy, H * 0.55), (-0.3, 0.2, 1), 0.28, 0.01, 32, 4))
        b.put("emit_s", sphere((cx, cy, H * 0.77), 0.035, 12, 6))


def solar(b):
    W, L, H, F = b.W, b.L, b.H, b.F
    b.flat("trim", box((0, 0, 0), (W, 0.4, H)))
    b.put("metal" if F != "opera" else "chrome", cyl((W / 2, 0.4, H / 2), (W / 2, L, H / 2), 0.03, 8))
    b.flat("solar", box((W / 2 - 0.015, 0.5, 0.05), (W / 2 + 0.015, L - 0.05, H - 0.05)))
    frame = "gold" if F == "opera" else ("accent" if F == "ind" else "metal")
    for z in (0.03, H - 0.07):
        b.flat(frame, box((W / 2 - 0.02, 0.48, z), (W / 2 + 0.02, L - 0.03, z + 0.04)))
    for y in (0.48, L / 2, L - 0.07):
        b.flat(frame, box((W / 2 - 0.02, y, 0.03), (W / 2 + 0.02, y + 0.04, H - 0.03)))


# ---------------------------------------------------------------------------------------------------------------------
# Cores: the ship's heart, intricate inside its footprint
# ---------------------------------------------------------------------------------------------------------------------

def core(b):
    W, L, H, F = b.W, b.L, b.H, b.F
    n = b.size
    kind = {(1, 1, 1): 1, (2, 2, 2): 2, (3, 3, 3): 3, (3, 5, 3): 35}[tuple(n)]
    {"ind": core_ind, "goth": core_goth, "opera": core_opera}[F](b, kind)


def core_ind(b, kind):
    W, L, H = b.W, b.L, b.H
    cx, cy, cz = W / 2, L / 2, H / 2
    if kind == 1:
        # gyro governor in a bolted socket
        b.flat("trim", chbox((0.02, 0.02, 0), (W - 0.02, L - 0.02, 0.08), 0.02))
        b.put("metal", cyl((cx, cy, 0.08), (cx, cy, 0.42), 0.015, 8))
        b.put("paint_p", torus((cx, cy, 0.27), (1, 0, 0), 0.15, 0.018, 24, 6))
        b.put("metal", torus((cx, cy, 0.27), (0, 1, 0), 0.12, 0.012, 24, 6))
        for k in range(2):
            a = math.radians(k * 180 + 30)
            p = (cx + math.cos(a) * 0.1, cy + math.sin(a) * 0.1, 0.3)
            b.put("metal", cyl((cx, cy, 0.38), p, 0.008, 4))
            b.put("accent", sphere(p, 0.035, 12, 6))
        b.put("emit_c", sphere((cx, cy, 0.27), 0.05, 16, 8))
        lamp(b, (W - 0.07, 0.07, 0.1), 0.015, "nav_g", None)
    elif kind == 2:
        # gimbal core: nested rings around a glowing ingot, on hydraulic struts from a heavy plate
        b.flat("trim", chbox((0, 0, 0), (W, L, 0.12), 0.04))
        hazard(b, (0.0, -0.004, 0.02), (W, 0.0, 0.1), 10)
        b.flat("emit_c", hull_pts([(cx + x * 0.09, cy + y * 0.09, cz + z * 0.16) for x in (-1, 1) for y in (-1, 1) for z in (-1, 1)]
                                  + [(cx, cy, cz + 0.24), (cx, cy, cz - 0.24)]))
        for i, (ax, r) in enumerate((((0, 0, 1), 0.42), ((1, 0, 0), 0.36), ((0, 1, 0), 0.3))):
            b.put("paint_p" if i == 0 else "metal", torus((cx, cy, cz), ax, r, 0.028, 48, 8))
        b.put("metal", cyl((cx - 0.42, cy, cz), (cx + 0.42, cy, cz), 0.02, 8))
        for k in range(4):
            a = math.radians(45 + k * 90)
            p0 = (cx + math.cos(a) * 0.4, cy + math.sin(a) * 0.4, 0.12)
            p1 = (cx + math.cos(a) * 0.3, cy + math.sin(a) * 0.3, cz - 0.28)
            b.put("trim", cyl(p0, ((p0[0] + p1[0]) / 2, (p0[1] + p1[1]) / 2, (p0[2] + p1[2]) / 2), 0.035, 10))
            b.put("chrome", cyl(((p0[0] + p1[0]) / 2, (p0[1] + p1[1]) / 2, (p0[2] + p1[2]) / 2), p1, 0.02, 8))
        b.put("hose", pipe_path([(0.1, 0.1, 0.12), (0.15, 0.2, 0.35), (cx - 0.08, cy - 0.05, cz - 0.2)], 0.02, 6))
        lamp(b, (W - 0.1, 0.1, 0.14), 0.02, "nav_g", None)
    elif kind == 3:
        # foundry heart: octagonal armoured drum with a molten column seen through vertical slots, flywheels, catwalk
        b.flat("trim", chbox((0, 0, 0), (W, L, 0.14), 0.05))
        R_ = 0.55
        oct8 = [(cx + math.cos(math.radians(22.5 + k * 45)) * R_, cy + math.sin(math.radians(22.5 + k * 45)) * R_) for k in range(8)]
        b.put("emit_c", cyl((cx, cy, 0.14), (cx, cy, H - 0.2), 0.2, 24))
        for k in range(8):
            (x0, y0), (x1, y1) = oct8[k], oct8[(k + 1) % 8]
            # each drum face is a plate with a slot: two half plates
            for t0, t1 in ((0.0, 0.38), (0.62, 1.0)):
                p0 = (x0 + (x1 - x0) * t0, y0 + (y1 - y0) * t0)
                p1 = (x0 + (x1 - x0) * t1, y0 + (y1 - y0) * t1)
                n = Vector((p1[1] - p0[1], -(p1[0] - p0[0]), 0)).normalized() * 0.05
                b.flat("paint_p", hull_pts([(p0[0], p0[1], 0.2), (p1[0], p1[1], 0.2), (p0[0], p0[1], H - 0.3), (p1[0], p1[1], H - 0.3),
                                            (p0[0] - n.x, p0[1] - n.y, 0.2), (p1[0] - n.x, p1[1] - n.y, 0.2),
                                            (p0[0] - n.x, p0[1] - n.y, H - 0.3), (p1[0] - n.x, p1[1] - n.y, H - 0.3)]))
        for z in (0.2, H * 0.5, H - 0.32):
            b.put("trim", torus((cx, cy, z), (0, 0, 1), R_ * 0.98, 0.04, 8, 6))
        # flywheel stack on top
        for k in range(3):
            z = H - 0.28 + k * 0.08
            b.put("metal", cyl((cx, cy, z), (cx, cy, z + 0.05), 0.42 - k * 0.1, 32))
        b.put("accent", cyl((cx, cy, H - 0.04), (cx, cy, H), 0.12, 16))
        # catwalk ring with rails
        b.put("trim", tube((cx, cy, H * 0.38), (cx, cy, H * 0.4), 0.72, 0.6, 32))
        for k in range(12):
            a = math.radians(k * 30)
            b.put("accent", cyl((cx + math.cos(a) * 0.7, cy + math.sin(a) * 0.7, H * 0.4), (cx + math.cos(a) * 0.7, cy + math.sin(a) * 0.7, H * 0.5), 0.008, 4))
        b.put("accent", torus((cx, cy, H * 0.5), (0, 0, 1), 0.7, 0.008, 48, 4))
        for s in (-1, 1):
            b.put("metal", pipe_path([(cx + s * 0.6, cy + 0.3, 0.14), (cx + s * 0.68, cy + 0.3, H * 0.3), (cx + s * 0.58, cy + 0.3, H * 0.62)], 0.045))
        lamp(b, (0.1, 0.1, 0.16), 0.03, "nav_r", None)
        lamp(b, (W - 0.1, L - 0.1, 0.16), 0.03, "nav_g", None)
    else:
        # spine core: a linear accelerator line of three toroidal magnets on a glowing tube, heavy end caps
        b.flat("trim", chbox((0, 0, 0), (W, L, 0.14), 0.05))
        b.put("emit_c", cyl((cx, 0.3, cz), (cx, L - 0.3, cz), 0.09, 20))
        b.put("glass", cyl((cx, 0.3, cz), (cx, L - 0.3, cz), 0.16, 24))
        for y in (0.0, L - 0.35):
            b.flat("paint_p", chbox((0.1, y, 0.14), (W - 0.1, y + 0.35, H - 0.1), 0.08))
            ring_bolts(b, (cx, y + (0.35 if y == 0 else 0.0), cz), (0, 1 if y == 0 else -1, 0), 0.45, 12)
        for k in range(3):
            y = 0.85 + k * (L - 1.7) / 2
            b.put("accent", torus((cx, y, cz), (0, 1, 0), 0.42, 0.13, 32, 12))
            b.put("trim", cyl((cx, y - 0.05, cz), (cx, y + 0.05, cz), 0.6, 32))
            b.flat("trim", box((cx - 0.15, y - 0.12, 0.14), (cx + 0.15, y + 0.12, cz - 0.45)))
        for s in (-1, 1):
            b.put("metal", cyl((cx + s * 0.6, 0.35, cz + 0.4), (cx + s * 0.6, L - 0.35, cz + 0.4), 0.045, 10))
            b.put("hose", cyl((cx + s * 0.6, 0.35, cz - 0.3), (cx + s * 0.6, L - 0.35, cz - 0.3), 0.035, 8))


def core_goth(b, kind):
    W, L, H = b.W, b.L, b.H
    cx, cy, cz = W / 2, L / 2, H / 2
    if kind == 1:
        # reliquary: a ribbed amber heart clutched by four claws
        b.flat("trim", chbox((0.03, 0.03, 0), (W - 0.03, L - 0.03, 0.07), 0.02))
        b.put("emit_c", sphere((cx, cy, 0.24), 0.07, 16, 8, (1, 1, 1.3)))
        for k in range(6):
            b.put("trim", torus((cx, cy, 0.24), (math.cos(math.radians(k * 30)), math.sin(math.radians(k * 30)), 0), 0.085, 0.006, 16, 3))
        for k in range(4):
            a = math.radians(45 + k * 90)
            pts = [(cx + math.cos(a) * 0.2, cy + math.sin(a) * 0.2, 0.07), (cx + math.cos(a) * 0.17, cy + math.sin(a) * 0.17, 0.25),
                   (cx + math.cos(a) * 0.09, cy + math.sin(a) * 0.09, 0.38)]
            b.put("metal", pipe_path(pts, 0.014, 6))
            b.put("metal", cyl(pts[-1], (cx + math.cos(a) * 0.03, cy + math.sin(a) * 0.03, 0.33), 0.012, 6, r1=0.002))
    elif kind in (2, 3):
        # hanging ribcage around a pulsing heart; organ pipes for the 3x3x3
        s = 1.0 if kind == 2 else 1.45
        b.flat("trim", chbox((0, 0, 0), (W, L, 0.12), 0.05))
        for (a0, a1) in (((0, 0), (W, 0.08)), ((0, L - 0.08), (W, L)), ((0, 0), (0.08, L)), ((W - 0.08, 0), (W, L))):
            b.flat("trim", box((a0[0], a0[1], H - 0.1), (a1[0], a1[1], H)))
        b.flat("trim", box((cx - 0.04, 0, H - 0.1), (cx + 0.04, L, H)))
        hz = cz + (0.0 if kind == 2 else -0.1)
        b.put("emit_c", sphere((cx, cy, hz), 0.17 * s, 24, 12, (1, 1, 1.25)))
        b.put("glass", sphere((cx, cy, hz), 0.22 * s, 24, 12, (1, 1, 1.2)))
        nr = 9 if kind == 2 else 13
        for k in range(nr):
            a = math.radians(k * 360 / nr)
            pts = []
            for j in range(9):
                t = j / 8
                r = (0.25 + 0.18 * math.sin(math.pi * t)) * s
                pts.append((cx + math.cos(a) * r, cy + math.sin(a) * r, H - 0.1 - t * (H - 0.22)))
            b.put("metal", pipe_path(pts, 0.016 * s, 6))
        b.put("hose", pipe_path([(cx, cy, H - 0.1), (cx, cy, hz + 0.22 * s)], 0.03 * s, 8))
        if kind == 3:
            # organ pipes around the cage
            for k in range(16):
                a = math.radians(k * 22.5 + 11)
                r = 0.68
                hh = 0.6 + 0.6 * (0.5 + 0.5 * math.sin(k * 1.7))
                p = (cx + math.cos(a) * r, cy + math.sin(a) * r)
                b.put("paint_p", cyl((p[0], p[1], 0.12), (p[0], p[1], 0.12 + hh), 0.045, 8))
                b.put("dark", cyl((p[0], p[1], 0.12 + hh), (p[0], p[1], 0.12 + hh + 0.004), 0.035, 8))
                b.put("emit_c", cyl((p[0], p[1], 0.12 + hh * 0.5), (p[0], p[1], 0.12 + hh * 0.5 + 0.03), 0.047, 8))
        for k in range(4):
            a = math.radians(45 + k * 90)
            b.flat("trim", box((cx + math.cos(a) * W * 0.44 - 0.04, cy + math.sin(a) * L * 0.44 - 0.04, 0.12),
                               (cx + math.cos(a) * W * 0.44 + 0.04, cy + math.sin(a) * L * 0.44 + 0.04, H - 0.1)))
    else:
        # spine: vertebrae along a glowing nerve cord
        b.flat("trim", chbox((0, 0, 0), (W, L, 0.12), 0.05))
        b.put("emit_c", cyl((cx, 0.15, cz), (cx, L - 0.15, cz), 0.06, 12))
        n = 11
        for k in range(n):
            y = 0.2 + k * (L - 0.4) / (n - 1)
            sc = 0.8 + 0.4 * math.sin(math.pi * k / (n - 1))
            pts = []
            for j in range(10):
                a = math.radians(j * 36)
                r = (0.22 if j % 2 else 0.3) * sc
                pts.append((cx + math.cos(a) * r, cz + math.sin(a) * r))
            pts.append((cx, cz + 0.62 * sc))
            pts.append((cx + 0.5 * sc, cz - 0.1))
            pts.append((cx - 0.5 * sc, cz - 0.1))
            v = [(x, y - 0.035, z) for x, z in pts] + [(x, y + 0.035, z) for x, z in pts]
            b.flat("paint_p", hull_pts(v))
            b.flat("trim", box((cx - 0.05, y - 0.03, 0.12), (cx + 0.05, y + 0.03, cz - 0.15)))
        for s in (-1, 1):
            hose(b, [(cx + s * 0.55, 0.2, cz - 0.2), (cx + s * 0.6, L / 2, cz - 0.35), (cx + s * 0.55, L - 0.2, cz - 0.2)], 0.035, "hose", "metal")


def core_opera(b, kind):
    W, L, H = b.W, b.L, b.H
    cx, cy, cz = W / 2, L / 2, H / 2

    def gem(c, r, tag="emit_c"):
        top = [(c[0] + math.cos(math.radians(k * 45)) * r * 0.6, c[1] + math.sin(math.radians(k * 45)) * r * 0.6, c[2] + r * 0.35) for k in range(8)]
        mid = [(c[0] + math.cos(math.radians(k * 45 + 22.5)) * r, c[1] + math.sin(math.radians(k * 45 + 22.5)) * r, c[2] + r * 0.1) for k in range(8)]
        b.flat(tag, hull_pts(top + mid + [(c[0], c[1], c[2] - r * 1.1)]))

    def pedestal(rr, hh):
        b.put("trim", lathe([(rr, 0), (rr, hh * 0.2), (rr * 0.45, hh * 0.5), (rr * 0.3, hh)], (cx, cy, 0), (0, 0, 1), 48, False, True))
        b.put("gold", torus((cx, cy, hh * 0.2), (0, 0, 1), rr, rr * 0.05, 48, 6))

    if kind == 1:
        # armillary sphere around a gem
        pedestal(0.18, 0.1)
        gem((cx, cy, 0.29), 0.07)
        for ax in ((0, 0, 1), (1, 0, 0.2), (0.3, 1, 0.4)):
            b.put("gold", torus((cx, cy, 0.29), ax, 0.15, 0.007, 32, 4))
    elif kind == 2:
        # orrery: planets on arms around a sun
        pedestal(0.42, 0.2)
        b.put("emit_c", sphere((cx, cy, cz + 0.1), 0.13, 32, 16))
        b.put("chrome", cyl((cx, cy, 0.2), (cx, cy, cz - 0.03), 0.025, 12))
        cols = ["hull2", "paint_p", "accent", "armor"]
        for k in range(4):
            a = math.radians(k * 97 + 20)
            r = 0.22 + k * 0.06
            z = cz + 0.1 + (k - 1.5) * 0.05
            p = (cx + math.cos(a) * r, cy + math.sin(a) * r, z)
            b.put("gold", torus((cx, cy, z), (0, 0, 1), r, 0.005, 48, 3))
            b.put("chrome", cyl((cx, cy, z), p, 0.006, 4))
            b.put(cols[k], sphere(p, 0.035 + 0.01 * (k % 2), 16, 8))
    elif kind == 3:
        # jewel heart in concentric gold rings on a fluted pedestal
        pedestal(0.65, 0.35)
        gem((cx, cy, cz + 0.12), 0.3)
        for i, ax in enumerate(((0, 0, 1), (1, 0, 0.35), (-0.35, 1, 0.2), (0.6, 0.6, 1))):
            b.put("gold" if i % 2 == 0 else "chrome", torus((cx, cy, cz + 0.12), ax, 0.48 + i * 0.07, 0.016, 64, 6))
        for k in range(8):
            a = math.radians(k * 45)
            b.put("gold", cyl((cx + math.cos(a) * 0.62, cy + math.sin(a) * 0.62, 0.07), (cx + math.cos(a) * 0.62, cy + math.sin(a) * 0.62, 0.3), 0.025, 8))
            b.put("emit_c", sphere((cx + math.cos(a) * 0.62, cy + math.sin(a) * 0.62, 0.32), 0.03, 10, 5))
    else:
        # astrolabe chain: three gems in rings, joined by a chrome spine
        b.put("trim", capsule((cx, 0.15, 0.12), (cx, L - 0.15, 0.12), 0.18, 24, 6))
        b.put("chrome", cyl((cx, 0.2, cz), (cx, L - 0.2, cz), 0.025, 12))
        for k in range(3):
            y = 0.5 + k * (L - 1.0) / 2
            gem((cx, y, cz + 0.06), 0.18)
            b.put("gold", torus((cx, y, cz), (0, 1, 0), 0.42, 0.02, 48, 6))
            b.put("gold", torus((cx, y, cz), (1, 0, 0.3), 0.36, 0.012, 48, 4))
            b.put("chrome", cyl((cx, y, 0.2), (cx, y, cz - 0.2), 0.03, 10))


BUILDERS = {
    "cockpit": cockpit, "autocannon": autocannon, "gatling": gatling, "railgun": railgun, "heavy_cannon": heavy_cannon,
    "mass_driver": mass_driver, "beam": beam, "tesla": tesla, "plasma": plasma, "torpedo": torpedo, "missile": missile,
    "flak": flak, "harpoon": harpoon,
    "thruster_s": lambda b: thruster(b, False), "thruster_l": lambda b: thruster(b, True), "reactor": reactor,
    "shield": shield, "capacitor": capacitor, "cargo": cargo, "radiator": radiator, "sensor": sensor, "solar": solar,
    "core_1": core, "core_2": core, "core_3": core, "core_35": core, "girder": girder,
}


from ld_capital import KIT_CAPITAL, LABELS_CAPITAL, CATEGORY_CAPITAL, BUILDERS_CAPITAL, BIG_HULL, big_hull  # noqa: E402

KIT.update(KIT_CAPITAL)
LABELS.update(LABELS_CAPITAL)
CATEGORY.update(CATEGORY_CAPITAL)
BUILDERS.update(BUILDERS_CAPITAL)
BUILDERS["thruster_xl"] = lambda b: thruster(b, True)


def build_block(kind, S, acc, group, M, size=None, remap=None):
    size = size or KIT[kind]
    b = B(S, acc, group, M, size)
    b.remap = remap or {}
    if kind in BIG_HULL:
        big_hull(b, kind)
        return b, None
    if kind.startswith("hull_"):
        hull_piece(b, kind)
        return b, None
    res = BUILDERS[kind](b)
    return b, res
