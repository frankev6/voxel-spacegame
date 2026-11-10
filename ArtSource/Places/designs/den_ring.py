"""Den option C: THE OLD RING. A dead ancient gate, 840 m across, standing on edge; the crew lives in it.

Owner (2026-11-10): "start the mission together by driving to one of the gates", and the den should be "player and
centered, upgradable", with "upgrades that will help you unlock stronger materials, more hangar spaces, etc."

Someone built this gate long before anyone here, and it died; a sector of it broke away and drifts beside it. The crew
has hung their den in its eye: the Knot, a hub held by eight spokes, with the Hearth (a deck round the Knot) where every
crew's berth faces in. Job cards circle the Knot as a ring of light. Inside the ring's body runs the Circuit, a tunnel
round the whole loop (the Hollow's ring road, turned on edge, so flying it is a vertical loop). Rooms are cut into the
ring: the Sump at the bottom (the breaker yard), the trade hall on the left, the foundry on the right, the Lookout at
the top. Progress is the ring itself: every upgrade wakes another arc, its lights crawling round the loop, and when the
loop is whole (tier 3) the old gate opens for the hardest jobs.
"""
import math

from place_kit import Place, DISTRICT as C, polar

R, RT = 420.0, 64.0


def on_ring(b, r=R, y=0.0):
    """A point on the ring's plane (x-z) at bearing b (0 = top, 90 = right)."""
    return (math.sin(math.radians(b)) * r, y, math.cos(math.radians(b)) * r)


