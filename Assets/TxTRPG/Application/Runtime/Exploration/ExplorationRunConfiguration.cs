using UnityEngine;
using TxTRPG.Gameplay.Exploration;

namespace TxTRPG.Application.Exploration
{
    [CreateAssetMenu(menuName = "TxT RPG/Exploration Run Configuration", fileName = "ExplorationRunConfiguration")]
    public sealed class ExplorationRunConfiguration : ScriptableObject
    {
        [SerializeField, Min(1)] private int choiceCount = 3;
        [SerializeField, Min(0)] private int combatWeight = 50;
        [SerializeField, Min(0)] private int recoveryUpgradeWeight = 50;

        public int ChoiceCount => choiceCount;
        public int CombatWeight => combatWeight;
        public int RecoveryUpgradeWeight => recoveryUpgradeWeight;

        public ExplorationNodeWeight[] CreateWeights() => new[]
        {
            new ExplorationNodeWeight(ExplorationNodeTypeIds.Combat, combatWeight),
            new ExplorationNodeWeight(ExplorationNodeTypeIds.RecoveryUpgrade, recoveryUpgradeWeight)
        };

#if UNITY_EDITOR
        public void ConfigureForEditor(int choices, int combat, int recoveryUpgrade)
        { choiceCount = choices; combatWeight = combat; recoveryUpgradeWeight = recoveryUpgrade; }
#endif
    }
}