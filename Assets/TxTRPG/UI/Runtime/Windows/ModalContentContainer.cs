using UnityEngine;

namespace TxTRPG.UI.Windows
{
    [DisallowMultipleComponent]
    public sealed class ModalContentContainer : MonoBehaviour
    {
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private RectTransform overlayRoot;
        [SerializeField] private RectOffset padding;

        public RectTransform ContentRoot => contentRoot;
        public RectTransform OverlayRoot => overlayRoot;
        public RectOffset Padding => padding;

        private void OnEnable() => ApplyLayout();
        private void OnRectTransformDimensionsChange() => ApplyLayout();

#if UNITY_EDITOR
        private void OnValidate() => ApplyLayout();
        public void ConfigureForEditor(RectTransform content, RectTransform overlay, RectOffset configuredPadding)
        {
            contentRoot = content;
            overlayRoot = overlay;
            padding = configuredPadding ?? new RectOffset();
            ApplyLayout();
        }
#endif

        public void ApplyLayout()
        {
            if (contentRoot == null) return;
            padding ??= new RectOffset();
            contentRoot.anchorMin = Vector2.zero;
            contentRoot.anchorMax = Vector2.one;
            contentRoot.offsetMin = new Vector2(padding.left, padding.bottom);
            contentRoot.offsetMax = new Vector2(-padding.right, -padding.top);
        }

        public bool ValidateConfiguration(out string reason)
        {
            if (contentRoot == null) { reason = "Content Root is required."; return false; }
            if (overlayRoot == null) { reason = "Overlay Root is required."; return false; }
            if (contentRoot == overlayRoot) { reason = "Content Root and Overlay Root must be separate."; return false; }
            reason = string.Empty;
            return true;
        }
    }
}
