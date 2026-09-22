using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Video;

namespace FmvDemo.Editor
{
    public abstract class DataRefineBeforeBuildBase { public abstract void RefineData(); }
    public sealed class FmvContentBuildStep : DataRefineBeforeBuildBase { public override void RefineData() => FmvContentPipeline.Synchronize(); }

    [Serializable]
    public sealed class FmvBuildReceipt
    {
        public string fingerprint;
        public string profile;
        public string buildTarget;
        public string builtUtc;
        public string[] files;
        public string[] hashes;
    }

    public static class FmvContentPipeline
    {
        public const string ContentRoot = "Assets/FMV/Content";
        public const string RuntimeSettingsPath = "Assets/FMV/Settings/FmvRuntimeSettings.asset";
        public const string DataGroup = "FMV Sequences";
        public const string VideoGroup = "FMV Videos";
        public static readonly DataRefineBeforeBuildBase[] Steps = { new FmvContentBuildStep() };
        public static AddressableAssetSettings Settings => AddressableAssetSettingsDefaultObject.Settings;

        public static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            Folder(Path.GetDirectoryName(path).Replace('\\', '/'));
            AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
        }

        public static void EnsureSettings()
        {
            Folder(ContentRoot); Folder("Assets/FMV/Settings"); Folder("Assets/FMV/Settings/Builders");
            if (Settings == null)
                AddressableAssetSettingsDefaultObject.Settings = AddressableAssetSettings.Create("Assets/AddressableAssetsData", "AddressableAssetSettings", true, true);
            var settings = Settings;
            bool first = string.IsNullOrEmpty(settings.profileSettings.GetProfileId("Local"));
            foreach (string name in new[] { "Local", "Test", "Release" })
                if (string.IsNullOrEmpty(settings.profileSettings.GetProfileId(name)))
                    settings.profileSettings.AddProfile(name, settings.activeProfileId);
            if (first) settings.activeProfileId = settings.profileSettings.GetProfileId("Local");
            AddBuilder<FmvFastMode>(); AddBuilder<FmvPackedMode>(); AddBuilder<FmvPackedPlayMode>();
            settings.ActivePlayerDataBuilderIndex = settings.DataBuilders.FindIndex(b => b is FmvPackedMode);
            if (first) settings.ActivePlayModeDataBuilderIndex = settings.DataBuilders.FindIndex(b => b is FmvFastMode);
            foreach (string name in new[] { "Local", "Test", "Release" })
            {
                string id = settings.profileSettings.GetProfileId(name);
                SetProfileValue(id, "Remote.BuildPath", $"ServerData/{name}/[BuildTarget]");
                if (name == "Local") continue;
                string root = FmvDeploymentSettings.instance.Endpoint(name).downloadRoot.TrimEnd('/');
                SetProfileValue(id, "Remote.LoadPath", root);
            }
            // Stable catalog name is required for later content updates.
            settings.OverridePlayerVersion = "fmv-v1";
            settings.DisableCatalogUpdateOnStartup = true;
            ConfigureProfile();
        }

        private static void SetProfileValue(string profile, string variable, string value)
        {
            if (Settings.profileSettings.GetValueByName(profile, variable) != value)
                Settings.profileSettings.SetValue(profile, variable, value);
        }

        private static void AddBuilder<T>() where T : ScriptableObject, UnityEditor.AddressableAssets.Build.IDataBuilder
        {
            if (Settings.DataBuilders.Any(b => b is T)) return;
            string path = "Assets/FMV/Settings/Builders/" + typeof(T).Name + ".asset";
            var builder = AssetDatabase.LoadAssetAtPath<T>(path);
            if (builder == null) { builder = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(builder, path); }
            Settings.AddDataBuilder(builder);
        }

        public static void ConfigureProfile()
        {
            string name = Settings.profileSettings.GetProfileName(Settings.activeProfileId);
            bool remote = name is "Test" or "Release";
            Settings.BuildRemoteCatalog = remote;
            Settings.RemoteCatalogBuildPath.SetVariableByName(Settings, "Remote.BuildPath");
            Settings.RemoteCatalogLoadPath.SetVariableByName(Settings, "Remote.LoadPath");
            foreach (string groupName in new[] { DataGroup, VideoGroup })
            {
                var schema = Settings.FindGroup(groupName)?.GetSchema<BundledAssetGroupSchema>();
                if (schema == null) continue;
                string before = EditorJsonUtility.ToJson(schema);
                schema.BuildPath.SetVariableByName(Settings, remote ? "Remote.BuildPath" : "Local.BuildPath");
                schema.LoadPath.SetVariableByName(Settings, remote ? "Remote.LoadPath" : "Local.LoadPath");
                if (before != EditorJsonUtility.ToJson(schema)) EditorUtility.SetDirty(schema);
            }
            var runtime = AssetDatabase.LoadAssetAtPath<FmvRuntimeSettings>(RuntimeSettingsPath);
            if (runtime != null && (runtime.profileName != name || runtime.checkRemoteUpdates != remote))
            {
                runtime.profileName = name; runtime.checkRemoteUpdates = remote;
                EditorUtility.SetDirty(runtime);
            }
        }

