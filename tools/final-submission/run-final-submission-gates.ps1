param(
    [string]$ProjectRoot = (Resolve-Path "$PSScriptRoot\..\..").Path,
    [string]$OutputName = "CatLife_final_submission_master_gate_20260705.md",
    [switch]$AllowIncomplete
)

$ErrorActionPreference = "Stop"

$finalDir = Join-Path $ProjectRoot "06-deliverables\final-submission"
$outputPath = Join-Path $finalDir $OutputName

function Invoke-GateStep {
    param(
        [string]$Name,
        [string]$ScriptRelativePath,
        [string[]]$Arguments,
        [int[]]$IncompleteExitCodes = @(2)
    )

    $scriptPath = Join-Path $ProjectRoot $ScriptRelativePath
    if (-not (Test-Path -LiteralPath $scriptPath)) {
        return [pscustomobject]@{
            Name = $Name
            Script = $ScriptRelativePath
            ExitCode = 127
            Status = "FAILED"
            Notes = "Script missing"
        }
    }

    $allArgs = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $scriptPath, "-ProjectRoot", $ProjectRoot)
    if ($Arguments) {
        $allArgs += $Arguments
    }

    & powershell @allArgs
    $exitCode = $LASTEXITCODE

    $status = "PASS"
    $notes = "Completed"
    if ($exitCode -ne 0) {
        if ($IncompleteExitCodes -contains $exitCode) {
            $status = "INCOMPLETE"
            $notes = "Required evidence is still missing"
        } else {
            $status = "FAILED"
            $notes = "Gate command failed"
        }
    }

    return [pscustomobject]@{
        Name = $Name
        Script = $ScriptRelativePath
        ExitCode = $exitCode
        Status = $status
        Notes = $notes
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
    if ($content -match $Pattern) {
        return $Matches[1]
    }

    return $null
}

New-Item -ItemType Directory -Force -Path $finalDir | Out-Null

$steps = New-Object System.Collections.Generic.List[object]
$steps.Add((Invoke-GateStep -Name "Cloud handoff" -ScriptRelativePath "tools\final-submission\prepare-cloud-device-handoff.ps1" -Arguments @())) | Out-Null
$steps.Add((Invoke-GateStep -Name "Cloud upload workspace" -ScriptRelativePath "tools\final-submission\prepare-cloud-device-upload-workspace.ps1" -Arguments @())) | Out-Null
$steps.Add((Invoke-GateStep -Name "APK credential boundary" -ScriptRelativePath "tools\final-submission\test-apk-credential-boundary.ps1" -Arguments @())) | Out-Null
$steps.Add((Invoke-GateStep -Name "Final evidence input check" -ScriptRelativePath "tools\final-submission\test-final-evidence-inputs.ps1" -Arguments @("-AllowIncomplete"))) | Out-Null
$steps.Add((Invoke-GateStep -Name "Video manifest" -ScriptRelativePath "tools\final-submission\test-final-video.ps1" -Arguments @("-AllowMissing"))) | Out-Null
$steps.Add((Invoke-GateStep -Name "PPT claim audit" -ScriptRelativePath "tools\final-submission\audit-ppt-claims.ps1" -Arguments @("-AllowHits"))) | Out-Null
$steps.Add((Invoke-GateStep -Name "Public secret scan" -ScriptRelativePath "tools\final-submission\scan-final-secrets.ps1" -Arguments @())) | Out-Null
$steps.Add((Invoke-GateStep -Name "Submission check" -ScriptRelativePath "tools\final-submission\check-final-submission.ps1" -Arguments @())) | Out-Null
$steps.Add((Invoke-GateStep -Name "Final requirements audit" -ScriptRelativePath "tools\final-submission\audit-final-requirements.ps1" -Arguments @())) | Out-Null

$submissionCheckPath = Join-Path $finalDir "CatLife_submission_check_20260705.md"
$requirementsAuditPath = Join-Path $finalDir "CatLife_final_requirements_audit_20260705.md"
$secretScanPath = Join-Path $finalDir "CatLife_public_secret_scan_20260705.md"
$videoManifestPath = Join-Path $finalDir "CatLife_video_manifest.md"
$waitStatusPath = Join-Path $finalDir "evidence\android\05-summary\stage9_wait_for_device_status.md"
$finalEvidenceImportSummaryPath = Join-Path $finalDir "CatLife_final_evidence_import_summary_20260705.md"
$finalEvidenceInputCheckPath = Join-Path $finalDir "CatLife_final_evidence_input_check_20260705.md"
$apkCredentialBoundaryPath = Join-Path $finalDir "CatLife_apk_private_credential_boundary_20260705.md"

if ((Test-Path -LiteralPath $finalEvidenceInputCheckPath) -and ((Get-Content -LiteralPath $finalEvidenceInputCheckPath -Raw) -match 'Ready to import:\s+False')) {
    for ($i = 0; $i -lt $steps.Count; $i++) {
        if ($steps[$i].Name -eq "Final evidence input check") {
            $steps[$i] = [pscustomobject]@{
                Name = $steps[$i].Name
                Script = $steps[$i].Script
                ExitCode = 2
                Status = "INCOMPLETE"
                Notes = "Final evidence input files are missing or weak"
            }
        }
    }
}

