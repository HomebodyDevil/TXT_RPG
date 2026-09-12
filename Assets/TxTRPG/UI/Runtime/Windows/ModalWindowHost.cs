using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TxTRPG.UI.Windows
{
    public sealed class ModalWindowHost : MonoBehaviour, ICancelHandler
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text title;
        [SerializeField] private GameObject loadingState;
        [SerializeField] private TMP_Text errorText;
        [SerializeField] private GameObject errorStateRoot;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private ModalContentContainer contentContainer;
        public event Action CloseRequested;
        public event Action RetryRequested;
        public bool IsVisible => canvasGroup != null && canvasGroup.interactable;
        public Button CloseButton => closeButton;
        public ModalContentContainer ContentContainer => contentContainer;
        private void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(RequestClose);
            if (retryButton != null) retryButton.onClick.AddListener(RequestRetry);
        }
        private void OnDestroy()
        {
            if (closeButton != null) closeButton.onClick.RemoveListener(RequestClose);
            if (retryButton != null) retryButton.onClick.RemoveListener(RequestRetry);
        }
        public void SetVisible(bool visible)
        { gameObject.SetActive(true); canvasGroup.alpha = visible ? 1f : 0f; canvasGroup.interactable = visible; canvasGroup.blocksRaycasts = visible; }
        public void SetLoading(bool loading) { if (loadingState != null) loadingState.SetActive(loading); }
        public void SetError(string value)
        {
            var visible = !string.IsNullOrEmpty(value);
            if (errorText != null) errorText.text = value ?? string.Empty;
            if (errorStateRoot != null) errorStateRoot.SetActive(visible);
            else if (errorText != null) errorText.gameObject.SetActive(visible);
        }
        public void SetTitle(string value) { if (title != null) title.text = value ?? string.Empty; }
        public void RequestClose() => CloseRequested?.Invoke();
        public void RequestRetry() => RetryRequested?.Invoke();
        public void OnCancel(BaseEventData eventData)
        {
            if (!IsVisible) return;
            eventData?.Use();
            RequestClose();
        }
#if UNITY_EDITOR
        public void ConfigureForEditor(CanvasGroup group, TMP_Text titleText, GameObject loading, TMP_Text error, Button close,
            ModalContentContainer container = null, GameObject errorRoot = null, Button retry = null)
        { canvasGroup = group; title = titleText; loadingState = loading; errorText = error; closeButton = close; contentContainer = container;
          errorStateRoot = errorRoot; retryButton = retry; }
#endif
    }
}
