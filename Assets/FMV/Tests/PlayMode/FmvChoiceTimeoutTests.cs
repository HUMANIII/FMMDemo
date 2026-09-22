using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FmvDemo.Tests
{
    public sealed class FmvChoiceTimeoutTests
    {
        private FmvPlayer player;
        private FmvSequenceDefinition fixture;
        private FmvRuntimeSettings originalSettings;
        private FmvRuntimeSettings settings;
        private readonly List<string> entered = new();
        [UnitySetUp] public IEnumerator Setup()
        {
            yield return SceneManager.LoadSceneAsync("FmvDemo");
            player = UnityEngine.Object.FindFirstObjectByType<FmvPlayer>();
            yield return Until(() => player.State == FmvPlaybackState.AwaitingChoice);
            originalSettings = player.settings;
            settings = UnityEngine.Object.Instantiate(originalSettings); settings.prepareTimeoutSeconds = 30; player.settings = settings;
            string guid = player.CurrentNode.video.AssetGUID;
            fixture = ScriptableObject.CreateInstance<FmvSequenceDefinition>();
            fixture.nodes = new List<FmvNode>
            {
                new() { id = "wake", video = new VideoReference(guid), timeoutMode = FmvTimeoutMode.Branch, choiceTimeoutSeconds = 0.4f, timeoutTargetNodeId = "idle",
                    choices = new List<FmvChoice> { new() {text="하나",targetNodeId="one"}, new() {text="둘",targetNodeId="two"}, new() {text="셋",targetNodeId="three"} } },
                new() { id="one", video=new VideoReference(guid) }, new() {id="two",video=new VideoReference(guid)},
                new() { id="three",video=new VideoReference(guid)}, new() {id="idle",video=new VideoReference(guid)}
            };
            entered.Clear(); player.Changed += Record;
        }
        private void Record() { if (player.State == FmvPlaybackState.Loading && player.CurrentNode != null) entered.Add(player.CurrentNode.id); }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1;
            if (player != null)
            {
                player.Changed -= Record; player.StopSequence(); player.settings = originalSettings;
                yield return null; Assert.That(player.Assets.ActiveHandleCount, Is.Zero);
            }
            if (fixture != null) UnityEngine.Object.Destroy(fixture);
            if (settings != null) UnityEngine.Object.Destroy(settings);
        }
        private IEnumerator Until(Func<bool> condition, float seconds = 35)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition()) { Assert.That(Time.realtimeSinceStartup, Is.LessThan(end), player.State + ": " + player.ErrorMessage); yield return null; }
        }
        private IEnumerator Start()
        {
            player.StartSequence(fixture);
            yield return Until(() => player.State == FmvPlaybackState.Playing);
            Assert.That(player.IsChoiceTimed, Is.False); Assert.That(player.ChoiceTimeRemaining, Is.Zero);
            yield return Until(() => player.State == FmvPlaybackState.AwaitingChoice);
        }
        [UnityTest] public IEnumerator SeparateBranch_ExpiresOnceAfterVideo_UsesUnscaledTime()
        {
            yield return Start();
            Assert.That(player.ChoiceTimeRemaining, Is.GreaterThan(0.2f));
            Time.timeScale = 0;
            yield return Until(() => player.CurrentNode.id == "idle", 3);
            Assert.That(player.Choose(0), Is.False);
            Assert.That(entered.Count(id => id == "idle"), Is.EqualTo(1));
            yield return Until(() => player.State == FmvPlaybackState.Completed);
            Assert.That(player.Assets.ActiveHandleCount, Is.Zero);
        }
        [UnityTest] public IEnumerator ExistingChoice_StillFollowsSameItemAfterReorder()
        {
            var node = fixture.FindNode("wake"); node.timeoutMode = FmvTimeoutMode.Choice; node.timeoutTargetNodeId = "";
            var choice = node.choices[2]; choice.isTimeoutDefault = true; node.choices.RemoveAt(2); node.choices.Insert(0, choice);
            yield return Start();
            yield return Until(() => player.CurrentNode.id == "three", 3);
            Assert.That(entered.Count(id => id == "three"), Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator ThirdButton_ManualClickCancelsTimeoutAndRejectsDuplicate()
        {
            yield return Start();
            var view = UnityEngine.Object.FindFirstObjectByType<FmvView>();
            Assert.That(view.choiceButtons.Length, Is.GreaterThanOrEqualTo(3));
            ExecuteEvents.Execute(view.choiceButtons[2].gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
            Assert.That(player.CurrentNode.id, Is.EqualTo("three")); Assert.That(player.Choose(1), Is.False);
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.That(entered, Does.Not.Contain("idle")); Assert.That(player.IsChoiceTimed, Is.False);
        }
        [UnityTest] public IEnumerator DeadlineClickAndExpiry_CommitOnlyOneResult()
        {
            yield return Start();
            yield return new WaitForSecondsRealtime(0.4f);
            player.Choose(0);
            yield return null;
            Assert.That(entered.Count(id => id != "wake"), Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator RestartAndStop_CancelTimer_NoLateBranch()
        {
            yield return Start();
            player.Restart();
            yield return new WaitForSecondsRealtime(0.7f);
            Assert.That(entered, Does.Not.Contain("idle"));
            player.StopSequence();
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.That(player.State, Is.EqualTo(FmvPlaybackState.Stopped)); Assert.That(player.ChoiceTimeRemaining, Is.Zero);
        }
        [UnityTest] public IEnumerator DisabledTimer_WaitsIndefinitely_AndManyChoicesScroll()
        {
            var node = fixture.FindNode("wake"); node.timeoutMode = FmvTimeoutMode.Disabled;
            for (int i = 0; i < 8; i++) node.choices.Add(new FmvChoice { text = "추가 " + i, targetNodeId = "one" });
            yield return Start();
            yield return new WaitForSecondsRealtime(0.7f);
            var view = UnityEngine.Object.FindFirstObjectByType<FmvView>();
            Assert.That(player.State, Is.EqualTo(FmvPlaybackState.AwaitingChoice)); Assert.That(player.IsChoiceTimed, Is.False);
            Assert.That(view.choiceButtons.Count(b => b.gameObject.activeSelf), Is.EqualTo(11));
            Assert.That(view.choiceContent.rect.height, Is.GreaterThan(view.choiceScroll.viewport.rect.height));
        }
        [UnityTest] public IEnumerator ReturningToSameNode_ResetsFullDuration()
        {
            fixture.FindNode("idle").nextNodeId = "wake";
            yield return Start();
            yield return Until(() => player.CurrentNode.id == "idle");
            yield return Until(() => player.State == FmvPlaybackState.AwaitingChoice);
            Assert.That(player.ChoiceTimeRemaining, Is.GreaterThan(0.2f));
        }
        [UnityTest] public IEnumerator ErrorAndRetry_ClearThenRestartTimer()
        {
            yield return Start();
            settings.prepareTimeoutSeconds = 0.000001f;
            player.Choose(0);
            yield return Until(() => player.State == FmvPlaybackState.Error);
            Assert.That(player.ChoiceTimeRemaining, Is.Zero); Assert.That(player.IsChoiceTimed, Is.False);
            settings.prepareTimeoutSeconds = 30; player.Retry();
            yield return Until(() => player.State == FmvPlaybackState.Completed);
            Assert.That(entered, Does.Not.Contain("idle"));
        }
    }
}
