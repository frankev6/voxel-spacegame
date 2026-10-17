#!/usr/bin/env python3
"""RACE-008 / RACE-013: extrude the approved racer plans (racer_silhouettes.py) into shaped voxel cells for the game.

Writes ArtSource/Racing/ships/<id>.json in the look-book format NocturneShipBuilder reads:
  cells: [x, y, z, tag, planes or null] in look-book axes (x right, y forward, z up), 0.5 m cells;
  mods:  [kind, min, rotation, size, size, face] module boxes; face is the nozzle or muzzle direction (look-book axes).
Tags are "#NN" paint indices (DESIGN-SYSTEM §10.13), so each line keeps its livery.

Shaping (owner, 2026-10-17: "you missed to use a lot of the hull block options that could honestly make some of these ships
look less blocky or more sleek where it matters"). Each ship is a height field built from its plan parts:
  - hulls, pods and large plates are domes: low at their outline, rising inward (Vestari 1:2 for long slopes, Khuchad 1:1
    for hard wedges), with a 45-degree chamfer underneath;
  - plates, cabins and gun barrels stand proud of the hull with chamfered edges; off the hull they are bevelled slabs
    (pylons, fins, spokes);
  - every cut cell carries its real cut planes: the dome's tangent on top, the chamfer below, and each outline edge it
    crosses, so the builder seats wedges, hip corners and long slopes along the silhouette as well as the dome.
Engines come from the ADR-0120 line, placed per ship (ENGINES); verniers sit at the ends of long lever arms.

Run:  python3 ArtSource/Racing/export_racers.py
"""
import json
import math
import os

import racer_silhouettes as R

OUT = os.path.join(os.path.dirname(__file__), "ships")
CELL = 0.5

# livery: part kind -> paint index (DESIGN-SYSTEM §10.13, NOCTURNE column)
PAINT = {
    "vestari": dict(base=16, plate=8, frame=1, cab=11, glass=8, gun=1, rad=13, haz=8, num=16),
    "khuchad": dict(base=1, plate=6, frame=11, cab=16, glass=5, gun=11, rad=1, haz=5, num=6),
}
# line style: dome rise (cells up per cell in), lowest edge height, how far proud a plate stands (cells)
STYLE = {"vestari": dict(rise=0.5, edge=1.0), "khuchad": dict(rise=1.0, edge=1.0)}

AFT, FWD, PORT, STBD = (-1, 0), (1, 0), (0, 1), (0, -1)   # plan directions (x forward, y port)
# Engines per ship: (kind, plan x of the nozzle exit or of the vernier, plan y, centre height m, plan facing of the nozzle).
ENGINES = {
    "ship_racer_needle": [
        ("sprint_m", -13.0, 6.5, 1.0, AFT), ("sprint_m", -13.0, -6.5, 1.0, AFT), ("sprint_s", -12.0, 0.0, 1.0, AFT),
        ("vernier", -3.6, 7.6, 1.0, PORT), ("vernier", -3.6, -7.6, 1.0, STBD),
        ("vernier", -11.4, 7.6, 1.0, PORT), ("vernier", -11.4, -7.6, 1.0, STBD)],
    "ship_racer_kite": [
        ("sprint_m", -8.2, 1.25, 0.75, AFT), ("sprint_m", -8.2, 0.0, 0.75, AFT), ("sprint_m", -8.2, -1.25, 0.75, AFT),
        ("vernier", -7.9, 11.5, 0.75, AFT), ("vernier", -7.9, -11.5, 0.75, AFT),
        ("vernier", -6.3, 11.3, 0.75, FWD), ("vernier", -6.3, -11.3, 0.75, FWD)],
    # the ring's eight pods are sprint engines pointing out of the ring: it pushes equally in every direction
    "ship_racer_gimbal": [("sprint_s", -4.2, 0.0, 1.0, AFT)] + [
        ("sprint_s", round(math.cos(a) * 8.0, 2), round(math.sin(a) * 8.0, 2), 1.0,
         (1 if math.cos(a) > 0 else -1, 0) if abs(math.cos(a)) >= abs(math.sin(a)) else (0, 1 if math.sin(a) > 0 else -1))
        for a in [k * math.tau / 8 + math.tau / 16 for k in range(8)]],
    # two torches and a salvaged torch on the port pod (it pulls to port while it spools), a Vestari sprint engine between
    "ship_racer_battering_ram": [
        ("torch_l", -14.4, 3.0, 1.5, AFT), ("torch_l", -14.4, -3.0, 1.5, AFT), ("sprint_m", -14.2, 0.0, 1.5, AFT),
        ("torch_l", -12.4, 6.5, 1.25, AFT),
        ("vernier", 8.7, 7.3, 1.0, PORT), ("vernier", 8.5, -7.3, 1.0, STBD),
        ("vernier", -11.2, 7.3, 1.0, PORT), ("vernier", -11.0, -5.2, 1.0, STBD)],
    # torches inboard for the straights, sprint engines outboard for quick corrections, verniers on the hammer's tips
    "ship_racer_hammerhead": [
        ("torch_l", -17.0, 1.8, 1.25, AFT), ("torch_l", -17.0, -1.8, 1.25, AFT),
        ("sprint_m", -17.0, 5.2, 1.25, AFT), ("sprint_m", -17.0, -5.2, 1.25, AFT),
        ("vernier", 14.6, 11.0, 1.0, FWD), ("vernier", 14.6, -11.0, 1.0, FWD),
        ("vernier", 11.4, 10.8, 1.0, AFT), ("vernier", 11.4, -10.8, 1.0, AFT)],
}
SIZE = {"vernier": (1, 1), "sprint_s": (1, 2), "sprint_m": (2, 3), "torch_l": (3, 4), "torch_xl": (5, 7)}   # width, length (cells)

