param(
    [string]$ProjectRoot = (Resolve-Path "$PSScriptRoot\..\..").Path
)

$ErrorActionPreference = "Stop"

$finalDir = Join-Path $ProjectRoot "06-deliverables\final-submission"
$evidenceDir = Join-Path $finalDir "evidence"

$dirs = @(
    "00-build",
    "01-install",
    "02-runtime",
    "02-startup",
    "03-llm",
    "03-screenshots",
    "04-focus",
    "04-recordings",
    "05-review",
    "android\00-build",
    "android\01-install",
    "android\02-startup",
    "android\03-llm",
    "android\04-focus",
    "android\05-summary"
)

New-Item -ItemType Directory -Force -Path $finalDir | Out-Null
foreach ($dir in $dirs) {
    New-Item -ItemType Directory -Force -Path (Join-Path $evidenceDir $dir) | Out-Null
}

function New-TemplateFile {
    param(
        [string]$Path,
        [string[]]$Lines
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        Set-Content -LiteralPath $Path -Value $Lines -Encoding UTF8
        Write-Host "Created $Path"
    } else {
        Write-Host "Exists  $Path"
    }
}

New-TemplateFile -Path (Join-Path $evidenceDir "00-build\unity-build-settings.txt") -Lines @(
    "Unity version: TODO",
    "Build date: TODO",
    "Project path: TODO",
    "Scenes in build: TODO",
    "Package name: com.catlife.mvp",
    "Version: 0.1.0",
    "Scripting backend: TODO",
    "Target architecture: TODO",
    "Texture compression: TODO",
    "Signing: debug/release TODO"
)

New-TemplateFile -Path (Join-Path $evidenceDir "00-build\apk-sha256.txt") -Lines @(
    "APK file: TODO",
    "Size bytes: TODO",
    "SHA256: TODO"
)

New-TemplateFile -Path (Join-Path $evidenceDir "android\00-build\private_config_presence_redacted.txt") -Lines @(
    "Private config path: work/CatLife_Unity_Main/Assets/Resources/CatLifePrivate/vivo_cloud_credentials.json",
    "Exists: TODO",
    "Git ignored: TODO",
    "AppID: 2026414599",
    "AppKEY present: TODO",
    "AppKEY value: REDACTED"
)

New-TemplateFile -Path (Join-Path $evidenceDir "01-install\device-info.txt") -Lines @(
    "Test date: TODO",
    "Tester: TODO",
    "Device source: local device / vivo cloud device",
    "Device model: TODO",
    "Android version: TODO",
    "CPU ABI: TODO",
    "Screen resolution: TODO"
)

New-TemplateFile -Path (Join-Path $evidenceDir "02-runtime\smoke-test-notes.md") -Lines @(
    "# CatLife APK Smoke Test Notes",
    "",
    "| Case | Result | Evidence |",
    "|---|---|---|",
    "| Install | TODO | evidence/01-install/android-install.txt |",
    "| Launch | TODO | evidence/03-screenshots/launch.png |",
    "| Main town visible | TODO | evidence/03-screenshots/town-main.png |",
    "| Normal state | TODO | evidence/04-recordings/raw-device-recording.mp4 |",
    "| Transition state | TODO | evidence/04-recordings/raw-device-recording.mp4 |",
    "| Focus state | TODO | evidence/03-screenshots/focus-state.png |",
    "| Reward state | TODO | evidence/03-screenshots/reward-state.png |",
    "| 3 minute stability | TODO | evidence/02-runtime/android-runtime-logcat.txt |",
    "| vivo cloud or BlueLM evidence | TODO | evidence/android/03-llm/logcat_vivo_cloud_llm.txt |"
)

New-TemplateFile -Path (Join-Path $evidenceDir "05-review\manual-review-notes.md") -Lines @(
    "# CatLife Final Manual Review Notes",
    "",
    "| Item | Reviewer | Result | Notes |",
    "|---|---|---|---|",
    "| PPT opens and uses real screenshots | TODO | TODO | TODO |",
    "| Video plays and meets time/resolution limits | TODO | TODO | TODO |",
    "| Poster opens and is portrait 70cm x 150cm | TODO | TODO | TODO |",
    "| APK installs and launches | TODO | TODO | TODO |",
    "| Code package has no secrets | TODO | TODO | TODO |",
    "| Platform upload success screenshot saved | TODO | TODO | TODO |"
)

Write-Host "Evidence scaffold ready: $evidenceDir"