if ((Test-Path -LiteralPath $videoManifestPath) -and ((Get-Content -LiteralPath $videoManifestPath -Raw) -match 'MISSING:\s+final demo video')) {
    for ($i = 0; $i -lt $steps.Count; $i++) {
        if ($steps[$i].Name -eq "Video manifest") {
            $steps[$i] = [pscustomobject]@{
                Name = $steps[$i].Name
                Script = $steps[$i].Script
                ExitCode = 2
                Status = "INCOMPLETE"
                Notes = "Final demo video is missing"
            }
        }
    }
}

$missing = Get-FirstRegexGroup -Path $requirementsAuditPath -Pattern 'MISSING:\s+(\d+)'
$partial = Get-FirstRegexGroup -Path $requirementsAuditPath -Pattern 'PARTIAL:\s+(\d+)'
$manual = Get-FirstRegexGroup -Path $requirementsAuditPath -Pattern 'MANUAL_REVIEW:\s+(\d+)'
$secretHits = Get-FirstRegexGroup -Path $secretScanPath -Pattern 'Hits:\s+(\d+)'
$waitStatus = Get-FirstRegexGroup -Path $waitStatusPath -Pattern 'Status:\s+([A-Z_]+)'

$failedCount = @($steps | Where-Object { $_.Status -eq "FAILED" }).Count
$incompleteCount = @($steps | Where-Object { $_.Status -eq "INCOMPLETE" }).Count
$ready = ($failedCount -eq 0 -and $incompleteCount -eq 0 -and $missing -eq "0" -and $partial -eq "0" -and ($null -eq $manual -or $manual -eq "0"))

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("# CatLife Final Submission Master Gate")
$lines.Add("")
$lines.Add("Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
$lines.Add("Project root: $ProjectRoot")
$lines.Add("")
$lines.Add("## Summary")
$lines.Add("")
$lines.Add("- Ready for final submission: $ready")
$lines.Add("- Gate failed count: $failedCount")
$lines.Add("- Gate incomplete count: $incompleteCount")
$lines.Add("- Final audit missing rows: " + $(if ($null -eq $missing) { "unknown" } else { $missing }))
$lines.Add("- Final audit partial rows: " + $(if ($null -eq $partial) { "unknown" } else { $partial }))
$lines.Add("- Final audit manual-review rows: " + $(if ($null -eq $manual) { "unknown" } else { $manual }))
$lines.Add("- Public secret scan hits: " + $(if ($null -eq $secretHits) { "unknown" } else { $secretHits }))
$lines.Add("- Stage9 wait status: " + $(if ($null -eq $waitStatus) { "missing" } else { $waitStatus }))
$lines.Add("")
$lines.Add("## Gate Steps")
$lines.Add("")
$lines.Add("| Gate | Status | Exit code | Script | Notes |")
$lines.Add("|---|---|---:|---|---|")
foreach ($step in $steps) {
    $lines.Add("| $($step.Name) | $($step.Status) | $($step.ExitCode) | $($step.Script) | $($step.Notes) |")
}
$lines.Add("")
$lines.Add("## Current Blocking Items")
$lines.Add("")
if ($ready) {
    $lines.Add("No automated blocking items remain. Manual platform upload review is still required.")
} else {
    $lines.Add("- Final video is required when video manifest or submission check reports missing video.")
    $lines.Add("- Cloud/local Android install evidence is required.")
    $lines.Add("- Startup logcat, LLM/fallback logcat, focus-flow logcat, and device/cloud recording evidence are required.")
    $lines.Add("- Do not mark the 10-stage goal complete while final audit has MISSING or PARTIAL rows.")
}
$lines.Add("")
$lines.Add("## Source Reports")
$lines.Add("")
$lines.Add("- Submission check: $submissionCheckPath")
$lines.Add("- Final requirements audit: $requirementsAuditPath")
$lines.Add("- Public secret scan: $secretScanPath")
$lines.Add("- Video manifest: $videoManifestPath")
$lines.Add("- Stage9 wait status: $waitStatusPath")
$lines.Add("- Final evidence import summary: $finalEvidenceImportSummaryPath")
$lines.Add("- Final evidence input check: $finalEvidenceInputCheckPath")
$lines.Add("- APK credential boundary: $apkCredentialBoundaryPath")

Set-Content -LiteralPath $outputPath -Value $lines -Encoding UTF8
Write-Host "Wrote $outputPath"

foreach ($step in $steps) {
    Write-Host "$($step.Status)`t$($step.Name)`t$($step.ExitCode)"
}

if (-not $ready -and -not $AllowIncomplete) {
    exit 2
}
if ($failedCount -gt 0) {
    exit 1
}
