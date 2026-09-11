using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.Windows
{
    [DisallowMultipleComponent]
    public sealed class GameMenuButtonView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private RectTransform visualRoot;
        [SerializeField] private Image background;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text label;
        [SerializeField] private Image border;
        [SerializeField] private RectTransform effectOverlay;

        public Button Button => button;
        public RectTransform VisualRoot => visualRoot;
        public TMP_Text Label => label;

        public void SetLabel(string value)
        {
            if (label != null) label.text = value ?? string.Empty;
        }

        public void SetIcon(Sprite sprite, bool visible = true)
        {
            if (icon == null) return;
            icon.sprite = sprite;
            icon.gameObject.SetActive(visible && sprite != null);
        }

#if UNITY_EDITOR
        public void ConfigureForEditor(Button targetButton, RectTransform targetVisualRoot,
            Image targetBackground, Image targetIcon, TMP_Text targetLabel, Image targetBorder,
            RectTransform targetEffectOverlay)
        {
            button = targetButton;
            visualRoot = targetVisualRoot;
            background = targetBackground;
            icon = targetIcon;
            label = targetLabel;
            border = targetBorder;
            effectOverlay = targetEffectOverlay;
            if (icon != null) icon.raycastTarget = false;
            if (label != null) label.raycastTarget = false;
            if (border != null) border.raycastTarget = false;
            if (effectOverlay != null)
                foreach (var graphic in effectOverlay.GetComponentsInChildren<Graphic>(true))
                    graphic.raycastTarget = false;
        }
#endif
    }
}
