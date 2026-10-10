"""Ships and stations as voxel cells (0.5 m) plus functional blocks, and the per-direction designs.

Coordinates in cells: x right, y forward (bow at +y, exhaust toward -y), z up; the ship is symmetric about x = 0
(cell i mirrors cell -1-i)."""

import math
from mathutils import Matrix, Vector

from ld_geom import CELL, Acc, auto_slopes, mesh_cells, hash01, DIRS, cbox, box, cyl, sphere, ico, torus, lathe
from ld_blocks import KIT, build_block, greeble_face


class Ship:
    def __init__(self, name, seed=1):
        self.name = name
        self.cells = {}
        self.mods = []          # (kind, min_cell, euler_deg, size)
        self.mod_cells = set()
        self.seed = seed
        self.craters = []       # (centre metres, radius metres)
        self.plumes = []        # (pos metres, dir, radius, length)
        self.extra = []         # callables(acc, group, S) for custom geometry (trusses, dishes, ...)

    # cell shapes -----------------------------------------------------------------------------------------------
    def fill(self, pred, xr, yr, zr, tag="hull", mirror=False):
        for x in range(*xr):
            for y in range(*yr):
                for z in range(*zr):
                    if pred(x + 0.5, y + 0.5, z + 0.5):
                        self.set((x, y, z), tag)
                        if mirror:
                            self.set((-1 - x, y, z), tag)

    def set(self, c, tag):
        if c not in self.mod_cells:
            self.cells[c] = tag

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
                    self.cells.pop((x, y, z), None)
                    if mirror:
                        self.cells.pop((-1 - x, y, z), None)

    def paint(self, pred, tag):
        for c in list(self.cells):
            if pred(c[0] + 0.5, c[1] + 0.5, c[2] + 0.5):
                self.cells[c] = tag

    def ellipsoid(self, cx, cy, cz, rx, ry, rz, tag="hull", shell=None):
        def pred(x, y, z):
            d = ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2 + ((z - cz) / rz) ** 2
            if shell is None:
                return d <= 1
            return d <= 1 and (((x - cx) / (rx - shell)) ** 2 + ((y - cy) / (ry - shell)) ** 2 + ((z - cz) / (rz - shell)) ** 2) > 1
        self.fill(pred, (int(cx - rx) - 1, int(cx + rx) + 2), (int(cy - ry) - 1, int(cy + ry) + 2),
                  (int(cz - rz) - 1, int(cz + rz) + 2), tag)

    def cyl_y(self, cx, cz, r, y0, y1, tag="hull", r1=None, shell=None, mirror=False):
        r1 = r if r1 is None else r1

        def pred(x, y, z):
            t = (y - y0) / max(1e-6, (y1 - y0))
            rr = r + (r1 - r) * t
            d = math.hypot(x - cx, z - cz)
            return d <= rr and (shell is None or d > rr - shell)
        m = max(r, r1)
        self.fill(pred, (int(cx - m) - 1, int(cx + m) + 2), (y0, y1), (int(cz - m) - 1, int(cz + m) + 2), tag, mirror)

    def cyl_z(self, cx, cy, r, z0, z1, tag="hull", r1=None, shell=None):
        r1 = r if r1 is None else r1

        def pred(x, y, z):
            t = (z - z0) / max(1e-6, (z1 - z0))
            rr = r + (r1 - r) * t
            d = math.hypot(x - cx, y - cy)
            return d <= rr and (shell is None or d > rr - shell)
        m = max(r, r1)
        self.fill(pred, (int(cx - m) - 1, int(cx + m) + 2), (int(cy - m) - 1, int(cy + m) + 2), (z0, z1), tag)

    def torus_z(self, cx, cy, cz, R, r, tag="hull", shell=None):
        def pred(x, y, z):
            q = math.hypot(x - cx, y - cy) - R
            d = math.hypot(q, z - cz)
            return d <= r and (shell is None or d > r - shell)
        m = R + r
        self.fill(pred, (int(cx - m) - 1, int(cx + m) + 2), (int(cy - m) - 1, int(cy + m) + 2), (int(cz - r) - 1, int(cz + r) + 2), tag)

    def torus_y(self, cx, cy, cz, R, r, tag="hull", shell=None):
        def pred(x, y, z):
            q = math.hypot(x - cx, z - cz) - R
            d = math.hypot(q, y - cy)
            return d <= r and (shell is None or d > r - shell)
        m = R + r
        self.fill(pred, (int(cx - m) - 1, int(cx + m) + 2), (int(cy - r) - 1, int(cy + r) + 2), (int(cz - m) - 1, int(cz + m) + 2), tag)

    def beam_line(self, p0, p1, w, tag="trim"):
        """Square-section voxel beam between two cell points."""
        a, b = Vector(p0), Vector(p1)
        n = int((b - a).length) + 1
        for i in range(n + 1):
            p = a + (b - a) * (i / max(1, n))
            for dx in range(w):
                for dy in range(w):
                    for dz in range(w):
                        self.set((int(math.floor(p.x)) + dx - w // 2, int(math.floor(p.y)) + dy - w // 2, int(math.floor(p.z)) + dz - w // 2), tag)

    # modules -----------------------------------------------------------------------------------------------------
    def mod(self, kind, x, y, z, rot=(0, 0, 0), mirror=False, size=None):
        size = size or KIT[kind]
        Rm = Matrix.Identity(3)
        for ax, deg in zip("XYZ", rot):
            if deg:
                Rm = Matrix.Rotation(math.radians(deg), 3, ax) @ Rm
        rs = Rm @ Vector(size)
        rsz = tuple(int(round(abs(c))) for c in rs)
        self._place(kind, (x, y, z), Rm, size, rsz)
        if mirror:
            Mx = Matrix.Diagonal((-1, 1, 1))
            self._place(kind, (-x - rsz[0], y, z), Rm, size, rsz, mirror_geom=False)

    def _place(self, kind, mn, Rm, size, rsz, mirror_geom=False):
        for dx in range(rsz[0]):
            for dy in range(rsz[1]):
                for dz in range(rsz[2]):
                    c = (mn[0] + dx, mn[1] + dy, mn[2] + dz)
                    self.cells.pop(c, None)
                    self.mod_cells.add(c)
        self.mods.append((kind, mn, Rm, size, rsz))

    def mod_matrix(self, mn, Rm, size, rsz):
        half_local = Vector(size) * (CELL / 2)
        centre = Vector(mn) * CELL + Vector(rsz) * (CELL / 2)
        return Matrix.Translation(centre) @ Rm.to_4x4() @ Matrix.Translation(-half_local)

    # damage --------------------------------------------------------------------------------------------------------
    def carve(self, centre_cells, radius_cells, seed=0):
        cx, cy, cz = centre_cells
        removed = []
        r2 = radius_cells
        for c in list(self.cells):
            d = math.dist((c[0] + 0.5, c[1] + 0.5, c[2] + 0.5), (cx, cy, cz))
            jag = (hash01(c[0], c[1], c[2], seed) - 0.5) * 1.6
            if d < r2 + jag:
                removed.append((c, self.cells.pop(c)))
        keep = []
        for m in self.mods:
            kind, mn, Rm, size, rsz = m
            mc = Vector(mn) + Vector(rsz) * 0.5
            if (mc - Vector((cx, cy, cz))).length < r2 * 0.8:
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


SLOPE_RULES = {
    "industrial": lambda c, ds: all(d[2] >= 0 for d in ds) and not any(d[1] < 0 for d in ds) and any(d[2] > 0 for d in ds),
    "sleek": lambda c, ds: not any(d[1] < 0 for d in ds),
    "retro": lambda c, ds: True,
    "nasa": lambda c, ds: False,
    "organic": lambda c, ds: True,
    "lowpoly": lambda c, ds: True,
}


def realize(ship, S, acc, group, greebles=True, plumes=True, nav=True):
    shape = S["shape"]
    solid = set(ship.cells) | ship.mod_cells
    rule = SLOPE_RULES[shape]
    shapes = auto_slopes(ship.cells, allow=rule, filled=solid)
    craters = ship.craters

    def tag_of(c, d):
        if craters:
            p = (Vector(c) + Vector((0.5, 0.5, 0.5))) * CELL
            for cc, r in craters:
                if (p - cc).length < r + 1.2 * CELL:
                    return "interior"
        return ship.cells[c]

    out, exposed = mesh_cells(ship.cells, shapes, tag_of=tag_of, solid=solid)
    for tag, vf in out.items():
        if tag == "interior":
            fade = []
            for v in vf[0]:
                best = 0.0
                for cc, r in craters:
                    d = (Vector(v) - cc).length
                    best = max(best, 1.0 - max(0.0, d - r * 0.7) / (r * 0.9 + 0.3))
                fade.append(best)
            acc.add_fade(group, "interior", vf, None, fade)
        else:
            acc.add(group, tag, vf)
    I = Matrix.Identity(4)
    for kind, mn, Rm, size, rsz in ship.mods:
        build_block(kind, S, acc, group, ship.mod_matrix(mn, Rm, size, rsz), size)
        if plumes and kind in ("thruster_s", "thruster_l"):
            M = ship.mod_matrix(mn, Rm, size, rsz)
            W, H = size[0] * CELL, size[2] * CELL
            r = min(W, H) * 0.42
            p = M @ Vector((W / 2, -0.05, H / 2))
            d = (M.to_3x3() @ Vector((0, -1, 0))).normalized()
            L = (6.0 if kind == "thruster_l" else 3.0) * (1.3 if shape in ("retro", "lowpoly") else 1.0)
            ship.plumes.append((p, d, r, L))
    for p, d, r, L in ship.plumes:
        plume(acc, group, p, d, r, L, S)
    for fn in ship.extra:
        fn(acc, group, S)
    if greebles:
        add_greebles(ship, S, acc, group, exposed, shapes)
    if nav:
        add_nav(ship, S, acc, group)
    return exposed


def plume(acc, group, p0, d, r, length, S):
    seg = 16
    prof = [(r * 0.92, 0.0), (r * 0.75, length * 0.2), (r * 0.42, length * 0.6), (r * 0.05, length)]
    v, f = lathe(prof, tuple(p0), tuple(d), seg, False, False)
    fade = []
    for _, h in prof:
        fade += [1.0 - h / length] * seg
    acc.add_fade(group, "plume", (v, f), None, fade)
    # hot core
    prof2 = [(r * 0.45, 0.0), (r * 0.3, length * 0.15), (r * 0.02, length * 0.4)]
    v2, f2 = lathe(prof2, tuple(p0), tuple(d), seg, False, False)
    fade2 = []
    for _, h in prof2:
        fade2 += [1.0 - h / (length * 0.4)] * seg
    acc.add_fade(group, "plume", (v2, f2), None, fade2)


GREEBLE_SIDES = {
    "window": "side", "porthole": "side", "pore": "any", "hatch": "any", "vent": "top_side", "pipe": "top_side",
    "plate": "any", "light": "any", "antenna": "top", "stripe": "top", "lightline": "side", "handrail": "any",
    "foil": "any", "vein": "any", "spine": "top", "stud": "any",
}


def add_greebles(ship, S, acc, group, exposed, shapes):
    dens = S["greeble"]
    kinds = [(k, p) for k, p in dens.items() if p > 0]
    if not kinds:
        return
    I = Matrix.Identity(4)
    seed = ship.seed
    for c, d in exposed:
        if d[2] < 0:
            continue
        side = "top" if d[2] > 0 else ("side" if d[0] != 0 else "end")
        # coherent runs: windows in rows, pipes along the length
        row_key = (c[0] if d[0] == 0 else d[0], c[2], d[0], d[1], d[2])
        chosen = None
        for k, p in kinds:
            where = GREEBLE_SIDES.get(k, "any")
            if where == "side" and side != "side":
                continue
            if where == "top" and side != "top":
                continue
            if where == "top_side" and side == "end":
                continue
            if k in ("window", "pipe", "lightline", "vein"):
                h = hash01(*row_key, c[1] // 7, seed, len(k))
                if h < p * 1.6 and (k != "window" or hash01(c[1], c[2], seed) < 0.85):
                    chosen = k
                    break
            else:
                if hash01(c[0], c[1], c[2], d[0] * 3 + d[1] * 5 + d[2] * 7, seed, len(k)) < p:
                    chosen = k
                    break
        if chosen:
            greeble_face(S, acc, group, I, c, d, chosen, hash01(c[0], c[2], c[1], 99), run=(c[1], 0))


def add_nav(ship, S, acc, group):
    if not ship.cells:
        return
    mn, mx = ship.bounds()
    cells = ship.cells
    # port (red, -x) and starboard (green, +x) at the widest points; white at the stern top
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
# Designs. Each returns a Ship. "light" ships ~15-20 m, "heavy" ~40-50 m, stations 100-250 m.
# ======================================================================================================================

def drydock_light():
    s = Ship("MULE", 11)
    s.box(-5, 5, 0, 30, 0, 6)
    s.box(-4, 4, 30, 34, 0, 5)
    s.box(-3, 3, 34, 38, 0, 3)
    s.paint(lambda x, y, z: z < 2, "hull2")
    s.box(-5, 5, 8, 24, 6, 7, "hull2")
    s.box(5, 8, -2, 16, 1, 5, "hull2", mirror=True)
    s.box(5, 7, 16, 20, 2, 4, "hull2", mirror=True)
    s.mod("cockpit", -2, 28, 5)
    s.mod("autocannon", 3, 24, 6, mirror=True)
    s.mod("missile", 1, 13, 7, mirror=True)
    s.mod("cargo", 2, 1, 6, mirror=True)
    s.mod("thruster_l", -2, -6, 1)
    s.mod("thruster_s", 5, -5, 2, mirror=True)
    s.mod("radiator", 8, 2, 1, mirror=True)
    s.mod("sensor", -1, 9, 7)
    s.mod("shield", -1, 18, 7)
    s.mod("reactor", -1, 6, 2)
    s.mod("core_2", -1, 14, 2)
    return s


def drydock_heavy():
    s = Ship("BULWARK", 12)
    s.box(-6, 6, 0, 80, 0, 10)
    s.box(-5, 5, 80, 88, 0, 8)
    s.box(-4, 4, 88, 93, 1, 6)
    s.paint(lambda x, y, z: z < 3, "hull2")
    for i in range(4):
        tag = "accent" if i % 2 == 0 else "armor"
        s.box(6, 14, 12 + i * 15, 25 + i * 15, 0, 8, tag, mirror=True)
        s.box(6, 14, 25 + i * 15, 27 + i * 15, 1, 7, "hull2", mirror=True)
    s.box(-4, 4, 56, 68, 10, 18)
    s.box(-3, 3, 58, 66, 18, 20, "hull2")
    s.box(-11, 11, -6, 8, 0, 12, "hull2")
    s.mod("cockpit", -2, 64, 18)
    for x in (1, 6):
        s.mod("thruster_l", x, -12, 1, mirror=True)
        s.mod("thruster_l", x, -12, 6, mirror=True) if x == 1 else None
    s.mod("flak", -1, 72, 10)
    s.mod("flak", -1, 40, 10)
    s.mod("railgun", -1, 86, 8)
    s.mod("autocannon", 9, 22, 8, mirror=True)
    s.mod("autocannon", 9, 52, 8, mirror=True)
    s.mod("missile", 2, 76, 10, mirror=True)
    s.mod("radiator", 11, -4, 3, mirror=True)
    s.mod("radiator", 11, 2, 3, mirror=True)
    s.mod("sensor", -1, 60, 20)
    s.mod("shield", 3, 30, 10, mirror=True)
    s.mod("shield", -1, 82, 8)
    s.mod("reactor", -2, 4, 12)
    s.mod("capacitor", 4, 4, 12, mirror=True)
    s.mod("core_3", -2, 30, 4)
    s.mod("cargo", 1, 12, 10, mirror=True)
    return s


def nocturne_light():
    s = Ship("KESTREL", 21)

    def pred(x, y, z):
        ax = abs(x)
        # fuselage: long needle, tapering at 45 degrees into the nose
        if ax < 3.5 - max(0, y - 34) and z < 4.5 - max(0, y - 30) * 0.4 and y < 40:
            return True
        # 45-degree swept wings
        if y < 22 and ax < 17 and ax <= 3 + (22 - y) and ax >= 3 and y >= max(0, ax - 9):
            return z < (2 if ax < 9 else 1)
        return False
    s.fill(pred, (-18, 18), (0, 42), (0, 6))
    s.paint(lambda x, y, z: abs(x) > 8, "hull2")
    s.fill(lambda x, y, z: y < 9 - (z - 4) * 1.0 and y > (z - 4) * 0.8, (4, 5), (0, 10), (4, 11), "hull2", mirror=True)
    s.mod("cockpit", -2, 25, 3)
    s.mod("thruster_s", 1, -3, 1, mirror=True)
    s.mod("thruster_s", 6, 0, 0, rot=(0, 0, 0), mirror=True)
    s.mod("beam", 9, 10, 1, mirror=True)
    s.mod("railgun", -1, 36, 0)
    s.mod("shield", -1, 10, 4)
    s.mod("core_1", 0, 16, 1)
    return s


def nocturne_heavy():
    s = Ship("VIGIL", 22)
    L = 100

    def hw(y):
        if y < 10:
            return 8 + y * 0.2
        if y > 62:
            return max(1.0, 10 * (1 - (y - 62) / 38) ** 0.9)
        return 10

    def pred(x, y, z):
        w = hw(y)
        if abs(x) > w:
            return False
        # hexagonal section
        top = 9 - abs(x) * 0.45
        bot = 0 + abs(x) * 0.3
        if y > 62:
            top -= (y - 62) * 0.12
        return bot <= z < top
    s.fill(pred, (-12, 12), (0, L), (0, 10))
    s.fill(lambda x, y, z: y > 18 + (z - 9) * 1.2 and y < 64 - (z - 9) * 1.6 and z < 26, (-1, 1), (18, 66), (9, 26), "hull2")
    s.fill(lambda x, y, z: y > 20 and y < 50 - (-z) * 0.8 and z > -6, (-1, 1), (20, 52), (-6, 0), "hull2")
    s.paint(lambda x, y, z: z < 2.5, "hull2")
    s.mod("cockpit", -2, 70, 6)
    for x in (-6, -2, 2):
        s.mod("thruster_l", x, -6, 2)
    for i in range(4):
        s.mod("beam", 10, 22 + i * 9, 4, rot=(0, 90, 0), mirror=True)
    s.mod("flak", -1, 60, 7)
    s.mod("flak", -1, 12, 7)
    s.mod("railgun", -1, 86, 3)
    s.mod("shield", -1, 40, 26)
    s.mod("sensor", -1, 76, 5)
    s.mod("radiator", 6, 2, 7, mirror=True)
    s.mod("core_35", -2, 30, 1)
    return s


def pulp_light():
    s = Ship("COMET", 31)
    s.cyl_y(0, 5, 5.2, 0, 24)
    s.cyl_y(0, 5, 5.2, 24, 38, r1=0.8)
    s.paint(lambda x, y, z: y > 31, "armor")
    s.paint(lambda x, y, z: 12 < y < 15, "hull2")
    # three fins: top and two lower diagonals
    s.fill(lambda x, y, z: y < 12 - (z - 9) * 1.1 and y > -3 + (z - 9) * 0.6, (-1, 1), (-3, 13), (9, 17), "armor")
    for sgn in (-1, 1):
        def fpred(x, y, z, sgn=sgn):
            dx, dz = x * sgn, -(z - 5)
            u = (dx + dz) / 1.414
            v = (dx - dz) / 1.414
            return abs(v) < 0.9 and u > 4 and y < 12 - (u - 4) * 1.1 and y > -3 + (u - 4) * 0.6
        s.fill(fpred, (-14, 14), (-3, 13), (-9, 5), "armor")
    s.box(5, 9, 10, 16, 4, 6, "hull2", mirror=True)
    s.mod("cockpit", -2, 23, 9)
    s.mod("thruster_l", -2, -6, 3)
    s.mod("autocannon", 7, 13, 6, mirror=True)
    s.mod("core_1", 0, 18, 4)
    return s


def pulp_heavy():
    s = Ship("BIG BERTHA", 32)
    s.ellipsoid(0, 80, 8, 9, 10, 8)
    s.paint(lambda x, y, z: y > 84, "armor")
    s.box(-4, 4, 10, 74, 2, 10, "hull2")
    colours = ["armor", "accent", "hull", "hull2", "accent"]
    for i in range(5):
        t = colours[i]
        s.box(-10, 10, 14 + i * 12, 24 + i * 12, 0 + (i % 2) * 2, 12 + (i % 2) * 2, t)
    s.cyl_y(0, 7, 9, 0, 10, "hull")
    s.paint(lambda x, y, z: y < 3, "armor")
    s.box(10, 16, 40, 46, 4, 8, "hull2", mirror=True)
    s.cyl_y(16, 6, 3, 30, 56, "hull", mirror=True)
    s.mod("cockpit", -2, 82, 15)
    s.mod("thruster_l", -2, -6, 5)
    s.mod("thruster_l", -7, -6, 1)
    s.mod("thruster_l", 3, -6, 1)
    s.mod("thruster_s", 15, 24, 5, mirror=True)
    s.mod("beam", 15, 56, 5, mirror=True)
    s.mod("flak", -1, 64, 14)
    s.mod("autocannon", 3, 40, 14, mirror=True)
    s.mod("missile", -1, 26, 14)
    s.mod("reactor", -1, 50, 14)
    s.mod("radiator", 10, 2, 8, mirror=True)
    s.mod("sensor", -1, 88, 14)
    s.mod("shield", 5, 74, 12, mirror=True)
    return s


def orbital_light():
    s = Ship("TUG", 41)
    s.cyl_y(0, 4, 4.2, 0, 18, "hull")
    s.cyl_y(0, 4, 5.2, -7, 0, "hull2")
    s.mod("cockpit", -2, 18, 2)
    s.mod("thruster_l", -2, -13, 2)
    s.mod("solar", 5, 2, 2, rot=(0, 0, 90), mirror=True)
    s.mod("solar", 5, 8, 2, rot=(0, 0, 90), mirror=True)
    s.mod("autocannon", -1, 10, 8)
    s.mod("sensor", 2, -4, 9)
    s.mod("radiator", 4, -6, 6, mirror=True)
    for z in (1, 7):
        s.mod("thruster_s", -1, 14, z, rot=(0, 0, 180)) if False else None
    return s


def orbital_heavy():
    s = Ship("PATHFINDER", 42)
    # habitat ring module at the bow
    s.cyl_y(0, 8, 7.2, 70, 92, "hull")
    s.cyl_y(0, 8, 7.2, 92, 98, "hull", r1=3)
    s.cyl_y(0, 8, 4.2, 56, 70, "hull")
    # truss spine (voxel girders)
    for (x, z) in ((-3, 5), (2, 5), (-3, 10), (2, 10)):
        s.box(x, x + 1, 6, 56, z, z + 1, "trim")
    for y in range(6, 56, 5):
        s.box(-3, 3, y, y + 1, 5, 6, "trim")
        s.box(-3, 3, y, y + 1, 10, 11, "trim")
        s.box(-3, -2, y, y + 1, 5, 11, "trim")
        s.box(2, 3, y, y + 1, 5, 11, "trim")
    # propellant tanks (gold foil)
    for y0 in (14, 34):
        s.ellipsoid(-7, y0 + 6, 8, 4.5, 8, 4.5, "hull2")
        s.ellipsoid(6, y0 + 6, 8, 4.5, 8, 4.5, "hull2")
    s.cyl_y(0, 8, 6.2, -6, 6, "hull2")
    s.mod("cockpit", -2, 98, 6)
    s.mod("thruster_l", -2, -12, 6)
    s.mod("thruster_s", -6, -9, 6)
    s.mod("thruster_s", 4, -9, 6)
    for i in range(3):
        s.mod("radiator", 4, 8 + i * 14, 11, mirror=True, rot=(0, 0, 0))
    s.mod("solar", 7, 60, 7, rot=(0, 0, 90), mirror=True)
    s.mod("solar", 7, 64, 7, rot=(0, 0, 90), mirror=True)
    s.mod("sensor", -1, 84, 15)
    s.mod("railgun", -1, 60, 12)
    s.mod("autocannon", -1, 76, 15)
    s.mod("shield", 3, 88, 13, mirror=True)
    s.mod("core_3", -2, 74, 5)

    def dish(acc, group, S):
        c = Vector((0, 50 * CELL, 13 * CELL))
        acc.add(group, "metal", cyl(tuple(c), tuple(c + Vector((0, 0, 1.6))), 0.08, 8), None, True)
        M = Matrix.Translation(c + Vector((0, 0, 1.6))) @ Matrix.Rotation(math.radians(50), 4, "X")
        prof = [(0.0, 0.0), (0.8, 0.08), (1.6, 0.32), (2.0, 0.5)]
        v, f = lathe(prof, (0, 0, 0), (0, 0, 1), 32, False, False)
        acc.add(group, "hull", (v, f), M, True)
        acc.add(group, "metal", cyl((0, 0, 0), (0, 0, 1.2), 0.03, 6), M, True)
    s.extra.append(dish)
    return s


def reef_light():
    s = Ship("RAY", 51)

    def pred(x, y, z):
        e = (x / 17) ** 2 + ((y - 14) / 15) ** 2
        if e > 1:
            return False
        thick = 1.0 + 4.2 * (1 - e) ** 1.4
        return abs(z - 3) < thick / 2 + (0.8 if abs(x) < 4 else 0)
    s.fill(pred, (-18, 18), (-2, 30), (-2, 9))
    # cephalic horns
    s.fill(lambda x, y, z: 3 < abs(x) < 6 and abs(z - 3) < 1.2 and y < 34 - (abs(x) - 3) * 1.5, (-7, 7), (26, 34), (1, 5), "hull2")
    # tail
    s.cyl_y(0, 3, 0.9, -14, -2, "hull2", r1=0.6)
    s.paint(lambda x, y, z: z > 4.5 and abs(x) < 10, "hull2")
    s.mod("cockpit", -2, 16, 5)
    s.mod("thruster_s", 3, -4, 2, mirror=True)
    s.mod("autocannon", 9, 16, 4, mirror=True)
    s.mod("core_2", -1, 8, 2)
    return s


def reef_heavy():
    s = Ship("LEVIATHAN", 52)
    segs = [(0, 6.5), (11, 8.5), (23, 10), (36, 10.5), (49, 10), (61, 9), (72, 7.5), (82, 6)]
    for y, r in segs:
        s.ellipsoid(0, y + 6, 8, r, 8.5, r * 0.85)
    s.ellipsoid(0, 92, 7, 6, 9, 5, "hull2")
    for y, r in segs[1:6]:
        s.fill(lambda x, y_, z, y0=y, r=r: abs(x) < 1 and abs(y_ - y0 - 6) < 3 - (z - 8 - r * 0.8) * 0.4, (-1, 1), (y, y + 13), (int(8 + r * 0.8), int(8 + r * 0.8 + 7)), "hull2")
    # pectoral fins
    s.fill(lambda x, y, z: 8 < abs(x) < 22 and abs(z - 5) < 1 and abs(y - 52 + (abs(x) - 8) * 0.9) < 6 - (abs(x) - 8) * 0.3, (-23, 23), (40, 70), (3, 7), "hull2")
    # tail flukes
    s.fill(lambda x, y, z: abs(z - 8) < 1 and abs(x) < 3 + (-y) * 0.9 and y > -14 + abs(x) * 0.3, (-16, 16), (-14, 2), (6, 10), "hull2")
    s.mod("cockpit", -2, 88, 11)
    s.mod("thruster_l", -2, -4, 6) if False else None
    s.mod("thruster_s", 3, -2, 4, mirror=True)
    s.mod("thruster_s", 3, -2, 10, mirror=True)
    for i in range(3):
        s.mod("beam", 9, 30 + i * 14, 7, rot=(0, 90, 0), mirror=True)
    s.mod("flak", -1, 70, 14)
    s.mod("shield", -1, 40, 18)
    s.mod("core_35", -2, 40, 5)
    return s


def facet_light():
    s = Ship("DART", 61)

    def pred(x, y, z):
        w = 13 * (1 - y / 34)
        if abs(x) > w + 1:
            return False
        h = 2 + 4 * (1 - abs(x) / (w + 1.5)) * (1 - y / 40)
        return z < h
    s.fill(pred, (-15, 15), (0, 34), (0, 7))
    s.paint(lambda x, y, z: abs(x) > 6, "hull2")
    s.paint(lambda x, y, z: y > 26, "accent")
    s.mod("cockpit", -2, 16, 5)
    s.mod("thruster_l", -2, -6, 0)
    s.mod("thruster_s", 6, -3, 0, mirror=True)
    s.mod("autocannon", 8, 8, 2, mirror=True)
    s.mod("missile", -1, 6, 5)
    s.mod("core_1", 0, 12, 1)
    return s


def facet_heavy():
    s = Ship("BASTION", 62)
    tiers = [(0, 86, 15, 0, 5, "hull2"), (6, 78, 12, 5, 10, "hull"), (14, 66, 8, 10, 15, "hull"), (24, 54, 5, 15, 20, "hull2"),
             (30, 46, 3, 20, 26, "accent")]
    for y0, y1, hw, z0, z1, tag in tiers:
        s.box(-hw, hw, y0, y1, z0, z1, tag)
    s.fill(lambda x, y, z: abs(x) < 11 - (y - 86) * 0.9, (-12, 12), (86, 98), (0, 5), "hull2")
    s.fill(lambda x, y, z: abs(x) < 8 - (y - 78) * 0.7, (-9, 9), (78, 90), (5, 10), "hull")
    s.mod("cockpit", -2, 40, 26)
    for x in (-10, -2, 6):
        s.mod("thruster_l", x, -6, 0)
    s.mod("thruster_l", -2, -6, 5) if False else None
    for y in (10, 30, 50, 68):
        s.mod("autocannon", 13, y, 5, mirror=True)
    s.mod("flak", 6, 20, 10, mirror=True)
    s.mod("flak", 6, 56, 10, mirror=True)
    s.mod("railgun", -1, 84, 10)
    s.mod("missile", 1, 26, 15, mirror=True)
    s.mod("missile", 1, 50, 15, mirror=True)
    s.mod("shield", -1, 32, 26)
    s.mod("sensor", 1, 44, 26)
    s.mod("core_3", -2, 40, 2)
    return s


SHIPS = {
    "drydock": (drydock_light, drydock_heavy),
    "nocturne": (nocturne_light, nocturne_heavy),
    "pulp-horizon": (pulp_light, pulp_heavy),
    "orbital": (orbital_light, orbital_heavy),
    "reef": (reef_light, reef_heavy),
    "facet": (facet_light, facet_heavy),
}
