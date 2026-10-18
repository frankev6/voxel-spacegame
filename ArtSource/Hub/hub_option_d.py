"""DEV-023 option D: Meridian built round a black hole (hub_layouts.py draws it; hub_arrival.py renders it).

Owner (2026-10-18): "make a fourth option with the gravity well at the core being a black hole and that is the
stations power, and also teaches you how that mechanic works, but its better integrated than your options. i live the
option a meridian you made, use that as a baseline for option a and integrate this idea." and "when i said "buildings"
you're thinking very blocky rectangles of a human city, im thinking form follows function and also different shapes
and sizes that make sense in SPACE, you're following a "tall skinny skyscrapper building" when theres no sky and no
gravity downwards. the spacehub uses the black hole in the center as their source of power, and everything is build
around that central idea"

Everything here is placed by what the hole does:
- Its pull falls off as 1/r²: g(r) = 11.4 m/s² × (600 / r)², the WRAITH's full forward thrust at the Horizon Line
  (600 m), 2.4 m/s² on the Current (1300 m), 0.7 m/s² at the perimeter. Inside the Horizon Line a starter ship
  cannot climb out.
- The Current is the circular orbit at 1300 m: v = sqrt(g r) ≈ 56 m/s. Cut your engines on it and you orbit; it is
  the city's ring road, and the frame-dragged inner lane runs faster still.
- The accretion disk powers the city. Eight mirror petals ring it, tilted out of the plane like an iris, focusing its
  light onto receivers; conduits carry the power out to each quarter, and energy canisters (cells) float at the nodes.
- The disk flares when it is fed. The Breakers feed it scrap down the Drain, a decaying-orbit spiral, so a flare
  follows every wreck; the mirrors' shadows are the only shelter (the reactor-pulse mechanic, at city scale).
- Structures orbit, so they line up with the hole: habitat drums point their axis at it (gravity-gradient stable),
  rings spin for their own gravity, radiators stand edge-on to it, collectors face it. Nothing stands on a floor:
  every form is centred on its own level, and the city carries on in lower orbits below the plane.
"""
import math
import random

from hub_layouts import (City, Fabric, CLASSES, polar, bearing_of, box, annulus, ngon, arc, lerp, sector_of, LANE_HALF)

G0, R_NR = 11.4, 600.0          # pull at the Horizon Line (m/s²), the Horizon Line (m)


def g_at(r):
    return G0 * (R_NR / r) ** 2


# ---------------------------------------------------------------- assemblies: what each kind of structure is for
def hab_ring(C, c, rng, district, z=0.0, R=None):
    """A spinning habitat ring: people live on its inner face, spin gives them weight. Spokes to a docking hub."""
    R = R or rng.uniform(60, 190)
    C.shape("torus", c, district, "hab_ring", rng.uniform(0.6, 1.0), z, R=R, r=max(7.0, R * 0.12), spokes=rng.choice([3, 4, 6]), hub=R * 0.2)
    C.shape("sphere", c, district, "hub", 0.8, z, r=R * 0.2)
    return R * 1.12


def hab_drum(C, c, rng, district, z=0.0, L=None):
    """A habitat drum, its axis on the hole (gravity-gradient stable), mirrors along it, a dock at the outer end."""
    L = L or rng.uniform(150, 420)
    rad = rng.uniform(24, 56)
    hd = bearing_of(c)
    C.shape("drum", c, district, "hab_drum", rng.uniform(0.6, 1.0), z, heading=hd, length=L, radius=rad)
    C.shape("sphere", polar(hd, L / 2 + rad * 0.6, c), district, "dock", 0.9, z, r=rad * 0.7)
    for s in (1, -1):
        C.shape("panel", polar(hd + 90, s * (rad + 12), c), district, "mirror", 0.0, z, heading=hd, length=L * 0.8, thick=2.5,
                height=rad * 1.6)
    return L / 2 + rad


def pod_spine(C, c, rng, district, z=0.0):
    """Pods on a truss spine: offices, labs, hostels; the spine carries power and air."""
    L = rng.uniform(120, 260)
    hd = bearing_of(c) + rng.choice([0, 90])
    C.shape("truss", c, district, "truss", 0.0, z, heading=hd, length=L, width=6)
    n = rng.randint(3, 6)
    for k in range(n):
        f = -0.4 + 0.8 * k / max(1, n - 1)
        side = polar(hd + 90, (1 if k % 2 else -1) * rng.uniform(16, 30))
        p = polar(hd, L * f, c)
        C.shape("sphere", (p[0] + side[0], p[1] + side[1]), district, "pod", rng.uniform(0.6, 1.0), z, r=rng.uniform(14, 34))
    return L / 2 + 34


