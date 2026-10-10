using System;
using System.Collections.Generic;

namespace TxTRPG.Gameplay.Runes
{
    // Explicit values for content and future save data. Unknown and unsupported values are rejected, never guessed.
    public enum RuneEffectKind
    {
        Unknown = 0,
        Attack = 1,
        Heal = 2
    }

    public static class RuneEffectKinds
    {
        public static bool IsSupported(RuneEffectKind kind) => kind == RuneEffectKind.Attack || kind == RuneEffectKind.Heal;
    }

    public sealed class RuneDefinition
    {
        public RuneDefinition(string id, string displayName, RuneEffectKind effectKind)
        {
            Id = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("A rune definition requires a non-empty id.", nameof(id)) : id.Trim();
            if (!RuneEffectKinds.IsSupported(effectKind))
                throw new ArgumentException($"Rune '{Id}' has unsupported effect '{effectKind}'.", nameof(effectKind));
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? Id : displayName.Trim();
            EffectKind = effectKind;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public RuneEffectKind EffectKind { get; }
    }

    public sealed class RuneDefinitionCatalog
    {
        private readonly List<RuneDefinition> definitions;
        private readonly Dictionary<string, RuneDefinition> byId = new(StringComparer.Ordinal);

        public RuneDefinitionCatalog(IEnumerable<RuneDefinition> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            definitions = new List<RuneDefinition>();
            foreach (var definition in source)
            {
                if (definition == null) throw new ArgumentException("Rune definitions cannot be null.", nameof(source));
                if (!byId.TryAdd(definition.Id, definition))
                    throw new ArgumentException($"Rune definition id '{definition.Id}' is duplicated.", nameof(source));
                definitions.Add(definition);
            }
        }

        public IReadOnlyList<RuneDefinition> Definitions => definitions.AsReadOnly();
        public bool Contains(string definitionId) => definitionId != null && byId.ContainsKey(definitionId.Trim());

        public bool TryGet(string definitionId, out RuneDefinition definition)
        {
            definition = null;
            return definitionId != null && byId.TryGetValue(definitionId.Trim(), out definition);
        }

        public RuneDefinition Get(string definitionId) =>
            TryGet(definitionId, out var definition) ? definition : throw new KeyNotFoundException($"Rune definition '{definitionId}' does not exist.");
    }

    // One owned rune. Several runes of the same kind are told apart by their instance IDs.
    public readonly struct OwnedRune
    {
        public OwnedRune(string instanceId, string definitionId)
        {
            InstanceId = string.IsNullOrWhiteSpace(instanceId) ? throw new ArgumentException("A rune requires a non-empty instance id.", nameof(instanceId)) : instanceId.Trim();
            DefinitionId = string.IsNullOrWhiteSpace(definitionId) ? throw new ArgumentException("A rune requires a non-empty definition id.", nameof(definitionId)) : definitionId.Trim();
        }

        public string InstanceId { get; }
        public string DefinitionId { get; }
    }

    public sealed class RuneInventory
    {
        private readonly List<OwnedRune> runes = new();
        private readonly Dictionary<string, OwnedRune> byId = new(StringComparer.Ordinal);

