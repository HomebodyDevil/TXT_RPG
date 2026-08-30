using System.Collections;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI
{
    [DisallowMultipleComponent]
    public class PanelBackgroundRenderer : MonoBehaviour
    {
        [SerializeField] private RectTransform visualRoot;
        [SerializeField] private Image backgroundA;
        [SerializeField] private Image backgroundB;
        [SerializeField] private Image effectOverlay;
        [SerializeField] private RectMask2D clipMask;
        [SerializeField] private PanelBackgroundStyle initialStyle;
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
        private Task currentLoadTask = Task.CompletedTask;
        public Task WhenAssetsReady => currentLoadTask;

        public bool IsVisible => gameObject.activeSelf;
        public PanelBackgroundStyle CurrentStyle { get; private set; }

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
                currentLoadTask = ObserveAssetLoadAsync(LoadAndApplyAsync(CurrentStyle, -1f));
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

        public void ApplyStyle(PanelBackgroundStyle style)
        {
            if (Application.isPlaying && style != null && style.HasAddressableAssets)
            {
                currentLoadTask = ObserveAssetLoadAsync(LoadAndApplyAsync(style, -1f));
                return;
            }

            ApplyResolvedStyle(style, style?.Sprite, style?.Material, style?.EffectSprite, style?.EffectMaterial, -1f);
        }

        private void ApplyResolvedStyle(
            PanelBackgroundStyle style,
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

        public void Change(PanelBackgroundStyle style, float duration = -1f)
        {
            InitializeSlots();
            var actualDuration = duration >= 0f ? duration : defaultTransitionDuration;
            if (Application.isPlaying && style != null && style.HasAddressableAssets)
            {
                currentLoadTask = ObserveAssetLoadAsync(LoadAndApplyAsync(style, actualDuration));
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
            PanelBackgroundStyle style,
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
            PanelBackgroundStyle style,
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
            PanelBackgroundStyle style,
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
                name = source.name + " (Panel Background Instance)"
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

        public async Task LoadAndApplyAsync(
            PanelBackgroundStyle style,
            float duration,
            CancellationToken cancellationToken = default)
        {
            CancelAssetRequest();
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            assetCancellation = cancellation;
            AssetScope pendingScope = new AssetScope(assetProvider);
            try
            {
                var spriteTask = LoadOptionalAsync(pendingScope, style.SpriteAssetId, style.Sprite, cancellation.Token);
                var materialTask = LoadOptionalAsync(pendingScope, style.MaterialAssetId, style.Material, cancellation.Token);
                var effectSpriteTask = LoadOptionalAsync(pendingScope, style.EffectSpriteAssetId, style.EffectSprite, cancellation.Token);
                var effectMaterialTask = LoadOptionalAsync(pendingScope, style.EffectMaterialAssetId, style.EffectMaterial, cancellation.Token);

                var sprite = await spriteTask;
                var material = await materialTask;
                var effectSprite = await effectSpriteTask;
                var effectMaterial = await effectMaterialTask;

                cancellation.Token.ThrowIfCancellationRequested();
                ClearLoadedAssetReferences();
                loadedAssets?.Dispose();
                loadedAssets = pendingScope;
                pendingScope = null;
                ApplyResolvedStyle(style, sprite, material, effectSprite, effectMaterial, duration);
            }
            finally
            {
                pendingScope?.Dispose();
                if (ReferenceEquals(assetCancellation, cancellation))
                {
                    assetCancellation = null;
                }
                cancellation.Dispose();
            }
        }

        private async Task<T> LoadOptionalAsync<T>(
            AssetScope scope,
            string assetId,
            T fallback,
            CancellationToken cancellationToken)
            where T : UnityEngine.Object
        {
            if (string.IsNullOrWhiteSpace(assetId)) return fallback;
            try
            {
                return (await scope.LoadAsync<T>(assetId, cancellationToken)).Asset;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Optional background asset '{assetId}' could not be loaded: {exception.Message}", this);
                return fallback;
            }
        }

        private async Task ObserveAssetLoadAsync(Task loadTask)
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
                Debug.LogWarning($"Panel background assets could not be loaded: {exception.Message}", this);
            }
        }

        private void ReleaseLoadedAssets()
        {
            CancelAssetRequest();
            ClearLoadedAssetReferences();
            loadedAssets?.Dispose();
            loadedAssets = null;
        }

        private void CancelAssetRequest()
        {
            assetCancellation?.Cancel();
            assetCancellation?.Dispose();
            assetCancellation = null;
        }

        private void ClearLoadedAssetReferences()
        {
            ReleaseMaterialInstances();
            if (backgroundA != null) { backgroundA.sprite = null; backgroundA.material = null; }
            if (backgroundB != null) { backgroundB.sprite = null; backgroundB.material = null; }
            if (effectOverlay != null) { effectOverlay.sprite = null; effectOverlay.material = null; }
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
