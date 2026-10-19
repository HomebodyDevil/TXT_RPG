using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.ScreenFrame
{
    // Scale With Screen Size that fits the reference frame inside Screen.safeArea (the Expand rule applied to the safe
    // area), so the frame is always referenceResolution canvas units. The reference resolution follows the profile; the
    // serialized CanvasScaler value is only kept in sync for the Inspector. ScreenFrameController pushes the scale it
    // computed from the screen in LateUpdate (SetFrameScale); without it the scaler computes the scale itself from
    // Canvas.renderingDisplaySize, because Screen.width can report another Editor view during canvas rendering.
    [ExecuteAlways]
    [RequireComponent(typeof(Canvas))]
    [AddComponentMenu("Layout/Screen Frame Canvas Scaler")]
    public sealed class ScreenFrameCanvasScaler : CanvasScaler
    {
        [SerializeField] private ScreenFrameProfile profile;

        private bool reportedMissingProfile;
        private IScreenFrameInput input = UnityScreenFrameInput.Instance;
        private Canvas rootCanvas;
        private bool hasFrameScale;
        private float frameScale = 1f;

        public ScreenFrameProfile Profile => profile;
        public Vector2 FrameReferenceResolution => profile != null ? profile.ReferenceResolution : ScreenFrameMath.DefaultReferenceResolution;
        public bool RespectSafeArea => profile == null || profile.RespectSafeArea;
        public bool HasFrameScale => hasFrameScale;
        public float FrameScale => frameScale;

        public void SetProfile(ScreenFrameProfile value)
        {
            profile = value;
            reportedMissingProfile = false;
            SyncSerializedSettings();
        }

        // Replaces the screen source (tests, simulated safe areas); null restores UnityEngine.Screen.
        public void SetInput(IScreenFrameInput source) => input = source ?? UnityScreenFrameInput.Instance;

        // Uses the scale computed by ScreenFrameController and applies it right away instead of waiting for the next
        // canvas render, so layouts computed later in the same frame (LateUpdate) see the new canvas size.
        public void SetFrameScale(float scale)
        {
            if (!(scale > 0f) || float.IsInfinity(scale)) return;
            hasFrameScale = true;
            frameScale = scale;
            Handle();
        }

        // Returns to computing the scale from the canvas display size.
        public void ClearFrameScale()
        {
            hasFrameScale = false;
            Handle();
        }

        protected override void OnEnable()
        {
            rootCanvas = GetComponent<Canvas>();
            SyncSerializedSettings();
            ReportMissingProfile();
            base.OnEnable();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            SyncSerializedSettings();
            base.OnValidate();
        }
#endif

        protected override void HandleScaleWithScreenSize()
        {
            ReportMissingProfile();
            if (hasFrameScale)
            {
                SetScaleFactor(frameScale);
            }
            else
            {
                var screen = ReferenceEquals(input, UnityScreenFrameInput.Instance) && rootCanvas != null
                    ? rootCanvas.renderingDisplaySize
                    : input.ScreenSize;
                var safe = RespectSafeArea ? input.SafeArea : new Rect(Vector2.zero, screen);
                var layout = ScreenFrameMath.Calculate(screen, safe, FrameReferenceResolution);
                // A zero-size screen (minimized window) keeps the last scale.
                if (layout.IsValid) SetScaleFactor(layout.ScaleFactor);
            }
            SetReferencePixelsPerUnit(m_ReferencePixelsPerUnit);
        }

        private void ReportMissingProfile()
        {
            if (profile != null || reportedMissingProfile) return;
            reportedMissingProfile = true;
            Debug.LogError($"{name}: ScreenFrameCanvasScaler has no ScreenFrameProfile; using 1920x1080 with the safe area.", this);
        }

        private void SyncSerializedSettings()
        {
            if (uiScaleMode != ScaleMode.ScaleWithScreenSize) uiScaleMode = ScaleMode.ScaleWithScreenSize;
            var reference = FrameReferenceResolution;
            if (referenceResolution != reference) referenceResolution = reference;
        }
    }
}
