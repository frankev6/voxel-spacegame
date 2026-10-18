#!/usr/bin/env python3
"""DEV-023: the arrival view of each hub option, rendered in Blender from the layouts in hub_options.json.

Block-out only: every building is its footprint extruded from its depth below the flight plane to its height above it,
with lit windows; ships are boxes with engine glow; lanes are rows of lights. It shows scale, density, traffic and the
skyline you see on arrival, at the game camera's 55° pitch and 40° field of view, pulled back for the arrival.

Run:  python3 ArtSource/Hub/hub_arrival.py [A,B,C] [--draft]
Out:  docs/design/hub/img/hub_arrival_<x>.jpg, hub_entry_<x>.jpg
"""
import json
import math
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
BLENDER = "/Applications/Blender.app/Contents/MacOS/Blender"
JSON = os.path.join(ROOT, "docs", "design", "hub", "hub_options.json")
OUT = os.path.join(ROOT, "docs", "design", "hub", "img")
FONT = os.path.join(ROOT, "Assets", "Game", "UI", "Theme", "Fonts", "Michroma", "FNT_Michroma-Regular.ttf")

LIVERY = {"tlax": ("#1F7A4D", "#5BE3A1"), "vest": ("#E4E9EE", "#9FD8FF"), "khu": ("#A8471E", "#FF7A2F"),
          "scav": ("#5B5E5A", "#FFB347"), "ark": ("#CFC8B8", "#F2B705"), "noct": ("#2C3139", "#FF463C"),
          "civ": ("#A6A6A0", "#FFE2B0")}
CLASSES = {"shuttle": (14, 6), "courier": (22, 8), "wraith": (28, 12), "tug": (32, 14), "cruiser": (52, 24),
           "hauler": (64, 20), "freighter": (140, 34), "barge": (190, 52), "liner": (240, 46)}
DISTRICT = {"harbour": "#FFE2B0", "exchange": "#5BE3A1", "yards": "#F2B705", "range": "#FF5530", "pit": "#FF7A2F",
            "institute": "#9FD8FF", "breakers": "#FFB347", "foundry": "#FF6A2A", "circuit": "#7CFF9A", "old_town": "#BEBAAC",
            "gate": "#FFF1D6", "relic": "#FFC86B"}
LANE = {"approach_in": "#7CFF9A", "approach_out": "#FFB347", "harbour": "#FFE2B0", "boulevard": "#9FE0C8",
        "avenue_in": "#7CFF9A", "avenue_out": "#FFB347", "district": "#9FE0C8", "launch": "#F2B705", "gate_in": "#FFC86B",
        "gate_out": "#FFC86B", "street": "#FFE2B0"}


def hexrgb(h, a=1.0):
    h = h.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple((x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4) for x in c) + (a,)


def polar(b, r, c=(0.0, 0.0)):
    a = math.radians(b)
    return (c[0] + r * math.sin(a), c[1] + r * math.cos(a))


