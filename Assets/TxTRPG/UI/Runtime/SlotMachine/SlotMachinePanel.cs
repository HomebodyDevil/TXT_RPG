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
    [DisallowMultipleComponent]
    public sealed class SlotMachinePanel : MonoBehaviour
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

        private readonly Vector3[] firstCorners = new Vector3[4];
        private readonly Vector3[] lastCorners = new Vector3[4];
        private int generation;
        private IReadOnlyList<SlotReelDisplay> pendingReels;
        private System.Random spinRandom;
        private bool warnedMissingProfile;
        private bool hasSettingsOverride;
        private SlotReelSpinSettings settingsOverride;
        private SlotMachinePanelSkin activeSkin;
        private bool skinResolved;
        private bool warnedSkinCatalog;
        private float appliedBorderOutset = float.NaN;
        private Vector4 appliedImageOutset = new(float.NaN, 0f, 0f, 0f);

        // Raised once after the skin actually changed.
        public event Action<SlotMachinePanel> SkinChanged;

        public bool IsSpinning { get; private set; }
        public int ReelCount => reelViews.Count;
        public IReadOnlyList<SlotReelViewBase> ReelViews => reelViews.AsReadOnly();
        public int ActiveDividerCount
        {
            get
            {
                var count = 0;
                foreach (var divider in reelDividers) if (divider != null && divider.gameObject.activeSelf) count++;
                return count;
            }
        }
        public string TotalLabelText => totalLabelText != null ? totalLabelText.text : string.Empty;
        public string TotalValueText => totalValueText != null ? totalValueText.text : string.Empty;
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

        // Exactly one payline display is enabled: the skin image when the skin has a payline sprite, otherwise the
        // code-drawn border (recolored when the skin overrides the color). Both keep their GameObjects.
        private void ApplyPaylineSkin()
        {
            var entry = activeSkin != null ? activeSkin.PaylineFrame : null;
            var useImage = activeSkin != null && activeSkin.HasPaylineImage && paylineImage != null && paylineRoot != null;
            if (paylineImage != null)
            {
                if (useImage)
                {
                    entry.WarnIfBorderMissing(this);
                    if (paylineImage.sprite != entry.Sprite) paylineImage.sprite = entry.Sprite;
                    if (paylineImage.type != entry.ImageType) paylineImage.type = entry.ImageType;
                    if (!Mathf.Approximately(paylineImage.pixelsPerUnitMultiplier, entry.PixelsPerUnitMultiplier))
                        paylineImage.pixelsPerUnitMultiplier = entry.PixelsPerUnitMultiplier;
                    var color = entry.OverrideColor ? entry.Color : Color.white;
                    if (paylineImage.color != color) paylineImage.color = color;
                }
                if (paylineImage.raycastTarget) paylineImage.raycastTarget = false;
                if (paylineImage.enabled != useImage) paylineImage.enabled = useImage;
            }
            if (paylineFrame != null)
            {
                // The border takes the color override only; a sprite applies to the image alone.
                paylineBorderSkin.ApplyTo(paylineFrame, useImage || entry == null || !entry.OverrideColor ? null : entry, this);
                if (paylineFrame.enabled == useImage) paylineFrame.enabled = !useImage;
            }
        }

        private void OnEnable()
        {
            if (reelAreaLayout != null) reelAreaLayout.LayoutApplied += UpdatePaylineFrame;
            UpdatePaylineFrame();
        }

        // Applies a reel configuration. While a spin is playing it is deferred until the spin ends, because the running
        // presentation always finishes with the accepted spin's strip copies. Reels whose ID and strip are unchanged keep
        // the cell they show; others show their first cell on the payline.
        public void SetReels(IReadOnlyList<SlotReelDisplay> reels)
        {
            if (reels == null || reels.Count == 0) throw new ArgumentException("At least one reel is required.", nameof(reels));
            if (IsSpinning) { pendingReels = reels; return; }
            pendingReels = null;
            if (!EnsureReelCount(reels.Count)) return;
            for (var i = 0; i < reels.Count; i++)
            {
                var view = reelViews[i];
                var keep = view.ReelId == reels[i].ReelId && SameValues(view.Strip, reels[i].Values);
                var stop = keep ? view.StopIndex : 0;
                view.ShowStop(reels[i], stop);
            }
        }

        // Total box and rune line table. Every string comes from the display data.
        public void SetSummary(SlotSummaryDisplay summary)
        {
            if (summary == null) throw new ArgumentNullException(nameof(summary));
            SetText(totalLabelText, summary.TotalLabel);
            SetText(totalValueText, summary.TotalValue);
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

        public async Task<bool> PlaySpinAsync(SlotSpinDisplay result, bool reduceMotion, CancellationToken cancellationToken)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (!isActiveAndEnabled || !EnsureReelCount(result.Reels.Count)) return false;
            var token = ++generation;
            IsSpinning = true;
            SetSummary(result.SpinningSummary);
            try
            {
                // Every reel gets its plan first, then all start together and each waits for its own start delay.
                spinRandom ??= new System.Random();
                var plans = SlotReelSpinPlanner.Create(Settings, result.Reels.Count, reduceMotion, spinRandom);
                var tasks = new Task<bool>[result.Reels.Count];
                for (var i = 0; i < tasks.Length; i++)
                    tasks[i] = reelViews[i].PlaySpinAsync(result.Reels[i].Reel, result.Reels[i].StopIndex, plans[i], cancellationToken);
                var completed = await Task.WhenAll(tasks);
                if (this == null || token != generation) return false;
                foreach (var done in completed)
                    if (!done) return false;
                SetSummary(result.ResultSummary);
                return true;
            }
            finally
            {
                if (this != null && token == generation) EndSpin();
            }
        }

        // Puts every reel at rest on the accepted result immediately (timeout, cancellation or a restored result).
        public void StopAtResult(SlotSpinDisplay result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            generation++;
            if (EnsureReelCount(result.Reels.Count))
                for (var i = 0; i < result.Reels.Count; i++)
                    reelViews[i].ShowStop(result.Reels[i].Reel, result.Reels[i].StopIndex);
            SetSummary(result.ResultSummary);
            EndSpin();
        }

        private void OnDisable()
        {
            if (reelAreaLayout != null) reelAreaLayout.LayoutApplied -= UpdatePaylineFrame;
            if (!IsSpinning) return;
            generation++;
            EndSpin();
        }

        private void EndSpin()
        {
            IsSpinning = false;
            var pending = pendingReels;
            pendingReels = null;
            if (pending != null) SetReels(pending);
        }

        private void SetStatus(string message)
        {
            if (statusText == null) return;
            statusText.text = message ?? string.Empty;
            statusText.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text == null) return;
            value ??= string.Empty;
            if (text.text != value) text.text = value;
        }

        // Adds reels from the prefab or removes surplus ones, with one divider between neighbouring reels. Runs only
        // when the reel count changes.
        private bool EnsureReelCount(int count)
        {
            reelViews.RemoveAll(view => view == null);
            if (reelViews.Count < count && (reelPrefab == null || reelArea == null)) return false;
            var changed = reelViews.Count != count;
            while (reelViews.Count < count)
            {
                var view = Instantiate(reelPrefab, reelArea, false);
                view.name = $"SlotReel_{reelViews.Count + 1}";
                if (skinResolved) view.ApplySkin(activeSkin);
                reelViews.Add(view);
            }
            while (reelViews.Count > count)
            {
                var last = reelViews[reelViews.Count - 1];
                reelViews.RemoveAt(reelViews.Count - 1);
                DestroyObject(last.gameObject);
            }
            changed |= EnsureDividers(count - 1);
            if (changed) ArrangeSiblings();
            // Fewer, wider reels get larger digits; the range keeps narrow screens and many reels readable.
            foreach (var view in reelViews) view.SetFontSizeRange(minimumFontSize, maximumFontSize);
            if (changed) UpdatePaylineFrame();
            return true;
        }

        // The first divider is kept (inactive) as the template, so dividers can be added again after a single reel.
        private bool EnsureDividers(int count)
        {
            reelDividers.RemoveAll(divider => divider == null);
            if (reelDividers.Count == 0 || reelArea == null) return false;
            var changed = false;
            while (reelDividers.Count < count)
            {
                var divider = Instantiate(reelDividers[0], reelArea, false);
                divider.name = $"ReelDivider_{reelDividers.Count + 1}";
                if (skinResolved) ApplyDividerSkin(divider);
                reelDividers.Add(divider);
                changed = true;
            }
            while (reelDividers.Count > Mathf.Max(1, count))
            {
                var last = reelDividers[reelDividers.Count - 1];
                reelDividers.RemoveAt(reelDividers.Count - 1);
                DestroyObject(last.gameObject);
                changed = true;
            }
            for (var i = 0; i < reelDividers.Count; i++)
            {
                var active = i < count;
                if (reelDividers[i].gameObject.activeSelf == active) continue;
                reelDividers[i].gameObject.SetActive(active);
                changed = true;
            }
            return changed;
        }

        // Reel, divider, reel, ... so a divider is only ever between two reels.
        private void ArrangeSiblings()
        {
            var index = 0;
            for (var i = 0; i < reelViews.Count; i++)
            {
                reelViews[i].transform.SetSiblingIndex(index++);
                if (i < reelViews.Count - 1 && i < reelDividers.Count) reelDividers[i].SetSiblingIndex(index++);
            }
        }

        // Places the payline root from the first reel window's left edge to the last one's right edge at the payline
        // cell height. Its children stretch over it with negative offsets: the border by its thickness, the image by the
        // skin's payline outset. Runs after reel area layout passes, reel count changes and skin changes only.
        // A Prefab not yet migrated (no root) keeps the old single-graphic frame, expanded by the border thickness.
        private void UpdatePaylineFrame()
        {
            var frame = paylineRoot != null ? paylineRoot : paylineFrame != null ? paylineFrame.rectTransform : null;
            if (frame == null || reelViews.Count == 0) return;
            if (!(frame.parent is RectTransform parent)) return;
            var first = reelViews[0];
            var last = reelViews[reelViews.Count - 1];
            if (first == null || last == null || !first.GetPaylineWorldCorners(firstCorners) || !last.GetPaylineWorldCorners(lastCorners)) return;
            var min = (Vector2)parent.InverseTransformPoint(firstCorners[0]);
            var max = (Vector2)parent.InverseTransformPoint(lastCorners[2]);
            var expand = paylineRoot == null ? paylineFrame.EffectiveThickness : 0f;
            var size = new Vector2(Mathf.Abs(max.x - min.x), Mathf.Abs(max.y - min.y)) + Vector2.one * (2f * expand);
            var center = (min + max) * .5f - parent.rect.center;
            var middle = new Vector2(.5f, .5f);
            if (frame.anchorMin != middle) frame.anchorMin = middle;
            if (frame.anchorMax != middle) frame.anchorMax = middle;
            if (frame.pivot != middle) frame.pivot = middle;
            if (frame.sizeDelta != size) frame.sizeDelta = size;
            if (frame.anchoredPosition != center) frame.anchoredPosition = center;
            if (paylineRoot != null) UpdatePaylineChildren();
        }

        // Rewrites the child offsets only when the expansion changed (skin swap or canvas scale change).
        private void UpdatePaylineChildren()
        {
            if (paylineFrame != null && paylineFrame.rectTransform.parent == paylineRoot)
            {
                var thickness = paylineFrame.EffectiveThickness;
                if (!Mathf.Approximately(thickness, appliedBorderOutset))
                {
                    appliedBorderOutset = thickness;
                    Expand(paylineFrame.rectTransform, thickness, thickness, thickness, thickness);
                }
            }
            if (paylineImage != null && paylineImage.rectTransform.parent == paylineRoot)
            {
                var outset = activeSkin != null ? activeSkin.PaylineOutset : null;
                var value = outset != null ? new Vector4(outset.Left, outset.Right, outset.Top, outset.Bottom) : Vector4.zero;
                if (value != appliedImageOutset)
                {
                    appliedImageOutset = value;
                    Expand(paylineImage.rectTransform, value.x, value.y, value.z, value.w);
                }
            }
        }

        private static void Expand(RectTransform rect, float left, float right, float top, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = new Vector2(-left, -bottom);
            rect.offsetMax = new Vector2(right, top);
        }

        private static void DestroyObject(GameObject target)
        {
            if (UnityEngine.Application.isPlaying) Destroy(target); else DestroyImmediate(target);
        }

        private static bool SameValues(IReadOnlyList<int> left, IReadOnlyList<int> right)
        {
            if (left == null || right == null || left.Count != right.Count) return false;
            for (var i = 0; i < left.Count; i++) if (left[i] != right[i]) return false;
            return true;
        }

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
        public void SetSpinRandomSeedForEditor(int seed) => spinRandom = new System.Random(seed);

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
            return changed;
        }

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
