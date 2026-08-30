# CatLife Town Visual Artifact Fix Notes

Date: 2026-06-29

## Current Recommended File

Use this safe visual-only Blender file for further scene work:

`D:\Agent\AIGC innovation\03-3d-models\catlife-town\current\catlife_v2_view_clean_no_merge.blend`

Original imported file preserved:

`D:\Agent\AIGC innovation\03-3d-models\catlife-town\archive\original\catlife_v2.blend`

Intermediate viewport-only copy:

`D:\Agent\AIGC innovation\03-3d-models\catlife-town\archive\intermediate-visual-clean\catlife_v2_visual_clean.blend`

Deprecated mesh-merge copy, do not use:

`D:\Agent\AIGC innovation\03-3d-models\catlife-town\archive\deprecated-merge-attempts\catlife_v2_mesh_clean.blend`

## Problem

The imported town scene showed black thin line / burr artifacts in Blender Solid viewport. The artifacts were strongest around many separated small objects and low-poly detail clusters.

## Likely Causes Found

- Solid viewport object outline was enabled.
- Imported GLB materials were set to hashed/dithered transparency mode.
- Many meshes had non-uniform or negative object scale.
- Mesh cleanup found a large number of near-duplicate vertices.

The mesh-clean pass removed `398527` near-duplicate vertices across `165` mesh objects and applied scale on `159` mesh objects.

## Fix Applied

Safe current fix:

`catlife_v2_view_clean_no_merge.blend`

Applied:

- Disabled viewport object outlines.
- Disabled viewport cavity.
- Disabled viewport wire overlays.
- Set viewport clipping to `0.05 - 500`.
- Disabled transparent shadows and transparent backface display on imported opaque materials.
- Forced material alpha inputs to `1.0` where available.
- Did not merge vertices.
- Did not apply scale.
- Did not change UVs, topology, normals, or object transforms.

Deprecated attempt:

`catlife_v2_mesh_clean.blend` applied scale, merged vertices, and recalculated normals. This caused visible material/UV/hard-edge damage after merge, so it is not a valid final solution.

## QA Images

Viewport/object-outline cleanup:

`D:\Agent\AIGC innovation\03-3d-models\catlife-town\reports\qa-visual-clean\catlife_v2_visual_clean_viewport.png`

Mesh cleanup with Studio lighting:

`D:\Agent\AIGC innovation\03-3d-models\catlife-town\reports\qa-visual-clean\catlife_v2_mesh_clean_viewport.png`

Diagnostic flat-lighting view:

`D:\Agent\AIGC innovation\03-3d-models\catlife-town\reports\qa-visual-clean\catlife_v2_mesh_clean_flat_viewport.png`

## Notes

The Flat lighting view removes most perceived black edge shading but also hides low-poly detail, so it is only a diagnostic view. The recommended working default is `Solid / Studio` with object outline, cavity, wireframe overlay, and face orientation overlay disabled.

If black speckles remain in final engine import, next checks should be:

- Material alpha mode in the target engine.
- Backface culling and shadow settings.
- Z-fighting from coplanar decorative plates or duplicated imported GLB instances.
