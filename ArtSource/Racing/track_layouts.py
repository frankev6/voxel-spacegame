#!/usr/bin/env python3
"""RACE-002: top-down layout options for the first race track (SPEC-92), plus the new-mechanics sheet.

Each option is a true-scale map (metres, +x east, +y north on the flight plane) built around a set piece,
with numbered sectors, every route's lap time, a speed trace and the per-lap events. The coordinates
RACE-004 needs are written as JSON next to the images.

Lap times use the production flight limits (config_flight) on a clean line: cap 120 m/s, boost 192 m/s,
about 25 m/s² forward, 20 m/s² braking and sideways grip, carry bleeding at 8 m/s² (SPEC-91). Gravity
wells, grapples, portals and pickups are not modelled, so the routes that use them are faster than shown.

Run:  python3 ArtSource/Racing/track_layouts.py
Out:  docs/design/racing/img/track_option_<x>.png, docs/design/racing/img/mechanics.png,
      docs/design/racing/track_options.json
"""
import json
import math
import os
import random
from PIL import Image, ImageDraw

from racing_common import *  # noqa: F401,F403  (shared colours, geometry and primitives)

OUT_IMG = os.path.join(ROOT, "docs", "design", "racing", "img")
OUT_JSON = os.path.join(ROOT, "docs", "design", "racing", "track_options.json")
V_CAP, V_BOOST, A_FWD, A_BRAKE, A_LAT = 120.0, 192.0, 25.0, 20.0, 20.0
CARRY_DECEL, STRIP_ACCEL, SLOW_CAP = 8.0, 60.0, 60.0


# ---------------------------------------------------------------- model
def nearest_f(path, p):
    bi = min(range(len(path["poly"])), key=lambda i: math.dist(p, path["poly"][i]))
    return path["s"][bi] / path["len"]


def build(opt):
    paths = {}
    for name in ["main"] + [n for n in opt["paths"] if n != "main"]:
        p = dict(opt["paths"][name])
        if "from_p" in p:  # branch leaves and rejoins the main line along its direction
            main = paths["main"]
            fa, fb = nearest_f(main, p["from_p"]), nearest_f(main, p["to_p"])
            (a, ta), (b, tb) = at(main, fa), at(main, fb)
            lead = p.get("lead", 90)
            p["points"] = [a, (a[0] + ta[0] * lead, a[1] + ta[1] * lead)] + p["inner"] + \
                          [(b[0] - tb[0] * lead, b[1] - tb[1] * lead), b]
            p["join"] = (fa, fb)
        extra = {k: v for k, v in p.items() if k not in ("points", "closed")}
        paths[name] = dict(make_path(p["points"], p.get("closed", False)), **extra, name=name)
    opt["_paths"] = paths
    for item in opt.get("barricades", []) + opt.get("doors", []):
        if "at_p" in item:
            item["f"] = nearest_f(paths[item["path"]], item["at_p"])
    skipped = [p["join"] for n, p in paths.items() if n != "main"] + [r["portal"] for r in opt["routes"] if r.get("portal")]
    for g in opt["gates"]:
        g["required"] = g["path"] == "main" and not any(a < g["f"] < b for a, b in skipped)
    return opt


def route_poly(opt, branches, portal=None):
    """Main loop with branches spliced in; `portal` = (f_in, f_out) skips that span of the main line."""
    main = opt["_paths"]["main"]
    pts, jumps, cursor = [], set(), 0.0
    cuts = [(opt["_paths"][b]["join"], b) for b in branches]
    if portal:
        cuts.append((portal, None))
    for (f_out, f_in), br in sorted(cuts, key=lambda c: c[0][0]):
        pts += sub_poly(main, cursor, f_out)
        if br:
            pts += opt["_paths"][br]["poly"]
        else:
            jumps.add(len(pts) - 1)
        cursor = f_in
    pts += sub_poly(main, cursor, 1.0001)
    return pts, jumps


G_TETHER, GM_WELL = 40.0, 2.9e6   # extra centripetal grip: grapple tether (m/s²); moon's GM (m³/s²)


def flags_along(pts, zones, opt):
    out = [False] * len(pts)
    for z in zones:
        path = opt["_paths"][z["path"]]
        seg = sub_poly(path, *z["f"])
        if len(seg) < 2:
            continue
        xs, ys = [q[0] for q in seg], [q[1] for q in seg]
        bx = (min(xs) - 100, min(ys) - 100, max(xs) + 100, max(ys) + 100)
        for i, p in enumerate(pts):
            if not out[i] and bx[0] <= p[0] <= bx[2] and bx[1] <= p[1] <= bx[3] and dist_to_poly(p, seg) < path["width"] / 2 + 5:
                out[i] = True
    return out


def lap(opt, branches, portal=None):
    pts, jumps = route_poly(opt, branches, portal)
    n = len(pts)
    r = curvature_radius(pts, True)
    boost = flags_along(pts, opt.get("boost", []), opt)
    slow = flags_along(pts, opt.get("slow", []), opt)
    vlim = []
    for i in range(n):
        grip = A_LAT
        for pl in opt.get("pylons", []):
            if math.dist(pts[i], pl["c"]) <= pl["reach"] + 40:
                grip += G_TETHER
        for w in opt.get("wells", []):
            d = max(w["body"], math.dist(pts[i], w["c"]))
            if d < w["infl"]:
                grip += GM_WELL / (d * d)
        if i in jumps or (i - 1) in jumps or (i + 1) % n in jumps:
            grip = 1e6  # the portal hop itself is not a corner
        vlim.append(min(math.sqrt(grip * r[i]), SLOW_CAP if slow[i] else V_BOOST))
    ds = [0.0 if i in jumps else math.dist(pts[i], pts[(i + 1) % n]) for i in range(n)]
    v, vel = [0.0] * n, 60.0
    for _ in range(2):
        for i in range(n):
            cap = V_BOOST if boost[i] else V_CAP
            if vel <= cap:
                vel = min(cap, math.sqrt(vel * vel + 2 * (STRIP_ACCEL if boost[i] else A_FWD) * ds[i]))
            else:
                vel = max(cap, math.sqrt(max(0.0, vel * vel - 2 * CARRY_DECEL * ds[i])))
            vel = min(vel, vlim[i]); v[i] = vel
    for _ in range(2):
        for i in range(n - 1, -1, -1):
            v[i] = min(v[i], math.sqrt(v[(i + 1) % n] ** 2 + 2 * A_BRAKE * ds[i]))
    t = sum(ds[i] / max(5.0, 0.5 * (v[i] + v[(i + 1) % n])) for i in range(n))
    return dict(time=t, len=sum(ds), vmin=min(v), vmax=max(v), trace=(v, ds))


# ---------------------------------------------------------------- derelict hull helpers
def hull_from_axis(a, b, profile):
    L = math.dist(a, b); u = ((b[0] - a[0]) / L, (b[1] - a[1]) / L); n = (-u[1], u[0])
    return [(a[0] + u[0] * s * L + n[0] * w, a[1] + u[1] * s * L + n[1] * w) for s, w in profile]


