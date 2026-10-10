using System.Collections.Generic;
using TxTRPG.UI.Shapes;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.Exploration
{
    // Card-serialized mirror of UiShape: values must stay identical (checked by ExplorationNodeChoiceCardTests).
    public enum ExplorationCardShape
    {
        Rectangle,
        RoundedRectangle,
        Circle,
        Ellipse,
        Triangle
    }

    public enum ExplorationCardShapeDrawMode
    {
        Fill,
        InnerBorder
    }

    [DisallowMultipleComponent, RequireComponent(typeof(CanvasRenderer))]
    public sealed class ExplorationCardShapeGraphic : MaskableGraphic
    {
        [SerializeField, HideInInspector] private ExplorationCardShape shape = ExplorationCardShape.RoundedRectangle;
        [SerializeField, HideInInspector, Min(0f)] private float cornerRadius = 14f;
        [SerializeField, HideInInspector, Min(1)] private int curveSegments = 6;
        [SerializeField, HideInInspector] private ExplorationCardShapeDrawMode drawMode;
        [SerializeField, HideInInspector, Min(0f)] private float borderThickness = 2f;

        private readonly List<Vector2> outer = new(64);
        private readonly List<Vector2> inner = new(64);

        public ExplorationCardShape Shape => shape;
        public float CornerRadius => cornerRadius;
        public float BorderThickness => borderThickness;

        public void Configure(ExplorationCardShape value, float radius, ExplorationCardShapeDrawMode mode, float thickness)
        {
            shape = value;
            cornerRadius = Sanitize(radius, 0f);
            drawMode = mode;
            borderThickness = Sanitize(thickness, 0f);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            UiShapeGeometry.Populate(vh, GetPixelAdjustedRect(), (UiShape)shape, cornerRadius, curveSegments,
                (UiShapeDrawMode)drawMode, borderThickness, color, outer, inner);
        }

        private static float Sanitize(float value, float fallback) => float.IsNaN(value) || float.IsInfinity(value) || value < 0f ? fallback : value;

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate(); cornerRadius = Sanitize(cornerRadius, 0f); borderThickness = Sanitize(borderThickness, 0f); curveSegments = Mathf.Clamp(curveSegments, 1, 16); SetVerticesDirty();
        }
#endif
    }
}
