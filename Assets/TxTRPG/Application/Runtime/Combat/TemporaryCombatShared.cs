using System;
using TxTRPG.Gameplay.Characters;
using TxTRPG.Gameplay.Combat;
using UnityEngine;

namespace TxTRPG.Application.Combat
{
    // The combat controller the exploration flow drives. Implemented by the slot machine controller (active in
    // TMP_MainScene) and by the reserved dice controller, so either can be wired without changing exploration.
    public interface ITemporaryCombatController
    {
        bool IsReady { get; }
        TemporaryCombatState Combat { get; }
        event Action<bool> CombatFinished;
        void BeginCombat(HealthState playerHealth);
        void ClearCombat();
    }

    public static class TemporaryCombatPresentationTimeout
    {
        public const float MaximumSeconds = 15f;

        // The timeout never cuts a normal presentation short: max(minimum, longest presentation + margin), within [1, 15]s.
        public static float Resolve(float minimumSeconds, float presentationSeconds, float marginSeconds) =>
            Mathf.Clamp(Mathf.Max(minimumSeconds, presentationSeconds + Mathf.Max(0f, marginSeconds)), 1f, MaximumSeconds);
    }

    // Story texts shared by the dice and slot machine combat paths. Replaceable by a localization provider later.
    public static class TemporaryCombatStoryText
    {
        public static string PlayerEvent(TemporaryCombatEvent combatEvent, string enemyName)
        {
            switch (combatEvent.Kind)
            {
                case TemporaryCombatEventKind.PlayerAttack:
                    return $"[전투] {enemyName}에게 피해 {combatEvent.AppliedAmount}. 체력 {combatEvent.CurrentHealth}/{combatEvent.MaximumHealth}.";
                case TemporaryCombatEventKind.PlayerHeal:
                    return $"[전투] 플레이어 회복 요청 {combatEvent.RequestedAmount}, 실제 회복 {combatEvent.AppliedAmount}. 체력 {combatEvent.CurrentHealth}/{combatEvent.MaximumHealth}.";
                case TemporaryCombatEventKind.PlayerAttackSkipped:
                    return $"[전투] {enemyName}이 이미 쓰러져 공격 {combatEvent.RequestedAmount}을 건너뛰었습니다.";
                default:
                    return null;
            }
        }

        public static string OutcomeEvent(TemporaryCombatEvent combatEvent, string enemyName)
        {
            switch (combatEvent.Kind)
            {
                case TemporaryCombatEventKind.EnemyAttack:
                    return $"[적 행동] 공격 {combatEvent.RequestedAmount}. 플레이어가 받은 피해 {combatEvent.AppliedAmount}. 체력 {combatEvent.CurrentHealth}/{combatEvent.MaximumHealth}.";
                case TemporaryCombatEventKind.EnemyHeal:
                    return $"[적 행동] 회복 요청 {combatEvent.RequestedAmount}, 실제 회복 {combatEvent.AppliedAmount}. 체력 {combatEvent.CurrentHealth}/{combatEvent.MaximumHealth}.";
                case TemporaryCombatEventKind.Victory:
                    return $"[전투] {enemyName}을 쓰러뜨렸습니다. 임시 전투가 종료되었습니다.";
                case TemporaryCombatEventKind.Defeat:
                    return "[전투] 플레이어가 전투 불능이 되었습니다. 임시 전투가 종료되었습니다.";
                default:
                    return null;
            }
        }

        public const string ActionFailed = "[전투] 행동 처리 중 오류가 발생했습니다. 이미 적용된 결과는 다시 실행하지 않습니다.";
    }
}
