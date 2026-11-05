param([Parameter(Mandatory)][string]$OutputDirectory, [string]$Serial='emulator-5556')
$ErrorActionPreference='Stop'; $env:ANDROID_ADB_SERVER_PORT='54036'
$adb='C:/Users/fujunye/AppData/Local/Android/Sdk/platform-tools/adb.exe'; $serial=$Serial; $package='com.catlife.review.ui'
$inputTool=Join-Path $PSScriptRoot '../agent/Invoke-CatLifeExitReviewInput.ps1'
function Adb([string[]]$Arguments) { $v=& $adb -s $serial @Arguments; if($LASTEXITCODE -ne 0){throw 'ADB failed'}; return $v }
function Shot([string]$Tag) { & $inputTool -Action Snapshot -Tag $Tag -Package $package -Serial $serial -OutputDirectory $OutputDirectory }
function Epoch(){ [double]::Parse((Adb @('shell','date','+%s.%N')),[Globalization.CultureInfo]::InvariantCulture) }
if (((Adb @('emu','avd','name')) -join '').Trim() -notmatch '^CatLifeInterview20261008\s*OK$') { throw 'Wrong AVD' }
# Caller has read the Setup screenshot, with AI on, prior consent and one-minute duration.
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$appPid=(Adb @('shell','pidof',$package)).Trim(); $started=Epoch
$rec=Start-Process -FilePath $adb -ArgumentList "-s $serial shell screenrecord --bit-rate 3000000 --time-limit 180 /sdcard/catlife-cloud-review.mp4" -WindowStyle Hidden -PassThru -RedirectStandardOutput "$OutputDirectory/recorder.out.log" -RedirectStandardError "$OutputDirectory/recorder.err.log"
Shot 'setup-before-start'
& $inputTool -Action Swipe -Tag 'start-once' -X .654 -Y .93 -ToX .654 -ToY .93 -DurationMs 350 -Package $package -Serial $serial -OutputDirectory $OutputDirectory
$focus=0; $watch=[Diagnostics.Stopwatch]::StartNew()
while($watch.Elapsed.TotalSeconds -lt 60){
    foreach($line in (Adb @('logcat','-d',"--pid=$appPid",'-v','epoch','-s','Unity:I','*:S'))){
        if($line -match '^\s*(\d+\.\d+).*focus_enter source=manual'){ $focus=[double]::Parse($Matches[1],[Globalization.CultureInfo]::InvariantCulture) }
    }
    if($focus -ge $started){break}; Start-Sleep -Seconds 1
}
if($focus -lt $started){throw 'No new Focus; no further input sent.'}
Shot 'focus'
while((Epoch) -lt $focus+61){Start-Sleep -Seconds 5}
Shot 'reward-early'
Start-Sleep -Seconds 35
Shot 'reward-final'
$lines=Adb @('logcat','-d',"--pid=$appPid",'-v','epoch','-s','Unity','AndroidRuntime')
$lines | Set-Content "$OutputDirectory/player.log" -Encoding utf8
$requests=@($lines | Where-Object { $_ -match '^\s*(\d+\.\d+).*\[CatLife\] llm_request ' -and [double]::Parse($Matches[1],[Globalization.CultureInfo]::InvariantCulture) -ge $started })
[pscustomobject]@{Pid=$appPid;StartedEpoch=$started;FocusEpoch=$focus;Requests=$requests.Count;RecorderPid=$rec.Id;Scope='One new completed session; do not retry failures.'} | ConvertTo-Json | Set-Content "$OutputDirectory/run.json" -Encoding utf8
Write-Output 'Read the reward, logs and completed recording before accepting the cloud gate.'
