"""Capital-ship kit: large hull blocks (2x2x2 to 3x6x3) and capital weapons for cruisers and dreadnoughts.

Same conventions as ld_blocks: local metres, footprint (0,0,0)..(sx,sy,sz)*CELL, +Y front, +Z top, base at z = 0.
Every block is built per flavour: 'ind' (DRYDOCK), 'goth' (NOCTURNE), 'opera' (OPERA)."""

import math

from mathutils import Vector

from ld_geom import CELL, box, cyl, lathe, tube, torus, sphere, ico, mesh_cells, hash01
from ld_blocks import (chbox, hull_pts, capsule, pipe_path, hose, bolts, ring_bolts, hazard, ribs, lamp, arc, barrel,
                       turret_base, gunhouse)

KIT_CAPITAL = {
    # large hull
    "hull_2": (2, 2, 2), "hull_3": (3, 3, 3), "wedge_3": (3, 3, 3), "corner_3": (3, 3, 3), "slope_3x6": (3, 6, 3),
    "armor_3": (3, 3, 3), "truss_3": (3, 6, 3),
    # capital weapons
    "battery": (12, 18, 8), "lance": (12, 80, 12), "singularity": (16, 24, 16), "storm": (10, 10, 20),
    "graviton": (14, 12, 12), "nova": (6, 6, 14), "hangar": (16, 24, 6),
    # capital modules
    "thruster_xl": (8, 12, 8),
}

LABELS_CAPITAL = {
    "hull_2": "Hull 2×2×2", "hull_3": "Hull 3×3×3", "wedge_3": "Slope 3×3×3", "corner_3": "Corner 3×3×3",
    "slope_3x6": "Long slope 3×6×3", "armor_3": "Armour block 3×3×3", "truss_3": "Truss 3×6×3",
    "battery": "Triple battery", "lance": "Spinal particle lance", "singularity": "Singularity projector",
    "storm": "Storm coil", "graviton": "Graviton snare", "nova": "Nova silo", "hangar": "Drone carrier bay",
    "thruster_xl": "Capital engine",
}

CATEGORY_CAPITAL = {
    "Large hull (cruisers and dreadnoughts)": ["hull_2", "hull_3", "wedge_3", "corner_3", "slope_3x6", "armor_3", "truss_3"],
    "Capital weapons": ["battery", "lance", "singularity", "storm", "graviton", "nova", "hangar"],
    "Capital modules": ["thruster_xl"],
}


# ======================================================================================================================
# Large hull blocks
# ======================================================================================================================

def big_hull(b, kind):
    F = b.F
    sx, sy, sz = b.size
    u = sx
    W, L, H = b.W, b.L, b.H
    tag = "armor" if kind == "armor_3" else "hull"
    if kind == "truss_3":
        return truss(b)
    cells = {(0, 0, 0): tag}
    shapes = {}
    if kind == "wedge_3":
        shapes[(0, 0, 0)] = ("slope", ((0, 1, 0), (0, 0, 1)))
    elif kind == "corner_3":
        shapes[(0, 0, 0)] = ("clip", (((0, 1, 1), 1.0), ((1, 0, 1), 1.0)))
    elif kind == "slope_3x6":
        cells = {(0, 0, 0): tag, (0, 1, 0): tag}
        shapes[(0, 0, 0)] = ("clip", (((0, 0.5, 1), 1.0),))
        shapes[(0, 1, 0)] = ("clip", (((0, 0.5, 1), 0.5),))
    out, _ = mesh_cells(cells, shapes, cell=CELL * u)
    for t, vf in out.items():
        b.flat(f"{t}#{u}", vf)
    if kind == "armor_3":
        armor_plates(b)
    elif kind in ("hull_2", "hull_3"):
        # big blocks carry their own fittings at their own scale: lifting eyes and a stencilled corner, or ribs, or trim
        if F == "ind":
            bolts(b, [(x, y, H) for x in (0.08, W - 0.08) for y in (0.08, L - 0.08)], r=0.03, h=0.02)
            hazard(b, (W - 0.32, L - 0.006, H - 0.32), (W - 0.04, L, H - 0.04), 4)
            for x in (W * 0.3, W * 0.7):
                b.put("metal", torus((x, L * 0.5, H + 0.03), (1, 0, 0), 0.05, 0.015, 10, 4))
        elif F == "goth":
            for k in range(max(2, u)):
                x = (k + 0.5) * W / max(2, u)
                b.flat("trim", box((x - 0.03, 0.0, H), (x + 0.03, L, H + 0.05)))
            b.flat("trim", box((0.0, L, H * 0.7), (W, L + 0.006, H * 0.76)))
        else:
            b.put("gold", torus((W / 2, L / 2, H + 0.004), (0, 0, 1), min(W, L) * 0.38, 0.012, 48, 4))
            b.flat("hull2", box((-0.004, L * 0.42, H * 0.3), (W + 0.004, L * 0.58, H * 0.4)))


