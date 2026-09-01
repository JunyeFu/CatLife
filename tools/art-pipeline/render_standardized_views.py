import json
import sys
from pathlib import Path

import bpy
from mathutils import Vector


def args():
    argv = sys.argv
    argv = argv[argv.index("--") + 1 :] if "--" in argv else []
    if not argv:
        raise RuntimeError("Output directory is required after --")
    output = Path(argv[0]).resolve()
    output.mkdir(parents=True, exist_ok=True)
    return output


def look_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def add_lighting(scene):
    if scene.world is None:
        scene.world = bpy.data.worlds.new("WORLD_CL_Runtime")
    scene.world.use_nodes = True
    background = scene.world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = (0.31, 0.62, 0.86, 1.0)
    background.inputs["Strength"].default_value = 0.65

    sun_data = bpy.data.lights.new("LGT_CL_WarmSun_Data", "SUN")
    sun_data.energy = 2.2
    sun_data.color = (1.0, 0.85, 0.65)
    sun = bpy.data.objects.new("LGT_CL_WarmSun", sun_data)
    scene.collection.objects.link(sun)
    sun.rotation_euler = (0.82, -0.40, -0.52)

    area_data = bpy.data.lights.new("LGT_CL_SoftFill_Data", "AREA")
    area_data.energy = 900.0
    area_data.shape = "DISK"
    area_data.size = 20.0
    area = bpy.data.objects.new("LGT_CL_SoftFill", area_data)
    scene.collection.objects.link(area)
    area.location = (-12.0, -18.0, 20.0)
    look_at(area, (0.0, 0.0, 0.0))


def main():
    output = args()
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 800
    scene.render.resolution_y = 600
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "8"
    scene.render.film_transparent = False
    add_lighting(scene)

    camera_data = bpy.data.cameras.new("CAM_CL_QA_Data")
    camera_data.lens = 48.0
    camera_data.clip_start = 0.05
    camera_data.clip_end = 250.0
    camera = bpy.data.objects.new("CAM_CL_QA", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera

    focus = bpy.data.objects.get("CL_BLD_FocusHouse_01")
    focus_target = tuple(focus.location) if focus else (0.0, 0.0, 1.0)
    views = {
        "01_hero": ((0.0, -43.0, 24.0), (0.0, 0.0, 0.5)),
        "02_front": ((0.0, -52.0, 13.0), (0.0, 0.0, 0.4)),
        "03_left": ((-45.0, -25.0, 20.0), (0.0, 0.0, 0.5)),
        "04_right": ((45.0, -25.0, 20.0), (0.0, 0.0, 0.5)),
        "05_top": ((0.0, 0.0, 58.0), (0.0, 0.0, 0.0)),
        "06_underside": ((0.0, -40.0, -12.0), (0.0, 0.0, -0.5)),
        "07_focus_house": ((focus_target[0] + 10.0, focus_target[1] - 13.0, focus_target[2] + 8.0), focus_target),
    }
    receipt = []
    for name, (position, target) in views.items():
        camera.location = position
        look_at(camera, target)
        destination = output / f"{name}.png"
        scene.render.filepath = str(destination)
        bpy.ops.render.render(write_still=True)
        receipt.append({"name": name, "camera_position": position, "target": target, "path": str(destination)})
        print(f"CATLIFE_RENDERED_VIEW={destination}")
    with (output / "views.json").open("w", encoding="utf-8") as stream:
        json.dump(receipt, stream, ensure_ascii=False, indent=2)


if __name__ == "__main__":
    main()
