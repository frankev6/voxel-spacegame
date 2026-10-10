"""Shots for the look-development pass (inside Blender)."""

import math
import os
import random

import bpy
from mathutils import Euler, Matrix, Vector

import ld_build
import ld_world
from ld_blocks import KIT, CATEGORY, build_block
from ld_geom import CELL, Acc, box, cyl, ico, lathe, sphere, mesh_cells, hash01
from ld_mats import G, lin, new_mat, finish
from ld_ships import SHIPS, realize as realize_ship, add_greebles, Ship
from ld_stations import STATIONS


# ======================================================================================================================
# Render setup
# ======================================================================================================================

def setup_render(S, scene, draft=False):
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 960 if draft else 1920
    scene.render.resolution_y = 540 if draft else 1080
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = False
    ee = scene.eevee
    ee.taa_render_samples = 12 if draft else 48
    ee.use_raytracing = True
    ee.use_shadows = True
    ee.shadow_ray_count = 2
    ee.shadow_step_count = 8
    ee.use_fast_gi = True
    vs = scene.view_settings
    try:
        vs.view_transform = S["view"]
    except TypeError:
        vs.view_transform = "AgX"
    try:
        vs.look = S["look"]
    except TypeError:
        pass
    vs.exposure = S.get("exposure", 0.0)
    setup_compositor(S, scene)


def setup_compositor(S, scene):
    try:
        tree = scene.compositing_node_group
        if tree is None:
            tree = bpy.data.node_groups.new("Compositor", "CompositorNodeTree")
            scene.compositing_node_group = tree
        for n in list(tree.nodes):
            tree.nodes.remove(n)
        rl = tree.nodes.new("CompositorNodeRLayers")
        glare = tree.nodes.new("CompositorNodeGlare")
        thr, strength = S["bloom"]
        glare.inputs["Type"].default_value = "Bloom"
        glare.inputs["Quality"].default_value = "High"
        glare.inputs["Threshold"].default_value = thr
        glare.inputs["Strength"].default_value = strength
        glare.inputs["Size"].default_value = 0.7
        hsv = tree.nodes.new("CompositorNodeHueSat")
        hsv.inputs["Saturation"].default_value = S.get("saturation", 1.0)
        tree.links.new(rl.outputs["Image"], glare.inputs["Image"])
        tree.links.new(glare.outputs["Image"], hsv.inputs["Image"])
        out = tree.nodes.new("NodeGroupOutput")
        if not tree.interface.items_tree:
            tree.interface.new_socket("Image", in_out="OUTPUT", socket_type="NodeSocketColor")
        tree.links.new(hsv.outputs["Image"], out.inputs[0])
    except Exception as e:
        print("compositor skipped:", e)


# ======================================================================================================================
# Helpers
# ======================================================================================================================

class Things:
    def __init__(self):
        self.items = {}

    def add(self, name, root, objs=None):
        self.items[name] = root

    def objects(self, name):
        root = self.items[name]
        return [root] + list(root.children_recursive)

    def show(self, names):
        for n in self.items:
            vis = n in names
            for o in self.objects(n):
                o.hide_render = not vis
                o.hide_viewport = not vis

    def place(self, name, loc, rot=(0, 0, 0)):
        r = self.items[name]
        r.location = loc
        r.rotation_euler = [math.radians(a) for a in rot]


def make_camera(scene, name="CAM"):
    cd = bpy.data.cameras.get(name) or bpy.data.cameras.new(name)
    cd.clip_start = 0.1
    cd.clip_end = 80000
    ob = bpy.data.objects.get(name)
    if ob is None:
        ob = bpy.data.objects.new(name, cd)
        scene.collection.objects.link(ob)
    scene.camera = ob
    return ob


def aim(cam, loc, target, lens=None, fov_v=None, ortho=None):
    cam.location = loc
    d = Vector(target) - Vector(loc)
    ld_world.set_view_yaw(math.atan2(-d.x, d.y))
    cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    cd = cam.data
    if ortho:
        cd.type = "ORTHO"
        cd.ortho_scale = ortho
    else:
        cd.type = "PERSP"
        if fov_v:
            cd.sensor_fit = "VERTICAL"
            cd.angle_y = math.radians(fov_v)
        else:
            cd.sensor_fit = "AUTO"
            cd.lens = lens or 35


