using System;
using System.Collections.Generic;
using TxTRPG.Editor.Common.Addressables;
using TxTRPG.Gameplay.Characters;
using TxTRPG.UI;
using UnityEditor;
using UnityEngine;

namespace TxTRPG.Content.Characters.Editor
{
    public sealed class CharacterContentAssetFactory
    {
        private readonly Action<CharacterContentCreationStep> afterStep;

        public CharacterContentAssetFactory(
            Action<CharacterContentCreationStep> afterStep = null)
        {
            this.afterStep = afterStep;
        }

        public CharacterContentCreationResult Create(
            CharacterContentCreationRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var folder = request.OutputFolder?.Replace('\\', '/').TrimEnd('/') ?? string.Empty;
            var paths = new CharacterContentAssetPaths(folder, request.AssetName?.Trim() ?? string.Empty);
            var errors = CharacterContentCreationValidator.Validate(request, paths);
            if (errors.Count > 0)
            {
                throw new ArgumentException(
                    "Character content creation failed validation:\n- " +
                    string.Join("\n- ", errors),
                    nameof(request));
            }

            var createdAssetPaths = new List<string>();
            var createdFolders = new List<string>();
            var registrations = new List<AddressableRegistrationReceipt>();
            CharacterContentDefinition content = null;
            var catalogUpdated = false;
            try
            {
                EnsureFolder(paths.Folder, createdFolders);

                var gameplay = ScriptableObject.CreateInstance<CharacterDefinition>();
                gameplay.ConfigureForEditor(request.DefinitionId, new[]
                {
                    new BaseStatEntry(CoreStatIds.AttackPower, request.AttackPower),
                    new BaseStatEntry(CoreStatIds.MaxHealth, request.MaxHealth)
                });
                AssetDatabase.CreateAsset(gameplay, paths.Gameplay);
                createdAssetPaths.Add(paths.Gameplay);
                afterStep?.Invoke(CharacterContentCreationStep.GameplayCreated);

                var statePolicy = ScriptableObject.CreateInstance<CharacterVisualStatePolicy>();
                statePolicy.ConfigureForEditor(
                    request.CriticalHealthRatio,
                    request.InjuredHealthRatio);
                AssetDatabase.CreateAsset(statePolicy, paths.VisualStatePolicy);
                createdAssetPaths.Add(paths.VisualStatePolicy);
                afterStep?.Invoke(CharacterContentCreationStep.VisualStatePolicyCreated);

                var runtimeSpriteAddress = GetRuntimeAddress(
                    request.DefaultSprite,
                    request.SpriteAddress,
                    request.RegisterSpriteWithAddressables);
                var framing = new CharacterArtworkFraming(
                    request.FramingPreset,
                    request.AdditionalScale,
                    request.PixelOffset);
                var appearance = ScriptableObject.CreateInstance<CharacterAppearanceDefinition>();
                var variants = CreateAppearanceVariants(request);
                appearance.ConfigureForEditor(
                    request.DefinitionId,
                    request.DefaultSprite,
                    runtimeSpriteAddress,
                    framing,
                    variants);
                AssetDatabase.CreateAsset(appearance, paths.Appearance);
                createdAssetPaths.Add(paths.Appearance);
                afterStep?.Invoke(CharacterContentCreationStep.AppearanceCreated);

                content = ScriptableObject.CreateInstance<CharacterContentDefinition>();
                content.ConfigureForEditor(
                    gameplay,
                    appearance,
                    statePolicy,
                    request.DisplayNameLocalizationKey);
                AssetDatabase.CreateAsset(content, paths.Content);
                createdAssetPaths.Add(paths.Content);
                afterStep?.Invoke(CharacterContentCreationStep.ContentCreated);

                Undo.RecordObject(request.Catalog, "Add Character Content");
                if (!request.Catalog.AddForEditor(content))
                {
                    throw new InvalidOperationException(
                        $"Catalog already contains character content '{request.DefinitionId}'.");
                }
                catalogUpdated = true;
                EditorUtility.SetDirty(request.Catalog);
                afterStep?.Invoke(CharacterContentCreationStep.CatalogUpdated);

                if (request.RegisterSpriteWithAddressables)
                {
                    var registrationsByAssetPath =
                        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    RegisterSprite(
                        request.DefaultSprite,
                        request.SpriteAddress,
                        request.AddressablesGroup,
                        registrationsByAssetPath,
                        registrations);
                    foreach (var variant in request.VisualVariants ??
                             new List<CharacterVisualVariantInput>())
                    {
                        RegisterSprite(
                            variant.Sprite,
                            variant.Address,
                            request.AddressablesGroup,
                            registrationsByAssetPath,
                            registrations);
                    }
                    afterStep?.Invoke(CharacterContentCreationStep.AddressablesRegistered);
                }

                if (!content.TryValidate(out var contentError))
                {
                    throw new InvalidOperationException(contentError);
                }

                EditorUtility.SetDirty(gameplay);
                EditorUtility.SetDirty(statePolicy);
                EditorUtility.SetDirty(appearance);
                EditorUtility.SetDirty(content);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(paths.Content, ImportAssetOptions.ForceUpdate);
                foreach (var registration in registrations)
                {
                    registration.Commit();
                }

                return new CharacterContentCreationResult
                {
                    GameplayDefinition = gameplay,
                    AppearanceDefinition = appearance,
                    ContentDefinition = content,
                    VisualStatePolicy = statePolicy,
                    GameplayAssetPath = paths.Gameplay,
                    AppearanceAssetPath = paths.Appearance,
                    ContentAssetPath = paths.Content,
                    VisualStatePolicyAssetPath = paths.VisualStatePolicy
                };
            }
            catch
            {
                DisposeRegistrations(registrations);
                if (catalogUpdated)
                {
                    request.Catalog.RemoveForEditor(content);
                    EditorUtility.SetDirty(request.Catalog);
                }
                for (var index = createdAssetPaths.Count - 1; index >= 0; index--)
                {
                    AssetDatabase.DeleteAsset(createdAssetPaths[index]);
                }
                for (var index = createdFolders.Count - 1; index >= 0; index--)
                {
                    AssetDatabase.DeleteAsset(createdFolders[index]);
                }
                AssetDatabase.SaveAssets();
                throw;
            }
            finally
            {
                DisposeRegistrations(registrations);
            }
        }

