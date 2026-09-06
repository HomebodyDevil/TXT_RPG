using System;
using System.Collections.Generic;
using System.IO;
using TxTRPG.Editor.Common.Addressables;
using UnityEditor;
using UnityEngine;

namespace TxTRPG.Content.Characters.Editor
{
    public static class CharacterContentCreationValidator
    {
        public static IReadOnlyList<string> Validate(
            CharacterContentCreationRequest request,
            CharacterContentAssetPaths paths)
        {
            var errors = new List<string>();
            if (request == null)
            {
                errors.Add("A character creation request is required.");
                return errors;
            }

            ValidateIdentity(request, paths, errors);
            ValidateGameplay(request, errors);
            ValidatePolicy(request, errors);
            ValidateAppearance(request, errors);
            ValidateCatalog(request, errors);
            return errors;
        }

        private static void ValidateIdentity(
            CharacterContentCreationRequest request,
            CharacterContentAssetPaths paths,
            List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(request.DefinitionId))
            {
                errors.Add("Definition ID is required.");
            }
            else if (!string.Equals(
                request.DefinitionId,
                request.DefinitionId.Trim(),
                StringComparison.Ordinal))
            {
                errors.Add("Definition ID cannot have leading or trailing whitespace.");
            }

            if (string.IsNullOrWhiteSpace(request.AssetName))
            {
                errors.Add("Asset Name is required.");
            }
            else if (request.AssetName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                     request.AssetName.Contains('/') ||
                     request.AssetName.Contains('\\'))
            {
                errors.Add("Asset Name contains invalid file-name characters.");
            }

            var folder = request.OutputFolder?.Replace('\\', '/') ?? string.Empty;
            if (!folder.StartsWith("Assets/", StringComparison.Ordinal) ||
                folder.Contains("..", StringComparison.Ordinal) ||
                folder.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("Output Folder must be a folder below Assets/.");
            }

            CheckPathCollision(paths.Gameplay, errors);
            CheckPathCollision(paths.Appearance, errors);
            CheckPathCollision(paths.VisualStatePolicy, errors);
            CheckPathCollision(paths.Content, errors);

            if (!string.IsNullOrWhiteSpace(request.DefinitionId))
            {
                foreach (var guid in AssetDatabase.FindAssets("t:CharacterContentDefinition"))
                {
                    var existing = AssetDatabase.LoadAssetAtPath<CharacterContentDefinition>(
                        AssetDatabase.GUIDToAssetPath(guid));
                    if (existing != null && string.Equals(
                        existing.DefinitionId,
                        request.DefinitionId,
                        StringComparison.Ordinal))
                    {
                        errors.Add($"Definition ID '{request.DefinitionId}' already exists.");
                        break;
                    }
                }
            }
        }

        private static void ValidatePolicy(
            CharacterContentCreationRequest request,
            List<string> errors)
        {
            if (request.CriticalHealthRatio < 0f ||
                request.CriticalHealthRatio >= request.InjuredHealthRatio ||
                request.InjuredHealthRatio > 1f)
            {
                errors.Add("Health ratios must satisfy 0 <= critical < injured <= 1.");
            }
        }

        private static void ValidateGameplay(
            CharacterContentCreationRequest request,
            List<string> errors)
        {
            if (request.AttackPower < 0)
            {
                errors.Add("Attack Power must be at least 0.");
            }
            if (request.MaxHealth < 1)
            {
                errors.Add("Max Health must be at least 1.");
            }
        }