        public RuneInventory(RuneDefinitionCatalog catalog)
        {
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public RuneDefinitionCatalog Catalog { get; }
        public int Count => runes.Count;
        public event Action Changed;
        // Set by the loadout that uses this inventory, so a rune placed on a line cannot be removed.
        internal RuneLoadout Loadout { get; set; }

        public IReadOnlyList<OwnedRune> Snapshot() => runes.ToArray();
        public bool Contains(string instanceId) => instanceId != null && byId.ContainsKey(instanceId.Trim());

        public bool TryGet(string instanceId, out OwnedRune rune)
        {
            rune = default;
            return instanceId != null && byId.TryGetValue(instanceId.Trim(), out rune);
        }

        // Entry point for rewards (events, defeated enemies). Choosing a line for the new rune belongs to that feature.
        public bool TryAdd(OwnedRune rune, out string reason)
        {
            if (rune.InstanceId == null) { reason = "A rune is required."; return false; }
            if (byId.ContainsKey(rune.InstanceId)) { reason = $"Rune instance '{rune.InstanceId}' already exists."; return false; }
            if (!Catalog.Contains(rune.DefinitionId)) { reason = $"Rune definition '{rune.DefinitionId}' does not exist."; return false; }
            runes.Add(rune);
            byId.Add(rune.InstanceId, rune);
            reason = string.Empty;
            Changed?.Invoke();
            return true;
        }

        // A rune placed on a line must be taken off the line first (for example before a future combination consumes it).
        public bool TryRemove(string instanceId, out string reason)
        {
            if (!TryGet(instanceId, out var rune)) { reason = $"Rune instance '{instanceId}' is not owned."; return false; }
            if (Loadout != null && Loadout.TryFindLineOf(rune.InstanceId, out var lineId))
            { reason = $"Rune instance '{rune.InstanceId}' is placed on line '{lineId}'. Remove it from the line first."; return false; }
            runes.Remove(rune);
            byId.Remove(rune.InstanceId);
            reason = string.Empty;
            Changed?.Invoke();
            return true;
        }
    }

    // One rune line: a stable line ID and the ordered rune instances stacked on it. Weight is the rune count.
    public sealed class RuneLoadoutLine
    {
        private readonly string[] runeInstanceIds;

        public RuneLoadoutLine(string lineId, IEnumerable<string> instanceIds)
        {
            LineId = string.IsNullOrWhiteSpace(lineId) ? throw new ArgumentException("A rune line requires a non-empty id.", nameof(lineId)) : lineId.Trim();
            if (instanceIds == null) throw new ArgumentNullException(nameof(instanceIds));
            var ids = new List<string>();
            foreach (var id in instanceIds)
            {
                if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException($"Rune line '{LineId}' contains an empty rune id.", nameof(instanceIds));
                ids.Add(id.Trim());
            }
            runeInstanceIds = ids.ToArray();
        }

        public string LineId { get; }
        public IReadOnlyList<string> RuneInstanceIds => Array.AsReadOnly(runeInstanceIds);
        public int Weight => runeInstanceIds.Length;
    }

    // Ordered rune lines with atomic, validated changes. A failed change keeps the previous lines.
    // Rules: a line holds one or more runes of one definition; a rune instance is on at most one line; every rune on a
    // line must be owned. Owned runes that are on no line take no action.
    public sealed class RuneLoadout
    {
        private List<RuneLoadoutLine> lines = new();

        public RuneLoadout(RuneInventory inventory, IEnumerable<RuneLoadoutLine> initialLines = null)
        {
            Inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            if (inventory.Loadout != null) throw new ArgumentException("The inventory already belongs to a rune loadout.", nameof(inventory));
            if (initialLines != null)
            {
                var candidate = new List<RuneLoadoutLine>(initialLines);
                if (!Validate(candidate, out var reason)) throw new ArgumentException(reason, nameof(initialLines));
                lines = candidate;
            }
            inventory.Loadout = this;
        }

        public RuneInventory Inventory { get; }
        public IReadOnlyList<RuneLoadoutLine> Lines => lines.AsReadOnly();
        public int LineCount => lines.Count;
        public event Action Changed;

        public int TotalWeight
        {
            get
            {
                var total = 0;
                foreach (var line in lines) total += line.Weight;
                return total;
            }
        }

        public string DefinitionIdOf(RuneLoadoutLine line)
        {
            if (line == null || line.Weight == 0 || !Inventory.TryGet(line.RuneInstanceIds[0], out var rune)) return string.Empty;
            return rune.DefinitionId;
        }

        public bool TryFindLineOf(string instanceId, out string lineId)
        {
            var id = instanceId?.Trim();
            foreach (var line in lines)
                foreach (var placed in line.RuneInstanceIds)
                    if (string.Equals(placed, id, StringComparison.Ordinal)) { lineId = line.LineId; return true; }
            lineId = string.Empty;
            return false;
        }

        public bool TryAddLine(RuneLoadoutLine line, out string reason) => TryInsertLine(lines.Count, line, out reason);

