param([Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'; $env:ANDROID_ADB_SERVER_PORT='54036'
$adb='C:/Users/fujunye/AppData/Local/Android/Sdk/platform-tools/adb.exe'; $serial='emulator-5556'; $package='com.catlife.review.ui'
$inputTool=Join-Path $PSScriptRoot '../agent/Invoke-CatLifeExitReviewInput.ps1'
function Adb([string[]]$Arguments){ $value=& $adb -s $serial @Arguments; if($LASTEXITCODE -ne 0){throw 'ADB failed'}; return $value }
function Epoch(){ [double]::Parse((Adb @('shell','date','+%s.%N')),[Globalization.CultureInfo]::InvariantCulture) }
function Tap([string]$Tag,[double]$X,[double]$Y){ & $inputTool -Action Tap -Tag $Tag -X $X -Y $Y -Package $package -OutputDirectory $OutputDirectory | Out-Null }
function Shot([string]$Tag){ & $inputTool -Action Snapshot -Tag $Tag -Package $package -OutputDirectory $OutputDirectory | Out-Null }
function NewEvents([double]$Since,[string]$Pattern){
    @(Adb @('logcat','-d',"--pid=$appPid",'-v','epoch','-s','Unity:I','*:S') | Where-Object {
        $_ -match '^\s*(\d+\.\d+)' -and [double]::Parse($Matches[1],[Globalization.CultureInfo]::InvariantCulture) -ge $Since -and $_ -match $Pattern
    })
}
if(((Adb @('emu','avd','name')) -join '').Trim() -notmatch '^CatLifeInterview20261008\s*OK$'){throw 'Wrong AVD'}
# Caller read Settings: AI off, statistics on, auto-focus off, one-minute default, 1080x2424.
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$appPid=(Adb @('shell','pidof',$package)).Trim(); $results=@()
foreach($seconds in @(30,60,120)){
    Tap "$seconds-setting" .5 .493
    Shot "$seconds-setting"
    Tap "$seconds-settings-back" .093 .06
    if($seconds -eq 30){
        Tap 'records-open' .889 .731; $recordsStart=Epoch
        Start-Sleep -Seconds 35
        if((NewEvents $recordsStart 'auto_transition|focus_enter').Count){throw 'Auto-focus incorrectly started on Records'}
        Shot 'records-no-auto'; Tap 'records-back' .093 .06
    }
    $homeStart=Epoch; $watch=[Diagnostics.Stopwatch]::StartNew(); $focus=@()
    Write-Output "Waiting for actual automatic Focus at configured $seconds seconds."
    while($watch.Elapsed.TotalSeconds -lt $seconds+60){
        $focus=@(NewEvents $homeStart 'focus_enter source=auto')
        if($focus.Count){break}; Start-Sleep -Seconds 3
    }
    if($focus.Count -ne 1){throw "Expected one automatic Focus at $seconds seconds"}
    [void]($focus[0] -match '^\s*(\d+\.\d+)'); $focusEpoch=[double]::Parse($Matches[1],[Globalization.CultureInfo]::InvariantCulture)
    Shot "$seconds-focus"
    & $inputTool -Action Swipe -Tag "$seconds-exit" -X .24 -Y .603 -ToX .94 -ToY .603 -DurationMs 2500 -Package $package -OutputDirectory $OutputDirectory | Out-Null
    Shot "$seconds-confirm"
    if((Epoch)-$focusEpoch -ge 35){throw 'Exit confirmation is too near the deadline; preserve session'}
    Tap "$seconds-interrupt" .71 .9; Shot "$seconds-home"
    Tap "$seconds-growth" .889 .661; Shot "$seconds-growth"; Tap "$seconds-growth-back" .093 .06
    $results+= [pscustomobject]@{ConfiguredSeconds=$seconds;HomeObservedEpoch=$homeStart;FocusEpoch=$focusEpoch;ElapsedSinceHomeObservation=$focusEpoch-$homeStart;AutoFocusEvents=$focus.Count;NewModelRequests=(NewEvents $homeStart '\[CatLife\] llm_request ').Count}
    $results | ConvertTo-Json | Set-Content "$OutputDirectory/results.json" -Encoding utf8
    Tap "$seconds-open-settings" .889 .8
}
Tap 'auto-off' .5 .493; Shot 'settings-auto-off'
Adb @('logcat','-d',"--pid=$appPid",'-v','epoch','-s','Unity','AndroidRuntime') | Set-Content "$OutputDirectory/player.log" -Encoding utf8
Write-Output 'Read all setting, Focus, confirmation, Home and growth screenshots before accepting the matrix.'
