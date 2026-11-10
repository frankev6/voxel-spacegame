"""Heist option B: THE COUNTING-HOUSE (Triple Alliance). A busy customs and bank station; the treasure is the Charter Seal.

Owner (2026-11-10): the transponder "hides you from the map ... but even neutrals or allies will see it as suspicious
behaviour if they find out".

The opposite of the Reliquary: here you go in openly. The Alliance's counting-house sits on a trade lane and every ship
passes through the Throughway, a customs tunnel through the station with scanner gates, transponder on. Inside, you
slip off the lane into the freight docks, go dark among the stacks of the warehouse, and drop down the freight lift
to the deep vault, a cube hung on struts in a cavity under the counting floor. The Charter Seal is in the cube. Getting
out means dragging it back up a crowded station full of traffic that will notice a dark ship with a load.
"""
from place_kit import Place, DISTRICT as C, polar


def design():
    P = Place("heist_counting", "Heist B · The Counting-House", "A Triple Alliance customs station: go in openly, go dark inside, lift the Seal.",
              ((-560, -280, -240), (560, 280, 240)), cell=8.0)

    # ------------------------------------------------------------------ the body: stepped terraces of jade plate
    for k in range(4):
        w, d = 1040 - k * 120, 440 - k * 70
        P.box((0, 0, 40 + k * 44), (w, d, 44), "jade" if k % 2 == 0 else "hull")
        P.box((0, 0, -40 - k * 44), (w, d, 44), "jade" if k % 2 == 0 else "hull")
    P.box((0, 0, 0), (1040, 440, 40), "gold")
    for x in range(-480, 520, 120):
        P.box((x, 0, 0), (12, 452, 260), "trim")                           # frames
    # 1 the Throughway: the customs tunnel, end to end, with scanner gates
    P.carve_box((0, -130, 0), (1100, 72, 64))
    # 2 the freight docks: bays cut into the north face
    for x in (-300, -150, 0, 150, 300):
        P.carve_box((x, 200, 8), (90, 90, 60))
    # 3 the warehouse: a hall of container stacks between the docks and the Throughway
    P.carve_box((0, 60, 10), (760, 180, 120))
    for x in range(-340, 360, 56):
        for y in (20, 100):
            h = 24 + ((x // 56 + y) % 4) * 16
            P.box((x, y, -46 + h / 2), (36, 48, h), "crate" if (x // 56) % 3 else "crate2")
    P.carve_box((0, -60, 10), (760, 40, 60))                               # an aisle to the Throughway
    for x in (-200, 0, 200):
        P.carve_box((x, -96, 0), (40, 40, 40))                             # side doors from the Throughway
    # 4 the counting floor: the hall above, with its drone galleries
    P.carve_box((0, 0, 130), (520, 300, 60))
    for x in range(-220, 240, 60):
        P.box((x, 0, 104), (30, 260, 8), "gold")
    # 5 the freight lift: a shaft from the warehouse floor down into the vault cavity
    P.carve_cylinder((260, 60, -50), (260, 60, -200), 24)
    # 6 the deep vault: a cube hung on struts in a cavity under everything
    P.carve_box((0, 40, -170), (300, 220, 120))
    P.box((0, 40, -170), (72, 72, 72), "gold")
    P.carve_box((0, 40, -170), (48, 48, 48))
    P.carve_box((0, 4, -170), (24, 24, 24))                                # its door
    for sx in (-1, 1):
        for sy in (-1, 1):
            P.cylinder((sx * 36, 40 + sy * 36, -170), (sx * 140, 40 + sy * 100, -170 + sy * 50), 4, "trim")
    P.ellipsoid((0, 40, -176), (8, 8, 8), "pearl")                          # the Charter Seal
    # 7 the drains: a cramped coolant duct from the cavity to the south face
    P.carve_tube([(-120, -20, -200), (-220, -150, -200), (-260, -300, -180)], 14)

    # ------------------------------------------------------------------ light, signs, alarm
    for x in range(-520, 540, 50):
        P.lamp((x, -130, 26), C["lime"], 0.35)
    for x in (-450, -150, 150, 450):
        P.holo((x, -130, -30), C["lime"], r=30)
        P.marker((x, -130, 0), 46, C["lime"])
    for x in range(-340, 360, 80):
        P.lamp((x, 60, 60), C["amber"], 0.5)
    for x in (-180, 0, 180):
        P.lamp((x, 0, 150), C["white"], 0.6)
        P.marker((x, 0, 140), 60, C["danger"])
    P.lamp((0, 40, -120), C["vault"], 1.2)
    P.lamp((120, 40, -200), C["vault"], 0.6)
    P.lamp((260, 60, -120), C["white"], 0.5)
    for x in (-300, -150, 0, 150, 300):
        P.lamp((x, 210, 30), C["white"], 0.6)
    P.sign((-556, -130, 44), "CUSTOMS", C["lime"], yaw=270, size=7)
    P.sign((0, 222, 64), "ALLIANCE FREIGHT", C["amber"], yaw=0, size=6)
    P.strip([(-540, -130, -28), (540, -130, -28)], C["lime"])
    P.strip([(-300, 170, -16), (-300, 100, -44), (260, 60, -44)], C["amber"])

    # ------------------------------------------------------------------ ships
    for i, x in enumerate((-480, -260, 40, 300)):
        P.ship((x, -130, 0), 90 if i % 2 == 0 else 270, "heavy" if i % 2 else "light")
    for x in (-300, 0, 300):
        P.ship((x, 210, 6), 0, "heavy", lit=False)
    for i in range(6):
        P.ship((-1000 + i * 100, -130 - (i % 2) * 40, 20 * (i % 3)), 90, "light" if i % 2 else "heavy")
    for x in (-180, 180):
        P.ship((x, 0, 140), 0, "light")

    # ------------------------------------------------------------------ routes
    P.route("the lane, transponder on", [(-1400, -130, 0), (-560, -130, 0), (560, -130, 0), (1400, -130, 0)], C["lime"])
    P.route("in: off the lane, go dark", [(-200, -130, 0), (-200, -96, 0), (-200, -60, 10), (100, 60, -20), (260, 60, -50), (260, 60, -170), (0, 40, -170)], C["violet"])
    P.route("out with the Seal (the drains)", [(0, 40, -170), (-120, -20, -200), (-260, -300, -180), (-600, -700, -200)], C["amber"], kind="escape")
    P.route("out with the Seal (back up, loud)", [(0, 40, -170), (260, 60, -170), (260, 60, -50), (300, 200, 8), (400, 700, 40)], C["danger"], kind="escape")
    P.route("drone patrol", [(-220, -120, 140), (220, -120, 140), (220, 120, 140), (-220, 120, 140), (-220, -120, 140)], C["danger"], kind="patrol")

    # ------------------------------------------------------------------ points of interest
    P.poi(1, "The Throughway", (-300, -130, 0), "lime", "the customs tunnel: everyone flies through, transponder on, scanned at each gate",
          view=((-600, -150, 20), (-200, -130, 0)))
    P.poi(2, "Side door", (-200, -96, 0), "violet", "slip off the lane between scanners into the warehouse aisle, and go dark",
          view=((-170, -160, 10), (-200, -40, 0)))
    P.poi(3, "The warehouse", (0, 60, 0), "amber", "a hall of container stacks: cover for a dark ship, and a maze for a load on a cable",
          view=((-300, -40, 50), (100, 80, -10)))
    P.poi(4, "Counting floor", (0, 0, 130), "danger", "the hall above with drone patrols; a dark ship seen here is reported at once",
          view=((250, -130, 150), (-60, 30, 130)))
    P.poi(5, "Freight lift", (260, 60, -100), "white", "the shaft down to the vault cavity; its car is a mover you can ride or cut through",
          view=((260, 30, -40), (260, 64, -200)))
    P.poi(6, "Deep vault", (0, 40, -170), "vault", "a gold cube hung on struts in a dark cavity; the Charter Seal inside; lift it and the clock runs",
          view=((120, -50, -150), (0, 40, -172)))
    P.poi(7, "The drains", (-220, -150, -200), "amber", "a cramped coolant duct to the south face: the quiet way out, tight for a load",
          view=((-100, -10, -196), (-240, -200, -196)))
    P.poi(8, "Freight docks", (0, 200, 8), "white", "Alliance haulers moor here: a hauler with a hold could stow the Seal and leave legally",
          view=((40, 420, 60), (0, 200, 0)))

    P.level(10, "the Throughway and the warehouse")
    P.level(150, "the counting floor")
    P.level(-160, "the vault cavity")
    P.section("x", 0, "through the vault, south to north")
    P.section("y", 40, "along the warehouse and the vault")

    P.notes = dict(
        idea=("The opposite of the Reliquary: you go in openly. The Triple Alliance's counting-house sits on a trade lane and every "
              "ship passes through the Throughway, a customs tunnel with scanner gates, transponder on. Inside, you slip off the lane, "
              "go dark among the warehouse stacks and drop down the freight lift to the deep vault, a gold cube hung on struts in a "
              "cavity under the counting floor. The Charter Seal is inside. Getting out means dragging it through a station full of "
              "traffic that will notice a dark ship with a load."),
        plays=["Fly the lane in with your transponder on, like any trader; between scanners, slip through a side door and go dark.",
               "The warehouse stacks are cover and a maze; the counting floor's drones report any dark ship they see.",
               "Lift the Seal: the clock starts. Out by the drains (quiet, tight for a load) or back up the lift and out a dock (loud).",
               "A crew with a hauler can moor it legally at the docks, stow the Seal in its hold, and fly out on the lane."],
        strengths=["A different kind of stealth from the Reliquary: blending in, then going dark; uses both sides of the transponder.",
                   "Traffic makes every run different and gives a hauler a role (stow the Seal and leave legally).",
                   "Horizontal and crowded, so the two heists feel unlike each other."],
        costs=["Needs traffic that reacts to suspicion (SPEC-33 provocation plus the new transponder rules).",
               "Many movers (lift, drones) and dense stacks: more AI and more voxels than the Reliquary."],
        key=[("the lane (legal)", C["lime"]), ("way in (dark)", C["violet"]), ("ways out with the Seal", C["amber"]),
             ("alarm, drones", C["danger"]), ("the vault", C["vault"])],
    )
    return P
