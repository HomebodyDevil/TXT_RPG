using TMPro;
using UnityEngine;

namespace TxTRPG.UI.SlotMachine
{
    // Font size from the text's own height, within [minimum, maximum]. Replaces TMP Auto Size for the slot machine
    // summary: it runs only when the rect size changes.
    [DisallowMultipleComponent, RequireComponent(typeof(TMP_Text))]
    public sealed class SlotHeightScaledText : MonoBehaviour
    {
        [SerializeField, Range(.05f, 2f)] private float heightRatio = .6f;
        [SerializeField, Min(1f)] private float minimumFontSize = 12f;
        [SerializeField, Min(1f)] private float maximumFontSize = 64f;

        private TMP_Text text;

        private void OnEnable() => Apply();
        private void OnRectTransformDimensionsChange() => Apply();

        public void Apply()
        {
            if (text == null) text = GetComponent<TMP_Text>();
            if (text == null) return;
            var height = ((RectTransform)transform).rect.height;
            if (height <= 0f) return;
            text.enableAutoSizing = false;
            var size = Mathf.Clamp(height * heightRatio, minimumFontSize, Mathf.Max(minimumFontSize, maximumFontSize));
            if (!Mathf.Approximately(text.fontSize, size)) text.fontSize = size;
        }

#if UNITY_EDITOR
        public void ConfigureForEditor(float ratio, float minimum, float maximum)
        {
            heightRatio = Mathf.Clamp(ratio, .05f, 2f);
            minimumFontSize = Mathf.Max(1f, minimum);
            maximumFontSize = Mathf.Max(minimumFontSize, maximum);
        }
#endif
    }
}
