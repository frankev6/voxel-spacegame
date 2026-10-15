"""Export look-book NOCTURNE ship designs as 0.5 m cells for the game's blueprint builder (QA-001).

Owner: "make a new frigate that aligns with the new versions we've been working on, and make a larger cruise one based on
the designs we planed for nocture, i want to see how we can handle bigger ships".
  WRAITH   the NOCTURNE frigate as rebuilt on the slope kit (ld_slopes.wraith_rebuilt, kit k3 = the game's ADR-0018 kit:
           cube, 45-degree wedge, hip corner, long slope 1x2), ~28 m;
  REQUIEM  the NOCTURNE heavy cruiser (ld_capships.nocturne_cruiser), ~115 m, built on kit k3 the same way.
Big blocks are split back into cells and every cut cell is snapped to one k3 piece, so each cell maps to one game block.
Own work: the designs and generator are ours (ArtSource/LookDev); nothing is imported.

Run inside Blender (ld_ships needs mathutils):
    /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python ArtSource/Bricks/export_lookbook_ships.py

Writes ArtSource/Bricks/ships/<id>.json:
    cells  [x, y, z, tag, planes | null]   look-book axes (x right, y forward, z up); planes are the cell-local half-spaces
                                           n . p <= d with p in [0, 1]^3, null for a full cube
    mods   [kind, min cell, rotation matrix rows, size, rotated size]
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, os.path.join(ROOT, "ArtSource", "LookDev"))
import ld_capships  # noqa: E402
import ld_slopes  # noqa: E402

KIT = ["w11", "hip", "s12"]
OUT = os.path.join(HERE, "ships")


def export(ship_id, s):
    for B in list(s.big.keys()):
        s._split(B)
    O, oidx, opl = ld_slopes.oriented(ld_slopes.option_pieces(KIT))
    ld_slopes.quantize(s, list(range(int(oidx.max()) + 1)), O, oidx, opl)
    cells = []
    for c, tag in sorted(s.cells.items()):
        shape = s.shapes.get(c)
        planes = [[[float(v) for v in n], float(d)] for n, d in shape[1]] if shape else None
        cells.append([int(c[0]), int(c[1]), int(c[2]), str(tag), planes])
    mods = [[kind, [int(v) for v in mn], [[float(v) for v in row] for row in Rm], [int(v) for v in size], [int(v) for v in rsz]]
            for kind, mn, Rm, size, rsz, _ in s.mods]
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, ship_id + ".json")
    with open(path, "w") as f:
        json.dump({"id": ship_id, "name": s.name, "cells": cells, "mods": mods}, f, separators=(",", ":"))
    cut = sum(1 for c in cells if c[4])
    print("EXPORT %s: %d cells (%d cut), %d modules -> %s" % (ship_id, len(cells), cut, len(mods), path))


def main():
    export("ship_nocturne_wraith", ld_slopes.wraith_rebuilt("k3"))
    requiem, exact, total = ld_slopes.option_ship(ld_capships.nocturne_cruiser, KIT)
    print("REQUIEM on kit k3: %d of %d cut blocks exact" % (exact, total))
    export("ship_nocturne_requiem", requiem)


main()
