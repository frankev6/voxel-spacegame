"""Ships and stations as voxel cells (0.5 m) plus functional blocks, and the per-direction designs.

Coordinates in cells: x right, y forward (bow at +y, exhaust toward -y), z up; the ship is symmetric about x = 0
(cell i mirrors cell -1-i, i.e. x -> -x in continuous coordinates).

Hull shapes come from straight-sided pieces (lofts between octagonal sections, and general convex pieces), so every
sloped face is one continuous plane across all the cells it passes through. That is what keeps edges and corners
aligned; there is no per-cell auto-slope pass any more."""

import math
from mathutils import Matrix, Vector

from ld_geom import (CELL, Acc, mesh_cells, hash01, cbox, box, cyl, sphere, ico, torus, lathe,
                     section_poly, loft_planes, classify_cell)
from ld_blocks import KIT, build_block, greeble_face

LIVERY_TAGS = ("hull", "hull2", "accent")


class Ship:
    def __init__(self, name, seed=1, livery=None):
        self.name = name
        self.cells = {}
        self.shapes = {}        # cut cells: {cell: ('clip', planes)}
        self.vols = {}
        self.livery = livery    # colour scheme name from the style's 'liveries'
        self.mods = []          # (kind, min_cell, rotation, size, rotated size, colour remap)
        self.mod_cells = set()
        self.seed = seed
        self.craters = []       # (centre metres, radius metres)
        self.plumes = []        # (pos metres, dir, radius, length)
        self.extra = []         # callables(acc, group, S) for custom geometry
        self.work_lights = []

    # cells -------------------------------------------------------------------------------------------------------
    def set(self, c, tag):
        if c not in self.mod_cells:
            self.cells[c] = tag
            self.shapes.pop(c, None)

    def box(self, x0, x1, y0, y1, z0, z1, tag="hull", mirror=False):
        for x in range(x0, x1):
            for y in range(y0, y1):
                for z in range(z0, z1):
                    self.set((x, y, z), tag)
                    if mirror:
                        self.set((-1 - x, y, z), tag)

    def clear(self, x0, x1, y0, y1, z0, z1, mirror=False):
        for x in range(x0, x1):
            for y in range(y0, y1):
                for z in range(z0, z1):
                    for c in ((x, y, z), (-1 - x, y, z)) if mirror else ((x, y, z),):
                        self.cells.pop(c, None)
                        self.shapes.pop(c, None)

    def paint(self, pred, tag):
        for c in list(self.cells):
            if pred(c[0] + 0.5, c[1] + 0.5, c[2] + 0.5):
                self.cells[c] = tag

    # straight-sided pieces ---------------------------------------------------------------------------------------
    def loft(self, stations, tag="hull", cx=0.0, mirror=False):
        """Hull between octagonal sections. stations: [(y, w, zb, zt[, ct[, cb]])] in cells; half-width w around cx,
        top/bottom chamfers ct/cb. Changes between stations of 0, 1:2, 1:1 or 2:1 keep every cut on whole cells."""
        for (ya, *a), (yb, *b) in zip(stations, stations[1:]):
            if yb <= ya:
                continue
            sa = section_poly(cx, a[0], a[1], a[2], *(list(a[3:5]) + [0, 0])[:2])
            sb = section_poly(cx, b[0], b[1], b[2], *(list(b[3:5]) + [0, 0])[:2])
            planes = loft_planes(ya, sa, yb, sb)
            xs = [x for x, _ in sa + sb]
            zs = [z for _, z in sa + sb]
            self.convex(planes, ((min(xs), ya, min(zs)), (max(xs), yb, max(zs))), tag)
        if mirror and cx:
            self.loft(stations, tag, -cx, False)

    def convex(self, planes, bbox, tag="hull", mirror=False):
        """Any convex piece given as half-spaces [(n, d)]: n.p <= d, in cell units."""
        (x0, y0, z0), (x1, y1, z1) = bbox
        for x in range(math.floor(x0), math.ceil(x1)):
            for y in range(math.floor(y0), math.ceil(y1)):
                for z in range(math.floor(z0), math.ceil(z1)):
                    self._piece((x, y, z), classify_cell(planes, (x, y, z)), tag)
        if mirror:
            mp = [((-n[0], n[1], n[2]), d) for n, d in planes]
            self.convex(mp, ((-x1, y0, z0), (-x0, y1, z1)), tag)

    def _piece(self, c, res, tag):
        if c in self.mod_cells or res[0] == "empty":
            return
        if res[0] == "full":
            self.cells[c] = tag
            self.shapes.pop(c, None)
            return
        if c in self.cells and c not in self.shapes:
            return
        if c in self.shapes and self.vols.get(c, 0) >= res[2]:
            return
        self.cells[c] = tag
        self.shapes[c] = ("clip", res[1])
        self.vols[c] = res[2]

    def spire(self, cx, cy, w, z0, z1, k=1.0, tag="hull", down=False):
        """Square tower with a pyramid cap (or an inverted one hanging down when down=True). k = cap steepness."""
        sides = [((1, 0, 0), cx + w), ((-1, 0, 0), -(cx - w)), ((0, 1, 0), cy + w), ((0, -1, 0), -(cy - w))]
        if not down:
            planes = sides + [((0, 0, -1), -z0), ((k, 0, 1), z1 + k * cx), ((-k, 0, 1), z1 - k * cx),
                              ((0, k, 1), z1 + k * cy), ((0, -k, 1), z1 - k * cy)]
        else:
            planes = sides + [((0, 0, 1), z1), ((k, 0, -1), -z0 + k * cx), ((-k, 0, -1), -z0 - k * cx),
                              ((0, k, -1), -z0 + k * cy), ((0, -k, -1), -z0 - k * cy)]
        self.convex(planes, ((cx - w, cy - w, z0), (cx + w, cy + w, z1)), tag)

    def ball(self, c, r, tag="hull", n=1):
        """Faceted sphere: intersection of half-spaces along icosphere directions (geodesic look)."""
        verts, _ = ico((0, 0, 0), 1.0, n)
        planes = [((v[0], v[1], v[2]), r + v[0] * c[0] + v[1] * c[1] + v[2] * c[2]) for v in verts]
        self.convex(planes, ((c[0] - r, c[1] - r, c[2] - r), (c[0] + r, c[1] + r, c[2] + r)), tag)

    def ring(self, c, axis, R0, R1, half_t, n=24, tags=("hull",)):
        """Faceted ring of n straight segments around `axis` ('z' or 'y') through centre c."""
        da = 2 * math.pi / n

        def u(a):
            return (math.cos(a), math.sin(a), 0.0) if axis == "z" else (math.cos(a), 0.0, math.sin(a))

        def tang(a):
            return (-math.sin(a), math.cos(a), 0.0) if axis == "z" else (-math.sin(a), 0.0, math.cos(a))

        wv = (0, 0, 1) if axis == "z" else (0, 1, 0)

        def dot(n_, p):
            return n_[0] * p[0] + n_[1] * p[1] + n_[2] * p[2]

        def neg(v):
            return tuple(-k for k in v)

        for i in range(n):
            a0, a1 = i * da, (i + 1) * da
            um = u((a0 + a1) / 2)
            t0, t1 = tang(a0), tang(a1)
            planes = [(um, R1 * math.cos(da / 2) + dot(um, c)), (neg(um), -R0 + dot(neg(um), c)),
                      (neg(t0), dot(neg(t0), c)), (t1, dot(t1, c)),
                      (wv, half_t + dot(wv, c)), (neg(wv), half_t + dot(neg(wv), c))]
            pts = [tuple(c[k] + u(a)[k] * r for k in range(3)) for a in (a0, a1) for r in (R0, R1)]
            lo = [min(p[k] for p in pts) - (half_t if wv[k] else 0) for k in range(3)]
            hi = [max(p[k] for p in pts) + (half_t if wv[k] else 0) for k in range(3)]
            self.convex(planes, (tuple(lo), tuple(hi)), tags[i % len(tags)])

    # modules -----------------------------------------------------------------------------------------------------
    def mod(self, kind, x, y, z, rot=(0, 0, 0), mirror=False, size=None, remap=None):
        size = size or KIT[kind]
        Rm = Matrix.Identity(3)
        for ax, deg in zip("XYZ", rot):
            if deg:
                Rm = Matrix.Rotation(math.radians(deg), 3, ax) @ Rm
        rs = Rm @ Vector(size)
        rsz = tuple(int(round(abs(c))) for c in rs)
        self._place(kind, (x, y, z), Rm, size, rsz, remap)
        if mirror:
            self._place(kind, (-x - rsz[0], y, z), Rm, size, rsz, remap)

    def _place(self, kind, mn, Rm, size, rsz, remap=None):
        for dx in range(rsz[0]):
            for dy in range(rsz[1]):
                for dz in range(rsz[2]):
                    c = (mn[0] + dx, mn[1] + dy, mn[2] + dz)
                    self.cells.pop(c, None)
                    self.shapes.pop(c, None)
                    self.mod_cells.add(c)
        self.mods.append((kind, mn, Rm, size, rsz, remap))

    def mod_matrix(self, mn, Rm, size, rsz):
        half_local = Vector(size) * (CELL / 2)
        centre = Vector(mn) * CELL + Vector(rsz) * (CELL / 2)
        return Matrix.Translation(centre) @ Rm.to_4x4() @ Matrix.Translation(-half_local)

    # damage --------------------------------------------------------------------------------------------------------
    def carve(self, centre_cells, radius_cells, seed=0):
        cx, cy, cz = centre_cells
        removed = []
        for c in list(self.cells):
            d = math.dist((c[0] + 0.5, c[1] + 0.5, c[2] + 0.5), (cx, cy, cz))
            jag = (hash01(c[0], c[1], c[2], seed) - 0.5) * 1.6
            if d < radius_cells + jag:
                removed.append((c, self.cells.pop(c)))
                self.shapes.pop(c, None)
        keep = []
        for m in self.mods:
            kind, mn, Rm, size, rsz, remap = m
            mc = Vector(mn) + Vector(rsz) * 0.5
            if (mc - Vector((cx, cy, cz))).length < radius_cells * 0.8:
                removed.append((tuple(int(v) for v in mc), "module"))
                for dx in range(rsz[0]):
                    for dy in range(rsz[1]):
                        for dz in range(rsz[2]):
                            self.mod_cells.discard((mn[0] + dx, mn[1] + dy, mn[2] + dz))
            else:
                keep.append(m)
        self.mods = keep
        self.craters.append((Vector((cx, cy, cz)) * CELL, radius_cells * CELL))
        return removed

    def bounds(self):
        xs = [c[0] for c in self.cells] + [c[0] for c in self.mod_cells]
        ys = [c[1] for c in self.cells] + [c[1] for c in self.mod_cells]
        zs = [c[2] for c in self.cells] + [c[2] for c in self.mod_cells]
        return (Vector((min(xs), min(ys), min(zs))) * CELL, Vector((max(xs) + 1, max(ys) + 1, max(zs) + 1)) * CELL)


