#!/usr/bin/env python3
"""DEV-023: layout options for the capital hub, a vertical slice of a finished, bustling space city.

Owner (2026-10-18): "you're thinking too much that this is a minimal testing ground, but i was actually asking for a
vertical slice spacehub to showcase what a finished bustling capital hub would look like. you should feel like one small
player in a big economic bustling city in space." and "i want to spawn outside the hub and the first impression as one
enters this space hub should be grand, a city with traffic and multiple buildings, sections etc."

Each option is a true-scale map (metres, +x east, +y north on the flight plane) of one city: its districts, buildings
(footprint, height above the plane, depth below it), traffic lanes, berths and a snapshot of its traffic. The same data
goes to hub_options.json, which hub_arrival.py (Blender) reads to render the arrival view.

Run:  python3 ArtSource/Hub/hub_layouts.py
Out:  docs/design/hub/img/hub_option_<x>.png, docs/design/hub/hub_options.json
"""
import json
import math
import os
import random
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "Racing"))
from racing_common import *  # noqa: F401,F403  (colours, View, starfield, fonts, hazard primitives)
from PIL import Image, ImageDraw, ImageFilter

OUT_IMG = os.path.join(ROOT, "docs", "design", "hub", "img")
OUT_JSON = os.path.join(ROOT, "docs", "design", "hub", "hub_options.json")

# Faction liveries (DESIGN-SYSTEM §3 faction table): hull, trim, light
LIVERY = {
    "tlax": ((31, 122, 77), (230, 224, 207), (91, 227, 161)),     # Tlaxcala / Triple Alliance: the city's owners
    "vest": ((228, 233, 238), (47, 127, 208), (159, 216, 255)),   # Vestari
    "khu": ((168, 71, 30), (43, 37, 34), (255, 122, 47)),         # Khuchad Clan
    "scav": ((91, 94, 90), (142, 43, 37), (255, 179, 71)),        # Scavengers
    "ark": ((207, 200, 184), (59, 62, 66), (242, 183, 5)),        # ARK expedition
    "noct": ((44, 49, 57), (107, 115, 128), (255, 70, 60)),       # NOCTURNE warships (the player line)
    "civ": ((166, 166, 160), (59, 62, 66), (255, 226, 176)),      # civilian, unaligned
}
# Traffic classes: length, beam (m). The civilian line is designed next (silhouettes first); the warships exist.
CLASSES = {
    "shuttle": (14, 6), "courier": (22, 8), "wraith": (28, 12), "tug": (32, 14), "cruiser": (52, 24),
    "hauler": (64, 20), "freighter": (140, 34), "barge": (190, 52), "liner": (240, 46),
}
DISTRICT_COL = {
    "harbour": (255, 226, 176), "exchange": (91, 227, 161), "yards": (242, 183, 5), "range": (255, 85, 48),
    "pit": (255, 122, 47), "institute": (159, 216, 255), "breakers": (255, 179, 71), "foundry": (255, 106, 42),
    "circuit": (124, 255, 154), "old_town": (190, 186, 172), "gate": (255, 241, 214), "relic": (255, 200, 107),
    # option E, the Hollow
    "maw": (255, 85, 48), "bazaar": (255, 46, 136), "chop": (34, 230, 255), "fence": (255, 176, 0), "drop": (255, 122, 47),
    "furnace": (255, 138, 42), "den": (255, 85, 48), "outer": (190, 186, 172), "run": (157, 255, 60),
}


# ---------------------------------------------------------------- geometry (bearing: degrees clockwise from north)
def polar(b, r, c=(0.0, 0.0)):
    a = math.radians(b)
    return (c[0] + r * math.sin(a), c[1] + r * math.cos(a))


def bearing_of(p, c=(0.0, 0.0)):
    return math.degrees(math.atan2(p[0] - c[0], p[1] - c[1])) % 360


def box(c, heading, length, width):
    """A rectangle centred on c, long axis along the bearing `heading`."""
    f, r = polar(heading, length / 2), polar(heading + 90, width / 2)
    return [(c[0] + f[0] + r[0], c[1] + f[1] + r[1]), (c[0] + f[0] - r[0], c[1] + f[1] - r[1]),
            (c[0] - f[0] - r[0], c[1] - f[1] - r[1]), (c[0] - f[0] + r[0], c[1] - f[1] + r[1])]


def annulus(r0, r1, b0, b1, c=(0.0, 0.0), step=3.0):
    n = max(2, int(abs(b1 - b0) / step) + 1)
    outer = [polar(b0 + (b1 - b0) * i / (n - 1), r1, c) for i in range(n)]
    inner = [polar(b1 - (b1 - b0) * i / (n - 1), r0, c) for i in range(n)]
    return outer + inner


def ngon(c, r, n, rot0=0.0):
    return [polar(rot0 + k * 360 / n, r, c) for k in range(n)]


def arc(r, b0, b1, c=(0.0, 0.0), step=2.0):
    n = max(2, int(abs(b1 - b0) / step) + 1)
    return [polar(b0 + (b1 - b0) * i / (n - 1), r, c) for i in range(n)]


def lerp(a, b, t):
    return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)


def poly_len(pts):
    return sum(math.dist(pts[i], pts[i + 1]) for i in range(len(pts) - 1))


def point_on(pts, f):
    """Point and bearing at fraction f along a polyline."""
    L, acc = poly_len(pts) * f, 0.0
    for i in range(len(pts) - 1):
        d = math.dist(pts[i], pts[i + 1])
        if acc + d >= L:
            p = lerp(pts[i], pts[i + 1], (L - acc) / max(d, 1e-9))
            return p, bearing_of(pts[i + 1], pts[i])
        acc += d
    return pts[-1], bearing_of(pts[-1], pts[-2])


# ---------------------------------------------------------------- a city under construction
class City:
    def __init__(self, key, seed):
        self.key, self.rng = key, random.Random(seed)
        self.buildings, self.lanes, self.berths, self.ships, self.fx = [], [], [], [], []
        self.labels, self.callouts, self.districts, self.holos = [], [], [], []

    def b(self, pts, h, d, kind="hab", district=None, lit=0.5, roof=None):
        """A building: footprint, top above the plane (m), bottom below it (m). Anything crossing 0 is a wall you fly
        around; the rest is skyline above or the city's lower levels below."""
        self.buildings.append(dict(pts=[(round(x, 1), round(y, 1)) for x, y in pts], h=h, d=d, kind=kind,
                                   district=district, lit=lit, roof=roof))

    def shape(self, shape, c, district=None, kind=None, lit=0.6, z=0.0, **p):
        """A space form (DEV-023 option D; owner: "form follows function and also different shapes and sizes that make
        sense in SPACE"), centred on its own level rather than standing on a floor:
        torus (R, r, spokes): a rotating habitat ring lying in the plane; drum (heading, length, radius): a habitat
        cylinder, axis along heading; sphere (r): a tank or pod; panel (heading, length, thick, height): a collector
        vane or radiator fin; truss (heading, length, width): an open lattice beam."""
        if shape == "torus" and p.get("upright"):      # standing across the plane: a bar in plan, axis along heading
            pts = box(c, p.get("heading", 0.0), 2 * p["r"], 2 * (p["R"] + p["r"]))
        elif shape == "torus":
            pts = ngon(c, p["R"] + p["r"], 24)
        elif shape == "drum":
            pts = box(c, p["heading"], p["length"], 2 * p["radius"])
        elif shape == "sphere":
            pts = ngon(c, p["r"], 16)
        elif shape == "petal":
            pts = annulus(p["r0"], p["r1"], p["bearing"] - p["width"] / 2, p["bearing"] + p["width"] / 2)
        elif shape in ("panel", "truss"):
            pts = box(c, p["heading"], p["length"], p.get("thick", p.get("width", 4)))
        else:
            raise ValueError(shape)
        half = {"torus": (p["R"] + p["r"]) if p.get("upright") else p.get("r", 10), "drum": p.get("radius", 20), "sphere": p.get("r", 10),
                "panel": p.get("height", 20) / 2, "truss": p.get("width", 6) / 2,
                "petal": abs(p.get("z1", 0)) if shape == "petal" else 0}[shape]
        self.buildings.append(dict(pts=[(round(x, 1), round(y, 1)) for x, y in pts], h=round(z + half, 1), d=round(z - half, 1),
                                   kind=kind or shape, district=district, lit=lit, roof=None, shape=shape, c=(round(c[0], 1), round(c[1], 1)),
                                   z=round(z, 1), p={k: (round(v, 2) if isinstance(v, float) else v) for k, v in p.items()}))

    def lane(self, pts, kind, closed=False, oneway=True):
        self.lanes.append(dict(pts=[(round(x, 1), round(y, 1)) for x, y in pts], kind=kind, closed=closed, oneway=oneway))

    def ship(self, c, heading, cls, livery="civ", state="cruise"):
        self.ships.append(dict(c=(round(c[0], 1), round(c[1], 1)), heading=round(heading % 360, 1), cls=cls,
                               livery=livery, state=state))

    def berth(self, c, heading, length, owner, ship=None, livery="civ"):
        self.berths.append(dict(c=(round(c[0], 1), round(c[1], 1)), heading=round(heading % 360, 1), length=length, owner=owner))
        if ship:
            self.ship(c, heading, ship, livery, "docked")

    def traffic_on(self, lane_pts, n, mix, seed, offset=0.0):
        rng = random.Random(seed)
        for i in range(n):
            f = (offset + (i + rng.uniform(0.2, 0.8)) / n) % 1.0
            p, hd = point_on(lane_pts, f)
            cls, liv = rng.choice(mix)
            self.ship(p, hd, cls, liv)

    def tower_cluster(self, c, r, n, hmin, hmax, district, kind="hab", fmin=30, fmax=80, avoid=(), seed=0, deep=(-200, -700)):
        rng = random.Random(seed)
        placed = []
        tries = 0
        while len(placed) < n and tries < n * 40:
            tries += 1
            a, rr = rng.uniform(0, 360), r * math.sqrt(rng.random())
            p = polar(a, rr, c)
            s = rng.uniform(fmin, fmax)
            if any(math.dist(p, q) < (s + t) * 0.62 for q, t in placed):
                continue
            if any(avoid_fn(p, s) for avoid_fn in avoid):
                continue
            placed.append((p, s))
            sides = rng.choice([4, 4, 4, 6, 8])
            pts = ngon(p, s / 2, sides, rng.uniform(0, 90)) if sides != 4 else box(p, rng.choice([0, 45, bearing_of(p, c)]), s, s * rng.uniform(0.6, 1.0))
            self.b(pts, rng.uniform(hmin, hmax), rng.uniform(*deep), kind, district, lit=rng.uniform(0.35, 0.9))
        return placed

    def district(self, key, name, centre, purpose, contents, marquee=None):
        self.districts.append(dict(key=key, name=name, centre=centre, purpose=purpose, contents=contents))
        if marquee:
            self.holos.append(dict(text=marquee, at=marquee_at(centre), district=key))

    def data(self):
        return dict(buildings=self.buildings, lanes=self.lanes, berths=self.berths, ships=self.ships, fx=self.fx,
                    districts=self.districts, holos=self.holos)


def marquee_at(c):
    return (round(c[0], 1), round(c[1], 1))


def lane_clear(lanes_pts, width):
    def fn(p, s):
        return any(dist_to_poly(p, pts) < width + s * 0.6 for pts in lanes_pts)
    return fn


