using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;

namespace FmvDemo.Editor
{
    [InitializeOnLoad]
    public static class FmvProjectSetup
    {
        public const string ScenePath = "Assets/FMV/Scenes/FmvDemo.unity";
        public const string SequencePath = "Assets/FMV/Content/Sequences/BedroomLoop.asset";
        private static readonly Color Ink = new(0.08f, 0.13f, 0.16f, 0.94f);
        private static readonly Color Paper = new(0.98f, 0.95f, 0.87f, 1);
        private const string Pending = "FMV.Setup.Pending";
        private const string BatchMode = "FMV.Setup.Batch";
        static FmvProjectSetup()
        {
            if (SessionState.GetBool(Pending, false)) EditorApplication.update += ContinueSetup;
        }

        public static void Batch()
        {
            SessionState.SetBool(BatchMode, true);
            CreateDemo();
        }

        [MenuItem("Tools/FMV/Create Missing Demo Assets")]
        public static void CreateDemo()
        {
            SessionState.SetBool(Pending, true);
            SessionState.SetFloat("FMV.Setup.Deadline", (float)EditorApplication.timeSinceStartup + 180);
            EditorApplication.update -= ContinueSetup;
            EditorApplication.update += ContinueSetup;
            if (AssetDatabase.LoadAssetAtPath<TMP_Settings>("Assets/TextMesh Pro/Resources/TMP Settings.asset") == null)
                TMP_PackageResourceImporter.ImportResources(true, false, false);
        }