SHIPS = [
    ("ship_racer_needle", "NEEDLE", R.dart_needle, "vestari", 4),
    ("ship_racer_kite", "KITE", R.dart_kite, "vestari", 3),
    ("ship_racer_gimbal", "GIMBAL", R.dart_gimbal, "vestari", 4),
    ("ship_racer_battering_ram", "BATTERING RAM", R.ram_battering, "khuchad", 7),
    ("ship_racer_hammerhead", "HAMMERHEAD", R.ram_hammer, "khuchad", 6),
]


# ------------------------------------------------------------------------------------------------ geometry (look-book cells)
def lb(p):
    """Plan metres (x forward, y port) -> look-book plan cells (X right, Y forward)."""
    return (-p[1] / CELL, p[0] / CELL)


def area(poly):
    return 0.5 * sum(poly[i - 1][0] * poly[i][1] - poly[i][0] * poly[i - 1][1] for i in range(len(poly)))


def inside(p, poly):
    x, y, c = p[0], p[1], False
    for i in range(len(poly)):
        a, b = poly[i], poly[i - 1]
        if (a[1] > y) != (b[1] > y) and x < (b[0] - a[0]) * (y - a[1]) / (b[1] - a[1] + 1e-12) + a[0]:
            c = not c
    return c


def seg_dist(p, a, b):
    dx, dy = b[0] - a[0], b[1] - a[1]
    t = max(0.0, min(1.0, ((p[0] - a[0]) * dx + (p[1] - a[1]) * dy) / (dx * dx + dy * dy + 1e-12)))
    return math.hypot(p[0] - a[0] - t * dx, p[1] - a[1] - t * dy), (a[0] + t * dx, a[1] + t * dy)


# Lines the kit draws cleanly in plan: along the grid, at 45 degrees (wedges) and at 1:2 (long slopes), each through cell
# corners. a*X + b*Y = c with c whole.
KIT_LINES = [(1, 0), (0, 1), (1, 1), (1, -1), (1, 2), (2, 1), (1, -2), (2, -1)]


def snap_outline(poly, tolerance_deg=8.5):
    """Moves each edge onto the nearest kit line when it is within tolerance_deg of one, then re-intersects neighbours.
    Edges far from every kit angle stay as drawn (they stair-step); a vertex that would move more than 1.5 cells, or a
    polygon whose area would change by more than 20%, keeps the original."""
    n = len(poly)
    lines = []
    for i in range(n):
        a, b = poly[i], poly[(i + 1) % n]
        dx, dy = b[0] - a[0], b[1] - a[1]
        L = math.hypot(dx, dy)
        best = None
        if L > 0.8:
            for (u, v) in KIT_LINES:
                # the line u*X + v*Y = c has direction (v, -u)
                ang = math.degrees(math.acos(min(1.0, abs(dx * v - dy * u) / (L * math.hypot(u, v)))))
                if ang <= tolerance_deg and (best is None or ang < best[0]):
                    mx, my = (a[0] + b[0]) / 2, (a[1] + b[1]) / 2
                    best = (ang, (u, v, float(round(u * mx + v * my))))
        lines.append(best[1] if best else None)
    out = []
    for i in range(n):
        l0, l1 = lines[i - 1], lines[i]   # the edges meeting at vertex i
        p = poly[i]
        q = p
        if l0 and l1:
            det = l0[0] * l1[1] - l0[1] * l1[0]
            if abs(det) > 1e-9:
                q = ((l0[2] * l1[1] - l0[1] * l1[2]) / det, (l0[0] * l1[2] - l0[2] * l1[0]) / det)
        elif l1 or l0:
            u, v, c = l1 or l0   # project the vertex onto its one snapped edge
            k = (u * p[0] + v * p[1] - c) / (u * u + v * v)
            q = (p[0] - k * u, p[1] - k * v)
        out.append(q if math.dist(p, q) <= 1.5 else p)
    if abs(area(out)) < 1e-6 or abs(area(out) - area(poly)) > 0.2 * abs(area(poly)):
        return poly
    return out


