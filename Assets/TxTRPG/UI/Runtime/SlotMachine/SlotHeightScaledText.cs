using TMPro;
using UnityEngine;

namespace TxTRPG.UI.SlotMachine
{
    // Font size from the text's own height, within [minimum, maximum]. Replaces TMP Auto Size for the slot machine
    // summary: it runs only when the rect size changes. With fitWidth on, the size is also limited so that a sample
    // string (or the current text when the sample is empty) fits the text's width, so narrow boxes shrink the font
    // instead of truncating or clipping it.
    [DisallowMultipleComponent, RequireComponent(typeof(TMP_Text))]
    public sealed class SlotHeightScaledText : MonoBehaviour
    {
        [SerializeField, Range(.05f, 2f)] private float heightRatio = .6f;
        [SerializeField, Min(1f)] private float minimumFontSize = 12f;
        [SerializeField, Min(1f)] private float maximumFontSize = 64f;
        [SerializeField, Tooltip("Also limit the font size so the width sample fits the text's width.")]
        private bool fitWidth;
        [SerializeField, Tooltip("Widest expected content (e.g. \"88\" for a two-digit value). Empty uses the current text.")]
        private string widthSample = string.Empty;

        private TMP_Text text;

        private void OnEnable() => Apply();
        private void OnRectTransformDimensionsChange() => Apply();

        public void Apply()
        {
            if (text == null) text = GetComponent<TMP_Text>();
            if (text == null) return;
            var rect = ((RectTransform)transform).rect;
            if (rect.height <= 0f) return;
            text.enableAutoSizing = false;
            var size = rect.height * heightRatio;
            if (fitWidth) size = Mathf.Min(size, WidthLimitedSize(rect.width));
            size = Mathf.Clamp(size, minimumFontSize, Mathf.Max(minimumFontSize, maximumFontSize));
            if (!Mathf.Approximately(text.fontSize, size)) text.fontSize = size;
        }

        // Glyph width scales with the font size, so the sample is measured once at the current size. The preferred width
        // includes the fixed margins, which do not scale; 2% is kept free so TMP does not truncate at the exact edge.
        private float WidthLimitedSize(float width)
        {
            var sample = string.IsNullOrEmpty(widthSample) ? text.text : widthSample;
            var margins = text.margin.x + text.margin.z;
            var available = (width - margins) * .98f;
            if (string.IsNullOrEmpty(sample) || available <= 0f) return float.PositiveInfinity;
            var current = Mathf.Max(1f, text.fontSize);
            var glyphs = text.GetPreferredValues(sample, float.PositiveInfinity, float.PositiveInfinity).x - margins;
            return glyphs <= 0f ? float.PositiveInfinity : available * current / glyphs;
        }

#if UNITY_EDITOR
        public void ConfigureForEditor(float ratio, float minimum, float maximum)
        {
            heightRatio = Mathf.Clamp(ratio, .05f, 2f);
            minimumFontSize = Mathf.Max(1f, minimum);
            maximumFontSize = Mathf.Max(minimumFontSize, maximum);
        }

        public void ConfigureWidthFitForEditor(bool enabled, string sample)
        {
            fitWidth = enabled;
            widthSample = sample ?? string.Empty;
        }
#endif
    }
}
