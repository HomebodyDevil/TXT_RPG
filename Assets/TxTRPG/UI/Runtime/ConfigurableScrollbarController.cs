using System;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI
{
    [DisallowMultipleComponent]
    public sealed class ConfigurableScrollbarController : MonoBehaviour
    {
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform viewport;
        [SerializeField] private Scrollbar scrollbar;
        [SerializeField] private RectTransform oppositeScrollbarArea;
        [SerializeField] private Image background;
        [SerializeField] private Image handle;
        [SerializeField] private ScrollbarStyle style;
        [SerializeField] private ScrollbarVisibilityMode visibility = ScrollbarVisibilityMode.Auto;
        [SerializeField] private ScrollbarSpaceMode spaceMode = ScrollbarSpaceMode.ReserveAlways;
        [SerializeField] private ScrollbarSide side = ScrollbarSide.Right;
        [SerializeField] private ScrollbarHandleSizeMode handleSizeMode = ScrollbarHandleSizeMode.MinimumPixels;
        [SerializeField, Min(1f)] private float width = 16f;
        [SerializeField, Min(0f)] private float gap = 8f;
        [SerializeField, Range(0.01f, 1f)] private float fixedNormalizedSize = 0.18f;
        [SerializeField, Min(1f)] private float handlePixels = 36f;
        private bool synchronizing;
        private bool refreshing;
        private bool isVisible;
        private float reservedInset = -1f;
        private float lastContentHeight = float.NaN;
        private Vector2 lastViewportSize = new(float.NaN, float.NaN);
        private bool missingOppositeAreaWarningLogged;

        public event Action ViewportLayoutChanged;

        public ScrollbarVisibilityMode Visibility
        {
            get => visibility;
            set
            {
                if (visibility == value) return;
                visibility = value;
                Refresh();
            }
        }

        public ScrollbarSpaceMode SpaceMode
        {
            get => spaceMode;
            set
            {
                if (spaceMode == value) return;
                spaceMode = value;
                Refresh();
            }
        }

        public ScrollbarSide Side
        {
            get => side;
            set
            {
                if (side == value) return;
                side = value;
                Refresh();
            }
        }

        public float Width
        {
            get => width;
            set
            {
                var normalized = Mathf.Max(1f, value);
                if (Mathf.Approximately(width, normalized)) return;
                width = normalized;
                Refresh();
            }
        }

        public float Gap
        {
            get => gap;
            set
            {
                var normalized = Mathf.Max(0f, value);
                if (Mathf.Approximately(gap, normalized)) return;
                gap = normalized;
                Refresh();
            }
        }

        public bool IsVisible => isVisible;
        public float ReservedInset => Mathf.Max(0f, reservedInset);
        public RectTransform OppositeScrollbarArea => oppositeScrollbarArea;

        private void OnEnable()
        {
            if (scrollRect != null) scrollRect.verticalScrollbar = null;
            if (scrollRect != null) scrollRect.onValueChanged.AddListener(OnScrollRectChanged);
            if (scrollbar != null) scrollbar.onValueChanged.AddListener(OnScrollbarChanged);
            Refresh();
        }

        private void OnDisable()
        {
            if (scrollRect != null) scrollRect.onValueChanged.RemoveListener(OnScrollRectChanged);
            if (scrollbar != null) scrollbar.onValueChanged.RemoveListener(OnScrollbarChanged);
        }

        private void LateUpdate()
        {
            if (scrollRect == null || viewport == null)
            {
                return;
            }

            var contentHeight = scrollRect.content != null ? scrollRect.content.rect.height : 0f;
            var viewportSize = viewport.rect.size;
            if (!Mathf.Approximately(lastContentHeight, contentHeight) ||
                !Approximately(lastViewportSize, viewportSize))
            {
                Refresh();
            }
        }

        public void Refresh()
        {
            if (refreshing || scrollRect == null || viewport == null || scrollbar == null) return;
            refreshing = true;
            try
            {
                ApplyAreaGeometry();
                if (spaceMode == ScrollbarSpaceMode.ReserveSymmetricallyAlways)
                {
                    ApplyViewportInset(Mathf.Max(1f, width) + Mathf.Max(0f, gap));
                }
                // Auto + conditional reservation must evaluate overflow using the full width.
                // Otherwise an old reserved inset can keep wrapped content overflowing forever.
                if (visibility == ScrollbarVisibilityMode.Auto &&
                    spaceMode == ScrollbarSpaceMode.ReserveWhenVisible &&
                    reservedInset > 0f)
                {
                    ApplyViewportInset(0f);
                }

                var overflows = HasOverflow(scrollRect.content, viewport);
                var visible = CalculateVisibility(visibility, overflows);
                var inset = CalculateReservedInset(visibility, spaceMode, visible, width, gap);
                ApplyScrollbarPresentation(visible);
                ApplyViewportInset(inset);
                isVisible = visible;
                ApplyHandleSize(overflows);
                lastContentHeight = scrollRect.content != null ? scrollRect.content.rect.height : 0f;
                lastViewportSize = viewport.rect.size;
            }
            finally
            {
                refreshing = false;
            }
        }

        public static bool CalculateVisibility(ScrollbarVisibilityMode mode, bool overflows)
        {
            return mode == ScrollbarVisibilityMode.Always ||
                   mode == ScrollbarVisibilityMode.Auto && overflows;
        }

        public static float CalculateReservedInset(
            ScrollbarVisibilityMode visibilityMode,
            ScrollbarSpaceMode mode,
            bool visible,
            float scrollbarWidth,
            float scrollbarGap)
        {
            var inset = Mathf.Max(1f, scrollbarWidth) + Mathf.Max(0f, scrollbarGap);
            if (mode == ScrollbarSpaceMode.ReserveSymmetricallyAlways)
            {
                return inset;
            }

            if (visibilityMode == ScrollbarVisibilityMode.Hidden ||
                mode == ScrollbarSpaceMode.Overlay)
            {
                return 0f;
            }

            var reserve = mode == ScrollbarSpaceMode.ReserveAlways ||
                          mode == ScrollbarSpaceMode.ReserveWhenVisible && visible;
            return reserve ? inset : 0f;
        }

        public static Vector2 CalculateHorizontalOffsets(
            ScrollbarSpaceMode mode,
            ScrollbarSide scrollbarSide,
            float inset)
        {
            inset = Mathf.Max(0f, inset);
            if (mode == ScrollbarSpaceMode.ReserveSymmetricallyAlways)
            {
                return new Vector2(inset, -inset);
            }

            return scrollbarSide == ScrollbarSide.Left
                ? new Vector2(inset, 0f)
                : new Vector2(0f, -inset);
        }

        private static bool HasOverflow(RectTransform content, RectTransform viewport)
        {
            return content != null && content.rect.height > viewport.rect.height + 0.5f;
        }

        private void ApplyScrollbarPresentation(bool visible)
        {
            scrollbar.gameObject.SetActive(visible);
            scrollbar.interactable = visible;
            style?.Apply(background, handle);
        }

        private void ApplyAreaGeometry()
        {
            var rect = (RectTransform)scrollbar.transform;
            var left = side == ScrollbarSide.Left;
            rect.anchorMin = new Vector2(left ? 0f : 1f, 0f);
            rect.anchorMax = new Vector2(left ? 0f : 1f, 1f);
            rect.pivot = new Vector2(left ? 0f : 1f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, rect.anchoredPosition.y);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(1f, width));

            var symmetric = spaceMode == ScrollbarSpaceMode.ReserveSymmetricallyAlways;
            if (oppositeScrollbarArea == null)
            {
                WarnMissingOppositeArea(symmetric);
                return;
            }

            oppositeScrollbarArea.gameObject.SetActive(symmetric);
            if (!symmetric)
            {
                return;
            }

            oppositeScrollbarArea.anchorMin = new Vector2(left ? 1f : 0f, 0f);
            oppositeScrollbarArea.anchorMax = new Vector2(left ? 1f : 0f, 1f);
            oppositeScrollbarArea.pivot = new Vector2(left ? 1f : 0f, 0.5f);
            oppositeScrollbarArea.anchoredPosition =
                new Vector2(0f, oppositeScrollbarArea.anchoredPosition.y);
            oppositeScrollbarArea.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                Mathf.Max(1f, width));
        }

        private void WarnMissingOppositeArea(bool symmetric)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (symmetric && !missingOppositeAreaWarningLogged)
            {
                missingOppositeAreaWarningLogged = true;
                Debug.LogWarning(
                    $"{nameof(ConfigurableScrollbarController)} on '{name}' uses " +
                    $"{nameof(ScrollbarSpaceMode.ReserveSymmetricallyAlways)} without an " +
                    "OppositeScrollbarArea reference. Symmetric viewport insets remain active.",
                    this);
            }