# ---------------------------------------------------------------------------------------------------------- Blender
def blender_main(key, draft):
    import bpy
    import bmesh
    sys.path.insert(0, os.path.join(ROOT, "ArtSource", "LookDev"))
    import ld_scenes
    import ld_world
    from ld_styles import STYLES

    opt = json.load(open(JSON))[key]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    S = dict(STYLES["nocturne"]); S["_key"] = "nocturne"
    S["bloom"] = (0.9, 0.55)
    ld_scenes.setup_render(S, scene, draft)
    ld_world.setup_world(S, scene)
    scene.eevee.taa_render_samples = 16 if draft else 64

    def mat_emit(name, hexcol, strength):
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        nt = m.node_tree; nt.nodes.clear()
        em = nt.nodes.new("ShaderNodeEmission"); em.inputs["Color"].default_value = hexrgb(hexcol)
        em.inputs["Strength"].default_value = strength
        out = nt.nodes.new("ShaderNodeOutputMaterial"); nt.links.new(em.outputs[0], out.inputs[0])
        return m

    def mat_flat(name, hexcol, rough=0.5, metal=0.3):
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        b = m.node_tree.nodes["Principled BSDF"]
        b.inputs["Base Color"].default_value = hexrgb(hexcol); b.inputs["Roughness"].default_value = rough
        b.inputs["Metallic"].default_value = metal
        return m

    # ---- the building material: plates by face colour, lit windows on the walls, darker with depth below the plane
    def building_material():
        m = bpy.data.materials.new("city")
        m.use_nodes = True
        nt = m.node_tree; N, L = nt.nodes, nt.links
        bsdf = N["Principled BSDF"]
        geo = N.new("ShaderNodeNewGeometry")
        col = N.new("ShaderNodeAttribute"); col.attribute_name = "tone"; col.attribute_type = "GEOMETRY"
        lit = N.new("ShaderNodeAttribute"); lit.attribute_name = "lit"; lit.attribute_type = "GEOMETRY"
        acc = N.new("ShaderNodeAttribute"); acc.attribute_name = "accent"; acc.attribute_type = "GEOMETRY"
        sep = N.new("ShaderNodeSeparateXYZ"); L.new(geo.outputs["Position"], sep.inputs[0])
        sepn = N.new("ShaderNodeSeparateXYZ"); L.new(geo.outputs["Normal"], sepn.inputs[0])

        def math_(op, a, b=None, clamp=False):
            n = N.new("ShaderNodeMath"); n.operation = op; n.use_clamp = clamp
            for i, v in enumerate((a, b)):
                if v is None:
                    continue
                if isinstance(v, (int, float)):
                    n.inputs[i].default_value = v
                else:
                    L.new(v, n.inputs[i])
            return n.outputs[0]
        u = math_("ADD", sep.outputs["X"], sep.outputs["Y"])
        z = sep.outputs["Z"]
        cu = math_("FLOOR", math_("DIVIDE", u, 4.5)); cv = math_("FLOOR", math_("DIVIDE", z, 3.6))
        fu = math_("FRACT", math_("DIVIDE", u, 4.5)); fv = math_("FRACT", math_("DIVIDE", z, 3.6))
        comb = N.new("ShaderNodeCombineXYZ"); L.new(cu, comb.inputs[0]); L.new(cv, comb.inputs[1])
        L.new(math_("MULTIPLY", lit.outputs["Fac"], 97.0), comb.inputs[2])
        wn = N.new("ShaderNodeTexWhiteNoise"); wn.noise_dimensions = "3D"; L.new(comb.outputs[0], wn.inputs["Vector"])
        on = math_("LESS_THAN", wn.outputs["Value"], math_("MULTIPLY", lit.outputs["Fac"], 0.5))
        shape = math_("MULTIPLY", math_("MULTIPLY", math_("GREATER_THAN", fu, 0.18), math_("LESS_THAN", fu, 0.82)),
                      math_("MULTIPLY", math_("GREATER_THAN", fv, 0.25), math_("LESS_THAN", fv, 0.7)))
        side = math_("LESS_THAN", math_("ABSOLUTE", sepn.outputs["Z"]), 0.5)
        win = math_("MULTIPLY", math_("MULTIPLY", on, shape), side)
        # warm and a few cool windows
        hue = N.new("ShaderNodeMix"); hue.data_type = "RGBA"
        L.new(math_("GREATER_THAN", wn.outputs["Value"], 0.42), hue.inputs["Factor"])
        hue.inputs["A"].default_value = hexrgb("#FFE2B0"); hue.inputs["B"].default_value = hexrgb("#BFD6FF")
        bsdf.inputs["Emission Strength"].default_value = 1.0
        emc = N.new("ShaderNodeMix"); emc.data_type = "RGBA"; emc.inputs["A"].default_value = (0, 0, 0, 1)
        L.new(math_("MULTIPLY", win, 6.0), emc.inputs["Factor"])
        L.new(hue.outputs["Result"], emc.inputs["B"])
        # district accent bands: a lit line every 40 m of height
        band = math_("MULTIPLY", math_("LESS_THAN", math_("FRACT", math_("DIVIDE", z, 40.0)), 0.04), side)
        emc2 = N.new("ShaderNodeMix"); emc2.data_type = "RGBA"
        L.new(math_("MULTIPLY", band, 0.6), emc2.inputs["Factor"])
        L.new(emc.outputs["Result"], emc2.inputs["A"]); L.new(acc.outputs["Color"], emc2.inputs["B"])
        L.new(emc2.outputs["Result"], bsdf.inputs["Emission Color"])
        # depth: the lower city fades into dark haze
        dk = math_("EXPONENT", math_("DIVIDE", math_("MINIMUM", z, 0.0), 700.0))
        base = N.new("ShaderNodeMix"); base.data_type = "RGBA"; base.inputs["A"].default_value = hexrgb("#0B0D10")
        L.new(dk, base.inputs["Factor"]); L.new(col.outputs["Color"], base.inputs["B"])
        L.new(base.outputs["Result"], bsdf.inputs["Base Color"])
        bsdf.inputs["Roughness"].default_value = 0.55; bsdf.inputs["Metallic"].default_value = 0.35
        return m

    city_mat = building_material()
    me = bpy.data.meshes.new("city")
    bm = bmesh.new()
    tone_l = bm.faces.layers.float_color.new("tone")
    lit_l = bm.faces.layers.float.new("lit")
    acc_l = bm.faces.layers.float_color.new("accent")
    TONE = {"spire": "#CFC8B8", "spire_top": "#E0DACB", "gatehouse": "#CFC8B8", "relic_foot": "#ECE7DC", "sanctum": "#E4E9EE",
            "warehouse": "#6C7075", "industrial": "#5E625E", "hangar": "#545A60", "cargo": "#7A6A4A", "market": "#A6A6A0",
            "wall": "#4A4E52", "bastion": "#5A5E62", "keel": "#8A8478", "rib": "#6C7075", "rib_tip": "#A6A6A0"}
    import random
    rng = random.Random(7)
    for b in opt["buildings"]:
        pts = b["pts"]
        if len(pts) < 3:
            continue
        lo, hi = b["d"], b["h"]
        vb = [bm.verts.new((x, y, lo)) for x, y in pts]
        vt = [bm.verts.new((x, y, hi)) for x, y in pts]
        faces = []
        try:
            faces.append(bm.faces.new(vt))
            faces.append(bm.faces.new(vb[::-1]))
        except ValueError:
            continue
        n = len(pts)
        for i in range(n):
            j = (i + 1) % n
            faces.append(bm.faces.new((vb[i], vb[j], vt[j], vt[i])))
        t = TONE.get(b["kind"], rng.choice(["#A6A6A0", "#9A9A92", "#B8B4A8", "#8A8E86", "#CFC8B8"]))
        tc = hexrgb(t)
        k = 0.8 + rng.random() * 0.3
        ac = hexrgb(DISTRICT.get(b.get("district") or "", "#FFE2B0"))
        for fi, f in enumerate(faces):
            kk = k * (0.5 if fi < 2 else 1.0)          # roofs darker than the lit walls
            f[tone_l] = (tc[0] * kk, tc[1] * kk, tc[2] * kk, 1)
            f[lit_l] = b["lit"]
            f[acc_l] = ac
    # annulus walls and stands are concave: ear-clip them, or their caps sheet across the city
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4], quad_method="BEAUTY", ngon_method="EAR_CLIP")
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me); bm.free()
    city = bpy.data.objects.new("city", me); scene.collection.objects.link(city)
    me.materials.append(city_mat)

    # ---- ships: boxes in livery, engine glow and plume when under way, nav light at the bow
    ship_mats = {k: mat_flat("hull_" + k, v[0], 0.45, 0.4) for k, v in LIVERY.items()}
    glow_mats = {k: mat_emit("glow_" + k, v[1], 25.0) for k, v in LIVERY.items()}
    plume = mat_emit("plume", "#8FB4FF", 1.6)

    def add_box(name, c, heading, L_, W_, H_, z, mat):
        bpy.ops.mesh.primitive_cube_add(size=1, location=(c[0], c[1], z))
        o = bpy.context.active_object; o.name = name
        o.scale = (W_, L_, H_); o.rotation_euler = (0, 0, -math.radians(heading))
        o.data.materials.append(mat)
        return o
    for i, s in enumerate(opt["ships"]):
        L_, B_ = CLASSES[s["cls"]]
        c, hd = s["c"], s["heading"]
        add_box(f"ship{i}", c, hd, L_, B_, B_ * 0.45, 0, ship_mats[s["livery"]])
        add_box(f"ship{i}_bridge", polar(hd, -L_ * 0.25, c), hd, L_ * 0.25, B_ * 0.5, B_ * 0.5, B_ * 0.35, ship_mats[s["livery"]])
        add_box(f"ship{i}_nav", polar(hd, L_ * 0.5, c), hd, 3, 3, 3, B_ * 0.25, glow_mats[s["livery"]])
        if s["state"] != "docked":
            add_box(f"ship{i}_eng", polar(hd, -L_ * 0.5, c), hd, 2, B_ * 0.8, B_ * 0.3, 0, glow_mats["vest"])
            add_box(f"ship{i}_plume", polar(hd, -L_ * 0.5 - L_ * 0.3, c), hd, L_ * 0.6, B_ * 0.18, B_ * 0.1, 0, plume)

    # ---- lanes: rows of lights on the plane
    lane_mats = {}
    lm = bpy.data.meshes.new("lanes"); bm = bmesh.new()
    lane_cols = bm.faces.layers.float_color.new("col")
    for ln in opt["lanes"]:
        pts = ln["pts"] + ([ln["pts"][0]] if ln["closed"] else [])
        step = 26.0 if ln["kind"] != "street" else 40.0
        acc_ = 0.0
        for i in range(len(pts) - 1):
            a, b = pts[i], pts[i + 1]
            d = math.dist(a, b)
            if d < 1e-6:
                continue
            t = (step - acc_) / d
            while t <= 1.0:
                p = (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)
                r = 1.6 if ln["kind"] != "street" else 1.0
                vs = [bm.verts.new((p[0] + dx, p[1] + dy, 0.5)) for dx, dy in ((-r, -r), (r, -r), (r, r), (-r, r))]
                f = bm.faces.new(vs); f[lane_cols] = hexrgb(LANE.get(ln["kind"], "#9FE0C8"))
                t += step / d
            acc_ = (acc_ + d) % step
    bm.to_mesh(lm); bm.free()
    lo = bpy.data.objects.new("lanes", lm); scene.collection.objects.link(lo)
    m = bpy.data.materials.new("lanelights"); m.use_nodes = True
    nt = m.node_tree; nt.nodes.clear()
    at = nt.nodes.new("ShaderNodeAttribute"); at.attribute_name = "col"; at.attribute_type = "GEOMETRY"
    em = nt.nodes.new("ShaderNodeEmission"); em.inputs["Strength"].default_value = 30.0
    nt.links.new(at.outputs["Color"], em.inputs["Color"])
    out = nt.nodes.new("ShaderNodeOutputMaterial"); nt.links.new(em.outputs[0], out.inputs[0])
    lm.materials.append(m)

    # ---- set pieces
    red = mat_emit("beacon", "#FF463C", 60.0)
    for b in opt["buildings"]:
        if b["kind"] in ("spire", "spire_top", "gatehouse", "rib_tip", "relic_foot") or (b["kind"] == "sanctum" and b["h"] > 280):
            c = (sum(p[0] for p in b["pts"]) / len(b["pts"]), sum(p[1] for p in b["pts"]) / len(b["pts"]))
            bpy.ops.mesh.primitive_uv_sphere_add(radius=6, location=(c[0], c[1], b["h"] + 8))
            bpy.context.active_object.data.materials.append(red)
    for fx in opt["fx"]:
        t = fx["type"]
        if t == "moon":
            bpy.ops.mesh.primitive_uv_sphere_add(radius=fx["r"], location=(fx["c"][0], fx["c"][1], 0), segments=96, ring_count=48)
            bpy.context.active_object.data.materials.append(mat_flat("moon", "#6A675E", 0.9, 0.0))
        elif t == "reactor":
            bpy.ops.mesh.primitive_uv_sphere_add(radius=40, location=(fx["c"][0], fx["c"][1], 0))
            bpy.context.active_object.data.materials.append(mat_emit("reactor", "#FF8A2A", 40.0))
            pl = bpy.data.lights.new("reactor_l", "POINT"); pl.energy = 4e8; pl.color = hexrgb("#FF8A2A")[:3]
            lo_ = bpy.data.objects.new("reactor_l", pl); lo_.location = (fx["c"][0], fx["c"][1], 60); scene.collection.objects.link(lo_)
        elif t == "relic_gate":
            a, b = fx["a"], fx["b"]
            R = math.dist(a, b) / 2
            bpy.ops.mesh.primitive_torus_add(major_radius=R, minor_radius=55, major_segments=160, minor_segments=16,
                                             location=((a[0] + b[0]) / 2, (a[1] + b[1]) / 2, 0), rotation=(math.radians(90), 0, 0))
            bpy.context.active_object.data.materials.append(mat_flat("relic", "#ECE7DC", 0.3, 0.6))
            bpy.ops.mesh.primitive_torus_add(major_radius=R - 70, minor_radius=6, major_segments=160, minor_segments=8,
                                             location=((a[0] + b[0]) / 2, (a[1] + b[1]) / 2, 0), rotation=(math.radians(90), 0, 0))
            bpy.context.active_object.data.materials.append(mat_emit("relic_glow", "#FFC86B", 30.0))
        elif t == "portal":
            bpy.ops.mesh.primitive_torus_add(major_radius=fx["width"] / 2, minor_radius=4, location=(fx["c"][0], fx["c"][1], 0),
                                             rotation=(math.radians(90), 0, -math.radians(fx["facing"])))
            bpy.context.active_object.data.materials.append(mat_emit("portal", "#A8CCFF", 30.0))
        elif t == "field_wall":
            a, b = fx["a"], fx["b"]
            c = ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2); hd = math.degrees(math.atan2(b[0] - a[0], b[1] - a[1]))
            add_box("field", c, hd, math.dist(a, b), 1.5, 40, 0, mat_emit("field", "#9FE0C8", 3.0))
        elif t == "hulk":
            add_box("hulk", fx["c"], fx["heading"], fx["length"], fx["width"], fx["width"] * 0.5, 0, mat_flat("hulk", "#4E4C46", 0.8, 0.2))
        elif t in ("target_station",):
            bpy.ops.mesh.primitive_cylinder_add(vertices=8, radius=60, depth=40, location=(fx["c"][0], fx["c"][1], 0))
            bpy.context.active_object.data.materials.append(mat_flat("tgt", "#8A8478"))
        elif t == "scanner":
            add_box("scan", ((fx["a"][0] + fx["b"][0]) / 2, fx["a"][1]), 90, math.dist(fx["a"], fx["b"]), 3, 3, 30,
                    mat_emit("scan", "#BFD6FF", 40.0))
        elif t == "marker":
            bpy.ops.mesh.primitive_uv_sphere_add(radius=5, location=(fx["c"][0], fx["c"][1], 0))
            bpy.context.active_object.data.materials.append(mat_emit("marker", "#7CFF9A", 50.0))
    # ---- holo signs: the city's name over the mouth and one word per quarter, facing the camera
    font = bpy.data.fonts.load(FONT)
    for h in opt["holos"]:
        if h.get("small"):
            continue
        cu = bpy.data.curves.new("holo", "FONT"); cu.body = h["text"]; cu.font = font
        cu.size = 150 if h.get("grand") else 46; cu.align_x = "CENTER"; cu.align_y = "CENTER"
        o = bpy.data.objects.new("holo", cu); scene.collection.objects.link(o)
        o.location = (h["at"][0], h["at"][1] - (200 if h.get("grand") else 0), 330 if h.get("grand") else 170)
        o.rotation_euler = (math.radians(35), 0, 0)
        o.data.materials.append(mat_emit("holo_" + h["district"], DISTRICT.get(h["district"], "#FFF1D6"), 8.0))

    # ---- light: a hard key pitched down from the north-east, a weak fill
    sun = bpy.data.lights.new("key", "SUN"); sun.energy = 3.2; sun.angle = math.radians(0.5)
    so = bpy.data.objects.new("key", sun); so.rotation_euler = (math.radians(50), 0, math.radians(140))
    scene.collection.objects.link(so)
    fill = bpy.data.lights.new("fill", "SUN"); fill.energy = 0.35; fill.color = (0.6, 0.7, 1.0)
    fo = bpy.data.objects.new("fill", fill); fo.rotation_euler = (math.radians(60), 0, math.radians(-40))
    scene.collection.objects.link(fo)

    # a gas giant far below the plane fills the gaps between the towers
    ld_world.planet(S, "planet", 26000.0, (14000, 52000, -52000), S["planet"], ld_world.sun_dir(S))
    cam = ld_scenes.make_camera(scene)
    cam.data.clip_end = 200000
    sp = opt["spawn"]["c"]

    def shot(name, target, dist, pitch=55.0, yaw=0.0):
        # the game camera: behind and above the target, pitched down
        p = math.radians(pitch)
        back = polar(yaw + 180, dist * math.cos(p), target)
        cam.location = (back[0], back[1], dist * math.sin(p))
        cam.data.sensor_fit = "VERTICAL"; cam.data.angle = math.radians(40)
        cam.rotation_euler = (math.radians(90 - pitch), 0, -math.radians(yaw))
        path = os.path.join(OUT, f"{name}_{key.lower()}.jpg")
        scene.render.filepath = path
        scene.render.image_settings.file_format = "JPEG"; scene.render.image_settings.quality = 90
        bpy.ops.render.render(write_still=True)
        print("HUB wrote", path)

    # the player's ship at the spawn
    add_box("player", sp, 0, 28, 12, 6, 0, ship_mats["noct"])
    add_box("player_eng", polar(0, -14, sp), 0, 2, 9, 3, 0, glow_mats["vest"])
    add_box("player_plume", polar(0, -30, sp), 0, 28, 4, 2, 0, plume)
    arrive = {"A": ((sp[0], sp[1] + 2050), 3300), "B": ((sp[0] - 500, sp[1] + 1900), 2900), "C": ((sp[0], sp[1] + 1700), 2700)}
    entry = {"A": ((70, -1750), 900), "B": ((600, -1700), 1300), "C": ((0, 300), 1300)}
    shot("hub_arrival", *arrive[key])
    shot("hub_entry", *entry[key])
    print("HUB done")


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    keys = (args[0].split(",") if args else ["A", "B", "C"])
    draft = "--draft" in sys.argv
    os.makedirs(OUT, exist_ok=True)
    for k in keys:
        log = os.path.join(ROOT, "Logs", "tools", f"hub-arrival-{k}.log")
        os.makedirs(os.path.dirname(log), exist_ok=True)
        cmd = [BLENDER, "-b", "--factory-startup", "--python", os.path.abspath(__file__), "--", "--blender", k] + (["--draft"] if draft else [])
        with open(log, "w") as fh:
            rc = subprocess.call(cmd, stdout=fh, stderr=subprocess.STDOUT)
        txt = open(log).read()
        print(k, "ok" if "HUB done" in txt else f"FAILED rc={rc} log={log}")
        if "HUB done" not in txt:
            print("\n".join(l for l in txt.splitlines() if "Error" in l or "Traceback" in l or "line " in l)[-3000:])


if __name__ == "__main__":
    if "--blender" in sys.argv:
        a = sys.argv[sys.argv.index("--blender") + 1:]
        blender_main(a[0], "--draft" in a)
    else:
        main()
