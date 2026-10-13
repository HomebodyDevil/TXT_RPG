using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TxTRPG.UI.SlotMachine
{
    // Places the total Fortune Critical multiplier (TotalBox/TotalMultiplier, a layout-only root) next to the drawn total
    // value: to the right of its glyphs like a superscript when there is room, otherwise above them (extending above a
    // very short TotalBox into the summary margin instead of covering the value). It never moves or resizes TotalValue or
    // TotalLabel. Runs when TotalBox changes size and when the panel changes the texts; never per
    // frame. Effects may change TotalMultiplier/VisualRoot only.
    [DisallowMultipleComponent]
    public sealed class SlotTotalMultiplierLayout : UIBehaviour
    {
        [SerializeField] private TMP_Text valueText;
        [SerializeField, Tooltip("Height-based font of the value; applied first so the measured glyphs use the current size.")]
        private SlotHeightScaledText valueScaler;
        [SerializeField] private RectTransform multiplierRoot;
        [SerializeField] private TMP_Text multiplierText;
        [SerializeField, Range(.2f, 1f), Tooltip("Multiplier font size as a fraction of the value font size.")]
        private float fontRatio = .5f;
        [SerializeField, Min(1f)] private float minimumFontSize = 12f;
        [SerializeField, Min(1f)] private float maximumFontSize = 36f;
        [SerializeField, Range(0f, .5f), Tooltip("Gap between the value glyphs and the multiplier, as a fraction of the value font size.")]
        private float gap = .08f;

        // True when the multiplier is beside the value glyphs, false when it is above them (verification only).
        public bool PlacedBeside { get; private set; }

        protected override void OnEnable()
        {
            base.OnEnable();
            Refresh();
        }

        protected override void OnRectTransformDimensionsChange() => Refresh();

        public void Refresh()
        {
            if (valueText == null || multiplierRoot == null || multiplierText == null || !(multiplierRoot.parent is RectTransform box)) return;
            if (!multiplierRoot.gameObject.activeSelf || string.IsNullOrEmpty(multiplierText.text)) return;
            if (valueScaler != null) valueScaler.Apply();
            var area = box.rect;
            var valueRect = (RectTransform)valueText.transform;
            if (area.width <= 0f || area.height <= 0f || valueRect.parent != box) return;
            // The drawn glyph bounds of the value, in TotalBox space (the value text was just set or resized).
            valueText.ForceMeshUpdate();
            var bounds = valueText.textBounds;
            var offset = (Vector2)valueRect.localPosition;
            var valueFont = valueText.fontSize;
            var spacing = valueFont * gap;
            var glyphRight = offset.x + bounds.max.x;
            var glyphTop = offset.y + bounds.max.y;

            var font = Mathf.Clamp(valueFont * fontRatio, minimumFontSize, Mathf.Max(minimumFontSize, maximumFontSize));
            var size = Measure(font);
            var besideLeft = glyphRight + spacing;
            PlacedBeside = besideLeft + size.x <= area.xMax;
            Vector2 position;
            if (PlacedBeside)
            {
                // Superscript: the multiplier's top lines up with the top of the value glyphs.
                position = new Vector2(besideLeft, Mathf.Min(area.yMax, glyphTop + size.y * .25f));
            }
            else
            {
                // Above the glyphs, right-aligned with them, shrunk to the free height but not below the minimum size.
                // In a very short box the multiplier then extends above TotalBox into the summary area's margin rather
                // than covering the value.
                var free = area.yMax - (glyphTop + spacing);
                font = Mathf.Clamp(Mathf.Min(font, free / 1.15f), minimumFontSize, Mathf.Max(minimumFontSize, maximumFontSize));
                size = Measure(font);
                var left = Mathf.Clamp(glyphRight - size.x, area.xMin, Mathf.Max(area.xMin, area.xMax - size.x));
                position = new Vector2(left, glyphTop + spacing + size.y);
            }
            var corner = new Vector2(0f, 1f);
            var middle = new Vector2(.5f, .5f);
            if (multiplierRoot.anchorMin != middle) multiplierRoot.anchorMin = middle;
            if (multiplierRoot.anchorMax != middle) multiplierRoot.anchorMax = middle;
            if (multiplierRoot.pivot != corner) multiplierRoot.pivot = corner;
            var anchored = position - area.center;
            if (multiplierRoot.anchoredPosition != anchored) multiplierRoot.anchoredPosition = anchored;
            if (multiplierRoot.sizeDelta != size) multiplierRoot.sizeDelta = size;
            if (multiplierText.enableAutoSizing) multiplierText.enableAutoSizing = false;
            if (!Mathf.Approximately(multiplierText.fontSize, font)) multiplierText.fontSize = font;
        }

        // Text width scales with the font size, so it is measured at the current size without changing the text.
        private Vector2 Measure(float font)
        {
            var current = Mathf.Max(1f, multiplierText.fontSize);
            var preferred = multiplierText.GetPreferredValues(multiplierText.text, float.PositiveInfinity, float.PositiveInfinity);
            return new Vector2(Mathf.Ceil(preferred.x * font / current) + 2f, Mathf.Ceil(font * 1.15f));
        }

#if UNITY_EDITOR
        public void ConfigureForEditor(TMP_Text targetValue, SlotHeightScaledText targetScaler, RectTransform targetRoot, TMP_Text targetText)
        {
            valueText = targetValue;
            valueScaler = targetScaler;
            multiplierRoot = targetRoot;
            multiplierText = targetText;
        }

        public RectTransform MultiplierRootForEditor => multiplierRoot;
#endif
    }
}