class Part:
    """A plan part. `role`: dome (hull, pod, big plate), layer (stands proud of a dome, else a slab), slab (off-hull only)."""
    def __init__(self, kind, poly=None, ring=None):
        self.kind, self.ring = kind, ring
        if poly is not None:
            poly = [lb(p) for p in poly]
            if area(poly) < 0:
                poly = poly[::-1]   # counter-clockwise: the inside is left of each edge
            if abs(area(poly)) >= 4:
                poly = snap_outline(poly)
        self.poly = poly
        if poly is not None:
            xs, ys = [p[0] for p in poly], [p[1] for p in poly]
            self.box = (min(xs), min(ys), max(xs), max(ys))
        else:
            c, (ao, bo) = ring[0], ring[2]
            self.box = (c[0] - ao, c[1] - ao, c[0] + ao, c[1] + ao)

    def contains(self, p):
        if p[0] < self.box[0] or p[0] > self.box[2] or p[1] < self.box[1] or p[1] > self.box[3]:
            return False
        if self.ring:
            return self.oct(p, self.ring[2]) <= 0 and self.oct(p, self.ring[1]) >= 0
        return inside(p, self.poly)

    def oct(self, p, ab):
        """Signed distance (cells, approximately) to the octagon |X| <= a, |Y| <= a, |X| + |Y| <= b about the ring centre."""
        a, b = ab; x, y = abs(p[0] - self.ring[0][0]), abs(p[1] - self.ring[0][1])
        return max(x - a, y - a, (x + y - b) / math.sqrt(2))

    def oct_face(self, p, ab):
        """The octagon face nearest p: (outward unit normal, point on the face)."""
        a, b = ab; cx, cy = self.ring[0]; x, y = p[0] - cx, p[1] - cy
        sx, sy = (1 if x >= 0 else -1), (1 if y >= 0 else -1)
        ds = [(abs(x) - a, (sx, 0.0)), (abs(y) - a, (0.0, sy)), ((abs(x) + abs(y) - b) / math.sqrt(2), (sx / math.sqrt(2), sy / math.sqrt(2)))]
        d, n = max(ds, key=lambda t: t[0])
        return n, (p[0] - n[0] * d, p[1] - n[1] * d)

    def depth(self, p):
        """Distance in cells from p to the part's outline (inside only)."""
        if self.ring:
            return min(-self.oct(p, self.ring[2]), self.oct(p, self.ring[1]))
        return min(seg_dist(p, self.poly[i - 1], self.poly[i])[0] for i in range(len(self.poly)))

    def walls(self, c):
        """Outline half-planes near cell centre c: (outward normal, point on the edge)."""
        out = []
        if self.ring:
            if abs(self.oct(c, self.ring[2])) < 0.9: out.append(self.oct_face(c, self.ring[2]))
            if abs(self.oct(c, self.ring[1])) < 0.9:
                n, q = self.oct_face(c, self.ring[1]); out.append(((-n[0], -n[1]), q))
            return out
        for i in range(len(self.poly)):
            a, b = self.poly[i - 1], self.poly[i]
            d, q = seg_dist(c, a, b)
            if d > 0.9:
                continue
            L = math.hypot(b[0] - a[0], b[1] - a[1]) + 1e-12
            n = ((b[1] - a[1]) / L, -(b[0] - a[0]) / L)   # outward for a counter-clockwise polygon
            if n[0] * (c[0] - a[0]) + n[1] * (c[1] - a[1]) > 0.45:
                continue   # the centre is well outside this edge's line: a concave corner, leave it to the other edge
            out.append((n, q))
        return out


