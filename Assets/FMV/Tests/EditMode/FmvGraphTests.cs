using System;
using System.Collections;
using System.IO;
using System.Linq;
using FmvDemo.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace FmvDemo.Tests
{
    public sealed class FmvGraphTests
    {
        private FmvSequenceDefinition sequence;
        private FmvGraphLayout layout;
        [SetUp] public void Setup()
        {
            sequence = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<FmvSequenceDefinition>(FmvProjectSetup.SequencePath));
            layout = ScriptableObject.CreateInstance<FmvGraphLayout>();
        }
        [TearDown] public void Cleanup()
        {
            Undo.ClearUndo(sequence); Undo.ClearUndo(layout);
            UnityEngine.Object.DestroyImmediate(sequence); UnityEngine.Object.DestroyImmediate(layout);
        }
        [Test] public void AddingNode_HasUniqueIdAndNoTimer()
        {
            var first = FmvSequenceEditService.Add(sequence); var second = FmvSequenceEditService.Add(sequence);
            Assert.That(first.id, Is.Not.EqualTo(second.id)); Assert.That(first.timeoutMode, Is.EqualTo(FmvTimeoutMode.Disabled));
            Assert.That(first.choiceTimeoutSeconds, Is.EqualTo(10));
        }
        [Test] public void ModeChange_UndoRestoresAllConnectionsAndTimer()
        {
            string before = JsonUtility.ToJson(sequence);
            Undo.IncrementCurrentGroup();
            FmvSequenceEditService.SetChoiceMode(sequence, sequence.FindNode("wake"), false);
            Undo.FlushUndoRecordObjects();
            Assert.That(sequence.FindNode("wake").choices, Is.Empty);
            Undo.PerformUndo(); Assert.That(JsonUtility.ToJson(sequence), Is.EqualTo(before));
            Undo.PerformRedo(); Assert.That(sequence.FindNode("wake").timeoutMode, Is.EqualTo(FmvTimeoutMode.Disabled));
        }
        [Test] public void Delete_ClearsAllIncomingAndStart_PreservesText_UndoRestores()
        {
            sequence.startNodeId = "sleep";
            string before = JsonUtility.ToJson(sequence);
            Undo.IncrementCurrentGroup();
            FmvSequenceEditService.Delete(sequence, new[] { "sleep" }, layout); Undo.FlushUndoRecordObjects();
            Assert.That(sequence.startNodeId, Is.Empty);
            Assert.That(sequence.FindNode("wake").timeoutTargetNodeId, Is.Empty);
            Assert.That(sequence.FindNode("wake").choices[1].text, Is.EqualTo("다시 눕는다"));
            Assert.That(sequence.FindNode("wake").choices[1].targetNodeId, Is.Empty);
            Assert.That(sequence.Validate(), Is.Not.Empty);
            Undo.PerformUndo(); Assert.That(JsonUtility.ToJson(sequence), Is.EqualTo(before));
        }
        [Test] public void Reorder_KeepsAutomaticChoice_DeletingItDoesNotSelectAnother()
        {
            var node = sequence.FindNode("wake");
            FmvSequenceEditService.SetTimeoutMode(sequence, node, FmvTimeoutMode.Choice);
            FmvSequenceEditService.SetDefaultChoice(sequence, node, 1);
            FmvSequenceEditService.MoveChoice(sequence, node, 1, 0);
            Assert.That(node.choices[0].isTimeoutDefault, Is.True);
            Assert.That(node.choices[0].targetNodeId, Is.EqualTo("sleep"));
            Assert.That(sequence.Validate(), Is.Empty);
            FmvSequenceEditService.RemoveChoice(sequence, node, 0);
            Assert.That(node.choices.Any(c => c.isTimeoutDefault), Is.False);
            Assert.That(sequence.Validate().Any(e => e.Contains("자동 대상")), Is.True);
        }
        [TestCase(0f)] [TestCase(-1f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void InvalidDuration_IsRejected(float duration)
        {
            sequence.FindNode("wake").choiceTimeoutSeconds = duration;
            Assert.That(sequence.Validate().Any(e => e.Contains("제한 시간")), Is.True);
        }
        [Test] public void TimerValidation_RejectsNoChoicesMissingOrDuplicateDefaultsAndBadTarget()
        {
            var node = sequence.FindNode("wake");
            node.timeoutTargetNodeId = "missing"; Assert.That(sequence.Validate(), Is.Not.Empty);
            FmvSequenceEditService.SetTimeoutMode(sequence, node, FmvTimeoutMode.Choice);
            Assert.That(sequence.Validate(), Is.Not.Empty);
            foreach (var c in node.choices) c.isTimeoutDefault = true;
            Assert.That(sequence.Validate(), Is.Not.Empty);
            node.choices.Clear(); Assert.That(sequence.Validate(), Is.Not.Empty);
        }
        [Test] public void CyclesAndSelfConnections_AreValid_UnreachableIsWarning()
        {
            FmvSequenceEditService.Connect(sequence, "sleep", FmvExitKind.Next, -1, "sleep");
            Assert.That(sequence.Validate(), Is.Empty);
            var isolated = FmvSequenceEditService.Add(sequence); isolated.video = sequence.nodes[0].video;
            Assert.That(sequence.Validate(), Is.Empty);
            Assert.That(sequence.GetDiagnostics().Any(d => d.Warning && d.NodeId == isolated.id), Is.True);
        }
        [Test] public void SelectingVideo_UsesReferenceWithoutChangingSequence()
        {
            var previous = Selection.activeObject;
            try
            {
                string before = JsonUtility.ToJson(sequence);
                var first = sequence.FindNode("wake"); var second = sequence.FindNode("walk"); second.video = first.video;
                before = JsonUtility.ToJson(sequence);
                Assert.That(FmvSequenceEditService.SelectVideo(first), Is.True);
                var selected = Selection.activeObject;
                Assert.That(selected, Is.SameAs(FmvSequenceEditService.Video(first)));
                Assert.That(FmvSequenceEditService.SelectVideo(second), Is.True); Assert.That(Selection.activeObject, Is.SameAs(selected));
                Assert.That(JsonUtility.ToJson(sequence), Is.EqualTo(before));
                Assert.That(FmvSequenceEditService.SelectVideo(new FmvNode()), Is.False);
                Assert.That(Selection.activeObject, Is.SameAs(selected));
            }
            finally { Selection.activeObject = previous; }
        }
        [Test] public void Layout_DoesNotChangeContentFingerprint_ButTimeoutDoes()
        {
            string fingerprint = FmvContentPipeline.Fingerprint();
            var original = AssetDatabase.LoadAssetAtPath<FmvSequenceDefinition>(FmvProjectSetup.SequencePath);
            var savedLayout = FmvGraphLayout.For(original);
            var oldPosition = savedLayout.Position("wake", 0);
            try
            {
                savedLayout.SetPosition("wake", oldPosition + new Vector2(120, 480));
                EditorUtility.SetDirty(savedLayout); AssetDatabase.SaveAssetIfDirty(savedLayout);
                Assert.That(FmvContentPipeline.Fingerprint(), Is.EqualTo(fingerprint));
            }
            finally { savedLayout.SetPosition("wake", oldPosition); EditorUtility.SetDirty(savedLayout); AssetDatabase.SaveAssetIfDirty(savedLayout); }
            float previous = original.FindNode("wake").choiceTimeoutSeconds;
            try
            {
                original.FindNode("wake").choiceTimeoutSeconds = previous + 1;
                EditorUtility.SetDirty(original); AssetDatabase.SaveAssetIfDirty(original);
                Assert.That(FmvContentPipeline.Fingerprint(), Is.Not.EqualTo(fingerprint));
            }
            finally { original.FindNode("wake").choiceTimeoutSeconds = previous; EditorUtility.SetDirty(original); AssetDatabase.SaveAssetIfDirty(original); }
        }
        [Test] public void MovedAndDeletedVideo_SelectionResolvesGuidWithoutEditingSequence()
        {
            string first = "Assets/FMV/Editor/graph-test-" + Guid.NewGuid().ToString("N") + ".mp4";
            string moved = first.Replace(".mp4", "-moved.mp4");
            var previous = Selection.activeObject;
            try
            {
                Assert.That(AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(FmvSequenceEditService.Video(sequence.nodes[0])), first), Is.True);
                AssetDatabase.ImportAsset(first, ImportAssetOptions.ForceSynchronousImport);
                var node = sequence.nodes[0]; node.video = new VideoReference(AssetDatabase.AssetPathToGUID(first));
                string before = JsonUtility.ToJson(sequence);
                Assert.That(AssetDatabase.MoveAsset(first, moved), Is.Empty);
                Assert.That(FmvSequenceEditService.SelectVideo(node), Is.True);
                Assert.That(AssetDatabase.GetAssetPath(Selection.activeObject), Is.EqualTo(moved));
                Assert.That(JsonUtility.ToJson(sequence), Is.EqualTo(before));
                AssetDatabase.DeleteAsset(moved);
                Assert.That(FmvSequenceEditService.SelectVideo(node), Is.False);
                Assert.That(FmvSequenceEditService.Diagnostics(sequence).Any(d => d.NodeId == node.id && !d.Warning), Is.True);
            }
            finally { Selection.activeObject = previous; AssetDatabase.DeleteAsset(first); AssetDatabase.DeleteAsset(moved); }
        }
        [UnityTest] public IEnumerator GraphWindow_ReopensSavedLayout_SelectionDoesNotEditContent()
        {
            var original = AssetDatabase.LoadAssetAtPath<FmvSequenceDefinition>(FmvProjectSetup.SequencePath);
            var selectedObject = Selection.activeObject;
            var window = FmvSequenceEditor.Open(original);
            var originalLayout = FmvGraphLayout.For(original);
            var position = originalLayout.Position("wake", 0);
            string before = File.ReadAllText(AssetDatabase.GetAssetPath(original));
            string fingerprint = FmvContentPipeline.Fingerprint();
            try
            {
                yield return null;
                Assert.That(window.Graph.nodes.Count(), Is.EqualTo(original.nodes.Count));
                window.SelectNode("wake");
                var viewPosition = window.Graph.contentViewContainer.style.translate;
                Assert.That(FmvSequenceEditService.SelectVideo(original.FindNode("walk")), Is.True);
                yield return null;
                Assert.That(window.Sequence, Is.SameAs(original)); Assert.That(window.SelectedNodeId, Is.EqualTo("wake"));
                Assert.That(window.Graph.contentViewContainer.style.translate, Is.EqualTo(viewPosition));
                window.Close();
                window = FmvSequenceEditor.Open(original);
                yield return null;
                var wake = window.Graph.nodes.Single(n => (string)n.userData == "wake");
                Assert.That(wake.GetPosition().position, Is.EqualTo(position));
                Assert.That(File.ReadAllText(AssetDatabase.GetAssetPath(original)), Is.EqualTo(before));
                Assert.That(FmvContentPipeline.Fingerprint(), Is.EqualTo(fingerprint));
            }
            finally { Selection.activeObject = selectedObject; window.SelectNode("wake"); }
        }
    }
}