        public static FmvSequenceDefinition[] Sequences() => AssetDatabase.FindAssets("t:FmvSequenceDefinition", new[] { ContentRoot })
            .OrderBy(g => g, StringComparer.Ordinal).Select(g => AssetDatabase.LoadAssetAtPath<FmvSequenceDefinition>(AssetDatabase.GUIDToAssetPath(g))).ToArray();

        public static List<string> ValidateContent()
        {
            var errors = new List<string>();
            var sequences = Sequences();
            if (sequences.Length == 0) errors.Add("FMV 시나리오가 없습니다.");
            foreach (var sequence in sequences)
            {
                errors.AddRange(sequence.Validate().Select(e => sequence.name + ": " + e));
                foreach (var node in sequence.nodes ?? new List<FmvNode>())
                    if (node?.video != null && AssetDatabase.LoadAssetAtPath<VideoClip>(AssetDatabase.GUIDToAssetPath(node.video.AssetGUID)) == null)
                        errors.Add($"영상 파일을 찾을 수 없습니다: {sequence.name}/{node.id}");
            }
            var runtime = AssetDatabase.LoadAssetAtPath<FmvRuntimeSettings>(RuntimeSettingsPath);
            if (runtime == null || runtime.sequence == null || !sequences.Any(s => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(s)) == runtime.sequence.AssetGUID))
                errors.Add("실행 설정의 시나리오가 FMV Content 폴더에 없습니다.");
            if (Settings != null)
            {
                string profile = Settings.profileSettings.GetProfileName(Settings.activeProfileId);
                if (profile is "Test" or "Release")
                {
                    string root = FmvDeploymentSettings.instance.Endpoint(profile).downloadRoot;
                    if (!Uri.TryCreate(root, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
                        errors.Add($"{profile} 다운로드 URL을 Project Settings > FMV Deployment에서 설정하세요.");
                }
            }
            return errors;
        }

        public static void Synchronize()
        {
            FmvSequenceEditor.SaveOpenEditors();
            EnsureSettings();
            var errors = ValidateContent();
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
            var expected = new Dictionary<string, (string address, string group, HashSet<string> labels)>();
            foreach (var sequence in Sequences())
            {
                string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(sequence));
                string label = FmvContentIds.SequenceLabel(guid);
                expected.Add(guid, (FmvContentIds.Sequence(guid), DataGroup, new HashSet<string> { FmvContentIds.Label, label }));
                foreach (var node in sequence.nodes)
                {
                    string video = node.video.AssetGUID;
                    if (!expected.ContainsKey(video)) expected.Add(video, (FmvContentIds.Video(video), VideoGroup, new HashSet<string> { FmvContentIds.Label }));
                    expected[video].labels.Add(label);
                }
            }
            var data = EnsureGroup(DataGroup, false);
            var videos = EnsureGroup(VideoGroup, true);
            bool changed = false;
            foreach (var group in new[] { data, videos })
            {
                foreach (var entry in group.entries.ToArray())
                    if (entry.address.StartsWith("fmv/", StringComparison.Ordinal) && !expected.ContainsKey(entry.guid))
                    { Settings.RemoveAssetEntry(entry.guid); changed = true; }
            }
            foreach (var item in expected.OrderBy(e => e.Key, StringComparer.Ordinal))
            {
                var desired = item.Value;
                var group = desired.group == DataGroup ? data : videos;
                var entry = Settings.FindAssetEntry(item.Key);
                if (entry == null || entry.parentGroup != group) { entry = Settings.CreateOrMoveEntry(item.Key, group); changed = true; }
                if (entry.address != desired.address) { entry.SetAddress(desired.address); changed = true; }
                foreach (var label in entry.labels.Where(l => l == "fmv" || l.StartsWith("fmv-sequence-", StringComparison.Ordinal)).Except(desired.labels).ToArray())
                { entry.SetLabel(label, false); changed = true; }
                foreach (var label in desired.labels)
                    if (!entry.labels.Contains(label)) { entry.SetLabel(label, true, true); changed = true; }
            }
            if (changed) Settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
            AssetDatabase.SaveAssets();
        }

