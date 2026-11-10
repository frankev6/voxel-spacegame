"""The den: THE RIFT. A colossal warship broken in two; the crew lives in the gap. Designed movement-first, by growth stage.

Owner (2026-11-10), on the first round: "the only place i liked from your designs was the rift", and "you fundamentally
started from a wrong point of view, you are not taking into account ship movement through the place, and visual
splendor/spectacle/difference. the fact that it should start as a den for lowly bandits and grow into something
potentially majestic but only upon the players desires, like its not a dumb "pay 1000 to build a laboratory" type of
station".

THE PLACE. A 1.4 km dreadnought snapped in half. The halves hang 240 m apart, the fore half lifted and turned a little;
the fracture faces show every deck like a doll's house. From the gap, the ship's insides spilled out long ago: the
Spill, a river of wreckage 1.4 km long that streams away to port, with the gas giant behind the Rift.

MOVEMENT FIRST. Four flight lines shape everything:
  - Home run: gate, up the Spill (a weaving channel through the wreckage), into the Rift at the port mouth, past the
    Table, a turn into your deck mouth. The reveal: the Rift glows between the halves the whole way up the Spill.
  - Tow run: low and straight, under the Spill's wreckage and along the Rift floor to the Cutter (the breaker),
    which hangs at the bottom of the gap, so you watch your haul cut from the Table above.
  - Canyon run: through the Rift end to end, port to starboard, between lit deck faces, under cables and bridges.
  - The decks: every berth opens onto a deck that runs on into the hull, bay after bay: the crew's own space.

GROWTH, BY CHOICE. Nothing is bought from a menu. The crew grows the den from what they drag home and where they
put it: a reactor towed in lights a deck; a captured gun goes on the face; a hull section from a wreck becomes a
bridge. Stages here are snapshots of one crew's den, and stage 2 shows two different crews' choices:
  1   the bandit hole: one deck lit, a plate on two cables for a Table, a scrap cradle, one welded scrap gate
  2a  the breakers: a crew that chose salvage; the Cutter grows into a breaking yard, the aft half is being eaten
  2b  the smugglers: a crew that chose heists; the outside stays dark, a hidden market in the fore face, a back door
  3   the majestic: what a crew can make of it in the end; the halves joined by a great span, the Spill a lit
      highway, every deck alight, the engines relit, three gates, visitors
"""
import math
import random

from place_kit import Place, DISTRICT as C, polar

AFT = [(-700, 0, 0, 104, 64, 4), (-640, 0, 4, 140, 88, 4), (-470, 0, 14, 168, 116, 3.4), (-260, 0, 20, 166, 124, 3.0),
       (-110, 0, 20, 160, 120, 3.0)]
FORE = [(130, 44, 74, 150, 114, 3.0), (300, 36, 70, 140, 104, 3.0), (470, 28, 64, 112, 84, 2.6), (610, 20, 58, 72, 58, 2.1),
        (712, 14, 56, 20, 22, 1.6)]
AFT_DECKS = (-70, -20, 30, 80)          # deck floors relative to the hull's centre line (aft centre z = 20)
FORE_DECKS = (-60, -10, 40)             # (fore centre z = 74)
TABLE = (20, 10, 40)
CUTTER = (10, 10, -110)
SPILL = [(-1540, 60, -30), (-1260, -40, 10), (-980, 90, 40), (-700, -30, 20), (-440, 50, 30), (-260, 10, 36), (-170, 10, 38)]
TOWLINE = [(-1540, 60, -30), (-1100, 0, -80), (-700, 20, -100), (-380, 10, -110), (-160, 10, -110), (-30, 10, -110)]


def hull_at(secs, y):
    for a, b in zip(secs, secs[1:]):
        if a[0] <= y <= b[0]:
            t = (y - a[0]) / (b[0] - a[0])
            return [a[i] + (b[i] - a[i]) * t for i in range(1, 5)]
    return list(secs[0][1:5]) if y < secs[0][0] else list(secs[-1][1:5])


def near_line(p, line, r):
    for a, b in zip(line, line[1:]):
        ax, ay, az = [b[i] - a[i] for i in range(3)]
        L2 = ax * ax + ay * ay + az * az
        t = max(0.0, min(1.0, ((p[0] - a[0]) * ax + (p[1] - a[1]) * ay + (p[2] - a[2]) * az) / (L2 or 1)))
        q = (a[0] + ax * t, a[1] + ay * t, a[2] + az * t)
        if math.dist(p, q) < r:
            return True
    return False


