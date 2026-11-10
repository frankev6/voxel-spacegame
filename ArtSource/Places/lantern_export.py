"""EXT-007: Tollhouse Nine, "the Lantern", in 3D, from the agreed sketches (sketches.py heist_lantern, lantern.py levels and jobs).

Owner (2026-11-10), on the sketches: "Yes, deepen it"; then "this looks good, and well thought out proceed with building it onto the game".

One sketch cell is 8 m; the town is built of 4 m voxels (two per sketch cell). Plans map to the game's frame as X = (x - 52) * 8 m
east, Z = (40 - y) * 8 m north. The town hangs below the arrival plane (y = 0): the Crown's roof at -8 m, six market galleries down
to -128 m, the Powerhouse (the Lantern) from -136 to -184 m with the core in the Cradle, the docks and the Yards at -184 to -240 m,
Undertown from -240 to -300 m, the outfall out of the bottom.

Writes ArtSource/Places/lantern/lantern_blocks.bin (read by Game/Content/Build Lantern, Assets/Game/Editor/Content/LanternBuilder.cs)
and Assets/Game/App/DevRuntime/Extraction/LanternLayout.g.cs (where things are: the core, tethers, doors, watchers, zones, traffic).

bin: "LANT", int32 version 1, int32 piece count; per piece: id and name (int16 length + UTF-8), float32 cell size, int32 block count,
blocks as int16 i, j, k, byte colour, byte material (0 hull, 1 glass, 2 rock). Little-endian.

Run:  python3 ArtSource/Places/lantern_export.py
"""
import math
import os
import random
import struct

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", ".."))
BIN = os.path.join(HERE, "lantern", "lantern_blocks.bin")
CS = os.path.join(ROOT, "Assets", "Game", "App", "DevRuntime", "Extraction", "LanternLayout.g.cs")
C = 4.0                       # voxel (m)
S = 8.0                       # sketch cell (m)

# palette (LanternBuilder writes these into config_voxel_palette at 60-76)
IVORY, IVORY2, JADE, BRASS, HULL, HULL2, ARMOR, DECK = 60, 61, 62, 63, 64, 65, 66, 67
RUST, TEAL, OCHRE, GLASS, GLOW, RED, TANK, TRUSS, WARM = 68, 69, 70, 71, 72, 73, 74, 75, 76
HULLM, GLASSM, ROCKM = 0, 1, 2

# heights (m, station frame)
CROWN_TOP, CROWN_DECK = 24, -8
MARKET_TOP, MARKET_BOTTOM, FLOORS = -8, -128, 6
POWER_TOP, POWER_BOTTOM = -136, -184
DOCKS_TOP, DOCKS_BOTTOM = -184, -240
UNDER_TOP, UNDER_BOTTOM = -240, -300
WELL_R, ARCH_R, TOWN_R, TRUNK_R = 64, 76, 248, 24
CORE_Y = (POWER_TOP + POWER_BOTTOM) / 2


def X(x):
    return (x - 52) * S


def Z(y):
    return (40 - y) * S


def cell(v):
    return int(math.floor(v / C))


