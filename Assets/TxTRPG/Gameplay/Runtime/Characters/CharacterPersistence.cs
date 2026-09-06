using System;
using System.Collections.Generic;
using UnityEngine;

namespace TxTRPG.Gameplay.Characters
{
    [Serializable]
    public sealed class CharacterSaveData
    {
        public const int CurrentVersion = 3;

        public int version = CurrentVersion;
        // Kept while version 1 and 2 saves and old callers are supported.
        public string characterId = string.Empty;
        public string characterInstanceId = string.Empty;
        public string characterDefinitionId = string.Empty;
        public int currentHealth;
        public List<SavedStatValue> permanentStatBonuses = new();
    }

    public static class CharacterSaveMigrator
    {
        public const string LegacyCharacterInstanceId = "legacy-character-0";

        public static CharacterSaveData Migrate(CharacterSaveData source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            if (source.version < 1)
            {
                throw new NotSupportedException(
                    $"Character save version {source.version} is not supported.");
            }
            if (source.version > CharacterSaveData.CurrentVersion)
            {
                throw new NotSupportedException(
                    $"Character save version {source.version} is newer than this game supports.");
            }

            var legacyDefinitionId = source.characterId?.Trim() ?? string.Empty;
            var definitionId = source.characterDefinitionId?.Trim() ?? string.Empty;
            var usesLegacyDefinitionId =
                string.IsNullOrEmpty(definitionId) && !string.IsNullOrEmpty(legacyDefinitionId);
            if (string.IsNullOrEmpty(definitionId))
            {
                definitionId = legacyDefinitionId;
            }

            var instanceId = source.characterInstanceId?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(instanceId) &&
                (source.version < CharacterSaveData.CurrentVersion || usesLegacyDefinitionId))
            {
                instanceId = LegacyCharacterInstanceId;
            }

            return new CharacterSaveData
            {
                version = CharacterSaveData.CurrentVersion,
                characterId = definitionId,
                characterDefinitionId = definitionId,
                characterInstanceId = instanceId,
                currentHealth = source.currentHealth,
                permanentStatBonuses = source.version >= 2 && source.permanentStatBonuses != null
                    ? new List<SavedStatValue>(source.permanentStatBonuses)
                    : new List<SavedStatValue>()
            };
        }
    }

    public static class CharacterSaveSerializer
    {
        public static string Serialize(CharacterRuntimeState state, bool prettyPrint = false)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }
            return JsonUtility.ToJson(state.CreateSaveData(), prettyPrint);
        }

        public static CharacterSaveData Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("Character save JSON cannot be empty.", nameof(json));
            }

            CharacterSaveData data;
            try
            {
                data = JsonUtility.FromJson<CharacterSaveData>(json);
            }
            catch (Exception exception)
            {
                throw new FormatException("Character save JSON is invalid.", exception);
            }
            if (data == null)
            {
                throw new FormatException("Character save JSON did not contain an object.");
            }
            return CharacterSaveMigrator.Migrate(data);
        }
    }
}
