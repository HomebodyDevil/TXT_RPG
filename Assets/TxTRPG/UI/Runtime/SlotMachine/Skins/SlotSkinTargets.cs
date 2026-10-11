using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.SlotMachine
{
    // A skinnable graphic and its default look, recorded once from the Prefab by the application menu (or the
    // "기본 외형 다시 기록" context menu). Applying computes the final look from the skin entry and these defaults only,
    // never from the graphic's current values, so switching skins any number of times returns exactly to the default.
    [Serializable]
    public sealed class SlotSkinGraphicTarget
    {
        [SerializeField] private Graphic graphic;
        [SerializeField] private bool recorded;
        [SerializeField] private Sprite defaultSprite;
        [SerializeField] private Color defaultColor = Color.white;
        [SerializeField] private Image.Type defaultType = Image.Type.Simple;
        [SerializeField] private float defaultPixelsPerUnitMultiplier = 1f;

        [NonSerialized] private bool warned;

        public SlotSkinGraphicTarget() { }
        public SlotSkinGraphicTarget(Graphic target) => graphic = target;

        public Graphic Graphic => graphic;
        public bool HasDefaults => recorded && graphic != null;
        public Sprite DefaultSprite => defaultSprite;
        public Color DefaultColor => defaultColor;
        public Image.Type DefaultType => defaultType;

        // Applies to the recorded graphic. Returns false when it was skipped (missing graphic or defaults).
        public bool Apply(SlotSkinImage entry, UnityEngine.Object context) => ApplyTo(graphic, entry, context);

        // Applies this target's defaults to another graphic created from the same template (added reel dividers).
        public bool ApplyTo(Graphic target, SlotSkinImage entry, UnityEngine.Object context)
        {
            if (target == null || !recorded)
            {
                // A missing target only matters when the skin wants to change it.
                if (!warned && entry != null && !entry.IsEmpty)
                {
                    warned = true;
                    Debug.LogWarning(target == null
                        ? $"{context?.name}: a slot machine skin target was removed from the Prefab; it is skipped."
                        : $"{context?.name}: the default look of '{target.name}' was never recorded; run the skin application menu. It is skipped.", context);
                }
                return false;
            }
            var hasSprite = entry != null && entry.Sprite != null;
            var color = entry != null && entry.OverrideColor ? entry.Color : hasSprite ? Color.white : defaultColor;
            if (target is Image image)
            {
                var sprite = hasSprite ? entry.Sprite : defaultSprite;
                var type = hasSprite ? entry.ImageType : defaultType;
                var multiplier = hasSprite ? entry.PixelsPerUnitMultiplier : defaultPixelsPerUnitMultiplier;
                if (hasSprite) entry.WarnIfBorderMissing(context);
                if (image.sprite != sprite) image.sprite = sprite;
                if (image.type != type) image.type = type;
                if (!Mathf.Approximately(image.pixelsPerUnitMultiplier, multiplier)) image.pixelsPerUnitMultiplier = multiplier;
            }
            if (target.color != color) target.color = color;
            return true;
        }

        // Records the current graphic values as the default. Existing defaults are kept unless overwrite is set.
        public bool Record(Graphic target, bool overwrite)
        {
            if (graphic != target) { graphic = target; recorded = false; }
            if (graphic == null || recorded && !overwrite) return false;
            defaultColor = graphic.color;
            if (graphic is Image image)
            {
                defaultSprite = image.sprite;
                defaultType = image.type;
                defaultPixelsPerUnitMultiplier = image.pixelsPerUnitMultiplier;
            }
            else
            {
                defaultSprite = null;
                defaultType = Image.Type.Simple;
                defaultPixelsPerUnitMultiplier = 1f;
            }
            recorded = true;
            return true;
        }
    }

    // A skinnable text and its recorded default color.
    [Serializable]
    public sealed class SlotSkinTextTarget
    {
        [SerializeField] private TMP_Text text;
        [SerializeField] private bool recorded;
        [SerializeField] private Color defaultColor = Color.white;

        public SlotSkinTextTarget() { }
        public SlotSkinTextTarget(TMP_Text target) => text = target;

        public TMP_Text Text => text;
        public bool HasDefaults => recorded && text != null;
        public Color DefaultColor => defaultColor;

        public Color Resolve(SlotSkinColor entry) => entry != null && entry.Override ? entry.Color : defaultColor;

        public bool Apply(SlotSkinColor entry)
        {
            if (!HasDefaults) return false;
            var color = Resolve(entry);
            if (text.color != color) text.color = color;
            return true;
        }

        public bool Record(TMP_Text target, bool overwrite)
        {
            if (text != target) { text = target; recorded = false; }
            if (text == null || recorded && !overwrite) return false;
            defaultColor = text.color;
            recorded = true;
            return true;
        }
    }
}
