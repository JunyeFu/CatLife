param(
    [string]$ProjectRoot = (Resolve-Path "$PSScriptRoot\..\..").Path,
    [string]$OutputName = "CatLife_submission_check_20260705.md"
)

$ErrorActionPreference = "Stop"

$finalDir = Join-Path $ProjectRoot "06-deliverables\final-submission"
$llmTemplateDir = Join-Path $ProjectRoot "06-deliverables\llm-code-package-template"
$outputPath = Join-Path $finalDir $OutputName
$privateConfigRelative = "work/CatLife_Unity_Main/Assets/Resources/CatLifePrivate/vivo_cloud_credentials.json"
$privateConfigPath = Join-Path $ProjectRoot $privateConfigRelative

function New-Result {
    param(
        [string]$Item,
        [string]$Expected,
        [bool]$Passed,
        [string]$Evidence,
        [string]$NextAction
    )

    [pscustomobject]@{
        Item = $Item
        Expected = $Expected
        Passed = $Passed
        Evidence = $Evidence
        NextAction = $NextAction
    }
}

function Find-FirstFile {
    param(
        [string[]]$Patterns,
        [switch]$Recurse
    )

    foreach ($pattern in $Patterns) {
        $match = Get-ChildItem -LiteralPath $finalDir -File -Filter $pattern -Recurse:$Recurse -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1
        if ($match) {
            return $match
        }
    }

    return $null
}

function Test-SubstantiveEvidenceFile {
    param($File)

    if (-not $File) {
        return $false
    }

    if ($File.Length -eq 0) {
        return $false
    }

    $textExtensions = @(".txt", ".md", ".log", ".csv", ".json")
    if ($textExtensions -contains $File.Extension.ToLowerInvariant()) {
        $content = Get-Content -LiteralPath $File.FullName -Raw -ErrorAction SilentlyContinue
        if ([string]::IsNullOrWhiteSpace($content)) {
            return $false
        }
        if ($content -match "\bTODO\b") {
            return $false
        }
        if ($File.Name -eq "apk-sha256.txt" -and $content -match "SHA256:\s*missing") {
            return $false
        }
        if ($File.Name -eq "unity-build-settings.txt" -and $content -match "Unity version:\s*TODO") {
            return $false
        }
    }

    return $true
}

function Find-FirstEvidenceFile {
    param([string[]]$Patterns)

    foreach ($pattern in $Patterns) {
        $matches = Get-ChildItem -LiteralPath $finalDir -File -Filter $pattern -Recurse -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending
        foreach ($match in $matches) {
            if (Test-SubstantiveEvidenceFile $match) {
                return $match
            }
        }
    }

    return $null
}

function Get-Sha256OrBlank {
    param($File)
    if (-not $File) {
        return ""
    }
    return (Get-FileHash -Algorithm SHA256 -LiteralPath $File.FullName).Hash
}

New-Item -ItemType Directory -Force -Path $finalDir | Out-Null

$checks = New-Object System.Collections.Generic.List[object]

$ppt = Find-FirstFile @("CatLife_作品介绍PPT*.pptx", "*.pptx")
$video = Find-FirstFile @("CatLife_作品演示视频*.mp4", "*.mp4")
$poster = Find-FirstFile @("CatLife_作品海报*.png", "*.png", "*.jpg", "*.jpeg")
$apk = Find-FirstFile @("CatLife_MVP_Android*.apk", "*.apk")
$codePackage = Find-FirstFile @("CatLife_大模型调用代码包*.zip", "*代码包*.zip")

