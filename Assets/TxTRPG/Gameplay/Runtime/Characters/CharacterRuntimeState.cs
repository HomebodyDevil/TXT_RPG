using System;
using System.Collections.Generic;

namespace TxTRPG.Gameplay.Characters
{
    public sealed class CharacterRuntimeState
    {
        internal CharacterRuntimeState(
            string characterDefinitionId,
            string characterInstanceId,
            StatBlock stats,
            int? currentHealth = null)
        {
            CharacterDefinitionId = characterDefinitionId?.Trim() ?? string.Empty;
            CharacterInstanceId = characterInstanceId?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(CharacterDefinitionId))
            {
                throw new ArgumentException(
                    "A character definition ID is required.",
                    nameof(characterDefinitionId));
            }
            if (string.IsNullOrWhiteSpace(CharacterInstanceId))
            {
                throw new ArgumentException(
                    "A character instance ID is required.",
                    nameof(characterInstanceId));
            }

            Stats = stats ?? throw new ArgumentNullException(nameof(stats));
            Health = new HealthState(Stats.MaxHealth, currentHealth);
        }

        public string CharacterDefinitionId { get; }
        public string CharacterInstanceId { get; }

        [Obsolete("Use CharacterDefinitionId. CharacterId will be removed after save migration is complete.")]
        public string CharacterId => CharacterDefinitionId;
        public StatBlock Stats { get; }
        public HealthState Health { get; }

        public CharacterSaveData CreateSaveData()
        {
            return new CharacterSaveData
            {
                version = CharacterSaveData.CurrentVersion,
                characterId = CharacterDefinitionId,
                characterDefinitionId = CharacterDefinitionId,
                characterInstanceId = CharacterInstanceId,
                currentHealth = Health.Current,
                permanentStatBonuses = new List<SavedStatValue>(Stats.PermanentBonuses)
            };
        }
    }

    public sealed class CharacterFactory
    {
        private readonly Dictionary<string, CharacterDefinition> definitions =
            new(StringComparer.Ordinal);

        public CharacterFactory(IEnumerable<CharacterDefinition> definitions)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            foreach (var definition in definitions)
            {
                if (definition == null)
                {
                    throw new ArgumentException("Character definitions cannot contain null.");
                }

                var id = definition.CharacterDefinitionId;
                if (string.IsNullOrWhiteSpace(id))
                {
                    throw new ArgumentException("Every CharacterDefinition requires a character ID.");
                }
                if (!this.definitions.TryAdd(id, definition))
                {
                    throw new ArgumentException($"Character ID '{id}' is defined more than once.");
                }
            }
        }

        public CharacterRuntimeState Create(string characterId)
        {
            return Create(characterId, characterId);
        }

        public CharacterRuntimeState Create(
            string characterDefinitionId,
            string characterInstanceId)
        {
            var definition = GetDefinition(characterDefinitionId);
            return definition.CreateRuntimeState(characterInstanceId);
        }

        public CharacterRuntimeState Restore(CharacterSaveData saveData)
        {
            var migrated = CharacterSaveMigrator.Migrate(saveData);
            var definition = GetDefinition(migrated.characterDefinitionId);
            var stats = definition.CreateStatBlock(migrated.permanentStatBonuses);
            return new CharacterRuntimeState(
                definition.CharacterDefinitionId,
                migrated.characterInstanceId,
                stats,
                migrated.currentHealth);
        }

        private CharacterDefinition GetDefinition(string characterId)
        {
            var normalizedId = characterId?.Trim() ?? string.Empty;
            if (!definitions.TryGetValue(normalizedId, out var definition))
            {
                throw new KeyNotFoundException(
                    $"Character definition '{normalizedId}' was not found.");
            }
            return definition;
        }
    }
}
