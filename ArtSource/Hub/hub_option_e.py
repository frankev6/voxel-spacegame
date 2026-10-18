"""DEV-023 option E: THE HOLLOW, a pirate haven built into an asteroid (hub_layouts.py draws it; hub_arrival.py renders it).

Owner (2026-10-18): "lets make an option 5, much smaller version, not as "bustling city", its a station built into an
moon/asteroid, with space station construction portruding outside/around the moon, and also tunnels and highways INSIDE
it, that the player can go into. like the station is built into and as part of the asteroid, not as many ships goin in
and out and not as large scale as the meridian. it is primarily a black market merchant / pirate haven."

The rock is a two-lobed asteroid about 1.7 km long, stored as a grid of 12 m rock columns (a column is one or two solid
spans: a tunnel or cavern is the gap between them), so the game can build it from voxels. Tunnels and caverns are cut
at the flight plane. Inside the rock, and near its cliffs, the rock above the plane is hidden round the player (a
cutaway), so the tunnels are flown like any street.

Who runs it: the Khuchad Clan. Raiders are hostile everywhere else (SPEC-33, relation locked); here they are only
customers, and what they take is sold in the black market.

Owner, on the first version: "no need for the sun you have at the center, its just an asteroid, doesn't have a core like
that. also you borrowed too much of the design things we talked about for #4 into here which i didn't ask for, also whats
the with the rotating thing at the top and also all the holes at the top of the asteroid? why would the bazaar be open to
the top?" So: a plain rock with no pits or reactor; everything inside it enclosed; only what was asked for (the black
market and its merchants, the pirates' docks, tunnels and highways, construction around the rock); working lamps, not neon.
"""
import math
import random

from hub_layouts import City, CLASSES, polar, bearing_of, box

CELL = 12.0
LOBES = [((-260.0, 90.0), 560.0, 470.0, 1.0), ((430.0, -110.0), 400.0, 330.0, 0.85)]   # centre, rx, ry, height


def _n(x, y, seed):
    """Smooth deterministic noise in about [-1, 1]."""
    v = 0.0
    for k, (fx, fy, a) in enumerate(((1 / 170, 1 / 230, 0.5), (1 / 83, 1 / 61, 0.3), (1 / 37, 1 / 43, 0.2))):
        v += a * math.sin(x * fx + seed * 1.7 + k) * math.cos(y * fy - seed * 0.9 + 2 * k)
    return v * 1.6


def field(x, y):
    """> 0 inside the rock; the larger, the deeper."""
    f = max(h * (1 - ((x - c[0]) / rx) ** 2 - ((y - c[1]) / ry) ** 2) for c, rx, ry, h in LOBES)
    return f + 0.10 * _n(x, y, 1)


def seg_dist(p, a, b):
    ax, ay = b[0] - a[0], b[1] - a[1]
    L2 = ax * ax + ay * ay
    t = 0.0 if L2 == 0 else max(0.0, min(1.0, ((p[0] - a[0]) * ax + (p[1] - a[1]) * ay) / L2))
    return math.dist(p, (a[0] + ax * t, a[1] + ay * t))


def poly_dist(p, pts):
    return min(seg_dist(p, a, b) for a, b in zip(pts, pts[1:]))


