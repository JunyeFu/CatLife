[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$InputVideo,
    [Parameter(Mandatory)][double]$StartSeconds,
    [Parameter(Mandatory)][double]$DurationSeconds,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [double]$SampleFps = 0,
    [string]$Crop,
    [int]$Columns = 6,
    [int]$Rows = 5,
    [int]$FrameWidth = 270,
    [string]$Ffmpeg = 'D:/MathModelingTools/envs/cumcm/Lib/site-packages/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe'
)
$ErrorActionPreference = 'Stop'
$culture = [System.Globalization.CultureInfo]::InvariantCulture
$video = (Resolve-Path -LiteralPath $InputVideo).Path
$output = [System.IO.Path]::GetFullPath($OutputDirectory)
if ($StartSeconds -lt 0 -or $DurationSeconds -le 0 -or $SampleFps -lt 0 -or $Columns -lt 1 -or $Rows -lt 1 -or $FrameWidth -lt 1) {
    throw 'The video interval and contact sheet dimensions must be positive.'
}
if (Test-Path -LiteralPath $output) { throw 'Use a new output directory to retain the earlier review.' }
$frames = Join-Path $output 'frames'
New-Item -ItemType Directory -Path $frames | Out-Null
$filters = @('trim=duration=' + $DurationSeconds.ToString($culture))
$filters += 'setpts=PTS-STARTPTS'
if ($SampleFps -gt 0) { $filters += 'fps=' + $SampleFps.ToString($culture) }
if ($Crop) { $filters += 'crop=' + $Crop }
$filters += "scale=${FrameWidth}:-2"
& $Ffmpeg -hide_banner -loglevel error -ss $StartSeconds.ToString($culture) -i $video -vf ($filters -join ',') -fps_mode vfr (Join-Path $frames 'frame-%06d.png')
if ($LASTEXITCODE -ne 0) { throw "Video decoding failed: $LASTEXITCODE" }
$count = @(Get-ChildItem -LiteralPath $frames -Filter 'frame-*.png').Count
if (!$count) { throw 'The selected interval produced no frames.' }
& $Ffmpeg -hide_banner -loglevel error -framerate 1 -i (Join-Path $frames 'frame-%06d.png') -vf "tile=${Columns}x${Rows}" -fps_mode vfr (Join-Path $output 'sheet-%03d.png')
if ($LASTEXITCODE -ne 0) { throw "Contact sheet generation failed: $LASTEXITCODE" }
$sheets = @(Get-ChildItem -LiteralPath $output -Filter 'sheet-*.png' | Sort-Object Name)
$capacity = $Columns * $Rows
$review = [ordered]@{
    Input = $video; StartSeconds = $StartSeconds; RequestedDurationSeconds = $DurationSeconds
    Sampling = if ($SampleFps -gt 0) { "sampled_${SampleFps}_fps" } else { 'all_decoded_frames_in_interval' }
    Frames = $count; Crop = $Crop; FrameWidth = $FrameWidth; SheetCapacity = $capacity
    LastSheetPadding = ($capacity - ($count % $capacity)) % $capacity
    Sheets = @($sheets.FullName); SourceFrames = $frames
    Inspection = 'pending_visual_read'; Scope = 'selected_interval_only'
}
$review | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $output 'review.json') -Encoding utf8
$review | ConvertTo-Json -Depth 4 -Compress
