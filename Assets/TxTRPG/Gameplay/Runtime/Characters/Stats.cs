using System;
using System.Collections.Generic;
using UnityEngine;

namespace TxTRPG.Gameplay.Characters
{
    [Serializable]
    public readonly struct StatId : IEquatable<StatId>
    {
        [SerializeField] private readonly string value;

        public StatId(string value)
        {
            this.value = value?.Trim() ?? string.Empty;
        }

        public string Value => value ?? string.Empty;
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public bool Equals(StatId other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is StatId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value;
        public static bool operator ==(StatId left, StatId right) => left.Equals(right);
        public static bool operator !=(StatId left, StatId right) => !left.Equals(right);
    }

    public static class CoreStatIds
    {
        public static readonly StatId AttackPower = new("core.attack_power");
        public static readonly StatId MaxHealth = new("core.max_health");
    }

    [Serializable]
    public struct BaseStatEntry
    {
        [SerializeField] private string statId;
        [SerializeField] private int value;

        public BaseStatEntry(StatId statId, int value)
        {
            this.statId = statId.Value;
            this.value = value;
        }

        public StatId StatId => new(statId);
        public int Value => value;
    }

    [Serializable]
    public struct SavedStatValue
    {
        public string statId;
        public int value;

        public SavedStatValue(StatId statId, int value)
        {
            this.statId = statId.Value;
            this.value = value;
        }

        public StatId StatId => new(statId);
    }

    public sealed class StatBlock
    {
        private readonly Dictionary<StatId, int> values = new();
        private readonly List<SavedStatValue> permanentBonuses = new();
        private readonly List<StatId> ignoredBonusStatIds = new();

        public StatBlock(
            IEnumerable<BaseStatEntry> baseStats,
            IEnumerable<SavedStatValue> savedPermanentBonuses = null)
        {
            if (baseStats == null)
            {
                throw new ArgumentNullException(nameof(baseStats));
            }

            foreach (var entry in baseStats)
            {
                var id = entry.StatId;
                if (!id.IsValid)
                {
                    throw new ArgumentException("Stat IDs cannot be empty.", nameof(baseStats));
                }
                if (entry.Value < GetMinimum(id))
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(baseStats),
                        $"Stat '{id}' must be at least {GetMinimum(id)}.");
                }
                if (!values.TryAdd(id, entry.Value))
                {
                    throw new ArgumentException(
                        $"Stat '{id}' is defined more than once.",
                        nameof(baseStats));
                }
            }

            RequireCoreStat(CoreStatIds.AttackPower);
            RequireCoreStat(CoreStatIds.MaxHealth);
            ApplyPermanentBonuses(savedPermanentBonuses);
        }

        public int AttackPower => Get(CoreStatIds.AttackPower);
        public int MaxHealth => Get(CoreStatIds.MaxHealth);
        public IReadOnlyList<SavedStatValue> PermanentBonuses => permanentBonuses;
        public IReadOnlyList<StatId> IgnoredBonusStatIds => ignoredBonusStatIds;

        public int Get(StatId id)
        {
            if (!id.IsValid)
            {
                throw new ArgumentException("A valid stat ID is required.", nameof(id));
            }
            if (!values.TryGetValue(id, out var value))
            {
                throw new KeyNotFoundException($"Stat '{id}' is not defined.");
            }
            return value;
        }

        public bool TryGet(StatId id, out int value) => values.TryGetValue(id, out value);

        private void ApplyPermanentBonuses(IEnumerable<SavedStatValue> bonuses)
        {
            if (bonuses == null)
            {
                return;
            }

            var combined = new Dictionary<StatId, int>();
            foreach (var bonus in bonuses)
            {
                var id = bonus.StatId;
                if (!id.IsValid || !values.ContainsKey(id))
                {
                    ignoredBonusStatIds.Add(id);
                    continue;
                }
                combined.TryGetValue(id, out var currentBonus);
                combined[id] = checked(currentBonus + bonus.value);
            }

            foreach (var pair in combined)
            {
                var finalValue = checked(values[pair.Key] + pair.Value);
                values[pair.Key] = Math.Max(GetMinimum(pair.Key), finalValue);
                permanentBonuses.Add(new SavedStatValue(pair.Key, pair.Value));
            }
        }

        private void RequireCoreStat(StatId id)
        {
            if (!values.ContainsKey(id))
            {
                throw new ArgumentException($"Required stat '{id}' is missing.");
            }
        }

        private static int GetMinimum(StatId id) => id == CoreStatIds.MaxHealth ? 1 : 0;
    }
}