def build_rock(tunnels, caverns, craters):
    """The rock as columns: [x, y, [[z0, z1], ...], kind] where kind is rock, tunnel, cavern or crater."""
    cols = []
    x0, x1, y0, y1 = -900, 900, -620, 640
    for iy in range(int((y1 - y0) / CELL)):
        for ix in range(int((x1 - x0) / CELL)):
            x, y = x0 + (ix + 0.5) * CELL, y0 + (iy + 0.5) * CELL
            f = field(x, y)
            if f <= 0:
                continue
            s = math.sqrt(f)
            top = 430 * s * (0.8 + 0.3 * _n(x, y, 2)) + 60 * max(0.0, _n(x * 2.3, y * 2.3, 7)) * s + 10
            bot = -480 * s * (0.85 + 0.2 * _n(x, y, 3)) - 10
            kind = "rock"
            for c, r, depth in craters:                      # impact craters: a bowl with a raised rim
                d = math.dist((x, y), c)
                if d < r * 1.25:
                    top += depth * 0.25 * max(0.0, 1 - abs(d - r) / (r * 0.25)) - (depth * (1 - (d / r) ** 2) if d < r else 0)
            spans = [[bot, top]]
            for c, r, floor, ceil in caverns:
                if math.dist((x, y), c) < r:
                    spans = [[bot, floor]] + ([[ceil, top]] if ceil is not None and top > ceil + 6 else [])
                    kind = "crater" if ceil is None else "cavern"
            if kind == "rock":
                for pts, w in tunnels:
                    if poly_dist((x, y), pts) < w / 2:
                        spans = [[bot, -32]] + ([[32, top]] if top > 40 else [])
                        kind = "tunnel"
                        break
            spans = [[round(a, 1), round(b, 1)] for a, b in spans if b - a > 2]
            if spans:
                cols.append([round(x, 1), round(y, 1), spans, kind])
    return cols


def surface(x, y):
    """The rock's top at (x, y), before craters: where things bolted to the high ground sit."""
    f = field(x, y)
    s = math.sqrt(max(f, 0.0))
    return 430 * s * (0.8 + 0.3 * _n(x, y, 2)) + 60 * max(0.0, _n(x * 2.3, y * 2.3, 7)) * s + 10


def edge_point(b, c, step=6.0):
    """Where a ray from c on bearing b leaves the rock."""
    r = 0.0
    while field(*polar(b, r, c)) > 0 and r < 1600:
        r += step
    return polar(b, r, c), r


def den(C, c, r, district, name, liv, cls, rng):
    """A merchant's den: a small cavern off a tunnel, its stock stacked round the wall, the merchant's ship moored in it,
    one lit sign over the door."""
    for k in range(int(r / 10)):
        a = k * 360 / int(r / 10) + rng.uniform(-6, 6)
        q = polar(a, r - 14, c)
        C.b(box(q, a + 90, rng.uniform(14, 24), rng.uniform(10, 16)), rng.uniform(-6, 8), -rng.uniform(30, 60), "cargo", district,
            lit=rng.uniform(0.2, 0.5), roof=rng.choice([liv, None]))
    C.ship(polar(rng.uniform(0, 360), r * 0.35, c), rng.uniform(0, 360), cls, liv, "docked")
    C.fx.append(dict(type="neon", c=polar(200, r - 6, c), heading=110, length=18, height=7, z=16, col="#FFB000"))
    for k in range(4):
        C.fx.append(dict(type="flash", c=polar(k * 90 + 45, r * 0.7, c), z=30, r=2.5, col="#FFC070"))
    C.holos.append(dict(text=name, at=c, district=district, small=True))