def orbit(target, dist, yaw, pitch):
    """Camera position around target. yaw 0 = in front of the bow (+Y), positive yaw toward +X."""
    t = Vector(target)
    y, p = math.radians(yaw), math.radians(pitch)
    return tuple(t + Vector((math.sin(y) * math.cos(p), math.cos(y) * math.cos(p), math.sin(p))) * dist)


def render(scene, path, jpeg=True, size=None, transparent=False):
    r = scene.render
    if size:
        r.resolution_x, r.resolution_y = size
    r.film_transparent = transparent
    s = r.image_settings
    if jpeg and not transparent:
        s.file_format = "JPEG"
        s.quality = 90
        s.color_mode = "RGB"
    else:
        s.file_format = "PNG"
        s.color_mode = "RGBA"
        s.color_depth = "8"
    r.filepath = path
    bpy.ops.render.render(write_still=True)
    print("RENDERED", path)


def realize_acc(acc, mats, S, name, things, centre=None, subdiv_levels=None):
    empties = ld_build.realize(acc, mats, S, name, subdiv_levels=subdiv_levels)
    root = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(root)
    for g, e in empties.items():
        e.parent = root
        if centre is not None:
            e.location = -Vector(centre)
    things.add(name, root)
    return root


def build_ship(ship, S, mats, things, name, subdiv_levels=None, plumes=True):
    acc = Acc()
    realize_ship(ship, S, acc, "ship", plumes=plumes)
    mn, mx = ship.bounds()
    c = (mn + mx) / 2
    root = realize_acc(acc, mats, S, name, things, centre=c, subdiv_levels=subdiv_levels)
    root["size"] = tuple(mx - mn)
    return root, (mx - mn)


# ======================================================================================================================
# Stage: block kit icons and the hull-variant patch
# ======================================================================================================================

def stage_kit(S, scene, mats, out, only=None):
    ld_world.setup_lights(S, scene)
    things = Things()
    os.makedirs(os.path.join(out, "icons"), exist_ok=True)
    kinds = list(KIT) if not only else only.split(",")
    for i, kind in enumerate(kinds):
        acc = Acc()
        build_block(kind, S, acc, "b", Matrix.Identity(4))
        size = Vector(KIT[kind]) * CELL
        realize_acc(acc, mats, S, kind, things, centre=size / 2)
        things.place(kind, (i * 20.0, 0, 0))
    cam = make_camera(scene)
    for i, kind in enumerate(kinds):
        things.show({kind})
        size = Vector(KIT[kind]) * CELL
        target = Vector((i * 20.0, 0, 0))
        # thrusters seen from behind, everything else from front-right-above
        if kind.startswith("thruster"):
            loc = orbit(target, 30, 180 + 40, 25)
        else:
            loc = orbit(target, 30, 40, 28)
        ext = max(size.x, size.y, size.z)
        if kind in ("railgun", "beam", "autocannon", "flak"):
            ext *= 1.25
        aim(cam, loc, target, ortho=ext * 1.75)
        render(scene, os.path.join(out, "icons", f"{kind}.png"), size=(384, 384), transparent=True)
    if not only:
        hull_patch(S, scene, mats, out, things, cam)


def hull_patch(S, scene, mats, out, things, cam):
    """A slab of hull with the direction's procedural variants: shows how plain hull gets windows, hatches, pipes."""
    s = Ship("patch", 77)
    s.box(-12, 12, 0, 40, 0, 4)
    s.box(-8, 8, 6, 30, 4, 7)
    s.box(-3, 3, 12, 24, 7, 9)
    s.paint(lambda x, y, z: z < 2, "hull2")
    dens = dict(S["greeble"])
    S2 = dict(S)
    S2["greeble"] = {k: min(0.5, v * 2.2) for k, v in dens.items()}
    acc = Acc()
    realize_ship(s, S2, acc, "ship", plumes=False, nav=False)
    mn, mx = s.bounds()
    realize_acc(acc, mats, S, "patch", things, centre=(mn + mx) / 2)
    things.place("patch", (0, 0, -300))
    things.show({"patch"})
    aim(cam, orbit((0, 0, -300), 26, 125, 32), (0, 1.0, -300.5), lens=50)
    render(scene, os.path.join(out, "hull_variants.jpg"), size=(1600, 900))