def tank_farm(C, c, rng, district, z=0.0):
    """Tanks and a smelter with radiator fins edge-on to the hole, carrying the waste heat away."""
    hd = bearing_of(c)
    rr = rng.uniform(26, 60)
    C.shape("sphere", c, district, "tank", 0.3, z, r=rr)
    for k in range(rng.randint(1, 3)):
        C.shape("sphere", polar(hd + 90 + rng.uniform(-40, 40), rr + rng.uniform(14, 30), c), district, "tank", 0.2, z,
                r=rr * rng.uniform(0.45, 0.75))
    Lf = rng.uniform(100, 220)
    for s in (1, -1):
        C.shape("panel", polar(hd + s * 90, rr + Lf / 2 + 6, c), district, "radiator", 0.0, z, heading=hd + 90, length=Lf, thick=3,
                height=rng.uniform(40, 70))
    return rr + Lf + 10


def container_raft(C, c, rng, district, z=0.0):
    """Containers lashed to a lattice, the market's warehouses: nothing needs walls when nothing falls."""
    hd = bearing_of(c) + 90
    rows, cols = rng.randint(3, 6), rng.randint(4, 8)
    for i in range(rows):
        for j in range(cols):
            p = polar(hd, (j - (cols - 1) / 2) * 26, c)
            p = polar(hd + 90, (i - (rows - 1) / 2) * 15, p)
            C.b(box(p, hd, 23, 11), z + 6, z - 6, "cargo", district, lit=0.15,
                roof=rng.choice(["tlax", "tlax", "khu", "vest", "scav", None]))
    C.shape("truss", c, district, "truss", 0.0, z, heading=hd, length=cols * 26 + 14, width=4)
    return max(cols * 13, rows * 8) + 18


def platform(C, c, rng, district, z=0.0):
    rp = rng.uniform(36, 70)
    C.b(ngon(c, rp, 6, 30), z + 12, z - 12, "platform", district, lit=0.6)
    C.shape("sphere", c, district, "dome", 0.8, z + 14, r=rp * 0.3)
    return rp + 6


ASSEMBLY = {"hab": [hab_ring, hab_drum, hab_drum, pod_spine], "commerce": [container_raft, container_raft, pod_spine],
            "industrial": [tank_farm, tank_farm, pod_spine], "military": [platform, pod_spine], "lab": [pod_spine, hab_ring]}


def fill_space(C, fab, style_at, tries, seed):
    """Packs the free ground with structures, largest footprints first."""
    rng = random.Random(seed)
    for b in C.buildings:
        fab.occupy(b["pts"], 20)
    for ln in C.lanes:
        fab.cut_line(ln["pts"] + ([ln["pts"][0]] if ln["closed"] else []), LANE_HALF.get(ln["kind"], 40))
    x0, y0, x1, y1 = fab.b
    for _ in range(tries):
        p = (rng.uniform(x0, x1), rng.uniform(y0, y1))
        if not fab.free(p):
            continue
        st = style_at(p)
        if not st:
            continue
        district, kind = st
        build = rng.choice(ASSEMBLY[kind])
        # dry run on a scratch city to measure the footprint, then place for real if it fits
        probe = City("probe", 0)
        r = build(probe, p, random.Random(rng.random()), district)
        if not fab.fits(ngon(p, r, 12)):
            continue
        C.buildings += probe.buildings
        fab.occupy(ngon(p, r, 12), rng.uniform(6, 14))


# ---------------------------------------------------------------- the city
def merchant_hub(C, c, name, livery, district, ship_cls, rng):
    """A trading house: a spinning hub with its own trader docked on a pier and its stock in a raft behind it."""
    hd = bearing_of(c)
    C.shape("torus", c, district, "market", 0.95, 0, R=62, r=9, spokes=4, hub=24)
    C.shape("sphere", c, district, "market", 1.0, 0, r=24)
    L = CLASSES[ship_cls][0]
    pier = polar(hd + 90, 62 + 30 + L / 2 * 0.0, c)
    C.shape("truss", polar(hd + 90, 62 + 24, c), district, "truss", 0.0, 0, heading=hd + 90, length=48, width=6)
    berth = polar(hd + 90, 62 + 48 + CLASSES[ship_cls][1] / 2 + 6, c)
    C.berth(berth, hd, L + 16, name, ship_cls, livery)
    container_raft(C, polar(hd - 90, 150, c), rng, district)
    C.holos.append(dict(text=name, at=(round(c[0], 1), round(c[1] - 90, 1)), district=district, small=True))