class Vol:
    def __init__(self):
        self.v = {}

    def box(self, x0, x1, y0, y1, z0, z1, col, mat=HULLM, carve=False, test=None):
        for i in range(cell(min(x0, x1)), cell(max(x0, x1) - 1e-3) + 1):
            for k in range(cell(min(z0, z1)), cell(max(z0, z1) - 1e-3) + 1):
                cx, cz = (i + .5) * C, (k + .5) * C
                if test is not None and not test(cx, cz):
                    continue
                for j in range(cell(min(y0, y1)), cell(max(y0, y1) - 1e-3) + 1):
                    if carve:
                        self.v.pop((i, j, k), None)
                    else:
                        self.v[(i, j, k)] = (col(i, j, k) if callable(col) else col, mat)

    def cyl(self, cx, cz, r0, r1, y0, y1, col, mat=HULLM, carve=False, sz=1.0, arc=None):
        """A ring r0..r1 round (cx, cz) (r0 = 0: a disc), squashed along z by sz; arc (a0, a1) in degrees keeps a sector."""
        def test(x, z):
            dx, dz = x - cx, (z - cz) / sz
            d = math.hypot(dx, dz)
            if not (r0 <= d < r1):
                return False
            if arc is not None:
                a = math.degrees(math.atan2(dz, dx)) % 360
                a0, a1 = arc[0] % 360, arc[1] % 360
                return (a0 <= a < a1) if a0 < a1 else (a >= a0 or a < a1)
            return True
        self.box(cx - r1, cx + r1, y0, y1, cz - r1 * sz, cz + r1 * sz, col, mat, carve, test)

    def seg(self, a, b, w, y0, y1, col, mat=HULLM, carve=False):
        """A wall or street of width w (m) from a to b (x, z) in plan."""
        ax, az = a; bx, bz = b
        L = math.hypot(bx - ax, bz - az) or 1
        def test(x, z):
            t = max(0, min(1, ((x - ax) * (bx - ax) + (z - az) * (bz - az)) / (L * L)))
            px, pz = ax + (bx - ax) * t, az + (bz - az) * t
            return math.hypot(x - px, z - pz) <= w / 2
        self.box(min(ax, bx) - w, max(ax, bx) + w, y0, y1, min(az, bz) - w, max(az, bz) + w, col, mat, carve, test)

    def sphere(self, cx, cy, cz, r, col, mat=HULLM, carve=False):
        for i in range(cell(cx - r), cell(cx + r) + 1):
            for j in range(cell(cy - r), cell(cy + r) + 1):
                for k in range(cell(cz - r), cell(cz + r) + 1):
                    if math.dist(((i + .5) * C, (j + .5) * C, (k + .5) * C), (cx, cy, cz)) <= r:
                        if carve:
                            self.v.pop((i, j, k), None)
                        else:
                            self.v[(i, j, k)] = (col, mat)

    def hollow(self):
        keep = {}
        for p, c in self.v.items():
            i, j, k = p
            if all(q in self.v for q in ((i + 1, j, k), (i - 1, j, k), (i, j + 1, k), (i, j - 1, k), (i, j, k + 1), (i, j, k - 1))):
                continue
            keep[p] = c
        self.v = keep


def P(a, r, c=(0, 0)):
    """The plan point at angle a (deg, sketch convention: 0 east, 90 south) and radius r (m) from c (m, x/z)."""
    return (c[0] + r * math.cos(math.radians(a)), c[1] - r * math.sin(math.radians(a)))


def tone(base, alt, seed):
    def f(i, j, k):
        h = (i * 73856093 ^ j * 19349663 ^ k * 83492791 ^ seed) & 0xffff
        return alt if h % 7 == 0 else base
    return f


# ====================================================================================================================
def crown(v):
    """The roof deck with the arch over the Well mouth; the Crown (garrison, bank, customs) west; Pilots' Rest east."""
    y0, y1 = CROWN_DECK - 4, CROWN_DECK
    v.cyl(0, 0, ARCH_R, TOWN_R, y0, y1, tone(DECK, HULL, 1))
    v.cyl(0, 0, WELL_R, ARCH_R, y0, y1 + 4, JADE)                  # the Well's lip
    # the arch: two jade ribs over the mouth, the toll scanners on them
    for a in (90, 0):
        p, q = P(a, ARCH_R), P(a + 180, ARCH_R)
        for t in range(0, 181, 4):
            ang = math.radians(t)
            x = p[0] + (q[0] - p[0]) * t / 180
            z = p[1] + (q[1] - p[1]) * t / 180
            y = y1 + math.sin(ang) * 44
            v.box(x - 4, x + 4, y, y + 6, z - 4, z + 4, JADE)
    # the Crown: ivory halls on the west, rooms inside (garrison, bank, customs)
    x0, x1, z0, z1 = X(8), X(42), Z(54), Z(26)
    v.box(x0, x1, y1, CROWN_TOP, z0, z1, tone(IVORY, IVORY2, 2))
    v.box(X(10), X(26), y1, CROWN_TOP - 4, Z(38), Z(28), 0, carve=True)       # garrison hall
    v.box(X(8) - 4, X(10), y1, CROWN_TOP - 8, Z(36), Z(30), 0, carve=True)    # its hangar door, west
    v.box(X(29), X(40), y1, CROWN_TOP - 4, Z(38), Z(28), 0, carve=True)       # the bank
    v.box(X(10), X(40), y1, CROWN_TOP - 4, Z(52), Z(42), 0, carve=True)       # the customs house
    v.box(X(40), X(43) + 4, y1, CROWN_TOP - 8, Z(49), Z(45), 0, carve=True)   # customs door, east (toward the Well)
    for x in (16, 24, 32):
        v.box(X(x), X(x + 1), y1, CROWN_TOP - 4, Z(48), Z(44), IVORY2)
    v.box(x0, x1, y1 + 15, y1 + 16, Z(40), Z(39), JADE)                       # the jade band
    # Pilots' Rest: the Long Wait's glass dome, the chapel, the clinic, the bunkhouse
    rx, rz = X(74), Z(46)
    for j in range(cell(y1), cell(y1 + 40)):
        y = (j + .5) * C - y1
        r = math.sqrt(max(0, 44 ** 2 - y ** 2))
        v.cyl(rx, rz, max(0, r - 4), r, j * C, j * C + C, GLASS, GLASSM)
    v.cyl(rx, rz, 0, 40, y1, y1 + 4, WARM)
    v.box(X(81), X(86), y1, y1 + 32, Z(40), Z(34), IVORY)                      # chapel
    v.box(X(64), X(71), y1, y1 + 20, Z(34), Z(28), RUST)                       # clinic
    v.box(X(66), X(80), y1, y1 + 16, Z(62), Z(56), TEAL)                       # bunkhouse
    # turret plinths
    for x, y in ((8, 55), (43, 57), (8, 25), (88, 41)):
        v.box(X(x) - 8, X(x) + 8, y1, y1 + 8, Z(y) - 8, Z(y) + 8, ARMOR)


