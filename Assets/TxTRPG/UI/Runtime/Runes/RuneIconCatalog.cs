using System;
using System.Collections.Generic;
using UnityEngine;

namespace TxTRPG.UI.Runes
{
    // UI-side pairing of rune definition IDs and Sprites. Game rule data (rune definitions) has no image field; a missing
    // entry or Sprite makes the rune grid show the name-only fallback card.
    [CreateAssetMenu(menuName = "TxT RPG/UI/Rune Icon Catalog", fileName = "RuneIconCatalog")]
    public sealed class RuneIconCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private string definitionId = string.Empty;
            [SerializeField] private Sprite icon;

            public Entry() { }
            public Entry(string id, Sprite sprite) { definitionId = id; icon = sprite; }

            public string DefinitionId => definitionId?.Trim() ?? string.Empty;
            public Sprite Icon => icon;
        }

        [SerializeField] private List<Entry> entries = new();

        public IReadOnlyList<Entry> Entries => entries;

        public void SetEntries(IEnumerable<Entry> source)
        {
            entries = source != null ? new List<Entry>(source) : new List<Entry>();
        }

        // IDs are trimmed and compared case-sensitively. With duplicates only the first entry counts; an entry without a
        // Sprite means no icon.
        public bool TryGetIcon(string definitionId, out Sprite icon)
        {
            icon = null;
            var id = definitionId?.Trim();
            if (string.IsNullOrEmpty(id) || entries == null) return false;
            foreach (var entry in entries)
            {
                if (entry == null || !string.Equals(entry.DefinitionId, id, StringComparison.Ordinal)) continue;
                icon = entry.Icon;
                return icon != null;
            }
            return false;
        }

        public bool Validate(out string reason)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < (entries?.Count ?? 0); i++)
            {
                var entry = entries[i];
                if (entry == null || entry.DefinitionId.Length == 0) { reason = $"Rune icon entry {i + 1} has an empty definition id."; return false; }
                if (!ids.Add(entry.DefinitionId)) { reason = $"Rune icon definition id '{entry.DefinitionId}' is duplicated."; return false; }
            }
            reason = string.Empty;
            return true;
        }
    }
}
