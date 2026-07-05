param(
    [string]$ProjectRoot = (Resolve-Path "$PSScriptRoot\..\..").Path,
    [string]$ApkPath = "",
    [string]$PackageName = "com.catlife.mvp",
    [string]$AdbPath = "",
    [string]$CloudAdbEndpoint = "",
    [string]$DeviceSerial = "",
    [int]$TimeoutSeconds = 600,
    [int]$PollSeconds = 5,
    [switch]$SkipInstall,
    [switch]$SkipRecording,
    [switch]$AllowNoDevice
)

$ErrorActionPreference = "Stop"

$finalDir = Join-Path $ProjectRoot "06-deliverables\final-submission"
$statusDir = Join-Path $finalDir "evidence\android\05-summary"
$statusPath = Join-Path $statusDir "stage9_wait_for_device_status.md"
$collectorPath = Join-Path $ProjectRoot "tools\final-submission\collect-stage9-android-evidence.ps1"

if ([string]::IsNullOrWhiteSpace($ApkPath)) {
    $ApkPath = Join-Path $finalDir "CatLife_MVP_Android_v0.1.0.apk"
}
if (-not [System.IO.Path]::IsPathRooted($ApkPath)) {
    $ApkPath = Join-Path $ProjectRoot $ApkPath
}

