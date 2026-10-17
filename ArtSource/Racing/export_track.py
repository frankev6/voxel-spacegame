#!/usr/bin/env python3
"""RACE-004: export Track 01 (the Battle Graveyard) as the runtime layout TrackBuilder turns into track_01.asset.

Writes ArtSource/Racing/tracks/track_01.json, in metres on the flight plane (x east, z north; the map's y is z):
  paths    dense centrelines (about every 12 m) with corridor widths;
  walls    field-wall polylines along every corridor edge, cut open wherever another corridor runs (junctions open);
  gates    position, yaw (radians, 0 = +z, clockwise from above), width, required, next (the gate graph from every route);
  boost    strips as boxes along the line;
  start    four grid slots behind gate 0;
  wrecks   hull outlines with kind, axis and cut (WreckBuilder extrudes them at 2 m cells);
  set pieces (mines, canisters, rocks, reactors, turrets, ...) carried through for later tasks.

Run:  python3 ArtSource/Racing/export_track.py
"""
import itertools
import json
import math
import os

import track_graveyard as G
import track_layouts as T
from racing_common import at, point_in_poly

OUT = os.path.join(os.path.dirname(__file__), "tracks", "track_01.json")
STEP = 12.0          # centreline sample spacing, m
WALL_RADIUS = 3.0    # field-wall capsule radius, m


def resample(poly, closed, step=STEP):
    pts = list(poly) + ([poly[0]] if closed else [])
    out = [pts[0]]; carry = 0.0
    for a, b in zip(pts, pts[1:]):
        d = math.dist(a, b)
        t = step - carry
        while t <= d:
            out.append((a[0] + (b[0] - a[0]) * t / d, a[1] + (b[1] - a[1]) * t / d)); t += step
        carry = d - (t - step)
    if closed and math.dist(out[-1], out[0]) < step * .5:
        out.pop()
    if not closed and out[-1] != pts[-1]:
        out.append(pts[-1])
    return out


def dist_seg(p, a, b):
    abx, aby = b[0] - a[0], b[1] - a[1]; L2 = abx * abx + aby * aby
    u = 0 if L2 == 0 else max(0, min(1, ((p[0] - a[0]) * abx + (p[1] - a[1]) * aby) / L2))
    return math.hypot(p[0] - a[0] - abx * u, p[1] - a[1] - aby * u)


def dist_poly(p, poly, closed):
    n = len(poly)
    return min(dist_seg(p, poly[i], poly[(i + 1) % n]) for i in range(n if closed else n - 1))


def yaw_of(t):
    """Yaw (radians) whose forward (sin, cos) is the tangent t = (dx, dz)."""
    return math.atan2(t[0], t[1])


