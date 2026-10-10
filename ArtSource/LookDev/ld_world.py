"""Space environment per direction: sky, lights, planets, atmospheres, rings, moons, asteroids, haze (inside Blender)."""

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


def _sun_node(g, sundir):
    """A vector node carrying the sun direction; its input is updated per shot."""
    n = g.n("ShaderNodeVectorMath", p_operation="SCALE")
    n.inputs[0].default_value = tuple(sundir)
    n.inputs[3].default_value = 1.0
    SUN_SOCKETS.append(n.inputs[0])
    return n.outputs[0]


# ----------------------------------------------------------------------------------------------------------------------
# Sky
# ----------------------------------------------------------------------------------------------------------------------

def _noise(g, v, scale, detail=6.0, rough=0.55, dist=0.0):
    nz = g.n("ShaderNodeTexNoise", Scale=scale, Detail=detail, Roughness=rough, Distortion=dist)
    g.set(nz.inputs["Vector"], v)
    return g.out(nz, "Fac")


def setup_world(S, scene):
    W = S["world"]
    world = bpy.data.worlds.get("LD") or bpy.data.worlds.new("LD")
    scene.world = world
    world.use_nodes = True
    g = G(world.node_tree)
    tc = g.n("ShaderNodeTexCoord")
    dn = g.vmath("NORMALIZE", g.out(tc, "Generated"))
    kind = W["kind"]
    bg = lin(W["bg"])
    if kind == "deep":
        # dark, slightly warm below and cool above; a galactic band with dust lanes; soft nebula clouds
        t = g.math("ADD", g.math("MULTIPLY", g.vmath("DOT_PRODUCT", dn, (0.2, -0.35, 0.92)), 0.5), 0.5)
        sky = g.ramp(t, [(0.0, lin("#0A0705")), (0.5, bg), (1.0, lin("#04060C"))])
        b1, b2, bk = W["band"]
        ax = Vector((0.35, 0.25, 0.9)).normalized()
        bd = g.math("ABSOLUTE", g.vmath("DOT_PRODUCT", dn, tuple(ax)))
        band = g.maprange(bd, 0.0, 0.32, 1.0, 0.0, "SMOOTHSTEP")
        bn = _noise(g, dn, 3.0, 10.0, 0.62, 0.3)
        lanes = g.maprange(_noise(g, dn, 7.0, 8.0, 0.6, 0.6), 0.45, 0.62, 1.0, 0.0)
        bandc = g.mix(bn, lin(b1), lin(b2))
        bandk = g.math("MULTIPLY", g.math("MULTIPLY", band, g.maprange(bn, 0.3, 0.75, 0.15, 1.0)), lanes)
        sky = g.mix(g.math("MULTIPLY", bandk, bk), sky, bandc, "ADD")
        n1, n2, nk = W["nebula"]
        neb = _noise(g, dn, 1.1, 10.0, 0.6, 0.4)
        mask = g.maprange(_noise(g, dn, 0.6, 2.0), 0.45, 0.72, 0.0, 1.0)
        dens = g.math("MULTIPLY", g.maprange(neb, 0.45, 0.85, 0.0, 1.0), mask)
        sky = g.mix(g.math("MULTIPLY", dens, nk), sky, g.mix(_noise(g, dn, 2.0), lin(n1), lin(n2)), "ADD")
        dc, dk = W["dust"]
        dust = g.maprange(_noise(g, dn, 2.6, 8.0, 0.6, 0.5), 0.5, 0.7, 0.0, dk)
        sky = g.mix(dust, sky, lin(dc))
        star_k = g.math("SUBTRACT", 1.0, g.math("MULTIPLY", dust, 0.8))
    elif kind == "storm":
        # oppressive: smoke and dark dust silhouetted against a dim sickly glow, few stars
        t = g.math("ADD", g.math("MULTIPLY", g.vmath("DOT_PRODUCT", dn, (-0.4, 0.3, 0.85)), 0.5), 0.5)
        sky = g.ramp(t, [(0.0, lin("#020302")), (0.6, bg), (1.0, lin("#050706"))])
        gc, gk = W["glow"]
        glow_dir = Vector((0.6, 0.7, -0.1)).normalized()
        gl = g.maprange(g.vmath("DOT_PRODUCT", dn, tuple(glow_dir)), 0.2, 1.0, 0.0, 1.0, "SMOOTHSTEP")
        smoke = _noise(g, dn, 1.4, 12.0, 0.66, 0.8)
        smoke2 = _noise(g, dn, 3.5, 10.0, 0.6, 1.2)
        n1, n2, nk = W["nebula"]
        neb = g.math("MULTIPLY", g.maprange(smoke, 0.4, 0.75, 0.0, 1.0), g.maprange(smoke2, 0.3, 0.8, 0.3, 1.0))
        sky = g.mix(g.math("MULTIPLY", neb, nk * 0.35), sky, g.mix(smoke2, lin(n1), lin(n2)), "ADD")
        sky = g.mix(g.math("MULTIPLY", g.math("MULTIPLY", gl, gk), g.maprange(smoke, 0.3, 0.7, 0.2, 1.0)), sky, lin(gc), "ADD")
        # filaments of brighter smoke edge
        ridge = g.math("ABSOLUTE", g.math("SUBTRACT", _noise(g, dn, 2.2, 6.0, 0.6, 0.7), 0.5))
        fil = g.math("MULTIPLY", g.maprange(ridge, 0.0, 0.025, 1.0, 0.0), g.maprange(smoke, 0.45, 0.7, 0.0, 1.0))
        sky = g.mix(g.math("MULTIPLY", fil, 0.12), sky, lin("#9AB090"), "ADD")
        dc, dk = W["dust"]
        dust = g.maprange(_noise(g, dn, 1.8, 10.0, 0.62, 0.6), 0.48, 0.66, 0.0, dk)
        sky = g.mix(dust, sky, lin(dc))
        star_k = g.math("SUBTRACT", 1.0, g.math("MULTIPLY", dust, 0.95))
    else:  # opera
        cols = [lin(c) for c in W["sky"]]
        t = g.math("ADD", g.math("MULTIPLY", g.vmath("DOT_PRODUCT", dn, (0.15, -0.3, 0.94)), 0.5), 0.5)
        t = g.math("ADD", t, g.math("MULTIPLY", g.math("SUBTRACT", _noise(g, dn, 1.5, 3.0), 0.5), 0.18))
        sky = g.ramp(t, [(0.0, cols[3]), (0.3, cols[2]), (0.52, cols[1]), (0.8, cols[0])], "EASE")
        n1, n2, nk = W["nebula"]
        # painted nebula: large soft glowing clouds with brighter cores, two hues, dark dust lanes across them
        neb = _noise(g, dn, 0.9, 8.0, 0.58, 0.15)
        mask = g.maprange(_noise(g, dn, 0.55, 2.0), 0.38, 0.68, 0.0, 1.0)
        dens = g.math("MULTIPLY", g.math("POWER", g.maprange(neb, 0.4, 0.82, 0.0, 1.0), 1.6), mask)
        nc = g.mix(g.maprange(_noise(g, dn, 1.2, 3.0), 0.35, 0.65, 0.0, 1.0), lin(n1), lin(n2))
        sky = g.mix(g.math("MULTIPLY", dens, nk), sky, nc, "ADD")
        core = g.math("POWER", dens, 3.0)
        sky = g.mix(g.math("MULTIPLY", core, 0.35), sky, lin("#FFE2C8"), "ADD")
        dk = g.maprange(_noise(g, dn, 2.6, 8.0, 0.6, 0.4), 0.55, 0.68, 0.0, 0.75)
        sky = g.mix(g.math("MULTIPLY", dk, mask), sky, lin("#05040E"))
        star_k = g.math("ADD", 1.0, g.math("MULTIPLY", mask, W.get("clusters", 0.0) * 1.5))
    # stars, three layers, tinted per star
    stars_total = None
    for scale, thresh, bright in ((180.0, 0.88, 6.0), (420.0, 0.6, 1.6), (900.0, 0.3, 0.6)):
        vor = g.n("ShaderNodeTexVoronoi", Scale=scale, p_feature="F1")
        g.set(vor.inputs["Vector"], dn)
        sel = g.n("ShaderNodeSeparateColor")
        g.set(sel.inputs[0], g.out(vor, "Color"))
        on = g.maprange(sel.outputs[0], thresh, thresh + 0.01, 0.0, 1.0)
        pt = g.math("MULTIPLY", g.maprange(g.out(vor, "Distance"), 0.0, 0.06 if scale < 300 else 0.08, 1.0, 0.0), on)
        pt = g.math("MULTIPLY", g.math("POWER", pt, 3.0), bright * W.get("stars", 1.0))
        stars_total = pt if stars_total is None else g.math("ADD", stars_total, pt)
    stars_total = g.math("MULTIPLY", stars_total, star_k)
    tint = g.mix(_noise(g, dn, 60.0, 1.0), lin("#AFC8FF"), lin("#FFD8B0"))
    sky = g.mix(stars_total, sky, tint, "ADD")
    if kind == "opera":
        vor = g.n("ShaderNodeTexVoronoi", Scale=30.0, p_feature="F1")
        g.set(vor.inputs["Vector"], dn)
        sel = g.n("ShaderNodeSeparateColor")
        g.set(sel.inputs[0], g.out(vor, "Color"))
        on = g.maprange(sel.outputs[0], 0.86, 0.87, 0.0, 1.0)
        soft = g.math("MULTIPLY", g.math("POWER", g.maprange(g.out(vor, "Distance"), 0.0, 0.1, 1.0, 0.0), 3.0), on)
        sky = g.mix(g.math("MULTIPLY", soft, 2.0), sky, lin("#FFF0F8"), "ADD")
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


