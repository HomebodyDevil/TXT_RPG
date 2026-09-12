using System;
using System.Collections.Generic;
using System.Linq;
using TxTRPG.Editor.Common.Menu;
using UnityEditor;
using UnityEngine;

namespace TxTRPG.Editor.Common.PrefabRebuild
{
    public sealed class PrefabRebuildWindow : EditorWindow
    {
        private PrefabRebuildRegistry registry;
        private readonly HashSet<string> selected = new(StringComparer.Ordinal);
        private Vector2 scroll;
        private string report = string.Empty;

        [MenuItem(TxTRPGEditorMenuPaths.Build + "Rebuild Generated Prefabs...", false, TxTRPGEditorMenuPriorities.Rebuild)]
        public static void Open() => GetWindow<PrefabRebuildWindow>("Prefab Rebuild");

        [MenuItem(TxTRPGEditorMenuPaths.Build + "Validate Prefab Rebuild Registry", false, TxTRPGEditorMenuPriorities.Validate)]
        public static void ValidateMenu()
        {
            var discovered = PrefabRebuildRegistry.Discover();
            Debug.Log($"Prefab rebuild registry is valid: {discovered.Tasks.Count} tasks, {discovered.Exclusions.Count} exclusions.");
        }

        private void OnEnable()
        {
            registry = PrefabRebuildRegistry.Discover();
            selected.Clear();
            foreach (var task in registry.Tasks.Where(x => x.IncludedByDefault)) selected.Add(task.Id);
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Generated prefab sources may overwrite direct edits. Keep custom visuals in Variants or separate Style assets. The batch never modifies Scenes automatically.", MessageType.Warning);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var task in registry.Tasks)
            {
                var enabled = selected.Contains(task.Id);
                var next = EditorGUILayout.ToggleLeft($"{task.DisplayName} ({task.Id})", enabled);
                if (next) selected.Add(task.Id); else selected.Remove(task.Id);
                EditorGUILayout.LabelField("  " + string.Join(", ", task.Outputs), EditorStyles.miniLabel);
            }
            EditorGUILayout.Space(); EditorGUILayout.LabelField("Excluded", EditorStyles.boldLabel);
            foreach (var item in registry.Exclusions) EditorGUILayout.HelpBox($"{item.DisplayName}: {item.Reason}", MessageType.Info);
            EditorGUILayout.EndScrollView();
            if (GUILayout.Button("Select All Eligible")) foreach (var task in registry.Tasks) selected.Add(task.Id);
            if (GUILayout.Button("Validate Plan")) ValidatePlan();
            if (GUILayout.Button("Rebuild Selected")) ExecutePlan();
            if (!string.IsNullOrEmpty(report)) EditorGUILayout.HelpBox(report, MessageType.None);
        }

        private IReadOnlyList<PrefabRebuildTaskDescriptor> CurrentPlan() => registry.BuildPlan(selected);
        private void ValidatePlan()
        {
            var plan = CurrentPlan(); var errors = PrefabRebuildRunner.Preflight(plan);
            report = errors.Count == 0 ? $"Valid plan: {string.Join(" -> ", plan.Select(x => x.Id))}" : string.Join("\n", errors);
        }
        private void ExecutePlan()
        {
            var plan = CurrentPlan(); var errors = PrefabRebuildRunner.Preflight(plan);
            if (errors.Count > 0) { report = string.Join("\n", errors); return; }
            var paths = string.Join("\n", plan.SelectMany(x => x.Outputs).Distinct());
            if (!EditorUtility.DisplayDialog("Rebuild generated prefabs?", "The following generated assets may be overwritten:\n\n" + paths + "\n\nCreate a version-control checkpoint first.", "Rebuild", "Cancel")) return;
            var result = PrefabRebuildRunner.Run(plan);
            report = result.Failure != null ? $"Failed at {result.FailedTaskId}: {result.Failure.Message}" : result.Cancelled ? $"Cancelled after: {string.Join(", ", result.Completed)}" : $"Completed: {string.Join(", ", result.Completed)}";
        }
    }
}
