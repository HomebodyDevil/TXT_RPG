using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using TxTRPG.UI.Exploration;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TxTRPG.UI.Editor
{
    // Temporary verification only; no generated assets or production batch registration.
    public static class ExplorationCardRuntimeVerification
    {
        private static readonly Stack<IEnumerator> running = new();
        private static CustomYieldInstruction wait;
        private static int lastFrame = -1;
        private static string operation;

        [MenuItem("Tools/TxT RPG/UI/Exploration/Temporary/Verify Card Runtime Lifecycle")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying || running.Count != 0) throw new InvalidOperationException("Enter Play Mode and wait for any previous verification.");
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("TxTRPG.UI.Tests.ExplorationCardFeedbackPlayModeTests")).FirstOrDefault(t => t != null);
            if (type == null) throw new InvalidOperationException("Compile the Play Mode test assembly first.");
            running.Push((IEnumerator)type.GetMethod("Pulse_InputSettingsReuseAndTextRebuild_AreSafe").Invoke(Activator.CreateInstance(type), null));
            operation = "pulse for 3 cycles, timeScale=0, focus, preferences, lock, touch, drag, text rebuild, reuse";
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }
        private static void Tick()
        {
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            try
            {
                if (wait != null && wait.keepWaiting) return;
                wait = null;
                if (running.Count == 0) { Finish(); Debug.Log($"CARD_RUNTIME_VERIFICATION: PASS ({operation})"); return; }
                var current = running.Peek();
                if (!current.MoveNext()) { (running.Pop() as IDisposable)?.Dispose(); return; }
                if (current.Current is CustomYieldInstruction custom) wait = custom;
                else if (current.Current is IEnumerator nested) running.Push(nested);
            }
            catch (Exception exception) { Finish(); Debug.LogError("CARD_RUNTIME_VERIFICATION: FAIL " + exception); }
        }
        private static void OnPlayModeChanged(PlayModeStateChange state) { if (state == PlayModeStateChange.ExitingPlayMode) Finish(); }
        private static void Finish()
        {
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            while (running.Count > 0) (running.Pop() as IDisposable)?.Dispose();
            wait = null;
        }

        [MenuItem("Tools/TxT RPG/UI/Exploration/Temporary/Capture Runtime Card States")]
        public static void CaptureStates()
        {
            var scene = SceneManager.GetSceneByPath("Assets/Scenes/TMP_MainScene.unity");
            if (!EditorApplication.isPlaying || !scene.isLoaded || running.Count != 0) throw new InvalidOperationException("Wait for TMP_MainScene and previous checks.");
            var card = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ExplorationNodeChoiceCardView>()).First(c => c.gameObject.activeInHierarchy);
            running.Push(CaptureStatesRoutine(card));
            operation = "runtime card state captures completed";
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }
        private static IEnumerator CaptureStatesRoutine(ExplorationNodeChoiceCardView card)
        {
            var feedback = card.Feedback;
            var settings = card.ShapePresentation.Settings;
            var interactive = card.Button.interactable;
            var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
            try
            {
                feedback.ResetState();
                yield return new WaitForSecondsRealtime(.4f);
                yield return Capture("normal");
                feedback.OnPointerEnter(pointer);
                yield return new WaitForSecondsRealtime(.9f);
                yield return Capture("highlighted");
                feedback.OnPointerDown(pointer);
                yield return new WaitForSecondsRealtime(.4f);
                yield return Capture("pressed");
                feedback.OnPointerUp(pointer);
                card.Button.interactable = false;
                var confirmation = feedback.PlayConfirmation();
                confirmation.MoveNext();
                yield return null;
                yield return Capture("confirming");
                yield return new WaitForSecondsRealtime(.2f);
                (confirmation as IDisposable)?.Dispose();
                yield return new WaitForSecondsRealtime(.4f);
                yield return Capture("disabled");
                card.Button.interactable = interactive; feedback.ResetState();
                var shape = settings; shape.visualMode = ExplorationCardVisualMode.ProceduralShape; shape.shape = ExplorationCardShape.Triangle;
                card.ApplyShape(shape);
                yield return new WaitForSecondsRealtime(.4f);
                yield return Capture("procedural");
                yield return new WaitForSecondsRealtime(.2f);
            }
            finally { card.Button.interactable = interactive; card.ApplyShape(settings); feedback.ResetState(); }
        }
        private static IEnumerator Capture(string state)
        {
            yield return null; yield return null;
            ScreenCapture.CaptureScreenshot($"Assets/Screenshots/exploration-state-{state}.png");
            yield return null; yield return null; yield return null;
        }

        [MenuItem("Tools/TxT RPG/UI/Exploration/Temporary/Inspect Runtime Card Text")]
        public static void Inspect()
        {
            var scene = SceneManager.GetSceneByPath("Assets/Scenes/TMP_MainScene.unity");
            if (!EditorApplication.isPlaying || !scene.isLoaded) throw new InvalidOperationException("Start from AppScene and wait for TMP_MainScene.");
            var cards = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ExplorationNodeChoiceCardView>(true)).Where(c => c.gameObject.activeInHierarchy);
            foreach (var card in cards)
            {
                foreach (var text in card.GetComponentsInChildren<TMP_Text>(false))
                {
                    var alpha = text.GetComponentsInParent<CanvasGroup>().Aggregate(1f, (a, g) => a * g.alpha);
                    var material = text.fontSharedMaterial;
                    var face = material.HasProperty("_FaceColor") ? material.GetColor("_FaceColor") : Color.white;
                    Debug.Log($"CARD_TEXT {card.name}/{text.name}: '{text.text}', color={text.color}, face={face}, CanvasAlpha={alpha}, overflow={text.isTextOverflowing}, materials={text.textInfo.materialCount}, richText={text.richText}");
                }
            }
        }
    }
}
