"""Den option A: THE HULK. The crew's den is a dead dreadnought they bring back to life, deck by deck.

Owner (2026-11-10): "new den player co-op station (can be hollow inspired but must be much different, player and
centered, upgradable, etc)", "the den should show this [co-op]", and "the coolest thing about the hollow is being able
to go inside it, you should be able with your ship to go inside stations and interact with everything from your ship".

A 960 m warship hulk, every plate a voxel. You fly in through the hangar mouth on its flank. The flight deck inside is
the Hearth: the crew's berths ring the docket board, ships parked nose-in like seats at a table. A spine corridor runs
the length of the ship above it, from the torn bow to the engine room; the engine room is the breaker yard, open to
space at the stern where the engines were torn out, so tows come in from behind. The trade dock is a dorsal hangar
open to the sky; the foundry fills the bow magazine; the bridge tower looks out over the gate yard.
Upgrades restore the hulk: sealed bulkheads open, dark decks light up, more berths, a rebuilt prow.
"""
from place_kit import Place, DISTRICT as C, polar


def design():
    P = Place("den_hulk", "Den A · The Hulk", "A dead dreadnought the crew brings back to life: you fly inside it, every deck is yours.",
              ((-176, -520, -128), (176, 520, 200)), cell=8.0)

    # ------------------------------------------------------------------ the hull: segments that hunch amidships
    segs = [(-480, -380, 100, -80, 72), (-380, -200, 112, -88, 84), (-200, 160, 120, -96, 96), (160, 320, 104, -84, 76),
            (320, 400, 84, -64, 56), (400, 480, 52, -40, 32)]
    for y0, y1, hw, zb, zt in segs:
        P.box(((0), (y0 + y1) / 2, (zb + zt) / 2), (2 * hw, y1 - y0, zt - zb), "hull")
    # armour belts and ribs
    for y0, y1, hw, zb, zt in segs[1:4]:
        for sx in (-1, 1):
            P.box((sx * (hw + 4), (y0 + y1) / 2, zb * 0.3), (8, y1 - y0 - 24, (zt - zb) * 0.45), "armor")
    for y in range(-440, 440, 80):
        hw = next(s[2] for s in segs if s[0] <= y < s[1])
        zb, zt = next((s[3], s[4]) for s in segs if s[0] <= y < s[1])
        P.box((0, y, (zb + zt) / 2), (2 * hw + 16, 8, zt - zb + 16), "trim")
    P.box((0, -20, 104), (128, 320, 24), "armor")                       # dorsal ridge
    P.box((0, 0, -104), (64, 640, 24), "plate_dark")                     # keel
    for sx in (-1, 1):
        for y in (-260, -100, 60):
            P.box((sx * 136, y, -40), (32, 112, 48), "armor")            # sponsons
    P.box((-40, -260, 136), (72, 112, 104), "armor")                     # the bridge tower
    P.box((-40, -270, 196), (40, 64, 24), "hull")
    P.cylinder((-40, -270, 208), (-40, -270, 260), 3, "trim")

    # ------------------------------------------------------------------ interiors
    # 2 the Hearth: the old flight deck, 180 x 260 x 72 m
    P.room((0, 10, 4), (180, 260, 72))
    P.carve_box((108, 10, -2), (40, 184, 56))                            # 1 the hangar mouth, starboard
    P.cylinder((0, 10, -30), (0, 10, -22), 18, "trim")                   # the table
    P.cylinder((0, 10, -22), (0, 10, -18), 22, "armor")
    for i, a in enumerate(range(0, 360, 60)):
        x, y, _ = polar(a + 30, 64, (0, 10))
        P.box((x, y, -28), (24, 24, 8), "deck")
    # 3 the Spine: bow breach to engine room, over the Hearth, with a well down into it
    spine = [(0, 500, 4), (0, 330, 24), (0, 160, 56), (0, -150, 56), (0, -300, 32), (0, -360, 8)]
    P.carve_tube(spine, 22, rz=18)
    P.carve_box((0, 10, 48), (56, 112, 24))
    # 4 the Breaker: the engine room, open to space at the stern
    P.ellipsoid((0, -408, 0), (112, 108, 92), "plate_dark")
    P.carve_ellipsoid((0, -408, 0), (100, 96, 80), noise=0.05, seed=3)
    P.torus((0, -420, 0), 52, 6, "armor", axis="y")                      # the cradle ring
    for a in (30, 150, 270):
        x, z, _ = polar(a, 92)
        P.cylinder((x, -420, z), (x * 0.45, -420, z * 0.45), 5, "hull2")   # cutting arms
    P.box((0, -420, 0), (40, 32, 24), "rust", yaw=24)                     # a wreck in the cradle
    P.box((12, -410, 14), (20, 16, 16), "rust", yaw=-10)
    # 5 the trade dock: a dorsal hangar open to the sky
    P.room((0, 250, 56), (152, 128, 64))
    P.carve_box((0, 250, 96), (152, 128, 24))
    for x in (-56, -20, 20, 56):
        P.box((x, 300, 28), (24, 16, 16), "crate" if x < 0 else "crate2")
    # 6 the foundry: the bow magazine, reached down a well from the spine
    P.room((0, 360, -24), (112, 72, 48), wall="armor")
    P.carve_cylinder((0, 360, 0), (0, 360, 30), 16)
    for x in (-40, -12, 16, 44):
        P.box((x, 386, -36), (16, 16, 24), "plate_dark")                  # smelters
    # 7 the bridge: up a shaft from the spine
    P.room((-40, -258, 140), (48, 80, 40), wall="trim")
    P.carve_box((-40, -214, 144), (36, 16, 20))                           # its window, looking forward
    P.carve_cylinder((-40, -258, 50), (-40, -258, 126), 14)
    # 9 battle damage: a breach in the port flank into the Hearth, and the torn bow
    P.carve_ellipsoid((-118, 54, 18), (34, 44, 28), noise=0.25, seed=7)
    P.carve_ellipsoid((0, 486, 12), (64, 44, 40), noise=0.3, seed=9)
    P.carve_ellipsoid((96, -150, 88), (30, 40, 20), noise=0.3, seed=11)

    # ------------------------------------------------------------------ light, signs, markers
    for y in range(-110, 150, 40):
        for x in (-84, 84):
            P.lamp((x, y, 34), C["amber"], 0.8)
    for i, a in enumerate(range(0, 360, 60)):
        x, y, _ = polar(a + 30, 64, (0, 10))
        P.lamp((x * 1.2, y * 1.2 - 2, -24), C["berths"] if i < 4 else C["white"], 0.5)
    P.lamp((0, 10, 6), C["hearth"], 1.2)
    for y in range(-340, 480, 60):
        z = 56 if -150 <= y <= 160 else 30
        P.lamp((14, y, z - 10), C["white"], 0.5)
    for a in (0, 90, 180, 270):
        x, z, _ = polar(a, 70)
        P.lamp((x, -400, z), C["breaker"], 1.2)
    for x in (-60, 60):
        P.lamp((x, 250, 80), C["trade"], 1.0)
    for x in (-40, 30):
        P.lamp((x, 360, -10), C["foundry"], 1.2)
    P.lamp((-40, -258, 150), C["white"], 0.6)
    P.strip([(126, 10, -26), (90, 10, -28), (40, 10, -28)], C["berths"])
    P.strip([(0, -50, -28), (0, -100, -28), (0, -118, -10), (0, -118, 30)], C["hearth"])
    P.strip([(-18, -150, 40), (-18, -300, 16), (-18, -350, -4)], C["breaker"])
    P.strip([(-18, 160, 40), (-18, 330, 8)], C["foundry"])
    P.sign((128, 10, 34), "THE HEARTH", C["hearth"], yaw=90, size=7)
    P.sign((0, -506, 64), "BREAKER", C["breaker"], yaw=180, size=8)
    P.sign((0, 186, 96), "TRADE", C["trade"], yaw=180, size=6)
    P.sign((0, 330, 4), "FOUNDRY", C["foundry"], yaw=180, size=4)
    P.sign((-40, -222, 158), "BRIDGE", C["white"], yaw=180, size=3)
    P.holo((0, 10, -16), C["hearth"], r=12)
    P.holo((0, -420, -60), C["breaker"], r=10)
    P.holo((0, 250, 30), C["trade"], r=9)
    P.holo((0, 360, -46), C["foundry"], r=8)

    # ------------------------------------------------------------------ ships, gates
    for i, (a, cls) in enumerate(zip(range(0, 360, 60), ("light", "heavy", "light", "heavy"))):
        x, y, _ = polar(a + 30, 64, (0, 10))
        import math
        yaw = math.degrees(math.atan2(-x, -(y - 10)))
        P.ship((x, y, -18), yaw, cls)
    P.ship((-40, 250, 44), 90, "heavy", lit=False)
    P.ship((40, 250, 44), 270, "heavy", lit=False)
    P.ship((20, -560, 10), 0, "light")                                     # bringing a tow in
    P.gate((-260, 1000, 40), 110, facing=0, label="gate 1")
    P.gate((160, 1180, -60), 90, facing=-15, label="gate 2")
    P.gate((520, 920, 110), 80, facing=-35, lit=False, label="gate 3 (tier 3)")

    # ------------------------------------------------------------------ routes
    P.route("arrival", [(-260, 1000, 40), (-60, 760, 30), (300, 300, 10), (300, 10, -6), (130, 10, -10), (60, 10, -18)], C["arrival"])
    P.route("tows to the breaker", [(60, -900, 0), (30, -620, 0), (0, -440, 0)], C["breaker"])
    P.route("spine", [(0, -360, 8), (0, -150, 56), (0, 160, 56), (0, 330, 24), (0, 360, 0), (0, 360, -20)], C["foundry"], kind="lane")
    P.route("to the trade dock", [(300, 300, 10), (120, 250, 140), (0, 250, 70)], C["trade"])

    # ------------------------------------------------------------------ points of interest
    P.poi(1, "Hangar mouth", (124, 10, -2), "arrival", "the front door, on the starboard flank; the crew's guide strips lead in",
          view=((360, -120, 40), (100, 10, 0)))
    P.poi(2, "The Hearth", (0, 10, -20), "hearth", "the old flight deck: the docket board on the table, six berths round it, ships nose-in",
          view=((76, -104, 14), (0, 22, -18)))
    P.poi(3, "The Spine", (0, 0, 56), "white", "the corridor the length of the ship, bow breach to engine room, a well down into the Hearth",
          view=((0, 150, 60), (0, -200, 48)))
    P.poi(4, "The Breaker", (0, -408, 0), "breaker", "the engine room, open at the stern: drag a tow into the cradle, let go, the arms cut it",
          view=((24, -344, 22), (0, -440, -6)))
    P.poi(5, "Trade dock", (0, 250, 50), "trade", "a dorsal hangar open to the sky: visiting merchants moor inside; you drop in from above",
          view=((90, 150, 150), (0, 262, 40)))
    P.poi(6, "Foundry", (0, 360, -24), "foundry", "the bow magazine: salvage becomes stronger materials (opens at tier 2)",
          view=((44, 330, -12), (-24, 384, -34)))
    P.poi(7, "The Bridge", (-40, -258, 140), "white", "up a shaft from the spine: the den ledger (upgrades) and the view over the gate yard",
          view=((-40, -292, 142), (-40, 200, 120)))
    P.poi(8, "Gate yard", (-60, 1000, 30), "gates", "the mission gates off the bow; a tier lights each one; friends arrive through gate 1",
          view=((40, 560, 80), (-120, 1000, 40)), inside=True)
    P.poi(9, "The breach", (-120, 54, 18), "danger", "a shell hole in the port flank, straight into the Hearth: a back door, patched at tier 2",
          view=((-340, 20, 50), (-110, 54, 14)))

    P.level(-8, "Hearth deck")
    P.level(64, "Spine")
    P.level(160, "Bridge")
    P.section("x", 0, "along the keel")
    P.section("y", 10, "across the Hearth")
    P.view("Top-down, the hull lid off", (0, -40, 1500), (0, 0, 0), fov=40, cut=60)

    P.notes = dict(
        idea=("The crew's den is a dead dreadnought, 960 m long, that you bring back to life. Every plate is a voxel. You fly in through "
              "the hangar mouth on its flank and land in the Hearth, the old flight deck, where the berths ring the docket board: "
              "your friends' ships park nose-in round the table, so whoever is online is visibly at the table. Above runs the Spine, "
              "bow to stern; the engine room at the stern is the breaker yard, open to space where the engines were torn out, so tows "
              "come in from behind. Upgrades restore the hulk section by section."),
        plays=["Arrive through gate 1, fly the lit lane to the hangar mouth, land on your berth: F at the table opens the docket board.",
               "Bring a tow home round the stern and into the Breaker; let go over the cradle and the arms cut it into blocks and credits.",
               "Tier 1: Hearth and Breaker lit, two berths, one gate. Tier 2: four berths, trade dock, foundry, breach patched. "
               "Tier 3: six berths, rebuilt prow, all three gates, a second cradle arm set.",
               "Every space is reachable by ship; the Spine links them in 3D (wells up to the bridge, down to the foundry)."],
        strengths=["Progress you can see: dark decks light up, sealed bulkheads open, the prow is rebuilt.",
                   "Unlike the Hollow: a ship, not a rock; one crew's home, not a clan city; long decks and wells, not a ring road.",
                   "Built from what exists: hollow capital-wreck shells (ADR-0119), the refit bay (DEV-033), the gates (SPEC-92 / ADR-0119)."],
        costs=["A 960 m hull at 2 m cells is a big grid; the shell must stay thin (ADR-0119 decision 10 budget).",
               "Interior lighting and a camera that works in tight decks with 3D flight."],
        key=[("berths and arrival", C["berths"]), ("the Hearth (docket board)", C["hearth"]), ("breaker yard", C["breaker"]),
             ("trade", C["trade"]), ("foundry", C["foundry"]), ("gates", C["gates"])],
    )
    return P
