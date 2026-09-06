using System;

namespace TxTRPG.Gameplay.Characters
{
    public enum HealthChangeKind
    {
        Damage,
        Healing,
        Restore,
        MaximumChanged
    }

    public readonly struct HealthChangeResult
    {
        public HealthChangeResult(
            HealthChangeKind kind,
            int previousHealth,
            int currentHealth,
            int previousMaximum,
            int currentMaximum,
            int appliedAmount,
            bool becameDefeated)
        {
            Kind = kind;
            PreviousHealth = previousHealth;
            CurrentHealth = currentHealth;
            PreviousMaximum = previousMaximum;
            CurrentMaximum = currentMaximum;
            AppliedAmount = appliedAmount;
            BecameDefeated = becameDefeated;
        }

        public HealthChangeKind Kind { get; }
        public int PreviousHealth { get; }
        public int CurrentHealth { get; }
        public int PreviousMaximum { get; }
        public int CurrentMaximum { get; }
        public int AppliedAmount { get; }
        public bool BecameDefeated { get; }
        public bool Changed =>
            PreviousHealth != CurrentHealth || PreviousMaximum != CurrentMaximum;
    }

    public sealed class HealthState
    {
        public HealthState(int maximum, int? current = null)
        {
            if (maximum < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maximum), "Maximum health must be positive.");
            }

            Maximum = maximum;
            Current = Math.Clamp(current ?? maximum, 0, maximum);
        }

        public event Action<HealthChangeResult> Changed;
        public event Action<HealthChangeResult> Defeated;

        public int Current { get; private set; }
        public int Maximum { get; private set; }
        public bool IsDefeated => Current == 0;

        public HealthChangeResult ApplyDamage(int damage)
        {
            if (damage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(damage), "Damage cannot be negative.");
            }

            var previous = Current;
            var applied = Math.Min(damage, Current);
            Current -= applied;
            return Publish(
                HealthChangeKind.Damage,
                previous,
                Maximum,
                applied,
                previous > 0 && Current == 0);
        }

        public HealthChangeResult Heal(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), "Healing cannot be negative.");
            }

            var previous = Current;
            var applied = Math.Min(amount, Maximum - Current);
            Current += applied;
            return Publish(
                HealthChangeKind.Healing,
                previous,
                Maximum,
                applied,
                false);
        }

        public HealthChangeResult RestoreToFull()
        {
            var previous = Current;
            Current = Maximum;
            return Publish(
                HealthChangeKind.Restore,
                previous,
                Maximum,
                Current - previous,
                false);
        }

        public HealthChangeResult SetMaximum(int maximum)
        {
            if (maximum < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maximum), "Maximum health must be positive.");
            }

            var previous = Current;
            var previousMaximum = Maximum;
            Maximum = maximum;
            Current = Math.Min(Current, Maximum);
            return Publish(
                HealthChangeKind.MaximumChanged,
                previous,
                previousMaximum,
                previous - Current,
                previous > 0 && Current == 0);
        }

        private HealthChangeResult Publish(
            HealthChangeKind kind,
            int previousHealth,
            int previousMaximum,
            int appliedAmount,
            bool becameDefeated)
        {
            var result = new HealthChangeResult(
                kind,
                previousHealth,
                Current,
                previousMaximum,
                Maximum,
                appliedAmount,
                becameDefeated);
            if (result.Changed)
            {
                Changed?.Invoke(result);
            }
            if (becameDefeated)
            {
                Defeated?.Invoke(result);
            }
            return result;
        }
    }
}
