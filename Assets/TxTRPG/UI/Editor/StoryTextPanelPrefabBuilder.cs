using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.Editor
{
    public static class StoryTextPanelPrefabBuilder
    {
        private const string PrefabFolder = "Assets/TxTRPG/UI/Prefabs";
        private const string MessagePrefabPath = PrefabFolder + "/StoryMessageItem.prefab";
        private const string PanelPrefabPath = PrefabFolder + "/StoryTextPanel.prefab";

        [MenuItem("Tools/TxT RPG/Rebuild Story Text Panel Prefabs")]
        public static void CreateOrUpdatePrefabs()
        {
            EnsureFolder(PrefabFolder);
            var messagePrefab = BuildMessagePrefab();
            BuildPanelPrefab(messagePrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Story text prefabs created at {PrefabFolder}.");
        }

        private static StoryMessageItem BuildMessagePrefab()
        {
            var root = CreateUiObject("StoryMessageItem");
            try
            {
                var rootRect = (RectTransform)root.transform;
                rootRect.sizeDelta = new Vector2(0f, 72f);

                var canvasGroup = root.AddComponent<CanvasGroup>();
                var layout = root.AddComponent<VerticalLayoutGroup>();
                layout.padding = new RectOffset(8, 12, 6, 10);
                layout.spacing = 3f;
                layout.childAlignment = TextAnchor.UpperLeft;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;

                var fitter = root.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                var speaker = CreateText("Speaker", root.transform, 17f, new Color32(218, 154, 93, 255));
                speaker.fontStyle = FontStyles.Bold;
                speaker.text = "Speaker";

                var body = CreateText("Body", root.transform, 24f, new Color32(235, 230, 218, 255));
                body.text = "Story text appears here.";
                body.lineSpacing = 14f;

                var separatorSlot = CreateUiObject("Separator", root.transform);
                var separatorSlotRect = (RectTransform)separatorSlot.transform;
                separatorSlotRect.sizeDelta = new Vector2(0f, 1f);
                var separatorLayout = separatorSlot.AddComponent<LayoutElement>();
                separatorLayout.minHeight = 1f;
                separatorLayout.preferredHeight = 1f;
                separatorLayout.flexibleHeight = 0f;

                var separatorVisualObject = CreateUiObject("Visual", separatorSlot.transform);
                var separatorVisual = (RectTransform)separatorVisualObject.transform;
                separatorVisual.anchorMin = new Vector2(0.5f, 0.5f);
                separatorVisual.anchorMax = new Vector2(0.5f, 0.5f);
                separatorVisual.pivot = new Vector2(0.5f, 0.5f);
                separatorVisual.anchoredPosition = Vector2.zero;
                separatorVisual.sizeDelta = new Vector2(360f, 1f);
                var separatorImage = separatorVisualObject.AddComponent<Image>();
                separatorImage.color = new Color(1f, 1f, 1f, 0.2f);
                separatorImage.raycastTarget = false;

                var component = root.AddComponent<StoryMessageItem>();
                var serialized = new SerializedObject(component);
                serialized.FindProperty("speakerText").objectReferenceValue = speaker;
                serialized.FindProperty("bodyText").objectReferenceValue = body;
                serialized.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
                serialized.FindProperty("separatorSlot").objectReferenceValue = separatorSlot;
                serialized.FindProperty("separatorVisual").objectReferenceValue = separatorVisual;
                serialized.FindProperty("separatorImage").objectReferenceValue = separatorImage;
                serialized.FindProperty("separatorLayout").objectReferenceValue = separatorLayout;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, MessagePrefabPath);
                return prefab.GetComponent<StoryMessageItem>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void BuildPanelPrefab(StoryMessageItem messagePrefab)
        {
            var root = CreateUiObject("StoryTextPanel");
            try
            {
                var rootRect = (RectTransform)root.transform;
                rootRect.sizeDelta = new Vector2(760f, 520f);

                var background = root.AddComponent<Image>();
                background.color = new Color32(15, 18, 24, 235);

                var viewport = CreateUiObject("Viewport", root.transform);
                var viewportRect = (RectTransform)viewport.transform;
                Stretch(viewportRect, 24f, 24f, 50f, 24f);
                var viewportImage = viewport.AddComponent<Image>();
                viewportImage.color = new Color(0f, 0f, 0f, 0.001f);
                viewport.AddComponent<RectMask2D>();

                var content = CreateUiObject("Content", viewport.transform);
                var contentRect = (RectTransform)content.transform;
                contentRect.anchorMin = new Vector2(0f, 0f);
                contentRect.anchorMax = new Vector2(1f, 0f);
                contentRect.pivot = new Vector2(0.5f, 0f);
                contentRect.anchoredPosition = Vector2.zero;
                contentRect.sizeDelta = Vector2.zero;

                var contentLayout = content.AddComponent<VerticalLayoutGroup>();
                contentLayout.padding = new RectOffset(0, 0, 0, 0);
                contentLayout.spacing = 12f;
                contentLayout.childAlignment = TextAnchor.LowerLeft;
                contentLayout.childControlWidth = true;
                contentLayout.childControlHeight = true;
                contentLayout.childForceExpandWidth = true;
                contentLayout.childForceExpandHeight = false;

                var contentFitter = content.AddComponent<ContentSizeFitter>();
                contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                var scrollbar = BuildScrollbar(root.transform, out var scrollbarBackground);

                var scrollRect = root.AddComponent<ScrollRect>();
                scrollRect.content = contentRect;
                scrollRect.viewport = viewportRect;
                scrollRect.horizontal = false;
                scrollRect.vertical = true;
                scrollRect.movementType = ScrollRect.MovementType.Clamped;
                scrollRect.inertia = true;
                scrollRect.decelerationRate = 0.135f;
                scrollRect.scrollSensitivity = 32f;

                var panel = root.AddComponent<StoryTextPanel>();
                var serialized = new SerializedObject(panel);
                serialized.FindProperty("messagePrefab").objectReferenceValue = messagePrefab;
                serialized.FindProperty("viewport").objectReferenceValue = viewportRect;
                serialized.FindProperty("content").objectReferenceValue = contentRect;
                serialized.FindProperty("scrollRect").objectReferenceValue = scrollRect;
                serialized.FindProperty("scrollbar").objectReferenceValue = scrollbar;
                serialized.FindProperty("scrollbarBackground").objectReferenceValue = scrollbarBackground;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PanelPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Scrollbar BuildScrollbar(Transform parent, out Image background)
        {
            var root = CreateUiObject("Scrollbar", parent);
            var rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = new Vector2(1f, 0f);
            rootRect.anchorMax = Vector2.one;
            rootRect.pivot = new Vector2(1f, 0.5f);
            rootRect.sizeDelta = new Vector2(18f, 0f);
            rootRect.anchoredPosition = Vector2.zero;

            background = root.AddComponent<Image>();
            background.color = new Color32(255, 255, 255, 20);

            var slidingArea = CreateUiObject("Sliding Area", root.transform);
            Stretch((RectTransform)slidingArea.transform, 3f, 3f, 3f, 3f);

            var handle = CreateUiObject("Handle", slidingArea.transform);
            var handleRect = (RectTransform)handle.transform;
            Stretch(handleRect);
            var handleImage = handle.AddComponent<Image>();
            handleImage.color = new Color32(213, 137, 75, 230);

            var scrollbar = root.AddComponent<Scrollbar>();
            scrollbar.handleRect = handleRect;
            scrollbar.targetGraphic = handleImage;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.numberOfSteps = 0;
            scrollbar.size = 0.18f;
            scrollbar.value = 0f;
            return scrollbar;
        }

        private static TextMeshProUGUI CreateText(
            string name,
            Transform parent,
            float fontSize,
            Color color)
        {
            var gameObject = CreateUiObject(name, parent);
            var text = gameObject.AddComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static GameObject CreateUiObject(string name, Transform parent = null)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            if (parent != null)
            {
                gameObject.transform.SetParent(parent, false);
            }

            return gameObject;
        }

        private static void Stretch(
            RectTransform rect,
            float left = 0f,
            float bottom = 0f,
            float right = 0f,
            float top = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void EnsureFolder(string path)
        {
            var segments = path.Split('/');
            var current = segments[0];
            for (var i = 1; i < segments.Length; i++)
            {
                var next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[i]);
                }

                current = next;
            }
        }
    }
}
