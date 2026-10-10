"""Geometry primitives and a voxel mesher for the 3D look-development scenes (runs inside Blender).

Every primitive returns (verts, faces) in metres. `Acc` collects parts per (group, material tag) so a whole ship
becomes a handful of objects. The voxel mesher turns a cell dict into exposed faces, including clipped "slope"
cells, which is how the game's brick mesher will see the same data.
"""

import math
from mathutils import Matrix, Vector

CELL = 0.5


# ----------------------------------------------------------------------------------------------------------------------
# Accumulator
# ----------------------------------------------------------------------------------------------------------------------

class Acc:
    """Collects geometry per (group, tag). Each entry: [verts, faces, smooth_flags]."""

    def __init__(self):
        self.parts = {}

    def add(self, group, tag, vf, matrix=None, smooth=False):
        verts, faces = vf
        if not faces:
            return
        bucket = self.parts.setdefault((group, tag), [[], [], []])
        base = len(bucket[0])
        if matrix is not None:
            verts = [tuple(matrix @ Vector(v)) for v in verts]
        bucket[0].extend(verts)
        bucket[1].extend(tuple(i + base for i in f) for f in faces)
        bucket[2].extend([smooth] * len(faces))
        if len(bucket) == 4:
            bucket[3].extend([0.0] * len(verts))

    def add_fade(self, group, tag, vf, matrix, fade):
        """Like add, plus a per-vertex 'fade' value (plumes, beams)."""
        bucket = self.parts.setdefault((group, tag), [[], [], [], []])
        if len(bucket) == 3:
            bucket.append([0.0] * len(bucket[0]))
        verts, faces = vf
        base = len(bucket[0])
        if matrix is not None:
            verts = [tuple(matrix @ Vector(v)) for v in verts]
        bucket[0].extend(verts)
        bucket[1].extend(tuple(i + base for i in f) for f in faces)
        bucket[2].extend([True] * len(faces))
        bucket[3].extend(fade)

    def groups(self):
        return sorted({g for g, _ in self.parts})


# ----------------------------------------------------------------------------------------------------------------------
# Primitives (local space, then transformed by the caller's matrix)
# ----------------------------------------------------------------------------------------------------------------------

def box(mn, mx):
    x0, y0, z0 = mn
    x1, y1, z1 = mx
    v = [(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0), (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)]
    f = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
    return v, f


def cbox(center, size):
    c = Vector(center)
    h = Vector(size) * 0.5
    return box(tuple(c - h), tuple(c + h))


def _frame(axis):
    """Orthonormal (u, v, w) with w along axis."""
    w = Vector(axis).normalized()
    ref = Vector((0, 0, 1)) if abs(w.z) < 0.9 else Vector((1, 0, 0))
    u = ref.cross(w).normalized()
    v = w.cross(u)
    return u, v, w


def lathe(profile, p0=(0, 0, 0), axis=(0, 0, 1), seg=16, cap0=True, cap1=True, phase=0.0):
    """Surface of revolution. profile = [(radius, height), ...] from bottom to top along axis."""
    u, v, w = _frame(axis)
    o = Vector(p0)
    verts, faces = [], []
    n = len(profile)
    for r, h in profile:
        for i in range(seg):
            a = 2 * math.pi * i / seg + phase
            verts.append(tuple(o + w * h + (u * math.cos(a) + v * math.sin(a)) * r))
    for j in range(n - 1):
        for i in range(seg):
            a = j * seg + i
            b = j * seg + (i + 1) % seg
            faces.append((a, b, b + seg, a + seg))
    if cap0 and profile[0][0] > 1e-6:
        verts.append(tuple(o + w * profile[0][1]))
        c = len(verts) - 1
        for i in range(seg):
            faces.append((c, (i + 1) % seg, i))
    if cap1 and profile[-1][0] > 1e-6:
        verts.append(tuple(o + w * profile[-1][1]))
        c = len(verts) - 1
        base = (n - 1) * seg
        for i in range(seg):
            faces.append((c, base + i, base + (i + 1) % seg))
    return verts, faces


