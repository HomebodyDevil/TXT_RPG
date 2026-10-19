using System;
using System.Linq;
using TxTRPG.Application.Encounters;
using TxTRPG.Gameplay.Encounters;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TxTRPG.Application.Editor
{
    // TEMPORARY Play Mode check (candidate-main-scene-encounter-toggles-and-debug-roll). Enter Play Mode through AppScene
    // so Candidate_MainScene is the content scene, then run the menu. It drives EncounterRollController through its public
    // API: no selection, chance 0, chance 1 with only Combat, only Rest, all weights 0; then it restores the configured
    // settings, clears the selection and closes the result modal. Selection and settings are scene-lifetime and never
    // saved, so nothing persists; safe to rerun. Results use the ENCOUNTER_ROLL_CHECK prefix. Closing by an outside click
    // stays a manual check. Retire it with TemporaryEncounterTogglesApply.
    public static class TemporaryEncounterRollPlayCheck
    {
        // TxTRPGEditorMenuPaths.Application + "Temporary/Encounter Roll Play Check".
        public const string MenuPath = "Tools/TxT RPG/Application/Temporary/Encounter Roll Play Check";

        [MenuItem(MenuPath, false, 3011)]
        public static void Run()
        {
            if (!EditorApplication.isPlaying) { Debug.LogError("ENCOUNTER_ROLL_CHECK: FAIL enter Play Mode through AppScene first."); return; }
            var scene = SceneManager.GetSceneByPath(TemporaryEncounterTogglesApply.ScenePath);
            if (!scene.isLoaded) { Debug.LogError("ENCOUNTER_ROLL_CHECK: FAIL Candidate_MainScene is not the loaded content scene."); return; }
            // Editor tool: finding the component from the scene roots is allowed here (runtime code uses serialized references).
            var controller = scene.GetRootGameObjects().Select(root => root.GetComponentInChildren<EncounterRollController>(true)).FirstOrDefault(found => found != null);
            if (controller == null || !controller.IsReady) { Debug.LogError("ENCOUNTER_ROLL_CHECK: FAIL Candidate_MainScene has no ready EncounterRollController."); return; }
            var window = controller.ResultWindow;
            if (window == null || controller.ResultPage == null || !controller.HasResultModal) { Debug.LogError("ENCOUNTER_ROLL_CHECK: FAIL the controller has no result modal."); return; }
            if (window.IsOpen) window.Close();

            const string rune = TemporaryEncounterTogglesApply.RuneId;
            var passed = true;
            try
            {
                controller.Selection.Clear();
                var result = controller.RollNow();
                passed &= Report(1, result?.Outcome == EncounterRollOutcome.NoElementsSelected && !window.IsOpen,
                    $"no selection -> {result?.Outcome}, modal open {window.IsOpen}");

                controller.Selection.SetSelected(rune, true);
                var barShowsSelection = controller.ToggleBar.IsSelected(rune);
                controller.SetSettings(new EncounterRollSettings(0f, .5f, 1f, 1f, 1f));
                result = controller.RollNow();
                passed &= Report(2, result?.Outcome == EncounterRollOutcome.ChanceFailed && !window.IsOpen && barShowsSelection,
                    $"rune selected (bar shows {barShowsSelection}), chance 0 -> {result?.Outcome}, modal open {window.IsOpen}");

                controller.SetSettings(new EncounterRollSettings(1f, .5f, 1f, 0f, 0f));
                result = controller.RollNow();
                passed &= Report(3, result?.Outcome == EncounterRollOutcome.Occurred && result.Kind == EncounterKind.Combat && window.IsOpen &&
                    window.CurrentPageId == EncounterRollController.ResultPageId && controller.ResultPage.ConfiguredMessage == "룬 : 전투",
                    $"chance 1, combat only -> {result?.Outcome} {result?.Kind}, modal open {window.IsOpen}, page '{window.CurrentPageId}', message '{controller.ResultPage.ConfiguredMessage}'");

                var refused = controller.RollNow();
                passed &= Report(4, refused == null, $"roll while the modal is open -> {(refused == null ? "refused" : refused.Outcome.ToString())}");

                window.Close();
                controller.SetSettings(new EncounterRollSettings(1f, .5f, 0f, 0f, 1f));
                result = controller.RollNow();
                passed &= Report(5, result?.Outcome == EncounterRollOutcome.Occurred && result.Kind == EncounterKind.Rest && window.IsOpen &&
                    controller.ResultPage.ConfiguredMessage == "룬 : 휴식",
                    $"after Close, rest only -> {result?.Outcome} {result?.Kind}, modal open {window.IsOpen}, message '{controller.ResultPage.ConfiguredMessage}'");

                window.Close();
                controller.SetSettings(new EncounterRollSettings(1f, .5f, 0f, 0f, 0f));
                result = controller.RollNow();
                passed &= Report(6, result?.Outcome == EncounterRollOutcome.NoKindAvailable && !window.IsOpen,
                    $"all weights 0 -> {result?.Outcome}, modal open {window.IsOpen}");
            }
            catch (Exception exception)
            {
                passed = false;
                Debug.LogError($"ENCOUNTER_ROLL_CHECK: FAIL {exception}");
            }
            finally
            {
                if (window.IsOpen) window.Close();
                controller.ResetSettingsToConfiguration();
                controller.Selection.Clear();
            }
            var restored = controller.Selection.Count == 0 && !controller.ToggleBar.IsSelected(rune) &&
                controller.CurrentSettings.OccurrenceChance.Equals(controller.Configuration.CreateSettings().OccurrenceChance);
            passed &= Report(7, restored, "settings reset to the configuration, selection cleared");
            if (passed) Debug.Log("ENCOUNTER_ROLL_CHECK: PASS all steps");
            else Debug.LogError("ENCOUNTER_ROLL_CHECK: FAIL see the steps above");
        }

        private static bool Report(int step, bool ok, string detail)
        {
            if (ok) Debug.Log($"ENCOUNTER_ROLL_CHECK: step {step} PASS {detail}");
            else Debug.LogError($"ENCOUNTER_ROLL_CHECK: step {step} FAIL {detail}");
            return ok;
        }
    }
}
