"""DEV-023 option D's quarters, rebuilt after the owner's review of 2026-10-18:

"theres no defenses on the entrance of the hub" / "no differentiation between each section, just a gap in the city, the
pit should be like a coliseum arena where ships fight" / "markets should be busier and have a bunch of ships trading in
and out ... anything for new ships, experimental technology, black markets" / "there is no point to the breakers section
... we already have a scrapyard" / "the scrapyard should be much more akin to a place where large junk ships gets sliced
with lasers or explosives into smaller chunks" / "the forge is meant to be a factory for NEW ships ... intricate machinery
and ships halfway through building process, this is where the player can also edit their ship" / "the institute is where
we trial experimental blocks, weapons, mechanics ... a combined range/institute should be a larger sector with different
parts for each thing being tested".

Each quarter fills its arc with the machinery of its job, so no quarter is a gap in the city:
  Forge 340-60, Salvage Yard 60-112, Exchange 112-170 (its bazaar lines the Grand Avenue), Harbour Mouth 180,
  the Pit 190-240, the Circuit 247, Proving Grounds 255-340.
"""
import math
import random

from hub_layouts import CLASSES, polar, bearing_of, box, ngon, annulus, arc

# quarter arcs (bearing from, bearing to), clockwise from north; the fill takes its style from these
ARCS = {"foundry": (340, 60), "breakers": (60, 112), "exchange": (112, 170), "gate": (170, 190), "pit": (190, 240),
        "circuit": (240, 255), "range": (255, 340)}


def quarter_at(b):
    b %= 360
    for k, (b0, b1) in ARCS.items():
        if (b - b0) % 360 <= (b1 - b0) % 360:
            return k
    return "old_town"


def at(c, hd, x, y):
    """A point x m to the right of and y m ahead of c, facing hd."""
    return polar(hd, y, polar(hd + 90, x, c))


def gun_turret(C, c, facing, district, z=0.0, big=False):
    """A gun turret: an armoured dome and twin barrels laid along its facing."""
    r, L = (22, 80) if big else (13, 46)
    C.shape("sphere", c, district, "turret", 0.6, z, r=r)
    for s in (-1, 1):
        C.shape("truss", at(c, facing, s * r * 0.35, r * 0.6 + L / 2), district, "barrel", 0.0, z + r * 0.3, heading=facing,
                length=L, width=5 if big else 3)
    C.fx.append(dict(type="turret", c=c, facing=facing, reach=900 if big else 560, arc=120 if big else 100))


# ---------------------------------------------------------------- Harbour Mouth: the gate is defended
def mouth(C, R_WALL):
    """Two gun keeps flank the gate ring, batteries stand off outside, a picket holds the approach, mine belts flank
    it, and every arrival passes the customs gantries inside."""
    for s in (1, -1):
        k = (s * 330, -R_WALL)
        C.b(ngon(k, 100, 6, 30), -8, -260, "bastion", "gate", lit=0.5)
        C.b(ngon(k, 64, 6, 0), 2, -8, "bastion", "gate", lit=0.7)
        for dx, dy in ((-55, -30), (55, -30), (0, -95)):
            gun_turret(C, (k[0] + dx, k[1] + dy), 180, "gate", z=8, big=True)
        C.shape("panel", (k[0], k[1] + 20), "gate", "mast", 0.9, 90, heading=0, length=5, thick=5, height=180)
        # the shield curtain: a field wall from the keep to the gatehouse; ships pass only through the ring
        C.fx.append(dict(type="field_wall", a=(s * 230, -R_WALL - 10), b=(s * 210, -R_WALL - 10)))
    for x in (-1000, -560, 560, 1000):
        p = (x, -R_WALL - 420 - abs(x) * 0.15)
        C.b(ngon(p, 40, 6, 30), -6, -40, "bastion", "gate", lit=0.6)
        gun_turret(C, p, 180 + x * 0.012, "gate", z=0)
    # the picket: Meridian militia cruisers on station across the approach, a heavy hull holding behind them
    for x, y, hd in ((-420, -3050, 90), (380, -3180, 270), (-150, -3420, 90)):
        C.ship((x, y), hd, "cruiser", "ark", "cruise")
    C.ship((-700, -3300), 0, "liner", "ark", "cruise")
    for s in (1, -1):
        C.fx.append(dict(type="minefield", c=(s * 820, -3050), r=260, n=30, kind="seeker" if s > 0 else "prox"))
    # customs: two gantries across the inbound lane, an inspected freighter and a cutter alongside
    for y in (-2120, -1990):
        C.shape("truss", (0, y), "gate", "gantry", 0.8, 30, heading=90, length=300, width=7)
        C.fx.append(dict(type="scanner", a=(-120, y), b=(120, y)))
    C.ship((70, -2060), 0, "freighter", "tlax", "cruise")
    C.ship((130, -2010), 0, "courier", "ark", "cruise")


