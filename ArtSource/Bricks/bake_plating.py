"""Bake the look book's procedural hull plating (ArtSource/LookDev/ld_plating.py) into tiling game textures.

The game samples these in the brick shader with grid-continuous face UVs (one tile = 6 m = 12 cells), so the plates, bolts,
grilles, hatches, stencils, chevrons, wear and window rows cost texture reads, not polygons. Own work: the maps come from
our numpy generator; nothing is imported.

Run with any Python 3 that has numpy (Blender's bundled one works):
    /Applications/Blender.app/Contents/Resources/<ver>/python/bin/python3 ArtSource/Bricks/bake_plating.py

Writes three flipbook strips (one 1024 px tile per style, top to bottom in STYLES order) to Assets/Game/Art/Nocturne/Plating:
    Plating_A.png  R tint x0.5, G secondary paint, B black/stencil, A accent      (ld_mats.plated_material imA)
    Plating_B.png  R height, G dark metal, B edge wear, A lit windows             (imB)
    Plating_N.png  RG tangent-space normal from height (x right, y up), B cavity occlusion, A 1
"""
import os
import struct
import sys
import zlib

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, os.path.join(ROOT, "ArtSource", "LookDev"))
import ld_plating  # noqa: E402

PX = 1024                                   # per 6 m tile: 170 px/m, 85 px per 0.5 m cell
STYLES = ["ark", "yard"]                    # slice 0: hull plate (round-4 HALO look); slice 1: calm plate for armour/machinery
RELIEF_M = 0.025                            # ld_mats bump distance
OUT = os.path.join(ROOT, "Assets", "Game", "Art", "Nocturne", "Plating")


def write_png(path, rgba):
    """Minimal RGBA8 PNG writer (stdlib zlib), rows top to bottom."""
    h, w, _ = rgba.shape
    raw = b"".join(b"\x00" + rgba[y].tobytes() for y in range(h))

    def chunk(tag, data):
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n")
        f.write(chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0)))
        f.write(chunk(b"IDAT", zlib.compress(raw, 9)))
        f.write(chunk(b"IEND", b""))


def to8(a):
    return (np.clip(a, 0, 1) * 255 + 0.5).astype(np.uint8)


def bake(name):
    ld_plating.PLATING[name] = dict(ld_plating.PLATING[name], px=PX)
    T = ld_plating.generate(name)
    ppm = PX / ld_plating.TILE_M
    H = T.H
    # Periodic central differences; array row 0 is the tile's bottom (the look book's Blender image convention).
    dx = (np.roll(H, -1, axis=1) - np.roll(H, 1, axis=1)) * 0.5
    dy = (np.roll(H, -1, axis=0) - np.roll(H, 1, axis=0)) * 0.5
    k = RELIEF_M * ppm
    n = np.stack([-dx * k, -dy * k, np.ones_like(H)], axis=-1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    cavity = np.clip(0.5 - ld_plating._blur(H, 3), 0, 1) * 2.0
    A = np.stack([T.T * 0.5, T.P, T.K, T.G], axis=-1)
    B = np.stack([H, T.M, T.W, T.E], axis=-1)
    N = np.stack([n[..., 0] * 0.5 + 0.5, n[..., 1] * 0.5 + 0.5, 1 - np.clip(cavity * 2.5, 0, 1), np.ones_like(H)], axis=-1)
    # PNG rows run top to bottom; flip so texture v = 0 is the tile's bottom row.
    return [to8(m[::-1]) for m in (A, B, N)]


def main():
    os.makedirs(OUT, exist_ok=True)
    tiles = [bake(s) for s in STYLES]
    for index, suffix in enumerate("ABN"):
        strip = np.concatenate([t[index] for t in tiles], axis=0)
        write_png(os.path.join(OUT, "Plating_%s.png" % suffix), strip)
        print("wrote Plating_%s.png" % suffix, strip.shape)


if __name__ == "__main__":
    main()