def haze(S, scene, centre=(0, 0, 0), size=400.0):
    """A local box of thin fog so work lights throw visible beams (NOCTURNE)."""
    H = S.get("haze")
    if not H:
        return None
    me = bpy.data.meshes.new("haze")
    h = size / 2
    from ld_geom import box
    v, f = box((-h, -h, -h), (h, h, h))
    me.from_pydata(v, [], f)
    ob = bpy.data.objects.new("haze", me)
    scene.collection.objects.link(ob)
    ob.location = centre
    m = new_mat("haze")
    g = G(m.node_tree)
    tc = g.n("ShaderNodeTexCoord")
    nz = g.n("ShaderNodeTexNoise", Scale=0.02, Detail=4.0)
    g.set(nz.inputs["Vector"], g.out(tc, "Object"))
    vol = g.n("ShaderNodeVolumePrincipled")
    g.set(g.sock(vol.inputs, "Color"), lin(H["color"]))
    g.set(g.sock(vol.inputs, "Density"), g.math("MULTIPLY", g.maprange(g.out(nz, "Fac"), 0.3, 0.7, 0.4, 1.6), H["density"]))
    g.set(g.sock(vol.inputs, "Anisotropy"), H.get("anisotropy", 0.5))
    o = g.n("ShaderNodeOutputMaterial")
    g.set(o.inputs["Volume"], vol.outputs[0])
    me.materials.append(m)
    scene.eevee.volumetric_end = size * 1.2
    try:
        scene.eevee.volumetric_tile_size = "4"
    except Exception:
        pass
    return ob


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


