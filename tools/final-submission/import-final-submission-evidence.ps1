param(
    [string]$ProjectRoot = (Resolve-Path "$PSScriptRoot\..\..").Path,
    [string]$SourceDir = "",
    [string]$FinalVideo = "",
    [string]$InstallLog = "install.log",
    [string]$DeviceInfo = "device-info.txt",
    [string]$StartupLogcat = "logcat_startup.txt",
    [string]$LlmLogcat = "logcat_vivo_cloud_llm.txt",
    [string]$BlueLmInitLogcat = "",
    [string]$BlueLmGenerateLogcat = "",
    [string]$FocusLogcat = "logcat_5min_focus.txt",
    [string]$StartupRecording = "",
    [string]$FocusRecording = "focus_5min_screenrecord.mp4",
    [string]$RawRecording = "",
    [string]$LaunchScreenshot = "launch.png",
    [string]$TownScreenshot = "town-main.png",
    [switch]$AllowIncomplete
)

$ErrorActionPreference = "Stop"

$finalDir = Join-Path $ProjectRoot "06-deliverables\final-submission"
$summaryPath = Join-Path $finalDir "CatLife_final_evidence_import_summary_20260705.md"
$canonicalVideoName = "CatLife_{0}{1}{2}{3}{4}{5}_v1.mp4" -f ([char]0x4F5C), ([char]0x54C1), ([char]0x6F14), ([char]0x793A), ([char]0x89C6), ([char]0x9891)
$canonicalVideoPath = Join-Path $finalDir $canonicalVideoName
$cloudImportScript = Join-Path $ProjectRoot "tools\final-submission\import-cloud-device-evidence.ps1"
$videoScript = Join-Path $ProjectRoot "tools\final-submission\test-final-video.ps1"
$gateScript = Join-Path $ProjectRoot "tools\final-submission\run-final-submission-gates.ps1"

function Resolve-InputPath {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return ""
    }

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return $Path
    }

    if (-not [string]::IsNullOrWhiteSpace($SourceDir)) {
        $sourceCandidate = Join-Path $SourceDir $Path
        if (Test-Path -LiteralPath $sourceCandidate) {
            return (Resolve-Path -LiteralPath $sourceCandidate).Path
        }
    }

    return (Join-Path $ProjectRoot $Path)
}

function Invoke-Tool {
    param(
        [string]$ScriptPath,
        [string[]]$Arguments,
        [int[]]$IncompleteExitCodes = @(2)
    )

    if (-not (Test-Path -LiteralPath $ScriptPath)) {
        return [pscustomobject]@{
            Script = $ScriptPath
            ExitCode = 127
            Status = "FAILED"
        }
    }

    & powershell -NoProfile -ExecutionPolicy Bypass -File $ScriptPath @Arguments
    $exitCode = $LASTEXITCODE
    $status = "PASS"
    if ($exitCode -ne 0) {
        if ($IncompleteExitCodes -contains $exitCode) {
            $status = "INCOMPLETE"
        } else {
            $status = "FAILED"
        }
    }

    return [pscustomobject]@{
        Script = $ScriptPath
        ExitCode = $exitCode
        Status = $status
    }
}

New-Item -ItemType Directory -Force -Path $finalDir | Out-Null

$videoSource = Resolve-InputPath -Path $FinalVideo
$videoImported = $false
$videoHash = ""
$videoSize = 0
if (-not [string]::IsNullOrWhiteSpace($videoSource) -and (Test-Path -LiteralPath $videoSource)) {
    Copy-Item -LiteralPath $videoSource -Destination $canonicalVideoPath -Force
    $videoFile = Get-Item -LiteralPath $canonicalVideoPath
    $videoSize = $videoFile.Length
    $videoHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $canonicalVideoPath).Hash
    $videoImported = $true
}

$cloudArgs = @(
    "-ProjectRoot", $ProjectRoot,
    "-InstallLog", $InstallLog,
    "-DeviceInfo", $DeviceInfo,
    "-StartupLogcat", $StartupLogcat,
    "-LlmLogcat", $LlmLogcat,
    "-FocusLogcat", $FocusLogcat,
    "-FocusRecording", $FocusRecording,
    "-LaunchScreenshot", $LaunchScreenshot,
    "-TownScreenshot", $TownScreenshot,
    "-AllowIncomplete"
)
if (-not [string]::IsNullOrWhiteSpace($SourceDir)) {
    $cloudArgs += @("-SourceDir", $SourceDir)
}
if (-not [string]::IsNullOrWhiteSpace($BlueLmInitLogcat)) {
    $cloudArgs += @("-BlueLmInitLogcat", $BlueLmInitLogcat)
}
if (-not [string]::IsNullOrWhiteSpace($BlueLmGenerateLogcat)) {
    $cloudArgs += @("-BlueLmGenerateLogcat", $BlueLmGenerateLogcat)
}
if (-not [string]::IsNullOrWhiteSpace($StartupRecording)) {
    $cloudArgs += @("-StartupRecording", $StartupRecording)
}
if (-not [string]::IsNullOrWhiteSpace($RawRecording)) {
    $cloudArgs += @("-RawRecording", $RawRecording)
}

