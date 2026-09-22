using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FmvDemo.Editor
{
    public sealed class FmvGraphLayout : ScriptableObject
    {
        [Serializable] public sealed class Entry { public string nodeId; public Vector2 position; }
        public string sequenceGuid;
        public List<Entry> positions = new();
        public Vector2 Position(string id, int index) => positions.FirstOrDefault(p => p.nodeId == id)?.position ?? new Vector2(index % 3 * 360, index / 3 * 340);
        public void SetPosition(string id, Vector2 value)
        {
            var entry = positions.FirstOrDefault(p => p.nodeId == id);
            if (entry == null) { entry = new Entry { nodeId = id }; positions.Add(entry); }
            entry.position = value;
        }
        public static FmvGraphLayout For(FmvSequenceDefinition sequence)
        {
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(sequence));
            if (string.IsNullOrEmpty(guid)) throw new InvalidOperationException("시나리오를 먼저 에셋으로 저장하세요.");
            const string folder = "Assets/FMV/Editor/GraphLayouts";
            string path = folder + "/" + guid + ".asset";
            var layout = AssetDatabase.LoadAssetAtPath<FmvGraphLayout>(path);
            if (layout != null) return layout;
            FmvContentPipeline.Folder(folder);
            layout = CreateInstance<FmvGraphLayout>(); layout.sequenceGuid = guid;
            int index = 0;
            foreach (var node in sequence.nodes.Where(n => n != null && !string.IsNullOrEmpty(n.id)))
                layout.SetPosition(node.id, layout.Position(node.id, index++));
            AssetDatabase.CreateAsset(layout, path);
            return layout;
        }
    }
}