def cyl(p0, p1, r, seg=12, caps=True, r1=None, phase=0.0):
    a, b = Vector(p0), Vector(p1)
    d = b - a
    L = d.length
    return lathe([(r, 0.0), (r if r1 is None else r1, L)], tuple(a), tuple(d), seg, caps, caps, phase)


def tube(p0, p1, r_out, r_in, seg=16):
    """Hollow cylinder (ring with thickness along the axis)."""
    a, b = Vector(p0), Vector(p1)
    d = b - a
    L = d.length
    u, v, w = _frame(d)
    verts, faces = [], []
    for h in (0, L):
        for r in (r_out, r_in):
            for i in range(seg):
                ang = 2 * math.pi * i / seg
                verts.append(tuple(a + w * h + (u * math.cos(ang) + v * math.sin(ang)) * r))

    def idx(hi, ri, i):
        return (hi * 2 + ri) * seg + i % seg

    for i in range(seg):
        faces.append((idx(0, 0, i), idx(0, 0, i + 1), idx(1, 0, i + 1), idx(1, 0, i)))   # outer
        faces.append((idx(0, 1, i), idx(1, 1, i), idx(1, 1, i + 1), idx(0, 1, i + 1)))   # inner
        faces.append((idx(0, 0, i), idx(0, 1, i), idx(0, 1, i + 1), idx(0, 0, i + 1)))   # bottom
        faces.append((idx(1, 0, i), idx(1, 0, i + 1), idx(1, 1, i + 1), idx(1, 1, i)))   # top
    return verts, faces


def torus(center, axis, R, r, seg=24, seg_r=8):
    u, v, w = _frame(axis)
    c = Vector(center)
    verts, faces = [], []
    for i in range(seg):
        a = 2 * math.pi * i / seg
        radial = u * math.cos(a) + v * math.sin(a)
        for j in range(seg_r):
            b = 2 * math.pi * j / seg_r
            verts.append(tuple(c + radial * (R + r * math.cos(b)) + w * (r * math.sin(b))))
    for i in range(seg):
        for j in range(seg_r):
            a0 = i * seg_r + j
            a1 = ((i + 1) % seg) * seg_r + j
            b0 = i * seg_r + (j + 1) % seg_r
            b1 = ((i + 1) % seg) * seg_r + (j + 1) % seg_r
            faces.append((a0, a1, b1, b0))
    return verts, faces


def sphere(center, r, seg=16, rings=8, scale=(1, 1, 1)):
    c = Vector(center)
    profile = []
    for j in range(rings + 1):
        t = math.pi * j / rings
        profile.append((math.sin(t) * r, -math.cos(t) * r))
    v, f = lathe(profile, (0, 0, 0), (0, 0, 1), seg, False, False)
    v = [tuple(c + Vector((x * scale[0], y * scale[1], z * scale[2]))) for x, y, z in v]
    return v, f


