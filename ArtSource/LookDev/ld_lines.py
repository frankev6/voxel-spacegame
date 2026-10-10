"""Round 4: a second ship line for DRYDOCK and for NOCTURNE (frigate, cargo ship, cruiser, dreadnought).

A line is unified by its build pattern and colours, not by one hull outline:

  DRYDOCK  line B  Caisson Heavy Industries. Machines, not hulls: every ship is a working tool (a wrench, a tank
                   carrier, a crane, a floating drydock), built from heavy yellow plate on graphite frames, open
                   lattice girders, hydraulic rams, grapple claws and amber work beacons.
  NOCTURNE line B  The Knell choir. Everything curves: ribs, arches, bells and hanging things. Bone plate over
                   blackened bronze ribs, gilt trim, candle-lit lancet windows, gargoyles and small bells.

Coordinates in cells (0.5 m): x right, y forward, z up; the same conventions as ld_ships."""

import math

from ld_geom import hash01
from ld_ships import Ship

BIG = dict(big=True)


def arc_pts(cx, cy, R, a0, a1, n, z=0.0):
    """Points on a horizontal arc (degrees, counter-clockwise from +x)."""
    return [(cx + R * math.cos(math.radians(a0 + (a1 - a0) * i / n)), cy + R * math.sin(math.radians(a0 + (a1 - a0) * i / n)), z)
            for i in range(n + 1)]


def rot2(p, ang, o=(0.0, 0.0)):
    a = math.radians(ang)
    x, y = p[0] - o[0], p[1] - o[1]
    return (o[0] + x * math.cos(a) - y * math.sin(a), o[1] + x * math.sin(a) + y * math.cos(a))


# ======================================================================================================================
# DRYDOCK line B: Caisson Heavy Industries
# ======================================================================================================================

CAISSON_GREEBLES = dict(window=0.05, hatch=0.05, vent=0.05, pipe=0.12, light=0.02, antenna=0.01, handrail=0.05)


def caisson_spanner():
    """SPANNER: boarding frigate, ~55 m. A combination wrench: an open jaw at the bow, set at 15 degrees like a real
    spanner, that closes on a hull while the cutting lance in its throat opens it; a closed ring at the stern around
    the engines; the cab perched off-centre on the handle."""
    s = Ship("SPANNER", 101, "caisson")
    s.greeble_override = CAISSON_GREEBLES
    # the handle: an I-section bar with a raised spine
    s.loft([(-8, 7, 0, 12, 3, 2), (40, 7, 0, 12, 3, 2), (52, 6, 0, 12, 3, 2), (58, 5, 1, 11, 2, 2)])
    s.loft([(-4, 3, 12, 14, 1), (44, 3, 12, 14, 1), (48, 2, 12, 13, 1)], "armor")
    s.loft([(4, 1, 2, 10, 0, 0), (40, 1, 2, 10, 0, 0)], "trim", cx=7, mirror=True)
    # the open jaw: an arc band of 290 degrees, rotated 15 degrees off the handle
    H = rot2((0, 18), 15)
    H = (H[0], H[1] + 58)
    path = arc_pts(H[0], H[1], 17, 105 + 50, 105 - 50 + 360, 18, 0)
    s.sweep(path, (5, 0, 12, 3, 2), "hull")
    # teeth at the jaw tips, pointing into the gap
    for ang in (105 + 50, 105 - 50):
        tip = (H[0] + 17 * math.cos(math.radians(ang)), H[1] + 17 * math.sin(math.radians(ang)))
        inward = (math.cos(math.radians(ang + 180)), math.sin(math.radians(ang + 180)))
        tng = (-math.sin(math.radians(ang)), math.cos(math.radians(ang)))
        pts = []
        for k in (-3.5, 3.5):
            for z in (2, 10):
                pts.append((tip[0] + tng[0] * k - inward[0] * 2, tip[1] + tng[1] * k - inward[1] * 2, z))
        apex = (tip[0] + inward[0] * 9, tip[1] + inward[1] * 9)
        pts += [(apex[0], apex[1], 5), (apex[0], apex[1], 7)]
        s.hull(pts, "armor")
    # throat block with the cutting lance aimed out through the jaw
    s.slab([(-6, 58), (6, 58), (6, 72), (-6, 72)], 0, 8, 1, 1, "armor")
    s.mod("beam", -1, 65, 8)
    s.mod("harpoon", -1, 40, 14)
    # the closed ring at the stern, around the engines
    s.sweep(arc_pts(0, -15, 11, 90, 450, 20, 2), (3.5, 0, 9, 2, 1), "hull", closed=True)
    s.mod("thruster_l", -5, -14, 3)
    s.mod("thruster_l", 1, -14, 3)
    # the cab: an off-centre tower on the handle
    s.tower(4, 28, [(12, 4, 1), (19, 4, 1), (20, 3, 1)], "hull2")
    s.mod("cockpit", 2, 26, 20)
    s.mod("beacon", 1, 27, 20)
    s.mod("sensor", -1, 18, 14)
    # jaw-tip guns, hydraulic rams on the handle
    s.mod("autocannon", -22, 79, 12)
    s.mod("autocannon", 6, 87, 12)
    for y in (6, 20):
        s.mod("piston", -4, y, 12)
    s.mod("radiator", -2, -4, 14)
    s.mod("radiator", 1, -4, 14)
    s.mod("core_2", -1, 20, 2)
    s.mod("reactor", -1, 4, 2)
    s.paint(lambda x, y, z: z < 2.5 and y < 56, "armor")
    s.paint(lambda x, y, z: 46 <= y < 48 and z > 3 and abs(x) > 4, "accent")
    return s


