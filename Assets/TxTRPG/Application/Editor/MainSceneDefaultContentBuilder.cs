using System;
using System.Linq;
using TxTRPG.Application.Presentation;
using TxTRPG.UI;

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TxTRPG.Application.Editor
{
    public static class MainSceneDefaultContentBuilder
    {
        public const string ScenePath = "Assets/Scenes/TMP_MainScene.unity";
        public const string ConfigurationFolder = "Assets/TxTRPG/Application/Configuration/MainScenePresentation";
        public const string DefaultProfilePath = ConfigurationFolder + "/MainSceneDefaultPresentationProfile.asset";
        public const string EnemyFallbackStylePath = ConfigurationFolder + "/MainSceneEnemyFallbackStyle.asset";
        public const string PreviewFolder = ConfigurationFolder + "/Preview";
        public const string PreviewHarnessPrefabPath = "Assets/TxTRPG/UI/Prefabs/MainScenePreviewHarness.prefab";
        private const string StoryStylePath = "Assets/TxTRPG/UI/Styles/StoryTextPanelDefaultBackgroundStyle.asset";
        private const string EnemyDemoStylePath = "Assets/TxTRPG/UI/DEMO/EnemyDisplayPanel/EnemyDisplayBackgroundDemoStyle.asset";
        private const string StoryDemoDataPath = "Assets/TxTRPG/UI/DEMO/StoryTextPanelDemoData.asset";
        private const string EnemyDemoDataPath = "Assets/TxTRPG/UI/DEMO/EnemyDisplayPanel/EnemyDisplayPanelDemoData.asset";

        [MenuItem("Tools/TxT RPG/Application/Configure Main Scene Default Content")]
        public static void Configure()
        {
            var defaults = CreateOrUpdateAssets();
            ConfigureScene(defaults);
            AssetDatabase.SaveAssets();
            Debug.Log("TMP_MainScene default presentation and isolated Preview profiles were configured.");
        }

        public static MainSceneDefaultPresentationProfile CreateOrUpdateAssets()
        {
            EnsureFolder(ConfigurationFolder); EnsureFolder(PreviewFolder);
            var storyStyle = AssetDatabase.LoadAssetAtPath<PanelBackgroundStyle>(StoryStylePath);
            if (AssetDatabase.LoadAssetAtPath<PanelBackgroundStyle>(EnemyFallbackStylePath) == null)
            {
                if (!AssetDatabase.CopyAsset(EnemyDemoStylePath, EnemyFallbackStylePath))
                    throw new InvalidOperationException("Could not create the local enemy fallback style.");
            }
            var enemyStyle = AssetDatabase.LoadAssetAtPath<PanelBackgroundStyle>(EnemyFallbackStylePath);
            var profile = LoadOrCreate<MainSceneDefaultPresentationProfile>(DefaultProfilePath);
            profile.ConfigureForEditor("The story is not available yet.", "The story could not be loaded.", "HP", "Unknown", storyStyle, enemyStyle);
            EditorUtility.SetDirty(profile);

            var storyData = AssetDatabase.LoadAssetAtPath<StoryTextPanelDemoData>(StoryDemoDataPath);
            var enemyData = AssetDatabase.LoadAssetAtPath<EnemyDisplayPanelDemoData>(EnemyDemoDataPath);
            foreach (MainScenePreviewScenario scenario in Enum.GetValues(typeof(MainScenePreviewScenario)))
            {
                var preview = LoadOrCreate<MainScenePreviewProfile>($"{PreviewFolder}/{scenario}.asset");
                preview.ConfigureForEditor(scenario, storyData, enemyData, scenario == MainScenePreviewScenario.DelayedRecovery ? 1.5f : 0f);
                EditorUtility.SetDirty(preview);
            }
            BuildPreviewHarness(AssetDatabase.LoadAssetAtPath<MainScenePreviewProfile>($"{PreviewFolder}/Normal.asset"));
            return profile;
        }

        public static void ConfigureScene(MainSceneDefaultPresentationProfile defaults)
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var story = FindOne<StoryTextPanel>(roots);
                var grid = FindOne<ActionGridPanel>(roots);
                var enemy = FindOne<EnemyDisplayPanel>(roots);
                var health = roots.SelectMany(root => root.GetComponentsInChildren<HealthBarPanel>(true)).FirstOrDefault();
                var main = roots.SelectMany(root => root.GetComponentsInChildren<FlexibleLayoutPanel>(true)).FirstOrDefault(panel => panel.name == "Main_FlexibleLayoutPanel")
                    ?? throw new InvalidOperationException("Main_FlexibleLayoutPanel was not found.");

                var demoRoot = enemy.GetComponentInParent<EnemyDisplayPanelDemoLoader>()?.gameObject;
                if (demoRoot != null)
                {
                    foreach (var loader in demoRoot.GetComponents<EnemyDisplayPanelDemoLoader>()) Object.DestroyImmediate(loader);
                    foreach (var startup in demoRoot.GetComponents<PanelStartupController>()) Object.DestroyImmediate(startup);
                    foreach (var reveal in demoRoot.GetComponents<FadePanelRevealTransition>()) Object.DestroyImmediate(reveal);
                    if (demoRoot.name == "EnemyDisplayPanelDemo") demoRoot.name = "EnemyDisplayPanelContainer";
                }

                var controller = main.GetComponent<MainScenePresentationController>() ?? main.gameObject.AddComponent<MainScenePresentationController>();
                controller.Configure(defaults, story, grid, enemy, health, false, null);
                EditorUtility.SetDirty(controller);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Could not save TMP_MainScene.");
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        private static void BuildPreviewHarness(MainScenePreviewProfile profile)
        {
            var root = new GameObject("MainScenePreviewHarness"); root.SetActive(false);
            try
            {
                var harness = root.AddComponent<MainScenePreviewHarness>();
                harness.Configure(null, profile, false);
                PrefabUtility.SaveAsPrefabAsset(root, PreviewHarnessPrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }
        private static T FindOne<T>(GameObject[] roots) where T : Component => roots.SelectMany(root => root.GetComponentsInChildren<T>(true)).FirstOrDefault()
            ?? throw new InvalidOperationException($"TMP_MainScene has no {typeof(T).Name}.");
        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        { var asset = AssetDatabase.LoadAssetAtPath<T>(path); if (asset != null) return asset; asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); return asset; }
        private static void EnsureFolder(string path)
        { var parts = path.Split('/'); var current = parts[0]; for (var i = 1; i < parts.Length; i++) { var next = current + "/" + parts[i]; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]); current = next; } }
    }
}
