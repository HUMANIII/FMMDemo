param(
    [ValidateSet('All','Build','Remote')][string]$Mode = 'All',
    [string]$UnityEditor = 'E:\UnityHubs\6000.3.23f1\Editor\Unity.exe'
)
$ErrorActionPreference = 'Stop'
$fmvRoot = Split-Path -Parent $PSScriptRoot
function Invoke-FmvBatch([string]$Name, [string]$Arguments) {
    $fmvArgs = '-batchmode -projectPath "' + $fmvRoot + '" -logFile "' + $fmvRoot + '\Logs\' + $Name + '.log" ' + $Arguments
    $fmvProcess = Start-Process -FilePath $UnityEditor -ArgumentList $fmvArgs -WindowStyle Hidden -PassThru -Wait
    if ($fmvProcess.ExitCode -ne 0) { throw "$Name failed ($($fmvProcess.ExitCode)). See Logs/$Name.log" }
}
Push-Location $fmvRoot
try {
    New-Item -ItemType Directory -Path Logs,TestResults -Force | Out-Null
    if ($Mode -in @('All','Build')) {
        try { Invoke-FmvBatch 'build-and-stale' '-executeMethod FmvDemo.Editor.FmvBatchVerification.BuildAndStale' }
        finally { Invoke-FmvBatch 'restore-auto' '-executeMethod FmvDemo.Editor.FmvBatchVerification.RestoreAuto -quit' }
    }
    if ($Mode -in @('All','Remote')) {
        # Start Tools/remote_test_server.py on loopback port 18084 before this phase.
        if ((Invoke-RestMethod 'http://127.0.0.1:18084/health') -ne 'FMV fixture') { throw 'Loopback fixture not available' }
        try {
            Invoke-FmvBatch 'remote-prepare' '-executeMethod FmvDemo.Editor.FmvBatchVerification.PrepareRemote'
            Invoke-FmvBatch 'remote-playmode' ('-runTests -testPlatform PlayMode -assemblyNames FmvDemo.RemoteTests -testResults "' + $fmvRoot + '\TestResults\remote-playmode.xml"')
            [xml]$fmvReport = Get-Content -LiteralPath 'TestResults/remote-playmode.xml'
            if ($fmvReport.'test-run'.result -ne 'Passed' -or [int]$fmvReport.'test-run'.passed -ne 1) { throw 'Remote playback test did not pass' }
            Copy-Item -LiteralPath '.tools/remote-verification/ready.json' -Destination 'Docs/Evidence/remote-protocol.json' -Force
        }
        finally { Invoke-FmvBatch 'remote-restore' '-executeMethod FmvDemo.Editor.FmvRemoteVerification.Restore -quit' }
    }
}
finally { Pop-Location }
