using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class FlexibleContentLayoutGroup : LayoutGroup
    {
        [SerializeField] private FlexibleLayoutAxis fixedAxis = FlexibleLayoutAxis.Horizontal;
        [SerializeField] private FlexibleLayoutAxisPolicy axisPolicy = FlexibleLayoutAxisPolicy.Fixed;
        [SerializeField, Min(1f)] private float breakpoint = 720f;
        [SerializeField, Min(0f)] private float spacing;
        [SerializeField] private FlexibleLayoutOverflow overflow = FlexibleLayoutOverflow.ShrinkBelowMinimum;
        [SerializeField] private bool includeInactiveChildren;

        private readonly List<FlexibleLayoutItem> items = new();

        public FlexibleLayoutAxis CurrentAxis => ResolveAxis(rectTransform.rect.width);
        public FlexibleLayoutAxis FixedAxis => fixedAxis;
        public FlexibleLayoutAxisPolicy AxisPolicy => axisPolicy;
        public float Breakpoint => breakpoint;
        public float Spacing => spacing;
        public FlexibleLayoutOverflow Overflow => overflow;
        public bool IncludeInactiveChildren => includeInactiveChildren;

        public void Configure(FlexibleLayoutAxis axis, FlexibleLayoutAxisPolicy policy, float widthBreakpoint,
            float childSpacing, RectOffset childPadding, FlexibleLayoutOverflow overflowPolicy,
            bool includeInactive, TextAnchor alignment)
        {
            var nextBreakpoint = Mathf.Max(1f, widthBreakpoint);
            var nextSpacing = Mathf.Max(0f, childSpacing);
            if (fixedAxis == axis &&
                axisPolicy == policy &&
                Mathf.Approximately(breakpoint, nextBreakpoint) &&
                Mathf.Approximately(spacing, nextSpacing) &&
                PaddingEquals(padding, childPadding) &&
                overflow == overflowPolicy &&
                includeInactiveChildren == includeInactive &&
                childAlignment == alignment)
            {
                return;
            }

            fixedAxis = axis;
            axisPolicy = policy;
            breakpoint = nextBreakpoint;
            spacing = nextSpacing;
            padding = CopyPadding(childPadding);
            overflow = overflowPolicy;
            includeInactiveChildren = includeInactive;
            childAlignment = alignment;
            SetDirty();
        }

        public FlexibleLayoutAxis ResolveAxis(float width)
        {
            return axisPolicy switch
            {
                FlexibleLayoutAxisPolicy.HorizontalWhenWide when width >= breakpoint => FlexibleLayoutAxis.Horizontal,
                FlexibleLayoutAxisPolicy.VerticalWhenNarrow when width < breakpoint => FlexibleLayoutAxis.Vertical,
                _ => fixedAxis
            };
        }

        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();
            CollectContentChildren();
            SetInputForAxis(0);
        }

        public override void CalculateLayoutInputVertical() => SetInputForAxis(1);
        public override void SetLayoutHorizontal() => ArrangeAxis(0);
        public override void SetLayoutVertical() => ArrangeAxis(1);

        public void Rebuild()
        {
            SetDirty();
            LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            SetDirty();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            breakpoint = Mathf.Max(1f, breakpoint);
            spacing = Mathf.Max(0f, spacing);
            SetDirty();
        }
#endif

        private void CollectContentChildren()
        {
            rectChildren.Clear();
            items.Clear();
            for (var i = 0; i < rectTransform.childCount; i++)
            {
                if (rectTransform.GetChild(i) is not RectTransform child ||
                    (!includeInactiveChildren && !child.gameObject.activeInHierarchy) || IsIgnored(child))
                    continue;
                rectChildren.Add(child);
                items.Add(child.GetComponent<FlexibleLayoutItem>());
            }
        }

        private void SetInputForAxis(int axis)
        {
            var mainAxis = CurrentAxis == FlexibleLayoutAxis.Horizontal ? 0 : 1;
            if (axis == mainAxis)
            {
                var inset = axis == 0 ? padding.horizontal : padding.vertical;
                var minimum = (float)inset + Mathf.Max(0, rectChildren.Count - 1) * spacing;
                var preferred = minimum;
                for (var i = 0; i < rectChildren.Count; i++)
                {
                    var request = GetRequest(i);
                    minimum += request.MinimumSize;
                    preferred += request.Mode == FlexibleLayoutSizeMode.Fixed
                        ? Mathf.Max(request.FixedSize, request.MinimumSize) : request.MinimumSize;
                }
                SetLayoutInputForAxis(minimum, preferred, -1f, axis);
                return;
            }

            var crossMinimum = 0f;
            var crossPreferred = 0f;
            for (var i = 0; i < rectChildren.Count; i++)
            {
                crossMinimum = Mathf.Max(crossMinimum, LayoutUtility.GetMinSize(rectChildren[i], axis));
                crossPreferred = Mathf.Max(crossPreferred, LayoutUtility.GetPreferredSize(rectChildren[i], axis));
            }
            var crossPadding = axis == 0 ? padding.horizontal : padding.vertical;
            SetLayoutInputForAxis(crossMinimum + crossPadding, crossPreferred + crossPadding, -1f, axis);
        }

        private void ArrangeAxis(int axis)
        {
            CollectContentChildren();
            var mainAxis = CurrentAxis == FlexibleLayoutAxis.Horizontal ? 0 : 1;
            if (axis != mainAxis)
            {
                var start = axis == 0 ? padding.left : padding.top;
                var size = rectTransform.rect.size[axis] - (axis == 0 ? padding.horizontal : padding.vertical);
                for (var i = 0; i < rectChildren.Count; i++)
                    SetChildAlongAxis(rectChildren[i], axis, start, Mathf.Max(0f, size));
                return;
            }

            var available = rectTransform.rect.size[axis] - (axis == 0 ? padding.horizontal : padding.vertical);
            var requests = new FlexibleLayoutSizeRequest[rectChildren.Count];
            for (var i = 0; i < requests.Length; i++) requests[i] = GetRequest(i);
            var sizes = FlexibleLayoutPanel.CalculateSizes(available, spacing, overflow, requests);
            var required = Mathf.Max(0, sizes.Length - 1) * spacing;
            for (var i = 0; i < sizes.Length; i++) required += sizes[i];
            var position = GetStartOffset(axis, required);
            for (var i = 0; i < rectChildren.Count; i++)
            {
                SetChildAlongAxis(rectChildren[i], axis, position, sizes[i]);
                position += sizes[i] + spacing;
            }
        }

        private FlexibleLayoutSizeRequest GetRequest(int index)
        {
            var item = index < items.Count ? items[index] : null;
            return item == null
                ? new FlexibleLayoutSizeRequest(FlexibleLayoutSizeMode.Weighted, 1f, 0f, 0f, 0f)
                : new FlexibleLayoutSizeRequest(item.SizeMode, item.Weight, item.FixedSize, item.MinimumSize, item.MaximumSize);
        }

        private static bool IsIgnored(RectTransform child)
        {
            foreach (var ignorer in child.GetComponents<ILayoutIgnorer>())
                if (ignorer.ignoreLayout) return true;
            return false;
        }

        private static RectOffset CopyPadding(RectOffset value) => value == null
            ? new RectOffset() : new RectOffset(value.left, value.right, value.top, value.bottom);

        private static bool PaddingEquals(RectOffset left, RectOffset right)
        {
            if (left == null || right == null) return left == null && right == null;
            return left.left == right.left && left.right == right.right &&
                   left.top == right.top && left.bottom == right.bottom;
        }
    }
}
