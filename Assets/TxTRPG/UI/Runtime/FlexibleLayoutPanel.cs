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

    public enum FlexibleLayoutContentLayer
    {
        Content,
        BackgroundContent
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

        [Header("Content Insets")]
        [Tooltip("Applies Left, Right, Top, and Bottom margins to the stretched ContentLayer. Disabled preserves manually authored RectTransform offsets.")]
        [SerializeField] private bool overrideContentMargins;
        [SerializeField] private RectOffset contentMargins = new();

        [Header("Content Display")]
        [SerializeField] private FlexibleContentLayoutGroup contentLayout;
        [SerializeField] private RectMask2D contentMask;
        [SerializeField] private bool clipContent;

        [Header("Background Content")]
        [SerializeField] private RectTransform backgroundContentRoot;
        [SerializeField] private FlexibleContentLayoutGroup backgroundContentLayout;
        [SerializeField] private RectMask2D backgroundContentMask;
        [SerializeField] private CanvasGroup backgroundContentCanvasGroup;
        [SerializeField] private bool useContentLayoutSettings = true;
        [SerializeField] private FlexibleLayoutAxis backgroundFixedAxis = FlexibleLayoutAxis.Horizontal;
        [SerializeField] private FlexibleLayoutAxisPolicy backgroundAxisPolicy = FlexibleLayoutAxisPolicy.Fixed;
        [SerializeField, Min(1f)] private float backgroundBreakpoint = 720f;
        [SerializeField, Min(0f)] private float backgroundSpacing;
        [SerializeField] private RectOffset backgroundPadding = new();
        [SerializeField] private FlexibleLayoutOverflow backgroundOverflow = FlexibleLayoutOverflow.ShrinkBelowMinimum;
        [SerializeField] private bool backgroundIncludeInactiveChildren;
        [SerializeField] private TextAnchor backgroundChildAlignment = TextAnchor.MiddleCenter;
        [SerializeField] private bool backgroundClipContent;
        [SerializeField] private bool overrideBackgroundContentMargins;
        [SerializeField] private RectOffset backgroundContentMargins = new();
        [SerializeField, HideInInspector] private bool previousUseContentLayoutSettings = true;
        [SerializeField, HideInInspector] private bool previousOverrideBackgroundContentMargins;
        [SerializeField, HideInInspector] private bool hasBackgroundAuthoredRect;
        [SerializeField, HideInInspector] private Vector2 backgroundAuthoredAnchorMin;
        [SerializeField, HideInInspector] private Vector2 backgroundAuthoredAnchorMax = Vector2.one;
        [SerializeField, HideInInspector] private Vector2 backgroundAuthoredPivot = new(0.5f, 0.5f);
        [SerializeField, HideInInspector] private Vector2 backgroundAuthoredPosition;
        [SerializeField, HideInInspector] private Vector2 backgroundAuthoredSizeDelta;

        public string NodeId => nodeId;
        public FlexibleLayoutAxis CurrentAxis => ResolveAxis(ContentRoot.rect.width);
        public RectTransform ContentRoot => contentRoot != null ? contentRoot : rectTransform;
        public FlexibleContentLayoutGroup ContentLayout => contentLayout;
        public bool ClipContent => clipContent;
        public RectOffset ContentPadding => padding;
        public RectOffset ContentMargins => contentMargins;
        public bool OverridesContentMargins => overrideContentMargins;
        public RectTransform BackgroundContentRoot => backgroundContentRoot;
        public FlexibleContentLayoutGroup BackgroundContentLayout => backgroundContentLayout;
        public CanvasGroup BackgroundContentCanvasGroup => backgroundContentCanvasGroup;
        public bool UsesContentLayoutSettingsForBackground => useContentLayoutSettings;
        public bool BackgroundClipContent => backgroundClipContent;

        public override void CalculateLayoutInputHorizontal()
        {
            SyncContentLayout();
            SetLayoutInputForAxis(-1f, -1f, -1f, 0);
        }

        public override void CalculateLayoutInputVertical()
        {
            SetLayoutInputForAxis(-1f, -1f, -1f, 1);
        }

        public override void SetLayoutHorizontal()
        {
            SyncContentLayout();
        }

        public override void SetLayoutVertical()
        {
            SyncContentLayout();
        }

        public void Add(Transform child, float weight = 1f)
        {
            Add(child, FlexibleLayoutContentLayer.Content, weight);
        }

        public void Add(
            Transform child,
            FlexibleLayoutContentLayer layer,
            float weight = 1f)
        {
            if (child == null)
            {
                return;
            }

            child.SetParent(GetLayerRoot(layer), false);
            var item = child.GetComponent<FlexibleLayoutItem>() ?? child.gameObject.AddComponent<FlexibleLayoutItem>();
            item.Configure(FlexibleLayoutSizeMode.Weighted, weight);
            Rebuild();
        }

        public void Remove(Transform child)
        {
            Remove(child, FlexibleLayoutContentLayer.Content);
        }

        public void Remove(Transform child, FlexibleLayoutContentLayer layer)
        {
            if (child != null && child.parent == GetLayerRoot(layer))
            {
                child.SetParent(null, false);
                Rebuild();
            }
        }

        public void SetWeight(Transform child, float weight)
        {
            SetWeight(child, FlexibleLayoutContentLayer.Content, weight);
        }

        public void SetWeight(
            Transform child,
            FlexibleLayoutContentLayer layer,
            float weight)
        {
            if (child == null || child.parent != GetLayerRoot(layer))
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

        public void SetPadding(int left, int right, int top, int bottom)
        {
            padding = CreateInsets(left, right, top, bottom);
            Rebuild();
        }

        public void SetContentMargins(int left, int right, int top, int bottom)
        {
            contentMargins = CreateInsets(left, right, top, bottom);
            overrideContentMargins = true;
            Rebuild();
        }

        public void UseAuthoredContentOffsets()
        {
            overrideContentMargins = false;
            Rebuild();
        }

        public void SetClipContent(bool value)
        {
            clipContent = value;
            SyncContentLayout();
        }

        public void SetBackgroundUsesContentLayoutSettings(bool value)
        {
            if (useContentLayoutSettings == value)
            {
                return;
            }

            if (value && !overrideBackgroundContentMargins) CaptureBackgroundAuthoredRect();
            else if (!value && !overrideBackgroundContentMargins) RestoreBackgroundAuthoredRect();
            useContentLayoutSettings = value;
            previousUseContentLayoutSettings = value;
            Rebuild();
        }

        public void ConfigureBackgroundLayout(
            FlexibleLayoutAxis axis,
            FlexibleLayoutAxisPolicy policy,
            float widthBreakpoint,
            float childSpacing,
            RectOffset childPadding,
            FlexibleLayoutOverflow overflowPolicy,
            bool includeInactive,
            TextAnchor alignment)
        {
            SetBackgroundUsesContentLayoutSettings(false);
            backgroundFixedAxis = axis;
            backgroundAxisPolicy = policy;
            backgroundBreakpoint = Mathf.Max(1f, widthBreakpoint);
            backgroundSpacing = Mathf.Max(0f, childSpacing);
            backgroundPadding = CopyInsets(childPadding);
            backgroundOverflow = overflowPolicy;
            backgroundIncludeInactiveChildren = includeInactive;
            backgroundChildAlignment = alignment;
            Rebuild();
        }

        public void SetBackgroundContentMargins(int left, int right, int top, int bottom)
        {
            SetBackgroundUsesContentLayoutSettings(false);
            if (!overrideBackgroundContentMargins) CaptureBackgroundAuthoredRect();
            backgroundContentMargins = CreateInsets(left, right, top, bottom);
            overrideBackgroundContentMargins = true;
            previousOverrideBackgroundContentMargins = true;
            Rebuild();
        }

        public void UseAuthoredBackgroundContentOffsets()
        {
            SetBackgroundUsesContentLayoutSettings(false);
            overrideBackgroundContentMargins = false;
            previousOverrideBackgroundContentMargins = false;
            RestoreBackgroundAuthoredRect();
            Rebuild();
        }

        public void SetBackgroundClipContent(bool value)
        {
            SetBackgroundUsesContentLayoutSettings(false);
            backgroundClipContent = value;
            SyncContentLayout();
        }

        public void SetBackgroundInteraction(bool interactable, bool blocksRaycasts)
        {
            RequireBackgroundLayer();
            backgroundContentCanvasGroup.interactable = interactable;
            backgroundContentCanvasGroup.blocksRaycasts = blocksRaycasts;
        }

        public void SetBackgroundAlpha(float value)
        {
            RequireBackgroundLayer();
            backgroundContentCanvasGroup.alpha = Mathf.Clamp01(value);
        }

        public RectTransform GetLayerRoot(FlexibleLayoutContentLayer layer)
        {
            return layer switch
            {
                FlexibleLayoutContentLayer.Content => ContentRoot,
                FlexibleLayoutContentLayer.BackgroundContent =>
                    backgroundContentRoot != null
                        ? backgroundContentRoot
                        : throw new InvalidOperationException(
                            "This FlexibleLayoutPanel has no BackgroundContentLayer. Upgrade the asset explicitly before using it."),
                _ => throw new ArgumentOutOfRangeException(nameof(layer), layer, null)
            };
        }

        public FlexibleContentLayoutGroup GetLayerLayout(FlexibleLayoutContentLayer layer)
        {
            return layer switch
            {
                FlexibleLayoutContentLayer.Content => contentLayout,
                FlexibleLayoutContentLayer.BackgroundContent =>
                    backgroundContentLayout != null
                        ? backgroundContentLayout
                        : throw new InvalidOperationException(
                            "This FlexibleLayoutPanel has no BackgroundContentLayer layout."),
                _ => throw new ArgumentOutOfRangeException(nameof(layer), layer, null)
            };
        }

        public void Rebuild()
        {
            SetDirty();
            SyncContentLayout();
            if (contentLayout != null) contentLayout.Rebuild();
            if (backgroundContentLayout != null) backgroundContentLayout.Rebuild();
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
            SyncContentLayout();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            breakpoint = Mathf.Max(1f, breakpoint);
            spacing = Mathf.Max(0f, spacing);
            backgroundBreakpoint = Mathf.Max(1f, backgroundBreakpoint);
            backgroundSpacing = Mathf.Max(0f, backgroundSpacing);
            EnsureNodeId();
            SynchronizeBackgroundModeTransition();
            SynchronizeBackgroundMarginTransition();
            SyncContentLayout();
        }
#endif

        private void SyncContentLayout()
        {
            var container = ContentRoot;
            if (container == rectTransform)
            {
                contentLayout = null;
                return;
            }
            if (contentLayout == null || contentLayout.transform != container)
                contentLayout = container.GetComponent<FlexibleContentLayoutGroup>();
            if (contentLayout == null)
                contentLayout = container.gameObject.AddComponent<FlexibleContentLayoutGroup>();
            if (overrideContentMargins)
            {
                container.offsetMin = new Vector2(contentMargins.left, contentMargins.bottom);
                container.offsetMax = new Vector2(-contentMargins.right, -contentMargins.top);
            }
            contentLayout.Configure(fixedAxis, axisPolicy, breakpoint, spacing, padding, overflow,
                includeInactiveChildren, childAlignment);
            if (contentMask == null || contentMask.transform != container)
                contentMask = container.GetComponent<RectMask2D>();
            if (contentMask == null && clipContent)
                contentMask = container.gameObject.AddComponent<RectMask2D>();
            if (contentMask != null) contentMask.enabled = clipContent;
            SyncBackgroundContentLayout(container);
        }

        private void SyncBackgroundContentLayout(RectTransform contentContainer)
        {
            if (backgroundContentRoot == null)
            {
                return;
            }
            if (backgroundContentLayout == null ||
                backgroundContentLayout.transform != backgroundContentRoot)
            {
                backgroundContentLayout =
                    backgroundContentRoot.GetComponent<FlexibleContentLayoutGroup>();
            }
            if (backgroundContentCanvasGroup == null ||
                backgroundContentCanvasGroup.transform != backgroundContentRoot)
            {
                backgroundContentCanvasGroup =
                    backgroundContentRoot.GetComponent<CanvasGroup>();
            }

            SynchronizeBackgroundModeTransition();
            SynchronizeBackgroundMarginTransition();

            if (useContentLayoutSettings)
            {
                CopyRect(contentContainer, backgroundContentRoot);
                backgroundContentLayout?.Configure(
                    fixedAxis, axisPolicy, breakpoint, spacing, padding, overflow,
                    includeInactiveChildren, childAlignment);
            }
            else
            {
                if (overrideBackgroundContentMargins)
                {
                    backgroundContentRoot.offsetMin = new Vector2(
                        backgroundContentMargins.left,
                        backgroundContentMargins.bottom);
                    backgroundContentRoot.offsetMax = new Vector2(
                        -backgroundContentMargins.right,
                        -backgroundContentMargins.top);
                }
                backgroundContentLayout?.Configure(
                    backgroundFixedAxis,
                    backgroundAxisPolicy,
                    backgroundBreakpoint,
                    backgroundSpacing,
                    backgroundPadding,
                    backgroundOverflow,
                    backgroundIncludeInactiveChildren,
                    backgroundChildAlignment);
            }

            if (backgroundContentMask == null ||
                backgroundContentMask.transform != backgroundContentRoot)
            {
                backgroundContentMask = backgroundContentRoot.GetComponent<RectMask2D>();
            }
            if (backgroundContentMask != null)
            {
                backgroundContentMask.enabled = useContentLayoutSettings
                    ? clipContent
                    : backgroundClipContent;
            }
        }

        private void RequireBackgroundLayer()
        {
            if (backgroundContentRoot == null || backgroundContentCanvasGroup == null)
            {
                throw new InvalidOperationException(
                    "This FlexibleLayoutPanel has no configured BackgroundContentLayer.");
            }
        }

        private void SynchronizeBackgroundModeTransition()
        {
            if (backgroundContentRoot == null ||
                previousUseContentLayoutSettings == useContentLayoutSettings)
            {
                return;
            }

            if (useContentLayoutSettings && !overrideBackgroundContentMargins) CaptureBackgroundAuthoredRect();
            else if (!useContentLayoutSettings && !overrideBackgroundContentMargins) RestoreBackgroundAuthoredRect();
            previousUseContentLayoutSettings = useContentLayoutSettings;
        }

        private void SynchronizeBackgroundMarginTransition()
        {
            if (backgroundContentRoot == null || useContentLayoutSettings ||
                previousOverrideBackgroundContentMargins == overrideBackgroundContentMargins)
            {
                return;
            }

            if (overrideBackgroundContentMargins) CaptureBackgroundAuthoredRect();
            else RestoreBackgroundAuthoredRect();
            previousOverrideBackgroundContentMargins = overrideBackgroundContentMargins;
        }

        private void CaptureBackgroundAuthoredRect()
        {
            if (backgroundContentRoot == null) return;
            backgroundAuthoredAnchorMin = backgroundContentRoot.anchorMin;
            backgroundAuthoredAnchorMax = backgroundContentRoot.anchorMax;
            backgroundAuthoredPivot = backgroundContentRoot.pivot;
            backgroundAuthoredPosition = backgroundContentRoot.anchoredPosition;
            backgroundAuthoredSizeDelta = backgroundContentRoot.sizeDelta;
            hasBackgroundAuthoredRect = true;
        }

        private void RestoreBackgroundAuthoredRect()
        {
            if (backgroundContentRoot == null || !hasBackgroundAuthoredRect) return;
            backgroundContentRoot.anchorMin = backgroundAuthoredAnchorMin;
            backgroundContentRoot.anchorMax = backgroundAuthoredAnchorMax;
            backgroundContentRoot.pivot = backgroundAuthoredPivot;
            backgroundContentRoot.anchoredPosition = backgroundAuthoredPosition;
            backgroundContentRoot.sizeDelta = backgroundAuthoredSizeDelta;
        }

        private static void CopyRect(RectTransform source, RectTransform destination)
        {
            if (source.anchorMin == destination.anchorMin &&
                source.anchorMax == destination.anchorMax &&
                source.pivot == destination.pivot &&
                source.anchoredPosition == destination.anchoredPosition &&
                source.sizeDelta == destination.sizeDelta)
            {
                return;
            }

            destination.anchorMin = source.anchorMin;
            destination.anchorMax = source.anchorMax;
            destination.pivot = source.pivot;
            destination.anchoredPosition = source.anchoredPosition;
            destination.sizeDelta = source.sizeDelta;
        }

        private static RectOffset CreateInsets(int left, int right, int top, int bottom)
        {
            return new RectOffset(
                Mathf.Max(0, left),
                Mathf.Max(0, right),
                Mathf.Max(0, top),
                Mathf.Max(0, bottom));
        }

        private static RectOffset CopyInsets(RectOffset source)
        {
            return source == null
                ? new RectOffset()
                : new RectOffset(source.left, source.right, source.top, source.bottom);
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
