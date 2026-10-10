"""Round-4 blocks for the second ship lines (DRYDOCK: Caisson Heavy Industries; NOCTURNE: the Knell choir).

Same conventions as ld_blocks: local metres, footprint (0,0,0)..(sx,sy,sz)*CELL, +Y front, +Z top, mount on the base
(z = 0) unless ld_check lists the block as mounting on its back (claw, toll) or its left side (coil)."""

import math

from mathutils import Vector

from ld_geom import CELL, box, cyl, lathe, tube, torus, sphere, ico, hash01, _frame
from ld_blocks import chbox, hull_pts, capsule, pipe_path, hose, bolts, ring_bolts, hazard, ribs, lamp

KIT4 = {
    "beacon": (1, 1, 1), "piston": (1, 1, 4), "claw": (4, 6, 4), "coil": (2, 4, 12),
    "toll": (20, 60, 20), "censer_core": (12, 12, 12), "bell_s": (2, 2, 3), "candle": (1, 1, 2), "gargoyle": (2, 2, 2),
}

LABELS4 = {
    "beacon": "Beacon / lantern", "piston": "Hydraulic ram", "claw": "Grapple claw", "coil": "Rail coil",
    "toll": "The Toll (bell clapper)", "censer_core": "Censer core", "bell_s": "Hanging bell", "candle": "Candles",
    "gargoyle": "Gargoyle",
}

CATEGORY4 = {
    "New this round (ship lines)": ["claw", "coil", "piston", "beacon", "toll", "censer_core", "bell_s", "candle", "gargoyle"],
}


def beacon(b):
    """DRYDOCK: rotating amber work beacon in a wire cage. NOCTURNE: an iron lantern with a candle flame."""
    W, L, H, F = b.W, b.L, b.H, b.F
    cx, cy = W / 2, L / 2
    if F == "goth":
        b.flat("trim", chbox((cx - 0.13, cy - 0.13, 0), (cx + 0.13, cy + 0.13, 0.06), 0.02))
        for sx in (-1, 1):
            for sy in (-1, 1):
                b.put("metal", cyl((cx + sx * 0.09, cy + sy * 0.09, 0.06), (cx + sx * 0.09, cy + sy * 0.09, 0.34), 0.012, 4))
        b.put("glass", box((cx - 0.08, cy - 0.08, 0.08), (cx + 0.08, cy + 0.08, 0.32)), False)
        b.put("emit_c", sphere((cx, cy, 0.19), 0.045, 8, 6, (1, 1, 1.8)))
        b.put("trim", cyl((cx, cy, 0.34), (cx, cy, 0.46), 0.12, 4, r1=0.0, phase=math.pi / 4))
        b.put("metal", torus((cx, cy, 0.49), (1, 0, 0), 0.035, 0.008, 8, 3))
    else:
        b.flat("trim", chbox((cx - 0.16, cy - 0.16, 0), (cx + 0.16, cy + 0.16, 0.07), 0.02))
        b.put("metal", cyl((cx, cy, 0.07), (cx, cy, 0.12), 0.1, 12))
        b.put("emit_c", sphere((cx, cy, 0.17), 0.085, 14, 8, (1, 1, 1.1)))
        b.put("emit_c", cyl((cx, cy, 0.12), (cx, cy, 0.2), 0.086, 14))
        for i in range(4):
            a = math.pi / 4 + i * math.pi / 2
            b.put("metal", cyl((cx + math.cos(a) * 0.1, cy + math.sin(a) * 0.1, 0.1), (cx + math.cos(a) * 0.1, cy + math.sin(a) * 0.1, 0.27), 0.008, 4))
        b.put("metal", torus((cx, cy, 0.27), (0, 0, 1), 0.1, 0.008, 12, 3))


