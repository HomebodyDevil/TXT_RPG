using System;
using System.Linq;
using TxTRPG.Application.Items;
using TxTRPG.UI;
using TxTRPG.UI.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TxTRPG.Application.Editor
{
    // Temporary, repeatable targeted application. Existing settings and authored children are preserved.
    public static class TemporaryActionsSingleRowUpgrade
    {
        [MenuItem("Tools/TxT RPG/UI/Actions/Temporary/Apply Single Row And Header")]
        public static void Apply()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || EditorApplication.isCompiling || scene.isDirty || scene.path != "Assets/Scenes/TMP_MainScene.unity")
                throw new InvalidOperationException("Open clean TMP_MainScene in Edit Mode after compilation. Save unrelated edits first.");
            var originalPanel = Object.FindObjectsByType<QuickItemGridPresenter>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single().GetComponent<ActionGridPanel>();
            var originalSettings = originalPanel.GetDisplaySettings();
            foreach (var path in new[] { "Assets/TxTRPG/UI/Prefabs/ActionGridPanel.prefab", "Assets/TxTRPG/UI/Prefabs/InventoryWindowPage.prefab" })
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (var grid in root.GetComponentsInChildren<ActionGridPanel>(true)) ActionGridSurfaceAuthoring.Ensure(grid);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var panel = Object.FindObjectsByType<QuickItemGridPresenter>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single().GetComponent<ActionGridPanel>();
            var surface = ActionGridSurfaceAuthoring.Ensure(panel);
            var settings = originalSettings; settings.flow = ActionGridFlow.SingleRow;
            settings.singleRow.alignment = ActionGridRowAlignment.ConditionalCenterOrEnds;
            settings.singleRow.centerThreshold = 4; settings.singleRow.verticalAlignment = ActionGridRowVerticalAlignment.Center;
            settings.singleRow.scrollbarVisibility = ScrollbarVisibilityMode.Hidden;
            surface.ConfigureHeader(false, 58, 8, new RectOffset(18,18,18,18));
            panel.ConfigureDisplay(settings);
            // Capture actual scene overrides, including inactive Header and geometry, without touching neighboring panels.
            foreach (var component in panel.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue;
                EditorUtility.SetDirty(component);
                if (PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
            var header = panel.transform.Find("Header").gameObject;
            PrefabUtility.RecordPrefabInstancePropertyModifications(header);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Scene save failed.");
            Debug.Log("ACTIONS_SINGLE_ROW_APPLY: PASS saved targeted Prefabs and TMP_MainScene. Header hidden; SingleRow; K=4; horizontal bar Hidden.");
        }
    }
}