def parts_of(ship):
    glows = [R_centroid(s) for k, s, kw in ship.parts if k == "glow"]
    parts = []
    # GIMBAL's thrust ring is drawn as 48 segments; it is shaped as one annulus so the dome runs round it without seams
    segs = [s for k, s, kw in ship.parts if k == "base" and len(s) == 4 and all(5.8 < math.hypot(*p) < 7.5 for p in s)]
    if len(segs) >= 24:
        # an octagon with whole-cell faces: axis faces at |X|, |Y| = a, diagonal faces at |X| + |Y| = b (b ~ a * sqrt 2)
        rs = [math.hypot(*p) for s in segs for p in s]
        ai, ao = round(min(rs) / CELL), round(max(rs) / CELL)
        parts.append(Part("base", ring=((0.0, 0.0), (ai, round(ai * math.sqrt(2))), (ao, round(ao * math.sqrt(2))))))
    for kind, shape, kw in ship.parts:
        if any(shape is s for s in segs):
            continue
        if kind in ("seam", "line", "num", "glow"):
            continue
        if kind == "frame" and len(shape) == 4 and any(math.dist(R_centroid(shape), g) < 1.2 for g in glows):
            continue   # an engine bell: the engine module goes there
        parts.append(Part(kind, shape))
    return parts


def R_centroid(poly):
    return sum(p[0] for p in poly) / len(poly), sum(p[1] for p in poly) / len(poly)


# ------------------------------------------------------------------------------------------------ the height field
LAYER = dict(plate=1.0, cab=2.0, gun=1.0, frame=0.0, rad=0.0, haz=0.0, glass=0.0)          # cells proud of the dome
SLAB = dict(plate=(1, 3), frame=(1, 3), cab=(1, 3), rad=(1, 2), gun=(1, 2), haz=(1, 2), glass=(1, 2))   # off-hull cells


class Field:
    def __init__(self, parts, style, hmax):
        self.style, self.hmax = style, hmax
        big = lambda p: p.kind in ("plate", "frame", "cab", "armor") and p.poly is not None and abs(area(p.poly)) >= 30
        self.domes = [p for p in parts if p.kind == "base" or big(p)]
        self.others = [p for p in parts if p not in self.domes]
        self.solid = self.domes + [p for p in self.others if p.kind in ("plate", "frame", "rad", "gun", "cab")]
        # Overlapping domes (a prow plate over the hull, a hammer plate across it) are one surface: their depth is measured
        # to the outline of their union, so the dome runs across the join instead of dipping to a valley.
        self.outline = []
        polys = [p for p in self.domes if p.poly is not None]
        for part in polys:
            pts = part.poly
            for i in range(len(pts)):
                a, b = pts[i - 1], pts[i]
                L = math.hypot(b[0] - a[0], b[1] - a[1]); n = max(1, int(L / 0.5))
                nx, ny = (b[1] - a[1]) / (L + 1e-12), -(b[0] - a[0]) / (L + 1e-12)
                for k in range(n):
                    p0 = (a[0] + (b[0] - a[0]) * k / n, a[1] + (b[1] - a[1]) * k / n)
                    p1 = (a[0] + (b[0] - a[0]) * (k + 1) / n, a[1] + (b[1] - a[1]) * (k + 1) / n)
                    mid = ((p0[0] + p1[0]) / 2 + nx * .3, (p0[1] + p1[1]) / 2 + ny * .3)
                    if not any(o is not part and o.contains(mid) for o in polys):
                        self.outline.append((p0, p1))
        self.memo = {}

    def depth(self, part, p):
        if part.poly is None:
            return part.depth(p)
        return min(seg_dist(p, a, b)[0] for a, b in self.outline) if self.outline else part.depth(p)

    def column(self, p):
        """(lo, hi, top part, dome part, dome hi) at plan point p, in cells; None if empty."""
        key = (round(p[0], 4), round(p[1], 4))
        if key not in self.memo:
            self.memo[key] = self._column(p)
        return self.memo[key]

    def _column(self, p):
        rise, edge = self.style["rise"], self.style["edge"]
        lo, hi, top, dome, dome_hi = None, None, None, None, None
        for part in self.domes:
            if not part.contains(p):
                continue
            d = self.depth(part, p)
            h = min(float(self.hmax), edge + rise * d)
            b = max(0.0, 0.75 - d)   # 45-degree chamfer under the outline
            if part.ring:
                h, b = 2.0, 0.0      # a flat band: a dome across a 45-degree face rounds to a checkerboard
            if hi is None or h > hi:
                hi, top, dome, dome_hi = h, part, part, h
            lo = b if lo is None else min(lo, b)
        for part in self.others:
            if not part.contains(p):
                continue
            d = part.depth(p)
            if dome is not None:
                t = LAYER.get(part.kind, 0.0)
                if t > 0:
                    h = dome_hi + min(t, 0.5 + rise * d * 2)
                    if h > hi:
                        hi, top = h, part
                elif hi == dome_hi and part.kind in ("frame", "rad", "haz", "glass"):
                    top = part   # paint only
                elif part.kind in ("haz", "glass", "frame", "rad"):
                    top = part
            elif part.kind in SLAB:
                z0, z1 = SLAB[part.kind]
                bev = max(0.0, 0.5 - d)
                l, h = z0 + bev, z1 - bev
                if hi is None or h > hi:
                    hi, top = h, part
                lo = l if lo is None else min(lo, l)
        if hi is None:
            return None
        return lo, hi, top, dome, dome_hi


