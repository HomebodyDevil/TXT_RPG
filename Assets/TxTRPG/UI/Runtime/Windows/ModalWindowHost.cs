using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TxTRPG.UI.Windows
{
    public sealed class ModalWindowHost : MonoBehaviour, ICancelHandler,
        IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text title;
        [SerializeField] private GameObject loadingState;
        [SerializeField] private TMP_Text errorText;
        [SerializeField] private GameObject errorStateRoot;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private ModalContentContainer contentContainer;
        [SerializeField] private RectTransform windowRect;
        private readonly Dictionary<int, PointerEventData> outsidePresses = new();
        private readonly Dictionary<int, PointerEventData> outsideReleases = new();
        public event Action CloseRequested;
        public event Action RetryRequested;
        public bool IsVisible => isActiveAndEnabled && canvasGroup != null && canvasGroup.interactable;
        public Button CloseButton => closeButton;
        public ModalContentContainer ContentContainer => contentContainer;
        public RectTransform WindowRect => windowRect;
        private void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(RequestClose);
            if (retryButton != null) retryButton.onClick.AddListener(RequestRetry);
        }
        private void OnDisable() => ClearPointers();
        private void OnApplicationFocus(bool focused) { if (!focused) ClearPointers(); }
        private void OnDestroy()
        {
            if (closeButton != null) closeButton.onClick.RemoveListener(RequestClose);
            if (retryButton != null) retryButton.onClick.RemoveListener(RequestRetry);
        }
        public void SetVisible(bool visible)
        {
            // Also called for replacement/retry requests: old presses cannot close a new request.
            ClearPointers();
            gameObject.SetActive(true);
            if (visible) transform.SetAsLastSibling();
            canvasGroup.alpha = visible ? 1f : 0f; canvasGroup.interactable = visible; canvasGroup.blocksRaycasts = visible;
        }
        private void ClearPointers() { outsidePresses.Clear(); outsideReleases.Clear(); }
        private bool IsOutsideBackground(PointerEventData data) =>
            IsVisible && windowRect != null && data.button == PointerEventData.InputButton.Left &&
            data.pointerCurrentRaycast.gameObject == gameObject &&
            !RectTransformUtility.RectangleContainsScreenPoint(windowRect, data.position, data.pressEventCamera);

        public void OnPointerDown(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left) return;
            outsidePresses.Remove(data.pointerId);
            outsideReleases.Remove(data.pointerId);
            // Child graphics (including owned overlay controls outside Window) never own backdrop clicks.
            if (IsOutsideBackground(data)) outsidePresses[data.pointerId] = data;
        }
        public void OnPointerUp(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left) return;
            var pressed = outsidePresses.TryGetValue(data.pointerId, out var original) && ReferenceEquals(original, data);
            outsidePresses.Remove(data.pointerId);
            outsideReleases.Remove(data.pointerId);
            if (pressed && data.eligibleForClick && !data.dragging && !IsCanceledTouch(data) && IsOutsideBackground(data))
                outsideReleases[data.pointerId] = data;
        }
        public void OnPointerClick(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left) return;
            var released = outsideReleases.TryGetValue(data.pointerId, out var original) && ReferenceEquals(original, data);
            outsideReleases.Remove(data.pointerId);
            if (!released || !data.eligibleForClick || data.dragging || !IsOutsideBackground(data)) return;
            data.Use();
            ClearPointers();
            RequestClose();
        }
        public void OnBeginDrag(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left) return;
            outsidePresses.Remove(data.pointerId);
            outsideReleases.Remove(data.pointerId);
        }
        public void OnDrag(PointerEventData data) { }
        public void OnEndDrag(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left) return;
            outsidePresses.Remove(data.pointerId);
            outsideReleases.Remove(data.pointerId);
        }
        private static bool IsCanceledTouch(PointerEventData data)
        {
            // Event-local control state, never global device polling. InputSystem can report a canceled touch as a release.
            if (data is not ExtendedPointerEventData extended) return false;
            for (var control = extended.control; control != null; control = control.parent)
                if (control is TouchControl touch)
                    return touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Canceled;
            return false;
        }
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
            ClearPointers();
            if (!IsVisible) return;
            eventData?.Use();
            RequestClose();
        }
#if UNITY_EDITOR
        public void ConfigureOutsideCloseForEditor(RectTransform window) => windowRect = window;
        public void ConfigureForEditor(CanvasGroup group, TMP_Text titleText, GameObject loading, TMP_Text error, Button close,
            ModalContentContainer container = null, GameObject errorRoot = null, Button retry = null)
        { canvasGroup = group; title = titleText; loadingState = loading; errorText = error; closeButton = close; contentContainer = container;
          errorStateRoot = errorRoot; retryButton = retry; }
#endif
    }
}
