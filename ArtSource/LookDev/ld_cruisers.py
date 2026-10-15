"""NOCTURNE heavy cruiser options designed on the game's block kit (QA-001).

Owner, 2026-10-15: "bro this requiem ship is VERY troubled, moves like a snail, its literally overlapping the station wtf??,
doesn't look good, you went to "copying" and ended up with an ugly mess, lets design a ship that looks really good, and
"borrows" slightly from the requiem instead of copying outright, and feels good ... refer to [the look book] in the slopes
sections how we figured for the wraith and other ships that we can't outright copy or go for parity with the blocks we have."

The first REQUIEM snapped the look book's free-form cruiser (built at twice the block size, ~115 m, 92k cells) onto kit k3.
These are drawn the way the WRAITH was rebuilt: on the kit's own angles from the start, so every sloped cell is one of the
game's blocks (cube, 45-degree slope, hip corner, 1x2 long slope). Rules that keep a hull clean on k3:
  - 45-degree faces may meet each other anywhere: the hip corner closes those corners;
  - a 1x2 slope only runs as a straight extrusion between flat faces (no corner block exists for it).
They borrow REQUIEM's language, not its geometry: the WRAITH-family ram and red bow band, a bone hull with an ochre nave
roof, dark iron castle and ribs, organ-pipe spires and the lightning lance. Each has one job, and its plan outline says it:
  A NAVE       line cruiser: a cathedral cross in plan; the transept arms carry the wing batteries;
  B BROADSIDE  asymmetric: a long starboard gun gallery, an offset command island to port;
  C LANCE      spinal lance: a forked bow with the lance between the tines, broad stern with outboard engine pods.
About 48-52 m long against the WRAITH's 26 m. Look-book axes: x right, y forward (bow), z up, in cells.
"""
import numpy as np

import ld_slopes
from ld_ships import Ship

KIT = ["w11", "hip", "s12"]

# 45-degree chamfer normals by edge: top/bottom long edges, vertical corners (plan), top/bottom ends
_CH = {"ct": [(1, 0, 1), (-1, 0, 1)], "cb": [(1, 0, -1), (-1, 0, -1)],
       "cf": [(1, 1, 0), (-1, 1, 0)], "cr": [(1, -1, 0), (-1, -1, 0)],
       "tf": [(0, 1, 1)], "tr": [(0, -1, 1)], "bf": [(0, 1, -1)], "br": [(0, -1, -1)]}


def cbox(s, x0, x1, y0, y1, z0, z1, tag="hull", mirror=False, **ch):
    """A box with 45-degree chamfers of the given sizes on its edges (keys of _CH)."""
    lo, hi = (x0, y0, z0), (x1, y1, z1)
    planes = [((1, 0, 0), x1), ((-1, 0, 0), -x0), ((0, 1, 0), y1), ((0, -1, 0), -y0), ((0, 0, 1), z1), ((0, 0, -1), -z0)]
    for key, c in ch.items():
        if not c:
            continue
        for n in _CH[key]:
            corner = sum(n[i] * (hi[i] if n[i] > 0 else lo[i]) for i in range(3))
            planes.append((n, corner - c))
    s.convex(planes, (lo, hi), tag, mirror=mirror)


def ram(s, y0, Wr, zt, deck_at, tip_z, tag="armor"):
    """WRAITH-family ram: vertical sides, flat top to deck_at, then a 1-in-2 deck to a tip at tip_z with a 1-in-2 chin.
    A pure extrusion across x, so it is clean on the kit. Returns the tip's y."""
    tip_y = deck_at + 2 * (zt - tip_z)
    s.convex([((1, 0, 0), Wr), ((-1, 0, 0), Wr), ((0, -1, 0), -y0), ((0, 0, -1), 0), ((0, 0, 1), zt),
              ((0, 1, 2), deck_at + 2 * zt), ((0, 1, -2), tip_y - 2 * tip_z)], ((-Wr, y0, 0), (Wr, tip_y, zt)), tag)
    return tip_y


def spire(s, cx, cy, w, z0, z1, tag="trim", mirror=False):
    """A square obelisk (2w wide) with a 45-degree pyramid cap ending at z1."""
    cbox(s, cx - w, cx + w, cy - w, cy + w, z0, z1, tag, mirror, ct=w, tf=w, tr=w)


def ribs(s, ys, x0, x1, z0, z1, tag="trim"):
    for y in ys:
        s.box(x0, x1, y, y + 1, z0, z1, tag, mirror=True)


def engines(s, y, cols, rows, z0, x0=None):
    """A bank of large thrusters on a stern face: cols x rows of 2-wide, 1-tall engines (box ends at y). With the core
    assist a ~280 t cruiser gets ~4.5 m/s² forward (WRAITH: 2.8 plus assist)."""
    s.mod("thruster_xl", -cols if x0 is None else x0, y - 3, z0, size=(2 * cols, 3, rows))


