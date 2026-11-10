"""The den and heist-station options as 2D low-res sketches (sketch2d.py draws them). One cell = one 8 m block.

Owner (2026-11-10):
  "the heist will require us stealing a station core, which are very hard to come by but will power up our den and
   allow us to start upgrading our own station, so this is a high stakes mission"
  "the station core is tethered to the station and as soon as we take it, alarms go off, theres enemies all around, but
   dont see that we're quiet (unless we fck up)"
  "i like your idea that maybe the station is broken and thats our chance, but you made a ship not a station and you
   made it so ripped apart that theres no movement puzzle, and by movement puzzle i mean, making it difficult to get in
   and out (or have to make your way through with other means like explosives."

Sizes that make the puzzles: a light ship is 23 m long and about 12 m wide, so a 3-cell (24 m) duct takes a ship; a
station core is 40 m across (5 cells), so it needs 6 cells. The way in is almost always too small for the way out.
"""
import math

from sketch2d import Sketch, COL


def P(c, deg, r):
    a = math.radians(deg)
    return (c[0] + r * math.cos(a), c[1] + r * math.sin(a))


# ====================================================================================================================
def heist_stack():
    sk = Sketch("heist_a_stack", "Heist", "A", "The Stack",
                "A tall relay station: the core hangs in the Heart drum; a rock strike long ago is the one soft way in.")
    sk.recommended = True
    v = sk.view("section", 100, 64, "Side section through the Stack (x across, height up). 1 cell = 8 m.")
    # the spine: the station's main shaft, dock to top hatch
    v.shell(18, 3, 28, 60, 2)
    v.door((20, 59), (26, 59), "top hatch")
    # dock ring at the bottom
    v.shell(6, 1, 94, 9, 1)
    v.rect(18, 7, 28, 9, "air")
    # guard deck
    v.shell(30, 12, 62, 20, 2)
    v.rect(26, 14, 32, 18, "air")
    # the Heart: armour 3 thick, except a 1-cell wall on the shadow side
    v.shell(30, 24, 77, 41, 3)
    v.rect(74, 27, 76, 37, "air")
    v.rect(26, 28, 33, 36, "air")
    v.shutter((31, 28), (31, 36), "")
    # cooling decks with the control pod
    v.shell(32, 44, 78, 57, 2)
    for x0, x1 in ((34, 40), (43, 56), (59, 76)):
        v.rect(x0, 50, x1, 51, "deck")
    v.shell(64, 51, 76, 56, 1, "hull")
    v.rect(64, 52, 65, 54, "air")
    v.rect(26, 48, 33, 53, "air")
    # two coolant ducts down into the Heart (3 cells: a ship fits, a core does not)
    for x in (40, 56):
        v.rect(x - 1, 38, x + 4, 46, "armor")
        v.rect(x, 38, x + 3, 46, "air")
        v.fan((x + 1.5, 42.5), 1.6)
        v.door((x, 39), (x + 3, 39), "")
    # the eject chute from the Heart floor to a hatch under the station
    pts = [(66, 26), (71, 17), (80, 8), (86, 1)]
    v.line(pts, 8, "armor")
    v.line(pts, 6, "air")
    v.door((83, 2), (89, 4), "eject hatch")
    # the old rock strike: a channel through the cooling decks' upper armour, patched with plate
    ch = [(99, 63), (86, 59), (76, 54)]
    v.line(ch, 6, "wreck")
    v.line(ch, 3, "air")
    v.rect(76, 53, 78, 56, "hull")
    # masts
    v.rect(50, 57, 52, 64, "hull")
    v.rect(42, 57, 43, 62, "hull")
    # the core and its tethers
    c = (53, 32.5)
    v.core(c, 2.5, "")
    for q in ((35, 39), (35, 26), (71, 39), (71, 26)):
        v.tether(c, q)
    v.eye((34, 39), -25, 12, 40)
    v.eye((73, 39), 205, 12, 40)
    v.eye((35, 55), -10, 14, 36)
    v.turret((10, 10), 120)
    v.turret((90, 10), 60)
    v.turret((23, 61), 90)
    for x in (12, 86):
        v.ship((x, 5), 0 if x < 50 else 180)
    v.ship((44, 16), 0)
    v.charge((76.5, 32), "charge")
    v.lamp((47, 47))
    v.lamp((70, 47))
    v.route([(99, 62), (86, 58), (76.5, 54.5), (66, 48), (57.5, 47), (57.5, 43), (57.5, 38), (55, 35)], "in", "in")
    v.nope((57.5, 41.5), "")
    v.route([(51, 31), (64, 27), (71, 17), (80, 8), (88, 0)], "eject", "")
    v.route([(56, 32), (76, 32), (98, 32)], "loud", "")
    v.route([(50, 32.5), (31, 32), (23, 34), (23, 57), (23, 63)], "out", "")
    v.route([(14, 5), (23, 9), (23, 30)], "patrol", "")
    v.label((88, 35), "blow the thin wall", COL["loud"])
    v.label((90, 10), "eject (co-op)", COL["eject"], "mm")
    v.label((10, 48), "cut the shutter,", COL["out"], "mm")
    v.label((10, 45), "run the spine", COL["out"], "mm")
    v.label((66, 40.5), "core 40 m, duct 24 m", COL["loud"], "lm", 11)
    for n, p in ((1, (90, 61)), (2, (46, 53)), (3, (61, 43)), (4, (53, 28)), (5, (70, 58)), (6, (77, 40)), (7, (34, 37)),
                 (8, (75, 15)), (9, (50, 5))):
        v.poi(n, p)

    pl = sk.view("plan", 72, 60, "Plan cut through the Heart (top down). The spine is outside on the west.")
    o = (38, 30)
    pl.disc(*o, 23, "armor")
    pl.disc(*o, 20, "air")
    for a in range(-24, 25):
        for r in (20.5, 21.5):
            x, y = P(o, a, r)
            pl.set(int(x), int(y), "air")
    pl.shell(2, 25, 12, 35, 2)
    pl.rect(10, 27, 18, 33, "air")
    pl.shutter((16.5, 27), (16.5, 33))
    pl.core(o, 2.5, "")
    for a in (45, 135, 225, 315):
        pl.tether(o, P(o, a, 18))
    pl.eye(P(o, 140, 18), -40, 12, 40)
    pl.eye(P(o, 300, 18), 120, 12, 40)
    pl.charge(P(o, 0, 22.2), "")
    pl.label(P(o, 135, 12), "duct above", COL["dim"])
    pl.label(P(o, 45, 12), "duct above", COL["dim"])
    pl.label(P(o, 250, 12), "eject chute below", COL["eject"])
    pl.route([P(o, a, 28) for a in range(0, 361, 20)], "patrol", "")
    pl.route([o, P(o, 0, 33)], "loud", "")
    for n, p in ((4, (o[0] + 4, o[1] - 4)), (6, P(o, 10, 25)), (7, (16.5, 37))):
        pl.poi(n, p)

    sk.note("1-9 on the map", "1 the old strike channel, patched with plate   2 the cooling decks   3 coolant ducts with fans",
            "4 the Heart: the core on four tethers   5 the control pod: the eject lever   6 the thin wall",
            "7 the Heart's shutter   8 the eject chute   9 the dock ring: the response launches here")
    sk.note("The break: our chance",
            "Years ago a rock punched a channel through the cooling decks' armour. It was patched from inside with plate, "
            "not armour: the one soft way in.")
    sk.note("Way in", "Fly the channel dark, cut the patch, cross the cooling decks between the searchlights, drop down a "
            "coolant duct between the fan blades, cut the grate into the Heart.")
    sk.note("The take", "The core hangs on four tethers. Cutting the last one trips the alarm: the Heart's shutter slams, "
            "the station locks down, the response launches from the dock ring and comes up the spine. They know the core "
            "is gone; they don't know where you are.")
    sk.note("Ways out", "The duct you came in by is 24 m, the core is 40 m. So:",
            "Eject (quiet, co-op): a crewmate in the control pod fires the core down the eject chute; you slip back out "
            "the duct and catch it outside.",
            "The thin wall (loud): a charge placed from outside on the way in; one blast and you're out with it, but "
            "everyone now knows where you are.",
            "Cut the shutter and run the spine (loud and long): straight into the response.")
    sk.note("Why it's hard", "The core doesn't fit back the way you came, and the obvious door locks on the alarm. Planning "
            "the exit before you take the core is the job.")
    return sk