# ======================================================================================================================
def base(P):
    """The wreck as it was found: two hollow halves with decks, the jagged fracture, the dead engine room, the Spill."""
    P.loft(AFT, "hull", shell=8)
    P.loft(FORE, "hull", shell=8)
    for secs in (AFT, FORE):
        y0, y1 = secs[0][0] + 40, secs[-1][0] - 30
        for sx in (-1, 1):
            pts = [(y, *hull_at(secs, y)) for y in (y0, (y0 + y1) / 2, y1)]
            P.loft([(y, u + sx * (w - 4), v - h * 0.25, 10, h * 0.32, 3) for y, u, v, w, h in pts], "armor")
        pts = [(y, *hull_at(secs, y)) for y in (y0, y1)]
        P.loft([(y, u, v + h - 4, w * 0.45, 12, 4) for y, u, v, w, h in pts], "armor")
        P.loft([(y, u, v - h + 2, w * 0.25, 14, 4) for y, u, v, w, h in pts], "plate_dark")
        for y in range(int(secs[0][0]) + 40, int(secs[-1][0]) - 10, 60):
            u, v, w, h = hull_at(secs, y)
            P.loft([(y - 3, u, v, w + 4, h + 4, 3.2), (y + 3, u, v, w + 4, h + 4, 3.2)], "trim")
            P.loft([(y - 3, u, v, w - 8, h - 8, 3.2), (y + 3, u, v, w - 8, h - 8, 3.2)], None, carve=True)

    def decks(secs, y0, y1, zs):
        u, v, w, h = hull_at(secs, (y0 + y1) / 2)
        for z in zs:
            P.box((u, (y0 + y1) / 2, v + z), (2 * w - 30, y1 - y0, 4), "deck")
        for y in range(int(y0) + 70, int(y1), 80):
            uu, vv, ww, hh = hull_at(secs, y)
            P.box((uu, y, vv), (2 * ww - 20, 4, 2 * hh - 20), "plate_dark")
            for z in zs[:-1]:
                for x in (-60, 0, 60):
                    P.carve_box((uu + x, y, vv + z + 18), (36, 6, 28))
    decks(AFT, -560, -110, AFT_DECKS)
    decks(FORE, 130, 540, FORE_DECKS)
    # the fracture
    P.carve_ellipsoid((0, -96, 20), (230, 46, 170), noise=0.45, scale=26, seed=3)
    P.carve_ellipsoid((40, 116, 74), (220, 44, 160), noise=0.45, scale=26, seed=4)
    for y, sd in ((-150, 5), (-180, 6), (170, 7), (200, 8)):
        u, v, w, h = hull_at(AFT if y < 0 else FORE, y)
        P.carve_ellipsoid((u + (w if sd % 2 else -w), y, v + h * 0.5), (40, 50, 34), noise=0.4, scale=18, seed=sd)
    for k, (x, z) in enumerate(((-120, -40), (90, 60), (-60, 110), (140, -70))):
        P.cylinder((x, -110, z), (x * 0.9, -60 - k * 8, z * 1.1), 4, "trim")
        P.cylinder((x + 40, 130, z + 54), (x * 0.9 + 40, 82 + k * 6, z + 50), 4, "trim")
    # the engine room, dead, open at the stern; the bridge tower; the fore side hangar; the bow magazine
    P.ellipsoid((0, -640, 6), (122, 110, 94), "plate_dark")
    P.carve_ellipsoid((0, -660, 6), (108, 120, 80), noise=0.06, seed=9)
    P.prism([(-60, -500), (40, -500), (60, -440), (40, -360), (-60, -360)], 130, 210, "armor")
    P.prism([(-40, -480), (24, -480), (40, -440), (24, -390), (-40, -390)], 210, 240, "hull")
    P.room((-12, -430, 176), (70, 90, 40), wall="trim")
    P.carve_box((26, -430, 180), (16, 70, 18))
    P.carve_cylinder((-12, -430, 100), (-12, -430, 156), 12)
    u, v, w, h = hull_at(FORE, 330)
    P.room((u + w - 56, 330, v + 20), (90, 150, 56), wall="plate_dark")
    P.carve_box((u + w - 4, 330, v + 20), (40, 120, 44))
    P.lamp((u + w - 56, 330, v + 36), C["white"], 0.25)
    u2, v2, w2, h2 = hull_at(FORE, 560)
    P.room((u2, 560, v2 - 8), (90, 80, 50), wall="armor")
    # the Spill: wreckage streaming from the gap to port, with a weaving channel through it
    rng = random.Random(17)
    n = 0
    while n < 170:
        t = rng.random() ** 1.5
        i = min(len(SPILL) - 2, int(t * (len(SPILL) - 1)))
        f = t * (len(SPILL) - 1) - i
        a, b = SPILL[i], SPILL[i + 1]
        c = [a[k] + (b[k] - a[k]) * f for k in range(3)]
        spread = 90 + 260 * (1 - t)
        p = (c[0] + rng.uniform(-60, 60), c[1] + rng.uniform(-spread, spread), c[2] + rng.uniform(-spread, spread) * 0.6)
        if near_line(p, SPILL, 56) or near_line(p, TOWLINE, 50) or p[0] > -150:
            continue
        size = rng.uniform(6, 34) * (1.4 - t * 0.6)
        kind = rng.random()
        mat = rng.choice(["hull", "hull", "plate_dark", "rust", "armor"])
        if kind < 0.55:
            P.ellipsoid(p, (size * rng.uniform(0.8, 1.6), size * rng.uniform(0.5, 1.0), size * rng.uniform(0.4, 0.8)), mat,
                        noise=0.3, scale=12, seed=n)
        elif kind < 0.85:
            P.box(p, (size * 2.2, size * rng.uniform(0.3, 0.6), size * 0.25), mat, yaw=rng.uniform(0, 180))
        else:
            q = (p[0] + rng.uniform(-40, 40), p[1] + rng.uniform(-40, 40), p[2] + rng.uniform(-30, 30))
            P.cylinder(p, q, max(3, size * 0.15), "trim")
        n += 1