def ico(center, r, subdiv=1, scale=(1, 1, 1), jitter=0.0, seed=0):
    t = (1 + 5 ** 0.5) / 2
    vs = [(-1, t, 0), (1, t, 0), (-1, -t, 0), (1, -t, 0), (0, -1, t), (0, 1, t), (0, -1, -t), (0, 1, -t),
          (t, 0, -1), (t, 0, 1), (-t, 0, -1), (-t, 0, 1)]
    fs = [(0, 11, 5), (0, 5, 1), (0, 1, 7), (0, 7, 10), (0, 10, 11), (1, 5, 9), (5, 11, 4), (11, 10, 2), (10, 7, 6),
          (7, 1, 8), (3, 9, 4), (3, 4, 2), (3, 2, 6), (3, 6, 8), (3, 8, 9), (4, 9, 5), (2, 4, 11), (6, 2, 10),
          (8, 6, 7), (9, 8, 1)]
    vs = [Vector(p).normalized() for p in vs]
    for _ in range(subdiv):
        cache = {}
        nf = []

        def mid(a, b):
            k = (min(a, b), max(a, b))
            if k not in cache:
                vs.append(((vs[a] + vs[b]) * 0.5).normalized())
                cache[k] = len(vs) - 1
            return cache[k]

        for a, b, c in fs:
            ab, bc, ca = mid(a, b), mid(b, c), mid(c, a)
            nf += [(a, ab, ca), (b, bc, ab), (c, ca, bc), (ab, bc, ca)]
        fs = nf
    c = Vector(center)
    out = []
    for i, p in enumerate(vs):
        k = 1.0 + (jitter * (hash01(i, seed) * 2 - 1) if jitter else 0)
        out.append(tuple(c + Vector((p.x * scale[0], p.y * scale[1], p.z * scale[2])) * r * k))
    return out, fs


def prism(poly2d, z0, z1):
    """Extrude a convex-or-simple polygon in the XY plane (counter-clockwise) from z0 to z1."""
    n = len(poly2d)
    verts = [(x, y, z0) for x, y in poly2d] + [(x, y, z1) for x, y in poly2d]
    faces = [tuple(reversed(range(n))), tuple(range(n, 2 * n))]
    for i in range(n):
        j = (i + 1) % n
        faces.append((i, j, n + j, n + i))
    return verts, faces


def fin(points_yz, thickness, x=0.0):
    """A flat fin in the YZ plane: polygon (y, z) extruded along X."""
    n = len(points_yz)
    h = thickness / 2
    verts = [(x - h, y, z) for y, z in points_yz] + [(x + h, y, z) for y, z in points_yz]
    faces = [tuple(range(n)), tuple(reversed(range(n, 2 * n)))]
    for i in range(n):
        j = (i + 1) % n
        faces.append((j, i, n + i, n + j))
    return verts, faces


def merge(*vfs):
    verts, faces = [], []
    for v, f in vfs:
        b = len(verts)
        verts += list(v)
        faces += [tuple(i + b for i in face) for face in f]
    return verts, faces


def xf(vf, matrix):
    v, f = vf
    return [tuple(matrix @ Vector(p)) for p in v], f


def T(x=0, y=0, z=0):
    return Matrix.Translation((x, y, z))


def R(axis, deg):
    return Matrix.Rotation(math.radians(deg), 4, axis)


def S(x, y=None, z=None):
    y = x if y is None else y
    z = x if z is None else z
    return Matrix.Diagonal((x, y, z, 1))


# ----------------------------------------------------------------------------------------------------------------------
# Hashing
# ----------------------------------------------------------------------------------------------------------------------

def hash01(*k):
    h = 2166136261
    for x in k:
        x = int(x) & 0xFFFFFFFF
        for s in (0, 8, 16, 24):
            h ^= (x >> s) & 0xFF
            h = (h * 16777619) & 0xFFFFFFFF
    h ^= h >> 13
    h = (h * 0x5bd1e995) & 0xFFFFFFFF
    h ^= h >> 15
    return h / 4294967296.0


# ----------------------------------------------------------------------------------------------------------------------
# Voxel mesher: cells -> exposed faces, with clipped slope cells
# ----------------------------------------------------------------------------------------------------------------------

DIRS = [(1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1)]
CUBE_FACES = {
    (1, 0, 0): [(1, 0, 0), (1, 1, 0), (1, 1, 1), (1, 0, 1)],
    (-1, 0, 0): [(0, 1, 0), (0, 0, 0), (0, 0, 1), (0, 1, 1)],
    (0, 1, 0): [(1, 1, 0), (0, 1, 0), (0, 1, 1), (1, 1, 1)],
    (0, -1, 0): [(0, 0, 0), (1, 0, 0), (1, 0, 1), (0, 0, 1)],
    (0, 0, 1): [(0, 0, 1), (1, 0, 1), (1, 1, 1), (0, 1, 1)],
    (0, 0, -1): [(0, 1, 0), (1, 1, 0), (1, 0, 0), (0, 0, 0)],
}