        public bool TryInsertLine(int index, RuneLoadoutLine line, out string reason)
        {
            if (line == null) { reason = "A rune line is required."; return false; }
            if (index < 0 || index > lines.Count) { reason = $"Line index {index} is out of range."; return false; }
            var candidate = new List<RuneLoadoutLine>(lines);
            candidate.Insert(index, line);
            return TryCommit(candidate, out reason);
        }

        public bool TryRemoveLine(string lineId, out string reason)
        {
            var index = IndexOf(lineId);
            if (index < 0) { reason = $"Rune line '{lineId}' does not exist."; return false; }
            var candidate = new List<RuneLoadoutLine>(lines);
            candidate.RemoveAt(index);
            return TryCommit(candidate, out reason);
        }

        // Stacks a rune on a line, raising its weight.
        public bool TryAddRuneToLine(string lineId, string instanceId, out string reason)
        {
            var index = IndexOf(lineId);
            if (index < 0) { reason = $"Rune line '{lineId}' does not exist."; return false; }
            var ids = new List<string>(lines[index].RuneInstanceIds) { instanceId?.Trim() };
            return TryReplaceLine(index, ids, out reason);
        }

        // Takes a rune off a line. Removing the last rune removes the line, because empty lines are not kept.
        public bool TryRemoveRuneFromLine(string lineId, string instanceId, out string reason)
        {
            var index = IndexOf(lineId);
            if (index < 0) { reason = $"Rune line '{lineId}' does not exist."; return false; }
            var ids = new List<string>(lines[index].RuneInstanceIds);
            if (!ids.Remove(instanceId?.Trim())) { reason = $"Rune '{instanceId}' is not on line '{lineId}'."; return false; }
            if (ids.Count == 0) return TryRemoveLine(lineId, out reason);
            return TryReplaceLine(index, ids, out reason);
        }

        public bool TryReplaceAll(IEnumerable<RuneLoadoutLine> replacement, out string reason)
        {
            if (replacement == null) { reason = "Rune lines are required."; return false; }
            return TryCommit(new List<RuneLoadoutLine>(replacement), out reason);
        }

        public bool Validate(IReadOnlyList<RuneLoadoutLine> candidate, out string reason)
        {
            if (candidate == null) { reason = "Rune lines are required."; return false; }
            var lineIds = new HashSet<string>(StringComparer.Ordinal);
            var placed = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < candidate.Count; i++)
            {
                var line = candidate[i];
                if (line == null) { reason = $"Rune line {i + 1} is null."; return false; }
                if (!lineIds.Add(line.LineId)) { reason = $"Rune line id '{line.LineId}' is duplicated."; return false; }
                if (line.Weight == 0) { reason = $"Rune line '{line.LineId}' is empty."; return false; }
                string definitionId = null;
                foreach (var instanceId in line.RuneInstanceIds)
                {
                    if (!Inventory.TryGet(instanceId, out var rune)) { reason = $"Rune '{instanceId}' on line '{line.LineId}' is not owned."; return false; }
                    if (!placed.Add(rune.InstanceId)) { reason = $"Rune '{instanceId}' is placed on more than one line or twice on one line."; return false; }
                    if (definitionId == null) definitionId = rune.DefinitionId;
                    else if (!string.Equals(definitionId, rune.DefinitionId, StringComparison.Ordinal))
                    { reason = $"Rune line '{line.LineId}' mixes '{definitionId}' and '{rune.DefinitionId}'."; return false; }
                }
            }
            reason = string.Empty;
            return true;
        }

        private bool TryReplaceLine(int index, List<string> ids, out string reason)
        {
            RuneLoadoutLine line;
            try { line = new RuneLoadoutLine(lines[index].LineId, ids); }
            catch (ArgumentException exception) { reason = exception.Message; return false; }
            var candidate = new List<RuneLoadoutLine>(lines) { [index] = line };
            return TryCommit(candidate, out reason);
        }

        private bool TryCommit(List<RuneLoadoutLine> candidate, out string reason)
        {
            if (!Validate(candidate, out reason)) return false;
            lines = candidate;
            Changed?.Invoke();
            return true;
        }

