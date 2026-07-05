# CatLife APK Smoke Test Notes

| Case | Result | Evidence |
|---|---|---|
| Build artifact exists | PASS | `evidence/android/00-build/apk-sha256.txt` |
| Private APK credential boundary | PASS | `evidence/android/00-build/private_config_presence_redacted.txt` |
| Install | MISSING | Expected `evidence/android/01-install/install.log` |
| Launch | MISSING | Expected `evidence/android/02-startup/logcat_startup.txt` plus optional `evidence/03-screenshots/launch.png` |
| Main town visible | PARTIAL | Unity/Editor screenshots exist under `evidence/03-screenshots/`; device/cloud screenshot still optional but not captured |
| Normal state | MISSING | Expected device/cloud recording under `evidence/android/04-focus/` or `evidence/04-recordings/` |
| Transition state | MISSING | Expected final demo video or raw device/cloud recording |
| Focus state | MISSING | Expected focus-flow logcat and recording |
| Reward state | NOT_CLAIMED | Do not claim reward flow until a recorded APK path proves it |
| 5 minute stability | MISSING | Expected `evidence/android/04-focus/logcat_5min_focus.txt` |

## Current closure rule

The smoke test is not complete until install, launch, LLM/fallback, focus-flow, and recording evidence are imported from a real Android device or vivo cloud device.
