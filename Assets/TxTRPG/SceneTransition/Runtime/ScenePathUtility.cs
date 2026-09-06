using System;
using UnityEngine.SceneManagement;

namespace TxTRPG.SceneTransition
{
    internal static class ScenePathUtility
    {
        public static string Normalize(string scenePath)
        {
            return string.IsNullOrWhiteSpace(scenePath)
                ? string.Empty
                : scenePath.Trim().Replace('\\', '/');
        }

        public static bool Equals(string left, string right)
        {
            return string.Equals(
                Normalize(left),
                Normalize(right),
                StringComparison.OrdinalIgnoreCase);
        }

        public static bool Matches(Scene scene, string scenePath)
        {
            return scene.IsValid() &&
                !string.IsNullOrEmpty(scene.path) &&
                Equals(scene.path, scenePath);
        }

        public static Scene GetLoadedScene(string scenePath)
        {
            var scene = SceneManager.GetSceneByPath(Normalize(scenePath));
            return scene.IsValid() && scene.isLoaded ? scene : default;
        }
    }
}
