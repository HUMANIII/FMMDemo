using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Video;

namespace FmvDemo
{
    [Serializable]
    public sealed class VideoReference : AssetReferenceT<VideoClip>
    {
        public VideoReference(string guid) : base(guid) { }
    }

    [Serializable]
    public sealed class FmvChoice
    {
        public string text;
        public string targetNodeId;
        public bool isTimeoutDefault;
    }

    public enum FmvTimeoutMode { Disabled, Choice, Branch }

    public sealed class FmvDiagnostic
    {
        public string NodeId { get; }
        public string Message { get; }
        public bool Warning { get; }
        public FmvDiagnostic(string nodeId, string message, bool warning = false)
        { NodeId = nodeId; Message = message; Warning = warning; }
    }

    [Serializable]
    public sealed class FmvNode
    {
        public string id;
        public string title;
        public VideoReference video;
        public string nextNodeId;
        public List<FmvChoice> choices = new();
        public FmvTimeoutMode timeoutMode;
        public float choiceTimeoutSeconds = 10;
        public string timeoutTargetNodeId;
    }

    [CreateAssetMenu(menuName = "FMV/Sequence")]
    public sealed class FmvSequenceDefinition : ScriptableObject
    {
        public string startNodeId = "wake";
        public List<FmvNode> nodes = new();
        public FmvNode FindNode(string id) => nodes?.FirstOrDefault(n => n != null && n.id == id);

        public List<string> Validate() => GetDiagnostics().Where(d => !d.Warning).Select(d => d.Message).ToList();

        public List<FmvDiagnostic> GetDiagnostics()
        {
            var errors = new List<FmvDiagnostic>();
            void Error(string id, string message) => errors.Add(new FmvDiagnostic(id, message));
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in nodes ?? new List<FmvNode>())
            {
                if (node == null || string.IsNullOrWhiteSpace(node.id)) { Error(null, "노드 ID가 비어 있습니다."); continue; }
                if (!ids.Add(node.id)) Error(node.id, $"중복 노드 ID: {node.id}");
                if (node.video == null || !node.video.RuntimeKeyIsValid()) Error(node.id, $"영상 누락: {node.id}");
                bool hasChoices = node.choices is { Count: > 0 };
                if (hasChoices && !string.IsNullOrEmpty(node.nextNodeId)) Error(node.id, $"선택지와 자동 이동을 동시에 지정했습니다: {node.id}");
                if (!Enum.IsDefined(typeof(FmvTimeoutMode), node.timeoutMode)) Error(node.id, $"알 수 없는 시간 초과 방식: {node.id}");
                if (node.timeoutMode != FmvTimeoutMode.Disabled)
                {
                    if (!hasChoices) Error(node.id, $"시간 초과에는 일반 선택지가 필요합니다: {node.id}");
                    if (float.IsNaN(node.choiceTimeoutSeconds) || float.IsInfinity(node.choiceTimeoutSeconds) || node.choiceTimeoutSeconds <= 0)
                        Error(node.id, $"제한 시간은 유한한 양수여야 합니다: {node.id}");
                    int defaults = node.choices?.Count(c => c != null && c.isTimeoutDefault) ?? 0;
                    if (node.timeoutMode == FmvTimeoutMode.Choice && defaults != 1)
                        Error(node.id, $"자동 대상 선택지를 하나 지정하세요: {node.id}");
                    if (node.timeoutMode == FmvTimeoutMode.Choice && !string.IsNullOrEmpty(node.timeoutTargetNodeId))
                        Error(node.id, $"일반 선택지와 전용 시간 초과 분기를 동시에 지정했습니다: {node.id}");
                    if (node.timeoutMode == FmvTimeoutMode.Branch && defaults != 0)
                        Error(node.id, $"전용 분기에서는 일반 자동 대상 지정을 해제하세요: {node.id}");
                }
            }
            if (!ids.Contains(startNodeId ?? "")) Error(null, "시작 노드를 찾을 수 없습니다.");
            foreach (var node in nodes ?? new List<FmvNode>())
            {
                if (node == null) continue;
                if (!string.IsNullOrEmpty(node.nextNodeId) && !ids.Contains(node.nextNodeId)) Error(node.id, $"잘못된 연결: {node.id} → {node.nextNodeId}");
                if (node.timeoutMode == FmvTimeoutMode.Branch && !ids.Contains(node.timeoutTargetNodeId ?? ""))
                    Error(node.id, $"시간 초과 연결 누락: {node.id}");
                foreach (var choice in node.choices ?? new List<FmvChoice>())
                    if (choice == null || string.IsNullOrWhiteSpace(choice.text) || !ids.Contains(choice.targetNodeId ?? ""))
                        Error(node.id, $"잘못된 선택지: {node.id}");
            }
            var reached = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Stack<string>();
            if (ids.Contains(startNodeId ?? "")) pending.Push(startNodeId);
            while (pending.Count > 0)
            {
                string id = pending.Pop();
                if (!reached.Add(id)) continue;
                var node = FindNode(id);
                if (node == null) continue;
                void Visit(string target) { if (!string.IsNullOrEmpty(target) && ids.Contains(target)) pending.Push(target); }
                Visit(node.nextNodeId);
                foreach (var choice in node.choices ?? new List<FmvChoice>()) Visit(choice?.targetNodeId);
                if (node.timeoutMode == FmvTimeoutMode.Branch) Visit(node.timeoutTargetNodeId);
            }
            foreach (var id in ids.Where(id => !reached.Contains(id))) errors.Add(new FmvDiagnostic(id, $"시작점에서 도달할 수 없습니다: {id}", true));
            return errors;
        }
    }

    public static class FmvContentIds
    {
        public const string Label = "fmv";
        public static string Video(string guid) => "fmv/video/" + guid;
        public static string Sequence(string guid) => "fmv/sequence/" + guid;
        public static string SequenceLabel(string guid) => "fmv-sequence-" + guid;
    }
}
