using UnityEditor;
using TxTRPG.Editor.Common.Menu;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.Editor
{
    public static class FlexibleLayoutPrefabBuilder
    {
        private const string PrefabFolder = "Assets/TxTRPG/UI/Prefabs";
        private const string DemoFolder = "Assets/TxTRPG/UI/DEMO/FlexibleLayoutPanel";
        private const string PanelPrefabPath = PrefabFolder + "/FlexibleLayoutPanel.prefab";
        private const string PlaceholderPrefabPath = PrefabFolder + "/FlexibleLayoutPlaceholder.prefab";
        private const string DemoPrefabPath = DemoFolder + "/FlexibleLayoutPanelDemo.prefab";
        private const string SampleMainDemoPrefabPath = DemoFolder + "/Sample_Main_FlexibleLayoutPanelDemo.prefab";
        private const string DemoStylePath = DemoFolder + "/FlexibleLayoutBackgroundDemoStyle.asset";
        private const string StoryDemoPath = "Assets/TxTRPG/UI/DEMO/StoryTextPanelDemo.prefab";
        private const string CharacterDemoPath = "Assets/TxTRPG/UI/DEMO/CharacterDisplayPanel/CharacterDisplayPanelDemo.prefab";
        private const string ActionDemoPath = "Assets/TxTRPG/UI/DEMO/ActionGridPanel/ActionGridPanelDemo.prefab";

        [MenuItem(
            TxTRPGEditorMenuPaths.UiPrefabs + "Rebuild Flexible Layout",
            false,
            TxTRPGEditorMenuPriorities.Rebuild)]
        public static void CreateOrUpdatePrefab()
        {
            EnsureFolder(PrefabFolder);
            CreateOrUpdatePlaceholderPrefab();
            var root = CreateLayoutObject("FlexibleLayoutPanel", "flexible-layout-root");
            try
            {
                ((RectTransform)root.transform).sizeDelta = new Vector2(960f, 640f);
                ConfigureLayout(
                    root.GetComponent<FlexibleLayoutPanel>(),
                    FlexibleLayoutAxis.Horizontal,
                    FlexibleLayoutAxisPolicy.Fixed,
                    720f,
                    12f,
                    new RectOffset(12, 12, 12, 12),
                    FlexibleLayoutOverflow.ShrinkBelowMinimum,
                    false,
                    TextAnchor.MiddleCenter);
                PrefabUtility.SaveAsPrefabAsset(root, PanelPrefabPath);
                Debug.Log($"Flexible layout prefab created at {PanelPrefabPath}.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [MenuItem(
            TxTRPGEditorMenuPaths.UiDemos + "Rebuild Flexible Layout Demo",
            false,
            TxTRPGEditorMenuPriorities.Rebuild)]
        public static void CreateOrUpdateDemo()
        {
            EnsureFolder(DemoFolder);
            CreateOrUpdatePlaceholderPrefab();
            var storyPrefab = LoadRequiredPrefab(StoryDemoPath);
            var characterPrefab = LoadRequiredPrefab(CharacterDemoPath);
            var actionPrefab = LoadRequiredPrefab(ActionDemoPath);
            var backgroundStyle = CreateOrUpdateDemoStyle();

            var root = CreateLayoutObject("FlexibleLayoutPanelDemo", "gameplay-layout-root");
            try
            {
                var rootRect = (RectTransform)root.transform;
                rootRect.sizeDelta = new Vector2(1280f, 720f);
                ConfigureBackground(root, backgroundStyle, new Color32(8, 10, 14, 255));
                ConfigureLayout(
                    root.GetComponent<FlexibleLayoutPanel>(),
                    FlexibleLayoutAxis.Horizontal,
                    FlexibleLayoutAxisPolicy.VerticalWhenNarrow,
                    720f,
                    16f,
                    new RectOffset(16, 16, 16, 16),
                    FlexibleLayoutOverflow.ShrinkBelowMinimum,
                    false,
                    TextAnchor.MiddleCenter);
                ConfigureBackgroundDemoContent(root.GetComponent<FlexibleLayoutPanel>(), false);

                var left = CreateLayoutObject("MainContent", "gameplay-layout-main", root.GetComponent<FlexibleLayoutPanel>().ContentRoot);
                ConfigureLayout(
                    left.GetComponent<FlexibleLayoutPanel>(),
                    FlexibleLayoutAxis.Vertical,
                    FlexibleLayoutAxisPolicy.Fixed,
                    720f,
                    12f,
                    new RectOffset(0, 0, 0, 0),
                    FlexibleLayoutOverflow.ShrinkBelowMinimum,
                    false,
                    TextAnchor.MiddleCenter);
                left.AddComponent<FlexibleLayoutItem>().Configure(
                    FlexibleLayoutSizeMode.Weighted,
                    7f,
                    newMinimumSize: 480f);

                var story = InstantiatePanel(storyPrefab, left.GetComponent<FlexibleLayoutPanel>().ContentRoot, "StoryTextPanelArea");
                story.AddComponent<FlexibleLayoutItem>().Configure(
                    FlexibleLayoutSizeMode.Weighted,
                    65f,
                    newMinimumSize: 280f);

                var actions = InstantiatePanel(actionPrefab, left.GetComponent<FlexibleLayoutPanel>().ContentRoot, "ActionGridPanelArea");
                actions.AddComponent<FlexibleLayoutItem>().Configure(
                    FlexibleLayoutSizeMode.Weighted,
                    35f,
                    newMinimumSize: 220f);

                var character = InstantiatePanel(characterPrefab, root.GetComponent<FlexibleLayoutPanel>().ContentRoot, "CharacterDisplayPanelArea");
                character.AddComponent<FlexibleLayoutItem>().Configure(
                    FlexibleLayoutSizeMode.Weighted,
                    3f,
                    newMinimumSize: 220f);

                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
                PrefabUtility.SaveAsPrefabAsset(root, DemoPrefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"Flexible layout demo created at {DemoPrefabPath}.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [MenuItem(
            TxTRPGEditorMenuPaths.UiDemos + "Rebuild Sample Main Layout Demo",
            false,
            TxTRPGEditorMenuPriorities.Rebuild)]
        public static void CreateOrUpdateSampleMainDemo()
        {
            EnsureFolder(DemoFolder);
            CreateOrUpdatePlaceholderPrefab();
            var actionPrefab = LoadRequiredPrefab(ActionDemoPath);
            var storyPrefab = LoadRequiredPrefab(StoryDemoPath);
            var characterPrefab = LoadRequiredPrefab(CharacterDemoPath);
            var backgroundStyle = CreateOrUpdateDemoStyle();
            var root = CreateLayoutObject("Sample_Main_FlexibleLayoutPanelDemo", "sample-main-layout-root");
            try
            {
                var rootRect = (RectTransform)root.transform;
                rootRect.sizeDelta = new Vector2(1920f, 1080f);
                ConfigureBackground(root, backgroundStyle, new Color32(8, 10, 14, 255));
                ConfigureLayout(root.GetComponent<FlexibleLayoutPanel>(), FlexibleLayoutAxis.Horizontal,
                    FlexibleLayoutAxisPolicy.VerticalWhenNarrow, 720f, 12f,
                    new RectOffset(12, 12, 12, 12), FlexibleLayoutOverflow.ShrinkBelowMinimum,
                    false, TextAnchor.MiddleCenter);
                ConfigureBackgroundDemoContent(root.GetComponent<FlexibleLayoutPanel>(), true);
                AddSampleColumn(root, "TMP_FlexibleLayoutPanel", "sample-main-actions", actionPrefab, "ActionGridPanelDemo", 1f, 280f);
                AddSampleColumn(root, "Text_FlexibleLayoutPanel", "sample-main-story", storyPrefab, "StoryTextPanelDemo", 3f, 560f);
                AddSampleColumn(root, "Character_FlexibleLayoutPanel", "sample-main-character", characterPrefab, "CharacterDisplayPanelDemo", 1f, 280f);
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
                PrefabUtility.SaveAsPrefabAsset(root, SampleMainDemoPrefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"Sample main flexible layout demo created at {SampleMainDemoPrefabPath}.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void AddSampleColumn(GameObject root, string columnName, string nodeId,
            GameObject demoPrefab, string demoName, float weight, float minimumSize)
        {
            var column = CreateLayoutObject(columnName, nodeId, root.GetComponent<FlexibleLayoutPanel>().ContentRoot);
            ConfigureLayout(column.GetComponent<FlexibleLayoutPanel>(), FlexibleLayoutAxis.Vertical,
                FlexibleLayoutAxisPolicy.Fixed, 720f, 0f, new RectOffset(0, 0, 0, 0),
                FlexibleLayoutOverflow.ShrinkBelowMinimum, false, TextAnchor.MiddleCenter);
            column.AddComponent<FlexibleLayoutItem>().Configure(
                FlexibleLayoutSizeMode.Weighted, weight, newMinimumSize: minimumSize);
            var demo = InstantiatePanel(demoPrefab, column.GetComponent<FlexibleLayoutPanel>().ContentRoot, demoName);
            demo.AddComponent<FlexibleLayoutItem>().Configure(FlexibleLayoutSizeMode.Weighted, 1f);
        }

        private static GameObject InstantiatePanel(GameObject prefab, Transform parent, string name)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = name;
            var rect = (RectTransform)instance.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return instance;
        }

        private static GameObject CreateLayoutObject(string name, string nodeId, Transform parent = null)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            if (parent != null)
            {
                gameObject.transform.SetParent(parent, false);
            }

            var layout = gameObject.AddComponent<FlexibleLayoutPanel>();
            var backgroundLayer = CreateLayer("BackgroundLayer", gameObject.transform);
            var clipMask = backgroundLayer.AddComponent<RectMask2D>();
            var visualRoot = CreateLayer("BackgroundVisualRoot", backgroundLayer.transform);
            var backgroundA = CreateImage("BackgroundA", visualRoot.transform, Color.clear);
            var backgroundB = CreateImage("BackgroundB", visualRoot.transform, Color.clear);
            var effectOverlay = CreateImage("BackgroundEffectOverlay", visualRoot.transform, Color.clear);
            backgroundB.gameObject.SetActive(false);
            effectOverlay.gameObject.SetActive(false);

            var background = backgroundLayer.AddComponent<FlexibleLayoutBackground>();
            var backgroundProperties = new SerializedObject(background);
            backgroundProperties.FindProperty("visualRoot").objectReferenceValue = visualRoot.transform;
            backgroundProperties.FindProperty("backgroundA").objectReferenceValue = backgroundA;
            backgroundProperties.FindProperty("backgroundB").objectReferenceValue = backgroundB;
            backgroundProperties.FindProperty("effectOverlay").objectReferenceValue = effectOverlay;
            backgroundProperties.FindProperty("clipMask").objectReferenceValue = clipMask;
            backgroundProperties.ApplyModifiedPropertiesWithoutUndo();

            var backgroundContentLayer = CreateLayer("BackgroundContentLayer", gameObject.transform);
            var backgroundContentGroup = backgroundContentLayer.GetComponent<CanvasGroup>();
            backgroundContentGroup.interactable = false;
            backgroundContentGroup.blocksRaycasts = false;
            var backgroundContentMask = backgroundContentLayer.AddComponent<RectMask2D>();
            backgroundContentMask.enabled = false;
            var backgroundContentLayout = backgroundContentLayer.AddComponent<FlexibleContentLayoutGroup>();

            var contentLayer = CreateLayer("ContentLayer", gameObject.transform);
            var contentMask = contentLayer.AddComponent<RectMask2D>();
            contentMask.enabled = false;
            var contentLayout = contentLayer.AddComponent<FlexibleContentLayoutGroup>();
            var foregroundLayer = CreateLayer("ForegroundLayer", gameObject.transform);
            CreateImage("Border", foregroundLayer.transform, Color.clear);

            var serialized = new SerializedObject(layout);
            serialized.FindProperty("nodeId").stringValue = nodeId;
            serialized.FindProperty("contentRoot").objectReferenceValue = contentLayer.transform;
            serialized.FindProperty("contentLayout").objectReferenceValue = contentLayout;
            serialized.FindProperty("contentMask").objectReferenceValue = contentMask;
            serialized.FindProperty("backgroundContentRoot").objectReferenceValue = backgroundContentLayer.transform;
            serialized.FindProperty("backgroundContentLayout").objectReferenceValue = backgroundContentLayout;
            serialized.FindProperty("backgroundContentMask").objectReferenceValue = backgroundContentMask;
            serialized.FindProperty("backgroundContentCanvasGroup").objectReferenceValue = backgroundContentGroup;
            serialized.FindProperty("useContentLayoutSettings").boolValue = true;
            serialized.FindProperty("previousUseContentLayoutSettings").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return gameObject;
        }

        private static void CreateOrUpdatePlaceholderPrefab()
        {
            var placeholder = new GameObject(
                "FlexibleLayoutPlaceholder",
                typeof(RectTransform),
                typeof(FlexibleLayoutItem));
            try
            {
                placeholder.GetComponent<FlexibleLayoutItem>().Configure(
                    FlexibleLayoutSizeMode.Weighted,
                    1f);
                PrefabUtility.SaveAsPrefabAsset(placeholder, PlaceholderPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(placeholder);
            }
        }

        private static void ConfigureBackgroundDemoContent(
            FlexibleLayoutPanel panel,
            bool useIndependentSettings)
        {
            if (useIndependentSettings)
            {
                panel.ConfigureBackgroundLayout(
                    FlexibleLayoutAxis.Horizontal,
                    FlexibleLayoutAxisPolicy.VerticalWhenNarrow,
                    680f,
                    10f,
                    new RectOffset(8, 8, 8, 8),
                    FlexibleLayoutOverflow.ShrinkBelowMinimum,
                    false,
                    TextAnchor.MiddleCenter);
            }

            var placeholderPrefab = LoadRequiredPrefab(PlaceholderPrefabPath);
            var placeholder = (GameObject)PrefabUtility.InstantiatePrefab(
                placeholderPrefab,
                panel.BackgroundContentRoot);
            placeholder.name = "ReservedBackdropSpace";
            placeholder.GetComponent<FlexibleLayoutItem>().Configure(
                FlexibleLayoutSizeMode.Weighted,
                1f,
                newMinimumSize: 80f,
                newMaximumSize: 360f);

            var region = new GameObject(
                "BackdropRegion",
                typeof(RectTransform),
                typeof(FlexibleLayoutItem));
            region.transform.SetParent(panel.BackgroundContentRoot, false);
            region.GetComponent<FlexibleLayoutItem>().Configure(
                FlexibleLayoutSizeMode.Weighted,
                1f,
                newMinimumSize: 80f);
            var visualRoot = CreateLayer("VisualRoot", region.transform);
            visualRoot.GetComponent<LayoutElement>().ignoreLayout = true;
            var image = CreateImage(
                "Tint",
                visualRoot.transform,
                new Color32(36, 76, 108, 42));
            image.raycastTarget = false;
        }

        private static GameObject CreateLayer(string name, Transform parent)
        {
            var layer = new GameObject(name, typeof(RectTransform), typeof(LayoutElement), typeof(CanvasGroup));
            layer.transform.SetParent(parent, false);
            Stretch((RectTransform)layer.transform);
            layer.GetComponent<LayoutElement>().ignoreLayout = true;
            return layer;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            var imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            Stretch((RectTransform)imageObject.transform);
            var image = imageObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static FlexibleLayoutBackgroundStyle CreateOrUpdateDemoStyle()
        {
            var style = AssetDatabase.LoadAssetAtPath<FlexibleLayoutBackgroundStyle>(DemoStylePath);
            if (style == null)
            {
                style = ScriptableObject.CreateInstance<FlexibleLayoutBackgroundStyle>();
                AssetDatabase.CreateAsset(style, DemoStylePath);
            }

            style.Configure(
                null,
                new Color32(8, 10, 14, 255),
                FlexibleLayoutBackgroundScaleMode.Stretch,
                1f,
                FlexibleLayoutBackgroundOverflowMode.ClipToPanel);
            EditorUtility.SetDirty(style);
            return style;
        }

        private static void ConfigureBackground(
            GameObject layoutObject,
            FlexibleLayoutBackgroundStyle style,
            Color previewColor)
        {
            var background = layoutObject.GetComponentInChildren<FlexibleLayoutBackground>(true);
            var properties = new SerializedObject(background);
            properties.FindProperty("initialStyle").objectReferenceValue = style;
            properties.ApplyModifiedPropertiesWithoutUndo();
            var image = background.transform.Find("BackgroundVisualRoot/BackgroundA")?.GetComponent<Image>();
            if (image != null)
            {
                image.color = previewColor;
            }
        }

        private static void ConfigureLayout(
            FlexibleLayoutPanel layout,
            FlexibleLayoutAxis axis,
            FlexibleLayoutAxisPolicy policy,
            float breakpoint,
            float spacing,
            RectOffset padding,
            FlexibleLayoutOverflow overflow,
            bool includeInactive,
            TextAnchor alignment)
        {
            layout.padding = padding;
            layout.childAlignment = alignment;
            layout.ContentLayout.Configure(axis, policy, breakpoint, spacing, padding, overflow,
                includeInactive, alignment);
            var serialized = new SerializedObject(layout);
            serialized.FindProperty("fixedAxis").enumValueIndex = (int)axis;
            serialized.FindProperty("axisPolicy").enumValueIndex = (int)policy;
            serialized.FindProperty("breakpoint").floatValue = breakpoint;
            serialized.FindProperty("spacing").floatValue = spacing;
            serialized.FindProperty("overflow").enumValueIndex = (int)overflow;
            serialized.FindProperty("includeInactiveChildren").boolValue = includeInactive;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject LoadRequiredPrefab(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                throw new UnityException($"Required panel demo was not found at {path}.");
            }

            return prefab;
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
