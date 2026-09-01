import bpy
import math
import os
import sys
from mathutils import Vector

SOURCE = os.path.abspath(sys.argv[sys.argv.index("--") + 1])
OUTPUT_FBX = os.path.abspath(sys.argv[sys.argv.index("--") + 2])
OUTPUT_BLEND = os.path.abspath(sys.argv[sys.argv.index("--") + 3])
REPORT_DIR = os.path.abspath(sys.argv[sys.argv.index("--") + 4])
os.makedirs(REPORT_DIR, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=SOURCE, use_anim=True)
armature = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH" and any(mod.type == "ARMATURE" for mod in obj.modifiers)]
for obj in list(bpy.context.scene.objects):
    if obj != armature and obj not in meshes:
        bpy.data.objects.remove(obj, do_unlink=True)
armature.name = "CL_CAT_CORRECTED_Armature"
armature.data.name = "CL_CAT_CORRECTED_Armature"
for index, obj in enumerate(meshes):
    obj.name = "CL_CAT_CORRECTED_Mesh" if index == 0 else f"CL_CAT_CORRECTED_Mesh_{index + 1:02d}"

def bone_by(*tokens):
    lowered = [(bone, bone.name.lower()) for bone in armature.pose.bones]
    for token in tokens:
        for bone, name in lowered:
            if token in name:
                return bone
    return None

hips = bone_by("hips", "pelvis")
chest = bone_by("chest", "spine")
head = bone_by("head")
front_l = armature.pose.bones.get("frontleg")
front_r = armature.pose.bones.get("R_frontleg")
back_l = armature.pose.bones.get("backleg")
back_r = armature.pose.bones.get("R_backleg")
tail = bone_by("tail1", "tail_01", "tail")
controls = [bone for bone in (hips, chest, head, front_l, front_r, back_l, back_r, tail) if bone]
if not hips or not head or len(controls) < 4:
    raise RuntimeError("Required cat pose bones could not be resolved: " + ", ".join(b.name for b in controls))

for bone in controls:
    bone.rotation_mode = "XYZ"

def reset_pose():
    for bone in armature.pose.bones:
        bone.rotation_mode = "XYZ"
        bone.location = Vector((0, 0, 0))
        bone.rotation_euler = Vector((0, 0, 0))
        bone.scale = Vector((1, 1, 1))

def key(bone, frame, rotation=(0, 0, 0), location=(0, 0, 0)):
    if bone is None:
        return
    bone.rotation_euler = [math.radians(value) for value in rotation]
    bone.location = location
    bone.keyframe_insert("rotation_euler", frame=frame, group=bone.name)
    bone.keyframe_insert("location", frame=frame, group=bone.name)

def pose(frame, sit=0.0, lie=0.0, breath=0.0, attention=0.0):
    key(hips, frame, (-12 * sit - 28 * lie + breath * 40, 0, 0))
    key(chest, frame, (8 * sit + 18 * lie + attention * -8, 0, 0))
    key(head, frame, (6 * sit + 16 * lie + attention * -18, attention * 8, attention * 5))
    key(front_l, frame, (18 * sit + 68 * lie, 0, -5 * lie))
    key(front_r, frame, (18 * sit + 68 * lie, 0, 5 * lie))
    key(back_l, frame, (-58 * sit - 38 * lie, 0, -10 * sit))
    key(back_r, frame, (-58 * sit - 38 * lie, 0, 10 * sit))
    key(tail, frame, (0, 8 * sit, 12 * attention))

def action(name, frames, samples, loop=False):
    reset_pose()
    act = bpy.data.actions.get(name) or bpy.data.actions.new(name)
    act.name = name
    armature.animation_data_create()
    armature.animation_data.action = act
    for frame, values in samples:
        pose(frame, **values)
    act.frame_start, act.frame_end = frames
    act.use_fake_user = True
    return act

created = [
    action("CL_CAT_SitDownTransition_v01_72f", (1, 72), [(1, {}), (36, {"sit": .65}), (72, {"sit": 1})]),
    action("CL_CAT_SitIdle_v01_loop_96f", (1, 96), [(1, {"sit": 1}), (48, {"sit": 1, "breath": .018}), (96, {"sit": 1})], True),
    action("CL_CAT_LieDownTransition_v01_120f", (1, 120), [(1, {"sit": 1}), (36, {"sit": 1, "attention": .65}), (64, {"sit": 1, "attention": 1}), (88, {"sit": .55, "lie": .65}), (120, {"lie": 1})]),
    action("CL_CAT_FocusRest_v01_loop_96f", (1, 96), [(1, {"lie": 1}), (48, {"lie": 1, "breath": .022}), (96, {"lie": 1})], True),
    action("CL_CAT_FocusAttention_v01_48f", (1, 48), [(1, {"lie": 1}), (24, {"lie": 1, "attention": 1}), (48, {"lie": 1})]),
    action("CL_CAT_WakeUpTransition_v01_72f", (1, 72), [(1, {"lie": 1}), (36, {"sit": .55, "lie": .45}), (72, {"sit": 1})]),
]

