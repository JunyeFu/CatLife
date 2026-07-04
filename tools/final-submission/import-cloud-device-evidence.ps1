param(
    [string]$ProjectRoot = (Resolve-Path "$PSScriptRoot\..\..").Path,
    [string]$SourceDir = "",
    [string]$InstallLog = "",
    [string]$DeviceInfo = "",
    [string]$StartupLogcat = "",
    [string]$LlmLogcat = "",
    [string]$BlueLmInitLogcat = "",
    [string]$BlueLmGenerateLogcat = "",
    [string]$FocusLogcat = "",
    [string]$StartupRecording = "",
    [string]$FocusRecording = "",
    [string]$RawRecording = "",
    [string]$LaunchScreenshot = "",
    [string]$TownScreenshot = "",
    [switch]$AllowIncomplete
)

$ErrorActionPreference = "Stop"

$finalDir = Join-Path $ProjectRoot "06-deliverables\final-submission"
$evidenceRoot = Join-Path $finalDir "evidence"
$androidRoot = Join-Path $evidenceRoot "android"
$summaryPath = Join-Path $androidRoot "05-summary\manual_cloud_device_import.md"

$dirs = @(
    "android\01-install",
    "android\02-startup",
    "android\03-llm",
    "android\04-focus",
    "android\05-summary",
    "03-screenshots",
    "04-recordings"
)
foreach ($dir in $dirs) {
    New-Item -ItemType Directory -Force -Path (Join-Path $evidenceRoot $dir) | Out-Null
}

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

function ConvertTo-RedactedText {
    param([string]$Text)

    if ($null -eq $Text) {
        return ""
    }

    $redacted = $Text
    $redacted = $redacted -replace 'sk-[A-Za-z0-9_\-+=./]{8,}', 'sk-REDACTED'
    $redacted = $redacted -replace '(?i)(Authorization\s*:\s*Bearer\s+)[A-Za-z0-9_\-+=./]{8,}', '${1}REDACTED'
    $redacted = $redacted -replace '(?i)("appKey"\s*:\s*")[^"]+(")', '${1}REDACTED${2}'
    $redacted = $redacted -replace '(?i)(appkey\s*[:=]\s*)\S+', '${1}REDACTED'
    $redacted = $redacted -replace '(?i)(token\s*[:=]\s*)\S+', '${1}REDACTED'
    return $redacted
}

function Test-SecretInText {
    param([string]$Text)

    if ([string]::IsNullOrWhiteSpace($Text)) {
        return $false
    }

    return ($Text -match 'sk-[A-Za-z0-9_\-+=./]{8,}' -or
        $Text -match '(?i)Authorization\s*:\s*Bearer\s+[A-Za-z0-9_\-+=./]{8,}' -or
        $Text -match '(?i)"appKey"\s*:\s*"(?!REDACTED)[^"]{8,}"')
}

function Copy-TextEvidence {
    param(
        [string]$Source,
        [string]$Destination,
        [string]$Label
    )

    $resolved = Resolve-InputPath $Source
    if ([string]::IsNullOrWhiteSpace($resolved) -or -not (Test-Path -LiteralPath $resolved)) {
        return [pscustomobject]@{
            Label = $Label
            Status = "missing"
            Source = $Source
            Destination = $Destination
            SizeBytes = 0
            SHA256 = ""
            SecretAfterRedaction = $false
        }
    }

    $raw = Get-Content -LiteralPath $resolved -Raw -ErrorAction Stop
    $redacted = ConvertTo-RedactedText $raw
    $secretAfterRedaction = Test-SecretInText $redacted
    Set-Content -LiteralPath $Destination -Value $redacted -Encoding UTF8
    $file = Get-Item -LiteralPath $Destination
    $hash = Get-FileHash -Algorithm SHA256 -LiteralPath $Destination

    return [pscustomobject]@{
        Label = $Label
        Status = "imported"
        Source = $resolved
        Destination = $Destination
        SizeBytes = $file.Length
        SHA256 = $hash.Hash
        SecretAfterRedaction = $secretAfterRedaction
    }
}