#endif
        }

        private void ApplyViewportInset(float inset)
        {
            inset = Mathf.Max(0f, inset);
            var horizontalOffsets = CalculateHorizontalOffsets(spaceMode, side, inset);
            var targetMin = new Vector2(horizontalOffsets.x, viewport.offsetMin.y);
            var targetMax = new Vector2(horizontalOffsets.y, viewport.offsetMax.y);
            if (Mathf.Approximately(reservedInset, inset) &&
                Approximately(viewport.offsetMin, targetMin) &&
                Approximately(viewport.offsetMax, targetMax))
            {
                return;
            }

            viewport.offsetMin = targetMin;
            viewport.offsetMax = targetMax;
            reservedInset = inset;
            ViewportLayoutChanged?.Invoke();
        }

        private static bool Approximately(Vector2 left, Vector2 right)
        {
            return Mathf.Approximately(left.x, right.x) &&
                   Mathf.Approximately(left.y, right.y);
        }

        private void ApplyHandleSize(bool overflows)
        {
            var track = Mathf.Max(1f, ((RectTransform)scrollbar.transform).rect.height);
            var ratio = overflows && scrollRect.content.rect.height > 0f
                ? Mathf.Clamp01(viewport.rect.height / scrollRect.content.rect.height) : 1f;
            scrollbar.size = handleSizeMode switch
            {
                ScrollbarHandleSizeMode.FixedNormalized => fixedNormalizedSize,
                ScrollbarHandleSizeMode.FixedPixels => Mathf.Clamp01(handlePixels / track),
                ScrollbarHandleSizeMode.MinimumPixels => Mathf.Max(ratio, Mathf.Clamp01(handlePixels / track)),
                _ => ratio
            };
        }

        private void OnScrollRectChanged(Vector2 value)
        {
            if (synchronizing) return;
            synchronizing = true;
            scrollbar.value = scrollRect.verticalNormalizedPosition;
            synchronizing = false;
        }

        private void OnScrollbarChanged(float value)
        {
            if (synchronizing) return;
            synchronizing = true;
            scrollRect.verticalNormalizedPosition = value;
            synchronizing = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            width = Mathf.Max(1f, width);
            gap = Mathf.Max(0f, gap);
            Refresh();
        }
#endif
    }
}
