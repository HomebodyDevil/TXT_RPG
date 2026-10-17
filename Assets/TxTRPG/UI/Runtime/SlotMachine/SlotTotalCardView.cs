using System;
using TMPro;
using TxTRPG.UI.Shapes;
using UnityEngine;

namespace TxTRPG.UI.SlotMachine
{
    public enum SlotTotalCardCornerRadiusUnit
    {
        // Fraction of the card's short side, [0, 0.5].
        ShortSideRatio = 0,
        // Canvas units, [0, short side / 2].
        Pixels = 1
    }

    // Look of the total card. Triangle (or an unknown value) is drawn as Rectangle with one warning.
    [Serializable]
    public struct SlotTotalCardAppearance : IEquatable<SlotTotalCardAppearance>
    {
        [SerializeField, Tooltip("Rectangle, RoundedRectangle, Circle or Ellipse. Triangle is drawn as Rectangle.")]
        private UiShape shape;
        [SerializeField] private SlotTotalCardCornerRadiusUnit cornerRadiusUnit;
        [SerializeField, Min(0f), Tooltip("RoundedRectangle only. ShortSideRatio: [0, 0.5] of the short side. Pixels: [0, short side / 2].")]
        private float cornerRadius;
        [SerializeField, Range(UiShapeGeometry.MinimumCurveSegments, UiShapeGeometry.MaximumCurveSegments)]
        private int curveSegments;
        [SerializeField] private Color fillColor;
        [SerializeField, Tooltip("Inner border; it never changes the card size.")] private bool borderEnabled;
        [SerializeField] private Color borderColor;
        [SerializeField, Min(0f)] private float borderThickness;
        [SerializeField, Tooltip("Total label and value text color; keep it readable on the fill color.")]
        private Color textColor;

        public SlotTotalCardAppearance(UiShape shape, SlotTotalCardCornerRadiusUnit cornerRadiusUnit, float cornerRadius, int curveSegments,
            Color fillColor, bool borderEnabled, Color borderColor, float borderThickness, Color textColor)
        {
            this.shape = shape;
            this.cornerRadiusUnit = cornerRadiusUnit;
            this.cornerRadius = cornerRadius;
            this.curveSegments = curveSegments;
            this.fillColor = fillColor;
            this.borderEnabled = borderEnabled;
            this.borderColor = borderColor;
            this.borderThickness = borderThickness;
            this.textColor = textColor;
        }

        // White rectangle with dark text; the 0.2 short-side radius is used once the shape becomes RoundedRectangle.
        public static SlotTotalCardAppearance Default => new(UiShape.Rectangle, SlotTotalCardCornerRadiusUnit.ShortSideRatio, .2f, 8,
            Color.white, false, new Color32(10, 10, 10, 255), 3f, new Color32(10, 10, 10, 255));

        public UiShape Shape => shape;
        public SlotTotalCardCornerRadiusUnit CornerRadiusUnit => cornerRadiusUnit;
        public float CornerRadius => cornerRadius;
        public int CurveSegments => curveSegments;
        public Color FillColor => fillColor;
        public bool BorderEnabled => borderEnabled;
        public Color BorderColor => borderColor;
        public float BorderThickness => borderThickness;
        public Color TextColor => textColor;

        public SlotTotalCardAppearance WithShape(UiShape value) { var copy = this; copy.shape = value; return copy; }
        public SlotTotalCardAppearance WithCornerRadius(SlotTotalCardCornerRadiusUnit unit, float value)
        {
            var copy = this;
            copy.cornerRadiusUnit = unit;
            copy.cornerRadius = value;
            return copy;
        }
        public SlotTotalCardAppearance WithBorder(bool enabled, Color color, float thickness)
        {
            var copy = this;
            copy.borderEnabled = enabled;
            copy.borderColor = color;
            copy.borderThickness = thickness;
            return copy;
        }

        // Shape actually drawn: Triangle and unknown values become Rectangle.
        public static UiShape ResolveShape(UiShape value) =>
            value == UiShape.Rectangle || value == UiShape.RoundedRectangle || value == UiShape.Circle || value == UiShape.Ellipse
                ? value : UiShape.Rectangle;

        // Corner radius in canvas units for a card of the given size, clamped to the unit's range.
        public float ResolveCornerRadiusPixels(Vector2 size)
        {
            var shortSide = Mathf.Max(0f, Mathf.Min(size.x, size.y));
            var value = float.IsNaN(cornerRadius) ? 0f : Mathf.Max(0f, cornerRadius);
            return cornerRadiusUnit == SlotTotalCardCornerRadiusUnit.Pixels
                ? Mathf.Min(value, shortSide * .5f)
                : Mathf.Min(value, .5f) * shortSide;
        }

