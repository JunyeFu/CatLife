import csv
import json
import math
import shutil
import subprocess
import sys
from pathlib import Path

import bpy
from mathutils import Vector


SOURCE_VERSION = "20260630"
SOURCE_FILE = "03-3d-models/catlife-town/current/catlife_v2_island_grass_style_no_skybox_20260630.blend"
AUTHORITY_SOURCE_TRIANGLES = 3499807

KNOWN_OBJECTS = {
    "柱体": ("ENV", "IslandBase", "完整空岛", "ISLAND"),
    "CL_StylePass_LowPoly_GrassPuffs_OnIsland": ("ENV", "IslandGrass", "空岛草丛", "ISLAND_GRASS"),
    "CL_StylePass_LowPoly_ColorFlowers_OnIsland": ("ENV", "IslandFlowers", "空岛花丛", "ISLAND_FLOWERS"),
    "CL_StylePass_LowPoly_TanStones_OnIsland": ("ENV", "IslandStones", "空岛石块", "ISLAND_STONES"),
    "Mesh_0.001": ("BLD", "FocusHouse", "专注小屋", "FOCUS_HOUSE"),
    "node_0.018": ("BLD", "TomatoClockTower", "番茄钟楼", "TOMATO_CLOCK_TOWER"),
    "node_0.007": ("BLD", "CatHouse", "猫咪小屋", "CAT_HOUSE"),
    "node_0.008": ("BLD", "FishShop", "鱼干超市", "FISH_SHOP"),
    "node_0.009": ("BLD", "TownGate", "小镇入口", "TOWN_GATE"),
    "node_0.017": ("ENV", "RewardTree", "许愿树", "REWARD_TREE"),
    "Mesh_0.005": ("ROAD", "PlazaRing", "广场环路", "PLAZA_RING"),
    "Mesh_0.007": ("ROAD", "PlazaPath", "广场道路", "PLAZA_PATH"),
    "Mesh_0.010": ("PROP", "CenterStone", "中心石板", "CENTER_STONE"),
    "node_0.037": ("ROAD", "CentralGardenRing", "中心花园环", "CENTRAL_GARDEN_RING"),
}

LANDMARK_TEXTURES = {
    "Mesh_0.001": ("Material.038", "T_CL_FocusHouse_BaseColor_1024.png", "MAT_CL_FocusHouse"),
    "node_0.018": ("Material.054", "T_CL_TomatoClockTower_BaseColor_1024.png", "MAT_CL_TomatoClockTower"),
    "node_0.007": ("Material.044", "T_CL_CatHouse_BaseColor_1024.png", "MAT_CL_CatHouse"),
    "node_0.008": ("Material.045", "T_CL_FishShop_BaseColor_1024.png", "MAT_CL_FishShop"),
    "node_0.009": ("Material.046", "T_CL_TownGate_BaseColor_1024.png", "MAT_CL_TownGate"),
    "node_0.017": ("Material.029", "T_CL_RewardTree_BaseColor_1024.png", "MAT_CL_RewardTree"),
    "Mesh_0.005": ("Material.042", "T_CL_Plaza_BaseColor_1024.png", "MAT_CL_Plaza"),
    "Mesh_0.010": ("Material.066", "T_CL_CenterStone_BaseColor_1024.png", "MAT_CL_CenterStone"),
}

ATLAS_SIZE = 1024
ATLAS_GRID = 8
ATLAS_TILE_SIZE = ATLAS_SIZE // ATLAS_GRID

PALETTE = {
    "GrassSoftGreen": (0.40, 0.67, 0.22, 1.0),
    "GrassLight": (0.60, 0.80, 0.31, 1.0),
    "GrassDeep": (0.27, 0.52, 0.15, 1.0),
    "SoilWarm": (0.58, 0.31, 0.12, 1.0),
    "SoilEdge": (0.72, 0.43, 0.20, 1.0),
    "IslandDarkBottom": (0.25, 0.14, 0.07, 1.0),
    "StoneLight": (0.62, 0.57, 0.49, 1.0),
    "WoodWarm": (0.48, 0.27, 0.12, 1.0),
    "WallCream": (0.88, 0.76, 0.56, 1.0),
    "FoliageGreen": (0.25, 0.55, 0.19, 1.0),
    "AccentFlower": (0.96, 0.55, 0.38, 1.0),
}


