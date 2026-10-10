"""Build checks for ships and stations: the rules the game's builder will enforce, run on the look-dev designs.

1. Every block is attached: the whole ship is one piece when cells, big blocks and modules are joined only through
   real shared faces (any overlap of solid face area on both sides; touching along an edge or at a
   corner does not count).
2. Every module sits on its mount face: turrets, masts and housings on their base, engines on their front. At least
   half of the cells across that face must be solid with a full face toward the module.
3. Shape catalogue: how many distinct cell shapes the hull uses, counted up to rotation and mirroring. This is the
   number of block shapes the game's mesher would need to support for these hulls.
"""

import itertools
import math

from mathutils import Vector

from ld_blocks import KIT
from ld_geom import cell_polys, face_of, _area, classify_cell

DIRS = [(1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1)]

FRONT = {"thruster_s", "thruster_l", "thruster_xl"}
ANY = {"reactor", "core_1", "core_2", "core_3", "core_35", "mass_driver", "capacitor", "lance", "singularity", "hangar",
       "cargo"}


def mount_normal(kind, Rm):
    """World direction from the module toward the structure it must sit on."""
    if kind in ANY:
        return None
    local = Vector((0, 1, 0)) if kind in FRONT else Vector((0, 0, -1))
    w = Rm @ local
    return tuple(int(round(v)) for v in w)


def _face_area(shape, d):
    if shape is None:
        return 1.0
    f = face_of(shape, d)
    if f is None:
        return 0.0
    poly, full = f
    return 1.0 if full else _area(poly)


def _small_face(ship, c, d):
    """Area (0..1) of the face of 0.5 m cell c on side d that is solid, looking at cells, big blocks and modules."""
    if c in ship.mod_cells:
        return 1.0
    if c in ship.cells:
        return _face_area(ship.shapes.get(c), d)
    if ship.big:
        B = ship.big_of(c)
        if B in ship.big:
            if B not in ship.big_shapes:
                return 1.0
            r = classify_cell(ship.big_world_planes(B), c)
            if r[0] == "full":
                return 1.0
            if r[0] == "empty":
                return 0.0
            return _face_area(("clip", r[1]), d)
    return 0.0


class _UF:
    def __init__(self):
        self.p = {}

    def find(self, a):
        p = self.p
        p.setdefault(a, a)
        while p[a] != a:
            p[a] = p[p[a]]
            a = p[a]
        return a

    def union(self, a, b):
        ra, rb = self.find(a), self.find(b)
        if ra != rb:
            self.p[ra] = rb


