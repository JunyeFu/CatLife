import bpy, sys
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=sys.argv[sys.argv.index("--") + 1], use_anim=True)
print("ACTIONS_BEGIN")
for action in bpy.data.actions:
    print(action.name, action.frame_range[:])
print("ACTIONS_END")
print("BONES_BEGIN")
for obj in bpy.context.scene.objects:
    if obj.type == "ARMATURE":
        for bone in obj.data.bones: print(bone.name)
print("BONES_END")