def caisson_carboy():
    """CARBOY: bulk tanker, ~95 m. Four insulated spherical tanks threaded on an open lattice spine, each in a yellow
    cradle ring, with a bulb cab at the bow and an engine block at the stern."""
    s = Ship("CARBOY", 102, "caisson", unit=2)
    s.greeble_override = dict(CAISSON_GREEBLES, window=0.02)
    tanks = [(20, 13), (56, 16), (96, 16), (132, 13)]
    # the spine: an open girder (lattice) from the engine block to the cab, with two manifold pipes
    s.box(-3, 3, -8, 156, -3, 3, "girder", big=True)
    s.drum((4, -6, 4), (4, 152, 4), 1.2, n=8, tag="metal")
    s.drum((-4, -6, 4), (-4, 152, 4), 1.2, n=8, tag="metal")
    for y, r in tanks:
        s.ball((0, y, 0), r, "hull2", n=2, big=True)
        s.ring((0, y, 0), "z", r - 1.5, r + 1.5, 1.5, 24, ("hull",), big=True)
        s.ring((0, y, 0), "y", r - 1.5, r + 1.0, 1.0, 24, ("hull",), big=True)
        s.box(-3, 3, y - 3, y + 3, r - 1, r + 2, "armor")                 # platform on the crown of each tank
        s.box(-4, 4, y - 4, y + 4, -r - 2, -r + 1, "armor")               # keel shoe
    s.mod("flak", -2, 54, 16 + 2)
    s.mod("flak", -2, 94, 16 + 2)
    s.mod("autocannon", -1, 19, 13 + 2)
    s.mod("autocannon", -1, 131, 13 + 2)
    # the bow: a bulb cab on a short neck
    s.drum((0, 148, 0), (0, 160, 0), 6, 7, 8, tag="hull", big=True)
    s.ball((0, 163, 1), 7.5, "hull", n=1, big=True)
    s.box(-5, 5, 156, 168, 6, 8, "armor")
    s.mod("cockpit", -2, 160, 8)
    s.mod("beacon", -5, 157, 8)
    s.mod("beacon", 4, 157, 8)
    s.mod("sensor", 2, 164, 8)
    # the stern: engine block, radiators, three main engines
    s.loft([(-26, 9, -8, 8, 3, 3), (-8, 9, -8, 8, 3, 3), (-4, 6, -6, 6, 2, 2)], "armor", big=True)
    s.mod("thruster_l", -6, -32, -6)
    s.mod("thruster_l", 2, -32, -6)
    s.mod("thruster_l", -2, -32, 1)
    s.mod("radiator", -6, -24, 8)
    s.mod("radiator", 5, -24, 8)
    s.mod("beacon", -1, -20, 8)
    s.paint(lambda x, y, z: z < -6 and y < 0, "trim")
    return s


