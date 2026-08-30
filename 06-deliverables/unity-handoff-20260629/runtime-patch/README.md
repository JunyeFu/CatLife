# CatLife Unity Runtime Patch

Date: 2026-06-29

This patch records the first validated MVP runtime animation integration.

For the complete Unity asset handoff, prefer:

```text
../mvp-unity-assets/
```

This `runtime-patch/` folder remains a script-level patch reference.

## Targets

Copy these files to the Unity project paths:

```text
Assets/脚本/Cat/CatController.cs
Assets/脚本/Cat/CatAnimationMvpDemo.cs
Assets/脚本/MainSceneManager.cs
```

## Verified Mapping

The updated controller keeps the existing `StateMachine` interface and plays real Animator state names directly through `Animator.CrossFadeInFixedTime`.

| CatLife state | Animator state |
|---|---|
| Normal | `CL_CAT_IdleBreath_v06_headsync_loop_108f` |
| Transition | `CL_CAT_CuriousSniff_v02_loop_112f` |
| Focus | `CL_CAT_IdleBreath_v06_headsync_loop_108f` |
| Reward | `CL_CAT_TailWagHappy_v01_loop_96f` |

Random Normal-state actions:

- `CL_CAT_AlertLook_v01_loop_120f`
- `CL_CAT_PawWave_v01_loop_96f`
- `CL_CAT_HeadTiltListen_v01_loop_96f`
- `CL_CAT_LookBack_v02_loop_112f`
- `CL_CAT_EarTwitchAlert_v02_loop_120f`

## Main Scene Integration

The validated `mainscene` setup uses:

- `CatModel_AnimatedMVP`: instantiated from `Assets/Art/Cat/Animations/CatLife_cat_10_actions_final_state.fbx`
- Animator Controller: `Assets/Art/Cat/Animations/CatLife_10Actions_Validation.controller`
- Temporary MVP material: orange URP/Standard material named `CatLife_OrangeCat_MVP`
- `CatLifeRuntime`: scene object with `StateMachine` and `CatAnimationMvpDemo`
- `MainSceneController`: patched to start its sequence only on `Transition` and to cross-fade to `CL_CAT_CuriousSniff_v02_loop_112f`

The original static `CatModel` was renamed to `CatModel_LegacyStatic` and disabled in the validation scene, so the change remains reversible.

## Validation Evidence

Validated in:

```text
D:\Agent\AIGC innovation\work\unity-import-validation-20260629
```

Unity MCP checks:

- MCP connected to Unity `6000.4.9f1` on `127.0.0.1:8080`.
- `Cat` GameObject has `CatController` and `Animator`.
- `CatLife_10Actions_Validation.controller` is assigned to the `Cat` Animator.
- Animator `HasState` passed for all serialized state fields.
- `mainscene` contains visible animated cat object `CatModel_AnimatedMVP`.
- Play Mode reached `Reward` state after the demo sequence and reported 0 runtime errors after refresh.
- Game View screenshot evidence: `../qa-screenshots/catlife-mvp-main-gameview.png`.

## Notes

The validation Animator Controller, FBX import `.meta`, orange material, and saved scene object wiring are local validation assets because the FBX package is treated as a large local artifact. Promote them into the final Unity project together after visual playback review.
