using System.Collections.Generic;
using System;
using System.Threading;
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
        private IAssetProvider assetProvider;
        private CancellationTokenSource artworkCancellation;
        private AssetLease<Sprite> artworkLease;

        public void SetAssetProvider(IAssetProvider provider)
        {
            ReleaseArtwork();
            assetProvider = provider;
        }

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
            ApplyArtworkAsync(presentation);

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
            ReleaseArtwork();
            ClearImage(baseImage);
            ClearImage(skinOverlay);
            ClearImage(effectOverlay);
            currentSprite = null;
            effectPlayer?.Clear();
        }

        private async void ApplyArtworkAsync(CharacterPresentation presentation)
        {
            if (!TryResolveArtwork(presentation, out var artwork))
            {
                SetArtwork(null, default);
                return;
            }

            if (!Application.isPlaying || string.IsNullOrWhiteSpace(artwork.AssetId))
            {
                SetArtwork(artwork.EditorFallback, artwork.Framing);
                return;
            }

            artworkCancellation?.Cancel();
            artworkCancellation?.Dispose();
            var cancellation = new CancellationTokenSource();
            artworkCancellation = cancellation;
            try
            {
                var lease = await (assetProvider ?? AddressablesAssetProvider.Shared)
                    .LoadAsync<Sprite>(artwork.AssetId, cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                artworkLease?.Dispose();
                artworkLease = lease;
                SetArtwork(lease.Asset, artwork.Framing);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Character artwork '{artwork.AssetId}' could not be loaded: {exception.Message}", this);
            }
        }

        private bool TryResolveArtwork(
            in CharacterPresentation presentation,
            out CharacterArtworkReference artwork)
        {
            foreach (var definition in appearanceDefinitions)
            {
                if (definition != null && definition.TryResolveReference(presentation, out artwork))
                {
                    return true;
                }
            }

            artwork = default;
            return false;
        }

        private void SetArtwork(Sprite sprite, CharacterArtworkFraming framing)
        {
            if (baseImage != null)
            {
                baseImage.sprite = sprite;
                baseImage.enabled = sprite != null;
                baseImage.preserveAspect = true;
            }

            currentSprite = sprite;
            currentFraming = framing;
            ApplyArtworkFraming();
        }

        private void ReleaseArtwork()
        {
            artworkCancellation?.Cancel();
            artworkCancellation?.Dispose();
            artworkCancellation = null;
            artworkLease?.Dispose();
            artworkLease = null;
        }

        private void OnDestroy()
        {
            ReleaseArtwork();
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
