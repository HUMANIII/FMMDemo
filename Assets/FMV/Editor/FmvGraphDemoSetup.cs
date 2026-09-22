using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FmvDemo.Editor
{
    public static class FmvGraphDemoSetup
    {
        [MenuItem("Tools/FMV/Apply Graph Demo Upgrade")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Play를 종료한 뒤 적용하세요.");
            if (SceneManager.GetActiveScene().path != FmvProjectSetup.ScenePath)
                throw new InvalidOperationException("FmvDemo 씬을 열어 주세요.");
            var view = UnityEngine.Object.FindFirstObjectByType<FmvView>();
            UpgradeView(view);
            var sequence = AssetDatabase.LoadAssetAtPath<FmvSequenceDefinition>(FmvProjectSetup.SequencePath);
            var wake = sequence.FindNode("wake");
            FmvSequenceEditService.Edit(sequence, "데모 시간 초과 분기", () =>
            {
                wake.timeoutMode = FmvTimeoutMode.Branch; wake.choiceTimeoutSeconds = 10; wake.timeoutTargetNodeId = "sleep";
                foreach (var c in wake.choices) c.isTimeoutDefault = false;
            });
            var layout = FmvGraphLayout.For(sequence);
            if (layout.positions.Count == 0)
            {
                layout.SetPosition("wake", new Vector2(40, 60)); layout.SetPosition("walk", new Vector2(410, 20));
                layout.SetPosition("computer", new Vector2(780, 20)); layout.SetPosition("return", new Vector2(1150, 20));
                layout.SetPosition("sleep", new Vector2(410, 380)); EditorUtility.SetDirty(layout);
            }
            FmvSequenceEditService.Save(sequence, layout);
            EditorSceneManager.MarkSceneDirty(view.gameObject.scene); EditorSceneManager.SaveScene(view.gameObject.scene);
            Debug.Log("[FMV] 가변 선택지 UI 및 10초 시간 초과 분기 적용 완료");
        }
        public static void UpgradeView(FmvView view)
        {
            if (view == null) throw new InvalidOperationException("FmvView를 찾을 수 없습니다.");
            if (view.choiceScroll != null) return;
            var panel = (RectTransform)view.choicePanel.transform;
            panel.anchorMax = new Vector2(panel.anchorMax.x, 0.32f);
            var prompt = panel.Find("Prompt") as RectTransform;
            if (prompt != null) SetRect(prompt, new Vector2(0.04f, 0.85f), new Vector2(0.96f, 0.98f));
            var scroll = Rect("Choice Scroll", panel, new Vector2(0.04f, 0.10f), new Vector2(0.96f, 0.65f));
            view.choiceScroll = scroll.gameObject.AddComponent<ScrollRect>();
            var viewport = Rect("Viewport", scroll, Vector2.zero, Vector2.one);
            viewport.gameObject.AddComponent<RectMask2D>();
            var background = viewport.gameObject.AddComponent<Image>(); background.color = Color.clear;
            var content = view.choiceButtons[0].transform.parent as RectTransform;
            if (content.TryGetComponent<HorizontalLayoutGroup>(out var oldLayout)) UnityEngine.Object.DestroyImmediate(oldLayout);
            content.SetParent(viewport, false); content.name = "Choice Content";
            SetRect(content, new Vector2(0, 1), Vector2.one); content.pivot = new Vector2(0.5f, 1);
            var grid = content.gameObject.AddComponent<GridLayoutGroup>(); grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2; grid.spacing = new Vector2(16, 12); grid.cellSize = new Vector2(450, 76);
            view.choiceContent = content; view.choiceScroll.content = content; view.choiceScroll.viewport = viewport;
            view.choiceScroll.horizontal = false; view.choiceScroll.vertical = true; view.choiceScroll.movementType = ScrollRect.MovementType.Clamped; view.choiceScroll.scrollSensitivity = 30;
            var timer = Rect("Choice Timer", panel, new Vector2(0.04f, 0.69f), new Vector2(0.96f, 0.83f));
            view.timerRoot = timer.gameObject;
            var textObject = UnityEngine.Object.Instantiate(view.status.gameObject, timer, false);
            textObject.name = "Countdown"; SetRect((RectTransform)textObject.transform, new Vector2(0, 0.25f), Vector2.one);
            view.timerLabel = textObject.GetComponent<TMP_Text>(); view.timerLabel.alignment = TextAlignmentOptions.Center; view.timerLabel.fontSize = 20;
            view.timerLabel.text = "선택까지 10.0초"; view.timerLabel.color = new Color(1, 0.78f, 0.43f);
            var track = Rect("Timer Track", timer, Vector2.zero, new Vector2(1, 0.12f));
            track.gameObject.AddComponent<Image>().color = new Color(0.3f, 0.34f, 0.36f);
            var fill = Rect("Timer Fill", track, Vector2.zero, Vector2.one);
            view.timerFill = fill.gameObject.AddComponent<Image>(); view.timerFill.color = new Color(1, 0.67f, 0.32f); view.timerFill.raycastTarget = false;
            timer.gameObject.SetActive(false); EditorUtility.SetDirty(view);
        }
        private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false); SetRect(rect, min, max); return rect;
        }
        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max) { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    }
}
