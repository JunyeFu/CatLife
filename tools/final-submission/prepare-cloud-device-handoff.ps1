param(
    [string]$ProjectRoot = (Resolve-Path "$PSScriptRoot\..\..").Path,
    [string]$OutputName = "CatLife_cloud_device_recording_handoff_20260705.md",
    [string]$AdbPath = ""
)

$ErrorActionPreference = "Stop"

$finalDir = Join-Path $ProjectRoot "06-deliverables\final-submission"
$outputPath = Join-Path $finalDir $OutputName
$apkPath = Join-Path $finalDir "CatLife_MVP_Android_v0.1.0.apk"
$packageName = "com.catlife.mvp"

function Resolve-AdbExecutable {
    if (-not [string]::IsNullOrWhiteSpace($AdbPath)) {
        return $AdbPath
    }

    $pathAdb = Get-Command adb -ErrorAction SilentlyContinue
    if ($pathAdb) {
        return $pathAdb.Source
    }

    $candidates = @(
        "D:\UnityEngine\6000.4.9f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe",
        "D:\UnityEngine\6000.3.15f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe"
    )
    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }
    }

    return ""
}

function Get-AdbDevicesText {
    param([string]$ResolvedAdb)

    if ([string]::IsNullOrWhiteSpace($ResolvedAdb) -or -not (Test-Path -LiteralPath $ResolvedAdb)) {
        return "adb not found"
    }

    try {
        return (& $ResolvedAdb devices | Out-String).Trim()
    } catch {
        return "adb devices failed: " + $_.Exception.GetType().Name
    }
}

New-Item -ItemType Directory -Force -Path $finalDir | Out-Null

