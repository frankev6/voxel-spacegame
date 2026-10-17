#!/usr/bin/env python3
"""RACE-008 / RACE-013: extrude the six approved racer plans (racer_silhouettes.py) into voxel cells for the game.

Writes ArtSource/Racing/ships/<id>.json in the look-book format NocturneShipBuilder reads:
  cells: [x, y, z, tag, planes or null] in look-book axes (x right, y forward, z up), 0.5 m cells;
  mods:  [kind, min, rotation, size, size] module boxes (engines, guns, core).
Tags are "#NN" paint indices (DESIGN-SYSTEM §10.13), so each line keeps its livery.

The plan view is the approved silhouette. Heights: the hull is domed (taller along its middle, one cell at its
edges), plates and armour stand one cell proud, cabins two, struts and pylons are two cells thick, and the top
cells carry cut planes from the height gradient so the builder seats real slopes.

Run:  python3 ArtSource/Racing/export_racers.py
"""
import json
import math
import os

import racer_silhouettes as R

OUT = os.path.join(os.path.dirname(__file__), "ships")
CELL = 0.5

# livery: part kind -> paint index (DESIGN-SYSTEM §10.13, NOCTURNE column)
PAINT = {
    "vestari": dict(base=16, plate=8, frame=1, cab=11, glass=8, gun=1, rad=13, haz=8, num=16),
    "khuchad": dict(base=1, plate=6, frame=11, cab=16, glass=5, gun=11, rad=1, haz=5, num=6),
}
SHIPS = [
    ("ship_racer_needle", "NEEDLE", R.dart_needle, "vestari", 4),
    ("ship_racer_kite", "KITE", R.dart_kite, "vestari", 3),
    ("ship_racer_gimbal", "GIMBAL", R.dart_gimbal, "vestari", 4),
    ("ship_racer_battering_ram", "BATTERING RAM", R.ram_battering, "khuchad", 7),
    ("ship_racer_salvage_tug", "SALVAGE TUG", R.ram_tug, "khuchad", 6),
    ("ship_racer_hammerhead", "HAMMERHEAD", R.ram_hammer, "khuchad", 6),
]


def inside(p, poly):
    x, y, c = p[0], p[1], False
    for i in range(len(poly)):
        a, b = poly[i], poly[i - 1]
        if (a[1] > y) != (b[1] > y) and x < (b[0] - a[0]) * (y - a[1]) / (b[1] - a[1] + 1e-12) + a[0]:
            c = not c
    return c


def raster(poly):
    """Cells (i, j) whose centres lie inside a plan polygon; i along the ship (forward), j to port."""
    xs, ys = [p[0] for p in poly], [p[1] for p in poly]
    out = set()
    for i in range(int(math.floor(min(xs) / CELL)) - 1, int(math.ceil(max(xs) / CELL)) + 1):
        for j in range(int(math.floor(min(ys) / CELL)) - 1, int(math.ceil(max(ys) / CELL)) + 1):
            if inside(((i + 0.5) * CELL, (j + 0.5) * CELL), poly):
                out.add((i, j))
    return out


def centroid(poly):
    return sum(p[0] for p in poly) / len(poly), sum(p[1] for p in poly) / len(poly)


