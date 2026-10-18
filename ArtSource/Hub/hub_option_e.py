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
customers, and what they take is fenced, chopped and sold.
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


def option_e():
    C = City("E", 21)
    rng = random.Random(21)
    A, B = LOBES[0][0], LOBES[1][0]

    # ---- tunnels at the flight plane: the Ring highway round both lobes, the Maw in from the south, spurs to the
    # caverns, the Smugglers' Run out the back to a hidden gate, a vent north, the passage into the Den
    ring = [(-60, -200), (-360, -200), (-560, 0), (-500, 260), (-260, 400), (40, 290), (300, 140), (560, 20), (640, -160),
            (470, -300), (230, -260), (40, -210), (-60, -200)]
    maw = [(0, -900), (0, -420), (-20, -205)]
    run = [(640, -160), (820, -200), (1000, -260)]
    vent = [(-260, 400), (-240, 720)]
    den = [(560, 20), (640, 90)]
    spurs = [[(-200, -200), (-290, -80)], [(470, -300), (440, -200)], [(40, -210), (95, -125)], [(300, 140), (160, 20)]]
    tunnels = [(ring, 84), (maw, 96), (run, 56), (vent, 50), (den, 70)] + [(s, 56) for s in spurs]

    # ---- caverns: (centre, radius, floor, ceiling); a ceiling of None is open to space
    bazaar, chop, fence, drop, furnace = (-330, 120), (430, -80), (-560, -130), (120, 255), (110, -60)
    caverns = [(bazaar, 215, -70, None), (chop, 130, -60, 90), (fence, 100, -50, 70), (drop, 105, -90, 80),
               (furnace, 80, -340, None)]
    craters = [((-620, 330), 90, 60), ((250, 380), 70, 50), ((640, -330), 60, 40), ((-120, -390), 50, 30)]
    rock = build_rock(tunnels, caverns, craters)

    # ---- the lanes the traffic flies: the Ring both ways, in and out through the Maw, the Run
    C.lane(ring, "tunnel", closed=True, oneway=False)
    C.traffic_on(ring, 12, [("hauler", "khu"), ("courier", "scav"), ("wraith", "khu"), ("tug", "scav"), ("shuttle", "civ")], 51)
    C.lane([(30, -1900), (30, -900), (20, -420), (-10, -215)], "approach_in")
    C.lane([(-30, -215), (-30, -420), (-30, -900), (-30, -1900)], "approach_out")
    C.traffic_on([(30, -1900), (30, -900), (20, -420)], 3, [("hauler", "khu"), ("wraith", "scav"), ("courier", "vest")], 52)
    C.traffic_on([(-30, -420), (-30, -900), (-30, -1900)], 2, [("hauler", "scav"), ("tug", "scav")], 53, 0.4)
    C.lane(run, "tunnel", oneway=False)
    C.lane(vent, "tunnel", oneway=False)
    for s in spurs + [den]:
        C.lane(s, "tunnel", oneway=False)
    # light strips along every tunnel wall
    for pts, w in tunnels:
        for s in (-1, 1):
            side = []
            for a, b in zip(pts, pts[1:]):
                hd = math.degrees(math.atan2(b[0] - a[0], b[1] - a[1]))
                side += [polar(hd + 90, s * (w / 2 - 4), a), polar(hd + 90, s * (w / 2 - 4), b)]
            C.fx.append(dict(type="neon_line", pts=[(round(x, 1), round(y, 1)) for x, y in side], z=-24,
                             col="#FFB347" if w > 80 else "#9FB4C8"))

    # ---- the Maw: the one front door, a gun-lined throat with a field gate and the Clan's sign over it
    mouth, _ = edge_point(180, (0, -420))
    for s in (1, -1):
        for k, dy in enumerate((0, 70, 150)):
            p = (s * (70 + k * 10), mouth[1] + dy)
            C.shape("sphere", p, "maw", "turret", 0.6, 34, r=14)
            for t_ in (-1, 1):
                C.shape("truss", (p[0] + t_ * 5, p[1] - 30), "maw", "barrel", 0.0, 38, heading=180, length=46, width=3)
            C.fx.append(dict(type="turret", c=p, facing=180 + s * 15, reach=520, arc=100))
    C.fx.append(dict(type="field_wall", a=(-48, mouth[1] - 10), b=(48, mouth[1] - 10)))
    C.fx.append(dict(type="scanner", a=(-46, mouth[1] + 40), b=(46, mouth[1] + 40)))
    for k in range(10):
        C.fx.append(dict(type="neon", c=(rng.uniform(-90, 90), mouth[1] + rng.uniform(-30, 10)), heading=90, length=rng.uniform(14, 40),
                         height=rng.uniform(6, 14), z=rng.uniform(50, 140), col=rng.choice(["#FF2E88", "#FFB000", "#FF5530", "#22E6FF"])))
    C.holos.append(dict(text="THE HOLLOW", at=(0, mouth[1] - 40), district="maw"))
    C.district("maw", "THE MAW", mouth, "The one front door: a gun-lined throat, a field gate, the Clan's scan.", [], None)

    # ---- the approach: no picket, but a maze: boulders and mines either side of a channel marked by red lamps,
    # captured prizes chained outside as a warning and an advertisement
    for k in range(70):
        x = rng.choice([-1, 1]) * rng.uniform(140, 900); y = rng.uniform(-1750, -560)
        if abs(x) < 160:
            continue
        C.fx.append(dict(type="boulder", c=(round(x, 1), round(y, 1)), r=round(rng.uniform(10, 55), 1)))
    for s in (1, -1):
        C.fx.append(dict(type="minefield", c=(s * 330, -1250), r=170, n=20, kind="prox"))
        C.fx.append(dict(type="minefield", c=(s * 520, -820), r=150, n=16, kind="seeker"))
    for y in range(-1800, -560, 140):
        for s in (1, -1):
            C.fx.append(dict(type="flash", c=(s * 120, y), z=0, r=4, col="#FF3C28"))
    for k, (c, hd) in enumerate((((-420, -720), 30), ((480, -600), 320), ((-700, -420), 70))):
        C.fx.append(dict(type="hulk", c=c, heading=hd, length=rng.uniform(110, 170), width=rng.uniform(30, 44), bow=True,
                         cut_back=True))
        anchor, _ = edge_point(bearing_of((c[0] - A[0], c[1] - A[1])) if k != 1 else 200, A if k != 1 else B)
        C.fx.append(dict(type="beam", a=c, za=0, b=anchor, zb=0, col="#7A7F86"))
    spawn = dict(c=(30, -1900), heading=0)
    C.fx.append(dict(type="marker", c=(30, -1500)))

    # ---- outside: the station grown over the rock. Docking spars with the Clan's and its customers' ships, habitat
    # blocks bolted to the cliffs, a hab ring on the high ground, radiators, masts and the ore mass driver
    for b, c in ((235, A), (290, A), (330, A), (20, A), (60, B), (110, B), (150, B)):
        p, r = edge_point(b, c)
        L = rng.uniform(150, 260)
        C.shape("truss", polar(b, L / 2 - 20, p), "outer", "spar", 0.6, 0, heading=b, length=L, width=8)
        for j in range(rng.randint(2, 4)):
            cls = rng.choice(["hauler", "wraith", "courier", "cruiser", "tug"])
            side = 1 if j % 2 else -1
            C.ship(polar(b + 90, side * (CLASSES[cls][1] / 2 + 8), polar(b, 30 + j * 55, p)), b + 90 * side, cls,
                   rng.choice(["khu", "khu", "scav", "scav", "vest", "tlax"]), "docked")
        C.fx.append(dict(type="flash", c=polar(b, L - 20, p), z=8, r=3, col="#FF3C28"))
    for k in range(26):
        c = rng.choice([A, B])
        b = rng.uniform(0, 360)
        p, r = edge_point(b, c)
        if abs(((b - 180) + 180) % 360 - 180) < 25 and c == A:        # keep the Maw clear
            continue
        q = polar(b, -rng.uniform(10, 60), p)
        L_, W_ = rng.uniform(30, 90), rng.uniform(18, 40)
        top = rng.uniform(40, 180)
        C.b(box(q, b + rng.choice([0, 90]), L_, W_), top, -rng.uniform(60, 200), "hab", "outer", lit=rng.uniform(0.5, 1.0))
        if rng.random() < 0.6:
            C.fx.append(dict(type="neon", c=polar(b, 6, q), heading=b + 90, length=rng.uniform(10, 30), height=rng.uniform(6, 14),
                             z=round(top + 10, 1), col=rng.choice(["#FF2E88", "#22E6FF", "#FFB000", "#9DFF3C"])))
    hr = (-20, 60)
    C.shape("torus", hr, "outer", "hab_ring", 0.9, surface(*hr) + 40, R=130, r=14, spokes=4, hub=28)
    C.shape("sphere", hr, "outer", "hub", 0.8, surface(*hr) + 40, r=28)
    C.shape("panel", hr, "outer", "mast", 0.9, surface(*hr) + 20, heading=0, length=10, thick=10, height=40)
    for k in range(5):
        p = polar(rng.uniform(0, 360), rng.uniform(80, 300), A)
        if math.dist(p, bazaar) < 260 or math.dist(p, hr) < 170:
            continue
        for f in range(6):
            q = polar(90, (f - 2.5) * 8, p)
            C.shape("panel", q, "outer", "radiator", 0.0, surface(*q) + 8, heading=0, length=70, thick=1.5, height=24)
    for k in range(6):
        p = polar(rng.uniform(0, 360), rng.uniform(60, 260), rng.choice([A, B]))
        hm = rng.uniform(80, 160)
        C.shape("panel", p, "outer", "mast", 0.9, surface(*p) + hm / 2, heading=0, length=5, thick=5, height=hm)
    md, _ = edge_point(30, B)
    C.shape("truss", polar(30, 220, md), "outer", "rail", 0.6, 20, heading=30, length=480, width=10)
    for k in range(6):
        C.fx.append(dict(type="canister", c=polar(30, 80 + k * 70, md)))
    C.district("outer", "THE OUTER WORKS", polar(300, 300, A), "Docking spars, habitat blocks bolted to the cliffs, the hab "
               "ring on the high ground, radiators and the ore mass driver.", [], None)

    # ---- the Black Bazaar: an old crater roofed by nothing, its walls terraced with stalls at the flight plane
    for k in range(34):
        a = k * 360 / 34 + rng.uniform(-3, 3)
        q = polar(a, 196, bazaar)
        col = rng.choice(["#FF2E88", "#22E6FF", "#FFB000", "#9DFF3C", "#B44CFF"])
        C.b(box(q, a + 90, rng.uniform(22, 36), rng.uniform(14, 22)), rng.uniform(-4, 12), -rng.uniform(40, 90), "stall", "bazaar",
            lit=rng.uniform(0.7, 1.0), roof=rng.choice(["khu", "scav", "vest", None]))
        C.fx.append(dict(type="neon", c=polar(a, -14, q), heading=a + 90, length=rng.uniform(12, 26), height=rng.uniform(6, 12),
                         z=rng.uniform(16, 40), col=col))
        if rng.random() < 0.45:
            cls = rng.choice(["shuttle", "courier", "tug", "hauler"])
            C.ship(polar(a, -(22 + CLASSES[cls][1] / 2), q), a + 90, cls, rng.choice(["khu", "scav", "vest", "tlax", "civ"]), "docked")
    for k in range(3):                                             # the stacks of stolen cargo on the crater floor
        C.b(box(polar(k * 120 + 30, 80, bazaar), k * 120, 70, 40), -40, -70, "cargo", "bazaar", lit=0.2, roof="khu")
    C.fx.append(dict(type="lab", c=bazaar))                        # the Quartermaster: the one reset
    C.lane([polar(a, 130, bazaar) for a in range(0, 360, 6)], "street", closed=True, oneway=False)
    C.traffic_on([polar(a, 130, bazaar) for a in range(0, 361, 6)], 6, [("shuttle", "civ"), ("courier", "scav"), ("tug", "khu")], 54)
    C.district("bazaar", "THE BLACK BAZAAR", bazaar, "An open crater, its walls terraced with stalls: stolen blocks, "
               "unregistered weapons, salvage with the serials ground off. The Quartermaster's console is the one reset.",
               ["Black-market blocks and weapons", "The Quartermaster: reset everything"], "BAZAAR")

    # ---- the Chop Shop: stolen ships stripped for parts, and the cradle where you refit your own, no questions
    for k, (dx, dy, hd, cls) in enumerate(((-40, 40, 70, "cruiser"), (50, -30, 250, "hauler"), (-20, -70, 100, "wraith"))):
        c = (chop[0] + dx, chop[1] + dy)
        C.ship(c, hd, cls, rng.choice(["vest", "tlax"]), "docked")
        L, W = CLASSES[cls]
        for s in (-1, 1):
            C.shape("truss", polar(hd + 90, s * (W / 2 + 12), c), "chop", "gantry", 0.8, 10, heading=hd, length=L + 20, width=5)
        for j in range(3):
            C.fx.append(dict(type="flash", c=polar(hd, rng.uniform(-L / 3, L / 3), polar(hd + 90, rng.choice([-1, 1]) * W * 0.4, c)),
                             z=8, r=2.5, col="#BFE8FF"))
    C.shape("torus", (chop[0] + 70, chop[1] + 60), "chop", "coil", 1.0, 0, R=34, r=5, upright=True, heading=40)
    C.holos.append(dict(text="REFIT", at=(chop[0] + 70, chop[1] + 60), district="chop", small=True))
    C.district("chop", "THE CHOP SHOP", chop, "Stolen ships stripped for parts under gantries; the refit cradle edits yours.",
               ["Refit cradle: edit your ship", "Parts off stolen hulls"], "CHOP SHOP")

    # ---- the Fence: brokers for anything taken off someone else's ship, their haulers alongside
    for k in range(2):
        q = polar(k * 140 + 20, 45, fence)
        for i in range(3):
            for j in range(4):
                C.b(box(polar(k * 140 + 110, (j - 1.5) * 24, polar(k * 140 + 20, (i - 1) * 13, q)), k * 140 + 110, 22, 10), 6, -6, "cargo",
                    "fence", lit=0.15, roof=rng.choice(["khu", "scav", None]))
    C.ship((fence[0] + 30, fence[1] - 50), 60, "hauler", "scav", "docked")
    C.district("fence", "THE FENCE", fence, "Brokers for stolen cargo and salvage.", ["Sell anything, no papers"], "FENCE")

    # ---- the Drop: a pit where crews settle things, ledges of spectators round it
    C.shape("torus", drop, "drop", "stands", 0.95, -30, R=92, r=10)
    C.shape("torus", drop, "drop", "stands", 0.95, -60, R=74, r=8)
    C.fx.append(dict(type="arena", c=drop, r=80))
    s1, s2 = polar(60, 40, drop), polar(250, 40, drop)
    C.ship(s1, 240, "wraith", "khu", "cruise"); C.ship(s2, 60, "wraith", "scav", "cruise")
    C.fx.append(dict(type="beam", a=s1, za=0, b=polar(70, 12, s2), zb=0, col="#FF7A2F"))
    C.district("drop", "THE DROP", drop, "A pit in the rock where crews settle things; the ledges round it fill up.",
               ["Duels and brawls"], "THE DROP")

    # ---- the Furnace: the reactor in the rock's waist, in a shaft open to space; its pulse sweeps the tunnels by it
    C.fx.append(dict(type="reactor", c=furnace, r=160, light=2.5e7))
    for k in range(4):
        C.shape("panel", polar(k * 90 + 45, 64, furnace), "furnace", "bulkhead", 0.2, 0, heading=k * 90 + 135, length=40, thick=8,
                height=50)
    C.district("furnace", "THE FURNACE", furnace, "The haven's reactor in a shaft through the rock; its pulse sweeps the tunnels "
               "beside it. Hide behind the bulkheads.", ["The reactor pulse: shadows are shelter"], None)

    # ---- the Den: an old warship rammed into the rock long ago, its hangar the Clan's own
    den_c = (760, 150)
    C.fx.append(dict(type="hulk", c=den_c, heading=55, length=460, width=120, bow=True, cut_back=False))
    for k in range(5):
        C.ship(polar(55 + 90, (k - 2) * 26, polar(55, -60 + (k % 2) * 40, den_c)), 235, rng.choice(["wraith", "cruiser"]), "khu", "docked")
    for k in range(8):
        C.fx.append(dict(type="flash", c=polar(55, -200 + k * 50, polar(145, 62, den_c)), z=30, r=3, col="#FF7A2F"))
    C.district("den", "THE DEN", den_c, "A warship rammed into the rock long ago; its hangar is the Clan's own.",
               ["The Clan's crews and their ships"], "THE DEN")

    # ---- the back door: the Smugglers' Run ends at a lane gate nobody admits to
    C.fx.append(dict(type="portal", c=(1060, -280), facing=110, width=80))
    C.district("run", "THE SMUGGLERS' RUN", (900, -230), "A back tunnel to a lane gate nobody admits to.", [], None)

    callouts = [
        ((30, -1900), "You spawn outside; follow the red lamps through the boulders and mines"),
        ((0, mouth[1]), "The Maw: the one front door, guns, a field gate, the Clan's scan"),
        (bazaar, "The Black Bazaar: an open crater of stalls; the Quartermaster is the one reset"),
        ((-450, 330), "The Ring: the highway inside the rock, both ways, round both lobes"),
        (chop, "The Chop Shop: stolen ships stripped; refit your own"),
        (fence, "The Fence: sell anything, no papers"),
        (drop, "The Drop: a fighting pit in the rock"),
        (furnace, "The Furnace: the reactor shaft; its pulse sweeps the tunnels"),
        (den_c, "The Den: the Clan's hangar in a rammed warship"),
        ((1060, -280), "The Smugglers' Run: the back door to a hidden gate"),
        (polar(300, 400, A), "The Outer Works: spars, cliff habitats, the hab ring"),
    ]
    return dict(key="E", title="OPTION E  ·  THE HOLLOW", name="THE HOLLOW",
                subtitle="A pirate haven built into an asteroid: tunnels and caverns inside, the station grown over the rock.",
                spawn=spawn, bounds=(-1250, -2000, 1300, 900), wall=0, seed=21, rock=dict(cell=CELL, cols=rock),
                concept=("The Khuchad Clan's haven, cut into a two-lobed asteroid 1.7 km long. You come in through a maze of "
                         "boulders and mines along a channel of red lamps to the Maw, the one front door. Inside, the Ring "
                         "highway runs round both lobes at the flight plane, two ways, lit along its walls, with caverns off it: "
                         "the Black Bazaar in an open crater, the Chop Shop, the Fence, the Drop, the Furnace in the rock's waist "
                         "and the Den in a warship rammed into the rock. Outside, the station has grown over the rock: docking "
                         "spars, habitat blocks bolted to the cliffs, a hab ring on the high ground, the ore mass driver. Inside "
                         "the rock the roof hides round you, so the tunnels fly like streets."),
                strengths=["Small and intimate: a few dozen ships, every one with a reason to be there",
                           "Tunnels and caverns are a new kind of flying: tight, lit, two-way",
                           "A different feel from Meridian: rough, hidden, run by one clan"],
                costs=["Tunnels need the roof cutaway round the player (the camera looks down)",
                       "The rock is voxels at 12 m: about 8,000 columns to build and to collide with"],
                play_shots=[("bazaar", bazaar, 700), ("ring", (-480, 120), 650), ("chop", chop, 520), ("maw", (0, mouth[1] - 60), 700),
                            ("den", den_c, 800), ("furnace", furnace, 600)],
                callouts=callouts, **C.data())
