# CatLife Stage 1 Source Isolation Manifest

Date: 2026-07-05
Unity project: work/CatLife_Unity_Main
Stage: Android size/performance optimization phase 1 - lossless source isolation

## Moved files

| Original Assets path | Archive path | Size bytes | SHA256 before | SHA256 after | Reason |
|---|---|---:|---|---|---|
| Assets/Art/Town/Source/catlife_v2_island_grass_style_no_skybox_20260702.glb | ArchivedSourceAssets/stage1-source-isolation-20260705/Art/Town/Source/catlife_v2_island_grass_style_no_skybox_20260702.glb | 67319640 | A3F8DC0FD90F3DEFBE7BC178118808528B47EE4A8AACFB8F2EE10561FF7C3BFB | A3F8DC0FD90F3DEFBE7BC178118808528B47EE4A8AACFB8F2EE10561FF7C3BFB | Duplicate source GLB; Unity AssetDatabase dependency scan found 0 users in Scenes/Prefabs/Materials/Assets/Controllers. Current MainScene uses 20260702_1.glb instead. |
| Assets/Art/Town/Source/catlife_v2_island_grass_style_no_skybox_20260702.glb.meta | ArchivedSourceAssets/stage1-source-isolation-20260705/Art/Town/Source/catlife_v2_island_grass_style_no_skybox_20260702.glb.meta | 155 | 1510085B6448A04B01FFAC05665E88B5B89EC873AD63DD8DB19636A821AC2EBA | 1510085B6448A04B01FFAC05665E88B5B89EC873AD63DD8DB19636A821AC2EBA | Keep original Unity GUID metadata for rollback. |
| Assets/Art/Cat/Animations/CL_CAT_SRC_Walk_60fps.fbx | ArchivedSourceAssets/stage1-source-isolation-20260705/Art/Cat/Animations/CL_CAT_SRC_Walk_60fps.fbx | 37205372 | 52A84C6FA4B3DB251BFD21F8A897DB230793AD76D9584A5D259FE0D458DC0E73 | 52A84C6FA4B3DB251BFD21F8A897DB230793AD76D9584A5D259FE0D458DC0E73 | Source-only walk FBX; runtime uses generated `Assets/Art/Cat/Animations/Clips/CL_CAT_SRC_Walk_60fps.anim`, and Unity dependency scan found 0 users for the FBX. |
| Assets/Art/Cat/Animations/CL_CAT_SRC_Walk_60fps.fbx.meta | ArchivedSourceAssets/stage1-source-isolation-20260705/Art/Cat/Animations/CL_CAT_SRC_Walk_60fps.fbx.meta | 2861 | C0D25793B2159663C2694B449AC9AAB59A0C29469B5433344D69A4BF3D38E583 | C0D25793B2159663C2694B449AC9AAB59A0C29469B5433344D69A4BF3D38E583 | Keep original Unity GUID metadata for rollback. |

## Dependency evidence before move

Unity AssetDatabase.GetDependencies evidence:

- Assets/Scenes/MainScene.unity -> Assets/Art/Town/Source/catlife_v2_island_grass_style_no_skybox_20260702.glb: False
- Assets/Scenes/SampleScene.unity -> Assets/Art/Town/Source/catlife_v2_island_grass_style_no_skybox_20260702.glb: False
- Search roots Assets/Scenes, Assets/Prefabs, Assets/Materials, Assets/Scripts, Assets/Configs: 0 users
- Assets/Art/Cat/Animations/CL_CAT_SRC_Walk_60fps.fbx: 0 users across Scenes/Prefabs/Materials/Scripts/Configs/Art serializable assets; `CL_CAT_SRC_Walk_60fps.anim` remains the tracked runtime clip.

Do not move in this phase:

- Assets/Art/Town/Source/catlife_v2_island_grass_style_no_skybox_20260702_1.glb because MainScene depends on it.
- Assets/Art/Town/Source/catlife_v2_island_grass_style_no_skybox_20260630.blend because MainScene depends on it.
- Assets/Art/Town/Source/catlife_v2_island_grass_style_no_skybox_20260630.fbx because MainScene depends on it.
- Assets/Art/Cat/Animations/CatLife_cat_10_actions_final_state.fbx because MainScene depends on it.

## Rollback

Move archived files back to their original `Assets/` paths, then let Unity reimport.
