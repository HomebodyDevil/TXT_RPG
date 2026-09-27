using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TxTRPG.UI
{
    // Owns only the panel surface geometry. Grid/preset and entry ownership remain in ActionGridPanel.
    public sealed class ActionGridSurfaceLayout : MonoBehaviour, IBeginDragHandler, IScrollHandler
    {
        [SerializeField] private ActionGridPanel owner;
        [SerializeField] private RectTransform header, body, viewport, content;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private ConfigurableScrollbarController verticalController;
        [SerializeField] private Scrollbar verticalBar, horizontalBar;
        [SerializeField] private RectTransform oppositeArea;
        [SerializeField] private bool manageHeaderLayout;
        [SerializeField] private float headerHeight = 58, headerBodyGap = 8;
        [SerializeField] private RectOffset bodyMargins;
        [SerializeField, HideInInspector] private bool baselineCaptured;
        [SerializeField, HideInInspector] private RectState gridContent, gridViewport;
        [SerializeField, HideInInspector] private bool gridHorizontal, gridVertical, gridControllerEnabled, gridBarActive, gridOppositeActive;
        [SerializeField, HideInInspector] private Scrollbar gridHorizontalBar, gridVerticalBar;
        [SerializeField, HideInInspector] private ScrollRect.ScrollbarVisibility gridHorizontalVisibility, gridVerticalVisibility;
        [SerializeField, HideInInspector] private GridLayoutGroup.Corner gridStartCorner;
        [SerializeField, HideInInspector] private GridLayoutGroup.Axis gridStartAxis;
        private bool rowActive, wasOverflowing;
        public bool HeaderVisible => header != null && header.gameObject.activeSelf;
        public float HeaderReservation => manageHeaderLayout && HeaderVisible ? headerHeight + headerBodyGap : 0;
        public Scrollbar HorizontalScrollbar => horizontalBar;
        public bool IsRowActive => rowActive;
        public Vector2 RowAvailableSize => body != null ? body.rect.size : viewport.rect.size;

        [Serializable] private struct RectState
        {
            public Vector2 min, max, pivot, size, position;
            public static RectState Capture(RectTransform rect) => new() { min = rect.anchorMin, max = rect.anchorMax, pivot = rect.pivot, size = rect.sizeDelta, position = rect.anchoredPosition };
            public void Restore(RectTransform rect) { rect.anchorMin = min; rect.anchorMax = max; rect.pivot = pivot; rect.sizeDelta = size; rect.anchoredPosition = position; }
        }
        public void InitializeAuthoring(ActionGridPanel panel, RectTransform headerRect, RectTransform bodyRect,
            RectTransform view, RectTransform cells, ScrollRect scroll, ConfigurableScrollbarController controller,
            Scrollbar legacyBar, RectTransform opposite, Scrollbar rowBar, GridLayoutGroup grid)
        {
            owner = panel; header = headerRect; body = bodyRect; viewport = view; content = cells;
            scrollRect = scroll; verticalController = controller; verticalBar = legacyBar; oppositeArea = opposite; horizontalBar = rowBar;
            if (baselineCaptured) return;
            gridContent = RectState.Capture(content); gridViewport = RectState.Capture(viewport);
            gridHorizontal = scroll.horizontal; gridVertical = scroll.vertical;
            gridHorizontalBar = scroll.horizontalScrollbar; gridVerticalBar = scroll.verticalScrollbar;
            gridHorizontalVisibility = scroll.horizontalScrollbarVisibility; gridVerticalVisibility = scroll.verticalScrollbarVisibility;
            gridControllerEnabled = controller != null && controller.enabled;
            gridBarActive = legacyBar != null && legacyBar.gameObject.activeSelf;
            gridOppositeActive = opposite != null && opposite.gameObject.activeSelf;
            gridStartCorner = grid.startCorner; gridStartAxis = grid.startAxis;
            baselineCaptured = true;
        }
        public void ConfigureHeader(bool visible, float height, float gap, RectOffset margins)
        {
            manageHeaderLayout = true;
            headerHeight = ActionGridDisplayPreset.Finite(height, 58, 0, 4096);
            headerBodyGap = ActionGridDisplayPreset.Finite(gap, 8, 0, 4096);
            bodyMargins = margins == null ? new RectOffset() : new RectOffset(Mathf.Clamp(margins.left,0,4096),Mathf.Clamp(margins.right,0,4096),Mathf.Clamp(margins.top,0,4096),Mathf.Clamp(margins.bottom,0,4096));
            if (header != null) header.gameObject.SetActive(visible);
            owner?.RefreshDisplayLayout();
        }
        public void SetHeaderVisible(bool visible)
        {
            if (header != null) header.gameObject.SetActive(visible);
            owner?.RefreshDisplayLayout();
        }
        public void HeaderStateChanged() { if (owner != null && owner.isActiveAndEnabled) owner.RefreshDisplayLayout(); }
        public void ApplyHeader()
        {
            if (!manageHeaderLayout || header == null || body == null) return;
            headerHeight = ActionGridDisplayPreset.Finite(headerHeight, 58, 0, 4096);
            headerBodyGap = ActionGridDisplayPreset.Finite(headerBodyGap, 8, 0, 4096);
            bodyMargins ??= new RectOffset(18,18,18,18);
            header.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, headerHeight);
            var min = new Vector2(Mathf.Clamp(bodyMargins.left,0,4096), Mathf.Clamp(bodyMargins.bottom,0,4096));
            var max = new Vector2(-Mathf.Clamp(bodyMargins.right,0,4096), -Mathf.Clamp(bodyMargins.top,0,4096) - HeaderReservation);
            if (body.offsetMin != min) body.offsetMin = min;
            if (body.offsetMax != max) body.offsetMax = max;
        }
        public void EnterRow()
        {
            if (rowActive) return;
            rowActive = true; wasOverflowing = false;
            if (verticalController != null) verticalController.enabled = false;
            if (verticalBar != null) verticalBar.gameObject.SetActive(false);
            if (oppositeArea != null) oppositeArea.gameObject.SetActive(false);
            scrollRect.StopMovement(); scrollRect.horizontal = true; scrollRect.vertical = false;
            scrollRect.verticalScrollbar = null; scrollRect.horizontalScrollbar = null;
            scrollRect.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(0, 1);
            content.anchoredPosition = Vector2.zero;
        }
        public void RestoreGrid(GridLayoutGroup grid)
        {
            if (!rowActive || !baselineCaptured) return;
            rowActive = false; wasOverflowing = false;
            scrollRect.StopMovement();
            scrollRect.horizontal = gridHorizontal; scrollRect.vertical = gridVertical;
            scrollRect.horizontalScrollbar = gridHorizontalBar; scrollRect.verticalScrollbar = gridVerticalBar;
            scrollRect.horizontalScrollbarVisibility = gridHorizontalVisibility; scrollRect.verticalScrollbarVisibility = gridVerticalVisibility;
            gridContent.Restore(content); gridViewport.Restore(viewport);
            content.anchoredPosition = Vector2.zero;
            grid.startCorner = gridStartCorner; grid.startAxis = gridStartAxis;
            if (horizontalBar != null) horizontalBar.gameObject.SetActive(false);
            if (verticalBar != null) verticalBar.gameObject.SetActive(gridBarActive);
            if (oppositeArea != null) oppositeArea.gameObject.SetActive(gridOppositeActive);
            if (verticalController != null) { verticalController.enabled = gridControllerEnabled; if (gridControllerEnabled) verticalController.Refresh(); }
        }
        public void ConfigureRowViewport(ActionGridSingleRowSettings settings, bool overflow)
        {
            var visible = horizontalBar != null && (settings.scrollbarVisibility == ScrollbarVisibilityMode.Always ||
                (settings.scrollbarVisibility == ScrollbarVisibilityMode.Auto && overflow));
            if (scrollRect.horizontalScrollbar != (visible ? horizontalBar : null)) scrollRect.horizontalScrollbar = visible ? horizontalBar : null;
            if (horizontalBar != null)
            {
                if (horizontalBar.gameObject.activeSelf != visible) horizontalBar.gameObject.SetActive(visible);
                var rect = (RectTransform)horizontalBar.transform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = new Vector2(1,0); rect.pivot = new Vector2(.5f,0);
                rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(0, settings.scrollbarHeight);
            }
            var min = new Vector2(0, visible ? settings.scrollbarHeight + settings.scrollbarGap : 0);
            if (viewport.offsetMin != min) viewport.offsetMin = min;
            if (viewport.offsetMax != Vector2.zero) viewport.offsetMax = Vector2.zero;
        }
        public void ApplyRowGeometry(ActionGridSingleRowResult row, GridLayoutGroup grid, RectOffset padding, ActionGridSingleRowSettings settings)
        {
            grid.startAxis = GridLayoutGroup.Axis.Horizontal; grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = row.Layout.Columns;
            grid.cellSize = row.Layout.CellSize; grid.spacing = row.Layout.Spacing; ActionGridLayoutCalculator.ApplyPadding(grid, padding);
            grid.childAlignment = (TextAnchor)((int)settings.verticalAlignment * 3 + (int)row.Alignment);
            if (grid is ActionGridLayoutGroup aligned) { aligned.ReserveConfiguredColumns = false; aligned.IncompleteRowAlignment = row.Alignment; }
            var width = Mathf.Max(viewport.rect.width, row.Layout.RequiredWidth);
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(0, viewport.rect.height));
            var position = content.anchoredPosition;
            if (!row.Overflow || !wasOverflowing) { scrollRect.StopMovement(); position = Vector2.zero; }
            else { position.x = Mathf.Clamp(position.x, -Mathf.Max(0,width-viewport.rect.width), 0); position.y = 0; }
            content.anchoredPosition = position; wasOverflowing = row.Overflow;
        }
        public void ResetLeft() { scrollRect.StopMovement(); content.anchoredPosition = Vector2.zero; scrollRect.horizontalNormalizedPosition = 0; }
        public void OnBeginDrag(PointerEventData data) { if (rowActive) owner?.NotifyUserScroll(); }
        public void OnScroll(PointerEventData data) { if (rowActive) owner?.NotifyUserScroll(); }
    }
}