# ======================================================================================================================
# Stage: scenes
# ======================================================================================================================

def smoke_material(S):
    m = new_mat(f"{S['name']}_smoke")
    g = G(m.node_tree)
    bsdf = g.n("ShaderNodeBsdfPrincipled", Roughness=1.0)
    g.set(g.sock(bsdf.inputs, "Base Color"), lin("#2A2826") if S["shape"] != "retro" else lin("#3A3550"))
    lw = g.n("ShaderNodeLayerWeight", Blend=0.4)
    g.set(g.sock(bsdf.inputs, "Alpha"), g.math("MULTIPLY", g.out(lw, "Facing"), 0.85))
    return finish(g, bsdf.outputs[0], m, blended=True)


def explosion(acc, group, c, size, seed=0):
    rnd = random.Random(seed)
    c = Vector(c)
    acc.add(group, "ember", ico(tuple(c), size * 0.55, 2, jitter=0.3, seed=seed), None, True)
    acc.add(group, "emit_p", ico(tuple(c), size * 0.3, 1, jitter=0.2, seed=seed + 1), None, True)
    for i in range(6):
        d = Vector((rnd.uniform(-1, 1), rnd.uniform(-1, 1), rnd.uniform(-0.6, 1))).normalized()
        acc.add(group, "ember", ico(tuple(c + d * size * 0.6), size * rnd.uniform(0.2, 0.4), 1, jitter=0.3, seed=seed + 10 + i), None, True)
        acc.add(group, "smoke", ico(tuple(c + d * size * 1.1), size * rnd.uniform(0.35, 0.7), 2, jitter=0.25, seed=seed + 20 + i), None, True)
    for i in range(18):
        d = Vector((rnd.uniform(-1, 1), rnd.uniform(-1, 1), rnd.uniform(-1, 1))).normalized()
        p0 = c + d * size * 0.4
        L = size * rnd.uniform(1.2, 3.0)
        v, f = cyl(tuple(p0), tuple(p0 + d * L), size * 0.025, 6, caps=False)
        acc.add_fade(group, "tracer", (v, f), None, [1.0] * 6 + [0.0] * 6)


def beam(acc, group, p0, p1, r=0.1):
    v, f = cyl(tuple(p0), tuple(p1), r, 10, caps=False)
    acc.add_fade(group, "beam", (v, f), None, [1.0] * len(v))
    v, f = cyl(tuple(p0), tuple(p1), r * 3.0, 12, caps=False)
    acc.add_fade(group, "plume", (v, f), None, [0.8] * len(v))
    acc.add(group, "emit_w", sphere(tuple(p1), r * 4, 12, 6), None, True)


def tracers(acc, group, p0, p1, n, seed=0, length=2.5):
    rnd = random.Random(seed)
    a, b = Vector(p0), Vector(p1)
    d = (b - a).normalized()
    for i in range(n):
        t = rnd.uniform(0.1, 0.95)
        p = a + (b - a) * t + Vector((rnd.uniform(-1, 1), rnd.uniform(-1, 1), rnd.uniform(-0.5, 0.5))) * 0.4
        v, f = cyl(tuple(p), tuple(p - d * length), 0.06, 6, caps=False)
        acc.add_fade(group, "tracer", (v, f), None, [1.0] * 6 + [0.0] * 6)


