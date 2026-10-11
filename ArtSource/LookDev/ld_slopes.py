"""Slope study (owner, 2026-10-11): "lets model the different sloped blocks so we can see how many are needed
(ideally as least as possible)", then "20 slopes is insane ... it can be 1x2s or 1x3s or 2x3" and "i want something
sensible so builders can build with". Ledger open question 6.

The answer is a small builder kit, not a match for the look-dev hulls. A slope block is one placeable block (a 1:2
slope is ONE 1x2 block); every block rotates freely. `render` builds, for each kit in KITS:
  - an isometric drawing of each block (block_<key>.svg);
  - the same small demo frigate designed on the kit's angles, with every cut cell limited to the kit's blocks
    (demo_<kit>.jpg and a nose close-up), the way a player with that kit would build it.
The share of cells a kit cannot build within 10 % was measured on every round-3 hull and the HALO, each designed on the
kit's angles (KITS[...][4]).

`stats` keeps the first, per-cell analysis (how many cut-cell shapes the look-dev hulls use); the owner set it aside.

Run inside Blender (it needs the look-dev mesher):
  blender -b --factory-startup --python ArtSource/LookDev/ld_slopes.py -- render [--draft]
"""

import itertools
import json
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, HERE)

from ld_check import _syms, canon  # noqa: E402

N = 8
_g = (np.arange(N) + 0.5) / N
PTS = np.stack(np.meshgrid(_g, _g, _g, indexing="ij"), -1).reshape(-1, 3)
OUT_JS = os.path.join(ROOT, "docs", "design", "lookbook", "slopes.js")
OUT_IMG = os.path.join(ROOT, "docs", "design", "lookbook", "img", "r4", "slopes")


# ======================================================================================================================
# Shapes
# ======================================================================================================================

def occ(planes):
    m = np.ones(len(PTS), bool)
    for n, d in planes:
        m &= PTS @ np.asarray(n, float) <= d + 1e-9
    return m


def orient(planes, perm, signs):
    """The same shape after the cube symmetry p' = S P (p - 0.5) + 0.5 (as ld_check.canon)."""
    out = []
    for n, d in planes:
        m = [signs[i] * n[perm[i]] for i in range(3)]
        dd = d - sum(n) * 0.5 + sum(m) * 0.5
        out.append((tuple(float(round(v, 6)) + 0.0 for v in m), round(float(dd), 6) + 0.0))
    return tuple(sorted(out))


def piece(n, i, run=1, rise=2):
    """Slope rising 1 cell over n cells: piece i of n (0 = lowest). Solid below the surface."""
    v = [0, 0, 0]
    v[run], v[rise] = -1, n
    return (((tuple(float(k) for k in v)), float(i)),)


PIECES = [(n, i) for n in (1, 2, 3) for i in range(n)]


def piece_name(n, i):
    if n == 1:
        return "1:1"
    return f"1:{n} {('low', 'high')[i] if n == 2 else ('low', 'mid', 'high')[i]}"


def candidates():
    """[(name, family, planes)]: one representative per shape up to the 48 cube symmetries."""
    seen = {}

    def add(name, fam, planes):
        v = occ(planes).mean()
        if v < 0.03 or v > 0.97:
            return
        k = canon(planes)
        if k not in seen:
            seen[k] = (name, fam, planes)

    for n, i in PIECES:
        add(f"Slope {piece_name(n, i)}", n, piece(n, i))
    for (n1, i1), (n2, i2) in itertools.combinations_with_replacement(PIECES, 2):
        a = piece(n1, i1)
        for perm, signs in _syms():
            b = orient(piece(n2, i2), perm, signs)
            if b == a:
                continue
            planes = tuple(sorted(set(a) | set(b)))
            add(f"Corner {piece_name(n1, i1)} × {piece_name(n2, i2)}", max(n1, n2), planes)
    for nrm in ((1, 1, 1), (1, 1, 2), (1, 2, 2)):
        s = sum(nrm)
        for k in range(1, 2 * s):
            add(f"Oblique corner {nrm[0]}·{nrm[1]}·{nrm[2]} at {k / 2:g}", max(nrm), ((tuple(float(x) for x in nrm), k / 2),))
    return list(seen.values())


def oriented(cands):
    """Occupancy of every candidate in every distinct orientation: (matrix, candidate index, planes)."""
    rows, idx, pl = [], [], []
    for j, (_, _, planes) in enumerate(cands):
        got = set()
        for perm, signs in _syms():
            p = orient(planes, perm, signs)
            o = occ(p)
            key = o.tobytes()
            if key in got:
                continue
            got.add(key)
            rows.append(o)
            idx.append(j)
            pl.append(p)
    return np.array(rows, np.float32), np.array(idx), pl


# ======================================================================================================================
# The hulls
# ======================================================================================================================

def human_ships():
    """The human hulls the owner has seen and kept: round-3 ships and stations of both worlds, and the HALO. The round-4
    line-B drafts (rejected as rectangles) are left out."""
    from ld_ships import DESIGNS
    from ld_ark import ARK_SHIPS
    from ld_stations import STATIONS
    out = []
    for d in ("nocturne", "drydock"):
        for k, fn in DESIGNS[d]:
            if not k.startswith("b_"):
                out.append((f"{d}/{k}", fn))
        out.append((f"{d}/station", STATIONS[d]))
    for k, fn in ARK_SHIPS:
        out.append((f"ark/{k}", fn))
    return out


