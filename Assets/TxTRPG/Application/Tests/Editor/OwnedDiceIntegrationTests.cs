using System;
using System.Linq;
using NUnit.Framework;
using TxTRPG.Application.Dice;
using TxTRPG.Application.Editor;
using TxTRPG.Gameplay.Combat;
using TxTRPG.Gameplay.Dice;
using TxTRPG.UI.Dice;
using UnityEditor;
using UnityEngine;

namespace TxTRPG.Application.Tests
{
    public sealed class OwnedDiceIntegrationTests
    {
        private const string CatalogPath = "Assets/TxTRPG/UI/Styles/Dice/DieShapeCatalog.asset";
        private static readonly (string Id, int Count)[] DefaultShapes =
            { ("temporary.d6", 6), (TemporaryDiceConfiguration.SecondaryD6Id, 6), ("temporary.d8", 8) };

        [Test]
        public void GeneratedShapes_RestEveryFaceFlatUpAndReadableFromTheCamera()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DieShapeCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.TryValidate(out var diagnostics), Is.True, string.Join("\n", diagnostics));
            foreach (var (id, count) in DefaultShapes)
            {
                var shape = catalog.Find(id, count);
                Assert.That(shape, Is.Not.Null, id);
                Assert.That(shape.IsPresentable, Is.True, shape.ValidationError);
                Assert.That(TemporaryOwnedDiceIntegration.TryValidateFaceLayout(shape, out var error), Is.True, $"{id}: {error}");
                Assert.That(shape.ColliderMesh, Is.SameAs(shape.ModelPrefab.GetComponent<MeshFilter>().sharedMesh), id);
                Assert.That(shape.ColliderMesh.isReadable, Is.True, id);
                for (var face = 0; face < count; face++)
                {
                    Assert.That(Vector3.Dot(shape.PoseFor(face) * shape.FaceNormals[face], Vector3.up), Is.GreaterThan(.999f), $"{id} face {face}");
                    Assert.That(Vector3.Dot(shape.PoseFor(face) * shape.FaceReadingUps[face], Vector3.forward), Is.GreaterThan(.999f), $"{id} face {face}");
                }
            }
            Assert.That(catalog.Find("temporary.d6", 6), Is.SameAs(catalog.Find(TemporaryDiceConfiguration.SecondaryD6Id, 6)),
                "The secondary D6 shares the D6 shape instead of a duplicated model.");
        }

        [Test]
        public void Corrections_ForEveryRestingAndDecidedFacePair_AreMeshSymmetriesThatShowTheDecidedFace()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DieShapeCatalog>(CatalogPath);
            foreach (var (id, count) in new[] { ("temporary.d6", 6), ("temporary.d8", 8) })
            {
                var shape = catalog.Find(id, count);
                var vertices = shape.ColliderMesh.vertices.Select(v => v.normalized).ToArray();
                Assert.That(shape.Symmetries.Count, Is.EqualTo(24), $"{id} rotation group order");
                for (var resting = 0; resting < count; resting++)
                    for (var decided = 0; decided < count; decided++)
                        foreach (var yaw in new[] { 0f, 37f, 145f, 290f })
                        {
                            var body = Quaternion.Euler(0f, yaw, 0f) *
                                DieFaceGeometry.RestPose(shape.FaceNormals[resting], shape.FaceReadingUps[resting], Vector3.up, Vector3.forward);
                            Assert.That(DieFaceGeometry.TrySelectCorrection(shape.Symmetries, shape.FaceNormals, shape.FaceReadingUps,
                                resting, decided, body, Vector3.forward, out var correction), Is.True, $"{id} {resting}->{decided}");
                            Assert.That(DieFaceGeometry.MapsOntoItself(correction, vertices), Is.True, $"{id} {resting}->{decided} vertices");
                            Assert.That(Vector3.Dot(body * correction * shape.FaceNormals[decided], Vector3.up), Is.GreaterThan(.999f));
                            Assert.That(DieFaceGeometry.UpFace(body * correction, shape.FaceNormals, Vector3.up, out _), Is.EqualTo(decided));
                        }
            }
        }

        [Test]
        public void Tetrahedron_IsRejectedAndFourFaceDiceHaveNoDisguisedShape()
        {
            var points = new[] { new Vector3(1, 1, 1), new Vector3(-1, -1, 1), new Vector3(-1, 1, -1), new Vector3(1, -1, -1) };
            var normals = points.Select(point => -point.normalized).ToArray();
            var ups = normals.Select(normal => Quaternion.LookRotation(-normal, Vector3.up) * Vector3.up).ToArray();
            Assert.That(DieFaceGeometry.HasOppositeFaces(normals, out var missing), Is.False);
            Assert.That(missing, Is.EqualTo(0));
            Assert.That(DieFaceGeometry.TryValidate(normals, ups, out var error), Is.False);
            StringAssert.Contains("parallel opposite face", error);
            var catalog = AssetDatabase.LoadAssetAtPath<DieShapeCatalog>(CatalogPath);
            Assert.That(catalog.Find("temporary.d4", 4), Is.Null);
            Assert.That(catalog.Find("temporary.d6", 4), Is.Null, "A 4-face die must not borrow the cube.");
            Assert.That(catalog.Shapes.Any(shape => shape == null || shape.FaceCount == 4), Is.False);
        }

        [Test]
        public void ArenaLayout_KeepsWallsCeilingAndOrderlySlotsVisibleForManyAspectsAndCounts()
        {
            var settings = new DiceArenaSettings();
            foreach (var aspect in new[] { 0.4f, 0.5625f, 0.75f, 1f, 1.333f, 1.778f, 2.4f, 3.6f })
                foreach (var count in new[] { 1, 3, 6, 12, 24 })
                {
                    var arena = DiceArenaLayout.Compute(aspect, count, 1.75f, settings);
                    Assert.That(DiceArenaLayout.IsBoxVisible(arena), Is.True, $"{aspect}/{count}");
                    var slots = DiceArenaLayout.OrderlyPositions(arena, count);
                    Assert.That(DiceArenaLayout.HasOverlap(slots, arena.Spacing * .99f), Is.False, $"{aspect}/{count}");
                    foreach (var slot in slots)
                    {
                        Assert.That(Mathf.Abs(slot.x), Is.LessThanOrEqualTo(arena.InnerHalfWidth + .001f), $"{aspect}/{count}");
                        Assert.That(slot.y, Is.InRange(arena.InnerNearZ - .001f, arena.InnerFarZ + .001f), $"{aspect}/{count}");
                    }
                }
        }

        [Test]
        public void OffscreenStarts_AreFullyBelowTheScreenInsideTheSideWallsAndApart()
        {
            var settings = new DiceArenaSettings();
            const float radius = 1.75f;
            foreach (var aspect in new[] { 0.4f, 0.5625f, 0.75f, 1f, 1.333f, 1.778f, 2.4f, 3.6f })
                foreach (var count in new[] { 1, 3, 6, 12, 20, 24 })
                    foreach (var height in new[] { 1.2f, radius * 1.3f, radius * 2f })
                    {
                        var arena = DiceArenaLayout.Compute(aspect, count, radius, settings);
                        var starts = DiceArenaLayout.OffscreenStartPositions(arena, count, height);
                        Assert.That(starts.Length, Is.EqualTo(count));
                        for (var i = 0; i < count; i++)
                        {
                            var label = $"{aspect}/{count}/{height:F2} die {i}";
                            Assert.That(Mathf.Abs(starts[i].x), Is.LessThanOrEqualTo(arena.InnerHalfWidth + .001f), label);
                            Assert.That(starts[i].y + radius, Is.LessThan(arena.NearZ), label);
                            Assert.That(SphereTop(arena, new Vector3(starts[i].x, height, starts[i].y), radius),
                                Is.LessThan(-arena.OrthographicSize), label);
                            for (var j = 0; j < i; j++)
                                Assert.That(Vector2.Distance(starts[i], starts[j]), Is.GreaterThanOrEqualTo(radius * 2f), label);
                        }
                    }
        }

        [Test]
        public void CommonBottomRecall_OneTranslationHidesEveryRestedDieBelowTheScreen()
        {
            var settings = new DiceArenaSettings();
            const float radius = 1.75f;
            foreach (var aspect in new[] { 0.4f, 1f, 1.778f, 3.6f })
                foreach (var count in new[] { 1, 3, 20 })
                {
                    var arena = DiceArenaLayout.Compute(aspect, count, radius, settings);
                    var slots = DiceArenaLayout.OrderlyPositions(arena, count);
                    // Rested dice may also sit against the far wall after a throw.
                    var rested = slots.Select((slot, i) => new Vector3(slot.x, 1f, i % 2 == 0 ? slot.y : arena.FarZ - 1f)).ToArray();
                    foreach (var lift in new[] { 0f, radius * .4f })
                    {
                        var offset = DiceArenaLayout.RecallOffsetZ(arena, rested, lift);
                        Assert.That(offset, Is.LessThan(0f));
                        foreach (var die in rested)
                        {
                            var moved = die + new Vector3(0f, lift, offset);
                            Assert.That(DiceArenaLayout.IsBelowScreen(arena, moved, radius), Is.True, $"{aspect}/{count}/{lift}");
                            Assert.That(SphereTop(arena, moved, radius), Is.LessThan(-arena.OrthographicSize), $"{aspect}/{count}/{lift}");
                        }
                    }
                }
        }

        [Test]
        public void RestOverlap_AcceptsTouchingDiceThatTheOldRadiusRuleRejected()
        {
            const float radius = 1.75f, rest = 1f;
            var touching = new[] { Vector2.zero, new Vector2(rest * 2f, 0f) };
            Assert.That(DiceArenaLayout.HasOverlap(touching, radius * 1.5f), Is.True, "Old rule moved touching dice.");
            Assert.That(DiceArenaLayout.HasRestOverlap(touching, new[] { rest, rest }), Is.False);
            Assert.That(DiceArenaLayout.HasRestOverlap(new[] { Vector2.zero, new Vector2(rest * 1.5f, 0f) }, new[] { rest, rest }), Is.True);
            var arena = DiceArenaLayout.Compute(1.6f, 3, radius, new DiceArenaSettings());
            Assert.That(arena.HasSameBounds(DiceArenaLayout.Compute(1.6f, 3, radius, new DiceArenaSettings())), Is.True);
            Assert.That(arena.HasSameBounds(DiceArenaLayout.Compute(0.6f, 3, radius, new DiceArenaSettings())), Is.False);
        }

        [Test]
        public void RollTimeout_NeverCutsTheLongestPresentationShort()
        {
            var settings = new DiceThrowSettings();
            var presentation = settings.MaximumThrowPresentationSeconds;
            Assert.That(presentation, Is.EqualTo(settings.RecallDuration + settings.ThrowDelay +
                settings.MaximumSimulationSeconds / settings.PlaybackSpeed + settings.ResultRevealDelay).Within(1e-4f));
            settings.SetThrowDelayForEditor(2f);
            Assert.That(settings.MaximumThrowPresentationSeconds, Is.EqualTo(presentation - .3f + 2f).Within(1e-4f), "Default delay is 0.3s.");
            var longest = TemporaryDiceRollMenuController.ResolveRollTimeout(3f, settings.MaximumThrowPresentationSeconds, 1f);
            Assert.That(longest, Is.GreaterThanOrEqualTo(settings.MaximumThrowPresentationSeconds + 1f - 1e-4f), "Maximum delay still fits.");
            settings.SetThrowDelayForEditor(.3f);
            var timeout = TemporaryDiceRollMenuController.ResolveRollTimeout(3f, presentation, 1f);
            Assert.That(timeout, Is.GreaterThanOrEqualTo(presentation + 1f - 1e-4f));
            Assert.That(timeout, Is.LessThanOrEqualTo(TemporaryDiceRollMenuController.MaximumRollTimeoutSeconds));
            Assert.That(TemporaryDiceRollMenuController.ResolveRollTimeout(3f, .5f, .5f), Is.EqualTo(3f), "The scene minimum is kept.");
            Assert.That(TemporaryDiceRollMenuController.ResolveRollTimeout(3f, 30f, 1f), Is.EqualTo(15f), "Upper bound.");
            Assert.That(TemporaryDiceRollMenuController.ResolveRollTimeout(.2f, .1f, -4f), Is.EqualTo(1f), "Lower bound.");
        }

        [Test]
        public void NearestEdge_PicksEachOfTheFourEdgesAndBreaksTiesBottomLeftRightTop()
        {
            var arena = DiceArenaLayout.Compute(1.6f, 3, 1.75f, new DiceArenaSettings());
            var halfWidth = arena.OrthographicSize * arena.Aspect;
            const float y = 1f;
            Assert.That(DiceArenaLayout.NearestEdge(arena, FloorAt(arena, 0f, -arena.OrthographicSize + 1f, y)), Is.EqualTo(DiceRecallEdge.Bottom));
            Assert.That(DiceArenaLayout.NearestEdge(arena, FloorAt(arena, -halfWidth + 1f, 0f, y)), Is.EqualTo(DiceRecallEdge.Left));
            Assert.That(DiceArenaLayout.NearestEdge(arena, FloorAt(arena, halfWidth - 1f, 0f, y)), Is.EqualTo(DiceRecallEdge.Right));
            Assert.That(DiceArenaLayout.NearestEdge(arena, FloorAt(arena, 0f, arena.OrthographicSize - 1f, y)), Is.EqualTo(DiceRecallEdge.Top));

            // Square view, screen centre: all four distances are equal -> Bottom.
            var square = new DiceArena(10f, 1f, 55f, 9f, -11f, 9f, 5f, 1.75f, 4f, 4, 4);
            Assert.That(DiceArenaLayout.NearestEdge(square, FloorAt(square, 0f, 0f, y)), Is.EqualTo(DiceRecallEdge.Bottom));
            // Left and right tie on the horizontal centre line nearer to the sides than to top or bottom -> Left.
            var wide = new DiceArena(10f, 0.5f, 55f, 4f, -11f, 9f, 5f, 1.75f, 4f, 2, 4);
            Assert.That(DiceArenaLayout.EdgesByDistance(wide, FloorAt(wide, 0f, 0f, y)),
                Is.EqualTo(new[] { DiceRecallEdge.Left, DiceRecallEdge.Right, DiceRecallEdge.Bottom, DiceRecallEdge.Top }));
            // Left/right equal but bottom/top equal and nearer: Bottom before Top.
            var flat = new DiceArena(10f, 3f, 55f, 29f, -11f, 9f, 5f, 1.75f, 4f, 6, 4);
            Assert.That(DiceArenaLayout.EdgesByDistance(flat, FloorAt(flat, 0f, 0f, y))[0], Is.EqualTo(DiceRecallEdge.Bottom));
            Assert.That(DiceArenaLayout.EdgesByDistance(flat, FloorAt(flat, 0f, 0f, y))[1], Is.EqualTo(DiceRecallEdge.Top));
        }

        [Test]
        public void RecallTargets_LeaveTheScreenPerpendicularToTheEdgeForAspectsCountsAndPitches()
        {
            const float radius = 1.75f, lift = radius * .4f;
            foreach (var aspect in new[] { 0.4f, 0.5625f, 1f, 1.778f, 3.6f })
                foreach (var count in new[] { 1, 3, 20 })
                    foreach (var pitch in new[] { 30f, 55f, 85f })
                    {
                        var computed = DiceArenaLayout.Compute(aspect, count, radius, new DiceArenaSettings());
                        var arena = new DiceArena(computed.OrthographicSize, computed.Aspect, pitch, computed.HalfWidth,
                            computed.NearZ, computed.FarZ, computed.Ceiling, radius, computed.Spacing, computed.Columns, computed.Rows);
                        foreach (var slot in DiceArenaLayout.OrderlyPositions(computed, count))
                            foreach (DiceRecallEdge edge in Enum.GetValues(typeof(DiceRecallEdge)))
                            {
                                var from = new Vector3(slot.x, 1f, slot.y);
                                var target = DiceArenaLayout.RecallTarget(arena, from, edge, lift);
                                var label = $"{aspect}/{count}/{pitch}/{edge}";
                                Assert.That(DiceArenaLayout.IsOffscreen(arena, target, radius), Is.True, label);
                                var halfWidth = arena.OrthographicSize * arena.Aspect;
                                var top = SphereTop(arena, target, radius);
                                var bottom = top - 2f * radius;
                                switch (edge)
                                {
                                    case DiceRecallEdge.Left:
                                        Assert.That(target.x + radius, Is.LessThan(-halfWidth), label);
                                        Assert.That(new Vector2(target.y, target.z), Is.EqualTo(new Vector2(from.y, from.z)), label);
                                        break;
                                    case DiceRecallEdge.Right:
                                        Assert.That(target.x - radius, Is.GreaterThan(halfWidth), label);
                                        Assert.That(new Vector2(target.y, target.z), Is.EqualTo(new Vector2(from.y, from.z)), label);
                                        break;
                                    case DiceRecallEdge.Bottom:
                                        Assert.That(top, Is.LessThan(-arena.OrthographicSize), label);
                                        Assert.That(target.x, Is.EqualTo(from.x), label);
                                        Assert.That(target.y, Is.EqualTo(from.y + lift).Within(1e-5f), label);
                                        break;
                                    default:
                                        Assert.That(bottom, Is.GreaterThan(arena.OrthographicSize), label);
                                        Assert.That(target.x, Is.EqualTo(from.x), label);
                                        Assert.That(target.z, Is.GreaterThanOrEqualTo(from.z), label);
                                        break;
                                }
                            }
                    }
        }

        [Test]
        public void RecallPlan_ReassignsCrossingPathsAndFallsBackToTheCommonDownwardRecall()
        {
            const float radius = 1.75f, rest = 1f, lift = radius * .4f;
            var arena = DiceArenaLayout.Compute(1.6f, 3, radius, new DiceArenaSettings());
            var halfWidth = arena.OrthographicSize * arena.Aspect;
            // A die near the left edge with another die right next to it on its way out: the second die's nearest edge
            // is also left, but the first die (nearer to the edge, slower) would be overtaken on screen.
            var nearLeft = FloorAt(arena, -halfWidth + 3f, 0f, rest);
            var behind = nearLeft + new Vector3(2.3f, 0f, 0f);
            var crowded = new[] { nearLeft, behind };
            var heights = new[] { rest, rest };
            Assert.That(DiceArenaLayout.NearestEdge(arena, behind), Is.EqualTo(DiceRecallEdge.Left));
            var plan = DiceArenaLayout.PlanRecall(arena, crowded, heights, lift);
            Assert.That(plan.Resolution, Is.Not.EqualTo(DiceRecallResolution.CommonBottom));
            Assert.That(DiceArenaLayout.FirstRecallConflict(arena, crowded, plan.Targets.ToArray(), heights, 2), Is.EqualTo(-1));
            foreach (var target in plan.Targets) Assert.That(DiceArenaLayout.IsOffscreen(arena, target, radius), Is.True);

            // Well separated dice keep their nearest edges.
            var spread = new[]
            {
                FloorAt(arena, -halfWidth + 3f, 0f, rest), FloorAt(arena, halfWidth - 3f, 0f, rest),
                FloorAt(arena, 0f, -arena.OrthographicSize + 3f, rest)
            };
            var free = DiceArenaLayout.PlanRecall(arena, spread, new[] { rest, rest, rest }, lift);
            Assert.That(free.Resolution, Is.EqualTo(DiceRecallResolution.NearestEdges));
            Assert.That(free.Edges, Is.EqualTo(new[] { DiceRecallEdge.Left, DiceRecallEdge.Right, DiceRecallEdge.Bottom }));

            // Dice that already overlap cannot be separated by any edge choice -> one common downward offset.
            var stacked = new[] { nearLeft, nearLeft + new Vector3(.5f, 0f, 0f) };
            var fallback = DiceArenaLayout.PlanRecall(arena, stacked, heights, lift);
            Assert.That(fallback.Resolution, Is.EqualTo(DiceRecallResolution.CommonBottom));
            Assert.That(fallback.Edges, Is.All.EqualTo(DiceRecallEdge.Bottom));
            Assert.That(fallback.Targets[0] - stacked[0], Is.EqualTo(fallback.Targets[1] - stacked[1]));
            foreach (var target in fallback.Targets) Assert.That(DiceArenaLayout.IsBelowScreen(arena, target, radius), Is.True);
        }

        [Test]
        public void RecallPlan_RestedLayoutsAlwaysProduceSeparatedOffscreenPaths()
        {
            const float radius = 1.75f, rest = 1f, lift = radius * .4f;
            var random = new System.Random(7);
            foreach (var aspect in new[] { 0.5f, 1.6f, 2.4f })
                foreach (var count in new[] { 3, 20 })
                {
                    var arena = DiceArenaLayout.Compute(aspect, count, radius, new DiceArenaSettings());
                    for (var layout = 0; layout < 10; layout++)
                    {
                        var positions = RandomRest(arena, count, rest, random);
                        var heights = Enumerable.Repeat(rest, count).ToArray();
                        var plan = DiceArenaLayout.PlanRecall(arena, positions, heights, lift);
                        foreach (var target in plan.Targets) Assert.That(DiceArenaLayout.IsOffscreen(arena, target, radius), Is.True);
                        if (plan.Resolution != DiceRecallResolution.CommonBottom)
                            Assert.That(DiceArenaLayout.FirstRecallConflict(arena, positions, plan.Targets.ToArray(), heights, count), Is.EqualTo(-1));
                    }
                }
        }

        // Rest positions inside the walls with rest footprints apart (rejection sampling).
        private static Vector3[] RandomRest(DiceArena arena, int count, float rest, System.Random random)
        {
            var result = new System.Collections.Generic.List<Vector3>();
            for (var attempt = 0; result.Count < count && attempt < 20000; attempt++)
            {
                var x = Mathf.Lerp(-arena.HalfWidth + rest, arena.HalfWidth - rest, (float)random.NextDouble());
                var z = Mathf.Lerp(arena.NearZ + rest, arena.FarZ - rest, (float)random.NextDouble());
                var candidate = new Vector3(x, rest, z);
                if (result.All(p => (p - candidate).magnitude >= rest * 2f)) result.Add(candidate);
            }
            Assert.That(result.Count, Is.EqualTo(count));
            return result.ToArray();
        }

        // Floor point (at height y) whose screen coordinates are (x, screenY).
        private static Vector3 FloorAt(DiceArena arena, float x, float screenY, float y) =>
            new(x, y, (screenY - y * Mathf.Cos(arena.Pitch * Mathf.Deg2Rad)) / Mathf.Sin(arena.Pitch * Mathf.Deg2Rad));

        // Independent of DiceArenaLayout's formula: the orthographic view-up vector of the pitched stage camera.
        private static float SphereTop(DiceArena arena, Vector3 center, float radius) =>
            Vector3.Dot(center, Quaternion.Euler(arena.Pitch, 0f, 0f) * Vector3.up) + radius;

        [Test]
        public void Trajectory_ReplayEndsAtTheSamePoseAtAnyFrameRate()
        {
            const int frames = 121;
            var positions = new Vector3[frames];
            var rotations = new Quaternion[frames];
            for (var i = 0; i < frames; i++)
            {
                positions[i] = new Vector3(i * .1f, Mathf.Abs(Mathf.Sin(i * .2f)), i * .05f);
                rotations[i] = Quaternion.Euler(i * 7f, i * 3f, i * 11f);
            }
            var trajectory = new DiceTrajectory(1, frames, .01f, positions, rotations);
            Assert.That(trajectory.Duration, Is.EqualTo(1.2f).Within(1e-4f));
            foreach (var fps in new[] { 30f, 60f, 120f, 144f })
            {
                var time = 0f; var playback = 1.1f; var sampled = 0;
                Vector3 position; Quaternion rotation;
                do { time += 1f / fps; sampled++; trajectory.Sample(0, time * playback, out position, out rotation); }
                while (time * playback < trajectory.Duration);
                Assert.That(Vector3.Distance(position, positions[frames - 1]), Is.LessThan(1e-4f), $"{fps}fps");
                Assert.That(Quaternion.Angle(rotation, rotations[frames - 1]), Is.LessThan(.01f), $"{fps}fps");
                Assert.That(sampled, Is.EqualTo(Mathf.CeilToInt(trajectory.Duration / playback * fps)).Within(1), $"{fps}fps");
            }
        }

        [Test]
        public void Configuration_ReplacesOnlyTheUnchangedDefaultD4InPlace()
        {
            var config = ScriptableObject.CreateInstance<TemporaryDiceConfiguration>();
            try
            {
                config.ConfigureForEditor(true, new[] { Make("temporary.d4", "D4", 4), Make("temporary.d6", "D6", 6), Make("temporary.d8", "D8", 8) });
                Assert.That(config.TryUpgradeKnownDefaultsForEditor(), Is.True);
                Assert.That(config.Dice.Select(d => d.Id), Is.EqualTo(new[] { TemporaryDiceConfiguration.SecondaryD6Id, "temporary.d6", "temporary.d8" }));
                var secondary = config.Dice[0];
                Assert.That(secondary.DisplayName, Is.EqualTo("D6 (보조)"));
                Assert.That(secondary.MinimumValue, Is.EqualTo(1));
                Assert.That(secondary.MaximumValue, Is.EqualTo(6));
                Assert.That(secondary.CreateRuntimeFaces(), Is.EqualTo(config.Dice[1].CreateRuntimeFaces()));
                Assert.That(config.TryUpgradeKnownDefaultsForEditor(), Is.False, "The upgrade is idempotent.");

                config.ConfigureForEditor(true, new[] { Make("temporary.d4", "My D4", 4), Make("temporary.d8", "D8", 8) });
                Assert.That(config.TryUpgradeKnownDefaultsForEditor(), Is.False, "A developer-renamed D4 is preserved.");
                Assert.That(config.Dice[0].Id, Is.EqualTo("temporary.d4"));
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }

        [Test]
        public void ConfigurationAsset_OwnsTwoMatchingD6AndOneD8()
        {
            var config = AssetDatabase.LoadAssetAtPath<TemporaryDiceConfiguration>(TemporaryDiceMenuBuilder.ConfigurationPath);
            Assert.That(config.Dice.Select(d => d.Id), Is.EqualTo(new[] { TemporaryDiceConfiguration.SecondaryD6Id, "temporary.d6", "temporary.d8" }));
            Assert.That(config.Dice[0].DisplayName, Is.EqualTo("D6 (보조)"));
            Assert.That(config.Dice[0].CreateRuntimeFaces().Select(f => (f.EffectKind, f.Amount)), Is.EqualTo(new[]
            {
                (DiceEffectKind.Attack, 1), (DiceEffectKind.Heal, 2), (DiceEffectKind.Attack, 3),
                (DiceEffectKind.Heal, 4), (DiceEffectKind.Attack, 5), (DiceEffectKind.Heal, 6)
            }));
            Assert.That(config.Dice[0].MaximumValue, Is.EqualTo(6));
        }

        [Test]
        public void History_ChangesScopeAtNewCombatAndTrimsOldRecordsByPolicy()
        {
            var history = new TemporaryDiceRollHistory();
            history.Configure(TemporaryDiceHistoryMode.RecentSessionCombats, 2);
            history.BeginCombat("run", "A");
            var roll = new PlayerDieRoll("instance", "temporary.d4", "D4", 1, 4,
                new DiceRollResult(2, new DiceFace(DiceEffectKind.Attack, 3)));
            var combat = new TemporaryCombatState(20, 20, 40, 2, 1);
            history.AddTurn("run", "A", new[] { roll }, combat.ExecuteTurn(new[] { roll.Result }));
            Assert.That(history.Snapshot().Count, Is.EqualTo(1));
            history.BeginCombat("run", "B");
            Assert.That(history.Snapshot()[0].NodeId, Is.EqualTo("A"));
            history.BeginCombat("run", "C");
            history.AddTurn("run", "C", new[] { roll },
                new TemporaryCombatState(20, 20, 40, 2, 1).ExecuteTurn(new[] { roll.Result }));
            Assert.That(history.Snapshot()[0].NodeId, Is.EqualTo("C"));
            Assert.That(history.CurrentTurns.Count, Is.EqualTo(1));
            history.Configure(TemporaryDiceHistoryMode.LatestCombatNode, 2);
            Assert.That(history.Snapshot().Count, Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => history.AddTurn("run", "A", new[] { roll },
                new TemporaryCombatState(20, 20, 40, 2, 1).ExecuteTurn(new[] { roll.Result })));
        }

        private static TemporaryDieDefinition Make(string id, string name, int faces)
        {
            var definition = new TemporaryDieDefinition();
            definition.ConfigureForEditor(id, name, faces);
            return definition;
        }
    }
}
