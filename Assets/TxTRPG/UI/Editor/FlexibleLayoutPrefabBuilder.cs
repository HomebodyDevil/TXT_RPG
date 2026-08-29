using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.Editor
{
    public static class FlexibleLayoutPrefabBuilder
    {
        private const string PrefabFolder = "Assets/TxTRPG/UI/Prefabs";
        private const string DemoFolder = "Assets/TxTRPG/UI/DEMO/FlexibleLayoutPanel";
        private const string PanelPrefabPath = PrefabFolder + "/FlexibleLayoutPanel.prefab";
        private const string DemoPrefabPath = DemoFolder + "/FlexibleLayoutPanelDemo.prefab";
        private const string StoryDemoPath = "Assets/TxTRPG/UI/DEMO/StoryTextPanelDemo.prefab";
        private const string CharacterDemoPath = "Assets/TxTRPG/UI/DEMO/CharacterDisplayPanel/CharacterDisplayPanelDemo.prefab";
        private const string ActionDemoPath = "Assets/TxTRPG/UI/DEMO/ActionGridPanel/ActionGridPanelDemo.prefab";

        [MenuItem("Tools/TxT RPG/Rebuild Flexible Layout Prefab")]
        public static void CreateOrUpdatePrefab()
        {
            EnsureFolder(PrefabFolder);
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
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"Flexible layout prefab created at {PanelPrefabPath}.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [MenuItem("Tools/TxT RPG/Rebuild Flexible Layout Demo")]
        public static void CreateOrUpdateDemo()
        {
            EnsureFolder(DemoFolder);
            var storyPrefab = LoadRequiredPrefab(StoryDemoPath);
            var characterPrefab = LoadRequiredPrefab(CharacterDemoPath);
            var actionPrefab = LoadRequiredPrefab(ActionDemoPath);

            var root = CreateLayoutObject("FlexibleLayoutPanelDemo", "gameplay-layout-root");
            try
            {
                var rootRect = (RectTransform)root.transform;
                rootRect.sizeDelta = new Vector2(1280f, 720f);
                var background = root.AddComponent<Image>();
                background.color = new Color32(8, 10, 14, 255);
                background.raycastTarget = false;
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

                var left = CreateLayoutObject("MainContent", "gameplay-layout-main", root.transform);
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

                var story = InstantiatePanel(storyPrefab, left.transform, "StoryTextPanelArea");
                story.AddComponent<FlexibleLayoutItem>().Configure(
                    FlexibleLayoutSizeMode.Weighted,
                    65f,
                    newMinimumSize: 280f);

                var actions = InstantiatePanel(actionPrefab, left.transform, "ActionGridPanelArea");
                actions.AddComponent<FlexibleLayoutItem>().Configure(
                    FlexibleLayoutSizeMode.Weighted,
                    35f,
                    newMinimumSize: 220f);

                var character = InstantiatePanel(characterPrefab, root.transform, "CharacterDisplayPanelArea");
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
            var serialized = new SerializedObject(layout);
            serialized.FindProperty("nodeId").stringValue = nodeId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return gameObject;
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
