using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.Shapes
{
    public enum ShapedButtonVisualMode { ProceduralShape, Image }

    public enum ShapedButtonEffectTarget { Background, Border, Icon, EffectOverlay }

    [Serializable]
    public struct ShapedButtonAppearance
    {
        [Tooltip("Procedural Shape draws the selected geometry without a texture. Image uses designer Sprites (9-slice when the Sprite has borders).")]
        public ShapedButtonVisualMode visualMode;
        [Tooltip("Shape of the background, border, and (when shape hit testing is on) the input area.")]
        public UiShape shape;
        [Min(0f), Tooltip("Corner radius for Rounded Rectangle and Triangle. Other shapes ignore it.")]
        public float cornerRadius;
        [Min(0f), Tooltip("Inner border thickness. It never changes the button size.")]
        public float borderThickness;
        public Color backgroundColor;
        public Color borderColor;
        [Tooltip("Image mode background Sprite.")]
        public Sprite backgroundSprite;
        [Tooltip("Image mode border Sprite. Empty hides the border in Image mode.")]
        public Sprite borderSprite;
        [Tooltip("Procedural mode: the input area follows the visible shape. Image mode: the input area is the rectangle.")]
        public bool shapeHitTest;
        [Range(0f, 1f), Tooltip("Image mode only: optional alpha hit test threshold (requires a Read/Write enabled texture). Zero keeps the rectangle.")]
        public float imageAlphaHitThreshold;

        public static ShapedButtonAppearance Default => new()
        {
            visualMode = ShapedButtonVisualMode.ProceduralShape,
            shape = UiShape.Circle,
            cornerRadius = 12f,
            borderThickness = 2f,
            backgroundColor = new Color(.2f, .24f, .32f, 1f),
            borderColor = new Color(.65f, .72f, .9f, .85f),
            shapeHitTest = true
        };
    }

    // Visual layer of a shaped button. The Root RectTransform (size and placement) and the HitArea are never touched by
    // effects; VisualRoot and its Background/Border/Icon/Label/EffectOverlay children are the effect targets.
    // Icon and label fallback stay with GameMenuButtonView on the same Root.
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class ShapedButtonView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private UiShapeHitArea hitArea;
        [SerializeField] private RectTransform visualRoot;
        [SerializeField] private CanvasGroup visualGroup;
        [SerializeField] private UiShapeGraphic backgroundShape;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private UiShapeGraphic borderShape;
        [SerializeField] private Image borderImage;
        [SerializeField] private Image icon;
        [SerializeField] private RectTransform effectOverlay;
        [Header("Shape Appearance")]
        [SerializeField] private ShapedButtonAppearance appearance = ShapedButtonAppearance.Default;
        [Header("Effects")]
        [SerializeField, Tooltip("Shader effect Material for the background (noise, glitch, ...). Empty uses the default UI material.")]
        private Material backgroundMaterial;
        [SerializeField] private Material borderMaterial;
        [SerializeField] private Material iconMaterial;
        [SerializeField, Tooltip("Applied to every Graphic under EffectOverlay.")] private Material effectOverlayMaterial;
        [SerializeField, Tooltip("Animated effects are removed while Reduce Motion is on or effects are disabled.")]
        private bool effectsAreAnimated = true;
        [SerializeField, Range(.2f, 1f), Tooltip("VisualRoot opacity while not interactable, in addition to the Button tint.")]
        private float disabledOpacity = .45f;

        private readonly Dictionary<ShapedButtonEffectTarget, Material> instances = new();
        private readonly List<Graphic> overlayGraphics = new();
        private readonly HashSet<Graphic> suppressedOverlay = new();
        private bool effectsAllowed = true;
        private bool interactableVisual = true;
        private bool refreshPending;

        public Button Button => button;
        public RectTransform VisualRoot => visualRoot;
        public UiShapeHitArea HitArea => hitArea;
        public ShapedButtonAppearance Appearance => appearance;
        public bool EffectsActive => effectsAllowed || !effectsAreAnimated;
        public float VisualOpacity => visualGroup != null ? visualGroup.alpha : 1f;

        public void ApplyAppearance(ShapedButtonAppearance value)
        {
            appearance = Normalize(value);
            Refresh();
        }

        public void SetInteractableVisual(bool interactable)
        {
            interactableVisual = interactable;
            if (visualGroup != null) visualGroup.alpha = interactable ? 1f : disabledOpacity;
        }

        // Connection point for Reduce Motion and quality tiers. Turning effects off keeps the button fully usable.
        public void SetEffectsAllowed(bool allowed)
        {
            if (effectsAllowed == allowed) return;
            effectsAllowed = allowed;
            ApplyMaterials();
        }

        // Runtime property changes go to an instance owned by this view; the shared Material asset is never modified.
        public Material GetOrCreateEffectInstance(ShapedButtonEffectTarget target)
        {
            if (instances.TryGetValue(target, out var existing) && existing != null) return existing;
            var source = SourceMaterial(target);
            if (source == null) return null;
            var instance = new Material(source) { name = source.name + " (Instance)", hideFlags = HideFlags.DontSave };
            instances[target] = instance;
            ApplyMaterials();
            return instance;
        }

        // Restores the serialized Materials and releases instances. Called on hide, disable, and destroy so a re-shown
        // button never keeps an earlier effect state.
        public void ResetEffects()
        {
            foreach (var instance in instances.Values) DestroyMaterial(instance);
            instances.Clear();
            ApplyMaterials();
        }

        public void Refresh()
        {
            refreshPending = false;
            var procedural = appearance.visualMode == ShapedButtonVisualMode.ProceduralShape;
            if (backgroundShape != null)
            {
                backgroundShape.enabled = procedural;
                backgroundShape.color = appearance.backgroundColor;
                backgroundShape.raycastTarget = false;
                backgroundShape.Configure(appearance.shape, appearance.cornerRadius, UiShapeDrawMode.Fill, 0f);
            }
            if (borderShape != null)
            {
                borderShape.enabled = procedural && appearance.borderThickness > 0f;
                borderShape.color = appearance.borderColor;
                borderShape.raycastTarget = false;
                borderShape.Configure(appearance.shape, appearance.cornerRadius, UiShapeDrawMode.InnerBorder, appearance.borderThickness);
            }
            if (backgroundImage != null)
            {
                backgroundImage.enabled = !procedural;
                backgroundImage.sprite = appearance.backgroundSprite;
                backgroundImage.type = HasBorder(appearance.backgroundSprite) ? Image.Type.Sliced : Image.Type.Simple;
                backgroundImage.color = appearance.backgroundColor;
                backgroundImage.raycastTarget = false;
            }
            if (borderImage != null)
            {
                borderImage.enabled = !procedural && appearance.borderSprite != null;
                borderImage.sprite = appearance.borderSprite;
                borderImage.type = HasBorder(appearance.borderSprite) ? Image.Type.Sliced : Image.Type.Simple;
                borderImage.color = appearance.borderColor;
                borderImage.raycastTarget = false;
            }
            if (hitArea != null)
            {
                var hitShape = procedural && appearance.shapeHitTest ? appearance.shape : UiShape.Rectangle;
                hitArea.Configure(hitShape, appearance.cornerRadius, procedural ? null : backgroundImage,
                    procedural ? 0f : appearance.imageAlphaHitThreshold);
            }
            if (button != null && button.transition == Selectable.Transition.ColorTint)
                button.targetGraphic = procedural ? backgroundShape : backgroundImage;
            if (visualGroup != null)
            {
                visualGroup.blocksRaycasts = false;
                visualGroup.interactable = false;
                visualGroup.alpha = interactableVisual ? 1f : disabledOpacity;
            }
            ApplyMaterials();
        }

        // Overlay graphics are enabled/disabled rather than activated so this is safe inside OnEnable/OnDisable.
        private void ApplyMaterials()
        {
            var active = EffectsActive;
            Assign(backgroundShape, ShapedButtonEffectTarget.Background, active);
            Assign(backgroundImage, ShapedButtonEffectTarget.Background, active);
            Assign(borderShape, ShapedButtonEffectTarget.Border, active);
            Assign(borderImage, ShapedButtonEffectTarget.Border, active);
            Assign(icon, ShapedButtonEffectTarget.Icon, active);
            if (effectOverlay == null) return;
            effectOverlay.GetComponentsInChildren(true, overlayGraphics);
            foreach (var graphic in overlayGraphics)
            {
                Assign(graphic, ShapedButtonEffectTarget.EffectOverlay, active);
                graphic.raycastTarget = false;
                if (!active && graphic.enabled) { graphic.enabled = false; suppressedOverlay.Add(graphic); }
            }
            overlayGraphics.Clear();
            if (!active) return;
            // Only graphics this view hid are restored; designer-disabled overlay graphics stay disabled.
            foreach (var graphic in suppressedOverlay) if (graphic != null) graphic.enabled = true;
            suppressedOverlay.Clear();
        }

        private void Assign(Graphic graphic, ShapedButtonEffectTarget target, bool active)
        {
            if (graphic == null) return;
            var material = active ? (instances.TryGetValue(target, out var instance) && instance != null ? instance : SourceMaterial(target)) : null;
            if (graphic.material != (material != null ? material : graphic.defaultMaterial)) graphic.material = material;
        }

        private Material SourceMaterial(ShapedButtonEffectTarget target) => target switch
        {
            ShapedButtonEffectTarget.Background => backgroundMaterial,
            ShapedButtonEffectTarget.Border => borderMaterial,
            ShapedButtonEffectTarget.Icon => iconMaterial,
            _ => effectOverlayMaterial
        };

        private static bool HasBorder(Sprite sprite) => sprite != null && sprite.border != Vector4.zero;

        private static ShapedButtonAppearance Normalize(ShapedButtonAppearance value)
        {
            value.cornerRadius = UiShapeGeometry.Sanitize(value.cornerRadius);
            value.borderThickness = UiShapeGeometry.Sanitize(value.borderThickness);
            value.imageAlphaHitThreshold = Mathf.Clamp01(value.imageAlphaHitThreshold);
            return value;
        }

        private static void DestroyMaterial(Material material)
        {
            if (material == null) return;
            if (Application.isPlaying) Destroy(material); else DestroyImmediate(material);
        }

        private void OnEnable() => Refresh();
        private void OnDisable() => ResetEffects();
        private void OnDestroy() { foreach (var instance in instances.Values) DestroyMaterial(instance); instances.Clear(); }
        private void OnValidate()
        {
            disabledOpacity = Mathf.Clamp(disabledOpacity, .2f, 1f);
            refreshPending = true;
        }
        private void LateUpdate()
        {
            if (refreshPending) { appearance = Normalize(appearance); Refresh(); }
        }

#if UNITY_EDITOR
        public void ConfigureForEditor(Button targetButton, UiShapeHitArea targetHitArea, RectTransform targetVisualRoot,
            CanvasGroup targetVisualGroup, UiShapeGraphic targetBackgroundShape, Image targetBackgroundImage,
            UiShapeGraphic targetBorderShape, Image targetBorderImage, Image targetIcon, RectTransform targetEffectOverlay)
        {
            button = targetButton; hitArea = targetHitArea; visualRoot = targetVisualRoot; visualGroup = targetVisualGroup;
            backgroundShape = targetBackgroundShape; backgroundImage = targetBackgroundImage;
            borderShape = targetBorderShape; borderImage = targetBorderImage; icon = targetIcon; effectOverlay = targetEffectOverlay;
            Refresh();
        }

        public void SetEffectMaterialsForEditor(Material background, Material border, Material iconEffect, Material overlay)
        { backgroundMaterial = background; borderMaterial = border; iconMaterial = iconEffect; effectOverlayMaterial = overlay; ApplyMaterials(); }
#endif
    }
}