# ====================================================================================================================
def heist_wheel():
    sk = Sketch("heist_b_wheel", "Heist", "B", "The Wheel",
                "A turning ring station: the core is in the still hub; doorways come and go with the turn.")
    v = sk.view("plan", 84, 66, "Plan (top down): the wheel turns around the hub. A snapshot: the dead spoke lines up "
                "with the service port now.")
    o = (42, 33)
    v.ring(*o, 23.5, 29.5, "hull")
    v.ring(*o, 25, 28, "air")
    spokes = (30, 90, 150, 210, 270, 330)
    for a in spokes:
        w = 8 if a == 90 else 5
        v.line([P(o, a, 9.5), P(o, a, 25)], w, "armor")
    for a in spokes:
        w = 6 if a == 90 else 3
        v.line([P(o, a, 11), P(o, a, 26)], w, "air")
    v.disc(*o, 11, "armor")
    v.disc(*o, 7.5, "air")
    v.line([P(o, 90, 6), P(o, 90, 11.5)], 6, "air")       # the cargo port, lined up with the cargo spoke
    v.line([P(o, 210, 6), P(o, 210, 11.5)], 3, "air")     # the service port
    v.shutter(P(o, 90 - 9, 9.6), P(o, 90 + 9, 9.6))
    for a in (30, 150, 270, 330):
        q, r_ = P(o, a, 24.5), P(o, a + 90, 2.5)
        v.shutter((q[0] + r_[0] - o[0], q[1] + r_[1] - o[1]), (q[0] - r_[0] + o[0], q[1] - r_[1] + o[1]))
    # the dead spoke: wreckage inside, a breach in the rim
    a = 210
    v.scatter(min(P(o, a, 12)[0], P(o, a, 25)[0]) - 2, min(P(o, a, 12)[1], P(o, a, 25)[1]) - 2,
              max(P(o, a, 12)[0], P(o, a, 25)[0]) + 2, max(P(o, a, 12)[1], P(o, a, 25)[1]) + 2, "wreck", 0.3, 4, only=("air",))
    v.line([P(o, a, 23), P(o, a, 31)], 5, "wreck")
    v.line([P(o, a, 22), P(o, a, 31)], 3, "air")
    v.core(o, 2.5, "")
    for t in (45, 135, 225, 315):
        v.tether(o, P(o, t, 7))
    v.eye(P(o, 120, 6.5), -60, 7, 50)
    for t in (0, 120, 240):
        v.turret(P(o, t, 30.5), t)
    for t in (60, 180, 300):
        v.ship(P(o, t, 33), t + 90)
    v.route([P(o, t, 26.5) for t in range(250, 600, 15)], "patrol", "")
    v.route([P(o, t, 20) for t in range(100, 170, 6)], "spin", "")
    v.route([P(o, 210, 40), P(o, 210, 30), P(o, 210, 20), P(o, 210, 12), P(o, 210, 4)], "in", "in")
    v.route([P(o, 270, 1), P(o, 90, 3), P(o, 90, 27), P(o, 90, 36)], "out", "")
    v.nope(P(o, 210, 16), "")
    v.label(P(o, 90, 38), "restart the wheel, ride the cargo spoke out", COL["out"])
    v.label((60, 18), "the wheel turns", COL["white"])
    v.label(P(o, 228, 18), "24 m", COL["loud"])
    for n, p in ((1, P(o, 210, 32)), (2, P(o, 200, 19)), (3, P(o, 225, 9)), (4, (o[0] + 4, o[1] - 3)), (5, P(o, 90, 18)),
                 (6, (o[0] - 4, o[1] + 3)), (7, P(o, 0, 33))):
        v.poi(n, p)

    s = sk.view("section", 84, 40, "Side section across the hub: the axle (top and bottom) is the one part that doesn't turn.")
    s.shell(33, 5, 51, 35, 3)
    s.rect(36, 8, 48, 32, "air")
    for x0, x1 in ((6, 13), (71, 78)):
        s.shell(x0, 16, x1, 24, 1, "hull")
    for x0, x1 in ((13, 33), (51, 71)):
        s.rect(x0, 18, x1, 22, "armor")
        s.rect(x0, 19, x1, 21, "air")
    s.rect(33, 19, 36, 21, "air")
    c = (42, 20)
    s.core(c, 2.5, "")
    for q in ((37, 30), (47, 30), (37, 10), (47, 10)):
        s.tether(c, q)
    s.charge((42, 33.5), "")
    s.route([c, (42, 34), (42, 40)], "loud", "")
    s.label((54, 37), "blow the endcap: out along the axle", COL["loud"], "lm")
    for x in (9, 75):
        s.ship((x, 28), 0 if x < 40 else 180)
    s.route([(9, 12), (30, 3), (54, 3), (75, 12)], "patrol", "")
    s.poi(8, (36, 34))

    sk.note("1-8 on the map", "1 the breach in the rim   2 the dead spoke: fused open, full of wreckage   3 the hub's service port",
            "4 the core on four tethers   5 the cargo spoke: the only one wide enough for the core   6 the spin control",
            "7 the rim: patrol ships, turrets   8 the axle endcap")
    sk.note("The break: our chance", "An old fire killed one spoke. Its shutters are fused open and it is full of "
            "wreckage; every other spoke is shuttered and watched.")
    sk.note("Way in", "Match the wheel's turn at the rim, slip into the breach, thread the wreckage while the spoke turns "
            "around you, and wait at its inner end for the hub's service port to come round (once a turn).")
    sk.note("The take", "Cut the tethers: the alarm. The wheel brakes to a stop, every spoke shutter slams and the response "
            "fills the ring. The dead spoke has stopped away from the service port: the way you came is gone.")
    sk.note("Ways out", "Restart the wheel from the spin control and ride the turn until the cargo spoke lines up with the "
            "cargo port, then down it and out (quiet, if nobody sees you in the spoke).",
            "Blow an axle endcap and leave along the axis, the patrols' blind spot (loud).",
            "Cut a new port from the hub into a spoke (slow, loud).")
    sk.note("Why it's hard", "The station moves: doorways come and go with the turn, and the alarm changes its shape. Timing, "
            "not just route.")
    return sk


