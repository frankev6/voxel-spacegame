#!/usr/bin/env python3
"""RACE-002 revision: the chosen track, a battle graveyard (owner, 2026-10-17).

"instead of just having one ship, its a group of derelict ships, like an ancient battle took place with large ships
and we go through and around them." No grapple pylons (cut by the owner).

Run:  python3 ArtSource/Racing/track_graveyard.py
Out:  docs/design/racing/img/track_01_battle_graveyard.png, docs/design/racing/track_01_battle_graveyard.json
"""
import json
import math
import os

import track_layouts as T
from racing_common import ROOT, wreck, at

OUT_JSON = os.path.join(ROOT, "docs", "design", "racing", "track_01_battle_graveyard.json")


def along(a, b, s, w):
    L = math.dist(a, b); u = ((b[0] - a[0]) / L, (b[1] - a[1]) / L); n = (-u[1], u[0])
    return (a[0] + u[0] * s * L + n[0] * w, a[1] + u[1] * s * L + n[1] * w)


# The dreadnought, broken in two: one 1.5 km hull, bow half drifting (the Break narrows every lap)
DN_A, DN_B = (-260, 110), (1180, 760)
def dn_w(s):
    if s > 0.86:
        return 185 * max(0.2, 1 - (s - 0.86) / 0.14 * 0.85)
    if 0.30 < s < 0.40:
        return 235          # bridge tower sponsons
    return 185 if s > 0.04 else 150
STERN_HALF = wreck(DN_A, DN_B, 0.0, 0.52, dn_w, jag1=55, seed=3)
BOW_HALF = wreck(DN_A, DN_B, 0.61, 1.0, dn_w, jag0=55, seed=4)
GAP = along(DN_A, DN_B, 0.565, 0)

# The carrier, lying north-south with its flight deck open end to end (the hangar run)
CV_A, CV_B = (-1330, -560), (-1290, 620)
def cv_w(s):
    return 125 if 0.05 < s < 0.92 else 95
CARRIER = wreck(CV_A, CV_B, 0.0, 1.0, cv_w, seed=7)
CV_ISLAND = [along(CV_A, CV_B, s, w) for s, w in ((0.55, -125), (0.55, -185), (0.72, -185), (0.72, -125))]

# The battleship along the south side, bow west, guns on its south flank, reactor breached on the north
BB_A, BB_B = (690, -600), (-690, -560)
def bb_w(s):
    if s > 0.88:
        return 105 * max(0.25, 1 - (s - 0.88) / 0.12 * 0.75)
    return 105
BATTLESHIP = wreck(BB_A, BB_B, 0.0, 1.0, bb_w, jag0=0, seed=9)
BB_BLISTERS = [[along(BB_A, BB_B, s, w) for s, w in ((s0, 105), (s0, 150), (s0 + 0.06, 150), (s0 + 0.06, 105))] for s0 in (0.18, 0.40, 0.62)]
ENGINE_SECTION = wreck((560, -1590), (180, -1600), 0.0, 1.0, lambda s: 70, jag1=40, seed=11)
REACTOR = along(BB_A, BB_B, 0.5, -115)   # north side (the axis normal points south)