def caisson_derrick():
    """DERRICK: crane cruiser, ~140 m with the boom. No hull box at all: a round slewing hub braced on an X of four
    outrigger legs, the crane house turned to follow its boom, and a lattice boom swung out over the starboard bow with
    a graviton snare and a grapple claw at its head. It drags prizes in for the batteries on the hub's rim."""
    s = Ship("DERRICK", 103, "caisson", unit=2)
    s.greeble_override = CAISSON_GREEBLES
    C = (0.0, 96.0)
    # the hub: a lower graphite skirt, the yellow drum, a slewing ring on top
    s.drum((C[0], C[1], -6), (C[0], C[1], 6), 40, 46, n=16, tag="armor", big=True)
    s.drum((C[0], C[1], 6), (C[0], C[1], 20), 46, 44, n=16, tag="hull", big=True)
    s.drum((C[0], C[1], 20), (C[0], C[1], 26), 30, 30, n=16, tag="trim", big=True)
    # bow and stern pods hung off the hub (not a hull box: two short tapered noses)
    s.loft([(130, 16, -2, 18, 5, 4), (156, 12, 0, 16, 4, 3), (176, 5, 4, 12, 2, 1)], "hull", big=True)
    s.loft([(14, 8, 0, 14, 3, 3), (60, 14, -4, 18, 4, 4)], "armor", big=True)
    # outrigger legs: an X, each ending in a pad with a flak turret
    for sx in (-1, 1):
        for ang in (35, -35):
            a = math.radians(90 - ang * sx) if ang > 0 else math.radians(270 + ang * sx)
            d = (math.cos(math.radians(90 + sx * (90 - abs(ang)) * (1 if ang > 0 else -1))) if False else 0, 0)
            del d, a
        for (ex, ey) in ((60, 168), (64, 22)):
            p0 = (sx * 30, C[1] + (ey - C[1]) * 0.4, 4)
            p2 = (sx * ex, ey, 0)
            p1 = ((p0[0] + p2[0]) / 2, (p0[1] + p2[1]) / 2, 8)
            s.sweep([p0, p1, p2], [(4, -4, 4, 1.5, 1.5), (3.5, -3.5, 3.5, 1, 1), (3, -3, 3, 1, 1)], "armor", big=True)
            s.drum((sx * ex, ey, -8), (sx * ex, ey, 8), 7, 6.5, n=8, tag="hull", big=True)
            s.mod("flak", sx * ex - 2, ey - 2, 8)
    # the crane house and counterweight, turned to line up with the boom
    yaw = 28.0
    def rpoly(pts):
        return [tuple(v + o for v, o in zip(rot2(p, -yaw), C)) for p in pts]
    s.slab(rpoly([(-14, -10), (14, -10), (14, 26), (-14, 26)]), 26, 46, 3, 0, "hull", big=True)
    s.slab(rpoly([(-16, -32), (16, -32), (16, -10), (-16, -10)]), 26, 42, 2, 2, "armor", big=True)
    s.slab(rpoly([(-10, 26), (10, 26), (6, 34), (-6, 34)]), 26, 40, 2, 0, "hull2", big=True)
    ax = lambda u, v, z: tuple(list(rot2((u, v), -yaw)) and [rot2((u, v), -yaw)[0] + C[0], rot2((u, v), -yaw)[1] + C[1], z])
    s.sweep([ax(-8, -6, 40), ax(0, 4, 72)], (1.6, -1.6, 1.6, 0.5, 0.5), "trim")
    s.sweep([ax(8, -6, 40), ax(0, 4, 72)], (1.6, -1.6, 1.6, 0.5, 0.5), "trim")
    apex = ax(0, 4, 72)
    s.box(round(apex[0]) - 2, round(apex[0]) + 2, round(apex[1]) - 2, round(apex[1]) + 2, 70, 74, "armor")
    s.mod("beacon", round(apex[0]) - 1, round(apex[1]) - 1, 74)
    # the boom: a tapering lattice girder from the house front, rising 8 degrees
    p0 = ax(0, 18, 36)
    el, L = math.radians(8), 176.0
    d = (math.sin(math.radians(yaw)) * math.cos(el), math.cos(math.radians(yaw)) * math.cos(el), math.sin(el))
    tip = tuple(p0[k] + d[k] * L for k in range(3))
    pts = [tuple(p0[k] + d[k] * L * t for k in range(3)) for t in (0.0, 0.5, 1.0)]
    s.sweep(pts, [(5.5, -5.5, 5.5, 0, 0), (4.5, -4.5, 4.5, 0, 0), (3, -3, 3, 0, 0)], "girder", big=True)
    s.cables = [(apex, tip), ((apex[0] + 1, apex[1], apex[2]), tuple(p0[k] + d[k] * L * 0.6 for k in range(3)))]
    hx, hy, hz = (round(v) for v in tip)
    s.box(hx - 7, hx + 7, hy - 6, hy + 8, hz - 6, hz + 2, "hull", big=True)
    s.mod("graviton", hx - 7, hy - 4, hz + 2)
    s.mod("claw", hx - 2, hy - 2, hz - 12, rot=(-90, 0, 0))
    # the cab on the house, batteries on the hub rim
    cab = ax(4, 18, 46)
    s.mod("cockpit", round(cab[0]) - 2, round(cab[1]) - 2, 46)
    s.mod("battery", -6, 128, 20)
    s.mod("battery", -6, 46, 20, rot=(0, 0, 180))
    for k in range(6):
        a = math.radians(150 + k * 12)
        x, y = C[0] + math.cos(a) * 37, C[1] + math.sin(a) * 37
        s.mod("missile", round(x) - 1, round(y) - 1, 20)
    for y in (150, 162):
        s.mod("autocannon", -1, y, 18 if y == 150 else 16)
    for x0 in (-15, -5, 5):
        s.mod("thruster_xl", x0 - 3, 2, 1 + (4 if x0 == -5 else 0))
    s.paint(lambda x, y, z: 6 <= z < 8, "accent")
    return s