def pipeline_args():
    argv = sys.argv
    argv = argv[argv.index("--") + 1 :] if "--" in argv else []
    if not argv:
        raise RuntimeError("Pipeline output root is required after --")
    root = Path(argv[0]).resolve()
    for relative in (
        "master",
        "runtime/textures",
        "render",
        "render/textures",
        "reports/source-views",
        "reports/master-views",
        "reports/runtime-views",
    ):
        (root / relative).mkdir(parents=True, exist_ok=True)
    source_fbx = Path(argv[1]).resolve() if len(argv) > 1 else None
    return root, source_fbx


def relink_fbx_images(root):
    repo_root = Path(__file__).resolve().parents[2]
    extracted = repo_root / "work" / "CatLife_Unity_Main" / "Assets" / "Art" / "Town" / "Textures" / "Extracted"
    relinked = 0
    receipt = []
    for image in bpy.data.images:
        if image.name == "Render Result":
            continue
        candidate_name = image.name
        if candidate_name.lower().endswith(".png"):
            candidate_name = candidate_name[:-4]
        head, separator, suffix = candidate_name.rpartition("_")
        if separator and suffix.isdigit() and len(suffix) == 3:
            candidate_name = head + "." + suffix
        candidate = extracted / (candidate_name + ".png")
        if not candidate.exists():
            continue
        render_texture = root / "render" / "textures" / candidate.name
        if not render_texture.exists() or render_texture.stat().st_size != candidate.stat().st_size:
            shutil.copy2(candidate, render_texture)
        image.filepath = str(render_texture)
        image.reload()
        relinked += 1
        receipt.append({"image_name": image.name, "render_path": str(render_texture), "bytes": render_texture.stat().st_size})
    with (root / "reports" / "render_texture_receipt.csv").open("w", newline="", encoding="utf-8-sig") as stream:
        writer = csv.DictWriter(stream, fieldnames=["image_name", "render_path", "bytes"])
        writer.writeheader()
        writer.writerows(receipt)
    print(f"CATLIFE_RELINKED_IMAGES={relinked}")


def triangles(obj):
    if obj.type != "MESH" or obj.data is None:
        return 0
    return sum(max(0, len(poly.vertices) - 2) for poly in obj.data.polygons)


def ascii_safe(value):
    return all(ord(char) < 128 for char in value)


def classify(obj, old_collections):
    known = KNOWN_OBJECTS.get(obj.name)
    if known:
        return known[0], known[1], known[2], known[3]
    dimensions = obj.dimensions
    footprint = dimensions.x * dimensions.y
    if any("Road" in name or "Stone_Ring" in name for name in old_collections):
        role = "ROAD"
    elif dimensions.z <= 0.65 and footprint >= 1.0:
        role = "ROAD"
    elif dimensions.z >= 1.5 and footprint >= 4.0:
        role = "BLD"
    elif dimensions.z >= 1.5:
        role = "ENV"
    else:
        role = "PROP"
    return role, "Source", obj.name if not ascii_safe(obj.name) else "", ""


def canonical_collections():
    scene_root = bpy.context.scene.collection
    root = bpy.data.collections.new("COL_CL_MASTER")
    scene_root.children.link(root)
    result = {}
    for name in (
        "00_REFERENCE",
        "10_ISLAND",
        "20_ROADS",
        "30_BUILDINGS",
        "40_PROPS",
        "50_VEGETATION",
        "60_ANCHORS",
        "90_CAMERAS",
        "99_LIGHTS",
    ):
        collection = bpy.data.collections.new(name)
        root.children.link(collection)
        result[name] = collection
    return root, result


def move_to_collection(obj, collection):
    if collection not in obj.users_collection:
        collection.objects.link(obj)
    for existing in list(obj.users_collection):
        if existing != collection:
            existing.objects.unlink(obj)


