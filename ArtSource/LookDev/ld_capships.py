"""Cruisers and dreadnoughts for DRYDOCK and NOCTURNE.

Scale ladder (length): light ~18 m, frigate ~50 m, cruiser ~115 m, dreadnought ~250 m. Big ships build their main hull
from large hull blocks (cruisers 2x2x2 cells = 1 m, dreadnoughts 3x3x3 = 1.5 m) and carry capital weapons; their
details (windows, hatches, small turrets) stay at human scale, which is what sells the size."""

import math

from ld_geom import hash01
from ld_ships import Ship, CONTAINER_TINTS

BIG = dict(big=True)


# ======================================================================================================================
# DRYDOCK
# ======================================================================================================================

def drydock_cruiser():
    """STEVEDORE: heavy cruiser, ~115 m. Container ledges along both flanks, a bridge tower, two triple batteries
    forward (the rear one superfiring), a drone bay, and a graviton snare on the foredeck to drag prizes home."""
    s = Ship("STEVEDORE", 13, "hauler", unit=2)
    s.loft([(0, 18, 0, 28, 4, 4), (150, 18, 0, 28, 4, 4), (176, 14, 4, 24, 4, 4), (200, 8, 8, 20, 4, 2), (212, 4, 10, 16, 2, 0)], **BIG)
    s.loft([(-26, 22, -2, 30, 6, 6), (0, 22, -2, 30, 6, 6), (6, 18, 0, 28, 4, 4)], "hull2", **BIG)
    s.loft([(30, 4, 6, 16, 2, 2), (130, 4, 6, 16, 2, 2), (138, 2, 8, 14, 2, 2)], "hull2", cx=22, mirror=True, big=True)
    s.loft([(28, 11, 28, 46, 4), (66, 11, 28, 46, 4), (74, 8, 28, 42, 4)], "hull2", **BIG)
    s.loft([(40, 7, 46, 54, 3), (60, 7, 46, 54, 3), (64, 5, 46, 52, 2)], **BIG)
    s.box(-8, 8, 104, 124, 28, 32, "armor", big=True)            # barbette for the superfiring battery
    s.box(-8, 8, 176, 190, 18, 24, "armor", big=True)            # foredeck platform
    s.paint(lambda x, y, z: z < 6, "armor")
    s.paint(lambda x, y, z: 140 <= y < 144 and z > 18, "accent")
    s.paint(lambda x, y, z: 50 <= y < 54 and z > 46, "accent")
    s.mod("battery", -6, 128, 28)
    s.mod("battery", -6, 104, 32)
    s.mod("graviton", -7, 177, 24)
    s.mod("hangar", -8, 78, 28)
    s.mod("cockpit", -2, 64, 46)
    s.mod("sensor", -1, 50, 54)
    s.mod("shield", 2, 44, 54, mirror=True)
    for y in range(80, 100, 3):
        s.mod("missile", 9, y, 28, mirror=True)
    for y in (34, 122):
        s.mod("flak", 20, y, 16, mirror=True)
    for y in (10, 146):
        s.mod("autocannon", 12, y, 28, mirror=True)
    for i, y in enumerate(range(40, 117, 4)):
        for col, x in enumerate((20, 22)):
            for k in range(1 + int(hash01(i, col, 7) < 0.55)):
                tint = CONTAINER_TINTS[int(hash01(i, col, k, 9) * len(CONTAINER_TINTS))]
                s.mod("cargo", x, y, 16 + k * 2, remap={"c_cargo": tint} if tint else None)
                s.mod("cargo", -x - 2, y, 16 + k * 2, remap={"c_cargo": tint} if tint else None)
    for x in (-14, -10, -6, 5, 9, 13):
        s.mod("radiator", x, -22, 30)
    for x0 in (-12, -4, 4):
        for z0 in (2, 16):
            s.mod("thruster_xl", x0, -38, z0)
    return s


