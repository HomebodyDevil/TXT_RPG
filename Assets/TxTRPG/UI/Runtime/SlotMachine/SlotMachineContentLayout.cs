using System;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.SlotMachine
{
    public enum SlotMachineContentArrangement
    {
        SideBySide = 0,
        Stacked = 1
    }

    // Lays out the slot machine Content: the reel machine and the summary area side by side when the content is wide
    // enough, or stacked (reel machine on top) when its width/height ratio is below stackBelowAspectRatio. It also
    // places the total box and the rune table area inside the summary area for the current arrangement (stacked puts the
    // total box on the left). Recalculated only through the standard LayoutGroup updates (size or child changes).
    [DisallowMultipleComponent]
    public sealed class SlotMachineContentLayout : LayoutGroup
    {
        [SerializeField] private RectTransform reelMachine;
        [SerializeField] private RectTransform summaryArea;
        [SerializeField] private RectTransform totalBox;
        [SerializeField] private RectTransform runeTableArea;

        [Header("Arrangement switch")]
        [SerializeField, Min(.1f), Tooltip("Content width/height ratios below this stack the reel machine above the summary.")]
        private float stackBelowAspectRatio = 1.3f;
        [SerializeField, Range(0f, .5f), Tooltip("The arrangement changes only beyond threshold ± this value, so it does not flicker near the threshold.")]
        private float hysteresis = .05f;

        [Header("Side by side (weights along the width)")]
        [SerializeField, Min(0f)] private float sideReelMachineWidth = .62f;
        [SerializeField, Min(0f)] private float sideGapWidth = .04f;
        [SerializeField, Min(0f)] private float sideSummaryWidth = .31f;

        [Header("Stacked (weights along the height)")]
        [SerializeField, Min(0f)] private float stackedReelMachineHeight = .62f;
        [SerializeField, Min(0f)] private float stackedGapHeight = .04f;
        [SerializeField, Min(0f)] private float stackedSummaryHeight = .34f;

        [Header("Summary area (anchor rectangles: x min, y min, x max, y max)")]
        [SerializeField] private Vector4 sideTotalBoxAnchors = new(.068f, .752f, .932f, .941f);
        [SerializeField] private Vector4 sideRuneTableAnchors = new(.068f, .044f, .932f, .70f);
        [SerializeField] private Vector4 stackedTotalBoxAnchors = new(.04f, .1f, .35f, .9f);
        [SerializeField] private Vector4 stackedRuneTableAnchors = new(.39f, .05f, .96f, .95f);

        private bool hasArrangement;

        public SlotMachineContentArrangement Arrangement { get; private set; }
        public float StackBelowAspectRatio => stackBelowAspectRatio;
        public event Action<SlotMachineContentArrangement> ArrangementChanged;

        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();
            SetLayoutInputForAxis(padding.horizontal, padding.horizontal, -1f, 0);
        }

        public override void CalculateLayoutInputVertical() => SetLayoutInputForAxis(padding.vertical, padding.vertical, -1f, 1);

        public override void SetLayoutHorizontal() => Arrange();
        public override void SetLayoutVertical() => Arrange();

        // Arrangement for a content size, with the hysteresis applied relative to the current arrangement.
        public SlotMachineContentArrangement Resolve(Vector2 size)
        {
            var ratio = size.y <= 0f ? float.MaxValue : size.x / size.y;
            if (!hasArrangement) return ratio < stackBelowAspectRatio ? SlotMachineContentArrangement.Stacked : SlotMachineContentArrangement.SideBySide;
            return Arrangement == SlotMachineContentArrangement.SideBySide
                ? ratio < stackBelowAspectRatio - hysteresis ? SlotMachineContentArrangement.Stacked : SlotMachineContentArrangement.SideBySide
                : ratio >= stackBelowAspectRatio + hysteresis ? SlotMachineContentArrangement.SideBySide : SlotMachineContentArrangement.Stacked;
        }

        private void Arrange()
        {
            m_Tracker.Clear();
            var size = rectTransform.rect.size;
            var arrangement = Resolve(size);
            var changed = !hasArrangement || arrangement != Arrangement;
            Arrangement = arrangement;
            hasArrangement = true;
            var width = Mathf.Max(0f, size.x - padding.horizontal);
            var height = Mathf.Max(0f, size.y - padding.vertical);
            if (arrangement == SlotMachineContentArrangement.SideBySide)
            {
                var total = Mathf.Max(.0001f, sideReelMachineWidth + sideGapWidth + sideSummaryWidth);
                var reelWidth = width * sideReelMachineWidth / total;
                var gap = width * sideGapWidth / total;
                Place(reelMachine, padding.left, reelWidth, padding.top, height);
                Place(summaryArea, padding.left + reelWidth + gap, width * sideSummaryWidth / total, padding.top, height);
                Anchor(totalBox, sideTotalBoxAnchors);
                Anchor(runeTableArea, sideRuneTableAnchors);
            }
            else
            {
                var total = Mathf.Max(.0001f, stackedReelMachineHeight + stackedGapHeight + stackedSummaryHeight);
                var reelHeight = height * stackedReelMachineHeight / total;
                var gap = height * stackedGapHeight / total;
                Place(reelMachine, padding.left, width, padding.top, reelHeight);
                Place(summaryArea, padding.left, width, padding.top + reelHeight + gap, height * stackedSummaryHeight / total);
                Anchor(totalBox, stackedTotalBoxAnchors);
                Anchor(runeTableArea, stackedRuneTableAnchors);
            }
            if (changed) ArrangementChanged?.Invoke(arrangement);
        }

        private void Place(RectTransform child, float x, float width, float y, float height)
        {
            if (child == null || child.parent != rectTransform) return;
            SetChildAlongAxis(child, 0, x, width);
            SetChildAlongAxis(child, 1, y, height);
        }

        private void Anchor(RectTransform target, Vector4 anchors)
        {
            if (target == null) return;
            m_Tracker.Add(this, target, DrivenTransformProperties.Anchors | DrivenTransformProperties.SizeDelta | DrivenTransformProperties.AnchoredPosition);
            target.anchorMin = new Vector2(anchors.x, anchors.y);
            target.anchorMax = new Vector2(anchors.z, anchors.w);
            target.sizeDelta = Vector2.zero;
            target.anchoredPosition = Vector2.zero;
        }

#if UNITY_EDITOR
        public void ConfigureForEditor(RectTransform targetReelMachine, RectTransform targetSummary, RectTransform targetTotalBox,
            RectTransform targetRuneTableArea)
        {
            reelMachine = targetReelMachine;
            summaryArea = targetSummary;
            totalBox = targetTotalBox;
            runeTableArea = targetRuneTableArea;
            SetDirty();
        }

        public RectTransform ReelMachineForEditor => reelMachine;
        public RectTransform SummaryAreaForEditor => summaryArea;
#endif
    }
}
