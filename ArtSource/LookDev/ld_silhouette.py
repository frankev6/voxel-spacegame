"""Plan, side and front silhouettes of look-book Ships, for judging outlines before a 3D build (owner: silhouettes first).

Run inside Blender (ld_ships needs mathutils):
    blender -b --factory-startup --python ArtSource/LookDev/ld_silhouette.py -- <out.png> [module:function ...]
Default: the NOCTURNE cruiser options next to the WRAITH, all at the same scale (4 px per 0.5 m cell).
Each view is a height-shaded projection coloured by paint tag, with module boxes in red.
"""
import importlib
import os
import sys
import zlib
import struct

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

PX = 4
TAG_RGB = {"hull": (205, 207, 200), "hull2": (196, 160, 98), "armor": (110, 116, 122), "trim": (52, 56, 62),
           "accent": (200, 40, 40), "mod": (170, 60, 60)}
BG = (10, 15, 26)


def _png(path, img):
    h, w, _ = img.shape
    raw = b"".join(b"\x00" + img[y].tobytes() for y in range(h))
    def chunk(t, d):
        return struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d) & 0xFFFFFFFF)
    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0))
                + chunk(b"IDAT", zlib.compress(raw, 6)) + chunk(b"IEND", b""))


def cells_of(s):
    out = {c: t for c, t in s.cells.items()}
    for kind, mn, Rm, size, rsz, _ in s.mods:
        for dx in range(rsz[0]):
            for dy in range(rsz[1]):
                for dz in range(rsz[2]):
                    out[(mn[0] + dx, mn[1] + dy, mn[2] + dz)] = "mod"
    return out


def view(cells, axes, depth_axis, flip_depth=False):
    """Project onto two axes; the visible cell is the nearest along depth_axis. Returns an RGB image and its origin."""
    pts = np.array(list(cells.keys()))
    lo, hi = pts.min(0), pts.max(0)
    a, b = axes
    W, H = hi[a] - lo[a] + 1, hi[b] - lo[b] + 1
    best = {}
    for c, t in cells.items():
        k = (c[a] - lo[a], c[b] - lo[b])
        d = c[depth_axis] * (-1 if flip_depth else 1)
        if k not in best or d > best[k][0]:
            best[k] = (d, t)
    ds = [v[0] for v in best.values()]
    dmin, dmax = min(ds), max(ds)
    img = np.zeros((H * PX, W * PX, 3), np.uint8)
    img[:] = BG
    for (i, j), (d, t) in best.items():
        k = 0.55 + 0.45 * (d - dmin) / max(1, dmax - dmin)
        col = np.array(TAG_RGB.get(t, (200, 0, 200))) * k
        y = (H - 1 - j) * PX
        img[y:y + PX, i * PX:(i + 1) * PX] = col.astype(np.uint8)
    return img


def pad(img, w, h):
    out = np.zeros((h, w, 3), np.uint8)
    out[:] = BG
    out[:img.shape[0], :img.shape[1]] = img
    return out


def sheet(ships):
    """One row per ship: plan (from above, bow up), side (bow right), front."""
    rows = []
    for s in ships:
        cells = cells_of(s)
        plan = view(cells, (0, 1), 2)                    # x right, y up the page
        side = view(cells, (1, 2), 0, flip_depth=False)  # y right, z up, seen from +x
        front = view(cells, (0, 2), 1)                   # x right, z up, seen from the bow
        h = max(plan.shape[0], side.shape[0], front.shape[0])
        gap = np.zeros((h, 24, 3), np.uint8)
        gap[:] = BG
        rows.append(np.concatenate([pad(plan, plan.shape[1], h), gap, pad(side, side.shape[1], h), gap,
                                    pad(front, front.shape[1], h)], 1))
    w = max(r.shape[1] for r in rows)
    out = []
    for r in rows:
        out.append(pad(r, w, r.shape[0]))
        sep = np.zeros((24, w, 3), np.uint8)
        sep[:] = BG
        out.append(sep)
    return np.concatenate(out, 0)


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    path = args[0] if args else os.path.join(HERE, "silhouettes.png")
    specs = args[1:] or ["ld_cruisers:cruiser_nave", "ld_cruisers:cruiser_broadside", "ld_cruisers:cruiser_lance",
                         "ld_slopes:wraith_rebuilt:k3"]
    ships = []
    for spec in specs:
        mod, fn, *a = spec.split(":")
        ships.append(getattr(importlib.import_module(mod), fn)(*a))
    _png(path, sheet(ships))
    for spec, s in zip(specs, ships):
        print("SIL", spec, len(s.cells), "cells, kit off", getattr(s, "kit_off", "-"))


main()
