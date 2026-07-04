param(
    [string]$ProjectRoot = (Resolve-Path "$PSScriptRoot\..\..").Path,
    [string]$ApkPath = "",
    [string]$PackageName = "com.catlife.mvp",
    [string]$DeviceSerial = "",
    [string]$CloudAdbEndpoint = "",
    [int]$StartupLogcatSeconds = 20,
    [int]$FocusLogcatSeconds = 60,
    [int]$StartupRecordSeconds = 12,
    [int]$FocusRecordSeconds = 60,
    [switch]$SkipInstall,
    [switch]$SkipRecording,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$finalDir = Join-Path $ProjectRoot "06-deliverables\final-submission"
$evidenceRoot = Join-Path $finalDir "evidence\android"
$privateConfigRelative = "work/CatLife_Unity_Main/Assets/Resources/CatLifePrivate/vivo_cloud_credentials.json"
$privateConfigPath = Join-Path $ProjectRoot $privateConfigRelative

if ([string]::IsNullOrWhiteSpace($ApkPath)) {
    $ApkPath = Join-Path $finalDir "CatLife_MVP_Android_v0.1.0.apk"
}
if (-not [System.IO.Path]::IsPathRooted($ApkPath)) {
    $ApkPath = Join-Path $ProjectRoot $ApkPath
}

$dirs = @(
    "00-build",
    "01-install",
    "02-startup",
    "03-llm",
    "04-focus",
    "05-summary"
)

foreach ($dir in $dirs) {
    New-Item -ItemType Directory -Force -Path (Join-Path $evidenceRoot $dir) | Out-Null
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

function Write-TextFile {
    param(
        [string]$Path,
        [string[]]$Lines
    )

    Set-Content -LiteralPath $Path -Value $Lines -Encoding UTF8
}

function Add-SummaryLine {
    param([string]$Line)
    $script:summaryLines.Add($Line) | Out-Null
}

function Invoke-External {
    param(
        [string]$FilePath,
        [string[]]$Arguments,
        [string]$OutputPath,
        [switch]$AllowFailure
    )

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $FilePath
    foreach ($arg in $Arguments) {
        [void]$psi.ArgumentList.Add($arg)
    }
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.UseShellExecute = $false
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $psi
    [void]$process.Start()
    $stdout = $process.StandardOutput.ReadToEnd()
    $stderr = $process.StandardError.ReadToEnd()
    $process.WaitForExit()

    $content = @()
    $content += "> $FilePath " + ($Arguments -join " ")
    $content += "exit_code=$($process.ExitCode)"
    if (-not [string]::IsNullOrWhiteSpace($stdout)) {
        $content += ""
        $content += "STDOUT:"
        $content += (ConvertTo-RedactedText $stdout).TrimEnd()
    }
    if (-not [string]::IsNullOrWhiteSpace($stderr)) {
        $content += ""
        $content += "STDERR:"
        $content += (ConvertTo-RedactedText $stderr).TrimEnd()
    }

    Write-TextFile -Path $OutputPath -Lines $content

    if ($process.ExitCode -ne 0 -and -not $AllowFailure) {
        throw "Command failed: $FilePath $($Arguments -join ' ')"
    }

    return [pscustomobject]@{
        ExitCode = $process.ExitCode
        Stdout = $stdout
        Stderr = $stderr
        OutputPath = $OutputPath
    }
}

function Get-AdbArguments {
    param([string[]]$Arguments)
    if ([string]::IsNullOrWhiteSpace($DeviceSerial)) {
        return $Arguments
    }
    return @("-s", $DeviceSerial) + $Arguments
}

function Invoke-Adb {
    param(
        [string[]]$Arguments,
        [string]$OutputPath,
        [switch]$AllowFailure
    )

    return Invoke-External -FilePath "adb" -Arguments (Get-AdbArguments $Arguments) -OutputPath $OutputPath -AllowFailure:$AllowFailure
}

function Capture-LogcatWindow {
    param(
        [int]$Seconds,
        [string]$OutputPath,
        [string]$FilterRegex = "CatLife|BlueLM|Unity|vivo|LLM|fallback|llm_source|llm_error"
    )

    $rawPath = $OutputPath + ".raw"
    $adbArgs = Get-AdbArguments @("logcat", "-v", "time")
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = "adb"
    foreach ($arg in $adbArgs) {
        [void]$psi.ArgumentList.Add($arg)
    }
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.UseShellExecute = $false
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $psi
    [void]$process.Start()
    Start-Sleep -Seconds $Seconds
    if (-not $process.HasExited) {
        $process.Kill()
    }
    $stdout = $process.StandardOutput.ReadToEnd()
    $stderr = $process.StandardError.ReadToEnd()

    $combined = @()
    if (-not [string]::IsNullOrWhiteSpace($stdout)) {
        $combined += $stdout -split "`r?`n" | Where-Object { $_ -match $FilterRegex }
    }
    if (-not [string]::IsNullOrWhiteSpace($stderr)) {
        $combined += "STDERR:"
        $combined += $stderr -split "`r?`n"
    }

    Set-Content -LiteralPath $rawPath -Value $combined -Encoding UTF8
    Set-Content -LiteralPath $OutputPath -Value (ConvertTo-RedactedText (($combined -join [Environment]::NewLine))) -Encoding UTF8
    Remove-Item -LiteralPath $rawPath -Force -ErrorAction SilentlyContinue
}

function Test-SecretInFile {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) {
        return $false
    }
    $content = Get-Content -LiteralPath $Path -Raw -ErrorAction SilentlyContinue
    return ($content -match 'sk-[A-Za-z0-9_\-+=./]{8,}' -or
        $content -match '(?i)Authorization\s*:\s*Bearer\s+[A-Za-z0-9_\-+=./]{8,}' -or
        $content -match '(?i)"appKey"\s*:\s*"(?!REDACTED)[^"]{8,}"')
}

$script:summaryLines = New-Object System.Collections.Generic.List[string]
Add-SummaryLine "# CatLife Stage9 Android Evidence Summary"
Add-SummaryLine ""
Add-SummaryLine "Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
Add-SummaryLine "Project root: $ProjectRoot"
Add-SummaryLine "Package: $PackageName"
Add-SummaryLine ""

$privateConfigLines = New-Object System.Collections.Generic.List[string]
$privateConfigLines.Add("Private config path: $privateConfigRelative") | Out-Null
$privateConfigLines.Add("Exists: " + (Test-Path -LiteralPath $privateConfigPath)) | Out-Null

$ignored = $false
try {
    git -C $ProjectRoot check-ignore -q -- $privateConfigRelative
    $ignored = ($LASTEXITCODE -eq 0)
} catch {
    $ignored = $false
}
$privateConfigLines.Add("Git ignored: $ignored") | Out-Null

$appId = ""
$appKeyPresent = $false
$appKeyLooksPlaceholder = $true
if (Test-Path -LiteralPath $privateConfigPath) {
    try {
        $configJson = Get-Content -LiteralPath $privateConfigPath -Raw | ConvertFrom-Json
        $appId = [string]$configJson.appId
        $appKey = [string]$configJson.appKey
        $appKeyPresent = -not [string]::IsNullOrWhiteSpace($appKey)
        $appKeyLooksPlaceholder = ($appKey -match 'DO_NOT_COMMIT|REPLACE_WITH|YOUR_APP_KEY|PLACEHOLDER')
    } catch {
        $privateConfigLines.Add("Parse error: $($_.Exception.GetType().Name)") | Out-Null
    }
}
$privateConfigLines.Add("AppID: " + $(if ([string]::IsNullOrWhiteSpace($appId)) { "missing" } else { $appId })) | Out-Null
$privateConfigLines.Add("AppKEY present: $appKeyPresent") | Out-Null
$privateConfigLines.Add("AppKEY placeholder-like: $appKeyLooksPlaceholder") | Out-Null
$privateConfigLines.Add("AppKEY value: REDACTED") | Out-Null
$privateConfigStatusPath = Join-Path $evidenceRoot "00-build\private_config_presence_redacted.txt"
Write-TextFile -Path $privateConfigStatusPath -Lines $privateConfigLines

Add-SummaryLine "## 1. Build Inputs"
Add-SummaryLine ""
Add-SummaryLine "- APK path: $ApkPath"
Add-SummaryLine "- APK exists: $(Test-Path -LiteralPath $ApkPath)"
Add-SummaryLine "- Private config exists: $(Test-Path -LiteralPath $privateConfigPath)"
Add-SummaryLine "- Private config ignored: $ignored"
Add-SummaryLine "- Private AppKEY present: $appKeyPresent"
Add-SummaryLine "- Private AppKEY placeholder-like: $appKeyLooksPlaceholder"
Add-SummaryLine "- Private AppKEY value: REDACTED"
Add-SummaryLine ""

if (Test-Path -LiteralPath $ApkPath) {
    $apkFile = Get-Item -LiteralPath $ApkPath
    $apkHash = Get-FileHash -Algorithm SHA256 -LiteralPath $ApkPath
    Write-TextFile -Path (Join-Path $evidenceRoot "00-build\apk-sha256.txt") -Lines @(
        "APK file: $($apkFile.FullName)",
        "Size bytes: $($apkFile.Length)",
        "SHA256: $($apkHash.Hash)"
    )
} else {
    Write-TextFile -Path (Join-Path $evidenceRoot "00-build\apk-sha256.txt") -Lines @(
        "APK file: $ApkPath",
        "Size bytes: missing",
        "SHA256: missing"
    )
}

$adb = Get-Command adb -ErrorAction SilentlyContinue
Add-SummaryLine "## 2. ADB"
Add-SummaryLine ""
Add-SummaryLine "- adb available: $([bool]$adb)"
if ($adb) {
    Add-SummaryLine "- adb path: $($adb.Source)"
} else {
    Add-SummaryLine "- adb path: missing"
}
$cloudEndpointDisplay = if ([string]::IsNullOrWhiteSpace($CloudAdbEndpoint)) { "<none>" } else { $CloudAdbEndpoint }
$deviceSerialDisplay = if ([string]::IsNullOrWhiteSpace($DeviceSerial)) { "<none>" } else { $DeviceSerial }
Add-SummaryLine "- Cloud endpoint requested: $cloudEndpointDisplay"
Add-SummaryLine "- Device serial requested: $deviceSerialDisplay"
Add-SummaryLine "- DryRun: $DryRun"
Add-SummaryLine ""

if ($DryRun) {
    Add-SummaryLine "Dry run requested. No adb install, launch, logcat, or screenrecord commands were executed."
} elseif (-not $adb) {
    Add-SummaryLine "ADB is not available on PATH. Stage9 cloud-device evidence remains incomplete until adb or cloud-device web evidence is provided."
} else {
    if (-not [string]::IsNullOrWhiteSpace($CloudAdbEndpoint)) {
        Invoke-External -FilePath "adb" -Arguments @("connect", $CloudAdbEndpoint) -OutputPath (Join-Path $evidenceRoot "01-install\adb_connect.txt") -AllowFailure | Out-Null
    }

    Invoke-External -FilePath "adb" -Arguments @("devices") -OutputPath (Join-Path $evidenceRoot "01-install\adb_devices.txt") -AllowFailure | Out-Null

    if (-not $SkipInstall) {
        if (Test-Path -LiteralPath $ApkPath) {
            Invoke-Adb -Arguments @("install", "-r", $ApkPath) -OutputPath (Join-Path $evidenceRoot "01-install\install.log") -AllowFailure | Out-Null
        } else {
            Write-TextFile -Path (Join-Path $evidenceRoot "01-install\install.log") -Lines @("APK missing: $ApkPath")
        }
    } else {
        Write-TextFile -Path (Join-Path $evidenceRoot "01-install\install.log") -Lines @("Skipped by -SkipInstall")
    }

    Invoke-Adb -Arguments @("shell", "getprop", "ro.product.model") -OutputPath (Join-Path $evidenceRoot "01-install\device_model.txt") -AllowFailure | Out-Null
    Invoke-Adb -Arguments @("shell", "getprop", "ro.build.version.release") -OutputPath (Join-Path $evidenceRoot "01-install\android_version.txt") -AllowFailure | Out-Null
    Invoke-Adb -Arguments @("logcat", "-c") -OutputPath (Join-Path $evidenceRoot "02-startup\logcat_clear.txt") -AllowFailure | Out-Null
    Invoke-Adb -Arguments @("shell", "monkey", "-p", $PackageName, "1") -OutputPath (Join-Path $evidenceRoot "02-startup\launch_monkey.txt") -AllowFailure | Out-Null

    if (-not $SkipRecording) {
        $startupRemote = "/sdcard/catlife-stage9-startup.mp4"
        Invoke-Adb -Arguments @("shell", "screenrecord", "--bit-rate", "8000000", "--time-limit", "$StartupRecordSeconds", $startupRemote) -OutputPath (Join-Path $evidenceRoot "02-startup\screenrecord_startup.log") -AllowFailure | Out-Null
        Invoke-Adb -Arguments @("pull", $startupRemote, (Join-Path $evidenceRoot "02-startup\startup_screenrecord.mp4")) -OutputPath (Join-Path $evidenceRoot "02-startup\screenrecord_startup_pull.log") -AllowFailure | Out-Null
    } else {
        Write-TextFile -Path (Join-Path $evidenceRoot "02-startup\screenrecord_startup.log") -Lines @("Skipped by -SkipRecording")
    }

    Capture-LogcatWindow -Seconds $StartupLogcatSeconds -OutputPath (Join-Path $evidenceRoot "02-startup\logcat_startup.txt")
    Capture-LogcatWindow -Seconds $StartupLogcatSeconds -OutputPath (Join-Path $evidenceRoot "03-llm\logcat_vivo_cloud_llm.txt")
    Capture-LogcatWindow -Seconds $StartupLogcatSeconds -OutputPath (Join-Path $evidenceRoot "03-llm\logcat_bluelm_init.txt") -FilterRegex "BlueLM|BlueLm|bluelm|init|SDK_NOT_LINKED|MODEL_PATH|ALL_FILES_ACCESS|fallback|Unity"
    Capture-LogcatWindow -Seconds $StartupLogcatSeconds -OutputPath (Join-Path $evidenceRoot "03-llm\logcat_bluelm_generate.txt") -FilterRegex "BlueLM|BlueLm|bluelm|generate|requestId|llm_source|llm_error|vivo_cloud|fallback|Unity"

    if (-not $SkipRecording) {
        $focusRemote = "/sdcard/catlife-stage9-focus.mp4"
        Invoke-Adb -Arguments @("shell", "screenrecord", "--bit-rate", "8000000", "--time-limit", "$FocusRecordSeconds", $focusRemote) -OutputPath (Join-Path $evidenceRoot "04-focus\screenrecord_focus.log") -AllowFailure | Out-Null
        Invoke-Adb -Arguments @("pull", $focusRemote, (Join-Path $evidenceRoot "04-focus\focus_5min_screenrecord.mp4")) -OutputPath (Join-Path $evidenceRoot "04-focus\screenrecord_focus_pull.log") -AllowFailure | Out-Null
    } else {
        Write-TextFile -Path (Join-Path $evidenceRoot "04-focus\screenrecord_focus.log") -Lines @("Skipped by -SkipRecording")
    }
    Capture-LogcatWindow -Seconds $FocusLogcatSeconds -OutputPath (Join-Path $evidenceRoot "04-focus\logcat_5min_focus.txt")
}

Add-SummaryLine "## 3. Secret Scan"
Add-SummaryLine ""
$secretFiles = @(
    (Join-Path $evidenceRoot "00-build\private_config_presence_redacted.txt"),
    (Join-Path $evidenceRoot "01-install\install.log"),
    (Join-Path $evidenceRoot "02-startup\logcat_startup.txt"),
    (Join-Path $evidenceRoot "03-llm\logcat_vivo_cloud_llm.txt"),
    (Join-Path $evidenceRoot "03-llm\logcat_bluelm_init.txt"),
    (Join-Path $evidenceRoot "03-llm\logcat_bluelm_generate.txt"),
    (Join-Path $evidenceRoot "04-focus\logcat_5min_focus.txt")
)
$secretHits = @()
foreach ($file in $secretFiles) {
    if (Test-SecretInFile -Path $file) {
        $secretHits += $file
    }
}
if ($secretHits.Count -eq 0) {
    Add-SummaryLine "No secret patterns found in generated text evidence."
} else {
    Add-SummaryLine "Potential secret patterns found. Do not share these files until reviewed:"
    foreach ($hit in $secretHits) {
        Add-SummaryLine "- $hit"
    }
}
Add-SummaryLine ""

Add-SummaryLine "## 4. Stage9 Status"
Add-SummaryLine ""
$stage9Ready = (Test-Path -LiteralPath $ApkPath) -and [bool]$adb -and ($secretHits.Count -eq 0)
if ($stage9Ready) {
    Add-SummaryLine "Evidence collection commands were attempted. Review install/logcat/screenrecord files before marking Stage9 complete."
} else {
    Add-SummaryLine "Stage9 is not complete yet."
    if (-not (Test-Path -LiteralPath $ApkPath)) {
        Add-SummaryLine "- Missing APK: $ApkPath"
    }
    if (-not [bool]$adb) {
        Add-SummaryLine "- Missing adb on PATH or no cloud-device web evidence supplied."
    }
    if ($secretHits.Count -gt 0) {
        Add-SummaryLine "- Generated evidence contains potential secret text and must be redacted."
    }
}

$summaryPath = Join-Path $evidenceRoot "05-summary\stage9_cloud_phone_result.md"
Write-TextFile -Path $summaryPath -Lines $summaryLines

Write-Host "Wrote $summaryPath"
Write-Host "Wrote $privateConfigStatusPath"

if ($secretHits.Count -gt 0) {
    exit 3
}
if (-not $stage9Ready) {
    exit 2
}
