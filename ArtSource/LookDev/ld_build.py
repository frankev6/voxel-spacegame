"""Turns an Acc into Blender objects with per-direction modifiers (bevel, subdivision, ink outline)."""

import math

import bmesh
import bpy

from ld_mats import material_for

HARD = {"hull", "hull2", "armor", "trim", "dark", "metal", "accent", "foil", "interior", "solar", "glass", "paint_w", "paint_p",
        "paint_e", "c_cargo", "gold", "chrome", "ceramic", "hose", "heat"}
PLANAR = {"hull", "hull2", "armor", "accent", "paint", "stripe"}
NO_OUTLINE = {"plume", "beam", "beam_x", "tracer", "shieldfx", "window", "nav_r", "nav_g", "nav_w", "grid", "ember"}


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
        base = tag.split("#")[0].split("@")[0]
        alien = base.startswith("a_")
        merge = base in HARD
        if merge:
            bm = bmesh.new()
            bm.from_mesh(me)
            bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4)
            if base in PLANAR:
                # merge coplanar cell faces into one polygon per flat region: removes the T-junctions that the
                # bevel would otherwise pinch into little triangles where cut cells meet
                bmesh.ops.dissolve_limit(bm, angle_limit=math.radians(0.5), use_dissolve_boundaries=False,
                                         verts=bm.verts, edges=bm.edges)
            bm.to_mesh(me)
            bm.free()
        flags = smooth[:len(me.polygons)] if len(smooth) >= len(me.polygons) else [False] * len(me.polygons)
        if alien:
            flags = [True] * len(me.polygons)
        me.polygons.foreach_set("use_smooth", flags)
        try:
            me.set_sharp_from_angle(angle=math.radians(80 if alien else 40))
        except Exception:
            pass
        ob = bpy.data.objects.new(me.name, me)
        col.objects.link(ob)
        ob.parent = empties[group]
        mat = material_for(mats, S, tag)
        me.materials.append(mat)
        # modifiers ---------------------------------------------------------------------------------------------
        if S.get("bevel") and base in HARD and base not in ("glass",):
            m = ob.modifiers.new("bevel", "BEVEL")
            m.width = S["bevel"]
            m.segments = S.get("bevel_seg", 2)
            m.limit_method = "ANGLE"
            m.angle_limit = math.radians(35)
            m.harden_normals = True
            m.use_clamp_overlap = True
        if S.get("outline") and base not in NO_OUTLINE and not base.startswith("emit"):
            _, w = S["outline"]
            me.materials.append(mats["ink"])
            m = ob.modifiers.new("ink", "SOLIDIFY")
            m.thickness = w
            m.offset = 1.0
            m.use_flip_normals = True
            m.use_rim = False
            m.material_offset = 1
        if tag in ("plume", "beam", "beam_x", "tracer", "shieldfx"):
            ob.visible_shadow = False
    return empties
