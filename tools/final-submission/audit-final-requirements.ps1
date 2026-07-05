param(
    [string]$ProjectRoot = (Resolve-Path "$PSScriptRoot\..\..").Path,
    [string]$OutputName = "CatLife_final_requirements_audit_20260705.md"
)

$ErrorActionPreference = "Stop"

$finalDir = Join-Path $ProjectRoot "06-deliverables\final-submission"
$planningDir = Join-Path $ProjectRoot "08-handoff-docs\planning"
$techSpecDir = Join-Path $ProjectRoot "07-tech-specs"
$outputPath = Join-Path $finalDir $OutputName

function Find-FirstFile {
    param(
        [string]$Root,
        [string[]]$Patterns,
        [switch]$Recurse
    )

    foreach ($pattern in $Patterns) {
        $match = Get-ChildItem -LiteralPath $Root -File -Filter $pattern -Recurse:$Recurse -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1
        if ($match) {
            return $match
        }
    }

    return $null
}

function Test-TextFileHasSignal {
    param(
        [string]$Path,
        [string]$Pattern
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return $false
    }

    $content = Get-Content -LiteralPath $Path -Raw -ErrorAction SilentlyContinue
    if ([string]::IsNullOrWhiteSpace($content)) {
        return $false
    }

    return ($content -match $Pattern)
}

function Test-GitIgnored {
    param([string]$RelativePath)

    try {
        git -C $ProjectRoot check-ignore -q -- $RelativePath
        return ($LASTEXITCODE -eq 0)
    } catch {
        return $false
    }
}

function Get-FirstRegexGroup {
    param(
        [string]$Path,
        [string]$Pattern
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return $null
    }

    $content = Get-Content -LiteralPath $Path -Raw -ErrorAction SilentlyContinue
    if ([string]::IsNullOrWhiteSpace($content)) {
        return $null
    }

    $match = [regex]::Match($content, $Pattern)
    if (-not $match.Success -or $match.Groups.Count -lt 2) {
        return $null
    }

    return $match.Groups[1].Value
}

function Test-PptAuditHasUserValidationHit {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return $false
    }

    $content = Get-Content -LiteralPath $Path -Raw -ErrorAction SilentlyContinue
    if ([string]::IsNullOrWhiteSpace($content)) {
        return $false
    }

    $patterns = @(
        "User validation claims need real anonymized evidence",
        "user_validation_completed_claim",
        "user\s+validation\s+completed",
        "survey\s+results",
        "interview\s+results"
    )

    foreach ($pattern in $patterns) {
        if ($content -match $pattern) {
            return $true
        }
    }

    return $false
}

function New-AuditRow {
    param(
        [string]$Area,
        [string]$Requirement,
        [string]$Status,
        [string]$Evidence,
        [string]$NextAction
    )

    [pscustomobject]@{
        Area = $Area
        Requirement = $Requirement
        Status = $Status
        Evidence = $Evidence
        NextAction = $NextAction
    }
}

function Add-AuditRow {
    param($Row)
    $script:rows.Add($Row) | Out-Null
}

New-Item -ItemType Directory -Force -Path $finalDir | Out-Null

