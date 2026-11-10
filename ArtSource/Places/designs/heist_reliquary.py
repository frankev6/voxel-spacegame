"""Heist option A: THE RELIQUARY (Vestari). A vertical spindle station; the treasure is the Pearl, in the vault at its root.

Owner (2026-11-10): "infiltrate a station (new design, not hollow or meridian) and steal a certain piece of treasure that
will make the alarms go off, and then you have to exit within a certain time"; the transponder makes you harder to find,
and once you drag the cargo, "the alarms trip if the cargo hits anything at all".

The Vestari are xenophobic: any foreign transponder in their space is ordered out, so the way in is dark. The spindle is
1 km tall. Its spine is the Nave, a shaft you dive down from the Crown vents to the vault; three choir rings turn round
it at their own rates, hollow inside (the cloisters), joined to the Nave by spokes. The vault is a sealed bulb at the
root with the Pearl on its cradle. Lifting the Pearl seals the Nave's iris below the vault and speeds the rings; the
way out is back up the Nave with the Pearl on a cable, past every rib and chapel eye, or out through a wall you cut.
"""
import math

from place_kit import Place, DISTRICT as C, polar


def design():
    P = Place("heist_reliquary", "Heist A · The Reliquary", "A Vestari spindle 1 km tall: dive the Nave, lift the Pearl, climb out with it on a cable.",
              ((-360, -360, -560), (360, 360, 560)), cell=8.0)

    # ------------------------------------------------------------------ the spindle
    P.cylinder((0, 0, -520), (0, 0, 520), 72, "obsidian")
    for z, r, h in ((-430, 110, 70), (-200, 96, 40), (80, 104, 48), (300, 92, 40), (470, 120, 60)):
        P.cylinder((0, 0, z - h / 2), (0, 0, z + h / 2), r, "obsidian", r2=r * 0.8)
        P.cylinder((0, 0, z + h / 2), (0, 0, z + h / 2 + 8), r * 0.8, "pearl")
    # the vault bulb at the root
    P.ellipsoid((0, 0, -330), (124, 124, 110), "pearl")
    P.ellipsoid((0, 0, -330), (112, 112, 98), "obsidian")
    P.carve_ellipsoid((0, 0, -330), (90, 90, 76))
    # 2 the Nave: the shaft down the spine, open at the Crown
    P.carve_cylinder((0, 0, -250), (0, 0, 560), 40)
    P.box((0, 0, -262), (96, 96, 8), "gold")                               # the iris over the vault (opens on the right chant)
    P.carve_box((0, 0, -262), (48, 48, 8))
    for z in range(-220, 520, 48):
        P.torus((0, 0, z), 40, 4, "pearl")                                  # ribs inside the shaft: what a dragged Pearl hits
    # 3 the choir rings: hollow cloisters, each with a gap, spokes to the Nave
    for z, R, gap in ((-160, 230, 40), (90, 290, 160), (320, 210, 280)):
        P.torus((0, 0, z), R, 28, "pearl", arc=[(gap + 50) % 360, gap])
        P.torus((0, 0, z), R, 26, "obsidian", arc=[(gap + 50) % 360, gap])
        P.torus((0, 0, z), R, 17, None)
        for a in (gap + 120, gap + 240):
            x, y, _ = polar(a, 70)
            X, Y, _ = polar(a, R - 20)
            P.cylinder((x, y, z), (X, Y, z), 22, "obsidian")
            P.carve_cylinder((x * 0.4, y * 0.4, z), (X, Y, z), 14)
    # 4 the chapels: four side rooms off the Nave, each with an eye
    for a in (45, 135, 225, 315):
        x, y, _ = polar(a, 92)
        P.box((x, y, 200), (60, 60, 48), "obsidian", yaw=-a)
        P.carve_box((x, y, 200), (44, 44, 32), yaw=-a)
        P.carve_box((x * 0.6, y * 0.6, 200), (30, 30, 24), yaw=-a)
    # 1 the Crown: vent fins over the shaft's mouth
    for a in range(0, 360, 30):
        x, y, _ = polar(a, 110)
        X, Y, _ = polar(a, 170)
        P.box(((x + X) / 2, (y + Y) / 2, 530), (16, 70, 40), "obsidian", yaw=-a)
    # 6 the pilgrim dock and the Undercroft below the vault
    P.ellipsoid((0, 0, -500), (190, 190, 44), "obsidian")
    P.carve_ellipsoid((0, 0, -500), (170, 170, 28), floor=-520)
    P.carve_cylinder((0, 0, -470), (0, 0, -420), 24)                       # the Undercroft passage, sealed at alarm
    for a in (0, 90, 180, 270):
        x, y, _ = polar(a, 190)
        P.carve_box((x, y, -500), (50, 50, 28), yaw=-a)                    # dock mouths
    # sensor pickets
    for a in (20, 110, 200, 290):
        x, y, _ = polar(a, 330)
        P.ellipsoid((x, y, -40), (12, 12, 12), "obsidian")

    # ------------------------------------------------------------------ light, the Pearl, alarms
    for z in range(-220, 520, 48):
        for a in (0, 120, 240):
            x, y, _ = polar(a + z, 34)
            P.lamp((x, y, z + 20), C["vault"], 0.35)
    for z, R in ((-160, 230), (90, 290), (320, 210)):
        for a in range(0, 360, 40):
            x, y, _ = polar(a, R)
            P.lamp((x, y, z), C["vault"], 0.4)
    P.lamp((0, 0, -330), C["vault"], 2.0)
    P.lamp((0, 0, -290), C["white"], 0.8)
    for a in (45, 135, 225, 315):
        x, y, _ = polar(a, 100)
        P.lamp((x, y, 200), C["danger"], 0.6)
        P.marker((x, y, 200), 70, C["danger"])
    for a in range(0, 360, 45):
        x, y, _ = polar(a, 150)
        P.lamp((x, y, -500), C["vault"], 0.6)
    P.ellipsoid((0, 0, -338), (12, 12, 12), "pearl")                       # the Pearl
    P.box((0, 0, -356), (40, 40, 12), "gold")                               # its cradle
    P.holo((0, 0, -352), C["vault"], r=16)
    P.marker((0, 0, -338), 30, C["vault"])
    for a in (20, 110, 200, 290):
        x, y, _ = polar(a, 330)
        P.marker((x, y, -40), 160, C["danger"])
    P.marker((0, 0, 0), 1400, C["danger"])
    P.sign((0, -128, 466), "VEL", C["vault"], yaw=180, size=10)
    P.strip([(0, 0, 520), (0, 0, -250)], C["vault"])

    # ------------------------------------------------------------------ ships
    for a in (0, 90, 180, 270):
        x, y, _ = polar(a, 120)
        P.ship((x, y, -506), (a + 180) % 360, "light", lit=False)
    for i, a in enumerate((60, 240)):
        x, y, _ = polar(a, 520)
        P.ship((x, y, 120 * (1 - 2 * i)), (a + 90) % 360, "heavy")

    # ------------------------------------------------------------------ routes
    P.route("in: dark, down the Nave", [(800, -900, 900), (200, -200, 620), (0, 0, 560), (0, 0, 0), (0, 0, -250), (0, 0, -330)], C["lime"])
    P.route("out with the Pearl", [(0, 0, -330), (0, 0, 0), (0, 0, 560), (-300, 300, 760), (-1200, 1200, 900)], C["amber"], kind="escape")
    P.route("out through a cut wall", [(0, 0, -330), (130, -60, -330), (600, -500, -400)], C["danger"], kind="escape")
    P.route("patrol", [polar(a, 520, z=0) for a in range(0, 361, 20)], C["danger"], kind="patrol")
    P.route("pilgrims (transponder on)", [(1200, 0, -500), (400, 0, -500), (190, 0, -500)], C["white"])

    # ------------------------------------------------------------------ points of interest
    P.poi(1, "Crown vents", (0, 0, 530), "lime", "the quiet way in: between the fins and down; heat pulses from the vents on a beat",
          view=((260, -300, 700), (0, 0, 500)))
    P.poi(2, "The Nave", (0, 0, 150), "vault", "the shaft down the spine, 80 m across, ribbed every 48 m: the dive in and the climb out",
          view=((0, -30, 330), (0, 10, 40)))
    P.poi(3, "Cloister ring", polar(200, 290, z=90), "white", "inside a choir ring as it turns: a hidden way round; spokes to the Nave",
          view=(polar(170, 290, z=90), polar(215, 290, z=86)))
    P.poi(4, "Chapel eye", polar(45, 92, z=200), "danger", "four side chapels, an eye in each, watching the Nave below them",
          view=((20, -20, 230), polar(45, 92, z=200)))
    P.poi(5, "The Vault", (0, 0, -330), "vault", "the Pearl on its cradle; lift it and the iris below seals, the rings speed up, the clock runs",
          view=((60, -50, -300), (0, 0, -340)))
    P.poi(6, "Pilgrim dock", (0, 0, -500), "white", "the front door for Vestari only; a foreign transponder here is ordered out at once",
          view=((360, -320, -470), (0, 0, -500)))
    P.poi(7, "Sensor picket", polar(200, 330, z=-40), "danger", "four buoys 330 m out: a dark ship inside their reach is seen by its signature",
          view=(polar(190, 520, z=40), polar(200, 330, z=-40)))
    P.poi(8, "Interdiction edge", (0, 0, 0), "danger", "1.4 km: inside it nobody jumps; outside it you can, unless you are towing without a hold",
          inside=False)

    P.level(220, "the chapels")
    P.level(90, "the middle ring")
    P.level(-330, "the vault")
    P.section("y", 0, "the spindle, cut through the Nave")
    P.view("The climb out: the Pearl on a cable up the Nave", (24, -30, -150), (0, 0, 40), fov=60,
           hero=((0, 0, -120), 0, 80))

    P.notes = dict(
        idea=("A Vestari reliquary 1 km tall. The Vestari are xenophobic: any foreign transponder in their space is ordered out, so "
              "the way in is dark. The spine is the Nave, a shaft you dive from the Crown vents to the vault at the root; three choir "
              "rings turn round it at their own rates, hollow inside, joined to the Nave by spokes. The Pearl sits in the vault. "
              "Lifting it seals the iris under the vault and speeds the rings; the way out is back up the Nave with the Pearl on a "
              "cable, past every rib and chapel eye, or out through a wall you cut, which is loud."),
        plays=["Approach dark (transponder off) between the pickets; time the Crown vents' heat pulses; dive the Nave.",
               "The chapel eyes watch the Nave: go through when they look away, or shoot one out (an eye that goes dark is noticed).",
               "Lift the Pearl: the clock starts. Climb the Nave towing it: every rib is 48 m apart; a scrape tells the station where you are.",
               "Or cut out through the vault wall (voxels): faster, loud, and it brings the patrol.",
               "Outside the 1.4 km interdiction edge a ship with a cargo hold can jump; a ship without one drags the Pearl to the gate."],
        strengths=["Vertical from top to bottom: the heist is a dive and a climb, which only works with 3D flight.",
                   "The station's own shape is the challenge: ribs, turning rings, eyes; built from movers, zones and voxels we have.",
                   "Clear story: get in dark, lift, get out careful or fast."],
        costs=["Spinning rings as kinematic movers with hollow interiors are a new size of mover.",
               "One main route (the Nave) needs the side routes (cloisters, cut walls) to keep replays fresh."],
        key=[("way in (dark)", C["lime"]), ("way out with the Pearl", C["amber"]), ("alarm, eyes, pickets, patrol", C["danger"]),
             ("the vault and the Pearl", C["vault"]), ("pilgrims (transponder on)", C["white"])],
    )
    return P
