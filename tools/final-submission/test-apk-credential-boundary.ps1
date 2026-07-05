param(
    [string]$ProjectRoot = (Resolve-Path "$PSScriptRoot\..\..").Path,
    [string]$ApkPath = "",
    [string]$OutputName = "CatLife_apk_private_credential_boundary_20260705.md"
)

$ErrorActionPreference = "Stop"

$finalDir = Join-Path $ProjectRoot "06-deliverables\final-submission"
$outputPath = Join-Path $finalDir $OutputName
$privateConfigRelative = "work/CatLife_Unity_Main/Assets/Resources/CatLifePrivate/vivo_cloud_credentials.json"
$privateConfigPath = Join-Path $ProjectRoot $privateConfigRelative
$runtimeConfigPath = Join-Path $ProjectRoot "work\CatLife_Unity_Main\Assets\Scripts\LLM\VivoCloudDemoConfig.cs"
$buildScriptPath = Join-Path $ProjectRoot "work\CatLife_Unity_Main\Assets\Editor\CatLifeAndroidBuild.cs"
$apkHashEvidencePath = Join-Path $finalDir "evidence\android\00-build\apk-sha256.txt"

if ([string]::IsNullOrWhiteSpace($ApkPath)) {
    $ApkPath = Join-Path $finalDir "CatLife_MVP_Android_v0.1.0.apk"
}
if (-not [System.IO.Path]::IsPathRooted($ApkPath)) {
    $ApkPath = Join-Path $ProjectRoot $ApkPath
}

function New-CheckRow {
    param(
        [string]$Check,
        [bool]$Pass,
        [string]$Evidence,
        [string]$RiskIfMissing
    )

    [pscustomobject]@{
        Check = $Check
        Pass = $Pass
        Evidence = $Evidence
        RiskIfMissing = $RiskIfMissing
    }
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

function Get-FileText {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return ""
    }

    return Get-Content -LiteralPath $Path -Raw -ErrorAction SilentlyContinue
}

New-Item -ItemType Directory -Force -Path $finalDir | Out-Null

$privateExists = Test-Path -LiteralPath $privateConfigPath
$privateIgnored = Test-GitIgnored $privateConfigRelative
$appId = ""
$appKeyPresent = $false
$appKeyPlaceholderLike = $true
$endpointHttps = $false
$modelPresent = $false
$privateParseOk = $false

if ($privateExists) {
    try {
        $config = Get-Content -LiteralPath $privateConfigPath -Raw | ConvertFrom-Json
        $privateParseOk = $true
        $appId = [string]$config.appId
        $appKey = [string]$config.appKey
        $endpoint = [string]$config.apiEndpoint
        $model = [string]$config.model
        $appKeyPresent = -not [string]::IsNullOrWhiteSpace($appKey)
        $appKeyPlaceholderLike = ($appKey -match 'DO_NOT_COMMIT|REPLACE_WITH|YOUR_APP_KEY|PLACEHOLDER|EXAMPLE')
        $endpointHttps = -not [string]::IsNullOrWhiteSpace($endpoint) -and $endpoint.StartsWith("https://", [System.StringComparison]::OrdinalIgnoreCase)
        $modelPresent = -not [string]::IsNullOrWhiteSpace($model)
    } catch {
        $privateParseOk = $false
    }
}

$runtimeText = Get-FileText $runtimeConfigPath
$buildText = Get-FileText $buildScriptPath
$runtimeLoadsPrivateResource = $runtimeText -match 'Resources\.Load<TextAsset>\(ResourcePath\)' -and $runtimeText -match 'CatLifePrivate/vivo_cloud_credentials'
$runtimeRejectsPlaceholders = $runtimeText -match 'IsPlaceholderAppKey' -and $runtimeText -match 'DO_NOT_COMMIT_REAL_APP_KEY'
$buildChecksPrivateResource = $buildText -match 'Resources\.Load<TextAsset>\("CatLifePrivate/vivo_cloud_credentials"\)' -and $buildText -match 'private_config_presence_redacted'

$apkExists = Test-Path -LiteralPath $ApkPath
$apkHashEvidenceExists = Test-Path -LiteralPath $apkHashEvidencePath
$apkHashMatchesEvidence = $false
$apkHash = ""
if ($apkExists) {
    $apkHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $ApkPath).Hash
    if ($apkHashEvidenceExists) {
        $hashEvidenceText = Get-FileText $apkHashEvidencePath
        $apkHashMatchesEvidence = $hashEvidenceText -match [regex]::Escape($apkHash)
    }
}

