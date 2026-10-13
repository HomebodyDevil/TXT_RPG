using System;
using System.Collections.Generic;
using TxTRPG.Gameplay.Exploration;
using TxTRPG.Gameplay.Exploration.Events;
using UnityEngine;

namespace TxTRPG.Application.Exploration.Events
{
    [Serializable]
    public sealed class ExplorationEventCatalogEntry
    {
        [SerializeField] private ExplorationEventDefinition definition;
        [SerializeField, Min(0), Tooltip("Relative chance among events when an event node is created. 0 never picks it.")]
        private int weight = 1;

        public ExplorationEventCatalogEntry() { }
        public ExplorationEventCatalogEntry(ExplorationEventDefinition eventDefinition, int pickWeight) { definition = eventDefinition; weight = pickWeight; }

        public ExplorationEventDefinition Definition => definition;
        public int Weight => weight;
    }

    // Events an event node can run, with their pick weights. Read-only at runtime.
    [CreateAssetMenu(menuName = "TxT RPG/Exploration/Event Catalog", fileName = "ExplorationEventCatalog")]
    public sealed class ExplorationEventCatalog : ScriptableObject
    {
        [SerializeField] private List<ExplorationEventCatalogEntry> entries = new();

        public IReadOnlyList<ExplorationEventCatalogEntry> Entries => entries;

        // False for structural errors (empty entry, negative weight, duplicated event ID): the whole catalog is unusable.
        // An event whose own content is invalid is left out of the runtime list and its reason is added to skipped.
        public bool TryBuild(out ExplorationEventCatalogRuntime runtime, out string error, List<string> skipped = null)
        {
            runtime = null;
            if (entries == null || entries.Count == 0) { error = "The event catalog is empty."; return false; }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var events = new List<(ExplorationEvent Event, int Weight)>();
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || entry.Definition == null) { error = $"Catalog entry {i + 1} has no event."; return false; }
                if (entry.Weight < 0) { error = $"Catalog entry {i + 1} has a negative weight."; return false; }
                var id = entry.Definition.EventId;
                if (id.Length == 0) { error = $"Catalog entry {i + 1} ({entry.Definition.name}) has no event ID."; return false; }
                if (!ids.Add(id)) { error = $"Event ID '{id}' is duplicated in the catalog."; return false; }
                if (!entry.Definition.TryCreateEvent(out var created, out var reason))
                {
                    skipped?.Add($"{id} ({entry.Definition.name}): {reason}");
                    continue;
                }
                events.Add((created, entry.Weight));
            }
            runtime = new ExplorationEventCatalogRuntime(events);
            error = string.Empty;
            return true;
        }

#if UNITY_EDITOR
        public void ConfigureForEditor(IEnumerable<ExplorationEventCatalogEntry> catalogEntries) =>
            entries = new List<ExplorationEventCatalogEntry>(catalogEntries ?? Array.Empty<ExplorationEventCatalogEntry>());

        public bool ContainsForEditor(ExplorationEventDefinition definition) => entries.Exists(item => item != null && item.Definition == definition);
        public void AddForEditor(ExplorationEventDefinition definition, int weight) => entries.Add(new ExplorationEventCatalogEntry(definition, weight));
#endif
    }

    // Validated events by ID. Independent of the asset after it is built.
    public sealed class ExplorationEventCatalogRuntime
    {
        private readonly Dictionary<string, ExplorationEvent> byId = new(StringComparer.Ordinal);
        private readonly List<ExplorationNodeContentWeight> weights = new();

        public ExplorationEventCatalogRuntime(IEnumerable<(ExplorationEvent Event, int Weight)> events)
        {
            foreach (var (item, weight) in events)
            {
                byId.Add(item.EventId, item);
                weights.Add(new ExplorationNodeContentWeight(item.EventId, weight));
            }
        }

        public int Count => byId.Count;
        // At least one event can be picked (weight above 0).
        public bool CanPick => weights.Exists(item => item.Weight > 0);
        public bool TryGet(string eventId, out ExplorationEvent result) => byId.TryGetValue(eventId?.Trim() ?? string.Empty, out result);
        public IExplorationNodeContentPicker CreatePicker() => new WeightedExplorationContentPicker(weights);
    }
}