def standardize_scene():
    source_records = {}
    source_materials = {material.name: material for material in bpy.data.materials}
    root, collections = canonical_collections()
    counters = {"ENV": 0, "ROAD": 0, "BLD": 0, "PROP": 0}

    for obj in sorted(bpy.data.objects, key=lambda item: item.name):
        source_name = obj.name
        old_collections = [collection.name for collection in obj.users_collection]
        if obj.type == "CAMERA":
            obj["source_name"] = source_name
            obj.name = "CAM_CL_SourcePreview_01"
            move_to_collection(obj, collections["90_CAMERAS"])
            continue
        if obj.type == "LIGHT":
            obj["source_name"] = source_name
            counters.setdefault("LIGHT", 0)
            counters["LIGHT"] += 1
            obj.name = f"LGT_CL_Source_{counters['LIGHT']:02d}"
            move_to_collection(obj, collections["99_LIGHTS"])
            continue
        if obj.type != "MESH":
            move_to_collection(obj, collections["00_REFERENCE"])
            continue

        role, semantic, display_name_zh, landmark_id = classify(obj, old_collections)
        counters[role] += 1
        if semantic == "Source":
            runtime_name = f"CL_{role}_Source_{counters[role]:03d}"
        else:
            runtime_name = f"CL_{role}_{semantic}_01"
        asset_id = runtime_name.replace("_", ".")
        source_records[source_name] = {
            "object": obj,
            "source_name": source_name,
            "runtime_name": runtime_name,
            "asset_id": asset_id,
            "role": role,
            "display_name_zh": display_name_zh,
            "landmark_id": landmark_id,
            "source_triangles": triangles(obj),
        }
        obj["cl_asset_id"] = asset_id
        obj["source_name"] = source_name
        obj["asset_role"] = role
        obj["mobile_export"] = True
        obj["render_export"] = True
        obj.name = runtime_name
        if obj.data:
            obj.data.name = "MSH_" + runtime_name

        if semantic.startswith("Island"):
            target_collection = collections["10_ISLAND"]
        elif role == "ROAD":
            target_collection = collections["20_ROADS"]
        elif role == "BLD":
            target_collection = collections["30_BUILDINGS"]
        elif role == "ENV":
            target_collection = collections["50_VEGETATION"]
        else:
            target_collection = collections["40_PROPS"]
        move_to_collection(obj, target_collection)

    canonical = {root, *collections.values()}
    for collection in list(bpy.data.collections):
        if collection not in canonical:
            bpy.data.collections.remove(collection)

    for index, material in enumerate(sorted(bpy.data.materials, key=lambda item: item.name), start=1):
        source_name = material.name
        material["source_name"] = source_name
        material.name = f"MAT_CL_Render_{index:03d}"

    return source_records, source_materials


def look_at(obj, target):
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def ensure_world(scene):
    if scene.world is None:
        scene.world = bpy.data.worlds.new("WORLD_CL_Sky")
    scene.world.use_nodes = True
    background = scene.world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = (0.32, 0.62, 0.86, 1.0)
    background.inputs["Strength"].default_value = 0.55


def render_views(output_dir, source_records):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 800
    scene.render.resolution_y = 600
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = False
    scene.render.image_settings.color_depth = "8"
    scene.render.filepath = str(output_dir)
    ensure_world(scene)

    camera_data = bpy.data.cameras.new("CAM_CL_QA_Data")
    camera = bpy.data.objects.new("CAM_CL_QA", camera_data)
    scene.collection.objects.link(camera)
    camera_data.lens = 48.0
    camera_data.clip_start = 0.05
    camera_data.clip_end = 250.0
    scene.camera = camera

    focus = source_records.get("Mesh_0.001", {}).get("object")
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
        destination = output_dir / f"{name}.png"
        scene.render.filepath = str(destination)
        bpy.ops.render.render(write_still=True)
        receipt.append({"name": name, "camera_position": position, "target": target, "path": str(destination)})
    with (output_dir / "views.json").open("w", encoding="utf-8") as stream:
        json.dump(receipt, stream, ensure_ascii=False, indent=2)
    bpy.data.objects.remove(camera, do_unlink=True)
    bpy.data.cameras.remove(camera_data)


