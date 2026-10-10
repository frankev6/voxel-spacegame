"""Turns an Acc into Blender objects with per-direction modifiers (bevel, subdivision, ink outline)."""

import math

import bmesh
import bpy

HARD = {"hull", "hull2", "armor", "trim", "dark", "metal", "accent", "foil", "interior", "solar", "glass"}
NO_OUTLINE = {"plume", "beam", "tracer", "shieldfx", "window", "nav_r", "nav_g", "nav_w", "grid", "ember"}


def realize(acc, mats, S, parent_name, collection=None, voxel_groups=(), subdiv_levels=None):
    """Create one object per (group, tag) under an empty per group. Returns {group: empty}."""
    col = collection or bpy.context.scene.collection
    empties = {}
    for group in acc.groups():
        name = f"{parent_name}:{group}"
        e = bpy.data.objects.new(name, None)
        col.objects.link(e)
        empties[group] = e
    for (group, tag), bucket in acc.parts.items():
        verts, faces, smooth = bucket[0], bucket[1], bucket[2]
        if not faces:
            continue
        me = bpy.data.meshes.new(f"{parent_name}:{group}:{tag}")
        me.from_pydata(verts, [], faces)
        me.validate(clean_customdata=False)
        if len(bucket) == 4:
            attr = me.attributes.new("fade", "FLOAT", "POINT")
            attr.data.foreach_set("value", bucket[3][:len(me.vertices)])
        merge = tag in HARD
        if merge:
            bm = bmesh.new()
            bm.from_mesh(me)
            bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4)
            bm.to_mesh(me)
            bm.free()
        flags = smooth[:len(me.polygons)] if len(smooth) >= len(me.polygons) else [False] * len(me.polygons)
        if S.get("flat"):
            flags = [False] * len(me.polygons)
        if S.get("subdiv") and tag in HARD:
            flags = [True] * len(me.polygons)
        me.polygons.foreach_set("use_smooth", flags)
        try:
            me.set_sharp_from_angle(angle=math.radians(40))
        except Exception:
            pass
        ob = bpy.data.objects.new(me.name, me)
        col.objects.link(ob)
        ob.parent = empties[group]
        mat = mats.get(tag) or mats["hull"]
        me.materials.append(mat)
        # modifiers ---------------------------------------------------------------------------------------------
        if S.get("subdiv") and tag in HARD:
            if tag in ("hull", "hull2", "armor"):
                sm = ob.modifiers.new("relax", "SMOOTH")
                sm.factor = 0.7
                sm.iterations = 8
            lv = S["subdiv"] if subdiv_levels is None else subdiv_levels
            m = ob.modifiers.new("subd", "SUBSURF")
            m.levels = lv
            m.render_levels = lv
        elif S.get("bevel") and tag in HARD and tag not in ("glass",):
            m = ob.modifiers.new("bevel", "BEVEL")
            m.width = S["bevel"]
            m.segments = S.get("bevel_seg", 2)
            m.limit_method = "ANGLE"
            m.angle_limit = math.radians(35)
            m.harden_normals = True
            m.use_clamp_overlap = True
        if S.get("outline") and tag not in NO_OUTLINE and not tag.startswith("emit"):
            _, w = S["outline"]
            me.materials.append(mats["ink"])
            m = ob.modifiers.new("ink", "SOLIDIFY")
            m.thickness = w
            m.offset = 1.0
            m.use_flip_normals = True
            m.use_rim = False
            m.material_offset = 1
        if tag in ("plume", "beam", "tracer", "shieldfx"):
            ob.visible_shadow = False
    return empties
