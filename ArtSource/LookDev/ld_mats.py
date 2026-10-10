"""Materials for the look-development scenes (inside Blender). One material set per direction, keyed by part tag."""

import bpy

from ld_geom import CELL


def hex_rgb(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4))


def lin(h):
    return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in hex_rgb(h)) + (1.0,)


class G:
    """Tiny node-graph helper."""

    def __init__(self, tree):
        self.t = tree
        self.t.nodes.clear()
        self.x = 0

    def n(self, kind, *args, **kw):
        node = self.t.nodes.new(kind)
        node.location = (self.x, 0)
        self.x += 180
        for k, v in kw.items():
            if k.startswith("p_"):
                setattr(node, k[2:], v)
        for i, v in enumerate(args):
            self.set(node.inputs[i], v)
        for k, v in kw.items():
            if not k.startswith("p_"):
                self.set(self.sock(node.inputs, k), v)
        return node

    @staticmethod
    def sock(socks, name):
        for s in socks:
            if s.identifier == name and s.enabled:
                return s
        for s in socks:
            if s.name == name and s.enabled:
                return s
        for s in socks:
            if s.identifier == name or s.name == name:
                return s
        raise KeyError(name)

    def set(self, sock, v):
        if v is None:
            return
        if isinstance(v, bpy.types.NodeSocket):
            self.t.links.new(v, sock)
        elif isinstance(v, bpy.types.Node):
            self.t.links.new(v.outputs[0], sock)
        else:
            if isinstance(v, (tuple, list)) and len(v) == 3 and getattr(sock, "type", "") == "RGBA":
                v = tuple(v) + (1.0,)
            sock.default_value = v

    def out(self, node, name):
        return self.sock(node.outputs, name)

    # shorthands -------------------------------------------------------------------------------------------------
    def math(self, op, a, b=None, clamp=False):
        node = self.n("ShaderNodeMath", p_operation=op, p_use_clamp=clamp)
        self.set(node.inputs[0], a)
        if b is not None:
            self.set(node.inputs[1], b)
        return node.outputs[0]

    def vmath(self, op, a, b=None, scale=None):
        node = self.n("ShaderNodeVectorMath", p_operation=op)
        self.set(node.inputs[0], a)
        if b is not None:
            self.set(node.inputs[1], b)
        if scale is not None:
            self.set(node.inputs[3], scale)
        out = node.outputs[1] if op in ("DOT_PRODUCT", "LENGTH", "DISTANCE") else node.outputs[0]
        return out

    def mix(self, fac, a, b, blend="MIX"):
        node = self.n("ShaderNodeMix", p_data_type="RGBA", p_blend_type=blend)
        self.set(self.sock(node.inputs, "Factor_Float"), fac)
        self.set(self.sock(node.inputs, "A_Color"), a)
        self.set(self.sock(node.inputs, "B_Color"), b)
        return self.sock(node.outputs, "Result_Color")

    def maprange(self, v, a0, a1, b0, b1, interp="LINEAR"):
        node = self.n("ShaderNodeMapRange", p_interpolation_type=interp, p_clamp=True)
        self.set(node.inputs[0], v)
        node.inputs[1].default_value = a0
        node.inputs[2].default_value = a1
        node.inputs[3].default_value = b0
        node.inputs[4].default_value = b1
        return node.outputs[0]

    def ramp(self, v, stops, interp="LINEAR"):
        node = self.n("ShaderNodeValToRGB")
        self.set(node.inputs[0], v)
        cr = node.color_ramp
        cr.interpolation = interp
        while len(cr.elements) > 1:
            cr.elements.remove(cr.elements[-1])
        cr.elements[0].position = stops[0][0]
        cr.elements[0].color = stops[0][1] if len(stops[0][1]) == 4 else tuple(stops[0][1]) + (1,)
        for pos, col in stops[1:]:
            e = cr.elements.new(pos)
            e.color = col if len(col) == 4 else tuple(col) + (1,)
        return node.outputs[0]

    def sep(self, v):
        node = self.n("ShaderNodeSeparateXYZ")
        self.set(node.inputs[0], v)
        return node.outputs


def new_mat(name):
    m = bpy.data.materials.get(name)
    if m:
        bpy.data.materials.remove(m)
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    return m


