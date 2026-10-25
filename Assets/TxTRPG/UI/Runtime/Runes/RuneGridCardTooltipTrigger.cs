using System;
using System.Collections.Generic;
using TxTRPG.UI.Tooltips;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TxTRPG.UI.Runes
{
    // Opens the rune tooltip of one rune card. The card root receives the enter/exit of both HitAreas (CardArea and
    // BadgeArea), so moving between them is one hover. The anchor rectangle is CardArea, wherever the pointer entered.
    // It implements no drag or scroll handler: wheel and drag events reach the grid's ScrollRect. A rebind to another line
    // or rune definition, disabling or releasing the card closes its tooltip at once; a weight change keeps it.
    [DisallowMultipleComponent]
    public sealed class RuneGridCardTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ITooltipAnchor
    {
        private static readonly Vector3[] Corners = new Vector3[4];

        [SerializeField] private RuneGridCardView card;

        private readonly List<TooltipBlock> fallbackBlocks = new(2);
        private TooltipPresenter presenter;
        private RuneTooltipCatalog catalog;
        private string key = string.Empty;
        private string definitionId = string.Empty;
        private string displayName = string.Empty;
        private bool bound;

        public RuneGridCardView Card => card;
        public bool IsBound => bound && presenter != null;
        public string DefinitionId => definitionId;
        public bool IsPointerOver { get; private set; }
        public bool IsAnchorValid => this != null && isActiveAndEnabled && bound && card != null && card.CardArea != null;

        public void Bind(TooltipPresenter tooltipPresenter, RuneTooltipCatalog tooltipCatalog, RuneGridEntryDisplay entry)
        {
            var changed = !bound || !string.Equals(key, entry.Key, StringComparison.Ordinal) ||
                !string.Equals(definitionId, entry.DefinitionId, StringComparison.Ordinal) || presenter != tooltipPresenter || catalog != tooltipCatalog;
            if (changed) HideOwnTooltip();
            presenter = tooltipPresenter;
            catalog = tooltipCatalog;
            key = entry.Key;
            definitionId = entry.DefinitionId;
            displayName = entry.DisplayName;
            bound = true;
        }

        public void Unbind()
        {
            HideOwnTooltip();
            bound = false;
            IsPointerOver = false;
            key = definitionId = displayName = string.Empty;
        }

        // The content shown for the bound rune: the catalog content, or the fallback (name and "설명이 없습니다.").
        public IReadOnlyList<TooltipBlock> CreateBlocks()
        {
            if (catalog != null && catalog.TryGetContent(definitionId, out var content)) return content.Blocks;
            fallbackBlocks.Clear();
            fallbackBlocks.Add(TooltipBlock.Title(displayName));
            fallbackBlocks.Add(TooltipBlock.Text(TooltipView.MissingDescription));
            return fallbackBlocks;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (TooltipPresenter.IsTouch(eventData)) return;
            IsPointerOver = true;
            if (IsBound) presenter.RequestRoot(this, CreateBlocks());
        }

        // Moving between the two HitAreas keeps the hover.
        public void OnPointerExit(PointerEventData eventData)
        {
            var next = eventData?.pointerCurrentRaycast.gameObject;
            if (next != null && next.transform.IsChildOf(transform)) return;
            IsPointerOver = false;
        }

        // Play Mode probes: the same path as a pointer entering or leaving the card.
        public void SimulatePointerForTests(bool over)
        {
            IsPointerOver = over;
            if (over && IsBound) presenter.RequestRoot(this, CreateBlocks());
        }

        public bool TryGetAnchorRect(RectTransform space, out Rect rect)
        {
            rect = default;
            if (!IsAnchorValid || space == null) return false;
            card.CardArea.GetWorldCorners(Corners);
            Vector2 min = space.InverseTransformPoint(Corners[0]);
            Vector2 max = space.InverseTransformPoint(Corners[2]);
            rect = Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
            return true;
        }

        private void OnDisable()
        {
            IsPointerOver = false;
            HideOwnTooltip();
        }

        private void HideOwnTooltip()
        {
            if (presenter != null) presenter.Hide(this);
        }

#if UNITY_EDITOR
        private void Reset() => card = GetComponent<RuneGridCardView>();
        public void ConfigureForEditor(RuneGridCardView view) => card = view;
#endif
    }
}