def caisson_dreadnought():
    """CAISSON: dreadnought, ~260 m. A floating drydock shaped like a pair of tongs: a round hinge hub at the stern
    (engines, the yard tower) and two armoured arms that bow out around an oval cradle and close to a jaw at the bow.
    A hull in the cradle is repaired; fired, the glowing coil hoops strung through the cradle throw whatever is in it
    (slag, scrap, a captured hull) out of the jaw at a few percent of light."""
    s = Ship("CAISSON", 104, "caisson", unit=3)
    s.greeble_override = CAISSON_GREEBLES
    H = (0.0, 40.0)
    # the hinge hub: a stepped round hull
    s.drum((0, H[1], -6), (0, H[1], 6), 50, 58, n=16, tag="armor", big=True)
    s.drum((0, H[1], 6), (0, H[1], 36), 58, 56, n=16, tag="hull", big=True)
    s.drum((0, H[1], 36), (0, H[1], 42), 44, 40, n=16, tag="trim", big=True)
    # the arms: bowed sweeps that taper in width and height toward the jaw
    for sx in (-1, 1):
        path, secs = [], []
        n = 12
        for k in range(n + 1):
            t = k / n
            y = 60 + 410 * t
            x = sx * (30 + 66 * math.sin(math.pi * (0.12 + 0.88 * t)) - 18 * t)
            path.append((x, y, 18))
            w = 26 - 14 * t
            secs.append((w, -18 + 6 * t, 18 - 8 * t, 6, 5))
        s.sweep(path, secs, "hull", big=True)
        # an armour belt and a raised upper deck along the outer edge of each arm
        belt = [(p[0] + sx * (secs[k][0] - 3), p[1], 8) for k, p in enumerate(path)][1:10]
        s.sweep(belt, (4, -10, 6, 2, 2), "armor", big=True)
        # the jaw: a curved tooth turning in at the tip
        tip = path[-1]
        s.sweep([tip, (tip[0] - sx * 10, tip[1] + 14, 18), (tip[0] - sx * 24, tip[1] + 18, 18)],
                [(10, -9, 9, 3, 3), (7, -7, 7, 2, 2), (3, -4, 4, 1, 1)], "armor", big=True)
    # coil hoops strung through the cradle, each braced to both arms
    for k, y in enumerate(range(140, 420, 46)):
        s.ring((0, y, 18), "y", 30, 35, 2, 32, ("trim",), big=True)
        s.ring((0, y, 18), "y", 28.5, 30.5, 1, 32, ("emit_p",))
        t = (y - 60) / 410
        xa = 30 + 66 * math.sin(math.pi * (0.12 + 0.88 * t)) - 18 * t - (26 - 14 * t) + 4
        for sx in (-1, 1):
            s.sweep([(sx * 33, y, 18), (sx * xa, y, 18)], (2.5, -3, 3, 1, 1), "armor", big=True)
    # the yard tower on the hub, a wedding cake of drums
    for z0, z1, r in ((42, 66, 18), (66, 84, 13), (84, 96, 9)):
        s.drum((0, H[1] - 8, z0), (0, H[1] - 8, z1), r, r, n=12, tag="hull2", big=True)
    s.mod("cockpit", -2, H[1] - 4, 96)
    s.mod("sensor", -5, H[1] - 12, 96)
    s.mod("beacon", 2, H[1] - 13, 96)
    s.mod("shield", 10, H[1] - 10, 84)
    # round barbettes on the arms with triple batteries
    for sx in (-1, 1):
        for t in (0.33, 0.6):
            y = 60 + 410 * t
            x = sx * (30 + 66 * math.sin(math.pi * (0.12 + 0.88 * t)) - 18 * t)
            top = 18 + 18 - 8 * t
            ztop = int(top) + 6
            s.drum((x, y, int(top) - 6), (x, y, ztop), 17, 16, n=16, tag="armor", big=True)
            s.mod("battery", round(x) - 9, round(y) - 13, ztop, size=(18, 27, 12))
        for t in (0.15, 0.8):
            y = 60 + 410 * t
            x = sx * (30 + 66 * math.sin(math.pi * (0.12 + 0.88 * t)) - 18 * t)
            top = int(18 + 18 - 8 * t)
            s.box(round(x) - 3, round(x) + 3, round(y) - 3, round(y) + 3, top - 4, top + 2, "armor")
            s.mod("flak", round(x) - 2, round(y) - 2, top + 2)
    # engines: four nacelles behind the hub
    for x in (-36, -12, 12, 36):
        s.drum((x, 2, 14), (x, -22, 14), 9, 8, n=8, tag="armor", big=True)
        s.mod("thruster_xl", x - 4, -34, 10)
    for k in range(8):
        a = math.radians(200 + k * 20)
        x, y = math.cos(a) * 50, H[1] + math.sin(a) * 50
        s.mod("radiator", round(x), round(y) - 3, 36)
    s.paint(lambda x, y, z: z < 4, "armor")
    s.weapons = {"slot": (0, 480, 18)}
    return s


