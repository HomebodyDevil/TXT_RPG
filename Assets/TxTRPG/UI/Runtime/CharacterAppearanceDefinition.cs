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

    [CreateAssetMenu(menuName = "TxT RPG/UI/Character Appearance Definition")]
    public sealed class CharacterAppearanceDefinition : ScriptableObject
    {
        [Serializable]
        public sealed class Variant
        {
            [SerializeField] private string appearanceId = string.Empty;
            [SerializeField] private string poseId = string.Empty;
            [SerializeField] private string expressionId = string.Empty;
            [SerializeField] private Sprite sprite;
            [SerializeField] private CharacterArtworkFraming framing;

            public string AppearanceId => appearanceId;
            public string PoseId => poseId;
            public string ExpressionId => expressionId;
            public Sprite Sprite => sprite;
            public CharacterArtworkFraming Framing => framing;
        }

        [SerializeField] private string characterId = string.Empty;
        [SerializeField] private Sprite fallbackSprite;
        [SerializeField] private CharacterArtworkFraming fallbackFraming;
        [SerializeField] private List<Variant> variants = new();

        public string CharacterId => characterId;

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
                sprite = fallbackSprite;
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