        private static AddressableAssetGroup EnsureGroup(string name, bool video)
        {
            var group = Settings.FindGroup(name) ?? Settings.CreateGroup(name, false, false, false, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            string before = EditorJsonUtility.ToJson(schema);
            bool remote = Settings.BuildRemoteCatalog;
            schema.BuildPath.SetVariableByName(Settings, remote ? "Remote.BuildPath" : "Local.BuildPath");
            schema.LoadPath.SetVariableByName(Settings, remote ? "Remote.LoadPath" : "Local.LoadPath");
            schema.BundleMode = video ? BundledAssetGroupSchema.BundlePackingMode.PackSeparately : BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            schema.Compression = video ? BundledAssetGroupSchema.BundleCompressionMode.Uncompressed : BundledAssetGroupSchema.BundleCompressionMode.LZ4;
            schema.UseAssetBundleCache = true;
            schema.IncludeGUIDInCatalog = true;
            schema.IncludeAddressInCatalog = true;
            schema.IncludeLabelsInCatalog = true;
            if (before != EditorJsonUtility.ToJson(schema)) EditorUtility.SetDirty(schema);
            return group;
        }

        public static string Fingerprint()
        {
            var text = new StringBuilder(Application.unityVersion).Append(EditorUserBuildSettings.activeBuildTarget).Append(Settings.activeProfileId);
            foreach (var sequence in Sequences())
            {
                string path = AssetDatabase.GetAssetPath(sequence);
                text.Append(path).Append(AssetDatabase.GetAssetDependencyHash(path));
            }
            // AssetReference stores GUID strings, which are not native serialized asset dependencies.
            foreach (string guid in Sequences().SelectMany(s => s.nodes).Select(n => n.video.AssetGUID).Distinct().OrderBy(g => g, StringComparer.Ordinal))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                text.Append(guid).Append(path).Append(AssetDatabase.GetAssetDependencyHash(path));
            }
            foreach (string name in new[] { DataGroup, VideoGroup })
            {
                var group = Settings.FindGroup(name);
                if (group == null) continue;
                text.Append(EditorJsonUtility.ToJson(group.GetSchema<BundledAssetGroupSchema>()));
                foreach (var entry in group.entries.OrderBy(e => e.guid, StringComparer.Ordinal))
                    text.Append(entry.guid).Append(entry.address).Append(string.Join(",", entry.labels.OrderBy(l => l, StringComparer.Ordinal)));
            }
            foreach (string variable in Settings.profileSettings.GetVariableNames().OrderBy(v => v, StringComparer.Ordinal))
                text.Append(variable).Append(Settings.profileSettings.GetValueByName(Settings.activeProfileId, variable));
            text.Append(Settings.OverridePlayerVersion).Append(Settings.BuildRemoteCatalog);
            return Hash128.Compute(text.ToString()).ToString();
        }

        public static string ReceiptPath => "Library/FMV/" + Settings.profileSettings.GetProfileName(Settings.activeProfileId) + "-" + EditorUserBuildSettings.activeBuildTarget + ".json";
        public static string HashFile(string path)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(path);
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
        }
        public static void SaveReceipt(IEnumerable<string> files)
        {
            var paths = files.Select(Path.GetFullPath).Where(File.Exists).Distinct().OrderBy(p => p, StringComparer.Ordinal).ToArray();
            var receipt = new FmvBuildReceipt { fingerprint = Fingerprint(), profile = Settings.profileSettings.GetProfileName(Settings.activeProfileId),
                buildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(), builtUtc = DateTime.UtcNow.ToString("O"), files = paths, hashes = paths.Select(HashFile).ToArray() };
            Directory.CreateDirectory("Library/FMV");
            File.WriteAllText(ReceiptPath, JsonUtility.ToJson(receipt, true));
        }
        public static FmvBuildReceipt RequireCurrentBuild()
        {
            if (!File.Exists(ReceiptPath) || !File.Exists(Addressables.BuildPath + "/settings.json")) throw new InvalidOperationException("FMV 콘텐츠를 먼저 빌드하세요: Tools > FMV > Build Content");
            var receipt = JsonUtility.FromJson<FmvBuildReceipt>(File.ReadAllText(ReceiptPath));
            if (receipt.fingerprint != Fingerprint() || receipt.files == null || receipt.hashes == null || receipt.files.Length != receipt.hashes.Length ||
                receipt.files.Where((file, index) => !File.Exists(file) || HashFile(file) != receipt.hashes[index]).Any())
                throw new InvalidOperationException("FMV 기존 빌드가 오래되었거나 변경되었습니다. Tools > FMV > Build Content를 실행하세요.");
            return receipt;
        }
    }
}