$cloudResult = Invoke-Tool -ScriptPath $cloudImportScript -Arguments $cloudArgs
$videoResult = Invoke-Tool -ScriptPath $videoScript -Arguments @("-ProjectRoot", $ProjectRoot, "-AllowMissing")
$gateResult = Invoke-Tool -ScriptPath $gateScript -Arguments @("-ProjectRoot", $ProjectRoot, "-AllowIncomplete")

$manualImportSummary = Join-Path $finalDir "evidence\android\05-summary\manual_cloud_device_import.md"
$masterGateReport = Join-Path $finalDir "CatLife_final_submission_master_gate_20260705.md"
if ((Test-Path -LiteralPath $manualImportSummary) -and ((Get-Content -LiteralPath $manualImportSummary -Raw) -match 'Manual cloud-device evidence is still incomplete')) {
    $cloudResult = [pscustomobject]@{
        Script = $cloudResult.Script
        ExitCode = 2
        Status = "INCOMPLETE"
    }
}
if (-not $videoImported) {
    $videoResult = [pscustomobject]@{
        Script = $videoResult.Script
        ExitCode = 2
        Status = "INCOMPLETE"
    }
}
if ((Test-Path -LiteralPath $masterGateReport) -and ((Get-Content -LiteralPath $masterGateReport -Raw) -match 'Ready for final submission:\s+False')) {
    $gateResult = [pscustomobject]@{
        Script = $gateResult.Script
        ExitCode = 2
        Status = "INCOMPLETE"
    }
}

$allPass = ($cloudResult.Status -eq "PASS" -and $videoResult.Status -eq "PASS" -and $gateResult.Status -eq "PASS")

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("# CatLife Final Evidence Import Summary")
$lines.Add("")
$lines.Add("Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
$lines.Add("SourceDir: " + $(if ([string]::IsNullOrWhiteSpace($SourceDir)) { "<none>" } else { $SourceDir }))
$lines.Add("")
$lines.Add("## Video")
$lines.Add("")
$lines.Add("- Final video source: " + $(if ([string]::IsNullOrWhiteSpace($videoSource)) { "<none>" } else { $videoSource }))
$lines.Add("- Imported to canonical path: $videoImported")
$lines.Add("- Canonical path: $canonicalVideoPath")
$lines.Add("- Size bytes: $videoSize")
$lines.Add("- SHA256: " + $(if ([string]::IsNullOrWhiteSpace($videoHash)) { "missing" } else { $videoHash }))
$lines.Add("")
$lines.Add("## Tool Results")
$lines.Add("")
$lines.Add("| Tool | Status | Exit code |")
$lines.Add("|---|---|---:|")
$lines.Add("| import-cloud-device-evidence.ps1 | $($cloudResult.Status) | $($cloudResult.ExitCode) |")
$lines.Add("| test-final-video.ps1 | $($videoResult.Status) | $($videoResult.ExitCode) |")
$lines.Add("| run-final-submission-gates.ps1 | $($gateResult.Status) | $($gateResult.ExitCode) |")
$lines.Add("")
$lines.Add("## Status")
$lines.Add("")
if ($allPass) {
    $lines.Add("PASS: final evidence import completed and all gates passed. Manual platform upload review is still required.")
} else {
    $lines.Add("INCOMPLETE: final evidence import did not close all gates.")
    if (-not $videoImported) { $lines.Add("- Final video was not imported.") }
    if ($cloudResult.Status -ne "PASS") { $lines.Add("- Cloud-device evidence remains incomplete or failed.") }
    if ($videoResult.Status -ne "PASS") { $lines.Add("- Video QA remains incomplete or failed.") }
    if ($gateResult.Status -ne "PASS") { $lines.Add("- Final master gate remains incomplete or failed.") }
}

Set-Content -LiteralPath $summaryPath -Value $lines -Encoding UTF8
Write-Host "Wrote $summaryPath"

if (-not $allPass -and -not $AllowIncomplete) {
    exit 2
}
if ($cloudResult.Status -eq "FAILED" -or $videoResult.Status -eq "FAILED" -or $gateResult.Status -eq "FAILED") {
    exit 1
}
