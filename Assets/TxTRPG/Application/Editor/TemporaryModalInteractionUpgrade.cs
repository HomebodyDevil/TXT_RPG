using System;
using System.Linq;
using TMPro;
using TxTRPG.Application.Items;
using TxTRPG.UI.Windows;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.Application.Editor
{
    // Temporary, targeted and repeatable migration; never regenerate or save scenes.
    public static class TemporaryModalInteractionUpgrade
    {
        [MenuItem("Tools/TxT RPG/Application/Temporary/Apply Modal Outside Close And Bag Empty State")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Exit Play Mode and wait for compilation.");
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("Save and close the Prefab Stage first.");
            foreach (var path in new[] { QuickItemsUiProjectBuilder.InventoryWindowPrefabPath,
                QuickItemsUiProjectBuilder.ModalWindowPrefabPath, QuickItemsUiProjectBuilder.GameMenuScreenPrefabPath })
            {
                if (EditorUtility.IsDirty(AssetDatabase.LoadAssetAtPath<GameObject>(path)))
                    throw new InvalidOperationException("Save pending prefab edits first: " + path);
            }
            foreach (var path in new[] { QuickItemsUiProjectBuilder.InventoryWindowPrefabPath,
                QuickItemsUiProjectBuilder.ModalWindowPrefabPath, QuickItemsUiProjectBuilder.GameMenuScreenPrefabPath })
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (var host in root.GetComponentsInChildren<ModalWindowHost>(true))
                    {
                        var window = host.WindowRect != null ? host.WindowRect : host.transform.Find("Window") as RectTransform;
                        if (window == null || !window.IsChildOf(host.transform) || window.GetComponent<Image>() == null || host.GetComponent<Image>() == null)
                            throw new InvalidOperationException("Invalid modal input boundary in " + path);
                        host.ConfigureOutsideCloseForEditor(window);
                        host.GetComponent<Image>().raycastTarget = true;
                        window.GetComponent<Image>().raycastTarget = true;
                    }
                    foreach (var page in root.GetComponentsInChildren<InventoryGameWindowPage>(true))
                    {
                        var serialized = new SerializedObject(page);
                        var empty = serialized.FindProperty("emptyState").objectReferenceValue as TMP_Text;
                        if (empty == null) throw new InvalidOperationException("Missing empty-state reference in " + path);
                        empty.raycastTarget = false;
                        empty.gameObject.SetActive(false);
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            Debug.Log("MODAL_INTERACTION_UPGRADE: saved targeted prefab references; no scenes regenerated or saved.");
        }
    }
}
