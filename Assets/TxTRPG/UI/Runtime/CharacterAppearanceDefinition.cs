using System;
using System.Collections.Generic;
using UnityEngine;

namespace TxTRPG.UI
{
    public enum CharacterFramingPreset
    {
        WholeArtwork,
        ThighUp,
        Custom
    }

    [Serializable]
    public struct CharacterArtworkFraming
    {
        [SerializeField] private CharacterFramingPreset preset;
        [SerializeField] private Rect customVisibleRect;
        [SerializeField, Min(0.1f)] private float additionalScale;
        [SerializeField] private Vector2 pixelOffset;

        public CharacterArtworkFraming(
            CharacterFramingPreset preset,
            float additionalScale = 1f,
            Vector2 pixelOffset = default,
            Rect customVisibleRect = default)
        {
            this.preset = preset;
            this.customVisibleRect = customVisibleRect;
            this.additionalScale = Mathf.Max(0.1f, additionalScale);
            this.pixelOffset = pixelOffset;
        }

        public Rect VisibleRect
        {
            get
            {
                return preset switch
                {
                    CharacterFramingPreset.ThighUp => new Rect(0f, 0.22f, 1f, 0.78f),
                    CharacterFramingPreset.Custom => Sanitize(customVisibleRect),
                    _ => new Rect(0f, 0f, 1f, 1f)
                };
            }
        }

        public float AdditionalScale => additionalScale > 0f ? additionalScale : 1f;
        public Vector2 PixelOffset => pixelOffset;

        private static Rect Sanitize(Rect rect)
        {
            const float minimumSize = 0.01f;
            var width = Mathf.Clamp(rect.width, minimumSize, 1f);
            var height = Mathf.Clamp(rect.height, minimumSize, 1f);
            var x = Mathf.Clamp(rect.x, 0f, 1f - width);
            var y = Mathf.Clamp(rect.y, 0f, 1f - height);
            return new Rect(x, y, width, height);
        }
    }

    public readonly struct CharacterArtworkReference
    {
        public CharacterArtworkReference(string assetId, Sprite editorFallback, CharacterArtworkFraming framing)
        {
            AssetId = assetId ?? string.Empty;
            EditorFallback = editorFallback;
            Framing = framing;
        }

        public string AssetId { get; }
        public Sprite EditorFallback { get; }
        public CharacterArtworkFraming Framing { get; }
    }

    [CreateAssetMenu(menuName = "TxT RPG/UI/Character Appearance Definition")]
    public sealed class CharacterAppearanceDefinition : ScriptableObject
    {
        [Serializable]
        public sealed class Variant
        {
            [SerializeField] private string appearanceId = string.Empty;
            [SerializeField] private string visualStateId = string.Empty;
            [SerializeField] private string poseId = string.Empty;
            [SerializeField] private string expressionId = string.Empty;
#if UNITY_EDITOR
            [SerializeField] private Sprite sprite;
#endif
            [SerializeField] private string spriteAssetId;
            [SerializeField] private CharacterArtworkFraming framing;

            public string AppearanceId => appearanceId;
            public string VisualStateId => visualStateId;
            public string PoseId => poseId;
            public string ExpressionId => expressionId;
#if UNITY_EDITOR
            public Sprite Sprite => sprite;
#else
            public Sprite Sprite => null;
#endif
            public string SpriteAssetId => spriteAssetId;
            public CharacterArtworkFraming Framing => framing;

#if UNITY_EDITOR
            public static Variant CreateForEditor(
                string configuredAppearanceId,
                string configuredVisualStateId,
                string configuredPoseId,
                string configuredExpressionId,
                Sprite configuredSprite,
                string configuredSpriteAssetId,
                CharacterArtworkFraming configuredFraming)
            {
                return new Variant
                {
                    appearanceId = configuredAppearanceId?.Trim() ?? string.Empty,
                    visualStateId = configuredVisualStateId?.Trim() ?? string.Empty,
                    poseId = configuredPoseId?.Trim() ?? string.Empty,
                    expressionId = configuredExpressionId?.Trim() ?? string.Empty,
                    sprite = configuredSprite,
                    spriteAssetId = configuredSpriteAssetId?.Trim() ?? string.Empty,
                    framing = configuredFraming
                };
            }
#endif
        }

        [SerializeField] private string characterId = string.Empty;
#if UNITY_EDITOR
        [SerializeField] private Sprite fallbackSprite;
#endif
        [SerializeField] private string fallbackSpriteAssetId;
        [SerializeField] private CharacterArtworkFraming fallbackFraming;
        [SerializeField] private List<Variant> variants = new();

        public string CharacterId => characterId;

#if UNITY_EDITOR
        public void ConfigureForEditor(
            string definitionId,
            Sprite defaultSprite,
            string defaultSpriteAssetId,
            CharacterArtworkFraming framing)
        {
            characterId = definitionId?.Trim() ?? string.Empty;
            fallbackSprite = defaultSprite;
            fallbackSpriteAssetId = defaultSpriteAssetId?.Trim() ?? string.Empty;
            fallbackFraming = framing;
            variants ??= new List<Variant>();
        }

