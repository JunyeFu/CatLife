import csv
import math
import sys
from pathlib import Path

from PIL import Image


ATLAS_SIZE = 1024
ATLAS_GRID = 8
TILE_SIZE = ATLAS_SIZE // ATLAS_GRID


def main():
    request_path = Path(sys.argv[1])
    output_dir = Path(sys.argv[2])
    manifest_path = Path(sys.argv[3])
    output_dir.mkdir(parents=True, exist_ok=True)

    with request_path.open(newline="", encoding="utf-8-sig") as stream:
        requests = list(csv.DictReader(stream))
    source_images = sorted({row["source_image"] for row in requests}, key=str.casefold)
    locations = {
        source_image: (index // (ATLAS_GRID * ATLAS_GRID), index % (ATLAS_GRID * ATLAS_GRID))
        for index, source_image in enumerate(source_images)
    }
    atlas_count = math.ceil(len(source_images) / (ATLAS_GRID * ATLAS_GRID))

    for old_atlas in output_dir.glob("T_CL_TownAtlas_BaseColor_*_1024.png"):
        old_atlas.unlink()
    atlases = [Image.new("RGBA", (ATLAS_SIZE, ATLAS_SIZE), (255, 255, 255, 255)) for _ in range(atlas_count)]
    for source_image, (atlas_index, tile_index) in locations.items():
        with Image.open(source_image) as image:
            tile = image.convert("RGBA").resize((TILE_SIZE, TILE_SIZE), Image.Resampling.LANCZOS)
        tile_x = tile_index % ATLAS_GRID
        tile_y = tile_index // ATLAS_GRID
        paste_x = tile_x * TILE_SIZE
        paste_y = ATLAS_SIZE - (tile_y + 1) * TILE_SIZE
        atlases[atlas_index].paste(tile, (paste_x, paste_y))

    for atlas_index, atlas in enumerate(atlases, start=1):
        atlas.save(
            output_dir / f"T_CL_TownAtlas_BaseColor_{atlas_index:02d}_1024.png",
            format="PNG",
            optimize=True,
        )

    manifest_rows = []
    for row in requests:
        atlas_index, tile_index = locations[row["source_image"]]
        manifest_rows.append(
            {
                **row,
                "atlas_texture": f"T_CL_TownAtlas_BaseColor_{atlas_index + 1:02d}_1024.png",
                "atlas_material": f"MAT_CL_TownAtlas_{atlas_index + 1:02d}",
                "tile_index": tile_index,
                "tile_x": tile_index % ATLAS_GRID,
                "tile_y": tile_index // ATLAS_GRID,
            }
        )
    fields = [
        "source_object",
        "material_slot",
        "source_material",
        "source_image",
        "atlas_texture",
        "atlas_material",
        "tile_index",
        "tile_x",
        "tile_y",
    ]
    with manifest_path.open("w", newline="", encoding="utf-8-sig") as stream:
        writer = csv.DictWriter(stream, fieldnames=fields)
        writer.writeheader()
        writer.writerows(manifest_rows)
    print(f"CATLIFE_PACKED_ATLASES={atlas_count} source_images={len(source_images)}")


if __name__ == "__main__":
    main()
