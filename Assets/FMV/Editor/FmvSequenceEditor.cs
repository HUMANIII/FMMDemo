using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Video;

namespace FmvDemo.Editor
{
    public sealed class FmvSequenceEditor : EditorWindow
    {
        [SerializeField] private FmvSequenceDefinition sequence;
        [SerializeField] private string selectedNodeId;
        [SerializeField] private Vector3 pan;
        [SerializeField] private Vector3 zoom = Vector3.one;
        private FmvGraphLayout layout;
        private SequenceGraph graph;
        private ScrollView details;
        private FmvVideoPreview videoPreview;
        private ObjectField picker;
        private VisualElement editingControls;
        private bool refreshQueued;
        public FmvSequenceDefinition Sequence => sequence;
        public string SelectedNodeId => selectedNodeId;
        public GraphView Graph => graph;
        private bool ReadOnly => EditorApplication.isPlayingOrWillChangePlaymode;

        [MenuItem("Tools/FMV/Sequence Editor")]
        public static void ShowWindow()
        {
            var target = Selection.activeObject as FmvSequenceDefinition;
            Open(target ?? AssetDatabase.LoadAssetAtPath<FmvSequenceDefinition>(FmvProjectSetup.SequencePath));
        }
        public static FmvSequenceEditor Open(FmvSequenceDefinition target)
        {
            var window = GetWindow<FmvSequenceEditor>("FMV Sequence");
            window.minSize = new Vector2(760, 430);
            if (target != null && target != window.sequence) window.Load(target);
            window.Show(); return window;
        }
        private void OnEnable()
        {
            Undo.undoRedoPerformed += QueueRefresh;
            EditorApplication.projectChanged += QueueRefresh;
            EditorApplication.playModeStateChanged += PlayChanged;
            AssemblyReloadEvents.beforeAssemblyReload += Save;
        }
        private void OnDisable()
        {
            videoPreview?.Dispose();
            Save();
            Undo.undoRedoPerformed -= QueueRefresh;
            EditorApplication.projectChanged -= QueueRefresh;
            EditorApplication.playModeStateChanged -= PlayChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= Save;
        }
        private void PlayChanged(PlayModeStateChange state)
        {
            videoPreview?.Stop();
            if (state == PlayModeStateChange.ExitingEditMode) Save();
            QueueRefresh();
        }
        public void CreateGUI()
        {
            videoPreview?.Dispose();
            rootVisualElement.Clear();
            AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/FMV/Editor/FmvSequenceEditor.uxml").CloneTree(rootVisualElement);
            var toolbar = rootVisualElement.Q("toolbar");
            picker = new ObjectField { objectType = typeof(FmvSequenceDefinition), allowSceneObjects = false, value = sequence };
            picker.AddToClassList("sequence-picker"); toolbar.Add(picker);
            picker.RegisterValueChangedCallback(e => Load(e.newValue as FmvSequenceDefinition));
            var create = new Button(CreateSequence) { text = "새 시나리오" }; toolbar.Add(create);
            toolbar.Add(new Button(Save) { text = "저장" });
            toolbar.Add(new Button(Rebuild) { text = "검증" });
            toolbar.Add(new Button(FitGraph) { text = "전체 보기" });
            var add = new Button(() => AddNode(graph.contentViewContainer.WorldToLocal(graph.worldBound.center))) { text = "+ 영상 노드" }; toolbar.Add(add);
            var split = new TwoPaneSplitView(1, 325, TwoPaneSplitViewOrientation.Horizontal);
            rootVisualElement.Q("workspace").Add(split);
            graph = new SequenceGraph(this); graph.AddToClassList("graph-pane"); split.Add(graph);
            var rightPane = new VisualElement(); rightPane.AddToClassList("right-pane"); split.Add(rightPane);
            details = new ScrollView(); details.AddToClassList("details-pane"); rightPane.Add(details);
            videoPreview = new FmvVideoPreview(); rightPane.Add(videoPreview);
            graph.viewTransformChanged += _ =>
            {
                var position = graph.contentViewContainer.style.translate.value;
                pan = new Vector3(position.x.value, position.y.value, position.z);
                zoom = graph.contentViewContainer.style.scale.value.value;
            };
            graph.UpdateViewTransform(pan, zoom);
            rootVisualElement.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.actionKey && e.keyCode == KeyCode.S) { Save(); e.StopPropagation(); }
            });
            rootVisualElement.schedule.Execute(() => { create.SetEnabled(!ReadOnly); add.SetEnabled(!ReadOnly && sequence != null); }).Every(100);
            Rebuild();
            if (pan == Vector3.zero) graph.schedule.Execute(FitGraph).StartingIn(150);
        }
        private void Load(FmvSequenceDefinition value)
        {
            Save(); sequence = value; selectedNodeId = null; layout = null; pan = Vector3.zero; zoom = Vector3.one;
            picker?.SetValueWithoutNotify(value);
            graph?.UpdateViewTransform(pan, zoom); Rebuild();
            graph?.schedule.Execute(FitGraph).StartingIn(150);
        }
        private void CreateSequence()
        {
            if (ReadOnly) return;
            FmvContentPipeline.Folder("Assets/FMV/Content/Sequences");
            string path = EditorUtility.SaveFilePanelInProject("새 FMV 시나리오", "NewSequence", "asset", "FMV Content 폴더 안에 저장하세요.", "Assets/FMV/Content/Sequences");
            if (string.IsNullOrEmpty(path)) return;
            if (!path.StartsWith(FmvContentPipeline.ContentRoot + "/", StringComparison.Ordinal)) { ShowNotification(new GUIContent("Assets/FMV/Content 안에 저장하세요.")); return; }
            var asset = CreateInstance<FmvSequenceDefinition>(); asset.startNodeId = "";
            AssetDatabase.CreateAsset(asset, path); Undo.RegisterCreatedObjectUndo(asset, "시나리오 생성"); Load(asset);
        }
        private void AddNode(Vector2 position)
        {
            if (ReadOnly || sequence == null) return;
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
            var node = FmvSequenceEditService.Add(sequence);
            Undo.RecordObject(layout, "노드 배치"); layout.SetPosition(node.id, position); EditorUtility.SetDirty(layout);
            if (sequence.nodes.Count == 1) FmvSequenceEditService.Edit(sequence, "시작점 지정", () => sequence.startNodeId = node.id);
            Undo.CollapseUndoOperations(group); selectedNodeId = node.id; Rebuild();
        }
        public void Save() => FmvSequenceEditService.Save(sequence, layout);
        public void FitGraph()
        {
            if (graph == null || graph.nodes.Count() == 0) return;
            var bounds = graph.nodes.First().GetPosition();
            foreach (var node in graph.nodes)
            {
                var rect = node.GetPosition();
                bounds = Rect.MinMaxRect(Mathf.Min(bounds.xMin, rect.xMin), Mathf.Min(bounds.yMin, rect.yMin), Mathf.Max(bounds.xMax, rect.xMax), Mathf.Max(bounds.yMax, rect.yMax));
            }
            float scale = Mathf.Clamp(Mathf.Min((graph.layout.width - 70) / bounds.width, (graph.layout.height - 70) / bounds.height), 0.2f, 1);
            var center = new Vector2(graph.layout.width, graph.layout.height) / 2;
            graph.UpdateViewTransform(center - bounds.center * scale, Vector3.one * scale);
        }
        public static void SaveOpenEditors()
        {
            foreach (var window in Resources.FindObjectsOfTypeAll<FmvSequenceEditor>()) window.Save();
        }
        public void SelectNode(string id)
        {
            selectedNodeId = id; DrawDetails();
        }
        private void Edit(string name, Action action)
        {
            FmvSequenceEditService.Edit(sequence, name, action); QueueRefresh();
        }
        private void QueueRefresh()
        {
            if (refreshQueued || rootVisualElement == null) return;
            refreshQueued = true;
            rootVisualElement.schedule.Execute(() => { refreshQueued = false; Rebuild(); });
        }
        private void Rebuild()
        {
            if (graph == null) return;
            rootVisualElement.Q("playNotice").EnableInClassList("hidden", !ReadOnly);
            if (sequence != null) layout = layout != null && layout.sequenceGuid == AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(sequence)) ? layout : FmvGraphLayout.For(sequence);
            graph.Draw(sequence, layout, selectedNodeId, ReadOnly);
            DrawDetails();
        }
        private void DrawDetails()
        {
            if (details == null) return;
            details.Clear();
            var node = sequence != null ? sequence.FindNode(selectedNodeId) : null;
            videoPreview?.SetClip(node == null ? null : FmvSequenceEditService.Video(node));
            if (sequence == null) { details.Add(new HelpBox("시나리오를 선택하거나 새로 만드세요.", HelpBoxMessageType.Info)); return; }
            editingControls = new VisualElement(); details.Add(editingControls);
            if (node == null) editingControls.Add(new HelpBox("노드를 선택해 영상과 분기를 편집하세요.", HelpBoxMessageType.Info));
            else
            {
                Heading(editingControls, "영상 노드");
                var id = new Label(node.id); id.AddToClassList("node-id"); editingControls.Add(id);
                var title = new TextField("제목") { value = node.title, isDelayed = true };
                title.RegisterValueChangedCallback(e => Edit("노드 제목 변경", () => node.title = e.newValue)); editingControls.Add(title);
                var video = new ObjectField("영상") { objectType = typeof(VideoClip), allowSceneObjects = false, value = FmvSequenceEditService.Video(node) };
                video.RegisterValueChangedCallback(e => Edit("영상 변경", () => node.video = e.newValue == null ? null : new VideoReference(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(e.newValue))))); editingControls.Add(video);
                var start = new Toggle("시작 노드") { value = sequence.startNodeId == node.id };
                start.RegisterValueChangedCallback(e => Edit("시작점 변경", () => sequence.startNodeId = e.newValue ? node.id : "")); editingControls.Add(start);
                bool hasChoices = node.choices is { Count: > 0 };
                var mode = new PopupField<string>("진행 방식", new List<string> { "자동 진행", "선택지" }, hasChoices ? 1 : 0);
                mode.RegisterValueChangedCallback(_ => { FmvSequenceEditService.SetChoiceMode(sequence, node, mode.index == 1); QueueRefresh(); }); editingControls.Add(mode);
                if (!hasChoices) editingControls.Add(new Label(string.IsNullOrEmpty(node.nextNodeId) ? "다음 연결 없음 · 영상 종료 후 끝" : "다음: " + TargetName(node.nextNodeId)));
                else DrawChoices(node);
                var remove = new Button(() => { FmvSequenceEditService.Delete(sequence, new[] { node.id }, layout); selectedNodeId = null; QueueRefresh(); }) { text = "이 노드 삭제" };
                editingControls.Add(remove);
            }
            editingControls.SetEnabled(!ReadOnly);
            Heading(details, "검증 결과");
            var diagnostics = FmvSequenceEditService.Diagnostics(sequence);
            if (diagnostics.Count == 0) details.Add(new HelpBox("오류 없음 · Play 전에 자동 등록됩니다.", HelpBoxMessageType.Info));
            foreach (var diagnostic in diagnostics)
            {
                var button = new Button(() => { if (diagnostic.NodeId != null) graph.FocusNode(diagnostic.NodeId); }) { text = (diagnostic.Warning ? "경고 · " : "오류 · ") + diagnostic.Message };
                button.AddToClassList("diagnostic"); button.AddToClassList(diagnostic.Warning ? "warning-item" : "error-item"); details.Add(button);
            }
        }
        private void DrawChoices(FmvNode node)
        {
            Heading(editingControls, "선택지");
            for (int i = 0; i < node.choices.Count; i++)
            {
                int index = i; var choice = node.choices[i];
                var card = new VisualElement(); card.AddToClassList("choice-card"); editingControls.Add(card);
                if (choice == null) { card.Add(new Button(() => { FmvSequenceEditService.RemoveChoice(sequence, node, index); QueueRefresh(); }) { text = "잘못된 선택지 삭제" }); continue; }
                var text = new TextField((i + 1) + "번 문구") { value = choice.text, isDelayed = true };
                text.RegisterValueChangedCallback(e => Edit("선택지 문구 변경", () => choice.text = e.newValue)); card.Add(text);
                card.Add(new Label("연결: " + TargetName(choice.targetNodeId)));
                var row = new VisualElement(); row.AddToClassList("field-row"); card.Add(row);
                var up = new Button(() => { FmvSequenceEditService.MoveChoice(sequence, node, index, index - 1); QueueRefresh(); }) { text = "↑" }; up.SetEnabled(i > 0); row.Add(up);
                var down = new Button(() => { FmvSequenceEditService.MoveChoice(sequence, node, index, index + 1); QueueRefresh(); }) { text = "↓" }; down.SetEnabled(i < node.choices.Count - 1); row.Add(down);
                row.Add(new Button(() => { FmvSequenceEditService.RemoveChoice(sequence, node, index); QueueRefresh(); }) { text = "삭제" });
                if (node.timeoutMode == FmvTimeoutMode.Choice)
                {
                    var automatic = new Toggle("시간 초과 시 자동 선택") { value = choice.isTimeoutDefault };
                    automatic.RegisterValueChangedCallback(e => { FmvSequenceEditService.SetDefaultChoice(sequence, node, e.newValue ? index : -1); QueueRefresh(); }); card.Add(automatic);
                }
            }
            editingControls.Add(new Button(() => Edit("선택지 추가", () => node.choices.Add(new FmvChoice { text = "새 선택지" }))) { text = "+ 선택지" });
            Heading(editingControls, "시간 초과");
            var options = new List<string> { "사용 안 함", "일반 선택지 사용", "전용 분기 사용" };
            var timeout = new PopupField<string>("처리 방식", options, Mathf.Clamp((int)node.timeoutMode, 0, 2));
            timeout.RegisterValueChangedCallback(_ => { FmvSequenceEditService.SetTimeoutMode(sequence, node, (FmvTimeoutMode)timeout.index); QueueRefresh(); }); editingControls.Add(timeout);
            if (node.timeoutMode != FmvTimeoutMode.Disabled)
            {
                var seconds = new FloatField("제한 시간 (초)") { value = node.choiceTimeoutSeconds, isDelayed = true };
                seconds.RegisterValueChangedCallback(e => Edit("제한 시간 변경", () => node.choiceTimeoutSeconds = e.newValue)); editingControls.Add(seconds);
                editingControls.Add(new HelpBox(node.timeoutMode == FmvTimeoutMode.Branch ? "노드의 주황색 시간 초과 연결점을 다음 영상으로 연결하세요. 일반 버튼에는 표시하지 않습니다." : "위 선택지 중 하나를 자동 대상으로 지정하세요. 순서를 바꿔도 지정이 유지됩니다.", HelpBoxMessageType.Info));
            }
        }
        private string TargetName(string id) => string.IsNullOrEmpty(id) ? "미연결" : sequence.FindNode(id)?.title ?? "없는 노드: " + id;
        private static void Heading(VisualElement root, string text) { var label = new Label(text); label.AddToClassList("section-title"); root.Add(label); }

        private sealed class Exit
        {
            public string sourceId;
            public FmvExitKind kind;
            public int index;
            public string target;
        }
        private sealed class VideoNode : Node
        {
            public string Id;
            public Port Input;
            public Action<string> Selected;
            public override void OnSelected() { base.OnSelected(); Selected?.Invoke(Id); }
        }
        private sealed class SequenceGraph : GraphView
        {
            private readonly FmvSequenceEditor owner;
            private readonly Dictionary<string, VideoNode> views = new();
            private bool drawing;
            public SequenceGraph(FmvSequenceEditor owner)
            {
                this.owner = owner;
                var grid = new GridBackground(); Insert(0, grid); grid.StretchToParentSize(); SetupZoom(0.2f, 1.6f);
                this.AddManipulator(new ContentDragger()); this.AddManipulator(new SelectionDragger()); this.AddManipulator(new RectangleSelector());
                graphViewChanged = Changed;
            }
            public override List<Port> GetCompatiblePorts(Port start, NodeAdapter adapter) => ports.Where(p => p.direction != start.direction && p.portType == start.portType && !owner.ReadOnly).ToList();
            public override void BuildContextualMenu(ContextualMenuPopulateEvent e)
            {
                if (owner.sequence != null && !owner.ReadOnly)
                {
                    Vector2 at = contentViewContainer.WorldToLocal(e.mousePosition);
                    e.menu.AppendAction("영상 노드 추가", _ => owner.AddNode(at));
                }
                base.BuildContextualMenu(e);
            }
            public void FocusNode(string id)
            {
                if (!views.TryGetValue(id, out var node)) return;
                ClearSelection(); AddToSelection(node); owner.SelectNode(id); FrameSelection();
            }
            public void Draw(FmvSequenceDefinition sequence, FmvGraphLayout layout, string selected, bool readOnly)
            {
                drawing = true;
                try
                {
                    foreach (var element in graphElements.ToList()) RemoveElement(element);
                    views.Clear();
                    if (sequence == null) return;
                    var diagnostics = FmvSequenceEditService.Diagnostics(sequence);
                    var exits = new List<Port>();
                    int position = 0;
                    foreach (var data in sequence.nodes ?? new())
                    {
                        if (data == null || string.IsNullOrEmpty(data.id) || views.ContainsKey(data.id)) continue;
                        var node = new VideoNode { Id = data.id, title = data.title, Selected = owner.SelectNode, userData = data.id };
                        node.AddToClassList("fmv-node"); node.EnableInClassList("start-node", sequence.startNodeId == data.id);
                        node.EnableInClassList("has-error", diagnostics.Any(d => d.NodeId == data.id && !d.Warning));
                        node.EnableInClassList("has-warning", diagnostics.Any(d => d.NodeId == data.id && d.Warning));
                        if (readOnly) node.capabilities &= ~(Capabilities.Movable | Capabilities.Deletable | Capabilities.Copiable);
                        node.Input = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(VideoClip));
                        node.Input.portName = sequence.startNodeId == data.id ? "시작 · 입력" : "입력";
                        node.Input.userData = data.id; node.inputContainer.Add(node.Input);
                        var clip = FmvSequenceEditService.Video(data);
                        var video = new Button(() => FmvSequenceEditService.SelectVideo(data)) { text = clip != null ? "▶ " + clip.name + "  ·  Inspector" : "영상 누락 · 오른쪽에서 지정", name = "video-" + data.id };
                        video.AddToClassList("video-select"); video.SetEnabled(clip != null);
                        video.RegisterCallback<MouseDownEvent>(e => e.StopPropagation());
                        video.RegisterCallback<PointerDownEvent>(e => e.StopPropagation());
                        node.extensionContainer.Add(video);
                        void Output(string label, FmvExitKind kind, int index, string target)
                        {
                            var port = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(VideoClip));
                            port.portName = label; port.userData = new Exit { sourceId = data.id, kind = kind, index = index, target = target };
                            port.portColor = kind == FmvExitKind.Timeout ? new Color(1, 0.65f, 0.3f) : new Color(0.4f, 0.83f, 0.73f);
                            port.SetEnabled(!readOnly); node.outputContainer.Add(port); exits.Add(port);
                        }
                        if (data.choices is { Count: > 0 })
                        {
                            for (int i = 0; i < data.choices.Count; i++)
                            {
                                var c = data.choices[i];
                                Output((i + 1) + ". " + (c?.text ?? "잘못된 선택지") + (data.timeoutMode == FmvTimeoutMode.Choice && c?.isTimeoutDefault == true ? " [자동]" : ""), FmvExitKind.Choice, i, c?.targetNodeId);
                            }
                            if (data.timeoutMode == FmvTimeoutMode.Branch) Output("시간 초과", FmvExitKind.Timeout, -1, data.timeoutTargetNodeId);
                        }
                        else Output(string.IsNullOrEmpty(data.nextNodeId) ? "다음 · 미연결이면 종료" : "다음", FmvExitKind.Next, -1, data.nextNodeId);
                        var info = new Label(data.timeoutMode == FmvTimeoutMode.Disabled ? "제한 시간 없음" : $"선택 대기 {data.choiceTimeoutSeconds:0.##}초");
                        info.AddToClassList("node-status"); if (data.timeoutMode != FmvTimeoutMode.Disabled) info.AddToClassList("timeout-info"); node.extensionContainer.Add(info);
                        node.RefreshExpandedState(); node.RefreshPorts();
                        node.SetPosition(new Rect(layout.Position(data.id, position++), new Vector2(280, 210)));
                        views.Add(data.id, node); AddElement(node);
                    }
                    foreach (var port in exits)
                    {
                        var exit = (Exit)port.userData;
                        if (!string.IsNullOrEmpty(exit.target) && views.TryGetValue(exit.target, out var target))
                        {
                            var edge = port.ConnectTo(target.Input);
                            if (readOnly) edge.capabilities &= ~Capabilities.Deletable;
                            AddElement(edge);
                        }
                    }
                    if (!string.IsNullOrEmpty(selected) && views.TryGetValue(selected, out var chosen)) AddToSelection(chosen);
                }
                finally { drawing = false; }
            }
            private GraphViewChange Changed(GraphViewChange change)
            {
                if (drawing) return change;
                if (owner.ReadOnly) { change.elementsToRemove?.Clear(); change.edgesToCreate?.Clear(); change.movedElements?.Clear(); return change; }
                // GraphView removes an occupied edge and creates its replacement in two callbacks
                // in the same input event. Keep both records in Unity's current undo group.
                int undo = Undo.GetCurrentGroup();
                var removed = change.elementsToRemove?.OfType<VideoNode>().Select(n => n.Id).ToHashSet() ?? new HashSet<string>();
                if (removed.Count > 0) FmvSequenceEditService.Delete(owner.sequence, removed, owner.layout);
                foreach (var edge in change.elementsToRemove?.OfType<Edge>() ?? Enumerable.Empty<Edge>())
                    if (edge.output?.userData is Exit exit && !removed.Contains(exit.sourceId)) FmvSequenceEditService.Connect(owner.sequence, exit.sourceId, exit.kind, exit.index, "");
                foreach (var edge in change.edgesToCreate ?? new List<Edge>())
                    if (edge.output.userData is Exit exit) FmvSequenceEditService.Connect(owner.sequence, exit.sourceId, exit.kind, exit.index, (string)edge.input.userData);
                if (change.movedElements != null)
                {
                    Undo.RecordObject(owner.layout, "노드 이동");
                    foreach (var node in change.movedElements.OfType<VideoNode>()) owner.layout.SetPosition(node.Id, node.GetPosition().position);
                    EditorUtility.SetDirty(owner.layout);
                }
                Undo.CollapseUndoOperations(undo);
                owner.QueueRefresh(); return change;
            }
        }
    }
}
