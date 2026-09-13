using System;
using System.Collections.Generic;
using System.Linq;
using TxTRPG.Gameplay.Characters;

namespace TxTRPG.Gameplay.Exploration
{
    public enum ExplorationNodeStatus { Available = 1, Active = 2, Unchosen = 3, Completed = 4, Failed = 5 }
    public enum ExplorationPhase { AwaitingChoice = 1, ResolvingNode = 2, Failed = 3 }

    public static class ExplorationNodeTypeIds
    {
        public const string Root = "root";
        public const string Combat = "combat";
        public const string RecoveryUpgrade = "recovery-upgrade";
    }

    public sealed class ExplorationNodeRecord
    {
        internal ExplorationNodeRecord(string id, string parentId, string choiceSetId, int siblingIndex, int depth, string typeId, ExplorationNodeStatus status)
        {
            Id = id; ParentId = parentId; ChoiceSetId = choiceSetId; SiblingIndex = siblingIndex; Depth = depth; TypeId = typeId; Status = status;
        }
        public string Id { get; }
        public string ParentId { get; }
        public string ChoiceSetId { get; }
        public int SiblingIndex { get; }
        public int Depth { get; }
        public string TypeId { get; }
        public ExplorationNodeStatus Status { get; internal set; }
        public string CompletionReason { get; internal set; } = string.Empty;
    }

    public readonly struct ExplorationNodeWeight
    {
        public ExplorationNodeWeight(string typeId, int weight)
        {
            if (string.IsNullOrWhiteSpace(typeId)) throw new ArgumentException("Node type ID is required.", nameof(typeId));
            if (weight < 0) throw new ArgumentOutOfRangeException(nameof(weight));
            TypeId = typeId.Trim(); Weight = weight;
        }
        public string TypeId { get; }
        public int Weight { get; }
    }

    public interface IExplorationRandomSource { int Next(int exclusiveMaximum); }

    public sealed class ExplorationNodeGenerator
    {
        private readonly int choiceCount;
        private readonly ExplorationNodeWeight[] weights;
        private readonly int totalWeight;
        private readonly IExplorationRandomSource random;

        public ExplorationNodeGenerator(int choiceCount, IEnumerable<ExplorationNodeWeight> weights, IExplorationRandomSource random)
        {
            if (choiceCount < 1) throw new ArgumentOutOfRangeException(nameof(choiceCount));
            this.weights = weights?.ToArray() ?? throw new ArgumentNullException(nameof(weights));
            if (this.weights.Length == 0 || this.weights.Any(item => item.Weight < 0)) throw new ArgumentException("At least one valid node weight is required.", nameof(weights));
            if (this.weights.GroupBy(item => item.TypeId, StringComparer.Ordinal).Any(group => group.Count() > 1)) throw new ArgumentException("Node type IDs must be unique.", nameof(weights));
            totalWeight = this.weights.Sum(item => item.Weight);
            if (totalWeight <= 0) throw new ArgumentException("The total node weight must be positive.", nameof(weights));
            this.choiceCount = choiceCount;
            this.random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public IReadOnlyList<string> Generate()
        {
            var result = new string[choiceCount];
            for (var i = 0; i < result.Length; i++)
            {
                var value = random.Next(totalWeight);
                if (value < 0 || value >= totalWeight) throw new InvalidOperationException("Exploration random source returned an out-of-range value.");
                var cumulative = 0;
                for (var j = 0; j < weights.Length; j++)
                {
                    cumulative += weights[j].Weight;
                    if (value < cumulative) { result[i] = weights[j].TypeId; break; }
                }
            }
            return result;
        }
    }

    public sealed class ExplorationRunState
    {
        public const string RootNodeId = "root";
        private readonly ExplorationNodeGenerator generator;
        private readonly List<ExplorationNodeRecord> nodes = new();
        private readonly Dictionary<string, ExplorationNodeRecord> nodesById = new(StringComparer.Ordinal);
        private readonly List<string> selectedPath = new();
        private int nextNodeNumber = 1;
        private int nextChoiceSetNumber = 1;