def along(a, b, s, w):
    L = math.dist(a, b); u = ((b[0] - a[0]) / L, (b[1] - a[1]) / L); n = (-u[1], u[0])
    return (a[0] + u[0] * s + n[0] * w, a[1] + u[1] * s + n[1] * w)


DER_A, DER_B = (150, 660), (1030, 64)
DER_PROFILE = [(0.0, -140), (0.06, -195), (0.27, -195), (0.31, -240), (0.44, -240), (0.48, -195), (0.78, -195),
               (0.9, -108), (1.03, 0), (0.9, 108), (0.78, 195), (0.6, 195), (0.56, 245), (0.47, 245), (0.43, 195),
               (0.06, 195), (0.0, 140)]

# ---------------------------------------------------------------- the options
OPTIONS = {
    "A": dict(
        name="Derelict Run", seed=11, start_f=0.004,
        tagline="A lap around, along or straight through a 1 km dead dreadnought. Its guns wake on lap 2.",
        concept="A dead capital ship lies across the north-east corner. The outer line goes around it past salvage cranes "
                "that sweep the track. The gun deck skims its flank under turrets. The hangar run flies through its "
                "dark interior, past bulkheads someone has to shoot open, to a blast door anyone on the gun deck can "
                "slam shut. The south half is about engines: three dead drives vent across the straight in turn, and "
                "a switchback full of seeker mines leads back to the start.",
        paths={
            "main": dict(closed=True, width=140, left="field", right="voxel", points=[
                (-1250, -100), (-1200, 450), (-900, 780), (-350, 840), (300, 900), (850, 1020), (1300, 860),
                (1450, 420), (1380, -150), (1150, -650), (700, -850), (0, -780), (-500, -820), (-740, -640),
                (-620, -420), (-950, -280)]),
            "hangar": dict(width=60, interior=True, left="voxel", right="voxel", label="HANGAR RUN", label_f=0.55, label_off=2.2,
                           from_p=(-640, 828), to_p=(820, -810), lead=120,
                           inner=[along(DER_A, DER_B, -120, 0), along(DER_A, DER_B, 500, 0), along(DER_A, DER_B, 1110, 0),
                                  (1190, -260), (1140, -560)]),
            "gundeck": dict(width=100, left="field", right="none", label="GUN DECK", label_f=0.30, label_off=-1.5,
                            from_p=(-760, 815), to_p=(860, -800), lead=150,
                            inner=[along(DER_A, DER_B, 40, -380), along(DER_A, DER_B, 560, -370), along(DER_A, DER_B, 960, -400),
                                   (990, -560)]),
        },
        structures=[dict(pts=hull_from_axis(DER_A, DER_B, DER_PROFILE), seed=5)],
        boost=[dict(path="main", f=(0.09, 0.15)), dict(path="main", f=(0.46, 0.50))],
        slow=[],
        canisters=[(-640, 840), (-560, 842), (-480, 845), (1452, 260), (1448, 180), (1440, 100), (300, -790), (380, -795)],
        mines=[dict(c=(-1010, 690), r=70, n=6, kind="emp"), dict(c=(-760, -500), r=150, n=10, kind="seeker"),
               dict(c=(-520, -660), r=40, n=3, kind="prox")],
        rocks=[((-860, -430), 120, 10), ((1180, -880), 90, 6)],
        barricades=[dict(path="hangar", at_p=along(DER_A, DER_B, 330, 0)), dict(path="hangar", at_p=along(DER_A, DER_B, 620, 0))],
        doors=[dict(path="hangar", at_p=along(DER_A, DER_B, 930, 0), switch=along(DER_A, DER_B, 880, -215))],
        turrets=[dict(c=along(DER_A, DER_B, s, -205), facing=-124, reach=330, arc=120) for s in (230, 520, 760)],
        sweepers=[dict(hub=(760, 1330), length=480, arms=2, phase=-70), dict(hub=(1700, 520), length=420, arms=3, phase=200)],
        washes=[dict(o=(520, -1100), dir=90, length=430, width=170), dict(o=(20, -1050), dir=90, length=380, width=160),
                dict(o=(-330, -1090), dir=90, length=380, width=160)],
        pylons=[dict(c=(930, -640), reach=230)],
        gates=[dict(id=0, path="main", f=0.0), dict(id=1, path="main", f=0.10), dict(id=2, path="main", f=0.20),
               dict(id=3, path="main", f=0.30), dict(id=4, path="main", f=0.40), dict(id=5, path="main", f=0.47),
               dict(id=6, path="main", f=0.60), dict(id=7, path="main", f=0.72), dict(id=8, path="main", f=0.84),
               dict(id=9, path="hangar", f=0.5), dict(id=10, path="gundeck", f=0.5)],
        routes=[dict(name="Outer line", branches=[], note="Cranes and canisters; slowest, but nothing shoots at you"),
                dict(name="Gun deck", branches=["gundeck"], note="Fastest on lap 1; from lap 2 three turrets shove the leader off it"),
                dict(name="Hangar run", branches=["hangar"], note="Shut on lap 1 until someone shoots the bulkheads; then the steady fast line, unless the door slams")],
        sectors=[
            ((-1300, 120), "Start straight on the west side; the pack funnels into the north-west apex."),
            ((-1010, 790), "EMP mines on the apex: one cuts your thrusters for 1.5 s and you drift on carry."),
            ((-560, 960), "Boost strip and energy canisters; then the split: outer line, gun deck or hangar."),
            (along(DER_A, DER_B, 0, 120), "Hangar mouth: fly into the wreck. Dark, 60 m wide, voxel walls."),
            (along(DER_A, DER_B, 520, 150), "Two bulkheads. The first ship through must shoot them; they stay open for everyone after."),
            (along(DER_A, DER_B, 930, 150), "Blast door. Its switch is on the gun deck: one shot slams it for 6 s on whoever is inside."),
            (along(DER_A, DER_B, 380, -480), "Gun deck: wide and fast, but on lap 2 the wreck's turrets wake and target the leader."),
            ((760, 1180), "Salvage cranes sweep the outer corner. Time the gap or get batted into the wall."),
            ((1560, 120), "East run: canisters, then a three-arm crane over the track."),
            ((780, -560), "Grapple hairpin, where the gun deck rejoins: hook the pylon and swing round it without braking."),
            ((100, -1170), "Engine graveyard: three dead drives vent across the straight in turn, every 12 s."),
            ((-900, -640), "Seeker switchback: mines drift toward the nearest ship; hide behind rocks or lead them into a rival."),
        ],
        events=["Lap 1: bulkheads are closed; the wreck is quiet.",
                "Lap 2: the gun-deck turrets wake and fire at the race leader.",
                "Lap 3: the wreck's reactor vents; a slow field fills the hangar's last third."],
        mechanics=["EMP mines", "Seeker mines", "Bulkheads", "Blast door + switch", "Gauntlet turrets", "Sweeper cranes",
                   "Grapple pylon", "Engine wash", "Canisters", "Lap events"],
    ),
    "B": dict(
        name="Moon Slingshot", seed=23, start_f=0.62,
        tagline="A figure eight: one lobe whips round a moon's gravity well, the other threads a spinning station.",
        concept="The east lobe orbits a cratered moon. The high orbit is wide and safe, but the far side is sunlit and "
                "solar flares hit it every 25 s. The low orbit skims the surface: the moon's gravity whips you round, "
                "and its shadow hides you from the flares, but debris orbits between the two lines and the moon "
                "itself is a voxel wall. The west lobe circles the Halo station, whose arms sweep the track. A pair of "
                "lane-gate portals skips them, but each portal jams for 6 s after use, so the leader can deny it. "
                "Both lobes meet at a level crossing seeded with repulsor mines.",
        paths={
            "main": dict(closed=True, width=150, left="field", right="field", points=[
                (0, 0), (165, 280), (650, 560), (1135, 280), (1135, -280), (650, -560), (165, -280),
                (0, 0), (-293, 235), (-700, 470), (-1107, 235), (-1107, -235), (-700, -470), (-293, -235)]),
            "low": dict(width=70, left="field", right="field", label="LOW ORBIT", label_f=0.5, label_off=-1.2,
                        from_p=(221, 360), to_p=(221, -360), lead=110,
                        inner=[(470, 400), (650, 345), (893, 243), (990, 0), (893, -243), (650, -345), (470, -400)]),
        },
        wells=[dict(c=(650, 0), body=230, infl=800)],
        orbits=[dict(c=(650, 0), r=448, n=30, dir=-1)],
        flare=dict(region=[(180, 740), (1450, 740), (1450, -740), (180, -740)], sun=0,
                   shadows=[[(650, 230), (1450, 230), (1450, -230), (650, -230)]]),
        structures=[dict(pts=[(-760 + 150 * math.cos(t), 150 * math.sin(t)) for t in [i * math.tau / 16 for i in range(16)]], seed=9)],
        sweepers=[dict(hub=(-760, 0), length=540, arms=2, phase=55, hub_r=150)],
        portals=[dict(path="main", f=0.66, label="GATE IN"), dict(path="main", f=0.84, label="GATE OUT")],
        boost=[dict(path="main", f=(0.96, 0.999)), dict(path="main", f=(0.0, 0.04)), dict(path="main", f=(0.46, 0.54))],
        slow=[],
        canisters=[(-200, -165), (-150, -125), (-100, -85), (100, 85), (150, 125), (200, 165)],
        mines=[dict(c=(105, -125), r=18, n=2, kind="repulse"), dict(c=(-105, 125), r=18, n=2, kind="repulse"),
               dict(c=(1000, 600), r=60, n=4, kind="gravity")],
        rocks=[],
        gates=[dict(id=0, path="main", f=0.62), dict(id=1, path="main", f=0.72), dict(id=2, path="main", f=0.78),
               dict(id=3, path="main", f=0.90), dict(id=4, path="main", f=0.06), dict(id=5, path="main", f=0.12),
               dict(id=6, path="main", f=0.25), dict(id=7, path="main", f=0.38), dict(id=8, path="main", f=0.47),
               dict(id=9, path="low", f=0.5)],
        routes=[dict(name="High orbit", branches=[], note="Wide; exposed to flares on the far side"),
                dict(name="Low orbit", branches=["low"], note="The moon's pull lets you corner harder; shadowed, narrow, debris"),
                dict(name="Portal + high", branches=[], portal=(0.66, 0.84), note="Skips the Halo arms when the gate is not jammed"),
                dict(name="Portal + low", branches=["low"], portal=(0.66, 0.84), note="The fastest lap, and the one most easily denied")],
        sectors=[
            ((-760, 300), "Start on the Halo's north side; the first move is portal or arms."),
            ((-1260, 0), "Halo sweepers: two 540 m arms cross the west end every 8 s."),
            ((-560, 600), "Lane-gate portals skip the sweepers; each jams for 6 s after use, so leading with it denies it."),
            ((-130, 0), "Level crossing with repulsor mines on two approaches: no damage, just a shove into crossing traffic."),
            ((400, -60), "Boost diagonals with canisters on both entries to the crossing."),
            ((650, 720), "High orbit: wide and safe, but sunlit."),
            ((1060, 0), "Low orbit: 70 m wide between the moon's surface and the debris; the well whips you round."),
            ((650, -448), "Debris ring orbits between the lines. Shoot a rock and it drifts into the other lane."),
            ((1300, -560), "Solar flare: every 25 s, 4 s of heat and shield drain on the sunlit side; the moon's shadow is safe."),
            ((1000, 760), "Gravity mines on the high line pull you off it."),
        ],
        events=["Lap 1: flares every 25 s.", "Lap 2: flares every 15 s; the Halo spins 30% faster.",
                "Lap 3: one portal stays jammed for the whole lap (random, seeded)."],
        mechanics=["Gravity well", "Debris orbit", "Solar flare + shadows", "Sweeper arms", "Lane-gate portals",
                   "Repulsor mines", "Gravity mines", "Canisters", "Level crossing", "Lap events"],
    ),
    "C": dict(
        name="Foundry Gauntlet", seed=37, start_f=0.004,
        tagline="An ore foundry: conveyor rocks, smelter vents, a test-firing freighter and a three-lane fork under guns.",
        concept="The track winds through a working foundry. A docked freighter test-fires its engines across the start "
                "straight. Smelter vents blast heat across the north sweeper, and an ore conveyor carries rocks across "
                "the S-bend on a rail. After a grapple hairpin the track forks into three lanes. The boost lane is "
                "fastest, but it runs under a turret platform and ends at a blast door whose switch sits on that "
                "platform. The barricade lane is shortest, narrow and walled. The safe lane is longest, with energy "
                "canisters. All three merge at an EMP minefield.",
        paths={
            "main": dict(closed=True, width=130, left="field", right="voxel", points=[
                (-1100, -150), (-1020, 360), (-680, 600), (-220, 520), (60, 300), (360, 420), (760, 600),
                (1110, 380), (1200, -60), (1020, -380), (760, -440), (560, -540), (300, -770), (0, -830),
                (-300, -770), (-600, -580), (-850, -520), (-1050, -430)]),
            "boostlane": dict(width=110, left="field", right="field", label="BOOST LANE", label_f=0.5, label_off=-1.4,
                              from_p=(910, -405), to_p=(-1010, -445), lead=40, inner=[(640, -360), (380, -290), (0, -240), (-380, -320), (-660, -455)]),
            "narrow": dict(width=60, left="voxel", right="voxel", label="BARRICADE LANE", label_at=(-300, -575),
                           from_p=(760, -440), to_p=(-1010, -445), lead=60, inner=[(420, -470), (0, -490), (-400, -505), (-700, -520)]),
        },
        structures=[dict(pts=[(-640, 140), (-260, 140), (-260, 330), (-380, 330), (-380, 380), (-520, 380), (-520, 330), (-640, 330)], seed=4),
                    dict(pts=[(-170, -320), (170, -320), (195, -360), (170, -400), (-170, -400), (-195, -360)], seed=8),
                    dict(pts=[(-1640, -10), (-1460, -10), (-1420, -50), (-1460, -90), (-1640, -90)], seed=2)],
        washes=[dict(o=(-1420, -50), dir=0, length=420, width=180), dict(o=(-560, 390), dir=95, length=230, width=120, heat=True),
                dict(o=(-340, 390), dir=80, length=220, width=110, heat=True)],
        rails=[dict(pts=[(230, 1080), (225, 700), (220, 360), (215, 20)], rocks=[0.12, 0.36, 0.62, 0.86])],
        pylons=[dict(c=(1010, -60), reach=210)],
        turrets=[dict(c=(-90, -320), facing=90, reach=240, arc=110), dict(c=(90, -320), facing=90, reach=240, arc=110),
                 dict(c=(-90, -400), facing=270, reach=200, arc=100), dict(c=(90, -400), facing=270, reach=200, arc=100)],
        doors=[dict(path="boostlane", f=0.84, switch=(-185, -360))],
        boost=[dict(path="boostlane", f=(0.10, 0.70)), dict(path="main", f=(0.40, 0.47))],
        slow=[dict(path="main", f=(0.12, 0.17))],
        canisters=[(300, -765), (150, -800), (0, -812), (-150, -800), (-300, -760)],
        mines=[dict(c=(-900, -480), r=70, n=9, kind="emp"), dict(c=(330, 470), r=40, n=3, kind="prox")],
        rocks=[((1250, 200), 90, 6)],
        barricades=[dict(path="narrow", f=0.12), dict(path="narrow", f=0.62)],
        gates=[dict(id=0, path="main", f=0.0), dict(id=1, path="main", f=0.10), dict(id=2, path="main", f=0.24),
               dict(id=3, path="main", f=0.36), dict(id=4, path="main", f=0.50), dict(id=5, path="main", f=0.60),
               dict(id=6, path="main", f=0.78), dict(id=7, path="main", f=0.94),
               dict(id=8, path="boostlane", f=0.5), dict(id=9, path="narrow", f=0.5)],
        routes=[dict(name="Safe lane", branches=[], note="Canisters refill your boost for the next lap"),
                dict(name="Boost lane", branches=["boostlane"], note="Four turrets above; door at the exit"),
                dict(name="Barricade lane", branches=["narrow"], note="Two barricades; one ship blocks it")],
        sectors=[
            ((-1300, 100), "Start straight: the docked freighter test-fires its engines across it every 14 s."),
            ((-870, 700), "North sweeper through magnetic dust (slow field)."),
            ((-450, 470), "Smelter vents: heat bursts shove you toward the outer wall."),
            ((380, 260), "Ore conveyor: rocks ride a rail across the S-bend. Shoot one off the rail into a rival."),
            ((1080, -260), "Grapple hairpin: hook the pylon and swing into the fork at speed."),
            ((860, -560), "The fork: boost lane, barricade lane or safe lane."),
            ((0, -120), "Boost lane: 192 m/s under four turrets; the exit door can slam shut."),
            ((280, -360), "Gauntlet platform: turrets cover both upper lanes, and the door switch is here."),
            ((0, -570), "Barricade lane: two barricades, 60 m between voxel walls."),
            ((0, -910), "Safe lane: longest, with energy canisters."),
            ((-960, -620), "The merge: EMP mines where all three lanes rejoin."),
        ],
        events=["Lap 1: conveyor every 6 s; barricades closed.", "Lap 2: the turrets fire at anyone in the boost lane, not just the leader.",
                "Lap 3: slag pours from the smelter: a slow field floods the north sweeper twice as long."],
        mechanics=["Engine wash (freighter)", "Heat vents", "Ore conveyor", "Grapple pylon", "Gauntlet turrets",
                   "Blast door + switch", "Barricades", "EMP mines", "Canisters", "Lap events"],
    ),
}


