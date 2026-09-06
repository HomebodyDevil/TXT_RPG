using System;
using System.Collections.Generic;
using TxTRPG.UI;
using TxTRPG.Editor.Common.Menu;
using UnityEditor;
using UnityEngine;

namespace TxTRPG.Content.Characters.Editor
{
    public static class DefaultCharacterContentBuilder
    {
        public const string CharactersFolder = "Assets/TxTRPG/Content/Characters";
        public const string OutputFolder = CharactersFolder + "/DefaultCharacter";
        public const string ArtworkFolder = OutputFolder + "/Artwork";
        public const string CatalogPath =
            CharactersFolder + "/CharacterContentCatalog.asset";
        public const string PlaceholderPath =
            ArtworkFolder + "/DefaultCharacterPlaceholder.asset";
        public const string ContentPath =
            OutputFolder + "/DefaultCharacterContent.asset";

        [MenuItem(
            TxTRPGEditorMenuPaths.CharacterContent + "Create Default Placeholder Content",
            false,
            TxTRPGEditorMenuPriorities.Create)]
        public static void CreateDefaultCharacterContent()
        {
            var existing = AssetDatabase.LoadAssetAtPath<CharacterContentDefinition>(
                ContentPath);
            if (existing != null)
            {
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                Debug.Log($"Default character content already exists at '{ContentPath}'.");
                return;
            }

            var createdRoot = !AssetDatabase.IsValidFolder(OutputFolder);
            var createdArtworkFolder = !AssetDatabase.IsValidFolder(ArtworkFolder);
            var placeholderExisted =
                AssetDatabase.LoadMainAssetAtPath(PlaceholderPath) != null;
            var createdCatalog = false;
            try
            {
                EnsureFolder(ArtworkFolder);
                var sprite = CreatePlaceholderSprite();
                var catalog = AssetDatabase.LoadAssetAtPath<CharacterContentCatalog>(
                    CatalogPath);
                if (catalog == null)
                {
                    EnsureFolder(CharactersFolder);
                    catalog = ScriptableObject.CreateInstance<CharacterContentCatalog>();
                    AssetDatabase.CreateAsset(catalog, CatalogPath);
                    createdCatalog = true;
                }

                const string baseAddress = "characters/default/artwork/states";
                var request = new CharacterContentCreationRequest
                {
                    DefinitionId = "character.default",
                    AssetName = "DefaultCharacter",
                    OutputFolder = OutputFolder,
                    DisplayNameLocalizationKey = "characters.default.name",
                    AttackPower = 10,
                    MaxHealth = 100,
                    DefaultSprite = sprite,
                    FramingPreset = CharacterFramingPreset.ThighUp,
                    AdditionalScale = 1f,
                    Catalog = catalog,
                    RegisterSpriteWithAddressables = true,
                    AddressablesGroup = "Character_Content",
                    SpriteAddress = baseAddress,
                    CriticalHealthRatio = 0.2f,
                    InjuredHealthRatio = 0.5f,
                    VisualVariants = CreateStandardVariants(sprite, baseAddress)
                };

                var result = new CharacterContentAssetFactory().Create(request);
                Selection.activeObject = result.ContentDefinition;
                EditorGUIUtility.PingObject(result.ContentDefinition);
                Debug.Log($"Created default character content at '{result.ContentAssetPath}'.");
            }
            catch
            {
                if (createdRoot && AssetDatabase.IsValidFolder(OutputFolder))
                {
                    AssetDatabase.DeleteAsset(OutputFolder);
                }
                else
                {
                    if (!placeholderExisted)
                    {
                        AssetDatabase.DeleteAsset(PlaceholderPath);
                    }
                    if (createdArtworkFolder && AssetDatabase.IsValidFolder(ArtworkFolder))
                    {
                        AssetDatabase.DeleteAsset(ArtworkFolder);
                    }
                }
                if (createdCatalog)
                {
                    AssetDatabase.DeleteAsset(CatalogPath);
                }
                AssetDatabase.SaveAssets();
                throw;
            }
        }

        private static List<CharacterVisualVariantInput> CreateStandardVariants(
            Sprite sprite,
            string baseAddress)
        {
            var result = new List<CharacterVisualVariantInput>();
            foreach (var stateId in new[]
                     {
                         CharacterVisualStatePolicy.NormalStateId,
                         CharacterVisualStatePolicy.InjuredStateId,
                         CharacterVisualStatePolicy.CriticalStateId,
                         CharacterVisualStatePolicy.DefeatedStateId
                     })
            {
                result.Add(new CharacterVisualVariantInput
                {
                    VisualStateId = stateId,
                    Sprite = sprite,
                    FramingPreset = CharacterFramingPreset.ThighUp,
                    AdditionalScale = 1f,
                    Address = baseAddress
                });
            }
            return result;
        }

        private static Sprite CreatePlaceholderSprite()
        {
            var existingAssets = AssetDatabase.LoadAllAssetsAtPath(PlaceholderPath);
            foreach (var asset in existingAssets)
            {
                if (asset is Sprite existingSprite)
                {
                    return existingSprite;
                }
            }
            if (existingAssets.Length > 0)
            {
                throw new InvalidOperationException(
                    $"Placeholder path is occupied: {PlaceholderPath}");
            }

            const int width = 64;
            const int height = 128;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "DefaultCharacterPlaceholderTexture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color[width * height];
            for (var y = 0; y < height; y++)
            {
                var vertical = y / (float)(height - 1);
                for (var x = 0; x < width; x++)
                {
                    var horizontal = Mathf.Abs((x / (float)(width - 1)) - 0.5f) * 2f;
                    var silhouette = horizontal < Mathf.Lerp(0.28f, 0.48f, vertical);
                    pixels[(y * width) + x] = silhouette
                        ? new Color(0.28f, 0.42f, 0.64f, 1f)
                        : new Color(0.08f, 0.1f, 0.16f, 0f);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            AssetDatabase.CreateAsset(texture, PlaceholderPath);

            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0f),
                100f);
            sprite.name = "DefaultCharacterPlaceholder";
            AssetDatabase.AddObjectToAsset(sprite, texture);
            EditorUtility.SetDirty(texture);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(PlaceholderPath, ImportAssetOptions.ForceUpdate);
            return sprite;
        }

        private static void EnsureFolder(string folder)
        {
            var segments = folder.Split('/');
            var current = segments[0];
            for (var index = 1; index < segments.Length; index++)
            {
                var next = $"{current}/{segments[index]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                }
                current = next;
            }
        }
    }
}
