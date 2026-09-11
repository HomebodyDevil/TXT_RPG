using System;
using System.Collections.Generic;
using TxTRPG.Gameplay.Characters;
using TxTRPG.Gameplay.Items;
using UnityEngine;

namespace TxTRPG.Gameplay.Players
{
    [Serializable]
    public sealed class PlayerSaveData
    {
        public const int CurrentVersion = 2;

        public int version = CurrentVersion;
        public string activeCharacterInstanceId = string.Empty;
        public List<CharacterSaveData> characters = new();
        public List<ItemQuantitySaveData> inventory = new();
        public List<QuickItemSlotSaveData> quickItems = new();
    }

    [Serializable] public sealed class ItemQuantitySaveData { public string itemDefinitionId = string.Empty; public int quantity; }
    [Serializable] public sealed class QuickItemSlotSaveData { public int slotIndex; public string itemDefinitionId = string.Empty; }

    public static class PlayerSaveMigrator
    {
        public static PlayerSaveData Migrate(PlayerSaveData source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            if (source.version < 1 || source.version > PlayerSaveData.CurrentVersion)
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
                characters = migratedCharacters,
                inventory = source.version >= 2 && source.inventory != null ? new List<ItemQuantitySaveData>(source.inventory) : new List<ItemQuantitySaveData>(),
                quickItems = source.version >= 2 && source.quickItems != null ? new List<QuickItemSlotSaveData>(source.quickItems) : new List<QuickItemSlotSaveData>()
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
            var quantities = new List<KeyValuePair<string, int>>();
            foreach (var item in migrated.inventory)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.itemDefinitionId)) throw new FormatException("Inventory contains an empty item ID.");
                quantities.Add(new KeyValuePair<string, int>(item.itemDefinitionId, item.quantity));
            }
            var quickSlots = new List<KeyValuePair<int, string>>();
            foreach (var slot in migrated.quickItems)
            {
                if (slot == null || string.IsNullOrWhiteSpace(slot.itemDefinitionId)) throw new FormatException("Quick-item data contains an empty item ID.");
                quickSlots.Add(new KeyValuePair<int, string>(slot.slotIndex, slot.itemDefinitionId));
            }
            return new PlayerState(characters, migrated.activeCharacterInstanceId,
                new InventoryState(quantities),
                new QuickItemLoadout(QuickItemLoadout.DefaultCapacity, quickSlots));
        }
    }
}
