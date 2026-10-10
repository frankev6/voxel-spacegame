"""Space environment per direction: sky, lights, planets, moons, rings, asteroids (inside Blender)."""

import math
import random

import bpy
from mathutils import Euler, Matrix, Vector

from ld_mats import G, lin, new_mat, finish, hex_rgb
from ld_geom import ico


SUN_SOCKETS = []      # shader inputs that hold the world-space direction toward the sun (updated per shot)
RIG = {"root": None, "S": None}


def toward(L):
    """Direction toward a light given azimuth (0 = from behind the camera, + = from the right) and elevation."""
    az, el = math.radians(L["az"]), math.radians(L["el"])
    return Vector((math.cos(el) * math.sin(az), -math.cos(el) * math.cos(az), math.sin(el))).normalized()


def sun_dir(S, yaw=0.0):
    return (Matrix.Rotation(yaw, 3, "Z") @ toward(S["sun"])).normalized()


def set_view_yaw(yaw):
    """Rotate the light rig so 'az = 0' is behind the current camera; update planet shaders."""
    if RIG["root"] is not None:
        RIG["root"].rotation_euler = (0, 0, yaw)
    d = sun_dir(RIG["S"], yaw)
    for sock in SUN_SOCKETS:
        sock.default_value = tuple(d)


