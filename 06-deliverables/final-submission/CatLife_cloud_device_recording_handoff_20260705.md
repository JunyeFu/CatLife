# CatLife Cloud Device Recording Handoff

Generated: 2026-07-05 10:03:28
Project root: C:\Users\fujunye\Desktop\Agent\05-AIGC

## Current APK

- APK path: 06-deliverables/final-submission/CatLife_MVP_Android_v0.1.0.apk
- APK exists: True
- APK size bytes: 2803906139
- APK SHA256: 97CA85AC82AF3A875B0D61E782B4E5C9506ABB86EE58E3B645CE6A61321A96B1
- Android package: com.catlife.mvp
- Private credential boundary: the local APK is expected to contain the ignored vivo cloud key for cloud-device recording; public logs/docs must only record redacted credential status.

## Current Local ADB State

- ADB path: D:\UnityEngine\6000.4.9f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe
- ADB devices output:

```text
List of devices attached
```

## Option A: Cloud Device With ADB Endpoint

Run this after the vivo cloud page provides an ADB endpoint:

```powershell
powershell -ExecutionPolicy Bypass -File tools/final-submission/collect-stage9-android-evidence.ps1 -ApkPath "06-deliverables/final-submission/CatLife_MVP_Android_v0.1.0.apk" -CloudAdbEndpoint "<vivo cloud adb ip:port>"
```

If the cloud endpoint is assigned but the adb device is not immediately visible, use the wait wrapper:

```powershell
powershell -ExecutionPolicy Bypass -File tools/final-submission/wait-and-collect-stage9-android-evidence.ps1 -ApkPath "06-deliverables/final-submission/CatLife_MVP_Android_v0.1.0.apk" -CloudAdbEndpoint "<vivo cloud adb ip:port>" -TimeoutSeconds 900
```

Expected outputs:

- evidence/android/01-install/install.log
- evidence/android/02-startup/logcat_startup.txt
- evidence/android/03-llm/logcat_vivo_cloud_llm.txt
- evidence/android/04-focus/logcat_5min_focus.txt
- evidence/android/02-startup/startup_screenrecord.mp4
- evidence/android/04-focus/focus_5min_screenrecord.mp4
- evidence/android/05-summary/stage9_cloud_phone_result.md

## Option B: Cloud Device Web Downloads

Ask the cloud-device workflow to return these files with stable names:

| Required file | Meaning | Import parameter |
|---|---|---|
| install.log | APK install result | -InstallLog install.log |
| device-info.txt | Device model, Android version, ABI, resolution | -DeviceInfo device-info.txt |
| logcat_startup.txt | Startup logcat after launching com.catlife.mvp | -StartupLogcat logcat_startup.txt |
| logcat_vivo_cloud_llm.txt | LLM or fallback source logcat | -LlmLogcat logcat_vivo_cloud_llm.txt |
| logcat_5min_focus.txt | Focus-flow logcat | -FocusLogcat logcat_5min_focus.txt |
| focus_5min_screenrecord.mp4 | Raw device or cloud-device recording | -FocusRecording focus_5min_screenrecord.mp4 |
| launch.png | Launch or splash screenshot | -LaunchScreenshot launch.png |
| town-main.png | Main town screenshot | -TownScreenshot town-main.png |

Import command:

```powershell
powershell -ExecutionPolicy Bypass -File tools/final-submission/import-cloud-device-evidence.ps1 -SourceDir "<folder containing downloaded cloud-device files>" -InstallLog "install.log" -DeviceInfo "device-info.txt" -StartupLogcat "logcat_startup.txt" -LlmLogcat "logcat_vivo_cloud_llm.txt" -FocusLogcat "logcat_5min_focus.txt" -FocusRecording "focus_5min_screenrecord.mp4" -LaunchScreenshot "launch.png" -TownScreenshot "town-main.png"
```

## Post-Import Verification

Run these commands after collecting or importing cloud-device evidence:

```powershell
powershell -ExecutionPolicy Bypass -File tools/final-submission/scan-final-secrets.ps1
powershell -ExecutionPolicy Bypass -File tools/final-submission/check-final-submission.ps1
powershell -ExecutionPolicy Bypass -File tools/final-submission/audit-final-requirements.ps1
```

The final audit can only close after install, startup, LLM/fallback, focus-flow, recording, and final demo video evidence exist.