def piston(b):
    """Hydraulic ram: barrel, chrome rod, clevis eyes and a hose."""
    W, L, H, F = b.W, b.L, b.H, b.F
    cx, cy = W / 2, L / 2
    paint = "hull" if F != "goth" else "hull2"
    b.flat("trim", chbox((cx - 0.17, cy - 0.17, 0), (cx + 0.17, cy + 0.17, 0.08), 0.02))
    b.put("metal", cyl((cx - 0.1, cy, 0.18), (cx + 0.1, cy, 0.18), 0.08, 12))
    b.put(paint, cyl((cx, cy, 0.1), (cx, cy, H * 0.55), 0.13, 14))
    b.put("trim", cyl((cx, cy, H * 0.55 - 0.03), (cx, cy, H * 0.55 + 0.04), 0.15, 14))
    b.put("chrome", cyl((cx, cy, H * 0.55), (cx, cy, H - 0.12), 0.065, 12))
    b.put("trim", cyl((cx, cy, H - 0.16), (cx, cy, H), 0.11, 12))
    b.put("metal", cyl((cx - 0.12, cy, H - 0.08), (cx + 0.12, cy, H - 0.08), 0.05, 8))
    hose(b, [(cx + 0.13, cy, 0.3), (cx + 0.2, cy, 0.5), (cx + 0.15, cy, H * 0.5)], 0.025, "hose")
    if F != "goth":
        hazard(b, (cx - 0.135, cy - 0.135, H * 0.42), (cx + 0.135, cy + 0.135, H * 0.5), 4, axis=2, tag="accent")


def claw(b):
    """Grapple claw (mounts on its back face): a wrist drum and three hydraulic talons curling forward."""
    W, L, H, F = b.W, b.L, b.H, b.F
    cx, cz = W / 2, H / 2
    paint = {"ind": "armor", "goth": "hull2", "opera": "hull2"}[F]
    knuckle = {"ind": "hull", "goth": "hull", "opera": "hull"}[F]
    b.put("trim", cyl((cx, 0.0, cz), (cx, 0.35, cz), W * 0.42, 16))
    b.put(paint, cyl((cx, 0.35, cz), (cx, 0.9, cz), W * 0.36, 16))
    b.put("metal", torus((cx, 0.62, cz), (0, 1, 0), W * 0.37, 0.04, 24, 6))
    b.put("dark", cyl((cx, 0.9, cz), (cx, 1.05, cz), W * 0.22, 16))
    for i in range(3):
        a = math.pi / 2 + 2 * math.pi * i / 3
        d = Vector((math.cos(a), 0, math.sin(a)))
        c0 = Vector((cx, 0.0, cz))
        pts = [c0 + d * 0.55 + Vector((0, 0.95, 0)), c0 + d * 0.9 + Vector((0, 1.55, 0)),
               c0 + d * 0.82 + Vector((0, 2.25, 0)), c0 + d * 0.32 + Vector((0, 2.9, 0))]
        rr = (0.2, 0.17, 0.13, 0.05)
        for k in range(3):
            b.put(paint if k < 2 else "metal", capsule(tuple(pts[k]), tuple(pts[k + 1]), rr[k], 10, 3, r1=rr[k + 1]))
            b.put(knuckle, sphere(tuple(pts[k]), rr[k] * 1.15, 10, 6))
        b.put("chrome", cyl(tuple(c0 + d * 0.3 + Vector((0, 0.5, 0))), tuple(pts[1] - d * 0.1), 0.045, 8))
        if F == "ind":
            p = pts[1]
            hazard(b, tuple(p - Vector((0.12, 0.12, 0.12))), tuple(p + Vector((0.12, 0.12, 0.12))), 4, axis=1, tag="accent")
    b.put("emit_c" if F != "goth" else "emit_x", sphere((cx, 1.08, cz), 0.12, 10, 6))


def coil(b):
    """Rail coil (mounts on its left side, against a wall): a tall electromagnet that lines a rail-driver channel."""
    W, L, H, F = b.W, b.L, b.H, b.F
    b.flat("trim", chbox((0, 0.05, 0.1), (0.35, L - 0.05, H - 0.1), 0.04))
    n = 9
    for i in range(n):
        z0 = 0.35 + i * (H - 0.7) / n
        z1 = z0 + (H - 0.7) / n * 0.72
        b.flat("foil", chbox((0.3, 0.15, z0), (W - 0.18, L - 0.15, z1), 0.05))
        b.flat("dark", box((W - 0.2, 0.3, z0 + 0.04), (W - 0.12, L - 0.3, z1 - 0.04)))
    b.flat("emit_p", box((W - 0.14, 0.42, 0.4), (W - 0.08, L - 0.42, H - 0.4)))
    for z in (0.12, H - 0.3):
        b.flat("armor" if F != "goth" else "hull2", chbox((0.3, 0.05, z), (W - 0.05, L - 0.05, z + 0.18), 0.03))
    if F == "ind":
        hazard(b, (W - 0.06, 0.05, H - 0.3), (W - 0.04, L - 0.05, H - 0.12), 6, axis=1, tag="accent")