# ---------------------------------------------------------------- shared set pieces
def drydock(city, c, heading, length, width, ship_cls, livery, district):
    """An open gantry frame around a berth: two side walls, cross-gantries, the ship inside."""
    side = polar(heading + 90, width / 2 + 8)
    for s in (1, -1):
        cc = (c[0] + side[0] * s, c[1] + side[1] * s)
        city.b(box(cc, heading, length, 12), 70, -90, "gantry", district, lit=0.8)
    for k in range(4):
        f = -0.4 + k * 0.8 / 3
        cc = (c[0] + polar(heading, length * f)[0], c[1] + polar(heading, length * f)[1])
        city.b(box(cc, heading + 90, width + 30, 6), 90, 70, "gantry_top", district, lit=0.9)
    city.berth(c, heading, length, district, ship_cls, livery)


def merchant_house(city, c, heading, name, livery, district, ship_cls="hauler"):
    """A trading house: storefront block facing its berth, a docked trader alongside, cargo stacks behind."""
    city.b(box(c, heading, 120, 70), 90, -260, "market", district, lit=0.95, roof=livery)
    front = polar(heading + 90, 35 + CLASSES[ship_cls][1] / 2 + 14)
    bc = (c[0] + front[0], c[1] + front[1])
    city.berth(bc, heading, CLASSES[ship_cls][0] + 16, name, ship_cls, livery)
    back = polar(heading - 90, 62)
    for k in range(4):   # container stacks
        o = polar(heading, -45 + k * 30)
        cc = (c[0] + back[0] + o[0], c[1] + back[1] + o[1])
        city.b(box(cc, heading, 22, 14), 8 + 6 * (k % 2), -20, "cargo", district, lit=0.2, roof=livery)
    city.holos.append(dict(text=name, at=(round(c[0] - front[0] * 0.2, 1), round(c[1] - front[1] * 0.2, 1)), district=district, small=True))


def exhibit_row(city, start, heading, names, district, spacing=95):
    for i, n in enumerate(names):
        p = (start[0] + polar(heading, i * spacing)[0], start[1] + polar(heading, i * spacing)[1])
        city.b(ngon(p, 26, 8, 22.5), 22, -60, "plinth", district, lit=1.0)
        city.fx.append(dict(type="exhibit", c=p, name=n))


# ---------------------------------------------------------------- the city fabric
# A city is dense: every block between its streets is built up, and open space exists only where something happens
# (a harbour basin, a firing range, an arena bowl). The fabric fills what the set pieces and streets leave free.
LANE_HALF = {"approach_in": 70, "approach_out": 70, "harbour": 90, "boulevard": 80, "avenue_in": 45, "avenue_out": 45,
             "district": 70, "launch": 110, "gate_in": 120, "gate_out": 120, "street": 30,
             "current": 110, "drain": 60, "slingshot": 50}
# style: footprint (min, max) m, height (min, max) m above the plane, depth (min, max) below it, kind, chance of a podium
STYLES = {
    "tower": ((34, 80), (220, 640), (-400, -1100), "hab", 0.55),
    "midrise": ((40, 110), (70, 240), (-150, -500), "hab", 0.3),
    "commerce": ((50, 130), (50, 180), (-120, -400), "market", 0.25),
    "warehouse": ((60, 150), (24, 70), (-60, -200), "warehouse", 0.0),
    "industrial": ((50, 170), (30, 110), (-80, -300), "industrial", 0.1),
    "military": ((50, 140), (30, 90), (-80, -260), "hangar", 0.0),
    "temple": ((30, 70), (90, 300), (-150, -500), "sanctum", 0.4),
}


class Fabric:
    """A buildable-ground mask (8 m pixels): 255 where a building may stand."""
    def __init__(self, bounds, res=8.0):
        self.b, self.res = bounds, res
        self.w, self.h = int((bounds[2] - bounds[0]) / res) + 1, int((bounds[3] - bounds[1]) / res) + 1
        self.img = Image.new("L", (self.w, self.h), 0)
        self.d = ImageDraw.Draw(self.img)
        self.px_ = self.img.load()

    def px(self, p):
        return ((p[0] - self.b[0]) / self.res, (self.b[3] - p[1]) / self.res)

    def region(self, pts):
        self.d.polygon([self.px(p) for p in pts], fill=255)

    def cut(self, pts):
        self.d.polygon([self.px(p) for p in pts], fill=0)

    def cut_line(self, pts, half):
        w = max(1, int(2 * half / self.res))
        self.d.line([self.px(p) for p in pts], fill=0, width=w, joint="curve")
        for p in (pts[0], pts[-1]):
            self.cut_circle(p, half)

    def cut_circle(self, c, r):
        x, y = self.px(c); rr = r / self.res
        self.d.ellipse((x - rr, y - rr, x + rr, y + rr), fill=0)

    def free(self, p):
        x, y = self.px(p)
        if not (0 <= x < self.w and 0 <= y < self.h):
            return False
        return self.px_[int(x), int(y)] == 255

    def fits(self, pts):
        c = (sum(p[0] for p in pts) / len(pts), sum(p[1] for p in pts) / len(pts))
        probes = [c] + list(pts) + [lerp(pts[i], pts[(i + 1) % len(pts)], 0.5) for i in range(len(pts))] + \
                 [lerp(c, p, 0.5) for p in pts]
        return all(self.free(p) for p in probes)

    def occupy(self, pts, gap):
        c = (sum(p[0] for p in pts) / len(pts), sum(p[1] for p in pts) / len(pts))
        grown = []
        for p in pts:
            dx, dy = p[0] - c[0], p[1] - c[1]; L = math.hypot(dx, dy) or 1
            grown.append((p[0] + dx / L * gap * 1.42, p[1] + dy / L * gap * 1.42))
        self.cut(grown)


def fill_fabric(city, fab, style_at, align_at, tries=60000, seed=1, gap=(14, 30)):
    """Packs the free ground with buildings, largest first. style_at(p) -> (district, style, height scale) or None;
    align_at(p) -> the bearing a building at p lines up with (its street)."""
    rng = random.Random(seed)
    for b in city.buildings:                      # set pieces and their surroundings stay clear
        fab.occupy(b["pts"], 18)
    for ln in city.lanes:
        fab.cut_line(ln["pts"] + ([ln["pts"][0]] if ln["closed"] else []), LANE_HALF.get(ln["kind"], 40))
    x0, y0, x1, y1 = fab.b
    for size_pass in (1.0, 0.7, 0.45, 0.3):
        for _ in range(int(tries * (0.4 if size_pass == 1.0 else 0.25))):
            p = (rng.uniform(x0, x1), rng.uniform(y0, y1))
            if not fab.free(p):
                continue
            st = style_at(p)
            if not st:
                continue
            district, style, hk = st
            (fmin, fmax), (hmin, hmax), (dmin, dmax), kind, podium = STYLES[style]
            L = rng.uniform(fmin, fmax) * (0.5 + size_pass * 0.5)
            Wd = L * rng.uniform(0.45, 1.0)
            hd = align_at(p) + rng.choice([0, 0, 0, 90])
            pts = box(p, hd, L, Wd)
            if not fab.fits(pts):
                continue
            h = rng.uniform(hmin, hmax) * hk * (0.6 + 0.4 * size_pass)
            if rng.random() < podium and L > 50:
                city.b(pts, h * 0.22, rng.uniform(dmin, dmax) * 0.5, kind, district, lit=rng.uniform(0.3, 0.7))
                top = box(p, hd + rng.choice([0, 45]), L * 0.55, Wd * 0.6)
                city.b(top, h, rng.uniform(dmin, dmax), kind, district, lit=rng.uniform(0.5, 1.0))
            else:
                city.b(pts, h, rng.uniform(dmin, dmax), kind, district, lit=rng.uniform(0.3, 1.0))
            fab.occupy(pts, rng.uniform(*gap))


def sector_of(p, sectors):
    """The district whose bearing is nearest p's (sectors: {key: bearing})."""
    b = bearing_of(p)
    return min(sectors, key=lambda k: abs((b - sectors[k] + 180) % 360 - 180))


