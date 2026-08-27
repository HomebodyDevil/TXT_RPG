using UnityEditor;
using UnityEngine;

namespace TxTRPG.UI.Editor
{
    public static class StoryTextPanelDemoBuilder
    {
        private const string DemoFolder = "Assets/TxTRPG/UI/Demo";
        private const string PanelPrefabPath = "Assets/TxTRPG/UI/Prefabs/StoryTextPanel.prefab";
        private const string DemoDataPath = DemoFolder + "/StoryTextPanelDemoData.asset";
        private const string DemoPrefabPath = DemoFolder + "/StoryTextPanelDemo.prefab";

        [MenuItem("Tools/TxT RPG/Rebuild Story Text Panel Demo")]
        public static void CreateOrUpdateDemo()
        {
            EnsureFolder(DemoFolder);
            var demoData = BuildDemoData();
            BuildDemoPrefab(demoData);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"StoryTextPanel demo created at {DemoFolder}.");
        }

        private static StoryTextPanelDemoData BuildDemoData()
        {
            var data = AssetDatabase.LoadAssetAtPath<StoryTextPanelDemoData>(DemoDataPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<StoryTextPanelDemoData>();
                AssetDatabase.CreateAsset(data, DemoDataPath);
            }

            data.ReplaceEntries(new[]
            {
                new StoryTextPanelDemoData.Entry("Night settles over the northern road."),
                new StoryTextPanelDemoData.Entry("The caravan stopped here three days ago. No one has seen it since.", "Scout"),
                new StoryTextPanelDemoData.Entry("A cold wind moves through the trees."),
                new StoryTextPanelDemoData.Entry("Did you hear that?", "Mira"),
                new StoryTextPanelDemoData.Entry("Only branches. Keep your lantern covered.", "Rowan"),
                new StoryTextPanelDemoData.Entry("Footprints emerge from the mud and disappear at the ruined milestone."),
                new StoryTextPanelDemoData.Entry("These tracks are fresh. There may be more than one creature.", "Scout"),
                new StoryTextPanelDemoData.Entry("A bell rings once somewhere beyond the fog."),
                new StoryTextPanelDemoData.Entry("That chapel has been empty for twenty years.", "Mira"),
                new StoryTextPanelDemoData.Entry("Then someone has finally come home.", "Rowan"),
                new StoryTextPanelDemoData.Entry("You advance between the black pines. The path narrows until the party must walk in single file."),
                new StoryTextPanelDemoData.Entry("Movement. Left side.", "Scout"),
                new StoryTextPanelDemoData.Entry("Two pale eyes open beneath the roots."),
                new StoryTextPanelDemoData.Entry("Stay behind me.", "Rowan"),
                new StoryTextPanelDemoData.Entry("The creature steps into the lantern light, and the old bell rings again."),
                new StoryTextPanelDemoData.Entry("Choose your next action.")
            });
            EditorUtility.SetDirty(data);
            return data;
        }

        private static void BuildDemoPrefab(StoryTextPanelDemoData demoData)
        {
            var panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath);
            if (panelPrefab == null)
            {
                throw new UnityException($"StoryTextPanel prefab was not found at {PanelPrefabPath}.");
            }

            var root = new GameObject("StoryTextPanelDemo", typeof(RectTransform));
            try
            {
                var rootRect = (RectTransform)root.transform;
                rootRect.sizeDelta = new Vector2(760f, 520f);

                var panelInstance = (GameObject)PrefabUtility.InstantiatePrefab(panelPrefab, root.transform);
                var panelRect = (RectTransform)panelInstance.transform;
                panelRect.anchorMin = Vector2.zero;
                panelRect.anchorMax = Vector2.one;
                panelRect.offsetMin = Vector2.zero;
                panelRect.offsetMax = Vector2.zero;

                var loader = root.AddComponent<StoryTextPanelDemoLoader>();
                var serialized = new SerializedObject(loader);
                serialized.FindProperty("target").objectReferenceValue = panelInstance.GetComponent<StoryTextPanel>();
                serialized.FindProperty("data").objectReferenceValue = demoData;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, DemoPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
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
