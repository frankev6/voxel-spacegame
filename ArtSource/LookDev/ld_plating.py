"""Hull plating textures (round 4): procedural, tiling panel maps generated with numpy, no imported images.

One tile covers TILE_M metres. Its top-level grid is the 1.5 m big-block grid, so the largest seams follow block edges.
Every 1.5 m panel is split recursively into plates down to ~0.2 m, and each plate gets relief (flush, raised or inset,
with bevelled edges and a seam), a tint, and features: bolts, grilles, hatches, tread plate, hazard chevrons, stencil
numbers (industrial), or fluting, lancet arches, quatrefoils, scale armour and gilt trim (gothic).

Maps (float, 0..1), packed into two RGBA images:
  A: T tint (x0.5), P secondary paint, K black/stencil, G accent (gilt, stripes)
  B: H height, M dark-metal detail, W edge wear (bare metal), E windows (lit on vertical faces only)
"""

import math

import numpy as np

TILE_M = 6.0

PLATING = {
    # DRYDOCK line B (Caisson Heavy Industries): worn safety-yellow heavy plate on graphite frames
    "caisson": dict(px=2048, seed=41, base_m=1.5, merge=0.35, maxd=4, stop=(0.0, 0.1, 0.3, 0.55, 0.8), min_m=0.22,
                    snap_m=0.125, long_bias=0.8, raise_p=0.32, inset_p=0.18, relief=0.11, bevel_m=0.022, seam_m=0.007,
                    tint=0.04, paint_top=0.12, paint_p=0.02, patch_p=0.08, black_p=0.0,
                    feats=dict(bolts=0.6, grille=0.06, hatch=0.06, tread=0.1, chevron=0.05, stencil=0.07, subpanel=0.18,
                               pipes=0.05, vents=0.05, stripe=0.05),
                    windows=dict(rows_m=3.0, z0_m=1.1, w_m=0.13, h_m=0.22, pitch_m=0.42, on=0.72),
                    wear=0.55, chips=0.35),
    # NOCTURNE line B (the Knell choir): bone plate and blackened bronze, fluted, arched, riveted, gilt-edged
    "knell": dict(px=2048, seed=57, base_m=1.5, merge=0.3, maxd=4, stop=(0.0, 0.08, 0.28, 0.5, 0.8), min_m=0.2,
                  snap_m=0.125, long_bias=0.85, raise_p=0.3, inset_p=0.26, relief=0.12, bevel_m=0.02, seam_m=0.008,
                  tint=0.035, paint_top=0.14, paint_p=0.02, patch_p=0.05, black_p=0.0,
                  feats=dict(rivets=0.6, flute=0.16, lancet=0.13, quatrefoil=0.07, scales=0.12, gilt=0.22, engrave=0.16,
                             grille=0.05, subpanel=0.08),
                  windows=dict(rows_m=3.0, z0_m=1.1, w_m=0.09, h_m=0.34, pitch_m=0.5, on=0.6, arch=True),
                  wear=0.35, chips=0.2),
    # batch 3, the ARK (printed Earth patterns): even 1.5 m standard blocks, bolted, stencilled pattern numbers, hazard chevrons
    "ark": dict(px=2048, seed=83, base_m=1.5, merge=0.25, maxd=3, stop=(0.0, 0.12, 0.38, 0.7), min_m=0.25,
                snap_m=0.125, long_bias=0.7, raise_p=0.28, inset_p=0.16, relief=0.1, bevel_m=0.02, seam_m=0.007,
                tint=0.035, paint_top=0.1, paint_p=0.03, patch_p=0.06, black_p=0.0,
                feats=dict(bolts=0.55, stencil=0.12, chevron=0.04, hatch=0.06, grille=0.05, subpanel=0.14, vents=0.05,
                           pipes=0.04, tread=0.04),
                windows=dict(rows_m=3.0, z0_m=1.1, w_m=0.12, h_m=0.2, pitch_m=0.42, on=0.7),
                wear=0.4, chips=0.25),
    # line A (round-3 ships), finer and calmer, so the earlier designs gain the same sense of scale
    "yard": dict(px=2048, seed=63, base_m=1.5, merge=0.4, maxd=3, stop=(0.0, 0.15, 0.4, 0.75), min_m=0.25,
                 snap_m=0.125, long_bias=0.8, raise_p=0.25, inset_p=0.15, relief=0.09, bevel_m=0.018, seam_m=0.006,
                 tint=0.05, paint_p=0.0, patch_p=0.1, black_p=0.0,
                 feats=dict(bolts=0.4, grille=0.07, hatch=0.05, subpanel=0.14, vents=0.06, stencil=0.04, pipes=0.04),
                 windows=dict(rows_m=3.0, z0_m=1.1, w_m=0.13, h_m=0.22, pitch_m=0.42, on=0.65),
                 wear=0.3, chips=0.15),
    "company": dict(px=2048, seed=71, base_m=1.5, merge=0.35, maxd=4, stop=(0.0, 0.12, 0.35, 0.6, 0.85), min_m=0.22,
                    snap_m=0.125, long_bias=0.85, raise_p=0.3, inset_p=0.2, relief=0.1, bevel_m=0.02, seam_m=0.008,
                    tint=0.06, paint_p=0.0, patch_p=0.1, black_p=0.06,
                    feats=dict(rivets=0.5, grille=0.08, engrave=0.14, flute=0.06, subpanel=0.12, pipes=0.05, vents=0.07,
                               hatch=0.04),
                    windows=dict(rows_m=3.0, z0_m=1.1, w_m=0.09, h_m=0.3, pitch_m=0.5, on=0.55, arch=True),
                    wear=0.25, chips=0.15),
}

