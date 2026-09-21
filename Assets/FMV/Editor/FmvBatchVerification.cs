using System;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace FmvDemo.Editor
{
    public static class FmvBatchVerification
    {
        private const string Backup = ".tools/auto-registration/original.json";
        private const string VideoPath = "Assets/FMV/Content/Videos/verification-wake.mp4";
        private static double deadline;
        private static bool sawPlayAttempt;

        public static void BuildAndStale()
        {
            try
            {
                Directory.CreateDirectory("Docs/Evidence");
                Directory.CreateDirectory(".tools/auto-registration");
                var definition = AssetDatabase.LoadAssetAtPath<FmvSequenceDefinition>(FmvProjectSetup.SequencePath);
                if (!File.Exists(Backup)) File.WriteAllText(Backup, EditorJsonUtility.ToJson(definition));
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Video.VideoClip>(VideoPath) == null)
                    AssetDatabase.CopyAsset("Assets/FMV/Content/Videos/wake.mp4", VideoPath);
                AssetDatabase.ImportAsset(VideoPath, ImportAssetOptions.ForceSynchronousImport);
                string guid = AssetDatabase.AssetPathToGUID(VideoPath);
                definition.nodes[0].video = new VideoReference(guid);
                EditorUtility.SetDirty(definition); AssetDatabase.SaveAssets();
                FmvEditorTools.Local();
                FmvContentPipeline.Settings.RemoveAssetEntry(guid); AssetDatabase.SaveAssets();
                if (FmvContentPipeline.Settings.FindAssetEntry(guid) != null) throw new Exception("Fixture was already registered");
                AddressableAssetSettings.BuildPlayerContent(out var result);
                if (!string.IsNullOrEmpty(result.Error)) throw new Exception(result.Error);
                if (FmvContentPipeline.Settings.FindAssetEntry(guid) == null) throw new Exception("Builder did not register video");
                var receipt = FmvContentPipeline.RequireCurrentBuild();
                File.WriteAllText("Docs/Evidence/editor-BuildAuto.json", "{\"registeredByBuilder\":true,\"files\":" + receipt.files.Length + ",\"fingerprint\":\"" + receipt.fingerprint + "\"}");
                definition = AssetDatabase.LoadAssetAtPath<FmvSequenceDefinition>(FmvProjectSetup.SequencePath);
                definition.nodes[0].title += " [stale verification]";
                EditorUtility.SetDirty(definition); AssetDatabase.SaveAssets();
                FmvEditorTools.PackedPlay();
                sawPlayAttempt = false;
                deadline = EditorApplication.timeSinceStartup + 30;
                EditorApplication.playModeStateChanged += ObservePlay;
                EditorApplication.update += CheckBlocked;
                EditorApplication.isPlaying = true;
            }
            catch (Exception error) { Failure(error); }
        }

        private static void ObservePlay(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode) sawPlayAttempt = true;
        }
        private static void CheckBlocked()
        {
            if (!sawPlayAttempt || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (EditorApplication.timeSinceStartup > deadline) Failure(new Exception("Stale Play was not blocked"));
                return;
            }
            EditorApplication.update -= CheckBlocked;
            EditorApplication.playModeStateChanged -= ObservePlay;
            try
            {
                bool rejected = false;
                try { FmvContentPipeline.RequireCurrentBuild(); } catch (InvalidOperationException) { rejected = true; }
                if (!rejected) throw new Exception("Stale receipt accepted");
                File.WriteAllText("Docs/Evidence/editor-CheckStale.json", "{\"rejectedStaleBuild\":true,\"playEntryObserved\":true,\"editorPlaying\":false}");
                RestoreAuto();
                EditorApplication.Exit(0);
            }
            catch (Exception error) { Failure(error); }
        }
        public static void RestoreAuto()
        {
            if (File.Exists(Backup))
            {
                var definition = AssetDatabase.LoadAssetAtPath<FmvSequenceDefinition>(FmvProjectSetup.SequencePath);
                EditorJsonUtility.FromJsonOverwrite(File.ReadAllText(Backup), definition);
                EditorUtility.SetDirty(definition); AssetDatabase.SaveAssets();
                AssetDatabase.DeleteAsset(VideoPath);
                File.Delete(Backup);
            }
            // capture_game_view writes relative paths underneath Assets; exported evidence lives in Docs.
            AssetDatabase.DeleteAsset("Assets/Docs/Evidence/game-choice.png");
            foreach (string folder in new[] { "Assets/Docs/Evidence", "Assets/Docs" })
                if (Directory.Exists(folder) && Directory.GetFileSystemEntries(folder).Length == 0) AssetDatabase.DeleteAsset(folder);
            FmvEditorTools.Local(); FmvEditorTools.FastPlay(); FmvContentPipeline.Synchronize();
            File.WriteAllText("Docs/Evidence/editor-RestoreAuto.json", "{\"restored\":true}");
        }

        public static void PrepareRemote()
        {
            Directory.CreateDirectory(".tools/remote-verification");
            foreach (string name in new[] { "ready.json", "error.txt" })
                if (File.Exists(".tools/remote-verification/" + name)) File.Delete(".tools/remote-verification/" + name);
            deadline = EditorApplication.timeSinceStartup + 300;
            EditorApplication.update += CheckRemote;
            FmvRemoteVerification.Prepare();
        }
        private static void CheckRemote()
        {
            if (File.Exists(".tools/remote-verification/error.txt")) { EditorApplication.update -= CheckRemote; EditorApplication.Exit(1); }
            else if (File.Exists(".tools/remote-verification/ready.json")) { EditorApplication.update -= CheckRemote; EditorApplication.Exit(0); }
            else if (EditorApplication.timeSinceStartup > deadline) Failure(new TimeoutException("Remote preparation exceeded 300 seconds"));
        }
        private static void Failure(Exception error)
        {
            Directory.CreateDirectory(".tools"); File.WriteAllText(".tools/batch-verification-error.txt", error.ToString());
            Debug.LogException(error); EditorApplication.Exit(1);
        }
    }
}