def setup_world(S, scene):
    W = S["world"]
    world = bpy.data.worlds.get("LD") or bpy.data.worlds.new("LD")
    scene.world = world
    world.use_nodes = True
    g = G(world.node_tree)
    tc = g.n("ShaderNodeTexCoord")
    d = g.out(tc, "Generated")
    dn = g.vmath("NORMALIZE", g.vmath("SUBTRACT", d, (0.0, 0.0, 0.0)))
    kind = W["kind"]
    bg = lin(W["bg"])
    sky = bg
    if kind in ("poster", "candy"):
        cols = [lin(c) for c in W["sky"]]
        # gradient across a tilted axis so the horizon band sits low in frame
        axis = (0.25, -0.35, 0.9) if kind == "poster" else (0.0, -0.3, 1.0)
        t = g.math("ADD", g.math("MULTIPLY", g.vmath("DOT_PRODUCT", dn, axis), 0.5), 0.5)
        nz = g.n("ShaderNodeTexNoise", Scale=2.0, Detail=3.0)
        g.set(nz.inputs["Vector"], dn)
        t = g.math("ADD", t, g.math("MULTIPLY", g.math("SUBTRACT", g.out(nz, "Fac"), 0.5), 0.25 if kind == "poster" else 0.1))
        interp = "CONSTANT" if kind == "poster" else "EASE"
        sky = g.ramp(t, [(0.0, cols[3]), (0.32, cols[2]), (0.5, cols[1]), (0.72, cols[0])], interp)
        if kind == "poster":
            # posterised nebula bands
            nb = g.n("ShaderNodeTexNoise", Scale=1.2, Detail=2.0, Distortion=1.5)
            g.set(nb.inputs["Vector"], dn)
            band = g.ramp(g.out(nb, "Fac"), [(0.0, (0, 0, 0, 1)), (0.62, (0, 0, 0, 1)), (0.66, (1, 1, 1, 1))], "CONSTANT")
            sky = g.mix(g.math("MULTIPLY", g.n("ShaderNodeRGBToBW", band).outputs[0], 0.35), sky, cols[2])
    neb = W.get("nebula")
    if neb:
        c1, c2, k = lin(neb[0]), lin(neb[1]), neb[2]
        n1 = g.n("ShaderNodeTexNoise", Scale=1.4, Detail=10.0, Roughness=0.62, Distortion=0.35)
        g.set(n1.inputs["Vector"], dn)
        n2 = g.n("ShaderNodeTexNoise", Scale=0.7, Detail=2.0)
        g.set(n2.inputs["Vector"], dn)
        mask = g.maprange(g.out(n2, "Fac"), 0.42, 0.7, 0.0, 1.0)
        dens = g.math("MULTIPLY", g.maprange(g.out(n1, "Fac"), 0.45, 0.85, 0.0, 1.0), mask)
        colr = g.mix(g.out(n1, "Fac"), c1, c2)
        sky = g.mix(g.math("MULTIPLY", dens, min(1.0, k)), sky, colr, "ADD")
        if kind == "filaments":
            n3 = g.n("ShaderNodeTexNoise", Scale=2.2, Detail=6.0, Distortion=0.6)
            g.set(n3.inputs["Vector"], dn)
            ridge = g.math("ABSOLUTE", g.math("SUBTRACT", g.out(n3, "Fac"), 0.5))
            fil = g.math("MULTIPLY", g.maprange(ridge, 0.0, 0.03, 1.0, 0.0), mask)
            sky = g.mix(g.math("MULTIPLY", fil, 0.22), sky, lin("#5FD0FF"), "ADD")
    dust = W.get("dust")
    if dust:
        n4 = g.n("ShaderNodeTexNoise", Scale=3.0, Detail=8.0)
        g.set(n4.inputs["Vector"], dn)
        dm = g.maprange(g.out(n4, "Fac"), 0.5, 0.8, 0.0, dust[1])
        sky = g.mix(dm, sky, lin(dust[0]), "ADD")
    # stars
    stars_total = None
    for scale, thresh, bright in ((180.0, 0.88, 6.0), (420.0, 0.6, 1.6), (900.0, 0.3, 0.6)):
        vor = g.n("ShaderNodeTexVoronoi", Scale=scale, p_feature="F1")
        g.set(vor.inputs["Vector"], dn)
        dist = g.out(vor, "Distance")
        sel = g.n("ShaderNodeSeparateColor")
        g.set(sel.inputs[0], g.out(vor, "Color"))
        on = g.maprange(sel.outputs[0], thresh, thresh + 0.01, 0.0, 1.0)
        pt = g.math("MULTIPLY", g.maprange(dist, 0.0, 0.06 if scale < 300 else 0.08, 1.0, 0.0), on)
        pt = g.math("MULTIPLY", g.math("POWER", pt, 3.0), bright * W.get("stars", 1.0))
        stars_total = pt if stars_total is None else g.math("ADD", stars_total, pt)
    tint = g.mix(0.5, lin("#BFD4FF"), lin("#FFE6C8"))
    sky = g.mix(stars_total, sky, tint, "ADD")
    if kind == "candy":
        # big soft stars
        vor = g.n("ShaderNodeTexVoronoi", Scale=26.0, p_feature="F1")
        g.set(vor.inputs["Vector"], dn)
        sel = g.n("ShaderNodeSeparateColor")
        g.set(sel.inputs[0], g.out(vor, "Color"))
        on = g.maprange(sel.outputs[0], 0.8, 0.81, 0.0, 1.0)
        soft = g.math("MULTIPLY", g.math("POWER", g.maprange(g.out(vor, "Distance"), 0.0, 0.12, 1.0, 0.0), 2.0), on)
        sky = g.mix(g.math("MULTIPLY", soft, 1.5), sky, lin("#FFF0F8"), "ADD")
    bgn = g.n("ShaderNodeBackground", Strength=1.0)
    g.set(bgn.inputs[0], sky)
    amb = g.n("ShaderNodeBackground", Strength=S["ambient"][1])
    g.set(amb.inputs[0], lin(S["ambient"][0]))
    lp = g.n("ShaderNodeLightPath")
    mixs = g.n("ShaderNodeMixShader")
    g.set(mixs.inputs[0], g.out(lp, "Is Camera Ray"))
    g.set(mixs.inputs[1], amb.outputs[0])
    g.set(mixs.inputs[2], bgn.outputs[0])
    out = g.n("ShaderNodeOutputWorld")
    g.set(out.inputs[0], mixs.outputs[0])
    return world


