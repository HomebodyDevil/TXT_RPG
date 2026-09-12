using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.Windows
{
    public enum GameMenuButtonDisplayMode { ImageOnly, ImageWithLabel }

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
        [SerializeField] private GameMenuButtonDisplayMode displayMode = GameMenuButtonDisplayMode.ImageWithLabel;
        [SerializeField, Min(0f)] private float iconPadding = 8f;
        [SerializeField] private Color iconColor = Color.white;

        public Button Button => button;
        public RectTransform VisualRoot => visualRoot;
        public TMP_Text Label => label;
        public GameMenuButtonDisplayMode DisplayMode => displayMode;

        public void SetLabel(string value)
        {
            if (label != null) label.text = value ?? string.Empty;
        }

        public void SetIcon(Sprite sprite, bool visible = true)
        {
            if (icon == null) return;
            icon.sprite = sprite;
            icon.color = iconColor;
            icon.preserveAspect = true;
            icon.gameObject.SetActive(visible && sprite != null);
            ApplyDisplayMode();
        }

        public void ConfigureDisplay(GameMenuButtonDisplayMode mode, float padding, Color color)
        { displayMode = mode; iconPadding = Mathf.Max(0f, padding); iconColor = color; ApplyDisplayMode(); }

        private void OnEnable() => ApplyDisplayMode();

        private void ApplyDisplayMode()
        {
            if (label != null) label.gameObject.SetActive(displayMode == GameMenuButtonDisplayMode.ImageWithLabel || icon == null || icon.sprite == null);
            if (icon != null)
            {
                icon.color = iconColor; icon.preserveAspect = true;
                if (icon.transform is RectTransform rect)
                {
                    if (displayMode == GameMenuButtonDisplayMode.ImageOnly)
                    {
                        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                        rect.offsetMin = Vector2.one * iconPadding; rect.offsetMax = -Vector2.one * iconPadding;
                    }
                    else
                    {
                        rect.anchorMin = rect.anchorMax = new Vector2(0f, .5f);
                        rect.pivot = new Vector2(0f, .5f); rect.anchoredPosition = new Vector2(12f, 0f);
                        rect.sizeDelta = new Vector2(28f, 28f);
                    }
                }
            }
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