def planet(S, name, radius, location, P, sundir, rot=(0.3, 0.0, 0.0)):
    kind = P["kind"]
    ob = _uv_sphere(name, radius)
    m = new_mat(name + "_surface")
    g = G(m.node_tree)
    tc = g.n("ShaderNodeTexCoord")
    o = g.vmath("SCALE", g.out(tc, "Object"), scale=1.0 / radius)
    geo = g.n("ShaderNodeNewGeometry")
    sd = _sun_node(g, sundir)
    ndl = g.vmath("DOT_PRODUCT", g.out(geo, "Normal"), sd)
    night = g.maprange(ndl, -0.05, 0.12, 1.0, 0.0)
    cols = [lin(c) for c in P["base"]]
    emis = None
    sep = g.sep(o)
    if kind == "giant":
        warp = _noise(g, o, 2.5, 6.0, 0.55, 0.6)
        lat = g.math("ADD", sep[2], g.math("MULTIPLY", g.math("SUBTRACT", warp, 0.5), 0.22))
        # great storm: a vortex patch
        spot_c = Vector((0.7, -0.55, -0.35)).normalized()
        sp = g.vmath("DISTANCE", o, tuple(spot_c))
        spot = g.maprange(sp, 0.08, 0.2, 1.0, 0.0, "SMOOTHSTEP")
        lat = g.math("ADD", lat, g.math("MULTIPLY", spot, g.math("MULTIPLY", g.math("SUBTRACT", _noise(g, o, 18.0, 4.0), 0.5), 0.3)))
        t = g.math("ADD", g.math("MULTIPLY", lat, 0.5), 0.5)
        col = g.ramp(t, [(0.0, cols[2]), (0.1, cols[0]), (0.18, cols[1]), (0.27, cols[3]), (0.36, cols[1]), (0.44, cols[0]),
                         (0.5, cols[1]), (0.56, cols[2]), (0.64, cols[1]), (0.72, cols[3]), (0.8, cols[0]), (0.9, cols[1]), (1.0, cols[2])])
        fine = _noise(g, g.vmath("MULTIPLY", o, (1.0, 1.0, 8.0)), 6.0, 8.0, 0.6, 0.4)
        k = g.maprange(fine, 0.3, 0.7, 0.88, 1.1)
        col = g.mix(1.0, col, g.n("ShaderNodeCombineColor", k, k, k).outputs[0], "MULTIPLY")
        col = g.mix(g.math("MULTIPLY", spot, 0.7), col, cols[2])
    elif kind == "storm":
        # banded, turbulent, desaturated; storm cells; lightning on the night side
        warp = _noise(g, o, 3.0, 10.0, 0.62, 1.6)
        lat = g.math("ADD", sep[2], g.math("MULTIPLY", g.math("SUBTRACT", warp, 0.5), 0.5))
        t = g.math("ADD", g.math("MULTIPLY", lat, 0.5), 0.5)
        col = g.ramp(t, [(0.0, cols[3]), (0.15, cols[0]), (0.3, cols[1]), (0.42, cols[2]), (0.5, cols[1]), (0.6, cols[0]),
                         (0.72, cols[2]), (0.85, cols[1]), (1.0, cols[3])])
        cl = _noise(g, o, 6.0, 12.0, 0.65, 1.0)
        cc, ca = P["cloud"]
        col = g.mix(g.maprange(cl, 0.5, 0.75, 0.0, ca), col, lin(cc))
        vor = g.n("ShaderNodeTexVoronoi", Scale=22.0, p_feature="F1")
        g.set(vor.inputs["Vector"], o)
        sel = g.n("ShaderNodeSeparateColor")
        g.set(sel.inputs[0], g.out(vor, "Color"))
        flash = g.math("MULTIPLY", g.maprange(sel.outputs[0], 0.93, 0.94, 0.0, 1.0),
                       g.math("POWER", g.maprange(g.out(vor, "Distance"), 0.0, 0.35, 1.0, 0.0), 2.0))
        lc, ls = P["lights"]
        emis = (lin(lc), g.math("MULTIPLY", g.math("MULTIPLY", flash, night), ls))
    else:
        h = _noise(g, o, 2.2, 12.0, 0.58)
        col = g.ramp(h, [(0.0, cols[3]), (0.35, cols[0]), (0.55, cols[1]), (0.75, cols[2]), (1.0, lin("#F0D0A0"))])
        land = g.maprange(h, 0.3, 0.4, 0.0, 1.0)
        # crater fields: dark floors with bright rims
        cv = g.n("ShaderNodeTexVoronoi", Scale=11.0, p_feature="F1")
        cn = g.n("ShaderNodeTexNoise", Scale=4.0)
        g.set(cn.inputs["Vector"], o)
        g.set(cv.inputs["Vector"], g.vmath("ADD", o, g.vmath("SCALE", g.out(cn, "Color"), scale=0.04)))
        cd = g.out(cv, "Distance")
        csel = g.n("ShaderNodeSeparateColor")
        g.set(csel.inputs[0], g.out(cv, "Color"))
        on = g.maprange(csel.outputs[0], 0.72, 0.73, 0.0, 1.0)
        col = g.mix(g.math("MULTIPLY", g.maprange(cd, 0.0, 0.18, 0.28, 0.0), on), col, cols[3])
        rim = g.math("MULTIPLY", g.maprange(cd, 0.15, 0.2, 0.0, 1.0), g.maprange(cd, 0.2, 0.3, 1.0, 0.0))
        col = g.mix(g.math("MULTIPLY", rim, g.math("MULTIPLY", on, 0.18)), col, lin("#E8C08A"))
        if P.get("lights"):
            lc, ls = P["lights"]
            city = g.maprange(_noise(g, o, 40.0, 6.0), 0.58, 0.72, 0.0, 1.0)
            city = g.math("MULTIPLY", city, land)
            emis = (lin(lc), g.math("MULTIPLY", g.math("MULTIPLY", city, night), ls))
        if P.get("cloud"):
            cc, ca = P["cloud"]
            cl = g.n("ShaderNodeTexWave", Scale=1.5, Distortion=8.0, Detail=6.0, p_wave_type="BANDS", p_bands_direction="Z")
            g.set(cl.inputs["Vector"], o)
            col = g.mix(g.maprange(g.out(cl, "Fac"), 0.75, 0.95, 0.0, ca), col, lin(cc))
    lam = g.math("POWER", g.maprange(ndl, -0.08, 0.9, 0.0, 1.0), 0.8)
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
        ring(S, name, radius, location, P, rot, sundir)
    return ob


