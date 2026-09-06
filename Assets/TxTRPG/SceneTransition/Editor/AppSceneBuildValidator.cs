using System;
using System.Linq;
using UnityEditor;
using TxTRPG.Editor.Common.Menu;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TxTRPG.SceneTransition.Editor
{
    public sealed class AppSceneBuildValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            ValidateOrThrow();
        }

        [MenuItem(
            TxTRPGEditorMenuPaths.Application + "Validate App Scene Configuration",
            false,
            TxTRPGEditorMenuPriorities.Validate)]
        public static void ValidateFromMenu()
        {
            ValidateOrThrow();
            Debug.Log("AppScene configuration is valid.");
        }

        public static void ValidateOrThrow()
        {
            var errors = BuildScenePathUtility.GetBuildSettingsErrors().ToList();
            var buildScenes = EditorBuildSettings.scenes;
            if (buildScenes.Length == 0 ||
                !buildScenes[0].enabled ||
                !string.Equals(
                    BuildScenePathUtility.Normalize(buildScenes[0].path),
                    SceneTransitionPrefabBuilder.AppScenePath,
                    StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(
                    $"The first enabled Build Settings scene must be " +
                    $"'{SceneTransitionPrefabBuilder.AppScenePath}'.");
            }

            var appSceneError = BuildScenePathUtility.GetValidationError(
                SceneTransitionPrefabBuilder.AppScenePath,
                false);
            if (!string.IsNullOrEmpty(appSceneError))
            {
                errors.Add(appSceneError);
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                SceneTransitionPrefabBuilder.AppRootPrefabPath);
            var appRoot = prefab != null ? prefab.GetComponent<AppSceneRoot>() : null;
            if (appRoot == null)
            {
                errors.Add(
                    $"AppRoot prefab is missing AppSceneRoot: " +
                    $"{SceneTransitionPrefabBuilder.AppRootPrefabPath}");
            }
            else
            {
                var contentError = BuildScenePathUtility.GetValidationError(
                    appRoot.InitialContentScenePath,
                    true);
                if (!string.IsNullOrEmpty(contentError))
                {
                    errors.Add($"Initial Content Scene is invalid. {contentError}");
                }
            }

            if (errors.Count > 0)
            {
                throw new BuildFailedException(
                    "AppScene configuration is invalid:" + Environment.NewLine +
                    string.Join(Environment.NewLine, errors.Select(error => "- " + error)));
            }
        }
    }
}

