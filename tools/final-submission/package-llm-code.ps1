param(
    [string]$ProjectRoot = (Resolve-Path "$PSScriptRoot\..\..").Path,
    [string]$OutputDir = "",
    [switch]$ForFinalSubmission
)

$ErrorActionPreference = "Stop"

$templateDir = Join-Path $ProjectRoot "06-deliverables\llm-code-package-template"
if (-not (Test-Path -LiteralPath $templateDir)) {
    throw "Missing template directory: $templateDir"
}

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    if ($ForFinalSubmission) {
        $OutputDir = Join-Path $ProjectRoot "06-deliverables\final-submission"
    } else {
        $OutputDir = Join-Path $ProjectRoot "work\llm-code-package-output"
    }
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$packageName = if ($ForFinalSubmission) {
    "CatLife_LLM_code_package_v1.zip"
} else {
    "CatLife_LLM_code_package_DRAFT.zip"
}

$zipPath = Join-Path $OutputDir $packageName
$manifestPath = Join-Path $OutputDir "CatLife_LLM_code_package_manifest.md"

$secretPatterns = @(
    "sk-[A-Za-z0-9]",
    "api[_-]key\s*[:=]",
    "API_KEY\s*[:=]",
    "secret\s*[:=]",
    "password\s*[:=]",
    "token\s*[:=]",
    "Bearer\s+[A-Za-z0-9_\-]{20,}"
)

$secretHits = New-Object System.Collections.Generic.List[string]
Get-ChildItem -LiteralPath $templateDir -Recurse -File |
    Where-Object { $_.Length -lt 5MB } |
    ForEach-Object {
        foreach ($pattern in $secretPatterns) {
            $hits = Select-String -LiteralPath $_.FullName -Pattern $pattern -CaseSensitive:$false -ErrorAction SilentlyContinue
            foreach ($hit in $hits) {
                $relative = Resolve-Path -LiteralPath $hit.Path -Relative
                $secretHits.Add("${relative}:$($hit.LineNumber):$($hit.Line.Trim())")
            }
        }
    }

if ($secretHits.Count -gt 0) {
    Write-Host "Potential secrets found. Package was not created."
    foreach ($hit in $secretHits) {
        Write-Host $hit
    }
    exit 3
}

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

Compress-Archive -Path (Join-Path $templateDir "*") -DestinationPath $zipPath -Force

$hash = Get-FileHash -Algorithm SHA256 -LiteralPath $zipPath
$files = Get-ChildItem -LiteralPath $templateDir -Recurse -File | Sort-Object FullName
$mode = if ($ForFinalSubmission) { "final-submission-name" } else { "draft" }

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("# CatLife LLM Code Package Manifest")
$lines.Add("")
$lines.Add("Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
$lines.Add("Mode: " + $mode)
$lines.Add("Package: " + (Resolve-Path -LiteralPath $zipPath -Relative))
$lines.Add("SHA256: " + $hash.Hash)
$lines.Add("")
$lines.Add("## Source Files")
$lines.Add("")
$lines.Add("| File | Size(bytes) |")
$lines.Add("|---|---:|")
foreach ($file in $files) {
    $relative = Resolve-Path -LiteralPath $file.FullName -Relative
    $lines.Add("| " + $relative + " | " + $file.Length + " |")
}
$lines.Add("")
$lines.Add("## Notes")
$lines.Add("")
if ($ForFinalSubmission) {
    $lines.Add("This package uses the final submission filename. Before uploading, confirm provider-specific API parsing has been updated and the demo call evidence exists.")
} else {
    $lines.Add("This is a draft package for review. Do not upload it as the final competition code package without real integration notes.")
}
$lines.Add("Secret scan: PASS, hits=0")

Set-Content -LiteralPath $manifestPath -Value $lines -Encoding UTF8

Write-Host "Wrote $zipPath"
Write-Host "Wrote $manifestPath"
Write-Host "SHA256 $($hash.Hash)"
