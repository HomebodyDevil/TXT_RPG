using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TxTRPG.SceneTransition.Editor
{
    public readonly struct BuildSceneOption
    {
        public BuildSceneOption(string path)
        {
            Path = BuildScenePathUtility.Normalize(path);
            var name = System.IO.Path.GetFileNameWithoutExtension(Path);
            DisplayName = $"{name} ({Path})";
        }

        public string Path { get; }
        public string DisplayName { get; }
    }

    public static class BuildScenePathUtility
    {
        public static IReadOnlyList<BuildSceneOption> GetSelectableScenes(bool excludeAppScene)
        {
            return EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => Normalize(scene.path))
                .Where(path => !string.IsNullOrEmpty(path))
                .Where(path => !excludeAppScene ||
                    !PathsEqual(path, SceneTransitionPrefabBuilder.AppScenePath))
                .Where(path => AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(path => new BuildSceneOption(path))
                .ToArray();
        }

        public static bool TryResolveEnabledScenePath(
            string value,
            bool excludeAppScene,
            out string resolvedPath)
        {
            resolvedPath = string.Empty;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = Normalize(value);
            if (IsProjectScenePath(normalized))
            {
                if (TryValidate(normalized, excludeAppScene, out _))
                {
                    resolvedPath = normalized;
                    return true;
                }
                return false;
            }

            var matches = GetSelectableScenes(excludeAppScene)
                .Where(option => string.Equals(
                    Path.GetFileNameWithoutExtension(option.Path),
                    value,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (matches.Length != 1)
            {
                return false;
            }

            resolvedPath = matches[0].Path;
            return true;
        }

        public static bool TryValidate(
            string scenePath,
            bool excludeAppScene,
            out string error)
        {
            error = GetValidationError(scenePath, excludeAppScene);
            return string.IsNullOrEmpty(error);
        }

        public static string GetValidationError(string scenePath, bool excludeAppScene)
        {
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                return "Select an enabled Build Settings scene.";
            }

            var normalized = Normalize(scenePath);
            if (!IsProjectScenePath(normalized))
            {
                return "Scene values must use a full project path such as " +
                    "'Assets/Scenes/MainScene.unity'.";
            }

            if (excludeAppScene &&
                PathsEqual(normalized, SceneTransitionPrefabBuilder.AppScenePath))
            {
                return "AppScene cannot be selected as a content scene.";
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(normalized) == null)
            {
                return $"Scene asset does not exist: {normalized}";
            }

            var matches = EditorBuildSettings.scenes
                .Where(scene => PathsEqual(scene.path, normalized))
                .ToArray();
            if (matches.Length == 0)
            {
                return $"Scene is not included in Build Settings: {normalized}";
            }
            if (matches.Length > 1)
            {
                return $"Scene is registered more than once in Build Settings: {normalized}";
            }
            if (!matches[0].enabled)
            {
                return $"Scene is disabled in Build Settings: {normalized}";
            }

            return string.Empty;
        }

        public static IReadOnlyList<string> GetBuildSettingsErrors()
        {
            var errors = new List<string>();
            var scenes = EditorBuildSettings.scenes;
            if (scenes.Length == 0)
            {
                errors.Add("Build Settings does not contain any scenes.");
                return errors;
            }

            foreach (var duplicate in scenes
                         .GroupBy(scene => Normalize(scene.path), StringComparer.OrdinalIgnoreCase)
                         .Where(group => !string.IsNullOrEmpty(group.Key) && group.Count() > 1))
            {
                errors.Add($"Scene is registered more than once in Build Settings: {duplicate.Key}");
            }
            return errors;
        }

        public static string Normalize(string path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : path.Trim().Replace('\\', '/');
        }

        private static bool IsProjectScenePath(string path)
        {
            return path.StartsWith("Assets/", StringComparison.Ordinal) &&
                path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);
        }

        private static bool PathsEqual(string left, string right)
        {
            return string.Equals(
                Normalize(left),
                Normalize(right),
                StringComparison.OrdinalIgnoreCase);
        }
    }
}