def armor_plates(b):
    W, L, H, F = b.W, b.L, b.H, b.F
    if F == "ind":
        for i in (0, 1):
            for j in (0, 1):
                x0, y0 = 0.05 + i * W / 2, 0.05 + j * L / 2
                b.flat("armor", chbox((x0, y0, H), (x0 + W / 2 - 0.1, y0 + L / 2 - 0.1, H + 0.09), 0.03))
                bolts(b, [(x0 + 0.06, y0 + 0.06, H + 0.09), (x0 + W / 2 - 0.16, y0 + 0.06, H + 0.09),
                          (x0 + 0.06, y0 + L / 2 - 0.16, H + 0.09), (x0 + W / 2 - 0.16, y0 + L / 2 - 0.16, H + 0.09)], r=0.025)
        b.flat("armor", chbox((0.05, L, 0.05), (W - 0.05, L + 0.09, H - 0.05), 0.03))
        hazard(b, (0.05, L + 0.09, H - 0.35), (W - 0.05, L + 0.095, H - 0.15), 10)
        for x in (0.25, W - 0.25):
            b.put("metal", torus((x, L + 0.12, H * 0.5), (1, 0, 0), 0.07, 0.02, 12, 4))
    elif F == "goth":
        n = 7
        for i in range(n):
            x = 0.05 + (i + 0.5) * (W - 0.1) / n
            b.flat("armor", hull_pts([(x - 0.05, 0.0, H), (x + 0.05, 0.0, H), (x - 0.05, L, H), (x + 0.05, L, H),
                                      (x, 0.0, H + 0.16), (x, L, H + 0.16)]))
        b.flat("trim", box((0.0, -0.01, H * 0.72), (W, L + 0.01, H * 0.8)))
        for k in range(3):
            y = (k + 0.5) * L / 3
            b.flat("trim", hull_pts([(W, y - 0.05, 0.1), (W, y + 0.05, 0.1), (W, y - 0.05, H - 0.1), (W, y + 0.05, H - 0.1),
                                     (W + 0.22, y - 0.05, 0.15), (W + 0.22, y + 0.05, 0.15)]))
    else:
        b.flat("armor", chbox((0.08, 0.08, H), (W - 0.08, L - 0.08, H + 0.08), 0.04, top=(0.06, 0.06)))
        b.put("gold", torus((W / 2, L / 2, H + 0.085), (0, 0, 1), min(W, L) * 0.32, 0.016, 48, 4))
        b.put("gold", sphere((W / 2, L / 2, H + 0.1), 0.05, 12, 6))
        for x in (0.0, W):
            b.flat("gold", box((x - 0.004, 0.1, H * 0.45), (x + 0.004, L - 0.1, H * 0.5)))


def truss(b):
    """Open lattice beam: docking arms, station frames, cradles."""
    W, L, H, F = b.W, b.L, b.H, b.F
    t = 0.08 if F != "opera" else 0.05
    corners = [(t, t), (W - t, t), (W - t, H - t), (t, H - t)]
    main = "accent" if F == "ind" else ("trim" if F == "goth" else "chrome")
    brace = "trim" if F == "ind" else ("metal" if F == "goth" else "chrome")
    for x, z in corners:
        if F == "opera":
            b.put(main, cyl((x, 0, z), (x, L, z), t, 12))
        else:
            b.flat(main, box((x - t, 0, z - t), (x + t, L, z + t)))
    n = 4
    for i in range(n + 1):
        y = i * L / n
        for (x0, z0), (x1, z1) in zip(corners, corners[1:] + corners[:1]):
            if F == "opera":
                b.put(brace, cyl((x0, y, z0), (x1, y, z1), t * 0.7, 8))
            else:
                b.flat(main, box((min(x0, x1) - t * 0.8, y - t * 0.8, min(z0, z1) - t * 0.8), (max(x0, x1) + t * 0.8, y + t * 0.8, max(z0, z1) + t * 0.8)))
        if i < n:
            y1 = (i + 1) * L / n
            for (x0, z0), (x1, z1) in zip(corners, corners[1:] + corners[:1]):
                if F == "goth":
                    # pointed arches instead of diagonal braces
                    mx, mz = (x0 + x1) / 2, (z0 + z1) / 2
                    ym = (y + y1) / 2
                    b.put(brace, cyl((x0, y, z0), (mx, ym, mz), 0.025, 6))
                    b.put(brace, cyl((x1, y1, z1), (mx, ym, mz), 0.025, 6))
                else:
                    b.put(brace, cyl((x0, y, z0), (x1, y1, z1), t * 0.45, 6))
    if F == "ind":
        for i in range(n + 1):
            lamp(b, (t, i * L / n, H + 0.02), 0.03, "nav_r" if i % 2 else "window", None)
    elif F == "opera":
        for i in range(n + 1):
            for x, z in corners:
                b.put("gold", sphere((x, i * L / n, z), t * 1.4, 10, 5))


# ======================================================================================================================
# Capital weapons
# ======================================================================================================================

