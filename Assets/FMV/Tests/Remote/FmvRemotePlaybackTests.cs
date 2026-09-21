using System;
using System.Collections;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FmvDemo.Tests
{
    public sealed class FmvRemotePlaybackTests
    {
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator OldLocalCatalog_UpdatesToRemoteV2_DownloadsPlaysAndClearsCache()
        {
            if (!File.Exists(".tools/remote-verification/ready.json") || !File.Exists(".tools/remote-verification/original.json"))
                Assert.Ignore("Run the loopback integration preparation first.");
            yield return SceneManager.LoadSceneAsync("FmvDemo");
            var player = UnityEngine.Object.FindFirstObjectByType<FmvPlayer>();
            double deadline = Time.realtimeSinceStartupAsDouble + 60;
            try
            {
                while (player.State != FmvPlaybackState.AwaitingChoice)
                {
                    Assert.That(player.State, Is.Not.EqualTo(FmvPlaybackState.Error), player.ErrorMessage);
                    Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline)); yield return null;
                }
                Assert.That(player.CurrentNode.title, Does.EndWith("[remote v2]"));
                Assert.That(player.Video.frame, Is.GreaterThan(1));
                Assert.That(player.Assets.ActiveHandleCount, Is.EqualTo(2));
                long decodedFrame = player.Video.frame;
                var cached = Addressables.GetDownloadSizeAsync(FmvContentIds.Label);
                yield return cached; long cachedBytes = cached.Result; Addressables.Release(cached);
                Assert.That(cachedBytes, Is.Zero);
                player.StopSequence(); Assert.That(player.Assets.ActiveHandleCount, Is.Zero);
                var clear = FmvRemoteContent.ClearCache(CancellationToken.None).AsTask();
                while (!clear.IsCompleted) yield return null;
                Assert.That(clear.IsCompletedSuccessfully, Is.True, clear.Exception?.ToString());
                var size = Addressables.GetDownloadSizeAsync(FmvContentIds.Label);
                yield return size; long bytesAfterClear = size.Result; Addressables.Release(size);
                Assert.That(bytesAfterClear, Is.GreaterThan(0));
                player.Restart(); deadline = Time.realtimeSinceStartupAsDouble + 60;
                while (player.State != FmvPlaybackState.AwaitingChoice)
                {
                    Assert.That(player.State, Is.Not.EqualTo(FmvPlaybackState.Error), player.ErrorMessage);
                    Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline)); yield return null;
                }
                Assert.That(player.CurrentNode.title, Does.EndWith("[remote v2]"));
                Assert.That(player.Video.frame, Is.GreaterThan(1));
                Directory.CreateDirectory("Docs/Evidence");
                File.WriteAllText("Docs/Evidence/remote-playback.json", "{\"localCatalog\":\"v1\",\"loadedTitle\":\"remote v2\",\"decodedFrame\":" + decodedFrame + ",\"cachedDownloadBytes\":" + cachedBytes + ",\"bytesAfterClear\":" + bytesAfterClear + ",\"redownloadAndReplay\":true}");
            }
            finally { player.StopSequence(); Assert.That(player.Assets.ActiveHandleCount, Is.Zero); }
        }
    }
}