def toll(b):
    """The Toll (KNELL's weapon, mounts on its back face at the bell's crown): a clapper the size of a frigate. Struck,
    it rings a graviton wave out of the bell's mouth that shakes block bonds loose across the whole cone."""
    W, L, H, F = b.W, b.L, b.H, b.F
    cx, cz = W / 2, H / 2
    R = W * 0.47
    yb = L - R * 1.05
    # the stem, with collars and tuning fins
    b.put("trim", cyl((cx, 0.0, cz), (cx, 1.2, cz), W * 0.16, 24))
    b.put("hull2", cyl((cx, 1.2, cz), (cx, yb - R * 0.6, cz), W * 0.06, 24))
    for t in (0.22, 0.45, 0.68):
        y = 1.2 + (yb - R * 0.6 - 1.2) * t
        b.put("gold", torus((cx, y, cz), (0, 1, 0), W * 0.075, 0.12, 32, 8))
        for i in range(6):
            a = 2 * math.pi * i / 6 + t
            d = Vector((math.cos(a), 0, math.sin(a)))
            p = Vector((cx, y, cz))
            b.flat("hull2", hull_pts([tuple(p + d * W * 0.05 + Vector((0, -0.8, 0))), tuple(p + d * W * 0.05 + Vector((0, 0.8, 0))),
                                      tuple(p + d * W * 0.2 + Vector((0, 0.2, 0))),
                                      tuple(p + d * W * 0.05 + Vector((0.05, 0, 0.05))), tuple(p + d * W * 0.18 + Vector((0.05, 0.1, 0.05)))]))
    b.put("emit_x", cyl((cx, 1.25, cz), (cx, yb - R * 0.65, cz), W * 0.03, 12))
    # the ball: a dark bronze striker, cracked with light, girdled by gilt bands
    b.put("hull2", cyl((cx, yb - R * 0.75, cz), (cx, yb - R * 0.2, cz), W * 0.06, 24, r1=W * 0.2))
    v, f = ico((cx, yb, cz), R, 3)
    b.put("trim", (v, f))
    b.put("emit_x", ico((cx, yb, cz), R * 0.985, 2))
    for k, (ax, tilt) in enumerate((((0, 1, 0), 0.0), ((0.3, 1, 0.2), 0.0), ((-0.3, 1, 0.25), 0.0))):
        b.put("gold", torus((cx, yb, cz), ax, R * 1.005, R * 0.035, 64, 6))
    # crack-light: thin emissive veins on the ball (rings at several tilts, just proud of the surface)
    for i in range(7):
        a = 2 * math.pi * i / 7
        axis = (math.cos(a), 0.35, math.sin(a))
        b.put("emit_x", torus((cx, yb, cz), axis, R * 1.002, R * 0.008, 48, 3))
    b.put("emit_x", sphere((cx, yb + R * 0.95, cz), R * 0.18, 16, 8))


def censer_core(b):
    """Censer core (CENSER's weapon): a caged furnace that vents an ionised 'incense' storm through the openwork globe."""
    W, L, H, F = b.W, b.L, b.H, b.F
    c = Vector((W / 2, L / 2, H / 2))
    R = W * 0.3
    b.put("ember", ico(tuple(c), R, 2))
    b.put("emit_x", ico(tuple(c), R * 0.7, 1))
    for i in range(8):
        a = 2 * math.pi * i / 8
        d = Vector((math.cos(a), math.sin(a), 0))
        b.put("gold", pipe_path([tuple(c + Vector((0, 0, -R * 1.45))), tuple(c + d * R * 1.25), tuple(c + Vector((0, 0, R * 1.45)))], 0.07, 8))
    for z in (-0.6, 0.0, 0.6):
        rr = R * 1.25 * math.cos(math.asin(min(0.95, abs(z))))
        b.put("gold", torus(tuple(c + Vector((0, 0, z * R * 1.25))), (0, 0, 1), rr, 0.06, 32, 6))
    b.put("trim", cyl(tuple(c + Vector((0, 0, R * 1.4))), (c.x, c.y, H), W * 0.12, 12))
    b.put("trim", cyl((c.x, c.y, 0.0), tuple(c + Vector((0, 0, -R * 1.4))), W * 0.12, 12))
    b.put("hull2", cyl((c.x, c.y, H - 0.5), (c.x, c.y, H), W * 0.22, 16))
    b.put("hull2", cyl((c.x, c.y, 0.0), (c.x, c.y, 0.5), W * 0.22, 16))
    b.put("shieldfx", sphere(tuple(c), R * 1.6, 24, 12))


