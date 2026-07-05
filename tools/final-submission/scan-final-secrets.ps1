param(
    [string]$ProjectRoot = (Resolve-Path "$PSScriptRoot\..\..").Path,
    [string]$OutputName = "CatLife_public_secret_scan_20260705.md"
)

$ErrorActionPreference = "Stop"

$outputPath = Join-Path $ProjectRoot ("06-deliverables\final-submission\" + $OutputName)
$scanRoots = @(
    "06-deliverables/final-submission",
    "06-deliverables/llm-code-package-template",
    "tools/final-submission",
    "08-handoff-docs/planning",
    "07-tech-specs"
)

$excludedExtensions = @(
    ".apk", ".aab", ".mp4", ".mov", ".png", ".jpg", ".jpeg", ".zip", ".7z", ".rar", ".pptx", ".bak"
)

$excludedRelativePatterns = @(
    "work/CatLife_Unity_Main/Assets/Resources/CatLifePrivate",
    "06-deliverables/final-submission/CatLife_MVP_Android",
    "06-deliverables/final-submission/CatLife_LLM_code_package",
    "06-deliverables/final-submission/CatLife_作品介绍PPT",
    "06-deliverables/final-submission/CatLife_作品海报"
)

$rules = @(
    [pscustomobject]@{
        Id = "sk_token"
        Pattern = "sk-[A-Za-z0-9_\-+=/]{16,}"
    },
    [pscustomobject]@{
        Id = "authorization_bearer"
        Pattern = "Authorization\s*:\s*Bearer\s+[A-Za-z0-9_\-+=./]{8,}"
    },
    [pscustomobject]@{
        Id = "bearer_token"
        Pattern = "Bearer\s+[A-Za-z0-9_\-+=./]{24,}"
    },
    [pscustomobject]@{
        Id = "json_app_key"
        Pattern = '"appKey"\s*:\s*"(?!REDACTED|CHANGE_ME|YOUR_|PLACEHOLDER|TODO|missing)[^"]{8,}"'
    },
    [pscustomobject]@{
        Id = "named_secret_value"
        Pattern = '(api[_-]?key|secret|password|token)\s*[:=]\s*["'']?(?!REDACTED|TODO|CHANGE_ME|PLACEHOLDER|missing|false|true)[A-Za-z0-9_\-+=./]{20,}'
    }
)

function Get-RepoRelativePath {
    param([string]$Path)
    $full = (Resolve-Path -LiteralPath $Path).Path
    if ($full.StartsWith($ProjectRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        return ($full.Substring($ProjectRoot.Length).TrimStart([char[]]@("\", "/")) -replace "\\", "/")
    }
    return ($full -replace "\\", "/")
}

function Test-ExcludedRelativePath {
    param([string]$RelativePath)
    foreach ($pattern in $excludedRelativePatterns) {
        if ($RelativePath.StartsWith($pattern, [System.StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
    }
    return $false
}

$hits = New-Object System.Collections.Generic.List[object]
$filesScanned = 0

foreach ($root in $scanRoots) {
    $rootPath = Join-Path $ProjectRoot $root
    if (-not (Test-Path -LiteralPath $rootPath)) {
        continue
    }

    Get-ChildItem -LiteralPath $rootPath -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object {
            $_.Length -lt 5MB -and
            $excludedExtensions -notcontains $_.Extension.ToLowerInvariant()
        } |
        ForEach-Object {
            $relative = Get-RepoRelativePath $_.FullName
            if (-not (Test-ExcludedRelativePath $relative)) {
                $script:filesScanned += 1

                foreach ($rule in $rules) {
                    $matches = Select-String -LiteralPath $_.FullName -Pattern $rule.Pattern -CaseSensitive:$false -ErrorAction SilentlyContinue
                    foreach ($match in $matches) {
                        $hits.Add([pscustomobject]@{
                            File = $relative
                            Line = $match.LineNumber
                            Rule = $rule.Id
                        }) | Out-Null
                    }
                }
            }
        }
}

$status = if ($hits.Count -eq 0) { "PASS" } else { "FAIL" }
$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("# CatLife Public Secret Scan")
$lines.Add("")
$lines.Add("Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
$lines.Add("Project root: $ProjectRoot")
$lines.Add("")
$lines.Add("## Summary")
$lines.Add("")
$lines.Add("- Status: " + $status)
$lines.Add("- Hits: " + $hits.Count)
$lines.Add("- Files scanned: " + $filesScanned)
$lines.Add("")
$lines.Add("## Scope")
$lines.Add("")
foreach ($root in $scanRoots) {
    $lines.Add("- " + $root)
}
$lines.Add("")
$lines.Add("## Exclusions")
$lines.Add("")
$lines.Add("- Binary deliverables and local ignored private Resources are not printed or scanned as public text.")
$lines.Add("- The real APK may include the local ignored vivo cloud key for cloud-device recording, but public Git/docs/logs/code packages must not contain plaintext credentials.")
$lines.Add("- Hit reports intentionally omit matched line content to avoid echoing secrets.")
$lines.Add("")
$lines.Add("## Hits")
$lines.Add("")
if ($hits.Count -eq 0) {
    $lines.Add("No configured public-secret patterns matched.")
} else {
    $lines.Add("| File | Line | Rule |")
    $lines.Add("|---|---:|---|")
    foreach ($hit in $hits) {
        $lines.Add("| $($hit.File) | $($hit.Line) | $($hit.Rule) |")
    }
}
$lines.Add("")

Set-Content -LiteralPath $outputPath -Value $lines -Encoding UTF8
Write-Host "Wrote $outputPath"
Write-Host "Status=$status Hits=$($hits.Count) Files=$filesScanned"

if ($hits.Count -gt 0) {
    exit 2
}