# ---------------------------------------------------------------- the Forge: a factory for new ships
def ship_in_build(C, c, hd, cls, stage, livery, rng):
    """A hull on the line: a keel and ribs (frame), half plated (ribs), plated in scaffold (plated) or finished."""
    L, W = CLASSES[cls]
    d = "foundry"
    if stage == "done":
        C.ship(c, hd, cls, livery, "docked")
    else:
        C.shape("truss", c, d, "keel", 0.0, 0, heading=hd, length=L, width=max(5, W * 0.16))
        n = max(3, int(L / 12))
        for i in range(n):
            f = -0.45 + 0.9 * i / (n - 1)
            taper = 1.0 - 0.55 * max(0.0, f) ** 2
            C.shape("truss", polar(hd, L * f, c), d, "rib", 0.0, 0, heading=hd + 90, length=W * taper, width=max(3, L * 0.025))
        if stage in ("ribs", "plated"):
            part = 0.5 if stage == "ribs" else 0.92
            C.b(box(polar(hd, -L * (1 - part) / 2, c), hd, L * part, W * 0.86), W * 0.28, -W * 0.28, "hull_new", d,
                lit=0.15 if stage == "ribs" else 0.35)
    # the gantry: rails either side, a bridge across, arms working the hull, welds flashing
    for s in (-1, 1):
        C.shape("truss", at(c, hd, s * (W / 2 + 16), 0), d, "gantry", 0.8, 30, heading=hd, length=L + 40, width=6)
    C.shape("truss", polar(hd, L * rng.uniform(-0.3, 0.3), c), d, "gantry", 0.8, W * 0.5 + 30, heading=hd + 90, length=W + 46,
            width=7)
    if stage != "done":
        for k in range(rng.randint(3, 6)):
            s = 1 if k % 2 else -1
            base = at(c, hd, s * (W / 2 + 16), L * rng.uniform(-0.4, 0.4))
            C.shape("sphere", base, d, "arm", 0.6, 30, r=5)
            C.shape("truss", polar(hd + 90 - s * 90 + s * rng.uniform(30, 60) * -1, 12, base), d, "arm", 0.6, 22,
                    heading=hd + 90 + s * rng.uniform(-40, 40), length=24, width=3)
            C.fx.append(dict(type="flash", c=at(c, hd, s * W * 0.4, L * rng.uniform(-0.4, 0.4)), z=W * 0.2, r=2.0, col="#BFE8FF"))


