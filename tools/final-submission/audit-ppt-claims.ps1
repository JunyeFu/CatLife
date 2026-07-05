param(
    [string]$ProjectRoot = (Resolve-Path "$PSScriptRoot\..\..").Path,
    [string]$PptPath = "",
    [string]$PythonPath = "",
    [switch]$AllowHits
)

$ErrorActionPreference = "Stop"

$finalDir = Join-Path $ProjectRoot "06-deliverables\final-submission"
if ([string]::IsNullOrWhiteSpace($PptPath)) {
    $ppt = Get-ChildItem -LiteralPath $finalDir -File -Filter "*.pptx" -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if ($ppt) {
        $PptPath = $ppt.FullName
    }
}

if (-not [System.IO.Path]::IsPathRooted($PptPath)) {
    $PptPath = Join-Path $ProjectRoot $PptPath
}

if ([string]::IsNullOrWhiteSpace($PythonPath)) {
    $bundled = Join-Path $env:USERPROFILE ".cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe"
    if (Test-Path -LiteralPath $bundled) {
        $PythonPath = $bundled
    }
}

if ([string]::IsNullOrWhiteSpace($PythonPath)) {
    $cmd = Get-Command python -ErrorAction SilentlyContinue
    if ($cmd) {
        $PythonPath = $cmd.Source
    }
}

if ([string]::IsNullOrWhiteSpace($PythonPath) -or -not (Test-Path -LiteralPath $PythonPath)) {
    throw "Python executable not found. Provide -PythonPath."
}

$scriptPath = Join-Path $PSScriptRoot "audit-ppt-claims.py"
$reportPath = Join-Path $finalDir "CatLife_PPT_claim_audit_20260705.md"
$textPath = Join-Path $finalDir "CatLife_PPT_extracted_text_20260705.md"

$argsList = @(
    $scriptPath,
    "--pptx", $PptPath,
    "--report", $reportPath,
    "--text-out", $textPath
)
if ($AllowHits) {
    $argsList += "--allow-hits"
}

& $PythonPath @argsList
exit $LASTEXITCODE