def drydock_dreadnought():
    """ANVIL: dreadnought, ~250 m. A forked bow with a spinal particle lance in a dorsal trench, a singularity
    projector on the foredeck, three triple batteries, a castle with the bridge, nova silos and drone bays on the
    flanks, nine capital engines."""
    s = Ship("ANVIL", 14, "yard", unit=3)
    s.loft([(0, 36, 0, 48, 6, 9), (300, 36, 0, 48, 6, 9), (360, 30, 6, 42, 6, 6), (390, 24, 9, 36, 6, 3)], **BIG)
    s.loft([(390, 9, 9, 33, 3, 3), (438, 9, 12, 30, 3, 3), (456, 6, 15, 27, 3, 0)], "armor", cx=15, mirror=True, big=True)
    s.loft([(-36, 42, -6, 54, 9, 9), (0, 42, -6, 54, 9, 9), (9, 36, 0, 48, 6, 9)], "hull2", **BIG)
    # the castle and the bridge
    s.loft([(70, 22, 48, 72, 6), (140, 22, 48, 72, 6), (150, 16, 48, 66, 6)], "hull2", **BIG)
    s.loft([(90, 14, 72, 90, 4), (130, 14, 72, 90, 4), (136, 10, 72, 86, 4)], **BIG)
    s.spire(0, 104, 3, 90, 126, 2.0, "armor")
    s.box(-11, 11, 154, 184, 48, 54, "armor", big=True)          # barbette
    # dorsal trench for the lance
    s.clear(-7, 7, 280, 400, 18, 48)
    s.paint(lambda x, y, z: z < 9, "armor")
    s.paint(lambda x, y, z: 292 <= y < 298 and z > 30, "accent")
    s.paint(lambda x, y, z: 150 <= y < 156 and z > 40, "accent")
    s.mod("lance", -6, 282, 18, size=(12, 180, 12))
    s.mod("singularity", -12, 226, 48, size=(24, 36, 24))
    s.mod("battery", -9, 188, 48, size=(18, 27, 12))
    s.mod("battery", -9, 156, 54, size=(18, 27, 12))
    s.mod("battery", -9, 24, 48, rot=(0, 0, 180), size=(18, 27, 12))
    for y in (220, 252):
        s.mod("hangar", 12, y, 48, mirror=True)
    for y in range(160, 210, 10):
        s.mod("nova", 22, y, 48, mirror=True)
    for y in (18, 60, 140, 276):
        s.mod("flak", 27, y, 48, mirror=True)
    for y in range(30, 300, 30):
        s.mod("autocannon", 34, y, 30, rot=(0, 90, 0))
        s.mod("autocannon", -36, y, 30, rot=(0, -90, 0))
    s.mod("cockpit", -2, 136, 72)
    s.mod("sensor", 6, 110, 90, mirror=True)
    s.mod("shield", 8, 96, 90, mirror=True)
    for y in (76, 84, 92, 100, 108, 116, 124, 132):
        s.mod("radiator", 15, y, 72, mirror=True)
    for x0 in (-14, -4, 6):
        for z0 in (2, 14, 26):
            s.mod("thruster_xl", x0, -48, z0)
    s.weapons = {"lance": (0, 464, 24), "singularity": (0, 263, 61)}
    return s


# ======================================================================================================================
# NOCTURNE
# ======================================================================================================================

def nocturne_cruiser():
    """REQUIEM: heavy cruiser, ~115 m. A hunched, ribbed body with a nave along its back, spires, a lightning tower
    at the stern flanked by organ-pipe silos, heavy cannons down both sides of the nave, side-mounted railguns."""
    s = Ship("REQUIEM", 23, "company", unit=2)
    s.loft([(0, 16, 0, 26, 6, 4), (130, 16, 0, 26, 6, 4), (170, 12, 4, 22, 5, 3), (196, 6, 8, 16, 3, 1), (206, 3, 10, 13, 1, 0)], **BIG)
    s.loft([(-28, 18, -4, 30, 7, 5), (0, 18, -4, 30, 7, 5), (6, 16, 0, 26, 6, 4)], "armor", **BIG)
    s.loft([(20, 6, 26, 36, 3), (126, 6, 26, 36, 3), (140, 4, 26, 32, 2)], "hull2", **BIG)
    for i, y in enumerate(range(28, 109, 16)):
        h = 14 + int(10 * math.sin(math.pi * (i + 0.5) / 7))
        s.spire(0, y + 4, 2, 36, 36 + h, 1.6, "hull")
    for y in range(24, 130, 8):
        s.box(-16, -6, y, y + 2, 25, 28, "trim", big=True)
        s.box(6, 16, y, y + 2, 25, 28, "trim", big=True)
    for y in range(10, 120, 26):
        s.spire(-4, y, 2, 0, -12 - int(6 * hash01(y, 1)), 1.4, "armor", down=True)
        s.spire(4, y + 12, 2, 0, -12 - int(6 * hash01(y, 2)), 1.4, "armor", down=True)
    s.paint(lambda x, y, z: z < 4, "armor")
    s.paint(lambda x, y, z: 150 <= y < 152 and z > 12, "accent")
    s.mod("storm", -5, -24, 30)
    for y, h in ((-24, 22), (-14, 16)):
        s.mod("nova", 6, y, 30, size=(6, 6, h), mirror=True)
    for y in (40, 70, 100):
        s.mod("heavy_cannon", 6, y, 26, mirror=True)
    for y in (20, 120):
        s.mod("flak", 7, y, 26, mirror=True)
    for y in (60, 90):
        s.mod("railgun", 16, y, 8, rot=(0, 90, 0))
        s.mod("railgun", -18, y, 8, rot=(0, -90, 0))
    s.mod("cockpit", -2, 122, 36)
    s.mod("sensor", -1, 100, 36)
    for x0, z0 in ((-10, 0), (2, 0), (-10, 14), (2, 14)):
        s.mod("thruster_xl", x0, -40, z0)
    for x0 in (-17, 13):
        s.mod("thruster_l", x0, -34, 6)
    return s


