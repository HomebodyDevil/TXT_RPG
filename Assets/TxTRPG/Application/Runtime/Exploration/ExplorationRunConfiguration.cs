using System;
using System.Collections.Generic;
using TxTRPG.Application.Exploration.Events;
using TxTRPG.Gameplay.Exploration;
using UnityEngine;

namespace TxTRPG.Application.Exploration
{
    [Serializable]
    public sealed class ExplorationNodeWeightEntry
    {
        [SerializeField] private string typeId = string.Empty;
        [SerializeField, Min(0)] private int weight;

        public ExplorationNodeWeightEntry() { }
        public ExplorationNodeWeightEntry(string nodeTypeId, int nodeWeight) { typeId = nodeTypeId; weight = nodeWeight; }

        public string TypeId => typeId?.Trim() ?? string.Empty;
        public int Weight => weight;
    }

    [CreateAssetMenu(menuName = "TxT RPG/Exploration Run Configuration", fileName = "ExplorationRunConfiguration")]
    public sealed class ExplorationRunConfiguration : ScriptableObject, ISerializationCallbackReceiver
    {
        public const int DefaultCombatWeight = 40;
        public const int DefaultRecoveryUpgradeWeight = 30;
        public const int DefaultEventWeight = 30;

        [SerializeField, Min(1)] private int choiceCount = 3;
        [SerializeField, Tooltip("Relative chance of each node type per candidate. A type missing here never appears.")]
        private List<ExplorationNodeWeightEntry> nodeWeights = CreateDefaultWeights();
        [SerializeField, Tooltip("Events an event node can run. Without a valid catalog, event nodes do not appear.")]
        private ExplorationEventCatalog eventCatalog;
        // Fields of the fixed two-type format. A value of 0 or more is moved into nodeWeights once when the asset is read;
        // -1 means already migrated. Kept so old YAML can still be read.
        [SerializeField, HideInInspector] private int combatWeight = -1;
        [SerializeField, HideInInspector] private int recoveryUpgradeWeight = -1;

        public int ChoiceCount => choiceCount;
        public IReadOnlyList<ExplorationNodeWeightEntry> NodeWeights => nodeWeights;
        public ExplorationEventCatalog EventCatalog => eventCatalog;
        public int CombatWeight => WeightOf(ExplorationNodeTypeIds.Combat);
        public int RecoveryUpgradeWeight => WeightOf(ExplorationNodeTypeIds.RecoveryUpgrade);
        public int EventWeight => WeightOf(ExplorationNodeTypeIds.Event);

        public static List<ExplorationNodeWeightEntry> CreateDefaultWeights() => new()
        {
            new ExplorationNodeWeightEntry(ExplorationNodeTypeIds.Combat, DefaultCombatWeight),
            new ExplorationNodeWeightEntry(ExplorationNodeTypeIds.RecoveryUpgrade, DefaultRecoveryUpgradeWeight),
            new ExplorationNodeWeightEntry(ExplorationNodeTypeIds.Event, DefaultEventWeight)
        };

        public int WeightOf(string typeId)
        {
            if (nodeWeights == null) return 0;
            foreach (var entry in nodeWeights)
                if (entry != null && string.Equals(entry.TypeId, typeId, StringComparison.Ordinal)) return entry.Weight;
            return 0;
        }

        // eventsAvailable false (no usable catalog or no event controller) treats the event weight as 0.
        public ExplorationNodeWeight[] CreateWeights(bool eventsAvailable = true)
        {
            if (!Validate(out var reason)) throw new InvalidOperationException($"Exploration Run Configuration is invalid. {reason}");
            var result = new List<ExplorationNodeWeight>(nodeWeights.Count);
            foreach (var entry in nodeWeights)
            {
                var weight = !eventsAvailable && entry.TypeId == ExplorationNodeTypeIds.Event ? 0 : entry.Weight;
                result.Add(new ExplorationNodeWeight(entry.TypeId, weight));
            }
            return result.ToArray();
        }

        public bool Validate(out string reason)
        {
            if (choiceCount < 1) { reason = "Choice Count must be positive."; return false; }
            if (nodeWeights == null || nodeWeights.Count == 0) { reason = "No node weight is configured."; return false; }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var total = 0L;
            for (var i = 0; i < nodeWeights.Count; i++)
            {
                var entry = nodeWeights[i];
                if (entry == null || entry.TypeId.Length == 0) { reason = $"Node weight {i + 1} has no type ID."; return false; }
                if (entry.Weight < 0) { reason = $"Node weight '{entry.TypeId}' is negative."; return false; }
                if (!ids.Add(entry.TypeId)) { reason = $"Node type '{entry.TypeId}' is listed twice."; return false; }
                total += entry.Weight;
            }
            if (total <= 0) { reason = "The total node weight must be positive."; return false; }
            reason = string.Empty;
            return true;
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        void ISerializationCallbackReceiver.OnAfterDeserialize() => MigrateLegacyWeights();

        // Moves the old fixed fields into the list unchanged; the new 40/30/30 values are written by the apply tool.
        private bool MigrateLegacyWeights()
        {
            if (combatWeight < 0 && recoveryUpgradeWeight < 0) return false;
            nodeWeights = new List<ExplorationNodeWeightEntry>
            {
                new(ExplorationNodeTypeIds.Combat, Math.Max(0, combatWeight)),
                new(ExplorationNodeTypeIds.RecoveryUpgrade, Math.Max(0, recoveryUpgradeWeight))
            };
            combatWeight = recoveryUpgradeWeight = -1;
            return true;
        }

#if UNITY_EDITOR
        public void ConfigureForEditor(int choices, int combat, int recoveryUpgrade) =>
            ConfigureForEditor(choices, new[]
            {
                new ExplorationNodeWeightEntry(ExplorationNodeTypeIds.Combat, combat),
                new ExplorationNodeWeightEntry(ExplorationNodeTypeIds.RecoveryUpgrade, recoveryUpgrade),
                new ExplorationNodeWeightEntry(ExplorationNodeTypeIds.Event, 0)
            });

        public void ConfigureForEditor(int choices, IEnumerable<ExplorationNodeWeightEntry> weights)
        {
            choiceCount = choices;
            nodeWeights = new List<ExplorationNodeWeightEntry>(weights ?? Array.Empty<ExplorationNodeWeightEntry>());
            combatWeight = recoveryUpgradeWeight = -1;
        }

        public void ConfigureEventCatalogForEditor(ExplorationEventCatalog catalog) => eventCatalog = catalog;

        // Tests only: the legacy fields as an old asset would have them.
        public void SetLegacyWeightsForEditor(int combat, int recoveryUpgrade) { combatWeight = combat; recoveryUpgradeWeight = recoveryUpgrade; }
        public bool MigrateLegacyWeightsForEditor() => MigrateLegacyWeights();
#endif
    }
}
