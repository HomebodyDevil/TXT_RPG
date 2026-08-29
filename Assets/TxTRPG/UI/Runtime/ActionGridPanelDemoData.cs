using System;
using System.Collections.Generic;
using UnityEngine;

namespace TxTRPG.UI
{
    [CreateAssetMenu(menuName = "TxT RPG/UI/Action Grid Demo Data")]
    public sealed class ActionGridPanelDemoData : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private string id;
            [SerializeField] private ActionGridEntryKind kind;
            [SerializeField] private Sprite icon;
            [SerializeField] private string displayName;
            [SerializeField, TextArea] private string description;
            [SerializeField, Min(0)] private int quantity;
            [SerializeField] private bool isEnabled = true;
            [SerializeField, Range(0f, 1f)] private float cooldownNormalized;
            [SerializeField] private string shortcutLabel;

            public ActionGridEntry ToEntry()
            {
                return new ActionGridEntry(
                    id,
                    kind,
                    icon,
                    displayName,
                    description,
                    quantity,
                    isEnabled,
                    cooldownNormalized,
                    shortcutLabel);
            }
        }

        [SerializeField] private List<Entry> entries = new();

        public IReadOnlyList<Entry> Entries => entries;

        public List<ActionGridEntry> CreateEntries()
        {
            var result = new List<ActionGridEntry>(entries.Count);
            foreach (var entry in entries)
            {
                if (entry != null)
                {
                    result.Add(entry.ToEntry());
                }
            }

            return result;
        }
    }
}