def finish(g, shader, m, blended=False):
    o = g.n("ShaderNodeOutputMaterial")
    g.set(o.inputs[0], shader)
    if blended:
        m.surface_render_method = "BLENDED"
        m.use_transparency_overlap = False
    return m


# ----------------------------------------------------------------------------------------------------------------------

def toon_shade(g, color, S, bands=(0.42, 0.72, 1.0)):
    diff = g.n("ShaderNodeBsdfDiffuse", Color=(1, 1, 1, 1))
    s2r = g.n("ShaderNodeShaderToRGB")
    g.set(s2r.inputs[0], diff.outputs[0])
    bw = g.n("ShaderNodeRGBToBW")
    g.set(bw.inputs[0], s2r.outputs[0])
    shade = g.ramp(bw.outputs[0], [(0.0, (bands[0],) * 3), (0.08, (bands[1],) * 3), (0.55, (bands[2],) * 3), (0.92, (1.25,) * 3)],
                   "CONSTANT")
    col = g.mix(1.0, color, shade, "MULTIPLY")
    em = g.n("ShaderNodeEmission", Strength=1.0)
    g.set(em.inputs[0], col)
    return em.outputs[0]


def panel_material(S, tag, base_hex):
    m = new_mat(f"{S['name']}_{tag}")
    g = G(m.node_tree)
    tc = g.n("ShaderNodeTexCoord")
    obj = g.out(tc, "Object")
    nrm = g.out(tc, "Normal")
    base = lin(base_hex)
    color = base
    # per-cell variation
    inset = g.vmath("SUBTRACT", obj, g.vmath("SCALE", nrm, scale=0.02))
    cellid = g.vmath("FLOOR", g.vmath("SCALE", inset, scale=1.0 / CELL))
    wn = g.n("ShaderNodeTexWhiteNoise", p_noise_dimensions="3D")
    g.set(wn.inputs["Vector"], cellid)
    var = S.get("variation", 0.0)
    if var:
        k = g.maprange(wn.outputs["Value"], 0, 1, 1 - var, 1 + var * 0.6)
        color = g.mix(1.0, color, g.n("ShaderNodeCombineColor", k, k, k).outputs[0], "MULTIPLY")
    # grime
    grime = S.get("grime", 0.0)
    if grime and tag in ("hull", "hull2", "armor", "trim"):
        nz = g.n("ShaderNodeTexNoise", Scale=1.2, Detail=8.0, Roughness=0.62)
        g.set(nz.inputs["Vector"], obj)
        gm = g.maprange(g.out(nz, "Fac"), 0.45, 0.75, 0.0, grime)
        ao = g.n("ShaderNodeAmbientOcclusion", Distance=0.6, p_samples=8)
        cav = g.maprange(g.out(ao, "AO"), 0.2, 1.0, grime * 1.6, 0.0)
        dirt = g.math("MAXIMUM", gm, cav)
        color = g.mix(dirt, color, (base[0] * 0.35, base[1] * 0.33, base[2] * 0.3, 1))
    # seams
    panel = S.get("panel", 0)
    seam = None
    if panel and S.get("seam_w", 0) > 0:
        size = CELL * panel
        p = g.vmath("SCALE", obj, scale=1.0 / size)
        f = g.vmath("FRACTION", p)
        e = g.vmath("ABSOLUTE", g.vmath("SUBTRACT", f, (0.5, 0.5, 0.5)))
        edge = g.vmath("SUBTRACT", (0.5, 0.5, 0.5), e)
        edge = g.vmath("ADD", edge, g.vmath("SCALE", g.vmath("ABSOLUTE", nrm), scale=10.0))
        xyz = g.sep(edge)
        mn = g.math("MINIMUM", g.math("MINIMUM", xyz[0], xyz[1]), xyz[2])
        w = S["seam_w"] / size
        seam = g.maprange(mn, w * 0.6, w * 1.4, 1.0, 0.0)
        color = g.mix(g.math("MULTIPLY", seam, S.get("seam_dark", 0.5)), color, (0, 0, 0, 1))
    rough = S["rough"].get(tag, 0.5)
    metal = S["metal"].get(tag, 0.0)
    if S.get("toon"):
        shader = toon_shade(g, color, S)
        return finish(g, shader, m)
    bsdf = g.n("ShaderNodeBsdfPrincipled", Roughness=rough, Metallic=metal)
    g.set(g.sock(bsdf.inputs, "Base Color"), color)
    # quilted thermal blanket (ORBITAL)
    if S.get("quilt") and tag == "hull":
        vor = g.n("ShaderNodeTexVoronoi", Scale=7.0, p_feature="SMOOTH_F1")
        g.set(vor.inputs["Vector"], obj)
        nz2 = g.n("ShaderNodeTexNoise", Scale=14.0, Detail=4.0)
        g.set(nz2.inputs["Vector"], obj)
        h = g.math("ADD", g.out(vor, "Distance"), g.math("MULTIPLY", g.out(nz2, "Fac"), 0.25))
        bump = g.n("ShaderNodeBump", Strength=0.35, Distance=0.02)
        g.set(bump.inputs["Height"], h)
        g.set(g.sock(bsdf.inputs, "Normal"), bump.outputs[0])
    if tag == "hull2" and S.get("quilt"):
        # crumpled gold foil
        vor = g.n("ShaderNodeTexVoronoi", Scale=18.0, p_feature="F1")
        g.set(vor.inputs["Vector"], obj)
        bump = g.n("ShaderNodeBump", Strength=0.6, Distance=0.01)
        g.set(bump.inputs["Height"], g.out(vor, "Distance"))
        g.set(g.sock(bsdf.inputs, "Normal"), bump.outputs[0])
    # nacre (REEF)
    if S.get("nacre") and tag in ("hull", "armor"):
        lw = g.n("ShaderNodeLayerWeight", Blend=0.45)
        irid = g.ramp(g.out(lw, "Facing"), [(0.0, lin("#E8F4F0")), (0.35, lin("#9FE8E0")), (0.6, lin("#E8A8D8")),
                                           (0.85, lin("#FFD8A0")), (1.0, lin("#C0F0FF"))])
        g.set(g.sock(bsdf.inputs, "Base Color"), g.mix(0.35, color, irid))
        g.set(g.sock(bsdf.inputs, "Coat Weight"), 0.8)
        # bioluminescent veins
        nz = g.n("ShaderNodeTexNoise", Scale=0.45, Detail=2.0, Distortion=0.5)
        g.set(nz.inputs["Vector"], obj)
        ridge = g.math("ABSOLUTE", g.math("SUBTRACT", g.out(nz, "Fac"), 0.5))
        vein = g.maprange(ridge, 0.003, 0.009, 1.0, 0.0)
        ec, es = S["emit"]["power"]
        g.set(g.sock(bsdf.inputs, "Emission Color"), lin(ec))
        g.set(g.sock(bsdf.inputs, "Emission Strength"), g.math("MULTIPLY", vein, es * 0.6))
    # light-lines in the seams (NOCTURNE)
    seam_emit = S["emit"].get("seam")
    if seam is not None and seam_emit and tag in ("hull", "armor"):
        ec, es = seam_emit
        # only some seams carry light: per-cell hash
        on = g.maprange(wn.outputs["Value"], 0.55, 0.56, 0.0, 1.0)
        g.set(g.sock(bsdf.inputs, "Emission Color"), lin(ec))
        g.set(g.sock(bsdf.inputs, "Emission Strength"), g.math("MULTIPLY", g.math("MULTIPLY", seam, on), es))
    if S.get("flat"):
        # subtle vertical gradient keeps flat colours from looking dead
        sepz = g.sep(obj)
        k = g.maprange(sepz[2], -2.0, 4.0, 0.88, 1.08)
        g.set(g.sock(bsdf.inputs, "Base Color"), g.mix(1.0, color, g.n("ShaderNodeCombineColor", k, k, k).outputs[0], "MULTIPLY"))
    return finish(g, bsdf.outputs[0], m)


