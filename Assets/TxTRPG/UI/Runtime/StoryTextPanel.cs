using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TxTRPG.UI
{
    public enum StoryScrollbarSide
    {
        Left,
        Right
    }

    [DisallowMultipleComponent]
    public sealed class StoryTextPanel : MonoBehaviour
    {
        [Header("Message Content")]
        [SerializeField] private StoryMessageItem messagePrefab;
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform content;
        [SerializeField] private ScrollRect scrollRect;

        [Header("User Scrolling")]
        [SerializeField] private bool allowUserScrolling = true;
        [SerializeField] private StoryScrollbarSide scrollbarSide = StoryScrollbarSide.Right;
        [SerializeField] private bool showScrollbarBackground = true;
        [SerializeField, Range(0.05f, 0.5f)] private float scrollbarHandleSize = 0.18f;
        [SerializeField, Min(8f)] private float scrollbarWidth = 18f;
        [SerializeField, Min(0f)] private float scrollbarGap = 8f;
        [SerializeField] private Scrollbar scrollbar;
        [SerializeField] private Image scrollbarBackground;

        [Header("Older Message Fade")]
        [Tooltip("Opacity of a message when its center reaches the top of the visible area.")]
        [SerializeField, Range(0f, 1f)] private float oldestVisibleOpacity = 0.15f;
        [Tooltip("Fade begins at this normalized height measured from the bottom of the visible area.")]
        [SerializeField, Range(0f, 1f)] private float fadeStartFromBottom = 0.35f;
        [Tooltip("Higher values keep messages opaque longer before fading near the top.")]
        [SerializeField, Range(0.1f, 5f)] private float fadeExponent = 1.5f;

        [Header("Behavior")]
        [SerializeField] private bool followLatestMessage = true;
        [SerializeField, Min(0)] private int maximumRetainedMessages;

        private readonly List<StoryMessageItem> items = new();
        private bool synchronizingScrollbar;
        private Coroutine scrollToBottomRoutine;

        public bool AllowUserScrolling
        {
            get => allowUserScrolling;
            set
            {
                allowUserScrolling = value;
                ApplyOptions();
            }
        }

        public StoryScrollbarSide ScrollbarSide
        {
            get => scrollbarSide;
            set
            {
                scrollbarSide = value;
                ApplyOptions();
            }
        }

        public int MessageCount => items.Count;

        public void AddMessage(string text, string speaker = null)
        {
            AddMessage(new StoryMessage(text, speaker));
        }

        public void AddMessage(in StoryMessage message)
        {
            var item = Instantiate(messagePrefab, content);
            item.gameObject.SetActive(true);
            item.Bind(message);
            items.Add(item);
            TrimOldMessages();

            if (followLatestMessage)
            {
                ScheduleScrollToBottom();
            }
            else
            {
                RebuildAndRefresh();
            }
        }

        public void Clear()
        {
            foreach (var item in items)
            {
                if (item != null)
                {
                    Destroy(item.gameObject);
                }
            }

            items.Clear();
            ScheduleScrollToBottom();
        }

        public void ScrollToOldest()
        {
            RebuildLayout();
            scrollRect.verticalNormalizedPosition = 1f;
            SynchronizeFromScrollRect(Vector2.up);
        }

        public void ScrollToLatest()
        {
            RebuildLayout();
            scrollRect.verticalNormalizedPosition = 0f;
            SynchronizeFromScrollRect(Vector2.zero);
        }

        public static float CalculateOpacity(
            float normalizedHeightFromBottom,
            float fadeStart,
            float minimumOpacity,
            float exponent)
        {
            if (normalizedHeightFromBottom <= fadeStart)
            {
                return 1f;
            }

            var fadeRange = Mathf.Max(0.0001f, 1f - fadeStart);
            var progress = Mathf.Clamp01((normalizedHeightFromBottom - fadeStart) / fadeRange);
            var shapedProgress = Mathf.Pow(progress, Mathf.Max(0.1f, exponent));
            return Mathf.Lerp(1f, Mathf.Clamp01(minimumOpacity), shapedProgress);
        }

        private void Awake()
        {
            ApplyOptions();
        }

        private void OnEnable()
        {
            scrollRect.onValueChanged.AddListener(SynchronizeFromScrollRect);
            scrollbar.onValueChanged.AddListener(SynchronizeFromScrollbar);
            ApplyOptions();
            ScheduleScrollToBottom();
        }

        private void OnDisable()
        {
            scrollRect.onValueChanged.RemoveListener(SynchronizeFromScrollRect);
            scrollbar.onValueChanged.RemoveListener(SynchronizeFromScrollbar);
        }

        private void LateUpdate()
        {
            RefreshMessageOpacity();
        }

        private void OnRectTransformDimensionsChange()
        {
            if (isActiveAndEnabled)
            {
                RebuildAndRefresh();
            }
        }

        private void OnValidate()
        {
            scrollbarHandleSize = Mathf.Clamp(scrollbarHandleSize, 0.05f, 0.5f);
            fadeStartFromBottom = Mathf.Clamp01(fadeStartFromBottom);
            oldestVisibleOpacity = Mathf.Clamp01(oldestVisibleOpacity);
            fadeExponent = Mathf.Max(0.1f, fadeExponent);

            if (isActiveAndEnabled && scrollRect != null && scrollbar != null)
            {
                ApplyOptions();
            }
        }

        private void ApplyOptions()
        {
            if (scrollRect == null || scrollbar == null || viewport == null)
            {
                return;
            }

            scrollRect.vertical = allowUserScrolling;
            scrollRect.horizontal = false;
            scrollRect.verticalScrollbar = null;
            scrollRect.horizontalScrollbar = null;

            scrollbar.gameObject.SetActive(allowUserScrolling);
            scrollbar.interactable = allowUserScrolling;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.size = scrollbarHandleSize;
            if (scrollbarBackground != null)
            {
                scrollbarBackground.enabled = showScrollbarBackground;
            }

            var barRect = (RectTransform)scrollbar.transform;
            var placeOnLeft = scrollbarSide == StoryScrollbarSide.Left;
            barRect.anchorMin = new Vector2(placeOnLeft ? 0f : 1f, 0f);
            barRect.anchorMax = new Vector2(placeOnLeft ? 0f : 1f, 1f);
            barRect.pivot = new Vector2(placeOnLeft ? 0f : 1f, 0.5f);
            barRect.sizeDelta = new Vector2(scrollbarWidth, 0f);
            barRect.anchoredPosition = new Vector2(placeOnLeft ? 0f : 0f, 0f);

            var reservedSpace = allowUserScrolling ? scrollbarWidth + scrollbarGap : 0f;
            viewport.offsetMin = new Vector2(placeOnLeft ? reservedSpace : 0f, viewport.offsetMin.y);
            viewport.offsetMax = new Vector2(placeOnLeft ? 0f : -reservedSpace, viewport.offsetMax.y);
        }

        private void SynchronizeFromScrollbar(float value)
        {
            if (synchronizingScrollbar)
            {
                return;
            }

            synchronizingScrollbar = true;
            scrollRect.verticalNormalizedPosition = value;
            synchronizingScrollbar = false;
            RefreshMessageOpacity();
        }

        private void SynchronizeFromScrollRect(Vector2 position)
        {
            if (!synchronizingScrollbar)
            {
                synchronizingScrollbar = true;
                scrollbar.value = scrollRect.verticalNormalizedPosition;
                scrollbar.size = scrollbarHandleSize;
                synchronizingScrollbar = false;
            }

            RefreshMessageOpacity();
        }

        private void RefreshMessageOpacity()
        {
            if (viewport == null || items.Count == 0)
            {
                return;
            }

            var viewportRect = viewport.rect;
            foreach (var item in items)
            {
                if (item == null)
                {
                    continue;
                }

                var itemRect = (RectTransform)item.transform;
                var worldCenter = itemRect.TransformPoint(itemRect.rect.center);
                var localCenter = viewport.InverseTransformPoint(worldCenter);
                var normalizedHeight = Mathf.InverseLerp(viewportRect.yMin, viewportRect.yMax, localCenter.y);
                item.SetOpacity(CalculateOpacity(
                    normalizedHeight,
                    fadeStartFromBottom,
                    oldestVisibleOpacity,
                    fadeExponent));
            }
        }

        private void TrimOldMessages()
        {
            if (maximumRetainedMessages <= 0)
            {
                return;
            }

            while (items.Count > maximumRetainedMessages)
            {
                var oldest = items[0];
                items.RemoveAt(0);
                if (oldest != null)
                {
                    Destroy(oldest.gameObject);
                }
            }
        }

        private void ScheduleScrollToBottom()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (scrollToBottomRoutine != null)
            {
                StopCoroutine(scrollToBottomRoutine);
            }

            scrollToBottomRoutine = StartCoroutine(ScrollToBottomAfterLayout());
        }

        private IEnumerator ScrollToBottomAfterLayout()
        {
            yield return null;
            RebuildLayout();
            scrollRect.verticalNormalizedPosition = 0f;
            SynchronizeFromScrollRect(Vector2.zero);
            scrollToBottomRoutine = null;
        }

        private void RebuildAndRefresh()
        {
            RebuildLayout();
            SynchronizeFromScrollRect(scrollRect.normalizedPosition);
        }

        private void RebuildLayout()
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            Canvas.ForceUpdateCanvases();
        }
    }
}
