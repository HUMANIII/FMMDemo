using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;

namespace FmvDemo.Editor
{
    public enum FmvExitKind { Next, Choice, Timeout }

    public static class FmvSequenceEditService
    {
        public static void Edit(FmvSequenceDefinition sequence, string action, Action change)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play 중에는 그래프를 수정할 수 없습니다.");
            Undo.RecordObject(sequence, action); change(); EditorUtility.SetDirty(sequence);
        }
        public static FmvNode Add(FmvSequenceDefinition sequence)
        {
            var node = new FmvNode { id = "node_" + Guid.NewGuid().ToString("N"), title = "새 영상" };
            Edit(sequence, "영상 노드 추가", () => { sequence.nodes ??= new(); sequence.nodes.Add(node); });
            return node;
        }
        public static void Delete(FmvSequenceDefinition sequence, IEnumerable<string> nodeIds, FmvGraphLayout layout)
        {
            var ids = nodeIds.ToHashSet();
            Undo.RecordObject(layout, "노드 배치 삭제");
            Edit(sequence, "영상 노드 삭제", () =>
            {
                sequence.nodes.RemoveAll(n => n != null && ids.Contains(n.id));
                if (ids.Contains(sequence.startNodeId ?? "")) sequence.startNodeId = "";
                foreach (var node in sequence.nodes.Where(n => n != null))
                {
                    if (ids.Contains(node.nextNodeId ?? "")) node.nextNodeId = "";
                    if (ids.Contains(node.timeoutTargetNodeId ?? "")) node.timeoutTargetNodeId = "";
                    foreach (var c in node.choices ?? new()) if (c != null && ids.Contains(c.targetNodeId ?? "")) c.targetNodeId = "";
                }
                layout.positions.RemoveAll(p => ids.Contains(p.nodeId));
            });
            EditorUtility.SetDirty(layout);
        }
        public static void SetChoiceMode(FmvSequenceDefinition sequence, FmvNode node, bool choices)
        {
            Edit(sequence, "진행 방식 변경", () =>
            {
                node.nextNodeId = ""; node.choices = new(); node.timeoutMode = FmvTimeoutMode.Disabled; node.timeoutTargetNodeId = "";
                if (choices) { node.choices.Add(new FmvChoice { text = "선택지 1" }); node.choices.Add(new FmvChoice { text = "선택지 2" }); }
            });
        }
        public static void SetTimeoutMode(FmvSequenceDefinition sequence, FmvNode node, FmvTimeoutMode mode)
        {
            Edit(sequence, "시간 초과 방식 변경", () =>
            {
                node.timeoutMode = mode; node.timeoutTargetNodeId = "";
                foreach (var choice in node.choices ?? new()) if (choice != null) choice.isTimeoutDefault = false;
                if (node.choiceTimeoutSeconds <= 0 || float.IsNaN(node.choiceTimeoutSeconds) || float.IsInfinity(node.choiceTimeoutSeconds)) node.choiceTimeoutSeconds = 10;
            });
        }
        public static void SetDefaultChoice(FmvSequenceDefinition sequence, FmvNode node, int index) => Edit(sequence, "자동 대상 변경", () =>
        {
            for (int i = 0; i < node.choices.Count; i++) if (node.choices[i] != null) node.choices[i].isTimeoutDefault = i == index;
        });
        public static void RemoveChoice(FmvSequenceDefinition sequence, FmvNode node, int index) => Edit(sequence, "선택지 삭제", () =>
        {
            node.choices.RemoveAt(index);
            if (node.choices.Count == 0) { node.timeoutMode = FmvTimeoutMode.Disabled; node.timeoutTargetNodeId = ""; }
        });
        public static void MoveChoice(FmvSequenceDefinition sequence, FmvNode node, int index, int destination) => Edit(sequence, "선택지 순서 변경", () =>
        {
            var choice = node.choices[index]; node.choices.RemoveAt(index); node.choices.Insert(destination, choice);
        });
        public static void Connect(FmvSequenceDefinition sequence, string sourceId, FmvExitKind kind, int index, string targetId)
        {
            var source = sequence.FindNode(sourceId) ?? throw new ArgumentException("원본 노드가 없습니다.");
            if (!string.IsNullOrEmpty(targetId) && sequence.FindNode(targetId) == null) throw new ArgumentException("대상 노드가 없습니다.");
            Edit(sequence, "영상 연결 변경", () =>
            {
                switch (kind)
                {
                    case FmvExitKind.Next: source.nextNodeId = targetId; break;
                    case FmvExitKind.Choice: source.choices[index].targetNodeId = targetId; break;
                    case FmvExitKind.Timeout: source.timeoutTargetNodeId = targetId; break;
                }
            });
        }
        public static VideoClip Video(FmvNode node) => node?.video == null ? null : AssetDatabase.LoadAssetAtPath<VideoClip>(AssetDatabase.GUIDToAssetPath(node.video.AssetGUID));
        public static bool SelectVideo(FmvNode node)
        {
            var clip = Video(node);
            if (clip == null) return false;
            Selection.activeObject = clip; EditorGUIUtility.PingObject(clip); return true;
        }
        public static List<FmvDiagnostic> Diagnostics(FmvSequenceDefinition sequence)
        {
            var result = sequence.GetDiagnostics();
            foreach (var node in sequence.nodes ?? new())
                if (node != null && node.video?.RuntimeKeyIsValid() == true && Video(node) == null)
                    result.Add(new FmvDiagnostic(node.id, "영상 파일을 찾을 수 없습니다: " + node.id));
            return result;
        }
        public static void Save(FmvSequenceDefinition sequence, FmvGraphLayout layout)
        {
            if (sequence != null) AssetDatabase.SaveAssetIfDirty(sequence);
            if (layout != null) AssetDatabase.SaveAssetIfDirty(layout);
        }
    }
}
