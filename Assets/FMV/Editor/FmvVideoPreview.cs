using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Video;

namespace FmvDemo.Editor
{
    /// <summary>Editor-only native video preview, independent of scene and Addressables playback.</summary>
    internal sealed class FmvVideoPreview : VisualElement, IDisposable
    {
        private readonly Image image;
        private readonly Label clipName;
        private readonly Label status;
        private readonly Button play;
        private readonly Button restart;
        private VideoClip clip;
        private Hash128 clipHash;
        private NativePreview native;
        private GUID previewId;
        private bool hasPreview;
        private bool disposed;
        private bool targetInitialized;
        private double prepareStarted;
        private double nextRefresh;

        public FmvVideoPreview()
        {
            name = "video-preview";
            AddToClassList("video-preview");
            Add(new Label("영상 미리보기") { name = "preview-heading" });
            clipName = new Label(); clipName.AddToClassList("preview-clip-name"); Add(clipName);
            image = new Image { name = "preview-image", scaleMode = ScaleMode.ScaleToFit };
            image.AddToClassList("preview-image"); Add(image);
            var controls = new VisualElement(); controls.AddToClassList("preview-controls"); Add(controls);
            play = new Button(TogglePlayback) { name = "preview-play", text = "재생" }; controls.Add(play);
            restart = new Button(Restart) { name = "preview-restart", text = "처음부터" }; controls.Add(restart);
            status = new Label(); status.AddToClassList("preview-status"); Add(status);
            RegisterCallback<DetachFromPanelEvent>(_ => Stop());
            EditorApplication.update += UpdatePreview;
            AssemblyReloadEvents.beforeAssemblyReload += Dispose;
            SetClip(null);
        }

        public void SetClip(VideoClip value)
        {
            var hash = value != null ? AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(value)) : default;
            if (targetInitialized && clip == value && clipHash == hash) return;
            targetInitialized = true;
            Stop();
            clip = value;
            clipHash = hash;
            status.tooltip = "";
            clipName.text = clip != null ? clip.name : "선택한 영상 없음";
            clipName.tooltip = clip != null ? AssetDatabase.GetAssetPath(clip) : "";
            play.SetEnabled(clip != null);
            restart.SetEnabled(clip != null);
            status.text = clip != null ? "재생하면 이곳에서 반복 미리보기합니다." : "영상이 지정된 노드를 선택하세요.";
        }

        private void TogglePlayback()
        {
            if (clip == null || disposed) return;
            try
            {
                native ??= new NativePreview();
                if (!hasPreview)
                {
                    previewId = native.Start(clip);
                    hasPreview = true;
                    prepareStarted = EditorApplication.timeSinceStartup;
                    status.text = "영상 준비 중…";
                }
                if (native.IsPlaying(previewId))
                {
                    native.Pause(previewId);
                    play.text = "재생";
                    status.text = "일시정지";
                }
                else
                {
                    native.Play(previewId, true);
                    play.text = "일시정지";
                }
            }
            catch (Exception error) { Fail(error); }
        }

        private void Restart()
        {
            Stop();
            TogglePlayback();
        }

        private void UpdatePreview()
        {
            if (!hasPreview || disposed) return;
            if (clip == null) { SetClip(null); return; }
            if (EditorApplication.timeSinceStartup < nextRefresh) return;
            nextRefresh = EditorApplication.timeSinceStartup + 1.0 / 30;
            try
            {
                var texture = native.Texture(previewId);
                if (texture != null)
                {
                    image.image = texture;
                    if (native.IsPlaying(previewId))
                    {
                        image.MarkDirtyRepaint();
                        status.text = "재생 중 · 반복";
                    }
                }
                else if (EditorApplication.timeSinceStartup - prepareStarted > 30)
                {
                    Fail(new InvalidOperationException("30초 안에 영상을 준비하지 못했어요. 다시 재생해 보세요."));
                }
            }
            catch (Exception error) { Fail(error); }
        }

        public void Stop()
        {
            // The texture belongs to Unity's native preview session; release the session, not the texture.
            image.image = null;
            if (hasPreview)
            {
                hasPreview = false;
                try { native.Stop(previewId); }
                catch (Exception error) { Debug.LogWarning("FMV 미리보기 정리: " + error.Message); }
                previewId = default;
            }
            play.text = "재생";
            if (clip != null) status.text = "재생하면 이곳에서 반복 미리보기합니다.";
        }

        private void Fail(Exception error)
        {
            Stop();
            status.text = "미리보기를 열지 못했어요. 재생 버튼으로 다시 시도하세요.";
            status.tooltip = error.Message;
            Debug.LogWarning("FMV 영상 미리보기: " + error.Message);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Stop();
            EditorApplication.update -= UpdatePreview;
            AssemblyReloadEvents.beforeAssemblyReload -= Dispose;
        }

        // Unity's VideoClip Inspector uses VideoUtil for Edit-mode playback. Keep this internal
        // API adapter isolated here; resolve signatures explicitly and fail visibly if they change.
        private sealed class NativePreview
        {
            private readonly Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("UnityEditor.VideoUtil")).FirstOrDefault(value => value != null);
            public readonly Func<VideoClip, GUID> Start;
            public readonly Action<GUID> Stop;
            public readonly Action<GUID, bool> Play;
            public readonly Action<GUID> Pause;
            public readonly Func<GUID, bool> IsPlaying;
            public readonly Func<GUID, Texture> Texture;

            public NativePreview()
            {
                Start = Bind<Func<VideoClip, GUID>>("StartPreview", typeof(VideoClip));
                Stop = Bind<Action<GUID>>("StopPreview", typeof(GUID));
                Play = Bind<Action<GUID, bool>>("PlayPreview", typeof(GUID), typeof(bool));
                Pause = Bind<Action<GUID>>("PausePreview", typeof(GUID));
                IsPlaying = Bind<Func<GUID, bool>>("IsPreviewPlaying", typeof(GUID));
                Texture = Bind<Func<GUID, Texture>>("GetPreviewTexture", typeof(GUID));
            }

            private T Bind<T>(string method, params Type[] arguments) where T : Delegate
            {
                var info = type?.GetMethod(method, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, arguments, null);
                if (info == null) throw new NotSupportedException("이 Unity 버전의 영상 미리보기 API를 사용할 수 없습니다: " + method);
                return (T)Delegate.CreateDelegate(typeof(T), info);
            }
        }
    }
}