def battery(b):
    """Triple battery: the dreadnought's main turret, three long barrels in an armoured house."""
    W, L, H, F = b.W, b.L, b.H, b.F
    cx, cy = W / 2, L * 0.36
    turret_base(b, cx, cy, W * 0.46, 0.35, F)
    gx0, gx1 = cx - W * 0.42, cx + W * 0.42
    gy0, gy1 = cy - L * 0.34, cy + L * 0.3
    gunhouse(b, (gx0, gy0, 0.35), (gx1, gy1, H * 0.78), F)
    zb = H * 0.5
    for s in (-1, 0, 1):
        x = cx + s * W * 0.22
        if F == "goth":
            for k in range(5):
                b.put("hose", torus((x, gy1 + 0.05 + k * 0.08, zb), (0, 1, 0), 0.3 - k * 0.02, 0.06, 14, 6))
        elif F == "ind":
            b.put("hose", cyl((x, gy1 - 0.05, zb), (x, gy1 + 0.45, zb), 0.36, 14, r1=0.26))
        else:
            b.put("hull", sphere((x, gy1, zb), 0.42, 24, 12))
        barrel(b, (x, gy1 + 0.4, zb), (x, L + 2.6, zb + 0.08), 0.17, F, "chrome" if F == "opera" else "metal")
    if F == "ind":
        # rangefinder arms, ladder, hatches, warning paint
        for s in (-1, 1):
            b.flat("trim", chbox((cx + s * (W * 0.42) - 0.2, gy0 + 0.4, H * 0.62), (cx + s * (W * 0.42 + 0.9), gy0 + 1.2, H * 0.74), 0.04))
            b.put("glass", cyl((cx + s * (W * 0.42 + 0.92), gy0 + 0.8, H * 0.68), (cx + s * (W * 0.42 + 0.95), gy0 + 0.8, H * 0.68), 0.1, 16))
        for k in range(10):
            b.put("accent", cyl((gx0 - 0.04, gy0 + 0.3, 0.6 + k * 0.22), (gx0 - 0.04, gy0 + 0.8, 0.6 + k * 0.22), 0.02, 6))
        b.flat("hull2", box((cx - 1.4, gy0 - 0.006, H * 0.5), (cx + 1.4, gy0, H * 0.7)))
        for x in (cx - 0.9, cx + 0.9):
            b.flat("trim", chbox((x - 0.3, gy0 + 0.3, H * 0.78), (x + 0.3, gy0 + 0.9, H * 0.82), 0.02))
        lamp(b, (cx, gy0 + 0.4, H * 0.84), 0.06, "nav_r")
    elif F == "goth":
        for s in (-1, 1):
            for k in range(4):
                y = gy0 + 0.5 + k * (gy1 - gy0 - 1.0) / 3
                b.flat("trim", hull_pts([(cx + s * W * 0.42, y, 0.4), (cx + s * W * 0.42, y + 0.15, 0.4), (cx + s * W * 0.42, y, H * 0.8),
                                         (cx + s * (W * 0.42 + 0.5), y, 0.4), (cx + s * (W * 0.42 + 0.5), y + 0.15, 0.4)]))
            b.put("metal", cyl((cx + s * 1.2, gy0 + 0.6, H * 0.78), (cx + s * 1.25, gy0 + 0.5, H * 1.25), 0.05, 6, r1=0.004))
        for k in range(8):
            x = cx - 1.4 + k * 0.4
            b.flat("window", box((x - 0.06, gy0 - 0.006, H * 0.62), (x + 0.06, gy0, H * 0.7)))
        lamp(b, (cx, gy0 + 0.3, H * 0.8), 0.05, "nav_r", None)
    else:
        b.put("glass", capsule((cx - 1.2, gy0 + 0.9, H * 0.76), (cx + 1.2, gy0 + 0.9, H * 0.76), 0.18, 16, 4))
        b.put("gold", torus((cx, cy, 0.35), (0, 0, 1), W * 0.46, 0.04, 64, 6))