def debris(acc, group, removed, crater_c, S, seed=0, n_chunks=14, spread=(4, 14)):
    rnd = random.Random(seed)
    cells = [c for c, t in removed if t != "module"]
    if not cells:
        return
    for k in range(n_chunks):
        seedc = cells[rnd.randrange(len(cells))]
        chunk = {c: "hull" for c in cells if max(abs(c[0] - seedc[0]), abs(c[1] - seedc[1]), abs(c[2] - seedc[2])) <= rnd.choice((0, 1, 1, 2))}
        out, _ = mesh_cells(chunk, {})
        cc = (Vector(seedc) + Vector((0.5, 0.5, 0.5))) * CELL
        d = (cc - Vector(crater_c)).normalized() if (cc - Vector(crater_c)).length > 0.01 else Vector((0, 0, 1))
        d = (d + Vector((rnd.uniform(-0.5, 0.5), rnd.uniform(-0.5, 0.5), rnd.uniform(-0.3, 0.6)))).normalized()
        off = d * rnd.uniform(*spread)
        rot = Euler((rnd.uniform(0, 6.28), rnd.uniform(0, 6.28), rnd.uniform(0, 6.28))).to_matrix().to_4x4()
        M = Matrix.Translation(cc + off) @ rot @ Matrix.Translation(-cc)
        for tag, vf in out.items():
            v = [tuple(M @ Vector(p)) for p in vf[0]]
            hot = rnd.uniform(0.3, 0.9)
            if rnd.random() < 0.5:
                acc.add_fade(group, "interior", (v, vf[1]), None, [hot] * len(v))
            else:
                acc.add(group, "hull", (v, vf[1]), None, False)


