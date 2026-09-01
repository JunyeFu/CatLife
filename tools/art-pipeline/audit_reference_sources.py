import csv
import json
import sys
from pathlib import Path

import bpy
from mathutils import Vector


def triangle_count(mesh):
    return sum(max(0, len(polygon.vertices) - 2) for polygon in mesh.polygons)


def scene_bounds(mesh_objects):
    points = [obj.matrix_world @ Vector(corner) for obj in mesh_objects for corner in obj.bound_box]
    if not points:
        return [0.0, 0.0, 0.0]
    minimum = Vector((min(point.x for point in points), min(point.y for point in points), min(point.z for point in points)))
    maximum = Vector((max(point.x for point in points), max(point.y for point in points), max(point.z for point in points)))
    return [round(value, 6) for value in maximum - minimum]


def import_and_measure(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(path))
    objects = list(bpy.context.scene.objects)
    meshes = [obj for obj in objects if obj.type == "MESH" and obj.data is not None]
    ranked = sorted(meshes, key=lambda obj: triangle_count(obj.data), reverse=True)
    return {
        "source_file": str(path),
        "source_bytes": path.stat().st_size,
        "object_count": len(objects),
        "mesh_count": len(meshes),
        "triangle_count": sum(triangle_count(obj.data) for obj in meshes),
        "material_count": len(bpy.data.materials),
        "image_count": len(bpy.data.images),
        "scene_bounds": scene_bounds(meshes),
        "top_objects": "|".join(obj.name for obj in ranked[:12]),
    }


def main():
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    if len(argv) != 3:
        raise RuntimeError("Expected: <full-scene.glb> <individual-glb-directory> <output-directory>")
    full_scene = Path(argv[0]).resolve()
    individual_directory = Path(argv[1]).resolve()
    output_directory = Path(argv[2]).resolve()
    output_directory.mkdir(parents=True, exist_ok=True)

    sources = [full_scene] + sorted(individual_directory.glob("*.glb"), key=lambda path: path.name)
    rows = [import_and_measure(path) for path in sources]
    fields = [
        "source_file", "source_bytes", "object_count", "mesh_count", "triangle_count",
        "material_count", "image_count", "scene_bounds", "top_objects",
    ]
    with (output_directory / "source_reference_contact.csv").open("w", newline="", encoding="utf-8-sig") as stream:
        writer = csv.DictWriter(stream, fieldnames=fields)
        writer.writeheader()
        for row in rows:
            serialized = dict(row)
            serialized["scene_bounds"] = "|".join(str(value) for value in row["scene_bounds"])
            writer.writerow(serialized)

    summary = {
        "full_scene": rows[0],
        "individual_source_count": len(rows) - 1,
        "individual_sources": rows[1:],
        "inspection_policy": "Semantic reference only; no GLB in this report is a Unity runtime dependency.",
    }
    with (output_directory / "source_reference_summary.json").open("w", encoding="utf-8") as stream:
        json.dump(summary, stream, ensure_ascii=False, indent=2)
    print("CATLIFE_REFERENCE_AUDIT=" + json.dumps({"sources": len(rows), "individual_sources": len(rows) - 1}, ensure_ascii=False))


if __name__ == "__main__":
    main()