$ppt = Find-FirstFile -Root $finalDir -Patterns @("*.pptx")
$pptManifest = Find-FirstFile -Root $finalDir -Patterns @("*PPT_manifest*.md", "*ppt_manifest*.md")
$video = Find-FirstFile -Root $finalDir -Patterns @("*.mp4")
$videoManifest = Find-FirstFile -Root $finalDir -Patterns @("*video_manifest*.md")
$poster = Find-FirstFile -Root $finalDir -Patterns @("*.png", "*.jpg", "*.jpeg")
$posterManifest = Find-FirstFile -Root $finalDir -Patterns @("*poster_manifest*.md")
$apk = Find-FirstFile -Root $finalDir -Patterns @("*.apk")
$codePackage = Find-FirstFile -Root $finalDir -Patterns @("*code_package*.zip", "*.zip")
$llmManifest = Find-FirstFile -Root $finalDir -Patterns @("*LLM_code_package_manifest*.md", "*code_package_manifest*.md")
$submissionCheck = Find-FirstFile -Root $finalDir -Patterns @("CatLife_submission_check_*.md")
$pptClaimAudit = Find-FirstFile -Root $finalDir -Patterns @("CatLife_PPT_claim_audit_*.md")
$pptClaimPatch = Find-FirstFile -Root $finalDir -Patterns @("CatLife_PPT_claim_patch_*.md")
$secretScanReport = Find-FirstFile -Root $finalDir -Patterns @("CatLife_public_secret_scan_*.md")
$cloudDeviceHandoff = Find-FirstFile -Root $finalDir -Patterns @("CatLife_cloud_device_recording_handoff_*.md")
$pptDefectTableFile = Find-FirstFile -Root $planningDir -Patterns @("*PPT*20260705.md")
$reviewChecklistFile = Find-FirstFile -Root $planningDir -Patterns @("*review*check*.md", "*checklist*.md")
$runbookFile = Find-FirstFile -Root $planningDir -Patterns @("*release*runbook*.md", "*runbook*.md")
$androidPlanFile = Find-FirstFile -Root $techSpecDir -Patterns @("*Android*.md")
$pptDefectTable = if ($pptDefectTableFile) { $pptDefectTableFile.FullName } else { "" }
$reviewChecklist = if ($reviewChecklistFile) { $reviewChecklistFile.FullName } else { "" }
$runbook = if ($runbookFile) { $runbookFile.FullName } else { "" }
$androidPlan = if ($androidPlanFile) { $androidPlanFile.FullName } else { "" }
$userValidationEvidence = Find-FirstFile -Root $finalDir -Patterns @("*user*feedback*.md", "*user*validation*.md", "*interview*.md", "*survey*.md") -Recurse
$privateConfigRelative = "work/CatLife_Unity_Main/Assets/Resources/CatLifePrivate/vivo_cloud_credentials.json"
$privateConfigPath = Join-Path $ProjectRoot $privateConfigRelative
$privateConfigEvidence = Join-Path $finalDir "evidence\android\00-build\private_config_presence_redacted.txt"
$apkHashEvidence = Join-Path $finalDir "evidence\android\00-build\apk-sha256.txt"
$installEvidence = Join-Path $finalDir "evidence\android\01-install\install.log"
$startupEvidence = Join-Path $finalDir "evidence\android\02-startup\logcat_startup.txt"
$llmEvidence = Join-Path $finalDir "evidence\android\03-llm\logcat_vivo_cloud_llm.txt"
$focusEvidence = Join-Path $finalDir "evidence\android\04-focus\logcat_5min_focus.txt"
$recordingEvidence = @(
    (Join-Path $finalDir "evidence\android\02-startup\startup_screenrecord.mp4"),
    (Join-Path $finalDir "evidence\android\04-focus\focus_5min_screenrecord.mp4"),
    (Join-Path $finalDir "evidence\04-recordings\raw-device-recording.mp4")
) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1

$script:rows = New-Object System.Collections.Generic.List[object]

Add-AuditRow (New-AuditRow "Official deliverable" "PPT exists and has tracked manifest." ($(if($ppt -and $pptManifest){"PASS"}elseif($ppt){"PARTIAL"}else{"MISSING"})) ($(if($ppt){$ppt.Name}else{"missing"}) + $(if($pptManifest){"; manifest=" + $pptManifest.Name}else{"; manifest=missing"})) "Keep PPT manifest and complete manual content review.")

