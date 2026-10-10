"""Ships and stations as voxel cells (0.5 m) plus functional blocks, and the per-direction designs.

Coordinates in cells: x right, y forward (bow at +y, exhaust toward -y), z up; the ship is symmetric about x = 0
(cell i mirrors cell -1-i, i.e. x -> -x in continuous coordinates).

Hull shapes come from straight-sided pieces (lofts between octagonal sections, and general convex pieces), so every
sloped face is one continuous plane across all the cells it passes through. That is what keeps edges and corners
aligned; there is no per-cell auto-slope pass any more.

Large ships also use big hull blocks (u x u x u cells, e.g. 3x3x3 = 1.5 m) on a second, coarser layer: pieces built with
big=True fill that layer, and modules placed into a big block split it back into 0.5 m cells around them."""

import math
from mathutils import Matrix, Vector

from ld_geom import (CELL, Acc, mesh_cells, hash01, cbox, box, cyl, sphere, ico, torus, lathe,
                     section_poly, loft_planes, classify_cell, cell_polys, polys_volume)
from ld_blocks import KIT, build_block, greeble_face

LIVERY_TAGS = ("hull", "hull2", "accent", "armor", "trim", "metal", "dark", "gold", "girder", "paint_w", "paint_p", "paint_e",
               "c_cargo")

_MERGE = {}


def merge_clip(k1, v1, k2, v2, tol=0.015):
    """Two convex pieces cut the same cell (e.g. neighbouring ring segments meeting on a diagonal seam). If their union
    is convex (within a sliver), return it as one cut shape: ('full',) or ('clip', planes, vol). Else None."""
    key = (k1, k2)
    if key in _MERGE:
        return _MERGE[key]
    import bmesh
    pts = []
    for k in (k1, k2):
        for _, poly, _ in cell_polys(("clip", k)):
            pts.extend(poly)
    inter = polys_volume(cell_polys(("clip", tuple(sorted(set(k1) | set(k2)))))) if True else 0.0
    union = v1 + v2 - max(0.0, inter)
    bm = bmesh.new()
    seen = set()
    for p in pts:
        q = tuple(round(v, 6) for v in p)
        if q not in seen:
            seen.add(q)
            bm.verts.new(q)
    bmesh.ops.convex_hull(bm, input=list(bm.verts))
    bmesh.ops.dissolve_limit(bm, angle_limit=math.radians(0.2), verts=bm.verts, edges=bm.edges)
    cen = Vector((0, 0, 0))
    for v in bm.verts:
        cen += v.co
    cen /= max(1, len(bm.verts))
    planes = []
    for f in bm.faces:
        n = f.normal.copy()
        p0 = f.verts[0].co
        if n.dot(cen - p0) > 0:
            n = -n
        d = n.dot(p0)
        # drop the cube's own faces
        ax = [i for i in range(3) if abs(n[i]) > 0.9999]
        if ax and (abs(d - (1.0 if n[ax[0]] > 0 else 0.0)) < 1e-5):
            continue
        planes.append((tuple(round(k, 6) for k in n), round(d, 6)))
    bm.free()
    if not planes:
        res = ("full",)
    else:
        hk = tuple(sorted(planes))
        hv = polys_volume(cell_polys(("clip", hk)))
        if hv > 1 - 1e-4:
            res = ("full",)
        elif hv <= union + tol:
            res = ("clip", hk, hv)
        else:
            res = None
    _MERGE[key] = res
    return res


_HULLS = {}


def hull_planes(pts):
    """Half-spaces [(n, d)]: n.p <= d of the convex hull of points (cell units), coplanar faces merged."""
    key = tuple(tuple(round(v, 5) for v in p) for p in pts)
    if key in _HULLS:
        return _HULLS[key]
    import bmesh
    bm = bmesh.new()
    seen = set()
    for p in key:
        if p not in seen:
            seen.add(p)
            bm.verts.new(p)
    if len(seen) < 4:
        bm.free()
        return []
    bmesh.ops.convex_hull(bm, input=list(bm.verts))
    bmesh.ops.dissolve_limit(bm, angle_limit=math.radians(0.05), verts=bm.verts, edges=bm.edges)
    bm.normal_update()
    cen = Vector((0, 0, 0))
    vs = [v for v in bm.verts if v.link_faces]
    for v in vs:
        cen += v.co
    cen /= max(1, len(vs))
    planes = {}
    for f in bm.faces:
        nrm = f.normal.copy()
        if nrm.length < 1e-9:
            continue
        p0 = f.verts[0].co
        if nrm.dot(cen - p0) > 0:
            nrm = -nrm
        nk = tuple(round(k, 6) + 0.0 for k in nrm)
        # the tight plane through the original points (float64), so faces on whole cells stay exactly on them
        d = max(nk[0] * p[0] + nk[1] * p[1] + nk[2] * p[2] for p in key)
        planes[nk] = max(planes.get(nk, -1e9), round(d, 5))
    bm.free()
    res = [(n, d) for n, d in planes.items()]
    _HULLS[key] = res
    return res


