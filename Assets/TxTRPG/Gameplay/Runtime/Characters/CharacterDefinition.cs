using System;
using System.Collections.Generic;
using UnityEngine;

namespace TxTRPG.Gameplay.Characters
{
    [CreateAssetMenu(
        fileName = "CharacterDefinition",
        menuName = "TxT RPG/Characters/Character Definition")]
    public sealed class CharacterDefinition : ScriptableObject
    {
        [SerializeField] private string characterId = "character.new";
        [SerializeField] private List<BaseStatEntry> baseStats = new()
        {
            new BaseStatEntry(CoreStatIds.AttackPower, 10),
            new BaseStatEntry(CoreStatIds.MaxHealth, 100)
        };

        public string CharacterDefinitionId => characterId?.Trim() ?? string.Empty;

        [Obsolete("Use CharacterDefinitionId. CharacterId will be removed after save migration is complete.")]
        public string CharacterId => CharacterDefinitionId;
        public IReadOnlyList<BaseStatEntry> BaseStats => baseStats;

        public StatBlock CreateStatBlock(
            IEnumerable<SavedStatValue> permanentBonuses = null)
        {
            if (string.IsNullOrWhiteSpace(CharacterDefinitionId))
            {
                throw new InvalidOperationException("CharacterDefinition requires a character ID.");
            }
            return new StatBlock(baseStats, permanentBonuses);
        }

        public CharacterRuntimeState CreateRuntimeState()
        {
            return CreateRuntimeState(CharacterDefinitionId);
        }

        public CharacterRuntimeState CreateRuntimeState(string characterInstanceId)
        {
            return new CharacterRuntimeState(
                CharacterDefinitionId,
                characterInstanceId,
                CreateStatBlock());
        }

#if UNITY_EDITOR
        public void ConfigureForEditor(
            string definitionId,
            IEnumerable<BaseStatEntry> stats)
        {
            if (stats == null)
            {
                throw new ArgumentNullException(nameof(stats));
            }

            characterId = definitionId?.Trim() ?? string.Empty;
            baseStats = new List<BaseStatEntry>(stats);
        }
#endif

        private void OnValidate()
        {
            characterId = characterId?.Trim() ?? string.Empty;
            baseStats ??= new List<BaseStatEntry>();
        }
    }
}
