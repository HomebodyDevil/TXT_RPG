using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TxTRPG.SceneTransition
{
    [DisallowMultipleComponent]
    public sealed class UnitySceneLoader : MonoBehaviour, ISceneLoader
    {
        public async Task<SceneLoadResult> LoadSceneAsync(
            string sceneName,
            LoadSceneMode loadMode,
            IProgress<float> progress,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                throw new ArgumentException("A scene name or build path is required.", nameof(sceneName));
            }

            cancellationToken.ThrowIfCancellationRequested();
            var operation = SceneManager.LoadSceneAsync(sceneName, loadMode);
            if (operation == null)
            {
                throw new InvalidOperationException($"Scene '{sceneName}' could not be loaded.");
            }

            // Unity scene operations cannot be cancelled once started. Finish the operation before
            // observing cancellation so a hidden scene activation cannot occur later.
            while (!operation.isDone)
            {
                progress?.Report(Mathf.Clamp01(operation.progress));
                await Task.Yield();
            }

            progress?.Report(1f);
            var lookupName = Path.GetFileNameWithoutExtension(sceneName.Replace('\\', '/'));
            var scene = SceneManager.GetSceneByName(lookupName);
            if (!scene.IsValid() && loadMode == LoadSceneMode.Single)
            {
                scene = SceneManager.GetActiveScene();
            }

            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new InvalidOperationException($"Scene '{sceneName}' finished loading but is not valid.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            return new SceneLoadResult(scene);
        }
    }
}