def lance(b):
    """Spinal particle lance: a dreadnought-length accelerator. Charges for seconds, then a beam that cuts a hull in two."""
    W, L, H, F = b.W, b.L, b.H, b.F
    cx, zc = W / 2, H / 2
    breech = L * 0.14
    # cradle: the beam rests on a keel girder all along its length
    b.flat("trim", chbox((cx - W * 0.22, 0.0, 0.0), (cx + W * 0.22, L * 0.97, zc - W * 0.2), 0.08))
    if F == "ind":
        b.flat("paint_w", chbox((0.0, 0.0, 0.0), (W, breech, H), 0.25))
        hazard(b, (0.0, -0.006, H * 0.2), (W, 0.0, H * 0.4), 12)
        for s in (-1, 1):
            for k in range(3):
                b.put("paint_p", cyl((cx + s * (W / 2 + 0.25), 0.6 + k * 1.3, 0.3), (cx + s * (W / 2 + 0.25), 0.6 + k * 1.3, H - 0.3), 0.32, 16))
        ring_tag, core_tag = "accent", "metal"
    elif F == "goth":
        b.flat("paint_w", chbox((0.0, 0.0, 0.0), (W, breech, H), 0.3, top=(W * 0.12, 0.2)))
        ribs(b, (0.1, 0.4, 0.0), (W - 0.1, breech - 0.3, H), 1, 9, 0.05, "trim", 0.06, inset=1)
        ring_tag, core_tag = "trim", "dark"
    else:
        b.flat("hull", chbox((0.0, 0.0, 0.0), (W, breech, H), 0.6, top=(W * 0.1, 0.4)))
        b.flat("hull2", box((-0.01, breech * 0.4, H * 0.42), (W + 0.01, breech * 0.6, H * 0.58)))
        ring_tag, core_tag = "gold", "hull"
    # the accelerator core, with a glowing channel visible through slots
    y0, y1 = breech, L - 2.0
    b.put(core_tag, cyl((cx, y0, zc), (cx, y1, zc), W * 0.17, 24))
    b.put("emit_x", cyl((cx, y0, zc), (cx, y1 + 0.1, zc), W * 0.05, 16))
    n = 14
    for i in range(n):
        y = y0 + 0.6 + i * (y1 - y0 - 1.2) / (n - 1)
        R = W * 0.42 - (i / n) * W * 0.06
        if F == "ind":
            pts = []
            for k in range(8):
                a = math.radians(22.5 + k * 45)
                pts += [(cx + math.cos(a) * R, y, zc + math.sin(a) * R), (cx + math.cos(a) * R, y + 0.7, zc + math.sin(a) * R)]
            b.flat(ring_tag, hull_pts(pts))
            b.flat("dark", box((cx - R * 0.6, y - 0.01, zc - R * 0.6), (cx + R * 0.6, y + 0.71, zc + R * 0.6)))
            b.flat("trim", box((cx - R * 0.98, y + 0.25, zc - R * 0.98), (cx + R * 0.98, y + 0.45, zc + R * 0.98)))
        elif F == "goth":
            pts = []
            for k in range(9):
                a = math.radians(-90 + k * 40)
                r = R * (1.0 if k % 2 == 0 else 0.82)
                pts.append((cx + math.cos(a) * r, zc + math.sin(a) * r))
            pts.append((cx, zc + R * 1.45))
            v = [(x, y, z) for x, z in pts] + [(x, y + 0.45, z) for x, z in pts]
            b.flat(ring_tag, hull_pts(v))
        else:
            b.put("chrome", torus((cx, y + 0.3, zc), (0, 1, 0), R * 0.86, W * 0.1, 48, 12))
            b.put(ring_tag, torus((cx, y + 0.3, zc), (0, 1, 0), R * 0.86, W * 0.03, 48, 6))
    # the focusing crown at the muzzle
    if F == "opera":
        b.put("hull", lathe([(W * 0.2, 0), (W * 0.4, 1.0), (W * 0.48, 1.9)], (cx, y1, zc), (0, 1, 0), 48, False, False))
        b.put("gold", torus((cx, y1 + 1.9, zc), (0, 1, 0), W * 0.48, 0.06, 48, 6))
    else:
        for k in range(3 if F == "ind" else 5):
            a = math.radians(90 + k * 360 / (3 if F == "ind" else 5))
            p0 = Vector((cx + math.cos(a) * W * 0.3, y1 - 0.5, zc + math.sin(a) * W * 0.3))
            p1 = Vector((cx + math.cos(a) * W * 0.16, L + 0.6, zc + math.sin(a) * W * 0.16))
            b.put("metal", cyl(tuple(p0), tuple(p1), W * 0.05, 10, r1=W * 0.02))
    b.put("emit_x", sphere((cx, L - 0.6, zc), W * 0.12, 24, 12))
    lamp(b, (cx, breech * 0.5, H + 0.05), 0.08, "nav_r", None)