def lights_common(P, lit_decks, deck_power=0.3):
    """Deck lights: lit_decks lists (half, deck index, x positions) that are alight."""
    for half, di, xs in lit_decks:
        secs, decks_, y = (AFT, AFT_DECKS, -122) if half == "aft" else (FORE, FORE_DECKS, 142)
        u, v, w, h = hull_at(secs, y)
        z = v + decks_[di] + 20
        for x in xs:
            P.lamp((u + x, y, z), C["amber"], deck_power)
            P.lamp((u + x, y + (-60 if half == "aft" else 60), z), C["amber"], deck_power * 0.7)


def berth(P, half, di, x, cls, lit=True):
    secs, decks_, y, yaw = (AFT, AFT_DECKS, -136, 0) if half == "aft" else (FORE, FORE_DECKS, 156, 180)
    u, v, w, h = hull_at(secs, y)
    z = v + decks_[di] + 12
    P.ship((u + x, y, z), yaw, cls, lit=lit)
    P.lamp((u + x, y + (8 if half == "aft" else -8), z - 8), C["berths"], 0.35)
    return (u + x, y, z)


def flights(P, stage):
    P.route("Home run: gate, up the Spill, into the Rift, to your berth", SPILL + [(-70, 10, 70), (-30, -70, 96), (-40, -128, 66)],
            C["arrival"], kind="flight", frames=6,
            note="The reveal: the Rift glows between the halves the whole way up the Spill; the channel weaves between wrecks.")
    P.route("Tow run: low and straight to the Cutter", TOWLINE, C["breaker"], kind="flight", frames=4,
            note="Wide and straight on purpose: a load on a cable comes home under the Spill and up to the cradle.")
    P.route("Canyon run: through the Rift, port to starboard", [(-200, 60, 130), (-110, 90, 96), (-20, 100, 60), (60, 90, 0), (130, 50, -50), (170, 20, -80)],
            C["hearth"], kind="flight", frames=5,
            note="Between the lit deck faces, under the cables and over the bridges.")
    P.route("Into the decks", [(-40, -128, 66), (-10, -170, 68), (0, -250, 68), (0, -330, 68), (0, -420, 68)], C["white"], kind="flight", frames=4,
            note="Behind your berth the deck runs on into the hull: bay after bay through the bulkhead doors.")