        public bool Equals(SlotTotalCardAppearance other) =>
            shape == other.shape && cornerRadiusUnit == other.cornerRadiusUnit && cornerRadius.Equals(other.cornerRadius) &&
            curveSegments == other.curveSegments && fillColor == other.fillColor && borderEnabled == other.borderEnabled &&
            borderColor == other.borderColor && borderThickness.Equals(other.borderThickness) && textColor == other.textColor;

        public override bool Equals(object obj) => obj is SlotTotalCardAppearance other && Equals(other);
        public override int GetHashCode() => HashCode.Combine((int)shape, (int)cornerRadiusUnit, cornerRadius, curveSegments, fillColor, borderEnabled, borderThickness);
    }

    // The total card of SlotReelDock (TotalCard, a layout-only root): a code-drawn shape with the total label, value and
    // Fortune Critical multiplier, placed across the reel machine's top edge. Its size and position follow the reel
    // machine's pre-effect RectTransform and are recalculated only when that size or a setting changes (SlotReelDock calls
    // Refresh after its layout). Effects attach to VisualRoot only; every graphic has raycastTarget off.
    // With a large heightRatio or a small overhangRatio the card may cover the upper reel cells and, at the extreme, the
    // payline; the defaults keep its bottom edge above the payline cell.
    [DisallowMultipleComponent]
    public sealed class SlotTotalCardView : MonoBehaviour
    {
        [SerializeField, Tooltip("Layout-only reel machine RectTransform (a sibling under the same parent) the card is placed against.")]
        private RectTransform reelMachine;
        [SerializeField, Tooltip("Effect target. Layout never moves or scales it.")] private RectTransform visualRoot;
        [SerializeField] private UiShapeGraphic fillGraphic;
        [SerializeField, Tooltip("Inner border; its GameObject is active only while the border is enabled.")]
        private UiShapeGraphic borderGraphic;
        [SerializeField, Tooltip("Text area inside the shape (inscribed rectangle for Circle and Ellipse).")]
        private RectTransform contentRoot;
        [SerializeField] private TMP_Text totalLabelText;
        [SerializeField] private TMP_Text totalValueText;
        [SerializeField, Tooltip("Layout-only root (ContentRoot/TotalMultiplier). Inactive while no multiplier is shown.")]
        private RectTransform totalMultiplierRoot;
        [SerializeField] private TMP_Text totalMultiplierText;
        [SerializeField, Tooltip("Places TotalMultiplier beside or above the value glyphs (on ContentRoot).")]
        private SlotTotalMultiplierLayout totalMultiplierLayout;

        [Header("Appearance")]
        [SerializeField] private SlotTotalCardAppearance appearance = SlotTotalCardAppearance.Default;

        [Header("Size and position (relative to the reel machine)")]
        [SerializeField, Range(.05f, 1.5f), Tooltip("Card height = reel machine height × ratio, then clamped. A large value may cover upper reel cells.")]
        private float heightRatio = .42f;
        [SerializeField, Min(1f)] private float minimumHeight = 56f;
        [SerializeField, Min(1f)] private float maximumHeight = 140f;
        [SerializeField, Min(.1f), Tooltip("Card width = height × ratio (1 for Circle), at most 90% of the reel machine width.")]
        private float aspectRatio = 1.8f;
        [SerializeField, Range(0f, 1f), Tooltip("Card center across the reel machine width (0 left, 1 right); the card stays inside the machine.")]
        private float horizontalPosition = .5f;
        [SerializeField, Range(0f, 1f), Tooltip("Part of the card height above the reel machine's top edge. 0 puts the card inside the machine and may cover the payline.")]
        private float overhangRatio = .5f;

        private const float MaximumWidthRatio = .9f;
        private bool warnedShape;
        private bool warnedFill;
        private bool hasLayout;
        private Vector2 laidOutMachineSize;
        private Vector2 laidOutMachinePosition;
        private Vector2 appliedCardSize = new(float.NaN, float.NaN);

        public SlotTotalCardAppearance Appearance => appearance;
        public RectTransform ReelMachine => reelMachine;
        public RectTransform VisualRoot => visualRoot;
        public RectTransform ContentRoot => contentRoot;
        public UiShapeGraphic FillGraphic => fillGraphic;
        public UiShapeGraphic BorderGraphic => borderGraphic;
        public float OverhangRatio => overhangRatio;
        public string TotalLabelText => totalLabelText != null ? totalLabelText.text : string.Empty;
        public string TotalValueText => totalValueText != null ? totalValueText.text : string.Empty;
        // Total Fortune Critical multiplier currently shown; empty when hidden.
        public string TotalMultiplierText => totalMultiplierRoot != null && totalMultiplierRoot.gameObject.activeSelf && totalMultiplierText != null
            ? totalMultiplierText.text : string.Empty;

