"""Tollhouse Nine, "the Lantern", in depth: the town level by level, six complete jobs, its day and its alarm.

Owner (2026-11-10), on the first Lantern sheet: "Yes, deepen it".

Run:  python3 ArtSource/Places/lantern.py
Out:  docs/design/places/sketch/lantern_levels.png, lantern_jobs.png
"""
import math
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from sketch2d import View, COL, render_grid, timeline, OUT  # noqa: E402
from sketches import lantern_town, LIV, STALL, P  # noqa: E402

AMBER = (255, 214, 140)
CORE_OK = (90, 240, 255)      # tint: the core (40 m) fits here
W, H = 104, 80
O = (52, 40)


def fits(v, x0, y0, x1, y1):
    v.tint(x0, y0, x1, y1, CORE_OK)


def names(v, items, size=12):
    for p, t, c in items:
        v.label(p, t, c, "mm", size)


# ====================================================================================================================
def level_crown():
    v = View("plan", W, H, "Level 1 · the Crown and the arch, from above (the roofs of the galleries below).")
    rng = random.Random(3)
    v.disc(*O, 31, "deck", noise=0.04, seed=3)
    v.disc(*O, 9.5, "jade")
    v.disc(*O, 8, "space")
    # the Crown, west
    v.rect(8, 26, 42, 54, "ivory")
    v.rect(8, 39, 42, 40, "jade")
    v.rect(10, 28, 26, 38, "air")
    v.rect(8, 30, 10, 36, "air")
    v.rect(29, 28, 40, 38, "air")
    v.rect(10, 42, 40, 52, "air")
    v.rect(40, 45, 43, 49, "air")
    for x in (16, 24, 32):
        v.rect(x, 44, x + 1, 48, "ivory")
    v.disc(42, 56, 2.6, "ivory")
    v.shutter((29, 30), (29, 36), "")
    # Pilots' Rest, east
    v.disc(74, 46, 5.5, "glass")
    v.disc(74, 46, 4.5, "air")
    v.rect(81, 34, 86, 40, "ivory")
    v.rect(82, 35, 85, 39, "air")
    v.rect(64, 28, 71, 34, "shanty2")
    v.rect(65, 29, 70, 33, "air")
    v.rect(66, 56, 80, 62, "shanty2")
    v.rect(67, 57, 79, 61, "air")
    v.bar(P(O, 20, 8.7), P(O, 200, 8.7), COL["lane"], "", 2)
    v.bar(P(O, 110, 8.7), P(O, 290, 8.7), COL["lane"], "", 2)
    for c, f in (((8, 55), 150), ((43, 57), 80), ((8, 25), 210), ((88, 41), 0)):
        v.turret(c, f)
    v.eye((43, 57), 60, 16, 30)
    v.eye((8, 25), 215, 16, 30)
    for k, l in enumerate(("civ", "vest", "scav", "civ", "khu", "civ")):
        v.ship((52 + (k % 2) * 3 - 1.5, 54 + k * 4.2), 270, LIV[l], 2.4)
    for p, h in (((14, 31), 180), ((19, 35), 180), ((14, 35), 180)):
        v.ship(p, h, LIV["tlax"], 3.2)
    for p, h, l in (((72, 45), 0, "civ"), ((76, 48), 180, "scav"), ((75, 43), 90, "civ"), ((70, 59), 0, "civ"), ((76, 59), 0, "vest"),
                    ((60, 22), 300, "civ"), ((66, 14), 300, "tlax"), ((36, 16), 40, "civ"), ((90, 60), 200, "civ")):
        v.ship(p, h, LIV[l], 2.2)
    v.ship((92, 20), 230, LIV["tlax"], 3.4)
    v.route([(52, 79), (52, 50)], "lane", "")
    v.route([(56, 34), (66, 16), (76, 2)], "lane", "")
    v.route([P(O, a, 36) for a in range(-40, 200, 20)], "patrol", "")
    names(v, [((18, 33), "garrison", COL["text"]), ((34.5, 33), "bank", COL["text"]), ((25, 50.5), "customs house", COL["text"]),
              ((74, 53.5), "the Long Wait (bar)", COL["text"]), ((88, 32), "chapel", COL["dim"]), ((67, 26.5), "clinic", COL["dim"]),
              ((73, 64), "bunkhouse", COL["dim"]), ((52, 40), "THE WELL", COL["text"]), ((60, 70), "toll queue", COL["lane"]),
              ((84, 6), "to the gates", COL["lane"]), ((52, 29), "arch", COL["lane"])])
    return v, "1 · the Crown and the arch", [
        "Who is here: the garrison (three cutters, forty soldiers, a captain counting days to transfer), customs clerks, the bank's "
        "tellers, and off shift everyone at the Long Wait.",
        "The arch scans and tolls every ship entering the Well. It runs on the Lantern: when the tethers go, it goes.",
        "The core fits: only the Well mouth itself."], None


