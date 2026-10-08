using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using TxTRPG.Application.Dice;
using TxTRPG.Application.Players;
using TxTRPG.Gameplay.Dice;
using TxTRPG.UI;
using TxTRPG.UI.Dice;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace TxTRPG.Application.Editor
{
    // Temporary Play Mode verification for the physics throw presentation. It calls OwnedDicePanel.PlayRollAsync
    // directly with forced face indices, so no combat turn, history record or save data is changed. Only Play state is
    // modified (temporary extra dice are removed again). Safe to rerun; retire with the other Temporary dice menus.
    public static class OwnedDiceThrowValidation
    {
        [MenuItem("Tools/TxT RPG/UI/Dice/Temporary/Validate Throw Presentation")]
        public static async void Validate()
        {
            var report = new List<string>();
            var added = new List<string>();
            PlayerSessionHost host = null;
            OwnedDicePanel panel = null;
            var previousFrameRate = UnityEngine.Application.targetFrameRate;
            var previousVSync = QualitySettings.vSyncCount;
            try
            {
                if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
                host = PlayerSessionHost.Instance ?? throw new InvalidOperationException("The player session is missing.");
                var binder = UnityEngine.Object.FindAnyObjectByType<OwnedDiceSessionBinder>()
                    ?? throw new InvalidOperationException("OwnedDiceSessionBinder is missing.");
                panel = binder.Panel;
                if (panel.IsRolling || host.IsTemporaryDiceActionPending) throw new InvalidOperationException("Wait for the current roll.");
                await Frames(3);

                // 1. Every face index of every default die, several seeds, real panel path.
                var coverage = 0; var fallbacks = 0; var slowest = 0d; var longest = 0d;
                for (var run = 0; run < 8; run++)
                {
                    var results = Results(host, (die, faces) => (run + die) % faces);
                    panel.SetThrowSeedForEditor(1000 + run * 37);
                    var watch = Stopwatch.StartNew();
                    if (!await panel.PlayRollAsync(results, false)) throw new InvalidOperationException($"Run {run}: the panel could not present the roll.");
                    longest = Math.Max(longest, watch.Elapsed.TotalSeconds);
                    if (panel.LastRollUsedPlacementFallback) fallbacks++;
                    slowest = Math.Max(slowest, panel.LastThrowOutcome?.ElapsedMilliseconds ?? 0d);
                    CheckFaces(panel, results, $"run {run}");
                    coverage += results.Length;
                    await Frames(2);
                }
                report.Add($"faces: {coverage} forced results across 8 seeds matched, placementFallbacks={fallbacks}, slowestSimMs={slowest:F1}, longestRoll={longest:F2}s");

                // 2. Reduced motion skips the throw but still shows the decided faces at rest.
                var reduced = Results(host, (die, faces) => faces - 1 - die % faces);
                var reducedWatch = Stopwatch.StartNew();
                if (!await panel.PlayRollAsync(reduced, true)) throw new InvalidOperationException("Reduced motion could not present the roll.");
                CheckFaces(panel, reduced, "reduced motion");
                report.Add($"reduced motion: {reducedWatch.Elapsed.TotalSeconds:F2}s");

                // 3. Same seed at different frame rates: same final pose and same replay length.
                QualitySettings.vSyncCount = 0;
                var poses = new List<(double Seconds, string Pose)>();
                foreach (var fps in new[] { 30, 120 })
                {
                    UnityEngine.Application.targetFrameRate = fps;
                    await Frames(5);
                    panel.SetThrowSeedForEditor(777);
                    var rate = Results(host, (die, faces) => die % faces);
                    var watch = Stopwatch.StartNew();
                    if (!await panel.PlayRollAsync(rate, false)) throw new InvalidOperationException($"{fps}fps roll failed.");
                    poses.Add((watch.Elapsed.TotalSeconds, PoseSignature(panel)));
                    CheckFaces(panel, rate, $"{fps}fps");
                }
                UnityEngine.Application.targetFrameRate = previousFrameRate;
                QualitySettings.vSyncCount = previousVSync;
                if (poses[0].Pose != poses[1].Pose) throw new InvalidOperationException($"Frame rate changed the final pose:\n{poses[0].Pose}\n{poses[1].Pose}");
                if (Math.Abs(poses[0].Seconds - poses[1].Seconds) > 0.12) throw new InvalidOperationException($"Frame rate changed replay length: {poses[0].Seconds:F2}s vs {poses[1].Seconds:F2}s");
                report.Add($"frame rate: 30fps {poses[0].Seconds:F2}s / 120fps {poses[1].Seconds:F2}s, identical final poses");

                // 4. Simulation that cannot settle -> placement fallback still shows the decided faces.
                var settings = panel.ThrowSettingsForEditor;
                var oldSeconds = settings.MaximumSimulationSeconds; var oldAttempts = settings.MaximumAttempts;
                settings.SetSimulationLimitsForEditor(0.5f, 1);
                try
                {
                    var failing = Results(host, (die, faces) => (die * 3 + 1) % faces);
                    if (!await panel.PlayRollAsync(failing, false)) throw new InvalidOperationException("Fallback roll failed.");
                    if (!panel.LastRollUsedPlacementFallback) throw new InvalidOperationException("The forced simulation failure did not use the placement fallback.");
                    CheckFaces(panel, failing, "simulation fallback");
                }
                finally { settings.SetSimulationLimitsForEditor(oldSeconds, oldAttempts); }
                report.Add("simulation failure: placement fallback showed decided faces");

                // 5. Viewport resize during a throw keeps the composition until the end, then re-fits.
                var screen = UnityEngine.Object.FindAnyObjectByType<ResponsiveMainScreen>();
                var viewport = screen?.PageScroll?.viewport ?? throw new InvalidOperationException("Responsive viewport is missing.");
                var oldMin = viewport.anchorMin; var oldMax = viewport.anchorMax; var oldSize = viewport.sizeDelta; var oldPosition = viewport.anchoredPosition;
                try
                {
                    var resize = Results(host, (die, faces) => (die + 2) % faces);
                    panel.SetThrowSeedForEditor(4321);
                    var task = panel.PlayRollAsync(resize, false);
                    await Task.Delay(300);
                    var arenaBefore = panel.CurrentArena.OrthographicSize;
                    viewport.anchorMin = viewport.anchorMax = new Vector2(.5f, .5f);
                    viewport.sizeDelta = new Vector2(390f, 844f);
                    viewport.anchoredPosition = Vector2.zero;
                    await Frames(5);
                    if (panel.IsRolling && !Mathf.Approximately(arenaBefore, panel.CurrentArena.OrthographicSize))
                        throw new InvalidOperationException("The camera composition changed during a throw.");
                    if (!await task) throw new InvalidOperationException("Resize roll failed.");
                    await Frames(5);
                    CheckFaces(panel, resize, "narrow after resize");
                    report.Add($"resize during throw: narrow aspect {panel.CurrentArena.Aspect:F2}, dice inside and separated");
                }
                finally
                {
                    viewport.anchorMin = oldMin; viewport.anchorMax = oldMax; viewport.sizeDelta = oldSize; viewport.anchoredPosition = oldPosition;
                }
                await Frames(5);
                if (!panel.AreAllDiceVisibleAndSeparated(out var restored)) throw new InvalidOperationException($"Restored layout: {restored}");

                // 6. Many dice and same shapes; a runtime 4-face die is reported, never disguised.
                var d6Faces = Enumerable.Range(1, 6).Select(i => new DiceFace(i % 2 == 1 ? DiceEffectKind.Attack : DiceEffectKind.Heal, i)).ToArray();
                foreach (var target in new[] { 6, 12, 20 })
                {
                    while (host.TemporaryDice.Count < target)
                    {
                        var id = $"validation.extra-{added.Count + 1}";
                        if (!host.TemporaryDice.TryAdd(id, "temporary.d6", $"검증 D6 {added.Count + 1}", d6Faces, 1, 6, out var reason))
                            throw new InvalidOperationException(reason);
                        added.Add(id);
                    }
                    await Frames(3);
                    if (!panel.AreAllDiceVisibleAndSeparated(out var orderly)) throw new InvalidOperationException($"{target} dice orderly: {orderly}");
                    var many = Results(host, (die, faces) => die % faces);
                    panel.SetThrowSeedForEditor(900 + target);
                    var watch = Stopwatch.StartNew();
                    if (!await panel.PlayRollAsync(many, false)) throw new InvalidOperationException($"{target} dice roll failed.");
                    CheckFaces(panel, many, $"{target} dice");
                    report.Add($"{target} dice: fallback={panel.LastRollUsedPlacementFallback}, attempts={panel.LastThrowOutcome?.Attempts}, simMs={panel.LastThrowOutcome?.ElapsedMilliseconds:F1}, roll={watch.Elapsed.TotalSeconds:F2}s, ortho={panel.CurrentArena.OrthographicSize:F1}");
                }
                if (!host.TemporaryDice.TryAdd("validation.d4", "temporary.d4", "검증 D4",
                        Enumerable.Range(1, 4).Select(i => new DiceFace(DiceEffectKind.Attack, i)), 1, 4, out var d4Reason))
                    throw new InvalidOperationException(d4Reason);
                added.Add("validation.d4");
                await Frames(2);
                var withD4 = Results(host, (die, faces) => 0);
                if (await panel.PlayRollAsync(withD4, false)) throw new InvalidOperationException("A 4-face die was presented by another shape.");
                if (panel.TryGetUpFace("validation.d4", out _)) throw new InvalidOperationException("A 4-face die has a 3D visual.");
                report.Add("runtime 4-face die: no 3D visual, PlayRollAsync=false -> text fallback path");

                var physicsScenes = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt)
                    .Count(scene => scene.name.StartsWith("OwnedDicePhysics-", StringComparison.Ordinal));
                report.Add($"isolated physics scenes loaded: {physicsScenes}");
                if (physicsScenes > 1) throw new InvalidOperationException("Isolated physics scenes accumulated.");
                Debug.Log("OWNED_DICE_THROW_VALIDATION: passed\n" + string.Join("\n", report));
            }
            catch (Exception exception)
            {
                Debug.LogError($"OWNED_DICE_THROW_VALIDATION_FAILED: {exception}\n" + string.Join("\n", report));
            }
            finally
            {
                UnityEngine.Application.targetFrameRate = previousFrameRate;
                QualitySettings.vSyncCount = previousVSync;
                if (panel != null) panel.SetThrowSeedForEditor(0);
                if (host != null) foreach (var id in added) host.TemporaryDice.TryRemove(id);
            }
        }

        private static DieRollDisplay[] Results(PlayerSessionHost host, Func<int, int, int> face)
        {
            var dice = host.TemporaryDice.Snapshot();
            return dice.Select((die, index) =>
            {
                var chosen = face(index, die.FaceCount);
                return new DieRollDisplay(die.InstanceId, chosen, die.Faces[chosen].EffectKind, die.Faces[chosen].Amount);
            }).ToArray();
        }

        private static void CheckFaces(OwnedDicePanel panel, IReadOnlyList<DieRollDisplay> results, string stage)
        {
            foreach (var roll in results)
                if (!panel.TryGetUpFace(roll.InstanceId, out var face) || face != roll.FaceIndex)
                    throw new InvalidOperationException($"{stage}: {roll.InstanceId} shows face {face + 1}, decided {roll.FaceIndex + 1}.");
            if (!panel.AreAllDiceVisibleAndSeparated(out var detail)) throw new InvalidOperationException($"{stage}: {detail}");
        }

        private static string PoseSignature(OwnedDicePanel panel)
        {
            var dice = UnityEngine.Object.FindObjectsByType<DieModelView>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .OrderBy(model => model.transform.parent.name, StringComparer.Ordinal);
            return string.Join(";", dice.Select(model => $"{model.transform.parent.name}:{model.transform.position.ToString("F3")}:{model.transform.rotation.eulerAngles.ToString("F1")}"));
        }

        private static async Task Frames(int count)
        {
            for (var i = 0; i < count; i++) await Task.Yield();
            await Task.Delay(20);
        }
    }
}