Add-AuditRow (New-AuditRow "Official deliverable" "Demo video exists and has video QA manifest." ($(if($video -and $videoManifest){"PASS"}elseif($video -or $videoManifest){"PARTIAL"}else{"MISSING"})) ($(if($video){$video.Name}else{"video=missing"}) + $(if($videoManifest){"; manifest=" + $videoManifest.Name}else{"; manifest=missing"})) "Add final demo MP4 and rerun test-final-video.ps1.")

Add-AuditRow (New-AuditRow "Official deliverable" "Poster exists, is tracked by manifest, and stays local binary." ($(if($poster -and $posterManifest){"PASS"}elseif($poster){"PARTIAL"}else{"MISSING"})) ($(if($poster){$poster.Name}else{"poster=missing"}) + $(if($posterManifest){"; manifest=" + $posterManifest.Name}else{"; manifest=missing"})) "Manual upload preview readability remains required.")

Add-AuditRow (New-AuditRow "Official deliverable" "Runnable product APK exists and has build hash evidence." ($(if($apk -and (Test-Path -LiteralPath $apkHashEvidence)){"PASS"}elseif($apk){"PARTIAL"}else{"MISSING"})) ($(if($apk){$apk.Name}else{"apk=missing"}) + $(if(Test-Path -LiteralPath $apkHashEvidence){"; hash evidence present"}else{"; hash evidence missing"})) "Complete install, startup, LLM, focus, and recording evidence.")

Add-AuditRow (New-AuditRow "Official deliverable" "Large-model code package exists and has manifest." ($(if($codePackage -and $llmManifest){"PASS"}elseif($codePackage){"PARTIAL"}else{"MISSING"})) ($(if($codePackage){$codePackage.Name}else{"code package=missing"}) + $(if($llmManifest){"; manifest=" + $llmManifest.Name}else{"; manifest=missing"})) "Rerun package-llm-code.ps1 after any LLM code changes.")

$privateBoundaryOk = (Test-Path -LiteralPath $privateConfigPath) -and (Test-GitIgnored $privateConfigRelative) -and (Test-Path -LiteralPath $privateConfigEvidence)
Add-AuditRow (New-AuditRow "Credential boundary" "Real APK must include local ignored vivo key, while public materials only keep redacted evidence." ($(if($privateBoundaryOk){"PASS"}else{"MISSING"})) ("private exists=" + (Test-Path -LiteralPath $privateConfigPath) + "; ignored=" + (Test-GitIgnored $privateConfigRelative) + "; redacted evidence=" + (Test-Path -LiteralPath $privateConfigEvidence)) "Keep private Resources ignored; never commit plaintext AppKEY.")

$installOk = Test-TextFileHasSignal -Path $installEvidence -Pattern "Success|INSTALL_SUCCEEDED|installed|安装成功"
$startupOk = Test-TextFileHasSignal -Path $startupEvidence -Pattern "CatLife|Unity|Activity|com\.catlife\.mvp"
$llmOk = Test-TextFileHasSignal -Path $llmEvidence -Pattern "vivo_cloud|bluelm_on_device|local_template|fallback|llm_source|llm_error|BlueLM|LLM"
$focusOk = Test-TextFileHasSignal -Path $focusEvidence -Pattern "CatLife|focus|llm_source|Unity|fallback"
$recordingOk = $null -ne $recordingEvidence

