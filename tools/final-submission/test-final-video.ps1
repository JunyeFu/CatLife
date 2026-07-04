param(
    [string]$ProjectRoot = (Resolve-Path "$PSScriptRoot\..\..").Path,
    [string]$VideoPath = "",
    [string]$OutputName = "CatLife_video_manifest.md",
    [string]$FfprobePath = "",
    [int]$TargetMaxSeconds = 180,
    [int]$HardMaxSeconds = 300,
    [switch]$AllowMissing
)

$ErrorActionPreference = "Stop"

$finalDir = Join-Path $ProjectRoot "06-deliverables\final-submission"
$outputPath = Join-Path $finalDir $OutputName

function Find-FirstFile {
    param([string[]]$Patterns)

    foreach ($pattern in $Patterns) {
        $match = Get-ChildItem -LiteralPath $finalDir -File -Filter $pattern -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1
        if ($match) {
            return $match
        }
    }

    return $null
}

function Resolve-FfprobeExecutable {
    if (-not [string]::IsNullOrWhiteSpace($FfprobePath)) {
        return $FfprobePath
    }

    $cmd = Get-Command ffprobe -ErrorAction SilentlyContinue
    if ($cmd) {
        return $cmd.Source
    }

    $candidateRoots = @(
        (Join-Path $ProjectRoot "tools"),
        (Join-Path $ProjectRoot "work"),
        "C:\ffmpeg",
        "C:\Program Files\ffmpeg",
        "C:\ProgramData\chocolatey\bin"
    )

    foreach ($root in $candidateRoots) {
        if (-not (Test-Path -LiteralPath $root)) {
            continue
        }

        $candidate = Get-ChildItem -LiteralPath $root -Recurse -Filter ffprobe.exe -ErrorAction SilentlyContinue |
            Select-Object -First 1
        if ($candidate) {
            return $candidate.FullName
        }
    }

    return ""
}

function ConvertTo-ProcessArgumentString {
    param([string[]]$Arguments)

    $escaped = @()
    foreach ($arg in $Arguments) {
        if ($null -eq $arg) {
            continue
        }

        if ($arg -match '[\s"]') {
            $escaped += '"' + ($arg -replace '"', '\"') + '"'
        } else {
            $escaped += $arg
        }
    }

    return ($escaped -join " ")
}

function Invoke-FfprobeJson {
    param(
        [string]$Executable,
        [string]$Path
    )

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $Executable
    $psi.Arguments = ConvertTo-ProcessArgumentString @(
        "-v", "error",
        "-print_format", "json",
        "-show_format",
        "-show_streams",
        $Path
    )
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.UseShellExecute = $false

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $psi
    [void]$process.Start()
    $stdout = $process.StandardOutput.ReadToEnd()
    $stderr = $process.StandardError.ReadToEnd()
    $process.WaitForExit()

    if ($process.ExitCode -ne 0) {
        throw "ffprobe failed with exit code $($process.ExitCode): $stderr"
    }

    return ($stdout | ConvertFrom-Json)
}

New-Item -ItemType Directory -Force -Path $finalDir | Out-Null

