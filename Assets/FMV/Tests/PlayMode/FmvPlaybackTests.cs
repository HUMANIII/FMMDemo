using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.EventSystems;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Video;

namespace FmvDemo.Tests
{
    public sealed class FmvPlaybackTests
    {
        private FmvPlayer player;
        [UnitySetUp]
        public IEnumerator Setup()
        {
            yield return SceneManager.LoadSceneAsync("FmvDemo");
            player = UnityEngine.Object.FindFirstObjectByType<FmvPlayer>();
            Assert.That(player, Is.Not.Null);
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (player != null)
            {
                player.StopSequence();
                yield return null;
                Assert.That(player.Assets?.ActiveHandleCount ?? 0, Is.Zero);
                Assert.That(player.Video.clip, Is.Null);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        private IEnumerator Until(Func<bool> condition, float timeout = 35)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!condition())
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), $"Timed out. State={player.State}, node={player.CurrentNode?.id}, frame={player.Video.frame}, time={player.Video.time}, length={player.Video.length}, playing={player.Video.isPlaying}, error={player.ErrorMessage}");
                yield return null;
            }
        }

        [UnityTest]
        [Timeout(420000)]
        public IEnumerator BothBranches_TenLoopsThroughUi_RealFramesAndNoGrowingHandles()
        {
            var completed = new List<string>();
            var decoded = new HashSet<string>();
            player.NodeCompleted += completed.Add;
            player.Video.sendFrameReadyEvents = true;
            VideoPlayer.FrameReadyEventHandler frame = (_, index) => { if (index > 0 && player.CurrentNode != null) decoded.Add(player.CurrentNode.id); };
            player.Video.frameReady += frame;
            var view = UnityEngine.Object.FindFirstObjectByType<FmvView>();
            yield return Until(() => player.State == FmvPlaybackState.AwaitingChoice);
            for (int branch = 0; branch < 2; branch++)
            for (int loop = 0; loop < 10; loop++)
            {
                completed.Clear();
                Debug.Log($"[FMV repeat] branch={branch}, loop={loop + 1}");
                Assert.That(view.choicePanel.activeInHierarchy, Is.True);
                ExecuteEvents.Execute(view.choiceButtons[branch].gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
                Assert.That(player.State, Is.Not.EqualTo(FmvPlaybackState.AwaitingChoice));
                Assert.That(player.Choose(1 - branch), Is.False, "Repeated click must not change the in-flight branch");
                yield return Until(() => player.State == FmvPlaybackState.AwaitingChoice);
                Assert.That(completed, Is.EqualTo(branch == 0 ? new[] { "walk", "computer", "return", "wake" } : new[] { "sleep", "wake" }));
                Assert.That(player.Assets.ActiveHandleCount, Is.EqualTo(2), "Only the definition and current video may remain loaded");
                Assert.That(player.Assets.OwnerCount, Is.EqualTo(2));
            }
            Assert.That(decoded, Is.EquivalentTo(new[] { "wake", "walk", "computer", "return", "sleep" }));
            player.Video.frameReady -= frame;
            player.NodeCompleted -= completed.Add;
        }

        [UnityTest]
        public IEnumerator ChoiceWait_HoldsFrameWithoutAutoSelecting()
        {
            yield return Until(() => player.State == FmvPlaybackState.AwaitingChoice);
            long frame = player.Video.frame;
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.That(player.State, Is.EqualTo(FmvPlaybackState.AwaitingChoice));
            Assert.That(player.Video.frame, Is.EqualTo(frame));
            Assert.That(player.Choose(-1), Is.False);
            Assert.That(player.Choose(2), Is.False);
        }

        [UnityTest]
        public IEnumerator RestartDuringLoading_CancelsOldWorkAndReturnsToStart()
        {
            yield return Until(() => player.State == FmvPlaybackState.AwaitingChoice);
            player.Choose(0);
            var previous = player.Assets;
            player.Restart(); player.Restart();
            yield return Until(() => player.State == FmvPlaybackState.AwaitingChoice);
            Assert.That(player.CurrentNode.id, Is.EqualTo("wake"));
            Assert.That(previous.ActiveHandleCount, Is.Zero);
            Assert.That(player.Assets.ActiveHandleCount, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator ConcurrentLoads_OneHandle_RemainsUntilFinalLease()
        {
            yield return Until(() => player.State == FmvPlaybackState.AwaitingChoice);
            string key = FmvContentIds.Video(player.CurrentNode.video.AssetGUID);
            using var manager = new FmvAddressableManager();
            var first = manager.LoadAsync<VideoClip>(key).AsTask();
            var second = manager.LoadAsync<VideoClip>(key).AsTask();
            yield return Until(() => first.IsCompleted && second.IsCompleted);
            Assert.That(first.IsCompletedSuccessfully && second.IsCompletedSuccessfully, Is.True);
            Assert.That(first.Result.Asset, Is.SameAs(second.Result.Asset));
            Assert.That(manager.ActiveHandleCount, Is.EqualTo(1));
            first.Result.Dispose();
            Assert.That(manager.Get<VideoClip>(key), Is.Not.Null);
            second.Result.Dispose(); second.Result.Dispose();
            Assert.That(manager.ActiveHandleCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator LabelLoads_ReleaseIndependently()
        {
            yield return Until(() => player.State == FmvPlaybackState.AwaitingChoice);
            using var manager = new FmvAddressableManager();
            var first = manager.LoadAddressableData(FmvContentIds.Label).AsTask();
            var second = manager.LoadAddressableData(FmvContentIds.Label).AsTask();
            yield return Until(() => first.IsCompleted && second.IsCompleted);
            Assert.That(first.IsCompletedSuccessfully && second.IsCompletedSuccessfully, Is.True);
            Assert.That(manager.ActiveHandleCount, Is.EqualTo(6));
            Assert.That(manager.Release(FmvContentIds.Label), Is.True);
            Assert.That(manager.ActiveHandleCount, Is.EqualTo(6));
            Assert.That(manager.Release(FmvContentIds.Label), Is.True);
            Assert.That(manager.ActiveHandleCount, Is.Zero);
            Assert.That(manager.Release(FmvContentIds.Label), Is.False);
        }

        [UnityTest]
        public IEnumerator MissingVideo_ShowsErrorAndRestartRecovers()
        {
            yield return Until(() => player.State == FmvPlaybackState.AwaitingChoice);
            var definition = ScriptableObject.CreateInstance<FmvSequenceDefinition>();
            definition.nodes.Add(new FmvNode { id = "wake", video = new VideoReference("11111111111111111111111111111111") });
            LogAssert.ignoreFailingMessages = true; // InvalidKeyException is an intentional provider failure.
            player.StartSequence(definition);
            yield return Until(() => player.State == FmvPlaybackState.Error);
            Assert.That(player.ErrorMessage, Is.Not.Empty);
            Assert.That(player.Assets.ActiveHandleCount, Is.Zero);
            player.Restart();
            yield return Until(() => player.State == FmvPlaybackState.AwaitingChoice);
            UnityEngine.Object.Destroy(definition);
        }

        [UnityTest]
        public IEnumerator DecoderError_UsesProductionErrorHandler()
        {
            yield return Until(() => player.State == FmvPlaybackState.Playing);
            LogAssert.ignoreFailingMessages = true;
            player.Video.Stop();
            player.Video.source = VideoSource.Url;
            string path = System.IO.Path.Combine(Application.temporaryCachePath, "fmv-intentionally-corrupt.mp4");
            System.IO.File.WriteAllText(path, "This is deliberately not a video bitstream.");
            try
            {
                player.Video.url = path;
                player.Video.Play();
                yield return Until(() => player.State == FmvPlaybackState.Error);
                Assert.That(player.ErrorMessage, Is.Not.Empty);
                Assert.That(player.Video.clip, Is.Null);
            }
            finally { System.IO.File.Delete(path); }
        }

        [UnityTest]
        public IEnumerator TerminalNode_ReleasesAllHandlesAndKeepsDisplayedFrame()
        {
            yield return Until(() => player.State == FmvPlaybackState.AwaitingChoice);
            var definition = ScriptableObject.CreateInstance<FmvSequenceDefinition>();
            definition.nodes.Add(new FmvNode { id = "wake", video = new VideoReference(player.CurrentNode.video.AssetGUID) });
            player.StartSequence(definition);
            yield return Until(() => player.State == FmvPlaybackState.Playing);
            yield return Until(() => player.State == FmvPlaybackState.Completed);
            Assert.That(player.Assets.ActiveHandleCount, Is.Zero);
            Assert.That(player.Video.clip, Is.Null);
            Assert.That(player.DisplayTexture.IsCreated(), Is.True);
            UnityEngine.Object.Destroy(definition);
        }

        private sealed class PendingOperation : AsyncOperationBase<UnityEngine.Object>
        {
            protected override void Execute() { }
            public void Finish() => Complete(null, true, "");
        }

        [UnityTest]
        public IEnumerator PreparationTimeout_ShowsErrorAndRetryRecovers()
        {
            yield return Until(() => player.State == FmvPlaybackState.AwaitingChoice);
            var original = player.settings;
            var shortTimeout = UnityEngine.Object.Instantiate(original);
            shortTimeout.prepareTimeoutSeconds = 0.000001f;
            player.settings = shortTimeout;
            player.Restart();
            yield return Until(() => player.State == FmvPlaybackState.Error);
            Assert.That(player.ErrorMessage, Does.Contain("준비 제한"));
            shortTimeout.prepareTimeoutSeconds = 30;
            player.Retry();
            yield return Until(() => player.State == FmvPlaybackState.AwaitingChoice);
            player.settings = original;
            UnityEngine.Object.Destroy(shortTimeout);
        }

        [UnityTest]
        public IEnumerator LoadingTimeoutAndCancellation_AreObservable()
        {
            var pending = new PendingOperation();
            var handle = Addressables.ResourceManager.StartOperation(pending, default);
            var timed = FmvAsync.Wait(handle, CancellationToken.None, 0.02f).AsTask();
            yield return Until(() => timed.IsCompleted);
            Assert.That(timed.Exception?.GetBaseException(), Is.TypeOf<TimeoutException>());
            using var cancel = new CancellationTokenSource();
            var canceled = FmvAsync.Wait(handle, cancel.Token, 30).AsTask(); cancel.Cancel();
            yield return Until(() => canceled.IsCompleted);
            pending.Finish(); Addressables.Release(handle);
            Assert.That(canceled.IsCanceled || canceled.Exception?.GetBaseException() is OperationCanceledException, Is.True,
                "Cancellation result: " + canceled.Status + " / " + canceled.Exception);
        }
    }
}