# ====================================================================================================================
def heist_quarry():
    sk = Sketch("heist_c_quarry", "Heist", "C", "The Quarry",
                "A mining station dug into an asteroid: the core cools at the bottom of a pit; the way out is a twisting "
                "tunnel that listens.")
    v = sk.view("section", 100, 64, "Side section through the asteroid and the pit. 1 cell = 8 m.")
    v.disc(46, 32, 31, "rock", noise=0.22, seed=3, sy=0.92)
    v.scatter(10, 6, 80, 60, "rock2", 0.18, 2, only=("rock",))
    # the station on the east face, its dock mouth cut into the rock
    v.shell(74, 18, 94, 46, 2, "hull")
    v.rect(66, 28, 95, 37, "air")
    v.rect(78, 40, 92, 44, "deck")
    v.rect(78, 21, 92, 24, "deck")
    # the pit
    v.disc(28, 27, 8, "air")
    v.rect(22, 18, 34, 20, "deck")
    c = (28, 22.5)
    v.core(c, 2.5, "")
    for q in ((21, 27), (35, 27), (23, 33), (33, 33)):
        v.tether(c, q)
    # the conveyor tunnel: 6 cells, wide enough for the core, but it twists
    tun = [(35, 25), (42, 20), (49, 29), (56, 22), (63, 31), (70, 32)]
    v.line(tun, 6, "air")
    v.fan((49, 29), 2.5)
    v.fan((63, 31), 2.5)
    # the crusher hall and slag chute
    v.disc(54, 42, 5, "air")
    v.line([(52, 38), (49, 31)], 3, "air")
    v.line([(56, 38), (58, 14), (61, 2)], 3, "air")
    # the old drift, cracked open to the surface by a cave-in
    dr = [(17, 62), (20, 54), (26, 48), (23, 41), (27, 35)]
    v.line(dr[:2], 5, "wreck")
    v.line(dr, 3, "air")
    # the thin roof at the pit's west end
    v.charge((18.5, 25), "charge")
    v.ship((82, 32), 180, (190, 170, 120), 9)
    v.ship((90, 8), 90)
    v.ship((92, 54), 270)
    v.eye((22, 33), -60, 10, 50)
    v.eye((66, 36), 200, 12, 40)
    v.turret((95, 47), 45)
    v.turret((95, 17), -45)
    for p in ((44, 24), (58, 27)):
        v.label(p, "))", COL["danger"], "mm", 13)
    v.route([(10, 63), (17, 60), (20, 54), (26, 48), (23, 41), (27, 35), (28, 29)], "in", "in")
    v.nope((23.5, 41), "")
    v.route([(30, 23), (42, 21), (49, 28), (56, 23), (63, 30), (74, 32), (84, 32)], "out", "")
    v.route([(27, 24), (18, 25), (2, 26)], "loud", "")
    v.route([(88, 30), (70, 31), (60, 28), (52, 24)], "patrol", "")
    v.label((6, 22), "blow the thin roof", COL["loud"], "lm")
    v.label((84, 26), "hide it in the ore", COL["out"])
    v.label((30, 42), "drift 24 m", COL["loud"], "lm", 11)
    for n, p in ((1, (13, 58)), (2, (31, 46)), (3, (28, 14)), (4, (45, 16)), (5, (54, 47)), (6, (81, 39)), (7, (15, 30))):
        v.poi(n, p)

    pl = sk.view("plan", 72, 52, "Plan (top down): the tunnel also twists sideways; the drift comes in from the north-west.")
    pl.disc(36, 26, 26, "rock", noise=0.2, seed=8)
    pl.scatter(8, 2, 64, 50, "rock2", 0.18, 6, only=("rock",))
    pl.disc(20, 24, 7, "air")
    pl.line([(26, 24), (32, 32), (40, 22), (48, 30), (56, 26), (66, 26)], 6, "air")
    pl.line([(8, 46), (14, 38), (18, 30)], 3, "air")
    pl.shell(58, 18, 70, 34, 2, "hull")
    pl.rect(56, 23, 71, 30, "air")
    pl.core((20, 24), 2.5, "")
    pl.charge((12, 22), "")
    pl.route([(22, 24), (32, 31), (40, 23), (48, 29), (64, 26)], "out", "")
    pl.route([(4, 50), (8, 46), (14, 38), (18, 30)], "in", "")

    sk.note("1-7 on the map", "1 the cave-in: the old drift cracked open   2 the drift (24 m: a ship, not a core)",
            "3 the pit: the core on four tethers   4 the conveyor tunnel: crushers, ore carts, listening posts",
            "5 the crusher hall and slag chute   6 the dock and the ore freighter   7 the thin roof")
    sk.note("The break: our chance", "A cave-in last season cracked the old mining drift open to the surface. Nobody has "
            "sealed it: it is too narrow for anything but a small ship.")
    sk.note("Way in", "Down the drift in the dark, through the crack into the top of the pit.")
    sk.note("The take", "Cut the tethers: the alarm. The miners' militia floods the tunnels and listens: every knock of the "
            "core on the rock is heard, and they come to the sound.")
    sk.note("Ways out", "Thread the core down the conveyor tunnel without touching rock (it is wide enough, but it twists "
            "in both directions, the crushers cycle and ore carts come the other way), then load it into the ore "
            "freighter at the dock and leave with the ore (quiet).",
            "Blow the thin roof at the pit's west end and go straight out (loud).")
    sk.note("Why it's hard", "The magnet tow is the puzzle: a heavy core swings on its cable, and every touch is noise.")
    return sk



# ====================================================================================================================
# The dens. Every den starts as a bandit hole with an empty core socket. The first stolen core powers it (the tinted
# area) and lets the crew start building; growth slots (dashed) are where they can build next, from what they haul home.
def band(v, a, b, half, m):
    """A straight hull band from a to b, half cells either side (a rotated rectangle)."""
    dx, dy = b[0] - a[0], b[1] - a[1]
    L = math.hypot(dx, dy)
    nx, ny = -dy / L * half, dx / L * half
    v.poly([(a[0] + nx, a[1] + ny), (b[0] + nx, b[1] + ny), (b[0] - nx, b[1] - ny), (a[0] - nx, a[1] - ny)], m)


def den_notes(sk, start, core, grow, move, costs):
    sk.note("Bandit start", start)
    sk.note("The first core", core)
    sk.note("Growing it", grow)
    sk.note("How you move through it", *move)
    sk.note("Costs", costs)


def den_rift():
    sk = Sketch("den_a_rift", "Den", "A", "The Rift, stood up",
                "The broken warship again, tilted on end: the gap between the halves is a canyon you climb.")
    sk.recommended = True
    v = sk.view("section", 100, 70, "Side section along the wreck. 1 cell = 8 m. Tinted: what the first core powers.")
    A0, A1 = (16, 6), (40, 33)          # aft half: stern to fracture
    F0, F1 = (55, 42), (84, 66)         # fore half: fracture to bow
    for a, b in ((A0, A1), (F0, F1)):
        band(v, a, b, 10, "armor")
        band(v, a, b, 8, "air")
    # decks run along each half; their ends open on the fracture faces (the berths)
    for off in (-5, 0, 5):
        for a, b in ((A0, A1), (F0, F1)):
            dx, dy = b[0] - a[0], b[1] - a[1]
            L = math.hypot(dx, dy)
            nx, ny = -dy / L * off, dx / L * off
            v.line([(a[0] + nx + dx * 0.12, a[1] + ny + dy * 0.12), (b[0] + nx - dx * 0.02, b[1] + ny - dy * 0.02)], 1, "deck")
    # bulkheads with doors
    for a, b, ts in ((A0, A1, (0.35, 0.6)), (F0, F1, (0.35, 0.65))):
        for t in ts:
            c = (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)
            dx, dy = b[0] - a[0], b[1] - a[1]
            L = math.hypot(dx, dy)
            band(v, (c[0] - dy / L * 8, c[1] + dx / L * 8), (c[0] + dy / L * 8, c[1] - dx / L * 8), 0.6, "hull")
    # the jagged fracture faces
    v.scatter(34, 26, 46, 40, "wreck", 0.25, 3, only=("armor",))
    v.scatter(50, 36, 62, 50, "wreck", 0.25, 4, only=("armor",))
    # the engine room at the stern: the Cutter, inside
    v.disc(15, 7, 7, "armor")
    v.disc(15, 7, 5, "air")
    v.rect(4, 2, 13, 8, "air")
    v.shutter((6, 2), (6, 8), "")
    # the reactor room: the empty core socket
    v.disc(27, 19, 3.5, "hull")
    v.disc(27, 19, 2.5, "air")
    v.core((27, 19), 2.2, "", dead=True)
    # the Table on cables in the gap
    v.rect(45, 37, 51, 38, "hull")
    v.tether((45, 37.5), (36, 34))
    v.tether((51, 37.5), (59, 44))
    v.lamp((48, 39), COL["cyan"])
    # the Spill tumbling down and away below the gap
    import random as _r
    rng = _r.Random(7)
    for k in range(40):
        t = rng.random()
        c = (50 + 40 * t + rng.uniform(-6, 6), 30 - 26 * t + rng.uniform(-5, 5))
        if math.hypot(c[0] - 62, c[1] - 18) < 4:
            continue
        v.disc(c[0], c[1], rng.uniform(0.6, 1.8), rng.choice(["wreck", "hull", "armor"]))
    for p in ((38, 31), (42, 28)):
        v.ship(p, 140, (255, 179, 71))
    v.ship((12, 7), 0, (255, 106, 42))
    v.lamp((40, 33))
    v.lamp((57, 41))
    v.tint(4, 1, 34, 30)
    v.slot(43, 46, 54, 54, "the span")
    v.slot(64, 44, 80, 60, "the fore decks")
    v.slot(86, 64, 96, 70, "prow")
    v.slot(26, 22, 34, 30, "power")
    v.route([(96, 2), (80, 12), (66, 20), (58, 30), (49, 34), (43, 31)], "home", "home run")
    v.route([(0, 0), (4, 4), (11, 6)], "tow", "")
    v.route([(46, 34), (50, 44), (55, 54), (60, 62)], "fly", "")
    v.label((2, 12), "tow run: in through", COL["tow"], "lm")
    v.label((2, 10), "the stern", COL["tow"], "lm")
    v.label((64, 63), "climb the canyon", COL["fly"], "lm")
    for n, p in ((1, (70, 14)), (2, (47, 41)), (3, (36, 36)), (4, (15, 14)), (5, (24, 22)), (6, (72, 52))):
        v.poi(n, p)

    pl = sk.view("plan", 70, 50, "Plan (top down): the halves overlap; the gap is open to both sides.")
    band(pl, (6, 20), (30, 26), 10, "armor")
    band(pl, (6, 20), (30, 26), 8, "air")
    band(pl, (38, 28), (64, 34), 10, "armor")
    band(pl, (38, 28), (64, 34), 8, "air")
    pl.rect(30, 30, 33, 31, "hull")
    pl.core((14, 22), 2.2, "", dead=True)
    pl.route([(34, 4), (34, 20), (33, 27)], "home", "")
    den_notes(sk,
              "A crew of nobodies in one deck mouth on the aft face: two berths, a hull plate on cables for the Table, the "
              "dead engine room as a hand-cut breaking bay. Everything else dark.",
              "The warship's reactor room (5) is an empty socket. Plug in a stolen core and the aft half wakes: lights, "
              "shutters, the Cutter's arms, berth locks. Building starts.",
              "Growth slots are the wreck's own spaces: the fore half's dark decks, a span across the gap, the prow, the "
              "engine bells. What goes in each depends on what you tow home.",
              ["Home run: up the tumbling Spill from below and in at the bottom of the gap.",
               "The canyon: the gap climbs diagonally between both deck faces; berths are deck mouths on either side.",
               "Tow run: into the stern; hauls are cut inside the engine room, out of sight."],
              "A big wreck: lots of dark space to fill, so early stages can feel empty.")
    return sk