def singularity(b):
    """Singularity projector: holds a pinhead black hole in a gimbal and throws it. Feeds on a reactor's full output."""
    W, L, H, F = b.W, b.L, b.H, b.F
    cx, cy, zc = W / 2, L * 0.48, H * 0.56
    R = min(W, H) * 0.42
    # base cradle and pylons
    base_t = 0.9
    if F == "opera":
        b.flat("hull", chbox((0.2, 0.4, 0.0), (W - 0.2, L - 0.4, base_t), 0.3, top=(0.4, 0.4)))
        b.put("gold", torus((cx, cy, base_t + 0.02), (0, 0, 1), R * 0.95, 0.05, 64, 6))
    else:
        b.flat("paint_w", chbox((0.1, 0.2, 0.0), (W - 0.1, L - 0.2, base_t), 0.2))
        if F == "ind":
            hazard(b, (0.1, 0.194, 0.2), (W - 0.1, 0.2, 0.6), 14)
    for s in (-1, 1):
        x = cx + s * (R + 0.55)
        if F == "ind":
            b.flat("accent", chbox((x - 0.45, cy - 1.6, base_t), (x + 0.45, cy + 1.6, zc + 0.5), 0.1, top=(0.0, 1.0)))
            b.flat("trim", box((x - 0.5, cy - 0.3, zc - 0.3), (x + 0.5, cy + 0.3, zc + 0.3)))
        elif F == "goth":
            b.flat("trim", hull_pts([(x - 0.4, cy - 2.2, base_t), (x + 0.4, cy - 2.2, base_t), (x - 0.4, cy + 2.2, base_t), (x + 0.4, cy + 2.2, base_t),
                                     (x - 0.25, cy - 0.3, zc + 1.8), (x + 0.25, cy - 0.3, zc + 1.8), (x - 0.25, cy + 0.3, zc + 1.8), (x + 0.25, cy + 0.3, zc + 1.8)]))
            b.put("metal", cyl((x, cy, zc + 1.8), (x, cy, zc + 3.2), 0.12, 8, r1=0.01))
        else:
            b.flat("hull", chbox((x - 0.35, cy - 1.2, base_t), (x + 0.35, cy + 1.2, zc + 0.4), 0.15, top=(0.0, 0.8)))
            b.put("gold", sphere((x, cy, zc), 0.4, 20, 10))
    # gimbal rings
    ring_tag = {"ind": "accent", "goth": "trim", "opera": "chrome"}[F]
    for k, (ax, rr) in enumerate((((1, 0, 0), R), ((0, 0.34, 0.94), R * 0.86), ((0.7, 0.71, 0), R * 0.73))):
        b.put(ring_tag, torus((cx, cy, zc), ax, rr, 0.16 if F != "opera" else 0.12, 64, 10))
        if F == "goth":
            from ld_geom import _frame
            u_, v_, w_ = _frame(ax)
            for i in range(12):
                a = 2 * math.pi * i / 12
                d = u_ * math.cos(a) + v_ * math.sin(a)
                p = Vector((cx, cy, zc)) + d * rr
                b.put("metal", cyl(tuple(p), tuple(p + d * 0.45), 0.06, 6, r1=0.004))
        elif F == "ind":
            ring_bolts(b, (cx, cy, zc), ax, rr * 1.0, 16, r=0.04)
        else:
            b.put("gold", torus((cx, cy, zc), ax, rr, 0.05, 64, 6))
    # the hole: black core, photon ring, accretion disc, a lensing bubble
    b.put("dark", sphere((cx, cy, zc), R * 0.26, 32, 16))
    b.put("emit_x", torus((cx, cy, zc), (0, 0.2, 1), R * 0.29, 0.03, 48, 6))
    from ld_geom import _frame
    u_, v_, w_ = _frame((0.25, -0.1, 1.0))
    ring_pts, faces = [], []
    seg = 64
    for i in range(seg):
        a = 2 * math.pi * i / seg
        d = u_ * math.cos(a) + v_ * math.sin(a)
        for rad in (R * 0.32, R * 0.45, R * 0.64):
            ring_pts.append(tuple(Vector((cx, cy, zc)) + d * rad))
    for i in range(seg):
        a0, b0 = i * 3, ((i + 1) % seg) * 3
        faces.append((a0, b0, b0 + 1, a0 + 1))
        faces.append((a0 + 1, b0 + 1, b0 + 2, a0 + 2))
    b.put("emit_c", (ring_pts[:], faces[0::2]))
    b.put("emit_x", (ring_pts[:], faces[1::2]))
    b.put("shieldfx", sphere((cx, cy, zc), R * 0.5, 32, 16))
    # launch throat in front, conduits from the capacitor banks behind
    yt = cy + R + 0.4
    throat_tag = "paint_w" if F != "opera" else "hull"
    b.put(throat_tag, lathe([(R * 0.45, 0), (R * 0.42, 0.6), (R * 0.55, L - yt - 0.2), (R * 0.62, L - yt)], (cx, yt, zc), (0, 1, 0), 32, False, False))
    b.put("emit_x", torus((cx, L - 0.05, zc), (0, 1, 0), R * 0.56, 0.05, 48, 6))
    b.flat("trim", chbox((cx - 0.5, yt, base_t), (cx + 0.5, L - 0.3, zc - R * 0.42), 0.08))
    cap_tag = "paint_p" if F != "opera" else "chrome"
    for s in (-1, 1):
        for k in range(2):
            x = cx + s * (1.0 + k * 1.3)
            b.put(cap_tag, cyl((x, 0.9, base_t), (x, 0.9, base_t + 2.2), 0.5, 20))
            b.put("trim", cyl((x, 0.9, base_t + 2.2), (x, 0.9, base_t + 2.4), 0.42, 20))
            if F == "goth":
                hose(b, [(x, 0.9, base_t + 2.3), (x * 0.7 + cx * 0.3, cy - R * 0.9, zc + 0.6), (cx + s * 0.4, cy - R * 0.32, zc)], 0.08, "hose", "metal", 0.14)
            else:
                b.put("hose" if F == "ind" else "gold", pipe_path([(x, 0.9, base_t + 2.3), (x * 0.7 + cx * 0.3, cy - R * 0.9, zc + 0.6),
                                                                  (cx + s * 0.4, cy - R * 0.32, zc)], 0.07))
    lamp(b, (0.4, 0.5, base_t + 0.05), 0.06, "nav_r", None)
    lamp(b, (W - 0.4, 0.5, base_t + 0.05), 0.06, "nav_r", None)


