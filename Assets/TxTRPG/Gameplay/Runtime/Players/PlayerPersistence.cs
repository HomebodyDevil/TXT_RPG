using System;
using System.Collections.Generic;
using TxTRPG.Gameplay.Characters;
using UnityEngine;

namespace TxTRPG.Gameplay.Players
{
    [Serializable]
    public sealed class PlayerSaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public string activeCharacterInstanceId = string.Empty;
        public List<CharacterSaveData> characters = new();
    }

    public static class PlayerSaveMigrator
    {
        public static PlayerSaveData Migrate(PlayerSaveData source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            if (source.version != PlayerSaveData.CurrentVersion)
            {
                throw new NotSupportedException(
                    $"Player save version {source.version} is not supported.");
            }

            var migratedCharacters = new List<CharacterSaveData>();
            if (source.characters != null)
            {
                foreach (var character in source.characters)
                {
                    migratedCharacters.Add(CharacterSaveMigrator.Migrate(character));
                }
            }

            return new PlayerSaveData
            {
                version = PlayerSaveData.CurrentVersion,
                activeCharacterInstanceId = source.activeCharacterInstanceId?.Trim() ?? string.Empty,
                characters = migratedCharacters
            };
        }

        public static PlayerSaveData FromLegacyCharacter(CharacterSaveData legacyCharacter)
        {
            var migratedCharacter = CharacterSaveMigrator.Migrate(legacyCharacter);
            migratedCharacter.characterInstanceId =
                CharacterSaveMigrator.LegacyCharacterInstanceId;

            return new PlayerSaveData
            {
                version = PlayerSaveData.CurrentVersion,
                activeCharacterInstanceId = CharacterSaveMigrator.LegacyCharacterInstanceId,
                characters = new List<CharacterSaveData> { migratedCharacter }
            };
        }
    }

    public static class PlayerSaveSerializer
    {
        public static string Serialize(PlayerState state, bool prettyPrint = false)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }
            return JsonUtility.ToJson(state.CreateSaveData(), prettyPrint);
        }

        public static PlayerSaveData Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("Player save JSON cannot be empty.", nameof(json));
            }

            PlayerSaveData data;
            try
            {
                data = JsonUtility.FromJson<PlayerSaveData>(json);
            }
            catch (Exception exception)
            {
                throw new FormatException("Player save JSON is invalid.", exception);
            }
            if (data == null)
            {
                throw new FormatException("Player save JSON did not contain an object.");
            }
            return PlayerSaveMigrator.Migrate(data);
        }
    }

    public sealed class PlayerFactory
    {
        private readonly CharacterFactory characterFactory;

        public PlayerFactory(CharacterFactory characterFactory)
        {
            this.characterFactory = characterFactory ??
                throw new ArgumentNullException(nameof(characterFactory));
        }

        public PlayerState CreateNew(
            string initialCharacterDefinitionId,
            string initialCharacterInstanceId)
        {
            var character = characterFactory.Create(
                initialCharacterDefinitionId,
                initialCharacterInstanceId);
            return new PlayerState(
                new[] { character },
                character.CharacterInstanceId);
        }

        public PlayerState Restore(PlayerSaveData saveData)
        {
            var migrated = PlayerSaveMigrator.Migrate(saveData);
            var characters = new List<CharacterRuntimeState>(migrated.characters.Count);
            foreach (var savedCharacter in migrated.characters)
            {
                characters.Add(characterFactory.Restore(savedCharacter));
            }
            return new PlayerState(characters, migrated.activeCharacterInstanceId);
        }
    }
}