def setup_lights(S, scene):
    objs = []
    root = bpy.data.objects.new("LD_rig", None)
    scene.collection.objects.link(root)
    RIG["root"], RIG["S"] = root, S
    for key, shadow in (("sun", True), ("fill", False), ("rim", False)):
        L = S[key]
        if L["strength"] <= 0:
            continue
        ld = bpy.data.lights.new(f"LD_{key}", "SUN")
        ld.color = hex_rgb(L["color"])
        ld.energy = L["strength"]
        ld.angle = math.radians(L.get("angle", 1.0))
        ld.use_shadow = shadow
        ob = bpy.data.objects.new(f"LD_{key}", ld)
        ob.rotation_euler = toward(L).to_track_quat("Z", "Y").to_euler()
        scene.collection.objects.link(ob)
        ob.parent = root
        objs.append(ob)
    return objs


# ----------------------------------------------------------------------------------------------------------------------
# Planets
# ----------------------------------------------------------------------------------------------------------------------

def _uv_sphere(name, radius, seg=192, rings=96):
    import bmesh
    me = bpy.data.meshes.new(name)
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=radius)
    bm.to_mesh(me)
    bm.free()
    me.polygons.foreach_set("use_smooth", [True] * len(me.polygons))
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def _lowpoly_planet(name, radius, cols, seed=3):
    import bmesh
    me = bpy.data.meshes.new(name)
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=4, radius=radius)
    rnd = random.Random(seed)
    from mathutils import noise
    for v in bm.verts:
        n = noise.noise(v.co / radius * 2.2 + Vector((seed, 0, 0)))
        v.co *= 1.0 + 0.035 * max(-0.4, n)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    m = new_mat(name + "_mat")
    g = G(m.node_tree)
    geo = g.n("ShaderNodeNewGeometry")
    tc = g.n("ShaderNodeTexCoord")
    h = g.vmath("LENGTH", g.out(tc, "Object"))
    t = g.maprange(h, radius * 0.995, radius * 1.03, 0.0, 1.0)
    col = g.ramp(t, [(0.0, lin(cols[3])), (0.3, lin(cols[0])), (0.42, lin(cols[1])), (0.6, lin(cols[2])), (0.85, lin("#F4EFE6"))], "CONSTANT")
    bsdf = g.n("ShaderNodeBsdfPrincipled", Roughness=0.8)
    g.set(g.sock(bsdf.inputs, "Base Color"), col)
    finish(g, bsdf.outputs[0], m)
    me.materials.append(m)
    return ob