def gather(ships):
    """Distinct cut shapes up to symmetry, with counts per ship set: {canon: [planes, count_all, {set: count}]}."""
    shapes = {}
    per_ship = {}
    for name, fn in ships:
        s = fn()
        s.prune()
        n = 0
        for store in (s.shapes, s.big_shapes):
            for _, sh in store.items():
                k = canon(sh[1])
                e = shapes.setdefault(k, [sh[1], 0, {}])
                e[1] += 1
                e[2][name] = e[2].get(name, 0) + 1
                n += 1
        per_ship[name] = dict(cut=n, cells=len(s.cells), big=len(s.big))
        print("SLOPES gathered", name, n, flush=True)
    return shapes, per_ship


# ======================================================================================================================
# Scoring
# ======================================================================================================================

def error_matrix(shapes, O, oidx, ncand):
    keys = list(shapes)
    C = np.array([occ(shapes[k][0]) for k in keys], np.float32)
    w = np.array([shapes[k][1] for k in keys], np.float64)
    cs = C.sum(1, keepdims=True)
    err = (cs + O.sum(1)[None, :] - 2 * C @ O.T) / len(PTS)
    E = np.full((len(keys), ncand), np.inf, np.float32)
    for j in range(ncand):
        cols = np.where(oidx == j)[0]
        E[:, j] = err[:, cols].min(1)
    base = np.minimum(cs[:, 0], len(PTS) - cs[:, 0]) / len(PTS)      # cube or nothing
    return keys, w, E, base


def score(best, w):
    t = w.sum()
    return dict(mean=float((best * w).sum() / t), exact=float(w[best <= 0.05].sum() / t), close=float(w[best <= 0.15].sum() / t),
                bad=float(w[best > 0.3].sum() / t))


def greedy(E, base, w, steps=24):
    best = base.copy()
    chosen, curve = [], [dict(k=0, **score(best, w))]
    for step in range(steps):
        gain = (w[:, None] * np.maximum(0, best[:, None] - E)).sum(0)
        gain[chosen] = -1
        j = int(gain.argmax())
        if gain[j] <= 0:
            break
        chosen.append(j)
        best = np.minimum(best, E[:, j])
        curve.append(dict(k=step + 1, add=j, **score(best, w)))
    return chosen, curve


def stats():
    cands = candidates()
    O, oidx, opl = oriented(cands)
    print(f"SLOPES {len(cands)} candidate shapes, {len(O)} oriented", flush=True)
    ships = human_ships()
    shapes, per_ship = gather(ships)
    keys, w, E, base = error_matrix(shapes, O, oidx, len(cands))
    print(f"SLOPES {len(keys)} distinct cut shapes in {int(w.sum())} cut blocks", flush=True)
    chosen, curve = greedy(E, base, w)
    names = [c[0] for c in cands]
    fam = [c[1] for c in cands]

    def fixed(pred):
        js = [j for j in range(len(cands)) if pred(j)]
        best = np.minimum(base, E[:, js].min(1)) if js else base
        return dict(n=len(js), **score(best, w))

    wedge = names.index("Slope 1:1")
    sets = {
        "cube": dict(n=0, **score(base, w)),
        "wedge": fixed(lambda j: j == wedge),
        "f1": fixed(lambda j: fam[j] == 1),
        "f2": fixed(lambda j: fam[j] <= 2),
        "f3": fixed(lambda j: fam[j] <= 3),
    }
    grids = grid_stats(ships)
    for m, k in CATALOGUES:
        g = grids[m]
        iso_catalogue(g["keys"][:k], g["counts"][:k], g["cut"], os.path.join(OUT_IMG, f"catalogue_1to{m}.svg"))
    for g in grids.values():
        g.pop("counts")
        keys = g.pop("keys")
        g["vols"] = [round(float(occ(k).mean()), 3) for k in keys[:12]]
    data = dict(grid=grids, candidates=len(cands), distinct=len(keys), blocks=int(w.sum()), per_ship=per_ship, sets=sets,
                greedy=[dict(c, name=names[c["add"]] if "add" in c else None) for c in curve],
                chosen=[dict(name=names[j], family=fam[j], planes=cands[j][2]) for j in chosen],
                fam_sizes={str(f): sum(1 for x in fam if x <= f) for f in (1, 2, 3)})
    with open(OUT_JS, "w") as fh:
        fh.write("/* written by ArtSource/LookDev/ld_slopes.py */\nwindow.SLOPES = " + json.dumps(data, indent=1) + ";\n")
    for c in data["greedy"]:
        print(f"SLOPES k={c['k']:2d} mean={c['mean']:.3f} exact={c['exact']:.3f} close={c['close']:.3f} bad={c['bad']:.3f} {c['name']}")
    for k, v in sets.items():
        print(f"SLOPES set {k}: {v}")


