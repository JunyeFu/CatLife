import bpy
import os
import sys
from mathutils import Vector

SOURCE = os.path.abspath(sys.argv[sys.argv.index("--") + 1])
bpy.ops.wm.open_mainfile(filepath=SOURCE)
armature = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
mesh = next(obj for obj in bpy.context.scene.objects if obj.type == "MESH" and any(mod.type == "ARMATURE" for mod in obj.modifiers))
names = sorted(action.name for action in bpy.data.actions if action.name.startswith("CL_CAT_"))
armature.animation_data_create()
depsgraph = bpy.context.evaluated_depsgraph_get()
for name in names:
    action = bpy.data.actions[name]
    armature.animation_data.action = action
    start, end = (int(value) for value in action.frame_range)
    for frame in (start, int((start + end) / 2), end):
        bpy.context.scene.frame_set(frame)
        evaluated = mesh.evaluated_get(depsgraph)
        evaluated_mesh = evaluated.to_mesh()
        points = [evaluated.matrix_world @ vertex.co for vertex in evaluated_mesh.vertices]
        evaluated.to_mesh_clear()
        minimum = Vector((min(point.x for point in points), min(point.y for point in points), min(point.z for point in points)))
        maximum = Vector((max(point.x for point in points), max(point.y for point in points), max(point.z for point in points)))
        hips = armature.pose.bones.get("Hips")
        print(f"ACTION_BOUNDS|{name}|frame={frame}|min={tuple(round(v, 4) for v in minimum)}|max={tuple(round(v, 4) for v in maximum)}|hips={tuple(round(v, 4) for v in hips.location)}")