def planet(S, name, radius, location, P, sundir, rot=(0.3, 0.0, 0.0)):
    kind = P["kind"]
    if kind == "lowpoly":
        ob = _lowpoly_planet(name, radius, P["base"])
    else:
        ob = _uv_sphere(name, radius)
        m = new_mat(name + "_surface")
        g = G(m.node_tree)
        tc = g.n("ShaderNodeTexCoord")
        o = g.vmath("SCALE", g.out(tc, "Object"), scale=1.0 / radius)
        geo = g.n("ShaderNodeNewGeometry")
        dotn = g.n("ShaderNodeVectorMath", p_operation="DOT_PRODUCT")
        g.set(dotn.inputs[0], g.out(geo, "Normal"))
        dotn.inputs[1].default_value = tuple(sundir)
        SUN_SOCKETS.append(dotn.inputs[1])
        ndl = dotn.outputs[1]
        night = g.maprange(ndl, -0.05, 0.12, 1.0, 0.0)
        cols = [lin(c) for c in P["base"]]
        bsdf = g.n("ShaderNodeBsdfPrincipled", Roughness=0.85)
        emis = None
        if kind == "giant":
            nz = g.n("ShaderNodeTexNoise", Scale=3.0, Detail=6.0, Distortion=0.4)
            g.set(nz.inputs["Vector"], o)
            sep = g.sep(o)
            lat = g.math("ADD", sep[2], g.math("MULTIPLY", g.math("SUBTRACT", g.out(nz, "Fac"), 0.5), 0.18))
            col = g.ramp(lat, [(0.0, cols[0]), (0.18, cols[1]), (0.3, cols[2]), (0.42, cols[3]), (0.5, cols[0]), (0.58, cols[1]),
                               (0.7, cols[3]), (0.82, cols[2]), (1.0, cols[1])], "CONSTANT" if S.get("toon") else "LINEAR")
            col = g.ramp(g.math("ADD", g.math("MULTIPLY", lat, 0.5), 0.5), [(0.0, cols[2]), (0.1, cols[0]), (0.2, cols[1]), (0.3, cols[3]),
                                                                          (0.4, cols[0]), (0.5, cols[2]), (0.6, cols[1]), (0.7, cols[3]),
                                                                          (0.8, cols[0]), (0.9, cols[2]), (1.0, cols[1])],
                         "CONSTANT" if S.get("toon") else "LINEAR")
        else:
            nz = g.n("ShaderNodeTexNoise", Scale=2.2, Detail=12.0, Roughness=0.58)
            g.set(nz.inputs["Vector"], o)
            h = g.out(nz, "Fac")
            if kind == "earth":
                col = g.ramp(h, [(0.0, cols[0]), (0.5, cols[1]), (0.53, cols[3]), (0.56, cols[2]), (0.7, lin("#5A6B3A")), (0.78, lin("#8A7A5A"))])
                sep = g.sep(o)
                ice = g.maprange(g.math("ABSOLUTE", sep[2]), 0.82, 0.88, 0.0, 1.0)
                col = g.mix(ice, col, lin("#F2F6FA"))
                land = g.maprange(h, 0.5, 0.53, 0.0, 1.0)
            elif kind == "ocean":
                col = g.ramp(h, [(0.0, cols[0]), (0.55, cols[1]), (0.62, cols[2]), (0.66, cols[3]), (0.7, lin("#3A8A6A"))])
                land = g.maprange(h, 0.6, 0.66, 0.0, 1.0)
            elif kind == "night":
                col = g.ramp(h, [(0.0, cols[3]), (0.45, cols[0]), (0.6, cols[1]), (0.75, cols[2])])
                land = g.maprange(h, 0.45, 0.5, 0.0, 1.0)
            else:  # desert
                col = g.ramp(h, [(0.0, cols[3]), (0.35, cols[0]), (0.55, cols[1]), (0.75, cols[2]), (1.0, lin("#F0D0A0"))])
                land = g.maprange(h, 0.3, 0.4, 0.0, 1.0)
            if P.get("lights"):
                lc, ls = P["lights"]
                cn = g.n("ShaderNodeTexNoise", Scale=40.0, Detail=6.0)
                g.set(cn.inputs["Vector"], o)
                city = g.maprange(g.out(cn, "Fac"), 0.58, 0.72, 0.0, 1.0)
                if kind == "ocean":
                    sw = g.n("ShaderNodeTexWave", Scale=2.5, Distortion=6.0, Detail=4.0)
                    g.set(sw.inputs["Vector"], o)
                    city = g.math("MULTIPLY", g.maprange(g.out(sw, "Fac"), 0.75, 0.95, 0.0, 1.0), g.math("SUBTRACT", 1.0, land))
                else:
                    city = g.math("MULTIPLY", city, land)
                emis = (lin(lc), g.math("MULTIPLY", g.math("MULTIPLY", city, night), ls))
            if P.get("cloud") and kind != "desert":
                cc, ca = P["cloud"]
                cl = g.n("ShaderNodeTexNoise", Scale=4.0, Detail=8.0, Distortion=0.8)
                g.set(cl.inputs["Vector"], o)
                cm = g.maprange(g.out(cl, "Fac"), 0.5, 0.75, 0.0, ca)
                col = g.mix(cm, col, lin(cc))
            if kind == "desert" and P.get("cloud"):
                cc, ca = P["cloud"]
                cl = g.n("ShaderNodeTexWave", Scale=1.5, Distortion=8.0, Detail=6.0, p_wave_type="BANDS")
                g.set(cl.inputs["Vector"], o)
                cm = g.maprange(g.out(cl, "Fac"), 0.75, 0.95, 0.0, ca * 0.5)
                col = g.mix(cm, col, lin(cc))
        # unlit shading: lambert from the shot's sun direction, so the terminator is where the sun says
        lam = g.maprange(ndl, -0.08, 0.9, 0.0, 1.0)
        if S.get("toon"):
            lam = g.ramp(lam, [(0.0, (0.06,) * 3), (0.05, (0.45,) * 3), (0.4, (1.0,) * 3)], "CONSTANT")
            lam = g.n("ShaderNodeRGBToBW", lam).outputs[0]
        else:
            lam = g.math("POWER", lam, 0.8)
        lit = g.mix(1.0, col, g.n("ShaderNodeCombineColor", lam, lam, lam).outputs[0], "MULTIPLY")
        lit = g.mix(1.0, lit, (P.get("gain", 1.25),) * 3 + (1,), "MULTIPLY")
        em = g.n("ShaderNodeEmission", Strength=1.0)
        g.set(em.inputs[0], lit)
        shader = em.outputs[0]
        if emis:
            em2 = g.n("ShaderNodeEmission")
            g.set(em2.inputs[0], emis[0])
            g.set(em2.inputs[1], emis[1])
            add = g.n("ShaderNodeAddShader")
            g.set(add.inputs[0], shader)
            g.set(add.inputs[1], em2.outputs[0])
            shader = add.outputs[0]
        finish(g, shader, m)
        ob.data.materials.append(m)
    ob.location = location
    ob.rotation_euler = rot
    atmo(S, name, radius, location, P, sundir)
    if P.get("ring"):
        ring(S, name, radius, location, P, rot)
    return ob