def flat_material(S, tag, hex_col, rough=0.5, metal=0.0, emit=None, coat=0.0):
    m = new_mat(f"{S['name']}_{tag}")
    g = G(m.node_tree)
    if S.get("toon") and emit is None:
        shader = toon_shade(g, lin(hex_col), S)
        return finish(g, shader, m)
    bsdf = g.n("ShaderNodeBsdfPrincipled", Roughness=rough, Metallic=metal)
    g.set(g.sock(bsdf.inputs, "Base Color"), lin(hex_col))
    if coat:
        g.set(g.sock(bsdf.inputs, "Coat Weight"), coat)
    if emit:
        ec, es = emit
        g.set(g.sock(bsdf.inputs, "Emission Color"), lin(ec))
        g.set(g.sock(bsdf.inputs, "Emission Strength"), es)
    return finish(g, bsdf.outputs[0], m)


def window_material(S):
    m = new_mat(f"{S['name']}_window")
    g = G(m.node_tree)
    tc = g.n("ShaderNodeTexCoord")
    p = g.vmath("FLOOR", g.vmath("SCALE", g.out(tc, "Object"), scale=4.0))
    wn = g.n("ShaderNodeTexWhiteNoise", p_noise_dimensions="3D")
    g.set(wn.inputs["Vector"], p)
    ec, es = S["emit"]["window"]
    k = g.maprange(wn.outputs["Value"], 0.0, 1.0, 0.15, 1.0)
    k = g.math("MULTIPLY", g.math("MULTIPLY", k, k), es)
    em = g.n("ShaderNodeEmission")
    g.set(em.inputs[0], lin(ec))
    g.set(em.inputs[1], k)
    return finish(g, em.outputs[0], m)


