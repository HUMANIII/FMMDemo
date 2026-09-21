using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.Networking;

namespace FmvDemo.Editor
{
    [InitializeOnLoad]
    public static class FmvEditorTools
    {
        private static bool uploadRunning;
        static FmvEditorTools() { EditorApplication.playModeStateChanged += OnPlayMode; }
        private static void OnPlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode) return;
            try
            {
                FmvContentPipeline.Synchronize();
                if (FmvContentPipeline.Settings.ActivePlayModeDataBuilder is UnityEditor.AddressableAssets.Build.DataBuilders.BuildScriptPackedPlayMode)
                    FmvContentPipeline.RequireCurrentBuild();
            }
            catch (Exception error) { Debug.LogError("[FMV] " + error.Message); EditorApplication.isPlaying = false; }
        }

        [MenuItem("Tools/FMV/Synchronize and Validate")]
        public static void Synchronize() { FmvContentPipeline.Synchronize(); Debug.Log("[FMV] 콘텐츠 동기화 및 검증 완료"); }

        [MenuItem("Tools/FMV/Validate Only")]
        public static void ValidateOnly()
        {
            var errors = FmvContentPipeline.ValidateContent();
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
            Debug.Log("[FMV] 읽기 전용 검증 통과");
        }

        [MenuItem("Tools/FMV/Build Content")]
        public static void BuildContent()
        {
            FmvContentPipeline.Synchronize();
            AddressableAssetSettings.BuildPlayerContent(out var result);
            if (!string.IsNullOrEmpty(result.Error)) throw new InvalidOperationException(result.Error);
            Debug.Log("[FMV] 콘텐츠 빌드 완료");
        }

        [MenuItem("Tools/FMV/Profile/Local")]
        public static void Local() => UseProfile("Local");
        [MenuItem("Tools/FMV/Profile/Test")]
        public static void Test() => UseProfile("Test");
        [MenuItem("Tools/FMV/Profile/Release")]
        public static void Release() => UseProfile("Release");
        public static void UseProfile(string name)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Play 모드를 종료한 뒤 프로필을 변경하세요.");
            FmvContentPipeline.EnsureSettings();
            string id = FmvContentPipeline.Settings.profileSettings.GetProfileId(name);
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("알 수 없는 프로필: " + name);
            FmvContentPipeline.Settings.activeProfileId = id;
            FmvContentPipeline.ConfigureProfile();
            EditorUtility.SetDirty(FmvContentPipeline.Settings);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/FMV/Play Mode/Asset Database")]
        public static void FastPlay() => SetPlayMode(false);
        [MenuItem("Tools/FMV/Play Mode/Existing Build")]
        public static void PackedPlay() => SetPlayMode(true);
        public static void SetPlayMode(bool packed)
        {
            FmvContentPipeline.EnsureSettings();
            var settings = FmvContentPipeline.Settings;
            settings.ActivePlayModeDataBuilderIndex = settings.DataBuilders.FindIndex(b => packed ? b is FmvPackedPlayMode : b is FmvFastMode);
            EditorUtility.SetDirty(settings); AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/FMV/Clear FMV Cache (Play Mode)")]
        public static async void ClearCache()
        {
            try
            {
                foreach (var player in UnityEngine.Object.FindObjectsByType<FmvPlayer>(FindObjectsSortMode.None)) player.StopSequence();
                await FmvRemoteContent.ClearCache(CancellationToken.None);
                Debug.Log("[FMV] FMV 의존성 캐시 삭제 완료");
            }
            catch (Exception error) { Debug.LogException(error); }
        }
        [MenuItem("Tools/FMV/Clear FMV Cache (Play Mode)", true)]
        private static bool CanClearCache() => EditorApplication.isPlaying;

        [MenuItem("Tools/FMV/Upload/Build and Upload Test")]
        public static async void BuildUploadTest() => await UploadMenu("Test", true);
        [MenuItem("Tools/FMV/Upload/Build and Upload Release")]
        public static async void BuildUploadRelease() => await UploadMenu("Release", true);
        [MenuItem("Tools/FMV/Upload/Upload Test")]
        public static async void UploadTest() => await UploadMenu("Test", false);
        [MenuItem("Tools/FMV/Upload/Upload Release")]
        public static async void UploadRelease() => await UploadMenu("Release", false);
        private static async Task UploadMenu(string profile, bool build)
        {
            try { await BuildAndUpload(profile, build, CancellationToken.None); }
            catch (Exception error) { Debug.LogException(error); }
        }

        public static async Task BuildAndUpload(string profile, bool build, CancellationToken token)
        {
            if (uploadRunning || EditorApplication.isPlaying) throw new InvalidOperationException("업로드 중이거나 Play 모드에서는 시작할 수 없습니다.");
            var endpoint = FmvDeploymentSettings.instance.Endpoint(profile);
            ValidateUploadUrl(endpoint.uploadUrl);
            FmvContentPipeline.EnsureSettings();
            string previous = FmvContentPipeline.Settings.profileSettings.GetProfileName(FmvContentPipeline.Settings.activeProfileId);
            uploadRunning = true;
            try
            {
                token.ThrowIfCancellationRequested();
                UseProfile(profile);
                FmvContentPipeline.Synchronize();
                if (build) BuildContent();
                var receipt = FmvContentPipeline.RequireCurrentBuild();
                string folder = Path.GetFullPath(FmvContentPipeline.Settings.profileSettings.EvaluateString(
                    FmvContentPipeline.Settings.activeProfileId, FmvContentPipeline.Settings.profileSettings.GetValueByName(FmvContentPipeline.Settings.activeProfileId, "Remote.BuildPath")));
                var files = receipt.files.Where(f => f.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                    (f.EndsWith(".bundle", StringComparison.OrdinalIgnoreCase) || Path.GetFileName(f).StartsWith("catalog_", StringComparison.Ordinal)))
                    .OrderBy(UploadOrder).ThenBy(f => f, StringComparer.Ordinal).ToArray();
                if (!files.Any(f => f.EndsWith(".bundle")) || !files.Any(f => f.EndsWith(".hash"))) throw new InvalidOperationException("원격 빌드 파일이 완전하지 않습니다.");
                await UploadFiles(files, endpoint, token);
                Debug.Log($"[FMV] {profile} 업로드 완료 ({files.Length} files)");
            }
            finally { try { UseProfile(previous); } finally { uploadRunning = false; } }
        }

        public static int UploadOrder(string path) => path.EndsWith(".bundle", StringComparison.OrdinalIgnoreCase) ? 0 : path.EndsWith(".hash", StringComparison.OrdinalIgnoreCase) ? 2 : 1;
        private static void ValidateUploadUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
                throw new InvalidOperationException("Project Settings > FMV Deployment에서 업로드 URL을 설정하세요.");
        }
        public static async Task UploadFiles(string[] files, FmvEndpoint endpoint, CancellationToken token)
        {
            ValidateUploadUrl(endpoint.uploadUrl);
            foreach (string file in files.OrderBy(UploadOrder).ThenBy(f => f, StringComparer.Ordinal))
            {
                token.ThrowIfCancellationRequested();
                var form = new WWWForm();
                form.AddBinaryData("file", await File.ReadAllBytesAsync(file, token), Path.GetFileName(file), "application/octet-stream");
                using var request = UnityWebRequest.Post(endpoint.uploadUrl, form);
                request.SetRequestHeader("x-upload-password", endpoint.uploadPassword);
                request.timeout = 30;
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    if (token.IsCancellationRequested) { request.Abort(); token.ThrowIfCancellationRequested(); }
                    await Task.Yield();
                }
                if (request.result != UnityWebRequest.Result.Success) throw new IOException($"업로드 실패: {Path.GetFileName(file)} (HTTP {request.responseCode})");
            }
        }
    }

    public sealed class FmvPlayerBuildPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;
        public void OnPreprocessBuild(BuildReport report) => FmvContentPipeline.Synchronize();
    }
}
