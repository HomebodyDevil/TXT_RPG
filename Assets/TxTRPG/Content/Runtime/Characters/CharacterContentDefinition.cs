using System;
using System.Collections.Generic;
using TxTRPG.Gameplay.Characters;
using TxTRPG.UI;
using UnityEngine;

namespace TxTRPG.Content.Characters
{
    [CreateAssetMenu(
        fileName = "CharacterContentDefinition",
        menuName = "TxT RPG/Characters/Character Content Definition")]
    public sealed class CharacterContentDefinition : ScriptableObject
    {
        [SerializeField] private CharacterDefinition gameplayDefinition;
        [SerializeField] private CharacterAppearanceDefinition appearanceDefinition;
        [SerializeField] private CharacterVisualStatePolicy visualStatePolicy;
        [SerializeField] private string displayNameLocalizationKey = string.Empty;
        [SerializeField] private string[] tags = Array.Empty<string>();

        public string DefinitionId => gameplayDefinition != null
            ? gameplayDefinition.CharacterDefinitionId
            : string.Empty;
        public CharacterDefinition GameplayDefinition => gameplayDefinition;
        public CharacterAppearanceDefinition AppearanceDefinition => appearanceDefinition;
        public CharacterVisualStatePolicy VisualStatePolicy => visualStatePolicy;
        public string DisplayNameLocalizationKey => displayNameLocalizationKey;
        public IReadOnlyList<string> Tags => tags ?? Array.Empty<string>();

#if UNITY_EDITOR
        public void ConfigureForEditor(
            CharacterDefinition gameplay,
            CharacterAppearanceDefinition appearance,
            string localizationKey,
            IEnumerable<string> contentTags = null)
        {
            gameplayDefinition = gameplay;
            appearanceDefinition = appearance;
            displayNameLocalizationKey = localizationKey?.Trim() ?? string.Empty;
            tags = contentTags == null
                ? Array.Empty<string>()
                : new List<string>(contentTags).ToArray();
        }

        public void ConfigureForEditor(
            CharacterDefinition gameplay,
            CharacterAppearanceDefinition appearance,
            CharacterVisualStatePolicy statePolicy,
            string localizationKey,
            IEnumerable<string> contentTags = null)
        {
            ConfigureForEditor(gameplay, appearance, localizationKey, contentTags);
            visualStatePolicy = statePolicy;
        }
#endif

        public CharacterRuntimeState CreateRuntimeState(string characterInstanceId)
        {
            if (!TryValidate(out var error))
            {
                throw new InvalidOperationException(error);
            }
            return gameplayDefinition.CreateRuntimeState(characterInstanceId);
        }

        public bool TryValidate(out string error)
        {
            if (gameplayDefinition == null)
            {
                error = $"Character content '{name}' has no gameplay definition.";
                return false;
            }
            if (appearanceDefinition == null)
            {
                error = $"Character content '{name}' has no appearance definition.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(DefinitionId))
            {
                error = $"Character content '{name}' has an empty definition ID.";
                return false;
            }
            if (!string.Equals(
                    appearanceDefinition.CharacterId,
                    DefinitionId,
                    StringComparison.Ordinal))
            {
                error = $"Character content '{name}' uses gameplay ID '{DefinitionId}' " +
                        $"but appearance ID '{appearanceDefinition.CharacterId}'.";
                return false;
            }

            if (!appearanceDefinition.TryValidateAddressableReferences(out var appearanceError))
            {
                error = $"Character content '{name}' is invalid: {appearanceError}";
                return false;
            }

            if (visualStatePolicy != null &&
                !visualStatePolicy.TryValidate(out var policyError))
            {
                error = $"Character content '{name}' is invalid: {policyError}";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private void OnValidate()
        {
            displayNameLocalizationKey = displayNameLocalizationKey?.Trim() ?? string.Empty;
            tags ??= Array.Empty<string>();
            for (var index = 0; index < tags.Length; index++)
            {
                tags[index] = tags[index]?.Trim() ?? string.Empty;
            }
        }
    }
}
