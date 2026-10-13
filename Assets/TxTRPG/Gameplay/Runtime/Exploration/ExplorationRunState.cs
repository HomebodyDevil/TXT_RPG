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
        public const string Event = "event";
    }

    public sealed class ExplorationNodeRecord
    {
        internal ExplorationNodeRecord(string id, string parentId, string choiceSetId, int siblingIndex, int depth, string typeId, ExplorationNodeStatus status, string contentId = "")
        {
            Id = id; ParentId = parentId; ChoiceSetId = choiceSetId; SiblingIndex = siblingIndex; Depth = depth; TypeId = typeId; Status = status; ContentId = contentId ?? string.Empty;
        }
        public string Id { get; }
        public string ParentId { get; }
        public string ChoiceSetId { get; }
        public int SiblingIndex { get; }
        public int Depth { get; }
        public string TypeId { get; }
        // What the node contains, decided when the candidate is created (the event ID of an event node). Empty for node
        // types without a content picker.
        public string ContentId { get; }
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

    public readonly struct ExplorationNodeCandidate
    {
        public ExplorationNodeCandidate(string typeId, string contentId)
        {
            TypeId = typeId ?? throw new ArgumentNullException(nameof(typeId));
            ContentId = contentId ?? string.Empty;
        }
        public string TypeId { get; }
        public string ContentId { get; }
    }

    public sealed class ExplorationNodeContentRequest
    {
        public ExplorationNodeContentRequest(string typeId, int depth, IReadOnlyList<string> pickedContentIds)
        {
            TypeId = typeId; Depth = depth; PickedContentIds = pickedContentIds ?? Array.Empty<string>();
        }
        public string TypeId { get; }
        public int Depth { get; }
        // Content IDs already picked for the same choice set, in slot order.
        public IReadOnlyList<string> PickedContentIds { get; }
    }

    // Decides the content of a node type when its candidate is created (for example which event an event node runs).
    public interface IExplorationNodeContentPicker
    {
        bool TryPick(ExplorationNodeContentRequest request, IExplorationRandomSource random, out string contentId);
    }

    public sealed class ExplorationNodeContentWeight
    {
        public ExplorationNodeContentWeight(string contentId, int weight)
        {
            if (string.IsNullOrWhiteSpace(contentId)) throw new ArgumentException("Content ID is required.", nameof(contentId));
            if (weight < 0) throw new ArgumentOutOfRangeException(nameof(weight));
            ContentId = contentId.Trim(); Weight = weight;
        }
        public string ContentId { get; }
        public int Weight { get; }
    }

    // Weighted draw that avoids content already picked for the same choice set; repeats are allowed only when nothing else
    // with weight remains. Fails when no entry has weight.
    public sealed class WeightedExplorationContentPicker : IExplorationNodeContentPicker
    {
        private readonly ExplorationNodeContentWeight[] entries;

        public WeightedExplorationContentPicker(IEnumerable<ExplorationNodeContentWeight> source)
        {
            entries = source?.Where(item => item != null && item.Weight > 0).ToArray() ?? throw new ArgumentNullException(nameof(source));
        }

        public int Count => entries.Length;

        public bool TryPick(ExplorationNodeContentRequest request, IExplorationRandomSource random, out string contentId)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            var picked = new HashSet<string>(request?.PickedContentIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            var fresh = entries.Where(item => !picked.Contains(item.ContentId)).ToArray();
            return TryDraw(fresh.Length > 0 ? fresh : entries, random, out contentId);
        }

        private static bool TryDraw(ExplorationNodeContentWeight[] pool, IExplorationRandomSource random, out string contentId)
        {
            contentId = string.Empty;
            var total = pool.Sum(item => item.Weight);
            if (total <= 0) return false;
            var value = random.Next(total);
            if (value < 0 || value >= total) throw new InvalidOperationException("Exploration random source returned an out-of-range value.");
            var cumulative = 0;
            foreach (var item in pool)
            {
                cumulative += item.Weight;
                if (value < cumulative) { contentId = item.ContentId; return true; }
            }
            return false;
        }
    }

    public sealed class ExplorationNodeGenerator
    {
        private readonly int choiceCount;
        private readonly ExplorationNodeWeight[] weights;
        private readonly IExplorationRandomSource random;
        private readonly Dictionary<string, IExplorationNodeContentPicker> pickers = new(StringComparer.Ordinal);

        public ExplorationNodeGenerator(int choiceCount, IEnumerable<ExplorationNodeWeight> weights, IExplorationRandomSource random,
            IReadOnlyDictionary<string, IExplorationNodeContentPicker> contentPickers = null)
        {
            if (choiceCount < 1) throw new ArgumentOutOfRangeException(nameof(choiceCount));
            this.weights = weights?.ToArray() ?? throw new ArgumentNullException(nameof(weights));
            if (this.weights.Length == 0 || this.weights.Any(item => item.Weight < 0)) throw new ArgumentException("At least one valid node weight is required.", nameof(weights));
            if (this.weights.GroupBy(item => item.TypeId, StringComparer.Ordinal).Any(group => group.Count() > 1)) throw new ArgumentException("Node type IDs must be unique.", nameof(weights));
            if (this.weights.Sum(item => item.Weight) <= 0) throw new ArgumentException("The total node weight must be positive.", nameof(weights));
            this.choiceCount = choiceCount;
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            if (contentPickers != null)
                foreach (var pair in contentPickers) if (pair.Value != null) pickers[pair.Key] = pair.Value;
        }

        // Type IDs only, for callers that do not use node content.
        public IReadOnlyList<string> Generate() => GenerateCandidates(0).Select(item => item.TypeId).ToArray();

        // Draws a type per slot, then its content when the type has a picker. When the picker fails, that type is excluded
        // and the slot is drawn again from the remaining types; when no type with weight remains, this throws.
        public IReadOnlyList<ExplorationNodeCandidate> GenerateCandidates(int depth)
        {
            var result = new ExplorationNodeCandidate[choiceCount];
            var pickedContent = new List<string>();
            for (var i = 0; i < result.Length; i++)
            {
                var excluded = new HashSet<string>(StringComparer.Ordinal);
                while (true)
                {
                    var typeId = DrawType(excluded);
                    if (!pickers.TryGetValue(typeId, out var picker)) { result[i] = new ExplorationNodeCandidate(typeId, string.Empty); break; }
                    if (picker.TryPick(new ExplorationNodeContentRequest(typeId, depth, pickedContent.ToArray()), random, out var contentId) &&
                        !string.IsNullOrWhiteSpace(contentId))
                    {
                        pickedContent.Add(contentId.Trim());
                        result[i] = new ExplorationNodeCandidate(typeId, contentId.Trim());
                        break;
                    }
                    excluded.Add(typeId);
                }
            }
            return result;
        }

        private string DrawType(HashSet<string> excluded)
        {
            var total = 0;
            foreach (var item in weights) if (!excluded.Contains(item.TypeId)) total += item.Weight;
            if (total <= 0) throw new InvalidOperationException("No exploration node type with weight can provide a candidate.");
            var value = random.Next(total);
            if (value < 0 || value >= total) throw new InvalidOperationException("Exploration random source returned an out-of-range value.");
            var cumulative = 0;
            foreach (var item in weights)
            {
                if (excluded.Contains(item.TypeId)) continue;
                cumulative += item.Weight;
                if (value < cumulative) return item.TypeId;
            }
            throw new InvalidOperationException("Exploration node weights are inconsistent.");
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
            var candidates = generator.GenerateCandidates(depth); var choiceSetId = $"choice-{nextChoiceSetNumber++}";
            for (var i = 0; i < candidates.Count; i++) Add(new ExplorationNodeRecord($"node-{nextNodeNumber++}", parentId, choiceSetId, i, depth, candidates[i].TypeId, ExplorationNodeStatus.Available, candidates[i].ContentId));
            CurrentChoiceSetId = choiceSetId;
        }

        private void Add(ExplorationNodeRecord node) { nodes.Add(node); nodesById.Add(node.Id, node); }
    }
}