        private static List<CharacterAppearanceDefinition.Variant> CreateAppearanceVariants(
            CharacterContentCreationRequest request)
        {
            var variants = new List<CharacterAppearanceDefinition.Variant>(
                request.VisualVariants?.Count ?? 0);
            if (request.VisualVariants == null)
            {
                return variants;
            }

            foreach (var input in request.VisualVariants)
            {
                variants.Add(CharacterAppearanceDefinition.Variant.CreateForEditor(
                    input.AppearanceId,
                    input.VisualStateId,
                    input.PoseId,
                    input.ExpressionId,
                    input.Sprite,
                    GetRuntimeAddress(
                        input.Sprite,
                        input.Address,
                        request.RegisterSpriteWithAddressables),
                    new CharacterArtworkFraming(
                        input.FramingPreset,
                        input.AdditionalScale,
                        input.PixelOffset)));
            }
            return variants;
        }

        private static string GetRuntimeAddress(
            Sprite sprite,
            string address,
            bool registeredByFactory)
        {
            var normalizedAddress = address?.Trim() ?? string.Empty;
            return registeredByFactory
                ? $"{normalizedAddress}[{sprite.name}]"
                : normalizedAddress;
        }

        private static void RegisterSprite(
            Sprite sprite,
            string address,
            string groupName,
            IDictionary<string, string> registrationsByAssetPath,
            ICollection<AddressableRegistrationReceipt> registrations)
        {
            var assetPath = AssetDatabase.GetAssetPath(sprite);
            var normalizedAddress = address.Trim();
            if (registrationsByAssetPath.TryGetValue(assetPath, out var registeredAddress))
            {
                if (!string.Equals(
                        registeredAddress,
                        normalizedAddress,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Sprite asset '{assetPath}' requested more than one base address.");
                }
                return;
            }

            var receipt = AddressableAssetRegistration.RegisterSprite(
                sprite,
                normalizedAddress,
                groupName.Trim(),
                out _);
            registrations.Add(receipt);
            registrationsByAssetPath.Add(assetPath, normalizedAddress);
        }

        private static void DisposeRegistrations(
            IReadOnlyList<AddressableRegistrationReceipt> registrations)
        {
            for (var index = registrations.Count - 1; index >= 0; index--)
            {
                registrations[index]?.Dispose();
            }
        }

        private static void EnsureFolder(string folder, List<string> createdFolders)
        {
            var segments = folder.Split('/');
            var current = segments[0];
            for (var index = 1; index < segments.Length; index++)
            {
                var next = $"{current}/{segments[index]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                    createdFolders.Add(next);
                }
                current = next;
            }
        }
    }
}
