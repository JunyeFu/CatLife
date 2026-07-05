param(
    [string]$ProjectRoot = (Resolve-Path "$PSScriptRoot\..\..").Path,
    [string]$OutputName = "CatLife_runtime_log_marker_check_20260705.md"
)

$ErrorActionPreference = "Stop"

$finalDir = Join-Path $ProjectRoot "06-deliverables\final-submission"
$outputPath = Join-Path $finalDir $OutputName
$sourceRoot = Join-Path $ProjectRoot "work\CatLife_Unity_Main\Assets\Scripts"

function New-MarkerRow {
    param(
        [string]$Marker,
        [string]$Purpose,
        [string]$Pattern
    )

    $matches = @()
    if (Test-Path -LiteralPath $sourceRoot) {
        $matches = Get-ChildItem -LiteralPath $sourceRoot -Recurse -File -Filter "*.cs" |
            Select-String -Pattern $Pattern -SimpleMatch -ErrorAction SilentlyContinue
    }

    [pscustomobject]@{
        Marker = $Marker
        Purpose = $Purpose
        Pattern = $Pattern
        Pass = (@($matches).Count -gt 0)
        Evidence = if (@($matches).Count -gt 0) {
            (@($matches) | Select-Object -First 3 | ForEach-Object {
                (Resolve-Path -LiteralPath $_.Path -Relative) + ":" + $_.LineNumber
            }) -join "; "
        } else {
            "missing"
        }
    }
}

New-Item -ItemType Directory -Force -Path $finalDir | Out-Null

$rows = New-Object System.Collections.Generic.List[object]
$rows.Add((New-MarkerRow "startup" "Startup logcat can prove app launch and package context." "[CatLife] startup package=com.catlife.mvp")) | Out-Null
$rows.Add((New-MarkerRow "focus_start" "Focus-flow logcat can prove focus session entry." "[CatLife] focus_start")) | Out-Null
$rows.Add((New-MarkerRow "focus_unlocked" "Focus-flow logcat can prove user unlock/cancel path." "[CatLife] focus_unlocked")) | Out-Null
$rows.Add((New-MarkerRow "focus_completed" "Focus-flow logcat can prove completed focus path." "[CatLife] focus_completed")) | Out-Null
$rows.Add((New-MarkerRow "focus_feedback_source" "Focus-flow logcat can prove feedback source without content capture." "[CatLife] focus_feedback llm_source=")) | Out-Null
$rows.Add((New-MarkerRow "llm_request_source" "LLM logcat can prove request source and config usability." "[CatLife] llm_request llm_source=")) | Out-Null
$rows.Add((New-MarkerRow "llm_result_source" "LLM logcat can prove vivo cloud, local template, or fallback source." "[CatLife] llm_result llm_source=")) | Out-Null
$rows.Add((New-MarkerRow "llm_factory_route" "LLM logcat can prove runtime route selection." "[CatLife] llm_factory")) | Out-Null

$passCount = @($rows | Where-Object { $_.Pass }).Count
$failCount = @($rows | Where-Object { -not $_.Pass }).Count
$ready = ($failCount -eq 0)

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("# CatLife Runtime Log Marker Check")
$lines.Add("")
$lines.Add("Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
$lines.Add("Project root: $ProjectRoot")
$lines.Add("")
$lines.Add("## Summary")
$lines.Add("")
$lines.Add("- Ready for Stage9 logcat capture: $ready")
$lines.Add("- Pass: $passCount")
$lines.Add("- Fail: $failCount")
$lines.Add("")
$lines.Add("## Marker Rows")
$lines.Add("")
$lines.Add("| Marker | Status | Purpose | Evidence |")
$lines.Add("|---|---|---|---|")
foreach ($row in $rows) {
    $status = if ($row.Pass) { "PASS" } else { "FAIL" }
    $lines.Add("| $($row.Marker) | $status | $($row.Purpose) | $($row.Evidence) |")
}
$lines.Add("")
$lines.Add("## Privacy Rule")
$lines.Add("")
$lines.Add("- Runtime evidence logs must contain state, source, route, and redacted ids only.")
$lines.Add("- Logs must not contain AppKEY, Authorization header, user-entered content, notification text, account data, or private chat content.")

Set-Content -LiteralPath $outputPath -Value $lines -Encoding UTF8
Write-Host "Wrote $outputPath"
Write-Host "Ready=$ready Pass=$passCount Fail=$failCount"

if (-not $ready) {
    exit 2
}
