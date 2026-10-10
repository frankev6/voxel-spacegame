"""Stations per direction (voxel cells at 0.5 m plus functional blocks and custom parts)."""

import math
from mathutils import Matrix, Vector

from ld_geom import CELL, box, cyl, lathe, sphere, torus, ico, hash01
from ld_ships import Ship, CONTAINER_TINTS


def drydock_station():
    """PORT KESSLER: a busy trade hub. A central spindle with a traffic-control crown, four long docking piers with
    berth heads, gantries and container yards, guide lights, ships docked along the piers. Defences from the same
    blocks: heavy cannons and flak on the hub ledge, a graviton snare on top for traffic control, a triple battery and
    flak on every berth head, autocannons along the piers."""
    s = Ship("PORT KESSLER", 101, "yard", unit=3)
    # the spindle: a wide lower drum, a ledge at z = 24, a narrower control tower, a crown
    s.tower(0, 0, [(-60, 12, 4), (-42, 24, 8), (-12, 30, 10), (24, 30, 10)], "hull", big=True)
    s.tower(0, 0, [(24, 20, 6), (66, 20, 6), (78, 12, 4), (84, 12, 4)], "hull2", big=True)
    s.tower(0, 0, [(84, 16, 5), (90, 16, 5)], "armor", big=True)
    s.spire(0, 0, 4, -96, -60, 1.6, "armor", down=True)
    s.paint(lambda x, y, z: -12 <= z < -6, "hull2")
    s.paint(lambda x, y, z: 18 <= z < 24 and max(abs(x), abs(y)) > 24, "accent")
    # four docking piers and their berth heads
    R0, R1 = 24, 186
    sec = (8, -6, 6, 3, 3)
    head = [(0, 22, -9, 9, 4, 4), (18, 22, -9, 9, 4, 4), (24, 16, -6, 6, 3, 3)]
    for axis in ("x", "y"):
        s.loft([(R0, *sec), (R1, *sec)], "hull", big=True, axis=axis)
        s.loft([(-R1, *sec), (-R0, *sec)], "hull", big=True, axis=axis)
        s.loft([(R1 + d, *rest) for d, *rest in head], "hull2", big=True, axis=axis)
        s.loft([(-R1 - d, *rest) for d, *rest in reversed(head)], "hull2", big=True, axis=axis)
    s.paint(lambda x, y, z: z < -3 and max(abs(x), abs(y)) > 30, "armor")
    # yellow gantries straddling the piers, every 40 cells
    for r in (60, 100, 140):
        for sgn in (1, -1):
            y0 = sgn * r if sgn > 0 else sgn * r - 2
            s.box(-10, -8, y0, y0 + 2, -4, 22, "accent")
            s.box(8, 10, y0, y0 + 2, -4, 22, "accent")
            s.box(-10, 10, y0, y0 + 2, 20, 22, "accent")
            s.box(y0, y0 + 2, -10, -8, -4, 22, "accent")
            s.box(y0, y0 + 2, 8, 10, -4, 22, "accent")
            s.box(y0, y0 + 2, -10, 10, 20, 22, "accent")
    # defences: hub ledge, crown, berth heads, piers
    s.mod("heavy_cannon", 22, -2, 24, rot=(0, 0, -90))
    s.mod("heavy_cannon", -28, -2, 24, rot=(0, 0, 90))
    s.mod("heavy_cannon", -2, 22, 24)
    s.mod("heavy_cannon", -2, -28, 24, rot=(0, 0, 180))
    for x, y in ((18, 18), (-21, 18), (18, -21), (-21, -21)):
        s.mod("flak", x, y, 24)
    s.mod("graviton", -7, -6, 90)
    s.mod("sensor", 8, -1, 84)
    s.mod("sensor", -10, -1, 84)
    s.mod("battery", -6, R1 + 6, 9)
    s.mod("battery", -6, -R1 - 24, 9, rot=(0, 0, 180))
    s.mod("battery", R1 + 6, -6, 9, rot=(0, 0, -90))
    s.mod("battery", -R1 - 24, -6, 9, rot=(0, 0, 90))
    for sgn in (1, -1):
        e = R1 + 9 if sgn > 0 else -R1 - 12
        s.mod("flak", 14, e, 9)
        s.mod("flak", -17, e, 9)
        s.mod("flak", e, 14, 9)
        s.mod("flak", e, -17, 9)
        for r in (44, 100, 166):
            p = sgn * r if sgn > 0 else sgn * r - 2
            s.mod("autocannon", 3, p, 6)
            s.mod("autocannon", p, -5, 6)
    # container yards on the piers
    k = 0
    for sgn in (1, -1):
        for r0, r1 in ((64, 96), (104, 136)):
            for r in range(r0, r1, 4):
                for col, c in enumerate((-6, -4, 2, 4)):
                    k += 1
                    h = 1 + int(hash01(r, col, sgn, 3) < 0.5)
                    tint = CONTAINER_TINTS[int(hash01(r, col, sgn, 5) * len(CONTAINER_TINTS))]
                    rm = {"c_cargo": tint} if tint else None
                    p = r if sgn > 0 else -r - 4
                    for kk in range(h):
                        s.mod("cargo", c, p, 6 + kk * 2, remap=rm)
                    if hash01(r, col, sgn, 8) < 0.7:
                        for kk in range(h):
                            s.mod("cargo", p, c, 6 + kk * 2, rot=(0, 0, 90), remap=rm)
    # docking clamps reaching out from the piers to the berthed ships
    s.berths = [("heavy", (105, 25, 0), -90), ("cruiser", (-38, -160, -2), 180), ("light", (34, 198, 0), 0),
                ("light", (-198, -34, 0), 90)]
    for x in (80, 110, 140):
        s.box(x, x + 2, 8, 12, -2, 2, "trim")
    for y in (-60, -100, -140):
        s.box(-12, -8, y, y + 2, -2, 2, "trim")

    def lights(acc, group, S):
        # guide-light rows along every pier edge, beacons on the berth heads, flood lamps on the gantries
        for axis in ("x", "y"):
            for sgn in (1, -1):
                for r in range(28, R1, 6):
                    for side in (-8.6, 8.6):
                        q = (sgn * (r + 0.5), side, 6.1) if axis == "x" else (side, sgn * (r + 0.5), 6.1)
                        acc.add(group, "nav_g" if (r // 6) % 2 else "window", sphere(tuple(Vector(q) * CELL), 0.09, 8, 4), None, True)
                e = sgn * (R1 + 24.6)
                for off in (-15, 15):
                    q = (e, off, 7) if axis == "x" else (off, e, 7)
                    acc.add(group, "nav_r", sphere(tuple(Vector(q) * CELL), 0.22, 10, 5), None, True)
        for r in (60, 100, 140):
            for sgn in (1, -1):
                for x in (-9, 9):
                    p = Vector((x, sgn * r + 1, 20)) * CELL
                    acc.add(group, "window", box(tuple(p - Vector((0.3, 0.3, 0.3))), tuple(p + Vector((0.3, 0.3, 0.0)))), None, False)
        p = Vector((0, 0, 92)) * CELL
        acc.add(group, "metal", cyl(tuple(p), tuple(p + Vector((0, 0, 16))), 0.12, 8), None, True)
        acc.add(group, "nav_w", sphere(tuple(p + Vector((0, 0, 16.2))), 0.3, 12, 6), None, True)
    s.extra.append(lights)
    s.work_lights = [((x + 0.5) * CELL, (sgn * r + 1) * CELL, 19 * CELL) for r in (60, 140) for sgn in (1, -1) for x in (-9, 9)]
    return s


def nocturne_station():
    """THE ROOD: a refinery towed between stars. A long deck with a cathedral of spires above and an inverted one
    hanging below, flying buttresses, sodium lamps."""
    s = Ship("THE ROOD", 102, "company")
    s.greeble_override = {"window": 0.004, "rib": 0.25, "pipe": 0.25, "vent": 0.1, "strobe": 0.02}
    s.loft([(-72, 18, 2, 8, 3, 3), (-62, 26, 0, 10, 3, 3), (58, 26, 0, 10, 3, 3), (70, 18, 2, 8, 3, 3)], "hull")
    s.paint(lambda x, y, z: z < 3, "armor")
    ys = list(range(-56, 56, 12))
    for i, y in enumerate(ys):
        for sx in (-1, 1):
            hgt = 22 + int(18 * hash01(i, sx, 1))
            s.spire(sx * 15, y, 3, 10, 10 + hgt, 1.7, "hull")
            hb = 20 + int(26 * hash01(i, sx, 2))
            s.spire(sx * 13, y + 5, 3, -hb, 0, 1.5, "armor", down=True)
        top = 46 + int(26 * math.sin(math.pi * (i + 0.5) / len(ys)))
        s.spire(0, y + 6, 4, 10, 10 + top, 1.5, "hull2")
        bot = 40 + int(30 * math.sin(math.pi * (i + 0.5) / len(ys)))
        s.spire(0, y, 4, -bot, 0, 1.4, "hull", down=True)
    # flying buttresses between the spire rows
    for i, y in enumerate(ys[:-1]):
        s.box(-13, 13, y + 5, y + 7, 26, 28, "trim")
        s.box(-12, 12, y - 1, y + 1, -18, -16, "trim")
    # tow yoke at the stern
    s.loft([(-86, 3, 3, 7, 1, 1), (-72, 3, 3, 7, 1, 1)], "trim", cx=12, mirror=True)
    s.box(-15, 15, -88, -84, 3, 7, "trim")
    for y in (-30, 0, 30):
        s.mod("radiator", 22, y, 10, mirror=True)
    s.mod("tesla", -1, -66, 8)
    # defences: heavy cannons between the spire rows, flak at the deck corners, turrets hanging under the deck
    for y in (-48, -24, 0, 24, 46):
        s.mod("heavy_cannon", 5, y, 10, mirror=True)
    for y in (-60, -14, 34):
        s.mod("flak", 19, y, 10, mirror=True)
    for y in (-50, -10, 30):
        s.mod("flak", 19, y, -3, rot=(0, 180, 0), mirror=True)

    def lamps(acc, group, S):
        for y in range(-60, 60, 10):
            for sx in (-1, 1):
                p = Vector((sx * 25.5, y + 0.5, 10.3)) * CELL
                acc.add(group, "metal", cyl(tuple(p), tuple(p + Vector((0, 0, 0.6))), 0.06, 6), None, True)
                acc.add(group, "window", sphere(tuple(p + Vector((0, 0, 0.7))), 0.18, 10, 5), None, True)
    s.extra.append(lamps)
    s.work_lights = [(sx * 22 * CELL, y * CELL, 18 * CELL) for y in (-40, -10, 20, 50) for sx in (-1, 1)]
    return s


def opera_station():
    """THE CROWN: a double wheel on a common axle (2001), geodesic hub, gold-banded spokes, ring windows."""
    s = Ship("THE CROWN", 103, "crown")
    for zc, tags in ((12, ("hull", "hull", "hull2", "hull")), (-12, ("hull", "hull2", "hull", "hull"))):
        s.ring((0, 0, zc), "z", 50, 58, 3, 48, tags)
        for ax in ((1, 0), (0, 1), (-1, 0), (0, -1)):
            x0, x1 = sorted((ax[0] * 2, ax[0] * 51)) if ax[0] else (-2, 2)
            y0, y1 = sorted((ax[1] * 2, ax[1] * 51)) if ax[1] else (-2, 2)
            s.box(x0, x1, y0, y1, zc - 2, zc + 2, "trim")
    s.ball((0, 0, 0), 15, "hull")
    oc = [((1, 0, 0), 5), ((-1, 0, 0), 5), ((0, 1, 0), 5), ((0, -1, 0), 5), ((1, 1, 0), 7), ((1, -1, 0), 7), ((-1, 1, 0), 7),
          ((-1, -1, 0), 7), ((0, 0, 1), 44), ((0, 0, -1), 44)]
    s.convex(oc, ((-5, -5, -44), (5, 5, 44)), "hull2")
    s.spire(0, 0, 3, 44, 58, 2.0, "accent")
    s.spire(0, 0, 3, -58, -44, 2.0, "accent", down=True)
    s.mod("sensor", 3, -1, 44)
    # defences: flak on the upper rim and under the lower rim, heavy cannons on the spokes
    for x, y in ((52, -1), (-55, -2), (-2, 52), (-1, -55)):
        s.mod("flak", x, y, 15)
        s.mod("flak", x, y, -18, rot=(0, 180, 0))
    s.mod("heavy_cannon", 28, -2, 14, rot=(0, 0, -90))
    s.mod("heavy_cannon", -34, -2, 14, rot=(0, 0, 90))
    s.mod("heavy_cannon", -2, 28, 14)
    s.mod("heavy_cannon", -2, -34, 14, rot=(0, 0, 180))

    def windows(acc, group, S):
        for zc in (12, -12):
            n = 192
            for i in range(n):
                if i % 8 in (0, 1):
                    continue
                a = 2 * math.pi * (i + 0.5) / n
                r = 58.05 * CELL * math.cos(math.pi / 48 * 0.0)
                p = Vector((math.cos(a) * r, math.sin(a) * r, zc * CELL))
                t = Vector((-math.sin(a), math.cos(a), 0)) * 0.35
                nrm = Vector((math.cos(a), math.sin(a), 0))
                v = [tuple(p - t + Vector((0, 0, -0.35))), tuple(p + t + Vector((0, 0, -0.35))), tuple(p + t + Vector((0, 0, 0.35))),
                     tuple(p - t + Vector((0, 0, 0.35)))]
                v = [tuple(Vector(q) + nrm * 0.12) for q in v]
                acc.add(group, "window", (v, [(0, 1, 2, 3)]), None, False)
            acc.add(group, "gold", torus((0, 0, (zc + 3.05) * CELL), (0, 0, 1), 54 * CELL, 0.12, 192, 6), None, True)
            acc.add(group, "gold", torus((0, 0, (zc - 3.05) * CELL), (0, 0, 1), 54 * CELL, 0.12, 192, 6), None, True)
        acc.add(group, "emit_c", sphere((0, 0, 0), 2.0, 24, 12), None, True)
    s.extra.append(windows)
    return s


STATIONS = {"drydock": drydock_station, "nocturne": nocturne_station, "opera": opera_station}
