#!/usr/bin/env python3
"""ART-004 block-kit style test (SPEC-80 §9 ART-004, DESIGN-SYSTEM §4, §x.3–§x.5, §10.6, §10.11, §10.15).

Two modes in one file:

  python3 ArtSource/StyleTest/build_style_test.py [--direction drydock|nocturne|pulp-horizon ...] [--blender PATH]
      Driver (system Python + Pillow): runs Blender once per direction, then composes composite.png.

  Blender -b --factory-startup --python build_style_test.py -- --render <direction>
      Inside Blender: generates the 256 px hull_plain / hull_heavy layers procedurally (numpy), builds the five blocks
      (block_hull and block_hull_angled as 4 × 4 × 1 patches, block_thruster_small, weapon_plasma_turret_small with base
      and yaw part, block_core_small), lights them per §x.4 and renders them with an orthographic camera pitched 55° at the
      §4 reference densities (14.8, 4.9 and 2.5 px per cell), plus a 148 px-per-cell close-up and the damage strip.
      Writes ArtSource/StyleTest/<direction>/style_test.blend and docs/design/board/img/blocks/<direction>/.

Units: 1 cell = 0.5 m. Blender Z is up; a block's business direction (Unity +Z) is Blender +Y.
Light intensities are multiplied by π: a Blender sun of strength 1 lights white diffuse to 1/π, URP's to 1.
Glyphs, bands and stencils are modelled as thin decal geometry (counted in the triangle budget), not trim-sheet textures.
"""
import json
import math
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", ".."))
OUT_ROOT = os.path.join(ROOT, "docs", "design", "board", "img", "blocks")
DIRECTIONS = ["drydock", "nocturne", "pulp-horizon"]
BLOCKS = ["block_hull", "block_hull_angled", "block_thruster_small", "weapon_plasma_turret_small", "block_core_small"]
DISTANCES = {"005": 148.0, "050": 14.8, "150": 4.9, "300": 2.5}       # DESIGN-SYSTEM §4 pixels per cell
CELL = 0.5
FOOTPRINT = {"block_hull": 16, "block_hull_angled": 16, "block_thruster_small": 2, "weapon_plasma_turret_small": 2, "block_core_small": 1}


def budget(cells):
    """DESIGN-SYSTEM §10.11 LOD0 triangles for a footprint; patches are bricks (≤ 2 per exposed face)."""
    return 150 if cells <= 1 else 600 if cells <= 4 else 1500 if cells <= 18 else 3000 if cells <= 64 else 5000


# ----------------------------------------------------------------------------------------------------------------------
# Direction looks (values from DESIGN-SYSTEM §5–§7; ARK expedition team colours)
# ----------------------------------------------------------------------------------------------------------------------

LOOKS = {
    "drydock": dict(
        name="DRYDOCK",
        backdrop="#040507", ambient="#14171C", view="Khronos PBR Neutral", saturation=0.90, contrast=0.0,
        bloom=(1.1, 0.25), key=("#FFF8F2", 2.0, 35), fill=("#8FA6C8", 0.3, 50, "opposite"),
        plate=dict(color="#CFC8B8", metal=0.05, smooth=0.40), heavy=dict(color="#6C7075", metal=0.6, smooth=0.40),
        frame="#3B3E42", bare="#9A9C9E", cavity="#1E2023", seam="#2A2C2F", seam_px=3, seam_emit=None,
        primary="#CFC8B8", secondary="#3B3E42", accent="#F2B705",
        body=dict(color="#6C7075", metal=0.6, smooth=0.42), body_trim="#3B3E42",
        functions=dict(thruster=("#FF9A1F", 1.5), weapon=("#E5332A", 2.0), core=("#3DDC84", 3.0)),
        glyph_mode="stencil", glyph_color="#E9E2D0", stencil=True, chamfer=0.06, strip=True,
        damage=dict(scorch="#3A332C", char="#141210", ember=("#FF5A1A", 4.0), mode="soot"),
        rim=None, toon=False, outline=None,
    ),
    "nocturne": dict(
        name="NOCTURNE",
        backdrop="#03040A", ambient="#0A0E18", view="ACES", saturation=1.0, contrast=0.10,
        bloom=(0.8, 0.9), key=("#FFF4EA", 1.6, 25), fill=("#5FD0FF", 0.56, 20, "rim"),
        plate=dict(color="#1B1E24", metal=0.85, smooth=0.70), heavy=dict(color="#424954", metal=0.60, smooth=0.55),
        frame="#6B7380", bare="#5D636B", cavity="#08090C", seam="#101216", seam_px=2, seam_emit=("#FFE9C7", 0.35),
        primary="#2C3139", secondary="#6B7380", accent="#FFE9C7",
        body=dict(color="#1B1E24", metal=0.9, smooth=0.72), body_trim="#6B7380",
        functions=dict(thruster=("#A98BFF", 3.0), weapon=("#FF3D6E", 3.0), core=("#F4F8FF", 4.0)),
        glyph_mode="emissive", glyph_color=None, stencil=False, chamfer=0.03, strip=True,
        damage=dict(scorch="#0A0B0E", char="#050608", ember=("#FFFFFF", 6.0), crack2=("#CFF6FF", 3.0), mode="light"),
        rim=("#5FD0FF", 0.35), toon=False, outline=None,
    ),
    "pulp-horizon": dict(
        name="PULP HORIZON",
        backdrop="#0E1C2E", ambient=("#1F4E5F", "#163049", "#0E1C2E"), view="Khronos PBR Neutral", saturation=1.05, contrast=0.0,
        bloom=(1.6, 0.12), key=("#FFF2DE", 1.8, 45), fill=("#1F4E5F", 0.54, 45, "opposite"),
        plate=dict(color="#EFE4C8", metal=0.0, smooth=0.30), heavy=dict(color="#D9C9A3", metal=0.0, smooth=0.30),
        frame="#5F7F86", bare="#9FA8B0", cavity="#161B26", seam="#2A3140", seam_px=2, seam_emit=None, ink="#2A3140",
        primary="#EFE4C8", secondary="#5F7F86", accent="#FFC233",
        body=None, body_trim="#2A3140",
        functions=dict(thruster=("#FF8A2B", 1.5), weapon=("#E2483D", 0.0), core=("#3FB950", 1.5)),
        glyph_mode="print", glyph_color="#F6EEDC", stencil=False, chamfer=0.15, strip=False,
        damage=dict(scorch="#2A3140", char="#2A3140", ember=("#FF8A2B", 1.5), mode="halftone"),
        rim=None, toon=True, outline=("#1A2030", 1.5),
    ),
}


def hex_rgb(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4))


def srgb_to_linear(c):
    return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c)


def mix_socket(sockets, name):
    """ShaderNodeMix has one A/B/Result socket per data type; pick the colour one by identifier."""
    for sock in sockets:
        if sock.identifier == f"{name}_Color":
            return sock
    return sockets[name]


# ======================================================================================================================
# Inside Blender
# ======================================================================================================================