DRYDOCK_B = [("frigate", caisson_spanner), ("cargo", caisson_carboy), ("cruiser", caisson_derrick),
             ("dreadnought", caisson_dreadnought)]


# ======================================================================================================================
# NOCTURNE line B: the Knell choir
# ======================================================================================================================

KNELL_GREEBLES = dict(window=0.03, hatch=0.03, vent=0.05, pipe=0.08, strobe=0.012, spike=0.03, rib=0.06)


def knell_vigil():
    """VIGIL: escort frigate, ~45 m. A hunched body under a bat's wings: bone ribs fanning from the shoulders with
    bronze membranes scalloped between them, a cowled head, a spiked tail, small bells hung under the belly, and arc
    projectors on the shoulders."""
    s = Ship("VIGIL", 201, "knell")
    s.greeble_override = KNELL_GREEBLES
    s.loft([(-10, 4, 2, 12, 3, 2), (8, 6, 0, 15, 4, 3), (48, 6, 0, 15, 4, 3), (66, 4, 2, 13, 3, 2), (78, 2, 5, 10, 2, 1)])
    s.loft([(36, 5, 15, 20, 3), (58, 4, 13, 18, 2), (68, 2, 12, 15, 1)], "armor")
    # the wings: four bone ribs per side from the shoulder; bronze membrane between them, scalloped trailing edges
    sh = (5.0, 40.0, 11.0)
    tips = [(34, 64, 14), (46, 42, 12), (42, 16, 9), (28, -6, 7)]
    mids = [(18, 56, 13), (26, 44, 12), (24, 28, 10), (16, 14, 9)]
    for t, m in zip(tips, mids):
        s.sweep([sh, m, t], [(1.6, -1.6, 1.6, 0.5, 0.5), (1.4, -1.4, 1.4, 0.5, 0.5), (0.8, -0.8, 0.8, 0.3, 0.3)], "hull", mirror=True)
        # talon at the rib tip
        d = [t[k] - m[k] for k in range(3)]
        L = math.sqrt(sum(v * v for v in d))
        tip2 = tuple(t[k] + d[k] / L * 6 for k in range(3))
        s.hull([(t[0] - 1, t[1] - 1, t[2] - 1), (t[0] + 1, t[1] + 1, t[2] - 1), (t[0] - 1, t[1] + 1, t[2] + 1), (t[0] + 1, t[1] - 1, t[2] + 1), tip2],
               "armor", mirror=True)
    for (ta, ma), (tb, mb) in zip(zip(tips, mids), list(zip(tips, mids))[1:]):
        scal = tuple(sh[k] + ((ta[k] + tb[k]) / 2 - sh[k]) * 0.62 for k in range(3))
        for tri in ((sh, ma, ta, scal), (sh, scal, tb, mb)):
            pts = []
            for p in tri:
                pts += [(p[0], p[1], p[2] - 0.5), (p[0], p[1], p[2] + 0.5)]
            s.hull(pts, "hull2", mirror=True)
    # the tail
    s.sweep([(0, -8, 8), (0, -24, 7), (0, -40, 9), (0, -50, 12)],
            [(3, -3, 3, 1, 1), (2.4, -2.4, 2.4, 1, 1), (1.6, -1.6, 1.6, 0.5, 0.5), (0.6, -0.6, 0.6, 0.2, 0.2)], "armor")
    for y in (-20, -32):
        s.spire(0, y, 1, 9, 15, 2.0, "hull")
    s.mod("thruster_l", -2, -16, 0, )
    s.mod("thruster_s", 6, 2, 3, mirror=True)
    s.mod("cockpit", -2, 50, 17)
    s.mod("tesla", 3, 30, 15, mirror=True)
    s.mod("gatling", -1, 60, -2, rot=(0, 180, 0))
    for y in (14, 26, 38):
        s.mod("bell_s", -1, y, -3, rot=(0, 180, 0))
    s.mod("beacon", -1, 60, 20)
    s.mod("core_2", -1, 24, 3)
    s.paint(lambda x, y, z: z < 2.5, "armor")
    s.paint(lambda x, y, z: 46 <= y < 47 and z > 5 and abs(x) < 6, "accent")
    return s