Add-AuditRow (New-AuditRow "Runtime evidence" "APK install evidence proves cloud/local device installation." ($(if($installOk){"PASS"}else{"MISSING"})) ($(if(Test-Path -LiteralPath $installEvidence){$installEvidence}else{"install evidence missing"})) "Install on vivo cloud device or import cloud-device install log.")
Add-AuditRow (New-AuditRow "Runtime evidence" "Startup logcat proves the app launches on device." ($(if($startupOk){"PASS"}else{"MISSING"})) ($(if(Test-Path -LiteralPath $startupEvidence){$startupEvidence}else{"startup logcat missing"})) "Capture startup logcat with collect-stage9-android-evidence.ps1 or import-cloud-device-evidence.ps1.")
Add-AuditRow (New-AuditRow "Runtime evidence" "LLM evidence proves vivo cloud, BlueLM, or fallback source." ($(if($llmOk){"PASS"}else{"MISSING"})) ($(if(Test-Path -LiteralPath $llmEvidence){$llmEvidence}else{"LLM logcat missing"})) "Capture LLM logcat showing vivo_cloud, bluelm_on_device, local_template, or failure/fallback state.")
Add-AuditRow (New-AuditRow "Runtime evidence" "Focus flow evidence proves a sustained focus session path." ($(if($focusOk){"PASS"}else{"MISSING"})) ($(if(Test-Path -LiteralPath $focusEvidence){$focusEvidence}else{"focus logcat missing"})) "Capture 5 minute focus flow logcat or import cloud-device focus evidence.")
Add-AuditRow (New-AuditRow "Runtime evidence" "Recording evidence exists for APK or cloud-device flow." ($(if($recordingOk){"PASS"}else{"MISSING"})) ($(if($recordingOk){$recordingEvidence}else{"recording missing"})) "Record cloud-device or APK flow before editing final demo video.")

$pptClaimAuditPath = if ($pptClaimAudit) { $pptClaimAudit.FullName } else { "" }
$pptHighHitsText = Get-FirstRegexGroup -Path $pptClaimAuditPath -Pattern 'High-risk hits:\s*`?(\d+)'
$pptMediumHitsText = Get-FirstRegexGroup -Path $pptClaimAuditPath -Pattern 'Medium-risk hits:\s*`?(\d+)'
$pptManualHitsText = Get-FirstRegexGroup -Path $pptClaimAuditPath -Pattern 'Manual-review hits:\s*`?(\d+)'
$pptHighHits = if ($null -ne $pptHighHitsText) { [int]$pptHighHitsText } else { -1 }
$pptMediumHits = if ($null -ne $pptMediumHitsText) { [int]$pptMediumHitsText } else { -1 }
$pptManualHits = if ($null -ne $pptManualHitsText) { [int]$pptManualHitsText } else { -1 }
$pptClaimAuditStatus = if (-not $pptClaimAudit) {
    "MISSING"
} elseif ($pptHighHits -gt 0) {
    "MISSING"
} elseif ($pptMediumHits -gt 0 -or $pptManualHits -gt 0) {
    "MANUAL_REVIEW"
} else {
    "PASS"
}
$pptClaimAuditEvidence = if ($pptClaimAudit) {
    $pptClaimAudit.FullName + "; high=" + $pptHighHits + "; medium=" + $pptMediumHits + "; manual=" + $pptManualHits
} else {
    "PPT claim audit missing"
}
Add-AuditRow (New-AuditRow "PPT claim alignment" "PPT extractable text has been audited for current-scope overclaims." $pptClaimAuditStatus $pptClaimAuditEvidence "Run audit-ppt-claims.ps1 -AllowHits; resolve high hits before upload and manually review medium/manual hits.")

$pptClaimPatchPath = if ($pptClaimPatch) { $pptClaimPatch.FullName } else { "" }
$pptPatchHasForestScope = Test-TextFileHasSignal -Path $pptClaimPatchPath -Pattern "historical concept only|Remove forest-scene wording"
$pptPatchHasLlmScope = Test-TextFileHasSignal -Path $pptClaimPatchPath -Pattern "Reduce LLM wording|high-level behavior bias"
$forestScopeStatus = if ($pptClaimAuditStatus -eq "PASS" -and $pptPatchHasForestScope) {
    "PASS"
} elseif (Test-Path -LiteralPath $pptDefectTable) {
    "MANUAL_REVIEW"
} else {
    "MISSING"
}
$forestScopeEvidence = if ($forestScopeStatus -eq "PASS") {
    "claim audit PASS; patch=" + $pptClaimPatchPath
} elseif (Test-Path -LiteralPath $pptDefectTable) {
    $pptDefectTable
} else {
    "PPT defect table or patch report missing"
}
$forestScopeNext = if ($forestScopeStatus -eq "PASS") {
    "Keep forest-related material labeled as historical/concept only; rerun audit-ppt-claims.ps1 after any PPT edits."
} else {
    "Review the final PPT against the defect table; forest visuals must be historical/concept only, not current engineering scope."
}
Add-AuditRow (New-AuditRow "PPT claim alignment" "No forest scene is required by the current product rule." $forestScopeStatus $forestScopeEvidence $forestScopeNext)