def rcs(s, x, y, z, mirror=True):
    """A manoeuvring pod (four large thrusters) on the outer face at x. The core's gyro is capped so that big ships turn
    on real thrusters off-centre (ADR-0114); pods at bow and stern give the lever."""
    s.mod("rcs", x, y, z, size=(1, 8, 2), mirror=mirror)


def finish(s):
    s.prune()
    O, oidx, opl = ld_slopes.oriented(ld_slopes.option_pieces(KIT))
    osum = O.sum(1)
    s.off_cells = []
    for cell, sh in s.shapes.items():
        c = ld_slopes.occ(sh[1]).astype(np.float32)
        if float(((c.sum() + osum - 2 * O @ c) / len(ld_slopes.PTS)).min()) > 0.02:
            s.off_cells.append(cell)
    s.kit_off = len(s.off_cells)
    ld_slopes.quantize(s, list(range(int(oidx.max()) + 1)), O, oidx, opl)
    return s


# ======================================================================================================================
def cruiser_nave():
    """A NAVE, line cruiser. A cathedral cross in plan: ram, long nave, a broad transept whose arms carry the wing
    batteries, a crossing tower, and a chamfered dark apse at the stern with the lance between organ pipes.
    Niche: holds a line and covers both flanks from the transept; counter: frigates under the apse, where only flak bears."""
    s = Ship("REQUIEM", 31, "company")
    cbox(s, -10, 10, 0, 18, 0, 14, "trim", ct=3, cb=2, cr=4)            # apse
    cbox(s, -6, 6, 18, 84, 0, 10, "hull", ct=2, cb=2)                          # nave hull
    cbox(s, -4, 4, 18, 76, 10, 13, "hull2", ct=2, tf=3)                        # ochre roof
    ribs(s, list(range(20, 44, 4)) + list(range(64, 80, 4)), 4, 7, 8, 10)      # buttress ribs
    cbox(s, -16, 16, 44, 60, 1, 9, "hull", ct=2, cb=2, cf=4, cr=4)             # transept
    for y in (47, 56):
        s.box(-15, 15, y, y + 1, 9, 10, "trim")
    cbox(s, -4, 4, 46, 58, 13, 22, "hull2", ct=4, tf=4, tr=4)                  # crossing tower
    spire(s, 0, 52, 1, 22, 27, "trim")
    cbox(s, -6, 6, 84, 88, 0, 10, "hull", ct=2, cb=2, cf=3)                    # shoulders
    ram(s, 88, 3, 10, 92, 4)
    for x, h in ((5, 9), (7, 6)):
        spire(s, x, 6, 1, 14, 14 + h, "trim", mirror=True)                      # organ pipes
    s.paint(lambda x, y, z: z < 1.5 and y >= 18, "armor")
    s.paint(lambda x, y, z: 0 <= y < 18 and 7 <= z < 8.5, "hull")             # bone band round the apse
    s.paint(lambda x, y, z: 80 <= y < 81.5 and z > 1, "accent")
    s.mod("storm", -2, 4, 14, size=(4, 4, 3))
    s.mod("heavy_cannon", 10, 50, 9, size=(4, 4, 2), mirror=True)
    s.mod("heavy_cannon", -2, 30, 13, size=(4, 4, 2))
    s.mod("heavy_cannon", -2, 66, 13, size=(4, 4, 2))
    s.mod("railgun", 16, 50, 3, size=(1, 4, 2), mirror=True)
    s.mod("flak", 10, 10, 6, size=(1, 3, 3), mirror=True)
    engines(s, 0, 6, 4, 2)
    rcs(s, 10, 8, 2)
    rcs(s, 6, 74, 3)
    return finish(s)


