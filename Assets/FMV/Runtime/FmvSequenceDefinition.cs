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
    }

    [Serializable]
    public sealed class FmvNode
    {
        public string id;
        public string title;
        public VideoReference video;
        public string nextNodeId;
        public List<FmvChoice> choices = new();
    }

    [CreateAssetMenu(menuName = "FMV/Sequence")]
    public sealed class FmvSequenceDefinition : ScriptableObject
    {
        public string startNodeId = "wake";
        public List<FmvNode> nodes = new();
        public FmvNode FindNode(string id) => nodes?.FirstOrDefault(n => n != null && n.id == id);

        public List<string> Validate()
        {
            var errors = new List<string>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in nodes ?? new List<FmvNode>())
            {
                if (node == null || string.IsNullOrWhiteSpace(node.id)) { errors.Add("노드 ID가 비어 있습니다."); continue; }
                if (!ids.Add(node.id)) errors.Add($"중복 노드 ID: {node.id}");
                if (node.video == null || !node.video.RuntimeKeyIsValid()) errors.Add($"영상 누락: {node.id}");
                bool hasChoices = node.choices is { Count: > 0 };
                if (hasChoices && !string.IsNullOrEmpty(node.nextNodeId)) errors.Add($"선택지와 자동 이동을 동시에 지정했습니다: {node.id}");
            }
            if (!ids.Contains(startNodeId ?? "")) errors.Add("시작 노드를 찾을 수 없습니다.");
            foreach (var node in nodes ?? new List<FmvNode>())
            {
                if (node == null) continue;
                if (!string.IsNullOrEmpty(node.nextNodeId) && !ids.Contains(node.nextNodeId)) errors.Add($"잘못된 연결: {node.id} → {node.nextNodeId}");
                foreach (var choice in node.choices ?? new List<FmvChoice>())
                    if (choice == null || string.IsNullOrWhiteSpace(choice.text) || !ids.Contains(choice.targetNodeId ?? ""))
                        errors.Add($"잘못된 선택지: {node.id}");
            }
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