def base_image(material):
    if material is None or not material.use_nodes or material.node_tree is None:
        return None
    candidates = []
    for node in material.node_tree.nodes:
        if node.type != "TEX_IMAGE" or node.image is None:
            continue
        lowered = node.image.name.lower()
        if "normal" in lowered or "metallic" in lowered or "rough" in lowered:
            continue
        candidates.append(node.image)
    return candidates[0] if candidates else None


def export_runtime_textures(runtime_texture_dir, source_records, source_materials, report_dir):
    exported = {}
    for source_object, (source_material, filename, runtime_material) in LANDMARK_TEXTURES.items():
        image = base_image(source_materials.get(source_material))
        if image is None:
            raise RuntimeError(f"Missing base-color image for {source_material}")
        copy = image.copy()
        copy.name = filename
        copy.scale(1024, 1024)
        copy.file_format = "PNG"
        copy.filepath_raw = str(runtime_texture_dir / filename)
        copy.save()
        exported[source_object] = {
            "path": runtime_texture_dir / filename,
            "material_name": runtime_material,
        }
        bpy.data.images.remove(copy)

    request_rows = []
    for source_name, record in source_records.items():
        if source_name == "柱体" or source_name in LANDMARK_TEXTURES:
            continue
        for slot_index, slot in enumerate(record["object"].material_slots):
            if slot.material is None:
                continue
            source_material_name = slot.material.get("source_name", slot.material.name)
            image = base_image(source_materials.get(source_material_name))
            if image is None:
                continue
            request_rows.append(
            {
                "source_object": source_name,
                "material_slot": slot_index,
                "source_material": source_material_name,
                "source_image": bpy.path.abspath(image.filepath),
            }
        )
    request_path = report_dir / "texture_atlas_requests.csv"
    manifest_path = report_dir / "texture_atlas_manifest.csv"
    with request_path.open("w", newline="", encoding="utf-8-sig") as stream:
        writer = csv.DictWriter(
            stream,
            fieldnames=["source_object", "material_slot", "source_material", "source_image"],
        )
        writer.writeheader()
        writer.writerows(request_rows)
    subprocess.run(
        [
            "py",
            str(Path(__file__).with_name("pack_texture_atlases.py")),
            str(request_path),
            str(runtime_texture_dir),
            str(manifest_path),
        ],
        check=True,
    )

    atlas_slots = {}
    atlas_names = set()
    with manifest_path.open(newline="", encoding="utf-8-sig") as stream:
        for row in csv.DictReader(stream):
            atlas_names.add(row["atlas_texture"])
            atlas_slots[(row["source_object"], int(row["material_slot"]))] = {
                "path": runtime_texture_dir / row["atlas_texture"],
                "material_name": row["atlas_material"],
                "tile_x": int(row["tile_x"]),
                "tile_y": int(row["tile_y"]),
            }
    atlas_count = len(atlas_names)
    print(f"CATLIFE_RUNTIME_ATLASES={atlas_count} mapped_slots={len(atlas_slots)}")
    return exported, atlas_slots


def make_material(name, color, texture_path=None):
    material = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    shader.inputs["Base Color"].default_value = color
    shader.inputs["Roughness"].default_value = 0.72
    links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    if texture_path is not None:
        image = bpy.data.images.load(str(texture_path), check_existing=True)
        image.colorspace_settings.name = "sRGB"
        texture = nodes.new("ShaderNodeTexImage")
        texture.image = image
        links.new(texture.outputs["Color"], shader.inputs["Base Color"])
        links.new(texture.outputs["Alpha"], shader.inputs["Alpha"])
    return material


def palette_materials():
    return {key: make_material("MAT_CL_" + key, value) for key, value in PALETTE.items()}


