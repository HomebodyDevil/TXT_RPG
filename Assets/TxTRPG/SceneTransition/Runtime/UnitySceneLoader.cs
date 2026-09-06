using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TxTRPG.SceneTransition
{
    [DisallowMultipleComponent]
    public sealed class UnitySceneLoader : MonoBehaviour, ISceneLoader, IScenePathValidator
    {
        public async Task<SceneLoadResult> LoadSceneAsync(
            string scenePath,
            LoadSceneMode loadMode,
            IProgress<float> progress,
            CancellationToken cancellationToken)
        {
            ValidateScenePath(scenePath);

            cancellationToken.ThrowIfCancellationRequested();
            var operation = SceneManager.LoadSceneAsync(scenePath, loadMode);
            if (operation == null)
            {
                throw new InvalidOperationException($"Scene '{scenePath}' could not be loaded.");
            }

            // Unity scene operations cannot be cancelled once started. Finish the operation before
            // observing cancellation so a hidden scene activation cannot occur later.
            while (!operation.isDone)
            {
                progress?.Report(Mathf.Clamp01(operation.progress));
                await Task.Yield();
            }

            progress?.Report(1f);
            var normalizedPath = ScenePathUtility.Normalize(scenePath);
            var scene = ScenePathUtility.GetLoadedScene(normalizedPath);
            if (!scene.IsValid() && loadMode == LoadSceneMode.Single)
            {
                var activeScene = SceneManager.GetActiveScene();
                if (ScenePathUtility.Matches(activeScene, normalizedPath))
                {
                    scene = activeScene;
                }
            }

            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new InvalidOperationException($"Scene '{scenePath}' finished loading but is not valid.");
            }

            return new SceneLoadResult(scene);
        }

        public async Task UnloadSceneAsync(Scene scene, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            var operation = SceneManager.UnloadSceneAsync(scene);
            if (operation == null)
            {
                throw new InvalidOperationException($"Scene '{scene.name}' could not be unloaded.");
            }

            // Unity cannot cancel an unload after it starts. Observe cancellation only after the
            // operation completes so the caller never assumes that the scene is still loaded.
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        public bool SetActiveScene(Scene scene)
        {
            return scene.IsValid() && scene.isLoaded && SceneManager.SetActiveScene(scene);
        }

        public void ValidateScenePath(string scenePath)
        {
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                throw new ArgumentException("A Build Settings scene path is required.", nameof(scenePath));
            }

            var normalizedPath = ScenePathUtility.Normalize(scenePath);
            if (!normalizedPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                !normalizedPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    $"Scene '{scenePath}' must use a full project path such as " +
                    "'Assets/Scenes/MainScene.unity'.",
                    nameof(scenePath));
            }

            if (!Application.CanStreamedLevelBeLoaded(normalizedPath))
            {
                throw new InvalidOperationException(
                    $"Scene '{normalizedPath}' is not included and enabled in Build Settings.");
            }
        }
    }
}