# ---------------------------------------------------------------- map composition
W, H, PANEL_W, STRIP_H = 3300, 1950, 1040, 170


def draw_track(opt, key):
    img = Image.new("RGB", (W * SS, H * SS), BG)
    map_rect = (60 * SS, 150 * SS, (W - PANEL_W - 40) * SS, (H - STRIP_H - 40) * SS)
    starfield(img, (0, 0, (W - PANEL_W) * SS, H * SS), opt["seed"])
    xs = [p[0] for path in opt["_paths"].values() for p in path["poly"]]
    ys = [p[1] for path in opt["_paths"].values() for p in path["poly"]]
    for s in opt.get("sweepers", []):
        xs += [s["hub"][0] - s["length"], s["hub"][0] + s["length"]]; ys += [s["hub"][1] - s["length"], s["hub"][1] + s["length"]]
    for w in opt.get("washes", []):
        xs.append(w["o"][0]); ys.append(w["o"][1])
    for st in opt.get("structures", []):
        xs += [q[0] for q in st["pts"]]; ys += [q[1] for q in st["pts"]]
    for r in opt.get("rails", []):
        xs += [q[0] for q in r["pts"]]; ys += [q[1] for q in r["pts"]]
    pad = 120
    v = View(img, map_rect, (min(xs) - pad, min(ys) - pad, max(xs) + pad, max(ys) + pad))
    paths = opt["_paths"]
    f_small, f_mid, f_lab = font(F_BOLD, 14), font(F_BOLD, 17), font(F_BOLD, 19)

    if "flare" in opt:
        fl = opt["flare"]; flare_zone(v, fl["region"], fl["sun"], fl["shadows"])
        v.arrow((fl["region"][0][0] - 170, 600), (fl["region"][0][0] - 40, 600), FLARE + (255,), v.px(4), head=v.px(14))
        v.text((fl["region"][0][0] - 110, 650), "SUN", f_mid, FLARE + (255,))
    for w in opt.get("wells", []):
        gravity_well(v, w["c"], w["body"], w["infl"])
    for o in opt.get("orbits", []):
        orbit(v, o["c"], o["r"], o["n"], opt["seed"], o["dir"])
    for i, s in enumerate(opt.get("structures", [])):
        structure(v, s["pts"], seed=s.get("seed", i))

    # corridors
    for path in paths.values():
        v.line(path["poly"], TRACK_EDGE, v.m(path["width"] + 8), path["closed"])
    for path in paths.values():
        v.line(path["poly"], TRACK, v.m(path["width"]), path["closed"])
        if path.get("interior"):
            hulls = [st["pts"] for st in opt.get("structures", [])]
            run = []
            for q in path["poly"] + [None]:
                if q is not None and any(point_in_poly(q, h) for h in hulls):
                    run.append(q)
                elif len(run) > 1:
                    v.line(run, INTERIOR, v.m(path["width"])); run = []
                else:
                    run = []
    for path in paths.values():
        v.line(path["poly"], CENTRE + (100,), v.px(2), path["closed"], dash=(v.px(14), v.px(18)))

    for z in opt.get("slow", []):
        path = paths[z["path"]]; seg = sub_poly(path, *z["f"])
        v.line(seg, SLOW + (60,), v.m(path["width"] + 30)); v.line(seg, SLOW + (95,), v.m(path["width"] * 0.7))
    for z in opt.get("boost", []):
        path = paths[z["path"]]; f0, f1 = z["f"]
        k = max(2, int((f1 - f0) * path["len"] / 45))
        for j in range(k):
            p, t = at(path, f0 + (f1 - f0) * (j + 0.5) / k)
            nx, ny = -t[1], t[0]; hw = path["width"] * 0.3; dep = 15
            tip = (p[0] + t[0] * dep, p[1] + t[1] * dep)
            l = (p[0] - t[0] * dep + nx * hw, p[1] - t[1] * dep + ny * hw)
            r = (p[0] - t[0] * dep - nx * hw, p[1] - t[1] * dep - ny * hw)
            v.d.line([v.P(l), v.P(tip), v.P(r)], fill=BOOST + (235,), width=v.px(4), joint="curve")

    # walls: each corridor edge, opened where another corridor passes
    for path in paths.values():
        for side in ("left", "right"):
            kind = path.get(side, "field")
            if kind == "none":
                continue
            sign = 1 if side == "left" else -1
            edge = offset(path["poly"], path["closed"], sign * (path["width"] / 2 + 6))
            runs, cur, cur_on = [], [], None
            for i in range(0, len(edge), 2):
                q = edge[i]
                on = not any(dist_to_poly(q, op["poly"]) < op["width"] / 2 + 2 for n2, op in paths.items() if n2 != path["name"])
                if on != cur_on and cur:
                    runs.append((cur_on, cur)); cur = []
                cur_on = on; cur.append(q)
            if cur:
                runs.append((cur_on, cur))
            for on, run in runs:
                if not on or len(run) < 2:
                    continue
                if kind == "field":
                    v.line(run, FIELD + (55,), v.px(12)); v.line(run, FIELD + (230,), v.px(3))
                else:
                    acc = 0.0
                    for i in range(1, len(run)):
                        acc += math.dist(run[i - 1], run[i])
                        if acc >= 15:
                            acc = 0.0; x, y = v.P(run[i]); s = v.m(8)
                            v.d.rectangle((x - s, y - s, x + s, y + s), fill=VOXEL + (255,), outline=(87, 90, 82))

    for r in opt.get("rails", []):
        rail(v, r["pts"], r["rocks"], opt["seed"])
    for c, rad, n in opt.get("rocks", []):
        rock_field(v, c, rad, n, opt["seed"] + int(c[0]))
    for i, m in enumerate(opt.get("mines", [])):
        mine_field(v, m["c"], m["r"], m["n"], m["kind"], opt["seed"] * 31 + i)
    for b in opt.get("barricades", []):
        path = paths[b["path"]]; p, t = at(path, b["f"]); nx, ny = -t[1], t[0]; hw = path["width"] / 2 + 6
        a = (p[0] + nx * hw, p[1] + ny * hw); c = (p[0] - nx * hw, p[1] - ny * hw)
        v.line([a, c], BARRICADE + (255,), v.m(12))
        for j in range(5):
            u = (j + 0.5) / 5; q = (a[0] + (c[0] - a[0]) * u, a[1] + (c[1] - a[1]) * u); x, y = v.P(q)
            v.d.line([(x - v.m(4), y - v.m(5)), (x + v.m(4), y + v.m(5))], fill=(13, 14, 12, 255), width=v.px(2))
    for d in opt.get("doors", []):
        blast_door(v, paths[d["path"]], d["f"], d["switch"])
    for t in opt.get("turrets", []):
        turret(v, t["c"], t["facing"], t["reach"], t["arc"])
    for w in opt.get("washes", []):
        if w.get("heat"):
            d = (math.cos(math.radians(w["dir"])), math.sin(math.radians(w["dir"])))
            n = (-d[1], d[0]); o = w["o"]
            for i in range(6, 0, -1):
                L = w["length"] * i / 6; Wd = w["width"] * (0.4 + 0.6 * i / 6)
                v.poly([(o[0] + n[0] * 20, o[1] + n[1] * 20), (o[0] + d[0] * L + n[0] * Wd / 2, o[1] + d[1] * L + n[1] * Wd / 2),
                        (o[0] + d[0] * L - n[0] * Wd / 2, o[1] + d[1] * L - n[1] * Wd / 2), (o[0] - n[0] * 20, o[1] - n[1] * 20)],
                       fill=FLARE + (int(16 + 4 * (6 - i)),))
            v.dot(o, 8, FLARE + (255,))
        else:
            engine_wash(v, w["o"], w["dir"], w["length"], w["width"])
    for s in opt.get("sweepers", []):
        sweeper(v, s["hub"], s["length"], s["arms"], s["phase"], hub_r=s.get("hub_r", 60))
    for pl in opt.get("pylons", []):
        grapple_pylon(v, pl["c"], pl["reach"])
    for pt in opt.get("portals", []):
        path = paths[pt["path"]]; p, t = at(path, pt["f"])
        portal(v, p, math.degrees(math.atan2(t[1], t[0])), path["width"] + 20, pt["label"], f_small)
    if opt.get("portals"):
        a = at(paths[opt["portals"][0]["path"]], opt["portals"][0]["f"])[0]
        b = at(paths[opt["portals"][1]["path"]], opt["portals"][1]["f"])[0]
        mid = ((a[0] + b[0]) / 2 + 160, (a[1] + b[1]) / 2)
        v.line(make_path([a, mid, b], False)["poly"], GRAVITY + (150,), v.px(2), dash=(v.px(8), v.px(8)))
    for c in opt.get("canisters", []):
        canister(v, c)

    # gates and start grid
    for g in opt["gates"]:
        path = paths[g["path"]]; p, t = at(path, g["f"]); nx, ny = -t[1], t[0]; hw = path["width"] / 2 + 4
        a = (p[0] + nx * hw, p[1] + ny * hw); c = (p[0] - nx * hw, p[1] - ny * hw)
        col = GATE if g["required"] else BRANCH
        if g["id"] == 0:
            for j in range(10):
                u0, u1 = j / 10, (j + 1) / 10
                v.line([(a[0] + (c[0] - a[0]) * u0, a[1] + (c[1] - a[1]) * u0), (a[0] + (c[0] - a[0]) * u1, a[1] + (c[1] - a[1]) * u1)],
                       (GATE if j % 2 else (40, 40, 40)) + (255,), v.m(9))
        else:
            v.line([a, c], col + (255,), v.px(3))
        for q in (a, c):
            v.dot(q, 4, col + (255,))
        v.text((a[0] + nx * 24, a[1] + ny * 24), "S/F" if g["id"] == 0 else f"G{g['id']}", f_small, col + (255,))
    sp, st = at(paths["main"], opt["start_f"])
    for i in range(4):
        back = 45 + 34 * (i // 2); side = (-1 if i % 2 else 1) * 24
        q = (sp[0] - st[0] * back - st[1] * side, sp[1] - st[1] * back + st[0] * side)
        x, y = v.P(q); s = v.m(8)
        v.d.rectangle((x - s, y - s, x + s, y + s), outline=GATE + (220,), width=v.px(1))
    if key == "B":
        x, y = v.P((0, 0)); r = v.m(100)
        v.d.ellipse((x - r, y - r, x + r, y + r), outline=MINE + (200,), width=v.px(3))

    for name, path in paths.items():
        if name == "main" or "label" not in path:
            continue
        if "label_at" in path:
            v.text(path["label_at"], path["label"], f_lab, BRANCH + (255,)); continue
        p, t = at(path, path.get("label_f", 0.5)); nx, ny = -t[1], t[0]
        off = path.get("label_off", 1) * (path["width"] / 2 + 60)
        v.text((p[0] + nx * off, p[1] + ny * off), path["label"], f_lab, BRANCH + (255,))
    for i, (p, _) in enumerate(opt["sectors"]):
        callout(v, p, i + 1, font(F_BOLD, 15))

    # header and scale
    d = v.d
    d.text((60 * SS, 48 * SS), f"OPTION {key}  ·  {opt['name'].upper()}", font=font(F_TITLE, 36), fill=TEXT)
    d.text((60 * SS, 104 * SS), opt["tagline"], font=font(F_BODY, 21), fill=DIM)
    bx, by = 60 * SS, (H - STRIP_H - 30) * SS
    d.line([(bx, by), (bx + v.m(500), by)], fill=TEXT, width=3 * SS)
    for tk in (0, 250, 500):
        d.line([(bx + v.m(tk), by - 8 * SS), (bx + v.m(tk), by + 8 * SS)], fill=TEXT, width=2 * SS)
    d.text((bx + v.m(500) + 14 * SS, by), "500 m", font=font(F_BODY, 16), fill=TEXT, anchor="lm")

    # speed trace of every route
    sx0, sy0, sx1, sy1 = 60 * SS, (H - STRIP_H + 10) * SS, (W - PANEL_W - 40) * SS, (H - 30) * SS
    d.rectangle((sx0, sy0, sx1, sy1), fill=(10, 11, 10), outline=PANEL_LINE)
    longest = max(r["len"] for r in opt["_routes"])
    cols = [TEXT, BOOST, BRANCH, GRAVITY]
    for vv in (60, 120, 192):
        yy = sy1 - (sy1 - sy0) * vv / 210
        d.line([(sx0, yy), (sx1, yy)], fill=(40, 42, 38), width=SS)
        d.text((sx0 + 8 * SS, yy - 2 * SS), f"{vv} m/s", font=font(F_MONO, 12), fill=DIM, anchor="ls")
    for ri, r in enumerate(opt["_routes"]):
        vs, ds = r["trace"]; acc = 0.0; pts = []
        for i in range(0, len(vs), 3):
            pts.append((sx0 + (sx1 - sx0) * acc / longest, sy1 - (sy1 - sy0) * vs[i] / 210))
            acc += sum(ds[i:i + 3])
        d.line(pts, fill=cols[ri % 4] + (230,), width=2 * SS)
        d.text((sx1 - 10 * SS, sy0 + (14 + 20 * ri) * SS), f"{r['name']}  {r['time']:.0f} s", font=font(F_BOLD, 14), fill=cols[ri % 4], anchor="rm")
    d.text((sx0 + 260 * SS, sy0 - 10 * SS), "SPEED ALONG THE LAP  (clean line, fighter class)", font=font(F_BOLD, 13), fill=DIM, anchor="ls")

    # side panel
    px = (W - PANEL_W) * SS
    d.rectangle((px, 0, W * SS, H * SS), fill=PANEL)
    d.line([(px, 0), (px, H * SS)], fill=PANEL_LINE, width=2 * SS)
    y = [50 * SS]

    def head(t):
        d.text((px + 40 * SS, y[0]), t, font=font(F_TITLE, 17), fill=BOOST); y[0] += 36 * SS

    def para(t, size=16, col=TEXT, indent=0, f=F_BODY, width=None):
        for chunk in wrap(t, width or int(102 - indent / 5)):
            d.text((px + (40 + indent) * SS, y[0]), chunk, font=font(f, size), fill=col); y[0] += int(size * 1.42) * SS

    head("THE IDEA")
    para(opt["concept"], 16, TEXT)
    y[0] += 14 * SS
    head("ROUTES  (clean lap, fighter class)")
    for i, r in enumerate(opt["_routes"]):
        d.rectangle((px + 40 * SS, y[0] + 5 * SS, px + 52 * SS, y[0] + 17 * SS), fill=cols[i % 4])
        para(f"{r['name']}: {r['time']:.0f} s · {r['len'] / 1000:.2f} km · top {r['vmax']:.0f} m/s", 16, TEXT, 22, F_BOLD)
        para(r["note"], 15, DIM, 22)
    if opt.get("portal_saving"):
        para("Portal skip: about 9–12 s off either orbit when it is not jammed.", 15, GRAVITY, 22)
    para(f"3 laps ≈ {3 * opt['_routes'][0]['time'] / 60:.1f} min. The Dart racer is about 10% faster on a clean line.", 14, DIM)
    y[0] += 12 * SS
    head("SECTORS")
    for i, (_, txt) in enumerate(opt["sectors"]):
        yy = y[0]
        d.ellipse((px + 40 * SS, yy + 1 * SS, px + 64 * SS, yy + 25 * SS), outline=TEXT, width=2 * SS)
        d.text((px + 52 * SS, yy + 13 * SS), str(i + 1), font=font(F_BOLD, 13), fill=TEXT, anchor="mm")
        para(txt, 15, TEXT, 36, width=92)
        y[0] += 4 * SS
    y[0] += 10 * SS
    head("EVERY LAP IS DIFFERENT")
    for e in opt["events"]:
        para("•  " + e, 15, TEXT)
    y[0] += 10 * SS
    head("MECHANICS ON THIS TRACK")
    para("  ·  ".join(opt["mechanics"]), 15, DIM)
    d.text((px + 40 * SS, (H - 34) * SS), "RACE-002 · true scale · everything sits on the flight plane", font=font(F_BODY, 13), fill=DIM)

    out = os.path.join(OUT_IMG, f"track_option_{key.lower()}.png")
    os.makedirs(OUT_IMG, exist_ok=True)
    img.resize((W, H), Image.LANCZOS).save(out, optimize=True)
    return out


# ---------------------------------------------------------------- mechanics sheet
def ship(v, c, heading_deg, scale=1.0, col=TEXT):
    pts = [(28, 0), (-14, 12), (-8, 0), (-14, -12)]
    v.poly([rot((c[0] + x * scale, c[1] + y * scale), heading_deg, c) for x, y in pts], fill=col + (255,))


def tracer(v, a, b, col=TURRET):
    v.line([a, b], col + (230,), v.px(2), dash=(v.px(8), v.px(5)))


def card_track(v, pts, width=110, closed=False):
    p = make_path(pts, closed)
    v.line(p["poly"], TRACK_EDGE, v.m(width + 8)); v.line(p["poly"], TRACK, v.m(width))
    for s in (1, -1):
        v.line(offset(p["poly"], closed, s * (width / 2 + 5)), FIELD + (200,), v.px(2))
    return p


def mech_well(v):
    gravity_well(v, (0, -40), 90, 300)
    card_track(v, [(-420, 260), (-150, 230), (130, 180), (260, -20), (150, -230), (-150, -260), (-420, -240)], 90)
    ship(v, (230, 80), -70, 1.4); v.arrow((230, 80), (250, -30), BOOST + (255,), v.px(3))


def mech_sweeper(v):
    card_track(v, [(-420, -40), (0, -40), (420, -40)], 120)
    sweeper(v, (0, 150), 240, 2, -95, hub_r=45)
    ship(v, (-250, -40), 0, 1.4); ship(v, (230, -60), -20, 1.4, MINE)


def mech_door(v):
    p = card_track(v, [(-420, 0), (0, 0), (420, 0)], 90)
    structure(v, [(-60, -150), (300, -150), (300, -230), (-60, -230)], seed=3, plate=20)
    blast_door(v, dict(p, width=90), 0.62, (120, -190))
    ship(v, (-200, 0), 0, 1.4); ship(v, (-60, -300), 40, 1.4, BOOST); tracer(v, (-40, -280), (110, -195))


def mech_wash(v):
    card_track(v, [(-420, 0), (420, 0)], 140)
    engine_wash(v, (30, 0), 180, 330, 120, engine=False)
    ship(v, (60, 0), 0, 1.8); ship(v, (-180, 10), 0, 1.4, MINE)
    v.arrow((-200, 50), (-310, 50), MINE + (255,), v.px(3))


def mech_grapple(v):
    card_track(v, [(-400, 230), (150, 220), (330, 60), (150, -150), (-400, -170)], 100)
    grapple_pylon(v, (140, 40), 170)
    ship(v, (300, 80), -100, 1.4); v.line([(300, 80), (140, 40)], BRANCH + (255,), v.px(2))


def mech_mines(v):
    card_track(v, [(-430, 0), (430, 0)], 150)
    for i, (k, lab) in enumerate([("prox", "PROXIMITY"), ("repulse", "REPULSOR"), ("emp", "EMP"), ("seeker", "SEEKER"), ("gravity", "GRAVITY")]):
        x = -340 + i * 170
        mine(v, (x, 10), k, 2.2)
        v.text((x, -120), lab, font(F_BOLD, 12), TEXT + (255,))


def mech_turrets(v):
    card_track(v, [(-430, 60), (430, 60)], 120)
    structure(v, [(-150, -110), (150, -110), (150, -190), (-150, -190)], seed=6, plate=20)
    for x in (-80, 80):
        turret(v, (x, -110), 90, 300, 110)
    ship(v, (240, 60), 0, 1.5); ship(v, (60, 70), 0, 1.4, BOOST); ship(v, (-120, 50), 0, 1.4, BRANCH)
    tracer(v, (80, -95), (230, 55)); tracer(v, (-80, -95), (225, 50))
    v.text((240, 150), "LEADER", font(F_BOLD, 12), TEXT + (255,))


def mech_flare(v):
    region = [(-430, 260), (430, 260), (430, -260), (-430, -260)]
    flare_zone(v, region, 0, shadows=[[(-20, 60), (430, 80), (430, -60), (-20, -40)]])
    rock(v, (-60, 10), 55, random.Random(2))
    card_track(v, [(-420, -170), (420, -170)], 100)
    v.arrow((-470, 200), (-380, 200), FLARE + (255,), v.px(4))
    ship(v, (200, 10), 0, 1.4, GRAVITY); ship(v, (100, -170), 0, 1.4, MINE)


def mech_portal(v):
    card_track(v, [(-430, 150), (-120, 150)], 100); card_track(v, [(120, -150), (430, -150)], 100)
    portal(v, (-120, 150), 0, 120); portal(v, (120, -150), 0, 120)
    v.line(make_path([(-120, 150), (60, 40), (120, -150)], False)["poly"], GRAVITY + (160,), v.px(2), dash=(v.px(8), v.px(8)))
    ship(v, (-230, 150), 0, 1.4); ship(v, (230, -150), 0, 1.4)
    v.text((260, 60), "JAMMED 6 s", font(F_BOLD, 12), MINE + (255,))


def mech_canister(v):
    card_track(v, [(-430, 0), (430, 0)], 120)
    for x in (-60, 20, 100, 180):
        canister(v, (x, 0), 2.0)
    ship(v, (-220, 0), 0, 1.6)
    tracer(v, (-190, 30), (180, 20), BOOST)
    v.text((180, 110), "SHOOT ONE: IT BLOWS", font(F_BOLD, 12), BOOST + (255,))


def mech_rails(v):
    card_track(v, [(-430, -20), (430, -20)], 120)
    rail(v, [(60, 330), (40, 0), (20, -330)], [0.2, 0.42, 0.75], 4)
    ship(v, (-200, -20), 0, 1.4)


def mech_events(v):
    for i, (lab, col) in enumerate([("LAP 1", TEXT), ("LAP 2", BOOST), ("LAP 3", MINE)]):
        x0 = -420 + i * 290
        v.poly([(x0, 200), (x0 + 250, 200), (x0 + 250, -200), (x0, -200)], fill=(20, 22, 19, 255), outline=col + (200,))
        v.text((x0 + 125, 165), lab, font(F_BOLD, 14), col + (255,))
    structure(v, [(-400, 60), (-200, 60), (-200, -20), (-400, -20)], seed=1, plate=18)
    v.line([(-330, -120), (-270, -120)], BARRICADE + (255,), v.m(14))
    structure(v, [(-110, 60), (90, 60), (90, -20), (-110, -20)], seed=1, plate=18)
    turret(v, (-10, -20), 270, 120, 80)
    structure(v, [(180, 60), (380, 60), (380, -20), (180, -20)], seed=1, plate=18)
    v.poly([(190, -60), (370, -60), (370, -170), (190, -170)], fill=SLOW + (90,))
    v.text((-300, -170), "closed", font(F_BODY, 12), DIM + (255,)); v.text((-10, -170), "turrets wake", font(F_BODY, 12), DIM + (255,))
    v.text((280, -190), "lane floods", font(F_BODY, 12), DIM + (255,))


MECHANICS = [
    ("GRAVITY WELLS", mech_well, "small add to SPEC-91: a sixth zone kind, Attract",
     "Dive close to a moon and its pull whips you round faster; misjudge it and you hit the surface.",
     "Every planet, moon and anomaly pulls ships, debris and shots. Slingshot round a gas giant, or get dragged toward a black hole."),
    ("SWEEPER ARMS", mech_sweeper, "new: moving structures (voxel grids on scripted rails)",
     "Station arms and salvage cranes sweep across the track. Time the gap or get batted by real voxels.",
     "Rotating stations, shipyard cranes, mining rigs and defence rings really move. Dock with a spinning ring or slip through a closing yard."),
    ("BLAST DOORS + SWITCHES", mech_door, "new: mechanisms (trigger -> action)",
     "Shoot a switch to slam a door on a rival, or open the shortcut for yourself. Doors are voxels.",
     "Derelicts and stations have doors, airlocks and power switches. Shoot them to get in, lock pursuers out, or reach a hidden hold."),
    ("ENGINE WASH", mech_wash, "small add: a push zone behind every firing thruster",
     "Real plumes shove and heat what is behind them. Tailgate and get blasted; brake-check with reverse thrust.",
     "Every ship's thrusters push and burn what is behind them. Capital ships are dangerous to tail, and docking under one is a skill."),
    ("GRAPPLE PYLONS", mech_grapple, "new: tether constraint plus a racer ability",
     "Hook a pylon and swing round a hairpin without braking. Release early and you overshoot.",
     "A tractor tether: tow hulks home for salvage, drag cargo pods out of wrecks, anchor to a rock in a storm."),
    ("THE MINE FAMILY", mech_mines, "small add to SPEC-91: fuse variants on one module",
     "Proximity blasts. Repulsors only shove. EMPs cut thrusters for 1.5 s. Seekers drift at you. Gravity mines pull.",
     "Old minefields guard derelicts and choke points; pirates seed lanes; later you can lay your own behind you."),
    ("GAUNTLET TURRETS", mech_turrets, "exists: station turrets (AI-009) plus a 'target the leader' rule",
     "Neutral turrets fire at whoever leads. It is physical rubber-banding: every hit shoves the leader.",
     "Faction checkpoints, abandoned defence grids and station guns that react to your standing or to who is winning a fight."),
    ("SOLAR FLARES + SHADOW", mech_flare, "small add: timed zones plus a shadow test against voxels",
     "Every few seconds a flare scorches a sector and drains shields. Hide in a moon's or a rock's shadow.",
     "Hot stars flare. Cross the inner system in a planet's shadow; outrun a storm or shelter behind an asteroid."),
    ("LANE-GATE PORTALS", mech_portal, "new: needs an ADR (it moves a root's transform)",
     "Fly in and come out elsewhere, speed kept. The gate jams for 6 s after each use, so the leader denies it.",
     "Ancient in-system gates, cousins of the lane network, and part of the mystery: who built them, and where else do they go?"),
    ("ENERGY CANISTERS", mech_canister, "small add: a device grid that grants energy on contact",
     "Fly through to refill boost and abilities. Shoot one ahead of a rival: it explodes in their face.",
     "Fuel, power and loot floating in wreck fields; volatile cargo that blows when shot."),
    ("MOVING RAILS", mech_rails, "new: same system as sweeper arms",
     "Ore conveyors and orbiting debris move across the line on a beat. Shoot a rock off its rail.",
     "Asteroid streams, ore conveyors at mining stations and debris belts round planets, all moving on a beat."),
    ("PLACES THAT CHANGE", mech_events, "small add: race lap events drive mechanism actions",
     "Lap 2 wakes the turrets, lap 3 floods a lane. Wrecks and debris from earlier laps stay on the track.",
     "Places that change while you are there: a derelict wakes, a station locks down, a storm rolls in."),
]


def draw_mechanics():
    CW, CH, cols, rows, top = 790, 590, 4, 3, 170
    Wm, Hm = 60 * 2 + CW * cols + 30 * (cols - 1), top + rows * CH + (rows - 1) * 30 + 60
    img = Image.new("RGB", (Wm * SS, Hm * SS), BG)
    starfield(img, (0, 0, Wm * SS, Hm * SS), 99, density=1.6)
    d = ImageDraw.Draw(img, "RGBA")
    d.text((60 * SS, 48 * SS), "NEW MECHANICS  ·  GENERAL WORLD SYSTEMS", font=font(F_TITLE, 38), fill=TEXT)
    d.text((60 * SS, 108 * SS), "Twelve systems for exploring the galaxy. The race tracks are simply the first place they are put together.",
           font=font(F_BODY, 22), fill=DIM)
    for i, (title, fn, status, race, game) in enumerate(MECHANICS):
        cx, cy = 60 + (i % cols) * (CW + 30), top + (i // cols) * (CH + 30)
        x0, y0, x1, y1 = cx * SS, cy * SS, (cx + CW) * SS, (cy + CH) * SS
        card = Image.new("RGB", (x1 - x0, y1 - y0), (10, 11, 10))
        fn(View(card, (10 * SS, 60 * SS, card.width - 10 * SS, 330 * SS), (-460, -280, 460, 280)))
        img.paste(card, (x0, y0))
        d = ImageDraw.Draw(img, "RGBA")
        d.rectangle((x0, y0, x1, y1), outline=PANEL_LINE, width=2 * SS)
        d.text((x0 + 22 * SS, y0 + 18 * SS), f"{i + 1:02d}  {title}", font=font(F_TITLE, 17), fill=BOOST)
        yy = y0 + 345 * SS
        for lab, txt, col in (("EXPLORING THE GALAXY", game, TEXT), ("ON A RACE TRACK", race, TEXT), ("BUILT FROM", status, DIM)):
            d.text((x0 + 22 * SS, yy), lab, font=font(F_BOLD, 13), fill=DIM); yy += 22 * SS
            for chunk in wrap(txt, 78):
                d.text((x0 + 22 * SS, yy), chunk, font=font(F_BODY, 16), fill=col); yy += 23 * SS
            yy += 8 * SS
    out = os.path.join(OUT_IMG, "mechanics.png")
    img.resize((Wm, Hm), Image.LANCZOS).save(out, optimize=True)
    return out


def main():
    data = {}
    for key, opt in OPTIONS.items():
        build(opt)
        opt["_routes"] = [dict(r, **lap(opt, r["branches"], r.get("portal"))) for r in opt["routes"]]
        print(key, draw_track(opt, key))
        for r in opt["_routes"]:
            print(f"   {r['name']:<16} {r['time']:6.1f} s  {r['len']:7.0f} m  v {r['vmin']:.0f}-{r['vmax']:.0f}")
        keep = ("structures", "boost", "slow", "canisters", "mines", "rocks", "barricades", "doors", "turrets", "sweepers",
                "washes", "pylons", "wells", "orbits", "flare", "portals", "rails", "events", "start_f")
        data[key] = dict({k: opt[k] for k in keep if k in opt}, name=opt["name"],
                         paths={n: dict(points=p["points"], width=p["width"], closed=p["closed"],
                                        left=p.get("left"), right=p.get("right"), interior=p.get("interior", False))
                                for n, p in opt["_paths"].items()},
                         gates=[dict(g, position=at(opt["_paths"][g["path"]], g["f"])[0]) for g in opt["gates"]],
                         routes=[dict(name=r["name"], lap_seconds=round(r["time"], 1), metres=round(r["len"])) for r in opt["_routes"]])
    print(draw_mechanics())
    with open(OUT_JSON, "w") as f:
        json.dump(data, f, indent=1)
    print(OUT_JSON)


if __name__ == "__main__":
    main()
