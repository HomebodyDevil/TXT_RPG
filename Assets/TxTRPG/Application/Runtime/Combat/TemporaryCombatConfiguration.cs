using UnityEngine;

namespace TxTRPG.Application.Combat
{
    [CreateAssetMenu(menuName = "TxT RPG/Temporary Combat Configuration", fileName = "TemporaryCombatConfiguration")]
    public sealed class TemporaryCombatConfiguration : ScriptableObject
    {
        [SerializeField] private bool enabledForScene = true;
        [SerializeField] private string enemyName = "테스트 적";
        [SerializeField] private string enemyId = "demo-slime";
        [SerializeField] private string enemyInstanceId = "temporary.enemy.1";
        [SerializeField, Min(1)] private int enemyMaximumHealth = 40;
        [SerializeField, Min(0)] private int enemyAttackAmount = 5;
        [SerializeField, Min(0)] private int enemyHealAmount = 3;

        public bool EnabledForScene => enabledForScene;
        public string EnemyName => string.IsNullOrWhiteSpace(enemyName) ? "테스트 적" : enemyName.Trim();
        public string EnemyId => string.IsNullOrWhiteSpace(enemyId) ? "demo-slime" : enemyId.Trim();
        public string EnemyInstanceId => string.IsNullOrWhiteSpace(enemyInstanceId) ? "temporary.enemy.1" : enemyInstanceId.Trim();
        public int EnemyMaximumHealth => Mathf.Max(1, enemyMaximumHealth);
        public int EnemyAttackAmount => Mathf.Max(0, enemyAttackAmount);
        public int EnemyHealAmount => Mathf.Max(0, enemyHealAmount);

#if UNITY_EDITOR
        public void ConfigureForEditor(bool enabled, string name, string id, string instanceId, int maximumHealth, int attack, int healing)
        {
            enabledForScene = enabled;
            enemyName = name;
            enemyId = id;
            enemyInstanceId = instanceId;
            enemyMaximumHealth = maximumHealth;
            enemyAttackAmount = attack;
            enemyHealAmount = healing;
        }
#endif
    }
}
