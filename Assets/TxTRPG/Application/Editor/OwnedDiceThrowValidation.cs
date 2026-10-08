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
                var settings = panel.ThrowSettingsForEditor;
                var timeout = TemporaryDiceRollMenuController.ResolveRollTimeout(3f, panel.MaximumPresentationSeconds, 1f);
                report.Add($"limits: maxPresentation={panel.MaximumPresentationSeconds:F2}s, timeout(scene minimum 3s, margin 1s)={timeout:F2}s");

                // 0. Simulation-only sweep at the current composition: entry, retries and replay length for 3 and 20 dice.
                await Sweep(host, panel, settings, 3, 20, report);
                await Sweep(host, panel, settings, 20, 10, report);

                // 1. Every face index of every default die, several seeds, real panel path: off-screen swap, settle,
                // result hold, and no re-layout after the dice stop.
                var coverage = 0; var fallbacks = 0; var slowest = 0d; var longest = 0d; var oldRuleMoves = 0;
                var shortestHold = double.MaxValue; var largestShift = 0f; var closestRecall = float.MaxValue;
                var resolutions = new Dictionary<DiceRecallResolution, int>();
                for (var run = 0; run < 8; run++)
                {
                    var results = Results(host, (die, faces) => (run + die) % faces);
                    panel.SetThrowSeedForEditor(1000 + run * 37);
                    var watch = Stopwatch.StartNew();
                    var rolling = panel.PlayRollAsync(results, false);
                    closestRecall = Mathf.Min(closestRecall, await WatchRecall(panel, rolling, $"run {run}"));
                    if (!await rolling) throw new InvalidOperationException($"Run {run}: the panel could not present the roll.");
                    var resolution = panel.LastRollTimeline.RecallResolution;
                    resolutions[resolution] = resolutions.TryGetValue(resolution, out var seen) ? seen + 1 : 1;
                    longest = Math.Max(longest, watch.Elapsed.TotalSeconds);
                    if (panel.LastRollUsedPlacementFallback) fallbacks++;
                    slowest = Math.Max(slowest, panel.LastThrowOutcome?.ElapsedMilliseconds ?? 0d);
                    CheckFaces(panel, results, $"run {run}");
                    var timeline = CheckTimeline(panel, settings, $"run {run}");
                    shortestHold = Math.Min(shortestHold, timeline.RevealedAt - timeline.SettledAt);
                    largestShift = Mathf.Max(largestShift, timeline.ShiftAfterSettle);
                    if (DiceArenaLayout.HasOverlap(RestPositions(), panel.CurrentArena.DieRadius * 1.5f)) oldRuleMoves++;
                    coverage += results.Length;
                    await Frames(2);
                }
                if (longest > timeout) throw new InvalidOperationException($"A normal roll ({longest:F2}s) exceeded the timeout {timeout:F2}s.");
                report.Add($"faces: {coverage} forced results across 8 seeds matched, placementFallbacks={fallbacks}, slowestSimMs={slowest:F1}, longestRoll={longest:F2}s");
                report.Add($"settle: recalled and launch poses hidden outside the screen at every swap, shortest hold={shortestHold:F2}s (delay {settings.ResultRevealDelay:F2}s), " +
                    $"largest shift after settle={largestShift:F4}, rolls the old 1.5R overlap rule would have re-laid out={oldRuleMoves}/8");
                report.Add($"recall: resolutions {Describe(resolutions)}, closest on-screen pair during recall={closestRecall:F2} x rest footprint");

                // 1b. Dice placed near the four sides and corners leave through the nearest edge without meeting on screen.
                {
                    var arena = panel.CurrentArena;
                    var inset = arena.DieRadius * 1.2f;
                    float x0 = -arena.HalfWidth + inset, x1 = arena.HalfWidth - inset, z0 = arena.NearZ + inset, z1 = arena.FarZ - inset;
                    var layouts = new[]
                    {
                        new[] { new Vector2(x0, arena.CenterZ), new Vector2(x1, arena.CenterZ), new Vector2(0f, z0) },
                        new[] { new Vector2(0f, z1), new Vector2(x0, z0), new Vector2(x1, z1) },
                        new[] { new Vector2(x1, z0), new Vector2(x0, z1), new Vector2(0f, arena.CenterZ) },
                    };
                    var edgeReport = new List<string>();
                    for (var layout = 0; layout < layouts.Length; layout++)
                    {
                        panel.PlaceDiceForEditor(layouts[layout]);
                        await Frames(2);
                        var rested = DiceRoots().Select(root => root.localPosition).ToArray();
                        var expected = DiceArenaLayout.PlanRecall(arena, rested, RestHeights(host),
                            arena.DieRadius * settings.RecallLiftRadii);
                        var results = Results(host, (die, faces) => (layout + die) % faces);
                        panel.SetThrowSeedForEditor(3100 + layout);
                        var rolling = panel.PlayRollAsync(results, false);
                        closestRecall = Mathf.Min(closestRecall, await WatchRecall(panel, rolling, $"edge layout {layout}"));
                        if (!await rolling) throw new InvalidOperationException($"Edge layout {layout} roll failed.");
                        var timeline = CheckTimeline(panel, settings, $"edge layout {layout}");
                        if (!timeline.RecallEdges.SequenceEqual(expected.Edges))
                            throw new InvalidOperationException($"Edge layout {layout}: edges {string.Join(",", timeline.RecallEdges)} != planned {string.Join(",", expected.Edges)}.");
                        if (layout == 0 && expected.Resolution == DiceRecallResolution.NearestEdges &&
                            (timeline.RecallEdges[0] != DiceRecallEdge.Left || timeline.RecallEdges[1] != DiceRecallEdge.Right))
                            throw new InvalidOperationException($"Side dice did not leave through their sides: {string.Join(",", timeline.RecallEdges)}.");
                        CheckFaces(panel, results, $"edge layout {layout}");
                        edgeReport.Add($"[{string.Join(",", timeline.RecallEdges)} {timeline.RecallResolution}]");
                        await Frames(2);
                    }
                    report.Add($"edge layouts (sides/corners): {string.Join(" ", edgeReport)}, closest on-screen pair={closestRecall:F2} x rest footprint");
                }

                // 1c. Throw delay 0 starts the throw at the swap; the maximum delay still finishes inside the timeout.
                var defaultDelay = settings.ThrowDelay;
                try
                {
                    foreach (var delay in new[] { 0f, 2f })
                    {
                        settings.SetThrowDelayForEditor(delay);
                        var limit = TemporaryDiceRollMenuController.ResolveRollTimeout(3f, panel.MaximumPresentationSeconds, 1f);
                        var results = Results(host, (die, faces) => (die + 1) % faces);
                        panel.SetThrowSeedForEditor(5150);
                        var watch = Stopwatch.StartNew();
                        if (!await panel.PlayRollAsync(results, false)) throw new InvalidOperationException($"Delay {delay} roll failed.");
                        var timeline = CheckTimeline(panel, settings, $"delay {delay}");
                        if (watch.Elapsed.TotalSeconds > limit) throw new InvalidOperationException($"Delay {delay}: {watch.Elapsed.TotalSeconds:F2}s exceeded the timeout {limit:F2}s.");
                        report.Add($"throwDelay {delay:F1}s: measured {timeline.ThrowStartedAt - timeline.RecallEndedAt:F3}s, roll {watch.Elapsed.TotalSeconds:F2}s, timeout {limit:F2}s");
                        await Frames(2);
                    }
                }
                finally { settings.SetThrowDelayForEditor(defaultDelay); }

                // 2. Reduced motion skips the throw but still shows the decided faces at rest.
                var reduced = Results(host, (die, faces) => faces - 1 - die % faces);
                var reducedWatch = Stopwatch.StartNew();
                if (!await panel.PlayRollAsync(reduced, true)) throw new InvalidOperationException("Reduced motion could not present the roll.");
                CheckFaces(panel, reduced, "reduced motion");
                CheckTimeline(panel, settings, "reduced motion");
                report.Add($"reduced motion: {reducedWatch.Elapsed.TotalSeconds:F2}s including the result hold");

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
                var oldSeconds = settings.MaximumSimulationSeconds; var oldAttempts = settings.MaximumAttempts;
                settings.SetSimulationLimitsForEditor(0.5f, 1);
                try
                {
                    var failing = Results(host, (die, faces) => (die * 3 + 1) % faces);
                    if (!await panel.PlayRollAsync(failing, false)) throw new InvalidOperationException("Fallback roll failed.");
                    if (!panel.LastRollUsedPlacementFallback) throw new InvalidOperationException("The forced simulation failure did not use the placement fallback.");
                    CheckFaces(panel, failing, "simulation fallback");
                    CheckTimeline(panel, settings, "simulation fallback");
                    report.Add($"simulation failure: placement fallback showed decided faces ({panel.LastThrowOutcome?.FailureReason})");
                }
                finally { settings.SetSimulationLimitsForEditor(oldSeconds, oldAttempts); }

                // 4b. Timeout order: the caller cancels mid-throw and stops the panel; the dice must already be at rest
                // with the decided faces before the result would be applied, and the late task must not move them.
                {
                    var timedOut = Results(host, (die, faces) => (die + 3) % faces);
                    panel.SetThrowSeedForEditor(2468);
                    using var cancellation = new System.Threading.CancellationTokenSource();
                    var task = panel.PlayRollAsync(timedOut, false, cancellation.Token);
                    await Task.Delay(Mathf.CeilToInt((settings.RecallDuration + 0.35f) * 1000f));
                    if (!panel.IsRolling) throw new InvalidOperationException("The roll finished before the forced timeout.");
                    cancellation.Cancel();
                    panel.StopRollAtResults();
                    if (panel.IsRolling) throw new InvalidOperationException("StopRollAtResults left the panel rolling.");
                    CheckFaces(panel, timedOut, "timeout (same frame)");
                    var stopped = PoseSignature(panel);
                    if (await task) throw new InvalidOperationException("A cancelled roll reported success.");
                    await Frames(5);
                    if (PoseSignature(panel) != stopped) throw new InvalidOperationException("The cancelled roll moved the dice after the timeout.");
                    report.Add("timeout: dice stopped at rest with decided faces in the same frame, no later movement");
                }

                // 4c. Cancellation and panel hiding during the throw delay: decided faces at rest, no later movement.
                foreach (var hide in new[] { false, true })
                {
                    var during = Results(host, (die, faces) => (die + 4) % faces);
                    panel.SetThrowSeedForEditor(2600 + (hide ? 1 : 0));
                    using var cancellation = new System.Threading.CancellationTokenSource();
                    var task = panel.PlayRollAsync(during, false, cancellation.Token);
                    while (panel.IsRolling && panel.LastRollTimeline.RecallEndedAt <= 0f) await Task.Yield();
                    await Task.Delay(50);
                    if (!panel.IsRolling || panel.LastRollTimeline.ThrowStartedAt > 0f)
                        throw new InvalidOperationException("The throw delay ended before the interruption.");
                    if (hide) panel.gameObject.SetActive(false);
                    else { cancellation.Cancel(); panel.StopRollAtResults(); }
                    if (await task) throw new InvalidOperationException("An interrupted roll reported success.");
                    if (hide) { panel.gameObject.SetActive(true); await Frames(3); }
                    CheckFaces(panel, during, hide ? "hidden during delay" : "cancelled during delay");
                    var stopped = PoseSignature(panel);
                    await Frames(5);
                    if (PoseSignature(panel) != stopped) throw new InvalidOperationException("The interrupted roll moved the dice later.");
                }
                report.Add("throw delay interruptions: cancel+StopRollAtResults and panel hide both left decided faces at rest");

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
                    var manyDelay = settings.ThrowDelay;
                    if (target == 20) settings.SetThrowDelayForEditor(2f);
                    try
                    {
                        var limit = TemporaryDiceRollMenuController.ResolveRollTimeout(3f, panel.MaximumPresentationSeconds, 1f);
                        var watch = Stopwatch.StartNew();
                        var rolling = panel.PlayRollAsync(many, false);
                        var closest = await WatchRecall(panel, rolling, $"{target} dice");
                        if (!await rolling) throw new InvalidOperationException($"{target} dice roll failed.");
                        CheckFaces(panel, many, $"{target} dice");
                        CheckTimeline(panel, settings, $"{target} dice");
                        if (watch.Elapsed.TotalSeconds > limit) throw new InvalidOperationException($"{target} dice: {watch.Elapsed.TotalSeconds:F2}s exceeded the timeout {limit:F2}s.");
                        report.Add($"{target} dice: fallback={panel.LastRollUsedPlacementFallback}, attempts={panel.LastThrowOutcome?.Attempts}, simMs={panel.LastThrowOutcome?.ElapsedMilliseconds:F1}, " +
                            $"roll={watch.Elapsed.TotalSeconds:F2}s (throwDelay {settings.ThrowDelay:F1}s, timeout {limit:F2}s), recall={panel.LastRollTimeline.RecallResolution}, " +
                            $"closest recall pair={closest:F2}, ortho={panel.CurrentArena.OrthographicSize:F1}");
                    }
                    finally { settings.SetThrowDelayForEditor(manyDelay); }
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

        private static async Task Sweep(PlayerSessionHost host, OwnedDicePanel panel, DiceThrowSettings settings,
            int count, int seeds, List<string> report)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DieShapeCatalog>("Assets/TxTRPG/UI/Styles/Dice/DieShapeCatalog.asset");
            var owned = host.TemporaryDice.Snapshot();
            var shapes = Enumerable.Range(0, count).Select(i => i < owned.Count
                ? catalog.Find(owned[i].DefinitionId, owned[i].FaceCount) : catalog.Find("temporary.d6", 6)).ToArray();
            if (shapes.Any(shape => shape == null)) throw new InvalidOperationException("Sweep shapes are missing.");
            var arena = DiceArenaLayout.Compute(panel.CurrentArena.Aspect, count, shapes.Max(shape => shape.BoundingRadius), new DiceArenaSettings());
            var bodies = shapes.Select(shape => new DiceThrowBody(shape.ColliderMesh, shape.FaceNormals, shape.RestHeight)).ToArray();
            using var simulator = new DiceThrowSimulator();
            var ok = 0; var attempts = 0; var slowest = 0d; var longest = 0f; var entry = 0f;
            var clearance = float.MaxValue; var snap = 0f; var snapDegrees = 0f;
            var failures = new Dictionary<string, int>();
            var recalls = new Dictionary<DiceRecallResolution, int>();
            var heights = shapes.Select(shape => shape.RestHeight).ToArray();
            for (var seed = 1; seed <= seeds; seed++)
            {
                var outcome = simulator.Simulate(bodies, arena, seed * 131 + count, settings);
                if (outcome.Success)
                {
                    // The settled layout of this throw is what the next roll recalls.
                    var rested = new Vector3[count];
                    for (var die = 0; die < count; die++) outcome.Trajectory.Final(die, out rested[die], out _);
                    var plan = DiceArenaLayout.PlanRecall(arena, rested, heights, arena.DieRadius * settings.RecallLiftRadii);
                    recalls[plan.Resolution] = recalls.TryGetValue(plan.Resolution, out var seen) ? seen + 1 : 1;
                }
                attempts += outcome.Attempts;
                slowest = Math.Max(slowest, outcome.ElapsedMilliseconds);
                if (outcome.Success)
                {
                    ok++;
                    longest = Mathf.Max(longest, outcome.Trajectory.Duration);
                    entry = Mathf.Max(entry, outcome.EntrySeconds);
                    clearance = Mathf.Min(clearance, outcome.EntryCeilingClearance);
                    snap = Mathf.Max(snap, outcome.FinalSnapDistance);
                    snapDegrees = Mathf.Max(snapDegrees, outcome.FinalSnapDegrees);
                    for (var die = 0; die < count; die++)
                    {
                        outcome.Trajectory.Sample(die, 0f, out var launch, out _);
                        if (!DiceArenaLayout.IsBelowScreen(arena, launch, arena.DieRadius))
                            throw new InvalidOperationException($"Sweep {count}: die {die} launches on screen.");
                    }
                }
                else
                {
                    var key = outcome.FailureReason.Split('(')[0];
                    failures[key] = failures.TryGetValue(key, out var n) ? n + 1 : 1;
                }
                await Task.Yield();
            }
            report.Add($"sweep {count} dice x{seeds}: success={ok}/{seeds} (fallback rate {(seeds - ok) * 100 / seeds}%), attempts={attempts}, " +
                $"slowestSimMs={slowest:F1}, longestReplay={longest / settings.PlaybackSpeed:F2}s, longestEntry={entry:F2}s, " +
                $"minCeilingClearance={clearance:F2}, finalSnap={snap:F4}/{snapDegrees:F2}deg, recall on settled layouts {Describe(recalls)}" +
                (failures.Count > 0 ? ", failures: " + string.Join("; ", failures.Select(pair => $"{pair.Key.Trim()} x{pair.Value}")) : string.Empty));
        }

        private static OwnedDiceRollTimeline CheckTimeline(OwnedDicePanel panel, DiceThrowSettings settings, string stage)
        {
            var timeline = panel.LastRollTimeline ?? throw new InvalidOperationException($"{stage}: no roll timeline.");
            if (timeline.UsedThrow && !timeline.HiddenAtSwap) throw new InvalidOperationException($"{stage}: {timeline.SwapDetail}");
            if (timeline.UsedThrow)
            {
                var delay = timeline.ThrowStartedAt - timeline.RecallEndedAt;
                if (delay < settings.ThrowDelay - 0.005f || delay > settings.ThrowDelay + 0.06f)
                    throw new InvalidOperationException($"{stage}: the throw started {delay:F3}s after the recall (throwDelay {settings.ThrowDelay:F2}s).");
            }
            if (timeline.RevealedAt - timeline.SettledAt < settings.ResultRevealDelay - 0.02f)
                throw new InvalidOperationException($"{stage}: the result was released {timeline.RevealedAt - timeline.SettledAt:F2}s after the dice stopped.");
            if (timeline.ShiftAfterSettle > 0.001f)
                throw new InvalidOperationException($"{stage}: a die moved {timeline.ShiftAfterSettle:F3} after it stopped.");
            return timeline;
        }

        // Polls every frame while the recall runs and returns the smallest centre distance between two participants,
        // either of them on screen, as a multiple of their rest footprint. Values below 1 fail.
        private static async Task<float> WatchRecall(OwnedDicePanel panel, Task rolling, string stage)
        {
            var arena = panel.CurrentArena;
            var host = PlayerSessionHost.Instance;
            var heights = RestHeights(host);
            var closest = float.MaxValue;
            while (!rolling.IsCompleted && panel.LastRollTimeline != null && panel.LastRollTimeline.RecallEndedAt <= 0f)
            {
                var roots = DiceRoots();
                for (var i = 0; i < roots.Length && i < heights.Length; i++)
                    for (var j = 0; j < i; j++)
                    {
                        var a = roots[i].localPosition; var b = roots[j].localPosition;
                        if (DiceArenaLayout.IsOffscreen(arena, a, arena.DieRadius) && DiceArenaLayout.IsOffscreen(arena, b, arena.DieRadius)) continue;
                        closest = Mathf.Min(closest, (a - b).magnitude / (heights[i] + heights[j]));
                    }
                await Task.Yield();
            }
            if (closest < DiceArenaLayout.RestSeparationFactor)
                throw new InvalidOperationException($"{stage}: two dice met on screen during the recall ({closest:F2} x rest footprint).");
            return closest;
        }

        // Dice roots in session (display) order.
        private static Transform[] DiceRoots()
        {
            var host = PlayerSessionHost.Instance;
            var roots = UnityEngine.Object.FindObjectsByType<DieModelView>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Select(model => model.transform.parent).ToDictionary(root => root.name, StringComparer.Ordinal);
            return host.TemporaryDice.Snapshot().Select(die => roots.TryGetValue($"Die-{die.InstanceId}", out var root) ? root : null)
                .Where(root => root != null).ToArray();
        }

        private static float[] RestHeights(PlayerSessionHost host)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DieShapeCatalog>("Assets/TxTRPG/UI/Styles/Dice/DieShapeCatalog.asset");
            return host.TemporaryDice.Snapshot().Select(die => catalog.Find(die.DefinitionId, die.FaceCount))
                .Where(shape => shape != null).Select(shape => shape.RestHeight).ToArray();
        }

        private static string Describe(Dictionary<DiceRecallResolution, int> counts) =>
            string.Join(", ", counts.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value}"));

        private static Vector2[] RestPositions() =>
            UnityEngine.Object.FindObjectsByType<DieModelView>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Select(model => model.transform.parent.localPosition).Select(p => new Vector2(p.x, p.z)).ToArray();

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
