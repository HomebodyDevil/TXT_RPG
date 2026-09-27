using System;
using System.Linq;
using TxTRPG.UI.Exploration;
using UnityEditor;
using UnityEditor.SceneManagement;
using TxTRPG.UI.Windows;
using UnityEngine;
using UnityEngine.EventSystems;
namespace TxTRPG.Application.Editor
{
    // Temporary, read-only runtime inspection. Safe to repeat; does not save assets.
    public static class TemporaryExplorationSelectionVerification
    {
        [MenuItem("Tools/TxT RPG/UI/Exploration/Temporary/Apply Input Ownership")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling) throw new InvalidOperationException("Use Edit Mode after compilation.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/TMP_MainScene.unity" || scene.isDirty) throw new InvalidOperationException("Open the saved TMP_MainScene first; preserve unsaved work.");
            var roots = scene.GetRootGameObjects();
            var list = roots.SelectMany(r=>r.GetComponentsInChildren<ExplorationNodeChoiceCardList>(true)).Single();
            var service = roots.SelectMany(r=>r.GetComponentsInChildren<GameWindowService>(true)).Single();
            Undo.RecordObject(list, "Connect exploration input ownership");
            list.ConfigureWindowServiceForEditor(service); EditorUtility.SetDirty(list);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Scene save failed.");
            Debug.Log("EXPLORATION_SELECTION_APPLIED: saved list windowService reference only.");
        }
        [MenuItem("Tools/TxT RPG/UI/Exploration/Temporary/Verify Initial Selection Runtime")]
        public static void Verify() => TemporaryActionsScrollVerification.Start("TxTRPG.UI.Tests.ExplorationInitialSelectionPlayModeTests");
        [MenuItem("Tools/TxT RPG/UI/Exploration/Temporary/Inspect Initial Selection")]
        public static void Inspect()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            var list = UnityEngine.Object.FindObjectsByType<ExplorationNodeChoiceCardList>(FindObjectsSortMode.None).Single();
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            var cards = list.Cards.Select(c => new { name=c.name, selected=selected==c.Button.gameObject,
                focus=c.GetComponentsInChildren<Transform>(true).First(t=>t.name=="FocusVisual").gameObject.activeSelf,
                border=c.GetComponentsInChildren<Transform>(true).First(t=>t.name=="ShapeBorder").gameObject.activeSelf,
                scale=c.transform.Find("MotionRoot").localScale.x }).ToArray();
            Debug.Log("EXPLORATION_SELECTION_INSPECT: " + Newtonsoft.Json.JsonConvert.SerializeObject(new { selected=selected != null ? selected.name : null, cards }));
        }
    }
}