def atmo(S, name, radius, location, P, sundir):
    ac, astr = P["atmo"]
    ob = _uv_sphere(name + "_atmo", radius * 1.035, 128, 64)
    ob.location = location
    m = new_mat(name + "_atmo")
    g = G(m.node_tree)
    geo = g.n("ShaderNodeNewGeometry")
    lw = g.n("ShaderNodeLayerWeight", Blend=0.12)
    rim = g.math("POWER", g.out(lw, "Facing"), 2.2)
    dotn = g.n("ShaderNodeVectorMath", p_operation="DOT_PRODUCT")
    g.set(dotn.inputs[0], g.out(geo, "Normal"))
    dotn.inputs[1].default_value = tuple(sundir)
    SUN_SOCKETS.append(dotn.inputs[1])
    ndl = dotn.outputs[1]
    lit = g.maprange(ndl, -0.35, 0.4, 0.04, 1.0)
    k = g.math("MULTIPLY", g.math("MULTIPLY", rim, lit), astr)
    em = g.n("ShaderNodeEmission")
    g.set(em.inputs[0], lin(ac))
    g.set(em.inputs[1], k)
    if P.get("aurora"):
        tc = g.n("ShaderNodeTexCoord")
        sep = g.sep(g.vmath("SCALE", g.out(tc, "Object"), scale=1.0 / radius))
        pole = g.maprange(g.math("ABSOLUTE", sep[2]), 0.75, 0.95, 0.0, 1.0)
        wv = g.n("ShaderNodeTexWave", Scale=6.0, Distortion=4.0, Detail=3.0)
        g.set(wv.inputs["Vector"], g.out(tc, "Object"))
        au = g.math("MULTIPLY", g.math("MULTIPLY", pole, g.maprange(g.out(wv, "Fac"), 0.5, 0.9, 0.0, 1.0)), 3.0)
        em2 = g.n("ShaderNodeEmission")
        g.set(em2.inputs[0], g.mix(g.out(wv, "Fac"), lin(P["aurora"][0]), lin(P["aurora"][1])))
        g.set(em2.inputs[1], g.math("MULTIPLY", au, g.math("SUBTRACT", 1.0, g.math("POWER", g.out(lw, "Facing"), 0.5))))
        add0 = g.n("ShaderNodeAddShader")
        g.set(add0.inputs[0], em.outputs[0])
        g.set(add0.inputs[1], em2.outputs[0])
        em = add0
    tr = g.n("ShaderNodeBsdfTransparent")
    add = g.n("ShaderNodeAddShader")
    g.set(add.inputs[0], em.outputs[0])
    g.set(add.inputs[1], tr.outputs[0])
    finish(g, add.outputs[0], m, blended=True)
    ob.data.materials.append(m)
    ob.visible_shadow = False
    return ob


