param([Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$env:ANDROID_ADB_SERVER_PORT='54036'
$adb='C:/Users/fujunye/AppData/Local/Android/Sdk/platform-tools/adb.exe'
$serial='emulator-5556'; $package='com.catlife.review.ui'
$inputTool=Join-Path $PSScriptRoot '../agent/Invoke-CatLifeExitReviewInput.ps1'
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
function Adb([string[]]$Arguments) { $value=& $adb -s $serial @Arguments; if($LASTEXITCODE -ne 0){throw 'ADB operation failed'}; return $value }
function Shot([string]$Tag) { & $inputTool -Action Snapshot -Tag $Tag -Package $package -OutputDirectory $OutputDirectory }
function AppPid() { (Adb @('shell','pidof',$package)).Trim() }
function Epoch() { [double]::Parse((Adb @('shell','date','+%s.%N')),[Globalization.CultureInfo]::InvariantCulture) }
if (((Adb @('emu','avd','name')) -join '').Trim() -notmatch '^CatLifeInterview20261008\s*OK$') { throw 'Wrong AVD' }
Adb @('shell','am','force-stop',$package) | Out-Null
$started=Epoch
$rec=Start-Process -FilePath $adb -ArgumentList "-s $serial shell screenrecord --size 1080x2424 --bit-rate 3000000 --time-limit 180 /sdcard/catlife-r37-recovery.mp4" -WindowStyle Hidden -PassThru -RedirectStandardOutput "$OutputDirectory/recovery-recorder.out.log" -RedirectStandardError "$OutputDirectory/recovery-recorder.err.log"
Adb @('shell','am','start','-n',"$package/com.unity3d.player.UnityPlayerGameActivity") | Out-Null
Start-Sleep -Seconds 6
$initialPid=AppPid; $focus=0
$watch=[Diagnostics.Stopwatch]::StartNew()
while($watch.Elapsed.TotalSeconds -lt 60) {
    $lines=Adb @('logcat','-d',"--pid=$initialPid",'-v','epoch','-s','Unity:I','*:S')
    foreach($line in $lines) { if($line -match '^\s*(\d+\.\d+).*focus_enter source=manual') { $focus=[double]::Parse($Matches[1],[Globalization.CultureInfo]::InvariantCulture) } }
    if($focus -ge $started){break}; Start-Sleep -Seconds 1
}
if($focus -lt $started){throw 'No new Focus entry after restoring the pending Transition; do not restart another session.'}
Shot 'r37-focus-before-background'
Adb @('shell','input','keyevent','KEYCODE_HOME') | Out-Null; Start-Sleep -Seconds 3
Adb @('shell','am','start','-n',"$package/com.unity3d.player.UnityPlayerGameActivity") | Out-Null
Start-Sleep -Seconds 2; Shot 'r37-focus-after-background'
Adb @('logcat','-d',"--pid=$initialPid",'-v','epoch','-s','Unity','AndroidRuntime') | Set-Content "$OutputDirectory/pre-restart-player.log" -Encoding utf8
Adb @('shell','am','force-stop',$package) | Out-Null; Start-Sleep -Seconds 2
Adb @('shell','am','start','-n',"$package/com.unity3d.player.UnityPlayerGameActivity") | Out-Null
$restoredAt=[datetime]::UtcNow.ToString('o'); Start-Sleep -Seconds 6
$restoredPid=AppPid; Shot 'r37-focus-after-process-restore'
[pscustomobject]@{StartedEpoch=$started;FocusEpoch=$focus;InitialPid=$initialPid;RestoredPid=$restoredPid;RestoredAtUtc=$restoredAt;RecorderPid=$rec.Id} | ConvertTo-Json | Set-Content "$OutputDirectory/recovery-run.json" -Encoding utf8
while((Epoch) -lt $focus+65){Write-Output 'Waiting for the original one-minute deadline; no pause or AI input.'; Start-Sleep -Seconds 5}
Shot 'r37-offline-reward'
Adb @('logcat','-d',"--pid=$restoredPid",'-v','epoch','-s','Unity','AndroidRuntime') | Set-Content "$OutputDirectory/restored-player.log" -Encoding utf8
Write-Output 'Recovery scenario captured; inspect screenshots before calling it passed. Recorder continues to its 180-second limit.'
