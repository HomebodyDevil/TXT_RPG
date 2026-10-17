using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.SlotMachine
{
    // Shows the slot machine reels with their dividers and payline frame, and the summary area (total box and rune line
    // table). It receives display data only and never decides results: PlaySpinAsync presents an already accepted spin
    // with the timing planned from the SlotReelSpinProfile. The panel takes no input; every graphic has raycastTarget
    // off. Reels are SlotReelViewBase instances, so the reel implementation can be replaced.
    // The look comes from a SlotMachinePanelSkin chosen from the skin catalog in Play Mode (SetSkin swaps it at runtime);
    // every skin target keeps its recorded Prefab default, which an empty skin entry or a null skin restores.
    // Reel, divider, payline and spin handling is delegated to SlotReelMachineCore (shared with SlotReelDock); the panel
    // keeps its serialized fields, the summary area and the skin targets.
    [DisallowMultipleComponent]
    public sealed class SlotMachinePanel : MonoBehaviour, ISlotMachinePresenter, ISlotReelMachineHost
    {
        [SerializeField] private RectTransform reelArea;
        [SerializeField, Tooltip("Raises LayoutApplied after the reel area is laid out; the payline frame follows it.")]
        private SlotReelAreaLayout reelAreaLayout;
        [SerializeField, Tooltip("Reel added when the configuration has more reels than the panel shows.")]
        private SlotReelViewBase reelPrefab;
        [SerializeField] private List<SlotReelViewBase> reelViews = new();
        [SerializeField, Tooltip("Dividers between reels; the first one is the template for added dividers.")]
        private List<RectTransform> reelDividers = new();
        [SerializeField, Tooltip("Layout-only payline root (PaylineFrame), placed on the payline cell range of every reel window.")]
        private RectTransform paylineRoot;
        [SerializeField, Tooltip("Code-drawn payline border (PaylineFrame/ProceduralBorder), shown when the skin has no payline image.")]
        private SlotPaylineFrameGraphic paylineFrame;
        [SerializeField, Tooltip("Payline image (PaylineFrame/SkinImage), shown when the skin has a payline sprite.")]
        private Image paylineImage;
        [SerializeField, Tooltip("Hidden while a configuration error is shown.")] private GameObject contentRoot;
        [SerializeField] private TMP_Text totalLabelText;
        [SerializeField] private TMP_Text totalValueText;
        [SerializeField, Tooltip("Layout-only root (TotalBox/TotalMultiplier) of the total Fortune Critical multiplier. Inactive while none is shown.")]
        private RectTransform totalMultiplierRoot;
        [SerializeField, Tooltip("TotalMultiplier/VisualRoot/Text. Effects may change VisualRoot only.")]
        private TMP_Text totalMultiplierText;
        [SerializeField, Tooltip("Places TotalMultiplier beside or above the total value glyphs (on TotalBox).")]
        private SlotTotalMultiplierLayout totalMultiplierLayout;
        [SerializeField] private TMP_Text runeColumnTitleText;
        [SerializeField] private TMP_Text valueColumnTitleText;
        [SerializeField] private SlotRuneTableView runeTable;
        [SerializeField] private TMP_Text statusText;
        [SerializeField, Tooltip("Spin timing, easing and high-speed effect. Without it the built-in defaults are used.")]
        private SlotReelSpinProfile spinProfile;
        [SerializeField, Min(1f)] private float minimumFontSize = 16f;
        [SerializeField, Min(1f)] private float maximumFontSize = 64f;

        [Header("Skin")]
        [SerializeField, Tooltip("Skins and the selected skin applied in Play Mode. Without it the Prefab's default look is shown.")]
        private SlotMachinePanelSkinCatalog skinCatalog;
        [SerializeField] private SlotSkinGraphicTarget panelBackgroundSkin = new();
        [SerializeField] private SlotSkinGraphicTarget reelMachineFrameSkin = new();
        [SerializeField] private SlotSkinGraphicTarget leftEdgeSkin = new();
        [SerializeField] private SlotSkinGraphicTarget rightEdgeSkin = new();
        [SerializeField, Tooltip("The first divider; its defaults are used for every divider, including added ones.")]
        private SlotSkinGraphicTarget dividerSkin = new();
        [SerializeField] private SlotSkinGraphicTarget summaryBackgroundSkin = new();
        [SerializeField] private SlotSkinGraphicTarget totalBoxBackgroundSkin = new();
        [SerializeField, Tooltip("Color of the code-drawn payline border.")]
        private SlotSkinGraphicTarget paylineBorderSkin = new();
        [SerializeField] private SlotSkinTextTarget totalLabelSkin = new();
        [SerializeField] private SlotSkinTextTarget totalValueSkin = new();
        [SerializeField] private SlotSkinTextTarget runeColumnTitleSkin = new();
        [SerializeField] private SlotSkinTextTarget valueColumnTitleSkin = new();
        [SerializeField, Tooltip("Total Fortune Critical multiplier; a skin may override its color with Fortune Text Color.")]
        private SlotSkinTextTarget totalMultiplierSkin = new();

        private SlotReelMachineCore machine;
        private bool warnedMissingProfile;
        private bool hasSettingsOverride;
        private SlotReelSpinSettings settingsOverride;
        private SlotMachinePanelSkin activeSkin;
        private bool skinResolved;
        private bool warnedSkinCatalog;

        // Raised once after the skin actually changed.
        public event Action<SlotMachinePanel> SkinChanged;

        public bool IsSpinning => Machine.IsSpinning;
        public int ReelCount => reelViews.Count;
        public IReadOnlyList<SlotReelViewBase> ReelViews => reelViews.AsReadOnly();
        public int ActiveDividerCount => Machine.ActiveDividerCount;
        public string TotalLabelText => totalLabelText != null ? totalLabelText.text : string.Empty;
        public string TotalValueText => totalValueText != null ? totalValueText.text : string.Empty;
        // Total Fortune Critical multiplier currently shown; empty when hidden.
        public string TotalMultiplierText => totalMultiplierRoot != null && totalMultiplierRoot.gameObject.activeSelf && totalMultiplierText != null
            ? totalMultiplierText.text : string.Empty;
        public string RuneColumnTitleText => runeColumnTitleText != null ? runeColumnTitleText.text : string.Empty;
        public string ValueColumnTitleText => valueColumnTitleText != null ? valueColumnTitleText.text : string.Empty;
        // (effect, value) per shown table row; message rows ("장착된 룬이 없습니다", "외 N줄") report the message as the effect.
        public IReadOnlyList<(string Effect, string Value)> RuneLineTexts =>
            runeTable != null ? runeTable.ShownRowTexts : Array.Empty<(string, string)>();
        public string StatusText => statusText != null && statusText.gameObject.activeSelf ? statusText.text : string.Empty;
        // Payline cell range across the reel windows (layout root, without the border or image expansion).
        public RectTransform PaylineFrameRect => paylineRoot != null ? paylineRoot : paylineFrame != null ? paylineFrame.rectTransform : null;
        // The payline graphic currently shown (code border or skin image), including its outward expansion.
        public RectTransform PaylineVisibleRect => PaylineImageShown ? paylineImage.rectTransform : paylineFrame != null ? paylineFrame.rectTransform : null;
        public bool PaylineImageShown => paylineImage != null && paylineImage.enabled;
        public bool PaylineBorderShown => paylineFrame != null && paylineFrame.enabled;
        public SlotMachinePanelSkin ActiveSkin => activeSkin;
        public string ActiveSkinId => activeSkin != null ? activeSkin.SkinId : string.Empty;
        public SlotMachinePanelSkinCatalog SkinCatalog => skinCatalog;
        public float MaximumPresentationSeconds => GetMaximumPresentationSeconds(Mathf.Max(1, reelViews.Count));

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

        private SlotReelMachineCore Machine => machine ??= new SlotReelMachineCore(this);

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
            // Skins are applied in Play Mode and builds only, so Edit Mode shows (and saves) the Prefab default.
            if (UnityEngine.Application.isPlaying && !skinResolved) ApplyCatalogSelection();
        }

        // Changes the skin at runtime; null restores the Prefab default. The same skin does nothing. A running spin
        // keeps its result, timing and positions: only colors, sprites and the payline expansion change.
        public void SetSkin(SlotMachinePanelSkin skin)
        {
            skinResolved = true;
            if (skin == activeSkin) return;
            activeSkin = skin;
            ApplySkinToAll();
            SkinChanged?.Invoke(this);
        }

        // Looks the ID up in the catalog. On failure the current skin is kept.
        public bool TrySetSkin(string skinId, out string reason)
        {
            if (skinCatalog == null) { reason = "no skin catalog is assigned"; return false; }
            if (!skinCatalog.TryGet(skinId, out var skin)) { reason = $"skin '{skinId}' is not registered in {skinCatalog.name}"; return false; }
            reason = string.Empty;
            SetSkin(skin);
            return true;
        }

        // Applies the catalog's developer selection when nothing chose a skin before. A missing or invalid catalog never
        // stops the panel: it warns once and keeps the default look.
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
            panelBackgroundSkin.Apply(skin != null ? skin.PanelBackground : null, this);
            reelMachineFrameSkin.Apply(skin != null ? skin.ReelMachineFrame : null, this);
            leftEdgeSkin.Apply(skin != null ? skin.ReelMachineEdge : null, this);
            rightEdgeSkin.Apply(skin != null ? skin.ReelMachineEdge : null, this);
            summaryBackgroundSkin.Apply(skin != null ? skin.SummaryBackground : null, this);
            totalBoxBackgroundSkin.Apply(skin != null ? skin.TotalBoxBackground : null, this);
            var dark = skin != null ? skin.DarkSurfaceTextColor : null;
            totalLabelSkin.Apply(dark);
            totalValueSkin.Apply(dark);
            var light = skin != null ? skin.LightSurfaceTextColor : null;
            runeColumnTitleSkin.Apply(light);
            valueColumnTitleSkin.Apply(light);
            totalMultiplierSkin.Apply(skin != null ? skin.FortuneTextColor : null);
            foreach (var divider in reelDividers) ApplyDividerSkin(divider);
            foreach (var view in reelViews) if (view != null) view.ApplySkin(skin);
            if (runeTable != null) runeTable.ApplySkin(skin);
            ApplyPaylineSkin();
            UpdatePaylineFrame();
        }

        private void ApplyDividerSkin(RectTransform divider)
        {
            if (divider == null) return;
            var graphic = divider.GetComponent<Graphic>();
            if (graphic != null) dividerSkin.ApplyTo(graphic, activeSkin != null ? activeSkin.ReelDivider : null, this);
        }

        private void ApplyPaylineSkin() => Machine.ApplyPaylineSkin(paylineBorderSkin, this);

        private void OnEnable() => Machine.Enable(reelAreaLayout);

        // Applies a reel configuration. While a spin is playing it is deferred until the spin ends, because the running
        // presentation always finishes with the accepted spin's strip copies. Reels whose ID and strip are unchanged keep
        // the cell they show; others show their first cell on the payline.
        public void SetReels(IReadOnlyList<SlotReelDisplay> reels) => Machine.SetReels(reels);

        // Total box and rune line table. Every string comes from the display data.
        public void SetSummary(SlotSummaryDisplay summary)
        {
            if (summary == null) throw new ArgumentNullException(nameof(summary));
            SetText(totalLabelText, summary.TotalLabel);
            SetText(totalValueText, summary.TotalValue);
            SetTotalMultiplier(summary.TotalMultiplierText);
            SetText(runeColumnTitleText, summary.RuneColumnTitle);
            SetText(valueColumnTitleText, summary.ValueColumnTitle);
            if (runeTable != null) runeTable.SetLines(summary.Lines, summary.EmptyText, summary.OverflowFormat);
        }

        // Configuration error: the reels and summary are hidden and the reason is shown.
        public void ShowError(string message)
        {
            if (contentRoot != null) contentRoot.SetActive(false);
            SetStatus(message);
        }

        public void ClearError()
        {
            if (contentRoot != null) contentRoot.SetActive(true);
            SetStatus(string.Empty);
        }

        // Shows result.SpinningSummary, plays every reel and shows result.ResultSummary once all reels completed.
        public Task<bool> PlaySpinAsync(SlotSpinDisplay result, bool reduceMotion, CancellationToken cancellationToken) =>
            Machine.PlaySpinAsync(result, reduceMotion, cancellationToken);

        // Puts every reel at rest on the accepted result immediately (timeout, cancellation or a restored result).
        public void StopAtResult(SlotSpinDisplay result) => Machine.StopAtResult(result);

        private void OnDisable() => Machine.Disable();

        private void SetStatus(string message)
        {
            if (statusText == null) return;
            statusText.text = message ?? string.Empty;
            statusText.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }

        private void SetTotalMultiplier(string value)
        {
            value ??= string.Empty;
            SetText(totalMultiplierText, value);
            if (totalMultiplierRoot != null && totalMultiplierRoot.gameObject.activeSelf != value.Length > 0)
                totalMultiplierRoot.gameObject.SetActive(value.Length > 0);
            // The value text was set just before, so the placement follows the current value glyphs.
            if (value.Length > 0 && totalMultiplierLayout != null) totalMultiplierLayout.Refresh();
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text == null) return;
            value ??= string.Empty;
            if (text.text != value) text.text = value;
        }

        private void UpdatePaylineFrame() => Machine.UpdatePaylineFrame();

