using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Video;

namespace FmvDemo
{
    public enum FmvPlaybackState { Stopped, Loading, Preparing, Playing, AwaitingChoice, Completed, Error }

    [RequireComponent(typeof(VideoPlayer), typeof(AudioSource))]
    public sealed class FmvPlayer : MonoBehaviour
    {
        public FmvRuntimeSettings settings;
        public bool playOnStart = true;
        public FmvPlaybackState State { get; private set; } = FmvPlaybackState.Stopped;
        public FmvNode CurrentNode { get; private set; }
        public string ErrorMessage { get; private set; }
        public string LoadingMessage { get; private set; }
        public FmvAddressableManager Assets { get; private set; }
        public RenderTexture DisplayTexture { get; private set; }
        public VideoPlayer Video { get; private set; }
        public event Action Changed;
        public event Action<string> NodeCompleted;
        private RenderTexture decodeTexture;
        private AudioSource audioOutput;
        private FmvSequenceDefinition sequence;
        private FmvAssetLease<FmvSequenceDefinition> sequenceLease;
        private FmvAssetLease<VideoClip> videoLease;
        private CancellationTokenSource session;
        private CancellationTokenSource nodeOperation;
        private string videoError;

        private void Awake()
        {
            Video = GetComponent<VideoPlayer>();
            audioOutput = GetComponent<AudioSource>();
            audioOutput.playOnAwake = false;
            Video.playOnAwake = false;
            Video.isLooping = false;
            Video.waitForFirstFrame = true;
            Video.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
            Application.targetFrameRate = 60;
            Video.renderMode = VideoRenderMode.RenderTexture;
            Video.aspectRatio = VideoAspectRatio.FitInside;
            Video.audioOutputMode = VideoAudioOutputMode.AudioSource;
            decodeTexture = NewTexture("FMV Decode");
            DisplayTexture = NewTexture("FMV Display");
            Video.targetTexture = decodeTexture;
        }

        private static RenderTexture NewTexture(string textureName)
        {
            var texture = new RenderTexture(1280, 720, 0, RenderTextureFormat.ARGB32) { name = textureName };
            texture.Create();
            var previous = RenderTexture.active;
            RenderTexture.active = texture;
            GL.Clear(true, true, Color.black);
            RenderTexture.active = previous;
            return texture;
        }

        private void OnEnable() { Video.loopPointReached += EndReached; Video.errorReceived += VideoFailed; }
        private void Start() { if (playOnStart) StartSequence(); }
        private void LateUpdate()
        {
            if (State == FmvPlaybackState.Playing && Video.frame >= 0) Graphics.Blit(decodeTexture, DisplayTexture);
        }
        private void OnDisable()
        {
            StopSequence();
            Video.loopPointReached -= EndReached;
            Video.errorReceived -= VideoFailed;
        }
        private void OnDestroy()
        {
            Video.targetTexture = null;
            decodeTexture.Release(); DisplayTexture.Release();
            Destroy(decodeTexture); Destroy(DisplayTexture);
        }

        public void StartSequence(FmvSequenceDefinition definition = null)
        {
            StopSequence();
            Assets = new FmvAddressableManager();
            session = new CancellationTokenSource();
            LoadingMessage = "영상을 준비하고 있어요";
            SetState(FmvPlaybackState.Loading);
            StartSessionAsync(definition, Assets, session.Token).Forget();
        }

