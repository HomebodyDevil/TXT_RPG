using System;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.SlotMachine
{
    public enum SlotSkinDrawMode
    {
        Simple,
        Sliced,
        Tiled
    }

    // One image entry of a SlotMachinePanelSkin. An entry with no sprite and no color override is empty: the target
    // shows its recorded Prefab default.
    [Serializable]
    public sealed class SlotSkinImage
    {
        [SerializeField, Tooltip("Empty: the Prefab's default sprite is used.")]
        private Sprite sprite;
        [SerializeField, Tooltip("On: Color is used. Off: white (the image's own colors) with a sprite, the Prefab's default color without one.")]
        private bool overrideColor;
        [SerializeField] private Color color = Color.white;
        [SerializeField, Tooltip("Applied only when a sprite is set. Sliced needs a sprite border.")]
        private SlotSkinDrawMode drawMode = SlotSkinDrawMode.Sliced;
        [SerializeField, Min(.01f), Tooltip("Border scale for Sliced and Tiled.")]
        private float pixelsPerUnitMultiplier = 1f;

        [NonSerialized] private bool warnedBorder;

        public Sprite Sprite => sprite;
        public bool OverrideColor => overrideColor;
        public Color Color => color;
        public SlotSkinDrawMode DrawMode => drawMode;
        public float PixelsPerUnitMultiplier => Mathf.Max(.01f, pixelsPerUnitMultiplier);
        public bool IsEmpty => sprite == null && !overrideColor;

        public Image.Type ImageType => drawMode switch
        {
            SlotSkinDrawMode.Simple => Image.Type.Simple,
            SlotSkinDrawMode.Tiled => Image.Type.Tiled,
            _ => Image.Type.Sliced
        };

        // A Sliced sprite without a border is stretched as a whole, which is almost never intended.
        internal void WarnIfBorderMissing(UnityEngine.Object context)
        {
            if (warnedBorder || sprite == null || drawMode != SlotSkinDrawMode.Sliced || sprite.border != Vector4.zero) return;
            warnedBorder = true;
            Debug.LogWarning($"Slot machine skin sprite '{sprite.name}' is drawn Sliced but has no border; it is stretched as a whole. Set its 9-slice border in the Sprite Editor.", context);
        }

        internal void Validate() => pixelsPerUnitMultiplier = Mathf.Max(.01f, pixelsPerUnitMultiplier);
    }

    // Optional text color of a SlotMachinePanelSkin. Off: the Prefab's default text color is used.
    [Serializable]
    public sealed class SlotSkinColor
    {
        [SerializeField, InspectorName("Override")] private bool overrideColor;
        [SerializeField] private Color color = Color.white;

        public bool Override => overrideColor;
        public Color Color => color;
    }

    // Outward expansion in canvas units (same meaning as RectOffset, but fractional).
    [Serializable]
    public sealed class SlotSkinOffset
    {
        [SerializeField] private float left = 4f;
        [SerializeField] private float right = 4f;
        [SerializeField] private float top = 4f;
        [SerializeField] private float bottom = 4f;

        public float Left => left;
        public float Right => right;
        public float Top => top;
        public float Bottom => bottom;
    }
}