def knell_reliquary():
    """RELIQUARY: relic barge, ~95 m. A ribcage: a vertebral spine over a keel, arched ribs between them, and inside,
    the cargo: rows of gilded reliquary caskets on a bier; a skull-browed prow and a pelvis that carries the engines."""
    s = Ship("RELIQUARY", 202, "knell", unit=2)
    s.greeble_override = dict(KNELL_GREEBLES, window=0.02)
    top, low = 34, 2
    # vertebral spine
    for i in range(14):
        y0 = 8 + i * 9
        s.drum((0, y0, top), (0, y0 + 5, top), 5.5, n=8, tag="hull", big=True)
        s.drum((0, y0 + 5, top), (0, y0 + 9, top), 3.2, n=8, tag="armor")
        s.spire(0, y0 + 2.5, 1, top + 4, top + 9 + int(4 * math.sin(math.pi * (i + 0.5) / 14)), 1.6, "hull")
    # keel (sternum)
    s.drum((0, 14, low), (0, 128, low), 4, n=8, tag="armor", big=True)
    # ribs: half-ellipses from spine to keel, biggest amidships
    for i in range(1, 13):
        y = 8 + i * 9 + 2.5
        bulge = math.sin(math.pi * (i - 0.5) / 12.5)
        rx = 8 + 16 * bulge
        pts = []
        for k in range(10):
            th = math.pi * k / 9
            pts.append((rx * math.sin(th) * 1.0, y, (top + low) / 2 + (top - low) / 2 * math.cos(th)))
        s.sweep(pts, (1.4, -1.6, 1.6, 0.5, 0.5), "hull", up=(0, 1, 0), mirror=True)
    # the bier and the caskets
    s.box(-13, 13, 22, 120, 5, 6, "trim")
    for row, y in enumerate(range(26, 116, 22)):
        for sx in (-1, 1):
            cx = sx * 6.5
            poly = [(cx - 3, y), (cx + 3, y), (cx + 4.5, y + 12), (cx + 3, y + 18), (cx - 3, y + 18), (cx - 4.5, y + 12)]
            s.slab(poly, 6, 13, 1.5, 0.5, "hull2")
    s.paint(lambda x, y, z: 12 <= z < 13 and abs(abs(x) - 6.5) < 0.6, "accent")
    # the skull-browed prow
    s.ball((0, 140, 22), 12, "hull", n=1, big=True)
    s.loft([(128, 9, 24, 34, 3, 2), (146, 9, 24, 34, 3, 2), (152, 6, 25, 32, 2, 1)], "armor", big=True)
    s.drum((0, 132, 10), (0, 150, 10), 7, 5, n=8, tag="armor", big=True)
    s.mod("cockpit", -2, 144, 34)
    s.mod("beacon", -5, 146, 34)
    s.mod("beacon", 4, 146, 34)
    # the pelvis and the engines
    s.loft([(-12, 10, 0, 30, 4, 4), (12, 12, 0, 36, 4, 4), (16, 8, 2, 34, 3, 3)], "armor", big=True)
    for x0, z0 in ((-7, 4), (3, 4), (-2, 14), (-7, 22), (3, 22)):
        s.mod("thruster_l", x0, -18, z0)
    s.mod("radiator", 9, -6, 30, mirror=True)
    # flak on spine platforms, bells under the keel, candles on the bier
    for y in (35, 71, 107):
        s.box(-2, 2, y, y + 4, top + 4, top + 6, "trim")
        s.mod("flak", -2, y, top + 6)
    for y in (30, 60, 90):
        s.mod("bell_s", -1, y, low - 7, rot=(0, 180, 0))
    for y in (24, 46, 68, 90, 112):
        s.mod("candle", -1, y, 6)
    s.paint(lambda x, y, z: z < -1, "armor")
    return s