function Copy-BinaryEvidence {
    param(
        [string]$Source,
        [string]$Destination,
        [string]$Label
    )

    $resolved = Resolve-InputPath $Source
    if ([string]::IsNullOrWhiteSpace($resolved) -or -not (Test-Path -LiteralPath $resolved)) {
        return [pscustomobject]@{
            Label = $Label
            Status = "missing"
            Source = $Source
            Destination = $Destination
            SizeBytes = 0
            SHA256 = ""
            SecretAfterRedaction = $false
        }
    }

    Copy-Item -LiteralPath $resolved -Destination $Destination -Force
    $file = Get-Item -LiteralPath $Destination
    $hash = Get-FileHash -Algorithm SHA256 -LiteralPath $Destination

    return [pscustomobject]@{
        Label = $Label
        Status = "imported"
        Source = $resolved
        Destination = $Destination
        SizeBytes = $file.Length
        SHA256 = $hash.Hash
        SecretAfterRedaction = $false
    }
}

function Test-TextEvidenceContains {
    param(
        [string]$Path,
        [string]$Pattern
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return $false
    }

    $content = Get-Content -LiteralPath $Path -Raw -ErrorAction SilentlyContinue
    return ($content -match $Pattern)
}

$results = New-Object System.Collections.Generic.List[object]
$results.Add((Copy-TextEvidence -Source $InstallLog -Destination (Join-Path $androidRoot "01-install\install.log") -Label "install log")) | Out-Null
$results.Add((Copy-TextEvidence -Source $DeviceInfo -Destination (Join-Path $androidRoot "01-install\device-info.txt") -Label "device info")) | Out-Null
$results.Add((Copy-TextEvidence -Source $StartupLogcat -Destination (Join-Path $androidRoot "02-startup\logcat_startup.txt") -Label "startup logcat")) | Out-Null
$results.Add((Copy-TextEvidence -Source $LlmLogcat -Destination (Join-Path $androidRoot "03-llm\logcat_vivo_cloud_llm.txt") -Label "LLM logcat")) | Out-Null
$results.Add((Copy-TextEvidence -Source $BlueLmInitLogcat -Destination (Join-Path $androidRoot "03-llm\logcat_bluelm_init.txt") -Label "BlueLM init logcat")) | Out-Null
$results.Add((Copy-TextEvidence -Source $BlueLmGenerateLogcat -Destination (Join-Path $androidRoot "03-llm\logcat_bluelm_generate.txt") -Label "BlueLM generate logcat")) | Out-Null
$results.Add((Copy-TextEvidence -Source $FocusLogcat -Destination (Join-Path $androidRoot "04-focus\logcat_5min_focus.txt") -Label "focus logcat")) | Out-Null
$results.Add((Copy-BinaryEvidence -Source $StartupRecording -Destination (Join-Path $androidRoot "02-startup\startup_screenrecord.mp4") -Label "startup recording")) | Out-Null
$results.Add((Copy-BinaryEvidence -Source $FocusRecording -Destination (Join-Path $androidRoot "04-focus\focus_5min_screenrecord.mp4") -Label "focus recording")) | Out-Null
$results.Add((Copy-BinaryEvidence -Source $RawRecording -Destination (Join-Path $evidenceRoot "04-recordings\raw-device-recording.mp4") -Label "raw recording")) | Out-Null
$results.Add((Copy-BinaryEvidence -Source $LaunchScreenshot -Destination (Join-Path $evidenceRoot "03-screenshots\launch.png") -Label "launch screenshot")) | Out-Null
$results.Add((Copy-BinaryEvidence -Source $TownScreenshot -Destination (Join-Path $evidenceRoot "03-screenshots\town-main.png") -Label "town screenshot")) | Out-Null

