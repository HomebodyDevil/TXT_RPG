using System;
using System.Collections.Generic;
using UnityEngine;

namespace TxTRPG.UI.Tooltips
{
    // One keyword: {kw:id} in a tooltip text shows displayName highlighted, and hovering it opens a tooltip with the
    // keyword blocks (title = displayName and icon when set). The id is stable: never change it after release, contents
    // refer to it by text. relatedEntries record which runes, items or skills the keyword belongs to; the Inspector
    // compares them with the actual uses.
    [CreateAssetMenu(menuName = "TxT RPG/UI/Tooltip Keyword", fileName = "TooltipKeyword")]
    public sealed class TooltipKeywordDefinition : ScriptableObject
    {
        [SerializeField, Tooltip("Stable ID used by {kw:id}. Trimmed, case-sensitive. Never change it after release.")]
        private string id = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField, Tooltip("Optional icon shown at the top of the keyword tooltip.")] private Sprite icon;
        [SerializeField, Tooltip("Blocks of the keyword tooltip below its title. Text blocks may contain other keywords.")]
        private List<TooltipBlock> blocks = new();
        [SerializeField] private List<TooltipRelatedEntry> relatedEntries = new();

        public string Id => id?.Trim() ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName.Trim();
        public Sprite Icon => icon;
        public IReadOnlyList<TooltipBlock> Blocks => blocks ?? (IReadOnlyList<TooltipBlock>)Array.Empty<TooltipBlock>();
        public IReadOnlyList<TooltipRelatedEntry> RelatedEntries => relatedEntries ?? (IReadOnlyList<TooltipRelatedEntry>)Array.Empty<TooltipRelatedEntry>();

        public void Configure(string keywordId, string name, Sprite keywordIcon, IEnumerable<TooltipBlock> keywordBlocks,
            IEnumerable<TooltipRelatedEntry> related = null)
        {
            id = keywordId ?? string.Empty;
            displayName = name ?? string.Empty;
            icon = keywordIcon;
            blocks = keywordBlocks != null ? new List<TooltipBlock>(keywordBlocks) : new List<TooltipBlock>();
            relatedEntries = related != null ? new List<TooltipRelatedEntry>(related) : new List<TooltipRelatedEntry>();
        }

        // Blocks shown in the keyword tooltip: title, optional icon, then the authored blocks.
        public IReadOnlyList<TooltipBlock> CreateTooltipBlocks()
        {
            var result = new List<TooltipBlock>(Blocks.Count + 2) { TooltipBlock.Title(DisplayName) };
            if (icon != null) result.Add(TooltipBlock.Image(icon, TooltipView.KeywordIconHeight));
            foreach (var block in Blocks) if (block != null) result.Add(block);
            return result;
        }
    }
}
