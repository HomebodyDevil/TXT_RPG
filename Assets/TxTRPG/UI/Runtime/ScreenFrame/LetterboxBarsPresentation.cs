using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.ScreenFrame
{
    // Default letterbox presentation: four bars (Top, Bottom, Left, Right), each with VisualRoot/Image. The bar
    // RectTransforms follow the layout; effects belong on VisualRoot. A bar of size zero turns its Image off. Assign a
    // Sprite on a bar's Image to texture it.
    [DisallowMultipleComponent]
    public sealed class LetterboxBarsPresentation : MonoBehaviour, ILetterboxPresentation
    {
        [SerializeField] private RectTransform top;
        [SerializeField] private RectTransform bottom;
        [SerializeField] private RectTransform left;
        [SerializeField] private RectTransform right;
        [SerializeField] private Image topImage;
        [SerializeField] private Image bottomImage;
        [SerializeField] private Image leftImage;
        [SerializeField] private Image rightImage;
        [SerializeField] private Color color = Color.black;

        public Color Color => color;
        public RectTransform Top => top;
        public RectTransform Bottom => bottom;
        public RectTransform Left => left;
        public RectTransform Right => right;
        public Image TopImage => topImage;
        public Image BottomImage => bottomImage;
        public Image LeftImage => leftImage;
        public Image RightImage => rightImage;

        // Builds the bar hierarchy under parent (used for the fallback bars and by the asset application tool).
        public static LetterboxBarsPresentation Create(Transform parent, Color barColor)
        {
            var root = new GameObject("LetterboxBarsPresentation", typeof(RectTransform));
            var rootRect = (RectTransform)root.transform;
            rootRect.SetParent(parent, false);
            StretchFull(rootRect);
            var presentation = root.AddComponent<LetterboxBarsPresentation>();
            presentation.color = barColor;
            presentation.top = CreateBar(rootRect, "Top", barColor, out presentation.topImage);
            presentation.bottom = CreateBar(rootRect, "Bottom", barColor, out presentation.bottomImage);
            presentation.left = CreateBar(rootRect, "Left", barColor, out presentation.leftImage);
            presentation.right = CreateBar(rootRect, "Right", barColor, out presentation.rightImage);
            return presentation;
        }

        public void SetColor(Color value)
        {
            color = value;
            ApplyColor();
        }

        public void ApplyLayout(ScreenFrameLayout layout)
        {
            if (!layout.IsValid) return;
            Place(top, topImage, layout.TopBar);
            Place(bottom, bottomImage, layout.BottomBar);
            Place(left, leftImage, layout.LeftBar);
            Place(right, rightImage, layout.RightBar);
        }

        private void Awake() => ApplyColor();

        private void ApplyColor()
        {
            SetImageColor(topImage);
            SetImageColor(bottomImage);
            SetImageColor(leftImage);
            SetImageColor(rightImage);
        }

        private void SetImageColor(Image image)
        {
            if (image != null && image.color != color) image.color = color;
        }

        private static void Place(RectTransform bar, Image image, Rect area)
        {
            if (bar != null)
            {
                if (bar.anchorMin != Vector2.zero) bar.anchorMin = Vector2.zero;
                if (bar.anchorMax != Vector2.zero) bar.anchorMax = Vector2.zero;
                if (bar.pivot != Vector2.zero) bar.pivot = Vector2.zero;
                if (bar.anchoredPosition != area.position) bar.anchoredPosition = area.position;
                if (bar.sizeDelta != area.size) bar.sizeDelta = area.size;
            }
            if (image != null)
            {
                var visible = area.width > 0f && area.height > 0f;
                if (image.enabled != visible) image.enabled = visible;
            }
        }

        private static RectTransform CreateBar(RectTransform parent, string barName, Color barColor, out Image image)
        {
            var bar = new GameObject(barName, typeof(RectTransform));
            var barRect = (RectTransform)bar.transform;
            barRect.SetParent(parent, false);
            barRect.anchorMin = barRect.anchorMax = barRect.pivot = Vector2.zero;
            barRect.sizeDelta = Vector2.zero;
            var visualRoot = new GameObject("VisualRoot", typeof(RectTransform));
            var visualRect = (RectTransform)visualRoot.transform;
            visualRect.SetParent(barRect, false);
            StretchFull(visualRect);
            var imageObject = new GameObject("Image", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var imageRect = (RectTransform)imageObject.transform;
            imageRect.SetParent(visualRect, false);
            StretchFull(imageRect);
            image = imageObject.GetComponent<Image>();
            image.color = barColor;
            image.raycastTarget = false;
            image.enabled = false;
            return barRect;
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }
    }
}