$checks.Add((New-Result "PPT" "PPT exists and includes real product screenshots" ([bool]$ppt) ($(if($ppt){$ppt.Name}else{"missing"})) "Add CatLife_presentation_v1.pptx"))
$checks.Add((New-Result "Video" "MP4, target <=3min, hard max <=5min, shows final product/name/UI/features" ([bool]$video) ($(if($video){$video.Name}else{"missing"})) "Add CatLife_demo_video_v1.mp4"))
$checks.Add((New-Result "Poster" "Portrait 70cm x 150cm poster, jpg/jpeg/png, includes title/slogan/visual" ([bool]$poster) ($(if($poster){$poster.Name}else{"missing"})) "Add CatLife_poster_v1.png"))
$checks.Add((New-Result "APK" "Runnable Android APK, installable and launchable on device" ([bool]$apk) ($(if($apk){$apk.Name}else{"missing"})) "Add CatLife_MVP_Android_v0.1.0.apk and adb install evidence"))
$checks.Add((New-Result "Code package" "Large-model code package zip, API call marked, no secrets" ([bool]$codePackage) ($(if($codePackage){$codePackage.Name}else{"missing"})) "Package 06-deliverables/llm-code-package-template after real integration notes"))

$llmTemplateExists = Test-Path -LiteralPath $llmTemplateDir
$checks.Add((New-Result "LLM template" "Large-model code package template exists" $llmTemplateExists ($(if($llmTemplateExists){$llmTemplateDir}else{"missing"})) "Keep template or package it as final code bundle"))

$privateConfigExists = Test-Path -LiteralPath $privateConfigPath
$privateConfigIgnored = $false
try {
    git -C $ProjectRoot check-ignore -q -- $privateConfigRelative
    $privateConfigIgnored = ($LASTEXITCODE -eq 0)
} catch {
    $privateConfigIgnored = $false
}
$privateConfigEvidence = "exists=$privateConfigExists; ignored=$privateConfigIgnored; value=REDACTED"
$checks.Add((New-Result "Private APK credential boundary" "Real APK can include local ignored vivo cloud key, while Git/code package excludes plaintext key" ($privateConfigExists -and $privateConfigIgnored) $privateConfigEvidence "Create local private Resources credential and verify .gitignore before real APK build"))

$secretPatterns = @(
    "sk-[A-Za-z0-9]",
    "api[_-]key\s*[:=]",
    "API_KEY\s*[:=]",
    "secret\s*[:=]",
    "password\s*[:=]",
    "token\s*[:=]",
    "Bearer\s+[A-Za-z0-9_\-]{20,}"
)

$secretHits = New-Object System.Collections.Generic.List[string]
$scanRoots = @($finalDir, $llmTemplateDir) | Where-Object { Test-Path -LiteralPath $_ }
foreach ($root in $scanRoots) {
    Get-ChildItem -LiteralPath $root -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Length -lt 5MB } |
        ForEach-Object {
            foreach ($pattern in $secretPatterns) {
                $hits = Select-String -LiteralPath $_.FullName -Pattern $pattern -CaseSensitive:$false -ErrorAction SilentlyContinue
                foreach ($hit in $hits) {
                    $relative = Resolve-Path -LiteralPath $hit.Path -Relative
                    $secretHits.Add("${relative}:$($hit.LineNumber):$($hit.Line.Trim())")
                }
            }
        }
}

$checks.Add((New-Result "Secret scan" "final-submission and LLM template contain no common secret patterns" ($secretHits.Count -eq 0) ("hits=" + $secretHits.Count) "Review and remove matched text"))

