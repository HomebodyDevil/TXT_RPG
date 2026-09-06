using System;
using System.Collections.Generic;
using UnityEngine;

namespace TxTRPG.Content.Characters
{
    public interface ICharacterContentRepository
    {
        bool TryGetLoaded(
            string characterDefinitionId,
            out CharacterContentDefinition definition);
    }

    [CreateAssetMenu(
        fileName = "CharacterContentCatalog",
        menuName = "TxT RPG/Characters/Character Content Catalog")]
    public sealed class CharacterContentCatalog : ScriptableObject, ICharacterContentRepository
    {
        [SerializeField] private List<CharacterContentDefinition> definitions = new();

        [NonSerialized] private Dictionary<string, CharacterContentDefinition> definitionsById;

        public IReadOnlyList<CharacterContentDefinition> Definitions => definitions;

        public bool TryGetLoaded(
            string characterDefinitionId,
            out CharacterContentDefinition definition)
        {
            EnsureIndex();
            var normalizedId = characterDefinitionId?.Trim() ?? string.Empty;
            return definitionsById.TryGetValue(normalizedId, out definition);
        }

        public CharacterContentDefinition GetRequired(string characterDefinitionId)
        {
            if (TryGetLoaded(characterDefinitionId, out var definition))
            {
                return definition;
            }
            throw new KeyNotFoundException(
                $"Character content '{characterDefinitionId?.Trim()}' was not found.");
        }

        public void RebuildIndex()
        {
            definitionsById = null;
            EnsureIndex();
        }

#if UNITY_EDITOR
        public bool AddForEditor(CharacterContentDefinition definition)
        {
            if (definition == null)
            {
                return false;
            }

            definitions ??= new List<CharacterContentDefinition>();
            foreach (var existing in definitions)
            {
                if (existing == definition ||
                    (existing != null && string.Equals(
                        existing.DefinitionId,
                        definition.DefinitionId,
                        StringComparison.Ordinal)))
                {
                    return false;
                }
            }

            definitions.Add(definition);
            definitionsById = null;
            return true;
        }

        public bool RemoveForEditor(CharacterContentDefinition definition)
        {
            if (definitions == null || !definitions.Remove(definition))
            {
                return false;
            }
            definitionsById = null;
            return true;
        }
#endif

        private void EnsureIndex()
        {
            if (definitionsById != null)
            {
                return;
            }

            var errors = CharacterContentValidation.CollectErrors(definitions);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
            }

            definitionsById = new Dictionary<string, CharacterContentDefinition>(
                definitions.Count,
                StringComparer.Ordinal);
            foreach (var definition in definitions)
            {
                definitionsById.Add(definition.DefinitionId, definition);
            }
        }

        private void OnEnable() => definitionsById = null;
        private void OnValidate() => definitionsById = null;
    }

    public static class CharacterContentValidation
    {
        public static IReadOnlyList<string> CollectErrors(
            IEnumerable<CharacterContentDefinition> definitions)
        {
            if (definitions == null)
            {
                return new[] { "Character content collection is null." };
            }

            var errors = new List<string>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var index = 0;
            foreach (var definition in definitions)
            {
                if (definition == null)
                {
                    errors.Add($"Character content entry {index} is null.");
                }
                else
                {
                    if (!definition.TryValidate(out var error))
                    {
                        errors.Add(error);
                    }
                    if (!string.IsNullOrWhiteSpace(definition.DefinitionId) &&
                        !ids.Add(definition.DefinitionId))
                    {
                        errors.Add(
                            $"Character content ID '{definition.DefinitionId}' is duplicated.");
                    }
                }
                index++;
            }
            return errors;
        }
    }
}
