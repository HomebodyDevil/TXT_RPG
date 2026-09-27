using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TxTRPG.Application.Editor
{
    // Temporary direct Play Mode test adapter when TestRunner cannot recover domain reload.
    public static class TemporaryMenuIconVerification
    {
        private static readonly Stack<IEnumerator> running = new();
        private static CustomYieldInstruction wait;
        private static int lastFrame;

        [MenuItem("Tools/TxT RPG/Application/Temporary/Verify Menu Icons Runtime")]
        public static void Run() => Start(false);

        [MenuItem("Tools/TxT RPG/Application/Temporary/Verify Menu Action Runtime")]
        public static void RunAction() => Start(true);

        private static void Start(bool actionOnly)
        {
            if (!EditorApplication.isPlaying || running.Count != 0) throw new InvalidOperationException("Enter Play Mode and wait for previous verification.");
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("TxTRPG.UI.Tests.MainSceneMenuInputPlayModeTests")).FirstOrDefault(t => t != null);
            if (type == null) throw new InvalidOperationException("Compile the Play Mode test assembly first.");
            running.Push(RunTests(type, actionOnly));
            lastFrame = -1;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }
        private static IEnumerator RunTests(Type type, bool actionOnly)
        {
            var methods = new[] { "AppScene_MenuButtonsOpenInventoryAndEmptySystemModal", "AppScene_TemporaryCombatAction_UpdatesTemporaryStateAndPreservesOperatingHealth" };
            foreach (var method in actionOnly ? methods.Skip(1) : methods)
            {
                yield return (IEnumerator)type.GetMethod(method).Invoke(Activator.CreateInstance(type), null);
                Debug.Log("MENU_ICON_RUNTIME: PASS " + method);
            }
            yield return new WaitForSecondsRealtime(.5f);
            ScreenCapture.CaptureScreenshot("Assets/Screenshots/game-menu-icons-combat.png");
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
            catch (Exception exception) { Finish(); Debug.LogError("MENU_ICON_RUNTIME: FAIL " + exception); }
        }
        private static void OnPlayModeChanged(PlayModeStateChange state) { if (state == PlayModeStateChange.ExitingPlayMode) Finish(); }
        private static void Finish()
        {
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            while (running.Count > 0) (running.Pop() as IDisposable)?.Dispose();
            wait = null;
        }
    }
}