def clip_poly(poly, n, d):
    """Keep the part of a polygon where dot(p, n) <= d (Sutherland-Hodgman)."""
    out = []
    m = len(poly)
    for i in range(m):
        a, b = poly[i], poly[(i + 1) % m]
        da = sum(a[k] * n[k] for k in range(3)) - d
        db = sum(b[k] * n[k] for k in range(3)) - d
        if da <= 1e-9:
            out.append(a)
        if (da < -1e-9 < db) or (db < -1e-9 < da):
            t = da / (da - db)
            out.append(tuple(a[k] + (b[k] - a[k]) * t for k in range(3)))
    # drop near-duplicates
    res = []
    for p in out:
        if not res or max(abs(p[k] - res[-1][k]) for k in range(3)) > 1e-7:
            res.append(p)
    if len(res) > 1 and max(abs(res[0][k] - res[-1][k]) for k in range(3)) < 1e-7:
        res.pop()
    return res if len(res) >= 3 else []


def cap_poly(n, d):
    """Polygon of plane dot(p,n)=d inside the unit cube, ordered counter-clockwise seen from +n."""
    pts = []
    corners = [(x, y, z) for x in (0, 1) for y in (0, 1) for z in (0, 1)]
    edges = [(a, b) for a in corners for b in corners if a < b and sum(abs(a[k] - b[k]) for k in range(3)) == 1]
    for a, b in edges:
        da = sum(a[k] * n[k] for k in range(3)) - d
        db = sum(b[k] * n[k] for k in range(3)) - d
        if abs(da) < 1e-9:
            pts.append(a)
        if abs(db) < 1e-9:
            pts.append(b)
        if da * db < -1e-12:
            t = da / (da - db)
            pts.append(tuple(a[k] + (b[k] - a[k]) * t for k in range(3)))
    uniq = []
    for p in pts:
        if all(max(abs(p[k] - q[k]) for k in range(3)) > 1e-7 for q in uniq):
            uniq.append(p)
    if len(uniq) < 3:
        return []
    c = Vector([sum(p[k] for p in uniq) / len(uniq) for k in range(3)])
    u, v, w = _frame(n)
    uniq.sort(key=lambda p: math.atan2((Vector(p) - c).dot(v), (Vector(p) - c).dot(u)))
    return uniq


def slope_plane(dirs):
    """Clip plane for a slope cell whose open (cut) directions are `dirs` (2 = wedge, 3 = corner)."""
    n = [0, 0, 0]
    for dd in dirs:
        for k in range(3):
            n[k] += dd[k]
    # plane through the cube centre for wedges; corner keeps x+y+z <= 1 relative to the far corner (pyramid)
    # dot(p, n) where p in [0,1]^3; the far vertex along n has value sum(max(n_k,0)).
    hi = sum(max(c, 0) for c in n)
    lo = sum(min(c, 0) for c in n)
    if len(dirs) == 2:
        d = (hi + lo) / 2.0
    else:
        d = lo + 2.0      # chamfered corner (5/6 of the cube): no spikes where wedges meet
    return tuple(n), d


_POLY_CACHE = {}


def cell_polys(shape):
    if shape not in _POLY_CACHE:
        _POLY_CACHE[shape] = _cell_polys(shape)
    return _POLY_CACHE[shape]