def export(ship_id, name, fn, line, hmax):
    ship = fn()
    pal = PAINT[line]
    field = Field(parts_of(ship), STYLE[line], hmax)
    seams = [([lb(p) for p in s], kw) for k, s, kw in ship.parts if k == "seam" and kw.get("col") == "haz"]
    allp = field.domes + field.others
    X0 = int(math.floor(min(p.box[0] for p in allp))) - 1; X1 = int(math.ceil(max(p.box[2] for p in allp))) + 1
    Y0 = int(math.floor(min(p.box[1] for p in allp))) - 1; Y1 = int(math.ceil(max(p.box[3] for p in allp))) + 1

    S = 4   # plan subsamples per cell side
    cells = {}
    for X in range(X0, X1):
        for Y in range(Y0, Y1):
            subs = []
            for a in range(S):
                for b in range(S):
                    subs.append(field.column((X + (a + .5) / S, Y + (b + .5) / S)))
            if all(s is None for s in subs):
                continue
            c = (X + .5, Y + .5)
            centre = field.column(c)
            zs = [s for s in subs if s is not None]
            for Z in range(int(math.floor(min(s[0] for s in zs))), int(math.ceil(max(s[1] for s in zs)))):
                frac = sum(max(0.0, min(Z + 1, s[1]) - max(Z, s[0])) for s in zs) / (S * S)
                if frac < 0.03:
                    continue
                planes = None
                stepped = False
                if centre is not None:
                    gx, gy = grad(field, c, 1)
                    if gx == 0 and gy == 0 and abs(centre[1] - round(centre[1])) > .01:
                        # a diagonal step: flat at a whole cell, filled or empty as the rounded height says
                        hq = float(round(centre[1]))
                        if hq <= Z:
                            continue
                        stepped = hq >= Z + 1
                if frac < 0.97 or stepped:
                    planes = []
                    if centre is not None:
                        lo, hi = centre[0], centre[1]
                        if not stepped and not (gx == 0 and gy == 0) and Z - 0.5 < hi < Z + 1.5:
                            planes.append([[-gx, -gy, 1.0], hi - Z - .5 * gx - .5 * gy])
                        if Z - 0.5 < lo < Z + 1.5 and lo > 0:
                            lx, ly = grad(field, c, 0)
                            planes.append([[lx, ly, -1.0], -(lo - Z) + .5 * lx + .5 * ly])
                    for part in field.solid:
                        if not (part.contains(c) or part.depth(c) < 0.9 and near_box(part, c)):
                            continue
                        for n, q in part.walls(c):
                            probe = (q[0] + n[0] * .35, q[1] + n[1] * .35)
                            if any(o is not part and o.contains(probe) for o in field.solid):
                                continue   # an inner seam between two parts, not the outline
                            planes.append([[n[0], n[1], 0.0], n[0] * (q[0] - X) + n[1] * (q[1] - Y)])
                    if not planes:
                        if frac < 0.5 and not stepped:
                            continue
                        planes = None
                tag = paint(field, pal, c, centre, Z, seams)
                cells[(X, Y, Z)] = (tag, planes)

    out_cells = [[x, y, z, tag, planes] for (x, y, z), (tag, planes) in sorted(cells.items())]
    mods = modules(ship, ship_id, field)
    os.makedirs(OUT, exist_ok=True)
    with open(os.path.join(OUT, ship_id + ".json"), "w") as f:
        json.dump({"id": ship_id, "name": name, "cells": out_cells, "mods": mods}, f, separators=(",", ":"))
    shaped = sum(1 for c in out_cells if c[4])
    kinds = {}
    for m in mods:
        kinds[m[0]] = kinds.get(m[0], 0) + 1
    print(f"{ship_id:28s} {len(out_cells):6d} cells, {shaped:5d} shaped  mods {kinds}")


