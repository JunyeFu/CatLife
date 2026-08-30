# CatLife Final Submission Folder

This folder is the current canonical CatLife competition deliverable folder after the 2026-07-06 migration and cleanup.

## Current Deliverables

- `CatLife_final_submission_manifest_20260830.md`
  - Authoritative five-file submission manifest and current capability boundary.
- `CatLife_MVP_Android_v0.1.0_release_optimized.apk`
  - Final retained Android APK.
  - Size: 425,788,640 bytes.
  - Clean-install, focus/unlock, second-focus, Home, and hot-resume regression passed on 2026-08-30.
- `CatLife_作品演示视频_v1.mp4`
  - Final 2:52.502 demonstration video.
- `CatLife_作品介绍PPT_最终提交版.pptx`
  - Final 23-slide presentation deck. The smaller `CatLife_作品介绍PPT_v1.pptx` is retained as an earlier local version.
- `CatLife_作品海报_最终提交版.pdf`
  - Final PDF poster. `CatLife_作品海报_v1.png` is retained as the image version.
- `CatLife_core_code_package_20260706.zip`
  - Core code package without large scene/model assets.
  - Highlights BlueLM/API, Unity bridge, prompt, privacy, and local fallback implementation.
- `CatLife_core_code_package_README_20260706.md`
  - Code package explanation and large-model integration notes.
- `CatLife_release_run_tutorial_20260706.md`
  - Installation and run tutorial.
- `evidence/android/09-current-regression-20260830/`
  - Current emulator regression screenshots and event summary for the optimized APK.

## Current Capability Boundary

- The final APK currently runs the local template path when the vivo cloud model is unavailable.
- The 2026-08-30 live cloud probe returned HTTP 200 with business error `400 no model access permission`.
- The repository contains the Unity/Java BlueLM bridge skeleton, but no official BlueLM AAR is present and no vivo device was available for this regression. On-device BlueLM is therefore not claimed as a completed runtime path.
- Historical 2026-07-05 evidence of vivo cloud status 200 remains under `evidence/android/08-current-apk-retest-20260706/`; it does not establish current model access.

## Cleanup Policy

- Only one APK is retained in this repository: `CatLife_MVP_Android_v0.1.0_release_optimized.apk`.
- Old 2.8 GB APKs, intermediate Unity build APKs, mapping/debug output, DoNotShip folders, PPT backups, outdated audit reports, and duplicate extracted code-package folders have been removed.
- The C-drive source project contents were deleted after migration to `D:\Agent\AIGC innovation`; only an empty source root directory may remain while held open by a running process.

## Notes

- The repository itself is now located at `D:\Agent\AIGC innovation`.
- The desktop submission folder may still contain a renamed copy named `CatLife.apk`; the repository canonical APK is the optimized release file listed above.
- Do not re-add older APKs or generated Unity build outputs unless a new final build is intentionally produced.
