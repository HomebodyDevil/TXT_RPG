using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TxTRPG.SceneTransition;
using TxTRPG.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TxTRPG.Application.Editor
{
    // TEMPORARY one-off application menu (candidate-main-scene-initial-content-and-layout). It
    // 1. puts Candidate_MainScene in Build Settings (AppScene, Candidate_MainScene, TMP_MainScene) and removes entries
    //    whose Scene file no longer exists,
    // 2. sets AppRoot.prefab's initial content to Candidate_MainScene and reverts that override in AppScene,
    // 3. rebuilds Candidate_MainScene/Canvas/MainArea and its Left/Middle/Right areas as FlexibleLayoutPanel.prefab
    //    instances (Weighted 1:3:1, Vertical When Narrow at 720, Spacing 20, margins 15/10), keeping the area colors.
    // It refuses to run in Play Mode, while compiling, or when a target Scene has unsaved changes, and saves only the
    // assets it changed. Each step checks whether it is already applied, so rerunning is safe. Remove this file once the
    // change is applied and verified.
    public static class TemporaryCandidateMainSceneApply
    {
        public const string MenuPath =
            "Tools/TxT RPG/Application/Temporary/Apply Candidate Main Scene Initial Content";

        private const string LogPrefix = "CANDIDATE_MAIN_SCENE_APPLY:";
        private const string AppScenePath = "Assets/Scenes/AppScene.unity";
        private const string CandidateScenePath = "Assets/Scenes/Candidate_MainScene.unity";
        private const string MainScenePath = "Assets/Scenes/TMP_MainScene.unity";
        private const string AppRootPrefabPath = "Assets/TxTRPG/SceneTransition/Prefabs/AppRoot.prefab";
        private const string FlexiblePanelPrefabPath = "Assets/TxTRPG/UI/Prefabs/FlexibleLayoutPanel.prefab";
        private const string BackgroundImagePath = "BackgroundLayer/BackgroundVisualRoot/BackgroundA";
        private const string MainAreaNodeId = "8a1cefb1f6c94b8bbf238bcc6f2373a8";
        private static readonly (string Name, float Weight)[] Areas =
        {
            ("LeftArea", 1f), ("MiddleArea", 3f), ("RightArea", 1f)
        };

        [MenuItem(MenuPath, false, 3000)]
        public static void Apply()
        {
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                    throw new InvalidOperationException("Leave Play Mode and wait for compilation before applying.");
                foreach (var path in new[] { AppScenePath, CandidateScenePath })
                {
                    var loaded = SceneManager.GetSceneByPath(path);
                    if (loaded.IsValid() && loaded.isLoaded && loaded.isDirty)
                        throw new InvalidOperationException($"Save or revert unsaved changes in '{path}' first.");
                }

                var summary = new StringBuilder();
                ApplyBuildSettings(summary);
                ApplyAppRootPrefab(summary);
                WithScene(AppScenePath, scene => RevertAppSceneOverride(scene, summary));
                WithScene(CandidateScenePath, scene => ApplyCandidateLayout(scene, summary));
                Debug.Log($"{LogPrefix} PASS\n{summary}");
            }
            catch (Exception exception)
            {
                Debug.LogError($"{LogPrefix} FAIL {exception.Message}");
            }
        }

        private static void ApplyBuildSettings(StringBuilder summary)
        {
            var current = EditorBuildSettings.scenes;
            var ordered = new List<EditorBuildSettingsScene>();
            void Add(string path, bool enabled) => ordered.Add(new EditorBuildSettingsScene(path, enabled));
            Add(AppScenePath, true);
            Add(CandidateScenePath, true);
            Add(MainScenePath, true);
            foreach (var scene in current)
            {
                if (ordered.Any(item => string.Equals(item.path, scene.path, StringComparison.OrdinalIgnoreCase))) continue;
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path) == null)
                {
                    summary.AppendLine($" Build Settings: removed missing Scene '{scene.path}'.");
                    continue;
                }
                ordered.Add(scene);
            }

            var unchanged = current.Length == ordered.Count && current.Zip(ordered, (left, right) =>
                left.path == right.path && left.enabled == right.enabled).All(same => same);
            if (unchanged)
            {
                summary.AppendLine(" Build Settings already match; not changed.");
                return;
            }
            EditorBuildSettings.scenes = ordered.ToArray();
            summary.AppendLine(" Build Settings: " + string.Join(", ", ordered.Select(item =>
                $"{System.IO.Path.GetFileNameWithoutExtension(item.path)}({(item.enabled ? "on" : "off")})")));
        }

        private static void ApplyAppRootPrefab(StringBuilder summary)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AppRootPrefabPath)
                ?? throw new InvalidOperationException($"'{AppRootPrefabPath}' was not found.");
            var root = prefab.GetComponent<AppSceneRoot>()
                ?? throw new InvalidOperationException("AppRoot.prefab has no AppSceneRoot.");
            if (root.InitialContentScenePath == CandidateScenePath)
            {
                summary.AppendLine(" AppRoot.prefab already loads Candidate_MainScene; not saved.");
                return;
            }
            var properties = new SerializedObject(root);
            properties.FindProperty("initialContentScenePath").stringValue = CandidateScenePath;
            properties.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SavePrefabAsset(prefab);
            summary.AppendLine(" AppRoot.prefab: initialContentScenePath = Candidate_MainScene; saved.");
        }

        private static void RevertAppSceneOverride(Scene scene, StringBuilder summary)
        {
            var root = scene.GetRootGameObjects()
                .SelectMany(item => item.GetComponentsInChildren<AppSceneRoot>(true))
                .SingleOrDefault() ?? throw new InvalidOperationException("AppScene has no AppSceneRoot.");
            var property = new SerializedObject(root).FindProperty("initialContentScenePath");
            if (!property.prefabOverride)
            {
                summary.AppendLine(" AppScene has no initialContentScenePath override; not saved.");
                return;
            }
            PrefabUtility.RevertPropertyOverride(property, InteractionMode.AutomatedAction);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save AppScene.");
            summary.AppendLine(" AppScene: reverted the initialContentScenePath override; saved.");
        }

        private static void ApplyCandidateLayout(Scene scene, StringBuilder summary)
        {
            var canvas = scene.GetRootGameObjects().SingleOrDefault(item => item.name == "Canvas")
                ?? throw new InvalidOperationException("Candidate_MainScene has no root Canvas.");
            var oldMain = canvas.transform.Find("MainArea")
                ?? throw new InvalidOperationException("Candidate_MainScene has no Canvas/MainArea.");
            var panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FlexiblePanelPrefabPath)
                ?? throw new InvalidOperationException($"'{FlexiblePanelPrefabPath}' was not found.");
            if (PrefabUtility.GetCorrespondingObjectFromSource(oldMain.gameObject) == panelPrefab)
            {
                summary.AppendLine(" Candidate_MainScene: MainArea is already a FlexibleLayoutPanel.prefab instance; not saved.");
                return;
            }

            var mainColor = ReadColor(oldMain);
            var areaColors = new Dictionary<string, Color>();
            foreach (var (name, _) in Areas)
            {
                var area = oldMain.Find(name) ?? throw new InvalidOperationException($"MainArea/{name} was not found.");
                if (area.childCount != 0) throw new InvalidOperationException($"MainArea/{name} has children; stop and review.");
                var unexpected = area.GetComponents<Component>().Where(component => !(component is RectTransform ||
                    component is CanvasRenderer || component is Image || component is FlexibleLayoutItem)).ToArray();
                if (unexpected.Length != 0)
                    throw new InvalidOperationException($"MainArea/{name} has unexpected components: " +
                        string.Join(", ", unexpected.Select(component => component.GetType().Name)));
                areaColors[name] = ReadColor(area);
            }
            if (oldMain.childCount != Areas.Length) throw new InvalidOperationException("MainArea has unexpected children; stop and review.");

            var siblingIndex = oldMain.GetSiblingIndex();
            var main = Instantiate(panelPrefab, canvas.transform, "MainArea");
            main.transform.SetSiblingIndex(siblingIndex);
            var mainRect = (RectTransform)main.transform;
            mainRect.anchorMin = Vector2.zero;
            mainRect.anchorMax = Vector2.one;
            mainRect.pivot = new Vector2(0.5f, 0.5f);
            mainRect.offsetMin = Vector2.zero;
            mainRect.offsetMax = Vector2.zero;
            mainRect.localScale = Vector3.one;
            var panel = new SerializedObject(main.GetComponent<FlexibleLayoutPanel>());
            panel.FindProperty("nodeId").stringValue = MainAreaNodeId;
            panel.FindProperty("fixedAxis").enumValueIndex = (int)FlexibleLayoutAxis.Horizontal;
            panel.FindProperty("axisPolicy").enumValueIndex = (int)FlexibleLayoutAxisPolicy.VerticalWhenNarrow;
            panel.FindProperty("breakpoint").floatValue = 720f;
            panel.FindProperty("spacing").floatValue = 20f;
            panel.FindProperty("overflow").enumValueIndex = (int)FlexibleLayoutOverflow.ShrinkBelowMinimum;
            panel.FindProperty("includeInactiveChildren").boolValue = false;
            panel.FindProperty("clipContent").boolValue = false;
            panel.FindProperty("overrideContentMargins").boolValue = true;
            var margins = panel.FindProperty("contentMargins");
            margins.FindPropertyRelative("m_Left").intValue = 15;
            margins.FindPropertyRelative("m_Right").intValue = 15;
            margins.FindPropertyRelative("m_Top").intValue = 10;
            margins.FindPropertyRelative("m_Bottom").intValue = 10;
            panel.ApplyModifiedProperties();
            SetBackgroundColor(main.transform, mainColor);

            var contentLayer = main.transform.Find("ContentLayer")
                ?? throw new InvalidOperationException("FlexibleLayoutPanel.prefab has no ContentLayer.");
            foreach (var (name, weight) in Areas)
            {
                var area = Instantiate(panelPrefab, contentLayer, name);
                var item = area.GetComponent<FlexibleLayoutItem>() ?? area.AddComponent<FlexibleLayoutItem>();
                var itemProperties = new SerializedObject(item);
                itemProperties.FindProperty("sizeMode").enumValueIndex = (int)FlexibleLayoutSizeMode.Weighted;
                itemProperties.FindProperty("weight").floatValue = weight;
                itemProperties.FindProperty("minimumSize").floatValue = 0f;
                itemProperties.FindProperty("maximumSize").floatValue = 0f;
                itemProperties.ApplyModifiedProperties();
                SetBackgroundColor(area.transform, areaColors[name]);
            }

            Object.DestroyImmediate(oldMain.gameObject);
            // Run the layout pass now so the Scene is not dirtied again right after saving.
            LayoutRebuilder.ForceRebuildLayoutImmediate(mainRect);
            Canvas.ForceUpdateCanvases();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save Candidate_MainScene.");
            summary.AppendLine(" Candidate_MainScene: MainArea and Left/Middle/Right rebuilt as FlexibleLayoutPanel.prefab " +
                "instances (1:3:1, Vertical When Narrow 720, Spacing 20, margins 15/15/10/10); saved.");
        }

        private static GameObject Instantiate(GameObject prefab, Transform parent, string name)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = name;
            return instance;
        }

        private static Color ReadColor(Transform target)
        {
            var image = target.GetComponent<Image>()
                ?? throw new InvalidOperationException($"'{target.name}' has no Image to take its color from.");
            return image.color;
        }

        private static void SetBackgroundColor(Transform panelRoot, Color color)
        {
            var image = panelRoot.Find(BackgroundImagePath)?.GetComponent<Image>()
                ?? throw new InvalidOperationException($"'{panelRoot.name}' has no {BackgroundImagePath} Image.");
            var properties = new SerializedObject(image);
            properties.FindProperty("m_Color").colorValue = color;
            properties.ApplyModifiedProperties();
        }

        private static void WithScene(string path, Action<Scene> action)
        {
            var scene = SceneManager.GetSceneByPath(path);
            var opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                action(scene);
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
