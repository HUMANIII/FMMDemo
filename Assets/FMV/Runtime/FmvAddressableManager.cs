using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace FmvDemo
{
    public sealed class FmvAssetLease<T> : IDisposable where T : Object
    {
        private Action release;
        public T Asset { get; }
        internal FmvAssetLease(T asset, Action release) { Asset = asset; this.release = release; }
        public void Dispose() { var action = release; release = null; action?.Invoke(); }
    }

    /// <summary>Main-thread, per-session ownership. Every successful load has one matching lease or label Release.</summary>
    public sealed class FmvAddressableManager : IDisposable
    {
        private sealed class Entry
        {
            public AsyncOperationHandle handle;
            public int owners;
            public bool released;
        }
        private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Stack<List<IDisposable>>> labels = new(StringComparer.Ordinal);
        private readonly CancellationTokenSource lifetime = new();
        private bool disposed;
        public int ActiveHandleCount => entries.Count;
        public int OwnerCount { get { int total = 0; foreach (var entry in entries.Values) total += entry.owners; return total; } }

        public async UniTask<FmvAssetLease<T>> LoadAsync<T>(string address, CancellationToken token = default, float timeout = 30) where T : Object
        {
            if (disposed) throw new ObjectDisposedException(nameof(FmvAddressableManager));
            token.ThrowIfCancellationRequested();
            if (!entries.TryGetValue(address, out var entry))
            {
                entry = new Entry { handle = Addressables.LoadAssetAsync<T>(address) };
                entries.Add(address, entry);
            }
            entry.owners++;
            try
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
                await FmvAsync.Wait(entry.handle, linked.Token, timeout);
                if (entry.handle.Result is not T asset) throw new InvalidOperationException($"에셋 형식 불일치: {address}, {typeof(T).Name}");
                return new FmvAssetLease<T>(asset, () => ReleaseEntry(address, entry));
            }
            catch { ReleaseEntry(address, entry); throw; }
        }

        public T Get<T>(string address) where T : Object => entries.TryGetValue(address, out var entry) && !entry.released &&
            entry.handle.IsDone && entry.handle.Status == AsyncOperationStatus.Succeeded ? entry.handle.Result as T : null;

        public async UniTask LoadAddressableData(string label, CancellationToken token = default, float timeout = 30)
        {
            if (disposed) throw new ObjectDisposedException(nameof(FmvAddressableManager));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
            var locations = Addressables.LoadResourceLocationsAsync(label);
            var owned = new List<IDisposable>();
            try
            {
                await FmvAsync.Wait(locations, linked.Token, timeout);
                if (locations.Result.Count == 0) throw new InvalidOperationException($"비어 있거나 존재하지 않는 라벨: {label}");
                var seen = new HashSet<string>();
                foreach (var location in locations.Result)
                    if (seen.Add(location.PrimaryKey)) owned.Add(await LoadAsync<Object>(location.PrimaryKey, linked.Token, timeout));
                linked.Token.ThrowIfCancellationRequested();
                if (!labels.TryGetValue(label, out var stack)) labels.Add(label, stack = new Stack<List<IDisposable>>());
                stack.Push(owned);
            }
            catch { foreach (var lease in owned) lease.Dispose(); throw; }
            finally { if (locations.IsValid()) Addressables.Release(locations); }
        }

        public bool Release(string label)
        {
            if (!labels.TryGetValue(label, out var stack) || stack.Count == 0) return false;
            foreach (var lease in stack.Pop()) lease.Dispose();
            if (stack.Count == 0) labels.Remove(label);
            return true;
        }

        private void ReleaseEntry(string address, Entry entry)
        {
            if (entry.released || --entry.owners > 0) return;
            entry.released = true;
            if (entries.TryGetValue(address, out var current) && ReferenceEquals(entry, current)) entries.Remove(address);
            if (entry.handle.IsValid()) Addressables.Release(entry.handle);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            lifetime.Cancel();
            foreach (var entry in new List<Entry>(entries.Values))
            {
                if (entry.released) continue;
                entry.released = true;
                if (entry.handle.IsValid()) Addressables.Release(entry.handle);
            }
            entries.Clear();
            labels.Clear();
            lifetime.Dispose();
        }
    }

    public static class FmvAsync
    {
        public static async UniTask Wait(AsyncOperationHandle handle, CancellationToken token, float seconds,
            Action<float> progress = null)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + seconds;
            while (!handle.IsDone)
            {
                token.ThrowIfCancellationRequested();
                if (Time.realtimeSinceStartupAsDouble >= deadline) throw new TimeoutException("로드 제한 시간을 초과했습니다.");
                progress?.Invoke(handle.GetDownloadStatus().Percent);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            token.ThrowIfCancellationRequested();
            if (handle.Status != AsyncOperationStatus.Succeeded) throw handle.OperationException ?? new InvalidOperationException("Addressables 작업 실패");
            progress?.Invoke(1);
        }
    }
}
