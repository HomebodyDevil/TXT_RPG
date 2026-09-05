using System;
using System.Collections.Generic;
using UnityEngine;

namespace TxTRPG.UI
{
    [Serializable]
    public sealed class EnemyPresentationData
    {
        [SerializeField] private string instanceId = string.Empty;
        [SerializeField] private string enemyId = string.Empty;
        [SerializeField] private string appearanceId = string.Empty;
        [SerializeField] private string poseId = string.Empty;
        [SerializeField] private string animationId = string.Empty;
        [SerializeField, Min(0)] private int formationIndex;
        [SerializeField] private bool mirrored;
        [SerializeField] private bool targeted;
        [SerializeField] private bool defeated;

        public EnemyPresentation ToPresentation() => new(
            instanceId,
            enemyId,
            appearanceId,
            poseId,
            animationId,
            formationIndex,
            mirrored,
            targeted,
            defeated);
    }

    [CreateAssetMenu(menuName = "TxT RPG/UI/Enemy Display Panel Demo Data")]
    public sealed class EnemyDisplayPanelDemoData : ScriptableObject
    {
        [SerializeField] private EnemyAppearanceDefinition appearanceDefinition;
        [SerializeField] private List<EnemyPresentationData> enemies = new();

        public EnemyAppearanceDefinition AppearanceDefinition => appearanceDefinition;
        public int EnemyCount => enemies.Count;

        public IReadOnlyList<EnemyPresentation> CreatePresentations()
        {
            var presentations = new EnemyPresentation[enemies.Count];
            for (var i = 0; i < enemies.Count; i++)
            {
                presentations[i] = enemies[i].ToPresentation();
            }
            return presentations;
        }
    }
}