        private async UniTaskVoid StartSessionAsync(FmvSequenceDefinition definition, FmvAddressableManager manager, CancellationToken token)
        {
            FmvAssetLease<FmvSequenceDefinition> acquired = null;
            try
            {
                if (settings == null) throw new InvalidOperationException("FMV 실행 설정이 지정되지 않았습니다.");
                if (definition == null)
                {
                    if (settings.sequence == null || !settings.sequence.RuntimeKeyIsValid()) throw new InvalidOperationException("시나리오가 지정되지 않았습니다.");
                    if (settings.checkRemoteUpdates)
                    {
                        long bytes = await FmvRemoteContent.CheckForUpdates(token, settings.loadTimeoutSeconds, Progress);
                        if (bytes > 0) await FmvRemoteContent.Download(token, settings.loadTimeoutSeconds,
                            (p, message) => Progress(p, $"{message} · {bytes / 1048576f:0.00} MB"));
                    }
                    acquired = await manager.LoadAsync<FmvSequenceDefinition>(FmvContentIds.Sequence(settings.sequence.AssetGUID), token, settings.loadTimeoutSeconds);
                    definition = acquired.Asset;
                }
                token.ThrowIfCancellationRequested();
                var errors = definition.Validate();
                if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
                sequence = definition;
                sequenceLease = acquired; acquired = null;
                BeginNode(sequence.startNodeId);
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { if (!token.IsCancellationRequested) Fail(error.Message); }
            finally { acquired?.Dispose(); }
        }

        private void Progress(float progress, string message)
        {
            LoadingMessage = $"{message}  {progress:P0}";
            Changed?.Invoke();
        }

        public bool Choose(int index)
        {
            if (State != FmvPlaybackState.AwaitingChoice || CurrentNode?.choices == null || index < 0 || index >= CurrentNode.choices.Count) return false;
            BeginNode(CurrentNode.choices[index].targetNodeId);
            return true;
        }

        public void Restart() => StartSequence();
        public void Retry()
        {
            if (State != FmvPlaybackState.Error) return;
            if (sequence != null && CurrentNode != null) BeginNode(CurrentNode.id);
            else StartSequence();
        }

        private void BeginNode(string id)
        {
            CancelNode();
            CurrentNode = sequence.FindNode(id);
            if (CurrentNode == null) { Fail($"노드를 찾을 수 없습니다: {id}"); return; }
            ErrorMessage = null;
            videoError = null;
            LoadingMessage = "영상을 불러오고 있어요";
            nodeOperation = CancellationTokenSource.CreateLinkedTokenSource(session.Token);
            SetState(FmvPlaybackState.Loading);
            PlayNodeAsync(CurrentNode, Assets, nodeOperation.Token).Forget();
        }

        private async UniTaskVoid PlayNodeAsync(FmvNode node, FmvAddressableManager manager, CancellationToken token)
        {
            FmvAssetLease<VideoClip> acquired = null;
            try
            {
                acquired = await manager.LoadAsync<VideoClip>(FmvContentIds.Video(node.video.AssetGUID), token, settings.loadTimeoutSeconds);
                token.ThrowIfCancellationRequested();
                videoLease = acquired; acquired = null;
                Video.source = VideoSource.VideoClip;
                Video.clip = videoLease.Asset;
                Video.controlledAudioTrackCount = (ushort)Math.Min(1, (int)Video.clip.audioTrackCount);
                if (Video.controlledAudioTrackCount > 0)
                {
                    Video.EnableAudioTrack(0, true);
                    Video.SetTargetAudioSource(0, audioOutput);
                }
                SetState(FmvPlaybackState.Preparing);
                Video.Prepare();
                double deadline = Time.realtimeSinceStartupAsDouble + settings.prepareTimeoutSeconds;
                while (!Video.isPrepared)
                {
                    token.ThrowIfCancellationRequested();
                    if (!string.IsNullOrEmpty(videoError)) throw new InvalidOperationException(videoError);
                    if (Time.realtimeSinceStartupAsDouble >= deadline) throw new TimeoutException("영상 준비 제한 시간을 초과했습니다.");
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
                token.ThrowIfCancellationRequested();
                if (!string.IsNullOrEmpty(videoError)) throw new InvalidOperationException(videoError);
                Video.Play();
                SetState(FmvPlaybackState.Playing);
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { if (!token.IsCancellationRequested) Fail(error.Message); }
            finally { acquired?.Dispose(); }
        }

        private void EndReached(VideoPlayer source)
        {
            if (State != FmvPlaybackState.Playing) return;
            Graphics.Blit(decodeTexture, DisplayTexture);
            var completed = CurrentNode;
            Video.Pause();
            bool choices = completed.choices is { Count: > 0 };
            bool automatic = !choices && !string.IsNullOrEmpty(completed.nextNodeId);
            var token = session.Token;
            if (!choices && !automatic) ReleaseSession();
            SetState(choices ? FmvPlaybackState.AwaitingChoice : FmvPlaybackState.Completed);
            NodeCompleted?.Invoke(completed.id);
            if (automatic) AdvanceAfterCallback(completed, token).Forget();
        }

        private async UniTaskVoid AdvanceAfterCallback(FmvNode completed, CancellationToken token)
        {
            try
            {
                // Let the native decoder finish its end callback before stopping/unloading its clip.
                await UniTask.NextFrame(PlayerLoopTiming.Update, token);
                if (State == FmvPlaybackState.Completed && ReferenceEquals(CurrentNode, completed)) BeginNode(completed.nextNodeId);
            }
            catch (OperationCanceledException) { }
        }

        private void VideoFailed(VideoPlayer source, string message)
        {
            videoError = message;
            if (State == FmvPlaybackState.Playing) Fail(message);
        }
        private void Fail(string message)
        {
            CancelNode();
            ErrorMessage = message;
            SetState(FmvPlaybackState.Error);
            Debug.LogWarning("[FMV] " + message, this);
        }
        private void CancelNode()
        {
            nodeOperation?.Cancel(); nodeOperation?.Dispose(); nodeOperation = null;
            if (Video != null) { Video.Stop(); Video.clip = null; }
            if (audioOutput != null) audioOutput.Stop();
            videoLease?.Dispose(); videoLease = null;
        }
        public void StopSequence()
        {
            ReleaseSession();
            CurrentNode = null; ErrorMessage = null;
            SetState(FmvPlaybackState.Stopped);
        }
        private void ReleaseSession()
        {
            session?.Cancel();
            CancelNode();
            session?.Dispose(); session = null;
            sequenceLease?.Dispose(); sequenceLease = null;
            Assets?.Dispose();
            sequence = null;
        }
        private void SetState(FmvPlaybackState state) { State = state; Changed?.Invoke(); }
    }
}