def export(ship_id, name, fn, line, hmax):
    ship = fn()
    pal = PAINT[line]
    base = set()
    layers = {}          # (i, j) -> list of (kind, part index)
    for idx, (kind, shape, kw) in enumerate(ship.parts):
        if kind in ("seam", "line", "num", "glow"):
            continue
        part = raster(shape)
        for c in part:
            layers.setdefault(c, []).append((kind, idx))
        # the hull dome takes the hull and every large plate, frame or cabin (prows, hammer plates, engine boxes)
        if kind == "base" or (kind in ("plate", "frame", "cab", "armor") and len(part) >= 30):
            base |= part
    # distance (in cells) from each base cell to the base edge, for the dome
    dist = {c: 0 for c in base if any((c[0] + di, c[1] + dj) not in base for di, dj in ((1, 0), (-1, 0), (0, 1), (0, -1)))}
    frontier = list(dist)
    while frontier:
        nxt = []
        for c in frontier:
            for di, dj in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                n = (c[0] + di, c[1] + dj)
                if n in base and n not in dist:
                    dist[n] = dist[c] + 1; nxt.append(n)
        frontier = nxt
    top = {c: min(float(hmax), 1.0 + d * 0.8) for c, d in dist.items()}   # dome height in cells

    cells = {}
    def put(i, j, k, tag, planes=None):
        cells[(i, j, k)] = (tag, planes)

    for (i, j), kinds in layers.items():
        ks = [k for k, _ in kinds]
        h = top.get((i, j), 0.0)
        if (i, j) in base:
            n_full = int(h)
            frac = h - n_full
            colour = pal["base"]
            if "plate" in ks or "haz" in ks:
                colour = pal["plate"] if "plate" in ks else pal["haz"]
            body = pal["base"] if "base" in ks else pal["plate"] if "plate" in ks else pal["frame"]
            for k in range(n_full):
                put(i, j, k, f"#{body}")
            # the top cell: plate/haz/frame/rad paint, sloped by the dome gradient
            gx = top.get((i + 1, j), h) - top.get((i - 1, j), h)
            gy = top.get((i, j + 1), h) - top.get((i, j - 1), h)
            gx, gy = gx * 0.5, gy * 0.5
            tagc = "#%d" % (pal["frame"] if "frame" in ks else pal["rad"] if "rad" in ks else colour)
            if frac > 0.2:
                # top surface z = frac + gx (forward - 0.5) + gy (port - 0.5), as a half-space n . p <= d
                # look-book cell axes: x right (= 1 - port), y forward, z up
                planes = [[[gy, -gx, 1.0], frac - 0.5 * gx + 0.5 * gy]]
                put(i, j, n_full, tagc, planes)
            elif n_full > 0:
                cells[(i, j, n_full - 1)] = (tagc, None)
            stack = max(n_full, 1)
            if "plate" in ks and "base" in ks:   # armour and plates stand one cell proud
                put(i, j, stack, f"#{pal['plate']}")
            if "cab" in ks:
                put(i, j, stack, f"#{pal['cab']}")
                put(i, j, stack + 1, f"#{pal['glass']}" if "glass" in ks else f"#{pal['cab']}")
        else:
            # parts off the hull: pylons, pods' plates, wings' fins, crane booms, barrels
            if "base" in ks:
                continue
            thick = 2 if any(k in ("frame", "plate", "crane", "cab") for k in ks) else 1
            z0 = 1
            tag = pal["plate"] if "plate" in ks else pal["frame"] if "frame" in ks else pal["rad"] if "rad" in ks else pal["gun"]
            if "gun" in ks:
                thick, z0, tag = 1, 1, pal["gun"]
            for k in range(z0, z0 + thick):
                put(i, j, k, f"#{tag}")

    # look-book axes: x right (= -port), y forward, z up
    out_cells = [[-j, i, k, tag, planes] for (i, j, k), (tag, planes) in sorted(cells.items())]

    # modules from glows: muzzle glows at gun tips become weapon boxes, the rest are engines
    mods = []
    guns = [shape for kind, shape, kw in ship.parts if kind == "gun"]
    for kind, shape, kw in ship.parts:
        if kind != "glow":
            continue
        cx, cy = centroid(shape)
        r = max(math.dist((cx, cy), p) for p in shape)
        muzzle = any(abs(cx - max(p[0] for p in g)) < 1.2 and min(p[1] for p in g) - 0.5 <= cy <= max(p[1] for p in g) + 0.5 for g in guns)
        i, j = int(math.floor(cx / CELL)), int(math.floor(cy / CELL))
        if muzzle:
            g = next(g for g in guns if abs(cx - max(p[0] for p in g)) < 1.2)
            gi = int(math.floor(min(p[0] for p in g) / CELL)) + 1          # the barrel's rear, on the hull
            z = int(top.get((gi, j), 1.0))
            mods.append(["gatling", [-j - 1, gi, z], [[1, 0, 0], [0, 1, 0], [0, 0, 1]], [2, 3, 2], [2, 3, 2]])
        elif fn is R.dart_gimbal and math.hypot(cx, cy) > 6.0:
            # ring pods: a tall thin box just outside the ring, so its open face (the nozzle) points out of the ring in the plane
            ux, uy = cx / math.hypot(cx, cy), cy / math.hypot(cx, cy)
            pi, pj = int(math.floor(ux * 8.0 / CELL)), int(math.floor(uy * 8.0 / CELL))
            if abs(ux) >= abs(uy):   # facing forward or aft (look-book ±y): 2 wide, 1 deep, 3 tall
                mods.append(["rcs", [-pj - 1, pi, 0], [[1, 0, 0], [0, 1, 0], [0, 0, 1]], [2, 1, 3], [2, 1, 3]])
            else:                    # facing port or starboard (look-book ∓x): 1 deep, 2 long, 3 tall
                mods.append(["rcs", [-pj, pi - 1, 0], [[1, 0, 0], [0, 1, 0], [0, 0, 1]], [1, 2, 3], [1, 2, 3]])
        else:
            big = r > 0.9
            w, L, hgt = (4, 6, 4) if big else (2, 3, 2)
            mods.append(["thruster_l" if big else "thruster_s", [-j - w // 2, i + 1, 0 if big else 1],
                         [[1, 0, 0], [0, 1, 0], [0, 0, 1]], [w, L, hgt], [w, L, hgt]])
    # no core module: RacerShipBuilder seats the core and a reactor where the hull is thickest (NocturneShipBuilder)
    os.makedirs(OUT, exist_ok=True)
    with open(os.path.join(OUT, ship_id + ".json"), "w") as f:
        json.dump({"id": ship_id, "name": name, "cells": out_cells, "mods": mods}, f, separators=(",", ":"))
    kinds = {}
    for m in mods:
        kinds[m[0]] = kinds.get(m[0], 0) + 1
    print(f"{ship_id:28s} {len(out_cells):6d} cells  mods {kinds}")


if __name__ == "__main__":
    for args in SHIPS:
        export(*args)