def option_d():
    C = City("D", 14)
    rng = random.Random(14)
    R_WALL, R_CUR, R_HARB, R_HARV, DISK = 2350, 1300, 1000, 720, 460
    AVENUES = [0, 45, 90, 135, 180, 225, 270, 315]

    # ---- the core: the hole, its disk, the Horizon Line and the contours of its pull
    C.fx.append(dict(type="black_hole", c=(0, 0), horizon=25, disk=DISK))
    C.fx.append(dict(type="buoys", c=(0, 0), r=R_NR, col=(255, 60, 40), every=45))
    C.fx.append(dict(type="buoys", c=(0, 0), r=900, col=(255, 179, 71), every=70))
    for r, a in ((1800, 60), (2600, 40)):
        C.fx.append(dict(type="contour", c=(0, 0), r=r, a=a))

    # ---- the Mirrors (owner: "large mirror structures around the black hole syphoning its energy"): eight petals
    # 420 m long tilted out of the plane like an iris, alternately rising above it and dipping below, each focusing the
    # disk's light onto a receiver that feeds a conduit out to its quarter. Their shadows are the shelter from flares.
    for k in range(8):
        b = k * 45 + 22.5
        up = 1 if k % 2 == 0 else -1
        C.shape("petal", polar(b, 770), "harbour", "mirror_petal", 1.0, 0, bearing=b, r0=560, r1=990, width=30,
                z0=-30 * up, z1=300 * up, sag=40)
        C.fx.append(dict(type="flare_shadow", pts=annulus(600, R_HARB - 40, b - 11, b + 11)))
        rc = polar(b, 640)
        C.shape("sphere", rc, "harbour", "receiver", 1.0, 150 * up, r=26)
        C.shape("truss", rc, "harbour", "truss", 0.0, 75 * up, heading=b, length=24, width=6)
        C.fx.append(dict(type="beam", a=rc, za=150 * up, b=polar(b, 1100), zb=0))
    # conduits: power out to each quarter from the receivers, canisters (energy cells) at the nodes
    for b in AVENUES:
        cb = b + 22.5
        pts = [polar(cb, 1000), polar(cb, R_HARB + 120), polar(cb, 1750)]
        C.fx.append(dict(type="conduit", pts=pts))
        for r in (1160, 1450):
            C.shape("sphere", polar(cb, r), "harbour", "node", 1.0, 0, r=11)
        for k in range(3):
            C.fx.append(dict(type="canister", c=polar(cb + rng.uniform(-3, 3), 1160 + rng.uniform(-60, 60))))

    # ---- the Harbour Ring: a slab ring in eight arcs, piers outward with liners and freighters alongside
    for b0 in AVENUES:
        C.b(annulus(R_HARB - 34, R_HARB + 34, b0 + 5, b0 + 40), 34, -34, "ring", "harbour", lit=0.85)
    for b in [i * 11.25 + 5.6 for i in range(32)]:
        if any(abs((b - a + 180) % 360 - 180) < 6 for a in AVENUES):
            continue
        C.shape("truss", polar(b, R_HARB + 34 + 60), "harbour", "pier", 0.6, 0, heading=b, length=120, width=8)
    for i, b in enumerate([i * 22.5 + 11.25 for i in range(16)]):
        if rng.random() < 0.25:
            continue
        cls = rng.choice(["freighter", "hauler", "liner", "barge", "hauler"])
        L = CLASSES[cls][0]
        bc = polar(b, R_HARB + 34 + 14 + L / 2)
        C.berth(bc, b, L + 10, f"pier {i + 1}", cls, rng.choice(["tlax", "tlax", "civ", "vest", "khu"]))
    # Harbour Control, beside the Grand Avenue: the Harbour Master (the one reset)
    hc = polar(198, 1190)
    C.shape("torus", hc, "harbour", "control", 1.0, 0, R=100, r=12, spokes=6, hub=40)
    C.shape("sphere", hc, "harbour", "control", 1.0, 0, r=40)
    C.fx.append(dict(type="lab", c=hc))

    # ---- lanes: the Current (the orbit), the service orbit, the avenues, the Drain, the Slingshot
    current = arc(R_CUR, 360, 0, step=1.5)
    C.lane(current, "current", closed=True)
    C.traffic_on(current, 22, [("shuttle", "civ"), ("hauler", "tlax"), ("courier", "vest"), ("freighter", "tlax"), ("tug", "scav"),
                               ("wraith", "noct"), ("shuttle", "ark"), ("liner", "civ")], 81)
    service = arc(880, 0, 360, step=2)
    C.lane(service, "harbour", closed=True)
    C.traffic_on(service, 6, [("tug", "ark"), ("courier", "ark")], 82)
    for b in AVENUES:
        if b == 180:
            continue
        C.lane([polar(b, R_CUR + 40), polar(b, 2150)], "district", oneway=False)
    inbound = [(70, -4400), (70, -R_WALL), (70, -1500)] + [polar(b, R_CUR) for b in (176, 168, 160)]
    outbound = [polar(b, R_CUR) for b in (200, 192, 186)] + [(-70, -1500), (-70, -R_WALL), (-70, -4400)]
    C.lane(inbound, "approach_in"); C.lane(outbound, "approach_out")
    C.traffic_on(inbound, 7, [("liner", "tlax"), ("freighter", "tlax"), ("hauler", "khu"), ("shuttle", "civ"), ("barge", "civ"),
                              ("courier", "vest")], 83)
    C.traffic_on(outbound, 6, [("freighter", "tlax"), ("hauler", "civ"), ("shuttle", "ark"), ("courier", "civ"), ("liner", "civ"),
                               ("tug", "scav")], 84, 0.5)
    for y in range(-4300, -R_WALL - 100, 400):
        C.fx.append(dict(type="marker", c=(0, y)))
    drain = [polar(90 - t * 330, 1700 - t * (1700 - DISK)) for t in [i / 80 for i in range(81)]]
    C.lane(drain, "drain")
    for t in (0.3, 0.55, 0.8):
        p = polar(90 - t * 330, 1700 - t * (1700 - DISK))
        C.fx.append(dict(type="hulk", c=p, heading=bearing_of(p) - 90, length=200 - t * 120, width=60 - t * 30))
        C.ship(polar(bearing_of(p) + 4, math.hypot(*p)), bearing_of(p) - 90, "tug", "scav", "towing")
    sling = [polar(247, 2150), polar(240, 1600), polar(225, 1100)] + [polar(225 - k * 15, 760) for k in range(1, 12)] + \
            [polar(45, 1100), polar(60, 1500)]
    C.lane(sling, "slingshot")

    # ---- the perimeter: defence platforms with turrets, field-wall pylons, the Harbour Mouth gate ring
    gates = (180, 0, 247)
    for k in range(48):
        b = k * 7.5 + 3.75
        if min(abs((b - g + 180) % 360 - 180) for g in gates) < 8:
            continue
        p = polar(b, R_WALL)
        if k % 4 == 0:
            C.b(ngon(p, 42, 6, 30), 14, -14, "bastion", "gate", lit=0.6)
            C.shape("sphere", p, "gate", "dome", 0.9, 14, r=12)
            C.fx.append(dict(type="turret", c=polar(b, R_WALL + 60), facing=b, reach=520, arc=110))
        else:
            C.shape("sphere", p, "gate", "pylon", 1.0, 0, r=9)
            q = polar(b + 7.5, R_WALL)
            if k % 4 != 3 and min(abs((b + 7.5 - g + 180) % 360 - 180) for g in gates) >= 8:
                C.fx.append(dict(type="field_wall", a=p, b=q))
    for s in (1, -1):
        C.shape("drum", (s * 175, -R_WALL), "gate", "gatehouse", 1.0, 0, heading=90, length=110, radius=34)
    C.shape("torus", (0, -R_WALL), "gate", "gate_ring", 1.0, 0, R=165, r=12, upright=True, heading=0)
    C.fx.append(dict(type="scanner", a=(-110, -R_WALL - 60), b=(110, -R_WALL - 60)))
    C.fx.append(dict(type="signal", c=(70, -R_WALL - 150), state="green"))
    C.fx.append(dict(type="signal", c=(-70, -R_WALL - 150), state="green"))
    C.holos.append(dict(text="MERIDIAN", at=(0, -R_WALL + 40), district="gate", grand=True))
    spawn = dict(c=(150, -3500), heading=0)
    C.fx.append(dict(type="patrol", c=(-420, -3000), heading=20))

    # ---- the quarters (A's bearings), each built in space forms for its job
    bE, bB, bF, bY, bR, bP, bI, bC = 135, 90, 45, 0, 315, 270, 225, 247
    for i, (name, liv, cls) in enumerate([("TLAXCALA", "tlax", "freighter"), ("KHUCHAD", "khu", "hauler"),
                                          ("VESTARI", "vest", "courier"), ("SWAP", "scav", "tug")]):
        r = 1520 + i * 200
        merchant_hub(C, polar(bE + (8 if i % 2 == 0 else -8) * (1700 / r), r), name, liv, "exchange", cls, rng)
    C.district("exchange", "THE EXCHANGE", polar(bE, 1750),
               "Four trading houses, spinning hubs with their traders docked alongside and their stock in rafts behind.",
               ["TLAXCALA: bulk goods, fuel, ore; buys anything", "KHUCHAD: weapon blocks",
                "VESTARI: shields, reactors, high-tech blocks", "SWAP: used and salvaged blocks"], "EXCHANGE")
    for k, off in enumerate((-10, 0, 10)):
        c = polar(bB + off, 1880)
        C.shape("truss", c, "breakers", "slipway", 0.4, 0, heading=bB, length=380, width=10)
        C.fx.append(dict(type="hulk", c=polar(bB + off + 2.5, 1880), heading=bB, length=300 - k * 40, width=70))
        C.fx.append(dict(type="rig", c=polar(bB + off, 1700), facing=bB))
    C.fx.append(dict(type="debris", c=polar(bB - 4, 1560), r=160, n=36))
    C.district("breakers", "BREAKERS' YARD", polar(bB, 1800),
               "Shipbreakers. What can be sold goes to the Swap; what cannot is towed down the Drain to feed the hole.",
               ["Salvage loose blocks", "Cutting rigs sweep the slipways", "Each wreck fed down the Drain makes the disk flare"],
               "BREAKERS")
    for k in range(3):
        tank_farm(C, polar(bF - 12 + k * 12, 1650 + (k % 2) * 260), rng, "foundry")
    C.fx.append(dict(type="moon", c=polar(52, 3150), r=260, infl=820))
    C.fx.append(dict(type="slow_field", c=polar(50, 2620), r=200))
    C.fx.append(dict(type="minefield", c=polar(28, 2750), r=260, n=26, kind="seeker"))
    C.fx.append(dict(type="minefield", c=polar(16, 2620), r=160, n=14, kind="prox"))
    C.district("foundry", "THE FORGE", polar(bF, 1800),
               "Smelters on the hole's power, radiators edge-on to it; ore barges come in from the quarry moon's well.",
               ["Two wells: slingshot between the moon and the hole", "The refinery's slow field", "The old minefield outside"],
               "FORGE")
    for k, (cls, liv) in enumerate([("wraith", "noct"), ("cruiser", "noct"), ("cruiser", "noct"), ("liner", "noct"),
                                    ("courier", "noct"), ("shuttle", "noct")]):
        off = -14 + k * 5.6
        r = 1880 if k % 2 == 0 else 1680
        c = polar(bY + off, r)
        L = {"liner": 150, "cruiser": 80}.get(cls, 50)
        for s in (1, -1):
            C.shape("truss", polar(bY + off + 90, s * 26, c), "yards", "gantry", 0.8, 0, heading=bY, length=L + 30, width=6)
        for f in (-0.4, 0.0, 0.4):
            C.shape("truss", polar(bY, L * f, c), "yards", "gantry", 0.8, 0, heading=bY + 90, length=60, width=4)
        C.berth(c, bY, L, "yards", cls, liv)
    lab = polar(bY + 16, 1560)
    hab_ring(C, lab, rng, "yards", R=70)
    C.fx.append(dict(type="lab", c=lab))
    C.fx.append(dict(type="engine_stand", c=polar(bY + 16, 2120), facing=bY + 180, length=420))
    C.lane([polar(bY, R_CUR + 40), polar(bY, R_WALL + 400), polar(bY, 3200)], "launch")
    C.district("yards", "MERIDIAN YARDS", polar(bY, 1780),
               "Open drydock frames with every ship in the game; the Blueprint Lab spins in its own ring.",
               ["Fly into a dock to take that ship", "Engine test stand: engine wash across the yard"], "YARDS")
    for k in range(6):
        p = polar(bR - 15 + k * 3.2, 1500)
        C.shape("truss", p, "range", "hangar", 0.6, 0, heading=bR + 90, length=40, width=4)
        C.ship(polar(bR - 15 + k * 3.2, 1530), bR, ["wraith", "cruiser", "cruiser", "wraith", "courier", "cruiser"][k], "noct", "docked")
    for d in (200, 400, 600, 800):
        C.fx.append(dict(type="range_mark", c=polar(bR, 1540 + d), facing=bR))
    for k in range(5):
        C.fx.append(dict(type="target", c=polar(bR - 12 + k * 6, 1800 + (k % 3) * 150)))
    C.fx.append(dict(type="target_station", c=polar(bR, 2180)))
    for s in (-1, 1):
        C.fx.append(dict(type="field_wall", a=polar(bR + s * 17, 1540), b=polar(bR + s * 17, 2250)))
    C.fx.append(dict(type="turret_alley", a=polar(bR + 24, 1500), b=polar(bR + 24, 2200)))
    C.district("range", "PROVING RANGE", polar(bR, 1800), "Every ship in every weapon fit; targets out to 800 m.",
               ["Field walls bound the lanes", "Turret Alley", "Scoreboard"], "RANGE")
    pit = polar(bP, 1800)
    C.shape("torus", pit, "pit", "stands", 0.95, 0, R=380, r=34, spokes=0, hub=0)
    C.fx.append(dict(type="arena", c=pit, r=330))
    for b0 in (0, 90, 180, 270):
        C.fx.append(dict(type="bout_gate", c=polar(b0 + 22.5, 380, pit)))
    C.district("pit", "THE PIT", pit, "The arena inside a spinning stands ring; the crowd lives in the ring.",
               ["Four bouts", "Field-wall edge"], "THE PIT")
    for i, n in enumerate(["MIRROR", "MAGNET", "PORTAL", "SHIELD", "REACTOR", "SWITCH+DOOR", "ARM", "REPULSOR", "EMP", "SEEKER",
                           "GRAVITY", "CANISTER", "DRIVER", "RAILS"]):
        r = 1620 if i < 7 else 1900
        b = bI - 15 + (i % 7) * 5
        C.shape("sphere", polar(b, r), "institute", "pod", 1.0, 0, r=22)
        C.fx.append(dict(type="exhibit", c=polar(b, r), name=n))
    for r in (1620, 1900):                       # the gallery spines follow the arc, in short straight runs
        for k in range(8):
            b = bI - 16 + 4 * k + 2
            C.shape("truss", polar(b, r), "institute", "truss", 0.0, 0, heading=b + 90, length=2 * math.pi * r * 4 / 360, width=5)
    C.district("institute", "THE INSTITUTE", polar(bI, 1760), "Exhibit pods along two spines: every block and mechanism, working.",
               ["Fourteen exhibits"], "INSTITUTE")
    hab_ring(C, polar(bC, 2120), rng, "circuit", R=110)
    C.fx.append(dict(type="portal", c=polar(bC, R_WALL + 260), facing=bC, width=160))
    C.district("circuit", "THE CIRCUIT", polar(bC, 2120),
               "The racing quarter. The Slingshot trial dives round the hole and out; the start portal opens onto Track 01.",
               ["The Slingshot: learn a gravity assist", "Race Track 01"], "CIRCUIT")
    C.district("harbour", "THE HARBOUR", (0, 0), "The hole, its disk, the Harvest Ring and the Harbour Ring.", [], None)
    C.district("gate", "HARBOUR MOUTH", (0, -R_WALL), "The gate ring, customs scan and signals.", [], None)

    # ---- the megastructures (owner: "buildings can be connected into large dense clusters, i want megastructures").
    # The quarters' band is cut by narrow flight streets into cells; neighbouring cells merge into long continuous
    # slabs, each stepped in tiers round the plane and hanging its deep decks below it; machinery covers them and
    # docking arms reach into the streets. Open space is left only where something happens (a plaza for a mechanic).
    fab = Fabric((-R_WALL - 60, -R_WALL - 60, R_WALL + 60, R_WALL + 60))
    fab.region(ngon((0, 0), R_WALL + 60, 120))
    fab.cut(box((0, -1800), 0, 1300, 300))                                   # the Grand Avenue
    fab.cut(annulus(1500, R_WALL, bR - 12, bR + 12))                       # the firing lanes
    for m in C.buildings:                                                    # every set piece keeps a clear apron
        if m.get("district") in ("yards", "institute", "breakers", "range", "circuit", "foundry") and m["kind"] != "truss":
            fab.occupy(m["pts"], 40)
    for f_ in C.fx:
        if f_["type"] in ("hulk", "target", "target_station", "engine_stand", "debris", "lab"):
            fab.cut_circle(f_["c"], f_.get("length", f_.get("r", 60)) * 0.6 + 50)
    for c_, r_ in ((pit, 450), (polar(bC, 2120), 170), (hc, 160)):
        fab.cut_circle(c_, r_)
    for bb in AVENUES:                                                       # the avenues stay open
        fab.cut_line([polar(bb, R_CUR), polar(bb, R_WALL)], 70)
    for m in [x for x in C.buildings if x.get("district") == "exchange"]:    # the trading houses keep their berths clear
        fab.occupy(m["pts"], 40)
    fab.cut_line(drain, 70); fab.cut_line(sling, 60)
    sectors = dict(yards=0, foundry=45, breakers=90, exchange=135, gate=180, institute=225, pit=270, range=315)
    mrng = random.Random(31)
    RINGS = [R_CUR + 110, 1640, 1890, 2120, R_WALL - 40]
    ST = 26                                                                  # half-width of a street
    for i in range(len(RINGS) - 1):
        r0, r1 = RINGS[i] + ST, RINGS[i + 1] - ST
        step = 6.5 if i < 2 else 5.5
        bs = [k * step + (step / 2 if i % 2 else 0) + mrng.uniform(-1.2, 1.2) for k in range(int(360 / step))]
        k = 0
        while k < len(bs):
            span = 1 + (mrng.random() < 0.45) + (mrng.random() < 0.25)   # merge 1-3 cells into one slab
            span = min(span, len(bs) - k)
            for trial in (span, 1):
                b0 = bs[k]; b1 = bs[k + trial] if k + trial < len(bs) else bs[0] + 360
                dm_ = math.degrees(ST / ((r0 + r1) / 2))
                pr = [polar(b0 + dm_ + (b1 - b0 - 2 * dm_) * u, r0 + (r1 - r0) * v) for u in (0.1, 0.5, 0.9) for v in (0.15, 0.5, 0.85)]
                if sum(fab.free(q) for q in pr) >= 8:
                    span = trial
                    break
            k += span
            dm = math.degrees(ST / ((r0 + r1) / 2))
            cell = annulus(r0, r1, b0 + dm, b1 - dm, step=1.5)
            mid = polar((b0 + b1) / 2, (r0 + r1) / 2)
            probes = [polar(b0 + dm + (b1 - b0 - 2 * dm) * u, r0 + (r1 - r0) * v) for u in (0.1, 0.5, 0.9) for v in (0.15, 0.5, 0.85)]
            if sum(fab.free(q) for q in probes) < 8:
                continue
            dist = sector_of(mid, sectors)
            edge = min(abs((bearing_of(mid) - sectors[q] + 180) % 360 - 180) for q in sectors)
            dist = "old_town" if edge > 15 else dist
            heavy = dist in ("foundry", "breakers", "yards")
            # the slab, crossing the plane; tiers stepping out above and below it; deep decks hanging under
            C.b(cell, mrng.uniform(16, 34), -mrng.uniform(50, 120), "hull", dist, lit=mrng.uniform(0.35, 0.8))
            for t in range(mrng.randint(1, 3)):
                ins = (t + 1) * mrng.uniform(10, 22)
                db = math.degrees(ins / ((r0 + r1) / 2))
                if r1 - r0 - 2 * ins < 30 or b1 - b0 - 2 * dm - 2 * db < 2:
                    break
                b0t, b1t = b0 + dm + db + mrng.uniform(0, 1.5), b1 - dm - db - mrng.uniform(0, 1.5)
                tier = annulus(r0 + ins, r1 - ins, b0t, b1t, step=1.5)
                C.b(tier, 34 + (t + 1) * mrng.uniform(12, 30) * (0.7 if heavy else 1.0), -120 - (t + 1) * mrng.uniform(60, 150),
                    "hull", dist, lit=mrng.uniform(0.5, 1.0))
            deep = annulus(r0 - 10, r1 + 10, b0 + dm * 0.5, b1 - dm * 0.5, step=2)
            C.b(deep, -260, -mrng.uniform(500, 1100), "hull", dist, lit=mrng.uniform(0.3, 0.7))
            # machinery on the slab: tanks, blocks, fins and masts; docking arms into the street with ships alongside
            area = (r1 - r0) * math.radians(b1 - b0) * (r0 + r1) / 2
            for _ in range(int(area / 5200)):
                bb = mrng.uniform(b0 + dm + 1, b1 - dm - 1); rr = mrng.uniform(r0 + 18, r1 - 18)
                q = polar(bb, rr); roll = mrng.random()
                top = 60 + mrng.uniform(0, 50)
                if roll < 0.3:
                    C.shape("sphere", q, dist, "tank", 0.3, top - 20, r=mrng.uniform(8, 22) * (1.4 if heavy else 1.0))
                elif roll < 0.62:
                    L_ = mrng.uniform(20, 70)
                    C.b(box(q, bb + mrng.choice([0, 90]), L_, L_ * mrng.uniform(0.4, 0.9)), top, top - mrng.uniform(20, 50), "module", dist,
                        lit=mrng.uniform(0.4, 1.0))
                elif roll < 0.82:
                    C.shape("panel", q, dist, "radiator" if heavy else "mirror", 0.0, top, heading=bb, length=mrng.uniform(40, 110),
                            thick=2.5, height=mrng.uniform(30, 70))
                else:
                    C.shape("panel", q, dist, "mast", 0.9, top + 30, heading=bb, length=4, thick=4, height=mrng.uniform(60, 140))
            for _ in range(mrng.randint(0, 2)):
                side = mrng.choice([-1, 1])
                bb = mrng.uniform(b0 + dm + 1, b1 - dm - 1)
                edge_r = r1 if side > 0 else r0
                cls = mrng.choice(["shuttle", "courier", "shuttle", "tug", "hauler"])
                q = polar(bb, edge_r + side * 14)
                C.shape("truss", q, dist, "arm", 0.6, 0, heading=bb, length=28, width=5)
                C.ship(polar(bb + side * 0.0, edge_r + side * (28 + CLASSES[cls][1] / 2)), bb + 90, cls,
                       mrng.choice(["tlax", "civ", "civ", "vest", "khu", "scav"]), "docked")
    # the streets between the slabs are lanes: traffic threads the city, not just its ring road
    for r_ in RINGS[1:-1]:
        lane = arc(r_, 0, 360, step=1.0)
        C.lane(lane, "street", closed=True, oneway=False)
        C.traffic_on(lane, 18, [("shuttle", "civ"), ("courier", "vest"), ("tug", "scav"), ("shuttle", "tlax"), ("hauler", "tlax")],
                     int(r_))
    callouts = [
        ((150, -3500), "You spawn outside, in the inbound stream; you feel the pull from here"),
        ((0, -R_WALL - 60), "Harbour Mouth: the gate ring, customs scan, signals"),
        ((0, 0), "The black hole: its disk powers the city. Red buoys: the Horizon Line"),
        (polar(157.5, 900), "The Mirrors: eight petals focus the disk on their receivers; their shadows shelter you from flares"),
        (polar(160, R_CUR), "The Current: the 1300 m orbit, the city's ring road; cut engines and you orbit"),
        (hc, "Harbour Control: the Harbour Master, the one reset"),
        (polar(bE, 1750), "The Exchange: four trading houses"),
        (polar(bY, 1780), "Meridian Yards: every ship in a drydock frame"),
        (polar(bR, 1800), "Proving Range: ships x weapons"),
        (pit, "The Pit: the arena in a stands ring"),
        (polar(bI, 1760), "The Institute: working blocks and mechanisms"),
        (polar(bB, 1800), "Breakers' Yard; the Drain feeds wrecks to the hole"),
        (polar(bF, 1800), "The Forge: radiators, the quarry moon's second well"),
        (polar(bC, 2120), "The Circuit: the Slingshot trial; portal to Track 01"),
    ]
    return dict(key="D", title="OPTION D  ·  MERIDIAN ON THE HOLE", name="MERIDIAN",
                subtitle="Option A rebuilt round a black hole: the city's power, its ring road and its first lesson.",
                spawn=spawn, bounds=(-2950, -3700, 2950, 2700), wall=R_WALL, seed=14,
                concept=("Meridian was built on a black hole for its power: eight mirror petals 420 m long ring it, focusing the "
                         "disk's light onto receivers that feed every quarter.  The hole's pull is the city's first lesson: you feel it from "
                         "the outer markers, the inbound lane bends into the Current, the 1300 m orbit that is the city's ring "
                         "road (cut your engines and you circle for free), and red buoys mark the Horizon Line past which a "
                         "starter ship cannot climb out. The Breakers feed scrap down the Drain; every wreck makes the disk flare, "
                         "and the mirrors' shadows are the shelter. Nothing here stands on a floor: rings spin for weight, drums "
                         "point at the hole, radiators stand edge-on to it, and the city carries on in lower orbits beneath you."),
                strengths=["One idea explains the whole city: power, traffic, hazards and layout all come from the hole",
                           "Teaches the gravity well by living with it: the pull, orbits, the Horizon Line, slingshots",
                           "Megastructures: each quarter is a few huge connected slabs threaded by flight streets"],
                costs=["Gravity everywhere: AI traffic must fly against it (it already uses real thrusters)",
                       "A black hole needs a new look: lensing, the disk and flares are new VFX"],
                callouts=callouts, **C.data())
