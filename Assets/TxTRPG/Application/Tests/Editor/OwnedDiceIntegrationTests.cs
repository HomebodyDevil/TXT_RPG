using System;
using NUnit.Framework;
using TxTRPG.Application.Dice;
using TxTRPG.Gameplay.Combat;
using TxTRPG.Gameplay.Dice;
using TxTRPG.UI.Dice;
using UnityEditor;
using UnityEngine;

namespace TxTRPG.Application.Tests
{
    public sealed class OwnedDiceIntegrationTests
    {
        [Test]
        public void GeneratedShapes_MapEveryFaceToAnIdentifiableCameraFacingPose()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DieShapeCatalog>(
                "Assets/TxTRPG/UI/Styles/Dice/DieShapeCatalog.asset");
            Assert.That(catalog, Is.Not.Null);
            foreach (var (id, count) in new[] { ("temporary.d4", 4), ("temporary.d6", 6), ("temporary.d8", 8) })
            {
                var shape = catalog.Find(id, count);
                Assert.That(shape, Is.Not.Null, id);
                var model = shape.ModelPrefab.GetComponent<DieModelView>();
                Assert.That(model.LabelCount, Is.EqualTo(count), id);
                var mesh = shape.ModelPrefab.GetComponent<MeshFilter>().sharedMesh;
                var verticesPerFace = id == "temporary.d6" ? 4 : 3;
                var target = id == "temporary.d4" ? new Vector3(0, .35f, -.94f).normalized :
                    new Vector3(0, .8f, -.6f).normalized;
                for (var face = 0; face < count; face++)
                {
                    var normal = mesh.normals[face * verticesPerFace];
                    Assert.That(Vector3.Dot(shape.PoseFor(face) * normal, target), Is.GreaterThan(.999f),
                        $"{id} face {face}");
                }
            }
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
    }
}
