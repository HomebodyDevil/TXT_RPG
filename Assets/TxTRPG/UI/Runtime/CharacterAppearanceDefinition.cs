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
            [SerializeField] private string poseId = string.Empty;
            [SerializeField] private string expressionId = string.Empty;
#if UNITY_EDITOR
            [SerializeField] private Sprite sprite;
#endif
            [SerializeField] private string spriteAssetId;
            [SerializeField] private CharacterArtworkFraming framing;

            public string AppearanceId => appearanceId;
            public string PoseId => poseId;
            public string ExpressionId => expressionId;
#if UNITY_EDITOR
            public Sprite Sprite => sprite;
#else
            public Sprite Sprite => null;
#endif
            public string SpriteAssetId => spriteAssetId;
            public CharacterArtworkFraming Framing => framing;
        }

        [SerializeField] private string characterId = string.Empty;
#if UNITY_EDITOR
        [SerializeField] private Sprite fallbackSprite;
#endif
        [SerializeField] private string fallbackSpriteAssetId;
        [SerializeField] private CharacterArtworkFraming fallbackFraming;
        [SerializeField] private List<Variant> variants = new();

        public string CharacterId => characterId;

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
                    !Matches(variant.PoseId, presentation.PoseId) ||
                    !Matches(variant.ExpressionId, presentation.ExpressionId))
                {
                    continue;
                }

                var score = Specificity(variant.AppearanceId) + Specificity(variant.PoseId) +
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
                    !Matches(variant.PoseId, presentation.PoseId) ||
                    !Matches(variant.ExpressionId, presentation.ExpressionId))
                {
                    continue;
                }

                var score = Specificity(variant.AppearanceId) +
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

        private static bool Matches(string configuredId, string requestedId)
        {
            return string.IsNullOrEmpty(configuredId) ||
                   string.Equals(configuredId, requestedId, StringComparison.Ordinal);
        }

        private static int Specificity(string id)
        {
            return string.IsNullOrEmpty(id) ? 0 : 1;
        }
    }
}
