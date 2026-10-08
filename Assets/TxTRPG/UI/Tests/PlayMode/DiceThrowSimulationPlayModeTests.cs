#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TxTRPG.UI.Dice;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TxTRPG.UI.Tests
{
    // Runs the real isolated-physics path. The decided face is forced for every face index and several seeds.
    public sealed class DiceThrowSimulationPlayModeTests
    {
        private const string CatalogPath = "Assets/TxTRPG/UI/Styles/Dice/DieShapeCatalog.asset";

        [UnityTest]
        public IEnumerator EveryDecidedFace_EndsUpAfterCorrectionInsideTheVisibleBox()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DieShapeCatalog>(CatalogPath);
            var d6 = catalog.Find("temporary.d6", 6);
            var d8 = catalog.Find("temporary.d8", 8);
            var shapes = new[] { d6, d6, d8 };
            var settings = new DiceThrowSettings();
            var arena = DiceArenaLayout.Compute(1.78f, shapes.Length, shapes.Max(s => s.BoundingRadius), new DiceArenaSettings());
            var bodies = shapes.Select(s => new DiceThrowBody(s.ColliderMesh, s.FaceNormals, s.RestHeight)).ToArray();
            var scenesBefore = SceneManager.sceneCount;
            var simulator = new DiceThrowSimulator();
            var successes = 0; var attempts = 0; var total = 0; var slowest = 0d;
            var longestEntry = 0f; var lowestClearance = float.MaxValue; var largestSnap = 0f; var largestSnapDegrees = 0f; var longest = 0f;
            try
            {
                for (var seed = 1; seed <= 8; seed++)
                {
                    var outcome = simulator.Simulate(bodies, arena, seed * 1013, settings);
                    total++;
                    attempts += outcome.Attempts;
                    slowest = System.Math.Max(slowest, outcome.ElapsedMilliseconds);
                    if (!outcome.Success) continue;
                    successes++;
                    Assert.That(outcome.Trajectory.Duration, Is.LessThanOrEqualTo(settings.MaximumSimulationSeconds + .001f));
                    Assert.That(outcome.EntrySeconds, Is.InRange(.001f, settings.MaximumEntrySeconds + .001f));
                    longestEntry = Mathf.Max(longestEntry, outcome.EntrySeconds);
                    lowestClearance = Mathf.Min(lowestClearance, outcome.EntryCeilingClearance);
                    largestSnap = Mathf.Max(largestSnap, outcome.FinalSnapDistance);
                    largestSnapDegrees = Mathf.Max(largestSnapDegrees, outcome.FinalSnapDegrees);
                    longest = Mathf.Max(longest, outcome.Trajectory.Duration);
                    for (var die = 0; die < shapes.Length; die++)
                    {
                        var shape = shapes[die];
                        outcome.Trajectory.Sample(die, 0f, out var launch, out _);
                        Assert.That(DiceArenaLayout.IsBelowScreen(arena, launch, arena.DieRadius), Is.True, $"seed {seed} die {die} launch");
                        for (var frame = 0f; frame <= outcome.Trajectory.Duration; frame += settings.SimulationStep)
                        {
                            outcome.Trajectory.Sample(die, frame, out var position, out _);
                            Assert.That(Mathf.Abs(position.x), Is.LessThanOrEqualTo(arena.HalfWidth - shape.RestHeight + .05f));
                            Assert.That(position.y, Is.InRange(shape.RestHeight - .05f, arena.Ceiling - shape.RestHeight + .05f));
                            // After the camera-side wall closes every die stays inside the visible arena.
                            if (frame >= outcome.EntrySeconds)
                                Assert.That(position.z, Is.InRange(arena.NearZ + shape.RestHeight - .05f, arena.FarZ - shape.RestHeight + .05f));
                        }
                        outcome.Trajectory.Final(die, out _, out var final);
                        for (var decided = 0; decided < shape.FaceCount; decided++)
                        {
                            Assert.That(DieFaceGeometry.TrySelectCorrection(shape.Symmetries, shape.FaceNormals, shape.FaceReadingUps,
                                outcome.UpFaces[die], decided, final, Vector3.forward, out var correction), Is.True);
                            Assert.That(DieFaceGeometry.UpFace(final * correction, shape.FaceNormals, Vector3.up, out var alignment),
                                Is.EqualTo(decided), $"seed {seed} die {die} face {decided}");
                            Assert.That(alignment, Is.GreaterThan(Mathf.Cos(settings.FlatToleranceDegrees * Mathf.Deg2Rad)));
                        }
                    }
                    yield return null;
                }
            }
            finally { simulator.Dispose(); }
            Debug.Log($"OWNED_DICE_SIM_METRICS: success={successes}/{total} attempts={attempts} slowestMs={slowest:F1} " +
                $"longestSim={longest:F2}s longestEntry={longestEntry:F2}s minCeilingClearance={lowestClearance:F2} " +
                $"finalSnap={largestSnap:F4}/{largestSnapDegrees:F2}deg");
            Assert.That(successes, Is.GreaterThanOrEqualTo(total - 1), "Valid rests should be found within the retry budget.");
            for (var frame = 0; frame < 10 && SceneManager.sceneCount > scenesBefore; frame++) yield return null;
            Assert.That(SceneManager.sceneCount, Is.EqualTo(scenesBefore), "The isolated physics Scene must be released.");
        }

        [UnityTest]
        public IEnumerator SameSeed_ProducesTheSameRecordedRest()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DieShapeCatalog>(CatalogPath);
            var d6 = catalog.Find("temporary.d6", 6);
            var settings = new DiceThrowSettings();
            var arena = DiceArenaLayout.Compute(1.4f, 2, d6.BoundingRadius, new DiceArenaSettings());
            var bodies = new[] { new DiceThrowBody(d6.ColliderMesh, d6.FaceNormals, d6.RestHeight),
                new DiceThrowBody(d6.ColliderMesh, d6.FaceNormals, d6.RestHeight) };
            var simulator = new DiceThrowSimulator();
            try
            {
                var first = simulator.Simulate(bodies, arena, 4242, settings);
                yield return null;
                var second = simulator.Simulate(bodies, arena, 4242, settings);
                Assert.That(second.Attempts, Is.EqualTo(first.Attempts));
                Assert.That(second.Success, Is.EqualTo(first.Success));
                if (first.Success)
                {
                    Assert.That(second.Trajectory.FrameCount, Is.EqualTo(first.Trajectory.FrameCount));
                    for (var die = 0; die < 2; die++)
                    {
                        first.Trajectory.Final(die, out var a, out var ra);
                        second.Trajectory.Final(die, out var b, out var rb);
                        Assert.That(Vector3.Distance(a, b), Is.LessThan(1e-3f));
                        Assert.That(Quaternion.Angle(ra, rb), Is.LessThan(.1f));
                    }
                }
            }
            finally { simulator.Dispose(); }
        }

        [UnityTest]
        public IEnumerator Capacity_MeasuresHowManyDiceStillSettleOnTheFloor()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DieShapeCatalog>(CatalogPath);
            var d6 = catalog.Find("temporary.d6", 6);
            var settings = new DiceThrowSettings();
            var simulator = new DiceThrowSimulator();
            var report = new List<string>();
            try
            {
                foreach (var count in new[] { 1, 3, 6, 10, 16, 20, 24 })
                {
                    var arena = DiceArenaLayout.Compute(1.78f, count, d6.BoundingRadius, new DiceArenaSettings());
                    var bodies = Enumerable.Repeat(new DiceThrowBody(d6.ColliderMesh, d6.FaceNormals, d6.RestHeight), count).ToArray();
                    var ok = 0; var ms = 0d; var attempts = 0; var longest = 0f; var entry = 0f;
                    var failures = new List<string>();
                    for (var seed = 1; seed <= 6; seed++)
                    {
                        var outcome = simulator.Simulate(bodies, arena, seed * 77 + count, settings);
                        attempts += outcome.Attempts;
                        if (outcome.Success)
                        {
                            ok++;
                            longest = Mathf.Max(longest, outcome.Trajectory.Duration);
                            entry = Mathf.Max(entry, outcome.EntrySeconds);
                        }
                        else failures.Add(outcome.FailureReason);
                        ms = System.Math.Max(ms, outcome.ElapsedMilliseconds);
                        yield return null;
                    }
                    report.Add($"{count} dice: {ok}/6 settled, attempts {attempts}, slowest {ms:F1}ms, longestSim {longest:F2}s, " +
                        $"longestEntry {entry:F2}s, orthoSize {arena.OrthographicSize:F1}" + (failures.Count > 0 ? $", last failure: {failures[^1]}" : string.Empty));
                }
            }
            finally { simulator.Dispose(); }
            Debug.Log("OWNED_DICE_CAPACITY: " + string.Join(" | ", report));
            Assert.That(report[1], Does.StartWith("3 dice: 6/6").Or.StartWith("3 dice: 5/6"), report[1]);
        }
    }
}
#endif