        private void OnEnable()
        {
            hasLayout = false;
            Refresh();
        }

        // Card height for a reel machine height, within [minimumHeight, maximumHeight].
        public float CalculateHeight(float reelMachineHeight) =>
            Mathf.Clamp(Mathf.Max(0f, reelMachineHeight) * heightRatio, minimumHeight, Mathf.Max(minimumHeight, maximumHeight));

        // Height above the reel machine's top edge; SlotReelDock reserves it above the reel machine.
        public float CalculateOverhang(float reelMachineHeight) => CalculateHeight(reelMachineHeight) * overhangRatio;

        public Vector2 CalculateSize(Vector2 reelMachineSize)
        {
            var height = CalculateHeight(reelMachineSize.y);
            var ratio = SlotTotalCardAppearance.ResolveShape(appearance.Shape) == UiShape.Circle ? 1f : aspectRatio;
            var width = Mathf.Min(height * ratio, Mathf.Max(0f, reelMachineSize.x) * MaximumWidthRatio);
            return new Vector2(width, height);
        }

        // Text area inside a card rectangle (card-local, centered) for the current appearance.
        public Rect CalculateContentRect(Vector2 cardSize)
        {
            var shape = SlotTotalCardAppearance.ResolveShape(appearance.Shape);
            var shortSide = Mathf.Max(0f, Mathf.Min(cardSize.x, cardSize.y));
            Vector2 size;
            switch (shape)
            {
                case UiShape.Ellipse:
                    size = cardSize * .707f;
                    break;
                case UiShape.Circle:
                    size = Vector2.one * (shortSide * .707f);
                    break;
                default:
                    var inset = shortSide * .08f;
                    if (shape == UiShape.RoundedRectangle) inset += appearance.ResolveCornerRadiusPixels(cardSize) * .3f;
                    size = new Vector2(Mathf.Max(0f, cardSize.x - inset * 2f), Mathf.Max(0f, cardSize.y - inset * 2f));
                    break;
            }
            return new Rect(-size * .5f, size);
        }

        // Places the card against the reel machine. Without force it returns early when the machine size and position are
        // unchanged since the last layout.
        public void Refresh(bool force = false)
        {
            if (reelMachine == null || !(transform is RectTransform card) || !(card.parent is RectTransform parent) || reelMachine.parent != parent) return;
            var machine = reelMachine.rect;
            var machinePosition = (Vector2)reelMachine.localPosition;
            if (!force && hasLayout && machine.size == laidOutMachineSize && machinePosition == laidOutMachinePosition) return;
            hasLayout = true;
            laidOutMachineSize = machine.size;
            laidOutMachinePosition = machinePosition;
            var size = CalculateSize(machine.size);
            var min = machinePosition + machine.min;
            var halfWidth = size.x * .5f;
            var centerX = min.x + Mathf.Clamp(machine.width * horizontalPosition, halfWidth, Mathf.Max(halfWidth, machine.width - halfWidth));
            var top = min.y + machine.height + size.y * overhangRatio;
            var center = new Vector2(centerX, top - size.y * .5f);
            var middle = new Vector2(.5f, .5f);
            if (card.anchorMin != middle) card.anchorMin = middle;
            if (card.anchorMax != middle) card.anchorMax = middle;
            if (card.pivot != middle) card.pivot = middle;
            if (card.sizeDelta != size) card.sizeDelta = size;
            var anchored = center - parent.rect.center;
            if (card.anchoredPosition != anchored) card.anchoredPosition = anchored;
            ApplyAppearance(size, force);
        }

        // Changes the look at runtime. The same value does nothing; otherwise only the shapes and the text area are
        // recalculated (Circle also changes the card width).
        public void SetAppearance(SlotTotalCardAppearance value)
        {
            if (appearance.Equals(value)) return;
            appearance = value;
            warnedShape = false;
            Refresh(true);
            if (!hasLayout && transform is RectTransform card) ApplyAppearance(card.rect.size, true);
        }

        public void SetTotal(string label, string value, string multiplier)
        {
            SetText(totalLabelText, label);
            SetText(totalValueText, value);
            multiplier ??= string.Empty;
            SetText(totalMultiplierText, multiplier);
            if (totalMultiplierRoot != null && totalMultiplierRoot.gameObject.activeSelf != multiplier.Length > 0)
                totalMultiplierRoot.gameObject.SetActive(multiplier.Length > 0);
            // The value text was set just before, so the placement follows the current value glyphs.
            if (multiplier.Length > 0 && totalMultiplierLayout != null) totalMultiplierLayout.Refresh();
        }

