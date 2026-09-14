using System;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.Exploration
{
    public enum ExplorationCardVisualMode { Image, ProceduralShape }

    [Serializable]
    public struct ExplorationCardShapeSettings
    {
        [Tooltip("Image keeps the compatible card background. Procedural Shape generates the selected geometry without a texture.")]
        public ExplorationCardVisualMode visualMode;
        [Tooltip("The generated background, artwork mask, and border geometry.")]
        public ExplorationCardShape shape;
        [Tooltip("The ShapeVisual size inside the card layout slot. Circle uses the shorter axis as its diameter.")]
        public Vector2 size;
        [Tooltip("Common corner radius for Rounded Rectangle and Triangle. Other shapes ignore this value.")]
        [Min(0f)] public float cornerRadius;
        [Tooltip("Inner border thickness. It never increases the card layout size.")]
        [Min(0f)] public float borderThickness;
        public Color backgroundColor;
        public Color borderColor;

        public static ExplorationCardShapeSettings Default => new()
        {
            visualMode = ExplorationCardVisualMode.Image,
            shape = ExplorationCardShape.RoundedRectangle,
            size = new Vector2(186f, 144f), cornerRadius = 14f, borderThickness = 2f,
            backgroundColor = new Color(.105f, .14f, .21f, 1f), borderColor = new Color(.65f, .78f, 1f, .65f)
        };
    }

    [DisallowMultipleComponent]
    public sealed class ExplorationCardShapePresentation : MonoBehaviour
    {
        [SerializeField] private RectTransform shapeVisual;
        [SerializeField] private Image legacyBackground;
        [SerializeField] private ExplorationCardShapeGraphic shapeMask;
        [SerializeField] private ExplorationCardShapeGraphic shapeBorder;
        [Header("Shape Appearance")]
        [SerializeField] private ExplorationCardShapeSettings settings = default;

        public ExplorationCardShapeSettings Settings => settings;

        public void Apply(ExplorationCardShapeSettings value)
        {
            settings = Normalize(value);
            if (shapeVisual != null) shapeVisual.sizeDelta = settings.size;
            var procedural = settings.visualMode == ExplorationCardVisualMode.ProceduralShape;
            if (legacyBackground != null) legacyBackground.enabled = !procedural;
            if (shapeMask != null)
            {
                shapeMask.enabled = true;
                shapeMask.color = procedural ? settings.backgroundColor : Color.clear;
                shapeMask.Configure(procedural ? settings.shape : ExplorationCardShape.Rectangle,
                    procedural ? settings.cornerRadius : 0f, ExplorationCardShapeDrawMode.Fill, 0f);
            }
            if (shapeBorder != null) { shapeBorder.enabled = procedural; shapeBorder.color = settings.borderColor; shapeBorder.Configure(settings.shape, settings.cornerRadius, ExplorationCardShapeDrawMode.InnerBorder, settings.borderThickness); }
        }

        private static ExplorationCardShapeSettings Normalize(ExplorationCardShapeSettings value)
        {
            if (value.size.x <= 0f || float.IsNaN(value.size.x) || float.IsInfinity(value.size.x)) value.size.x = 186f;
            if (value.size.y <= 0f || float.IsNaN(value.size.y) || float.IsInfinity(value.size.y)) value.size.y = 144f;
            value.cornerRadius = Safe(value.cornerRadius, 0f); value.borderThickness = Safe(value.borderThickness, 0f);
            return value;
        }

        private static float Safe(float value, float fallback) => float.IsNaN(value) || float.IsInfinity(value) || value < 0f ? fallback : value;

        private void Awake() { if (settings.size == Vector2.zero) settings = ExplorationCardShapeSettings.Default; Apply(settings); }
#if UNITY_EDITOR
        private void OnValidate() { if (settings.size == Vector2.zero) settings = ExplorationCardShapeSettings.Default; Apply(settings); }
        public void ConfigureForEditor(RectTransform targetShapeVisual, Image targetLegacyBackground, ExplorationCardShapeGraphic targetMask, ExplorationCardShapeGraphic targetBorder, ExplorationCardShapeSettings value)
        { shapeVisual = targetShapeVisual; legacyBackground = targetLegacyBackground; shapeMask = targetMask; shapeBorder = targetBorder; Apply(value); }
#endif
    }
}
