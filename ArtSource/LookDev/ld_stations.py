"""Stations per direction (voxel cells at 0.5 m plus functional blocks and custom parts)."""

import math
from mathutils import Matrix, Vector

from ld_geom import CELL, box, cyl, lathe, sphere, torus, ico
from ld_ships import Ship


def hollow_box(s, x0, x1, y0, y1, z0, z1, tag="hull", t=2):
    s.box(x0, x1, y0, y1, z0, z1, tag)
    s.clear(x0 + t, x1 - t, y0 + t, y1 - t, z0 + t, z1 - t)


def drydock_station():
    s = Ship("KESSLER YARD", 101)
    X0, X1, Y0, Y1, Z0, Z1 = -40, 40, 0, 130, -12, 44
    T = 3
    # portal frames every 26 cells along the dock
    for y in range(Y0, Y1 + 1, 26):
        s.box(X0, X0 + T, y, y + T, Z0, Z1, "accent")
        s.box(X1 - T, X1, y, y + T, Z0, Z1, "accent")
        s.box(X0, X1, y, y + T, Z1 - T, Z1, "accent")
        s.box(X0, X1, y, y + T, Z0, Z0 + T, "accent")
    # longitudinal beams
    for (x, z) in ((X0, Z0), (X1 - T, Z0), (X0, Z1 - T), (X1 - T, Z1 - T), (X0, 14), (X1 - T, 14)):
        s.box(x, x + T, Y0, Y1 + T, z, z + T, "hull2")
    # hazard paint on frame ends
    s.paint(lambda x, y, z: y < 3 and (z > Z1 - 4), "accent")
    # control / habitat block on the starboard side
    hollow_box(s, X1, X1 + 26, 30, 100, -6, 26, "hull")
    hollow_box(s, X1 + 4, X1 + 22, 46, 84, 26, 36, "hull")
    s.paint(lambda x, y, z: x > X1 and z < 0, "hull2")
    # crane gantry on top rails
    s.box(X0, X1, 70, 74, Z1, Z1 + 4, "accent")
    s.box(-4, 4, 66, 78, Z1 - 10, Z1, "armor")
    # the hull under construction: lower half plated, upper half skeleton
    hb = Ship("HULL", 7)
    from ld_ships import drydock_heavy
    h = drydock_heavy()
    for c, tag in h.cells.items():
        x, y, z = c
        cc = (x, y + 22, z - 4)
        if z < 5 or (y < 30 and z < 9):
            s.cells[cc] = tag
        elif x in (-6, 5) or z in (9,) or y % 8 == 0:
            if (x + y + z) % 2 == 0 or z == 9:
                s.cells[cc] = "trim"
    for kind, mn, Rm, size, rsz in h.mods:
        if kind in ("thruster_l", "reactor", "flak"):
            s.mods.append((kind, (mn[0], mn[1] + 22, mn[2] - 4), Rm, size, rsz))
            for dx in range(rsz[0]):
                for dy in range(rsz[1]):
                    for dz in range(rsz[2]):
                        s.mod_cells.add((mn[0] + dx, mn[1] + 22 + dy, mn[2] - 4 + dz))
    s.mod("sensor", X1 + 10, 60, 36)
    s.mod("shield", X1 + 4, 90, 26)
    s.mod("radiator", X1 + 24, 40, 26, rot=(0, 0, 0))
    s.mod("radiator", X1 + 24, 60, 26)

    def lights(acc, group, S):
        # work-light housings along the frame (the light itself is a scene light added by the shot)
        for y in range(8, 130, 26):
            for x in (X0 + T, X1 - T - 1):
                p = Vector((x + 0.5, y + 1.5, Z1 - T - 0.5)) * CELL
                acc.add(group, "metal", box(tuple(p - Vector((0.4, 0.4, 0.3))), tuple(p + Vector((0.4, 0.4, 0.3)))), None, False)
                acc.add(group, "nav_w", box(tuple(p - Vector((0.3, 0.3, 0.32))), tuple(p + Vector((0.3, 0.3, -0.28)))), None, False)
    s.extra.append(lights)
    s.work_lights = [((x + 0.5) * CELL, (y + 1.5) * CELL, (Z1 - T - 1) * CELL) for y in range(8, 130, 26) for x in (X0 + T, X1 - T - 1)]
    return s


