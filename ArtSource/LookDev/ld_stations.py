"""Stations per direction (voxel cells at 0.5 m plus functional blocks and custom parts)."""

import math
from mathutils import Matrix, Vector

from ld_geom import CELL, box, cyl, lathe, sphere, torus, ico, hash01
from ld_ships import Ship


def drydock_station():
    """KESSLER YARD: a dry dock. Safety-yellow portal frames around a frigate under construction, control tower,
    crane gantry, work lights."""
    s = Ship("KESSLER YARD", 101, "yard")
    X0, X1, Z0, Z1 = -40, 40, -14, 44
    T = 3
    frames = list(range(0, 133, 22))
    for y in frames:
        for cx in (X0 + 1.5, X1 - 1.5):
            s.loft([(y, 1.5, Z0, Z1, 1, 1), (y + T, 1.5, Z0, Z1, 1, 1)], "accent", cx=cx)
        s.loft([(y, X1, Z1 - T, Z1, 1, 1), (y + T, X1, Z1 - T, Z1, 1, 1)], "accent")
        s.loft([(y, X1, Z0, Z0 + T, 1, 1), (y + T, X1, Z0, Z0 + T, 1, 1)], "accent")
    # hazard bands at the feet of every upright
    s.paint(lambda x, y, z: z < Z0 + 9 and abs(x) > X1 - T - 0.5 and ((int(y) + int(z)) // 2) % 2 == 0, "armor")
    for cx in (X0 + 1.5, X1 - 1.5):
        for zc in (Z0 + 1.5, Z1 - 1.5, 14.5):
            s.loft([(0, 1.5, zc - 1.5, zc + 1.5, 1, 1), (frames[-1] + T, 1.5, zc - 1.5, zc + 1.5, 1, 1)], "trim", cx=cx)
    # control tower on the starboard side, with a narrower crown and a comms spire
    s.loft([(34, 11, -10, 28, 3, 2), (98, 11, -10, 28, 3, 2)], "hull", cx=X1 + 11)
    s.loft([(48, 7, 28, 36, 2), (84, 7, 28, 36, 2)], "hull2", cx=X1 + 11)
    s.spire(X1 + 11, 66, 1.5, 36, 62, 3.0, "armor")
    for y in (44, 66, 88):
        s.box(X1 - 1, X1 + 1, y, y + 3, 6, 10, "trim")
    s.paint(lambda x, y, z: x > X1 and z < -4, "hull2")
    # crane gantry riding on the top rails
    s.loft([(70, X1 + 2, Z1, Z1 + 4, 1, 1), (75, X1 + 2, Z1, Z1 + 4, 1, 1)], "accent")
    s.loft([(68, 3, Z1 - 8, Z1, 1), (77, 3, Z1 - 8, Z1, 1)], "armor", cx=-6)
    # the frigate under construction: lower hull plated, upper hull still bare ribs
    from ld_ships import drydock_heavy
    h = drydock_heavy()
    dy, dz = 22, -6
    for c, tag in h.cells.items():
        x, y, z = c
        cc = (x, y + dy, z + dz)
        if z < 6 or y < 24 or y > 86:
            s.cells[cc] = tag
            if c in h.shapes:
                s.shapes[cc] = h.shapes[c]
        elif y % 6 == 0 and any((x + ex, y, z + ez) not in h.cells for ex, ez in ((1, 0), (-1, 0), (0, 1))):
            s.cells[cc] = "trim"
    for kind, mn, Rm, size, rsz, remap in h.mods:
        if kind in ("thruster_l", "reactor", "flak", "heavy_cannon", "cockpit", "radiator", "tesla", "sensor"):
            nm = (mn[0], mn[1] + dy, mn[2] + dz)
            s.mods.append((kind, nm, Rm, size, rsz, remap))
            for ddx in range(rsz[0]):
                for ddy in range(rsz[1]):
                    for ddz in range(rsz[2]):
                        s.mod_cells.add((nm[0] + ddx, nm[1] + ddy, nm[2] + ddz))
    s.mod("sensor", X1 + 10, 60, 36)
    s.mod("shield", X1 + 14, 76, 36)
    for y in (38, 58, 78):
        s.mod("radiator", X1 + 22, y, 28)
    s.mod("cargo", X1 + 4, 40, 28, remap={"c_cargo": "paint_p"})
    s.mod("cargo", X1 + 4, 86, 28)

    def lights(acc, group, S):
        # work-light housings under the top beams (the light itself is a scene light added by the shot)
        for y in frames:
            for x in (X0 + T + 1, X1 - T - 2):
                p = Vector((x + 0.5, y + 1.5, Z1 - T - 0.6)) * CELL
                acc.add(group, "metal", box(tuple(p - Vector((0.45, 0.45, 0.25))), tuple(p + Vector((0.45, 0.45, 0.3)))), None, False)
                acc.add(group, "window", box(tuple(p - Vector((0.36, 0.36, 0.27))), tuple(p + Vector((0.36, 0.36, -0.22)))), None, False)
        # crane hook cable
        p = Vector((-6, 72.5, Z1 - 8)) * CELL
        acc.add(group, "metal", cyl(tuple(p), tuple(p - Vector((0, 0, 9))), 0.05, 6), None, True)
        acc.add(group, "accent", box(tuple(p - Vector((0.5, 0.5, 10))), tuple(p - Vector((-0.5, -0.5, 9)))), None, False)
    s.extra.append(lights)
    s.work_lights = [((x + 0.5) * CELL, (y + 1.5) * CELL, (Z1 - T - 1.2) * CELL) for y in frames for x in (X0 + T + 1, X1 - T - 2)]
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
        s.mod("radiator", 25, y, 10, mirror=True)
    s.mod("tesla", -1, -66, 8)

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
            x0, x1 = sorted((ax[0] * 13, ax[0] * 51)) if ax[0] else (-2, 2)
            y0, y1 = sorted((ax[1] * 13, ax[1] * 51)) if ax[1] else (-2, 2)
            s.box(x0, x1, y0, y1, zc - 2, zc + 2, "trim")
    s.ball((0, 0, 0), 15, "hull")
    oc = [((1, 0, 0), 5), ((-1, 0, 0), 5), ((0, 1, 0), 5), ((0, -1, 0), 5), ((1, 1, 0), 7), ((1, -1, 0), 7), ((-1, 1, 0), 7),
          ((-1, -1, 0), 7), ((0, 0, 1), 44), ((0, 0, -1), 44)]
    s.convex(oc, ((-5, -5, -44), (5, 5, 44)), "hull2")
    s.spire(0, 0, 3, 44, 58, 2.0, "accent")
    s.spire(0, 0, 3, -58, -44, 2.0, "accent", down=True)
    s.mod("sensor", -1, -1, 58)

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
