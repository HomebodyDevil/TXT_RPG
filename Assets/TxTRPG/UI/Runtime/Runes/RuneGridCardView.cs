using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.Runes
{
    // One display-only rune card. The root RectTransform belongs to the grid layout; effects go on VisualRoot, CardArea or
    // BadgeArea. CardArea and BadgeArea are placed with anchor ratios inside the cell (card bottom right, badge top left),
    // so they follow any cell size without pixel offsets. Shows the icon when there is one, otherwise the name.
    [DisallowMultipleComponent]
    public sealed class RuneGridCardView : MonoBehaviour
    {
        public const int MaximumShownCount = 99;

        [SerializeField] private RectTransform visualRoot;
        [SerializeField] private RectTransform cardArea;
        [SerializeField] private RectTransform badgeArea;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text fallbackLabel;
        [SerializeField] private TMP_Text countText;

        public RectTransform VisualRoot => visualRoot;
        public RectTransform CardArea => cardArea;
        public RectTransform BadgeArea => badgeArea;
        public Image Icon => icon;
        public TMP_Text FallbackLabel => fallbackLabel;
        public TMP_Text CountText => countText;
        public string Key { get; private set; } = string.Empty;
        public bool HasIcon => icon != null && icon.gameObject.activeSelf && icon.sprite != null;

        public bool IsValid(out string missing)
        {
            missing = visualRoot == null ? nameof(visualRoot) : cardArea == null ? nameof(cardArea) : badgeArea == null ? nameof(badgeArea)
                : icon == null ? nameof(icon) : fallbackLabel == null ? nameof(fallbackLabel) : countText == null ? nameof(countText) : string.Empty;
            return missing.Length == 0;
        }

        public static string FormatCount(int count) =>
            count > MaximumShownCount ? $"{MaximumShownCount}+" : Mathf.Max(0, count).ToString(CultureInfo.InvariantCulture);

        // Card side C and badge side C × badgeRatio inside a cell of side C × (1 + overhangRatio).
        public void ApplyProportions(float badgeRatio, float overhangRatio)
        {
            if (cardArea == null || badgeArea == null) return;
            var cell = 1f + Mathf.Clamp01(overhangRatio);
            var cardStart = Mathf.Clamp01(overhangRatio) / cell;
            var badge = Mathf.Clamp01(badgeRatio) / cell;
            SetAnchors(cardArea, new Vector2(cardStart, 0f), new Vector2(1f, 1f / cell));
            SetAnchors(badgeArea, new Vector2(0f, 1f - badge), new Vector2(badge, 1f));
        }

        public void Bind(RuneGridEntryDisplay entry, Sprite iconSprite)
        {
            Key = entry.Key;
            var hasIcon = iconSprite != null;
            if (icon != null)
            {
                icon.sprite = iconSprite;
                icon.gameObject.SetActive(hasIcon);
            }
            if (fallbackLabel != null)
            {
                fallbackLabel.text = entry.DisplayName;
                fallbackLabel.gameObject.SetActive(!hasIcon);
            }
            if (countText != null) countText.text = FormatCount(entry.StackCount);
        }

        public void Clear()
        {
            Key = string.Empty;
            if (icon != null)
            {
                icon.sprite = null;
                icon.gameObject.SetActive(false);
            }
            if (fallbackLabel != null)
            {
                fallbackLabel.text = string.Empty;
                fallbackLabel.gameObject.SetActive(false);
            }
            if (countText != null) countText.text = string.Empty;
        }

        private static void SetAnchors(RectTransform target, Vector2 min, Vector2 max)
        {
            if (target.anchorMin == min && target.anchorMax == max && target.offsetMin == Vector2.zero && target.offsetMax == Vector2.zero) return;
            target.anchorMin = min;
            target.anchorMax = max;
            target.offsetMin = target.offsetMax = Vector2.zero;
        }
    }
}
