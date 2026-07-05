param(
    [string]$ProjectRoot = (Resolve-Path "$PSScriptRoot\..\..").Path,
    [string]$OutputDir = "",
    [string]$ManifestName = "CatLife_cloud_device_upload_workspace_manifest_20260705.md"
)

$ErrorActionPreference = "Stop"

$finalDir = Join-Path $ProjectRoot "06-deliverables\final-submission"
$apkPath = Join-Path $finalDir "CatLife_MVP_Android_v0.1.0.apk"
$handoffPath = Join-Path $finalDir "CatLife_cloud_device_recording_handoff_20260705.md"
$hashEvidencePath = Join-Path $finalDir "evidence\android\00-build\apk-sha256.txt"
$privateConfigEvidencePath = Join-Path $finalDir "evidence\android\00-build\private_config_presence_redacted.txt"
$privateConfigRelative = "work/CatLife_Unity_Main/Assets/Resources/CatLifePrivate/vivo_cloud_credentials.json"
$privateConfigPath = Join-Path $ProjectRoot $privateConfigRelative

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $ProjectRoot "work\final-submission-cloud-upload"
}

function Test-GitIgnored {
    param([string]$RelativePath)

    $old = Get-Location
    try {
        Set-Location -LiteralPath $ProjectRoot
        & git check-ignore -q -- $RelativePath
        return ($LASTEXITCODE -eq 0)
    } finally {
        Set-Location $old
    }
}

function Copy-IfExists {
    param(
        [string]$Source,
        [string]$Destination
    )

    if (Test-Path -LiteralPath $Source) {
        Copy-Item -LiteralPath $Source -Destination $Destination -Force
        return $true
    }

    return $false
}

New-Item -ItemType Directory -Force -Path $finalDir | Out-Null
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$handoffScript = Join-Path $ProjectRoot "tools\final-submission\prepare-cloud-device-handoff.ps1"
if (Test-Path -LiteralPath $handoffScript) {
    & powershell -NoProfile -ExecutionPolicy Bypass -File $handoffScript -ProjectRoot $ProjectRoot | Out-Null
}

$apkExists = Test-Path -LiteralPath $apkPath
$apkSize = if ($apkExists) { (Get-Item -LiteralPath $apkPath).Length } else { 0 }
$apkHash = if ($apkExists) { (Get-FileHash -Algorithm SHA256 -LiteralPath $apkPath).Hash } else { "missing" }
$privateConfigExists = Test-Path -LiteralPath $privateConfigPath
$privateConfigIgnored = Test-GitIgnored -RelativePath $privateConfigRelative
$handoffExists = Test-Path -LiteralPath $handoffPath
$hashEvidenceExists = Test-Path -LiteralPath $hashEvidencePath
$privateConfigEvidenceExists = Test-Path -LiteralPath $privateConfigEvidencePath

$readmePath = Join-Path $OutputDir "UPLOAD_README.md"
$sourcePathFile = Join-Path $OutputDir "APK_SOURCE_PATH.txt"
$expectedEvidenceFile = Join-Path $OutputDir "EXPECTED_CLOUD_DEVICE_DOWNLOADS.txt"

