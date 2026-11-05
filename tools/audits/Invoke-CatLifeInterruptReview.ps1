param([Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'; $env:ANDROID_ADB_SERVER_PORT='54036'
$adb='C:/Users/fujunye/AppData/Local/Android/Sdk/platform-tools/adb.exe'; $serial='emulator-5556'; $package='com.catlife.review.ui'
$inputTool=Join-Path $PSScriptRoot '../agent/Invoke-CatLifeExitReviewInput.ps1'
function Adb([string[]]$Arguments) { $v=& $adb -s $serial @Arguments; if($LASTEXITCODE -ne 0){throw 'ADB failed'}; return $v }
function Tap([string]$Tag,[double]$X,[double]$Y) { & $inputTool -Action Swipe -Tag $Tag -X $X -Y $Y -ToX $X -ToY $Y -DurationMs 350 -Package $package -OutputDirectory $OutputDirectory }
function Shot([string]$Tag) { & $inputTool -Action Snapshot -Tag $Tag -Package $package -OutputDirectory $OutputDirectory }
function Epoch(){ [double]::Parse((Adb @('shell','date','+%s.%N')),[Globalization.CultureInfo]::InvariantCulture) }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$appPid=(Adb @('shell','pidof',$package)).Trim(); $started=Epoch
$rec=Start-Process -FilePath $adb -ArgumentList "-s $serial shell screenrecord --size 1080x2424 --bit-rate 3000000 --time-limit 90 /sdcard/catlife-r37-interrupt-confirmed.mp4" -WindowStyle Hidden -PassThru -RedirectStandardOutput "$OutputDirectory/recorder.out.log" -RedirectStandardError "$OutputDirectory/recorder.err.log"
# Caller supplies the observed Reward page with AI off and one-minute default.
Tap 'reward-home' .272 .934; Tap 'setup' .5 .875; Tap 'start' .654 .93
$focus=0; $watch=[Diagnostics.Stopwatch]::StartNew()
while($watch.Elapsed.TotalSeconds -lt 45){
    foreach($line in (Adb @('logcat','-d',"--pid=$appPid",'-v','epoch','-s','Unity:I','*:S'))){
        if($line -match '^\s*(\d+\.\d+).*focus_enter source=manual'){ $focus=[double]::Parse($Matches[1],[Globalization.CultureInfo]::InvariantCulture) }
    }
    if($focus -ge $started){break}; Start-Sleep -Milliseconds 250
}
if($focus -lt $started){throw 'No new Focus; no confirmation sent.'}
& $inputTool -Action Swipe -Tag 'exit-drag' -X .24 -Y .603 -ToX .94 -ToY .603 -DurationMs 2500 -Package $package -OutputDirectory $OutputDirectory
Shot 'confirm-before-deadline'
if((Epoch)-$focus -ge 35){throw 'Confirmation window passed; preserve the session.'}
Tap 'confirm-interrupt' .71 .9
$confirmed=Epoch
Shot 'home-after-interrupt'; Tap 'records' .889 .731; Shot 'records-after-interrupt'
Tap 'records-back' .093 .06; Tap 'growth' .889 .661; Shot 'growth-after-interrupt'
[pscustomobject]@{Pid=$appPid;FocusEpoch=$focus;ConfirmEpoch=$confirmed;SecondsAtConfirm=$confirmed-$focus;RecorderPid=$rec.Id} | ConvertTo-Json | Set-Content "$OutputDirectory/run.json" -Encoding utf8