        private int IndexOf(string lineId)
        {
            var id = lineId?.Trim();
            for (var i = 0; i < lines.Count; i++)
                if (string.Equals(lines[i].LineId, id, StringComparison.Ordinal)) return i;
            return -1;
        }
    }

    public readonly struct RuneAllocationLine
    {
        public RuneAllocationLine(int order, string lineId, string definitionId, string displayName,
            RuneEffectKind effectKind, int weight, int amount)
        {
            Order = order;
            LineId = lineId;
            DefinitionId = definitionId;
            DisplayName = displayName;
            EffectKind = effectKind;
            Weight = weight;
            Amount = amount;
        }

        // 1-based position in the loadout at allocation time.
        public int Order { get; }
        public string LineId { get; }
        public string DefinitionId { get; }
        public string DisplayName { get; }
        public RuneEffectKind EffectKind { get; }
        public int Weight { get; }
        public int Amount { get; }
    }

    // Immutable allocation snapshot, in line order. The line amounts always add up to Total.
    public sealed class RuneAllocation
    {
        private readonly RuneAllocationLine[] lines;

        public RuneAllocation(int total, IReadOnlyList<RuneAllocationLine> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            lines = new RuneAllocationLine[source.Count];
            long sum = 0;
            for (var i = 0; i < lines.Length; i++) { lines[i] = source[i]; sum += source[i].Amount; }
            if (sum != total) throw new ArgumentException($"Allocated amounts ({sum}) do not add up to the total ({total}).", nameof(source));
            Total = total;
        }

        public int Total { get; }
        public IReadOnlyList<RuneAllocationLine> Lines => Array.AsReadOnly(lines);
    }

    // Variation point: the weighted policy is the default; a manual allocation mode is a planned second implementation.
    public interface IRuneAllocationPolicy
    {
        RuneAllocation Allocate(int total, RuneLoadout loadout, RuneDefinitionCatalog catalog);
    }

    // Largest remainder: floor(total * weight / W) per line, then the remaining units go one each to the lines with the
    // largest remainder (total * weight mod W), earlier lines first on ties. Deterministic and sum-preserving.
    public sealed class WeightedRuneAllocationPolicy : IRuneAllocationPolicy
    {
        public static readonly WeightedRuneAllocationPolicy Instance = new();

        public RuneAllocation Allocate(int total, RuneLoadout loadout, RuneDefinitionCatalog catalog)
        {
            if (total < 0) throw new ArgumentOutOfRangeException(nameof(total), "The total to allocate cannot be negative.");
            if (loadout == null) throw new ArgumentNullException(nameof(loadout));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var source = loadout.Lines;
            long weightTotal = 0;
            foreach (var line in source) weightTotal += line.Weight;
            if (source.Count == 0 || weightTotal == 0)
                throw new InvalidOperationException("No rune line has weight; nothing can be allocated.");

            var amounts = new long[source.Count];
            var remainders = new long[source.Count];
            long assigned = 0;
            for (var i = 0; i < source.Count; i++)
            {
                var product = (long)total * source[i].Weight;
                amounts[i] = product / weightTotal;
                remainders[i] = product % weightTotal;
                assigned += amounts[i];
            }

            var order = new int[source.Count];
            for (var i = 0; i < order.Length; i++) order[i] = i;
            Array.Sort(order, (left, right) =>
            {
                var byRemainder = remainders[right].CompareTo(remainders[left]);
                return byRemainder != 0 ? byRemainder : left.CompareTo(right);
            });
            var leftover = total - assigned;
            for (var i = 0; leftover > 0; i++, leftover--) amounts[order[i % order.Length]]++;

            var result = new RuneAllocationLine[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                var line = source[i];
                var definition = catalog.Get(loadout.DefinitionIdOf(line));
                result[i] = new RuneAllocationLine(i + 1, line.LineId, definition.Id, definition.DisplayName,
                    definition.EffectKind, line.Weight, (int)amounts[i]);
            }
            return new RuneAllocation(total, result);
        }
    }
}
