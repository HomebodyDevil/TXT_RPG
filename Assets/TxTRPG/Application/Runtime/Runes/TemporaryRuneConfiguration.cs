using System;
using System.Collections.Generic;
using TxTRPG.Gameplay.Runes;
using UnityEngine;

namespace TxTRPG.Application.Runes
{
    [Serializable]
    public sealed class TemporaryRuneDefinitionEntry
    {
        [SerializeField] private string id = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private RuneEffectKind effectKind = RuneEffectKind.Attack;

        public TemporaryRuneDefinitionEntry() { }
        public TemporaryRuneDefinitionEntry(string definitionId, string name, RuneEffectKind kind)
        { id = definitionId; displayName = name; effectKind = kind; }

        public string Id => id?.Trim() ?? string.Empty;
        public string DisplayName => displayName ?? string.Empty;
        public RuneEffectKind EffectKind => effectKind;
    }

    [Serializable]
    public sealed class TemporaryOwnedRuneEntry
    {
        [SerializeField] private string instanceId = string.Empty;
        [SerializeField] private string definitionId = string.Empty;

        public TemporaryOwnedRuneEntry() { }
        public TemporaryOwnedRuneEntry(string instance, string definition) { instanceId = instance; definitionId = definition; }

        public string InstanceId => instanceId?.Trim() ?? string.Empty;
        public string DefinitionId => definitionId?.Trim() ?? string.Empty;
    }

    [Serializable]
    public sealed class TemporaryRuneLineEntry
    {
        [SerializeField] private string lineId = string.Empty;
        [SerializeField, Tooltip("Owned rune instance IDs stacked on this line. All must be the same rune kind.")]
        private string[] runeInstanceIds = Array.Empty<string>();

        public TemporaryRuneLineEntry() { }
        public TemporaryRuneLineEntry(string line, params string[] instances) { lineId = line; runeInstanceIds = instances ?? Array.Empty<string>(); }

        public string LineId => lineId?.Trim() ?? string.Empty;
        public IReadOnlyList<string> RuneInstanceIds => runeInstanceIds ?? Array.Empty<string>();
    }

    // Authoring source of the session runes: definitions, starting runes and starting lines. The defaults (one attack
    // and one heal rune on separate lines) are temporary values close to the old alternating dice faces.
    [CreateAssetMenu(menuName = "TxT RPG/Temporary Rune Configuration", fileName = "TemporaryRuneConfiguration")]
    public sealed class TemporaryRuneConfiguration : ScriptableObject
    {
        public const string AttackRuneId = "rune.attack";
        public const string HealRuneId = "rune.heal";

        [SerializeField] private List<TemporaryRuneDefinitionEntry> definitions = new()
        {
            new TemporaryRuneDefinitionEntry(AttackRuneId, "공격 룬", RuneEffectKind.Attack),
            new TemporaryRuneDefinitionEntry(HealRuneId, "회복 룬", RuneEffectKind.Heal)
        };
        [SerializeField] private List<TemporaryOwnedRuneEntry> startingRunes = new()
        {
            new TemporaryOwnedRuneEntry("rune.attack.1", AttackRuneId),
            new TemporaryOwnedRuneEntry("rune.heal.1", HealRuneId)
        };
        [SerializeField] private List<TemporaryRuneLineEntry> startingLines = new()
        {
            new TemporaryRuneLineEntry("line.1", "rune.attack.1"),
            new TemporaryRuneLineEntry("line.2", "rune.heal.1")
        };

        public IReadOnlyList<TemporaryRuneDefinitionEntry> Definitions => definitions;
        public IReadOnlyList<TemporaryOwnedRuneEntry> StartingRunes => startingRunes;
        public IReadOnlyList<TemporaryRuneLineEntry> StartingLines => startingLines;

#if UNITY_EDITOR
        public void ConfigureForEditor(IEnumerable<TemporaryRuneDefinitionEntry> runeDefinitions,
            IEnumerable<TemporaryOwnedRuneEntry> runes, IEnumerable<TemporaryRuneLineEntry> lines)
        {
            definitions = new List<TemporaryRuneDefinitionEntry>(runeDefinitions ?? Array.Empty<TemporaryRuneDefinitionEntry>());
            startingRunes = new List<TemporaryOwnedRuneEntry>(runes ?? Array.Empty<TemporaryOwnedRuneEntry>());
            startingLines = new List<TemporaryRuneLineEntry>(lines ?? Array.Empty<TemporaryRuneLineEntry>());
        }
#endif
    }

