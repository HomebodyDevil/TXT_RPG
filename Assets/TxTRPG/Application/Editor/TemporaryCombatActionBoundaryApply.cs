using System;
using System.Linq;
using TxTRPG.Application.Dice;
using TxTRPG.UI;
using TxTRPG.UI.Windows;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TxTRPG.Application.Editor
{
    // TEMPORARY one-off application menu (combat-action-button-on-panel-boundary). It applies only the boundary action
    // button migration to TMP_MainScene, without the other Apply Player Dice Roll Menu steps. Safe to rerun; saves the
    // Scene. Retire it once every working copy has been migrated.
    public static class TemporaryCombatActionBoundaryApply
    {
        public const string MenuPath = "Tools/TxT RPG/Application/Temporary/Apply Combat Action Boundary Button";

        [MenuItem(MenuPath)]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before applying the combat action button.");
            if (PrefabStageUtility.GetCurrentPrefabStage() != null) throw new InvalidOperationException("Save and close Prefab Stage before applying the combat action button.");
            var scene = SceneManager.GetSceneByPath(TemporaryDiceMenuBuilder.ScenePath);
            var opened = !scene.IsValid() || !scene.isLoaded;
            if (!opened && scene.isDirty) throw new InvalidOperationException("Save or revert unsaved TMP_MainScene changes before applying the combat action button.");
            if (opened) scene = EditorSceneManager.OpenScene(TemporaryDiceMenuBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                var components = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Component>(true)).ToArray();
                var menu = components.OfType<GameMenuPanel>().Single();
                var story = components.OfType<StoryTextPanel>().Single();
                var enemyPanel = components.OfType<EnemyDisplayPanel>().Single();
                var combat = components.OfType<TemporaryDiceRollMenuController>().Single();
                TemporaryDiceMenuBuilder.RemoveLegacyMenuActionButton(menu);
                var boundary = TemporaryDiceMenuBuilder.EnsureBoundaryActionButton(menu, story, enemyPanel, combat);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save TMP_MainScene.");
                Debug.Log($"COMBAT_ACTION_BOUNDARY_APPLY: {boundary.name} is on the EnemyDisplayPanel/StoryTextPanel boundary; the menu action button and binding are removed. Scene saved. Safe to rerun.");
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
