using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.Runes
{
    // Places the active card cells of Content row by row from RuneGridLayoutMath and supplies Content's preferred height.
    // Width comes from the parent (Content stretches to the Viewport); a ContentSizeFitter applies the height. Runs only in
    // Unity's layout rebuild, never per frame. The LayoutGroup's own Padding and Child Alignment are not used; padding
    // lives in Settings.
    [AddComponentMenu("Layout/TxT RPG/Rune Grid Layout Group")]
    public sealed class RuneGridLayoutGroup : LayoutGroup
    {
        [SerializeField] private RuneGridLayoutSettings settings = new();

        public RuneGridLayoutSettings Settings => settings;
        public RuneGridLayoutResult CurrentLayout { get; private set; }

        public void SetSettings(RuneGridLayoutSettings value)
        {
            settings = value ?? new RuneGridLayoutSettings();
            SetDirty();
        }

        public float AvailableWidth => rectTransform.rect.width - settings.PaddingLeft - settings.PaddingRight;

        private RuneGridLayoutResult Calculate() =>
            CurrentLayout = RuneGridLayoutMath.Calculate(AvailableWidth, rectChildren.Count, settings);

        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();
            var horizontal = settings.PaddingLeft + settings.PaddingRight;
            // Minimum and preferred width are only the padding: the grid adapts to any width instead of asking for more.
            SetLayoutInputForAxis(horizontal, horizontal, -1f, 0);
        }

        public override void CalculateLayoutInputVertical()
        {
            var height = Calculate().ContentHeight;
            SetLayoutInputForAxis(height, height, -1f, 1);
        }

        public override void SetLayoutHorizontal() => Place(0);
        public override void SetLayoutVertical() => Place(1);

        private void Place(int axis)
        {
            var layout = Calculate();
            var step = layout.CellSize + layout.Spacing;
            for (var i = 0; i < rectChildren.Count; i++)
            {
                var column = i % layout.Columns;
                var row = i / layout.Columns;
                var position = axis == 0 ? settings.PaddingLeft + column * step : settings.PaddingTop + row * step;
                SetChildAlongAxis(rectChildren[i], axis, position, layout.CellSize);
            }
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            settings ??= new RuneGridLayoutSettings();
        }
#endif
    }
}
