using System;
using UnityEngine;

namespace TxTRPG.UI
{
    public enum PanelBoundaryOrientation { None, Horizontal, Vertical }

    public static class PanelBoundaryCalculator
    {
        // Rects are in one shared local space. A horizontal boundary separates panels stacked vertically; its center is
        // the midline of the gap (or the shared edge when Spacing is 0) at the middle of the panels' common width.
        public static bool TryCalculate(Rect first, Rect second, out Vector2 center, out PanelBoundaryOrientation orientation)
        {
            center = default;
            orientation = PanelBoundaryOrientation.None;
            if (!IsUsable(first) || !IsUsable(second)) return false;
            var verticalGap = Mathf.Max(first.yMin - second.yMax, second.yMin - first.yMax);
            var horizontalGap = Mathf.Max(first.xMin - second.xMax, second.xMin - first.xMax);
            const float tolerance = .01f;
            if (verticalGap < -tolerance && horizontalGap < -tolerance) return false; // Overlapping panels have no boundary.
            if (verticalGap >= horizontalGap)
            {
                var overlapMin = Mathf.Max(first.xMin, second.xMin);
                var overlapMax = Mathf.Min(first.xMax, second.xMax);
                if (overlapMax < overlapMin) return false;
                var lineY = first.center.y >= second.center.y ? (first.yMin + second.yMax) * .5f : (second.yMin + first.yMax) * .5f;
                center = new Vector2((overlapMin + overlapMax) * .5f, lineY);
                orientation = PanelBoundaryOrientation.Horizontal;
                return true;
            }
            var overlapBottom = Mathf.Max(first.yMin, second.yMin);
            var overlapTop = Mathf.Min(first.yMax, second.yMax);
            if (overlapTop < overlapBottom) return false;
            var lineX = first.center.x <= second.center.x ? (first.xMax + second.xMin) * .5f : (second.xMax + first.xMin) * .5f;
            center = new Vector2(lineX, (overlapBottom + overlapTop) * .5f);
            orientation = PanelBoundaryOrientation.Vertical;
            return true;
        }

        // Keeps a target of the given size fully inside bounds. A target larger than bounds is centered on that axis.
        public static Vector2 ClampInside(Vector2 center, Vector2 size, Rect bounds)
        {
            var half = size * .5f;
            return new Vector2(ClampAxis(center.x, half.x, bounds.xMin, bounds.xMax), ClampAxis(center.y, half.y, bounds.yMin, bounds.yMax));
        }

        private static float ClampAxis(float value, float half, float min, float max) =>
            max - min < half * 2f ? (min + max) * .5f : Mathf.Clamp(value, min + half, max - half);

        private static bool IsUsable(Rect rect) =>
            rect.width > 0f && rect.height > 0f && float.IsFinite(rect.x) && float.IsFinite(rect.y) && float.IsFinite(rect.width) && float.IsFinite(rect.height);
    }

    // Places a target over the boundary between two panels without joining any layout. Put it on a stretched,
    // ignoreLayout container; the container's rect is the clamp area. The target is centered on its pivot.
    // It runs after the Canvas layout pass (Canvas.willRenderCanvases), so the target follows the same frame.
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public sealed class PanelBoundaryPlacement : MonoBehaviour
    {
        [SerializeField, Tooltip("Upper (or left) panel.")] private RectTransform firstPanel;
        [SerializeField, Tooltip("Lower (or right) panel.")] private RectTransform secondPanel;
        [SerializeField, Tooltip("Placed object; its anchors are set to this container's center.")] private RectTransform target;

        private readonly Vector3[] corners = new Vector3[4];
        private DrivenRectTransformTracker tracker;
        private RectTransform container;
        private Rect lastFirst, lastSecond, lastBounds;
        private Vector2 lastTargetSize;
        private bool lastFirstActive, lastSecondActive, hasCache;

        public event Action BoundaryChanged;
        public bool HasBoundary { get; private set; }
        public PanelBoundaryOrientation Orientation { get; private set; }
        public Vector2 BoundaryCenter { get; private set; }
        public RectTransform FirstPanel => firstPanel;
        public RectTransform SecondPanel => secondPanel;
        public RectTransform Target => target;

        private void OnEnable()
        {
            container = (RectTransform)transform;
            hasCache = false;
            Canvas.willRenderCanvases += UpdatePlacement;
            UpdatePlacement();
        }

        private void OnDisable()
        {
            Canvas.willRenderCanvases -= UpdatePlacement;
            tracker.Clear();
        }

        public void UpdatePlacement() => Evaluate(false);
        public void ForceUpdate() => Evaluate(true);

        private void Evaluate(bool force)
        {
            if (this == null || container == null) return;
            var firstActive = firstPanel != null && firstPanel.gameObject.activeInHierarchy;
            var secondActive = secondPanel != null && secondPanel.gameObject.activeInHierarchy;
            var first = firstActive ? LocalRect(firstPanel) : default;
            var second = secondActive ? LocalRect(secondPanel) : default;
            var bounds = container.rect;
            var targetSize = target != null ? target.rect.size : Vector2.zero;
            if (!force && hasCache && firstActive == lastFirstActive && secondActive == lastSecondActive &&
                first == lastFirst && second == lastSecond && bounds == lastBounds && targetSize == lastTargetSize) return;
            hasCache = true;
            lastFirstActive = firstActive; lastSecondActive = secondActive;
            lastFirst = first; lastSecond = second; lastBounds = bounds; lastTargetSize = targetSize;

            var had = HasBoundary;
            var previousCenter = BoundaryCenter;
            var center = Vector2.zero;
            var orientation = PanelBoundaryOrientation.None;
            HasBoundary = firstActive && secondActive && PanelBoundaryCalculator.TryCalculate(first, second, out center, out orientation);
            if (HasBoundary)
            {
                BoundaryCenter = PanelBoundaryCalculator.ClampInside(center, targetSize, bounds);
                Orientation = orientation;
                if (target != null)
                {
                    // Driven values are owned by this component: they are not saved as Scene/Prefab changes.
                    tracker.Clear();
                    tracker.Add(this, target, DrivenTransformProperties.AnchoredPosition | DrivenTransformProperties.Anchors);
                    target.anchorMin = target.anchorMax = new Vector2(.5f, .5f);
                    var position = BoundaryCenter - bounds.center + Vector2.Scale(target.pivot - new Vector2(.5f, .5f), targetSize);
                    if (target.anchoredPosition != position) target.anchoredPosition = position;
                }
            }
            else Orientation = PanelBoundaryOrientation.None;
            if (had != HasBoundary || previousCenter != BoundaryCenter) BoundaryChanged?.Invoke();
        }

        private Rect LocalRect(RectTransform panel)
        {
            panel.GetWorldCorners(corners);
            var min = (Vector2)container.InverseTransformPoint(corners[0]);
            var max = min;
            for (var i = 1; i < 4; i++)
            {
                var point = (Vector2)container.InverseTransformPoint(corners[i]);
                min = Vector2.Min(min, point); max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

#if UNITY_EDITOR
        public void ConfigureForEditor(RectTransform first, RectTransform second, RectTransform placedTarget)
        { firstPanel = first; secondPanel = second; target = placedTarget; container = (RectTransform)transform; ForceUpdate(); }
#endif
    }
}
