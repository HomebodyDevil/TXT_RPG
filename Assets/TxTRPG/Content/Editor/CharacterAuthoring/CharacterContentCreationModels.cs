using System;
using System.Collections.Generic;
using TxTRPG.Content.Characters;
using TxTRPG.Gameplay.Characters;
using TxTRPG.UI;
using UnityEngine;

namespace TxTRPG.Content.Characters.Editor
{
    [Serializable]
    public sealed class CharacterVisualVariantInput
    {
        public string VisualStateId = string.Empty;
        public string AppearanceId = string.Empty;
        public string PoseId = string.Empty;
        public string ExpressionId = string.Empty;
        public Sprite Sprite;
        public CharacterFramingPreset FramingPreset = CharacterFramingPreset.ThighUp;
        public float AdditionalScale = 1f;
        public Vector2 PixelOffset;
        public string Address = string.Empty;
    }

    public sealed class CharacterContentCreationRequest
    {
        public string OutputFolder { get; set; }
        public string AssetName { get; set; }
        public string DefinitionId { get; set; }
        public int AttackPower { get; set; }
        public int MaxHealth { get; set; }
        public Sprite DefaultSprite { get; set; }
        public CharacterFramingPreset FramingPreset { get; set; } =
            CharacterFramingPreset.ThighUp;
        public float AdditionalScale { get; set; } = 1f;
        public Vector2 PixelOffset { get; set; }
        public string DisplayNameLocalizationKey { get; set; }
        public CharacterContentCatalog Catalog { get; set; }
        public bool RegisterSpriteWithAddressables { get; set; } = true;
        public string AddressablesGroup { get; set; } = "Character_Content";
        public string SpriteAddress { get; set; }
        public List<CharacterVisualVariantInput> VisualVariants { get; set; } = new();
        public float InjuredHealthRatio { get; set; } = 0.5f;
        public float CriticalHealthRatio { get; set; } = 0.2f;
    }

    public sealed class CharacterContentCreationResult
    {
        public CharacterDefinition GameplayDefinition { get; internal set; }
        public CharacterAppearanceDefinition AppearanceDefinition { get; internal set; }
        public CharacterContentDefinition ContentDefinition { get; internal set; }
        public CharacterVisualStatePolicy VisualStatePolicy { get; internal set; }
        public string GameplayAssetPath { get; internal set; }
        public string AppearanceAssetPath { get; internal set; }
        public string ContentAssetPath { get; internal set; }
        public string VisualStatePolicyAssetPath { get; internal set; }
    }

    public readonly struct CharacterContentAssetPaths
    {
        public CharacterContentAssetPaths(string folder, string assetName)
        {
            Folder = folder.TrimEnd('/');
            Gameplay = $"{Folder}/{assetName}Gameplay.asset";
            Appearance = $"{Folder}/{assetName}Appearance.asset";
            VisualStatePolicy = $"{Folder}/{assetName}VisualStatePolicy.asset";
            Content = $"{Folder}/{assetName}Content.asset";
        }

        public string Folder { get; }
        public string Gameplay { get; }
        public string Appearance { get; }
        public string VisualStatePolicy { get; }
        public string Content { get; }
    }

    public enum CharacterContentCreationStep
    {
        GameplayCreated,
        AppearanceCreated,
        ContentCreated,
        CatalogUpdated,
        AddressablesRegistered,
        VisualStatePolicyCreated
    }
}
