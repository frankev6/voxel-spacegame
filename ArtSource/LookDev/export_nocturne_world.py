"""Bake the approved own-work Nocturne surface for the physical M1 planet.
Run: /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python ArtSource/LookDev/export_nocturne_world.py
The sky is a procedural, animated Unity shader; no sky texture is generated.
"""
import math
import sys
from pathlib import Path
import bpy
sys.path.insert(0, str(Path(__file__).resolve().parent))
from ld_styles import STYLES
from ld_world import planet, sun_dir

root = Path(__file__).resolve().parents[2]
output = root / 'Assets/Game/Content/Environment/Nocturne/T_VesperSurface.png'
output.parent.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
scene = bpy.context.scene
style = STYLES['nocturne']
scene.render.engine = 'CYCLES'
scene.cycles.samples = 16
scene.view_settings.view_transform = style['view']
scene.view_settings.look = style['look']
scene.view_settings.exposure = style['exposure']
scene.render.image_settings.file_format = 'PNG'
scene.render.image_settings.color_mode = 'RGB'

# Bake the existing procedural surface, including its fixed stellar illumination and lightning.
# Geometry and atmosphere remain separate in Unity, with real perspective/parallax.
body = planet(style, 'VESPER-9 surface', 1, (0, 0, 0), style['planet'], sun_dir(style), rot=(0, 0, 0))
# Explicit seam-duplicated latitude/longitude topology matches Unity's sphere.
# Shifting UVs beyond 1 on Blender's welded primitive left an unbaked black meridian.
material = body.data.materials[0]
segments, rings = 256, 128
verts, faces, texcoords = [], [], []
for y in range(rings + 1):
    v = y / rings
    lat = (v - .5) * math.pi
    for x in range(segments + 1):
        u = x / segments
        lon = u * 2 * math.pi
        verts.append((math.cos(lon)*math.cos(lat), math.sin(lon)*math.cos(lat), math.sin(lat)))
        texcoords.append((u, v))
        if x < segments and y < rings:
            a = y * (segments + 1) + x
            b = a + segments + 1
            faces.append((a, a + 1, b + 1, b))
mesh = bpy.data.meshes.new('SurfaceBakeSphere')
mesh.from_pydata(verts, [], faces)
mesh.materials.append(material)
body.data = mesh
uv = mesh.uv_layers.new(name='SurfaceUV')
for polygon in mesh.polygons:
    polygon.use_smooth = True
    for index in polygon.loop_indices:
        uv.data[index].uv = texcoords[mesh.loops[index].vertex_index]
texture = bpy.data.images.new('VESPER-9 surface', width=4096, height=2048, alpha=False)
material = body.data.materials[0]
node = material.node_tree.nodes.new('ShaderNodeTexImage')
node.image = texture
material.node_tree.nodes.active = node
bpy.ops.object.select_all(action='DESELECT')
body.select_set(True)
bpy.context.view_layer.objects.active = body
scene.render.bake.margin = 8
bpy.ops.object.bake(type='EMIT')
# Save through the same AgX view transform as the look book and sky.
texture.save_render(str(output.parent / 'T_VesperSurface.png'), scene=scene)
print('NOCTURNE_SURFACE_EXPORT', output.parent / 'T_VesperSurface.png')