$readme = New-Object System.Collections.Generic.List[string]
$readme.Add("# CatLife Cloud Device Upload Workspace")
$readme.Add("")
$readme.Add("Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
$readme.Add("")
$readme.Add("## Upload APK")
$readme.Add("")
$readme.Add("Upload this APK to the vivo cloud-device page:")
$readme.Add("")
$readme.Add('```text')
$readme.Add($apkPath)
$readme.Add('```')
$readme.Add("")
$readme.Add("- APK exists: $apkExists")
$readme.Add("- APK size bytes: $apkSize")
$readme.Add("- APK SHA256: $apkHash")
$readme.Add("- Android package: com.catlife.mvp")
$readme.Add("")
$readme.Add("## Credential Boundary")
$readme.Add("")
$readme.Add("- Private Resources path: $privateConfigRelative")
$readme.Add("- Private Resources exists: $privateConfigExists")
$readme.Add("- Private Resources git ignored: $privateConfigIgnored")
$readme.Add("- Public files must record only REDACTED credential status.")
$readme.Add("- Do not paste AppKEY into cloud-device logs, PPT, video subtitles, GitHub, or code package.")
$readme.Add("")
$readme.Add("## Record Or Download")
$readme.Add("")
$readme.Add("If the cloud page provides an ADB endpoint, use the command in the handoff document.")
$readme.Add("If it only provides web downloads, save the files listed in EXPECTED_CLOUD_DEVICE_DOWNLOADS.txt and import them with import-cloud-device-evidence.ps1.")
$readme.Add("")
$readme.Add("## Included Small Files")
$readme.Add("")
$readme.Add("- CatLife_cloud_device_recording_handoff_20260705.md")
$readme.Add("- apk-sha256.txt")
$readme.Add("- private_config_presence_redacted.txt")
$readme.Add("- EXPECTED_CLOUD_DEVICE_DOWNLOADS.txt")
Set-Content -LiteralPath $readmePath -Value $readme -Encoding UTF8

Set-Content -LiteralPath $sourcePathFile -Value @(
    "APK path: $apkPath",
    "APK exists: $apkExists",
    "APK size bytes: $apkSize",
    "APK SHA256: $apkHash"
) -Encoding UTF8

Set-Content -LiteralPath $expectedEvidenceFile -Value @(
    "install.log",
    "device-info.txt",
    "logcat_startup.txt",
    "logcat_vivo_cloud_llm.txt",
    "logcat_5min_focus.txt",
    "focus_5min_screenrecord.mp4",
    "launch.png",
    "town-main.png"
) -Encoding UTF8

$copiedHandoff = Copy-IfExists -Source $handoffPath -Destination (Join-Path $OutputDir "CatLife_cloud_device_recording_handoff_20260705.md")
$copiedHash = Copy-IfExists -Source $hashEvidencePath -Destination (Join-Path $OutputDir "apk-sha256.txt")
$copiedPrivateEvidence = Copy-IfExists -Source $privateConfigEvidencePath -Destination (Join-Path $OutputDir "private_config_presence_redacted.txt")

$manifestPath = Join-Path $finalDir $ManifestName
$manifest = New-Object System.Collections.Generic.List[string]
$manifest.Add("# CatLife Cloud Device Upload Workspace Manifest")
$manifest.Add("")
$manifest.Add("Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
$manifest.Add("Workspace: $OutputDir")
$manifest.Add("")
$manifest.Add("## APK")
$manifest.Add("")
$manifest.Add("- Path: $apkPath")
$manifest.Add("- Exists: $apkExists")
$manifest.Add("- Size bytes: $apkSize")
$manifest.Add("- SHA256: $apkHash")
$manifest.Add("- Android package: com.catlife.mvp")
$manifest.Add("- APK copied to workspace: False")
$manifest.Add("- Reason: avoid duplicating a multi-GB binary; upload from the canonical final-submission path.")
$manifest.Add("")
$manifest.Add("## Private Credential Boundary")
$manifest.Add("")
$manifest.Add("- Private Resources path: $privateConfigRelative")
$manifest.Add("- Private Resources exists: $privateConfigExists")
$manifest.Add("- Private Resources git ignored: $privateConfigIgnored")
$manifest.Add("- AppKEY value: REDACTED")
$manifest.Add("- Public secret rule: no plaintext AppKEY in GitHub, code package, logs, screenshots, PPT, poster, or video subtitles.")
$manifest.Add("")
$manifest.Add("## Workspace Files")
$manifest.Add("")
$manifest.Add("| File | Present | Purpose |")
$manifest.Add("|---|---:|---|")
$manifest.Add("| UPLOAD_README.md | True | Human upload instructions |")
$manifest.Add("| APK_SOURCE_PATH.txt | True | Canonical APK path and hash |")
$manifest.Add("| EXPECTED_CLOUD_DEVICE_DOWNLOADS.txt | True | Required cloud-device evidence filenames |")
$manifest.Add("| CatLife_cloud_device_recording_handoff_20260705.md | $copiedHandoff | Full handoff command sheet |")
$manifest.Add("| apk-sha256.txt | $copiedHash | Build hash evidence copy |")
$manifest.Add("| private_config_presence_redacted.txt | $copiedPrivateEvidence | Redacted private config evidence copy |")
$manifest.Add("")
$manifest.Add("## Stage 9 Closure")
$manifest.Add("")
$manifest.Add("This workspace does not prove Stage 9 completion. Stage 9 still requires cloud/local device install evidence, startup logcat, LLM/fallback logcat, focus-flow logcat, and recording evidence.")
Set-Content -LiteralPath $manifestPath -Value $manifest -Encoding UTF8

Write-Host "Wrote $readmePath"
Write-Host "Wrote $sourcePathFile"
Write-Host "Wrote $expectedEvidenceFile"
Write-Host "Wrote $manifestPath"
if (-not $apkExists) {
    Write-Warning "APK is missing. Cloud upload workspace is incomplete."
}
if (-not ($privateConfigExists -and $privateConfigIgnored)) {
    Write-Warning "Private credential boundary is incomplete. Do not upload APK until this is resolved."
}