def merge_acc(src, dst, livery=None):
    """Move every part of src into dst, applying a livery suffix to the painted tags."""
    for (g, tag), bucket in src.parts.items():
        t = f"{tag}@{livery}" if livery and tag in LIVERY_TAGS else tag
        dst_b = dst.parts.setdefault((g, t), [[], [], []])
        if len(bucket) == 4 and len(dst_b) == 3:
            dst_b.append([0.0] * len(dst_b[0]))
        base = len(dst_b[0])
        dst_b[0].extend(bucket[0])
        dst_b[1].extend(tuple(i + base for i in f) for f in bucket[1])
        dst_b[2].extend(bucket[2])
        if len(dst_b) == 4:
            dst_b[3].extend(bucket[3] if len(bucket) == 4 else [0.0] * len(bucket[0]))


def realize(ship, S, acc, group, greebles=True, plumes=True, nav=True):
    if hasattr(ship, "realize"):
        return ship.realize(S, acc, group, plumes=plumes)
    solid = set(ship.cells) | ship.mod_cells
    craters = ship.craters

    def tag_of(c, d):
        if craters:
            p = (Vector(c) + Vector((0.5, 0.5, 0.5))) * CELL
            for cc, r in craters:
                if (p - cc).length < r + 1.2 * CELL:
                    return "interior"
        return ship.cells[c]

    sub = Acc()
    out, exposed = mesh_cells(ship.cells, ship.shapes, tag_of=tag_of, solid=solid)
    for tag, vf in out.items():
        if tag == "interior":
            fade = []
            for v in vf[0]:
                best = 0.0
                for cc, r in craters:
                    d = (Vector(v) - cc).length
                    best = max(best, 1.0 - max(0.0, d - r * 0.7) / (r * 0.9 + 0.3))
                fade.append(best)
            sub.add_fade(group, "interior", vf, None, fade)
        else:
            sub.add(group, tag, vf)
    for kind, mn, Rm, size, rsz, remap in ship.mods:
        M = ship.mod_matrix(mn, Rm, size, rsz)
        build_block(kind, S, sub, group, M, size, remap)
        if plumes and kind in ("thruster_s", "thruster_l"):
            W, H = size[0] * CELL, size[2] * CELL
            r = min(W, H) * 0.42
            p = M @ Vector((W / 2, -0.05, H / 2))
            d = (M.to_3x3() @ Vector((0, -1, 0))).normalized()
            ship.plumes.append((p, d, r, 6.0 if kind == "thruster_l" else 3.0))
    for p, d, r, L in ship.plumes:
        plume(sub, group, p, d, r, L, S)
    for fn in ship.extra:
        fn(sub, group, S)
    if greebles:
        add_greebles(ship, S, sub, group, exposed)
    if nav:
        add_nav(ship, S, sub, group)
    merge_acc(sub, acc, ship.livery)
    return exposed