# ---------------------------------------------------------------- option A: Harbour City
def option_a():
    C = City("A", 11)
    R_WALL, R_BLVD, R_OLD0, R_OLD1, R_D0, R_D1 = 2300, 1170, 880, 1060, 1290, 2180
    AVENUES = [0, 45, 90, 135, 180, 225, 270, 315]

    # Port Authority: the spire, its docking ring, sixteen piers and the harbour roads around them
    C.b(ngon((0, 0), 120, 8, 22.5), 420, -900, "spire", "harbour", lit=1.0)
    C.b(ngon((0, 0), 70, 8, 22.5), 620, -1100, "spire_top", "harbour", lit=1.0)
    for b in (45, 135, 225, 315):
        C.b(box(polar(b, 220), b, 210, 34), 30, -60, "spoke", "harbour", lit=0.6)
    for b0 in range(0, 360, 90):
        C.b(annulus(310, 362, b0 + 6, b0 + 84), 46, -140, "ring", "harbour", lit=0.85)
    piers = [b for b in [i * 22.5 for i in range(16)] if b not in (180.0,)]
    for b in piers:
        C.b(box(polar(b, 430), b, 136, 16), 18, -40, "pier", "harbour", lit=0.7)
    rng = random.Random(4)
    for i, b in enumerate(piers):
        cls = rng.choice(["freighter", "freighter", "hauler", "liner", "barge", "hauler"])
        L = CLASSES[cls][0]
        bc = polar(b + 11.25, 362 + 12 + L / 2)
        C.berth(bc, b + 11.25, L + 10, f"pier {i + 1}", cls if rng.random() < 0.8 else None,
                rng.choice(["tlax", "tlax", "civ", "vest", "khu"]))
    harbour_roads = arc(640, 0, -360)                                          # counter-clockwise circulation
    C.lane(harbour_roads, "harbour", closed=True)
    C.traffic_on(harbour_roads, 9, [("hauler", "tlax"), ("shuttle", "civ"), ("courier", "vest"), ("freighter", "tlax"),
                                    ("tug", "scav"), ("shuttle", "ark")], 7)

    # Old Town: habitation spires between the avenues, the skyline you see on the way in
    for k, b in enumerate(AVENUES):
        mid = b + 22.5
        C.tower_cluster(polar(mid, (R_OLD0 + R_OLD1) / 2), 150, 9, 140, 520, "old_town", seed=30 + k, fmin=36, fmax=90)
    blvd = arc(R_BLVD, 0, 360)
    C.lane(blvd, "boulevard", closed=True)
    C.traffic_on(blvd, 16, [("shuttle", "civ"), ("shuttle", "tlax"), ("courier", "civ"), ("hauler", "tlax"),
                            ("wraith", "noct"), ("tug", "scav"), ("courier", "vest")], 8)
    for b in AVENUES:   # avenues: two-way traffic between the harbour roads, the boulevard and each district
        for s, kind in ((1, "avenue_out"), (-1, "avenue_in")):
            off = polar(b + 90, 40 * s)
            a0, a1 = polar(b, 700), polar(b, R_D0 + 60)
            pts = [(a0[0] + off[0], a0[1] + off[1]), (a1[0] + off[0], a1[1] + off[1])]
            C.lane(pts if s > 0 else pts[::-1], kind)

    # The wall: bastions with gauntlet turrets, three gates (the Harbour Mouth, the Yards launch, the Circuit)
    gates = {180: 9, 0: 5, 247: 4}   # bearing: half-width (degrees)
    cuts =sorted([((g - w) % 360, (g + w) % 360) for g, w in gates.items()], key=lambda t: t[0])
    starts = [hi for _, hi in cuts]
    ends = [lo for lo, _ in cuts[1:]] + [cuts[0][0]]
    for s0, s1 in zip(starts, ends):
        C.b(annulus(R_WALL, R_WALL + 46, s0, s1 if s1 > s0 else s1 + 360), 60, -180, "wall", "gate", lit=0.4)
    for bb in range(15, 360, 30):
        if min(abs((bb - g + 180) % 360 - 180) for g in gates) < 14:
            continue
        p = polar(bb, R_WALL + 23)
        C.b(ngon(p, 34, 6), 90, -200, "bastion", "gate", lit=0.6)
        C.fx.append(dict(type="turret", c=polar(bb, R_WALL + 70), facing=bb, reach=520, arc=110))
    # Harbour Mouth: gatehouse towers, customs arch, traffic signals, the city's name
    for s in (1, -1):
        C.b(box((150 * s, -R_WALL - 10), 0, 120, 70), 230, -500, "gatehouse", "gate", lit=1.0)
    C.fx.append(dict(type="scanner", a=(-110, -R_WALL - 60), b=(110, -R_WALL - 60)))
    C.fx.append(dict(type="signal", c=(70, -R_WALL - 150), state="green"))
    C.fx.append(dict(type="signal", c=(-70, -R_WALL - 150), state="green"))
    C.holos.append(dict(text="MERIDIAN", at=(0, -R_WALL + 40), district="gate", grand=True))

    # Approach: the inbound and outbound lanes from deep space, outer markers every 400 m
    inbound = [(70, -4400), (70, -R_WALL), (70, -700)]
    outbound = [(-70, -700), (-70, -R_WALL), (-70, -4400)]
    C.lane(inbound, "approach_in"); C.lane(outbound, "approach_out")
    for y in range(-4300, -R_WALL - 100, 400):
        C.fx.append(dict(type="marker", c=(0, y)))
    C.traffic_on(inbound, 6, [("liner", "tlax"), ("freighter", "tlax"), ("hauler", "khu"), ("shuttle", "civ"),
                              ("barge", "civ"), ("courier", "vest")], 21)
    C.traffic_on(outbound, 6, [("freighter", "tlax"), ("hauler", "civ"), ("shuttle", "ark"), ("courier", "civ"),
                               ("liner", "civ"), ("tug", "scav")], 22, 0.5)
    spawn = dict(c=(150, -3500), heading=0)
    C.fx.append(dict(type="patrol", c=(-420, -3000), heading=20))

    # ---- districts (wedge quarters between the boulevard and the wall)
    def wedge(b, half=19):
        return annulus(R_D0, R_D1, b - half, b + half)

    # Exchange (135°): four trading houses on the Exchange Canal, the busiest quarter, first one you pass
    bE = 135
    canal = [polar(bE, R_D0), polar(bE, R_D1 - 80)]
    C.lane(canal, "district")
    for i, (name, liv, cls) in enumerate([("TLAXCALA", "tlax", "freighter"), ("KHUCHAD", "khu", "hauler"),
                                          ("VESTARI", "vest", "courier"), ("SWAP", "scav", "tug")]):
        r = R_D0 + 180 + i * 210
        s = 1 if i % 2 == 0 else -1
        c = polar(bE + s * 9.5 * (1550 / r), r)
        merchant_house(C, c, bE, name, liv, "exchange", cls)
    C.tower_cluster(polar(bE, 1750), 420, 22, 40, 180, "exchange", "warehouse", 30, 70, avoid=[lane_clear([canal], 120)], seed=51,
                    deep=(-80, -300))
    C.traffic_on(canal, 6, [("hauler", "tlax"), ("shuttle", "civ"), ("tug", "scav"), ("courier", "vest")], 52)
    C.district("exchange", "THE EXCHANGE", polar(bE, 1720),
               "The market quarter. Four trading houses on the Exchange Canal, each with its own trader docked at its berth "
               "and its own stock.",
               ["TLAXCALA house: bulk goods, fuel, ore; buys everything", "KHUCHAD house: weapon blocks",
                "VESTARI house: shields, reactors, high-tech blocks (charges foreigners more)",
                "SWAP: used and salvaged blocks, cheap, under the plane", "Haulers load and unload at every berth"], "EXCHANGE")

    # Breakers' Yard (90°): shipbreakers cut hulks on slipways, tugs haul wrecks in through the east
    bB = 90
    for k, (off, L, W) in enumerate([(-11, 420, 90), (0, 300, 70), (11, 360, 80)]):
        c = polar(bB + off, 1760)
        C.b(box(c, bB, L + 60, 14), 40, -60, "slipway", "breakers", lit=0.5)
        C.fx.append(dict(type="hulk", c=c, heading=bB + (k - 1) * 4, length=L, width=W))
        C.fx.append(dict(type="rig", c=polar(bB + off, 1760 - L / 2 + 40), facing=bB))
    C.fx.append(dict(type="debris", c=polar(bB, 1450), r=180, n=40))
    C.ship(polar(bB - 6, 1500), 270, "tug", "scav", "towing"); C.ship(polar(bB + 5, 1420), 90, "tug", "scav")
    C.district("breakers", "BREAKERS' YARD", polar(bB, 1700),
               "Shipbreakers. Three hulks on slipways being cut apart by rigs; the debris drifts free for anyone with a magnet.",
               ["Salvage loose blocks and sell them at the SWAP", "Cutting rigs sweep the slipways (moving arms)",
                "Tugs tow a fresh wreck in every few minutes"], "BREAKERS")

    # Foundry (45°): the power plant and the refinery; outside the wall, the quarry moon and the old minefield
    bF = 45
    reactor = polar(bF, 1700)
    C.fx.append(dict(type="reactor", c=reactor, r=260))
    for k in range(6):
        C.b(box(polar(bF - 20 + k * 8, 1700 - 170 if k % 2 else 1700 + 170), bF + 90, 70, 18), 50, -120, "bulkhead", "foundry", lit=0.3)
    C.tower_cluster(polar(bF - 10, 1420), 160, 7, 60, 200, "foundry", "tank", 30, 60, seed=61, deep=(-100, -400))
    C.tower_cluster(polar(bF + 12, 2020), 120, 5, 120, 300, "foundry", "stack", 22, 40, seed=62, deep=(-100, -400))
    moon = polar(52, 3150)
    C.fx.append(dict(type="moon", c=moon, r=260, infl=820))
    C.fx.append(dict(type="slow_field", c=polar(50, 2620), r=200))
    C.fx.append(dict(type="minefield", c=polar(28, 2700), r=260, n=26, kind="seeker"))
    C.fx.append(dict(type="minefield", c=polar(18, 2560), r=160, n=14, kind="prox"))
    C.lane([polar(bF, R_D0), polar(bF, 1500), polar(50, R_WALL - 40)], "district")
    C.district("foundry", "THE FOUNDRY", polar(bF, 1650),
               "Power and industry. The city's reactor pulses behind blast bulkheads; ore barges come in from the quarry moon.",
               ["Reactor pulse: hide in the bulkheads' shadows", "Quarry moon's gravity well and the refinery's slow field",
                "The old defence minefield outside the wall: proximity and seeker mines"], "FOUNDRY")

    # Meridian Yards (0°): drydocks with every new ship, the Blueprint Lab, the engine test stand, the launch gate
    bY = 0
    for k, (cls, liv) in enumerate([("wraith", "noct"), ("cruiser", "noct"), ("cruiser", "noct"), ("liner", "noct"),
                                    ("courier", "noct"), ("shuttle", "noct")]):
        off = -14 + k * 5.6
        r = 1760 if k % 2 == 0 else 1580
        L = {"liner": 150, "cruiser": 80}.get(cls, 50)
        drydock(C, polar(bY + off, r), bY, L, 30 if cls != "liner" else 46, cls, liv, "yards")
    C.b(box(polar(bY + 15, 1450), bY, 140, 90), 120, -300, "office", "yards", lit=1.0)
    C.fx.append(dict(type="lab", c=polar(bY + 15, 1450)))
    C.fx.append(dict(type="engine_stand", c=polar(bY + 15, 2050), facing=bY + 180, length=420))
    launch = [polar(bY, R_D0), polar(bY, R_WALL + 400), polar(bY, 3200)]
    C.lane(launch, "launch")
    C.district("yards", "MERIDIAN YARDS", polar(bY, 1700),
               "The shipyard. Every ship in the game sits in a drydock; fly into a dock to take it. The Blueprint Lab is in the "
               "yard office.",
               ["Drydocks: WRAITH, DIRGE, VESPER, REQUIEM, the racers, the xeno hulls (quarantine dock)",
                "Blueprint Lab: edit your ship", "Engine test stand: a cruiser engine fires across the yard on a cycle (engine wash)",
                "New ships launch through the north gate"], "YARDS")

    # Proving Range (315°): the test line of armed ships, the firing lanes, targets out to the wall
    bR = 315
    for k in range(6):
        C.b(box(polar(bR - 15 + k * 3.2, 1400), bR, 70, 26), 30, -80, "hangar", "range", lit=0.8)
        C.ship(polar(bR - 15 + k * 3.2, 1400), bR, ["wraith", "cruiser", "cruiser", "wraith", "courier", "cruiser"][k], "noct", "docked")
    for k, d in enumerate([200, 400, 600, 800]):
        C.fx.append(dict(type="range_mark", c=polar(bR, 1440 + d), facing=bR))
    for k in range(5):
        C.fx.append(dict(type="target", c=polar(bR - 12 + k * 6, 1700 + (k % 3) * 160)))
    C.fx.append(dict(type="target_station", c=polar(bR, 2080)))
    for s in (-1, 1):
        a, b = polar(bR + s * 17, 1440), polar(bR + s * 17, 2150)
        C.fx.append(dict(type="field_wall", a=a, b=b))
    C.fx.append(dict(type="turret_alley", a=polar(bR + 24, 1400), b=polar(bR + 24, 2100)))
    C.b(box(polar(bR - 20, 1330), bR + 90, 140, 20), 70, -40, "scoreboard", "range", lit=1.0)
    C.district("range", "PROVING RANGE", polar(bR, 1700),
               "Weapons testing. Take any ship from the test line, each fitted with a different weapon kit, and fire on targets "
               "out to 800 m.",
               ["Test line: every ship in every weapon fit", "Targets: static hulks, moving rails, drones, a breakable station",
                "Field walls bound the lanes; shoot their emitters to drop them", "Turret Alley: run the gauntlet turrets",
                "Scoreboard wall: hits, damage, DPS"], "RANGE")

    # The Pit (270°): the Khuchad arena, spectator decks round a 600 m bowl, four bout gates
    bP = 270
    pit = polar(bP, 1720)
    for b0 in range(0, 360, 45):
        C.b(annulus(330, 380, b0 + 5, b0 + 40, pit), 70, -160, "stands", "pit", lit=0.95)
    C.fx.append(dict(type="arena", c=pit, r=320))
    for b0 in (0, 90, 180, 270):
        C.fx.append(dict(type="bout_gate", c=polar(b0 + 22.5, 355, pit)))
    C.district("pit", "THE PIT", pit,
               "The arena. Pick a bout at a gate and fight it in front of the crowd.",
               ["Bouts: raider duel, raider wing, xeno swarm, cruiser bout", "The bowl's edge is a field wall",
                "The crowd's lights flash on every kill"], "THE PIT")

    # Institute (225°): the gallery of working blocks and mechanisms
    bI = 225
    C.b(annulus(1380, 1440, bI - 17, bI + 17), 60, -200, "gallery", "institute", lit=0.9)
    C.b(annulus(1960, 2020, bI - 17, bI + 17), 60, -200, "gallery", "institute", lit=0.9)
    names = ["MIRROR", "MAGNET", "PORTAL", "SHIELD", "REACTOR", "SWITCH+DOOR", "ARM", "REPULSOR", "EMP", "SEEKER",
             "GRAVITY", "CANISTER", "DRIVER", "RAILS"]
    for i, n in enumerate(names):
        r = 1560 if i < 7 else 1830
        b = bI - 15 + (i % 7) * 5
        C.b(ngon(polar(b, r), 26, 8, 22.5), 22, -60, "plinth", "institute", lit=1.0)
        C.fx.append(dict(type="exhibit", c=polar(b, r), name=n))
    C.lane(arc(1700, bI - 17, bI + 17), "district")
    C.district("institute", "THE INSTITUTE", polar(bI, 1700),
               "The engineering gallery. Every special block and mechanism, working, in its own bay along the flyway.",
               ["Prismatic mirror, salvage magnet, portal blocks, shield emitter, reactor", "Switch and blast door, sweeper arm, rails",
                "Repulsor, EMP, seeker, gravity and proximity mines; energy canisters; impact driver"], "INSTITUTE")

    # Circuit (247°, at the wall): the racing quarter, grandstand and pit lane, the start portal to Track 01
    bC = 247
    C.b(annulus(2080, 2140, bC - 9, bC + 9), 80, -160, "grandstand", "circuit", lit=1.0)
    for k in range(6):
        C.ship(polar(bC - 6 + k * 2.4, 2200), bC, "courier", ["vest", "khu", "tlax", "scav", "ark", "noct"][k], "docked")
    C.fx.append(dict(type="portal", c=polar(bC, R_WALL + 260), facing=bC, width=160))
    C.lane([polar(bC, R_BLVD), polar(bC, R_WALL + 260)], "district")
    C.district("circuit", "THE CIRCUIT", polar(bC, 2150),
               "The racing quarter. Racers line up in the pit lane; the start portal outside the wall opens onto Track 01.",
               ["Race the AI on Track 01 (Battle Graveyard)", "Leaderboard on the grandstand"], "CIRCUIT")

    # Harbour (centre) and the gate
    C.district("harbour", "THE HARBOUR", (0, 0),
               "Port Authority. The spire, the docking ring and sixteen piers where liners and freighters dock. The Harbour "
               "Master's console resets the whole city.",
               ["Liners, freighters and barges dock, dwell and leave all the time", "Harbour Master: reset the city",
                "Harbour Control hails you on the way in"], None)
    C.district("gate", "HARBOUR MOUTH", (0, -R_WALL),
               "The way in. Gatehouse towers, the customs scanner and traffic signals over the inbound and outbound lanes.",
               ["Customs scan sweeps you as you pass", "Signals hold traffic when the mouth is busy"], None)
    C.district("old_town", "OLD TOWN", polar(202, 970), "Habitation spires, the skyline. No gameplay; life.", [], None)

    # ---- the fabric: streets between the avenues, ring streets, and every block between them built up
    for b in [k * 11.25 for k in range(32)]:
        if b % 45 == 0:
            continue
        C.lane([polar(b, R_BLVD + 80), polar(b, R_WALL - 30)], "street", oneway=False)
    for r in (1450, 1990):
        C.lane(arc(r, 0, 360, step=1.5), "street", closed=True, oneway=False)
    fab = Fabric((-R_WALL - 60, -R_WALL - 60, R_WALL + 60, R_WALL + 60))
    fab.region(ngon((0, 0), R_WALL - 8, 120))
    fab.cut_circle((0, 0), 880)                                              # the harbour basin
    fab.cut(box((0, -1600), 0, 1500, 300))                                   # the Grand Avenue from the mouth
    fab.cut(annulus(R_D0 - 20, R_D1, bE - 7, bE + 7))                        # the Exchange Canal
    fab.cut(annulus(1450, 1960, bY - 17, bY + 17))                           # the drydock basin
    fab.cut(annulus(1340, R_WALL, bR - 19, bR + 19))                         # the range
    fab.cut_circle(pit, 430)                                                 # the arena
    fab.cut(annulus(1340, 2060, bI - 18, bI + 18))                           # the gallery
    fab.cut(annulus(1450, R_WALL, bB - 16, bB + 16))                         # the slipways
    fab.cut_circle(reactor, 330)                                             # the reactor court
    fab.cut(annulus(2000, R_WALL, bC - 11, bC + 11))                         # the grandstand
    sectors = dict(yards=0, foundry=45, breakers=90, exchange=135, gate=180, institute=225, pit=270, range=315)
    style_of = dict(yards="industrial", foundry="industrial", breakers="warehouse", exchange="commerce", gate="midrise",
                    institute="temple", pit="midrise", range="military")

    def style_at(p):
        r = math.hypot(*p)
        if r < R_BLVD:
            return ("old_town", "tower", 1.0 - (r - 880) / 900)
        k = sector_of(p, sectors)
        b = bearing_of(p)
        # between quarters, and round the harbour: the city lives here too (towers falling off toward the wall)
        edge = min(abs((b - sectors[s] + 180) % 360 - 180) for s in sectors)
        if edge > 15 and r < 1700:
            return (k, "tower", 0.75 - (r - R_BLVD) / 2400)
        if rng.random() < 0.25:
            return (k, "midrise", 0.8 - (r - R_BLVD) / 3000)
        return (k, style_of[k], 1.0)
    fill_fabric(C, fab, style_at, lambda p: bearing_of(p), tries=90000, seed=17)

    callouts = [
        ((150, -3500), "You spawn here, in the inbound stream, with the city ahead"),
        ((0, -R_WALL - 60), "Harbour Mouth: customs scan, signals, MERIDIAN overhead"),
        ((0, 0), "Port Authority spire; Harbour Master (the one reset)"),
        (polar(135, 1720), "The Exchange: four trading houses"),
        (polar(0, 1700), "Meridian Yards: every ship in a drydock"),
        (polar(315, 1700), "Proving Range: ships x weapons, targets to 800 m"),
        (polar(270, 1720), "The Pit: arena bouts"),
        (polar(225, 1700), "The Institute: working blocks and mechanisms"),
        (polar(90, 1700), "Breakers' Yard: salvage"),
        (polar(45, 1700), "The Foundry: reactor, moon, minefield"),
        (polar(247, 2300), "The Circuit: start portal to Track 01"),
    ]
    return dict(key="A", title="OPTION A  ·  HARBOUR CITY", name="MERIDIAN",
                subtitle="A walled free port of the Triple Alliance. Nine quarters round a central harbour; every lane is busy.",
                spawn=spawn, bounds=(-2950, -3700, 2950, 2700), wall=R_WALL, seed=11,
                concept=("Meridian is the Triple Alliance's free port, the richest city on the lanes. It is built like a harbour town: "
                         "a walled ring 4.6 km across, a docking spire at its heart, and nine quarters between the boulevard and "
                         "the wall, each run by the people who work there. You arrive from deep space in the inbound stream, behind "
                         "a liner, with the skyline filling the screen. You pass under the gatehouse towers and the customs scan, "
                         "and Harbour Control hails you. Everything you can do in the game happens somewhere in the city, for a reason."),
                strengths=["The most city-like: you are surrounded by buildings and traffic everywhere you fly",
                           "Every quarter is one flight from the harbour along its avenue: easy to learn, easy to return",
                           "Hazards sit where a city would have them: the reactor, the defences, the quarry"],
                costs=["Largest build: about 1.6 M cells", "The ring layout repeats; districts need strong silhouettes to tell apart"],
                callouts=callouts, **C.data())


