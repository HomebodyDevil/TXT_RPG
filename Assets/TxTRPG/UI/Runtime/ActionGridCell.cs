using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI
{
    public enum ActionGridIconSizingMode
    {
        FixedPadding,
        RelativeToContent,
        RelativeWithMaxSize
    }

    public sealed class ActionGridCell : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI quantity;
        [SerializeField] private Image cooldownOverlay;
        [SerializeField] private GameObject disabledOverlay;
        [SerializeField] private GameObject selectionFrame;
        [SerializeField] private TextMeshProUGUI shortcutLabel;
        [SerializeField] private GameObject emptySlotVisual;

        [Header("Icon Layout")]
        [Tooltip("Fixed Padding uses Canvas UI units. Relative To Content scales the Icon Rect from the actual ContentRoot size.")]
        [SerializeField] private ActionGridIconSizingMode iconSizingMode = ActionGridIconSizingMode.FixedPadding;
        [Tooltip("Fraction of ContentRoot width and height occupied by the centered Icon Rect. Preserve Aspect still controls the sprite inside this Rect.")]
        [SerializeField, Range(0f, 1f)] private float iconAreaRatio = 0.9f;
        [Tooltip("Left, Right, Top, and Bottom padding in Canvas UI units. Used only in Fixed Padding mode.")]
        [SerializeField] private RectOffset iconPadding;
        [Tooltip("Maximum Icon Rect width and height in Canvas UI units. Used only with Relative With Max Size.")]
        [SerializeField] private Vector2 iconMaximumSize = new(64f, 64f);

        [Header("Empty Slot Layout")]
        [Tooltip("Fixed Padding uses Canvas UI units. Relative To Content scales only the EmptySlot Rect from the actual ContentRoot size.")]
        [SerializeField] private ActionGridIconSizingMode emptySlotSizingMode = ActionGridIconSizingMode.FixedPadding;
        [Tooltip("Fraction of ContentRoot width and height occupied by the centered EmptySlot Rect. This setting does not affect item icons.")]
        [SerializeField, Range(0f, 1f)] private float emptySlotAreaRatio = 0.9f;
        [Tooltip("Left, Right, Top, and Bottom padding in Canvas UI units. Used only in Fixed Padding mode; the legacy value is 14 on every side.")]
        [SerializeField] private RectOffset emptySlotPadding;
        [Tooltip("Maximum EmptySlot Rect width and height in Canvas UI units. Used only with Relative With Max Size.")]
        [SerializeField] private Vector2 emptySlotMaximumSize = new(64f, 64f);

        private Action<int> activate;
        private bool isApplyingIconLayout;
        private bool isApplyingEmptySlotLayout;
        private bool didWarnAboutMissingEmptySlotRect;

        public int Index { get; private set; }
        public bool HasEntry { get; private set; }
        public Button Button => button;
        public ActionGridIconSizingMode IconSizingMode => iconSizingMode;
        public float IconAreaRatio => iconAreaRatio;
        public RectOffset IconPadding => iconPadding;
        public Vector2 IconMaximumSize => iconMaximumSize;
        public RectTransform IconRect => icon != null ? icon.rectTransform : null;
        public ActionGridIconSizingMode EmptySlotSizingMode => emptySlotSizingMode;
        public float EmptySlotAreaRatio => emptySlotAreaRatio;
        public RectOffset EmptySlotPadding => emptySlotPadding;
        public Vector2 EmptySlotMaximumSize => emptySlotMaximumSize;
        public RectTransform EmptySlotRect => emptySlotVisual != null ? emptySlotVisual.transform as RectTransform : null;
        public bool IsEmptySlotVisible => emptySlotVisual != null && emptySlotVisual.activeSelf;

        private void Awake()
        {
            ValidateLayoutSettings();
            ApplyIconLayout();
            ApplyEmptySlotLayout();
            ValidateEmptySlotReference();
            button?.onClick.AddListener(Activate);
        }

        private void OnEnable()
        {
            ApplyIconLayout();
            ApplyEmptySlotLayout();
        }

        private void OnRectTransformDimensionsChange()
        {
            ApplyIconLayout();
            ApplyEmptySlotLayout();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ValidateLayoutSettings();
            ApplyIconLayout();
            ApplyEmptySlotLayout();
        }
#endif

        public void ConfigureIconSizing(ActionGridIconSizingMode mode, float areaRatio = 0.9f,
            int left = 10, int right = 10, int top = 10, int bottom = 10)
        {
            iconSizingMode = mode;
            iconAreaRatio = NormalizeRatio(areaRatio);
            iconPadding = new RectOffset(Mathf.Max(0, left), Mathf.Max(0, right),
                Mathf.Max(0, top), Mathf.Max(0, bottom));
            ApplyIconLayout();
        }


        public void ConfigureIconRatioCappedSizing(float areaRatio, Vector2 maximumSize)
        {
            iconSizingMode = ActionGridIconSizingMode.RelativeWithMaxSize;
            iconAreaRatio = NormalizeRatio(areaRatio);
            iconMaximumSize = NormalizeMaximumSize(maximumSize);
            ApplyIconLayout();
        }
        public void ConfigureEmptySlotSizing(ActionGridIconSizingMode mode, float areaRatio = 0.9f,
            int left = 14, int right = 14, int top = 14, int bottom = 14)
        {
            emptySlotSizingMode = mode;
            emptySlotAreaRatio = NormalizeRatio(areaRatio);
            emptySlotPadding = new RectOffset(Mathf.Max(0, left), Mathf.Max(0, right),
                Mathf.Max(0, top), Mathf.Max(0, bottom));
            ApplyEmptySlotLayout();
        }

        public void ConfigureEmptySlotRatioCappedSizing(float areaRatio, Vector2 maximumSize)
        {
            emptySlotSizingMode = ActionGridIconSizingMode.RelativeWithMaxSize;
            emptySlotAreaRatio = NormalizeRatio(areaRatio);
            emptySlotMaximumSize = NormalizeMaximumSize(maximumSize);
            ApplyEmptySlotLayout();
        }
        public void ApplyEmptySlotLayout()
        {
            if (isApplyingEmptySlotLayout || emptySlotVisual == null) return;
            var emptySlotRect = emptySlotVisual.transform as RectTransform;
            var contentRect = emptySlotRect != null ? emptySlotRect.parent as RectTransform : null;
            if (emptySlotRect == null || contentRect == null) return;
            isApplyingEmptySlotLayout = true;
            try
            {
                ValidateLayoutSettings();
                emptySlotRect.pivot = new Vector2(0.5f, 0.5f);
                ApplySizingToRect(emptySlotRect, contentRect, emptySlotSizingMode,
                    emptySlotAreaRatio, emptySlotPadding, emptySlotMaximumSize);
            }
            finally { isApplyingEmptySlotLayout = false; }
        }
        public void ApplyIconLayout()
        {
            if (isApplyingIconLayout || icon == null) return;
            var iconRect = icon.rectTransform;
            var contentRect = iconRect.parent as RectTransform;
            if (contentRect == null) return;
            isApplyingIconLayout = true;
            try
            {
                ValidateLayoutSettings();
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                ApplySizingToRect(iconRect, contentRect, iconSizingMode, iconAreaRatio, iconPadding, iconMaximumSize);
            }
            finally { isApplyingIconLayout = false; }
        }

        public static Vector2 CalculateIconSize(Vector2 contentSize, ActionGridIconSizingMode mode,
            float areaRatio, RectOffset padding, Vector2 maximumSize = default)
        {
            var width = Mathf.Max(0f, contentSize.x);
            var height = Mathf.Max(0f, contentSize.y);
            if (mode == ActionGridIconSizingMode.RelativeToContent)
            {
                var ratio = NormalizeRatio(areaRatio);
                return new Vector2(width * ratio, height * ratio);
            }
            if (mode == ActionGridIconSizingMode.RelativeWithMaxSize)
            {
                var ratio = NormalizeRatio(areaRatio);
                var cap = NormalizeMaximumSize(maximumSize);
                return new Vector2(Mathf.Min(width * ratio, cap.x),
                    Mathf.Min(height * ratio, cap.y));
            }
            padding ??= new RectOffset();
            var horizontal = ClampPaddingPair(padding.left, padding.right, width);
            var vertical = ClampPaddingPair(padding.bottom, padding.top, height);
            return new Vector2(Mathf.Max(0f, width - horizontal.x - horizontal.y),
                Mathf.Max(0f, height - vertical.x - vertical.y));
        }


        public void Bind(int index, in ActionGridEntry entry, Action<int> onActivate)
        {
            Index = index;
            HasEntry = true;
            activate = onActivate;
            gameObject.name = $"ActionGridCell {index}: {entry.DisplayName}";

            if (icon != null)
            {
                icon.sprite = entry.Icon;
                icon.enabled = entry.Icon != null;
            }

            if (quantity != null)
            {
                quantity.text = entry.Quantity > 1 ? entry.Quantity.ToString() : string.Empty;
                quantity.gameObject.SetActive(entry.Quantity > 1);
            }

            if (cooldownOverlay != null)
            {
                cooldownOverlay.fillAmount = entry.CooldownNormalized;
                cooldownOverlay.gameObject.SetActive(entry.CooldownNormalized > 0f);
            }

            disabledOverlay?.SetActive(!entry.IsEnabled);
            emptySlotVisual?.SetActive(false);
            if (shortcutLabel != null)
            {
                shortcutLabel.text = entry.ShortcutLabel;
                shortcutLabel.gameObject.SetActive(!string.IsNullOrEmpty(entry.ShortcutLabel));
            }

            if (button != null)
            {
                button.interactable = true;
            }

            SetSelected(false);
        }

        public void BindEmpty(int index, Action<int> onActivate)
        {
            Index = index;
            HasEntry = false;
            activate = onActivate;
            gameObject.name = $"ActionGridCell {index}: Empty";
            if (icon != null)
            {
                icon.sprite = null;
                icon.enabled = false;
            }

            quantity?.gameObject.SetActive(false);
            cooldownOverlay?.gameObject.SetActive(false);
            disabledOverlay?.SetActive(false);
            shortcutLabel?.gameObject.SetActive(false);
            ApplyEmptySlotLayout();
            emptySlotVisual?.SetActive(true);
            if (button != null)
            {
                button.interactable = true;
            }

            SetSelected(false);
        }

        public void Unbind()
        {
            activate = null;
            HasEntry = false;
            if (icon != null) { icon.sprite = null; icon.enabled = false; }
            if (quantity != null) { quantity.text = string.Empty; quantity.gameObject.SetActive(false); }
            if (cooldownOverlay != null) { cooldownOverlay.fillAmount = 0f; cooldownOverlay.gameObject.SetActive(false); }
            disabledOverlay?.SetActive(false);
            selectionFrame?.SetActive(false);
            if (shortcutLabel != null) { shortcutLabel.text = string.Empty; shortcutLabel.gameObject.SetActive(false); }
            emptySlotVisual?.SetActive(false);
        }

        public void SetSelected(bool selected)
        {
            selectionFrame?.SetActive(selected);
        }

        public void ClearIcon()
        {
            if (icon == null) return;
            icon.sprite = null;
            icon.enabled = false;
        }

        private void ValidateLayoutSettings()
        {
            iconAreaRatio = NormalizeRatio(iconAreaRatio);
            iconPadding ??= new RectOffset(10, 10, 10, 10);
            iconPadding.left = Mathf.Max(0, iconPadding.left);
            iconPadding.right = Mathf.Max(0, iconPadding.right);
            iconPadding.top = Mathf.Max(0, iconPadding.top);
            iconPadding.bottom = Mathf.Max(0, iconPadding.bottom);
            emptySlotAreaRatio = NormalizeRatio(emptySlotAreaRatio);
            emptySlotPadding ??= new RectOffset(14, 14, 14, 14);
            emptySlotPadding.left = Mathf.Max(0, emptySlotPadding.left);
            emptySlotPadding.right = Mathf.Max(0, emptySlotPadding.right);
            emptySlotPadding.top = Mathf.Max(0, emptySlotPadding.top);
            emptySlotPadding.bottom = Mathf.Max(0, emptySlotPadding.bottom);
            iconMaximumSize = NormalizeMaximumSize(iconMaximumSize);
            emptySlotMaximumSize = NormalizeMaximumSize(emptySlotMaximumSize);
        }


        private static void ApplySizingToRect(RectTransform target, RectTransform parent,
            ActionGridIconSizingMode mode, float areaRatio, RectOffset padding, Vector2 maximumSize)
        {
            if (mode == ActionGridIconSizingMode.RelativeWithMaxSize)
            {
                var size = CalculateIconSize(parent.rect.size, mode, areaRatio, padding, maximumSize);
                target.anchorMin = new Vector2(0.5f, 0.5f);
                target.anchorMax = new Vector2(0.5f, 0.5f);
                target.anchoredPosition = Vector2.zero;
                target.sizeDelta = size;
                return;
            }

            if (mode == ActionGridIconSizingMode.RelativeToContent)
            {
                var inset = (1f - NormalizeRatio(areaRatio)) * 0.5f;
                target.anchorMin = new Vector2(inset, inset);
                target.anchorMax = new Vector2(1f - inset, 1f - inset);
                target.anchoredPosition = Vector2.zero;
                target.sizeDelta = Vector2.zero;
                return;
            }

            padding ??= new RectOffset();
            var horizontal = ClampPaddingPair(padding.left, padding.right, parent.rect.width);
            var vertical = ClampPaddingPair(padding.bottom, padding.top, parent.rect.height);
            target.anchorMin = Vector2.zero;
            target.anchorMax = Vector2.one;
            target.offsetMin = new Vector2(horizontal.x, vertical.x);
            target.offsetMax = new Vector2(-horizontal.y, -vertical.y);
        }

        private void ValidateEmptySlotReference()
        {
            if (emptySlotVisual == null || emptySlotVisual.transform is RectTransform) return;
            if (didWarnAboutMissingEmptySlotRect) return;
            didWarnAboutMissingEmptySlotRect = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("ActionGridCell EmptySlot reference must use a RectTransform.", this);
#endif
        }
        private static Vector2 NormalizeMaximumSize(Vector2 value)
        {
            return new Vector2(NormalizeMaximumAxis(value.x), NormalizeMaximumAxis(value.y));
        }

        private static float NormalizeMaximumAxis(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return 64f;
            return Mathf.Max(0f, value);
        }
        private static float NormalizeRatio(float value) =>
            float.IsNaN(value) || float.IsInfinity(value) ? 0.9f : Mathf.Clamp01(value);

        private static Vector2 ClampPaddingPair(float first, float second, float available)
        {
            first = Mathf.Max(0f, first);
            second = Mathf.Max(0f, second);
            available = Mathf.Max(0f, available);
            var total = first + second;
            if (total <= available || total <= 0f) return new Vector2(first, second);
            var scale = available / total;
            return new Vector2(first * scale, second * scale);
        }

        private void Activate()
        {
            activate?.Invoke(Index);
        }
    }
}
