using TxTRPG.Editor.Common.Menu;
using UnityEditor;
using UnityEngine;

namespace TxTRPG.UI.Editor
{
    public static class HealthBarPanelDemoBuilder
    {
        public const string DemoPrefabPath =
            "Assets/TxTRPG/UI/DEMO/CharacterStatusPanel/HealthBarPanelDemo.prefab";

        [MenuItem(
            TxTRPGEditorMenuPaths.UiDemos + "Rebuild Health Bar Panel Demo",
            false,
            TxTRPGEditorMenuPriorities.Rebuild)]
        public static void CreateOrUpdateDemo()
        {
            CharacterStatusPrefabBuilder.CreateOrUpdatePrefabs();
            EnsureFolder("Assets/TxTRPG/UI/DEMO/CharacterStatusPanel");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                CharacterStatusPrefabBuilder.HealthBarPrefabPath);
            var root = new GameObject("HealthBarPanelDemo", typeof(RectTransform));
            try
            {
                ((RectTransform)root.transform).sizeDelta = new Vector2(960f, 920f);
                for (var y = 0; y < 3; y++)
                for (var x = 0; x < 3; x++)
                {
                    var horizontal = (HealthBarHorizontalAlignment)x;
                    var vertical = (HealthBarVerticalAlignment)y;
                    var instance = Instantiate(prefab, root.transform,
                        $"{vertical}_{horizontal}",
                        new Vector2((x - 1) * 310f, (y - 1) * 250f + 100f),
                        new Vector2(290f, 220f));
                    var layout = instance.GetComponent<HealthBarLayoutController>();
                    layout.SetSizeModes(HealthBarAxisSizeMode.Fixed, HealthBarAxisSizeMode.Fixed);
                    layout.SetFixedSize(150f, 24f);
                    layout.SetAlignment(horizontal, vertical);
                    instance.GetComponent<HealthBarPanel>().Apply(Presentation(65));
                }

                var runtime = Instantiate(prefab, root.transform, "RuntimeAlignmentAndPulse",
                    new Vector2(0f, -365f), new Vector2(910f, 130f));
                var runtimePanel = runtime.GetComponent<HealthBarPanel>();
                var runtimeLayout = runtime.GetComponent<HealthBarLayoutController>();
                runtimeLayout.SetSizeModes(HealthBarAxisSizeMode.Stretch, HealthBarAxisSizeMode.Stretch);
                runtimePanel.Apply(Presentation(100));
                var pulse = runtime.AddComponent<HealthBarScalePulseEffect>();
                pulse.Configure(runtimePanel.BarVisualRoot);
                runtimePanel.RegisterEffect(pulse);
                var controller = runtime.AddComponent<HealthBarPanelDemoController>();
                controller.Configure(runtimePanel, runtimeLayout);

                PrefabUtility.SaveAsPrefabAsset(root, DemoPrefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static GameObject Instantiate(GameObject prefab, Transform parent, string name,
            Vector2 position, Vector2 size)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = name;
            var rect = (RectTransform)instance.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return instance;
        }

        private static CharacterStatusPresentation Presentation(int current) =>
            new(new CharacterNamePresentation(string.Empty),
                new HealthPresentation(current, 100, "HP", $"{current} / 100"));

        private static void EnsureFolder(string path)
        {
            var segments = path.Split('/');
            var current = segments[0];
            for (var index = 1; index < segments.Length; index++)
            {
                var next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segments[index]);
                current = next;
            }
        }
    }
}
