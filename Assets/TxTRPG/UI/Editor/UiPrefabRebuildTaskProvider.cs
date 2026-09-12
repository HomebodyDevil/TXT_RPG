using System;
using System.Collections.Generic;
using System.Linq;
using TxTRPG.Editor.Common.PrefabRebuild;
using UnityEditor;
using UnityEngine;

namespace TxTRPG.UI.Editor
{
    public sealed class UiPrefabRebuildTaskProvider : IPrefabRebuildTaskProvider
    {
        public IEnumerable<PrefabRebuildTaskDescriptor> GetTasks()
        {
            yield return Task("ui.action-grid", "Action Grid",
                new[] { "Assets/TxTRPG/UI/Prefabs/ActionGridCell.prefab", "Assets/TxTRPG/UI/Prefabs/ActionContextMenu.prefab", "Assets/TxTRPG/UI/Prefabs/ActionGridPanel.prefab" },
                ActionGridPrefabBuilder.CreateOrUpdatePrefabs);
            yield return Task("ui.character-display", "Character Display and Status",
                new[] { "Assets/TxTRPG/UI/Prefabs/HealthBarPanel.prefab", "Assets/TxTRPG/UI/Prefabs/CharacterStatusPanel.prefab", "Assets/TxTRPG/UI/Prefabs/CharacterDisplayPanel.prefab" },
                CharacterDisplayPanelPrefabBuilder.CreateOrUpdatePrefab);
            yield return Task("ui.enemy-display", "Enemy Display",
                new[] { "Assets/TxTRPG/UI/Prefabs/EnemyDisplayPanel.prefab" },
                EnemyDisplayPanelPrefabBuilder.CreateOrUpdatePrefab);
            yield return Task("ui.flexible-layout", "Flexible Layout",
                new[] { "Assets/TxTRPG/UI/Prefabs/FlexibleLayoutPlaceholder.prefab", "Assets/TxTRPG/UI/Prefabs/FlexibleLayoutPanel.prefab" },
                FlexibleLayoutPrefabBuilder.CreateOrUpdatePrefab);
        }

        public IEnumerable<PrefabRebuildExclusion> GetExclusions()
        {
            yield return new("ui.story-text", "Story Text Panel", "The current builder overwrites the existing default background Style. Split a preserve-existing-style prefab-only core before registration.");
            yield return new("ui.demos", "UI Demo and Sample builders", "Demo assets and Addressables registrations are optional and are never selected by the production-prefab batch.");
            yield return new("ui.addressables", "Addressables registration and content build", "Addressables settings and player-content builds are outside prefab rebuilding.");
        }

        private static PrefabRebuildTaskDescriptor Task(string id, string name, string[] outputs, Action execute) =>
            new(id, name, PrefabRebuildCategory.Production, true, Array.Empty<string>(), outputs,
                Array.Empty<string>(), "Deterministically rebuilds generated production UI prefabs only.", execute,
                validateResult: () => ValidatePrefabs(outputs));

        private static IReadOnlyList<string> ValidatePrefabs(IEnumerable<string> paths)
        {
            var errors = new List<string>();
            foreach (var path in paths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) { errors.Add($"Generated prefab is missing: {path}"); continue; }
                var missing = prefab.GetComponentsInChildren<Transform>(true).Sum(item => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject));
                if (missing > 0) errors.Add($"Generated prefab has {missing} missing script reference(s): {path}");
            }
            return errors;
        }
    }
}