def fade_emit_material(S, tag, hex_col, strength, power=1.5, facing=True):
    """Additive transparent emission faded by the per-vertex 'fade' attribute (plumes, beams, tracers)."""
    m = new_mat(f"{S['name']}_{tag}")
    g = G(m.node_tree)
    attr = g.n("ShaderNodeAttribute", p_attribute_name="fade")
    f = g.math("POWER", g.out(attr, "Fac"), power)
    if facing:
        lw = g.n("ShaderNodeLayerWeight", Blend=0.5)
        edge = g.math("POWER", g.math("SUBTRACT", 1.0, g.out(lw, "Facing")), 2.0)
        f = g.math("MULTIPLY", f, edge)
    em = g.n("ShaderNodeEmission")
    g.set(em.inputs[0], lin(hex_col))
    g.set(em.inputs[1], g.math("MULTIPLY", f, strength))
    tr = g.n("ShaderNodeBsdfTransparent")
    add = g.n("ShaderNodeAddShader")
    g.set(add.inputs[0], em.outputs[0])
    g.set(add.inputs[1], tr.outputs[0])
    return finish(g, add.outputs[0], m, blended=True)


def shield_material(S):
    m = new_mat(f"{S['name']}_shieldfx")
    g = G(m.node_tree)
    lw = g.n("ShaderNodeLayerWeight", Blend=0.3)
    rim = g.math("POWER", g.out(lw, "Facing"), 3.0)
    tc = g.n("ShaderNodeTexCoord")
    vor = g.n("ShaderNodeTexVoronoi", Scale=3.0, p_feature="DISTANCE_TO_EDGE")
    g.set(vor.inputs["Vector"], g.out(tc, "Object"))
    hexl = g.maprange(g.out(vor, "Distance"), 0.0, 0.04, 1.0, 0.0)
    ec, es = S["emit"]["shield"]
    k = g.math("ADD", g.math("MULTIPLY", rim, es * 0.5), g.math("MULTIPLY", hexl, es * 0.12))
    em = g.n("ShaderNodeEmission")
    g.set(em.inputs[0], lin(ec))
    g.set(em.inputs[1], k)
    tr = g.n("ShaderNodeBsdfTransparent")
    add = g.n("ShaderNodeAddShader")
    g.set(add.inputs[0], em.outputs[0])
    g.set(add.inputs[1], tr.outputs[0])
    return finish(g, add.outputs[0], m, blended=True)


def ink_material(S):
    col, _ = S["outline"]
    m = new_mat(f"{S['name']}_ink")
    g = G(m.node_tree)
    em = g.n("ShaderNodeEmission", Strength=1.0)
    g.set(em.inputs[0], lin(col))
    finish(g, em.outputs[0], m)
    m.use_backface_culling = True
    return m


