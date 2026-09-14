using System;
using TMPro;
using TxTRPG.UI.Exploration;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.Editor
{
    public static class ExplorationNodeChoiceCardPrefabBuilder
    {
        public const string PrefabPath = "Assets/TxTRPG/UI/Prefabs/ExplorationNodeChoiceCard.prefab";

        [MenuItem("Tools/TxT RPG/UI/Exploration/Rebuild Node Choice Card Prefab")]
        public static void CreateOrUpdatePrefab()
        {
            var root = Rect("ExplorationNodeChoiceCard", null);
            try
            {
                root.sizeDelta = new Vector2(210f, 300f);
                var layoutElement = root.gameObject.AddComponent<LayoutElement>();
                layoutElement.preferredWidth = 210f; layoutElement.preferredHeight = 300f;
                var motion = Rect("MotionRoot", root); Stretch(motion);
                var visual = Rect("VisualRoot", motion); Stretch(visual);
                var canvasGroup = visual.gameObject.AddComponent<CanvasGroup>();

                var backgroundRect = Rect("Background", visual); Stretch(backgroundRect);
                var background = backgroundRect.gameObject.AddComponent<Image>();
                background.color = new Color(.105f, .14f, .21f, 1f);

                var shapeVisual = Rect("ShapeVisual", visual);
                shapeVisual.anchorMin = shapeVisual.anchorMax = new Vector2(.5f, .74f);
                shapeVisual.pivot = new Vector2(.5f, .5f); shapeVisual.sizeDelta = new Vector2(186f, 144f);
                shapeVisual.gameObject.AddComponent<CanvasRenderer>();
                var shapeMask = shapeVisual.gameObject.AddComponent<ExplorationCardShapeGraphic>();
                shapeMask.color = new Color(.105f, .14f, .21f, 1f); shapeMask.raycastTarget = false;
                shapeVisual.gameObject.AddComponent<Mask>().showMaskGraphic = true;
                var artworkRect = Rect("Artwork", shapeVisual); Stretch(artworkRect);
                var artwork = artworkRect.gameObject.AddComponent<Image>();
                artwork.color = new Color(.16f, .2f, .28f, 1f); artwork.preserveAspect = true; artwork.raycastTarget = false;
                var shapeBorderRect = Rect("ShapeBorder", visual);
                shapeBorderRect.anchorMin = shapeBorderRect.anchorMax = shapeVisual.anchorMin;
                shapeBorderRect.pivot = shapeVisual.pivot; shapeBorderRect.sizeDelta = shapeVisual.sizeDelta;
                shapeBorderRect.gameObject.AddComponent<CanvasRenderer>();
                var shapeBorder = shapeBorderRect.gameObject.AddComponent<ExplorationCardShapeGraphic>();
                shapeBorder.color = new Color(.65f, .78f, 1f, .65f); shapeBorder.raycastTarget = false;
                shapeBorder.Configure(ExplorationCardShape.RoundedRectangle, 14f, ExplorationCardShapeDrawMode.InnerBorder, 2f);

                var titleRect = Rect("Title", visual);
                titleRect.anchorMin = new Vector2(0f, .31f); titleRect.anchorMax = new Vector2(1f, .47f);
                titleRect.offsetMin = new Vector2(14f, 0f); titleRect.offsetMax = new Vector2(-14f, 0f);
                var title = Text(titleRect, "노드", 22f, FontStyles.Bold);

                var descriptionRect = Rect("Description", visual);
                descriptionRect.anchorMin = new Vector2(0f, .09f); descriptionRect.anchorMax = new Vector2(1f, .31f);
                descriptionRect.offsetMin = new Vector2(14f, 6f); descriptionRect.offsetMax = new Vector2(-14f, 0f);
                var description = Text(descriptionRect, "노드 설명", 16f, FontStyles.Normal);

                var badgeRect = Rect("StatusBadge", visual);
                badgeRect.anchorMin = badgeRect.anchorMax = new Vector2(.5f, .05f); badgeRect.pivot = new Vector2(.5f, .5f);
                badgeRect.sizeDelta = new Vector2(112f, 30f);
                var badgeImage = badgeRect.gameObject.AddComponent<Image>(); badgeImage.color = new Color(.55f, .31f, .12f, .95f); badgeImage.raycastTarget = false;
                var statusRect = Rect("Text", badgeRect); Stretch(statusRect, 6f, 2f, 6f, 2f);
                var status = Text(statusRect, "미구현", 14f, FontStyles.Bold);

                var borderRect = Rect("Border", visual); Stretch(borderRect);
                var border = borderRect.gameObject.AddComponent<Image>(); border.color = new Color(.55f, .68f, .9f, .38f); border.raycastTarget = false;
                var outline = borderRect.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(.65f, .78f, 1f, .65f); outline.effectDistance = new Vector2(2f, -2f);

                var focusRect = Rect("FocusVisual", visual); Stretch(focusRect, 4f, 4f, 4f, 4f);
                var focus = focusRect.gameObject.AddComponent<Image>(); focus.color = new Color(.4f, .68f, 1f, .08f); focus.raycastTarget = false;
                var effectRect = Rect("EffectOverlay", visual); Stretch(effectRect);
                var effect = effectRect.gameObject.AddComponent<Image>(); effect.color = Color.clear; effect.raycastTarget = false;

                var shapePresentation = visual.gameObject.AddComponent<ExplorationCardShapePresentation>();
                shapePresentation.ConfigureForEditor(shapeVisual, background, shapeMask, shapeBorder, ExplorationCardShapeSettings.Default);

                var button = root.gameObject.AddComponent<Button>(); button.targetGraphic = background;
                button.transition = Selectable.Transition.ColorTint;
                var colors = button.colors; colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f); colors.selectedColor = new Color(1.12f, 1.12f, 1.12f, 1f); colors.pressedColor = new Color(.82f, .88f, 1f, 1f); button.colors = colors;
                var view = root.gameObject.AddComponent<ExplorationNodeChoiceCardView>();
                view.ConfigureForEditor(button, motion, canvasGroup, artwork, shapeVisual.gameObject, title, description, status, shapePresentation);
                PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root.gameObject); }
        }

        public static void ValidatePrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) ?? throw new InvalidOperationException("Exploration node choice card prefab is missing.");
            var view = prefab.GetComponent<ExplorationNodeChoiceCardView>() ?? throw new InvalidOperationException("Card View is missing.");
            if (view.Button == null || view.ShapePresentation == null || prefab.transform.Find("MotionRoot/VisualRoot/ShapeVisual/Artwork") == null || prefab.transform.Find("MotionRoot/VisualRoot/ShapeBorder") == null || prefab.transform.Find("MotionRoot/VisualRoot/EffectOverlay") == null)
                throw new InvalidOperationException("Exploration node choice card references are incomplete.");
        }

        private static RectTransform Rect(string name, Transform parent) { var go = new GameObject(name, typeof(RectTransform)); if (parent != null) go.transform.SetParent(parent, false); return (RectTransform)go.transform; }
        private static void Stretch(RectTransform rect, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, -top); }
        private static TMP_Text Text(RectTransform rect, string value, float size, FontStyles style) { var text = rect.gameObject.AddComponent<TextMeshProUGUI>(); text.text = value; text.fontSize = size; text.fontStyle = style; text.alignment = TextAlignmentOptions.Center; text.enableWordWrapping = true; text.overflowMode = TextOverflowModes.Ellipsis; text.raycastTarget = false; return text; }
    }
}