def fallback_material(record, source_name, palette):
    semantic = KNOWN_OBJECTS.get(source_name, (record["role"], "", "", ""))[1]
    if semantic == "IslandGrass":
        return palette["FoliageGreen"]
    if semantic == "IslandFlowers":
        return palette["AccentFlower"]
    if semantic == "IslandStones" or record["role"] == "ROAD":
        return palette["StoneLight"]
    if record["role"] == "ENV":
        return palette["FoliageGreen"]
    if record["role"] == "BLD":
        return palette["WallCream"]
    if record["role"] == "PROP":
        return palette["WoodWarm"]
    return palette["GrassSoftGreen"]


def remap_atlas_uv(obj, polygon, atlas_slot):
    uv_layer = obj.data.uv_layers.active
    if uv_layer is None:
        return
    tile_x = atlas_slot["tile_x"]
    tile_y = atlas_slot["tile_y"]
    pixel_inset = 0.5 / ATLAS_SIZE
    tile_scale = (ATLAS_TILE_SIZE - 1.0) / ATLAS_SIZE
    for loop_index in polygon.loop_indices:
        uv = uv_layer.data[loop_index].uv
        source_u = uv.x if 0.0 <= uv.x <= 1.0 else uv.x - math.floor(uv.x)
        source_v = uv.y if 0.0 <= uv.y <= 1.0 else uv.y - math.floor(uv.y)
        uv.x = tile_x / ATLAS_GRID + pixel_inset + source_u * tile_scale
        uv.y = tile_y / ATLAS_GRID + pixel_inset + source_v * tile_scale


def replace_materials(source_records, texture_exports, atlas_slots):
    palette = palette_materials()
    textured = {
        source_name: make_material(info["material_name"], (1.0, 1.0, 1.0, 1.0), info["path"])
        for source_name, info in texture_exports.items()
    }
    atlas_materials = {}
    for info in atlas_slots.values():
        if info["material_name"] not in atlas_materials:
            atlas_materials[info["material_name"]] = make_material(
                info["material_name"], (1.0, 1.0, 1.0, 1.0), info["path"]
            )
    island_slot_map = {
        "M_Island_GrassTop": palette["GrassSoftGreen"],
        "M_Island_SoilSide": palette["SoilWarm"],
        "M_Island_DarkBottom": palette["IslandDarkBottom"],
        "M_Island_GrassTop_LightPatch": palette["GrassLight"],
        "M_Island_SoilSide_WarmEdge": palette["SoilEdge"],
        "M_Island_GrassTop_DeepPatch": palette["GrassDeep"],
    }
    assigned = {}
    for source_name, record in source_records.items():
        obj = record["object"]
        old_material_names = [
            slot.material.get("source_name", slot.material.name) if slot.material else ""
            for slot in obj.material_slots
        ]
        target_materials = []
        target_indices = {}
        for slot_index, material_name in enumerate(old_material_names):
            atlas_slot = atlas_slots.get((source_name, slot_index))
            if source_name == "柱体":
                target = island_slot_map.get(material_name, palette["GrassSoftGreen"])
            elif source_name in textured:
                target = textured[source_name]
            elif atlas_slot is not None:
                target = atlas_materials[atlas_slot["material_name"]]
            else:
                target = fallback_material(record, source_name, palette)
            if target not in target_materials:
                target_materials.append(target)
            target_indices[slot_index] = target_materials.index(target)

        if not target_materials:
            target_materials.append(fallback_material(record, source_name, palette))

        for polygon in obj.data.polygons:
            old_index = polygon.material_index
            atlas_slot = atlas_slots.get((source_name, old_index))
            if atlas_slot is not None:
                remap_atlas_uv(obj, polygon, atlas_slot)
            polygon.material_index = target_indices.get(old_index, 0)

        obj.data.materials.clear()
        for material in target_materials:
            obj.data.materials.append(material)
        assigned[source_name] = "|".join(material.name for material in target_materials)

    used = {material for obj in bpy.data.objects if obj.type == "MESH" for material in obj.data.materials if material}
    for material in list(bpy.data.materials):
        if material not in used:
            bpy.data.materials.remove(material)
    used_images = {
        node.image
        for material in used
        if material.use_nodes and material.node_tree
        for node in material.node_tree.nodes
        if node.type == "TEX_IMAGE" and node.image
    }
    for image in list(bpy.data.images):
        if image not in used_images:
            bpy.data.images.remove(image)
    bpy.ops.outliner.orphans_purge(do_recursive=True)
    return assigned


