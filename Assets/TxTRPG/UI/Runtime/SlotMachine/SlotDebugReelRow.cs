using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.SlotMachine
{
    // One reel row of the slot debug tab: "Reel n" label, 0..100 chance slider and the value text. SlotDebugTabView
    // clones the inactive template row and reuses rows when the reel count shrinks.
    [DisallowMultipleComponent]
    public sealed class SlotDebugReelRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private Slider slider;
        [SerializeField] private TMP_Text value;

        public TMP_Text Label => label;
        public Slider Slider => slider;
        public TMP_Text Value => value;
        public string ReelId { get; internal set; } = string.Empty;

        public bool IsValid => label != null && slider != null && value != null;

#if UNITY_EDITOR
        public void ConfigureForEditor(TMP_Text targetLabel, Slider targetSlider, TMP_Text targetValue)
        {
            label = targetLabel;
            slider = targetSlider;
            value = targetValue;
        }
#endif
    }
}