# 3 x 5 stencil digits
DIGITS = {
    "0": "111101101101111", "1": "010110010010111", "2": "111001111100111", "3": "111001111001111",
    "4": "101101111001001", "5": "111100111001111", "6": "111100111101111", "7": "111001010010010",
    "8": "111101111101111", "9": "111101111001111",
}


def _blur(a, r):
    """Periodic box blur (separable), radius r px."""
    if r <= 0:
        return a
    out = a
    for ax in (0, 1):
        c = np.cumsum(np.concatenate([out.take(range(-r - 1, 0), axis=ax), out, out.take(range(0, r), axis=ax)], axis=ax),
                      axis=ax, dtype=np.float64)
        n = out.shape[ax]
        hi = c.take(range(2 * r + 1, 2 * r + 1 + n), axis=ax)
        lo = c.take(range(0, n), axis=ax)
        out = ((hi - lo) / (2 * r + 1)).astype(np.float32)
    return out


def fbm(n, rng, octaves=((6, 1.0), (12, 0.5), (24, 0.25), (48, 0.125), (96, 0.07))):
    """Tiling value noise 0..1."""
    out = np.zeros((n, n), np.float32)
    tot = 0.0
    for k, amp in octaves:
        g = rng.random((k, k)).astype(np.float32)
        t = np.arange(n, dtype=np.float32) * k / n
        i0 = np.floor(t).astype(int) % k
        i1 = (i0 + 1) % k
        f = t - np.floor(t)
        f = f * f * (3 - 2 * f)
        a = g[i0][:, i0] * (1 - f)[None, :] + g[i0][:, i1] * f[None, :]
        b = g[i1][:, i0] * (1 - f)[None, :] + g[i1][:, i1] * f[None, :]
        out += amp * (a * (1 - f)[:, None] + b * f[:, None])
        tot += amp
    return out / tot


class Tile:
    def __init__(self, n):
        self.n = n
        z = lambda v=0.0: np.full((n, n), v, np.float32)
        self.T, self.P, self.K, self.G = z(1.0), z(), z(), z()
        self.H, self.M, self.W, self.E = z(0.5), z(), z(), z()
        self.edge = z()        # bevel zones (for wear)


def _split(rng, r, depth, C, out, ppm):
    x0, y0, x1, y1 = r
    w, h = x1 - x0, y1 - y0
    mn = C["min_m"] * ppm
    stop = C["stop"][min(depth, len(C["stop"]) - 1)]
    if depth >= C["maxd"] or rng.random() < stop or max(w, h) < 2 * mn:
        out.append(r)
        return
    axis = 0 if w >= h else 1
    if rng.random() > C["long_bias"]:
        axis = 1 - axis
    L = w if axis == 0 else h
    if L < 2 * mn:
        axis = 1 - axis
        L = w if axis == 0 else h
    if L < 2 * mn:
        out.append(r)
        return
    snap = C["snap_m"] * ppm
    t = round(rng.uniform(0.3, 0.7) * L / snap) * snap
    t = int(round(min(max(t, mn), L - mn)))
    if axis == 0:
        _split(rng, (x0, y0, x0 + t, y1), depth + 1, C, out, ppm)
        _split(rng, (x0 + t, y0, x1, y1), depth + 1, C, out, ppm)
    else:
        _split(rng, (x0, y0, x1, y0 + t), depth + 1, C, out, ppm)
        _split(rng, (x0, y0 + t, x1, y1), depth + 1, C, out, ppm)


