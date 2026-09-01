import json
import sys
from pathlib import Path

import bpy
from mathutils import Vector


def look_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def main():
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    if len(argv) != 1:
        raise RuntimeError("Output directory is required after --")
    output = Path(argv[0]).resolve()
    output.mkdir(parents=True, exist_ok=True)

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.resolution_x = 800
    scene.render.resolution_y = 600
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    scene.display.shading.show_shadows = True
    scene.display.shading.show_cavity = True
    scene.display.shading.cavity_type = "WORLD"
    scene.display.shading.background_type = "VIEWPORT"
    scene.display.shading.background_color = (0.31, 0.62, 0.86)

    camera_data = bpy.data.cameras.new("CAM_CL_SourceQA_Data")
    camera_data.lens = 48.0
    camera_data.clip_start = 0.05
    camera_data.clip_end = 250.0
    camera = bpy.data.objects.new("CAM_CL_SourceQA", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera

    focus = bpy.data.objects.get("Mesh_0.001")
    focus_target = tuple(focus.matrix_world.translation) if focus else (0.0, 0.0, 1.0)
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
        receipt.append({"name": name, "camera_position": position, "target": target, "path": str(destination), "engine": "WORKBENCH"})
        print(f"CATLIFE_RENDERED_SOURCE_VIEW={destination}")
    with (output / "views.json").open("w", encoding="utf-8") as stream:
        json.dump(receipt, stream, ensure_ascii=False, indent=2)


if __name__ == "__main__":
    main()
