using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TxTRPG.UI.ScreenFrame
{
    // Lives on the root Canvas. Compares the screen size and safe area each LateUpdate (value comparison, no allocation)
    // and, only when one changed, places frameRoot on the computed frame, hands the layout to the letterbox and raises
    // LayoutChanged. Runs early so frame-relative layouts (SlotReelDock) see the new frame in the same LateUpdate.
    // In Edit Mode it drives frameRoot through a DrivenRectTransformTracker so Game View resizes do not dirty the Scene.
    // In Play Mode a canvas scale change marks every TMP_Text under the Canvas for regeneration, because TextMeshPro
    // otherwise keeps a wrong SDF scale after the change (ScreenFrameTextRegeneration).
    [ExecuteAlways]
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class ScreenFrameController : MonoBehaviour
    {
        [SerializeField] private ScreenFrameCanvasScaler scaler;
        [SerializeField] private RectTransform frameRoot;
        [SerializeField] private LetterboxView letterboxView;
        [SerializeField, Tooltip("Play Mode: regenerates TMP text under this Canvas when the canvas scale changes, so text stays sharp after a resize.")]
        private bool regenerateTextOnScaleChange = true;

        private DrivenRectTransformTracker tracker;
        private IScreenFrameInput input = UnityScreenFrameInput.Instance;
        private bool hasInput;
        private Vector2 lastScreenSize;
        private Rect lastSafeArea;
        private Vector2 lastReference;
        private bool lastRespectSafeArea;
        private float lastAppliedScale = float.NaN;
        private bool regenerateTextInEditMode;
        private readonly List<TMP_Text> textBuffer = new List<TMP_Text>();

        public event Action<ScreenFrameLayout> LayoutChanged;

        // The last valid layout; invalid until the first valid screen size was seen.
        public ScreenFrameLayout Current { get; private set; }
        public RectTransform FrameRoot => frameRoot;
        public ScreenFrameCanvasScaler Scaler => scaler;
        public LetterboxView Letterbox => letterboxView;
        public int RecalculationCount { get; private set; }
        public int TextRegenerationCount { get; private set; }

        // Tests only: Edit Mode skips text regeneration because marking text dirty would mark the Scene as changed.
        public void SetTextRegenerationInEditModeForTests(bool enabled) => regenerateTextInEditMode = enabled;

        public void Configure(ScreenFrameCanvasScaler canvasScaler, RectTransform frame, LetterboxView letterbox)
        {
            scaler = canvasScaler;
            frameRoot = frame;
            letterboxView = letterbox;
            if (scaler != null) scaler.SetInput(input);
            hasInput = false;
        }

        // Replaces the screen source of the controller and its scaler (tests, simulated safe areas); null restores
        // UnityEngine.Screen.
        public void SetInput(IScreenFrameInput source)
        {
            input = source ?? UnityScreenFrameInput.Instance;
            if (scaler != null) scaler.SetInput(source);
            hasInput = false;
        }

        private void OnEnable()
        {
            hasInput = false;
            RefreshIfChanged();
        }

        private void OnDisable()
        {
            tracker.Clear();
            if (scaler != null) scaler.ClearFrameScale();
        }

        private void LateUpdate() => RefreshIfChanged();

        // Returns true when the inputs changed and the frame was recalculated.
        public bool RefreshIfChanged()
        {
            var screen = input.ScreenSize;
            var reference = scaler != null ? scaler.FrameReferenceResolution : ScreenFrameMath.DefaultReferenceResolution;
            var respectSafeArea = scaler == null || scaler.RespectSafeArea;
            var safe = respectSafeArea ? input.SafeArea : new Rect(Vector2.zero, screen);
            if (hasInput && screen == lastScreenSize && safe == lastSafeArea && reference == lastReference &&
                respectSafeArea == lastRespectSafeArea) return false;

            hasInput = true;
            lastScreenSize = screen;
            lastSafeArea = safe;
            lastReference = reference;
            lastRespectSafeArea = respectSafeArea;

            var layout = ScreenFrameMath.Calculate(screen, safe, reference);
            // A zero-size screen (minimized window) keeps the last valid layout.
            if (!layout.IsValid) return false;
            Apply(layout);
            return true;
        }

        private void Apply(ScreenFrameLayout layout)
        {
            RecalculationCount++;
            Current = layout;
            if (scaler != null) scaler.SetFrameScale(layout.ScaleFactor);
            RegenerateTextIfScaleChanged(layout.ScaleFactor);
            PlaceFrame(layout);
            if (letterboxView != null) letterboxView.ApplyLayout(layout);
            LayoutChanged?.Invoke(layout);
        }

        private void RegenerateTextIfScaleChanged(float scale)
        {
            if (scale == lastAppliedScale) return;
            lastAppliedScale = scale;
            if (!regenerateTextOnScaleChange || !(UnityEngine.Application.isPlaying || regenerateTextInEditMode)) return;
            ScreenFrameTextRegeneration.MarkForRegeneration(transform, textBuffer);
            TextRegenerationCount++;
        }

        private void PlaceFrame(ScreenFrameLayout layout)
        {
            if (frameRoot == null) return;
            tracker.Clear();
            tracker.Add(this, frameRoot, DrivenTransformProperties.Anchors | DrivenTransformProperties.Pivot |
                DrivenTransformProperties.AnchoredPosition | DrivenTransformProperties.SizeDelta);
            if (frameRoot.anchorMin != Vector2.zero) frameRoot.anchorMin = Vector2.zero;
            if (frameRoot.anchorMax != Vector2.zero) frameRoot.anchorMax = Vector2.zero;
            if (frameRoot.pivot != Vector2.zero) frameRoot.pivot = Vector2.zero;
            if (frameRoot.anchoredPosition != layout.FrameRect.position) frameRoot.anchoredPosition = layout.FrameRect.position;
            if (frameRoot.sizeDelta != layout.FrameRect.size) frameRoot.sizeDelta = layout.FrameRect.size;
        }
    }
}
