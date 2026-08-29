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
        private const float MaximumInitialRevealFrameDelta = 0.1f;

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

        [Header("Message Separators")]
        [SerializeField] private bool showMessageSeparators = true;
        [SerializeField] private Sprite separatorSprite;
        [SerializeField] private Color separatorColor = new(1f, 1f, 1f, 0.2f);
        [SerializeField] private Image.Type separatorImageType = Image.Type.Simple;
        [SerializeField, Min(1f)] private float separatorWidth = 360f;
        [SerializeField, Min(1f)] private float separatorHeight = 1f;

        [Header("Typography")]
        [SerializeField, Min(1f)] private float speakerFontSize = 17f;
        [SerializeField, Min(1f)] private float bodyFontSize = 24f;

        [Header("Behavior")]
        [SerializeField] private bool followLatestMessage = true;
        [SerializeField, Min(0)] private int maximumRetainedMessages;

        [Header("Initial Reveal")]
        [Tooltip("Keeps messages hidden until their initial layout is ready, then gradually reveals them with their position-based opacity applied.")]
        [SerializeField] private bool revealInitialMessages = true;
        [Tooltip("Duration in unscaled seconds for the initial message reveal. This setting is used only when Reveal Initial Messages is enabled.")]
        [SerializeField, Min(0f)] private float initialRevealDuration = 0.35f;

        private readonly List<StoryMessageItem> items = new();
        private bool synchronizingScrollbar;
        private Coroutine scrollToBottomRoutine;
        private Coroutine initialRevealRoutine;
        private float initialRevealProgress = 1f;
        private float initialRevealElapsed;

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

        public bool RevealInitialMessages
        {
            get => revealInitialMessages;
            set
            {
                if (revealInitialMessages == value)
                {
                    return;
                }

                revealInitialMessages = value;
                if (!Application.isPlaying)
                {
                    return;
                }

                if (!revealInitialMessages)
                {
                    if (initialRevealRoutine != null)
                    {
                        StopCoroutine(initialRevealRoutine);
                        initialRevealRoutine = null;
                    }

                    initialRevealProgress = 1f;
                    RefreshMessageOpacity();
                    return;
                }

                initialRevealProgress = 0f;
                initialRevealElapsed = 0f;
                if (followLatestMessage)
                {
                    ScheduleScrollToBottom();
                }
                else if (isActiveAndEnabled)
                {
                    RebuildAndRefresh();
                }
            }
        }

        public float InitialRevealDuration
        {
            get => initialRevealDuration;
            set => initialRevealDuration = Mathf.Max(0f, value);
        }

        public void AddMessage(string text, string speaker = null)
        {
            AddMessage(new StoryMessage(text, speaker));
        }

        public void AddMessage(in StoryMessage message)
        {
            var item = Instantiate(messagePrefab, content);
            item.gameObject.SetActive(true);
            item.Bind(message);
            item.ConfigureTextSize(speakerFontSize, bodyFontSize);
            items.Add(item);
            TrimOldMessages();
            RefreshMessageSeparators();

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

        public static float CalculateInitialRevealProgress(float elapsed, float duration)
        {
            if (duration <= 0f)
            {
                return 1f;
            }

            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
        }

        private void Awake()
        {
            initialRevealProgress = revealInitialMessages ? 0f : 1f;
            initialRevealElapsed = 0f;
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

            if (scrollToBottomRoutine != null)
            {
                StopCoroutine(scrollToBottomRoutine);
            }

            if (initialRevealRoutine != null)
            {
                StopCoroutine(initialRevealRoutine);
            }

            scrollToBottomRoutine = null;
            initialRevealRoutine = null;
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
            separatorWidth = Mathf.Max(1f, separatorWidth);
            separatorHeight = Mathf.Max(1f, separatorHeight);
            speakerFontSize = Mathf.Max(1f, speakerFontSize);
            bodyFontSize = Mathf.Max(1f, bodyFontSize);
            initialRevealDuration = Mathf.Max(0f, initialRevealDuration);

            if (isActiveAndEnabled && scrollRect != null && scrollbar != null)
            {
                ApplyOptions();
                RefreshMessageSeparators();
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
                    fadeExponent) * initialRevealProgress);
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

        private void RefreshMessageSeparators()
        {
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null)
                {
                    continue;
                }

                item.ConfigureTextSize(speakerFontSize, bodyFontSize);
                item.ConfigureSeparator(
                    showMessageSeparators && i < items.Count - 1,
                    separatorSprite,
                    separatorColor,
                    separatorImageType,
                    separatorWidth,
                    separatorHeight);
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
            StartInitialRevealWhenReady();
        }

        private void StartInitialRevealWhenReady()
        {
            if (!revealInitialMessages || items.Count == 0 || initialRevealProgress >= 1f || initialRevealRoutine != null)
            {
                return;
            }

            initialRevealRoutine = StartCoroutine(RevealInitialMessagesOverTime());
        }

        private IEnumerator RevealInitialMessagesOverTime()
        {
            if (initialRevealDuration <= 0f)
            {
                initialRevealProgress = 1f;
                RefreshMessageOpacity();
                initialRevealRoutine = null;
                yield break;
            }

            // Render one complete frame at zero opacity before consuming any elapsed time.
            initialRevealProgress = 0f;
            RefreshMessageOpacity();
            yield return null;

            while (initialRevealElapsed < initialRevealDuration)
            {
                var frameDelta = Mathf.Min(Time.unscaledDeltaTime, MaximumInitialRevealFrameDelta);
                initialRevealElapsed = Mathf.Min(initialRevealElapsed + frameDelta, initialRevealDuration);
                initialRevealProgress = CalculateInitialRevealProgress(
                    initialRevealElapsed,
                    initialRevealDuration);
                RefreshMessageOpacity();
                yield return null;
            }

            initialRevealProgress = 1f;
            RefreshMessageOpacity();
            initialRevealRoutine = null;
        }

        private void RebuildAndRefresh()
        {
            RebuildLayout();
            SynchronizeFromScrollRect(scrollRect.normalizedPosition);
            StartInitialRevealWhenReady();
        }

        private void RebuildLayout()
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            Canvas.ForceUpdateCanvases();
        }
    }
}