def atmo(S, name, radius, location, P, sundir):
    """Scattering glow that hugs the limb and fades outward, brightest on the day side, with forward scatter when the
    sun is behind the planet. Computed from the ray's closest approach to the planet centre (impact parameter)."""
    ac, ac2, astr = P["atmo"]
    Ra = radius * 1.075
    ob = _uv_sphere(name + "_atmo", Ra, 160, 80)
    ob.location = location
    m = new_mat(name + "_atmo")
    g = G(m.node_tree)
    tc = g.n("ShaderNodeTexCoord")
    O = g.out(tc, "Object")
    geo = g.n("ShaderNodeNewGeometry")
    I = g.vmath("NORMALIZE", g.out(geo, "Incoming"))
    b = g.vmath("LENGTH", g.vmath("CROSS_PRODUCT", O, I))
    h = g.maprange(b, radius, Ra, 0.0, 1.0)
    out_glow = g.math("POWER", g.math("SUBTRACT", 1.0, h), 3.0)
    in_glow = g.math("ADD", g.math("MULTIPLY", g.math("POWER", g.math("DIVIDE", b, radius), 14.0), 0.35), 0.01)
    is_out = g.math("GREATER_THAN", b, radius)
    glow = g.math("ADD", g.math("MULTIPLY", is_out, out_glow), g.math("MULTIPLY", g.math("SUBTRACT", 1.0, is_out), in_glow))
    sd = _sun_node(g, sundir)
    # closest-approach point: O - I * dot(O, I)
    oi = g.vmath("DOT_PRODUCT", O, I)
    sc = g.n("ShaderNodeVectorMath", p_operation="SCALE")
    g.set(sc.inputs[0], I)
    g.set(sc.inputs[3], oi)
    pc = g.vmath("NORMALIZE", g.vmath("SUBTRACT", O, sc.outputs[0]))
    L = g.vmath("DOT_PRODUCT", pc, sd)
    day = g.maprange(L, -0.28, 0.3, 0.0, 1.0, "SMOOTHSTEP")
    fwd = g.math("MULTIPLY", g.math("POWER", g.math("MAXIMUM", g.math("MULTIPLY", g.vmath("DOT_PRODUCT", I, sd), -1.0), 0.0), 8.0), 1.6)
    k = g.math("MULTIPLY", g.math("MULTIPLY", glow, g.math("ADD", day, g.math("MULTIPLY", fwd, is_out))), astr * 3.0)
    col = g.ramp(h, [(0.0, lin(ac2)), (0.35, lin(ac)), (1.0, lin(ac))])
    sunset = g.maprange(g.math("ABSOLUTE", L), 0.0, 0.18, 1.0, 0.0)
    col = g.mix(g.math("MULTIPLY", sunset, 0.55), col, lin("#FF7A3A"))
    em = g.n("ShaderNodeEmission")
    g.set(em.inputs[0], col)
    g.set(em.inputs[1], k)
    tr = g.n("ShaderNodeBsdfTransparent")
    add = g.n("ShaderNodeAddShader")
    g.set(add.inputs[0], em.outputs[0])
    g.set(add.inputs[1], tr.outputs[0])
    finish(g, add.outputs[0], m, blended=True)
    m.use_backface_culling = True
    ob.data.materials.append(m)
    ob.visible_shadow = False
    return ob


