param(
    [string]$ProjectRoot = (Resolve-Path "$PSScriptRoot\..\..").Path,
    [string]$ApkPath = "",
    [string]$OutputName = "CatLife_apk_build_freshness_20260705.md"
)

$ErrorActionPreference = "Stop"

$finalDir = Join-Path $ProjectRoot "06-deliverables\final-submission"
$outputPath = Join-Path $finalDir $OutputName
$unityProject = Join-Path $ProjectRoot "work\CatLife_Unity_Main"

if ([string]::IsNullOrWhiteSpace($ApkPath)) {
    $ApkPath = Join-Path $finalDir "CatLife_MVP_Android_v0.1.0.apk"
}
if (-not [System.IO.Path]::IsPathRooted($ApkPath)) {
    $ApkPath = Join-Path $ProjectRoot $ApkPath
}

$sourceRoots = @(
    (Join-Path $unityProject "Assets\Scripts"),
    (Join-Path $unityProject "Assets\Editor"),
    (Join-Path $unityProject "Assets\Scenes"),
    (Join-Path $unityProject "ProjectSettings")
)

$sourceFiles = New-Object System.Collections.Generic.List[object]
foreach ($root in $sourceRoots) {
    if (-not (Test-Path -LiteralPath $root)) {
        continue
    }

    Get-ChildItem -LiteralPath $root -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object {
            $_.Extension -in @(".cs", ".asmdef", ".asset", ".unity", ".json", ".txt") -and
            $_.FullName -notmatch "\\Library\\|\\Temp\\|\\Obj\\"
        } |
        ForEach-Object { $sourceFiles.Add($_) | Out-Null }
}

$apkExists = Test-Path -LiteralPath $ApkPath
$apkItem = if ($apkExists) { Get-Item -LiteralPath $ApkPath } else { $null }
$newestSource = $sourceFiles | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
$newerSources = @()
if ($apkItem) {
    $newerSources = $sourceFiles |
        Where-Object { $_.LastWriteTimeUtc -gt $apkItem.LastWriteTimeUtc } |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 20
}

$ready = $apkExists -and $newestSource -and @($newerSources).Count -eq 0

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("# CatLife APK Build Freshness")
$lines.Add("")
$lines.Add("Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
$lines.Add("Project root: $ProjectRoot")
$lines.Add("")
$lines.Add("## Summary")
$lines.Add("")
$lines.Add("- APK fresh against Unity source: $ready")
$lines.Add("- APK exists: $apkExists")
$lines.Add("- APK path: $ApkPath")
$lines.Add("- APK last write UTC: " + $(if($apkItem){$apkItem.LastWriteTimeUtc.ToString("o")}else{"missing"}))
$lines.Add("- Unity source files checked: " + $sourceFiles.Count)
$lines.Add("- Newer source files count (sampled): " + @($newerSources).Count)
$lines.Add("- Newest source file: " + $(if($newestSource){(Resolve-Path -LiteralPath $newestSource.FullName -Relative) + " / " + $newestSource.LastWriteTimeUtc.ToString("o")}else{"missing"}))
$lines.Add("")
$lines.Add("## Newer Source Files")
$lines.Add("")
if (@($newerSources).Count -eq 0) {
    $lines.Add("No checked Unity source file is newer than the APK.")
} else {
    $lines.Add("| File | Last write UTC |")
    $lines.Add("|---|---|")
    foreach ($file in $newerSources) {
        $lines.Add("| " + (Resolve-Path -LiteralPath $file.FullName -Relative) + " | " + $file.LastWriteTimeUtc.ToString("o") + " |")
    }
}
$lines.Add("")
$lines.Add("## Rule")
$lines.Add("")
$lines.Add("If any runtime source file is newer than the final APK, rebuild the APK before cloud-device recording. Otherwise logcat evidence may come from stale code.")

Set-Content -LiteralPath $outputPath -Value $lines -Encoding UTF8
Write-Host "Wrote $outputPath"
Write-Host "Ready=$ready NewerSources=$(@($newerSources).Count)"

if (-not $ready) {
    exit 2
}
