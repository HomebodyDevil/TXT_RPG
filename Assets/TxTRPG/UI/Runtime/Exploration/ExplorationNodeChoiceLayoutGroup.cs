using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.Exploration
{
    [DisallowMultipleComponent]
    public sealed class ExplorationNodeChoiceLayoutGroup : LayoutGroup
    {
        [Header("Card Layout")]
        [SerializeField, Min(120f), Tooltip("Card slot width. Cards wrap instead of shrinking below this value.")] private float cardWidth = 210f;
        [SerializeField, Min(160f), Tooltip("Card slot height.")] private float cardHeight = 300f;
        [SerializeField, Min(0f), Tooltip("Space between adjacent cards only.")] private float horizontalSpacing = 18f;
        [SerializeField, Min(0f), Tooltip("Space between rows only.")] private float verticalSpacing = 18f;

        public float CardWidth => cardWidth;
        public float CardHeight => cardHeight;
        public int ColumnCount { get; private set; } = 1;
        public float RequiredContentHeight { get; private set; }

        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();
            var count = rectChildren.Count;
            SetLayoutInputForAxis(padding.horizontal + (count > 0 ? cardWidth : 0f), -1f, -1f, 0);
        }

        public override void CalculateLayoutInputVertical()
        {
            var rows = Mathf.CeilToInt(rectChildren.Count / (float)Mathf.Max(1, CalculateColumns()));
            RequiredContentHeight = padding.vertical + rows * cardHeight + Mathf.Max(0, rows - 1) * verticalSpacing;
            var viewportHeight = rectTransform.parent is RectTransform parent ? parent.rect.height : 0f;
            var height = Mathf.Max(RequiredContentHeight, viewportHeight);
            SetLayoutInputForAxis(height, height, -1f, 1);
        }

        public override void SetLayoutHorizontal() => Arrange();
        public override void SetLayoutVertical() => Arrange();

        private int CalculateColumns()
        {
            var available = Mathf.Max(0f, rectTransform.rect.width - padding.horizontal);
            ColumnCount = Mathf.Max(1, Mathf.FloorToInt((available + horizontalSpacing) / (cardWidth + horizontalSpacing)));
            return ColumnCount;
        }

        private void Arrange()
        {
            var columns = CalculateColumns();
            var rows = Mathf.CeilToInt(rectChildren.Count / (float)columns);
            var requiredHeight = padding.vertical + rows * cardHeight + Mathf.Max(0, rows - 1) * verticalSpacing;
            var availableVertical = Mathf.Max(0f, rectTransform.rect.height - requiredHeight);
            var startY = padding.top + availableVertical * GetVerticalFactor(childAlignment);
            for (var i = 0; i < rectChildren.Count; i++)
            {
                var row = i / columns;
                var indexInRow = i % columns;
                var remaining = rectChildren.Count - row * columns;
                var rowCount = Mathf.Min(columns, remaining);
                var rowWidth = rowCount * cardWidth + Mathf.Max(0, rowCount - 1) * horizontalSpacing;
                var startX = padding.left + Mathf.Max(0f, rectTransform.rect.width - padding.horizontal - rowWidth) * GetHorizontalFactor(childAlignment);
                SetChildAlongAxis(rectChildren[i], 0, startX + indexInRow * (cardWidth + horizontalSpacing), cardWidth);
                SetChildAlongAxis(rectChildren[i], 1, startY + row * (cardHeight + verticalSpacing), cardHeight);
            }
        }

        private static float GetHorizontalFactor(TextAnchor value) => value is TextAnchor.UpperCenter or TextAnchor.MiddleCenter or TextAnchor.LowerCenter ? .5f : value is TextAnchor.UpperRight or TextAnchor.MiddleRight or TextAnchor.LowerRight ? 1f : 0f;
        private static float GetVerticalFactor(TextAnchor value) => value is TextAnchor.MiddleLeft or TextAnchor.MiddleCenter or TextAnchor.MiddleRight ? .5f : value is TextAnchor.LowerLeft or TextAnchor.LowerCenter or TextAnchor.LowerRight ? 1f : 0f;

        public void Configure(float width, float height, float spacingX, float spacingY, RectOffset targetPadding, TextAnchor alignment = TextAnchor.UpperCenter)
        {
            cardWidth = Mathf.Max(120f, width); cardHeight = Mathf.Max(160f, height);
            horizontalSpacing = Mathf.Max(0f, spacingX); verticalSpacing = Mathf.Max(0f, spacingY);
            padding = targetPadding ?? new RectOffset(); childAlignment = alignment; SetDirty();
        }

        public void SetAlignment(TextAnchor value) { childAlignment = value; SetDirty(); }
        public void SetCardSize(Vector2 value) { cardWidth = Mathf.Max(120f, value.x); cardHeight = Mathf.Max(160f, value.y); SetDirty(); }
        public void SetSpacing(Vector2 value) { horizontalSpacing = Mathf.Max(0f, value.x); verticalSpacing = Mathf.Max(0f, value.y); SetDirty(); }
        public void SetPadding(RectOffset value) { padding = value ?? new RectOffset(); SetDirty(); }
    }
}
