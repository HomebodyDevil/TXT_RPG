using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.SlotMachine
{
    // One row of the rune line table: effect name and value cells, or a single message across the row (no rune lines,
    // or "외 N줄" when the table cannot fit every line). Layout root and visual Background are separate objects.
    [DisallowMultipleComponent]
    public sealed class SlotRuneLineRow : MonoBehaviour
    {
        [SerializeField] private Graphic background;
        [SerializeField] private TMP_Text effectText;
        [SerializeField] private TMP_Text valueText;
        [SerializeField] private TMP_Text messageText;
        [Header("Skin targets (defaults recorded from the Prefab)")]
        [SerializeField] private SlotSkinGraphicTarget backgroundSkin = new();
        [SerializeField] private SlotSkinTextTarget effectTextSkin = new();
        [SerializeField] private SlotSkinTextTarget valueTextSkin = new();
        [SerializeField] private SlotSkinTextTarget messageTextSkin = new();

        public string EffectText => effectText != null ? effectText.text : string.Empty;
        public string ValueText => valueText != null ? valueText.text : string.Empty;
        public string MessageText => messageText != null ? messageText.text : string.Empty;

        // The unused texts are emptied rather than deactivated, so a row can change while a layout rebuild runs.
        public void Bind(string effect, string value)
        {
            SetText(effectText, effect);
            SetText(valueText, value);
            SetText(messageText, string.Empty);
        }

        public void ShowMessage(string message)
        {
            SetText(effectText, string.Empty);
            SetText(valueText, string.Empty);
            SetText(messageText, message);
        }

        // Rows created from the template carry the template's recorded defaults, so they never inherit a previous skin.
        public void ApplySkin(SlotMachinePanelSkin skin)
        {
            backgroundSkin.Apply(skin != null ? skin.RuneRowBackground : null, this);
            var text = skin != null ? skin.LightSurfaceTextColor : null;
            effectTextSkin.Apply(text);
            valueTextSkin.Apply(text);
            messageTextSkin.Apply(text);
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text == null) return;
            value ??= string.Empty;
            if (text.text != value) text.text = value;
        }

#if UNITY_EDITOR
        public void ConfigureForEditor(Graphic targetBackground, TMP_Text targetEffect, TMP_Text targetValue, TMP_Text targetMessage)
        {
            background = targetBackground;
            effectText = targetEffect;
            valueText = targetValue;
            messageText = targetMessage;
        }

        public bool ConfigureSkinForEditor(bool overwrite)
        {
            var changed = backgroundSkin.Record(background, overwrite);
            changed |= effectTextSkin.Record(effectText, overwrite);
            changed |= valueTextSkin.Record(valueText, overwrite);
            changed |= messageTextSkin.Record(messageText, overwrite);
            return changed;
        }

        public SlotSkinGraphicTarget BackgroundSkinForEditor => backgroundSkin;
        public SlotSkinTextTarget EffectTextSkinForEditor => effectTextSkin;
#endif
    }
}
