using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.Windows
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class GameMenuLayoutGroup : LayoutGroup
    {
        [SerializeField] private RectTransform viewport;
        [SerializeField] private GameMenuLayoutMode layoutMode = GameMenuLayoutMode.HorizontalScroll;
        [SerializeField] private Vector2 buttonSize = new(150f, 48f);
        [SerializeField] private Vector2 spacing = new(12f, 12f);
        [SerializeField] private GameMenuHorizontalAlignment horizontalAlignment = GameMenuHorizontalAlignment.Center;
        [SerializeField] private GameMenuVerticalAlignment verticalAlignment = GameMenuVerticalAlignment.Center;
        [SerializeField] private GameMenuWrapColumnPolicy wrapColumnPolicy = GameMenuWrapColumnPolicy.AutoFit;
        [SerializeField, Min(1)] private int maximumColumns = 4;

        private readonly List<RectTransform> visibleChildren = new();
        private GameMenuLayoutResult current;

        public GameMenuLayoutResult Current => current;
        public Vector2 ButtonSize => buttonSize;
        public Vector2 Spacing => spacing;
        public GameMenuLayoutMode LayoutMode => layoutMode;

        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();
            RefreshVisibleChildren();
            Calculate();
            SetLayoutInputForAxis(current.RequiredWidth, current.RequiredWidth, -1f, 0);
        }

        public override void CalculateLayoutInputVertical()
        {
            Calculate();
            SetLayoutInputForAxis(current.RequiredHeight, current.RequiredHeight, -1f, 1);
        }

        public override void SetLayoutHorizontal() => Arrange();
        public override void SetLayoutVertical() => Arrange();

        public void Configure(GameMenuLayoutMode mode, Vector2 size, Vector2 gaps, RectOffset newPadding,
            GameMenuHorizontalAlignment horizontal, GameMenuVerticalAlignment vertical,
            GameMenuWrapColumnPolicy columnPolicy, int maxColumns)
        {
            layoutMode = mode;
            buttonSize = new Vector2(Mathf.Max(1f, size.x), Mathf.Max(1f, size.y));
            spacing = new Vector2(Mathf.Max(0f, gaps.x), Mathf.Max(0f, gaps.y));
            padding = new RectOffset(Mathf.Max(0, newPadding?.left ?? 0),
                Mathf.Max(0, newPadding?.right ?? 0), Mathf.Max(0, newPadding?.top ?? 0),
                Mathf.Max(0, newPadding?.bottom ?? 0));
            horizontalAlignment = horizontal;
            verticalAlignment = vertical;
            wrapColumnPolicy = columnPolicy;
            maximumColumns = Mathf.Max(1, maxColumns);
            SetDirty();
        }

        public void SetViewport(RectTransform value) { viewport = value; SetDirty(); }
        public void Refresh() => SetDirty();

        protected override void OnValidate()
        {
            base.OnValidate();
            buttonSize.x = Mathf.Max(1f, buttonSize.x);
            buttonSize.y = Mathf.Max(1f, buttonSize.y);
            spacing.x = Mathf.Max(0f, spacing.x);
            spacing.y = Mathf.Max(0f, spacing.y);
            maximumColumns = Mathf.Max(1, maximumColumns);
        }

        private void RefreshVisibleChildren()
        {
            visibleChildren.Clear();
            for (var i = 0; i < rectTransform.childCount; i++)
                if (rectTransform.GetChild(i) is RectTransform child && child.gameObject.activeSelf)
                    visibleChildren.Add(child);
        }

        private void Calculate()
        {
            var area = viewport != null ? viewport.rect.size : rectTransform.rect.size;
            current = GameMenuLayoutCalculator.Calculate(layoutMode, visibleChildren.Count,
                area.x, area.y, buttonSize, spacing, padding, wrapColumnPolicy, maximumColumns);
        }

        private void Arrange()
        {
            RefreshVisibleChildren();
            Calculate();
            var area = viewport != null ? viewport.rect.size : rectTransform.rect.size;
            var startY = GameMenuLayoutCalculator.StartY(area.y, current.RequiredHeight,
                padding, verticalAlignment);
            var columns = Mathf.Max(1, current.Columns);
            for (var i = 0; i < visibleChildren.Count; i++)
            {
                var row = layoutMode == GameMenuLayoutMode.HorizontalScroll ? 0 : i / columns;
                var column = layoutMode == GameMenuLayoutMode.HorizontalScroll ? i : i % columns;
                var rowCount = layoutMode == GameMenuLayoutMode.HorizontalScroll
                    ? visibleChildren.Count
                    : Mathf.Min(columns, visibleChildren.Count - row * columns);
                var rowWidth = rowCount * buttonSize.x + Mathf.Max(0, rowCount - 1) * spacing.x;
                var x = GameMenuLayoutCalculator.RowStartX(area.x, rowWidth, padding,
                    horizontalAlignment) + column * (buttonSize.x + spacing.x);
                var y = startY + row * (buttonSize.y + spacing.y);
                SetChildAlongAxis(visibleChildren[i], 0, x, buttonSize.x);
                SetChildAlongAxis(visibleChildren[i], 1, y, buttonSize.y);
            }
        }
    }
}