def level_market():
    v = View("plan", W, H, "Level 2 · the market: six gallery floors like this one, round the Well.")
    v.disc(*O, 31, "hull", noise=0.04, seed=3)
    v.ring(*O, 9.5, 29, "air")
    v.disc(*O, 8, "space")
    for a in range(0, 360, 45):
        v.line([P(O, a, 7), P(O, a, 11)], 2.6, "air")
    rows = ["Noodle Row", "Parts Row", "Cloth Row", "Changers' Row", "Pawn Row", "Tool Row", "Green Row", "Shrine Corner"]
    for k, a in enumerate(range(22, 360, 45)):
        v.line([P(O, a, 13), P(O, a, 29)], 1, "deck")
    for a in range(0, 360, 6):
        v.set(int(P(O, a, 30.2)[0]), int(P(O, a, 30.2)[1]), "glass" if (a // 6) % 3 == 0 else "hull")
    v.line([P(O, 30, 8), P(O, 210, 8)], 1, "deck")
    v.core(O, 1.8, "")
    k = 0
    for a in range(22, 360, 45):
        for r in range(14, 29, 2):
            for s_ in (-1, 1):
                q = P(O, a + s_ * (70 / r), r)
                v.dot(q, STALL[k % 4], 1.6)
                k += 1
    rng = random.Random(8)
    for a in range(0, 360, 45):
        for r in (16, 21, 26):
            if rng.random() < 0.8:
                v.ship(P(O, a + rng.uniform(-8, 8), r), a + rng.choice([0, 180]), LIV[rng.choice(["civ", "civ", "scav", "vest", "khu", "tlax"])], 1.9)
    for a, r_, l in ((20, 4, "civ"), (95, 5, "vest"), (170, 3, "scav"), (230, 6, "civ"), (300, 4, "tlax"), (350, 6, "khu"), (60, 11, "civ"), (200, 11, "civ")):
        v.ship(P(O, a, r_), a + 100, LIV[l], 2)
    for kk in range(1, 9):
        v.dot((O[0] - 7 + kk * 1.6, O[1] + 3 + kk * 0.5), STALL[kk % 4], 1.4)
    for k, a in enumerate(range(0, 360, 45)):
        v.label(P(O, a, 34.5), rows[k], COL["text"] if k % 2 == 0 else COL["dim"], "mm", 12)
    names(v, [((52, 46), "Stall Bridge", COL["dim"]), ((52, 36.5), "the Lantern, below", COL["core"])])
    fits(v, 44, 32, 60, 48)
    return v, "2 · the market", [
        "Who is here: three hundred stalls, each a berth: you fly up, moor and trade from your ship. Noodles, parts, cloth, "
        "money-changers, pawnbrokers, a shrine. Haulers' crews on a night off.",
        "A dark ship is seen by a thousand people. A trader with a tow cable and nothing to sell is remembered.",
        "The core fits: up or down the Well, nowhere in the galleries."], None


def level_lantern():
    v = View("plan", W, H, "Level 3 · the Lantern: the Powerhouse under the market floor.")
    v.rect(24, 26, 86, 54, "armor")
    v.rect(26, 28, 42, 52, "air")          # turbine hall
    v.rect(46, 34, 58, 46, "air")          # the Cradle
    v.rect(42, 38, 46, 41, "air")          # staff door
    v.rect(58, 36, 86, 43, "air")          # the core road
    v.rect(62, 46, 80, 52, "air")          # engineers
    v.rect(70, 43, 73, 46, "air")
    v.rect(32, 26, 35, 28, "air")          # the dock service lift (comes up from the docks)
    v.rect(60, 28, 72, 33, "air")          # stores
    v.rect(64, 33, 66, 36, "air")
    for q in ((34, 40), (52, 62), (52, 16), (76, 39.5), (38, 60), (70, 20)):
        v.tether((52, 40), q, AMBER)
    v.core((52, 40), 2.5, "")
    v.fan((34, 34), 3)
    v.fan((34, 46), 3)
    v.shutter((85.5, 36), (85.5, 43), "")
    v.door((44, 38), (44, 41), "")
    v.door((32, 27), (35, 27), "")
    for a in range(0, 360, 15):
        v.dot(P((52, 40), a, 8), (120, 130, 135), 1.2)
    v.eye((47, 45), -50, 9, 50)
    v.eye((84, 42), 180, 12, 30)
    for p, h, l, sz in (((70, 49), 0, "tlax", 1.8), ((30, 40), 90, "tug", 2), ((66, 30.5), 0, "tug", 1.8), ((92, 39.5), 180, "tug", 2.6)):
        v.ship(p, h, LIV[l], sz)
    fits(v, 46, 34, 58, 46)
    fits(v, 58, 36, 86, 43)
    names(v, [((34, 54.8), "turbine hall", COL["text"]), ((52, 48), "the Cradle", COL["text"]), ((72, 34.6), "the core road", COL["text"]),
              ((71, 53), "engineers' room", COL["text"]), ((66, 26.8), "stores", COL["dim"]), ((92, 46), "the big door", COL["shutter"]),
              ((33.5, 24.5), "lift from the docks", COL["dim"]), ((44, 43), "staff door", COL["door"]), ((52, 31.5), "the Grate above", COL["dim"]),
              ((95, 34), "to the Yards", COL["dim"])])
    return v, "3 · the Lantern", [
        "Who is here: four engineers a shift, bored; the yard's crane-tug when something heavy moves; one garrison post at the "
        "big door, which opens only on the Crown's code.",
        "The six tethers leave the Cradle for the Crown, the market, the docks, the Yards, Undertown and the gate beacons.",
        "The core fits: the Cradle, the core road and the big door (and straight up through the Grate's panel)."], None


def level_docks():
    v = View("plan", W, H, "Level 4 · the freight docks and the Yards, either side of the trunk.")
    v.rect(40, 28, 64, 52, "armor")
    v.disc(52, 40, 3, "air")
    v.rect(4, 38, 40, 42, "deck")
    for x in (8, 16, 24, 32):
        v.rect(x, 20, x + 1, 38, "truss")
        v.rect(x, 42, x + 1, 60, "truss")
    for x, y in ((12, 46), (28, 30), (20, 50), (36, 46), (5, 30)):
        v.rect(x, y, x + 3, y + 3, "shanty2")
    v.rect(33, 42, 36, 45, "air")
    v.rect(64, 30, 100, 50, "deck")
    v.rect(70, 33, 98, 45, "truss")
    v.rect(70, 34, 82, 44, "hull")
    v.rect(72, 36, 82, 42, "air")
    v.rect(64, 36, 70, 43, "air")
    v.line([(66, 56), (101, 66)], 2.6, "armor")
    for k in range(6):
        q = (70 + k * 5.4, 57.2 + k * 1.55)
        v.disc(q[0], q[1], 1.6, "hull")
    for c, r in (((88, 12), 4.2), ((98, 14), 3.6), ((92, 22), 3.2)):
        v.disc(c[0], c[1], r, "tank")
    v.line([(92, 25), (92, 30)], 1, "truss")
    for p, h, l, sz in (((12, 28), 90, "civ", 7), ((20, 30), 90, "tlax", 6), ((12, 52), 270, "civ", 7), ((28, 50), 270, "scav", 5),
                        ((4, 46), 0, "civ", 5), ((36, 28), 90, "vest", 4), ((67, 39.5), 0, "tug", 2.6), ((90, 48), 180, "tug", 2.2),
                        ((60, 60), 30, "tug", 2.2)):
        v.ship(p, h, LIV[l], sz)
    v.turret((2, 62), 200)
    v.turret((102, 28), 0)
    v.charge((92, 16), "")
    v.route([(0, 40), (8, 40)], "lane", "")
    fits(v, 64, 30, 100, 50)
    fits(v, 49, 37, 55, 43)
    names(v, [((22, 63), "FREIGHT DOCKS", COL["text"]), ((84, 52), "THE YARDS", COL["text"]), ((88, 68), "mass driver", COL["text"]),
              ((96, 6), "fuel depot", COL["text"]), ((52, 46), "outfall", COL["dim"]), ((52, 31), "the trunk", COL["dim"]),
              ((67, 34.6), "ramp up", COL["dim"]), ((34.5, 47), "lift", COL["dim"]), ((86, 31.6), "a hauler, half built", COL["dim"])])
    return v, "4 · the docks and the Yards", [
        "Who is here: dock hands on three shifts (06, 14, 22), hauler crews, the repair guild's tugs, the yard foreman who "
        "hires by the day. The mass driver throws an ore pod to the refineries every half hour.",
        "The lift by the dock wall goes up into the turbine hall: staff only.",
        "The core fits: the yard apron, the mass driver's breech, a hauler's hold, and the outfall."], None


def level_under():
    v = View("plan", W, H, "Level 5 · Undertown: the old quarter, and the Wound.")
    rng = random.Random(12)
    v.disc(50, 40, 36, "shanty", noise=0.10, seed=5, sy=0.85)
    v.scatter(8, 4, 96, 76, "shanty2", 0.32, 4, only=("shanty",))
    v.ring(52, 40, 3, 5.2, "armor")
    # the warren: a ring street, radial alleys, squares
    for a in range(0, 360, 4):
        if not (150 < a < 215):
            v.line([P((50, 40), a, 15), P((50, 40), a + 4, 15)], 2.4, "air")
    for a in range(20, 360, 3):
        if (a // 30) % 3 != 1:
            v.line([P((50, 40), a, 26), P((50, 40), a + 3, 26)], 2, "air")
    for a, r0, r1, w in ((30, 15, 33, 2), (75, 6, 26, 2), (120, 15, 30, 2), (250, 15, 31, 2), (300, 6, 15, 2), (340, 15, 34, 2.4), (200, 6, 15, 2)):
        v.line([P((50, 40), a, r0), P((50, 40), a, r1)], w, "air")
    v.line([(49, 40), (38, 36), (30, 37)], 3, "air")      # outfall branches
    v.line([(55, 40), (66, 44), (74, 43)], 3, "air")
    v.disc(38, 28, 4.2, "air")
    v.ring(38, 28, 1.4, 2.6, "armor")
    v.disc(68, 52, 3.6, "air")
    v.disc(62, 24, 3, "air")
    v.disc(52, 40, 3, "air")
    v.disc(14, 38, 10, "space", noise=0.35, seed=9)
    v.scatter(2, 22, 30, 54, "wreck", 0.4, 2, only=("shanty", "shanty2"))
    v.line([(6, 30), (24, 46)], 1, "truss")
    v.line([(6, 46), (24, 30)], 1, "truss")
    v.line([(4, 38), (26, 38)], 1, "truss")
    for _ in range(90):
        x, y = rng.uniform(12, 90), rng.uniform(8, 72)
        if v.g[int(y)][int(x)] in ("shanty", "shanty2"):
            v.dot((x, y), (255, 190, 120), 1.2)
    v.eye((27, 47), 215, 14, 44)
    for p, h in (((12, 42), 40), ((18, 33), 120), ((9, 35), 300)):
        v.ship(p, h, LIV["tug"], 2.2)
    for p, h, l in (((38, 31.5), 0, "khu"), ((68, 52), 90, "scav"), ((62, 24), 200, "civ"), ((36, 52), 20, "scav"), ((63, 40.5), 0, "civ")):
        v.ship(p, h, LIV[l], 1.9)
    fits(v, 49, 37, 55, 43)
    fits(v, 5, 30, 24, 46)
    names(v, [((38, 22.6), "Winch Square: the fixer", COL["text"]), ((70, 57.5), "the Sump (bar)", COL["text"]), ((62, 19.8), "scrap chapel", COL["dim"]),
              ((14, 52), "the Wound", COL["loud"]), ((52, 46.8), "outfall", COL["dim"]), ((80, 30), "hab stacks", COL["dim"]),
              ((30, 64), "pawn alleys", COL["dim"]), ((50, 76), "UNDERTOWN", COL["text"])])
    return v, "5 · Undertown", [
        "Who is here: the people the Alliance tolls but does not house: dockers, stall hands, the repair guild, the Clan's fixer "
        "at Winch Square. It wakes when the market sleeps, and it does not talk to the garrison for free.",
        "The barge strike left the west side open to space. Scaffolds, floodlights, repair tugs by day; almost nobody by night.",
        "The core fits: the outfall trunk and the Wound's gap. The alleys take a ship, never a core."], None


def sheet_levels():
    panels = [level_crown(), level_market(), level_lantern(), level_docks(), level_under()]
    sec = View("section", 108, 126, "The side section again, for the five cuts (1 at the top, 5 at the bottom).")
    lantern_town(sec, random.Random(31))
    for n, z in ((1, 115), (2, 84), (3, 55), (4, 40), (5, 18)):
        sec.bar((0, z), (108, z), (220, 235, 255), "", 1)
        sec.poi(n, (4, z))
    sec.h = 126
    # crop the section to the panel shape by drawing it in a view of the same grid height: scale by rendering separately
    return panels, sec


# ====================================================================================================================
def job(title, colour, steps, routes, wrong, needs):
    v = View("section", 108, 126, "")
    lantern_town(v, random.Random(31))
    for kind, pts in routes:
        v.route(pts, kind, "")
    letters = "abcdefgh"
    lines = []
    for k, (p, text) in enumerate(steps):
        v.poi(letters[k], p, colour)
        lines.append(f"{letters[k]}  {text}")
    lines.append("If it goes wrong: " + wrong)
    lines.append("Needs: " + needs)
    return v, title, lines, colour


def sheet_jobs():
    wound_in = [(0, 5), (10, 11), (19, 17), (28, 21), (36, 20), (48.5, 26), (50.5, 30), (50.5, 49)]
    jobs = [
        job("1 · The Dimming", COL["out"],
            [((33, 84), "Arrive as traders on Lantern Night; buy sailcloth in Cloth Row and stitch a shroud."),
             ((52, 64.5), "For the ceremony the yard crane lifts the Grate's panel, so the light shines up the Well."),
             ((48, 56), "21:00, the Dimming: the town turns its lights off for one minute. Drop the shroud over the core and cut "
              "the tethers. For sixty seconds the dark is the show."),
             ((52, 100), "Tow the core straight up the Well among a thousand lantern boats."),
             ((52, 121), "The arch is dead without the Lantern. Out, and gone before the lights fail to come back.")],
            [("out", [(52, 56), (52, 64), (52, 100), (52, 125)])],
            "the shroud slips and the core lights the Well: a thousand witnesses and three cutters overhead.",
            "two ships, the festival date, a steady tow. Nothing is forced or blown."),
        job("2 · The Scrap Run", COL["out"],
            [((36, 10), "Winch Square: the fixer forges a work order, 'replace cracked reactor housing, expect brownouts'."),
             ((14, 20), "A week with the repair guild at the Wound earns a work code and makes your tug a familiar sight."),
             ((88, 38), "Take a scrap housing from the Yards and deliver it to the Powerhouse: the big door opens for a "
              "scheduled part."),
             ((62, 54), "Inside, the core goes into the housing. The engineers log the blackout as the planned brownout."),
             ((102, 40), "Tow 'the old housing' out through the Yards. The outbound scan weighs it: it matches the manifest.")],
            [("out", [(54, 55), (80, 54), (86, 44), (98, 40), (107, 52)])],
            "a customs clerk calls the Crown to confirm the work order; you have until someone answers.",
            "one ship, patience, the fixer's price. The quietest way, and the slowest."),
        job("3 · The Inside Man", COL["out"],
            [((72, 104), "The night engineer drinks at the Long Wait. He owes the bank more than his posting pays."),
             ((34, 109.5), "Clear his debt: pay it, or get into the bank and change the ledger."),
             ((70, 59.5), "On his shift (22:00 to 06:00) he logs tether maintenance and holds the outfall's flush."),
             ((52, 49), "He opens the Cradle's floor hatch. Cut the tethers; the core goes down the trunk."),
             ((52, 6), "Out the bottom in the dark. Undertown is awake, and Undertown says nothing.")],
            [("out", [(52, 54), (52, 48), (52, 6), (57, 0)])],
            "he loses his nerve, or the captain makes a surprise round; the flush is back on its clock.",
            "one or two ships, days of groundwork in the town. Leaves a man who knows your face."),
        job("4 · The Trojan", COL["eject"],
            [((14, 39), "A crewmate's hauler books a freight berth and a real load. Your ship is inside its hold."),
             ((35, 47), "At the 22:00 shift change, slip out and up the dock lift into the turbine hall."),
             ((45, 53.5), "Through the staff door to the Cradle. Cut the tethers: the town goes dark."),
             ((80, 54), "A charge on the big door from the inside; in the blackout it is one bang among many."),
             ((88, 41), "The hauler has moved to the yard apron 'to load a hull section'. The core goes in its hold; it leaves "
              "with the crowd, transponder on, manifest clean.")],
            [("in", [(14, 39), (35, 43), (35, 52), (45, 53.5)]), ("eject", [(56, 54), (82, 54), (88, 42), (106, 36)])],
            "the hauler is searched at the lane gate; a hold with a core in it cannot jump until its cargo blocks are sound.",
            "a hauler with cargo blocks, a small ship, two pilots. Uses the hold rule both ways."),
        job("5 · The Depot Fire", COL["loud"],
            [((98, 22), "Dark, from the east: a charge on the fuel depot."),
             ((50.5, 47), "A second ship goes up the outfall between flushes and mines the Cradle's floor from below."),
             ((93, 14), "Blow the depot. Every cutter and fire tug in town goes east."),
             ((52, 52), "Blow the floor. The core drops into the trunk on its tethers; cut them."),
             ((30, 8), "Down the trunk, out through the Wound, and run for the rocks. The cruiser is six minutes out.")],
            [("in", wound_in), ("loud", [(53.5, 50), (53.5, 8), (40, 2), (20, 0)])],
            "it already has. This is the plan for when there is no time to be clever.",
            "two ships, charges, speed. The town will remember this one."),
        job("6 · Pod Seven", COL["eject"],
            [((86, 40), "Hire on as a dock hand in the Yards. Learn the mass driver's clock: an ore pod every half hour."),
             ((104, 60), "A crewmate waits forty kilometres down the pod line with a tow rig."),
             ((70, 59.5), "03:00, the engineers' round: from their empty room, request a heavy lift. The big door opens for "
              "the yard's crane-tug, which tonight is you."),
             ((60, 54), "Cut the tethers. The alarm locks the driver in thirty seconds: 240 m of core road with a core on the "
              "cable."),
             ((92, 47), "Into the breech as pod seven. Fire. Finish your shift and clock out.")],
            [("eject", [(56, 54), (78, 54), (86, 50), (90, 47), (107, 47)])],
            "the core is in the breech when the driver locks: a 40 m ball, stuck, with the lights coming back.",
            "two pilots, a job held for days, the best tow flying in the game."),
    ]
    day = timeline("A day at Tollhouse Nine", 24, "hours, station time", [
        ("toll rush at the arch", COL["lane"], [(7, 9, "long queue: slow, and nobody looks closely"), (18, 20)]),
        ("market open", (255, 46, 136), [(9, 23, "stalls close at 23:00")]),
        ("dock shifts change", COL["door"], [6, 14, (22, None, "ten minutes with no one on the piers")]),
        ("mass driver", COL["eject"], [(6, 22, "a pod every 30 min, daytime only (night shots: by request)")]),
        ("outfall flush", (230, 210, 120), [(0, 24, "every 20 min for 40 s, all day")]),
        ("garrison", COL["danger"], [(0, 24, "a cutter loop every 12 min"), ]),
        ("cutters refuel", COL["danger"], [(1, 1.5, "one cutter down"), (13, 13.5)]),
        ("night engineer", COL["core"], [(22, 24), (0, 6, "one man in the engineers' room; rounds at 03:00")]),
        ("repair crews at the Wound", LIV["tug"], [(6, 20, "floodlit and busy by day, empty at night")]),
        ("Undertown awake", (255, 190, 120), [(20, 24), (0, 4)]),
        ("Lantern Night (monthly)", COL["out"], [(19, 21.06, "festival from 19:00; the Dimming at 21:00: one minute of dark")]),
    ], marks=[(h, f"{h:02d}:00") for h in range(0, 25, 3)])
    alarm = timeline("When the last tether is cut", 10, "minutes after", [
        ("the town", (255, 190, 120), [(0, 10, "blackout: emergency lamps only; the arch, scanners and door locks are on batteries")]),
        ("doors", COL["shutter"], [(0.17, 10, "0:10 the big door and the Crown lock")]),
        ("mass driver", COL["eject"], [(0.5, 10, "0:30 safeties lock the driver")]),
        ("garrison cutters", COL["danger"], [(1, 10, "1:00 three cutters launch; searchlights sweep the Well, then the docks, then below")]),
        ("the lanes", COL["lane"], [(2, 10, "2:00 outbound ships are stopped and scanned by hand, one by one")]),
        ("the crowd", (190, 190, 180), [(0.5, 5, "everyone who can leave is leaving: your cover"), ]),
        ("Alliance cruiser", COL["loud"], [(6, 10, "6:00 arrives from the gate; nothing leaves after this")]),
        ("what gives you away", COL["text"], [(0, 10, "a dark ship seen · the core's glow uncovered · the core knocking a wall · a tow cable in a searchlight · a hull they saw casing")]),
    ], marks=[(m, f"{m}:00") for m in range(0, 11)],
        note="they know the core is gone; they do not know who has it")
    return jobs, [day, alarm]


if __name__ == "__main__":
    panels, sec = sheet_levels()
    p = render_grid("lantern_levels", "Tollhouse Nine · in depth 1 of 2", "The town, level by level",
                    "Five cuts from the Crown down to Undertown. Blue-tinted areas are the only places a 40 m core fits; a ship fits "
                    "everywhere that is open.", panels, 3, 6)
    print("SKETCH", p)
    jobs, strips = sheet_jobs()
    p = render_grid("lantern_jobs", "Tollhouse Nine · in depth 2 of 2", "Six ways to take the Lantern",
                    "Each is one complete job on the same town, drawn on its side section. They can be mixed; these are the ones "
                    "the town most plainly offers.", jobs, 3, 5, footers=strips)
    print("SKETCH", p)
