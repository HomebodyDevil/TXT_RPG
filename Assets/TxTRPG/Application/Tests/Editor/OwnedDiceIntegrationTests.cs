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
                    foreach (var start in DiceArenaLayout.ThrowStartPositions(arena, count))
                        Assert.That(Mathf.Abs(start.x), Is.LessThanOrEqualTo(arena.InnerHalfWidth + .001f), $"{aspect}/{count}");
                }
        }

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
