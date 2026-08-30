using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI
{
    public enum FlexibleLayoutAxis
    {
        Horizontal,
        Vertical
    }

    public enum FlexibleLayoutAxisPolicy
    {
        Fixed,
        HorizontalWhenWide,
        VerticalWhenNarrow
    }

    public enum FlexibleLayoutOverflow
    {
        ShrinkBelowMinimum,
        Clip
    }

    public readonly struct FlexibleLayoutSizeRequest
    {
        public FlexibleLayoutSizeRequest(
            FlexibleLayoutSizeMode mode,
            float weight,
            float fixedSize,
            float minimumSize,
            float maximumSize)
        {
            Mode = mode;
            Weight = Mathf.Max(0f, weight);
            FixedSize = Mathf.Max(0f, fixedSize);
            MinimumSize = Mathf.Max(0f, minimumSize);
            MaximumSize = Mathf.Max(0f, maximumSize);
        }

        public FlexibleLayoutSizeMode Mode { get; }
        public float Weight { get; }
        public float FixedSize { get; }
        public float MinimumSize { get; }
        public float MaximumSize { get; }
    }

    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class FlexibleLayoutPanel : LayoutGroup
    {
        [Header("Identity")]
        [SerializeField] private string nodeId = string.Empty;

        [Header("Direction")]
        [SerializeField] private FlexibleLayoutAxis fixedAxis = FlexibleLayoutAxis.Horizontal;
        [SerializeField] private FlexibleLayoutAxisPolicy axisPolicy = FlexibleLayoutAxisPolicy.Fixed;
        [SerializeField, Min(1f)] private float breakpoint = 720f;

        [Header("Distribution")]
        [SerializeField, Min(0f)] private float spacing;
        [SerializeField] private FlexibleLayoutOverflow overflow = FlexibleLayoutOverflow.ShrinkBelowMinimum;
        [SerializeField] private bool includeInactiveChildren;
        [SerializeField] private RectTransform contentRoot;

        private readonly List<FlexibleLayoutItem> items = new();
        private float[] calculatedSizes = Array.Empty<float>();

        public string NodeId => nodeId;
        public FlexibleLayoutAxis CurrentAxis => ResolveAxis(rectTransform.rect.width);
        public RectTransform ContentRoot => contentRoot != null ? contentRoot : rectTransform;

        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();
            CollectChildren();
            SetInputForAxis(0);
        }

        public override void CalculateLayoutInputVertical()
        {
            SetInputForAxis(1);
        }

        public override void SetLayoutHorizontal()
        {
            ArrangeAxis(0);
        }

        public override void SetLayoutVertical()
        {
            ArrangeAxis(1);
        }

        public void Add(Transform child, float weight = 1f)
        {
            if (child == null)
            {
                return;
            }

            child.SetParent(ContentRoot, false);
            var item = child.GetComponent<FlexibleLayoutItem>() ?? child.gameObject.AddComponent<FlexibleLayoutItem>();
            item.Configure(FlexibleLayoutSizeMode.Weighted, weight);
            Rebuild();
        }

        public void Remove(Transform child)
        {
            if (child != null && child.parent == ContentRoot)
            {
                child.SetParent(null, false);
                Rebuild();
            }
        }

        public void SetWeight(Transform child, float weight)
        {
            if (child == null || child.parent != ContentRoot)
            {
                return;
            }

            var item = child.GetComponent<FlexibleLayoutItem>() ?? child.gameObject.AddComponent<FlexibleLayoutItem>();
            item.Configure(FlexibleLayoutSizeMode.Weighted, weight);
            Rebuild();
        }

        public void SetAxis(FlexibleLayoutAxis axis)
        {
            fixedAxis = axis;
            axisPolicy = FlexibleLayoutAxisPolicy.Fixed;
            Rebuild();
        }

        public void SetSpacing(float value)
        {
            spacing = Mathf.Max(0f, value);
            Rebuild();
        }

        public void Rebuild()
        {
            SetDirty();
            LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
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

        public static float[] CalculateSizes(
            float availableSize,
            float spacing,
            FlexibleLayoutOverflow overflow,
            IReadOnlyList<FlexibleLayoutSizeRequest> requests)
        {
            var count = requests?.Count ?? 0;
            var result = new float[count];
            if (count == 0)
            {
                return result;
            }

            var distributable = Mathf.Max(0f, availableSize - Mathf.Max(0f, spacing) * (count - 1));
            var weighted = new bool[count];
            var baseTotal = 0f;
            for (var i = 0; i < count; i++)
            {
                var request = requests[i];
                weighted[i] = request.Mode == FlexibleLayoutSizeMode.Weighted;
                if (weighted[i])
                {
                    result[i] = request.MinimumSize;
                }
                else
                {
                    var fixedTarget = Mathf.Max(request.FixedSize, request.MinimumSize);
                    result[i] = request.MaximumSize > 0f
                        ? Mathf.Max(request.MinimumSize, Mathf.Min(fixedTarget, request.MaximumSize))
                        : fixedTarget;
                }
                baseTotal += result[i];
            }

            if (baseTotal > distributable && baseTotal > 0f)
            {
                if (overflow == FlexibleLayoutOverflow.ShrinkBelowMinimum)
                {
                    var scale = distributable / baseTotal;
                    for (var i = 0; i < count; i++)
                    {
                        result[i] *= scale;
                    }
                }

                return result;
            }

            var remaining = distributable - baseTotal;
            var eligible = new bool[count];
            for (var i = 0; i < count; i++)
            {
                eligible[i] = weighted[i] &&
                              (requests[i].MaximumSize <= 0f || result[i] < requests[i].MaximumSize);
            }

            for (var pass = 0; pass < count && remaining > 0.001f; pass++)
            {
                var totalWeight = 0f;
                var eligibleCount = 0;
                for (var i = 0; i < count; i++)
                {
                    if (!eligible[i])
                    {
                        continue;
                    }

                    totalWeight += requests[i].Weight;
                    eligibleCount++;
                }

                if (eligibleCount == 0)
                {
                    break;
                }

                var distributed = 0f;
                var passRemaining = remaining;
                for (var i = 0; i < count; i++)
                {
                    if (!eligible[i])
                    {
                        continue;
                    }

                    var normalizedWeight = totalWeight > 0f
                        ? requests[i].Weight / totalWeight
                        : 1f / eligibleCount;
                    var addition = passRemaining * normalizedWeight;
                    var maximum = requests[i].MaximumSize;
                    if (maximum > 0f && result[i] + addition >= maximum)
                    {
                        addition = Mathf.Max(0f, maximum - result[i]);
                        eligible[i] = false;
                    }

                    result[i] += addition;
                    distributed += addition;
                }

                if (distributed <= 0.001f)
                {
                    break;
                }

                remaining -= distributed;
            }

            return result;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            EnsureNodeId();
            SetDirty();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            breakpoint = Mathf.Max(1f, breakpoint);
            spacing = Mathf.Max(0f, spacing);
            EnsureNodeId();
            SetDirty();
        }
#endif

        private void CollectChildren()
        {
            rectChildren.Clear();
            items.Clear();
            var container = ContentRoot;
            for (var i = 0; i < container.childCount; i++)
            {
                if (container.GetChild(i) is not RectTransform child ||
                    (!includeInactiveChildren && !child.gameObject.activeInHierarchy) ||
                    IsIgnored(child))
                {
                    continue;
                }

                rectChildren.Add(child);
                items.Add(child.GetComponent<FlexibleLayoutItem>());
            }
        }

        private void SetInputForAxis(int axis)
        {
            var mainAxis = CurrentAxis == FlexibleLayoutAxis.Horizontal ? 0 : 1;
            if (axis == mainAxis)
            {
                float minimum = padding.horizontal;
                float preferred = padding.horizontal;
                if (axis == 1)
                {
                    minimum = padding.vertical;
                    preferred = padding.vertical;
                }

                minimum += Mathf.Max(0, rectChildren.Count - 1) * spacing;
                preferred += Mathf.Max(0, rectChildren.Count - 1) * spacing;
                for (var i = 0; i < rectChildren.Count; i++)
                {
                    var request = GetRequest(i);
                    minimum += request.MinimumSize;
                    preferred += request.Mode == FlexibleLayoutSizeMode.Fixed
                        ? Mathf.Max(request.FixedSize, request.MinimumSize)
                        : request.MinimumSize;
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
            CollectChildren();
            var mainAxis = CurrentAxis == FlexibleLayoutAxis.Horizontal ? 0 : 1;
            if (axis != mainAxis)
            {
                var start = axis == 0 ? padding.left : padding.top;
                var size = rectTransform.rect.size[axis] - (axis == 0 ? padding.horizontal : padding.vertical);
                for (var i = 0; i < rectChildren.Count; i++)
                {
                    SetChildAlongAxis(rectChildren[i], axis, start, Mathf.Max(0f, size));
                }

                return;
            }

            var available = rectTransform.rect.size[axis] - (axis == 0 ? padding.horizontal : padding.vertical);
            var requests = new FlexibleLayoutSizeRequest[rectChildren.Count];
            for (var i = 0; i < requests.Length; i++)
            {
                requests[i] = GetRequest(i);
            }

            calculatedSizes = CalculateSizes(available, spacing, overflow, requests);
            var required = Mathf.Max(0, calculatedSizes.Length - 1) * spacing;
            for (var i = 0; i < calculatedSizes.Length; i++)
            {
                required += calculatedSizes[i];
            }

            var position = GetStartOffset(axis, required);
            for (var i = 0; i < rectChildren.Count; i++)
            {
                SetChildAlongAxis(rectChildren[i], axis, position, calculatedSizes[i]);
                position += calculatedSizes[i] + spacing;
            }
        }

        private FlexibleLayoutSizeRequest GetRequest(int index)
        {
            var item = index >= 0 && index < items.Count ? items[index] : null;
            return item == null
                ? new FlexibleLayoutSizeRequest(FlexibleLayoutSizeMode.Weighted, 1f, 0f, 0f, 0f)
                : new FlexibleLayoutSizeRequest(
                    item.SizeMode,
                    item.Weight,
                    item.FixedSize,
                    item.MinimumSize,
                    item.MaximumSize);
        }

        private static bool IsIgnored(RectTransform child)
        {
            var ignorers = child.GetComponents<ILayoutIgnorer>();
            for (var i = 0; i < ignorers.Length; i++)
            {
                if (ignorers[i].ignoreLayout)
                {
                    return true;
                }
            }

            return false;
        }

        private void EnsureNodeId()
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                nodeId = Guid.NewGuid().ToString("N");
            }
        }
    }
}