        public ExplorationRunState(string runId, HealthState playerHealth, ExplorationNodeGenerator generator)
        {
            if (string.IsNullOrWhiteSpace(runId)) throw new ArgumentException("Run ID is required.", nameof(runId));
            RunId = runId.Trim(); PlayerHealth = playerHealth ?? throw new ArgumentNullException(nameof(playerHealth)); this.generator = generator ?? throw new ArgumentNullException(nameof(generator));
            var root = new ExplorationNodeRecord(RootNodeId, string.Empty, string.Empty, 0, -1, ExplorationNodeTypeIds.Root, ExplorationNodeStatus.Completed) { CompletionReason = "VirtualRoot" };
            Add(root); LastCompletedNodeId = root.Id; GenerateChoices(root.Id, 0);
        }

        public string RunId { get; }
        public HealthState PlayerHealth { get; }
        public ExplorationPhase Phase { get; private set; } = ExplorationPhase.AwaitingChoice;
        public string ActiveNodeId { get; private set; } = string.Empty;
        public string LastCompletedNodeId { get; private set; }
        public string CurrentChoiceSetId { get; private set; }
        public IReadOnlyList<ExplorationNodeRecord> Nodes => nodes.AsReadOnly();
        public IReadOnlyList<string> SelectedPath => selectedPath.AsReadOnly();
        public IReadOnlyList<ExplorationNodeRecord> CurrentChoices => nodes.Where(item => item.ChoiceSetId == CurrentChoiceSetId && item.Status == ExplorationNodeStatus.Available).OrderBy(item => item.SiblingIndex).ToArray();

        public ExplorationNodeRecord Select(string choiceSetId, string nodeId)
        {
            if (Phase != ExplorationPhase.AwaitingChoice) throw new InvalidOperationException("The run is not awaiting a choice.");
            if (!string.Equals(choiceSetId, CurrentChoiceSetId, StringComparison.Ordinal)) throw new InvalidOperationException("The choice request is stale.");
            if (!nodesById.TryGetValue(nodeId, out var selected) || selected.Status != ExplorationNodeStatus.Available || selected.ChoiceSetId != CurrentChoiceSetId) throw new InvalidOperationException("The selected node is not available.");
            foreach (var candidate in nodes.Where(item => item.ChoiceSetId == CurrentChoiceSetId)) candidate.Status = candidate == selected ? ExplorationNodeStatus.Active : ExplorationNodeStatus.Unchosen;
            ActiveNodeId = selected.Id; selectedPath.Add(selected.Id); CurrentChoiceSetId = string.Empty; Phase = ExplorationPhase.ResolvingNode; return selected;
        }

        public void CompleteActive(string nodeId, string reason)
        {
            if (Phase != ExplorationPhase.ResolvingNode || !string.Equals(nodeId, ActiveNodeId, StringComparison.Ordinal)) throw new InvalidOperationException("The active node does not match the completion request.");
            var node = nodesById[nodeId]; node.Status = ExplorationNodeStatus.Completed; node.CompletionReason = reason ?? string.Empty;
            LastCompletedNodeId = node.Id; ActiveNodeId = string.Empty; GenerateChoices(node.Id, node.Depth + 1); Phase = ExplorationPhase.AwaitingChoice;
        }

        public void FailActive(string nodeId, string reason)
        {
            if (Phase != ExplorationPhase.ResolvingNode || !string.Equals(nodeId, ActiveNodeId, StringComparison.Ordinal)) throw new InvalidOperationException("The active node does not match the failure request.");
            var node = nodesById[nodeId]; node.Status = ExplorationNodeStatus.Failed; node.CompletionReason = reason ?? string.Empty; Phase = ExplorationPhase.Failed;
        }

        private void GenerateChoices(string parentId, int depth)
        {
            var types = generator.Generate(); var choiceSetId = $"choice-{nextChoiceSetNumber++}";
            for (var i = 0; i < types.Count; i++) Add(new ExplorationNodeRecord($"node-{nextNodeNumber++}", parentId, choiceSetId, i, depth, types[i], ExplorationNodeStatus.Available));
            CurrentChoiceSetId = choiceSetId;
        }

        private void Add(ExplorationNodeRecord node) { nodes.Add(node); nodesById.Add(node.Id, node); }
    }
}