def storm(b):
    """Storm coil: a tower that throws chain lightning through every hull in reach."""
    W, L, H, F = b.W, b.L, b.H, b.F
    cx, cy = W / 2, L / 2
    base_h = 1.4
    if F == "opera":
        b.put("hull", lathe([(W * 0.48, 0), (W * 0.48, base_h * 0.6), (W * 0.3, base_h)], (cx, cy, 0), (0, 0, 1), 48, False, True))
        b.put("gold", torus((cx, cy, base_h * 0.6), (0, 0, 1), W * 0.48, 0.04, 48, 4))
    else:
        b.flat("paint_p", chbox((0.1, 0.1, 0.0), (W - 0.1, L - 0.1, base_h), 0.2))
        ribs(b, (0.1, 0.1, 0.15), (W - 0.1, L - 0.1, base_h - 0.15), 0, 9, 0.06, "trim", 0.04)
        if F == "ind":
            hazard(b, (0.1, 0.094, 0.2), (W - 0.1, 0.1, 0.55), 10)
    # insulator column
    n = 14
    top = H - 2.0
    for k in range(n):
        z = base_h + k * (top - base_h) / n
        r = 0.75 - 0.02 * k
        b.put("ceramic", lathe([(0.35, 0), (r, 0.12), (r, 0.2), (0.35, 0.32)], (cx, cy, z), (0, 0, 1), 32, False, False))
    b.put("ceramic", cyl((cx, cy, base_h), (cx, cy, top), 0.35, 24))
    # crown
    if F == "opera":
        b.put("chrome", torus((cx, cy, top + 0.4), (0, 0, 1), 1.5, 0.45, 48, 16))
        b.put("gold", torus((cx, cy, top + 0.4), (0, 0, 1), 1.5, 0.47, 48, 4))
        b.put("chrome", sphere((cx, cy, top + 1.1), 0.7, 32, 16))
    elif F == "ind":
        b.put("metal", torus((cx, cy, top + 0.4), (0, 0, 1), 1.6, 0.5, 48, 16))
        b.put("metal", cyl((cx, cy, top), (cx, cy, top + 0.6), 1.0, 32))
        ring_bolts(b, (cx, cy, top + 0.6), (0, 0, 1), 0.8, 12, r=0.05)
    else:
        for k in range(9):
            a = math.radians(k * 40)
            p0 = (cx + math.cos(a) * 0.6, cy + math.sin(a) * 0.6, top)
            p1 = (cx + math.cos(a + 0.4) * 1.8, cy + math.sin(a + 0.4) * 1.8, top + 1.6 + 0.4 * (k % 2))
            b.put("metal", cyl(p0, p1, 0.1, 6, r1=0.01))
        b.put("trim", cyl((cx, cy, top - 0.3), (cx, cy, top + 0.4), 0.9, 9))
        # cage of iron ribs around the column, converging like a spire
        for k in range(6):
            a = math.radians(k * 60 + 15)
            b.put("trim", cyl((cx + math.cos(a) * 1.9, cy + math.sin(a) * 1.9, base_h), (cx + math.cos(a) * 0.95, cy + math.sin(a) * 0.95, top - 0.4), 0.07, 6))
    b.put("emit_s", sphere((cx, cy, top + 0.5), 0.35, 20, 10))
    # lightning crawling over the crown and down to the ground rods
    for k in range(4):
        a = math.radians(k * 90 + 45)
        rod = (cx + math.cos(a) * (W / 2 - 0.4), cy + math.sin(a) * (L / 2 - 0.4))
        b.put("trim" if F != "opera" else "gold", cyl((rod[0], rod[1], base_h), (rod[0], rod[1], base_h + 2.4), 0.07, 8))
        b.put("metal" if F != "opera" else "chrome", sphere((rod[0], rod[1], base_h + 2.5), 0.16, 12, 6))
        arc(b, (cx + math.cos(a) * 1.4, cy + math.sin(a) * 1.4, top + 0.3), (rod[0], rod[1], base_h + 2.5), seed=k + 11, n=10, jit=0.35, r=0.03)
    for k in range(5):
        a = math.radians(k * 72 + 10)
        arc(b, (cx + math.cos(a) * 1.5, cy + math.sin(a) * 1.5, top + 0.5),
            (cx + math.cos(a + 1.1) * 2.6, cy + math.sin(a + 1.1) * 2.6, top + 1.6 + hash01(k, 3)), seed=k + 31, n=8, jit=0.3, r=0.025)