$rows = New-Object System.Collections.Generic.List[object]
$rows.Add((New-CheckRow "Private credential file exists locally" $privateExists $privateConfigRelative "Real APK cannot be exported with vivo cloud credentials.")) | Out-Null
$rows.Add((New-CheckRow "Private credential file is git-ignored" $privateIgnored "git check-ignore $privateConfigRelative" "Plaintext AppKEY could leak into Git or code package.")) | Out-Null
$rows.Add((New-CheckRow "Private credential JSON parses" $privateParseOk "JSON parse without printing secret values" "Build may include invalid credentials.")) | Out-Null
$rows.Add((New-CheckRow "AppID matches expected vivo resource" ($appId -eq "2026414599") "AppID: $appId" "Cloud request may use the wrong competition resource.")) | Out-Null
$rows.Add((New-CheckRow "AppKEY is present and not placeholder-like" ($appKeyPresent -and -not $appKeyPlaceholderLike) "AppKEY present=$appKeyPresent; placeholder-like=$appKeyPlaceholderLike; value=REDACTED" "Cloud-device APK may only run fallback instead of attempting real API.")) | Out-Null
$rows.Add((New-CheckRow "Endpoint and model are usable" ($endpointHttps -and $modelPresent) "endpoint_https=$endpointHttps; model_present=$modelPresent" "Runtime may reject direct cloud API config.")) | Out-Null
$rows.Add((New-CheckRow "Unity runtime loads private Resources config" $runtimeLoadsPrivateResource $runtimeConfigPath "APK runtime may never read the private config.")) | Out-Null
$rows.Add((New-CheckRow "Unity runtime rejects public placeholder keys" $runtimeRejectsPlaceholders $runtimeConfigPath "Public example credentials may be treated as usable.")) | Out-Null
$rows.Add((New-CheckRow "Unity Android build records private Resources boundary" $buildChecksPrivateResource $buildScriptPath "Build evidence may not prove Resources loadability precondition.")) | Out-Null
$rows.Add((New-CheckRow "APK artifact exists" $apkExists $ApkPath "No real/local APK is available for cloud-device recording.")) | Out-Null
$rows.Add((New-CheckRow "APK hash evidence matches current APK" ($apkExists -and $apkHashEvidenceExists -and $apkHashMatchesEvidence) $apkHashEvidencePath "Evidence may describe a different APK than the one uploaded.")) | Out-Null

$passCount = @($rows | Where-Object { $_.Pass }).Count
$failCount = @($rows | Where-Object { -not $_.Pass }).Count
$ready = ($failCount -eq 0)

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("# CatLife APK Private Credential Boundary")
$lines.Add("")
$lines.Add("Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
$lines.Add("Project root: $ProjectRoot")
$lines.Add("")
$lines.Add("## Summary")
$lines.Add("")
$lines.Add("- Ready for cloud-device real APK credential boundary: $ready")
$lines.Add("- Pass: $passCount")
$lines.Add("- Fail: $failCount")
$lines.Add("- APK path: $ApkPath")
$lines.Add("- APK SHA256: " + $(if ([string]::IsNullOrWhiteSpace($apkHash)) { "missing" } else { $apkHash }))
$lines.Add("- Private AppKEY value: REDACTED")
$lines.Add("")
$lines.Add("## Check Rows")
$lines.Add("")
$lines.Add("| Check | Status | Evidence | Risk if missing |")
$lines.Add("|---|---|---|---|")
foreach ($row in $rows) {
    $status = if ($row.Pass) { "PASS" } else { "FAIL" }
    $lines.Add("| $($row.Check) | $status | $($row.Evidence) | $($row.RiskIfMissing) |")
}
$lines.Add("")
$lines.Add("## Boundary Rule")
$lines.Add("")
$lines.Add("- The real/local APK is expected to be exported with the ignored private Unity Resources config so the vivo cloud device can try the real API without extra setup.")
$lines.Add("- Public GitHub files, code package files, logs, screenshots, PPT, poster, and video subtitles must not contain the plaintext AppKEY.")
$lines.Add("- This report proves the local build preconditions and redacted evidence chain; final Stage9 still requires cloud/local install, startup, LLM/fallback, focus-flow, and recording evidence.")

Set-Content -LiteralPath $outputPath -Value $lines -Encoding UTF8
Write-Host "Wrote $outputPath"
Write-Host "Ready=$ready Pass=$passCount Fail=$failCount"

if (-not $ready) {
    exit 2
}
