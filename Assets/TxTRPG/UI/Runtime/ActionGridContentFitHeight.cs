using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI
{
    public enum ActionGridContentFitState
    {
        Supplying,
        Disabled,
        MissingReference,
        GridFlow,
        HorizontalAxis,
        UnmanagedBodyLayout
    }

    // Supplies the SingleRow ActionGridPanel height to a parent FlexibleContentLayoutGroup:
    // bodyTop + header + (cell + 2 * gap) + horizontal scrollbar + bodyBottom.
    // It owns only this size calculation; entries, selection and scrolling stay in ActionGridPanel.
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class ActionGridContentFitHeight : MonoBehaviour, IFlexibleLayoutSizeSource
#if UNITY_EDITOR
        , IEditorValidationRefresh
#endif
    {
        public const float DefaultGap = 12f;
        public const float MaximumGap = 4096f;
        private const float ChangeThreshold = 0.5f;

        [SerializeField] private ActionGridPanel panel;
        [SerializeField, Tooltip("Distance between the cell edges and the top and bottom of the Viewport, in Canvas UI units.")]
        private float gap = DefaultGap;

        private FlexibleContentLayoutGroup parentGroup;
        private float reportedHeight = float.NaN;
        private bool rebuildDeferred;
        private bool missingReferenceWarned;
        private bool unmanagedBodyWarned;

        public ActionGridPanel Panel => panel;
        public float Gap => NormalizeGap(gap);
        public float ResolvedHeight { get; private set; } = float.NaN;
        public ActionGridContentFitState State { get; private set; } = ActionGridContentFitState.Disabled;
        public bool IsSupplying => State == ActionGridContentFitState.Supplying;

        public static float NormalizeGap(float value) =>
            float.IsNaN(value) || float.IsInfinity(value) ? DefaultGap : Mathf.Clamp(value, 0f, MaximumGap);

        // Pure height rule; it does not depend on the panel's own height, so the parent layout cannot cycle.
        public static float CalculateHeight(float cellHeight, float gapValue, float marginTop, float marginBottom,
            float headerReservation, float scrollbarReservation)
        {
            cellHeight = ActionGridDisplayPreset.Finite(cellHeight, 64f, 1f, 1024f);
            return Mathf.Max(0f, marginTop) + Mathf.Max(0f, headerReservation) +
                   cellHeight + 2f * NormalizeGap(gapValue) +
                   Mathf.Max(0f, scrollbarReservation) + Mathf.Max(0f, marginBottom);
        }

        public void Initialize(ActionGridPanel target, float gapValue)
        {
            if (panel != null && panel != target) panel.DetachContentFitHeight(this);
            panel = target;
            gap = NormalizeGap(gapValue);
            if (isActiveAndEnabled && panel != null) panel.AttachContentFitHeight(this);
            RefreshPanelAndParent();
        }

        public void SetGap(float value)
        {
            var next = NormalizeGap(value);
            if (Mathf.Approximately(gap, next)) return;
            gap = next;
            RefreshPanelAndParent();
        }

        // Evaluates the current height for a vertical parent axis without touching any layout state.
        public bool TryResolveHeight(out float height, out ActionGridContentFitState state)
        {
            height = float.NaN;
            if (!isActiveAndEnabled) { state = ActionGridContentFitState.Disabled; return false; }
            var surface = panel != null ? panel.SurfaceLayout : null;
            if (panel == null || surface == null || panel.RowSettings == null)
            {
                state = ActionGridContentFitState.MissingReference;
                WarnOnce(ref missingReferenceWarned, "ActionGridContentFitHeight requires an ActionGridPanel with an ActionGridSurfaceLayout. Height supply is disabled.");
                return false;
            }
            if (!panel.isActiveAndEnabled) { state = ActionGridContentFitState.Disabled; return false; }
            if (!panel.IsSingleRow) { state = ActionGridContentFitState.GridFlow; return false; }
            if (!surface.ManagesBodyLayout)
            {
                // Without managed header layout the body margins are authored offsets this component does not own.
                state = ActionGridContentFitState.UnmanagedBodyLayout;
                WarnOnce(ref unmanagedBodyWarned, "ActionGridContentFitHeight requires ActionGridSurfaceLayout to manage the header layout and body margins. Height supply is disabled.");
                return false;
            }
            var options = panel.RowSettings;
            height = CalculateHeight(panel.RowCellHeight, gap, surface.BodyMarginTop, surface.BodyMarginBottom,
                surface.HeaderReservation, surface.RowScrollbarReservation(options, panel.IsRowOverflowing));
            state = ActionGridContentFitState.Supplying;
            return true;
        }

        bool IFlexibleLayoutSizeSource.TryGetSizeRequest(FlexibleLayoutAxis axis, out FlexibleLayoutSizeRequest request)
        {
            request = default;
            var supplying = Evaluate(axis, out var height);
            reportedHeight = supplying ? height : float.NaN;
            if (!supplying) return false;
            request = new FlexibleLayoutSizeRequest(FlexibleLayoutSizeMode.Fixed, 1f, height, height, height);
            return true;
        }

        internal bool SuppliesRowGap(ActionGridPanel owner) =>
            ReferenceEquals(owner, panel) && Evaluate(ResolveParentAxis(), out _);

        // Called by ActionGridPanel after each layout pass. Requests one parent rebuild when the supplied height changes.
        internal void NotifyPanelLayoutApplied()
        {
            var supplying = Evaluate(ResolveParentAxis(), out var height);
            var changed = supplying != !float.IsNaN(reportedHeight) ||
                          (supplying && Mathf.Abs(height - reportedHeight) >= ChangeThreshold);
            if (changed) RequestParentRebuild();
        }

        private bool Evaluate(FlexibleLayoutAxis axis, out float height)
        {
            var supplying = TryResolveHeight(out height, out var state);
            if (supplying && axis != FlexibleLayoutAxis.Vertical)
            {
                supplying = false;
                state = ActionGridContentFitState.HorizontalAxis;
            }
            State = state;
            ResolvedHeight = supplying ? height : float.NaN;
            return supplying;
        }

        private FlexibleLayoutAxis ResolveParentAxis()
        {
            if (parentGroup == null || parentGroup.transform != transform.parent) ResolveParentGroup();
            return parentGroup != null ? parentGroup.CurrentAxis : FlexibleLayoutAxis.Horizontal;
        }

        private void ResolveParentGroup()
        {
            parentGroup = null;
            if (transform.parent != null) transform.parent.TryGetComponent(out parentGroup);
        }

        private void OnEnable()
        {
            ResolveParentGroup();
            gap = NormalizeGap(gap);
            if (panel != null) panel.AttachContentFitHeight(this);
            RefreshPanelAndParent();
        }

        private void OnDisable()
        {
            Canvas.willRenderCanvases -= FlushDeferredRebuild;
            rebuildDeferred = false;
            State = ActionGridContentFitState.Disabled;
            ResolvedHeight = float.NaN;
            if (panel != null)
            {
                panel.DetachContentFitHeight(this);
                if (panel.isActiveAndEnabled) panel.RefreshDisplayLayout();
            }
            RequestParentRebuild();
        }

        private void OnTransformParentChanged()
        {
            ResolveParentGroup();
            RequestParentRebuild();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            gap = NormalizeGap(gap);
            EditorValidationRefreshPump.Enqueue(this);
        }

        void IEditorValidationRefresh.ApplyDeferredValidation()
        {
            if (!isActiveAndEnabled) return;
            if (panel != null) panel.AttachContentFitHeight(this);
            RefreshPanelAndParent();
        }
#endif

        private void RefreshPanelAndParent()
        {
            if (!isActiveAndEnabled) return;
            // The panel applies the gap padding and then reports back through NotifyPanelLayoutApplied.
            if (panel != null && panel.isActiveAndEnabled) panel.RefreshDisplayLayout();
            RequestParentRebuild();
        }

        private void RequestParentRebuild()
        {
            if (CanvasUpdateRegistry.IsRebuildingLayout() || CanvasUpdateRegistry.IsRebuildingGraphics())
            {
                if (!isActiveAndEnabled || rebuildDeferred) return;
                rebuildDeferred = true;
                Canvas.willRenderCanvases -= FlushDeferredRebuild;
                Canvas.willRenderCanvases += FlushDeferredRebuild;
                return;
            }
            if (transform.parent is RectTransform parent) LayoutRebuilder.MarkLayoutForRebuild(parent);
        }

        private void FlushDeferredRebuild()
        {
            if (CanvasUpdateRegistry.IsRebuildingLayout() || CanvasUpdateRegistry.IsRebuildingGraphics()) return;
            Canvas.willRenderCanvases -= FlushDeferredRebuild;
            rebuildDeferred = false;
            if (this != null && transform.parent is RectTransform parent) LayoutRebuilder.MarkLayoutForRebuild(parent);
        }

        private void WarnOnce(ref bool warned, string message)
        {
            if (warned) return;
            warned = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(message, this);
#endif
        }
    }
}
