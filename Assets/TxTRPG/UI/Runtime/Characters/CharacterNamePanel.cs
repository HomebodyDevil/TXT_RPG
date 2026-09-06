using TMPro;
using UnityEngine;

namespace TxTRPG.UI
{
    [DisallowMultipleComponent]
    public sealed class CharacterNamePanel : CharacterStatusElement
    {
        [SerializeField] private TMP_Text valueText;

        public override void Apply(in CharacterStatusPresentation presentation)
        {
            if (valueText == null)
            {
                return;
            }

            valueText.text = presentation.Name.DisplayName;
            valueText.gameObject.SetActive(
                !string.IsNullOrWhiteSpace(presentation.Name.DisplayName));
        }

        public override void Clear()
        {
            if (valueText == null)
            {
                return;
            }

            valueText.text = string.Empty;
            valueText.gameObject.SetActive(false);
        }

        public void Configure(TMP_Text configuredValueText)
        {
            valueText = configuredValueText;
        }
    }
}
