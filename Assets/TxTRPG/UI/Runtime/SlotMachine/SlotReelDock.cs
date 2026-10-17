using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.SlotMachine
{
    // Slot reels docked at the bottom of their parent (an overlay layer outside the screen layout), with the total card
    // across the reel machine's top edge. It receives display data only and never decides results. Reel, divider,
    // payline and spin handling is SlotReelMachineCore, shared with SlotMachinePanel, with the same SlotReel.prefab, spin
    // profile and skin catalog (the skin applies to the reel targets only).
    // Of SlotSummaryDisplay it shows TotalLabel, TotalValue and TotalMultiplierText; the rune lines, column titles, empty
    // text and overflow format are ignored because the dock has no rune table.
    // It takes no input and never blocks the screen behind it: every graphic has raycastTarget off and the CanvasGroup
    // neither blocks raycasts nor is interactable. Show/Hide move PresentationRoot only; the layout root, the reel layout
    // and the card placement stay, and the GameObject stays active so a running spin continues while hidden.
    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public sealed class SlotReelDock : MonoBehaviour, ISlotMachinePresenter, ISlotReelMachineHost
    {
        [Header("Placement (canvas units; ratios of the parent size)")]
        [SerializeField, Range(.05f, 1f), Tooltip("Reel machine height = parent height × ratio, clamped. Excludes the total card overhang.")]
        private float heightRatio = .28f;
        [SerializeField, Min(1f)] private float minimumHeight = 140f;
        [SerializeField, Min(1f)] private float maximumHeight = 320f;
        [SerializeField, Min(0f)] private float horizontalMargin = 16f;
        [SerializeField, Min(0f)] private float bottomMargin = 12f;
        [SerializeField, Min(0f), Tooltip("Maximum dock width, centered. 0 means no limit.")]
        private float maximumWidth = 1200f;
        [SerializeField, Tooltip("Adds the bottom, left and right Screen.safeArea insets to the outer margins.")]
        private bool respectSafeArea = true;
        [SerializeField, Tooltip("Shown state on Awake.")] private bool initiallyShown = true;
        [SerializeField, Tooltip("Optional MonoBehaviour implementing ISlotReelDockTransition. Empty switches immediately.")]
        private MonoBehaviour transitionBehaviour;

        [Header("Structure")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField, Tooltip("The only RectTransform show/hide (and later transitions) move.")]
        private RectTransform presentationRoot;
        [SerializeField, Tooltip("Layout-only reel machine; fills the dock below the total card overhang.")]
        private RectTransform reelMachine;
        [SerializeField] private RectTransform reelArea;
        [SerializeField, Tooltip("Raises LayoutApplied after the reel area is laid out; the payline frame follows it.")]
        private SlotReelAreaLayout reelAreaLayout;
        [SerializeField, Tooltip("Reel added when a display has more reels than the dock shows.")]
        private SlotReelViewBase reelPrefab;
        [SerializeField] private List<SlotReelViewBase> reelViews = new();
        [SerializeField, Tooltip("Dividers between reels; the first one is the template for added dividers.")]
        private List<RectTransform> reelDividers = new();
        [SerializeField, Tooltip("Layout-only payline root (PaylineFrame).")] private RectTransform paylineRoot;
        [SerializeField] private SlotPaylineFrameGraphic paylineFrame;
        [SerializeField] private Image paylineImage;
        [SerializeField] private SlotTotalCardView totalCard;
        [SerializeField, Tooltip("Configuration error text; inactive otherwise.")] private TMP_Text statusText;
        [SerializeField, Tooltip("Spin timing, easing and high-speed effect. Without it the built-in defaults are used.")]
        private SlotReelSpinProfile spinProfile;
        [SerializeField, Min(1f)] private float minimumFontSize = 16f;
        [SerializeField, Min(1f)] private float maximumFontSize = 64f;

        [Header("Skin (reel targets only)")]
        [SerializeField, Tooltip("Skins and the selected skin applied in Play Mode. Without it the Prefab's default look is shown.")]
        private SlotMachinePanelSkinCatalog skinCatalog;
        [SerializeField] private SlotSkinGraphicTarget reelMachineFrameSkin = new();
        [SerializeField] private SlotSkinGraphicTarget leftEdgeSkin = new();
        [SerializeField] private SlotSkinGraphicTarget rightEdgeSkin = new();
        [SerializeField, Tooltip("The first divider; its defaults are used for every divider, including added ones.")]
        private SlotSkinGraphicTarget dividerSkin = new();
        [SerializeField, Tooltip("Color of the code-drawn payline border.")]
        private SlotSkinGraphicTarget paylineBorderSkin = new();

        private SlotReelMachineCore machine;
        private bool warnedMissingProfile;
        private bool hasSettingsOverride;
        private SlotReelSpinSettings settingsOverride;
        private SlotMachinePanelSkin activeSkin;
        private bool skinResolved;
        private bool warnedSkinCatalog;
        private bool warnedTransition;
        private bool initialized;
        private CancellationTokenSource transitionCancellation;
        private Vector2 dockedOffset;

        // Last layout inputs; LateUpdate only compares them.
        private bool hasLayout;
        private Vector2 laidOutParentSize;
        private Rect laidOutSafeArea;
        private Vector2Int laidOutScreenSize;
        private float reservedOverhang;
        private Vector2 shownPosition;
        private Vector2 hiddenPosition;

        // Raised once after IsShown actually changed, with the new state.
        public event Action<bool> ShownChanged;

        public bool IsShown { get; private set; }
        // Set by the owner of the accessibility preference; passed to the transition.
        public bool ReduceMotion { get; set; }
        public bool IsSpinning => Machine.IsSpinning;
        public int ReelCount => reelViews.Count;
        public IReadOnlyList<SlotReelViewBase> ReelViews => reelViews.AsReadOnly();
        public IReadOnlyList<RectTransform> ReelDividers => reelDividers.AsReadOnly();
        public int ActiveDividerCount => Machine.ActiveDividerCount;
        public RectTransform PresentationRoot => presentationRoot;
        public RectTransform ReelMachine => reelMachine;
        public RectTransform ReelArea => reelArea;
        public CanvasGroup CanvasGroup => canvasGroup;
        public SlotTotalCardView TotalCard => totalCard;
        public RectTransform PaylineFrameRect => paylineRoot != null ? paylineRoot : paylineFrame != null ? paylineFrame.rectTransform : null;
        public string TotalLabelText => totalCard != null ? totalCard.TotalLabelText : string.Empty;
        public string TotalValueText => totalCard != null ? totalCard.TotalValueText : string.Empty;
        public string TotalMultiplierText => totalCard != null ? totalCard.TotalMultiplierText : string.Empty;
        public string StatusText => statusText != null && statusText.gameObject.activeSelf ? statusText.text : string.Empty;
        public SlotMachinePanelSkin ActiveSkin => activeSkin;
        // Additional position of the shown state (a later drag placement changes it).
        public Vector2 DockedOffset => dockedOffset;
        // PresentationRoot position of the hidden state: below the parent's bottom edge.
        public Vector2 HiddenOffset => hiddenPosition;
        // PresentationRoot position of the shown state: DockedOffset limited to the parent's safe rectangle.
        public Vector2 ShownPosition => shownPosition;
        // Height reserved above the reel machine for the total card.
        public float ReservedOverhang => reservedOverhang;
        public float MaximumPresentationSeconds => GetMaximumPresentationSeconds(Mathf.Max(1, reelViews.Count));

        private SlotReelMachineCore Machine => machine ??= new SlotReelMachineCore(this);

        private SlotReelSpinSettings Settings
        {
            get
            {
                if (hasSettingsOverride) return settingsOverride;
                if (spinProfile != null) return spinProfile.Settings;
                if (!warnedMissingProfile)
                {
                    warnedMissingProfile = true;
                    Debug.LogWarning($"{name}: no SlotReelSpinProfile is assigned; the built-in default spin settings are used.", this);
                }
                return SlotReelSpinSettings.Default;
            }
        }

        MonoBehaviour ISlotReelMachineHost.Owner => this;
        RectTransform ISlotReelMachineHost.ReelArea => reelArea;
        SlotReelViewBase ISlotReelMachineHost.ReelPrefab => reelPrefab;
        List<SlotReelViewBase> ISlotReelMachineHost.ReelViews => reelViews;
        List<RectTransform> ISlotReelMachineHost.ReelDividers => reelDividers;
        RectTransform ISlotReelMachineHost.PaylineRoot => paylineRoot;
        SlotPaylineFrameGraphic ISlotReelMachineHost.PaylineFrame => paylineFrame;
        Image ISlotReelMachineHost.PaylineImage => paylineImage;
        float ISlotReelMachineHost.MinimumFontSize => minimumFontSize;
        float ISlotReelMachineHost.MaximumFontSize => maximumFontSize;
        SlotReelSpinSettings ISlotReelMachineHost.SpinSettings => Settings;
        bool ISlotReelMachineHost.SkinResolved => skinResolved;
        SlotMachinePanelSkin ISlotReelMachineHost.ActiveSkin => activeSkin;
        void ISlotReelMachineHost.ApplyDividerSkin(RectTransform divider) => ApplyDividerSkin(divider);
        void ISlotReelMachineHost.SetSummary(SlotSummaryDisplay summary) => SetSummary(summary);

        public float GetMaximumPresentationSeconds(int reelCount) =>
            SlotReelSpinPlanner.GetMaximumPresentationSeconds(Settings, Mathf.Max(1, reelCount));

        private void Awake()
        {
            reelViews.RemoveAll(view => view == null);
            reelDividers.RemoveAll(divider => divider == null);
            if (UnityEngine.Application.isPlaying && !skinResolved) ApplyCatalogSelection();
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;
            IsShown = initiallyShown;
            if (canvasGroup != null)
            {
                if (canvasGroup.blocksRaycasts) canvasGroup.blocksRaycasts = false;
                if (canvasGroup.interactable) canvasGroup.interactable = false;
            }
            RefreshLayout();
            SnapToState();
        }

        private void OnEnable() => Machine.Enable(reelAreaLayout);

        private void OnDisable()
        {
            Machine.Disable();
            CancelTransition();
        }

        private void OnDestroy() => CancelTransition();

        // Layout inputs are compared each frame; the layout itself runs only when one changed.
        private void LateUpdate()
        {
            if (!(transform.parent is RectTransform parent)) return;
            var safeArea = respectSafeArea ? Screen.safeArea : default;
            var screen = new Vector2Int(Screen.width, Screen.height);
            if (hasLayout && parent.rect.size == laidOutParentSize && safeArea == laidOutSafeArea && screen == laidOutScreenSize) return;
            RefreshLayout();
        }

        // ── Display contract ──

        public void SetReels(IReadOnlyList<SlotReelDisplay> reels) => Machine.SetReels(reels);

        // Shows TotalLabel, TotalValue and TotalMultiplierText; rune lines are ignored.
        public void SetSummary(SlotSummaryDisplay summary)
        {
            if (summary == null) throw new ArgumentNullException(nameof(summary));
            if (totalCard != null) totalCard.SetTotal(summary.TotalLabel, summary.TotalValue, summary.TotalMultiplierText);
        }

        // A hidden dock still plays the presentation and returns its result; it is only invisible.
        public Task<bool> PlaySpinAsync(SlotSpinDisplay result, bool reduceMotion, CancellationToken cancellationToken) =>
            Machine.PlaySpinAsync(result, reduceMotion, cancellationToken);

        public void StopAtResult(SlotSpinDisplay result) => Machine.StopAtResult(result);

        // Configuration error: the reel machine and the total card are hidden and the reason is shown.
        public void ShowError(string message)
        {
            if (reelMachine != null) reelMachine.gameObject.SetActive(false);
            if (totalCard != null) totalCard.gameObject.SetActive(false);
            SetStatus(message);
        }

        public void ClearError()
        {
            if (reelMachine != null) reelMachine.gameObject.SetActive(true);
            if (totalCard != null) totalCard.gameObject.SetActive(true);
            SetStatus(string.Empty);
        }

        private void SetStatus(string message)
        {
            if (statusText == null) return;
            statusText.text = message ?? string.Empty;
            statusText.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }

        // ── Show / hide ──

        public void Show() => SetShown(true);
        public void Hide() => SetShown(false);

        // Changes the target state; the same state does nothing.
        public void SetShown(bool shown)
        {
            EnsureInitialized();
            if (IsShown == shown) return;
            IsShown = shown;
            MoveToState();
            ShownChanged?.Invoke(shown);
        }

        // Shown-state offset; the resulting position stays inside the parent's safe rectangle.
        public void SetDockedOffset(Vector2 offset)
        {
            EnsureInitialized();
            if (dockedOffset == offset) return;
            dockedOffset = offset;
            RefreshLayout();
        }

        private void MoveToState()
        {
            CancelTransition();
            if (presentationRoot == null) return;
            var target = IsShown ? shownPosition : hiddenPosition;
            var transition = ResolveTransition();
            if (transition == null || !UnityEngine.Application.isPlaying)
            {
                SnapToState();
                return;
            }
            if (IsShown) SetAlpha(1f);
            transitionCancellation = new CancellationTokenSource();
            RunTransition(transition, presentationRoot.anchoredPosition, target, transitionCancellation);
        }

        private async void RunTransition(ISlotReelDockTransition transition, Vector2 from, Vector2 to, CancellationTokenSource cancellation)
        {
            try
            {
                await transition.PlayAsync(presentationRoot, from, to, ReduceMotion, cancellation.Token);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
            if (this == null || cancellation != transitionCancellation) return;
            transitionCancellation = null;
            cancellation.Dispose();
            SnapToState();
        }

        private void CancelTransition()
        {
            var cancellation = transitionCancellation;
            transitionCancellation = null;
            if (cancellation == null) return;
            cancellation.Cancel();
            cancellation.Dispose();
        }

        // Puts PresentationRoot on the current state's position (no transition running).
        private void SnapToState()
        {
            if (presentationRoot != null)
            {
                var target = IsShown ? shownPosition : hiddenPosition;
                if (presentationRoot.anchoredPosition != target) presentationRoot.anchoredPosition = target;
            }
            SetAlpha(IsShown ? 1f : 0f);
        }

        private void SetAlpha(float alpha)
        {
            if (canvasGroup != null && !Mathf.Approximately(canvasGroup.alpha, alpha)) canvasGroup.alpha = alpha;
        }

        private ISlotReelDockTransition ResolveTransition()
        {
            if (transitionBehaviour == null) return null;
            if (transitionBehaviour is ISlotReelDockTransition transition) return transition;
            if (!warnedTransition)
            {
                warnedTransition = true;
                Debug.LogWarning($"{name}: '{transitionBehaviour.GetType().Name}' does not implement ISlotReelDockTransition; the dock switches immediately.", this);
            }
            return null;
        }

        // ── Layout ──

        // Sizes and places the dock in its parent, reserves the total card overhang and places the card. Called when the
        // parent size, the safe area, the screen size or a setting changed.
        public void RefreshLayout()
        {
            if (!(transform is RectTransform root) || !(root.parent is RectTransform parent)) return;
            var parentSize = parent.rect.size;
            hasLayout = true;
            laidOutParentSize = parentSize;
            laidOutSafeArea = respectSafeArea ? Screen.safeArea : default;
            laidOutScreenSize = new Vector2Int(Screen.width, Screen.height);
            var insets = SafeInsets(parent);

            var width = parentSize.x - horizontalMargin * 2f - insets.x - insets.y;
            if (maximumWidth > 0f) width = Mathf.Min(width, maximumWidth);
            width = Mathf.Max(0f, width);
            var bottom = bottomMargin + insets.z;
            var available = Mathf.Max(0f, parentSize.y - bottom - insets.w);
            var reelHeight = Mathf.Clamp(parentSize.y * heightRatio, minimumHeight, Mathf.Max(minimumHeight, maximumHeight));
            var overhang = Overhang(reelHeight);
            // A short parent shrinks the reels below the minimum so the card never passes the parent's top edge.
            for (var i = 0; i < 4 && reelHeight + overhang > available; i++)
            {
                reelHeight = Mathf.Max(0f, available - overhang);
                overhang = Overhang(reelHeight);
            }
            if (reelHeight + overhang > available) overhang = Mathf.Max(0f, available - reelHeight);
            reservedOverhang = overhang;
            var height = reelHeight + overhang;

            var bottomCenter = new Vector2(.5f, 0f);
            if (root.anchorMin != bottomCenter) root.anchorMin = bottomCenter;
            if (root.anchorMax != bottomCenter) root.anchorMax = bottomCenter;
            if (root.pivot != bottomCenter) root.pivot = bottomCenter;
            var size = new Vector2(width, height);
            if (root.sizeDelta != size) root.sizeDelta = size;
            var position = new Vector2((insets.x - insets.y) * .5f, bottom);
            if (root.anchoredPosition != position) root.anchoredPosition = position;

            if (presentationRoot != null) Stretch(presentationRoot, 0f, presentationRoot.anchoredPosition);
            if (reelMachine != null) Stretch(reelMachine, overhang, Vector2.zero);
            if (totalCard != null) totalCard.Refresh();

            // Shown position: the docked offset limited so the dock stays inside the parent's safe rectangle.
            var minX = -parentSize.x * .5f + insets.x - (position.x - width * .5f);
            var maxX = parentSize.x * .5f - insets.y - (position.x + width * .5f);
            var minY = insets.z - bottom;
            var maxY = parentSize.y - insets.w - (bottom + height);
            shownPosition = new Vector2(ClampRange(dockedOffset.x, minX, maxX), ClampRange(dockedOffset.y, minY, maxY));
            hiddenPosition = new Vector2(shownPosition.x, -(bottom + height + Mathf.Max(0f, shownPosition.y)));
            // Before initialization (Edit Mode, builders) the shown state is not decided yet, so the saved look is kept.
            if (initialized && transitionCancellation == null) SnapToState();
        }

        private float Overhang(float reelHeight) => totalCard != null ? totalCard.CalculateOverhang(reelHeight) : 0f;

        // (left, right, bottom, top) safe-area insets in parent units. The top inset only limits the card and drag range.
        private Vector4 SafeInsets(RectTransform parent)
        {
            if (!respectSafeArea || Screen.width <= 0 || Screen.height <= 0) return Vector4.zero;
            var safe = Screen.safeArea;
            var canvas = parent.GetComponentInParent<Canvas>();
            var reference = canvas != null ? (RectTransform)canvas.rootCanvas.transform : parent;
            var scale = new Vector2(reference.rect.width / Screen.width, reference.rect.height / Screen.height);
            if (scale.x <= 0f || scale.y <= 0f || float.IsNaN(scale.x) || float.IsNaN(scale.y)) return Vector4.zero;
            return new Vector4(
                Mathf.Max(0f, safe.xMin) * scale.x,
                Mathf.Max(0f, Screen.width - safe.xMax) * scale.x,
                Mathf.Max(0f, safe.yMin) * scale.y,
                Mathf.Max(0f, Screen.height - safe.yMax) * scale.y);
        }

        private static float ClampRange(float value, float min, float max) => max < min ? (min + max) * .5f : Mathf.Clamp(value, min, max);

        private static void Stretch(RectTransform rect, float top, Vector2 anchoredPosition)
        {
            if (rect.anchorMin != Vector2.zero) rect.anchorMin = Vector2.zero;
            if (rect.anchorMax != Vector2.one) rect.anchorMax = Vector2.one;
            var middle = new Vector2(.5f, .5f);
            if (rect.pivot != middle) rect.pivot = middle;
            var sizeDelta = new Vector2(0f, -top);
            if (rect.sizeDelta != sizeDelta) rect.sizeDelta = sizeDelta;
            var position = anchoredPosition + new Vector2(0f, -top * .5f);
            if (rect.anchoredPosition != position) rect.anchoredPosition = position;
        }

        // ── Skin ──

        // Changes the skin at runtime; null restores the Prefab default. The same skin does nothing.
        public void SetSkin(SlotMachinePanelSkin skin)
        {
            skinResolved = true;
            if (skin == activeSkin) return;
            activeSkin = skin;
            ApplySkinToAll();
        }

        private void ApplyCatalogSelection()
        {
            if (skinCatalog == null)
            {
                WarnSkinCatalogOnce("no SlotMachinePanelSkinCatalog is assigned; the Prefab's default look is shown.");
                skinResolved = true;
                return;
            }
            if (!skinCatalog.Validate(out var reason)) WarnSkinCatalogOnce($"skin catalog '{skinCatalog.name}' is invalid ({reason}).");
            var selected = skinCatalog.SelectedSkin;
            if (selected == null)
            {
                WarnSkinCatalogOnce($"skin catalog '{skinCatalog.name}' has no usable selected skin; the Prefab's default look is shown.");
                skinResolved = true;
                return;
            }
            SetSkin(selected);
        }

        private void WarnSkinCatalogOnce(string message)
        {
            if (warnedSkinCatalog) return;
            warnedSkinCatalog = true;
            Debug.LogWarning($"{name}: {message}", this);
        }

        private void ApplySkinToAll()
        {
            var skin = activeSkin;
            reelMachineFrameSkin.Apply(skin != null ? skin.ReelMachineFrame : null, this);
            leftEdgeSkin.Apply(skin != null ? skin.ReelMachineEdge : null, this);
            rightEdgeSkin.Apply(skin != null ? skin.ReelMachineEdge : null, this);
            foreach (var divider in reelDividers) ApplyDividerSkin(divider);
            foreach (var view in reelViews) if (view != null) view.ApplySkin(skin);
            Machine.ApplyPaylineSkin(paylineBorderSkin, this);
            Machine.UpdatePaylineFrame();
        }

        private void ApplyDividerSkin(RectTransform divider)
        {
            if (divider == null) return;
            var graphic = divider.GetComponent<Graphic>();
            if (graphic != null) dividerSkin.ApplyTo(graphic, activeSkin != null ? activeSkin.ReelDivider : null, this);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            maximumHeight = Mathf.Max(minimumHeight, maximumHeight);
            maximumFontSize = Mathf.Max(minimumFontSize, maximumFontSize);
            hasLayout = false;
        }

        public void ConfigureForEditor(CanvasGroup targetGroup, RectTransform targetPresentation, RectTransform targetReelMachine,
            RectTransform targetReelArea, SlotReelAreaLayout targetLayout, SlotReelViewBase targetReelPrefab,
            IEnumerable<SlotReelViewBase> views, IEnumerable<RectTransform> dividers, RectTransform targetPaylineRoot,
            SlotPaylineFrameGraphic targetPaylineFrame, Image targetPaylineImage, SlotTotalCardView targetCard, TMP_Text targetStatus,
            SlotReelSpinProfile targetProfile, SlotMachinePanelSkinCatalog targetCatalog)
        {
            canvasGroup = targetGroup;
            presentationRoot = targetPresentation;
            reelMachine = targetReelMachine;
            reelArea = targetReelArea;
            reelAreaLayout = targetLayout;
            reelPrefab = targetReelPrefab;
            reelViews = new List<SlotReelViewBase>(views ?? Array.Empty<SlotReelViewBase>());
            reelDividers = new List<RectTransform>(dividers ?? Array.Empty<RectTransform>());
            paylineRoot = targetPaylineRoot;
            paylineFrame = targetPaylineFrame;
            paylineImage = targetPaylineImage;
            totalCard = targetCard;
            statusText = targetStatus;
            spinProfile = targetProfile;
            skinCatalog = targetCatalog;
            hasLayout = false;
        }

        // Records the default look of the reel skin targets. Existing defaults are kept unless overwrite is set.
        public bool RecordSkinDefaultsForEditor(Graphic machineFrame, Graphic leftEdge, Graphic rightEdge, bool overwrite)
        {
            var changed = reelMachineFrameSkin.Record(machineFrame, overwrite);
            changed |= leftEdgeSkin.Record(leftEdge, overwrite);
            changed |= rightEdgeSkin.Record(rightEdge, overwrite);
            var divider = reelDividers.Count > 0 && reelDividers[0] != null ? reelDividers[0].GetComponent<Graphic>() : null;
            changed |= dividerSkin.Record(divider, overwrite);
            changed |= paylineBorderSkin.Record(paylineFrame, overwrite);
            return changed;
        }

        // Re-records the defaults after a developer changed the Prefab's default look. Reels record theirs in
        // SlotReel.prefab with the same context menu on SlotReelView.
        [ContextMenu("기본 외형 다시 기록")]
        private void RecordSkinDefaultsFromContextMenu()
        {
            if (UnityEngine.Application.isPlaying) { Debug.LogWarning("Record the default look outside Play Mode; a skin may be applied now.", this); return; }
            UnityEditor.Undo.RecordObject(this, "기본 외형 다시 기록");
            RecordSkinDefaultsForEditor(reelMachineFrameSkin.Graphic, leftEdgeSkin.Graphic, rightEdgeSkin.Graphic, true);
            UnityEditor.EditorUtility.SetDirty(this);
        }

        // Verification only: replaces the profile settings (for example SlotReelSpinSettings.Instant).
        public void SetSpinSettingsForEditor(SlotReelSpinSettings? settings)
        {
            hasSettingsOverride = settings.HasValue;
            settingsOverride = settings ?? default;
        }

        // Verification only: Edit Mode tests lay the dock out without the Game View's safe area.
        public void SetRespectSafeAreaForEditor(bool value)
        {
            respectSafeArea = value;
            hasLayout = false;
        }

        public void SetSpinRandomSeedForEditor(int seed) => Machine.SetRandomSeed(seed);
        public void UpdatePaylineFrameForEditor() => Machine.UpdatePaylineFrame();
        public void ApplyCatalogSelectionForEditor() => ApplyCatalogSelection();

        public SlotReelViewBase ReelPrefabForEditor => reelPrefab;
        public SlotReelSpinProfile SpinProfileForEditor => spinProfile;
        public SlotMachinePanelSkinCatalog SkinCatalogForEditor => skinCatalog;
        public SlotReelAreaLayout ReelAreaLayoutForEditor => reelAreaLayout;
        public TMP_Text StatusTextForEditor => statusText;
        public IReadOnlyList<SlotSkinGraphicTarget> SkinTargetsForEditor => new[]
        {
            reelMachineFrameSkin, leftEdgeSkin, rightEdgeSkin, dividerSkin, paylineBorderSkin
        };
#endif
    }
}