# ======================================================================================================================
# Designing on a slope grid: snap every hull face to an allowed slope and to the cell lattice
# ======================================================================================================================

def allowed_normals(m):
    """Integer face directions whose components are at most m (a slope of at most 1:m in any direction)."""
    import math as _m
    out = set()
    for v in itertools.product(range(-m, m + 1), repeat=3):
        if not any(v):
            continue
        g = _m.gcd(_m.gcd(abs(v[0]), abs(v[1])), abs(v[2]))
        out.add(tuple(k // g for k in v))
    arr = np.array(sorted(out), float)
    return arr, arr / np.linalg.norm(arr, axis=1, keepdims=True)


class SlopeGrid:
    """While active, Ship.convex snaps every plane's direction to the nearest allowed slope and its offset to the
    lattice (whole cells, or whole big blocks for big pieces), anchored where the plane crosses the piece."""

    def __init__(self, m):
        self.m = m
        self.V, self.Vn = allowed_normals(m)

    def snap(self, planes, bbox, u=1):
        c0 = (np.array(bbox[0], float) + np.array(bbox[1], float)) / 2
        out = []
        for n, d in planes:
            n = np.asarray(n, float)
            L = np.linalg.norm(n)
            nu, dd = n / L, d / L
            j = int(np.argmax(self.Vn @ nu))
            v = self.V[j]
            p0 = c0 - (nu @ c0 - dd) * nu
            off = round(float(v @ p0) / u) * u
            out.append((tuple(float(k) for k in v), float(off)))
        return out

    def __enter__(self):
        import ld_ships
        self._orig = ld_ships.Ship.convex
        grid = self

        def convex(ship, planes, bbox, tag="hull", mirror=False, big=False):
            u = ship.u if (big and ship.u > 1) else 1
            return grid._orig(ship, grid.snap(planes, bbox, u), bbox, tag, mirror, big)
        ld_ships.Ship.convex = convex
        return self

    def __exit__(self, *a):
        import ld_ships
        ld_ships.Ship.convex = self._orig


def grid_stats(ships, ms=(1, 2, 3)):
    """For each slope limit: how many distinct block shapes the snapped hulls use, and how often."""
    out = {}
    for m in ms:
        counts = {}
        total = 0
        with SlopeGrid(m):
            for name, fn in ships:
                s = fn()
                s.prune()
                for store in (s.shapes, s.big_shapes):
                    for _, sh in store.items():
                        k = canon(sh[1])
                        counts[k] = counts.get(k, 0) + 1
                        total += 1
        freq = sorted(counts.values(), reverse=True)
        cum = np.cumsum(freq) / max(1, total)
        out[m] = dict(shapes=len(counts), cut=total, top=[int(x) for x in freq[:40]],
                      for95=int(np.searchsorted(cum, 0.95) + 1), for99=int(np.searchsorted(cum, 0.99) + 1),
                      for999=int(np.searchsorted(cum, 0.999) + 1))
        out[m]["keys"] = sorted(counts, key=lambda k: -counts[k])
        out[m]["counts"] = [counts[k] for k in out[m]["keys"]]
        print(f"SLOPES grid 1:{m}: {len(counts)} shapes over {total} cut blocks; 95% {out[m]['for95']}, 99% {out[m]['for99']}, "
              f"99.9% {out[m]['for999']}", flush=True)
    return out


# ======================================================================================================================
# Block types: multi-cell slope blocks (owner, 2026-10-11: "it doesn't have to be all 1x1s it can be 1x2s or 1x3s or
# 2x3"). A slope of rise a over run b is ONE block, b cells long and a cells tall; its cells are pieces of one plane.
# Its corner is one block too (b x b x a), where two of those slopes meet.
# ======================================================================================================================

def slope_block(a, b):
    """Per-cell pieces of a straight slope block: rise a cells over run b cells (surface b*z <= a*y)."""
    out = []
    for i in range(b):
        for j in range(a):
            out.append((((0.0, -float(a), float(b)), float(a * i - b * j)),))
    return out


def corner_block(a, b):
    """Per-cell pieces of the outer corner of that slope: two slopes meeting at a hip (b x b x a block)."""
    out = []
    for i in range(b):
        for k in range(b):
            for j in range(a):
                out.append((((0.0, -float(a), float(b)), float(a * i - b * j)), ((-float(a), 0.0, float(b)), float(a * k - b * j))))
    return out


def tetra_block():
    """The 1x1 three-face corner (where a tapered chamfer meets): one oblique plane."""
    return [(((1.0, 1.0, 1.0), 1.0),)]


BLOCK_TYPES = {
    # key: (name, per-cell pieces, slope directions it adds, block size in cells (x, y, z), planes in block space)
    "cube": ("Cube", lambda: [], [], (1, 1, 1), []),
    "w11": ("Slope", lambda: slope_block(1, 1), [(0, 1, 1)], (1, 1, 1), [((0, -1, 1), 0)]),
    "s12": ("Long slope 1×2", lambda: slope_block(1, 2), [(0, 1, 2)], (1, 2, 1), [((0, -1, 2), 0)]),
    "tet": ("Corner", tetra_block, [(1, 1, 1)], (1, 1, 1), [((1, 1, 1), 1)]),
    "inv": ("Inverted corner", lambda: [(((1.0, 1.0, 1.0), 2.0),)], [(1, 1, 1)], (1, 1, 1), [((1, 1, 1), 2)]),
    "hip": ("Hip corner", lambda: corner_block(1, 1), [], (1, 1, 1), [((0, -1, 1), 0), ((-1, 0, 1), 0)]),
    "hip12": ("Long hip corner 2×2", lambda: corner_block(1, 2), [], (2, 2, 1), [((0, -1, 2), 0), ((-1, 0, 2), 0)]),
}

# The kits proposed to the owner (cube is always there): (key, label, block types, note)
KITS = [
    ("spec", "Today's spec: cube and slope", ["w11"], "Every diagonal edge where two slopes meet becomes a stair step.", 0.090),
    ("k2", "2 blocks: slope and hip corner", ["w11", "hip"],
     "Everything at 45°. At that angle the corner where two slopes meet is the same block in every direction, so one hip "
     "corner closes ridges, pyramid noses and chamfered edges.", 0.0147),
    ("k3", "3 blocks: slope, hip corner, long slope 1×2", ["w11", "hip", "s12"],
     "Adds a shallow 27° slope for long flat runs: decks, glacis armour, wings. Its edges have no corner block, so wherever "
     "it meets another slope the edge steps (the nose below).", 0.0333),
    ("k4", "4 blocks: kit 3 plus a long hip corner 2×2", ["w11", "hip", "s12", "hip12"],
     "Closes the ridge where two long slopes meet, but not a long-slope nose or a long slope meeting a 45° edge: those are "
     "different corners again. A fully closed 1×2 family needs about four blocks of its own.", 0.0228),
]
KITS_BY_KEY = {k[0]: k for k in KITS}


def option_normals(types):
    out = set()
    for t_ in types:
        for v in BLOCK_TYPES[t_][2]:
            for perm in itertools.permutations(range(3)):
                for sg in itertools.product((1, -1), repeat=3):
                    w = tuple(sg[i] * v[perm[i]] for i in range(3))
                    if any(w):
                        out.add(w)
    arr = np.array(sorted(out), float)
    return arr, arr / np.linalg.norm(arr, axis=1, keepdims=True)


def option_pieces(types):
    """[(label, type, planes)] one per distinct per-cell piece (up to symmetry) of the chosen block types."""
    seen = {}
    for t_ in types:
        for planes in BLOCK_TYPES[t_][1]():
            v = occ(planes).mean()
            if v < 0.02 or v > 0.98:
                continue
            k = canon(planes)
            if k not in seen:
                seen[k] = (BLOCK_TYPES[t_][0], t_, planes)
    return list(seen.values())


class OptionGrid(SlopeGrid):
    """Snap hull faces to the slopes of a block-type option only."""

    def __init__(self, types):
        self.V, self.Vn = option_normals(types)


def sample_hull():
    """A plain test hull a player might build: a box body with chamfered edges, a pointed nose that drops toward the
    bow, a tapered stern, and a bridge tower. Not one of the look-dev ships."""
    from ld_ships import Ship
    s = Ship("SAMPLE", 7, None)
    s.loft([(-14, 6, 0, 8, 2, 2), (0, 10, 0, 12, 3, 3), (24, 10, 0, 12, 3, 3), (40, 3, 1, 8, 1, 1)])
    s.loft([(4, 5, 12, 15, 2, 0), (14, 5, 12, 15, 2, 0), (20, 2, 12, 14, 1, 0)], "hull2")
    s.slab([(-10, 2), (-22, 8), (-22, 18), (-10, 20)], 4, 6, 0, 0, "armor")
    s.slab([(10, 2), (22, 8), (22, 18), (10, 20)], 4, 6, 0, 0, "armor")
    s.mod("thruster_l", -6, -20, 2)
    s.mod("thruster_l", 2, -20, 2)
    s.mod("autocannon", -1, 30, 10)
    s.mod("cockpit", -2, 12, 15)
    return s


def demo_ship(kit):
    """The same small frigate (about 13 x 25 m) as a player would build it with one kit: every face is at one of the
    kit's angles, and every cut cell is then limited to the kit's blocks (a builder can only place those)."""
    long = kit in KITS_BY_KEY and "s12" in KITS_BY_KEY[kit][2]
    s = _build_demo(long)
    if kit != "free":
        O, oidx, opl = oriented(option_pieces(KITS_BY_KEY[kit][2]))
        quantize(s, list(range(int(oidx.max()) + 1)), O, oidx, opl)
    return s


def _build_demo(long):
    from ld_ships import Ship
    s = Ship("DEMO", 5, None)
    s.greeble_override = dict(window=0.04, hatch=0.04, vent=0, pipe=0.05, light=0.01, antenna=0, handrail=0, stripe=0, plate=0,
                              rib=0, strobe=0, spike=0)
    W, Z, Y1 = 8, 8, 28

    def body(y0, y1, extra):
        planes = [((1, 0, 0), W), ((-1, 0, 0), W), ((0, 1, 0), y1), ((0, -1, 0), -y0), ((0, 0, 1), Z), ((0, 0, -1), 0),
                  ((1, 0, 1), W + Z - 2), ((-1, 0, 1), W + Z - 2), ((1, 0, -1), W - 2), ((-1, 0, -1), W - 2)] + extra
        s.convex(planes, ((-W, y0, 0), (W, y1, Z)))
    body(-4, Y1, [((0, -1, 1), Z + 2)])                       # body; the stern's top edge chamfered 45 degrees
    if long:                                                  # nose: the deck falls 1 in 2 and the sides close 1 in 2
        body(Y1, Y1 + 16, [((2, 1, 0), 2 * W + Y1), ((-2, 1, 0), 2 * W + Y1), ((0, 1, 2), Y1 + 2 * Z)])
    else:                                                     # nose: a 45-degree pyramid
        body(Y1, Y1 + 10, [((1, 1, 0), W + Y1), ((-1, 1, 0), W + Y1), ((0, 1, 1), Y1 + Z)])
    # bridge: a chamfered block with a sloped front
    s.convex([((1, 0, 0), 4), ((-1, 0, 0), 4), ((0, 1, 0), 22), ((0, -1, 0), -8), ((0, 0, 1), 13), ((0, 0, -1), -7),
              ((1, 0, 1), 4 + 13 - 2), ((-1, 0, 1), 4 + 13 - 2), ((0, 1, 1), 22 + 13 - 3)], ((-4, 8, 7), (4, 22, 13)), "hull2")
    # engine sponsons, with 45-degree ends in plan
    for sx in (1, -1):
        s.convex([((sx, 0, 0), 13), ((-sx, 0, 0), -(W - 1)), ((0, 1, 0), 14), ((0, -1, 0), 4), ((0, 0, 1), 6), ((0, 0, -1), -1),
                  ((sx, 1, 0), 13 + 14 - 3), ((sx, -1, 0), 13 + 4 - 3), ((sx, 0, 1), 13 + 6 - 2)],
                 ((min(sx * 13, sx * (W - 1)), -4, 1), (max(sx * 13, sx * (W - 1)), 14, 6)), "armor")
    s.mod("thruster_l", -6, -10, 2)
    s.mod("thruster_l", 2, -10, 2)
    s.mod("autocannon", -1, 28, 7)
    s.mod("cockpit", -2, 15, 13)
    s.prune()
    return s


def kit_report(types, long_nose=False):
    """How a kit copes with the demo design: blocks used per type, and cells it cannot build within 10 %."""
    pieces = option_pieces(types)
    O, oidx, opl = oriented(pieces)
    osum = O.sum(1)
    src = _build_demo(long_nose)
    use, bad, tot = {}, 0, 0
    for store in (src.shapes, src.big_shapes):
        for _, sh in store.items():
            c = occ(sh[1]).astype(np.float32)
            n = c.sum()
            errs = (n + osum - 2 * O @ c) / len(PTS)
            opts = [(len(PTS) - n) / len(PTS), n / len(PTS)]
            j = int(np.argmin(errs))
            e = float(errs[j])
            if min(opts) < e:
                e, kind = min(opts), "cube or empty"
            else:
                kind = pieces[oidx[j]][0]
            use[kind] = use.get(kind, 0) + 1
            bad += e > 0.10
            tot += 1
    return dict(use=use, bad=bad, cut=tot)


def wraith_rebuilt(kit):
    """The NOCTURNE WRAITH redesigned by a builder for one kit: the same dropship (hull, ram nose, gun pods on stub
    wings, twin fins, engine block), drawn so every sloped cell is exactly one of the kit's blocks.
    k2 (slope + hip corner): 45-degree chamfers, a 45-degree arrowhead ram with chamfered edges, hip corners wherever
    two slopes meet. k3 (adds the long slope): the same 45-degree hull, pods and hips, plus a long 1-in-2 arrowhead
    ram and 1-in-2 fin tops; long slopes only meet flat faces, so nothing steps."""
    from ld_ships import Ship
    long = kit == "k3"
    s = Ship("WRAITH", 21, "company")
    W, Z = 4, 8
    # hull: an octagon with 45-degree chamfers, top 2 cells and bottom 1 cell
    hull = [((1, 0, 0), W), ((-1, 0, 0), W), ((0, 0, 1), Z), ((0, 0, -1), 0),
            ((1, 0, 1), W + Z - 2), ((-1, 0, 1), W + Z - 2), ((1, 0, -1), W - 1), ((-1, 0, -1), W - 1)]
    s.convex(hull + [((0, 1, 0), 26), ((0, -1, 0), 0)], ((-W, 0, 0), (W, 26, Z)))
    # shoulders: the hull narrows to the ram at 45 degrees in plan; hip corners close the chamfers
    s.convex(hull + [((0, 1, 0), 28), ((0, -1, 0), -26), ((1, 1, 0), W + 26), ((-1, 1, 0), W + 26)], ((-W, 26, 0), (W, 28, Z)))
    # the ram nose: 4 cells wide, an arrowhead in side view meeting on a cell corner
    ram = [((1, 0, 0), 2), ((-1, 0, 0), 2), ((0, 0, -1), 0), ((0, -1, 0), -28)]
    if long:
        ram += [((0, 1, 2), 28 + 2 * Z), ((0, 1, -2), 36)]          # deck 1 in 2 down to y=44, chin 1 in 2: tip at y=40
        s.convex(ram + [((0, 1, 0), 40)], ((-2, 28, 0), (2, 40, Z)))
    else:
        ram += [((0, 1, 1), 28 + Z), ((0, 1, -1), 30), ((1, 0, 1), 2 + Z - 1), ((-1, 0, 1), 2 + Z - 1)]   # tip at y=33
        s.convex(ram + [((0, 1, 0), 33)], ((-2, 28, 0), (2, 33, Z)))
    # the engine block at the stern
    s.loft([(-6, 5, 0, 8, 3, 2), (2, 5, 0, 8, 3, 2)], "hull2")
    # gun pods on stub wings: pointed fronts at 45 degrees, chamfered tops, hip corners where they meet
    for sx in (1, -1):
        a, b = (4, 10) if sx > 0 else (-10, -4)
        pod = [((1, 0, 0), b), ((-1, 0, 0), -a), ((0, 0, 1), 3), ((0, 0, -1), -1), ((0, -1, 0), -5), ((0, 1, 0), 16),
               ((1, 1, 0), b + 13), ((-1, 1, 0), -a + 13), ((1, 0, 1), b + 3 - 1), ((-1, 0, 1), -a + 3 - 1)]
        s.convex(pod, ((a, 5, 1), (b, 16, 3)), "armor")
        s.box(min(sx * 4, sx * 5), max(sx * 4, sx * 5), 8, 12, 1, 3, "armor")
    # tail fins: one cell thick, their tops falling toward the bow
    for sx in (1, -1):
        x0, x1 = (3, 4) if sx > 0 else (-4, -3)
        top = ((0, 1, 2), 24) if long else ((0, 1, 1), 10)
        s.convex([((1, 0, 0), x1), ((-1, 0, 0), -x0), ((0, 1, 0), 2), ((0, -1, 0), 4), ((0, 0, 1), 14), ((0, 0, -1), -6), top],
                 ((x0, -4, 6), (x1, 2, 14)), "hull2")
    s.paint(lambda x, y, z: z < 1.5, "armor")
    s.paint(lambda x, y, z: 18 <= y < 19, "accent")
    s.mod("cockpit", -2, 21, 8)
    s.mod("gatling", 6, 9, -1, rot=(0, 180, 0), mirror=True)
    s.mod("autocannon", -1, 24, -2, rot=(0, 180, 0))
    s.mod("thruster_l", -2, -12, 1)
    s.mod("thruster_s", 8, 2, 1, mirror=True)
    s.mod("sensor", -1, 12, 8)
    s.mod("core_1", 0, 14, 2)
    s.prune()
    O, oidx, opl = oriented(option_pieces(KITS_BY_KEY[kit][2]))
    osum = O.sum(1)
    off = 0
    for store in (s.shapes, s.big_shapes):
        for _, sh in store.items():
            c = occ(sh[1]).astype(np.float32)
            off += float(((c.sum() + osum - 2 * O @ c) / len(PTS)).min()) > 0.02
    s.kit_off = off
    quantize(s, list(range(int(oidx.max()) + 1)), O, oidx, opl)
    return s


def block_svg(key, path):
    """An isometric drawing of one block: its size in cells (dashed), the cut faces in ochre."""
    from ld_geom import _clip_polys
    nm, _, _, size, planes = BLOCK_TYPES[key]
    sz = np.array(size, float)
    unit = [((float(n[0]) * sz[0], float(n[1]) * sz[1], float(n[2]) * sz[2]), float(d)) for n, d in planes]
    c = np.array([1.1, -1.5, 1.25])
    c /= np.linalg.norm(c)
    right = np.cross(-c, [0, 0, 1.0])
    right /= np.linalg.norm(right)
    up = np.cross(right, -c)
    light = np.array([-0.35, -0.55, 1.0])
    light /= np.linalg.norm(light)
    W = H = 220
    s = 92 / max(1.0, float(np.linalg.norm(sz)) / 1.732)
    cen = sz / 2

    def P(p):
        p = np.asarray(p, float) - cen
        return W / 2 + s * (p @ right), H / 2 - s * (p @ up)

    out = [f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {W} {H}" width="{W}" height="{H}">']
    segs = []
    for i in range(int(sz[0]) + 1):
        for j in range(int(sz[1]) + 1):
            segs.append(((i, j, 0), (i, j, sz[2])))
    for z in (0, sz[2]):
        for i in range(int(sz[0]) + 1):
            segs.append(((i, 0, z), (i, sz[1], z)))
        for j in range(int(sz[1]) + 1):
            segs.append(((0, j, z), (sz[0], j, z)))
    for a, b in segs:
        (x0, y0), (x1, y1) = P(a), P(b)
        out.append(f'<line x1="{x0:.1f}" y1="{y0:.1f}" x2="{x1:.1f}" y2="{y1:.1f}" stroke="#2A3B55" stroke-dasharray="3 3"/>')
    faces = []
    for dv, poly, full in _clip_polys(unit):
        pts = np.array(poly, float) * sz
        n = np.cross(pts[1] - pts[0], pts[2] - pts[0])
        if np.linalg.norm(n) < 1e-9:
            continue
        n /= np.linalg.norm(n)
        if n @ (pts.mean(0) - cen) < 0:
            n = -n
        if n @ c <= 1e-6:
            continue
        k = 0.45 + 0.55 * max(0.0, float(n @ light))
        base = (190, 160, 100) if dv is None else (163, 164, 156)
        faces.append((float(pts.mean(0) @ c), "#%02X%02X%02X" % tuple(int(min(255, v * k)) for v in base), pts))
    for _, col, pts in sorted(faces, key=lambda q: q[0]):
        d = " ".join(f"{x:.1f},{y:.1f}" for x, y in (P(p) for p in pts))
        out.append(f'<polygon points="{d}" fill="{col}" stroke="#0A0F1A" stroke-width="1.5" stroke-linejoin="round"/>')
    out.append("</svg>")
    with open(path, "w") as fh:
        fh.write("".join(out))


def option_ship(fn, types, cache={}):
    key = tuple(types)
    if key not in cache:
        cache[key] = oriented(option_pieces(types))
    O, oidx, opl = cache[key]
    with OptionGrid(types):
        s = fn()
    s.prune()
    exact = total = 0
    for store in (s.shapes, s.big_shapes):
        for _, sh in store.items():
            c = occ(sh[1]).astype(np.float32)
            n = c.sum()
            e = min([(len(PTS) - n) / len(PTS), n / len(PTS)] + list((n + O.sum(1) - 2 * O @ c) / len(PTS)))
            exact += e <= 0.05
            total += 1
    quantize(s, list(range(int(oidx.max()) + 1)), O, oidx, opl)
    return s, exact, total


# ======================================================================================================================
# Rebuilding hulls with a block set, and rendering them
# ======================================================================================================================

# Variants: (key, label, slope limit or None for the original hull, number of block shapes kept or None for all)
VARIANTS = [("free", "Free cut cells (the look book)", None, None),
            ("spec", "Cube and wedge only (the spec today)", 1, 1),
            ("g1k", "1:1 grid, the 8 most used blocks", 1, 8),
            ("g2k", "1:2 grid, the 20 most used blocks", 2, 20),
            ("g2", "1:2 grid, every block it makes (253)", 2, None)]
CATALOGUES = [(1, 8), (2, 20)]
RENDER_SHIPS = [("halo", "HALO (ARK war line)"), ("cruiser", "REQUIEM (NOCTURNE cruiser)"), ("heavy", "CANTICLE (NOCTURNE frigate)")]


def keep_set(keys, k):
    """Oriented occupancy for the k most used shapes (canonical planes are valid plane sets)."""
    cands = [(f"#{i + 1}", 0, key) for i, key in enumerate(keys[:k])]
    return oriented(cands), list(range(len(cands)))


def quantize(ship, js, O, oidx, opl):
    """Replace every cut cell and cut big block with the closest shape from candidates js (or a cube, or nothing)."""
    jset = set(js)
    cols = [i for i in range(len(oidx)) if oidx[i] in jset]
    Os = O[cols]
    osum = Os.sum(1)
    cache = {}

    def best(key):
        if key not in cache:
            c = occ(key).astype(np.float32)
            n = c.sum()
            errs = list((n + osum - 2 * Os @ c) / len(PTS)) if cols else []
            opts = [(len(PTS) - n) / len(PTS), n / len(PTS)] + errs
            i = int(np.argmin(opts))
            cache[key] = "full" if i == 0 else "empty" if i == 1 else opl[cols[i - 2]]
        return cache[key]

    for c, sh in list(ship.shapes.items()):
        r = best(sh[1])
        if r == "full":
            ship.shapes.pop(c)
        elif r == "empty":
            ship.shapes.pop(c)
            ship.cells.pop(c, None)
        else:
            ship.shapes[c] = ("clip", r)
    for B, sh in list(ship.big_shapes.items()):
        r = best(sh[1])
        if r == "full":
            ship.big_shapes.pop(B)
        elif r == "empty":
            ship.big_shapes.pop(B)
            ship.big.pop(B, None)
        else:
            ship.big_shapes[B] = ("clip", r)
    return ship


def build_variant(fn, m, k, grids):
    if m is None:
        s = fn()
        s.prune()
        return s
    with SlopeGrid(m):
        s = fn()
    s.prune()
    if k is not None:
        (O, oidx, opl), js = grids[m]["keep"][k]
        quantize(s, js, O, oidx, opl)
    return s


def display_orient(planes):
    """The orientation that sits the block on its base with its cut faces toward the catalogue camera (+x, -y, +z)."""
    best = None
    for perm, signs in _syms():
        p = orient(planes, perm, signs)
        o = occ(p)
        c = PTS[o].mean(0)
        k = (round(c[2], 3), round(-c[1], 3), round(c[0], 3))
        if best is None or k < best[0]:
            best = (k, p)
    return best[1]


def iso_catalogue(keys, counts, total, path):
    """Isometric line drawings of block shapes (cut faces in ochre, cube faces in grey), with their share of use."""
    from ld_geom import cell_polys
    c = np.array([1.1, -1.5, 1.25])
    c /= np.linalg.norm(c)
    f = -c
    right = np.cross(f, [0, 0, 1.0])
    right /= np.linalg.norm(right)
    up = np.cross(right, f)
    light = np.array([-0.35, -0.55, 1.0])
    light /= np.linalg.norm(light)
    cols, tile = 5, 150
    rows = (len(keys) + cols - 1) // cols
    W, H = cols * tile, rows * (tile + 34)
    out = [f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {W} {H}" width="{W}" height="{H}" font-family="Inter, sans-serif">',
           f'<rect width="{W}" height="{H}" fill="#0A0F1A"/>']
    cube = [(x, y, z) for x in (0, 1) for y in (0, 1) for z in (0, 1)]
    edges = [(a, b) for a in cube for b in cube if a < b and sum(abs(a[i] - b[i]) for i in range(3)) == 1]
    for i, key in enumerate(keys):
        planes = display_orient(key)
        ox, oy = (i % cols) * tile + tile / 2, (i // cols) * (tile + 34) + tile / 2 + 6
        s = tile * 0.52

        def P(p):
            p = np.asarray(p, float) - 0.5
            return ox + s * (p @ right), oy - s * (p @ up)

        for a, b in edges:
            (x0, y0), (x1, y1) = P(a), P(b)
            out.append(f'<line x1="{x0:.1f}" y1="{y0:.1f}" x2="{x1:.1f}" y2="{y1:.1f}" stroke="#2A3B55" stroke-width="1" stroke-dasharray="3 3"/>')
        faces = []
        for dv, poly, full in cell_polys(("clip", planes)):
            pts = np.array(poly, float)
            n = np.cross(pts[1] - pts[0], pts[2] - pts[0])
            if np.linalg.norm(n) < 1e-9:
                continue
            n /= np.linalg.norm(n)
            if n @ (pts.mean(0) - 0.5) < 0:
                n = -n
            if n @ c <= 1e-6:
                continue
            k = 0.45 + 0.55 * max(0.0, float(n @ light))
            base = (168, 144, 96) if dv is None else (163, 164, 156)
            col = "#%02X%02X%02X" % tuple(int(min(255, v * k)) for v in base)
            faces.append((float(pts.mean(0) @ c), col, pts))
        for _, col, pts in sorted(faces, key=lambda q: q[0]):
            d = " ".join(f"{x:.1f},{y:.1f}" for x, y in (P(p) for p in pts))
            out.append(f'<polygon points="{d}" fill="{col}" stroke="#0A0F1A" stroke-width="1.2" stroke-linejoin="round"/>')
        share = 100 * counts[i] / total
        out.append(f'<text x="{ox:.0f}" y="{oy + tile * 0.5 + 16:.0f}" fill="#DCE8F5" font-size="13" text-anchor="middle">#{i + 1} · {share:.1f}%</text>')
    out.append("</svg>")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w") as fh:
        fh.write("".join(out))


def render(draft=False):
    import bpy
    from mathutils import Vector
    import ld_mats
    import ld_scenes
    import ld_world
    from ld_ark import ark_halo
    from ld_ships import DESIGNS, Ship
    from ld_styles import STYLES
    S = dict(STYLES["nocturne"])
    S["_key"] = "nocturne"
    fns = {"halo": ark_halo, **dict(DESIGNS["nocturne"])}
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    ld_scenes.setup_render(S, scene, draft)
    ld_world.setup_world(S, scene)
    mats = ld_mats.make_materials(S)
    ld_world.setup_lights(S, scene)
    things = ld_scenes.Things()
    cam = ld_scenes.make_camera(scene)
    os.makedirs(OUT_IMG, exist_ok=True)
    for f in os.listdir(OUT_IMG):
        if f.endswith(".jpg"):
            os.remove(os.path.join(OUT_IMG, f))
    for key in ["cube"] + sorted({b for k in KITS for b in k[2]}):
        block_svg(key, os.path.join(OUT_IMG, f"block_{key}.svg"))
    for vk in ["free"] + [k[0] for k in KITS]:
        s = demo_ship(vk)
        name = f"demo_{vk}"
        _, size = ld_scenes.build_ship(s, S, mats, things, name, plumes=False)
        L = Vector(size).length
        things.show({name})
        ld_scenes.aim(cam, ld_scenes.orbit((0, 0, 0), L * 1.7, 35, 30), (0, 1.0, -1.0), lens=50)
        ld_scenes.render(scene, os.path.join(OUT_IMG, f"{name}.jpg"))
        nose = Vector((0, size.y * 0.5 - 4.0, 1.0))
        ld_scenes.aim(cam, nose + Vector(ld_scenes.orbit((0, 0, 0), 15.0, 30, 28)), nose, lens=50)
        ld_scenes.render(scene, os.path.join(OUT_IMG, f"{name}_nose.jpg"))
        print("SLOPES rendered", name, flush=True)
        things.show(set())



if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if not args or args[0] == "stats":
        stats()
    elif args[0] == "render":
        render("--draft" in args)
    print("LOOKDEV done")