TRACK = dict(
    name="Battle Graveyard", title="TRACK 01  ·  BATTLE GRAVEYARD", seed=19,
    tagline="An ancient fleet action, frozen. The lap runs between, around and straight through its dead capital ships.",
    concept="Two fleets fought here over an ancient lane-gate and nobody won. A dreadnought lies broken in two, and its "
            "drifting bow closes the gap between the halves a little every lap. A carrier's flight deck is open end to "
            "end, a hangar run with bulkheads and a blast door. A battleship lies along the south side: its guns still "
            "cover the south straight, and its breached reactor pulses across the keel line on its north side. A salvage "
            "rig working the wrecks sweeps the outer corner, the battleship's broken-off engines still fire, and the old "
            "minefields were never cleared. The gate the fleets died for still works.",
    paths={
        "main": dict(closed=True, width=140, left="field", right="voxel", points=[
            (-300, 1015), (150, 1030), (850, 1250), (1550, 1500), (2150, 1250), (2280, 550), (2050, -150), (1800, -560), (1500, -800), (1060, -1080),
            (400, -1290), (-400, -1290), (-1050, -1000), (-1500, -700), (-1850, -250), (-1800, 450), (-1400, 900), (-700, 995)]),
        "breach": dict(width=70, left="voxel", right="voxel", label="THE BREAK", label_f=0.36, label_off=1.6,
                       from_p=(-150, 1020), to_p=(1800, -560), lead=150,
                       inner=[along(DN_A, DN_B, 0.565, 300), GAP, along(DN_A, DN_B, 0.565, -320), (1500, -150)]),
        "hangar": dict(width=60, interior=True, left="voxel", right="voxel", label="FLIGHT DECK", label_f=0.5, label_off=-3.2,
                       from_p=(-1150, -905), to_p=(-560, 1003), lead=50,
                       inner=[along(CV_A, CV_B, -0.1, 0), along(CV_A, CV_B, 0.5, 0), along(CV_A, CV_B, 1.08, 0), (-1080, 900)]),
        "keel": dict(width=80, left="field", right="field", label="KEEL LINE", label_f=0.5, label_off=1.4,
                     from_p=(1800, -560), to_p=(-1500, -700), lead=60,
                     inner=[(1500, -560), (1100, -450), (700, -390), along(BB_A, BB_B, 0.5, -215), (-720, -390), (-1150, -620)]),
    },
    structures=[dict(pts=[(1830, 1640), (1970, 1640), (1970, 1760), (1830, 1760)], seed=41)],
    wrecks=[dict(a=DN_A, b=DN_B, kind="dreadnought", seed=31, cut=(0.0, 0.52)),
            dict(a=DN_A, b=DN_B, kind="dreadnought", seed=32, cut=(0.61, 1.0)),
            dict(a=CV_A, b=CV_B, kind="carrier", seed=33),
            dict(a=BB_A, b=BB_B, kind="battleship", seed=35),
            dict(a=(365, -1530), b=(365, -3030), kind="battleship", seed=40, cut=(0.0, 0.12)),
            dict(a=(1350, 650), b=(1800, 300), kind="cruiser", seed=42),
            dict(a=(2280, -760), b=(2460, -160), kind="cruiser", seed=43, cut=(0.0, 0.62)),
            dict(a=(-900, 300), b=(-600, 560), kind="frigate", seed=44),
            dict(a=(-620, 280), b=(-880, 560), kind="frigate", seed=45),
            dict(a=(-520, -140), b=(-170, -250), kind="frigate", seed=46)],
    reactors=[dict(c=REACTOR, r=420, shadows=[
        [along(BB_A, BB_B, 0.43, -118), along(BB_A, BB_B, 0.47, -118), along(BB_A, BB_B, 0.38, -520), along(BB_A, BB_B, 0.30, -520)],
        [along(BB_A, BB_B, 0.55, -118), along(BB_A, BB_B, 0.58, -118), along(BB_A, BB_B, 0.72, -520), along(BB_A, BB_B, 0.64, -520)]]),
    ],
    sweepers=[dict(hub=(1900, 1700), length=520, arms=2, phase=-150, hub_r=70)],
    drifts=[dict(c=along(DN_A, DN_B, 0.8, 0), r=330, a0=200, a1=240)],
    washes=[dict(o=(310, -1520), dir=90, length=360, width=140), dict(o=(420, -1520), dir=90, length=360, width=140)],
    turrets=[dict(c=along(BB_A, BB_B, s, 112), facing=-92, reach=680, arc=100) for s in (0.2, 0.42, 0.64)],
    doors=[dict(path="hangar", at_p=along(CV_A, CV_B, 0.88, 0), switch=along(CV_A, CV_B, 0.78, 135))],
    barricades=[dict(path="hangar", at_p=along(CV_A, CV_B, 0.25, 0)), dict(path="hangar", at_p=along(CV_A, CV_B, 0.55, 0))],
    portals=[dict(path="main", at_p=(2100, -60), label="GATE IN"), dict(path="main", at_p=(860, -1035), label="GATE OUT")],
    boost=[dict(path="main", p=((-250, 1016), (300, 1040))), dict(path="main", p=((-1640, -150), (-1615, 380)))],
    slow=[],
    canisters=[along(DN_A, DN_B, 0.565, 190), along(DN_A, DN_B, 0.565, 120), along(DN_A, DN_B, 0.565, -150),
               (-200, 1012), (-120, 1015), (-40, 1018), (-640, -420), (-560, -420)],
    mines=[dict(c=(1480, -800), r=150, n=12, kind="prox"), dict(c=(1280, -900), r=100, n=6, kind="seeker"),
           dict(c=(-1470, 860), r=70, n=7, kind="emp"), dict(c=(-950, -500), r=60, n=5, kind="emp"),
           dict(c=(240, 1060), r=30, n=2, kind="repulse")],
    rocks=[(along(DN_A, DN_B, 0.565, 420), 110, 10), (along(DN_A, DN_B, 0.565, -430), 120, 12), ((-1640, 0), 90, 8),
           ((1700, 700), 200, 16), ((-300, 300), 160, 12)],
    start_p=(-300, 1015),
    gates=[dict(id=0, path="main", at_p=(-300, 1015)), dict(id=1, path="main", at_p=(600, 1120)),
           dict(id=2, path="main", at_p=(2150, 1250)), dict(id=3, path="main", at_p=(2270, 400)),
           dict(id=4, path="main", at_p=(1250, -950)), dict(id=5, path="main", at_p=(600, -1240)),
           dict(id=6, path="main", at_p=(-200, -1300)), dict(id=7, path="main", at_p=(-1250, -820)),
           dict(id=8, path="main", at_p=(-1640, 100)), dict(id=9, path="main", at_p=(-1150, 925)),
           dict(id=10, path="breach", f=0.5), dict(id=11, path="hangar", f=0.5), dict(id=12, path="keel", f=0.5),
           dict(id=13, path="main", at_p=(1730, -640)), dict(id=14, path="main", at_p=(-1080, -985)), dict(id=15, path="main", at_p=(-450, 1008))],
    routes=[dict(name="Outer line", branches=[], note="Salvage-rig arms, minefields, the south straight under the guns"),
            dict(name="The Break", branches=["breach"], note="Through the broken dreadnought; 130 m wide on lap 1, 90 m on lap 2, 50 m on lap 3"),
            dict(name="Flight deck", branches=["hangar"], note="Inside the carrier; two bulkheads, and a blast door switched from outside"),
            dict(name="Keel line", branches=["keel"], note="Out of the guns' arcs, but across the reactor's pulses; ride the shadows"),
            dict(name="Break + keel", branches=["breach", "keel"], note="The fastest clean lap, through both hazards")],
    sectors_old=[
        ((-200, -1420), "Start under the battleship. From lap 2 its guns wake and target the leader down the whole south straight."),
        ((365, -1760), "Its broken-off engine section lies south of the track and still fires across it, one drive at a time."),
        ((-1250, -1060), "The split: outer line round the west, or the carrier's flight deck."),
        (along(CV_A, CV_B, 0.40, -260), "Flight deck: two bulkheads to shoot open (they stay open), then a blast door. Its switch faces the outer line."),
        ((-1560, 1010), "EMP mines on the north-west apex: thrusters cut for 1.5 s, and you drift on carry into the wall."),
        ((-100, 1150), "Boost and canisters down the north straight; repulsors at the Break entry shove you off line."),
        (along(DN_A, DN_B, 0.565, 520), "The Break: through the gap between the dreadnought's halves. The bow drifts, so the gap closes every lap."),
        ((1760, 1700), "A salvage rig still works the wrecks. Its arms sweep the outer corner."),
        ((2330, -100), "The ancient lane-gate the fleets fought over: skip the minefield, but it jams 6 s after each use."),
        ((1560, -1000), "The old minefield, never cleared: proximity mines and seekers that drift toward you."),
        (along(BB_A, BB_B, 0.5, -560), "Keel line: out of the guns' arcs, across the breached reactor. It pulses every 8 s; the shadows behind fallen bulkheads are safe."),
        ((-900, -360), "EMP mines and canisters where the keel line runs out past the carrier."),
    ],
    events=["Lap 1: the Break is 130 m wide; flight-deck bulkheads are closed; the battleship is quiet.",
            "Lap 2: the battleship's guns wake; the Break narrows to 90 m; the rig speeds up.",
            "Lap 3: the reactor pulses every 4 s; the Break is 50 m; wrecks of fallen racers stay on the track."],
    mechanics=["Moving structures (drifting bow, rig arms)", "Bulkheads", "Blast door + switch", "Gauntlet turrets",
               "Reactor pulse + shadows", "Engine wash", "Proximity, seeker, EMP and repulsor mines", "Lane-gate portal",
               "Energy canisters", "Field walls"],
    texts=[(along(DN_A, DN_B, 0.25, 260), "DREADNOUGHT (stern half)"), (along(DN_A, DN_B, 0.86, 230), "BOW HALF"),
           (along(BB_A, BB_B, 0.84, 190), "BATTLESHIP"), ((620, -1640), "ENGINE SECTION"), (along(CV_A, CV_B, 0.06, 230), "CARRIER"),
           ((1580, 220), "CRUISER"), ((-740, 640), "FRIGATES, RAMMED"), ((2160, -560), "BURNED-OUT CRUISER")],
)
TRACK["sectors"] = [TRACK["sectors_old"][i] for i in (5, 6, 7, 8, 9, 10, 11, 0, 1, 2, 3, 4)]
TRACK["sectors"][0] = ((-100, 1150), "Start and finish on the north straight: boost and canisters, repulsors at the Break entry.")
TRACK["sectors"][7] = ((-200, -1420), "The battleship's guns cover the whole south straight; from lap 2 they wake and target the leader.")
TRACK["routes"].append(dict(name="Gate + outer", branches=[], portal="portals", note="Skips the minefield when the gate is not jammed"))


