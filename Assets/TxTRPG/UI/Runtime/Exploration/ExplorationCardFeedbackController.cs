using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TxTRPG.UI.Exploration
{
    [DisallowMultipleComponent]
    public sealed class ExplorationCardFeedbackController : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler, ICancelHandler
    {
        [SerializeField] private RectTransform motionRoot;
        [SerializeField] private Button button;
        [SerializeField] private Graphic[] tintTargets;
        [SerializeField] private Graphic focusVisual;
        [SerializeField] private ExplorationCardPresentationProfile profile;
        private Color[] baseColors;
        private bool hovered, focused, pressed, confirming;
        private bool navigationFocus = true, pulsing;
        private double pulseStarted;
        private int generation;
        private PointerEventData pointer;
        private InputSystemUIInputModule inputModule;
        private InputAction pointAction, clickAction, moveAction, submitAction;
        private InputDevice inputDevice;
        private Vector3 targetScale = Vector3.one;
        private Color targetTint = Color.white;

        public float ConfirmationDuration => profile != null ? profile.SafeConfirmationDuration : 0f;

        private void Awake() { CaptureBaseColors(); RefreshTarget(); }
        private void OnEnable() { ExplorationCardPlayerPreferences.Changed += OnPreferencesChanged; InputSystem.onDeviceChange += OnDeviceChange; if (baseColors == null) CaptureBaseColors(); ResetState(); BindInput(); }
        private void OnDisable() { ExplorationCardPlayerPreferences.Changed -= OnPreferencesChanged; InputSystem.onDeviceChange -= OnDeviceChange; UnbindInput(); ResetState(); }
        private void Update()
        {
            if (motionRoot == null) return;
            BindInput();
            if (pointer != null && pointer.dragging) { hovered = pressed = false; navigationFocus = false; }
            RefreshTarget(); // Also observes external Button and CanvasGroup locks.
            var duration = profile != null && float.IsFinite(profile.transitionDuration) ? Mathf.Max(0f, profile.transitionDuration) : 0f;
            var factor = duration <= 0f ? 1f : 1f - Mathf.Exp(-Time.unscaledDeltaTime / duration);
            motionRoot.localScale = ExplorationCardPlayerPreferences.AllowsMotion ? Vector3.Lerp(motionRoot.localScale, targetScale, factor) : Vector3.one;
            ApplyTint(factor);
        }

        public IEnumerator PlayConfirmation()
        {
            var version = generation;
            confirming = true; RefreshTarget();
            try
            {
                var end = Time.unscaledTimeAsDouble + ConfirmationDuration;
                while (version == generation && isActiveAndEnabled && ExplorationCardPlayerPreferences.AllowsMotion && Time.unscaledTimeAsDouble < end) yield return null;
            }
            finally { if (version == generation) { confirming = false; RefreshTarget(); } }
        }

        public void ResetState() { generation++; hovered = focused = pressed = confirming = pulsing = false; pointer = null; pulseStarted = 0d; RefreshTarget(); if (motionRoot != null) motionRoot.localScale = Vector3.one; ApplyTint(1f); }
        private static bool IsTouch(PointerEventData data) => data is ExtendedPointerEventData extended && extended.pointerType == UIPointerType.Touch;
        public void OnPointerEnter(PointerEventData data) { pointer = data; hovered = !IsTouch(data); RefreshTarget(); }
        public void OnPointerExit(PointerEventData data) { pointer = null; hovered = pressed = false; RefreshTarget(); }
        public void OnPointerDown(PointerEventData data) { pointer = data; navigationFocus = false; if (IsTouch(data)) hovered = false; pressed = data.button == PointerEventData.InputButton.Left; RefreshTarget(); }
        public void OnPointerUp(PointerEventData data) { pointer = null; pressed = false; if (IsTouch(data)) hovered = false; RefreshTarget(); }
        public void OnSelect(BaseEventData data) { focused = true; if (data is PointerEventData) navigationFocus = false; RefreshTarget(); }
        public void OnDeselect(BaseEventData _) { focused = false; pressed = false; RefreshTarget(); }
        public void OnCancel(BaseEventData _) { pointer = null; hovered = pressed = navigationFocus = false; RefreshTarget(); }

        private void OnPreferencesChanged() { pulsing = false; RefreshTarget(); if (!ExplorationCardPlayerPreferences.AllowsMotion && motionRoot != null) motionRoot.localScale = Vector3.one; ApplyTint(1f); }
        private void RefreshTarget()
        {
            var effects = ExplorationCardPlayerPreferences.EffectsEnabled;
            var interactive = button == null || button.IsInteractable();
            var highlighted = hovered || focused;
            var tint = profile == null ? Color.white : confirming ? profile.confirmingTint : !interactive ? profile.disabledTint : pressed ? profile.pressedTint : highlighted ? profile.highlightedTint : profile.normalTint;
            targetTint = tint;
            var scale = 1f;
            var shouldPulse = interactive && !pressed && !confirming && (hovered || focused && navigationFocus) && ExplorationCardPlayerPreferences.AllowsMotion && profile != null && profile.pulseEnabled;
            if (shouldPulse && !pulsing) pulseStarted = Time.unscaledTimeAsDouble;
            pulsing = shouldPulse;
            if (effects && !ExplorationCardPlayerPreferences.ReduceMotion && profile != null)
            {
                if (confirming) scale = ExplorationCardPresentationProfile.SafeScale(profile.highlightedScale);
                else if (interactive && pressed) scale = ExplorationCardPresentationProfile.SafeScale(profile.pressedScale);
                else if (pulsing) scale = profile.EvaluatePulse(Time.unscaledTimeAsDouble - pulseStarted);
                else if (interactive && (hovered || focused && navigationFocus) && !profile.pulseEnabled) scale = ExplorationCardPresentationProfile.SafeScale(profile.highlightedScale);
            }
            targetScale = Vector3.one * scale;
            if (focusVisual != null) focusVisual.gameObject.SetActive(interactive && highlighted || confirming);
        }
        private void BindInput()
        {
            var current = EventSystem.current != null ? EventSystem.current.currentInputModule as InputSystemUIInputModule : null;
            if (current == inputModule) return;
            UnbindInput(); inputModule = current;
            if (current == null) return;
            pointAction = current.point?.action; clickAction = current.leftClick?.action;
            moveAction = current.move?.action; submitAction = current.submit?.action;
            if (pointAction != null) pointAction.performed += OnInput;
            if (clickAction != null) clickAction.performed += OnInput;
            if (moveAction != null) moveAction.performed += OnInput;
            if (submitAction != null) submitAction.performed += OnInput;
        }
        private void UnbindInput()
        {
            if (pointAction != null) pointAction.performed -= OnInput;
            if (clickAction != null) clickAction.performed -= OnInput;
            if (moveAction != null) moveAction.performed -= OnInput;
            if (submitAction != null) submitAction.performed -= OnInput;
            pointAction = clickAction = moveAction = submitAction = null; inputModule = null;
        }
        private void OnInput(InputAction.CallbackContext context)
        {
            inputDevice = context.control?.device;
            navigationFocus = context.action == moveAction || context.action == submitAction;
            if (inputDevice is Touchscreen) { hovered = pressed = false; pointer = null; }
            RefreshTarget();
        }
        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (device != inputDevice || change is not (InputDeviceChange.Removed or InputDeviceChange.Disconnected or InputDeviceChange.Disabled)) return;
            hovered = pressed = navigationFocus = false; pointer = null; RefreshTarget();
        }
        private void CaptureBaseColors()
        {
            if (tintTargets == null) tintTargets = System.Array.Empty<Graphic>();
            baseColors = new Color[tintTargets.Length]; for (var i = 0; i < tintTargets.Length; i++) if (tintTargets[i] != null) baseColors[i] = tintTargets[i].canvasRenderer.GetColor();
        }
        private void ApplyTint(float factor)
        {
            if (baseColors == null || baseColors.Length != tintTargets.Length) CaptureBaseColors();
            // Keep authored Graphic colors intact, including runtime shape changes.
            for (var i = 0; i < tintTargets.Length; i++)
                if (tintTargets[i] != null)
                {
                    var renderer = tintTargets[i].canvasRenderer;
                    var next = Color.Lerp(renderer.GetColor(), baseColors[i] * targetTint, factor);
                    if (renderer.GetColor() != next) renderer.SetColor(next);
                }
        }
        private void ApplyImmediate() { if (motionRoot != null) motionRoot.localScale = targetScale; ApplyTint(1f); }
#if UNITY_EDITOR
        public void ConfigureForEditor(RectTransform motion, Button targetButton, Graphic[] targets, Graphic focus, ExplorationCardPresentationProfile targetProfile)
        { motionRoot = motion; button = targetButton; tintTargets = targets; focusVisual = focus; profile = targetProfile; CaptureBaseColors(); RefreshTarget(); ApplyImmediate(); }
#endif
    }
}
