using System;
using System.Collections.Generic;
using UnityEngine;

namespace TxTRPG.UI
{
    public readonly struct EnemyArtworkReference
    {
        public EnemyArtworkReference(string assetId, Sprite editorFallback, CharacterArtworkFraming framing)
        {
            AssetId = assetId ?? string.Empty;
            EditorFallback = editorFallback;
            Framing = framing;
        }

        public string AssetId { get; }
        public Sprite EditorFallback { get; }
        public CharacterArtworkFraming Framing { get; }
    }

    [CreateAssetMenu(menuName = "TxT RPG/UI/Enemy Appearance Definition")]
    public sealed class EnemyAppearanceDefinition : ScriptableObject
    {
        [Serializable]
        public sealed class Variant
        {
            [SerializeField] private string appearanceId = string.Empty;
            [SerializeField] private string poseId = string.Empty;
#if UNITY_EDITOR
            [SerializeField] private Sprite sprite;
#endif
            [SerializeField] private string spriteAssetId;
            [SerializeField] private CharacterArtworkFraming framing;

            public string AppearanceId => appearanceId;
            public string PoseId => poseId;
#if UNITY_EDITOR
            public Sprite Sprite => sprite;
#else
            public Sprite Sprite => null;
#endif
            public string SpriteAssetId => spriteAssetId;
            public CharacterArtworkFraming Framing => framing;
        }

        [SerializeField] private string enemyId = string.Empty;
#if UNITY_EDITOR
        [SerializeField] private Sprite fallbackSprite;
#endif
        [SerializeField] private string fallbackSpriteAssetId;
        [SerializeField] private CharacterArtworkFraming fallbackFraming;
        [SerializeField] private List<Variant> variants = new();

        public string EnemyId => enemyId;

        public bool TryResolveReference(
            in EnemyPresentation presentation,
            out EnemyArtworkReference artwork)
        {
            artwork = default;
            if (!string.Equals(enemyId, presentation.EnemyId, StringComparison.Ordinal))
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
                    !Matches(variant.PoseId, presentation.PoseId))
                {
                    continue;
                }

                var score = Specificity(variant.AppearanceId) + Specificity(variant.PoseId);
                if (score > bestScore)
                {
                    best = variant;
                    bestScore = score;
                }
            }

            artwork = best != null
                ? new EnemyArtworkReference(best.SpriteAssetId, best.Sprite, best.Framing)
                : new EnemyArtworkReference(
                    fallbackSpriteAssetId,
#if UNITY_EDITOR
                    fallbackSprite,
#else
                    null,
#endif
                    fallbackFraming);
            return !string.IsNullOrWhiteSpace(artwork.AssetId) || artwork.EditorFallback != null;
        }

        private static bool Matches(string configured, string requested) =>
            string.IsNullOrWhiteSpace(configured) ||
            string.Equals(configured, requested, StringComparison.Ordinal);

        private static int Specificity(string value) => string.IsNullOrWhiteSpace(value) ? 0 : 1;
    }
}
