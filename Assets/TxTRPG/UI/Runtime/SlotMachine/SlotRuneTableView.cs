using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.SlotMachine
{
    // Rows of the rune line table, one per rune line, created from an inactive template and reused. Rows shrink to the
    // minimum height; when even that does not fit, the leading lines are shown and the last row shows the overflow
    // text. Rebuilt only when the lines or the table size change (a size change is applied in the next LateUpdate).
    // Takes no input.
    [DisallowMultipleComponent]
    public sealed class SlotRuneTableView : MonoBehaviour
    {
        [SerializeField, Tooltip("Inactive row used to create rows.")] private SlotRuneLineRow rowTemplate;
        [SerializeField] private List<SlotRuneLineRow> rows = new();
        [SerializeField, Range(.05f, 1f), Tooltip("Largest row height as a fraction of the table height.")]
        private float maximumRowHeightRatio = .22f;
        [SerializeField, Min(1f), Tooltip("Rows never get shorter than this (canvas units).")]
        private float minimumRowHeight = 18f;
        [SerializeField, Range(0f, .2f), Tooltip("Space between rows as a fraction of the table height.")]
        private float spacingRatio = .033f;
        [SerializeField, Tooltip("RuneTable/Background; its default look is recorded from the Prefab.")]
        private SlotSkinGraphicTarget backgroundSkin = new();

        private IReadOnlyList<SlotRuneLineDisplay> lines = Array.Empty<SlotRuneLineDisplay>();
        private string emptyText = string.Empty;
        private string overflowFormat = string.Empty;
        private int shownRows = -1;
        private Vector2 layoutSize = new(-1f, -1f);
        private bool sizeChanged;
        private SlotMachinePanelSkin skin;

        public int ShownRowCount => Mathf.Max(0, shownRows);

        // (effect, value) of every shown row; a message row reports its message as the effect and an empty value.
        public IReadOnlyList<(string Effect, string Value)> ShownRowTexts
        {
            get
            {
                var result = new List<(string, string)>(ShownRowCount);
                for (var i = 0; i < ShownRowCount && i < rows.Count; i++)
                {
                    var row = rows[i];
                    if (row == null) continue;
                    result.Add(string.IsNullOrEmpty(row.MessageText) ? (row.EffectText, row.ValueText) : (row.MessageText, string.Empty));
                }
                return result;
            }
        }

        public void SetLines(IReadOnlyList<SlotRuneLineDisplay> targetLines, string targetEmptyText, string targetOverflowFormat)
        {
            lines = targetLines ?? Array.Empty<SlotRuneLineDisplay>();
            emptyText = targetEmptyText ?? string.Empty;
            overflowFormat = targetOverflowFormat ?? string.Empty;
            layoutSize = new Vector2(-1f, -1f);
            Refresh();
        }

        // Applies to the table background, the template and every created row; rows created later get the same skin.
        public void ApplySkin(SlotMachinePanelSkin targetSkin)
        {
            skin = targetSkin;
            backgroundSkin.Apply(skin != null ? skin.RuneTableBackground : null, this);
            if (rowTemplate != null) rowTemplate.ApplySkin(skin);
            foreach (var row in rows) if (row != null) row.ApplySkin(skin);
        }

        // Size changes arrive during the canvas layout rebuild, where rows must not be activated or deactivated, so the
        // table is rebuilt in the next LateUpdate. Nothing runs on frames without a size change.
        private void OnRectTransformDimensionsChange() => sizeChanged = true;

        private void LateUpdate()
        {
            if (!sizeChanged) return;
            sizeChanged = false;
            Refresh();
        }

        private void Refresh()
        {
            var rect = (RectTransform)transform;
            var size = rect.rect.size;
            if (size == layoutSize && shownRows >= 0) return;
            layoutSize = size;
            var height = Mathf.Max(0f, size.y);
            var spacing = height * spacingRatio;
            var wanted = Mathf.Max(1, lines.Count);
            var rowHeight = Mathf.Min(height * maximumRowHeightRatio, (height - spacing * (wanted - 1)) / wanted);
            var count = wanted;
            var overflow = 0;
            // Before the first layout pass the table has no height; every line is bound and sized when it gets one.
            if (height > 0f && rowHeight < minimumRowHeight && lines.Count > 1)
            {
                count = Mathf.Clamp(Mathf.FloorToInt((height + spacing) / (minimumRowHeight + spacing)), 1, wanted);
                rowHeight = minimumRowHeight;
                if (count < lines.Count) overflow = lines.Count - (count - 1);
            }
            rowHeight = Mathf.Max(0f, Mathf.Max(rowHeight, Mathf.Min(minimumRowHeight, height)));
            EnsureRows(count);
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null) continue;
                var visible = i < count;
                if (row.gameObject.activeSelf != visible) row.gameObject.SetActive(visible);
                if (!visible) continue;
                var rowRect = (RectTransform)row.transform;
                rowRect.anchorMin = new Vector2(0f, 1f);
                rowRect.anchorMax = new Vector2(1f, 1f);
                rowRect.pivot = new Vector2(.5f, 1f);
                rowRect.sizeDelta = new Vector2(0f, rowHeight);
                rowRect.anchoredPosition = new Vector2(0f, -i * (rowHeight + spacing));
                if (lines.Count == 0) row.ShowMessage(emptyText);
                else if (overflow > 0 && i == count - 1) row.ShowMessage(FormatOverflow(overflow));
                else row.Bind(lines[i].EffectText, lines[i].ValueText);
            }
            shownRows = count;
        }

        private string FormatOverflow(int hidden)
        {
            try { return string.Format(overflowFormat, hidden); }
            catch (FormatException) { return overflowFormat; }
        }

        // Rows are created only when more are needed and are kept for reuse.
        private void EnsureRows(int count)
        {
            rows.RemoveAll(row => row == null);
            while (rows.Count < count && rowTemplate != null)
            {
                var row = Instantiate(rowTemplate, transform, false);
                row.name = $"RuneLineRow_{rows.Count + 1}";
                row.gameObject.SetActive(true);
                row.ApplySkin(skin);
                rows.Add(row);
            }
        }

#if UNITY_EDITOR
        public void ConfigureForEditor(SlotRuneLineRow template, IEnumerable<SlotRuneLineRow> existingRows)
        {
            rowTemplate = template;
            rows = new List<SlotRuneLineRow>(existingRows ?? Array.Empty<SlotRuneLineRow>());
            shownRows = -1;
        }

        public bool ConfigureSkinForEditor(Graphic targetBackground, bool overwrite)
        {
            var changed = backgroundSkin.Record(targetBackground, overwrite);
            if (rowTemplate != null) changed |= rowTemplate.ConfigureSkinForEditor(overwrite);
            return changed;
        }

        public SlotSkinGraphicTarget BackgroundSkinForEditor => backgroundSkin;
        public SlotRuneLineRow RowTemplateForEditor => rowTemplate;
        public IReadOnlyList<SlotRuneLineRow> RowsForEditor => rows;

        public void RefreshForEditor()
        {
            layoutSize = new Vector2(-1f, -1f);
            Refresh();
        }
#endif
    }
}