def views(P):
    P.view("Arrival: up the Spill toward the Rift", (-1350, -160, 140), (0, 10, 40), fov=42)
    P.view("The fracture face", (180, 20, 60), (-20, -140, 20), fov=55)
    P.view("Over the Rift from above", (240, -60, 460), (20, 12, 20), fov=50)
    P.view("From the Table", (60, 40, 50), (-40, -130, 30), fov=60)


def pois(P, table_name, table_text, cutter_text):
    u, v, w, h = hull_at(FORE, 330)
    P.poi(1, "The Spill", (-700, -30, 20), "arrival", "the river of wreckage from the gap, 1.4 km to port: the road home weaves through it",
          view=((-900, -150, 90), (-500, 30, 30)))
    P.poi(2, "The Rift", TABLE, "hearth", "the gap between the halves: the den itself; every deck face looks into it",
          view=((-230, 10, 80), (40, 10, 40)))
    P.poi(3, table_name, TABLE, "hearth", table_text, view=((96, -40, 70), (10, 30, 40)))
    P.poi(4, "Your berth", (-30, -136, 62), "berths", "a deck mouth on a fracture face; behind it your deck runs on into the hull",
          view=((40, -40, 70), (-40, -170, 50)))
    P.poi(5, "The Cutter", CUTTER, "breaker", cutter_text, view=((190, -90, -30), (10, 10, -110)))
    P.poi(6, "Engine room", (0, -640, 6), "white", "the dead engines at the stern: a space waiting for whatever the crew brings home",
          view=((0, -830, 30), (0, -640, 6)))
    P.poi(7, "Fore hangar", (u + w - 56, 330, v + 20), "trade", "a side hangar in the fore half", view=((u + w + 120, 260, v + 50), (u + w - 60, 340, v + 10)))
    P.poi(8, "Gate", (-1700, 60, -30), "gates", "where the Spill ends: the den's gate(s) to the jobs", inside=False)