        private static void ValidateAppearance(
            CharacterContentCreationRequest request,
            List<string> errors)
        {
            if (request.AdditionalScale < 0.1f)
            {
                errors.Add("Additional Scale must be at least 0.1.");
            }
            if (request.RegisterSpriteWithAddressables &&
                string.IsNullOrWhiteSpace(request.AddressablesGroup))
            {
                errors.Add("Addressables Group is required when registration is enabled.");
            }

            var registeredPaths = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
            var usedAddresses = new Dictionary<string, Sprite>(
                StringComparer.OrdinalIgnoreCase);
            ValidateArtwork(
                request.DefaultSprite,
                request.SpriteAddress,
                "Default Sprite",
                request,
                registeredPaths,
                usedAddresses,
                errors);

            var variants = request.VisualVariants ?? new List<CharacterVisualVariantInput>();
            for (var index = 0; index < variants.Count; index++)
            {
                var variant = variants[index];
                if (variant == null)
                {
                    errors.Add($"Visual Variant {index} is null.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(variant.VisualStateId))
                {
                    errors.Add($"Visual Variant {index} requires a Visual State ID.");
                }
                if (variant.AdditionalScale < 0.1f)
                {
                    errors.Add($"Visual Variant {index} Additional Scale must be at least 0.1.");
                }
                ValidateArtwork(
                    variant.Sprite,
                    variant.Address,
                    $"Visual Variant {index} Sprite",
                    request,
                    registeredPaths,
                    usedAddresses,
                    errors);
            }

            ValidateVariantSelection(variants, errors);
        }

        private static void ValidateArtwork(
            Sprite sprite,
            string address,
            string label,
            CharacterContentCreationRequest request,
            IDictionary<string, string> registeredPaths,
            IDictionary<string, Sprite> usedAddresses,
            List<string> errors)
        {
            if (sprite == null)
            {
                errors.Add($"{label} is required.");
                return;
            }

            var assetPath = AssetDatabase.GetAssetPath(sprite);
            if (string.IsNullOrEmpty(assetPath))
            {
                errors.Add($"{label} must be a saved project asset.");
                return;
            }

            var normalizedAddress = address?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(normalizedAddress))
            {
                errors.Add($"{label} Address is required.");
                return;
            }

            if (!request.RegisterSpriteWithAddressables)
            {
                if (usedAddresses.TryGetValue(normalizedAddress, out var previousSprite) &&
                    previousSprite != sprite)
                {
                    errors.Add($"Artwork address '{normalizedAddress}' is used more than once.");
                }
                else
                {
                    usedAddresses[normalizedAddress] = sprite;
                }
                return;
            }

            if (registeredPaths.TryGetValue(assetPath, out var previousAddress) &&
                !string.Equals(previousAddress, normalizedAddress, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(
                    $"Sprites from '{assetPath}' must share one Addressables base address.");
                return;
            }
            registeredPaths[assetPath] = normalizedAddress;

            if (usedAddresses.TryGetValue(normalizedAddress, out var previous) &&
                AssetDatabase.GetAssetPath(previous) != assetPath)
            {
                errors.Add($"Addressables address '{normalizedAddress}' is used by multiple assets.");
                return;
            }
            usedAddresses[normalizedAddress] = sprite;

            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (AddressableAssetRegistration.TryFindAddress(
                    normalizedAddress,
                    out var duplicate) &&
                !string.Equals(duplicate.guid, guid, StringComparison.Ordinal))
            {
                errors.Add($"Addressables address '{normalizedAddress}' is already in use.");
            }
            if (AddressableAssetRegistration.TryFindAsset(assetPath, out var existing) &&
                !string.Equals(
                    existing.address,
                    normalizedAddress,
                    StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"{label} is already registered as '{existing.address}'.");
            }
        }

        private static void ValidateVariantSelection(
            IReadOnlyList<CharacterVisualVariantInput> variants,
            List<string> errors)
        {
            for (var leftIndex = 0; leftIndex < variants.Count; leftIndex++)
            {
                var left = variants[leftIndex];
                if (left == null)
                {
                    continue;
                }
                for (var rightIndex = leftIndex + 1; rightIndex < variants.Count; rightIndex++)
                {
                    var right = variants[rightIndex];
                    if (right == null || Specificity(left) != Specificity(right) ||
                        !Overlaps(left.AppearanceId, right.AppearanceId) ||
                        !Overlaps(left.VisualStateId, right.VisualStateId) ||
                        !Overlaps(left.PoseId, right.PoseId) ||
                        !Overlaps(left.ExpressionId, right.ExpressionId))
                    {
                        continue;
                    }
                    errors.Add(
                        $"Visual Variants {leftIndex} and {rightIndex} overlap with equal specificity.");
                }
            }
        }

        private static int Specificity(CharacterVisualVariantInput variant) =>
            Specificity(variant.AppearanceId) + Specificity(variant.VisualStateId) +
            Specificity(variant.PoseId) + Specificity(variant.ExpressionId);

        private static int Specificity(string value) =>
            string.IsNullOrWhiteSpace(value) ? 0 : 1;

        private static bool Overlaps(string left, string right) =>
            string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right) ||
            string.Equals(left.Trim(), right.Trim(), StringComparison.Ordinal);

        private static void ValidateCatalog(
            CharacterContentCreationRequest request,
            List<string> errors)
        {
            if (request.Catalog == null)
            {
                errors.Add("Character Content Catalog is required.");
            }
            else if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(request.Catalog)))
            {
                errors.Add("Character Content Catalog must be a saved project asset.");
            }
        }

        private static void CheckPathCollision(string path, List<string> errors)
        {
            if (!string.IsNullOrWhiteSpace(path) &&
                AssetDatabase.LoadMainAssetAtPath(path) != null)
            {
                errors.Add($"Asset path already exists: {path}");
            }
        }
    }
}