def forge(C, rng, R_CUR, R_WALL):
    """Three assembly lines run out from the Current to the wall, each hull moving through four stations; factory halls
    stand between the lines; the Refit Dock, where you edit your own ship, sits on the avenue."""
    lines = [(354, "cruiser", [1500, 1640, 1780, 1920, 2060, 2200], "noct", 2),
             (12, "freighter", [1530, 1770, 2010, 2220], "tlax", 1),
             (32, "liner", [1590, 1880, 2170], "tlax", 1)]
    stages_for = {6: ["frame", "frame", "ribs", "ribs", "plated", "done"], 5: ["frame", "ribs", "plated", "plated", "done"],
                  4: ["frame", "ribs", "plated", "done"], 3: ["frame", "plated", "done"]}
    for b, cls, radii, liv, abreast in lines:
        L, W = CLASSES[cls]
        st = stages_for[len(radii)]
        for r, stage in zip(radii, st):
            for a in range(abreast):
                off = (a - (abreast - 1) / 2) * (W + 60)
                ship_in_build(C, at(polar(b, r), b, off, 0), b, cls, stage, liv, rng)
        # the line's conveyor: rails the length of the line, parts tugs running it
        width = abreast * (W + 60) + 40
        for s in (-1, 1):
            C.shape("truss", at(polar(b, (radii[0] + radii[-1]) / 2), b, s * width / 2, 0), "foundry", "rail", 0.6, -20, heading=b,
                    length=radii[-1] - radii[0] + L + 60, width=4)
        lane = [polar(b, radii[0] - L / 2 - 30), polar(b, radii[-1] + L / 2 + 20)]
        lane = [at(p, b, width / 2 + 24, 0) for p in lane]
        C.lane(lane, "district", oneway=False)
        C.traffic_on(lane, 4, [("tug", "noct"), ("courier", "noct")], int(b * 7 + 1))
    # factory halls between the lines: long sheds with their fabricators glowing through the roof slots
    for b in (3, 22, 43):
        for r in (1620, 2000):
            C.b(box(polar(b, r), b, 240, 54), -10, -150, "hall", "foundry", lit=0.8)
            for k in range(4):
                C.fx.append(dict(type="flash", c=polar(b, r - 90 + k * 60), z=-8, r=4, col="#FF9A40"))
    # the Refit Dock on the avenue: a cradle ring you fly into to edit your ship, the Blueprint Lab beside it
    rd = polar(50, 1500)
    C.shape("torus", rd, "foundry", "coil", 1.0, 0, R=46, r=6, upright=True, heading=50)
    C.shape("torus", polar(50, 70, rd), "foundry", "coil", 1.0, 0, R=46, r=6, upright=True, heading=50)
    for s in (-1, 1):
        C.shape("truss", at(rd, 50, s * 52, 35), "foundry", "gantry", 0.8, 0, heading=50, length=120, width=6)
    C.ship(polar(50, 35, rd), 50, "wraith", "noct", "docked")
    C.fx.append(dict(type="lab", c=polar(140, 70, rd)))
    C.holos.append(dict(text="REFIT", at=polar(50, 35, rd), district="foundry", small=True))
    C.fx.append(dict(type="engine_stand", c=polar(57, 2180), facing=237, length=340))
    C.lane([polar(12, R_CUR + 40), polar(12, R_WALL + 400), polar(12, 3200)], "launch")
    C.district("foundry", "THE FORGE", polar(20, 1800),
               "The shipworks: three assembly lines build new hulls from keel to launch; the Refit Dock edits yours.",
               ["Refit Dock: edit your ship, the Blueprint Lab", "Ships on the line, frame to finished",
                "Engine test stand: engine wash across the yard"], "FORGE")
    return dict(cuts=[(b, radii[0] - CLASSES[cls][0] / 2 - 30, radii[-1] + CLASSES[cls][0] / 2 + 20,
                       abreast * (CLASSES[cls][1] + 60) + 110) for b, cls, radii, _, abreast in lines],
                circles=[(rd, 130), (polar(57, 2180), 120)])


# ---------------------------------------------------------------- the Salvage Yard: junk ships cut into chunks
def hulk_cut(C, c, hd, L, W, pieces, rng):
    """A hulk in its cradles, sliced across: the bow still whole, the cut-off sections drifting apart, lasers on the
    current cut, charges flashing along the next, tugs hauling the chunks away."""
    d = "breakers"
    cuts = sorted(rng.uniform(0.25, 0.75) for _ in range(pieces - 1))
    edges = [0.0] + cuts + [1.0]
    drift = 0.0
    for i in range(len(edges) - 1):
        f0, f1 = edges[i], edges[i + 1]
        seg = (f1 - f0) * L
        drift += 0 if i == 0 else rng.uniform(18, 45)
        mid = polar(hd, -L / 2 + (f0 + f1) / 2 * L + drift, c)
        jitter = 0 if i == 0 else rng.uniform(-9, 9)
        side = 0 if i == 0 else rng.uniform(-14, 14)
        last = i == len(edges) - 2
        C.fx.append(dict(type="hulk", c=polar(hd + 90, side, mid), heading=hd + jitter, length=seg, width=W * (1 - 0.06 * i),
                         bow=last, cut_front=not last, cut_back=i > 0))
        if i and rng.random() < 0.7:
            C.ship(at(mid, hd, (W / 2 + 26) * rng.choice([-1, 1]), -seg / 2), hd + 180, "tug", "scav", "towing")
    for f in (0.15, 0.5, 0.85):
        C.shape("truss", polar(hd, -L / 2 + f * L, c), d, "cradle", 0.3, -W * 0.3, heading=hd + 90, length=W + 40, width=8)
    # the current cut: a gantry astride it, two cutting lasers down onto the seam
    cut = polar(hd, -L / 2 + cuts[0] * L, c)
    C.shape("truss", cut, d, "gantry", 0.8, 90, heading=hd + 90, length=W + 70, width=7)
    for s in (-1, 1):
        C.shape("truss", at(cut, hd, s * (W / 2 + 30), 0), d, "gantry", 0.8, 45, heading=hd, length=30, width=6)
        C.fx.append(dict(type="beam", a=at(cut, hd, s * (W / 2 + 20), 0), za=90, b=at(cut, hd, s * W * 0.2, 0), zb=6, col="#FF3020"))
    C.fx.append(dict(type="flash", c=cut, z=8, r=10, col="#FFF0D0"))
    # charges along the next seam
    nxt = polar(hd, -L / 2 + cuts[0] * L * 0.55, c)
    for k in range(5):
        C.fx.append(dict(type="flash", c=at(nxt, hd, (k - 2) * W / 5, 0), z=W * 0.3, r=5 if k != 2 else 14,
                         col="#FFB347" if k != 2 else "#FFE2B0"))
    C.fx.append(dict(type="debris", c=polar(hd, -L / 2 - 40, c), r=W, n=18))


