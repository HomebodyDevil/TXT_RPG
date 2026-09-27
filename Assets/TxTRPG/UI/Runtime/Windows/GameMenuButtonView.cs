using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.Windows
{
    public enum GameMenuButtonDisplayMode { ImageOnly, ImageWithLabel }

    [ExecuteAlways, DisallowMultipleComponent]
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
        [SerializeField, Tooltip("Hide the image explicitly while retaining the text fallback.")]
        private bool iconVisible = true;
        private Image appliedIcon;
        private Sprite appliedSprite;
        private bool appliedVisible, appliedEnabled, appliedActive, refreshPending;
        private GameMenuButtonDisplayMode appliedMode;
        private float appliedPadding;
        private Color appliedColor;

        public Button Button => button;
        public RectTransform VisualRoot => visualRoot;
        public TMP_Text Label => label;
        public Image Icon => icon;
        public GameMenuButtonDisplayMode DisplayMode => displayMode;

        public void SetLabel(string value)
        {
            if (label != null) label.text = value ?? string.Empty;
        }

        public void SetIcon(Sprite sprite, bool visible = true)
        {
            iconVisible = visible;
            if (icon != null) { icon.sprite = sprite; icon.enabled = true; }
            ApplyDisplayMode();
        }

        public void ConfigureDisplay(GameMenuButtonDisplayMode mode, float padding, Color color)
        { displayMode = mode; iconPadding = float.IsFinite(padding) ? Mathf.Max(0f, padding) : 8f; iconColor = color; ApplyDisplayMode(); }

        private void OnEnable() => ApplyDisplayMode();
        private void OnValidate() => refreshPending = true;
        private void LateUpdate()
        {
            if (refreshPending || appliedIcon != icon || appliedSprite != (icon != null ? icon.sprite : null) ||
                appliedVisible != iconVisible || appliedMode != displayMode || appliedPadding != iconPadding || appliedColor != iconColor ||
                appliedEnabled != (icon != null && icon.enabled) || appliedActive != (icon != null && icon.gameObject.activeSelf))
                ApplyDisplayMode();
        }

        public void RefreshDisplay() => ApplyDisplayMode();

        private void ApplyDisplayMode()
        {
            var showImage = iconVisible && icon != null && icon.enabled && icon.sprite != null;
            if (label != null) label.gameObject.SetActive(displayMode == GameMenuButtonDisplayMode.ImageWithLabel || !showImage);
            if (icon != null)
            {
                icon.gameObject.SetActive(showImage);
                icon.color = iconColor; icon.preserveAspect = true;
                if (icon.transform is RectTransform rect)
                {
                    if (displayMode == GameMenuButtonDisplayMode.ImageOnly)
                    {
                        rect.pivot = new Vector2(.5f, .5f);
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
            appliedIcon = icon; appliedSprite = icon != null ? icon.sprite : null;
            appliedVisible = iconVisible; appliedMode = displayMode; appliedPadding = iconPadding; appliedColor = iconColor;
            appliedEnabled = icon != null && icon.enabled; appliedActive = icon != null && icon.gameObject.activeSelf;
            refreshPending = false;
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