def _cell_polys(shape):
    """Polygons (unit cube space) for a cell shape: ('cube',) or ('slope', dirs)."""
    polys = []
    if shape[0] == "cube":
        for dvec, quad in CUBE_FACES.items():
            polys.append((dvec, quad, True))
        return polys
    n, d = slope_plane(shape[1])
    for dvec, quad in CUBE_FACES.items():
        cp = clip_poly(quad, n, d)
        if cp:
            full = len(cp) == 4 and all(any(max(abs(p[k] - q[k]) for k in range(3)) < 1e-7 for q in quad) for p in cp)
            polys.append((dvec, cp, full))
    cap = cap_poly(n, d)
    if cap:
        polys.append((None, cap, False))
    return polys


def auto_slopes(cells, allow=None, protect=None, filled=None):
    """Turn exposed edge/corner cells into slopes. cells: {(x,y,z): kind}. Returns {cell: ('slope', dirs)}.

    allow(cell, dirs) -> bool filters which cuts are allowed (e.g. no slopes on the underside)."""
    shapes = {}
    filled = filled if filled is not None else set(cells)
    for c in cells:
        if protect and c in protect:
            continue
        x, y, z = c
        empty = [d for d in DIRS if (x + d[0], y + d[1], z + d[2]) not in filled]
        if len(empty) < 2:
            continue
        # opposite of every empty side must be supported, so the slope leans on something
        cand = [d for d in empty if (x - d[0], y - d[1], z - d[2]) in filled]
        if len(cand) < 2:
            continue
        axes = {}
        for d in cand:
            ax = [k for k in range(3) if d[k]][0]
            axes.setdefault(ax, d)
        if len(axes) < 2:
            continue
        ds = list(axes.values())
        pick = None
        pairs = [(ds[i], ds[j]) for i in range(len(ds)) for j in range(i + 1, len(ds))]
        # prefer planform (x/y) pairs: they smooth diagonal outlines of wings and hulls
        pairs.sort(key=lambda pr: 0 if (pr[0][2] == 0 and pr[1][2] == 0) else 1)
        for pair in pairs:
            ax = [k for k in range(3) if not pair[0][k] and not pair[1][k]][0]
            e = [0, 0, 0]
            e[ax] = 1
            run = ax == 2 or (x + e[0], y + e[1], z + e[2]) in filled or (x - e[0], y - e[1], z - e[2]) in filled
            if run and (allow is None or allow(c, list(pair))):
                pick = list(pair)
                break
        if pick:
            shapes[c] = ("slope", tuple(pick))
    return shapes


def mesh_cells(cells, shapes, origin=(0, 0, 0), cell=CELL, tag_of=None, solid=None):
    """Exposed faces of a cell set. cells: {pos: tag}. shapes: {pos: shape}. Returns {tag: (verts, faces)} and a list
    of exposed full faces [(pos, dir)] for greebling."""
    out = {}
    exposed = []
    filled = solid if solid is not None else cells
    ox, oy, oz = origin
    for c, tag in cells.items():
        shape = shapes.get(c, ("cube",))
        x, y, z = c
        for dvec, poly, full in cell_polys(shape):
            if dvec is not None:
                nb = (x + dvec[0], y + dvec[1], z + dvec[2])
                if nb in filled:
                    nshape = shapes.get(nb, ("cube",))
                    if nshape[0] == "cube" or nshape == shape:
                        continue
                    # neighbour slope: cull only if both faces are full squares
                    opp = (-dvec[0], -dvec[1], -dvec[2])
                    nfull = any(dv == opp and fl for dv, _, fl in cell_polys(nshape))
                    if full and nfull:
                        continue
                    if not full and nfull:
                        continue
                if full and shape[0] == "cube":
                    exposed.append((c, dvec))
            t = tag_of(c, dvec) if tag_of else tag
            verts, faces = out.setdefault(t, ([], []))
            b = len(verts)
            for p in poly:
                verts.append(((x + p[0]) * cell + ox, (y + p[1]) * cell + oy, (z + p[2]) * cell + oz))
            faces.append(tuple(range(b, b + len(poly))))
    return out, exposed
