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

        public ScrollbarVisibilityMode Visibility { get => visibility; set { visibility = value; Refresh(); } }

        private void OnEnable()
        {
            if (scrollRect != null) scrollRect.verticalScrollbar = null;
            if (scrollRect != null) scrollRect.verticalScrollbar = null;
            if (scrollRect != null) scrollRect.onValueChanged.AddListener(OnScrollRectChanged);
            if (scrollbar != null) scrollbar.onValueChanged.AddListener(OnScrollbarChanged);
            Refresh();
        }

        private void LateUpdate() => Refresh();

        private void OnDisable()
        {
            if (scrollRect != null) scrollRect.onValueChanged.RemoveListener(OnScrollRectChanged);
            if (scrollbar != null) scrollbar.onValueChanged.RemoveListener(OnScrollbarChanged);
        }

        public void Refresh()
        {
            if (scrollRect == null || viewport == null || scrollbar == null) return;
            var overflows = scrollRect.content != null && scrollRect.content.rect.height > viewport.rect.height + 0.5f;
            var visible = visibility == ScrollbarVisibilityMode.Always ||
                visibility == ScrollbarVisibilityMode.Auto && overflows;
            scrollbar.gameObject.SetActive(visible);
            scrollbar.interactable = visible;
            style?.Apply(background, handle);
            var rect = (RectTransform)scrollbar.transform;
            var left = side == ScrollbarSide.Left;
            rect.anchorMin = new Vector2(left ? 0f : 1f, 0f);
            rect.anchorMax = new Vector2(left ? 0f : 1f, 1f);
            rect.pivot = new Vector2(left ? 0f : 1f, 0.5f);
            rect.sizeDelta = new Vector2(width, 0f);
            var reserve = spaceMode == ScrollbarSpaceMode.ReserveAlways ||
                spaceMode == ScrollbarSpaceMode.ReserveWhenVisible && visible;
            var inset = reserve ? width + gap : 0f;
            viewport.offsetMin = new Vector2(left ? inset : 0f, viewport.offsetMin.y);
            viewport.offsetMax = new Vector2(left ? 0f : -inset, viewport.offsetMax.y);
            ApplyHandleSize(overflows);
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
    }
}
