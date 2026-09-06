using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TxTRPG.Gameplay.Characters;

namespace TxTRPG.Gameplay.Players
{
    public sealed class PlayerState
    {
        private readonly List<CharacterRuntimeState> characters = new();
        private readonly Dictionary<string, CharacterRuntimeState> charactersByInstanceId =
            new(StringComparer.Ordinal);
        private readonly ReadOnlyCollection<CharacterRuntimeState> charactersView;

        public PlayerState(
            IEnumerable<CharacterRuntimeState> characters,
            string activeCharacterInstanceId)
        {
            if (characters == null)
            {
                throw new ArgumentNullException(nameof(characters));
            }

            foreach (var character in characters)
            {
                AddCharacterCore(character);
            }
            if (this.characters.Count == 0)
            {
                throw new ArgumentException(
                    "A player must own at least one character.",
                    nameof(characters));
            }

            ActiveCharacterInstanceId = NormalizeId(activeCharacterInstanceId);
            if (!charactersByInstanceId.ContainsKey(ActiveCharacterInstanceId))
            {
                throw new ArgumentException(
                    $"Active character '{ActiveCharacterInstanceId}' is not owned by the player.",
                    nameof(activeCharacterInstanceId));
            }

            charactersView = this.characters.AsReadOnly();
        }

        public event Action<CharacterRuntimeState, CharacterRuntimeState> ActiveCharacterChanged;

        public IReadOnlyList<CharacterRuntimeState> Characters => charactersView;
        public string ActiveCharacterInstanceId { get; private set; }
        public CharacterRuntimeState ActiveCharacter =>
            charactersByInstanceId[ActiveCharacterInstanceId];

        public bool TryGetCharacter(
            string characterInstanceId,
            out CharacterRuntimeState character)
        {
            return charactersByInstanceId.TryGetValue(
                NormalizeId(characterInstanceId),
                out character);
        }

        public bool TrySetActiveCharacter(string characterInstanceId)
        {
            var normalizedId = NormalizeId(characterInstanceId);
            if (!charactersByInstanceId.TryGetValue(normalizedId, out var next))
            {
                return false;
            }
            if (normalizedId == ActiveCharacterInstanceId)
            {
                return true;
            }

            var previous = ActiveCharacter;
            ActiveCharacterInstanceId = normalizedId;
            ActiveCharacterChanged?.Invoke(previous, next);
            return true;
        }

        public void AddCharacter(CharacterRuntimeState character)
        {
            AddCharacterCore(character);
        }

        public bool RemoveCharacter(string characterInstanceId)
        {
            var normalizedId = NormalizeId(characterInstanceId);
            if (!charactersByInstanceId.TryGetValue(normalizedId, out var removed))
            {
                return false;
            }
            if (characters.Count == 1)
            {
                return false;
            }

            var removedIndex = characters.IndexOf(removed);
            CharacterRuntimeState replacement = null;
            if (normalizedId == ActiveCharacterInstanceId)
            {
                var replacementIndex = removedIndex + 1 < characters.Count
                    ? removedIndex + 1
                    : removedIndex - 1;
                replacement = characters[replacementIndex];
            }

            characters.RemoveAt(removedIndex);
            charactersByInstanceId.Remove(normalizedId);
            if (replacement != null)
            {
                ActiveCharacterInstanceId = replacement.CharacterInstanceId;
                ActiveCharacterChanged?.Invoke(removed, replacement);
            }
            return true;
        }

        public PlayerSaveData CreateSaveData()
        {
            var savedCharacters = new List<CharacterSaveData>(characters.Count);
            foreach (var character in characters)
            {
                savedCharacters.Add(character.CreateSaveData());
            }

            return new PlayerSaveData
            {
                version = PlayerSaveData.CurrentVersion,
                activeCharacterInstanceId = ActiveCharacterInstanceId,
                characters = savedCharacters
            };
        }

        private void AddCharacterCore(CharacterRuntimeState character)
        {
            if (character == null)
            {
                throw new ArgumentNullException(nameof(character));
            }
            if (!charactersByInstanceId.TryAdd(character.CharacterInstanceId, character))
            {
                throw new ArgumentException(
                    $"Character instance ID '{character.CharacterInstanceId}' is already owned.",
                    nameof(character));
            }
            characters.Add(character);
        }

        private static string NormalizeId(string id) => id?.Trim() ?? string.Empty;
    }
}