def ring(S, name, radius, location, P, rot, sundir):
    import bmesh
    c1, c2, c3 = P["ring"]
    me = bpy.data.meshes.new(name + "_ring")
    bm = bmesh.new()
    seg = 384
    r0, r1 = radius * 1.35, radius * 2.4
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
    ob.rotation_euler = (rot[0] + 0.32, rot[1] + 0.12, rot[2])
    m = new_mat(name + "_ring")
    g = G(m.node_tree)
    tc = g.n("ShaderNodeTexCoord")
    po = g.out(tc, "Object")
    r = g.vmath("LENGTH", po)
    t = g.maprange(r, r0, r1, 0.0, 1.0)
    tv = g.n("ShaderNodeCombineXYZ", t, 0.0, 0.0).outputs[0]
    w1 = g.n("ShaderNodeTexWave", Scale=5.0, Detail=8.0, Distortion=1.5, p_bands_direction="X")
    g.set(w1.inputs["Vector"], tv)
    w2 = g.n("ShaderNodeTexNoise", Scale=180.0, Detail=6.0)
    g.set(w2.inputs["Vector"], tv)
    w3 = g.n("ShaderNodeTexNoise", Scale=22.0, Detail=3.0)
    g.set(w3.inputs["Vector"], tv)
    dens = g.math("MULTIPLY", g.maprange(g.out(w1, "Fac"), 0.1, 0.9, 0.3, 1.0), g.maprange(g.out(w2, "Fac"), 0.25, 0.75, 0.6, 1.0))
    dens = g.math("MULTIPLY", g.math("MULTIPLY", dens, g.maprange(g.out(w3, "Fac"), 0.3, 0.7, 0.35, 1.0)), 0.62)
    col = g.ramp(t, [(0.0, lin(c3)), (0.25, lin(c2)), (0.5, lin(c1)), (0.75, lin(c2)), (1.0, lin(c1))])
    col = g.mix(g.out(w2, "Fac"), col, lin(c1))
    edge = g.math("MULTIPLY", g.maprange(t, 0.0, 0.06, 0.0, 1.0), g.maprange(t, 0.9, 1.0, 1.0, 0.0))
    gaps = g.math("MULTIPLY", g.maprange(g.math("ABSOLUTE", g.math("SUBTRACT", t, 0.6)), 0.0, 0.025, 0.05, 1.0),
                  g.maprange(g.math("ABSOLUTE", g.math("SUBTRACT", t, 0.84)), 0.0, 0.01, 0.2, 1.0))
    alpha = g.math("MULTIPLY", g.math("MULTIPLY", dens, edge), gaps)
    # lighting: sun on either face, and the planet's shadow across the ring
    vt = g.n("ShaderNodeVectorTransform", p_vector_type="VECTOR", p_convert_from="OBJECT", p_convert_to="WORLD")
    g.set(vt.inputs[0], po)
    pw = vt.outputs[0]
    sd = _sun_node(g, sundir)
    along = g.vmath("DOT_PRODUCT", pw, sd)
    sc = g.n("ShaderNodeVectorMath", p_operation="SCALE")
    g.set(sc.inputs[0], sd)
    g.set(sc.inputs[3], along)
    perp = g.vmath("LENGTH", g.vmath("SUBTRACT", pw, sc.outputs[0]))
    shadow = g.math("MULTIPLY", g.math("LESS_THAN", along, 0.0), g.maprange(perp, radius * 0.97, radius * 1.03, 1.0, 0.0))
    geo = g.n("ShaderNodeNewGeometry")
    ndl = g.math("ABSOLUTE", g.vmath("DOT_PRODUCT", g.out(geo, "Normal"), sd))
    light = g.math("MULTIPLY", g.math("ADD", 0.25, g.math("MULTIPLY", ndl, 1.1)), g.math("SUBTRACT", 1.0, g.math("MULTIPLY", shadow, 0.93)))
    lit = g.mix(1.0, col, g.n("ShaderNodeCombineColor", light, light, light).outputs[0], "MULTIPLY")
    em = g.n("ShaderNodeEmission", Strength=1.2)
    g.set(em.inputs[0], lit)
    tr = g.n("ShaderNodeBsdfTransparent")
    mixs = g.n("ShaderNodeMixShader")
    g.set(mixs.inputs[0], alpha)
    g.set(mixs.inputs[1], tr.outputs[0])
    g.set(mixs.inputs[2], em.outputs[0])
    finish(g, mixs.outputs[0], m, blended=True)
    me.materials.append(m)
    ob.visible_shadow = False
    return ob