def interior_material(S):
    m = new_mat(f"{S['name']}_interior")
    g = G(m.node_tree)
    tc = g.n("ShaderNodeTexCoord")
    attr = g.n("ShaderNodeAttribute", p_attribute_name="fade")
    wave = g.n("ShaderNodeTexWave", Scale=4.0, Distortion=2.0, Detail=3.0)
    g.set(wave.inputs["Vector"], g.out(tc, "Object"))
    girder = g.maprange(g.out(wave, "Fac"), 0.4, 0.6, 0.6, 1.2)
    base = lin(S["col"]["interior"])
    col = g.mix(1.0, base, g.n("ShaderNodeCombineColor", girder, girder, girder).outputs[0], "MULTIPLY")
    bsdf = g.n("ShaderNodeBsdfPrincipled", Roughness=0.6, Metallic=0.5)
    g.set(g.sock(bsdf.inputs, "Base Color"), col)
    ec, es = S["emit"]["ember"]
    nz = g.n("ShaderNodeTexNoise", Scale=6.0, Detail=4.0)
    g.set(nz.inputs["Vector"], g.out(tc, "Object"))
    hot = g.math("MULTIPLY", g.out(attr, "Fac"), g.maprange(g.out(nz, "Fac"), 0.4, 0.7, 0.2, 1.0))
    g.set(g.sock(bsdf.inputs, "Emission Color"), lin(ec))
    g.set(g.sock(bsdf.inputs, "Emission Strength"), g.math("MULTIPLY", g.math("POWER", hot, 2.0), es))
    return finish(g, bsdf.outputs[0], m)


def solar_material(S):
    m = new_mat(f"{S['name']}_solar")
    g = G(m.node_tree)
    tc = g.n("ShaderNodeTexCoord")
    p = g.vmath("SCALE", g.out(tc, "Object"), scale=8.0)
    f = g.vmath("FRACTION", p)
    e = g.vmath("ABSOLUTE", g.vmath("SUBTRACT", f, (0.5, 0.5, 0.5)))
    xyz = g.sep(e)
    grid = g.maprange(g.math("MAXIMUM", g.math("MAXIMUM", xyz[0], xyz[1]), xyz[2]), 0.44, 0.47, 0.0, 1.0)
    col = g.mix(grid, lin(S["col"].get("solar", "#141B2B")), lin("#C8CCD4"))
    bsdf = g.n("ShaderNodeBsdfPrincipled", Roughness=0.12, Metallic=0.2)
    g.set(g.sock(bsdf.inputs, "Base Color"), col)
    g.set(g.sock(bsdf.inputs, "Coat Weight"), 1.0)
    return finish(g, bsdf.outputs[0], m)


def make_materials(S):
    c = S["col"]
    E = S["emit"]
    mats = {}
    for tag in ("hull", "hull2", "armor", "trim"):
        mats[tag] = panel_material(S, tag, c[tag])
    mats["dark"] = flat_material(S, "dark", c["dark"], 0.7, 0.3)
    mats["metal"] = flat_material(S, "metal", c["metal"], S["rough"]["metal"], S["metal"]["metal"])
    mats["accent"] = flat_material(S, "accent", c["accent"], 0.5, 0.0)
    mats["foil"] = panel_material(S, "foil", c["foil"]) if S.get("quilt") else flat_material(S, "foil", c["foil"], 0.3, 0.9)
    if S.get("quilt"):
        mats["foil"] = mats["hull2"]
    mats["glass"] = flat_material(S, "glass", c["glass"], 0.05, 0.0, emit=(c["glass"], 0.15), coat=1.0)
    mats["window"] = window_material(S)
    for key in ("thruster", "weapon", "core", "shield", "power"):
        mats["emit_" + key[0]] = flat_material(S, "emit_" + key, E[key][0], 0.3, 0.0, emit=E[key])
    for key in ("nav_r", "nav_g", "nav_w"):
        mats[key] = flat_material(S, key, E[key][0], 0.3, 0.0, emit=E[key])
    mats["ember"] = flat_material(S, "ember", E["ember"][0], 0.5, 0.0, emit=E["ember"])
    mats["interior"] = interior_material(S)
    mats["plume"] = fade_emit_material(S, "plume", E["thruster"][0], E["thruster"][1] * 1.4, 1.4)
    mats["beam"] = fade_emit_material(S, "beam", E["weapon"][0], E["weapon"][1] * 4, 0.7, facing=False)
    mats["tracer"] = fade_emit_material(S, "tracer", E["weapon"][0], E["weapon"][1] * 3, 1.0, facing=False)
    mats["shieldfx"] = shield_material(S)
    mats["solar"] = solar_material(S)
    mats["grid"] = flat_material(S, "grid", E["shield"][0], 0.5, 0.0, emit=(E["shield"][0], 1.5))
    if S.get("outline"):
        mats["ink"] = ink_material(S)
    return mats
