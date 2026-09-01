# CatLife Art Pipeline

This folder contains the reproducible Blender and Unity-side asset-pipeline tools for the CatLife town rebuild.

The source `.blend`, `.fbx`, `.glb`, and original textures are treated as read-only archives. Scripts write only to the parallel `03-3d-models/catlife-town/pipeline` tree and generated Unity runtime assets.

Asset identity is recorded through stable semantic IDs, source paths, source object names, versions, measured geometry/material budgets, and visual evidence. The pipeline does not use checksums as an acceptance gate.

## Source audit

Run Blender in background mode with the current source scene and the audit script:

```powershell
blender.exe --background <source.blend> --python tools/art-pipeline/audit_source_scene.py -- <report-directory>
```

The audit emits object, material, image, and large-horizontal-object reports without saving the source scene.

Render geometry-safe source evidence without modifying or saving the source scene:

```powershell
blender.exe --background <source.blend> --python tools/art-pipeline/render_source_views.py -- 03-3d-models/catlife-town/pipeline/reports/source-views
```

Audit the complete-scene GLB and every named individual GLB as semantic references:

```powershell
blender.exe --background --factory-startup --python tools/art-pipeline/audit_reference_sources.py -- 03-3d-models/catlife-town/source/catlife_full_scene.glb 03-3d-models/catlife-town/source/individual-glb 03-3d-models/catlife-town/pipeline/reports/source-baseline
```

## Standardized four-stage output

Use a Blender version that can open the current source scene without forward-compatibility loss:

```powershell
blender.exe --background <source.blend> --python tools/art-pipeline/build_standardized_assets.py -- 03-3d-models/catlife-town/pipeline
```

The command creates the standardized master, runtime, and render `.blend` files; the runtime FBX, 1024px landmark textures, shared 1024px mobile atlases, atlas manifest, semantic asset manifest, and matching visual receipts. Ordinary source textures contribute 128px atlas tiles; key landmarks retain dedicated 1024px textures. Original source files are never saved or renamed.

When the current Blender major version is unavailable, start from a factory scene and pass the matching FBX as the second script argument. The audited source `.blend` remains the visual and semantic authority:

```powershell
blender.exe --background --factory-startup --python tools/art-pipeline/build_standardized_assets.py -- 03-3d-models/catlife-town/pipeline 03-3d-models/catlife-town/current/catlife_v2_island_grass_style_no_skybox_20260630.fbx
```

Prepare only the derived town and cat files that Unity may import:

```powershell
py tools/art-pipeline/prepare_unity_runtime_assets.py
```

The promotion render scene links its high-quality source textures from `pipeline/render/textures`, never from the Unity project. That texture directory is a local binary working set and is intentionally ignored by Git.

After the local Unity Personal license is valid, run the Unity import, scene, validation, EditMode, preview, and Release-build gates in order:

```powershell
powershell -ExecutionPolicy Bypass -File tools/art-pipeline/run_unity_hard_gates.ps1
```
