using System;
using TxTRPG.Editor.Common.Menu;
using UnityEditor;
using UnityEngine;

namespace TxTRPG.SceneTransition.Editor
{
    /// <summary>
    /// Switches the running content scene to TMP_MainScene through AppRoot's
    /// SceneTransitionService, keeping AppRoot and the player session alive.
    /// Play Mode verification menus that need TMP_MainScene start from here,
    /// because AppScene now loads Candidate_MainScene first.
    /// </summary>
    public static class PlayModeContentSceneMenu
    {
        public const string MainScenePath = "Assets/Scenes/TMP_MainScene.unity";
        private const string LogPrefix = "[Play Mode Content Scene]";

        [MenuItem(
            TxTRPGEditorMenuPaths.Application + "Play Mode/Load TMP_MainScene",
            false,
            TxTRPGEditorMenuPriorities.Validate)]
        public static async void LoadMainScene()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogError($"{LogPrefix} Enter Play Mode before loading '{MainScenePath}'.");
                return;
            }

            var root = AppSceneRoot.Instance;
            if (root == null || root.SceneTransitions == null)
            {
                Debug.LogError(
                    $"{LogPrefix} AppSceneRoot or its SceneTransitionService is not available. " +
                    "Start Play Mode through AppScene.");
                return;
            }

            var transitions = root.SceneTransitions;
            if (transitions.IsTransitioning)
            {
                Debug.LogError($"{LogPrefix} A scene transition is already in progress. Try again when it finishes.");
                return;
            }

            var current = transitions.CurrentContentScene;
            if (current.IsValid() && current.isLoaded &&
                string.Equals(current.path, MainScenePath, StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log($"{LogPrefix} '{MainScenePath}' is already the current content scene.");
                return;
            }

            try
            {
                await transitions.LoadContentSceneAsync(MainScenePath);
                Debug.Log($"{LogPrefix} Loaded '{MainScenePath}'.");
            }
            catch (Exception exception)
            {
                Debug.LogError($"{LogPrefix} Could not load '{MainScenePath}': {exception.Message}");
            }
        }
    }
}