        private void ApplyAppearance(Vector2 size, bool force)
        {
            if (!force && size == appliedCardSize) return;
            appliedCardSize = size;
            var shape = SlotTotalCardAppearance.ResolveShape(appearance.Shape);
            if (shape != appearance.Shape && !warnedShape)
            {
                warnedShape = true;
                Debug.LogWarning($"{name}: total card shape '{appearance.Shape}' is not supported; it is drawn as Rectangle.", this);
            }
            var radius = shape == UiShape.RoundedRectangle ? appearance.ResolveCornerRadiusPixels(size) : 0f;
            if (fillGraphic != null)
            {
                fillGraphic.Configure(shape, radius, UiShapeDrawMode.Fill, 0f);
                fillGraphic.SetCurveSegments(appearance.CurveSegments);
                if (fillGraphic.color != appearance.FillColor) fillGraphic.color = appearance.FillColor;
                if (fillGraphic.raycastTarget) fillGraphic.raycastTarget = false;
            }
            else if (!warnedFill)
            {
                warnedFill = true;
                Debug.LogWarning($"{name}: the total card has no fill UiShapeGraphic; only its text is shown.", this);
            }
            if (borderGraphic != null)
            {
                var border = appearance.BorderEnabled && appearance.BorderThickness > 0f;
                if (border)
                {
                    borderGraphic.Configure(shape, radius, UiShapeDrawMode.InnerBorder, appearance.BorderThickness);
                    borderGraphic.SetCurveSegments(appearance.CurveSegments);
                    if (borderGraphic.color != appearance.BorderColor) borderGraphic.color = appearance.BorderColor;
                }
                if (borderGraphic.raycastTarget) borderGraphic.raycastTarget = false;
                if (borderGraphic.gameObject.activeSelf != border) borderGraphic.gameObject.SetActive(border);
            }
            SetColor(totalLabelText, appearance.TextColor);
            SetColor(totalValueText, appearance.TextColor);
            if (contentRoot != null)
            {
                var content = CalculateContentRect(size);
                var middle = new Vector2(.5f, .5f);
                if (contentRoot.anchorMin != middle) contentRoot.anchorMin = middle;
                if (contentRoot.anchorMax != middle) contentRoot.anchorMax = middle;
                if (contentRoot.pivot != middle) contentRoot.pivot = middle;
                if (contentRoot.sizeDelta != content.size) contentRoot.sizeDelta = content.size;
                if (contentRoot.anchoredPosition != content.center) contentRoot.anchoredPosition = content.center;
            }
        }

        private static void SetColor(TMP_Text text, Color color)
        {
            if (text != null && text.color != color) text.color = color;
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text == null) return;
            value ??= string.Empty;
            if (text.text != value) text.text = value;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            maximumHeight = Mathf.Max(minimumHeight, maximumHeight);
            // OnValidate also runs when a Scene loads, so Edit Mode refreshes only while the Prefab is open in Prefab Mode;
            // otherwise a Scene instance would get layout values as overrides. Hierarchy changes (the border GameObject)
            // are not allowed inside OnValidate, so the refresh runs afterwards.
            if (!UnityEngine.Application.isPlaying && UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(gameObject) == null) return;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null && isActiveAndEnabled) Refresh(true);
            };
        }

        public void ConfigureForEditor(RectTransform targetReelMachine, RectTransform targetVisualRoot, UiShapeGraphic targetFill,
            UiShapeGraphic targetBorder, RectTransform targetContent, TMP_Text targetLabel, TMP_Text targetValue,
            RectTransform targetMultiplierRoot, TMP_Text targetMultiplierText, SlotTotalMultiplierLayout targetMultiplierLayout)
        {
            reelMachine = targetReelMachine;
            visualRoot = targetVisualRoot;
            fillGraphic = targetFill;
            borderGraphic = targetBorder;
            contentRoot = targetContent;
            totalLabelText = targetLabel;
            totalValueText = targetValue;
            totalMultiplierRoot = targetMultiplierRoot;
            totalMultiplierText = targetMultiplierText;
            totalMultiplierLayout = targetMultiplierLayout;
            hasLayout = false;
        }

        public RectTransform TotalMultiplierRootForEditor => totalMultiplierRoot;
        public SlotTotalMultiplierLayout TotalMultiplierLayoutForEditor => totalMultiplierLayout;
#endif
    }
}
