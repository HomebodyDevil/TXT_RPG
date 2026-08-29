using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class StoryMessageItem : MonoBehaviour
    {
        [SerializeField] private TMP_Text speakerText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private GameObject speakerBodySpacer;
        [SerializeField] private LayoutElement speakerBodySpacerLayout;
        [SerializeField] private GameObject separatorSlot;
        [SerializeField] private RectTransform separatorVisual;
        [SerializeField] private Image separatorImage;
        [SerializeField] private LayoutElement separatorLayout;

        public void Bind(in StoryMessage message)
        {
            bodyText.text = message.Text;
            speakerText.gameObject.SetActive(message.HasSpeaker);
            if (speakerBodySpacer != null)
            {
                speakerBodySpacer.SetActive(message.HasSpeaker);
            }

            if (message.HasSpeaker)
            {
                speakerText.text = message.Speaker;
            }
        }

        public void SetOpacity(float opacity)
        {
            canvasGroup.alpha = Mathf.Clamp01(opacity);
        }

        public void ConfigureTextSize(float speakerFontSize, float bodyFontSize)
        {
            speakerText.fontSize = Mathf.Max(1f, speakerFontSize);
            bodyText.fontSize = Mathf.Max(1f, bodyFontSize);
        }

        public void ConfigureSpeakerColor(Color color)
        {
            speakerText.color = color;
        }

        public void ConfigureSpeakerBodySpacing(float spacing)
        {
            if (!TryEnsureSpeakerBodySpacer())
            {
                return;
            }

            var clampedSpacing = Mathf.Max(0f, spacing);
            speakerBodySpacerLayout.minHeight = clampedSpacing;
            speakerBodySpacerLayout.preferredHeight = clampedSpacing;
            speakerBodySpacer.SetActive(speakerText.gameObject.activeSelf && clampedSpacing > 0f);

            var layout = GetComponent<VerticalLayoutGroup>();
            if (layout != null)
            {
                layout.spacing = 0f;
                var padding = layout.padding;
                padding.bottom = 0;
                layout.padding = padding;
            }
        }

        public void ConfigureSeparator(
            bool visible,
            Sprite sprite,
            Color color,
            Image.Type imageType,
            float width,
            float height,
            float spacingAbove,
            float spacingBelow)
        {
            if (!TryEnsureSeparatorReferences())
            {
                return;
            }

            separatorSlot.SetActive(visible);
            if (!visible)
            {
                return;
            }

            var clampedWidth = Mathf.Max(1f, width);
            var clampedHeight = Mathf.Max(1f, height);
            var clampedSpacingAbove = Mathf.Max(0f, spacingAbove);
            var clampedSpacingBelow = Mathf.Max(0f, spacingBelow);
            var slotHeight = clampedSpacingAbove + clampedHeight + clampedSpacingBelow;
            separatorLayout.minHeight = slotHeight;
            separatorLayout.preferredHeight = slotHeight;
            separatorVisual.sizeDelta = new Vector2(clampedWidth, clampedHeight);
            separatorVisual.anchoredPosition = new Vector2(
                0f,
                (clampedSpacingBelow - clampedSpacingAbove) * 0.5f);
            separatorImage.sprite = sprite;
            separatorImage.color = color;
            separatorImage.type = imageType;
        }

        private bool TryEnsureSpeakerBodySpacer()
        {
            if (speakerBodySpacer != null && speakerBodySpacerLayout != null)
            {
                return true;
            }

            var existingSpacer = transform.Find("Speaker Body Spacing");
            if (existingSpacer != null)
            {
                speakerBodySpacer = existingSpacer.gameObject;
                speakerBodySpacerLayout = existingSpacer.GetComponent<LayoutElement>();
                return speakerBodySpacerLayout != null;
            }

            speakerBodySpacer = new GameObject(
                "Speaker Body Spacing",
                typeof(RectTransform),
                typeof(LayoutElement));
            speakerBodySpacer.layer = gameObject.layer;
            speakerBodySpacer.transform.SetParent(transform, false);
            speakerBodySpacer.transform.SetSiblingIndex(speakerText.transform.GetSiblingIndex() + 1);
            speakerBodySpacerLayout = speakerBodySpacer.GetComponent<LayoutElement>();
            speakerBodySpacerLayout.flexibleHeight = 0f;
            return true;
        }

        private bool TryEnsureSeparatorReferences()
        {
            if (separatorSlot != null && separatorVisual != null &&
                separatorImage != null && separatorLayout != null)
            {
                RemoveDuplicateSeparatorSlots(separatorSlot.transform);
                return true;
            }

            Transform existingSlot = null;
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.name != "Separator")
                {
                    continue;
                }

                if (existingSlot == null)
                {
                    existingSlot = child;
                    continue;
                }

                DestroySeparatorObject(child.gameObject);
            }

            if (existingSlot != null)
            {
                separatorSlot = existingSlot.gameObject;
                separatorLayout = existingSlot.GetComponent<LayoutElement>();
                separatorVisual = existingSlot.Find("Visual") as RectTransform;
                separatorImage = separatorVisual != null
                    ? separatorVisual.GetComponent<Image>()
                    : null;

                if (separatorLayout != null && separatorVisual != null && separatorImage != null)
                {
                    return true;
                }
            }

            if (!Application.isPlaying)
            {
                return false;
            }

            separatorSlot = new GameObject("Separator", typeof(RectTransform), typeof(LayoutElement));
            separatorSlot.layer = gameObject.layer;
            separatorSlot.transform.SetParent(transform, false);
            separatorLayout = separatorSlot.GetComponent<LayoutElement>();
            separatorLayout.flexibleHeight = 0f;

            var visualObject = new GameObject("Visual", typeof(RectTransform), typeof(Image));
            visualObject.layer = gameObject.layer;
            visualObject.transform.SetParent(separatorSlot.transform, false);
            separatorVisual = (RectTransform)visualObject.transform;
            separatorVisual.anchorMin = new Vector2(0.5f, 0.5f);
            separatorVisual.anchorMax = new Vector2(0.5f, 0.5f);
            separatorVisual.pivot = new Vector2(0.5f, 0.5f);
            separatorVisual.anchoredPosition = Vector2.zero;
            separatorImage = visualObject.GetComponent<Image>();
            separatorImage.raycastTarget = false;
            return true;
        }

        private void RemoveDuplicateSeparatorSlots(Transform retainedSlot)
        {
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child != retainedSlot && child.name == "Separator")
                {
                    DestroySeparatorObject(child.gameObject);
                }
            }
        }

        private static void DestroySeparatorObject(GameObject separatorObject)
        {
            if (Application.isPlaying)
            {
                Destroy(separatorObject);
            }
            else
            {
                DestroyImmediate(separatorObject);
            }
        }

        private void Reset()
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        private void OnValidate()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }
        }
    }
}