        public void ConfigureForEditor(
            string definitionId,
            Sprite defaultSprite,
            string defaultSpriteAssetId,
            CharacterArtworkFraming framing,
            IEnumerable<Variant> configuredVariants)
        {
            ConfigureForEditor(definitionId, defaultSprite, defaultSpriteAssetId, framing);
            variants = configuredVariants == null
                ? new List<Variant>()
                : new List<Variant>(configuredVariants);
        }
#endif

        public bool TryValidateAddressableReferences(out string error)
        {
            if (variants == null)
            {
                error = $"Character appearance '{name}' has a null variant collection.";
                return false;
            }

            if (!TryValidateVariantSelection(out error))
            {
                return false;
            }

            for (var index = 0; index < variants.Count; index++)
            {
                var variant = variants[index];
                if (variant == null)
                {
                    error = $"Character appearance '{name}' has a null variant at index {index}.";
                    return false;
                }
                if (string.IsNullOrWhiteSpace(variant.SpriteAssetId))
                {
                    error = $"Character appearance '{name}' variant {index} has no " +
                            "Addressable artwork ID.";
                    return false;
                }
            }

            var defaultPresentation = new CharacterPresentation(characterId);
            if (!TryResolveReference(defaultPresentation, out var artwork) ||
                string.IsNullOrWhiteSpace(artwork.AssetId))
            {
                error = $"Character appearance '{name}' has no default Addressable artwork ID.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool TryResolveReference(
            in CharacterPresentation presentation,
            out CharacterArtworkReference artwork)
        {
            artwork = default;
            if (!string.Equals(characterId, presentation.CharacterId, StringComparison.Ordinal))
            {
                return false;
            }

            Variant best = null;
            var bestScore = -1;
            foreach (var variant in variants)
            {
                if (variant == null ||
                    (string.IsNullOrWhiteSpace(variant.SpriteAssetId) && variant.Sprite == null) ||
                    !Matches(variant.AppearanceId, presentation.AppearanceId) ||
                    !Matches(variant.VisualStateId, presentation.VisualStateId) ||
                    !Matches(variant.PoseId, presentation.PoseId) ||
                    !Matches(variant.ExpressionId, presentation.ExpressionId))
                {
                    continue;
                }

                var score = Specificity(variant.AppearanceId) +
                            Specificity(variant.VisualStateId) + Specificity(variant.PoseId) +
                            Specificity(variant.ExpressionId);
                if (score > bestScore)
                {
                    best = variant;
                    bestScore = score;
                }
            }

            artwork = best != null
                ? new CharacterArtworkReference(best.SpriteAssetId, best.Sprite, best.Framing)
                : new CharacterArtworkReference(
                    fallbackSpriteAssetId,
#if UNITY_EDITOR
                    fallbackSprite,
#else
                    null,
#endif
                    fallbackFraming);
            return !string.IsNullOrWhiteSpace(artwork.AssetId) || artwork.EditorFallback != null;
        }

        public bool TryResolve(in CharacterPresentation presentation, out Sprite sprite)
        {
            var resolved = TryResolve(presentation, out sprite, out _);
            return resolved;
        }

        public bool TryResolve(
            in CharacterPresentation presentation,
            out Sprite sprite,
            out CharacterArtworkFraming framing)
        {
            sprite = null;
            framing = fallbackFraming;
            if (!string.Equals(characterId, presentation.CharacterId, StringComparison.Ordinal))
            {
                return false;
            }

            var bestScore = -1;
            foreach (var variant in variants)
            {
                if (variant == null || variant.Sprite == null ||
                    !Matches(variant.AppearanceId, presentation.AppearanceId) ||
                    !Matches(variant.VisualStateId, presentation.VisualStateId) ||
                    !Matches(variant.PoseId, presentation.PoseId) ||
                    !Matches(variant.ExpressionId, presentation.ExpressionId))
                {
                    continue;
                }

                var score = Specificity(variant.AppearanceId) +
                            Specificity(variant.VisualStateId) +
                            Specificity(variant.PoseId) +
                            Specificity(variant.ExpressionId);
                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                sprite = variant.Sprite;
                framing = variant.Framing;
            }

            if (sprite == null)
            {
#if UNITY_EDITOR
                sprite = fallbackSprite;
#endif
            }

            return sprite != null;
        }

        public bool TryValidateVariantSelection(out string error)
        {
            if (variants == null)
            {
                error = $"Character appearance '{name}' has a null variant collection.";
                return false;
            }

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

                    error = $"Character appearance '{name}' variants {leftIndex} and " +
                            $"{rightIndex} overlap with equal specificity.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private static bool Matches(string configuredId, string requestedId)
        {
            return string.IsNullOrEmpty(configuredId) ||
                   string.Equals(configuredId, requestedId, StringComparison.Ordinal);
        }

        private static int Specificity(string id)
        {
            return string.IsNullOrEmpty(id) ? 0 : 1;
        }

        private static int Specificity(Variant variant) =>
            Specificity(variant.AppearanceId) + Specificity(variant.VisualStateId) +
            Specificity(variant.PoseId) + Specificity(variant.ExpressionId);

        private static bool Overlaps(string left, string right) =>
            string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right) ||
            string.Equals(left, right, StringComparison.Ordinal);
    }
}
