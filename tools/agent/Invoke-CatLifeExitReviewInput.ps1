[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Inspect','Snapshot','Tap','Swipe','PressBackground','PressDeadline')][string]$Action,
    [Parameter(Mandatory)][ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$Tag,
    [float]$X, [float]$Y, [float]$ToX, [float]$ToY,
    [int]$DurationMs = 2500,
    [int]$TapCount = 1,
    [double]$DeadlineEpoch,
    [string]$OutputDirectory,
    [string]$Package = 'com.catlife.review.wp04',
    [string]$Serial = 'emulator-5556'
)
$ErrorActionPreference = 'Stop'
$adbExe = 'C:/Users/fujunye/AppData/Local/Android/Sdk/platform-tools/adb.exe'
$reviewSerial = $Serial
$reviewPackage = $Package
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$outputRoot = if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $repoRoot 'Reports/wp04-android-review-r22-20261017' }
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
function Adb([string[]]$Arguments) {
    $result = & $adbExe -s $reviewSerial @Arguments
    if ($LASTEXITCODE -ne 0) { throw ('adb failed: ' + ($Arguments -join ' ')) }
    return $result
}
$avd = Adb -Arguments @('shell','getprop','ro.boot.qemu.avd_name')
if (($avd -join '').Trim() -ne 'CatLifeInterview20261008') { throw 'Not the dedicated CatLife AVD.' }
$dump = & $adbExe -s $reviewSerial shell uiautomator dump /sdcard/catlife-wp04-review-ui.xml 2>&1
if ($LASTEXITCODE -ne 0 -or ($dump -join "`n") -match 'ERROR:') {
    throw 'UI hierarchy capture failed; no input was sent and the previous XML was not reused.'
}
$uiPath = Join-Path $outputRoot ($Tag + '-ui.xml')
Adb -Arguments @('pull','/sdcard/catlife-wp04-review-ui.xml',$uiPath) | Out-Null
[xml]$ui = Get-Content -LiteralPath $uiPath -Raw
$surface = $ui.SelectSingleNode("//node[@resource-id='$($reviewPackage):id/unitySurfaceView' and @package='$reviewPackage']")
if (!$surface) { throw 'Review Unity surface node not found; no input was sent.' }
$edges = @([regex]::Matches($surface.bounds,'\d+') | ForEach-Object { [int]$_.Value })
$width = $edges[2]-$edges[0]; $height = $edges[3]-$edges[1]
$pointX = [int]($edges[0]+$X*$width); $pointY = [int]($edges[1]+$Y*$height)
$endX = [int]($edges[0]+$ToX*$width); $endY = [int]($edges[1]+$ToY*$height)
switch ($Action) {
    'Tap' { for ($tap=0; $tap -lt $TapCount; $tap++) { Adb -Arguments @('shell','input','tap',"$pointX","$pointY") | Out-Null } }
    'Swipe' { Adb -Arguments @('shell','input','swipe',"$pointX","$pointY","$endX","$endY","$DurationMs") | Out-Null }
    'PressBackground' {
        # Preserve a real pressed pointer until Home changes focus; CANCEL then clears the OS stream.
        Adb -Arguments @('shell',"input touchscreen motionevent DOWN $pointX $pointY; sleep .12; input touchscreen motionevent MOVE $endX $endY; sleep .55; input keyevent KEYCODE_HOME; input touchscreen motionevent CANCEL") | Out-Null
    }
    'PressDeadline' {
        $nearX = [int]($pointX + .34*.84*.76*$width)
        Adb -Arguments @('shell','input','touchscreen','motionevent','DOWN',"$pointX","$pointY") | Out-Null
        Adb -Arguments @('shell','input','touchscreen','motionevent','MOVE',"$nearX","$pointY") | Out-Null
        $deviceNow = [double]::Parse((Adb -Arguments @('shell','date','+%s.%N')), [Globalization.CultureInfo]::InvariantCulture)
        $wait = $DeadlineEpoch-.75-$deviceNow
        if ($wait -le 0) {
            Adb -Arguments @('shell','input','touchscreen','motionevent','CANCEL') | Out-Null
            throw 'Deadline contact window already passed; no race evidence.'
        }
        Start-Sleep -Seconds $wait
        Adb -Arguments @('shell','input','touchscreen','motionevent','MOVE',"$endX","$endY") | Out-Null
        Write-Output ('Contact injection device epoch '+(Adb -Arguments @('shell','date','+%s.%N')))
        Start-Sleep -Seconds 2.3
        Adb -Arguments @('shell','input','touchscreen','motionevent','UP',"$endX","$endY") | Out-Null
    }
    'Snapshot' {
        Adb -Arguments @('shell','screencap','-p','/sdcard/catlife-wp04-review-screen.png') | Out-Null
        Adb -Arguments @('pull','/sdcard/catlife-wp04-review-screen.png',(Join-Path $outputRoot ($Tag+'.png'))) | Out-Null
    }
}
[pscustomobject]@{
    At = [datetime]::Now.ToString('o'); Action=$Action; Package=$reviewPackage; Surface=$surface.bounds
    Point=@($pointX,$pointY); End=@($endX,$endY); DurationMs=$DurationMs; TapCount=$TapCount; DeadlineEpoch=$DeadlineEpoch
} | ConvertTo-Json -Compress | Tee-Object -FilePath (Join-Path $outputRoot ($Tag+'-input.json'))
