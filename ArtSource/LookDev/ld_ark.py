"""Round 4, batch 3 in 3D: the ARK expedition's ships, built from the approved plan-view silhouettes in sil_batch3.py.

The ARK war line shares one build rule: heat to port, ammunition to starboard. Hulls are graphite, armour blocks bone
(hull2), magazines safety yellow (armor), frames and radiator decks dark (trim), and one red stripe across each hull marks its fire team (livery 'arkw').

Coordinates in cells (0.5 m): x right (starboard), y forward, z up; the silhouette's metres times two."""

from ld_ships import Ship

ARK_GREEBLES = dict(window=0.05, hatch=0.06, vent=0.015, pipe=0.08, light=0.015, antenna=0.012, handrail=0.04, stripe=0.0, plate=0.0)


def ark_halo():
    """HALO: fire-control destroyer, ~80 m. The team's shield (six point-defence turrets and a flak battery) and its
    eyes: the fire-control dish on a boom to port marks one target for every linked gun. Heat to port (radiator wing),
    ammunition to starboard (flak magazine)."""
    s = Ship("HALO", 201, "arkw")
    s.greeble_override = ARK_GREEBLES
    # power and data trunks along the dorsal hull, from the reactor to the bridge
    for x in (-3, 2):
        s.drum((x, 14, 18.3), (x, 70, 18.3), 1.0, n=8, tag="metal")
    # hull: aft engine block, main hull, waist under the bridge, forward point-defence sponsons, bone armour prow
    s.loft([(-4, 24, 0, 14, 3, 3), (12, 24, 0, 14, 3, 3), (19, 17, 0, 14, 3, 3)])
    s.loft([(12, 17, 0, 18, 4, 3), (72, 17, 0, 18, 4, 3), (76, 13, 0, 18, 4, 3), (104, 13, 0, 18, 4, 3)])
    s.loft([(102, 25, 0, 16, 3, 3), (120, 25, 0, 16, 3, 3), (129, 16, 0, 16, 3, 3)])
    s.loft([(128, 16, 1, 15, 4, 3), (140, 10, 1, 15, 3, 2), (156, 2, 2, 11, 1, 1)], "hull2")
    s.loft([(116, 16, 15, 17, 1, 0), (128, 16, 15, 17, 1, 0)], "hull2")
    # port: the radiator wing, a thin deck carrying rows of radiator fins
    s.slab([(-16, 18), (-32, 24), (-32, 60), (-16, 66)], 8, 10, 0, 0, "trim")
    for x in (-30, -27, -24, -21):
        for y in (24, 32, 40, 48):
            s.mod("radiator", x, y, 10, size=(1, 8, 6))
    # port: fire-control boom (a lattice arm) to the pedestal and the dish, tilted toward the bow
    s.sweep([(-10, 86, 10), (-26, 101, 10), (-41, 115, 10)], (2, -2, 2, 1, 1), "girder")
    s.tower(-43, 117, [(6, 4, 1), (18, 3, 1), (19, 2, 1)], "trim")
    s.shell((-43, 115, 17), (-43, 121, 27), [(0.0, 1.5, 1.5), (0.4, 8.0, 1.2), (1.0, 13.0, 1.0)], 20, "hull2")
    s.drum((-43, 116, 18), (-43, 122, 30), 0.7, n=6, tag="metal")
    # starboard: the flak magazine, a yellow box against the hull, with a hazard band at its bow end
    s.loft([(24, 6, 0, 14, 2, 2), (80, 6, 0, 14, 2, 2)], "hull", cx=23)
    s.paint(lambda x, y, z: x > 17 and 24 <= y < 80, "armor")
    s.paint(lambda x, y, z: x > 17 and 74 <= y < 77, "dark")
    s.mod("flak", 20, 42, 14, size=(6, 6, 6))
    # bridge tower on the waist, the cockpit facing forward, a sensor mast and a point-defence mount on its roof
    s.tower(0, 90, [(17, 9, 2), (24, 8, 2), (26, 6, 2)], "hull2")
    s.mod("cockpit", -2, 91, 26)
    s.mod("gatling", -2, 84, 26, size=(4, 6, 4))
    # weapons: a twin coilgun turret on the prow, point defence fore and aft
    s.mod("heavy_cannon", -4, 129, 15, size=(8, 12, 6))
    s.mod("gatling", 17, 110, 16, size=(4, 6, 4), mirror=True)
    s.mod("gatling", 8, 56, 18, size=(4, 6, 4))
    s.mod("gatling", 16, 2, 14, size=(4, 6, 4), mirror=True)
    # engines and internals
    s.mod("thruster_l", 4, -16, 3, size=(8, 12, 8), mirror=True)
    s.mod("thruster_s", 14, -10, 5, size=(4, 6, 4), mirror=True)
    s.mod("reactor", -1, 20, 6)
    s.mod("core_2", -1, 44, 6)
    s.mod("beacon", -8, 62, 18)
    # the fire-team stripe across the hull
    s.paint(lambda x, y, z: 71 <= y < 74 and abs(x) <= 17 and z > 2, "paint_w")
    return s


ARK_SHIPS = [("halo", ark_halo)]