def ring(S, name, radius, location, P, rot):
    import bmesh
    c1, c2 = P["ring"]
    me = bpy.data.meshes.new(name + "_ring")
    bm = bmesh.new()
    seg = 256
    r0, r1 = radius * 1.45, radius * 2.35
    vs = []
    for i in range(seg):
        a = 2 * math.pi * i / seg
        vs.append((bm.verts.new((math.cos(a) * r0, math.sin(a) * r0, 0)), bm.verts.new((math.cos(a) * r1, math.sin(a) * r1, 0))))
    for i in range(seg):
        a0, b0 = vs[i]
        a1, b1 = vs[(i + 1) % seg]
        bm.faces.new((a0, a1, b1, b0))
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name + "_ring", me)
    bpy.context.scene.collection.objects.link(ob)
    ob.location = location
    ob.rotation_euler = (rot[0] + 0.25, rot[1] + 0.1, rot[2])
    m = new_mat(name + "_ring")
    g = G(m.node_tree)
    tc = g.n("ShaderNodeTexCoord")
    r = g.vmath("LENGTH", g.out(tc, "Object"))
    t = g.maprange(r, r0, r1, 0.0, 1.0)
    wave = g.n("ShaderNodeTexWave", Scale=3.0, Detail=4.0, p_bands_direction="X")
    g.set(wave.inputs["Vector"], g.n("ShaderNodeCombineXYZ", t, 0.0, 0.0).outputs[0])
    col = g.mix(g.out(wave, "Fac"), lin(c1), lin(c2))
    alpha = g.math("MULTIPLY", g.maprange(g.out(wave, "Fac"), 0.2, 0.8, 0.25, 0.95),
                   g.math("MULTIPLY", g.maprange(t, 0.0, 0.05, 0.0, 1.0), g.maprange(t, 0.9, 1.0, 1.0, 0.0)))
    gap = g.maprange(g.math("ABSOLUTE", g.math("SUBTRACT", t, 0.62)), 0.0, 0.03, 0.0, 1.0)
    alpha = g.math("MULTIPLY", alpha, gap)
    bsdf = g.n("ShaderNodeBsdfPrincipled", Roughness=0.9)
    g.set(g.sock(bsdf.inputs, "Base Color"), col)
    g.set(g.sock(bsdf.inputs, "Alpha"), alpha)
    finish(g, bsdf.outputs[0], m, blended=True)
    me.materials.append(m)
    return ob


def moon(S, name, radius, location):
    Mo = S["moon"]
    if Mo["kind"] == "lowpoly":
        return _lowpoly_planet(name, radius, (Mo["base"][0], Mo["base"][1], Mo["base"][1], Mo["base"][0]), seed=9)
    ob = _uv_sphere(name, radius, 96, 48)
    ob.location = location
    m = new_mat(name + "_mat")
    g = G(m.node_tree)
    tc = g.n("ShaderNodeTexCoord")
    o = g.vmath("SCALE", g.out(tc, "Object"), scale=1.0 / radius)
    nz = g.n("ShaderNodeTexNoise", Scale=4.0, Detail=10.0)
    g.set(nz.inputs["Vector"], o)
    col = g.mix(g.out(nz, "Fac"), lin(Mo["base"][0]), lin(Mo["base"][1]))
    vor = g.n("ShaderNodeTexVoronoi", Scale=8.0)
    g.set(vor.inputs["Vector"], o)
    crater = g.maprange(g.out(vor, "Distance"), 0.05, 0.3, 0.0, 1.0)
    bsdf = g.n("ShaderNodeBsdfPrincipled", Roughness=0.95)
    g.set(g.sock(bsdf.inputs, "Base Color"), col)
    bump = g.n("ShaderNodeBump", Strength=0.6)
    g.set(bump.inputs["Height"], g.math("ADD", crater, g.math("MULTIPLY", g.out(nz, "Fac"), 0.5)))
    g.set(g.sock(bsdf.inputs, "Normal"), bump.outputs[0])
    if S.get("toon"):
        from ld_mats import toon_shade
        finish(g, toon_shade(g, col, S, bands=(0.3, 0.65, 1.0)), m)
    else:
        finish(g, bsdf.outputs[0], m)
    ob.data.materials.append(m)
    return ob