def normalize_transforms(source_records):
    for record in source_records.values():
        obj = record["object"]
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
        obj.select_set(False)


def apply_decimate(obj, ratio):
    source_triangles = triangles(obj)
    if source_triangles <= 120 or ratio >= 0.999:
        return
    minimum_ratio = min(1.0, 120.0 / source_triangles)
    modifier = obj.modifiers.new("CL_Runtime_Decimate", "DECIMATE")
    modifier.ratio = max(minimum_ratio, ratio)
    modifier.use_collapse_triangulate = True
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    obj.select_set(False)


def optimize_runtime(source_records):
    protected = {
        "柱体",
        "CL_StylePass_LowPoly_GrassPuffs_OnIsland",
        "CL_StylePass_LowPoly_ColorFlowers_OnIsland",
        "CL_StylePass_LowPoly_TanStones_OnIsland",
    }
    landmarks = {"Mesh_0.001", "node_0.018", "node_0.007", "node_0.008", "node_0.017"}
    plazas = {"Mesh_0.005", "Mesh_0.007", "Mesh_0.010", "node_0.037"}
    for source_name, record in source_records.items():
        if source_name in protected:
            ratio = 1.0
        elif source_name in landmarks:
            ratio = 0.45
        elif source_name in plazas:
            ratio = 0.30
        else:
            ratio = 0.20
        apply_decimate(record["object"], ratio)

    total = sum(triangles(record["object"]) for record in source_records.values())
    if total > 290000:
        correction = 285000.0 / total
        for source_name, record in source_records.items():
            if source_name not in protected:
                apply_decimate(record["object"], correction)
    return sum(triangles(record["object"]) for record in source_records.values())


def material_name(obj):
    names = [material.name for material in obj.data.materials if material]
    return "|".join(names)


def texture_names(obj):
    names = []
    for material in obj.data.materials:
        if material is None or not material.use_nodes or material.node_tree is None:
            continue
        for node in material.node_tree.nodes:
            if node.type == "TEX_IMAGE" and node.image:
                names.append(Path(node.image.filepath).name)
    return "|".join(sorted(set(names)))


def write_manifest(path, source_records):
    fields = [
        "asset_id",
        "display_name_zh",
        "runtime_name",
        "category",
        "source_file",
        "source_object",
        "source_version",
        "mobile_policy",
        "render_policy",
        "triangle_budget",
        "material_set",
        "texture_set",
        "landmark_id",
        "status",
    ]
    rows = []
    for source_name, record in sorted(source_records.items(), key=lambda item: item[1]["runtime_name"]):
        obj = record["object"]
        rows.append(
            {
                "asset_id": record["asset_id"],
                "display_name_zh": record["display_name_zh"],
                "runtime_name": record["runtime_name"],
                "category": record["role"],
                "source_file": SOURCE_FILE,
                "source_object": source_name,
                "source_version": SOURCE_VERSION,
                "mobile_policy": "required",
                "render_policy": "required",
                "triangle_budget": triangles(obj),
                "material_set": material_name(obj),
                "texture_set": texture_names(obj),
                "landmark_id": record["landmark_id"],
                "status": "READY",
            }
        )
    with path.open("w", newline="", encoding="utf-8-sig") as stream:
        writer = csv.DictWriter(stream, fieldnames=fields)
        writer.writeheader()
        writer.writerows(rows)


def remove_runtime_non_meshes():
    for obj in list(bpy.data.objects):
        if obj.type != "MESH":
            bpy.data.objects.remove(obj, do_unlink=True)
    for camera in list(bpy.data.cameras):
        bpy.data.cameras.remove(camera)
    for light in list(bpy.data.lights):
        bpy.data.lights.remove(light)


