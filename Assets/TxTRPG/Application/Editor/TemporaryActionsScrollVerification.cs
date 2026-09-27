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
    public static class TemporaryActionsScrollVerification
    {
        private static readonly Stack<IEnumerator> running = new();
        private static CustomYieldInstruction wait;
        private static int lastFrame;
        [MenuItem("Tools/TxT RPG/UI/Actions/Temporary/Verify Initial Scroll Runtime")]
        public static void Run() => Start("TxTRPG.UI.Tests.ActionGridInitialScrollPlayModeTests");
        [MenuItem("Tools/TxT RPG/UI/Actions/Temporary/Verify Single Row Runtime")]
        public static void SingleRow() => Start("TxTRPG.UI.Tests.ActionGridSingleRowPlayModeTests");
        public static void Start(string typeName)
        {
            if (!EditorApplication.isPlaying || running.Count != 0) throw new InvalidOperationException("Enter Play Mode and wait for previous verification.");
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(typeName)).FirstOrDefault(t => t != null);
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
                Debug.Log("ACTIONS_SCROLL_RUNTIME: PASS " + method.Name);
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
            catch (Exception exception) { Finish(); Debug.LogError("ACTIONS_SCROLL_RUNTIME: FAIL " + exception); }
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
