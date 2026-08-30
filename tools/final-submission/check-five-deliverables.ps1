param(
    [string]$ProjectRoot = (Resolve-Path "$PSScriptRoot\..\..").Path
)

$ErrorActionPreference = "Stop"
$finalDir = Join-Path $ProjectRoot "06-deliverables\final-submission"

$apkPath = Join-Path $finalDir "CatLife_MVP_Android_v0.1.0_release_optimized.apk"
$codePath = Join-Path $finalDir "CatLife_core_code_package_20260706.zip"
$videoPath = (Get-ChildItem -LiteralPath $finalDir -File -Filter "*.mp4" | Sort-Object Length -Descending | Select-Object -First 1).FullName
$pptPath = (Get-ChildItem -LiteralPath $finalDir -File -Filter "*.pptx" | Sort-Object Length -Descending | Select-Object -First 1).FullName
$posterPath = (Get-ChildItem -LiteralPath $finalDir -File -Filter "*.pdf" | Sort-Object Length -Descending | Select-Object -First 1).FullName

foreach ($path in @($apkPath, $videoPath, $pptPath, $posterPath, $codePath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or (Get-Item -LiteralPath $path).Length -eq 0) {
        throw "Missing or empty deliverable: $path"
    }
}

Add-Type -AssemblyName System.IO.Compression.FileSystem

$apk = [System.IO.Compression.ZipFile]::OpenRead($apkPath)
$apkNames = $apk.Entries.FullName
if (-not ($apkNames -contains "AndroidManifest.xml") -or
    -not ($apkNames -contains "classes.dex") -or
    -not ($apkNames -contains "lib/arm64-v8a/libunity.so")) {
    $apk.Dispose()
    throw "APK structure is incomplete."
}
$apk.Dispose()

$ppt = [System.IO.Compression.ZipFile]::OpenRead($pptPath)
$pptNames = $ppt.Entries.FullName
$slideCount = @($pptNames | Where-Object { $_ -match '^ppt/slides/slide[0-9]+\.xml$' }).Count
if (-not ($pptNames -contains "[Content_Types].xml") -or
    -not ($pptNames -contains "ppt/presentation.xml") -or
    $slideCount -eq 0) {
    $ppt.Dispose()
    throw "PPTX structure is incomplete."
}
$ppt.Dispose()

$code = [System.IO.Compression.ZipFile]::OpenRead($codePath)
$csharpCount = @($code.Entries.FullName | Where-Object { $_ -like "*.cs" }).Count
$javaCount = @($code.Entries.FullName | Where-Object { $_ -like "*.java" }).Count
if ($csharpCount -eq 0 -or $javaCount -eq 0) {
    $code.Dispose()
    throw "Core code package is incomplete."
}
$code.Dispose()

$pdfBytes = [System.IO.File]::ReadAllBytes($posterPath)
$pdfHeader = [Text.Encoding]::ASCII.GetString($pdfBytes, 0, 8)
$pdfTail = [Text.Encoding]::ASCII.GetString($pdfBytes, $pdfBytes.Length - 32, 32)
if (-not $pdfHeader.StartsWith("%PDF-") -or -not $pdfTail.Contains("%%EOF")) {
    throw "Poster is not a complete PDF."
}

$video = [System.IO.File]::OpenRead($videoPath)
$videoHeader = New-Object byte[] 12
[void]$video.Read($videoHeader, 0, 12)
$video.Dispose()
if (-not [Text.Encoding]::ASCII.GetString($videoHeader).Contains("ftyp")) {
    throw "Video is not an MP4 container."
}

Write-Host "PASS APK $((Get-Item -LiteralPath $apkPath).Length) bytes"
Write-Host "PASS VIDEO $((Get-Item -LiteralPath $videoPath).Length) bytes"
Write-Host "PASS PPTX $slideCount slides"
Write-Host "PASS POSTER $((Get-Item -LiteralPath $posterPath).Length) bytes"
Write-Host "PASS CODE CSharp=$csharpCount Java=$javaCount"
