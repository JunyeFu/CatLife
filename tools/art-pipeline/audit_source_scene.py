import csv
import json
import math
import os
import sys
from pathlib import Path

import bpy


def script_args():
    argv = sys.argv
    argv = argv[argv.index("--") + 1 :] if "--" in argv else []
    output_dir = Path(argv[0]).resolve() if argv else Path.cwd() / "art-pipeline-audit"
    output_dir.mkdir(parents=True, exist_ok=True)
    return output_dir


def csv_cell(value):
    if isinstance(value, (list, tuple)):
        return "|".join(str(item) for item in value)
    return value


def write_csv(path, fieldnames, rows):
    with path.open("w", newline="", encoding="utf-8-sig") as stream:
        writer = csv.DictWriter(stream, fieldnames=fieldnames, extrasaction="ignore")
        writer.writeheader()
        for row in rows:
            writer.writerow({key: csv_cell(row.get(key, "")) for key in fieldnames})


def round_vec(vector):
    return [round(float(value), 6) for value in vector]


def image_path(image):
    try:
        return bpy.path.abspath(image.filepath, library=image.library)
    except Exception:
        return image.filepath


def mesh_stats(obj):
    if obj.type != "MESH" or obj.data is None:
        return 0, 0, 0
    mesh = obj.data
    return len(mesh.vertices), len(mesh.edges), sum(max(0, len(poly.vertices) - 2) for poly in mesh.polygons)


def object_row(obj):
    vertices, edges, triangles = mesh_stats(obj)
    collections = sorted(collection.name for collection in obj.users_collection)
    materials = []
    if obj.type == "MESH" and obj.data is not None:
        materials = [slot.material.name if slot.material else "<none>" for slot in obj.material_slots]
    dimensions = round_vec(obj.dimensions)
    footprint = dimensions[0] * dimensions[1]
    volume = footprint * dimensions[2]
    return {
        "name": obj.name,
        "type": obj.type,
        "parent": obj.parent.name if obj.parent else "",
        "collections": collections,
        "location": round_vec(obj.location),
        "rotation_deg": round_vec([math.degrees(value) for value in obj.rotation_euler]),
        "scale": round_vec(obj.scale),
        "dimensions": dimensions,
        "footprint": round(footprint, 6),
        "volume": round(volume, 6),
        "vertices": vertices,
        "edges": edges,
        "triangles": triangles,
        "material_slots": len(materials),
        "materials": materials,
        "asset_id": obj.get("cl_asset_id", ""),
        "source_name": obj.get("source_name", ""),
    }


def material_row(material):
    images = []
    if material.use_nodes and material.node_tree:
        for node in material.node_tree.nodes:
            if node.type == "TEX_IMAGE" and node.image:
                images.append(node.image.name)
    return {
        "name": material.name,
        "users": material.users,
        "use_nodes": material.use_nodes,
        "node_count": len(material.node_tree.nodes) if material.use_nodes and material.node_tree else 0,
        "images": sorted(set(images)),
        "diffuse_rgba": round_vec(material.diffuse_color),
    }


def image_row(image):
    absolute_path = image_path(image)
    file_bytes = os.path.getsize(absolute_path) if absolute_path and os.path.isfile(absolute_path) else 0
    return {
        "name": image.name,
        "filepath": image.filepath,
        "absolute_path": absolute_path,
        "width": int(image.size[0]),
        "height": int(image.size[1]),
        "packed": bool(image.packed_file),
        "users": image.users,
        "file_bytes": file_bytes,
    }


def main():
    output_dir = script_args()
    object_rows = [object_row(obj) for obj in bpy.data.objects]
    material_rows = [material_row(material) for material in bpy.data.materials]
    image_rows = [image_row(image) for image in bpy.data.images]

    object_rows.sort(key=lambda row: (-row["triangles"], row["name"]))
    material_rows.sort(key=lambda row: (-row["users"], row["name"]))
    image_rows.sort(key=lambda row: (-row["file_bytes"], row["name"]))

    mesh_rows = [row for row in object_rows if row["type"] == "MESH"]
    large_horizontal = sorted(
        [row for row in mesh_rows if row["footprint"] >= 20.0],
        key=lambda row: (-row["footprint"], -row["volume"]),
    )[:40]
    anonymous_objects = [row["name"] for row in mesh_rows if row["name"].startswith(("node_", "Mesh_"))]
    anonymous_materials = [row["name"] for row in material_rows if row["name"].startswith("texture_pbr_")]

    summary = {
        "source_blend": bpy.data.filepath,
        "blender_version": bpy.app.version_string,
        "scene_names": [scene.name for scene in bpy.data.scenes],
        "collection_count": len(bpy.data.collections),
        "object_count": len(object_rows),
        "mesh_count": len(mesh_rows),
        "material_count": len(material_rows),
        "image_count": len(image_rows),
        "camera_count": len(bpy.data.cameras),
        "light_count": len(bpy.data.lights),
        "total_vertices": sum(row["vertices"] for row in mesh_rows),
        "total_triangles": sum(row["triangles"] for row in mesh_rows),
        "anonymous_object_count": len(anonymous_objects),
        "anonymous_material_count": len(anonymous_materials),
        "packed_image_count": sum(1 for row in image_rows if row["packed"]),
        "external_image_bytes": sum(row["file_bytes"] for row in image_rows),
        "anonymous_objects": anonymous_objects,
        "anonymous_materials": anonymous_materials,
        "large_horizontal_candidates": large_horizontal,
    }

    with (output_dir / "source_scene_audit.json").open("w", encoding="utf-8") as stream:
        json.dump(summary, stream, ensure_ascii=False, indent=2)

    write_csv(
        output_dir / "source_objects.csv",
        [
            "name", "type", "parent", "collections", "location", "rotation_deg", "scale",
            "dimensions", "footprint", "volume", "vertices", "edges", "triangles",
            "material_slots", "materials", "asset_id", "source_name",
        ],
        object_rows,
    )
    write_csv(
        output_dir / "source_materials.csv",
        ["name", "users", "use_nodes", "node_count", "images", "diffuse_rgba"],
        material_rows,
    )
    write_csv(
        output_dir / "source_images.csv",
        ["name", "filepath", "absolute_path", "width", "height", "packed", "users", "file_bytes"],
        image_rows,
    )

    with (output_dir / "island_candidates.md").open("w", encoding="utf-8") as stream:
        stream.write("# Large horizontal source objects\n\n")
        stream.write("Candidates are ranked by XY footprint. They are evidence for inspection, not automatic semantic assignments.\n\n")
        stream.write("| Source object | Dimensions | Triangles | Materials |\n")
        stream.write("|---|---:|---:|---|\n")
        for row in large_horizontal:
            stream.write(
                f"| `{row['name']}` | `{row['dimensions']}` | {row['triangles']} | "
                f"`{' | '.join(row['materials'])}` |\n"
            )

    print("CATLIFE_SOURCE_AUDIT=" + json.dumps(summary, ensure_ascii=False))


if __name__ == "__main__":
    main()