def graviton(b):
    """Graviton snare: a dish that pulls a ship in (salvage, boarding) or holds it still for the big guns."""
    W, L, H, F = b.W, b.L, b.H, b.F
    cx, cy = W / 2, L * 0.42
    turret_base(b, cx, cy, min(W, L) * 0.36, 0.4, F)
    # yoke
    yoke = "paint_w" if F != "opera" else "hull"
    for s in (-1, 1):
        x = cx + s * W * 0.36
        b.flat(yoke, chbox((x - 0.3, cy - 0.6, 0.4), (x + 0.3, cy + 0.6, H * 0.55), 0.1, top=(0.0, 0.3)))
    b.flat(yoke, chbox((cx - W * 0.4, cy - 0.7, 0.4), (cx + W * 0.4, cy + 0.7, 1.0), 0.12))
    # dish facing forward, tilted up
    tilt = math.radians(18)
    axis = Vector((0, math.cos(tilt), math.sin(tilt)))
    c = Vector((cx, cy + 0.4, H * 0.55))
    Rd = min(W, H) * 0.47
    depth = Rd * 0.32
    prof = [(Rd * k / 10, depth * (k / 10) ** 2) for k in range(11)]
    dish_tag = {"ind": "metal", "goth": "trim", "opera": "chrome"}[F]
    v, f = lathe(prof, tuple(c - axis * depth), tuple(axis), 64, False, False)
    b.put(dish_tag, (v, f))
    b.put("dark", (v, [tuple(reversed(x)) for x in f]))
    from ld_geom import _frame
    u_, v_, w_ = _frame(tuple(axis))
    if F == "goth":
        # rose window: radial tracery and concentric rings
        for i in range(16):
            a = 2 * math.pi * i / 16
            d = u_ * math.cos(a) + v_ * math.sin(a)
            p0 = c - axis * depth + d * Rd * 0.12
            p1 = c - axis * (depth - depth) + d * Rd * 0.99
            b.put("metal", cyl(tuple(p0 + axis * 0.04), tuple(p1 + axis * 0.04), 0.05, 6))
        for t in (0.35, 0.62, 0.86):
            b.put("metal", torus(tuple(c - axis * depth * (1 - t * t) + axis * 0.05), tuple(axis), Rd * t, 0.05, 48, 6))
    else:
        for t in (0.3, 0.55, 0.8):
            b.put("emit_x", torus(tuple(c - axis * depth * (1 - t * t) + axis * 0.03), tuple(axis), Rd * t, 0.035, 64, 4))
    b.put(dish_tag, torus(tuple(c), tuple(axis), Rd, 0.12, 64, 8))
    if F == "ind":
        hazard(b, (cx - 1.2, cy - 0.706, 0.5), (cx + 1.2, cy - 0.7, 0.9), 10)
    elif F == "opera":
        b.put("gold", torus(tuple(c), tuple(axis), Rd, 0.05, 64, 6))
    # focus spike and the snare's glowing focus
    for i in range(3):
        a = 2 * math.pi * i / 3 + 0.5
        d = u_ * math.cos(a) + v_ * math.sin(a)
        b.put("metal" if F != "opera" else "gold", cyl(tuple(c + d * Rd * 0.95), tuple(c + axis * Rd * 0.9), 0.05, 6))
    b.put("emit_x", sphere(tuple(c + axis * Rd * 0.92), 0.28, 20, 10))
    b.put("shieldfx", sphere(tuple(c + axis * Rd * 0.92), 0.6, 20, 10))


def nova(b):
    """Nova silo: one heavy warhead in an armoured tube; the hatch is open and the warhead is up."""
    W, L, H, F = b.W, b.L, b.H, b.F
    cx, cy = W / 2, L / 2
    r = min(W, L) * 0.36
    if F == "goth":
        # an organ pipe: ribbed tube with a pointed hood
        b.flat("paint_w", chbox((0.1, 0.1, 0.0), (W - 0.1, L - 0.1, 1.0), 0.15))
        b.put("trim", cyl((cx, cy, 0.0), (cx, cy, H - 1.6), r * 1.08, 12))
        for k in range(int((H - 2.0) / 0.5)):
            b.put("metal", torus((cx, cy, 1.2 + k * 0.5), (0, 0, 1), r * 1.1, 0.05, 12, 4))
        b.put("trim", lathe([(r * 1.12, 0), (r * 0.9, 0.6), (0.05, 1.6)], (cx, cy, H - 1.6), (0, 0, 1), 12, False, False))
        b.flat("window", box((cx - 0.08, cy - r * 1.1 - 0.01, 2.0), (cx + 0.08, cy - r * 1.09, H - 2.4)))
        b.put("emit_w", sphere((cx, cy - r * 1.05, H - 2.2), 0.09, 10, 5))
        return
    hous = "paint_w" if F == "ind" else "hull"
    b.flat(hous, chbox((0.05, 0.05, 0.0), (W - 0.05, L - 0.05, H * 0.62), 0.2 if F == "ind" else 0.4, top=(0.1, 0.1)))
    zt = H * 0.62
    b.put("dark", cyl((cx, cy, zt - 0.02), (cx, cy, zt + 0.01), r, 32))
    # the warhead rising out of the tube
    wh = "paint_w" if F == "ind" else "chrome"
    b.put("metal", cyl((cx, cy, zt - 0.3), (cx, cy, zt + 0.9), r * 0.82, 32))
    b.put(wh, lathe([(r * 0.82, 0), (r * 0.82, 0.6), (r * 0.6, 1.5), (r * 0.25, 2.3), (0.0, 2.7)], (cx, cy, zt + 0.9), (0, 0, 1), 32, False, False))
    if F == "ind":
        for k in range(4):
            b.put("accent" if k % 2 == 0 else "dark", cyl((cx, cy, zt + 0.95 + k * 0.12), (cx, cy, zt + 1.07 + k * 0.12), r * 0.83, 32))
        hazard(b, (0.05, 0.044, 0.3), (W - 0.05, 0.05, 0.9), 8)
    else:
        b.put("gold", torus((cx, cy, zt + 1.5), (0, 0, 1), r * 0.61, 0.03, 32, 4))
        b.put("gold", sphere((cx, cy, zt + 3.62), 0.07, 12, 6))
    # hatch swung open behind
    hinge_y = L - 0.05
    b.flat("trim" if F == "ind" else "hull2", hull_pts([(cx - r * 1.05, hinge_y, zt), (cx + r * 1.05, hinge_y, zt), (cx - r * 1.05, hinge_y, zt + 0.15),
                                                        (cx + r * 1.05, hinge_y, zt + 0.15), (cx - r * 1.05, hinge_y + 0.2, zt + r * 2.0),
                                                        (cx + r * 1.05, hinge_y + 0.2, zt + r * 2.0)]))
    lamp(b, (0.25, 0.25, zt + 0.02), 0.05, "nav_r", None)


