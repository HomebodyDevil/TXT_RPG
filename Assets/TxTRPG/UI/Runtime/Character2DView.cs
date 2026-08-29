using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI
{
    public sealed class Character2DView : CharacterViewBase
    {
        [Header("Visuals")]
        [SerializeField] private RectTransform frameViewport;
        [SerializeField] private RectTransform visualRoot;
        [SerializeField] private RectTransform artworkRoot;
        [SerializeField] private Image baseImage;
        [SerializeField] private Image skinOverlay;
        [SerializeField] private Image effectOverlay;
        [SerializeField] private Animator characterAnimator;
        [SerializeField] private CharacterEffectPlayer effectPlayer;

        [Header("Appearances")]
        [SerializeField] private List<CharacterAppearanceDefinition> appearanceDefinitions = new();

        private Sprite currentSprite;
        private CharacterArtworkFraming currentFraming;

        public void SetAppearanceDefinitions(IEnumerable<CharacterAppearanceDefinition> definitions)
        {
            appearanceDefinitions.Clear();
            if (definitions == null)
            {
                return;
            }

            foreach (var definition in definitions)
            {
                if (definition != null)
                {
                    appearanceDefinitions.Add(definition);
                }
            }
        }

        protected override void ApplyPresentation(in CharacterPresentation presentation)
        {
            var resolved = TryResolveSprite(presentation, out var sprite, out var framing);
            if (baseImage != null)
            {
                baseImage.sprite = sprite;
                baseImage.enabled = resolved;
                baseImage.preserveAspect = true;
            }

            currentSprite = sprite;
            currentFraming = framing;
            ApplyArtworkFraming();

            if (visualRoot != null)
            {
                var scale = visualRoot.localScale;
                scale.x = Mathf.Abs(scale.x) * (presentation.Mirrored ? -1f : 1f);
                visualRoot.localScale = scale;
            }

            PlayAnimation(presentation.AnimationId);
        }

        public override void PlayAnimation(string animationId)
        {
            if (characterAnimator != null && !string.IsNullOrWhiteSpace(animationId))
            {
                characterAnimator.Play(animationId, 0, 0f);
            }
        }

        public override void PlayEffect(string effectId)
        {
            effectPlayer?.Play(effectId);
        }

        protected override void ClearPresentation()
        {
            ClearImage(baseImage);
            ClearImage(skinOverlay);
            ClearImage(effectOverlay);
            currentSprite = null;
            effectPlayer?.Clear();
        }

        private bool TryResolveSprite(
            in CharacterPresentation presentation,
            out Sprite sprite,
            out CharacterArtworkFraming framing)
        {
            foreach (var definition in appearanceDefinitions)
            {
                if (definition != null && definition.TryResolve(presentation, out sprite, out framing))
                {
                    return true;
                }
            }

            sprite = null;
            framing = default;
            return false;
        }

        private void OnRectTransformDimensionsChange()
        {
            ApplyArtworkFraming();
        }

        private void ApplyArtworkFraming()
        {
            if (currentSprite == null || frameViewport == null || artworkRoot == null)
            {
                return;
            }

            var viewportSize = frameViewport.rect.size;
            var spriteSize = currentSprite.rect.size;
            var visibleRect = currentFraming.VisibleRect;
            if (viewportSize.x <= 0f || viewportSize.y <= 0f || spriteSize.x <= 0f || spriteSize.y <= 0f)
            {
                return;
            }

            var scaleToCover = Mathf.Max(
                viewportSize.x / (spriteSize.x * visibleRect.width),
                viewportSize.y / (spriteSize.y * visibleRect.height));
            var artworkSize = spriteSize * scaleToCover * currentFraming.AdditionalScale;
            var visibleCenter = visibleRect.center;

            artworkRoot.anchorMin = new Vector2(0.5f, 0.5f);
            artworkRoot.anchorMax = new Vector2(0.5f, 0.5f);
            artworkRoot.pivot = new Vector2(0.5f, 0.5f);
            artworkRoot.sizeDelta = artworkSize;
            artworkRoot.anchoredPosition = new Vector2(
                -(visibleCenter.x - 0.5f) * artworkSize.x,
                -(visibleCenter.y - 0.5f) * artworkSize.y) + currentFraming.PixelOffset;
        }

        private static void ClearImage(Image image)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = null;
            image.enabled = false;
        }
    }
}
