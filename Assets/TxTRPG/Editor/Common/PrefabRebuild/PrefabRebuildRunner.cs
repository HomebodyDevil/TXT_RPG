using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TxTRPG.Editor.Common.PrefabRebuild
{
    public sealed class PrefabRebuildRunResult
    {
        public readonly List<string> Completed = new();
        public string FailedTaskId;
        public Exception Failure;
        public bool Cancelled;
    }

    public static class PrefabRebuildRunner
    {
        private static bool running;
        public static bool IsRunning => running;

        public static IReadOnlyList<string> Preflight(IReadOnlyList<PrefabRebuildTaskDescriptor> plan)
        {
            var errors = new List<string>();
            if (running) errors.Add("A prefab rebuild is already running.");
            if (EditorApplication.isPlayingOrWillChangePlaymode) errors.Add("Exit Play Mode before rebuilding prefabs.");
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) errors.Add("Wait for Unity compilation and asset import to finish.");
            if (PrefabStageUtility.GetCurrentPrefabStage() != null) errors.Add("Close Prefab Mode before rebuilding generated prefabs.");
            if (Enumerable.Range(0, EditorSceneManager.sceneCount).Select(EditorSceneManager.GetSceneAt).Any(scene => scene.isDirty)) errors.Add("Save or revert all open Scene changes before rebuilding prefabs.");
            foreach (var task in plan)
            {
                foreach (var input in task.Inputs) if (AssetDatabase.LoadMainAssetAtPath(input) == null) errors.Add($"Task '{task.Id}' requires missing input '{input}'.");
                errors.AddRange(task.Validate().Select(error => $"Task '{task.Id}': {error}"));
            }
            return errors;
        }

        public static PrefabRebuildRunResult Run(IReadOnlyList<PrefabRebuildTaskDescriptor> plan)
        {
            var result = new PrefabRebuildRunResult();
            var errors = Preflight(plan);
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
            running = true;
            try
            {
                for (var index = 0; index < plan.Count; index++)
                {
                    var task = plan[index];
                    if (EditorUtility.DisplayCancelableProgressBar("Rebuild Generated Prefabs", task.DisplayName, (float)index / Math.Max(1, plan.Count))) { result.Cancelled = true; break; }
                    try
                    {
                        task.Execute();
                        var validationErrors = task.ValidateResult();
                        if (validationErrors.Count > 0) throw new InvalidOperationException(string.Join("\n", validationErrors));
                        result.Completed.Add(task.Id);
                    }
                    catch (Exception exception) { result.FailedTaskId = task.Id; result.Failure = exception; break; }
                }
            }
            finally { running = false; EditorUtility.ClearProgressBar(); }
            return result;
        }
    }
}