#if UNITY_EDITOR
        public void ConfigureForEditor(RectTransform targetReelArea, SlotReelAreaLayout targetReelAreaLayout,
            SlotReelViewBase targetReelPrefab, IEnumerable<SlotReelViewBase> views, IEnumerable<RectTransform> dividers,
            SlotPaylineFrameGraphic targetPaylineFrame, GameObject targetContent, TMP_Text targetTotalLabel,
            TMP_Text targetTotalValue, TMP_Text targetRuneColumnTitle, TMP_Text targetValueColumnTitle,
            SlotRuneTableView targetRuneTable, TMP_Text targetStatus, SlotReelSpinProfile targetProfile)
        {
            reelArea = targetReelArea;
            reelAreaLayout = targetReelAreaLayout;
            reelPrefab = targetReelPrefab;
            reelViews = new List<SlotReelViewBase>(views ?? Array.Empty<SlotReelViewBase>());
            reelDividers = new List<RectTransform>(dividers ?? Array.Empty<RectTransform>());
            paylineFrame = targetPaylineFrame;
            contentRoot = targetContent;
            totalLabelText = targetTotalLabel;
            totalValueText = targetTotalValue;
            runeColumnTitleText = targetRuneColumnTitle;
            valueColumnTitleText = targetValueColumnTitle;
            runeTable = targetRuneTable;
            statusText = targetStatus;
            spinProfile = targetProfile;
        }

        // Verification only: replaces the profile settings (for example SlotReelSpinSettings.Instant, so a spin ends
        // without waiting for frames). Pass null to use the profile again.
        public void SetSpinSettingsForEditor(SlotReelSpinSettings? settings)
        {
            hasSettingsOverride = settings.HasValue;
            settingsOverride = settings ?? default;
        }

        // Verification only: makes the per-reel jitter reproducible.
        public void SetSpinRandomSeedForEditor(int seed) => Machine.SetRandomSeed(seed);

        // Verification only: places the payline frame immediately (Edit Mode has no layout callbacks).
        public void UpdatePaylineFrameForEditor() => UpdatePaylineFrame();

        // Connects the payline children and the catalog and records the default look of every panel-owned skin target.
        // Existing defaults are kept unless overwrite is set. Returns true when something changed.
        public bool ConfigureSkinForEditor(SlotMachinePanelSkinCatalog targetCatalog, RectTransform targetPaylineRoot,
            SlotPaylineFrameGraphic targetPaylineBorder, Image targetPaylineImage, Graphic targetBackground, Graphic targetMachineFrame,
            Graphic targetLeftEdge, Graphic targetRightEdge, Graphic targetSummaryBackground, Graphic targetTotalBoxBackground, bool overwrite)
        {
            var changed = skinCatalog != targetCatalog || paylineRoot != targetPaylineRoot || paylineFrame != targetPaylineBorder || paylineImage != targetPaylineImage;
            skinCatalog = targetCatalog;
            paylineRoot = targetPaylineRoot;
            paylineFrame = targetPaylineBorder;
            paylineImage = targetPaylineImage;
            changed |= RecordSkinDefaults(targetBackground, targetMachineFrame, targetLeftEdge, targetRightEdge, targetSummaryBackground,
                targetTotalBoxBackground, overwrite);
            return changed;
        }

        private bool RecordSkinDefaults(Graphic background, Graphic machineFrame, Graphic leftEdge, Graphic rightEdge,
            Graphic summaryBackground, Graphic totalBoxBackground, bool overwrite)
        {
            var changed = panelBackgroundSkin.Record(background, overwrite);
            changed |= reelMachineFrameSkin.Record(machineFrame, overwrite);
            changed |= leftEdgeSkin.Record(leftEdge, overwrite);
            changed |= rightEdgeSkin.Record(rightEdge, overwrite);
            changed |= summaryBackgroundSkin.Record(summaryBackground, overwrite);
            changed |= totalBoxBackgroundSkin.Record(totalBoxBackground, overwrite);
            var divider = reelDividers.Count > 0 && reelDividers[0] != null ? reelDividers[0].GetComponent<Graphic>() : null;
            changed |= dividerSkin.Record(divider, overwrite);
            changed |= paylineBorderSkin.Record(paylineFrame, overwrite);
            changed |= totalLabelSkin.Record(totalLabelText, overwrite);
            changed |= totalValueSkin.Record(totalValueText, overwrite);
            changed |= runeColumnTitleSkin.Record(runeColumnTitleText, overwrite);
            changed |= valueColumnTitleSkin.Record(valueColumnTitleText, overwrite);
            changed |= totalMultiplierSkin.Record(totalMultiplierText, overwrite);
            return changed;
        }

        // Connects the total Fortune Critical multiplier and records its default text color once. True when something changed.
        public bool ConfigureFortuneForEditor(RectTransform targetRoot, TMP_Text targetText, SlotTotalMultiplierLayout targetLayout)
        {
            var changed = totalMultiplierRoot != targetRoot || totalMultiplierText != targetText || totalMultiplierLayout != targetLayout;
            totalMultiplierRoot = targetRoot;
            totalMultiplierText = targetText;
            totalMultiplierLayout = targetLayout;
            changed |= totalMultiplierSkin.Record(targetText, false);
            return changed;
        }

        public RectTransform TotalMultiplierRootForEditor => totalMultiplierRoot;
        public TMP_Text TotalMultiplierTextForEditor => totalMultiplierText;
        public SlotTotalMultiplierLayout TotalMultiplierLayoutForEditor => totalMultiplierLayout;
        public SlotSkinTextTarget TotalMultiplierSkinForEditor => totalMultiplierSkin;

        // Re-records the defaults after a developer changed the Prefab's default look (panel, rune table and row
        // template). Reels record theirs in SlotReel.prefab with the same context menu on SlotReelView.
        [ContextMenu("기본 외형 다시 기록")]
        private void RecordSkinDefaultsFromContextMenu()
        {
            if (UnityEngine.Application.isPlaying) { Debug.LogWarning("Record the default look outside Play Mode; a skin may be applied now.", this); return; }
            UnityEditor.Undo.RecordObject(this, "기본 외형 다시 기록");
            RecordSkinDefaults(panelBackgroundSkin.Graphic, reelMachineFrameSkin.Graphic, leftEdgeSkin.Graphic, rightEdgeSkin.Graphic,
                summaryBackgroundSkin.Graphic, totalBoxBackgroundSkin.Graphic, true);
            UnityEditor.EditorUtility.SetDirty(this);
            if (runeTable == null) return;
            UnityEditor.Undo.RecordObject(runeTable, "기본 외형 다시 기록");
            if (runeTable.RowTemplateForEditor != null) UnityEditor.Undo.RecordObject(runeTable.RowTemplateForEditor, "기본 외형 다시 기록");
            runeTable.ConfigureSkinForEditor(runeTable.BackgroundSkinForEditor.Graphic, true);
            UnityEditor.EditorUtility.SetDirty(runeTable);
            if (runeTable.RowTemplateForEditor != null) UnityEditor.EditorUtility.SetDirty(runeTable.RowTemplateForEditor);
        }

        // Verification only: applies the catalog selection as Awake does in Play Mode.
        public void ApplyCatalogSelectionForEditor() => ApplyCatalogSelection();

        public IReadOnlyList<SlotSkinGraphicTarget> PanelSkinTargetsForEditor => new[]
        {
            panelBackgroundSkin, reelMachineFrameSkin, leftEdgeSkin, rightEdgeSkin, dividerSkin, summaryBackgroundSkin, totalBoxBackgroundSkin
        };
        public IReadOnlyList<SlotSkinTextTarget> PanelTextTargetsForEditor => new[] { totalLabelSkin, totalValueSkin, runeColumnTitleSkin, valueColumnTitleSkin };
        public SlotSkinGraphicTarget DividerSkinForEditor => dividerSkin;
        public SlotSkinGraphicTarget PaylineBorderSkinForEditor => paylineBorderSkin;
        public IReadOnlyList<RectTransform> ReelDividersForEditor => reelDividers;
        public SlotPaylineFrameGraphic PaylineBorderForEditor => paylineFrame;
        public Image PaylineImageForEditor => paylineImage;
        public RectTransform ReelAreaForEditor => reelArea;
        public SlotReelViewBase ReelPrefabForEditor => reelPrefab;
        public SlotReelSpinProfile SpinProfileForEditor => spinProfile;
        public SlotRuneTableView RuneTableForEditor => runeTable;
#endif
    }
}
