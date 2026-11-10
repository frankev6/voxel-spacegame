"""Den option B: THE GEODE. A hollow ice-rock moonlet with a vertical well through its heart.

Owner (2026-11-10): the den "can be hollow inspired but must be much different, player and centered, upgradable", and
"the coolest thing about the hollow is being able to go inside it".

Where the Hollow is a flat ring road round caverns at one height, the Geode is a 3D place: a well 560 m deep, lined
with ice, with the crew's berths cut as niches up its walls in a spiral round the docket board, which hangs in the
middle of the well. You come in through the Crown at the top and drop down to your niche. Tows come in at the bottom,
through the Throat, to the breaker cradle on the well floor, whose glow lights the whole shaft from below. Side tunnels
at their own heights lead to the market cavern (east, with its own mouth) and the foundry (west, deep).
Upgrades are digging: each tier cuts new niches and tunnels, fresh pale rock against the old.
"""
import math

from place_kit import Place, DISTRICT as C, polar, helix


def design():
    P = Place("den_geode", "Den B · The Geode", "A hollow ice-rock moonlet: the crew's berths spiral up a lit well round the board.",
              ((-416, -448, -400), (416, 416, 424)), cell=8.0)

    # ------------------------------------------------------------------ the rock
    P.ellipsoid((0, 0, 0), (360, 340, 360), "rock", noise=0.06, scale=90, seed=2, strata=True)
    P.ellipsoid((20, -10, 230), (270, 250, 150), "ice", noise=0.1, scale=60, seed=4)
    P.ellipsoid((-30, 20, 0), (200, 200, 330), "rock", noise=0.04, scale=50, seed=5, strata=True)
    P.ellipsoid((0, 0, 10), (156, 156, 300), "ice", noise=0.03, seed=6)           # the well's ice lining
    for a, z in ((40, -40), (150, 120), (260, -180), (320, 200)):
        x, y, _ = polar(a, 330)
        P.ellipsoid((x, y, z), (60, 60, 50), "ice", noise=0.2, seed=a)            # ice outcrops

    # ------------------------------------------------------------------ 2 the Well, and 1 the Crown at its top
    P.carve_ellipsoid((0, 0, 10), (128, 128, 290), noise=0.05, scale=40, seed=8)
    P.carve_cylinder((0, 0, 220), (0, 0, 430), 72, r2=96)
    # 3 the niches: six berths spiralling up the wall
    niches = []
    for i in range(6):
        a = i * 60 + 15
        z = -150 + i * 60
        x, y, _ = polar(a, 150)
        P.carve_box((x, y, z), (64, 64, 32), yaw=-a)
        P.box((x, y, z - 18), (64, 64, 8), "deck", yaw=-a)
        niches.append((a, x, y, z))
    # the board: a lantern hung from the Crown by three cables
    P.torus((0, 0, 30), 30, 3, "trim")
    P.cylinder((0, 0, 24), (0, 0, 32), 18, "armor")
    for a in (0, 120, 240):
        x, y, _ = polar(a, 30)
        X, Y, _ = polar(a, 110)
        P.cylinder((x, y, 32), (X, Y, 270), 2.5, "trim")
    # 4 the breaker: the well floor
    P.carve_ellipsoid((0, 0, -268), (160, 160, 84), noise=0.06, seed=9, floor=-312)
    P.torus((0, 0, -296), 52, 6, "armor")
    for a in (30, 150, 270):
        x, y, _ = polar(a, 120)
        X, Y, _ = polar(a, 46)
        P.cylinder((x, y, -306), (X, Y, -280), 5, "hull2")
    P.box((0, 0, -290), (40, 28, 22), "rust", yaw=30)
    # 5 the Throat: the tow entrance, from the south face down into the breaker
    P.carve_tube([(0, -440, -150), (0, -330, -180), (0, -200, -240), (0, -100, -270)], 44, rz=34, noise=0.08, seed=12)
    # 6 the market: east tunnel at +40, a cavern, and its own mouth
    P.carve_tube([(110, 0, 40), (200, 10, 50), (260, 10, 50)], 34, rz=26, noise=0.08, seed=13)
    P.carve_ellipsoid((276, 10, 48), (84, 96, 52), noise=0.08, seed=14, floor=12)
    P.carve_tube([(330, 60, 50), (430, 120, 60)], 30, rz=24, noise=0.1, seed=15)
    for k, (dx, dy) in enumerate(((-30, -50), (10, -60), (50, -30), (-40, 60), (20, 70))):
        P.box((276 + dx, 10 + dy, 18), (20, 14, 12), "crate" if k % 2 else "crate2")
    # 7 the foundry: west, deep
    P.carve_tube([(-112, 0, -120), (-200, 20, -126), (-250, 24, -130)], 30, rz=24, noise=0.08, seed=16)
    P.carve_ellipsoid((-270, 24, -128), (70, 76, 46), noise=0.08, seed=17, floor=-160)
    for dy in (-30, 0, 30):
        P.box((-300, 24 + dy, -150), (16, 16, 24), "plate_dark")
    # the Crown's works: a collar of plates and docking lights round the mouth
    P.torus((0, 0, 360), 104, 10, "armor")
    for a in range(0, 360, 45):
        x, y, _ = polar(a, 130)
        P.box((x, y, 352), (24, 24, 40), "hull", yaw=-a)

    # ------------------------------------------------------------------ light, signs, markers
    for z in (-200, -100, 0, 100, 200):
        for a in range(0, 360, 45):
            x, y, _ = polar(a + (z % 90), 122)
            P.lamp((x, y, z), C["amber"], 0.5)
    for a, x, y, z in niches:
        P.lamp((x * 0.88, y * 0.88, z - 6), C["berths"], 0.5)
    P.lamp((0, 0, 50), C["hearth"], 1.2)
    for a in (0, 120, 240):
        x, y, _ = polar(a, 90)
        P.lamp((x, y, -270), C["breaker"], 1.6)
    P.lamp((276, 10, 70), C["trade"], 1.0)
    P.lamp((240, -40, 40), C["trade"], 0.6)
    P.lamp((-270, 24, -110), C["foundry"], 1.2)
    for z in (-170, -210, -250):
        P.lamp((24, -330 + (z + 170) * -2.2, z), C["white"], 0.5)
    for a in range(0, 360, 30):
        x, y, _ = polar(a, 104)
        P.lamp((x, y, 376), C["white"], 0.4)
    P.strip(helix((0, 0), 124, -170, 140, 0.84, d0=15, n=60), C["berths"])
    P.strip([(-20, -420, -176), (-20, -300, -194), (-20, -190, -250)], C["breaker"])
    P.strip([(116, -20, 18), (230, -10, 26)], C["trade"])
    P.strip([(-116, -16, -140), (-230, 4, -150)], C["foundry"])
    P.sign((0, -112, 330), "THE GEODE", C["white"], yaw=180, size=7)
    P.sign((0, -452, -110), "BREAKER", C["breaker"], yaw=180, size=6)
    P.sign((420, 110, 96), "MARKET", C["trade"], yaw=60, size=5)
    P.sign((-210, 30, -106), "FOUNDRY", C["foundry"], yaw=90, size=3)
    P.holo((0, 0, 40), C["hearth"], r=14)
    P.holo((0, 0, -308), C["breaker"], r=12)
    P.holo((276, 10, 16), C["trade"], r=9)
    P.holo((-270, 24, -156), C["foundry"], r=8)

    # ------------------------------------------------------------------ ships and gates
    for i, (a, x, y, z) in enumerate(niches):
        if i < 4:
            P.ship((x * 0.95, y * 0.95, z - 6), (a + 180) % 360, ("light", "heavy", "light", "heavy")[i])
    P.ship((290, 50, 26), 200, "heavy", lit=False)
    P.ship((250, -50, 26), 20, "heavy", lit=False)
    P.ship((10, -520, -130), 0, "light")
    P.gate((-150, -1000, -150), 100, facing=180, label="gate 1")
    P.gate((210, -1160, -40), 90, facing=165, label="gate 2")
    P.gate((540, -900, 120), 80, facing=140, lit=False, label="gate 3 (tier 3)")

    # ------------------------------------------------------------------ routes
    P.route("arrival", [(-150, -1000, -150), (-60, -600, 200), (0, -200, 560), (0, 0, 480), (0, 0, 300), (0, 0, 120)], C["arrival"])
    P.route("tows to the breaker", [(-150, -1000, -150), (0, -600, -150), (0, -330, -180), (0, -100, -270), (0, 0, -290)], C["breaker"])
    P.route("to the market", [(0, 0, 40), (200, 10, 50), (276, 10, 40)], C["trade"])
    P.route("to the foundry", [(0, 0, -120), (-200, 20, -126), (-270, 24, -128)], C["foundry"])

    # ------------------------------------------------------------------ points of interest
    P.poi(1, "The Crown", (0, 0, 360), "arrival", "the front door at the top: a collar of docking lights; drop in and the well opens below",
          view=((300, -260, 560), (0, 0, 330)))
    P.poi(2, "The Well", (0, 0, 40), "hearth", "the Hearth: a 560 m shaft; the docket board hangs in the middle, lit from the breaker below",
          view=((70, -96, -40), (-30, 40, 90)))
    P.poi(3, "A niche", niches[3][1:4], "berths", "your berth: one of six cut up the wall in a spiral, each crew's ship nose-in to the board",
          view=((niches[3][1] * 0.3 + 40, niches[3][2] * 0.3 - 30, niches[3][3] + 24), (niches[3][1], niches[3][2], niches[3][3] - 6)))
    P.poi(4, "The Breaker", (0, 0, -290), "breaker", "the well floor: drag a tow up the Throat, let go over the cradle, the arms cut it",
          view=((90, -90, -220), (0, 0, -296)))
    P.poi(5, "The Throat", (0, -330, -180), "breaker", "the tow entrance: wide and straight on purpose, so a load on a cable gets in clean",
          view=((0, -420, -150), (0, -150, -250)))
    P.poi(6, "Market cavern", (276, 10, 48), "trade", "east, with its own mouth: visiting merchants moor among stacked cargo",
          view=((200, -50, 66), (296, 30, 30)))
    P.poi(7, "Foundry", (-270, 24, -128), "foundry", "west and deep, opens at tier 2: salvage becomes stronger materials",
          view=((-196, -14, -118), (-290, 34, -146)))
    P.poi(8, "Gate yard", (0, -1050, -100), "gates", "the mission gates below the south face; friends arrive through gate 1",
          view=((80, -480, 40), (40, -1050, -100)))

    P.level(60, "the Well at the market's height")
    P.level(-120, "the Well at the foundry's height")
    P.level(-270, "the breaker floor")
    P.section("y", 0, "through the Well, east to west")
    P.section("x", 0, "through the Throat, south to north")

    P.notes = dict(
        idea=("A hollow ice-rock moonlet, 720 m across, with a vertical well through its heart. Where the Hollow is a flat ring road "
              "round caverns, the Geode is built for 3D flight: the crew's berths are niches cut up the well wall in a spiral, all "
              "facing the docket board, which hangs in the middle of the shaft. You drop in through the Crown at the top; tows come "
              "in at the bottom through the Throat to the breaker cradle, whose glow lights the well from below. Side tunnels at "
              "their own heights lead to the market and the foundry."),
        plays=["Arrive through gate 1, climb over the south face and drop into the Crown; your niche's lamp is lit in your colour.",
               "Bring a tow along the south face into the Throat: wide and straight, so a load on a cable gets in without a scrape.",
               "Tier 1: two niches, the breaker. Tier 2: four niches, market tunnel, foundry. Tier 3: six niches, second gate, the "
               "Crown's collar of guns. Each tier is dug: fresh pale rock against the old.",
               "From any niche you see every other crew's niche across the well: who's home is visible at a glance."],
        strengths=["The most 3D option: up and down matter everywhere, which 3-axis flight needs to feel worth it.",
                   "Hollow-inspired (a rock you fly into) but different in every way that matters: vertical, yours, quiet.",
                   "Voxel rock throughout, like the Hollow's pipeline (hollow_export.py), so building it reuses that path."],
        costs=["Rock interiors need lighting and a camera that copes when you fly straight up or down a shaft.",
               "A vertical den is harder to read at first; guide strips and the board's light must do the wayfinding."],
        key=[("berths and arrival", C["berths"]), ("the Well (docket board)", C["hearth"]), ("breaker", C["breaker"]),
             ("market", C["trade"]), ("foundry", C["foundry"]), ("gates", C["gates"])],
    )
    return P