$llmWordingStatus = if ($pptClaimAuditStatus -eq "PASS" -and $pptPatchHasLlmScope) {
    "PASS"
} elseif (Test-Path -LiteralPath $pptDefectTable) {
    "MANUAL_REVIEW"
} else {
    "MISSING"
}
$llmWordingEvidence = if ($llmWordingStatus -eq "PASS") {
    "claim audit PASS; patch=" + $pptClaimPatchPath
} elseif (Test-Path -LiteralPath $pptDefectTable) {
    $pptDefectTable
} else {
    "PPT defect table or patch report missing"
}
$llmWordingNext = if ($llmWordingStatus -eq "PASS") {
    "Keep LLM wording as behavior bias or suggestion; rerun audit-ppt-claims.ps1 after any PPT edits."
} else {
    "Review the final PPT manually against the defect table before upload."
}
Add-AuditRow (New-AuditRow "PPT claim alignment" "PPT wording must not claim completed BlueLM on-device SDK or true Android behavior recognition before evidence exists." $llmWordingStatus $llmWordingEvidence $llmWordingNext)

$pptClaimAuditFile = if ($pptClaimAudit) { $pptClaimAudit.FullName } else { "" }
$userValidationClaimed = Test-PptAuditHasUserValidationHit -Path $pptClaimAuditFile
$userValidationStatus = if ($userValidationEvidence) {
    "MANUAL_REVIEW"
} elseif ($userValidationClaimed) {
    "MISSING"
} else {
    "PASS"
}
$userValidationEvidenceText = if ($userValidationEvidence) {
    $userValidationEvidence.FullName
} elseif ($userValidationClaimed) {
    "completed user-validation claim found without evidence"
} else {
    "no completed user-validation claim found in extracted PPT text"
}
$userValidationNextAction = if ($userValidationEvidence) {
    "Review anonymized user validation evidence before upload."
} elseif ($userValidationClaimed) {
    "Add anonymized user feedback summary or remove completed user-validation wording."
} else {
    "Keep PPT wording as planned/future validation unless real anonymized feedback is added."
}
Add-AuditRow (New-AuditRow "PPT claim alignment" "User validation data is either evidenced or not claimed as completed." $userValidationStatus $userValidationEvidenceText $userValidationNextAction)

$secretScanPath = if ($secretScanReport) { $secretScanReport.FullName } else { "" }
$secretScanStatusText = Get-FirstRegexGroup -Path $secretScanPath -Pattern 'Status:\s+([A-Z]+)'
$secretScanHitsText = Get-FirstRegexGroup -Path $secretScanPath -Pattern 'Hits:\s+(\d+)'
$secretScanHits = if ($null -ne $secretScanHitsText) { [int]$secretScanHitsText } else { -1 }
$secretScanStatus = if (-not $secretScanReport) {
    "MANUAL_REVIEW"
} elseif ($secretScanStatusText -eq "PASS" -and $secretScanHits -eq 0) {
    "PASS"
} else {
    "MISSING"
}
$secretScanEvidence = if ($secretScanReport) {
    $secretScanReport.FullName + "; status=" + $secretScanStatusText + "; hits=" + $secretScanHits
} else {
    "public secret scan report missing"
}
$secretScanNextAction = if ($secretScanStatus -eq "PASS") {
    "Rerun scan-final-secrets.ps1 after any final material changes."
} else {
    "Run scan-final-secrets.ps1 and remove or redact any public secret hits."
}
Add-AuditRow (New-AuditRow "Security" "Tracked final docs and scripts have no obvious plaintext AppKEY or bearer token." $secretScanStatus $secretScanEvidence $secretScanNextAction)

