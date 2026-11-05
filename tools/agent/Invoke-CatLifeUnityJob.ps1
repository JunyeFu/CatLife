[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Start', 'Status')][string]$Action,
    [Parameter(Mandatory)][ValidatePattern('^[a-zA-Z0-9][a-zA-Z0-9_-]*$')][string]$RunName,
    [ValidateSet('Test', 'Execute')][string]$Mode = 'Test',
    [ValidateSet('EditMode', 'PlayMode')][string]$Platform = 'EditMode',
    [string]$Filter,
    [string]$ExecuteMethod,
    [string[]]$ExecuteArguments = @(),
    [string]$UnityExe = 'F:/AgentTools/UnityEngine/6000.4.9f1/Editor/Unity.exe',
    [string]$UnityProjectPath
)
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$projectPath = if ($UnityProjectPath) { (Resolve-Path -LiteralPath $UnityProjectPath).Path } else { Join-Path $repoRoot 'work/CatLife_Unity_Main' }
$runFolder = Join-Path $repoRoot ('Reports/agent-runs/' + $RunName)
$jobPath = Join-Path $runFolder 'job.json'

if ($Action -eq 'Start') {
    if (Test-Path -LiteralPath $runFolder) {
        throw "Run already exists. Read it with -Action Status -RunName $RunName; use a new name for a deliberate new run."
    }
    # A second Editor cannot open the same project; do not kill the user's Editor.
    $editors = @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" |
        Where-Object { $_.CommandLine -and $_.CommandLine.Replace('\', '/').Contains($projectPath.Replace('\', '/')) })
    if ($editors.Count) {
        [pscustomobject]@{ State = 'editor_in_use'; Pids = @($editors.ProcessId); Project = $projectPath } | ConvertTo-Json -Compress
        exit 2
    }
    if ($Mode -eq 'Test' -and !$Filter) { throw 'Choose the relevant test filter.' }
    if ($Mode -eq 'Execute' -and !$ExecuteMethod) { throw 'ExecuteMethod is required for Execute mode.' }
    New-Item -ItemType Directory -Path $runFolder | Out-Null
    $resultPath = Join-Path $runFolder 'tests.xml'
    $logPath = Join-Path $runFolder 'unity.log'
    $arguments = @('-batchmode', '-projectPath', $projectPath, '-logFile', $logPath)
    if ($Mode -eq 'Test') {
        $arguments += @('-runTests', '-testPlatform', $Platform, '-testFilter', $Filter, '-testResults', $resultPath)
        # Test runner owns shutdown; -quit can terminate it before results exist.
    } else {
        $arguments += @('-executeMethod', $ExecuteMethod, '-quit')
        $arguments += $ExecuteArguments
    }
    if (@($arguments | Where-Object { $_.Contains('"') }).Count) { throw 'Double quotes are not supported in task arguments.' }
    $quoted = ($arguments | ForEach-Object { '"' + $_ + '"' }) -join ' '
    $process = Start-Process -FilePath $UnityExe -ArgumentList $quoted -WorkingDirectory $repoRoot -WindowStyle Hidden -PassThru
    $job = [ordered]@{
        Run = $RunName; Mode = $Mode; Platform = $Platform; Filter = $Filter; ExecuteMethod = $ExecuteMethod
        Pid = $process.Id; StartedUtc = $process.StartTime.ToUniversalTime().ToString('o')
        Project = $projectPath; Log = $logPath; Result = $resultPath
    }
    $job | ConvertTo-Json | Set-Content -LiteralPath $jobPath -Encoding utf8
    [pscustomobject]@{ State = 'running'; Pid = $process.Id; Job = $jobPath; Log = $logPath; Result = $resultPath } | ConvertTo-Json -Compress
    exit 0
}

$job = Get-Content -LiteralPath $jobPath -Raw | ConvertFrom-Json
$process = Get-Process -Id $job.Pid -ErrorAction SilentlyContinue
$live = $process -and $process.ProcessName -eq 'Unity' -and
    $process.StartTime.ToUniversalTime().Ticks -eq ([datetime]$job.StartedUtc).ToUniversalTime().Ticks
$state = if ($live) { 'running' } else { 'exited' }
$tests = $null
if ($job.Mode -eq 'Test' -and (Test-Path -LiteralPath $job.Result)) {
    [xml]$xml = Get-Content -LiteralPath $job.Result -Raw
    $run = $xml.'test-run'
    $tests = [ordered]@{ Result = [string]$run.result; Total = [int]$run.total; Passed = [int]$run.passed; Failed = [int]$run.failed }
    if (!$live) { $state = if ($run.result -eq 'Passed' -and [int]$run.total -gt 0) { 'tests_passed' } else { 'tests_failed' } }
} elseif (!$live -and $job.Mode -eq 'Test') {
    $state = 'exited_without_results'
}
# Execute completion must be read from its specific artifact/build report, not the launch exit code.
[pscustomobject]@{ State = $state; Pid = $job.Pid; Tests = $tests; Project = $job.Project; Log = $job.Log; Result = $job.Result } | ConvertTo-Json -Depth 4 -Compress
