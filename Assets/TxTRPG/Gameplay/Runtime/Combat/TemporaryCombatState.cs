using System;
using System.Collections.Generic;
using TxTRPG.Gameplay.Characters;
using TxTRPG.Gameplay.Dice;

namespace TxTRPG.Gameplay.Combat
{
    public enum EnemyActionKind
    {
        Attack = 1,
        Heal = 2
    }

    public enum TemporaryCombatEventKind
    {
        PlayerAttack = 1,
        PlayerHeal = 2,
        PlayerAttackSkipped = 3,
        EnemyAttack = 4,
        EnemyHeal = 5,
        Victory = 6,
        Defeat = 7
    }

    public readonly struct TemporaryCombatEvent
    {
        public TemporaryCombatEvent(TemporaryCombatEventKind kind, int requestedAmount, int appliedAmount, int currentHealth = 0, int maximumHealth = 0)
        {
            Kind = kind;
            RequestedAmount = requestedAmount;
            AppliedAmount = appliedAmount;
            CurrentHealth = currentHealth;
            MaximumHealth = maximumHealth;
        }

        public TemporaryCombatEventKind Kind { get; }
        public int RequestedAmount { get; }
        public int AppliedAmount { get; }
        public int CurrentHealth { get; }
        public int MaximumHealth { get; }
    }

    public sealed class TemporaryCombatTurnResult
    {
        public TemporaryCombatTurnResult(IReadOnlyList<TemporaryCombatEvent> events)
        {
            Events = events ?? throw new ArgumentNullException(nameof(events));
        }

        public IReadOnlyList<TemporaryCombatEvent> Events { get; }
    }

    public sealed class TemporaryCombatState
    {
        private readonly IDamageResolver damageResolver;
        private readonly int enemyAttackAmount;
        private readonly int enemyHealAmount;

        public TemporaryCombatState(
            int playerMaximumHealth,
            int playerCurrentHealth,
            int enemyMaximumHealth,
            int enemyAttackAmount,
            int enemyHealAmount,
            IDamageResolver damageResolver = null)
            : this(new HealthState(playerMaximumHealth, playerCurrentHealth), enemyMaximumHealth, enemyAttackAmount, enemyHealAmount, damageResolver)
        {
        }

        public TemporaryCombatState(
            HealthState playerHealth,
            int enemyMaximumHealth,
            int enemyAttackAmount,
            int enemyHealAmount,
            IDamageResolver damageResolver = null)
        {
            PlayerHealth = playerHealth ?? throw new ArgumentNullException(nameof(playerHealth));
            if (PlayerHealth.IsDefeated) throw new ArgumentException("The combat player must be alive.", nameof(playerHealth));
            if (enemyAttackAmount < 0) throw new ArgumentOutOfRangeException(nameof(enemyAttackAmount));
            if (enemyHealAmount < 0) throw new ArgumentOutOfRangeException(nameof(enemyHealAmount));
            EnemyHealth = new HealthState(enemyMaximumHealth);
            this.enemyAttackAmount = enemyAttackAmount;
            this.enemyHealAmount = enemyHealAmount;
            this.damageResolver = damageResolver ?? new PassthroughDamageResolver();
            NextEnemyAction = EnemyActionKind.Attack;
        }

        public HealthState PlayerHealth { get; }
        public HealthState EnemyHealth { get; }
        public EnemyActionKind NextEnemyAction { get; private set; }
        public bool IsComplete => PlayerHealth.IsDefeated || EnemyHealth.IsDefeated;

        public TemporaryCombatTurnResult ExecuteTurn(IReadOnlyList<DiceRollResult> rolls)
        {
            if (rolls == null) throw new ArgumentNullException(nameof(rolls));
            if (IsComplete) throw new InvalidOperationException("The temporary combat is already complete.");

            var events = new List<TemporaryCombatEvent>();
            for (var i = 0; i < rolls.Count; i++)
            {
                var roll = rolls[i];
                switch (roll.EffectKind)
                {
                    case DiceEffectKind.Attack:
                        if (EnemyHealth.IsDefeated)
                        {
                            events.Add(new TemporaryCombatEvent(TemporaryCombatEventKind.PlayerAttackSkipped, roll.Amount, 0, EnemyHealth.Current, EnemyHealth.Maximum));
                            break;
                        }
                        var damage = damageResolver.Resolve(new DamageRequest("temporary.player", "temporary.enemy", roll.Amount));
                        var enemyDamage = EnemyHealth.ApplyDamage(damage.FinalDamage);
                        events.Add(new TemporaryCombatEvent(TemporaryCombatEventKind.PlayerAttack, roll.Amount, enemyDamage.AppliedAmount, EnemyHealth.Current, EnemyHealth.Maximum));
                        break;
                    case DiceEffectKind.Heal:
                        var healing = PlayerHealth.Heal(roll.Amount);
                        events.Add(new TemporaryCombatEvent(TemporaryCombatEventKind.PlayerHeal, roll.Amount, healing.AppliedAmount, PlayerHealth.Current, PlayerHealth.Maximum));
                        break;
                    default:
                        throw new InvalidOperationException($"Unsupported dice effect '{roll.EffectKind}'.");
                }
            }

            if (!EnemyHealth.IsDefeated)
            {
                ExecuteEnemyAction(events);
            }

            if (EnemyHealth.IsDefeated)
                events.Add(new TemporaryCombatEvent(TemporaryCombatEventKind.Victory, 0, 0));
            else if (PlayerHealth.IsDefeated)
                events.Add(new TemporaryCombatEvent(TemporaryCombatEventKind.Defeat, 0, 0));

            return new TemporaryCombatTurnResult(events);
        }

        private void ExecuteEnemyAction(List<TemporaryCombatEvent> events)
        {
            if (NextEnemyAction == EnemyActionKind.Attack)
            {
                var damage = damageResolver.Resolve(new DamageRequest("temporary.enemy", "temporary.player", enemyAttackAmount));
                var change = PlayerHealth.ApplyDamage(damage.FinalDamage);
                events.Add(new TemporaryCombatEvent(TemporaryCombatEventKind.EnemyAttack, enemyAttackAmount, change.AppliedAmount, PlayerHealth.Current, PlayerHealth.Maximum));
                NextEnemyAction = EnemyActionKind.Heal;
            }
            else
            {
                var change = EnemyHealth.Heal(enemyHealAmount);
                events.Add(new TemporaryCombatEvent(TemporaryCombatEventKind.EnemyHeal, enemyHealAmount, change.AppliedAmount, EnemyHealth.Current, EnemyHealth.Maximum));
                NextEnemyAction = EnemyActionKind.Attack;
            }
        }
    }
}
