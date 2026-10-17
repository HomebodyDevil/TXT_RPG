using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.Shapes
{
    // Texture-free procedural shape. Material is assignable like any Graphic, so shader effects attach here.
    [DisallowMultipleComponent, RequireComponent(typeof(CanvasRenderer))]
    public sealed class UiShapeGraphic : MaskableGraphic
    {
        [SerializeField] private UiShape shape = UiShape.Circle;
        [SerializeField, Min(0f)] private float cornerRadius = 12f;
        [SerializeField, Range(UiShapeGeometry.MinimumCurveSegments, UiShapeGeometry.MaximumCurveSegments)] private int curveSegments = 8;
        [SerializeField] private UiShapeDrawMode drawMode;
        [SerializeField, Min(0f)] private float borderThickness = 2f;

        private readonly List<Vector2> outer = new(64);
        private readonly List<Vector2> inner = new(64);

        public UiShape Shape => shape;
        public float CornerRadius => cornerRadius;
        public int CurveSegments => curveSegments;
        public UiShapeDrawMode DrawMode => drawMode;
        public float BorderThickness => borderThickness;

        public void Configure(UiShape value, float radius, UiShapeDrawMode mode, float thickness)
        {
            if (shape == value && Mathf.Approximately(cornerRadius, UiShapeGeometry.Sanitize(radius)) && drawMode == mode &&
                Mathf.Approximately(borderThickness, UiShapeGeometry.Sanitize(thickness))) return;
            shape = value;
            cornerRadius = UiShapeGeometry.Sanitize(radius);
            drawMode = mode;
            borderThickness = UiShapeGeometry.Sanitize(thickness);
            SetVerticesDirty();
        }

        // Clamped to the UiShapeGeometry range; the same value does nothing.
        public void SetCurveSegments(int value)
        {
            value = Mathf.Clamp(value, UiShapeGeometry.MinimumCurveSegments, UiShapeGeometry.MaximumCurveSegments);
            if (curveSegments == value) return;
            curveSegments = value;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh) =>
            UiShapeGeometry.Populate(vh, GetPixelAdjustedRect(), shape, cornerRadius, curveSegments, drawMode, borderThickness, color, outer, inner);

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            cornerRadius = UiShapeGeometry.Sanitize(cornerRadius);
            borderThickness = UiShapeGeometry.Sanitize(borderThickness);
            curveSegments = Mathf.Clamp(curveSegments, UiShapeGeometry.MinimumCurveSegments, UiShapeGeometry.MaximumCurveSegments);
            SetVerticesDirty();
        }
#endif
    }
}