def _disc(T, cx, cy, r, h=None, m=None, w=None, field="H"):
    """Stamp a disc (soft edge) at (cx, cy) px: adds h to height, sets metal mask m."""
    n = T.n
    x0, x1 = int(cx - r - 2), int(cx + r + 3)
    y0, y1 = int(cy - r - 2), int(cy + r + 3)
    if x0 < 0 or y0 < 0 or x1 > n or y1 > n:
        return
    yy, xx = np.mgrid[y0:y1, x0:x1].astype(np.float32)
    d = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2)
    k = np.clip(r - d + 0.5, 0, 1)
    dome = np.clip(1 - (d / max(r, 1e-3)) ** 2, 0, 1) ** 0.5 * k
    if h:
        T.H[y0:y1, x0:x1] += h * dome
    if m is not None:
        T.M[y0:y1, x0:x1] = np.maximum(T.M[y0:y1, x0:x1], m * k)
    if w is not None:
        T.W[y0:y1, x0:x1] = np.maximum(T.W[y0:y1, x0:x1], w * k * (d > r * 0.55))


def generate(name):
    C = PLATING[name]
    n = C["px"]
    ppm = n / TILE_M
    rng = np.random.default_rng(C["seed"])
    T = Tile(n)
    noise = fbm(n, rng)
    fine = fbm(n, rng, ((64, 1.0), (128, 0.6), (256, 0.4)))
    base = int(round(C["base_m"] * ppm))
    k = n // base
    # top level: 1.5 m panels, some merged into 3 x 1.5 m plates
    used = set()
    tops = []
    for j in range(k):
        for i in range(k):
            if (i, j) in used:
                continue
            if i + 1 < k and (i + 1, j) not in used and rng.random() < C["merge"]:
                used |= {(i, j), (i + 1, j)}
                tops.append((i * base, j * base, (i + 2) * base, (j + 1) * base))
            else:
                used.add((i, j))
                tops.append((i * base, j * base, (i + 1) * base, (j + 1) * base))
    rects = []
    zone = []
    for r in tops:
        z = 1.0 if rng.random() < C.get("paint_top", 0.0) else 0.0
        before = len(rects)
        _split(rng, r, 0, C, rects, ppm)
        zone += [z] * (len(rects) - before)
    seam = max(1.0, C["seam_m"] * ppm)
    bevel = max(2.0, C["bevel_m"] * ppm)
    feats = C["feats"]
    fk = list(feats)
    fw = np.array([feats[f] for f in fk], np.float64)
    for ri, (x0, y0, x1, y1) in enumerate(rects):
        w, h = x1 - x0, y1 - y0
        yy, xx = np.mgrid[y0:y1, x0:x1].astype(np.float32)
        d = np.minimum(np.minimum(xx - x0 + 0.5, x1 - 0.5 - xx), np.minimum(yy - y0 + 0.5, y1 - 0.5 - yy))
        u = rng.random()
        rel = C["relief"] if u < C["raise_p"] else (-C["relief"] if u < C["raise_p"] + C["inset_p"] else 0.0)
        ramp = np.clip((d - seam) / bevel, 0, 1)
        Hs = 0.5 + rel * ramp
        Hs = np.where(d < seam, 0.5 - 0.22 * (1 - d / seam) - 0.06, Hs)
        sl = (slice(y0, y1), slice(x0, x1))
        T.H[sl] = Hs
        T.edge[sl] = np.where((d >= seam) & (d < seam + bevel * 1.6), 1.0, 0.0) * (1.0 if rel >= 0 else 0.5)
        T.T[sl] = 1.0 + rng.uniform(-C["tint"], C["tint"])
        v = rng.random()
        if zone[ri] and rng.random() < 0.85:
            T.P[sl] = 1.0
        elif v < C["paint_p"]:
            T.P[sl] = 1.0
        elif v < C["paint_p"] + C["patch_p"]:
            T.P[sl] = rng.uniform(0.12, 0.3)         # a mismatched replacement plate
            T.T[sl] *= rng.uniform(0.92, 1.1)
        elif v < C["paint_p"] + C["patch_p"] + C["black_p"]:
            T.K[sl] = 0.65
        # features: one or two per plate, chosen by weight among those that fit its size and shape
        nfeat = 1 + int(rng.random() < 0.3)
        for _ in range(nfeat):
            if rng.random() > 0.78:
                continue
            f = fk[int(rng.choice(len(fk), p=fw / fw.sum()))]
            _feature(T, f, rng, (x0, y0, x1, y1), xx, yy, d, seam, bevel, ppm, C)
    # windows: rows of lit ports at deck heights (the shader shows them only on vertical faces)
    Wn = C.get("windows")
    if Wn:
        _windows(T, rng, Wn, ppm)
    # wear: chips along raised edges and a few scuffs, where the noise allows
    wear = C.get("wear", 0.0)
    chips = C.get("chips", 0.0)
    T.W = np.maximum(T.W, (T.edge * (noise > 0.62 - wear * 0.2) * (fine > 0.55)).astype(np.float32))
    T.W = np.maximum(T.W, ((fine > 0.86 - chips * 0.1) * (noise > 0.6)).astype(np.float32) * 0.8)
    # grime: dark seams and recesses, a little tonal cloud
    cav = np.clip(0.5 - _blur(T.H, 3), 0, 1) * 2.0
    T.T *= (1.0 - 0.55 * np.clip(cav * 2.5, 0, 1)) * (0.94 + 0.12 * noise)
    T.T = np.clip(T.T, 0.0, 2.0)
    return T