def moon(S, name, radius, location):
    Mo = S["moon"]
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
    finish(g, bsdf.outputs[0], m)
    ob.data.materials.append(m)
    return ob


def asteroids(S, acc_group_name, n, centre, spread, size=(1.0, 6.0), seed=5, flatten=0.35):
    """Scatter rocks as one object."""
    rnd = random.Random(seed)
    verts, faces = [], []
    for i in range(n):
        p = Vector(centre) + Vector((rnd.uniform(-1, 1) * spread[0], rnd.uniform(-1, 1) * spread[1], rnd.uniform(-1, 1) * spread[2]))
        r = size[0] + (size[1] - size[0]) * rnd.random() ** 3
        v, f = ico(tuple(p), r, 2, scale=(1.0, rnd.uniform(0.6, 1.3), rnd.uniform(0.5, 1.0)), jitter=0.28, seed=i + seed)
        b = len(verts)
        verts += v
        faces += [tuple(k + b for k in face) for face in f]
    me = bpy.data.meshes.new(acc_group_name)
    me.from_pydata(verts, [], faces)
    me.polygons.foreach_set("use_smooth", [True] * len(me.polygons))
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
    bsdf = g.n("ShaderNodeBsdfPrincipled", Roughness=0.9)
    g.set(g.sock(bsdf.inputs, "Base Color"), col)
    bump = g.n("ShaderNodeBump", Strength=0.5)
    nz2 = g.n("ShaderNodeTexNoise", Scale=3.0, Detail=8.0)
    g.set(nz2.inputs["Vector"], g.out(tc, "Object"))
    g.set(bump.inputs["Height"], g.out(nz2, "Fac"))
    g.set(g.sock(bsdf.inputs, "Normal"), bump.outputs[0])
    finish(g, bsdf.outputs[0], m)
    me.materials.append(m)
    return ob
