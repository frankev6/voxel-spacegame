#!/usr/bin/env python3
"""Place design tool for 3D flight: a design (ArtSource/Places/designs/<key>.py, built with place_kit) voxelised and
rendered low-res in Blender, then drawn as review sheets. It extends DEV-023's flat map sheets (hub_layouts.py) to places
with height: plans at several cut heights, sections, and views from inside at ship scale.

Run:   python3 ArtSource/Places/places.py <key>[@stage][,<key>...] [--draft] [--cell 8] [--only plans,views,sheet] [--remote newton-windows]
       --remote renders on another machine over ssh (the Windows PC on the tailnet renders far faster than this Mac)
Out:   docs/design/places/<key>/  sheet.png (map sheet), views.jpg (contact sheet), img/*.jpg (each render)
Time:  about a minute a design in draft. Iterate by editing the design module and running again.

Stages (system python3 has Pillow but no numpy; Blender's python has numpy but no Pillow):
  1. design  (this python): imports the design module, writes its spec JSON
  2. render  (Blender, numpy): voxelises the spec, meshes the exposed faces (re-meshed per cut, cut faces flat), adds
             lamps, guide strips, dot-matrix signs, holo markers, gates and look-book ships, renders every shot and
             writes meta.json (each shot's camera, so overlays line up)
  3. sheets  (this python, Pillow): the map sheet and the views contact sheet
"""
import importlib
import json
import math
import os
import subprocess
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
BLENDER = "/Applications/Blender.app/Contents/MacOS/Blender"
OUT_ROOT = os.path.join(ROOT, "docs", "design", "places")
WORK_ROOT = os.path.join(ROOT, "Logs", "places")

# 5 x 7 dot-matrix glyphs for the holo signs (rows top to bottom)
GLYPHS = {
    "A": "01110 10001 10001 11111 10001 10001 10001", "B": "11110 10001 10001 11110 10001 10001 11110",
    "C": "01111 10000 10000 10000 10000 10000 01111", "D": "11110 10001 10001 10001 10001 10001 11110",
    "E": "11111 10000 10000 11110 10000 10000 11111", "F": "11111 10000 10000 11110 10000 10000 10000",
    "G": "01111 10000 10000 10011 10001 10001 01111", "H": "10001 10001 10001 11111 10001 10001 10001",
    "I": "11111 00100 00100 00100 00100 00100 11111", "J": "00111 00010 00010 00010 00010 10010 01100",
    "K": "10001 10010 10100 11000 10100 10010 10001", "L": "10000 10000 10000 10000 10000 10000 11111",
    "M": "10001 11011 10101 10101 10001 10001 10001", "N": "10001 11001 10101 10011 10001 10001 10001",
    "O": "01110 10001 10001 10001 10001 10001 01110", "P": "11110 10001 10001 11110 10000 10000 10000",
    "Q": "01110 10001 10001 10001 10101 10010 01101", "R": "11110 10001 10001 11110 10100 10010 10001",
    "S": "01111 10000 10000 01110 00001 00001 11110", "T": "11111 00100 00100 00100 00100 00100 00100",
    "U": "10001 10001 10001 10001 10001 10001 01110", "V": "10001 10001 10001 10001 10001 01010 00100",
    "W": "10001 10001 10001 10101 10101 10101 01010", "X": "10001 10001 01010 00100 01010 10001 10001",
    "Y": "10001 10001 01010 00100 00100 00100 00100", "Z": "11111 00001 00010 00100 01000 10000 11111",
    "0": "01110 10011 10101 10101 11001 10001 01110", "1": "00100 01100 00100 00100 00100 00100 01110",
    "2": "01110 10001 00001 00110 01000 10000 11111", "3": "11110 00001 00001 01110 00001 00001 11110",
    "4": "00010 00110 01010 10010 11111 00010 00010", "5": "11111 10000 11110 00001 00001 10001 01110",
    "6": "00110 01000 10000 11110 10001 10001 01110", "7": "11111 00001 00010 00100 01000 01000 01000",
    "8": "01110 10001 10001 01110 10001 10001 01110", "9": "01110 10001 10001 01111 00001 00010 01100",
    "-": "00000 00000 00000 11111 00000 00000 00000", " ": "00000 00000 00000 00000 00000 00000 00000",
    "'": "00100 00100 01000 00000 00000 00000 00000", ".": "00000 00000 00000 00000 00000 00000 00100",
}