def _feature(T, f, rng, r, xx, yy, d, seam, bevel, ppm, C):
    x0, y0, x1, y1 = r
    w, h = x1 - x0, y1 - y0
    m = seam + bevel + 0.025 * ppm          # inner margin
    iw, ih = w - 2 * m, h - 2 * m
    sl = (slice(y0, y1), slice(x0, x1))
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    long_x = w >= h
    if f in ("bolts", "rivets"):
        step = (0.075 if f == "bolts" else 0.05) * ppm
        rad = (0.012 if f == "bolts" else 0.008) * ppm
        ins = seam + bevel + rad + 0.012 * ppm
        if w < 4 * ins or h < 4 * ins:
            return
        for (ax, ay, bx, by) in ((x0 + ins, y0 + ins, x1 - ins, y0 + ins), (x0 + ins, y1 - ins, x1 - ins, y1 - ins),
                                 (x0 + ins, y0 + ins, x0 + ins, y1 - ins), (x1 - ins, y0 + ins, x1 - ins, y1 - ins)):
            L = math.hypot(bx - ax, by - ay)
            cnt = max(2, int(L / step))
            for i in range(cnt + 1):
                t = i / cnt
                _disc(T, ax + (bx - ax) * t, ay + (by - ay) * t, rad, h=0.06, m=0.35, w=0.6)
    elif f == "grille":
        if min(iw, ih) < 0.12 * ppm or max(iw, ih) < 0.3 * ppm:
            return
        inner = (xx > x0 + m) & (xx < x1 - m) & (yy > y0 + m) & (yy < y1 - m)
        per = 0.035 * ppm
        q = ((xx if long_x else yy) % per) / per
        slot = inner & (q < 0.55)
        T.H[sl] = np.where(inner, np.where(slot, 0.3, 0.52), T.H[sl])
        T.M[sl] = np.maximum(T.M[sl], slot * 1.0)
        frame = ((xx > x0 + m - 4) & (xx < x1 - m + 4) & (yy > y0 + m - 4) & (yy < y1 - m + 4)) & ~inner
        T.H[sl] = np.where(frame, T.H[sl] + 0.05, T.H[sl])
    elif f == "hatch":
        R = min(iw, ih) * 0.45
        if R < 0.08 * ppm:
            return
        dd = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2)
        ring = np.abs(dd - R) < 0.008 * ppm
        disc = dd < R
        T.H[sl] = np.where(disc, T.H[sl] + 0.04, T.H[sl])
        T.H[sl] = np.where(ring, 0.32, T.H[sl])
        T.M[sl] = np.maximum(T.M[sl], ring * 0.8)
        for i in range(8):
            a = 2 * math.pi * i / 8 + 0.3
            _disc(T, cx + math.cos(a) * R * 0.82, cy + math.sin(a) * R * 0.82, 0.012 * ppm, h=0.05, m=0.4, w=0.5)
        bar = (np.abs(yy - cy) < 0.012 * ppm) & (np.abs(xx - cx) < R * 0.45)
        T.H[sl] = np.where(bar, T.H[sl] + 0.08, T.H[sl])
        T.M[sl] = np.maximum(T.M[sl], bar * 0.5)
    elif f == "tread":
        inner = d > seam + bevel * 0.6
        c = 0.045 * ppm
        lx, ly = (xx % c) / c - 0.5, (yy % c) / c - 0.5
        par = ((np.floor(xx / c) + np.floor(yy / c)) % 2) == 0
        lozenge = np.where(par, np.abs(lx - ly) < 0.12, np.abs(lx + ly) < 0.12) & (np.abs(lx) < 0.38) & (np.abs(ly) < 0.38)
        T.H[sl] = np.where(inner & lozenge, T.H[sl] + 0.05, T.H[sl])
        T.W[sl] = np.maximum(T.W[sl], (inner & lozenge) * 0.35)
        T.T[sl] *= 0.96
    elif f == "chevron":
        band = 0.16 * ppm
        if (h if long_x else w) < band * 1.5:
            return
        per = 0.07 * ppm
        side = rng.integers(2)
        if long_x:
            inb = (yy < y0 + band) if side == 0 else (yy > y1 - band)
        else:
            inb = (xx < x0 + band) if side == 0 else (xx > x1 - band)
        stripes = (((xx + yy) % (2 * per)) < per)
        T.K[sl] = np.maximum(T.K[sl], (inb & stripes & (d > seam)) * 1.0)
        T.P[sl] = np.where(inb, 0.0, T.P[sl])
    elif f == "stencil":
        px = max(4.0, 0.03 * ppm)
        digits = "".join(str(int(rng.integers(10))) for _ in range(int(rng.integers(2, 4))))
        tw, th = len(digits) * 4 * px, 5 * px
        if tw > iw or th > ih:
            return
        ox = x0 + m + rng.uniform(0, iw - tw)
        oy = y0 + m + rng.uniform(0, ih - th)
        val = 1.0
        target = T.K if rng.random() < 0.6 else T.G
        for di, ch in enumerate(digits):
            bits = DIGITS[ch]
            for b, bit in enumerate(bits):
                if bit == "1":
                    col, row = b % 3, 4 - b // 3
                    gx0 = int(ox + (di * 4 + col) * px)
                    gy0 = int(oy + row * px)
                    target[gy0:int(gy0 + px * 0.92), gx0:int(gx0 + px * 0.92)] = val
    elif f == "subpanel":
        mx, my = w * rng.uniform(0.15, 0.3), h * rng.uniform(0.15, 0.3)
        if w - 2 * mx < 0.08 * ppm or h - 2 * my < 0.08 * ppm:
            return
        dd = np.minimum(np.minimum(xx - (x0 + mx), (x1 - mx) - xx), np.minimum(yy - (y0 + my), (y1 - my) - yy))
        groove = np.abs(dd) < 0.005 * ppm
        T.H[sl] = np.where(groove, T.H[sl] - 0.12, np.where(dd > 0, T.H[sl] - 0.02, T.H[sl]))
        for sx, sy in ((x0 + mx + 6, y0 + my + 6), (x1 - mx - 6, y0 + my + 6), (x0 + mx + 6, y1 - my - 6), (x1 - mx - 6, y1 - my - 6)):
            _disc(T, sx, sy, 0.009 * ppm, h=0.05, m=0.4, w=0.4)
    elif f == "pipes":
        cnt = int(rng.integers(2, 5))
        span = ih if long_x else iw
        rr = min(0.025 * ppm, span / (cnt * 2.4))
        if rr < 4:
            return
        for i in range(cnt):
            off = (i - (cnt - 1) / 2) * rr * 2.3
            q = (yy - cy - off) if long_x else (xx - cx - off)
            along = (xx > x0 + m) & (xx < x1 - m) if long_x else (yy > y0 + m) & (yy < y1 - m)
            prof = np.sqrt(np.clip(1 - (q / rr) ** 2, 0, 1))
            T.H[sl] = np.where(along & (prof > 0), np.maximum(T.H[sl], 0.5 + 0.3 * prof), T.H[sl])
            T.M[sl] = np.maximum(T.M[sl], along * (prof > 0) * 0.35)
            clamp = along & (np.abs(q) < rr * 1.3) & ((((xx if long_x else yy) - x0) % (0.4 * ppm)) < 0.02 * ppm)
            T.H[sl] = np.where(clamp, T.H[sl] + 0.08, T.H[sl])
    elif f == "vents":
        cnt = int(rng.integers(3, 7))
        sw = 0.018 * ppm
        ln = min(0.12 * ppm, (ih if long_x else iw) * 0.8)
        if ln < 0.04 * ppm or (iw if long_x else ih) < cnt * sw * 3:
            return
        for i in range(cnt):
            off = (i - (cnt - 1) / 2) * sw * 2.8
            if long_x:
                dx, dy = np.abs(xx - cx - off), np.abs(yy - cy)
                cap = np.sqrt(np.maximum(dy - (ln / 2 - sw), 0) ** 2 + dx ** 2) < sw
            else:
                dx, dy = np.abs(xx - cx), np.abs(yy - cy - off)
                cap = np.sqrt(np.maximum(dx - (ln / 2 - sw), 0) ** 2 + dy ** 2) < sw
            T.H[sl] = np.where(cap, 0.3, T.H[sl])
            T.M[sl] = np.maximum(T.M[sl], cap * 1.0)
    elif f == "stripe":
        if long_x and h > 0.2 * ppm:
            band = np.abs(yy - cy) < 0.03 * ppm
        elif not long_x and w > 0.2 * ppm:
            band = np.abs(xx - cx) < 0.03 * ppm
        else:
            return
        T.G[sl] = np.maximum(T.G[sl], (band & (d > seam + bevel)) * 1.0)
    elif f == "gilt":
        g = np.abs(d - (seam + bevel + 0.02 * ppm)) < 0.006 * ppm
        T.G[sl] = np.maximum(T.G[sl], g * 1.0)
        T.H[sl] = np.where(g, T.H[sl] + 0.04, T.H[sl])
    elif f == "engrave":
        cnt = int(rng.integers(1, 4))
        for i in range(cnt):
            t = (i + 1) / (cnt + 1)
            if long_x:
                line = (np.abs(yy - (y0 + h * t)) < 0.004 * ppm) & (xx > x0 + m) & (xx < x1 - m)
            else:
                line = (np.abs(xx - (x0 + w * t)) < 0.004 * ppm) & (yy > y0 + m) & (yy < y1 - m)
            T.H[sl] = np.where(line, T.H[sl] - 0.1, T.H[sl])
    elif f == "flute":
        inner = d > seam + bevel
        per = 0.06 * ppm
        q = (((xx - x0) if not long_x else (yy - y0)) % per) / per
        prof = 0.5 - 0.5 * np.cos(2 * math.pi * q)
        T.H[sl] = np.where(inner, T.H[sl] - 0.07 * (1 - prof), T.H[sl])
    elif f == "lancet":
        aw = min(iw * 0.4, ih * 0.25)
        if aw < 0.06 * ppm:
            return
        spring = y0 + m + ih * 0.42
        apex_r = 2 * aw

        def inside(a):
            low = (np.abs(xx - cx) < a) & (yy > y0 + m) & (yy <= spring)
            top = (yy > spring) & (np.hypot(xx - (cx + a), yy - spring) < 2 * a) & (np.hypot(xx - (cx - a), yy - spring) < 2 * a)
            return low | top
        t = 0.012 * ppm
        outer, inner_ = inside(aw), inside(aw - t)
        band = outer & ~inner_
        T.H[sl] = np.where(inner_, T.H[sl] - 0.12, np.where(band, T.H[sl] + 0.05, T.H[sl]))
        T.G[sl] = np.maximum(T.G[sl], band * 0.8)
        T.K[sl] = np.maximum(T.K[sl], inner_ * 0.2)
        del apex_r
    elif f == "quatrefoil":
        R = min(iw, ih) * 0.42
        if R < 0.07 * ppm:
            return
        r = R * 0.55

        def foil(rr):
            o = R - rr
            return ((np.hypot(xx - cx - o, yy - cy) < rr) | (np.hypot(xx - cx + o, yy - cy) < rr) |
                    (np.hypot(xx - cx, yy - cy - o) < rr) | (np.hypot(xx - cx, yy - cy + o) < rr))
        t = 0.01 * ppm
        outer, inner_ = foil(r), foil(r - t)
        band = outer & ~inner_
        T.H[sl] = np.where(inner_, T.H[sl] - 0.08, np.where(band, T.H[sl] + 0.05, T.H[sl]))
        T.G[sl] = np.maximum(T.G[sl], band * 0.9)
        _disc(T, cx, cy, R * 0.16, h=0.1, m=0.0, w=0.3)
    elif f == "scales":
        inner = d > seam + bevel
        sw = 0.11 * ppm
        sh = sw * 0.62
        vv = (yy - y0) / sh
        row = np.floor(vv)
        uu = (xx - x0) / sw + 0.5 * (row % 2)
        fu = uu - np.floor(uu) - 0.5
        fv = vv - row
        dd = np.sqrt(fu ** 2 + (fv * sh / sw) ** 2)
        edge = np.clip((dd - 0.42) / 0.08, 0, 1)
        T.H[sl] = np.where(inner, T.H[sl] + 0.06 * (1 - fv) - 0.1 * edge, T.H[sl])
        T.T[sl] = np.where(inner, T.T[sl] * (1.0 - 0.08 * edge), T.T[sl])


