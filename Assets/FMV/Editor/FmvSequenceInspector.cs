using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FmvDemo.Editor
{
    [CustomEditor(typeof(FmvSequenceDefinition))]
    public sealed class FmvSequenceInspector : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.Add(new Button(() => FmvSequenceEditor.Open((FmvSequenceDefinition)target)) { text = "FMV 노드 편집기 열기" });
            var details = new VisualElement(); root.Add(details);
            InspectorElement.FillDefaultInspector(details, serializedObject, this);
            return root;
        }
        [OnOpenAsset]
        private static bool OpenAsset(int id, int line)
        {
            if (EditorUtility.EntityIdToObject(id) is not FmvSequenceDefinition sequence) return false;
            FmvSequenceEditor.Open(sequence); return true;
        }
    }
}