def design(stage="3"):
    stage = str(stage)
    names = {"1": "the bandit hole", "2a": "the breakers' yard", "2b": "the smugglers' nest", "3": "the majestic"}
    P = Place("den_rift", f"The Rift · stage {stage}: {names[stage]}",
              "A warship broken in two; the crew lives in the gap. It grows from what they drag home and where they put it.",
              ((-1560, -760, -220), (320, 760, 300)), cell=4.0)
    base(P)
    P.planet((7200, 2600, 900), 5200, kind="giant")
    flights(P, stage)
    views(P)

    if stage == "1":
        # ------------------------------------------------------------ the bandit hole
        P.window_density = 0.08
        P.box((TABLE[0], TABLE[1], TABLE[2]), (44, 30, 4), "rust", yaw=12)            # a hull plate for a table
        P.cylinder((TABLE[0] - 18, TABLE[1], TABLE[2]), (-80, -104, 110), 2, "trim")
        P.cylinder((TABLE[0] + 18, TABLE[1], TABLE[2]), (90, 124, 160), 2, "trim")
        P.holo((TABLE[0], TABLE[1], TABLE[2] + 4), C["hearth"], r=8)
        P.lamp((TABLE[0], TABLE[1], TABLE[2] + 16), C["hearth"], 0.8)
        for k in range(8):                                                              # a lamp string across the gap
            t = k / 7
            P.lamp((-80 + 170 * t, -104 + 228 * t, 110 + 50 * t - 30 * math.sin(t * math.pi)), C["amber"], 0.25)
        P.cylinder(CUTTER, (-60, -110, -90), 5, "hull2")                                 # a scrap cradle: two salvaged arms
        P.cylinder(CUTTER, (80, -110, -80), 5, "hull2")
        P.box(CUTTER, (34, 24, 20), "rust", yaw=20)
        P.lamp(CUTTER, C["breaker"], 2.5)
        lights_common(P, [("aft", 2, (-60, -20, 20))])
        berth(P, "aft", 2, -40, "light")
        berth(P, "aft", 2, 30, "heavy")
        P.strip([(-160, -100, 48), (-60, -100, 48)], C["berths"])
        for p in SPILL[1:-1]:
            P.lamp((p[0], p[1], p[2] + 30), C["danger"], 0.3)                           # red marker beacons up the channel
        P.gate((-1700, 60, -30), 90, facing=270, label="scrap gate")
        P.sign((-30, -100, 96), "DEN", C["amber"], yaw=180, size=3)
        pois(P, "The Table", "a hull plate on two cables with the board on it; F here", "two salvaged arms hold a wreck; the crew cuts it by hand")
        P.notes = dict(
            idea=("Stage 1, the bandit hole. A crew of nobodies has found the Rift and moved into one deck on the aft face. Most of the "
                  "wreck is dark. The Table is a hull plate on two cables, a lamp string runs across the gap, two salvaged arms hold "
                  "whatever they dragged home, and a gate welded from scrap sits at the end of the Spill, with red beacons marking "
                  "the channel through the wreckage."),
            plays=["Home run: the scrap gate, red beacons up the Spill, the Rift's one lit deck growing as you come.",
                   "Tow run: low under the Spill to the scrap cradle; you see it from the Table above.",
                   "Everything else in the wreck is there to claim: dark decks, the engine room, the fore hangar, the bow."],
            strengths=["A humble start that already looks like a place: the broken ship, the Spill, the planet behind.",
                       "Every later stage is the same wreck, so the growth is the crew's own story."],
            costs=["The dark wreck must still be readable: the Rift's few lights and the beacons carry the wayfinding."],
            key=[("home run", C["arrival"]), ("tow run", C["breaker"]), ("canyon run", C["hearth"]), ("into the decks", C["white"]),
                 ("flight lines: clearance under 20 m red, under 40 m amber", C["danger"])],
        )
    elif stage == "2a":
        # ------------------------------------------------------------ the breakers' yard
        P.window_density = 0.35
        P.cylinder((TABLE[0], TABLE[1], TABLE[2] - 4), (TABLE[0], TABLE[1], TABLE[2] + 4), 30, "deck")
        for (a_, b_) in (((-110, -104, 120), (-70, 124, 170)), ((120, -104, 120), (150, 124, 170))):
            P.cylinder(TABLE, a_, 3, "trim")
            P.cylinder(TABLE, b_, 3, "trim")
        P.holo((TABLE[0], TABLE[1], TABLE[2] + 6), C["hearth"], r=12)
        P.lamp((TABLE[0], TABLE[1], TABLE[2] + 20), C["hearth"], 1.0)
        # the breaking yard fills the Rift floor: a gantry across the gap, three cradles, cranes on the aft face
        P.box((10, 10, -70), (300, 14, 12), "hull2")
        P.box((10, 10, -150), (300, 14, 12), "hull2")
        for x in (-110, 10, 130):
            P.torus((x, 10, -110), 34, 5, "armor", axis="x")
            P.cylinder((x, 10, -70), (x, 10, -95), 4, "hull2")
            P.box((x, 10, -110), (30, 22, 18), "rust", yaw=x)
            P.lamp((x, 10, -100), C["breaker"], 2.5)
        for x in (-140, -60, 40, 120):
            P.cylinder((x, -110, -40), (x + 20, -60, 30), 5, "hull2")                     # cranes off the aft face
            P.cylinder((x + 20, -60, 30), (x + 20, -20, -60), 2, "trim")
        for k in range(10):                                                              # stacked plate on the deck ledges
            P.box((-150 + k * 30, -128, -48 + (k % 3) * 6), (24, 16, 10 + (k % 3) * 6), "rust" if k % 2 else "plate_dark")
        # the aft half's flank opened: the crew is breaking the wreck itself for plate
        for k, (y, z) in enumerate(((-300, 60), (-380, -20), (-460, 40))):
            u, v, w, h = hull_at(AFT, y)
            P.carve_box((u - w + 10, y, z), (40, 50, 40))
            P.lamp((u - w + 10, y, z), C["breaker"], 0.6)
        lights_common(P, [("aft", 0, (-60, 0, 60)), ("aft", 1, (-60, 0, 60)), ("aft", 2, (-60, 0, 60)), ("fore", 1, (-40, 40))])
        berth(P, "aft", 2, -40, "light")
        berth(P, "aft", 2, 40, "heavy")
        berth(P, "aft", 1, -20, "light")
        berth(P, "fore", 1, 0, "heavy")
        u, v, w, h = hull_at(FORE, 330)
        P.ship((u + w - 56, 300, v + 10), 270, "heavy", lit=False)                       # scrap buyers
        P.ship((u + w - 56, 366, v + 10), 270, "heavy", lit=False)
        P.lamp((u + w - 50, 330, v + 40), C["trade"], 0.8)
        for p in SPILL[1:-1]:
            P.lamp((p[0], p[1], p[2] + 30), C["amber"], 0.35)
        for p in TOWLINE[1:-1]:
            P.lamp((p[0], p[1], p[2] - 20), C["breaker"], 0.4)
        P.strip(TOWLINE[2:], C["breaker"], below=None)
        P.gate((-1700, 60, -30), 100, facing=270, label="gate")
        P.sign((10, -6, -40), "THE CUTTER", C["breaker"], yaw=180, size=5)
        pois(P, "The Table", "a deck on four cables, the board on it, over the breaking yard",
             "a gantry across the Rift floor with three cradles; cranes on the aft face; the crew is cutting the wreck itself")
        P.notes = dict(
            idea=("Stage 2a, a crew that chose salvage. The Cutter has grown into a breaking yard across the Rift floor: a gantry, three "
                  "cradles, cranes off the aft face, stacked plate on the deck ledges, and the aft half's own flank cut open for its "
                  "plate. Scrap buyers moor in the fore hangar. The tow run is lit end to end in ember; the Spill channel in amber."),
            plays=["More of what you bring home is cut at once; the yard's glow lights the whole Rift from below.",
                   "The wreck itself is a mine: breaking your own den's hull pays, and changes its shape."],
            strengths=["Growth that comes from play: this crew towed home a lot, and the den shows it."],
            costs=["A busy Rift floor narrows the tow run; the clearance numbers show where."],
            key=[("home run", C["arrival"]), ("tow run", C["breaker"]), ("canyon run", C["hearth"]), ("into the decks", C["white"]),
                 ("flight lines: clearance under 20 m red, under 40 m amber", C["danger"])],
        )
    elif stage == "2b":
        # ------------------------------------------------------------ the smugglers' nest
        P.window_density = 0.02
        P.cylinder((TABLE[0], TABLE[1], TABLE[2] - 4), (TABLE[0], TABLE[1], TABLE[2] + 4), 26, "plate_dark")
        P.cylinder(TABLE, (-80, -104, 110), 2, "trim")
        P.cylinder(TABLE, (90, 124, 160), 2, "trim")
        P.holo((TABLE[0], TABLE[1], TABLE[2] + 6), C["violet"], r=10)
        P.lamp((TABLE[0], TABLE[1], TABLE[2] + 20), C["violet"], 0.8)
        # a hidden market in the fore face's decks: pink and violet behind hanging plates
        u, v, w, h = hull_at(FORE, 150)
        for k, x in enumerate((-80, -30, 20, 70)):
            P.lamp((u + x, 150, v + FORE_DECKS[1] + 18), C["trade"] if k % 2 else C["violet"], 0.6)
            P.box((u + x, 132, v + FORE_DECKS[1] + 34), (36, 4, 20), "plate_dark")             # hanging screens over the mouths
        P.sign((u - 20, 128, v + FORE_DECKS[1] + 54), "ASK NOTHING", C["trade"], yaw=180, size=3)
        # a back door: a tunnel through the fore half to its starboard flank
        P.carve_tube([(60, 150, 100), (120, 260, 110), (190, 300, 110)], 22)
        P.lamp((120, 260, 110), C["violet"], 0.5)
        # decoys: false beacons in the Spill lead strangers off the channel
        rng = random.Random(4)
        for k in range(8):
            p = SPILL[1 + k % 5]
            P.lamp((p[0] + rng.uniform(-80, 80), p[1] + rng.uniform(-200, 200), p[2] + rng.uniform(-80, 80)), C["danger"], 0.3)
        for p in SPILL[2:-1]:
            P.lamp((p[0], p[1], p[2] + 30), C["violet"], 0.2)
        # captured guns hidden in the deck mouths
        for x in (-120, 100):
            P.cylinder((x, -112, 100), (x - 30, -60, 110), 3, "armor")
        lights_common(P, [("aft", 2, (-40, 20)), ("fore", 1, (-60, 0, 60))], deck_power=0.2)
        berth(P, "aft", 2, -40, "light")
        berth(P, "aft", 2, 30, "light")
        berth(P, "fore", 1, -20, "heavy")
        berth(P, "fore", 1, 50, "light")
        P.cylinder(CUTTER, (-60, -110, -90), 5, "hull2")
        P.box(CUTTER, (34, 24, 20), "rust", yaw=20)
        P.lamp(CUTTER, C["breaker"], 1.5)
        P.gate((-1700, 60, -30), 90, facing=270, label="gate")
        pois(P, "The Table", "a dark plate on cables, the board in violet; the market's light leaks from the fore face",
             "kept small: this crew sells what it takes whole, through the market")
        P.notes = dict(
            idea=("Stage 2b, a crew that chose heists. The outside of the wreck stays dark on purpose: anyone passing sees a dead hulk. "
                  "Inside the Rift, the fore face's decks hold a hidden market behind hanging plates (pink and violet), captured guns sit "
                  "in the aft deck mouths, a tunnel through the fore half makes a back door, and false beacons in the Spill lead "
                  "strangers off the channel."),
            plays=["Only the crew knows the true channel; the decoys lead others into the wreckage.",
                   "The back door lets a crew leave without going up the Spill: useful after a heist goes loud."],
            strengths=["Same wreck as 2a, a different place: the crew's choices are visible from outside and in."],
            costs=["A dark den must still be easy for its own crew to read: the violet lights and the true beacons do that."],
            key=[("home run", C["arrival"]), ("tow run", C["breaker"]), ("canyon run", C["hearth"]), ("into the decks", C["white"]),
                 ("flight lines: clearance under 20 m red, under 40 m amber", C["danger"])],
        )
    else:
        # ------------------------------------------------------------ the majestic
        P.window_density = 1.0
        # the span: a great built bridge joining the halves across the top of the Rift, hollow, a hall inside
        P.loft([(-150, 10, 150, 30, 20, 2.6), (-60, 14, 186, 26, 22, 2.6), (10, 20, 200, 24, 22, 2.6), (80, 28, 204, 26, 22, 2.6),
                (170, 40, 196, 30, 20, 2.6)], "pearl", shell=5)
        for y in (-80, -30, 20, 70, 120):
            P.loft([(y - 2, 10 + (y + 130) * 0.1, 194, 30, 26, 2.6), (y + 2, 10 + (y + 130) * 0.1, 194, 30, 26, 2.6)], "trim")
            P.lamp((10 + (y + 130) * 0.1, y, 186), C["white"], 0.5)
            P.lamp((10 + (y + 130) * 0.1 + 34, y, 196), C["vault"], 0.3)
        for y in (-60, 0, 60, 120):                                                  # its arches below
            P.torus((20, y, 120), 70, 4, "pearl", axis="y", arc=[270, 90])
        P.box((-70, 10, -10), (12, 250, 8), "armor")                                 # lower bridges
        P.box((110, 10, 20), (12, 250, 8), "armor")
        # the Hearth: a ring hall round the Table
        P.torus(TABLE, 64, 10, "hull")
        P.torus(TABLE, 64, 7, None)
        P.cylinder((TABLE[0], TABLE[1], TABLE[2] - 6), (TABLE[0], TABLE[1], TABLE[2]), 46, "deck")
        for a in range(0, 360, 60):
            x, y, _ = polar(a, 64, TABLE[:2])
            P.cylinder((x, y, TABLE[2]), (x * 0.6 + TABLE[0] * 0.4, y * 0.6 + TABLE[1] * 0.4, 150), 3, "trim")
        P.holo((TABLE[0], TABLE[1], TABLE[2] + 6), C["hearth"], r=20)
        P.lamp((TABLE[0], TABLE[1], TABLE[2] + 24), C["hearth"], 1.6)
        for a in range(0, 360, 30):
            x, y, _ = polar(a, 64, TABLE[:2])
            P.lamp((x, y, TABLE[2] + 12), C["white"], 0.3)
        # the prow rebuilt, a spire on the aft half, the engines relit
        P.loft([(712, 14, 56, 30, 30, 1.8), (780, 12, 54, 14, 14, 1.4), (820, 10, 54, 4, 4, 1.2)], "armor")
        P.loft([(-440, -10, 230, 30, 30, 2), (-440 + 1, -10, 240, 30, 30, 2)], "hull")
        P.cylinder((-10, -440, 240), (-10, -440, 330), 8, "armor", r2=2)
        for a in range(0, 360, 60):
            x, z, _ = polar(a, 112)
            P.cylinder((x, -712, z + 6), (x * 0.95, -690, z * 0.95 + 6), 22, "trim", r2=26)
            P.lamp((x, -720, z + 6), C["white"], 0.6)
        # the Spill tamed: beacon pylons in pairs along the channel, a highway
        for a, b in zip(SPILL, SPILL[1:]):
            for k in range(3):
                t = k / 3
                c = [a[i] + (b[i] - a[i]) * t for i in range(3)]
                for sy in (-50, 50):
                    P.cylinder((c[0], c[1] + sy, c[2] - 20), (c[0], c[1] + sy, c[2] + 20), 3, "armor")
                    P.lamp((c[0], c[1] + sy, c[2] + 22), C["white"], 0.35)
        # the yard and the markets
        for x in (-110, 10, 130):
            P.torus((x, 10, -110), 34, 5, "armor", axis="x")
            P.lamp((x, 10, -100), C["breaker"], 2.5)
        P.box((10, 10, -70), (300, 14, 12), "hull2")
        lights_common(P, [(h_, d_, (-80, -20, 40, 100)) for h_, n_ in (("aft", 4), ("fore", 3)) for d_ in range(n_)], deck_power=0.3)
        for i, (half, di, x, cls) in enumerate((("aft", 2, -40, "light"), ("aft", 2, 40, "heavy"), ("aft", 1, -20, "light"),
                                                ("fore", 1, 0, "heavy"), ("fore", 0, 60, "light"), ("fore", 2, -50, "heavy"))):
            berth(P, half, di, x, cls)
        u, v, w, h = hull_at(FORE, 330)
        P.ship((u + w - 56, 300, v + 10), 270, "heavy", lit=False)
        P.ship((u + w - 56, 366, v + 10), 270, "heavy", lit=False)
        rng = random.Random(9)
        for k in range(6):
            p = SPILL[1 + k % 5]
            P.ship((p[0] + 40, p[1] + (-20 if k % 2 else 20), p[2]), 90 if k % 2 else 270, "light" if k % 3 else "heavy")
        P.gate((-1700, 60, -30), 110, facing=270, label="gate 1")
        P.gate((-1680, 360, 160), 95, facing=280, label="gate 2")
        P.gate((-1660, -260, -180), 95, facing=260, label="gate 3")
        P.sign((20, -30, 230), "THE RIFT", C["white"], yaw=180, size=9)
        pois(P, "The Hearth", "a ring hall round the Table under the span; six berths face it from both halves",
             "three cradles on a gantry across the Rift floor")
        P.notes = dict(
            idea=("Stage 3, the majestic: what a crew can make of the Rift if they want to. A great span joins the halves across the top "
                  "of the gap, a hall inside it; a ring hall round the Table; every deck alight; the prow rebuilt and a spire on the "
                  "aft half; the engines relit; the Spill tamed into a highway of beacon pylons with visitors on it; three gates."),
            plays=["Home run: up a lit highway through the Spill, under the span, into the Hearth.",
                   "Other crews and merchants come here now: the den has become a place of its own.",
                   "Every piece of it was dragged home and placed by the crew; a different crew's stage 3 would look different."],
            strengths=["The payoff the bandit hole promised: the same wreck, majestic."],
            costs=["Many structures: they must come from the crew's own building (the block editor at den scale), not from content."],
            key=[("home run", C["arrival"]), ("tow run", C["breaker"]), ("canyon run", C["hearth"]), ("into the decks", C["white"]),
                 ("flight lines: clearance under 20 m red, under 40 m amber", C["danger"])],
        )

    P.level(50, "the Rift at the Table")
    P.level(-100, "the Rift floor and the tow run")
    P.section("x", 20, "along the ship, through the Rift")
    P.section("y", 10, "across the Rift: both faces, the Table, the Cutter")
    return P