def option_e():
    C = City("E", 21)
    rng = random.Random(21)
    A, B = LOBES[0][0], LOBES[1][0]

    # ---- tunnels at the flight plane: the Ring highway round both lobes, the Maw in from the south, the back door
    # out the east, spurs to the dens, the docks and the Den
    ring = [(-60, -200), (-360, -200), (-560, 0), (-500, 260), (-260, 400), (40, 290), (300, 140), (560, 20), (640, -160),
            (470, -300), (230, -260), (40, -210), (-60, -200)]
    maw = [(0, -900), (0, -420), (-20, -205)]
    back = [(640, -160), (820, -200), (960, -230)]
    to_den = [(560, 20), (640, 90)]
    spurs = [[(-200, -200), (-290, -110)], [(470, -300), (440, -215)], [(40, -210), (100, -120)], [(40, 290), (100, 245)],
             [(-560, 0), (-570, -60)], [(-260, 400), (-110, 345)]]
    tunnels = [(ring, 84), (maw, 96), (back, 60), (to_den, 70)] + [(s_, 50) for s_ in spurs]

    # ---- caverns, all enclosed: the Market, the pirates' Docks, four merchants' dens
    market, docks = (-330, 120), (430, -80)
    dens = [((-570, -135), 75, "GUNSMITH", "khu", "hauler"), ((115, 205), 62, "PARTS", "tlax", "hauler"),
            ((110, -60), 70, "SALVAGE", "scav", "tug"), ((-90, 320), 56, "SMUGGLER", "vest", "courier")]
    caverns = [(market, 205, -80, 110), (docks, 135, -70, 90)] + [(c, r, -50, 70) for c, r, _, _, _ in dens]
    rock = build_rock(tunnels, caverns, [])

    # ---- the lanes the traffic flies
    C.lane(ring, "tunnel", closed=True, oneway=False)
    C.traffic_on(ring, 10, [("hauler", "khu"), ("courier", "scav"), ("wraith", "khu"), ("tug", "scav"), ("shuttle", "civ")], 51)
    C.lane([(30, -1700), (30, -900), (20, -420), (-10, -215)], "approach_in")
    C.lane([(-30, -215), (-30, -420), (-30, -900), (-30, -1700)], "approach_out")
    C.traffic_on([(30, -1700), (30, -900), (20, -420)], 2, [("hauler", "khu"), ("wraith", "scav")], 52)
    C.traffic_on([(-30, -420), (-30, -900), (-30, -1700)], 2, [("hauler", "scav"), ("tug", "scav")], 53, 0.4)
    for t_ in [back, to_den] + spurs:
        C.lane(t_, "tunnel", oneway=False)
    # working lamps: a sodium strip along the highway walls, lamps at intervals in the side tunnels
    for pts, w in tunnels:
        for s_ in (-1, 1):
            side = []
            for a, b in zip(pts, pts[1:]):
                hd = math.degrees(math.atan2(b[0] - a[0], b[1] - a[1]))
                side += [polar(hd + 90, s_ * (w / 2 - 4), a), polar(hd + 90, s_ * (w / 2 - 4), b)]
            if w > 80:
                C.fx.append(dict(type="neon_line", pts=[(round(x, 1), round(y, 1)) for x, y in side], z=-24, col="#C8782A"))
            else:
                for a, b in zip(side[::2], side[1::2]):
                    n = max(1, int(math.dist(a, b) / 40))
                    for k in range(n):
                        f = (k + 0.5) / n
                        C.fx.append(dict(type="flash", c=(a[0] + (b[0] - a[0]) * f, a[1] + (b[1] - a[1]) * f), z=-20, r=2.0,
                                         col="#FFC070"))

    # ---- the Maw: the front door, two gun emplacements on its lip, docking lights down its throat
    mouth, _ = edge_point(180, (0, -420))
    for s_ in (1, -1):
        p = (s_ * 95, mouth[1] + 40)
        C.shape("sphere", p, "maw", "turret", 0.6, 30, r=14)
        for t_ in (-1, 1):
            C.shape("truss", (p[0] + t_ * 5, p[1] - 30), "maw", "barrel", 0.0, 34, heading=180, length=46, width=3)
        C.fx.append(dict(type="turret", c=p, facing=180 + s_ * 15, reach=520, arc=100))
    for k in range(6):
        for s_ in (1, -1):
            C.fx.append(dict(type="flash", c=(s_ * 44, mouth[1] - 120 + k * 60), z=0, r=3, col="#FFF0D0" if k % 2 else "#FFB347"))
    C.fx.append(dict(type="neon", c=(0, mouth[1] - 6), heading=90, length=60, height=10, z=70, col="#FF5530"))
    C.holos.append(dict(text="THE MAW", at=(0, mouth[1] - 70), district="maw", small=True))

    # ---- the approach: an ordinary rock field round the asteroid, a few navigation beacons on the way in
    for k in range(44):
        x = rng.choice([-1, 1]) * rng.uniform(150, 900); y = rng.uniform(-1600, -560)
        C.fx.append(dict(type="boulder", c=(round(x, 1), round(y, 1)), r=round(rng.uniform(8, 45), 1)))
    for y in range(-1500, -560, 260):
        C.fx.append(dict(type="flash", c=(90, y), z=0, r=4, col="#FFB347"))
        C.fx.append(dict(type="flash", c=(-90, y), z=0, r=4, col="#FFB347"))
    spawn = dict(c=(30, -1700), heading=0)

    # ---- the construction built onto and round the rock: docking spars, modules bolted to the cliffs, scaffolding
    # over the flanks, a girder belt round the waist, radiators, masts, the ore mass driver
    for b, c in ((235, A), (290, A), (330, A), (20, A), (60, B), (110, B), (150, B)):
        p, r = edge_point(b, c)
        L = rng.uniform(150, 240)
        C.shape("truss", polar(b, L / 2 - 20, p), "outer", "spar", 0.6, 0, heading=b, length=L, width=8)
        for j in range(rng.randint(1, 3)):
            cls = rng.choice(["hauler", "wraith", "courier", "tug"])
            side = 1 if j % 2 else -1
            C.ship(polar(b + 90, side * (CLASSES[cls][1] / 2 + 8), polar(b, 30 + j * 55, p)), b + 90 * side, cls,
                   rng.choice(["khu", "khu", "scav", "scav", "vest", "tlax"]), "docked")
        C.fx.append(dict(type="flash", c=polar(b, L - 20, p), z=8, r=3, col="#FF3C28"))
    for k in range(30):
        c = rng.choice([A, B])
        b = rng.uniform(0, 360)
        if c == A and abs((b - 180 + 180) % 360 - 180) < 25:       # keep the Maw clear
            continue
        p, r = edge_point(b, c)
        q = polar(b, -rng.uniform(10, 50), p)
        L_, W_ = rng.uniform(30, 80), rng.uniform(18, 36)
        top = min(surface(*q) + rng.uniform(10, 40), 240)
        C.b(box(q, b + rng.choice([0, 90]), L_, W_), top, -rng.uniform(60, 200), "hab", "outer", lit=rng.uniform(0.4, 0.8))
    for k in range(7):                                            # scaffolding over the flanks: a lattice of girders
        c = rng.choice([A, B])
        p = polar(rng.uniform(0, 360), rng.uniform(120, 320) * (1 if c == A else 0.7), c)
        hd = rng.uniform(0, 180)
        for i in range(4):
            for s_ in (0, 90):
                q = polar(hd + s_, (i - 1.5) * 28, p)
                C.shape("truss", q, "outer", "gantry", 0.8, surface(*q) + 14, heading=hd + 90 - s_, length=100, width=3)
    belt = []                                                     # the girder belt hugging the rock at its waist
    for b in range(0, 360, 6):
        c = A if (b > 200 or b < 20) else B
        p, r = edge_point(b, c)
        belt.append(polar(b, 22, p))
    for a, b in zip(belt, belt[1:] + belt[:1]):
        if math.dist(a, b) > 160 or (abs(a[0]) < 120 and a[1] < -250):   # not across the lobes' join, not over the Maw
            continue
        mid = ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2)
        C.shape("truss", mid, "outer", "gantry", 0.8, 70, heading=math.degrees(math.atan2(b[0] - a[0], b[1] - a[1])),
                length=math.dist(a, b), width=6)
    for k in range(4):
        p = polar(rng.uniform(0, 360), rng.uniform(80, 300), A)
        for f in range(6):
            q = polar(90, (f - 2.5) * 8, p)
            C.shape("panel", q, "outer", "radiator", 0.0, surface(*q) + 8, heading=0, length=70, thick=1.5, height=24)
    for k in range(5):
        p = polar(rng.uniform(0, 360), rng.uniform(60, 260), rng.choice([A, B]))
        hm = rng.uniform(70, 140)
        C.shape("panel", p, "outer", "mast", 0.9, surface(*p) + hm / 2, heading=0, length=5, thick=5, height=hm)
    md, _ = edge_point(30, B)
    C.shape("truss", polar(30, 200, md), "outer", "rail", 0.6, 20, heading=30, length=420, width=10)
    for k in range(5):
        C.fx.append(dict(type="canister", c=polar(30, 80 + k * 70, md)))

    # ---- the Market: the black market in the rock's largest cavern, stalls on ledges round its wall, stolen cargo on
    # the floor, the Quartermaster's console the one reset
    for k in range(30):
        a = k * 12 + rng.uniform(-3, 3)
        q = polar(a, 186, market)
        C.b(box(q, a + 90, rng.uniform(22, 34), rng.uniform(14, 20)), rng.uniform(-4, 10), -rng.uniform(40, 80), "stall", "bazaar",
            lit=rng.uniform(0.5, 0.9), roof=rng.choice(["khu", "scav", "vest", None]))
        if k % 3 == 0:
            C.fx.append(dict(type="flash", c=polar(a, -16, q), z=26, r=2.5, col="#FFC070"))
        if rng.random() < 0.35:
            cls = rng.choice(["shuttle", "courier", "tug", "hauler"])
            C.ship(polar(a, -(22 + CLASSES[cls][1] / 2), q), a + 90, cls, rng.choice(["khu", "scav", "vest", "tlax", "civ"]), "docked")
    for k in range(5):
        C.b(box(polar(k * 72 + 20, 80, market), k * 72, rng.uniform(40, 70), rng.uniform(24, 36)), -46, -80, "cargo", "bazaar",
            lit=0.2, roof="khu")
    C.fx.append(dict(type="lab", c=market))
    C.lane([polar(a, 125, market) for a in range(0, 360, 6)], "street", closed=True, oneway=False)
    C.traffic_on([polar(a, 125, market) for a in range(0, 360, 6)], 5, [("shuttle", "civ"), ("courier", "scav"), ("tug", "khu")], 54)
    C.holos.append(dict(text="THE MARKET", at=market, district="bazaar", small=True))
    C.district("bazaar", "THE MARKET", market, "The black market in the rock's largest cavern: stolen blocks, unregistered "
               "weapons, salvage with the serials ground off. The Quartermaster's console is the one reset.",
               ["Black-market blocks and weapons", "The Quartermaster: reset everything"], None)

    # ---- the merchants' dens off the tunnels, each one dealer
    for c, r, name, liv, cls in dens:
        den(C, c, r, "fence", name, liv, cls, rng)
    C.district("fence", "THE DENS", dens[0][0], "Single dealers in small caverns off the tunnels: a gunsmith, a parts broker, "
               "a salvage broker, a smuggler.", ["GUNSMITH: weapons", "PARTS: blocks and engines", "SALVAGE: buys and sells wreckage",
                                                 "SMUGGLER: contraband cargo"], None)

    # ---- the Docks: the pirates' berths inside the rock, crews' ships along the cavern wall, repairs under way
    for k in range(9):
        a = k * 40 + 10
        cls = rng.choice(["wraith", "wraith", "cruiser", "hauler"])
        q = polar(a, 135 - CLASSES[cls][1] / 2 - 14, docks)
        C.shape("truss", polar(a, 135 - 8, docks), "chop", "arm", 0.6, 0, heading=a, length=20, width=5)
        C.ship(q, a + 90, cls, rng.choice(["khu", "scav", "scav"]), "docked")
        if k % 3 == 0:
            C.fx.append(dict(type="flash", c=q, z=10, r=2.5, col="#BFE8FF"))
    C.holos.append(dict(text="THE DOCKS", at=docks, district="chop", small=True))
    C.district("chop", "THE DOCKS", docks, "The pirates' berths inside the rock; crews' ships along the cavern wall.",
               ["Pirate crews and their ships"], None)

    # ---- the Den: an old warship rammed into the rock long ago, its hangar the Clan's own
    den_c = (760, 150)
    C.fx.append(dict(type="hulk", c=den_c, heading=55, length=460, width=120, bow=True, cut_back=False))
    for k in range(4):
        C.ship(polar(55 + 90, (k - 1.5) * 26, polar(55, -60 + (k % 2) * 40, den_c)), 235, rng.choice(["wraith", "cruiser"]), "khu", "docked")
    for k in range(6):
        C.fx.append(dict(type="flash", c=polar(55, -150 + k * 50, polar(145, 62, den_c)), z=30, r=3, col="#FFC070"))
    C.holos.append(dict(text="THE DEN", at=den_c, district="den", small=True))
    C.district("den", "THE DEN", den_c, "A warship rammed into the rock long ago; its hangar is the Clan's own.", [], None)
    C.district("outer", "THE OUTER WORKS", polar(300, 300, A), "Spars, modules on the cliffs, scaffolding, the girder belt.", [], None)
    C.district("maw", "THE MAW", mouth, "The front door.", [], None)

    callouts = [
        ((30, -1700), "You spawn outside the rock field; the beacons lead to the Maw"),
        ((0, mouth[1]), "The Maw: the front door, two gun emplacements, docking lights"),
        ((-450, 330), "The Ring: the highway inside the rock, both ways, round both lobes"),
        (market, "The Market: the black market in the largest cavern; the Quartermaster is the one reset"),
        (dens[0][0], "The dens: a gunsmith, a parts broker, a salvage broker, a smuggler"),
        (docks, "The Docks: the pirates' berths inside the rock"),
        (den_c, "The Den: the Clan's hangar in a rammed warship"),
        ((960, -230), "The back door: a second way out"),
        (polar(300, 400, A), "Outside: spars, modules on the cliffs, scaffolding, the girder belt, the mass driver"),
    ]
    return dict(key="E", title="OPTION E  ·  THE HOLLOW", name="THE HOLLOW",
                subtitle="A pirate haven built into an asteroid: tunnels and caverns inside, construction built onto the rock.",
                spawn=spawn, bounds=(-1250, -1800, 1300, 900), wall=0, seed=21, rock=dict(cell=CELL, cols=rock),
                concept=("The Khuchad Clan's haven, built into a two-lobed asteroid 1.7 km long. You come in through the rock "
                         "field along a few beacons to the Maw. Inside, the Ring highway runs round both lobes at the flight "
                         "plane, two ways, lit by working lamps. Off it are the Market (the black market, in the largest "
                         "cavern), four merchants' dens, the pirates' Docks and the Den, a warship rammed into the rock. "
                         "Outside, the station is built onto the rock: docking spars, modules bolted to the cliffs, scaffolding, "
                         "a girder belt round its waist and an ore mass driver. Inside the rock the roof hides round you, so the "
                         "tunnels fly like streets."),
                strengths=["Small and quiet: about forty ships, every one with a reason to be there",
                           "Tunnels and caverns are a new kind of flying: tight, two-way, lit",
                           "A different feel from Meridian: rough, hidden, one clan's rock"],
                costs=["Tunnels need the roof cutaway round the player (the camera looks down)",
                       "The rock is voxels at 12 m: about 8,000 columns to build and to collide with"],
                play_shots=[("market", market, 650), ("ring", (-480, 120), 650), ("docks", docks, 520), ("maw", (0, mouth[1] - 60), 700),
                            ("den", den_c, 800), ("dens", (115, 120), 520)],
                callouts=callouts, **C.data())