def knell_censer():
    """CENSER: cruiser, ~120 m. An arched spine like a thurible's handle, with a crossbar at the crown; under the arch
    hangs an openwork globe on four chains, a furnace caged inside it. The censer vents an ionised 'incense' storm
    that strips shields; batteries ride the feet of the arch."""
    s = Ship("CENSER", 203, "knell", unit=2)
    s.greeble_override = KNELL_GREEBLES
    # the arch: a swept spine in the y-z plane (section: radial thickness 6, width 8 each side)
    pts = []
    for k in range(15):
        t = 0.28 + (math.pi - 0.56) * k / 14
        pts.append((0.0, 112 - 108 * math.cos(t), 18 + 54 * math.sin(t)))
    s.sweep(pts, (6, -8, 8, 2, 2), "hull", up=(1, 0, 0), big=True)
    # the feet of the arch: stern house and bow chapel
    s.loft([(-24, 16, 0, 30, 5, 4), (18, 16, 0, 30, 5, 4), (26, 12, 4, 28, 4, 3)], "hull", big=True)
    s.loft([(198, 12, 4, 28, 4, 3), (206, 16, 0, 30, 5, 4), (236, 16, 0, 30, 5, 4), (252, 8, 6, 24, 3, 2), (262, 3, 10, 18, 2, 1)], "hull", big=True)
    s.loft([(-20, 4, 30, 34, 1), (14, 4, 30, 34, 1)], "armor", cx=10, mirror=True)
    # spires on the feet
    for y in (-14, 4):
        for sx in (-1, 1):
            s.spire(sx * 10, y, 2, 30, 44, 1.8, "hull")
    for y in (214, 228):
        for sx in (-1, 1):
            s.spire(sx * 10, y, 2, 30, 42, 1.8, "hull")
    # the crossbar at the crown, with lanterns at its ends
    s.loft([(-46, 4, 66, 76, 2, 2), (46, 4, 66, 76, 2, 2)], "hull2", cx=112, axis="x", big=True)
    for sx in (-1, 1):
        s.drum((sx * 46, 112, 54), (sx * 46, 112, 68), 4, 5, n=8, tag="hull2", big=True)
        s.mod("bell_s", sx * 46 - 1, 111, 51, rot=(0, 180, 0))
        s.spire(sx * 46, 112, 2, 76, 86, 2.0, "hull")
    # the globe: meridian ribs, latitude rings, a crown and a finial; the furnace inside
    G = (0.0, 112.0, 0.0)
    RG = 30
    for j in range(8):
        a = math.pi * j / 8
        n_ = (-math.sin(a), math.cos(a), 0.0)
        path = []
        for k in range(13):
            ph = math.pi * k / 12
            path.append((G[0] + RG * math.sin(ph) * math.cos(a), G[1] + RG * math.sin(ph) * math.sin(a), G[2] + RG * math.cos(ph)))
        s.sweep(path, (1.6, -1.6, 1.6, 0.5, 0.5), "hull2", up=n_)
    for zf, rf in ((0.0, 1.0), (0.55, 0.835), (-0.55, 0.835)):
        s.ring((G[0], G[1], G[2] + RG * zf), "z", RG * rf - 1.5, RG * rf + 1.0, 1.2, 32, ("hull",))
    s.drum((0, 112, RG - 2), (0, 112, RG + 6), 9, 6, n=12, tag="hull", big=True)
    s.spire(0, 112, 2, -RG - 10, -RG + 2, 1.6, "hull2", down=True)
    for k in range(4):
        a = math.pi / 4 + k * math.pi / 2
        s.sweep([(math.cos(a) * (RG - 1), 112 + math.sin(a) * (RG - 1), 0), (math.cos(a) * 7, 112 + math.sin(a) * 7, 0)],
                (1.0, -1.0, 1.0, 0.3, 0.3), "trim")
    s.mod("censer_core", -6, 106, -6)
    # the chains: four struts from the crown to the arch
    for sx, sy in ((-1, -1), (1, -1), (-1, 1), (1, 1)):
        s.sweep([(sx * 6, 112 + sy * 6, RG + 4), (sx * 3, 112 + sy * 10, 66)], (0.9, -0.9, 0.9, 0.3, 0.3), "trim")
    # weapons and engines on the feet
    s.mod("battery", -6, 206, 30)
    s.mod("battery", -6, -6, 30, rot=(0, 0, 180))
    for sx in (-1, 1):
        s.mod("nova", sx * 14 - 3, -20, 30, size=(6, 6, 18))
        s.mod("flak", sx * 12 - 1, 240, 24) if False else None
    for y in (150, 174):
        pass
    s.mod("railgun", 16, 0, 10, rot=(0, 90, 0))
    s.mod("railgun", -18, 0, 10, rot=(0, -90, 0))
    s.mod("railgun", 16, 216, 10, rot=(0, 90, 0))
    s.mod("railgun", -18, 216, 10, rot=(0, -90, 0))
    for x0, z0 in ((-12, 4), (4, 4), (-12, 16), (4, 16)):
        s.mod("thruster_xl", x0, -36, z0)
    s.mod("cockpit", -2, 238, 30)
    s.mod("sensor", -1, 232, 30)
    for y in (180, 186):
        s.mod("candle", -1, y, 30) if False else None
    s.paint(lambda x, y, z: z < 4, "armor")
    return s


