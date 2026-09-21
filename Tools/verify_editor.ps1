param(
    [Parameter(Mandatory)][ValidateSet('PrepareAuto', 'CheckAuto', 'RestoreAuto')][string]$Phase,
    [string]$UnityCli = "$env:LOCALAPPDATA\Unity\bin\unity.exe"
)
$ErrorActionPreference = 'Stop'
$fmvRoot = Split-Path -Parent $PSScriptRoot
$env:UNITY_NO_UPDATE_CHECK = '1'
# These phases intentionally leave normal Editor Play entry to the caller.
$fmvCode = switch ($Phase) {
    'PrepareAuto' { @'
if (UnityEditor.EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play first");
System.IO.Directory.CreateDirectory(".tools/auto-registration");
if (System.IO.File.Exists(".tools/auto-registration/original.json")) throw new System.InvalidOperationException("RestoreAuto first");
var s = UnityEditor.AssetDatabase.LoadAssetAtPath<FmvDemo.FmvSequenceDefinition>(FmvDemo.Editor.FmvProjectSetup.SequencePath);
System.IO.File.WriteAllText(".tools/auto-registration/original.json", UnityEditor.EditorJsonUtility.ToJson(s));
FmvDemo.Editor.FmvEditorTools.Local(); FmvDemo.Editor.FmvEditorTools.FastPlay();
var path = "Assets/FMV/Content/Videos/verification-wake.mp4";
if (!UnityEditor.AssetDatabase.CopyAsset("Assets/FMV/Content/Videos/wake.mp4", path)) throw new System.Exception("Copy failed");
UnityEditor.AssetDatabase.ImportAsset(path, UnityEditor.ImportAssetOptions.ForceSynchronousImport);
var guid = UnityEditor.AssetDatabase.AssetPathToGUID(path);
s.nodes[0].video = new FmvDemo.VideoReference(guid);
UnityEditor.EditorUtility.SetDirty(s); UnityEditor.AssetDatabase.SaveAssets();
if (FmvDemo.Editor.FmvContentPipeline.Settings.FindAssetEntry(guid) != null) throw new System.Exception("Fixture was registered before Play");
return new { unregistered = true, guid };
'@ }
    'CheckAuto' { @'
var p = UnityEngine.Object.FindFirstObjectByType<FmvDemo.FmvPlayer>();
var guid = UnityEditor.AssetDatabase.AssetPathToGUID("Assets/FMV/Content/Videos/verification-wake.mp4");
var entry = FmvDemo.Editor.FmvContentPipeline.Settings.FindAssetEntry(guid);
if (p == null || p.State != FmvDemo.FmvPlaybackState.AwaitingChoice || p.CurrentNode.video.AssetGUID != guid || p.Video.frame < 1 || entry == null) throw new System.Exception("Automatic Play registration or playback failed");
return new { registeredByPlay = true, entry.address, node = p.CurrentNode.id, frame = p.Video.frame, handles = p.Assets.ActiveHandleCount };
'@ }
    'RestoreAuto' { @'
if (UnityEditor.EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play first");
var s = UnityEditor.AssetDatabase.LoadAssetAtPath<FmvDemo.FmvSequenceDefinition>(FmvDemo.Editor.FmvProjectSetup.SequencePath);
UnityEditor.EditorJsonUtility.FromJsonOverwrite(System.IO.File.ReadAllText(".tools/auto-registration/original.json"), s);
UnityEditor.EditorUtility.SetDirty(s); UnityEditor.AssetDatabase.SaveAssets();
UnityEditor.AssetDatabase.DeleteAsset("Assets/FMV/Content/Videos/verification-wake.mp4");
FmvDemo.Editor.FmvEditorTools.Local(); FmvDemo.Editor.FmvEditorTools.FastPlay();
FmvDemo.Editor.FmvContentPipeline.Synchronize();
System.IO.File.Delete(".tools/auto-registration/original.json");
return new { restored = true };
'@ }
}
Push-Location $fmvRoot
try {
    $fmvRaw = & $UnityCli command eval --project-path $fmvRoot --caller plugin --skill unity-cli $fmvCode --timeout 180000 --format json
    $fmvResult = $fmvRaw | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or -not $fmvResult.success -or -not $fmvResult.data.result.success) { throw ($fmvRaw -join "`n") }
    New-Item -ItemType Directory -Path 'Docs/Evidence' -Force | Out-Null
    $fmvResult.data.result.result | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath "Docs/Evidence/editor-$Phase.json" -Encoding utf8
    $fmvResult.data.result.result | ConvertTo-Json -Depth 10
}
finally { Pop-Location }