def asteroids(S, acc_group_name, n, centre, spread, size=(1.0, 6.0), seed=5, flatten=0.35):
    """Scatter rocks as one object."""
    import bmesh
    rnd = random.Random(seed)
    verts, faces = [], []
    for i in range(n):
        p = Vector(centre) + Vector((rnd.uniform(-1, 1) * spread[0], rnd.uniform(-1, 1) * spread[1], rnd.uniform(-1, 1) * spread[2]))
        r = size[0] + (size[1] - size[0]) * rnd.random() ** 3
        v, f = ico(tuple(p), r, 1 if S["shape"] == "lowpoly" else 2, scale=(1.0, rnd.uniform(0.6, 1.3), rnd.uniform(0.5, 1.0)),
                   jitter=0.28, seed=i + seed)
        b = len(verts)
        verts += v
        faces += [tuple(k + b for k in face) for face in f]
    me = bpy.data.meshes.new(acc_group_name)
    me.from_pydata(verts, [], faces)
    smooth = S["shape"] not in ("lowpoly",)
    me.polygons.foreach_set("use_smooth", [smooth] * len(me.polygons))
    ob = bpy.data.objects.new(acc_group_name, me)
    bpy.context.scene.collection.objects.link(ob)
    Mo = S["moon"]
    m = new_mat(acc_group_name + "_mat")
    g = G(m.node_tree)
    tc = g.n("ShaderNodeTexCoord")
    nz = g.n("ShaderNodeTexNoise", Scale=0.6, Detail=8.0)
    g.set(nz.inputs["Vector"], g.out(tc, "Object"))
    col = g.mix(g.out(nz, "Fac"), lin(Mo["base"][0]), lin(Mo["base"][1]))
    col = g.mix(1.0, col, (0.55, 0.55, 0.55, 1.0), "MULTIPLY")
    if S.get("toon"):
        from ld_mats import toon_shade
        finish(g, toon_shade(g, col, S, bands=(0.3, 0.65, 1.0)), m)
    else:
        bsdf = g.n("ShaderNodeBsdfPrincipled", Roughness=0.9)
        g.set(g.sock(bsdf.inputs, "Base Color"), col)
        bump = g.n("ShaderNodeBump", Strength=0.5)
        nz2 = g.n("ShaderNodeTexNoise", Scale=3.0, Detail=8.0)
        g.set(nz2.inputs["Vector"], g.out(tc, "Object"))
        g.set(bump.inputs["Height"], g.out(nz2, "Fac"))
        g.set(g.sock(bsdf.inputs, "Normal"), bump.outputs[0])
        finish(g, bsdf.outputs[0], m)
    me.materials.append(m)
    if S.get("outline"):
        from ld_mats import ink_material
        me.materials.append(bpy.data.materials.get(f"{S['name']}_ink") or ink_material(S))
        mod = ob.modifiers.new("ink", "SOLIDIFY")
        mod.thickness = 0.08
        mod.offset = 1.0
        mod.use_flip_normals = True
        mod.use_rim = False
        mod.material_offset = 1
    return ob