def near_box(part, c):
    b = part.box
    return b[0] - 1 <= c[0] <= b[2] + 1 and b[1] - 1 <= c[1] <= b[3] + 1


def grad(field, c, which):
    """Gradient of the column's top (which=1) or bottom (0) across the cell, one-sided at steps, clamped."""
    def h(p):
        col = field.column(p)
        return None if col is None else col[which]
    h0 = h(c)
    g = []
    for d in ((.5, 0), (0, .5)):
        a, b = h((c[0] + d[0], c[1] + d[1])), h((c[0] - d[0], c[1] - d[1]))
        da = None if a is None or abs(a - h0) > 0.6 else (a - h0) / .5
        db = None if b is None or abs(b - h0) > 0.6 else (h0 - b) / .5
        v = (da + db) / 2 if da is not None and db is not None else da if da is not None else db if db is not None else 0.0
        g.append(max(-1.5, min(1.5, v)))
    # A slope only reads clean when it runs along the grid: a long slope or wedge seated across a diagonal leaves a ragged
    # patchwork (RACE-008 review). Near-axis slopes snap to their axis; diagonal ones become flat steps, and the outline's
    # vertical wedges carry the diagonal instead.
    gx, gy = g
    if abs(gx) > 2.2 * abs(gy): return gx, 0.0
    if abs(gy) > 2.2 * abs(gx): return 0.0, gy
    return 0.0, 0.0


def paint(field, pal, c, centre, Z, seams):
    if centre is None:
        return "#%d" % pal["base"]
    lo, hi, top, dome, dome_hi = centre
    if Z + 1 >= hi - 0.05:   # the visible top cell
        for pts, kw in seams:
            for i in range(1, len(pts)):
                if seg_dist(c, pts[i - 1], pts[i])[0] < max(0.5, kw.get("w", 0.12) / CELL / 2):
                    return "#%d" % pal[kw.get("col", "frame") if kw.get("col", "frame") in pal else "frame"]
        kind = top.kind if top is not None else "base"
        return "#%d" % pal.get(kind, pal["base"])
    if dome is not None and Z + 0.5 > dome_hi and top is not None:
        return "#%d" % pal.get(top.kind, pal["base"])
    if dome is not None:
        return "#%d" % pal["base" if dome.kind == "base" else dome.kind]
    return "#%d" % pal.get(top.kind if top else "base", pal["base"])


# ------------------------------------------------------------------------------------------------ modules
def modules(ship, ship_id, field):
    mods = []
    I3 = [[1, 0, 0], [0, 1, 0], [0, 0, 1]]
    for kind, x, y, zc, face in ENGINES[ship_id]:
        w, L = SIZE[kind]
        fX, fY = -face[1], face[0]   # plan facing -> look-book plan axes
        cX, cY = lb((x, y))
        z0 = int(round(zc / CELL - w / 2))
        if fY != 0:      # nozzle fore or aft: the box runs along Y from the exit inward
            ex = int(math.floor(cX - w / 2 + .5)); ey = int(round(cY)) if fY < 0 else int(round(cY)) - L
            mn, size = [ex, ey, z0], [w, L, w]
        else:            # nozzle to a side: the box runs along X
            ey = int(math.floor(cY - w / 2 + .5)); ex = int(round(cX)) if fX < 0 else int(round(cX)) - L
            mn, size = [ex, ey, z0], [L, w, w]
        mods.append([kind, mn, I3, size, size, [fX, fY, 0]])
    # guns: a turret on the hull at the barrel's rear, firing forward
    for kind, shape, kw in ship.parts:
        if kind != "gun":
            continue
        xs = [p[0] for p in shape]; ys = [p[1] for p in shape]
        rear = (min(xs) + 0.75, (min(ys) + max(ys)) / 2)
        X, Y = lb(rear)
        col = field.column((X, Y))
        z = int(math.ceil(col[4] if col and col[4] is not None else col[1] if col else 1))
        mods.append(["gatling", [int(math.floor(X)) - 1, int(math.floor(Y)), z], I3, [2, 3, 2], [2, 3, 2]])
    return mods


if __name__ == "__main__":
    for args in SHIPS:
        export(*args)
