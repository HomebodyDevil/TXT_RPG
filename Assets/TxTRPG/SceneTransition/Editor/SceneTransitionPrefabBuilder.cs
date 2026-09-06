using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TxTRPG.SceneTransition.Editor
{
    public static class SceneTransitionPrefabBuilder
    {
        public const string DefaultProfilePath =
            "Assets/TxTRPG/SceneTransition/Profiles/DefaultSceneTransitionProfile.asset";
        public const string AppRootPrefabPath =
            "Assets/TxTRPG/SceneTransition/Prefabs/AppRoot.prefab";
        public const string AppScenePath = "Assets/Scenes/AppScene.unity";
        public const string InitialContentScenePath = "Assets/Scenes/TMP_MainScene.unity";

        [MenuItem("Tools/TxT RPG/Rebuild App Scene")]
        public static void CreateOrUpdateAssets()
        {
            EnsureFolder("Assets/TxTRPG/SceneTransition/Profiles");
            EnsureFolder("Assets/TxTRPG/SceneTransition/Prefabs");
            EnsureFolder("Assets/Scenes");
            var profile = CreateOrUpdateProfile();
            var settings = ResolveAppRootSettings();
            var appRootPrefab = BuildAppRoot(profile, settings);
            BuildAppScene(appRootPrefab);
            UpdateBuildSettings(settings.InitialContentScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"AppScene created at {AppScenePath}; its root prefab is {AppRootPrefabPath}.");
        }

        private static SceneTransitionProfile CreateOrUpdateProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<SceneTransitionProfile>(DefaultProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<SceneTransitionProfile>();
                AssetDatabase.CreateAsset(profile, DefaultProfilePath);
            }

            var properties = new SerializedObject(profile);
            properties.FindProperty("color").colorValue = Color.black;
            properties.FindProperty("fadeOutEnabled").boolValue = true;
            properties.FindProperty("coverDuration").floatValue = 0.25f;
            properties.FindProperty("fadeInEnabled").boolValue = true;
            properties.FindProperty("revealDuration").floatValue = 0.25f;
            properties.FindProperty("coverDuringLoad").boolValue = true;
            properties.FindProperty("sceneSwapMode").enumValueIndex =
                (int)ContentSceneSwapMode.LoadThenUnload;
            properties.FindProperty("minimumCoveredTime").floatValue = 0.1f;
            properties.FindProperty("readinessTimeout").floatValue = 30f;
            properties.FindProperty("maximumFrameDelta").floatValue = 0.05f;
            properties.FindProperty("useUnscaledTime").boolValue = true;
            properties.FindProperty("blockInput").boolValue = true;
            properties.FindProperty("reducedMotionMode").enumValueIndex =
                (int)ReducedMotionTransitionMode.ShortFade;
            properties.FindProperty("reducedMotionDuration").floatValue = 0.08f;
            properties.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static AppRootSettings ResolveAppRootSettings()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AppRootPrefabPath);
            var existingRoot = prefab != null ? prefab.GetComponent<AppSceneRoot>() : null;
            var loadOnStart = true;
            var candidate = string.Empty;
            if (existingRoot != null)
            {
                var properties = new SerializedObject(existingRoot);
                loadOnStart = properties.FindProperty("loadInitialContentOnStart").boolValue;
                candidate = existingRoot.InitialContentScenePath;
            }

            if (!BuildScenePathUtility.TryResolveEnabledScenePath(
                    candidate,
                    true,
                    out var resolvedPath))
            {
                resolvedPath = InitialContentScenePath;
            }
            return new AppRootSettings(loadOnStart, resolvedPath);
        }

        private static GameObject BuildAppRoot(
            SceneTransitionProfile profile,
            AppRootSettings settings)
        {
            var root = new GameObject("AppRoot");
            try
            {
                var appRoot = root.AddComponent<AppSceneRoot>();
                var loader = root.AddComponent<UnitySceneLoader>();
                var service = root.AddComponent<SceneTransitionService>();

                var canvasObject = CreateUiObject("TransitionCanvas", root.transform);
                var canvas = canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 32760;
                var scaler = canvasObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
                canvasObject.AddComponent<GraphicRaycaster>();

                var blockerObject = CreateUiObject("InputBlocker", canvasObject.transform);
                var blockerImage = blockerObject.AddComponent<Image>();
                blockerImage.color = Color.clear;
                blockerImage.raycastTarget = true;
                var blocker = blockerObject.AddComponent<CanvasGroup>();
                blocker.interactable = true;
                blocker.blocksRaycasts = true;

                var transitionObject = CreateUiObject("TransitionImage", canvasObject.transform);
                var transitionImage = transitionObject.AddComponent<Image>();
                var initialColor = profile.Color;
                initialColor.a = 1f;
                transitionImage.color = initialColor;
                transitionImage.raycastTarget = false;
                var fade = transitionObject.AddComponent<FadeScreenTransitionEffect>();
                fade.Configure(transitionImage);

                var effectLayer = CreateUiObject("EffectLayer", canvasObject.transform);
                effectLayer.AddComponent<CanvasGroup>().blocksRaycasts = false;

                var loadingIndicator = CreateUiObject("LoadingIndicator", canvasObject.transform);
                loadingIndicator.AddComponent<CanvasGroup>().blocksRaycasts = false;
                loadingIndicator.SetActive(false);

                var errorObject = CreateUiObject("ErrorFallback", canvasObject.transform);
                var errorImage = errorObject.AddComponent<Image>();
                errorImage.color = new Color(0.08f, 0.02f, 0.02f, 0.94f);
                errorImage.raycastTarget = true;
                errorObject.SetActive(false);

                var serviceProperties = new SerializedObject(service);
                serviceProperties.FindProperty("sceneLoaderBehaviour").objectReferenceValue = loader;
                serviceProperties.FindProperty("transitionEffect").objectReferenceValue = fade;
                serviceProperties.FindProperty("defaultProfile").objectReferenceValue = profile;
                serviceProperties.FindProperty("inputBlocker").objectReferenceValue = blocker;
                serviceProperties.FindProperty("errorFallback").objectReferenceValue = errorObject;
                serviceProperties.ApplyModifiedPropertiesWithoutUndo();

                var rootProperties = new SerializedObject(appRoot);
                rootProperties.FindProperty("sceneTransitionService").objectReferenceValue = service;
                rootProperties.FindProperty("loadInitialContentOnStart").boolValue =
                    settings.LoadInitialContentOnStart;
                rootProperties.FindProperty("initialContentScenePath").stringValue =
                    settings.InitialContentScenePath;
                rootProperties.ApplyModifiedPropertiesWithoutUndo();

                return PrefabUtility.SaveAsPrefabAsset(root, AppRootPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void BuildAppScene(GameObject appRootPrefab)
        {
            var previousActiveScene = SceneManager.GetActiveScene();
            if (!Application.isBatchMode && string.IsNullOrEmpty(previousActiveScene.path))
            {
                throw new InvalidOperationException(
                    "Save the current scene before rebuilding AppScene.");
            }

            var isBatchExecuteMethod = Application.isBatchMode &&
                Environment.GetCommandLineArgs().Any(argument =>
                    string.Equals(argument, "-executeMethod", StringComparison.OrdinalIgnoreCase));
            var creationMode = isBatchExecuteMethod
                ? NewSceneMode.Single
                : NewSceneMode.Additive;
            var appScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, creationMode);
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(appRootPrefab, appScene);
                instance.name = "AppRoot";
                if (!EditorSceneManager.SaveScene(appScene, AppScenePath))
                {
                    throw new InvalidOperationException($"Could not save AppScene at '{AppScenePath}'.");
                }
            }
            finally
            {
                if (!Application.isBatchMode)
                {
                    EditorSceneManager.CloseScene(appScene, true);
                    if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                    {
                        EditorSceneManager.SetActiveScene(previousActiveScene);
                    }
                }
            }
        }

        private static void UpdateBuildSettings(string initialContentScenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new(AppScenePath, true)
            };
            var addedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                BuildScenePathUtility.Normalize(AppScenePath)
            };

            foreach (var scene in EditorBuildSettings.scenes)
            {
                var normalizedPath = BuildScenePathUtility.Normalize(scene.path);
                if (string.IsNullOrEmpty(normalizedPath) || !addedPaths.Add(normalizedPath))
                {
                    continue;
                }
                var mustEnable = string.Equals(
                    normalizedPath,
                    initialContentScenePath,
                    StringComparison.OrdinalIgnoreCase);
                scenes.Add(new EditorBuildSettingsScene(normalizedPath, scene.enabled || mustEnable));
            }

            if (addedPaths.Add(BuildScenePathUtility.Normalize(initialContentScenePath)))
            {
                scenes.Add(new EditorBuildSettingsScene(initialContentScenePath, true));
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private readonly struct AppRootSettings
        {
            public AppRootSettings(bool loadInitialContentOnStart, string initialContentScenePath)
            {
                LoadInitialContentOnStart = loadInitialContentOnStart;
                InitialContentScenePath = initialContentScenePath;
            }

            public bool LoadInitialContentOnStart { get; }
            public string InitialContentScenePath { get; }
        }

        private static GameObject CreateUiObject(string name, Transform parent)
        {
            var result = new GameObject(name, typeof(RectTransform));
            result.transform.SetParent(parent, false);
            var rect = (RectTransform)result.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return result;
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
