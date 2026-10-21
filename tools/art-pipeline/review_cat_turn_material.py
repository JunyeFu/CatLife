"""Render existing turn sources with the runtime texture and two fixed views.

blender -b --python this_file -- source.blend output texture.png [frame_step]
Only saves a separate review scene; never installs runtime assets.
"""
import bpy
import json
import os
import sys
import numpy as np
from mathutils import Vector

args = sys.argv[sys.argv.index('--') + 1:]
source, output, texture = map(os.path.abspath, args[:3])
step = int(args[3]) if len(args) > 3 else 1
os.makedirs(output, exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=source)
scene = bpy.context.scene
mesh = bpy.data.objects['CL_CAT_CORRECTED_Mesh']
rig = bpy.data.objects['CL_CAT_CORRECTED_Armature']
scene.frame_set(1)
bpy.context.view_layer.update()
material = bpy.data.materials.new('MAT_CL_TurnReview_RuntimeColor')
material.use_nodes = True
material.node_tree.nodes.clear()
shader = material.node_tree.nodes.new('ShaderNodeBsdfPrincipled')
surface = material.node_tree.nodes.new('ShaderNodeOutputMaterial')
material.node_tree.links.new(shader.outputs['BSDF'], surface.inputs['Surface'])
shader.inputs['Roughness'].default_value = .8
tex = material.node_tree.nodes.new('ShaderNodeTexImage')
tex.image = bpy.data.images.load(texture, check_existing=True)
material.node_tree.links.new(tex.outputs['Color'], shader.inputs['Base Color'])
for slot in mesh.material_slots:
    slot.material = material
scene.render.engine = 'CYCLES'
scene.cycles.samples = 8
scene.cycles.use_denoising = True
scene.render.resolution_x = 640
scene.render.resolution_y = 560
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.film_transparent = False
scene.view_settings.view_transform = 'AgX'
corners = [mesh.matrix_world @ Vector(p) for p in mesh.bound_box]
lo = Vector(tuple(min(p[i] for p in corners) for i in range(3)))
hi = Vector(tuple(max(p[i] for p in corners) for i in range(3)))
center = (lo + hi) / 2
extent = max(hi - lo)
scene.world = bpy.data.worlds.new('CL_WORLD_TurnReview')
scene.world.use_nodes = True
background = scene.world.node_tree.nodes.get('Background')
if background is None:
    background = next(n for n in scene.world.node_tree.nodes if n.type == 'BACKGROUND')
background.inputs['Color'].default_value = (.22,.25,.3,1)
background.inputs['Strength'].default_value = .7
for label, offset, power in [('Key',(1,-1,2),100),('Fill',(-1,1,1),50)]:
    light_data = bpy.data.lights.new('CL_LIGHT_TurnReview_'+label,'AREA')
    light_data.energy = power * extent * extent
    light_data.size = extent
    light = bpy.data.objects.new(light_data.name, light_data)
    scene.collection.objects.link(light)
    light.location = center + Vector(offset)*extent
    light.rotation_euler = (center-light.location).to_track_quat('-Z','Y').to_euler()
views = {'front': Vector((1.0, -1.8, .7)),
         'rear': Vector((-1.3, 1.6, .45))}
cameras = {}
for name, direction in views.items():
    data = bpy.data.cameras.new('CL_CAM_TurnReview_' + name)
    camera = bpy.data.objects.new(data.name, data)
    scene.collection.objects.link(camera)
    camera.location = center + direction * extent
    camera.rotation_euler = (center-camera.location).to_track_quat('-Z','Y').to_euler()
    data.type = 'ORTHO'
    data.ortho_scale = extent * 1.45
    cameras[name] = camera

# Re-evaluate every authored frame, including frames not rendered in preview mode.
groups = {g.name:g.index for g in mesh.vertex_groups}
roots = ('backleg','frontleg','R_backleg','R_frontleg')
feet = {r:np.array([v.index for v in mesh.data.vertices
        if any(g.group == groups[r+'2'] and g.weight > .99 for g in v.groups)]) for r in roots}
rows = []
for frame in range(1,122):
    scene.frame_set(frame)
    bpy.context.view_layer.update()
    evaluated = mesh.evaluated_get(bpy.context.evaluated_depsgraph_get())
    values = np.empty(len(evaluated.data.vertices)*3, dtype=np.float32)
    evaluated.data.vertices.foreach_get('co',values)
    vertices = values.reshape(-1,3) @ np.array(evaluated.matrix_world.to_3x3()).T + np.array(evaluated.matrix_world.translation)
    rows.append({'frame':frame,'minimum_z':float(vertices[:,2].min()),
                 'soles':{r:float(vertices[ids,2].min()) for r,ids in feet.items()}})
with open(os.path.join(output,'review.json'),'w',encoding='utf-8') as stream:
    json.dump({'source':source,'texture':texture,'action':rig.animation_data.action.name,
               'fps':60,'render_step':step,'units':'source world units',
               'scope':'Blender material review, not Unity or navigation acceptance',
               'samples':rows},stream,indent=2)
scene.frame_set(1)
scene.camera = cameras['front']
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(output,'CatLife_turn_material_review.blend'))
for name,camera in cameras.items():
    scene.camera = camera
    os.makedirs(os.path.join(output,name),exist_ok=True)
    for frame in sorted(set(range(1,122,step)) | {121}):
        scene.frame_set(frame)
        scene.render.filepath = os.path.join(output,name,f'frame-{frame:03d}.png')
        bpy.ops.render.render(write_still=True)
print('TURN_MATERIAL_REVIEW',output,flush=True)