def inset_poly(poly, d):
    """Convex polygon [(x, y)] (either winding) offset inward by d."""
    if d <= 0:
        return list(poly)
    n = len(poly)
    area = sum(poly[i][0] * poly[(i + 1) % n][1] - poly[(i + 1) % n][0] * poly[i][1] for i in range(n))
    sgn = 1.0 if area > 0 else -1.0
    lines = []
    for i in range(n):
        (x0, y0), (x1, y1) = poly[i], poly[(i + 1) % n]
        ex, ey = x1 - x0, y1 - y0
        L = math.hypot(ex, ey) or 1.0
        nx, ny = -ey / L * sgn, ex / L * sgn          # inward normal
        lines.append(((x0 + nx * d, y0 + ny * d), (ex, ey)))
    out = []
    for i in range(n):
        (p, e), (q, f) = lines[i - 1], lines[i]
        den = e[0] * f[1] - e[1] * f[0]
        if abs(den) < 1e-9:
            out.append(q)
            continue
        t = ((q[0] - p[0]) * f[1] - (q[1] - p[1]) * f[0]) / den
        out.append((p[0] + e[0] * t, p[1] + e[1] * t))
    return out


class Ship:
    def __init__(self, name, seed=1, livery=None, unit=1):
        self.name = name
        self.u = unit           # big hull block size in cells (1 = no big layer)
        self.big = {}           # big block index -> tag
        self.big_shapes = {}
        self.big_vols = {}
        self.split = set()      # big blocks broken back into cells (modules placed inside them)
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

    def box(self, x0, x1, y0, y1, z0, z1, tag="hull", mirror=False, big=False):
        if big:
            planes = [((1, 0, 0), x1), ((-1, 0, 0), -x0), ((0, 1, 0), y1), ((0, -1, 0), -y0), ((0, 0, 1), z1), ((0, 0, -1), -z0)]
            self.convex(planes, ((x0, y0, z0), (x1, y1, z1)), tag, mirror, big=True)
            return
        for x in range(x0, x1):
            for y in range(y0, y1):
                for z in range(z0, z1):
                    self.set((x, y, z), tag)
                    if mirror:
                        self.set((-1 - x, y, z), tag)

    def clear(self, x0, x1, y0, y1, z0, z1, mirror=False):
        if self.big:
            u = self.u
            regions = [((x0, x1), (y0, y1), (z0, z1))]
            if mirror:
                regions.append(((-x1, -x0), (y0, y1), (z0, z1)))
            for B in list(self.big):
                for reg in regions:
                    if all(B[k] * u < hi and (B[k] + 1) * u > lo for k, (lo, hi) in enumerate(reg)):
                        self._split(B)
                        break
        for x in range(x0, x1):
            for y in range(y0, y1):
                for z in range(z0, z1):
                    for c in ((x, y, z), (-1 - x, y, z)) if mirror else ((x, y, z),):
                        self.cells.pop(c, None)
                        self.shapes.pop(c, None)

    def paint(self, pred, tag, big=True):
        for c in list(self.cells):
            if pred(c[0] + 0.5, c[1] + 0.5, c[2] + 0.5):
                self.cells[c] = tag
        if big:
            u = self.u
            for B in list(self.big):
                if pred((B[0] + 0.5) * u, (B[1] + 0.5) * u, (B[2] + 0.5) * u):
                    self.big[B] = tag

    # straight-sided pieces ---------------------------------------------------------------------------------------
    def loft(self, stations, tag="hull", cx=0.0, mirror=False, big=False, axis="y"):
        """Hull between octagonal sections. stations: [(y, w, zb, zt[, ct[, cb]])] in cells; half-width w around cx,
        top/bottom chamfers ct/cb. Changes between stations of 0, 1:2, 1:1 or 2:1 keep every cut on whole cells.
        axis='x' runs the loft along x instead (stations give x, and cx is then the centre in y)."""
        for (ya, *a), (yb, *b) in zip(stations, stations[1:]):
            if yb <= ya:
                continue
            sa = section_poly(cx, a[0], a[1], a[2], *(list(a[3:5]) + [0, 0])[:2])
            sb = section_poly(cx, b[0], b[1], b[2], *(list(b[3:5]) + [0, 0])[:2])
            planes = loft_planes(ya, sa, yb, sb)
            xs = [x for x, _ in sa + sb]
            zs = [z for _, z in sa + sb]
            lo, hi = (min(xs), ya, min(zs)), (max(xs), yb, max(zs))
            if axis == "x":
                planes = [((n[1], n[0], n[2]), d) for n, d in planes]
                lo, hi = (lo[1], lo[0], lo[2]), (hi[1], hi[0], hi[2])
            self.convex(planes, (lo, hi), tag, big=big)
        if mirror and cx:
            self.loft(stations, tag, -cx, False, big, axis)

    def convex(self, planes, bbox, tag="hull", mirror=False, big=False):
        """Any convex piece given as half-spaces [(n, d)]: n.p <= d, in cell units. big=True fills big hull blocks."""
        (x0, y0, z0), (x1, y1, z1) = bbox
        if big and self.u > 1:
            u = self.u
            pb = [(n, d / u) for n, d in planes]
            for x in range(math.floor(x0 / u), math.ceil(x1 / u)):
                for y in range(math.floor(y0 / u), math.ceil(y1 / u)):
                    for z in range(math.floor(z0 / u), math.ceil(z1 / u)):
                        B = (x, y, z)
                        if B in self.split:
                            for c in self._cells_of(B):
                                self._piece(c, classify_cell(planes, c), tag)
                        else:
                            self._bpiece(B, classify_cell(pb, B), tag)
        else:
            for x in range(math.floor(x0), math.ceil(x1)):
                for y in range(math.floor(y0), math.ceil(y1)):
                    for z in range(math.floor(z0), math.ceil(z1)):
                        self._piece((x, y, z), classify_cell(planes, (x, y, z)), tag)
        if mirror:
            mp = [((-n[0], n[1], n[2]), d) for n, d in planes]
            self.convex(mp, ((-x1, y0, z0), (-x0, y1, z1)), tag, big=big)

    def _bpiece(self, B, res, tag):
        if res[0] == "empty":
            return
        if res[0] == "full":
            self.big[B] = tag
            self.big_shapes.pop(B, None)
            return
        if B in self.big and B not in self.big_shapes:
            return
        if B in self.big_shapes:
            m = merge_clip(self.big_shapes[B][1], self.big_vols[B], res[1], res[2])
            if m is not None:
                if res[2] > self.big_vols[B]:
                    self.big[B] = tag
                if m[0] == "full":
                    self.big_shapes.pop(B, None)
                else:
                    self.big_shapes[B] = ("clip", m[1])
                    self.big_vols[B] = m[2]
                return
            if self.big_vols.get(B, 0) >= res[2]:
                return
        self.big[B] = tag
        self.big_shapes[B] = ("clip", res[1])
        self.big_vols[B] = res[2]

    def _cells_of(self, B):
        u = self.u
        return [(B[0] * u + i, B[1] * u + j, B[2] * u + k) for i in range(u) for j in range(u) for k in range(u)]

    def big_world_planes(self, B):
        """World half-spaces (cell units) of a cut big block."""
        u = self.u
        return [(n, u * (d + n[0] * B[0] + n[1] * B[1] + n[2] * B[2])) for n, d in self.big_shapes[B][1]]

    def _split(self, B):
        """Break a big block back into 0.5 m cells (exactly the same shape), e.g. to seat a module inside it."""
        if B not in self.big:
            return
        tag = self.big.pop(B)
        self.split.add(B)
        if B in self.big_shapes:
            wp = self.big_world_planes(B)
            self.big_shapes.pop(B)
            for c in self._cells_of(B):
                self._piece(c, classify_cell(wp, c), tag)
        else:
            for c in self._cells_of(B):
                if c not in self.mod_cells:
                    self.cells[c] = tag
                    self.shapes.pop(c, None)

    def prune(self):
        """Drop hairline slivers: cut cells that touch the rest of the hull only along an edge or a corner (they
        appear where two sloped pieces meet at a section change). They are a few millimetres thick."""
        from ld_check import _small_face
        if getattr(self, "_pruned", False):
            return 0
        removed = 0
        for _ in range(3):
            drop = []
            for c in self.shapes:
                linked = False
                for d in ((1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1)):
                    nb = (c[0] + d[0], c[1] + d[1], c[2] + d[2])
                    if _small_face(self, c, d) >= 0.02 and _small_face(self, nb, (-d[0], -d[1], -d[2])) >= 0.02:
                        linked = True
                        break
                if not linked:
                    drop.append(c)
            for c in drop:
                self.cells.pop(c, None)
                self.shapes.pop(c, None)
            removed += len(drop)
            if not drop:
                break
        # the same for cut big blocks
        from ld_check import _face_area
        u = self.u
        for _ in range(3):
            drop = []
            for B, sh in self.big_shapes.items():
                linked = False
                for d in ((1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1)):
                    if _face_area(sh, d) < 0.02:
                        continue
                    nb = (B[0] + d[0], B[1] + d[1], B[2] + d[2])
                    if nb in self.big and _face_area(self.big_shapes.get(nb), (-d[0], -d[1], -d[2])) >= 0.02:
                        linked = True
                        break
                    if nb in self.split:
                        linked = True
                        break
                if not linked:
                    drop.append(B)
            for B in drop:
                self.big.pop(B, None)
                self.big_shapes.pop(B, None)
            removed += len(drop)
            if not drop:
                break
        self._pruned = True
        return removed

    def big_of(self, c):
        u = self.u
        return (c[0] // u, c[1] // u, c[2] // u)

    def solid_kind(self, c):
        """'full' when cell c is solid structure or module, 'part' when partly filled, None when empty."""
        if c in self.mod_cells:
            return "full"
        if c in self.cells:
            return "full" if c not in self.shapes else "part"
        if self.big:
            B = self.big_of(c)
            if B in self.big:
                if B not in self.big_shapes:
                    return "full"
                r = classify_cell(self.big_world_planes(B), c)
                return {"full": "full", "empty": None}.get(r[0], "part")
        return None

    def _piece(self, c, res, tag):
        if c in self.mod_cells or res[0] == "empty":
            return
        if res[0] == "full":
            self.cells[c] = tag
            self.shapes.pop(c, None)
            return
        if c in self.cells and c not in self.shapes:
            return
        if c in self.shapes:
            m = merge_clip(self.shapes[c][1], self.vols[c], res[1], res[2])
            if m is not None:
                if res[2] > self.vols[c]:
                    self.cells[c] = tag
                if m[0] == "full":
                    self.shapes.pop(c, None)
                else:
                    self.shapes[c] = ("clip", m[1])
                    self.vols[c] = m[2]
                return
            if self.vols.get(c, 0) >= res[2]:
                return
        self.cells[c] = tag
        self.shapes[c] = ("clip", res[1])
        self.vols[c] = res[2]

    def tower(self, cx, cy, stations, tag="hull", big=False):
        """Vertical octagonal tower: stations [(z, half-width, chamfer)], straight-sided between stations."""
        dirs = [(1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1)]
        for (za, wa, ca), (zb, wb, cb) in zip(stations, stations[1:]):
            if zb <= za:
                continue
            planes = [((0, 0, -1), -za), ((0, 0, 1), zb)]
            for mx, my in dirs:
                hA = wa if not (mx and my) else 2 * wa - ca
                hB = wb if not (mx and my) else 2 * wb - cb
                k = (hB - hA) / (zb - za)
                planes.append(((mx, my, -k), hA - k * za + mx * cx + my * cy))
            w = max(wa, wb)
            self.convex(planes, ((cx - w, cy - w, za), (cx + w, cy + w, zb)), tag, big=big)

    def spire(self, cx, cy, w, z0, z1, k=1.0, tag="hull", down=False, big=False):
        """Square tower with a pyramid cap (or an inverted one hanging down when down=True). k = cap steepness."""
        sides = [((1, 0, 0), cx + w), ((-1, 0, 0), -(cx - w)), ((0, 1, 0), cy + w), ((0, -1, 0), -(cy - w))]
        if not down:
            planes = sides + [((0, 0, -1), -z0), ((k, 0, 1), z1 + k * cx), ((-k, 0, 1), z1 - k * cx),
                              ((0, k, 1), z1 + k * cy), ((0, -k, 1), z1 - k * cy)]
        else:
            planes = sides + [((0, 0, 1), z1), ((k, 0, -1), -z0 + k * cx), ((-k, 0, -1), -z0 - k * cx),
                              ((0, k, -1), -z0 + k * cy), ((0, -k, -1), -z0 - k * cy)]
        self.convex(planes, ((cx - w, cy - w, z0), (cx + w, cy + w, z1)), tag, big=big)

    def ball(self, c, r, tag="hull", n=1, big=False):
        """Faceted sphere: intersection of half-spaces along icosphere directions (geodesic look)."""
        verts, _ = ico((0, 0, 0), 1.0, n)
        planes = [((v[0], v[1], v[2]), r + v[0] * c[0] + v[1] * c[1] + v[2] * c[2]) for v in verts]
        self.convex(planes, ((c[0] - r, c[1] - r, c[2] - r), (c[0] + r, c[1] + r, c[2] + r)), tag, big=big)

    def ring(self, c, axis, R0, R1, half_t, n=24, tags=("hull",), big=False):
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
            self.convex(planes, (tuple(lo), tuple(hi)), tags[i % len(tags)], big=big)

    # free-form pieces (round 4): any convex hull of points, swept sections, drums, plan-view slabs --------------
    def hull(self, pts, tag="hull", mirror=False, big=False):
        """One straight-sided piece: the convex hull of points (cell units)."""
        planes = hull_planes(pts)
        if not planes:
            return
        lo = tuple(min(p[k] for p in pts) for k in range(3))
        hi = tuple(max(p[k] for p in pts) for k in range(3))
        self.convex(planes, (lo, hi), tag, mirror=mirror, big=big)

    def sweep(self, path, sec, tag="hull", up=(0, 0, 1), mirror=False, big=False, closed=False, tags=None):
        """A section swept along a polyline: arcs, crescents, claws, ribs. path: [(x, y, z)] in cells. sec: one
        (w, h0, h1[, ct[, cb]]) or one per point: half-width w along the side axis, h0..h1 along the up axis, top and
        bottom chamfers. Joints are mitred, so neighbouring segments share their section exactly. up: the reference
        that defines 'up' for the section (z for flat arcs; y for ribs standing across the ship)."""
        P = [Vector(p) for p in path]
        n = len(P)
        secs = sec if isinstance(sec, list) else [sec] * n
        U = Vector(up)
        rings = []
        for i in range(n):
            if closed:
                a, b = P[i - 1], P[(i + 1) % n]
                dp, dn = (P[i] - a).normalized(), (b - P[i]).normalized()
            else:
                dp = (P[i] - P[i - 1]).normalized() if i > 0 else (P[1] - P[0]).normalized()
                dn = (P[i + 1] - P[i]).normalized() if i < n - 1 else dp
            t = (dp + dn)
            t = t.normalized() if t.length > 1e-6 else dn
            s = t.cross(U)
            if s.length < 1e-6:
                s = t.cross(Vector((1, 0, 0)))
            s.normalize()
            v = s.cross(t).normalized()
            cosh = max(0.3, dp.dot(t))
            bend = dn - dp
            bend = bend - t * bend.dot(t)
            bn = bend.normalized() if bend.length > 1e-6 else None
            w, h0, h1, *ch = secs[i]
            ct, cb = (list(ch) + [0, 0])[:2]
            pts = []
            for x, z in section_poly(0.0, w, h0, h1, ct, cb):
                q = s * x + v * z
                if bn is not None:
                    q = q + bn * (q.dot(bn) * (1.0 / cosh - 1.0))
                pts.append(tuple(P[i] + q))
            rings.append(pts)
        segs = range(n) if closed else range(n - 1)
        for i in segs:
            tg = tags[i % len(tags)] if tags else tag
            self.hull(rings[i] + rings[(i + 1) % n], tg, mirror=mirror, big=big)

    def drum(self, p0, p1, r0, r1=None, n=12, sq=(1.0, 1.0), up=(0, 0, 1), tag="hull", mirror=False, big=False, phase=None):
        """Faceted cylinder or cone frustum from p0 to p1 (radius r0 to r1), n sides; sq squashes the section."""
        r1 = r0 if r1 is None else r1
        a, b = Vector(p0), Vector(p1)
        t = (b - a).normalized()
        s = t.cross(Vector(up))
        if s.length < 1e-6:
            s = t.cross(Vector((1, 0, 0)))
        s.normalize()
        v = s.cross(t)
        ph = math.pi / n if phase is None else phase
        pts = []
        for c, r in ((a, r0), (b, r1)):
            for i in range(n):
                ang = ph + 2 * math.pi * i / n
                if r <= 1e-6:
                    pts.append(tuple(c))
                    break
                pts.append(tuple(c + s * (math.cos(ang) * r * sq[0]) + v * (math.sin(ang) * r * sq[1])))
        self.hull(pts, tag, mirror=mirror, big=big)

    def bell(self, axis_p0, axis_p1, profile, n=16, tag="hull", big=False, tags=None, sq=(1.0, 1.0)):
        """A lathe of stacked frusta along an axis: profile [(t 0..1 along the axis, radius)]."""
        a, b = Vector(axis_p0), Vector(axis_p1)
        for i, ((t0, r0), (t1, r1)) in enumerate(zip(profile, profile[1:])):
            tg = tags[i % len(tags)] if tags else tag
            self.drum(tuple(a + (b - a) * t0), tuple(a + (b - a) * t1), r0, r1, n, sq=sq, tag=tg, big=big)

    def shell(self, p0, p1, profile, n=24, tag="hull", big=False, tags=None, phase=None):
        """Hollow lathe (a bell, a nozzle): profile [(t 0..1 along p0->p1, outer radius, wall thickness)]. Each angular
        sector of each profile segment is one straight-sided piece, so the wall is exact blocks inside and out."""
        a, b = Vector(p0), Vector(p1)
        ax = (b - a).normalized()
        s = ax.cross(Vector((0, 0, 1)))
        if s.length < 1e-6:
            s = ax.cross(Vector((1, 0, 0)))
        s.normalize()
        v = s.cross(ax)
        ph = math.pi / n if phase is None else phase
        for i, ((t0, r0, k0), (t1, r1, k1)) in enumerate(zip(profile, profile[1:])):
            for j in range(n):
                a0, a1 = ph + 2 * math.pi * j / n, ph + 2 * math.pi * (j + 1) / n
                pts = []
                for t, r, k in ((t0, r0, k0), (t1, r1, k1)):
                    c = a + (b - a) * t
                    for ang in (a0, a1):
                        d = s * math.cos(ang) + v * math.sin(ang)
                        pts += [tuple(c + d * r), tuple(c + d * max(0.0, r - k))]
                tg = tags(i, j) if callable(tags) else (tags[i % len(tags)] if tags else tag)
                self.hull(pts, tg, big=big)

    def slab(self, poly, z0, z1, ct=0.0, cb=0.0, tag="hull", mirror=False, big=False):
        """A convex plan-view polygon [(x, y)] extruded from z0 to z1, with chamfered top and bottom edges."""
        pts = []
        for z, ins in ((z0, cb), (z0 + cb, 0.0), (z1 - ct, 0.0), (z1, ct)):
            for x, y in inset_poly(poly, ins):
                pts.append((x, y, z))
        self.hull(pts, tag, mirror=mirror, big=big)

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
        if self.big:
            u = self.u
            for bx in range(mn[0] // u, (mn[0] + rsz[0] - 1) // u + 1):
                for by in range(mn[1] // u, (mn[1] + rsz[1] - 1) // u + 1):
                    for bz in range(mn[2] // u, (mn[2] + rsz[2] - 1) // u + 1):
                        self._split((bx, by, bz))
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
        pts = list(self.cells) + list(self.mod_cells)
        u = self.u
        for B in self.big:
            pts.append((B[0] * u, B[1] * u, B[2] * u))
            pts.append((B[0] * u + u - 1, B[1] * u + u - 1, B[2] * u + u - 1))
        xs, ys, zs = zip(*pts)
        return (Vector((min(xs), min(ys), min(zs))) * CELL, Vector((max(xs) + 1, max(ys) + 1, max(zs) + 1)) * CELL)

    def cell_count(self):
        return len(self.cells) + len(self.mod_cells) + len(self.big) * self.u ** 3


def merge_acc(src, dst, livery=None):
    """Move every part of src into dst, applying a livery suffix to the painted tags ('hull#3' -> 'hull@oxide#3')."""
    for (g, tag), bucket in src.parts.items():
        base, _, k = tag.partition("#")
        t = f"{base}@{livery}" + (f"#{k}" if k else "") if livery and base in LIVERY_TAGS else tag
        dst_b = dst.parts.setdefault((g, t), [[], [], []])
        if len(bucket) == 4 and len(dst_b) == 3:
            dst_b.append([0.0] * len(dst_b[0]))
        base = len(dst_b[0])
        dst_b[0].extend(bucket[0])
        dst_b[1].extend(tuple(i + base for i in f) for f in bucket[1])
        dst_b[2].extend(bucket[2])
        if len(dst_b) == 4:
            dst_b[3].extend(bucket[3] if len(bucket) == 4 else [0.0] * len(bucket[0]))


class _SmallFilled:
    """'Is this 0.5 m cell solid?' for the small-cell mesher: cells, modules, and the inside of big blocks."""

    def __init__(self, ship):
        self.s = ship
        self.cache = {}

    def __contains__(self, c):
        s = self.s
        if c in s.cells or c in s.mod_cells:
            return True
        if not s.big:
            return False
        B = s.big_of(c)
        if B not in s.big:
            return False
        if B not in s.big_shapes:
            return True
        if c not in self.cache:
            self.cache[c] = classify_cell(s.big_world_planes(B), c)[0] == "full"
        return self.cache[c]


def realize(ship, S, acc, group, greebles=True, plumes=True, nav=True):
    if hasattr(ship, "realize"):
        return ship.realize(S, acc, group, plumes=plumes)
    craters = ship.craters

    def tag_of(c, d):
        if craters:
            p = (Vector(c) + Vector((0.5, 0.5, 0.5))) * CELL
            for cc, r in craters:
                if (p - cc).length < r + 1.2 * CELL:
                    return "interior"
        return ship.cells[c]

    ship.prune()
    sub = Acc()
    out, exposed = mesh_cells(ship.cells, ship.shapes, tag_of=tag_of, solid=_SmallFilled(ship))
    surf = [(c, d, ship.cells[c], False) for c, d in exposed]
    if ship.big:
        u = ship.u
        bout, bexp = mesh_cells(ship.big, ship.big_shapes, cell=CELL * u, solid=ship.big)
        for tag, vf in bout.items():
            sub.add(group, f"{tag}#{u}", vf)
        # greebles stay at human scale: every exposed big face offers its u x u sub-cell faces
        for B, d in bexp:
            ax = [k for k in range(3) if d[k]][0]
            rng = [range(B[k] * u, B[k] * u + u) for k in range(3)]
            rng[ax] = [B[ax] * u + (u - 1 if d[ax] > 0 else 0)]
            t = ship.big[B]
            for x in rng[0]:
                for y in rng[1]:
                    for z in rng[2]:
                        surf.append(((x, y, z), d, t, True))
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
        if plumes and kind in PLUME_KINDS:
            W, H = size[0] * CELL, size[2] * CELL
            r = min(W, H) * 0.42
            p = M @ Vector((W / 2, -0.05, H / 2))
            d = (M.to_3x3() @ Vector((0, -1, 0))).normalized()
            ship.plumes.append((p, d, r, PLUME_KINDS[kind] * (size[1] / KIT[kind][1])))
    for p, d, r, L in ship.plumes:
        plume(sub, group, p, d, r, L, S)
    for a, b in getattr(ship, "cables", []):
        # rigging (crane hoist lines, chains): thin cables between two points, not blocks
        from ld_geom import cyl as _cyl
        sub.add(group, "metal", _cyl(tuple(Vector(a) * CELL), tuple(Vector(b) * CELL), 0.05, 6), None, True)
    for fn in ship.extra:
        fn(sub, group, S)
    if greebles:
        add_greebles(ship, S, sub, group, surf)
    if nav:
        add_nav(ship, S, sub, group)
    merge_acc(sub, acc, ship.livery)
    return exposed


PLUME_KINDS = {"thruster_s": 3.0, "thruster_l": 6.0, "thruster_xl": 16.0}


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

def add_greebles(ship, S, acc, group, surf):
    dens = dict(S["greeble"])
    dens.update(getattr(ship, "greeble_override", {}) or {})
    seed = ship.seed
    I = Matrix.Identity(4)
    for c, d, ctag, big in surf:
        if d[2] < 0 or ctag in ("accent", "trim"):
            continue
        if big:
            # big hulls: windows only on deck rows every 3 m, and fewer small fittings, so the size reads as calm
            x, y, z = c
            if d[2] == 0 and z % 6 != 1:
                if hash01(x, y, z, seed, 41) > 0.25:
                    continue
            elif d[2] > 0 and hash01(x, y, z, seed, 43) > 0.45:
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
    cells = set(ship.cells)
    if ship.big:
        u = ship.u
        for B in ship.big:
            for c in ship._cells_of(B):
                if c[0] in (B[0] * u, B[0] * u + u - 1) or c[2] == B[2] * u + u - 1:
                    cells.add(c)
    if not cells:
        return
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
    s.loft([(1, 2, 1, 5, 1, 1), (19, 2, 1, 5, 1, 1), (21, 1, 2, 4, 0, 0)], "hull2", cx=7, mirror=True)
    s.loft([(3, 1.5, 0, 6, 0, 0), (17, 1.5, 0, 6, 0, 0)], "trim", cx=5, mirror=True)
    s.paint(lambda x, y, z: z < 2 and y > 3, "hull2")
    s.paint(lambda x, y, z: 25 <= y < 26 and z > 5, "accent")
    s.mod("cockpit", -2, 24, 7)
    s.mod("gatling", -1, 23, -2, rot=(0, 180, 0))
    s.mod("harpoon", -1, 33, 4)
    s.mod("autocannon", 6, 11, 5, mirror=True)
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
    s.loft([(-4, 0.5, 6, 14, 0, 0), (2, 0.5, 6, 10, 0, 0)], "hull2", cx=3.5, mirror=True)
    s.paint(lambda x, y, z: z < 1.5, "armor")
    s.paint(lambda x, y, z: 18 <= y < 19, "accent")
    s.mod("cockpit", -2, 22, 7)
    s.mod("gatling", 8, 9, -1, rot=(0, 180, 0), mirror=True)
    s.mod("autocannon", -1, 21, -2, rot=(0, 180, 0))
    s.mod("thruster_l", -2, -12, 1)
    s.mod("thruster_s", 8, 2, 1, mirror=True)
    s.mod("sensor", -1, 12, 7)
    s.mod("core_1", 0, 14, 2)
    return s


def nocturne_heavy():
    """CANTICLE: refinery tug. Top-heavy castle with gothic spires, sloped prow, five-nozzle engine block, keel gun."""
    s = Ship("CANTICLE", 22, "company")
    s.loft([(0, 9, 0, 13, 3, 2), (62, 9, 0, 13, 3, 2), (74, 7, 1, 12, 3, 2), (86, 4, 3, 9, 2, 1), (92, 2, 4, 7, 1, 0)])
    s.loft([(8, 7, 13, 20, 2), (44, 7, 13, 20, 2), (50, 5, 13, 18, 2), (54, 3, 13, 16, 1)], "hull2")
    s.loft([(-10, 11, -2, 16, 4, 3), (2, 11, -2, 16, 4, 3), (6, 9, 0, 13, 3, 2)], "armor")
    s.loft([(30, 3, -3, 1, 0, 1), (70, 3, -3, 1, 0, 1)], "armor")
    s.box(-2, 1, 70, 84, 0, 3, "armor")
    for y, top in ((12, 38), (22, 34), (32, 30)):
        s.spire(0, y, 2, 20, top, 1.5, "hull")
    for sx in (-1, 1):
        s.spire(sx * 6, -3, 1, 16, 26, 2.0, "armor")
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
    s.mod("railgun", 9, 20, 4, rot=(0, 90, 0))
    s.mod("railgun", -11, 20, 4, rot=(0, -90, 0))
    s.mod("core_35", -2, 30, 3)
    s.mod("reactor", -2, 0, 4)
    return s


def opera_light():
    """LARK: courier. Octagonal fuselage, 45-degree swept wings, bubble cab, twin sail fins, wingtip engines."""
    s = Ship("LARK", 31, "crown")
    s.loft([(-6, 2, 2, 5, 1, 1), (-2, 3, 1, 6, 2, 2), (18, 3, 1, 6, 2, 2), (24, 2, 2, 5, 1, 1), (28, 1, 3, 4, 0, 0)])
    # swept wing: leading edge y <= 17 - (x - 3), trailing edge y >= 1, thickness 2..4 with a chamfered tip
    wing = [((-1, 0, 0), -1), ((1, 0, 0), 14), ((1, 1, 0), 20), ((0, -1, 0), -1), ((0, 0, -1), -2), ((0, 0, 1), 4),
            ((1, 0, 1), 17)]
    s.convex(wing, ((1, 1, 2), (14, 19, 4)), "hull2", mirror=True)
    s.mod("cockpit", -2, 15, 5)
    s.mod("thruster_l", -2, -12, 1)
    s.mod("thruster_s", 11, -2, 2, mirror=True)
    s.mod("autocannon", 4, 8, 4, mirror=True)
    s.mod("beam", -1, 8, 6)
    s.mod("radiator", 1, -5, 4, mirror=True)
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
    s.mod("sensor", -1, 81, 19)
    s.mod("shield", -1, 77, 19)
    for x, z in ((-6, 1), (2, 1), (-6, 11), (2, 11), (-2, 6)):
        s.mod("thruster_l", x, -12, z)
    s.mod("core_3", -2, 78, 6)
    return s


SHIPS = {
    "drydock": (drydock_light, drydock_heavy),
    "nocturne": (nocturne_light, nocturne_heavy),
    "opera": (opera_light, opera_heavy),
}

def _designs():
    from ld_capships import drydock_cruiser, drydock_dreadnought, nocturne_cruiser, nocturne_dreadnought
    import ld_lines
    return {
        "drydock": [("light", drydock_light), ("heavy", drydock_heavy), ("cruiser", drydock_cruiser), ("dreadnought", drydock_dreadnought)]
                   + [("b_" + k, f) for k, f in ld_lines.LINES.get("drydock", [])],
        "nocturne": [("light", nocturne_light), ("heavy", nocturne_heavy), ("cruiser", nocturne_cruiser), ("dreadnought", nocturne_dreadnought)]
                    + [("b_" + k, f) for k, f in ld_lines.LINES.get("nocturne", [])],
        "opera": [("light", opera_light), ("heavy", opera_heavy)],
    }


class _Lazy(dict):
    def __missing__(self, k):
        self.update(_designs())
        return dict.__getitem__(self, k)


DESIGNS = _Lazy()
