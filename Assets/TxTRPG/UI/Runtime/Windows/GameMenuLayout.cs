using UnityEngine;

namespace TxTRPG.UI.Windows
{
    public enum GameMenuLayoutMode { HorizontalScroll, Wrap }
    public enum GameMenuHorizontalAlignment { Left, Center, Right }
    public enum GameMenuVerticalAlignment { Top, Center, Bottom }
    public enum GameMenuWrapColumnPolicy { AutoFit, MaximumColumns }
    public enum GameMenuScrollbarVisibility { Hidden, Auto, Always }
    public enum GameMenuScrollbarSpaceMode { Overlay, ReserveAlways, ReserveWhenVisible }

    public readonly struct GameMenuLayoutResult
    {
        public GameMenuLayoutResult(int columns, int rows, float requiredWidth, float requiredHeight,
            bool insufficientWidth, bool insufficientHeight)
        {
            Columns = columns;
            Rows = rows;
            RequiredWidth = requiredWidth;
            RequiredHeight = requiredHeight;
            InsufficientWidth = insufficientWidth;
            InsufficientHeight = insufficientHeight;
        }

        public int Columns { get; }
        public int Rows { get; }
        public float RequiredWidth { get; }
        public float RequiredHeight { get; }
        public bool InsufficientWidth { get; }
        public bool InsufficientHeight { get; }
        public bool HasInsufficientSpace => InsufficientWidth || InsufficientHeight;
    }

    public static class GameMenuLayoutCalculator
    {
        public static GameMenuLayoutResult Calculate(GameMenuLayoutMode mode, int visibleCount,
            float viewportWidth, float viewportHeight, Vector2 buttonSize, Vector2 spacing,
            RectOffset padding, GameMenuWrapColumnPolicy columnPolicy, int maximumColumns)
        {
            visibleCount = Mathf.Max(0, visibleCount);
            buttonSize.x = Mathf.Max(1f, buttonSize.x);
            buttonSize.y = Mathf.Max(1f, buttonSize.y);
            spacing.x = Mathf.Max(0f, spacing.x);
            spacing.y = Mathf.Max(0f, spacing.y);
            maximumColumns = Mathf.Max(1, maximumColumns);
            padding ??= new RectOffset();

            if (mode == GameMenuLayoutMode.HorizontalScroll)
            {
                var width = padding.horizontal + visibleCount * buttonSize.x
                    + Mathf.Max(0, visibleCount - 1) * spacing.x;
                var height = padding.vertical + (visibleCount > 0 ? buttonSize.y : 0f);
                return new GameMenuLayoutResult(visibleCount, visibleCount > 0 ? 1 : 0, width, height,
                    visibleCount > 0 && viewportWidth < buttonSize.x + padding.horizontal,
                    viewportHeight + 0.01f < height);
            }

            var availableWidth = Mathf.Max(0f, viewportWidth - padding.horizontal);
            var fitColumns = Mathf.FloorToInt((availableWidth + spacing.x) / (buttonSize.x + spacing.x));
            var columns = visibleCount == 0 ? 0 : Mathf.Max(1, fitColumns);
            if (columnPolicy == GameMenuWrapColumnPolicy.MaximumColumns)
                columns = Mathf.Min(columns, maximumColumns);
            columns = Mathf.Min(columns, Mathf.Max(1, visibleCount));
            var rows = columns == 0 ? 0 : Mathf.CeilToInt(visibleCount / (float)columns);
            var requiredWidth = padding.horizontal + (columns > 0
                ? columns * buttonSize.x + (columns - 1) * spacing.x : 0f);
            var requiredHeight = padding.vertical + (rows > 0
                ? rows * buttonSize.y + (rows - 1) * spacing.y : 0f);
            return new GameMenuLayoutResult(columns, rows, requiredWidth, requiredHeight,
                visibleCount > 0 && availableWidth < buttonSize.x,
                viewportHeight + 0.01f < requiredHeight);
        }

        public static float RowStartX(float viewportWidth, float rowWidth, RectOffset padding,
            GameMenuHorizontalAlignment alignment)
        {
            padding ??= new RectOffset();
            var free = Mathf.Max(0f, viewportWidth - padding.horizontal - rowWidth);
            var aligned = alignment == GameMenuHorizontalAlignment.Center ? free * 0.5f
                : alignment == GameMenuHorizontalAlignment.Right ? free : 0f;
            return padding.left + aligned;
        }

        public static float StartY(float viewportHeight, float contentHeight, RectOffset padding,
            GameMenuVerticalAlignment alignment)
        {
            padding ??= new RectOffset();
            var innerContent = Mathf.Max(0f, contentHeight - padding.vertical);
            var free = Mathf.Max(0f, viewportHeight - padding.vertical - innerContent);
            var aligned = alignment == GameMenuVerticalAlignment.Center ? free * 0.5f
                : alignment == GameMenuVerticalAlignment.Bottom ? free : 0f;
            return padding.top + aligned;
        }

        public static bool ShouldShowScrollbar(GameMenuScrollbarVisibility visibility, bool overflow)
            => visibility == GameMenuScrollbarVisibility.Always ||
               visibility == GameMenuScrollbarVisibility.Auto && overflow;

        public static bool ShouldReserveScrollbarSpace(GameMenuScrollbarSpaceMode mode, bool isVisible)
            => mode == GameMenuScrollbarSpaceMode.ReserveAlways ||
               mode == GameMenuScrollbarSpaceMode.ReserveWhenVisible && isVisible;
    }
}