$buildEvidence = Find-FirstEvidenceFile @("*build*.log", "unity-build-settings.txt", "apk-sha256.txt")
$installEvidence = Find-FirstEvidenceFile @("*install*.txt", "*install*.log", "device-info.txt", "adb_devices.txt")
$runtimeEvidence = Find-FirstEvidenceFile @("*logcat*.txt", "*android-runtime*.txt", "smoke-test-notes.md")
$llmEvidence = Find-FirstEvidenceFile @("logcat_vivo_cloud_llm.txt", "logcat_bluelm_init.txt", "logcat_bluelm_generate.txt", "*vivo-cloud-llm-logcat.txt")
$recordingEvidence = Find-FirstEvidenceFile @("*device*.mp4", "*recording*.mp4", "raw-device-recording.mp4", "startup_screenrecord.mp4", "focus_5min_screenrecord.mp4")
$deviceEvidence = if ($installEvidence) { $installEvidence } elseif ($runtimeEvidence) { $runtimeEvidence } else { $null }
$checks.Add((New-Result "Build evidence" "Real build settings/log/hash evidence exists under final-submission/evidence" ([bool]$buildEvidence) ($(if($buildEvidence){$buildEvidence.FullName}else{"missing"})) "Build the APK, then run collect-stage9-android-evidence.ps1 to save build log/settings/hash"))
$checks.Add((New-Result "Android evidence" "Install/runtime/logcat evidence exists" ([bool]$deviceEvidence) ($(if($deviceEvidence){$deviceEvidence.FullName}else{"missing"})) "Save adb install and logcat evidence after device test"))
$checks.Add((New-Result "LLM runtime evidence" "logcat can distinguish vivo_cloud, bluelm_on_device, local_template, failure code, or fallback state" ([bool]$llmEvidence) ($(if($llmEvidence){$llmEvidence.FullName}else{"missing"})) "Run collect-stage9-android-evidence.ps1 after APK install or save cloud-device LLM logcat"))
$checks.Add((New-Result "Recording evidence" "Raw device or cloud-device recording exists under evidence/04-recordings" ([bool]$recordingEvidence) ($(if($recordingEvidence){$recordingEvidence.FullName}else{"missing"})) "Record APK or cloud-device flow before editing final video"))

$hashRows = @()
foreach ($file in @($ppt, $video, $poster, $apk, $codePackage) | Where-Object { $_ }) {
    $hashRows += [pscustomobject]@{
        File = $file.Name
        SizeBytes = $file.Length
        SHA256 = Get-Sha256OrBlank $file
    }
}

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("# CatLife Submission Check")
$lines.Add("")
$lines.Add("Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
$lines.Add("Directory: " + $finalDir)
$lines.Add("")
$lines.Add("## 1. Check Results")
$lines.Add("")
$lines.Add("| Item | Expected | Status | Evidence | Next action |")
$lines.Add("|---|---|---|---|---|")
foreach ($check in $checks) {
    $status = if ($check.Passed) { "PASS" } else { "MISSING" }
    $lines.Add("| $($check.Item) | $($check.Expected) | $status | $($check.Evidence) | $($check.NextAction) |")
}

$lines.Add("")
$lines.Add("## 2. File Hashes")
$lines.Add("")
$lines.Add("| File | Size(bytes) | SHA256 |")
$lines.Add("|---|---:|---|")
if ($hashRows.Count -eq 0) {
    $lines.Add("| No final deliverable files found | 0 |  |")
} else {
    foreach ($row in $hashRows) {
        $lines.Add("| $($row.File) | $($row.SizeBytes) | $($row.SHA256) |")
    }
}

$lines.Add("")
$lines.Add("## 3. Secret Scan")
$lines.Add("")
if ($secretHits.Count -eq 0) {
    $lines.Add("No common secret patterns matched.")
} else {
    $lines.Add("Potential sensitive text found. Review manually:")
    foreach ($hit in $secretHits) {
        $lines.Add("- " + $hit)
    }
}

$lines.Add("")
$lines.Add("## 4. Conclusion")
$lines.Add("")
if (($checks | Where-Object { -not $_.Passed }).Count -eq 0) {
    $lines.Add("All automated checks passed. Manual review is still required for PPT/video/poster readability, APK device flow, and platform upload constraints.")
} else {
    $lines.Add("Missing items remain. The final submission package is not complete.")
}

Set-Content -LiteralPath $outputPath -Value $lines -Encoding UTF8

Write-Host "Wrote $outputPath"
foreach ($check in $checks) {
    $status = if ($check.Passed) { "PASS" } else { "MISSING" }
    Write-Host "$status`t$($check.Item)`t$($check.Evidence)"
}

if (($checks | Where-Object { -not $_.Passed }).Count -gt 0) {
    exit 2
}
