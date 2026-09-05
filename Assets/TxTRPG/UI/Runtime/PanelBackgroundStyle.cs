using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI
{
    public enum FlexibleLayoutBackgroundScaleMode
    {
        Stretch,
        Fit,
        Fill,
        Tile,
        Sliced
    }

    public enum FlexibleLayoutBackgroundOverflowMode
    {
        Visible,
        ClipToPanel
    }

    public enum FlexibleLayoutMaterialMode
    {
        Shared,
        Instance
    }

    [CreateAssetMenu(fileName = "PanelBackgroundStyle", menuName = "TxT RPG/UI/Panel Background Style")]
    public class PanelBackgroundStyle : ScriptableObject
    {
#if UNITY_EDITOR
        [SerializeField] private Sprite sprite;
#endif
        [SerializeField] private string spriteAssetId;
        [SerializeField] private Color tint = Color.white;
        [SerializeField, Range(0f, 1f)] private float opacity = 1f;
#if UNITY_EDITOR
        [SerializeField] private Material material;
#endif
        [SerializeField] private string materialAssetId;
        [SerializeField] private FlexibleLayoutMaterialMode materialMode = FlexibleLayoutMaterialMode.Shared;
        [SerializeField] private FlexibleLayoutBackgroundScaleMode scaleMode =
            FlexibleLayoutBackgroundScaleMode.Sliced;
        [SerializeField] private FlexibleLayoutBackgroundOverflowMode overflowMode =
            FlexibleLayoutBackgroundOverflowMode.ClipToPanel;
#if UNITY_EDITOR
        [SerializeField] private Sprite effectSprite;
#endif
        [SerializeField] private string effectSpriteAssetId;
        [SerializeField] private Color effectTint = Color.clear;
#if UNITY_EDITOR
        [SerializeField] private Material effectMaterial;
#endif
        [SerializeField] private string effectMaterialAssetId;
        [SerializeField] private FlexibleLayoutMaterialMode effectMaterialMode =
            FlexibleLayoutMaterialMode.Shared;

#if UNITY_EDITOR
        public Sprite Sprite => sprite;
#else
        public Sprite Sprite => null;
#endif
        public string SpriteAssetId => spriteAssetId;
        public Color Tint => tint;
        public float Opacity => Mathf.Clamp01(opacity);
#if UNITY_EDITOR
        public Material Material => material;
#else
        public Material Material => null;
#endif
        public string MaterialAssetId => materialAssetId;
        public FlexibleLayoutMaterialMode MaterialMode => materialMode;
        public FlexibleLayoutBackgroundScaleMode ScaleMode => scaleMode;
        public FlexibleLayoutBackgroundOverflowMode OverflowMode => overflowMode;
#if UNITY_EDITOR
        public Sprite EffectSprite => effectSprite;
#else
        public Sprite EffectSprite => null;
#endif
        public string EffectSpriteAssetId => effectSpriteAssetId;
        public Color EffectTint => effectTint;
#if UNITY_EDITOR
        public Material EffectMaterial => effectMaterial;
#else
        public Material EffectMaterial => null;
#endif
        public string EffectMaterialAssetId => effectMaterialAssetId;
        public FlexibleLayoutMaterialMode EffectMaterialMode => effectMaterialMode;
        public bool HasAddressableAssets =>
            !string.IsNullOrWhiteSpace(spriteAssetId) ||
            !string.IsNullOrWhiteSpace(materialAssetId) ||
            !string.IsNullOrWhiteSpace(effectSpriteAssetId) ||
            !string.IsNullOrWhiteSpace(effectMaterialAssetId);

        public void ConfigureAddressableIds(
            string newSpriteAssetId,
            string newMaterialAssetId = "",
            string newEffectSpriteAssetId = "",
            string newEffectMaterialAssetId = "")
        {
            spriteAssetId = newSpriteAssetId ?? string.Empty;
            materialAssetId = newMaterialAssetId ?? string.Empty;
            effectSpriteAssetId = newEffectSpriteAssetId ?? string.Empty;
            effectMaterialAssetId = newEffectMaterialAssetId ?? string.Empty;
        }

        public void Configure(
            Sprite newSprite,
            Color newTint,
            FlexibleLayoutBackgroundScaleMode newScaleMode,
            float newOpacity = 1f,
            FlexibleLayoutBackgroundOverflowMode newOverflowMode =
                FlexibleLayoutBackgroundOverflowMode.ClipToPanel)
        {
#if UNITY_EDITOR
            sprite = newSprite;
#endif
            tint = newTint;
            scaleMode = newScaleMode;
            opacity = Mathf.Clamp01(newOpacity);
            overflowMode = newOverflowMode;
        }
    }

}
