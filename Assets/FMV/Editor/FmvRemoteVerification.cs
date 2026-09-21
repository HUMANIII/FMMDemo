using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace FmvDemo.Editor
{
    /// <summary>Reproducible loopback integration fixture; explicitly invoked, never runs during normal Play.</summary>
    public static class FmvRemoteVerification
    {
        private const string Root = ".tools/remote-verification";
        private const string ServerLog = ".tools/remote-server/requests.jsonl";
        [Serializable] private sealed class SavedState { public string definitionJson; public string testJson; public string profile; public bool packed; }
        [Serializable] private sealed class Request { public string method; public string filename; public string field; public bool authorized; public int status; }

        public static void Prepare() => PrepareAsync().ContinueWithReport();
        private static async Task PrepareAsync()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("먼저 Play를 종료하세요.");
            Directory.CreateDirectory(Root);
            if (File.Exists(Root + "/original.json")) throw new InvalidOperationException("기존 원격 검증을 먼저 Restore 하세요.");
            var definition = AssetDatabase.LoadAssetAtPath<FmvSequenceDefinition>(FmvProjectSetup.SequencePath);
            var endpoint = FmvDeploymentSettings.instance.Endpoint("Test");
            var saved = new SavedState { definitionJson = EditorJsonUtility.ToJson(definition), testJson = JsonUtility.ToJson(endpoint),
                profile = FmvContentPipeline.Settings.profileSettings.GetProfileName(FmvContentPipeline.Settings.activeProfileId),
                packed = FmvContentPipeline.Settings.ActivePlayModeDataBuilder is FmvPackedPlayMode };
            File.WriteAllText(Root + "/original.json", JsonUtility.ToJson(saved, true));
            try
            {
                endpoint.downloadRoot = "http://127.0.0.1:18084/content";
                endpoint.uploadUrl = "http://127.0.0.1:18084/upload";
                endpoint.uploadPassword = "fmv-local-verification";
                FmvDeploymentSettings.instance.Persist();
                await VerifyProtocol(endpoint);
                // Verify the production build/upload path restores its profile after an HTTP failure.
                endpoint.uploadUrl += "?fail=catalog_";
                bool failed = false;
                try { await FmvEditorTools.BuildAndUpload("Test", true, CancellationToken.None); }
                catch (IOException) { failed = true; }
                Require(failed, "Injected HTTP failure was not observed");
                Require(FmvContentPipeline.Settings.profileSettings.GetProfileName(FmvContentPipeline.Settings.activeProfileId) == saved.profile, "Profile was not restored");
                endpoint.uploadUrl = "http://127.0.0.1:18084/upload";
                FmvDeploymentSettings.instance.Persist();

                FmvEditorTools.Test();
                definition = AssetDatabase.LoadAssetAtPath<FmvSequenceDefinition>(FmvProjectSetup.SequencePath);
                definition.nodes[0].title = "침대에서 일어나기 [remote v1]";
                EditorUtility.SetDirty(definition); AssetDatabase.SaveAssets();
                await FmvEditorTools.BuildAndUpload("Test", true, CancellationToken.None);
                var first = FmvContentPipeline.RequireCurrentBuild();
                definition = AssetDatabase.LoadAssetAtPath<FmvSequenceDefinition>(FmvProjectSetup.SequencePath);
                string v1Definition = EditorJsonUtility.ToJson(definition);
                string v1Receipt = File.ReadAllText(FmvContentPipeline.ReceiptPath);
                string projectRoot = Path.GetFullPath(".") + Path.DirectorySeparatorChar;
                foreach (var file in first.files)
                {
                    Require(file.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase), "Unexpected build output outside workspace");
                    string destination = Root + "/v1/" + Path.GetRelativePath(projectRoot, file);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)); File.Copy(file, destination, true);
                }
                definition.nodes[0].title = "침대에서 일어나기 [remote v2]";
                EditorUtility.SetDirty(definition); AssetDatabase.SaveAssets();
                await FmvEditorTools.BuildAndUpload("Test", true, CancellationToken.None);
                // Keep an old, valid client build locally; the independent HTTP server keeps v2.
                definition = AssetDatabase.LoadAssetAtPath<FmvSequenceDefinition>(FmvProjectSetup.SequencePath);
                EditorJsonUtility.FromJsonOverwrite(v1Definition, definition);
                EditorUtility.SetDirty(definition); AssetDatabase.SaveAssets();
                foreach (var file in first.files) File.Copy(Root + "/v1/" + Path.GetRelativePath(projectRoot, file), file, true);
                File.WriteAllText(FmvContentPipeline.ReceiptPath, v1Receipt);
                FmvContentPipeline.RequireCurrentBuild();
                FmvEditorTools.PackedPlay();
                File.WriteAllText(Root + "/ready.json", "{\"protocol\":\"passed\",\"failureStopsUpload\":true,\"profileRestored\":true,\"local\":\"v1\",\"server\":\"v2\",\"expectedTitle\":\"침대에서 일어나기 [remote v2]\"}");
                Debug.Log("[FMV verification] 원격 v2 / 로컬 v1 준비 완료. Play에서 원격 v2와 영상 프레임을 확인하세요.");
            }
            catch { Restore(); throw; }
        }

        private static async Task VerifyProtocol(FmvEndpoint endpoint)
        {
            string fixture = Root + "/protocol"; Directory.CreateDirectory(fixture);
            string[] files = { fixture + "/catalog_protocol.hash", fixture + "/protocol.bundle", fixture + "/catalog_protocol.json" };
            foreach (string file in files) File.WriteAllText(file, "FMV protocol fixture");
            int start = ReadPosts().Length;
            await FmvEditorTools.UploadFiles(files, endpoint, CancellationToken.None);
            var posted = ReadPosts().Skip(start).ToArray();
            Require(posted.Select(p => p.filename).SequenceEqual(new[] { "protocol.bundle", "catalog_protocol.json", "catalog_protocol.hash" }), "Upload order mismatch");
            Require(posted.All(p => p.field == "file" && p.authorized && p.status == 200), "Multipart/header contract mismatch");
            start = ReadPosts().Length;
            var failureEndpoint = new FmvEndpoint { uploadUrl = endpoint.uploadUrl + "?fail=catalog_protocol.json", uploadPassword = endpoint.uploadPassword };
            bool rejected = false;
            try { await FmvEditorTools.UploadFiles(files, failureEndpoint, CancellationToken.None); } catch (IOException) { rejected = true; }
            posted = ReadPosts().Skip(start).ToArray();
            Require(rejected && posted.Length == 2 && posted[1].status == 500, "Upload continued after a failure");
            var denied = new FmvEndpoint { uploadUrl = endpoint.uploadUrl, uploadPassword = "intentionally-wrong" };
            rejected = false;
            try { await FmvEditorTools.UploadFiles(files, denied, CancellationToken.None); } catch (IOException) { rejected = true; }
            Require(rejected && ReadPosts().Last().status == 403, "Authentication error was not surfaced");
        }
        private static Request[] ReadPosts() => !File.Exists(ServerLog) ? Array.Empty<Request>() : File.ReadAllLines(ServerLog).Select(JsonUtility.FromJson<Request>).Where(r => r.method == "POST").ToArray();
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

        public static void Restore()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("먼저 Play를 종료하세요.");
            if (!File.Exists(Root + "/original.json")) return;
            var saved = JsonUtility.FromJson<SavedState>(File.ReadAllText(Root + "/original.json"));
            var definition = AssetDatabase.LoadAssetAtPath<FmvSequenceDefinition>(FmvProjectSetup.SequencePath);
            EditorJsonUtility.FromJsonOverwrite(saved.definitionJson, definition);
            EditorUtility.SetDirty(definition);
            JsonUtility.FromJsonOverwrite(saved.testJson, FmvDeploymentSettings.instance.Endpoint("Test"));
            FmvDeploymentSettings.instance.Persist();
            FmvEditorTools.UseProfile(saved.profile);
            FmvEditorTools.SetPlayMode(saved.packed);
            FmvContentPipeline.Synchronize();
            File.Delete(Root + "/original.json");
            Debug.Log("[FMV verification] 원래 시나리오와 설정 복구 완료");
        }

        private static async void ContinueWithReport(this Task task)
        {
            try { await task; }
            catch (Exception error) { File.WriteAllText(Root + "/error.txt", error.ToString()); Debug.LogException(error); }
        }
    }
}