def _windows(T, rng, Wn, ppm):
    n = T.n
    rows = max(1, int(round(TILE_M / Wn["rows_m"])))
    ww, wh, pitch = Wn["w_m"] * ppm, Wn["h_m"] * ppm, Wn["pitch_m"] * ppm
    xs = np.arange(n, dtype=np.float32)
    for r in range(rows):
        vc = (Wn["z0_m"] + r * Wn["rows_m"]) * ppm
        y0, y1 = int(vc - wh / 2), int(vc + wh / 2)
        q = xs % pitch
        k = np.floor(xs / pitch).astype(int)
        lit = np.array([rng.random() < Wn["on"] for _ in range(int(n / pitch) + 2)])
        # runs: whole stretches dark (unlit decks), the rest flickering on and off
        run = np.array([rng.random() < 0.75 for _ in range(int(n / pitch / 6) + 2)])
        on = lit[k] & run[k // 6]
        colmask = (np.abs(q - pitch / 2) < ww / 2)
        yy = np.arange(y0, y1)
        if Wn.get("arch"):
            # lancet windows: pointed tops
            for y in yy:
                t = (y - y0) / max(1, (y1 - y0))
                half = ww / 2 if t < 0.7 else ww / 2 * max(0.0, 1 - (t - 0.7) / 0.3) ** 0.6
                T.E[y % n, :] = np.maximum(T.E[y % n, :], ((np.abs(q - pitch / 2) < half) & on) * 1.0)
        else:
            for y in yy:
                T.E[y % n, :] = np.maximum(T.E[y % n, :], (colmask & on) * 1.0)


def to_images(name, T):
    """Two packed float images in Blender (non-colour)."""
    import bpy
    n = T.n
    out = []
    for suffix, chans in (("A", (T.T * 0.5, T.P, T.K, T.G)), ("B", (T.H, T.M, T.W, T.E))):
        nm = f"plating_{name}_{suffix}"
        img = bpy.data.images.get(nm)
        if img:
            bpy.data.images.remove(img)
        img = bpy.data.images.new(nm, n, n, alpha=True, float_buffer=True)
        img.colorspace_settings.name = "Non-Color"
        arr = np.stack([np.clip(c, 0, 1).astype(np.float32) for c in chans], axis=-1)
        img.pixels.foreach_set(arr.ravel())
        img.pack()
        out.append(img)
    return out


_CACHE = {}


def images(name):
    if name not in _CACHE:
        _CACHE[name] = to_images(name, generate(name))
    return _CACHE[name]


def preview(name, path):
    """Debug: write the maps as one PNG strip (needs PIL outside Blender; inside Blender uses bpy)."""
    T = generate(name)
    import bpy
    n = T.n
    tiles = [T.T * 0.5, T.P, T.K, T.G, T.H, T.M, T.W, T.E]
    strip = np.concatenate([np.clip(t, 0, 1) for t in tiles], axis=1)
    h, w = strip.shape
    img = bpy.data.images.new("prev_" + name, w, h, alpha=False)
    rgba = np.stack([strip, strip, strip, np.ones_like(strip)], axis=-1)
    img.pixels.foreach_set(rgba.astype(np.float32).ravel())
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