def builder_grid(S, size_m=60, cell=CELL):
    m = new_mat(f"{S['name']}_buildgrid")
    g = G(m.node_tree)
    tc = g.n("ShaderNodeTexCoord")
    p = g.vmath("SCALE", g.out(tc, "Object"), scale=1.0 / cell)
    f = g.vmath("FRACTION", p)
    e = g.vmath("ABSOLUTE", g.vmath("SUBTRACT", f, (0.5, 0.5, 0.5)))
    xyz = g.sep(e)
    line = g.maprange(g.math("MAXIMUM", xyz[0], xyz[1]), 0.465, 0.49, 0.0, 1.0)
    p4 = g.vmath("SCALE", g.out(tc, "Object"), scale=1.0 / (cell * 8))
    f4 = g.vmath("FRACTION", p4)
    e4 = g.vmath("ABSOLUTE", g.vmath("SUBTRACT", f4, (0.5, 0.5, 0.5)))
    xyz4 = g.sep(e4)
    major = g.maprange(g.math("MAXIMUM", xyz4[0], xyz4[1]), 0.49, 0.497, 0.0, 1.0)
    r = g.vmath("LENGTH", g.out(tc, "Object"))
    fade = g.maprange(r, size_m * 0.15, size_m * 0.48, 1.0, 0.0)
    k = g.math("MULTIPLY", g.math("ADD", g.math("MULTIPLY", line, 0.35), major), fade)
    ec = S["emit"]["shield"][0]
    em = g.n("ShaderNodeEmission")
    g.set(em.inputs[0], lin(ec))
    g.set(em.inputs[1], g.math("MULTIPLY", k, 2.0))
    tr = g.n("ShaderNodeBsdfTransparent")
    dark = g.n("ShaderNodeBsdfPrincipled", Roughness=0.7, Metallic=0.3)
    g.set(g.sock(dark.inputs, "Base Color"), lin(S["col"]["dark"]))
    mixf = g.n("ShaderNodeMixShader")
    g.set(mixf.inputs[0], g.math("MULTIPLY", fade, 0.92))
    g.set(mixf.inputs[1], tr.outputs[0])
    g.set(mixf.inputs[2], dark.outputs[0])
    add = g.n("ShaderNodeAddShader")
    g.set(add.inputs[0], em.outputs[0])
    g.set(add.inputs[1], mixf.outputs[0])
    finish(g, add.outputs[0], m, blended=True)
    me = bpy.data.meshes.new("buildgrid")
    h = size_m / 2
    me.from_pydata([(-h, -h, 0), (h, -h, 0), (h, h, 0), (-h, h, 0)], [], [(0, 1, 2, 3)])
    me.materials.append(m)
    ob = bpy.data.objects.new("buildgrid", me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def stage_scenes(S, scene, mats, out, only=None, draft=False):
    mats["smoke"] = smoke_material(S)
    lights = ld_world.setup_lights(S, scene)
    sd = ld_world.sun_dir(S)
    things = Things()
    dname = S["_key"]
    light_fn, heavy_fn = SHIPS[dname]

    # --- ships -------------------------------------------------------------------------------------------------
    L = light_fn()
    H = heavy_fn()
    build_ship(L, S, mats, things, "light")
    build_ship(light_fn(), S, mats, things, "light2")
    build_ship(H, S, mats, things, "heavy")
    build_ship(light_fn(), S, mats, things, "light_still", plumes=False)
    build_ship(heavy_fn(), S, mats, things, "heavy_still", plumes=False)
    Lsize = Vector(things.items["light"]["size"])
    Hsize = Vector(things.items["heavy"]["size"])

    # --- damaged heavy for combat --------------------------------------------------------------------------------
    D = heavy_fn()
    mn, mx = D.bounds()
    cells_mn, cells_mx = mn / CELL, mx / CELL
    rnd = random.Random(4)
    craters = []
    for k in range(3):
        cy = cells_mn.y + (cells_mx.y - cells_mn.y) * (0.3 + 0.22 * k)
        cx = cells_mx.x - 1 if k != 1 else cells_mn.x + 1
        cz = cells_mn.z + (cells_mx.z - cells_mn.z) * 0.6
        removed = D.carve((cx, cy, cz), 5.5 + k * 1.5, seed=k)
        craters.append(((Vector((cx, cy, cz))) * CELL, removed))
    acc = Acc()
    realize_ship(D, S, acc, "ship")
    for i, (cc, removed) in enumerate(craters):
        debris(acc, "debris", removed, cc, S, seed=i, n_chunks=10)
        explosion(acc, "fx", cc + Vector((0, 0, 0.5)), 2.2 + i * 0.6, seed=i * 7)
    c = (mn + mx) / 2
    realize_acc(acc, mats, S, "heavy_dmg", things, centre=c)
    crater_world = [cc - c for cc, _ in craters]

    # --- station -----------------------------------------------------------------------------------------------
    st = STATIONS[dname]()
    acc = Acc()
    realize_ship(st, S, acc, "station", plumes=False, nav=False)
    smn, smx = st.bounds()
    realize_acc(acc, mats, S, "station", things, centre=(smn + smx) / 2, subdiv_levels=1 if S.get("subdiv") else None)
    ssize = smx - smn

    # --- planet, moon, rocks, grid -----------------------------------------------------------------------------
    P = S["planet"]
    pr = 3200.0
    pl = ld_world.planet(S, "planet", pr, (0, 0, 0), P, sd)
    proot = bpy.data.objects.new("planet_root", None)
    scene.collection.objects.link(proot)
    for o in [o for o in scene.objects if o.name.startswith("planet") and o is not proot]:
        o.parent = proot
    things.add("planet", proot)
    mo = ld_world.moon(S, "moon", 380.0, (0, 0, 0))
    mroot = bpy.data.objects.new("moon_root", None)
    scene.collection.objects.link(mroot)
    mo.parent = mroot
    things.add("moon", mroot)
    rocks = ld_world.asteroids(S, "rocks", 70, (0, 0, 0), (160, 160, 14), size=(0.6, 5.5), seed=3)
    rroot = bpy.data.objects.new("rocks_root", None)
    scene.collection.objects.link(rroot)
    rocks.parent = rroot
    things.add("rocks", rroot)
    grid = builder_grid(S)
    groot = bpy.data.objects.new("grid_root", None)
    scene.collection.objects.link(groot)
    grid.parent = groot
    things.add("grid", groot)

    # shield bubble for combat
    acc = Acc()
    acc.add("fx", "shieldfx", sphere((0, 0, 0), 1.0, 48, 24, (Lsize.x * 0.75, Lsize.y * 0.65, Lsize.z * 1.4)), None, True)
    realize_acc(acc, mats, S, "shield", things)

    cam = make_camera(scene)
    shots = {}

    def shot(name):
        return (not only) or name in only.split(",")

    # ---------------------------------------------------------------------------------------------------------------
    # 1. ship close-ups
    if shot("ship_light"):
        things.show({"light_still", "planet", "moon"})
        things.place("light_still", (0, 0, 0), (0, 0, 0))
        things.place("planet", (-2600, -7800, -3000))
        things.place("moon", (900, -6000, 600))
        dist = Lsize.length * 1.45
        aim(cam, orbit((0, 0, 0), dist, 38, 20), (0, -0.5, -0.4), lens=50)
        render(scene, os.path.join(out, "ship_light.jpg"))
    if shot("ship_heavy"):
        things.show({"heavy_still", "planet", "moon"})
        things.place("heavy_still", (0, 0, 0), (0, 0, 0))
        things.place("planet", (2400, -8200, -2600))
        things.place("moon", (-1500, -7000, 900))
        dist = Hsize.length * 1.25
        aim(cam, orbit((0, 0, 0), dist, -42, 18), (0, -1.0, -1.5), lens=50)
        render(scene, os.path.join(out, "ship_heavy.jpg"))

    # 2. hero composition
    if shot("hero"):
        things.show({"light", "light2", "heavy", "station", "planet", "moon", "rocks"})
        things.place("heavy", (-24, 105, 6), (4, -3, -62))
        things.place("light", (9, 36, -2.5), (6, 8, -38))
        things.place("light2", (24, 58, 3.5), (4, 6, -40))
        smax = max(ssize.x, ssize.y, ssize.z)
        things.place("station", (smax * 0.9, smax * 3.2, smax * 0.15), (0, 0, 25))
        things.place("planet", (-3600, 9800, -3800))
        things.place("moon", (3200, 11000, 2200))
        things.place("rocks", (0, 260, -20))
        aim(cam, (0, 0, 0), (6, 100, 2), lens=30)
        render(scene, os.path.join(out, "hero.jpg"))

    # 3. gameplay camera: 55 degree pitch, 40 degree vertical FOV (SPEC-62)
    if shot("flight"):
        things.show({"light", "heavy", "station", "planet", "rocks"})
        things.place("light", (0, 0, 0), (0, 0, 0))
        things.place("heavy", (-30, 30, -3), (0, 0, 8))
        smax = max(ssize.x, ssize.y, ssize.z)
        things.place("station", (smax * 0.6, 420, -40), (0, 0, 30))
        things.place("planet", (-2200, 3600, -4300))
        things.place("rocks", (10, 60, -6))
        d = 48.0
        aim(cam, (0, -d * math.cos(math.radians(55)), d * math.sin(math.radians(55))), (0, 2.0, 0), fov_v=40)
        render(scene, os.path.join(out, "flight.jpg"))

    # 4. combat at gameplay camera
    if shot("combat"):
        things.show({"light", "light2", "heavy_dmg", "planet", "rocks", "shield"})
        things.place("heavy_dmg", (6, 22, 0), (0, 0, 80))
        things.place("light", (-20, -12, 0.5), (0, 0, -55))
        things.place("light2", (-34, 4, -1), (0, 0, -70))
        things.place("shield", (-34, 4, -1), (0, 0, -70))
        things.place("planet", (2600, 3400, -4600))
        things.place("rocks", (0, 30, -8))
        # beams and tracers from the player ships to the craters (in world space, built after placement)
        bpy.context.view_layer.update()
        hd = things.items["heavy_dmg"]
        Mw = hd.matrix_world
        targets = [Mw @ cw for cw in crater_world]
        acc = Acc()
        lw = things.items["light"].matrix_world
        lw2 = things.items["light2"].matrix_world
        beam(acc, "fx", lw @ Vector((Lsize.x * 0.3, Lsize.y * 0.45, 0.2)), targets[0], 0.09)
        beam(acc, "fx", lw @ Vector((-Lsize.x * 0.3, Lsize.y * 0.45, 0.2)), targets[0] + Vector((0.6, 0.4, 0.3)), 0.09)
        tracers(acc, "fx", lw2 @ Vector((0, Lsize.y * 0.5, 0.3)), targets[1], 14, seed=2)
        # enemy return fire hitting the shield
        sh = things.items["shield"].matrix_world @ Vector((Lsize.x * 0.5, Lsize.y * 0.3, 0.6))
        tracers(acc, "fx", Mw @ Vector((0, 8, 3)), sh, 10, seed=5)
        acc.add("fx", "emit_s", ico(tuple(sh), 0.9, 1, jitter=0.4), None, True)
        realize_acc(acc, mats, S, "fxc", things)
        things.show({"light", "light2", "heavy_dmg", "planet", "rocks", "shield", "fxc"})
        d = 70.0
        aim(cam, (-4, 6 - d * math.cos(math.radians(55)), d * math.sin(math.radians(55))), (-4, 8, 0), fov_v=40)
        pts = []
        for i, t in enumerate(targets):
            ld = bpy.data.lights.new(f"boom{i}", "POINT")
            ld.color = ld_world.hex_rgb(S["emit"]["ember"][0])
            ld.energy = 25000
            ld.shadow_soft_size = 2.0
            ob = bpy.data.objects.new(f"boom{i}", ld)
            ob.location = t + Vector((0, 0, 2))
            scene.collection.objects.link(ob)
            pts.append(ob)
        render(scene, os.path.join(out, "combat.jpg"))
        for ob in pts:
            ob.hide_render = True

    # 5. station
    if shot("station"):
        things.show({"station", "light", "heavy", "planet", "moon"})
        smax = max(ssize.x, ssize.y, ssize.z)
        things.place("station", (0, 0, 0), (0, 0, 0))
        things.place("light", (smax * 0.35, -smax * 0.55, smax * 0.05), (0, 0, 165))
        things.place("heavy", (-smax * 0.5, -smax * 0.3, -smax * 0.12), (0, 0, 120))
        things.place("planet", (-2000, 8200, -2600))
        things.place("moon", (2600, 9000, 1400))
        work = []
        if getattr(st, "work_lights", None):
            for i, p in enumerate(st.work_lights):
                ld = bpy.data.lights.new(f"work{i}", "SPOT")
                ld.color = (1.0, 0.86, 0.62)
                ld.energy = 30000
                ld.spot_size = math.radians(70)
                ld.spot_blend = 0.6
                ob = bpy.data.objects.new(f"work{i}", ld)
                ob.location = Vector(p) - (smn + smx) / 2
                ob.rotation_euler = (0, 0, 0)
                ob.rotation_euler = (Vector((0, 0, -1))).to_track_quat("-Z", "Y").to_euler()
                ob.rotation_euler = (math.radians(180 + (25 if p[0] < 0 else -25)), 0, math.radians(90))
                scene.collection.objects.link(ob)
                work.append(ob)
        aim(cam, orbit((0, 0, 0), smax * 1.25, -38, 16), (0, 0, -smax * 0.05), lens=35)
        render(scene, os.path.join(out, "station.jpg"))
        for ob in work:
            ob.hide_render = True

    # 6. planet reveal (discovery)
    if shot("planet"):
        things.show({"planet", "moon", "light"})
        things.place("planet", (600, 6200, -1400))
        things.place("moon", (-1900, 4200, 900))
        k = Lsize.length
        things.place("light", (0.12 * k, 2.4 * k, -0.5 * k), (6, 0, -25))
        aim(cam, (0, 0, 0), (0, 100, -6), lens=24)
        render(scene, os.path.join(out, "planet.jpg"))

    # 7. builder (Blueprint Lab): the ship on the cell grid
    if shot("builder"):
        things.show({"light_still", "grid"})
        things.place("light_still", (0, 0, 0), (0, 0, 0))
        things.place("grid", (0, 0, -Lsize.z / 2 - 0.02))
        aim(cam, orbit((0, 0, 0), Lsize.length * 1.6, 145, 32), (0, 0.5, -0.8), lens=45)
        render(scene, os.path.join(out, "builder.jpg"))
