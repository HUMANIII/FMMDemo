using TMPro;
using UnityEngine;

namespace FmvDemo
{
    public sealed class FmvView : MonoBehaviour
    {
        public FmvPlayer player;
        public UnityEngine.UI.RawImage screen;
        public TMP_Text title;
        public TMP_Text status;
        public TMP_Text error;
        public GameObject choicePanel;
        public GameObject errorPanel;
        public UnityEngine.UI.Button[] choiceButtons;
        public TMP_Text[] choiceTexts;
        public UnityEngine.UI.Button retryButton;
        public UnityEngine.UI.Button restartButton;
        public UnityEngine.UI.Image progressFill;

        private void Start()
        {
            screen.texture = player.DisplayTexture;
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                int index = i;
                choiceButtons[i].onClick.AddListener(() => player.Choose(index));
            }
            retryButton.onClick.AddListener(player.Retry);
            restartButton.onClick.AddListener(player.Restart);
            player.Changed += Refresh;
            Refresh();
        }
        private void OnDestroy() { if (player != null) player.Changed -= Refresh; }
        private void Update()
        {
            if (player.Video != null && player.Video.length > 0)
            {
                float progress = Mathf.Clamp01((float)(player.Video.time / player.Video.length));
                // A sprite-free uGUI Image ignores fillAmount, so size the solid rectangle instead.
                progressFill.rectTransform.anchorMax = new Vector2(progress, 1);
            }
        }
        private void Refresh()
        {
            title.text = player.CurrentNode?.title ?? "작은 하루";
            choicePanel.SetActive(player.State == FmvPlaybackState.AwaitingChoice);
            errorPanel.SetActive(player.State == FmvPlaybackState.Error);
            error.text = player.ErrorMessage ?? "";
            status.text = player.State switch
            {
                FmvPlaybackState.Loading => player.LoadingMessage,
                FmvPlaybackState.Preparing => "재생 준비 중",
                FmvPlaybackState.AwaitingChoice => "이제 무엇을 할까?",
                FmvPlaybackState.Error => "영상을 재생하지 못했어요",
                _ => "작은 하루  ·  FMV 데모"
            };
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                bool visible = player.CurrentNode?.choices != null && i < player.CurrentNode.choices.Count;
                choiceButtons[i].gameObject.SetActive(visible);
                if (visible) choiceTexts[i].text = player.CurrentNode.choices[i].text;
            }
        }
    }
}
