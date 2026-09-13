using System;
using NUnit.Framework;
using System.Linq;
using TxTRPG.Gameplay.Combat;
using TxTRPG.Gameplay.Dice;

namespace TxTRPG.Gameplay.Tests
{
    public sealed class TemporaryCombatStateTests
    {
        [Test]
        public void Turn_AppliesPlayerRollsBeforeAdvertisedEnemyAction()
        {
            var combat = new TemporaryCombatState(20, 10, 40, 5, 3);
            var result = combat.ExecuteTurn(new[]
            {
                Roll(DiceEffectKind.Attack, 7),
                Roll(DiceEffectKind.Heal, 4)
            });

            Assert.That(combat.EnemyHealth.Current, Is.EqualTo(33));
            Assert.That(combat.PlayerHealth.Current, Is.EqualTo(9));
            Assert.That(result.Events[0].Kind, Is.EqualTo(TemporaryCombatEventKind.PlayerAttack));
            Assert.That(result.Events[1].Kind, Is.EqualTo(TemporaryCombatEventKind.PlayerHeal));
            Assert.That(result.Events[2].Kind, Is.EqualTo(TemporaryCombatEventKind.EnemyAttack));
            Assert.That(combat.NextEnemyAction, Is.EqualTo(EnemyActionKind.Heal));
        }

        [Test]
        public void EnemyDefeat_SkipsRemainingAttacksButKeepsRemainingHealing()
        {
            var combat = new TemporaryCombatState(20, 10, 3, 5, 3);
            var result = combat.ExecuteTurn(new[]
            {
                Roll(DiceEffectKind.Attack, 3),
                Roll(DiceEffectKind.Attack, 5),
                Roll(DiceEffectKind.Heal, 4)
            });

            Assert.That(combat.EnemyHealth.Current, Is.Zero);
            Assert.That(combat.PlayerHealth.Current, Is.EqualTo(14));
            Assert.That(result.Events[1].Kind, Is.EqualTo(TemporaryCombatEventKind.PlayerAttackSkipped));
            Assert.That(result.Events[2].Kind, Is.EqualTo(TemporaryCombatEventKind.PlayerHeal));
            Assert.That(result.Events, Has.None.Matches<TemporaryCombatEvent>(item =>
                item.Kind == TemporaryCombatEventKind.EnemyAttack || item.Kind == TemporaryCombatEventKind.EnemyHeal));
            Assert.That(result.Events[^1].Kind, Is.EqualTo(TemporaryCombatEventKind.Victory));
        }

        [Test]
        public void EnemyActions_AlternateAndFullHealingRecordsZero()
        {
            var combat = new TemporaryCombatState(30, 30, 40, 5, 3);
            combat.ExecuteTurn(Array.Empty<DiceRollResult>());
            var second = combat.ExecuteTurn(Array.Empty<DiceRollResult>());

            Assert.That(second.Events[0].Kind, Is.EqualTo(TemporaryCombatEventKind.EnemyHeal));
            Assert.That(second.Events[0].RequestedAmount, Is.EqualTo(3));
            Assert.That(second.Events[0].AppliedAmount, Is.Zero);
            Assert.That(combat.NextEnemyAction, Is.EqualTo(EnemyActionKind.Attack));
        }

        [Test]
        public void Defeat_BlocksFurtherTurns()
        {
            var combat = new TemporaryCombatState(4, 4, 40, 5, 3);
            var result = combat.ExecuteTurn(Array.Empty<DiceRollResult>());
            Assert.That(combat.PlayerHealth.IsDefeated, Is.True);
            Assert.That(result.Events[^1].Kind, Is.EqualTo(TemporaryCombatEventKind.Defeat));
            Assert.Throws<InvalidOperationException>(() => combat.ExecuteTurn(Array.Empty<DiceRollResult>()));
        }

        [Test]
        public void CombatHealth_IsIndependentFromSourceValues()
        {
            var combat = new TemporaryCombatState(100, 80, 40, 5, 3);
            combat.PlayerHealth.ApplyDamage(10);
            Assert.That(combat.PlayerHealth.Current, Is.EqualTo(70));
            Assert.That(combat.PlayerHealth.Maximum, Is.EqualTo(100));
        }

        private static DiceRollResult Roll(DiceEffectKind kind, int amount) =>
            new DiceRollResult(0, new DiceFace(kind, amount));
    }
}