$missingCount = @($rows | Where-Object { $_.Status -eq "MISSING" }).Count
$partialCount = @($rows | Where-Object { $_.Status -eq "PARTIAL" }).Count
$manualCount = @($rows | Where-Object { $_.Status -eq "MANUAL_REVIEW" }).Count

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("# CatLife Final Requirements Audit")
$lines.Add("")
$lines.Add("Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
$lines.Add("Project root: $ProjectRoot")
$lines.Add("")
$lines.Add("## Summary")
$lines.Add("")
$lines.Add("- PASS: " + @($rows | Where-Object { $_.Status -eq "PASS" }).Count)
$lines.Add("- PARTIAL: $partialCount")
$lines.Add("- MISSING: $missingCount")
$lines.Add("- MANUAL_REVIEW: $manualCount")
$lines.Add("")
if ($missingCount -eq 0 -and $partialCount -eq 0) {
    $lines.Add("Automated evidence has no missing or partial rows. Manual review rows must still be closed before final submission.")
} else {
    $lines.Add("Final submission is not complete. Missing or partial evidence remains.")
}
$lines.Add("")
$lines.Add("## Audit Rows")
$lines.Add("")
$lines.Add("| Area | Requirement | Status | Evidence | Next action |")
$lines.Add("|---|---|---|---|---|")
foreach ($row in $rows) {
    $evidence = ($row.Evidence -replace "\|", "/")
    $next = ($row.NextAction -replace "\|", "/")
    $lines.Add("| $($row.Area) | $($row.Requirement) | $($row.Status) | $evidence | $next |")
}

$lines.Add("")
$lines.Add("## Source Documents")
$lines.Add("")
$lines.Add("- Final submission check: " + $(if($submissionCheck){$submissionCheck.FullName}else{"missing"}))
$lines.Add("- PPT claim audit: " + $(if($pptClaimAudit){$pptClaimAudit.FullName}else{"missing"}))
$lines.Add("- PPT claim patch: " + $(if($pptClaimPatch){$pptClaimPatch.FullName}else{"missing"}))
$lines.Add("- Public secret scan: " + $(if($secretScanReport){$secretScanReport.FullName}else{"missing"}))
$lines.Add("- Cloud-device handoff: " + $(if($cloudDeviceHandoff){$cloudDeviceHandoff.FullName}else{"missing"}))
$lines.Add("- PPT defect table: " + $(if([string]::IsNullOrWhiteSpace($pptDefectTable)){"not auto-resolved"}else{$pptDefectTable}))
$lines.Add("- Review checklist: " + $(if([string]::IsNullOrWhiteSpace($reviewChecklist)){"not auto-resolved"}else{$reviewChecklist}))
$lines.Add("- Release runbook: " + $(if([string]::IsNullOrWhiteSpace($runbook)){"not auto-resolved"}else{$runbook}))
$lines.Add("- Android QA plan: " + $(if([string]::IsNullOrWhiteSpace($androidPlan)){"not auto-resolved"}else{$androidPlan}))
$lines.Add("")
$lines.Add("## Closure Rule")
$lines.Add("")
$lines.Add("Do not mark the 10-stage goal complete until this audit has zero MISSING/PARTIAL rows, required MANUAL_REVIEW rows are signed off, and check-final-submission.ps1 also passes.")

Set-Content -LiteralPath $outputPath -Value $lines -Encoding UTF8
Write-Host "Wrote $outputPath"

if ($missingCount -gt 0 -or $partialCount -gt 0) {
    exit 2
}
