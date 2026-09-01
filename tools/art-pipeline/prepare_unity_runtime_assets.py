import json
import shutil
from pathlib import Path

from PIL import Image


REPO_ROOT = Path(__file__).resolve().parents[2]
PIPELINE_RUNTIME = REPO_ROOT / "03-3d-models" / "catlife-town" / "pipeline" / "runtime"
UNITY_ART = REPO_ROOT / "work" / "CatLife_Unity_Main" / "Assets" / "MobileRuntime" / "Art"
CAT_SOURCE = REPO_ROOT / "work" / "CatLife_Unity_Main" / "Assets" / "Art" / "Cat"
REPORT_PATH = REPO_ROOT / "03-3d-models" / "catlife-town" / "pipeline" / "reports" / "unity_runtime_asset_receipt.json"


def copy_file(source, destination):
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, destination)
    return {
        "source": str(source.relative_to(REPO_ROOT)),
        "destination": str(destination.relative_to(REPO_ROOT)),
        "bytes": destination.stat().st_size,
    }


def resize_texture(source, destination, size, mode=None):
    destination.parent.mkdir(parents=True, exist_ok=True)
    with Image.open(source) as image:
        image.load()
        if mode:
            image = image.convert(mode)
        image.thumbnail((size, size), Image.Resampling.LANCZOS)
        image.save(destination, format="PNG", optimize=True)
        dimensions = list(image.size)
    return {
        "source": str(source.relative_to(REPO_ROOT)),
        "destination": str(destination.relative_to(REPO_ROOT)),
        "width": dimensions[0],
        "height": dimensions[1],
        "bytes": destination.stat().st_size,
    }


def main():
    receipts = []
    receipts.append(
        copy_file(
            PIPELINE_RUNTIME / "CL_TWN_Runtime.fbx",
            UNITY_ART / "Town" / "Source" / "CL_TWN_Runtime.fbx",
        )
    )
    receipts.append(
        copy_file(
            PIPELINE_RUNTIME / "asset_manifest.csv",
            UNITY_ART / "Town" / "Catalog" / "asset_manifest.csv",
        )
    )
    for texture in sorted((PIPELINE_RUNTIME / "textures").glob("*.png")):
        receipts.append(copy_file(texture, UNITY_ART / "Town" / "Textures" / texture.name))

    receipts.append(
        copy_file(
            CAT_SOURCE / "Animations" / "CatLife_cat_10_actions_final_state.fbx",
            UNITY_ART / "Cat" / "Source" / "CL_CAT_Runtime.fbx",
        )
    )
    receipts.append(
        copy_file(
            CAT_SOURCE / "Animations" / "cat_actions_manifest.json",
            UNITY_ART / "Cat" / "Catalog" / "cat_actions_manifest.json",
        )
    )
    receipts.append(
        resize_texture(
            CAT_SOURCE / "Textures" / "Meshy_AI_Low_Poly_Orange_Cat_quadruped_texture_0.png",
            UNITY_ART / "Cat" / "Textures" / "T_CL_Cat_BaseColor_1024.png",
            1024,
            "RGBA",
        )
    )
    receipts.append(
        resize_texture(
            CAT_SOURCE / "Textures" / "Meshy_AI_Low_Poly_Orange_Cat_quadruped_texture_0_normal.png",
            UNITY_ART / "Cat" / "Textures" / "T_CL_Cat_Normal_1024.png",
            1024,
            "RGB",
        )
    )

    REPORT_PATH.parent.mkdir(parents=True, exist_ok=True)
    with REPORT_PATH.open("w", encoding="utf-8") as stream:
        json.dump(
            {
                "policy": "Stable IDs, source paths, versions, dimensions, and sizes; no checksum gate.",
                "files": receipts,
                "total_bytes": sum(receipt["bytes"] for receipt in receipts),
            },
            stream,
            ensure_ascii=False,
            indent=2,
        )
    print(f"Prepared {len(receipts)} Unity runtime files at {UNITY_ART}")


if __name__ == "__main__":
    main()
