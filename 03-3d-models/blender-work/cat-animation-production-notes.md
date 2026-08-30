# CatLife Cat Animation Production Notes

## Accepted Coordinate Baseline

- Blender source animation uses `+Z` as up.
- Cat head faces `-Y`.
- Cat tail points toward `+Y`.
- `X` is left/right.
- Keep this Blender source convention intact. Engine-specific conversion, such as Unity `Y-up/Z-forward`, should happen at export or import settings rather than by changing the source animation axes.

## Accepted Source Rig

- Working blend file:
  `D:\Agent\AIGC innovation\03-3d-models\blender-work\CatLife_cat_animation_coordinate_corrected.blend`
- Accepted rig objects:
  - `CL_CAT_CORRECTED_Armature`
  - `CL_CAT_CORRECTED_Mesh`
- Accepted animation library marker collection:
  - `CL_CAT_ANIMATION_LIBRARY`

## First Accepted Animation

- Action:
  `CL_CAT_IdleBreath_v06_headsync_loop_108f`
- Frame range:
  `1-109`
- Intent:
  slow idle breathing loop.
- Motion ingredients:
  - body breathing through `Hips` and `chest`
  - head follow-through through `head`
  - subtle tail chain sway through `tail`, `tailstart`, `tail1`, `tail2`, `tail3`
  - tiny leg settling through front/back upper leg bones
- Visual QA:
  - full body:
    `D:\Agent\AIGC innovation\03-3d-models\blender-work\qa-frames\idlebreath-v06-headsync-108f\contact_sheet.png`
  - head close-up:
    `D:\Agent\AIGC innovation\03-3d-models\blender-work\qa-frames\idlebreath-v06-headsync-closeup\contact_sheet.png`

## Lessons Learned

### 1. Do not trust that an Action exists.

Earlier idle versions created Action datablocks but the keyed values were effectively neutral:

- quaternion channels stayed at identity values
- location channels stayed at zero
- Blender could display an Action name, but the visible model did not move

Required check:

- sample key frames numerically after assigning the Action to `CL_CAT_CORRECTED_Armature`
- confirm important pose bones have non-neutral values
- confirm evaluated mesh bounding box changes over the sampled frames

### 2. Use the real bone names.

The corrected rig has no independent `neck` bone. The head chain is:

- `chest`
- `head`
- `headend`
- `earend`
- `R_earend`

Head animation should therefore be authored on `head`, with `chest` providing body support.

Tail animation should use the real chain:

- `tail`
- `tailstart`
- `tail1`
- `tail2`
- `tail3`

### 3. Preserve the accepted idle instead of overwriting it.

When improving an accepted animation, create a new versioned Action:

- `v04`, `v05`, `v06`, etc.

Keep the previous accepted Action available until the user explicitly approves the new one.

### 4. Visual QA must include the right camera.

Full body QA alone may hide small head changes. For subtle actions, produce both:

- full body contact sheet
- focused close-up contact sheet for the moving part

### 5. Slower means longer timing, not bigger motion.

For the accepted idle, slowing down meant:

- keeping the same general pose amplitudes
- extending the loop from the previous 72-frame interval to a 108-frame interval
- adding intermediate keys for smoother interpolation

## Per-Animation Acceptance Checklist

Before asking for acceptance, each new animation must satisfy:

- Action is versioned and does not overwrite the last accepted Action.
- `CL_CAT_CORRECTED_Armature.animation_data.action` is set to the new Action.
- Scene frame range matches the new Action.
- Numerically sampled bones show non-neutral changes at meaningful frames.
- Evaluated mesh bounding box changes across the loop.
- Full body contact sheet is generated.
- Close-up contact sheet is generated when the motion is subtle or localized.
- Blender file is saved after setting the new Action active.

## Second Animation Draft

- Action:
  `CL_CAT_AlertLook_v01_loop_120f`
- Frame range:
  `1-121`
- Intent:
  alert standing look-around loop.
- Status:
  draft ready for user review, not yet accepted.
- Motion ingredients:
  - planted standing body
  - slight alert lift through `Hips` and `chest`
  - left/right head scan through `head`
  - subtle tail counter-sway through the full tail chain
  - tiny foreleg/backleg settling