def main():
    T.build(TRACK)
    TRACK["_routes"] = [dict(r, **T.lap(TRACK, r["branches"], r.get("portal"))) for r in TRACK["routes"]]
    out = T.draw_track(TRACK, "01")
    print(out)
    os.replace(out, os.path.join(os.path.dirname(out), "track_01_battle_graveyard.png"))
    for r in TRACK["_routes"]:
        print(f"   {r['name']:<14} {r['time']:6.1f} s  {r['len']:7.0f} m  v {r['vmin']:.0f}-{r['vmax']:.0f}")
    data = dict(name=TRACK["name"], start_f=TRACK["start_f"],
                paths={n: dict(points=p["points"], width=p["width"], closed=p["closed"], left=p.get("left"), right=p.get("right"),
                               interior=p.get("interior", False)) for n, p in TRACK["_paths"].items()},
                wrecks=dict(dreadnought=dict(axis=[DN_A, DN_B], stern=STERN_HALF, bow=BOW_HALF), carrier=dict(axis=[CV_A, CV_B], hull=CARRIER),
                            battleship=dict(axis=[BB_A, BB_B], hull=BATTLESHIP), engine_section=ENGINE_SECTION),
                gates=[dict(g, position=at(TRACK["_paths"][g["path"]], g["f"])[0]) for g in TRACK["gates"]],
                **{k: TRACK[k] for k in ("reactors", "sweepers", "drifts", "washes", "turrets", "doors", "barricades", "portals",
                                         "boost", "canisters", "mines", "rocks", "events")},
                routes=[dict(name=r["name"], lap_seconds=round(r["time"], 1), metres=round(r["len"])) for r in TRACK["_routes"]])
    with open(OUT_JSON, "w") as f:
        json.dump(data, f, indent=1)


if __name__ == "__main__":
    main()
