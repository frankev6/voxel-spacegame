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
- The accretion disk powers the city. Eight Siphons reach in from the Harbour Ring: magnetic nozzles at the disk's
  edge pull plasma off it and drive it up their booms into converters; conduits carry the power out to each quarter,
  and energy canisters (cells) float at the nodes.
- The disk flares when it is fed. The Breakers feed it scrap down the Drain, a decaying-orbit spiral, so a flare
  follows every wreck; the converters' lee is the only shelter (the reactor-pulse mechanic, at city scale).
- Structures orbit, so they line up with the hole: habitat drums point their axis at it (gravity-gradient stable),
  rings spin for their own gravity, radiators stand edge-on to it, collectors face it. Nothing stands on a floor:
  every form is centred on its own level, and the city carries on in lower orbits below the plane.
"""
import math
import random

from hub_layouts import (City, Fabric, CLASSES, polar, bearing_of, box, annulus, ngon, arc, lerp, sector_of, LANE_HALF)
import hub_quarters_d as Q

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
    """A habitat drum, its axis on the hole (gravity-gradient stable), a dock at the outer end."""
    L = L or rng.uniform(150, 420)
    rad = rng.uniform(24, 56)
    hd = bearing_of(c)
    C.shape("drum", c, district, "hab_drum", rng.uniform(0.6, 1.0), z, heading=hd, length=L, radius=rad)
    C.shape("sphere", polar(hd, L / 2 + rad * 0.6, c), district, "dock", 0.9, z, r=rad * 0.7)
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


def harvester(C, b, up, r_head=482, r_conv=930):
    """A Siphon: a magnetic nozzle at the disk's edge, a lattice boom, a converter on the Harbour Ring.

    The nozzle is four coil rings standing across the plane, their axis on the hole, narrowing outward so the
    field funnels the plasma up the boom. The boom runs a little above (or below) the plane, so the stream arcs
    up off the disk into the nozzle. Radiators beside the converter stand edge-on to the hole."""
    z = 46 * up
    # the nozzle: a funnel of coils, widest facing the disk, and its cowl
    for i, (dr, R) in enumerate(((0, 52), (26, 42), (50, 32), (72, 24))):
        C.shape("torus", polar(b, r_head + dr), "harbour", "coil", 1.0, z, R=R, r=6.5 - i, upright=True, heading=b)
    C.shape("drum", polar(b, r_head + 100), "harbour", "nozzle", 0.9, z, heading=b, length=60, radius=20)
    # the boom: twin rails with cross-bracing
    r0, r1 = r_head + 130, r_conv - 40
    mid = polar(b, (r0 + r1) / 2)
    for s_ in (-1, 1):
        C.shape("truss", polar(b + 90, s_ * 22, mid), "harbour", "truss", 0.0, z, heading=b, length=r1 - r0, width=10)
    for j in range(int((r1 - r0) // 34) + 1):
        C.shape("truss", polar(b, r0 + j * 34), "harbour", "truss", 0.0, z, heading=b + 90, length=46, width=4)
    # the converter: a squat drum on the ring's inner edge, coolant tanks, radiators edge-on to the hole
    conv = polar(b, r_conv)
    # a power station, big enough to read as the end of the stream: drum, field coils, its glow through the vents
    C.shape("drum", conv, "harbour", "converter", 1.0, z * 0.5, heading=b, length=140, radius=70)
    for f in (-45, 0, 45):
        C.shape("torus", polar(b, f, conv), "harbour", "coil", 1.0, z * 0.5, R=76, r=7, upright=True, heading=b)
    C.fx.append(dict(type="flash", c=polar(b, -72, conv), z=z * 0.5, r=26, col="#FFC890"))
    for s_ in (-1, 1):
        C.shape("sphere", polar(b + 90, s_ * 96, conv), "harbour", "tank", 0.4, z * 0.5, r=26)
        for f in range(6):
            C.shape("panel", polar(b + 90, s_ * (130 + f * 9), polar(b, 30, conv)), "harbour", "radiator", 0.0, z * 0.5,
                    heading=b, length=130, thick=1.5, height=40)
    # the stream: drawn off the disk with its swirl, rising into the nozzle, then straight up the boom
    pts, zs, rs = [], [], []
    for r_, db, zf, w in ((230, -26, 0.0, 1.5), (300, -16, 0.02, 9), (370, -9, 0.12, 18), (430, -4, 0.45, 17),
                          (r_head - 10, 0, 1.0, 13), (r_head + 100, 0, 1.0, 9), (r_conv - 50, 0, 1.0, 7)):
        pts.append(polar(b + db, r_)); zs.append(round(z * zf, 1)); rs.append(w)
    C.fx.append(dict(type="plasma", pts=[(round(x, 1), round(y, 1)) for x, y in pts], z=zs, r=rs))
    C.fx.append(dict(type="flare_shadow", pts=annulus(r_conv + 50, 1240, b - 5, b + 5)))
    # the nozzle is inside the Horizon Line: hazard beacons ring it, a no-go for anything that cannot climb out
    for k in range(8):
        C.fx.append(dict(type="flash", c=polar(k * 45, 80, polar(b, r_head + 30)), z=z, r=4, col="#FF3C28" if k % 2 else "#FFF0D0"))


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
    C.fx.append(dict(type="buoys", c=(0, 0), r=R_NR, col=(255, 60, 40), every=28))
    for r, a in ((1800, 60), (2600, 40)):
        C.fx.append(dict(type="contour", c=(0, 0), r=r, a=a))

    # ---- the Siphons (owner: "contraptions syphoning the black hole's energy"): eight harvester arms reach in from the
    # Harbour Ring to the edge of the disk. Each ends in a magnetic nozzle, a funnel of coil rings that pulls a tongue of
    # plasma off the disk and drives it up the boom into a converter on the ring; the converters feed the conduits.
    # The converters' lee is the shelter from flares.
    for k in range(8):
        harvester(C, k * 45 + 22.5, -1)
    # conduits: power out to each quarter from the converters, canisters (energy cells) at the nodes
    for b in AVENUES:
        cb = b + 22.5
        pts = [polar(cb, 1000), polar(cb, R_HARB + 120), polar(cb, 1750)]
        C.fx.append(dict(type="conduit", pts=pts, z=-6))
        for r in (1160, 1450):
            C.shape("sphere", polar(cb, r), "harbour", "node", 1.0, 0, r=11)
        for k in range(3):
            C.fx.append(dict(type="canister", c=polar(cb + rng.uniform(-3, 3), 1160 + rng.uniform(-60, 60))))

    # ---- the Harbour Ring: a slab ring in eight arcs, piers outward with liners and freighters alongside
    for b0 in AVENUES:
        C.b(annulus(R_HARB - 34, R_HARB + 34, b0 + 5, b0 + 40), -8, -90, "ring", "harbour", lit=0.85)
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
    C.shape("sphere", hc, "harbour", "control", 1.0, -34, r=40)
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
            C.b(ngon(p, 42, 6, 30), -6, -40, "bastion", "gate", lit=0.6)
            Q.gun_turret(C, p, b, "gate", z=-4)
        else:
            C.shape("sphere", p, "gate", "pylon", 1.0, 0, r=9)
            q = polar(b + 7.5, R_WALL)
            if k % 4 != 3 and min(abs((b + 7.5 - g + 180) % 360 - 180) for g in gates) >= 8:
                C.fx.append(dict(type="field_wall", a=p, b=q))
    for s in (1, -1):
        C.shape("drum", (s * 175, -R_WALL), "gate", "gatehouse", 1.0, 0, heading=90, length=110, radius=34)
    C.shape("torus", (0, -R_WALL), "gate", "gate_ring", 1.0, 0, R=165, r=12, upright=True, heading=0)
    C.fx.append(dict(type="scanner", a=(-110, -R_WALL - 60), b=(110, -R_WALL - 60)))
    Q.mouth(C, R_WALL)
    C.fx.append(dict(type="signal", c=(70, -R_WALL - 150), state="green"))
    C.fx.append(dict(type="signal", c=(-70, -R_WALL - 150), state="green"))
    C.holos.append(dict(text="MERIDIAN", at=(0, -R_WALL + 40), district="gate", grand=True))
    spawn = dict(c=(150, -3500), heading=0)
    C.fx.append(dict(type="patrol", c=(-420, -3000), heading=20))

    # ---- the quarters, each filling its arc with the machinery of its job (hub_quarters_d.py)
    forge_k = Q.forge(C, rng, R_CUR, R_WALL)
    salvage_k = Q.salvage(C, rng)
    exchange_k = Q.exchange(C, rng, R_WALL, merchant_hub)
    Q.bazaar(C, rng)
    pit_k, pit = Q.pit(C, rng)
    proving_k = Q.proving(C, rng)
    bC = 247
    hab_ring(C, polar(bC, 2140), rng, "circuit", R=80)
    C.fx.append(dict(type="portal", c=polar(bC, R_WALL + 260), facing=bC, width=160))
    C.district("circuit", "THE CIRCUIT", polar(bC, 2140),
               "The racing quarter. The Slingshot trial dives round the hole and out; the start portal opens onto Track 01.",
               ["The Slingshot: learn a gravity assist", "Race Track 01"], "CIRCUIT")
    C.district("harbour", "THE HARBOUR", (0, 0), "The hole, its disk, the Siphons and the Harbour Ring.", [], None)
    C.district("gate", "HARBOUR MOUTH", (0, -R_WALL), "Gun keeps, batteries, the picket, mine belts, customs.", [], None)

    # ---- the megastructures (owner: "buildings can be connected into large dense clusters, i want megastructures").
    # The quarters' band is cut by narrow flight streets into cells; neighbouring cells merge into long continuous
    # slabs, each stepped in tiers round the plane and hanging its deep decks below it; machinery covers them and
    # docking arms reach into the streets. Open space is left only where something happens (a plaza for a mechanic).
    fab = Fabric((-R_WALL - 60, -R_WALL - 60, R_WALL + 60, R_WALL + 60))
    fab.region(ngon((0, 0), R_WALL + 60, 120))
    fab.cut(box((0, -1800), 0, 1300, 300))                                   # the Grand Avenue
    for m in C.buildings:                                                    # every set piece keeps a clear apron
        if m.get("district") in ("foundry", "breakers", "exchange", "range", "pit", "circuit", "gate") and m.get("shape") != "truss" \
                and m["kind"] not in ("rib", "keel", "turret", "barrel"):
            fab.occupy(m["pts"], 14)
    for f_ in C.fx:
        if f_["type"] in ("hulk", "target", "target_station", "engine_stand", "debris", "lab", "reactor", "portal", "minefield"):
            fab.cut_circle(f_["c"], f_.get("length", f_.get("r", 60)) * 0.6 + 40)
    for b_, r0_, r1_, w_ in forge_k["cuts"]:
        fab.cut_line([polar(b_, r0_), polar(b_, r1_)], w_ / 2)
    for b_, r0_, r1_, w_ in proving_k["lanes"]:
        fab.cut(annulus(r0_, r1_, b_ - w_ / 2, b_ + w_ / 2))
    for b_, r_ in proving_k["cells"]:
        fab.cut_circle(polar(b_, r_), 78)
    for c_, r_ in forge_k["circles"] + salvage_k["circles"] + exchange_k["circles"] + pit_k["circles"] + proving_k["circles"] + \
            [(polar(bC, 2140), 130), (hc, 160)]:
        fab.cut_circle(c_, r_)
    for bb in AVENUES:                                                       # the avenues stay open
        fab.cut_line([polar(bb, R_CUR), polar(bb, R_WALL)], 70)
    fab.cut_line(drain, 70); fab.cut_line(sling, 60)
    mrng = random.Random(31)
    RINGS = [R_CUR + 110, 1640, 1890, 2120, R_WALL - 40]
    ST = 40                                                                  # half-width of a main street (80 m)
    # owner (2026-10-18, agreeing the review): the city's mass hangs below the flight plane, so the 55° camera sees
    # roofs, docks and machinery rather than walls; only masts, coils, signs, guns and ships rise above it. And
    # "buildings should have a cyberpunk feel to them with neon signage".
    TOP = -14
    NEON = ["#FF2E88", "#22E6FF", "#FFB000", "#9DFF3C", "#B44CFF", "#FF5530"]
    # per quarter: tank, module, radiator fins, mast, neon sign (the rest); docked ships per slab; how lit it is
    MIX = {"exchange": (0.08, 0.40, 0.04, 0.08, (2, 5), (0.7, 1.0)), "foundry": (0.15, 0.35, 0.30, 0.12, (0, 2), (0.4, 0.8)),
           "breakers": (0.35, 0.30, 0.20, 0.10, (0, 2), (0.15, 0.45)), "range": (0.15, 0.50, 0.10, 0.18, (0, 1), (0.6, 1.0)),
           "pit": (0.08, 0.40, 0.05, 0.12, (1, 3), (0.6, 1.0)), "old_town": (0.15, 0.40, 0.10, 0.10, (1, 3), (0.5, 0.9))}

    def slab(r0, r1, b0, b1, st, small=False):
        rm = (r0 + r1) / 2
        dm = math.degrees(st / rm)
        if r1 - r0 < 40 or b1 - b0 - 2 * dm < 1.2:
            return False
        probes = [polar(b0 + dm + (b1 - b0 - 2 * dm) * u, r0 + (r1 - r0) * v) for u in (0.1, 0.5, 0.9) for v in (0.15, 0.5, 0.85)]
        if sum(fab.free(q) for q in probes) < 8:
            return False
        mid = polar((b0 + b1) / 2, rm)
        dist = Q.quarter_at(bearing_of(mid))
        dist = "old_town" if dist in ("gate", "circuit") else dist
        mix = MIX[dist]
        heavy = dist in ("foundry", "breakers")
        cell = annulus(r0, r1, b0 + dm, b1 - dm, step=1.5)
        top = TOP - mrng.uniform(0, 30)
        C.b(cell, top, top - mrng.uniform(60, 140), "hull", dist, lit=mrng.uniform(*mix[5]) * 0.8)
        # tiers hang below the roof, each narrower and deeper: the bulk of the city is under you
        for t in range(mrng.randint(1, 3)):
            ins = (t + 1) * mrng.uniform(10, 22)
            db = math.degrees(ins / rm)
            if r1 - r0 - 2 * ins < 30 or b1 - b0 - 2 * dm - 2 * db < 2:
                break
            tier = annulus(r0 + ins, r1 - ins, b0 + dm + db, b1 - dm - db, step=1.5)
            C.b(tier, top - 90 - t * 60, -260 - (t + 1) * mrng.uniform(60, 150), "hull", dist, lit=mrng.uniform(*mix[5]))
        C.b(annulus(r0 - 10, r1 + 10, b0 + dm * 0.5, b1 - dm * 0.5, step=2), -320, -mrng.uniform(600, 1100), "hull", dist,
            lit=mrng.uniform(0.3, 0.7))
        # the roof: machinery, fins, masts and neon
        area = (r1 - r0) * math.radians(b1 - b0) * rm
        for _ in range(int(area / 3800)):
            bb = mrng.uniform(b0 + dm + 0.6, b1 - dm - 0.6); rr = mrng.uniform(r0 + 16, r1 - 16)
            q = polar(bb, rr); roll = mrng.random()
            if roll < mix[0]:
                rad = mrng.uniform(8, 20) * (1.3 if heavy else 1.0)
                C.shape("sphere", q, dist, "tank", 0.3, top - rad * 0.4, r=rad)
            elif roll < mix[0] + mix[1]:
                L_ = mrng.uniform(18, 60)
                C.b(box(q, bb + mrng.choice([0, 90]), L_, L_ * mrng.uniform(0.4, 0.9)), top + mrng.uniform(4, 16), top - 10, "module", dist,
                    lit=mrng.uniform(*mix[5]))
            elif roll < mix[0] + mix[1] + mix[2]:
                # a radiator array: thin fins in a row, edge-on to the hole, glowing at the root
                n = mrng.randint(4, 8); Lf = mrng.uniform(30, 70); hf = mrng.uniform(14, 26)
                for f in range(n):
                    C.shape("panel", polar(bb + 90, (f - (n - 1) / 2) * 7, q), dist, "radiator", 0.0, top + hf / 2, heading=bb,
                            length=Lf, thick=1.5, height=hf)
            elif roll < mix[0] + mix[1] + mix[2] + mix[3]:
                hm = mrng.uniform(50, 130)
                C.shape("panel", q, dist, "mast", 0.9, top + hm / 2, heading=bb, length=4, thick=4, height=hm)
            else:
                # neon: a sign board standing on the roof, or a tall blade; the glyphs are colour, not words
                tall = mrng.random() < 0.4
                w_, h_ = (mrng.uniform(5, 8), mrng.uniform(30, 60)) if tall else (mrng.uniform(20, 50), mrng.uniform(8, 16))
                C.fx.append(dict(type="neon", c=q, heading=bb + 90, length=w_, height=h_, z=round(top + 6 + h_ / 2, 1),
                                 col=mrng.choice(NEON)))
        # neon trim along the roof's street edges
        for edge in (r0 + 3, r1 - 3):
            if mrng.random() < (0.75 if dist in ("exchange", "pit", "old_town") else 0.35):
                C.fx.append(dict(type="neon_line", pts=[(round(x, 1), round(y, 1)) for x, y in arc(edge, b0 + dm, b1 - dm, step=1.5)],
                                 z=round(top + 1, 1), col=mrng.choice(NEON)))
        # docking arms into the street, each with its ship alongside
        lo, hi = mix[4]
        for _ in range(mrng.randint(0, 1) if small else mrng.randint(lo, hi)):
            side = mrng.choice([-1, 1])
            bb = mrng.uniform(b0 + dm + 0.5, b1 - dm - 0.5)
            edge_r = r1 if side > 0 else r0
            cls = mrng.choice(["shuttle", "courier", "shuttle", "tug", "hauler"])
            C.shape("truss", polar(bb, edge_r + side * 14), dist, "arm", 0.6, 0, heading=bb, length=28, width=5)
            C.ship(polar(bb, edge_r + side * (28 + CLASSES[cls][1] / 2)), bb + 90, cls,
                   mrng.choice(["tlax", "civ", "civ", "vest", "khu", "scav"]), "docked")
        return True

    for i in range(len(RINGS) - 1):
        r0, r1 = RINGS[i] + ST, RINGS[i + 1] - ST
        step = 6.5 if i < 2 else 5.5
        bs = [k * step + (step / 2 if i % 2 else 0) + mrng.uniform(-1.2, 1.2) for k in range(int(360 / step))]
        k = 0
        while k < len(bs):
            span = min(1 + (mrng.random() < 0.45) + (mrng.random() < 0.25), len(bs) - k)   # merge 1-3 cells into one slab
            for trial in (span, 1):
                b0 = bs[k]; b1 = bs[k + trial] if k + trial < len(bs) else bs[0] + 360
                if slab(r0, r1, b0, b1, ST):
                    break
            else:
                # round the quarters' machinery the block breaks into smaller ones that fit the gaps
                b0 = bs[k]; b1 = bs[k + 1] if k + 1 < len(bs) else bs[0] + 360
                rm_, bm_ = (r0 + r1) / 2, (b0 + b1) / 2
                for ra, rb in ((r0, rm_ - 8), (rm_ + 8, r1)):
                    for ba, bb_ in ((b0, bm_ + 0.3), (bm_ - 0.3, b1)):
                        slab(ra, rb, ba, bb_, 12, small=True)
                trial = 1
            k += trial
    # the streets between the slabs are lanes: traffic threads the city, not just its ring road
    for r_ in RINGS[1:-1]:
        lane = arc(r_, 0, 360, step=1.0)
        C.lane(lane, "street", closed=True, oneway=False)
        C.traffic_on(lane, 40, [("shuttle", "civ"), ("courier", "vest"), ("tug", "scav"), ("shuttle", "tlax"), ("hauler", "tlax"),
                                ("courier", "khu"), ("shuttle", "civ")], int(r_))
    for m in C.buildings:
        if m.get("shape") is None and m["h"] > TOP + 20 and m["kind"] not in ("hull_new",):
            dz = m["h"] - (TOP + 6)
            m["h"] = round(m["h"] - dz, 1); m["d"] = round(m["d"] - dz, 1)
        elif m.get("shape") == "sphere" and m["kind"] in ("market", "lab", "hub", "dock", "pod_big") and m["z"] > -m["p"]["r"] * 0.5:
            dz = m["z"] + m["p"]["r"] * 0.6
            m["z"] = round(m["z"] - dz, 1); m["h"] = round(m["h"] - dz, 1); m["d"] = round(m["d"] - dz, 1)
    callouts = [
        ((150, -3500), "You spawn outside, in the inbound stream; you feel the pull from here"),
        ((0, -R_WALL - 60), "Harbour Mouth: gun keeps, batteries, the picket, mine belts, customs"),
        ((0, 0), "The black hole: its disk powers the city. Red buoys: the Horizon Line"),
        (polar(157.5, 900), "The Siphons: eight arms draw plasma off the disk into converters; their lee shelters you from flares"),
        (polar(160, R_CUR), "The Current: the 1300 m orbit, the city's ring road; cut engines and you orbit"),
        (hc, "Harbour Control: the Harbour Master, the one reset"),
        (polar(141, 1800), "The Exchange: trading houses, Tech Bazaar, Engine Mart, the Undermarket"),
        ((0, -1800), "The Bazaar: stalls along the Grand Avenue"),
        (polar(20, 1800), "The Forge: assembly lines, frame to launch; the Refit Dock edits your ship"),
        (polar(86, 1900), "The Salvage Yard: junk ships cut into chunks; the Drain feeds the hole"),
        (pit, "The Pit: a coliseum, ships fight on its floor"),
        (polar(297, 1880), "Proving Grounds: the Line, Weapons Range, Block Lab, Mechanics trials"),
        (polar(bC, 2140), "The Circuit: the Slingshot trial; portal to Track 01"),
    ]
    return dict(key="D", title="OPTION D  ·  MERIDIAN ON THE HOLE", name="MERIDIAN",
                subtitle="Option A rebuilt round a black hole: the city's power, its ring road and its first lesson.",
                spawn=spawn, bounds=(-2950, -3700, 2950, 2700), wall=R_WALL, seed=14,
                # gameplay-zoom checks: a Siphon nozzle, the Current by Harbour Control, an Exchange street (zoomed out
                # and at the normal zoom), the gate from inside
                play_shots=[("siphon", polar(22.5, 600), 450), ("forge", polar(12, 1800), 650), ("salvage", polar(86, 1900), 700),
                            ("exchange", polar(141, 1760), 650), ("street_near", polar(141, 1640), 220), ("pit", pit, 800),
                            ("proving", polar(297, 1880), 800), ("gate", (0, -2350), 700)],
                concept=("Meridian was built on a black hole for its power: eight Siphons, 450 m arms, reach in to the disk's "
                         "edge, pull plasma off it with magnetic nozzles and drive it into converters that feed every quarter.  The hole's pull is the city's first lesson: you feel it from "
                         "the outer markers, the inbound lane bends into the Current, the 1300 m orbit that is the city's ring "
                         "road (cut your engines and you circle for free), and red buoys mark the Horizon Line past which a "
                         "starter ship cannot climb out. The Breakers feed scrap down the Drain; every wreck makes the disk flare, "
                         "and the converters' lee is the shelter. Nothing here stands on a floor: rings spin for weight, drums "
                         "point at the hole, radiators stand edge-on to it, and the city carries on in lower orbits beneath you."),
                strengths=["One idea explains the whole city: power, traffic, hazards and layout all come from the hole",
                           "Teaches the gravity well by living with it: the pull, orbits, the Horizon Line, slingshots",
                           "Megastructures: each quarter is a few huge connected slabs threaded by flight streets"],
                costs=["Gravity everywhere: AI traffic must fly against it (it already uses real thrusters)",
                       "A black hole needs a new look: lensing, the disk and flares are new VFX"],
                callouts=callouts, **C.data())