def hangar(b):
    """Drone carrier bay: an open deck with launch rails and a flight of drones ready to go."""
    W, L, H, F = b.W, b.L, b.H, b.F
    wall = "paint_e" if F != "opera" else "hull"
    b.flat(wall, chbox((0.0, 0.0, 0.0), (W, L, 0.5), 0.1))
    for x0 in (0.0, W - 0.6):
        b.flat(wall, chbox((x0, 0.0, 0.0), (x0 + 0.6, L, H), 0.12, top=(0.05, 0.0)))
    b.flat(wall, chbox((0.0, 0.0, 0.0), (W, 1.2, H), 0.12))
    b.flat("dark", box((0.6, 1.2, 0.5), (W - 0.6, L, 0.52)))
    # launch rails with deck lights
    nr = 3
    for i in range(nr):
        x = 0.6 + (i + 0.5) * (W - 1.2) / nr
        b.flat("metal", box((x - 0.06, 1.4, 0.52), (x + 0.06, L - 0.1, 0.6)))
        for k in range(10):
            y = 1.6 + k * (L - 2.0) / 10
            b.put("window" if F != "goth" else "emit_w", sphere((x - 0.35, y, 0.56), 0.04, 8, 4))
            b.put("window" if F != "goth" else "emit_w", sphere((x + 0.35, y, 0.56), 0.04, 8, 4))
        # drones on the rails
        for k in range(2):
            y = 2.6 + k * 4.2 + i * 0.6
            drone(b, x, y, 0.62, F)
    # half-open roof doors
    door = "trim" if F != "opera" else "hull2"
    b.flat(door, box((0.6, 1.2, H - 0.12), (W * 0.32, L * 0.7, H)))
    b.flat(door, box((W * 0.68, 1.2, H - 0.12), (W - 0.6, L * 0.7, H)))
    if F == "ind":
        hazard(b, (0.6, L - 0.006, 0.6), (W - 0.6, L, 0.85), 16)
        hazard(b, (W * 0.32 - 0.25, 1.2, H), (W * 0.32, L * 0.7, H + 0.006), 12, axis=1)
    elif F == "goth":
        for k in range(6):
            y = 1.6 + k * (L - 2.4) / 5
            b.flat("trim", hull_pts([(0.0, y, H), (0.0, y + 0.2, H), (0.0, y, 0.4), (-0.4, y, 0.4), (-0.4, y + 0.2, 0.4), (0.0, y + 0.2, 0.4)]))
            b.flat("trim", hull_pts([(W, y, H), (W, y + 0.2, H), (W, y, 0.4), (W + 0.4, y, 0.4), (W + 0.4, y + 0.2, 0.4), (W, y + 0.2, 0.4)]))
    else:
        b.flat("gold", box((-0.005, 0.3, H * 0.6), (0.0, L - 0.3, H * 0.66)))
        b.flat("gold", box((W, 0.3, H * 0.6), (W + 0.005, L - 0.3, H * 0.66)))
    lamp(b, (0.3, L - 0.2, H + 0.04), 0.05, "nav_g", None)
    lamp(b, (W - 0.3, L - 0.2, H + 0.04), 0.05, "nav_r", None)


def drone(b, x, y, z, F):
    body = "hull2" if F != "goth" else "armor"
    b.flat(body, hull_pts([(x - 0.18, y - 0.6, z), (x + 0.18, y - 0.6, z), (x - 0.1, y + 0.7, z), (x + 0.1, y + 0.7, z),
                           (x, y - 0.5, z + 0.32), (x, y + 0.3, z + 0.22)]))
    b.flat("hull" if F != "goth" else "trim", hull_pts([(x - 0.75, y - 0.5, z + 0.08), (x + 0.75, y - 0.5, z + 0.08), (x, y + 0.35, z + 0.08),
                                                        (x - 0.75, y - 0.5, z + 0.13), (x + 0.75, y - 0.5, z + 0.13), (x, y + 0.35, z + 0.13)]))
    b.put("emit_t", sphere((x, y - 0.62, z + 0.12), 0.07, 8, 4))
    b.put("glass", sphere((x, y + 0.2, z + 0.2), 0.08, 8, 4))


BUILDERS_CAPITAL = {
    "battery": battery, "lance": lance, "singularity": singularity, "storm": storm, "graviton": graviton, "nova": nova,
    "hangar": hangar,
}
BIG_HULL = {"hull_2", "hull_3", "wedge_3", "corner_3", "slope_3x6", "armor_3", "truss_3"}