def nocturne_station():
    s = Ship("LANTERN SPIRE", 102)
    # central spire along Z
    s.cyl_z(0, 0, 9, -90, 0, "hull", r1=11, shell=2.5)
    s.cyl_z(0, 0, 11, 0, 40, "hull", r1=9, shell=2.5)
    s.cyl_z(0, 0, 9, 40, 150, "hull", r1=2, shell=2.5)
    s.cyl_z(0, 0, 9, -150, -90, "hull", r1=1)
    s.paint(lambda x, y, z: (int(z) // 12) % 3 == 0, "hull2")
    # rings
    s.torus_z(0, 0, 18, 56, 4.5, "hull", shell=2)
    s.torus_z(0, 0, -30, 40, 3.5, "armor", shell=2)
    # spokes to the rings
    for a in range(0, 360, 60):
        r = math.radians(a)
        s.beam_line((math.cos(r) * 9, math.sin(r) * 9, 18), (math.cos(r) * 52, math.sin(r) * 52, 18), 3, "trim")
    for a in range(30, 360, 120):
        r = math.radians(a)
        s.beam_line((math.cos(r) * 9, math.sin(r) * 9, -30), (math.cos(r) * 37, math.sin(r) * 37, -30), 2, "trim")
    # docking fins
    for a in range(0, 360, 90):
        r = math.radians(a + 45)
        d = Vector((math.cos(r), math.sin(r), 0))
        for i in range(10, 30):
            for z in range(60, 60 + int(30 - i * 0.8)):
                c = d * i
                s.set((int(round(c.x)), int(round(c.y)), z), "hull2")

    def glow(acc, group, S):
        for z in (-60, -20, 30, 80, 120):
            rr = 10.5 if z < 40 else max(2.5, 9 - (z - 40) / 110 * 7) + 0.2
            acc.add(group, "emit_s", torus((0, 0, z * CELL), (0, 0, 1), rr * CELL, 0.06, 64, 6), None, True)
        acc.add(group, "emit_c", sphere((0, 0, 152 * CELL), 0.8, 16, 8), None, True)
        acc.add(group, "emit_s", torus((0, 0, 18 * CELL), (0, 0, 1), 60.6 * CELL, 0.08, 128, 6), None, True)
    s.extra.append(glow)
    return s


def pulp_station():
    s = Ship("BIG WHEEL", 103)
    R = 78
    s.torus_z(0, 0, 0, R, 9, "hull", shell=2)
    s.paint(lambda x, y, z: (int((math.degrees(math.atan2(y, x)) + 360) // 30)) % 2 == 0 and abs(z) < 5, "armor")
    s.paint(lambda x, y, z: abs(z) >= 5, "hull2")
    s.ellipsoid(0, 0, 0, 20, 20, 16, "hull", shell=2.5)
    s.paint(lambda x, y, z: abs(z) < 3 and math.hypot(x, y) < 22, "accent")
    for a in range(0, 360, 90):
        r = math.radians(a + 45)
        s.beam_line((math.cos(r) * 18, math.sin(r) * 18, 0), (math.cos(r) * (R - 7), math.sin(r) * (R - 7), 0), 5, "hull2")
    s.cyl_z(0, 0, 3, 14, 60, "trim", r1=1)
    s.cyl_z(0, 0, 6, -40, -14, "hull2", r1=10)

    def bits(acc, group, S):
        acc.add(group, "emit_p", sphere((0, 0, 61 * CELL), 1.1, 16, 8), None, True)
        for k in range(3):
            acc.add(group, "armor", torus((0, 0, (30 + k * 9) * CELL), (0, 0, 1), (2.6 - k * 0.5) * CELL * 3, 0.25, 24, 8), None, True)
        for a in range(0, 360, 15):
            r = math.radians(a)
            p = Vector((math.cos(r) * (R + 9.2), math.sin(r) * (R + 9.2), 0)) * CELL
            acc.add(group, "window", sphere(tuple(p), 0.6, 12, 6, (1, 1, 1)), None, True)
    s.extra.append(bits)
    return s


def orbital_station():
    s = Ship("GATEWAY", 104)
    # main truss along X
    for (y, z) in ((-3, -3), (2, -3), (-3, 2), (2, 2)):
        s.box(-180, 180, y, y + 1, z, z + 1, "trim")
    for x in range(-180, 180, 6):
        s.box(x, x + 1, -3, 3, -3, -2, "trim")
        s.box(x, x + 1, -3, 3, 2, 3, "trim")
        s.box(x, x + 1, -3, -2, -3, 3, "trim")
        s.box(x, x + 1, 2, 3, -3, 3, "trim")
    # pressurised modules in a cross at the centre
    s.cyl_y(0, 0, 8.2, -60, 60, "hull", shell=2)
    s.cyl_y(0, 0, 8.2, 60, 70, "hull", r1=4, shell=2)
    s.cyl_z(0, 30, 7.2, 4, 40, "hull", shell=2)
    s.cyl_z(0, -30, 7.2, -40, -4, "hull", shell=2)
    s.ellipsoid(0, 0, 0, 12, 12, 12, "hull", shell=2)
    s.cyl_y(28, 0, 6.2, -20, 20, "hull2", shell=2)
    s.cyl_y(-28, 0, 6.2, -20, 20, "hull2", shell=2)
    s.mod("cockpit", -2, 70, -2)
    s.mod("sensor", -1, 30, 40)
    s.mod("sensor", 2, -30, -43, rot=(180, 0, 0))

    def arrays(acc, group, S):
        for sx in (-1, 1):
            for k in range(2):
                x0 = sx * (110 + k * 40) * CELL
                for sz in (-1, 1):
                    c = Vector((x0, 0, sz * 30 * CELL))
                    acc.add(group, "solar", box(tuple(c - Vector((8.5, 0.05, 13))), tuple(c + Vector((8.5, 0.05, 13)))), None, False)
                    acc.add(group, "metal", cyl(tuple(c - Vector((0, 0, sz * 13))), tuple(c - Vector((0, 0, sz * 28 * CELL - sz * 13 + sz * 13))), 0.15, 8), None, True)
                acc.add(group, "trim", box((x0 - 0.4, -0.4, -16.5), (x0 + 0.4, 0.4, 16.5)), None, False)
        for sx in (-1, 1):
            for k in range(3):
                c = Vector((sx * (40 + k * 14) * CELL, 0, 0))
                acc.add(group, "hull", box(tuple(c + Vector((-2.8, -0.03, 2.2))), tuple(c + Vector((2.8, 0.03, 12)))), None, False)
                acc.add(group, "hull", box(tuple(c + Vector((-2.8, -0.03, -12))), tuple(c + Vector((2.8, 0.03, -2.2)))), None, False)
        for y in (-60, 72):
            p = Vector((0, y * CELL, 0))
            acc.add(group, "metal", torus(tuple(p), (0, 1, 0), 1.6, 0.15, 32, 8), None, True)
    s.extra.append(arrays)
    return s


def reef_station():
    s = Ship("CORAL BLOOM", 105)
    import random
    rnd = random.Random(5)

    def branch(p, d, length, r, depth):
        if depth == 0 or r < 1.2:
            s.ellipsoid(p.x, p.y, p.z, r * 2.2, r * 2.2, r * 2.2, "hull2")
            return
        q = p + d * length
        n = int(length)
        for i in range(n + 1):
            t = i / max(1, n)
            c = p + (q - p) * t
            rr = r * (1 - 0.35 * t)
            s.ellipsoid(c.x, c.y, c.z, rr, rr, rr, "hull")
        k = 2 if depth > 1 else 3
        for j in range(k):
            nd = (d + Vector((rnd.uniform(-0.9, 0.9), rnd.uniform(-0.9, 0.9), rnd.uniform(-0.4, 0.8)))).normalized()
            branch(q, nd, length * 0.72, r * 0.68, depth - 1)
    branch(Vector((0, 0, -40)), Vector((0, 0, 1)), 36, 9, 4)
    s.ellipsoid(0, 0, -46, 22, 22, 12, "hull2", shell=3)

    def pods(acc, group, S):
        rnd2 = random.Random(9)
        cells = [c for c, t in s.cells.items() if t == "hull2"]
        for i in range(70):
            c = cells[rnd2.randrange(len(cells))]
            p = (Vector(c) + Vector((0.5, 0.5, 0.5))) * CELL
            acc.add(group, "emit_p", sphere(tuple(p), rnd2.uniform(0.4, 1.1), 10, 5), None, True)
    s.extra.append(pods)
    return s


def facet_station():
    s = Ship("PRISM HUB", 106)

    def bipyr(cx, cy, cz, r, h, tag):
        s.fill(lambda x, y, z: (abs(x - cx) + abs(y - cy)) / r + abs(z - cz) / h <= 1, (int(cx - r) - 1, int(cx + r) + 2),
               (int(cy - r) - 1, int(cy + r) + 2), (int(cz - h) - 1, int(cz + h) + 2), tag)
    bipyr(0, 0, 0, 34, 52, "hull")
    s.paint(lambda x, y, z: z > 18, "hull2")
    s.paint(lambda x, y, z: abs(z) < 4, "accent")
    for a in range(0, 360, 60):
        r = math.radians(a)
        cx, cy = math.cos(r) * 78, math.sin(r) * 78
        s.cyl_z(cx, cy, 12, -3, 3, "hull2")
        s.box(int(cx) - 3, int(cx) + 3, int(cy) - 3, int(cy) + 3, 3, 12, "hull")
        s.beam_line((math.cos(r) * 26, math.sin(r) * 26, 0), (cx, cy, 0), 4, "trim")
    bipyr(0, 0, 70, 6, 20, "accent")
    bipyr(0, 0, -70, 6, 20, "hull2")
    return s


STATIONS = {
    "drydock": drydock_station, "nocturne": nocturne_station, "pulp-horizon": pulp_station,
    "orbital": orbital_station, "reef": reef_station, "facet": facet_station,
}
