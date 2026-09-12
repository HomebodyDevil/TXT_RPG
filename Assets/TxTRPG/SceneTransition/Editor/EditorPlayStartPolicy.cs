using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TxTRPG.SceneTransition.Editor
{
    [InitializeOnLoad]
    public static class EditorPlayStartPolicy
    {
        public const string AppScenePath = "Assets/Scenes/AppScene.unity";
        public const string ExpectedInitialContentScenePath = "Assets/Scenes/TMP_MainScene.unity";
        public const string SettingsPath = "Project/TxT RPG/Play Mode Start";
        private const string AppRootPrefabPath = "Assets/TxTRPG/SceneTransition/Prefabs/AppRoot.prefab";


        static EditorPlayStartPolicy()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.delayCall += () => ApplyProjectPolicy(false);
        }

        public static bool IsEnabled => EditorPlayStartSettings.instance.StartThroughAppScene;
        public static bool IsConfigured => AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene) == AppScenePath;

        public static void SetEnabled(bool enabled)
        {
            if (enabled) ApplyProjectPolicy(true);
            else DisablePolicy();
        }

        public static void ApplyProjectPolicy() => ApplyProjectPolicy(true);

        public static bool TryValidateConfiguration(out string error)
        {
            var appScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(AppScenePath);
            if (appScene == null) { error = $"App Scene was not found at '{AppScenePath}'."; return false; }
            var enabledScenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).ToArray();
            if (enabledScenes.Length == 0 || enabledScenes[0].path != AppScenePath)
            { error = $"'{AppScenePath}' must be the first enabled Build Settings Scene."; return false; }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AppRootPrefabPath);
            var root = prefab != null ? prefab.GetComponent<AppSceneRoot>() : null;
            if (root == null) { error = $"AppRoot prefab is missing AppSceneRoot at '{AppRootPrefabPath}'."; return false; }
            var serialized = new SerializedObject(root);
            if (!serialized.FindProperty("loadInitialContentOnStart").boolValue)
            { error = "AppSceneRoot.loadInitialContentOnStart is disabled."; return false; }
            if (root.InitialContentScenePath != ExpectedInitialContentScenePath)
            { error = $"AppSceneRoot initial content is '{root.InitialContentScenePath}', expected '{ExpectedInitialContentScenePath}'."; return false; }
            error = string.Empty;
            return true;
        }

        private static void ApplyProjectPolicy(bool explicitChange)
        {
            var settings = EditorPlayStartSettings.instance;
            if (!settings.StartThroughAppScene && !explicitChange) return;
            var appScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(AppScenePath);
            if (appScene == null) { Debug.LogError($"Editor Play policy cannot find '{AppScenePath}'."); return; }
            var current = EditorSceneManager.playModeStartScene;
            if (current == appScene)
            {
                settings.Configure(true, settings.OwnsStartScene, settings.PreviousStartSceneGuid);
                return;
            }
            if (current != null && !explicitChange)
            {
                Debug.LogWarning($"Editor Play policy did not replace existing start Scene '{AssetDatabase.GetAssetPath(current)}'. Open {SettingsPath} to apply it explicitly.");
                return;
            }
            var previousGuid = current == null ? string.Empty : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(current));
            EditorSceneManager.playModeStartScene = appScene;
            settings.Configure(true, true, previousGuid);
        }

        private static void DisablePolicy()
        {
            var settings = EditorPlayStartSettings.instance;
            if (settings.OwnsStartScene && IsConfigured)
            {
                var previousPath = AssetDatabase.GUIDToAssetPath(settings.PreviousStartSceneGuid);
                EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previousPath)
                    ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previousPath);
            }
            settings.Configure(false, false, string.Empty);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode || !IsEnabled || IsPlayModeTestRunning()) return;
            if (!IsConfigured)
            {
                CancelPlay("AppScene Play 정책이 적용되어 있지 않습니다. Project Settings에서 정책을 다시 적용해 주세요.");
                return;
            }
            if (!TryValidateConfiguration(out var error))
            {
                CancelPlay(error);
                return;
            }
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null && prefabStage.scene.isDirty)
            {
                CancelPlay("현재 Prefab Stage에 저장되지 않은 변경이 있습니다. Prefab을 저장한 뒤 다시 Play해 주세요.");
                return;
            }
            if (EditorSceneManager.GetSceneManagerSetup().Any(value => value.isLoaded && SceneManager.GetSceneByPath(value.path).isDirty) &&
                !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorApplication.isPlaying = false;
            }
        }

        private static bool IsPlayModeTestRunning()
        {
            const string typeName = "UnityEditor.TestTools.TestRunner.PlaymodeLauncher";
            var launcherType = Type.GetType($"{typeName}, UnityEditor.TestRunner") ??
                AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType(typeName, false))
                    .FirstOrDefault(type => type != null);
            var isRunningField = launcherType?.GetField(
                "IsRunning", BindingFlags.Public | BindingFlags.Static);
            return isRunningField != null && isRunningField.GetValue(null) is true;
        }

        private static void CancelPlay(string reason)
        {
            Debug.LogError($"Editor Play 시작이 취소되었습니다. {reason}");
            EditorApplication.isPlaying = false;
            EditorUtility.DisplayDialog("TxT RPG Play Mode", reason, "확인");
        }
    }

    public sealed class EditorPlayStartSettingsProvider : SettingsProvider
    {
        private EditorPlayStartSettingsProvider(string path) : base(path, SettingsScope.Project) { }

        [SettingsProvider]
        public static SettingsProvider Create() => new EditorPlayStartSettingsProvider(EditorPlayStartPolicy.SettingsPath)
        { label = "Play Mode Start", keywords = new[] { "Play", "AppScene", "Bootstrap", "TMP_MainScene" } };

        public override void OnGUI(string searchContext)
        {
            var enabled = EditorPlayStartPolicy.IsEnabled;
            var next = EditorGUILayout.ToggleLeft("Always start Play Mode through AppScene", enabled);
            if (next != enabled) EditorPlayStartPolicy.SetEnabled(next);
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField("App Scene", AssetDatabase.LoadAssetAtPath<SceneAsset>(EditorPlayStartPolicy.AppScenePath), typeof(SceneAsset), false);
            EditorGUILayout.HelpBox(EditorPlayStartPolicy.IsConfigured
                ? "Play Mode starts in AppScene. The Scene currently open for editing is restored when Play Mode ends."
                : "The configured Play Mode start Scene is not AppScene.",
                EditorPlayStartPolicy.IsConfigured ? MessageType.Info : MessageType.Warning);
            if (next && GUILayout.Button("Reapply AppScene Start Policy")) EditorPlayStartPolicy.ApplyProjectPolicy();
            if (!EditorPlayStartPolicy.TryValidateConfiguration(out var error))
                EditorGUILayout.HelpBox(error, MessageType.Error);
        }
    }
}