- Visual QA:
  - full body:
    `D:\Agent\AIGC innovation\03-3d-models\blender-work\qa-frames\alertlook-v01-120f\contact_sheet.png`
  - head close-up:
    `D:\Agent\AIGC innovation\03-3d-models\blender-work\qa-frames\alertlook-v01-120f-closeup\contact_sheet.png`

## Third Accepted Animation

- Action:
  `CL_CAT_PawWave_v01_loop_96f`
- Frame range:
  `1-97`
- Intent:
  subtle standing front-paw wave.
- Status:
  accepted.
- Acceptance note:
  this rig/model does not support a large isolated front-paw lift cleanly. The stronger `CL_CAT_PawWave_v02_loop_96f` attempt reads less naturally, so `v01` is the accepted version.
- Motion ingredients:
  - planted standing body
  - mild front paw wave using the `R_frontleg` chain
  - small head follow-through
  - subtle tail counter motion
- Visual QA:
  - full body:
    `D:\Agent\AIGC innovation\03-3d-models\blender-work\qa-frames\pawwave-v01-96f\contact_sheet.png`

## Fourth Animation Draft

- Action:
  `CL_CAT_TailWagHappy_v01_loop_96f`
- Frame range:
  `1-97`
- Intent:
  happy standing tail wag loop.
- Status:
  accepted.
- Motion ingredients:
  - planted standing body
  - clear left/right sweep through `tail`, `tailstart`, `tail1`, `tail2`, `tail3`
  - small body and head response
  - subtle leg settling
- Visual QA:
  - textured full body:
    `D:\Agent\AIGC innovation\03-3d-models\blender-work\qa-frames\tailwag-v01-96f\contact_sheet.png`
  - textured tail view:
    `D:\Agent\AIGC innovation\03-3d-models\blender-work\qa-frames\tailwag-v01-96f-tailview\contact_sheet.png`

## Fifth Animation Draft

- Action:
  `CL_CAT_CuriousSniff_v02_loop_112f`
- Frame range:
  `1-113`
- Intent:
  curious look-down / light sniff loop.
- Status:
  accepted.
- Iteration note:
  `CL_CAT_CuriousSniff_v01_loop_112f` was too subtle from the front view. `v02` increases chest and head pitch while keeping the paws planted.
- Motion ingredients:
  - planted standing body
  - chest dip through `chest`
  - head down and side-to-side sniff beats through `head`
  - small ear and tail reactions
- Visual QA:
  - textured full body:
    `D:\Agent\AIGC innovation\03-3d-models\blender-work\qa-frames\curioussniff-v02-112f\contact_sheet.png`
  - textured side close-up:
    `D:\Agent\AIGC innovation\03-3d-models\blender-work\qa-frames\curioussniff-v02-112f-sideclose\contact_sheet.png`

## Sixth Animation Draft

- Action:
  `CL_CAT_HeadTiltListen_v01_loop_96f`
- Frame range:
  `1-97`
- Intent:
  curious head-tilt / listening loop.
- Status:
  accepted.
- Motion ingredients:
  - planted standing body
  - readable left/right head tilt through `head`
  - small ear response through `earend` and `R_earend`
  - subtle tail and body follow-through
- Visual QA:
  - textured full body:
    `D:\Agent\AIGC innovation\03-3d-models\blender-work\qa-frames\headtiltlisten-v01-96f\contact_sheet.png`
  - textured head close-up:
    `D:\Agent\AIGC innovation\03-3d-models\blender-work\qa-frames\headtiltlisten-v01-96f-closeup\contact_sheet.png`

## Seventh Animation Draft

- Action:
  `CL_CAT_LookBack_v02_loop_112f`
- Frame range:
  `1-113`
- Intent:
  over-shoulder / look-back loop while the body stays planted.
- Status:
  accepted.
- Iteration note:
  `CL_CAT_LookBack_v01_loop_112f` read too much like a normal side look from the front camera. `v02` increases shoulder, chest, neck, and head rotation so the action reads more clearly as looking back while preserving the accepted `+Z` up, head `-Y`, tail `+Y` coordinate basis.
- Motion ingredients:
  - planted standing body
  - stronger chest and shoulder twist through `chest`
  - clear head over-shoulder turn through `head`
  - small ear response through `earend` and `R_earend`
  - restrained tail counter motion so the silhouette stays stable
