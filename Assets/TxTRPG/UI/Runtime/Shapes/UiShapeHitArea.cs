using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.Shapes
{
    // Invisible raycast target. It lives outside the effect VisualRoot so shakes, scaling, or mask shaders
    // never move or reshape the input area. Rectangle accepts the whole rect.
    [DisallowMultipleComponent, RequireComponent(typeof(CanvasRenderer))]
    public sealed class UiShapeHitArea : Graphic, ICanvasRaycastFilter
    {
        [SerializeField] private UiShape shape = UiShape.Circle;
        [SerializeField, Min(0f)] private float cornerRadius = 12f;
        [SerializeField, Tooltip("Optional alpha test against an Image (requires a Read/Write enabled texture). Zero uses the shape.")]
        [Range(0f, 1f)] private float alphaThreshold;
        [SerializeField] private Image alphaSource;

        private readonly List<Vector2> boundary = new(64);

        public UiShape Shape => shape;
        public float CornerRadius => cornerRadius;
        public float AlphaThreshold => alphaThreshold;

        public void Configure(UiShape value, float radius, Image alphaImage = null, float threshold = 0f)
        {
            shape = value;
            cornerRadius = UiShapeGeometry.Sanitize(radius);
            alphaSource = alphaImage;
            alphaThreshold = Mathf.Clamp01(threshold);
        }

        protected override void OnPopulateMesh(VertexHelper vh) => vh.Clear();

        public bool ContainsLocalPoint(Vector2 localPoint)
        {
            var rect = rectTransform.rect;
            if (alphaThreshold > 0f && TryAlphaTest(rect, localPoint, out var opaque)) return opaque;
            return UiShapeGeometry.Contains(rect, shape, cornerRadius, UiShapeGeometry.MaximumCurveSegments, localPoint, boundary);
        }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera) =>
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out var local) && ContainsLocalPoint(local);

        private bool TryAlphaTest(Rect rect, Vector2 localPoint, out bool opaque)
        {
            opaque = false;
            var sprite = alphaSource != null ? alphaSource.sprite : null;
            if (sprite == null || sprite.texture == null || !sprite.texture.isReadable || !rect.Contains(localPoint)) return false;
            // Sample the sprite rectangle proportionally; sliced borders are treated as stretched for hit testing.
            var normalized = Rect.PointToNormalized(rect, localPoint);
            var area = sprite.textureRect;
            var pixel = sprite.texture.GetPixelBilinear((area.x + area.width * normalized.x) / sprite.texture.width,
                (area.y + area.height * normalized.y) / sprite.texture.height);
            opaque = pixel.a >= alphaThreshold;
            return true;
        }
    }
}