def cruiser_broadside():
    """B BROADSIDE: asymmetric with a reason. A long starboard gallery carries four turrets that all bear to starboard;
    the command island and its spires sit offset to port. Niche: lays its gallery against the enemy line; counter: come in
    from port, where one railgun and a flak mount bear."""
    s = Ship("REQUIEM-B", 32, "company")
    cbox(s, -9, 9, 0, 16, 0, 13, "trim", ct=3, cb=2, cr=3)
    cbox(s, -7, 7, 16, 84, 0, 10, "hull", ct=2, cb=2)
    cbox(s, -6, 6, 84, 88, 0, 10, "hull", ct=2, cb=2, cf=3)
    ram(s, 88, 3, 10, 92, 4)
    # starboard gallery: a low deck stepping out from the hull, 45-degree ends and outer top edge
    s.convex([((1, 0, 0), 17), ((-1, 0, 0), -6), ((0, 1, 0), 74), ((0, -1, 0), -18), ((0, 0, 1), 9), ((0, 0, -1), -2),
              ((1, 1, 0), 17 + 74 - 6), ((1, -1, 0), 17 - 18 - 6), ((1, 0, 1), 17 + 9 - 3)], ((6, 18, 2), (17, 74, 9)), "hull2")
    for y in range(22, 72, 4):
        s.box(15, 16, y, y + 1, 2, 6, "trim")
    # port command island: chamfered, sloped fore and aft, two spires
    cbox(s, -8, -1, 34, 58, 10, 18, "hull2", ct=3, tf=4, tr=4)
    spire(s, -5, 40, 1, 18, 30, "trim")
    spire(s, -5, 46, 1, 18, 25, "trim")
    ribs(s, range(62, 82, 4), 4, 7, 8, 10)
    s.paint(lambda x, y, z: z < 1.5 and y >= 16, "armor")
    s.paint(lambda x, y, z: 0 <= y < 16 and 7 <= z < 8.5, "hull")             # bone band round the castle
    s.paint(lambda x, y, z: 80 <= y < 81.5 and z > 1, "accent")
    s.mod("storm", -2, 4, 13, size=(4, 4, 3))
    for y in (24, 36, 48, 60):
        s.mod("heavy_cannon", 8, y, 9, size=(4, 4, 2))
    s.mod("railgun", 17, 30, 3, size=(1, 4, 2))
    s.mod("railgun", 17, 54, 3, size=(1, 4, 2))
    s.mod("railgun", -8, 48, 3, size=(1, 4, 2))
    s.mod("flak", -8, 26, 4, size=(1, 3, 3))
    engines(s, 0, 6, 4, 2)
    rcs(s, 9, 8, 2)
    rcs(s, 7, 74, 3)
    return finish(s)


def cruiser_lance():
    """C LANCE: spinal lance cruiser. The bow forks into two armoured tines with the lightning lance mounted between them;
    a narrow waist; a broad dark stern with outboard engine pods and an organ of four spires. Niche: one devastating
    forward shot down the fork; counter: anything on its flanks, which must wait for the whole hull to turn."""
    s = Ship("REQUIEM-C", 33, "company")
    cbox(s, -12, 12, 0, 20, 0, 15, "trim", ct=3, cb=2, cr=3)            # stern
    cbox(s, 12, 18, -2, 14, 3, 10, "armor", mirror=True, ct=2, cb=2, cf=3)     # engine pods
    cbox(s, -7, 7, 20, 52, 1, 11, "hull", ct=2, cb=2)                          # waist
    cbox(s, -5, 5, 20, 50, 11, 14, "hull2", ct=2, tf=3)                        # nave roof
    cbox(s, -11, 11, 46, 58, 0, 10, "hull", ct=2, cb=2, cr=4)                  # yoke where the tines join
    # tines: 45 degrees everywhere (a plan point on the outer side, chamfered outer edges), straight inner faces
    s.convex([((1, 0, 0), 11), ((-1, 0, 0), -4), ((0, -1, 0), -58), ((0, 0, -1), 0), ((0, 0, 1), 9),
              ((1, 1, 0), 11 + 98 - 7), ((1, 0, 1), 11 + 9 - 2)], ((4, 58, 0), (11, 98, 9)), "hull", mirror=True)
    ribs(s, range(22, 46, 4), 5, 8, 9, 11)
    for x, h in ((3, 10), (7, 14)):
        spire(s, x, 8, 1, 15, 15 + h, "trim", mirror=True)
    s.paint(lambda x, y, z: z < 2.5 and y >= 20, "armor")
    s.paint(lambda x, y, z: 0 <= y < 20 and 7 <= z < 8.5, "hull")             # bone band round the stern
    s.paint(lambda x, y, z: 56 <= y < 57.5 and z > 1, "accent")
    s.mod("storm", -2, 58, 2, size=(4, 6, 5))
    s.mod("heavy_cannon", -2, 34, 14, size=(4, 4, 2))
    s.mod("heavy_cannon", 6, 74, 9, size=(4, 4, 2), mirror=True)
    s.mod("railgun", 11, 64, 2, size=(1, 4, 2), mirror=True)
    s.mod("flak", -2, 14, 15, size=(4, 3, 2))
    engines(s, 0, 6, 3, 3)
    engines(s, -2, 2, 3, 4, x0=13)
    engines(s, -2, 2, 3, 4, x0=-17)
    rcs(s, 18, 4, 4)
    rcs(s, 11, 80, 2)
    return finish(s)


OPTIONS = [("ship_nocturne_cruiser_a", cruiser_nave), ("ship_nocturne_cruiser_b", cruiser_broadside),
           ("ship_nocturne_cruiser_c", cruiser_lance)]