- Visual QA:
  - textured full body:
    `D:\Agent\AIGC innovation\03-3d-models\blender-work\qa-frames\lookback-v02-112f\contact_sheet.png`
  - textured rear-side check:
    `D:\Agent\AIGC innovation\03-3d-models\blender-work\qa-frames\lookback-v02-112f-rearview\contact_sheet.png`

## Eighth Animation Draft

- Action:
  `CL_CAT_StretchYawn_v03_slow_loop_264f`
- Frame range:
  `1-265`
- Intent:
  standing stretch / sleepy body stretch loop.
- Status:
  accepted.
- Iteration note:
  `CL_CAT_StretchYawn_v01_loop_132f` used root translation and produced visible height/cropping instability in QA, so it is not the review candidate. `v02` removes root translation and carries the stretch through chest, head, tail, and paired leg rotations for a more stable loop. `v03` retimes `v02` to 0.5x playback speed by scaling the keyframes from `1-133` to `1-265`, with denser QA sampling for review.
- Motion ingredients:
  - planted standing body with no root translation
  - 0.5x playback speed compared with `v02`
  - mild chest and back stretch through `chest`
  - head dips and returns through `head`
  - tail lifts and settles through `tail`, `tailstart`, `tail1`, `tail2`, `tail3`
  - small paired front/back leg response without isolated paw lifting
- Visual QA:
  - textured full body:
    `D:\Agent\AIGC innovation\03-3d-models\blender-work\qa-frames\stretchyawn-v03-slow-264f\contact_sheet.png`
  - textured side check:
    `D:\Agent\AIGC innovation\03-3d-models\blender-work\qa-frames\stretchyawn-v03-slow-264f-side\contact_sheet.png`

## Ninth Animation Draft

- Action:
  `CL_CAT_EarTwitchAlert_v02_loop_120f`
- Frame range:
  `1-121`
- Intent:
  grounded alert ear twitch / scanning loop.
- Status:
  accepted.
- Iteration note:
  `CL_CAT_CrouchPouncePrep_v01_loop_144f` and `CL_CAT_CrouchPouncePrep_v02_loop_144f` are rejected directions. Without IK foot constraints, lowering `Hips.location` makes the cat read as floating or leaving the ground. The corrected direction forbids root translation and uses only head, ear, chest, and tail rotations.
- Motion ingredients:
  - planted standing body with no `Hips.location` animation
  - no leg/root translation, avoiding the previous flying issue
  - readable head scan through `head`
  - stronger alternating ear twitch through `earend` and `R_earend`
  - tense small tail flick through `tailstart`, `tail1`, `tail2`, `tail3`
- Visual QA:
  - textured full body:
    `D:\Agent\AIGC innovation\03-3d-models\blender-work\qa-frames\eartwitchalert-v02-120f\contact_sheet.png`
  - textured side check:
    `D:\Agent\AIGC innovation\03-3d-models\blender-work\qa-frames\eartwitchalert-v02-120f-side\contact_sheet.png`
  - textured close-up:
    `D:\Agent\AIGC innovation\03-3d-models\blender-work\qa-frames\eartwitchalert-v02-120f-closeup\contact_sheet.png`

## Tenth Animation Draft

- Action:
  `CL_CAT_HeadShakeNo_v01_loop_108f`
- Frame range:
  `1-109`
- Intent:
  grounded head-shake / "no" reaction loop.
- Status:
  draft, ready for user review.
- Iteration note:
  Built after the ninth-animation correction. This action keeps the same grounded rule: no `Hips.location`, no leg/root translation, and no pose that depends on foot IK.
- Motion ingredients:
  - planted standing body
  - no root translation
  - alternating head yaw through `head`
  - subtle chest counter-rotation through `chest`
  - ear follow-through through `earend` and `R_earend`
  - small tail counter-swing through `tailstart`, `tail1`, `tail2`, `tail3`
- Visual QA:
  - textured full body:
    `D:\Agent\AIGC innovation\03-3d-models\blender-work\qa-frames\headshakeno-v01-108f\contact_sheet.png`
  - textured side check:
    `D:\Agent\AIGC innovation\03-3d-models\blender-work\qa-frames\headshakeno-v01-108f-side\contact_sheet.png`
  - textured close-up:
    `D:\Agent\AIGC innovation\03-3d-models\blender-work\qa-frames\headshakeno-v01-108f-closeup\contact_sheet.png`