def plume(acc, group, p0, d, r, length, S):
    seg = 16
    prof = [(r * 0.92, 0.0), (r * 0.75, length * 0.2), (r * 0.42, length * 0.6), (r * 0.05, length)]
    v, f = lathe(prof, tuple(p0), tuple(d), seg, False, False)
    fade = []
    for _, h in prof:
        fade += [1.0 - h / length] * seg
    acc.add_fade(group, "plume", (v, f), None, fade)
    prof2 = [(r * 0.45, 0.0), (r * 0.3, length * 0.15), (r * 0.02, length * 0.4)]
    v2, f2 = lathe(prof2, tuple(p0), tuple(d), seg, False, False)
    fade2 = []
    for _, h in prof2:
        fade2 += [1.0 - h / (length * 0.4)] * seg
    acc.add_fade(group, "plume", (v2, f2), None, fade2)


# ----------------------------------------------------------------------------------------------------------------------
# Procedural hull detail, placed in coherent zones (window rows, pipe runs, vent clusters) rather than a random scatter
# ----------------------------------------------------------------------------------------------------------------------

def add_greebles(ship, S, acc, group, exposed):
    dens = dict(S["greeble"])
    dens.update(getattr(ship, "greeble_override", {}) or {})
    seed = ship.seed
    I = Matrix.Identity(4)
    for c, d in exposed:
        if d[2] < 0 or ship.cells.get(c) in ("accent", "trim"):
            continue
        x, y, z = c
        side = "top" if d[2] > 0 else ("side" if d[0] != 0 else "end")
        zone = (y // 6, d[0], d[1], d[2], (x // 6) if side == "top" else 0)

        def h(k):
            return hash01(*zone, seed, k)

        kind = None
        if side == "side":
            if "window" in dens and z % 3 == 1 and h(1) < dens["window"] * 4.5 and hash01(x, y, z, seed) < 0.9:
                kind = "window"
            elif "rib" in dens and y % 2 == 0 and h(2) < dens["rib"] * 3:
                kind = "rib"
            elif "vent" in dens and z % 3 != 1 and h(3) < dens["vent"] * 3 and hash01(x, y, z, seed, 3) < 0.6:
                kind = "vent"
            elif "pipe" in dens and z % 3 == 2 and h(4) < dens["pipe"] * 3:
                kind = "pipe"
            elif "fairing" in dens and z % 4 == 2 and h(5) < dens["fairing"] * 4:
                kind = "fairing"
        elif side == "top":
            if "pipe" in dens and x % 4 == 1 and h(6) < dens["pipe"] * 3:
                kind = "pipe"
            elif "vent" in dens and h(7) < dens["vent"] * 2.5 and hash01(x, y, z, seed, 7) < 0.55:
                kind = "vent"
            elif "stripe" in dens and y % 12 == 0 and h(8) < dens["stripe"] * 12:
                kind = "stripe"
            elif "hatch" in dens and hash01(x, y, z, seed, 8) < dens["hatch"]:
                kind = "hatch"
            elif "plate" in dens and h(9) < dens["plate"] * 3 and hash01(x, y, z, seed, 9) < 0.5:
                kind = "plate"
            elif "handrail" in dens and x % 5 == 0 and hash01(x, y, seed, 10) < dens["handrail"] * 2:
                kind = "handrail"
        else:
            if "vent" in dens and hash01(x, y, z, seed, 11) < dens["vent"] * 2:
                kind = "vent"
            elif "hatch" in dens and hash01(x, y, z, seed, 12) < dens["hatch"] * 2:
                kind = "hatch"
        if kind is None:
            for k in ("light", "strobe", "antenna", "spike"):
                if k in dens and hash01(x, y, z, d[0] * 3 + d[1] * 5 + d[2] * 7, seed, len(k)) < dens[k]:
                    if k in ("antenna", "spike") and side != "top":
                        continue
                    kind = k
                    break
        if kind:
            greeble_face(S, acc, group, I, c, d, kind, hash01(x, z, y, 99), run=(y, 0))


def add_nav(ship, S, acc, group):
    if not ship.cells:
        return
    cells = ship.cells
    xmin = min(c[0] for c in cells)
    xmax = max(c[0] for c in cells)
    for xe, tag, sgn in ((xmin, "nav_r", -1), (xmax, "nav_g", 1)):
        cand = [c for c in cells if c[0] == xe]
        c = max(cand, key=lambda q: (q[2], q[1]))
        p = (Vector(c) + Vector((0.5 + 0.55 * sgn, 0.5, 0.5))) * CELL
        acc.add(group, tag, sphere(tuple(p), 0.07, 8, 4), None, True)
    top = max(cells, key=lambda q: (q[2], -q[1]))
    p = (Vector(top) + Vector((0.5, 0.5, 1.1))) * CELL
    acc.add(group, "nav_w", sphere(tuple(p), 0.06, 8, 4), None, True)


# ======================================================================================================================
# Designs. "light" ships ~15-20 m, "heavy" ~45-55 m.
# ======================================================================================================================

CONTAINER_TINTS = [None, "hull2", "paint_p", "paint_w", "accent", None, "hull"]


def drydock_light():
    """MULE: salvage gunship. Chunky chamfered hull, sponsons, harpoon on the nose, rotary chin gun."""
    s = Ship("MULE", 11, "yard")
    s.loft([(0, 5, 0, 7, 2, 1), (28, 5, 0, 7, 2, 1), (30, 4, 1, 6, 2, 1), (36, 2, 2, 5, 1, 0)])
    s.loft([(-5, 6, 0, 8, 2, 1), (1, 6, 0, 8, 2, 1), (3, 5, 0, 7, 2, 1)], "hull2")
    s.loft([(8, 3, 7, 9, 1), (22, 3, 7, 9, 1), (24, 2, 7, 8, 1)], "armor")
    s.loft([(1, 2, 1, 5, 1, 1), (19, 2, 1, 5, 1, 1), (21, 1, 2, 4, 0, 0)], "hull2", cx=8, mirror=True)
    s.box(5, 6, 4, 14, 2, 4, "trim", mirror=True)
    s.paint(lambda x, y, z: z < 2 and y > 3, "hull2")
    s.paint(lambda x, y, z: 25 <= y < 26 and z > 5, "accent")
    s.mod("cockpit", -2, 24, 7)
    s.mod("gatling", -1, 27, -2)
    s.mod("harpoon", -1, 33, 4)
    s.mod("autocannon", 7, 11, 5, mirror=True)
    s.mod("thruster_l", -2, -11, 2)
    s.mod("thruster_s", 7, -2, 2, mirror=True)
    s.mod("radiator", 3, -4, 8, mirror=True)
    s.mod("cargo", 1, 2, 7, mirror=True)
    s.mod("sensor", -1, 18, 9)
    s.mod("shield", -1, 10, 9)
    s.mod("core_2", -1, 14, 2)
    s.mod("reactor", -1, 4, 2)
    return s


def drydock_heavy():
    """BULWARK: container frigate. Bridge castle aft, a deck of colour-coded containers, twin turret, keel mass driver."""
    s = Ship("BULWARK", 12, "oxide")
    s.loft([(0, 7, 0, 11, 2, 2), (76, 7, 0, 11, 2, 2), (82, 6, 1, 10, 2, 2), (92, 3, 3, 8, 1, 1)])
    s.loft([(-8, 9, 0, 13, 3, 2), (6, 9, 0, 13, 3, 2), (10, 7, 0, 11, 2, 2)], "hull2")
    s.loft([(10, 5, 11, 21, 1), (20, 5, 11, 21, 1), (22, 4, 11, 20, 1)])
    s.box(5, 9, 14, 20, 18, 20, "hull2", mirror=True)
    s.loft([(28, 2, 3, 8, 1, 1), (70, 2, 3, 8, 1, 1), (74, 1, 4, 7, 0, 0)], "hull2", cx=9, mirror=True)
    s.loft([(54, 2, -3, 0, 0, 1), (74, 2, -3, 0, 0, 1)], "armor")
    s.paint(lambda x, y, z: z < 3, "hull2")
    s.paint(lambda x, y, z: 76 <= y < 78 and z > 7, "accent")
    for row in range(8):
        y = 26 + row * 5
        for col, x in enumerate((-6, -4, -2, 0, 2, 4)):
            if hash01(row, col, 9) < 0.12:
                continue
            for k in range(1 + int(hash01(row, col, 3) < 0.6)):
                tint = CONTAINER_TINTS[int(hash01(row, col, k, 5) * len(CONTAINER_TINTS))]
                s.mod("cargo", x, y, 11 + k * 2, remap={"c_cargo": tint} if tint else None)
    s.mod("heavy_cannon", -2, 70, 11)
    s.mod("mass_driver", -2, 74, -3)
    s.mod("cockpit", -2, 16, 21)
    s.mod("flak", 5, 15, 20, mirror=True)
    s.mod("sensor", 1, 11, 21)
    s.mod("shield", -3, 11, 21)
    s.mod("autocannon", 8, 34, 8, mirror=True)
    s.mod("autocannon", 8, 58, 8, mirror=True)
    for x in (-6, -2, 2):
        for z in (1, 7):
            s.mod("thruster_l", x, -14, z)
    s.mod("radiator", 5, -6, 13, mirror=True)
    s.mod("tesla", -1, -4, 13)
    s.mod("core_3", -2, 40, 3)
    s.mod("reactor", -2, 0, 3)
    return s


def nocturne_light():
    """WRAITH: hunched dropship. Heavy cowled nose, stub wings with hanging gun pods, twin canted tail fins."""
    s = Ship("WRAITH", 21, "company")
    s.loft([(0, 4, 0, 7, 2, 1), (20, 4, 0, 8, 3, 1), (26, 4, 0, 7, 3, 2), (32, 2, 1, 5, 2, 1), (35, 1, 2, 4, 1, 0)])
    s.loft([(-6, 5, 0, 7, 2, 2), (2, 5, 0, 8, 3, 2)], "hull2")
    s.loft([(5, 3, 1, 3, 1, 0), (14, 3, 1, 3, 1, 0), (16, 2, 1, 3, 1, 0)], "armor", cx=7, mirror=True)
    s.loft([(-4, 0.5, 8, 14, 0, 0), (2, 0.5, 8, 10, 0, 0)], "hull2", cx=3.5, mirror=True)
    s.paint(lambda x, y, z: z < 1.5, "armor")
    s.paint(lambda x, y, z: 18 <= y < 19, "accent")
    s.mod("cockpit", -2, 22, 7)
    s.mod("gatling", 8, 9, -1, mirror=True)
    s.mod("autocannon", -1, 28, -2)
    s.mod("thruster_l", -2, -12, 1)
    s.mod("thruster_s", 8, 2, 1, mirror=True)
    s.mod("sensor", -1, 12, 8)
    s.mod("core_1", 0, 14, 2)
    return s


def nocturne_heavy():
    """CANTICLE: refinery tug. Top-heavy castle with gothic spires, sloped prow, five-nozzle engine block, keel gun."""
    s = Ship("CANTICLE", 22, "company")
    s.loft([(0, 9, 0, 13, 3, 2), (56, 9, 0, 13, 3, 2), (70, 7, 1, 12, 3, 2), (86, 4, 3, 9, 2, 1), (92, 2, 4, 7, 1, 0)])
    s.loft([(8, 7, 13, 20, 2), (44, 7, 13, 20, 2), (50, 5, 13, 18, 2), (54, 3, 13, 16, 1)], "hull2")
    s.loft([(-10, 11, -2, 16, 4, 3), (2, 11, -2, 16, 4, 3), (6, 9, 0, 13, 3, 2)], "armor")
    s.loft([(30, 3, -3, 1, 0, 1), (70, 3, -3, 1, 0, 1)], "armor")
    s.box(-2, 1, 70, 82, 0, 2, "armor")
    for y, top in ((12, 38), (22, 34), (32, 30)):
        s.spire(0, y, 2, 20, top, 1.5, "hull")
    for sx in (-1, 1):
        s.spire(sx * 6, 4, 1, 16, 26, 2.0, "armor")
    s.paint(lambda x, y, z: z < 2.5, "armor")
    s.paint(lambda x, y, z: 60 <= y < 61 and z > 9, "accent")
    s.mod("heavy_cannon", -2, 55, 13)
    s.mod("mass_driver", -2, 70, -3)
    s.mod("cockpit", -2, 46, 18)
    s.mod("flak", 2, 38, 20, mirror=True)
    s.mod("tesla", -1, 7, 20)
    s.mod("sensor", -1, 40, 20)
    for x, z in ((-7, 5), (-2, 5), (3, 5), (-5, 0), (1, 0)):
        s.mod("thruster_l", x, -16, z)
    s.mod("railgun", 9, 20, 9, mirror=True)
    s.mod("core_35", -2, 30, 3)
    s.mod("reactor", -2, 0, 4)
    return s


def opera_light():
    """LARK: courier. Octagonal fuselage, 45-degree swept wings, bubble cab, twin sail fins, wingtip engines."""
    s = Ship("LARK", 31, "crown")
    s.loft([(-6, 2, 2, 5, 1, 1), (-2, 3, 1, 6, 2, 2), (18, 3, 1, 6, 2, 2), (24, 2, 2, 5, 1, 1), (28, 1, 3, 4, 0, 0)])
    # swept wing: leading edge y <= 17 - (x - 3), trailing edge y >= 1, thickness 2..4 with a chamfered tip
    wing = [((-1, 0, 0), -3), ((1, 0, 0), 14), ((1, 1, 0), 20), ((0, -1, 0), -1), ((0, 0, -1), -2), ((0, 0, 1), 4),
            ((1, 0, 1), 17)]
    s.convex(wing, ((3, 1, 2), (14, 17, 4)), "hull2", mirror=True)
    s.mod("cockpit", -2, 15, 6)
    s.mod("thruster_l", -2, -12, 1)
    s.mod("thruster_s", 11, -2, 2, mirror=True)
    s.mod("autocannon", 4, 8, 4, mirror=True)
    s.mod("beam", -1, 8, 6)
    s.mod("radiator", 2, -5, 6, mirror=True)
    s.mod("core_1", 0, 10, 3)
    return s


def opera_heavy():
    """SOVEREIGN: monumental capital ship. Geodesic command sphere, long spine with a spinal gun, ring, engine monolith."""
    s = Ship("SOVEREIGN", 32, "guild")
    s.ball((0, 80, 8), 12.5, "hull")
    s.loft([(56, 4, 4, 12, 3, 3), (70, 4, 4, 12, 3, 3)], "hull2")
    s.loft([(10, 3, 5, 11, 2, 2), (58, 3, 5, 11, 2, 2)], "hull2")
    s.ring((0, 30, 8), "y", 11, 15, 2, 24, ("hull", "hull", "hull2"))
    s.box(3, 11, 29, 31, 7, 9, "trim", mirror=True)
    s.box(-1, 1, 29, 31, 11, 19, "trim")
    s.box(-1, 1, 29, 31, -3, 5, "trim")
    s.loft([(-6, 9, -1, 17, 4, 4), (10, 9, -1, 17, 4, 4), (14, 6, 2, 14, 3, 3)], "hull")
    s.paint(lambda x, y, z: -1 <= y < 3, "hull2")
    s.mod("mass_driver", -2, 34, 11)
    s.mod("heavy_cannon", -2, 1, 17)
    s.mod("missile", 2, -5, 17, mirror=True)
    s.mod("cockpit", -2, 87, 6)
    s.mod("sensor", -1, 78, 20)
    s.mod("shield", -1, 72, 19)
    for x, z in ((-6, 1), (2, 1), (-6, 11), (2, 11), (-2, 6)):
        s.mod("thruster_l", x, -12, z)
    s.mod("core_3", -2, 78, 6)
    return s


SHIPS = {
    "drydock": (drydock_light, drydock_heavy),
    "nocturne": (nocturne_light, nocturne_heavy),
    "opera": (opera_light, opera_heavy),
}