def blender_main(direction):
    import bpy
    import bmesh
    import numpy as np
    from mathutils import Vector, Matrix

    look = LOOKS[direction]
    out_dir = os.path.join(OUT_ROOT, direction)
    src_dir = os.path.join(HERE, direction)
    tex_dir = os.path.join(src_dir, "textures")
    os.makedirs(out_dir, exist_ok=True)
    os.makedirs(tex_dir, exist_ok=True)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version = 0       # no .blend1 backups
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.unit_settings.system = "METRIC"
    rng = np.random.default_rng(80 + DIRECTIONS.index(direction))
    S = 256

    # ------------------------------------------------------------------ textures (numpy, 256 px, macroTile 2 = 128 px cells)
    def lin(hexstr):
        return np.array(srgb_to_linear(hex_rgb(hexstr)), dtype=np.float32)

    def save_image(name, rgba, colorspace, depth="8"):
        """rgba: float32 [S,S,4], row 0 = bottom (Blender convention). Saves PNG and returns the loaded image."""
        path = os.path.join(tex_dir, name + ".png")
        img = bpy.data.images.new(name, S, S, alpha=True, float_buffer=(depth == "16"))
        img.colorspace_settings.name = "Non-Color"            # values are written as file values: no colour conversion on save
        if colorspace == "srgb":
            rgb = rgba[..., :3]
            rgba = np.concatenate([np.where(rgb <= 0.0031308, rgb * 12.92, 1.055 * np.power(np.clip(rgb, 0, None), 1 / 2.4) - 0.055), rgba[..., 3:]], -1)
        img.pixels.foreach_set(np.clip(rgba, 0, 1).astype(np.float32).ravel())
        img.filepath_raw = path
        img.file_format = "PNG"
        img.save()
        bpy.data.images.remove(img)
        loaded = bpy.data.images.load(path, check_existing=False)
        loaded.colorspace_settings.name = "sRGB" if colorspace == "srgb" else "Non-Color"
        loaded.name = name
        return loaded

    yy, xx = np.mgrid[0:S, 0:S]
    cx, cy = xx % 128, yy % 128

    def voronoi_edges(points, seed_scale=1.0):
        d1 = np.full((S, S), 1e9, np.float32)
        d2 = np.full((S, S), 1e9, np.float32)
        for px, py in points:
            for ox in (-S, 0, S):
                for oy in (-S, 0, S):
                    d = np.hypot(xx - (px + ox), yy - (py + oy)).astype(np.float32)
                    closer = d < d1
                    d2 = np.where(closer, d1, np.minimum(d2, d))
                    d1 = np.where(closer, d, d1)
        return (d2 - d1) * 0.5

    crack_points = rng.uniform(0, S, size=(7, 2))
    crack_dist = voronoi_edges(crack_points)
    crack_keep = rng.uniform(0, 1, size=(7,)) < 0.5          # stage 2 shows about half of the crack network
    nearest = np.argmin(np.stack([np.min(np.stack([np.hypot(xx - (p[0] + ox), yy - (p[1] + oy)) for ox in (-S, 0, S) for oy in (-S, 0, S)]), 0) for p in crack_points]), 0)

    def layer_maps(kind, stage):
        """Albedo (linear rgb), mask (metal, smooth, paint, emissive), normal (tangent xyz), emission (linear rgb ×8 scale)."""
        spec = look["plate"] if kind == "plain" else look["heavy"]
        base = lin(spec["color"])
        albedo = np.broadcast_to(base, (S, S, 3)).copy()
        metal = np.full((S, S), spec["metal"], np.float32)
        smooth = np.full((S, S), spec["smooth"], np.float32)
        emission = np.zeros((S, S, 3), np.float32)
        nx = np.zeros((S, S), np.float32)
        ny = np.zeros((S, S), np.float32)

        if direction != "pulp-horizon":
            albedo *= (1 + (rng.normal(0, 0.012, (S, S, 1)))).astype(np.float32)   # faint paint grain
        # Macro-panel seam (2 × 2 cells = whole texture).
        sp = look["seam_px"]
        seam = (xx < sp) | (yy < sp) | (xx >= S - sp) | (yy >= S - sp)
        albedo[seam] = lin(look["seam"])
        smooth[seam] = 0.25
        if look["seam_emit"] and stage == 0:
            emission[seam] = lin(look["seam_emit"][0]) * look["seam_emit"][1]
        if look["seam_emit"] and stage == 1:
            panel_on = rng.uniform() > 0.05 * 4          # flicker: a panel's seams are off in this frame
            if panel_on:
                emission[seam] = lin(look["seam_emit"][0]) * look["seam_emit"][1]
        # Cell bevel: 4 texels in the normal map; DRYDOCK adds a 1-texel highlight top-left at 15 %.
        bevel = 4
        nx = np.where(cx < bevel, -0.6, np.where(cx >= 128 - bevel, 0.6, 0)).astype(np.float32)
        ny = np.where(cy < bevel, -0.6, np.where(cy >= 128 - bevel, 0.6, 0)).astype(np.float32)
        if direction == "drydock":
            hi = ((cx == 0) | (cy == 127)) & ~seam
            albedo[hi] *= 1.15
        if direction == "pulp-horizon":
            light = ((cx < 2) | (cy >= 126)) & ~seam
            dark = ((cx >= 126) | (cy < 2)) & ~seam
            albedo[light] = np.minimum(albedo[light] * 1.2, 1)
            albedo[dark] = lin(look["ink"])
        # Heavy plates.
        if kind == "heavy":
            if direction == "drydock":
                for bx, by in ((20, 20), (236, 20), (20, 236), (236, 236)):
                    r = np.hypot(xx - bx, yy - by)
                    bolt = r < 7
                    albedo[bolt] = lin("#55585C")
                    metal[bolt] = 0.9
                    smooth[bolt] = 0.5
                    nx = np.where(bolt, (xx - bx) / 9.0, nx)
                    ny = np.where(bolt, (yy - by) / 9.0, ny)
            else:
                inset = (np.minimum(np.minimum(xx, S - 1 - xx), np.minimum(yy, S - 1 - yy)) == 24)
                nx = np.where(inset & ((xx == 24) | (xx == S - 25)), np.where(xx < 128, -0.7, 0.7), nx)
                ny = np.where(inset & ((yy == 24) | (yy == S - 25)), np.where(yy < 128, -0.7, 0.7), ny)
                if direction == "pulp-horizon":
                    albedo[inset] = lin(look["ink"])
                else:
                    albedo[inset] *= 0.6

        # ---- damage (DESIGN-SYSTEM §x.3 damage tables)
        dmg = look["damage"]
        edge = np.minimum(np.minimum(cx, 127 - cx), np.minimum(cy, 127 - cy))
        if dmg["mode"] == "soot":
            scorch = [0, 0.30, 0.60, 0.85][stage]
            albedo = albedo * (1 - scorch) + lin(dmg["scorch"]) * scorch
            wear_px = 0 if stage == 0 else 5 * (1.4 if stage >= 1 else 1)
            wear = (edge < wear_px) & ~seam & (rng.uniform(0, 1, (S, S)) < 0.55)
            albedo[wear] = lin(look["bare"])
            metal[wear] = 1.0
            smooth[wear] = 0.55
            smooth = np.clip(smooth - (0.10 if stage >= 1 else 0), 0, 1)
        elif dmg["mode"] == "light":
            smooth = np.clip(smooth - (0.15 if stage >= 1 else 0), 0, 1)
            if stage >= 2:
                emission[:] = 0
        crack = np.zeros((S, S), bool)
        if stage >= 2:
            width = 1.6 if direction == "pulp-horizon" else 1.8 + (1.0 if stage == 3 else 0)
            crack = crack_dist < width
            if stage == 2:
                crack &= crack_keep[nearest]
            if dmg["mode"] == "soot":
                albedo[crack] = lin(dmg["char"])
                smooth[crack] = 0.1
                if stage == 3:
                    core = crack_dist < 0.9
                    emission[core] = lin(dmg["ember"][0]) * dmg["ember"][1]
            elif dmg["mode"] == "light":
                albedo[crack] = lin(dmg["char"])
                glow = dmg["crack2"] if stage == 2 else dmg["ember"]
                emission[crack] = lin(glow[0]) * glow[1]
            else:
                albedo[crack] = lin(look["ink"])
                if stage == 3:
                    dots = (((xx // 16) + (yy // 16)) % 5 == 0) & (np.hypot(xx % 16 - 8, yy % 16 - 8) < 3.5)
                    albedo[dots] = lin(dmg["ember"][0])
                    emission[dots] = lin(dmg["ember"][0]) * dmg["ember"][1]
        nz = np.sqrt(np.clip(1 - nx * nx - ny * ny, 0, 1))
        normal = np.stack([nx, ny, nz], -1)
        normal /= np.linalg.norm(normal, axis=-1, keepdims=True)
        mask = np.stack([metal, smooth, np.where(seam, 0, 1).astype(np.float32), (emission.max(-1) > 0).astype(np.float32)], -1)
        return albedo.astype(np.float32), mask.astype(np.float32), normal.astype(np.float32), emission.astype(np.float32)

    textures = {}
    tex_stats = []
    for kind in ("plain", "heavy"):
        for stage in range(4):
            albedo, mask, normal, emission = layer_maps(kind, stage)
            one = np.ones((S, S, 1), np.float32)
            name = f"hull_{kind}_s{stage}"
            textures[(kind, stage)] = dict(
                albedo=save_image(name + "_albedo", np.concatenate([albedo, one], -1), "srgb"),
                mask=save_image(name + "_mask", mask, "linear"),
                normal=save_image(name + "_normal", np.concatenate([normal * 0.5 + 0.5, one], -1), "linear", depth="16"),
                emission=save_image(name + "_emission", np.concatenate([np.clip(emission / 8.0, 0, 1), one], -1), "linear"),
            )
            for m in ("albedo", "mask", "normal", "emission"):
                tex_stats.append(dict(name=f"{name}_{m}.png", size=[S, S]))

    # ------------------------------------------------------------------ materials
    def node_tree(mat):
        mat.use_nodes = True
        nt = mat.node_tree
        for n in list(nt.nodes):
            nt.nodes.remove(n)
        out = nt.nodes.new("ShaderNodeOutputMaterial")
        return nt, out

    halftone_nodes = []   # (multiply node) updated per render resolution

    def toon_shader(nt, color_socket, normal_socket=None, emission_socket=None, halftone=0.0):
        """PULP HORIZON §7.4/§7.6: 3-step toon ramp of albedo (0.35 / 0.70 / 1.0); shadow tone leans toward Ink."""
        diffuse = nt.nodes.new("ShaderNodeBsdfDiffuse")
        diffuse.inputs["Color"].default_value = (1, 1, 1, 1)
        if normal_socket is not None:
            nt.links.new(normal_socket, diffuse.inputs["Normal"])
        to_rgb = nt.nodes.new("ShaderNodeShaderToRGB")
        nt.links.new(diffuse.outputs[0], to_rgb.inputs[0])
        bw = nt.nodes.new("ShaderNodeRGBToBW")
        nt.links.new(to_rgb.outputs["Color"], bw.inputs[0])
        scale = nt.nodes.new("ShaderNodeMath")
        scale.operation = "DIVIDE"
        scale.inputs[1].default_value = 0.70   # key light ≈ 0.7 of full at 45° pitch on top faces → full step
        nt.links.new(bw.outputs[0], scale.inputs[0])
        ramp = nt.nodes.new("ShaderNodeValToRGB")
        ramp.color_ramp.interpolation = "CONSTANT"
        ramp.color_ramp.elements[0].position = 0.0
        ramp.color_ramp.elements[0].color = (0.35, 0.35, 0.35, 1)
        ramp.color_ramp.elements[1].position = 0.33
        ramp.color_ramp.elements[1].color = (0.70, 0.70, 0.70, 1)
        e3 = ramp.color_ramp.elements.new(0.66)
        e3.color = (1, 1, 1, 1)
        nt.links.new(scale.outputs[0], ramp.inputs["Fac"])
        ink = nt.nodes.new("ShaderNodeMix")
        ink.data_type = "RGBA"
        mix_socket(ink.inputs, "A").default_value = (*srgb_to_linear(hex_rgb(look["ink"])), 1)
        nt.links.new(color_socket, mix_socket(ink.inputs, "B"))
        nt.links.new(ramp.outputs["Color"], ink.inputs["Factor"])
        final = mix_socket(ink.outputs, "Result")
        if halftone > 0:
            # Screen-aligned dots (DESIGN-SYSTEM §7.3 damage): 6 px cells rotated 45°, dot area = density.
            coord = nt.nodes.new("ShaderNodeTexCoord")
            mul = nt.nodes.new("ShaderNodeVectorMath")
            mul.operation = "MULTIPLY"
            nt.links.new(coord.outputs["Window"], mul.inputs[0])
            halftone_nodes.append(mul)
            rot = nt.nodes.new("ShaderNodeVectorRotate")
            rot.rotation_type = "Z_AXIS"
            rot.inputs["Angle"].default_value = math.radians(45)
            nt.links.new(mul.outputs[0], rot.inputs["Vector"])
            frac = nt.nodes.new("ShaderNodeVectorMath")
            frac.operation = "FRACTION"
            nt.links.new(rot.outputs[0], frac.inputs[0])
            sub = nt.nodes.new("ShaderNodeVectorMath")
            sub.operation = "SUBTRACT"
            sub.inputs[1].default_value = (0.5, 0.5, 0)
            nt.links.new(frac.outputs[0], sub.inputs[0])
            length = nt.nodes.new("ShaderNodeVectorMath")
            length.operation = "LENGTH"
            nt.links.new(sub.outputs[0], length.inputs[0])
            less = nt.nodes.new("ShaderNodeMath")
            less.operation = "LESS_THAN"
            less.inputs[1].default_value = math.sqrt(halftone / math.pi)
            nt.links.new(length.outputs["Value"], less.inputs[0])
            dots = nt.nodes.new("ShaderNodeMix")
            dots.data_type = "RGBA"
            nt.links.new(final, mix_socket(dots.inputs, "A"))
            mix_socket(dots.inputs, "B").default_value = (*srgb_to_linear(hex_rgb(look["ink"])), 1)
            nt.links.new(less.outputs[0], dots.inputs["Factor"])
            final = mix_socket(dots.outputs, "Result")
        shade = nt.nodes.new("ShaderNodeEmission")
        nt.links.new(final, shade.inputs["Color"])
        shader = shade.outputs[0]
        if emission_socket is not None:
            emit = nt.nodes.new("ShaderNodeEmission")
            emit.inputs["Strength"].default_value = 8.0
            nt.links.new(emission_socket, emit.inputs["Color"])
            add = nt.nodes.new("ShaderNodeAddShader")
            nt.links.new(shader, add.inputs[0])
            nt.links.new(emit.outputs[0], add.inputs[1])
            shader = add.outputs[0]
        return shader

    def add_rim(nt, shader):
        if not look["rim"]:
            return shader
        weight = nt.nodes.new("ShaderNodeLayerWeight")
        weight.inputs["Blend"].default_value = 0.5
        power = nt.nodes.new("ShaderNodeMath")
        power.operation = "POWER"
        power.inputs[1].default_value = 4.0
        nt.links.new(weight.outputs["Facing"], power.inputs[0])
        emit = nt.nodes.new("ShaderNodeEmission")
        emit.inputs["Color"].default_value = (*srgb_to_linear(hex_rgb(look["rim"][0])), 1)
        strength = nt.nodes.new("ShaderNodeMath")
        strength.operation = "MULTIPLY"
        strength.inputs[1].default_value = look["rim"][1]
        nt.links.new(power.outputs[0], strength.inputs[0])
        nt.links.new(strength.outputs[0], emit.inputs["Strength"])
        add = nt.nodes.new("ShaderNodeAddShader")
        nt.links.new(shader, add.inputs[0])
        nt.links.new(emit.outputs[0], add.inputs[1])
        return add.outputs[0]

    def layer_material(kind, stage):
        mat = bpy.data.materials.new(f"M_{direction}_hull_{kind}_s{stage}")
        nt, out = node_tree(mat)
        t = textures[(kind, stage)]
        def tex(img):
            n = nt.nodes.new("ShaderNodeTexImage")
            n.image = img
            n.interpolation = "Linear"
            return n
        albedo, mask, normal, emission = tex(t["albedo"]), tex(t["mask"]), tex(t["normal"]), tex(t["emission"])
        nmap = nt.nodes.new("ShaderNodeNormalMap")
        nt.links.new(normal.outputs["Color"], nmap.inputs["Color"])
        if look["toon"]:
            halftone = [0, 0.15, 0.40, 0.75][stage]
            shader = toon_shader(nt, albedo.outputs["Color"], nmap.outputs["Normal"], emission.outputs["Color"], halftone)
        else:
            bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
            sep = nt.nodes.new("ShaderNodeSeparateColor")
            nt.links.new(mask.outputs["Color"], sep.inputs["Color"])
            rough = nt.nodes.new("ShaderNodeMath")
            rough.operation = "SUBTRACT"
            rough.inputs[0].default_value = 1.0
            nt.links.new(sep.outputs[1], rough.inputs[1])
            nt.links.new(albedo.outputs["Color"], bsdf.inputs["Base Color"])
            nt.links.new(sep.outputs[0], bsdf.inputs["Metallic"])
            nt.links.new(rough.outputs[0], bsdf.inputs["Roughness"])
            nt.links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])
            nt.links.new(emission.outputs["Color"], bsdf.inputs["Emission Color"])
            bsdf.inputs["Emission Strength"].default_value = 8.0
            shader = add_rim(nt, bsdf.outputs[0])
        nt.links.new(shader, out.inputs["Surface"])
        return mat

    def flat_material(name, color, metal=0.0, smooth=0.4, emit=0.0, emit_color=None):
        mat = bpy.data.materials.new(f"M_{direction}_{name}")
        nt, out = node_tree(mat)
        rgb = (*srgb_to_linear(hex_rgb(color)), 1)
        if look["toon"]:
            col = nt.nodes.new("ShaderNodeRGB")
            col.outputs[0].default_value = rgb
            emission_socket = None
            if emit > 0:
                ec = nt.nodes.new("ShaderNodeRGB")
                ec.outputs[0].default_value = tuple(v * emit / 8.0 for v in srgb_to_linear(hex_rgb(emit_color or color))) + (1,)
                emission_socket = ec.outputs[0]
            shader = toon_shader(nt, col.outputs[0], None, emission_socket)
        else:
            bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
            bsdf.inputs["Base Color"].default_value = rgb
            bsdf.inputs["Metallic"].default_value = metal
            bsdf.inputs["Roughness"].default_value = 1 - smooth
            if emit > 0:
                bsdf.inputs["Emission Color"].default_value = (*srgb_to_linear(hex_rgb(emit_color or color)), 1)
                bsdf.inputs["Emission Strength"].default_value = emit
            shader = add_rim(nt, bsdf.outputs[0])
        nt.links.new(shader, out.inputs["Surface"])
        return mat

    # ------------------------------------------------------------------ meshes
    def new_object(name, verts, faces, uvs=None, mats=None, face_mats=None):
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata([tuple(v) for v in verts], [], faces)
        if uvs is not None:
            layer = mesh.uv_layers.new(name="UVMap")
            for poly in mesh.polygons:
                for li, vi in zip(poly.loop_indices, faces[poly.index]):
                    layer.data[li].uv = uvs[poly.index][list(faces[poly.index]).index(vi)]
        for m in mats or []:
            mesh.materials.append(m)
        if face_mats:
            for poly, mi in zip(mesh.polygons, face_mats):
                poly.material_index = mi
        mesh.update()
        obj = bpy.data.objects.new(name, mesh)
        scene.collection.objects.link(obj)
        return obj

    def triangles(obj):
        return sum(len(p.vertices) - 2 for p in obj.data.polygons)

    def hull_patch(name, stage, angled):
        """4 × 4 × 1 cells; left half hull_plain, right half hull_heavy; cell-face UVs / macroTile 2."""
        mats = [layer_material("plain", stage), layer_material("heavy", stage)]
        verts, faces, uvs, fm = [], [], [], []
        def quad(ps, uv, mi):
            base = len(verts)
            verts.extend(ps)
            faces.append((base, base + 1, base + 2, base + 3))
            uvs.append(uv)
            fm.append(mi)
        def tri(ps, uv, mi):
            base = len(verts)
            verts.extend(ps)
            faces.append((base, base + 1, base + 2))
            uvs.append(uv)
            fm.append(mi)
        c = CELL
        for i in range(4):
            for j in range(4):
                mi = 0 if i < 2 else 1
                x0, x1, y0, y1 = i * c, (i + 1) * c, j * c, (j + 1) * c
                u0, u1, v0, v1 = i / 2, (i + 1) / 2, j / 2, (j + 1) / 2
                if not angled:
                    quad([(x0, y0, c), (x1, y0, c), (x1, y1, c), (x0, y1, c)], [(u0, v0), (u1, v0), (u1, v1), (u0, v1)], mi)   # top
                    quad([(x0, y1, 0), (x1, y1, 0), (x1, y0, 0), (x0, y0, 0)], [(u0, v1), (u1, v1), (u1, v0), (u0, v0)], mi)   # bottom
                else:
                    # Wedge: slope rises toward −Y (the back edge is full height); the slope texture is stretched √2.
                    quad([(x0, y1, 0), (x1, y1, 0), (x1, y0, c), (x0, y0, c)], [(u0, v0), (u1, v0), (u1, v0 + 0.5), (u0, v0 + 0.5)], mi)
                    quad([(x0, y1, 0), (x0, y0, 0), (x1, y0, 0), (x1, y1, 0)], [(u0, v1), (u0, v0), (u1, v0), (u1, v1)], mi)
                    if j > 0:   # the riser above the previous wedge's low edge
                        quad([(x0, y0, 0), (x0, y0, c), (x1, y0, c), (x1, y0, 0)], [(u0, 0), (u0, 0.5), (u1, 0.5), (u1, 0)], mi)
                # outer sides
                if j == 0:
                    quad([(x0, y0, 0), (x1, y0, 0), (x1, y0, c), (x0, y0, c)], [(u0, 0), (u1, 0), (u1, 0.5), (u0, 0.5)], mi)
                if j == 3 and not angled:
                    quad([(x1, y1, 0), (x0, y1, 0), (x0, y1, c), (x1, y1, c)], [(u1, 0), (u0, 0), (u0, 0.5), (u1, 0.5)], mi)
                if i == 0:
                    if angled:
                        tri([(x0, y1, 0), (x0, y0, c), (x0, y0, 0)], [(v1, 0), (v0, 0.5), (v0, 0)], mi)
                    else:
                        quad([(x0, y1, 0), (x0, y0, 0), (x0, y0, c), (x0, y1, c)], [(v1, 0), (v0, 0), (v0, 0.5), (v1, 0.5)], mi)
                if i == 3:
                    if angled:
                        tri([(x1, y0, 0), (x1, y0, c), (x1, y1, 0)], [(v0, 0), (v0, 0.5), (v1, 0)], mi)
                    else:
                        quad([(x1, y0, 0), (x1, y1, 0), (x1, y1, c), (x1, y0, c)], [(v0, 0), (v1, 0), (v1, 0.5), (v0, 0.5)], mi)
        obj = new_object(name, verts, faces, uvs, mats, fm)
        obj.location = (-2 * c, -2 * c, 0)
        return obj

    # --- functional blocks: chamfered boxes plus decal geometry
    body_mat = flat_material("body", look["body"]["color"], look["body"]["metal"], look["body"]["smooth"]) if look["body"] else None
    trim_mat = flat_material("trim", look["body_trim"], 0.3, 0.35)

    def box(bm, lo, hi, chamfer):
        """Chamfered box as a bmesh (cells → metres done by the caller's scale)."""
        geom = bmesh.ops.create_cube(bm, size=1.0)
        verts = geom["verts"]
        size = Vector(hi) - Vector(lo)
        centre = (Vector(hi) + Vector(lo)) / 2
        for v in verts:
            v.co = Vector((v.co.x * size.x, v.co.y * size.y, v.co.z * size.z)) + centre
        if chamfer > 0:
            edges = list({e for v in verts for e in v.link_edges})
            bmesh.ops.bevel(bm, geom=verts + edges, offset=chamfer, segments=1, affect="EDGES", profile=0.5)
        return verts

    def mesh_object(name, bm, mat, scale=CELL):
        mesh = bpy.data.meshes.new(name)
        bm.to_mesh(mesh)
        bm.free()
        mesh.materials.append(mat)
        obj = bpy.data.objects.new(name, mesh)
        obj.scale = (scale, scale, scale)
        scene.collection.objects.link(obj)
        return obj

    def join(objs, name):
        for o in objs:                    # join keeps the active object's UV layers, so every part gets one first
            if not o.data.uv_layers:
                o.data.uv_layers.new(name="UVMap")
        bpy.ops.object.select_all(action="DESELECT")
        for o in objs:
            o.select_set(True)
        bpy.context.view_layer.objects.active = objs[0]
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        bpy.ops.object.join()
        objs[0].name = name
        objs[0].data.name = name
        return objs[0]

    def decal(name, polys, mat, normal, offset=0.004):
        """Flat polygons (lists of 3-D points in cells) lifted along the face normal; one material."""
        verts, faces = [], []
        n = Vector(normal) * offset
        for poly in polys:
            base = len(verts)
            verts.extend([Vector(p) + n for p in poly])
            faces.append(tuple(range(base, base + len(poly))))
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata([tuple(v) for v in verts], [], faces)
        mesh.materials.append(mat)
        obj = bpy.data.objects.new(name, mesh)
        obj.scale = (CELL, CELL, CELL)
        scene.collection.objects.link(obj)
        return obj

    def face_frame(normal):
        n = Vector(normal)
        up = Vector((0, 0, 1)) if abs(n.z) < 0.9 else Vector((0, 1, 0))
        u = up.cross(n).normalized()
        v = n.cross(u).normalized()
        return u, v

    def glyph_polys(kind, centre, normal, size, forward=None):
        """§10.6 glyph shapes as polygons on a face: 'chevron' (two nested, pointing toward thrust), 'crosshair', 'diamond'."""
        u, v = face_frame(normal)
        if forward is not None:
            f = Vector(forward)
            f = (f - Vector(normal) * f.dot(Vector(normal)))
            if f.length > 1e-6:
                v = f.normalized()
                u = v.cross(Vector(normal)).normalized() * -1
        c = Vector(centre)
        P = lambda a, b: c + u * (a * size) + v * (b * size)
        polys = []
        if kind == "chevron":
            for off in (-0.28, 0.12):
                t = 0.16
                polys.append([P(-0.42, off - 0.12), P(0.0, off + 0.22), P(0.0, off + 0.22 + t), P(-0.42, off - 0.12 + t)])
                polys.append([P(0.0, off + 0.22), P(0.42, off - 0.12), P(0.42, off - 0.12 + t), P(0.0, off + 0.22 + t)])
        elif kind == "crosshair":
            seg, r0, r1 = 12, 0.24, 0.34
            for k in range(seg):
                a0, a1 = 2 * math.pi * k / seg, 2 * math.pi * (k + 1) / seg
                polys.append([P(r0 * math.cos(a0), r0 * math.sin(a0)), P(r1 * math.cos(a0), r1 * math.sin(a0)),
                              P(r1 * math.cos(a1), r1 * math.sin(a1)), P(r0 * math.cos(a1), r0 * math.sin(a1))])
            w = 0.05
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                if dx:
                    polys.append([P(dx * 0.30, -w), P(dx * 0.48, -w), P(dx * 0.48, w), P(dx * 0.30, w)])
                else:
                    polys.append([P(-w, dy * 0.30), P(w, dy * 0.30), P(w, dy * 0.48), P(-w, dy * 0.48)])
        elif kind == "diamond":
            outer, inner = 0.42, 0.27
            ring = [(0, 1), (1, 0), (0, -1), (-1, 0)]
            for k in range(4):
                a, b = ring[k], ring[(k + 1) % 4]
                polys.append([P(a[0] * inner, a[1] * inner), P(a[0] * outer, a[1] * outer), P(b[0] * outer, b[1] * outer), P(b[0] * inner, b[1] * inner)])
            d = 0.08
            polys.append([P(0, -d), P(d, 0), P(0, d), P(-d, 0)])
        return polys

    def stencil_texture(text, name):
        """DRYDOCK stencil code (§5.5): rendered from a Blender text object into a 256 × 64 RGBA texture."""
        path = os.path.join(tex_dir, f"stencil_{name}.png")
        prev_scene = bpy.context.window.scene if bpy.context.window else scene
        tmp = bpy.data.scenes.new("stencil_" + name)
        tmp.render.engine = "BLENDER_EEVEE"
        tmp.render.resolution_x, tmp.render.resolution_y = 256, 64
        tmp.render.film_transparent = True
        tmp.view_settings.view_transform = "Standard"
        curve = bpy.data.curves.new("txt", "FONT")
        curve.body = text
        curve.font = bpy.data.fonts.load(os.path.join(ROOT, "Assets", "Game", "Art", "_Placeholder", "Fonts", "OfficeCodePro-Bold.ttf"),
                                         check_existing=True)   # OFL 1.1 (repository rule); the direction's own fonts arrive with ART-008
        curve.align_x, curve.align_y = "CENTER", "CENTER"
        curve.size = 1.0
        curve.space_character = 1.1
        txt = bpy.data.objects.new("txt", curve)
        tmp.collection.objects.link(txt)
        m = bpy.data.materials.new("txtmat")
        m.use_nodes = True
        nt = m.node_tree
        for n in list(nt.nodes):
            nt.nodes.remove(n)
        out = nt.nodes.new("ShaderNodeOutputMaterial")
        em = nt.nodes.new("ShaderNodeEmission")
        em.inputs["Color"].default_value = (1, 1, 1, 1)
        nt.links.new(em.outputs[0], out.inputs["Surface"])
        curve.materials.append(m)
        cam = bpy.data.cameras.new("tc")
        cam.type = "ORTHO"
        cam.ortho_scale = 0.66 * len(text) + 0.4
        co = bpy.data.objects.new("tc", cam)
        co.location = (0, 0, 5)
        tmp.collection.objects.link(co)
        tmp.camera = co
        tmp.render.filepath = path
        tmp.render.image_settings.file_format = "PNG"
        tmp.render.image_settings.color_mode = "RGBA"
        tmp.render.image_settings.color_depth = "8"
        bpy.ops.render.render(write_still=True, scene=tmp.name)
        bpy.data.scenes.remove(tmp)
        img = bpy.data.images.load(path, check_existing=False)
        tex_stats.append(dict(name=f"stencil_{name}.png", size=[256, 64]))
        return img

    def stencil_material(img, color, ground):
        """Opaque decal: the band colour with the stencil paint where the text mask is set."""
        mat = bpy.data.materials.new(f"M_{direction}_stencil_{img.name}")
        nt, out = node_tree(mat)
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = img
        mix = nt.nodes.new("ShaderNodeMix")
        mix.data_type = "RGBA"
        mix_socket(mix.inputs, "A").default_value = (*srgb_to_linear(hex_rgb(ground)), 1)
        mix_socket(mix.inputs, "B").default_value = (*srgb_to_linear(hex_rgb(color)), 1)
        nt.links.new(tex.outputs["Alpha"], mix.inputs["Factor"])
        bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
        bsdf.inputs["Roughness"].default_value = 0.6
        bsdf.inputs["Metallic"].default_value = 0.1
        nt.links.new(mix_socket(mix.outputs, "Result"), bsdf.inputs["Base Color"])
        nt.links.new(bsdf.outputs[0], out.inputs["Surface"])
        return mat

    def function_block(name, length, function, glyph, extra):
        """A functional block: body over a 1 × 1 × length footprint (+Y = business direction), function band or full paint,
        glyph decals on every exposed face, emissive strip (≥ 2 cells) where the direction asks for one."""
        fcolor, fint = look["functions"][function]
        pulp = direction == "pulp-horizon"
        fmat = flat_material(f"{function}_paint", fcolor, 0.1, 0.35)
        femit = flat_material(f"{function}_light", fcolor, 0.0, 0.5, emit=fint if fint > 0 else 0.0)
        main = body_mat if not pulp else fmat
        ch = look["chamfer"]
        parts = []
        # Body (pivot at the centre of the root cell: footprint x −0.5..0.5, y −0.5..length−0.5, z −0.5..0.5 in cells).
        lo, hi = (-0.45, -0.5, -0.45), (0.45, length - 0.5 - extra.get("front_inset", 0.0), 0.42)
        bm = bmesh.new()
        box(bm, lo, hi, ch)
        parts.append(mesh_object(name + "_body", bm, main))
        # Function band: 1 cell around the mount face (−Y) for DRYDOCK/NOCTURNE; PULP paints the whole body.
        if not pulp and length > 1:
            bm = bmesh.new()
            box(bm, (-0.462, -0.506, -0.462), (0.462, 0.5, 0.432), ch * 0.6)   # proud of the body: no coplanar faces
            parts.append(mesh_object(name + "_band", bm, fmat if direction == "drydock" else trim_mat))
        if not pulp and length == 1:      # core: function colour on the top panel (DESIGN-SYSTEM §5.3 "beacon dome, top panel")
            panel = [(-0.36, -0.36, 0.42), (0.36, -0.36, 0.42), (0.36, 0.36, 0.42), (-0.36, 0.36, 0.42)]
            parts.append(decal(name + "_panel", [panel], fmat if direction == "drydock" else trim_mat, (0, 0, 1), offset=0.002))
        # Glyph on every exposed face: top, both sides, and the back (mount face hidden when attached; shown anyway).
        gmat = femit if look["glyph_mode"] == "emissive" else flat_material("glyph", look["glyph_color"], 0, 0.3)
        gsize = 0.86 if pulp else (1.12 if look["glyph_mode"] == "emissive" else 0.8)   # NOCTURNE: glyph covers ~60 % of the face
        faces = [((0, (length - 1) / 2 if length > 1 else 0, 0.42), (0, 0, 1)),
                 ((0.45, (length - 1) / 2 + (0.5 if length > 1 else 0), 0), (1, 0, 0)),
                 ((-0.45, (length - 1) / 2 + (0.5 if length > 1 else 0), 0), (-1, 0, 0))]
        if extra.get("glyph_top_only"):
            faces = faces[:1]
        for k, (centre, normal) in enumerate(faces):
            if length > 1 and normal[2] == 0:
                centre = (centre[0], length - 1.0, centre[2])      # side glyph on the front cell, clear of the band
            polys = glyph_polys(glyph, centre, normal, gsize, forward=(0, 1, 0))
            parts.append(decal(f"{name}_glyph{k}", polys, gmat, normal))
        # Emissive strip ≥ 2 cells along the length (DRYDOCK indicator, NOCTURNE strips).
        if look["strip"] and length >= 2:
            w = 0.07
            for sx in (-1, 1):
                strip = [(sx * 0.452, -0.4, 0.30), (sx * 0.452, -0.4, 0.30 + w), (sx * 0.452, length - 0.6, 0.30 + w), (sx * 0.452, length - 0.6, 0.30)]
                parts.append(decal(f"{name}_strip{sx}", [strip], femit, (sx, 0, 0)))
        # DRYDOCK stencil code on both sides of the band cell.
        if look["stencil"] and extra.get("code"):
            img = stencil_texture(extra["code"], name)
            smat = stencil_material(img, "#E9E2D0", fcolor if length > 1 else look["body"]["color"])
            for sx in (-1, 1):
                y0, y1 = (-0.42, 0.42) if length > 1 else (-0.35, 0.35)
                q = [(sx * 0.47, y0 if sx > 0 else y1, -0.30), (sx * 0.47, y1 if sx > 0 else y0, -0.30),
                     (sx * 0.47, y1 if sx > 0 else y0, -0.09), (sx * 0.47, y0 if sx > 0 else y1, -0.09)]
                obj = decal(f"{name}_stencil{sx}", [q], smat, (sx, 0, 0))
                layer = obj.data.uv_layers.new(name="UVMap")
                for li, uv in zip(obj.data.polygons[0].loop_indices, [(0, 0), (1, 0), (1, 1), (0, 1)]):
                    layer.data[li].uv = uv
                parts.append(obj)
        return parts, fmat, femit

    def cylinder(bm, centre, radius, depth, axis, segments, cap=True):
        res = bmesh.ops.create_cone(bm, cap_ends=cap, cap_tris=False, segments=segments, radius1=radius, radius2=radius, depth=depth)
        rot = Matrix.Rotation(math.radians(90), 4, "X") if axis == "Y" else Matrix.Identity(4)
        bmesh.ops.transform(bm, matrix=Matrix.Translation(Vector(centre)) @ rot, verts=res["verts"])
        return res

    def cone(bm, centre, r1, r2, depth, axis, segments, cap=False):
        res = bmesh.ops.create_cone(bm, cap_ends=cap, cap_tris=False, segments=segments, radius1=r1, radius2=r2, depth=depth)
        rot = Matrix.Rotation(math.radians(-90), 4, "X") if axis == "Y" else Matrix.Identity(4)
        bmesh.ops.transform(bm, matrix=Matrix.Translation(Vector(centre)) @ rot, verts=res["verts"])
        return res

    blocks = {}
    stats_meshes = []

    # block_hull / block_hull_angled
    blocks["block_hull"] = [hull_patch("block_hull", 0, angled=False)]
    blocks["block_hull_angled"] = [hull_patch("block_hull_angled", 0, angled=True)]

    # block_thruster_small: 1 × 1 × 2, exhaust toward +Y; nozzle bell may extend 1 cell into the clearance.
    parts, fmat, femit = function_block("block_thruster_small", 2, "thruster", "chevron", dict(code="THR-S", front_inset=0.25))
    bm = bmesh.new()
    cone(bm, (0, 1.45, 0), 0.30, 0.40, 0.45, "Y", 12)
    parts.append(mesh_object("thruster_bell", bm, trim_mat if direction != "pulp-horizon" else fmat))
    bm = bmesh.new()
    cylinder(bm, (0, 1.30, 0), 0.27, 0.06, "Y", 12)
    parts.append(mesh_object("thruster_glow", bm, femit))
    blocks["block_thruster_small"] = [join(parts, "block_thruster_small")]

    # weapon_plasma_turret_small: base (1 × 1 × 2, low) + yaw part (head and barrel toward +Y, muzzle at y = 1.5 cells).
    parts, fmat, femit = function_block("weapon_plasma_turret_small", 2, "weapon", "crosshair", dict(code="WPN-PL"))
    for p in parts:
        if p.name.endswith("_body"):
            p.scale.z *= 0.55
            p.location.z -= 0.45 * CELL * 0.45
    base = join(parts, "weapon_plasma_turret_small")
    bm = bmesh.new()
    cylinder(bm, (0, 0.5, 0.36), 0.36, 0.30, "Z", 12)
    head = mesh_object("turret_head", bm, body_mat if body_mat else fmat)
    bm = bmesh.new()
    cylinder(bm, (0, 1.05, 0.38), 0.09, 0.85, "Y", 8)
    barrel = mesh_object("turret_barrel", bm, trim_mat)
    bm = bmesh.new()
    cylinder(bm, (0, 1.46, 0.38), 0.12, 0.06, "Y", 8)
    muzzle = mesh_object("turret_muzzle", bm, femit)
    head_glyph = decal("turret_head_glyph", glyph_polys("crosshair", (0, 0.5, 0.51), (0, 0, 1), 0.62, forward=(0, 1, 0)),
                       femit if look["glyph_mode"] == "emissive" else flat_material("glyph2", look["glyph_color"], 0, 0.3), (0, 0, 1))
    yaw = join([head, barrel, muzzle, head_glyph], "weapon_plasma_turret_small_Yaw")
    blocks["weapon_plasma_turret_small"] = [base, yaw]

    # block_core_small: 1 cell, ≤ 150 triangles: chamfered body, beacon dome (emissive), diamond glyph on every face.
    parts, fmat, femit = function_block("block_core_small", 1, "core", "diamond", dict(code="CORE"))
    for p in parts:
        if "_glyph0" in p.name:
            p.location.z += 0.0
    bm = bmesh.new()
    res = bmesh.ops.create_uvsphere(bm, u_segments=8, v_segments=3, radius=0.2)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.z < -1e-4], context="VERTS")
    bmesh.ops.translate(bm, vec=Vector((0, 0, 0.42)), verts=bm.verts)
    parts.append(mesh_object("core_beacon", bm, femit))
    core = join(parts, "block_core_small")
    blocks["block_core_small"] = [core]

    # Damage strip: four copies of the hull patch, stages 0–3.
    strip = []
    for stage in range(4):
        obj = hull_patch(f"damage_s{stage}", stage, angled=False)
        obj.location = (-2 * CELL + (stage - 1.5) * 5 * CELL, -2 * CELL, 0)
        strip.append(obj)

    for name, objs in blocks.items():
        tris = sum(triangles(o) for o in objs)
        stats_meshes.append(dict(block=name, triangles=tris, parts={o.name: triangles(o) for o in objs},
                                 footprint_cells=FOOTPRINT[name],
                                 budget=(2 * 6 * FOOTPRINT[name] if name.startswith("block_hull") else budget(FOOTPRINT[name])),
                                 budget_rule="bricks: ≤ 2 triangles per exposed face" if name.startswith("block_hull") else "DESIGN-SYSTEM §10.11 LOD0"))

    # ------------------------------------------------------------------ world, lights, view
    world = bpy.data.worlds.new("World")
    world.use_nodes = True
    wt = world.node_tree
    for n in list(wt.nodes):
        wt.nodes.remove(n)
    wout = wt.nodes.new("ShaderNodeOutputWorld")
    backdrop = wt.nodes.new("ShaderNodeBackground")
    backdrop.inputs["Color"].default_value = (*srgb_to_linear(hex_rgb(look["backdrop"])), 1)
    ambient = wt.nodes.new("ShaderNodeBackground")
    if isinstance(look["ambient"], tuple):          # PULP HORIZON gradient: sky = band, equator = horizon, ground = top
        coord = wt.nodes.new("ShaderNodeTexCoord")
        sep = wt.nodes.new("ShaderNodeSeparateXYZ")
        wt.links.new(coord.outputs["Generated"], sep.inputs[0])
        ramp = wt.nodes.new("ShaderNodeValToRGB")
        ramp.color_ramp.elements[0].position = 0.0
        ramp.color_ramp.elements[0].color = (*srgb_to_linear(hex_rgb(look["ambient"][2])), 1)
        mid = ramp.color_ramp.elements.new(0.5)
        mid.color = (*srgb_to_linear(hex_rgb(look["ambient"][1])), 1)
        ramp.color_ramp.elements[-1].position = 1.0
        ramp.color_ramp.elements[-1].color = (*srgb_to_linear(hex_rgb(look["ambient"][0])), 1)
        remap = wt.nodes.new("ShaderNodeMapRange")
        remap.inputs["From Min"].default_value = -1
        remap.inputs["From Max"].default_value = 1
        wt.links.new(sep.outputs["Z"], remap.inputs["Value"])
        wt.links.new(remap.outputs[0], ramp.inputs["Fac"])
        wt.links.new(ramp.outputs["Color"], ambient.inputs["Color"])
    else:
        ambient.inputs["Color"].default_value = (*srgb_to_linear(hex_rgb(look["ambient"])), 1)
    if direction == "nocturne":                     # reflections: faint teal/violet filaments in the environment
        noise = wt.nodes.new("ShaderNodeTexNoise")
        noise.inputs["Scale"].default_value = 3.0
        tint = wt.nodes.new("ShaderNodeMix")
        tint.data_type = "RGBA"
        mix_socket(tint.inputs, "A").default_value = (*srgb_to_linear(hex_rgb("#0A0E18")), 1)
        mix_socket(tint.inputs, "B").default_value = (*srgb_to_linear(hex_rgb("#1F6E86")), 1)
        wt.links.new(noise.outputs["Fac"], tint.inputs["Factor"])
        wt.links.new(mix_socket(tint.outputs, "Result"), ambient.inputs["Color"])
    path = wt.nodes.new("ShaderNodeLightPath")
    mix = wt.nodes.new("ShaderNodeMixShader")
    wt.links.new(path.outputs["Is Camera Ray"], mix.inputs["Fac"])
    wt.links.new(ambient.outputs[0], mix.inputs[1])
    wt.links.new(backdrop.outputs[0], mix.inputs[2])
    wt.links.new(mix.outputs[0], wout.inputs["Surface"])
    scene.world = world

    yaw_deg = 30.0
    def sun(name, color, intensity, pitch, azimuth, shadows):
        data = bpy.data.lights.new(name, "SUN")
        data.color = srgb_to_linear(hex_rgb(color))
        data.energy = intensity * math.pi
        data.use_shadow = shadows
        data.angle = math.radians(1.0)
        obj = bpy.data.objects.new(name, data)
        obj.rotation_euler = (math.radians(90 - pitch), 0, math.radians(azimuth))
        scene.collection.objects.link(obj)
        return obj
    key_color, key_int, key_pitch = look["key"]
    sun("Key", key_color, key_int, key_pitch, yaw_deg + 25, True)      # lights both camera-facing sides, unequally
    fcol, fint_, fpitch, fmode = look["fill"]
    sun("Fill" if fmode != "rim" else "Rim", fcol, fint_, fpitch, yaw_deg + 180 if fmode == "rim" else yaw_deg + 205, False)

    view = scene.view_settings
    for candidate in ([look["view"]] + (["ACES 2.0", "ACES 1.3", "Filmic"] if look["view"] == "ACES" else [])):
        try:
            view.view_transform = candidate
            break
        except TypeError:
            continue
    used_view = view.view_transform
    view.look = "None"
    view.exposure = 0.0
    scene.render.film_transparent = False
    if look["outline"]:
        scene.render.use_freestyle = True
        scene.render.line_thickness_mode = "ABSOLUTE"
        scene.render.line_thickness = look["outline"][1]
        lineset = scene.view_layers[0].freestyle_settings.linesets[0] if scene.view_layers[0].freestyle_settings.linesets else scene.view_layers[0].freestyle_settings.linesets.new("Outlines")
        if lineset.linestyle is None:
            lineset.linestyle = bpy.data.linestyles.new("Ink")
        lineset.linestyle.color = srgb_to_linear(hex_rgb(look["outline"][0]))
        lineset.linestyle.thickness = look["outline"][1]
        lineset.select_by_visibility = True
        lineset.select_silhouette = True
        lineset.select_border = False          # decals are open meshes; the game's outline is depth/normal edge detection
        lineset.select_crease = True
        scene.view_layers[0].freestyle_settings.crease_angle = math.radians(120)

    # Compositor: bloom (§x.4), saturation and contrast.
    scene.use_nodes = True if hasattr(scene, "use_nodes") else None
    comp_ok = True
    try:
        tree = scene.compositing_node_group if hasattr(scene, "compositing_node_group") else scene.node_tree
        if tree is None:
            tree = bpy.data.node_groups.new("Compositor", "CompositorNodeTree")
            scene.compositing_node_group = tree
        for n in list(tree.nodes):
            tree.nodes.remove(n)
        rl = tree.nodes.new("CompositorNodeRLayers")
        glare = tree.nodes.new("CompositorNodeGlare")
        thr, strength = look["bloom"]
        glare.inputs["Type"].default_value = "Bloom"           # Blender 5: the glare type is a menu input
        glare.inputs["Quality"].default_value = "High"
        glare.inputs["Threshold"].default_value = thr
        glare.inputs["Strength"].default_value = strength
        glare.inputs["Size"].default_value = 0.6
        hsv = tree.nodes.new("CompositorNodeHueSat")
        hsv.inputs["Saturation"].default_value = look["saturation"]
        last = hsv.outputs["Image"]
        tree.links.new(rl.outputs["Image"], glare.inputs["Image"])
        tree.links.new(glare.outputs["Image"], hsv.inputs["Image"])
        if look["contrast"]:
            # URP contrast pivots on mid-grey in log space: x' = 0.18 · (x / 0.18)^(1 + contrast). (Bright/Contrast pivots on 0.5
            # in linear light and would crush NOCTURNE's dark hulls to black.)
            steps = [("MULTIPLY", (1 / 0.18,) * 3), ("POWER", (1 + look["contrast"],) * 3), ("MULTIPLY", (0.18,) * 3)]
            for op, value in steps:
                node = tree.nodes.new("ShaderNodeVectorMath")
                node.operation = op
                node.inputs[1].default_value = value
                tree.links.new(last, node.inputs[0])
                last = node.outputs[0]
        out_node = tree.nodes.new("NodeGroupOutput") if not hasattr(bpy.types, "CompositorNodeComposite") else tree.nodes.new("CompositorNodeComposite")
        if out_node.bl_idname == "NodeGroupOutput":
            tree.interface.new_socket("Image", in_out="OUTPUT", socket_type="NodeSocketColor")
        tree.links.new(last, out_node.inputs[0])
    except Exception as e:  # keep rendering without post if the compositor API differs
        comp_ok = f"compositor skipped: {e}"

    eevee = scene.eevee
    for attr, val in (("taa_render_samples", 64), ("use_shadows", True), ("shadow_ray_count", 2), ("shadow_step_count", 8)):
        if hasattr(eevee, attr):
            setattr(eevee, attr, val)
    scene.render.filter_size = 1.5

    # ------------------------------------------------------------------ camera and renders
    cam_data = bpy.data.cameras.new("Camera")
    cam_data.type = "ORTHO"
    cam = bpy.data.objects.new("Camera", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    cam.rotation_euler = (math.radians(90 - 55), 0, math.radians(yaw_deg))

    def all_objects():
        return [o for o in scene.objects if o.type == "MESH"]

    def show_only(objs):
        keep = set(o.name for o in objs)
        for o in all_objects():
            o.hide_render = o.name not in keep

    def frame(objs, ppc, margin_cells=0.6):
        """Fit an orthographic view around objs at exactly ppc pixels per cell; returns (width_px, height_px, ppc_actual)."""
        forward = cam.matrix_world.to_3x3() @ Vector((0, 0, -1))
        corners = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
        target = sum(corners, Vector()) / len(corners)
        cam.location = target - forward * 20.0
        bpy.context.view_layer.update()
        inv = cam.matrix_world.inverted()
        pts = [inv @ c for c in corners]
        xs, ys = [p.x for p in pts], [p.y for p in pts]
        cx_, cy_ = (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2
        width = (max(xs) - min(xs)) / CELL + 2 * margin_cells
        height = (max(ys) - min(ys)) / CELL + 2 * margin_cells
        wpx, hpx = max(8, round(width * ppc)), max(8, round(height * ppc))
        scale_m = max(wpx, hpx) / ppc * CELL
        cam_data.ortho_scale = scale_m
        cam.location = cam.location + cam.matrix_world.to_3x3() @ Vector((cx_, cy_, 0))
        cam_data.clip_start, cam_data.clip_end = 0.1, 60.0
        scene.render.resolution_x, scene.render.resolution_y = wpx, hpx
        scene.render.resolution_percentage = 100
        return wpx, hpx, max(wpx, hpx) / (scale_m / CELL)

    renders = []
    def render(path, objs, ppc, label):
        show_only(objs)
        wpx, hpx, actual = frame(objs, ppc)
        for mul in halftone_nodes:
            mul.inputs[1].default_value = (wpx / 6.0, hpx / 6.0, 0)
        scene.render.filepath = path
        scene.render.image_settings.file_format = "PNG"
        scene.render.image_settings.color_mode = "RGB"
        scene.render.image_settings.color_depth = "8"
        scene.render.image_settings.compression = 100       # lossless; keeps the board under its 25 MB budget
        bpy.ops.render.render(write_still=True)
        renders.append(dict(file=os.path.relpath(path, out_dir), size=[wpx, hpx], ortho_scale_m=round(cam_data.ortho_scale, 6),
                            ppc_target=ppc, ppc=round(actual, 3), view=label))

    for name, objs in blocks.items():
        for dist, ppc in DISTANCES.items():
            render(os.path.join(out_dir, f"{name}_{dist}.png"), objs, ppc, dist)
    render(os.path.join(out_dir, "damage_strip.png"), strip, DISTANCES["050"], "050")
    render(os.path.join(out_dir, "damage_strip_005.png"), strip, 60.0, "close")

    for o in all_objects():
        o.hide_render = False
    os.makedirs(src_dir, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(src_dir, "style_test.blend"), compress=True, relative_remap=True)

    stats = dict(direction=direction, generator="ArtSource/StyleTest/build_style_test.py", blender=bpy.app.version_string,
                 view_transform=used_view, compositor=comp_ok if comp_ok is not True else "bloom + saturation + contrast",
                 cell_metres=CELL, camera=dict(type="orthographic", pitch_deg=55, yaw_deg=yaw_deg),
                 meshes=stats_meshes, textures=tex_stats, renders=renders)
    with open(os.path.join(out_dir, "stats.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump(stats, f, indent=2)
        f.write("\n")
    print("STYLE_TEST_DONE", direction)


# ======================================================================================================================
# Driver: run Blender, then compose (Pillow)
# ======================================================================================================================

def compose(direction):
    from PIL import Image, ImageDraw, ImageFont
    out_dir = os.path.join(OUT_ROOT, direction)
    stats = json.load(open(os.path.join(out_dir, "stats.json"), encoding="utf-8"))
    look = LOOKS[direction]
    font_dir = os.path.join(ROOT, "Assets", "Game", "Art", "_Placeholder", "Fonts")
    bold = ImageFont.truetype(os.path.join(font_dir, "OfficeCodePro-Bold.ttf"), 34)
    label = ImageFont.truetype(os.path.join(font_dir, "OfficeCodePro-Regular.ttf"), 16)
    small = ImageFont.truetype(os.path.join(font_dir, "OfficeCodePro-Regular.ttf"), 13)
    W, H = 1920, 1080
    bg = tuple(int(c * 255) for c in hex_rgb(look["backdrop"]))
    canvas = Image.new("RGB", (W, H), tuple(min(255, c + 10) for c in bg))
    d = ImageDraw.Draw(canvas)
    fg, dim = (232, 230, 224), (150, 150, 146)
    d.text((40, 26), f"{look['name']} — block kit style test (ART-004)", font=bold, fill=fg)
    d.text((40, 70), "Close-up at 148 px/cell (5 m), then true pixel size at 50 / 150 / 300 m (14.8 / 4.9 / 2.5 px per cell), "
                     "enlarged with nearest-neighbour pixels.", font=label, fill=dim)
    names = {"block_hull": "hull", "block_hull_angled": "hull angled", "block_thruster_small": "thruster small",
             "weapon_plasma_turret_small": "plasma turret small", "block_core_small": "core small"}
    tris = {m["block"]: m for m in stats["meshes"]}
    col_w = (W - 80) // 5
    rows = [("050", 2, 488), ("150", 4, 690), ("300", 6, 690)]
    for i, block in enumerate(BLOCKS):
        x0 = 40 + i * col_w
        close = Image.open(os.path.join(out_dir, f"{block}_005.png")).convert("RGB")
        close.thumbnail((col_w - 20, 300), Image.LANCZOS)
        canvas.paste(close, (x0 + (col_w - 20 - close.width) // 2, 104 + (300 - close.height) // 2))
        m = tris[block]
        d.text((x0, 412), names[block], font=label, fill=fg)
        d.text((x0, 434), f"{m['triangles']} tris / budget {m['budget']}", font=small, fill=dim)
        x150 = x0
        for dist, mul, y in rows:
            im = Image.open(os.path.join(out_dir, f"{block}_{dist}.png")).convert("RGB")
            im = im.resize((im.width * mul, im.height * mul), Image.NEAREST)
            im.thumbnail((col_w - 20 if dist == "050" else (col_w - 30) // 2, 180 if dist == "050" else 150), Image.NEAREST)
            x = x0 if dist != "300" else x150
            canvas.paste(im, (x, y))
            d.text((x, y + im.height + 2), f"{dist} m (×{mul})", font=small, fill=dim)
            if dist == "150":
                x150 = x0 + im.width + 16
    strip = Image.open(os.path.join(out_dir, "damage_strip_005.png")).convert("RGB")
    strip.thumbnail((1180, 190), Image.LANCZOS)
    canvas.paste(strip, (40, H - strip.height - 40))
    d.text((40, H - 34), "Damage stages 0 → 3 on the hull patch (hull_plain left half, hull_heavy right half)", font=label, fill=dim)
    strip50 = Image.open(os.path.join(out_dir, "damage_strip.png")).convert("RGB")
    strip50 = strip50.resize((strip50.width * 2, strip50.height * 2), Image.NEAREST)
    strip50.thumbnail((W - 1280, 190), Image.NEAREST)
    canvas.paste(strip50, (1260, H - strip50.height - 40))
    d.text((1260, H - 34), "Damage at 50 m (×2)", font=label, fill=dim)
    canvas.save(os.path.join(out_dir, "composite.png"), optimize=True)
    print(f"composed {os.path.relpath(os.path.join(out_dir, 'composite.png'), ROOT)}")


def driver(argv):
    import argparse
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--direction", action="append", choices=DIRECTIONS)
    ap.add_argument("--blender", default=os.environ.get("BLENDER", "/Applications/Blender.app/Contents/MacOS/Blender"))
    ap.add_argument("--compose-only", action="store_true")
    args = ap.parse_args(argv)
    for direction in args.direction or DIRECTIONS:
        if not args.compose_only:
            log = os.path.join(ROOT, "Logs", "tools", f"style_test_{direction}.log")
            os.makedirs(os.path.dirname(log), exist_ok=True)
            with open(log, "w") as f:
                rc = subprocess.call([args.blender, "-b", "--factory-startup", "--python", os.path.abspath(__file__), "--", "--render", direction],
                                     stdout=f, stderr=subprocess.STDOUT)
            text = open(log, encoding="utf-8", errors="replace").read()
            if rc != 0 or f"STYLE_TEST_DONE {direction}" not in text:
                print(f"Blender failed for {direction} (see {os.path.relpath(log, ROOT)}):")
                print("\n".join(l for l in text.splitlines() if "Error" in l or "Traceback" in l or l.startswith("  "))[-3000:])
                return 1
        compose(direction)
    return 0


if __name__ == "__main__":
    if "--render" in sys.argv:
        blender_main(sys.argv[sys.argv.index("--render") + 1])
    else:
        sys.exit(driver(sys.argv[1:]))