def den_relay():
    sk = Sketch("den_b_relay", "Den", "B", "The Dead Relay",
                "A small station whose core was stolen years ago: the empty socket is the whole story.")
    v = sk.view("section", 100, 64, "Side section through the hub and spine. 1 cell = 8 m. Tinted: what the first core powers.")
    o = (50, 32)
    # the spine: a truss shaft from top to bottom, flown inside
    v.shell(46, 2, 54, 62, 1, "hull")
    for y in range(6, 60, 6):
        v.rect(46, y, 47, y + 1, "air")
        v.rect(53, y, 54, y + 1, "air")
    # the hub sphere, glass dome on top (the Table)
    v.disc(*o, 10, "armor")
    v.disc(*o, 8, "air")
    v.disc(50, 41, 4, "glass")
    v.disc(50, 41, 3, "air")
    v.rect(47, 38, 54, 40, "air")
    # one arm intact (west), the other broken off (east)
    v.shell(18, 28, 42, 36, 2)
    v.shell(8, 25, 20, 39, 2)
    v.rect(8, 28, 10, 36, "air")
    v.door((8.5, 28), (8.5, 36), "")
    v.rect(40, 30, 42, 34, "air")
    v.shell(58, 29, 70, 35, 2)
    v.rect(58, 31, 60, 33, "air")
    v.scatter(66, 27, 74, 37, "wreck", 0.5, 5, only=("armor",))
    v.rect(69, 30, 73, 34, "space")
    # the cradle bay in the lower spine: the Cutter, enclosed
    v.shell(38, 6, 62, 18, 2)
    v.rect(46, 8, 54, 16, "air")
    v.shutter((38.5, 8), (38.5, 16), "")
    v.rect(36, 8, 40, 16, "air")
    v.core(o, 2.2, "", dead=True)
    v.lamp((50, 41), COL["cyan"])
    for x in (13, 16):
        v.ship((x, 32), 0, (255, 179, 71))
    v.ship((50, 11), 0, (255, 106, 42))
    v.tint(8, 6, 62, 42)
    for y0 in (44, 52):
        v.slot(55, y0, 66, y0 + 6, "")
        v.slot(34, y0, 45, y0 + 6, "")
    v.slot(70, 27, 84, 37, "east arm")
    v.slot(55, 2, 70, 6, "")
    v.label((61, 61), "the spine grows: modules bolt on", COL["slot"], "lm")
    v.route([(0, 40), (6, 33), (12, 32)], "home", "home run")
    v.route([(20, 2), (30, 10), (36, 12), (48, 12)], "tow", "")
    v.route([(50, 26), (50, 18), (50, 4)], "fly", "")
    v.route([(50, 44), (50, 60)], "fly", "")
    v.label((22, 7), "tow run", COL["tow"], "mm")
    for n, p in ((1, (6, 42)), (2, (50, 46)), (3, (54, 28)), (4, (62, 12)), (5, (78, 41)), (6, (57, 58))):
        v.poi(n, p)

    pl = sk.view("plan", 70, 50, "Plan (top down): the hub, its one working arm, and the stubs where modules attach.")
    pl.disc(35, 25, 10, "armor")
    pl.disc(35, 25, 8, "air")
    pl.shell(5, 21, 27, 29, 2)
    pl.rect(25, 23, 28, 27, "air")
    for a in (90, 270):
        x, y = P((35, 25), a, 13)
        pl.shell(x - 3, y - 3, x + 3, y + 3, 1, "hull")
        pl.slot(x - 6, y - 6 if a == 90 else y - 9, x + 6, y + 9 if a == 90 else y + 6, "")
    pl.shell(43, 23, 52, 27, 1)
    pl.slot(52, 19, 66, 31, "")
    pl.core((35, 25), 2.2, "", dead=True)
    den_notes(sk,
              "One working arm with two berths (1), a hub with a glass dome where the Table sits (2), a cradle bay in the "
              "lower spine. The east arm is a broken stub.",
              "The hub's socket (3) was emptied long ago; scorch marks round it. Plug in a stolen core: the hub, the arm "
              "and the cradle bay (4) wake.",
              "The relay was built to be extended: the spine (6) and the stubs take modules. A towed-home container "
              "becomes a hold, a wreck section an arm, a captured gun a battery.",
              ["Home run: straight into the arm's hangar door.",
               "The spine: a shaft through the whole station, flown up and down inside; it grows longer as you build.",
               "Tow run: into the cradle bay under the hub, behind a shutter."],
              "Small and tidy: less spectacle at the start; the majesty comes only from what the crew builds.")
    return sk


def den_rock():
    sk = Sketch("den_c_rock", "Den", "C", "The Hollow Rock",
                "A hollowed asteroid behind a crack only locals can find: the den grows by digging.")
    v = sk.view("section", 100, 64, "Side section through the rock. 1 cell = 8 m. Tinted: what the first core powers.")
    v.disc(50, 32, 30, "rock", noise=0.2, seed=11, sy=0.95)
    v.scatter(14, 4, 86, 62, "rock2", 0.2, 3, only=("rock",))
    # the crack: a winding way in from the top
    crack = [(78, 60), (72, 54), (76, 47), (68, 42), (62, 40)]
    v.line(crack, 3, "air")
    # the main cavern with ledges
    v.disc(48, 34, 12, "air", noise=0.15, seed=2, sy=0.75)
    for x0, x1, y in ((36, 42, 30), (54, 61, 33), (40, 46, 39)):
        v.rect(x0, y - 1, x1, y, "deck")
    # the haul tunnel from the bottom face to the Cutter cave
    v.line([(56, 2), (54, 10), (46, 16)], 6, "air")
    v.disc(42, 17, 6, "air", noise=0.1, seed=4)
    v.line([(44, 22), (46, 26)], 3, "air")
    v.shutter((52, 6), (59, 7), "")
    # the well: the empty socket below the cavern floor
    v.disc(30, 22, 4, "air")
    v.line([(36, 27), (32, 24)], 3, "air")
    v.core((30, 22), 2.2, "", dead=True)
    v.rect(46, 37, 51, 38, "hull")
    v.tether((46, 37.5), (42, 45))
    v.tether((51, 37.5), (54, 45))
    v.lamp((48, 39), COL["cyan"])
    for x0, y in ((38, 31), (56, 34), (42, 40)):
        v.ship((x0 + 2, y + 1), 0 if x0 < 50 else 180, (255, 179, 71))
    v.ship((42, 17), 90, (255, 106, 42))
    v.lamp((70, 47), COL["danger"])
    v.tint(24, 10, 62, 47)
    v.slot(62, 20, 76, 30, "dig")
    v.slot(22, 36, 34, 48, "dig")
    v.slot(36, 50, 54, 58, "dig")
    v.slot(80, 8, 96, 22, "build out")
    v.route([(92, 63), (78, 60), (72, 54), (76, 47), (68, 42), (60, 38), (52, 36)], "home", "home run")
    v.route([(60, 0), (56, 4), (54, 10), (44, 17)], "tow", "")
    v.label((64, 2), "tow run", COL["tow"], "lm")
    for n, p in ((1, (82, 52)), (2, (48, 30)), (3, (48, 43)), (4, (36, 12)), (5, (24, 26)), (6, (69, 25))):
        v.poi(n, p)

    pl = sk.view("plan", 70, 50, "Plan (top down): the cavern and its side caves; the crack bends twice.")
    pl.disc(35, 25, 22, "rock", noise=0.2, seed=6)
    pl.scatter(10, 2, 60, 48, "rock2", 0.2, 8, only=("rock",))
    pl.disc(34, 25, 9, "air", noise=0.15, seed=1)
    pl.line([(54, 42), (48, 36), (50, 30), (42, 28)], 3, "air")
    pl.disc(24, 18, 4, "air")
    pl.core((24, 18), 2.0, "", dead=True)
    pl.slot(42, 10, 54, 20, "")
    pl.slot(14, 28, 24, 38, "")
    den_notes(sk,
              "Bandits in a cave: a crack you have to know (1), a cavern with ledges for three ships (2), the Table hung "
              "from the roof (3), a side cave for cutting hauls (4).",
              "The well (5) under the cavern floor takes the core. It lights the cavern, opens the haul tunnel's shutter "
              "and powers the cutting cave.",
              "Everything is voxel rock: the crew digs new caves where they choose (dashed), widens the crack into a gate, "
              "and builds out onto the surface.",
              ["Home run: down the winding crack, a real piloting test; strangers miss it.",
               "The cavern: ledges at different heights, flown between.",
               "Tow run: up a wide haul tunnel from the far side of the rock into the cutting cave."],
              "Closest to the Hollow in look; must still feel like ours, not a small Hollow.")
    return sk



