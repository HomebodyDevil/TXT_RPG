using System;
using TxTRPG.Content.Characters;
using UnityEngine;

namespace TxTRPG.Application.Configuration
{
    [CreateAssetMenu(
        fileName = "NewGameProfile",
        menuName = "TxT RPG/Application/New Game Profile")]
    public sealed class NewGameProfile : ScriptableObject
    {
        [SerializeField] private CharacterContentCatalog characterCatalog;
        [SerializeField] private CharacterContentDefinition initialCharacter;

        public CharacterContentCatalog CharacterCatalog => characterCatalog;
        public CharacterContentDefinition InitialCharacter => initialCharacter;

        public bool TryValidate(out string error)
        {
            if (characterCatalog == null)
            {
                error = $"New game profile '{name}' has no character catalog.";
                return false;
            }
            if (initialCharacter == null)
            {
                error = $"New game profile '{name}' has no initial character.";
                return false;
            }

            var catalogErrors = CharacterContentValidation.CollectErrors(
                characterCatalog.Definitions);
            if (catalogErrors.Count > 0)
            {
                error = $"New game profile '{name}' uses an invalid character catalog: " +
                        string.Join(Environment.NewLine, catalogErrors);
                return false;
            }
            if (!initialCharacter.TryValidate(out var contentError))
            {
                error = contentError;
                return false;
            }
            if (initialCharacter.VisualStatePolicy == null)
            {
                error = $"Initial character '{initialCharacter.name}' has no visual state policy.";
                return false;
            }

            foreach (var definition in characterCatalog.Definitions)
            {
                if (definition == initialCharacter)
                {
                    error = string.Empty;
                    return true;
                }
            }

            error = $"Initial character '{initialCharacter.DefinitionId}' is not registered " +
                    $"in catalog '{characterCatalog.name}'.";
            return false;
        }

        public void ValidateOrThrow()
        {
            if (!TryValidate(out var error))
            {
                throw new InvalidOperationException(error);
            }
        }

#if UNITY_EDITOR
        public void ConfigureForEditor(
            CharacterContentCatalog catalog,
            CharacterContentDefinition initialContent)
        {
            characterCatalog = catalog;
            initialCharacter = initialContent;
        }
#endif
    }
}
