import csv
import sys
from pathlib import Path

import bpy


def candidate_path(image, extracted):
    name = image.name[:-4] if image.name.lower().endswith(".png") else image.name
    head, separator, suffix = name.rpartition("_")
    if separator and suffix.isdigit() and len(suffix) == 3:
        name = head + "." + suffix
    return extracted / (name + ".png")


def base_image(material):
    if material is None or not material.use_nodes or material.node_tree is None:
        return None
    for node in material.node_tree.nodes:
        if node.type != "TEX_IMAGE" or node.image is None:
            continue
        lowered = node.image.name.lower()
        if "normal" not in lowered and "metallic" not in lowered and "rough" not in lowered:
            return node.image
    return None


def main():
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    if len(argv) != 3:
        raise RuntimeError("Expected: <town.fbx> <extracted-texture-directory> <output.csv>")
    fbx = Path(argv[0]).resolve()
    extracted = Path(argv[1]).resolve()
    output = Path(argv[2]).resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.fbx_import(filepath=str(fbx))

    rows = []
    for obj in sorted((item for item in bpy.context.scene.objects if item.type == "MESH"), key=lambda item: item.name):
        for index, slot in enumerate(obj.material_slots):
            image = base_image(slot.material)
            candidate = candidate_path(image, extracted) if image else None
            rows.append({
                "source_object": obj.name,
                "material_slot": index,
                "source_material": slot.material.name if slot.material else "",
                "image_name": image.name if image else "",
                "image_path": str(candidate) if candidate and candidate.exists() else "",
                "image_bytes": candidate.stat().st_size if candidate and candidate.exists() else 0,
                "uv_layers": len(obj.data.uv_layers),
                "triangles": sum(max(0, len(poly.vertices) - 2) for poly in obj.data.polygons),
            })
    with output.open("w", newline="", encoding="utf-8-sig") as stream:
        writer = csv.DictWriter(stream, fieldnames=rows[0].keys())
        writer.writeheader()
        writer.writerows(rows)
    print(f"CATLIFE_TEXTURE_USAGE rows={len(rows)} textured={sum(1 for row in rows if row['image_path'])}")


if __name__ == "__main__":
    main()
