using System.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using TxTRPG.Application.Items;
using TxTRPG.UI;
using TxTRPG.UI.Windows;
using UnityEditor;
using UnityEngine;

namespace TxTRPG.Application.Editor
{
    public static class TemporaryModalInteractionVerification
    {
        private static readonly Stack<IEnumerator> running = new();
        private static CustomYieldInstruction wait;
        private static int lastFrame;
        [MenuItem("Tools/TxT RPG/Application/Temporary/Verify Modal Input Runtime")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying || running.Count != 0) throw new InvalidOperationException("Enter Play Mode and wait for previous verification.");
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("TxTRPG.UI.Tests.ModalOutsideClosePlayModeTests")).FirstOrDefault(t => t != null);
            if (type == null) throw new InvalidOperationException("Compile the Play Mode test assembly first.");
            running.Push(RunTests(type));
            lastFrame = -1;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }
        private static IEnumerator RunTests(Type type)
        {
            foreach (var method in type.GetMethods().Where(m => m.ReturnType == typeof(IEnumerator) && m.GetParameters().Length == 0))
            {
                var scopeType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("UnityEngine.TestTools.Logging.LogScope")).FirstOrDefault(t => t != null)
                    ?? throw new InvalidOperationException("Test Framework LogScope is unavailable.");
                using (var scope = (IDisposable)Activator.CreateInstance(scopeType))
                {
                    yield return (IEnumerator)method.Invoke(Activator.CreateInstance(type), null);
                    scopeType.GetMethod("EvaluateLogScope").Invoke(scope, new object[] { true });
                }
                Debug.Log("MODAL_INPUT_RUNTIME: PASS " + method.Name);
            }
        }
        private static void Tick()
        {
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            try
            {
                if (wait != null && wait.keepWaiting) return;
                wait = null;
                if (running.Count == 0) { Finish(); return; }
                var current = running.Peek();
                if (!current.MoveNext()) { (running.Pop() as IDisposable)?.Dispose(); return; }
                if (current.Current is CustomYieldInstruction custom) wait = custom;
                else if (current.Current is IEnumerator nested) running.Push(nested);
            }
            catch (Exception exception) { Finish(); Debug.LogError("MODAL_INPUT_RUNTIME: FAIL " + exception); }
        }
        private static void OnPlayModeChanged(PlayModeStateChange state) { if (state == PlayModeStateChange.ExitingPlayMode) Finish(); }
        private static void Finish()
        {
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            while (running.Count > 0) (running.Pop() as IDisposable)?.Dispose();
            wait = null;
        }
        [MenuItem("Tools/TxT RPG/Application/Temporary/Inspect Bag Empty State")]
        public static async void Inspect()
        {
            if (!EditorApplication.isPlaying) throw new System.InvalidOperationException("Enter Play Mode from AppScene first.");
            var service = UnityEngine.Object.FindObjectsByType<GameWindowService>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single();
            if (!await service.OpenAsync(GamePageIds.Inventory)) return;
            var page = (InventoryGameWindowPage)service.Pages.Single(p => p.PageId == GamePageIds.Inventory);
            var grid = page.GetComponentInChildren<ActionGridPanel>(true);
            var empty = page.transform.Find("EmptyState");
            Debug.Log($"BAG_EMPTY_INSPECTION: mode={page.DisplayMode}, slots={grid.VisibleCellCount}, empty={empty.gameObject.activeSelf}, settings={JsonUtility.ToJson(page.CreateDefaultRequest().Configuration)}");
        }
    }
}