$installOk = Test-TextEvidenceContains -Path (Join-Path $androidRoot "01-install\install.log") -Pattern "Success|INSTALL_SUCCEEDED|installed|安装成功"
$startupOk = Test-TextEvidenceContains -Path (Join-Path $androidRoot "02-startup\logcat_startup.txt") -Pattern "CatLife|Unity|Activity|com\.catlife\.mvp"
$llmOk = Test-TextEvidenceContains -Path (Join-Path $androidRoot "03-llm\logcat_vivo_cloud_llm.txt") -Pattern "vivo_cloud|bluelm_on_device|local_template|fallback|llm_source|llm_error|BlueLM|LLM"
$recordingOk = (Test-Path -LiteralPath (Join-Path $androidRoot "04-focus\focus_5min_screenrecord.mp4")) -or
    (Test-Path -LiteralPath (Join-Path $evidenceRoot "04-recordings\raw-device-recording.mp4")) -or
    (Test-Path -LiteralPath (Join-Path $androidRoot "02-startup\startup_screenrecord.mp4"))
$secretHits = @($results | Where-Object { $_.SecretAfterRedaction })

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("# CatLife Manual Cloud-Device Evidence Import")
$lines.Add("")
$lines.Add("Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
$lines.Add("SourceDir: " + $(if ([string]::IsNullOrWhiteSpace($SourceDir)) { "<none>" } else { $SourceDir }))
$lines.Add("")
$lines.Add("## Imported Files")
$lines.Add("")
$lines.Add("| Item | Status | Size(bytes) | SHA256 | Destination |")
$lines.Add("|---|---|---:|---|---|")
foreach ($result in $results) {
    $destination = if ([string]::IsNullOrWhiteSpace($result.Destination)) { "" } else { $result.Destination.Replace($ProjectRoot, "").TrimStart("\") }
    $lines.Add("| $($result.Label) | $($result.Status) | $($result.SizeBytes) | $($result.SHA256) | ``$destination`` |")
}
$lines.Add("")
$lines.Add("## Readiness Signals")
$lines.Add("")
$lines.Add("- Install evidence looks successful: $installOk")
$lines.Add("- Startup logcat contains CatLife/Unity/app signal: $startupOk")
$lines.Add("- LLM logcat contains vivo/BlueLM/fallback signal: $llmOk")
$lines.Add("- Recording evidence exists: $recordingOk")
$lines.Add("- Text evidence secret scan after redaction: " + $(if ($secretHits.Count -eq 0) { "PASS" } else { "FAILED" }))
$lines.Add("")
$lines.Add("## Status")
$lines.Add("")
if ($installOk -and $startupOk -and $llmOk -and $recordingOk -and $secretHits.Count -eq 0) {
    $lines.Add("Manual cloud-device evidence was imported and has the minimum signals needed for review. Run ``tools/final-submission/check-final-submission.ps1`` next.")
} else {
    $lines.Add("Manual cloud-device evidence is still incomplete.")
    if (-not $installOk) { $lines.Add("- Missing or weak install success evidence.") }
    if (-not $startupOk) { $lines.Add("- Missing or weak startup logcat evidence.") }
    if (-not $llmOk) { $lines.Add("- Missing or weak LLM vivo/BlueLM/fallback logcat evidence.") }
    if (-not $recordingOk) { $lines.Add("- Missing cloud-device recording evidence.") }
    if ($secretHits.Count -gt 0) { $lines.Add("- Redacted text evidence still contains potential secret patterns.") }
}

Set-Content -LiteralPath $summaryPath -Value $lines -Encoding UTF8
Write-Host "Wrote $summaryPath"

if (-not $AllowIncomplete -and ($secretHits.Count -gt 0)) {
    exit 3
}
if (-not $AllowIncomplete -and -not ($installOk -and $startupOk -and $llmOk -and $recordingOk)) {
    exit 2
}
