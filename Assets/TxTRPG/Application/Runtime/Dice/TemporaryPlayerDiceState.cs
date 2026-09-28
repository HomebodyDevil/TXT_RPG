using System;
using System.Collections.Generic;
using TxTRPG.Gameplay.Dice;

namespace TxTRPG.Application.Dice
{
    public readonly struct PlayerDieRoll
    {
        public PlayerDieRoll(string id, string displayName, int order, int faceCount, DiceRollResult result)
            : this(id, id, displayName, order, faceCount, result) { }
        public PlayerDieRoll(string id, string definitionId, string displayName, int order, int faceCount, DiceRollResult result)
        { Id = id; DefinitionId = definitionId; DisplayName = displayName; Order = order; FaceCount = faceCount; Result = result; }
        public string Id { get; }
        public string DefinitionId { get; }
        public string DisplayName { get; }
        public int Order { get; }
        public int FaceCount { get; }
        public DiceRollResult Result { get; }
    }

    public sealed class OwnedDieSnapshot
    {
        private readonly DiceFace[] faces;
        public OwnedDieSnapshot(string instanceId, string definitionId, string displayName, IReadOnlyList<DiceFace> source)
        {
            InstanceId = instanceId;
            DefinitionId = definitionId;
            DisplayName = displayName;
            faces = new DiceFace[source.Count];
            for (var i = 0; i < faces.Length; i++) faces[i] = source[i];
        }
        public string InstanceId { get; }
        public string DefinitionId { get; }
        public string DisplayName { get; }
        public IReadOnlyList<DiceFace> Faces => Array.AsReadOnly(faces);
        public int FaceCount => faces.Length;
    }

    public sealed class TemporaryPlayerDiceState
    {
        private readonly List<Entry> entries = new();

        public TemporaryPlayerDiceState(TemporaryDiceConfiguration configuration)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            if (!configuration.EnabledForSession) return;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < configuration.Dice.Count; i++)
            {
                var definition = configuration.Dice[i] ?? throw new InvalidOperationException($"Temporary die {i + 1} is null.");
                if (string.IsNullOrWhiteSpace(definition.Id) || !ids.Add(definition.Id))
                    throw new InvalidOperationException($"Temporary die {i + 1} requires a unique non-empty id.");
                entries.Add(new Entry(definition.Id, definition.Id, definition.DisplayName,
                    new TxTRPG.Gameplay.Dice.Dice(definition.CreateRuntimeFaces(), definition.MinimumValue, definition.MaximumValue)));
            }
        }

        public int Count => entries.Count;
        public event Action Changed;

        public IReadOnlyList<OwnedDieSnapshot> Snapshot()
        {
            var snapshot = new OwnedDieSnapshot[entries.Count];
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                snapshot[i] = new OwnedDieSnapshot(entry.Id, entry.DefinitionId, entry.DisplayName, entry.Die.Faces);
            }
            return Array.AsReadOnly(snapshot);
        }

        public bool TryAdd(string instanceId, string definitionId, string displayName,
            IEnumerable<DiceFace> faces, int minimumValue, int maximumValue, out string reason)
        {
            instanceId = instanceId?.Trim();
            definitionId = definitionId?.Trim();
            if (string.IsNullOrEmpty(instanceId) || string.IsNullOrEmpty(definitionId))
            { reason = "Instance and definition IDs are required."; return false; }
            if (entries.Exists(entry => string.Equals(entry.Id, instanceId, StringComparison.Ordinal)))
            { reason = $"Die instance '{instanceId}' already exists."; return false; }
            TxTRPG.Gameplay.Dice.Dice die;
            try { die = new TxTRPG.Gameplay.Dice.Dice(faces, minimumValue, maximumValue); }
            catch (ArgumentException exception) { reason = exception.Message; return false; }
            entries.Add(new Entry(instanceId, definitionId,
                string.IsNullOrWhiteSpace(displayName) ? definitionId : displayName.Trim(), die));
            reason = string.Empty;
            Changed?.Invoke();
            return true;
        }

        public bool TryRemove(string instanceId)
        {
            var index = entries.FindIndex(entry => string.Equals(entry.Id, instanceId?.Trim(), StringComparison.Ordinal));
            if (index < 0) return false;
            entries.RemoveAt(index);
            Changed?.Invoke();
            return true;
        }

        public IReadOnlyList<PlayerDieRoll> RollAll(IRandomIndexSource random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            var results = new List<PlayerDieRoll>(entries.Count);
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                results.Add(new PlayerDieRoll(entry.Id, entry.DefinitionId, entry.DisplayName,
                    i + 1, entry.Die.FaceCount, entry.Die.Roll(random)));
            }
            return results;
        }

        private sealed class Entry
        {
            public Entry(string id, string definitionId, string displayName, TxTRPG.Gameplay.Dice.Dice die)
            { Id = id; DefinitionId = definitionId; DisplayName = displayName; Die = die; }
            public string Id { get; }
            public string DefinitionId { get; }
            public string DisplayName { get; }
            public TxTRPG.Gameplay.Dice.Dice Die { get; }
        }
    }
}
