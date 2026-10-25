using System;
using UnityEngine;

namespace TxTRPG.UI.Tooltips
{
    // Fixed values for authored assets. 0 is unknown: such a block is skipped with a warning. A new block kind gets a new
    // value and a new template in TooltipView.
    public enum TooltipBlockKind
    {
        Unknown = 0,
        Title = 1,
        Text = 2,
        Image = 3,
        Separator = 4
    }

    // Kind of thing a keyword is related to. Targets are referenced by ID string so the UI assembly never depends on the
    // Gameplay or Content definitions.
    public enum TooltipSubjectCategory
    {
        Unknown = 0,
        Rune = 1,
        Item = 2,
        Skill = 3,
        Other = 4
    }

    // One tooltip block. Title and Text use text (Text may contain {kw:id} keywords); Image uses sprite and imageHeight
    // (its width follows the tooltip width, keeping the aspect ratio); Separator uses nothing.
    [Serializable]
    public sealed class TooltipBlock
    {
        public const float DefaultImageHeight = 96f;

        [SerializeField] private TooltipBlockKind kind = TooltipBlockKind.Text;
        [SerializeField, TextArea(2, 8)] private string text = string.Empty;
        [SerializeField] private Sprite sprite;
        [SerializeField, Min(0f), Tooltip("Image height in canvas units; the width follows the tooltip.")]
        private float imageHeight = DefaultImageHeight;

        public TooltipBlock() { }

        public TooltipBlock(TooltipBlockKind blockKind, string blockText = null, Sprite blockSprite = null, float height = DefaultImageHeight)
        {
            kind = blockKind;
            text = blockText ?? string.Empty;
            sprite = blockSprite;
            imageHeight = Mathf.Max(0f, height);
        }

        public static TooltipBlock Title(string value) => new(TooltipBlockKind.Title, value);
        public static TooltipBlock Text(string value) => new(TooltipBlockKind.Text, value);
        public static TooltipBlock Image(Sprite value, float height = DefaultImageHeight) => new(TooltipBlockKind.Image, null, value, height);
        public static TooltipBlock Separator() => new(TooltipBlockKind.Separator);

        public TooltipBlockKind Kind => kind;
        public string Content => text ?? string.Empty;
        public Sprite Sprite => sprite;
        public float ImageHeight => Mathf.Max(0f, imageHeight);
    }

    [Serializable]
    public sealed class TooltipRelatedEntry
    {
        [SerializeField] private TooltipSubjectCategory category = TooltipSubjectCategory.Rune;
        [SerializeField, Tooltip("Stable ID of the related rune, item or skill definition.")] private string subjectId = string.Empty;

        public TooltipRelatedEntry() { }
        public TooltipRelatedEntry(TooltipSubjectCategory subjectCategory, string id) { category = subjectCategory; subjectId = id; }

        public TooltipSubjectCategory Category => category;
        public string SubjectId => subjectId?.Trim() ?? string.Empty;
    }
}
