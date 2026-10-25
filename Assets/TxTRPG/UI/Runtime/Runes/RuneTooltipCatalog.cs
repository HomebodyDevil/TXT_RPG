using System;
using System.Collections.Generic;
using TxTRPG.UI.Tooltips;
using UnityEngine;

namespace TxTRPG.UI.Runes
{
    // UI-side pairing of rune definition IDs and tooltip contents (like RuneIconCatalog). Game rule data has no tooltip
    // field; a rune without an entry shows the fallback tooltip (its name and "설명이 없습니다.").
    [CreateAssetMenu(menuName = "TxT RPG/UI/Rune Tooltip Catalog", fileName = "RuneTooltipCatalog")]
    public sealed class RuneTooltipCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private string definitionId = string.Empty;
            [SerializeField] private TooltipContent content;

            public Entry() { }
            public Entry(string id, TooltipContent tooltip) { definitionId = id; content = tooltip; }

            public string DefinitionId => definitionId?.Trim() ?? string.Empty;
            public TooltipContent Content => content;
        }

        [SerializeField] private List<Entry> entries = new();

        public IReadOnlyList<Entry> Entries => entries ?? (IReadOnlyList<Entry>)Array.Empty<Entry>();

        public void SetEntries(IEnumerable<Entry> source) =>
            entries = source != null ? new List<Entry>(source) : new List<Entry>();

        // IDs are trimmed and compared case-sensitively. With duplicates only the first entry counts; an entry without
        // content means no tooltip content.
        public bool TryGetContent(string definitionId, out TooltipContent content)
        {
            content = null;
            var id = definitionId?.Trim();
            if (string.IsNullOrEmpty(id)) return false;
            foreach (var entry in Entries)
            {
                if (entry == null || !string.Equals(entry.DefinitionId, id, StringComparison.Ordinal)) continue;
                content = entry.Content;
                return content != null;
            }
            return false;
        }

        public bool Validate(out string reason)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var source = Entries;
            for (var i = 0; i < source.Count; i++)
            {
                var entry = source[i];
                if (entry == null || entry.DefinitionId.Length == 0) { reason = $"Rune tooltip entry {i + 1} has an empty definition id."; return false; }
                if (!ids.Add(entry.DefinitionId)) { reason = $"Rune tooltip definition id '{entry.DefinitionId}' is duplicated."; return false; }
            }
            reason = string.Empty;
            return true;
        }
    }
}
