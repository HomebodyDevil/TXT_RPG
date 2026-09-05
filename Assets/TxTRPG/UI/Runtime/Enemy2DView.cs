using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI
{
    [Serializable]
    public sealed class EnemyAnimationBinding
    {
        [SerializeField] private string animationId = string.Empty;
        [SerializeField] private string animatorStateName = string.Empty;

        public string AnimationId => animationId;
        public string AnimatorStateName => animatorStateName;
    }

    public sealed class Enemy2DView : MonoBehaviour
    {
        [Header("Visuals")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform frameViewport;
        [SerializeField] private RectTransform visualRoot;
        [SerializeField] private RectTransform artworkRoot;
        [SerializeField] private Image baseImage;
        [SerializeField] private Image skinOverlay;
        [SerializeField] private Image effectOverlay;
        [SerializeField] private GameObject targetMarker;
        [SerializeField] private GameObject defeatedOverlay;
        [SerializeField] private Animator enemyAnimator;
        [SerializeField] private CharacterEffectPlayer effectPlayer;

        [Header("Definitions")]
        [SerializeField] private List<EnemyAppearanceDefinition> appearanceDefinitions = new();
        [SerializeField] private List<EnemyAnimationBinding> animationBindings = new();

        private IAssetProvider assetProvider;
        private CancellationTokenSource artworkCancellation;
        private AssetLease<Sprite> artworkLease;
        private Sprite currentSprite;
        private CharacterArtworkFraming currentFraming;
        private EnemyPresentation currentPresentation;
        private Task currentArtworkTask = Task.CompletedTask;

        public string InstanceId { get; private set; } = string.Empty;
        public bool IsBound => !string.IsNullOrEmpty(InstanceId);
        public bool IsTargeted { get; private set; }
        public Task WhenAssetsReady => currentArtworkTask;

        public void SetAssetProvider(IAssetProvider provider)
        {
            ReleaseArtwork();
            SetArtwork(null, default);
            assetProvider = provider;
            ReloadArtworkIfBound();
        }

        public void SetAppearanceDefinitions(IEnumerable<EnemyAppearanceDefinition> definitions)
        {
            appearanceDefinitions.Clear();
            if (definitions != null)
            {
                foreach (var definition in definitions)
                {
                    if (definition != null)
                    {
                        appearanceDefinitions.Add(definition);
                    }
                }
            }

            ReloadArtworkIfBound();
        }

        public void Bind(in EnemyPresentation presentation)
        {
            gameObject.SetActive(true);
            InstanceId = presentation.InstanceId;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
            UpdatePresentation(presentation);
        }

        public void UpdatePresentation(in EnemyPresentation presentation)
        {
            if (!string.Equals(InstanceId, presentation.InstanceId, StringComparison.Ordinal))
            {
                return;
            }

            var artworkChanged = !UsesSameArtwork(currentPresentation, presentation);
            var animationChanged = !string.Equals(
                currentPresentation.AnimationId,
                presentation.AnimationId,
                StringComparison.Ordinal);
            currentPresentation = presentation;

            if (visualRoot != null)
            {
                var scale = visualRoot.localScale;
                scale.x = Mathf.Abs(scale.x) * (presentation.Mirrored ? -1f : 1f);
                visualRoot.localScale = scale;
            }

            SetTargeted(presentation.IsTargeted);
            SetDefeated(presentation.IsDefeated);
            if (artworkChanged)
            {
                currentArtworkTask = ObserveArtworkLoadAsync(ApplyArtworkAsync(presentation));
            }
            if (animationChanged)
            {
                PlayAnimation(presentation.AnimationId);
            }
        }

        public void SetTargeted(bool targeted)
        {
            IsTargeted = targeted;
            if (targetMarker != null)
            {
                targetMarker.SetActive(targeted);
            }
        }

        public void SetDefeated(bool defeated)
        {
            if (defeatedOverlay != null)
            {
                defeatedOverlay.SetActive(defeated);
            }
        }

        public void PlayAnimation(string animationId)
        {
            if (enemyAnimator == null || string.IsNullOrWhiteSpace(animationId))
            {
                return;
            }

            var stateName = string.Empty;
            foreach (var binding in animationBindings)
            {
                if (binding != null &&
                    string.Equals(binding.AnimationId, animationId, StringComparison.Ordinal))
                {
                    stateName = binding.AnimatorStateName;
                    break;
                }
            }

            if (!string.IsNullOrWhiteSpace(stateName))
            {
                enemyAnimator.Play(stateName, 0, 0f);
            }
        }

        public void PlayEffect(string effectId) => effectPlayer?.Play(effectId);

        public void Unbind()
        {
            InstanceId = string.Empty;
            currentPresentation = default;
            ReleaseArtwork();
            ClearImage(baseImage);
            ClearImage(skinOverlay);
            ClearImage(effectOverlay);
            SetTargeted(false);
            SetDefeated(false);
            effectPlayer?.Clear();
            gameObject.SetActive(false);
        }

        public async Task ApplyArtworkAsync(
            EnemyPresentation presentation,
            CancellationToken cancellationToken = default)
        {
            var expectedInstanceId = presentation.InstanceId;
            if (!TryResolveArtwork(presentation, out var artwork))
            {
                if (string.Equals(InstanceId, expectedInstanceId, StringComparison.Ordinal))
                {
                    ReleaseArtwork();
                    SetArtwork(null, default);
                }
                return;
            }

            if (!Application.isPlaying || string.IsNullOrWhiteSpace(artwork.AssetId))
            {
                if (string.Equals(InstanceId, expectedInstanceId, StringComparison.Ordinal))
                {
                    ReleaseArtwork();
                    SetArtwork(artwork.EditorFallback, artwork.Framing);
                }
                return;
            }

            CancelArtworkRequest();
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            artworkCancellation = cancellation;
            AssetLease<Sprite> pendingLease = null;
            try
            {
                pendingLease = await (assetProvider ?? AddressablesAssetProvider.Shared)
                    .LoadAsync<Sprite>(artwork.AssetId, cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                if (!string.Equals(InstanceId, expectedInstanceId, StringComparison.Ordinal))
                {
                    return;
                }

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

        private bool TryResolveArtwork(
            in EnemyPresentation presentation,
            out EnemyArtworkReference artwork)
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
                Debug.LogWarning($"Enemy artwork could not be loaded: {exception.Message}", this);
            }
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

        private void OnRectTransformDimensionsChange() => ApplyArtworkFraming();

        private void ApplyArtworkFraming()
        {
            if (currentSprite == null || frameViewport == null || artworkRoot == null)
            {
                return;
            }

            var viewportSize = frameViewport.rect.size;
            var spriteSize = currentSprite.rect.size;
            var visibleRect = currentFraming.VisibleRect;
            if (viewportSize.x <= 0f || viewportSize.y <= 0f ||
                spriteSize.x <= 0f || spriteSize.y <= 0f)
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

        private void ReleaseArtwork()
        {
            CancelArtworkRequest();
            artworkLease?.Dispose();
            artworkLease = null;
            currentSprite = null;
        }

        private void ReloadArtworkIfBound()
        {
            if (!IsBound)
            {
                return;
            }

            currentArtworkTask = ObserveArtworkLoadAsync(ApplyArtworkAsync(currentPresentation));
        }

        private static bool UsesSameArtwork(
            in EnemyPresentation left,
            in EnemyPresentation right) =>
            string.Equals(left.EnemyId, right.EnemyId, StringComparison.Ordinal) &&
            string.Equals(left.AppearanceId, right.AppearanceId, StringComparison.Ordinal) &&
            string.Equals(left.PoseId, right.PoseId, StringComparison.Ordinal);

        private void CancelArtworkRequest()
        {
            artworkCancellation?.Cancel();
            artworkCancellation?.Dispose();
            artworkCancellation = null;
        }

        private void OnDestroy() => ReleaseArtwork();

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
