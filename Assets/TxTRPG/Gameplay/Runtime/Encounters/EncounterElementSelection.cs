using System;
using System.Collections.Generic;

namespace TxTRPG.Gameplay.Encounters
{
    // Selected encounter element ids (for example "rune"). Ids are trimmed and case-sensitive; empty ids are rejected.
    // Snapshot keeps the selection order. Changed fires only when the selection actually changes.
    public sealed class EncounterElementSelection
    {
        private readonly List<string> selected = new();

        public event Action Changed;

        public int Count => selected.Count;

        public bool IsSelected(string id)
        {
            var key = Normalize(id);
            return key.Length > 0 && selected.Contains(key);
        }

        public void SetSelected(string id, bool value)
        {
            var key = Require(id);
            var index = selected.IndexOf(key);
            if (value == index >= 0) return;
            if (value) selected.Add(key);
            else selected.RemoveAt(index);
            Changed?.Invoke();
        }

        public bool Toggle(string id)
        {
            var value = !IsSelected(Require(id));
            SetSelected(id, value);
            return value;
        }

        public void Clear()
        {
            if (selected.Count == 0) return;
            selected.Clear();
            Changed?.Invoke();
        }

        public IReadOnlyList<string> Snapshot() => selected.ToArray();

        private static string Normalize(string id) => id?.Trim() ?? string.Empty;

        private static string Require(string id)
        {
            var key = Normalize(id);
            if (key.Length == 0) throw new ArgumentException("Encounter element id must not be empty.", nameof(id));
            return key;
        }
    }
}
