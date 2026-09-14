using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.Exploration
{
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
            vh.Clear();
            var rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f) return;

            BuildBoundary(rect, outer);
            if (outer.Count < 3) return;
            if (drawMode == ExplorationCardShapeDrawMode.Fill)
            {
                AddFan(vh, outer, color);
                return;
            }

            var thickness = Mathf.Min(Sanitize(borderThickness, 0f), Mathf.Min(rect.width, rect.height) * .5f);
            if (thickness <= .001f) return;
            var center = rect.center;
            var sx = Mathf.Max(0f, (rect.width - thickness * 2f) / rect.width);
            var sy = Mathf.Max(0f, (rect.height - thickness * 2f) / rect.height);
            inner.Clear();
            for (var i = 0; i < outer.Count; i++) inner.Add(center + Vector2.Scale(outer[i] - center, new Vector2(sx, sy)));
            AddRing(vh, outer, inner, color);
        }

        private void BuildBoundary(Rect rect, List<Vector2> points)
        {
            points.Clear();
            var segments = Mathf.Clamp(curveSegments, 1, 16);
            switch (shape)
            {
                case ExplorationCardShape.Circle:
                    var diameter = Mathf.Min(rect.width, rect.height);
                    AddEllipse(new Rect(rect.center - Vector2.one * diameter * .5f, Vector2.one * diameter), points, segments * 4);
                    break;
                case ExplorationCardShape.Ellipse:
                    AddEllipse(rect, points, segments * 4);
                    break;
                case ExplorationCardShape.Triangle:
                    AddRoundedPolygon(new[] { new Vector2(rect.center.x, rect.yMax), new Vector2(rect.xMax, rect.yMin), new Vector2(rect.xMin, rect.yMin) }, EffectiveTriangleRadius(rect), points, segments);
                    break;
                case ExplorationCardShape.RoundedRectangle:
                    AddRoundedRectangle(rect, Mathf.Min(Sanitize(cornerRadius, 0f), Mathf.Min(rect.width, rect.height) * .5f), points, segments);
                    break;
                default:
                    points.Add(new Vector2(rect.xMin, rect.yMin)); points.Add(new Vector2(rect.xMin, rect.yMax));
                    points.Add(new Vector2(rect.xMax, rect.yMax)); points.Add(new Vector2(rect.xMax, rect.yMin));
                    break;
            }
        }

        private float EffectiveTriangleRadius(Rect rect)
        {
            var requested = Sanitize(cornerRadius, 0f);
            var side = Vector2.Distance(new Vector2(rect.center.x, rect.yMax), new Vector2(rect.xMax, rect.yMin));
            var baseLength = rect.width;
            // A rounded polygon trims each adjacent edge. Keeping the trim below one quarter of
            // the shortest edge prevents arcs from crossing even at a very flat triangle angle.
            return Mathf.Min(requested, Mathf.Min(side, baseLength) * .25f);
        }

        private static void AddEllipse(Rect rect, List<Vector2> points, int segments)
        {
            segments = Mathf.Clamp(segments, 12, 64);
            for (var i = 0; i < segments; i++)
            {
                var angle = Mathf.PI * 2f * i / segments;
                points.Add(rect.center + new Vector2(Mathf.Cos(angle) * rect.width * .5f, Mathf.Sin(angle) * rect.height * .5f));
            }
        }

        private static void AddRoundedRectangle(Rect rect, float radius, List<Vector2> points, int segments)
        {
            if (radius <= .001f)
            {
                points.Add(new Vector2(rect.xMin, rect.yMin)); points.Add(new Vector2(rect.xMin, rect.yMax));
                points.Add(new Vector2(rect.xMax, rect.yMax)); points.Add(new Vector2(rect.xMax, rect.yMin)); return;
            }
            AddArc(new Vector2(rect.xMin + radius, rect.yMin + radius), radius, 180f, 270f, points, segments);
            AddArc(new Vector2(rect.xMax - radius, rect.yMin + radius), radius, 270f, 360f, points, segments);
            AddArc(new Vector2(rect.xMax - radius, rect.yMax - radius), radius, 0f, 90f, points, segments);
            AddArc(new Vector2(rect.xMin + radius, rect.yMax - radius), radius, 90f, 180f, points, segments);
        }

        private static void AddRoundedPolygon(IReadOnlyList<Vector2> vertices, float radius, List<Vector2> points, int segments)
        {
            if (radius <= .001f) { for (var i = 0; i < vertices.Count; i++) points.Add(vertices[i]); return; }
            for (var i = 0; i < vertices.Count; i++)
            {
                var current = vertices[i];
                var previous = vertices[(i - 1 + vertices.Count) % vertices.Count];
                var next = vertices[(i + 1) % vertices.Count];
                var incoming = (previous - current).normalized;
                var outgoing = (next - current).normalized;
                var halfAngle = Mathf.Acos(Mathf.Clamp(Vector2.Dot(incoming, outgoing), -1f, 1f)) * .5f;
                var trim = Mathf.Min(radius / Mathf.Max(.001f, Mathf.Tan(halfAngle)), Mathf.Min(Vector2.Distance(current, previous), Vector2.Distance(current, next)) * .25f);
                var start = current + incoming * trim;
                var end = current + outgoing * trim;
                for (var step = 0; step <= segments; step++)
                {
                    var t = step / (float)segments;
                    var oneMinusT = 1f - t;
                    points.Add(oneMinusT * oneMinusT * start + 2f * oneMinusT * t * current + t * t * end);
                }
            }
        }

        private static void AddArc(Vector2 center, float radius, float fromDegrees, float toDegrees, List<Vector2> points, int segments)
        {
            for (var i = 0; i <= segments; i++)
            {
                var radians = Mathf.Lerp(fromDegrees, toDegrees, i / (float)segments) * Mathf.Deg2Rad;
                points.Add(center + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius);
            }
        }

        private static void AddFan(VertexHelper vh, IReadOnlyList<Vector2> points, Color32 tint)
        {
            var center = Vector2.zero; for (var i = 0; i < points.Count; i++) center += points[i]; center /= points.Count;
            vh.AddVert(center, tint, new Vector2(.5f, .5f));
            for (var i = 0; i < points.Count; i++) vh.AddVert(points[i], tint, Vector2.zero);
            for (var i = 0; i < points.Count; i++) vh.AddTriangle(0, i + 1, (i + 1) % points.Count + 1);
        }

        private static void AddRing(VertexHelper vh, IReadOnlyList<Vector2> outside, IReadOnlyList<Vector2> inside, Color32 tint)
        {
            for (var i = 0; i < outside.Count; i++) { vh.AddVert(outside[i], tint, Vector2.zero); vh.AddVert(inside[i], tint, Vector2.zero); }
            for (var i = 0; i < outside.Count; i++) { var next = (i + 1) % outside.Count; vh.AddTriangle(i * 2, next * 2, i * 2 + 1); vh.AddTriangle(next * 2, next * 2 + 1, i * 2 + 1); }
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
