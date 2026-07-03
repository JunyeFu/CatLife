# CatLife Unity Project Structure

This Unity workspace keeps runtime code, generated assets, editor tools, and validation evidence separated so CatLife can be rebuilt and audited without relying on Unity scene memory.

## Root Directories

| Path | Purpose | Rules |
|---|---|---|
| `Assets/Art/` | Runtime art assets grouped by domain. | Keep source manifests here. Large source binaries remain local unless explicitly tracked or moved to LFS. |
| `Assets/Art/Cat/` | Cat model, textures, materials, animator controller, extracted animation clips. | Runtime clips live under `Animations/Clips/`; controller lives under `Animator/`. |
| `Assets/Art/Town/` | Town source references, generated material bindings, extracted texture support. | The active scene instance is in `MainScene`; source assets stay out of scene-specific folders. |
| `Assets/Editor/` | Editor-only setup, extraction, validation, and automation tools. | Menu tools use `CatLife/Runtime/...` paths and must be safe to rerun. |
| `Assets/Materials/` | Generated or shared materials not owned by a single art domain. | Keep generated town material sets in named subfolders. |
| `Assets/Prefabs/` | Reusable prefabs. | Prefer prefabs for durable reusable scene objects; scene-only runtime roots may remain in scenes. |
| `Assets/Scenes/` | Unity scene assets. | `MainScene.unity` is the current runtime assembly scene. |
| `Assets/Screenshots/` | Curated visual evidence only. | Do not store temporary screenshots here. |
| `Assets/Scripts/` | Runtime C# scripts split by domain. | No editor-only code here; editor-only code belongs in `Assets/Editor/`. |
| `Assets/Settings/` | URP and Unity settings assets. | Project settings and render pipeline assets stay here. |
| `Assets/UI/` | UI sprite/font/layout assets. | Runtime UI behavior scripts stay in `Assets/Scripts/UI/`. |

## Runtime Script Domains

| Path | Purpose |
|---|---|
| `Assets/Scripts/Camera/` | Camera orbit and plaza camera controls. |
| `Assets/Scripts/Cat/` | Cat navigation, behavior driver, animation control, action routing, NavMesh safety. |
| `Assets/Scripts/Core/` | Shared runtime primitives. |
| `Assets/Scripts/LLM/` | Prompt context, prompt builder, model-client interface, safe mock client. |
| `Assets/Scripts/Recognition/` | Local privacy-preserving recognition snapshots and realtime feature windows. |
| `Assets/Scripts/UI/` | CatLife home UI and interaction widgets. |

## Validation Gates

Run these Unity menu items before committing scene/runtime changes:

1. `CatLife/Runtime/Validate Runtime Assembly`
   - Checks scene wiring, NavMesh roots, cat components, recognition/LLM systems, UI binding, and animator states.
2. `CatLife/Runtime/Validate Play Mode Behavior Smoke`
   - Run in Play Mode. Checks NavMesh runtime, safety guard, realtime recognition features, prompt context, and animation state responsiveness.

For command-line or handoff validation, run the Edit Mode gate through Unity batchmode:

```powershell
Unity.exe -batchmode -quit -projectPath "C:\Users\fujunye\Desktop\Agent\05-AIGC\work\CatLife_Unity_Main" -executeMethod CatLife.EditorTools.CatLifeBatchValidationRunner.RunEditModeValidationAndExit
```

The batch entrypoint exits with `0` on PASS and `1` on FAIL. The Play Mode smoke gate still requires an interactive Editor or an automation harness that can enter Play Mode first.

## Import And Storage Rules

- Do not place `.blend`, `.fbx`, `.glb`, `.mp4`, `.zip`, or other large source binaries directly under `Assets/`.
- Keep generated local logs under ignored folders such as `Assets/UnityMCP/Log/`.
- Keep temporary screenshots outside the Unity project, or move only curated evidence into `Assets/Screenshots/`.
- Preserve `.meta` files for tracked Unity assets.
- Prefer rerunnable `Assets/Editor/` setup tools over manual scene-only changes when a change affects runtime wiring.
