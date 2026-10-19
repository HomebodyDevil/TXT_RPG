using UnityEngine;

namespace TxTRPG.UI.ScreenFrame
{
    // Keeps this RectTransform on the screen-frame area while it sits in a full-screen layer (for example a modal body
    // under a full-screen backdrop). The parent must cover the whole canvas; the frame is expressed as anchor fractions
    // of the canvas, so the result does not depend on when the canvas size updates. No graphic, so input passes
    // through. Without a controller it stays stretched over the parent and warns once.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class ScreenFrameAlignedRect : MonoBehaviour
    {
        [SerializeField] private ScreenFrameController controller;

        private bool warnedMissingController;

        public ScreenFrameController Controller => controller;

        public void SetController(ScreenFrameController value)
        {
            if (controller == value) return;
            if (isActiveAndEnabled && controller != null) controller.LayoutChanged -= Apply;
            controller = value;
            if (isActiveAndEnabled) Bind();
        }

        private void OnEnable() => Bind();

        private void OnDisable()
        {
            if (controller != null) controller.LayoutChanged -= Apply;
        }

        private void Bind()
        {
            if (controller == null)
            {
                if (!warnedMissingController && UnityEngine.Application.isPlaying)
                {
                    warnedMissingController = true;
                    Debug.LogWarning($"{name}: ScreenFrameAlignedRect has no ScreenFrameController; it covers the whole screen.", this);
                }
                Apply(ScreenFrameLayout.Invalid);
                return;
            }
            controller.LayoutChanged -= Apply;
            controller.LayoutChanged += Apply;
            Apply(controller.Current);
        }

        public void Apply(ScreenFrameLayout layout)
        {
            var rect = (RectTransform)transform;
            Vector2 min = Vector2.zero, max = Vector2.one;
            if (layout.IsValid && layout.CanvasSize.x > 0f && layout.CanvasSize.y > 0f)
            {
                min = new Vector2(layout.FrameRect.xMin / layout.CanvasSize.x, layout.FrameRect.yMin / layout.CanvasSize.y);
                max = new Vector2(layout.FrameRect.xMax / layout.CanvasSize.x, layout.FrameRect.yMax / layout.CanvasSize.y);
            }
            if (rect.anchorMin != min) rect.anchorMin = min;
            if (rect.anchorMax != max) rect.anchorMax = max;
            if (rect.anchoredPosition != Vector2.zero) rect.anchoredPosition = Vector2.zero;
            if (rect.sizeDelta != Vector2.zero) rect.sizeDelta = Vector2.zero;
        }
    }
}
