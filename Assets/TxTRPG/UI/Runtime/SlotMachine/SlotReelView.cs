using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.SlotMachine
{
    [Serializable]
    public sealed class SlotSymbolSprite
    {
        [SerializeField] private int value;
        [SerializeField] private Sprite sprite;
        public int Value => value;
        public Sprite Sprite => sprite;
    }

    // Low-alpha copies of one cell's text, placed behind it during the high-speed phase.
    [Serializable]
    public sealed class SlotCellAfterimages
    {
        [SerializeField] private TMP_Text[] texts = Array.Empty<TMP_Text>();
        public SlotCellAfterimages() { }
        public SlotCellAfterimages(TMP_Text[] targetTexts) => texts = targetTexts ?? Array.Empty<TMP_Text>();
        public IReadOnlyList<TMP_Text> Texts => texts ?? Array.Empty<TMP_Text>();
    }

    // 2D uGUI reel. Hierarchy (see slot-machine-and-runes.md):
    //   SlotReel_{index} (layout only) > Frame (cylinder shading), Window (RectMask2D) > Strip (motion root) >
    //   SymbolCell_0..{visible + 1} > VisualRoot > Icon, Value, Afterimage_1..3, plus Window > ShadeOverlay and an
    //   EffectRoot outside the Window for effects that must not be clipped.
    // With c = visibleCellCount / 2 + 1, cell i sits at y = -(i - c) × cellHeight inside Strip and shows
    // strip[(base + i - c) mod n], so the cell above the payline is the previous value and the one below is the next.
    // Spinning moves Strip up by a fraction of a cell; at each whole cell the base index advances and Strip snaps back.
    // Effects only change each cell's VisualRoot and its afterimages; cell positions are set by UpdateCellLayout alone.
    [DisallowMultipleComponent]
    public sealed class SlotReelView : SlotReelViewBase
    {
        [SerializeField] private RectTransform window;
        [SerializeField] private RectTransform strip;
        [SerializeField, Min(1), Tooltip("Odd number of cells visible in the Window. Every cell array holds this + 2 entries.")]
        private int visibleCellCount = 5;
        [SerializeField] private RectTransform[] cells = Array.Empty<RectTransform>();
        [SerializeField] private RectTransform[] cellVisualRoots = Array.Empty<RectTransform>();
        [SerializeField] private TMP_Text[] cellTexts = Array.Empty<TMP_Text>();
        [SerializeField] private Image[] cellIcons = Array.Empty<Image>();
        [SerializeField] private SlotCellAfterimages[] cellAfterimages = Array.Empty<SlotCellAfterimages>();
        [SerializeField, Tooltip("Optional value-to-sprite map. A value without a sprite is shown as its number.")]
        private List<SlotSymbolSprite> symbolSprites = new();
        [SerializeField, Range(.1f, 1f), Tooltip("Font size as a fraction of the smaller of cell height and width.")]
        private float paylineFontRatio = .62f;
        [SerializeField, Tooltip("Bold text on the payline cell at rest.")] private bool boldPayline;
        [SerializeField, Tooltip("Text alpha by distance from the payline in cells (index 0 = payline). Interpolated while spinning.")]
        private float[] distanceAlpha = { 1f, .8f, .4f };
        [SerializeField, Tooltip("Font scale by distance from the payline in cells (index 0 = payline).")]
        private float[] distanceFontScale = { 1f, 1f, 1f };
        [SerializeField] private float minimumFontSize = 18f;
        [SerializeField] private float maximumFontSize = 64f;
        [Header("Skin targets (defaults recorded from the Prefab)")]
        [SerializeField, Tooltip("Frame (cylinder shading).")] private SlotSkinGraphicTarget frameSkin = new();
        [SerializeField, Tooltip("Window/ShadeOverlay.")] private SlotSkinGraphicTarget shadeSkin = new();
        [SerializeField, Tooltip("Payline Value text; its default color is the RGB base of every cell and afterimage text.")]
        private SlotSkinTextTarget valueTextSkin = new();

        private readonly int[] noValues = Array.Empty<int>();
        private readonly Vector3[] corners = new Vector3[4];
        private int[] values = Array.Empty<int>();
        private string reelId = string.Empty;
        private int baseIndex;
        private int boundBase = -1;
        private int generation;
        private bool emphasizePayline = true;
        private bool warnedLayout;
        private Vector2 cellLayoutSize = new(-1f, -1f);
        private SlotReelSpinSettings effectSettings;
        private float appliedOffset = -1f;
        private float appliedStrength = -1f;
        private bool hasTextBase;
        private Color textBase = Color.white;

        public override string ReelId => reelId;
        public override int PaylineValue => values.Length == 0 ? 0 : values[baseIndex];
        public override IReadOnlyList<int> Strip => values.Length == 0 ? noValues : Array.AsReadOnly(values);
        public override int StopIndex => baseIndex;
        public int VisibleCellCount => VisibleCells;
        // Values and texts shown around the payline (verification only); offsets from -(c - 1) to c - 1.
        public int ValueAt(int offsetFromPayline) => values.Length == 0 ? 0 : values[Wrap(baseIndex + offsetFromPayline)];
        public string TextAt(int offsetFromPayline)
        {
            var index = Center + offsetFromPayline;
            return LayoutValid && index >= 0 && index < cellTexts.Length && cellTexts[index] != null ? cellTexts[index].text : string.Empty;
        }
        // Current effect state of the cells (verification only).
        public bool AnyAfterimageVisible
        {
            get
            {
                foreach (var set in cellAfterimages)
                    if (set != null)
                        foreach (var text in set.Texts)
                            if (text != null && text.enabled) return true;
                return false;
            }
        }
        public float MaximumVisualScaleY
        {
            get
            {
                var max = 1f;
                foreach (var root in cellVisualRoots) if (root != null) max = Mathf.Max(max, root.localScale.y);
                return max;
            }
        }

        private int VisibleCells => Mathf.Max(1, visibleCellCount | 1);
        private int CellCount => VisibleCells + 2;
        private int Center => VisibleCells / 2 + 1;
        private bool LayoutValid => window != null && cells.Length == CellCount && cellTexts.Length == CellCount;
        private float CellHeight => window == null ? 0f : window.rect.height / VisibleCells;

        public override void SetFontSizeRange(float minimum, float maximum)
        {
            minimumFontSize = Mathf.Max(1f, minimum);
            maximumFontSize = Mathf.Max(minimumFontSize, maximum);
            cellLayoutSize = new Vector2(-1f, -1f);
            UpdateCellLayout();
        }

        public override void ShowStop(SlotReelDisplay reel, int stopIndex)
        {
            if (reel == null) throw new ArgumentNullException(nameof(reel));
            generation++;
            Assign(reel);
            baseIndex = Wrap(stopIndex);
            SetRest();
        }

        public override async Task<bool> PlaySpinAsync(SlotReelDisplay reel, int stopIndex, SlotReelSpinPlan plan,
            CancellationToken cancellationToken)
        {
            if (reel == null) throw new ArgumentNullException(nameof(reel));
            var token = ++generation;
            var start = Wrap(baseIndex);
            Assign(reel);
            start = Wrap(start);
            var target = Wrap(stopIndex);
            var startTime = Time.unscaledTime;

            if (plan.ReduceMotion)
            {
                while (Time.unscaledTime - startTime < plan.ReducedMotionSeconds)
                {
                    await Task.Yield();
                    if (Interrupted(token, cancellationToken)) return false;
                }
                FinishAt(target);
                return true;
            }

            // The previous stop stays visible during the start delay.
            while (Time.unscaledTime - startTime < plan.DelaySeconds)
            {
                await Task.Yield();
                if (Interrupted(token, cancellationToken)) return false;
            }
            var curve = new SlotReelSpinCurve(plan, target - start, values.Length);
            if (!curve.HasMotion) { FinishAt(target); return true; }
            var motionStart = startTime + plan.DelaySeconds;
            effectSettings = plan.Settings;
            emphasizePayline = false;
            boundBase = -1;
            cellLayoutSize = new Vector2(-1f, -1f);
            UpdateCellLayout();
            try
            {
                while (true)
                {
                    // A long frame jumps straight to the matching point of the curve.
                    var elapsed = Time.unscaledTime - motionStart;
                    if (elapsed >= curve.DurationSeconds) break;
                    SetPosition(start, curve.Position(elapsed), curve.EffectStrength(elapsed));
                    await Task.Yield();
                    if (Interrupted(token, cancellationToken)) return false;
                }
            }
            finally
            {
                // An interrupted spin never leaves afterimages or a stretched VisualRoot behind.
                if (this != null && token == generation) ApplyAppearance(0f, 0f);
            }
            FinishAt(target);
            return true;
        }

        public override bool GetPaylineWorldCorners(Vector3[] output)
        {
            if (output == null || output.Length < 4 || window == null) return false;
            window.GetWorldCorners(corners);
            var leftUp = (corners[1] - corners[0]) / VisibleCells;
            var rightUp = (corners[2] - corners[3]) / VisibleCells;
            var leftMid = (corners[0] + corners[1]) * .5f;
            var rightMid = (corners[3] + corners[2]) * .5f;
            output[0] = leftMid - leftUp * .5f;
            output[1] = leftMid + leftUp * .5f;
            output[2] = rightMid + rightUp * .5f;
            output[3] = rightMid - rightUp * .5f;
            return leftUp.sqrMagnitude > 0f;
        }

        // Images take the skin entry or their recorded default. Text keeps its per-cell and afterimage alpha, which is
        // multiplied by the alpha of the skin (or default) color; only the base color changes here, so spinning is unaffected.
        public override void ApplySkin(SlotMachinePanelSkin skin)
        {
            frameSkin.Apply(skin != null ? skin.ReelBackground : null, this);
            shadeSkin.Apply(skin != null ? skin.ReelShadeOverlay : null, this);
            if (!valueTextSkin.HasDefaults && (skin == null || !skin.ReelTextColor.Override)) hasTextBase = false;
            else
            {
                var color = valueTextSkin.Resolve(skin != null ? skin.ReelTextColor : null);
                // The stored default alpha is whatever distance alpha the Prefab was saved with; the base is opaque.
                if (skin == null || !skin.ReelTextColor.Override) color.a = 1f;
                hasTextBase = true;
                textBase = color;
            }
            appliedOffset = -1f;
            ApplyAppearance(Mathf.Max(0f, appliedStrength) > 0f ? CurrentOffset : 0f, Mathf.Max(0f, appliedStrength));
        }

        private void OnRectTransformDimensionsChange() => UpdateCellLayout();

        private void OnDisable()
        {
            generation++;
            if (cells.Length > 0) ApplyAppearance(0f, 0f);
        }

        private bool Interrupted(int token, CancellationToken cancellationToken) =>
            this == null || token != generation || cancellationToken.IsCancellationRequested || !isActiveAndEnabled;

        private void FinishAt(int target)
        {
            baseIndex = target;
            SetRest();
        }

        private void SetRest()
        {
            emphasizePayline = true;
            if (strip != null) strip.anchoredPosition = Vector2.zero;
            boundBase = -1;
            cellLayoutSize = new Vector2(-1f, -1f);
            UpdateCellLayout();
            Bind();
            ApplyAppearance(0f, 0f);
        }

        private void SetPosition(int start, float cellsTravelled, float strength)
        {
            // FloorToInt and Wrap also handle the negative and overshooting positions of Elastic and Back easings.
            var whole = Mathf.FloorToInt(cellsTravelled);
            var fraction = cellsTravelled - whole;
            baseIndex = Wrap(start + whole);
            if (strip != null) strip.anchoredPosition = new Vector2(0f, fraction * CellHeight);
            Bind();
            ApplyAppearance(fraction, strength);
        }

        private void Assign(SlotReelDisplay reel)
        {
            reelId = reel.ReelId;
            if (values.Length != reel.Values.Count) values = new int[reel.Values.Count];
            for (var i = 0; i < values.Length; i++) values[i] = reel.Values[i];
            boundBase = -1;
        }

        // Rebinds the cell texts only when the base index changes; never per frame otherwise.
        private void Bind()
        {
            if (values.Length == 0 || !LayoutValid || boundBase == baseIndex) return;
            boundBase = baseIndex;
            for (var i = 0; i < CellCount; i++)
            {
                var value = values[Wrap(baseIndex + i - Center)];
                var sprite = FindSprite(value);
                var text = cellTexts[i];
                if (text != null)
                {
                    text.enabled = sprite == null;
                    var label = Label(value);
                    text.text = label;
                    var afterimages = AfterimagesOf(i);
                    for (var j = 0; j < afterimages.Count; j++)
                        if (afterimages[j] != null && afterimages[j].enabled) afterimages[j].text = label;
                }
                if (i < cellIcons.Length && cellIcons[i] != null)
                {
                    cellIcons[i].sprite = sprite;
                    cellIcons[i].enabled = sprite != null;
                }
            }
        }

        // Sizes and positions the cells from the Window height, and derives the font sizes from the cell size.
        private void UpdateCellLayout()
        {
            if (window == null) return;
            if (!LayoutValid)
            {
                if (!warnedLayout && cells.Length > 0)
                {
                    warnedLayout = true;
                    Debug.LogWarning($"{name}: SlotReelView expects {CellCount} cells and texts for {VisibleCells} visible cells; the layout is skipped.", this);
                }
                return;
            }
            var size = window.rect.size;
            var key = new Vector2(size.x, size.y * (emphasizePayline ? 1f : -1f));
            if (key == cellLayoutSize) return;
            cellLayoutSize = key;
            var height = size.y / VisibleCells;
            var baseSize = Mathf.Clamp(Mathf.Min(height, size.x) * paylineFontRatio, minimumFontSize, maximumFontSize);
            for (var i = 0; i < CellCount; i++)
            {
                var cell = cells[i];
                if (cell == null) continue;
                cell.anchorMin = new Vector2(0f, .5f);
                cell.anchorMax = new Vector2(1f, .5f);
                cell.pivot = new Vector2(.5f, .5f);
                cell.sizeDelta = new Vector2(0f, height);
                cell.anchoredPosition = new Vector2(0f, -(i - Center) * height);
                var text = cellTexts[i];
                if (text == null) continue;
                var fontSize = baseSize * Sample(distanceFontScale, Mathf.Abs(i - Center), 1f);
                text.enableAutoSizing = false;
                text.fontSize = fontSize;
                var style = emphasizePayline && boldPayline && i == Center ? FontStyles.Bold : FontStyles.Normal;
                text.fontStyle = style;
                var afterimages = AfterimagesOf(i);
                for (var j = 0; j < afterimages.Count; j++)
                {
                    if (afterimages[j] == null) continue;
                    afterimages[j].enableAutoSizing = false;
                    afterimages[j].fontSize = fontSize;
                    afterimages[j].fontStyle = style;
                }
            }
            appliedOffset = -1f;
            ApplyAppearance(Mathf.Max(0f, appliedStrength) > 0f ? CurrentOffset : 0f, Mathf.Max(0f, appliedStrength));
        }

        private float CurrentOffset => strip == null || CellHeight <= 0f ? 0f : strip.anchoredPosition.y / CellHeight;

        // Text alpha follows the visual distance from the payline, so it does not jump when Strip snaps back. The
        // high-speed strength adds afterimages, a vertical stretch of VisualRoot and a lower text alpha.
        private void ApplyAppearance(float offset, float strength)
        {
            if (!LayoutValid) return;
            strength = Mathf.Clamp01(strength);
            if (Mathf.Approximately(offset, appliedOffset) && Mathf.Approximately(strength, appliedStrength)) return;
            appliedOffset = offset;
            appliedStrength = strength;
            var textAlpha = Mathf.Lerp(1f, effectSettings.CruiseTextAlpha, strength);
            var afterimageCount = strength > 0f ? effectSettings.AfterimageCount : 0;
            var scaleY = strength > 0f ? 1f + (effectSettings.MaximumVerticalStretch - 1f) * strength : 1f;
            var spacing = effectSettings.AfterimageSpacing * CellHeight * strength;
            for (var i = 0; i < CellCount; i++)
            {
                var text = cellTexts[i];
                if (text != null)
                {
                    var distance = Mathf.Abs(i - Center - offset);
                    SetAlpha(text, Sample(distanceAlpha, distance, 1f) * textAlpha);
                }
                if (i < cellVisualRoots.Length && cellVisualRoots[i] != null)
                {
                    var scale = cellVisualRoots[i].localScale;
                    if (!Mathf.Approximately(scale.y, scaleY)) cellVisualRoots[i].localScale = new Vector3(scale.x, scaleY, scale.z);
                }
                var afterimages = AfterimagesOf(i);
                var showAfterimages = text != null && text.enabled;
                for (var j = 0; j < afterimages.Count; j++)
                {
                    var afterimage = afterimages[j];
                    if (afterimage == null) continue;
                    var show = showAfterimages && j < afterimageCount;
                    if (!show)
                    {
                        if (afterimage.enabled) afterimage.enabled = false;
                        var rect = afterimage.rectTransform;
                        if (rect.anchoredPosition != Vector2.zero) rect.anchoredPosition = Vector2.zero;
                        continue;
                    }
                    if (!afterimage.enabled)
                    {
                        afterimage.text = text.text;
                        afterimage.enabled = true;
                    }
                    // Strip moves up, so afterimages trail below the text (the direction it came from).
                    afterimage.rectTransform.anchoredPosition = new Vector2(0f, -(j + 1) * spacing);
                    SetAlpha(afterimage, effectSettings.AfterimageAlpha * strength * (1f - j / (float)(afterimageCount + 1)));
                }
            }
        }

        private IReadOnlyList<TMP_Text> AfterimagesOf(int cell) =>
            cell < cellAfterimages.Length && cellAfterimages[cell] != null ? cellAfterimages[cell].Texts : Array.Empty<TMP_Text>();

        // Without a skin base the text keeps its own RGB (the Prefab color) and only the alpha changes.
        private void SetAlpha(TMP_Text text, float alpha)
        {
            var color = text.color;
            if (hasTextBase)
            {
                var target = new Color(textBase.r, textBase.g, textBase.b, textBase.a * alpha);
                if (color != target) text.color = target;
                return;
            }
            if (Mathf.Approximately(color.a, alpha)) return;
            color.a = alpha;
            text.color = color;
        }

        // Linear interpolation over a per-distance table; distances past the end use the last entry.
        private static float Sample(float[] table, float distance, float fallback)
        {
            if (table == null || table.Length == 0) return fallback;
            if (distance <= 0f) return table[0];
            var lower = Mathf.FloorToInt(distance);
            if (lower >= table.Length - 1) return table[table.Length - 1];
            return Mathf.Lerp(table[lower], table[lower + 1], distance - lower);
        }

        private Sprite FindSprite(int value)
        {
            for (var i = 0; i < symbolSprites.Count; i++)
                if (symbolSprites[i] != null && symbolSprites[i].Value == value) return symbolSprites[i].Sprite;
            return null;
        }

        // Cached labels, so rebinding during a spin does not allocate a string per wrap.
        private static readonly string[] Labels = new string[256];
        private static string Label(int value)
        {
            if (value < 0 || value >= Labels.Length) return value.ToString();
            return Labels[value] ??= value.ToString();
        }

        private int Wrap(int index) => values.Length == 0 ? 0 : ((index % values.Length) + values.Length) % values.Length;

#if UNITY_EDITOR
        public void ConfigureForEditor(RectTransform targetWindow, RectTransform targetStrip, int targetVisibleCells,
            RectTransform[] targetCells, RectTransform[] targetVisualRoots, TMP_Text[] targetTexts, Image[] targetIcons,
            SlotCellAfterimages[] targetAfterimages)
        {
            window = targetWindow;
            strip = targetStrip;
            visibleCellCount = Mathf.Max(1, targetVisibleCells | 1);
            cells = targetCells ?? Array.Empty<RectTransform>();
            cellVisualRoots = targetVisualRoots ?? Array.Empty<RectTransform>();
            cellTexts = targetTexts ?? Array.Empty<TMP_Text>();
            cellIcons = targetIcons ?? Array.Empty<Image>();
            cellAfterimages = targetAfterimages ?? Array.Empty<SlotCellAfterimages>();
            cellLayoutSize = new Vector2(-1f, -1f);
            warnedLayout = false;
        }

        // Verification only: applies the high-speed effect at a fixed strength without a running spin.
        public void ApplyMotionEffectForEditor(SlotReelSpinSettings settings, float strength)
        {
            effectSettings = settings;
            ApplyAppearance(CurrentOffset, strength);
        }

        // Records the default look of the skin targets. Existing defaults are kept unless overwrite is set.
        public bool ConfigureSkinForEditor(Graphic targetFrame, Graphic targetShade, bool overwrite)
        {
            var changed = frameSkin.Record(targetFrame, overwrite);
            changed |= shadeSkin.Record(targetShade, overwrite);
            var center = cellTexts.Length > Center ? cellTexts[Center] : null;
            changed |= valueTextSkin.Record(center, overwrite);
            return changed;
        }

        [ContextMenu("기본 외형 다시 기록")]
        private void RecordSkinDefaultsFromContextMenu()
        {
            if (UnityEngine.Application.isPlaying) { Debug.LogWarning("Record the default look outside Play Mode; a skin may be applied now.", this); return; }
            UnityEditor.Undo.RecordObject(this, "기본 외형 다시 기록");
            ConfigureSkinForEditor(frameSkin.Graphic, shadeSkin.Graphic, true);
            UnityEditor.EditorUtility.SetDirty(this);
        }

        public SlotSkinGraphicTarget FrameSkinForEditor => frameSkin;
        public SlotSkinGraphicTarget ShadeSkinForEditor => shadeSkin;
        public IReadOnlyList<TMP_Text> CellTextsForEditor => cellTexts;
        public RectTransform StripForEditor => strip;
        public RectTransform WindowForEditor => window;
#endif
    }
}
