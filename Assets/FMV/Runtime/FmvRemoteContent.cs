using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace FmvDemo
{
    public static class FmvRemoteContent
    {
        // Addressables 2.8 unloads released bundles asynchronously. Its protected provider barrier
        // lets us wait for actual unload completion before asking Unity's cache to delete files.
        private sealed class UnloadBarrier : AssetBundleProvider
        {
            public static bool Pending => UnloadingBundles.Values.Any(operation => !operation.isDone);
        }
        public static async UniTask<long> CheckForUpdates(CancellationToken token, float timeout = 30, Action<float, string> progress = null)
        {
            var init = Addressables.InitializeAsync(false);
            try { await FmvAsync.Wait(init, token, timeout); }
            finally { if (init.IsValid()) Addressables.Release(init); }
            var check = Addressables.CheckForCatalogUpdates(false);
            try
            {
                await FmvAsync.Wait(check, token, timeout, p => progress?.Invoke(p, "업데이트 확인"));
                if (check.Result.Count > 0)
                {
                    var update = Addressables.UpdateCatalogs(check.Result, false);
                    try { await FmvAsync.Wait(update, token, timeout); }
                    finally { if (update.IsValid()) Addressables.Release(update); }
                }
            }
            finally { if (check.IsValid()) Addressables.Release(check); }
            var size = Addressables.GetDownloadSizeAsync(FmvContentIds.Label);
            try { await FmvAsync.Wait(size, token, timeout); return size.Result; }
            finally { if (size.IsValid()) Addressables.Release(size); }
        }

        public static async UniTask Download(CancellationToken token, float timeout = 30, Action<float, string> progress = null)
        {
            var download = Addressables.DownloadDependenciesAsync(FmvContentIds.Label, false);
            try { await FmvAsync.Wait(download, token, timeout, p => progress?.Invoke(p, "영상 다운로드")); }
            finally { if (download.IsValid()) Addressables.Release(download); }
        }

        public static async UniTask ClearCache(CancellationToken token, float timeout = 30)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + timeout;
            await UniTask.NextFrame(PlayerLoopTiming.Update, token);
            while (UnloadBarrier.Pending)
            {
                token.ThrowIfCancellationRequested();
                if (Time.realtimeSinceStartupAsDouble >= deadline) throw new TimeoutException("번들 해제 대기 제한 시간을 초과했습니다.");
                await UniTask.NextFrame(PlayerLoopTiming.Update, token);
            }
            var clear = Addressables.ClearDependencyCacheAsync(FmvContentIds.Label, false);
            try { await FmvAsync.Wait(clear, token, timeout); if (!clear.Result) throw new InvalidOperationException("FMV 캐시 삭제 실패"); }
            finally { if (clear.IsValid()) Addressables.Release(clear); }
        }
    }
}
