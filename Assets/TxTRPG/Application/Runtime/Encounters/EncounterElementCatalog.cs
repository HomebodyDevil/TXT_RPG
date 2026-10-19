using System;
using System.Collections.Generic;
using TxTRPG.UI.Encounters;
using UnityEngine;

namespace TxTRPG.Application.Encounters
{
    // The encounter element buttons (for example "rune" / "룬"). List order is the button order and the order of the
    // elements in a result line. Adding an element is adding an entry here; no code change is needed.
    [CreateAssetMenu(menuName = "TxT RPG/Encounters/Encounter Element Catalog", fileName = "EncounterElementCatalog")]
    public sealed class EncounterElementCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private string id = string.Empty;
            [SerializeField] private string displayName = string.Empty;

            public Entry() { }
            public Entry(string id, string displayName) { this.id = id; this.displayName = displayName; }

            public string Id => id?.Trim() ?? string.Empty;
            public string DisplayName => displayName?.Trim() ?? string.Empty;
        }

        [SerializeField] private List<Entry> entries = new();

        public IReadOnlyList<Entry> Entries => entries;

        // Reports empty ids, duplicate ids and empty names. Returns true when there is no problem.
        public bool Validate(out IReadOnlyList<string> problems)
        {
            var found = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry == null || entry.Id.Length == 0) { found.Add($"entry {index}: empty id"); continue; }
                if (!seen.Add(entry.Id)) found.Add($"entry {index}: duplicate id '{entry.Id}'");
                if (entry.DisplayName.Length == 0) found.Add($"entry {index} ('{entry.Id}'): empty display name");
            }
            problems = found;
            return found.Count == 0;
        }

        // Valid entries only, in list order: an invalid entry (empty id or name, or a repeated id) gets no button.
        public IReadOnlyList<EncounterElementToggleEntry> CreateToggleEntries()
        {
            var result = new List<EncounterElementToggleEntry>(entries.Count);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (entry == null || entry.Id.Length == 0 || entry.DisplayName.Length == 0 || !seen.Add(entry.Id)) continue;
                result.Add(new EncounterElementToggleEntry(entry.Id, entry.DisplayName));
            }
            return result;
        }

        public bool TryGetDisplayName(string id, out string displayName)
        {
            var key = id?.Trim() ?? string.Empty;
            foreach (var entry in entries)
                if (entry != null && entry.Id.Length > 0 && entry.DisplayName.Length > 0 && string.Equals(entry.Id, key, StringComparison.Ordinal))
                {
                    displayName = entry.DisplayName;
                    return true;
                }
            displayName = string.Empty;
            return false;
        }

        // Catalog position of a valid id, or -1.
        public int IndexOf(string id)
        {
            var key = id?.Trim() ?? string.Empty;
            for (var index = 0; index < entries.Count; index++)
                if (entries[index] != null && string.Equals(entries[index].Id, key, StringComparison.Ordinal)) return index;
            return -1;
        }

        public void SetEntries(IEnumerable<Entry> values) => entries = values != null ? new List<Entry>(values) : new List<Entry>();
    }
}
