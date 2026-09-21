using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using FmvDemo.Editor;

namespace FmvDemo.Tests
{
    public sealed class FmvContentTests
    {
        private const string Fixture = "Assets/FMV/Content/__Verification";
        [TearDown]
        public void Cleanup()
        {
            if (AssetDatabase.IsValidFolder(Fixture)) AssetDatabase.DeleteAsset(Fixture);
            FmvContentPipeline.Synchronize();
        }

        [Test]
        public void ShippedSequence_HasBothCompleteLoops()
        {
            var sequence = AssetDatabase.LoadAssetAtPath<FmvSequenceDefinition>(FmvProjectSetup.SequencePath);
            Assert.That(sequence.Validate(), Is.Empty);
            Assert.That(sequence.startNodeId, Is.EqualTo("wake"));
            Assert.That(sequence.FindNode("wake").choices.Select(c => c.targetNodeId), Is.EqualTo(new[] { "walk", "sleep" }));
            Assert.That(sequence.FindNode("walk").nextNodeId, Is.EqualTo("computer"));
            Assert.That(sequence.FindNode("computer").nextNodeId, Is.EqualTo("return"));
            Assert.That(sequence.FindNode("return").nextNodeId, Is.EqualTo("wake"));
            Assert.That(sequence.FindNode("sleep").nextNodeId, Is.EqualTo("wake"));
        }

        [TestCase("duplicate")]
        [TestCase("missingVideo")]
        [TestCase("badChoice")]
        [TestCase("badNext")]
        [TestCase("bothExits")]
        [TestCase("badStart")]
        public void InvalidDefinitions_AreRejected(string defect)
        {
            var copy = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<FmvSequenceDefinition>(FmvProjectSetup.SequencePath));
            try
            {
                switch (defect)
                {
                    case "duplicate": copy.nodes[1].id = copy.nodes[0].id; break;
                    case "missingVideo": copy.nodes[0].video = null; break;
                    case "badChoice": copy.nodes[0].choices[0].targetNodeId = "absent"; break;
                    case "badNext": copy.nodes[1].nextNodeId = "absent"; break;
                    case "bothExits": copy.nodes[0].nextNodeId = "sleep"; break;
                    case "badStart": copy.startNodeId = "absent"; break;
                }
                Assert.That(copy.Validate(), Is.Not.Empty);
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
        }

        [Test]
        public void Synchronize_RegistersSharesMovesAndRemovesWithoutRepeatedChanges()
        {
            FmvContentPipeline.Folder(Fixture);
            string videoPath = Fixture + "/copy.mp4";
            Assert.That(AssetDatabase.CopyAsset("Assets/FMV/Content/Videos/wake.mp4", videoPath), Is.True);
            AssetDatabase.ImportAsset(videoPath, ImportAssetOptions.ForceSynchronousImport);
            string videoGuid = AssetDatabase.AssetPathToGUID(videoPath);
            var sequence = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<FmvSequenceDefinition>(FmvProjectSetup.SequencePath));
            sequence.nodes[0].video = new VideoReference(videoGuid);
            sequence.nodes[1].video = new VideoReference(videoGuid);
            AssetDatabase.CreateAsset(sequence, Fixture + "/sequence.asset");
            FmvContentPipeline.Synchronize();
            var entry = FmvContentPipeline.Settings.FindAssetEntry(videoGuid);
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry.parentGroup.Name, Is.EqualTo(FmvContentPipeline.VideoGroup));
            Assert.That(entry.address, Is.EqualTo(FmvContentIds.Video(videoGuid)));
            Assert.That(entry.labels, Does.Contain(FmvContentIds.Label));
            Assert.That(FmvContentPipeline.Settings.FindGroup(FmvContentPipeline.VideoGroup).entries.Count(e => e.guid == videoGuid), Is.EqualTo(1));
            var before = Directory.GetFiles("Assets/AddressableAssetsData", "*.asset", SearchOption.AllDirectories).ToDictionary(p => p, File.ReadAllText);
            FmvContentPipeline.Synchronize();
            foreach (var pair in before) Assert.That(File.ReadAllText(pair.Key), Is.EqualTo(pair.Value), pair.Key);
            string address = entry.address;
            Assert.That(AssetDatabase.MoveAsset(videoPath, Fixture + "/renamed.mp4"), Is.Empty);
            FmvContentPipeline.Synchronize();
            Assert.That(FmvContentPipeline.Settings.FindAssetEntry(videoGuid).address, Is.EqualTo(address));
            AssetDatabase.DeleteAsset(Fixture + "/sequence.asset");
            AssetDatabase.DeleteAsset(Fixture + "/renamed.mp4");
            FmvContentPipeline.Synchronize();
            Assert.That(FmvContentPipeline.Settings.FindAssetEntry(videoGuid), Is.Null);
        }

        [Test]
        public void MissingVideoFile_BlocksBuildBeforeRegisteringInvalidContent()
        {
            FmvContentPipeline.Folder(Fixture);
            var sequence = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<FmvSequenceDefinition>(FmvProjectSetup.SequencePath));
            sequence.nodes[0].video = new VideoReference("11111111111111111111111111111111");
            AssetDatabase.CreateAsset(sequence, Fixture + "/invalid.asset");
            Assert.Throws<InvalidOperationException>(() => FmvContentPipeline.Synchronize());
            Assert.That(FmvContentPipeline.Settings.FindAssetEntry("11111111111111111111111111111111"), Is.Null);
        }

        [Test]
        public void ContentChanges_InvalidateExistingBuildFingerprint()
        {
            var sequence = AssetDatabase.LoadAssetAtPath<FmvSequenceDefinition>(FmvProjectSetup.SequencePath);
            string title = sequence.nodes[0].title;
            string before = FmvContentPipeline.Fingerprint();
            try
            {
                sequence.nodes[0].title = title + " changed";
                EditorUtility.SetDirty(sequence); AssetDatabase.SaveAssets();
                Assert.That(FmvContentPipeline.Fingerprint(), Is.Not.EqualTo(before));
            }
            finally { sequence.nodes[0].title = title; EditorUtility.SetDirty(sequence); AssetDatabase.SaveAssets(); }
        }

        [Test]
        public void ReplacingVideoBytesWithSameGuid_InvalidatesExistingBuildFingerprint()
        {
            FmvContentPipeline.Folder(Fixture);
            string path = Fixture + "/replacement.mp4";
            AssetDatabase.CopyAsset("Assets/FMV/Content/Videos/wake.mp4", path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            string guid = AssetDatabase.AssetPathToGUID(path);
            var sequence = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<FmvSequenceDefinition>(FmvProjectSetup.SequencePath));
            sequence.nodes[0].video = new VideoReference(guid);
            AssetDatabase.CreateAsset(sequence, Fixture + "/replacement.asset");
            FmvContentPipeline.Synchronize();
            string before = FmvContentPipeline.Fingerprint();
            File.Copy("Assets/FMV/Content/Videos/sleep.mp4", path, true);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
            Assert.That(FmvContentPipeline.Fingerprint(), Is.Not.EqualTo(before));
        }
    }
}