def bell_s(b):
    """A hanging bell (mount on the base; placed upside down it hangs from the hull above)."""
    W, L, H, F = b.W, b.L, b.H, b.F
    cx, cy = W / 2, L / 2
    tag = "hull2" if F == "goth" else "gold"
    b.flat("trim", chbox((cx - 0.2, cy - 0.06, 0), (cx + 0.2, cy + 0.06, 0.12), 0.02))
    b.put("metal", torus((cx, cy, 0.18), (0, 1, 0), 0.08, 0.02, 12, 4))
    prof = [(0.0, 0.24), (0.14, 0.26), (0.2, 0.34), (0.22, 0.6), (0.26, 0.9), (0.36, 1.18), (0.47, 1.3), (0.48, 1.36), (0.43, 1.36),
            (0.33, 1.2), (0.24, 0.95), (0.19, 0.62), (0.16, 0.36), (0.0, 0.34)]
    b.put(tag, lathe([(r, z) for r, z in prof], (cx, cy, 0.0), (0, 0, 1), 24, False, False))
    b.put("gold", torus((cx, cy, 1.22), (0, 0, 1), 0.4, 0.025, 24, 4))
    b.put("dark", sphere((cx, cy, 1.25), 0.09, 10, 6))
    b.put("metal", cyl((cx, cy, 0.34), (cx, cy, 1.2), 0.02, 6))


def candle(b):
    """A cluster of votive candles on an iron dish, flames lit."""
    W, L, H, F = b.W, b.L, b.H, b.F
    cx, cy = W / 2, L / 2
    b.put("trim", cyl((cx, cy, 0.0), (cx, cy, 0.05), 0.22, 12, r1=0.2))
    for i, (dx, dy, h) in enumerate(((0.0, 0.0, 0.55), (0.11, 0.06, 0.38), (-0.1, 0.07, 0.3), (0.02, -0.12, 0.44), (-0.08, -0.08, 0.22))):
        b.put("ceramic", cyl((cx + dx, cy + dy, 0.05), (cx + dx, cy + dy, 0.05 + h), 0.035, 8))
        b.put("ceramic", sphere((cx + dx + 0.03, cy + dy, 0.05 + h * 0.8), 0.018, 6, 4))
        b.put("emit_c", sphere((cx + dx, cy + dy, 0.05 + h + 0.035), 0.02, 8, 6, (1, 1, 2.2)))


def gargoyle(b):
    """A crouched gargoyle on a plinth, wings half-spread, looking forward (+Y)."""
    W, L, H, F = b.W, b.L, b.H, b.F
    cx, cy = W / 2, L / 2
    stone = "hull" if F == "goth" else "armor"
    b.flat("trim", chbox((cx - 0.4, cy - 0.4, 0), (cx + 0.4, cy + 0.4, 0.14), 0.03))
    b.put(stone, ico((cx, cy - 0.05, 0.42), 0.26, 1, scale=(1.0, 1.2, 1.0), jitter=0.12, seed=3))
    b.put(stone, ico((cx, cy + 0.22, 0.62), 0.15, 1, scale=(1.0, 1.25, 0.95), jitter=0.1, seed=5))
    for s in (-1, 1):
        b.put(stone, cyl((cx + s * 0.07, cy + 0.32, 0.7), (cx + s * 0.13, cy + 0.2, 0.86), 0.03, 5, r1=0.004))     # horns
        b.put(stone, capsule((cx + s * 0.15, cy + 0.12, 0.3), (cx + s * 0.17, cy + 0.3, 0.14), 0.05, 6, 2))       # fore legs
        b.put(stone, capsule((cx + s * 0.2, cy - 0.15, 0.3), (cx + s * 0.22, cy - 0.05, 0.14), 0.07, 6, 2))       # haunches
        b.flat(stone, hull_pts([(cx + s * 0.15, cy - 0.1, 0.55), (cx + s * 0.6, cy - 0.3, 0.95), (cx + s * 0.55, cy - 0.38, 0.6),
                                (cx + s * 0.15, cy - 0.25, 0.45), (cx + s * 0.16, cy - 0.12, 0.57)]))
        b.put("emit_w", sphere((cx + s * 0.05, cy + 0.36, 0.66), 0.018, 6, 4))
    b.put(stone, capsule((cx, cy - 0.3, 0.28), (cx, cy - 0.45, 0.16), 0.04, 6, 2))


BUILDERS4 = {
    "beacon": beacon, "piston": piston, "claw": claw, "coil": coil, "toll": toll, "censer_core": censer_core,
    "bell_s": bell_s, "candle": candle, "gargoyle": gargoyle,
}
