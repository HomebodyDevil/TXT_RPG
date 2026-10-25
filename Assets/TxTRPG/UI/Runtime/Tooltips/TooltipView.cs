using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TxTRPG.UI.Tooltips
{
    // What TooltipView.Show found while building a box; the presenter turns it into one-time warnings.
    public readonly struct TooltipShowReport
    {
        public TooltipShowReport(int unknownBlocks, IReadOnlyList<string> unknownKeywordIds, bool hasUnclosedMarkup, bool clipped)
        {
            UnknownBlocks = unknownBlocks;
            UnknownKeywordIds = unknownKeywordIds ?? Array.Empty<string>();
            HasUnclosedMarkup = hasUnclosedMarkup;
            Clipped = clipped;
        }

        public int UnknownBlocks { get; }
        public IReadOnlyList<string> UnknownKeywordIds { get; }
        public bool HasUnclosedMarkup { get; }
        public bool Clipped { get; }
    }

    // One tooltip box. The root is the placement root (the presenter positions it; pivot top-left) and its size follows
    // the content: the widest text line plus padding, between minimum and maximum width, and the content height up to the
    // maximum height (the rest is clipped by Content's RectMask2D). Effects belong on VisualRoot. Blocks are clones of the
    // inactive templates, reused across shows. Background is the only box raycast target besides the Text blocks, so
    // "the pointer is over the box" is the root's enter/exit state.
    [DisallowMultipleComponent]
    public sealed class TooltipView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public const float DefaultMinimumWidth = 160f;
        public const float DefaultMaximumWidth = 420f;
        public const float KeywordIconHeight = 56f;
        public const string MissingDescription = "설명이 없습니다.";

        [SerializeField] private RectTransform visualRoot;
        [SerializeField] private Graphic background;
        [SerializeField, Tooltip("Vertical layout of the blocks (padding and spacing are read from it).")] private VerticalLayoutGroup blocksRoot;
        [SerializeField] private TMP_Text titleTemplate;
        [SerializeField] private TooltipKeywordLinkDetector textTemplate;
        [SerializeField] private Image imageTemplate;
        [SerializeField] private Image separatorTemplate;

        private readonly List<TMP_Text> titles = new();
        private readonly List<TooltipKeywordLinkDetector> texts = new();
        private readonly List<Image> images = new();
        private readonly List<Image> separators = new();
        private readonly List<TooltipKeywordLinkDetector> activeTexts = new();
        private readonly List<string> unknownKeywords = new();
        private int usedTitles, usedTexts, usedImages, usedSeparators;

        // Keyword link under the pointer changed in one of this box's Text blocks (empty link ID: left the link).
        public event Action<TooltipView, TooltipKeywordLinkDetector, string> KeywordLinkChanged;

        public RectTransform Rect => (RectTransform)transform;
        public RectTransform VisualRoot => visualRoot;
        public Graphic Background => background;
        public bool IsPointerOver { get; private set; }
        public bool IsShown { get; private set; }
        public IReadOnlyList<TooltipKeywordLinkDetector> ActiveTextBlocks => activeTexts;
        public int ActiveBlockCount => usedTitles + usedTexts + usedImages + usedSeparators;

        public bool IsValid(out string missing)
        {
            missing = visualRoot == null ? nameof(visualRoot) : background == null ? nameof(background) : blocksRoot == null ? nameof(blocksRoot)
                : titleTemplate == null ? nameof(titleTemplate) : textTemplate == null ? nameof(textTemplate)
                : imageTemplate == null ? nameof(imageTemplate) : separatorTemplate == null ? nameof(separatorTemplate) : string.Empty;
            return missing.Length == 0;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!TooltipPresenter.IsTouch(eventData)) IsPointerOver = true;
        }

        // Moving between this box's own raycast targets keeps the hover.
        public void OnPointerExit(PointerEventData eventData)
        {
            var next = eventData?.pointerCurrentRaycast.gameObject;
            if (next != null && next.transform.IsChildOf(transform)) return;
            IsPointerOver = false;
        }

        public void SetPointerOverForTests(bool value) => IsPointerOver = value;

        // Builds the box and returns its size (canvas units). Never throws for bad content: unknown blocks are skipped and
        // reported, unknown keywords are shown as plain text.
        public Vector2 Show(IReadOnlyList<TooltipBlock> blocks, TooltipKeywordCatalog keywords, float minimumWidth, float maximumWidth,
            float maximumHeight, out TooltipShowReport report)
        {
            Clear();
            IsShown = true;
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            HideTemplates();
            var unknownBlocks = 0;
            var unclosed = false;
            unknownKeywords.Clear();
            var padding = blocksRoot != null ? blocksRoot.padding.horizontal : 0;
            var widest = 0f;
            var sibling = 0;
            if (blocks != null)
            {
                foreach (var block in blocks)
                {
                    if (block == null) continue;
                    switch (block.Kind)
                    {
                        case TooltipBlockKind.Title:
                        {
                            var title = Take(titles, titleTemplate, ref usedTitles);
                            title.text = block.Content;
                            widest = Mathf.Max(widest, PreferredWidth(title));
                            Place(title.transform, ref sibling);
                            break;
                        }
                        case TooltipBlockKind.Text:
                        {
                            var text = Take(texts, textTemplate, ref usedTexts);
                            var markup = TooltipMarkup.Parse(block.Content, keywords);
                            unclosed |= markup.HasUnclosedMarkup;
                            unknownKeywords.AddRange(markup.UnknownKeywordIds);
                            text.SetText(markup.Text);
                            activeTexts.Add(text);
                            widest = Mathf.Max(widest, PreferredWidth(text.Text));
                            Place(text.transform, ref sibling);
                            break;
                        }
                        case TooltipBlockKind.Image:
                        {
                            if (block.Sprite == null) break;
                            var image = Take(images, imageTemplate, ref usedImages);
                            image.sprite = block.Sprite;
                            image.preserveAspect = true;
                            SetHeight(image.gameObject, block.ImageHeight);
                            Place(image.transform, ref sibling);
                            break;
                        }
                        case TooltipBlockKind.Separator:
                            Place(Take(separators, separatorTemplate, ref usedSeparators).transform, ref sibling);
                            break;
                        default:
                            unknownBlocks++;
                            break;
                    }
                }
            }

            var width = Mathf.Clamp(Mathf.Ceil(widest) + padding, Mathf.Max(0f, minimumWidth), Mathf.Max(minimumWidth, maximumWidth));
            Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            var preferred = 0f;
            if (blocksRoot != null)
            {
                var blocksRect = (RectTransform)blocksRoot.transform;
                LayoutRebuilder.ForceRebuildLayoutImmediate(blocksRect);
                preferred = LayoutUtility.GetPreferredHeight(blocksRect);
            }
            var height = Mathf.Min(preferred, Mathf.Max(0f, maximumHeight));
            Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            report = new TooltipShowReport(unknownBlocks, unknownKeywords.ToArray(), unclosed, preferred > height + .5f);
            return new Vector2(width, height);
        }

        public void Clear()
        {
            for (var i = 0; i < usedTitles; i++) { titles[i].text = string.Empty; titles[i].gameObject.SetActive(false); }
            for (var i = 0; i < usedTexts; i++) { texts[i].Clear(); texts[i].gameObject.SetActive(false); }
            for (var i = 0; i < usedImages; i++) { images[i].sprite = null; images[i].gameObject.SetActive(false); }
            for (var i = 0; i < usedSeparators; i++) separators[i].gameObject.SetActive(false);
            usedTitles = usedTexts = usedImages = usedSeparators = 0;
            activeTexts.Clear();
            IsPointerOver = false;
            IsShown = false;
        }

        public void Hide()
        {
            Clear();
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        private void OnDisable() => IsPointerOver = false;

        private void OnDestroy()
        {
            foreach (var text in texts) if (text != null) text.LinkChanged -= OnLinkChanged;
        }

        private T Take<T>(List<T> pool, T template, ref int used) where T : Component
        {
            if (used == pool.Count)
            {
                var clone = Instantiate(template, template.transform.parent, false);
                clone.name = template.name.Replace("Template", string.Empty) + (pool.Count + 1);
                if (clone is TooltipKeywordLinkDetector detector) detector.LinkChanged += OnLinkChanged;
                pool.Add(clone);
            }
            var item = pool[used++];
            if (!item.gameObject.activeSelf) item.gameObject.SetActive(true);
            return item;
        }

        private void Place(Transform child, ref int sibling) => child.SetSiblingIndex(sibling++);

        private void HideTemplates()
        {
            if (titleTemplate != null && titleTemplate.gameObject.activeSelf) titleTemplate.gameObject.SetActive(false);
            if (textTemplate != null && textTemplate.gameObject.activeSelf) textTemplate.gameObject.SetActive(false);
            if (imageTemplate != null && imageTemplate.gameObject.activeSelf) imageTemplate.gameObject.SetActive(false);
            if (separatorTemplate != null && separatorTemplate.gameObject.activeSelf) separatorTemplate.gameObject.SetActive(false);
        }

        private void OnLinkChanged(TooltipKeywordLinkDetector detector, string linkId) => KeywordLinkChanged?.Invoke(this, detector, linkId);

        // Width of the longest line without wrapping (rich text tags excluded).
        private static float PreferredWidth(TMP_Text text) =>
            text == null || string.IsNullOrEmpty(text.text) ? 0f : text.GetPreferredValues(text.text, float.PositiveInfinity, float.PositiveInfinity).x;

        private static void SetHeight(GameObject target, float height)
        {
            var layout = target.GetComponent<LayoutElement>() ?? target.AddComponent<LayoutElement>();
            layout.minHeight = layout.preferredHeight = height;
        }

#if UNITY_EDITOR
        public void ConfigureForEditor(RectTransform visual, Graphic box, VerticalLayoutGroup layout, TMP_Text title,
            TooltipKeywordLinkDetector text, Image image, Image separator)
        {
            visualRoot = visual; background = box; blocksRoot = layout; titleTemplate = title; textTemplate = text;
            imageTemplate = image; separatorTemplate = separator;
        }
#endif
    }
}