    // Session-lifetime runes (not saved). Inventory and Loadout expose the validated change APIs (TryAdd, TryRemove,
    // TryAddLine, TryAddRuneToLine, ...); Changed is raised for either.
    public sealed class TemporaryRuneState
    {
        private TemporaryRuneState(RuneDefinitionCatalog catalog, RuneInventory inventory, RuneLoadout loadout)
        {
            Catalog = catalog;
            Inventory = inventory;
            Loadout = loadout;
            Inventory.Changed += () => Changed?.Invoke();
            Loadout.Changed += () => Changed?.Invoke();
        }

        public RuneDefinitionCatalog Catalog { get; }
        public RuneInventory Inventory { get; }
        public RuneLoadout Loadout { get; }
        public event Action Changed;
        // Only lines that receive allocation count: a loadout of test rune lines alone cannot take an action.
        public bool CanAllocate => Loadout.LineCount > 0 && Loadout.AllocatableWeight(Catalog) > 0;
        // Definition IDs merged from the test rune configuration, in configuration order (empty without one).
        public IReadOnlyList<string> TestDefinitionIds { get; private set; } = Array.Empty<string>();

        public RuneAllocation Allocate(int total, IRuneAllocationPolicy policy = null) =>
            (policy ?? WeightedRuneAllocationPolicy.Instance).Allocate(total, Loadout, Catalog);

        public static bool TryCreate(TemporaryRuneConfiguration configuration, out TemporaryRuneState state, out string reason) =>
            TryCreate(configuration, null, out state, out reason, out _);

        // Merges the test rune definitions into the catalog. A test configuration that fails validation or whose IDs
        // collide with the regular definitions is left out (testReason says why) and never fails the regular runes.
        public static bool TryCreate(TemporaryRuneConfiguration configuration, TemporaryTestRuneConfiguration testConfiguration,
            out TemporaryRuneState state, out string reason, out string testReason)
        {
            state = null;
            testReason = string.Empty;
            if (configuration == null) { reason = "No rune configuration is assigned."; return false; }
            try
            {
                var definitions = new List<RuneDefinition>();
                foreach (var entry in configuration.Definitions)
                {
                    if (entry == null) throw new ArgumentException("A rune definition entry is empty.");
                    definitions.Add(new RuneDefinition(entry.Id, entry.DisplayName, entry.EffectKind));
                }
                var testIds = MergeTestDefinitions(definitions, testConfiguration, out testReason);
                var catalog = new RuneDefinitionCatalog(definitions);
                var inventory = new RuneInventory(catalog);
                foreach (var entry in configuration.StartingRunes)
                {
                    if (entry == null) throw new ArgumentException("A starting rune entry is empty.");
                    if (!inventory.TryAdd(new OwnedRune(entry.InstanceId, entry.DefinitionId), out reason))
                        throw new ArgumentException(reason);
                }
                var lines = new List<RuneLoadoutLine>();
                foreach (var entry in configuration.StartingLines)
                {
                    if (entry == null) throw new ArgumentException("A starting rune line entry is empty.");
                    lines.Add(new RuneLoadoutLine(entry.LineId, entry.RuneInstanceIds));
                }
                var loadout = new RuneLoadout(inventory, lines);
                state = new TemporaryRuneState(catalog, inventory, loadout) { TestDefinitionIds = testIds };
                reason = string.Empty;
                return true;
            }
            catch (ArgumentException exception)
            {
                reason = exception.Message;
                return false;
            }
        }

        private static IReadOnlyList<string> MergeTestDefinitions(List<RuneDefinition> definitions,
            TemporaryTestRuneConfiguration testConfiguration, out string testReason)
        {
            testReason = string.Empty;
            if (testConfiguration == null || testConfiguration.Definitions.Count == 0) return Array.Empty<string>();
            if (!testConfiguration.Validate(out testReason)) return Array.Empty<string>();
            var regular = new HashSet<string>(StringComparer.Ordinal);
            foreach (var definition in definitions) regular.Add(definition.Id);
            var tests = new List<RuneDefinition>();
            foreach (var entry in testConfiguration.Definitions)
            {
                if (regular.Contains(entry.Id)) { testReason = $"Test rune id '{entry.Id}' is also a regular rune definition."; return Array.Empty<string>(); }
                try { tests.Add(new RuneDefinition(entry.Id, entry.DisplayName, entry.EffectKind)); }
                catch (ArgumentException exception) { testReason = exception.Message; return Array.Empty<string>(); }
            }
            var ids = new string[tests.Count];
            for (var i = 0; i < tests.Count; i++) { definitions.Add(tests[i]); ids[i] = tests[i].Id; }
            return Array.AsReadOnly(ids);
        }
    }
}