def knell_dreadnought():
    """KNELL: dreadnought, ~230 m. A bell 90 m across the mouth, flying mouth-first. The Toll, a clapper the size of a
    frigate, hangs from the crown inside; struck, the bell rings a graviton wave out of its mouth that shakes block
    bonds loose across the whole cone. Bronze buttress ribs, a crown of spires, gilt inscription bands, gargoyles on
    the lip, small bells under it, engines behind the crown."""
    s = Ship("KNELL", 204, "knell", unit=3)
    s.greeble_override = KNELL_GREEBLES
    y0, y1 = 70, 450
    prof = [(0.0, 54, 54), (0.08, 58, 10), (0.3, 58, 9), (0.5, 62, 9), (0.68, 72, 10), (0.82, 86, 13), (0.93, 96, 14), (1.0, 95, 9)]
    s.shell((0, y0, 0), (0, y1, 0), prof, n=24, tag="hull", big=True)
    # buttress ribs along the outside
    for j in range(12):
        a = math.pi / 24 + 2 * math.pi * j / 12 + math.pi / 12
        path = []
        for t, r, k in prof[1:]:
            path.append((math.cos(a) * (r + 2.5), y0 + (y1 - y0) * t, math.sin(a) * (r + 2.5)))
        s.sweep(path, (3, -2.5, 2.5, 1, 1), "hull2", up=(0, 1, 0), big=True)
    # gilt inscription bands and a glowing resonator ring inside the lip
    L = y1 - y0
    s.paint(lambda x, y, z: y0 + 0.86 * L <= y < y0 + 0.88 * L or y0 + 0.4 * L <= y < y0 + 0.415 * L, "accent")
    s.paint(lambda x, y, z: y0 + 0.95 * L <= y < y0 + 0.975 * L and x * x + z * z < 84 ** 2, "emit_x")
    # the crown, the hanging loop and the engines
    s.drum((0, 18, 0), (0, 72, 0), 36, 46, n=16, tag="armor", big=True)
    s.sweep([(math.cos(math.radians(a)) * 30, 46, 30 + math.sin(math.radians(a)) * 30) for a in range(0, 181, 15)],
            (5, -5, 5, 2, 2), "hull2", up=(0, 1, 0), big=True)
    for x0, z0 in ((-4, -4), (-22, -4), (14, -4), (-13, 12), (5, 12), (-13, -20), (5, -20)):
        s.mod("thruster_xl", x0, 6, z0)
    # the Toll, hanging from the crown inside the bell
    s.mod("toll", -20, 126, -20, size=(40, 150, 40))
    # platforms on the shoulders: batteries, the bridge, a crown of spires
    def platform(y, z, hw, hl):
        s.box(-hw, hw, y - hl, y + hl, z - 12, z, "armor", big=True)
    platform(150, 63, 12, 18)
    s.mod("battery", -9, 136, 63, size=(18, 27, 12))
    platform(250, 69, 12, 18)
    s.mod("battery", -9, 236, 69, size=(18, 27, 12))
    platform(110, 63, 9, 12)
    s.mod("cockpit", -2, 112, 63)
    s.mod("sensor", -6, 104, 63)
    for i, y in enumerate((96, 186, 206, 300, 320)):
        h = (30, 42, 36, 30, 26)[i]
        s.spire(0 if i in (0,) else (-14 if i % 2 else 14), y, 4, 50, 63 + h, 1.8, "hull", big=True)
    # gargoyles on the lip, bells under it, flak on the flanks
    for k in range(10):
        a = math.pi / 2 + (k - 4.5) * 0.24
        x, z = math.cos(a) * 92, math.sin(a) * 92
        s.box(int(x) - 2, int(x) + 2, 428, 436, int(z) - 6, int(z) + 1, "armor")
        s.mod("gargoyle", int(x) - 1, 431, int(z) + 1)
    for k in range(8):
        a = -math.pi / 2 + (k - 3.5) * 0.3
        x, z = math.cos(a) * 94, math.sin(a) * 94
        s.box(int(x) - 2, int(x) + 2, 426, 434, int(z) - 1, int(z) + 6, "armor")
        s.mod("bell_s", int(x) - 1, 429, int(z) - 4, rot=(0, 180, 0))
    for y in (180, 300):
        for sx in (-1, 1):
            s.box(sx * 60 - 4, sx * 60 + 4, y - 4, y + 4, 34, 46, "armor", big=True)
            s.mod("flak", sx * 60 - 2, y - 2, 46)
    s.weapons = {"toll": (0, 450, 0)}
    return s


NOCTURNE_B = [("frigate", knell_vigil), ("cargo", knell_reliquary), ("cruiser", knell_censer), ("dreadnought", knell_dreadnought)]

LINES = {"drydock": DRYDOCK_B, "nocturne": NOCTURNE_B}