approved_existing = {
    "CL_CAT_IdleBreath_v06_headsync_loop_108f", "CL_CAT_SRC_Walk_60fps", "CL_CAT_AlertLook_v01_loop_120f",
    "CL_CAT_CuriousSniff_v02_loop_112f", "CL_CAT_EarTwitchAlert_v02_loop_120f", "CL_CAT_HeadShakeNo_v01_loop_108f",
    "CL_CAT_HeadTiltListen_v01_loop_96f", "CL_CAT_LookBack_v02_loop_112f", "CL_CAT_PawWave_v01_loop_96f",
    "CL_CAT_StretchYawn_v03_slow_loop_264f", "CL_CAT_TailWagHappy_v01_loop_96f"
}
for existing in list(bpy.data.actions):
    for approved_name in approved_existing:
        if existing.name.endswith(approved_name):
            existing.name = approved_name
            break
keep = approved_existing | {action.name for action in created}
for existing in list(bpy.data.actions):
    if existing.name not in keep:
        bpy.data.actions.remove(existing)

scene = bpy.context.scene
scene.render.engine = "BLENDER_WORKBENCH"
scene.display.shading.light = "STUDIO"
scene.display.shading.show_shadows = True
scene.display.shading.color_type = "OBJECT"
for obj in meshes:
    obj.color = (0.92, 0.38, 0.08, 1.0)
scene.render.resolution_x = 420
scene.render.resolution_y = 420
scene.render.resolution_percentage = 100
camera_data = bpy.data.cameras.new("QA_Camera")
camera = bpy.data.objects.new("QA_Camera", camera_data)
scene.collection.objects.link(camera)
scene.camera = camera
corners = [obj.matrix_world @ Vector(corner) for obj in meshes for corner in obj.bound_box]
minimum = Vector((min(v.x for v in corners), min(v.y for v in corners), min(v.z for v in corners)))
maximum = Vector((max(v.x for v in corners), max(v.y for v in corners), max(v.z for v in corners)))
center = (minimum + maximum) * .5
size = max((maximum - minimum).length, .1)
camera.location = center + Vector((size * .85, -size * 1.65, size * .55))
direction = center - camera.location
camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
camera.data.lens = 58

report = ["source=" + SOURCE, "armature=" + armature.name, "mesh_count=" + str(len(meshes)), "bones=" + ",".join(b.name for b in controls)]
for act in created:
    armature.animation_data.action = act
    report.append(f"{act.name},frames={int(act.frame_start)}-{int(act.frame_end)},keyframed_bones={len(controls)}")
    for index, frame in enumerate((act.frame_start, (act.frame_start + act.frame_end) // 2, act.frame_end)):
        scene.frame_set(int(frame))
        scene.render.filepath = os.path.join(REPORT_DIR, f"{act.name}_{index + 1}.png")
        bpy.ops.render.render(write_still=True)

with open(os.path.join(REPORT_DIR, "cat_animation_runtime_v03.txt"), "w", encoding="utf-8") as handle:
    handle.write("\n".join(report))

bpy.data.objects.remove(camera, do_unlink=True)
armature.animation_data.action = created[1]
bpy.ops.wm.save_as_mainfile(filepath=OUTPUT_BLEND)
bpy.context.view_layer.objects.active = armature
for obj in bpy.context.selected_objects:
    obj.select_set(False)
armature.select_set(True)
for obj in meshes:
    obj.select_set(True)
bpy.ops.export_scene.fbx(
    filepath=OUTPUT_FBX,
    use_selection=True,
    object_types={"ARMATURE", "MESH"},
    apply_unit_scale=True,
    axis_forward="-Z",
    axis_up="Y",
    add_leaf_bones=False,
    bake_anim=True,
    bake_anim_use_all_bones=True,
    bake_anim_use_nla_strips=False,
    bake_anim_use_all_actions=True,
    bake_anim_force_startend_keying=True,
    path_mode="STRIP",
    embed_textures=False,
)
print("CAT_RUNTIME_V03_OK", OUTPUT_FBX)
