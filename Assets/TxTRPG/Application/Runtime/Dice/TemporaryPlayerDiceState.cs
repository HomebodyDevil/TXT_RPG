using System;
using System.Collections.Generic;
using TxTRPG.Gameplay.Dice;

namespace TxTRPG.Application.Dice
{
    public readonly struct PlayerDieRoll
    {
        public PlayerDieRoll(string id, string displayName, int order, int faceCount, DiceRollResult result)
        { Id = id; DisplayName = displayName; Order = order; FaceCount = faceCount; Result = result; }
        public string Id { get; }
        public string DisplayName { get; }
        public int Order { get; }
        public int FaceCount { get; }
        public DiceRollResult Result { get; }
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
                entries.Add(new Entry(definition.Id, definition.DisplayName,
                    new TxTRPG.Gameplay.Dice.Dice(definition.CreateRuntimeFaces(), definition.MinimumValue, definition.MaximumValue)));
            }
        }

        public int Count => entries.Count;

        public IReadOnlyList<PlayerDieRoll> RollAll(IRandomIndexSource random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            var results = new List<PlayerDieRoll>(entries.Count);
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                results.Add(new PlayerDieRoll(entry.Id, entry.DisplayName, i + 1, entry.Die.FaceCount, entry.Die.Roll(random)));
            }
            return results;
        }

        private sealed class Entry
        {
            public Entry(string id, string displayName, TxTRPG.Gameplay.Dice.Dice die) { Id = id; DisplayName = displayName; Die = die; }
            public string Id { get; }
            public string DisplayName { get; }
            public TxTRPG.Gameplay.Dice.Dice Die { get; }
        }
    }
}
