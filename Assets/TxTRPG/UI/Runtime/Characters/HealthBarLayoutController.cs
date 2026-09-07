using System;
using UnityEngine;

namespace TxTRPG.UI
{
    public enum HealthBarHorizontalAlignment { Left, Center, Right }
    public enum HealthBarVerticalAlignment { Bottom, Middle, Top }
    public enum HealthBarAxisSizeMode { Fixed, Stretch }

    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class HealthBarLayoutController : MonoBehaviour
    {
        [SerializeField] private RectTransform referenceArea;
        [SerializeField] private RectTransform barRoot;
        [SerializeField] private HealthBarHorizontalAlignment horizontalAlignment = HealthBarHorizontalAlignment.Center;
        [SerializeField] private HealthBarVerticalAlignment verticalAlignment = HealthBarVerticalAlignment.Middle;
        [SerializeField] private HealthBarAxisSizeMode horizontalSizeMode = HealthBarAxisSizeMode.Stretch;
        [SerializeField] private HealthBarAxisSizeMode verticalSizeMode = HealthBarAxisSizeMode.Stretch;
        [SerializeField] private Vector2 fixedSize = new(240f, 24f);
        [SerializeField, Min(0)] private int paddingLeft = 12;
        [SerializeField, Min(0)] private int paddingRight = 12;
        [SerializeField, Min(0)] private int paddingTop = 12;
        [SerializeField, Min(0)] private int paddingBottom = 12;
        [SerializeField] private Vector2 offset;

        public RectTransform ReferenceArea => ResolveReferenceArea();
        public RectTransform BarRoot => barRoot;
        public HealthBarHorizontalAlignment HorizontalAlignment => horizontalAlignment;
        public HealthBarVerticalAlignment VerticalAlignment => verticalAlignment;
        public HealthBarAxisSizeMode HorizontalSizeMode => horizontalSizeMode;
        public HealthBarAxisSizeMode VerticalSizeMode => verticalSizeMode;
        public Vector2 FixedSize => fixedSize;
        public RectOffset Padding => CreatePadding();
        public Vector2 Offset => offset;

        public void Configure(
            RectTransform configuredReferenceArea,
            RectTransform configuredBarRoot,
            HealthBarHorizontalAlignment horizontal,
            HealthBarVerticalAlignment vertical,
            HealthBarAxisSizeMode horizontalMode,
            HealthBarAxisSizeMode verticalMode,
            Vector2 configuredFixedSize,
            RectOffset configuredPadding,
            Vector2 configuredOffset)
        {
            referenceArea = configuredReferenceArea;
            barRoot = configuredBarRoot;
            horizontalAlignment = horizontal;
            verticalAlignment = vertical;
            horizontalSizeMode = horizontalMode;
            verticalSizeMode = verticalMode;
            fixedSize = ClampSize(configuredFixedSize);
            SetPaddingValues(configuredPadding);
            offset = configuredOffset;
            ApplyLayout();
        }

        public void SetAlignment(
            HealthBarHorizontalAlignment horizontal,
            HealthBarVerticalAlignment vertical)
        {
            horizontalAlignment = horizontal;
            verticalAlignment = vertical;
            ApplyLayout();
        }

        public void SetSizeModes(HealthBarAxisSizeMode horizontal, HealthBarAxisSizeMode vertical)
        {
            horizontalSizeMode = horizontal;
            verticalSizeMode = vertical;
            ApplyLayout();
        }

        public void SetFixedSize(float width, float height)
        {
            fixedSize = new Vector2(SafeNonNegative(width), SafeNonNegative(height));
            ApplyLayout();
        }

        public void SetPadding(int left, int right, int top, int bottom)
        {
            paddingLeft = Mathf.Max(0, left);
            paddingRight = Mathf.Max(0, right);
            paddingTop = Mathf.Max(0, top);
            paddingBottom = Mathf.Max(0, bottom);
            ApplyLayout();
        }

        public void SetOffset(Vector2 value)
        {
            offset = new Vector2(SafeFinite(value.x), SafeFinite(value.y));
            ApplyLayout();
        }

        public void ApplyLayout()
        {
            var root = transform as RectTransform;
            var reference = ResolveReferenceArea();
            if (root == null || barRoot == null || barRoot.parent != root || reference == barRoot ||
                reference.IsChildOf(barRoot))
            {
                return;
            }

            var referenceRect = GetRectInRootSpace(reference, root);
            var target = CalculateLayout(
                referenceRect,
                horizontalAlignment,
                verticalAlignment,
                horizontalSizeMode,
                verticalSizeMode,
                fixedSize,
                CreatePadding(),
                offset);
            var anchorPosition = root.rect.center;
            SetIfDifferent(barRoot, target.size, target.center - anchorPosition);
        }

        public static Rect CalculateLayout(
            Rect referenceRect,
            HealthBarHorizontalAlignment horizontal,
            HealthBarVerticalAlignment vertical,
            HealthBarAxisSizeMode horizontalMode,
            HealthBarAxisSizeMode verticalMode,
            Vector2 requestedFixedSize,
            RectOffset requestedPadding,
            Vector2 requestedOffset)
        {
            var safePadding = CopyPadding(requestedPadding);
            NormalizeInsets(referenceRect.width, safePadding.left, safePadding.right,
                out var left, out var right);
            NormalizeInsets(referenceRect.height, safePadding.bottom, safePadding.top,
                out var bottom, out var top);
            var xMin = referenceRect.xMin + left;
            var xMax = referenceRect.xMax - right;
            var yMin = referenceRect.yMin + bottom;
            var yMax = referenceRect.yMax - top;

            var availableWidth = Mathf.Max(0f, xMax - xMin);
            var availableHeight = Mathf.Max(0f, yMax - yMin);
            var width = horizontalMode == HealthBarAxisSizeMode.Stretch
                ? availableWidth
                : Mathf.Min(SafeNonNegative(requestedFixedSize.x), availableWidth);
            var height = verticalMode == HealthBarAxisSizeMode.Stretch
                ? availableHeight
                : Mathf.Min(SafeNonNegative(requestedFixedSize.y), availableHeight);

            var centerX = horizontal switch
            {
                HealthBarHorizontalAlignment.Left => xMin + width * 0.5f,
                HealthBarHorizontalAlignment.Right => xMax - width * 0.5f,
                _ => (xMin + xMax) * 0.5f
            };
            var centerY = vertical switch
            {
                HealthBarVerticalAlignment.Bottom => yMin + height * 0.5f,
                HealthBarVerticalAlignment.Top => yMax - height * 0.5f,
                _ => (yMin + yMax) * 0.5f
            };
            return new Rect(
                centerX - width * 0.5f + SafeFinite(requestedOffset.x),
                centerY - height * 0.5f + SafeFinite(requestedOffset.y),
                width,
                height);
        }

        private void OnEnable() => ApplyLayout();
        private void OnRectTransformDimensionsChange() => ApplyLayout();
        private void OnTransformParentChanged() => ApplyLayout();
        private void OnDidApplyAnimationProperties() => ApplyLayout();

#if UNITY_EDITOR
        private void OnValidate()
        {
            fixedSize = ClampSize(fixedSize);
            ClampPadding();
            ApplyLayout();
        }
#endif

        private RectTransform ResolveReferenceArea()
        {
            var root = transform as RectTransform;
            if (root == null) return null;
            return referenceArea != null && referenceArea != barRoot && !referenceArea.IsChildOf(barRoot) &&
                   (referenceArea == root || referenceArea.IsChildOf(root))
                ? referenceArea
                : root;
        }

        private static Rect GetRectInRootSpace(RectTransform source, RectTransform root)
        {
            var corners = new Vector3[4];
            source.GetWorldCorners(corners);
            var min = root.InverseTransformPoint(corners[0]);
            var max = root.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static void SetIfDifferent(RectTransform target, Vector2 size, Vector2 position)
        {
            if (target.anchorMin != new Vector2(0.5f, 0.5f)) target.anchorMin = new Vector2(0.5f, 0.5f);
            if (target.anchorMax != new Vector2(0.5f, 0.5f)) target.anchorMax = new Vector2(0.5f, 0.5f);
            if (target.pivot != new Vector2(0.5f, 0.5f)) target.pivot = new Vector2(0.5f, 0.5f);
            if ((target.sizeDelta - size).sqrMagnitude > 0.0001f) target.sizeDelta = size;
            if ((target.anchoredPosition - position).sqrMagnitude > 0.0001f) target.anchoredPosition = position;
        }

        private static void NormalizeInsets(
            float availableSize,
            float first,
            float second,
            out float normalizedFirst,
            out float normalizedSecond)
        {
            var size = SafeNonNegative(availableSize);
            normalizedFirst = SafeNonNegative(first);
            normalizedSecond = SafeNonNegative(second);
            var sum = normalizedFirst + normalizedSecond;
            if (sum <= size || sum <= 0f) return;
            var scale = size / sum;
            normalizedFirst *= scale;
            normalizedSecond *= scale;
        }

        private static Vector2 ClampSize(Vector2 value) =>
            new(SafeNonNegative(value.x), SafeNonNegative(value.y));

        private static float SafeNonNegative(float value) =>
            float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Max(0f, value);

        private static float SafeFinite(float value) =>
            float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;

        private static RectOffset CopyPadding(RectOffset value) => value == null
            ? new RectOffset()
            : new RectOffset(
                Mathf.Max(0, value.left), Mathf.Max(0, value.right),
                Mathf.Max(0, value.top), Mathf.Max(0, value.bottom));

        private RectOffset CreatePadding() =>
            new(paddingLeft, paddingRight, paddingTop, paddingBottom);

        private void SetPaddingValues(RectOffset value)
        {
            var safe = CopyPadding(value);
            paddingLeft = safe.left;
            paddingRight = safe.right;
            paddingTop = safe.top;
            paddingBottom = safe.bottom;
        }

        private void ClampPadding()
        {
            paddingLeft = Mathf.Max(0, paddingLeft);
            paddingRight = Mathf.Max(0, paddingRight);
            paddingTop = Mathf.Max(0, paddingTop);
            paddingBottom = Mathf.Max(0, paddingBottom);
        }
    }
}