# ---------------------------------------------------------------- option B: the Keel
def option_b():
    C = City("B", 12)
    Y0, Y1 = -2700, 2700
    # The keel: the colony ship that brought people here, 5.4 km, its ribs now the city's streets
    C.b(box((0, 0), 0, Y1 - Y0, 190), 50, -260, "keel", "harbour", lit=0.7)
    for k, y in enumerate(range(-2000, 2401, 400)):   # the old superstructure, now the city's towers, along the keel
        C.b(box((0, y), 0, 120, 110), 120, -300, "keel_castle", "old_town", lit=0.8)
        C.b(box((0, y + 10), 0 if k % 2 else 45, 60, 60), 260 + (k * 97) % 240, -500, "hab", "old_town", lit=1.0)
    ribs = list(range(-2200, 2201, 400))
    for i, y in enumerate(ribs):
        span = 760 if i % 2 == 0 else 560
        for s in (1, -1):
            C.b(box((s * (95 + span / 2), y), 90, span, 54), 90, -300, "rib", None, lit=0.6)
            C.b(box((s * (95 + span), y), 0, 60, 90), 200, -420, "rib_tip", None, lit=0.9)
    # Bow: the Harbour, a docking cradle round the old bow with liners alongside
    for s in (1, -1):
        for k in range(4):
            y = -2620 + k * 110
            C.berth((s * 210, y), 0, 150, f"bow {k + 1}", ["liner", "freighter", "barge", "freighter"][k], ["tlax", "civ", "tlax", "vest"][k])
        C.b(box((s * 120, -2450), 0, 500, 30), 40, -80, "pier", "harbour", lit=0.7)
    C.b(ngon((0, -2500), 90, 8, 22.5), 520, -900, "spire", "harbour", lit=1.0)
    C.holos.append(dict(text="THE KEEL", at=(0, -2780), district="gate", grand=True))
    # Flank lanes: northbound up the east side, southbound down the west; bay lanes into each bay
    east = [(980, -4400), (980, -2700), (980, 2700), (980, 3200)]
    west = [(-980, 3200), (-980, 2700), (-980, -2700), (-980, -4400)]
    C.lane(east, "approach_in"); C.lane(west, "approach_out")
    keel_e = [(150, -2300), (150, 2300)]; keel_w = [(-150, 2300), (-150, -2300)]
    C.lane(keel_e, "boulevard"); C.lane(keel_w, "boulevard")
    C.traffic_on(east, 12, [("liner", "tlax"), ("freighter", "tlax"), ("hauler", "khu"), ("shuttle", "civ"), ("barge", "civ"),
                            ("courier", "vest"), ("tug", "scav")], 31)
    C.traffic_on(west, 12, [("freighter", "tlax"), ("hauler", "civ"), ("shuttle", "ark"), ("courier", "civ"), ("liner", "civ"),
                            ("tug", "scav")], 32, 0.4)
    C.traffic_on(keel_e, 10, [("shuttle", "civ"), ("shuttle", "tlax"), ("courier", "civ"), ("wraith", "noct")], 33)
    C.traffic_on(keel_w, 10, [("shuttle", "civ"), ("courier", "vest"), ("hauler", "tlax"), ("tug", "scav")], 34, 0.5)
    for y in range(-4300, -2800, 400):
        C.fx.append(dict(type="marker", c=(980 - 120, y)))
    spawn = dict(c=(840, -3700), heading=0)

    def bay(y0, s):
        return ((s * 560, (y0 + y0 + 400) / 2))

    # Exchange: bays -2200..-1400, both sides
    for k, (name, liv, cls) in enumerate([("TLAXCALA", "tlax", "freighter"), ("KHUCHAD", "khu", "hauler"),
                                          ("VESTARI", "vest", "courier"), ("SWAP", "scav", "tug")]):
        s = 1 if k < 2 else -1
        y = -2000 + (k % 2) * 400
        merchant_house(C, (s * 470, y), 90 if s > 0 else 270, name, liv, "exchange", cls)
    C.district("exchange", "THE EXCHANGE", (0, -1800),
               "Four trading houses in the first bays behind the bow, two on each flank.",
               ["TLAXCALA, KHUCHAD, VESTARI, SWAP, each with its own stock and trader"], "EXCHANGE")
    # Old Town: bays -1400..-600: towers on the ribs
    for s in (1, -1):
        C.tower_cluster((s * 520, -1000), 330, 16, 160, 560, "old_town", seed=40 + s, fmin=40, fmax=90,
                        avoid=[lane_clear([[(s * 520, -1400), (s * 520, -1400)]], 0)])
    C.district("old_town", "OLD TOWN", (0, -1000), "Habitation towers on the ribs; the skyline.", [], None)
    # Yards (east) and Range (west): bays -600..200
    for k in range(4):
        drydock(C, (520, -500 + k * 180), 90, 70 if k else 160, 34, ["liner", "cruiser", "cruiser", "wraith"][k], "noct", "yards")
    C.b(box((300, 120), 0, 80, 80), 120, -300, "office", "yards", lit=1.0); C.fx.append(dict(type="lab", c=(300, 120)))
    C.district("yards", "KEEL YARDS", (560, -200), "Drydocks between the ribs; take any ship.", ["Blueprint Lab in the yard office"], "YARDS")
    for k in range(5):
        C.ship((-330, -500 + k * 150), 270, ["wraith", "cruiser", "cruiser", "wraith", "courier"][k], "noct", "docked")
    for d in (200, 400, 600, 800):
        C.fx.append(dict(type="range_mark", c=(-330 - d, -200), facing=270))
    for k in range(5):
        C.fx.append(dict(type="target", c=(-700 - k * 120, -420 + (k % 3) * 200)))
    C.fx.append(dict(type="target_station", c=(-1250, -200)))
    C.fx.append(dict(type="field_wall", a=(-400, -620), b=(-1300, -620)))
    C.fx.append(dict(type="field_wall", a=(-400, 220), b=(-1300, 220)))
    C.district("range", "PROVING RANGE", (-700, -200), "The test line on the west flank fires out into open space.",
               ["Ships x weapon kits", "Targets to 800 m", "Field walls"], "RANGE")
    # Pit (west) and Institute (east): 200..1000
    pit = (-560, 600)
    for b0 in range(0, 360, 45):
        C.b(annulus(250, 290, b0 + 5, b0 + 40, pit), 70, -160, "stands", "pit", lit=0.95)
    C.fx.append(dict(type="arena", c=pit, r=240))
    C.district("pit", "THE PIT", pit, "The arena, slung between two ribs.", ["Four bouts"], "THE PIT")
    for i, n in enumerate(["MIRROR", "MAGNET", "PORTAL", "SHIELD", "REACTOR", "SWITCH+DOOR", "ARM", "REPULSOR", "EMP",
                           "SEEKER", "GRAVITY", "CANISTER", "DRIVER", "RAILS"]):
        p = (300 + (i % 7) * 85, 330 + (i // 7) * 330)
        C.b(ngon(p, 24, 8, 22.5), 22, -60, "plinth", "institute", lit=1.0)
        C.fx.append(dict(type="exhibit", c=p, name=n))
    C.district("institute", "THE INSTITUTE", (560, 500), "The gallery of working blocks between the ribs.", ["Fourteen exhibits"], "INSTITUTE")
    # Foundry (east, 1000..1800): the colony ship's reactor, still running
    C.fx.append(dict(type="reactor", c=(420, 1400), r=300))
    for k in range(5):
        C.b(box((300 + k * 90, 1180 if k % 2 else 1620), 0, 20, 70), 50, -120, "bulkhead", "foundry", lit=0.3)
    C.district("foundry", "THE FOUNDRY", (520, 1400), "The keel's original reactor, still running; it pulses through the bays.",
               ["Reactor pulse and shadows"], "FOUNDRY")
    # Circuit (west, 1000..1800)
    C.b(box((-560, 1400), 0, 520, 50), 80, -160, "grandstand", "circuit", lit=1.0)
    C.fx.append(dict(type="portal", c=(-1300, 1400), facing=270, width=160))
    C.district("circuit", "THE CIRCUIT", (-700, 1400), "Pit lane and grandstand; the start portal off the west flank.", [], "CIRCUIT")
    # Stern: the Breakers, cutting up the keel's broken stern; its engines still fire aft
    for k in range(3):
        C.fx.append(dict(type="hulk", c=(-300 + k * 300, 2450), heading=0, length=320, width=100))
    C.fx.append(dict(type="engine_stand", c=(0, 2750), facing=0, length=900))
    C.fx.append(dict(type="debris", c=(0, 2300), r=260, n=40))
    C.fx.append(dict(type="minefield", c=(0, 3400), r=320, n=30, kind="seeker"))
    C.fx.append(dict(type="moon", c=(1900, 2500), r=240, infl=760))
    C.district("breakers", "BREAKERS' YARD", (0, 2450), "The keel's broken stern, being cut up for parts. Its engines still fire aft.",
               ["Salvage", "Engine wash off the stern", "Old minefield beyond"], "BREAKERS")
    # ---- the fabric: streets across every bay, the bays built up between the ribs
    for i, y in enumerate(ribs[:-1]):
        for s in (1, -1):
            C.lane([(s * 210, y + 200), (s * 900, y + 200)], "street", oneway=False)
    fab = Fabric((-1000, Y0 - 50, 1000, Y1 + 50))
    for s in (1, -1):
        fab.region(box((s * 480, 0), 0, 4500, 760))
    fab.cut(box((-560, -200), 0, 840, 560))          # the range's firing field
    fab.cut_circle(pit, 330)
    fab.cut(box((560, 500), 0, 560, 640))            # the gallery
    fab.cut_circle((420, 1400), 330)                 # the reactor court
    fab.cut(box((-600, 1400), 0, 300, 600))          # the grandstand
    fab.cut(box((560, -200), 0, 900, 560))           # the drydocks

    def style_at(p):
        x, y = p
        if y < -1400:
            return ("exchange", "commerce" if abs(x) < 600 else "warehouse", 1.0)
        if y < -600:
            return ("old_town", "tower", 1.1)
        if y < 200:
            return ("yards", "industrial", 1.0) if x > 0 else ("range", "military", 1.0)
        if y < 1000:
            return ("institute", "temple", 1.0) if x > 0 else ("pit", "midrise", 1.0)
        if y < 1800:
            return ("foundry", "industrial", 1.0) if x > 0 else ("circuit", "midrise", 0.9)
        return ("breakers", "warehouse", 1.0)
    fill_fabric(C, fab, style_at, lambda p: 0.0, tries=70000, seed=18)
    C.district("harbour", "THE BOW", (0, -2500), "Port Authority on the bow; liners dock in the cradle.", ["Harbour Master: reset"], None)
    callouts = [((840, -3700), "You spawn on the east flank lane, looking up the keel"),
                ((0, -2500), "The Bow: Port Authority and the liner cradle"), ((0, -1800), "The Exchange"),
                ((560, -200), "Keel Yards"), ((-700, -200), "Proving Range"), (pit, "The Pit"), ((560, 500), "The Institute"),
                ((520, 1400), "The Foundry: the keel's reactor"), ((-700, 1400), "The Circuit"), ((0, 2450), "Breakers' Yard")]
    return dict(key="B", title="OPTION B  ·  THE KEEL", name="THE KEEL",
                subtitle="A city grown along the 5.4 km keel of the colony ship that brought people here.",
                spawn=spawn, bounds=(-2300, -3900, 2300, 3600), wall=None, seed=12,
                concept=("The colony ship that brought the first settlers never left. Its 5.4 km keel became a street, its ribs "
                         "became the city's blocks, and three centuries of building filled the bays between them. Traffic runs "
                         "up the east flank and down the west, and through the keel's own arches. You arrive at the bow and look "
                         "up the whole length of the city, its lights running away into the distance. The ship's reactor still "
                         "pulses and its stern engines still fire."),
                strengths=["The grandest arrival: the whole city in one long perspective", "A single spine is impossible to get lost on",
                           "The ship's own history explains the reactor and the engine wash"],
                costs=["Linear: the far end is 5 km from the bow", "Districts on a flank feel like bays, less like quarters of a city"],
                callouts=callouts, **C.data())


# ---------------------------------------------------------------- option C: the Crown (an Aksharan gate)
def option_c():
    C = City("C", 13)
    # The relic: a 1.3 km Aksharan hoop standing across the plane; its two feet are where it crosses the flight plane
    for s in (1, -1):
        C.b(ngon((s * 650, 0), 70, 8, 22.5), 650, -650, "relic_foot", "relic", lit=1.0)
    C.fx.append(dict(type="relic_gate", a=(-650, 0), b=(650, 0)))
    C.holos.append(dict(text="THE CROWN", at=(0, -1800), district="gate", grand=True))
    # Arrival corridor through the gate (ships appear in a flash), departures leave the other way
    gate_in = [(-200, 0), (-200, -900)]
    gate_out = [(200, -900), (200, 0)]
    C.lane(gate_in, "gate_in"); C.lane(gate_out, "gate_out")
    C.traffic_on(gate_in, 4, [("liner", "vest"), ("freighter", "tlax"), ("hauler", "khu"), ("courier", "vest")], 41)
    C.traffic_on(gate_out, 4, [("freighter", "tlax"), ("hauler", "civ"), ("courier", "civ"), ("barge", "civ")], 42)
    loop = arc(1750, 0, 360)
    C.lane(loop, "boulevard", closed=True)
    C.traffic_on(loop, 18, [("shuttle", "civ"), ("hauler", "tlax"), ("courier", "vest"), ("tug", "scav"), ("wraith", "noct"),
                            ("freighter", "tlax")], 43)
    # Islands
    isl = {"harbour": (0, -1150), "exchange": polar(130, 1650), "yards": polar(70, 1900), "range": polar(30, 2300),
           "pit": polar(0, 1700), "institute": polar(285, 1250), "breakers": polar(320, 2050), "foundry": polar(220, 1900),
           "circuit": polar(250, 2500)}
    h = isl["harbour"]
    C.b(ngon(h, 110, 8, 22.5), 420, -900, "spire", "harbour", lit=1.0)
    for b in range(0, 360, 30):
        if 150 < b < 210:
            continue
        C.b(box(polar(b, 230, h), b, 120, 16), 18, -40, "pier", "harbour", lit=0.7)
        cls = ["freighter", "hauler", "liner", "barge"][b // 30 % 4]
        C.berth(polar(b + 15, 230 + CLASSES[cls][0] / 2 - 40, h), b + 15, CLASSES[cls][0] + 10, "pier", cls, "tlax")
    for k, (name, liv, cls) in enumerate([("TLAXCALA", "tlax", "freighter"), ("KHUCHAD", "khu", "hauler"),
                                          ("VESTARI", "vest", "courier"), ("SWAP", "scav", "tug")]):
        merchant_house(C, polar(k * 90 + 45, 220, isl["exchange"]), k * 90 + 45 + 90, name, liv, "exchange", cls)
    C.tower_cluster(isl["exchange"], 160, 8, 60, 260, "exchange", "warehouse", 30, 60, seed=71)
    for k in range(4):
        drydock(C, polar(70 + (k - 1.5) * 6, 1900), 70, 80, 34, ["cruiser", "cruiser", "wraith", "liner"][k], "noct", "yards")
    C.fx.append(dict(type="lab", c=polar(62, 1700)))
    for d in (200, 400, 600, 800):
        C.fx.append(dict(type="range_mark", c=polar(30, 2100 + d), facing=30))
    C.fx.append(dict(type="target_station", c=polar(30, 3000)))
    pit = isl["pit"]
    for b0 in range(0, 360, 45):
        C.b(annulus(250, 290, b0 + 5, b0 + 40, pit), 70, -160, "stands", "pit", lit=0.95)
    C.fx.append(dict(type="arena", c=pit, r=240))
    for i, n in enumerate(["MIRROR", "MAGNET", "PORTAL", "SHIELD", "REACTOR", "SWITCH+DOOR", "ARM", "REPULSOR", "EMP",
                           "SEEKER", "GRAVITY", "CANISTER", "DRIVER", "RAILS"]):
        p = polar(i * 360 / 14, 240, isl["institute"])
        C.b(ngon(p, 22, 8, 22.5), 22, -60, "plinth", "institute", lit=1.0)
        C.fx.append(dict(type="exhibit", c=p, name=n))
    C.b(ngon(isl["institute"], 90, 12), 300, -500, "sanctum", "institute", lit=1.0)
    for k in range(3):
        C.fx.append(dict(type="hulk", c=polar(320 + (k - 1) * 9, 2100 + k * 60), heading=320 + k * 30, length=320, width=90))
    C.fx.append(dict(type="debris", c=isl["breakers"], r=300, n=50))
    C.fx.append(dict(type="moon", c=polar(215, 2500), r=260, infl=800))
    C.fx.append(dict(type="reactor", c=isl["foundry"], r=240))
    C.fx.append(dict(type="minefield", c=polar(150, 2900), r=300, n=24, kind="prox"))
    C.fx.append(dict(type="portal", c=isl["circuit"], facing=250, width=160))
    for k, v in isl.items():
        if k not in ("harbour",):
            C.tower_cluster(v, 260, 6, 60, 240, k, "hab", 30, 60, seed=sum(map(ord, k)),
                            avoid=[lambda p, s, v=v: math.dist(p, v) < 200])
    for k, v in isl.items():
        C.lane([polar(bearing_of(v), 1750), v], "district", oneway=False)
    spawn = dict(c=(-200, -3200), heading=0)
    for y in range(-3100, -1400, 400):
        C.fx.append(dict(type="marker", c=(0, y)))
    C.lane([(-120, -3800), (-120, -1300)], "approach_in"); C.lane([(120, -1300), (120, -3800)], "approach_out")
    C.traffic_on([(-120, -3800), (-120, -1300)], 4, [("hauler", "civ"), ("shuttle", "civ"), ("freighter", "tlax")], 44)
    names = dict(harbour="THE HARBOUR", exchange="THE EXCHANGE", yards="CROWN YARDS", range="PROVING RANGE", pit="THE PIT",
                 institute="THE SANCTUM", breakers="BREAKERS' YARD", foundry="THE FOUNDRY", circuit="THE CIRCUIT")
    for k, v in isl.items():
        C.district(k, names[k], v, "", [], names[k].replace("THE ", "") if k not in ("harbour",) else None)
    # ---- the fabric: each island is a town, two cross streets and a ring street, built up to its edge
    fab = Fabric((-3200, -3600, 3200, 3400))
    for k, v in isl.items():
        R = 470 if k != "harbour" else 0
        if not R:
            continue
        fab.region(ngon(v, R, 40))
        hd = bearing_of(v)
        for a in (hd, hd + 90):
            C.lane([polar(a, R, v), polar(a + 180, R, v)], "street", oneway=False)
        C.lane(arc(R * 0.62, 0, 360, v, 4), "street", closed=True, oneway=False)
    fab.cut_circle(isl["pit"], 330); fab.cut_circle(isl["foundry"], 300); fab.cut_circle(isl["institute"], 280)
    fab.cut_circle(isl["breakers"], 260); fab.cut_circle(isl["circuit"], 200)
    style_of = dict(exchange="commerce", yards="industrial", range="military", pit="midrise", institute="temple",
                    breakers="warehouse", foundry="industrial", circuit="midrise")

    def style_at(p):
        k = min(isl, key=lambda q: math.dist(p, isl[q]))
        d = math.dist(p, isl[k]) / 470
        if k == "harbour":
            return None
        return (k, "tower" if d < 0.35 and k in ("exchange", "institute", "pit") else style_of[k], 1.2 - d * 0.6)
    fill_fabric(C, fab, style_at, lambda p: bearing_of(p, isl[min(isl, key=lambda q: math.dist(p, isl[q]))]), tries=70000, seed=19)
    callouts = [((-200, -3200), "You spawn on the approach, the hoop rising ahead"),
                ((0, 0), "The Crown: ships arrive through the Aksharan gate in flashes")] + \
               [(v, names[k]) for k, v in isl.items()]
    return dict(key="C", title="OPTION C  ·  THE CROWN", name="THE CROWN",
                subtitle="Station islands round an Aksharan gate, 1.3 km high, that still works.",
                spawn=spawn, bounds=(-3100, -3500, 3100, 3300), wall=None, seed=13,
                concept=("Nobody built the Crown. It is an Aksharan gate, a hoop 1.3 km across standing through the plane, and it "
                         "still opens onto other systems. A city of station islands grew around it to tax what comes through. "
                         "Ships arrive in a flash between its feet every few seconds. The Vestari say it is holy and keep a sanctum "
                         "at its foot; everyone else just trades."),
                strengths=["The strongest landmark in the game: a 1.3 km gate rising toward the camera",
                           "Arrivals are a show: ships flash in through the gate all the time"],
                costs=["Islands with open space between them: less like a city, more like an archipelago",
                       "The gate's lore commits the mystery (the Aksharans) early"],
                callouts=callouts, **C.data())


# ---------------------------------------------------------------- drawing
W, H, PANEL_W = 3300, 2100, 1060
KIND_TONE = {"spire": 1.25, "spire_top": 1.4, "ring": 1.0, "pier": 0.85, "spoke": 0.8, "wall": 0.75, "bastion": 0.9,
             "gatehouse": 1.2, "hab": 1.0, "warehouse": 0.8, "market": 1.05, "cargo": 0.7, "gantry": 0.95, "gantry_top": 1.1,
             "office": 1.1, "hangar": 0.9, "stands": 1.0, "plinth": 1.15, "gallery": 0.95, "grandstand": 1.05, "slipway": 0.6,
             "tank": 0.85, "stack": 0.9, "bulkhead": 0.7, "scoreboard": 1.0, "keel": 0.85, "rib": 0.8, "rib_tip": 1.0,
             "relic_foot": 1.3, "sanctum": 1.2, "keel_castle": 0.95}


def shade(col, k):
    return tuple(max(0, min(255, int(c * k))) for c in col)


def draw_shape(v, b, rng):
    s, c, p = b["shape"], b["c"], b["p"]
    dim = 1.0 if b["z"] > -120 else 0.3            # the lower orbits, under the plane, drawn dim
    acc = DISTRICT_COL.get(b.get("district") or "", (226, 228, 220))
    if s == "torus" and p.get("upright"):
        # a ring standing across the plane: seen from above it is a bar; a coil's inner face glows
        v.poly(b["pts"], fill=shade((170, 172, 166), dim) + (255,), outline=shade(acc, dim) + (160,), width=v.px(1))
        if b["kind"] == "coil":
            pts = b["pts"]
            v.line([lerp(pts[0], pts[1], 0.5), lerp(pts[3], pts[2], 0.5)], (190, 236, 255, int(230 * dim)), v.px(2))
    elif s == "torus":
        R, r = p["R"], p["r"]
        v.circle(c, R, outline=shade((150, 152, 144), dim) + (255,), width=max(v.px(2), v.m(2 * r)))
        v.circle(c, R + r, outline=shade(acc, dim) + (120,), width=v.px(1))
        n = int(R * 0.6 * b["lit"])
        for k in range(n):
            v.dot(polar(k * 360 / max(n, 1), R, c), 1.0, (255, 226, 176, int(200 * dim)))
        for k in range(p.get("spokes", 0)):
            a = k * 360 / p["spokes"]
            v.line([polar(a, p["hub"], c), polar(a, R - r, c)], shade((120, 122, 116), dim) + (255,), max(v.px(1.5), v.m(4)))
        if p.get("hub"):
            v.circle(c, p["hub"], fill=shade((140, 142, 136), dim) + (255,))
    elif s == "drum":
        pts = b["pts"]
        v.poly(pts, fill=shade((128, 130, 124), dim) + (255,), outline=shade(acc, dim) + (150,), width=v.px(1))
        for k in range(1, 6):
            q0 = lerp(pts[0], pts[3], k / 6); q1 = lerp(pts[1], pts[2], k / 6)
            v.line([q0, q1], (255, 226, 176, int(110 * dim)), v.px(1))
    elif s == "sphere":
        v.circle(c, p["r"], fill=shade((150, 148, 138), dim * 0.9) + (255,), outline=shade(acc, dim) + (140,), width=v.px(1))
        v.circle((c[0] - p["r"] * 0.3, c[1] + p["r"] * 0.3), p["r"] * 0.45, fill=(255, 255, 255, int(28 * dim)))
    elif s == "panel":
        col = {"collector": (40, 46, 70), "radiator": (150, 60, 36)}.get(b["kind"], (90, 92, 88))
        v.poly(b["pts"], fill=shade(col, dim) + (255,), outline=shade(acc if b["kind"] != "radiator" else (255, 120, 60), dim) + (200,),
               width=v.px(1))
    elif s == "petal":
        # a tilted mirror: bright where it faces the disk, its edge lit; rising petals solid, dipping ones dimmer
        up = p["z1"] > 0
        n = 10
        for i in range(n):
            f0, f1 = i / n, (i + 1) / n
            seg = annulus(p["r0"] + (p["r1"] - p["r0"]) * f0, p["r0"] + (p["r1"] - p["r0"]) * f1, p["bearing"] - p["width"] / 2,
                          p["bearing"] + p["width"] / 2)
            k = (1 - f0) * (1.0 if up else 0.6)
            v.poly(seg, fill=(int(150 + 90 * k), int(170 + 70 * k), int(200 + 50 * k), int(150 + 80 * k)))
        v.poly(b["pts"], outline=(255, 236, 200, 255), width=v.px(2))
    elif s == "truss":
        pts = b["pts"]
        v.line([lerp(pts[0], pts[3], 0.5), lerp(pts[1], pts[2], 0.5)], shade((150, 150, 140), dim) + (220,), max(v.px(1.5), v.m(p.get("width", 6))))


# each quarter builds in its own material (owner: "no differentiation between each section")
DISTRICT_BASE = {"exchange": (86, 80, 70), "foundry": (58, 62, 68), "breakers": (78, 64, 52), "range": (104, 108, 112),
                 "pit": (96, 74, 60), "gate": (62, 66, 70)}


def draw_building(v, b, rng):
    if b.get("shape"):
        return draw_shape(v, b, rng)
    pts = b["pts"]
    base = DISTRICT_BASE.get(b.get("district"), (70, 72, 68))
    tone = KIND_TONE.get(b["kind"], 1.0) * (0.75 + min(b["h"], 600) / 1400)
    if b["kind"].startswith("relic"):
        base = (150, 128, 84)
    # depth shadow: tall things cast a long dark shadow down-screen (light from the north-east, above the plane)
    sh = min(b["h"], 500) * 0.18
    v.poly([(x + sh * 0.5, y - sh) for x, y in pts], fill=(0, 0, 0, 120))
    v.poly(pts, fill=shade(base, tone) + (255,))
    xs, ys = [p[0] for p in pts], [p[1] for p in pts]
    w = max(xs) - min(xs); hh = max(ys) - min(ys)
    # roof plates and lit windows
    if b["kind"] in ("hab", "spire", "spire_top", "gatehouse", "office", "market", "rib_tip", "sanctum", "stack"):
        n = int(min(60, w * hh / 260 * b["lit"]))
        for _ in range(n):
            p = (rng.uniform(min(xs), max(xs)), rng.uniform(min(ys), max(ys)))
            if point_in_poly(p, pts):
                v.dot(p, rng.choice([1.0, 1.0, 1.5]), (255, 226, 176, int(120 + 120 * rng.random())))
    roof = b.get("roof")
    if roof:
        v.poly(pts, outline=LIVERY[roof][2] + (255,), width=v.px(2))
    else:
        v.line(pts, shade(VOXEL, 0.9 * tone) + (255,), v.px(1.5), closed=True)
        acc = DISTRICT_COL.get(b.get("district") or "", None)
        if acc and b["kind"] not in ("wall", "bastion"):
            inner = [lerp(q, (sum(xs) / len(xs), sum(ys) / len(ys)), 0.18) for q in pts]
            v.line(inner, acc + (90,), v.px(1), closed=True)
    if b["kind"] in ("spire", "spire_top", "gatehouse", "rib_tip", "relic_foot") or (b["kind"] == "sanctum" and b["h"] > 280):
        c = (sum(xs) / len(xs), sum(ys) / len(ys))
        v.dot(c, 4, (255, 70, 60, 255) if b["kind"] != "relic_foot" else (255, 200, 107, 255))


def draw_ship(v, s):
    L, B = CLASSES[s["cls"]]
    hull, trim, light = LIVERY[s["livery"]]
    c, hd = s["c"], s["heading"]
    f = polar(hd, L / 2); r = polar(hd + 90, B / 2)
    nose = (c[0] + f[0], c[1] + f[1])
    sh = [(c[0] + f[0] * 0.55 + r[0], c[1] + f[1] * 0.55 + r[1]), nose, (c[0] + f[0] * 0.55 - r[0], c[1] + f[1] * 0.55 - r[1]),
          (c[0] - f[0] - r[0], c[1] - f[1] - r[1]), (c[0] - f[0] + r[0], c[1] - f[1] + r[1])]
    v.poly(sh, fill=hull + (255,), outline=shade(trim, 1.0) + (255,), width=v.px(1))
    if s["state"] != "docked":
        tail = (c[0] - f[0] * 1.9, c[1] - f[1] * 1.9)
        v.line([(c[0] - f[0], c[1] - f[1]), tail], light + (150,), max(v.px(1.5), v.m(B * 0.35)))
    v.dot(nose, 1.4, light + (255,))


LANE_STYLE = {
    "approach_in": ((124, 255, 154), 4), "approach_out": ((255, 179, 71), 4), "harbour": ((255, 226, 176), 3),
    "boulevard": ((159, 224, 200), 3), "avenue_in": ((124, 255, 154), 2), "avenue_out": ((255, 179, 71), 2),
    "district": ((159, 224, 200), 2), "launch": ((242, 183, 5), 3), "gate_in": ((255, 200, 107), 4), "gate_out": ((255, 200, 107), 3),
    "current": ((120, 220, 255), 5), "drain": ((255, 106, 42), 3), "slingshot": ((124, 255, 154), 3), "street": ((255, 226, 176), 1),
    "tunnel": ((255, 179, 71), 3),
}


def draw_lane(v, ln):
    col, w = LANE_STYLE.get(ln["kind"], ((159, 224, 200), 2))
    pts = ln["pts"] + ([ln["pts"][0]] if ln["closed"] else [])
    v.line(pts, col + (40,), v.px(w * 5))
    v.line(pts, col + (150,), v.px(w * 0.6), dash=(v.px(10), v.px(9)))
    if ln["oneway"]:
        L = poly_len(pts)
        n = max(1, int(L / 380))
        for i in range(n):
            p, hd = point_on(pts, (i + 0.5) / n)
            a = polar(hd + 150, 26, p); b = polar(hd - 150, 26, p)
            v.line([a, p, b], col + (220,), v.px(2.5))


def draw_fx(v, fx, fonts):
    t = fx["type"]
    if t == "turret":
        turret(v, fx["c"], 90 - fx["facing"], fx["reach"] * 0.5, fx["arc"])
    elif t == "marker":
        v.dot(fx["c"], 4, (124, 255, 154, 255)); v.circle(fx["c"], 30, outline=(124, 255, 154, 120), width=v.px(1.5))
    elif t == "scanner":
        v.line([fx["a"], fx["b"]], (191, 214, 255, 220), v.px(5))
        v.poly([fx["a"], fx["b"], (fx["b"][0], fx["b"][1] - 160), (fx["a"][0], fx["a"][1] - 160)], fill=(191, 214, 255, 30))
    elif t == "signal":
        v.dot(fx["c"], 6, (124, 255, 154, 255))
    elif t == "patrol":
        for k in range(3):
            draw_ship(v, dict(c=polar(fx["heading"] + 180, k * 60, fx["c"]) if k else fx["c"], heading=fx["heading"], cls="wraith",
                              livery="noct", state="cruise"))
    elif t == "hulk":
        pts = box(fx["c"], fx["heading"], fx["length"], fx["width"])
        structure(v, pts, seed=int(fx["c"][0]) % 50, plate=22, base=(78, 76, 70))
    elif t == "rig":
        sweeper(v, fx["c"], 150, 2, 90 - fx["facing"] + 30, hub_r=30)
    elif t == "debris":
        rock_field(v, fx["c"], fx["r"], fx["n"], 9, 4, 12)
    elif t == "reactor":
        reactor_pulse(v, fx["c"], fx["r"])
    elif t == "moon":
        gravity_well(v, fx["c"], fx["r"], fx["infl"])
    elif t == "slow_field":
        v.circle(fx["c"], fx["r"], fill=SLOW + (40,), outline=SLOW + (160,), width=v.px(2))
    elif t == "minefield":
        mine_field(v, fx["c"], fx["r"], fx["n"], fx["kind"], 5)
    elif t == "lab":
        v.circle(fx["c"], 40, outline=(242, 183, 5, 255), width=v.px(3))
    elif t == "engine_stand":
        engine_wash(v, fx["c"], 90 - fx["facing"], fx["length"], 90)
    elif t == "range_mark":
        a = polar(fx["facing"] + 90, 260, fx["c"]); b = polar(fx["facing"] - 90, 260, fx["c"])
        v.line([a, b], (255, 85, 48, 90), v.px(1.5), dash=(v.px(6), v.px(6)))
    elif t == "target":
        v.circle(fx["c"], 22, outline=(255, 85, 48, 255), width=v.px(2)); v.dot(fx["c"], 3, (255, 85, 48, 255))
    elif t == "target_station":
        structure(v, ngon(fx["c"], 60, 8, 22.5), seed=3, plate=14)
    elif t == "field_wall":
        v.line([fx["a"], fx["b"]], (159, 224, 200, 60), v.px(10)); v.line([fx["a"], fx["b"]], (159, 224, 200, 255), v.px(2))
        for q in (fx["a"], fx["b"]):
            v.dot(q, 5, (159, 224, 200, 255))
    elif t == "turret_alley":
        n = 5
        for k in range(n):
            p = lerp(fx["a"], fx["b"], k / (n - 1))
            turret(v, p, 90 - bearing_of(fx["b"], fx["a"]) + 90, 140, 90)
    elif t == "arena":
        v.circle(fx["c"], fx["r"], fill=(255, 122, 47, 16), outline=(159, 224, 200, 200), width=v.px(2))
    elif t == "bout_gate":
        v.dot(fx["c"], 6, (255, 122, 47, 255))
    elif t == "exhibit":
        v.dot(fx["c"], 4, (159, 216, 255, 255))
    elif t == "portal":
        portal(v, fx["c"], 90 - fx["facing"] + 90, fx["width"])
    elif t == "black_hole":
        c = fx["c"]
        # the accretion disk: banded, hot white-blue inside to dull orange outside; the shadow; the photon ring
        rng_ = random.Random(3)
        n = 60
        for i in range(n, 0, -1):
            f = i / n
            r = fx["horizon"] * 2.5 + (fx["disk"] - fx["horizon"] * 2.5) * f
            col = lerp((255, 236, 200), (200, 70, 24), f) if f > 0.2 else lerp((210, 228, 255), (255, 236, 200), f / 0.2)
            band = 0.55 + 0.45 * rng_.random()
            v.circle(c, r, outline=tuple(int(x) for x in col) + (int((40 + 190 * (1 - f) ** 1.6) * band),), width=max(1, int(v.m((fx["disk"]) / n * 1.3))))
        v.circle(c, fx["horizon"] * 2.6, outline=(255, 250, 235, 255), width=v.px(3))
        v.circle(c, fx["horizon"] * 2.2, fill=(0, 0, 0, 255))
    elif t == "buoys":
        n = int(2 * math.pi * fx["r"] / fx.get("every", 60))
        for k in range(n):
            v.dot(polar(k * 360 / n, fx["r"], fx["c"]), 2.6, tuple(fx["col"]) + (230,))
    elif t == "contour":
        v.circle(fx["c"], fx["r"], outline=GRAVITY + (int(fx.get("a", 50)),), width=v.px(1.5))
        for k in range(16):
            a = k * 22.5 + 11
            v.arrow(polar(a, fx["r"] + 30, fx["c"]), polar(a, fx["r"] - 30, fx["c"]), GRAVITY + (int(fx.get("a", 50)) + 40,), v.px(1.5), head=v.px(5))
    elif t == "conduit":
        for w_, al in ((14, 30), (6, 90), (2, 255)):
            v.line(fx["pts"], (159, 216, 255, al), v.px(w_))
    elif t == "boulder":
        v.circle(fx["c"], fx["r"], fill=(84, 78, 70, 255), outline=(120, 112, 100, 255), width=v.px(1))
        v.circle((fx["c"][0] - fx["r"] * 0.3, fx["c"][1] + fx["r"] * 0.3), fx["r"] * 0.4, fill=(255, 255, 255, 18))
    elif t == "neon":
        a = polar(fx["heading"], -fx["length"] / 2, fx["c"]); b = polar(fx["heading"], fx["length"] / 2, fx["c"])
        col = tuple(int(fx["col"][i:i + 2], 16) for i in (1, 3, 5))
        for w_, al in ((7, 50), (3, 140), (1.2, 255)):
            v.line([a, b], col + (al,), max(v.px(w_), v.m(fx["height"] * 0.25)))
    elif t == "neon_line":
        col = tuple(int(fx["col"][i:i + 2], 16) for i in (1, 3, 5))
        for w_, al in ((4, 40), (1.2, 220)):
            v.line(fx["pts"], col + (al,), v.px(w_))
    elif t == "flash":
        col = tuple(int(fx["col"][i:i + 2], 16) for i in (1, 3, 5))
        for k_, al in ((3.0, 40), (1.6, 110), (0.7, 255)):
            v.circle(fx["c"], fx["r"] * k_, fill=col + (al,))
    elif t == "plasma":
        # plasma drawn off the disk: a hot stream, thick where it leaves the disk, thinning up the boom
        pts, rad = fx["pts"], fx["r"]
        for (a, b), (ra, rb) in zip(zip(pts, pts[1:]), zip(rad, rad[1:])):
            w = (ra + rb) / 2
            for k_, al, col in ((3.0, 40, (255, 150, 60)), (1.6, 110, (255, 190, 110)), (0.6, 255, (255, 246, 228))):
                v.line([a, b], col + (al,), max(v.px(1), v.m(w * k_)))
    elif t == "beam":
        col = tuple(int(fx["col"][i:i + 2], 16) for i in (1, 3, 5)) if fx.get("col") else (255, 236, 200)
        for w_, al in ((10, 40), (3, 255)):
            v.line([fx["a"], fx["b"]], col + (al,), v.px(w_))
    elif t == "flare_shadow":
        v.poly(fx["pts"], fill=(0, 0, 0, 90))
    elif t == "canister":
        canister(v, fx["c"], 0.8)
    elif t == "relic_gate":
        for w_, al in ((60, 30), (26, 80), (8, 220)):
            v.line([fx["a"], fx["b"]], (255, 200, 107, al), v.px(w_))


UNDER_FX = ("moon", "reactor", "minefield", "slow_field", "debris", "arena", "engine_stand", "black_hole", "contour",
            "flare_shadow", "conduit")


def draw_rock(v, rock):
    """Option E's asteroid in x-ray: solid rock shaded by its height, tunnels and caverns cut through it lit from inside."""
    h = rock["cell"] / 2 + 0.6
    for x, y, spans, kind in rock["cols"]:
        top = spans[-1][1]
        if kind == "rock":
            k = max(0.0, min(1.0, top / 320))
            col = (int(52 + 60 * k), int(48 + 52 * k), int(44 + 44 * k), 255)
        elif kind == "tunnel":
            col = (40, 30, 24, 255)
        elif kind == "cavern":
            col = (56, 40, 34, 255)
        else:
            col = (64, 50, 42, 255)
        v.poly([(x - h, y - h), (x + h, y - h), (x + h, y + h), (x - h, y + h)], fill=col)


def draw_option(opt):
    img = Image.new("RGB", (W * SS, H * SS), BG)
    starfield(img, (0, 0, (W - PANEL_W) * SS, H * SS), opt["seed"], 0.8)
    v = View(img, (50 * SS, 150 * SS, (W - PANEL_W - 30) * SS, (H - 50) * SS), opt["bounds"])
    d = v.d
    f_title, f_sub = font(F_TITLE, 34), font(F_BODY, 18)
    f_lab, f_small, f_holo = font(F_TITLE, 15), font(F_BOLD, 12), font(F_TITLE, 22)
    rng = random.Random(opt["seed"])
    # district tint, under everything
    for dd in ([] if opt.get("rock") else opt["districts"]):
        col = DISTRICT_COL.get(dd["key"], (200, 200, 200))
        v.circle(dd["centre"], 380 if dd["key"] not in ("harbour", "gate", "old_town") else 0, fill=col + (10,))
    # lower levels: a dim glow below the city (it goes on down past the plane)
    glow = Image.new("L", img.size, 0)
    gd = ImageDraw.Draw(glow)
    for b in opt["buildings"]:
        if b["d"] < -150:
            gd.polygon([v.P(p) for p in b["pts"]], fill=int(min(255, -b["d"] / 4)))
    glow = glow.filter(ImageFilter.GaussianBlur(40 * SS))
    tint = Image.new("RGB", img.size, (255, 196, 120))
    img.paste(Image.blend(img, tint, 1.0), mask=glow.point(lambda x: int(x * 0.16)))
    d = v.d = ImageDraw.Draw(img, "RGBA")
    for fx in opt["fx"]:
        if fx["type"] in UNDER_FX:
            draw_fx(v, fx, None)
    if opt.get("rock"):
        draw_rock(v, opt["rock"])
    for ln in opt["lanes"]:
        draw_lane(v, ln)
    for b in sorted(opt["buildings"], key=lambda b: b["h"]):
        draw_building(v, b, rng)
    for fx in opt["fx"]:
        if fx["type"] not in UNDER_FX:
            draw_fx(v, fx, None)
    for be in opt["berths"]:
        L = be["length"]
        v.poly(box(be["c"], be["heading"], L, 8), outline=(255, 226, 176, 70), width=v.px(1))
    for s in opt["ships"]:
        draw_ship(v, s)
    for h in opt["holos"]:
        f = font(F_TITLE, 40) if h.get("grand") else (font(F_TITLE, 11) if h.get("small") else f_holo)
        col = DISTRICT_COL.get(h["district"], (255, 241, 214))
        v.text(h["at"], h["text"], f, col + (235,))
    # spawn
    sp = opt["spawn"]
    v.circle(sp["c"], 70, outline=(255, 255, 255, 255), width=v.px(3))
    draw_ship(v, dict(c=sp["c"], heading=sp["heading"], cls="wraith", livery="noct", state="cruise"))
    v.text((sp["c"][0], sp["c"][1] - 130), "YOU START HERE", font(F_BOLD, 14), (255, 255, 255, 255))
    # numbered callouts
    for i, (p, _) in enumerate(opt["callouts"]):
        q = (p[0], p[1] + 170) if any(math.dist(p, h["at"]) < 60 for h in opt["holos"]) else p
        x, y = v.P(q); r = 15 * SS
        d.ellipse((x - r, y - r, x + r, y + r), fill=(0, 0, 0, 200), outline=TEXT + (255,), width=2 * SS)
        d.text((x, y), str(i + 1), font=font(F_BOLD, 14), fill=TEXT, anchor="mm")
    # scale bar
    x0, y0 = 60 * SS, (H - 40) * SS
    L = v.m(1000)
    d.line([(x0, y0), (x0 + L, y0)], fill=TEXT, width=2 * SS)
    for k in range(3):
        d.line([(x0 + L * k / 2, y0 - 8 * SS), (x0 + L * k / 2, y0 + 2 * SS)], fill=TEXT, width=2 * SS)
    d.text((x0 + L + 12 * SS, y0), "1 km", font=font(F_BODY, 15), fill=TEXT, anchor="lm")
    vw = v.m(195); vh = v.m(109)
    d.rectangle((x0 + L + 110 * SS, y0 - vh, x0 + L + 110 * SS + vw, y0), outline=(255, 255, 255, 160), width=1 * SS)
    d.text((x0 + L + 120 * SS + vw, y0 - vh / 2), "the screen at the normal 150 m zoom", font=font(F_BODY, 13), fill=DIM, anchor="lm")
    d.text((50 * SS, 40 * SS), opt["title"], font=f_title, fill=TEXT)
    d.text((50 * SS, 95 * SS), opt["subtitle"], font=f_sub, fill=DIM)

    # side panel
    px = (W - PANEL_W) * SS
    d.rectangle((px, 0, W * SS, H * SS), fill=PANEL)
    d.line([(px, 0), (px, H * SS)], fill=PANEL_LINE, width=2 * SS)
    y = [46 * SS]

    def head(t):
        d.text((px + 40 * SS, y[0]), t, font=font(F_TITLE, 17), fill=BOOST); y[0] += 34 * SS

    def para(t, size=15, col=TEXT, indent=0, f=F_BODY, width=None):
        for chunk in wrap(t, width or int(108 - indent / 5)):
            d.text((px + (40 + indent) * SS, y[0]), chunk, font=font(f, size), fill=col); y[0] += int(size * 1.42) * SS

    head("THE IDEA")
    para(opt["concept"])
    y[0] += 12 * SS
    head("ON THE MAP")
    for i, (_, txt) in enumerate(opt["callouts"]):
        yy = y[0]
        d.ellipse((px + 40 * SS, yy, px + 62 * SS, yy + 22 * SS), outline=TEXT, width=2 * SS)
        d.text((px + 51 * SS, yy + 11 * SS), str(i + 1), font=font(F_BOLD, 12), fill=TEXT, anchor="mm")
        para(txt, 15, TEXT, 34)
        y[0] += 2 * SS
    y[0] += 10 * SS
    head("STRENGTHS")
    for s in opt["strengths"]:
        para("+  " + s, 15, (124, 255, 154))
    y[0] += 8 * SS
    head("COSTS")
    for s in opt["costs"]:
        para("–  " + s, 15, (255, 179, 71))
    y[0] += 10 * SS
    head("TRAFFIC ON THIS MAP")
    n_dock = sum(1 for s in opt["ships"] if s["state"] == "docked")
    para(f"{len(opt['ships'])} ships: {len(opt['ships']) - n_dock} under way, {n_dock} docked. Every one is a real ship flying on "
         "its own thrusters: they queue, dock, wait, undock and leave, give way to you and recover when you ram them.", 15, DIM)
    y[0] += 10 * SS
    head("KEY")
    keys = [((124, 255, 154), "inbound lanes"), ((255, 179, 71), "outbound lanes"), ((159, 224, 200), "city lanes, field walls"),
            ((255, 85, 48), "turrets, targets"), ((168, 204, 255), "gravity, portals"), ((255, 106, 42), "reactor pulse")]
    for col, txt in keys:
        d.rectangle((px + 40 * SS, y[0] + 4 * SS, px + 54 * SS, y[0] + 18 * SS), fill=col)
        para(txt, 14, DIM, 24)
    d.text((px + 40 * SS, (H - 34) * SS), "DEV-023 · true scale · the flight plane; buildings rise above it and hang below it",
           font=font(F_BODY, 13), fill=DIM)
    out = os.path.join(OUT_IMG, f"hub_option_{opt['key'].lower()}.png")
    os.makedirs(OUT_IMG, exist_ok=True)
    img.resize((W, H), Image.LANCZOS).save(out, optimize=True)
    return out


def main():
    import hub_option_d
    import hub_option_e
    opts = [option_a(), option_b(), option_c(), hub_option_d.option_d(), hub_option_e.option_e()]
    for o in opts:
        print(draw_option(o), len(o["buildings"]), "buildings", len(o["ships"]), "ships")
    os.makedirs(os.path.dirname(OUT_JSON), exist_ok=True)
    with open(OUT_JSON, "w") as f:
        json.dump({o["key"]: {k: v for k, v in o.items()} for o in opts}, f, separators=(",", ":"))
    print(OUT_JSON)


if __name__ == "__main__":
    main()