if ([string]::IsNullOrWhiteSpace($VideoPath)) {
    $video = Find-FirstFile @("CatLife_作品演示视频*.mp4", "CatLife_demo_video*.mp4", "*.mp4")
} else {
    if (-not [System.IO.Path]::IsPathRooted($VideoPath)) {
        $VideoPath = Join-Path $ProjectRoot $VideoPath
    }
    $video = if (Test-Path -LiteralPath $VideoPath) { Get-Item -LiteralPath $VideoPath } else { $null }
}

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("# CatLife Video Manifest")
$lines.Add("")
$lines.Add("Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
$lines.Add("")

if (-not $video) {
    $lines.Add("## Status")
    $lines.Add("")
    $lines.Add("MISSING: final demo video was not found.")
    $lines.Add("")
    $lines.Add("Expected file pattern: CatLife demo video MP4 under final-submission")
    $lines.Add("")
    $lines.Add("The video remains a blocking final-submission item.")
    Set-Content -LiteralPath $outputPath -Value $lines -Encoding UTF8
    Write-Host "Wrote $outputPath"
    if ($AllowMissing) {
        exit 0
    }
    exit 2
}

$hash = Get-FileHash -Algorithm SHA256 -LiteralPath $video.FullName
$ffprobe = Resolve-FfprobeExecutable
$ffprobeAvailable = -not [string]::IsNullOrWhiteSpace($ffprobe)
$durationSeconds = $null
$width = $null
$height = $null
$codec = ""
$metadataStatus = "NOT_CHECKED"
$metadataError = ""

if ($ffprobeAvailable) {
    try {
        $probe = Invoke-FfprobeJson -Executable $ffprobe -Path $video.FullName
        if ($probe.format -and $probe.format.duration) {
            $durationSeconds = [double]$probe.format.duration
        }
        $videoStream = @($probe.streams | Where-Object { $_.codec_type -eq "video" } | Select-Object -First 1)
        if ($videoStream.Count -gt 0) {
            $width = [int]$videoStream[0].width
            $height = [int]$videoStream[0].height
            $codec = [string]$videoStream[0].codec_name
        }
        $metadataStatus = "CHECKED"
    } catch {
        $metadataStatus = "FAILED"
        $metadataError = $_.Exception.Message
    }
}

$extensionOk = $video.Extension.ToLowerInvariant() -eq ".mp4"
$durationKnown = $null -ne $durationSeconds
$hardDurationOk = $durationKnown -and $durationSeconds -le $HardMaxSeconds
$targetDurationOk = $durationKnown -and $durationSeconds -le $TargetMaxSeconds
$resolutionKnown = ($null -ne $width -and $null -ne $height)
$resolutionOk = $resolutionKnown -and (
    ($width -eq 1920 -and $height -eq 1080) -or
    ($width -eq 1080 -and $height -eq 1920)
)
$metadataComplete = $ffprobeAvailable -and $metadataStatus -eq "CHECKED" -and $durationKnown -and $resolutionKnown
$videoPass = $extensionOk -and $metadataComplete -and $hardDurationOk -and $resolutionOk

$sidecarPatterns = @(
    [System.IO.Path]::ChangeExtension($video.FullName, ".srt"),
    [System.IO.Path]::ChangeExtension($video.FullName, ".txt"),
    [System.IO.Path]::ChangeExtension($video.FullName, ".md")
)
$sidecarSecretHits = New-Object System.Collections.Generic.List[string]
foreach ($sidecar in $sidecarPatterns) {
    if (-not (Test-Path -LiteralPath $sidecar)) {
        continue
    }

    $content = Get-Content -LiteralPath $sidecar -Raw -ErrorAction SilentlyContinue
    if ($content -match 'sk-[A-Za-z0-9_\-+=./]{8,}' -or
        $content -match '(?i)Authorization\s*:\s*Bearer\s+[A-Za-z0-9_\-+=./]{8,}' -or
        $content -match '(?i)"appKey"\s*:\s*"(?!REDACTED)[^"]{8,}"') {
        $sidecarSecretHits.Add($sidecar) | Out-Null
    }
}

$lines.Add("## File")
$lines.Add("")
$lines.Add("- File: $($video.FullName)")
$lines.Add("- Size bytes: $($video.Length)")
$lines.Add("- SHA256: $($hash.Hash)")
$lines.Add("- Extension OK: $extensionOk")
$lines.Add("")
$lines.Add("## Metadata")
$lines.Add("")
$lines.Add("- ffprobe available: $ffprobeAvailable")
if ($ffprobeAvailable) {
    $lines.Add("- ffprobe path: $ffprobe")
}
$lines.Add("- metadata status: $metadataStatus")
if (-not [string]::IsNullOrWhiteSpace($metadataError)) {
    $lines.Add("- metadata error: $metadataError")
}
$lines.Add("- duration seconds: " + $(if ($durationKnown) { "{0:N2}" -f $durationSeconds } else { "unknown" }))
$lines.Add("- target duration <= $TargetMaxSeconds seconds: $targetDurationOk")
$lines.Add("- hard duration <= $HardMaxSeconds seconds: $hardDurationOk")
$lines.Add("- resolution: " + $(if ($resolutionKnown) { "$width x $height" } else { "unknown" }))
$lines.Add("- official resolution OK: $resolutionOk")
if (-not [string]::IsNullOrWhiteSpace($codec)) {
    $lines.Add("- video codec: $codec")
}
$lines.Add("")
$lines.Add("## Privacy")
$lines.Add("")
$lines.Add("- Subtitle/transcript sidecar secret scan hits: $($sidecarSecretHits.Count)")
foreach ($hit in $sidecarSecretHits) {
    $lines.Add("  - $hit")
}
$lines.Add("")
$lines.Add("## Manual Review Required")
$lines.Add("")
$lines.Add("- First 5 seconds show CatLife name and cat/town.")
$lines.Add("- Footage comes from the final APK or current Unity Game View.")
$lines.Add("- Demonstrates normal -> transition -> focus -> reward or clearly explains any fallback.")
$lines.Add("- No AppKEY, account page, system notification, private chat, or unrelated desktop content is visible.")
$lines.Add("- Audio/subtitles are readable and do not overclaim BlueLM/on-device status beyond log evidence.")
$lines.Add("")
$lines.Add("## Status")
$lines.Add("")
if ($videoPass -and $sidecarSecretHits.Count -eq 0) {
    $lines.Add("PASS: automated video checks passed. Manual content review is still required.")
} else {
    $lines.Add("INCOMPLETE: automated video checks did not fully pass.")
    if (-not $extensionOk) { $lines.Add("- File is not MP4.") }
    if (-not $metadataComplete) { $lines.Add("- Duration/resolution metadata was not fully checked. Install or provide ffprobe, then rerun this script.") }
    if ($durationKnown -and -not $hardDurationOk) { $lines.Add("- Video is longer than the hard 5 minute limit.") }
    if ($durationKnown -and -not $targetDurationOk) { $lines.Add("- Video is longer than the recommended 3 minute target.") }
    if ($resolutionKnown -and -not $resolutionOk) { $lines.Add("- Video is not 1920x1080 or 1080x1920.") }
    if ($sidecarSecretHits.Count -gt 0) { $lines.Add("- Subtitle/transcript sidecar contains potential secret text.") }
}

Set-Content -LiteralPath $outputPath -Value $lines -Encoding UTF8
Write-Host "Wrote $outputPath"

if (-not $videoPass -or $sidecarSecretHits.Count -gt 0) {
    exit 2
}