def check(ship, verbose=False):
    """Returns a dict with problems and stats."""
    pruned = ship.prune() if hasattr(ship, "prune") else 0
    modcell = {}
    for i, (kind, mn, Rm, size, rsz, remap) in enumerate(ship.mods):
        for dx in range(rsz[0]):
            for dy in range(rsz[1]):
                for dz in range(rsz[2]):
                    modcell[(mn[0] + dx, mn[1] + dy, mn[2] + dz)] = i
    problems = []
    # 1. overlapping modules
    seen = {}
    for i, (kind, mn, Rm, size, rsz, remap) in enumerate(ship.mods):
        for dx in range(rsz[0]):
            for dy in range(rsz[1]):
                for dz in range(rsz[2]):
                    c = (mn[0] + dx, mn[1] + dy, mn[2] + dz)
                    if c in seen and seen[c] != i:
                        problems.append(f"overlap: {kind}@{mn} and {ship.mods[seen[c]][0]}@{ship.mods[seen[c]][1]}")
                    seen[c] = i
    # 2. mount faces
    for i, (kind, mn, Rm, size, rsz, remap) in enumerate(ship.mods):
        n = mount_normal(kind, Rm)
        cells = [(mn[0] + dx, mn[1] + dy, mn[2] + dz) for dx in range(rsz[0]) for dy in range(rsz[1]) for dz in range(rsz[2])]
        foot = set(cells)
        best = 0.0
        normals = [n] if n else DIRS
        for nn in normals:
            ring = [c for c in cells if (c[0] + nn[0], c[1] + nn[1], c[2] + nn[2]) not in foot]
            opp = (-nn[0], -nn[1], -nn[2])
            sup = 0.0
            for c in ring:
                nb = (c[0] + nn[0], c[1] + nn[1], c[2] + nn[2])
                if nb in modcell:
                    sup += 1.0
                else:
                    sup += min(1.0, _small_face(ship, nb, opp) * 1.25)
            best = max(best, sup / max(1, len(ring)))
        if best < 0.5:
            where = "base" if n == (0, 0, -1) else ("front" if n and kind in FRONT else ("mount " + str(n) if n else "any face"))
            problems.append(f"unsupported: {kind}@{mn} {where} {best:.0%} supported")
    # 3. one piece
    uf = _UF()
    u = ship.u

    def node(c):
        if c in modcell:
            return ("m", modcell[c])
        if c in ship.cells:
            return ("c", c)
        if ship.big and ship.big_of(c) in ship.big:
            return ("b", ship.big_of(c))
        return None

    for c in list(ship.cells) + list(modcell):
        a = node(c)
        uf.find(a)
        for d in DIRS:
            nb = (c[0] + d[0], c[1] + d[1], c[2] + d[2])
            b = node(nb)
            if b is None or b == a:
                continue
            opp = (-d[0], -d[1], -d[2])
            if _small_face(ship, c, d) >= 0.02 and _small_face(ship, nb, opp) >= 0.02:
                uf.union(a, b)
    for B in ship.big:
        a = ("b", B)
        uf.find(a)
        for d in DIRS:
            nb = (B[0] + d[0], B[1] + d[1], B[2] + d[2])
            if nb in ship.big:
                opp = (-d[0], -d[1], -d[2])
                if _face_area(ship.big_shapes.get(B), d) >= 0.02 and _face_area(ship.big_shapes.get(nb), opp) >= 0.02:
                    uf.union(a, ("b", nb))
    comps = {}
    for k in list(uf.p):
        comps.setdefault(uf.find(k), []).append(k)
    groups = sorted(comps.values(), key=len, reverse=True)
    for g in groups[1:]:
        mods = sorted({ship.mods[k[1]][0] for k in g if k[0] == "m"})
        pts = [k[1] for k in g if k[0] == "c"] + [tuple(v * u for v in k[1]) for k in g if k[0] == "b"] + \
              [ship.mods[k[1]][1] for k in g if k[0] == "m"]
        lo = tuple(min(p[i] for p in pts) for i in range(3))
        hi = tuple(max(p[i] for p in pts) for i in range(3))
        problems.append(f"loose piece: {len(g)} parts {lo}..{hi}" + (f" with {', '.join(mods)}" if mods else ""))
    # 4. shape catalogue
    keys = {}
    for c, sh in ship.shapes.items():
        keys[canon(sh[1])] = keys.get(canon(sh[1]), 0) + 1
    for B, sh in ship.big_shapes.items():
        keys[canon(sh[1])] = keys.get(canon(sh[1]), 0) + 1
    return dict(name=ship.name, problems=problems, cells=ship.cell_count(), cut=len(ship.shapes) + len(ship.big_shapes),
                shape_types=len(keys), big=len(ship.big), u=u, mods=len(ship.mods), pieces=len(groups), pruned=pruned)


_SYM = None


def _syms():
    global _SYM
    if _SYM is None:
        _SYM = []
        for perm in itertools.permutations(range(3)):
            for signs in itertools.product((1, -1), repeat=3):
                _SYM.append((perm, signs))
    return _SYM


def canon(planes):
    """Canonical key of a cut-cell shape under the 48 symmetries of the cube (rotations and mirrors)."""
    best = None
    for perm, signs in _syms():
        out = []
        for n, d in planes:
            L = math.sqrt(sum(v * v for v in n)) or 1.0
            m = [signs[i] * n[perm[i]] / L for i in range(3)]
            # p' = S P (p - 0.5) + 0.5  ->  n'.p' <= d/L - n.0.5/L + n'.0.5
            dd = d / L - sum(n) * 0.5 / L + sum(m) * 0.5
            out.append((tuple(round(v, 4) + 0.0 for v in m), round(dd, 4) + 0.0))
        key = tuple(sorted(out))
        if best is None or key < best:
            best = key
    return best


def report(ships):
    lines = []
    ok = True
    for s in ships:
        r = check(s)
        flag = "OK " if not r["problems"] else "FIX"
        ok &= not r["problems"]
        big = f", {r['big']} big blocks ({r['u']}x{r['u']}x{r['u']})" if r["big"] else ""
        lines.append(f"{flag} {r['name']}: {r['cells']} cells{big}, {r['mods']} modules, {r['cut']} cut cells, "
                     f"{r['shape_types']} cut shapes, {r['pruned']} slivers pruned")
        for p in r["problems"]:
            lines.append(f"      - {p}")
    return ok, lines
