# CatLife Final Submission Folder

This folder is the current canonical CatLife competition deliverable folder after the 2026-07-06 migration and cleanup.

## Current Deliverables

- `CatLife_MVP_Android_v0.1.0_release_optimized.apk`
  - Final retained Android APK.
  - Size: about 406 MB.
  - SHA256 from the latest emulator retest: `DD9CCC25C432608642F3902BAD20D7BA1042764BCB9DE40C1D25E3F4D38395C9`.
- `CatLife_core_code_package_20260706.zip`
  - Core code package without large scene/model assets.
  - Highlights BlueLM/API, Unity bridge, prompt, privacy, and local fallback implementation.
- `CatLife_core_code_package_README_20260706.md`
  - Code package explanation and large-model integration notes.
- `CatLife_release_run_tutorial_20260706.md`
  - Installation and run tutorial.
- `CatLife_作品介绍PPT_v1.pptx`
  - Current presentation deck.
- `CatLife_作品海报_v1.png`
  - Current poster.
- `evidence/android/08-current-apk-retest-20260706/`
  - Latest retained emulator retest evidence for the optimized APK.

## Cleanup Policy

- Only one APK is retained in this repository: `CatLife_MVP_Android_v0.1.0_release_optimized.apk`.
- Old 2.8 GB APKs, intermediate Unity build APKs, mapping/debug output, DoNotShip folders, PPT backups, outdated audit reports, and duplicate extracted code-package folders have been removed.
- The C-drive source project contents were deleted after migration to `D:\Agent\AIGC innovation`; only an empty source root directory may remain while held open by a running process.

## Notes

- The repository itself is now located at `D:\Agent\AIGC innovation`.
- The desktop submission folder may still contain a renamed copy named `CatLife.apk`; the repository canonical APK is the optimized release file listed above.
- Do not re-add older APKs or generated Unity build outputs unless a new final build is intentionally produced.
