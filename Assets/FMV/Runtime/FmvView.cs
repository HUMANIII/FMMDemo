using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

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
        public RectTransform choiceContent;
        public ScrollRect choiceScroll;
        public GameObject timerRoot;
        public TMP_Text timerLabel;
        public Image timerFill;
        private readonly List<Button> buttons = new();
        private readonly List<TMP_Text> labels = new();
        private readonly List<UnityEngine.Events.UnityAction> clickHandlers = new();
        private FmvNode displayedNode;

        private void Start()
        {
            screen.texture = player.DisplayTexture;
            foreach (var button in choiceButtons) AddButton(button);
            retryButton.onClick.AddListener(player.Retry);
            restartButton.onClick.AddListener(player.Restart);
            player.Changed += Refresh;
            Refresh();
        }
        private void AddButton(Button button)
        {
            int index = buttons.Count;
            UnityEngine.Events.UnityAction click = () => player.Choose(index);
            buttons.Add(button); labels.Add(button.GetComponentInChildren<TMP_Text>(true));
            clickHandlers.Add(click); button.onClick.AddListener(click);
        }
        private void OnDestroy()
        {
            if (player != null) player.Changed -= Refresh;
            for (int i = 0; i < buttons.Count; i++) if (buttons[i] != null) buttons[i].onClick.RemoveListener(clickHandlers[i]);
        }
        private void Update()
        {
            if (player.Video != null && player.Video.length > 0)
            {
                float progress = Mathf.Clamp01((float)(player.Video.time / player.Video.length));
                // A sprite-free uGUI Image ignores fillAmount, so size the solid rectangle instead.
                progressFill.rectTransform.anchorMax = new Vector2(progress, 1);
            }
            if (timerRoot != null)
            {
                timerRoot.SetActive(player.IsChoiceTimed);
                if (player.IsChoiceTimed)
                {
                    timerLabel.text = $"선택까지 {player.ChoiceTimeRemaining:0.0}초";
                    timerFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(player.ChoiceTimeRemaining / player.CurrentNode.choiceTimeoutSeconds), 1);
                }
            }
            if (choiceContent != null && choiceScroll != null)
            {
                var grid = choiceContent.GetComponent<GridLayoutGroup>();
                float width = Mathf.Max(1, (choiceScroll.viewport.rect.width - grid.spacing.x) / 2);
                grid.cellSize = new Vector2(width, 76);
                int count = player.CurrentNode?.choices?.Count ?? 0;
                choiceContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Ceil(count / 2f) * (76 + grid.spacing.y));
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
            int count = player.CurrentNode?.choices?.Count ?? 0;
            while (buttons.Count < count && buttons.Count > 0)
            {
                var button = Instantiate(buttons[0], choiceContent != null ? choiceContent : buttons[0].transform.parent);
                button.name = "Choice " + buttons.Count;
                button.onClick = new Button.ButtonClickedEvent();
                AddButton(button);
            }
            choiceButtons = buttons.ToArray(); choiceTexts = labels.ToArray();
            if (choiceContent != null)
            {
                var panel = (RectTransform)choicePanel.transform;
                panel.anchorMax = new Vector2(panel.anchorMax.x, count > 2 ? 0.46f : 0.32f);
                if (displayedNode != player.CurrentNode && choiceScroll != null) choiceScroll.verticalNormalizedPosition = 1;
            }
            displayedNode = player.CurrentNode;
            for (int i = 0; i < buttons.Count; i++)
            {
                bool visible = i < count;
                buttons[i].gameObject.SetActive(visible);
                if (visible)
                {
                    var choice = player.CurrentNode.choices[i];
                    bool automatic = player.CurrentNode.timeoutMode == FmvTimeoutMode.Choice && choice.isTimeoutDefault;
                    labels[i].text = choice.text + (automatic ? "\n<size=65%>시간 초과 시 자동 선택</size>" : "");
                    buttons[i].interactable = player.State == FmvPlaybackState.AwaitingChoice;
                }
            }
        }
    }
}