def design():
    P = Place("den_ring", "Den C · The Old Ring", "A dead ancient gate on edge: the crew lives in its eye and inside its body.",
              ((-520, -176, -520), (520, 176, 520)), cell=4.0)

    # ------------------------------------------------------------------ the ring, broken at 35..65
    P.torus((0, 0, 0), R, RT, "basalt", axis="y", arc=[65, 35])
    for b in range(70, 390, 30):
        P.torus((0, 0, 0), R, RT + 3, "bone", axis="y", arc=[b % 360, (b + 8) % 360])      # stone bands
    for y in (-62, 62):
        P.torus((0, y, 0), R, 18, "bone", axis="y", arc=[65, 35])                      # rims
        P.torus((0, y * 1.08, 0), R - RT + 6, 8, "basalt", axis="y", arc=[65, 35])
    for b in range(80, 380, 20):
        P.box(on_ring(b % 360, R + RT + 6), (18, 90, 18), "basalt")                     # teeth on the outer face
    # the broken sector, drifting: chunks of ring
    for k, (b, dr, dy, rr) in enumerate(((42, 70, -60, 52), (52, 120, 30, 40), (60, 40, 80, 30), (47, 170, -20, 22))):
        x, _, z = on_ring(b, R + dr)
        P.ellipsoid((x, dy, z), (rr * 1.3, rr, rr * 1.1), "basalt", noise=0.3, scale=20, seed=k + 30)
        P.ellipsoid((x + rr * 0.4, dy - rr * 0.3, z), (rr * 0.5, rr * 0.5, rr * 0.5), "bone", noise=0.2, seed=k + 40)

    # ------------------------------------------------------------------ the Circuit and its openings
    P.torus((0, 0, 0), R, 28, None, axis="y")
    for b in (0, 90, 135, 180, 225, 270, 315):
        a = on_ring(b, R - RT - 8)
        c = on_ring(b, R)
        P.carve_cylinder(a, c, 26)
    # rooms in the ring
    P.carve_ellipsoid(on_ring(180), (104, 92, 76), noise=0.08, seed=5)                  # the Sump (breaker)
    P.carve_ellipsoid(on_ring(180, R + 70), (70, 70, 60), noise=0.1, seed=6)             # its mouth, outside: tows come in
    P.torus(on_ring(180), 46, 6, "armor", axis="z")
    for b in (30, 150, 270):
        x, y, _ = polar(b, 92)
        X, Y, _ = polar(b, 40)
        P.cylinder((x, y, -470), (X, Y, -440), 5, "hull2")
    P.box((0, 0, -440), (40, 30, 24), "rust", yaw=30)
    P.carve_ellipsoid(on_ring(270), (80, 84, 104), noise=0.08, seed=7)                  # the trade hall
    P.carve_ellipsoid(on_ring(90), (70, 76, 94), noise=0.08, seed=8)                    # the foundry
    P.carve_ellipsoid(on_ring(0), (60, 60, 44), noise=0.06, seed=9)                     # the Lookout
    for k, dz in enumerate((-60, -20, 20, 60)):
        P.box((-440, (k - 1.5) * 26, dz), (20, 18, 16), "crate" if k % 2 else "crate2")
    for dz in (-40, 0, 40):
        P.box((450, 30, dz), (18, 18, 26), "plate_dark")

    # ------------------------------------------------------------------ the Knot, the Hearth, the spokes
    P.loft([(-90, 0, 0, 18, 18, 2), (-50, 0, 0, 52, 52, 2.4), (30, 0, 0, 60, 60, 2.4), (80, 0, 0, 34, 34, 2), (110, 0, 0, 12, 12, 2)],
           "hull", axis="z")
    P.loft([(-50, 0, 0, 44, 44, 2.4), (30, 0, 0, 52, 52, 2.4)], None, axis="z", carve=True)          # hollow: the Lantern room
    P.carve_box((0, -50, -6), (40, 30, 40))                                                         # its door
    P.cylinder((0, 0, -6), (0, 0, 0), 150, "deck")                                                   # the Hearth deck
    P.cylinder((0, 0, -6), (0, 0, 0), 66, None)
    P.cylinder((0, 0, -12), (0, 0, -6), 150, "armor")
    P.cylinder((0, 0, -12), (0, 0, -6), 70, None)
    P.torus((0, 0, -3), 150, 5, "trim")
    for b in range(0, 360, 45):
        if 35 <= b <= 65:
            continue
        a = on_ring(b, 70)
        c = on_ring(b, R - RT + 4)
        P.cylinder(a, c, 5, "trim")                                                                  # spokes
        P.cylinder(on_ring(b, R - RT + 4), on_ring(b, R - RT - 14), 12, "armor")                     # anchors
    for i in range(6):
        b = i * 60 + 30
        x, y, _ = polar(b, 120)
        P.box((x, y, 2), (36, 36, 4), "plate_dark", yaw=-b)                                          # berth cradles

    # ------------------------------------------------------------------ light, signs, markers
    lit = list(range(180, 361, 12)) + list(range(0, 35, 12)) + list(range(66, 180, 12))
    for b in lit:
        on = 180 <= b <= 360 or b < 30
        P.lamp(on_ring(b, R - RT - 6), C["vault"] if on else C["amber"], 0.4 if on else 0.15)
    P.strip([on_ring(b, R - RT - 3) for b in range(180, 361, 6)], C["vault"])
    for b in range(70, 180, 30):
        P.lamp(on_ring(b, R), C["white"], 0.3)                                                       # the Circuit, dark arc
    for b in range(180, 361, 30):
        P.lamp(on_ring(b, R), C["vault"], 0.4)
    P.lamp(on_ring(180), C["breaker"], 1.6)
    P.lamp(on_ring(270), C["trade"], 1.4)
    P.lamp(on_ring(90), C["foundry"], 1.4)
    P.lamp(on_ring(0), C["white"], 0.6)
    P.lamp((0, 0, 40), C["hearth"], 1.4)
    P.lamp((0, 0, -30), C["amber"], 0.8)
    for i in range(6):
        x, y, _ = polar(i * 60 + 30, 120)
        P.lamp((x, y, 12), C["berths"] if i < 4 else C["white"], 0.4)
    P.holo((0, 0, 40), C["hearth"], r=72)
    P.holo(on_ring(180, R - 60), C["breaker"], r=12)
    P.holo(on_ring(270, R - 40), C["trade"], r=10)
    P.holo(on_ring(90, R - 40), C["foundry"], r=10)
    P.sign((0, -60, 130), "THE KNOT", C["hearth"], yaw=180, size=6)
    P.sign(on_ring(180, R + 150), "BREAKER", C["breaker"], yaw=180, size=8)
    P.sign((-420, -110, 120), "TRADE", C["trade"], yaw=180, size=6)
    P.sign((420, -100, 110), "FOUNDRY", C["foundry"], yaw=180, size=5)

    # ------------------------------------------------------------------ ships, gates
    for i in range(4):
        b = i * 60 + 30
        x, y, _ = polar(b, 120)
        P.ship((x, y, 10), (b + 180) % 360, ("light", "heavy", "light", "heavy")[i])
    P.ship((-430, 10, -30), 0, "heavy", lit=False)
    P.ship((0, -60, -640), 0, "light")
    P.gate((-260, -900, 120), 100, facing=180, label="gate 1")
    P.gate((240, -1000, -160), 90, facing=170, label="gate 2")

    # ------------------------------------------------------------------ routes
    P.route("arrival", [(-260, -900, 120), (-120, -400, 60), (0, -120, 20), (0, -40, 10)], C["arrival"])
    P.route("tows to the Sump", [(240, -1000, -160), (60, -500, -640), (0, 0, -640), (0, 0, -480)], C["breaker"])
    P.route("the Circuit", [on_ring(b, R) for b in range(70, 400, 15)], C["vault"], kind="lane")

    # ------------------------------------------------------------------ points of interest
    P.poi(1, "The Knot", (0, 0, 0), "hearth", "the hub in the ring's eye, held by spokes; the Lantern room inside, job cards circling it",
          view=((190, -230, 90), (0, 0, 20)))
    P.poi(2, "The Hearth", polar(150, 120, z=6), "berths", "a deck round the Knot: six berths, every crew's ship nose-in, cards overhead",
          view=((150, -60, 40), polar(220, 110, z=6)))
    P.poi(3, "The Circuit", on_ring(140), "vault", "the tunnel inside the ring, all the way round: a vertical loop; the lit arc is yours",
          view=(on_ring(118), on_ring(146)))
    P.poi(4, "The Sump", on_ring(180), "breaker", "the bottom of the ring: tows come in from below, the cradle cuts them, sparks fall away",
          view=((70, -70, -360), (0, 0, -440)))
    P.poi(5, "Trade hall", on_ring(270), "trade", "a cavern in the left arc: merchants moor inside the old stone",
          view=((-340, -60, 40), (-440, 10, -10)))
    P.poi(6, "Foundry", on_ring(90), "foundry", "a cavern in the right arc, opens at tier 2: salvage becomes stronger materials",
          view=((350, -60, 40), (440, 20, -10)))
    P.poi(7, "The break", on_ring(50), "danger", "where a sector of the ring broke away; its chunks drift alongside; the Circuit ends open here",
          view=((560, -380, 520), on_ring(50)))
    P.poi(8, "The Lookout", on_ring(0), "white", "the top of the ring: the den ledger (upgrades), looking straight down through the Knot",
          view=((0, -30, 404), (0, 0, 0)))
    P.poi(9, "Gate yard", (-260, -900, 120), "gates", "two new gates for today's jobs; the Old Ring itself opens when the whole loop is lit",
          view=((0, -380, 160), (-260, -900, 120)))

    P.level(10, "the Hearth and the side halls")
    P.level(-420, "the Sump")
    P.section("y", 0, "the ring face: Circuit, rooms, Knot and spokes")
    P.section("x", 0, "edge-on through the Knot, the Lookout and the Sump")
    P.view("Arrival: the Old Ring, two thirds awake", (700, -1300, 300), (0, 0, 0), fov=40)

    P.notes = dict(
        idea=("A dead ancient gate, 840 m across, standing on edge; a sector broke away long ago and drifts beside it. The crew hangs "
              "their den in its eye: the Knot, a hub held by spokes, ringed by the Hearth deck where every crew's berth faces in, with "
              "the job cards circling the Knot as a ring of light. Inside the ring's body runs the Circuit, a tunnel all the way round "
              "(the Hollow's ring road turned on edge: a vertical loop). Rooms are cut into the stone: the Sump at the bottom (breaker), "
              "trade on the left, the foundry on the right, the Lookout at the top. Every upgrade wakes another arc; when the loop is "
              "whole, the old gate opens for the hardest jobs."),
        plays=["Arrive through gate 1 and fly into the ring's eye; berth on the Hearth facing the Knot; F opens the board.",
               "Tows come up from below into the Sump at the bottom of the ring.",
               "Fly the Circuit to get anywhere in the ring; the lit arc is what you have restored.",
               "Tier 1: a third of the ring lit, two berths, the Sump. Tier 2: two thirds, four berths, trade, foundry. Tier 3: the whole "
               "loop, six berths, and the Old Ring opens as a gate."],
        strengths=["Progress you can read from across the system: how much of the ring is lit.",
                   "A goal built into the place: wake the whole ring and it becomes the best gate you have.",
                   "Hollow-inspired (a ring road, rooms cut in stone) but turned on edge and built round the crew in its eye."],
        costs=["The ring is big (840 m) but thin; the Knot and Hearth are small by comparison, so the den's heart must read from afar.",
               "Lore: whose gate it was (the Aksharan descendants the Vestari claim?) is an owner decision."],
        key=[("berths and arrival", C["berths"]), ("the Knot, the board", C["hearth"]), ("the lit arc, the Circuit", C["vault"]),
             ("breaker", C["breaker"]), ("trade", C["trade"]), ("foundry", C["foundry"]), ("gates", C["gates"])],
    )
    return P
