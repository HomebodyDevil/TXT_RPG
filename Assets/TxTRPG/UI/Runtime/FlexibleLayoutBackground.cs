using System.Collections;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI
{
    [DisallowMultipleComponent]
    public sealed class FlexibleLayoutBackground : MonoBehaviour
    {
        [SerializeField] private RectTransform visualRoot;
        [SerializeField] private Image backgroundA;
        [SerializeField] private Image backgroundB;
        [SerializeField] private Image effectOverlay;
        [SerializeField] private RectMask2D clipMask;
        [SerializeField] private FlexibleLayoutBackgroundStyle initialStyle;
        [SerializeField, Min(0f)] private float defaultTransitionDuration = 0.25f;
        [SerializeField] private bool useUnscaledTime = true;
        [SerializeField] private bool effectsEnabled = true;

        private Image activeBackground;
        private Coroutine transition;
        private Material backgroundAInstance;
        private Material backgroundBInstance;
        private IAssetProvider assetProvider;
        private CancellationTokenSource assetCancellation;
        private AssetScope loadedAssets;

        public bool IsVisible => gameObject.activeSelf;
        public FlexibleLayoutBackgroundStyle CurrentStyle { get; private set; }

        private void Awake()
        {
            InitializeSlots();
            if (initialStyle != null)
            {
                ApplyStyle(initialStyle);
            }
        }

        private void OnDisable()
        {
            CancelTransition();
            ReleaseLoadedAssets();
        }

        private void OnEnable()
        {
            if (Application.isPlaying && CurrentStyle != null && CurrentStyle.HasAddressableAssets &&
                loadedAssets == null && assetCancellation == null)
            {
                LoadAndApplyAsync(CurrentStyle, -1f);
            }
        }

        private void OnDestroy()
        {
            ReleaseMaterialInstances();
            ReleaseLoadedAssets();
        }

        public void SetAssetProvider(IAssetProvider provider)
        {
            ReleaseLoadedAssets();
            assetProvider = provider;
        }

        public void ApplyStyle(FlexibleLayoutBackgroundStyle style)
        {
            if (Application.isPlaying && style != null && style.HasAddressableAssets)
            {
                LoadAndApplyAsync(style, -1f);
                return;
            }

            ApplyResolvedStyle(style, style?.Sprite, style?.Material, style?.EffectSprite, style?.EffectMaterial, -1f);
        }

        private void ApplyResolvedStyle(
            FlexibleLayoutBackgroundStyle style,
            Sprite sprite,
            Material material,
            Sprite effectSprite,
            Material effectMaterial,
            float transitionDuration)
        {
            CancelTransition();
            InitializeSlots();
            CurrentStyle = style;
            if (style == null)
            {
                Clear();
                return;
            }

            if (transitionDuration >= 0f && isActiveAndEnabled)
            {
                transition = StartCoroutine(CrossFade(
                    style, sprite, material, effectSprite, effectMaterial, transitionDuration));
                return;
            }

            ConfigureImage(activeBackground, style, sprite, material);
            SetImageAlpha(activeBackground, style.Opacity);
            SetImageAlpha(GetInactiveBackground(), 0f);
            ApplyEffect(style, effectSprite, effectMaterial);
            ApplyOverflow(style.OverflowMode);
            gameObject.SetActive(true);
        }

        public void Change(FlexibleLayoutBackgroundStyle style, float duration = -1f)
        {
            InitializeSlots();
            var actualDuration = duration >= 0f ? duration : defaultTransitionDuration;
            if (Application.isPlaying && style != null && style.HasAddressableAssets)
            {
                LoadAndApplyAsync(style, actualDuration);
                return;
            }
            if (!isActiveAndEnabled || actualDuration <= 0f || style == null)
            {
                ApplyStyle(style);
                return;
            }

            CancelTransition();
            transition = StartCoroutine(CrossFade(
                style, style.Sprite, style.Material, style.EffectSprite, style.EffectMaterial, actualDuration));
        }

        public void SetSprite(Sprite sprite)
        {
            InitializeSlots();
            activeBackground.sprite = sprite;
            activeBackground.enabled = sprite != null;
            UpdateAspect(activeBackground);
        }

        public void SetColor(Color color)
        {
            InitializeSlots();
            activeBackground.color = color;
        }

        public void SetMaterial(Material material)
        {
            InitializeSlots();
            ReleaseMaterialInstance(activeBackground);
            activeBackground.material = material;
        }

        public void SetEffectsEnabled(bool enabled)
        {
            effectsEnabled = enabled;
            if (effectOverlay != null)
            {
                effectOverlay.gameObject.SetActive(enabled && effectOverlay.sprite != null);
            }
        }

        public void Clear()
        {
            CancelTransition();
            CurrentStyle = null;
            ReleaseLoadedAssets();
            ClearImage(backgroundA);
            ClearImage(backgroundB);
            ClearImage(effectOverlay);
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private IEnumerator CrossFade(
            FlexibleLayoutBackgroundStyle style,
            Sprite sprite,
            Material material,
            Sprite effectSprite,
            Material effectMaterial,
            float duration)
        {
            var previous = activeBackground;
            var next = GetInactiveBackground();
            ConfigureImage(next, style, sprite, material);
            SetImageAlpha(next, 0f);
            next.gameObject.SetActive(true);
            ApplyEffect(style, effectSprite, effectMaterial);
            ApplyOverflow(style.OverflowMode);

            var previousAlpha = previous != null ? previous.color.a : 0f;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                var progress = Mathf.Clamp01(elapsed / duration);
                SetImageAlpha(previous, Mathf.Lerp(previousAlpha, 0f, progress));
                SetImageAlpha(next, Mathf.Lerp(0f, style.Opacity, progress));
                yield return null;
                var delta = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                elapsed += Mathf.Min(Mathf.Max(0f, delta), 0.05f);
            }

            SetImageAlpha(previous, 0f);
            SetImageAlpha(next, style.Opacity);
            activeBackground = next;
            CurrentStyle = style;
            transition = null;
        }

        private void InitializeSlots()
        {
            activeBackground ??= backgroundA != null ? backgroundA : backgroundB;
            ConfigureRaycast(backgroundA);
            ConfigureRaycast(backgroundB);
            ConfigureRaycast(effectOverlay);
        }

        private Image GetInactiveBackground()
        {
            return activeBackground == backgroundA ? backgroundB : backgroundA;
        }

        private void ConfigureImage(
            Image image,
            FlexibleLayoutBackgroundStyle style,
            Sprite sprite,
            Material material)
        {
            if (image == null)
            {
                return;
            }

            image.gameObject.SetActive(true);
            image.sprite = sprite;
            image.color = new Color(style.Tint.r, style.Tint.g, style.Tint.b, style.Opacity);
            image.enabled = sprite != null || style.Tint.a > 0f;
            ApplyScaleMode(image, style.ScaleMode);
            ApplyMaterial(image, material, style.MaterialMode);
            ConfigureRaycast(image);
        }

        private void ApplyEffect(
            FlexibleLayoutBackgroundStyle style,
            Sprite sprite,
            Material material)
        {
            if (effectOverlay == null)
            {
                return;
            }

            effectOverlay.sprite = sprite;
            effectOverlay.color = style.EffectTint;
            effectOverlay.material = material;
            effectOverlay.gameObject.SetActive(effectsEnabled && sprite != null);
            ConfigureRaycast(effectOverlay);
        }

        private void ApplyOverflow(FlexibleLayoutBackgroundOverflowMode mode)
        {
            if (clipMask != null)
            {
                clipMask.enabled = mode == FlexibleLayoutBackgroundOverflowMode.ClipToPanel;
            }
        }

        private void ApplyMaterial(
            Image image,
            Material source,
            FlexibleLayoutMaterialMode materialMode)
        {
            ReleaseMaterialInstance(image);
            if (source == null || materialMode == FlexibleLayoutMaterialMode.Shared)
            {
                image.material = source;
                return;
            }

            var instance = new Material(source)
            {
                name = source.name + " (Flexible Layout Instance)"
            };
            image.material = instance;
            if (image == backgroundA)
            {
                backgroundAInstance = instance;
            }
            else if (image == backgroundB)
            {
                backgroundBInstance = instance;
            }
        }

        private static void ApplyScaleMode(Image image, FlexibleLayoutBackgroundScaleMode mode)
        {
            image.preserveAspect = mode == FlexibleLayoutBackgroundScaleMode.Fit ||
                                   mode == FlexibleLayoutBackgroundScaleMode.Fill;
            image.type = mode switch
            {
                FlexibleLayoutBackgroundScaleMode.Tile => Image.Type.Tiled,
                FlexibleLayoutBackgroundScaleMode.Sliced => Image.Type.Sliced,
                _ => Image.Type.Simple
            };

            var fitter = image.GetComponent<AspectRatioFitter>();
            if (mode == FlexibleLayoutBackgroundScaleMode.Fill)
            {
                fitter ??= image.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.enabled = true;
                UpdateAspect(image);
            }
            else if (fitter != null)
            {
                fitter.enabled = false;
            }
        }

        private static void UpdateAspect(Image image)
        {
            var fitter = image != null ? image.GetComponent<AspectRatioFitter>() : null;
            var sprite = image != null ? image.sprite : null;
            if (fitter != null && sprite != null && sprite.rect.height > 0f)
            {
                fitter.aspectRatio = sprite.rect.width / sprite.rect.height;
            }
        }

        private static void ConfigureRaycast(Image image)
        {
            if (image != null)
            {
                image.raycastTarget = false;
            }
        }

        private static void SetImageAlpha(Image image, float alpha)
        {
            if (image == null)
            {
                return;
            }

            var color = image.color;
            color.a = Mathf.Clamp01(alpha);
            image.color = color;
        }

        private void ClearImage(Image image)
        {
            if (image == null)
            {
                return;
            }

            ReleaseMaterialInstance(image);
            image.sprite = null;
            image.material = null;
            image.color = Color.clear;
            image.gameObject.SetActive(false);
        }

        private void CancelTransition()
        {
            if (transition != null)
            {
                StopCoroutine(transition);
                transition = null;
            }
        }

        private async void LoadAndApplyAsync(FlexibleLayoutBackgroundStyle style, float duration)
        {
            assetCancellation?.Cancel();
            assetCancellation?.Dispose();
            var cancellation = new CancellationTokenSource();
            assetCancellation = cancellation;
            var scope = new AssetScope(assetProvider);
            try
            {
                var sprite = style.Sprite;
                var material = style.Material;
                var effectSprite = style.EffectSprite;
                var effectMaterial = style.EffectMaterial;
                if (!string.IsNullOrWhiteSpace(style.SpriteAssetId))
                    sprite = (await scope.LoadAsync<Sprite>(style.SpriteAssetId, cancellation.Token)).Asset;
                if (!string.IsNullOrWhiteSpace(style.MaterialAssetId))
                    material = (await scope.LoadAsync<Material>(style.MaterialAssetId, cancellation.Token)).Asset;
                if (!string.IsNullOrWhiteSpace(style.EffectSpriteAssetId))
                    effectSprite = (await scope.LoadAsync<Sprite>(style.EffectSpriteAssetId, cancellation.Token)).Asset;
                if (!string.IsNullOrWhiteSpace(style.EffectMaterialAssetId))
                    effectMaterial = (await scope.LoadAsync<Material>(style.EffectMaterialAssetId, cancellation.Token)).Asset;

                cancellation.Token.ThrowIfCancellationRequested();
                loadedAssets?.Dispose();
                loadedAssets = scope;
                ApplyResolvedStyle(style, sprite, material, effectSprite, effectMaterial, duration);
            }
            catch (OperationCanceledException)
            {
                scope.Dispose();
            }
            catch (Exception exception)
            {
                scope.Dispose();
                Debug.LogWarning($"Flexible layout background assets could not be loaded: {exception.Message}", this);
            }
        }

        private void ReleaseLoadedAssets()
        {
            assetCancellation?.Cancel();
            assetCancellation?.Dispose();
            assetCancellation = null;
            loadedAssets?.Dispose();
            loadedAssets = null;
        }

        private void ReleaseMaterialInstances()
        {
            ReleaseMaterialInstance(backgroundA);
            ReleaseMaterialInstance(backgroundB);
        }

        private void ReleaseMaterialInstance(Image image)
        {
            Material instance = null;
            if (image == backgroundA)
            {
                instance = backgroundAInstance;
                backgroundAInstance = null;
            }
            else if (image == backgroundB)
            {
                instance = backgroundBInstance;
                backgroundBInstance = null;
            }

            if (instance == null)
            {
                return;
            }

            if (image != null && image.material == instance)
            {
                image.material = null;
            }

            if (Application.isPlaying)
            {
                Destroy(instance);
            }
            else
            {
                DestroyImmediate(instance);
            }
        }
    }
}
