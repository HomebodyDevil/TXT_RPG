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

    public enum PlayerCombatActionKind
    {
        Attack = 1,
        Heal = 2
    }

    // Player input to one combat turn: what to do, how much, and where it came from (for example a rune line ID).
    // Dice rolls and rune allocations both become this, so the combat rules do not depend on either system.
    public readonly struct PlayerCombatAction
    {
        public PlayerCombatAction(PlayerCombatActionKind kind, int amount, string sourceId = "")
        {
            if (kind != PlayerCombatActionKind.Attack && kind != PlayerCombatActionKind.Heal)
                throw new ArgumentOutOfRangeException(nameof(kind), $"Unsupported player combat action '{kind}'.");
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Kind = kind;
            Amount = amount;
            SourceId = sourceId ?? string.Empty;
        }

        public PlayerCombatActionKind Kind { get; }
        public int Amount { get; }
        public string SourceId { get; }
    }

    public readonly struct TemporaryCombatEvent
    {
        public TemporaryCombatEvent(TemporaryCombatEventKind kind, int requestedAmount, int appliedAmount, int currentHealth = 0, int maximumHealth = 0)
            : this(kind, requestedAmount, appliedAmount, currentHealth, maximumHealth, string.Empty)
        {
        }

        public TemporaryCombatEvent(TemporaryCombatEventKind kind, int requestedAmount, int appliedAmount, int currentHealth, int maximumHealth, string sourceId)
        {
            Kind = kind;
            RequestedAmount = requestedAmount;
            AppliedAmount = appliedAmount;
            CurrentHealth = currentHealth;
            MaximumHealth = maximumHealth;
            sourceIdValue = sourceId ?? string.Empty;
        }

        private readonly string sourceIdValue;

        public TemporaryCombatEventKind Kind { get; }
        public int RequestedAmount { get; }
        public int AppliedAmount { get; }
        public int CurrentHealth { get; }
        public int MaximumHealth { get; }
        // The PlayerCombatAction source of a player event (empty for enemy and outcome events, and for dice rolls).
        public string SourceId => sourceIdValue ?? string.Empty;
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

        // Dice input, kept for the reserved dice system. Rolls are converted up front, so an unsupported effect is
        // rejected before any change is applied.
        public TemporaryCombatTurnResult ExecuteTurn(IReadOnlyList<DiceRollResult> rolls)
        {
            if (rolls == null) throw new ArgumentNullException(nameof(rolls));
            var actions = new PlayerCombatAction[rolls.Count];
            for (var i = 0; i < rolls.Count; i++)
            {
                var roll = rolls[i];
                actions[i] = roll.EffectKind switch
                {
                    DiceEffectKind.Attack => new PlayerCombatAction(PlayerCombatActionKind.Attack, roll.Amount),
                    DiceEffectKind.Heal => new PlayerCombatAction(PlayerCombatActionKind.Heal, roll.Amount),
                    _ => throw new InvalidOperationException($"Unsupported dice effect '{roll.EffectKind}'.")
                };
            }
            return ExecuteTurn(actions);
        }

        // Applies the player actions in order (an attack on a defeated enemy is recorded as skipped), then the advertised
        // enemy action when the enemy is still standing, then the victory or defeat event.
        public TemporaryCombatTurnResult ExecuteTurn(IReadOnlyList<PlayerCombatAction> actions)
        {
            if (actions == null) throw new ArgumentNullException(nameof(actions));
            if (IsComplete) throw new InvalidOperationException("The temporary combat is already complete.");

            var events = new List<TemporaryCombatEvent>();
            for (var i = 0; i < actions.Count; i++)
            {
                var action = actions[i];
                switch (action.Kind)
                {
                    case PlayerCombatActionKind.Attack:
                        if (EnemyHealth.IsDefeated)
                        {
                            events.Add(new TemporaryCombatEvent(TemporaryCombatEventKind.PlayerAttackSkipped, action.Amount, 0, EnemyHealth.Current, EnemyHealth.Maximum, action.SourceId));
                            break;
                        }
                        var damage = damageResolver.Resolve(new DamageRequest("temporary.player", "temporary.enemy", action.Amount));
                        var enemyDamage = EnemyHealth.ApplyDamage(damage.FinalDamage);
                        events.Add(new TemporaryCombatEvent(TemporaryCombatEventKind.PlayerAttack, action.Amount, enemyDamage.AppliedAmount, EnemyHealth.Current, EnemyHealth.Maximum, action.SourceId));
                        break;
                    case PlayerCombatActionKind.Heal:
                        var healing = PlayerHealth.Heal(action.Amount);
                        events.Add(new TemporaryCombatEvent(TemporaryCombatEventKind.PlayerHeal, action.Amount, healing.AppliedAmount, PlayerHealth.Current, PlayerHealth.Maximum, action.SourceId));
                        break;
                    default:
                        throw new InvalidOperationException($"Unsupported player combat action '{action.Kind}'.");
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
