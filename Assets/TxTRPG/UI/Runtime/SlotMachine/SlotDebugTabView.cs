using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using TxTRPG.UI.Encounters;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TxTRPG.UI.SlotMachine
{
    // Display data of one reel row: the reel ID, its "Reel n" label and its Fortune Critical chance (0..1).
    public readonly struct SlotDebugReelEntry : IEquatable<SlotDebugReelEntry>
    {
        public SlotDebugReelEntry(string reelId, string label, float chance)
        {
            ReelId = reelId ?? string.Empty;
            Label = label ?? string.Empty;
            Chance = chance;
        }

        public string ReelId { get; }
        public string Label { get; }
        public float Chance { get; }

        public bool Equals(SlotDebugReelEntry other) => ReelId == other.ReelId && Label == other.Label && Chance.Equals(other.Chance);
        public override bool Equals(object obj) => obj is SlotDebugReelEntry other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(ReelId, Label, Chance);
        public override string ToString() => $"{ReelId} {Label} {Chance}";
    }

    // Slot tab of the development debug panel: reel count (−/+), a Fortune Critical chance slider per reel, the total
    // chance, spin, reset and show/hide buttons and the last result. It knows nothing about slot rules or the session: the
    // owner listens to the events and calls the Set methods, which never raise events. Chance sliders store whole percents
    // (0..100) and the events carry value / 100. Reel rows are clones of an inactive template and are reused.
    [DisallowMultipleComponent]
    public sealed class SlotDebugTabView : MonoBehaviour
    {
        public const float DefaultBottomMargin = 16f;
        public const float DefaultMinimumPageHeight = 120f;

        [Header("Reel count")]
        [SerializeField] private Button minusButton;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private Button plusButton;
        [Header("Reel rows")]
        [SerializeField] private RectTransform reelRowsRoot;
        [SerializeField, Tooltip("Inactive template row; cloned for each reel.")] private SlotDebugReelRow reelRowTemplate;
        [Header("Total")]
        [SerializeField] private Slider totalSlider;
        [SerializeField] private TMP_Text totalValue;
        [Header("Buttons")]
        [SerializeField] private Button spinButton;
        [SerializeField] private Button resetButton;
        [SerializeField] private Button showButton;
        [SerializeField] private Button hideButton;
        [SerializeField] private TMP_Text resultText;
        [Header("Page")]
        [SerializeField, Tooltip("Height of the page: the content's preferred height, limited by the space below the page.")] private LayoutElement pageLayout;
        [SerializeField] private RectTransform content;
        [SerializeField, Tooltip("Panel root that grows with the page (top anchored). The page may grow until this root's bottom edge is bottomMargin above its parent's bottom edge.")]
        private RectTransform boundsRoot;
        [SerializeField, Min(0f), Tooltip("Space kept between the panel's bottom edge and its parent's bottom edge (canvas units).")]
        private float bottomMargin = DefaultBottomMargin;
        [SerializeField, Min(0f), Tooltip("The page never becomes shorter than this (or its content) when the parent is very short; it then scrolls.")]
        private float minimumPageHeight = DefaultMinimumPageHeight;
        [SerializeField, Tooltip("Optional sibling page (the encounter page) whose last laid-out height is this page's minimum height, so this tab is never shorter than that one. Empty or never laid out: no minimum.")]
        private RectTransform heightReference;
        [SerializeField, Tooltip("Up navigation target of the first row (the selected tab button).")]
        private Selectable navigationUp;

        private readonly List<SlotDebugReelRow> rows = new();
        private readonly List<UnityEngine.Events.UnityAction<float>> rowHandlers = new();
        private int activeRowCount;
        private bool listening;
        private bool canRemove;
        private bool canAdd;
        private bool spinning;
        private bool shown = true;
        private bool sessionAvailable = true;
        // Bounds the page height was last calculated for; LateUpdate only compares them.
        private float laidOutParentHeight = -1f;
        private Vector2 laidOutBoundsPosition;
        private float laidOutReferenceHeight = -1f;
        // Set while the page is hidden or just shown: its layout is only valid once every child is enabled.
        private bool pageHeightPending = true;
        private Canvas.WillRenderCanvases beforeCanvasRender;

        public event Action<int> ReelCountChangeRequested;
        public event Action<string, float> ReelChanceChanged;
        public event Action<float> TotalChanceChanged;
        public event Action SpinRequested;
        public event Action ShowRequested;
        public event Action HideRequested;
        public event Action ResetRequested;

        public Button MinusButton => minusButton;
        public Button PlusButton => plusButton;
        public TMP_Text CountText => countText;
        public Slider TotalSlider => totalSlider;
        public TMP_Text TotalValue => totalValue;
        public Button SpinButton => spinButton;
        public Button ResetButton => resetButton;
        public Button ShowButton => showButton;
        public Button HideButton => hideButton;
        public TMP_Text ResultText => resultText;
        public LayoutElement PageLayout => pageLayout;
        public SlotDebugReelRow ReelRowTemplate => reelRowTemplate;
        public int ActiveRowCount => activeRowCount;
        public RectTransform BoundsRoot => boundsRoot;
        public float BottomMargin => bottomMargin;
        public RectTransform HeightReference => heightReference;
        public bool IsSpinning => spinning;
        public bool IsSessionAvailable => sessionAvailable;

        public SlotDebugReelRow GetRow(int index) => index >= 0 && index < activeRowCount ? rows[index] : null;

        public bool IsValid(out string missing)
        {
            missing = minusButton == null ? nameof(minusButton) : countText == null ? nameof(countText) : plusButton == null ? nameof(plusButton)
                : reelRowsRoot == null ? nameof(reelRowsRoot) : reelRowTemplate == null || !reelRowTemplate.IsValid ? nameof(reelRowTemplate)
                : totalSlider == null ? nameof(totalSlider) : totalValue == null ? nameof(totalValue) : spinButton == null ? nameof(spinButton)
                : resetButton == null ? nameof(resetButton) : showButton == null ? nameof(showButton) : hideButton == null ? nameof(hideButton)
                : resultText == null ? nameof(resultText) : pageLayout == null ? nameof(pageLayout) : content == null ? nameof(content) : boundsRoot == null ? nameof(boundsRoot) : string.Empty;
            return missing.Length == 0;
        }

        public static string FormatChance(float chance) => EncounterDebugPanel.FormatChance(chance);

        private void Awake() => Listen();

        // Unity enables this page before its children, so their layout is not available yet: the height is calculated
        // later in this frame, before the canvases are laid out and drawn. LateUpdate alone is not enough because Unity
        // skips it on the first activation until Start has run, which would draw one frame at the stale height.
        private void OnEnable()
        {
            pageHeightPending = true;
            beforeCanvasRender ??= RefreshPendingPageHeight;
            Canvas.preWillRenderCanvases += beforeCanvasRender;
        }

        private void OnDisable()
        {
            if (beforeCanvasRender != null) Canvas.preWillRenderCanvases -= beforeCanvasRender;
        }

        // Outside the canvas layout loop, so the page's LayoutElement may still change and is laid out in this frame.
        private void RefreshPendingPageHeight()
        {
            if (pageHeightPending) RefreshPageHeight();
        }

        // The page height follows the space below it; it is recalculated only when it is pending (shown, or changed while
        // hidden) or when the parent height, the panel position or the reference height changed.
        private void LateUpdate()
        {
            if (!pageHeightPending)
            {
                if (boundsRoot == null || !(boundsRoot.parent is RectTransform parent)) return;
                if (Mathf.Approximately(parent.rect.height, laidOutParentHeight) && boundsRoot.anchoredPosition == laidOutBoundsPosition &&
                    Mathf.Approximately(ReferenceHeight(), laidOutReferenceHeight)) return;
            }
            RefreshPageHeight();
        }

        private void OnDestroy()
        {
            if (!listening) return;
            if (minusButton != null) minusButton.onClick.RemoveListener(OnMinus);
            if (plusButton != null) plusButton.onClick.RemoveListener(OnPlus);
            if (totalSlider != null) totalSlider.onValueChanged.RemoveListener(OnTotalSlider);
            if (spinButton != null) spinButton.onClick.RemoveListener(OnSpin);
            if (resetButton != null) resetButton.onClick.RemoveListener(OnReset);
            if (showButton != null) showButton.onClick.RemoveListener(OnShow);
            if (hideButton != null) hideButton.onClick.RemoveListener(OnHide);
            for (var i = 0; i < rows.Count && i < rowHandlers.Count; i++)
                if (rows[i] != null && rows[i].Slider != null) rows[i].Slider.onValueChanged.RemoveListener(rowHandlers[i]);
            listening = false;
        }

        // Shows the reels without raising events. The same ID list only updates the values; otherwise rows are activated,
        // reused or deactivated and the page height and navigation are recalculated.
        public void SetReels(IReadOnlyList<SlotDebugReelEntry> reels, bool canRemoveReel, bool canAddReel)
        {
            if (reels == null) throw new ArgumentNullException(nameof(reels));
            Listen();
            canRemove = canRemoveReel;
            canAdd = canAddReel;
            var sameIds = reels.Count == activeRowCount;
            for (var i = 0; sameIds && i < reels.Count; i++) sameIds = rows[i].ReelId == reels[i].ReelId;
            if (!sameIds) LayoutRows(reels.Count);
            for (var i = 0; i < reels.Count; i++)
            {
                var row = rows[i];
                row.ReelId = reels[i].ReelId;
                SetText(row.Label, reels[i].Label);
                SetChance(row.Slider, row.Value, reels[i].Chance);
            }
            SetText(countText, reels.Count.ToString(CultureInfo.InvariantCulture));
            if (!sameIds)
            {
                RefreshNavigation();
                RefreshPageHeight();
            }
            RefreshInteractable();
        }

        public void SetTotalChance(float chance)
        {
            Listen();
            SetChance(totalSlider, totalValue, chance);
        }

        // Shown state of the dock: the button for the current state is disabled.
        public void SetShown(bool isShown)
        {
            Listen();
            shown = isShown;
            RefreshInteractable();
        }

        // While spinning: spin, −, + and reset are disabled; sliders and show/hide stay usable.
        public void SetSpinning(bool isSpinning)
        {
            Listen();
            spinning = isSpinning;
            RefreshInteractable();
        }

        // Without a session only show/hide are usable.
        public void SetSessionAvailable(bool available)
        {
            Listen();
            sessionAvailable = available;
            RefreshInteractable();
        }

        public void ShowResult(string message)
        {
            Listen();
            SetText(resultText, message ?? string.Empty);
        }

        // Recalculates the page height: the content's preferred height, limited to the space below the page so the panel's
        // bottom edge stays bottomMargin above its parent's bottom edge (never below minimumPageHeight), and never shorter
        // than the reference page (that minimum wins over the bottom margin). With enough vertical space every control is
        // shown and a shorter content leaves empty space below it; otherwise the page scrolls. Runs when the row count
        // changes, when the page is shown and when the parent height, panel position or reference height changed. A
        // hidden page has no layout: its height is kept and only marked for recalculation.
        public void RefreshPageHeight()
        {
            if (pageLayout == null || content == null) return;
            if (!gameObject.activeInHierarchy) { pageHeightPending = true; return; }
            // The panel is laid out first so the content has its final width (text heights depend on it).
            if (boundsRoot != null) LayoutRebuilder.ForceRebuildLayoutImmediate(boundsRoot);
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            var preferred = LayoutUtility.GetPreferredHeight(content);
            var limited = Mathf.Max(Mathf.Min(preferred, AvailablePageHeight()), Mathf.Min(preferred, minimumPageHeight));
            var reference = ReferenceHeight();
            var height = Mathf.Max(limited, reference);
            if (!Mathf.Approximately(pageLayout.preferredHeight, height)) pageLayout.preferredHeight = height;
            if (!Mathf.Approximately(pageLayout.minHeight, height)) pageLayout.minHeight = height;
            pageHeightPending = false;
            laidOutReferenceHeight = reference;
            if (boundsRoot != null && boundsRoot.parent is RectTransform parent)
            {
                laidOutParentHeight = parent.rect.height;
                laidOutBoundsPosition = boundsRoot.anchoredPosition;
            }
        }

        // Largest page height that keeps boundsRoot's bottom edge bottomMargin above its parent's bottom edge. The space
        // between the page's and the panel's bottom edges (layout padding) does not depend on the page height.
        public float AvailablePageHeight()
        {
            if (boundsRoot == null || !(boundsRoot.parent is RectTransform parent) || !gameObject.activeInHierarchy) return float.PositiveInfinity;
            LayoutRebuilder.ForceRebuildLayoutImmediate(boundsRoot);
            var page = (RectTransform)pageLayout.transform;
            var pageBounds = BoundsIn(parent, page);
            var panelBounds = BoundsIn(parent, boundsRoot);
            var below = pageBounds.yMin - panelBounds.yMin;
            return Mathf.Max(0f, pageBounds.yMax - (parent.rect.yMin + bottomMargin) - below);
        }

        // Last laid-out height of the reference page (a hidden RectTransform keeps its last size). Layout-driven sizes are
        // not saved, so a reference that was never laid out reads 0, which means "unknown" and adds no minimum.
        public float ReferenceHeight()
        {
            if (heightReference == null || heightReference == transform) return 0f;
            var height = heightReference.rect.height;
            return float.IsNaN(height) || height < 0f ? 0f : height;
        }

        private static readonly Vector3[] CornerBuffer = new Vector3[4];

        private static Rect BoundsIn(RectTransform space, RectTransform rect)
        {
            rect.GetWorldCorners(CornerBuffer);
            var min = space.InverseTransformPoint(CornerBuffer[0]);
            var max = space.InverseTransformPoint(CornerBuffer[2]);
            return Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
        }

        private void LayoutRows(int count)
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            var lostFocus = false;
            while (rows.Count < count) AddRow();
            for (var i = 0; i < rows.Count; i++)
            {
                var active = i < count;
                if (!active && selected != null && selected.transform.IsChildOf(rows[i].transform)) lostFocus = true;
                if (rows[i].gameObject.activeSelf != active) rows[i].gameObject.SetActive(active);
                if (!active) rows[i].ReelId = string.Empty;
            }
            activeRowCount = count;
            if (lostFocus && minusButton != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(minusButton.gameObject);
        }

        private void AddRow()
        {
            if (reelRowTemplate == null || reelRowsRoot == null) throw new InvalidOperationException($"{name}: the reel row template is missing.");
            var row = Instantiate(reelRowTemplate, reelRowsRoot, false);
            row.name = $"ReelRow{rows.Count + 1}";
            var index = rows.Count;
            UnityEngine.Events.UnityAction<float> handler = value => OnRowSlider(index, value);
            row.Slider.onValueChanged.AddListener(handler);
            rows.Add(row);
            rowHandlers.Add(handler);
        }

        private void Listen()
        {
            if (listening) return;
            listening = true;
            if (reelRowTemplate != null && reelRowTemplate.gameObject.activeSelf) reelRowTemplate.gameObject.SetActive(false);
            if (minusButton != null) minusButton.onClick.AddListener(OnMinus);
            if (plusButton != null) plusButton.onClick.AddListener(OnPlus);
            if (totalSlider != null) totalSlider.onValueChanged.AddListener(OnTotalSlider);
            if (spinButton != null) spinButton.onClick.AddListener(OnSpin);
            if (resetButton != null) resetButton.onClick.AddListener(OnReset);
            if (showButton != null) showButton.onClick.AddListener(OnShow);
            if (hideButton != null) hideButton.onClick.AddListener(OnHide);
            RefreshNavigation();
            RefreshInteractable();
        }

        private void OnMinus() { if (sessionAvailable && !spinning && canRemove) ReelCountChangeRequested?.Invoke(-1); }
        private void OnPlus() { if (sessionAvailable && !spinning && canAdd) ReelCountChangeRequested?.Invoke(1); }
        private void OnSpin() { if (sessionAvailable && !spinning) SpinRequested?.Invoke(); }
        private void OnReset() { if (sessionAvailable && !spinning) ResetRequested?.Invoke(); }
        private void OnShow() => ShowRequested?.Invoke();
        private void OnHide() => HideRequested?.Invoke();

        private void OnRowSlider(int index, float value)
        {
            if (index >= activeRowCount) return;
            var row = rows[index];
            SetText(row.Value, FormatChance(value / 100f));
            ReelChanceChanged?.Invoke(row.ReelId, value / 100f);
        }

        private void OnTotalSlider(float value)
        {
            SetText(totalValue, FormatChance(value / 100f));
            TotalChanceChanged?.Invoke(value / 100f);
        }

        private void RefreshInteractable()
        {
            SetInteractable(minusButton, sessionAvailable && !spinning && canRemove);
            SetInteractable(plusButton, sessionAvailable && !spinning && canAdd);
            SetInteractable(spinButton, sessionAvailable && !spinning);
            SetInteractable(resetButton, sessionAvailable && !spinning);
            SetInteractable(totalSlider, sessionAvailable);
            for (var i = 0; i < activeRowCount; i++) SetInteractable(rows[i].Slider, sessionAvailable);
            SetInteractable(showButton, !shown);
            SetInteractable(hideButton, shown);
        }

        // Explicit vertical chain: tab → −/+ → reel rows → total → spin/reset → show/hide.
        private void RefreshNavigation()
        {
            var column = new List<Selectable>();
            for (var i = 0; i < activeRowCount; i++) column.Add(rows[i].Slider);
            column.Add(totalSlider);
            var firstRow = column.Count > 0 ? column[0] : null;
            var lastRow = totalSlider;

            SetNavigation(minusButton, null, plusButton, navigationUp, firstRow);
            SetNavigation(plusButton, minusButton, null, navigationUp, firstRow);
            for (var i = 0; i < column.Count; i++)
            {
                var up = i == 0 ? (Selectable)minusButton : column[i - 1];
                var down = i + 1 < column.Count ? column[i + 1] : spinButton;
                SetNavigation(column[i], null, null, up, down);
            }
            SetNavigation(spinButton, null, resetButton, lastRow, showButton);
            SetNavigation(resetButton, spinButton, null, lastRow, hideButton);
            SetNavigation(showButton, null, hideButton, spinButton, null);
            SetNavigation(hideButton, showButton, null, resetButton, null);
        }

        private static void SetNavigation(Selectable target, Selectable left, Selectable right, Selectable up, Selectable down)
        {
            if (target == null) return;
            target.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnLeft = left,
                selectOnRight = right,
                selectOnUp = up,
                selectOnDown = down
            };
        }

        private static void SetChance(Slider slider, TMP_Text text, float chance)
        {
            // A value that is not a whole percent is shown rounded; the session value only changes when the user moves it.
            var percent = Mathf.Round(Mathf.Clamp01(float.IsNaN(chance) ? 0f : chance) * 100f);
            if (slider != null) slider.SetValueWithoutNotify(percent);
            SetText(text, FormatChance(percent / 100f));
        }

        private static void SetInteractable(Selectable target, bool value) { if (target != null && target.interactable != value) target.interactable = value; }
        private static void SetText(TMP_Text text, string value) { if (text != null && text.text != value) text.text = value; }

#if UNITY_EDITOR
        public void ConfigureForEditor(Button minus, TMP_Text count, Button plus, RectTransform rowsRoot, SlotDebugReelRow template,
            Slider total, TMP_Text totalText, Button spin, Button reset, Button show, Button hide, TMP_Text result,
            LayoutElement page, RectTransform pageContent, Selectable up)
        {
            minusButton = minus; countText = count; plusButton = plus; reelRowsRoot = rowsRoot; reelRowTemplate = template;
            totalSlider = total; totalValue = totalText; spinButton = spin; resetButton = reset; showButton = show; hideButton = hide;
            resultText = result; pageLayout = page; content = pageContent; navigationUp = up;
        }

        public Selectable NavigationUpForEditor => navigationUp;

        public void SetPageBoundsForEditor(RectTransform root, float margin, float minimumHeight)
        {
            boundsRoot = root;
            bottomMargin = Mathf.Max(0f, margin);
            minimumPageHeight = Mathf.Max(0f, minimumHeight);
            laidOutParentHeight = -1f;
        }

        public void SetHeightReferenceForEditor(RectTransform reference)
        {
            heightReference = reference;
            laidOutReferenceHeight = -1f;
        }
#endif
    }
}
