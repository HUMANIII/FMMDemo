param(
    [ValidateSet('All', 'EditMode', 'Fast', 'Packed')][string]$Mode = 'All',
    [string]$UnityEditor = 'E:\UnityHubs\6000.3.23f1\Editor\Unity.exe'
)
$ErrorActionPreference = 'Stop'
$fmvRoot = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path -LiteralPath $UnityEditor)) { throw "Unity Editor not found: $UnityEditor" }
New-Item -ItemType Directory -Path (Join-Path $fmvRoot 'Logs'), (Join-Path $fmvRoot 'TestResults') -Force | Out-Null

function Invoke-FmvUnity([string]$TaskName, [string]$Arguments) {
    $fmvLog = Join-Path $fmvRoot "Logs\$TaskName.log"
    $fmvArgs = '-batchmode -projectPath "' + $fmvRoot + '" -logFile "' + $fmvLog + '" ' + $Arguments
    $fmvProcess = Start-Process -FilePath $UnityEditor -ArgumentList $fmvArgs -WindowStyle Hidden -PassThru -Wait
    if ($fmvProcess.ExitCode -ne 0) { throw "$TaskName failed ($($fmvProcess.ExitCode)). See $fmvLog" }
}

function Invoke-FmvTests([string]$Platform, [string]$Name) {
    $fmvReport = Join-Path $fmvRoot "TestResults\$Name.xml"
    $fmvAssembly = if ($Platform -eq 'EditMode') { 'FmvDemo.EditModeTests' } else { 'FmvDemo.PlayModeTests' }
    Invoke-FmvUnity $Name ('-runTests -testPlatform ' + $Platform + ' -assemblyNames ' + $fmvAssembly + ' -testResults "' + $fmvReport + '"')
    [xml]$fmvXml = Get-Content -LiteralPath $fmvReport
    $fmvXml.'test-run' | Select-Object result, total, passed, failed, duration
    if ($fmvXml.'test-run'.result -ne 'Passed') { throw "Failed test report: $fmvReport" }
}

# Close this project's interactive Editor before starting. Never kill another Editor.
Invoke-FmvUnity 'verify-local' '-executeMethod FmvDemo.Editor.FmvEditorTools.Local -quit'
try {
    if ($Mode -in @('All', 'EditMode')) { Invoke-FmvTests 'EditMode' 'editmode' }
    if ($Mode -in @('All', 'Fast')) {
        Invoke-FmvUnity 'verify-fast-mode' '-executeMethod FmvDemo.Editor.FmvEditorTools.FastPlay -quit'
        Invoke-FmvTests 'PlayMode' 'playmode-fast'
    }
    if ($Mode -in @('All', 'Packed')) {
        Invoke-FmvUnity 'build-local' '-executeMethod FmvDemo.Editor.FmvEditorTools.BuildContent -quit'
        Invoke-FmvUnity 'verify-packed-mode' '-executeMethod FmvDemo.Editor.FmvEditorTools.PackedPlay -quit'
        Invoke-FmvTests 'PlayMode' 'playmode-packed'
    }
}
finally {
    Invoke-FmvUnity 'restore-fast-mode' '-executeMethod FmvDemo.Editor.FmvEditorTools.FastPlay -quit'
}