# ====================================================================================================================
# Round 2 (2026-11-10). Owner: "in my head its supposed to be a bustling station (like the hollow in how busy its is
# and crowded, and commerce) and you could potentially with a trading ship get in the station that way, or you could
# keep transponders of and find a gap through the bottom, or explode your way through (AND OTHER CHOISES TOO ...) its
# more like a hitman map with multitude of options and ideas of how you could go about it, but what you've done so far
# is just a box with a big shiny ball in it and its whole purpose is to have the ball in it, instead of feeling like an
# actual station." and "get more creative and make a proper lived in feeling world/scenario please".
LIV = {"tlax": (91, 227, 161), "vest": (200, 220, 240), "khu": (255, 122, 47), "scav": (200, 170, 90), "civ": (190, 190, 180),
       "tug": (242, 183, 5), "you": (184, 156, 255)}
STALL = [(255, 46, 136), (255, 174, 74), (46, 196, 182), (255, 241, 214)]


def heist_lantern():
    import random
    rng = random.Random(31)
    sk = Sketch("heist_lantern", "Heist", "·", "Tollhouse Nine, \"the Lantern\"",
                "An Alliance toll town where three gate lanes meet. Its core hangs in plain sight at the foot of the market: "
                "the town calls it the Lantern.")
    v = sk.view("section", 108, 126, "Side section through the town (x across, height up). 1 cell = 8 m; the town is about 1 km tall.")

    # ------------------------------------------------------------------ Undertown: the old hand-built quarter
    v.disc(46, 18, 36, "shanty", noise=0.10, seed=5, sy=0.36)
    v.scatter(8, 3, 86, 34, "shanty2", 0.32, 4, only=("shanty",))
    # ------------------------------------------------------------------ the structural trunk under the Powerhouse
    v.rect(40, 30, 64, 50, "armor")
    v.rect(47, 6, 57, 32, "armor")
    # ------------------------------------------------------------------ freight docks (west) and the Yards (east)
    v.rect(4, 30, 44, 33, "deck")
    v.rect(6, 46, 44, 48, "hull")
    for x in (12, 22, 32):
        v.rect(x, 33, x + 1, 46, "truss")
    v.rect(62, 30, 100, 32, "deck")
    v.rect(68, 34, 96, 35, "truss")
    v.rect(68, 35, 80, 43, "hull")
    v.rect(70, 37, 80, 41, "air")
    for x in range(82, 96, 3):
        v.rect(x, 35, x + 1, 43, "truss")
    v.rect(72, 46, 107, 48, "armor")                 # the mass driver
    for x in range(76, 106, 5):
        v.rect(x, 45, x + 2, 49, "hull")
    for c, r in (((89, 22), 4), ((98, 22), 4), ((93, 13), 3.5)):   # the fuel depot
        v.disc(c[0], c[1], r, "tank")
    v.rect(89, 26, 90, 30, "truss")
    v.rect(98, 26, 99, 30, "truss")
    v.rect(92, 16, 94, 19, "truss")
    # ------------------------------------------------------------------ the Powerhouse and the Cradle
    v.rect(26, 48, 82, 62, "armor")
    v.rect(28, 50, 44, 60, "air")                    # turbine hall
    v.rect(46, 50, 58, 61, "air")                    # the Cradle
    v.rect(44, 52, 46, 55, "air")                    # staff door: a ship fits, the core does not
    v.rect(58, 50, 82, 57, "air")                    # the core road to the Yards (the way the core came in)
    v.rect(64, 58, 78, 61, "air")                    # engineers' room
    v.rect(44, 61, 60, 62, "truss")                  # the Grate
    v.rect(49, 61, 55, 62, "hull")                   # its lift-out panel
    # ------------------------------------------------------------------ the outfall: coolant trunk down through Undertown
    v.line([(49, 26), (36, 20), (28, 21)], 3, "air")
    v.line([(55, 20), (66, 14), (74, 15)], 3, "air")
    # Undertown's alleys and squares
    v.line([(12, 21), (24, 23), (36, 26), (45, 28)], 3, "air")
    v.line([(59, 27), (68, 24), (79, 20)], 3, "air")
    v.line([(30, 9), (31, 20), (28, 28)], 2, "air")
    v.line([(63, 8), (64, 18), (70, 24)], 2, "air")
    v.disc(30, 21, 3.4, "air")
    v.disc(68, 22, 3.2, "air")
    v.ring(40, 12, 2.5, 4.5, "armor")                # the old gate-anchor winch
    v.rect(49, 3, 55, 50, "air")                     # the trunk itself
    # the Wound: a runaway ore barge tore open the lower west side three weeks ago; under repair
    v.disc(17, 14, 9, "space", noise=0.35, seed=9)
    v.scatter(4, 3, 32, 28, "wreck", 0.4, 2, only=("shanty", "shanty2"))
    v.line([(9, 6), (27, 24)], 1, "truss")
    v.line([(7, 20), (25, 7)], 1, "truss")
    v.line([(8, 14), (27, 14)], 1, "truss")
    v.rect(21, 22, 28, 23, "hull")
    for x in (24, 36, 62, 72):
        v.rect(x, 0, x + 1, 5, "hull")               # radiators
    # ------------------------------------------------------------------ the market: galleries round the Well
    v.rect(22, 62, 44, 106, "hull")
    v.rect(60, 62, 84, 98, "hull")
    for z0 in (65, 73, 81, 89, 97):
        v.rect(24, z0, 44, z0 + 5, "air")
        if z0 < 96:
            v.rect(60, z0, 82, z0 + 5, "air")
    v.rect(44, 62, 60, 126, "space")
    v.rect(44, 78, 60, 79, "deck")                   # the Stall Bridge
    v.rect(44, 94, 60, 95, "truss")
    for z in (67, 75, 83, 91, 99):
        v.set(22, z, "glass")
        v.set(22, z + 1, "glass")
        if z < 96:
            v.set(83, z, "glass")
            v.set(83, z + 1, "glass")
    # ------------------------------------------------------------------ the Crown: garrison, customs house, bank
    v.rect(10, 106, 44, 120, "ivory")
    v.rect(10, 112, 44, 113, "jade")
    v.rect(10, 108, 26, 112, "air")                  # garrison hangar, open to the west
    v.rect(29, 108, 40, 111, "air")                  # the bank
    v.rect(14, 114, 40, 118, "air")                  # the customs house
    v.rect(38, 120, 42, 125, "ivory")
    v.rect(38, 124, 42, 125, "jade")
    # ------------------------------------------------------------------ Pilots' Rest: bar, chapel, clinic
    v.rect(60, 98, 88, 100, "deck")
    v.disc(72, 104, 5, "glass")
    v.disc(72, 104, 4, "air")
    v.rect(66, 100, 78, 101, "deck")
    v.rect(81, 100, 85, 108, "ivory")
    v.rect(82, 101, 84, 106, "air")
    v.rect(82.5, 108, 83.5, 114, "ivory")
    v.rect(60, 100, 65, 104, "shanty2")
    v.rect(61, 101, 64, 103, "air")

    # ------------------------------------------------------------------ the core and its tethers (the town's power lines)
    core = (52, 55.5)
    amber = (255, 214, 140)
    for path in ([core, (45, 62), (43, 104), (30, 110)], [core, (59, 62), (61, 96), (70, 101)], [core, (36, 56), (20, 47)],
                 [core, (70, 54), (88, 44)], [core, (50, 46), (41, 14)]):
        for a_, b_ in zip(path, path[1:]):
            v.tether(a_, b_, amber)
    v.core(core, 2.5, "")
    v.fan((34, 55), 3)
    v.fan((52, 38), 2.8)
    v.shutter((81, 50), (81, 57), "")
    v.door((45, 52), (45, 55), "")
    v.bar((44, 113.5), (60, 113.5), COL["lane"], "customs scan")
    for z, x0, x1 in ((86, 44, 60), (70, 44, 60), (102, 44, 60)):
        v.tether((x0, z), (x1, z + 3), (255, 190, 120))
        for k in range(1, 8):
            v.dot((x0 + (x1 - x0) * k / 8, z + 3 * k / 8 - 0.6), STALL[k % 4], 1.5)
    # stalls: a light every other cell on every gallery level
    for z0 in (65, 73, 81, 89, 97):
        for x in range(25, 43, 2):
            v.dot((x + 0.5, z0 + 1.2), STALL[(x + z0) % 4], 1.6)
        if z0 < 96:
            for x in range(61, 82, 2):
                v.dot((x + 0.5, z0 + 1.2), STALL[(x + z0 + 1) % 4], 1.6)
    for _ in range(60):                               # Undertown's windows
        x, z = rng.uniform(10, 82), rng.uniform(6, 30)
        if v.g[int(z)][int(x)] in ("shanty", "shanty2"):
            v.dot((x, z), (255, 190, 120), 1.2)
    # searchlights, guns
    v.eye((12, 121), 200, 16, 30)
    v.eye((40, 125.5), 20, 18, 26)
    v.eye((26, 27), 235, 13, 44)
    v.turret((11, 121), 150)
    v.turret((43, 121), 60)
    v.turret((87, 100.5), 20)
    v.turret((5, 49), 160)
    v.turret((101, 32.5), -30)
    # traffic: the Well, the customs queue, the docks, the lanes, the garrison
    for p, h, l, sz in (((50, 120), 270, "civ", 2.4), ((53, 124), 270, "vest", 3), ((51, 116), 270, "scav", 2.2),
                        ((48, 108), 250, "civ", 2), ((56, 104), 90, "tlax", 2.2), ((50, 99), 200, "civ", 1.8), ((55, 90), 80, "khu", 2.2),
                        ((47, 88), 270, "vest", 2), ((53, 84), 100, "civ", 2), ((49, 74), 60, "scav", 2), ((56, 70), 250, "civ", 1.8),
                        ((47, 66), 20, "tlax", 2), ((57, 82), 270, "civ", 1.6), ((46, 97), 0, "civ", 1.6)):
        v.ship(p, h, LIV[l], sz)
    for z0, xs in ((65, (28, 36)), (73, (31, 40, 66, 76)), (81, (27, 38, 70)), (89, (33, 64, 78)), (97, (29, 39))):
        for x in xs:
            v.ship((x, z0 + 3.2), 0 if x < 52 else 180, LIV[rng.choice(["civ", "civ", "scav", "vest", "khu"])], 1.9)
    for x, l in ((14, "civ"), (27, "tlax"), (37, "civ")):
        v.ship((x + 2, 39), 0, LIV[l], 7 if x < 30 else 4)
    v.ship((1, 41), 0, LIV["civ"], 5)
    v.ship((66, 39), 0, LIV["tug"], 2.4)
    for p, h in (((15, 110), 180), ((21, 110), 180), ((96, 118), 200), ((103, 78), 270), ((2, 70), 90), ((30, 124), 0)):
        v.ship(p, h, LIV["tlax"], 3.4)
    for p, h in (((13, 17), 40), ((21, 10), 120), ((24, 19), 200)):
        v.ship(p, h, LIV["tug"], 2.2)
    v.ship((70, 103.5), 0, LIV["civ"], 1.8)
    v.ship((74, 103.5), 180, LIV["scav"], 1.8)
    for p, h, l in (((92, 96), 150, "civ"), ((100, 104), 150, "vest"), ((96, 60), 200, "civ"), ((6, 90), 330, "civ")):
        v.ship(p, h, LIV[l], 2.2)
    # routes: only the main lines; the rest is in the notes
    v.route([(52, 126), (52, 115)], "lane", "")
    v.route([(0, 37), (9, 37)], "lane", "")
    v.route([(0, 5), (10, 11), (19, 17), (28, 21), (36, 20), (48.5, 26), (50.5, 30), (50.5, 49)], "in", "")
    v.route([(54, 59), (55, 66), (55, 100), (56, 124)], "out", "")
    v.route([(56, 54), (78, 54), (86, 50), (90, 47), (107, 47)], "eject", "")
    v.route([(53.5, 50), (53.5, 8), (56, 0)], "loud", "")
    v.route([(98, 116), (104, 86), (102, 56)], "patrol", "")
    v.charge((93, 22), "")
    # names on the map
    T, D = COL["text"], COL["dim"]
    for p, t, c, a in (((27, 122), "THE CROWN", T, "mm"), ((6, 106), "garrison", D, "mm"), ((34.5, 109.5), "bank", T, "mm"),
                       ((27, 116), "customs house", T, "mm"), ((52, 106), "THE WELL", T, "mm"), ((33, 104), "market galleries", D, "mm"),
                       ((73, 111), "Pilots' Rest", T, "mm"), ((52, 80.5), "Stall Bridge", D, "mm"), ((52, 63.3), "the Grate", D, "mm"),
                       ((52, 51), "THE LANTERN", COL["core"], "mm"), ((36, 61), "turbine hall", D, "mm"), ((70, 49), "the core road", D, "mm"),
                       ((22, 49.5), "FREIGHT DOCKS", T, "mm"), ((84, 33.5), "THE YARDS", T, "mm"), ((95, 51), "mass driver", D, "mm"),
                       ((103, 14), "fuel", D, "mm"), ((60, 4), "UNDERTOWN", T, "lm"), ((15, 3), "the Wound", COL["loud"], "mm"),
                       ((58, 40), "outfall", D, "lm"), ((40, 7), "old winch", D, "mm"), ((100, 122), "to the gates", COL["lane"], "mm"),
                       ((71, 59.5), "engineers", D, "mm")):
        v.label(p, t, c, a, 12)
    for n, p in ((1, (63, 115)), (2, (52, 110)), (3, (6, 116)), (4, (90, 106)), (5, (41, 57)), (6, (30, 63)), (7, (9, 43)), (8, (100, 38)),
                 (9, (104, 51)), (10, (104, 20)), (11, (76, 9)), (12, (6, 14)), (13, (46, 2)), (14, (84, 59))):
        v.poi(n, p)

    # ------------------------------------------------------------------ plan: the market level
    pl = sk.view("plan", 76, 60, "Plan at the market (top down): the Well, with the Lantern glowing up through the Grate.")
    o = (38, 30)
    pl.disc(*o, 19, "hull", noise=0.05, seed=3)
    pl.ring(*o, 9.5, 16.5, "air")
    pl.disc(*o, 8, "space")
    for a in range(0, 360, 45):
        pl.line([P(o, a, 7), P(o, a, 11)], 2.6, "air")
    for a in range(22, 360, 45):
        pl.line([P(o, a, 12), P(o, a, 17)], 1, "deck")
    pl.rect(1, 20, 17, 40, "ivory")
    pl.rect(1, 29, 17, 30, "jade")
    pl.rect(3, 22, 12, 28, "air")
    pl.rect(3, 32, 14, 38, "air")
    pl.disc(64, 34, 6, "glass")
    pl.disc(64, 34, 5, "air")
    pl.rect(60, 20, 66, 26, "ivory")
    pl.line([P(o, 30, 8), P(o, 210, 8)], 1, "deck")
    pl.core(o, 1.8, "")
    for a in range(0, 360, 12):
        pl.dot(P(o, a, 10.6), STALL[(a // 12) % 4], 1.6)
        pl.dot(P(o, a + 6, 15.6), STALL[(a // 12 + 1) % 4], 1.6)
    for a, r_, l in ((20, 4, "civ"), (95, 5, "vest"), (170, 3, "scav"), (230, 6, "civ"), (300, 4, "tlax"), (350, 6.5, "khu"),
                     (60, 13, "civ"), (140, 13, "civ"), (200, 13.5, "scav"), (255, 13, "civ"), (320, 13, "vest"), (100, 13.5, "civ")):
        pl.ship(P(o, a, r_), a + 100, LIV[l], 2)
    for p in ((6, 25), (9, 25)):
        pl.ship(p, 180, LIV["tlax"], 3)
    pl.ship((70, 50), 120, LIV["tlax"], 3)
    pl.route([P(o, a, 22.5) for a in range(0, 361, 20)], "patrol", "")
    for p, t in (((9, 43), "the Crown"), ((64, 43), "Pilots' Rest"), ((38, 53), "market galleries")):
        pl.label(p, t, COL["dim"], "mm", 12)

    # ------------------------------------------------------------------ plan: the Lantern and the docks
    p2 = sk.view("plan", 100, 58, "Plan at the Lantern (top down): the Powerhouse between the freight docks and the Yards.")
    p2.rect(24, 18, 78, 40, "armor")
    p2.rect(26, 20, 42, 38, "air")
    p2.rect(44, 23, 56, 35, "air")
    p2.rect(42, 27, 44, 30, "air")
    p2.rect(56, 25, 78, 32, "air")
    p2.rect(58, 34, 74, 38, "air")
    p2.rect(2, 27, 24, 31, "deck")
    for x in (5, 11, 17):
        p2.rect(x, 12, x + 1, 27, "truss")
        p2.rect(x, 31, x + 1, 46, "truss")
    p2.rect(82, 20, 98, 36, "truss")
    p2.rect(83, 22, 91, 34, "hull")
    p2.rect(85, 24, 91, 32, "air")
    p2.line([(74, 44), (98, 55)], 2.4, "armor")
    for c, r in (((90, 8), 4), ((97, 13), 3)):
        p2.disc(c[0], c[1], r, "tank")
    cc = (50, 29)
    for q in ((33, 29), (50, 46), (50, 12), (70, 28.5), (36, 44)):
        p2.tether(cc, q, amber)
    p2.core(cc, 2.5, "")
    p2.fan((34, 29), 3)
    p2.shutter((77.5, 25), (77.5, 32), "")
    p2.door((43, 27), (43, 30), "")
    for p, h, sz, l in (((8, 19), 90, 6, "civ"), ((14, 21), 90, 5, "tlax"), ((8, 39), 270, 6, "civ"), ((20, 37), 270, 4, "scav"),
                        ((80, 28.5), 0, 2.4, "tug"), ((66, 36), 0, 2, "tlax")):
        p2.ship(p, h, LIV[l], sz)
    p2.route([(54, 29), (76, 28.5), (80, 40), (86, 49), (99, 55.5)], "eject", "")
    p2.charge((90, 8), "")
    for p, t in (((34, 41.5), "turbine hall"), ((50, 21.5), "the Cradle"), ((67, 23.5), "the core road"), ((12, 49), "freight docks"),
                 ((90, 38), "the Yards"), ((88, 54), "mass driver"), ((66, 39.5), "engineers")):
        p2.label(p, t, COL["dim"], "mm", 12)

    # ------------------------------------------------------------------ the notes: a place first, then the job
    sk.note("The place",
            "Tollhouse Nine sits where three gate lanes cross. Every hauler pays the Alliance toll here, and most stay a night: "
            "to sell, refuel, repair, eat and sleep. Around the toll a town of a few thousand grew: stall-keepers, dock hands, "
            "tug pilots, fixers, a garrison that is bored most days.",
            "Its core hangs in the Cradle at the foot of the market, under an iron grate. It lights the Well from below; its "
            "tethers are the power lines to every district. People check the time by its pulse. Nobody thinks of it as "
            "something that can be stolen.")
    sk.note("1-14 on the map",
            "1 customs arch: every ship is scanned and tolled   2 the Well: the market's open shaft, six gallery levels of stalls",
            "3 the Crown: garrison cutters, customs house, the bank   4 Pilots' Rest: the bar, the chapel, the clinic",
            "5 the Lantern in its Cradle   6 the Grate, with a lift-out panel   7 freight docks   8 the Yards: a hauler half built",
            "9 the mass driver: ore out to the refineries   10 the fuel depot   11 Undertown: the old quarter, the fixers",
            "12 the Wound: a runaway barge hit three weeks ago; scaffolds and repair tugs   13 the coolant outfall   14 the big door")
    sk.note("Who you are: the transponder is the disguise",
            "Trader (transponder on): the lanes, the market, the docks, Pilots' Rest. Nobody looks twice.",
            "Work code (repair tug, dock hand): service doors, the Wound, the turbine hall. Bought, cloned or earned.",
            "Garrison code: the big door, the Crown. Stolen or bribed.",
            "Dark: no questions at the arch, but anyone who sees a dark ship in town reports it.")
    sk.note("Ways in",
            "Sell something: queue at the arch as a trader, berth at a stall and look around first.",
            "The Wound: dark, through the torn hull between repair shifts and up Undertown's alleys.",
            "Take the job: the repair guild hires by the day. A work code and a reason to carry a tow cable.",
            "In the hold: a crewmate's hauler docks to load freight, with you inside it.",
            "The outfall: up the coolant trunk between flushes, straight to the Cradle floor.")
    sk.note("Getting at the Lantern",
            "The staff door from the turbine hall (work code). A ship fits; the core does not.",
            "The Grate's lift-out panel: the yard crane opens it for servicing; so can you, in front of the whole market.",
            "The big door on the core road (garrison code, the night engineer, or a charge).",
            "Cut the tethers one at a time: each one blacks out a district, and the garrison sends engineers, not guns.")
    sk.note("Cover: what the town gives you",
            "Lantern Night: once a month the town dims its lights to watch the core. Crowds, fireworks, a garrison on crowd duty.",
            "Shift change at the docks; the flush cycle of the outfall; the toll queue at the evening rush.",
            "The fuel depot: one charge and every cutter in town goes east.",
            "The fixer by the old winch sells patrol times, a cloned code, a forged manifest. The night engineer hates his posting.")
    sk.note("Getting it out",
            "The last tether trips the alarm and the whole town goes dark. Then:",
            "In the crowd: a thousand ships run for the lanes in the blackout. Yours has the core in its hold, transponder on.",
            "The mass driver: down the core road to the Yards and fire it into the dark, where a crewmate waits.",
            "Down the outfall and out through the Wound, dark, before the cutters think to look down.",
            "As scrap: a tug with a work code, a tarp and a forged manifest tows 'a cracked reactor housing' out the front.",
            "Loud: blow the Cradle floor, or the big door, and run. A cruiser is six minutes away.")
    return sk


def den_rock2():
    sk = Sketch("den_rock", "Den", "·", "The Rock",
                "A natural asteroid with a cave. You dig it and build on it with the ship editor; then you have to hold it.")

    def rock(v, plan=False):
        if plan:
            v.disc(40, 30, 22, "rock", noise=0.22, seed=14)
            v.disc(76, 38, 8, "rock", noise=0.3, seed=5)
            v.scatter(10, 2, 92, 58, "rock2", 0.2, 3, only=("rock",))
            v.disc(59, 32, 6, "air", noise=0.2, seed=2)
            return
        v.disc(42, 36, 25, "rock", noise=0.22, seed=21, sy=0.9)
        v.disc(84, 44, 9, "rock", noise=0.3, seed=5)
        v.scatter(10, 2, 98, 68, "rock2", 0.2, 3, only=("rock",))
        v.line([(22, 28), (32, 42), (38, 54)], 1.6, "ore")
        v.line([(40, 16), (52, 22)], 1.4, "ore")
        v.disc(61, 38, 7, "air", noise=0.2, seed=2)                  # the natural cave, facing the shard
        v.line([(45, 64), (47, 54), (53, 46), (57, 41)], 2, "air")    # a natural crack down from the top

    v = sk.view("section", 104, 72, "After the first core: one crew's den (yours will differ). Side section, 1 cell = 8 m.")
    rock(v)
    v.rect(50, 32, 64, 43, "air")                    # the cave squared off into a berth hall
    v.rect(50, 37, 56, 38, "deck")
    v.line([(50, 36), (36, 35)], 6, "air")           # the core tunnel, dug wide enough for it
    v.disc(30, 35, 5, "air")                         # the core chamber, deep in the rock
    v.rect(34, 13, 56, 24, "air")                    # the Cutter hall
    v.line([(45, 13), (45, 5)], 6, "air")            # its haul door, underneath
    v.line([(54, 32), (52, 24)], 3, "air")           # lift shaft, hall to Cutter
    v.rect(36, 21, 37, 24, "truss")
    v.rect(52, 21, 53, 24, "truss")
    v.rect(41, 16, 48, 19, "wreck")
    v.rect(38, 59, 46, 60, "hull")                   # built: a watch tower on the crown
    v.rect(40, 60, 44, 67, "hull")
    v.rect(41, 61, 43, 66, "air")
    v.rect(66, 44, 77, 45, "hull")                   # built: a bridge to the shard
    v.disc(84, 44, 3, "air")
    v.rect(77, 43, 82, 46, "air")
    v.rect(14, 40, 18, 46, "tank")                   # built: tanks on the west face
    core = (30, 35)
    amber = (255, 214, 140)
    for q in ((42, 62), (66, 52), (22, 16), (86, 53), (45, 10), (65.5, 38), (84, 44)):
        v.tether(core, q, amber)
    v.core(core, 2.4, "")
    v.shutter((65.5, 33), (65.5, 43), "")
    v.shutter((42, 9), (48, 9), "")
    v.door((43, 33), (43, 38), "")
    for c, f in (((42, 68), 90), ((67, 53), 50), ((21, 15), 215), ((87, 54), 40), ((86, 35), -40)):
        v.turret(c, f)
        v.eye(c, f, 14, 70, COL["friend"])
    for p, h in (((55, 39.5), 0), ((60, 34.5), 180), ((53, 34.5), 0)):
        v.ship(p, h, (255, 179, 71), 2.4)
    v.ship((45, 20.5), 0, (255, 106, 42), 2.4)
    v.rect(57, 41, 61, 42, "hull")
    v.lamp((59, 42.5), COL["cyan"])
    for p in ((52, 41), (62, 36), (38, 22), (50, 22), (33, 38), (28, 32)):
        v.lamp(p)
    v.ghost(47, 60, 53, 64, "place")
    v.dig(22, 44, 29, 50, "dig")
    v.route([(103, 30), (90, 32), (78, 38), (71, 39), (62, 38)], "home", "")
    v.route([(60, 0), (48, 2), (45, 8), (45, 18)], "tow", "")
    for p, t, c in (((57, 44.5), "berth hall", COL["dim"]), ((30, 28.5), "core chamber", COL["core"]), ((45, 25.5), "the Cutter", COL["dim"]),
                    ((42, 70.5), "watch tower", COL["dim"]), ((71, 47), "bridge", COL["dim"]), ((84, 39.5), "gun nest", COL["dim"]),
                    ((71, 30), "the canyon", COL["text"]), ((92, 28), "home run", COL["home"]), ((64, 2), "tow run", COL["tow"]),
                    ((9, 43), "tanks", COL["dim"]), ((59, 46.5), "the Table", COL["cyan"])):
        v.label(p, t, c, "mm", 12)
    for n, p in ((1, (71, 34)), (2, (59, 30)), (3, (25, 39)), (4, (57, 18)), (5, (36, 66)), (6, (92, 48)), (7, (39, 40))):
        v.poi(n, p)

    d1 = sk.view("section", 104, 72, "Day one: the rock as you find it. Two ships in a cave, a plate for a Table, no power.")
    rock(d1)
    for p, h in (((60, 36), 180), ((63, 40), 180)):
        d1.ship(p, h, (255, 179, 71), 2.4)
    d1.rect(56, 38, 59, 39, "hull")
    d1.lamp((57.5, 39.5), COL["cyan"])
    d1.lamp((62, 43))
    d1.label((61, 47), "the camp", COL["dim"], "mm", 12)
    d1.label((30, 30), "ore", COL["dim"], "mm", 12)
    d1.label((86, 56), "the shard", COL["dim"], "mm", 12)
    d1.label((71, 60), "the canyon", COL["dim"], "mm", 12)
    d1.label((44, 67.5), "a crack", COL["dim"], "mm", 12)

    at = sk.view("plan", 100, 62, "Under attack (top down): the same den, its gun arcs, and who comes for it.")
    rock(at, True)
    at.rect(50, 28, 62, 37, "air")
    at.line([(50, 32), (36, 31)], 6, "air")
    at.disc(30, 31, 5, "air")
    at.rect(66, 36, 70, 37, "hull")
    at.core((30, 31), 2.4, "")
    for c, f in (((40, 53), 90), ((62, 44), 40), ((20, 16), 215), ((80, 45), 50), ((78, 31), -30), ((44, 8), 270)):
        at.turret(c, f)
        at.eye(c, f, 15, 80, COL["friend"])
    at.shutter((62.5, 28), (62.5, 37), "")
    for k in range(9):
        at.dot((66 + (k % 3) * 2.2, 22 + (k // 3) * 2.4), COL["loud"], 2)
    at.dot((8, 54), COL["lamp"], 3)
    at.label((8, 57.5), "decoy beacon", COL["dim"], "mm", 12)
    at.label((69, 18.5), "mines", COL["loud"], "mm", 12)
    at.route([(99, 60), (88, 50), (76, 42), (68, 33)], "patrol", "")
    at.route([(99, 4), (84, 14), (72, 24), (66, 31)], "patrol", "")
    at.route([(0, 2), (10, 12), (18, 22), (25, 29)], "in", "")
    at.label((98, 58), "the Alliance: they followed the core", COL["loud"], "rm", 12)
    at.label((20, 4), "a rival crew: quiet, dark, digging toward your core", COL["in"], "lm", 12)
    for p in ((56, 30), (58, 35), (53, 34)):
        at.ship(p, 0, (255, 179, 71), 2.2)

    sk.note("1-7 on the map", "1 the canyon between the rock and its shard: the way home   2 the berth hall, dug out of the cave",
            "3 the core chamber   4 the Cutter: hauls come in underneath and are cut inside   5 a watch tower, built",
            "6 a gun nest on the shard, and the bridge to it   7 a blast door on the core tunnel")
    sk.note("Day one", "A natural rock with a cave facing its own shard, an ore vein, a crack. Two ships, a hull plate for a "
            "Table, lamps on batteries. No core, so no power: no guns, no doors, no Cutter.")
    sk.note("Dig and build: the ship editor, at den scale",
            "Dig mode removes rock cell by cell or by the box; what you dig comes out as ore. Build mode places the same blocks "
            "a ship is built from: plate, armour, doors, guns, lamps, cradles.",
            "Nothing is bought from a menu. Blocks come from what you haul home and cut, and from the rock itself.",
            "Where the berths go, how wide the tunnels are, where the core sits: all yours. No two dens are alike.")
    sk.note("The core",
            "The stolen core goes wherever you dig a chamber for it. Everything powered is tethered to it by cable you lay: "
            "guns, doors, the Cutter, lights. A short cable is safe; a long one across the surface can be cut.",
            "More cores later mean more power: more guns, bigger works, in time a station of your own.")
    sk.note("Holding it",
            "A lit core can be tracked. The Alliance comes for the one you took; rival crews come to take it the way you did: "
            "dark, through a gap, cutting tethers.",
            "So the den is your own heist map, drawn by you: gun arcs, blast doors, mines, decoys, a tunnel too narrow for a core "
            "to leave by. Every trick you used at the Lantern is one to guard against here.")
    sk.note("To check before promising", "The Hollow's rock is fixed scenery today. A rock the crew digs in play needs an "
            "editable voxel grid at this size; that has to be measured first.")
    return sk


ALL = [heist_lantern, den_rock2]
ROUND1 = [heist_stack, heist_wheel, heist_quarry, den_rift, den_relay, den_rock]