def market(v):
    """Six gallery floors round the Well: an outer wall with windows, the Well wall with eight mouths, stall rows on radial streets."""
    h = (MARKET_TOP - MARKET_BOTTOM) / FLOORS
    v.cyl(0, 0, TOWN_R - 8, TOWN_R, MARKET_BOTTOM, MARKET_TOP, tone(HULL, HULL2, 3))
    v.cyl(0, 0, WELL_R, ARCH_R, MARKET_BOTTOM, MARKET_TOP, tone(HULL2, ARMOR, 4))
    rng = random.Random(8)
    for f in range(FLOORS + 1):
        y = MARKET_BOTTOM + f * h
        v.cyl(0, 0, ARCH_R, TOWN_R - 8, y - 4, y, tone(DECK, HULL2, 10 + f))
    for f in range(FLOORS):
        y0 = MARKET_BOTTOM + f * h
        # eight mouths from the Well into the gallery
        for a in range(0, 360, 45):
            p, q = P(a, WELL_R - 2), P(a, ARCH_R + 2)
            v.seg(p, q, 24, y0, y0 + h - 4, 0, carve=True)
        # windows on the outer wall
        for a in range(0, 360, 6):
            if (a // 6) % 3 == 0:
                p = P(a, TOWN_R - 4)
                v.box(p[0] - 4, p[0] + 4, y0 + 4, y0 + h - 8, p[1] - 4, p[1] + 4, GLASS, GLASSM)
        # stall rows along the radial streets: each a little box with an awning colour
        for a in range(22, 360, 45):
            for r in range(112, 232, 16):
                for side in (-1, 1):
                    q = P(a + side * math.degrees(14 / r), r)
                    col = rng.choice((RUST, TEAL, OCHRE, WARM, BRASS))
                    v.box(q[0] - 4, q[0] + 4, y0, y0 + 8, q[1] - 4, q[1] + 4, col)
        # shrine corner and pawn row walls: a few partitions so the gallery is not an empty ring
        for a in range(0, 360, 90):
            p, q = P(a + 10, ARCH_R + 4), P(a + 10, TOWN_R - 12)
            v.seg(p, q, 4, y0, y0 + h - 4, IVORY2)
    # the Stall Bridge across the Well at the second floor
    yb = MARKET_BOTTOM + 2 * h
    v.seg(P(30, WELL_R), P(210, WELL_R), 8, yb - 4, yb, BRASS)


def powerhouse(v):
    """The Lantern: the Powerhouse box under the market, the Cradle in the middle, the core road east to the big door."""
    y0, y1 = POWER_BOTTOM, POWER_TOP
    x0, x1, z0, z1 = X(24), X(86), Z(54), Z(26)
    v.box(x0, x1, y0 - 4, y1, z0, z1, tone(ARMOR, HULL2, 5))
    ceiling = y1 - 4
    rooms = [  # (x0, x1, y0, y1) in sketch cells: plan y is the row
        ((26, 42, 28, 52), "turbine hall"), ((46, 58, 34, 46), "the Cradle"), ((42, 46, 38, 41), "staff door"),
        ((58, 86, 36, 43), "the core road"), ((62, 80, 46, 52), "engineers"), ((70, 73, 43, 46), "engineers' door"),
        ((60, 72, 28, 33), "stores"), ((64, 66, 33, 36), "stores door"),
    ]
    for (a, b, c, d), _ in rooms:
        v.box(X(a), X(b), y0, ceiling, Z(d), Z(c), 0, carve=True)
    # the big door's opening in the east wall (the door grid fills it)
    v.box(X(86) - 8, X(86) + 4, y0, y0 + 48, Z(43), Z(36), 0, carve=True)
    # the Grate: the market's Well floor is the Powerhouse roof; its panel over the Cradle is a door grid
    v.box(X(46), X(58), y1 - 8, y1 + 1, Z(46), Z(34), 0, carve=True)
    # the Cradle's floor hatch over the trunk (a door grid)
    v.cyl(0, 0, 0, TRUNK_R, y0 - 4, y0, 0, carve=True)
    # the turbines: two drums in the turbine hall
    for y in (34, 46):
        v.cyl(X(34), Z(y), 14, 20, y0, ceiling - 8, BRASS)
    # the lift from the docks, in the turbine hall's south-west
    v.box(X(32), X(35), y0 - 8, y0, Z(29), Z(27), 0, carve=True)
    # cradle ring: the core sits in a jade collar
    v.cyl(0, 0, 32, 40, y0, y0 + 4, JADE)


def docks(v):
    """The trunk's casing, the freight docks west on a long pier, the Yards east (apron, a half-built hauler, the mass driver), the
    fuel depot north-east."""
    y0, y1 = DOCKS_BOTTOM, DOCKS_TOP
    v.box(X(40), X(64), y0, y1, Z(52), Z(28), tone(ARMOR, HULL2, 6))
    v.cyl(0, 0, 0, TRUNK_R, y0 - 2, y1 + 2, 0, carve=True)
    # freight docks: the pier, its masts (trusses) and the dock offices
    pier_y = -210
    v.box(X(4), X(40), pier_y - 4, pier_y, Z(42), Z(38), tone(DECK, HULL, 7))
    for x in (8, 16, 24, 32):
        v.box(X(x), X(x + 1), pier_y - 24, pier_y + 32, Z(38), Z(20), TRUSS)
        v.box(X(x), X(x + 1), pier_y - 24, pier_y + 32, Z(60), Z(42), TRUSS)
    for x, y in ((12, 46), (28, 30), (20, 50), (36, 46), (5, 30)):
        v.box(X(x), X(x + 3), pier_y, pier_y + 16, Z(y + 3), Z(y), RUST)
    # the dock lift's shaft: from the pier up the trunk casing to the turbine hall
    v.box(X(32) - 4, X(35) + 4, pier_y, POWER_BOTTOM - 4, Z(29) - 4, Z(27) + 4, HULL2)
    v.box(X(32), X(35), pier_y, POWER_BOTTOM, Z(29), Z(27), 0, carve=True)
    v.box(X(32), X(35), pier_y, pier_y + 16, Z(29) - 8, Z(29), 0, carve=True)   # its door on the pier side
    # the Yards: an apron right outside the big door, at the Powerhouse floor
    ay = POWER_BOTTOM - 4
    v.box(X(64), X(100), ay - 4, ay, Z(50), Z(30), tone(DECK, HULL2, 8))
    v.box(X(70), X(98), ay, ay + 4, Z(45), Z(33), TRUSS)
    # the half-built hauler on its slip: a hull shell open at the top and aft
    v.box(X(70), X(82), ay, ay + 28, Z(44), Z(34), HULL)
    v.box(X(72), X(82) + 4, ay + 4, ay + 32, Z(42), Z(36), 0, carve=True)
    # the mass driver: a line of rings from the breech (by the apron) out to the south-east
    a, b = (X(66), Z(56)), (X(101), Z(66))
    L = math.hypot(b[0] - a[0], b[1] - a[1])
    for n in range(0, 9):
        t = n / 8
        cx, cz = a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t
        ux, uz = (b[0] - a[0]) / L, (b[1] - a[1]) / L
        for ang in range(0, 360, 12):
            rr = 30
            ox, oy = -uz * math.cos(math.radians(ang)) * rr, math.sin(math.radians(ang)) * rr
            oz = ux * math.cos(math.radians(ang)) * rr
            px, py, pz = cx + ox, ay + 26 + oy, cz + oz
            v.box(px - 3, px + 3, py - 3, py + 3, pz - 3, pz + 3, RED if n == 0 else HULL2)
    # turrets on the dock and yard ends
    for x, y in ((2, 62), (102, 28)):
        v.box(X(x) - 8, X(x) + 8, pier_y - 8, pier_y, Z(y) - 8, Z(y) + 8, ARMOR)


def undertown(v):
    """The old quarter: a warren of shanty round the trunk, a ring street and radial alleys, Winch Square, the Sump, the scrap chapel,
    the outfall's branches, and the Wound open to space on the west."""
    y0, y1 = UNDER_BOTTOM, UNDER_TOP
    cx, cz = X(50), Z(40)
    def shanty(i, j, k):
        h = (i * 92821 ^ k * 68917 ^ (j // 3) * 1237) & 0xff
        return (RUST, TEAL, OCHRE, HULL2, RUST, WARM)[h % 6] if h % 11 else WARM
    v.cyl(cx, cz, 0, 36 * S, y0, y1, shanty, sz=.85)
    # lumpy roofline: some stacks rise a level into the docks' underside
    rng = random.Random(12)
    for _ in range(140):
        a, r = rng.uniform(0, 360), rng.uniform(60, 270)
        p = P(a, r, (cx, cz))
        w = rng.choice((8, 12, 16))
        v.box(p[0] - w, p[0] + w, y1, y1 + rng.choice((8, 12, 20)), p[1] - w, p[1] + w, shanty)
    # the trunk's armour ring and the trunk
    v.cyl(0, 0, TRUNK_R, 42, y0, y1, ARMOR)
    v.cyl(0, 0, 0, TRUNK_R, y0 - 2, y1 + 2, 0, carve=True)
    ys, ye = y0 + 4, y1 - 8    # streets: 48 m tall canyons under the roof
    for a in range(0, 360, 4):
        if not (150 < a < 215):
            v.seg(P(a, 15 * S, (cx, cz)), P(a + 4, 15 * S, (cx, cz)), 20, ys, ye, 0, carve=True)
    for a in range(20, 360, 3):
        if (a // 30) % 3 != 1:
            v.seg(P(a, 26 * S, (cx, cz)), P(a + 3, 26 * S, (cx, cz)), 16, ys, ye, 0, carve=True)
    for a, r0, r1, w in ((30, 15, 37, 16), (75, 6, 26, 16), (120, 15, 37, 16), (250, 15, 37, 16), (300, 6, 15, 16), (340, 15, 37, 20), (200, 6, 15, 16)):
        v.seg(P(a, r0 * S, (cx, cz)), P(a, r1 * S, (cx, cz)), w, ys, ye, 0, carve=True)
    # outfall branches (from the trunk into the warren)
    for pts in (((49, 40), (38, 36), (30, 37)), ((55, 40), (66, 44), (74, 43))):
        for p, q in zip(pts, pts[1:]):
            v.seg((X(p[0]), Z(p[1])), (X(q[0]), Z(q[1])), 24, ys, ye, 0, carve=True)
    # squares
    for (x, y, r) in ((38, 28, 4.2), (68, 52, 3.6), (62, 24, 3)):
        v.cyl(X(x), Z(y), 0, r * S, ys, ye, 0, carve=True)
    v.cyl(X(38), Z(28), 1.4 * S, 2.6 * S, ys, ys + 24, ARMOR)     # the winch at Winch Square
    # the Wound: the barge strike's hole, open to space, with scaffolds across it
    v.cyl(X(14), Z(38), 0, 10 * S + 8, y0 - 8, y1 + 24, 0, carve=True)
    for a, b in (((6, 30), (24, 46)), ((6, 46), (24, 30)), ((4, 38), (26, 38))):
        v.seg((X(a[0]), Z(a[1])), (X(b[0]), Z(b[1])), 4, (y0 + y1) / 2 - 2, (y0 + y1) / 2 + 2, TRUSS)
    # the outfall out of the bottom: an open mouth
    v.cyl(0, 0, 0, TRUNK_R + 8, y0 - 2, y0 + 8, 0, carve=True)


# ====================================================================================================================
def pieces():
    out = []
    for fn, pid, name in ((crown, "ship_lantern_crown", "Tollhouse Nine: the Crown"), (market, "ship_lantern_market", "Tollhouse Nine: the market"),
                          (powerhouse, "ship_lantern_powerhouse", "Tollhouse Nine: the Powerhouse"), (docks, "ship_lantern_docks", "Tollhouse Nine: the docks and Yards"),
                          (undertown, "ship_lantern_under", "Tollhouse Nine: Undertown")):
        v = Vol()
        fn(v)
        v.hollow()
        out.append((pid, name, v))
    return out


def write_bin(ps):
    os.makedirs(os.path.dirname(BIN), exist_ok=True)
    def s(t):
        b = t.encode("utf-8")
        return struct.pack("<h", len(b)) + b
    out = bytearray(b"LANT" + struct.pack("<ii", 1, len(ps)))
    for pid, name, v in ps:
        out += s(pid) + s(name) + struct.pack("<f", C) + struct.pack("<i", len(v.v))
        for (i, j, k), (col, mat) in sorted(v.v.items()):
            out += struct.pack("<hhhBB", i, j, k, col, mat)
    open(BIN, "wb").write(out)


def f(v):
    return f"{float(v):.1f}f"


def v3(p):
    return f"new float3({f(p[0])}, {f(p[1])}, {f(p[2])})"


def layout():
    """Where things are, in the station's frame (m)."""
    ay = POWER_BOTTOM - 4
    core = (0, CORE_Y, 0)
    # tether posts: four in the Cradle's walls, one in its ceiling, one in its floor ring; each names its district
    r = 44
    tethers = [
        ((-r, CORE_Y, 0), 3, "docks"), ((r, CORE_Y, 0), 4, "Yards"), ((0, CORE_Y, r), 2, "market"), ((0, CORE_Y, -r), 6, "gate beacons"),
        ((r * .7, POWER_TOP - 10, r * .7), 1, "the Crown"), ((-r * .7, POWER_BOTTOM + 6, -r * .7), 5, "Undertown"),
    ]
    districts = [(1, "the Crown"), (2, "the market"), (3, "the docks"), (4, "the Yards"), (5, "Undertown"), (6, "the gate beacons")]
    doors = [  # id, blueprint, closed pose, open offset, group
        ("bigdoor", "ship_lantern_door_big", (X(86) - 2, POWER_BOTTOM + 24, Z(39.5)), (0, 0, 64), 41),
        ("grate", "ship_lantern_door_grate", (0, POWER_TOP - 4, 0), (104, 0, 0), 42),
        ("hatch", "ship_lantern_door_hatch", (0, POWER_BOTTOM - 2, 0), (0, 0, 56), 43),
        ("staff", "ship_lantern_door_staff", (X(44), POWER_BOTTOM + 12, Z(39.5)), (0, 0, 24), 44),
        ("crown", "ship_lantern_door_crown", (X(9) - 2, CROWN_DECK + 12, Z(33)), (0, 0, 52), 45),
    ]
    # watchers: kind, position, facing, range, half-arc deg, sweep deg, sweep s, district
    watchers = []
    for a in range(0, 360, 90):   # searchlights on the arch, looking down the Well, sweeping
        p = P(a, WELL_R + 4)
        watchers.append(("Light", (p[0], CROWN_DECK + 6, p[1]), (-p[0] * .4, -1, -p[1] * .4), 260, 22, 35, 9 + a / 90, 1))
    for f_ in range(FLOORS):   # a crowd on every gallery: a thousand eyes
        y = MARKET_BOTTOM + (f_ + .5) * (MARKET_TOP - MARKET_BOTTOM) / FLOORS
        for a in (45, 225):
            p = P(a, 150)
            watchers.append(("Crowd", (p[0], y, p[1]), (0, 0, 1), 150, 180, 0, 0, 2))
    watchers += [
        ("Sensor", (0, CROWN_DECK + 30, Z(60)), (0, 0, 1), 320, 40, 0, 0, 1),                 # the arch's toll scanner, down the queue
        ("Light", (X(8), CROWN_DECK + 10, Z(25)), (-.6, -.3, .8), 220, 30, 25, 7, 1),           # the Crown's corner lamps
        ("Light", (X(43), CROWN_DECK + 10, Z(57)), (.5, -.3, -.8), 220, 30, 25, 8, 1),
        ("Sensor", (-40, POWER_BOTTOM + 30, -40), (.6, -.3, .6), 80, 50, 0, 0, 2),              # the Cradle's camera
        ("Sensor", (X(84), POWER_BOTTOM + 16, Z(42)), (-1, 0, 0), 230, 18, 0, 0, 4),            # the garrison post at the big door, down the core road
        ("Light", (X(27), -250, Z(47)), (-.7, 0, -.7), 180, 35, 20, 11, 5),                     # floodlights over the Wound
        ("Light", (X(90), ay + 30, Z(40)), (-.2, -.4, -1), 200, 40, 30, 10, 4),                 # the Yards' crane lamps
        ("Light", (X(20), -190, Z(40)), (0, -.5, -1), 200, 40, 30, 12, 3),                      # the freight docks' mast lamps
    ]
    zones = [  # needs, centre, half extents
        ("Trader", (0, -60, 0), (TOWN_R, 72, TOWN_R)),                                   # the market and the Well: traders
        ("Garrison", ((X(8) + X(42)) / 2, CROWN_DECK + 16, (Z(26) + Z(54)) / 2), ((X(42) - X(8)) / 2, 24, (Z(26) - Z(54)) / 2)),
        ("Work", ((X(24) + X(86)) / 2, (POWER_TOP + POWER_BOTTOM) / 2, 0), ((X(86) - X(24)) / 2, 24, (Z(26) - Z(54)) / 2)),
        ("Work", ((X(64) + X(100)) / 2, ay + 24, (Z(30) + Z(50)) / 2), ((X(100) - X(64)) / 2, 30, (Z(30) - Z(50)) / 2)),
        ("Trader", ((X(4) + X(40)) / 2, -200, Z(40)), ((X(40) - X(4)) / 2, 30, 100)),
    ]
    # traffic: planar loops at gallery heights; the toll queue above; boats rising and falling in the Well
    routes = []
    for f_ in range(FLOORS):
        y = MARKET_BOTTOM + (f_ + .45) * (MARKET_TOP - MARKET_BOTTOM) / FLOORS
        routes.append((y, [P(a, 160) for a in range(0 + f_ * 15, 360 + f_ * 15, 30)], True, 2))
    routes.append((40, [(0, Z(79)), (0, Z(60)), (0, Z(50)), (40, 0), (P(300, 260)), (P(320, 400))], False, 5))   # the toll queue and out north-east
    routes.append((-200, [(X(2), Z(40)), (X(36), Z(40)), (X(36), Z(46)), (X(2), Z(46))], True, 2))                 # freight docks
    pois = {
        "arrival": (0, 40, Z(84)), "queue": (0, 40, Z(66)), "well_mouth": (0, CROWN_DECK + 20, 0), "cradle": core,
        "core_road": (X(72), CORE_Y, Z(39.5)), "big_door_out": (X(92), CORE_Y, Z(39.5)), "yard_apron": (X(88), ay + 20, Z(40)),
        "breech": (X(66), ay + 26, Z(56)), "pod_line": (X(101) + 300, ay + 26, Z(66) - 90), "pier": (X(20), -196, Z(40)),
        "lift": (X(33.5), -200, Z(28)), "turbine_hall": (X(34), CORE_Y, Z(40)), "wound": (X(14) - 60, -270, Z(38)),
        "winch_square": (X(38), -270, Z(28)), "sump": (X(68), -270, Z(52)), "outfall": (0, UNDER_BOTTOM - 40, 0),
        "trunk": (0, -260, 0), "depot": (X(92), -200, Z(16)), "long_wait": (X(74), CROWN_DECK + 12, Z(46)),
        "cloth_row": P(112, 160) + (0,), "garrison": (X(18), CROWN_DECK + 10, Z(33)), "engineers": (X(71), CORE_Y, Z(49)),
    }
    pois["cloth_row"] = (P(112, 160)[0], MARKET_BOTTOM + 2.5 * (MARKET_TOP - MARKET_BOTTOM) / FLOORS, P(112, 160)[1])
    depot = [((X(88), -200, Z(12)), 34), ((X(98), -200, Z(14)), 29), ((X(92), -200, Z(22)), 26)]
    turrets = [(X(8), CROWN_DECK + 8, Z(55)), (X(43), CROWN_DECK + 8, Z(57)), (X(8), CROWN_DECK + 8, Z(25)), (X(88), CROWN_DECK + 8, Z(41)),
               (X(2), -210, Z(62)), (X(102), -210, Z(28))]
    moorings = [("ship_hauler_heavy", (X(12), -200, Z(28)), 90), ("ship_hauler_heavy", (X(12), -200, Z(52)), 270),
                ("ship_freighter_medium", (X(28), -200, Z(50)), 270)]
    return dict(core=core, tethers=tethers, districts=districts, doors=doors, watchers=watchers, zones=zones, routes=routes,
                pois=pois, depot=depot, turrets=turrets, moorings=moorings)


def write_cs(L):
    o = []
    w = o.append
    w("// <auto-generated> ArtSource/Places/lantern_export.py (EXT-007). Do not edit: change the export and run it again. </auto-generated>")
    w("#if UNITY_EDITOR || DEVELOPMENT_BUILD")
    w("using Unity.Mathematics;")
    w("")
    w("namespace Game.App.DevRuntime.Extraction")
    w("{")
    w("    /// <summary>Tollhouse Nine's layout in its own frame (m; y up, 0 = the arrival plane). See ArtSource/Places/lantern_export.py.</summary>")
    w("    internal static class LanternLayout")
    w("    {")
    w("        internal static readonly string[] Pieces = { " + ", ".join(f'"{p}"' for p in ("ship_lantern_crown", "ship_lantern_market", "ship_lantern_powerhouse", "ship_lantern_docks", "ship_lantern_under")) + " };")
    w(f"        internal static readonly float3 Core = {v3(L['core'])};")
    w("        internal const float CoreRadius = 20f, WellRadius = %sf, TrunkRadius = %sf, TownRadius = %sf;" % (WELL_R, TRUNK_R, TOWN_R))
    w("        internal const float CrownDeck = %sf, MarketBottom = %sf, PowerTop = %sf, PowerBottom = %sf, UnderBottom = %sf;" % (CROWN_DECK, MARKET_BOTTOM, POWER_TOP, POWER_BOTTOM, UNDER_BOTTOM))
    w("        internal static readonly (float3 Post, ushort District, string Name)[] Tethers =")
    w("        {")
    for p, d, n in L["tethers"]:
        w(f"            ({v3(p)}, {d}, \"{n}\"),")
    w("        };")
    w("        internal static readonly (ushort Id, string Name)[] Districts = { " + ", ".join(f'({i}, "{n}")' for i, n in L["districts"]) + " };")
    w("        internal static readonly (string Id, string Blueprint, float3 Closed, float3 Open, ushort Group)[] Doors =")
    w("        {")
    for i, bp, p, d, g in L["doors"]:
        w(f"            (\"{i}\", \"{bp}\", {v3(p)}, {v3(d)}, {g}),")
    w("        };")
    w("        internal static readonly (string Kind, float3 At, float3 Facing, float Range, float HalfArcDeg, float SweepDeg, float SweepSeconds, ushort District)[] Watchers =")
    w("        {")
    for k, p, d, r, a, sw, ss, di in L["watchers"]:
        w(f"            (\"{k}\", {v3(p)}, {v3(d)}, {f(r)}, {f(a)}, {f(sw)}, {f(ss)}, {di}),")
    w("        };")
    w("        internal static readonly (string Needs, float3 Centre, float3 Half)[] Zones =")
    w("        {")
    for n, c, h in L["zones"]:
        w(f"            (\"{n}\", {v3(c)}, {v3(h)}),")
    w("        };")
    w("        internal static readonly (float Y, float2[] Points, bool Loop, int Ships)[] Routes =")
    w("        {")
    for y, pts, loop, n in L["routes"]:
        w(f"            ({f(y)}, new[] {{ " + ", ".join(f"new float2({f(p[0])}, {f(p[1])})" for p in pts) + f" }}, {'true' if loop else 'false'}, {n}),")
    w("        };")
    w("        internal static readonly (string Id, float3 At)[] Pois =")
    w("        {")
    for k, p in L["pois"].items():
        w(f"            (\"{k}\", {v3(p)}),")
    w("        };")
    w("        internal static readonly (float3 At, float Radius)[] Depot = { " + ", ".join(f"({v3(p)}, {f(r)})" for p, r in L["depot"]) + " };")
    w("        internal static readonly float3[] Turrets = { " + ", ".join(v3(p) for p in L["turrets"]) + " };")
    w("        internal static readonly (string Blueprint, float3 At, float YawDeg)[] Moorings = { " + ", ".join(f'("{b}", {v3(p)}, {f(y)})' for b, p, y in L["moorings"]) + " };")
    w("")
    w("        internal static float3 Poi(string id) { foreach (var p in Pois) if (p.Id == id) return p.At; return float3.zero; }")
    w("    }")
    w("}")
    w("#endif")
    os.makedirs(os.path.dirname(CS), exist_ok=True)
    open(CS, "w").write("\n".join(o) + "\n")


if __name__ == "__main__":
    ps = pieces()
    write_bin(ps)
    L = layout()
    write_cs(L)
    for pid, name, v in ps:
        print(f"{pid:28s} {len(v.v):8d} cells")
    print("total", sum(len(v.v) for _, _, v in ps), "->", BIN)