def nocturne_dreadnought():
    """SEPULCHRE: dreadnought, ~250 m. A flying cathedral-fortress: a nave hull with a castle and a forest of spires,
    flying buttresses, keel spikes hanging underneath, an organ of nova silos, lightning towers, a singularity
    projector at the bow, triple batteries and side sponsons with drone bays."""
    s = Ship("SEPULCHRE", 24, "company", unit=3)
    s.loft([(0, 30, 0, 42, 9, 6), (320, 30, 0, 42, 9, 6), (380, 24, 6, 36, 9, 6), (420, 15, 12, 30, 6, 3), (444, 6, 18, 24, 3, 0)], **BIG)
    s.loft([(-36, 36, -6, 48, 12, 9), (0, 36, -6, 48, 12, 9), (9, 30, 0, 42, 9, 6)], "armor", **BIG)
    s.loft([(120, 24, 42, 66, 9), (200, 24, 42, 66, 9), (210, 18, 42, 60, 6)], "hull2", **BIG)
    s.loft([(140, 9, 12, 30, 3, 3), (300, 9, 12, 30, 3, 3), (312, 6, 15, 27, 3, 3)], "armor", cx=36, mirror=True, big=True)
    s.spire(0, 160, 6, 66, 132, 1.6, "hull", big=True)
    for sx in (-1, 1):
        s.spire(sx * 12, 140, 4, 66, 104, 1.8, "hull", big=True)
        s.spire(sx * 12, 182, 4, 66, 108, 1.8, "hull", big=True)
        s.spire(sx * 13, 161, 2, 66, 94, 2.0, "hull2", big=True)
    # flying buttresses from the castle walls down to the deck edges
    for y in range(126, 200, 12):
        planes = [((0, -1, 0), -y), ((0, 1, 0), y + 3), ((-1, 0, 0), -24), ((0, 0, -1), -42), ((2.4, 0, 1), 66 + 2.4 * 24)]
        s.convex(planes, ((24, y, 42), (34, y + 3, 66)), "trim", mirror=True, big=True)
    for i, y in enumerate(range(40, 320, 40)):
        s.spire(0, y, 4, -24 - int(14 * math.sin(math.pi * (i + 0.5) / 7)), 0, 1.4, "armor", down=True, big=True)
    s.paint(lambda x, y, z: z < 6, "armor")
    s.paint(lambda x, y, z: 326 <= y < 330 and z > 20, "accent")
    s.mod("singularity", -12, 290, 42, size=(24, 36, 24))
    s.mod("battery", -9, 252, 42, size=(18, 27, 12))
    s.box(-11, 11, 220, 250, 42, 48, "armor", big=True)            # barbette
    s.mod("battery", -9, 222, 48, size=(18, 27, 12))
    for x0, h in zip((-21, -15, -9, -3, 3, 9, 15), (14, 18, 22, 28, 22, 18, 14)):
        s.mod("nova", x0, 100, 42, size=(6, 6, h))
    for x0 in (-15, 5):
        s.mod("storm", x0, 122, 66)
    for y in (160, 226):
        s.mod("hangar", 28, y, 30, mirror=True)
    for y in (196, 258, 290):
        s.mod("heavy_cannon", 32, y, 30, mirror=True)
    for y in (20, 60, 230, 270):
        s.mod("flak", 18, y, 42, mirror=True)
    for y in (60, 100):
        s.mod("railgun", 30, y, 20, rot=(0, 90, 0))
        s.mod("railgun", -32, y, 20, rot=(0, -90, 0))
    s.mod("cockpit", -2, 206, 60)
    s.mod("sensor", -1, 196, 66)
    for x0 in (-14, -4, 6):
        for z0 in (4, 20):
            s.mod("thruster_xl", x0, -48, z0)
    s.weapons = {"singularity": (0, 327, 55), "battery": (0, 284, 48)}
    return s