# ======================================================================================================================
# Stage 2: inside Blender
# ======================================================================================================================
def blender_main(spec_path, out_dir, draft, only):
    import bpy
    import numpy as np
    from mathutils import Matrix, Vector
    sys.path.insert(0, os.path.join(ROOT, "ArtSource", "LookDev"))
    import ld_mats
    import ld_scenes
    import ld_world
    from ld_mats import G, lin, new_mat, finish
    from ld_styles import STYLES

    SP = json.load(open(spec_path))
    t_start = time.time()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    S = dict(STYLES["nocturne"])
    S["_key"] = "nocturne"
    ld_scenes.setup_render(S, scene, draft)
    scene.eevee.taa_render_samples = 8 if draft else 40
    ld_world.setup_world(S, scene)
    mats = ld_mats.make_materials(S)
    ld_world.setup_lights(S, scene)
    sd = ld_world.sun_dir(S)
    cell = float(SP["cell"])
    (x0, y0, z0), (x1, y1, z1) = SP["bounds"]
    nx, ny, nz = int(math.ceil((x1 - x0) / cell)), int(math.ceil((y1 - y0) / cell)), int(math.ceil((z1 - z0) / cell))
    print(f"PLACES grid {nx}x{ny}x{nz} = {nx * ny * nz / 1e6:.1f} M cells at {cell} m", flush=True)

    # ------------------------------------------------------------------------------------------ voxelise
    V = np.zeros((nx, ny, nz), np.uint8)
    cx = x0 + (np.arange(nx) + 0.5) * cell
    cy = y0 + (np.arange(ny) + 0.5) * cell
    cz = z0 + (np.arange(nz) + 0.5) * cell

    def idx_range(lo, hi, o, n):
        a = int(max(0, math.floor((lo - o) / cell)))
        b = int(min(n, math.ceil((hi - o) / cell) + 1))
        return a, b

    def sub(lo, hi):
        ia, ib = idx_range(lo[0], hi[0], x0, nx)
        ja, jb = idx_range(lo[1], hi[1], y0, ny)
        ka, kb = idx_range(lo[2], hi[2], z0, nz)
        if ia >= ib or ja >= jb or ka >= kb:
            return None
        X, Y, Z = np.meshgrid(cx[ia:ib], cy[ja:jb], cz[ka:kb], indexing="ij")
        return (slice(ia, ib), slice(ja, jb), slice(ka, kb)), X, Y, Z

    def noise3(X, Y, Z, scale, seed):
        v = np.zeros_like(X)
        for k, (f, a) in enumerate(((1.0, 0.5), (2.1, 0.3), (4.3, 0.2))):
            s = scale / f
            v += a * np.sin(X / s + seed * 1.7 + k) * np.cos(Y / (s * 1.3) - seed * 0.9 + 2 * k) * np.sin(Z / (s * 0.8) + seed + 3 * k)
        return v * 1.8

    STRATA = [SP["mats"][k][0] for k in ("rock", "rock_pale", "rock", "ochre", "rock", "rust", "basalt", "rock")]

    def write(region, mask, mat, op, X=None, Y=None, Z=None):
        sl = region
        blk = V[sl]
        if mat == 0:
            blk[mask] = 0
        elif op.get("strata") and Z is not None:
            band = np.floor((Z + 6 * noise3(X, Y, Z, 90.0, op.get("seed", 0) + 5)) / 22.0).astype(np.int64) % len(STRATA)
            vals = np.array(STRATA, np.uint8)[band]
            blk[mask] = vals[mask]
        else:
            blk[mask] = mat
        V[sl] = blk

    def seg_dist(X, Y, Z, a, b, zs=1.0):
        a = np.array(a, float); b = np.array(b, float)
        d = b - a
        px, py, pz = X - a[0], Y - a[1], (Z - a[2])
        dz = d[2]
        L2 = d[0] ** 2 + d[1] ** 2 + (dz * zs) ** 2
        t = np.clip((px * d[0] + py * d[1] + pz * zs * dz * zs) / max(L2, 1e-9), 0, 1)
        qx, qy, qz = px - t * d[0], py - t * d[1], (pz - t * dz) * zs
        return np.sqrt(qx * qx + qy * qy + qz * qz), t

    for op in SP["ops"]:
        k, mat = op["kind"], op["mat"]
        if k == "ellipsoid":
            c, r = op["c"], op["r"]
            m = 1 + op.get("noise", 0)
            R = sub([c[i] - r[i] * m for i in range(3)], [c[i] + r[i] * m for i in range(3)])
            if not R:
                continue
            sl, X, Y, Z = R
            f = ((X - c[0]) / r[0]) ** 2 + ((Y - c[1]) / r[1]) ** 2 + ((Z - c[2]) / r[2]) ** 2
            if op.get("noise"):
                f = np.sqrt(f) - op["noise"] * noise3(X, Y, Z, op.get("scale", 60.0), op.get("seed", 0))
                mask = f <= 1
            else:
                mask = f <= 1
            if op.get("floor") is not None:
                mask &= Z >= op["floor"]
            write(sl, mask, mat, op, X, Y, Z)
        elif k == "box":
            c, s, yaw = op["c"], op["size"], math.radians(op.get("yaw", 0))
            ext = math.hypot(s[0], s[1]) / 2
            R = sub([c[0] - ext, c[1] - ext, c[2] - s[2] / 2], [c[0] + ext, c[1] + ext, c[2] + s[2] / 2])
            if not R:
                continue
            sl, X, Y, Z = R
            dx, dy = X - c[0], Y - c[1]
            u = dx * math.cos(yaw) - dy * math.sin(yaw)
            v = dx * math.sin(yaw) + dy * math.cos(yaw)
            mask = (np.abs(u) <= s[0] / 2) & (np.abs(v) <= s[1] / 2) & (np.abs(Z - c[2]) <= s[2] / 2)
            write(sl, mask, mat, op, X, Y, Z)
        elif k == "cylinder":
            a, b, r, r2 = op["a"], op["b"], op["r"], op["r2"]
            rm = max(r, r2)
            R = sub([min(a[i], b[i]) - rm for i in range(3)], [max(a[i], b[i]) + rm for i in range(3)])
            if not R:
                continue
            sl, X, Y, Z = R
            A = np.array(a, float); B = np.array(b, float)
            d = B - A
            L2 = float(d @ d)
            t = ((X - A[0]) * d[0] + (Y - A[1]) * d[1] + (Z - A[2]) * d[2]) / max(L2, 1e-9)
            qx, qy, qz = X - A[0] - t * d[0], Y - A[1] - t * d[1], Z - A[2] - t * d[2]
            rr = r + (r2 - r) * np.clip(t, 0, 1)
            mask = (t >= 0) & (t <= 1) & (qx * qx + qy * qy + qz * qz <= rr * rr)
            write(sl, mask, mat, op, X, Y, Z)
        elif k == "tube":
            pts, r, rz = op["pts"], op["r"], op["rz"]
            rm = max(r, rz) * (1 + op.get("noise", 0))
            for a, b in zip(pts, pts[1:]):
                R = sub([min(a[i], b[i]) - rm for i in range(3)], [max(a[i], b[i]) + rm for i in range(3)])
                if not R:
                    continue
                sl, X, Y, Z = R
                d, _ = seg_dist(X, Y, Z, a, b, zs=r / rz)
                if op.get("noise"):
                    d = d - op["noise"] * r * noise3(X, Y, Z, 30.0, op.get("seed", 0))
                write(sl, d <= r, mat, op, X, Y, Z)
        elif k == "torus":
            c, Rr, r, axis = op["c"], op["R"], op["r"], op.get("axis", "z")
            ext = Rr + r
            R = sub([c[0] - ext, c[1] - ext, c[2] - ext], [c[0] + ext, c[1] + ext, c[2] + ext])
            if not R:
                continue
            sl, X, Y, Z = R
            dx, dy, dz = X - c[0], Y - c[1], Z - c[2]
            if axis == "x":
                p, q, h = dy, dz, dx
            elif axis == "y":
                p, q, h = dx, dz, dy
            else:
                p, q, h = dx, dy, dz
            rho = np.sqrt(p * p + q * q)
            mask = (rho - Rr) ** 2 + h * h <= r * r
            if op.get("arc"):
                brg = (np.degrees(np.arctan2(p, q)) + 360) % 360
                a0, a1 = op["arc"]
                mask &= ((brg - a0) % 360) <= ((a1 - a0) % 360)
            write(sl, mask, mat, op, X, Y, Z)
        elif k == "loft":
            ax = {"x": 0, "y": 1, "z": 2}[op["axis"]]
            secs = sorted(op["sections"], key=lambda q: q[0])
            o = [x0, y0, z0]
            nn = [nx, ny, nz]
            cc = [cx, cy, cz]
            ua, va = [a for a in range(3) if a != ax]
            s0, s1 = secs[0][0], secs[-1][0]
            ia, ib = idx_range(s0, s1, o[ax], nn[ax])
            if ia >= ib:
                continue
            spos = cc[ax][ia:ib]
            sp = np.array([q[0] for q in secs], float)
            P = {key: np.interp(spos, sp, np.array([q[i] for q in secs], float)) for i, key in
                 ((1, "u"), (2, "v"), (3, "w"), (4, "h"), (5, "n"))}
            umax = max(abs(q[1]) + q[3] for q in secs)
            vmax = max(abs(q[2]) + q[4] for q in secs)
            ub = idx_range(-umax, umax, o[ua], nn[ua])
            vb = idx_range(-vmax, vmax, o[va], nn[va])
            if ub[0] >= ub[1] or vb[0] >= vb[1]:
                continue
            Uc, Vc_ = cc[ua][ub[0]:ub[1]], cc[va][vb[0]:vb[1]]
            shape = [0, 0, 0]
            shape[ax], shape[ua], shape[va] = ib - ia, len(Uc), len(Vc_)

            def along(arr, a):
                sh = [1, 1, 1]; sh[a] = len(arr)
                return arr.reshape(sh)
            du = np.abs(along(Uc, ua) - along(P["u"], ax))
            dv = np.abs(along(Vc_, va) - along(P["v"], ax))
            Wd, Hd, N = along(P["w"], ax), along(P["h"], ax), along(P["n"], ax)
            f = (du / np.maximum(Wd, 1e-6)) ** N + (dv / np.maximum(Hd, 1e-6)) ** N
            mask = np.broadcast_to(f <= 1, shape).copy()
            if op.get("shell"):
                t = op["shell"]
                fi = (du / np.maximum(Wd - t, 1e-6)) ** N + (dv / np.maximum(Hd - t, 1e-6)) ** N
                inner = np.broadcast_to((fi <= 1) & (Wd > t) & (Hd > t), shape)
            sl = [None, None, None]
            sl[ax], sl[ua], sl[va] = slice(ia, ib), slice(*ub), slice(*vb)
            sl = tuple(sl)
            write(sl, mask, mat, op)
            if op.get("shell"):
                write(sl, inner.copy(), 0, op)
        elif k == "prism":
            ax = {"x": 0, "y": 1, "z": 2}[op["axis"]]
            ua, va = [a for a in range(3) if a != ax]
            o = [x0, y0, z0]
            nn = [nx, ny, nz]
            cc = [cx, cy, cz]
            poly = np.array(op["poly"], float)
            ia, ib = idx_range(min(op["a0"], op["a1"]), max(op["a0"], op["a1"]), o[ax], nn[ax])
            ub = idx_range(poly[:, 0].min(), poly[:, 0].max(), o[ua], nn[ua])
            vb = idx_range(poly[:, 1].min(), poly[:, 1].max(), o[va], nn[va])
            if ia >= ib or ub[0] >= ub[1] or vb[0] >= vb[1]:
                continue
            Uu, Vv = np.meshgrid(cc[ua][ub[0]:ub[1]], cc[va][vb[0]:vb[1]], indexing="ij")
            inside = np.zeros(Uu.shape, bool)
            for (pa, pb) in zip(poly, np.roll(poly, -1, 0)):
                cond = (pa[1] > Vv) != (pb[1] > Vv)
                xint = pa[0] + (Vv - pa[1]) * (pb[0] - pa[0]) / ((pb[1] - pa[1]) or 1e-9)
                inside ^= cond & (Uu < xint)
            shape = [0, 0, 0]
            shape[ax], shape[ua], shape[va] = ib - ia, Uu.shape[0], Uu.shape[1]
            m2 = inside.reshape([1 if a == ax else (Uu.shape[0] if a == ua else Uu.shape[1]) for a in range(3)])
            mask = np.broadcast_to(m2, shape).copy()
            sl = [None, None, None]
            sl[ax], sl[ua], sl[va] = slice(ia, ib), slice(*ub), slice(*vb)
            write(tuple(sl), mask, mat, op)
    solid = V > 0
    print(f"PLACES voxels {int(solid.sum())} solid in {time.time() - t_start:.1f}s", flush=True)

    # ------------------------------------------------------------------------------------------ materials
    def voxel_material(name, hexcol, rough=0.7, metal=0.1, var=0.12):
        """Flat colour with a per-voxel tone (the cell is found from the face position pushed half a cell inward)."""
        m = new_mat(name)
        g = G(m.node_tree)
        geo = g.n("ShaderNodeNewGeometry")
        p = g.vmath("SUBTRACT", g.out(geo, "Position"), g.vmath("SCALE", g.out(geo, "Normal"), scale=cell * 0.5))
        cellp = g.vmath("FLOOR", g.vmath("SCALE", p, scale=1.0 / cell))
        wn = g.n("ShaderNodeTexWhiteNoise", p_noise_dimensions="3D")
        g.set(wn.inputs["Vector"], cellp)
        k = g.maprange(wn.outputs["Value"], 0.0, 1.0, 1.0 - var, 1.0 + var)
        base = lin(hexcol)
        col = g.n("ShaderNodeCombineColor")
        for i, ch in enumerate(("Red", "Green", "Blue")):
            g.set(col.inputs[ch], g.math("MULTIPLY", k, base[i]))
        # an inset seam per voxel face: darker near the cell edges
        f = g.vmath("FRACTION", g.vmath("SCALE", g.out(geo, "Position"), scale=1.0 / cell))
        sep = g.sep(f)
        sepn = g.sep(g.out(geo, "Normal"))
        edge = None
        for ch in ("X", "Y", "Z"):
            v = g.math("MINIMUM", sep[ch], g.math("SUBTRACT", 1.0, sep[ch]))
            v = g.math("ADD", v, g.math("ABSOLUTE", sepn[ch]))
            edge = v if edge is None else g.math("MINIMUM", edge, v)
        seam = g.maprange(g.math("MAXIMUM", edge, 0.0), 0.0, 0.04, 0.55, 1.0)
        bsdf = g.n("ShaderNodeBsdfPrincipled", Roughness=rough, Metallic=metal)
        sc = g.n("ShaderNodeCombineColor")
        for ch in ("Red", "Green", "Blue"):
            g.set(sc.inputs[ch], seam)
        res = g.mix(1.0, col.outputs[0], sc.outputs[0], blend="MULTIPLY")
        g.set(g.sock(bsdf.inputs, "Base Color"), res)
        return finish(g, bsdf.outputs[0], m)

    def emit(name, hexcol, strength):
        return ld_mats.flat_material(S, name, hexcol, 0.4, 0.0, emit=(hexcol, strength))

    def holo(name, hexcol, strength, base=0.15):
        m = new_mat(name)
        g = G(m.node_tree)
        lw = g.n("ShaderNodeLayerWeight", Blend=0.35)
        edge = g.math("ADD", g.math("MULTIPLY", g.math("SUBTRACT", 1.0, g.out(lw, "Facing")), 1.0), base)
        em = g.n("ShaderNodeEmission")
        g.set(em.inputs[0], lin(hexcol))
        g.set(em.inputs[1], g.math("MULTIPLY", edge, strength))
        tr = g.n("ShaderNodeBsdfTransparent")
        add = g.n("ShaderNodeAddShader")
        g.set(add.inputs[0], em.outputs[0])
        g.set(add.inputs[1], tr.outputs[0])
        return finish(g, add.outputs[0], m, blended=True)

    MAT_BY_ID = {}
    METALLIC = {"hull": 0.35, "hull2": 0.2, "armor": 0.55, "trim": 0.5, "plate_dark": 0.5, "obsidian": 0.6, "gold": 0.9,
                "pearl": 0.15, "glass": 0.1, "deck": 0.4}
    for key, (mid, hexcol) in SP["mats"].items():
        MAT_BY_ID[mid] = voxel_material("vox_" + key, hexcol, 0.35 if key in ("obsidian", "glass", "pearl", "gold") else 0.72,
                                        METALLIC.get(key, 0.05), 0.05 if key in ("obsidian", "pearl", "glass") else 0.12)
    SECTION_MAT = voxel_material("vox_section", "#5A4A40", 0.9, 0.0, 0.04)
    WINDOW_MAT = mats["window"]
    EM = {}

    def em(hexcol, strength=12.0):
        k = (hexcol, strength)
        if k not in EM:
            EM[k] = emit(f"em_{hexcol}_{strength}", hexcol, strength)
        return EM[k]

    HOLO = {}

    def hm(hexcol, strength=4.0, base=0.15):
        k = (hexcol, strength, base)
        if k not in HOLO:
            HOLO[k] = holo(f"holo_{hexcol}_{strength}_{base}", hexcol, strength, base)
        return HOLO[k]

    # ------------------------------------------------------------------------------------------ meshing
    HULLISH = {SP["mats"][k][0] for k in ("hull", "hull2", "armor", "plate_dark", "obsidian", "jade", "pearl")}
    mat_ids = sorted(MAT_BY_ID)
    slot_of = {mid: i for i, mid in enumerate(mat_ids)}
    SECTION_SLOT = len(mat_ids)
    DIRS = [((1, 0, 0), 0), ((-1, 0, 0), 0), ((0, 1, 0), 1), ((0, -1, 0), 1), ((0, 0, 1), 2), ((0, 0, -1), 2)]

    def quads_for(Vc, cut_axis=None, cut_index=None):
        """Exposed faces of Vc as (corners, material slot, is-window-candidate)."""
        sol = Vc > 0
        allq, alls, allw = [], [], []
        for (d, ax) in DIRS:
            nb = np.zeros_like(sol)
            src = [slice(None)] * 3
            dst = [slice(None)] * 3
            if d[ax] > 0:
                src[ax] = slice(1, None); dst[ax] = slice(0, -1)
            else:
                src[ax] = slice(0, -1); dst[ax] = slice(1, None)
            nb[tuple(dst)] = sol[tuple(src)]
            face = sol & ~nb
            ii, jj, kk = np.nonzero(face)
            if len(ii) == 0:
                continue
            mids = Vc[ii, jj, kk]
            lut = np.zeros(256, np.int32)
            for mid, sidx in slot_of.items():
                lut[mid] = sidx
            sl = lut[mids]
            if cut_axis is not None and ax == cut_axis and d[ax] > 0:
                at_cut = (np.array([ii, jj, kk])[ax] == cut_index - 1)
                sl = np.where(at_cut, SECTION_SLOT, sl)
            # corners
            base = np.stack([x0 + ii * cell, y0 + jj * cell, z0 + kk * cell], 1).astype(np.float32)
            o = np.zeros(3, np.float32)
            if d[ax] > 0:
                o[ax] = cell
            u_ax, v_ax = [a for a in range(3) if a != ax]
            U = np.zeros(3, np.float32); U[u_ax] = cell
            W = np.zeros(3, np.float32); W[v_ax] = cell
            p0 = base + o
            if (d[ax] > 0) == ((ax == 1)):
                c4 = [p0, p0 + W, p0 + U + W, p0 + U]
            else:
                c4 = [p0, p0 + U, p0 + U + W, p0 + W]
            q = np.stack(c4, 1)
            allq.append(q); alls.append(sl)
            wcand = (ax != 2) & np.isin(mids, list(HULLISH)) & (sl != SECTION_SLOT)
            allw.append(np.where(wcand, ax * 2 + (d[ax] > 0), -1))
        return np.concatenate(allq), np.concatenate(alls), np.concatenate(allw)

    def make_mesh(name, quads, slots, mlist):
        n = len(quads)
        me = bpy.data.meshes.new(name)
        me.vertices.add(n * 4)
        me.vertices.foreach_set("co", quads.reshape(-1).astype(np.float32))
        me.loops.add(n * 4)
        me.loops.foreach_set("vertex_index", np.arange(n * 4, dtype=np.int32))
        me.polygons.add(n)
        me.polygons.foreach_set("loop_start", np.arange(0, n * 4, 4, dtype=np.int32))
        me.polygons.foreach_set("loop_total", np.full(n, 4, np.int32))
        me.polygons.foreach_set("material_index", slots.astype(np.int32))
        me.update(calc_edges=True)
        me.validate()
        for m in mlist:
            me.materials.append(m)
        ob = bpy.data.objects.new(name, me)
        scene.collection.objects.link(ob)
        return ob

    MLIST = [MAT_BY_ID[m] for m in mat_ids] + [SECTION_MAT]
    rng = np.random.default_rng(3)

    def windows_mesh(name, quads, wdir):
        sel = np.nonzero(wdir >= 0)[0]
        if len(sel) == 0:
            return None
        keep = sel[rng.random(len(sel)) < 0.22 * (cell / 8.0) ** 2 * 0.5 * SP.get("window_density", 1.0)]
        keep = keep[:40000]
        q = quads[keep]
        c = q.mean(1)
        d = wdir[keep]
        ax = d // 2
        sgn = np.where(d % 2 == 1, 1.0, -1.0)
        out = np.zeros((len(keep), 3), np.float32)
        out[np.arange(len(keep)), ax] = sgn * 0.15
        c = c + out
        # a horizontal slit: along the face's horizontal axis, cell*0.55 long, cell*0.1 tall, at a deck row
        hor = np.where(ax == 0, 1, 0)
        H = np.zeros((len(keep), 3), np.float32)
        H[np.arange(len(keep)), hor] = cell * 0.28
        Zv = np.zeros((len(keep), 3), np.float32); Zv[:, 2] = cell * 0.05
        row = (rng.integers(0, 3, len(keep)) - 1) * cell * 0.25
        c[:, 2] += row
        qq = np.stack([c - H - Zv, c + H - Zv, c + H + Zv, c - H + Zv], 1)
        return make_mesh(name, qq, np.zeros(len(keep), np.int32), [WINDOW_MAT])

    MESHES = {}

    def voxel_objects(cut=None):
        """The voxel mesh (and its windows) with everything beyond a cut removed. cut: ('z', h) / ('y', at) / ('x', at)."""
        key = tuple(cut) if cut else None
        if key in MESHES:
            return MESHES[key]
        if cut is None:
            Vc, ax, ci = V, None, None
        else:
            axn = {"x": 0, "y": 1, "z": 2}[cut[0]]
            o = (x0, y0, z0)[axn]
            ci = int(np.clip(round((cut[1] - o) / cell), 1, V.shape[axn]))
            Vc = V.copy()
            sl = [slice(None)] * 3
            sl[axn] = slice(ci, None)
            Vc[tuple(sl)] = 0
            ax = axn
        t0 = time.time()
        q, s, w = quads_for(Vc, ax, ci)
        ob = make_mesh(f"vox_{key}", q, s, MLIST)
        wo = windows_mesh(f"win_{key}", q, w)
        print(f"PLACES mesh {key}: {len(q)} faces in {time.time() - t0:.1f}s", flush=True)
        MESHES[key] = [o for o in (ob, wo) if o]
        return MESHES[key]

    # ------------------------------------------------------------------------------------------ props
    PROPS = []          # (object, z-min) so cut views can hide what lies above the cut

    def add_prop_mesh(name, verts, faces, mat):
        me = bpy.data.meshes.new(name)
        me.from_pydata([tuple(v) for v in verts], [], faces)
        me.materials.append(mat)
        ob = bpy.data.objects.new(name, me)
        scene.collection.objects.link(ob)
        zs = [v[2] for v in verts]
        PROPS.append((ob, min(zs), max(zs), min(v[0] for v in verts), min(v[1] for v in verts)))
        return ob

    class Acc:
        def __init__(self):
            self.v, self.f = [], []

        def box(self, c, size, R=None):
            hx, hy, hz = size[0] / 2, size[1] / 2, size[2] / 2
            pts = [Vector((sx * hx, sy * hy, sz * hz)) for sx in (-1, 1) for sy in (-1, 1) for sz in (-1, 1)]
            if R is not None:
                pts = [R @ p for p in pts]
            o = len(self.v)
            self.v += [tuple(Vector(c) + p) for p in pts]
            self.f += [tuple(o + i for i in f) for f in ((0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3))]

        def seg(self, a, b, w, h=None):
            a, b = Vector(a), Vector(b)
            d = b - a
            L = d.length
            if L < 1e-6:
                return
            fwd = d / L
            up = Vector((0, 0, 1)) if abs(fwd.z) < 0.95 else Vector((1, 0, 0))
            side = fwd.cross(up).normalized()
            upv = side.cross(fwd).normalized()
            self.box((a + b) / 2, (w, L, h or w), Matrix((side, fwd, upv)).transposed())

        def torus(self, c, R0, r, Rm=None, n=64, m=8):
            o = len(self.v)
            for i in range(n):
                a = 2 * math.pi * i / n
                for j in range(m):
                    b = 2 * math.pi * j / m
                    p = Vector((math.cos(a) * (R0 + r * math.cos(b)), math.sin(a) * (R0 + r * math.cos(b)), r * math.sin(b)))
                    self.v.append(tuple(Vector(c) + (Rm @ p if Rm else p)))
            for i in range(n):
                for j in range(m):
                    i2, j2 = (i + 1) % n, (j + 1) % m
                    self.f.append((o + i * m + j, o + i2 * m + j, o + i2 * m + j2, o + i * m + j2))

        def sphere(self, c, r, n=24, m=12):
            o = len(self.v)
            for j in range(m + 1):
                t = math.pi * j / m
                for i in range(n):
                    a = 2 * math.pi * i / n
                    self.v.append((c[0] + r * math.sin(t) * math.cos(a), c[1] + r * math.sin(t) * math.sin(a), c[2] + r * math.cos(t)))
            for j in range(m):
                for i in range(n):
                    i2 = (i + 1) % n
                    self.f.append((o + j * n + i, o + j * n + i2, o + (j + 1) * n + i2, o + (j + 1) * n + i))

    def flush(acc, name, mat):
        if acc.v:
            return add_prop_mesh(name, acc.v, acc.f, mat)

    lights = []
    for li, L in enumerate(SP["lamps"]):
        a = Acc()
        a.box(L["p"], (2.2, 2.2, 2.2))
        flush(a, f"lamp{li}", em(L["colour"], 30))
        lights.append(L)
    lights.sort(key=lambda L: -L.get("power", 1.0))
    for li, L in enumerate(lights[:180]):
        h = L["colour"].lstrip("#")
        rgb = tuple(int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))
        ob = ld_scenes.point_light(scene, f"pl{li}", L["p"], rgb, 6e4 * L.get("power", 1.0) * (cell / 8.0) ** 2, 4.0)
        ob.data.use_shadow = li < 24
        PROPS.append((ob, L["p"][2], L["p"][2], L["p"][0], L["p"][1]))

    for si, st in enumerate(SP["strips"]):
        a = Acc()
        pts = st["pts"]
        for p, q in zip(pts, pts[1:]):
            a.seg(p, q, 1.4, 0.5)
        flush(a, f"strip{si}", em(st["colour"], 14))
        # the flowing line beside it: dashes
        b = Acc()
        for p, q in zip(pts, pts[1:]):
            L = math.dist(p, q)
            n = max(1, int(L / 10))
            for k in range(n):
                t0, t1 = k / n, (k + 0.45) / n
                b.seg([p[i] + (q[i] - p[i]) * t0 for i in range(3)], [p[i] + (q[i] - p[i]) * t1 for i in range(3)], 0.7, 0.3)
        o = flush(b, f"dash{si}", em(st["colour"], 6))
        if o:
            o.location = (0, 0, 1.2)

    for ri, rt in enumerate(SP["routes"]):
        if rt["kind"] not in ("lane", "patrol", "escape"):
            continue
        a = Acc()
        pts = rt["pts"]
        acc_d = 0.0
        for p, q in zip(pts, pts[1:]):
            L = math.dist(p, q)
            step = 18.0
            k = 0
            while k * step < L:
                t = k * step / L
                c = [p[i] + (q[i] - p[i]) * t for i in range(3)]
                a.box(c, (1.6, 1.6, 1.6))
                k += 1
        flush(a, f"route{ri}", em(rt["colour"], 4 if rt["kind"] == "lane" else 6))

    def sign_mesh(sg, si):
        a = Acc()
        s = sg["size"]
        dot = s / 7.0
        yaw = math.radians(sg["yaw"])
        f = Vector((math.sin(yaw), math.cos(yaw), 0))
        right = (-f).cross(Vector((0, 0, 1))).normalized()
        text = sg["text"]
        width = len(text) * 6 * dot
        origin = Vector(sg["p"]) - right * width / 2
        for ci, ch in enumerate(text):
            rows = GLYPHS.get(ch, GLYPHS[" "]).split()
            for r, row in enumerate(rows):
                for col, bit in enumerate(row):
                    if bit == "1":
                        p = origin + right * ((ci * 6 + col) * dot) + Vector((0, 0, (6 - r) * dot))
                        a.box(p, (dot * 0.7, dot * 0.7, dot * 0.7))
        flush(a, f"sign{si}", em(sg["colour"], 16))

    for si, sg in enumerate(SP["signs"]):
        sign_mesh(sg, si)

    for hi, H in enumerate(SP["holos"]):
        a = Acc()
        a.torus(H["p"], H["r"], 0.5, n=48, m=6)
        a.torus(H["p"], H["r"] * 0.7, 0.3, n=48, m=6)
        flush(a, f"holoring{hi}", em(H["colour"], 10))
        b = Acc()
        c = Vector(H["p"]) + Vector((0, 0, H["r"] * 1.1))
        rr = H["r"] * 0.55
        # an octahedral emblem
        vs = [c + Vector(v) * rr for v in ((1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1.3), (0, 0, -1.3))]
        b.v += [tuple(v) for v in vs]
        b.f += [(0, 2, 4), (2, 1, 4), (1, 3, 4), (3, 0, 4), (2, 0, 5), (1, 2, 5), (3, 1, 5), (0, 3, 5)]
        flush(b, f"emblem{hi}", hm(H["colour"], 6, 0.35))

    for gi, gt in enumerate(SP["gates"]):
        Rm = Matrix.Rotation(math.radians(-gt["facing"]), 3, "Z") @ Matrix.Rotation(math.radians(gt["tilt"]), 3, "X")
        a = Acc()
        a.torus(gt["c"], gt["R"], gt["R"] * 0.07, Rm, n=96, m=10)
        flush(a, f"gate{gi}", MAT_BY_ID[SP["mats"]["armor"][0]] if gt["lit"] else MAT_BY_ID[SP["mats"]["trim"][0]])
        b = Acc()
        for k in range(16):
            ang = 2 * math.pi * k / 16
            b.box(Vector(gt["c"]) + Rm @ Vector((math.cos(ang) * gt["R"] * 0.9, math.sin(ang) * gt["R"] * 0.9, 0)), (3, 3, 3))
        flush(b, f"gatel{gi}", em("#FFF1D6" if gt["lit"] else "#5A4A30", 20 if gt["lit"] else 2))
        if gt["lit"]:
            c = Acc()
            c.torus(gt["c"], gt["R"] * 0.86, gt["R"] * 0.02, Rm, n=96, m=6)
            flush(c, f"gateg{gi}", hm("#A8CCFF", 3.0))

    for mi, mk in enumerate(SP["markers"]):
        a = Acc()
        for Rm in (Matrix.Identity(3), Matrix.Rotation(math.pi / 2, 3, "X"), Matrix.Rotation(math.pi / 2, 3, "Y")):
            a.torus(mk["p"], mk["r"], max(0.6, mk["r"] * 0.004), Rm, n=96, m=4)
        flush(a, f"marker{mi}", hm(mk["colour"], 2.5, 0.6))

    for pi, pl in enumerate(SP.get("planets", [])):
        PP = dict(S["planet"])
        PP["kind"] = pl.get("kind", PP["kind"])
        ld_world.planet(S, f"planet{pi}", pl["r"], tuple(pl["p"]), PP, sd)

    # look-book ships, built once per class and instanced
    import ld_ships
    D = dict(ld_ships.DESIGNS["nocturne"])
    SHIPCOLL = {}

    def ship_coll(cls, lit=True):
        k = (cls, lit)
        if k in SHIPCOLL:
            return SHIPCOLL[k]
        coll = bpy.data.collections.new(f"ship_{cls}_{lit}")
        root, size = ld_scenes.build_ship(D[cls](), S, mats, ld_scenes.Things(), f"proto_{cls}_{lit}", plumes=lit)
        objs = [root] + list(root.children_recursive)
        for o in objs:
            for c in list(o.users_collection):
                c.objects.unlink(o)
            coll.objects.link(o)
        SHIPCOLL[k] = coll
        return coll

    def place_ship(p, yaw, pitch, cls="light", lit=True, name="ship", track=True):
        inst = bpy.data.objects.new(name, None)
        inst.instance_type = "COLLECTION"
        inst.instance_collection = ship_coll(cls, lit)
        inst.location = p
        inst.rotation_euler = (math.radians(pitch), 0, math.radians(-yaw))
        scene.collection.objects.link(inst)
        if track:
            PROPS.append((inst, p[2] - 4, p[2] + 4, p[0] - 4, p[1] - 4))
        return inst

    for si, sh in enumerate(SP["ships"]):
        place_ship(sh["p"], sh["yaw"], sh.get("pitch", 0.0), sh.get("cls", "light"), sh.get("lit", True), f"ship{si}")

    # ------------------------------------------------------------------------------------------ shots
    cam = ld_scenes.make_camera(scene)
    meta = dict(key=SP["key"], cell=cell, bounds=SP["bounds"], shots=[])
    img_dir = os.path.join(out_dir, "img")
    os.makedirs(img_dir, exist_ok=True)
    W_IMG, H_IMG = (1024, 576) if draft else (1920, 1080)
    hero_objs = []

    def show_only(cut):
        objs = voxel_objects(cut)
        for k, lst in MESHES.items():
            for o in lst:
                o.hide_render = o not in objs
        for (o, zmin, zmax, xmin, ymin) in PROPS:
            hidden = False
            if cut and cut[0] == "z":
                hidden = zmin > cut[1] + 2
            elif cut and cut[0] == "y":
                hidden = ymin > cut[1] - 2 or o.name.startswith("route")
            elif cut and cut[0] == "x":
                hidden = xmin > cut[1] - 2 or o.name.startswith("route")
            o.hide_render = hidden

    def set_hero(h):
        for o in hero_objs:
            bpy.data.objects.remove(o, do_unlink=True)
        hero_objs.clear()
        if h:
            hero_objs.append(place_ship(h[0], h[1], h[2], "light", True, "hero", track=False))

    def shoot(name, kind, eye, target, fov=50.0, cut=None, ortho=None, size=None, exposure=0.0, hero=None, caption="", rot=0):
        if only and kind not in only and not (kind in ("inside", "view", "flight") and "views" in only) and not (kind == "flight" and "flights" in only):
            return
        show_only(cut)
        set_hero(hero)
        ld_scenes.aim(cam, tuple(eye), tuple(target), fov_v=None if ortho else fov, ortho=ortho)
        if ortho:
            cam.data.sensor_fit = "AUTO"
            if rot:
                cam.rotation_euler.z += math.radians(rot)
        scene.view_settings.exposure = S.get("exposure", 0.0) + exposure
        w, h = size or (W_IMG, H_IMG)
        path = os.path.join(img_dir, name + ".jpg")
        t0 = time.time()
        ld_scenes.render(scene, path, size=(w, h))
        meta["shots"].append(dict(name=name, kind=kind, path=path, eye=list(eye), target=list(target), fov=fov,
                                  ortho=ortho, size=[w, h], cut=list(cut) if cut else None, caption=caption, rot=rot))
        print(f"PLACES shot {name} {time.time() - t0:.1f}s", flush=True)

    bx, by, bz = (x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2
    ext = max(x1 - x0, y1 - y0, z1 - z0)
    # overview from the south-east, above
    shoot("overview", "overview", (bx + ext * 0.75, by - ext * 0.95, bz + ext * 0.55), (bx, by, bz - ext * 0.05), 40,
          caption="Arrival")
    # plans: top-down, cut away above each level
    span_x, span_y = x1 - x0, y1 - y0
    asp = span_x / span_y
    PW = 1800 if not draft else 1200
    rot = 90 if asp < 0.8 else 0            # a tall place is drawn on its side: north to the right
    a_img = asp if rot == 0 else 1 / asp
    psize = (PW, int(PW / a_img)) if a_img >= 1 else (int(PW * a_img), PW)
    for li, L in enumerate(SP["levels"]):
        shoot(f"plan{li}", "plans", (bx, by, z1 + 500), (bx, by + 0.001, z0), cut=("z", L["z"]),
              ortho=max(span_x, span_y), size=psize, exposure=0.5, caption=f"Plan: {L['name']} (cut at {L['z']:+.0f} m)", rot=rot)
    for si, sc in enumerate(SP["sections"]):
        if sc["axis"] == "y":
            eye, tgt = (bx, sc["at"] - 2000, bz), (bx, sc["at"], bz)
            sw, sh_ = span_x, z1 - z0
        else:
            eye, tgt = (sc["at"] - 2000, by, bz), (sc["at"], by, bz)
            sw, sh_ = span_y, z1 - z0
        a2 = sw / sh_
        ssize = (PW, int(PW / a2)) if a2 >= 1 else (int(PW * a2), PW)
        # a section looks at the cut face: remove everything on the camera's side of the cut
        cutspec = (sc["axis"], sc["at"])
        # cut keeps cells below 'at'; the camera must be on the + side: put it there
        if sc["axis"] == "y":
            eye = (bx, sc["at"] + 2000, bz)
        else:
            eye = (sc["at"] + 2000, by, bz)
        shoot(f"section{si}", "plans", eye, tgt, cut=cutspec, ortho=max(sw, sh_), size=ssize, exposure=0.6,
              caption=f"Section: {sc['name']}")
    # inside shots at each point of interest
    for P in SP["pois"]:
        if not P.get("inside", True):
            continue
        if P.get("view"):
            eye, tgt = P["view"]
        else:
            p = P["p"]
            eye, tgt = (p[0] + 70, p[1] - 90, p[2] + 40), p
        e, t = Vector(eye), Vector(tgt)
        d = (t - e).normalized()
        hero_p = e + d * 34 + Vector((0, 0, -6))
        yaw = math.degrees(math.atan2(d.x, d.y))
        pitch = math.degrees(math.asin(max(-0.9, min(0.9, d.z))))
        shoot(f"poi{P['n']:02d}", "inside", eye, tgt, 58, exposure=0.25, hero=(tuple(hero_p), yaw, pitch),
              caption=f"{P['n']}  {P['name']}")
    for vi, vw in enumerate(SP["views"]):
        shoot(f"view{vi:02d}", "view", vw["eye"], vw["target"], vw.get("fov", 50), cut=("z", vw["cut"]) if vw.get("cut") else None,
              exposure=0.5, hero=vw.get("hero"), caption=vw["name"])
    # ------------------------------------------------------------------ flight lines: clearance and fly-through frames
    solid_idx = np.argwhere(V > 0)

    def clearance(p, R=96.0):
        """Distance from p to the nearest solid voxel's centre, minus half a cell (capped at R)."""
        i0 = np.floor((np.array(p) - np.array([x0, y0, z0])) / cell).astype(int)
        r = int(R / cell) + 1
        lo = np.maximum(i0 - r, 0)
        hi = np.minimum(i0 + r + 1, [nx, ny, nz])
        if np.any(lo >= hi):
            return R
        blk = V[lo[0]:hi[0], lo[1]:hi[1], lo[2]:hi[2]]
        ii = np.argwhere(blk > 0)
        if len(ii) == 0:
            return R
        cpos = (ii + lo + 0.5) * cell + np.array([x0, y0, z0])
        return float(max(0.0, min(R, np.sqrt(((cpos - np.array(p)) ** 2).sum(1)).min() - cell * 0.5)))

    def resample(pts, step):
        out = [list(pts[0])]
        for a, b in zip(pts, pts[1:]):
            L = math.dist(a, b)
            n = max(1, int(L / step))
            out += [[a[i] + (b[i] - a[i]) * k / n for i in range(3)] for k in range(1, n + 1)]
        return out

    meta["flights"] = []
    for rt in SP["routes"]:
        if rt["kind"] != "flight":
            continue
        samples = resample(rt["pts"], 10.0)
        clr = [clearance(q) for q in samples]
        entry = dict(name=rt["name"], colour=rt["colour"], samples=[q + [c] for q, c in zip(samples, clr)], frames=[],
                     length=sum(math.dist(a, b) for a, b in zip(rt["pts"], rt["pts"][1:])), note=rt.get("note", ""))
        nf = rt.get("frames", 6)
        if not only or "flights" in only or "views" in only:
            L = len(samples)
            for k in range(nf):
                j = min(L - 2, int((k + 0.5) * (L - 1) / nf))
                p, q = Vector(samples[j]), Vector(samples[min(L - 1, j + 3)])
                d = (q - p).normalized()
                up = Vector((0, 0, 1)) if abs(d.z) < 0.9 else Vector((0, 1, 0))
                side = d.cross(up).normalized()
                upv = side.cross(d).normalized()
                eye = p - d * 46 + upv * 14
                tgt = p + d * 80
                yaw = math.degrees(math.atan2(d.x, d.y))
                pitch = math.degrees(math.asin(max(-0.95, min(0.95, d.z))))
                nm = f"flight_{len(meta['flights'])}_{k}"
                shoot(nm, "flight", tuple(eye), tuple(tgt), 62, exposure=0.25, hero=(tuple(p), yaw, pitch),
                      size=(640, 360), caption=f"{rt['name']} {k + 1}/{nf}")
                entry["frames"].append(dict(name=nm, t=j / (L - 1), clear=clr[j]))
        meta["flights"].append(entry)
        print(f"PLACES flight {rt['name']}: {entry['length']:.0f} m, clearance min {min(clr):.0f} m, mean {sum(clr) / len(clr):.0f} m", flush=True)

    json.dump(meta, open(os.path.join(out_dir, "meta.json"), "w"), indent=1)
    print(f"PLACES done {SP['key']} in {time.time() - t_start:.1f}s", flush=True)


# ======================================================================================================================
# Stage 3: sheets (Pillow)
# ======================================================================================================================
def draw_sheets(spec, out_dir):
    from PIL import Image, ImageDraw, ImageFont
    sys.path.insert(0, os.path.join(ROOT, "ArtSource", "Racing"))
    import racing_common as RC
    meta = json.load(open(os.path.join(out_dir, "meta.json")))
    shots = {s["name"]: s for s in meta["shots"]}

    def F(path, size):
        return ImageFont.truetype(path, size)
    f_title, f_h, f_b, f_s, f_n = F(RC.F_TITLE, 48), F(RC.F_TITLE, 22), F(RC.F_BODY, 17), F(RC.F_BODY, 14), F(RC.F_BOLD, 18)

    def hexrgb(h, a=255):
        h = h.lstrip("#")
        return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)

    DIST = {"hearth": "#2EC4B6", "berths": "#FFB347", "breaker": "#FF6A2A", "trade": "#FF2E88", "foundry": "#FF8A2A",
            "gates": "#A8CCFF", "arrival": "#FFF1D6", "danger": "#FF463C", "vault": "#9FD8FF", "lime": "#9DFF3C",
            "violet": "#B89CFF", "white": "#DCEBFF", "amber": "#FFAE4A"}

    def ortho_map(shot, img_w, img_h):
        """World (x, y, z) to image pixels for an ortho shot."""
        ex, ey, ez = shot["eye"]
        tx, ty, tz = shot["target"]
        sw, sh = shot["size"]
        scale = shot["ortho"] / max(sw, sh)            # metres per pixel
        if abs(ez - tz) > 1 and shot.get("rot"):        # top-down on its side: y right, x down
            return lambda p: ((p[1] - ty) / scale * img_w / sw + img_w / 2, (p[0] - tx) / scale * img_h / sh + img_h / 2)
        if abs(ez - tz) > 1:                            # top-down: x right, y up
            return lambda p: ((p[0] - tx) / scale * img_w / sw + img_w / 2, -(p[1] - ty) / scale * img_h / sh + img_h / 2)
        if abs(ey - ty) > 1:                            # looking along -y (camera at +y): x to the left
            return lambda p: (-(p[0] - tx) / scale * img_w / sw + img_w / 2, -(p[2] - tz) / scale * img_h / sh + img_h / 2)
        return lambda p: ((p[1] - ty) / scale * img_w / sw + img_w / 2, -(p[2] - tz) / scale * img_h / sh + img_h / 2)

    def overlay(img, shot, pois=True, routes=True, zcut=None, num_r=20):
        d = ImageDraw.Draw(img, "RGBA")
        M = ortho_map(shot, img.width, img.height)
        if routes:
            for fl in meta.get("flights", []):
                sm = fl["samples"]
                for a, b in zip(sm, sm[1:]):
                    c = min(a[3], b[3])
                    col = (255, 70, 60, 235) if c < 20 else (242, 179, 61, 235) if c < 40 else (139, 214, 74, 235)
                    d.line([M(a), M(b)], fill=col, width=5)
            for rt in spec["routes"]:
                if rt["kind"] == "flight":
                    continue
                pts = [M(p) for p in rt["pts"]]
                col = hexrgb(rt["colour"], 210)
                w = 4 if rt["kind"] in ("lane", "escape") else 3
                for a, b in zip(pts, pts[1:]):
                    if rt["kind"] in ("patrol", "escape"):
                        n = max(1, int(math.dist(a, b) / 14))
                        for k in range(n):
                            if k % 2 == 0:
                                t0, t1 = k / n, (k + 1) / n
                                d.line([(a[0] + (b[0] - a[0]) * t0, a[1] + (b[1] - a[1]) * t0),
                                        (a[0] + (b[0] - a[0]) * t1, a[1] + (b[1] - a[1]) * t1)], fill=col, width=w)
                    else:
                        d.line([a, b], fill=col, width=w)
                if len(pts) > 1 and rt["kind"] != "patrol":
                    a, b = pts[-2], pts[-1]
                    ang = math.atan2(b[1] - a[1], b[0] - a[0])
                    for s in (-1, 1):
                        d.line([b, (b[0] - 14 * math.cos(ang + s * 0.5), b[1] - 14 * math.sin(ang + s * 0.5))], fill=col, width=w)
        if pois:
            for P in spec["pois"]:
                x, y = M(P["p"])
                if not (0 <= x < img.width and 0 <= y < img.height):
                    continue
                col = hexrgb(DIST.get(P["district"], "#FFFFFF"))
                faded = zcut is not None and P["p"][2] > zcut + 30
                a = 110 if faded else 255
                d.ellipse([x - num_r, y - num_r, x + num_r, y + num_r], fill=(8, 9, 8, 200 if not faded else 90),
                          outline=col[:3] + (a,), width=3)
                t = str(P["n"])
                tw = d.textlength(t, font=f_n)
                d.text((x - tw / 2, y - 11), t, font=f_n, fill=(236, 230, 216, a))

    # ---------------------------------------------------------------- the map sheet
    W, H = 3300, 2100
    sheet = Image.new("RGB", (W, H), (4, 5, 6))
    RC_star = getattr(RC, "starfield", None)
    d = ImageDraw.Draw(sheet, "RGBA")
    d.text((50, 40), spec["title"].upper(), font=f_title, fill=(236, 230, 216))
    d.text((52, 110), spec["subtitle"], font=f_b, fill=(169, 163, 149))
    plans = [s for s in meta["shots"] if s["name"].startswith("plan")]
    secs = [s for s in meta["shots"] if s["name"].startswith("section")]
    # main plan: left, big
    left_w, top = 2040, 160
    if plans:
        main = plans[0]
        im = Image.open(main["path"]).convert("RGB")
        k = min(left_w / im.width, 1240 / im.height)
        im = im.resize((int(im.width * k), int(im.height * k)))
        overlay(im, main, zcut=main["cut"][1] if main["cut"] else None, num_r=19)
        sheet.paste(im, (50, top))
        d.text((54, top + im.height + 8), main["caption"], font=f_s, fill=(169, 163, 149))
        # scale bar: 200 m
        mpp = main["ortho"] / max(main["size"]) / k * (main["size"][0] / Image.open(main["path"]).width)
        L = 200 / mpp
        y = top + im.height + 40
        d.line([(54, y), (54 + L, y)], fill=(236, 230, 216), width=3)
        for xx in (54, 54 + L / 2, 54 + L):
            d.line([(xx, y - 8), (xx, y + 8)], fill=(236, 230, 216), width=2)
        d.text((62 + L, y - 10), "200 m", font=f_s, fill=(236, 230, 216))
        y_small = y + 40
    else:
        y_small = top
    # the other plans and the sections: a strip under the main plan
    small = plans[1:] + secs
    x = 50
    row_h = H - y_small - 40
    for s in small:
        im = Image.open(s["path"]).convert("RGB")
        k = min(row_h / im.height, 660 / im.width)
        im = im.resize((int(im.width * k), int(im.height * k)))
        overlay(im, s, routes=s["name"].startswith("plan"), zcut=s["cut"][1] if s["cut"] and s["cut"][0] == "z" else None, num_r=12)
        if x + im.width > left_w + 50:
            break
        sheet.paste(im, (x, y_small))
        d.text((x + 2, y_small + im.height + 4), s["caption"], font=f_s, fill=(169, 163, 149))
        x += im.width + 24
    # right panel
    px = 2160
    d.rectangle([px - 30, 0, W, H], fill=(9, 10, 10))
    y = 50
    notes = spec.get("notes", {})

    def para(title, text, colour=(242, 179, 61)):
        nonlocal y
        d.text((px, y), title.upper(), font=f_h, fill=colour)
        y += 38
        for line in RC.wrap(text, 98):
            d.text((px, y), line, font=f_s, fill=(214, 208, 196))
            y += 21
        y += 14

    def bullets(title, items, colour, mark):
        nonlocal y
        if not items:
            return
        d.text((px, y), title.upper(), font=f_h, fill=colour)
        y += 38
        for it in items:
            lines = RC.wrap(it, 94)
            for i, line in enumerate(lines):
                d.text((px, y), (mark + " " if i == 0 else "   ") + line, font=f_s, fill=colour if i == 0 and False else (214, 208, 196))
                y += 21
        y += 14

    if notes.get("idea"):
        para("The idea", notes["idea"])
    d.text((px, y), "ON THE MAP", font=f_h, fill=(242, 179, 61))
    y += 38
    for P in spec["pois"]:
        col = hexrgb(DIST.get(P["district"], "#FFFFFF"))
        d.ellipse([px, y, px + 24, y + 24], outline=col, width=2)
        t = str(P["n"])
        d.text((px + 12 - d.textlength(t, font=f_s) / 2, y + 3), t, font=f_s, fill=(236, 230, 216))
        lines = RC.wrap(f"{P['name']}: {P['text']}", 90)
        for i, line in enumerate(lines):
            d.text((px + 36, y + 2 + i * 20), line, font=f_s, fill=(214, 208, 196))
        y += max(30, 20 * len(lines) + 8)
    y += 10
    bullets("How it plays", notes.get("plays", []), (159, 224, 200), "·")
    bullets("Strengths", notes.get("strengths", []), (139, 214, 74), "+")
    bullets("Costs", notes.get("costs", []), (242, 179, 61), "–")
    if notes.get("key"):
        d.text((px, y), "KEY", font=f_h, fill=(242, 179, 61))
        y += 36
        for label, colour in notes["key"]:
            d.rectangle([px, y + 3, px + 14, y + 17], fill=hexrgb(colour))
            d.text((px + 24, y), label, font=f_s, fill=(214, 208, 196))
            y += 22
    d.text((px, H - 40), f"places.py · {spec['key']} · voxels at {spec['cell']:.0f} m · every solid cell is a block and can be cut",
           font=f_s, fill=(110, 112, 104))
    sheet.save(os.path.join(out_dir, "sheet.png"))

    # ---------------------------------------------------------------- the views contact sheet
    views = [s for s in meta["shots"] if s["kind"] in ("overview", "inside", "view")]
    cols = 3
    tw, th = 1000, 563
    rows = (len(views) + cols - 1) // cols
    cs = Image.new("RGB", (cols * tw + (cols + 1) * 16, rows * (th + 44) + 90), (8, 9, 10))
    dc = ImageDraw.Draw(cs)
    dc.text((16, 20), spec["title"].upper() + " · VIEWS", font=F(RC.F_TITLE, 30), fill=(236, 230, 216))
    for i, s in enumerate(views):
        im = Image.open(s["path"]).convert("RGB").resize((tw, th))
        xx = 16 + (i % cols) * (tw + 16)
        yy = 80 + (i // cols) * (th + 44)
        cs.paste(im, (xx, yy))
        dc.text((xx + 2, yy + th + 8), s["caption"].upper(), font=F(RC.F_BOLD, 20), fill=(236, 230, 216))
    cs.save(os.path.join(out_dir, "views.jpg"), quality=88)

    # ---------------------------------------------------------------- the flight lines sheet
    flights = meta.get("flights", [])
    if flights:
        fw, fh = 640, 360
        nfr = max(len(f["frames"]) for f in flights) or 1
        Wf = 40 + nfr * (fw + 12) + 360
        Hf = 90 + len(flights) * (fh + 70)
        fs = Image.new("RGB", (Wf, Hf), (8, 9, 10))
        dfs = ImageDraw.Draw(fs)
        dfs.text((20, 20), spec["title"].upper() + " · FLIGHT LINES", font=F(RC.F_TITLE, 30), fill=(236, 230, 216))
        for r, f in enumerate(flights):
            y0r = 80 + r * (fh + 70)
            clr = [s_[3] for s_ in f["samples"]]
            dfs.text((20, y0r), f"{f['name'].upper()}  ·  {f['length']:.0f} m  ·  clearance min {min(clr):.0f} m, "
                     f"mean {sum(clr) / len(clr):.0f} m", font=F(RC.F_BOLD, 20), fill=hexrgb(f["colour"])[:3])
            # clearance profile along the line
            bx0, by0, bw, bh = 20 + nfr * (fw + 12), y0r + 34, 330, fh
            dfs.rectangle([bx0, by0, bx0 + bw, by0 + bh], outline=(60, 62, 58))
            for lvl, col in ((20, (255, 70, 60)), (40, (242, 179, 61))):
                yy = by0 + bh - lvl / 96 * bh
                dfs.line([(bx0, yy), (bx0 + bw, yy)], fill=col + (255,), width=1)
                dfs.text((bx0 + bw - 60, yy - 16), f"{lvl} m", font=F(RC.F_BODY, 13), fill=col)
            pts = [(bx0 + i / max(1, len(clr) - 1) * bw, by0 + bh - min(c, 96) / 96 * bh) for i, c in enumerate(clr)]
            dfs.line(pts, fill=hexrgb(f["colour"])[:3], width=2)
            dfs.text((bx0, by0 + bh + 6), "clearance along the line (start to end)", font=F(RC.F_BODY, 13), fill=(150, 154, 144))
            if f.get("note"):
                for i, line in enumerate(RC.wrap(f["note"], 150)[:2]):
                    dfs.text((20, y0r + fh + 40 + i * 16), line, font=F(RC.F_BODY, 14), fill=(190, 186, 176))
            for k, fr in enumerate(f["frames"]):
                sh = shots.get(fr["name"])
                if not sh:
                    continue
                im = Image.open(sh["path"]).convert("RGB").resize((fw, fh))
                fs.paste(im, (20 + k * (fw + 12), y0r + 34))
                c = fr["clear"]
                col = (255, 70, 60) if c < 20 else (242, 179, 61) if c < 40 else (139, 214, 74)
                dfs.text((26 + k * (fw + 12), y0r + 40), f"{c:.0f} m clear", font=F(RC.F_BOLD, 16), fill=col)
        fs.save(os.path.join(out_dir, "flights.jpg"), quality=88)
        print("SHEETS", os.path.join(out_dir, "flights.jpg"))
    print("SHEETS", os.path.join(out_dir, "sheet.png"), os.path.join(out_dir, "views.jpg"))


# ======================================================================================================================
# Driver
# ======================================================================================================================
REMOTE_ROOT = "C:/Users/Frank/places_work"
REMOTE_BLENDER = "C:/Program Files/Blender Foundation/Blender 5.1/blender.exe"


def render_remote(host, key, spec_path, out_dir, draft, only, log):
    """Render on another machine over ssh (the Windows PC on the tailnet: RTX 3060, Blender 5.1): copy the scripts and
    the spec, run Blender there, copy the renders and meta.json back, and point meta.json at the local files."""
    rdir = f"{REMOTE_ROOT}/out/{key}"
    sh = lambda cmd: subprocess.call(["ssh", host, cmd], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    sh(f'cmd /c "if not exist {REMOTE_ROOT.replace("/", chr(92))} mkdir {REMOTE_ROOT.replace("/", chr(92))}"')
    tar = subprocess.Popen(["tar", "czf", "-", "--exclude", "__pycache__", "--exclude", "*.blend*", "-C", ROOT,
                            "ArtSource/LookDev", "ArtSource/Places"], stdout=subprocess.PIPE)
    subprocess.call(["ssh", host, f"tar xzf - -C {REMOTE_ROOT}"], stdin=tar.stdout)
    tar.wait()
    subprocess.call(["scp", "-q", spec_path, f"{host}:{REMOTE_ROOT}/{key}.json"])
    sh(f'cmd /c "if exist {rdir.replace("/", chr(92))} rmdir /s /q {rdir.replace("/", chr(92))}"')
    args = f'"{REMOTE_BLENDER}" -b --factory-startup --python {REMOTE_ROOT}/ArtSource/Places/places.py -- --blender {REMOTE_ROOT}/{key}.json {rdir}'
    if draft:
        args += " --draft"
    if only:
        args += f" --only {only}"
    with open(log, "w") as fh:
        subprocess.call(["ssh", host, args], stdout=fh, stderr=subprocess.STDOUT)
    os.makedirs(os.path.join(out_dir, "img"), exist_ok=True)
    pull = subprocess.Popen(["ssh", host, f"tar czf - -C {rdir} ."], stdout=subprocess.PIPE)
    subprocess.call(["tar", "xzf", "-", "-C", out_dir], stdin=pull.stdout)
    pull.wait()
    mp = os.path.join(out_dir, "meta.json")
    if os.path.exists(mp):
        meta = json.load(open(mp))
        for shot in meta["shots"]:
            shot["path"] = os.path.join(out_dir, "img", os.path.basename(shot["path"].replace(chr(92), "/")))
        json.dump(meta, open(mp, "w"), indent=1)
    return True


def main():
    if "--blender" in sys.argv:
        a = sys.argv[sys.argv.index("--") + 1:]
        only = None
        if "--only" in a:
            only = a[a.index("--only") + 1].split(",")
        blender_main(a[1], a[2], "--draft" in a, only)
        return
    args = [x for x in sys.argv[1:] if not x.startswith("--")]
    draft = "--draft" in sys.argv
    only = sys.argv[sys.argv.index("--only") + 1] if "--only" in sys.argv else None
    if only:
        args = [x for x in args if x != only]
    remote = sys.argv[sys.argv.index("--remote") + 1] if "--remote" in sys.argv else None
    if remote:
        args = [x for x in args if x != remote]
    cell = float(sys.argv[sys.argv.index("--cell") + 1]) if "--cell" in sys.argv else None
    if cell:
        args = [x for x in args if x != sys.argv[sys.argv.index("--cell") + 1]]
    sys.path.insert(0, os.path.join(HERE, "designs"))
    sys.path.insert(0, HERE)
    for key in args[0].split(","):
        stage = None
        if "@" in key:                                  # den_rift@2b: the design at a growth stage
            key, stage = key.split("@")
        mod = importlib.import_module(key)
        place = mod.design(stage) if stage else mod.design()
        if stage:
            key = f"{key}_{stage}"
            place.key = key
        if cell:
            place.cell = cell
        spec = place.spec()
        out_dir = os.path.join(OUT_ROOT, key)
        work = os.path.join(WORK_ROOT, key)
        os.makedirs(out_dir, exist_ok=True)
        os.makedirs(work, exist_ok=True)
        sp = os.path.join(work, "spec.json")
        json.dump(spec, open(sp, "w"))
        if only != "sheet":
            log = os.path.join(work, "blender.log")
            t0 = time.time()
            if remote:
                ok = render_remote(remote, key, sp, out_dir, draft, only, log)
            else:
                cmd = [BLENDER, "-b", "--factory-startup", "--python", os.path.abspath(__file__), "--", "--blender", sp, out_dir]
                if draft:
                    cmd.append("--draft")
                if only:
                    cmd += ["--only", only]
                with open(log, "w") as fh:
                    subprocess.call(cmd, stdout=fh, stderr=subprocess.STDOUT)
            txt = open(log, encoding="utf-8", errors="replace").read()
            ok = "PLACES done" in txt
            print(f"{key}: render {'ok' if ok else 'FAILED'} in {time.time() - t0:.0f}s ({remote or 'local'})  log={log}", flush=True)
            for line in txt.splitlines():
                if line.startswith("PLACES ") and ("grid" in line or "voxels" in line):
                    print("  " + line)
            if not ok:
                print("\n".join([l for l in txt.splitlines() if "Error" in l or "Traceback" in l or "line " in l][-14:]))
                continue
        draw_sheets(spec, out_dir)


if __name__ == "__main__":
    main()
