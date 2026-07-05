param(
    [string]$ProjectRoot = (Resolve-Path "$PSScriptRoot\..\..").Path,
    [string]$SourceDir = "",
    [string]$FinalVideo = "",
    [string]$InstallLog = "install.log",
    [string]$DeviceInfo = "device-info.txt",
    [string]$StartupLogcat = "logcat_startup.txt",
    [string]$LlmLogcat = "logcat_vivo_cloud_llm.txt",
    [string]$FocusLogcat = "logcat_5min_focus.txt",
    [string]$FocusRecording = "focus_5min_screenrecord.mp4",
    [string]$LaunchScreenshot = "launch.png",
    [string]$TownScreenshot = "town-main.png",
    [string]$OutputName = "CatLife_final_evidence_input_check_20260705.md",
    [switch]$AllowIncomplete
)

$ErrorActionPreference = "Stop"

$finalDir = Join-Path $ProjectRoot "06-deliverables\final-submission"
$outputPath = Join-Path $finalDir $OutputName

function Resolve-InputPath {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return ""
    }

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return $Path
    }

    if (-not [string]::IsNullOrWhiteSpace($SourceDir)) {
        return (Join-Path $SourceDir $Path)
    }

    return (Join-Path $ProjectRoot $Path)
}

function New-InputRow {
    param(
        [string]$Name,
        [string]$Path,
        [bool]$Required,
        [string]$SignalPattern
    )

    $resolved = Resolve-InputPath -Path $Path
    $exists = (-not [string]::IsNullOrWhiteSpace($resolved)) -and (Test-Path -LiteralPath $resolved)
    $size = 0
    $signalOk = $false
    if ($exists) {
        $item = Get-Item -LiteralPath $resolved
        $size = $item.Length
        if ([string]::IsNullOrWhiteSpace($SignalPattern)) {
            $signalOk = $true
        } elseif ($item.Length -lt 10MB) {
            $content = Get-Content -LiteralPath $resolved -Raw -ErrorAction SilentlyContinue
            $signalOk = ($content -match $SignalPattern)
        }
    }

    $status = "PASS"
    if (-not $exists) {
        $status = if ($Required) { "MISSING" } else { "OPTIONAL_MISSING" }
    } elseif (-not $signalOk) {
        $status = if ($Required) { "WEAK_SIGNAL" } else { "OPTIONAL_WEAK_SIGNAL" }
    }

    return [pscustomobject]@{
        Name = $Name
        Input = $Path
        Resolved = $resolved
        Required = $Required
        Exists = $exists
        SizeBytes = $size
        SignalOk = $signalOk
        Status = $status
    }
}

New-Item -ItemType Directory -Force -Path $finalDir | Out-Null

$rows = New-Object System.Collections.Generic.List[object]
$rows.Add((New-InputRow -Name "Install log" -Path $InstallLog -Required $true -SignalPattern "Success|INSTALL_SUCCEEDED|installed|安装成功")) | Out-Null
$rows.Add((New-InputRow -Name "Device info" -Path $DeviceInfo -Required $false -SignalPattern "")) | Out-Null
$rows.Add((New-InputRow -Name "Startup logcat" -Path $StartupLogcat -Required $true -SignalPattern "CatLife|Unity|Activity|com\.catlife\.mvp")) | Out-Null
$rows.Add((New-InputRow -Name "LLM logcat" -Path $LlmLogcat -Required $true -SignalPattern "vivo_cloud|bluelm_on_device|local_template|fallback|llm_source|llm_error|BlueLM|LLM")) | Out-Null
$rows.Add((New-InputRow -Name "Focus logcat" -Path $FocusLogcat -Required $true -SignalPattern "CatLife|focus|llm_source|Unity|fallback")) | Out-Null
$rows.Add((New-InputRow -Name "Focus recording" -Path $FocusRecording -Required $true -SignalPattern "")) | Out-Null
$rows.Add((New-InputRow -Name "Launch screenshot" -Path $LaunchScreenshot -Required $false -SignalPattern "")) | Out-Null
$rows.Add((New-InputRow -Name "Town screenshot" -Path $TownScreenshot -Required $false -SignalPattern "")) | Out-Null
$rows.Add((New-InputRow -Name "Final demo video" -Path $FinalVideo -Required $true -SignalPattern "")) | Out-Null

$blockingRows = @($rows | Where-Object { $_.Required -and $_.Status -ne "PASS" })
$ready = ($blockingRows.Count -eq 0)

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("# CatLife Final Evidence Input Check")
$lines.Add("")
$lines.Add("Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
$lines.Add("SourceDir: " + $(if ([string]::IsNullOrWhiteSpace($SourceDir)) { "<none>" } else { $SourceDir }))
$lines.Add("")
$lines.Add("## Summary")
$lines.Add("")
$lines.Add("- Ready to import: $ready")
$lines.Add("- Blocking input issues: $($blockingRows.Count)")
$lines.Add("")
$lines.Add("## Inputs")
$lines.Add("")
$lines.Add("| Input | Required | Status | Exists | Size(bytes) | Signal OK | Path |")
$lines.Add("|---|---:|---|---:|---:|---:|---|")
foreach ($row in $rows) {
    $safePath = if ([string]::IsNullOrWhiteSpace($row.Resolved)) { "" } else { $row.Resolved.Replace("|", "/") }
    $lines.Add("| $($row.Name) | $($row.Required) | $($row.Status) | $($row.Exists) | $($row.SizeBytes) | $($row.SignalOk) | ``$safePath`` |")
}
$lines.Add("")
$lines.Add("## Next Command")
$lines.Add("")
$lines.Add("After all required rows are PASS, run:")
$lines.Add("")
$lines.Add('```powershell')
$lines.Add('powershell -ExecutionPolicy Bypass -File tools/final-submission/import-final-submission-evidence.ps1 -SourceDir "<folder containing downloaded cloud-device files>" -FinalVideo "<path to final demo mp4>"')
$lines.Add('```')
$lines.Add("")
$lines.Add("This check does not copy files and does not prove Stage9 completion.")

Set-Content -LiteralPath $outputPath -Value $lines -Encoding UTF8
Write-Host "Wrote $outputPath"
Write-Host "ReadyToImport=$ready BlockingIssues=$($blockingRows.Count)"

if (-not $ready -and -not $AllowIncomplete) {
    exit 2
}