def main():
    track = G.TRACK
    T.build(track)
    track["_routes"] = [dict(r, **T.lap(track, r["branches"], r.get("portal"))) for r in track["routes"]]
    os.remove(T.draw_track(track, "01"))   # drawing computes each wreck's outline (w["_outline"]); the image is not kept
    paths = track["_paths"]

    dense = {n: resample(p["poly"], p["closed"]) for n, p in paths.items()}

    # walls: both edges of each corridor; a point inside another corridor is a junction, so the wall opens there
    walls = []
    for name, p in paths.items():
        half = p["width"] / 2
        poly = dense[name]
        n = len(poly)
        for side in (1, -1):
            run = []
            for i in range(n + (1 if p["closed"] else 0)):
                q, a, b = poly[i % n], poly[(i - 1) % n if (i > 0 or p["closed"]) else i], poly[(i + 1) % n if (i < n - 1 or p["closed"]) else i]
                tx, ty = b[0] - a[0], b[1] - a[1]; L = max(1e-6, math.hypot(tx, ty))
                e = (q[0] - ty / L * half * side, q[1] + tx / L * half * side)
                open_here = any(o != name and dist_poly(e, dense[o], paths[o]["closed"]) < paths[o]["width"] / 2 - 2 for o in paths)
                if open_here:
                    if len(run) > 1: walls.append(run)
                    run = []
                else:
                    run.append((round(e[0], 2), round(e[1], 2)))
            if len(run) > 1: walls.append(run)

    # every route: the main loop with any set of non-overlapping branches, with or without the portal skip
    branches = [n for n in paths if n != "main"]
    joins = {b: paths[b]["join"] for b in branches}
    portal = (track["portals"][0]["f"], track["portals"][1]["f"])
    spans = [(b, joins[b]) for b in branches] + [(None, portal)]
    routes = []
    for k in range(len(spans) + 1):
        for combo in itertools.combinations(spans, k):
            iv = sorted(c[1] for c in combo)
            if all(iv[i][1] <= iv[i + 1][0] for i in range(len(iv) - 1)):
                routes.append(combo)

    gates = track["gates"]
    def route_gates(combo):
        order = []
        for g in gates:
            if g["path"] == "main":
                if not any(a < g["f"] < b for _, (a, b) in combo):
                    order.append((g["f"], g["id"]))
            elif any(br == g["path"] for br, _ in combo):
                a, b = joins[g["path"]]
                order.append((a + (b - a) * g["f"] * .999 + 1e-6, g["id"]))   # a branch gate sits inside its span on the main line
        return [gid for _, gid in sorted(order)]
    nxt = {g["id"]: set() for g in gates}
    on_all = None
    for combo in routes:
        seq = route_gates(combo)
        for i, gid in enumerate(seq):
            nxt[gid].add(seq[(i + 1) % len(seq)])
        on_all = set(seq) if on_all is None else on_all & set(seq)

    out_gates = []
    for g in gates:
        pos, tan = at(paths[g["path"]], g["f"])
        out_gates.append(dict(id=g["id"], path=g["path"], position=[round(pos[0], 2), round(pos[1], 2)], yaw=round(yaw_of(tan), 5),
                              width=paths[g["path"]]["width"], required=g["id"] in on_all, next=sorted(nxt[g["id"]])))

    # the start grid: four slots in two columns behind gate 0, facing along the line
    main = paths["main"]
    start = []
    for k in range(4):
        f = -(60 + 45 * (k // 2)) / main["len"]
        pos, tan = at(main, f)
        nrm = (-tan[1], tan[0]); off = 22 if k % 2 == 0 else -22
        start.append(dict(position=[round(pos[0] + nrm[0] * off, 2), round(pos[1] + nrm[1] * off, 2)], yaw=round(yaw_of(tan), 5)))

    boost = []
    for z in track["boost"]:
        a, b = at(paths[z["path"]], z["f"][0])[0], at(paths[z["path"]], z["f"][1])[0]   # on the line (a design point can sit off it)
        boost.append(dict(center=[(a[0] + b[0]) / 2, (a[1] + b[1]) / 2], yaw=round(yaw_of((b[0] - a[0], b[1] - a[1])), 5),
                          length=round(math.dist(a, b), 1), width=paths[z["path"]]["width"] * .8))

    wrecks = [dict(kind=w["kind"], a=list(w["a"]), b=list(w["b"]), cut=list(w.get("cut") or (0, 1)), seed=w.get("seed", 0),
                   outline=[[round(q[0], 1), round(q[1], 1)] for q in w["_outline"]]) for w in track["wrecks"]]
    interiors = [n for n, p in paths.items() if p.get("interior")]

    data = dict(id="track_01", name="Battle Graveyard", laps=3, wall_radius=WALL_RADIUS,
                paths={n: dict(points=[[round(q[0], 2), round(q[1], 2)] for q in dense[n]], width=p["width"], closed=p["closed"],
                               interior=p.get("interior", False)) for n, p in paths.items()},
                walls=walls, gates=out_gates, start=start, boost=boost, wrecks=wrecks, interiors=interiors,
                **{k: track[k] for k in ("mines", "canisters", "rocks", "reactors", "turrets", "doors", "barricades", "portals",
                                         "sweepers", "drifts", "washes")})
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w") as f:
        json.dump(data, f, separators=(",", ":"))
    print(OUT, f"{len(walls)} walls, {sum(len(w) for w in walls)} wall points, {len(routes)} routes, required {sorted(on_all)}")
    for g in out_gates:
        print(f"  gate {g['id']:2d} {g['path']:7s} req={g['required']!s:5s} next={g['next']}")


if __name__ == "__main__":
    main()