$apkExists = Test-Path -LiteralPath $apkPath
$apkSize = if ($apkExists) { (Get-Item -LiteralPath $apkPath).Length } else { 0 }
$apkHash = if ($apkExists) { (Get-FileHash -Algorithm SHA256 -LiteralPath $apkPath).Hash } else { "missing" }
$adb = Resolve-AdbExecutable
$adbDevices = Get-AdbDevicesText -ResolvedAdb $adb

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("# CatLife Cloud Device Recording Handoff")
$lines.Add("")
$lines.Add("Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
$lines.Add("Project root: $ProjectRoot")
$lines.Add("")
$lines.Add("## Current APK")
$lines.Add("")
$lines.Add("- APK path: 06-deliverables/final-submission/CatLife_MVP_Android_v0.1.0.apk")
$lines.Add("- APK exists: $apkExists")
$lines.Add("- APK size bytes: $apkSize")
$lines.Add("- APK SHA256: $apkHash")
$lines.Add("- Android package: $packageName")
$lines.Add("- Private credential boundary: the local APK is expected to contain the ignored vivo cloud key for cloud-device recording; public logs/docs must only record redacted credential status.")
$lines.Add("")
$lines.Add("## Current Local ADB State")
$lines.Add("")
$lines.Add("- ADB path: " + $(if([string]::IsNullOrWhiteSpace($adb)){"missing"}else{$adb}))
$lines.Add("- ADB devices output:")
$lines.Add("")
$lines.Add('```text')
$lines.Add($adbDevices)
$lines.Add('```')
$lines.Add("")
$lines.Add("## Option A: Cloud Device With ADB Endpoint")
$lines.Add("")
$lines.Add("Run this after the vivo cloud page provides an ADB endpoint:")
$lines.Add("")
$lines.Add('```powershell')
$lines.Add('powershell -ExecutionPolicy Bypass -File tools/final-submission/collect-stage9-android-evidence.ps1 -ApkPath "06-deliverables/final-submission/CatLife_MVP_Android_v0.1.0.apk" -CloudAdbEndpoint "<vivo cloud adb ip:port>"')
$lines.Add('```')
$lines.Add("")
$lines.Add("If the cloud endpoint is assigned but the adb device is not immediately visible, use the wait wrapper:")
$lines.Add("")
$lines.Add('```powershell')
$lines.Add('powershell -ExecutionPolicy Bypass -File tools/final-submission/wait-and-collect-stage9-android-evidence.ps1 -ApkPath "06-deliverables/final-submission/CatLife_MVP_Android_v0.1.0.apk" -CloudAdbEndpoint "<vivo cloud adb ip:port>" -TimeoutSeconds 900')
$lines.Add('```')
$lines.Add("")
$lines.Add("Expected outputs:")
$lines.Add("")
$lines.Add("- evidence/android/01-install/install.log")
$lines.Add("- evidence/android/02-startup/logcat_startup.txt")
$lines.Add("- evidence/android/03-llm/logcat_vivo_cloud_llm.txt")
$lines.Add("- evidence/android/04-focus/logcat_5min_focus.txt")
$lines.Add("- evidence/android/02-startup/startup_screenrecord.mp4")
$lines.Add("- evidence/android/04-focus/focus_5min_screenrecord.mp4")
$lines.Add("- evidence/android/05-summary/stage9_cloud_phone_result.md")
$lines.Add("")
$lines.Add("## Option B: Cloud Device Web Downloads")
$lines.Add("")
$lines.Add("Ask the cloud-device workflow to return these files with stable names:")
$lines.Add("")
$lines.Add('| Required file | Meaning | Import parameter |')
$lines.Add('|---|---|---|')
$lines.Add('| install.log | APK install result | -InstallLog install.log |')
$lines.Add('| device-info.txt | Device model, Android version, ABI, resolution | -DeviceInfo device-info.txt |')
$lines.Add('| logcat_startup.txt | Startup logcat after launching com.catlife.mvp | -StartupLogcat logcat_startup.txt |')
$lines.Add('| logcat_vivo_cloud_llm.txt | LLM or fallback source logcat | -LlmLogcat logcat_vivo_cloud_llm.txt |')
$lines.Add('| logcat_5min_focus.txt | Focus-flow logcat | -FocusLogcat logcat_5min_focus.txt |')
$lines.Add('| focus_5min_screenrecord.mp4 | Raw device or cloud-device recording | -FocusRecording focus_5min_screenrecord.mp4 |')
$lines.Add('| launch.png | Launch or splash screenshot | -LaunchScreenshot launch.png |')
$lines.Add('| town-main.png | Main town screenshot | -TownScreenshot town-main.png |')
$lines.Add("")
$lines.Add("Import command:")
$lines.Add("")
$lines.Add('```powershell')
$lines.Add('powershell -ExecutionPolicy Bypass -File tools/final-submission/import-cloud-device-evidence.ps1 -SourceDir "<folder containing downloaded cloud-device files>" -InstallLog "install.log" -DeviceInfo "device-info.txt" -StartupLogcat "logcat_startup.txt" -LlmLogcat "logcat_vivo_cloud_llm.txt" -FocusLogcat "logcat_5min_focus.txt" -FocusRecording "focus_5min_screenrecord.mp4" -LaunchScreenshot "launch.png" -TownScreenshot "town-main.png"')
$lines.Add('```')
$lines.Add("")
$lines.Add("## Post-Import Verification")
$lines.Add("")
$lines.Add("Run these commands after collecting or importing cloud-device evidence:")
$lines.Add("")
$lines.Add('```powershell')
$lines.Add("powershell -ExecutionPolicy Bypass -File tools/final-submission/scan-final-secrets.ps1")
$lines.Add("powershell -ExecutionPolicy Bypass -File tools/final-submission/check-final-submission.ps1")
$lines.Add("powershell -ExecutionPolicy Bypass -File tools/final-submission/audit-final-requirements.ps1")
$lines.Add('```')
$lines.Add("")
$lines.Add("The final audit can only close after install, startup, LLM/fallback, focus-flow, recording, and final demo video evidence exist.")

Set-Content -LiteralPath $outputPath -Value $lines -Encoding UTF8
Write-Host "Wrote $outputPath"
