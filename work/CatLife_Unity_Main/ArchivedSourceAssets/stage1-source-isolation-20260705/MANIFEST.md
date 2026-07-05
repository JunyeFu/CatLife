# CatLife Stage 1 Source Isolation Manifest

Date: 2026-07-05
Unity project: work/CatLife_Unity_Main
Stage: Android size/performance optimization phase 1 - lossless source isolation

## Moved files

| Original Assets path | Archive path | Size bytes | SHA256 before | SHA256 after | Reason |
|---|---|---:|---|---|---|
| Assets/Art/Town/Source/catlife_v2_island_grass_style_no_skybox_20260702.glb | ArchivedSourceAssets/stage1-source-isolation-20260705/Art/Town/Source/catlife_v2_island_grass_style_no_skybox_20260702.glb | 67319640 | A3F8DC0FD90F3DEFBE7BC178118808528B47EE4A8AACFB8F2EE10561FF7C3BFB | A3F8DC0FD90F3DEFBE7BC178118808528B47EE4A8AACFB8F2EE10561FF7C3BFB | Duplicate source GLB; Unity AssetDatabase dependency scan found 0 users in Scenes/Prefabs/Materials/Assets/Controllers. Current MainScene uses 20260702_1.glb instead. |
| Assets/Art/Town/Source/catlife_v2_island_grass_style_no_skybox_20260702.glb.meta | ArchivedSourceAssets/stage1-source-isolation-20260705/Art/Town/Source/catlife_v2_island_grass_style_no_skybox_20260702.glb.meta | 155 | 1510085B6448A04B01FFAC05665E88B5B89EC873AD63DD8DB19636A821AC2EBA | 1510085B6448A04B01FFAC05665E88B5B89EC873AD63DD8DB19636A821AC2EBA | Keep original Unity GUID metadata for rollback. |

## Dependency evidence before move

Unity AssetDatabase.GetDependencies evidence:

- Assets/Scenes/MainScene.unity -> Assets/Art/Town/Source/catlife_v2_island_grass_style_no_skybox_20260702.glb: False
- Assets/Scenes/SampleScene.unity -> Assets/Art/Town/Source/catlife_v2_island_grass_style_no_skybox_20260702.glb: False
- Search roots Assets/Scenes, Assets/Prefabs, Assets/Materials, Assets/Scripts, Assets/Configs: 0 users

Do not move in this phase:

- Assets/Art/Town/Source/catlife_v2_island_grass_style_no_skybox_20260702_1.glb because MainScene depends on it.
- Assets/Art/Town/Source/catlife_v2_island_grass_style_no_skybox_20260630.blend because MainScene depends on it.
- Assets/Art/Town/Source/catlife_v2_island_grass_style_no_skybox_20260630.fbx because MainScene depends on it.

## Rollback

Move both archived files back to their original Assets/Art/Town/Source/ paths, then let Unity reimport.