function Resolve-AdbExecutable {
    if (-not [string]::IsNullOrWhiteSpace($AdbPath)) {
        if (Test-Path -LiteralPath $AdbPath) {
            return (Resolve-Path -LiteralPath $AdbPath).Path
        }
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

function Invoke-AdbLines {
    param(
        [string]$AdbExecutable,
        [string[]]$Arguments
    )

    try {
        return (& $AdbExecutable @Arguments 2>&1)
    } catch {
        return @("adb failed: " + $_.Exception.GetType().Name)
    }
}

function Get-ConnectedDeviceSerials {
    param([string]$AdbExecutable)

    $lines = Invoke-AdbLines -AdbExecutable $AdbExecutable -Arguments @("devices")
    $serials = New-Object System.Collections.Generic.List[string]
    foreach ($line in $lines) {
        if ($line -match "^([^\s]+)\s+device$") {
            $serials.Add($Matches[1]) | Out-Null
        }
    }

    return [pscustomobject]@{
        Lines = $lines
        Serials = @($serials)
    }
}

function Write-Status {
    param(
        [string]$Status,
        [string]$AdbExecutable,
        [string[]]$DeviceLines,
        [string]$SelectedSerial,
        [string]$Message
    )

    New-Item -ItemType Directory -Force -Path $statusDir | Out-Null
    $cloudEndpointDisplay = if ([string]::IsNullOrWhiteSpace($CloudAdbEndpoint)) { "<none>" } else { $CloudAdbEndpoint }
    $requestedSerialDisplay = if ([string]::IsNullOrWhiteSpace($DeviceSerial)) { "<none>" } else { $DeviceSerial }
    $selectedSerialDisplay = if ([string]::IsNullOrWhiteSpace($SelectedSerial)) { "<none>" } else { $SelectedSerial }
    $adbDisplay = if ([string]::IsNullOrWhiteSpace($AdbExecutable)) { "missing" } else { $AdbExecutable }

    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add("# CatLife Stage9 Wait For Device Status")
    $lines.Add("")
    $lines.Add("Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
    $lines.Add("Status: $Status")
    $lines.Add("Message: $Message")
    $lines.Add("")
    $lines.Add("## Inputs")
    $lines.Add("")
    $lines.Add("- APK path: $ApkPath")
    $lines.Add("- APK exists: " + (Test-Path -LiteralPath $ApkPath))
    $lines.Add("- Package name: $PackageName")
    $lines.Add("- Cloud endpoint: $cloudEndpointDisplay")
    $lines.Add("- Requested serial: $requestedSerialDisplay")
    $lines.Add("- Selected serial: $selectedSerialDisplay")
    $lines.Add("- Timeout seconds: $TimeoutSeconds")
    $lines.Add("- Poll seconds: $PollSeconds")
    $lines.Add("- Skip install: $SkipInstall")
    $lines.Add("- Skip recording: $SkipRecording")
    $lines.Add("")
    $lines.Add("## ADB")
    $lines.Add("")
    $lines.Add("- ADB path: $adbDisplay")
    $lines.Add("")
    $lines.Add('```text')
    if ($DeviceLines -and $DeviceLines.Count -gt 0) {
        foreach ($line in $DeviceLines) {
            $lines.Add($line)
        }
    } else {
        $lines.Add("<no adb output>")
    }
    $lines.Add('```')
    $lines.Add("")
    $lines.Add("## Closure")
    $lines.Add("")
    $lines.Add("This wait status is not Stage9 completion evidence. Stage9 only closes after install, startup logcat, LLM/fallback logcat, focus-flow logcat, and recording evidence are collected.")
    Set-Content -LiteralPath $statusPath -Value $lines -Encoding UTF8
}

if (-not (Test-Path -LiteralPath $collectorPath)) {
    throw "Missing collector script: $collectorPath"
}

$adb = Resolve-AdbExecutable
if ([string]::IsNullOrWhiteSpace($adb)) {
    Write-Status -Status "MISSING_ADB" -AdbExecutable "" -DeviceLines @("adb not found") -SelectedSerial "" -Message "ADB executable was not found."
    if ($AllowNoDevice) {
        Write-Host "No adb executable found. Status written: $statusPath"
        exit 0
    }
    exit 2
}

$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
$selectedSerial = ""
$lastDevices = @()

while ((Get-Date) -le $deadline) {
    if (-not [string]::IsNullOrWhiteSpace($CloudAdbEndpoint)) {
        Invoke-AdbLines -AdbExecutable $adb -Arguments @("connect", $CloudAdbEndpoint) | Out-Null
    }

    $devices = Get-ConnectedDeviceSerials -AdbExecutable $adb
    $lastDevices = @($devices.Lines)
    $serials = @($devices.Serials)

    if (-not [string]::IsNullOrWhiteSpace($DeviceSerial)) {
        if ($serials -contains $DeviceSerial) {
            $selectedSerial = $DeviceSerial
            break
        }
    } elseif ($serials.Count -gt 0) {
        $selectedSerial = $serials[0]
        break
    }

    if ($TimeoutSeconds -le 0) {
        break
    }
    Start-Sleep -Seconds ([Math]::Max(1, $PollSeconds))
}

if ([string]::IsNullOrWhiteSpace($selectedSerial)) {
    Write-Status -Status "NO_DEVICE" -AdbExecutable $adb -DeviceLines $lastDevices -SelectedSerial "" -Message "No connected adb device was detected before timeout."
    if ($AllowNoDevice) {
        Write-Host "No connected device. Status written: $statusPath"
        exit 0
    }
    exit 2
}

Write-Status -Status "DEVICE_FOUND" -AdbExecutable $adb -DeviceLines $lastDevices -SelectedSerial $selectedSerial -Message "Connected adb device detected; invoking Stage9 collector."

$collectorArgs = @(
    "-NoProfile",
    "-ExecutionPolicy", "Bypass",
    "-File", $collectorPath,
    "-ProjectRoot", $ProjectRoot,
    "-ApkPath", $ApkPath,
    "-PackageName", $PackageName,
    "-AdbPath", $adb,
    "-DeviceSerial", $selectedSerial
)

if (-not [string]::IsNullOrWhiteSpace($CloudAdbEndpoint)) {
    $collectorArgs += @("-CloudAdbEndpoint", $CloudAdbEndpoint)
}
if ($SkipInstall) {
    $collectorArgs += "-SkipInstall"
}
if ($SkipRecording) {
    $collectorArgs += "-SkipRecording"
}

& powershell @collectorArgs
exit $LASTEXITCODE
