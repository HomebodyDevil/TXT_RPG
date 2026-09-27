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
        public const string ProfilePath = "Assets/TxTRPG/UI/Styles/ExplorationCardDefaultPresentation.asset";

        [MenuItem("Tools/TxT RPG/UI/Exploration/Temporary/Upgrade Card Readability And Pulse")]
        public static void UpgradeReadabilityAndPulse()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before upgrading the card.");
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == PrefabPath) throw new InvalidOperationException("Save and close the card Prefab Stage before upgrading.");
            var profile = AssetDatabase.LoadAssetAtPath<ExplorationCardPresentationProfile>(ProfilePath);
            if (profile == null || EditorUtility.IsDirty(profile)) throw new InvalidOperationException("Save the default profile before upgrading.");
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var visual = root.transform.Find("MotionRoot/VisualRoot");
                var feedback = root.GetComponent<ExplorationCardFeedbackController>();
                if (visual == null || feedback == null) throw new InvalidOperationException("Card references are incomplete.");
                var backdrop = visual.Find("TextBackdrop") as RectTransform;
                if (backdrop == null)
                {
                    backdrop = Rect("TextBackdrop", visual);
                    backdrop.anchorMin = Vector2.zero; backdrop.anchorMax = new Vector2(1f, .47f);
                    backdrop.offsetMin = backdrop.offsetMax = Vector2.zero;
                    var image = backdrop.gameObject.AddComponent<Image>();
                    image.color = new Color(.105f, .14f, .21f, 1f); image.raycastTarget = false;
                    backdrop.SetSiblingIndex(visual.Find("Title").GetSiblingIndex());
                }
                UpgradeBorder(visual.Find("Border").gameObject, feedback, false);
                UpgradeBorder(visual.Find("FocusVisual").gameObject, feedback, true);
                var badge = visual.Find("StatusBadge").GetComponent<Image>();
                if (badge.color == new Color(.55f, .31f, .12f, .95f)) badge.color = new Color(.28f, .16f, .06f, 1f);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            // Only the shipped default profile is migrated. Never touch custom profiles or variants.
            var serialized = new SerializedObject(profile);
            serialized.FindProperty("pulseEnabled").boolValue = true;
            serialized.FindProperty("pulseMinScale").floatValue = 1f;
            serialized.FindProperty("pulseMaxScale").floatValue = 1.03f;
            serialized.FindProperty("pulsePeriod").floatValue = 1.8f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile); // New fields may equal their C# defaults and still need serialization.
            AssetDatabase.SaveAssetIfDirty(profile);
            ValidatePrefab();
            Debug.Log("Card readability and pulse upgraded. Scene layout and custom profiles were preserved.");
        }

        private static void UpgradeBorder(GameObject target, ExplorationCardFeedbackController feedback, bool focus)
        {
            if (target.GetComponent<ExplorationCardShapeGraphic>() != null) return;
            var old = target.GetComponent<Image>();
            if (old == null) throw new InvalidOperationException("Expected legacy card decoration Image.");
            var serialized = new SerializedObject(feedback);
            var targets = serialized.FindProperty("tintTargets");
            var indices = new System.Collections.Generic.List<int>();
            for (var i = 0; i < targets.arraySize; i++) if (targets.GetArrayElementAtIndex(i).objectReferenceValue == old) indices.Add(i);
            foreach (var effect in target.GetComponents<BaseMeshEffect>()) UnityEngine.Object.DestroyImmediate(effect);
            UnityEngine.Object.DestroyImmediate(old);
            var graphic = target.AddComponent<ExplorationCardShapeGraphic>();
            graphic.color = focus ? new Color(.4f, .68f, 1f, 1f) : new Color(.65f, .78f, 1f, .65f);
            graphic.raycastTarget = false;
            graphic.Configure(ExplorationCardShape.Rectangle, 0f, ExplorationCardShapeDrawMode.InnerBorder, focus ? 3f : 2f);
            foreach (var index in indices) targets.GetArrayElementAtIndex(index).objectReferenceValue = graphic;
            if (focus) serialized.FindProperty("focusVisual").objectReferenceValue = graphic;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("Tools/TxT RPG/UI/Exploration/Rebuild Node Choice Card Prefab")]
        public static void CreateOrUpdatePrefab()
        {
            var profile = AssetDatabase.LoadAssetAtPath<ExplorationCardPresentationProfile>(ProfilePath);
            if (profile == null) { profile = ScriptableObject.CreateInstance<ExplorationCardPresentationProfile>(); AssetDatabase.CreateAsset(profile, ProfilePath); }
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

                var textBackdrop = Rect("TextBackdrop", visual);
                textBackdrop.anchorMin = Vector2.zero; textBackdrop.anchorMax = new Vector2(1f, .47f);
                textBackdrop.offsetMin = textBackdrop.offsetMax = Vector2.zero;
                var textBackground = textBackdrop.gameObject.AddComponent<Image>();
                textBackground.color = new Color(.105f, .14f, .21f, 1f); textBackground.raycastTarget = false;

                var titleRect = Rect("Title", visual);
                titleRect.anchorMin = new Vector2(0f, .31f); titleRect.anchorMax = new Vector2(1f, .47f);
                titleRect.offsetMin = new Vector2(14f, 0f); titleRect.offsetMax = new Vector2(-14f, 0f);
                var title = Text(titleRect, "노드", 22f, FontStyles.Bold);
                title.gameObject.AddComponent<ExplorationCardTextEffectController>().ConfigureForEditor(title, ExplorationCardTextRole.Title, profile);

                var descriptionRect = Rect("Description", visual);
                descriptionRect.anchorMin = new Vector2(0f, .09f); descriptionRect.anchorMax = new Vector2(1f, .31f);
                descriptionRect.offsetMin = new Vector2(14f, 6f); descriptionRect.offsetMax = new Vector2(-14f, 0f);
                var description = Text(descriptionRect, "노드 설명", 16f, FontStyles.Normal);
                description.gameObject.AddComponent<ExplorationCardTextEffectController>().ConfigureForEditor(description, ExplorationCardTextRole.Description, profile);

                var badgeRect = Rect("StatusBadge", visual);
                badgeRect.anchorMin = badgeRect.anchorMax = new Vector2(.5f, .05f); badgeRect.pivot = new Vector2(.5f, .5f);
                badgeRect.sizeDelta = new Vector2(112f, 30f);
                var badgeImage = badgeRect.gameObject.AddComponent<Image>(); badgeImage.color = new Color(.28f, .16f, .06f, 1f); badgeImage.raycastTarget = false;
                var statusRect = Rect("Text", badgeRect); Stretch(statusRect, 6f, 2f, 6f, 2f);
                var status = Text(statusRect, "미구현", 14f, FontStyles.Bold);
                status.gameObject.AddComponent<ExplorationCardTextEffectController>().ConfigureForEditor(status, ExplorationCardTextRole.Status, profile);

                var borderRect = Rect("Border", visual); Stretch(borderRect);
                var border = borderRect.gameObject.AddComponent<ExplorationCardShapeGraphic>(); border.color = new Color(.65f, .78f, 1f, .65f); border.raycastTarget = false;
                border.Configure(ExplorationCardShape.Rectangle, 0f, ExplorationCardShapeDrawMode.InnerBorder, 2f);

                var focusRect = Rect("FocusVisual", visual); Stretch(focusRect, 4f, 4f, 4f, 4f);
                var focus = focusRect.gameObject.AddComponent<ExplorationCardShapeGraphic>(); focus.color = new Color(.4f, .68f, 1f, 1f); focus.raycastTarget = false;
                focus.Configure(ExplorationCardShape.Rectangle, 0f, ExplorationCardShapeDrawMode.InnerBorder, 3f);
                var effectRect = Rect("EffectOverlay", visual); Stretch(effectRect);
                var effect = effectRect.gameObject.AddComponent<Image>(); effect.color = Color.clear; effect.raycastTarget = false;

                var shapePresentation = visual.gameObject.AddComponent<ExplorationCardShapePresentation>();
                shapePresentation.ConfigureForEditor(shapeVisual, background, shapeMask, shapeBorder, ExplorationCardShapeSettings.Default);

                var button = root.gameObject.AddComponent<Button>(); button.targetGraphic = background; button.transition = Selectable.Transition.None;
                var feedback = root.gameObject.AddComponent<ExplorationCardFeedbackController>();
                feedback.ConfigureForEditor(motion, button, new Graphic[] { background, shapeMask, shapeBorder, border }, focus, profile);
                var view = root.gameObject.AddComponent<ExplorationNodeChoiceCardView>();
                view.ConfigureForEditor(button, motion, canvasGroup, artwork, shapeVisual.gameObject, title, description, status, shapePresentation, feedback);
                PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root.gameObject); }
        }

        public static void ValidatePrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) ?? throw new InvalidOperationException("Exploration node choice card prefab is missing.");
            var view = prefab.GetComponent<ExplorationNodeChoiceCardView>() ?? throw new InvalidOperationException("Card View is missing.");
            if (view.Button == null || view.ShapePresentation == null || view.Feedback == null || prefab.transform.Find("MotionRoot/VisualRoot/ShapeVisual/Artwork") == null || prefab.transform.Find("MotionRoot/VisualRoot/ShapeBorder") == null || prefab.transform.Find("MotionRoot/VisualRoot/EffectOverlay") == null)
                throw new InvalidOperationException("Exploration node choice card references are incomplete.");
        }

        private static RectTransform Rect(string name, Transform parent) { var go = new GameObject(name, typeof(RectTransform)); if (parent != null) go.transform.SetParent(parent, false); return (RectTransform)go.transform; }
        private static void Stretch(RectTransform rect, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, -top); }
        private static TMP_Text Text(RectTransform rect, string value, float size, FontStyles style) { var text = rect.gameObject.AddComponent<TextMeshProUGUI>(); text.text = value; text.fontSize = size; text.fontStyle = style; text.alignment = TextAlignmentOptions.Center; text.enableWordWrapping = true; text.overflowMode = TextOverflowModes.Ellipsis; text.raycastTarget = false; return text; }
    }
}