def salvage(C, rng):
    hulks = [(77, 1960, 520, 110, 3), (95, 1720, 420, 92, 3), (97, 2170, 340, 76, 3)]
    for b, r, L, W, n in hulks:
        hulk_cut(C, polar(b, r), b + 90, L, W, n, rng)
    for k in range(14):
        b, r = rng.uniform(70, 104), rng.uniform(1560, 2240)
        L_ = rng.uniform(26, 80)
        C.fx.append(dict(type="hulk", c=polar(b, r), heading=rng.uniform(0, 360), length=L_, width=L_ * rng.uniform(0.35, 0.6),
                         cut_front=True, cut_back=rng.random() < 0.5))
        if rng.random() < 0.6:
            C.ship(polar(b + 1.4, r), b + 180, "tug", "scav", "towing")
            C.fx.append(dict(type="beam", a=polar(b + 1.4, r), za=0, b=polar(b, r), zb=0, col="#9FB4C8"))
    for k in range(5):
        C.fx.append(dict(type="debris", c=polar(rng.uniform(72, 102), rng.uniform(1600, 2200)), r=rng.uniform(50, 90), n=22))
    # magnet cranes on towers, the sorting rafts of salvaged blocks, the yard's own tugs
    for b, r in ((68, 1600), (86, 2080), (106, 1980), (66, 2230)):
        C.shape("sphere", polar(b, r), "breakers", "crane", 0.6, 40, r=14)
        C.shape("truss", polar(b + 3, r), "breakers", "gantry", 0.8, 40, heading=b + 90, length=90, width=5)
    for b, r in ((64, 1720), (108, 1580)):
        C.b(box(polar(b, r), b + 90, 160, 60), 6, -6, "cargo", "breakers", lit=0.2)
        for k in range(10):
            C.b(box(polar(b + (k % 5 - 2) * 1.6, r + (k // 5 - 0.5) * 26), b + 90, 22, 10), 14, 6, "cargo", "breakers", lit=0.1,
                roof=rng.choice(["scav", "khu", None]))
    C.district("breakers", "THE SALVAGE YARD", polar(86, 1900),
               "Junk ships are cut into chunks with lasers and charges; tugs haul the chunks off, and what nobody buys "
               "goes down the Drain to feed the hole.",
               ["Salvage the drifting chunks and loose blocks", "Cutting lasers and charges on the seams",
                "Each chunk fed down the Drain makes the disk flare"], "SALVAGE")
    return dict(circles=[(polar(b, r), L * 0.5 + 20) for b, r, L, W, n in hulks])


# ---------------------------------------------------------------- the Exchange: the busiest place in the city
def stall(C, c, hd, district, rng, livery):
    C.b(box(c, hd, rng.uniform(24, 40), rng.uniform(16, 24)), rng.uniform(-2, 6), -rng.uniform(30, 60), "stall", district,
        lit=rng.uniform(0.7, 1.0), roof=livery)
    # every stall has its neon: a board facing the avenue and a blade above it
    col = rng.choice(["#FF2E88", "#22E6FF", "#FFB000", "#9DFF3C", "#B44CFF"])
    C.fx.append(dict(type="neon", c=c, heading=hd + 90, length=rng.uniform(16, 28), height=rng.uniform(6, 10), z=12, col=col))
    if rng.random() < 0.5:
        C.fx.append(dict(type="neon", c=polar(hd, 10, c), heading=hd, length=5, height=rng.uniform(26, 44), z=26,
                         col=rng.choice(["#FF2E88", "#22E6FF", "#FFB000"])))


def exchange(C, rng, R_WALL, merchant_hub):
    d = "exchange"
    # the four trading houses
    for i, (name, liv, cls) in enumerate([("TLAXCALA", "tlax", "freighter"), ("KHUCHAD", "khu", "hauler"),
                                          ("VESTARI", "vest", "courier"), ("SWAP", "scav", "tug")]):
        r = 1530 + i * 210
        merchant_hub(C, polar(141 + (9 if i % 2 == 0 else -9) * (1700 / r), r), name, liv, d, cls, rng)
    # the Tech Bazaar: Vestari prototype labs under field domes, couriers waiting at their locks
    for k, r in enumerate((1600, 1830, 2060)):
        c = polar(121, r)
        C.shape("sphere", c, d, "lab", 1.0, -24, r=38)
        C.shape("torus", c, d, "coil", 1.0, 0, R=54, r=3)
        C.ship(polar(121 + 5, r), 211, "courier", "vest", "docked")
    # the Engine Mart: engines racked for sale, nozzles out, for anyone building or refitting a ship
    for row in range(3):
        for k in range(6):
            p = polar(161 - row * 2.2, 1560 + k * 34)
            C.shape("drum", p, d, "engine", 0.9, 0, heading=161 + 90, length=22, radius=7)
            C.fx.append(dict(type="flash", c=polar(161 - row * 2.2 + 0.5, 1560 + k * 34), z=0, r=3, col="#8FB4FF"))
    # piers: fingers of berths along the market canal, each one full
    for k, b in enumerate((128, 134, 148, 154)):
        r0 = 1460 + (k % 2) * 60
        C.shape("truss", polar(b, r0 + 180), d, "pier", 0.6, 0, heading=b, length=360, width=7)
        for j in range(5):
            cls = rng.choice(["hauler", "courier", "freighter", "shuttle", "tug", "courier"])
            W = CLASSES[cls][1]
            side = 1 if j % 2 else -1
            C.berth(at(polar(b, r0 + 30 + j * 70), b, side * (W / 2 + 8), 0), b, CLASSES[cls][0], "pier",
                    cls, rng.choice(["tlax", "khu", "vest", "scav", "civ"]))
    # the market canal: a loop through the quarter, crowded both ways
    canal = [polar(b, r) for b, r in ((116, 1440), (166, 1440), (168, 2250), (114, 2250))]
    canal = canal + [canal[0]]
    C.lane(canal, "street", closed=True, oneway=False)
    C.traffic_on(canal, 26, [("shuttle", "civ"), ("hauler", "tlax"), ("courier", "vest"), ("tug", "scav"), ("hauler", "khu"),
                             ("freighter", "tlax"), ("courier", "civ")], 141)
    # the Undermarket: old hulks lashed together under the wall, below the plane, red lamps and no questions
    um = polar(116, R_WALL - 150)
    for k in range(5):
        C.fx.append(dict(type="hulk", c=polar(116 + (k - 2) * 2.6, R_WALL - 150 + (k % 2) * 50), heading=116 + 90 + rng.uniform(-30, 30),
                         length=rng.uniform(70, 130), width=rng.uniform(26, 40)))
        C.ship(polar(116 + (k - 2) * 2.6 + 1.2, R_WALL - 220), 206, rng.choice(["tug", "courier", "hauler"]), "scav", "docked")
    for k in range(6):
        C.fx.append(dict(type="flash", c=polar(116 + (k - 2.5) * 2.0, R_WALL - 180), z=10, r=4, col="#FF3C28"))
    C.district("exchange", "THE EXCHANGE", polar(141, 1820),
               "Four trading houses, the Tech Bazaar's prototype labs, the Engine Mart, piers full of traders and, under the "
               "wall, the Undermarket. Anything a new ship needs is sold here.",
               ["TLAXCALA: bulk goods, fuel, ore; buys anything", "KHUCHAD: weapon blocks",
                "VESTARI: shields, reactors; the Tech Bazaar's prototypes", "SWAP: used and salvaged blocks",
                "The Engine Mart", "The Undermarket: black-market goods, no questions"], "EXCHANGE")
    return dict(circles=[(um, 200)])


def bazaar(C, rng):
    """Stalls line both sides of the Grand Avenue from the gate to the Current, each with a trader moored at it."""
    for s in (1, -1):
        for k, y in enumerate(range(-2200, -1440, 64)):
            c = (s * 132, y + (16 if k % 2 else 0))
            liv = rng.choice(["tlax", "khu", "vest", "scav", "civ"])
            stall(C, c, 0, "exchange", rng, liv)
            if rng.random() < 0.7:
                cls = rng.choice(["shuttle", "courier", "shuttle", "tug"])
                C.ship((s * (132 - 22 - CLASSES[cls][1] / 2), y), 0 if s > 0 else 180, cls, liv, "docked")


# ---------------------------------------------------------------- the Pit: a coliseum where ships fight
def pit(C, rng):
    c = polar(215, 1860)
    d = "pit"
    # the bowl: tiers stepping out and up from the fighting floor, the crowd's windows lit in every tier
    for R, z, r in ((205, -150, 12), (238, -118, 16), (272, -86, 19), (308, -54, 21), (345, -24, 23)):
        C.shape("torus", c, d, "stands", 0.95, z, R=R, r=r)
    # the outer wall of arches
    for k in range(40):
        a = k * 9
        C.b(box(polar(a, 372, c), a + 90, 2 * math.pi * 372 / 40 - 6, 22), -6, -200, "stands", d, lit=0.9)
        C.shape("truss", polar(a + 4.5, 290, c), d, "rib", 0.0, -70, heading=a + 4.5, length=150, width=5)
    for k in range(8):
        C.shape("panel", polar(k * 45 + 22.5, 392, c), d, "mast", 1.0, 60, heading=k * 45, length=5, thick=5, height=150)
    # spectators moored round the rim
    for k in range(22):
        a = k * 360 / 22 + 4
        cls = rng.choice(["shuttle", "courier", "hauler", "shuttle", "liner"] if k % 7 == 0 else ["shuttle", "courier", "tug"])
        C.ship(polar(a, 400 + CLASSES[cls][1] / 2, c), a + 90, cls, rng.choice(["khu", "khu", "civ", "tlax", "scav"]), "docked")
    # a bout under way: two raiders duelling, their tracers crossing, hits flashing on the floor's field wall
    s1, s2 = polar(40, 110, c), polar(230, 80, c)
    C.ship(s1, 250, "wraith", "khu", "cruise"); C.ship(s2, 60, "wraith", "scav", "cruise")
    for k in range(3):
        C.fx.append(dict(type="beam", a=polar(250, 16 + k * 6, s1), za=0, b=polar(70 + k * 4, 30, s2), zb=0, col="#FF7A2F"))
    C.fx.append(dict(type="flash", c=polar(60, 24, s2), z=0, r=9, col="#FFE2B0"))
    C.fx.append(dict(type="arena", c=c, r=195))
    for b0 in (0, 90, 180, 270):
        C.fx.append(dict(type="bout_gate", c=polar(b0 + 22.5, 205, c)))
    C.district("pit", "THE PIT", c, "The Khuchad coliseum: ships fight on the floor of a bowl of lit tiers, the crowd "
               "moored round the rim.", ["Four bouts: raider duel, raider wing, xeno swarm, cruiser bout",
                                         "A field wall bounds the floor"], "THE PIT")
    return dict(circles=[(c, 440)]), c


# ---------------------------------------------------------------- Proving Grounds: the Range and the Institute in one
def pen(C, c, hd, w, h):
    corners = [at(c, hd, sx * w / 2, sy * h / 2) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    for i in range(4):
        C.fx.append(dict(type="field_wall", a=corners[i], b=corners[(i + 1) % 4]))


def proving(C, rng):
    d = "range"
    # the Line: a hangar bay for every ship in every fit, along the Current
    ships = ["wraith", "cruiser", "courier", "wraith", "cruiser", "hauler", "wraith", "courier", "cruiser", "liner"]
    for k, cls in enumerate(ships):
        b = 262 + k * 7.2
        r = 1500 if cls != "liner" else 1520
        C.shape("truss", polar(b - 2.6, r), d, "hangar", 0.6, 0, heading=b, length=CLASSES[cls][0] + 20, width=5)
        C.shape("truss", polar(b + 2.6, r), d, "hangar", 0.6, 0, heading=b, length=CLASSES[cls][0] + 20, width=5)
        C.ship(polar(b, r), b, cls, "noct" if k % 3 else "ark", "docked")
    # the Weapons Range: firing lanes out to 800 m between field walls, Turret Alley alongside
    bR = 327
    for dd in (200, 400, 600, 800):
        C.fx.append(dict(type="range_mark", c=polar(bR, 1560 + dd), facing=bR))
    for k in range(5):
        C.fx.append(dict(type="target", c=polar(bR - 6 + k * 3, 1800 + (k % 3) * 150)))
    C.fx.append(dict(type="target_station", c=polar(bR, 2200)))
    for s in (-1, 1):
        C.fx.append(dict(type="field_wall", a=polar(bR + s * 9, 1600), b=polar(bR + s * 9, 2270)))
    C.fx.append(dict(type="turret_alley", a=polar(315, 1620), b=polar(315, 2240)))
    for k in range(5):
        gun_turret(C, polar(312 if k % 2 else 318, 1700 + k * 120), 315 + (90 if k % 2 else -90), d)
    # the Block Lab: a grid of test cells, one experimental block working in each
    names = ["MIRROR", "MAGNET", "PORTAL", "SHIELD", "REACTOR", "DOOR", "ARM", "REPULSOR", "RAILS", "DRIVER", "CANISTER", "EMP"]
    for i, n in enumerate(names):
        b = 289 + (i % 4) * 5.4
        r = 1680 + (i // 4) * 170
        c = polar(b, r)
        pen(C, c, b, 120, 120)
        C.shape("sphere", c, d, "pod", 1.0, 0, r=16)
        C.shape("panel", polar(b + 90, 44, c), d, "mast", 0.9, 30, heading=b, length=4, thick=4, height=60)
        C.fx.append(dict(type="exhibit", c=c, name=n))
    # Mechanics trials: an artificial gravity well, a live minefield, a pulse chamber behind blast walls, a portal pair
    gw = polar(268, 1760)
    C.shape("sphere", gw, d, "core", 1.0, 0, r=26)
    C.shape("torus", gw, d, "coil", 1.0, 0, R=44, r=4)
    for r in (140, 230):
        C.fx.append(dict(type="contour", c=gw, r=r, a=70))
    C.fx.append(dict(type="minefield", c=polar(266, 2110), r=150, n=18, kind="prox"))
    pen(C, polar(266, 2110), 266, 330, 330)
    pc = polar(281, 1960)
    C.fx.append(dict(type="reactor", c=pc, r=170))
    for k in range(4):
        C.shape("panel", polar(k * 90 + 45, 120, pc), d, "bulkhead", 0.2, 0, heading=k * 90 + 45 + 90, length=70, thick=8, height=60)
    C.fx.append(dict(type="portal", c=polar(279, 2240), facing=279, width=90))
    C.fx.append(dict(type="portal", c=polar(262, 2240), facing=262, width=90))
    # Trials Control: the observation ring that runs the grounds
    C.shape("torus", polar(300, 2230), d, "control", 1.0, 0, R=60, r=8, spokes=4, hub=18)
    C.district("range", "PROVING GROUNDS", polar(297, 1880),
               "Where Meridian trials what it builds: every ship in every fit on the Line, the Weapons Range, the Block "
               "Lab's test cells and the Mechanics trials.",
               ["The Line: take any ship in any fit", "Weapons Range: targets out to 800 m; Turret Alley",
                "Block Lab: twelve experimental blocks, one per cell", "Mechanics trials: gravity well, mines, pulse, portals"],
               "PROVING GROUNDS")
    return dict(circles=[(gw, 200), (pc, 190), (polar(266, 2110), 200), (polar(300, 2230), 110)],
                lanes=[(bR, 1560, 2290, 24), (315, 1600, 2260, 10)],
                cells=[(289 + (i % 4) * 5.4, 1680 + (i // 4) * 170) for i in range(12)])