def export_fbx(path, source_records):
    bpy.ops.object.select_all(action="DESELECT")
    for record in source_records.values():
        record["object"].select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=str(path),
        use_selection=True,
        axis_forward="-Z",
        axis_up="Y",
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        bake_space_transform=False,
        object_types={"MESH"},
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        use_triangles=True,
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="RELATIVE",
        embed_textures=False,
    )
    bpy.ops.object.select_all(action="DESELECT")


def report_pipeline(root, source_records, runtime_triangles):
    files = {
        "render_blend": root / "render" / "CatLife_render.blend",
        "master_blend": root / "master" / "CatLife_master.blend",
        "runtime_blend": root / "runtime" / "CatLife_runtime.blend",
        "runtime_fbx": root / "runtime" / "CL_TWN_Runtime.fbx",
    }
    report = {
        "source_file": SOURCE_FILE,
        "source_object_count": len(source_records),
        "source_triangles": AUTHORITY_SOURCE_TRIANGLES,
        "fbx_interchange_triangles": sum(record["source_triangles"] for record in source_records.values()),
        "runtime_triangles": runtime_triangles,
        "runtime_materials": len(bpy.data.materials),
        "runtime_images": len(bpy.data.images),
        "anonymous_runtime_names": [
            obj.name for obj in bpy.data.objects if obj.name.startswith(("node_", "Mesh_", "texture_pbr_"))
        ],
        "files": {
            name: {"path": str(path), "bytes": path.stat().st_size if path.exists() else 0}
            for name, path in files.items()
        },
    }
    with (root / "reports" / "pipeline_summary.json").open("w", encoding="utf-8") as stream:
        json.dump(report, stream, ensure_ascii=False, indent=2)
    with (root / "reports" / "pipeline_summary.md").open("w", encoding="utf-8") as stream:
        stream.write("# CatLife standardized art pipeline summary\n\n")
        stream.write(f"- Source meshes: {report['source_object_count']}\n")
        stream.write(f"- Source triangles: {report['source_triangles']}\n")
        stream.write(f"- Runtime triangles: {report['runtime_triangles']}\n")
        stream.write(f"- Runtime materials: {report['runtime_materials']}\n")
        stream.write(f"- Runtime images: {report['runtime_images']}\n")
        stream.write(f"- Anonymous runtime names: {len(report['anonymous_runtime_names'])}\n")
        stream.write("- Traceability: stable asset IDs, source paths, source object names, versions, budgets, and visual receipts.\n")
        stream.write("- Checksums are not used as an acceptance gate.\n")
    print("CATLIFE_PIPELINE_SUMMARY=" + json.dumps(report, ensure_ascii=False))


def main():
    root, source_fbx = pipeline_args()
    if source_fbx is not None:
        bpy.ops.object.select_all(action="SELECT")
        bpy.ops.object.delete(use_global=False)
        bpy.ops.wm.fbx_import(filepath=str(source_fbx))
        relink_fbx_images(root)
    source_records, source_materials = standardize_scene()
    bpy.ops.wm.save_as_mainfile(filepath=str(root / "render" / "CatLife_render.blend"), compress=True)

    texture_exports, atlas_slots = export_runtime_textures(
        root / "runtime" / "textures", source_records, source_materials, root / "reports"
    )
    replace_materials(source_records, texture_exports, atlas_slots)
    normalize_transforms(source_records)
    bpy.ops.wm.save_as_mainfile(filepath=str(root / "master" / "CatLife_master.blend"), compress=True)

    runtime_triangles = optimize_runtime(source_records)
    remove_runtime_non_meshes()
    bpy.ops.wm.save_as_mainfile(filepath=str(root / "runtime" / "CatLife_runtime.blend"), compress=True)
    export_fbx(root / "runtime" / "CL_TWN_Runtime.fbx", source_records)
    write_manifest(root / "runtime" / "asset_manifest.csv", source_records)
    report_pipeline(root, source_records, runtime_triangles)


if __name__ == "__main__":
    main()
