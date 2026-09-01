param(
    [string]$UnityExe = "D:\UnityEngine\6000.4.9f1\Editor\Unity.exe",
    [string]$ProjectPath = "D:\Agent\AIGC innovation\work\CatLife_Unity_Main"
)

$ErrorActionPreference = "Stop"
$reportRoot = Join-Path $ProjectPath "Reports\MobileRebuild\art-standardization-20260901"
$buildRoot = Join-Path $reportRoot "build"
New-Item -ItemType Directory -Force -Path $reportRoot, $buildRoot | Out-Null

function Invoke-UnityGate {
    param([string]$Name, [string[]]$Arguments)
    $logPath = Join-Path $reportRoot ($Name + ".log")
    $processInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $processInfo.FileName = $UnityExe
    $processInfo.UseShellExecute = $false
    $processInfo.CreateNoWindow = $true
    foreach ($argument in @("-batchmode", "-quit", "-projectPath", $ProjectPath) + $Arguments + @("-logFile", $logPath)) {
        $processInfo.ArgumentList.Add($argument)
    }
    $startedAt = Get-Date
    [System.Diagnostics.Process]::Start($processInfo) | Out-Null
    Start-Sleep -Seconds 3
    while (Get-Process Unity -ErrorAction SilentlyContinue) {
        Start-Sleep -Seconds 2
    }
    if (!(Test-Path -LiteralPath $logPath) -or (Get-Item -LiteralPath $logPath).LastWriteTime -lt $startedAt -or (Select-String -Path $logPath -Pattern "error CS\d+|FAIL CatLife|executeMethod class .* could not be found|return code 1|CATLIFE_ANDROID_BUILD result=(?!Succeeded)")) {
        throw "Unity gate failed: $Name. See $logPath"
    }
}

Invoke-UnityGate "01-texture-policy" @("-executeMethod", "CatLifeMobileTexturePolicy.ApplyBatch")
Invoke-UnityGate "02-model-policy" @(
    "-executeMethod", "CatLife.Editor.CatLifeModelImportPolicy.ApplyModelImportPolicyBatch",
    "-reportDir", (Join-Path $reportRoot "model-policy")
)
Invoke-UnityGate "03-scene-build" @("-executeMethod", "CatLifeMobileSceneBuilder.BuildBatch")
Invoke-UnityGate "04-validator" @("-executeMethod", "CatLife.Editor.CatLifeMobileBuildValidator.ValidateBatch")
Invoke-UnityGate "05-preview" @("-executeMethod", "CatLifeMobilePreviewRenderer.RenderBatch")
Invoke-UnityGate "06-editmode-tests" @(
    "-runTests", "-testPlatform", "EditMode",
    "-testResults", (Join-Path $reportRoot "editmode-results.xml")
)
[xml]$testResults = Get-Content -LiteralPath (Join-Path $reportRoot "editmode-results.xml")
if ($testResults.'test-run'.result -ne "Passed") {
    throw "Unity EditMode tests failed. See $(Join-Path $reportRoot 'editmode-results.xml')"
}

$apkPath = Join-Path $buildRoot "CatLife-standardized-release.apk"
Invoke-UnityGate "07-release-build" @(
    "-executeMethod", "CatLife.Editor.CatLifeAndroidBuild.BuildApk",
    "-outputPath", $apkPath, "-release"
)

$apk = Get-Item -LiteralPath $apkPath
if ($apk.Length -gt 120MB) {
    throw "Release APK exceeds 120 MiB: $([math]::Round($apk.Length / 1MB, 2)) MiB"
}
Write-Output "PASS Unity hard gates. APK=$apkPath MiB=$([math]::Round($apk.Length / 1MB, 2))"