        private static void ContinueSetup()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            bool ready = AssetDatabase.LoadAssetAtPath<TMP_Settings>("Assets/TextMesh Pro/Resources/TMP Settings.asset") != null;
            if (!ready && EditorApplication.timeSinceStartup < SessionState.GetFloat("FMV.Setup.Deadline", 0)) return;
            EditorApplication.update -= ContinueSetup;
            SessionState.SetBool(Pending, false);
            bool batch = SessionState.GetBool(BatchMode, false);
            SessionState.SetBool(BatchMode, false);
            try
            {
                if (!ready) throw new TimeoutException("TMP Essentials 가져오기를 완료하지 못했습니다.");
                CreateDemoReady();
                if (batch) EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); if (batch) EditorApplication.Exit(1); }
        }

        private static void CreateDemoReady()
        {
            AssetDatabase.Refresh();
            FmvContentPipeline.Folder("Assets/FMV/Content/Sequences");
            FmvContentPipeline.Folder("Assets/FMV/Scenes");
            FmvContentPipeline.EnsureSettings();
            var font = EnsureFont();
            var sequence = AssetDatabase.LoadAssetAtPath<FmvSequenceDefinition>(SequencePath);
            if (sequence == null)
            {
                sequence = ScriptableObject.CreateInstance<FmvSequenceDefinition>();
                sequence.nodes = new List<FmvNode> {
                    Node("wake", "침대에서 일어나기", null), Node("walk", "컴퓨터로 가기", "computer"),
                    Node("computer", "컴퓨터 사용하기", "return"), Node("return", "침대로 돌아오기", "wake"),
                    Node("sleep", "다시 눕기", "wake")
                };
                sequence.nodes[0].choices = new List<FmvChoice> {
                    new() { text = "컴퓨터로 간다", targetNodeId = "walk" }, new() { text = "다시 눕는다", targetNodeId = "sleep" }
                };
                sequence.nodes[0].timeoutMode = FmvTimeoutMode.Branch;
                sequence.nodes[0].choiceTimeoutSeconds = 10;
                sequence.nodes[0].timeoutTargetNodeId = "sleep";
                AssetDatabase.CreateAsset(sequence, SequencePath);
            }
            var settings = AssetDatabase.LoadAssetAtPath<FmvRuntimeSettings>(FmvContentPipeline.RuntimeSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<FmvRuntimeSettings>();
                settings.sequence = new AssetReferenceT<FmvSequenceDefinition>(AssetDatabase.AssetPathToGUID(SequencePath));
                AssetDatabase.CreateAsset(settings, FmvContentPipeline.RuntimeSettingsPath);
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) CreateScene(settings, font);
            else EditorSceneManager.OpenScene(ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            EditorSettings.enterPlayModeOptionsEnabled = false;
            PlayerSettings.runInBackground = true;
            PlayerSettings.companyName = "FMV Demo";
            PlayerSettings.productName = "작은 하루 - FMV Demo";
            FmvEditorTools.Local();
            FmvEditorTools.FastPlay();
            FmvContentPipeline.Synchronize();
            AssetDatabase.SaveAssets();
            Debug.Log("[FMV] 데모 준비 완료: " + ScenePath);
        }

        private static FmvNode Node(string id, string title, string next)
        {
            string path = "Assets/FMV/Content/Videos/" + id + ".mp4";
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid)) throw new InvalidOperationException("영상 파일 누락: " + path);
            return new FmvNode { id = id, title = title, video = new VideoReference(guid), nextNodeId = next ?? "" };
        }

        private static TMP_FontAsset EnsureFont()
        {
            const string path = "Assets/FMV/Fonts/FmvKorean.asset";
            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (fontAsset != null) return fontAsset;
            var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/FMV/Fonts/NotoSansCJKkr-Regular.otf");
            fontAsset = TMP_FontAsset.CreateFontAsset(font, 48, 6, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            fontAsset.name = "FmvKorean";
            AssetDatabase.CreateAsset(fontAsset, path);
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            foreach (var texture in fontAsset.atlasTextures) AssetDatabase.AddObjectToAsset(texture, fontAsset);
            string text = "작은 하루 · FMV 데모 침대에서 일어나기 컴퓨터로 가기 사용하기 돌아오기 다시 눕기 간다 눕는다 이제 무엇을 할까? 영상을 준비하고 있어요 불러오고 재생 준비 중 못했어요 재시도 처음부터 업데이트 확인 다운로드 0123456789%";
            fontAsset.TryAddCharacters(text, out string missing);
            if (!string.IsNullOrEmpty(missing)) Debug.LogWarning("[FMV font] Missing glyphs: " + missing);
            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            return fontAsset;
        }

        private static void CreateScene(FmvRuntimeSettings settings, TMP_FontAsset font)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera"; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Ink;
            camera.orthographic = true; camera.transform.position = new Vector3(0, 0, -10);
            var player = new GameObject("FMV Player").AddComponent<FmvPlayer>();
            player.settings = settings;
            var canvas = new GameObject("FMV Canvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            var view = canvas.AddComponent<FmvView>(); view.player = player;
            var viewport = Rect("Video Viewport", canvas.transform, Vector2.zero, Vector2.one);
            var raw = viewport.gameObject.AddComponent<UnityEngine.UI.RawImage>(); raw.raycastTarget = false; view.screen = raw;
            var fit = viewport.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>(); fit.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.FitInParent; fit.aspectRatio = 16f / 9f;
            var header = Panel("Header", canvas.transform, new Vector2(0.025f, 0.86f), new Vector2(0.40f, 0.965f), Ink);
            view.title = Text("Node Title", header, font, "작은 하루", 32, new Vector2(0.045f, 0.44f), new Vector2(0.96f, 0.91f));
            view.status = Text("Status", header, font, "작은 하루 · FMV 데모", 19, new Vector2(0.045f, 0.12f), new Vector2(0.96f, 0.46f));
            view.status.color = new Color(0.76f, 0.84f, 0.81f);
            var choice = Panel("Choices", canvas.transform, new Vector2(0.20f, 0.08f), new Vector2(0.80f, 0.26f), Ink);
            Text("Prompt", choice, font, "이제 무엇을 할까?", 28, new Vector2(0.04f, 0.67f), new Vector2(0.96f, 0.94f), TextAlignmentOptions.Center);
            var row = Rect("Choice Buttons", choice, new Vector2(0.04f, 0.13f), new Vector2(0.96f, 0.58f));
            var layout = row.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.spacing = 20; layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandWidth = true;
            view.choiceButtons = new UnityEngine.UI.Button[2]; view.choiceTexts = new TMP_Text[2];
            for (int i = 0; i < 2; i++)
            {
                view.choiceButtons[i] = Button("Choice " + i, row, font, i == 0 ? "컴퓨터로 간다" : "다시 눕는다", out var label);
                view.choiceTexts[i] = label;
            }
            view.choicePanel = choice.gameObject; choice.gameObject.SetActive(false);
            var errorPanel = Panel("Playback Error", canvas.transform, new Vector2(0.20f, 0.32f), new Vector2(0.80f, 0.68f), Ink);
            view.error = Text("Error Message", errorPanel, font, "", 24, new Vector2(0.06f, 0.37f), new Vector2(0.94f, 0.90f), TextAlignmentOptions.Center);
            var retryRow = Rect("Error Actions", errorPanel, new Vector2(0.06f, 0.10f), new Vector2(0.94f, 0.30f));
            var retryLayout = retryRow.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            retryLayout.spacing = 20; retryLayout.childControlWidth = true; retryLayout.childControlHeight = true; retryLayout.childForceExpandWidth = true;
            view.retryButton = Button("Retry", retryRow, font, "재시도", out _);
            view.restartButton = Button("Restart", retryRow, font, "처음부터", out _);
            view.errorPanel = errorPanel.gameObject; errorPanel.gameObject.SetActive(false);
            var progress = Panel("Progress Track", canvas.transform, new Vector2(0, 0), new Vector2(1, 0.005f), new Color(0, 0, 0, 0.35f));
            var fill = Panel("Progress", progress, Vector2.zero, Vector2.one, new Color(0.83f, 0.56f, 0.33f));
            view.progressFill = fill.GetComponent<UnityEngine.UI.Image>();
            view.progressFill.type = UnityEngine.UI.Image.Type.Filled; view.progressFill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            var system = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            system.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            FmvGraphDemoSetup.UpgradeView(view);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }
        private static RectTransform Panel(string name, Transform parent, Vector2 min, Vector2 max, Color color)
        {
            var rect = Rect(name, parent, min, max); var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color; image.raycastTarget = false; return rect;
        }
        private static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, string value, float size, Vector2 min, Vector2 max, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
        {
            var rect = Rect(name, parent, min, max); var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font; text.text = value; text.fontSize = size; text.color = Paper; text.alignment = align;
            text.raycastTarget = false; return text;
        }
        private static UnityEngine.UI.Button Button(string name, Transform parent, TMP_FontAsset font, string value, out TMP_Text label)
        {
            var rect = Panel(name, parent, Vector2.zero, Vector2.one, Paper);
            rect.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 450;
            var image = rect.GetComponent<UnityEngine.UI.Image>(); image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.highlightedColor = new Color(0.82f, 0.94f, 0.90f); colors.pressedColor = new Color(0.63f, 0.81f, 0.75f); button.colors = colors;
            label = Text("Label", rect, font, value, 28, new Vector2(0.02f, 0.06f), new Vector2(0.98f, 0.94f), TextAlignmentOptions.Center);
            label.color = Ink; return button;
        }
    }
}
