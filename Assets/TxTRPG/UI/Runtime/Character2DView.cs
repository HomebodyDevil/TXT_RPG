using System.Collections.Generic;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI
{
    public sealed class Character2DView : CharacterViewBase,
        ICharacterAppearanceDefinitionReceiver,
        ICharacterAssetReadySource
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
        private Task currentArtworkTask = Task.CompletedTask;
        private CharacterPresentation currentPresentation;
        private bool hasPresentation;
        public Task WhenAssetsReady => currentArtworkTask;

        public void SetAssetProvider(IAssetProvider provider)
        {
            ReleaseArtwork();
            assetProvider = provider;
            ReloadArtworkIfBound();
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
            ReloadArtworkIfBound();
        }

        protected override void ApplyPresentation(in CharacterPresentation presentation)
        {
            var artworkChanged = !hasPresentation ||
                !UsesSameArtwork(currentPresentation, presentation);
            var animationChanged = !hasPresentation ||
                !string.Equals(
                    currentPresentation.AnimationId,
                    presentation.AnimationId,
                    StringComparison.Ordinal);
            currentPresentation = presentation;
            hasPresentation = true;

            if (artworkChanged)
            {
                currentArtworkTask = ObserveArtworkLoadAsync(ApplyArtworkAsync(presentation));
            }

            if (visualRoot != null)
            {
                var scale = visualRoot.localScale;
                scale.x = Mathf.Abs(scale.x) * (presentation.Mirrored ? -1f : 1f);
                visualRoot.localScale = scale;
            }

            if (animationChanged)
            {
                PlayAnimation(presentation.AnimationId);
            }
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
            currentPresentation = default;
            hasPresentation = false;
            effectPlayer?.Clear();
        }

        public async Task ApplyArtworkAsync(
            CharacterPresentation presentation,
            CancellationToken cancellationToken = default)
        {
            CancelArtworkRequest();
            if (!TryResolveArtwork(presentation, out var artwork))
            {
                ReleaseArtworkLease();
                SetArtwork(null, default);
                return;
            }

            if (!Application.isPlaying || string.IsNullOrWhiteSpace(artwork.AssetId))
            {
                ReleaseArtworkLease();
                SetArtwork(artwork.EditorFallback, artwork.Framing);
                return;
            }

            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            artworkCancellation = cancellation;
            AssetLease<Sprite> pendingLease = null;
            try
            {
                pendingLease = await (assetProvider ?? AddressablesAssetProvider.Shared)
                    .LoadAsync<Sprite>(artwork.AssetId, cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();

                var previousLease = artworkLease;
                artworkLease = pendingLease;
                pendingLease = null;
                SetArtwork(artworkLease.Asset, artwork.Framing);
                previousLease?.Dispose();
            }
            finally
            {
                pendingLease?.Dispose();
                if (ReferenceEquals(artworkCancellation, cancellation))
                {
                    artworkCancellation = null;
                }
                cancellation.Dispose();
            }
        }

        private async Task ObserveArtworkLoadAsync(Task loadTask)
        {
            try
            {
                await loadTask;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Character artwork could not be loaded: {exception.Message}", this);
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
            CancelArtworkRequest();
            ClearImage(baseImage);
            artworkLease?.Dispose();
            artworkLease = null;
        }

        private void ReleaseArtworkLease()
        {
            artworkLease?.Dispose();
            artworkLease = null;
        }

        private void ReloadArtworkIfBound()
        {
            if (!hasPresentation)
            {
                return;
            }
            currentArtworkTask = ObserveArtworkLoadAsync(
                ApplyArtworkAsync(currentPresentation));
        }

        private static bool UsesSameArtwork(
            in CharacterPresentation left,
            in CharacterPresentation right) =>
            string.Equals(left.CharacterId, right.CharacterId, StringComparison.Ordinal) &&
            string.Equals(left.AppearanceId, right.AppearanceId, StringComparison.Ordinal) &&
            string.Equals(left.VisualStateId, right.VisualStateId, StringComparison.Ordinal) &&
            string.Equals(left.PoseId, right.PoseId, StringComparison.Ordinal) &&
            string.Equals(left.ExpressionId, right.ExpressionId, StringComparison.Ordinal);

        private void CancelArtworkRequest()
        {
            artworkCancellation?.Cancel();
            artworkCancellation?.Dispose();
            artworkCancellation = null;
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
