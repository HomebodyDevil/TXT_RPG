using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.Shapes
{
    // Shared procedural UI shapes. Add a new shape here (enum value + BuildBoundary case) and every
    // shape graphic and hit area can use it. ExplorationCardShape mirrors these values for card serialization.
    public enum UiShape
    {
        Rectangle,
        RoundedRectangle,
        Circle,
        Ellipse,
        Triangle
    }

    public enum UiShapeDrawMode
    {
        Fill,
        InnerBorder
    }

    public static class UiShapeGeometry
    {
        public const int MinimumCurveSegments = 1;
        public const int MaximumCurveSegments = 16;

        public static void BuildBoundary(Rect rect, UiShape shape, float cornerRadius, int curveSegments, List<Vector2> points)
        {
            points.Clear();
            var segments = Mathf.Clamp(curveSegments, MinimumCurveSegments, MaximumCurveSegments);
            switch (shape)
            {
                case UiShape.Circle:
                    var diameter = Mathf.Min(rect.width, rect.height);
                    AddEllipse(new Rect(rect.center - Vector2.one * diameter * .5f, Vector2.one * diameter), points, segments * 4);
                    break;
                case UiShape.Ellipse:
                    AddEllipse(rect, points, segments * 4);
                    break;
                case UiShape.Triangle:
                    AddRoundedPolygon(new Vector2(rect.center.x, rect.yMax), new Vector2(rect.xMax, rect.yMin), new Vector2(rect.xMin, rect.yMin),
                        EffectiveTriangleRadius(rect, cornerRadius), points, segments);
                    break;
                case UiShape.RoundedRectangle:
                    AddRoundedRectangle(rect, Mathf.Min(Sanitize(cornerRadius), Mathf.Min(rect.width, rect.height) * .5f), points, segments);
                    break;
                default:
                    AddRectangle(rect, points);
                    break;
            }
        }

        // Fills the mesh for one shape. outer/inner are caller-owned scratch lists so a rebuild does not allocate.
        public static void Populate(VertexHelper vh, Rect rect, UiShape shape, float cornerRadius, int curveSegments,
            UiShapeDrawMode mode, float borderThickness, Color32 tint, List<Vector2> outer, List<Vector2> inner)
        {
            vh.Clear();
            if (rect.width <= 0f || rect.height <= 0f) return;
            BuildBoundary(rect, shape, cornerRadius, curveSegments, outer);
            if (outer.Count < 3) return;
            if (mode == UiShapeDrawMode.Fill)
            {
                AddFan(vh, outer, tint);
                return;
            }

            var thickness = Mathf.Min(Sanitize(borderThickness), Mathf.Min(rect.width, rect.height) * .5f);
            if (thickness <= .001f) return;
            var center = rect.center;
            var scale = new Vector2(Mathf.Max(0f, (rect.width - thickness * 2f) / rect.width), Mathf.Max(0f, (rect.height - thickness * 2f) / rect.height));
            inner.Clear();
            for (var i = 0; i < outer.Count; i++) inner.Add(center + Vector2.Scale(outer[i] - center, scale));
            AddRing(vh, outer, inner, tint);
        }

        // Point-in-shape test in the same local space as rect. The boundary list is caller-owned scratch.
        public static bool Contains(Rect rect, UiShape shape, float cornerRadius, int curveSegments, Vector2 point, List<Vector2> boundary)
        {
            if (rect.width <= 0f || rect.height <= 0f || !rect.Contains(point)) return false;
            if (shape == UiShape.Rectangle) return true;
            BuildBoundary(rect, shape, cornerRadius, curveSegments, boundary);
            return ContainsPolygon(boundary, point);
        }

        public static bool ContainsPolygon(IReadOnlyList<Vector2> polygon, Vector2 point)
        {
            var inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                var a = polygon[i];
                var b = polygon[j];
                if (a.y > point.y != b.y > point.y && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside;
        }

        public static float Sanitize(float value) => float.IsNaN(value) || float.IsInfinity(value) || value < 0f ? 0f : value;

        private static float EffectiveTriangleRadius(Rect rect, float cornerRadius)
        {
            var side = Vector2.Distance(new Vector2(rect.center.x, rect.yMax), new Vector2(rect.xMax, rect.yMin));
            // A rounded polygon trims each adjacent edge. Keeping the trim below one quarter of
            // the shortest edge prevents arcs from crossing even at a very flat triangle angle.
            return Mathf.Min(Sanitize(cornerRadius), Mathf.Min(side, rect.width) * .25f);
        }

        private static void AddRectangle(Rect rect, List<Vector2> points)
        {
            points.Add(new Vector2(rect.xMin, rect.yMin)); points.Add(new Vector2(rect.xMin, rect.yMax));
            points.Add(new Vector2(rect.xMax, rect.yMax)); points.Add(new Vector2(rect.xMax, rect.yMin));
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
            if (radius <= .001f) { AddRectangle(rect, points); return; }
            AddArc(new Vector2(rect.xMin + radius, rect.yMin + radius), radius, 180f, 270f, points, segments);
            AddArc(new Vector2(rect.xMax - radius, rect.yMin + radius), radius, 270f, 360f, points, segments);
            AddArc(new Vector2(rect.xMax - radius, rect.yMax - radius), radius, 0f, 90f, points, segments);
            AddArc(new Vector2(rect.xMin + radius, rect.yMax - radius), radius, 90f, 180f, points, segments);
        }

        private static void AddRoundedPolygon(Vector2 first, Vector2 second, Vector2 third, float radius, List<Vector2> points, int segments)
        {
            AddRoundedCorner(third, first, second, radius, points, segments);
            AddRoundedCorner(first, second, third, radius, points, segments);
            AddRoundedCorner(second, third, first, radius, points, segments);
        }

        private static void AddRoundedCorner(Vector2 previous, Vector2 current, Vector2 next, float radius, List<Vector2> points, int segments)
        {
            if (radius <= .001f) { points.Add(current); return; }
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
    }
}
